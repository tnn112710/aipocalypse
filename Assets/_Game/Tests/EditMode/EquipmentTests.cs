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

        ItemInstance _rifle, _pistol, _axe;

        [SetUp]
        public void SetUp()
        {
            _rifle = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _pistol = ScriptableObject.CreateInstance<GunDefinition>().CreateInstance();
            _axe = ScriptableObject.CreateInstance<MeleeDefinition>().CreateInstance();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rifle.Definition);
            Object.DestroyImmediate(_pistol.Definition);
            Object.DestroyImmediate(_axe.Definition);
        }

        static Hotbar GunsAndMelee() => new(new[] { ItemCategory.Gun, ItemCategory.Gun, ItemCategory.Melee });

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
        public void RemovingSelectedItem_HolstersToEmptyHands()
        {
            Equipment equipment = Loaded();

            equipment.Hotbar.Remove(0);
            equipment.Tick(Holster, 1f);

            Assert.AreEqual(-1, equipment.SelectedIndex);
            Assert.IsNull(equipment.Switcher.Held);
            Assert.AreEqual(SwitchPhase.Empty, equipment.Switcher.Phase);
        }
    }
}
