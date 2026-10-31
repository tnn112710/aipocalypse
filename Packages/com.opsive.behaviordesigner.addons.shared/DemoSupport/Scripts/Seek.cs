/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Demo
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Uses the NavMeshAgent to seek to the specified target.
    /// This task is basic intended for demo purposes. For a more complete task see the Movement Pack:
    /// https://assetstore.unity.com/packages/slug/310243
    /// </summary>
    [Opsive.Shared.Utility.Category("Behavior Designer Add-On Samples")]
    public class Seek : Action
    {
        [Tooltip("The seek destination.")]
        [SerializeField] protected SharedVariable<GameObject> m_Destination;

        private NavMeshAgent m_Agent;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            m_Agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Sets the NavMesh destination.
        /// </summary>
        public override void OnStart()
        {
            m_Agent.isStopped = false;
            m_Agent.SetDestination(m_Destination.Value.transform.position);
        }

        /// <summary>
        /// Returns success when the agent has arrived.
        /// </summary>
        /// <returns>Success when the agent has arrived.</returns>
        public override TaskStatus OnUpdate()
        {
            var destination = m_Destination.Value.transform.position;
            // A moving target can arrive before the cached path updates, or leave after that path completes.
            if ((destination - m_Agent.transform.position).sqrMagnitude <= m_Agent.stoppingDistance * m_Agent.stoppingDistance &&
                !m_Agent.Raycast(destination, out _)) {
                return TaskStatus.Success;
            }

            if (m_Agent.destination != destination) {
                m_Agent.SetDestination(destination);
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public override void OnEnd()
        {
            if (m_Agent.isOnNavMesh) {
                m_Agent.isStopped = true;
            }
        }
    }
}