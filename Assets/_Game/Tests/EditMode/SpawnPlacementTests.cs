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
    }
}
