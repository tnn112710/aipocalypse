using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.ForecastSystem;
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
    }
}
