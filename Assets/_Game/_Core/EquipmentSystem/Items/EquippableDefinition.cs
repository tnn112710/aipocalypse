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

        [SerializeField, Min(0f), Tooltip("Seconds to raise it, before EquipmentConfig.SwitchSpeed")]
        float _equipTime = 0.35f;

        [SerializeField, Min(0f), Tooltip("Seconds to put it away, before EquipmentConfig.SwitchSpeed")]
        float _holsterTime = 0.25f;

        [SerializeField, FoldoutGroup("Hold feel"), HideLabel, InlineProperty]
        HoldFeel _holdFeel = new();

        public float EquipTime => _equipTime;
        public float HolsterTime => _holsterTime;
        public HoldFeel HoldFeel => _holdFeel;
    }
}
