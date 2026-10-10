using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Immutable appearance-property metadata stays available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudio2DStyleEditor : IDisposable
    {
        const string NoExpandedLayer = "<none>";
        readonly SerializedObject _serializedObject;
        readonly string _propertyRoot;
        readonly Func<CompiledTextStudio2DStyle> _compile;
        Object Target => _serializedObject.targetObject;
        string ExpansionKey => "text-studio-2d-style-expanded-" + Target.GetStableId() +
            (string.IsNullOrEmpty(_propertyRoot) ? string.Empty : "-" + _propertyRoot);

        internal TextStudio2DStyleEditor(
            SerializedObject owner,
            string propertyRoot,
            Func<CompiledTextStudio2DStyle> compile)
        {
            _serializedObject = owner;
            _propertyRoot = propertyRoot ?? string.Empty;
            _compile = compile;
            LoadExpandedLayerState();
        }

        SerializedProperty FindProperty(string name)
        {
            return _serializedObject.FindProperty(string.IsNullOrEmpty(_propertyRoot)
                ? name
                : _propertyRoot + "." + name);
        }

        readonly Dictionary<string, Foldout> _layerFoldouts =
            new Dictionary<string, Foldout>(StringComparer.Ordinal);
        readonly List<Action> _layerHeaderRefreshes = new List<Action>();

        VisualElement _root;
        VisualElement _cards;
        VisualElement _validation;
        Button _addButton;
        string _expandedLayerId = string.Empty;
        bool _synchronizingAccordion;

        internal Func<string, string, string, string, bool> DeleteConfirmationHandler { get; set; }

        internal VisualElement Build()
        {
            _serializedObject.UpdateIfRequiredOrScript();
            _root = TextStudioInspectorElements.CreateRoot("ts-2d-style-inspector");

            SerializedProperty baseStyle = FindProperty("_baseStyle");
            PropertyField baseStyleField = CreateField(
                baseStyle,
                "Base Style",
                "Optional style whose appearance layers are evaluated before the local layers.");
            SerializedProperty inheritanceMode = FindProperty("_inheritanceMode");
            PropertyField inheritanceModeField = CreateField(
                inheritanceMode,
                "Inheritance Mode",
                "Choose whether local layers append to, override matching IDs from, or replace the base style.");
            Label inheritanceHelp = TextStudioInspectorElements.CreateMutedText(
                "Inherited layers stay owned by the base asset. Open the base asset to edit them; this list contains only the local layers that can be reordered here.",
                "ts-2d-style-inheritance-help");

            void RefreshInheritanceState()
            {
                bool hasBase = baseStyle != null && baseStyle.objectReferenceValue != null;
                inheritanceModeField.style.display = hasBase
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                inheritanceHelp.style.display = hasBase
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (baseStyle != null)
                baseStyleField.TrackPropertyValue(baseStyle, _ => RefreshInheritanceState());
            RefreshInheritanceState();
            _root.Add(TextStudioInspectorElements.CreateSection(
                "Inheritance",
                "ts-2d-style-inheritance",
                baseStyleField,
                inheritanceModeField,
                inheritanceHelp));

            PropertyField sourceFieldQuality = CreateField(
                FindProperty("_sourceFieldQuality"),
                "Source Field Quality",
                "Request native, high, or ultra glyph-field density. Automatic follows the highest active layer quality and reports when the current text source cannot meet it.");
            PropertyField painterOrder = CreateField(
                FindProperty("_painterOrder"),
                "Painter Order",
                "By Layer completes each layer across the text. By Character completes source layers for each drawing cluster in backend visual order. Neighborhood effects and composite adjustments process the completed lower image once. Groups can override this order.");
            PropertyField worldShadowMode = CreateField(
                FindProperty("_worldShadowMode"),
                "World Shadows",
                "Alpha Clipped lets world-space Text Studio output cast its final styled silhouette into supported shadow maps. Canvas output is unaffected.");
            VisualElement renderingSection = TextStudioInspectorElements.CreateSection(
                "Rendering",
                "ts-2d-style-rendering",
                sourceFieldQuality,
                painterOrder,
                worldShadowMode);

            Label layerHelp = TextStudioInspectorElements.CreateMutedText(
                "Build the appearance from ordered paint, depth, structure, and motion layers. Open a card to edit only the controls that apply to that treatment.",
                "ts-2d-style-layer-help");
            _cards = new VisualElement
            {
                name = "ts-2d-style-layer-cards"
            };
            _cards.AddToClassList("ts-2d-style-layer-cards");
            _addButton = TextStudioInspectorElements.CreateButton(
                "Add Appearance Layer",
                "Choose a treatment and add it to the end of this local appearance stack.",
                ShowAddLayerMenu,
                primary: true);
            _addButton.name = "ts-2d-style-add-layer";
            VisualElement addArea = TextStudioInspectorElements.CreateLayerAddArea(_addButton);
            _root.Add(TextStudioInspectorElements.CreateSection(
                "Appearance Layers",
                "ts-2d-style-layers",
                layerHelp,
                _cards,
                addArea));

            _validation = new VisualElement
            {
                name = "ts-2d-style-validation"
            };
            VisualElement validationSection = TextStudioInspectorElements.CreateSection(
                "Needs Attention",
                "ts-2d-style-needs-attention",
                _validation);
            _root.Add(validationSection);
            _root.Add(renderingSection);

            _root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Undo.undoRedoPerformed -= HandleUndoRedo;
                Undo.undoRedoPerformed += HandleUndoRedo;
                AttachAppearanceInsertion();
            });
            _root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CancelRetainedRestore();
                Undo.undoRedoPerformed -= HandleUndoRedo;
                _appearanceInsertion?.Dispose(); _appearanceInsertion = null;
            });
            _root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                _root.schedule.Execute(RefreshValidation));
            _root.RegisterCallback<PointerDownEvent>(_ => CancelRetainedRestore(), TrickleDown.TrickleDown);
            _root.RegisterCallback<WheelEvent>(_ => CancelRetainedRestore(), TrickleDown.TrickleDown);
            _root.RegisterCallback<KeyDownEvent>(_ => CancelRetainedRestore(), TrickleDown.TrickleDown);

            RebuildLayerCards();
            RefreshValidation();
            _root.Bind(_serializedObject);
            return _root;
        }

        void RebuildLayerCards(RetainedState retained = null)
        {
            if (_cards == null)
                return;

            CancelRetainedRestore();
            retained ??= CaptureRetainedState();
            string expandedLayerId = _expandedLayerId;
            _synchronizingAccordion = true;
            try
            {
                _serializedObject.UpdateIfRequiredOrScript();
                _cards.Unbind();
                _cards.Clear();
                _layerFoldouts.Clear();
                _layerHeaderRefreshes.Clear();
                SerializedProperty layers = FindProperty("_layers");
                if (layers == null || !layers.isArray)
                {
                    _cards.Add(TextStudioInspectorElements.CreateStatus(
                        "The appearance layer data is unavailable.",
                        CommonInspectorStatusType.Error));
                    _addButton.SetEnabled(false);
                    return;
                }

                if (_serializedObject.isEditingMultipleObjects)
                {
                    _cards.Add(TextStudioInspectorElements.CreateStatus(
                        "Select one appearance owner to edit its ordered layers. Shared inheritance and rendering settings remain available.",
                        CommonInspectorStatusType.Info));
                    _addButton.SetEnabled(false);
                    return;
                }

                _addButton.SetEnabled(true);
                if (layers.arraySize == 0)
                {
                    _cards.Add(TextStudioInspectorElements.CreateMutedText(
                        "No local appearance layers yet. Add a Fill or another treatment to begin.",
                        "ts-2d-style-empty"));
                    return;
                }

                for (int i = 0; i < layers.arraySize; i++)
                    _cards.Add(CreateLayerCard(layers, i));
                _cards.Bind(_serializedObject);
                _expandedLayerId = expandedLayerId;
                foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
                {
                    bool shouldExpand = string.Equals(
                        pair.Key,
                        expandedLayerId,
                        StringComparison.Ordinal);
                    if (pair.Value.value != shouldExpand)
                        pair.Value.SetValueWithoutNotify(shouldExpand);
                }
            }
            finally
            {
                _synchronizingAccordion = false;
                CacheRenderedSelectors();
                RestoreRetainedState(retained);
            }
        }

        VisualElement CreateLayerCard(SerializedProperty layers, int index)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            SerializedProperty itemId = layer.FindPropertyRelative("_itemId");
            SerializedProperty displayName = layer.FindPropertyRelative("_displayName");
            SerializedProperty kind = layer.FindPropertyRelative("_kind");
            SerializedProperty enabled = layer.FindPropertyRelative("_enabled");
            SerializedProperty solo = layer.FindPropertyRelative("_solo");
            TextStudio2DLayerKind layerKind =
                (TextStudio2DLayerKind)kind.enumValueIndex;
            string stableId = GetItemId(itemId);
            string cardKey = string.IsNullOrEmpty(stableId)
                ? index.ToString()
                : stableId;
            string cardName = "ts-2d-style-layer-" + index;

            TextStudioInspectorElements.LayerCardShell shell = null;
            shell = TextStudioInspectorElements.CreateLayerCardShell(
                cardName,
                BuildLayerTitle(displayName, layerKind, enabled, ((TextStudio2DLayer)layer.boxedValue).AppearanceActivation),
                string.Equals(_expandedLayerId, cardKey, StringComparison.Ordinal),
                "Open this appearance layer to edit its scope, treatment, relationships, and execution settings.",
                () => ToggleVisibility(index),
                () => ToggleSolo(index),
                () => MoveLayer(index, -1),
                () => MoveLayer(index, 1),
                () => RemoveLayer(index));
            shell.Card.AddToClassList("ts-2d-style-layer-card");
            shell.Foldout.name = "ts-2d-style-layer-foldout-" + index;
            shell.Foldout.viewDataKey = string.Empty;
            shell.Actions.name = "ts-2d-style-layer-actions-" + index;
            shell.VisibilityButton.name = "ts-2d-style-layer-enabled-" + index;
            shell.SoloButton.name = "ts-2d-style-layer-solo-" + index;
            shell.MoveUpButton.name = "ts-2d-style-layer-up-" + index;
            shell.MoveDownButton.name = "ts-2d-style-layer-down-" + index;
            shell.DeleteButton.name = "ts-2d-style-layer-remove-" + index;
            _layerFoldouts[cardKey] = shell.Foldout;
            shell.Foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == shell.Foldout)
                    HandleLayerFoldoutChanged(cardKey, evt.newValue);
            });

            void RefreshHeader()
            {
                shell.Refresh(
                    BuildLayerTitle(displayName, layerKind, enabled, ((TextStudio2DLayer)layer.boxedValue).AppearanceActivation),
                    enabled.boolValue,
                    solo.boolValue,
                    index > 0,
                    index < layers.arraySize - 1);
            }

            RefreshHeader();
            _layerHeaderRefreshes.Add(RefreshHeader);
            shell.Foldout.TrackPropertyValue(displayName, _ => RefreshHeader());
            shell.Foldout.TrackPropertyValue(enabled, _ => RefreshHeader());
            shell.Foldout.TrackPropertyValue(solo, _ => RefreshHeader());

            PropertyField nameField = CreateField(
                displayName,
                "Name",
                "Name shown in this style's layer stack.");
            nameField.name = "ts-2d-style-layer-name-" + index;
            nameField.AddToClassList("ts-layer-card-name");
            shell.Foldout.Add(nameField);

            if (layerKind == TextStudio2DLayerKind.Group)
                shell.Foldout.Add(CreateActivationEditor(layer, index));
            shell.Foldout.Add(CreateBasicsGroup(layer, layerKind));
            Foldout selectionGroup = (Foldout)CreateSelectorGroup(layer, index);
            shell.Foldout.Add(selectionGroup);
            shell.Foldout.Add(CreateRelationshipsGroup(layers, layer, index, layerKind));
            VisualElement treatment = CreateTreatmentGroup(layer, layerKind);
            if (treatment != null)
            {
                VisualElement mappingSelection = new VisualElement { name = "ts-2d-style-mapping-scope-" + index };
                Label mappingHelp = TextStudioInspectorElements.CreateMutedText(string.Empty);
                mappingSelection.Add(mappingHelp);
                Button editSelection = TextStudioInspectorElements.CreateButton("Edit Selection",
                    "Open Apply Where to choose the text and edit its range bounds. Opening these settings preserves the current selection.",
                    () =>
                    {
                        selectionGroup.value = true;
                        DropdownField selection = selectionGroup.Q<DropdownField>("ts-2d-style-selector-mode-" + index);
                        selection?.Focus();
                        selectionGroup.schedule.Execute(() => selectionGroup.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(selectionGroup));
                    });
                editSelection.name = "ts-2d-style-mapping-selection-" + index;
                editSelection.style.alignSelf = Align.FlexStart;
                mappingSelection.Add(editSelection);
                treatment.Insert(Math.Min(2, treatment.childCount), mappingSelection);

                void RefreshMappingSelection()
                {
                    TextStudio2DLayer current = (TextStudio2DLayer)layer.boxedValue;
                    TextStudio2DPaint paint = UsesPaint(layerKind) ? current.Paint.Resolve() : default;
                    bool nativeFill = layerKind == TextStudio2DLayerKind.Fill &&
                        layer.FindPropertyRelative("_fill").FindPropertyRelative("_preservesNativeAppearance").boolValue;
                    bool range = layerKind == TextStudio2DLayerKind.GraphicEffect && current.GraphicEffect.Mapping == TextStudio2DMappingSpace.Range ||
                        layerKind == TextStudio2DLayerKind.AnimatedEffect && current.AnimatedEffect.Mapping == TextStudio2DMappingSpace.Range ||
                        UsesPaint(layerKind) && !nativeFill && paint.Kind != TextStudio2DPaintKind.Solid && paint.Kind != TextStudio2DPaintKind.DistanceGradient &&
                        paint.Mapping == TextStudio2DMappingSpace.Range && (layerKind != TextStudio2DLayerKind.FauxDepth || current.FauxDepth.PaintMapping == TextStudio2DFauxDepthPaintMapping.Surface);
                    mappingSelection.style.display = range ? DisplayStyle.Flex : DisplayStyle.None;
                    mappingHelp.text = SelectionMode(current.Selector) == 0
                        ? "Range currently spans all text. Set its bounds in Apply Where."
                        : "Range uses the text selected in Apply Where.";
                }

                mappingSelection.TrackSerializedObjectValue(_serializedObject, _ => RefreshMappingSelection());
                treatment.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshMappingSelection());
                treatment.Q<DropdownField>("ts-animated-effect-mapping")?.RegisterValueChangedCallback(_ => RefreshMappingSelection());
                RefreshMappingSelection();
                shell.Foldout.Add(treatment);
            }
            shell.Foldout.Add(CreateAdvancedGroup(layer));
            AddExposureActions(shell.Foldout, layer, index);

            Button duplicate = TextStudioInspectorElements.CreateButton(
                layerKind == TextStudio2DLayerKind.Group ? "Duplicate Group" : "Duplicate Layer",
                "Duplicate this layer and its subtree with independent identities and remapped bindings.",
                () => DuplicateLayer(index));
            duplicate.name = "ts-2d-style-layer-duplicate-" + index;
            shell.Foldout.Add(TextStudioInspectorElements.CreateLayerFooter(duplicate));
            ConfigureRetainedKeys(shell.Card, cardKey);
            return shell.Card;
        }

        VisualElement CreateBasicsGroup(
            SerializedProperty layer,
            TextStudio2DLayerKind kind)
        {
            Foldout group = CreateGroupFoldout("Basics", "basics", true);
            group.Add(CreateField(layer.FindPropertyRelative("_sourceApplicability"), "Source Types",
                "Automatic includes native images in fills, outer shadows, glows, and shared decorations. Text And Color also applies detailed treatments to images when their source supports them. Color Only excludes ordinary font glyphs."));
            group.Add(CreateField(
                layer.FindPropertyRelative("_opacity"),
                "Opacity",
                "Overall opacity applied by this layer before it blends with earlier layers."));
            if (kind != TextStudio2DLayerKind.ColorAdjustment) group.Add(CreateField(
                layer.FindPropertyRelative("_blendMode"),
                "Blend Mode",
                "How this layer combines with the appearance accumulated before it."));
            if (kind != TextStudio2DLayerKind.Group &&
                kind != TextStudio2DLayerKind.Mask && !TextStudio2DColorAdjustmentUtility.IsAdjustment(kind))
            {
                group.Add(CreateField(
                    layer.FindPropertyRelative("_participatesInAnimatedTint"),
                    "Animated Tint",
                    "Allow Text Studio color animation to tint this layer."));
            }
            if (kind == TextStudio2DLayerKind.Group)
                group.Add(CreateField(layer.FindPropertyRelative("_groupOrder"), "Painter Order",
                    "Inherit uses the parent order. By Layer finishes each layer across this range. By Character finishes source layers for each drawing cluster; neighborhood effects and composite adjustments process the completed lower image once. Nested groups compose once at their first selected character."));
            if (kind == TextStudio2DLayerKind.Decoration)
                group.Add(CreateField(layer.FindPropertyRelative("_sharedSurfaceOrder"), "Shared Surface Order",
                    "When the owning scope uses By Character, draw this decoration and its treatments once before or after the characters. By Layer follows the stack position."));
            return group;
        }

        VisualElement CreateSelectorGroup(SerializedProperty layer, int layerIndex)
        {
            Foldout group = CreateGroupFoldout("Apply Where", "selector", false);
            group.name = "ts-2d-style-selector-" + layerIndex;
            SerializedProperty selector = layer.FindPropertyRelative("_selector");
            if (selector == null)
                return group;

            TextStudioApplicationSelector value =
                (TextStudioApplicationSelector)selector.boxedValue;
            List<string> modes = new List<string> { "All", "None", "Range", "Tag", "Advanced" };
            int modeIndex = SelectionMode(value);
            string identity = GetItemId(layer.FindPropertyRelative("_itemId"));
            if (_advancedSelectors.Contains(identity)) modeIndex = 4;
            DropdownField mode = new DropdownField("Selection", modes, modeIndex)
            {
                name = "ts-2d-style-selector-mode-" + layerIndex,
                tooltip = "Narrow this active appearance to all units, none, a range, an appearance or effect tag, or a compound expression."
            };
            mode.AddToClassList("unity-base-field__aligned");
            mode.AddToClassList("ts-appearance-responsive-field");
            mode.RegisterValueChangedCallback(evt =>
            {
                int selected = modes.IndexOf(evt.newValue);
                if (selected == modeIndex) return;
                if (selected == 4)
                {
                    _advancedSelectors.Add(identity);
                    RebuildLayerCards();
                    return;
                }
                if (SelectionMode(value) == 4 && !EditorUtility.DisplayDialog("Replace Selection Expression", "This replaces the existing compound selection. Undo restores the complete expression.", "Replace", "Keep Expression"))
                {
                    mode.SetValueWithoutNotify(modes[modeIndex]);
                    return;
                }
                _advancedSelectors.Remove(identity);
                TextStudioApplicationSelector replacement = selected switch
                {
                    0 => TextStudioApplicationSelector.All,
                    1 => TextStudioApplicationSelector.None,
                    3 => TextStudioApplicationSelector.Create(new[] { TextStudioApplicationSelectorNode.AppearanceTag(string.Empty) }, 0),
                    _ => TextStudioApplicationSelector.Create(new[] { TextStudioApplicationSelectorNode.Range(0, 1) }, 0)
                };
                SetSelector(layerIndex, replacement, "Change 2D Layer Selection");
            });
            group.Add(mode);
            if (modeIndex < 2) return group;
            if (modeIndex == 2) group.Add(TextStudioSelectionField.CreateRangeNode(selector.FindPropertyRelative("_nodes").GetArrayElementAtIndex(value.Root)));
            if (modeIndex == 3) group.Add(CreateTypedTagSelector(selector.FindPropertyRelative("_nodes").GetArrayElementAtIndex(value.Root)));

            SerializedProperty root = selector.FindPropertyRelative("_root");
            SerializedProperty nodes = selector.FindPropertyRelative("_nodes");
            if (root == null || nodes == null || !nodes.isArray)
                return group;

            VisualElement expression = new VisualElement
            {
                name = "ts-2d-style-selector-expression-" + layerIndex
            };
            expression.AddToClassList("ts-2d-style-selector-expression");
            if (modeIndex == 4) CreateSelectorConditionList(
                expression,
                nodes,
                root.intValue,
                layerIndex,
                string.Empty,
                new HashSet<int>());
            group.Add(expression);

            if (modeIndex != 4)
            {
                Button addRequired = TextStudioInspectorElements.CreateButton(
                    "Add Required Condition",
                    "Add another predicate that must also match.",
                    () => ShowSelectorConditionMenu(layerIndex, TextStudioSelectorNodeKind.And));
                Button addAlternative = TextStudioInspectorElements.CreateButton(
                    "Add Alternative",
                    "Add another predicate where either branch may match.",
                    () => ShowSelectorConditionMenu(layerIndex, TextStudioSelectorNodeKind.Or));
                group.Add(TextStudioInspectorElements.CreateButtonRow(
                    addRequired,
                    addAlternative));
            }
            bool negated = value.TryGetNode(
                               value.Root,
                               out TextStudioApplicationSelectorNode rootNode) &&
                           rootNode.Kind == TextStudioSelectorNodeKind.Not;
            group.Add(TextStudioInspectorElements.CreateButton(
                negated ? "Remove Exclusion" : "Exclude This Selection",
                negated
                    ? "Use this expression directly instead of excluding its matches."
                    : "Wrap this expression in a Not condition so matching glyphs are excluded.",
                () => ToggleSelectorNegation(layerIndex)));
            return group;
        }

        VisualElement CreateRelationshipsGroup(
            SerializedProperty layers,
            SerializedProperty layer,
            int layerIndex,
            TextStudio2DLayerKind layerKind)
        {
            Foldout group = CreateGroupFoldout("Relationships", "relationships", false);
            string currentId = GetItemId(layer.FindPropertyRelative("_itemId"));
            group.Add(CreateRelationshipDropdown(
                "Parent Group",
                "Place this layer inside a Group layer while keeping stable ID references across reordering.",
                layers,
                layerIndex,
                    currentId,
                    "_parentId",
                    candidateKind => candidateKind == TextStudio2DLayerKind.Group,
                    excludeCycles: true));

            if (layerKind == TextStudio2DLayerKind.Mask)
            {
                group.Add(CreateRelationshipDropdown(
                    "Mask Source",
                    "Choose the layer whose coverage supplies this mask.",
                    layers,
                    layerIndex,
                    currentId,
                    "_mask._sourceItemId",
                    candidateKind => candidateKind != TextStudio2DLayerKind.Mask && !TextStudio2DColorAdjustmentUtility.IsAdjustment(candidateKind),
                    excludeCycles: true));
            }
            else if (layerKind != TextStudio2DLayerKind.ColorAdjustment)
            {
                group.Add(CreateRelationshipDropdown(
                    "Mask",
                    "Optionally limit this layer using an authored Mask layer.",
                    layers,
                    layerIndex,
                    currentId,
                    "_maskId",
                    candidateKind => candidateKind == TextStudio2DLayerKind.Mask,
                    excludeCycles: true));
            }
            return group;
        }

        VisualElement CreateTreatmentGroup(
            SerializedProperty layer,
            TextStudio2DLayerKind kind)
        {
            Foldout group = CreateGroupFoldout("Treatment", "treatment", true);
            SerializedProperty paint = layer.FindPropertyRelative("_paint");
            if (kind == TextStudio2DLayerKind.Fill)
            {
                AddFillTreatment(group, layer);
                return group;
            }
            if (UsesPaint(kind))
            {
                group.Add(CreatePaintEditor(paint));
                if (kind == TextStudio2DLayerKind.FauxDepth)
                {
                    group.Add(TextStudioInspectorElements.CreateMutedText(
                        "Paint controls color placement. Depth direction and length below control the extrusion geometry."));
                }
            }

            string specPath = kind switch
            {
                TextStudio2DLayerKind.Fill => "_fill",
                TextStudio2DLayerKind.Stroke => "_stroke",
                TextStudio2DLayerKind.InnerShadow => "_shadow",
                TextStudio2DLayerKind.OuterShadow => "_shadow",
                TextStudio2DLayerKind.InnerGlow => "_glow",
                TextStudio2DLayerKind.OuterGlow => "_glow",
                TextStudio2DLayerKind.Bevel => "_bevel",
                TextStudio2DLayerKind.FauxDepth => "_fauxDepth",
                TextStudio2DLayerKind.Decoration => "_decoration",
                TextStudio2DLayerKind.Mask => "_mask",
                TextStudio2DLayerKind.MaterialResponse => "_materialResponse",
                TextStudio2DLayerKind.GraphicEffect => "_graphicEffect",
                TextStudio2DLayerKind.AnimatedEffect => "_animatedEffect",
                TextStudio2DLayerKind.ColorAdjustment => "_colorAdjustment",
                TextStudio2DLayerKind.CompositeAdjustment => "_colorAdjustment",
                _ => string.Empty
            };
            if (kind == TextStudio2DLayerKind.Mask)
            {
                SerializedProperty mask = layer.FindPropertyRelative("_mask");
                group.Add(CreateField(
                    mask.FindPropertyRelative("_invert"),
                    "Invert",
                    "Invert the selected mask source before applying it."));
                group.Add(CreateField(
                    mask.FindPropertyRelative("_feather"),
                    "Feather",
                    "Soften the edge of the selected mask source."));
            }
            else if (!string.IsNullOrEmpty(specPath))
            {
                SerializedProperty treatment =
                    layer.FindPropertyRelative(specPath);
                if (TextStudio2DColorAdjustmentUtility.IsAdjustment(kind))
                {
                    AddColorAdjustmentChildren(group, treatment, kind);
                }
                else if (kind == TextStudio2DLayerKind.Stroke)
                {
                    AddStrokeChildren(group, treatment);
                }
                else if (kind == TextStudio2DLayerKind.InnerShadow ||
                    kind == TextStudio2DLayerKind.OuterShadow ||
                    kind == TextStudio2DLayerKind.InnerGlow ||
                    kind == TextStudio2DLayerKind.OuterGlow)
                {
                    bool supportsTextWideBlur =
                        kind == TextStudio2DLayerKind.OuterShadow ||
                        kind == TextStudio2DLayerKind.OuterGlow;
                    AddSoftEffectChildren(
                        group,
                        treatment,
                        supportsTextWideBlur);
                }
                else if (kind == TextStudio2DLayerKind.FauxDepth)
                {
                    AddFauxDepthChildren(group, treatment, paint);
                }
                else if (kind == TextStudio2DLayerKind.Decoration)
                {
                    AddDecorationChildren(group, treatment);
                }
                else
                {
                    AddApplicableTreatmentChildren(group, treatment, kind);
                }
            }

            if (kind == TextStudio2DLayerKind.Group)
            {
                group.Add(TextStudioInspectorElements.CreateMutedText(
                    "A Group organizes child layers. Use Parent Group on other cards to assign them here."));
            }
            return group;
        }

        VisualElement CreatePaintEditor(SerializedProperty paint)
        {
            return new TextStudio2DPaintAuthoringElement(paint);
        }

        VisualElement CreateAdvancedGroup(SerializedProperty layer)
        {
            Foldout group = CreateGroupFoldout("Advanced", "advanced", false);
            group.Add(CreateField(
                layer.FindPropertyRelative("_executionPath"),
                "Execution Path",
                "Choose Automatic unless this layer must use a specific 2D rendering backend."));
            group.Add(CreateField(
                layer.FindPropertyRelative("_qualityTier"),
                "Quality Tier",
                "Balance responsiveness and visual fidelity for this layer."));
            return group;
        }

        VisualElement CreateRelationshipDropdown(
            string label,
            string tooltip,
            SerializedProperty layers,
            int layerIndex,
            string currentId,
            string relationshipPath,
            Func<TextStudio2DLayerKind, bool> acceptsKind,
            bool excludeCycles)
        {
            List<string> labels = new List<string> { "None" };
            List<string> ids = new List<string> { string.Empty };
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (i == layerIndex)
                    continue;
                SerializedProperty candidate = layers.GetArrayElementAtIndex(i);
                TextStudio2DLayerKind candidateKind =
                    (TextStudio2DLayerKind)candidate.FindPropertyRelative("_kind").enumValueIndex;
                if (!acceptsKind(candidateKind))
                    continue;
                string candidateId = GetItemId(candidate.FindPropertyRelative("_itemId"));
                if (excludeCycles &&
                    WouldCreateRelationshipCycle(layers, currentId, candidateId))
                {
                    continue;
                }
                string candidateName = candidate.FindPropertyRelative("_displayName").stringValue;
                labels.Add(BuildUniqueChoiceLabel(
                    labels,
                    candidateName,
                    candidateKind));
                ids.Add(candidateId);
            }

            SerializedProperty relationship = FindRelativePath(
                layers.GetArrayElementAtIndex(layerIndex),
                relationshipPath);
            string selectedId = GetItemId(relationship);
            int selectedIndex = Mathf.Max(0, ids.IndexOf(selectedId));
            DropdownField dropdown = new DropdownField(label, labels, selectedIndex)
            {
                name = "ts-2d-style-relationship-" +
                       relationshipPath.TrimStart('_')
                           .Replace("._", "-")
                           .Replace("_", "-") +
                       "-" + layerIndex,
                tooltip = tooltip
            };
            dropdown.AddToClassList("unity-base-field__aligned");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int choiceIndex = labels.IndexOf(evt.newValue);
                if (choiceIndex < 0)
                    return;
                SetRelationship(
                    layerIndex,
                    relationshipPath,
                    ids[choiceIndex],
                    "Change 2D Layer Relationship");
            });
            return dropdown;
        }

        TextStudioStackItemId _addLayerParent;
        void ShowAddLayerMenu() { _addLayerParent = default; BuildAddLayerMenu(); }
        void ShowAddLayerMenuForGroup(TextStudioStackItemId parent) { _addLayerParent = parent; BuildAddLayerMenu(); }

        void BuildAddLayerMenu()
        {
            GenericMenu menu = new GenericMenu();
            AddLayerMenuItem(menu, "Paint/Fill", TextStudio2DLayerKind.Fill);
            AddLayerMenuItem(menu, "Paint/Stroke", TextStudio2DLayerKind.Stroke);
            AddLayerMenuItem(menu, "Light And Depth/Inner Shadow", TextStudio2DLayerKind.InnerShadow);
            AddLayerMenuItem(menu, "Light And Depth/Outer Shadow", TextStudio2DLayerKind.OuterShadow);
            AddLayerMenuItem(menu, "Light And Depth/Inner Glow", TextStudio2DLayerKind.InnerGlow);
            AddLayerMenuItem(menu, "Light And Depth/Outer Glow", TextStudio2DLayerKind.OuterGlow);
            AddLayerMenuItem(menu, "Light And Depth/Bevel", TextStudio2DLayerKind.Bevel);
            AddLayerMenuItem(menu, "Light And Depth/Faux Depth", TextStudio2DLayerKind.FauxDepth);
            AddLayerMenuItem(menu, "Structure/Decoration", TextStudio2DLayerKind.Decoration);
            AddLayerMenuItem(menu, "Structure/Mask", TextStudio2DLayerKind.Mask);
            AddLayerMenuItem(menu, "Structure/Group", TextStudio2DLayerKind.Group);
            menu.AddItem(new GUIContent("Structure/Add Inline Style"), false, AddInlineStyle);
            AddLayerMenuItem(menu, "Advanced/Material Response", TextStudio2DLayerKind.MaterialResponse);
            AddLayerMenuItem(menu, "Advanced/Graphic Effect", TextStudio2DLayerKind.GraphicEffect);
            AddLayerMenuItem(menu, "Advanced/Animated Effect", TextStudio2DLayerKind.AnimatedEffect);
            AddLayerMenuItem(menu, "Color/Color Adjustment", TextStudio2DLayerKind.ColorAdjustment);
            AddLayerMenuItem(menu, "Color/Composite Adjustment", TextStudio2DLayerKind.CompositeAdjustment);
            menu.ShowAsContext();
        }

        void AddLayerMenuItem(
            GenericMenu menu,
            string path,
            TextStudio2DLayerKind kind)
        {
            menu.AddItem(new GUIContent(path), false, () => AddLayer(kind));
        }

        void AddLayer(TextStudio2DLayerKind kind)
        {
            TextStudio2DLayer layer = CreateDefaultLayerForAuthoring(kind).WithRelationships(_addLayerParent);
            MutateLayers("Add 2D Appearance Layer", layers =>
            {
                int index = layers.arraySize;
                if (_addLayerParent.IsValid)
                {
                    for (int i = 0; i < layers.arraySize; i++)
                    {
                        if (GetItemId(layers.GetArrayElementAtIndex(i).FindPropertyRelative("_itemId")) != _addLayerParent.Value) continue;
                        List<int> subtree = CollectSubtree(layers, i);
                        index = subtree[subtree.Count - 1] + 1;
                        break;
                    }
                }
                layers.InsertArrayElementAtIndex(index);
                layers.GetArrayElementAtIndex(index).boxedValue = layer;
                _expandedLayerId = layer.ItemId.Value;
            });
        }

        void DuplicateLayer(int index)
        {
            MutateLayers("Duplicate 2D Appearance Subtree", layers => DuplicateSubtree(layers, index));
        }

        void ToggleVisibility(int index)
        {
            MutateLayers("Mute 2D Appearance Layer", layers =>
            {
                if (index < 0 || index >= layers.arraySize)
                    return;
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                SerializedProperty enabled = layer.FindPropertyRelative("_enabled");
                SerializedProperty solo = layer.FindPropertyRelative("_solo");
                enabled.boolValue = !enabled.boolValue;
                if (!enabled.boolValue)
                    solo.boolValue = false;
            }, rebuildCards: false);
        }

        void ToggleSolo(int index)
        {
            MutateLayers("Solo 2D Appearance Layer", layers =>
            {
                if (index < 0 || index >= layers.arraySize)
                    return;
                SerializedProperty selected = layers.GetArrayElementAtIndex(index);
                bool activate = selected.FindPropertyRelative("_enabled").boolValue &&
                                !selected.FindPropertyRelative("_solo").boolValue;
                TextStudio2DLayer[] authored = new TextStudio2DLayer[layers.arraySize];
                for (int i = 0; i < authored.Length; i++) authored[i] = (TextStudio2DLayer)layers.GetArrayElementAtIndex(i).boxedValue;
                int[] domains = TextStudioAppearanceDomains.Build(authored);
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (domains[i] != domains[index]) continue;
                    layers.GetArrayElementAtIndex(i).FindPropertyRelative("_solo").boolValue = activate && i == index;
                }
            }, rebuildCards: false);
        }

        void MoveLayer(int index, int offset)
        {
            MutateLayers("Reorder 2D Appearance Subtree", layers => MoveSubtree(layers, index, offset));
        }

        void RemoveLayer(int index)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty layers = FindProperty("_layers");
            if (layers == null || index < 0 || index >= layers.arraySize)
                return;
            SerializedProperty targetLayer = layers.GetArrayElementAtIndex(index);
            string targetId = GetItemId(targetLayer.FindPropertyRelative("_itemId"));
            string targetName = targetLayer.FindPropertyRelative("_displayName").stringValue;
            List<int> subtree = CollectSubtree(layers, index);
            List<string> dependents = new List<string>();
            foreach (int child in subtree)
            {
                string removedId = GetItemId(layers.GetArrayElementAtIndex(child).FindPropertyRelative("_itemId"));
                foreach (string dependent in FindDependents(layers, removedId, child))
                    if (!dependents.Contains(dependent)) dependents.Add(dependent);
            }
            if (dependents.Count > 0 || subtree.Count > 1)
            {
                string message = "Delete '" + targetName + "'" + (subtree.Count > 1 ? " and its " + (subtree.Count - 1) + " child layers" : string.Empty) + "?";
                if (dependents.Count > 0) message += "\n\nReferences from these layers will be cleared where the layer is retained:\n\n" + string.Join("\n", dependents) + "\n\nExposed parameters retain unavailable bindings for repair.";
                bool confirmed = DeleteConfirmationHandler != null
                    ? DeleteConfirmationHandler(
                        "Delete Referenced Appearance Layer",
                        message,
                        "Delete and Clear References",
                        "Cancel")
                    : EditorUtility.DisplayDialog(
                        "Delete Referenced Appearance Layer",
                        message,
                        "Delete and Clear References",
                        "Cancel");
                if (!confirmed)
                {
                    return;
                }
            }

            MutateLayers("Delete 2D Appearance Layer", mutableLayers =>
            {
                RemoveSubtree(mutableLayers, index);
                if (string.Equals(_expandedLayerId, targetId, StringComparison.Ordinal))
                    _expandedLayerId = string.Empty;
            });
        }

        void MutateLayers(
            string undoName,
            Action<SerializedProperty> mutation,
            bool rebuildCards = true,
            RetainedState retained = null)
        {
            if (_serializedObject.isEditingMultipleObjects)
                return;
            CaptureExpandedLayerFromCards();
            _serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty layers = FindProperty("_layers");
            if (layers == null || !layers.isArray)
                return;

            Object[] owners = _serializedObject.targetObjects;
            Undo.RecordObjects(owners, undoName);
            mutation(layers);
            string expandedLayerId = _expandedLayerId;
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _expandedLayerId = expandedLayerId;
            MarkTargetsDirty(owners);
            SaveExpandedLayerState();
            if (rebuildCards)
            {
                RebuildLayerCards(retained);
            }
            else
            {
                _serializedObject.UpdateIfRequiredOrScript();
                foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
                {
                    pair.Value.SetValueWithoutNotify(string.Equals(
                        pair.Key,
                        expandedLayerId,
                        StringComparison.Ordinal));
                }
                for (int i = 0; i < _layerHeaderRefreshes.Count; i++)
                    _layerHeaderRefreshes[i]?.Invoke();
            }
            TextStudioAppearanceAuthoringMetadata.Invalidate();
            RefreshValidation();
        }

        void CaptureExpandedLayerFromCards()
        {
            if (!string.IsNullOrEmpty(_expandedLayerId) &&
                _layerFoldouts.TryGetValue(_expandedLayerId, out Foldout trackedFoldout) &&
                trackedFoldout.value)
            {
                return;
            }

            foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
            {
                if (!pair.Value.value)
                    continue;
                _expandedLayerId = pair.Key;
                return;
            }
        }

        void SetSelector(
            int layerIndex,
            TextStudioApplicationSelector selector,
            string undoName,
            RetainedState retained = null)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            MutateLayers(undoName, layers =>
            {
                if (layerIndex < 0 || layerIndex >= layers.arraySize)
                    return;
                SerializedProperty layerProperty = layers.GetArrayElementAtIndex(layerIndex);
                TextStudio2DLayer layer = (TextStudio2DLayer)layerProperty.boxedValue;
                layerProperty.boxedValue = layer.WithSelector(selector);
            }, retained: retained);
        }

        void ShowSelectorConditionMenu(
            int layerIndex,
            TextStudioSelectorNodeKind combination)
        {
            GenericMenu menu = new GenericMenu();
            AddSelectorMenuItem(menu, "Range", layerIndex, combination, TextStudioSelectorNodeKind.Range);
            AddSelectorMenuItem(menu, "Effect Tag", layerIndex, combination, TextStudioSelectorNodeKind.Tag);
            AddSelectorMenuItem(menu, "Appearance Tag", layerIndex, combination, TextStudioSelectorNodeKind.AppearanceTag);
            AddSelectorMenuItem(menu, "Source Color", layerIndex, combination, TextStudioSelectorNodeKind.SourceColor);
            AddSelectorMenuItem(menu, "Semantic State", layerIndex, combination, TextStudioSelectorNodeKind.SemanticState);
            AddSelectorMenuItem(menu, "Coverage", layerIndex, combination, TextStudioSelectorNodeKind.Coverage);
            AddSelectorMenuItem(menu, "Render Property", layerIndex, combination, TextStudioSelectorNodeKind.RenderProperty);
            menu.ShowAsContext();
        }

        void AddSelectorMenuItem(
            GenericMenu menu,
            string label,
            int layerIndex,
            TextStudioSelectorNodeKind combination,
            TextStudioSelectorNodeKind predicate)
        {
            menu.AddItem(
                new GUIContent(label),
                false,
                () => AddSelectorCondition(layerIndex, combination, predicate));
        }

        void AddSelectorCondition(
            int layerIndex,
            TextStudioSelectorNodeKind combination,
            TextStudioSelectorNodeKind predicateKind)
        {
            SerializedProperty layers = FindProperty("_layers");
            if (layers == null || layerIndex < 0 || layerIndex >= layers.arraySize)
                return;
            TextStudio2DLayer layer = (TextStudio2DLayer)layers.GetArrayElementAtIndex(layerIndex).boxedValue;
            TextStudioApplicationSelector selector = layer.Selector;
            TextStudioApplicationSelectorNode[] source = selector.GetNodesUnsafe() ??
                                                           Array.Empty<TextStudioApplicationSelectorNode>();
            TextStudioApplicationSelectorNode predicate = CreateSelectorPredicate(predicateKind);
            if (source.Length == 0 || selector.IsTrivialAll)
            {
                SetSelector(
                    layerIndex,
                    TextStudioApplicationSelector.Create(new[] { predicate }, 0),
                    "Add 2D Layer Selection Condition");
                return;
            }

            TextStudioApplicationSelectorNode[] nodes =
                new TextStudioApplicationSelectorNode[source.Length + 2];
            Array.Copy(source, nodes, source.Length);
            int predicateIndex = source.Length;
            nodes[predicateIndex] = predicate;
            nodes[predicateIndex + 1] = combination == TextStudioSelectorNodeKind.Or
                ? TextStudioApplicationSelectorNode.Or(selector.Root, predicateIndex)
                : TextStudioApplicationSelectorNode.And(selector.Root, predicateIndex);
            SetSelector(
                layerIndex,
                TextStudioApplicationSelector.Create(nodes, predicateIndex + 1),
                "Add 2D Layer Selection Condition");
        }

        void ToggleSelectorNegation(int layerIndex)
        {
            SerializedProperty layers = FindProperty("_layers");
            if (layers == null || layerIndex < 0 || layerIndex >= layers.arraySize)
                return;
            TextStudio2DLayer layer = (TextStudio2DLayer)layers.GetArrayElementAtIndex(layerIndex).boxedValue;
            TextStudioApplicationSelector selector = layer.Selector;
            TextStudioApplicationSelectorNode[] source = selector.GetNodesUnsafe() ??
                                                           Array.Empty<TextStudioApplicationSelectorNode>();
            if (source.Length == 0 || selector.Root < 0 || selector.Root >= source.Length)
                return;

            TextStudioApplicationSelectorNode root = source[selector.Root];
            if (root.Kind == TextStudioSelectorNodeKind.Not)
            {
                TextStudioApplicationSelectorNode[] unwrappedNodes = source;
                if (selector.Root == source.Length - 1)
                {
                    unwrappedNodes = new TextStudioApplicationSelectorNode[source.Length - 1];
                    Array.Copy(source, unwrappedNodes, unwrappedNodes.Length);
                }
                TextStudioApplicationSelector unwrapped = TextStudioApplicationSelector.Create(unwrappedNodes, root.Left);
                RetainedState retained = CaptureSelectorRootState(layerIndex, false, unwrapped);
                SetSelector(
                    layerIndex,
                    unwrapped,
                    "Remove 2D Layer Selection Exclusion",
                    retained);
                return;
            }

            TextStudioApplicationSelectorNode[] nodes =
                new TextStudioApplicationSelectorNode[source.Length + 1];
            Array.Copy(source, nodes, source.Length);
            nodes[source.Length] = TextStudioApplicationSelectorNode.Not(selector.Root);
            TextStudioApplicationSelector wrapped = TextStudioApplicationSelector.Create(nodes, source.Length);
            RetainedState wrappedState = CaptureSelectorRootState(layerIndex, true, wrapped);
            SetSelector(
                layerIndex,
                wrapped,
                "Exclude 2D Layer Selection",
                wrappedState);
        }

        void SetRelationship(
            int layerIndex,
            string relationshipPath,
            string value,
            string undoName)
        {
            MutateLayers(undoName, layers =>
            {
                if (layerIndex < 0 || layerIndex >= layers.arraySize)
                    return;
                SerializedProperty relationship = FindRelativePath(
                    layers.GetArrayElementAtIndex(layerIndex),
                    relationshipPath);
                SerializedProperty idValue = relationship?.FindPropertyRelative("_value");
                if (idValue != null)
                    idValue.stringValue = value ?? string.Empty;
            });
        }

        internal void RefreshValidation()
        {
            if (_validation == null)
                return;
            _validation.Clear();
            if (_serializedObject.isEditingMultipleObjects || Target == null)
            {
                _validation.parent.style.display = DisplayStyle.None;
                return;
            }

            CompiledTextStudio2DStyle compiled = _compile();
            int issueCount = 0;
            TextStudioAppearanceDefinitionTable definitions = TextStudioAppearanceDefinitions.Resolve(ReadSettings());
            foreach (string message in definitions.Diagnostics)
            {
                issueCount++;
                _validation.Add(TextStudioInspectorElements.CreateStatus(message, CommonInspectorStatusType.Warning));
            }
            foreach (TextStudioAppearanceDefinition definition in definitions.Definitions)
            {
                if (definition.IsValid) continue;
                issueCount++;
                _validation.Add(TextStudioInspectorElements.CreateStatus("$" + definition.Id + ": " + definition.Diagnostic, CommonInspectorStatusType.Warning));
            }
            for (int i = 0; i < compiled.DiagnosticCount; i++)
            {
                TextStudioStackDiagnostic diagnostic = compiled.GetDiagnostic(i);
                if (diagnostic.Severity == TextStudioStackDiagnosticSeverity.Information)
                    continue;
                issueCount++;
                _validation.Add(TextStudioInspectorElements.CreateStatus(
                    diagnostic.Message,
                    diagnostic.Severity == TextStudioStackDiagnosticSeverity.Error
                        ? CommonInspectorStatusType.Error
                        : CommonInspectorStatusType.Warning,
                    "ts-2d-style-issue-" + issueCount));
            }
            issueCount += AddBrokenExposureBindings();
            _validation.parent.style.display = issueCount > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        void HandleLayerFoldoutChanged(string stableId, bool expanded)
        {
            if (_synchronizingAccordion)
                return;
            if (!expanded)
            {
                if (string.Equals(_expandedLayerId, stableId, StringComparison.Ordinal))
                    _expandedLayerId = string.Empty;
                SaveExpandedLayerState();
                return;
            }

            _expandedLayerId = stableId;
            SaveExpandedLayerState();
            _synchronizingAccordion = true;
            foreach (KeyValuePair<string, Foldout> pair in _layerFoldouts)
            {
                if (!string.Equals(pair.Key, stableId, StringComparison.Ordinal) &&
                    pair.Value.value)
                {
                    pair.Value.SetValueWithoutNotify(false);
                }
            }
            _synchronizingAccordion = false;
        }

        void HandleUndoRedo()
        {
            if (Target == null)
            {
                Undo.undoRedoPerformed -= HandleUndoRedo;
                return;
            }
            RebuildLayerCards(CaptureUndoRedoSelectorState());
            RefreshValidation();
        }

        void SaveExpandedLayerState()
        {
            if (Target == null)
                return;
            SessionState.SetString(
                ExpansionKey,
                string.IsNullOrEmpty(_expandedLayerId)
                    ? NoExpandedLayer
                    : _expandedLayerId);
        }

        void LoadExpandedLayerState()
        {
            string saved = SessionState.GetString(
                ExpansionKey,
                NoExpandedLayer);
            _expandedLayerId = string.Equals(saved, NoExpandedLayer, StringComparison.Ordinal)
                ? string.Empty
                : saved;
        }

        public void Dispose()
        {
            CancelRetainedRestore();
            _appearanceInsertion?.Dispose(); _appearanceInsertion = null;
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        internal static TextStudio2DLayer CreateDefaultLayerForAuthoring(
            TextStudio2DLayerKind kind)
        {
            return TextStudio2DLayerDefaults.Create(kind);
        }

        static TextStudioApplicationSelectorNode CreateSelectorPredicate(
            TextStudioSelectorNodeKind kind)
        {
            return kind switch
            {
                TextStudioSelectorNodeKind.Tag =>
                    TextStudioApplicationSelectorNode.Tag("tag"),
                TextStudioSelectorNodeKind.AppearanceTag =>
                    TextStudioApplicationSelectorNode.AppearanceTag("style"),
                TextStudioSelectorNodeKind.SourceColor =>
                    TextStudioApplicationSelectorNode.SourceColor(Color.white, 0.05f),
                TextStudioSelectorNodeKind.SemanticState =>
                    TextStudioApplicationSelectorNode.Semantic(
                        TextStudioSelectorSemanticState.Visible),
                TextStudioSelectorNodeKind.Coverage =>
                    TextStudioApplicationSelectorNode.Coverage(
                        TextStudioSelectorComparison.GreaterOrEqual,
                        0.5f),
                TextStudioSelectorNodeKind.RenderProperty =>
                    TextStudioApplicationSelectorNode.RenderProperty(
                        string.Empty,
                        TextStudioSelectorComparison.GreaterOrEqual,
                        0.5f),
                _ => TextStudioApplicationSelectorNode.Range(0, 1)
            };
        }

        static PropertyField CreateField(
            SerializedProperty property,
            string label,
            string tooltip)
        {
            if (property == null)
                return new PropertyField();
            PropertyField field = new PropertyField(property, label)
            {
                tooltip = string.IsNullOrEmpty(property.tooltip)
                    ? tooltip ?? string.Empty
                    : property.tooltip
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList("unity-base-field__aligned");
            if (property.propertyType == SerializedPropertyType.Vector2 || property.propertyType == SerializedPropertyType.Vector3 || property.propertyType == SerializedPropertyType.Vector4)
                field.AddToClassList("ts-appearance-responsive-field");
            return field;
        }

        static Foldout CreateGroupFoldout(
            string title,
            string key,
            bool expanded)
        {
            Foldout foldout = new Foldout
            {
                text = title,
                value = expanded,
                viewDataKey = "text-studio-2d-style-" + key
            };
            foldout.AddToClassList("ts-effect-settings-foldout");
            foldout.AddToClassList("ts-2d-style-group");
            return foldout;
        }

        static void AddImmediateChildren(
            VisualElement parent,
            SerializedProperty composite)
        {
            if (composite == null)
                return;
            SerializedProperty child = composite.Copy();
            SerializedProperty end = child.GetEndProperty();
            int childDepth = composite.depth + 1;
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                if (child.depth != childDepth)
                    continue;
                if (child.name == "_dashPattern" || child.name == "_customPoints")
                {
                    parent.Add(new TextStudio2DParameterArrayEditor(
                        child.Copy(), child.name == "_customPoints"));
                    continue;
                }
                string label = Nicify(child.name);
                parent.Add(CreateField(
                    child.Copy(),
                    label,
                    string.IsNullOrEmpty(child.tooltip)
                        ? "Adjusts the layer's " + label.ToLowerInvariant() + " treatment parameter."
                        : child.tooltip));
            }
        }

        static void AddSoftEffectChildren(
            VisualElement parent,
            SerializedProperty composite,
            bool supportsTextWideBlur)
        {
            if (composite == null)
                return;
            SerializedProperty child = composite.Copy();
            SerializedProperty end = child.GetEndProperty();
            int childDepth = composite.depth + 1;
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                if (child.depth != childDepth)
                    continue;
                string label = Nicify(child.name);
                string tooltip = child.name == "_rendering"
                    ? supportsTextWideBlur
                        ? "Distance Field is compact and evaluated per glyph. Text Wide Blur routes this outer effect through the Hero compositor to produce one continuous text silhouette."
                        : "Inner soft effects are evaluated inside each glyph and use Distance Field rendering."
                    : "Adjusts the layer's " + label.ToLowerInvariant() +
                      " treatment parameter.";
                PropertyField field = CreateField(
                    child.Copy(),
                    label,
                    tooltip);
                if (child.name == "_rendering" && !supportsTextWideBlur)
                {
                    field.SetEnabled(false);
                    field.style.display = DisplayStyle.None;
                }
                parent.Add(field);
            }
        }

        static void AddFauxDepthChildren(
            VisualElement parent,
            SerializedProperty composite,
            SerializedProperty paint)
        {
            if (composite == null)
                return;
            parent.Add(CreateFauxDepthDirectionEditor(
                composite.FindPropertyRelative("_offset")));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_taper"),
                "Taper",
                "Change silhouette scale per unit of depth. Negative values widen it; positive values narrow it. Shorter depth reduces the taper expansion too."));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_falloff"),
                "Falloff",
                "Reduce coverage gradually from the face toward the depth endpoint."));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_softness"),
                "Softness",
                "Soften the silhouette edge along the complete extrusion."));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_silhouetteOutset"),
                "Silhouette Outset",
                "Expand the traced source contour so the extrusion can carry the outer silhouette of a face stroke. Uses the selected Units."));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_endFade"),
                "End Fade",
                "Fade the terminal portion of the extrusion without changing its direction or length."));
            SerializedProperty paintMappingProperty =
                composite.FindPropertyRelative("_paintMapping");
            PropertyField paintMapping = CreateField(
                paintMappingProperty,
                "Depth Paint Projection",
                "Surface preserves the paint's normal glyph or text coordinates. Depth Progress maps the paint from the face to the extrusion endpoint.");
            parent.Add(paintMapping);
            SerializedProperty paintKindProperty =
                paint?.FindPropertyRelative("_kind");
            void RefreshPaintMappingState()
            {
                TextStudio2DPaintKind paintKind = paintKindProperty != null
                    ? (TextStudio2DPaintKind)paintKindProperty.intValue
                    : TextStudio2DPaintKind.Solid;
                bool active = paintKind != TextStudio2DPaintKind.Solid &&
                              paintKind != TextStudio2DPaintKind.DistanceGradient;
                paintMapping.SetEnabled(active);
                paintMapping.tooltip = active
                    ? "Surface preserves the paint's normal glyph or text coordinates. Depth Progress maps the paint from the face to the extrusion endpoint."
                    : paintKind == TextStudio2DPaintKind.DistanceGradient
                        ? "Distance Gradient already follows the traced depth distance, so this projection is not used."
                        : "Choose a gradient, texture, pattern, or procedural-noise paint before selecting a depth projection.";
            }
            if (paintKindProperty != null)
                parent.TrackPropertyValue(paintKindProperty, _ => RefreshPaintMappingState());
            RefreshPaintMappingState();
            SerializedProperty sideLightAngleProperty =
                composite.FindPropertyRelative("_sideLightAngle");
            SerializedProperty sideLightStrengthProperty =
                composite.FindPropertyRelative("_sideLightStrength");
            PropertyField sideLightAngle = CreateField(
                sideLightAngleProperty,
                "Side Light Angle",
                "Rotate only the light evaluated from the refined extrusion contour. This does not change the depth direction.");
            parent.Add(sideLightAngle);
            parent.Add(CreateField(
                sideLightStrengthProperty,
                "Side Light Strength",
                "Shade differently oriented side walls without adding depth slices."));
            void RefreshSideLightState()
            {
                bool active = sideLightStrengthProperty != null &&
                              sideLightStrengthProperty.floatValue > 0.0001f;
                sideLightAngle.SetEnabled(active);
                sideLightAngle.tooltip = active
                    ? "Rotate only the light evaluated from the refined extrusion contour. This does not change the depth direction."
                    : "Increase Side Light Strength above zero before adjusting its angle.";
            }
            if (sideLightStrengthProperty != null)
            {
                parent.TrackPropertyValue(
                    sideLightStrengthProperty,
                    _ => RefreshSideLightState());
            }
            RefreshSideLightState();
            SerializedProperty frontBevelWidthProperty =
                composite.FindPropertyRelative("_frontBevelWidth");
            SerializedProperty frontBevelStrengthProperty =
                composite.FindPropertyRelative("_frontBevelStrength");
            PropertyField frontBevelWidth = CreateField(
                frontBevelWidthProperty,
                "Front Bevel Width",
                "Set the normalized depth occupied by the near-face highlight.");
            parent.Add(frontBevelWidth);
            parent.Add(CreateField(
                frontBevelStrengthProperty,
                "Front Bevel Strength",
                "Brighten the extrusion immediately behind the front face."));
            void RefreshFrontBevelState()
            {
                bool active = frontBevelStrengthProperty != null &&
                              frontBevelStrengthProperty.floatValue > 0.0001f;
                frontBevelWidth.SetEnabled(active);
                frontBevelWidth.tooltip = active
                    ? "Set the normalized depth occupied by the near-face highlight."
                    : "Increase Front Bevel Strength above zero before adjusting its width.";
            }
            if (frontBevelStrengthProperty != null)
            {
                parent.TrackPropertyValue(
                    frontBevelStrengthProperty,
                    _ => RefreshFrontBevelState());
            }
            RefreshFrontBevelState();
            parent.Add(CreateField(
                composite.FindPropertyRelative("_farCapShade"),
                "Far Cap Shade",
                "Darken the terminal depth smoothly before the authored end fade."));
            parent.Add(CreateField(
                composite.FindPropertyRelative("_units"),
                "Units",
                "Author offset and softness relative to the font em or in screen pixels."));
        }

        static VisualElement CreateFauxDepthDirectionEditor(
            SerializedProperty offsetProperty)
        {
            VisualElement editor = new VisualElement
            {
                name = "ts-2d-faux-depth-direction"
            };
            editor.AddToClassList("ts-2d-faux-depth-direction");
            if (offsetProperty == null)
                return editor;

            string propertyPath = offsetProperty.propertyPath;
            SerializedObject owner = offsetProperty.serializedObject;
            FloatField angle = new FloatField("Depth Angle")
            {
                name = "ts-2d-faux-depth-angle",
                tooltip = "Set the extrusion direction in degrees: 0 points right, 90 points up, and -90 points down."
            };
            FloatField length = new FloatField("Depth Length")
            {
                name = "ts-2d-faux-depth-length",
                tooltip = "Set the full extrusion length using the selected depth Units. Zero hides the extrusion, including its taper, softness, and silhouette outset."
            };
            angle.AddToClassList("ts-property-field");
            angle.AddToClassList("unity-base-field__aligned");
            length.AddToClassList("ts-property-field");
            length.AddToClassList("unity-base-field__aligned");
            editor.Add(angle);
            editor.Add(length);

            bool refreshing = false;
            void Refresh()
            {
                if (refreshing || owner == null)
                    return;
                refreshing = true;
                owner.UpdateIfRequiredOrScript();
                SerializedProperty current = owner.FindProperty(propertyPath);
                Vector2 offset = current != null
                    ? current.vector2Value
                    : Vector2.zero;
                float degrees = offset.sqrMagnitude > 0.0000001f
                    ? Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg
                    : angle.value;
                angle.SetValueWithoutNotify(degrees);
                length.SetValueWithoutNotify(offset.magnitude);
                refreshing = false;
            }

            void Apply(float degrees, float magnitude)
            {
                if (refreshing || owner == null)
                    return;
                if (!float.IsFinite(degrees) || !float.IsFinite(magnitude))
                {
                    Refresh();
                    return;
                }
                Object[] targets = owner.targetObjects;
                Undo.RecordObjects(targets, "Edit Faux Depth Direction");
                owner.UpdateIfRequiredOrScript();
                SerializedProperty current = owner.FindProperty(propertyPath);
                if (current == null)
                    return;
                float radians = degrees * Mathf.Deg2Rad;
                float clampedMagnitude = Mathf.Max(0f, magnitude);
                current.vector2Value = new Vector2(
                    Mathf.Cos(radians) * clampedMagnitude,
                    Mathf.Sin(radians) * clampedMagnitude);
                owner.ApplyModifiedProperties();
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                        EditorUtility.SetDirty(targets[i]);
                }
                Refresh();
            }

            angle.RegisterValueChangedCallback(evt =>
                Apply(evt.newValue, length.value));
            length.RegisterValueChangedCallback(evt =>
                Apply(angle.value, evt.newValue));
            editor.TrackPropertyValue(offsetProperty, _ => Refresh());
            Refresh();
            return editor;
        }

        static void AddNodeFields(
            VisualElement parent,
            SerializedProperty node,
            params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty property = node.FindPropertyRelative(names[i]);
                string label = Nicify(names[i]);
                if (names[i] == "_start" || names[i] == "_count")
                {
                    parent.Add(TextStudio2DParameterDrawer.CreateSelectorIndexField(node, property, label));
                    continue;
                }
                if (names[i] == "_value" &&
                    (TextStudioSelectorNodeKind)node.FindPropertyRelative("_kind").intValue ==
                    TextStudioSelectorNodeKind.Coverage)
                {
                    Slider coverage = new Slider(label, 0f, 1f)
                    {
                        bindingPath = property.propertyPath,
                        showInputField = true,
                        tooltip = "Compare glyph coverage from fully transparent (0) to fully covered (1)."
                    };
                    coverage.AddToClassList(BaseField<float>.alignedFieldUssClassName);
                    coverage.AddToClassList("ts-appearance-responsive-field");
                    parent.Add(coverage);
                    continue;
                }
                PropertyField field = CreateField(
                    property,
                    label,
                    "Controls the " + label.ToLowerInvariant() + " for this selection condition.");
                if (property.propertyType != SerializedPropertyType.Boolean)
                    field.AddToClassList("ts-appearance-responsive-field");
                parent.Add(field);
            }
        }

        static bool UsesPaint(TextStudio2DLayerKind kind)
        {
            return kind == TextStudio2DLayerKind.Fill ||
                   kind == TextStudio2DLayerKind.Stroke ||
                   kind == TextStudio2DLayerKind.InnerShadow ||
                   kind == TextStudio2DLayerKind.OuterShadow ||
                   kind == TextStudio2DLayerKind.InnerGlow ||
                   kind == TextStudio2DLayerKind.OuterGlow ||
                   kind == TextStudio2DLayerKind.FauxDepth;
        }

        static string BuildLayerTitle(
            SerializedProperty displayName,
            TextStudio2DLayerKind kind,
            SerializedProperty enabled,
            TextStudioAppearanceActivation activation = default)
        {
            string resolvedName = string.IsNullOrWhiteSpace(displayName?.stringValue)
                ? ObjectNames.NicifyVariableName(kind.ToString())
                : displayName.stringValue.Trim();
            if (activation.Mode == TextStudioAppearanceActivationMode.Tagged)
            {
                TextStudioAppearanceApplyMode mode = activation.Source == TextStudioAppearanceDefinitionSource.Asset && activation.Asset != null && !activation.OverrideApplyMode ? activation.Asset.DefaultApplyMode : activation.ApplyMode;
                return resolvedName + " ($" + activation.Id + " · " + activation.Source + " · " + mode + (enabled != null && !enabled.boolValue ? " · Off" : string.Empty) + ")";
            }
            return resolvedName + " (" + ObjectNames.NicifyVariableName(kind.ToString()) +
                   (enabled != null && !enabled.boolValue ? " · Off" : string.Empty) + ")";
        }

        static string GetSelectorNodeTitle(TextStudioSelectorNodeKind kind)
        {
            return kind switch
            {
                TextStudioSelectorNodeKind.And => "All Conditions",
                TextStudioSelectorNodeKind.Or => "Any Condition",
                TextStudioSelectorNodeKind.Not => "Exclude",
                TextStudioSelectorNodeKind.Tag => "Effect Tag",
                TextStudioSelectorNodeKind.SourceColor => "Source Color",
                TextStudioSelectorNodeKind.SemanticState => "Semantic State",
                TextStudioSelectorNodeKind.RenderProperty => "Render Property",
                _ => ObjectNames.NicifyVariableName(kind.ToString())
            };
        }

        static string Nicify(string serializedName)
        {
            return ObjectNames.NicifyVariableName(
                (serializedName ?? string.Empty).TrimStart('_'));
        }

        static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string GetItemId(SerializedProperty idProperty)
        {
            return idProperty?.FindPropertyRelative("_value")?.stringValue ?? string.Empty;
        }

        static SerializedProperty FindRelativePath(
            SerializedProperty property,
            string path)
        {
            if (property == null || string.IsNullOrEmpty(path))
                return property;
            string[] segments = path.Split('.');
            SerializedProperty current = property;
            for (int i = 0; i < segments.Length && current != null; i++)
                current = current.FindPropertyRelative(segments[i]);
            return current;
        }

        static bool WouldCreateRelationshipCycle(
            SerializedProperty layers,
            string currentId,
            string candidateId)
        {
            if (string.IsNullOrEmpty(candidateId))
                return false;

            Stack<string> pending = new Stack<string>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(candidateId);
            while (pending.Count > 0 && visited.Count <= layers.arraySize)
            {
                string itemId = pending.Pop();
                if (string.Equals(itemId, currentId, StringComparison.Ordinal))
                    return true;
                if (string.IsNullOrEmpty(itemId) || !visited.Add(itemId))
                    continue;
                SerializedProperty layer = FindLayerById(layers, itemId);
                if (layer == null)
                    continue;
                PushRelationship(pending, layer.FindPropertyRelative("_parentId"));
                PushRelationship(pending, layer.FindPropertyRelative("_maskId"));
                TextStudio2DLayerKind kind = (TextStudio2DLayerKind)layer
                    .FindPropertyRelative("_kind").enumValueIndex;
                if (kind == TextStudio2DLayerKind.Mask)
                {
                    PushRelationship(
                        pending,
                        FindRelativePath(layer, "_mask._sourceItemId"));
                }
            }
            return false;
        }

        static SerializedProperty FindLayerById(
            SerializedProperty layers,
            string itemId)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.Equals(
                        GetItemId(layer.FindPropertyRelative("_itemId")),
                        itemId,
                        StringComparison.Ordinal))
                {
                    return layer;
                }
            }
            return null;
        }

        static void PushRelationship(
            Stack<string> pending,
            SerializedProperty relationship)
        {
            string itemId = GetItemId(relationship);
            if (!string.IsNullOrEmpty(itemId))
                pending.Push(itemId);
        }

        static string BuildUniqueChoiceLabel(
            List<string> existing,
            string displayName,
            TextStudio2DLayerKind kind)
        {
            string baseLabel = (string.IsNullOrWhiteSpace(displayName)
                ? ObjectNames.NicifyVariableName(kind.ToString())
                : displayName.Trim()) + " (" + ObjectNames.NicifyVariableName(kind.ToString()) + ")";
            string label = baseLabel;
            int suffix = 2;
            while (existing.Contains(label))
                label = baseLabel + " #" + suffix++;
            return label;
        }

        static List<string> FindDependents(
            SerializedProperty layers,
            string targetId,
            int targetIndex)
        {
            List<string> dependents = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (i == targetIndex)
                    continue;
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                bool references = ReferencesId(layer.FindPropertyRelative("_parentId"), targetId) ||
                                  ReferencesId(layer.FindPropertyRelative("_maskId"), targetId) ||
                                  ReferencesId(
                                      FindRelativePath(layer, "_mask._sourceItemId"),
                                      targetId);
                if (!references)
                    continue;
                string name = layer.FindPropertyRelative("_displayName").stringValue;
                dependents.Add("• " + (string.IsNullOrWhiteSpace(name) ? "Unnamed Layer" : name));
            }
            return dependents;
        }

        static void ClearReferences(
            SerializedProperty layers,
            string targetId,
            int targetIndex)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (i == targetIndex)
                    continue;
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                ClearReference(layer.FindPropertyRelative("_parentId"), targetId);
                ClearReference(layer.FindPropertyRelative("_maskId"), targetId);
                ClearReference(FindRelativePath(layer, "_mask._sourceItemId"), targetId);
            }
        }

        static bool ReferencesId(SerializedProperty id, string targetId)
        {
            return string.Equals(GetItemId(id), targetId, StringComparison.Ordinal);
        }

        static void ClearReference(SerializedProperty id, string targetId)
        {
            if (!ReferencesId(id, targetId))
                return;
            SerializedProperty value = id.FindPropertyRelative("_value");
            if (value != null)
                value.stringValue = string.Empty;
        }

        static void MarkTargetsDirty(Object[] targets)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    EditorUtility.SetDirty(targets[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(targets[i]);
                }
            }
        }
    }
}
