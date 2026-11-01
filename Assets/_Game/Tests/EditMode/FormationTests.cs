using System.Collections.Generic;
using Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public sealed class FormationTests
    {
        [Test]
        public void Scatter_FillsTheRadiusAndKeepsNeighboursApart()
        {
            const int count = 8;
            var offsets = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = FormationMath.Scatter(i, count, 4f, 1.2f, 0.3f);
                Assert.That(offsets[i].y, Is.Zero);
                Assert.That(offsets[i].magnitude, Is.LessThanOrEqualTo(4f + 1e-4f));
            }
            Assert.That(offsets[count - 1].magnitude, Is.GreaterThan(3f), "the outer members reach out near the radius");
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    Assert.That(Vector3.Distance(offsets[i], offsets[j]), Is.GreaterThan(1f), $"{i} and {j} too close");
        }

        [Test]
        public void Scatter_WidensABigGroupToKeepTheSpacing()
        {
            const int count = 60;
            float farthest = 0f;
            for (int i = 0; i < count; i++)
                farthest = Mathf.Max(farthest, FormationMath.Scatter(i, count, 3f, 1.2f, 0f).magnitude);
            Assert.That(farthest, Is.GreaterThan(4.5f));
        }

        [Test]
        public void Scatter_LoneMemberStandsOnTheAnchor() =>
            Assert.That(FormationMath.Scatter(0, 1, 4f, 1.2f, 1f), Is.EqualTo(Vector3.zero));

        [Test]
        public void Line_LeaderInTheMiddle_NeighboursSpacedEvenly()
        {
            Assert.That(FormationMath.Line(0, 2f), Is.EqualTo(Vector3.zero));
            var xs = new List<float>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 slot = FormationMath.Line(i, 2f);
                Assert.That(slot.z, Is.Zero, "abreast");
                xs.Add(slot.x);
            }
            xs.Sort();
            Assert.That(xs, Is.EqualTo(new[] { -4f, -2f, 0f, 2f, 4f }));
        }

        [Test]
        public void Column_FilesBehindTheLeader()
        {
            Assert.That(FormationMath.Column(0, 1.5f), Is.EqualTo(Vector3.zero));
            Assert.That(FormationMath.Column(3, 1.5f), Is.EqualTo(new Vector3(0f, 0f, -4.5f)));
        }

        [Test]
        public void Wedge_LeaderAtTheTip_PairsStepBackAndOut()
        {
            Assert.That(FormationMath.Wedge(0, 2f), Is.EqualTo(Vector3.zero));
            Assert.That(FormationMath.Wedge(1, 2f), Is.EqualTo(new Vector3(2f, 0f, -2f)));
            Assert.That(FormationMath.Wedge(2, 2f), Is.EqualTo(new Vector3(-2f, 0f, -2f)));
            Assert.That(FormationMath.Wedge(4, 2f), Is.EqualTo(new Vector3(-4f, 0f, -4f)));
        }

        [Test]
        public void Ring_SlotsOnACircle_WidenedToKeepTheSpacing()
        {
            for (int i = 0; i < 6; i++)
                Assert.That(FormationMath.Ring(i, 6, 5f, 1f).magnitude, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(FormationMath.Ring(0, 6, 5f, 1f).z, Is.EqualTo(5f).Within(1e-4f), "slot 0 in front");
            float widened = FormationMath.Ring(0, 40, 2f, 1.5f).magnitude;
            Assert.That(2f * Mathf.PI * widened / 40f, Is.GreaterThanOrEqualTo(1.5f - 1e-3f));
        }

        [Test]
        public void Clusters_SplitTheGroupSideBySide()
        {
            int left = 0;
            int right = 0;
            for (int i = 0; i < 8; i++)
            {
                if (FormationMath.Cluster(i, 8, 2, 10f, 1.2f, 0f).x < 0f)
                    left++;
                else
                    right++;
            }
            Assert.That(left, Is.EqualTo(4));
            Assert.That(right, Is.EqualTo(4));
        }

        [Test]
        public void Formation_GivesOneSlotPerMember_LeaderUnjittered()
        {
            var formation = ScriptableObject.CreateInstance<Formation>();
            try
            {
                formation.Shape = FormationShape.Custom;
                formation.Slots = new List<Vector3> { Vector3.zero, Vector3.right };
                formation.Jitter = 0.5f;
                var slots = new List<Vector3>();
                formation.GetSlots(5, slots);
                Assert.That(slots.Count, Is.EqualTo(5));
                Assert.That(slots[0], Is.EqualTo(Vector3.zero));
                Assert.That(slots[4].z, Is.LessThan(-1f), "repeated slots stand further back");
            }
            finally
            {
                Object.DestroyImmediate(formation);
            }
        }
    }
}
