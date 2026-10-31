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

    [Opsive.Shared.Utility.Description("Evade the specified target. Evade will predict where the target is headed based on the Look Ahead Distance. " +
                     "Evade is similar to Flee except Evade uses the target's velocity to predict where to move towards whereas Flee only uses the target's position.")]
    [NodeIcon("8e0f6d1ef072a8d468943f37b3453bd9", "cfa59bcf26297d44e8b774d0568c996c")]
    public class Evade : MovementAgent
    {
        [Tooltip("The GameObject that the agent is evading from.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The agent has evaded when the magnitude is greater than this value")]
        [SerializeField] protected SharedVariable<float> m_EvadeDistance = 10;
        [Tooltip("The distance to look ahead when evading")]
        [SerializeField] protected SharedVariable<float> m_LookAheadDistance = 5;
        [Tooltip("Specifies how far to predict the distance ahead of the target. Lower values indicate that less distance should be predicated.")]
        [SerializeField] protected SharedVariable<float> m_DistancePrediction = 20;
        [Tooltip("Specifies the multiplier for predicting the look ahead distance.")]
        [SerializeField] protected SharedVariable<float> m_DistancePredictionMultiplier = 20;
        [Tooltip("The maximum number of iterations used to find a valid evade position.")]
        [SerializeField] [Range(0, 100)] protected int m_MaxInterations = 1;
        [Tooltip("The minimum time between evade destination replans.")]
        [SerializeField] protected SharedVariable<float> m_ReplanInterval = 0.1f;
        [Tooltip("The uninterrupted safe duration required before the task succeeds.")]
        [SerializeField] protected SharedVariable<float> m_SafeDuration = 0f;

        private Vector3 m_TargetPosition;
        private Rigidbody m_TargetRigidbody;
        private float m_LastTargetSampleTime;
        private float m_LastReplanTime;
        private float m_SafeStartTime;
        private bool m_DestinationAccepted;

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_DestinationAccepted = false;
            if (m_MaxInterations <= 0) {
                Debug.LogWarning("Warning: Max iterations must be greater than 0.");
                m_MaxInterations = 1;
            }

            if (m_Target.Value == null) {
                Debug.LogError("Error: A target must be set on the Evade ability.");
                return;
            }

            m_TargetPosition = m_Target.Value.transform.position;
            m_TargetRigidbody = m_Target.Value.GetComponent<Rigidbody>();
            m_LastTargetSampleTime = Time.time;
            m_LastReplanTime = Time.time;
            m_SafeStartTime = -1f;
            m_DestinationAccepted = SetTargetDestination();
        }

        /// <summary>
        /// Updates the flee destination.
        /// </summary>
        /// <returns>Success when the agent has fleed.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null) { return TaskStatus.Failure; }
            if (Vector3.Magnitude(transform.position - m_Target.Value.transform.position) > m_EvadeDistance.Value) {
                if (m_SafeStartTime < 0f) { m_SafeStartTime = Time.time; }
                if (Time.time - m_SafeStartTime >= Mathf.Max(0f, m_SafeDuration.Value)) { return TaskStatus.Success; }
            } else {
                m_SafeStartTime = -1f;
            }
            if (!m_DestinationAccepted || HasMovementFailed()) { return TaskStatus.Failure; }

            if (HasArrived() || Time.time - m_LastReplanTime >= Mathf.Max(0f, m_ReplanInterval.Value)) {
                m_DestinationAccepted = SetTargetDestination();
                m_LastReplanTime = Time.time;
                if (!m_DestinationAccepted) { return TaskStatus.Failure; }
            }
            return TaskStatus.Running;
        }

        /// <summary>
        /// Sets the target destination.
        /// </summary>
        /// <returns>True if the target destination is valid.</returns>
        private bool SetTargetDestination()
        {
            var predictedDestination = GetPredictedDestination();
            var away = transform.position - predictedDestination;
            if (away.sqrMagnitude < 0.0001f) { away = -m_Target.Value.transform.forward; }
            for (int iteration = 0; iteration < m_MaxInterations; ++iteration) {
                var scale = (m_MaxInterations - iteration) / (float)m_MaxInterations;
                var targetDestination = transform.position + away.normalized * Mathf.Max(0.1f, m_LookAheadDistance.Value) * scale;
                if (SetDestination(targetDestination)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// Returns the predicted target destination.
        /// </summary>
        /// <returns>The predicted target destination.</returns>
        private Vector3 GetPredictedDestination()
        {
            // Calculate the current distance to the target and the current speed.
            var distance = (m_Target.Value.transform.position - transform.position).magnitude;
            var velocityMagnitude = m_Pathfinder.Velocity.magnitude;

            var maximumPrediction = Mathf.Max(0f, m_DistancePrediction.Value);
            var futurePrediction = velocityMagnitude <= 0.01f ? maximumPrediction :
                Mathf.Min(maximumPrediction, (distance / velocityMagnitude) * Mathf.Max(0f, m_DistancePredictionMultiplier.Value));

            // Predict the future with a time-based target velocity so behavior is independent of the behavior-tree tick rate.
            var now = Time.time;
            Vector3 targetVelocity;
#if UNITY_6000_0_OR_NEWER
            targetVelocity = m_TargetRigidbody != null ? m_TargetRigidbody.linearVelocity : Vector3.zero;
#else
            targetVelocity = m_TargetRigidbody != null ? m_TargetRigidbody.velocity : Vector3.zero;
#endif
            if (m_TargetRigidbody == null) {
                var deltaTime = now - m_LastTargetSampleTime;
                targetVelocity = deltaTime > 0.0001f ? (m_Target.Value.transform.position - m_TargetPosition) / deltaTime : Vector3.zero;
            }
            m_TargetPosition = m_Target.Value.transform.position;
            m_LastTargetSampleTime = now;
            return m_TargetPosition + targetVelocity * futurePrediction;
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
            m_EvadeDistance = 10;
            m_LookAheadDistance = 5;
            m_DistancePrediction = 20;
            m_DistancePredictionMultiplier = 20;
            m_MaxInterations = 1;
            m_ReplanInterval = 0.1f;
            m_SafeDuration = 0f;
        }
    }
}