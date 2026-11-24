using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cue = MB13.Aipocalypse.Core.UI.InventoryActions.Cue;
using Outcome = MB13.Aipocalypse.Core.UI.InventoryActions.Outcome;

namespace MB13.Aipocalypse.Tests
{
    // The inventory screen's item rules: what each action moves, and the sound and refusal it hands the screen.
    public class InventoryActionsTests
    {
        const int BaseSlots = 3;
        // Gear slots: 0 = backpack, 1 = belt.
        const int BackpackGear = 0;

        readonly List<Object> _created = new();
        AmmoType _rounds;
        GunDefinition _gun;
        MeleeDefinition _axe;
        BackpackDefinition _bigPack;
        BackpackDefinition _smallPack;
        Equipment _equipment;
        Inventory _inventory;
        InventoryActions _actions;

        [SetUp]
        public void SetUp()
        {
            _rounds = Track(TestItems.Create<AmmoType>(maxStack: 30));
            _gun = Track(TestItems.Create<GunDefinition>());
            _axe = Track(TestItems.Create<MeleeDefinition>());
            _bigPack = Track(Backpack(extraSlots: 4));
            _smallPack = Track(Backpack(extraSlots: 1));
            // Hotbar: two guns, melee, a quick slot. Gear: backpack, belt.
            _inventory = new Inventory(
                new ItemContainer(new[] { ItemCategory.Gun, ItemCategory.Gun, ItemCategory.Melee, ItemCategory.Consumable | ItemCategory.Tool }),
                new ItemContainer(new[] { ItemCategory.Backpack, ItemCategory.Lantern }),
                BaseSlots, 25f);
            _equipment = new Equipment(_inventory);
            _actions = new InventoryActions(_equipment, slot => (slot + 1).ToString());
        }

        [TearDown]
        public void TearDown()
        {
            _equipment.Dispose();
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }

        static BackpackDefinition Backpack(int extraSlots)
        {
            var pack = TestItems.Create<BackpackDefinition>();
            var so = new SerializedObject(pack);
            so.FindProperty("_extraSlots").intValue = extraSlots;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pack;
        }

        LootContainer Crate(params ItemInstance[] items)
        {
            var crate = Track(new GameObject("Crate")).AddComponent<LootContainer>();
            for (int i = 0; i < items.Length; i++)
                crate.Items.TryPlace(i, items[i]);
            return crate;
        }

        static void AssertPlaced(Outcome outcome)
        {
            Assert.AreEqual(Cue.Place, outcome.Sound);
            Assert.IsNull(outcome.Refusal);
        }

        [Test]
        public void SendAcross_TakesALootStackIntoTheBackpack()
        {
            LootContainer crate = Crate(_rounds.CreateInstance(20));

            Outcome outcome = _actions.SendAcross(crate.Items, 0, null, crate);

            AssertPlaced(outcome);
            Assert.AreEqual(20, _inventory.Backpack.CountOf(_rounds));
            Assert.IsNull(crate.Items[0]);
        }

        [Test]
        public void SendAcross_WithACrateOpen_SendsABackpackStackIntoTheCrate()
        {
            LootContainer crate = Crate();
            _inventory.Backpack.TryPlace(0, _rounds.CreateInstance(10));

            Outcome outcome = _actions.SendAcross(_inventory.Backpack, 0, null, crate);

            AssertPlaced(outcome);
            Assert.AreEqual(10, crate.Items.CountOf(_rounds));
            Assert.IsNull(_inventory.Backpack[0]);
        }

        [Test]
        public void SendAcross_FromTheBackpack_PutsAGunInAFreeGunSlot()
        {
            ItemInstance gun = _gun.CreateInstance();
            _inventory.Backpack.TryPlace(0, gun);

            AssertPlaced(_actions.SendAcross(_inventory.Backpack, 0, null, null));

            Assert.AreSame(gun, _inventory.Hotbar[0]);
        }

        [Test]
        public void SendAcross_FromTheBackpack_SaysNowhereToWear_WhenNoSlotTakesIt()
        {
            _inventory.Backpack.TryPlace(0, _rounds.CreateInstance(10));

            Outcome outcome = _actions.SendAcross(_inventory.Backpack, 0, null, null);

            Assert.AreEqual(Cue.None, outcome.Sound);
            Assert.AreEqual(InventoryActions.NowhereToWear.Text(), outcome.Refusal);
            Assert.AreEqual(10, _inventory.Backpack[0].Count);
        }

        [Test]
        public void SendAcross_RefusedBackpackSwap_GivesItsRefusalOnce_WithNoPlaceSound()
        {
            _inventory.TryAdd(_bigPack.CreateInstance(), out _);
            ItemInstance small = _smallPack.CreateInstance();
            _inventory.Backpack.TryPlace(0, small);
            _inventory.Backpack.TryPlace(BaseSlots + 3, _rounds.CreateInstance(5));

            Outcome outcome = _actions.SendAcross(_inventory.Backpack, 0, null, null);

            Assert.AreEqual(Cue.None, outcome.Sound, "only the refusal sound plays");
            Assert.AreEqual(Inventory.BackpackNotEmpty.Text(), outcome.Refusal, "the swap's own refusal, not \"nowhere to wear\"");
            Assert.AreSame(_bigPack, _inventory.WornBackpack);
            Assert.AreSame(small, _inventory.Backpack[0]);
        }

        [Test]
        public void SendAcross_FromAnActiveSlot_GoesToTheBackpack_OrSaysItIsFull()
        {
            ItemInstance gun = _gun.CreateInstance();
            _inventory.Hotbar.TryPlace(0, gun);

            AssertPlaced(_actions.SendAcross(_inventory.Hotbar, 0, null, null));
            Assert.AreSame(gun, _inventory.Backpack[0]);

            _inventory.Hotbar.TryPlace(1, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(1, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(2, _gun.CreateInstance());
            Outcome full = _actions.SendAcross(_inventory.Hotbar, 1, null, null);

            Assert.AreEqual(Cue.None, full.Sound);
            Assert.AreEqual(InventoryActions.BackpackFull.Text(), full.Refusal);
            Assert.IsNotNull(_inventory.Hotbar[1]);
        }

        [Test]
        public void SendAcross_APartWithNoGunInspected_AsksForAGun()
        {
            var part = Track(TestItems.Create<AttachmentDefinition>());
            _inventory.Backpack.TryPlace(0, part.CreateInstance());

            Outcome outcome = _actions.SendAcross(_inventory.Backpack, 0, null, null);

            Assert.AreEqual(InventoryActions.PickGun.Text(), outcome.Refusal);
            Assert.IsNotNull(_inventory.Backpack[0]);
        }

        [Test]
        public void Move_IntoAHotbarSlotThatDoesntTakeIt_SaysWhatTheSlotTakes()
        {
            ItemInstance axe = _axe.CreateInstance();
            _inventory.Backpack.TryPlace(0, axe);

            Outcome outcome = _actions.Move(_inventory.Backpack, 0, _inventory.Hotbar, 0);

            Assert.AreEqual(Cue.None, outcome.Sound);
            Assert.AreEqual(InventoryActions.SlotGun.Format("1"), outcome.Refusal);
            Assert.AreSame(axe, _inventory.Backpack[0]);
        }

        [Test]
        public void Move_PartOfAStack_PlaysPlace()
        {
            _inventory.Backpack.TryPlace(0, _rounds.CreateInstance(10));

            AssertPlaced(_actions.Move(_inventory.Backpack, 0, _inventory.Backpack, 1, amount: 4));

            Assert.AreEqual(6, _inventory.Backpack[0].Count);
            Assert.AreEqual(4, _inventory.Backpack[1].Count);
        }

        [Test]
        public void ToHotbar_MovesToTheKeysSlot_AndIgnoresTheSlotItIsIn()
        {
            ItemInstance gun = _gun.CreateInstance();
            _inventory.Backpack.TryPlace(0, gun);

            AssertPlaced(_actions.ToHotbar(_inventory.Backpack, 0, HotbarKey.Key2));
            Assert.AreSame(gun, _inventory.Hotbar[1]);

            Outcome again = _actions.ToHotbar(_inventory.Hotbar, 1, HotbarKey.Key2);
            Assert.AreEqual(Cue.None, again.Sound);
            Assert.IsNull(again.Refusal);
        }

        [Test]
        public void Drop_HandsTheItemOut_ButALockedOneStays()
        {
            ItemInstance gun = _gun.CreateInstance();
            _inventory.Backpack.TryPlace(0, gun);
            gun.Locked = true;

            Outcome locked = _actions.Drop(_inventory.Backpack, 0, out ItemInstance removed);

            Assert.AreEqual(InventoryActions.DropLocked.Text(), locked.Refusal);
            Assert.IsNull(removed);
            Assert.AreSame(gun, _inventory.Backpack[0]);

            gun.Locked = false;
            Outcome dropped = _actions.Drop(_inventory.Backpack, 0, out removed);

            Assert.AreEqual(Cue.None, dropped.Sound);
            Assert.IsNull(dropped.Refusal);
            Assert.AreSame(gun, removed);
            Assert.IsNull(_inventory.Backpack[0]);
        }

        [Test]
        public void ToggleLock_FlipsTheLock_AndPlaysPlace()
        {
            ItemInstance gun = _gun.CreateInstance();

            AssertPlaced(InventoryActions.ToggleLock(gun));
            Assert.IsTrue(gun.Locked);
        }

        [Test]
        public void Pickup_TakesWhatFits_PlaysPlace_AndSaysTheBackpackIsFull()
        {
            _inventory.Backpack.TryPlace(0, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(1, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(2, _rounds.CreateInstance(25));
            LootContainer crate = Crate(_rounds.CreateInstance(10));

            Outcome outcome = _actions.Pickup(crate.Items, 0);

            Assert.AreEqual(Cue.Place, outcome.Sound);
            Assert.AreEqual(InventoryActions.BackpackFull.Text(), outcome.Refusal);
            Assert.AreEqual(30, _inventory.Backpack[2].Count);
            Assert.AreEqual(5, crate.Items[0].Count);
        }

        [Test]
        public void Pickup_NothingFits_SaysNoRoom_WithoutAPlaceSound()
        {
            _inventory.Backpack.TryPlace(0, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(1, _gun.CreateInstance());
            _inventory.Backpack.TryPlace(2, _gun.CreateInstance());
            LootContainer crate = Crate(_rounds.CreateInstance(10));

            Outcome outcome = _actions.Pickup(crate.Items, 0);

            Assert.AreEqual(Cue.None, outcome.Sound);
            Assert.AreEqual(InventoryActions.NoRoom.Text(), outcome.Refusal);
            Assert.AreEqual(10, crate.Items[0].Count);
        }

        [Test]
        public void TakeAll_TakesOnlyRevealedStacks()
        {
            ItemInstance hiddenGun = _gun.CreateInstance();
            LootContainer crate = Crate(_rounds.CreateInstance(10), hiddenGun);
            crate.RestoreHidden(new[] { 1 });

            Outcome outcome = _actions.TakeAll(crate);

            AssertPlaced(outcome);
            Assert.AreEqual(10, _inventory.Backpack.CountOf(_rounds));
            Assert.AreSame(hiddenGun, crate.Items[1]);
        }

        [Test]
        public void Use_GivesTheConsumersRefusal_AndUsesNothing()
        {
            var bandage = Track(TestItems.Create<ConsumableDefinition>());
            ItemInstance item = bandage.CreateInstance();
            var consumer = new RefusingConsumer();

            Outcome outcome = InventoryActions.Use(item, consumer);

            Assert.AreEqual(RefusingConsumer.Refusal, outcome.Refusal);
            Assert.AreEqual(Cue.None, outcome.Sound);
            Assert.IsFalse(consumer.Used);
            Assert.IsFalse(item.IsEmpty);
        }

        sealed class RefusingConsumer : IConsumer
        {
            public const string Refusal = "full";

            public bool Used { get; private set; }
            public float LastHurtTime => 0f;
            public string CannotUse(ConsumableDefinition item) => Refusal;
            public void Use(ConsumableDefinition item) => Used = true;
        }
    }
}
