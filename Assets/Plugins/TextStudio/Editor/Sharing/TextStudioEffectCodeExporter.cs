using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioEffectCodeExporter
    {
        internal static bool TryCreatePackage(
            TextStudioEffectDefinitionAsset root,
            out TextStudioEffectCodePackage package,
            out string error)
        {
            package = null;
            error = string.Empty;
            if (root == null)
            {
                error = "Select a Text Studio effect before copying an Effect Code.";
                return false;
            }

            try
            {
                ExportGraph graph = new ExportGraph();
                package = graph.Build(root);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = "Text Studio could not create an Effect Code. " + exception.Message;
                return false;
            }
        }

        internal static string CreateClipboardText(
            TextStudioEffectCodePackage package,
            string token)
        {
            TextStudioEffectCodeAsset root = package.Root;
            string name = root != null && !string.IsNullOrWhiteSpace(root.Name) ? root.Name.Trim() : "Shared Effect";
            string id = root != null ? root.EffectId : string.Empty;
            int layerCount = root?.Composition?.Layers.Count ?? 0;
            int dependencyCount = Math.Max(0, package.Assets.Count - 1);
            string kind = root != null && root.Kind == TextStudioEffectCodeAssetKind.Composed
                ? layerCount + (layerCount == 1 ? " layer" : " layers")
                : "custom effect";
            string dependencies = dependencyCount == 0 && package.RequiredEffects.Count == 0
                ? "self-contained"
                : dependencyCount + " bundled, " + package.RequiredEffects.Count + " required";
            string effectId = string.IsNullOrWhiteSpace(id) ? string.Empty : " · <" + id + ">";
            return "Text Studio Effect: " + name + effectId + " · " + kind + " · " + dependencies + "\n" +
                   token;
        }

        sealed class ExportGraph
        {
            readonly TextStudioEffectCodePackage _package = new TextStudioEffectCodePackage
            {
                WriterVersion = TextStudioEditorInfo.Version
            };
            readonly Dictionary<UnityEngine.Object, int> _indices =
                new Dictionary<UnityEngine.Object, int>();
            readonly Dictionary<string, TextStudioEffectCodeRequirement> _requiredEffects =
                new Dictionary<string, TextStudioEffectCodeRequirement>(StringComparer.OrdinalIgnoreCase);

            public TextStudioEffectCodePackage Build(TextStudioEffectDefinitionAsset root)
            {
                TextStudioRegistry.EnsureInitialized();
                _package.RootIndex = AddAsset(root);
                foreach (TextStudioEffectCodeRequirement requirement in _requiredEffects.Values)
                    _package.RequiredEffects.Add(requirement);
                _package.RequiredEffects.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.EffectId, right.EffectId));
                TextStudioEffectCodeFormat.AddRequiredCapabilities(_package);
                return _package;
            }

            int AddAsset(ScriptableObject source)
            {
                if (source == null)
                    throw new InvalidOperationException("An Effect Code dependency is missing.");
                if (_indices.TryGetValue(source, out int existing))
                    return existing;
                if (_package.Assets.Count >= TextStudioEffectCodeFormat.MaxAssetCount)
                    throw new InvalidOperationException("This effect contains more than 256 bundled assets and cannot be shared as one code.");

                int index = _package.Assets.Count;
                _indices.Add(source, index);
                TextStudioEffectCodeAsset asset = new TextStudioEffectCodeAsset
                {
                    Index = index,
                    Name = source.name ?? string.Empty,
                    DeclaredEffectId = source is TextStudioEffectDefinitionAsset definition
                        ? definition.Id
                        : string.Empty
                };
                _package.Assets.Add(asset);

                if (source is TextStudioComposedEffectAsset composed)
                {
                    if (!composed.TryCompileForValidation(out string validationError))
                        throw new InvalidOperationException(
                            "Fix this Composed Effect before sharing it. " + validationError);
                    asset.Kind = TextStudioEffectCodeAssetKind.Composed;
                    asset.Composition = ReadComposition(composed);
                    DiscoverCompositionDependencies(asset.Composition);
                }
                else
                {
                    asset.Kind = TextStudioEffectCodeAssetKind.Serialized;
                    Type type = source.GetType();
                    asset.TypeName = type.FullName ?? type.Name;
                    asset.AssemblyName = type.Assembly.GetName().Name;
                    SerializeWithReferences(source, asset);
                }
                return index;
            }

            TextStudioEffectCodeComposition ReadComposition(TextStudioComposedEffectAsset source)
            {
                SerializedObject serialized = new SerializedObject(source);
                SerializedProperty tag = Required(serialized.FindProperty("_tagStack"), "tag stack");
                TextStudioEffectCodeComposition composition = new TextStudioEffectCodeComposition
                {
                    Id = Required(serialized.FindProperty("_id"), "effect id").stringValue,
                    AuthoringSpace = (TextStudioCompositionAuthoringSpace)Required(
                        serialized.FindProperty("_authoringSpace"),
                        "authoring space").enumValueIndex,
                    RevealActiveBlendDuration = Required(
                        serialized.FindProperty("_revealActiveBlendDuration"),
                        "reveal blend duration").floatValue,
                    TagEnabled = Required(tag.FindPropertyRelative("_enabled"), "tag enabled state").boolValue,
                    OpeningMarkup = Required(tag.FindPropertyRelative("_openingMarkup"), "opening markup").stringValue,
                    ClosingMode = (TextStudioTagExpansionClosingMode)Required(
                        tag.FindPropertyRelative("_closingMode"),
                        "closing mode").enumValueIndex,
                    CustomClosingMarkup = Required(
                        tag.FindPropertyRelative("_customClosingMarkup"),
                        "custom closing markup").stringValue
                };

                SerializedProperty layers = Required(serialized.FindProperty("_layers"), "layers");
                if (layers.arraySize > TextStudioEffectCodeFormat.MaxLayerCount)
                    throw new InvalidOperationException("This effect contains more than 4096 layers and cannot be shared as one code.");
                for (int i = 0; i < layers.arraySize; i++)
                    composition.Layers.Add(ReadLayer(layers.GetArrayElementAtIndex(i)));
                return composition;
            }

            TextStudioEffectCodeLayer ReadLayer(SerializedProperty property)
            {
                TextStudioCompositionLayerKind kind = (TextStudioCompositionLayerKind)Required(
                    property.FindPropertyRelative("_kind"),
                    "layer kind").enumValueIndex;
                TextStudioCompositionChannel channel = (TextStudioCompositionChannel)Required(
                    property.FindPropertyRelative("_channel"),
                    "layer channel").enumValueIndex;
                string effectId = Required(property.FindPropertyRelative("_effectId"), "effect id").stringValue;
                TextStudioEffectCodeLayer layer = TextStudioEffectCodeFormat.CreateFrozenDefaults(kind, channel, effectId);
                SerializedProperty renderPropertyId = property.FindPropertyRelative("_renderPropertyId");
                layer.RenderPropertyId = renderPropertyId != null ? renderPropertyId.stringValue : string.Empty;
                layer.Enabled = Required(property.FindPropertyRelative("_enabled"), "layer enabled state").boolValue;
                layer.Name = Required(property.FindPropertyRelative("_name"), "layer name").stringValue;
                layer.EffectParameters = Required(
                    property.FindPropertyRelative("_effectParameters"),
                    "effect parameters").stringValue;
                SerializedProperty parameters = property.FindPropertyRelative("_effectParameterEntries");
                if (parameters != null && parameters.isArray)
                {
                    for (int parameterIndex = 0; parameterIndex < parameters.arraySize; parameterIndex++)
                    {
                        SerializedProperty parameter = parameters.GetArrayElementAtIndex(parameterIndex);
                        layer.Parameters.Add(new TextStudioEffectCodeParameter
                        {
                            Name = Required(parameter.FindPropertyRelative("_name"), "parameter name").stringValue,
                            Value = Required(parameter.FindPropertyRelative("_value"), "parameter value").stringValue,
                            Operation = (TextStudioTagParameterOperation)Required(
                                parameter.FindPropertyRelative("_operation"),
                                "parameter operation").enumValueIndex,
                            Baseline = Required(
                                parameter.FindPropertyRelative("_baseline"),
                                "parameter baseline").stringValue
                        });
                    }
                }
                layer.Phases = (TextStudioPhaseMask)Required(
                    property.FindPropertyRelative("_phases"),
                    "layer phases").intValue;
                layer.Playback = (TextStudioEffectPlaybackPreset)Required(
                    property.FindPropertyRelative("_playback"),
                    "layer playback").enumValueIndex;
                UnityEngine.Object customPlayback = Required(
                    property.FindPropertyRelative("_customPlayback"),
                    "custom playback").objectReferenceValue;
                if (customPlayback != null)
                {
                    if (customPlayback is not EffectPlaybackAsset playbackAsset)
                        throw new InvalidOperationException("A composed layer references an unsupported custom playback object.");
                    layer.CustomPlaybackIndex = AddAsset(playbackAsset);
                }
                layer.Curve = (TextStudioEffectCurvePreset)Required(
                    property.FindPropertyRelative("_curve"),
                    "layer curve").enumValueIndex;
                UnityEngine.Object customCurveAsset = property
                    .FindPropertyRelative("_customCurveAsset")?.objectReferenceValue;
                if (customCurveAsset != null)
                {
                    if (customCurveAsset is not TextStudioCurveProfile curveAsset)
                        throw new InvalidOperationException(
                            "A composed layer references an unsupported Curve asset object.");
                    layer.CustomCurveAssetIndex = AddAsset(curveAsset);
                }
                UnityEngine.Object writingStyle = property
                    .FindPropertyRelative("_writingStyle")?.objectReferenceValue;
                if (writingStyle != null)
                {
                    if (writingStyle is not TextStudioWritingStyleAsset styleAsset)
                    {
                        throw new InvalidOperationException(
                            "A composed Writing layer references an unsupported Writing Style object.");
                    }
                    layer.WritingStyleIndex = AddAsset(styleAsset);
                }
                layer.CustomCurve = Required(
                    property.FindPropertyRelative("_customCurve"),
                    "custom curve").animationCurveValue;
                layer.Vector = Required(property.FindPropertyRelative("_vector"), "layer vector").vector3Value;
                layer.BottomLeft = Required(
                    property.FindPropertyRelative("_bottomLeftCorner"),
                    "bottom-left corner").vector2Value;
                layer.TopLeft = Required(
                    property.FindPropertyRelative("_topLeftCorner"),
                    "top-left corner").vector2Value;
                layer.TopRight = Required(
                    property.FindPropertyRelative("_topRightCorner"),
                    "top-right corner").vector2Value;
                layer.BottomRight = Required(
                    property.FindPropertyRelative("_bottomRightCorner"),
                    "bottom-right corner").vector2Value;
                layer.ColorMode = (TextStudioCompositionColorMode)Required(
                    property.FindPropertyRelative("_colorMode"),
                    "color mode").enumValueIndex;
                layer.ColorGradient = Required(
                    property.FindPropertyRelative("_colorGradient"),
                    "color gradient").gradientValue;
                layer.Scalar = Required(property.FindPropertyRelative("_scalar"), "layer scalar").floatValue;
                layer.Unsigned = Required(
                    property.FindPropertyRelative("_unsigned"),
                    "layer unsigned value").uintValue;
                layer.Intensity = Required(property.FindPropertyRelative("_intensity"), "layer intensity").floatValue;
                layer.TimeOffset = Required(property.FindPropertyRelative("_timeOffset"), "layer time offset").floatValue;
                SerializedProperty timing = Required(property.FindPropertyRelative("_timing"), "layer timing");
                layer.Timing = new PhaseParams
                {
                    charOffset = Required(timing.FindPropertyRelative("charOffset"), "character offset").floatValue,
                    wordOffset = Required(timing.FindPropertyRelative("wordOffset"), "word offset").floatValue,
                    lineOffset = Required(timing.FindPropertyRelative("lineOffset"), "line offset").floatValue,
                    speed = Required(timing.FindPropertyRelative("speed"), "timing speed").floatValue
                };
                SerializedProperty sequence = Required(property.FindPropertyRelative("_sequence"), "layer sequence");
                layer.Sequence = new TextStudioSequenceSettings
                {
                    Order = (TextStudioSequenceOrder)Required(
                        sequence.FindPropertyRelative("Order"),
                        "sequence order").enumValueIndex,
                    Spread = Required(sequence.FindPropertyRelative("Spread"), "sequence spread").floatValue,
                    Direction = (TextStudioSequenceDirection)Required(sequence.FindPropertyRelative("Direction"), "sequence direction").intValue,
                    Seed = Required(sequence.FindPropertyRelative("Seed"), "sequence seed").intValue
                };
                SerializedProperty area = Required(property.FindPropertyRelative("_area"), "layer area");
                layer.Area = new TextStudioEffectAreaSettings(
                    (TextStudioEffectAreaMode)Required(area.FindPropertyRelative("_mode"), "area mode").enumValueIndex,
                    Required(area.FindPropertyRelative("_groupSize"), "area group size").intValue);
                layer.BlendMode = (EffectBlendMode)Required(
                    property.FindPropertyRelative("_blendMode"),
                    "blend mode").enumValueIndex;
                SerializedProperty surfaceMask = property.FindPropertyRelative("_surfaceMask");
                layer.SurfaceMask = surfaceMask != null
                    ? (TextStudioSurfaceMask)surfaceMask.intValue
                    : TextStudioSurfaceMask.All;
                SerializedProperty spatialInfluence = property.FindPropertyRelative("_spatialInfluence");
                if (spatialInfluence?.boxedValue is TextStudioInfluenceFieldStack stack && stack.Count > 0)
                    layer.SpatialInfluenceJson = JsonUtility.ToJson(stack);
                SerializedProperty sourceColorSelector = property.FindPropertyRelative("_sourceColorSelector");
                if (sourceColorSelector?.boxedValue is TextStudioSourceColorSelector selector &&
                    selector.Enabled)
                {
                    layer.SourceColorSelectorJson = JsonUtility.ToJson(selector);
                }
                SerializedProperty logicalSelector = property.FindPropertyRelative("_selector");
                if (logicalSelector != null &&
                    logicalSelector.boxedValue is TextStudioApplicationSelector selection &&
                    selection.GetNodesUnsafe()?.Length > 0)
                    layer.SelectorJson = JsonUtility.ToJson(selection);
                return layer;
            }

            void DiscoverCompositionDependencies(TextStudioEffectCodeComposition composition)
            {
                for (int i = 0; i < composition.Layers.Count; i++)
                {
                    TextStudioEffectCodeLayer layer = composition.Layers[i];
                    if (layer.Kind == TextStudioCompositionLayerKind.Effect)
                        DiscoverEffectDependency(layer.EffectId);
                }

                if (string.IsNullOrWhiteSpace(composition.OpeningMarkup))
                    return;
                List<string> invocations = new List<string>();
                if (!TextStudioTransitionStyleUtility.TrySplitInvocations(
                        composition.OpeningMarkup,
                        invocations,
                        out string error))
                    throw new InvalidOperationException("The composed effect tag stack cannot be shared: " + error);
                for (int i = 0; i < invocations.Count; i++)
                {
                    if (!TextStudioEffectInvocationParser.TryParse(
                            invocations[i],
                            out TextStudioEffectInvocation invocation,
                            out error))
                        throw new InvalidOperationException("The composed effect tag stack cannot be shared: " + error);
                    DiscoverEffectDependency(invocation.EffectId);
                }
            }

            void DiscoverEffectDependency(string effectId)
            {
                if (string.IsNullOrWhiteSpace(effectId) || TextStudioEffectCodeFormat.GetKnownEffectWireId(effectId) != 0)
                    return;
                if (TextStudioRegistry.Effects.TryGetQuiet(effectId, out ITextStudioEffectDefinition definition))
                {
                    if (definition is TextStudioEffectDefinitionAsset asset)
                        AddAsset(asset);
                    else
                        AddExternalRequirement(effectId, definition.GetType());
                    return;
                }
                AddExternalRequirement(effectId, null);
            }

            void AddExternalRequirement(string effectId, Type type)
            {
                if (_requiredEffects.ContainsKey(effectId))
                    return;
                _requiredEffects.Add(effectId, new TextStudioEffectCodeRequirement
                {
                    EffectId = effectId,
                    TypeName = type != null ? type.FullName ?? type.Name : string.Empty,
                    AssemblyName = type != null ? type.Assembly.GetName().Name : string.Empty
                });
            }

            void SerializeWithReferences(ScriptableObject source, TextStudioEffectCodeAsset asset)
            {
                Type type = source.GetType();
                ScriptableObject clone = ScriptableObject.CreateInstance(type);
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source, false), clone);
                    SerializedObject sourceSerialized = new SerializedObject(source);
                    SerializedObject cloneSerialized = new SerializedObject(clone);
                    SerializedProperty iterator = sourceSerialized.GetIterator();
                    bool enterChildren = true;
                    while (iterator.Next(enterChildren))
                    {
                        enterChildren = false;
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                            string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal))
                            continue;
                        UnityEngine.Object referenced = iterator.objectReferenceValue;
                        if (referenced == null)
                            continue;

                        TextStudioEffectCodeReference reference = CreateReference(iterator.propertyPath, referenced);
                        asset.References.Add(reference);
                        SerializedProperty cloneProperty = cloneSerialized.FindProperty(iterator.propertyPath);
                        if (cloneProperty == null)
                            throw new InvalidOperationException(
                                "The custom effect reference '" + iterator.propertyPath + "' could not be prepared for sharing.");
                        cloneProperty.objectReferenceValue = null;
                    }
                    cloneSerialized.ApplyModifiedPropertiesWithoutUndo();
                    asset.Json = EditorJsonUtility.ToJson(clone, false);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }

            TextStudioEffectCodeReference CreateReference(string propertyPath, UnityEngine.Object referenced)
            {
                TextStudioEffectCodeReference reference = new TextStudioEffectCodeReference
                {
                    PropertyPath = propertyPath
                };
                if (referenced is TextStudioEffectDefinitionAsset ||
                    referenced is TextStudioCurveProfile ||
                    referenced is EffectPlaybackAsset ||
                    referenced is TextStudioWritingStyleAsset ||
                    referenced is WritingFontAsset ||
                    referenced is WritingPhraseAsset)
                {
                    if (referenced is not ScriptableObject scriptable)
                        throw new InvalidOperationException("A Text Studio dependency is not a serializable asset.");
                    reference.LocalAssetIndex = AddAsset(scriptable);
                    return reference;
                }

                if (!EditorUtility.IsPersistent(referenced) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(referenced, out string guid, out long localId))
                    throw new InvalidOperationException(
                        "The custom effect references an unsaved object at '" + propertyPath + "'. Save that dependency as an asset first.");
                Type type = referenced.GetType();
                reference.Guid = guid;
                reference.LocalFileId = localId;
                reference.TypeName = type.FullName ?? type.Name;
                reference.AssemblyName = type.Assembly.GetName().Name;
                return reference;
            }

            static SerializedProperty Required(SerializedProperty property, string description)
            {
                if (property == null)
                    throw new InvalidOperationException(
                        "This Text Studio version cannot read the " + description + " needed for an Effect Code.");
                return property;
            }
        }
    }
}
