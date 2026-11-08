using MB13.Aipocalypse.Core.RaidSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class DeathTimeRuleTests
    {
        RaidSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RaidSettings>();
            _settings.DefeatWakeHour = 7f;
            _settings.HoursLostOnDeath = 1f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        static void AssertTime(WorldTime expected, WorldTime actual) =>
            Assert.AreEqual(expected.TotalHours, actual.TotalHours, 1e-6, $"expected {expected}, got {actual}");

        [Test]
        public void WakeTime_RaidDeathInTheEvening_WakesAtSevenTheNextDay()
        {
            AssertTime(WorldTime.At(6, 7f), DeathTimeRule.WakeTime(WorldTime.At(5, 21f), true, _settings));
        }

        [Test]
        public void WakeTime_RaidDeathAfterMidnight_WakesAtSevenThatDay()
        {
            AssertTime(WorldTime.At(6, 7f), DeathTimeRule.WakeTime(WorldTime.At(6, 2f), true, _settings));
        }

        [Test]
        public void WakeTime_RaidDeathByDay_WakesAtTheNextSeven()
        {
            AssertTime(WorldTime.At(4, 7f), DeathTimeRule.WakeTime(WorldTime.At(3, 10f), true, _settings));
        }

        [Test]
        public void WakeTime_OtherDeath_MovesTheClockOnHoursLostOnDeath()
        {
            AssertTime(WorldTime.At(3, 11f), DeathTimeRule.WakeTime(WorldTime.At(3, 10f), false, _settings));
        }

        [Test]
        public void WakeTime_OtherDeathLateAtNight_CrossesMidnight()
        {
            AssertTime(WorldTime.At(4, 0.5f), DeathTimeRule.WakeTime(WorldTime.At(3, 23.5f), false, _settings));
        }
    }
}
