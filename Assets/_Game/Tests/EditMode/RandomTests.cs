using System.Collections.Generic;
using Aipocalypse.Core.RandomSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public sealed class RandomTests
    {
        static float[] Rolls(RandomStream stream, int count)
        {
            var rolls = new float[count];
            for (int i = 0; i < count; i++)
                rolls[i] = stream.Value;
            return rolls;
        }

        [Test]
        public void Stream_SameSeedGivesTheSameRolls()
        {
            Assert.AreEqual(Rolls(new RandomStream(42), 20), Rolls(new RandomStream(42), 20));
            Assert.AreNotEqual(Rolls(new RandomStream(42), 20), Rolls(new RandomStream(43), 20));
        }

        [Test]
        public void Stream_StaysInsideItsRanges()
        {
            var stream = new RandomStream(7);
            for (int i = 0; i < 5000; i++)
            {
                float value = stream.Value;
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(stream.Range(-2f, 3f), Is.GreaterThanOrEqualTo(-2f).And.LessThan(3f));
                Assert.That(stream.Range(5, 8), Is.InRange(5, 7));
                Assert.That(stream.RangeInclusive(new Vector2Int(2, 4)), Is.InRange(2, 4));
                Assert.That(stream.InsideUnitCircle.sqrMagnitude, Is.LessThanOrEqualTo(1.0001f));
                Assert.AreEqual(1f, stream.OnUnitSphere.magnitude, 0.001f);
            }
            Assert.AreEqual(5, stream.Range(5, 5));
        }

        [Test]
        public void Stream_RangeReachesEveryValue()
        {
            var stream = new RandomStream(1);
            var seen = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
                seen.Add(stream.Range(0, 6));
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void Stream_ShuffleKeepsEveryItem()
        {
            var items = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            new RandomStream(3).Shuffle(items);
            items.Sort();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, items);
        }

        [Test]
        public void Service_NamedStreamsAreIndependentAndReseedRestartsThem()
        {
            var service = new RandomService(99);
            RandomStream loot = service.Stream("loot");
            Assert.AreSame(loot, service.Stream("loot"));
            float[] first = Rolls(loot, 10);

            // Rolling another stream doesn't change loot's sequence.
            var other = new RandomService(99);
            Rolls(other.Stream("raids"), 50);
            Assert.AreEqual(first, Rolls(other.Stream("loot"), 10));
            Assert.AreNotEqual(first, Rolls(new RandomService(99).Stream("raids"), 10));

            service.Reseed(99);
            Assert.AreEqual(first, Rolls(loot, 10));
            Assert.AreEqual(99UL, service.Seed);
        }

        [Test]
        public void WeightedPick_PicksByShareOfTheTotal()
        {
            var weights = new[] { 3f, 1f };
            Assert.AreEqual(0, WeightedPick.Index(weights, 0f));
            Assert.AreEqual(0, WeightedPick.Index(weights, 0.74f));
            Assert.AreEqual(1, WeightedPick.Index(weights, 0.76f));
            Assert.AreEqual(1, WeightedPick.Index(weights, 1f));
        }

        [Test]
        public void WeightedPick_NothingWeightAndEmptyWeightsGiveMinusOne()
        {
            var weights = new[] { 1f, 1f };
            Assert.AreEqual(1, WeightedPick.Index(weights, 0.49f, nothingWeight: 2f));
            Assert.AreEqual(-1, WeightedPick.Index(weights, 0.51f, nothingWeight: 2f));
            Assert.AreEqual(-1, WeightedPick.Index(new[] { 0f, -1f }, 0.5f));
            Assert.AreEqual(-1, WeightedPick.Index(new float[0], 0.5f));
        }

        [Test]
        public void WeightedPick_SkipsZeroWeights()
        {
            var weights = new[] { 0f, 2f, 0f };
            Assert.AreEqual(1, WeightedPick.Index(weights, 0f));
            Assert.AreEqual(1, WeightedPick.Index(weights, 1f));
        }

        [Test]
        public void WeightedTable_RollsCountPicksAndDropsNothingAndUnsetEntries()
        {
            var table = new WeightedTable<string> { Count = new Vector2Int(3, 3) };
            table.Entries.Add(new WeightedEntry<string> { Value = "ammo", Weight = 1f });
            table.Entries.Add(new WeightedEntry<string> { Value = null, Weight = 100f });
            var results = new List<string>();
            Assert.AreEqual(3, table.Roll(new RandomStream(5), results));
            CollectionAssert.AreEqual(new[] { "ammo", "ammo", "ammo" }, results);

            table.NothingWeight = 1f;
            Assert.IsTrue(table.TryPick(0.4f, out string picked));
            Assert.AreEqual("ammo", picked);
            Assert.IsFalse(table.TryPick(0.6f, out picked));
            Assert.IsNull(picked);
        }

        [Test]
        public void WeightedTable_ShareMatchesWeightsOverManyRolls()
        {
            var table = new WeightedTable<int>();
            table.Entries.Add(new WeightedEntry<int> { Value = 0, Weight = 3f });
            table.Entries.Add(new WeightedEntry<int> { Value = 1, Weight = 1f });
            var stream = new RandomStream(11);
            int zeros = 0;
            for (int i = 0; i < 10000; i++)
                if (table.TryPick(stream, out int value) && value == 0)
                    zeros++;
            Assert.AreEqual(0.75f, zeros / 10000f, 0.02f);
        }
    }
}
