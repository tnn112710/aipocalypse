using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.EnvironmentSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class NightHighlightTests
    {
        const string SettingsPath = "Assets/_Game/Data/Environment/NightHighlight.asset";

        [Test]
        public void LightRotation_ShinesTheWayThePlayerLooks_TiltedDownWhateverTheCameraPitch()
        {
            Vector3 lookingUp = new Vector3(0f, 1f, 1f).normalized;
            Vector3 forward = NightHighlight.LightRotation(lookingUp, 35f, 0f) * Vector3.forward;

            Assert.AreEqual(-Mathf.Sin(35f * Mathf.Deg2Rad), forward.y, 0.001f);
            Assert.AreEqual(0f, forward.x, 0.001f);
            Assert.Greater(forward.z, 0f);
        }

        [Test]
        public void LightRotation_TurnsToTheSideByYaw()
        {
            Vector3 forward = NightHighlight.LightRotation(Vector3.forward, 0f, 90f) * Vector3.forward;

            Assert.AreEqual(1f, forward.x, 0.001f);
        }

        [Test]
        public void Enemies_JoinTheLayerTheNightHighlightLights()
        {
            var highlight = AssetDatabase.LoadAssetAtPath<NightHighlightSettings>(SettingsPath);
            var enemies = AssetDatabase.LoadAssetAtPath<EnemySystemSettings>("Assets/_Game/Data/Enemies/EnemySystemSettings.asset");

            Assert.NotNull(highlight, SettingsPath);
            Assert.AreNotEqual(0u, highlight.Layer.value, "the highlight picks no rendering layer");
            Assert.AreEqual(highlight.Layer.value, enemies.RenderingLayer.value, "enemies join a different rendering layer than the highlight lights");
        }
    }
}
