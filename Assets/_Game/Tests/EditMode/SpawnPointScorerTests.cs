using Aipocalypse.Core.SpawnSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public sealed class SpawnPointScorerTests
    {
        static readonly SpawnRules Rules = new() { MinPlayerDistance = 30f };

        static SpawnCandidate Point(float distance, bool seen, float angle = 0f) => new(Vector3.zero, distance, seen, angle);

        [Test]
        public void Choose_NoCandidates_ReturnsNone()
        {
            Assert.AreEqual(-1, SpawnPointScorer.Choose(new SpawnCandidate[0], Rules, true, 0.5f));
        }

        [Test]
        public void Choose_PrefersAHiddenPoint_OverVisibleOnes()
        {
            var candidates = new[] { Point(70f, true, 80f), Point(70f, false), Point(70f, true, 120f) };

            Assert.AreEqual(1, SpawnPointScorer.Choose(candidates, Rules, true, 0.5f));
        }

        [Test]
        public void Choose_PicksAmongHiddenPoints_ByTheRoll()
        {
            var candidates = new[] { Point(70f, false), Point(70f, true), Point(70f, false), Point(70f, false) };

            Assert.AreEqual(0, SpawnPointScorer.Choose(candidates, Rules, false, 0f));
            Assert.AreEqual(2, SpawnPointScorer.Choose(candidates, Rules, false, 0.5f));
            Assert.AreEqual(3, SpawnPointScorer.Choose(candidates, Rules, false, 0.99f));
            Assert.AreEqual(3, SpawnPointScorer.Choose(candidates, Rules, false, 1f), "a roll of 1 stays in range");
        }

        [Test]
        public void Choose_RejectsPointsCloserThanMinPlayerDistance()
        {
            var candidates = new[] { Point(29f, false), Point(70f, true, 60f) };

            Assert.AreEqual(-1, SpawnPointScorer.Choose(candidates, Rules, false, 0f), "the hidden point is too close");
            Assert.AreEqual(1, SpawnPointScorer.Choose(candidates, Rules, true, 0f));
            Assert.AreEqual(-1, SpawnPointScorer.Choose(new[] { Point(10f, true, 170f) }, Rules, true, 0f));
        }

        [Test]
        public void Choose_OnlyVisiblePoints_WaitUntilVisibleIsAllowed()
        {
            var candidates = new[] { Point(70f, true, 10f), Point(70f, true, 40f) };

            Assert.AreEqual(-1, SpawnPointScorer.Choose(candidates, Rules, false, 0f));
        }

        [Test]
        public void Choose_VisibleAllowed_TakesThePointFarthestFromTheViewCentre()
        {
            var candidates = new[] { Point(70f, true, 10f), Point(70f, true, 50f), Point(20f, true, 90f), Point(70f, true, 30f) };

            Assert.AreEqual(1, SpawnPointScorer.Choose(candidates, Rules, true, 0f), "the widest one is too close to the player");
        }
    }
}
