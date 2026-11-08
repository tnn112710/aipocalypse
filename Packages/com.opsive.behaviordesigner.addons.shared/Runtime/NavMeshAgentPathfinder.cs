/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Implements the Pathfindiner abstract class for the NavMeshAgent pathfinding implementation.
    /// </summary>
    public class NavMeshAgentPathfinder : Pathfinder
    {
        [Tooltip("Should the NavMeshAgent rotation be updated?")]
        [SerializeField] protected SharedVariable<bool> m_UpdateRotation = true;
        [Tooltip("Sets the minimum amount of time in between destination updates. This allows for throttling the number of path searches.")]
        [SerializeField] protected SharedVariable<float> m_DestinationUpdateInterval = 0;
        [Tooltip("Specifies the stopping distance of the NavMeshAgent.")]
        [SerializeField] protected SharedVariable<float> m_StoppingDistance = 0.2f;

        private NavMeshAgent m_NavMeshAgent;
        private NavMeshPath m_CoverPath;
        private Vector3[] m_CoverCorners = new Vector3[32];
        private bool m_StartUpdateRotation;
        private float m_SetDestinationTime;

        public override bool IsValid => m_NavMeshAgent != null && m_NavMeshAgent.isActiveAndEnabled && m_NavMeshAgent.isOnNavMesh;
        public override bool IsPathPending => IsValid && m_NavMeshAgent.pathPending;
        public override bool IsPathInvalid => IsValid && !m_NavMeshAgent.pathPending && m_NavMeshAgent.hasPath && m_NavMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid;
        public override Vector3 Velocity => IsValid ? m_NavMeshAgent.velocity : Vector3.zero;
        public override float RemainingDistance => !IsValid || m_NavMeshAgent.pathPending ? float.PositiveInfinity : m_NavMeshAgent.remainingDistance;
        public override Vector3 Destination => IsValid ? m_NavMeshAgent.destination : Vector3.zero;
        public override float Speed {
            get => IsValid ? m_NavMeshAgent.speed : 0f;
            set { if (IsValid) { m_NavMeshAgent.speed = value; } }
        }

        /// <summary>
        /// Initializes the Pathfinder.
        /// </summary>
        /// <param name="gameObject">The parent GameObject.</param>
        public override void Initialize(GameObject gameObject)
        {
            m_NavMeshAgent = gameObject != null ? gameObject.GetComponent<NavMeshAgent>() : null;
            if (m_NavMeshAgent == null) {
                Debug.LogError($"Error: Unable to find the NavMeshAgent component on the {gameObject} GameObject.");
                return;
            }

            if (m_StoppingDistance.Value <= 0) {
                Debug.LogWarning("Warning: The NavMeshAgent stopping distance is set to 0. This should be a positive value to ensure the agent is able to arrive at their destination.");
                m_StoppingDistance.Value = 0.2f;
            }
        }

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            if (!IsValid) { return; }
            m_NavMeshAgent.isStopped = false;
            m_NavMeshAgent.stoppingDistance = m_StoppingDistance.Value;
            m_StartUpdateRotation = m_NavMeshAgent.updateRotation;
            m_SetDestinationTime = -m_DestinationUpdateInterval.Value;
            UpdateRotation(m_UpdateRotation.Value);
        }

        /// <summary>
        /// Specifies if the rotation should be updated.
        /// </summary>
        /// <param name="update">Should the rotation be updated?</param>
        private void UpdateRotation(bool update)
        {
            if (m_NavMeshAgent == null) { return; }
            m_NavMeshAgent.updateRotation = update;
            m_NavMeshAgent.updateUpAxis = update;
        }

        /// <summary>
        /// Set a new pathfinding destination.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True if the destination is valid.</returns>
        public override bool SetDesination(Vector3 destination)
        {
            if (!IsValid) { return false; }
            m_NavMeshAgent.isStopped = false;
            if (m_NavMeshAgent.destination == destination) {
                return true;
            }

            // Prevent the destination from being set too often.
            if (m_SetDestinationTime + m_DestinationUpdateInterval.Value > Time.time) {
                return true;
            }

            m_SetDestinationTime = Time.time;
            return m_NavMeshAgent.SetDestination(destination);
        }

        /// <summary>
        /// Does the agent have a pathfinding path?
        /// </summary>
        /// <returns>True if the agent has a pathfinding path.</returns>
        public override bool HasPath()
        {
            return IsValid && m_NavMeshAgent.hasPath && RemainingDistance > m_NavMeshAgent.stoppingDistance && !m_NavMeshAgent.isStopped;
        }

        /// <summary>
        /// Returns true if the position is a valid pathfinding position.
        /// </summary>
        /// <param name="position">The position to sample. The position will be updated to the valid sampled position.</param>
        /// <returns>True if the position is a valid pathfinding position.</returns>
        public override bool SamplePosition(ref Vector3 position)
        {
            if (!IsValid) { return false; }
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, m_NavMeshAgent.height * 2, new NavMeshQueryFilter { agentTypeID = m_NavMeshAgent.agentTypeID, areaMask = m_NavMeshAgent.areaMask })) {
                position = hit.position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Projects a formation slot to the reachable end of its NavMesh route, avoiding disconnected obstacle tops.
        /// </summary>
        /// <param name="position">The requested slot, updated to the reachable navigation position on success.</param>
        /// <returns>True when the route ends within the navigation sampling distance of the requested slot.</returns>
        public override bool SampleReachablePosition(ref Vector3 position)
        {
            if (!IsValid) { return false; }
            if (m_CoverPath == null) { m_CoverPath = new NavMeshPath(); }
            if (!m_NavMeshAgent.CalculatePath(position, m_CoverPath) || m_CoverPath.status == NavMeshPathStatus.PathInvalid) {
                // A slot deep inside geometry may be outside CalculatePath's endpoint projection range.
                // Sample a nearby surface first, then use the route's reachable endpoint rather than that surface.
                var sampledPosition = position;
                if (!SamplePosition(ref sampledPosition) || !m_NavMeshAgent.CalculatePath(sampledPosition, m_CoverPath) ||
                    m_CoverPath.status == NavMeshPathStatus.PathInvalid) { return false; }
            }
            var count = m_CoverPath.GetCornersNonAlloc(m_CoverCorners);
            while (count == m_CoverCorners.Length) {
                System.Array.Resize(ref m_CoverCorners, m_CoverCorners.Length * 2);
                count = m_CoverPath.GetCornersNonAlloc(m_CoverCorners);
            }
            if (count == 0) { return false; }
            var endpoint = m_CoverCorners[count - 1];
            var sampleDistance = m_NavMeshAgent.height * 2;
            if ((endpoint - position).sqrMagnitude > sampleDistance * sampleDistance) { return false; }
            position = endpoint;
            return true;
        }

        /// <summary>
        /// Measures a complete NavMesh route without changing movement.
        /// </summary>
        /// <param name="start">The actor position.</param>
        /// <param name="destination">The candidate destination.</param>
        /// <param name="distance">The route length.</param>
        /// <returns>True when a complete path exists.</returns>
        public override bool TryGetPathDistance(Vector3 start, Vector3 destination, out float distance)
        {
            distance = 0f;
            if (m_CoverPath == null) { m_CoverPath = new NavMeshPath(); }
            if (!IsValid || !m_NavMeshAgent.CalculatePath(destination, m_CoverPath) || m_CoverPath.status != NavMeshPathStatus.PathComplete) { return false; }
            var count = m_CoverPath.GetCornersNonAlloc(m_CoverCorners);
            while (count == m_CoverCorners.Length) { System.Array.Resize(ref m_CoverCorners, m_CoverCorners.Length * 2); count = m_CoverPath.GetCornersNonAlloc(m_CoverCorners); }
            for (int i = 1; i < count; ++i) { distance += Vector3.Distance(m_CoverCorners[i - 1], m_CoverCorners[i]); }
            return true;
        }

        /// <summary>
        /// Has the agent arrived at the destination?
        /// </summary>
        /// <returns>True if the agent has arrived at the destination.</returns>
        public override bool HasArrived()
        {
            if (!IsValid) { return false; }
            // The path hasn't been computed yet if the path is pending.
            float remainingDistance;
            if (m_NavMeshAgent.pathPending) {
                remainingDistance = float.PositiveInfinity;
            } else {
                remainingDistance = m_NavMeshAgent.remainingDistance;
            }

            return m_NavMeshAgent.pathStatus == NavMeshPathStatus.PathComplete && remainingDistance <= m_NavMeshAgent.stoppingDistance;
        }

        /// <summary>
        /// The agent should stop moving.
        /// </summary>
        public override void Stop()
        {
            if (m_NavMeshAgent != null && m_NavMeshAgent.isActiveAndEnabled) {
                m_NavMeshAgent.isStopped = true;
            }
        }

        /// <summary>
        /// Resumes the NavMeshAgent after a behavior tree pause.
        /// </summary>
        public override void Resume()
        {
            if (IsValid) { m_NavMeshAgent.isStopped = false; }
        }

        /// <summary>
        /// The task has stopped.
        /// </summary>
        public override void OnEnd()
        {
            if (m_NavMeshAgent != null) { UpdateRotation(m_StartUpdateRotation); }
        }

        /// <summary>
        /// Resets the values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_UpdateRotation = true;
            m_DestinationUpdateInterval = 0f;
            m_StoppingDistance = 0.2f;
        }
    }
}