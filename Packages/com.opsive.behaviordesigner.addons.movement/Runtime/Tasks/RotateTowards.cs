/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Rotates towards the target rotation using Quaternion.RotateTowards. The rotation can either be specified by a Transform or rotation. " +
                     "If the Transform is used then the rotation will not be used.")]
    [NodeIcon("b3a51997594f86c4e87b5b7f2044b96d", "8d266206744b431478023101189e616b")]
    public class RotateTowards : Action
    {
        [Tooltip("Is the agent rotating in 2D?")]
        [SerializeField] protected bool m_Use2DPhysics;
        [Tooltip("The maximum number of angles the agent can rotate in a single tick (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_MaxRotationDelta = 1;
        [Tooltip("Specifies the angle when the agent has arrived at the target rotation (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_ArrivedAngle = 0.5f;
        [Tooltip("Should the rotation only affect the Y axis?")]
        [SerializeField] protected SharedVariable<bool> m_OnlyY = true;
        [Tooltip("The GameObject that the agent is rotating towards.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("If target is null then use the target rotation.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetRotation;
        [Tooltip("Should the task fail after a maximum duration?")]
        [SerializeField] protected SharedVariable<bool> m_UseTimeout = false;
        [Tooltip("The maximum rotation duration when timeout is enabled.")]
        [SerializeField] protected SharedVariable<float> m_Timeout = 10f;

        private float m_StartTime;

        /// <summary>
        /// Records timeout state when the task starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_StartTime = Time.time;
        }

        /// <summary>
        /// Rotates towards the target.
        /// </summary>
        /// <returns>Success when the agent has rotated towards the target.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_UseTimeout.Value && Time.time - m_StartTime >= Mathf.Max(0f, m_Timeout.Value)) { return TaskStatus.Failure; }
            var targetRotation = GetTargetRotation();

            // Return a task status of success once the agent is done rotating.
            if (Quaternion.Angle(transform.rotation, targetRotation) <= Mathf.Max(0f, m_ArrivedAngle.Value)) {
                return TaskStatus.Success;
            }

            // Keep rotating towards the target.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Mathf.Max(0f, m_MaxRotationDelta.Value));
            return TaskStatus.Running;
        }

        /// <summary>
        /// Returns the target rotation.
        /// </summary>
        /// <returns>The target rotation.</returns>
        private Quaternion GetTargetRotation()
        {
            if (m_Target.Value == null) {
                return Quaternion.Euler(m_TargetRotation.Value);
            }

            var direction = m_Target.Value.transform.position - transform.position;
            if (m_OnlyY.Value) {
                direction.y = 0;
            }
            if (direction.sqrMagnitude < 0.0001f) { return transform.rotation; }
            if (m_Use2DPhysics) {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return Quaternion.AngleAxis(angle, Vector3.forward);
            }
            return Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Use2DPhysics = false;
            m_MaxRotationDelta = 1;
            m_ArrivedAngle = 0.5f;
            m_OnlyY = true;
            m_Target = null;
            m_TargetRotation = Vector3.zero;
            m_UseTimeout = false;
            m_Timeout = 10f;
        }
    }
}