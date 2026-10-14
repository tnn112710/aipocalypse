using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Static data shared by every copy of an item. Runtime state lives in ItemInstance.
    public abstract class ItemDefinition : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField, PreviewField(64)] public Sprite Icon { get; private set; }

        [field: SerializeField, Tooltip("Lying in the world: picked up from it, spawned when dropped")]
        public WorldItem WorldPrefab { get; private set; }

        public abstract ItemCategory Category { get; }

        public virtual ItemInstance CreateInstance() => new(this);
    }
}
