using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.ForecastSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Tests
{
    public class ForecastTests
    {
        const string PrefabPath = "Assets/_Game/Content/Structures/ForecastBoard/ForecastBoard.prefab";

        ForecastSettings _settings;
        IObjectResolver _resolver;
        ForecastService _forecast;
        int _changed;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<ForecastSettings>();
            var builder = new ContainerBuilder();
            builder.RegisterMessagePipe();
            builder.RegisterInstance(_settings);
            builder.Register<ForecastService>(Lifetime.Singleton);
            _resolver = builder.Build();
            _forecast = _resolver.Resolve<ForecastService>();
            _changed = 0;
            _resolver.Resolve<ISubscriber<ForecastChanged>>().Subscribe(_ => _changed++);
        }

        [TearDown]
        public void TearDown()
        {
            _resolver.Dispose();
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Fight_IsSmallInEveryPeriodByDefault()
        {
            foreach (NightPeriod period in Enum.GetValues(typeof(NightPeriod)))
                Assert.That(_forecast.Fight(period), Is.EqualTo(FightSize.Small), period.ToString());
        }

        [Test]
        public void Override_ChangesOnePeriodUntilCleared_AndAnnouncesEachChange()
        {
            _forecast.Override(NightPeriod.LateNight, FightSize.Boss);
            Assert.That(_forecast.Fight(NightPeriod.LateNight), Is.EqualTo(FightSize.Boss));
            Assert.That(_forecast.Fight(NightPeriod.Evening), Is.EqualTo(FightSize.Small));

            _forecast.ClearOverrides();
            Assert.That(_forecast.Fight(NightPeriod.LateNight), Is.EqualTo(FightSize.Small));
            Assert.That(_changed, Is.EqualTo(2));
        }

        [Test]
        public void Fight_IsNoneEverywhereWhenDisabled()
        {
            _settings.Enabled = false;
            _forecast.Override(NightPeriod.Evening, FightSize.Medium);
            foreach (NightPeriod period in Enum.GetValues(typeof(NightPeriod)))
                Assert.That(_forecast.Fight(period), Is.EqualTo(FightSize.None), period.ToString());
        }

        [Test]
        public void EveryForecastSettingsAsset_HasEveryPeriod()
        {
            string[] guids = AssetDatabase.FindAssets("t:ForecastSettings", new[] { "Assets/_Game" });
            Assert.That(guids, Is.Not.Empty, "no ForecastSettings asset found");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var present = new HashSet<NightPeriod>(AssetDatabase.LoadAssetAtPath<ForecastSettings>(path).Periods.Keys);
                foreach (NightPeriod period in Enum.GetValues(typeof(NightPeriod)))
                    Assert.That(present, Does.Contain(period), $"{path} has no entry for {period}");
            }
        }

        [Test]
        public void BoardPrefab_HasAPlaquePerPeriodAndAMarkPerFight()
        {
            var board = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<ForecastBoard>();
            Assert.That(board, Is.Not.Null, PrefabPath + " has no ForecastBoard");
            foreach (NightPeriod period in Enum.GetValues(typeof(NightPeriod)))
                Assert.That(board.Plaques.Count(p => p.Period == period && p.Mark != null), Is.EqualTo(1), $"plaque for {period}");
            foreach (FightSize fight in Enum.GetValues(typeof(FightSize)))
                if (fight != FightSize.None)
                    Assert.That(board.Marks.TryGetValue(fight, out Material mark) && mark != null, $"no mark for {fight}");
        }

        // ---- reveal ----

        [Test]
        public void DayOf_IsStillYesterdayBeforeTheNewForecastHour()
        {
            Assert.That(ForecastRules.DayOf(WorldTime.At(3, 5.9f), 6f), Is.EqualTo(2));
            Assert.That(ForecastRules.DayOf(WorldTime.At(3, 6f), 6f), Is.EqualTo(3));
        }

        [Test]
        public void IsRevealed_HiddenEachMorning_ShownOncePulledOrPastTheSelfRevealHour()
        {
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(2, 8f), 1, _settings), Is.False, "yesterday's pull");
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(2, 8f), 2, _settings), Is.True, "pulled today");
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(2, 18.9f), 1, _settings), Is.False, "before 19:00");
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(2, 19f), 1, _settings), Is.True, "self-revealed at 19:00");
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(3, 1.5f), 1, _settings), Is.True, "still shown through the night");
            Assert.That(ForecastRules.IsRevealed(WorldTime.At(3, 6f), 2, _settings), Is.False, "a new day hides it again");
        }

        [Test]
        public void Reveal_ShowsTodaysForecastUntilTheNextMorning_AndAnnouncesItLive()
        {
            using var clock = new ClockFixture(_settings);
            var events = new List<ForecastRevealChanged>();
            using var _ = clock.Resolver.Resolve<ISubscriber<ForecastRevealChanged>>().Subscribe(events.Add);
            clock.Clock.SetTime(WorldTime.At(2, 8f));
            clock.Reveal.Start();
            Assert.That(clock.Reveal.IsRevealed, Is.False);

            clock.Reveal.Reveal();
            Assert.That(clock.Reveal.IsRevealed, Is.True);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Revealed && events[0].Live, "a pull spins the board");

            clock.Clock.SetTime(WorldTime.At(3, 1f));
            Assert.That(clock.Reveal.IsRevealed, Is.True, "through the night");
            clock.Clock.SetTime(WorldTime.At(3, 7f));
            Assert.That(clock.Reveal.IsRevealed, Is.False, "the next morning");
            Assert.That(events[^1].Live, Is.False, "a jump just shows it");
        }

        [Test]
        public void Reveal_WithoutAClockOrWithHidingOff_AlwaysShows()
        {
            var reveal = RevealWithoutAClock();
            Assert.That(reveal.IsRevealed, Is.True, "no clock");

            using var clock = new ClockFixture(_settings);
            _settings.HideUntilRevealed = false;
            clock.Clock.SetTime(WorldTime.At(2, 8f));
            Assert.That(clock.Reveal.IsRevealed, Is.True, "hiding off");
        }

        [Test]
        public void SaveSection_RoundTripsTheRevealedDay()
        {
            using var clock = new ClockFixture(_settings);
            clock.Clock.SetTime(WorldTime.At(4, 9f));
            clock.Reveal.Reveal();
            var saveSettings = ScriptableObject.CreateInstance<SaveSettings>();
            try
            {
                SaveData data = new SaveService(saveSettings).Create();
                new ForecastSaveSection(new SaveRegistry(), clock.Reveal).Capture(data);

                using var loaded = new ClockFixture(_settings);
                loaded.Clock.SetTime(WorldTime.At(4, 9f));
                new ForecastSaveSection(new SaveRegistry(), loaded.Reveal).Restore(data);
                Assert.That(loaded.Reveal.RevealedDay, Is.EqualTo(4));
                Assert.That(loaded.Reveal.IsRevealed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(saveSettings);
            }
        }

        [Test]
        public void BoardPrefab_HasReelsAQuestionMarkAndAPullRope()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var board = prefab.GetComponent<ForecastBoard>();
            Assert.That(board.Unknown, Is.Not.Null, "no '?' material");
            foreach (ForecastBoard.Plaque plaque in board.Plaques)
                Assert.That(plaque.Reel != null && plaque.Mark.transform.IsChildOf(plaque.Reel), $"{plaque.Period}: the mark turns with its reel");
            var rope = prefab.GetComponentInChildren<ForecastRope>();
            Assert.That(rope, Is.Not.Null, "no pull rope");
            Assert.That(rope.GetComponent<Collider>(), Is.Not.Null, "the rope needs a collider to look at");
        }

        ForecastReveal RevealWithoutAClock() => new(_settings, _resolver.Resolve<IPublisher<ForecastRevealChanged>>(),
            _resolver.Resolve<ISubscriber<TimeJumped>>(), _resolver);

        // A real WorldClock and a ForecastReveal on it.
        sealed class ClockFixture : IDisposable
        {
            readonly ClockSettings _clockSettings = ScriptableObject.CreateInstance<ClockSettings>();
            public readonly IObjectResolver Resolver;
            public readonly WorldClock Clock;
            public readonly ForecastReveal Reveal;

            public ClockFixture(ForecastSettings settings)
            {
                var builder = new ContainerBuilder();
                builder.RegisterMessagePipe();
                builder.RegisterInstance(_clockSettings);
                builder.Register<WorldClock>(Lifetime.Singleton);
                builder.RegisterInstance(settings);
                builder.Register<ForecastReveal>(Lifetime.Singleton);
                Resolver = builder.Build();
                Clock = Resolver.Resolve<WorldClock>();
                Reveal = Resolver.Resolve<ForecastReveal>();
            }

            public void Dispose()
            {
                Resolver.Dispose();
                UnityEngine.Object.DestroyImmediate(_clockSettings);
            }
        }
    }
}
