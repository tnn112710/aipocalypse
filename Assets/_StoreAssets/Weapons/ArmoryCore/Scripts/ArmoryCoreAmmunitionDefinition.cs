using UnityEngine;

namespace ArmoryCore
{
    public enum ArmoryCoreProjectileType
    {
        Bullet,
        Rocket
    }

    /// <summary>Shape of a bullet's nose. Rifle bullets are usually spitzers, pistol bullets round nose.</summary>
    public enum ArmoryCoreBulletNose
    {
        RoundNose,
        Spitzer,
        HollowPoint,
        FlatNose
    }

    [CreateAssetMenu(menuName = "ArmoryCore Weapons/Ammunition Definition", fileName = "Ammunition")]
    public sealed class ArmoryCoreAmmunitionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;
        [Tooltip("Bullets are simulated as inert ballistic projectiles. Rockets are visible, motor-driven and explode on impact.")]
        public ArmoryCoreProjectileType projectileType = ArmoryCoreProjectileType.Bullet;

        [Header("External ballistics")]
        [Min(0.01f)] public float projectileMassGrams = 4f;
        [Min(0.01f)] public float projectileDiameterMillimeters = 5.7f;
        [Tooltip("Shape of the bullet's nose. Pick the drag coefficient to match it: spitzer ≈ 0.29, round nose ≈ 0.36.")]
        public ArmoryCoreBulletNose noseShape = ArmoryCoreBulletNose.Spitzer;
        [Tooltip("Length of the bullet itself, without the case. 0 estimates it from the mass and calibre.")]
        [Min(0f)] public float bulletLengthMillimeters;
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

        [Header("Rocket motor")]
        [Tooltip("Net acceleration produced by the sustainer motor while it burns.")]
        [Min(0f)] public float motorAccelerationMetersPerSecondSquared = 180f;
        [Tooltip("Time after launch before the sustainer motor ignites.")]
        [Min(0f)] public float motorIgnitionDelay = 0.08f;
        [Min(0f)] public float motorBurnTime = 1f;
        [Min(1f)] public float maximumRocketSpeedMetersPerSecond = 295f;

        [Header("Warhead")]
        [Tooltip("Distance the rocket must travel before the fuze arms. Earlier impacts do not detonate.")]
        [Min(0f)] public float armingDistanceMeters = 5f;
        [Min(0.1f)] public float explosionRadiusMeters = 5f;
        [Tooltip("Blast damage at the centre of the explosion. Weapon damage is added to the directly hit target.")]
        [Min(0f)] public float explosionDamage = 250f;
        [Range(0f, 1f)] public float explosionDamageAtEdge = 0.1f;
        [Tooltip("Impulse applied to rigidbodies at the centre of the explosion.")]
        [Min(0f)] public float explosionForce = 600f;
        public bool explosionAffectsOwner;
        [Tooltip("Detonate in the air when the flight time or range runs out instead of disappearing.")]
        public bool explodeOnTimeout = true;

        [Header("Rocket visuals")]
        [Min(0.05f)] public float rocketLengthMeters = 0.9f;
        [Min(0f)] public float smokeTrailTime = 1.6f;

        public float ProjectileMassKilograms => projectileMassGrams * 0.001f;
        public float RadiusMeters => projectileDiameterMillimeters * 0.0005f;
        public float CrossSectionSquareMeters => Mathf.PI * RadiusMeters * RadiusMeters;
        /// <summary>Mass per frontal area, kg/m²: the higher, the better the projectile keeps its speed and penetrates.</summary>
        public float SectionalDensity => ProjectileMassKilograms / CrossSectionSquareMeters;
        /// <summary>Ballistic coefficient m / (Cd·A), kg/m²: how well the projectile carries through the air.</summary>
        public float BallisticCoefficient => ProjectileMassKilograms / (aerodynamicDragCoefficient * CrossSectionSquareMeters);

        /// <summary>The authored bullet length, or the length of a jacketed lead bullet of this mass and calibre.</summary>
        public float BulletLengthMillimeters => bulletLengthMillimeters > 0f ? bulletLengthMillimeters : EstimatedBulletLengthMillimeters;

        /// <summary>Lead-core cylinder of the same mass and calibre, lengthened by a third for the nose.</summary>
        public float EstimatedBulletLengthMillimeters
            => ProjectileMassKilograms / (10500f * CrossSectionSquareMeters) * 1300f;

        public bool IsRocket => projectileType == ArmoryCoreProjectileType.Rocket;
        public float MaximumSpeedMetersPerSecond => IsRocket
            ? Mathf.Max(muzzleVelocityMetersPerSecond, maximumRocketSpeedMetersPerSecond)
            : muzzleVelocityMetersPerSecond;

        public float KineticEnergy(float speedMetersPerSecond)
        {
            return 0.5f * ProjectileMassKilograms * speedMetersPerSecond * speedMetersPerSecond;
        }

        /// <summary>Drag-free flight time estimate, used for sight zero compensation.</summary>
        public float EstimateFlightTime(float distanceMeters, float launchSpeedMetersPerSecond)
        {
            var speed = Mathf.Max(1f, launchSpeedMetersPerSecond);
            if (!IsRocket || motorAccelerationMetersPerSecondSquared <= 0f || motorBurnTime <= 0f)
                return distanceMeters / speed;

            var coastDistance = speed * motorIgnitionDelay;
            if (distanceMeters <= coastDistance)
                return distanceMeters / speed;

            var acceleration = motorAccelerationMetersPerSecondSquared;
            var remaining = distanceMeters - coastDistance;
            var accelerationTime = Mathf.Min(
                motorBurnTime,
                Mathf.Max(0f, maximumRocketSpeedMetersPerSecond - speed) / acceleration);
            var accelerationDistance = speed * accelerationTime
                + 0.5f * acceleration * accelerationTime * accelerationTime;
            if (remaining <= accelerationDistance)
            {
                var time = (-speed + Mathf.Sqrt(speed * speed + 2f * acceleration * remaining)) / acceleration;
                return motorIgnitionDelay + time;
            }

            var cruiseSpeed = speed + acceleration * accelerationTime;
            return motorIgnitionDelay + accelerationTime + (remaining - accelerationDistance) / cruiseSpeed;
        }

        private void OnValidate()
        {
            projectileMassGrams = Mathf.Max(0.01f, projectileMassGrams);
            projectileDiameterMillimeters = Mathf.Max(0.01f, projectileDiameterMillimeters);
            bulletLengthMillimeters = Mathf.Max(0f, bulletLengthMillimeters);
            muzzleVelocityMetersPerSecond = Mathf.Max(1f, muzzleVelocityMetersPerSecond);
            maximumFlightTime = Mathf.Max(0.01f, maximumFlightTime);
            maximumRangeMeters = Mathf.Max(1f, maximumRangeMeters);
            maximumSegmentLengthMeters = Mathf.Max(0.01f, maximumSegmentLengthMeters);
            maximumSubstepsPerFrame = Mathf.Max(1, maximumSubstepsPerFrame);
            maximumRocketSpeedMetersPerSecond = Mathf.Max(1f, maximumRocketSpeedMetersPerSecond);
            explosionRadiusMeters = Mathf.Max(0.1f, explosionRadiusMeters);
            rocketLengthMeters = Mathf.Max(0.05f, rocketLengthMeters);
        }
    }
}
