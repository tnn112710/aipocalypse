using Aipocalypse.Core.Combat;
using Aipocalypse.Core.EquipmentSystem;
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
            stamina.Spend(100f);
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

            stamina.Spend(10f);
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
        public void NoRegen_WhileNotResting_AndDelayRestartsOnceIdle()
        {
            var stamina = new Stamina(100f, regenDelay: 1f, recoverFraction: 0.3f);
            stamina.Spend(50f);

            stamina.Tick(2f, regenPerSecond: 20f, resting: false);
            Assert.AreEqual(50f, stamina.Current, "moving never regenerates");

            stamina.Tick(0.5f, regenPerSecond: 20f, resting: true);
            Assert.AreEqual(50f, stamina.Current, "the delay counts only from standing idle");

            stamina.Tick(0.6f, regenPerSecond: 20f, resting: true);
            stamina.Tick(1f, regenPerSecond: 20f, resting: true);
            Assert.AreEqual(70f, stamina.Current, 0.001f);
        }

        [Test]
        public void EverySpend_PausesRegen()
        {
            var stamina = new Stamina(100f, regenDelay: 1f, recoverFraction: 0.3f);
            stamina.Spend(50f);

            stamina.Tick(0.5f, regenPerSecond: 20f);

            Assert.AreEqual(50f, stamina.Current);
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
        static BreathingSettings Settings() => new() { Below = 0.25f, EaseTime = 0f };

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
            Assert.AreEqual(1f, PlayerBreathSound.VolumeFor(breathing.Tiredness, 0.4f), 0.001f);
        }

        [Test]
        public void Tiredness_BuildsAsStaminaDrops_AndEases()
        {
            var breathing = new PlayerBreathing(Settings());
            breathing.Tick(0.125f, exhausted: false, deltaTime: 0.1f);
            Assert.AreEqual(0.5f, breathing.Tiredness, 0.001f);
            Assert.AreEqual(0.7f, PlayerBreathSound.VolumeFor(breathing.Tiredness, 0.4f), 0.001f);

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

    public class PlayerVisionTests
    {
        const float Step = 0.02f;

        GameObject _object;
        Light _light;
        PlayerVisionTuning _tuning;
        LanternDefinition _lantern;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("VisionLight");
            _light = _object.AddComponent<Light>();
            _tuning = ScriptableObject.CreateInstance<PlayerVisionTuning>();
            _lantern = ScriptableObject.CreateInstance<LanternDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_tuning);
            Object.DestroyImmediate(_lantern);
        }

        [Test]
        public void ByDay_TheLightIsOff()
        {
            var vision = new PlayerVision(_light, _tuning);

            vision.Tick(darkness: 0f, radius: 4f, lantern: null, time: 0f, deltaTime: Step);

            Assert.IsFalse(_light.enabled);
        }

        [Test]
        public void AtNight_RangeReachesPastTheRadiusEdge_AndDarknessScalesTheIntensity()
        {
            var vision = new PlayerVision(_light, _tuning);
            float height = _tuning.Position.y;
            float edgeSqr = 4f * 4f + height * height;

            vision.Tick(darkness: 1f, radius: 4f, lantern: null, time: 0f, deltaTime: Step);
            float full = PlayerVision.IntensityFor(_tuning.Brightness, edgeSqr, _tuning.RangeScale, _tuning.MaxIntensity);
            Assert.IsTrue(_light.enabled);
            Assert.AreEqual(Mathf.Sqrt(edgeSqr) * _tuning.RangeScale, _light.range, 0.001f);
            Assert.AreEqual(full, _light.intensity, 0.001f);

            vision.Tick(darkness: 0.5f, radius: 4f, lantern: null, time: 0f, deltaTime: Step);
            Assert.AreEqual(full * 0.5f, _light.intensity, 0.001f);
        }

        [Test]
        public void BiggerRadius_IsABrighterLongerLight()
        {
            var small = new PlayerVision(_light, _tuning);
            small.Tick(1f, 4f, null, 0f, Step);
            var big = new PlayerVision(_light, _tuning);
            big.Tick(1f, 8f, null, 0f, Step);

            Assert.Greater(big.Intensity, small.Intensity * 2f);
            Assert.Greater(big.Range, small.Range);
        }

        [Test]
        public void IntensityFor_GivesTheEdgeBrightness_UnderUrpFalloff()
        {
            const float edgeDistance = 5f;
            const float rangeScale = 1.6f;
            float intensity = PlayerVision.IntensityFor(0.12f, edgeDistance * edgeDistance, rangeScale, max: 1000f);

            float fade = 1f - Mathf.Pow(1f / rangeScale, 4f);
            Assert.AreEqual(0.12f, intensity / (edgeDistance * edgeDistance) * fade * fade, 0.0001f);
            Assert.AreEqual(3f, PlayerVision.IntensityFor(0.12f, 100f, rangeScale, max: 3f));
        }

        [Test]
        public void Lantern_AddsItsRadius_EasingIn_FromItsOwnPosition()
        {
            var vision = new PlayerVision(_light, _tuning);
            vision.Tick(1f, 4f, null, 0f, Step);

            vision.Tick(1f, 4f, _lantern, Step, Step);
            Assert.Greater(vision.Radius, 4f);
            Assert.Less(vision.Radius, 4f + _lantern.Radius - 0.5f);

            for (int i = 2; i < 200; i++)
                vision.Tick(1f, 4f, _lantern, i * Step, Step);
            float radius = 4f + _lantern.Radius;
            float height = _tuning.LanternPosition.y;
            float steady = PlayerVision.IntensityFor(_lantern.Brightness, radius * radius + height * height, _tuning.RangeScale, _tuning.MaxIntensity);
            Assert.AreEqual(radius, vision.Radius, 0.01f);
            Assert.That(_light.intensity, Is.InRange(steady * (1f - _lantern.Flicker) - 0.01f, steady + 0.01f));
            Assert.That(Vector3.Distance(_tuning.LanternPosition, _object.transform.localPosition), Is.LessThan(0.01f));
        }
    }
}
