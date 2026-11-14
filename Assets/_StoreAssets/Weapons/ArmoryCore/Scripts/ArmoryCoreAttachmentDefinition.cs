using UnityEngine;

namespace ArmoryCore
{
    public enum ArmoryCoreAttachmentSlot
    {
        Sights,
        Barrel,
        Grip,
        Stock,
        Magazine,
        Generic
    }

    [CreateAssetMenu(menuName = "ArmoryCore Weapons/Attachment Definition", fileName = "Attachment")]
    public sealed class ArmoryCoreAttachmentDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public ArmoryCoreAttachmentSlot slot;
        public string[] requiredWeaponTags = System.Array.Empty<string>();

        [Header("Physical")]
        [Min(0f)] public float massKilograms = 0.2f;
        [Min(0.01f)] public float recoilMultiplier = 1f;
        [Min(0.01f)] public float spreadMultiplier = 1f;
        [Min(0.01f)] public float muzzleVelocityMultiplier = 1f;
        [Min(0.01f)] public float reloadTimeMultiplier = 1f;
        [Min(0.01f)] public float aimTimeMultiplier = 1f;
        [Min(0.01f)] public float reliabilityMultiplier = 1f;

        [Header("Signature")]
        public bool suppressesMuzzleBlast;
        [Range(0f, 2f)] public float soundPressureMultiplier = 1f;
        [Range(0f, 2f)] public float muzzleFlashMultiplier = 1f;

        [Header("Optic")]
        [Min(1f)] public float minimumMagnification = 1f;
        [Min(1f)] public float maximumMagnification = 1f;
        [Min(1f)] public float defaultZeroDistanceMeters = 100f;

        public bool IsReflexSight => slot == ArmoryCoreAttachmentSlot.Sights
            && (string.Equals(id, "1P63", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "1P87", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "golo1", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "kobra", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "KPSR2", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "OKP7", System.StringComparison.OrdinalIgnoreCase));

        public float GameplayMinimumMagnification => IsReflexSight
            ? 1f
            : Mathf.Max(1f, minimumMagnification);

        public float GameplayMaximumMagnification => IsReflexSight
            ? 1f
            : Mathf.Max(GameplayMinimumMagnification, maximumMagnification);

        private void OnValidate()
        {
            recoilMultiplier = Mathf.Max(0.01f, recoilMultiplier);
            spreadMultiplier = Mathf.Max(0.01f, spreadMultiplier);
            muzzleVelocityMultiplier = Mathf.Max(0.01f, muzzleVelocityMultiplier);
            reloadTimeMultiplier = Mathf.Max(0.01f, reloadTimeMultiplier);
            aimTimeMultiplier = Mathf.Max(0.01f, aimTimeMultiplier);
            reliabilityMultiplier = Mathf.Max(0.01f, reliabilityMultiplier);
            minimumMagnification = Mathf.Max(1f, minimumMagnification);
            maximumMagnification = Mathf.Max(minimumMagnification, maximumMagnification);
        }
    }
}
