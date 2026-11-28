using System;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class PlayerNeedsTests
    {
        const string SettingsPath = "Assets/_Game/Data/Player/PlayerNeedsSettings.asset";

        PlayerNeedsSettings _settings;
        StatModifiers _stats;
        Health _health;
        Stamina _stamina;
        PlayerNeeds _needs;

        [SetUp]
        public void SetUp()
        {
            // The field defaults are the spec's starting values.
            _settings = ScriptableObject.CreateInstance<PlayerNeedsSettings>();
            _stats = new StatModifiers();
            _health = new Health(100f);
            _stamina = new Stamina(100f, 1f, 0.3f);
            _needs = new PlayerNeeds(_settings, _stats, _health, _stamina);
        }

        [TearDown]
        public void TearDown()
        {
            _needs.Dispose();
            UnityEngine.Object.DestroyImmediate(_settings);
        }

        [Test]
        public void New_StartsEveryNeedFull()
        {
            Assert.AreEqual(1f, _needs.Hunger.Normalized);
            Assert.AreEqual(1f, _needs.Thirst.Normalized);
            Assert.AreEqual(1f, _needs.Sanity.Normalized);
            Assert.AreEqual(0f, _needs.HoursAwake);
        }

        [Test]
        public void Advance_AtRestEmptiesHungerInADayAndThirstInSixteenHours()
        {
            _needs.Advance(16f, false);
            Assert.AreEqual(0f, _needs.Thirst.Current, 0.05f);
            Assert.AreEqual(100f / 3f, _needs.Hunger.Current, 0.05f);

            _needs.Advance(8f, false);
            Assert.AreEqual(0f, _needs.Hunger.Current, 0.05f);
            Assert.AreEqual(100f, _needs.Sanity.Current, "sanity has no drain of its own yet");
        }

        [Test]
        public void Advance_EffortDrainsMore()
        {
            _stamina.Spend(100f);

            _needs.Advance(0f, false);

            Assert.AreEqual(98.5f, _needs.Hunger.Current, 1e-3f);
            Assert.AreEqual(97.5f, _needs.Thirst.Current, 1e-3f);
        }

        [Test]
        public void Advance_AsleepDrainsAThirdAndDoesNotCountHoursAwake()
        {
            _needs.Advance(3f, asleep: true);

            Assert.AreEqual(100f - 100f / 24f * 3f * 0.33f, _needs.Hunger.Current, 0.01f);
            Assert.AreEqual(0f, _needs.HoursAwake);
            _needs.Advance(2f, asleep: false);
            Assert.AreEqual(2f, _needs.HoursAwake, 1e-4f);
        }

        [Test]
        public void Advance_DrainUpgradeSlowsIt()
        {
            _stats.Set("upgrade", StatId.HungerDrain, multiply: 0.5f);

            _needs.Advance(24f, false);

            Assert.AreEqual(50f, _needs.Hunger.Current, 0.05f);
        }

        [Test]
        public void EmptyHungerAndThirst_StopAtTheSameHealthFloor()
        {
            _needs.Hunger.Set(0f);
            _needs.Thirst.Set(0f);
            _needs.Tick(1f);

            _needs.Advance(1f, false);
            Assert.AreEqual(80f, _health.Current, 0.05f, "both drain together: 10 + 10 an hour");

            _needs.Advance(100f, false);
            Assert.AreEqual(25f, _health.Current, 1e-3f);
        }

        [Test]
        public void HealthFloor_NeverHealsAPlayerBelowIt()
        {
            _needs.Hunger.Set(0f);
            _health.Set(10f);
            _needs.Tick(1f);

            _needs.Advance(5f, false);

            Assert.AreEqual(10f, _health.Current, 1e-4f);
        }

        [Test]
        public void WellFed_RegeneratesHealthOnlyOutOfCombat()
        {
            _health.Set(50f);
            _needs.OnHurt();

            _needs.Tick(5f);
            _needs.Advance(1f, false);
            Assert.AreEqual(50f, _health.Current, 1e-3f, "hurt 5 s ago: still in combat");

            _needs.Tick(6f);
            _needs.Advance(1f, false);
            Assert.AreEqual(62f, _health.Current, 0.05f);
        }

        [Test]
        public void NotWellFed_DoesNotRegenerate()
        {
            _health.Set(50f);
            _needs.Hunger.Set(60f);
            _needs.Tick(1f);

            _needs.Advance(1f, false);

            Assert.AreEqual(50f, _health.Current, 1e-3f);
        }

        [Test]
        public void ApplyDeathFloor_RaisesOnlyMetersUnderHalf()
        {
            _needs.Hunger.Set(20f);
            _needs.Thirst.Set(80f);

            _needs.ApplyDeathFloor();

            Assert.AreEqual(50f, _needs.Hunger.Current, 1e-3f);
            Assert.AreEqual(80f, _needs.Thirst.Current, 1e-3f);
        }

        [Test]
        public void ApplyDeathFloor_UpgradeRaisesTheFloor()
        {
            _stats.Set("upgrade", StatId.DeathNeedFloor, add: 0.25f);
            _needs.Hunger.Set(20f);

            _needs.ApplyDeathFloor();

            Assert.AreEqual(75f, _needs.Hunger.Current, 1e-3f);
        }

        [Test]
        public void Frozen_StopsEveryDrain()
        {
            _needs.Frozen = true;
            _stamina.Spend(50f);

            _needs.Advance(10f, false);
            _needs.Drain(PlayerMeterId.Sanity, 30f, "test");

            Assert.AreEqual(100f, _needs.Hunger.Current);
            Assert.AreEqual(100f, _needs.Thirst.Current);
            Assert.AreEqual(100f, _needs.Sanity.Current);
        }

        [Test]
        public void Drain_TakesFromTheNamedNeedScaledByItsDrainStat()
        {
            _stats.Set("upgrade", StatId.SanityDrain, multiply: 0.5f);

            _needs.Drain(PlayerMeterId.Sanity, 30f, "enemy.scream");

            Assert.AreEqual(85f, _needs.Sanity.Current, 1e-3f);
            Assert.AreEqual(100f, _needs.Hunger.Current);
        }

        [Test]
        public void MaxUpgrade_KeepsTheShare()
        {
            _needs.Hunger.Set(50f);

            _stats.Set("upgrade", StatId.MaxHunger, add: 50f);

            Assert.AreEqual(150f, _needs.Hunger.Max);
            Assert.AreEqual(75f, _needs.Hunger.Current, 1e-3f);
        }

        [Test]
        public void DisabledNeed_NeitherDrainsNorHurts()
        {
            _settings.Hunger.Enabled = false;
            _needs.Hunger.Set(0f);

            _needs.Advance(10f, false);

            Assert.AreEqual(0f, _needs.Hunger.Current);
            Assert.AreEqual(100f, _health.Current, 1e-3f, "thirst's well-fed tier doesn't heal, and hunger is off");
        }

        [Test]
        public void Advance_OneLargeStepMatchesManySmallSteps()
        {
            var small = new PlayerNeeds(_settings, new StatModifiers(), new Health(100f), new Stamina(100f, 1f, 0.3f));
            var smallHealth = (Health)small.Meter(PlayerMeterId.Health);
            _needs.Hunger.Set(20f);
            small.Hunger.Set(20f);
            _needs.Tick(1f);
            small.Tick(1f);

            _needs.Advance(8f, false);
            for (int i = 0; i < 800; i++)
                small.Advance(0.01f, false);

            Assert.AreEqual(small.Hunger.Current, _needs.Hunger.Current, 0.5f);
            Assert.AreEqual(small.Thirst.Current, _needs.Thirst.Current, 0.5f);
            Assert.AreEqual(smallHealth.Current, _health.Current, 0.5f, "health drains once hunger empties mid-step");
            Assert.Less(_health.Current, 80f);
            small.Dispose();
        }

        static readonly SanityInput FarInTheDark = new(darkness: 1f, distanceOutsideLight: 150f, inLight: false, nearComfort: false);
        static readonly SanityInput ByTheFire = new(darkness: 1f, distanceOutsideLight: 0f, inLight: true, nearComfort: false);

        [Test]
        public void Sanity_DrainsInTheDarkAwayFromLight()
        {
            _needs.Advance(2f, false, FarInTheDark);

            Assert.AreEqual(50f, _needs.Sanity.Current, 0.05f, "25 an hour, not yet 4 hours awake");
        }

        [Test]
        public void Sanity_DoesNotChangeWhileAsleep()
        {
            _needs.Sanity.Set(60f);
            _needs.HoursAwake = 10f;

            _needs.Advance(4f, true, FarInTheDark);

            Assert.AreEqual(60f, _needs.Sanity.Current, 1e-3f);
        }

        [Test]
        public void Sanity_FrozenStopsTheDrainNotTheRecovery()
        {
            _needs.Frozen = true;
            _needs.Sanity.Set(50f);

            _needs.Advance(1f, false, FarInTheDark);
            Assert.AreEqual(50f, _needs.Sanity.Current, 1e-3f);

            _needs.Advance(1f, false, ByTheFire);
            Assert.AreEqual(53.3f, _needs.Sanity.Current, 0.01f);
        }

        [Test]
        public void Sanity_RecoveryAndDrainUpgradesScaleTheirParts()
        {
            _stats.Set("upgrade", StatId.SanityRecovery, multiply: 2f);
            _stats.Set("upgrade", StatId.SanityDrain, multiply: 0.5f);
            _needs.Sanity.Set(50f);

            _needs.Advance(1f, false, ByTheFire);
            Assert.AreEqual(56.6f, _needs.Sanity.Current, 0.01f);

            _needs.Advance(1f, false, FarInTheDark);
            Assert.AreEqual(56.6f - 12.5f, _needs.Sanity.Current, 0.01f);
        }

        [Test]
        public void OnWoke_ResetsHoursAwake()
        {
            _needs.HoursAwake = 15f;

            _needs.OnWoke();

            Assert.AreEqual(0f, _needs.HoursAwake);
        }

        [Test]
        public void Meter_CoversEveryPlayerMeterId()
        {
            foreach (PlayerMeterId id in Enum.GetValues(typeof(PlayerMeterId)))
                Assert.IsNotNull(_needs.Meter(id), id.ToString());
            Assert.AreSame(_health, _needs.Meter(PlayerMeterId.Health));
            Assert.AreSame(_stamina, _needs.Meter(PlayerMeterId.Stamina));
            Assert.AreSame(_needs.Thirst, _needs.Meter(PlayerMeterId.Thirst));
        }

        [Test]
        public void Settings_EveryNeedHasSortedTiersCoveringZeroToOne()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>(SettingsPath);
            Assert.IsNotNull(asset, SettingsPath);

            foreach (NeedSettings need in new[] { asset.Hunger, asset.Thirst, asset.Sanity })
            {
                Assert.IsNotEmpty(need.Tiers);
                float previous = float.MaxValue;
                int empty = 0;
                bool coversZero = false;
                foreach (NeedTier tier in need.Tiers)
                {
                    if (tier.Severity == NeedSeverity.Empty)
                    {
                        empty++;
                        continue;
                    }
                    Assert.LessOrEqual(tier.From, 1f);
                    Assert.Less(tier.From, previous, "highest From first");
                    previous = tier.From;
                    coversZero |= tier.From <= 0f;
                }
                Assert.AreEqual(1, empty, "one Empty tier");
                Assert.IsTrue(coversZero, "a tier reaches down to 0");
            }
        }
    }
}
