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
    /// Pursues a moving target using bounded interception and target-based arrival.
    /// </summary>
    [OpsiveSharedUtility.Description("Pursues the specified target. Pursue will predict where the target is headed based on the Target Distance Prediction value.")]
    [NodeIcon("079b135d5d495e14abd9ad2cb0dfaf9e", "c0515b66e6314734ba5a8f2ec7d6f451")]
    public class Pursue : MovementAgent
    {
        [Tooltip("The GameObject that the agent is seeking.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The maximum prediction horizon in seconds. The prediction never exceeds the earliest possible intercept.")]
        [SerializeField] protected SharedVariable<float> m_DistancePrediction = 20;
        [Tooltip("Scales the agent travel time used to limit prediction. Prediction remains bounded by the earliest possible intercept.")]
        [SerializeField] protected SharedVariable<float> m_DistancePredictionMultiplier = 20;
        [Tooltip("The minimum time between pursuit destination replans.")]
        [SerializeField] protected SharedVariable<float> m_ReplanInterval = 0.1f;
        [Tooltip("The destination displacement required before replacing the current path.")]
        [SerializeField] protected SharedVariable<float> m_ReplanDistance = 0.1f;

        [Tooltip("The distance from the actual target at which pursuit succeeds.")]
        [SerializeField] protected SharedVariable<float> m_ArrivalDistance = 0.5f;

        private Vector3 m_TargetPosition;
        private Vector3 m_LastDestination;
        private Rigidbody m_TargetRigidbody;
        private float m_LastTargetSampleTime;
        private float m_LastReplanTime;
        private bool m_DestinationAccepted;

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_DestinationAccepted = false;
            if (m_Target.Value == null) {
                Debug.LogError("Error: A target must be set on the Pursue ability.");
                return;
            }

            m_TargetPosition = m_Target.Value.transform.position;
            m_TargetRigidbody = m_Target.Value.GetComponent<Rigidbody>();
            m_LastTargetSampleTime = Time.time;
            m_LastReplanTime = Time.time;
            m_DestinationAccepted = TryGetTargetDestination(out m_LastDestination) && SetDestination(m_LastDestination);
        }

        /// <summary>
        /// Returns the target destination.
        /// </summary>
        /// <returns>The target destination.</returns>
        private Vector3 GetTargetDestination()
        {
            // Predict with a time-based target velocity so behavior is independent of the behavior-tree tick rate.
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
            var offset = m_TargetPosition - transform.position;
            // Use configured travel speed while accelerating instead of projecting arbitrarily far ahead while stationary.
            var speed = Mathf.Max(0.01f, Mathf.Max(m_Pathfinder.Speed, m_Pathfinder.Velocity.magnitude));
            var maximumPrediction = Mathf.Min(Mathf.Max(0f, m_DistancePrediction.Value),
                offset.magnitude / speed * Mathf.Max(0f, m_DistancePredictionMultiplier.Value));
            return m_TargetPosition + targetVelocity * InterceptTime(offset, targetVelocity, speed, maximumPrediction);
        }

        /// <summary>
        /// Samples the predicted intercept, using the current target when prediction leaves navigation.
        /// </summary>
        /// <param name="destination">The navigable pursuit destination.</param>
        /// <returns>True when either the intercept or current target can be sampled.</returns>
        private bool TryGetTargetDestination(out Vector3 destination)
        {
            destination = GetTargetDestination();
            if (SamplePosition(ref destination)) { return true; }
            destination = m_Target.Value.transform.position;
            return SamplePosition(ref destination);
        }

        /// <summary>
        /// Finds the earliest possible intercept within the configured prediction horizon.
        /// </summary>
        /// <param name="offset">The target position relative to the agent.</param>
        /// <param name="velocity">The target velocity.</param>
        /// <param name="speed">The agent travel speed.</param>
        /// <param name="maximum">The maximum prediction time.</param>
        /// <returns>The bounded intercept time, or the horizon when interception is impossible.</returns>
        private static float InterceptTime(Vector3 offset, Vector3 velocity, float speed, float maximum)
        {
            var a = velocity.sqrMagnitude - speed * speed;
            var b = 2f * Vector3.Dot(offset, velocity);
            var c = offset.sqrMagnitude;
            if (c < 0.0001f) { return 0f; }
            if (Mathf.Abs(a) < 0.0001f) { return b < -0.0001f ? Mathf.Min(maximum, -c / b) : maximum; }
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) { return maximum; }
            var root = Mathf.Sqrt(discriminant);
            var first = (-b - root) / (2f * a);
            var second = (-b + root) / (2f * a);
            var time = first >= 0f ? first : float.PositiveInfinity;
            if (second >= 0f) { time = Mathf.Min(time, second); }
            return Mathf.Min(maximum, time);
        }

        /// <summary>
        /// Updates the destination.
        /// </summary>
        /// <returns>Success when the agent arrives.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null || !m_DestinationAccepted) {
                return TaskStatus.Failure;
            }

            var arrivalDistance = Mathf.Max(0f, m_ArrivalDistance.Value);
            if ((m_Target.Value.transform.position - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance) {
                return TaskStatus.Success;
            }
            if (HasMovementFailed()) { return TaskStatus.Failure; }

            var reachedIntercept = HasArrived();
            if (reachedIntercept || Time.time - m_LastReplanTime >= Mathf.Max(0f, m_ReplanInterval.Value)) {
                if (!TryGetTargetDestination(out var destination)) { return TaskStatus.Failure; }
                if (reachedIntercept || (destination - m_LastDestination).sqrMagnitude >= m_ReplanDistance.Value * m_ReplanDistance.Value) {
                    if (!SetDestination(destination)) { return TaskStatus.Failure; }
                    m_LastDestination = destination;
                }
                m_LastReplanTime = Time.time;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Target = null;
            m_DistancePrediction = 20;
            m_DistancePredictionMultiplier = 20;
            m_ReplanInterval = 0.1f;
            m_ReplanDistance = 0.1f;
            m_ArrivalDistance = 0.5f;
        }
    }
}