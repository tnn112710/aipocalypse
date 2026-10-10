using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    static class EffectPropertyDrawerLayout
    {
        public static Foldout CreateFoldout(SerializedProperty property, GUIContent label)
        {
            Foldout foldout = new Foldout
            {
                text = label != null ? label.text : ObjectNames.NicifyVariableName(property.name),
                tooltip = label != null && !string.IsNullOrEmpty(label.tooltip) ? label.tooltip : property.tooltip,
                value = property.isExpanded,
                viewDataKey = "text-studio-property-" + property.propertyPath
            };
            foldout.AddToClassList("ts-property-group");
            foldout.RegisterValueChangedCallback(evt => property.isExpanded = evt.newValue);
            return foldout;
        }

        public static PropertyField CreateProperty(SerializedProperty property, string label)
        {
            if (property == null)
                return null;

            PropertyField field = new PropertyField(property.Copy(), label)
            {
                tooltip = property.tooltip
            };
            field.AddToClassList("ts-property-field");
            return field;
        }

        public static void AddVisibleChildren(VisualElement parent, SerializedProperty property)
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                parent.Add(CreateProperty(child, ObjectNames.NicifyVariableName(child.name)));
            }
        }
    }

    [CustomPropertyDrawer(typeof(TextStudioInfluenceField))]
    internal sealed class TextStudioInfluenceFieldPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty enabled = property.FindPropertyRelative("_enabled");
            SerializedProperty shape = property.FindPropertyRelative("_shape");
            SerializedProperty space = property.FindPropertyRelative("_space");
            SerializedProperty combine = property.FindPropertyRelative("_combine");
            SerializedProperty axis = property.FindPropertyRelative("_axis");
            SerializedProperty center = property.FindPropertyRelative("_center");
            SerializedProperty size = property.FindPropertyRelative("_size");
            SerializedProperty radius = property.FindPropertyRelative("_radius");
            SerializedProperty majorRadius = property.FindPropertyRelative("_majorRadius");
            SerializedProperty noiseScale = property.FindPropertyRelative("_noiseScale");
            SerializedProperty seed = property.FindPropertyRelative("_seed");
            SerializedProperty strength = property.FindPropertyRelative("_strength");
            SerializedProperty invert = property.FindPropertyRelative("_invert");
            SerializedProperty falloff = property.FindPropertyRelative("_falloff");

            Foldout foldout = EffectPropertyDrawerLayout.CreateFoldout(
                property,
                new GUIContent(property.displayName, property.tooltip));
            foldout.name = "ts-influence-field-card-" +
                SanitizePropertyPath(property.propertyPath);
            foldout.userData = property.propertyPath;
            foldout.AddToClassList("ts-influence-field-card");

            PropertyField enabledField = CreateField(
                enabled,
                "Enabled",
                "ts-influence-field-enabled");
            PropertyField shapeField = CreateField(
                shape,
                "Shape",
                "ts-influence-field-shape");
            PropertyField spaceField = CreateField(
                space,
                "Space",
                "ts-influence-field-space");
            PropertyField combineField = CreateField(
                combine,
                "Combine",
                "ts-influence-field-combine");
            PropertyField axisField = CreateField(
                axis,
                "Axis",
                "ts-influence-field-axis");
            PropertyField centerField = CreateField(
                center,
                "Center",
                "ts-influence-field-center");
            PropertyField sizeField = CreateField(
                size,
                "Size",
                "ts-influence-field-size");
            PropertyField radiusField = CreateField(
                radius,
                "Radius",
                "ts-influence-field-radius");
            PropertyField majorRadiusField = CreateField(
                majorRadius,
                "Ring Radius",
                "ts-influence-field-major-radius");
            PropertyField noiseScaleField = CreateField(
                noiseScale,
                "Noise Scale",
                "ts-influence-field-noise-scale");
            PropertyField seedField = CreateField(
                seed,
                "Seed",
                "ts-influence-field-seed");
            PropertyField strengthField = CreateField(
                strength,
                "Strength",
                "ts-influence-field-strength");
            PropertyField invertField = CreateField(
                invert,
                "Invert",
                "ts-influence-field-invert");
            PropertyField falloffField = CreateField(
                falloff,
                "Falloff",
                "ts-influence-field-falloff");
            Label targetGuidance = TextStudioInspectorElements.CreateMutedText(
                "Uses the Text Studio component's Shared Spatial Target. Fallback Center is used by asset previews and whenever no target is assigned.",
                "ts-influence-field-target-guidance");

            foldout.Add(enabledField);
            foldout.Add(shapeField);
            foldout.Add(spaceField);
            foldout.Add(combineField);
            foldout.Add(targetGuidance);
            foldout.Add(axisField);
            foldout.Add(centerField);
            foldout.Add(sizeField);
            foldout.Add(radiusField);
            foldout.Add(majorRadiusField);
            foldout.Add(noiseScaleField);
            foldout.Add(seedField);
            foldout.Add(strengthField);
            foldout.Add(invertField);
            foldout.Add(falloffField);

            Action refresh = () =>
            {
                shape.serializedObject.UpdateIfRequiredOrScript();
                TextStudioInfluenceFieldShape selectedShape =
                    (TextStudioInfluenceFieldShape)shape.enumValueIndex;
                TextStudioInfluenceFieldSpace selectedSpace =
                    (TextStudioInfluenceFieldSpace)space.enumValueIndex;
                bool linear = selectedShape ==
                    TextStudioInfluenceFieldShape.Linear;
                bool box = selectedShape ==
                    TextStudioInfluenceFieldShape.Box;
                bool cylinder = selectedShape ==
                    TextStudioInfluenceFieldShape.Cylinder;
                bool torus = selectedShape ==
                    TextStudioInfluenceFieldShape.Torus;
                bool targetDistance = selectedShape ==
                    TextStudioInfluenceFieldShape.TargetDistance;
                bool noise = selectedShape ==
                    TextStudioInfluenceFieldShape.Noise;

                foldout.text = FormatEnum(selectedShape) +
                    " (" + FormatEnum(selectedSpace) + ")";
                axisField.style.display = linear || cylinder || torus
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                centerField.style.display = noise
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                centerField.label = targetDistance
                    ? "Fallback Center"
                    : "Center";
                sizeField.style.display = box
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                radiusField.style.display = box || noise
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                radiusField.label = torus
                    ? "Tube Radius"
                    : linear
                        ? "Range"
                        : "Radius";
                majorRadiusField.style.display = torus
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                noiseScaleField.style.display = noise
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                seedField.style.display = noise
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                targetGuidance.style.display = targetDistance
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };
            foldout.TrackPropertyValue(shape, _ => refresh());
            foldout.TrackPropertyValue(space, _ => refresh());
            refresh();
            return foldout;
        }

        static PropertyField CreateField(
            SerializedProperty property,
            string label,
            string name)
        {
            PropertyField field = EffectPropertyDrawerLayout.CreateProperty(
                property,
                label);
            field.name = name;
            return field;
        }

        static string FormatEnum(Enum value)
        {
            return ObjectNames.NicifyVariableName(value.ToString());
        }

        static string SanitizePropertyPath(string propertyPath)
        {
            return (propertyPath ?? string.Empty)
                .Replace('.', '-')
                .Replace('[', '-')
                .Replace(']', '-');
        }
    }

    [CustomPropertyDrawer(typeof(PhaseParams))]
    internal sealed class PhaseParamsPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Foldout foldout = EffectPropertyDrawerLayout.CreateFoldout(
                property,
                new GUIContent(property.displayName, property.tooltip));
            foldout.Add(EffectPropertyDrawerLayout.CreateProperty(property.FindPropertyRelative("charOffset"), "Character Delay"));
            foldout.Add(EffectPropertyDrawerLayout.CreateProperty(property.FindPropertyRelative("wordOffset"), "Word Delay"));
            foldout.Add(EffectPropertyDrawerLayout.CreateProperty(property.FindPropertyRelative("lineOffset"), "Line Delay"));
            foldout.Add(EffectPropertyDrawerLayout.CreateProperty(property.FindPropertyRelative("speed"), "Speed Multiplier"));
            return foldout;
        }

    }

    /// <summary>
    /// Draws a <see cref="TextStudioRangeAttribute"/> as a labelled slider with optional snap
    /// increment and suffix.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextStudioRangeAttribute))]
    internal class TextStudioRangePropertyDrawer : PropertyDrawer
    {
        const float SuffixGap = 4f;
        const float OverflowFieldGap = 4f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            TextStudioRangeAttribute attr = (TextStudioRangeAttribute)attribute;
            if (property.propertyType != SerializedPropertyType.Float &&
                property.propertyType != SerializedPropertyType.Integer)
            {
                return TextStudioInspectorElements.CreateHelpBox(
                    "Text Studio Range supports only floating-point and integer fields.",
                    HelpBoxMessageType.Warning);
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("ts-range-field");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                SliderInt slider = new SliderInt(
                    property.displayName,
                    Mathf.RoundToInt(attr.Min),
                    Mathf.RoundToInt(attr.Max))
                {
                    bindingPath = property.propertyPath,
                    showInputField = true,
                    tooltip = property.tooltip
                };
                slider.AddToClassList("unity-base-field__aligned");
                slider.style.flexGrow = 1f;
                row.Add(slider);
            }
            else
            {
                Slider slider = new Slider(property.displayName, attr.Min, attr.Max)
                {
                    tooltip = property.tooltip,
                    showInputField = !attr.AllowOverflow
                };
                slider.AddToClassList("unity-base-field__aligned");
                slider.style.flexGrow = 1f;
                row.Add(slider);

                FloatField overflow = null;
                if (attr.AllowOverflow)
                {
                    overflow = new FloatField
                    {
                        name = property.name + "-overflow",
                        tooltip = "Enter a value above the slider range when needed."
                    };
                    overflow.style.width = 64f;
                    overflow.style.marginLeft = OverflowFieldGap;
                    row.Add(overflow);
                }

                FloatField overflowField = overflow;
                slider.RegisterValueChangedCallback(evt =>
                    ApplyFloat(property, Snap(evt.newValue, attr.Snap), slider, overflowField));
                if (overflowField != null)
                {
                    overflowField.RegisterValueChangedCallback(evt =>
                        ApplyFloat(property, Mathf.Max(attr.Min, evt.newValue), slider, overflowField));
                }
                row.TrackPropertyValue(property, changed =>
                {
                    float value = changed.floatValue;
                    slider.SetValueWithoutNotify(Mathf.Clamp(value, attr.Min, attr.Max));
                    overflowField?.SetValueWithoutNotify(value);
                });
                slider.SetValueWithoutNotify(Mathf.Clamp(property.floatValue, attr.Min, attr.Max));
                overflowField?.SetValueWithoutNotify(property.floatValue);
            }

            if (!string.IsNullOrEmpty(attr.Suffix))
            {
                Label suffix = new Label(attr.Suffix)
                {
                    tooltip = property.tooltip
                };
                suffix.AddToClassList("ts-muted-text");
                suffix.style.marginLeft = SuffixGap;
                row.Add(suffix);
            }
            return row;
        }

        static void ApplyFloat(
            SerializedProperty property,
            float value,
            Slider slider,
            FloatField overflowField)
        {
            property.serializedObject.UpdateIfRequiredOrScript();
            property.floatValue = value;
            property.serializedObject.ApplyModifiedProperties();
            slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.lowValue, slider.highValue));
            overflowField?.SetValueWithoutNotify(value);
        }

        static float Snap(float value, float increment)
        {
            return increment > 0f
                ? Mathf.Round(value / increment) * increment
                : value;
        }
    }

    /// <summary>
    /// Draws a <see cref="TextStudioDefaultAttribute"/> field with an inline "Reset" button.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextStudioDefaultAttribute))]
    internal class TextStudioDefaultPropertyDrawer : PropertyDrawer
    {
        const float ResetButtonWidth = 50f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            TextStudioDefaultAttribute attr = (TextStudioDefaultAttribute)attribute;
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-default-field");
            row.style.flexDirection = FlexDirection.Row;

            PropertyField field = new PropertyField(property.Copy())
            {
                tooltip = property.tooltip
            };
            field.AddToClassList("ts-property-field");
            field.style.flexGrow = 1f;
            row.Add(field);

            Button reset = TextStudioInspectorElements.CreateButton(
                "Reset",
                "Restore the recommended Text Studio default.",
                () =>
                {
                    Undo.RecordObjects(property.serializedObject.targetObjects, "Reset Text Studio Value");
                    property.serializedObject.UpdateIfRequiredOrScript();
                    ApplyDefault(property, attr.Value);
                    property.serializedObject.ApplyModifiedProperties();
                });
            reset.style.width = ResetButtonWidth;
            row.Add(reset);
            return row;
        }

        static void ApplyDefault(SerializedProperty p, object value)
        {
            try
            {
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Float:
                        p.floatValue = Convert.ToSingle(value); break;
                    case SerializedPropertyType.Integer:
                        p.intValue = Convert.ToInt32(value); break;
                    case SerializedPropertyType.Boolean:
                        p.boolValue = Convert.ToBoolean(value); break;
                    case SerializedPropertyType.String:
                        p.stringValue = value?.ToString() ?? string.Empty; break;
                    case SerializedPropertyType.Color when value is Color c:
                        p.colorValue = c; break;
                    case SerializedPropertyType.Vector2 when value is Vector2 v2:
                        p.vector2Value = v2; break;
                    case SerializedPropertyType.Vector3 when value is Vector3 v3:
                        p.vector3Value = v3; break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TextStudioDefault] Failed to apply default to {p.name}: {e.Message}");
            }
        }
    }

    internal readonly struct TextStudioSpatialTargetConsumerReport
    {
        public TextStudioSpatialTargetConsumerReport(
            int taggedRangeCount,
            string tagList,
            IReadOnlyList<TextStudioInfluenceField> targetFields)
        {
            TaggedRangeCount = taggedRangeCount;
            TagList = tagList ?? string.Empty;
            TargetFields = targetFields ?? Array.Empty<TextStudioInfluenceField>();
        }

        public int TaggedRangeCount { get; }
        public string TagList { get; }
        public IReadOnlyList<TextStudioInfluenceField> TargetFields { get; }
        public bool HasConsumers => TaggedRangeCount > 0;
    }

    internal static class TextStudioSpatialTargetEffectUtility
    {
        const string BaseEffectId = "target-influence";

        internal static TextStudioSpatialTargetConsumerReport AnalyzeContent(
            string markup)
        {
            if (string.IsNullOrEmpty(markup))
            {
                return new TextStudioSpatialTargetConsumerReport(
                    0,
                    string.Empty,
                    Array.Empty<TextStudioInfluenceField>());
            }

            TextStudioRegistry.EnsureInitialized();
            TextStudioTokenizer tokenizer = new TextStudioTokenizer();
            tokenizer.Tokenize(markup);
            ReadOnlySpan<TextToken> tokens = tokenizer.Tokens;
            TextStudioEffectResolver resolver = new TextStudioEffectResolver();
            List<TextStudioResolvedEffectBinding> bindings =
                new List<TextStudioResolvedEffectBinding>(8);
            Dictionary<string, int> counts = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            List<string> orderedTags = new List<string>();
            List<TextStudioInfluenceField> targetFields =
                new List<TextStudioInfluenceField>();
            HashSet<TextStudioInfluenceField> uniqueTargetFields =
                new HashSet<TextStudioInfluenceField>();
            List<TextStudioInfluenceField> scopeFields =
                new List<TextStudioInfluenceField>();
            int taggedRangeCount = 0;

            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                TextToken token = tokens[tokenIndex];
                if (token.kind != TokenKind.StyleOpen &&
                    token.kind != TokenKind.EffectTag)
                {
                    continue;
                }

                string tagName = token.GetNameSpan(markup).ToString();
                if (!TextStudioPhaseTagUtility.TryResolveEffectId(
                        tagName,
                        out _))
                {
                    continue;
                }

                string openingTag = markup.Substring(
                    token.outerStart,
                    token.outerEnd - token.outerStart);
                if (!TextStudioEffectInvocationParser.TryParse(
                        openingTag,
                        out TextStudioEffectInvocation invocation,
                        out _))
                {
                    continue;
                }

                bindings.Clear();
                resolver.TryResolve(
                    invocation.EffectId,
                    invocation.Parameters,
                    invocation.PhaseMask,
                    bindings,
                    null,
                    invocation.Intensity,
                    invocation.TimeOffset);
                if (!CollectTargetConsumers(
                        bindings,
                        targetFields,
                        uniqueTargetFields,
                        scopeFields))
                    continue;

                taggedRangeCount++;
                if (counts.TryGetValue(tagName, out int count))
                {
                    counts[tagName] = count + 1;
                }
                else
                {
                    counts.Add(tagName, 1);
                    orderedTags.Add(tagName);
                }
            }

            List<string> labels = new List<string>(orderedTags.Count);
            for (int i = 0; i < orderedTags.Count; i++)
            {
                string tagName = orderedTags[i];
                int count = counts[tagName];
                labels.Add(count == 1
                    ? "<" + tagName + ">"
                    : "<" + tagName + "> (" + count + ")");
            }
            return new TextStudioSpatialTargetConsumerReport(
                taggedRangeCount,
                string.Join(", ", labels),
                targetFields);
        }

        static bool CollectTargetConsumers(
            List<TextStudioResolvedEffectBinding> bindings,
            List<TextStudioInfluenceField> targetFields,
            HashSet<TextStudioInfluenceField> uniqueTargetFields,
            List<TextStudioInfluenceField> scopeFields)
        {
            bool found = false;
            for (int i = 0; i < bindings.Count; i++)
            {
                // Effects such as Lens Magnifier read the shared target directly and declare it as a capability.
                if (ReadsInfluenceTarget(bindings[i].EffectId, bindings[i].Effect))
                    found = true;
                TextStudioEffectScopeChain scope = bindings[i].Scope;
                if (scope == null || !scope.HasTargetDistanceInfluence)
                    continue;

                found = true;
                scopeFields.Clear();
                scope.AppendTargetDistanceFields(scopeFields);
                for (int fieldIndex = 0;
                     fieldIndex < scopeFields.Count;
                     fieldIndex++)
                {
                    TextStudioInfluenceField field = scopeFields[fieldIndex];
                    if (uniqueTargetFields.Add(field))
                        targetFields.Add(field);
                }
            }
            return found;
        }

        static bool ReadsInfluenceTarget(string effectId, ITextStudioEffect effect)
        {
            if (effect is ITextStudioEffectCapabilitiesProvider provider)
                return provider.Capabilities.ReadsInfluenceTarget;
            return !string.IsNullOrEmpty(effectId) &&
                   TextStudioEffects.TryGetDefinition(
                       effectId,
                       out ITextStudioEffectDefinition definition) &&
                   definition is ITextStudioEffectCapabilitiesProvider registered &&
                   registered.Capabilities.ReadsInfluenceTarget;
        }

        internal static void CreateAndRegister()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Target Effect",
                "Target Influence",
                "asset",
                "Choose where to save the target-aware Composed Effect.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioComposedEffectAsset effect = CreateForAuthoring(
                FindUniqueEffectId());
            AssetDatabase.CreateAsset(effect, path);
            Undo.RegisterCreatedObjectUndo(
                effect,
                "Create Target Influence Effect");
            AssetDatabase.SaveAssets();
            TextStudioEffectCatalogUtility.Add(effect);
            Selection.activeObject = effect;
            EditorGUIUtility.PingObject(effect);
        }

        internal static TextStudioComposedEffectAsset CreateForAuthoring(
            string effectId)
        {
            TextStudioCompositionLayer layer =
                TextStudioCompositionLayer.CreateChannel(
                    TextStudioCompositionChannel.Position);
            layer.ConfigureChannelForAuthoring(
                string.Empty,
                "Position",
                TextStudioCompositionChannel.Position,
                TextStudioEffectPlaybackPreset.External,
                TextStudioEffectCurvePreset.Hold,
                new Vector3(0f, 0.75f, 0f),
                0f,
                null,
                TextStudioCompositionColorMode.MultiplyExistingColor,
                TextStudioPhaseMask.Active,
                1f,
                0f,
                PhaseParams.NoOffset,
                TextStudioEffectAreaSettings.Default,
                EffectBlendMode.Additive);
            TextStudioInfluenceField field = layer.SpatialInfluence.Add(
                TextStudioInfluenceFieldShape.TargetDistance);
            field.Configure(
                TextStudioInfluenceFieldShape.TargetDistance,
                TextStudioInfluenceFieldSpace.PlacedText,
                Vector3.zero,
                2f);

            TextStudioComposedEffectAsset effect =
                ScriptableObject.CreateInstance<TextStudioComposedEffectAsset>();
            effect.Initialize(
                string.IsNullOrWhiteSpace(effectId)
                    ? BaseEffectId
                    : effectId.Trim(),
                new[] { layer });
            effect.SetRevealActiveBlendDurationForAuthoring(0f);
            return effect;
        }

        internal static void ShowBrowser()
        {
            List<TextStudioComposedEffectAsset> effects =
                FindTargetInfluenceEffects();
            GenericMenu menu = new GenericMenu();
            if (effects.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(
                    "No target-aware effects found"));
            }
            else
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    TextStudioComposedEffectAsset effect = effects[i];
                    string label = effect.name + "  <" + effect.Id + ">";
                    menu.AddItem(
                        new GUIContent(label),
                        Selection.activeObject == effect,
                        () => SelectEffect(effect));
                }
            }
            menu.ShowAsContext();
        }

        static void SelectEffect(TextStudioComposedEffectAsset effect)
        {
            Selection.activeObject = effect;
            EditorGUIUtility.PingObject(effect);
        }

        static string FindUniqueEffectId()
        {
            HashSet<string> ids = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            AddEffectIds<TextStudioEffectDefinitionAsset>(ids);

            if (!ids.Contains(BaseEffectId))
                return BaseEffectId;
            for (int suffix = 2; suffix < int.MaxValue; suffix++)
            {
                string candidate = BaseEffectId + "-" + suffix;
                if (!ids.Contains(candidate))
                    return candidate;
            }
            return BaseEffectId + "-new";
        }

        static void AddEffectIds<T>(HashSet<string> ids)
            where T : TextStudioEffectDefinitionAsset
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T effect = AssetDatabase.LoadAssetAtPath<T>(path);
                if (effect != null && !string.IsNullOrWhiteSpace(effect.Id))
                    ids.Add(effect.Id);
            }
        }

        static List<TextStudioComposedEffectAsset> FindTargetInfluenceEffects()
        {
            List<TextStudioComposedEffectAsset> effects =
                new List<TextStudioComposedEffectAsset>();
            string[] guids = AssetDatabase.FindAssets(
                "t:TextStudioComposedEffectAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TextStudioComposedEffectAsset effect =
                    AssetDatabase.LoadAssetAtPath<TextStudioComposedEffectAsset>(
                        path);
                if (effect != null && UsesTarget(effect))
                    effects.Add(effect);
            }
            effects.Sort((left, right) => string.Compare(
                left.name,
                right.name,
                StringComparison.OrdinalIgnoreCase));
            return effects;
        }

        static bool UsesTarget(
            TextStudioComposedEffectAsset effect)
        {
            IReadOnlyList<TextStudioCompositionLayer> layers = effect.Layers;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                TextStudioCompositionLayer layer = layers[layerIndex];
                if (layer == null)
                    continue;
                if (layer.Kind == TextStudioCompositionLayerKind.Effect &&
                    ReadsInfluenceTarget(layer.EffectId, null))
                    return true;
                IReadOnlyList<TextStudioInfluenceField> fields =
                    layer.SpatialInfluence.Fields;
                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    TextStudioInfluenceField field = fields[fieldIndex];
                    if (field != null &&
                        field.Shape ==
                        TextStudioInfluenceFieldShape.TargetDistance)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioSpatialTargetGizmoDrawer
    {
        static readonly Color ActiveColor =
            new Color(0.2f, 0.82f, 1f, 0.9f);
        static readonly Color InactiveColor =
            new Color(1f, 0.62f, 0.2f, 0.9f);

        static TextStudioText s_cachedHub;
        static string s_cachedMarkup = string.Empty;
        static uint s_cachedRegistryRevision;
        static double s_nextAnalysisTime;
        static TextStudioSpatialTargetConsumerReport s_cachedReport;
        static GUIStyle s_labelStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawTargetRadius(TextStudioText hub, GizmoType gizmoType)
        {
            if (hub == null || hub.InfluenceTarget == null)
                return;

            Transform target = hub.InfluenceTarget;
            if (!Selection.Contains(hub.gameObject) &&
                !Selection.Contains(target.gameObject))
            {
                return;
            }

            TextStudioSpatialTargetConsumerReport report = Analyze(hub);
            Color color = report.HasConsumers ? ActiveColor : InactiveColor;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.color = color;

            for (int i = 0; i < report.TargetFields.Count; i++)
            {
                TextStudioInfluenceField field = report.TargetFields[i];
                if (field.Space ==
                    TextStudioInfluenceFieldSpace.SourceTextNormalized)
                {
                    continue;
                }

                bool worldSpace = field.Space ==
                    TextStudioInfluenceFieldSpace.World;
                Gizmos.matrix = worldSpace
                    ? Matrix4x4.identity
                    : hub.transform.localToWorldMatrix;
                Vector3 center = worldSpace
                    ? target.position
                    : hub.transform.InverseTransformPoint(target.position);
                Gizmos.DrawWireSphere(center, field.Radius);
            }

            Gizmos.matrix = Matrix4x4.identity;
            float markerSize = HandleUtility.GetHandleSize(target.position) *
                0.055f;
            Gizmos.DrawSphere(target.position, markerSize);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            string consumerText = report.HasConsumers
                ? report.TagList
                : "No consuming content tag";
            string radiusText = BuildRadiusText(report.TargetFields);
            Handles.Label(
                target.position + Vector3.up * markerSize * 2.5f,
                "Shared Spatial Target\n" + consumerText + radiusText,
                GetLabelStyle());
        }

        static TextStudioSpatialTargetConsumerReport Analyze(
            TextStudioText hub)
        {
            string markup = hub.Markup ?? string.Empty;
            uint registryRevision = TextStudioRegistry.Effects.Revision;
            double now = EditorApplication.timeSinceStartup;
            if (s_cachedHub != hub ||
                !string.Equals(
                    s_cachedMarkup,
                    markup,
                    StringComparison.Ordinal) ||
                s_cachedRegistryRevision != registryRevision ||
                now >= s_nextAnalysisTime)
            {
                s_cachedHub = hub;
                s_cachedMarkup = markup;
                s_cachedRegistryRevision = registryRevision;
                s_cachedReport =
                    TextStudioSpatialTargetEffectUtility.AnalyzeContent(markup);
                s_nextAnalysisTime = now + 0.5d;
            }
            return s_cachedReport;
        }

        static string BuildRadiusText(
            IReadOnlyList<TextStudioInfluenceField> fields)
        {
            if (fields == null || fields.Count == 0)
                return string.Empty;

            List<string> labels = new List<string>(fields.Count);
            for (int i = 0; i < fields.Count; i++)
            {
                TextStudioInfluenceField field = fields[i];
                labels.Add(
                    field.Space ==
                    TextStudioInfluenceFieldSpace.SourceTextNormalized
                        ? "Normalized radius " + field.Radius.ToString("0.###")
                        : "Radius " + field.Radius.ToString("0.###") +
                          " (" + FormatEnum(field.Space) + ")");
            }
            return "\n" + string.Join(", ", labels);
        }

        static string FormatEnum(Enum value)
        {
            return ObjectNames.NicifyVariableName(value.ToString());
        }

        static GUIStyle GetLabelStyle()
        {
            if (s_labelStyle != null)
                return s_labelStyle;

            s_labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };
            s_labelStyle.normal.textColor = Color.white;
            return s_labelStyle;
        }
    }
}
