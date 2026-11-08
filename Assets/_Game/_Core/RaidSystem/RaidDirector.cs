using System.Collections.Generic;
using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.RaidSystem
{
    // Runs raid nights as pure timing over IRaidSpawner. Night N lasts from StartHour on day N to DawnHour on day N + 1 and is
    // read from WorldClock.Now every tick, so clock cheats and jumps behave; a night that ended for any reason never restarts.
    public sealed class RaidDirector : ITickable, IStartable
    {
        readonly WorldClock _clock;
        readonly RaidSettings _settings;
        readonly IRaidSchedule _schedule;
        readonly IRaidSpawner _spawner;
        readonly PlayerRegistry _players;
        readonly IPublisher<RaidStarted> _started;
        readonly IPublisher<RaidWaveStarted> _waveStarted;
        readonly IPublisher<RaidEnded> _ended;
        readonly HashSet<int> _handledNights = new();
        readonly List<PendingGroup> _groups = new();
        int _nextGroup;
        bool _manual;
        // Seconds since the raid started; when the wave started, fully spawned (-1 until then) and may spawn its next group.
        float _time;
        float _waveStartedAt;
        float _waveSpawnedAt;
        float _nextGroupAt;

        public RaidDirector(WorldClock clock, RaidSettings settings, IRaidSchedule schedule, IRaidSpawner spawner,
            PlayerRegistry players, IPublisher<RaidStarted> started, IPublisher<RaidWaveStarted> waveStarted,
            IPublisher<RaidEnded> ended)
        {
            _clock = clock;
            _settings = settings;
            _schedule = schedule;
            _spawner = spawner;
            _players = players;
            _started = started;
            _waveStarted = waveStarted;
            _ended = ended;
        }

        public bool IsRunning { get; private set; }
        // Night (the day it starts on) of the running or last raid; 0 before any.
        public int Night { get; private set; }
        public RaidPlan Plan { get; private set; }
        // Index of the current wave; -1 before the first.
        public int Wave { get; private set; } = -1;
        public int Spawned { get; private set; }
        public int Alive => _spawner.Alive;
        // How the last raid ended; null while one runs or before any.
        public RaidEndReason? LastEnd { get; private set; }

        // The raid night `time` falls in; 0 by day.
        public static int NightOf(WorldTime time, RaidSettings settings)
        {
            float hour = time.Hour;
            if (hour >= settings.StartHour)
                return time.Day;
            return hour < settings.DawnHour ? time.Day - 1 : 0;
        }

        // Gets the pool ready for the current or coming night.
        public void Start()
        {
            RaidPlan plan = _schedule.PlanFor(NightOrToday(_clock.Now));
            if (plan != null)
                _spawner.Prepare(plan, _settings.AliveCap + _settings.CorpseHeadroom);
        }

        public void Tick() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            _time += deltaTime;
            int night = NightOf(_clock.Now, _settings);
            if (IsRunning && !_manual && night != Night)
                EndRaid(RaidEndReason.Dawn);
            if (!IsRunning && night > 0 && !_handledNights.Contains(night))
                Begin(night, manual: false);
            if (IsRunning)
                Run();
        }

        // Tonight's raid at once, even by day (console). It ignores the night's window: only Cleared, Stopped or Defeat end it.
        public void StartNow()
        {
            if (!IsRunning)
                Begin(NightOrToday(_clock.Now), manual: true);
        }

        // Starts the next wave now; the rest of the current one is skipped (console).
        public void NextWave()
        {
            if (IsRunning && Wave < Plan.Waves.Count - 1)
                StartWave(Wave + 1);
        }

        public void Stop() => EndRaid(RaidEndReason.Stopped);

        // Defeat and Stopped withdraw every raider at once (behind the death screen, or by hand); at Dawn they leave out of sight.
        public void EndRaid(RaidEndReason reason)
        {
            if (!IsRunning)
                return;
            switch (reason)
            {
                case RaidEndReason.Defeat:
                case RaidEndReason.Stopped:
                    _spawner.Withdraw(true);
                    break;
                case RaidEndReason.Dawn:
                    _spawner.Withdraw(false);
                    break;
            }
            IsRunning = false;
            _manual = false;
            _groups.Clear();
            _nextGroup = 0;
            LastEnd = reason;
            _ended.Publish(new RaidEnded(Night, reason));
        }

        int NightOrToday(WorldTime now)
        {
            int night = NightOf(now, _settings);
            return night > 0 ? night : now.Day;
        }

        // A night without a plan (or with no waves) is quiet, and handled all the same.
        void Begin(int night, bool manual)
        {
            _handledNights.Add(night);
            RaidPlan plan = _schedule.PlanFor(night);
            if (plan == null || plan.Waves.Count == 0)
                return;
            IsRunning = true;
            _manual = manual;
            Night = night;
            Plan = plan;
            Wave = -1;
            Spawned = 0;
            LastEnd = null;
            _time = 0f;
            _spawner.Prepare(plan, _settings.AliveCap + _settings.CorpseHeadroom);
            _started.Publish(new RaidStarted(night));
            StartWave(0);
        }

        // Splits each entry into groups of its card's size; the first group of each card in the wave plays its arrival cue.
        void StartWave(int index)
        {
            Wave = index;
            RaidWave wave = Plan.Waves[index];
            _waveStarted.Publish(new RaidWaveStarted(Night, index, wave.Count));
            _groups.Clear();
            _nextGroup = 0;
            foreach (RaidWaveEntry entry in wave.Entries)
            {
                if (entry.Card == null)
                    continue;
                bool announce = !Queued(entry.Card);
                for (int left = entry.Count; left > 0;)
                {
                    int size = Mathf.Min(left, entry.Card.PickGroupSize(Random.value));
                    _groups.Add(new PendingGroup(entry.Card, size, announce));
                    announce = false;
                    left -= size;
                }
            }
            _waveStartedAt = _time;
            _waveSpawnedAt = -1f;
            _nextGroupAt = _time;
        }

        void Run()
        {
            RaidWave wave = Plan.Waves[Wave];
            SpawnDue(wave);
            if (_nextGroup < _groups.Count)
                return;
            if (_waveSpawnedAt < 0f)
                _waveSpawnedAt = _time;
            int alive = _spawner.Alive;
            if (Wave == Plan.Waves.Count - 1)
            {
                if (alive == 0)
                    EndRaid(RaidEndReason.Cleared);
                return;
            }
            bool thinnedOut = alive <= wave.NextWhenAliveAtMost && _time - _waveSpawnedAt >= wave.MinBreather;
            if (!thinnedOut && _time - _waveStartedAt < wave.MaxWait)
                return;
            StartWave(Wave + 1);
            SpawnDue(Plan.Waves[Wave]);
        }

        // The wave's groups as they fall due, GroupInterval apart; a group that would pass AliveCap spawns what fits and keeps
        // the rest queued. Nothing spawns while the player lies dead, or while the spawner has no point ready (0: next tick).
        void SpawnDue(RaidWave wave)
        {
            if (_players.Current is { IsDead: true })
                return;
            while (_nextGroup < _groups.Count && _time >= _nextGroupAt)
            {
                int room = _settings.AliveCap - _spawner.Alive;
                if (room <= 0)
                    return;
                PendingGroup group = _groups[_nextGroup];
                int made = _spawner.SpawnGroup(group.Card, Mathf.Min(group.Count, room), wave.Zone, group.Announce);
                if (made <= 0)
                    return;
                Spawned += made;
                if (made >= group.Count)
                    _nextGroup++;
                else
                    _groups[_nextGroup] = new PendingGroup(group.Card, group.Count - made, false);
                _nextGroupAt = _time + wave.GroupInterval;
            }
        }

        bool Queued(EnemySpawnCard card)
        {
            foreach (PendingGroup group in _groups)
                if (group.Card == card)
                    return true;
            return false;
        }

        readonly struct PendingGroup
        {
            public readonly EnemySpawnCard Card;
            public readonly int Count;
            public readonly bool Announce;

            public PendingGroup(EnemySpawnCard card, int count, bool announce)
            {
                Card = card;
                Count = count;
                Announce = announce;
            }
        }
    }
}
