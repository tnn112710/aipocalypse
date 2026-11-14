using System.Collections.Generic;
using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreRocketProjectile : MonoBehaviour
    {
        private const int HitBufferSize = 16;
        private const int OverlapBufferSize = 64;
        private const int InitialPoolCapacity = 8;
        private static readonly Stack<ArmoryCoreRocketProjectile> Pool = new(InitialPoolCapacity);
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];
        private static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];
        private static readonly Dictionary<IArmoryCoreBallisticTarget, ArmoryCoreBallisticHit> BlastHits = new();
        private static readonly List<KeyValuePair<IArmoryCoreBallisticTarget, ArmoryCoreBallisticHit>> BlastHitList = new();
        private static readonly HashSet<Rigidbody> PushedBodies = new();
        private static readonly List<MonoBehaviour> BehaviourBuffer = new();

        private ArmoryCoreAmmunitionDefinition ammunition;
        private GameObject owner;
        private LayerMask hitMask;
        private Vector3 velocity;
        private Vector3 position;
        private Vector3 previousPosition;
        private float baseDamage;
        private float flightTime;
        private float distanceTravelled;
        private bool flying;
        private bool lingering;
        private float recycleTime;

        private Transform body;
        private Transform flame;
        private TrailRenderer smokeTrail;

        public Vector3 Velocity => velocity;
        public float DistanceTravelled => distanceTravelled;
        public bool MotorBurning => flying && IsMotorBurning(flightTime);

        public static ArmoryCoreRocketProjectile Launch(ArmoryCoreProjectileLaunchData data)
        {
            if (data.ammunition == null)
                return null;

            ArmoryCoreRocketProjectile rocket = null;
            while (Pool.Count > 0 && rocket == null)
                rocket = Pool.Pop();
            if (rocket == null)
                rocket = CreatePooledRocket();

            var direction = data.direction.sqrMagnitude > 0.0001f ? data.direction.normalized : Vector3.forward;
            rocket.ammunition = data.ammunition;
            rocket.owner = data.owner;
            rocket.hitMask = data.hitMask;
            rocket.position = data.position;
            rocket.previousPosition = data.position;
            rocket.velocity = direction * Mathf.Max(1f, data.muzzleVelocityMetersPerSecond);
            rocket.baseDamage = Mathf.Max(0f, data.baseDamage);
            rocket.flightTime = 0f;
            rocket.distanceTravelled = 0f;
            rocket.flying = true;
            rocket.lingering = false;
            rocket.transform.SetPositionAndRotation(data.position, Quaternion.LookRotation(direction));
            rocket.ApplyVisualDimensions();
            rocket.gameObject.SetActive(true);
            rocket.smokeTrail.Clear();
            rocket.smokeTrail.emitting = true;
            rocket.body.gameObject.SetActive(true);
            rocket.flame.gameObject.SetActive(rocket.MotorBurning);
            return rocket;
        }

        private static ArmoryCoreRocketProjectile CreatePooledRocket()
        {
            var root = new GameObject("ArmoryCore Rocket Projectile");
            root.hideFlags = HideFlags.HideInHierarchy;
            root.SetActive(false);
            DontDestroyOnLoad(root);
            var rocket = root.AddComponent<ArmoryCoreRocketProjectile>();

            rocket.body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body", root.transform,
                ArmoryCoreEffectMaterials.RocketBody);
            rocket.flame = CreateVisualPrimitive(PrimitiveType.Sphere, "Motor Flame", root.transform,
                ArmoryCoreEffectMaterials.MotorFlame);

            rocket.smokeTrail = root.AddComponent<TrailRenderer>();
            rocket.smokeTrail.sharedMaterial = ArmoryCoreEffectMaterials.SmokeTrail;
            rocket.smokeTrail.minVertexDistance = 0.25f;
            rocket.smokeTrail.autodestruct = false;
            rocket.smokeTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rocket.smokeTrail.receiveShadows = false;
            rocket.smokeTrail.textureMode = LineTextureMode.Stretch;
            rocket.smokeTrail.numCapVertices = 2;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.78f, 0.45f), 0f),
                    new GradientColorKey(new Color(0.78f, 0.77f, 0.75f), 0.06f),
                    new GradientColorKey(new Color(0.62f, 0.62f, 0.62f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0.55f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            rocket.smokeTrail.colorGradient = gradient;
            return rocket;
        }

        private static Transform CreateVisualPrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            // Removed immediately so the rocket's own raycasts and blast queries never see it.
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                DestroyImmediate(collider);
            visual.transform.SetParent(parent, false);
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return visual.transform;
        }

        private void ApplyVisualDimensions()
        {
            var diameter = Mathf.Max(0.02f, ammunition.projectileDiameterMillimeters * 0.001f);
            var length = Mathf.Max(diameter * 2f, ammunition.rocketLengthMeters);
            // Capsule and sphere primitives are 1 unit wide; the capsule is 2 units tall along Y.
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.localPosition = Vector3.zero;
            body.localScale = new Vector3(diameter, length * 0.5f, diameter);
            flame.localRotation = Quaternion.identity;
            flame.localPosition = new Vector3(0f, 0f, -length * 0.5f - diameter * 0.55f);
            flame.localScale = new Vector3(diameter * 0.75f, diameter * 0.75f, diameter * 2.2f);

            smokeTrail.time = ammunition.smokeTrailTime;
            smokeTrail.widthMultiplier = 1f;
            smokeTrail.widthCurve = new AnimationCurve(
                new Keyframe(0f, diameter * 0.8f),
                new Keyframe(0.15f, diameter * 2.5f),
                new Keyframe(1f, Mathf.Max(0.35f, diameter * 9f)));
        }

        private void FixedUpdate()
        {
            if (!flying)
                return;
            if (ammunition == null)
            {
                Recycle();
                return;
            }

            previousPosition = position;
            var deltaTime = Time.fixedDeltaTime;
            var plannedDistance = ammunition.MaximumSpeedMetersPerSecond * deltaTime;
            var requestedSubsteps = Mathf.CeilToInt(plannedDistance / ammunition.maximumSegmentLengthMeters);
            var substeps = Mathf.Clamp(requestedSubsteps, 1, ammunition.maximumSubstepsPerFrame);
            var stepTime = deltaTime / substeps;

            for (var i = 0; i < substeps && flying; i++)
                SimulateStep(stepTime);
        }

        private void Update()
        {
            if (lingering)
            {
                if (Time.time >= recycleTime)
                    Recycle();
                return;
            }
            if (!flying)
                return;

            // Interpolate between physics steps so the visible rocket and its trail move smoothly.
            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
            transform.position = Vector3.Lerp(previousPosition, position, alpha);
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
            flame.gameObject.SetActive(MotorBurning);
            if (flame.gameObject.activeSelf)
            {
                var flicker = Random.Range(0.8f, 1.2f);
                var diameter = Mathf.Max(0.02f, ammunition.projectileDiameterMillimeters * 0.001f);
                flame.localScale = new Vector3(diameter * 0.75f, diameter * 0.75f, diameter * 2.2f * flicker);
            }
        }

        private bool IsMotorBurning(float time)
        {
            return ammunition != null
                && ammunition.motorAccelerationMetersPerSecondSquared > 0f
                && time >= ammunition.motorIgnitionDelay
                && time < ammunition.motorIgnitionDelay + ammunition.motorBurnTime;
        }

        private void SimulateStep(float deltaTime)
        {
            flightTime += deltaTime;
            if (flightTime >= ammunition.maximumFlightTime
                || distanceTravelled >= ammunition.maximumRangeMeters)
            {
                if (ammunition.explodeOnTimeout && distanceTravelled >= ammunition.armingDistanceMeters)
                    Detonate(position, -velocity.normalized, null);
                else
                    Recycle();
                return;
            }

            var forward = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
            if (IsMotorBurning(flightTime))
            {
                velocity += forward * ammunition.motorAccelerationMetersPerSecondSquared * deltaTime;
                var maximumSpeed = ammunition.MaximumSpeedMetersPerSecond;
                if (velocity.sqrMagnitude > maximumSpeed * maximumSpeed)
                    velocity = velocity.normalized * maximumSpeed;
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
                velocity -= relativeAirVelocity.normalized * Mathf.Min(dragAcceleration * deltaTime, relativeSpeed);
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
                position = hit.point;
                HandleImpact(hit);
                return;
            }

            position += displacement;
            distanceTravelled += segmentLength;
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
                if (candidate.collider == null || IsOwnerCollider(candidate.collider))
                    continue;
                if (candidate.distance >= closestDistance)
                    continue;
                closest = candidate;
                closestDistance = candidate.distance;
                found = true;
            }
            return found;
        }

        private void HandleImpact(RaycastHit hit)
        {
            if (distanceTravelled >= ammunition.armingDistanceMeters)
            {
                Detonate(hit.point, hit.normal, hit.collider);
                return;
            }

            // Unarmed fuze: the rocket strikes as a dud and only delivers its impact damage.
            var surface = hit.collider.GetComponentInParent<ArmoryCoreBallisticMaterial>();
            var surfaceDamageMultiplier = surface == null ? 1f : surface.receivedDamageMultiplier;
            NotifyBallisticTargets(hit.collider, new ArmoryCoreBallisticHit(
                hit.point,
                hit.normal,
                velocity,
                ammunition.KineticEnergy(velocity.magnitude),
                baseDamage * surfaceDamageMultiplier,
                distanceTravelled,
                ammunition,
                owner,
                hit.collider));
            StopFlight();
        }

        private void Detonate(Vector3 point, Vector3 normal, Collider directCollider)
        {
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;
            normal.Normalize();
            var radius = ammunition.explosionRadiusMeters;
            var center = point + normal * 0.05f;
            var impactVelocity = velocity;
            var impactEnergy = ammunition.KineticEnergy(velocity.magnitude);

            BlastHits.Clear();
            PushedBodies.Clear();

            if (directCollider != null)
            {
                var surface = directCollider.GetComponentInParent<ArmoryCoreBallisticMaterial>();
                var surfaceDamageMultiplier = surface == null ? 1f : surface.receivedDamageMultiplier;
                CollectBlastHit(directCollider, new ArmoryCoreBallisticHit(
                    point,
                    normal,
                    impactVelocity,
                    impactEnergy,
                    (ammunition.explosionDamage + baseDamage) * surfaceDamageMultiplier,
                    distanceTravelled,
                    ammunition,
                    owner,
                    directCollider,
                    true));
            }

            var count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                OverlapBuffer,
                hitMask,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var collider = OverlapBuffer[i];
                if (collider == null)
                    continue;
                if (!ammunition.explosionAffectsOwner && IsOwnerCollider(collider))
                    continue;

                var closest = collider == directCollider ? point : ClosestPointOn(collider, center);
                var offset = closest - center;
                var distance = offset.magnitude;
                if (distance > radius)
                    continue;
                if (collider != directCollider && IsBlastOccluded(center, closest, distance, collider))
                    continue;

                var falloff = 1f - distance / radius;
                ApplyBlastForce(collider, center, radius);
                if (collider == directCollider)
                    continue;

                var surface = collider.GetComponentInParent<ArmoryCoreBallisticMaterial>();
                var surfaceDamageMultiplier = surface == null ? 1f : surface.receivedDamageMultiplier;
                var blastDirection = distance > 0.001f ? offset / distance : normal;
                CollectBlastHit(collider, new ArmoryCoreBallisticHit(
                    closest,
                    -blastDirection,
                    blastDirection * impactVelocity.magnitude * falloff,
                    0f,
                    ammunition.explosionDamage
                        * Mathf.Lerp(ammunition.explosionDamageAtEdge, 1f, falloff)
                        * surfaceDamageMultiplier,
                    distanceTravelled,
                    ammunition,
                    owner,
                    collider,
                    true));
            }

            // Notify after collecting so each target receives one hit, and so callbacks that
            // destroy objects cannot disturb the blast queries.
            BlastHitList.Clear();
            BlastHitList.AddRange(BlastHits);
            BlastHits.Clear();
            PushedBodies.Clear();
            for (var i = 0; i < BlastHitList.Count; i++)
            {
                var target = BlastHitList[i].Key;
                if (target is Object unityObject && unityObject == null)
                    continue;
                target.ReceiveBallisticHit(BlastHitList[i].Value);
            }
            BlastHitList.Clear();

            ArmoryCoreExplosionEffect.Spawn(point, normal, radius);
            StopFlight();
        }

        private static void CollectBlastHit(Collider collider, ArmoryCoreBallisticHit hit)
        {
            BehaviourBuffer.Clear();
            collider.GetComponentsInParent(true, BehaviourBuffer);
            for (var i = 0; i < BehaviourBuffer.Count; i++)
            {
                if (BehaviourBuffer[i] is not IArmoryCoreBallisticTarget target)
                    continue;
                if (BlastHits.TryGetValue(target, out var existing) && existing.Damage >= hit.Damage)
                    continue;
                BlastHits[target] = hit;
            }
            BehaviourBuffer.Clear();
        }

        private void ApplyBlastForce(Collider collider, Vector3 center, float radius)
        {
            var body = collider.attachedRigidbody;
            if (body == null || body.isKinematic || ammunition.explosionForce <= 0f || !PushedBodies.Add(body))
                return;
            body.AddExplosionForce(ammunition.explosionForce, center, radius, 0.4f, ForceMode.Impulse);
        }

        private bool IsBlastOccluded(Vector3 center, Vector3 point, float distance, Collider target)
        {
            if (distance <= 0.05f)
                return false;
            var direction = (point - center) / distance;
            var count = Physics.RaycastNonAlloc(
                center,
                direction,
                HitBuffer,
                distance - 0.02f,
                hitMask,
                QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var blocker = HitBuffer[i].collider;
                if (blocker == null || blocker == target || IsOwnerCollider(blocker))
                    continue;
                if (blocker.attachedRigidbody != null && blocker.attachedRigidbody == target.attachedRigidbody)
                    continue;
                return true;
            }
            return false;
        }

        private static Vector3 ClosestPointOn(Collider collider, Vector3 point)
        {
            // Physics.ClosestPoint only supports primitive and convex mesh colliders.
            if (collider is BoxCollider
                || collider is SphereCollider
                || collider is CapsuleCollider
                || (collider is MeshCollider meshCollider && meshCollider.convex))
            {
                return collider.ClosestPoint(point);
            }
            return collider.bounds.ClosestPoint(point);
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

        private void StopFlight()
        {
            if (!flying)
                return;
            flying = false;
            transform.position = position;
            body.gameObject.SetActive(false);
            flame.gameObject.SetActive(false);
            smokeTrail.emitting = false;
            // Keep the object alive until the smoke trail has faded out.
            lingering = true;
            recycleTime = Time.time + smokeTrail.time;
            ammunition = null;
            owner = null;
        }

        private void Recycle()
        {
            if (!flying && !lingering)
                return;
            flying = false;
            lingering = false;
            ammunition = null;
            owner = null;
            smokeTrail.Clear();
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
