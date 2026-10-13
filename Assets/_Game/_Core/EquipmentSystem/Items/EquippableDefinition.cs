using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // An item you hold in your hands when its slot is selected (guns, melee, later tools).
    public abstract class EquippableDefinition : ItemDefinition
    {
        [field: SerializeField, Tooltip("First-person model; its root needs a HeldItem component")]
        public HeldItem HeldPrefab { get; private set; }

        [field: SerializeField, Min(0f), Tooltip("Seconds to raise it, before EquipmentConfig.SwitchSpeed")]
        public float EquipTime { get; private set; } = 0.35f;

        [field: SerializeField, Min(0f), Tooltip("Seconds to put it away, before EquipmentConfig.SwitchSpeed")]
        public float HolsterTime { get; private set; } = 0.25f;
    }
}
