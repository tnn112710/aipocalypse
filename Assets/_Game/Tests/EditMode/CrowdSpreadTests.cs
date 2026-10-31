using Aipocalypse.Core.EnemySystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class CrowdSpreadTests
    {
        [Test]
        public void GroupOffset_FillsTheRadiusAndKeepsNeighboursApart()
        {
            const int count = 8;
            var offsets = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = CrowdSpread.GroupOffset(i, count, 4f, 1.2f, 0.3f);
                Assert.That(offsets[i].y, Is.Zero);
                Assert.That(offsets[i].magnitude, Is.LessThanOrEqualTo(4f + 1e-4f));
            }
            Assert.That(offsets[count - 1].magnitude, Is.GreaterThan(3f), "the outer members reach out near the radius");
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    Assert.That(Vector3.Distance(offsets[i], offsets[j]), Is.GreaterThan(1f), $"{i} and {j} too close");
        }

        [Test]
        public void GroupOffset_WidensABigGroupToKeepTheSpacing()
        {
            const int count = 60;
            float farthest = 0f;
            for (int i = 0; i < count; i++)
                farthest = Mathf.Max(farthest, CrowdSpread.GroupOffset(i, count, 3f, 1.2f, 0f).magnitude);
            Assert.That(farthest, Is.GreaterThan(4.5f));
        }

        [Test]
        public void GroupOffset_LoneEnemyStandsOnThePoint() =>
            Assert.That(CrowdSpread.GroupOffset(0, 1, 4f, 1.2f, 1f), Is.EqualTo(Vector3.zero));

        [Test]
        public void SpeedFactor_StaysWithinTheVariance()
        {
            Assert.That(CrowdSpread.SpeedFactor(0.12f, 0f), Is.EqualTo(0.88f).Within(1e-5f));
            Assert.That(CrowdSpread.SpeedFactor(0.12f, 1f), Is.EqualTo(1.12f).Within(1e-5f));
            Assert.That(CrowdSpread.SpeedFactor(0.12f, 0.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(CrowdSpread.SpeedFactor(0f, 0.9f), Is.EqualTo(1f));
        }

        [Test]
        public void FlankPoint_TurnsTheHeadingByTheAngleFarAway()
        {
            var target = new Vector3(0f, 0f, 0f);
            var self = new Vector3(0f, 0f, 60f);
            Vector3 aim = CrowdSpread.FlankPoint(self, target, 1f, 30f, 25f, 5f);
            float heading = Vector3.Angle(target - self, aim - self);
            Assert.That(heading, Is.EqualTo(30f).Within(0.01f));
            Vector3 other = CrowdSpread.FlankPoint(self, target, -1f, 30f, 25f, 5f);
            Assert.That(Mathf.Sign(aim.x), Is.Not.EqualTo(Mathf.Sign(other.x)), "the two sides go opposite ways");
        }

        [Test]
        public void FlankPoint_StraightensAsItCloses()
        {
            var target = new Vector3(3f, 1f, -2f);
            Assert.That(CrowdSpread.FlankPoint(target + new Vector3(4f, 0f, 0f), target, 1f, 30f, 25f, 5f), Is.EqualTo(target), "inside the engage radius");
            Assert.That(CrowdSpread.FlankPoint(target + new Vector3(0f, 0f, 40f), target, 0f, 30f, 25f, 5f), Is.EqualTo(target), "no side");

            Vector3 self = target + new Vector3(0f, 0f, 15f);
            float halfway = Vector3.Angle(target - self, CrowdSpread.FlankPoint(self, target, 1f, 30f, 25f, 5f) - self);
            Assert.That(halfway, Is.EqualTo(15f).Within(0.01f), "half the angle halfway through the fade");
        }
    }
}
