using System.Collections.Generic;
using UnityEngine;

namespace ArmoryCore
{
    public struct ArmoryCoreProjectileLaunchData
    {
        public Vector3 position;
        public Vector3 direction;
        public float muzzleVelocityMetersPerSecond;
        public float baseDamage;
        public ArmoryCoreAmmunitionDefinition ammunition;
        public LayerMask hitMask;
        public GameObject owner;
    }

    [DisallowMultipleComponent]
    public sealed class ArmoryCoreBallisticProjectile : MonoBehaviour
    {
        private const int HitBufferSize = 16;
        private const int InitialPoolCapacity = 64;
        private static readonly Stack<ArmoryCoreBallisticProjectile> Pool = new(InitialPoolCapacity);
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];

        private ArmoryCoreAmmunitionDefinition ammunition;
        private GameObject owner;
        private LayerMask hitMask;
        private Vector3 velocity;
        private Vector3 position;
        private float baseDamage;
        private float muzzleEnergy;
        private float flightTime;
        private float distanceTravelled;
        private Collider lastPenetratedCollider;
        private bool activeProjectile;

        public Vector3 Velocity => velocity;
        public float DistanceTravelled => distanceTravelled;

        public static ArmoryCoreBallisticProjectile Launch(ArmoryCoreProjectileLaunchData data)
        {
            if (data.ammunition == null)
                return null;

            ArmoryCoreBallisticProjectile projectile = null;
            while (Pool.Count > 0 && projectile == null)
                projectile = Pool.Pop();
            if (projectile == null)
                projectile = CreatePooledProjectile();
            projectile.gameObject.SetActive(true);
            projectile.ammunition = data.ammunition;
            projectile.owner = data.owner;
            projectile.hitMask = data.hitMask;
            projectile.position = data.position;
            projectile.transform.position = data.position;
            projectile.velocity = data.direction.normalized * Mathf.Max(1f, data.muzzleVelocityMetersPerSecond);
            projectile.baseDamage = Mathf.Max(0f, data.baseDamage);
            projectile.muzzleEnergy = data.ammunition.KineticEnergy(projectile.velocity.magnitude);
            projectile.flightTime = 0f;
            projectile.distanceTravelled = 0f;
            projectile.lastPenetratedCollider = null;
            projectile.activeProjectile = true;
            return projectile;
        }

        private static ArmoryCoreBallisticProjectile CreatePooledProjectile()
        {
            var gameObject = new GameObject("ArmoryCore Ballistic Projectile");
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<ArmoryCoreBallisticProjectile>();
        }

        private void FixedUpdate()
        {
            if (!activeProjectile || ammunition == null)
            {
                Recycle();
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            var plannedDistance = velocity.magnitude * deltaTime;
            var requestedSubsteps = Mathf.CeilToInt(plannedDistance / ammunition.maximumSegmentLengthMeters);
            var substeps = Mathf.Clamp(requestedSubsteps, 1, ammunition.maximumSubstepsPerFrame);
            var stepTime = deltaTime / substeps;

            for (var i = 0; i < substeps && activeProjectile; i++)
                SimulateStep(stepTime);

            if (!activeProjectile)
                return;
            transform.position = position;
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }

        private void SimulateStep(float deltaTime)
        {
            flightTime += deltaTime;
            var speed = velocity.magnitude;
            if (flightTime >= ammunition.maximumFlightTime
                || distanceTravelled >= ammunition.maximumRangeMeters
                || speed < ammunition.minimumLethalVelocity)
            {
                Recycle();
                return;
            }

            var relativeAirVelocity = velocity - ArmoryCoreBallisticsEnvironment.WindVelocity;
            var relativeSpeed = relativeAirVelocity.magnitude;
            if (relativeSpeed > 0.001f)
            {
                var dragForce = 0.5f
                    * ArmoryCoreBallisticsEnvironment.AirDensity
                    * ammunition.aerodynamicDragCoefficient
                    * ammunition.CrossSectionSquareMeters
                    * relativeSpeed
                    * relativeSpeed;
                var dragAcceleration = dragForce / ammunition.ProjectileMassKilograms;
                velocity -= relativeAirVelocity.normalized * dragAcceleration * deltaTime;
            }
            velocity += Physics.gravity * ammunition.gravityMultiplier * deltaTime;

            var displacement = velocity * deltaTime;
            var segmentLength = displacement.magnitude;
            if (segmentLength <= 0.00001f)
                return;
            var direction = displacement / segmentLength;

            if (TryGetFirstValidHit(position, direction, segmentLength, out var hit))
            {
                distanceTravelled += hit.distance;
                HandleImpact(hit, direction);
                return;
            }

            position += displacement;
            distanceTravelled += segmentLength;
            lastPenetratedCollider = null;
        }

        private bool TryGetFirstValidHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            var count = Physics.RaycastNonAlloc(
                origin,
                direction,
                HitBuffer,
                distance,
                hitMask,
                QueryTriggerInteraction.Ignore);
            var found = false;
            var closestDistance = float.PositiveInfinity;
            closest = default;
            for (var i = 0; i < count; i++)
            {
                var candidate = HitBuffer[i];
                if (candidate.collider == null
                    || candidate.collider == lastPenetratedCollider
                    || IsOwnerCollider(candidate.collider))
                    continue;
                if (candidate.distance >= closestDistance)
                    continue;
                closest = candidate;
                closestDistance = candidate.distance;
                found = true;
            }
            return found;
        }

        private void HandleImpact(RaycastHit hit, Vector3 incomingDirection)
        {
            var speed = velocity.magnitude;
            var energy = ammunition.KineticEnergy(speed);
            var damageFraction = muzzleEnergy <= 0.001f ? 0f : Mathf.Clamp01(energy / muzzleEnergy);
            var surface = hit.collider.GetComponentInParent<ArmoryCoreBallisticMaterial>();
            var surfaceDamageMultiplier = surface == null ? 1f : surface.receivedDamageMultiplier;
            var damage = baseDamage
                * Mathf.Lerp(ammunition.retainedDamageAtMinimumVelocity, 1f, damageFraction)
                * surfaceDamageMultiplier;
            var hitData = new ArmoryCoreBallisticHit(
                hit.point,
                hit.normal,
                velocity,
                energy,
                damage,
                distanceTravelled,
                ammunition,
                owner,
                hit.collider);
            NotifyBallisticTargets(hit.collider, hitData);

            if (surface != null && TryRicochet(surface, hit, incomingDirection))
                return;
            if (surface != null && TryPenetrate(surface, hit, incomingDirection, energy))
                return;

            Recycle();
        }

        private bool TryPenetrate(
            ArmoryCoreBallisticMaterial surface,
            RaycastHit hit,
            Vector3 incomingDirection,
            float kineticEnergy)
        {
            var normalCosine = Mathf.Max(0.08f, Mathf.Abs(Vector3.Dot(-incomingDirection, hit.normal)));
            var pathMillimeters = surface.thicknessMillimeters / normalCosine;
            var randomFactor = Random.Range(
                1f - surface.penetrationRandomness,
                1f + surface.penetrationRandomness);
            var energyCost = pathMillimeters
                * surface.penetrationEnergyPerMillimeter
                * randomFactor
                / Mathf.Max(0.01f, ammunition.penetrationMultiplier);
            if (kineticEnergy <= energyCost)
                return false;

            var remainingEnergy = kineticEnergy - energyCost;
            var remainingSpeed = Mathf.Sqrt(2f * remainingEnergy / ammunition.ProjectileMassKilograms);
            velocity = incomingDirection * remainingSpeed;
            position = hit.point + incomingDirection * (pathMillimeters * 0.001f + 0.002f);
            distanceTravelled += pathMillimeters * 0.001f;
            lastPenetratedCollider = hit.collider;
            return true;
        }

        private bool TryRicochet(
            ArmoryCoreBallisticMaterial surface,
            RaycastHit hit,
            Vector3 incomingDirection)
        {
            var angleFromNormal = Vector3.Angle(-incomingDirection, hit.normal);
            if (angleFromNormal < surface.minimumRicochetAngleFromNormal)
                return false;
            var grazingAmount = Mathf.InverseLerp(
                surface.minimumRicochetAngleFromNormal,
                90f,
                angleFromNormal);
            if (Random.value > surface.ricochetChanceAtGrazingAngle * grazingAmount)
                return false;

            var reflected = Vector3.Reflect(incomingDirection, hit.normal).normalized;
            var deflection = Random.insideUnitSphere * 0.025f;
            var retainedEnergy = ammunition.KineticEnergy(velocity.magnitude)
                * ammunition.ricochetEnergyRetention;
            var retainedSpeed = Mathf.Sqrt(2f * retainedEnergy / ammunition.ProjectileMassKilograms);
            velocity = (reflected + deflection).normalized * retainedSpeed;
            position = hit.point + velocity.normalized * 0.003f;
            lastPenetratedCollider = hit.collider;
            return true;
        }

        private bool IsOwnerCollider(Collider collider)
        {
            return owner != null && collider.transform.IsChildOf(owner.transform);
        }

        private static void NotifyBallisticTargets(Collider collider, ArmoryCoreBallisticHit hit)
        {
            var behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IArmoryCoreBallisticTarget target)
                    target.ReceiveBallisticHit(hit);
            }
        }

        private void Recycle()
        {
            if (!activeProjectile)
                return;
            activeProjectile = false;
            ammunition = null;
            owner = null;
            lastPenetratedCollider = null;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
