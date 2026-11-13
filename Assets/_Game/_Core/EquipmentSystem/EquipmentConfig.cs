using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // What the player can carry: the hotbar's active slots and their keys, the gear slots, backpack size and carry weight.
    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Aipocalypse/Equipment/Equipment Config")]
    public sealed class EquipmentConfig : ScriptableObject
    {
        [Serializable]
        public struct SlotConfig
        {
            public string Name;
            [Tooltip("Items table: the caption under its inventory tile; empty = none")] public LocalizedString Caption;
            [EnumToggleButtons] public ItemCategory Accepts;
            [Tooltip("The key that selects it")] public HotbarKey Key;
            [Tooltip("Placed at spawn")] public ItemDefinition StartingItem;
        }

        [Serializable]
        public struct GearSlotConfig
        {
            public string Name;
            [Tooltip("Items table: the caption under its inventory tile")] public LocalizedString Caption;
            [EnumToggleButtons] public ItemCategory Accepts;
            [Tooltip("Worn at spawn")] public ItemDefinition StartingItem;
        }

        [SerializeField, Range(0.1f, 5f), Tooltip("Multiplies every item's equip and holster speed. 2 = switching takes half as long")]
        float _switchSpeed = 1f;

        [SerializeField, Tooltip("Active slots in display order (guns, melee, quick slots), each with its key. Each key once")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = nameof(SlotConfig.Name))]
        List<SlotConfig> _slots = new();

        [SerializeField, Tooltip("Worn items: head, body, face, backpack, belt (lantern)")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = nameof(GearSlotConfig.Name))]
        List<GearSlotConfig> _gear = new();

        [SerializeField, Min(0), Tooltip("Backpack slots without a backpack worn")]
        int _backpackSlots = 10;

        [SerializeField, Min(0f), Tooltip("Kilograms the player carries without a backpack worn (shown only: no slowdown yet)")]
        float _maxWeight = 25f;

        [SerializeField, Tooltip("Put in the backpack at spawn")]
        List<ItemStack> _startingItems = new();

        public float SwitchSpeed => _switchSpeed;
        public IReadOnlyList<SlotConfig> Slots => _slots;
        public IReadOnlyList<GearSlotConfig> Gear => _gear;

        public Equipment CreateEquipment()
        {
            var keys = new HotbarKey[_slots.Count];
            var names = new LocalizedString[_slots.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = _slots[i].Key;
                names[i] = _slots[i].Caption;
            }
            var gearNames = new LocalizedString[_gear.Count];
            for (int i = 0; i < gearNames.Length; i++)
                gearNames[i] = _gear[i].Caption;
            return new Equipment(CreateInventory(), keys, names, gearNames);
        }

        public Inventory CreateInventory()
        {
            var hotbarAccepts = new ItemCategory[_slots.Count];
            for (int i = 0; i < hotbarAccepts.Length; i++)
                hotbarAccepts[i] = _slots[i].Accepts;
            var gearAccepts = new ItemCategory[_gear.Count];
            for (int i = 0; i < gearAccepts.Length; i++)
                gearAccepts[i] = _gear[i].Accepts;

            var inventory = new Inventory(new ItemContainer(hotbarAccepts), new ItemContainer(gearAccepts), _backpackSlots, _maxWeight);
            for (int i = 0; i < _gear.Count; i++)
                Place(inventory.Gear, i, _gear[i].StartingItem);
            for (int i = 0; i < _slots.Count; i++)
                Place(inventory.Hotbar, i, _slots[i].StartingItem);
            foreach (ItemStack stack in _startingItems)
                if (stack.Item != null && inventory.AddNew(stack.Item, stack.Count) < stack.Count)
                    Debug.LogWarning($"{name}: no room for all {stack.Count} {stack.Item.name} at spawn.", this);
            return inventory;
        }

        void Place(ItemContainer container, int index, ItemDefinition start)
        {
            if (start != null && !container.TryPlace(index, start.CreateInstance()))
                Debug.LogWarning($"{name}: slot {index + 1} doesn't accept {start.name} ({start.Category}).", this);
        }

        void OnValidate()
        {
            for (int i = 0; i < _slots.Count; i++)
                for (int j = 0; j < i; j++)
                    if (_slots[i].Key == _slots[j].Key)
                        Debug.LogWarning($"{name}: slots {j + 1} and {i + 1} share the key {_slots[i].Key}; only the first one is selected by it.", this);
        }
    }
}
