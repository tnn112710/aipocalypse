#if GRAPH_DESIGNER
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>Specifies how the threat used for concealment is supplied.</summary>
    public enum CoverThreatMode { Target, Position, Direction, AgentForward, Automatic }

    /// <summary>Explains why a cover candidate or movement request was rejected.</summary>
    public enum CoverFailure { None, InvalidThreat, NoGeometry, Exposed, Obstructed, Occupied, Unreachable, DestinationRejected, MovementFailed, Reserved, TooClose, ExhaustedRetries }

    /// <summary>The stance that a cover selection requires the character controller to apply.</summary>
    public enum CoverStance { Standing, Crouching }
    /// <summary>The stances that a cover query may consider.</summary>
    public enum CoverStanceMode { Standing, Crouching, Either }
    /// <summary>The sources used to generate candidate positions.</summary>
    public enum CoverSource { Geometry, AuthoredPoints, Both }
    /// <summary>The preference used after hard safety checks.</summary>
    public enum CoverSelection { ShortestPath, ThreatDistance, AuthoredPriority }
    /// <summary>Whether to finish on arrival or keep protecting the selected position.</summary>
    public enum CoverCompletion { Arrival, Maintain }

    /// <summary>Performs bounded surface searches and retains optional candidate diagnostics.</summary>
    public sealed class CoverSearch
    {
        /// <summary>Samples navigation without changing the active destination.</summary>
        /// <param name="position">The requested and sampled position.</param>
        /// <returns>True when navigation was sampled.</returns>
        public delegate bool SamplePosition(ref Vector3 position);
        /// <summary>Measures a complete route without changing the active destination.</summary>
        /// <param name="position">The destination.</param>
        /// <param name="distance">The route length.</param>
        /// <returns>True when a complete route exists.</returns>
        public delegate bool PathDistance(Vector3 position, out float distance);

        /// <summary>Contains the geometry settings for one search.</summary>
        public struct Settings
        {
            [Tooltip("The moving actor.")] public Transform Actor;
            [Tooltip("The threat point or normalized direction toward the threat.")] public Vector3 Threat;
            [Tooltip("Whether Threat represents a direction.")] public bool Directional;
            [Tooltip("The potential cover layers.")] public int Layers;
            [Tooltip("The layers containing other occupants.")] public int OccupancyLayers;
            [Tooltip("The search radius.")] public float Distance;
            [Tooltip("The height that must be hidden.")] public float Height;
            [Tooltip("The actor's half-width and clearance radius.")] public float Radius;
            [Tooltip("The surface clearance.")] public float Offset;
            [Tooltip("The additional occupancy radius.")] public float OccupancyRadius;
            [Tooltip("Whether to retain rejected candidates for gizmos.")] public bool Debug;
            [Tooltip("The target hierarchy excluded from cover geometry.")] public Transform ThreatObject;
            [Tooltip("The permitted stance selection.")] public CoverStanceMode Stances;
            [Tooltip("The crouching body height.")] public float CrouchHeight;
            [Tooltip("Infer standing height and radius from the actor's controller, capsule, or NavMeshAgent.")] public bool InferDimensions;
            [Tooltip("The source of cover candidates.")] public CoverSource Source;
            [Tooltip("The preference applied after geometry validation.")] public CoverSelection Selection;
            [Tooltip("The weight of greater distance from a positional threat.")] public float ThreatDistanceWeight;
            [Tooltip("The weight of authored cover priorities.")] public float PriorityWeight;
            [Tooltip("The minimum distance from a positional threat.")] public float MinimumThreatDistance;
            [Tooltip("Reject other actors' reserved positions.")] public bool UseReservations;
        }

        /// <summary>A candidate rejection retained for visualization.</summary>
        private struct Candidate
        {
            [Tooltip("The sampled foot position.")] public Vector3 Position;
            [Tooltip("The candidate rejection reason.")] public CoverFailure Failure;
            [Tooltip("The body height tested at this candidate.")] public float Height;
        }

        private Collider[] m_Colliders;
        private Collider[] m_Overlaps;
        private readonly RaycastHit[] m_SightHits = new RaycastHit[32];
        private readonly List<Candidate> m_Candidates = new List<Candidate>();
        private Settings m_Settings;
        private CoverPoint[] m_Points;
        private int m_Count, m_PointCount, m_Index, m_Probe, m_StanceIndex;
        private SamplePosition m_Sample;
        private PathDistance m_Path;
        private Vector3 m_Origin;
        private float m_TestHeight;
        private UnityEngine.AI.NavMeshPath m_DebugPath;
        private Vector3[] m_DebugCorners = new Vector3[32];
        public bool IsSearching { get; private set; }
        public bool HasSelection => Collider != null;
        public CoverStance RequiredStance { get; private set; }
        public CoverPoint Point { get; private set; }
        public Vector3 AuthoredPosition { get; private set; }
        public float SelectedHeight { get; private set; }
        public float Radius => m_Settings.Radius;
        public float Score { get; private set; }
        public float Priority { get; private set; }
        public int LastStepCandidates { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Normal { get; private set; }
        public Collider Collider { get; private set; }
        public CoverFailure Failure { get; private set; }
        public bool Truncated { get; private set; }

        /// <summary>Resolves target, last-known position, and directional threat inputs.</summary>
        /// <param name="mode">The input mode.</param>
        /// <param name="target">The optional threat object.</param>
        /// <param name="offset">The threat-local eye offset.</param>
        /// <param name="position">The world-space threat position.</param>
        /// <param name="direction">The world-space direction toward the threat.</param>
        /// <param name="actor">The moving actor.</param>
        /// <param name="threat">The resolved point or direction.</param>
        /// <param name="directional">Whether the resolved threat is directional.</param>
        /// <returns>True when the requested source is valid.</returns>
        public static bool ResolveThreat(CoverThreatMode mode, GameObject target, Vector3 offset, Vector3 position,
            Vector3 direction, Transform actor, out Vector3 threat, out bool directional)
        {
            if (mode == CoverThreatMode.Automatic) { mode = target != null ? CoverThreatMode.Target : CoverThreatMode.AgentForward; }
            directional = mode == CoverThreatMode.Direction || mode == CoverThreatMode.AgentForward;
            threat = position;
            if (mode == CoverThreatMode.Target) {
                if (target == null) { return false; }
                threat = target.transform.TransformPoint(offset);
            } else if (directional) {
                threat = mode == CoverThreatMode.AgentForward ? actor.forward : direction;
                threat.y = 0f;
                if (threat.sqrMagnitude < 0.0001f) { return false; }
                threat.Normalize();
            }
            return true;
        }

        /// <summary>Infers dimensions when requested and clamps the upright capsule to valid dimensions.</summary>
        /// <param name="settings">The settings to normalize.</param>
        /// <returns>The normalized settings.</returns>
        public static Settings Normalize(Settings settings)
        {
            if (settings.InferDimensions && settings.Actor != null) {
                var controller = settings.Actor.GetComponent<CharacterController>();
                var capsule = settings.Actor.GetComponent<CapsuleCollider>();
                var agent = settings.Actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
                var scale = settings.Actor.lossyScale;
                if (controller != null) { settings.Height = controller.height * Mathf.Abs(scale.y); settings.Radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)); }
                else if (capsule != null && capsule.direction == 1) { settings.Height = capsule.height * Mathf.Abs(scale.y); settings.Radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)); }
                else if (agent != null) { settings.Height = agent.height; settings.Radius = agent.radius; }
            }
            settings.Radius = Mathf.Max(0.01f, settings.Radius);
            settings.Height = Mathf.Max(settings.Radius * 2f, settings.Height);
            settings.CrouchHeight = Mathf.Clamp(settings.CrouchHeight > 0 ? settings.CrouchHeight : settings.Height * 0.6f, settings.Radius * 2f, settings.Height);
            settings.Distance = Mathf.Max(0.1f, settings.Distance);
            return settings;
        }

        /// <summary>Prepares reusable validation buffers without querying or changing navigation.</summary>
        /// <param name="settings">The actor and threat settings.</param>
        /// <param name="maximumOverlaps">The bounded overlap capacity.</param>
        public void Configure(Settings settings, int maximumOverlaps)
        {
            m_Settings = Normalize(settings);
            if (m_Overlaps == null || m_Overlaps.Length != Mathf.Max(1, maximumOverlaps)) { m_Overlaps = new Collider[Mathf.Max(1, maximumOverlaps)]; }
        }

        /// <summary>Begins a bounded search; Step controls how much candidate work is done per update.</summary>
        /// <param name="settings">The search settings.</param>
        /// <param name="maximumColliders">The geometry and authored-point budget.</param>
        /// <param name="maximumOverlaps">The overlap budget.</param>
        /// <param name="sample">The navigation sampler.</param>
        /// <param name="path">The complete-route query.</param>
        public void Begin(Settings settings, int maximumColliders, int maximumOverlaps, SamplePosition sample, PathDistance path)
        {
            Configure(settings, maximumOverlaps);
            var capacity = Mathf.Max(1, maximumColliders);
            if (m_Colliders == null || m_Colliders.Length != capacity) { m_Colliders = new Collider[capacity]; m_Points = new CoverPoint[capacity]; }
            m_Sample = sample; m_Path = path; m_Origin = settings.Actor.position;
            m_Candidates.Clear(); Collider = null; Point = null; Position = Vector3.zero; Normal = Vector3.zero;
            RequiredStance = CoverStance.Standing; SelectedHeight = m_Settings.Height; Priority = 0;
            Failure = CoverFailure.NoGeometry; Score = float.PositiveInfinity;
            m_Index = 0; m_Probe = 0; m_StanceIndex = 0;
            m_Count = settings.Source == CoverSource.AuthoredPoints ? 0 : Physics.OverlapSphereNonAlloc(m_Origin, m_Settings.Distance, m_Colliders, settings.Layers, QueryTriggerInteraction.Ignore);
            m_PointCount = settings.Source == CoverSource.Geometry ? 0 : CoverPoint.Collect(m_Origin, m_Settings.Distance, m_Points);
            Truncated = m_Count == capacity || m_PointCount == capacity;
            IsSearching = true;
        }

        /// <summary>Processes at most the requested number of candidate/stance pairs.</summary>
        /// <param name="budget">The maximum candidates to evaluate in this update.</param>
        /// <returns>True when the search has completed.</returns>
        public bool Step(int budget)
        {
            LastStepCandidates = 0;
            while (IsSearching && LastStepCandidates < Mathf.Max(1, budget)) {
                if (m_Index >= m_PointCount + m_Count) { IsSearching = false; break; }
                ++LastStepCandidates;
                var stance = m_Settings.Stances == CoverStanceMode.Crouching || (m_Settings.Stances == CoverStanceMode.Either && m_StanceIndex == 1) ? CoverStance.Crouching : CoverStance.Standing;
                m_TestHeight = stance == CoverStance.Crouching ? m_Settings.CrouchHeight : m_Settings.Height;
                if (m_Index < m_PointCount) {
                    var point = m_Points[m_Index];
                    if (point != null && point.isActiveAndEnabled && point.Cover != null &&
                        (point.Stances == CoverStanceMode.Either || (int)point.Stances == (int)stance)) {
                        Consider(point.transform.position, point.transform.forward, point.Cover, point, point.Priority, stance);
                    }
                } else {
                    var cover = m_Colliders[m_Index - m_PointCount];
                    if (cover != null && !Ignored(cover)) { ProbeSurface(cover, stance); }
                }
                if (m_Settings.Stances == CoverStanceMode.Either && m_StanceIndex == 0) { m_StanceIndex = 1; }
                else {
                    m_StanceIndex = 0;
                    if (m_Index < m_PointCount || ++m_Probe >= 15) { ++m_Index; m_Probe = 0; }
                }
            }
            if (m_Index >= m_PointCount + m_Count) { IsSearching = false; }
            if (!IsSearching && Collider != null) { Failure = CoverFailure.None; }
            return !IsSearching;
        }

        /// <summary>Runs a complete search synchronously for tools and deterministic unit tests.</summary>
        /// <param name="settings">The search settings.</param>
        /// <param name="maximumColliders">The collider budget.</param>
        /// <param name="maximumOverlaps">The overlap budget.</param>
        /// <param name="sample">The sampler.</param>
        /// <param name="path">The route query.</param>
        /// <returns>True when a usable selection exists.</returns>
        public bool Find(Settings settings, int maximumColliders, int maximumOverlaps, SamplePosition sample, PathDistance path)
        {
            Begin(settings, maximumColliders, maximumOverlaps, sample, path);
            while (!Step(64)) { }
            return HasSelection;
        }

        /// <summary>Samples nearest surfaces, threat-facing surfaces, radial surfaces, and wall ends.</summary>
        /// <param name="cover">The potential cover collider.</param>
        /// <param name="stance">The tested stance.</param>
        private void ProbeSurface(Collider cover, CoverStance stance)
        {
            var center = cover.bounds.center;
            center.y = Mathf.Clamp(m_Origin.y + m_TestHeight, cover.bounds.min.y, cover.bounds.max.y);
            var closest = cover.ClosestPoint(new Vector3(m_Origin.x, center.y, m_Origin.z));
            var direction = m_Probe == 0 ? m_Origin - closest : m_Probe == 1 ? (m_Settings.Directional ? -m_Settings.Threat : center - m_Settings.Threat) :
                Quaternion.Euler(0, (m_Probe - 2) * 40f, 0) * Vector3.forward;
            var anchor = m_Probe == 0 ? closest : center;
            if (m_Probe >= 11) {
                var alongX = cover.bounds.extents.x >= cover.bounds.extents.z;
                var tangent = alongX ? Vector3.right : Vector3.forward;
                var length = alongX ? cover.bounds.extents.x : cover.bounds.extents.z;
                anchor = center + tangent * ((m_Probe % 2 == 0 ? 1 : -1) * Mathf.Max(0, length - m_Settings.Radius));
                direction = alongX ? Vector3.forward : Vector3.right;
                if (m_Probe >= 13) { direction = -direction; }
            }
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) { return; }
            direction.Normalize();
            var reach = cover.bounds.extents.magnitude + Mathf.Max(0f, m_Settings.Offset) + 1;
            if (!cover.Raycast(new Ray(anchor + direction * reach, -direction), out var hit, reach * 2)) { return; }
            var candidate = hit.point + hit.normal * Mathf.Max(m_Settings.Radius + 0.03f, m_Settings.Offset);
            candidate.y = m_Origin.y;
            Consider(candidate, hit.normal, cover, null, 0, stance);
        }

        /// <summary>Validates and scores one candidate without replacing movement.</summary>
        /// <param name="position">The foot position.</param>
        /// <param name="normal">The outward normal.</param>
        /// <param name="cover">The associated collider.</param>
        /// <param name="point">The optional authored point.</param>
        /// <param name="priority">The authored priority.</param>
        /// <param name="stance">The tested stance.</param>
        private void Consider(Vector3 position, Vector3 normal, Collider cover, CoverPoint point, float priority, CoverStance stance)
        {
            CoverFailure reason;
            if (Ignored(cover) || !cover.enabled || !cover.gameObject.activeInHierarchy || (m_Settings.Layers & (1 << cover.gameObject.layer)) == 0 ||
                !m_Sample(ref position) || (position - m_Origin).sqrMagnitude > m_Settings.Distance * m_Settings.Distance) { reason = CoverFailure.Unreachable; }
            else if (!m_Settings.Directional && Vector3.Distance(position, m_Settings.Threat) < Mathf.Max(0, m_Settings.MinimumThreatDistance)) { reason = CoverFailure.TooClose; }
            else if (m_Settings.UseReservations && CoverReservations.IsReserved(m_Settings.Actor.gameObject, position, m_Settings.Radius, point)) { reason = CoverFailure.Reserved; }
            else if ((reason = ValidateHeight(position, m_Settings.Threat, m_Settings.Directional, m_TestHeight)) == CoverFailure.None) {
                if (!m_Path(position, out var distance)) { reason = CoverFailure.Unreachable; }
                else {
                    var score = EvaluateScore(m_Settings, position, distance, priority);
                    if (score < Score) {
                        Score = score; Position = position; Normal = normal.normalized; Collider = cover; Point = point;
                        AuthoredPosition = point != null ? point.transform.position : Vector3.zero;
                        Priority = priority; RequiredStance = stance; SelectedHeight = m_TestHeight;
                    }
                }
            }
            if ((int)reason > (int)Failure) { Failure = reason; }
            if (m_Settings.Debug) { m_Candidates.Add(new Candidate { Position = position, Failure = reason, Height = m_TestHeight }); }
        }

        /// <summary>Computes comparable costs for the selected preference.</summary>
        /// <param name="settings">The selection settings.</param>
        /// <param name="position">The candidate.</param>
        /// <param name="distance">The complete path length.</param>
        /// <param name="priority">The authored priority.</param>
        /// <returns>A lower cost for a more desirable candidate.</returns>
        public static float EvaluateScore(Settings settings, Vector3 position, float distance, float priority)
        {
            if (settings.Selection == CoverSelection.ThreatDistance && !settings.Directional) { return distance - Mathf.Max(0, settings.ThreatDistanceWeight) * Vector3.Distance(position, settings.Threat); }
            if (settings.Selection == CoverSelection.AuthoredPriority) { return distance - Mathf.Max(0, settings.PriorityWeight) * priority; }
            return distance;
        }

        /// <summary>Checks actor and threat hierarchies which must never provide cover.</summary>
        /// <param name="collider">The candidate geometry.</param>
        /// <returns>True for excluded geometry.</returns>
        private bool Ignored(Collider collider)
        {
            return collider == null || collider.transform.IsChildOf(m_Settings.Actor) ||
                (m_Settings.ThreatObject != null && collider.transform.IsChildOf(m_Settings.ThreatObject));
        }

        /// <summary>Revalidates concealment, physical clearance, and occupancy after navigation sampling.</summary>
        /// <param name="position">The actor's foot position.</param>
        /// <param name="threat">The current threat point or direction.</param>
        /// <param name="directional">Whether the threat is directional.</param>
        /// <returns>The first failed validation or None.</returns>
        public CoverFailure Validate(Vector3 position, Vector3 threat, bool directional)
        {
            return ValidateHeight(position, threat, directional, SelectedHeight > 0 ? SelectedHeight : m_Settings.Height);
        }

        /// <summary>Validates a foot position at an explicit body height without searching or navigating.</summary>
        /// <param name="position">The foot position.</param>
        /// <param name="threat">The threat point or direction.</param>
        /// <param name="directional">Whether the threat is directional.</param>
        /// <param name="height">The body height.</param>
        /// <returns>The failed validation or None.</returns>
        public CoverFailure ValidateHeight(Vector3 position, Vector3 threat, bool directional, float height)
        {
            m_Settings.Threat = threat; m_Settings.Directional = directional;
            if (!directional && Vector3.Distance(position, threat) < Mathf.Max(0, m_Settings.MinimumThreatDistance)) { return CoverFailure.TooClose; }
            var center = position + Vector3.up * height;
            var toward = directional ? threat : threat - center;
            var side = Vector3.Cross(Vector3.up, toward).normalized * m_Settings.Radius;
            for (int i = -1; i <= 1; ++i) {
                var end = center + side * i;
                var origin = directional ? end + threat * Mathf.Max(1f, m_Settings.Distance * 2f) : threat;
                var delta = end - origin;
                var hits = Physics.RaycastNonAlloc(origin, delta.normalized, m_SightHits, delta.magnitude, m_Settings.Layers, QueryTriggerInteraction.Ignore);
                var blocked = false;
                for (int hit = 0; hit < hits; ++hit) { if (!Ignored(m_SightHits[hit].collider)) { blocked = true; break; } }
                if (!blocked) { return CoverFailure.Exposed; }
            }
            var radius = m_Settings.Radius * 0.95f;
            var bottom = position + Vector3.up * (m_Settings.Radius + 0.03f);
            var top = position + Vector3.up * Mathf.Max(m_Settings.Radius + 0.03f, height - m_Settings.Radius);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, m_Overlaps, m_Settings.Layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i) {
                if (!m_Overlaps[i].transform.IsChildOf(m_Settings.Actor)) { return CoverFailure.Obstructed; }
            }
            if (count == m_Overlaps.Length) { return CoverFailure.Obstructed; }
            if (m_Settings.OccupancyLayers != 0) {
                count = Physics.OverlapSphereNonAlloc(position + Vector3.up * m_Settings.Radius, Mathf.Max(m_Settings.Radius, m_Settings.OccupancyRadius),
                    m_Overlaps, m_Settings.OccupancyLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; ++i) {
                    if (!m_Overlaps[i].transform.IsChildOf(m_Settings.Actor)) { return CoverFailure.Occupied; }
                }
                if (count == m_Overlaps.Length) { return CoverFailure.Occupied; }
            }
            return CoverFailure.None;
        }

        /// <summary>Draws retained candidates, rejection labels, selected clearance, sight lines, and the active NavMesh path.</summary>
        public void DrawGizmos()
        {
            if (!m_Settings.Debug || m_Settings.Actor == null) { return; }
            var previous = Gizmos.color;
            foreach (var candidate in m_Candidates) {
                Gizmos.color = candidate.Failure == CoverFailure.None ? Color.yellow : Color.red;
                Gizmos.DrawWireSphere(candidate.Position, 0.1f);
#if UNITY_EDITOR
                if (candidate.Failure != CoverFailure.None) { UnityEditor.Handles.Label(candidate.Position, candidate.Failure.ToString()); }
#endif
            }
            if (Collider != null) {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(Position + Vector3.up * m_Settings.Radius, m_Settings.Radius);
                Gizmos.DrawWireSphere(Position + Vector3.up * (SelectedHeight - m_Settings.Radius), m_Settings.Radius);
                Gizmos.DrawLine(Position, Position + Normal);
                var body = Position + Vector3.up * SelectedHeight;
                var toward = m_Settings.Directional ? m_Settings.Threat : m_Settings.Threat - body;
                var side = Vector3.Cross(Vector3.up, toward).normalized * m_Settings.Radius;
                for (int i = -1; i <= 1; ++i) {
                    var end = body + side * i;
                    Gizmos.DrawLine(m_Settings.Directional ? end + toward * m_Settings.Distance * 2f : m_Settings.Threat, end);
                }
            }
            var agent = m_Settings.Actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh && agent.hasPath) {
                Gizmos.color = Color.cyan;
                if (m_DebugPath == null) { m_DebugPath = new UnityEngine.AI.NavMeshPath(); }
                if (agent.CalculatePath(agent.destination, m_DebugPath)) {
                    var count = m_DebugPath.GetCornersNonAlloc(m_DebugCorners);
                    while (count == m_DebugCorners.Length) { System.Array.Resize(ref m_DebugCorners, m_DebugCorners.Length * 2); count = m_DebugPath.GetCornersNonAlloc(m_DebugCorners); }
                    for (int i = 1; i < count; ++i) { Gizmos.DrawLine(m_DebugCorners[i - 1], m_DebugCorners[i]); }
                }
            }
#if UNITY_EDITOR
            if (Truncated) { UnityEditor.Handles.Label(m_Settings.Actor.position, "Cover search budget reached"); }
#endif
            Gizmos.color = previous;
        }
    }
}
#endif