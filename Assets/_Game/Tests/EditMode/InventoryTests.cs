using System.Collections.Generic;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MB13.Aipocalypse.Tests
{
    public class InventoryTests
    {
        const int BaseSlots = 3;
        const float BaseWeight = 25f;
        // Gear slots: 0 = backpack, 1 = belt.
        const int BackpackGear = 0;

        readonly List<Object> _created = new();
        AmmoType _rounds;
        GunDefinition _gun;
        MeleeDefinition _axe;
        LanternDefinition _lantern;
        BackpackDefinition _bigPack;
        BackpackDefinition _smallPack;
        CurrencyDefinition _tokens;

        [SetUp]
        public void SetUp()
        {
            _rounds = Track(TestItems.Create<AmmoType>(maxStack: 30, weight: 0.02f));
            _gun = Track(TestItems.Create<GunDefinition>(weight: 3f));
            _axe = Track(TestItems.Create<MeleeDefinition>(weight: 2f));
            _lantern = Track(TestItems.Create<LanternDefinition>());
            _tokens = Track(TestItems.Create<CurrencyDefinition>(unlimited: true));
            _bigPack = Track(Backpack(extraSlots: 4, extraWeight: 15f));
            _smallPack = Track(Backpack(extraSlots: 1, extraWeight: 5f));
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

        static BackpackDefinition Backpack(int extraSlots, float extraWeight)
        {
            var pack = TestItems.Create<BackpackDefinition>(weight: 1f);
            var so = new SerializedObject(pack);
            so.FindProperty("_extraSlots").intValue = extraSlots;
            so.FindProperty("_extraWeight").floatValue = extraWeight;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pack;
        }

        // Hotbar: two guns, melee, two quick slots (consumables, tools). Gear: backpack, belt.
        static Inventory Make() => new(
            new ItemContainer(new[] { ItemCategory.Gun, ItemCategory.Gun, ItemCategory.Melee, ItemCategory.Consumable | ItemCategory.Tool,
                ItemCategory.Consumable | ItemCategory.Tool }),
            new ItemContainer(new[] { ItemCategory.Backpack, ItemCategory.Lantern }),
            BaseSlots, BaseWeight);

        [Test]
        public void TryAdd_PutsGunsInFreeGunSlots_ThenInTheBackpack()
        {
            Inventory inventory = Make();
            ItemInstance first = _gun.CreateInstance(), second = _gun.CreateInstance(), third = _gun.CreateInstance();

            inventory.TryAdd(first, out _);
            inventory.TryAdd(second, out _);
            inventory.TryAdd(third, out _);

            Assert.AreSame(first, inventory.Hotbar[0]);
            Assert.AreSame(second, inventory.Hotbar[1]);
            Assert.AreSame(third, inventory.Backpack[0], "only two guns are active");
        }

        [Test]
        public void TryAdd_PutsMeleeInTheMeleeSlot_AndAmmoInTheBackpack()
        {
            Inventory inventory = Make();
            ItemInstance axe = _axe.CreateInstance();

            inventory.TryAdd(axe, out _);
            inventory.TryAdd(_rounds.CreateInstance(20), out _);

            Assert.AreSame(axe, inventory.Hotbar[2]);
            Assert.AreEqual(20, inventory.Backpack.CountOf(_rounds));
            Assert.AreEqual(0, inventory.Hotbar.CountOf(_rounds), "the quick slots don't take ammo");
        }

        [Test]
        public void TryAdd_WearsABackpackAndALantern_WhenTheirGearSlotIsFree()
        {
            Inventory inventory = Make();
            ItemInstance pack = _bigPack.CreateInstance();
            ItemInstance lantern = _lantern.CreateInstance();

            inventory.TryAdd(pack, out _);
            inventory.TryAdd(lantern, out _);

            Assert.AreSame(pack, inventory.Gear[BackpackGear]);
            Assert.AreSame(lantern, inventory.FindGear(ItemCategory.Lantern));
            Assert.AreSame(_bigPack, inventory.WornBackpack);
        }

        [Test]
        public void WearingABackpack_AddsSlotsAndCarryWeight()
        {
            Inventory inventory = Make();
            Assert.AreEqual(BaseSlots, inventory.Backpack.Count);
            Assert.AreEqual(BaseWeight, inventory.MaxWeight);

            inventory.TryAdd(_bigPack.CreateInstance(), out _);

            Assert.AreEqual(BaseSlots + 4, inventory.Backpack.Count);
            Assert.AreEqual(BaseWeight + 15f, inventory.MaxWeight);
        }

        [Test]
        public void TakingOffTheBackpack_IsRefused_WhileItsExtraSlotsHoldItems()
        {
            Inventory inventory = Make();
            inventory.TryAdd(_bigPack.CreateInstance(), out _);
            inventory.Backpack.TryPlace(BaseSlots + 1, _rounds.CreateInstance(5));

            Assert.IsFalse(inventory.TryMove(inventory.Gear, BackpackGear, inventory.Backpack, 0, out string refusal));
            Assert.AreEqual(Inventory.BackpackNotEmpty.Text(), refusal);
            Assert.AreSame(_bigPack, inventory.WornBackpack);

            inventory.Backpack.Remove(BaseSlots + 1);
            Assert.IsTrue(inventory.TryMove(inventory.Gear, BackpackGear, inventory.Backpack, 0, out refusal));
            Assert.IsNull(inventory.WornBackpack);
            Assert.AreEqual(BaseSlots, inventory.Backpack.Count);
            Assert.AreSame(_bigPack, inventory.Backpack[0].Definition);
        }

        [Test]
        public void TheBackpack_CantGoIntoASlotItAdds()
        {
            Inventory inventory = Make();
            inventory.TryAdd(_bigPack.CreateInstance(), out _);

            Assert.IsFalse(inventory.TryMove(inventory.Gear, BackpackGear, inventory.Backpack, BaseSlots + 2, out string refusal));
            Assert.AreEqual(Inventory.BackpackNotEmpty.Text(), refusal);
            Assert.AreSame(_bigPack, inventory.WornBackpack);
        }

        [Test]
        public void SwappingForASmallerBackpack_NeedsTheSlotsItLosesEmpty()
        {
            Inventory inventory = Make();
            inventory.TryAdd(_bigPack.CreateInstance(), out _);
            inventory.Backpack.TryPlace(0, _smallPack.CreateInstance());
            inventory.Backpack.TryPlace(BaseSlots + 3, _rounds.CreateInstance(5));

            Assert.IsFalse(inventory.TryMove(inventory.Backpack, 0, inventory.Gear, BackpackGear, out _));

            inventory.Backpack.Remove(BaseSlots + 3);
            Assert.IsTrue(inventory.TryMove(inventory.Backpack, 0, inventory.Gear, BackpackGear, out _));
            Assert.AreSame(_smallPack, inventory.WornBackpack);
            Assert.AreSame(_bigPack, inventory.Backpack[0].Definition, "the old one took its place");
            Assert.AreEqual(BaseSlots + 1, inventory.Backpack.Count);
        }

        [Test]
        public void CountAndTake_SpanTheHotbarAndTheBackpack()
        {
            Inventory inventory = Make();
            inventory.AddNew(_rounds, 50);

            Assert.AreEqual(50, inventory.CountOf(_rounds));
            Assert.AreEqual(45, inventory.Take(_rounds, 45));
            Assert.AreEqual(5, inventory.CountOf(_rounds));
        }

        [Test]
        public void AddNew_StopsWhenFull_AndSaysHowManyFit()
        {
            Inventory inventory = Make();

            Assert.AreEqual(BaseSlots * 30, inventory.AddNew(_rounds, 1000));
            Assert.AreEqual(0, inventory.AddNew(_rounds, 1));
        }

        [Test]
        public void Weight_CountsEverythingCarried()
        {
            Inventory inventory = Make();
            inventory.TryAdd(_gun.CreateInstance(), out _);
            inventory.TryAdd(_bigPack.CreateInstance(), out _);
            inventory.AddNew(_rounds, 10);
            inventory.AddNew(_tokens, 9999);

            Assert.AreEqual(3f + 1f + 10 * 0.02f, inventory.Weight, 0.0001f, "tokens weigh nothing");
        }

        [Test]
        public void Changed_FiresForAnyCarriedChange()
        {
            Inventory inventory = Make();
            int changed = 0;
            inventory.Changed += () => changed++;

            inventory.AddNew(_rounds, 5);
            int afterAdd = changed;
            inventory.Take(_rounds, 1);

            Assert.Greater(afterAdd, 0);
            Assert.Greater(changed, afterAdd);
        }

        [Test]
        public void AmmoReserve_ReadsAndSpendsTheInventoryStacks()
        {
            Inventory inventory = Make();
            var reserve = new AmmoReserve(inventory);
            int changed = 0;
            reserve.Changed += _ => changed++;

            Assert.AreEqual(40, reserve.Add(_rounds, 40));
            Assert.AreEqual(40, reserve.Count(_rounds));
            Assert.AreEqual(2, inventory.Backpack.UsedSlots, "30 + 10");
            Assert.AreEqual(25, reserve.Take(_rounds, 25));
            Assert.AreEqual(15, inventory.CountOf(_rounds));
            Assert.Greater(changed, 0);

            reserve.Infinite = true;
            Assert.AreEqual(100, reserve.Take(_rounds, 100));
            Assert.AreEqual(15, reserve.Count(_rounds), "infinite never runs it down");
        }

        [Test]
        public void InventoryScreen_SetsUpQuickSlotsOnTheHudHotbar()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/UI/Prefabs/HUD.prefab");
            var screen = hud.GetComponentInChildren<InventoryScreen>(true);
            var hotbar = hud.GetComponentInChildren<HotbarHud>(true);
            var screenFields = new SerializedObject(screen);
            var hotbarFields = new SerializedObject(hotbar);

            Assert.AreSame(hotbar, screenFields.FindProperty("_hotbar").objectReferenceValue, "the screen drags in and out of the HUD row");
            Assert.IsNull(screen.transform.Find("Quick"), "no second row of quick slots");
            var raycaster = (Behaviour)hotbarFields.FindProperty("_raycaster").objectReferenceValue;
            Assert.IsNotNull(raycaster);
            Assert.IsFalse(raycaster.enabled, "the HUD takes no clicks while the screen is closed");
            int screenOrder = screen.GetComponent<Canvas>().sortingOrder;
            foreach (string follower in new[] { "DragIcon", "Tooltip", "ItemMenu" })
            {
                Canvas canvas = screen.transform.Find(follower).GetComponent<Canvas>();
                Assert.IsTrue(canvas != null && canvas.overrideSorting && canvas.sortingOrder > screenOrder + 1,
                    follower + " is drawn above the raised hotbar row");
            }
        }

        [Test]
        public void InventoryScreen_HasTheItemMenu_AndSlotsShowALock()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/UI/Prefabs/HUD.prefab");
            var screen = hud.GetComponentInChildren<InventoryScreen>(true);
            var menu = (ItemContextMenu)new SerializedObject(screen).FindProperty("_menu").objectReferenceValue;
            Assert.IsNotNull(menu, "right-click opens the item menu");
            Assert.IsFalse(menu.gameObject.activeSelf, "closed until a right-click");
            var menuFields = new SerializedObject(menu);
            foreach (string field in new[] { "_panel", "_pickup", "_use", "_drop", "_lock", "_pickupLabel", "_useLabel", "_dropLabel", "_lockLabel" })
                Assert.IsNotNull(menuFields.FindProperty(field).objectReferenceValue, field);
            var panel = (RectTransform)menuFields.FindProperty("_panel").objectReferenceValue;
            Assert.IsNull(panel.GetComponent<UnityEngine.UI.Image>(),
                "an Image on the panel sizes it to its sprite (204 px): its background is a child the column ignores");
            var tooltip = screen.GetComponentInChildren<ItemTooltipView>(true);
            Assert.IsNotNull(new SerializedObject(tooltip).FindProperty("_hint").objectReferenceValue, "the tooltip shows the controls");
            Assert.IsNotNull(menu.GetComponent("GraphicRaycaster"), "its own canvas needs a raycaster to take clicks");
            Assert.IsTrue(screen.GetComponent<CanvasGroup>().ignoreParentGroups,
                "the HUD root's group isn't interactable: inherited, no button in the screen takes a click");

            foreach (string path in new[] { "Widgets/ItemSlot", "Widgets/HotbarSlot" })
            {
                var slot = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/_Core/UI/Prefabs/{path}.prefab");
                Component view = (Component)slot.GetComponent<ItemSlotView>() ?? slot.GetComponent<HotbarSlotView>();
                Assert.IsNotNull(new SerializedObject(view).FindProperty("_lock").objectReferenceValue, path + " shows a lock");
            }
        }

        // The inventory screen reads these keys while the UI map is on, when the Debug map is on too (editor, cheat builds):
        // a shared key would also fire a cheat (L revives, and so heals, the player).
        [Test]
        public void InventoryKeys_AreNotDebugKeys()
        {
            var actions = new GameInputActions();
            try
            {
                var inventoryKeys = new List<InputAction> { actions.UI.Lock, actions.Player.Drop, actions.Player.Melee };
                for (int i = 1; i <= 9; i++)
                    inventoryKeys.Add(actions.Player.Get().FindAction("Slot" + i, throwIfNotFound: true));
                foreach (InputAction key in inventoryKeys)
                    foreach (InputBinding binding in key.bindings)
                        foreach (InputBinding debug in actions.Debug.Get().bindings)
                            Assert.AreNotEqual(debug.path, binding.path, $"{key.name} shares its key with the Debug map's {debug.action}");
            }
            finally
            {
                Object.DestroyImmediate(actions.asset);
            }
        }

        [Test]
        public void RarityTextColor_IsOpaque_EvenForARarityWithNoBorder()
        {
            var style = Track(ScriptableObject.CreateInstance<InventoryStyle>());
            style.SetRarityColor(ItemRarity.Common, new Color(0.6f, 0.6f, 0.6f, 0f));

            Assert.AreEqual(new Color(0.6f, 0.6f, 0.6f, 1f), style.RarityTextColor(ItemRarity.Common));
        }

        [Test]
        public void LockedItem_StaysLockedThroughASplitAndASave()
        {
            ItemInstance rounds = _rounds.CreateInstance(20);
            rounds.Locked = true;

            Assert.IsTrue(rounds.Split(5).Locked, "half of a locked stack is locked too");
            ItemCatalog catalog = Track(ScriptableObject.CreateInstance<ItemCatalog>());
            catalog.GetType().GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(catalog, new Dictionary<string, ItemDefinition> { ["rounds"] = _rounds });
            Assert.IsTrue(ItemSaves.Read(ItemSaves.Write(rounds, catalog), catalog).Locked);
            Assert.IsFalse(ItemSaves.Read(ItemSaves.Write(_rounds.CreateInstance(3), catalog), catalog).Locked);
        }
    }
}
