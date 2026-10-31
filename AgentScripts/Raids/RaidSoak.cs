// Raid soak test: the player fights a raid night by itself (cheat.autopilot) and the run is sampled until the raid ends.
// Enter Play in WorldMap first (editor auto tick on), then:
//   unity command script run --file AgentScripts/Raids/RaidSoak.cs --entry RaidSoak.Run --args '[600]' --timeout 660
// Args: maxSeconds (real time), god (false: hits hurt, and a death ends the raid as Defeat), infiniteAmmo (false: guns run dry
// and the autopilot swaps them), guns (gun definitions put into free hotbar slots; the first is selected). Sets cheat.god and
// cheat.infammo, turns cheat.autopilot on, jumps the clock just before the raid's start hour, samples the raid every 0.5 s and
// the frame time every frame, and turns the three cheats off at the end.
// Everything runs from EditorApplication.update: await continuations would run inside the PlayerLoop and count as frame cost.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.Combat;
using Aipocalypse.Core.Debugging;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.RaidSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class RaidSoak
{
    public static async Task<string> Run(float maxSeconds = 600f, bool god = true, bool infiniteAmmo = true,
        string guns = "AK47,MP5,PumpShotgun")
    {
        if (!Application.isPlaying)
            return Error("enter play mode in WorldMap first");
        Application.runInBackground = true;

        LifetimeScope scope = LifetimeScope.Find<GameLifetimeScope>();
        if (scope == null || scope.Container == null)
            return Error("no GameLifetimeScope");
        IObjectResolver container = scope.Container;
        if (!container.TryResolve(out RaidDirector director))
            return Error("no RaidDirector (no RaidInstaller on the GameLifetimeScope, or it didn't install)");
        if (!container.TryResolve(out WorldClock clock))
            return Error("no WorldClock");
        if (!container.TryResolve(out CommandRegistry commands))
            return Error("no CommandRegistry (editor and development builds only)");
        IPlayer player = container.Resolve<PlayerRegistry>().Current;
        if (player?.Equipment == null)
            return Error("no player");

        var soak = new Soak(container, director, commands, player, maxSeconds);
        try
        {
            if (player.IsDead)
                soak.Setup("revive", commands.Execute("cheat.revive"));
            soak.Setup("guns", GiveGuns(player, guns));
            soak.Setup("god", commands.Execute(god ? "cheat.god on" : "cheat.god off"));
            soak.Setup("infammo", commands.Execute(infiniteAmmo ? "cheat.infammo on" : "cheat.infammo off"));
            CommandResult autopilot = commands.Execute("cheat.autopilot on");
            soak.Setup("autopilot", autopilot);
            if (!autopilot.Succeeded)
            {
                soak.Stop();
                return Error("cheat.autopilot on failed: " + autopilot.Message);
            }

            float start = container.TryResolve(out RaidSettings settings) ? settings.StartHour : 20f;
            // 0.02 h before the start: about a second of real time at 45 s per game hour.
            float before = start - 0.02f;
            float hour = clock.Now.Hour;
            if (!director.IsRunning && (hour < before || hour >= start))
                clock.JumpTo(before);
            soak.Setup("clock", $"day {clock.Now.Day} {clock.Now.WholeHour:00}:{clock.Now.Minute:00}");
        }
        catch (Exception e)
        {
            soak.Stop();
            return Error("setup failed: " + e.Message);
        }
        return await soak.Start();
    }

    static string Error(string message) => $"{{ error: \"{message}\" }}";

    // Puts each named gun into a free hotbar slot (not one already carried), then selects the first named one.
    static string GiveGuns(IPlayer player, string names)
    {
        Equipment equipment = player.Equipment;
        Hotbar hotbar = equipment.Hotbar;
        GunDefinition[] definitions = AssetDatabase.FindAssets("t:" + nameof(GunDefinition), new[] { "Assets/_Game/Content" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<GunDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(d => d != null).ToArray();
        var report = new List<string>();
        int first = -1;
        foreach (string name in names.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0))
        {
            GunDefinition definition = definitions.FirstOrDefault(d => string.Equals(d.name, name, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                report.Add(name + " (not found)");
                continue;
            }
            int slot = FindGun(hotbar, definition);
            if (slot >= 0)
                report.Add(name + " (carried)");
            else
            {
                ItemInstance gun = definition.CreateInstance();
                if (equipment.GetPickUpAction(gun) != PickUpAction.Store)
                {
                    report.Add(name + " (hotbar full)");
                    continue;
                }
                equipment.TryPickUp(gun, out _);
                slot = hotbar.IndexOf(gun);
                report.Add(name);
            }
            if (first < 0)
                first = slot;
        }
        if (first >= 0)
            equipment.Select(first);
        return string.Join(", ", report);
    }

    static int FindGun(Hotbar hotbar, GunDefinition definition)
    {
        for (int i = 0; i < hotbar.Count; i++)
            if (hotbar[i] is GunInstance gun && gun.Gun == definition)
                return i;
        return -1;
    }

    sealed class Soak
    {
        const float SampleInterval = 0.5f;
        const float TimelineInterval = 30f;
        // No raid by then: something is off (clock frozen, director not ticking).
        const float StartTimeout = 30f;
        // The first frames include the script's in-memory compile.
        const int SkippedFrames = 3;
        // A longer present wait is the editor throttling a background window, not game cost.
        const float StallMs = 100f;
        const int ErrorsShown = 5;

        readonly TaskCompletionSource<string> _done = new();
        readonly RaidDirector _director;
        readonly CommandRegistry _commands;
        readonly EnemyRegistry _enemies;
        readonly IPlayer _player;
        readonly GameObject _playerRoot;
        readonly float _maxSeconds;
        readonly List<IDisposable> _subscriptions = new();
        readonly List<string> _setup = new();
        readonly List<string> _events = new();
        readonly List<string> _timeline = new();
        readonly List<string> _errors = new();
        readonly Dictionary<string, int> _errorCounts = new();
        readonly List<float> _frameMs = new(65536);
        readonly List<float> _gameMs = new(65536);
        ProfilerRecorder _loop;
        ProfilerRecorder _presentWait;

        float _startTime;
        float _nextSample;
        float _nextTimeline;
        int _lastFrame;
        int _frames;
        int _stalls;
        bool _started;
        bool _finished;

        int _maxWave = -1;
        int _maxSpawned;
        int _maxAlive;
        int _maxRegistered;
        float _minHealth = float.MaxValue;
        int _kills;
        int _playerKills;
        int _playerHits;
        int _headshots;
        int _hitsTaken;
        float _damageTaken;
        float _killGapFrom;
        float _longestKillGap;
        int _errorCount;
        int _warningCount;

        public Soak(IObjectResolver container, RaidDirector director, CommandRegistry commands, IPlayer player, float maxSeconds)
        {
            _director = director;
            _commands = commands;
            _enemies = container.Resolve<EnemyRegistry>();
            _player = player;
            _playerRoot = player.Transform.gameObject;
            _maxSeconds = maxSeconds;
            _subscriptions.Add(container.Resolve<ISubscriber<DamageDealt>>().Subscribe(OnDamage));
            _subscriptions.Add(container.Resolve<ISubscriber<RaidStarted>>().Subscribe(e =>
            {
                _started = true;
                Event($"start night {e.Night}");
            }));
            _subscriptions.Add(container.Resolve<ISubscriber<RaidWaveStarted>>().Subscribe(e => Event($"wave {e.Wave} x{e.Count}")));
            _subscriptions.Add(container.Resolve<ISubscriber<RaidEnded>>().Subscribe(e => Event($"end {e.Reason}")));
            Application.logMessageReceived += OnLog;
            _startTime = Time.realtimeSinceStartup;
        }

        float Elapsed => Time.realtimeSinceStartup - _startTime;

        public void Setup(string step, CommandResult result) => Setup(step, (result.Succeeded ? "" : "FAILED ") + (result.Message ?? "ok"));

        public void Setup(string step, string result) => _setup.Add($"{step}: {result}");

        public Task<string> Start()
        {
            _startTime = Time.realtimeSinceStartup;
            _killGapFrom = 0f;
            _lastFrame = Time.frameCount;
            _loop = ProfilerRecorder.StartNew(new ProfilerCategory("Internal"), "PlayerLoop");
            _presentWait = ProfilerRecorder.StartNew(ProfilerCategory.Render, "WaitForLastPresentation");
            EditorApplication.update += Tick;
            return _done.Task;
        }

        // Releases everything; the cheats are turned off while still in play.
        public void Stop()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            if (_loop.Valid)
                _loop.Dispose();
            if (_presentWait.Valid)
                _presentWait.Dispose();
            if (!Application.isPlaying)
                return;
            _commands.Execute("cheat.autopilot off");
            _commands.Execute("cheat.god off");
            _commands.Execute("cheat.infammo off");
        }

        void Tick()
        {
            if (_finished)
                return;
            try
            {
                if (!Application.isPlaying)
                {
                    Finish("play mode ended");
                    return;
                }
                if (Time.frameCount != _lastFrame)
                {
                    _lastFrame = Time.frameCount;
                    SampleFrame();
                }
                float t = Elapsed;
                if (t < _nextSample)
                    return;
                _nextSample = t + SampleInterval;
                SampleRaid(t);

                if (_started && !_director.IsRunning && _director.LastEnd.HasValue)
                    Finish(_director.LastEnd.Value.ToString());
                else if (!_started && t >= StartTimeout)
                    Finish($"never started ({StartTimeout:0} s)");
                else if (t >= _maxSeconds)
                    Finish($"timeout ({_maxSeconds:0} s, still running)");
            }
            catch (Exception e)
            {
                Finish("soak failed: " + e.Message);
            }
        }

        void SampleFrame()
        {
            if (++_frames <= SkippedFrames)
                return;
            _frameMs.Add(Time.unscaledDeltaTime * 1000f);
            if (!_loop.Valid || !_presentWait.Valid)
                return;
            float wait = _presentWait.LastValue * 1e-6f;
            if (wait > StallMs)
                _stalls++;
            _gameMs.Add(Mathf.Max(0f, _loop.LastValue * 1e-6f - wait));
        }

        void SampleRaid(float t)
        {
            bool running = _director.IsRunning;
            _started |= running;
            int alive = _director.Alive;
            float health = _player.Health != null ? _player.Health.Current : 0f;
            if (running)
            {
                _maxWave = Mathf.Max(_maxWave, _director.Wave);
                _maxSpawned = Mathf.Max(_maxSpawned, _director.Spawned);
                _maxAlive = Mathf.Max(_maxAlive, alive);
                _maxRegistered = Mathf.Max(_maxRegistered, _enemies.Count);
                _minHealth = Mathf.Min(_minHealth, health);
            }
            // Seconds raiders stayed alive without a kill: long gaps mean ones the autopilot can't see or reach.
            if (!running || alive == 0)
                _killGapFrom = t;
            else
                _longestKillGap = Mathf.Max(_longestKillGap, t - _killGapFrom);

            if (t < _nextTimeline)
                return;
            _nextTimeline = t + TimelineInterval;
            _timeline.Add($"{t:0} run={(running ? 1 : 0)} wave={_director.Wave} spawned={_director.Spawned} alive={alive} " +
                          $"registered={_enemies.Count} kills={_kills} hp={health:0}");
        }

        void OnDamage(DamageDealt hit)
        {
            if (ReferenceEquals(hit.Target, _player))
            {
                _hitsTaken++;
                _damageTaken += hit.Result.Applied;
                return;
            }
            if (hit.Target is not EnemyBody)
                return;
            bool byPlayer = hit.Info.Source == _playerRoot;
            if (byPlayer)
            {
                _playerHits++;
                if (hit.Info.Critical)
                    _headshots++;
            }
            if (!hit.Result.Killed)
                return;
            _kills++;
            if (byPlayer)
                _playerKills++;
            _killGapFrom = Elapsed;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                _warningCount++;
                return;
            }
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert))
                return;
            _errorCount++;
            string line = condition.Split('\n')[0];
            if (line.Length > 200)
                line = line.Substring(0, 200) + "…";
            if (_errorCounts.TryGetValue(line, out int count))
                _errorCounts[line] = count + 1;
            else
            {
                _errorCounts[line] = 1;
                if (_errors.Count < ErrorsShown)
                    _errors.Add(line);
            }
        }

        void Event(string what) => _events.Add($"{Elapsed:0.0} {what}");

        void Finish(string ended)
        {
            if (_finished)
                return;
            _finished = true;
            string report;
            try
            {
                report = Report(ended);
            }
            catch (Exception e)
            {
                report = Error("report failed: " + e.Message);
            }
            Stop();
            _done.TrySetResult(report);
        }

        string Report(string ended)
        {
            RaidPlan plan = _director.Plan;
            int planWaves = plan != null ? plan.Waves.Count : 0;
            int planEnemies = plan != null ? plan.TotalEnemies : 0;
            Health health = _player.Health;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  ended: \"{ended}\", seconds: {Elapsed:0.0}, night: {_director.Night},");
            sb.AppendLine($"  waves: {_maxWave + 1}/{planWaves}, spawned: {Math.Max(_maxSpawned, _director.Spawned)}/{planEnemies}, " +
                          $"alive: {_director.Alive} (max {_maxAlive}, registered max {_maxRegistered}),");
            sb.AppendLine($"  kills: {_kills} (player {_playerKills}; spawned - alive {_director.Spawned - _director.Alive}), " +
                          $"playerHits: {_playerHits} (headshots {_headshots}), longestKillGap: {_longestKillGap:0.0} s,");
            string minHealth = _minHealth < float.MaxValue ? _minHealth.ToString("0") : "-";
            sb.AppendLine($"  player: {{ minHealth: {minHealth}/{health?.Max:0}, hitsTaken: {_hitsTaken}, damageTaken: {_damageTaken:0}, " +
                          $"dead: {_player.IsDead} }},");
            sb.AppendLine($"  frameMs: {Stats(_frameMs)}, stalls: {_stalls},");
            sb.AppendLine($"  gameMs (PlayerLoop minus present wait): {Stats(_gameMs)},");
            string firstErrors = string.Join(", ", _errors.Select(e => "\"" + e + "\" x" + _errorCounts[e]));
            sb.AppendLine($"  errors: {_errorCount}, warnings: {_warningCount}, firstErrors: [{firstErrors}],");
            sb.AppendLine($"  setup: [{string.Join("; ", _setup)}],");
            sb.AppendLine($"  events: [{string.Join("; ", _events)}],");
            sb.AppendLine($"  timeline: [{string.Join("; ", _timeline)}]");
            sb.Append("}");
            return sb.ToString();
        }

        static string Stats(List<float> values)
        {
            if (values.Count == 0)
                return "{ frames: 0 }";
            values.Sort();
            float p95 = values[Mathf.Clamp((int)(values.Count * 0.95f), 0, values.Count - 1)];
            return $"{{ frames: {values.Count}, avg: {values.Average():0.0}, p95: {p95:0.0}, max: {values[values.Count - 1]:0.0} }}";
        }
    }
}
