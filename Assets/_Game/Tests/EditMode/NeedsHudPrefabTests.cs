using System;
using System.Reflection;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
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
                foreach (NeedSeverity severity in Enum.GetValues(typeof(NeedSeverity)))
                    Assert.IsTrue(style.Colors.ContainsKey(severity), $"{field}: a color for {severity}");
            }
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
        }
    }
}
