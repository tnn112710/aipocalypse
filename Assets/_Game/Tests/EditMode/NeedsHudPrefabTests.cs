using System;
using System.Reflection;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using MB13.Aipocalypse.Core.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // The needs' HUD wiring in HUD.prefab: built by AgentScripts/Hud/NeedsHudSetup, read here so a broken rebuild shows up.
    public sealed class NeedsHudPrefabTests
    {
        const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";

        static T Field<T>(object owner, string name) where T : class =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) as T;

        [Test]
        public void Hud_HasARingPerNeedWithAStyleAndIcon()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Assert.IsNotNull(hud, HudPath);
            var needs = hud.GetComponentInChildren<PlayerNeedsHud>(true);
            Assert.IsNotNull(needs, "a PlayerNeedsHud in HUD.prefab");

            foreach (string field in new[] { "_hunger", "_thirst", "_sanity" })
            {
                var ring = Field<NeedRing>(needs, field);
                Assert.IsNotNull(ring, field);
                NeedRingStyle style = ring.Style;
                Assert.IsNotNull(style, $"{field}: style");
                Assert.IsNotNull(style.Icon, $"{field}: icon");
                Assert.IsNotNull(style.RingSprite, $"{field}: ring sprite");
                Assert.AreEqual(4, style.Bands.Length, $"{field}: green, yellow, orange, red");
                Assert.AreEqual(0f, style.Bands[^1].Above, $"{field}: the last band reaches 0");
            }
        }

        [TestCase(1f, 0)]
        [TestCase(0.61f, 0)]
        [TestCase(0.6f, 1)] // green above 60%
        [TestCase(0.31f, 1)]
        [TestCase(0.3f, 2)] // yellow 30-60%
        [TestCase(0.11f, 2)]
        [TestCase(0.1f, 3)] // orange 10-30%, red under
        [TestCase(0f, 3)]
        public void RingColour_FollowsTheSharesBand(float share, int band)
        {
            var style = ScriptableObject.CreateInstance<NeedRingStyle>();
            Color expected = style.Bands[band].Color;

            Assert.AreEqual(expected, style.ColorAt(share));
            UnityEngine.Object.DestroyImmediate(style);
        }

        [TestCase(1f, true)]
        [TestCase(0.61f, true)]
        [TestCase(0.6f, false)] // yellow, orange and red stay bright
        [TestCase(0.2f, false)]
        [TestCase(0f, false)]
        public void Ring_DimsOnlyWhileGreen(float share, bool dims)
        {
            var style = ScriptableObject.CreateInstance<NeedRingStyle>();

            Assert.AreEqual(dims, style.Dims(share));
            UnityEngine.Object.DestroyImmediate(style);
        }

        // The asset and the code defaults (NeedsSetup.ResetSettings): a tier changes where a ring changes colour.
        [TestCase(true)]
        [TestCase(false)]
        public void NeedTiers_ChangeWhereTheRingColourChanges(bool asset)
        {
            var settings = asset
                ? AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>("Assets/_Game/Data/Player/PlayerNeedsSettings.asset")
                : ScriptableObject.CreateInstance<PlayerNeedsSettings>();
            var style = ScriptableObject.CreateInstance<NeedRingStyle>();
            float[] edges = Array.ConvertAll(style.Bands, band => band.Above);

            foreach (NeedSettings need in new[] { settings.Hunger, settings.Thirst, settings.Sanity })
            {
                Assert.IsTrue(Array.Exists(need.Tiers, tier => tier.From == edges[0]), "a tier starts where green ends");
                foreach (NeedTier tier in need.Tiers)
                    if (tier.Severity != NeedSeverity.Good && tier.From > 0f)
                        Assert.Contains(tier.From, edges, $"{tier.Severity} from {tier.From}");
            }
            SanityEffectSettings effects = settings.SanityEffects;
            float From(NeedSeverity severity) => Array.Find(settings.Sanity.Tiers, tier => tier.Severity == severity).From;
            Assert.AreEqual(From(NeedSeverity.Good), effects.UneasyFrom, "uneasy look");
            Assert.AreEqual(From(NeedSeverity.Normal), effects.LowFrom, "low look");
            Assert.AreEqual(From(NeedSeverity.Low), effects.CriticalFrom, "critical look");
            Assert.AreEqual(effects.LowFrom, effects.LiesFrom, "the HUD starts lying with the low look");
            Assert.AreEqual(effects.LowFrom, effects.HeartbeatFrom, "heartbeat with the low look");
            Assert.AreEqual(effects.CriticalFrom, effects.BlipsFrom, "fake dots with the critical look");
            UnityEngine.Object.DestroyImmediate(style);
            if (!asset)
                UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Hud_HasABagGaugeWithAColorPerLoadState()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var load = hud.GetComponentInChildren<PlayerLoadHud>(true);
            Assert.IsNotNull(load, "a PlayerLoadHud in HUD.prefab");
            var gauge = Field<BagGauge>(load, "_gauge");
            Assert.IsNotNull(gauge, "_gauge");
            Assert.IsNotNull(gauge.Style, "style");
            Assert.IsNotNull(gauge.Style.Icon, "icon");
            foreach (LoadState state in Enum.GetValues(typeof(LoadState)))
                Assert.IsTrue(gauge.Style.Colors.ContainsKey(state), $"a color for {state}");
            Vector2 range = gauge.Style.FillRange;
            Assert.That(range.x, Is.GreaterThan(0f).And.LessThan(range.y), "the bag's drawn rows start above the sprite's bottom edge");
            Assert.That(range.y, Is.LessThanOrEqualTo(1f));
        }

        [TestCase(0f, 0.1f)]
        [TestCase(1f, 0.9f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.25f, 0.3f)]
        public void BagGauge_MapsTheShareOntoTheDrawnBag(float share, float expected)
        {
            Assert.AreEqual(expected, BagGauge.OnBag(share, new Vector2(0.1f, 0.9f)), 1e-4f);
        }

        [Test]
        public void PickupFeed_HasAnOverloadedSound()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var feed = hud.GetComponentInChildren<PickupFeedHud>(true);
            Assert.IsNotNull(feed, "a PickupFeedHud in HUD.prefab");
            var style = Field<InventoryStyle>(feed, "_style");
            Assert.IsNotNull(style, "_style");
            Assert.IsTrue(style.OverloadedSound.IsValid(), "InventoryStyle.OverloadedSound");
        }

        [Test]
        public void NeedSettings_NoStatRowMultipliesByZero()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerNeedsSettings>("Assets/_Game/Data/Player/PlayerNeedsSettings.asset");
            Assert.IsNotNull(settings);
            foreach (NeedSettings need in new[] { settings.Hunger, settings.Thirst, settings.Sanity })
            foreach (NeedTier tier in need.Tiers)
            foreach (StatChange change in tier.Changes)
                Assert.AreNotEqual(0f, change.Multiply, $"{tier.Severity} {change.Stat}: × 0 zeroes the stat (a row added in the Inspector without a multiply?)");
        }

        [Test]
        public void NeedTier_NewRowsInTheInspectorChangeNothing()
        {
            MethodInfo add = typeof(NeedTier).GetMethod("NewChange", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(add, "NeedTier.NewChange, Changes' Odin add function");
            var change = (StatChange)add.Invoke(new NeedTier(), null);
            Assert.AreEqual(1f, change.Multiply);
            Assert.AreEqual(0f, change.Add);
        }
    }
}
