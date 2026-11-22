using MB13.Aipocalypse.Core.EnemySystem;
using MB13.Aipocalypse.Core.Events;
using MB13.Aipocalypse.Core.RobotSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class RobotStateTests
    {
        RobotSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RobotSettings>();
            _settings.HitHold = 1.2f;
            _settings.KnockdownHits = 3;
            _settings.KnockdownWindow = 2.5f;
            _settings.KnockdownLie = 2.6f;
            _settings.KnockdownRise = 2f;
            _settings.SulkTime = 10f;
            _settings.GreetHold = 2.5f;
            _settings.StepExpiry = 0.5f;
            _settings.NightWake = 12f;
            _settings.CheerTime = 5f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        [Test]
        public void Hit_FlinchesForTheHitHold()
        {
            var state = new RobotState(_settings);

            Assert.AreEqual(RobotHit.Flinch, state.TakeHit(0f, 2));

            Assert.AreEqual(2, state.HitIndex);
            Assert.IsTrue(state.Hit(1.1f));
            Assert.IsFalse(state.Hit(1.3f));
            Assert.IsFalse(state.Down(0.5f));
        }

        [Test]
        public void Hit_EnoughInTheWindowKnockItDown()
        {
            var state = new RobotState(_settings);

            state.TakeHit(0f, 0);
            state.TakeHit(1f, 0);

            Assert.AreEqual(RobotHit.Knockdown, state.TakeHit(2f, 0));
            Assert.IsTrue(state.Lying(4.5f), "lies until KnockdownLie");
            Assert.IsFalse(state.Lying(4.7f));
            Assert.IsTrue(state.Down(6.5f), "then gets up over KnockdownRise");
            Assert.IsTrue(state.Hit(6.5f), "nothing below the flinch branch runs while it's down");
            Assert.IsFalse(state.Down(6.7f));
        }

        [Test]
        public void Hit_SpreadOutHitsDontKnockItDown()
        {
            var state = new RobotState(_settings);

            state.TakeHit(0f, 0);
            state.TakeHit(2f, 0);

            Assert.AreEqual(RobotHit.Flinch, state.TakeHit(4f, 0), "the first hit fell out of the 2.5 s window");
        }

        [Test]
        public void Hit_WhileDownDoesNothing()
        {
            var state = new RobotState(_settings);
            for (int i = 0; i < 3; i++)
                state.TakeHit(i * 0.1f, 0);

            Assert.AreEqual(RobotHit.None, state.TakeHit(1f, 1));
            Assert.AreEqual(0, state.HitIndex);
        }

        [Test]
        public void Knockdown_IsFollowedByASulk()
        {
            var state = new RobotState(_settings);
            for (int i = 0; i < 3; i++)
                state.TakeHit(0f, 0);

            Assert.IsTrue(state.Sulking(4.6f + 9.9f), "for SulkTime after getting up");
            Assert.IsFalse(state.Sulking(4.6f + 10.1f));
        }

        [Test]
        public void Pat_EndsTheSulkAndGreets()
        {
            var state = new RobotState(_settings);
            for (int i = 0; i < 3; i++)
                state.TakeHit(0f, 0);
            int pats = state.PatVersion;
            int greetings = state.GreetingVersion;

            state.Pat(6f);

            Assert.IsFalse(state.Sulking(6f));
            Assert.AreEqual(pats + 1, state.PatVersion);
            Assert.AreEqual(greetings + 1, state.GreetingVersion);
            Assert.IsTrue(state.Greeting(6f));
        }

        [Test]
        public void Greet_LookingOnKeepsTheSameGreeting()
        {
            var state = new RobotState(_settings);
            state.Greet(0f, fresh: true);
            int version = state.GreetingVersion;

            state.Greet(1f, fresh: false);

            Assert.AreEqual(version, state.GreetingVersion);
            Assert.IsTrue(state.Greeting(3.4f), "held GreetHold after the last look");
        }

        [Test]
        public void Asleep_OnlyAtNightUntilWoken()
        {
            var state = new RobotState(_settings);

            Assert.IsFalse(state.Asleep(night: false, 0f));
            Assert.IsTrue(state.Asleep(night: true, 0f));
            state.Wake(0f);
            Assert.IsFalse(state.Asleep(night: true, 11f));
            Assert.IsTrue(state.Asleep(night: true, 12.1f));
        }

        [Test]
        public void Greet_DoesNotWakeIt()
        {
            var state = new RobotState(_settings);

            state.Greet(0f, fresh: true);

            Assert.IsTrue(state.Asleep(night: true, 0.1f), "a glance at night doesn't wake it; E, a pat or a whistle does");
        }

        [Test]
        public void Step_RequestGoesStale()
        {
            var state = new RobotState(_settings);

            state.RequestStep(Vector3.one, 0f);

            Assert.IsTrue(state.StepRequested(0.4f));
            Assert.AreEqual(Vector3.one, state.StepFrom);
            Assert.IsFalse(state.StepRequested(0.6f));
        }

        [Test]
        public void Step_AHitDropsTheRequest()
        {
            var state = new RobotState(_settings);
            state.RequestStep(Vector3.one, 0f);

            state.TakeHit(0.1f, 0);

            Assert.IsFalse(state.StepRequested(0.2f));
        }

        [Test]
        public void Cheer_LastsCheerTime()
        {
            var state = new RobotState(_settings);

            state.CheerFor(RobotCheer.Win, 0f);

            Assert.IsTrue(state.Cheering(4.9f));
            Assert.IsFalse(state.Cheering(5.1f));
            state.CheerFor(RobotCheer.None, 6f);
            Assert.IsFalse(state.Cheering(6f));
        }

        [TestCase(RaidEndReason.Cleared, false, RobotCheer.Win)]
        [TestCase(RaidEndReason.Dawn, false, RobotCheer.Win)]
        [TestCase(RaidEndReason.Cleared, true, RobotCheer.Cry)]
        [TestCase(RaidEndReason.Defeat, false, RobotCheer.Cry)]
        [TestCase(RaidEndReason.Stopped, true, RobotCheer.None)]
        public void Raid_EndDecidesTheCheer(RaidEndReason reason, bool barrierBroken, RobotCheer expected) =>
            Assert.AreEqual(expected, RobotManager.CheerFor(reason, barrierBroken));

        [Test]
        public void Homecoming_WelcomesAfterBeingAwayLongEnough()
        {
            var homecoming = new RobotHomecoming();

            Assert.IsFalse(homecoming.Update(true, 0f, 60f), "first sighting");
            Assert.IsFalse(homecoming.Update(false, 10f, 60f), "left");
            Assert.IsFalse(homecoming.Update(false, 50f, 60f));
            Assert.IsTrue(homecoming.Update(true, 70f, 60f), "back after 60 s");
            Assert.IsFalse(homecoming.Update(true, 71f, 60f), "once");
        }

        [Test]
        public void Homecoming_ShortTripsAreNotWelcomed()
        {
            var homecoming = new RobotHomecoming();
            homecoming.Update(true, 0f, 60f);
            homecoming.Update(false, 10f, 60f);

            Assert.IsFalse(homecoming.Update(true, 30f, 60f));
        }

        [Test]
        public void Homecoming_StartingOutsideIsNotAHomecoming()
        {
            var homecoming = new RobotHomecoming();

            homecoming.Update(false, 0f, 60f);

            Assert.IsFalse(homecoming.Update(true, 5f, 60f), "only 5 s outside since it was first seen");
        }

        [Test]
        public void NavTags_WalkersStayOffRobotsOnlyGround()
        {
            Assert.IsFalse(NavTags.Allows(NavTags.Walkers, NavTags.RobotsOnly));
            Assert.IsTrue(NavTags.Allows(NavTags.Walkers, 0));
            Assert.IsTrue(NavTags.Allows(NavTags.All, NavTags.RobotsOnly));
        }

        [Test]
        public void Call_SecondWhistleKeepsWhenItWasCalled()
        {
            var call = new RobotCall();

            call.Begin(Vector3.zero, Vector3.zero, 1f);
            call.Begin(Vector3.one, Vector3.zero, 3f);

            Assert.AreEqual(1f, call.BegunAt);
            Assert.AreEqual(Vector3.one, call.MeetPoint);
        }
    }
}
