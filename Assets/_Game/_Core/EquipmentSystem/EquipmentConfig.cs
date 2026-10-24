using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Aipocalypse/Equipment/Equipment Config")]
    public sealed class EquipmentConfig : ScriptableObject
    {
        [Serializable]
        public struct SlotConfig
        {
            public string Name;
            [EnumToggleButtons] public ItemCategory Accepts;
            [Tooltip("Placed at spawn until an inventory fills the hotbar")]
            public ItemDefinition StartingItem;
        }

        [SerializeField, Range(0.1f, 5f), Tooltip("Multiplies every item's equip and holster speed. 2 = switching takes half as long")]
        float _switchSpeed = 1f;

        [SerializeField, EnumToggleButtons, Tooltip("Worn on the belt instead of going into the hotbar: one at a time, works " +
            "whatever is in the hands")]
        ItemCategory _beltAccepts = ItemCategory.Lantern;

        public float SwitchSpeed => _switchSpeed;
        public ItemCategory BeltAccepts => _beltAccepts;

        public Equipment CreateEquipment() => new(CreateHotbar(), BeltAccepts);

        [SerializeField, Tooltip("Hotbar slots in key order: slot 1 is key 1. At most 9")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = nameof(SlotConfig.Name))]
        List<SlotConfig> _slots = new();

        public Hotbar CreateHotbar()
        {
            var accepts = new ItemCategory[_slots.Count];
            for (int i = 0; i < accepts.Length; i++)
                accepts[i] = _slots[i].Accepts;

            var hotbar = new Hotbar(accepts);
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemDefinition start = _slots[i].StartingItem;
                if (start != null && !hotbar.TryPlace(i, start.CreateInstance()))
                    Debug.LogWarning($"{name}: slot {i + 1} doesn't accept {start.name} ({start.Category}).", this);
            }
            return hotbar;
        }

        void OnValidate()
        {
            if (_slots.Count > Hotbar.MaxSlots)
                _slots.RemoveRange(Hotbar.MaxSlots, _slots.Count - Hotbar.MaxSlots);
        }
    }
}
