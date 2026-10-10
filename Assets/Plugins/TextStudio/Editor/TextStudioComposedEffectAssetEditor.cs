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
    [CustomEditor(typeof(TextStudioComposedEffectAsset))]
    [CanEditMultipleObjects]
    internal sealed partial class TextStudioComposedEffectAssetEditor : UnityEditor.Editor
    {
        internal const string PreviewSampleText = "Text Studio\nComposed Effect";
        const double PreviewTickIntervalMs = 33.0;
        const string FoldoutStatePrefix = "TextStudio.ComposedEffectInspector.";
        const string NoExpandedLayerState = "<none>";
        const float DefaultPreviewCycleDuration = 1f;
        const float DefaultRawEffectDuration = 1f;
        const int MinimumActivePreviewDurationMultiplier = 1;
        const int DefaultActivePreviewDurationMultiplier = 4;
        const int MaximumActivePreviewDurationMultiplier = 8;

        readonly List<PreviewGlyph> _previewGlyphs = new List<PreviewGlyph>(64);
        readonly List<int> _previewGlyphMap = new List<int>(64);
        readonly List<Button> _phaseButtons = new List<Button>(3);
        readonly List<Button> _speedButtons = new List<Button>(5);
        readonly Dictionary<string, Foldout> _layerFoldouts = new Dictionary<string, Foldout>(StringComparer.Ordinal);
        readonly List<VisualElement> _layerTimingBands = new List<VisualElement>(24);
        readonly List<VisualElement> _layerTimingRevealBlendBands = new List<VisualElement>(8);
        readonly List<VisualElement> _layerTimingPlayheads = new List<VisualElement>(24);
        readonly List<Action> _layerTimingRowRefreshers = new List<Action>(8);
        readonly VisualElement[] _layerTimingPhaseHeaders = new VisualElement[3];
        readonly StringTextSource _previewSource = new StringTextSource();
        readonly TextStudioPipeline _previewPipeline = new TextStudioPipeline();
        readonly AnimationEvaluator _previewEvaluator = new AnimationEvaluator();
        readonly RevealController _previewReveal = new RevealController();
        Vector3[] _previewSourcePositions;
        TextStudioGlyphVisualBounds[] _previewSourceVisualBounds;
        Vector3[] _previewNormalizedPositions;
        TextStudioGlyphColorQuad[] _previewSourceColors;
        bool[] _previewSourceColorValidity;

        SerializedProperty _id;
        SerializedProperty _authoringSpace;
        SerializedProperty _revealActiveBlendDuration;
        SerializedProperty _tagStack;
        SerializedProperty _layers;
        VisualElement _layerRows;
        VisualElement _layerListView;
        VisualElement _layerTimingView;
        VisualElement _layerTimingRows;
        Slider _layerTimingScrub;
        Button _layerListViewButton;
        Button _layerTimingViewButton;
        bool _layerTimingRowsBuilt;
        bool _layerTimingViewSelected;
        float _layerTimingPhaseTime;
        float _layerTimingRevealWindow = -1f;
        float _layerTimingActiveWindow = -1f;
        float _layerTimingHideWindow = -1f;
        float _layerTimingRevealBlendWindow = -1f;
        VisualElement _previewCanvas;
        VisualElement _previewGlyphCanvas;
        Image _rendererPreviewImage;
        Label _rendererPreviewStatus;
        Label _previewSourceColorStatus;
        Label _previewContext;
        Slider _previewScrub;
        Button _previewPlaybackButton;
        Button _previewRepeatButton;
        Button _activePreviewDurationButton;
        Foldout _tagStackFoldout;
        Foldout _visualLayersFoldout;
        VisualElement _validationSection;
        Label _validationLabel;
        AnimationPhase _previewPhase = AnimationPhase.Persistent;
        TextStudioPreviewSegment _previewSegments = TextStudioPreviewSegment.Active;
        float _previewTime = 0.5f;
        float _previewCycleDuration = DefaultPreviewCycleDuration;
        float _previewRevealDuration = DefaultPreviewCycleDuration;
        float _previewActiveDuration = DefaultPreviewCycleDuration;
        float _previewHideDuration = DefaultPreviewCycleDuration;
        float _previewSpeed = 1f;
        int _activePreviewDurationMultiplier = DefaultActivePreviewDurationMultiplier;
        double _lastPreviewTick;
        bool _isPreviewPlaying = true;
        bool _previewRepeat;
        UnityEngine.Object _previewDirtyTarget;
        int _previewDirtyCount = int.MinValue;
        string _foldoutStateScope;
        string _expandedLayerStableId = string.Empty;
        bool _syncingLayerAccordion;
        readonly Dictionary<string, Action> _layerHeaderRefreshes =
            new Dictionary<string, Action>(StringComparer.Ordinal);
        TextStudioPreviewExportSnapshot _livePreviewSnapshot;
        ITextStudioPreviewExportFrameRenderer _livePreviewRenderer;
        int _livePreviewDirtyCount = int.MinValue;
        string _livePreviewRendererId = string.Empty;
        TextStudioPreviewSegment _livePreviewSegments = TextStudioPreviewSegment.None;
        int _livePreviewActiveDurationMultiplier;
        string _livePreviewSoloLayerStableId = string.Empty;
        int _previewContextDirtyCount = int.MinValue;
        AnimationPhase _previewContextPhase = (AnimationPhase)(-1);
        string _previewContextSoloLayerStableId = string.Empty;

        string _soloLayerStableId = string.Empty;
        internal string SoloLayerStableId
        {
            get
            {
                if (target is TextStudioComposedEffectAsset effect)
                    for (int i = 0; i < effect.Layers.Count; i++)
                        if (effect.Layers[i] != null && effect.Layers[i].Enabled &&
                            effect.Layers[i].StableId == _soloLayerStableId)
                            return _soloLayerStableId;
                _soloLayerStableId = string.Empty;
                return string.Empty;
            }
        }
        internal AnimationPhase PreviewPhase => _previewPhase;
        internal TextStudioPreviewSegment PreviewSegments => _previewSegments;
        internal float PreviewCycleDuration => _previewCycleDuration;
        internal float PreviewSpeed => _previewSpeed;
        internal float PreviewTime => _previewTime;
        internal int ActivePreviewDurationMultiplier => _activePreviewDurationMultiplier;
        internal bool PreviewRepeat => _previewRepeat;

        struct PreviewGlyph
        {
            public Label Label;
            public int GlyphIndex;
        }

        readonly struct EffectChoiceEntry
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string DisplayName;
            public readonly string Category;
            public readonly string Description;
            public readonly string SearchText;
            public readonly bool SupportsReducedMotion;
            public readonly ITextStudioEffect PreviewEffect;
            public readonly TextStudioRenderDomain SupportedDomains;
            public readonly TextStudioComposedLayerPresentationInfo Presentation;

            public EffectChoiceEntry(
                string id,
                string label,
                string displayName,
                string category,
                string description,
                string searchText,
                bool supportsReducedMotion,
                ITextStudioEffect previewEffect,
                TextStudioRenderDomain supportedDomains,
                TextStudioComposedLayerPresentationInfo presentation)
            {
                Id = id;
                Label = label;
                DisplayName = displayName;
                Category = category;
                Description = description;
                SearchText = searchText;
                SupportsReducedMotion = supportsReducedMotion;
                PreviewEffect = previewEffect;
                SupportedDomains = supportedDomains;
                Presentation = presentation;
            }
        }

        enum LayerChoiceKind
        {
            Property,
            Effect,
            Writing
        }

        readonly struct LayerChoice
        {
            public readonly LayerChoiceKind Kind;
            public readonly string Name;
            public readonly string Category;
            public readonly string Description;
            public readonly string Source;
            public readonly string SearchText;
            public readonly TextStudioRenderDomain SupportedDomains;
            public readonly TextStudioComposedLayerPresentationInfo Presentation;
            public readonly Action Add;

            public LayerChoice(
                LayerChoiceKind kind,
                string name,
                string category,
                string description,
                string source,
                TextStudioRenderDomain supportedDomains,
                TextStudioComposedLayerPresentationInfo presentation,
                Action add)
            {
                Kind = kind;
                Name = name ?? string.Empty;
                Category = category ?? string.Empty;
                Description = description ?? string.Empty;
                Source = source ?? string.Empty;
                SupportedDomains = supportedDomains;
                Presentation = presentation;
                Add = add;
                SearchText = Name + " " + Category + " " + Description + " " +
                    Source + " " + presentation.PickerMetadata(string.Empty);
            }
        }

        void OnEnable()
        {
            CacheProperties();
            _previewSegments = TextStudioComposedEffectPreviewPreferences.Segments;
            _previewTime = (_previewSegments & TextStudioPreviewSegment.Reveal) != 0
                ? 0f
                : 0.5f;
            _previewDirtyTarget = null;
            _previewDirtyCount = int.MinValue;
            _previewContextDirtyCount = int.MinValue;
            _previewContextPhase = (AnimationPhase)(-1);
            _previewContextSoloLayerStableId = string.Empty;
            _foldoutStateScope = BuildFoldoutStateScope();
            InitializeSpatialPreviewState();
        }

        void OnDisable()
        {
            DisposeSpatialPreviewInteraction();
            DisposeLivePreview();
        }

        public override bool RequiresConstantRepaint()
        {
            return target != null &&
                   (_isPreviewPlaying ||
                    (_spatialHasTargetField && _spatialAutoMove));
        }

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();
            serializedObject.Update();

            if (targets != null && targets.Length > 1)
                return CreateMultiObjectInspector();

            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-composed-effect-editor-root");
            root.AddToClassList("ts-composed-effect-editor-root");

            root.Add(CreateHeader(root));
            root.Add(CreateEffectSettings());
            root.Add(CreatePreview());
            root.Add(CreateTagStack());
            root.Add(CreateVisualLayers());
            root.Add(CreateValidation());
            RegisterSpatialPreviewFocusCallbacks(root);
            SynchronizeLayerAccordion();
            root.RegisterCallback<AttachToPanelEvent>(_ =>
                root.schedule.Execute(() =>
                {
                    SynchronizeLayerAccordion();
                    EnsureLayerTimingRowsCurrent();
                    RefreshLayerTimingPreviewState();
                }).StartingIn(0));
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                DisposeSpatialPreviewInteraction();
                DisposeLivePreview();
            });

            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (target == null || root.panel == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                AdvancePreviewTime();
                RenderPreview();
            }, () =>
            {
                if (target == null || root.panel == null)
                    return;
                AdvancePreviewTime();
                RenderPreview(refreshInspectorState: false);
            }, (long)PreviewTickIntervalMs);

            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (target == null || root.panel == null)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                UpdateValidation();
            }, 250);

            _lastPreviewTick = EditorApplication.timeSinceStartup;
            UpdateValidation();
            RenderPreview();
            return root;
        }

        void CacheProperties()
        {
            _id = serializedObject.FindProperty("_id");
            _authoringSpace = serializedObject.FindProperty("_authoringSpace");
            _revealActiveBlendDuration = serializedObject.FindProperty("_revealActiveBlendDuration");
            _tagStack = serializedObject.FindProperty("_tagStack");
            _layers = serializedObject.FindProperty("_layers");
        }


        sealed class LayerPickerWindow : EditorWindow
        {
            readonly List<LayerChoice> _choices = new List<LayerChoice>();
            TextStudioRenderDomain _requiredDomain;
            TextStudioRenderDomain _availableDomains;
            LayerChoiceKind _selectedKind;
            VisualElement _categoryControl;
            ToolbarSearchField _search;
            Toggle _showUnavailable;
            ScrollView _results;
            readonly List<LayerChoiceView> _choiceViews = new List<LayerChoiceView>();
            VisualElement _emptyResult;

            internal static void Show(
                EditorWindow owner,
                VisualElement anchor,
                List<LayerChoice> choices,
                TextStudioRenderDomain requiredDomain,
                TextStudioRenderDomain availableDomains)
            {
                if (owner == null || anchor == null)
                    return;

                LayerPickerWindow window = CreateInstance<LayerPickerWindow>();
                window._requiredDomain = requiredDomain;
                window._availableDomains = availableDomains;
                window._selectedKind = LayerChoiceKind.Property;
                if (choices != null)
                    window._choices.AddRange(choices);
                CommonUITK.ShowAsDropDown(
                    window,
                    owner,
                    anchor,
                    new Vector2(440f, 480f));
            }

            public void CreateGUI()
            {
                VisualElement root = rootVisualElement;
                root.AddToClassList("ts-inspector-root");
                root.AddToClassList("ts-composed-layer-picker");
                StyleSheet styleSheet = TextStudioTextEditor.LoadTextStudioInspectorStyleSheet();
                if (styleSheet != null)
                    root.styleSheets.Add(styleSheet);

                VisualElement content = new VisualElement
                {
                    name = "ts-composed-layer-picker-content"
                };
                content.AddToClassList("ts-composed-layer-picker-content");
                root.Add(content);

                Label title = new Label("Add Layer");
                title.AddToClassList("ts-section-title");
                content.Add(title);

                _categoryControl = CreateCategoryControl();
                content.Add(_categoryControl);

                _search = new ToolbarSearchField
                {
                    name = "ts-composed-layer-picker-search",
                    tooltip = "Search by name, category, purpose, or source."
                };
                _search.RegisterValueChangedCallback(_ => UpdateResults());
                content.Add(_search);

                _showUnavailable = new Toggle("Show unavailable")
                {
                    name = "ts-composed-layer-picker-unavailable",
                    tooltip = "Reveal choices for the other authoring space and explain why they cannot be added here."
                };
                _showUnavailable.RegisterValueChangedCallback(_ => UpdateResults());

                _results = new ScrollView
                {
                    name = "ts-composed-layer-picker-results"
                };
                _results.AddToClassList("ts-composed-layer-picker-results");
                content.Add(_results);

                VisualElement footer = new VisualElement
                {
                    name = "ts-composed-layer-picker-footer"
                };
                footer.AddToClassList("ts-composed-layer-picker-footer");
                footer.Add(_showUnavailable);
                footer.style.display = HasUnavailableChoices()
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                root.Add(footer);
                root.RegisterCallback<KeyDownEvent>(HandleKeyDown);
                RefreshCategoryControl();
                BuildResults();
                _search.schedule.Execute(() => _search.Focus()).StartingIn(0);
            }

            VisualElement CreateCategoryControl()
            {
                GUIContent[] categories =
                {
                    new GUIContent("Properties", "Show built-in property layers."),
                    new GUIContent("Effects", "Show reusable effect layers."),
                    new GUIContent("Writing", "Show reusable Writing treatment layers.")
                };

                VisualElement control = CommonUITK.CreateSegmentedControl(
                    categories,
                    (int)_selectedKind,
                    index =>
                    {
                        _selectedKind = (LayerChoiceKind)index;
                        RefreshCategoryControl();
                        UpdateResults();
                    },
                    new CommonUITK.SegmentedControlClasses
                    {
                        RootClass = "ts-composed-layer-picker-segmented",
                        ButtonBaseClass = "ts-authoring-mode-button",
                        ButtonClass = "ts-composed-layer-picker-segment-button",
                        ActiveButtonClass = "ts-authoring-mode-active",
                        FirstButtonClass = "ts-composed-layer-picker-segment-first",
                        LastButtonClass = "ts-composed-layer-picker-segment-last"
                    });
                control.name = "ts-composed-layer-picker-categories";
                return control;
            }

            void RefreshCategoryControl()
            {
                CommonUITK.RefreshSegmentedControl(
                    _categoryControl,
                    (int)_selectedKind,
                    "ts-authoring-mode-active");
            }

            void BuildResults()
            {
                if (_results == null)
                    return;

                _results.Clear();
                _choiceViews.Clear();
                for (int i = 0; i < _choices.Count; i++)
                {
                    LayerChoice choice = _choices[i];
                    bool installed = choice.SupportedDomains == TextStudioRenderDomain.None ||
                                     (choice.SupportedDomains & _availableDomains) != TextStudioRenderDomain.None;
                    bool available = (choice.SupportedDomains & _requiredDomain) != 0;
                    Button row = CreateResult(choice, available);
                    _choiceViews.Add(new LayerChoiceView(choice, row, installed, available));
                    _results.Add(row);
                }

                _emptyResult = TextStudioInspectorElements.CreateMutedText("No layers are available for this authoring space.");
                _results.Add(_emptyResult);
                UpdateResults();
            }

            void UpdateResults()
            {
                if (_results == null)
                    return;

                string query = _search?.value?.Trim() ?? string.Empty;
                bool showUnavailable = _showUnavailable?.value ?? false;
                int resultCount = 0;
                for (int i = 0; i < _choiceViews.Count; i++)
                {
                    LayerChoiceView view = _choiceViews[i];
                    bool visible = view.Choice.Kind == _selectedKind &&
                                   view.Installed &&
                                   (query.Length == 0 || view.Choice.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) &&
                                   (view.Available || showUnavailable);
                    view.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (visible)
                        resultCount++;
                }

                if (_emptyResult != null)
                {
                    Label label = _emptyResult as Label;
                    if (label != null)
                        label.text = query.Length > 0
                            ? "No matching layers."
                            : "No layers are available for this authoring space.";
                    _emptyResult.style.display = resultCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            void HandleKeyDown(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopImmediatePropagation();
                    return;
                }
                if (evt.keyCode == KeyCode.Return ||
                    evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (ActivateFirstVisibleChoice())
                        evt.StopImmediatePropagation();
                    return;
                }
                if (evt.keyCode == KeyCode.DownArrow ||
                    evt.keyCode == KeyCode.UpArrow)
                {
                    if (FocusVisibleChoice(evt.keyCode == KeyCode.DownArrow ? 1 : -1))
                        evt.StopImmediatePropagation();
                }
            }

            bool ActivateFirstVisibleChoice()
            {
                for (int i = 0; i < _choiceViews.Count; i++)
                {
                    LayerChoiceView view = _choiceViews[i];
                    if (!view.Available ||
                        view.Root.style.display.value == DisplayStyle.None)
                    {
                        continue;
                    }
                    view.Choice.Add?.Invoke();
                    Close();
                    return true;
                }
                return false;
            }

            bool FocusVisibleChoice(int direction)
            {
                List<VisualElement> visible = new List<VisualElement>();
                VisualElement focused = rootVisualElement.panel?.focusController
                    ?.focusedElement as VisualElement;
                int focusedIndex = -1;
                for (int i = 0; i < _choiceViews.Count; i++)
                {
                    LayerChoiceView view = _choiceViews[i];
                    if (!view.Available ||
                        view.Root.style.display.value == DisplayStyle.None)
                    {
                        continue;
                    }
                    if (ReferenceEquals(view.Root, focused))
                        focusedIndex = visible.Count;
                    visible.Add(view.Root);
                }
                if (visible.Count == 0)
                    return false;
                int next = focusedIndex < 0
                    ? (direction > 0 ? 0 : visible.Count - 1)
                    : (focusedIndex + direction + visible.Count) % visible.Count;
                visible[next].Focus();
                return true;
            }

            readonly struct LayerChoiceView
            {
                public readonly LayerChoice Choice;
                public readonly VisualElement Root;
                public readonly bool Installed;
                public readonly bool Available;

                public LayerChoiceView(LayerChoice choice, VisualElement root, bool installed, bool available)
                {
                    Choice = choice;
                    Root = root;
                    Installed = installed;
                    Available = available;
                }
            }

            bool HasUnavailableChoices()
            {
                for (int i = 0; i < _choices.Count; i++)
                {
                    LayerChoice choice = _choices[i];
                    bool installed = choice.SupportedDomains == TextStudioRenderDomain.None ||
                                     (choice.SupportedDomains & _availableDomains) !=
                                     TextStudioRenderDomain.None;
                    bool compatible = (choice.SupportedDomains & _requiredDomain) !=
                                      TextStudioRenderDomain.None;
                    if (installed && !compatible)
                        return true;
                }

                return false;
            }

            Button CreateResult(LayerChoice choice, bool available)
            {
                Button row = new Button(() =>
                {
                    choice.Add?.Invoke();
                    Close();
                })
                {
                    tooltip = available
                        ? choice.Description
                        : TextStudioEffectCompatibilityResolver.BuildUnsupportedReason(
                            choice.SupportedDomains)
                };
                row.AddToClassList("ts-composed-layer-picker-item");
                row.SetEnabled(available);

                VisualElement copy = new VisualElement();
                copy.AddToClassList("ts-composed-layer-picker-copy");
                Label name = new Label(choice.Name);
                name.AddToClassList("ts-composed-layer-picker-name");
                copy.Add(name);
                Label description = new Label(available
                    ? choice.Description
                    : row.tooltip);
                description.AddToClassList("ts-muted-text");
                description.AddToClassList("ts-composed-layer-picker-description");
                copy.Add(description);
                row.Add(copy);

                Label metadata = new Label(
                    choice.Category + "\n" +
                    choice.Presentation.PickerMetadata(choice.Source));
                metadata.AddToClassList("ts-composed-layer-picker-metadata");
                row.Add(metadata);
                return row;
            }
        }


        static AnimationCurve BuildCurvePreview(TextStudioEffectCurvePreset preset)
        {
            const int sampleCount = 32;
            Keyframe[] keys = new Keyframe[sampleCount + 1];
            for (int i = 0; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float value = TextStudioEffectCurveUtility.Evaluate(preset, t);
                keys[i] = new Keyframe(t, value);
            }
            return new AnimationCurve(keys);
        }

    }
}
