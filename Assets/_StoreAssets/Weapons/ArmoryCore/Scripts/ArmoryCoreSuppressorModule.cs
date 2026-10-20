using UnityEngine;

namespace ArmoryCore
{
    /// <summary>Runtime behaviour and optional per-prefab tuning for suppressors.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreSuppressorModule : ArmoryCoreAttachmentModule
    {
        [Header("Suppressor overrides")]
        [SerializeField, Range(0.01f, 2f)] private float additionalSoundMultiplier = 1f;
        [SerializeField, Range(0.01f, 2f)] private float additionalRecoilMultiplier = 0.9f;
        [SerializeField, Range(0f, 2f)] private float additionalFlashMultiplier = 1f;
        [SerializeField] private AudioClip suppressedShotOverride;

        public override float VerticalRecoilMultiplier => additionalRecoilMultiplier;
        public override float HorizontalRecoilMultiplier => additionalRecoilMultiplier;
        public override float SoundPressureMultiplier => additionalSoundMultiplier;
        public override float MuzzleFlashMultiplier => additionalFlashMultiplier;
        public override bool ForcesSuppressedShot => true;
        public override AudioClip ShotSoundOverride => suppressedShotOverride;

        private void OnValidate()
        {
            additionalSoundMultiplier = Mathf.Max(0.01f, additionalSoundMultiplier);
            additionalRecoilMultiplier = Mathf.Max(0.01f, additionalRecoilMultiplier);
            additionalFlashMultiplier = Mathf.Max(0f, additionalFlashMultiplier);
        }
    }
}
