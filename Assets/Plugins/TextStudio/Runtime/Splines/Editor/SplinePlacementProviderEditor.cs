using System.Collections.Generic;
using TextStudio.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.Splines;
using UnityEngine.UIElements;

namespace TextStudio.Splines.Editor
{
    [CustomEditor(typeof(SplinePlacementProvider))]
    [CanEditMultipleObjects]
    public sealed class SplinePlacementProviderEditor : UnityEditor.Editor
    {
        static readonly Vector3[] s_splineGuidePoints = new Vector3[49];

        enum SplineQuickStart
        {
            Wayfinding,
            ReverseRoute,
            ClosedMarquee
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-spline-placement-inspector");
            SplinePlacementProvider provider =
                target as SplinePlacementProvider;
            Button quickStart = TextStudioInspectorElements.CreateButton(
                "Choose Quick Start…",
                "Apply ordinary editable spline and orientation settings.",
                () => ShowQuickStartMenu(provider),
                true);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Starting Point",
                "ts-spline-placement-starting-point",
                TextStudioInspectorElements.CreateMutedText(
                    "Choose a practical starting point, then refine the spline settings below."),
                TextStudioInspectorElements.CreateButtonRow(quickStart)));
            PropertyField startOffset =
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_startOffset",
                    "Closed Path Start");
            Foldout advanced = new Foldout
            {
                name = "ts-spline-placement-advanced",
                text = "Advanced",
                value = false,
                tooltip = "Show optional placement controls."
            };
            advanced.Add(
                TextStudioInspectorElements.CreatePropertyField(
                    serializedObject,
                    "_pathPosition",
                    "Path Position"));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Spline Placement",
                "ts-spline-placement-settings",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_splineContainer", "Spline Container"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_splineIndex", "Spline Index"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_spacing", "Spacing"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_fill", "Fill"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_reverse", "Reverse"),
                startOffset,
                advanced));
            HelpBox status = TextStudioInspectorElements.CreateHelpBox(
                string.Empty,
                HelpBoxMessageType.Info,
                "ts-spline-placement-provider-status");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Readiness",
                "ts-spline-placement-readiness",
                status));
            System.Action refresh = () =>
            {
                UpdateStatus(provider, status);
                startOffset.style.display =
                    HasMixedSelectedSplineState() || IsClosed(provider)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            };
            Track(root, "_splineContainer", refresh);
            Track(root, "_splineIndex", refresh);
            refresh();
            return root;
        }

        bool HasMixedSelectedSplineState()
        {
            SerializedProperty container =
                serializedObject.FindProperty("_splineContainer");
            SerializedProperty index =
                serializedObject.FindProperty("_splineIndex");
            return
                (container != null && container.hasMultipleDifferentValues) ||
                (index != null && index.hasMultipleDifferentValues);
        }

        void ShowQuickStartMenu(SplinePlacementProvider provider)
        {
            GenericMenu menu = new GenericMenu();
            AddQuickStart(menu, provider, "Wayfinding", SplineQuickStart.Wayfinding);
            AddQuickStart(menu, provider, "Reverse Route", SplineQuickStart.ReverseRoute);
            if (IsClosed(provider))
            {
                AddQuickStart(
                    menu,
                    provider,
                    "Closed Marquee",
                    SplineQuickStart.ClosedMarquee);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Closed Marquee"));
            }
            menu.ShowAsContext();
        }

        void AddQuickStart(
            GenericMenu menu,
            SplinePlacementProvider provider,
            string label,
            SplineQuickStart preset)
        {
            menu.AddItem(
                new GUIContent(label),
                false,
                () => ApplyQuickStart(provider, preset));
        }

        void ApplyQuickStart(
            SplinePlacementProvider provider,
            SplineQuickStart preset)
        {
            if (provider == null)
                return;

            TextStudioText hub = provider.GetComponent<TextStudioText>();
            Object[] undoTargets = hub != null
                ? new Object[] { provider, hub }
                : new Object[] { provider };
            Undo.RecordObjects(undoTargets, "Apply Spline Placement Quick Start");
            provider.Spacing = PathSpacing.Even;
            provider.Fill = 1f;
            provider.StartOffset = 0f;
            provider.PathPosition = PathPosition.Above;
            provider.Reverse = preset == SplineQuickStart.ReverseRoute;
            if (preset == SplineQuickStart.ClosedMarquee)
                provider.StartOffset = 0.125f;
            if (hub != null)
                hub.SetPlacementOrientation(TextStudioPlacementOrientation.FollowPath);
            EditorUtility.SetDirty(provider);
            if (hub != null)
                EditorUtility.SetDirty(hub);
            serializedObject.Update();
        }

        static bool IsClosed(SplinePlacementProvider provider)
        {
            if (provider == null || provider.SplineContainer == null)
                return false;
            int index = provider.SplineIndex;
            if (provider.SplineContainer.Splines == null ||
                index < 0 ||
                index >= provider.SplineContainer.Splines.Count)
            {
                return false;
            }

            Spline spline = provider.SplineContainer.Splines[index];
            return spline != null && spline.Closed;
        }

        void OnSceneGUI()
        {
            if (target is not SplinePlacementProvider provider ||
                provider.SplineContainer == null)
            {
                return;
            }

            int index = provider.SplineIndex;
            if (provider.SplineContainer.Splines == null ||
                index < 0 ||
                index >= provider.SplineContainer.Splines.Count)
            {
                return;
            }

            Spline spline = provider.SplineContainer.Splines[index];
            if (spline == null || spline.Count < 2)
                return;

            bool closed = spline.Closed;
            float direction = provider.Reverse ? -1f : 1f;
            int last = s_splineGuidePoints.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                float progress = (float)i / last;
                float t = closed
                    ? Mathf.Repeat(
                        provider.StartOffset + progress * direction,
                        1f)
                    : provider.Reverse ? 1f - progress : progress;
                SplineUtility.Evaluate(
                    spline,
                    t,
                    out float3 position,
                    out float3 tangent,
                    out float3 up);
                s_splineGuidePoints[i] = (Vector3)position;
            }

            using (new Handles.DrawingScope(
                       new Color(0.25f, 0.78f, 1f, 0.9f),
                       provider.SplineContainer.transform.localToWorldMatrix))
            {
                Handles.DrawAAPolyLine(2f, s_splineGuidePoints);
                Vector3 start = s_splineGuidePoints[0];
                Vector3 end = s_splineGuidePoints[2];
                Handles.DrawLine(start, end, 3f);
                Handles.DrawSolidDisc(
                    start,
                    Vector3.forward,
                    Mathf.Max(
                        0.025f,
                        HandleUtility.GetHandleSize(
                            provider.SplineContainer.transform.TransformPoint(start)) *
                        0.025f));
            }
        }

        static void UpdateStatus(
            SplinePlacementProvider provider,
            HelpBox status)
        {
            if (provider == null)
                return;

            if (!provider.isActiveAndEnabled)
            {
                status.text = "Enable this component to make Spline placement available to the Text Studio hub.";
                TextStudioInspectorElements.SetHelpBoxMessageType(
                    status,
                    HelpBoxMessageType.Warning);
                return;
            }

            SplineContainer container = provider.SplineContainer;
            if (container == null)
            {
                status.text = "Assign a Unity Spline Container to place the text.";
                TextStudioInspectorElements.SetHelpBoxMessageType(
                    status,
                    HelpBoxMessageType.Warning);
                return;
            }

            int splineCount = container.Splines != null
                ? container.Splines.Count
                : 0;
            if (provider.SplineIndex < 0 ||
                provider.SplineIndex >= splineCount)
            {
                status.text =
                    "Choose an available spline index. This container currently has " +
                    splineCount + ".";
                TextStudioInspectorElements.SetHelpBoxMessageType(
                    status,
                    HelpBoxMessageType.Warning);
                return;
            }

            Spline spline = container.Splines[provider.SplineIndex];
            if (spline == null || spline.Count < 2)
            {
                status.text = "The selected spline needs at least two knots.";
                TextStudioInspectorElements.SetHelpBoxMessageType(
                    status,
                    HelpBoxMessageType.Warning);
                return;
            }

            status.text =
                "Ready: " + spline.Count + " knots, " +
                spline.GetLength().ToString("0.###") + " spline units.";
            TextStudioInspectorElements.SetHelpBoxMessageType(
                status,
                HelpBoxMessageType.Info);
        }

        void Track(
            VisualElement root,
            string propertyName,
            System.Action callback)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            if (property != null)
                root.TrackPropertyValue(property, _ => callback());
        }
    }

    public sealed class TextStudioSplineIntegrationProvider :
        ITextStudioIntegrationEditorProvider
    {
        public string IntegrationId => "unity-splines";

        public bool CanConfigure(TextStudioIntegrationValidationContext context)
        {
            return context?.SelectedGameObject != null;
        }

        public void Configure(TextStudioIntegrationValidationContext context)
        {
            GameObject selected = context?.SelectedGameObject;
            if (selected == null)
                return;

            TextStudioText target =
                selected.GetComponent<TextStudioText>() ??
                selected.GetComponentInChildren<TextStudioText>(true);
            if (target == null)
                target = Undo.AddComponent<TextStudioText>(selected);

            SplinePlacementProvider provider =
                target.GetComponent<SplinePlacementProvider>();
            if (provider == null)
                provider = Undo.AddComponent<SplinePlacementProvider>(target.gameObject);

            SplineContainer container =
                selected.GetComponent<SplineContainer>() ??
                selected.GetComponentInParent<SplineContainer>() ??
                selected.GetComponentInChildren<SplineContainer>(true);

            Undo.RecordObjects(
                new Object[] { target, provider },
                "Configure Text Studio Spline Placement");
            target.PlacementMode = PlacementMode.Spline;
            if (provider.SplineContainer == null && container != null)
                provider.SplineContainer = container;
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(provider);
        }

        public void Validate(
            TextStudioIntegrationValidationContext context,
            ICollection<TextStudioIntegrationIssue> issues)
        {
            GameObject selected = context?.SelectedGameObject;
            if (selected == null)
                return;

            TextStudioText target =
                selected.GetComponent<TextStudioText>() ??
                selected.GetComponentInChildren<TextStudioText>(true);
            SplinePlacementProvider provider =
                target != null
                    ? target.GetComponent<SplinePlacementProvider>()
                    : null;
            if (target == null || provider == null)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Info,
                    "Select a spline-driven text object",
                    "Configure Selected adds Text Studio and its Unity Splines placement provider."));
                return;
            }

            if (target.PlacementMode != PlacementMode.Spline)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Warning,
                    "Spline placement is not active",
                    "The Text Studio target is using another placement mode.",
                    "Use Configure Selected."));
            }

            if (provider.SplineContainer == null)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Warning,
                    "Spline container is missing",
                    "Assign the Unity Spline Container that should drive glyph placement."));
                return;
            }

            int splineCount = provider.SplineContainer.Splines != null
                ? provider.SplineContainer.Splines.Count
                : 0;
            if (provider.SplineIndex < 0 || provider.SplineIndex >= splineCount)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Error,
                    "Spline index is unavailable",
                    "Choose a spline index that exists in the assigned container."));
            }
            else if (provider.SplineContainer.Splines[provider.SplineIndex].Count < 2)
            {
                issues.Add(new TextStudioIntegrationIssue(
                    TextStudioIntegrationIssueSeverity.Warning,
                    "Spline needs another knot",
                    "Text Studio needs at least two knots to orient and place glyphs."));
            }
        }
    }
}
