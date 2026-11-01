using System;
using System.Collections.Generic;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.RaidSystem;
using Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Aipocalypse.Tests
{
    public sealed class RaidDirectorTests
    {
        ClockSettings _clockSettings;
        RaidSettings _settings;
        RaidPlan _plan;
        EnemySpawnCard _card;
        FakeSpawner _spawner;
        FakeSchedule _schedule;
        IObjectResolver _resolver;
        WorldClock _clock;
        RaidDirector _director;
        readonly List<RaidStarted> _starts = new();
        readonly List<RaidWaveStarted> _waves = new();
        readonly List<RaidEnded> _ends = new();
        readonly List<IDisposable> _subscriptions = new();

        [SetUp]
        public void SetUp()
        {
            _clockSettings = ScriptableObject.CreateInstance<ClockSettings>();
            _clockSettings.SecondsPerGameHour = 30f;
            _clockSettings.StartHour = 7f;

            _card = ScriptableObject.CreateInstance<EnemySpawnCard>();
            _card.GroupSize = new Vector2Int(2, 2);
            _plan = ScriptableObject.CreateInstance<RaidPlan>();
            UsePlan(Wave(5), Wave(6), Wave(7));
            _settings = ScriptableObject.CreateInstance<RaidSettings>();
            _settings.Plan = _plan;
            _settings.AliveCap = 12;

            _spawner = new FakeSpawner();
            _schedule = new FakeSchedule { Plan = _plan };

            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(_clockSettings);
            builder.Register<WorldClock>(Lifetime.Singleton);
            builder.Register<PlayerRegistry>(Lifetime.Singleton);
            builder.RegisterInstance(_settings);
            builder.RegisterInstance<IRaidSchedule>(_schedule);
            builder.RegisterInstance<IRaidSpawner>(_spawner);
            builder.Register<RaidDirector>(Lifetime.Singleton);
            builder.Register<DeathTimeRule>(Lifetime.Singleton);
            _resolver = builder.Build();
            _clock = _resolver.Resolve<WorldClock>();
            _director = _resolver.Resolve<RaidDirector>();

            _starts.Clear();
            _waves.Clear();
            _ends.Clear();
            _subscriptions.Add(_resolver.Resolve<ISubscriber<RaidStarted>>().Subscribe(_starts.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<RaidWaveStarted>>().Subscribe(_waves.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<RaidEnded>>().Subscribe(_ends.Add));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IDisposable subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            _resolver.Dispose();
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_plan);
            Object.DestroyImmediate(_card);
            Object.DestroyImmediate(_clockSettings);
        }

        RaidWave Wave(int count, float interval = 4f, int nextAtMost = 2, float breather = 15f, float maxWait = 90f)
        {
            var wave = new RaidWave { GroupInterval = interval, NextWhenAliveAtMost = nextAtMost, MinBreather = breather, MaxWait = maxWait };
            wave.Entries.Add(new RaidWaveEntry(_card, count));
            return wave;
        }

        void UsePlan(params RaidWave[] waves)
        {
            _plan.Waves.Clear();
            _plan.Waves.AddRange(waves);
        }

        // Clock and director together, as their ticks run in play.
        void Run(float seconds, float step = 0.1f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += step)
            {
                float delta = Mathf.Min(step, seconds - t);
                _clock.Advance(delta);
                _director.Advance(delta);
            }
        }

        void StartAt(int day, float hour)
        {
            _clock.SetTime(WorldTime.At(day, hour));
            Run(0.1f);
        }

        [Test]
        public void Raid_StartsWhenTheClockReachesStartHour_AndPublishes()
        {
            _director.Start();
            Assert.AreEqual(1, _spawner.Prepared, "Start prewarms for the coming night");

            _clock.SetTime(WorldTime.At(1, 19.9f));
            Run(2f);
            Assert.IsFalse(_director.IsRunning, "nothing before 20:00");
            Assert.AreEqual(0, _starts.Count);
            Assert.AreEqual(0, _spawner.Groups.Count);

            Run(2f);
            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(1, _director.Night);
            Assert.AreEqual(1, _starts.Count);
            Assert.AreEqual(1, _starts[0].Night);
            Assert.AreEqual(1, _waves.Count);
            Assert.AreEqual(0, _waves[0].Wave);
            Assert.AreEqual(5, _waves[0].Count);
            Assert.AreEqual(2, _spawner.Prepared, "the raid prewarms again when it starts");
            Assert.AreEqual(_settings.AliveCap + _settings.CorpseHeadroom, _spawner.PreparedInstances, "room for the bodies still lying");
            Assert.AreEqual(1, _spawner.Groups.Count, "the first group spawns at once");
            Assert.AreEqual("Front", _spawner.Groups[0].Zone);
        }

        [Test]
        public void Groups_ArePacedByGroupInterval_AndOnlyTheFirstAnnounces()
        {
            StartAt(1, 20f);
            Assert.AreEqual(1, _spawner.Groups.Count);

            Run(3.5f);
            Assert.AreEqual(1, _spawner.Groups.Count);

            Run(1f);
            Assert.AreEqual(2, _spawner.Groups.Count);

            Run(4f);
            Run(10f);
            Assert.AreEqual(3, _spawner.Groups.Count);
            CollectionAssert.AreEqual(new[] { 2, 2, 1 }, _spawner.Groups.ConvertAll(g => g.Count));
            CollectionAssert.AreEqual(new[] { true, false, false }, _spawner.Groups.ConvertAll(g => g.Announce));
            Assert.AreEqual(5, _director.Spawned);
        }

        [Test]
        public void Groups_WaitAtTheAliveCap_AndSpawnWhatFits()
        {
            _settings.AliveCap = 3;
            StartAt(1, 20f);
            Run(4.5f);
            CollectionAssert.AreEqual(new[] { 2, 1 }, _spawner.Groups.ConvertAll(g => g.Count), "the second group fills the cap only");

            Run(4.5f);
            Assert.AreEqual(2, _spawner.Groups.Count, "no room: the rest waits");

            _spawner.Alive = 1;
            Run(0.2f);
            Run(4.5f);
            CollectionAssert.AreEqual(new[] { 2, 1, 1, 1 }, _spawner.Groups.ConvertAll(g => g.Count));
            CollectionAssert.AreEqual(new[] { true, false, false, false }, _spawner.Groups.ConvertAll(g => g.Announce));
            Assert.LessOrEqual(_spawner.MostAlive, 3);
            Assert.AreEqual(5, _director.Spawned);
        }

        [Test]
        public void Groups_RetryNextTick_WhenNoSpawnPointIsReady()
        {
            _spawner.Blocked = true;
            StartAt(1, 20f);
            Run(1f);
            Assert.AreEqual(0, _spawner.Groups.Count);

            _spawner.Blocked = false;
            Run(0.1f);
            Assert.AreEqual(1, _spawner.Groups.Count);
            Assert.IsTrue(_spawner.Groups[0].Announce, "the wave's cue still plays with its first group");
        }

        [Test]
        public void NextWave_StartsOnceFewAreAlive()
        {
            UsePlan(Wave(4, nextAtMost: 2, breather: 15f), Wave(3));
            StartAt(1, 20f);
            Run(20f);
            Assert.AreEqual(0, _director.Wave, "4 alive: the wave isn't down to 2");

            _spawner.Alive = 2;
            Run(0.1f);
            Assert.AreEqual(1, _director.Wave);
            Assert.AreEqual(2, _waves.Count);
            Assert.AreEqual(1, _waves[1].Wave);
            Assert.AreEqual(3, _waves[1].Count);
        }

        [Test]
        public void NextWave_WaitsForTheBreatherAfterTheWaveHasSpawned()
        {
            UsePlan(Wave(4, nextAtMost: 2, breather: 15f), Wave(3));
            StartAt(1, 20f);
            Run(4.5f);
            _spawner.Alive = 0;

            Run(13f);
            Assert.AreEqual(0, _director.Wave, "fully spawned at 4 s: the breather runs to 19 s");

            Run(3f);
            Assert.AreEqual(1, _director.Wave);
        }

        [Test]
        public void NextWave_StartsAfterMaxWait_EvenWithManyAlive()
        {
            UsePlan(Wave(4, nextAtMost: 0, maxWait: 30f), Wave(3));
            StartAt(1, 20f);
            Run(29f);
            Assert.AreEqual(0, _director.Wave);

            Run(2f);
            Assert.AreEqual(1, _director.Wave);
        }

        [Test]
        public void NextWave_NeverBeforeTheWaveHasFullySpawned()
        {
            UsePlan(Wave(4, nextAtMost: 10, breather: 0f, maxWait: 10f), Wave(3));
            StartAt(1, 20f);
            _spawner.Blocked = true;
            Run(60f);
            Assert.AreEqual(0, _director.Wave);
        }

        [Test]
        public void LastWave_ClearedWhenNoneAreAlive_AndNotRestartedThatNight()
        {
            UsePlan(Wave(2));
            StartAt(1, 20f);
            Run(1f);
            Assert.IsTrue(_director.IsRunning, "2 still alive");

            _spawner.Alive = 0;
            Run(0.1f);
            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(RaidEndReason.Cleared, _director.LastEnd);
            Assert.AreEqual(1, _ends.Count);
            Assert.AreEqual(RaidEndReason.Cleared, _ends[0].Reason);
            Assert.AreEqual(1, _ends[0].Night);
            Assert.AreEqual(0, _spawner.Withdrawals.Count, "nothing left to withdraw");

            Run(30f);
            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(1, _starts.Count);
        }

        [Test]
        public void JumpIntoTheWindow_MidNight_StartsThatNightsRaid()
        {
            _clock.SetTime(WorldTime.At(3, 2f));
            Run(0.1f);

            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(2, _director.Night, "02:00 on day 3 belongs to night 2");
        }

        [Test]
        public void LeavingTheWindow_EndsWithDawn_AndWithdrawsOutOfSight()
        {
            StartAt(1, 20f);
            _clock.SetTime(WorldTime.At(2, 5.95f));
            Run(0.1f);
            Assert.IsTrue(_director.IsRunning, "still night 1");

            Run(3f);
            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(RaidEndReason.Dawn, _director.LastEnd);
            Assert.AreEqual(RaidEndReason.Dawn, _ends[0].Reason);
            CollectionAssert.AreEqual(new[] { false }, _spawner.Withdrawals);
        }

        [TestCase(RaidEndReason.Defeat)]
        [TestCase(RaidEndReason.Stopped)]
        public void EndRaid_DefeatOrStopped_WithdrawsAtOnce_AndStaysOver(RaidEndReason reason)
        {
            StartAt(1, 20f);

            _director.EndRaid(reason);
            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(reason, _director.LastEnd);
            Assert.AreEqual(reason, _ends[0].Reason);
            CollectionAssert.AreEqual(new[] { true }, _spawner.Withdrawals);

            Run(10f);
            Assert.AreEqual(1, _starts.Count, "the night stays over");
        }

        [Test]
        public void NextNight_StartsAgain()
        {
            StartAt(1, 20f);
            _director.EndRaid(RaidEndReason.Defeat);

            StartAt(2, 20f);
            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(2, _director.Night);
            Assert.AreEqual(2, _starts.Count);
            Assert.IsNull(_director.LastEnd);
            Assert.AreEqual(0, _director.Wave);
        }

        [Test]
        public void StartNow_ByDay_StartsAtOnce_AndIgnoresTheWindow()
        {
            _director.StartNow();
            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(1, _director.Night, "by day: today's night");
            Assert.AreEqual(1, _starts.Count);

            Run(1f);
            Assert.IsTrue(_director.IsRunning, "a console raid doesn't end outside the window");
            Assert.AreEqual(1, _spawner.Groups.Count);

            StartAt(1, 20f);
            Assert.AreEqual(1, _starts.Count, "tonight's raid has run already");
            Assert.IsTrue(_director.IsRunning);
        }

        [Test]
        public void NextWave_ByHand_StartsTheNextWaveAtOnce()
        {
            StartAt(1, 20f);
            _director.NextWave();
            Run(0.1f);

            Assert.AreEqual(1, _director.Wave);
            Assert.AreEqual(2, _waves.Count);
            Assert.AreEqual(2, _spawner.Groups.Count);
        }

        [Test]
        public void QuietNight_StartsNothing_AndStaysQuiet()
        {
            _schedule.Quiet.Add(1);
            StartAt(1, 20f);
            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(0, _starts.Count);
            Assert.AreEqual(0, _spawner.Groups.Count);

            _schedule.Quiet.Clear();
            Run(1f);
            Assert.IsFalse(_director.IsRunning, "the night was handled when it began");

            StartAt(2, 20f);
            Assert.IsTrue(_director.IsRunning);
            Assert.AreEqual(2, _director.Night);
        }

        [Test]
        public void RaidStarted_SeesNoWaveYet_EvenAfterAnEarlierRaid()
        {
            var wavesAtStart = new List<int>();
            _subscriptions.Add(_resolver.Resolve<ISubscriber<RaidStarted>>().Subscribe(_ => wavesAtStart.Add(_director.Wave)));
            StartAt(1, 20f);
            _director.NextWave();
            _director.Stop();

            StartAt(2, 20f);
            CollectionAssert.AreEqual(new[] { -1, -1 }, wavesAtStart);
        }

        [Test]
        public void DeathDuringARaid_EndsItInDefeat_AndWakesAtDefeatWakeHour()
        {
            _resolver.Resolve<DeathTimeRule>().Start();
            StartAt(1, 21f);

            _resolver.Resolve<IAsyncPublisher<PlayerRespawning>>().Publish(new PlayerRespawning(Vector3.zero));

            Assert.IsFalse(_director.IsRunning);
            Assert.AreEqual(RaidEndReason.Defeat, _director.LastEnd);
            CollectionAssert.AreEqual(new[] { true }, _spawner.Withdrawals);
            Assert.AreEqual(WorldTime.At(2, _settings.DefeatWakeHour).TotalHours, _clock.Now.TotalHours, 1e-6);
        }

        [Test]
        public void DeathAfterDawn_WithdrawsTheLeftoversAtOnce_AndCostsHoursLostOnDeath()
        {
            _resolver.Resolve<DeathTimeRule>().Start();
            StartAt(1, 20f);
            _clock.SetTime(WorldTime.At(2, 6.5f));
            Run(0.1f);
            CollectionAssert.AreEqual(new[] { false }, _spawner.Withdrawals, "dawn: they leave out of sight");
            double before = _clock.Now.TotalHours;

            _resolver.Resolve<IAsyncPublisher<PlayerRespawning>>().Publish(new PlayerRespawning(Vector3.zero));

            CollectionAssert.AreEqual(new[] { false, true }, _spawner.Withdrawals, "behind the death screen they all go");
            Assert.AreEqual(RaidEndReason.Dawn, _director.LastEnd);
            Assert.AreEqual(before + _settings.HoursLostOnDeath, _clock.Now.TotalHours, 1e-6);
        }

        sealed class FakeSchedule : IRaidSchedule
        {
            public RaidPlan Plan;
            public readonly HashSet<int> Quiet = new();

            public RaidPlan PlanFor(int night) => Quiet.Contains(night) ? null : Plan;
        }

        // Every group spawns whole unless Blocked; each spawned enemy stays alive until a test sets Alive.
        sealed class FakeSpawner : IRaidSpawner
        {
            public readonly List<(int Count, string Zone, bool Announce)> Groups = new();
            public readonly List<bool> Withdrawals = new();
            public int Prepared;
            public int PreparedInstances;
            public bool Blocked;
            public int MostAlive;
            int _alive;

            public int Alive
            {
                get => _alive;
                set
                {
                    _alive = value;
                    MostAlive = Mathf.Max(MostAlive, value);
                }
            }

            public void Prepare(RaidPlan plan, int instances)
            {
                Prepared++;
                PreparedInstances = instances;
            }

            public int SpawnGroup(EnemySpawnCard card, int count, string zone, bool announce)
            {
                if (Blocked)
                    return 0;
                Groups.Add((count, zone, announce));
                Alive += count;
                return count;
            }

            public void Withdraw(bool now)
            {
                Withdrawals.Add(now);
                Alive = 0;
            }
        }
    }
}
