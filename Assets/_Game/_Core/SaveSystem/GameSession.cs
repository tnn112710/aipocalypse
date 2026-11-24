using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MessagePipe;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root: the game being played. Which slot it saves to, its play time, saving and loading, a new game, back to the menu.
    // A load reloads the world scene and hands the save to it: the scene's WorldSaver restores every section on its first
    // frame. Pressing Play in a world scene in the editor, or Prototype in the main menu, starts a quick game: it saves to
    // the editor slot in the editor, and to no slot in a build until the player picks one (Save Game).
    public sealed class GameSession : ITickable, IDisposable
    {
        const float AutosaveRetrySeconds = 30f;
        static readonly LocalizedString NotInGame = Loc.UI("session.not_in_game");
        static readonly LocalizedString Loading = Loc.UI("session.loading");
        static readonly LocalizedString NoPlayer = Loc.UI("session.no_player");
        static readonly LocalizedString Down = Loc.UI("session.down");
        static readonly LocalizedString DuringRaid = Loc.UI("session.raid");
        static readonly LocalizedString AlreadyLoading = Loc.UI("session.already_loading");
        static readonly LocalizedString CantOpenScene = Loc.UI("session.cant_open_scene");
        static readonly LocalizedString LoadingSave = Loc.UI("session.loading_save");
        static readonly LocalizedString LoadingBackup = Loc.UI("session.loading_backup");
        static readonly LocalizedString NoSlot = Loc.UI("session.no_slot");
        static readonly LocalizedString NoSlotPicked = Loc.UI("session.no_slot_picked");
        static readonly LocalizedString NewGameIn = Loc.UI("session.new_game");
        static readonly LocalizedString LoadingPrototype = Loc.UI("session.loading_prototype");
        static readonly LocalizedString NoAutosaveSlot = Loc.UI("session.no_autosave_slot");
        static readonly LocalizedString SavingTo = Loc.UI("session.saving_to");
        static readonly LocalizedString SaveFailed = Loc.UI("session.save_failed");
        static readonly LocalizedString AutosavedTo = Loc.UI("session.autosaved_to");
        static readonly LocalizedString SavedTo = Loc.UI("session.saved_to");
        static readonly LocalizedString SectionsFailed = Loc.UI("session.sections_failed");

        readonly SaveService _saves;
        readonly SaveRegistry _sections;
        readonly SaveSettings _settings;
        readonly SceneLoader _scenes;
        readonly TimeService _time;
        readonly PlayerRegistry _players;
        readonly RandomService _random;
        readonly IPublisher<GameSaved> _saved;
        readonly IDisposable _subscriptions;

        // The save loaded or last written: its keys that no section claims today are written back unchanged.
        SaveData _carry;
        // A loaded save waiting for the world scene to start.
        SaveData _pending;
        // The world scene's saver while one is running.
        object _world;
        // A slot was picked for this game (New Game / Load); false for a quick game (Play in a world scene, Prototype).
        bool _started;
        bool _raid;
        bool _autosaveDue;
        float _autosaveRetryAt;
        // Counts games started; a write finishing after another game started doesn't touch the new one.
        int _game;

        public GameSession(SaveService saves, SaveRegistry sections, SaveSettings settings, SceneLoader scenes, TimeService time,
            PlayerRegistry players, RandomService random, IPublisher<GameSaved> saved, ISubscriber<RaidStarted> raidStarted,
            ISubscriber<RaidEnded> raidEnded)
        {
            _saves = saves;
            _sections = sections;
            _settings = settings;
            _scenes = scenes;
            _time = time;
            _players = players;
            _random = random;
            _saved = saved;
            _subscriptions = DisposableBag.Create(
                raidStarted.Subscribe(_ => _raid = true),
                raidEnded.Subscribe(_ => _raid = false));
        }

        // 1-based slot this game saves to (SaveService.EditorSlot in the editor's quick game); 0 for none yet.
        public int Slot { get; private set; }
        // Real seconds played in this game (not paused, not loading), across saves.
        public double PlayTime { get; private set; }
        public bool InWorld => _world != null;
        public bool IsLoading => _scenes.IsLoading;

        public void Dispose()
        {
            _subscriptions.Dispose();
            _saves.Flush();
        }

        public void Tick()
        {
            if (!InWorld || _scenes.IsLoading)
                return;
            if (!_time.IsPaused)
                PlayTime += Time.unscaledDeltaTime;
            // Waits out a raid or a death, then saves; a failed write is tried again a little later (FinishSave), never dropped.
            if (_autosaveDue && Time.unscaledTime >= _autosaveRetryAt && CanSave(out _))
            {
                _autosaveDue = false;
                if (!Save(Slot, autosave: true, out string message))
                    RetryAutosave(message);
            }
        }

        // Saving needs a living player in the world and no raid running (a raid's enemies and waves aren't saved).
        public bool CanSave(out string reason)
        {
            reason = !InWorld ? NotInGame.Text()
                : _scenes.IsLoading ? Loading.Text()
                : _players.Current == null ? NoPlayer.Text()
                : _players.Current.IsDead ? Down.Text()
                : _raid ? DuringRaid.Text()
                : null;
            return reason == null;
        }

        // Starts saving to the slot: the game is captured now, the file is written in the background and GameSaved tells
        // how it went. False (with the reason) when saving isn't allowed now.
        public bool Save(int slot, out string message) => Save(slot, false, out message);

        // Loads the slot: the world scene reloads and the save is restored on its first frame.
        public bool Load(int slot, out string message)
        {
            if (_scenes.IsLoading)
            {
                message = AlreadyLoading.Text();
                return false;
            }
            if (!_saves.TryRead(slot, out SaveData data, out message, out bool fromBackup))
                return false;
            string scene = string.IsNullOrEmpty(data.Header.Scene) ? _settings.WorldScene : data.Header.Scene;
            GameState before = State;
            _pending = data;
            // The world being left stays this session's until its scene unloads (WorldSaver.Dispose), so a load that
            // fails mid-way leaves it playable, saving to its own slot.
            if (!_scenes.Load(scene, () => Revert(before)))
            {
                _pending = null;
                message = CantOpenScene.Format(scene);
                return false;
            }
            Slot = slot;
            PlayTime = data.Header.PlayTimeSeconds;
            _carry = null;
            _started = true;
            _autosaveDue = false;
            _game++;
            string name = SaveFormat.SlotName(slot);
            message = (fromBackup ? LoadingBackup : LoadingSave).Format(name);
            return true;
        }

        // A fresh game in the slot from the world scene's start. The slot's old save stays on disk until the new game's
        // first save replaces it (and then remains as its .bak), so quitting before that loses nothing.
        public bool NewGame(int slot, out string message)
        {
            if (!_saves.IsValidSlot(slot))
            {
                message = NoSlot.Format(slot);
                return false;
            }
            if (!_scenes.Load(_settings.WorldScene, RevertTo(State)))
            {
                message = CantOpenScene.Format(_settings.WorldScene);
                return false;
            }
            // Before the scene starts, so its systems roll on the new game's seed from their first roll.
            _random.Reseed(NewSeed());
            Slot = slot;
            PlayTime = 0d;
            _pending = null;
            _carry = null;
            _started = true;
            _autosaveDue = false;
            _game++;
            message = NewGameIn.Format(SaveFormat.SlotName(slot));
            return true;
        }

        // A quick fresh game in the world scene, as pressing Play there in the editor: no slot to pick (see the class).
        public bool StartPrototype(out string message)
        {
            if (!_scenes.Load(_settings.WorldScene, RevertTo(State)))
            {
                message = CantOpenScene.Format(_settings.WorldScene);
                return false;
            }
            _random.Reseed(NewSeed());
            Slot = 0;
            PlayTime = 0d;
            _pending = null;
            _carry = null;
            _started = false;
            _autosaveDue = false;
            _game++;
            message = LoadingPrototype.Text();
            return true;
        }

        // Deletes a slot's save. The game being played keeps going (and saves there again later).
        public bool Delete(int slot, out string problem) => _saves.Delete(slot, out problem);

        public bool ReturnToMainMenu()
        {
            if (!_scenes.Load(_settings.MainMenuScene, RevertTo(State)))
                return false;
            _started = false;
            _pending = null;
            _carry = null;
            _autosaveDue = false;
            _game++;
            return true;
        }

        // Saves to the game's slot, then quits once the file is written (Quit waits for it).
        public bool SaveAndQuit(out string message)
        {
            if (!Save(Slot, out message))
                return false;
            Quit();
            return true;
        }

        public void Quit()
        {
            _saves.Flush();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // The world scene's first frame (WorldSaver): restores the loaded save, or starts a new game.
        internal void EnterWorld(object world)
        {
            _world = world;
            _raid = false;
            _autosaveDue = false;
            if (_pending == null)
                _sections.StartFresh();
            else
            {
                SaveData data = _pending;
                _pending = null;
                int failed = _sections.Restore(data);
                _carry = data;
                if (failed > 0)
                    Debug.LogWarning($"{SaveFormat.SlotName(Slot)} loaded; {failed} system(s) couldn't read their part and start fresh.");
                return;
            }
            if (!_started)
            {
                Slot = SaveService.HasEditorSlot ? SaveService.EditorSlot : 0;
                PlayTime = 0d;
                _carry = null;
                _started = true;
            }
        }

        internal void LeaveWorld(object world)
        {
            if (_world != world)
                return;
            _world = null;
            _autosaveDue = false;
        }

        // Saves to the current slot as soon as saving is allowed (WorldSaver, each morning).
        public void RequestAutosave()
        {
            if (!_settings.Autosave || !InWorld)
                return;
            if (Slot <= 0)
            {
                _saved.Publish(new GameSaved(0, true, false, NoAutosaveSlot.Text()));
                return;
            }
            _autosaveDue = true;
            _autosaveRetryAt = 0f;
        }

        bool Save(int slot, bool autosave, out string message)
        {
            if (!_saves.IsValidSlot(slot))
            {
                message = slot == 0 ? NoSlotPicked.Text() : NoSlot.Format(slot);
                return false;
            }
            if (!CanSave(out message))
                return false;
            SaveData data = _saves.Create(_carry);
            int failed = _sections.Capture(data);
            SaveHeader header = data.Header;
            header.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
            header.PlayTimeSeconds = PlayTime;
            header.Scene = SceneManager.GetActiveScene().name;
            header.Build = Application.version;
            Task<string> write = _saves.WriteAsync(slot, data);
            Slot = slot;
            _carry = data;
            FinishSave(write, slot, autosave, failed, _game).Forget();
            message = SavingTo.Format(SaveFormat.SlotName(slot));
            return true;
        }

        // Back on the main thread once the file is written: tells everyone (toast, slot list), retries a failed autosave.
        async UniTaskVoid FinishSave(Task<string> write, int slot, bool autosave, int failedSections, int game)
        {
            string problem = await write;
            string name = SaveFormat.SlotName(slot);
            string message;
            if (problem != null)
            {
                message = SaveFailed.Format(problem);
                if (autosave && game == _game)
                    RetryAutosave(message);
            }
            else
            {
                message = (autosave ? AutosavedTo : SavedTo).Format(name);
                if (failedSections > 0)
                    message += SectionsFailed.Format(failedSections);
                Debug.Log(message);
            }
            _saved.Publish(new GameSaved(slot, autosave, problem == null, message));
        }

        void RetryAutosave(string message)
        {
            _autosaveDue = true;
            _autosaveRetryAt = Time.unscaledTime + AutosaveRetrySeconds;
            Debug.LogWarning($"{message}; trying again in {AutosaveRetrySeconds:0} s");
        }

        // What starting another game changes, put back when its scene load throws part-way and the old world keeps running.
        readonly struct GameState
        {
            public readonly int Slot;
            public readonly double PlayTime;
            public readonly SaveData Carry;
            public readonly bool Started;
            public readonly bool AutosaveDue;
            public readonly int Game;

            public GameState(int slot, double playTime, SaveData carry, bool started, bool autosaveDue, int game)
            {
                Slot = slot;
                PlayTime = playTime;
                Carry = carry;
                Started = started;
                AutosaveDue = autosaveDue;
                Game = game;
            }
        }

        GameState State => new(Slot, PlayTime, _carry, _started, _autosaveDue, _game);

        Action RevertTo(GameState state) => () => Revert(state);

        void Revert(GameState state)
        {
            Slot = state.Slot;
            PlayTime = state.PlayTime;
            _carry = state.Carry;
            _started = state.Started;
            _autosaveDue = state.AutosaveDue;
            _game = state.Game;
            _pending = null;
        }

        static ulong NewSeed() => (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32);
    }
}
