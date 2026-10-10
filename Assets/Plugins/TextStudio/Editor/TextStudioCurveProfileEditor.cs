using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Authoring inspector for reusable curve shapes.</summary>
    [CustomEditor(typeof(TextStudioCurveProfile))]
    [CanEditMultipleObjects]
    internal sealed class TextStudioCurveProfileEditor : UnityEditor.Editor
    {
        const float PreviewHeight = 132f;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-effect-curve-inspector");
            Label summary = TextStudioInspectorElements.CreateMutedText(
                "A shared curve shape for text placement and motion. Edits to this profile update every linked host.");
            summary.name = "ts-effect-curve-summary";
            root.Add(summary);

            VisualElement quickStarts = new VisualElement
            {
                name = "ts-effect-curve-quick-starts"
            };
            quickStarts.AddToClassList("ts-button-row");
            quickStarts.Add(CreatePresetButton("Linear", CurveType.Linear));
            quickStarts.Add(CreatePresetButton("Flat", CurveType.Flat));
            quickStarts.Add(CreatePresetButton("Arch", CurveType.Arch));
            quickStarts.Add(CreatePresetButton("Valley", CurveType.Valley));
            quickStarts.Add(CreatePresetButton("S-Curve", CurveType.SCurve));
            quickStarts.Add(CreatePresetButton("Oscillate", CurveType.Sin));
            quickStarts.Add(CreatePresetButton("Bounce", CurveType.Bounce));
            quickStarts.Add(CreatePresetButton("Step", CurveType.Step));
            quickStarts.Add(CreatePresetButton("Hold", CurveType.Hold));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Quick Starts",
                "ts-effect-curve-presets",
                quickStarts));

            SerializedProperty curveType = serializedObject.FindProperty("_curveType");
            PropertyField shape = new PropertyField(curveType, "Shape")
            {
                name = "ts-effect-curve-shape",
                tooltip = "Select a built-in mathematical curve or Custom to author an Animation Curve."
            };
            shape.AddToClassList("ts-property-field");
            SerializedProperty customCurveProperty = serializedObject.FindProperty("_customCurve");
            PropertyField customCurve = new PropertyField(customCurveProperty, "Custom Curve")
            {
                name = "ts-effect-curve-custom",
                tooltip = "The horizontal axis is normalized time from 0 to 1. Values may be positive, negative, or overshoot when the consuming effect supports it."
            };
            customCurve.AddToClassList("ts-property-field");
            Label rangeHelp = TextStudioInspectorElements.CreateMutedText(
                "Curve input runs from 0 to 1. Height and orientation belong to each placement or effect that uses this shape.",
                "ts-effect-curve-range-help");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Shape",
                "ts-effect-curve-settings",
                shape,
                customCurve,
                rangeHelp));

            VisualElement preview = new VisualElement
            {
                name = "ts-effect-curve-preview"
            };
            preview.style.height = PreviewHeight;
            preview.style.backgroundColor = new Color(0.075f, 0.09f, 0.12f, 1f);
            preview.generateVisualContent += context =>
                DrawCurvePreview(context, preview.contentRect, target as TextStudioCurveProfile);
            root.Add(TextStudioInspectorElements.CreateSection(
                "Preview",
                "ts-effect-curve-preview-section",
                preview));

            void RefreshConditionalFields()
            {
                serializedObject.UpdateIfRequiredOrScript();
                bool isCustom = curveType.intValue == (int)CurveType.Custom;
                customCurve.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                preview.MarkDirtyRepaint();
            }

            shape.RegisterValueChangeCallback(_ => RefreshConditionalFields());
            root.TrackPropertyValue(curveType, _ => RefreshConditionalFields());
            root.TrackPropertyValue(customCurveProperty, _ => preview.MarkDirtyRepaint());
            RefreshConditionalFields();
            return root;
        }

        Button CreatePresetButton(string label, CurveType curveType)
        {
            return new Button(() => ApplyPreset(curveType))
            {
                name = "ts-effect-curve-preset-" + curveType.ToString().ToLowerInvariant(),
                text = label
            };
        }

        void ApplyPreset(CurveType curveType)
        {
            serializedObject.Update();
            serializedObject.FindProperty("_curveType").enumValueIndex = (int)curveType;
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawCurvePreview(MeshGenerationContext context, Rect rect, ITextStudioCurve curve)
        {
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            for (int i = 1; i < 4; i++)
            {
                float x = rect.x + rect.width * i / 4f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.y));
                painter.LineTo(new Vector2(x, rect.yMax));
                painter.Stroke();
            }
            float zeroY = Mathf.Lerp(rect.yMax - 5f, rect.y + 5f, 0.5f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.x, zeroY));
            painter.LineTo(new Vector2(rect.xMax, zeroY));
            painter.Stroke();

            if (curve != null)
            {
                painter.strokeColor = new Color(0.20f, 0.88f, 1f, 1f);
                painter.lineWidth = 2f;
                const int Samples = 128;
                painter.BeginPath();
                for (int i = 0; i <= Samples; i++)
                {
                    float time = i / (float)Samples;
                    float value = Mathf.Clamp(curve.Evaluate(time), -1.5f, 1.5f);
                    float normalizedValue = Mathf.InverseLerp(-1.5f, 1.5f, value);
                    Vector2 point = new Vector2(
                        Mathf.Lerp(rect.x + 5f, rect.xMax - 5f, time),
                        Mathf.Lerp(rect.yMax - 5f, rect.y + 5f, normalizedValue));
                    if (i == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }
                painter.Stroke();
            }
        }
    }
}
