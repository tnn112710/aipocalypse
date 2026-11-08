/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Editor
{
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using System;
    using UnityEngine.UIElements;

    /// <summary>
    /// Implements TypeControlBase for the Wander type.
    /// </summary>
    [ControlType(typeof(Wander))]
    public class WanderTypeControl : MovementAgentTypeControl
    {
        /// <summary>
        /// The WanderView displays the contents of the Wander task.
        /// </summary>
        private class WanderView : MovementAgentView
        {
            /// <summary>
            /// WanderView constructor.
            /// </summary>
            /// <param name="unityObject">A reference to the owning Unity Object.</param>
            /// <param name="movementAgent">The MovementAgent task being drawn.</param>
            /// <param name="onChangeEvent">An event that is sent when the value changes. Returns false if the control cannot be changed.</param>
            public WanderView(UnityEngine.Object unityObject, MovementAgent movementAgent, Func<object, bool> onChangeEvent) : base(unityObject, movementAgent, onChangeEvent) { }

            /// <summary>
            /// Adds the remaining task fields.
            /// </summary>
            protected override void AddTaskFields()
            {
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_StopOnTaskEnd");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_StopOnTaskEnd", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
                AddMovementValidationFields();
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_Is2D");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_Is2D", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
                var boundsContainer = new VisualElement();
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj);
                    UpdateBounds(boundsContainer); return true; }
                }, "m_Bounds");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_Bounds", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                    UpdateBounds(boundsContainer);
                });
#endif

                Add(boundsContainer);
                UpdateBounds(boundsContainer);

#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_WanderDistance");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_WanderDistance", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_WanderDegrees");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_WanderDegrees", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_WanderDistanceDecrease");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_WanderDistanceDecrease", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_WanderDegreesIncrease");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_WanderDegreesIncrease", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_WaitAtDestinationDuration");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_WaitAtDestinationDuration", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_DestinationSelectionDistance");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_DestinationSelectionDistance", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_DestinationRetries");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_DestinationRetries", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_RequiredDestinations");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_RequiredDestinations", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_DrawInvalidDestinationRay");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_DrawInvalidDestinationRay", this, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
            }

            /// <summary>
            /// Updates the bounds fields.
            /// </summary>
            /// <param name="container">The parent container.</param>
            private void UpdateBounds(VisualElement container)
            {
                container.Clear();

                var wanderTask = m_MovementAgent as Wander;
                if (wanderTask.Bounds == null || wanderTask.Bounds.Value == Wander.WanderBounds.None) {
                    return;
                }

#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = container,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                }, "m_BoundsCenter");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_BoundsCenter", container, (object obj) =>
                {
                    m_OnChangeEvent(obj);
                });
#endif
                if (wanderTask.Bounds.Value == Wander.WanderBounds.Sphere) {
#if OPSIVE_SHARED_V3
                    InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                        UnityObject = m_UnityObject, Target = m_MovementAgent, Container = container,
                        OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                    }, "m_BoundsRadius");
#else
                    FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_BoundsRadius", container, (object obj) =>
                    {
                        m_OnChangeEvent(obj);
                    });
#endif
                }
                if (wanderTask.Bounds.Value == Wander.WanderBounds.Box) {
#if OPSIVE_SHARED_V3
                    InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                        UnityObject = m_UnityObject, Target = m_MovementAgent, Container = container,
                        OnChangeEvent = (object obj) => { m_OnChangeEvent(obj); return true; }
                    }, "m_BoundsSize");
#else
                    FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_BoundsSize", container, (object obj) =>
                    {
                        m_OnChangeEvent(obj);
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
            return new WanderView(input.UnityObject, input.Value as MovementAgent, input.OnChangeEvent);
        }
    }
}