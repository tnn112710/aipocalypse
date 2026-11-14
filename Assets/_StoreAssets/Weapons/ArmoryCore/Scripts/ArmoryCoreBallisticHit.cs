using UnityEngine;

namespace ArmoryCore
{
    public readonly struct ArmoryCoreBallisticHit
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Velocity;
        public readonly float KineticEnergyJoules;
        public readonly float Damage;
        public readonly float DistanceMeters;
        public readonly ArmoryCoreAmmunitionDefinition Ammunition;
        public readonly GameObject Shooter;
        public readonly Collider Collider;
        /// <summary>True when the hit comes from a rocket warhead blast rather than a projectile strike.</summary>
        public readonly bool IsExplosion;

        public ArmoryCoreBallisticHit(
            Vector3 point,
            Vector3 normal,
            Vector3 velocity,
            float kineticEnergyJoules,
            float damage,
            float distanceMeters,
            ArmoryCoreAmmunitionDefinition ammunition,
            GameObject shooter,
            Collider collider,
            bool isExplosion = false)
        {
            Point = point;
            Normal = normal;
            Velocity = velocity;
            KineticEnergyJoules = kineticEnergyJoules;
            Damage = damage;
            DistanceMeters = distanceMeters;
            Ammunition = ammunition;
            Shooter = shooter;
            Collider = collider;
            IsExplosion = isExplosion;
        }
    }

    public interface IArmoryCoreBallisticTarget
    {
        void ReceiveBallisticHit(ArmoryCoreBallisticHit hit);
    }
}
