/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Editor
{
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements.Controls;
    using System;

#pragma warning disable CS0618
    /// <summary>
    /// Compatibility alias for the renamed MovementAgentTypeControl.
    /// </summary>
    [Obsolete("MovementBaseTypeControl has been renamed to MovementAgentTypeControl. Derive from MovementAgentTypeControl instead.")]
    [ControlType(typeof(MovementBase))]
    public class MovementBaseTypeControl : MovementAgentTypeControl
    {
        /// <summary>
        /// Compatibility alias for the renamed MovementAgentView.
        /// </summary>
        [Obsolete("MovementBaseView has been renamed to MovementAgentView. Derive from MovementAgentView instead.")]
        public class MovementBaseView : MovementAgentView
        {
            /// <summary>
            /// Initializes the compatibility view.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="movementBase">The movement task being drawn.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes.</param>
            public MovementBaseView(UnityEngine.Object unityObject, MovementBase movementBase, Func<object, bool> onChangeEvent) : base(unityObject, movementBase, onChangeEvent)
            {
            }
        }
    }
#pragma warning restore CS0618
}