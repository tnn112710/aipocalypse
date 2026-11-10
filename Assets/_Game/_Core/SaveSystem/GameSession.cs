using System;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Root: the game being played. Which slot it saves to, its play time, saving and loading, a new game, back to the menu.
    // A load reloads the world scene and hands the save to it: the scene's WorldSaver restores every section on its first
    // frame. Pressing Play in a world scene in the editor (no menu) starts a new game on SaveSettings.EditorSlot.
    public sealed class GameSession : ITickable, IDisposable
    {
        const float AutosaveRetrySeconds = 30f;

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
        // A menu started this session (New Game / Load); false when Play was pressed in a world scene.
        bool _started;
        bool _raid;
        bool _autosaveDue;
        float _autosaveRetryAt;

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

        // 1-based slot this game saves to; 0 before any game.
        public int Slot { get; private set; }
        // Real seconds played in this game (not paused, not loading), across saves.
        public double PlayTime { get; private set; }
        public bool InWorld => _world != null;
        public bool IsLoading => _scenes.IsLoading;

        public void Dispose() => _subscriptions.Dispose();

        public void Tick()
        {
            if (!InWorld || _scenes.IsLoading)
                return;
            if (!_time.IsPaused)
                PlayTime += Time.unscaledDeltaTime;
            // Waits out a raid or a death, then saves; a failed write is tried again a little later, never dropped.
            if (_autosaveDue && Time.unscaledTime >= _autosaveRetryAt && CanSave(out _))
            {
                if (Save(Slot, autosave: true, out string message))
                {
                    _autosaveDue = false;
                    Debug.Log(message);
                }
                else
                {
                    _autosaveRetryAt = Time.unscaledTime + AutosaveRetrySeconds;
                    Debug.LogWarning($"{message}; trying again in {AutosaveRetrySeconds:0} s");
                }
            }
        }

        // Saving needs a living player in the world and no raid running (a raid's enemies and waves aren't saved).
        public bool CanSave(out string reason)
        {
            reason = !InWorld ? "Not in a game"
                : _scenes.IsLoading ? "Loading"
                : _players.Current == null ? "No player"
                : _players.Current.IsDead ? "Can't save while down"
                : _raid ? "Can't save during a raid"
                : null;
            return reason == null;
        }

        public bool Save(int slot, out string message) => Save(slot, false, out message);

        // Loads the slot: the world scene reloads and the save is restored on its first frame.
        public bool Load(int slot, out string message)
        {
            if (_scenes.IsLoading)
            {
                message = "Already loading";
                return false;
            }
            if (!_saves.TryRead(slot, out SaveData data, out message, out bool fromBackup))
                return false;
            string scene = string.IsNullOrEmpty(data.Header.Scene) ? _settings.WorldScene : data.Header.Scene;
            _pending = data;
            // The world being left stays this session's until its scene unloads (WorldSaver.Dispose), so a load that
            // fails mid-way leaves it playable.
            if (!_scenes.Load(scene, () => _pending = null))
            {
                _pending = null;
                message = $"Can't open scene {scene}";
                return false;
            }
            Slot = slot;
            PlayTime = data.Header.PlayTimeSeconds;
            _carry = null;
            _started = true;
            _autosaveDue = false;
            message = fromBackup ? $"Save {slot} was damaged: loading its backup" : $"Loading Save {slot}";
            return true;
        }

        // A fresh game in the slot from the world scene's start. The slot's old save stays on disk until the new game's
        // first save replaces it (and then remains as its .bak), so quitting before that loses nothing.
        public bool NewGame(int slot, out string message)
        {
            if (!_saves.IsValidSlot(slot))
            {
                message = $"No slot {slot}";
                return false;
            }
            if (!_scenes.Load(_settings.WorldScene))
            {
                message = $"Can't open scene {_settings.WorldScene}";
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
            message = $"New game in Save {slot}";
            return true;
        }

        // Deletes a slot's save. The game being played keeps going (and saves there again later).
        public bool Delete(int slot) => _saves.Delete(slot);

        public bool ReturnToMainMenu()
        {
            if (!_scenes.Load(_settings.MainMenuScene))
                return false;
            _started = false;
            _pending = null;
            _carry = null;
            _autosaveDue = false;
            return true;
        }

        public void Quit()
        {
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
            if (_pending != null)
            {
                SaveData data = _pending;
                _pending = null;
                int failed = _sections.Restore(data);
                _carry = data;
                if (failed > 0)
                    Debug.LogWarning($"Save {Slot} loaded; {failed} system(s) couldn't read their part and start fresh.");
                return;
            }
            if (!_started)
            {
                Slot = Mathf.Clamp(_settings.EditorSlot, 1, _saves.SlotCount);
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
            if (!_settings.Autosave || Slot <= 0 || !InWorld)
                return;
            _autosaveDue = true;
            _autosaveRetryAt = 0f;
        }

        bool Save(int slot, bool autosave, out string message)
        {
            if (!CanSave(out message))
                return false;
            SaveData data = _saves.Create(_carry);
            int failed = _sections.Capture(data);
            SaveHeader header = data.Header;
            header.SavedAtUtcTicks = DateTime.UtcNow.Ticks;
            header.PlayTimeSeconds = PlayTime;
            header.Scene = SceneManager.GetActiveScene().name;
            header.Build = Application.version;
            if (!_saves.Write(slot, data, out string problem))
            {
                message = $"Save failed: {problem}";
                return false;
            }
            Slot = slot;
            _carry = data;
            _saved.Publish(new GameSaved(slot, autosave));
            message = autosave ? $"Autosaved to Save {slot}" : $"Saved to Save {slot}";
            if (failed > 0)
                message += $" ({failed} system(s) kept their last saved state: see the console)";
            return true;
        }

        static ulong NewSeed() => (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32);
    }
}
