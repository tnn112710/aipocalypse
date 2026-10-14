using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace Aipocalypse.Tests
{
    public class EquipmentTests
    {
        // Default definition times: equip 0.35 s, holster 0.25 s.
        const float Equip = 0.35f;
        const float Holster = 0.25f;

        const ItemCategory Weapon = ItemCategory.Gun | ItemCategory.Melee;

        ItemInstance _rifle, _pistol, _axe, _rifle2;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _pistol = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _axe = ScriptableObject.CreateInstance<MeleeDefinition>().CreateInstance();
            _rifle2 = _rifle.Definition.CreateInstance();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rifle.Definition);
            Object.DestroyImmediate(_pistol.Definition);
            Object.DestroyImmediate(_axe.Definition);
        }

        static Hotbar GunsAndMelee() => new(new[] { ItemCategory.Gun, ItemCategory.Gun, ItemCategory.Melee });
        static Hotbar AnyWeapon() => new(new[] { Weapon, Weapon, Weapon });

        static Equipment Empty(Hotbar hotbar)
        {
            var equipment = new Equipment(hotbar);
            equipment.EquipFirstImmediately();
            return equipment;
        }

        static void Settle(Equipment equipment) => equipment.Tick(10f, 1f);

        Equipment Loaded()
        {
            Hotbar hotbar = GunsAndMelee();
            hotbar.TryPlace(0, _rifle);
            hotbar.TryPlace(1, _pistol);
            hotbar.TryPlace(2, _axe);
            var equipment = new Equipment(hotbar);
            equipment.EquipFirstImmediately();
            return equipment;
        }

        [Test]
        public void Hotbar_RejectsWrongCategory()
        {
            Hotbar hotbar = GunsAndMelee();

            Assert.IsFalse(hotbar.TryPlace(0, _axe));
            Assert.IsFalse(hotbar.TryPlace(2, _rifle));
            Assert.IsTrue(hotbar.TryPlace(2, _axe));
        }

        [Test]
        public void Hotbar_RejectsMoreThanNineSlots()
        {
            Assert.Throws<System.ArgumentException>(() => new Hotbar(new ItemCategory[Hotbar.MaxSlots + 1]));
        }

        [Test]
        public void FindNext_SkipsEmptyAndWraps()
        {
            Hotbar hotbar = GunsAndMelee();
            hotbar.TryPlace(0, _rifle);
            hotbar.TryPlace(2, _axe);

            Assert.AreEqual(2, hotbar.FindNext(0, 1, _ => true));
            Assert.AreEqual(0, hotbar.FindNext(2, 1, _ => true));
            Assert.AreEqual(2, hotbar.FindNext(0, -1, _ => true));
            Assert.AreEqual(0, hotbar.FindNext(-1, 1, _ => true));
        }

        [Test]
        public void Switch_HolstersThenEquips_OverItemTimes()
        {
            Equipment equipment = Loaded();
            EquipmentSwitcher switcher = equipment.Switcher;
            Assert.AreEqual(SwitchPhase.Ready, switcher.Phase);

            equipment.Select(1);
            equipment.Tick(Holster * 0.5f, 1f);
            Assert.AreEqual(SwitchPhase.Holstering, switcher.Phase);
            Assert.AreSame(_rifle, switcher.Held);
            Assert.AreEqual(0.5f, switcher.Raise, 0.001f);

            equipment.Tick(Holster * 0.5f, 1f);
            Assert.AreSame(_pistol, switcher.Held);
            Assert.AreEqual(SwitchPhase.Equipping, switcher.Phase);

            equipment.Tick(Equip, 1f);
            Assert.IsTrue(switcher.IsReady);
        }

        [Test]
        public void SwitchSpeed_ScalesDuration()
        {
            Equipment equipment = Loaded();

            equipment.Select(1);
            equipment.Tick(Holster / 2f, 2f);
            equipment.Tick(Equip / 2f, 2f);

            Assert.AreSame(_pistol, equipment.Switcher.Held);
            Assert.IsTrue(equipment.Switcher.IsReady);
        }

        [Test]
        public void ChangingMindMidHolster_RaisesTheSameItemAgain()
        {
            Equipment equipment = Loaded();
            int swaps = 0;
            equipment.Switcher.HeldChanged += (_, _) => swaps++;

            equipment.Select(1);
            equipment.Tick(Holster * 0.5f, 1f);
            equipment.Select(0);
            equipment.Tick(Equip, 1f);

            Assert.AreEqual(0, swaps);
            Assert.AreSame(_rifle, equipment.Switcher.Held);
            Assert.IsTrue(equipment.Switcher.IsReady);
        }

        [Test]
        public void Select_IgnoresEmptyAndCurrentSlot()
        {
            Hotbar hotbar = GunsAndMelee();
            hotbar.TryPlace(0, _rifle);
            var equipment = new Equipment(hotbar);
            equipment.EquipFirstImmediately();

            Assert.IsFalse(equipment.Select(1));
            Assert.IsFalse(equipment.Select(0));
            Assert.AreEqual(0, equipment.SelectedIndex);
        }

        [Test]
        public void Cycle_WrapsBothWays()
        {
            Equipment equipment = Loaded();

            equipment.Cycle(-1);
            Assert.AreEqual(2, equipment.SelectedIndex);
            equipment.Cycle(1);
            Assert.AreEqual(0, equipment.SelectedIndex);
        }

        [Test]
        public void RemovingSelectedItem_LeavesHandsAtOnce()
        {
            Equipment equipment = Loaded();

            equipment.Hotbar.Remove(0);

            Assert.AreEqual(-1, equipment.SelectedIndex);
            Assert.IsNull(equipment.Switcher.Held);
            Assert.AreEqual(SwitchPhase.Empty, equipment.Switcher.Phase);
        }

        [Test]
        public void PickUp_FillsFirstFreeSlot_WhateverTheKind()
        {
            Equipment equipment = Empty(AnyWeapon());

            Assert.IsTrue(equipment.TryPickUp(_axe, out ItemInstance dropped));
            Assert.IsNull(dropped);
            Assert.IsTrue(equipment.TryPickUp(_rifle, out _));
            Assert.IsTrue(equipment.TryPickUp(_pistol, out _));

            Assert.AreSame(_axe, equipment.Hotbar[0]);
            Assert.AreSame(_rifle, equipment.Hotbar[1]);
            Assert.AreSame(_pistol, equipment.Hotbar[2]);
        }

        [Test]
        public void PickUp_SwitchesToThePickedUpItem()
        {
            Equipment equipment = Empty(AnyWeapon());

            equipment.TryPickUp(_pistol, out _);
            Assert.AreEqual(0, equipment.SelectedIndex);
            Assert.AreSame(_pistol, equipment.Switcher.Target);
            Settle(equipment);

            equipment.TryPickUp(_rifle, out _);
            Assert.AreEqual(1, equipment.SelectedIndex);
            Assert.AreSame(_rifle, equipment.Switcher.Target);
            Assert.AreEqual(SwitchPhase.Holstering, equipment.Switcher.Phase);
        }

        [Test]
        public void PickUp_WhenFull_SwapsWithSelected_AndRaisesTheNewOne()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_rifle, out _);
            equipment.TryPickUp(_pistol, out _);
            equipment.TryPickUp(_axe, out _);
            equipment.Select(1);
            Settle(equipment);

            Assert.AreEqual(PickUpAction.Swap, equipment.GetPickUpAction(_rifle2));
            Assert.IsTrue(equipment.TryPickUp(_rifle2, out ItemInstance dropped));

            Assert.AreSame(_pistol, dropped);
            Assert.AreSame(_rifle2, equipment.Hotbar[1]);
            Assert.AreEqual(1, equipment.SelectedIndex);
            Assert.AreSame(_rifle2, equipment.Switcher.Held);
            Assert.AreEqual(SwitchPhase.Equipping, equipment.Switcher.Phase);
            Assert.AreEqual(0f, equipment.Switcher.Raise);
        }

        [Test]
        public void PickUp_RespectsSlotCategories()
        {
            Equipment equipment = Loaded();
            equipment.Select(2);

            Assert.AreEqual(PickUpAction.None, equipment.GetPickUpAction(_rifle2));
            Assert.IsFalse(equipment.TryPickUp(_rifle2, out _));

            equipment.Select(0);
            Assert.AreEqual(PickUpAction.Swap, equipment.GetPickUpAction(_rifle2));
        }

        [Test]
        public void DropSelected_EmptiesSlotAndHands_AndFreesTheSlotForPickUp()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_rifle, out _);
            equipment.TryPickUp(_pistol, out _);
            equipment.TryPickUp(_axe, out _);
            equipment.Select(0);
            Settle(equipment);

            Assert.AreSame(_rifle, equipment.DropSelected());
            Assert.IsNull(equipment.Hotbar[0]);
            Assert.AreEqual(-1, equipment.SelectedIndex);
            Assert.AreEqual(SwitchPhase.Empty, equipment.Switcher.Phase);
            Assert.IsNull(equipment.DropSelected());

            Assert.AreEqual(PickUpAction.Store, equipment.GetPickUpAction(_rifle2));
            equipment.TryPickUp(_rifle2, out _);
            Assert.AreSame(_rifle2, equipment.Hotbar[0]);
            Assert.AreEqual(0, equipment.SelectedIndex);
        }

        [Test]
        public void PickUp_IgnoresItemAlreadyInHotbar()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_rifle, out _);

            Assert.AreEqual(PickUpAction.None, equipment.GetPickUpAction(_rifle));
        }
    }
}
