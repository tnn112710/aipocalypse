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

#if OPSIVE_SHARED_V3
    using OpsiveSharedUtility = Opsive.Shared.Runtime.Utility;
#else
    using OpsiveSharedUtility = Opsive.Shared.Utility;
#endif

    /// <summary>
    /// Moves away from the target using its predicted position.
    /// </summary>
    [OpsiveSharedUtility.Description("Flee from the target specified. Flee will predict where the target is headed based on the Look Ahead Distance.")]
    [NodeIcon("1ec2f15c8d735df4db1b25bd175908d5", "c3bcfb3ed7a811f42bd18b1316442621")]
    public class Flee : MovementAgent
    {
        [Tooltip("The GameObject that the agent is fleeing from.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The agent has fleed when the magnitude is greater than this value.")]
        [SerializeField] protected SharedVariable<float> m_FleedDistance = 20;
        [Tooltip("The distance to look ahead when fleeing.")]
        [SerializeField] protected SharedVariable<float> m_LookAheadDistance = 5;
        [Tooltip("The minimum time between flee destination replans.")]
        [SerializeField] protected SharedVariable<float> m_ReplanInterval = 0.25f;
        [Tooltip("The uninterrupted safe duration required before the task succeeds.")]
        [SerializeField] protected SharedVariable<float> m_SafeDuration = 0f;

        private bool m_DestinationAccepted;
        private float m_LastReplanTime;
        private float m_SafeStartTime;

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_DestinationAccepted = false;
            if (m_Target.Value == null) {
                Debug.LogError("Error: A target must be set on the Flee ability.");
                return;
            }

            m_SafeStartTime = -1f;
            m_LastReplanTime = Time.time;
            m_DestinationAccepted = SetDestination(GetTargetDestination());
        }

        /// <summary>
        /// Returns the target destination.
        /// </summary>
        /// <returns>The target destination.</returns>
        private Vector3 GetTargetDestination()
        {
            var away = transform.position - m_Target.Value.transform.position;
            if (away.sqrMagnitude < 0.0001f) { away = -m_Target.Value.transform.forward; }
            return transform.position + away.normalized * Mathf.Max(0.1f, m_LookAheadDistance.Value);
        }

        /// <summary>
        /// Updates the flee destination.
        /// </summary>
        /// <returns>Success when the agent has fleed.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null) { return TaskStatus.Failure; }
            if (Vector3.Magnitude(transform.position - m_Target.Value.transform.position) > m_FleedDistance.Value) {
                if (m_SafeStartTime < 0f) { m_SafeStartTime = Time.time; }
                if (Time.time - m_SafeStartTime >= Mathf.Max(0f, m_SafeDuration.Value)) { return TaskStatus.Success; }
            } else {
                m_SafeStartTime = -1f;
            }
            if (!m_DestinationAccepted || HasMovementFailed()) { return TaskStatus.Failure; }

            if (HasArrived() || Time.time - m_LastReplanTime >= Mathf.Max(0f, m_ReplanInterval.Value)) {
                if (!SetDestination(GetTargetDestination())) {
                    return TaskStatus.Failure;
                }
                m_LastReplanTime = Time.time;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Sets the destination. Returns false if the destination isn't valid.
        /// </summary>
        /// <param name="destination">The target destination.</param>
        /// <returns>True if the destination was successfully set.</returns>
        protected override bool SetDestination(Vector3 destination)
        {
            if (!SamplePosition(ref destination)) {
                return false;
            }
            return base.SetDestination(destination);
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Target = null;
            m_FleedDistance = 20;
            m_LookAheadDistance = 5;
            m_ReplanInterval = 0.25f;
            m_SafeDuration = 0f;
        }
    }
}