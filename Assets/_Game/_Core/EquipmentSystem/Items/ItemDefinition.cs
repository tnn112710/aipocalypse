using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Static data shared by every copy of an item. Runtime state lives in ItemInstance.
    public abstract class ItemDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(64)] public Sprite Icon { get; private set; }

        [field: SerializeField, Tooltip("The item's one prefab: lies in the world (picked up from it, spawned when dropped) and, " +
            "for equippables, is also the first-person model with its HeldItem on the same root")]
        public WorldItem Prefab { get; private set; }

        public abstract ItemCategory Category { get; }

        public virtual ItemInstance CreateInstance() => new(this);
    }
}
