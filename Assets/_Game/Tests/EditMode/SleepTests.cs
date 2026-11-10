using System.Collections.Generic;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class SleepTests
    {
        const float RaidStart = 20f;
        const float Dawn = 6f;

        // Raids every night except the handled ones.
        static SleepRaids Raids(bool running = false, params int[] handled)
        {
            var done = new HashSet<int>(handled);
            return new SleepRaids(running, RaidStart, Dawn, night => night > 0 && !done.Contains(night));
        }

        static SleepWindow Window(int day, float hour, SleepRaids? raids, int min = 1, int max = 12) =>
            SleepRules.Window(WorldTime.At(day, hour), min, max, 0.5f, raids);

        [Test]
        public void Window_WithoutRaids_RunsFromTheNextWholeHourToTheLongestSleep()
        {
            SleepWindow window = Window(1, 9.4f, null);

            Assert.IsTrue(window.CanSleep);
            Assert.AreEqual(WorldTime.At(1, 11f).TotalHours, window.Earliest.TotalHours, 1e-6);
            Assert.AreEqual(WorldTime.At(1, 21f).TotalHours, window.Latest.TotalHours, 1e-6);
            Assert.AreEqual(11, window.Choices);
        }

        [Test]
        public void Window_ByDay_NeverPassesTonightsRaid()
        {
            SleepWindow window = Window(1, 10f, Raids());

            Assert.IsTrue(window.CanSleep);
            Assert.AreEqual(WorldTime.At(1, 19f).TotalHours, window.Latest.TotalHours, 1e-6, "the last whole hour before 19:30");
        }

        [Test]
        public void Window_TooCloseToTheRaid_Refuses()
        {
            SleepWindow window = Window(1, 19.2f, Raids());

            Assert.IsFalse(window.CanSleep);
            Assert.AreEqual(SleepRules.RaidTooClose, window.Reason);
        }

        [Test]
        public void Window_DuringARaidOrANightStillToRaid_Refuses()
        {
            Assert.AreEqual(SleepRules.RaidRunning, Window(1, 21f, Raids(running: true)).Reason);
            Assert.AreEqual(SleepRules.RaidTonight, Window(1, 21f, Raids()).Reason);
            Assert.AreEqual(SleepRules.RaidTonight, Window(2, 3f, Raids()).Reason, "before dawn is still night 1");
        }

        [Test]
        public void Window_AfterTonightsRaid_ReachesIntoTheNextDayUpToItsRaid()
        {
            SleepWindow evening = Window(1, 23f, Raids(false, 1));
            Assert.IsTrue(evening.CanSleep);
            Assert.AreEqual(WorldTime.At(2, 11f).TotalHours, evening.Latest.TotalHours, 1e-6, "12 hours, well before night 2");

            SleepWindow beforeDawn = Window(2, 4f, Raids(false, 1), max: 24);
            Assert.AreEqual(WorldTime.At(2, 19f).TotalHours, beforeDawn.Latest.TotalHours, 1e-6, "night 2 starts today at 20:00");
        }

        [Test]
        public void DefaultWake_IsTheNextDefaultHourInReachElseTheLatest()
        {
            SleepWindow night = Window(1, 23f, Raids(false, 1));
            Assert.AreEqual(WorldTime.At(2, 7f).TotalHours, SleepRules.DefaultWake(night, 7f).TotalHours, 1e-6);

            SleepWindow day = Window(1, 10f, Raids());
            Assert.AreEqual(day.Latest.TotalHours, SleepRules.DefaultWake(day, 7f).TotalHours, 1e-6);
        }
    }
}
