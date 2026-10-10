using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        void AddColorAdjustmentChildren(VisualElement parent, SerializedProperty property, TextStudio2DLayerKind kind)
        {
            parent.Add(TextStudioInspectorElements.CreateMutedText(kind == TextStudio2DLayerKind.ColorAdjustment
                ? "Adjust selected lower paint contributions before blending, including shadows and glows. Image effects can add color afterward; use Composite Adjustment to recolor their finished output. Rows above stay unchanged."
                : "Adjust the composed lower image, including colors added by image effects. Selecting the whole scope includes expanded effects and shared surfaces; a partial selection uses glyph silhouettes. Use a mask for another spatial boundary."));
            string path = property.propertyPath;
            TextStudio2DColorAdjustmentSpec spec = (TextStudio2DColorAdjustmentSpec)property.boxedValue;
            List<string> choices = new List<string> { "Standard", "RGBA Matrix" };
            DropdownField controls = new DropdownField("Controls", choices, spec.Mode == TextStudio2DColorAdjustmentMode.Standard ? 0 : 1)
            {
                name = "ts-2d-color-adjustment-controls",
                tooltip = "Matrix preserves the current transform. Switching to Standard starts with neutral named controls. All adjustments use linear light."
            };
            controls.AddToClassList("unity-base-field__aligned");
            controls.RegisterValueChangedCallback(evt =>
            {
                _serializedObject.Update();
                SerializedProperty current = _serializedObject.FindProperty(path);
                TextStudio2DColorAdjustmentSpec value = (TextStudio2DColorAdjustmentSpec)current.boxedValue;
                current.boxedValue = evt.newValue == "RGBA Matrix" ? value.Resolve() :
                    TextStudio2DColorAdjustmentSpec.Adjust(strength: value.Strength, clampOutput: value.ClampOutput).WithSurfaceSelection(value.SurfaceSelection);
                _serializedObject.ApplyModifiedProperties();
                RebuildLayerCards();
            });
            parent.Add(controls);
            if (spec.Mode == TextStudio2DColorAdjustmentMode.Standard)
            {
                AddAdjustmentField(parent, property, "_saturation", "Saturation", "Zero removes color; one preserves it.");
                AddAdjustmentField(parent, property, "_hueDegrees", "Hue", "Rotate the hue in degrees.");
                AddAdjustmentField(parent, property, "_exposure", "Exposure", "Exposure in stops. One stop doubles linear RGB.");
                AddAdjustmentField(parent, property, "_brightness", "Brightness", "Add a linear RGB offset before contrast.");
                AddAdjustmentField(parent, property, "_contrast", "Contrast", "Scale linear RGB around the contrast pivot.");
                AddAdjustmentField(parent, property, "_contrastPivot", "Contrast Pivot", "Linear luminance preserved by contrast. The default is 0.18.");
                AddAdjustmentField(parent, property, "_tint", "Tint", "Multiply straight RGBA by this tint.");
                AddAdjustmentField(parent, property, "_grayscale", "Grayscale", "Blend toward Rec. 709 luminance.");
                AddAdjustmentField(parent, property, "_sepia", "Sepia", "Blend toward a warm sepia transform.");
                AddAdjustmentField(parent, property, "_invert", "Invert", "Blend toward one minus linear RGB.");
            }
            else
            {
                AddAdjustmentField(parent, property, "_red", "Red Row", "RGBA coefficients for straight red output.");
                AddAdjustmentField(parent, property, "_green", "Green Row", "RGBA coefficients for straight green output.");
                AddAdjustmentField(parent, property, "_blue", "Blue Row", "RGBA coefficients for straight blue output.");
                AddAdjustmentField(parent, property, "_alpha", "Alpha Row", "RGBA coefficients for paint alpha. Geometry coverage remains separate.");
                AddAdjustmentField(parent, property, "_offset", "Offset", "Linear RGBA offsets. Alpha offsets cannot create coverage outside the source shape.");
            }
            AddAdjustmentField(parent, property, "_strength", "Strength", "Blend between the original and adjusted straight color.");
            AddAdjustmentField(parent, property, "_clampOutput", "Clamp Output", "Clamp the transformed RGBA to zero through one before blending by strength. Off preserves HDR RGB.");
            if (kind == TextStudio2DLayerKind.ColorAdjustment)
                AddAdjustmentField(parent, property, "_surfaceSelection", "Shared Surfaces", "Whole Surface adjusts a grouped surface when any member matches. Exclude Partial requires every member to match.");
        }

        void AddAdjustmentField(VisualElement parent, SerializedProperty property, string name, string label, string tooltip)
        {
            parent.Add(CreateField(property.FindPropertyRelative(name), label, tooltip));
        }
    }
}
