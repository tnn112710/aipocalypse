using MB13.Aipocalypse.Core.LocalizationSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // What pressing an item's hotbar key does.
    public enum SelectBehaviour
    {
        // Nothing: it's carried, not used from the hotbar.
        None,
        // Holds it in the hands (guns, melee, tools, consumables).
        Hold,
        // Uses it at once and keeps whatever is in the hands (Equipment.QuickUseRequested).
        QuickUse,
    }

    // Static data shared by every copy of an item. Runtime state lives in ItemInstance.
    public abstract class ItemDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Items table (Data/Localization/Csv/Items.csv)")] LocalizedString _displayName;
        [SerializeField, PreviewField(64)] Sprite _icon;

        [SerializeField, Tooltip("The item's one prefab: lies in the world (picked up from it, spawned when dropped) and, " +
            "for equippables, is also the first-person model with its HeldItem on the same root")]
        WorldItem _prefab;

        [SerializeField, Min(1), Tooltip("How many fit in one slot. Above 1, picking up another adds to the stack")]
        int _maxStack = 1;

        [SerializeField, Tooltip("No stack limit: any amount fits in one slot (Tokens)")]
        bool _unlimitedStack;

        [SerializeField, Min(0f), Tooltip("Kilograms per unit (one round, one token, one gun)")]
        float _weight;

        [SerializeField, Tooltip("Flies to the player within their pickup radius instead of waiting for the Interact key (Tokens)")]
        bool _autoCollect;

        [SerializeField, Min(0), Tooltip("Tokens one unit is worth (one round, one gun); a used-up item is worth less (ItemInstance.Value)")]
        int _value;

        [SerializeField, Tooltip("Its slot border and tooltip colour")]
        ItemRarity _rarity;

        public string DisplayName => _displayName.Text();
        public Sprite Icon => _icon;
        public WorldItem Prefab => _prefab;
        public int MaxStack => _unlimitedStack ? int.MaxValue : _maxStack;
        public bool Stacks => MaxStack > 1;
        public float Weight => _weight;
        public bool AutoCollect => _autoCollect;
        public int Value => _value;
        public ItemRarity Rarity => _rarity;

        public virtual SelectBehaviour Selecting => SelectBehaviour.None;

        public abstract ItemCategory Category { get; }

        public virtual ItemInstance CreateInstance() => new(this);

        // A stack of `count` (clamped to 1..MaxStack).
        public ItemInstance CreateInstance(int count)
        {
            ItemInstance item = CreateInstance();
            item.SetCount(Mathf.Clamp(count, 1, MaxStack));
            return item;
        }
    }
}
