using UnityEngine;

namespace ArmoryCore
{
    [CreateAssetMenu(menuName = "ArmoryCore Weapons/Ammunition Definition", fileName = "Ammunition")]
    public sealed class ArmoryCoreAmmunitionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("External ballistics")]
        [Min(0.01f)] public float projectileMassGrams = 4f;
        [Min(0.01f)] public float projectileDiameterMillimeters = 5.7f;
        [Min(1f)] public float muzzleVelocityMetersPerSecond = 880f;
        [Range(0.01f, 2f)] public float aerodynamicDragCoefficient = 0.24f;
        [Range(0f, 2f)] public float gravityMultiplier = 1f;
        [Min(0.01f)] public float maximumFlightTime = 6f;
        [Min(1f)] public float maximumRangeMeters = 1500f;
        [Min(0.01f)] public float minimumLethalVelocity = 60f;

        [Header("Terminal ballistics")]
        [Range(0f, 1f)] public float retainedDamageAtMinimumVelocity = 0.12f;
        [Min(0f)] public float penetrationMultiplier = 1f;
        [Range(0f, 1f)] public float ricochetEnergyRetention = 0.55f;

        [Header("Simulation")]
        [Min(0.01f)] public float maximumSegmentLengthMeters = 2f;
        [Min(1)] public int maximumSubstepsPerFrame = 24;

        public float ProjectileMassKilograms => projectileMassGrams * 0.001f;
        public float RadiusMeters => projectileDiameterMillimeters * 0.0005f;
        public float CrossSectionSquareMeters => Mathf.PI * RadiusMeters * RadiusMeters;

        public float KineticEnergy(float speedMetersPerSecond)
        {
            return 0.5f * ProjectileMassKilograms * speedMetersPerSecond * speedMetersPerSecond;
        }

        private void OnValidate()
        {
            projectileMassGrams = Mathf.Max(0.01f, projectileMassGrams);
            projectileDiameterMillimeters = Mathf.Max(0.01f, projectileDiameterMillimeters);
            muzzleVelocityMetersPerSecond = Mathf.Max(1f, muzzleVelocityMetersPerSecond);
            maximumFlightTime = Mathf.Max(0.01f, maximumFlightTime);
            maximumRangeMeters = Mathf.Max(1f, maximumRangeMeters);
            maximumSegmentLengthMeters = Mathf.Max(0.01f, maximumSegmentLengthMeters);
            maximumSubstepsPerFrame = Mathf.Max(1, maximumSubstepsPerFrame);
        }
    }
}
