using System;
using System.Collections;
using System.Collections.Generic;
using ImpossibleRobert.Common.Timing;
using TextStudio.BuiltIns;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace TextStudio
{
    /// <summary>
    /// Central TextStudio hub. Owns the authored markup, runs the full pipeline
    /// (tokenize, resolve, layout, placement, reveal, effects) once per tick, and
    /// drives every attached renderer via <see cref="ITextStudioRenderer"/>.
    ///
    /// Text ownership, layout, and mesh application are delegated to the selected
    /// text backend. TextMeshPro remains the built-in fallback. Editor tick management
    /// is handled by <see cref="TextStudioEditorHooks"/> so this file contains zero
    /// UnityEditor references.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Text Studio/Text Studio Text")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    public sealed partial class TextStudioText : MonoBehaviour,
        ITextStudioContent,
        ITextStudioPlayback,
        ITextStudioVisibility,
        ITextStudioEffectControl,
        ITextStudioBackendHost,
        ITextStudioController,
        ITextStudioWaitLifecycleSink,
        ITypewriterProvider,
        ITextStudioReactionActionHost,
        ITextStudioReactionBodyActionHost,
        ITextStudioReactionBooleanInputProvider,
        ITextStudioReactionNamedAnchorHost,
        ITextStudioReactionUnitTextSource,
        ITextStudioReactionUIParticleHost
    {
        /// <summary>Focused content view for integrations that do not control playback.</summary>
        public ITextStudioContent Content => this;

        /// <summary>Focused reveal and hide playback view.</summary>
        public ITextStudioPlayback Playback => this;

        /// <summary>Focused runtime visibility view.</summary>
        public ITextStudioVisibility Visibility => this;

        /// <summary>Focused renderer-neutral effect control view.</summary>
        public ITextStudioEffectControl Effects => this;

        /// <summary>Advanced text-backend discovery and state view.</summary>
        public ITextStudioBackendHost Backends => this;

        // ── Authored content ────────────────────────────────────────────────
        [Header("Text")]
        [SerializeField, TextArea(3, 10)] string _markup = "";
        [SerializeField] TextStudioAuthoringMode _authoringMode = TextStudioAuthoringMode.Raw;
        [SerializeField] TextStudioBackendWriteHandling _backendWriteHandling =
            TextStudioBackendWriteHandling.Warn;
        [SerializeField, Tooltip("Optional syntax settings for this component. Leave empty to inherit the project syntax profile.")]
        TextStudioMarkupSyntax _markupSyntaxOverride;

        [Header("Default Effects")]
        [Tooltip("Optional renderer-neutral behavior profile for default effects, Typewriter, and reactions.")]
        [SerializeField] TextStudioBehaviorProfile _behaviorProfile;
        [SerializeField] TextStudioDefaultEffectMode _inlineDefaultEffectMode = TextStudioDefaultEffectMode.None;
        [SerializeField, TextArea(1, 3)] string _inlineDefaultEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _inlineDefaultRevealEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _inlineDefaultActiveEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _inlineDefaultHideEffects = string.Empty;

        [Header("Appearance")]
        [SerializeField, Tooltip("Optional persistent 2D appearance stack. Animation effects remain independent and can drive its tint and renderer properties.")]
        TextStudio2DStyleAsset _appearanceStyle;
        [SerializeField] TextStudioAppearanceSource _appearanceSource;
#if UNITY_EDITOR
        [SerializeField, HideInInspector] int _styleSelectionMode;
        [SerializeField, HideInInspector] TextStudio2DStyleAsset _catalogStyleSelection;
        [SerializeField, HideInInspector] TextStudio2DStyleAsset _customStyleSelection;
#endif
        [SerializeField] TextStudio2DStyleSettings _inlineAppearanceSettings;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha threshold for inline image silhouettes. Lower this for translucent artwork. The original image opacity is preserved independently.")]
        float _inlineColorAlphaThreshold = .5f;
        [SerializeField, Tooltip("Optional writing treatment with honest authored-path, outline, coverage, and typewriter fallback levels.")]
        TextStudioWritingStyleAsset _writingStyle;
        [SerializeField, Tooltip("Draw letters within the Typewriter reveal lifecycle.")]
        bool _writingEnabled;
        [SerializeField] bool _eraseOnHide;
        [SerializeField] TextStudioTypewriterTimingMode _typewriterTimingMode;
        [SerializeField] TextStudioEffectDefinitionAsset _typewriterRevealEffect;
        [SerializeField] TextStudioEffectDefinitionAsset _typewriterHideEffect;
        [SerializeField, TextArea(1, 3), HideInInspector] string _typewriterRevealEffectExpression = string.Empty;
        [SerializeField, TextArea(1, 3), HideInInspector] string _typewriterHideEffectExpression = string.Empty;

        [Header("Reveal")]
        [SerializeField] TextStudioTypewriterSource _typewriterSource = TextStudioTypewriterSource.None;
        [Tooltip("Reusable typewriter recipe used when Typewriter Source = Custom.")]
        [SerializeField] TextStudioTypewriterPreset _typewriterPreset;
        [SerializeField] RevealMode _revealMode = RevealMode.Instant;
        [Tooltip("Optional custom timings asset. When set, takes precedence over the " +
                 "Built-In Preset below.")]
        [SerializeField] TypingTimingsBase _timings;
        [Tooltip("Built-in timing preset used when no custom Timings asset is assigned. " +
                 "Pick the pacing you want and Text Studio handles the rest.")]
        [SerializeField] RevealTimingsPreset _builtInTimingsPreset = RevealTimingsPreset.Uniform;
        [Tooltip("When no custom timing asset is assigned, include the built-in cadence's final trailing delay before reveal completion.")]
        [SerializeField] bool _waitAfterFinalReveal = true;
        [SerializeField] float _revealSpeedMultiplier = 1f;
        [SerializeField, Min(0.01f), Tooltip("Seconds each glyph spends in its appearance phase after reveal.")]
        float _revealAppearanceDuration = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("Seconds visible glyphs spend in their disappearance phase before hiding.")]
        float _revealDisappearanceDuration = 0.3f;
        [SerializeField, Tooltip("When enabled, StartReveal repeats automatically after the reveal reaches the end.")]
        bool _loopReveal;
        [SerializeField, Min(0f), Tooltip("Seconds to hold the completed text before a looped reveal restarts.")]
        float _loopRevealDelay = 0.75f;
        [SerializeField] TextStudioSequenceSettings _revealSequence = default;
        [SerializeField] TextStudioSequenceSettings _hideSequence = default;
        [SerializeField] TextStudioMotionPolicy _motionPolicy = TextStudioMotionPolicy.Inherit;
        [SerializeField] TextStudioTypewriterStartMode _typewriterStartMode = TextStudioTypewriterStartMode.Manual;
        [SerializeField, Tooltip("Choose whether replacement text appears immediately, after the current text hides, or while a capable renderer continues the outgoing visual.")]
        TextStudioTextChangeMode _textChangeMode = TextStudioTextChangeMode.Immediate;
        [SerializeField, Tooltip("Keep matching glyphs visually continuous, or animate the complete old and new strings during replacement.")]
        TextStudioTextChangeScope _textChangeScope = TextStudioTextChangeScope.WholeText;
        [SerializeField] bool _resetSpeedOnNewText = true;
        [SerializeField] TextStudioTypewriterSkipRevealMode _skipRevealMode = TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately;
        [SerializeField] bool _triggerRemainingMarkersOnSkip;
        [SerializeField] bool _skipCurrentWaitOnSkip = true;
        [SerializeField] TextStudioTypewriterSkipHideMode _skipHideMode = TextStudioTypewriterSkipHideMode.HideImmediately;

        [Header("Typewriter Preset")]
        [SerializeField] string _typewriterPresetId = "typewriter.classic";

        [Header("Timing")]
        [Tooltip("Clock source and update timing used by reveal playback, effects, actions, and previews.")]
        [SerializeField] FrameTimingSettings _timing = FrameTimingSettings.Default(HostTime.DefaultMaxDeltaTime, true);

        [Header("Preview")]
        [Tooltip("Allow Text Studio action tags to execute during edit-mode preview playback. Disabled by default to avoid edit-time scene side effects.")]
        [SerializeField] bool _runActionsInPreview;
        [SerializeField, Tooltip("Choose whether edit-mode preview shows the normal output, flat TMP, an attached renderer, or both side by side.")]
        TextStudioPreviewStageMode _previewStageMode = TextStudioPreviewStageMode.Automatic;
        [SerializeField, Tooltip("Camera framing used when focusing a renderer-aware preview stage.")]
        TextStudioPreviewView _previewView = TextStudioPreviewView.Perspective;
        [SerializeField, Tooltip("Draft favors responsive iteration; Full restores authored renderer quality.")]
        TextStudioPreviewQuality _previewQuality = TextStudioPreviewQuality.Full;
        [SerializeField, Tooltip("Horizontal and vertical orbit angles used by the Orbit preview view.")]
        Vector2 _previewOrbitAngles = new Vector2(25f, -20f);
        [SerializeField, Min(0f), Tooltip("Local spacing between flat and renderer output in Compare mode.")]
        float _previewCompareSpacing = 4f;

        [SerializeField] PlacementMode _placementMode = PlacementMode.Linear;
        [SerializeField, Tooltip("Stable identifier of the active component-based placement extension.")]
        string _extensionPlacementId = string.Empty;
        [SerializeField] ArcPlacement _arcPlacement = new ArcPlacement();
        [SerializeField] WavePlacement _wavePlacement = new WavePlacement();
        [SerializeField] CirclePlacement _circlePlacement = new CirclePlacement();
        [SerializeField] GridPlacement _gridPlacement = new GridPlacement();
        [SerializeField] CurvePlacement _curvePlacement = new CurvePlacement();
        [NonSerialized] TextStudioCurveProfile _subscribedCurveProfile;

        [SerializeField, Tooltip("Optional moving target used by Target Distance influence fields. Text Studio 3D interactions also use this target when their own Target is empty.")]
        Transform _influenceTarget;

        [Tooltip("Shared glyph orientation override applied after the active placement strategy.")]
        [SerializeField] PlacementOrientation _placementRotation = new PlacementOrientation();

        [Header("Presentation")]
        [SerializeField, Tooltip("Optional addon glyph provider (Terminal FX, Mechanical, etc.).")]
        MonoBehaviour _presentationProvider;

        [SerializeField, Tooltip("Invoked with the current markup whenever SetText is called.")]
        UnityEvent<string> _onSetText = new UnityEvent<string>();
        [SerializeField, Tooltip("Invoked after the complete text has been revealed.")]
        UnityEvent _onShowComplete = new UnityEvent();
        [SerializeField, Tooltip("Invoked after the complete text has disappeared.")]
        UnityEvent _onDisappearComplete = new UnityEvent();

        // ── Pipeline state ──────────────────────────────────────────────────
        readonly TextStudioRuntimeCore _runtime = new TextStudioRuntimeCore();
        // One-based slot in TextStudioHubEvents; zero while the hub receives no project-wide notifications.
        internal int HubEventSlot;
        readonly TextStudioLayout _layout = new TextStudioLayout();
        readonly PlacementEngine _placementEngine = new PlacementEngine();
        TextStudioTypewriterSettings _appliedRuntimeSettings;
        TextStudioDefaultEffectSettings _appliedDefaultEffects;
        bool _hasAppliedRuntimeSettings;
        bool _hasAppliedDefaultEffects;
        float _time;
        float _presentationTimeOrigin;
        float _placementTime;
        double _lastHostTime;
        TextStudioMonoBehaviourActionRunner _actionRunner;
        readonly TextStudioEditModeActionRunner _editModeActionRunner = new TextStudioEditModeActionRunner();
        readonly TextStudioSourceState _sourceState = new TextStudioSourceState();
        string _lastIgnoredBackendWrite = string.Empty;
        bool _startRevealAfterDeferredPipeline;
        bool _hadInfluenceTarget;
        bool _isShuttingDown;
        Vector3 _lastInfluenceTargetLocalPosition;
        Vector3 _lastInfluenceTargetWorldPosition;

        // ── TMP integration ─────────────────────────────────────────────────
        TextStudioTMPBridge _tmpBridge;
        TextStudioTMPBridge TmpBridge => _tmpBridge ??= new TextStudioTMPBridge(this);
        internal TextStudioLayout LayoutForDebug => _layout;
        // TMP layout passes that resumed after the lines an append kept.
        internal int AppendLayoutCountForDebug => _tmpBridge != null ? _tmpBridge.AppendLayoutCountForDebug : 0;
        internal string AppendLayoutRejectionForDebug => _tmpBridge?.AppendLayoutRejectionForDebug;
        /// <summary>Renderer of a text engine that hands its glyph output to Text Studio, when one is attached.</summary>
        internal TextStudio2DExternalAppearanceRenderer ExternalAppearance { get; set; }
        internal string MaterialCompatibilityWarning =>
            ExternalAppearance != null ? ExternalAppearance.CompatibilityWarning :
            _tmpBridge != null ? _tmpBridge.MaterialCompatibilityWarning : string.Empty;
        internal string AppearanceQualityHint =>
            _tmpBridge != null ? _tmpBridge.AppearanceQualityHint : string.Empty;
        internal TextStudio2DHeroCompositorStatistics HeroCompositorStatistics =>
            _tmpBridge != null ? _tmpBridge.HeroCompositorStatistics : default;

        /// <summary>How this text's 2D Appearance rendered in its latest presentation update, for profiling and diagnostics.</summary>
        public TextStudio2DRenderStatistics RenderStatistics =>
            ExternalAppearance != null ? ExternalAppearance.CaptureStatistics() :
            _tmpBridge != null ? _tmpBridge.CaptureRenderStatistics() : default;
        internal long AppearanceSelectionPayloadBytes => _tmpBridge?.AppearanceSelectionPayloadBytes ?? 0;
        internal long AppearanceMeshPayloadBytes => _tmpBridge?.AppearanceMeshPayloadBytes ?? 0;

        internal int OrderedGeometryUploadCount => _tmpBridge?.OrderedGeometryUploadCount ?? 0;
        internal int OrderedDrawCount => _tmpBridge?.OrderedDrawCount ?? 0;
        internal int AppearanceResolvedMatchCount => _tmpBridge?.AppearanceResolvedMatchCount ?? 0;
        internal TextStudioAppearanceSourceStatistics AppearanceSourceStatistics =>
            _tmpBridge != null ? _tmpBridge.AppearanceSourceStatistics : default;

        internal Vector4 GetAppearancePreparedQuality(int renderGlyphIndex) =>
            _tmpBridge != null ? _tmpBridge.GetAppearancePreparedQuality(renderGlyphIndex) : default;

        // ── Text source binding ─────────────────────────────────────────────
        bool _hasExternalTextSource;

        // ── Renderer/layout providers ───────────────────────────────────────
        readonly List<ITextStudioRenderer> _renderers = new List<ITextStudioRenderer>();
        readonly List<ITextStudioPreviewStageProvider> _previewStageProviders =
            new List<ITextStudioPreviewStageProvider>();
        readonly List<IPlacementStrategy> _placementStrategies = new List<IPlacementStrategy>();
        readonly List<ITextStudioExternalTextSource> _externalSources = new List<ITextStudioExternalTextSource>();
        readonly List<ITextStudioExternalTextSource> _registeredExternalSources = new List<ITextStudioExternalTextSource>();
        readonly List<ITextStudioExternalEffectSource> _externalEffectSources =
            new List<ITextStudioExternalEffectSource>();
        readonly List<ITextStudioExternalEffectSource> _registeredExternalEffectSources =
            new List<ITextStudioExternalEffectSource>();
        readonly List<ITextStudioExtensionTagProcessor> _extensionProcessors = new List<ITextStudioExtensionTagProcessor>();
        readonly List<ITextStudioExtensionTagProcessor> _registeredExtensionProcessors = new List<ITextStudioExtensionTagProcessor>();
        readonly List<ITextStudioAction> _localActions = new List<ITextStudioAction>();

        // ── Presentation ────────────────────────────────────────────────────
        PresentationGlyph[] _presentationGlyphs;
        IPresentationGlyphProvider _glyphProvider;
        readonly TextStudioCharacterSubstitutionPresenter _characterSubstitutionPresenter =
            new TextStudioCharacterSubstitutionPresenter();
        bool _presentationTimeDiscontinuous = true;

        // ── Persistent 2D appearance ────────────────────────────────────────
        CompiledTextStudio2DStyle _compiledAppearanceStyle;
        TextStudio2DStyleAsset _compiledAppearanceSource;
        TextStudioAppearanceSource _compiledAppearanceMode;
        ulong _appearanceDependencyRevision;

        // ── Writing ─────────────────────────────────────────────────────────
        CompiledTextStudioWritingStyle _compiledWritingStyle;
        TextStudioWritingStyleAsset _compiledWritingSource;
        ulong _writingDependencyRevision;

        // ── Preview ─────────────────────────────────────────────────────────
        TextStudioPreview _preview;

        // A new preview starts outside runtime ownership, which a sleeping hub's check reads, so creating one unparks it.
        TextStudioPreview CreatePreview()
        {
            Unpark();
            return _preview = new TextStudioPreview(this);
        }

        // ── Events / public API ─────────────────────────────────────────────
        /// <summary>Occurs when a visible glyph crosses its reveal boundary.</summary>
        public event Action<int, char> OnGlyphRevealed;
        /// <summary>Occurs when a visible glyph crosses its disappearance boundary.</summary>
        public event Action<int> OnGlyphDisappeared;
        /// <summary>Occurs when a semantic text unit becomes visible.</summary>
        public event Action<int, string> OnTextUnitRevealed;
        public event Action<string, string[]> OnMarkerReached;
        bool _warnedAboutUnhandledMarker;
        /// <summary>Occurs after compilation changes the set of extension tags.</summary>
        public event Action<TextStudioPlanView> OnExtensionTagsChanged;
        /// <summary>Occurs when reveal reaches an extension tag configured for reveal dispatch.</summary>
        public event Action<TextStudioExtensionTag> OnExtensionTagReached;
        /// <summary>Occurs immediately before a typewriter action suspends playback.</summary>
        public event Action<TextStudioTypewriterWaitInfo> OnTypewriterWaitStarted;
        /// <summary>Occurs when a typewriter wait completes or is skipped.</summary>
        public event Action<TextStudioTypewriterWaitInfo> OnTypewriterWaitFinished;
        public event Action OnTypewriterStarted;
        public event Action OnShowComplete;
        public event Action OnDisappearComplete;
        public UnityEvent<string> OnSetTextEvent => _onSetText;
        public UnityEvent OnShowCompleteEvent => _onShowComplete;
        public UnityEvent OnDisappearCompleteEvent => _onDisappearComplete;

        public string Markup => _markup ?? string.Empty;
        public string Text => _runtime.Text;
        public TextStudioPlanView Plan => new TextStudioPlanView(_runtime.Plan);
        public TextStudio2DStyleAsset AppearanceStyle => AppearanceSource == TextStudioAppearanceSource.Style ? _appearanceStyle : null;
        public CompiledTextStudio2DStyle CompiledAppearanceStyle => _compiledAppearanceStyle;
        public TextStudioWritingStyleAsset WritingStyle => ResolveWritingStyle();
        public CompiledTextStudioWritingStyle CompiledWritingStyle =>
            _compiledWritingStyle;
        public TextStudioPreview Preview => _preview ?? CreatePreview();
        public IReadOnlyList<ITextStudioRenderer> Renderers => _renderers;
        public IReadOnlyList<ITextStudioPreviewStageProvider> PreviewStageProviders => _previewStageProviders;
        public TMP_Text TMPText => TmpBridge.TMPText;
        public bool HasExternalTextSource => _hasExternalTextSource;
        public int ExternalTextSourceCount => _externalSources.Count;
        internal DirtyFlags PendingDirtyFlags => _runtime.DirtyFlags;
        internal bool IsUsingUnanimatedTMPPassThrough =>
            TmpBridge.IsUsingUnanimatedPassThrough;
        internal TextStudioCompiledPlan CompiledPlan => _runtime.Plan;
        internal IReadOnlyList<TextStudioCompatibilityDiagnostic> CompatibilityDiagnostics =>
            _runtime.Pipeline.CompatibilityDiagnostics;
        internal bool LastProcessDetectedTMPEffects =>
            _runtime.Pipeline.LastProcessDetectedTMPEffects;
        internal int LastCompatibilityTranslationCount =>
            _runtime.Pipeline.LastCompatibilityTranslationCount;
        internal bool RequiresContinuousEvaluation =>
            _runtime.RequiresContinuousEvaluation ||
            (ActiveCompiledAppearancePlan != null &&
             ActiveCompiledAppearancePlan.RequiresContinuousRenderingForPolicy(
                 _motionPolicy));
        public IReadOnlyList<ITextStudioExtensionTagProcessor> ExtensionTagProcessors => _extensionProcessors;
        public IReadOnlyList<IPlacementStrategy> PlacementStrategies => _placementStrategies;
        public string ExtensionPlacementId => _extensionPlacementId ?? string.Empty;
        public ITextStudioPlacementExtension ActivePlacementExtension => FindPlacementExtension(_extensionPlacementId);
        public TextStudioPreviewStageMode PreviewStageMode => _previewStageMode;
        public TextStudioPreviewView PreviewView => _previewView;
        public TextStudioPreviewQuality PreviewQuality => _previewQuality;
        public Vector2 PreviewOrbitAngles => _previewOrbitAngles;
        public float PreviewCompareSpacing => _previewCompareSpacing;
        public Transform InfluenceTarget
        {
            get => _influenceTarget;
            set
            {
                if (_influenceTarget == value)
                    return;
                _influenceTarget = value;
                _hadInfluenceTarget = false;
                ScheduleVisualStateUpdate();
            }
        }
        public bool HasInfluenceTarget => _influenceTarget != null;
        public Vector3 InfluenceTargetWorldPosition => _influenceTarget != null
            ? _influenceTarget.position
            : Vector3.zero;
        public TextStudioPreviewStageSettings PreviewStageSettings => new TextStudioPreviewStageSettings(
            _previewStageMode,
            _previewView,
            _previewQuality,
            _previewOrbitAngles,
            _previewCompareSpacing);
        public TextStudioAuthoringMode AuthoringMode => _authoringMode;
        public TextStudioBackendWriteHandling BackendWriteHandling =>
            _backendWriteHandling;
        public TextStudioBehaviorProfile BehaviorProfile => _behaviorProfile;
        public bool UsesBehaviorProfileDefaultEffects =>
            ResolveBehaviorSettings().UsesBehaviorProfileDefaultEffects;
        public bool UsesBehaviorProfileTypewriter =>
            ResolveBehaviorSettings().UsesBehaviorProfileTypewriter;
        /// <summary>
        /// Gets or sets optional syntax settings for this component. Null inherits project settings.
        /// </summary>
        public TextStudioMarkupSyntax MarkupSyntaxOverride
        {
            get => _markupSyntaxOverride;
            set => SetMarkupSyntaxOverride(value);
        }
        /// <summary>
        /// Gets the effective authoring profile after component and project settings are resolved.
        /// </summary>
        public TextStudioAuthoringProfile ResolvedAuthoringProfile =>
            TextStudioMarkupSyntax.ResolveProfile(
                _markupSyntaxOverride != null
                    ? _markupSyntaxOverride
                    : TextStudioProjectSettings.ActiveMarkupSyntax);
        public TextStudioDefaultEffectsSource DefaultEffectsSource =>
            UsesBehaviorProfileDefaultEffects
            ? TextStudioDefaultEffectsSource.BehaviorProfile
            : _inlineDefaultEffectMode == TextStudioDefaultEffectMode.None
                ? TextStudioDefaultEffectsSource.None
                : TextStudioDefaultEffectsSource.Inline;

        string ITextStudioBackendHost.LastIgnoredExternalWrite =>
            _lastIgnoredBackendWrite ?? string.Empty;
        public float RevealSpeedMultiplier => _runtime.RevealSpeedMultiplier;
        public bool WaitAfterFinalReveal
        {
            get => ResolveTypewriterSettings().WaitAfterFinalReveal;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _waitAfterFinalReveal = value;
                SyncRuntimeConfiguration();
            }
        }
        public bool RunActionsInPreview => _runActionsInPreview;
        public TextStudioTypewriterPreset TypewriterPreset => ResolveTypewriterRecipe().CustomPreset;
        public TextStudioEffectDefinitionAsset TypewriterRevealEffect => ResolveTypewriterSettings().RevealEffect;
        public TextStudioEffectDefinitionAsset TypewriterHideEffect => ResolveTypewriterSettings().HideEffect;
        public TextStudioTypewriterSource TypewriterSource
        {
            get => ResolveTypewriterRecipe().Source;
            set
            {
                _typewriterSource = value;
                SyncRuntimeConfiguration();
                ScheduleTextRebuild();
            }
        }

        internal bool IsDrivingTextBackend
        {
            get
            {
                if (!HasPrimaryTextBackendTarget() && !HasAvailableExternalRenderer())
                    return false;
                if (!Application.isPlaying && _preview != null && !_preview.ShowEndState)
                    return true;
                if (!Application.isPlaying && _authoringMode == TextStudioAuthoringMode.Raw)
                    return false;
                if (_hasExternalTextSource)
                    return true;
                if (Application.isPlaying || _authoringMode == TextStudioAuthoringMode.Preview)
                    return true;
                if (HasAvailableExternalRenderer())
                    return true;
                return PlanRequiresDrivenText(_runtime.Plan);
            }
        }

        internal bool IsDrivingTMPText =>
            !UsesCustomTextBackend && IsDrivingTextBackend;

        bool ITextStudioBackendHost.IsDrivingOutput => IsDrivingTextBackend;
        bool ITextStudioBackendHost.IsDrivingBuiltInTextMeshPro => IsDrivingTMPText;

        bool IsDrivingTextStudioOutput =>
            IsDrivingTextBackend || IsRenderingRawExternalOutput;

        bool IsRenderingRawExternalOutput =>
            !Application.isPlaying &&
            _authoringMode == TextStudioAuthoringMode.Raw &&
            Preview.ShowEndState &&
            HasAvailableExternalRenderer();

        public PlacementMode PlacementMode
        {
            get => _placementMode;
            set
            {
                _placementMode = value;
                ApplyPlacementStrategy();
                _runtime.SetDirty(DirtyFlags.Placement);
            }
        }

        public RevealMode RevealMode
        {
            get => _runtime.RevealMode;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _revealMode = value;
                SyncRuntimeConfiguration();
            }
        }

        public RevealTimingsPreset BuiltInTimingsPreset
        {
            get => ResolveTypewriterSettings().BuiltInTimingsPreset;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _builtInTimingsPreset = value;
                SyncRuntimeConfiguration();
            }
        }

        public float ArcStartAngle => _arcPlacement.startAngle;
        public PathPosition ArcPathPosition => _arcPlacement.pathPosition;
        public float CircleStartAngle => _circlePlacement.startAngle;
        public PathPosition CirclePathPosition => _circlePlacement.pathPosition;
        public int GridColumns => _gridPlacement.columns;
        public GridCellSizing GridCellSizing => _gridPlacement.cellSizing;
        public Vector2 GridCellSize => _gridPlacement.cellSize;
        public Vector2 GridGap => _gridPlacement.gap;
        public GridFlow GridFlow => _gridPlacement.flow;
        internal ArcPlacement ArcPlacementSettings => _arcPlacement;
        internal CirclePlacement CirclePlacementSettings => _circlePlacement;
        internal GridPlacement GridPlacementSettings => _gridPlacement;

        // ── ITextStudioController ───────────────────────────────────────────
        public float RevealProgress => _runtime.RevealProgress;

        /// <summary>Optional language-specific word segmentation. Null uses Unicode default word boundaries.</summary>
        public ITextStudioWordBoundaryProvider WordBoundaryProvider
        {
            get => _runtime.WordBoundaryProvider;
            set => _runtime.WordBoundaryProvider = value;
        }
        public int RevealedGlyphCount => _runtime.RevealedGlyphCount;
        public int TotalGlyphCount => _runtime.TotalGlyphCount;
        public int WordCount => _runtime.WordCount;
        public bool HasVisibilityOverrides => _runtime.HasVisibilityOverrides;
        public bool IsRevealing => _runtime.IsRevealing;

        /// <summary>Whether pending text, reveal transitions, actions, or finite character substitutions still need presentation frames. Persistent effects do not keep this busy.</summary>
        public bool IsPresentationBusy => _runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style) ||
                                          _startRevealAfterDeferredPipeline || IsTextChangePending ||
                                          IsRevealing || IsWaitingForAction || HasActiveRevealAppearance ||
                                          _characterSubstitutionPresenter.HasPendingCompletion;
        internal bool HasActiveRevealAppearance => _runtime.HasActiveRevealAppearance;
        public bool IsRevealPaused => _runtime.IsRevealPaused;

        /// <summary>
        /// Whether reveal is paused while an action is still executing.
        /// </summary>
        public bool IsWaitingForAction => _runtime.IsWaitingForAction;
        public bool IsDisappearing => _runtime.IsDisappearing;
        public bool RevealEffectsEnabled
        {
            get => _runtime.RevealEffectsEnabled;
            set => _runtime.RevealEffectsEnabled = value;
        }
        public bool ActiveEffectsEnabled
        {
            get => _runtime.ActiveEffectsEnabled;
            set => _runtime.ActiveEffectsEnabled = value;
        }
        public bool HideEffectsEnabled
        {
            get => _runtime.HideEffectsEnabled;
            set => _runtime.HideEffectsEnabled = value;
        }
        public MonoBehaviour CoroutineHost => this;

        // ── ITypewriterProvider ─────────────────────────────────────────────
        public bool IsShowingText => _runtime.IsShowingText;

        public bool LoopReveal
        {
            get => ResolveTypewriterSettings().LoopReveal;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _loopReveal = value;
                SyncRuntimeConfiguration();
            }
        }

        public float LoopRevealDelay
        {
            get => ResolveTypewriterSettings().LoopRevealDelay;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _loopRevealDelay = Mathf.Max(0f, value);
                SyncRuntimeConfiguration();
            }
        }

        public float RevealAppearanceDuration
        {
            get => _runtime.RevealAppearanceDuration;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _revealAppearanceDuration = Mathf.Max(0.01f, value);
                SyncRuntimeConfiguration();
            }
        }

        public float RevealDisappearanceDuration
        {
            get => _runtime.RevealDisappearanceDuration;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _revealDisappearanceDuration = Mathf.Max(0.01f, value);
                SyncRuntimeConfiguration();
            }
        }

        public string TypewriterPresetId => ResolveTypewriterRecipe().BuiltInPresetId ?? string.Empty;
        public string RevealTransitionStyle => ResolveTypewriterSettings().RevealEffectExpression ?? string.Empty;
        public string HideTransitionStyle => ResolveTypewriterSettings().HideEffectExpression ?? string.Empty;
        public TextStudioSequenceSettings RevealSequence
        {
            get => ResolveTypewriterSettings().RevealSequence;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _revealSequence = value;
                SyncRuntimeConfiguration();
            }
        }

        public TextStudioSequenceSettings HideSequence
        {
            get => ResolveTypewriterSettings().HideSequence;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _hideSequence = value;
                SyncRuntimeConfiguration();
            }
        }

        public TextStudioMotionPolicy MotionPolicy
        {
            get => _motionPolicy;
            set
            {
                if (_motionPolicy == value)
                    return;
                _motionPolicy = value;
                _runtime.MotionPolicy = value;
                HandleMotionPolicyChanged();
            }
        }

        public TextStudioTypewriterStartMode TypewriterStartMode
        {
            get => ResolveTypewriterSettings().StartMode;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _typewriterStartMode = value;
            }
        }

        public TextStudioTextChangeMode TextChangeMode
        {
            get => ResolveTypewriterSettings().TextChangeMode;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _textChangeMode = value;
            }
        }

        public TextStudioTextChangeScope TextChangeScope
        {
            get => ResolveTypewriterSettings().TextChangeScope;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _textChangeScope = value;
            }
        }

        public bool ResetSpeedOnNewText
        {
            get => ResolveTypewriterSettings().ResetSpeedOnNewText;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _resetSpeedOnNewText = value;
            }
        }

        public TextStudioTypewriterSkipRevealMode SkipRevealMode
        {
            get => ResolveTypewriterSettings().SkipRevealMode;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _skipRevealMode = value;
                SyncRuntimeConfiguration();
            }
        }

        public bool TriggerRemainingMarkersOnSkip
        {
            get => ResolveTypewriterSettings().TriggerRemainingMarkersOnSkip;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _triggerRemainingMarkersOnSkip = value;
                SyncRuntimeConfiguration();
            }
        }

        public bool SkipCurrentWaitOnSkip
        {
            get => ResolveTypewriterSettings().SkipCurrentWaitOnSkip;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _skipCurrentWaitOnSkip = value;
                SyncRuntimeConfiguration();
            }
        }

        public TextStudioTypewriterSkipHideMode SkipHideMode
        {
            get => ResolveTypewriterSettings().SkipHideMode;
            set
            {
                if (!EnsureInlineTypewriterSource())
                    return;
                _skipHideMode = value;
                SyncRuntimeConfiguration();
            }
        }

        /// <summary>Applies a reusable custom typewriter preset when local settings are active.</summary>
        public void ApplyTypewriterPreset(TextStudioTypewriterPreset preset)
        {
            if (preset == null)
                return;

            _typewriterPreset = preset;
            _typewriterSource = TextStudioTypewriterSource.Custom;
            SyncRuntimeConfiguration();
            ScheduleTextRebuild();
        }

        /// <summary>Copies a complete typewriter settings snapshot into this component's local configuration.</summary>
        public void ApplyTypewriterSettings(TextStudioTypewriterSettings settings)
        {
            _typewriterSource = TextStudioTypewriterSource.Inline;
            StoreInlineTypewriterSettings(settings);
            SyncRuntimeConfiguration();
            ScheduleTextRebuild();
        }

        void StoreInlineTypewriterSettings(TextStudioTypewriterSettings settings)
        {
            _revealMode = settings.RevealMode;
            _typewriterTimingMode = settings.TimingMode;
            _writingEnabled = settings.WritingEnabled;
            _writingStyle = settings.WritingStyle;
            _eraseOnHide = settings.EraseOnHide;
            _timings = settings.Timings;
            _builtInTimingsPreset = settings.BuiltInTimingsPreset;
            _waitAfterFinalReveal = settings.WaitAfterFinalReveal;
            _revealSpeedMultiplier = Mathf.Max(0.01f, settings.RevealSpeedMultiplier);
            _revealAppearanceDuration = Mathf.Max(0.01f, settings.RevealAppearanceDuration);
            _revealDisappearanceDuration = Mathf.Max(0.01f, settings.RevealDisappearanceDuration);
            _revealSequence = settings.RevealSequence;
            _hideSequence = settings.HideSequence;
            _typewriterStartMode = settings.StartMode;
            _textChangeMode = settings.TextChangeMode;
            _textChangeScope = settings.TextChangeScope;
            _resetSpeedOnNewText = settings.ResetSpeedOnNewText;
            _skipRevealMode = settings.SkipRevealMode;
            _triggerRemainingMarkersOnSkip = settings.TriggerRemainingMarkersOnSkip;
            _skipCurrentWaitOnSkip = settings.SkipCurrentWaitOnSkip;
            _skipHideMode = settings.SkipHideMode;
            _loopReveal = settings.LoopReveal;
            _loopRevealDelay = Mathf.Max(0f, settings.LoopRevealDelay);
            _typewriterRevealEffect = settings.RevealEffect;
            _typewriterHideEffect = settings.HideEffect;
            _typewriterRevealEffectExpression = settings.RevealEffectExpression ?? string.Empty;
            _typewriterHideEffectExpression = settings.HideEffectExpression ?? string.Empty;
        }

        /// <summary>Assigns the shared effect definitions used during Typewriter reveal and hide.</summary>
        public void SetTypewriterTransitionEffects(
            TextStudioEffectDefinitionAsset revealEffect,
            TextStudioEffectDefinitionAsset hideEffect)
        {
            SetTypewriterTransitionEffects(
                revealEffect,
                hideEffect,
                _typewriterRevealEffectExpression,
                _typewriterHideEffectExpression);
        }

        /// <summary>Sets shared Reveal and Hide effect assets or inline effect tag expressions.</summary>
        public void SetTypewriterTransitionEffects(
            TextStudioEffectDefinitionAsset revealEffect,
            TextStudioEffectDefinitionAsset hideEffect,
            string revealEffectExpression,
            string hideEffectExpression)
        {
            if (!EnsureInlineTypewriterSource())
                return;

            _typewriterRevealEffect = revealEffect;
            _typewriterHideEffect = hideEffect;
            _typewriterRevealEffectExpression = revealEffect == null
                ? revealEffectExpression ?? string.Empty
                : string.Empty;
            _typewriterHideEffectExpression = hideEffect == null
                ? hideEffectExpression ?? string.Empty
                : string.Empty;
            SyncRuntimeConfiguration();
        }

        /// <summary>Selects a complete built-in typewriter recipe by stable identifier.</summary>
        public void SetBuiltInTypewriterPreset(string presetId)
        {
            TextStudioBuiltInTypewriterPreset? preset = BuiltInTypewriterPresets.ForId(presetId);
            if (!preset.HasValue)
                return;

            TextStudioBuiltInTypewriterPreset resolvedPreset = preset.Value;
            _typewriterSource = TextStudioTypewriterSource.BuiltIn;
            _typewriterPreset = null;
            _typewriterPresetId = resolvedPreset.Id;
            _typewriterRevealEffect = null;
            _typewriterHideEffect = null;
            _typewriterRevealEffectExpression = string.Empty;
            _typewriterHideEffectExpression = string.Empty;
            SyncRuntimeConfiguration();
            ScheduleTextRebuild();
        }

        /// <summary>Captures current reveal, renderer, and provider diagnostics without changing playback.</summary>
        public TextStudioRevealDebugSnapshot GetRevealDebugSnapshot()
        {
            int activeRendererCount = _renderers.Count;
            if (UsesCustomTextBackend && IsTextBackendVisualOutputEnabled)
                activeRendererCount++;
            else if (TMPText != null && TMPText.enabled)
                activeRendererCount++;

            return _runtime.CreateRevealDebugSnapshot(
                _placementMode,
                activeRendererCount,
                _hasExternalTextSource,
                _externalSources.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event forwarders
        // ─────────────────────────────────────────────────────────────────────

        void HandleRuntimeTextSet(string markup)
        {
            _onSetText?.Invoke(markup);
        }

        void SetCanonicalMarkup(string markup)
        {
            markup ??= string.Empty;
            if (!string.Equals(_markup, markup, StringComparison.Ordinal))
                _lastIgnoredBackendWrite = string.Empty;

            _markup = markup;
            _sourceState.SetMarkup(_markup);
        }

        void HandleRuntimePlanChanged(TextStudioCompiledPlan plan)
        {
            Unpark();
            int count = plan != null ? plan.VisibleGlyphCount : 0;
            _glyphProvider = _presentationProvider as IPresentationGlyphProvider;
            bool requiresPresentationGlyphs =
                _glyphProvider != null ||
                (plan != null && plan.CharacterSubstitutionBindingCount > 0);
            if (!requiresPresentationGlyphs)
            {
                _presentationGlyphs = null;
            }
            else
            {
                if (_presentationGlyphs == null || _presentationGlyphs.Length < count)
                    _presentationGlyphs = new PresentationGlyph[TextStudioBufferCapacity.ForCount(count)];

                for (int i = 0; i < count; i++)
                    _presentationGlyphs[i] = PresentationGlyph.Default(plan.GetPrimaryChar(i), i);
            }

            _characterSubstitutionPresenter.OnTextChanged(plan,
                _runtime.HasPreparedTextChangeContinuity ? _latestCommittedGlyphChanges : default);
            _glyphProvider?.OnTextChanged(count);
            _presentationTimeOrigin = _time;
            _presentationTimeDiscontinuous = true;
        }

        void HandleRuntimeGlyphRevealed(int glyphIndex, char character)
        {
            OnGlyphRevealed?.Invoke(glyphIndex, character);
        }

        void HandleRuntimeGlyphDisappeared(int glyphIndex)
        {
            OnGlyphDisappeared?.Invoke(glyphIndex);
        }

        void HandleRuntimeTextUnitRevealed(int glyphIndex, string textUnit)
        {
            OnTextUnitRevealed?.Invoke(glyphIndex, textUnit);
        }

        void HandleRuntimeMarkerReached(string id, string[] parameters)
        {
            if (Debug.isDebugBuild && OnMarkerReached == null && !TextStudioEvents.HasListeners && !_warnedAboutUnhandledMarker &&
                string.Equals(id, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase))
            {
                _warnedAboutUnhandledMarker = true;
                Debug.LogWarning("[Text Studio] Event '" + TextStudioMarkerEventRouter.GetRouteId(id, parameters) +
                    "' has no marker listeners. Add a Marker Event Router to this text and configure its route, or subscribe to OnMarkerReached in code. This suggestion is shown once per text instance.", this);
            }
            OnMarkerReached?.Invoke(id, parameters);
        }

        void HandleRuntimeExtensionTagsChanged(TextStudioPlanView plan)
        {
            OnExtensionTagsChanged?.Invoke(plan);
        }

        void HandleRuntimeExtensionTagReached(TextStudioExtensionTag tag)
        {
            OnExtensionTagReached?.Invoke(tag);
        }

        void HandleRuntimeWaitStarted(TextStudioTypewriterWaitInfo info)
        {
            OnTypewriterWaitStarted?.Invoke(info);
        }

        void HandleRuntimeWaitFinished(TextStudioTypewriterWaitInfo info)
        {
            OnTypewriterWaitFinished?.Invoke(info);
        }

        void HandleRuntimeTypewriterStarted()
        {
            OnTypewriterStarted?.Invoke();
        }

        internal IEnumerator ExecuteAction(ITextStudioAction action, ActionContext context)
        {
            return _runtime.ExecuteAction(action, context);
        }

        void HandleRuntimeRevealReset(bool preservePresentation)
        {
            Unpark();
            if (!preservePresentation)
            {
                _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
                _presentationTimeOrigin = _time;
                _presentationTimeDiscontinuous = true;
            }
            NotifyRenderersRevealReset();
        }

        internal void HandleProjectCatalogChanged()
        {
            _runtime.SetDirty(
                DirtyFlags.Text |
                DirtyFlags.Style |
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.Placement |
                DirtyFlags.Animation |
                DirtyFlags.Visual);

            if (!Application.isPlaying && isActiveAndEnabled)
            {
                SyncValidatedTextImmediate();
                TextStudioEditorHooks.RequestRepaint();
            }
        }

        internal void HandleMotionPolicyChanged()
        {
            _presentationTimeDiscontinuous = true;
            _runtime.SetDirty(
                DirtyFlags.Animation |
                DirtyFlags.Visual |
                DirtyFlags.AppearanceRenderState);

            if (!Application.isPlaying && isActiveAndEnabled)
                TextStudioEditorHooks.RequestRepaint();
        }

        void NotifyRenderersRevealReset()
        {
            for (int i = 0; i < _renderers.Count; i++)
                _renderers[i]?.OnRevealReset();
        }

        void HandleRuntimeShowComplete()
        {
            Preview.OnRevealComplete();
            _onShowComplete?.Invoke();
            OnShowComplete?.Invoke();
        }

        void HandleRuntimeDisappearComplete()
        {
            Preview.OnDisappearComplete();
            _onDisappearComplete?.Invoke();
            OnDisappearComplete?.Invoke();
            CompletePendingTextChange();
        }
    }
}
