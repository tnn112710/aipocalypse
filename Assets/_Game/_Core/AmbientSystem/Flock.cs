using MB13.Aipocalypse.Core.SpawnSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.AmbientSystem
{
    // A flock of birds or a school of fish spawned as one Spawnable: its members are plain renderers moved by FlockSystem's
    // central tick (boids, FlockMath), kept round where it spawned and inside its card's height or depth band. No tree, agent,
    // Animator or Update per member. Spawn it through SpawnService; one placed in a scene stays still.
    [RequireComponent(typeof(Spawnable))]
    public sealed class Flock : MonoBehaviour, ISpawnable
    {
        // Metres kept from a water body's surface and bed.
        const float WaterMargin = 0.3f;
        const float Probe = 200f;
        // Water roams are fitted by probing rings this many metres apart round home, 8 points each.
        const float RoamProbeStep = 2f;
        const float MinRoam = 2f;
        // Water at least this deep (m), at most this far above or below home's level (another reach of a river), still counts.
        const float RoamMinDepth = 0.8f;
        const float RoamLevelTolerance = 0.5f;

        [SerializeField] FlockDefinition _definition;

        FlockSystem _system;
        SpawnSystemSettings _settings;
        IWaterLevel _water;
        Transform[] _members;
        Vector3[] _positions;
        Vector3[] _velocities;

        public FlockDefinition Definition => _definition;
        public int Count { get; private set; }
        public Vector3 Home { get; private set; }
        // The band its members stay in (world heights).
        public float Floor { get; private set; }
        public float Ceiling { get; private set; }
        // Metres from home it roams: the definition's, or less under water when the water round home is narrower (a stream).
        public float RoamRadius { get; private set; }

        [Inject]
        void Construct(SpawnSystemSettings settings, IObjectResolver resolver)
        {
            _settings = settings;
            resolver.TryResolve(out _system);
            resolver.TryResolve(out _water);
        }

        public Vector3 MemberPosition(int index) => _positions[index];

        void ISpawnable.OnSpawned(in SpawnInfo info)
        {
            if (_definition == null || _definition.Member == null || _system == null)
            {
                Debug.LogError($"{name}: no flock definition with a member, or no FlockSystem (the scope needs an AmbientInstaller).", this);
                return;
            }
            Home = transform.position;
            SpawnPlacement placement = info.Card != null ? info.Card.Placement : null;
            SetBand(placement);
            RoamRadius = FitRoam(placement);
            EnsureMembers();
            Count = Random.Range(Mathf.Max(1, _definition.Count.x), Mathf.Max(_definition.Count.x, _definition.Count.y) + 1);
            Count = Mathf.Min(Count, _members.Length);
            float spread = Mathf.Max(1f, _definition.Separation * Mathf.Sqrt(Count));
            for (int i = 0; i < _members.Length; i++)
            {
                bool used = i < Count;
                _members[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                Vector2 around = Random.insideUnitCircle * spread;
                _positions[i] = new Vector3(Home.x + around.x, Mathf.Clamp(Home.y + Random.Range(-0.5f, 0.5f), Floor, Ceiling), Home.z + around.y);
                Vector2 heading = Random.insideUnitCircle.normalized * _definition.Speed;
                _velocities[i] = new Vector3(heading.x, 0f, heading.y);
                _members[i].SetPositionAndRotation(_positions[i], Quaternion.LookRotation(_velocities[i]));
            }
            _system.Add(this);
        }

        void ISpawnable.OnDespawned() => _system?.Remove(this);

        // One step of every member (FlockSystem).
        internal void Simulate(float deltaTime, Vector3 threat, bool hasThreat, float time)
        {
            for (int i = 0; i < Count; i++)
            {
                Vector3 desired = FlockMath.Steer(i, _positions, _velocities, Count, Home, Floor, Ceiling, _definition, threat, hasThreat, time,
                    RoamRadius);
                Vector3 velocity = Vector3.MoveTowards(_velocities[i], desired, _definition.Acceleration * deltaTime);
                Vector3 position = _positions[i] + velocity * deltaTime;
                position.y = Mathf.Clamp(position.y, Floor, Ceiling);
                _velocities[i] = velocity;
                _positions[i] = position;
                if (velocity.sqrMagnitude > 0.0001f)
                    _members[i].SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
                else
                    _members[i].position = position;
            }
        }

        void EnsureMembers()
        {
            if (_members != null)
                return;
            int most = Mathf.Max(1, Mathf.Max(_definition.Count.x, _definition.Count.y));
            _members = new Transform[most];
            _positions = new Vector3[most];
            _velocities = new Vector3[most];
            for (int i = 0; i < most; i++)
            {
                GameObject member = Instantiate(_definition.Member, transform);
                member.name = _definition.Member.name;
                _members[i] = member.transform;
            }
        }

        // The largest ring round home (up to the definition's roam) whose points all have water deep enough at about home's level.
        float FitRoam(SpawnPlacement placement)
        {
            float roam = _definition.RoamRadius;
            if (placement == null || placement.Medium != SpawnMedium.Water || _water == null || !_water.TryGetLevel(Home, out float level))
                return roam;
            LayerMask ground = _settings.DefaultRules.GroundMask;
            for (float radius = RoamProbeStep; radius <= roam; radius += RoamProbeStep)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector3 point = Home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    bool wet = _water.TryGetLevel(point, out float there) && Mathf.Abs(there - level) <= RoamLevelTolerance;
                    if (wet && Physics.Raycast(new Vector3(point.x, there, point.z), Vector3.down, out RaycastHit bed, Probe, ground,
                            QueryTriggerInteraction.Ignore))
                        wet = there - bed.point.y >= RoamMinDepth;
                    if (!wet)
                        return Mathf.Max(MinRoam, radius - RoamProbeStep);
                }
            }
            return roam;
        }

        // Air: the card's height band over the ground or water below. Water: its depth band, kept off the surface and the bed.
        void SetBand(SpawnPlacement placement)
        {
            Floor = Home.y - 2f;
            Ceiling = Home.y + 2f;
            if (placement == null)
                return;
            LayerMask ground = _settings.DefaultRules.GroundMask;
            var above = new Vector3(Home.x, Home.y + Probe * 0.5f, Home.z);
            bool hasGround = Physics.Raycast(above, Vector3.down, out RaycastHit hit, Probe, ground, QueryTriggerInteraction.Ignore);
            float level = float.NegativeInfinity;
            bool hasWater = _water != null && _water.TryGetLevel(Home, out level);
            switch (placement.Medium)
            {
                case SpawnMedium.Air:
                    float surface = Mathf.Max(hasGround ? hit.point.y : Home.y - placement.Height.x, level);
                    Floor = surface + Mathf.Min(placement.Height.x, placement.Height.y);
                    Ceiling = surface + Mathf.Max(placement.Height.x, placement.Height.y);
                    break;
                case SpawnMedium.Water when hasWater:
                    float bed = hasGround && hit.point.y < level ? hit.point.y : level - Mathf.Max(placement.Height.y, 1f);
                    Floor = Mathf.Max(bed + WaterMargin, level - Mathf.Max(placement.Height.x, placement.Height.y));
                    Ceiling = level - Mathf.Max(WaterMargin, Mathf.Min(placement.Height.x, placement.Height.y));
                    break;
            }
            if (Ceiling < Floor)
                Floor = Ceiling = Home.y;
            Home = new Vector3(Home.x, Mathf.Clamp(Home.y, Floor, Ceiling), Home.z);
        }
    }
}
