using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using MB13.Aipocalypse.Core.Stats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    // Sanity in the world: safe lights, comfort sources and comforting.
    public sealed class SanityWorldTests
    {
        sealed class Comfort : IComfortSource
        {
            public Vector3 Position { get; set; }
            public float Radius { get; set; } = 8f;
            public bool IsActive { get; set; } = true;
        }

        readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        SafeLight Light(Vector3 position, float radius)
        {
            var go = new GameObject("Light");
            _created.Add(go);
            go.transform.position = position;
            var light = go.AddComponent<SafeLight>();
            var so = new SerializedObject(light);
            so.FindProperty("_radius").floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        [Test]
        public void DistanceOutside_IsZeroInsideAndMeasuresToTheNearestEdge()
        {
            var lights = new List<SafeLight> { Light(Vector3.zero, 10f), Light(new Vector3(100f, 0f, 0f), 20f) };

            Assert.AreEqual(0f, SafeLight.DistanceOutside(new Vector3(5f, 0f, 0f), lights), 1e-4f);
            Assert.AreEqual(5f, SafeLight.DistanceOutside(new Vector3(15f, 0f, 0f), lights), 1e-4f, "to the first light's edge");
            Assert.AreEqual(10f, SafeLight.DistanceOutside(new Vector3(70f, 0f, 0f), lights), 1e-4f, "to the second, nearer edge");
            Assert.AreEqual(0f, SafeLight.DistanceOutside(new Vector3(5f, 30f, 0f), lights), 1e-4f, "height doesn't count (upstairs, a slope)");
        }

        [Test]
        public void DistanceOutside_WithNoLightIsInfinite()
        {
            Assert.IsTrue(float.IsPositiveInfinity(SafeLight.DistanceOutside(Vector3.zero, new List<SafeLight>())));
            Assert.IsTrue(float.IsPositiveInfinity(SafeLight.DistanceOutside(Vector3.zero, null)));
        }

        [Test]
        public void ComfortSources_NearOnlyInsideAnActiveSourcesRadius()
        {
            var sources = new ComfortSources();
            var dog = new Comfort { Position = new Vector3(10f, 0f, 0f) };
            sources.Add(dog);

            Assert.IsTrue(sources.Near(new Vector3(4f, 0f, 0f)));
            Assert.IsFalse(sources.Near(new Vector3(-5f, 0f, 0f)));
            dog.IsActive = false;
            Assert.IsFalse(sources.Near(new Vector3(10f, 0f, 0f)));
            dog.IsActive = true;
            sources.Remove(dog);
            Assert.IsFalse(sources.Near(new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void VolumeStep_SendsOnlyRealChanges()
        {
            var step = new VolumeStep();

            Assert.IsTrue(step.Next(0.3f, 0.02f, out float sent));
            Assert.AreEqual(0.3f, sent, 1e-4f);
            Assert.IsFalse(step.Next(0.31f, 0.02f, out _), "under a step: no new fade");
            Assert.IsTrue(step.Next(0.33f, 0.02f, out sent));
            Assert.AreEqual(0.33f, sent, 1e-4f);
            Assert.IsTrue(step.Next(0f, 0.02f, out sent), "reaching silence always sends");
            Assert.AreEqual(0f, sent);
            step.Reset();
            Assert.IsTrue(step.Next(0.01f, 0.02f, out _), "after a reset the first value always sends");
        }

        [Test]
        public void Comfort_AddsSanityScaledByRecovery()
        {
            var settings = ScriptableObject.CreateInstance<PlayerNeedsSettings>();
            _created.Add(settings);
            var stats = new StatModifiers();
            var needs = new PlayerNeeds(settings, stats, new Health(100f), new Stamina(100f, 1f, 0.3f));
            needs.Sanity.Set(50f);

            needs.Comfort(5f);
            Assert.AreEqual(55f, needs.Sanity.Current, 1e-3f);

            stats.Set("upgrade", StatId.SanityRecovery, multiply: 2f);
            needs.Comfort(5f);
            Assert.AreEqual(65f, needs.Sanity.Current, 1e-3f);
            needs.Dispose();
        }
    }
}
