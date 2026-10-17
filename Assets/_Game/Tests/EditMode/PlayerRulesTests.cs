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

        [Test]
        public void RunningDry_WindsYou_BeforeRegenStarts()
        {
            var stamina = new Stamina(100f, regenDelay: 0f, recoverFraction: 0.3f, windedTime: 2f);
            stamina.Spend(100f, delaysRegen: false);
            Assert.IsTrue(stamina.IsWinded);

            stamina.Tick(1.5f, regenPerSecond: 20f);
            Assert.AreEqual(0f, stamina.Current, "no regen while winded");
            Assert.IsTrue(stamina.IsWinded);

            stamina.Tick(0.6f, regenPerSecond: 20f);
            Assert.IsFalse(stamina.IsWinded);
            stamina.Tick(1f, regenPerSecond: 20f);
            Assert.AreEqual(20f, stamina.Current, 0.001f);
        }

        [Test]
        public void DrainingAnExhaustedMeter_DoesNotRestartTheWind()
        {
            var stamina = new Stamina(100f, regenDelay: 0f, recoverFraction: 0.3f, windedTime: 2f);
            stamina.Spend(100f);
            stamina.Tick(2.1f, regenPerSecond: 20f);
            stamina.Tick(0.25f, regenPerSecond: 20f);

            stamina.Spend(10f, delaysRegen: false);
            Assert.AreEqual(0f, stamina.Current);
            Assert.IsFalse(stamina.IsWinded, "still exhausted, so no new wait");
        }

        [Test]
        public void Restore_ClearsTheWind()
        {
            var stamina = new Stamina(100f, regenDelay: 0f, recoverFraction: 0.3f, windedTime: 2f);
            stamina.Spend(100f);
            stamina.Restore();
            Assert.IsFalse(stamina.IsWinded);
        }

        [Test]
        public void SpendWithoutRegenDelay_KeepsRegenRunning()
        {
            var stamina = new Stamina(100f, regenDelay: 1f, recoverFraction: 0.3f);
            stamina.Spend(50f, delaysRegen: false);

            stamina.Tick(0.5f, regenPerSecond: 20f);

            Assert.AreEqual(60f, stamina.Current, 0.001f);
        }
    }

    public class FatigueTests
    {
        static FatigueSettings Settings(float easeTime = 0f) => new()
        {
            Tiers = new[]
            {
                new FatigueTier { Below = 0.5f, Recoil = 1.25f, MeleePower = 0.85f, MoveSpeed = 0.95f },
                new FatigueTier { Below = 0.1f, Recoil = 2.5f, MeleePower = 0.4f, MoveSpeed = 0.7f },
                new FatigueTier { Below = 0.25f, Recoil = 1.6f, MeleePower = 0.65f, MoveSpeed = 0.85f },
            },
            EaseTime = easeTime,
        };

        [Test]
        public void TierFor_PicksTheLowestThresholdAboveStamina()
        {
            FatigueTier[] tiers = Settings().Tiers;

            Assert.IsNull(Fatigue.TierFor(tiers, 0.5f), "at the threshold: fresh");
            Assert.AreEqual(0.5f, Fatigue.TierFor(tiers, 0.3f).Below);
            Assert.AreEqual(0.25f, Fatigue.TierFor(tiers, 0.2f).Below);
            Assert.AreEqual(0.1f, Fatigue.TierFor(tiers, 0f).Below);
        }

        [Test]
        public void Tick_WithoutEase_AppliesTierValues()
        {
            var fatigue = new Fatigue(Settings());

            fatigue.Tick(0.2f, penaltyScale: 1f, deltaTime: 0.016f);

            Assert.AreEqual(1.6f, fatigue.RecoilMultiplier, 0.001f);
            Assert.AreEqual(0.65f, fatigue.MeleePower, 0.001f);
            Assert.AreEqual(0.85f, fatigue.MoveSpeed, 0.001f);
        }

        [Test]
        public void Tick_EasesIntoANewTier()
        {
            var fatigue = new Fatigue(Settings(easeTime: 0.35f));

            fatigue.Tick(0f, penaltyScale: 1f, deltaTime: 0.05f);
            Assert.Greater(fatigue.RecoilMultiplier, 1f);
            Assert.Less(fatigue.RecoilMultiplier, 1.5f, "one frame doesn't jump to the tier");

            for (int i = 0; i < 100; i++)
                fatigue.Tick(0f, 1f, 0.05f);
            Assert.AreEqual(2.5f, fatigue.RecoilMultiplier, 0.01f);
        }

        [Test]
        public void Winded_SlowsMovementFurther()
        {
            FatigueSettings settings = Settings();
            settings.WindedMoveSpeed = 0.5f;
            var fatigue = new Fatigue(settings);

            fatigue.Tick(0f, penaltyScale: 1f, deltaTime: 0.016f, winded: true);
            Assert.AreEqual(0.35f, fatigue.MoveSpeed, 0.001f);

            fatigue.Tick(0f, penaltyScale: 1f, deltaTime: 0.016f);
            Assert.AreEqual(0.7f, fatigue.MoveSpeed, 0.001f);
        }

        [Test]
        public void PenaltyScale_ShrinksOrRemovesThePenalty()
        {
            var fatigue = new Fatigue(Settings());

            fatigue.Tick(0f, penaltyScale: 0.5f, deltaTime: 0.016f);
            Assert.AreEqual(1.75f, fatigue.RecoilMultiplier, 0.001f);
            Assert.AreEqual(0.7f, fatigue.MeleePower, 0.001f);

            fatigue.Tick(0f, penaltyScale: 0f, deltaTime: 0.016f);
            Assert.AreEqual(1f, fatigue.RecoilMultiplier, 0.001f);
            Assert.AreEqual(1f, fatigue.MoveSpeed, 0.001f);
        }
    }

    public class BreathingTests
    {
        static BreathingSettings Settings() => new() { Below = 0.25f, MinVolume = 0.4f, EaseTime = 0f };

        [Test]
        public void Active_OnlyWhenLowOrExhausted()
        {
            var breathing = new PlayerBreathing(Settings());

            breathing.Tick(0.5f, exhausted: false, deltaTime: 0.1f);
            Assert.IsFalse(breathing.IsActive);
            Assert.AreEqual(0f, breathing.Tiredness);

            breathing.Tick(0.2f, exhausted: false, deltaTime: 0.1f);
            Assert.IsTrue(breathing.IsActive);

            breathing.Tick(0.5f, exhausted: true, deltaTime: 0.1f);
            Assert.IsTrue(breathing.IsActive, "exhausted keeps breathing above the threshold");
            Assert.AreEqual(1f, breathing.Tiredness);
            Assert.AreEqual(1f, breathing.Volume, 0.001f);
        }

        [Test]
        public void Tiredness_BuildsAsStaminaDrops_AndEases()
        {
            var breathing = new PlayerBreathing(Settings());
            breathing.Tick(0.125f, exhausted: false, deltaTime: 0.1f);
            Assert.AreEqual(0.5f, breathing.Tiredness, 0.001f);
            Assert.AreEqual(0.7f, breathing.Volume, 0.001f);

            BreathingSettings eased = Settings();
            eased.EaseTime = 0.6f;
            var slow = new PlayerBreathing(eased);
            slow.Tick(0f, exhausted: true, deltaTime: 0.1f);
            Assert.That(slow.Tiredness, Is.InRange(0.05f, 0.5f), "builds up, doesn't snap");
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
