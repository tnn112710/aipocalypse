using System.Collections.Generic;
using Aipocalypse.Core.Events;
using Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Tests
{
    public class WorldClockTests
    {
        ClockSettings _settings;
        IObjectResolver _resolver;
        WorldClock _clock;
        readonly List<HourChanged> _hours = new();
        readonly List<DayPhaseChanged> _phases = new();
        readonly List<DayStarted> _days = new();
        readonly List<TimeJumped> _jumps = new();
        readonly List<TimeSkipEnded> _skips = new();
        readonly List<System.IDisposable> _subscriptions = new();

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<ClockSettings>();
            _settings.SecondsPerGameHour = 30f;
            _settings.StartHour = 7f;

            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(_settings);
            builder.Register<WorldClock>(Lifetime.Singleton);
            _resolver = builder.Build();
            _clock = _resolver.Resolve<WorldClock>();

            _hours.Clear();
            _phases.Clear();
            _days.Clear();
            _jumps.Clear();
            _skips.Clear();
            _subscriptions.Add(_resolver.Resolve<ISubscriber<HourChanged>>().Subscribe(_hours.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<DayPhaseChanged>>().Subscribe(_phases.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<DayStarted>>().Subscribe(_days.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<TimeJumped>>().Subscribe(_jumps.Add));
            _subscriptions.Add(_resolver.Resolve<ISubscriber<TimeSkipEnded>>().Subscribe(_skips.Add));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
            _resolver.Dispose();
            Object.DestroyImmediate(_settings);
        }

        void Run(float seconds, float step = 1f / 60f)
        {
            for (float t = 0f; t < seconds - 1e-6f; t += step)
                _clock.Advance(Mathf.Min(step, seconds - t));
        }

        [Test]
        public void StartsAtStartHour_OnDayOne()
        {
            Assert.AreEqual(1, _clock.Now.Day);
            Assert.AreEqual(7f, _clock.Now.Hour, 1e-4f);
            Assert.AreEqual(DayPhase.Day, _clock.Phase);
        }

        [Test]
        public void ThirtySecondsPerHour_AdvancesOneHour()
        {
            Run(30f);

            Assert.AreEqual(8f, _clock.Now.Hour, 1e-3f);
            Assert.AreEqual(1, _hours.Count);
            Assert.AreEqual(8, _hours[0].Now.WholeHour);
        }

        [Test]
        public void FullDay_IsTwelveRealMinutes_AtThirtySecondsPerHour()
        {
            Assert.AreEqual(720d, _settings.RealSecondsPerDay, 1e-3d);

            _settings.MasterSpeed = 2f;
            Assert.AreEqual(360d, _settings.RealSecondsPerDay, 1e-3d);
        }

        [Test]
        public void PhaseSpeed_ShortensThatPhaseOnly()
        {
            _settings.Night = new PhaseTiming(20f, 3f);

            // Night is 20:00–05:00 (9 h): 270 s at speed 1, 90 s at speed 3. The other 15 h stay at 450 s.
            Assert.AreEqual(450d + 90d, _settings.RealSecondsPerDay, 1e-3d);
        }

        [Test]
        public void BigStep_PublishesEveryHourAndPhaseInOrder()
        {
            // One huge frame from 07:00 to 21:00 must still send each hour and Dusk → Night.
            _clock.Advance(14f * 30f);

            Assert.AreEqual(21f, _clock.Now.Hour, 1e-3f);
            Assert.AreEqual(14, _hours.Count);
            Assert.AreEqual(2, _phases.Count);
            Assert.AreEqual(DayPhase.Dusk, _phases[0].Current);
            Assert.AreEqual(DayPhase.Night, _phases[1].Current);
            Assert.AreEqual(18, _phases[0].Now.WholeHour);
        }

        [Test]
        public void FrameEndingJustBeforeBoundary_DoesNotSkipPhase()
        {
            _clock.SetTime(WorldTime.At(1, 17.99999f));
            _phases.Clear();

            _clock.Advance(0.001f);
            _clock.Advance(1f);

            Assert.AreEqual(1, _phases.Count);
            Assert.AreEqual(DayPhase.Dusk, _phases[0].Current);
        }

        [Test]
        public void Midnight_StartsNextDay()
        {
            _clock.SetTime(WorldTime.At(1, 23.5f));
            _days.Clear();

            Run(30f);

            Assert.AreEqual(2, _clock.Now.Day);
            Assert.AreEqual(1, _days.Count);
            Assert.AreEqual(2, _days[0].Day);
            Assert.AreEqual(DayPhase.Night, _clock.Phase);
        }

        [Test]
        public void JumpTo_GoesForwardToNextOccurrence_WithOneJumpEvent()
        {
            _clock.JumpTo(6f);

            Assert.AreEqual(2, _clock.Now.Day);
            Assert.AreEqual(6f, _clock.Now.Hour, 1e-4f);
            Assert.AreEqual(1, _jumps.Count);
            Assert.AreEqual(0, _hours.Count, "instant jumps don't replay hours");
            Assert.AreEqual(1, _days.Count);
            Assert.AreEqual(DayPhase.Dawn, _clock.Phase);
            Assert.AreEqual(1, _phases.Count);
        }

        [Test]
        public void NextHourCheat_SnapsToWholeHour()
        {
            _clock.SetTime(WorldTime.At(1, 7.7f));

            _clock.JumpToNextHour();
            Assert.AreEqual(8f, _clock.Now.Hour, 1e-4f);

            _clock.JumpToNextHour();
            Assert.AreEqual(9f, _clock.Now.Hour, 1e-4f);
        }

        [Test]
        public void NextPhaseCheat_CyclesPhases()
        {
            _clock.JumpToNextPhase();
            Assert.AreEqual(DayPhase.Dusk, _clock.Phase);
            Assert.AreEqual(18f, _clock.Now.Hour, 1e-4f);

            _clock.JumpToNextPhase();
            _clock.JumpToNextPhase();
            Assert.AreEqual(DayPhase.Dawn, _clock.Phase);
            Assert.AreEqual(2, _clock.Now.Day);
        }

        [Test]
        public void SkipTo_ReachesTargetInRealSeconds_AndPlaysHours()
        {
            _clock.SkipTo(13f, 3f);
            Assert.IsTrue(_clock.IsSkipping);

            Run(3.1f);

            Assert.IsFalse(_clock.IsSkipping);
            Assert.AreEqual(13f, _clock.Now.Hour, 0.01f, "normal speed resumes after the target");
            Assert.AreEqual(6, _hours.Count);
            Assert.AreEqual(1, _skips.Count);
        }

        [Test]
        public void JumpDuringSkip_EndsTheSkip()
        {
            _clock.SkipTo(13f, 3f);

            _clock.JumpToNextHour();

            Assert.IsFalse(_clock.IsSkipping);
            Assert.AreEqual(1, _skips.Count, "a sleep waiting for TimeSkipEnded must still hear it");
        }

        [Test]
        public void Freeze_StopsClock_AndSlowestRequestWins()
        {
            object cutscene = new();
            object tutorial = new();

            _clock.RequestSpeed(tutorial, 0.5f);
            _clock.Freeze(cutscene);
            Run(30f);
            Assert.AreEqual(7f, _clock.Now.Hour, 1e-4f);

            _clock.Release(cutscene);
            Run(30f);
            Assert.AreEqual(7.5f, _clock.Now.Hour, 1e-3f);

            _clock.Release(tutorial);
            Assert.AreEqual(1f, _clock.SpeedScale);
        }

        [Test]
        public void RealSecondsUntil_CountsPhaseSpeeds()
        {
            _settings.Dusk = new PhaseTiming(18f, 2f);

            // 07:00 → 18:00 at speed 1 (330 s), 18:00 → 19:00 at speed 2 (15 s).
            Assert.AreEqual(345d, _clock.RealSecondsUntil(19f), 1e-3d);
        }

        [Test]
        public void MoonPhase_IsFullOnFirstFullMoonDay_AndNewHalfACycleLater()
        {
            _settings.MoonCycleDays = 8;
            _settings.FirstFullMoonDay = 1;

            Assert.AreEqual(0.5d, _settings.MoonPhaseAt(WorldTime.At(1, 0f)), 1e-6d);
            Assert.AreEqual(0d, _settings.MoonPhaseAt(WorldTime.At(5, 0f)), 1e-6d);
        }
    }
}
