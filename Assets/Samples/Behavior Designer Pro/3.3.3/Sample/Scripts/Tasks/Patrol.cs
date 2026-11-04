/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Uses the NavMeshAgent to patrol through a set of waypoints.
    /// This task is basic intended for demo purposes. For a more complete task see the Movement Pack:
    /// https://assetstore.unity.com/packages/slug/310243
    /// </summary>
    [Shared.Utility.Category("Behavior Designer Samples")]
    public class Patrol : NavMeshPatrolSampleBase
    {
        [Tooltip("The patrol waypoints.")]
        [SerializeField] protected SharedVariable<GameObject[]> m_Waypoints;
        [Tooltip("Should the starting waypoint be randomized?")]
        [SerializeField] protected SharedVariable<bool> m_RandomStart;

        /// <summary>
        /// Gets the existing serialized wrapper for the shared sample behavior.
        /// </summary>
        protected sealed override SharedVariable<GameObject[]> PatrolWaypoints => m_Waypoints;

        /// <summary>
        /// Gets the existing serialized wrapper for the shared sample behavior.
        /// </summary>
        protected sealed override SharedVariable<bool> PatrolRandomStart => m_RandomStart;
    }
}