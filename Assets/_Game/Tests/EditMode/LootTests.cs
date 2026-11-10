using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class LootTests
    {
        readonly List<Object> _created = new();
        AmmoType _rounds;
        CurrencyDefinition _tokens;

        [SetUp]
        public void SetUp()
        {
            _rounds = Track(TestItems.Create<AmmoType>(maxStack: 30));
            _tokens = Track(TestItems.Create<CurrencyDefinition>(unlimited: true));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        LootContainer Container(bool hideWhenEmpty = false)
        {
            var go = Track(new GameObject("Loot"));
            var container = go.AddComponent<LootContainer>();
            var so = new UnityEditor.SerializedObject(container);
            so.FindProperty("_hideWhenEmpty").boolValue = hideWhenEmpty;
            so.ApplyModifiedPropertiesWithoutUndo();
            return container;
        }

        LootTable Table(float nothingWeight, Vector2Int picks, params (ItemDefinition item, Vector2Int count, float weight)[] drops)
        {
            var table = Track(ScriptableObject.CreateInstance<LootTable>());
            table.Drops.NothingWeight = nothingWeight;
            table.Drops.Count = picks;
            foreach ((ItemDefinition item, Vector2Int count, float weight) in drops)
                table.Drops.Entries.Add(new WeightedEntry<LootDrop> { Value = new LootDrop { Item = item, Count = count }, Weight = weight });
            return table;
        }

        [Test]
        public void LootTable_RollsPicksWithAmountsInRange()
        {
            LootTable table = Table(0f, new Vector2Int(2, 2), (_tokens, new Vector2Int(5, 20), 1f));
            var results = new List<ItemInstance>();

            table.Roll(new RandomStream(7), results);

            Assert.AreEqual(2, results.Count);
            foreach (ItemInstance item in results)
            {
                Assert.AreSame(_tokens, item.Definition);
                Assert.That(item.Count, Is.InRange(5, 20));
            }
        }

        [Test]
        public void LootTable_NothingOnly_RollsNothing()
        {
            LootTable table = Table(1f, new Vector2Int(3, 3), (_tokens, Vector2Int.one, 0f));
            var results = new List<ItemInstance>();

            table.Roll(new RandomStream(1), results);

            Assert.IsEmpty(results);
        }

        [Test]
        public void LootTable_SameSeed_SameLoot()
        {
            LootTable table = Table(1f, new Vector2Int(1, 3), (_tokens, new Vector2Int(1, 50), 2f), (_rounds, new Vector2Int(5, 10), 1f));
            var first = new List<ItemInstance>();
            var second = new List<ItemInstance>();

            table.Roll(new RandomStream(42), first);
            table.Roll(new RandomStream(42), second);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i].Definition, second[i].Definition);
                Assert.AreEqual(first[i].Count, second[i].Count);
            }
        }

        [Test]
        public void Fill_MergesSameItems_AndHidesThemAgain()
        {
            LootContainer container = Container();
            container.Fill(new[] { _tokens.CreateInstance(1) });
            container.RevealAll();
            Assert.IsTrue(container.Searched);

            container.Fill(new[] { _tokens.CreateInstance(5), _tokens.CreateInstance(7), _rounds.CreateInstance(10) });

            Assert.AreEqual(12, container.Items.CountOf(_tokens));
            Assert.AreEqual(2, container.Items.UsedSlots);
            Assert.IsFalse(container.Searched);
            Assert.AreEqual(2, container.HiddenCount);
            Assert.IsTrue(container.IsHidden(0) && container.IsHidden(1));
            Assert.IsFalse(container.IsHidden(2), "an empty slot hides nothing");
        }

        [Test]
        public void Reveal_GoesTopToBottom_AndAnEmptiedSlotIsNoLongerHidden()
        {
            LootContainer container = Container();
            container.Fill(new[] { _tokens.CreateInstance(5), _rounds.CreateInstance(3), _rounds.CreateInstance(30) });
            Assert.AreEqual(3, container.HiddenCount, "30 + 3 rounds are two stacks");

            Assert.AreEqual(0, container.RevealNext());
            Assert.AreEqual(1, container.NextHidden);
            container.Items.Remove(2);

            Assert.AreEqual(1, container.HiddenCount);
            Assert.AreEqual(1, container.RevealNext());
            Assert.IsTrue(container.Searched);
            Assert.AreEqual(-1, container.RevealNext());
        }

        [Test]
        public void Emptied_FiresOnceWhenTheLastItemIsTaken()
        {
            LootContainer container = Container();
            container.Fill(new[] { _tokens.CreateInstance(5), _rounds.CreateInstance(3) });
            int emptied = 0;
            container.Emptied += _ => emptied++;

            container.Items.Remove(0);
            Assert.AreEqual(0, emptied);
            container.Items.Take(_rounds, 3);

            Assert.AreEqual(1, emptied);
        }

        [Test]
        public void HideWhenEmpty_MakesAnEmptyBodyUnsearchable()
        {
            LootContainer body = Container(hideWhenEmpty: true);
            Assert.IsFalse(body.Searchable);

            body.Fill(new[] { _tokens.CreateInstance(3) });
            Assert.IsTrue(body.Searchable);

            body.SetSearchable(false);
            Assert.IsFalse(body.Searchable);
            Assert.IsTrue(Container().Searchable, "a crate can be opened empty");
        }

        [Test]
        public void Search_OpensAtOnce_ThenRevealsOneStackPerSecond()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerInteractionTuning>());
            var looting = new PlayerLooting(tuning);
            LootContainer crate = Container();
            crate.Fill(new[] { _tokens.CreateInstance(5), _rounds.CreateInstance(3), _rounds.CreateInstance(30) });
            LootContainer opened = null;
            var revealed = new List<int>();
            looting.Opened += c => opened = c;
            looting.Revealed += (_, slot) => revealed.Add(slot);

            Assert.AreEqual(3f, looting.SearchTime(crate), 0.0001f, "1 s per stack");
            looting.Begin(crate);
            Assert.AreSame(crate, opened, "the loot panel opens at once");
            Assert.AreEqual(0, looting.RevealingSlot);

            looting.Tick(1.5f);
            CollectionAssert.AreEqual(new[] { 0 }, revealed);
            Assert.AreEqual(0.5f, looting.Progress, 0.001f);
            Assert.AreEqual(1, looting.RevealingSlot);
            looting.Tick(1.51f);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, revealed);
            Assert.IsTrue(crate.Searched);
            Assert.IsFalse(looting.IsSearching);
            Assert.AreEqual(0f, looting.SearchTime(crate));
        }

        [Test]
        public void Search_StoppedByClosing_CarriesOnWhereItStopped()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerInteractionTuning>());
            var looting = new PlayerLooting(tuning);
            LootContainer crate = Container();
            crate.Fill(new[] { _tokens.CreateInstance(5), _rounds.CreateInstance(3) });

            looting.Begin(crate);
            looting.Tick(1.6f);
            looting.Stop();
            looting.Tick(5f);

            Assert.AreEqual(1, crate.HiddenCount, "nothing revealed after closing");
            looting.Begin(crate);
            Assert.AreEqual(1, looting.RevealingSlot, "carries on with what's still hidden");
            Assert.AreEqual(0f, looting.Progress, "the stack being revealed starts over");
            looting.Tick(1f);
            Assert.IsTrue(crate.Searched);
        }

        [Test]
        public void Search_StopsWhenTheContainerCantBeSearched()
        {
            var tuning = Track(ScriptableObject.CreateInstance<PlayerInteractionTuning>());
            var looting = new PlayerLooting(tuning);
            LootContainer body = Container(hideWhenEmpty: true);
            body.Fill(new[] { _tokens.CreateInstance(5) });

            looting.Begin(body);
            body.SetSearchable(false);
            looting.Tick(5f);

            Assert.IsFalse(looting.IsSearching);
            Assert.IsFalse(body.Searched);
        }
    }
}
