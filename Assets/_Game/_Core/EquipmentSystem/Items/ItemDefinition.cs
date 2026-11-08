using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // What pressing an item's hotbar key does.
    public enum SelectBehaviour
    {
        // Nothing: it's carried, not used from the hotbar.
        None,
        // Holds it in the hands (guns, melee, tools).
        Hold,
        // Uses it at once and keeps whatever is in the hands (Equipment.QuickUseRequested: consumables).
        QuickUse,
    }

    // Static data shared by every copy of an item. Runtime state lives in ItemInstance.
    public abstract class ItemDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField, PreviewField(64)] Sprite _icon;

        [SerializeField, Tooltip("The item's one prefab: lies in the world (picked up from it, spawned when dropped) and, " +
            "for equippables, is also the first-person model with its HeldItem on the same root")]
        WorldItem _prefab;

        [SerializeField, Min(1), Tooltip("How many fit in one slot. Above 1, picking up another adds to the stack")]
        int _maxStack = 1;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public WorldItem Prefab => _prefab;
        public int MaxStack => _maxStack;

        public virtual SelectBehaviour Selecting => SelectBehaviour.None;

        public abstract ItemCategory Category { get; }

        public virtual ItemInstance CreateInstance() => new(this);
    }
}
