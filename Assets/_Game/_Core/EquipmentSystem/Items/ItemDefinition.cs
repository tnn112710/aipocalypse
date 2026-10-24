using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
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
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(64)] public Sprite Icon { get; private set; }

        [field: SerializeField, Tooltip("The item's one prefab: lies in the world (picked up from it, spawned when dropped) and, " +
            "for equippables, is also the first-person model with its HeldItem on the same root")]
        public WorldItem Prefab { get; private set; }

        [field: SerializeField, Min(1), Tooltip("How many fit in one slot. Above 1, picking up another adds to the stack")]
        public int MaxStack { get; private set; } = 1;

        public virtual SelectBehaviour Selecting => SelectBehaviour.None;

        public abstract ItemCategory Category { get; }

        public virtual ItemInstance CreateInstance() => new(this);
    }
}
