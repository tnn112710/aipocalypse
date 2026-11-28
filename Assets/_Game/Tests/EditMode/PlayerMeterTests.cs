using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public sealed class PlayerMeterTests
    {
        [Test]
        public void StatId_KeepsTheValuesSavesAlreadyHold()
        {
            Assert.AreEqual(14, (int)StatId.AimWhileFiringLevel);
            Assert.AreEqual(15, (int)StatId.MaxHunger);
            Assert.AreEqual(28, (int)StatId.DeathNeedFloor);
        }

        [Test]
        public void Stamina_SetClampsAndCountsAsAMeter()
        {
            IMeter stamina = new Stamina(100f, 1f, 0.3f);

            stamina.Set(140f);
            Assert.AreEqual(100f, stamina.Current);
            stamina.Set(-5f);
            Assert.AreEqual(0f, stamina.Current);
        }

        [Test]
        public void Stamina_SetAboveTheRecoverShareEndsExhaustion()
        {
            var stamina = new Stamina(100f, 1f, 0.3f);
            stamina.Spend(100f);
            Assert.IsTrue(stamina.IsExhausted);

            stamina.Set(50f);

            Assert.IsFalse(stamina.IsExhausted);
        }

        [Test]
        public void Stamina_SetToZeroExhaustsLikeRunningDry()
        {
            var stamina = new Stamina(100f, 1f, 0.3f, windedTime: 2f);

            stamina.Set(0f);

            Assert.IsTrue(stamina.IsExhausted, "out of breath: no sprint until the recover share");
            Assert.IsTrue(stamina.IsWinded, "catching breath before regen, as when running dry");
            Assert.IsFalse(stamina.CanExert);
        }

        [Test]
        public void Stamina_TakeSpentReturnsWhatWasSpentOnce()
        {
            var stamina = new Stamina(100f, 1f, 0.3f);
            stamina.Spend(12f);
            stamina.Spend(3f);

            Assert.AreEqual(15f, stamina.TakeSpent(), 1e-4f);
            Assert.AreEqual(0f, stamina.TakeSpent());
        }

        [Test]
        public void Stamina_TakeSpentCountsOnlyWhatWasLeft()
        {
            var stamina = new Stamina(100f, 1f, 0.3f);
            stamina.Spend(90f);
            stamina.Spend(30f);

            Assert.AreEqual(100f, stamina.TakeSpent(), 1e-4f);
        }

        [Test]
        public void MeterCheat_NeverTouchesHealthOfTheDeadOrSetsItToZero()
        {
            Assert.IsFalse(NeedsCommands.CanSet(PlayerMeterId.Health, 1f, dead: true), "the revive flow brings the dead back");
            Assert.IsFalse(NeedsCommands.CanSet(PlayerMeterId.Health, 0f, dead: false), "0 is death: cheat.kill");
            Assert.IsTrue(NeedsCommands.CanSet(PlayerMeterId.Health, 0.5f, dead: false));
            Assert.IsTrue(NeedsCommands.CanSet(PlayerMeterId.Hunger, 0f, dead: true), "the needs may be set any time");
        }

        [Test]
        public void Health_IsAMeter()
        {
            IMeter health = new Health(80f);
            health.Set(20f);

            Assert.AreEqual(0.25f, health.Normalized, 1e-4f);
        }
    }
}
