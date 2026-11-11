using MB13.Aipocalypse.Core.MapSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class MinimapTests
    {
        const float Tolerance = 1e-4f;

        static void AssertNear(Vector2 actual, Vector2 expected, string message = null) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance), $"{message}: got {actual}, expected {expected}");

        [Test]
        public void Heading_IsCompassDegrees()
        {
            Assert.That(MinimapMath.Heading(Vector3.forward), Is.EqualTo(0f).Within(Tolerance), "north");
            Assert.That(MinimapMath.Heading(Vector3.right), Is.EqualTo(90f).Within(Tolerance), "east");
            Assert.That(Mathf.Abs(MinimapMath.Heading(Vector3.back)), Is.EqualTo(180f).Within(Tolerance), "south");
            Assert.That(MinimapMath.Heading(Vector3.left), Is.EqualTo(-90f).Within(Tolerance), "west");
        }

        [Test]
        public void ToDisc_FacingNorth_IsAPlainTopDownView()
        {
            AssertNear(MinimapMath.ToDisc(new Vector3(0f, 5f, 10f), 0f, 2f), new Vector2(0f, 20f), "ahead is up, height ignored");
            AssertNear(MinimapMath.ToDisc(new Vector3(10f, 0f, 0f), 0f, 2f), new Vector2(20f, 0f), "east is right");
        }

        [Test]
        public void ToDisc_WhatThePlayerFacesIsAlwaysUp()
        {
            AssertNear(MinimapMath.ToDisc(Vector3.right, 90f, 1f), Vector2.up, "facing east: east is up");
            AssertNear(MinimapMath.ToDisc(Vector3.forward, 90f, 1f), Vector2.left, "facing east: north is left");
            AssertNear(MinimapMath.ToDisc(Vector3.forward, 180f, 1f), Vector2.down, "facing south: north is down");
            AssertNear(MinimapMath.ToDisc(Vector3.forward, -90f, 1f), Vector2.right, "facing west: north is right");
        }

        [Test]
        public void ClampToRadius_KeepsInsidePoints_AndPullsOthersOntoTheRim()
        {
            var inside = new Vector2(3f, 4f);
            Assert.That(MinimapMath.ClampToRadius(ref inside, 5f), Is.True);
            AssertNear(inside, new Vector2(3f, 4f), "unchanged");

            var outside = new Vector2(30f, 40f);
            Assert.That(MinimapMath.ClampToRadius(ref outside, 5f), Is.False);
            AssertNear(outside, new Vector2(3f, 4f), "same direction, on the rim");
        }

        [Test]
        public void Registry_AddsOnce_AndRemoves()
        {
            var registry = new MapMarkerRegistry();
            var go = new GameObject("Marker");
            try
            {
                var marker = go.AddComponent<MapMarker>();
                registry.Add(marker);
                registry.Add(marker);
                Assert.That(registry.Markers.Count, Is.EqualTo(1));
                registry.Remove(marker);
                Assert.That(registry.Markers.Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
