/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using UnityEngine;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime;

#if OPSIVE_SHARED_V3
    using OpsiveSharedUtility = Opsive.Shared.Runtime.Utility;
#else
    using OpsiveSharedUtility = Opsive.Shared.Utility;
#endif

    /// <summary>
    /// Tests concealment and body clearance at the current position without querying or changing navigation.
    /// </summary>
    [OpsiveSharedUtility.Category("Movement Pack")]
    public class IsInCover : Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Conditional
    {

        [Tooltip("Where the concealment threat comes from.")]
        [SerializeField] protected CoverThreatMode m_ThreatMode = CoverThreatMode.Target;
        [Tooltip("The threat GameObject.")]
        [SerializeField] protected SharedVariable<GameObject> m_Threat = new SharedVariable<GameObject>();
        [Tooltip("The local eye offset on the threat.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatOffset = Vector3.zero;
        [Tooltip("The world-space threat eye position.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatPosition = Vector3.zero;
        [Tooltip("The world-space direction toward the threat.")]
        [SerializeField] protected SharedVariable<Vector3> m_ThreatDirection = Vector3.forward;
        [Tooltip("The geometry that can conceal the actor.")]
        [SerializeField] protected LayerMask m_CoverLayers = ~0;
        [Tooltip("The layers that prevent occupancy.")]
        [SerializeField] protected LayerMask m_OccupancyLayers;
        [Tooltip("The standing height to conceal.")]
        [SerializeField] protected SharedVariable<float> m_CoverHeight = 1.5f;
        [Tooltip("The actor half-width and capsule clearance radius.")]
        [SerializeField] protected SharedVariable<float> m_CoverRadius = 0.5f;
        [Tooltip("Infer standing dimensions from a controller, capsule, or navigation agent.")]
        [SerializeField] protected bool m_InferDimensions;
        [Tooltip("The allowed cover stances. Either returns the first concealed stance, preferring standing.")]
        [SerializeField] protected CoverStanceMode m_StanceMode;
        [Tooltip("The crouching body height.")]
        [SerializeField] protected SharedVariable<float> m_CrouchHeight = 0.9f;
        [Tooltip("The maximum directional sight-line length.")]
        [SerializeField] protected SharedVariable<float> m_SightDistance = 30f;
        [Tooltip("Maximum overlap colliders. A full buffer conservatively rejects the position.")]
        [SerializeField] protected int m_MaximumOverlapResults = 16;
        [Tooltip("Receives the concealed stance; meaningful only when the condition succeeds.")]
        [SerializeField] protected SharedVariable<CoverStance> m_RequiredStance = new SharedVariable<CoverStance>();
        [Tooltip("Receives the failed validation, or None on success.")]
        [SerializeField] protected SharedVariable<CoverFailure> m_CoverFailure = new SharedVariable<CoverFailure>();
        private readonly CoverSearch m_Validator = new CoverSearch();

        /// <summary>
        /// Checks the actor's current position using the same visibility and clearance rules as cover selection.
        /// </summary>
        /// <returns>True when an allowed stance is concealed and clear.</returns>
        private bool CheckCover()
        {
            m_RequiredStance.Value = CoverStance.Standing;
            if (transform == null || !CoverSearch.ResolveThreat(m_ThreatMode, m_Threat.Value, m_ThreatOffset.Value, m_ThreatPosition.Value,
                m_ThreatDirection.Value, transform, out var threat, out var directional)) {
                m_CoverFailure.Value = CoverFailure.InvalidThreat; return false;
            }
            var settings = CoverSearch.Normalize(new CoverSearch.Settings { Actor = transform, Threat = threat, Directional = directional,
                ThreatObject = m_Threat.Value != null ? m_Threat.Value.transform : null, Layers = m_CoverLayers, OccupancyLayers = m_OccupancyLayers,
                Height = m_CoverHeight.Value, Radius = m_CoverRadius.Value, CrouchHeight = m_CrouchHeight.Value,
                InferDimensions = m_InferDimensions, Distance = Mathf.Max(0.1f, m_SightDistance.Value * 0.5f) });
            m_Validator.Configure(settings, m_MaximumOverlapResults);
            var crouch = m_StanceMode == CoverStanceMode.Crouching;
            var failure = m_Validator.ValidateHeight(transform.position, threat, directional, crouch ? settings.CrouchHeight : settings.Height);
            if (failure != CoverFailure.None && m_StanceMode == CoverStanceMode.Either) {
                crouch = true; failure = m_Validator.ValidateHeight(transform.position, threat, directional, settings.CrouchHeight);
            }
            m_CoverFailure.Value = failure;
            if (failure != CoverFailure.None) { return false; }
            m_RequiredStance.Value = crouch ? CoverStance.Crouching : CoverStance.Standing; return true;
        }

        /// <summary>
        /// Restores condition defaults.
        /// </summary>
        public override void Reset()
        {
            base.Reset(); m_ThreatMode = CoverThreatMode.Target; m_Threat = new SharedVariable<GameObject>();
            m_ThreatOffset = Vector3.zero; m_ThreatPosition = Vector3.zero; m_ThreatDirection = Vector3.forward;
            m_CoverLayers = ~0; m_OccupancyLayers = 0; m_CoverHeight = 1.5f; m_CoverRadius = 0.5f;
            m_InferDimensions = false; m_StanceMode = CoverStanceMode.Standing; m_CrouchHeight = 0.9f;
            m_SightDistance = 30f; m_MaximumOverlapResults = 16;
            m_RequiredStance.Value = CoverStance.Standing; m_CoverFailure.Value = CoverFailure.None;
        }

        /// <summary>
        /// Evaluates current concealment.
        /// </summary>
        /// <returns>The condition result.</returns>
        public override Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus OnUpdate() => CheckCover() ? Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Success : Opsive.BehaviorDesigner.Runtime.Tasks.TaskStatus.Failure;
    }
}