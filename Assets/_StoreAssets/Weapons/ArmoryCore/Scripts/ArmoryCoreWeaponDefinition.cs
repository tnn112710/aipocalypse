using System;
using UnityEngine;

namespace ArmoryCore
{
    public enum ArmoryCoreFireMode
    {
        Safe,
        SemiAutomatic,
        Burst,
        Automatic
    }

    public enum ArmoryCoreWeaponAction
    {
        ClosedBolt,
        OpenBolt,
        Manual,
        Pump,
        BreakAction
    }

    public enum ArmoryCoreReloadType
    {
        DetachableMagazine,
        InternalMagazine,
        SingleRound,
        BeltFed
    }

    [CreateAssetMenu(menuName = "ArmoryCore Weapons/Weapon Definition", fileName = "Weapon")]
    public sealed class ArmoryCoreWeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        public string[] weaponTags = Array.Empty<string>();

        [Header("Mechanism")]
        public ArmoryCoreWeaponAction action = ArmoryCoreWeaponAction.ClosedBolt;
        public ArmoryCoreReloadType reloadType = ArmoryCoreReloadType.DetachableMagazine;
        public ArmoryCoreFireMode[] fireModes = { ArmoryCoreFireMode.Safe, ArmoryCoreFireMode.SemiAutomatic };
        [Min(1f)] public float roundsPerMinute = 600f;
        [Min(2)] public int burstSize = 3;
        [Min(0f)] public float actionCycleTime = 0.7f;

        [Header("Feeding")]
        public ArmoryCoreAmmunitionDefinition ammunition;
        [Min(1)] public int magazineCapacity = 30;
        [Range(0, 1)] public int chamberCapacity = 1;
        public bool spawnWithFullMagazine = true;
        public bool spawnWithChamberLoaded = true;

        [Header("Shot")]
        [Min(1)] public int projectilesPerShot = 1;
        [Min(0f)] public float damagePerProjectile = 30f;
        [Min(0.1f)] public float muzzleVelocityMultiplier = 1f;
        [Min(0.01f)] public float accuracyMinutesOfAngle = 3f;
        [Min(1f)] public float hipFireSpreadMultiplier = 12f;
        [Min(1f)] public float movingSpreadMultiplier = 2f;
        [Min(1f)] public float heatSpreadMultiplier = 2.5f;

        [Header("Recoil")]
        [Min(0f)] public float verticalRecoilDegrees = 1.1f;
        [Min(0f)] public float horizontalRecoilDegrees = 0.35f;
        [Min(0f)] public float recoilRandomness = 0.2f;
        [Min(0.01f)] public float recoilRecoveryDegreesPerSecond = 8f;
        [Min(0.01f)] public float recoilRecoveryDelay = 0.08f;

        [Header("Handling")]
        [Min(0.1f)] public float weaponMassKilograms = 3.4f;
        [Min(0.01f)] public float aimDownSightsTime = 0.25f;
        [Min(0.01f)] public float tacticalReloadTime = 2.4f;
        [Min(0.01f)] public float emptyReloadTime = 3.1f;
        [Min(0.01f)] public float perRoundReloadTime = 0.65f;

        [Header("Reliability and heat")]
        [Tooltip("Mean rounds between stoppages for a clean, cool weapon at full durability.")]
        [Min(1f)] public float meanRoundsBetweenFailures = 3500f;
        [Min(0f)] public float heatPerShot = 1f;
        [Min(0.01f)] public float heatDissipationPerSecond = 2.5f;
        [Min(0.01f)] public float heatForAccuracyPenalty = 35f;
        [Min(0.01f)] public float heatForCookOff = 100f;
        [Range(0f, 1f)] public float durabilityLossPerShot = 0.000025f;
        [Range(0f, 1f)] public float foulingPerShot = 0.00015f;

        [Header("Compatibility")]
        public string[] compatibleAttachmentIds = Array.Empty<string>();

        [Header("Audio (ArmoryCore)")]
        public AudioClip shotSound;
        public AudioClip suppressedShotSound;
        public AudioClip reloadSound;
        public AudioClip emptySound;
        [Range(0f, 1f)] public float shotVolume = 1f;
        [Range(0f, 1f)] public float reloadVolume = 0.8f;
        [Range(0f, 1f)] public float emptyVolume = 0.75f;
        [Range(0f, 0.15f)] public float shotPitchVariation = 0.025f;
        [Min(0.01f)] public float audioMinDistanceMeters = 2f;
        [Min(1f)] public float audioMaxDistanceMeters = 250f;

        public bool Supports(ArmoryCoreFireMode mode)
        {
            if (fireModes == null)
                return false;
            for (var i = 0; i < fireModes.Length; i++)
            {
                if (fireModes[i] == mode)
                    return true;
            }
            return false;
        }

        private void OnValidate()
        {
            roundsPerMinute = Mathf.Max(1f, roundsPerMinute);
            magazineCapacity = Mathf.Max(1, magazineCapacity);
            projectilesPerShot = Mathf.Max(1, projectilesPerShot);
            chamberCapacity = Mathf.Clamp(chamberCapacity, 0, 1);
            muzzleVelocityMultiplier = Mathf.Max(0.1f, muzzleVelocityMultiplier);
            accuracyMinutesOfAngle = Mathf.Max(0.01f, accuracyMinutesOfAngle);
            meanRoundsBetweenFailures = Mathf.Max(1f, meanRoundsBetweenFailures);
            audioMinDistanceMeters = Mathf.Max(0.01f, audioMinDistanceMeters);
            audioMaxDistanceMeters = Mathf.Max(audioMinDistanceMeters, audioMaxDistanceMeters);
        }
    }
}
