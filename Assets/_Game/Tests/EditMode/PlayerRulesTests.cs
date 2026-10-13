using Aipocalypse.Core.Combat;
using Aipocalypse.Core.PlayerSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class StaminaTests
    {
        [Test]
        public void SpendingToZero_Exhausts_AndBlocksExertion()
        {
            var stamina = new Stamina(100f, regenDelay: 1f, recoverFraction: 0.3f);

            stamina.Spend(150f);

            Assert.AreEqual(0f, stamina.Current);
            Assert.IsTrue(stamina.IsExhausted);
            Assert.IsFalse(stamina.CanExert);
        }

        [Test]
        public void Regen_WaitsForDelay()
        {
            var stamina = new Stamina(100f, regenDelay: 1f, recoverFraction: 0.3f);
            stamina.Spend(50f);

            stamina.Tick(0.5f, regenPerSecond: 20f);
            Assert.AreEqual(50f, stamina.Current);

            stamina.Tick(0.6f, regenPerSecond: 20f);
            stamina.Tick(1f, regenPerSecond: 20f);
            Assert.AreEqual(70f, stamina.Current, 0.001f);
        }

        [Test]
        public void Exhaustion_ClearsOnlyAtRecoverFraction()
        {
            var stamina = new Stamina(100f, regenDelay: 0f, recoverFraction: 0.3f);
            stamina.Spend(100f);

            stamina.Tick(1f, regenPerSecond: 20f);
            Assert.IsTrue(stamina.IsExhausted, "20% is below the 30% recover threshold");

            stamina.Tick(0.6f, regenPerSecond: 20f);
            Assert.IsFalse(stamina.IsExhausted);
            Assert.IsTrue(stamina.CanExert);
        }

        [Test]
        public void Regen_NeverExceedsMax()
        {
            var stamina = new Stamina(100f, regenDelay: 0f, recoverFraction: 0.3f);
            stamina.Spend(10f);

            stamina.Tick(10f, regenPerSecond: 50f);

            Assert.AreEqual(100f, stamina.Current);
        }
    }

    public class HealthTests
    {
        [Test]
        public void Damage_ClampsAtZero_AndReportsAppliedAmount()
        {
            var health = new Health(100f);

            Assert.AreEqual(30f, health.Damage(30f));
            Assert.AreEqual(70f, health.Damage(500f));
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Damage(10f), "no damage once dead");
        }

        [Test]
        public void Heal_DoesNotReviveOrOverfill()
        {
            var health = new Health(100f);
            health.Damage(40f);
            health.Heal(100f);
            Assert.AreEqual(100f, health.Current);

            health.Damage(100f);
            health.Heal(50f);
            Assert.IsTrue(health.IsDead);
        }
    }

    public class FallDamageTests
    {
        [TestCase(0f, 0f)]
        [TestCase(3.5f, 0f)]
        [TestCase(8.75f, 50f)]
        [TestCase(14f, 100f)]
        [TestCase(40f, 100f)]
        public void ScalesLinearlyBetweenSafeAndLethal(float drop, float expected)
        {
            Assert.AreEqual(expected, FallDamage.For(drop, safeHeight: 3.5f, lethalHeight: 14f, maxHealth: 100f), 0.001f);
        }
    }

    public class LocomotionTests
    {
        [Test]
        public void DirectionalSpeedScale_ForwardStrafeBackward()
        {
            Assert.AreEqual(1f, PlayerMotor.DirectionalSpeedScale(Vector2.up, 0.9f, 0.7f), 0.001f);
            Assert.AreEqual(0.9f, PlayerMotor.DirectionalSpeedScale(Vector2.right, 0.9f, 0.7f), 0.001f);
            Assert.AreEqual(0.7f, PlayerMotor.DirectionalSpeedScale(Vector2.down, 0.9f, 0.7f), 0.001f);
        }
    }
}
