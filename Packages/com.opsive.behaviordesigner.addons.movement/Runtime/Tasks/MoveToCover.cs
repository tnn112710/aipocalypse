/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime;

#if OPSIVE_SHARED_V3
    using OpsiveSharedUtility = Opsive.Shared.Runtime.Utility;
#else
    using OpsiveSharedUtility = Opsive.Shared.Utility;
#endif

    /// <summary>
    /// Finds reachable, clear cover that conceals the actor from the selected threat.
    /// </summary>
    [MovedFrom(false, "Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks", "Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime", "Cover")]
    [OpsiveSharedUtility.Description("Moves towards a 3D cover point. The agent can look at the cover point after the agent has arrived.")]
    [NodeIcon("54759f53c1dc79a489ef7702de195ade", "1565ea2bb117e0643b8943c3dac0de81")]
    public class MoveToCover : MovementAgent
    {
        [Tooltip("The optional threat used to orient the initial cover search. The agent forward direction is used when empty.")]
        [SerializeField] protected SharedVariable<GameObject> m_Threat = new SharedVariable<GameObject>();
        [Tooltip("Specifies the maximum distance away from the agent of a valid cover location.")]
        [SerializeField] protected SharedVariable<float> m_CoverSearchDistance = 20;
        [Tooltip("The offset relative to the agent's transform that the cover search should originate from.")]
        [SerializeField] protected SharedVariable<Vector3> m_CoverSearchOffset = new Vector3(0, 1, 0);
        [Tooltip("The layermask of valid cover locations.")]
        [SerializeField] protected SharedVariable<LayerMask> m_CoverLayers;
        [Tooltip("The maximum number of raycasts that should be invoked before the agent gives up looking for a location to find cover.")]
        [SerializeField, HideInInspector] protected SharedVariable<int> m_MaxRaycasts = 90;
        [Tooltip("The step size betwen raycasts.")]
        [SerializeField, HideInInspector] protected SharedVariable<float> m_RayStep = 4;
        [Tooltip("Specifies the distance away from the cover point that the agent should move to.")]
        [SerializeField] protected SharedVariable<float> m_CoverOffset = 1;
        [Tooltip("Should an OverlapSphere check be performed at the cover location in order to determine if it is occupied?")]
        [SerializeField] protected SharedVariable<bool> m_CheckForOccupency = true;
        [Tooltip("The radius of the OverlapSphere check if checking for occupency.")]
        [SerializeField] protected SharedVariable<float> m_OccupencyOverlapRadius = 0.2f;
        [Tooltip("The layermask of the layers that can cause overlap.")]
        [SerializeField] protected SharedVariable<LayerMask> m_OccupencyLayerMask;
        [Tooltip("The maximum number of occupancy colliders considered by the non-allocating overlap query.")]
        [SerializeField] protected int m_MaxOccupencyObjects = 16;
        [Tooltip("Should the agent look at the cover point after it has arrived?")]
        [SerializeField] protected SharedVariable<bool> m_LookAtCoverPoint;
        [Tooltip("The maximum number of angles the agent can rotate in a single tick (in degrees).")]
        [SerializeField, HideInInspector] protected SharedVariable<float> m_MaxRotationDelta = 1;
        [Tooltip("Specifies the angle when the agent has arrived at the target rotation (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 0.5f;

        [Tooltip("The height above the feet that must be concealed.")]
        [SerializeField] protected SharedVariable<float> m_CoverHeight = 1.5f;
        [Tooltip("The actor half-width used for visibility and physical clearance.")]
        [SerializeField] protected SharedVariable<float> m_CoverRadius = 0.5f;
        [Tooltip("The maximum number of cover colliders considered per search.")]
        [SerializeField] protected int m_MaximumCoverCandidates = 64;
        [Tooltip("The uninterrupted concealed arrival duration before success.")]
        [SerializeField] protected SharedVariable<float> m_StableArrivalDuration = 0.15f;
        [Tooltip("The turn speed in degrees per second. A negative value converts the legacy per-tick setting at 60 ticks per second.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = -1f;
        [Tooltip("Where the threat comes from. Direction points toward the threat in world space.")]
        [SerializeField] protected CoverThreatMode m_ThreatMode = CoverThreatMode.Automatic;
        [Tooltip("The local eye offset on the threat object.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatOffset = Vector3.zero;
        [Tooltip("The world-space threat or last-known eye position.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatPosition = Vector3.zero;
        [Tooltip("The world-space direction toward the threat.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatDirection = Vector3.forward;
        [Tooltip("The interval between concealment and occupancy checks while moving.")]
        [SerializeField] protected SharedVariable<float> m_RecheckInterval = 0.25f;
        [Tooltip("Draw candidates, rejection reasons, clearance, sight lines, and the active route in the Scene view.")]
        [SerializeField] protected bool m_DebugCover;
        [Tooltip("Receives whether the selected cover outputs are currently valid.")]
        [SerializeField] protected SharedVariable<bool> m_HasCover = false;
        [Tooltip("Receives the selected cover foot position.")]
        [SerializeField] protected SharedVariable<Vector3> m_CoverPosition = Vector3.zero;
        [Tooltip("Receives the outward normal of the selected cover surface.")]
        [SerializeField] protected SharedVariable<Vector3> m_CoverNormal = Vector3.zero;
        [Tooltip("Receives the selected cover geometry object.")]
        [SerializeField] protected SharedVariable<GameObject> m_CoverObject = new SharedVariable<GameObject>();
        [Tooltip("Receives the last cover rejection or movement failure. None means no failure.")]
        [SerializeField] protected SharedVariable<CoverFailure> m_CoverFailure = new SharedVariable<CoverFailure>();

        [Tooltip("The permitted standing or crouching cover stances.")]
        [SerializeField] protected CoverStanceMode m_StanceMode = CoverStanceMode.Standing;
        [Tooltip("Infer standing dimensions from the actor controller, capsule, or navigation agent.")]
        [SerializeField] protected bool m_InferDimensions = false;
        [Tooltip("The body height that must be concealed while crouching.")]
        [SerializeField] protected SharedVariable<float> m_CrouchHeight = 0.9f;
        [Tooltip("Use geometry, authored CoverPoint components, or both.")]
        [SerializeField] protected CoverSource m_CoverSource = CoverSource.Geometry;
        [Tooltip("The preference applied after safety and reachability checks.")]
        [SerializeField] protected CoverSelection m_Selection = CoverSelection.ShortestPath;
        [Tooltip("Route cost reduction per unit of distance from a positional threat.")]
        [SerializeField] protected SharedVariable<float> m_ThreatDistanceWeight = 1f;
        [Tooltip("Route cost reduction per unit of authored priority.")]
        [SerializeField] protected SharedVariable<float> m_PriorityWeight = 1f;
        [Tooltip("The minimum allowed distance from a positional threat.")]
        [SerializeField] protected SharedVariable<float> m_MinimumThreatDistance = 0f;
        [Tooltip("Finish on arrival or continue maintaining valid cover.")]
        [SerializeField] protected CoverCompletion m_CompletionMode = CoverCompletion.Arrival;
        [Tooltip("Maximum candidate and stance pairs evaluated per action update.")]
        [SerializeField] protected int m_CandidatesPerUpdate = 32;
        [Tooltip("Maximum consecutive recovery searches before failure.")]
        [SerializeField] protected int m_MaximumRetries = 3;
        [Tooltip("The active-time delay between failed searches.")]
        [SerializeField] protected SharedVariable<float> m_RetryDelay = 0.5f;
        [Tooltip("Interval between better-cover searches in Maintain mode. Zero disables optional replacement.")]
        [SerializeField] protected SharedVariable<float> m_ReselectionInterval = 2f;
        [Tooltip("The minimum cost improvement required to replace still-valid cover.")]
        [SerializeField] protected SharedVariable<float> m_MinimumImprovement = 1f;
        [Tooltip("Exclude other actors' leased cover positions and reserve this selection.")]
        [SerializeField] protected bool m_UseReservations = false;
        [Tooltip("Lease duration in scaled seconds, renewed while running and retained briefly after success.")]
        [SerializeField] protected SharedVariable<float> m_ReservationDuration = 3f;
        [Tooltip("Receives the stance that the character controller should apply.")]
        [SerializeField] protected SharedVariable<CoverStance> m_RequiredStance = new SharedVariable<CoverStance>();
        [Tooltip("Receives the authored cover point, or null for a geometry selection.")]
        [SerializeField] protected SharedVariable<GameObject> m_SelectedCoverPoint = new SharedVariable<GameObject>();

        private readonly CoverController m_Controller = new CoverController();
        private Vector3 m_ResolvedThreat;
        private bool m_DirectionalThreat, m_UseCapturedDirection;
        private float m_ArrivalStartTime;

        /// <summary>
        /// Resolves the current threat while retaining a captured forward direction.
        /// </summary>
        /// <returns>True when the configured source exists.</returns>
        private bool ResolveThreat()
        {
            if (m_UseCapturedDirection) { return true; }
            return CoverSearch.ResolveThreat(m_ThreatMode, m_Threat.Value, m_ThreatOffset.Value, m_ThreatPosition.Value,
                m_ThreatDirection.Value, transform, out m_ResolvedThreat, out m_DirectionalThreat);
        }

        /// <summary>
        /// Builds the current query settings without changing navigation.
        /// </summary>
        /// <returns>The geometry and threat settings.</returns>
        private CoverSearch.Settings Settings()
        {
            return new CoverSearch.Settings { Actor = transform, Threat = m_ResolvedThreat, Directional = m_DirectionalThreat,
                ThreatObject = m_Threat.Value != null ? m_Threat.Value.transform : null,
                Layers = m_CoverLayers.Value, OccupancyLayers = m_CheckForOccupency.Value ? m_OccupencyLayerMask.Value.value : 0, Distance = m_CoverSearchDistance.Value,
                Height = m_CoverHeight.Value, Radius = m_CoverRadius.Value, Offset = m_CoverOffset.Value,
                OccupancyRadius = m_OccupencyOverlapRadius.Value, Debug = m_DebugCover,
                Stances = m_StanceMode, InferDimensions = m_InferDimensions, CrouchHeight = m_CrouchHeight.Value,
                Source = m_CoverSource, Selection = m_Selection, ThreatDistanceWeight = m_ThreatDistanceWeight.Value,
                PriorityWeight = m_PriorityWeight.Value, MinimumThreatDistance = m_MinimumThreatDistance.Value, UseReservations = m_UseReservations };
        }

        /// <summary>
        /// Starts an incremental cover query using the configured provider.
        /// </summary>
        private void BeginCover()
        {
            m_ArrivalStartTime = -1f;
            m_Controller.Start(Settings(), new CoverController.Options { Budget = m_CandidatesPerUpdate, Retries = m_MaximumRetries,
                RetryDelay = m_RetryDelay.Value, RecheckInterval = m_RecheckInterval.Value, ReselectionInterval = m_ReselectionInterval.Value,
                MinimumImprovement = m_MinimumImprovement.Value, LeaseDuration = m_ReservationDuration.Value,
                Maintain = ControlsMovement && m_CompletionMode == CoverCompletion.Maintain }, m_MaximumCoverCandidates, m_MaxOccupencyObjects,
                SamplePosition, TryCoverPath, ActiveElapsedTime());
        }

        /// <summary>
        /// Clears stale outputs after loss, cancellation, or a new request.
        /// </summary>
        /// <param name="failure">The last rejection to publish.</param>
        private void ClearCover(CoverFailure failure)
        {
            m_HasCover.Value = false; m_CoverPosition.Value = Vector3.zero; m_CoverNormal.Value = Vector3.zero;
            m_CoverObject.Value = null; m_SelectedCoverPoint.Value = null; m_RequiredStance.Value = CoverStance.Standing;
            m_CoverFailure.Value = failure;
        }

        /// <summary>
        /// Publishes the selected position, geometry, and required stance.
        /// </summary>
        private void PublishCover()
        {
            var selected = m_Controller.Current;
            m_HasCover.Value = true; m_CoverPosition.Value = selected.Position; m_CoverNormal.Value = selected.Normal;
            m_CoverObject.Value = selected.Collider.gameObject; m_CoverFailure.Value = CoverFailure.None;
            m_RequiredStance.Value = selected.Stance; m_SelectedCoverPoint.Value = selected.Point != null ? selected.Point.gameObject : null;
        }

        /// <summary>
        /// Draws optional query and selection diagnostics.
        /// </summary>
        protected override void OnDrawGizmosSelected() { if (m_DebugCover) { m_Controller.DrawGizmos(); } }

        /// <summary>
        /// Restores the additional selection and maintenance settings.
        /// </summary>
        private void ResetCoverOptions()
        {
            m_StanceMode = CoverStanceMode.Standing; m_InferDimensions = false; m_CrouchHeight = 0.9f;
            m_CoverSource = CoverSource.Geometry; m_Selection = CoverSelection.ShortestPath;
            m_ThreatDistanceWeight = 1f; m_PriorityWeight = 1f; m_MinimumThreatDistance = 0f;
            m_CompletionMode = CoverCompletion.Arrival; m_CandidatesPerUpdate = 32; m_MaximumRetries = 3;
            m_RetryDelay = 0.5f; m_ReselectionInterval = 2f; m_MinimumImprovement = 1f;
            m_UseReservations = false; m_ReservationDuration = 3f;
        }

        private bool m_Started, m_Succeeded;

        /// <summary>
        /// Starts with a resolved threat and a bounded query.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart(); ClearCover(CoverFailure.None); m_Succeeded = false; m_Started = false; m_UseCapturedDirection = false;
            if (!ResolveThreat()) { ClearCover(CoverFailure.InvalidThreat); return; }
            m_UseCapturedDirection = m_ThreatMode == CoverThreatMode.AgentForward || (m_ThreatMode == CoverThreatMode.Automatic && m_Threat.Value == null);
            if (m_Pathfinder == null || !m_Pathfinder.IsValid) { ClearCover(CoverFailure.MovementFailed); return; }
            if (ControlsMovement) { m_Pathfinder.Stop(); }
            BeginCover(); m_Started = true;
        }

        /// <summary>
        /// Measures a complete route without changing the current destination.
        /// </summary>
        /// <param name="position">The candidate.</param>
        /// <param name="distance">The route length.</param>
        /// <returns>True when the provider confirms reachability.</returns>
        private bool TryCoverPath(Vector3 position, out float distance)
        {
            return m_Pathfinder.TryGetPathDistance(transform.position, position, out distance);
        }

        /// <summary>
        /// Advances selection, movement, and optional continuous maintenance.
        /// </summary>
        /// <returns>The current task status.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_Started) { return TaskStatus.Failure; }
            if (!ResolveThreat()) { ClearCover(CoverFailure.InvalidThreat); return TaskStatus.Failure; }
            if (m_Pathfinder == null || !m_Pathfinder.IsValid || (m_UseTimeout.Value && ActiveElapsedTime() >= Mathf.Max(0f, m_Timeout.Value))) {
                ClearCover(CoverFailure.MovementFailed); return TaskStatus.Failure;
            }
            var hadCover = m_Controller.HasCover;
            var status = m_Controller.Tick(Settings(), ActiveElapsedTime());
            if (hadCover && !m_Controller.HasCover && ControlsMovement) { m_Pathfinder.Stop(); }
            if (status != CoverController.Status.Ready) {
                ClearCover(m_Controller.Failure); m_ArrivalStartTime = -1f;
                return status == CoverController.Status.Pending ? TaskStatus.Running : TaskStatus.Failure;
            }
            if (m_Controller.Changed) {
                m_ArrivalStartTime = -1f; ResetMovementValidation();
                if (ControlsMovement && !SetDestination(m_Controller.Current.Position)) { return RecoverCover(CoverFailure.DestinationRejected); }
            }
            PublishCover();
            if (!ControlsMovement) { m_Succeeded = true; return TaskStatus.Success; }
            // A throttled pathfinder can accept a request before applying the destination. Retry until it is applied.
            if ((m_Pathfinder.Destination - m_Controller.Current.Position).sqrMagnitude > 0.0001f) {
                if (!SetDestination(m_Controller.Current.Position)) { return RecoverCover(CoverFailure.DestinationRejected); }
                return TaskStatus.Running;
            }
            if (HasMovementFailed(!HasArrived())) { return RecoverCover(CoverFailure.MovementFailed); }
            if (!HasArrived()) { m_ArrivalStartTime = -1f; return TaskStatus.Running; }
            var failure = m_Controller.ValidateCurrent(transform.position);
            if (failure != CoverFailure.None) { return RecoverCover(failure); }
            if (m_ArrivalStartTime < 0f) { m_ArrivalStartTime = ActiveElapsedTime(); }
            if (ActiveElapsedTime() - m_ArrivalStartTime < Mathf.Max(0f, m_StableArrivalDuration.Value)) { return TaskStatus.Running; }
            m_Controller.MarkStable();
            if (m_LookAtCoverPoint.Value) {
                var direction = -m_Controller.Current.Normal; direction.y = 0;
                if (direction.sqrMagnitude > 0.0001f) {
                    var rotation = Quaternion.LookRotation(direction, Vector3.up);
                    if (Quaternion.Angle(transform.rotation, rotation) > Mathf.Max(0.1f, m_ArrivedAngle.Value)) {
                        var speed = m_RotationSpeed.Value < 0f ? m_MaxRotationDelta.Value * 60f : m_RotationSpeed.Value;
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, Mathf.Max(0f, speed) * Time.deltaTime);
                        return TaskStatus.Running;
                    }
                }
            }
            if (m_CompletionMode == CoverCompletion.Maintain) { return TaskStatus.Running; }
            m_Succeeded = true; return TaskStatus.Success;
        }

        /// <summary>
        /// Stops invalid movement and schedules bounded recovery.
        /// </summary>
        /// <param name="failure">The failed safety or route check.</param>
        /// <returns>Running while recovery is possible.</returns>
        private TaskStatus RecoverCover(CoverFailure failure)
        {
            if (ControlsMovement) { m_Pathfinder.Stop(); }
            m_ArrivalStartTime = -1f; ClearCover(failure);
            return m_Controller.Recover(failure, ActiveElapsedTime()) ? TaskStatus.Running : TaskStatus.Failure;
        }

        /// <summary>
        /// Releases canceled reservations and retains successful outputs.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd(); m_Controller.End(m_Succeeded);
            if (!m_Succeeded) { ClearCover(m_CoverFailure.Value == CoverFailure.None ? CoverFailure.MovementFailed : m_CoverFailure.Value); }
        }

        /// <summary>
        /// Resumes the tree without restoring an abandoned path while a replacement search is pending.
        /// </summary>
        public override void OnBehaviorTreeStarted()
        {
            base.OnBehaviorTreeStarted();
            if (ControlsMovement && m_Started && !m_Controller.HasCover && m_Pathfinder != null && m_Pathfinder.IsValid) { m_Pathfinder.Stop(); }
        }
        /// <summary>
        /// Rebuilds cover after load because geometry and reservations may have changed.
        /// </summary>
        /// <param name="saveData">The saved task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, Unity.Entities.World world, Unity.Entities.Entity entity)
        {
            m_Controller.End(false); OnStart(); base.Load(saveData, world, entity);
            if (ControlsMovement && m_Pathfinder != null && m_Pathfinder.IsValid) { m_Pathfinder.Stop(); }
        }

        /// <summary>
        /// Restores default cover settings and preserves legacy field compatibility.
        /// </summary>
        public override void Reset()
        {
            base.Reset(); ResetCoverOptions();
            m_Threat = new SharedVariable<GameObject>(); m_ThreatMode = CoverThreatMode.Automatic;
            m_ThreatOffset = Vector3.zero; m_ThreatPosition = Vector3.zero; m_ThreatDirection = Vector3.forward;
            m_RecheckInterval = 0.25f; m_DebugCover = false; ClearCover(CoverFailure.None);
            m_CoverSearchDistance = 20; m_CoverSearchOffset = new Vector3(0, 1, 0); m_CoverLayers = new LayerMask();
            m_MaxRaycasts = 90; m_RayStep = 4; m_CoverOffset = 1; m_CheckForOccupency = true;
            m_OccupencyOverlapRadius = 0.2f; m_OccupencyLayerMask = new LayerMask(); m_MaxOccupencyObjects = 16;
            m_LookAtCoverPoint = false; m_MaxRotationDelta = 1; m_RotationSpeed = 90f; m_ArrivedAngle = 0.5f;
            m_CoverHeight = 1.5f; m_CoverRadius = 0.5f; m_MaximumCoverCandidates = 64; m_StableArrivalDuration = 0.15f;
        }
    }
}