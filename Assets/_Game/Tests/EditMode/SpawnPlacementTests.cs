using System.Collections.Generic;
using Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public sealed class SpawnPlacementTests
    {
        [Test]
        public void Water_RejectsShallowPoints()
        {
            Assert.That(SpawnPlacer.PickWaterDepth(10f, 9.5f, new Vector2(1f, 3f), 0.3f, 0.5f, out _), Is.False, "half a metre deep");
            Assert.That(SpawnPlacer.PickWaterDepth(10f, 11f, new Vector2(0f, 3f), 0.3f, 0.5f, out _), Is.False, "the bed is above the water");
            Assert.That(SpawnPlacer.PickWaterDepth(10f, 8.6f, new Vector2(1f, 3f), 0.3f, 0.5f, out _), Is.True, "1.4 m is just deep enough");
        }

        [Test]
        public void Water_StaysBetweenTheBedAndTheSurface()
        {
            for (float roll = 0f; roll <= 1f; roll += 0.25f)
            {
                Assert.That(SpawnPlacer.PickWaterDepth(10f, 4f, new Vector2(1f, 20f), 0.3f, roll, out float y), Is.True);
                Assert.That(y, Is.InRange(4.3f, 9f), "1 m under the surface at least, 0.3 m over the bed");
            }
            SpawnPlacer.PickWaterDepth(10f, 4f, new Vector2(1f, 2f), 0.3f, 1f, out float deepest);
            Assert.That(deepest, Is.EqualTo(8f).Within(1e-4f), "never deeper than the band");
        }

        [Test]
        public void Air_StaysInItsBand()
        {
            Assert.That(SpawnPlacer.PickAltitude(5f, new Vector2(10f, 20f), 0f), Is.EqualTo(15f));
            Assert.That(SpawnPlacer.PickAltitude(5f, new Vector2(10f, 20f), 1f), Is.EqualTo(25f));
            Assert.That(SpawnPlacer.PickAltitude(5f, new Vector2(20f, 10f), 0.5f), Is.EqualTo(20f), "a reversed band still works");
        }

        [Test]
        public void WaterBodies_GiveTheLevelOverTheirFootprintOnly()
        {
            var lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            try
            {
                lake.transform.position = new Vector3(0f, 3f, 0f);
                var water = new WaterBodies(new List<Renderer> { lake.GetComponent<Renderer>(), null });
                Assert.That(water.TryGetLevel(new Vector3(2f, 0f, -3f), out float level), Is.True);
                Assert.That(level, Is.EqualTo(3f).Within(1e-3f));
                Assert.That(water.TryGetLevel(new Vector3(20f, 0f, 0f), out _), Is.False, "outside the plane");
            }
            finally
            {
                Object.DestroyImmediate(lake);
            }
        }

        [Test]
        public void RingArea_SamplesBetweenItsRadii()
        {
            var ring = new RingArea(new Vector3(1f, 2f, 3f), 4f, 6f);
            for (float a = 0f; a <= 1f; a += 0.2f)
            for (float b = 0f; b <= 1f; b += 0.2f)
            {
                Vector3 point = ring.Sample(a, b);
                Assert.That(point.y, Is.EqualTo(2f));
                Assert.That(Vector3.Distance(point, ring.Origin), Is.InRange(4f - 1e-3f, 6f + 1e-3f));
            }
        }

        [Test]
        public void RiverCourse_GivesTheSurfaceInsideItsWidth_FollowingTheSlope()
        {
            // A river 10 m wide running along +X, dropping 1 m every 10 m.
            var river = new RiverCourse();
            for (float x = 0f; x <= 100f; x += RiverCourse.Step)
                river.Add(new Vector3(x, 20f - x * 0.1f, 0f), Vector3.right, 5f);
            river.Close();
            Assert.That(river.TryGetLevel(new Vector3(51f, 0f, 4f), out float level), Is.True);
            Assert.That(level, Is.EqualTo(15f).Within(0.3f), "the level of the reach it's over");
            Assert.That(river.TryGetLevel(new Vector3(51f, 0f, 6f), out _), Is.False, "past the bank");
            Assert.That(river.TryGetLevel(new Vector3(110f, 0f, 0f), out _), Is.False, "past its end");
        }

        [Test]
        public void RiverCourse_HasNoGapsOnTheOutsideOfABend()
        {
            // A quarter circle of radius 30 m, 8 m wide.
            var river = new RiverCourse();
            for (float a = 0f; a <= 90f; a += 3f)
            {
                float rad = a * Mathf.Deg2Rad;
                river.Add(new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 30f, new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)), 4f);
            }
            river.Close();
            for (float a = 0f; a <= 90f; a += 0.5f)
            {
                float rad = a * Mathf.Deg2Rad;
                Assert.That(river.TryGetLevel(new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 33.5f, out _), Is.True, $"outer edge at {a}°");
            }
        }

        [Test]
        public void SpawnZone_WithPoints_SamplesRoundThemOnly()
        {
            var zone = new GameObject("Zone").AddComponent<SpawnZone>();
            var cave = new GameObject("Cave").transform;
            var gap = new GameObject("Gap").transform;
            try
            {
                cave.position = new Vector3(100f, 5f, 0f);
                gap.position = new Vector3(0f, 5f, 100f);
                var serialized = new UnityEditor.SerializedObject(zone);
                SerializedPropertyList(serialized, "_points", cave, null, gap);
                serialized.FindProperty("_pointRadius").floatValue = 3f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(zone.UsesPoints, Is.True);
                int nearCave = 0;
                for (float a = 0f; a <= 1f; a += 0.05f)
                for (float b = 0f; b <= 1f; b += 0.25f)
                {
                    Vector3 point = zone.Sample(a, b);
                    float toCave = Vector3.Distance(Flat(point), Flat(cave.position));
                    float toGap = Vector3.Distance(Flat(point), Flat(gap.position));
                    Assert.That(Mathf.Min(toCave, toGap), Is.LessThanOrEqualTo(3f + 1e-3f), $"sample ({a}, {b})");
                    if (toCave < toGap)
                        nearCave++;
                }
                Assert.That(nearCave, Is.GreaterThan(0), "the spot after an empty slot is still picked");
            }
            finally
            {
                Object.DestroyImmediate(zone.gameObject);
                Object.DestroyImmediate(cave.gameObject);
                Object.DestroyImmediate(gap.gameObject);
            }
        }

        static void SerializedPropertyList(UnityEditor.SerializedObject serialized, string name, params Transform[] items)
        {
            UnityEditor.SerializedProperty list = serialized.FindProperty(name);
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        static Vector3 Flat(Vector3 point) => new(point.x, 0f, point.z);
    }
}
