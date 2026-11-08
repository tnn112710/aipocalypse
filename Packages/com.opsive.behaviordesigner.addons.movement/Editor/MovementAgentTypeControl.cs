/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Editor
{
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks;
    using Opsive.BehaviorDesigner.AddOns.Shared.Editor;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
#if OPSIVE_SHARED_V3
    using Opsive.Shared.Runtime.Utility;
    using Opsive.Shared.Runtime.Serialization;
    using Serialization = Opsive.Shared.Runtime.Serialization.Serialization;
    using MemberVisibility = Opsive.Shared.Runtime.Serialization.MemberVisibility;
#else
    using Opsive.Shared.Utility;
#endif
    using System;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the MovementAgent type.
    /// </summary>
    [ControlType(typeof(MovementAgent))]
    public class MovementAgentTypeControl : PathfinderTypeControlBase
    {
        /// <summary>
        /// The MovementAgentView displays the contents of the MovementAgent.
        /// </summary>
        public class MovementAgentView : PathfinderView
        {
            protected MovementAgent m_MovementAgent;

            /// <summary>
            /// MovementAgentView constructor.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="movementAgent">The MovementAgent task being drawn.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public MovementAgentView(UnityEngine.Object unityObject, MovementAgent movementAgent, Func<object, bool> onChangeEvent) : base(unityObject, onChangeEvent, MovementAgent.PathfinderTypeKey)
            {
                m_MovementAgent = movementAgent;

                InitializePathfinder(
                    () => m_MovementAgent.Pathfinder,
                    (pathfinder) => m_MovementAgent.Pathfinder = pathfinder
                );

                AddTitleLabel(m_MovementAgent.GetType().Name, m_MovementAgent.GetType().FullName);
                AddTaskFields();
            }

            /// <summary>
            /// Adds the remaining task fields.
            /// </summary>
            protected virtual void AddTaskFields()
            {
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddFields(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent?.Invoke(obj); return true; }
                }, MemberVisibility.Public);
#else
                FieldInspectorView.AddFields(m_UnityObject, m_MovementAgent, MemberVisibility.Public, this, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
#endif
                // AddFields already includes the inherited serialized validation settings.
            }

            /// <summary>
            /// Adds the common timeout, path validation, and stuck detection fields.
            /// </summary>
            protected void AddMovementValidationFields()
            {
                var fieldNames = new[] { "m_FailOnInvalidPath", "m_UseTimeout", "m_Timeout", "m_UseStuckDetection",
                    "m_StuckDuration", "m_MinimumProgressDistance", "m_PathValidationGrace" };
                for (int i = 0; i < fieldNames.Length; ++i) {
#if OPSIVE_SHARED_V3
                    InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                        UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                        OnChangeEvent = (object obj) => { m_OnChangeEvent?.Invoke(obj); return true; }
                    }, fieldNames[i]);
#else
                    FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, fieldNames[i], this, (object obj) => {
                        m_OnChangeEvent?.Invoke(obj);
                    });
#endif
                }
            }
        }

        /// <summary>
        /// Returns the control that should be used for the specified ControlType.
        /// </summary>
        /// <param name="input">The input to the control.</param>
        /// <returns>The created control.</returns>
#if OPSIVE_SHARED_V3
        protected override VisualElement GetControl(Opsive.Shared.Editor.UIElements.Controls.ControlInput input)
#else
        protected override VisualElement GetControl(TypeControlInput input)
#endif
        {
            return new MovementAgentView(input.UnityObject, input.Value as MovementAgent, input.OnChangeEvent);
        }
    }
}