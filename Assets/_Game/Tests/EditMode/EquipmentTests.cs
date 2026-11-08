using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using NUnit.Framework;
using UnityEngine;

namespace MB13.Aipocalypse.Tests
{
    public class EquipmentTests
    {
        // Default definition times: equip 0.35 s, holster 0.25 s.
        const float Equip = 0.35f;
        const float Holster = 0.25f;

        const ItemCategory Weapon = ItemCategory.Gun | ItemCategory.Melee;

        ItemInstance _rifle, _pistol, _axe, _rifle2, _lantern, _lantern2;
        readonly System.Collections.Generic.List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _pistol = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _axe = ScriptableObject.CreateInstance<MeleeDefinition>().CreateInstance();
            _rifle2 = _rifle.Definition.CreateInstance();
            _lantern = ScriptableObject.CreateInstance<LanternDefinition>().CreateInstance();
            _lantern2 = _lantern.Definition.CreateInstance();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rifle.Definition);
            Object.DestroyImmediate(_pistol.Definition);
            Object.DestroyImmediate(_axe.Definition);
            Object.DestroyImmediate(_lantern.Definition);
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        ItemInstance Bandage(int maxStack = 3)
        {
            var definition = ScriptableObject.CreateInstance<TestConsumableDefinition>();
            _created.Add(definition);
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("_maxStack").intValue = maxStack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return definition.CreateInstance();
        }

        Equipment HoldingRifle()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_rifle, out _);
            Settle(equipment);
            return equipment;
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
        public void LowerHands_HolstersButKeepsTheSlot_RaiseHandsBringsItBack()
        {
            Equipment equipment = Loaded();

            equipment.LowerHands();
            Settle(equipment);
            Assert.AreEqual(SwitchPhase.Empty, equipment.Switcher.Phase);
            Assert.AreEqual(0, equipment.SelectedIndex);

            equipment.RaiseHands();
            Settle(equipment);
            Settle(equipment);
            Assert.AreSame(_rifle, equipment.Switcher.Held);
            Assert.IsTrue(equipment.Switcher.IsReady);
        }

        [Test]
        public void LowerHands_SelectingWaitsForRaiseHands()
        {
            Equipment equipment = Loaded();
            equipment.LowerHands();

            equipment.Select(1);
            Settle(equipment);
            Assert.IsNull(equipment.Switcher.Held, "hands stay down");
            Assert.AreEqual(1, equipment.SelectedIndex);

            equipment.RaiseHands();
            Settle(equipment);
            Settle(equipment);
            Assert.AreSame(_pistol, equipment.Switcher.Held);
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
        public void DropHeld_EmptiesSlotAndHands_AndFreesTheSlotForPickUp()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_rifle, out _);
            equipment.TryPickUp(_pistol, out _);
            equipment.TryPickUp(_axe, out _);
            equipment.Select(0);
            Settle(equipment);

            Assert.AreSame(_rifle, equipment.DropHeld());
            Assert.IsNull(equipment.Hotbar[0]);
            Assert.AreEqual(-1, equipment.SelectedIndex);
            Assert.AreEqual(SwitchPhase.Empty, equipment.Switcher.Phase);
            Assert.IsNull(equipment.DropHeld());

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

        [Test]
        public void Lantern_GoesOnTheBelt_EvenWithAFullHotbar_AndLeavesTheHandsAlone()
        {
            Equipment equipment = Loaded();

            Assert.AreEqual(PickUpAction.Store, equipment.GetPickUpAction(_lantern));
            Assert.IsTrue(equipment.TryPickUp(_lantern, out ItemInstance dropped));

            Assert.IsNull(dropped);
            Assert.AreSame(_lantern, equipment.Belt);
            Assert.AreEqual(-1, equipment.Hotbar.IndexOf(_lantern));
            Assert.AreEqual(0, equipment.SelectedIndex);
            Assert.AreSame(_rifle, equipment.Switcher.Held);
            Assert.AreEqual(PickUpAction.None, equipment.GetPickUpAction(_lantern));
        }

        [Test]
        public void SecondLantern_SwapsWithTheWornOne()
        {
            Equipment equipment = Empty(AnyWeapon());
            equipment.TryPickUp(_lantern, out _);

            Assert.AreEqual(PickUpAction.Swap, equipment.GetPickUpAction(_lantern2));
            Assert.IsTrue(equipment.TryPickUp(_lantern2, out ItemInstance dropped));

            Assert.AreSame(_lantern, dropped);
            Assert.AreSame(_lantern2, equipment.Belt);
        }

        [Test]
        public void DropHeld_MidSwitch_DropsTheItemBeingPutAway()
        {
            Equipment equipment = Loaded();
            equipment.Select(2);
            Assert.AreEqual(SwitchPhase.Holstering, equipment.Switcher.Phase);

            Assert.AreSame(_rifle, equipment.DropHeld());

            Assert.IsNull(equipment.Hotbar[0]);
            Assert.AreSame(_axe, equipment.Hotbar[2], "the newly selected item stays");
            Assert.AreEqual(2, equipment.SelectedIndex);
            Settle(equipment);
            Assert.AreSame(_axe, equipment.Switcher.Held);
        }

        [Test]
        public void PickUp_QuickUseItem_IsStored_NotUsedOrSelected()
        {
            Equipment equipment = HoldingRifle();
            int used = 0;
            equipment.QuickUseRequested += _ => used++;
            ItemInstance bandage = Bandage();

            Assert.IsTrue(equipment.TryPickUp(bandage, out _));

            Assert.AreSame(bandage, equipment.Hotbar[1]);
            Assert.AreEqual(0, used);
            Assert.AreEqual(0, equipment.SelectedIndex);
            Assert.AreSame(_rifle, equipment.Switcher.Target);
        }

        [Test]
        public void Select_QuickUseItem_AsksForAUse_AndKeepsTheHands()
        {
            Equipment equipment = HoldingRifle();
            ItemInstance bandage = Bandage();
            equipment.TryPickUp(bandage, out _);
            ItemInstance used = null;
            equipment.QuickUseRequested += item => used = item;

            Assert.IsTrue(equipment.Select(1));

            Assert.AreSame(bandage, used);
            Assert.AreEqual(0, equipment.SelectedIndex);
        }

        [Test]
        public void PickUp_SameConsumable_MergesOntoTheCarriedStack()
        {
            Equipment equipment = HoldingRifle();
            ItemInstance bandage = Bandage();
            ItemInstance second = bandage.Definition.CreateInstance();
            equipment.TryPickUp(bandage, out _);

            Assert.AreEqual(PickUpAction.Merge, equipment.GetPickUpAction(second));
            Assert.IsTrue(equipment.TryPickUp(second, out _));

            Assert.AreEqual(2, bandage.Count);
            Assert.IsTrue(second.IsEmpty);
            Assert.IsNull(equipment.Hotbar[2]);
        }

        [Test]
        public void Consume_TheLastOfAStack_LeavesTheHotbar()
        {
            Equipment equipment = HoldingRifle();
            ItemInstance bandage = Bandage();
            equipment.TryPickUp(bandage, out _);

            Assert.IsTrue(equipment.Consume(bandage));

            Assert.Less(equipment.Hotbar.IndexOf(bandage), 0);
            Assert.IsFalse(equipment.Consume(bandage), "nothing left to use");
        }
    }
}
