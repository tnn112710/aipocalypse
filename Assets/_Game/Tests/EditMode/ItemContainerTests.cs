using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class ItemContainerTests
    {
        readonly List<Object> _created = new();
        AmmoType _rounds;
        AmmoType _shells;
        CurrencyDefinition _tokens;
        GunDefinition _gun;

        [SetUp]
        public void SetUp()
        {
            _rounds = Track(TestItems.Create<AmmoType>(maxStack: 10, weight: 0.01f));
            _shells = Track(TestItems.Create<AmmoType>(maxStack: 5, weight: 0.05f));
            _tokens = Track(TestItems.Create<CurrencyDefinition>(unlimited: true));
            _gun = Track(TestItems.Create<GunDefinition>(weight: 3f));
            _rounds.name = "Rounds";
            _shells.name = "Shells";
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

        [Test]
        public void TryAdd_TopsUpStacksFirst_ThenUsesFreeSlots()
        {
            var bag = new ItemContainer(3);
            bag.TryAdd(_rounds.CreateInstance(7), out _);

            Assert.IsTrue(bag.TryAdd(_rounds.CreateInstance(6), out int added));

            Assert.AreEqual(6, added);
            Assert.AreEqual(10, bag[0].Count);
            Assert.AreEqual(3, bag[1].Count);
            Assert.AreEqual(13, bag.CountOf(_rounds));
        }

        [Test]
        public void TryAdd_WhenFull_TakesWhatFits_AndLeavesTheRestOnTheItem()
        {
            var bag = new ItemContainer(1);
            bag.TryAdd(_rounds.CreateInstance(8), out _);
            ItemInstance pile = _rounds.CreateInstance(5);

            Assert.IsFalse(bag.TryAdd(pile, out int added));

            Assert.AreEqual(2, added);
            Assert.AreEqual(3, pile.Count);
            Assert.AreEqual(10, bag[0].Count);
        }

        [Test]
        public void TryAdd_PlacesTheItemItself_SoAGunKeepsItsState()
        {
            var bag = new ItemContainer(2);
            ItemInstance gun = _gun.CreateInstance();

            Assert.IsTrue(bag.TryAdd(gun, out _));

            Assert.AreSame(gun, bag[0]);
            Assert.IsFalse(bag.TryAdd(gun, out _), "already inside");
        }

        [Test]
        public void UnlimitedStack_TakesAnyAmountInOneSlot()
        {
            var bag = new ItemContainer(1);
            bag.TryAdd(_tokens.CreateInstance(1000), out _);

            Assert.IsTrue(bag.TryAdd(_tokens.CreateInstance(5_000_000), out _));

            Assert.AreEqual(5_001_000, bag.CountOf(_tokens));
            Assert.AreEqual(1, bag.UsedSlots);
            Assert.Greater(bag.SpaceFor(_tokens.CreateInstance()), 1_000_000);
        }

        [Test]
        public void Take_TakesFromTheLastStack_AndEmptiedStacksLeaveTheirSlot()
        {
            var bag = new ItemContainer(3);
            bag.TryAdd(_rounds.CreateInstance(10), out _);
            bag.TryAdd(_rounds.CreateInstance(10), out _);
            bag.TryAdd(_rounds.CreateInstance(4), out _);

            Assert.AreEqual(6, bag.Take(_rounds, 6));

            Assert.IsNull(bag[2], "the last stack ran out and left");
            Assert.AreEqual(8, bag[1].Count);
            Assert.AreEqual(10, bag[0].Count);
            Assert.AreEqual(18, bag.Take(_rounds, 50), "takes only what's there");
            Assert.IsTrue(bag.IsEmpty);
        }

        [Test]
        public void ConsumingAStackOutsideTheContainer_StillClearsItsSlot_AndRaisesChanged()
        {
            var bag = new ItemContainer(2);
            ItemInstance stack = _rounds.CreateInstance(2);
            bag.TryAdd(stack, out _);
            int changed = 0;
            bag.Changed += () => changed++;

            stack.Consume(2);

            Assert.IsNull(bag[0]);
            Assert.Greater(changed, 0);
        }

        [Test]
        public void SpaceFor_CountsStackRoomAndFreeSlots_ThatTakeTheItem()
        {
            var bag = new ItemContainer(3);
            bag.TryAdd(_rounds.CreateInstance(7), out _);
            bag.TryAdd(_gun.CreateInstance(), out _);

            Assert.AreEqual(3 + 10, bag.SpaceFor(_rounds.CreateInstance()));

            var gunsOnly = new ItemContainer(new[] { ItemCategory.Gun, ItemCategory.Gun });
            Assert.AreEqual(0, gunsOnly.SpaceFor(_rounds.CreateInstance()));
            Assert.AreEqual(2, gunsOnly.SpaceFor(_gun.CreateInstance()));
        }

        [Test]
        public void Move_IntoAnEmptySlot_MovesTheWholeStack()
        {
            var bag = new ItemContainer(2);
            var loot = new ItemContainer(2);
            ItemInstance stack = _rounds.CreateInstance(6);
            loot.TryAdd(stack, out _);

            Assert.IsTrue(ItemContainer.Move(loot, 0, bag, 1));

            Assert.AreSame(stack, bag[1]);
            Assert.IsNull(loot[0]);
        }

        [Test]
        public void Move_PartOfAStack_SplitsIt()
        {
            var bag = new ItemContainer(2);
            bag.TryAdd(_rounds.CreateInstance(9), out _);

            Assert.IsTrue(ItemContainer.Move(bag, 0, bag, 1, amount: 4));

            Assert.AreEqual(5, bag[0].Count);
            Assert.AreEqual(4, bag[1].Count);
        }

        [Test]
        public void Move_OntoTheSameItem_MergesAsMuchAsFits()
        {
            var bag = new ItemContainer(2);
            bag.TryPlace(0, _rounds.CreateInstance(8));
            bag.TryPlace(1, _rounds.CreateInstance(5));

            Assert.IsTrue(ItemContainer.Move(bag, 1, bag, 0));

            Assert.AreEqual(10, bag[0].Count);
            Assert.AreEqual(3, bag[1].Count, "the rest stays where it was");
        }

        [Test]
        public void Move_OntoADifferentItem_SwapsThem_OnlyWhenBothSlotsTakeTheOther()
        {
            var bag = new ItemContainer(2);
            var guns = new ItemContainer(new[] { ItemCategory.Gun });
            ItemInstance rounds = _rounds.CreateInstance(3);
            ItemInstance gun = _gun.CreateInstance();
            ItemInstance otherGun = _gun.CreateInstance();
            bag.TryPlace(0, rounds);
            bag.TryPlace(1, otherGun);
            guns.TryPlace(0, gun);

            Assert.IsFalse(ItemContainer.Move(bag, 0, guns, 0), "the gun slot doesn't take rounds");
            Assert.IsTrue(ItemContainer.Move(bag, 1, guns, 0));

            Assert.AreSame(otherGun, guns[0]);
            Assert.AreSame(gun, bag[1]);
            Assert.IsTrue(ItemContainer.Move(bag, 0, bag, 1), "anything swaps inside a bag");
            Assert.AreSame(rounds, bag[1]);
        }

        [Test]
        public void Resize_Grows_AndShrinksOnlyOverEmptySlots()
        {
            var bag = new ItemContainer(2);
            bag.TryPlace(1, _rounds.CreateInstance());
            int resized = 0;
            bag.Resized += () => resized++;

            Assert.IsTrue(bag.Resize(4));
            Assert.AreEqual(4, bag.Count);
            Assert.IsFalse(bag.Resize(1), "slot 2 holds an item");
            Assert.AreEqual(4, bag.Count);
            Assert.IsTrue(bag.Resize(2));
            Assert.AreEqual(2, resized);
            Assert.IsFalse(new ItemContainer(new[] { ItemCategory.Gun }).Resize(3), "fixed slots don't resize");
        }

        [Test]
        public void Sort_MergesStacks_AndPacksByKind()
        {
            var bag = new ItemContainer(5);
            ItemInstance gun = _gun.CreateInstance();
            bag.TryPlace(0, _shells.CreateInstance(2));
            bag.TryPlace(1, _rounds.CreateInstance(3));
            bag.TryPlace(3, gun);
            bag.TryPlace(4, _rounds.CreateInstance(4));

            bag.Sort();

            Assert.AreSame(gun, bag[0], "guns sort first");
            Assert.AreSame(_rounds, bag[1].Definition);
            Assert.AreEqual(7, bag[1].Count, "partial stacks merged");
            Assert.AreSame(_shells, bag[2].Definition);
            Assert.IsNull(bag[3]);
            Assert.IsNull(bag[4]);
        }

        [Test]
        public void Weight_SumsUnitsTimesTheirWeight()
        {
            var bag = new ItemContainer(3);
            bag.TryAdd(_rounds.CreateInstance(10), out _);
            bag.TryAdd(_gun.CreateInstance(), out _);
            bag.TryAdd(_tokens.CreateInstance(500), out _);

            Assert.AreEqual(10 * 0.01f + 3f, bag.Weight, 0.0001f);
        }
    }
}
