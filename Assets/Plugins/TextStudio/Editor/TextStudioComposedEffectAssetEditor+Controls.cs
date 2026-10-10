using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioComposedEffectAssetEditor
    {
        VisualElement CreateMultiObjectInspector()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-composed-effect-editor-root");
            root.AddToClassList("ts-composed-effect-editor-root");
            VisualElement section = CreateSection("Multiple Composed Effects");
            section.name = "ts-composed-multi-object";
            section.Add(TextStudioInspectorElements.CreateStatus(
                "Shared settings can be edited together. Select one asset to edit its Tag ID, Tag Stack, ordered layers, and production preview.",
                CommonInspectorStatusType.Info,
                "ts-composed-multi-object-guidance"));
            section.Add(CreateBoundField(
                _authoringSpace,
                "Authoring Space",
                "ts-composed-multi-authoring-space"));
            section.Add(CreateBoundField(
                _revealActiveBlendDuration,
                "Reveal Blend",
                "ts-composed-multi-reveal-active-blend"));
            root.Add(section);
            return root;
        }

        static VisualElement CreateSection(string title)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("ts-section");
            Label label = new Label(title);
            label.AddToClassList("ts-section-title");
            section.Add(label);
            return section;
        }

        static VisualElement CreateFoldoutSection(
            string title,
            string name,
            bool expanded,
            out Foldout foldout)
        {
            VisualElement section = new VisualElement
            {
                name = name
            };
            section.AddToClassList("ts-section");
            section.AddToClassList("ts-composed-foldout-section");
            foldout = new Foldout
            {
                name = name + "-foldout",
                text = title,
                value = expanded
            };
            foldout.AddToClassList("ts-composed-section-foldout");
            section.Add(foldout);
            return section;
        }

        string BuildFoldoutStateScope()
        {
            Object[] inspectedTargets = targets;
            Object inspectedTarget = inspectedTargets != null && inspectedTargets.Length > 0
                ? inspectedTargets[0]
                : null;
            if (inspectedTarget != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    inspectedTarget,
                    out string guid,
                    out long localId))
            {
                return guid + "." + localId;
            }

            return inspectedTarget != null
                ? "instance." + inspectedTarget.GetStableId()
                : "none";
        }

        bool GetFoldoutState(string id, bool defaultValue)
        {
            return SessionState.GetBool(GetFoldoutStateKey(id), defaultValue);
        }

        void RememberFoldoutState(Foldout foldout, string id)
        {
            string key = GetFoldoutStateKey(id);
            foldout.RegisterValueChangedCallback(evt => SessionState.SetBool(key, evt.newValue));
        }

        string GetFoldoutStateKey(string id)
        {
            if (string.IsNullOrEmpty(_foldoutStateScope))
                _foldoutStateScope = BuildFoldoutStateScope();
            return FoldoutStatePrefix + _foldoutStateScope + "." + id;
        }

        static VisualElement CreateAddRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-composed-add-row");
            return row;
        }

        VisualElement CreateAssistedTagField(
            SerializedProperty property,
            string label,
            string name)
        {
            VisualElement editor = new VisualElement
            {
                name = name + "-editor"
            };
            editor.AddToClassList("ts-composed-tag-editor");

            TextField field = new TextField(label)
            {
                name = name,
                bindingPath = property.propertyPath,
                multiline = true
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList("ts-source-field-wrap");
            field.AddToClassList("unity-base-field__aligned");
            TextStudioInspectorElements.ConfigureCodeTextField(field);
            editor.Add(field);

            string propertyPath = property.propertyPath;
            TextStudioInlineTagHelpElement inlineHelp = new TextStudioInlineTagHelpElement(
                source => TextStudioTagInspectorContext.Build(null, source),
                null,
                edit => ApplyAssistedTagEdit(propertyPath, field, edit))
            {
                name = name + "-inline-help"
            };
            inlineHelp.AttachSourceField(field);
            editor.Add(inlineHelp);
            return editor;
        }

        void ApplyAssistedTagEdit(
            string propertyPath,
            TextField field,
            TextStudioTagSourceEditor.EditResult edit)
        {
            if (serializedObject == null ||
                serializedObject.targetObject == null ||
                field == null ||
                string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty current = serializedObject.FindProperty(propertyPath);
            if (current == null || current.propertyType != SerializedPropertyType.String)
                return;

            Undo.RecordObjects(serializedObject.targetObjects, "Edit Composed Effect Tag");
            current.stringValue = edit.Text;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
                EditorUtility.SetDirty(serializedObject.targetObjects[i]);

            field.SetValueWithoutNotify(edit.Text);
        }

        static PropertyField CreateBoundField(SerializedProperty property, string label, string name)
        {
            PropertyField field = new PropertyField(property, label)
            {
                name = name
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList("ts-composed-layer-aligned-field");
            return field;
        }

        static HelpBox CreateHelpBox(string message, HelpBoxMessageType type)
        {
            return TextStudioInspectorElements.CreateHelpBox(message, type);
        }

        static void AddInspectorStyleSheet(VisualElement root)
        {
            StyleSheet style = TextStudioTextEditor.LoadTextStudioInspectorStyleSheet();
            if (style != null)
                root.styleSheets.Add(style);
        }

        static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }

        bool TryCreateExportSnapshot(
            out TextStudioPreviewExportSnapshot snapshot,
            out string error)
        {
            if (serializedObject != null && serializedObject.targetObject != null)
                serializedObject.UpdateIfRequiredOrScript();
            return TextStudioPreviewExportSnapshot.TryCreate(
                target as TextStudioComposedEffectAsset,
                _previewSegments,
                _previewSpeed,
                _activePreviewDurationMultiplier,
                out snapshot,
                out error,
                SoloLayerStableId);
        }
    }
}

namespace TextStudio.Editor
{
    internal readonly struct TextStudioComposedLayerPresentationInfo
    {
        internal string CostClass { get; }
        internal int CostRank { get; }
        internal string RendererSupport { get; }
        internal string ReducedMotion { get; }
        internal string Bounds { get; }
        internal TextStudioReducedMotionBehavior ReducedMotionBehavior { get; }
        internal TextStudioEffectBoundsBehavior BoundsBehavior { get; }

        internal TextStudioComposedLayerPresentationInfo(
            string costClass,
            int costRank,
            TextStudioRenderDomain rendererDomains,
            TextStudioReducedMotionBehavior reducedMotionBehavior,
            TextStudioEffectBoundsBehavior boundsBehavior =
                TextStudioEffectBoundsBehavior.Unchanged)
        {
            CostClass = costClass ?? string.Empty;
            CostRank = Mathf.Max(0, costRank);
            RendererSupport = TextStudioComposedLayerPresentation.FormatRendererSupport(
                rendererDomains);
            ReducedMotionBehavior = reducedMotionBehavior;
            ReducedMotion = TextStudioComposedLayerPresentation.FormatReducedMotion(
                reducedMotionBehavior);
            BoundsBehavior = boundsBehavior;
            Bounds = TextStudioComposedLayerPresentation.FormatBounds(boundsBehavior);
        }

        internal string PickerMetadata(string source)
        {
            List<string> values = new List<string>(5);
            if (!string.IsNullOrWhiteSpace(source))
                values.Add(source.Trim());
            if (!string.IsNullOrWhiteSpace(RendererSupport))
                values.Add(RendererSupport);
            if (!string.IsNullOrWhiteSpace(CostClass))
                values.Add(CostClass);
            if (!string.IsNullOrWhiteSpace(ReducedMotion))
                values.Add(ReducedMotion);
            if (!string.IsNullOrWhiteSpace(Bounds))
                values.Add(Bounds);
            return string.Join(" · ", values);
        }
    }

    internal static class TextStudioComposedLayerPresentation
    {
        internal static TextStudioComposedLayerPresentationInfo ForBuiltInChannel()
        {
            return new TextStudioComposedLayerPresentationInfo(
                "Realtime",
                0,
                TextStudioRenderDomain.All,
                TextStudioReducedMotionBehavior.Reduce);
        }

        internal static TextStudioComposedLayerPresentationInfo ForDescriptor(
            in TextStudioRenderPropertyDescriptor descriptor)
        {
            return new TextStudioComposedLayerPresentationInfo(
                FormatPerformanceClass(descriptor.PerformanceClass),
                (int)TextStudioRenderPropertyCapabilities.ResolvePerformanceTier(
                    descriptor.PerformanceClass),
                descriptor.SupportedRenderDomains,
                descriptor.ReducedMotionBehavior,
                descriptor.BoundsBehavior);
        }

        internal static TextStudioComposedLayerPresentationInfo ForDefinition(
            ITextStudioEffectDefinition definition)
        {
            return ForDefinition(definition, new HashSet<int>());
        }

        static TextStudioComposedLayerPresentationInfo ForDefinition(
            ITextStudioEffectDefinition definition,
            HashSet<int> visitedCompositions)
        {
            if (definition is TextStudioComposedEffectAsset composition)
                return ForComposition(composition, visitedCompositions);
            TextStudioEffectCapabilities capabilities =
                definition is ITextStudioEffectCapabilitiesProvider provider
                    ? provider.Capabilities
                    : new TextStudioEffectCapabilities(
                        TextStudioRenderChannels.All,
                        TextStudioPhaseMask.All);
            return new TextStudioComposedLayerPresentationInfo(
                FormatPerformanceTier(capabilities.PerformanceTier),
                (int)capabilities.PerformanceTier,
                capabilities.SupportedRenderDomains,
                capabilities.ReducedMotionBehavior,
                capabilities.BoundsBehavior);
        }

        internal static TextStudioComposedLayerPresentationInfo ForWriting(
            TextStudioWritingStyleAsset style)
        {
            TextStudioWritingConfiguration configuration = style != null
                ? style.Configuration
                : TextStudioWritingConfiguration.Default;
            TextStudioWritingDataLevel level =
                TextStudioWritingModeUtility.RequestedDataLevel(configuration.Mode);
            string cost = level switch
            {
                TextStudioWritingDataLevel.None => "No render work",
                TextStudioWritingDataLevel.OrdinaryTypewriter => "Visibility only",
                TextStudioWritingDataLevel.InkCoverage => "Source coverage",
                TextStudioWritingDataLevel.PreparedOutline => "Retained outline",
                TextStudioWritingDataLevel.AuthoredCenterline => "Retained path",
                _ => "Writing"
            };
            return new TextStudioComposedLayerPresentationInfo(
                cost,
                (int)level,
                TextStudioRenderDomain.FlatText |
                TextStudioRenderDomain.Extruded3D,
                configuration.ReducedMotionBehavior);
        }

        internal static TextStudioComposedLayerPresentationInfo ForLayer(
            TextStudioCompositionLayer layer)
        {
            return ForLayer(layer, new HashSet<int>());
        }

        static TextStudioComposedLayerPresentationInfo ForLayer(
            TextStudioCompositionLayer layer,
            HashSet<int> visitedCompositions)
        {
            if (layer == null)
                return ForBuiltInChannel();

            if (layer.Kind == TextStudioCompositionLayerKind.Writing)
                return ForWriting(layer.WritingStyle);
            if (layer.Kind == TextStudioCompositionLayerKind.Effect)
            {
                TextStudioRegistry.EnsureInitialized();
                return TextStudioRegistry.Effects.TryGetQuiet(
                    layer.EffectId,
                    out ITextStudioEffectDefinition definition)
                        ? ForDefinition(definition, visitedCompositions)
                        : new TextStudioComposedLayerPresentationInfo(
                            "Unavailable",
                            0,
                            TextStudioRenderDomain.None,
                            TextStudioReducedMotionBehavior.Reduce);
            }
            if (layer.UsesRenderProperty &&
                TextStudioRenderPropertyRegistry.TryGet(
                    layer.RenderPropertyId,
                    out TextStudioRenderPropertyDescriptor descriptor))
            {
                return ForDescriptor(in descriptor);
            }
            return ForBuiltInChannel();
        }

        static TextStudioComposedLayerPresentationInfo ForComposition(
            TextStudioComposedEffectAsset composition,
            HashSet<int> visitedCompositions)
        {
            int instanceId = composition.GetStableId();
            TextStudioRenderDomain domains = composition.RequiredRenderDomain;
            if (!visitedCompositions.Add(instanceId))
            {
                return new TextStudioComposedLayerPresentationInfo(
                    "Composition cycle",
                    5,
                    domains,
                    TextStudioReducedMotionBehavior.Reduce);
            }

            bool hasRawStack = composition.TagStack.Enabled &&
                !string.IsNullOrWhiteSpace(composition.TagStack.OpeningMarkup);
            TextStudioComposedLayerPresentationInfo highest =
                new TextStudioComposedLayerPresentationInfo(
                    hasRawStack ? "Mixed composition" : "Realtime",
                    hasRawStack ? 5 : 0,
                    domains,
                    TextStudioReducedMotionBehavior.Preserve);
            TextStudioReducedMotionBehavior reducedMotion =
                TextStudioReducedMotionBehavior.Preserve;
            TextStudioEffectBoundsBehavior boundsBehavior =
                TextStudioEffectBoundsBehavior.Unchanged;
            IReadOnlyList<TextStudioCompositionLayer> layers = composition.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled)
                    continue;
                TextStudioComposedLayerPresentationInfo candidate =
                    ForLayer(layer, visitedCompositions);
                if (candidate.CostRank > highest.CostRank)
                    highest = candidate;
                TextStudioReducedMotionBehavior layerReducedMotion =
                    candidate.ReducedMotionBehavior;
                if ((int)layerReducedMotion > (int)reducedMotion)
                    reducedMotion = layerReducedMotion;
                if ((int)candidate.BoundsBehavior > (int)boundsBehavior)
                    boundsBehavior = candidate.BoundsBehavior;
            }
            visitedCompositions.Remove(instanceId);
            return new TextStudioComposedLayerPresentationInfo(
                highest.CostClass,
                highest.CostRank,
                domains,
                reducedMotion,
                boundsBehavior);
        }

        internal static string BuildPreviewContext(
            TextStudioComposedEffectAsset effect,
            AnimationPhase phase,
            string soloLayerStableId)
        {
            if (effect == null)
                return string.Empty;

            string layerContext = "All layers";
            int highestRank = -1;
            string highestCost = "Pass-through";
            IReadOnlyList<TextStudioCompositionLayer> layers = effect.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled)
                    continue;
                if (!string.IsNullOrEmpty(soloLayerStableId) &&
                    !string.Equals(
                        layer.StableId,
                        soloLayerStableId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                TextStudioComposedLayerPresentationInfo presentation = ForLayer(layer);
                if (presentation.CostRank >= highestRank)
                {
                    highestRank = presentation.CostRank;
                    highestCost = presentation.CostClass;
                }
                if (!string.IsNullOrEmpty(soloLayerStableId))
                    layerContext = "Solo: " + layer.DisplayName;
            }

            string authoring = effect.AuthoringSpace ==
                TextStudioCompositionAuthoringSpace.ThreeD
                    ? "3D"
                    : "2D";
            return authoring + " · " + FormatPhase(phase) + " · " +
                   layerContext + " · Highest cost: " + highestCost;
        }

        internal static bool TryGetWritingCoverageWarning(
            TextStudioComposedEffectAsset effect,
            out string warning)
        {
            warning = string.Empty;
            if (effect == null)
                return false;

            IReadOnlyList<TextStudioCompositionLayer> layers = effect.Layers;
            for (int firstIndex = 0; firstIndex < layers.Count; firstIndex++)
            {
                TextStudioCompositionLayer first = layers[firstIndex];
                if (!CanUseWritingCoverage(first))
                    continue;
                for (int secondIndex = firstIndex + 1;
                     secondIndex < layers.Count;
                     secondIndex++)
                {
                    TextStudioCompositionLayer second = layers[secondIndex];
                    if (!CanUseWritingCoverage(second) ||
                        (first.Phases & second.Phases) == TextStudioPhaseMask.None)
                    {
                        continue;
                    }

                    warning = "Writing layers '" + first.DisplayName + "' and '" +
                              second.DisplayName +
                              "' overlap. Authored paths remain independent. If both fall back to compact source coverage for the same glyph, the later layer controls its directional reveal.";
                    return true;
                }
            }
            return false;
        }

        internal static string BuildTypewriterUsage(TextStudioComposedEffectAsset effect)
        {
            int transitionLayers = 0;
            int activeOnlyLayers = 0;
            if (effect != null)
            {
                IReadOnlyList<TextStudioCompositionLayer> layers = effect.Layers;
                for (int i = 0; i < layers.Count; i++)
                {
                    TextStudioCompositionLayer layer = layers[i];
                    if (layer == null || !layer.Enabled)
                        continue;
                    bool hasTransition =
                        (layer.Phases & (TextStudioPhaseMask.Reveal |
                                         TextStudioPhaseMask.Hide)) != 0;
                    if (hasTransition)
                        transitionLayers++;
                    else if ((layer.Phases & TextStudioPhaseMask.Active) != 0)
                        activeOnlyLayers++;
                }
            }

            string message =
                "Also usable as a Typewriter treatment. Assign this asset under Typewriter > Motion > Composed Effect; only Reveal and Hide content runs there.";
            if (activeOnlyLayers > 0)
            {
                message += " " + activeOnlyLayers +
                           (activeOnlyLayers == 1 ? " Active-only layer is" : " Active-only layers are") +
                           " intentionally ignored in that context.";
            }
            else if (transitionLayers > 0)
            {
                message += " " + transitionLayers +
                           (transitionLayers == 1 ? " transition layer is" : " transition layers are") +
                           " ready for that lifecycle.";
            }
            return message;
        }

        internal static string FormatRendererSupport(TextStudioRenderDomain domains)
        {
            bool flat = (domains & TextStudioRenderDomain.FlatText) != 0;
            bool extruded = (domains & TextStudioRenderDomain.Extruded3D) != 0;
            if (flat && extruded)
                return "2D + 3D";
            if (flat)
                return "2D";
            if (extruded)
                return "3D";
            return "Unavailable";
        }

        internal static string FormatReducedMotion(
            TextStudioReducedMotionBehavior behavior)
        {
            return behavior switch
            {
                TextStudioReducedMotionBehavior.Preserve =>
                    "Reduced motion: preserved",
                TextStudioReducedMotionBehavior.Disable =>
                    "Reduced motion: disabled",
                _ => "Reduced motion: reduced"
            };
        }

        internal static string FormatBounds(TextStudioEffectBoundsBehavior behavior)
        {
            return behavior switch
            {
                TextStudioEffectBoundsBehavior.DynamicOutput => "Dynamic bounds",
                TextStudioEffectBoundsBehavior.GeometryRefresh =>
                    "Bounds follow geometry refresh",
                _ => string.Empty
            };
        }

        static string FormatPerformanceTier(TextStudioEffectPerformanceTier tier)
        {
            return tier switch
            {
                TextStudioEffectPerformanceTier.VertexStream => "Vertex stream",
                TextStudioEffectPerformanceTier.Material => "Material route",
                TextStudioEffectPerformanceTier.GeometryRefresh =>
                    "Geometry refresh",
                TextStudioEffectPerformanceTier.PreparedDeformation =>
                    "Prepared deformation",
                TextStudioEffectPerformanceTier.Simulation => "Simulation",
                _ => "Realtime"
            };
        }

        static string FormatPerformanceClass(
            TextStudioRenderPropertyPerformanceClass performanceClass)
        {
            return performanceClass switch
            {
                TextStudioRenderPropertyPerformanceClass.VertexStream =>
                    "Vertex stream",
                TextStudioRenderPropertyPerformanceClass.GeometryRefresh =>
                    "Geometry refresh",
                TextStudioRenderPropertyPerformanceClass.PreparedDeformation =>
                    "Prepared deformation",
                TextStudioRenderPropertyPerformanceClass.Simulation =>
                    "Simulation",
                _ => "Realtime"
            };
        }

        static bool CanUseWritingCoverage(TextStudioCompositionLayer layer)
        {
            if (layer == null || !layer.Enabled ||
                layer.Kind != TextStudioCompositionLayerKind.Writing ||
                layer.WritingStyle == null)
            {
                return false;
            }
            TextStudioWritingDataLevel level =
                TextStudioWritingModeUtility.RequestedDataLevel(
                    layer.WritingStyle.Configuration.Mode);
            return level >= TextStudioWritingDataLevel.InkCoverage;
        }


        static string FormatPhase(AnimationPhase phase)
        {
            return phase switch
            {
                AnimationPhase.Appearance => "Reveal",
                AnimationPhase.Disappearance => "Hide",
                _ => "Active"
            };
        }
    }
}
