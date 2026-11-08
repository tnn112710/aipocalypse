/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Editor
{
    using System;
    using System.Reflection;
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime;
    using Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks;
    using Opsive.Shared.Editor.UIElements;
    using Opsive.Shared.Editor.UIElements.Controls;
    using UnityEngine.UIElements;

    /// <summary>

    /// Displays cover inputs, advanced options, outputs, and diagnostics.

    /// </summary>
    [ControlType(typeof(MoveToCover))]

    public class CoverTypeControl : MovementAgentTypeControl
    {
        /// <summary>
        /// Builds the cover-specific pathfinder inspector.
        /// </summary>
        private class CoverView : MovementAgentView
        {
            /// <summary>
            /// Creates the view.
            /// </summary>
            /// <param name="owner">The owning Unity object.</param>
            /// <param name="cover">The cover task.</param>
            /// <param name="changed">The change callback.</param>
            public CoverView(UnityEngine.Object owner, MoveToCover cover, Func<object, bool> changed) : base(owner, cover, changed) { }

            /// <summary>

            /// Adds relevance-aware cover fields.

            /// </summary>
            protected override void AddTaskFields()
            {
                var objects = new VisualElement(); var positions = new VisualElement(); var directions = new VisualElement();
                void UpdateVisibility()
                {
                    var mode = (CoverThreatMode)typeof(MoveToCover).GetField("m_ThreatMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(m_MovementAgent);
                    objects.style.display = mode == CoverThreatMode.Target || mode == CoverThreatMode.Automatic ? DisplayStyle.Flex : DisplayStyle.None;
                    positions.style.display = mode == CoverThreatMode.Position ? DisplayStyle.Flex : DisplayStyle.None;
                    directions.style.display = mode == CoverThreatMode.Direction ? DisplayStyle.Flex : DisplayStyle.None;
                }
#if OPSIVE_SHARED_V3
                InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                    UnityObject = m_UnityObject, Target = m_MovementAgent, Container = this,
                    OnChangeEvent = (object obj) => { m_OnChangeEvent?.Invoke(obj); UpdateVisibility(); return true; }
                }, "m_ThreatMode");
#else
                FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, "m_ThreatMode", this, (object obj) => { m_OnChangeEvent?.Invoke(obj); UpdateVisibility(); });
#endif
                AddFields(objects, "m_Threat", "m_ThreatOffset"); Add(objects);
                AddFields(positions, "m_ThreatPosition"); Add(positions);
                AddFields(directions, "m_ThreatDirection"); Add(directions);
                AddFields(this, "m_CoverLayers", "m_CoverSearchDistance", "m_CoverOffset");
                var coverGroup0 = new Foldout { text = "Stance", value = false }; Add(coverGroup0);
                AddFields(coverGroup0, "m_StanceMode", "m_InferDimensions", "m_CoverHeight", "m_CoverRadius", "m_CrouchHeight");
                var coverGroup1 = new Foldout { text = "Selection", value = false }; Add(coverGroup1);
                AddFields(coverGroup1, "m_CoverSource", "m_Selection", "m_ThreatDistanceWeight", "m_PriorityWeight", "m_MinimumThreatDistance");
                var coverGroup2 = new Foldout { text = "Search and Recovery", value = false }; Add(coverGroup2);
                AddFields(coverGroup2, "m_CandidatesPerUpdate", "m_MaximumRetries", "m_RetryDelay", "m_RecheckInterval");
                var coverGroup3 = new Foldout { text = "Reservations", value = false }; Add(coverGroup3);
                AddFields(coverGroup3, "m_UseReservations", "m_ReservationDuration");
                if (!(m_MovementAgent is FindCover)) {
                    var maintenance = new Foldout { text = "Completion and Maintenance", value = false }; Add(maintenance);
                    AddFields(maintenance, "m_CompletionMode", "m_StableArrivalDuration", "m_ReselectionInterval", "m_MinimumImprovement", "m_LookAtCoverPoint", "m_RotationSpeed", "m_ArrivedAngle");
                }
                var advanced = new Foldout { text = "Advanced", value = false }; Add(advanced);
                AddFields(advanced, "m_CheckForOccupency", "m_OccupencyOverlapRadius", "m_OccupencyLayerMask", "m_MaxOccupencyObjects", "m_MaximumCoverCandidates", "m_UseTimeout", "m_Timeout");
                if (!(m_MovementAgent is FindCover)) {
                    AddFields(advanced, "m_StopOnTaskEnd", "m_FailOnInvalidPath", "m_UseStuckDetection", "m_StuckDuration", "m_MinimumProgressDistance", "m_PathValidationGrace");
                }
                var outputs = new Foldout { text = "Outputs", value = false }; Add(outputs);
                AddFields(outputs, "m_HasCover", "m_CoverPosition", "m_CoverNormal", "m_CoverObject", "m_SelectedCoverPoint", "m_RequiredStance", "m_CoverFailure");
                AddFields(this, "m_DebugCover"); UpdateVisibility();
            }

            /// <summary>

            /// Adds named fields using the standard task serialization callback.

            /// </summary>
            /// <param name="container">The destination element.</param>
            /// <param name="names">The serialized fields.</param>
            private void AddFields(VisualElement container, params string[] names)
            {
                foreach (var name in names) {
#if OPSIVE_SHARED_V3
                    InspectorBuilder.AddField(new Opsive.Shared.Editor.UIElements.Controls.ControlInput {
                        UnityObject = m_UnityObject, Target = m_MovementAgent, Container = container,
                        OnChangeEvent = (object obj) => { m_OnChangeEvent?.Invoke(obj); return true; }
                    }, name);
#else
                    FieldInspectorView.AddField(m_UnityObject, m_MovementAgent, name, container, (object obj) => { m_OnChangeEvent?.Invoke(obj); });
#endif
                }
            }
        }

        /// <summary>

        /// Creates a cover inspector.

        /// </summary>
        /// <param name="input">The editor input.</param>
        /// <returns>The configured view.</returns>
#if OPSIVE_SHARED_V3
        protected override VisualElement GetControl(Opsive.Shared.Editor.UIElements.Controls.ControlInput input)
#else
        protected override VisualElement GetControl(TypeControlInput input)
#endif
        {
            return new CoverView(input.UnityObject, input.Value as MoveToCover, input.OnChangeEvent);
        }
    }
    /// <summary>
    /// Uses the cover inspector for query-only selection.
    /// </summary>
    [ControlType(typeof(FindCover))]
    public class FindCoverTypeControl : CoverTypeControl { }
}