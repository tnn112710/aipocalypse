using System;
using UnityEngine;

namespace ArmoryCore
{
    /// <summary>
    /// Base component for every installable weapon module. The definition contains
    /// reusable data, while derived components add runtime behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArmoryCoreAttachmentModule : MonoBehaviour
    {
        [SerializeField] private ArmoryCoreAttachmentDefinition definition;
        [SerializeField] private Transform muzzlePoint;

        private ArmoryCoreWeaponController owner;

        public ArmoryCoreAttachmentDefinition Definition => definition;
        public ArmoryCoreWeaponController Owner => owner;
        public Transform MuzzlePoint => muzzlePoint;

        public virtual float AdditionalMassKilograms => 0f;
        public virtual float RoundsPerMinuteMultiplier => 1f;
        public virtual float MuzzleVelocityMultiplier => 1f;
        public virtual float SpreadMultiplier => 1f;
        public virtual float VerticalRecoilMultiplier => 1f;
        public virtual float HorizontalRecoilMultiplier => 1f;
        public virtual float ReloadTimeMultiplier => 1f;
        public virtual float AimTimeMultiplier => 1f;
        public virtual float ReliabilityMultiplier => 1f;
        public virtual float SoundPressureMultiplier => 1f;
        public virtual float MuzzleFlashMultiplier => 1f;
        public virtual bool ForcesSuppressedShot => false;
        public virtual bool OverridesOptic => false;
        public virtual float CurrentMagnification => 1f;
        public virtual float CurrentZeroDistanceMeters => 100f;
        public virtual Transform AimPoint => null;
        public virtual AudioClip ShotSoundOverride => null;

        public void Configure(ArmoryCoreAttachmentDefinition value, Transform moduleMuzzlePoint = null)
        {
            definition = value;
            muzzlePoint = moduleMuzzlePoint;
        }

        public virtual void OnInstalled(ArmoryCoreWeaponController weapon)
        {
            owner = weapon;
        }

        public virtual void OnRemoved(ArmoryCoreWeaponController weapon)
        {
            if (owner == weapon)
                owner = null;
        }

        public virtual void OnWeaponShot() { }

        public virtual void OnAimChanged(bool aiming) { }

        protected void RequestStatsRefresh()
        {
            owner?.RebuildInstalledAttachments();
        }

        protected static bool Contains(string value, string fragment)
        {
            return value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
