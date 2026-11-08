using System.Collections.Generic;
using MB13.Aipocalypse.Core.SpawnSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public sealed class SpawnTableTests
    {
        sealed class Counts : ISpawnCounts
        {
            public readonly Dictionary<SpawnCard, int> Alive = new();
            public int CountAlive(SpawnCard card) => Alive.TryGetValue(card, out int alive) ? alive : 0;
            public int CountAlive(SpawnCategory category) => 0;
        }

        SpawnTable _table;
        SpawnCard _deer;
        SpawnCard _wolf;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<SpawnTable>();
            _deer = ScriptableObject.CreateInstance<SpawnCard>();
            _wolf = ScriptableObject.CreateInstance<SpawnCard>();
            _table.Entries.Add(new SpawnTableEntry { Card = _deer, Weight = 3f });
            _table.Entries.Add(new SpawnTableEntry { Card = _wolf, Weight = 1f });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_deer);
            Object.DestroyImmediate(_wolf);
        }

        [Test]
        public void Table_PicksByWeight()
        {
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0f), Is.SameAs(_deer));
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0.74f), Is.SameAs(_deer), "the first three quarters");
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0.76f), Is.SameAs(_wolf), "the last quarter");
            Assert.That(_table.Pick(SpawnConditions.Any, null, 1f), Is.SameAs(_wolf), "a roll of 1 still picks one");
        }

        [Test]
        public void Table_SkipsEntriesOutsideTheirPhaseOrDays()
        {
            _table.Entries[1].Phases.Add(DayPhase.Midnight);
            _table.Entries[0].Days = new Vector2Int(3, 0);
            Assert.That(_table.Pick(new SpawnConditions(DayPhase.Midnight, 1), null, 0f), Is.SameAs(_wolf), "deer only from day 3");
            Assert.That(_table.Pick(new SpawnConditions(DayPhase.Morning, 1), null, 0.5f), Is.Null, "wolves only at midnight");
            Assert.That(_table.Pick(new SpawnConditions(DayPhase.Morning, 3), null, 0.99f), Is.SameAs(_deer));
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0f), Is.SameAs(_deer), "unknown conditions filter nothing");
        }

        [Test]
        public void Table_RespectsMaxAlive()
        {
            _table.Entries[0].MaxAlive = 2;
            var counts = new Counts();
            counts.Alive[_deer] = 2;
            Assert.That(_table.Pick(SpawnConditions.Any, counts, 0f), Is.SameAs(_wolf), "two deer are out already");
            counts.Alive[_deer] = 1;
            Assert.That(_table.Pick(SpawnConditions.Any, counts, 0f), Is.SameAs(_deer));
        }

        [Test]
        public void Table_NothingWeightCanPickNothing()
        {
            _table.NothingWeight = 4f;
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0.1f), Is.SameAs(_deer));
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0.6f), Is.Null, "the last half is nothing");
            _table.Entries.Clear();
            Assert.That(_table.Pick(SpawnConditions.Any, null, 0f), Is.Null);
        }

        [Test]
        public void Squad_FillsSlotsInMemberOrder()
        {
            var squad = ScriptableObject.CreateInstance<SpawnSquad>();
            try
            {
                squad.Members.Add(new SquadMember(_wolf, 1));
                squad.Members.Add(new SquadMember(_deer, 2));
                Assert.That(squad.Count, Is.EqualTo(3));
                Assert.That(squad.Lead, Is.SameAs(_wolf));
                Assert.That(squad.CardAt(0), Is.SameAs(_wolf));
                Assert.That(squad.CardAt(2), Is.SameAs(_deer));
                Assert.That(squad.CardAt(3), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(squad);
            }
        }
    }
}
