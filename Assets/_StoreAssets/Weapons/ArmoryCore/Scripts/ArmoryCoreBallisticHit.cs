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

        public ArmoryCoreBallisticHit(
            Vector3 point,
            Vector3 normal,
            Vector3 velocity,
            float kineticEnergyJoules,
            float damage,
            float distanceMeters,
            ArmoryCoreAmmunitionDefinition ammunition,
            GameObject shooter,
            Collider collider)
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
        }
    }

    public interface IArmoryCoreBallisticTarget
    {
        void ReceiveBallisticHit(ArmoryCoreBallisticHit hit);
    }
}
