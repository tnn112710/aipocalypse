/// ---------------------------------------------
/// Senses Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.SensesPack.Runtime.Sensors
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Base class for all sensors in the Senses Pack. Provides common functionality for detecting and processing sensory information.
    /// </summary>
    public abstract class Sensor
    {
        [Tooltip("Should debug gizmos be drawn in the Scene view?")]
        [SerializeField] protected SharedVariable<bool> m_DrawDebugGizmos = true;

        /// <summary>
        /// The GameObject that this sensor is attached to.
        /// </summary>
        protected GameObject m_GameObject;

        /// <summary>
        /// The Transform component of the GameObject that this sensor is attached to.
        /// </summary>
        protected Transform m_Transform;

        /// <summary>
        /// Should debug gizmos be drawn in the Scene view?
        /// </summary>
        public bool DrawDebugGizmos { get { return m_DrawDebugGizmos == null || m_DrawDebugGizmos.Value; } }

        /// <summary>
        /// Initializes the sensor with the specified GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject that the sensor is attached to.</param>
        public virtual void Initialize(GameObject gameObject)
        {
            m_GameObject = gameObject;
            m_Transform = gameObject.transform;
        }

        /// <summary>
        /// Draws gizmos to visualize the sensor's detection area or range.
        /// </summary>
        /// <param name="transform">The transform of the agent that the sensor is attached to.</param>
        public virtual void OnDrawGizmos(Transform transform) { }
    }
}