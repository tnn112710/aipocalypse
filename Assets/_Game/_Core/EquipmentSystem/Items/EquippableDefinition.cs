using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // An item you hold in your hands when its slot is selected (guns, melee, later tools).
    public abstract class EquippableDefinition : ItemDefinition
    {
        // The first-person model: the HeldItem on the root of Prefab.
        public HeldItem HeldPrefab => Prefab != null ? Prefab.GetComponent<HeldItem>() : null;

        public override SelectBehaviour Selecting => SelectBehaviour.Hold;

        [field: SerializeField, Min(0f), Tooltip("Seconds to raise it, before EquipmentConfig.SwitchSpeed")]
        public float EquipTime { get; private set; } = 0.35f;

        [field: SerializeField, Min(0f), Tooltip("Seconds to put it away, before EquipmentConfig.SwitchSpeed")]
        public float HolsterTime { get; private set; } = 0.25f;

        [field: SerializeField, FoldoutGroup("Hold feel"), HideLabel, InlineProperty]
        public HoldFeel HoldFeel { get; private set; } = new();
    }
}
