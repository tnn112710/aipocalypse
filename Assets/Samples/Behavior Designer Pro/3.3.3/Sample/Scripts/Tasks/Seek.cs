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
    /// Uses the NavMeshAgent to seek to the specified target.
    /// This task is basic intended for demo purposes. For a more complete task see the Movement Pack:
    /// https://assetstore.unity.com/packages/slug/310243
    /// </summary>
    [Shared.Utility.Category("Behavior Designer Samples")]
    public class Seek : NavMeshSeekSampleBase
    {
        [Tooltip("The seek destination.")]
        [SerializeField] protected SharedVariable<GameObject> m_Destination;

        /// <summary>
        /// Gets the existing serialized wrapper for the shared sample behavior.
        /// </summary>
        protected sealed override SharedVariable<GameObject> SeekDestination => m_Destination;
    }
}