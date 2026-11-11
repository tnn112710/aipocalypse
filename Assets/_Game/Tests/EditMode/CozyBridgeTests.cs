using System.Linq;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using NUnit.Framework;

namespace MB13.Aipocalypse.Tests
{
    public class CozyBridgeTests
    {
        // The WorldMap sun path: sunrise 06:00, sunset 19:00.
        const float Sunrise = 6f;
        const float Sunset = 19f;

        [TestCase(6f, 6f)]
        [TestCase(19f, 18f)]
        [TestCase(12.5f, 12f)]
        [TestCase(0.5f, 24f)]
        public void CozyHour_PutsOurSunEventsOnCozysClock(float hour, float expected)
        {
            Assert.That(CozyBridge.CozyHour(hour, Sunrise, Sunset), Is.EqualTo(expected % 24f).Within(1e-4f));
        }

        [Test]
        public void OurHour_UndoesCozyHourAllDay()
        {
            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                float cozy = CozyBridge.CozyHour(hour, Sunrise, Sunset);
                Assert.That(CozyBridge.OurHour(cozy, Sunrise, Sunset), Is.EqualTo(hour).Within(1e-3f), $"hour {hour}");
            }
        }

        [Test]
        public void CozyHour_KeepsTheDayInOrder()
        {
            float previous = CozyBridge.CozyHour(Sunrise, Sunrise, Sunset);
            for (float step = 0.25f; step < 24f; step += 0.25f)
            {
                float next = CozyBridge.CozyHour(Sunrise + step, Sunrise, Sunset);
                float advance = (next - previous + 24f) % 24f;
                Assert.That(advance, Is.GreaterThan(0f).And.LessThan(1f), $"{Sunrise + step}h");
                previous = next;
            }
        }

        // COZY's own composite lets the sky over far geometry at night (DAYNIGHT.md → COZY); a feature re-add or a
        // COZY update would silently bring it back.
        [Test]
        public void Renderer_CompositesWithOurCozyFork()
        {
            var feature = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Settings/PC_Renderer.asset")
                .FirstOrDefault(o => o != null && o.GetType().Name == "CozyRendererFeature");
            Assert.That(feature, Is.Not.Null, "PC_Renderer has no COZY feature");
            var material = new UnityEditor.SerializedObject(feature).FindProperty("compositeMat").objectReferenceValue as UnityEngine.Material;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Aipocalypse/Environment/CozyComposite"));
        }
    }
}
