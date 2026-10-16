using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.TimeSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class RepairQteTests
    {
        TimingQteChallenge _challenge;
        GunDefinition _definition;
        GunRepairProfile _repair;

        [SetUp]
        public void SetUp()
        {
            _challenge = ScriptableObject.CreateInstance<TimingQteChallenge>();
            _challenge.TotalDuration = 4f;
            _challenge.CycleDuration = 1f;
            _challenge.GreenZoneCenter = 0.7f;
            _challenge.GreenZoneWidth = 0.2f;
            _definition = ScriptableObject.CreateInstance<GunDefinition>();
            _repair = ScriptableObject.CreateInstance<GunRepairProfile>();
            _definition.Jam.Repair = _repair;
            _repair.RequireReloadPress = true;
            _repair.QuickDuration = 2.6f;
            _repair.StubbornDuration = 3.8f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_challenge);
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_repair);
        }

        [Test]
        public void MissWaitsForNextPassAndCanThenSucceed()
        {
            var session = (TimingQteSession)_challenge.CreateSession();
            session.Tick(0.2f);
            session.Press();
            session.Tick(0.5f);
            session.Press();
            Assert.That(session.Result, Is.EqualTo(QteResult.Running));
            Assert.That(session.Misses, Is.EqualTo(1));
            session.Tick(1f);
            session.Press();
            Assert.That(session.Result, Is.EqualTo(QteResult.Success));
        }

        [Test]
        public void LongerDurationGivesMorePassesWithoutChangingFillSpeed()
        {
            _challenge.TotalDuration = 1.2f;
            var shortSession = (TimingQteSession)_challenge.CreateSession();
            _challenge.TotalDuration = 4f;
            var longSession = (TimingQteSession)_challenge.CreateSession();
            shortSession.Tick(1.7f);
            longSession.Tick(1.7f);
            Assert.That(shortSession.Result, Is.EqualTo(QteResult.TimedOut));
            Assert.That(longSession.Progress, Is.EqualTo(0.7f).Within(0.0001f));
            longSession.Press();
            Assert.That(longSession.Result, Is.EqualTo(QteResult.Success));
        }

        [Test]
        public void WideningZoneAllowsAnOtherwiseMissedInput()
        {
            var narrow = _challenge.CreateSession();
            _challenge.GreenZoneWidth = 0.5f;
            var wide = _challenge.CreateSession();
            narrow.Tick(0.5f);
            wide.Tick(0.5f);
            narrow.Press();
            wide.Press();
            Assert.That(narrow.Result, Is.EqualTo(QteResult.Running));
            Assert.That(wide.Result, Is.EqualTo(QteResult.Success));
        }

        [Test]
        public void ExpiredChallengeCannotSucceed()
        {
            var session = _challenge.CreateSession();
            session.Tick(4.7f);
            session.Press();
            Assert.That(session.Result, Is.EqualTo(QteResult.TimedOut));
        }

        static GunInput Ready(bool reload = false) => new()
        {
            IsReady = true, CanFire = true, CanAim = true, Reload = reload,
        };

        Gun Jammed(float roll)
        {
            var item = new GunInstance(_definition);
            item.SetJammed(true);
            return new Gun(item, new AmmoReserve(), () => roll);
        }

        [TestCase(0f, true, 3.8f)]
        [TestCase(1f, false, 2.6f)]
        public void ManualRepairChoosesItsSequenceOnce(float roll, bool stubborn, float duration)
        {
            Gun gun = Jammed(roll);
            gun.Tick(Ready(), 1f);
            Assert.That(gun.Action, Is.EqualTo(GunAction.None));
            gun.Tick(Ready(true), 0.01f);
            Assert.That(gun.RepairIsStubborn, Is.EqualTo(stubborn));
            gun.Tick(Ready(), duration - 0.1f);
            Assert.That(gun.Item.IsJammed, Is.True);
            gun.Tick(Ready(), 0.11f);
            Assert.That(gun.Item.IsJammed, Is.False);
        }

        [Test]
        public void RepairKeepsAdvancingAndSuccessClearsImmediatelyWithoutReloading()
        {
            Gun gun = Jammed(1f);
            gun.Tick(Ready(true), 0.01f);
            gun.Tick(Ready(), 1.3f);
            float progress = gun.ActionProgress;
            int rounds = gun.Item.Rounds;
            gun.Tick(Ready(), 0.5f);
            Assert.That(gun.ActionProgress, Is.GreaterThan(progress));
            Assert.That(gun.Item.IsJammed, Is.True);
            Assert.That(gun.CompleteRepair(), Is.True);
            Assert.That(gun.Action, Is.EqualTo(GunAction.None));
            Assert.That(gun.Item.IsJammed, Is.False);
            Assert.That(gun.Item.Rounds, Is.EqualTo(rounds));
        }

        [Test]
        public void InterruptedRepairRetainsJamAndIgnoresStaleSuccess()
        {
            Gun gun = Jammed(1f);
            gun.Tick(Ready(true), 0.01f);
            gun.Interrupt();
            Assert.That(gun.CompleteRepair(), Is.False);
            Assert.That(gun.Item.IsJammed, Is.True);
        }

        [TestCase(1f, 4f)]
        [TestCase(0f, 5f)]
        public void AutomaticRepairCompletesWithoutInput(float roll, float duration)
        {
            _repair.RequireReloadPress = false;
            _repair.QuickDuration = 4f;
            _repair.StubbornDuration = 5f;
            Gun gun = Jammed(roll);
            gun.Tick(Ready(), 0f);
            Assert.That(gun.Action, Is.EqualTo(GunAction.ClearingJam));
            gun.Tick(Ready(), duration - 0.1f);
            Assert.That(gun.Item.IsJammed, Is.True);
            gun.Tick(Ready(), 0.11f);
            Assert.That(gun.Item.IsJammed, Is.False);
        }

        [Test]
        public void OwnerControlledQteCanRepeatBeyondItsStandaloneTimeout()
        {
            _challenge.TotalDuration = 0.1f;
            var session = (TimingQteSession)_challenge.CreateSession(ownerControlled: true);
            session.Tick(0.2f);
            session.Press();
            session.Tick(10.5f);
            Assert.That(session.Result, Is.EqualTo(QteResult.Running));
            Assert.That(float.IsNaN(session.RemainingFraction), Is.False);
            session.Press();
            Assert.That(session.Result, Is.EqualTo(QteResult.Success));
        }

        [Test]
        public void ReleasingOneSlowMotionRequestPreservesOthersAndPause()
        {
            float scale = Time.timeScale;
            float fixedStep = Time.fixedDeltaTime;
            using (var time = new TimeService())
            {
                object qte = new object();
                object other = new object();
                time.Request(qte, 0.2f);
                time.Request(other, 0.1f);
                Assert.That(Time.timeScale, Is.EqualTo(0.1f));
                time.SetPaused(true);
                time.Release(qte);
                Assert.That(Time.timeScale, Is.Zero);
                time.SetPaused(false);
                Assert.That(Time.timeScale, Is.EqualTo(0.1f));
                time.Release(other);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep).Within(0.0000001f));
        }
    }
}
