/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

#if OPSIVE_SHARED_V3
    using OpsiveSharedUtility = Opsive.Shared.Runtime.Utility;
#else
    using OpsiveSharedUtility = Opsive.Shared.Utility;
#endif

    /// <summary>
    /// Maintains a follow distance while matching the moving target's speed.
    /// </summary>
    [OpsiveSharedUtility.Description("Follows the specified target from a distance.")]
    [NodeIcon("0221132e8633eb840a68588d030942d2", "12095e7f978bf294c915462d18637030")]
    public class Follow : MovementAgent
    {
        [Tooltip("The GameObject that the agent is following.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("Start moving towards the target if the target is further than the specified distance")]
        [SerializeField] protected SharedVariable<RangeFloat> m_FollowRange = new RangeFloat(1, 3);
        [Tooltip("The distance hysteresis that prevents rapid switching between approach, stop, and retreat.")]
        [SerializeField] protected SharedVariable<float> m_DistanceHysteresis = 0f;
        [Tooltip("Should the agent rotate toward the target while following?")]
        [SerializeField] protected SharedVariable<bool> m_FaceTarget = false;
        [Tooltip("The maximum facing rotation speed in degrees per second.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 360f;

        private bool m_TargetValid;
        private float m_OriginalSpeed;
        private bool m_SpeedCaptured;
        private Transform m_SampledTarget;
        private Vector3 m_LastTargetPosition;
        private float m_LastTargetTime;
        private float m_TargetSpeed;

        /// <summary>
        /// Returns the active time used to measure target motion.
        /// </summary>
        /// <returns>The active task time.</returns>
        protected virtual float GetFollowTime() => ActiveElapsedTime();

        /// <summary>
        /// Starts a fresh motion sample without including paused movement.
        /// </summary>
        private void ResetTargetMotion()
        {
            m_SampledTarget = m_Target.Value != null ? m_Target.Value.transform : null;
            m_LastTargetPosition = m_SampledTarget != null ? m_SampledTarget.position : Vector3.zero;
            m_LastTargetTime = GetFollowTime();
            m_TargetSpeed = 0f;
        }

        /// <summary>
        /// Measures target speed independently of its movement component.
        /// </summary>
        private void UpdateTargetMotion()
        {
            if (m_SampledTarget != m_Target.Value.transform) { ResetTargetMotion(); return; }
            var time = GetFollowTime();
            var elapsed = time - m_LastTargetTime;
            if (elapsed <= 0f) { return; }
            m_TargetSpeed = Vector3.Distance(m_SampledTarget.position, m_LastTargetPosition) / elapsed;
            m_LastTargetPosition = m_SampledTarget.position;
            m_LastTargetTime = time;
        }

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_SpeedCaptured = m_Pathfinder != null && m_Pathfinder.IsValid;
            if (m_SpeedCaptured) { m_OriginalSpeed = m_Pathfinder.Speed; }
            ResetTargetMotion();
            m_TargetValid = m_Target.Value != null;
            if (!m_TargetValid) {
                Debug.LogError("Error: A target must be set on the Follow ability.");
            }
        }

        /// <summary>
        /// Follows the target.
        /// </summary>
        /// <returns>Always returns running as the agent should continue to follow the target.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_TargetValid || m_Target.Value == null || HasMovementFailed(false)) {
                return TaskStatus.Failure;
            }

            UpdateTargetMotion();
            var targetDirection = m_Target.Value.transform.position - transform.position;
            var distance = targetDirection.magnitude;
            var hysteresis = Mathf.Max(0f, m_DistanceHysteresis.Value);
            if (distance > m_FollowRange.Value.Max + hysteresis) {
                m_Pathfinder.Speed = m_OriginalSpeed;
                // Move towards the target if the agent is too far away.
                if (!SetDestination(m_Target.Value.transform.position)) { return TaskStatus.Failure; }
            } else if (distance < Mathf.Max(0f, m_FollowRange.Value.Min - hysteresis)) {
                m_Pathfinder.Speed = m_OriginalSpeed;
                // Move away from the target if the agent is too close.
                var away = transform.position - m_Target.Value.transform.position;
                if (away.sqrMagnitude < 0.0001f) { away = -m_Target.Value.transform.forward; }
                if (!SetDestination(m_Target.Value.transform.position + away.normalized * m_FollowRange.Value.Min)) { return TaskStatus.Failure; }
            } else {
                var speed = Mathf.Min(Mathf.Max(0f, m_OriginalSpeed), m_TargetSpeed);
                m_Pathfinder.Speed = speed;
                if (speed > 0.01f) {
                    // Keep enough path ahead to avoid braking at the edge of the follow range.
                    if (!SetDestination(m_SampledTarget.position)) { return TaskStatus.Failure; }
                } else {
                    m_Pathfinder.Stop();
                    ResetMovementValidation();
                }
            }

            if (m_FaceTarget.Value && targetDirection.sqrMagnitude > 0.0001f) {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(targetDirection),
                    Mathf.Max(0f, m_RotationSpeed.Value) * Time.deltaTime);
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resumes target sampling without counting movement during the pause.
        /// </summary>
        public override void OnBehaviorTreeStarted()
        {
            base.OnBehaviorTreeStarted();
            ResetTargetMotion();
        }

        /// <summary>
        /// Restores the configured movement speed for subsequent tasks.
        /// </summary>
        public override void OnEnd()
        {
            if (m_SpeedCaptured) { m_Pathfinder.Speed = m_OriginalSpeed; m_SpeedCaptured = false; }
            base.OnEnd();
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Target = null;
            m_FollowRange = new RangeFloat(1, 3);
            m_DistanceHysteresis = 0f;
            m_FaceTarget = false;
            m_RotationSpeed = 360f;
        }
    }
}