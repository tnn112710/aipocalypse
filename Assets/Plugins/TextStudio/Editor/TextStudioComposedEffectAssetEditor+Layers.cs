using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioComposedEffectAssetEditor
    {
        VisualElement CreateVisualLayers()
        {
            bool defaultExpanded = target is TextStudioComposedEffectAsset effect && effect.Layers.Count > 0;
            bool expanded = GetFoldoutState("visual-layers", defaultExpanded);
            VisualElement section = CreateFoldoutSection(
                "Visual Layers",
                "ts-composed-layers",
                expanded,
                out Foldout content);
            _visualLayersFoldout = content;
            RememberFoldoutState(content, "visual-layers");
            Label help = new Label(
                "Add and combine animated properties, reusable effects, and Writing treatments.");
            help.AddToClassList("ts-muted-text");
            content.Add(help);

            content.Add(CreateLayerViewToolbar());
            _layerListView = new VisualElement
            {
                name = "ts-composed-layer-list-view"
            };
            _layerListView.AddToClassList("ts-composed-layer-list-view");
            content.Add(_layerListView);

            _layerRows = new VisualElement
            {
                name = "ts-composed-layer-rows"
            };
            _layerListView.Add(_layerRows);

            _layerTimingView = CreateLayerTimingView();
            content.Add(_layerTimingView);
            RebuildLayerRows();
            content.Add(CreateLayerAddArea());
            SetLayerView(GetFoldoutState("layer-timing-view", false));

            Action refreshLayerCount = () =>
            {
                if (target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                int count = _layers != null && _layers.isArray ? _layers.arraySize : 0;
                content.text = count > 0 ? "Visual Layers (" + count + ")" : "Visual Layers";
            };
            if (_layers != null)
                content.TrackPropertyValue(_layers, _ => refreshLayerCount());
            refreshLayerCount();
            return section;
        }

        VisualElement CreateLayerAddArea()
        {
            VisualElement addArea = new VisualElement
            {
                name = "ts-composed-add-area"
            };
            addArea.AddToClassList("ts-composed-add-area");
            addArea.AddToClassList("ts-layer-stack-add-area");
            Button addLayer = TextStudioInspectorElements.CreateButton(
                "+ Add Layer…",
                "Choose an animated property, reusable effect, or Writing treatment. The catalog shows renderer, cost, and reduced-motion support before applying it.",
                null);
            addLayer.name = "ts-composed-add-layer-button";
            addLayer.AddToClassList("ts-composed-add-layer-button");
            addLayer.clicked += () => ShowLayerPicker(addLayer);
            addArea.Add(addLayer);

            Action refreshAvailability = () =>
            {
                if (target is not TextStudioComposedEffectAsset effect)
                    return;

                bool available = TextStudioAuthoringCapabilities.Supports(effect.RequiredRenderDomain);
                addLayer.SetEnabled(available);
                addLayer.tooltip = available
                    ? "Choose an animated property, reusable effect, or Writing treatment. The catalog is filtered for this effect's authoring space."
                    : "Install the 3D add-on to add or replace layers in this 3D Composed Effect.";
            };
            if (_authoringSpace != null)
                addArea.TrackPropertyValue(_authoringSpace, _ => refreshAvailability());
            refreshAvailability();

            return addArea;
        }

        void ShowLayerPicker(Button anchor)
        {
            if (anchor == null || anchor.panel == null || target is not TextStudioComposedEffectAsset effect)
                return;
            TextStudioRenderDomain required = effect.RequiredRenderDomain;
            if (!TextStudioAuthoringCapabilities.Supports(required))
                return;
            EditorWindow owner = FindOwningWindow(anchor);
            LayerPickerWindow.Show(
                owner,
                anchor,
                BuildLayerChoices(effect),
                required,
                TextStudioAuthoringCapabilities.AvailableRenderDomains);
        }

        static EditorWindow FindOwningWindow(VisualElement element)
        {
            if (element?.panel == null)
                return null;

            EditorWindow mouseWindow = EditorWindow.mouseOverWindow;
            if (mouseWindow != null && mouseWindow.rootVisualElement?.panel == element.panel)
                return mouseWindow;

            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null && focusedWindow.rootVisualElement?.panel == element.panel)
                return focusedWindow;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window != null && window.rootVisualElement?.panel == element.panel)
                    return window;
            }

            return null;
        }

        List<LayerChoice> BuildLayerChoices(TextStudioComposedEffectAsset effect)
        {
            List<LayerChoice> choices = new List<LayerChoice>();
            foreach (TextStudioCompositionChannel channel in Enum.GetValues(typeof(TextStudioCompositionChannel)))
            {
                TextStudioCompositionChannel captured = channel;
                string name = channel == TextStudioCompositionChannel.UVOffset
                    ? "UV Offset"
                    : ObjectNames.NicifyVariableName(channel.ToString());
                choices.Add(new LayerChoice(
                    LayerChoiceKind.Property,
                    name,
                    GetBuiltInChannelCategory(channel),
                    "Animate this property as an editable composition layer.",
                    "Built-in",
                    TextStudioRenderDomain.All,
                    TextStudioComposedLayerPresentation.ForBuiltInChannel(),
                    () => AddChannelLayer(captured)));
            }

            List<TextStudioRenderPropertyDescriptor> descriptors = new List<TextStudioRenderPropertyDescriptor>();
            TextStudioRenderPropertyRegistry.GetAll(descriptors);
            for (int i = 0; i < descriptors.Count; i++)
            {
                TextStudioRenderPropertyDescriptor descriptor = descriptors[i];
                string descriptorId = descriptor.Id;
                choices.Add(new LayerChoice(
                    LayerChoiceKind.Property,
                    descriptor.DisplayName,
                    descriptor.Category,
                    descriptor.Description,
                    TextStudioRenderPropertyRegistry.IsBuiltIn(descriptor.Id)
                        ? "Built-in"
                        : "Installed extension",
                    descriptor.SupportedRenderDomains,
                    TextStudioComposedLayerPresentation.ForDescriptor(in descriptor),
                    () => AddRenderPropertyLayer(descriptorId)));
            }

            List<EffectChoiceEntry> effects = BuildEffectChoices(effect);
            for (int i = 0; i < effects.Count; i++)
            {
                EffectChoiceEntry choice = effects[i];
                string effectId = choice.Id;
                choices.Add(new LayerChoice(
                    LayerChoiceKind.Effect,
                    choice.DisplayName,
                    choice.Category,
                    choice.Description,
                    choice.Label.EndsWith("(Composed)", StringComparison.Ordinal)
                        ? "Existing Effect · Composed"
                        : choice.Label.EndsWith("(Custom)", StringComparison.Ordinal)
                            ? "Existing Effect · Custom"
                            : "Existing Effect · Built-in",
                    choice.SupportedDomains,
                    choice.Presentation,
                    () => AddEffectLayer(effectId)));
            }

            choices.Add(new LayerChoice(
                LayerChoiceKind.Writing,
                "Choose Writing Style…",
                "Reveal and Writing",
                "Add a Writing layer, then choose or create its reusable writing treatment.",
                "Text Studio",
                TextStudioRenderDomain.FlatText,
                TextStudioComposedLayerPresentation.ForWriting(null),
                () => AddWritingLayer(null)));
            string[] writingGuids = AssetDatabase.FindAssets(
                "t:TextStudioWritingStyleAsset");
            List<string> writingPaths = new List<string>(writingGuids.Length);
            for (int i = 0; i < writingGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(writingGuids[i]);
                if (!string.IsNullOrEmpty(path))
                    writingPaths.Add(path);
            }
            writingPaths.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < writingPaths.Count; i++)
            {
                TextStudioWritingStyleAsset style =
                    AssetDatabase.LoadAssetAtPath<TextStudioWritingStyleAsset>(
                        writingPaths[i]);
                if (style == null)
                    continue;
                TextStudioWritingStyleAsset captured = style;
                choices.Add(new LayerChoice(
                    LayerChoiceKind.Writing,
                    style.name,
                    "Reveal and Writing",
                    "Use this reusable Writing treatment with Typewriter progress and the composition's Apply Where controls.",
                    "Project asset · " + FormatWritingMode(style.Configuration.Mode),
                    TextStudioRenderDomain.FlatText,
                    TextStudioComposedLayerPresentation.ForWriting(style),
                    () => AddWritingLayer(captured)));
            }

            for (int i = choices.Count - 1; i >= 0; i--)
            {
                if (choices[i].SupportedDomains != TextStudioRenderDomain.None &&
                    !TextStudioAuthoringCapabilities.Intersects(choices[i].SupportedDomains))
                    choices.RemoveAt(i);
            }
            return choices;
        }

        static string FormatWritingMode(TextStudioWritingMode mode)
        {
            return mode switch
            {
                TextStudioWritingMode.AuthoredCenterline => "True Handwriting",
                TextStudioWritingMode.CalligraphyPressure => "Calligraphy",
                TextStudioWritingMode.OutlineTrace => "Outline Trace",
                TextStudioWritingMode.Marker => "Marker",
                TextStudioWritingMode.Chalk => "Chalk",
                TextStudioWritingMode.StrokeToFill => "Stroke to Fill",
                TextStudioWritingMode.InkReveal => "Ink Reveal",
                _ => "Writing"
            };
        }

        static string GetBuiltInChannelCategory(TextStudioCompositionChannel channel)
        {
            return channel switch
            {
                TextStudioCompositionChannel.Position => "Transform",
                TextStudioCompositionChannel.Rotation => "Transform",
                TextStudioCompositionChannel.Scale => "Transform",
                TextStudioCompositionChannel.Pivot => "Transform",
                TextStudioCompositionChannel.Color => "Color and Visibility",
                TextStudioCompositionChannel.Alpha => "Color and Visibility",
                TextStudioCompositionChannel.UVOffset => "Surface and Material",
                TextStudioCompositionChannel.Skew => "2D Design Animation",
                TextStudioCompositionChannel.CornerDeformation => "2D Design Animation",
                _ => "Properties"
            };
        }

        void RebuildLayerRows()
        {
            if (_layerRows == null)
                return;
            _syncingLayerAccordion = true;
            try
            {
                _layerRows.Clear();
                _layerFoldouts.Clear();
                _layerHeaderRefreshes.Clear();
                serializedObject.UpdateIfRequiredOrScript();
                if (_layers == null || !_layers.isArray || _layers.arraySize == 0)
                {
                    _layerRows.Add(TextStudioInspectorElements.CreateMutedText(
                        "No visual layers yet. Add a property or reusable effect below to begin composing."));
                    return;
                }

                PrepareLayerAccordion(_layers);
                string expandedLayerStableId = _expandedLayerStableId;
                AddLayerCards(_layers, _layerRows);
                _layerRows.Bind(serializedObject);
                _expandedLayerStableId = expandedLayerStableId;
                foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
                {
                    bool shouldExpand = string.Equals(
                        pair.Key,
                        expandedLayerStableId,
                        StringComparison.Ordinal);
                    if (pair.Value.value != shouldExpand)
                        pair.Value.SetValueWithoutNotify(shouldExpand);
                }
            }
            finally
            {
                _syncingLayerAccordion = false;
            }
        }

        void PrepareLayerAccordion(SerializedProperty layers)
        {
            string savedStableId = SessionState.GetString(GetFoldoutStateKey("layer-accordion"), string.Empty);
            if (string.Equals(savedStableId, NoExpandedLayerState, StringComparison.Ordinal))
            {
                _expandedLayerStableId = string.Empty;
                return;
            }
            bool savedLayerExists = false;
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty stableId = layers.GetArrayElementAtIndex(i).FindPropertyRelative("_stableId");
                if (stableId != null && string.Equals(stableId.stringValue, savedStableId, StringComparison.Ordinal))
                {
                    savedLayerExists = true;
                    break;
                }
            }

            if (savedLayerExists)
            {
                _expandedLayerStableId = savedStableId;
                return;
            }

            SerializedProperty firstStableId = layers.GetArrayElementAtIndex(0).FindPropertyRelative("_stableId");
            _expandedLayerStableId = firstStableId != null ? firstStableId.stringValue : string.Empty;
            SessionState.SetString(GetFoldoutStateKey("layer-accordion"), _expandedLayerStableId);
        }

        void AddLayerCards(
            SerializedProperty layers,
            VisualElement parent)
        {
            for (int i = 0; i < layers.arraySize; i++)
                parent.Add(CreateLayerCard(layers, i));
        }

        VisualElement CreateLayerCard(
            SerializedProperty siblings,
            int index)
        {
            SerializedProperty layer = siblings.GetArrayElementAtIndex(index);
            SerializedProperty kind = layer.FindPropertyRelative("_kind");
            SerializedProperty channel = layer.FindPropertyRelative("_channel");
            SerializedProperty effectId = layer.FindPropertyRelative("_effectId");
            SerializedProperty writingStyle = layer.FindPropertyRelative("_writingStyle");
            SerializedProperty name = layer.FindPropertyRelative("_name");
            SerializedProperty enabled = layer.FindPropertyRelative("_enabled");
            SerializedProperty phases = layer.FindPropertyRelative("_phases");
            SerializedProperty stableId = layer.FindPropertyRelative("_stableId");
            TextStudioCompositionLayerKind layerKind = (TextStudioCompositionLayerKind)kind.enumValueIndex;
            string stableIdValue = stableId != null ? stableId.stringValue : string.Empty;
            string visualKey = index.ToString();

            string cardName = "ts-composed-layer-" + visualKey;
            TextStudioInspectorElements.LayerCardShell shell = null;
            shell = TextStudioInspectorElements.CreateLayerCardShell(
                cardName,
                BuildLayerTitle(
                    layerKind,
                    channel,
                    effectId,
                    writingStyle,
                    name,
                    enabled,
                    phases),
                string.Equals(_expandedLayerStableId, stableIdValue, StringComparison.Ordinal),
                BuildLayerPhaseTooltip(phases),
                () => ToggleLayerEnabled(enabled),
                () => ToggleSoloLayer(stableIdValue),
                () => MoveLayer(stableIdValue, -1),
                () => MoveLayer(stableIdValue, 1),
                () => RemoveLayer(stableIdValue));
            VisualElement card = shell.Card;
            card.AddToClassList("ts-composed-layer-card");
            Foldout foldout = shell.Foldout;
            foldout.name = "ts-composed-layer-foldout-" + visualKey;
            foldout.viewDataKey = string.Empty;
            _layerFoldouts[stableIdValue] = foldout;
            foldout.RegisterValueChangedCallback(evt => HandleLayerFoldoutChanged(stableIdValue, evt.newValue));
            shell.VisibilityButton.name = "ts-composed-layer-enabled-" + visualKey;
            shell.Actions.name = "ts-composed-layer-quick-actions-" + visualKey;
            shell.SoloButton.name = "ts-composed-layer-solo-" + visualKey;
            shell.MoveUpButton.name = "ts-composed-layer-up-" + visualKey;
            shell.MoveDownButton.name = "ts-composed-layer-down-" + visualKey;
            shell.DeleteButton.name = "ts-composed-layer-remove-" + visualKey;

            Action refreshHeader = () =>
            {
                foldout.tooltip = BuildLayerPhaseTooltip(phases);
                shell.Refresh(
                    BuildLayerTitle(
                        layerKind,
                        channel,
                        effectId,
                        writingStyle,
                        name,
                        enabled,
                        phases),
                    enabled.boolValue,
                    string.Equals(SoloLayerStableId, stableIdValue, StringComparison.Ordinal),
                    index > 0,
                    index < siblings.arraySize - 1);
            };
            refreshHeader();
            _layerHeaderRefreshes[stableIdValue] = refreshHeader;
            SerializedProperty[] headerProperties =
            {
                enabled,
                name,
                channel,
                effectId,
                writingStyle,
                phases
            };
            for (int headerIndex = 0; headerIndex < headerProperties.Length; headerIndex++)
            {
                SerializedProperty headerProperty = headerProperties[headerIndex];
                if (headerProperty != null)
                    foldout.TrackPropertyValue(headerProperty, _ => refreshHeader());
            }

            TextField nameField = new TextField("Name")
            {
                name = "ts-composed-layer-name-" + visualKey,
                bindingPath = name.propertyPath
            };
            nameField.AddToClassList("ts-property-field");
            nameField.AddToClassList("unity-base-field__aligned");
            nameField.AddToClassList("ts-composed-layer-aligned-field");
            nameField.AddToClassList("ts-layer-card-name");
            nameField.BindProperty(name);
            foldout.Add(nameField);
            if (target is TextStudioComposedEffectAsset composition &&
                index < composition.Layers.Count &&
                !TryGetLayerCompatibility(
                    composition,
                    composition.Layers[index],
                    out string compatibilityError))
            {
                Label compatibility = TextStudioInspectorElements.CreateStatus(
                    compatibilityError,
                    CommonInspectorStatusType.Warning,
                    "ts-composed-layer-compatibility");
                foldout.Add(compatibility);
            }
            if (layerKind != TextStudioCompositionLayerKind.Writing)
                foldout.Add(CreateLayerPhaseSelector(phases, visualKey));

            if (layerKind == TextStudioCompositionLayerKind.Channel)
                AddChannelFields(foldout, layer);
            else if (layerKind == TextStudioCompositionLayerKind.Writing)
                AddWritingFields(foldout, layer, stableIdValue);
            else
                AddEffectFields(foldout, layer, index);

            Button duplicate = TextStudioInspectorElements.CreateButton(
                "Duplicate Layer",
                "Duplicate this layer directly after the original.",
                () => DuplicateLayer(stableIdValue));
            duplicate.name = "ts-composed-layer-duplicate-" + visualKey;
            Button extract = TextStudioInspectorElements.CreateButton(
                "Extract as Composed Effect…",
                "Create a reusable Composed Effect from this layer and replace it with a nested reference.",
                () => ExtractLayer(stableIdValue));
            extract.name = "ts-composed-layer-extract-" + visualKey;
            foldout.Add(TextStudioInspectorElements.CreateLayerFooter(duplicate, extract));
            return card;
        }

        void HandleLayerFoldoutChanged(string stableId, bool expanded)
        {
            if (_syncingLayerAccordion)
                return;

            if (!expanded)
            {
                if (string.Equals(_expandedLayerStableId, stableId, StringComparison.Ordinal))
                {
                    _expandedLayerStableId = string.Empty;
                    SessionState.SetString(GetFoldoutStateKey("layer-accordion"), NoExpandedLayerState);
                }
                return;
            }

            _expandedLayerStableId = stableId;
            SessionState.SetString(GetFoldoutStateKey("layer-accordion"), stableId);
            _syncingLayerAccordion = true;
            foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
            {
                if (!string.Equals(pair.Key, stableId, StringComparison.Ordinal) && pair.Value.value)
                    pair.Value.SetValueWithoutNotify(false);
            }
            _syncingLayerAccordion = false;
        }

        void SynchronizeLayerAccordion()
        {
            _syncingLayerAccordion = true;
            foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
            {
                bool shouldExpand = string.Equals(
                    pair.Key,
                    _expandedLayerStableId,
                    StringComparison.Ordinal);
                if (pair.Value.value != shouldExpand)
                    pair.Value.SetValueWithoutNotify(shouldExpand);
            }
            _syncingLayerAccordion = false;
        }

        static void RefreshLayerTitle(
            Foldout foldout,
            TextStudioCompositionLayerKind kind,
            SerializedProperty channel,
            SerializedProperty effectId,
            SerializedProperty writingStyle,
            SerializedProperty name,
            SerializedProperty enabled,
            SerializedProperty phases)
        {
            foldout.text = BuildLayerTitle(
                kind,
                channel,
                effectId,
                writingStyle,
                name,
                enabled,
                phases);
            foldout.tooltip = BuildLayerPhaseTooltip(phases);
        }

        void AddChannelFields(VisualElement parent, SerializedProperty layer)
        {
            SerializedProperty channel = layer.FindPropertyRelative("_channel");
            SerializedProperty renderPropertyId = layer.FindPropertyRelative("_renderPropertyId");
            Label channelStatus = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-composed-extension-channel-status");
            parent.Add(channelStatus);
            PropertyField colorMode = CreateBoundField(
                layer.FindPropertyRelative("_colorMode"),
                "Color Mode",
                "ts-composed-layer-color-mode");
            colorMode.tooltip = "Replace the authored text color, or multiply it to preserve the original coloring.";
            parent.Add(colorMode);
            VisualElement colorGradient = new TextStudioComposedGradientAuthoringElement(
                layer.FindPropertyRelative("_colorGradient"),
                () =>
                {
                    InvalidatePreviewPlan();
                    RenderPreview();
                });
            parent.Add(colorGradient);
            LayerEnvelopeControls envelope = AddLayerEnvelopeFields(parent, layer);
            PropertyField curveField = envelope.CurveField;
            PropertyField customCurve = envelope.InlineCurve;
            CurveField presetCurve = envelope.PresetCurve;

            SerializedProperty vectorProperty = layer.FindPropertyRelative("_vector");
            PropertyField vector = CreateBoundField(vectorProperty, "Multiplier", "ts-composed-layer-vector");
            Vector2Field planarVector = new Vector2Field("Offset")
            {
                name = "ts-composed-layer-planar-vector",
                tooltip = "Edit the two axes used by flat text. Any preserved depth value is reported separately."
            };
            planarVector.AddToClassList("ts-property-field");
            planarVector.AddToClassList("unity-base-field__aligned");
            planarVector.AddToClassList("ts-composed-layer-aligned-field");
            planarVector.RegisterValueChangedCallback(evt =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                Vector3 value = vectorProperty.vector3Value;
                value.x = evt.newValue.x;
                value.y = evt.newValue.y;
                Undo.RecordObject(target, "Edit Composed Effect Property");
                vectorProperty.vector3Value = value;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                InvalidatePreviewPlan();
                RenderPreview();
            });
            FloatField planarRotation = new FloatField("Angle")
            {
                name = "ts-composed-layer-planar-rotation",
                tooltip = "Rotation around the flat text plane, in degrees."
            };
            planarRotation.AddToClassList("ts-property-field");
            planarRotation.AddToClassList("unity-base-field__aligned");
            planarRotation.AddToClassList("ts-composed-layer-aligned-field");
            planarRotation.RegisterValueChangedCallback(evt =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                Vector3 value = vectorProperty.vector3Value;
                value.z = evt.newValue;
                Undo.RecordObject(target, "Edit Composed Effect Rotation");
                vectorProperty.vector3Value = value;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                InvalidatePreviewPlan();
                RenderPreview();
            });
            VisualElement nonPlanarWarning = new VisualElement
            {
                name = "ts-composed-layer-non-planar-warning"
            };
            nonPlanarWarning.AddToClassList("ts-composed-inline-warning");
            Label nonPlanarMessage = new Label(
                "This layer contains values that are not available in 2D.");
            nonPlanarMessage.AddToClassList("ts-muted-text");
            nonPlanarWarning.Add(nonPlanarMessage);
            Button resetNonPlanar = TextStudioInspectorElements.CreateButton(
                "Fix for 2D",
                "Reset only the hidden depth or off-plane rotation values required for this layer to work in 2D.",
                () =>
                {
                    serializedObject.UpdateIfRequiredOrScript();
                    TextStudioCompositionChannel selected =
                        (TextStudioCompositionChannel)channel.enumValueIndex;
                    Vector3 value = vectorProperty.vector3Value;
                    if (selected == TextStudioCompositionChannel.Position)
                        value.z = 0f;
                    else if (selected == TextStudioCompositionChannel.Rotation)
                    {
                        value.x = 0f;
                        value.y = 0f;
                    }
                    else if (selected == TextStudioCompositionChannel.Scale)
                        value.z = 1f;
                    Undo.RecordObject(target, "Fix Composed Effect for 2D");
                    vectorProperty.vector3Value = value;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                    InvalidatePreviewPlan();
                    UpdateValidation();
                    RenderPreview();
                });
            nonPlanarWarning.Add(resetNonPlanar);
            PropertyField scalar = CreateBoundField(layer.FindPropertyRelative("_scalar"), "Target Alpha", "ts-composed-layer-scalar");
            PropertyField unsigned = CreateBoundField(
                layer.FindPropertyRelative("_unsigned"),
                "Target Value",
                "ts-composed-layer-unsigned");
            Foldout cornerOffsets = new Foldout
            {
                name = "ts-composed-layer-corner-offsets",
                text = "Corner Offsets",
                value = false
            };
            cornerOffsets.tooltip = "Offset each glyph corner independently in effect units. Use small values first.";
            cornerOffsets.Add(CreateBoundField(layer.FindPropertyRelative("_bottomLeftCorner"), "Bottom Left", "ts-composed-layer-corner-bottom-left"));
            cornerOffsets.Add(CreateBoundField(layer.FindPropertyRelative("_topLeftCorner"), "Top Left", "ts-composed-layer-corner-top-left"));
            cornerOffsets.Add(CreateBoundField(layer.FindPropertyRelative("_topRightCorner"), "Top Right", "ts-composed-layer-corner-top-right"));
            cornerOffsets.Add(CreateBoundField(layer.FindPropertyRelative("_bottomRightCorner"), "Bottom Right", "ts-composed-layer-corner-bottom-right"));
            parent.Add(vector);
            parent.Add(planarVector);
            parent.Add(planarRotation);
            parent.Add(nonPlanarWarning);
            parent.Add(scalar);
            parent.Add(unsigned);
            parent.Add(cornerOffsets);
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_intensity"), "Influence", "ts-composed-layer-intensity"));
            Foldout timingGroup = CreateLayerGroup(
                "Timing & Sequence",
                "ts-composed-layer-timing-sequence",
                "Control phase offsets, playback timing, and ordered staggering.");
            PropertyField phaseShift = CreateBoundField(
                layer.FindPropertyRelative("_timeOffset"),
                "Phase Shift",
                "ts-composed-layer-time-offset");
            phaseShift.tooltip = "Shift this layer's playback in seconds. Positive values advance it; negative values delay it.";
            timingGroup.Add(phaseShift);
            timingGroup.Add(CreateBoundField(layer.FindPropertyRelative("_sequence"), "Sequence", "ts-composed-layer-sequence"));
            timingGroup.Add(CreateBoundField(layer.FindPropertyRelative("_timing"), "Timing", "ts-composed-layer-timing"));
            parent.Add(timingGroup);
            Foldout scopeGroup = CreateLayerGroup(
                "Apply Where",
                "ts-composed-layer-scope",
                "Choose the affected text unit, spatial area, and original source colors.");
            AddLayerScopeFields(scopeGroup, layer);
            parent.Add(scopeGroup);
            Foldout rendererGroup = CreateLayerGroup(
                "Composition & Renderer",
                "ts-composed-layer-composition-renderer",
                "Control layer blending and compatible renderer surfaces.");
            PropertyField surfaceMask = CreateBoundField(
                layer.FindPropertyRelative("_surfaceMask"),
                "Surfaces",
                "ts-composed-layer-surface-mask");
            surfaceMask.tooltip = "Choose which compatible renderer surfaces receive this property. Text Studio 3D maps these to front, back, sides, edges, and inner walls.";
            rendererGroup.Add(surfaceMask);
            PropertyField blend = CreateBoundField(layer.FindPropertyRelative("_blendMode"), "Blend", "ts-composed-layer-blend");
            rendererGroup.Add(blend);
            parent.Add(rendererGroup);

            Action refreshFields = () =>
            {
                if (target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                TextStudioCompositionChannel value = (TextStudioCompositionChannel)channel.enumValueIndex;
                string descriptorId = renderPropertyId != null ? renderPropertyId.stringValue : string.Empty;
                bool usesDescriptor = !string.IsNullOrEmpty(descriptorId);
                TextStudioRenderPropertyDescriptor descriptor = default;
                bool descriptorAvailable = usesDescriptor &&
                    TextStudioRenderPropertyRegistry.TryGet(descriptorId, out descriptor);
                bool isTwoD = _authoringSpace == null ||
                    _authoringSpace.enumValueIndex ==
                    (int)TextStudioCompositionAuthoringSpace.TwoD;
                bool isPlanarTransform = isTwoD && !usesDescriptor &&
                    (value == TextStudioCompositionChannel.Position ||
                     value == TextStudioCompositionChannel.Rotation ||
                     value == TextStudioCompositionChannel.Scale);
                bool showsSurfaceMask = descriptorAvailable && descriptor.SupportsSurfaceMask;
                surfaceMask.style.display = showsSurfaceMask
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                bool isColor = usesDescriptor
                    ? descriptorAvailable && descriptor.ValueType == TextStudioRenderPropertyValueType.Color
                    : value == TextStudioCompositionChannel.Color;
                bool isCornerDeformation = !usesDescriptor &&
                    value == TextStudioCompositionChannel.CornerDeformation;
                channelStatus.style.display = usesDescriptor ? DisplayStyle.Flex : DisplayStyle.None;
                if (usesDescriptor)
                {
                    channelStatus.text = descriptorAvailable
                        ? descriptor.Category + " · " + descriptor.Stage + " · " + descriptor.PerformanceClass +
                          (string.IsNullOrEmpty(descriptor.Description) ? string.Empty : "\n" + descriptor.Description)
                        : "Missing extension property: " + descriptorId +
                          ". The layer remains serialized but evaluates as a safe no-op until its extension is installed.";
                    SetClass(channelStatus, "ts-state-warning", !descriptorAvailable);
                }
                colorMode.style.display = isColor ? DisplayStyle.Flex : DisplayStyle.None;
                colorGradient.style.display = isColor ? DisplayStyle.Flex : DisplayStyle.None;
                curveField.label = isColor ? "Weight Curve" : "Curve";
                customCurve.label = isColor ? "Custom Weight" : "Custom Curve";
                presetCurve.label = isColor ? "Weight Shape" : "Curve Shape";
                vector.label = descriptorAvailable ? descriptor.DisplayName : ChannelValueLabel(value);
                bool descriptorUsesVector = descriptorAvailable &&
                    descriptor.ValueType != TextStudioRenderPropertyValueType.Float &&
                    descriptor.ValueType != TextStudioRenderPropertyValueType.Color &&
                    descriptor.ValueType != TextStudioRenderPropertyValueType.UInt;
                vector.style.display = usesDescriptor
                    ? (descriptorUsesVector ? DisplayStyle.Flex : DisplayStyle.None)
                    : (value == TextStudioCompositionChannel.Alpha || isColor || isCornerDeformation
                        ? DisplayStyle.None
                        : DisplayStyle.Flex);
                if (isPlanarTransform)
                    vector.style.display = DisplayStyle.None;
                Vector3 authoredVector = vectorProperty.vector3Value;
                planarVector.label = value == TextStudioCompositionChannel.Scale
                    ? "Target Scale"
                    : "Offset";
                planarVector.SetValueWithoutNotify(new Vector2(authoredVector.x, authoredVector.y));
                planarRotation.SetValueWithoutNotify(authoredVector.z);
                planarVector.style.display = isPlanarTransform &&
                    value != TextStudioCompositionChannel.Rotation
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                planarRotation.style.display = isPlanarTransform &&
                    value == TextStudioCompositionChannel.Rotation
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                bool hasNonPlanar = isPlanarTransform &&
                    (value == TextStudioCompositionChannel.Position
                        ? !Mathf.Approximately(authoredVector.z, 0f)
                        : value == TextStudioCompositionChannel.Rotation
                            ? !Mathf.Approximately(authoredVector.x, 0f) ||
                              !Mathf.Approximately(authoredVector.y, 0f)
                            : !Mathf.Approximately(authoredVector.z, 1f));
                nonPlanarMessage.text = TextStudioCompositionLayer.GetNonPlanarValuesWarning(
                    value,
                    "This layer");
                nonPlanarWarning.style.display = hasNonPlanar
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                scalar.label = descriptorAvailable ? descriptor.DisplayName : "Target Alpha";
                scalar.style.display = usesDescriptor
                    ? (descriptorAvailable && descriptor.ValueType == TextStudioRenderPropertyValueType.Float
                        ? DisplayStyle.Flex
                        : DisplayStyle.None)
                    : (value == TextStudioCompositionChannel.Alpha ? DisplayStyle.Flex : DisplayStyle.None);
                unsigned.label = descriptorAvailable
                    ? descriptor.DisplayName
                    : "Target Value";
                unsigned.style.display = usesDescriptor &&
                    descriptorAvailable &&
                    descriptor.ValueType == TextStudioRenderPropertyValueType.UInt
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                cornerOffsets.style.display = isCornerDeformation ? DisplayStyle.Flex : DisplayStyle.None;
                bool showsBlend = usesDescriptor || !isColor;
                blend.style.display = showsBlend ? DisplayStyle.Flex : DisplayStyle.None;
                rendererGroup.style.display = showsSurfaceMask || showsBlend
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };
            parent.TrackPropertyValue(channel, _ => refreshFields());
            if (renderPropertyId != null)
                parent.TrackPropertyValue(renderPropertyId, _ => refreshFields());
            parent.TrackPropertyValue(vectorProperty, _ => refreshFields());
            if (_authoringSpace != null)
                parent.TrackPropertyValue(_authoringSpace, _ => refreshFields());
            refreshFields();
        }

        sealed class LayerEnvelopeControls
        {
            internal PropertyField CurveField;
            internal PropertyField InlineCurve;
            internal CurveField PresetCurve;
        }

        LayerEnvelopeControls AddLayerEnvelopeFields(
            VisualElement parent,
            SerializedProperty layer,
            bool progressDriven = false)
        {
            SerializedProperty playback = layer.FindPropertyRelative("_playback");
            SerializedProperty customPlaybackProperty = layer.FindPropertyRelative("_customPlayback");
            SerializedProperty curve = layer.FindPropertyRelative("_curve");
            SerializedProperty customCurveAssetProperty = layer.FindPropertyRelative("_customCurveAsset");

            parent.Add(CreateBoundField(playback, "Playback", "ts-composed-layer-playback"));
            PropertyField customPlayback = CreateBoundField(
                customPlaybackProperty,
                "Custom Playback",
                "ts-composed-layer-custom-playback");
            customPlayback.tooltip = "Assign a reusable playback envelope. Select the asset to edit its live preview, Quick Starts, timing, cycles, and completion behavior.";
            if (progressDriven)
            {
                customPlayback.tooltip = "Assign a reusable playback profile for sweep position, delays, cycles, return timing, and completion behavior.";
            }
            parent.Add(customPlayback);
            Label customPlaybackHint = TextStudioInspectorElements.CreateMutedText(
                "Select the assigned Playback asset to edit its live envelope preview and mode-specific controls.",
                "ts-composed-layer-custom-playback-help");
            Label customPlaybackWarning = TextStudioInspectorElements.CreateStatus(
                "Custom Playback needs a Playback asset. Create one through Assets > Create > Text Studio > Effects > Playback, then assign it here.",
                CommonInspectorStatusType.Warning,
                "ts-composed-layer-custom-playback-warning");
            parent.Add(customPlaybackHint);
            parent.Add(customPlaybackWarning);

            PropertyField curveField = CreateBoundField(
                curve,
                progressDriven ? "Progress Curve" : "Curve",
                "ts-composed-layer-curve");
            curveField.tooltip = progressDriven
                ? "Shapes normalized sweep position. Reverse Linear runs a one-way pass in the opposite direction."
                : "Shapes the playback envelope applied to this layer.";
            parent.Add(curveField);
            CurveField presetCurve = new CurveField(
                progressDriven ? "Progress Shape" : "Curve Shape")
            {
                name = "ts-composed-layer-curve-shape",
                pickingMode = PickingMode.Ignore
            };
            presetCurve.AddToClassList("ts-property-field");
            presetCurve.AddToClassList("unity-base-field__aligned");
            presetCurve.AddToClassList("ts-composed-layer-aligned-field");
            presetCurve.SetEnabled(false);
            parent.Add(presetCurve);
            PropertyField customCurveAsset = CreateBoundField(
                customCurveAssetProperty,
                "Curve Asset",
                "ts-composed-layer-custom-curve-asset");
            customCurveAsset.tooltip = "Assign a reusable Curve asset. When assigned, it is authoritative for this layer.";
            parent.Add(customCurveAsset);
            Label customCurveAssetHint = TextStudioInspectorElements.CreateMutedText(
                "The assigned Curve asset is authoritative. Clear it to edit an inline curve instead.",
                "ts-composed-layer-custom-curve-asset-help");
            parent.Add(customCurveAssetHint);
            PropertyField inlineCurve = CreateBoundField(
                layer.FindPropertyRelative("_customCurve"),
                "Custom Curve",
                "ts-composed-layer-custom-curve");
            parent.Add(inlineCurve);

            int lastCurvePreset = -1;
            Action refresh = () =>
            {
                if (target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                bool usesCustomPlayback = playback.enumValueIndex ==
                    (int)TextStudioEffectPlaybackPreset.Custom;
                bool hasCustomPlayback = customPlaybackProperty.objectReferenceValue != null;
                customPlayback.style.display = usesCustomPlayback ? DisplayStyle.Flex : DisplayStyle.None;
                customPlaybackHint.style.display = usesCustomPlayback && hasCustomPlayback
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                customPlaybackWarning.style.display = usesCustomPlayback && !hasCustomPlayback
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                bool usesCustomCurve = curve.enumValueIndex ==
                    (int)TextStudioEffectCurvePreset.Custom;
                bool hasCurveAsset = customCurveAssetProperty.objectReferenceValue != null;
                presetCurve.style.display = usesCustomCurve ? DisplayStyle.None : DisplayStyle.Flex;
                customCurveAsset.style.display = usesCustomCurve ? DisplayStyle.Flex : DisplayStyle.None;
                customCurveAssetHint.style.display = usesCustomCurve && hasCurveAsset
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                inlineCurve.style.display = usesCustomCurve && !hasCurveAsset
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (!usesCustomCurve && lastCurvePreset != curve.enumValueIndex)
                {
                    lastCurvePreset = curve.enumValueIndex;
                    presetCurve.SetValueWithoutNotify(BuildCurvePreview(
                        (TextStudioEffectCurvePreset)lastCurvePreset));
                }
            };
            parent.TrackPropertyValue(playback, _ => refresh());
            parent.TrackPropertyValue(customPlaybackProperty, _ => refresh());
            parent.TrackPropertyValue(curve, _ => refresh());
            parent.TrackPropertyValue(customCurveAssetProperty, _ => refresh());
            refresh();

            return new LayerEnvelopeControls
            {
                CurveField = curveField,
                InlineCurve = inlineCurve,
                PresetCurve = presetCurve
            };
        }

        void AddEffectFields(VisualElement parent, SerializedProperty layer, int layerIndex)
        {
            bool progressDriven = false;
            if (target is TextStudioComposedEffectAsset composition &&
                layerIndex >= 0 &&
                layerIndex < composition.Layers.Count &&
                composition.Layers[layerIndex] != null)
            {
                TextStudioCompositionLayer runtimeLayer = composition.Layers[layerIndex];
                progressDriven = runtimeLayer.UsesProgressPlayback;
                if (runtimeLayer.SynchronizeParameterSnapshot(false))
                {
                    EditorUtility.SetDirty(composition);
                    serializedObject.UpdateIfRequiredOrScript();
                }
            }

            SerializedProperty effectId = layer.FindPropertyRelative("_effectId");
            SerializedProperty parameters = layer.FindPropertyRelative("_effectParameters");
            Label effect = new Label("Nested effect: " + effectId.stringValue);
            effect.name = "ts-composed-layer-effect-summary";
            effect.AddToClassList("ts-muted-text");
            parent.Add(effect);
            parent.Add(TextStudioComposedEffectParameterEditor.Create(
                serializedObject,
                effectId,
                layer.FindPropertyRelative("_effectParameterEntries"),
                parameters,
                OnEffectParametersChanged));
            AddNestedLayerControls(parent, layer, "nested effect", progressDriven);
        }

        void AddWritingFields(
            VisualElement parent,
            SerializedProperty layer,
            string stableId)
        {
            SerializedProperty writingStyle =
                layer.FindPropertyRelative("_writingStyle");
            PropertyField styleField = CreateBoundField(
                writingStyle,
                "Writing Style",
                "ts-composed-layer-writing-style");
            styleField.tooltip =
                "The reusable Writing treatment. Its design and fallback promise are identical whether used directly on text or inside this composition.";
            Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                () => CreateWritingStyleForLayer(stableId),
                "ts-composed-layer-create-writing-style",
                "Create and assign a reusable Writing Style for this layer.");
            parent.Add(TextStudioInspectorElements.CreateAssetReferenceRow(
                styleField,
                create,
                "ts-composed-layer-writing-style-row"));
            parent.Add(TextStudioInspectorElements.CreateMutedText(
                "Typewriter owns Reveal and Hide progress. This layer selects where the writing applies, its local curve, and its influence."));
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_intensity"), "Influence", "ts-writing-influence"));
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_curve"), "Progress Curve", "ts-writing-curve"));
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_customCurveAsset"), "Curve Asset", "ts-writing-curve-asset"));
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_customCurve"), "Custom Curve", "ts-writing-custom-curve"));
            Foldout scope = CreateLayerGroup(
                "Apply Where",
                "ts-composed-layer-scope",
                "Choose the affected text unit, spatial area, and original source colors.");
            AddLayerScopeFields(scope, layer);
            parent.Add(scope);
        }

        void AddNestedLayerControls(
            VisualElement parent,
            SerializedProperty layer,
            string subject,
            bool progressDriven = false)
        {
            parent.Add(CreateBoundField(layer.FindPropertyRelative("_intensity"), "Influence", "ts-composed-layer-intensity"));
            AddLayerEnvelopeFields(parent, layer, progressDriven);
            Foldout timingGroup = CreateLayerGroup(
                "Timing & Sequence",
                "ts-composed-layer-timing-sequence",
                "Control phase offsets, playback speed, and ordered staggering.");
            PropertyField phaseShift = CreateBoundField(
                layer.FindPropertyRelative("_timeOffset"),
                "Phase Shift",
                "ts-composed-layer-time-offset");
            phaseShift.tooltip = "Shift this " + subject +
                " in seconds. Positive values advance it; negative values delay it.";
            timingGroup.Add(phaseShift);
            timingGroup.Add(CreateBoundField(layer.FindPropertyRelative("_sequence"), "Sequence", "ts-composed-layer-sequence"));
            SerializedProperty timing = layer.FindPropertyRelative("_timing");
            PropertyField playbackSpeed = CreateBoundField(
                timing.FindPropertyRelative("speed"),
                "Playback Speed",
                "ts-composed-layer-playback-speed");
            playbackSpeed.tooltip = "Multiplies the selected playback profile speed without adding effect-specific cadence controls.";
            timingGroup.Add(playbackSpeed);
            Foldout stagger = new Foldout
            {
                name = "ts-composed-layer-stagger",
                text = "Stagger",
                value = false,
                tooltip = "Offset this layer across successive glyph, word, and line units without changing its own speed."
            };
            stagger.Add(CreateBoundField(
                timing.FindPropertyRelative("charOffset"),
                "Character Offset",
                "ts-composed-layer-char-offset"));
            stagger.Add(CreateBoundField(
                timing.FindPropertyRelative("wordOffset"),
                "Word Offset",
                "ts-composed-layer-word-offset"));
            stagger.Add(CreateBoundField(
                timing.FindPropertyRelative("lineOffset"),
                "Line Offset",
                "ts-composed-layer-line-offset"));
            timingGroup.Add(stagger);
            Foldout scopeGroup = CreateLayerGroup(
                "Apply Where",
                "ts-composed-layer-scope",
                "Choose the affected text unit, spatial area, and original source colors.");
            AddLayerScopeFields(scopeGroup, layer);
            parent.Add(timingGroup);
            parent.Add(scopeGroup);
        }

        void AddLayerScopeFields(Foldout scopeGroup, SerializedProperty layer)
        {
            scopeGroup.Add(CreateBoundField(
                layer.FindPropertyRelative("_area"),
                "Area",
                "ts-composed-layer-area"));

            Foldout spatialInfluence = new Foldout
            {
                name = "ts-composed-layer-spatial-influence",
                text = "Spatial Influence",
                value = false,
                tooltip = "Limit this layer with stackable fields in source layout, normalized text, placed text, or world space."
            };
            SerializedProperty spatialInfluenceStack =
                layer.FindPropertyRelative("_spatialInfluence");
            PropertyField spatialInfluenceFields = CreateBoundField(
                spatialInfluenceStack.FindPropertyRelative("_fields"),
                "Fields",
                "ts-composed-layer-spatial-influence-fields");
            spatialInfluenceFields.tooltip = "Fields combine from top to bottom. Empty stacks preserve the layer everywhere.";
            spatialInfluence.Add(spatialInfluenceFields);
            scopeGroup.Add(spatialInfluence);

            SerializedProperty logicalSelector = layer.FindPropertyRelative("_selector");
            if (logicalSelector != null)
            {
                VisualElement selection = TextStudioSelectionField.Create(logicalSelector, "Text Selection");
                selection.name = "ts-composed-layer-text-selection";
                scopeGroup.Add(selection);
            }

            SerializedProperty selector = layer.FindPropertyRelative("_sourceColorSelector");
            if (selector == null)
                return;

            SerializedProperty enabled = selector.FindPropertyRelative("_enabled");
            SerializedProperty metric = selector.FindPropertyRelative("_metric");
            Toggle colorSelection = new Toggle("Color Selection")
            {
                name = "ts-composed-layer-color-selection",
                bindingPath = enabled.propertyPath,
                tooltip = "Filter this layer using each glyph's original color, before any Text Studio effects are applied."
            };
            colorSelection.AddToClassList("ts-property-field");
            colorSelection.AddToClassList("unity-base-field__aligned");
            colorSelection.AddToClassList("ts-composed-layer-aligned-field");
            colorSelection.BindProperty(enabled);
            scopeGroup.Add(colorSelection);

            VisualElement details = new VisualElement
            {
                name = "ts-composed-layer-color-selection-details"
            };
            details.AddToClassList("ts-composed-layer-color-selection-details");
            PropertyField metricField = CreateBoundField(
                metric,
                "Metric",
                "ts-composed-layer-color-selection-metric");
            details.Add(metricField);
            PropertyField targetColor = CreateBoundField(
                selector.FindPropertyRelative("_targetColor"),
                "Target Hue",
                "ts-composed-layer-color-selection-target-color");
            PropertyField scalarTarget = CreateBoundField(
                selector.FindPropertyRelative("_target"),
                "Target",
                "ts-composed-layer-color-selection-target");
            details.Add(targetColor);
            details.Add(scalarTarget);
            details.Add(CreateBoundField(
                selector.FindPropertyRelative("_tolerance"),
                "Tolerance",
                "ts-composed-layer-color-selection-tolerance"));
            details.Add(CreateBoundField(
                selector.FindPropertyRelative("_softness"),
                "Softness",
                "ts-composed-layer-color-selection-softness"));
            details.Add(CreateBoundField(
                selector.FindPropertyRelative("_invert"),
                "Invert",
                "ts-composed-layer-color-selection-invert"));
            details.Add(TextStudioInspectorElements.CreateMutedText(
                "Selection samples the immutable center color of each original glyph. A renderer without source-color data leaves this layer unfiltered and reports compatibility.",
                "ts-composed-layer-color-selection-help"));
            scopeGroup.Add(details);

            Action refreshColorSelection = () =>
            {
                bool isEnabled = enabled.boolValue;
                details.style.display = isEnabled ? DisplayStyle.Flex : DisplayStyle.None;
                TextStudioSourceColorMetric selectedMetric =
                    (TextStudioSourceColorMetric)metric.enumValueIndex;
                targetColor.style.display =
                    selectedMetric == TextStudioSourceColorMetric.Hue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                scalarTarget.style.display =
                    selectedMetric == TextStudioSourceColorMetric.Hue
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            };
            colorSelection.RegisterValueChangedCallback(_ => refreshColorSelection());
            metricField.RegisterCallback<SerializedPropertyChangeEvent>(_ => refreshColorSelection());
            refreshColorSelection();
        }

        static Foldout CreateLayerGroup(string title, string name, string tooltip)
        {
            Foldout foldout = new Foldout
            {
                name = name,
                text = title,
                value = false,
                tooltip = tooltip
            };
            foldout.AddToClassList("ts-composed-borderless-foldout");
            foldout.AddToClassList("ts-composed-layer-group");
            return foldout;
        }

        void OnEffectParametersChanged()
        {
            if (target != null)
                EditorUtility.SetDirty(target);
            InvalidatePreviewPlan();
            UpdateValidation();
            RenderPreview();
        }

        VisualElement CreateLayerPhaseSelector(SerializedProperty phases, string layerKey)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-button-row");
            AddLayerPhaseButton(row, phases, layerKey, "Reveal", TextStudioPhaseMask.Reveal);
            AddLayerPhaseButton(row, phases, layerKey, "Active", TextStudioPhaseMask.Active);
            AddLayerPhaseButton(row, phases, layerKey, "Hide", TextStudioPhaseMask.Hide);
            VisualElement container = TextStudioInspectorElements.CreateAlignedControlRow(
                "Runs During",
                row,
                "ts-composed-layer-phases");
            container.AddToClassList("ts-composed-phase-selector");
            container.AddToClassList("ts-composed-layer-aligned-field");
            Label label = container.Q<Label>(className: "ts-aligned-control-row__label");
            label?.AddToClassList("ts-composed-phase-label");
            return container;
        }

        void AddLayerPhaseButton(
            VisualElement parent,
            SerializedProperty phases,
            string layerKey,
            string label,
            TextStudioPhaseMask phase)
        {
            Button button = null;
            Action refresh = () =>
            {
                if (button == null || target == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                bool active = (phases.intValue & (int)phase) != 0;
                button.text = label;
                SetClass(button, "ts-authoring-mode-active", active);
            };
            button = new Button(() =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                int current = phases.intValue;
                int next = current ^ (int)phase;
                Undo.RecordObject(target, "Change Composed Effect Phases");
                phases.intValue = next;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                InvalidatePreviewPlan();
                refresh();
                UpdateValidation();
                RenderPreview();
            })
            {
                name = "ts-composed-layer-" + layerKey + "-phase-" + label.ToLowerInvariant(),
                tooltip = "Toggle this layer during " + label.ToLowerInvariant() +
                    ". Turn off all phases to disable the layer."
            };
            button.AddToClassList("ts-authoring-mode-button");
            parent.Add(button);
            button.TrackPropertyValue(phases, _ => refresh());
            refresh();
        }

        void AddChannelLayer(TextStudioCompositionChannel channel)
        {
            if (target is not TextStudioComposedEffectAsset effect)
                return;
            Undo.RecordObject(effect, "Add Composed Effect Property");
            effect.AddChannelLayer(channel);
            EditorUtility.SetDirty(effect);
            RefreshAfterLayerStructureChange();
        }

        void AddRenderPropertyLayer(string propertyId)
        {
            if (target is not TextStudioComposedEffectAsset effect ||
                string.IsNullOrWhiteSpace(propertyId))
                return;
            Undo.RecordObject(effect, "Add Composed Effect Extension Property");
            effect.AddRenderPropertyLayer(propertyId);
            EditorUtility.SetDirty(effect);
            RefreshAfterLayerStructureChange();
        }

        void AddEffectLayer(string effectId, string parameters = "")
        {
            if (target is not TextStudioComposedEffectAsset effect || string.IsNullOrWhiteSpace(effectId))
                return;
            Undo.RecordObject(effect, "Add Composed Effect Layer");
            TextStudioCompositionLayer layer = effect.AddEffectLayer(effectId);
            if (layer != null && TextStudioEffects.TryGetDefinition(
                    effectId,
                    out ITextStudioEffectDefinition definition))
            {
                ITextStudioBuiltIn builtIn = definition as ITextStudioBuiltIn;
                TextStudioEffectMetadata metadata =
                    (definition as ITextStudioEffectMetadataProvider)?.Metadata;
                layer.ConfigureEffectForAuthoring(
                    layer.StableId,
                    !string.IsNullOrWhiteSpace(metadata?.DisplayName)
                        ? metadata.DisplayName
                        : !string.IsNullOrWhiteSpace(builtIn?.DisplayName)
                            ? builtIn.DisplayName
                            : layer.DisplayName,
                    effectId,
                    parameters,
                    metadata != null && metadata.SuggestedPhases != TextStudioPhaseMask.None
                        ? metadata.SuggestedPhases
                        : layer.Phases,
                    layer.Intensity,
                    layer.TimeOffset,
                    layer.DefaultArea,
                    layer.Timing);
            }
            EditorUtility.SetDirty(effect);
            RefreshAfterLayerStructureChange();
        }

        void AddWritingLayer(TextStudioWritingStyleAsset writingStyle)
        {
            if (target is not TextStudioComposedEffectAsset effect)
                return;
            Undo.RecordObject(effect, "Add Composed Writing Layer");
            effect.AddWritingLayer(writingStyle);
            EditorUtility.SetDirty(effect);
            RefreshAfterLayerStructureChange();
        }

        void CreateWritingStyleForLayer(string stableId)
        {
            if (target is not TextStudioComposedEffectAsset effect ||
                string.IsNullOrEmpty(stableId))
            {
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Writing Style",
                "Text Studio Writing Style",
                "asset",
                "Choose where to save the reusable Text Studio writing treatment.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioWritingStyleAsset style =
                ScriptableObject.CreateInstance<TextStudioWritingStyleAsset>();
            style.SetConfiguration(TextStudioWritingConfiguration.Default);
            AssetDatabase.CreateAsset(style, path);
            Undo.RegisterCreatedObjectUndo(
                style,
                "Create Text Studio Writing Style");
            Undo.RecordObject(effect, "Assign Composed Writing Style");
            if (!effect.SetLayerWritingStyle(stableId, style))
            {
                Debug.LogWarning(
                    "Text Studio could not assign the new Writing Style because the composition layer changed.",
                    effect);
            }
            EditorUtility.SetDirty(effect);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(style);
            RefreshAfterLayerStructureChange();
        }

        void DuplicateLayer(string stableId)
        {
            if (target is not TextStudioComposedEffectAsset effect)
                return;
            Undo.RecordObject(effect, "Duplicate Composed Effect Layer");
            if (!effect.DuplicateLayer(stableId))
                return;
            EditorUtility.SetDirty(effect);
            RefreshAfterLayerStructureChange();
        }

        void RemoveLayer(string stableId)
        {
            serializedObject.UpdateIfRequiredOrScript();
            if (!TryGetSerializedLayerIndex(stableId, out SerializedProperty layers, out int index))
                return;

            Undo.RecordObjects(serializedObject.targetObjects, "Remove Composed Effect Layer");
            layers.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            MarkTargetsDirty();
            RefreshAfterLayerStructureChange();
        }

        void ExtractLayer(string stableId)
        {
            if (target is not TextStudioComposedEffectAsset effect ||
                !effect.TryCloneLayer(stableId, out TextStudioCompositionLayer clone))
            {
                return;
            }

            string suggestedName = string.IsNullOrWhiteSpace(clone.DisplayName)
                ? "Nested Composed Effect"
                : clone.DisplayName + " Effect";
            string path = EditorUtility.SaveFilePanelInProject(
                "Extract Composed Effect",
                suggestedName,
                "asset",
                "Choose where to save the reusable nested Composed Effect.");
            if (string.IsNullOrEmpty(path))
                return;

            string id = BuildTagId(System.IO.Path.GetFileNameWithoutExtension(path));
            TextStudioComposedEffectAsset extracted = ScriptableObject.CreateInstance<TextStudioComposedEffectAsset>();
            extracted.Initialize(id, effect.AuthoringSpace, new[] { clone });
            AssetDatabase.CreateAsset(extracted, path);
            Undo.RegisterCreatedObjectUndo(extracted, "Extract Composed Effect Layer");

            Undo.RecordObject(effect, "Replace Layer With Composed Effect");
            effect.ReplaceLayerWithEffect(stableId, id);
            EditorUtility.SetDirty(effect);
            TextStudioEffectCatalogUtility.Add(extracted);
            AssetDatabase.SaveAssets();
            Selection.activeObject = extracted;
            RebuildLayerRows();
            RefreshLayerTimingRowsAfterStructureChange();
            InvalidatePreviewPlan();
            UpdateValidation();
            RenderPreview();
        }

        static string BuildTagId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "composed";

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            bool capitalizeNext = false;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (!char.IsLetterOrDigit(current))
                {
                    capitalizeNext = builder.Length > 0;
                    continue;
                }

                if (builder.Length == 0)
                    builder.Append(char.ToLowerInvariant(current));
                else if (capitalizeNext)
                    builder.Append(char.ToUpperInvariant(current));
                else
                    builder.Append(current);
                capitalizeNext = false;
            }

            return builder.Length > 0 ? builder.ToString() : "composed";
        }

        void ToggleSoloLayer(string stableId)
        {
            serializedObject.UpdateIfRequiredOrScript();
            if (!TryGetSerializedLayerIndex(stableId, out SerializedProperty layers, out int selectedIndex))
                return;

            string expandedLayerStableId = _expandedLayerStableId;
            SerializedProperty selected = layers.GetArrayElementAtIndex(selectedIndex);
            SerializedProperty selectedEnabled = selected.FindPropertyRelative("_enabled");
            if (selectedEnabled == null || !selectedEnabled.boolValue)
                return;
            _soloLayerStableId = string.Equals(SoloLayerStableId, stableId, StringComparison.Ordinal)
                ? string.Empty : stableId;
            RestoreLayerAccordion(expandedLayerStableId);
            serializedObject.UpdateIfRequiredOrScript();
            RefreshLayerHeaders();
            InvalidatePreviewPlan();
            RefreshLayerTimingRowsAfterStructureChange();
            UpdateValidation();
            RenderPreview();
        }

        void ToggleLayerEnabled(SerializedProperty enabled)
        {
            if (enabled == null)
                return;

            string expandedLayerStableId = _expandedLayerStableId;
            serializedObject.UpdateIfRequiredOrScript();
            Undo.RecordObjects(serializedObject.targetObjects, "Toggle Composed Effect Layer");
            enabled.boolValue = !enabled.boolValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            RestoreLayerAccordion(expandedLayerStableId);
            serializedObject.UpdateIfRequiredOrScript();
            RefreshLayerHeaders();
            MarkTargetsDirty();
            InvalidatePreviewPlan();
            UpdateValidation();
            RenderPreview();
        }

        void RestoreLayerAccordion(string stableId)
        {
            _expandedLayerStableId = stableId ?? string.Empty;
            SessionState.SetString(
                GetFoldoutStateKey("layer-accordion"),
                string.IsNullOrEmpty(_expandedLayerStableId)
                    ? NoExpandedLayerState
                    : _expandedLayerStableId);
            _syncingLayerAccordion = true;
            foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
            {
                pair.Value.SetValueWithoutNotify(string.Equals(
                    pair.Key,
                    _expandedLayerStableId,
                    StringComparison.Ordinal));
            }
            _syncingLayerAccordion = false;
        }

        void RefreshLayerHeaders()
        {
            foreach (KeyValuePair<string, Action> pair in _layerHeaderRefreshes)
                pair.Value?.Invoke();
        }

        void MarkTargetsDirty()
        {
            UnityEngine.Object[] targets = serializedObject.targetObjects;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                    EditorUtility.SetDirty(targets[i]);
            }
        }

        void MoveLayer(string stableId, int offset)
        {
            serializedObject.UpdateIfRequiredOrScript();
            if (!TryGetSerializedLayerIndex(stableId, out SerializedProperty layers, out int index))
                return;
            int destination = index + offset;
            if (destination < 0 || destination >= layers.arraySize)
                return;

            Undo.RecordObjects(serializedObject.targetObjects, "Reorder Composed Effect Layers");
            layers.MoveArrayElement(index, destination);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            MarkTargetsDirty();
            RefreshAfterLayerStructureChange();
        }

        bool TryGetSerializedLayerIndex(
            string stableId,
            out SerializedProperty layers,
            out int index)
        {
            layers = serializedObject.FindProperty("_layers");
            index = -1;
            if (string.IsNullOrEmpty(stableId) || layers == null || !layers.isArray)
                return false;

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty candidate = layers.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("_stableId");
                if (candidate != null && string.Equals(
                        candidate.stringValue,
                        stableId,
                        StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        void RefreshAfterLayerStructureChange()
        {
            serializedObject.Update();
            InvalidatePreviewPlan();
            RebuildLayerRows();
            RefreshLayerTimingRowsAfterStructureChange();
            UpdateValidation();
            RenderPreview();
        }

        static bool TryGetLayerCompatibility(
            TextStudioComposedEffectAsset composition,
            TextStudioCompositionLayer layer,
            out string error)
        {
            error = string.Empty;
            if (composition == null || layer == null || !layer.Enabled)
                return true;

            TextStudioRenderDomain supported = TextStudioRenderDomain.All;
            if (layer.Kind == TextStudioCompositionLayerKind.Channel)
            {
                if (layer.UsesRenderProperty)
                {
                    if (!TextStudioRenderPropertyRegistry.TryGet(
                            layer.RenderPropertyId,
                            out TextStudioRenderPropertyDescriptor descriptor))
                    {
                        error = "The property provider is not installed: " + layer.RenderPropertyId + ".";
                        return false;
                    }
                    supported = descriptor.SupportedRenderDomains;
                }
                if (composition.AuthoringSpace == TextStudioCompositionAuthoringSpace.TwoD &&
                    layer.HasNonPlanarValues)
                {
                    error = layer.GetNonPlanarValuesWarning("This layer");
                    return false;
                }
            }
            else if (layer.Kind == TextStudioCompositionLayerKind.Writing)
            {
                if (layer.WritingStyle == null)
                {
                    error = "Choose a Writing Style for this layer.";
                    return false;
                }
                CompiledTextStudioWritingStyle writing =
                    layer.ResolveCompiledWritingStyle();
                if (writing == null || !writing.IsValid)
                {
                    error = "The selected Writing Style has validation errors.";
                    return false;
                }
                supported = TextStudioRenderDomain.FlatText;
            }
            else
            {
                if (!TextStudioRegistry.Effects.TryGetQuiet(
                        layer.EffectId,
                        out ITextStudioEffectDefinition definition))
                {
                    error = "The referenced effect is not installed: " + layer.EffectId + ".";
                    return false;
                }
                supported = TextStudioEffectCompatibilityResolver.GetSupportedDomains(definition);
            }

            if ((supported & composition.RequiredRenderDomain) != 0)
                return true;
            error = TextStudioEffectCompatibilityResolver.BuildUnsupportedReason(supported);
            return false;
        }

        static string BuildLayerTitle(
            TextStudioCompositionLayerKind kind,
            SerializedProperty channel,
            SerializedProperty effectId,
            SerializedProperty writingStyle,
            SerializedProperty name,
            SerializedProperty enabled,
            SerializedProperty phases)
        {
            string underlyingName = GetUnderlyingLayerName(
                kind,
                channel,
                effectId,
                writingStyle);
            string authoredName = name?.stringValue?.Trim() ?? string.Empty;
            string normalizedAuthoredName = ObjectNames.NicifyVariableName(authoredName);
            string title = string.IsNullOrEmpty(authoredName) ? underlyingName : authoredName;
            List<string> metadata = new List<string>(3);
            if (!string.IsNullOrEmpty(authoredName) &&
                !string.Equals(normalizedAuthoredName, underlyingName, StringComparison.OrdinalIgnoreCase))
            {
                metadata.Add(underlyingName);
            }

            string phase = BuildLayerPhaseMetadata(phases);
            if (!string.IsNullOrEmpty(phase))
                metadata.Add(phase);
            if (enabled != null && !enabled.boolValue)
                metadata.Add("Off");

            return metadata.Count > 0
                ? title + " (" + string.Join(" · ", metadata) + ")"
                : title;
        }

        static string BuildLayerPhaseMetadata(SerializedProperty phases)
        {
            TextStudioPhaseMask phaseMask = phases != null
                ? (TextStudioPhaseMask)phases.intValue
                : TextStudioPhaseMask.Active;
            if ((phaseMask & TextStudioPhaseMask.Active) != 0)
                return string.Empty;

            bool reveal = (phaseMask & TextStudioPhaseMask.Reveal) != 0;
            bool hide = (phaseMask & TextStudioPhaseMask.Hide) != 0;
            if (reveal && hide)
                return "Reveal + Hide";
            if (reveal)
                return "Reveal";
            if (hide)
                return "Hide";
            return "No Phase";
        }

        static string BuildLayerPhaseTooltip(SerializedProperty phases)
        {
            TextStudioPhaseMask phaseMask = phases != null
                ? (TextStudioPhaseMask)phases.intValue
                : TextStudioPhaseMask.Active;
            if ((phaseMask & TextStudioPhaseMask.Active) != 0)
                return string.Empty;

            bool reveal = (phaseMask & TextStudioPhaseMask.Reveal) != 0;
            bool hide = (phaseMask & TextStudioPhaseMask.Hide) != 0;
            if (reveal && hide)
                return "Runs during Reveal and Hide, so it is not visible during Active.";
            if (reveal)
                return "Runs during Reveal, so it is not visible during Active.";
            if (hide)
                return "Runs during Hide, so it is not visible during Active.";
            return "This layer has no playback phase.";
        }

        static string GetUnderlyingLayerName(
            TextStudioCompositionLayerKind kind,
            SerializedProperty channel,
            SerializedProperty effectId,
            SerializedProperty writingStyle)
        {
            if (kind == TextStudioCompositionLayerKind.Channel && channel != null)
                return ObjectNames.NicifyVariableName(channel.enumDisplayNames[channel.enumValueIndex]);
            if (kind == TextStudioCompositionLayerKind.Writing)
            {
                UnityEngine.Object style = writingStyle?.objectReferenceValue;
                return style != null ? style.name : "Writing";
            }

            string id = effectId?.stringValue?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(id) ? "Effect" : ObjectNames.NicifyVariableName(id);
        }

        static List<EffectChoiceEntry> BuildEffectChoices(TextStudioComposedEffectAsset current)
        {
            TextStudioProjectSettingsEditorUtility.ActivateDeterministic(false);
            TextStudioRegistry.EnsureInitialized();
            List<EffectChoiceEntry> choices = new List<EffectChoiceEntry>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ITextStudioEffectDefinition> pair in TextStudioRegistry.Effects.GetAll())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null || ReferenceEquals(pair.Value, current))
                    continue;
                if (!ids.Add(pair.Key))
                    continue;
                ITextStudioBuiltIn builtIn = pair.Value as ITextStudioBuiltIn;
                TextStudioEffectMetadata metadata =
                    (pair.Value as ITextStudioEffectMetadataProvider)?.Metadata;
                string suffix = pair.Value is TextStudioComposedEffectAsset
                    ? " (Composed)"
                    : builtIn != null || TextStudioRegistry.Effects.IsBuiltIn(pair.Key, pair.Value)
                        ? " (Built-in)"
                        : " (Custom)";
                string category = !string.IsNullOrWhiteSpace(metadata?.Category)
                    ? metadata.Category
                    : string.IsNullOrWhiteSpace(builtIn?.Category)
                        ? "Effects"
                        : builtIn.Category;
                string displayName = !string.IsNullOrWhiteSpace(metadata?.DisplayName)
                    ? metadata.DisplayName
                    : string.IsNullOrWhiteSpace(builtIn?.DisplayName)
                        ? pair.Key
                        : builtIn.DisplayName;
                string label = category + " / " + displayName + suffix;
                bool supportsReducedMotion = HasMetadataParameter(metadata, "reduced");
                string description = !string.IsNullOrWhiteSpace(metadata?.Description)
                    ? metadata.Description
                    : builtIn?.Description ?? string.Empty;
                string searchText = label + " " + pair.Key + " " + description +
                    (supportsReducedMotion ? " accessible reduced motion" : string.Empty);
                TextStudioEffects.TryCreate(
                    pair.Value,
                    in TextStudioTagParameters.Empty,
                    out ITextStudioEffect previewEffect);
                choices.Add(new EffectChoiceEntry(
                    pair.Key,
                    label,
                    displayName,
                    category,
                    description,
                    searchText,
                    supportsReducedMotion,
                    previewEffect,
                    TextStudioEffectCompatibilityResolver.GetSupportedDomains(pair.Value),
                    TextStudioComposedLayerPresentation.ForDefinition(pair.Value)));
            }
            choices.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            return choices;
        }

        static bool HasMetadataParameter(TextStudioEffectMetadata metadata, string name)
        {
            if (metadata == null)
                return false;
            for (int i = 0; i < metadata.Parameters.Count; i++)
            {
                if (metadata.Parameters[i] != null && metadata.Parameters[i].MatchesName(name))
                    return true;
            }
            return false;
        }

        static string ChannelValueLabel(TextStudioCompositionChannel channel)
        {
            return channel switch
            {
                TextStudioCompositionChannel.Position => "Offset",
                TextStudioCompositionChannel.Rotation => "Rotation",
                TextStudioCompositionChannel.Scale => "Target Scale",
                TextStudioCompositionChannel.Skew => "Skew",
                TextStudioCompositionChannel.UVOffset => "UV Offset",
                TextStudioCompositionChannel.Pivot => "Pivot Offset",
                TextStudioCompositionChannel.CornerDeformation => "Corner Offsets",
                _ => "Multiplier"
            };
        }
    }
}
