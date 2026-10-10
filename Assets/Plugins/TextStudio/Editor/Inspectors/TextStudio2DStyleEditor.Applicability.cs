using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed partial class TextStudio2DStyleEditor
    {
        static void AddApplicableTreatmentChildren(VisualElement parent, SerializedProperty spec, TextStudio2DLayerKind kind, bool surface = false)
        {
            SerializedProperty child = spec.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                if (child.depth != spec.depth + 1) continue;
                string fieldName = child.name;
                if (surface && fieldName == "_preservesNativeAppearance") continue;
                if (surface && (fieldName == "_colorFace" || fieldName == "_rendering" || fieldName == "_join" || fieldName == "_miterLimit" ||
                    fieldName == "_dashPattern" || fieldName == "_dashOffset" || fieldName == "_dashCap" || fieldName == "_continueAcrossContours")) continue;
                VisualElement field;
                if (fieldName == "_halftoneSizeRamp")
                {
                    PropertyField ramp = CreateField(child.Copy(), "Dot Size Ramp", null);
                    SerializedProperty enabled = child.FindPropertyRelative("_enabled");
                    void RefreshRamp() => ramp.Query<PropertyField>().ForEach(parameter =>
                    {
                        if (parameter != ramp && parameter.bindingPath != null && !parameter.bindingPath.EndsWith("._enabled"))
                            parameter.style.display = enabled.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                    });
                    ramp.TrackPropertyValue(enabled, _ => RefreshRamp());
                    ramp.RegisterCallback<GeometryChangedEvent>(_ => RefreshRamp());
                    RefreshRamp();
                    field = ramp;
                }
                else if (fieldName == "_mapping" && kind == TextStudio2DLayerKind.AnimatedEffect)
                    field = CreateAnimatedMappingField(spec);
                else field = CreateField(child.Copy(), Nicify(fieldName), null);
                parent.Add(field);
                void Refresh() => field.style.display = TreatmentFieldApplies(spec, kind, fieldName) ? DisplayStyle.Flex : DisplayStyle.None;
                field.TrackSerializedObjectValue(spec.serializedObject, _ => Refresh());
                Refresh();
            }
        }

        static VisualElement CreateAnimatedMappingField(SerializedProperty spec)
        {
            SerializedObject serialized = spec.serializedObject;
            string mappingPath = spec.FindPropertyRelative("_mapping").propertyPath;
            string kindPath = spec.FindPropertyRelative("_kind").propertyPath;
            TextStudio2DMappingSpace[] mappings = (TextStudio2DMappingSpace[])Enum.GetValues(typeof(TextStudio2DMappingSpace));
            VisualElement container = new VisualElement();
            DropdownField dropdown = new DropdownField("Mapping") { name = "ts-animated-effect-mapping" };
            dropdown.AddToClassList("ts-property-field");
            dropdown.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            HelpBox warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            container.Add(dropdown);
            container.Add(warning);
            bool requiresPostProcess = false;

            void Refresh()
            {
                serialized.UpdateIfRequiredOrScript();
                SerializedProperty mapping = serialized.FindProperty(mappingPath);
                SerializedProperty effectKind = serialized.FindProperty(kindPath);
                if (mapping == null || effectKind == null) return;
                requiresPostProcess = TextStudio2DAnimatedEffectUtility.RequiresPostProcess((TextStudio2DAnimatedEffectKind)effectKind.intValue);
                bool unsupported = !TextStudioVisualPrimitiveMath.SupportsMapping((TextStudio2DMappingSpace)mapping.intValue, requiresPostProcess);

                List<string> choices = new List<string>();
                foreach (TextStudio2DMappingSpace choice in mappings)
                    if (TextStudioVisualPrimitiveMath.SupportsMapping(choice, requiresPostProcess)) choices.Add(choice.ToString());
                dropdown.choices = choices;
                TextStudio2DMappingSpace current = (TextStudio2DMappingSpace)mapping.intValue;
                dropdown.SetValueWithoutNotify(current + (TextStudioVisualPrimitiveMath.SupportsMapping(current, requiresPostProcess) ? string.Empty : " (Unsupported)"));
                dropdown.tooltip = requiresPostProcess
                    ? "Image effects support Glyph and Text mapping. Changing the mapping preserves the effect's other settings."
                    : "Choose the coordinate frame for the treatment. Range uses the layer's selected text.";
                warning.text = requiresPostProcess
                    ? "An image effect uses an unsupported mapping. Choose Glyph or Text to restore its appearance."
                    : "An effect uses an unsupported mapping. Choose a valid mapping to restore its appearance.";
                warning.style.display = unsupported ? DisplayStyle.Flex : DisplayStyle.None;
            }

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out TextStudio2DMappingSpace chosen)) return;
                Refresh();
                if (!TextStudioVisualPrimitiveMath.SupportsMapping(chosen, requiresPostProcess)) return;
                SerializedProperty mapping = serialized.FindProperty(mappingPath);
                if (mapping == null) return;
                mapping.intValue = (int)chosen;
                serialized.ApplyModifiedProperties();
                Refresh();
            });
            container.TrackSerializedObjectValue(serialized, _ => Refresh());
            Refresh();
            return container;
        }

        // These predicates mirror the native SDF and Hero post-process branches, not preset values.
        internal static bool TreatmentFieldApplies(SerializedProperty spec, TextStudio2DLayerKind layer, string field)
        {
            if (layer == TextStudio2DLayerKind.GraphicEffect)
            {
                TextStudio2DGraphicEffectKind kind = (TextStudio2DGraphicEffectKind)spec.FindPropertyRelative("_kind").intValue;
                bool noise = kind is TextStudio2DGraphicEffectKind.Grain or TextStudio2DGraphicEffectKind.Duotone or TextStudio2DGraphicEffectKind.Distress or TextStudio2DGraphicEffectKind.InkBleed;
                bool pattern = noise || kind is TextStudio2DGraphicEffectKind.Halftone or TextStudio2DGraphicEffectKind.StencilCutout or TextStudio2DGraphicEffectKind.PrismaticEdge or TextStudio2DGraphicEffectKind.EngravedLine;
                bool ramp = kind == TextStudio2DGraphicEffectKind.Halftone && spec.FindPropertyRelative("_halftoneSizeRamp._enabled").boolValue;
                return field switch
                {
                    "_mapping" or "_scale" => pattern,
                    "_detail" => noise,
                    "_seed" => noise || kind == TextStudio2DGraphicEffectKind.PrismaticEdge,
                    "_angle" => kind is TextStudio2DGraphicEffectKind.Halftone or TextStudio2DGraphicEffectKind.Duotone or TextStudio2DGraphicEffectKind.Letterpress or TextStudio2DGraphicEffectKind.StencilCutout or TextStudio2DGraphicEffectKind.PrismaticEdge or TextStudio2DGraphicEffectKind.EngravedLine,
                    "_offset" => kind is TextStudio2DGraphicEffectKind.StickerPlate or TextStudio2DGraphicEffectKind.CmykOffset or TextStudio2DGraphicEffectKind.RgbSeparation,
                    "_intensity" => !ramp,
                    "_width" => !ramp && kind is not (TextStudio2DGraphicEffectKind.Grain or TextStudio2DGraphicEffectKind.Duotone or TextStudio2DGraphicEffectKind.Distress or TextStudio2DGraphicEffectKind.CmykOffset or TextStudio2DGraphicEffectKind.RgbSeparation),
                    "_softness" => kind is not (TextStudio2DGraphicEffectKind.Grain or TextStudio2DGraphicEffectKind.Duotone or TextStudio2DGraphicEffectKind.CmykOffset or TextStudio2DGraphicEffectKind.RgbSeparation),
                    "_halftoneSizeRamp" => kind == TextStudio2DGraphicEffectKind.Halftone,
                    _ => true
                };
            }
            if (layer == TextStudio2DLayerKind.AnimatedEffect)
            {
                TextStudio2DAnimatedEffectKind kind = (TextStudio2DAnimatedEffectKind)spec.FindPropertyRelative("_kind").intValue;
                return field switch
                {
                    "_scale" => kind is TextStudio2DAnimatedEffectKind.Shimmer or TextStudio2DAnimatedEffectKind.BurningEdge or TextStudio2DAnimatedEffectKind.ElectricOutline or TextStudio2DAnimatedEffectKind.GlitchDisplacement or TextStudio2DAnimatedEffectKind.HeatHaze or TextStudio2DAnimatedEffectKind.LiquidWarp or TextStudio2DAnimatedEffectKind.PixelDissolve,
                    "_width" => kind is not (TextStudio2DAnimatedEffectKind.Shimmer or TextStudio2DAnimatedEffectKind.GlitchDisplacement or TextStudio2DAnimatedEffectKind.HeatHaze or TextStudio2DAnimatedEffectKind.LiquidWarp or TextStudio2DAnimatedEffectKind.EchoTrails),
                    "_softness" => kind is not (TextStudio2DAnimatedEffectKind.HeatHaze or TextStudio2DAnimatedEffectKind.LiquidWarp or TextStudio2DAnimatedEffectKind.EchoTrails),
                    "_amplitude" => kind != TextStudio2DAnimatedEffectKind.LightSweep,
                    "_frequency" => kind is not (TextStudio2DAnimatedEffectKind.LightSweep or TextStudio2DAnimatedEffectKind.EchoTrails or TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus),
                    "_direction" => kind != TextStudio2DAnimatedEffectKind.PixelDissolve,
                    "_seed" => kind is TextStudio2DAnimatedEffectKind.Shimmer or TextStudio2DAnimatedEffectKind.BurningEdge or TextStudio2DAnimatedEffectKind.ElectricOutline or TextStudio2DAnimatedEffectKind.PixelDissolve,
                    "_historyFrames" => kind == TextStudio2DAnimatedEffectKind.EchoTrails,
                    _ => true
                };
            }
            if (layer == TextStudio2DLayerKind.MaterialResponse && field == "_seed")
                return spec.FindPropertyRelative("_kind").intValue == (int)TextStudio2DMaterialResponseKind.Ice ||
                    spec.FindPropertyRelative("_iridescence").floatValue > 0 || spec.FindPropertyRelative("_sparkle").floatValue > 0;
            return true;
        }
    }
}
