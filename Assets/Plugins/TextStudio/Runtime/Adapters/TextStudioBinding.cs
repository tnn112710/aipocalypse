using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio
{
    /// <summary>Marks the source selector for Text Studio's retained UI Builder authoring control.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TextStudioSourceModeAuthoringAttribute : PropertyAttribute
    {
    }

    /// <summary>Determines where a Text Studio UI Toolkit binding reads its authored markup.</summary>
    public enum TextStudioBindingSourceMode
    {
        [InspectorName("Label Text")]
        LabelText = 0,
        Element = 1,
        Data = 2
    }

    /// <summary>Stable diagnostic categories reported by a Text Studio UI Toolkit binding.</summary>
    public enum TextStudioBindingDiagnosticCode
    {
        None = 0,
        InvalidTargetProperty = 1,
        UnsupportedTarget = 2,
        EditableTarget = 3,
        MissingSourceElementName = 4,
        SourceElementNotFound = 5,
        DuplicateSourceElementName = 6,
        ConversionFailed = 7,
        UnresolvedDataSource = 8,
        BindingCollision = 9
    }

    /// <summary>Non-spamming runtime state surfaced by the binding authoring UI.</summary>
#if UNITY_6000_7_OR_NEWER
    // None is immutable value metadata shared by all bindings.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public readonly partial struct TextStudioBindingDiagnostic : IEquatable<TextStudioBindingDiagnostic>
    {
        public static readonly TextStudioBindingDiagnostic None =
            new TextStudioBindingDiagnostic(TextStudioBindingDiagnosticCode.None, string.Empty);

        public TextStudioBindingDiagnosticCode Code { get; }
        public string Message { get; }
        public bool IsError => Code != TextStudioBindingDiagnosticCode.None;

        /// <summary>Creates a diagnostic with a stable category and customer-facing message.</summary>
        public TextStudioBindingDiagnostic(TextStudioBindingDiagnosticCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool Equals(TextStudioBindingDiagnostic other)
        {
            return Code == other.Code && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioBindingDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Code * 397) ^ StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
        }

        /// <summary>Returns whether two diagnostics have the same category and message.</summary>
        public static bool operator ==(
            TextStudioBindingDiagnostic left,
            TextStudioBindingDiagnostic right)
        {
            return left.Equals(right);
        }

        /// <summary>Returns whether two diagnostics differ in category or message.</summary>
        public static bool operator !=(
            TextStudioBindingDiagnostic left,
            TextStudioBindingDiagnostic right)
        {
            return !left.Equals(right);
        }
    }

    [GeneratePropertyBag]
    internal sealed class TextStudioLabelTextSource : INotifyBindablePropertyChanged
    {
        static readonly BindingId s_ValueProperty = nameof(Value);
        string _value = string.Empty;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        [CreateProperty]
        public string Value
        {
            get => _value;
            set
            {
                value ??= string.Empty;
                if (string.Equals(_value, value, StringComparison.Ordinal))
                    return;

                _value = value;
                propertyChanged?.Invoke(
                    this,
                    new BindablePropertyChangedEventArgs(s_ValueProperty));
            }
        }
    }

    /// <summary>
    /// Binds Text Studio markup, reveal playback, effects, placement, and visibility control to an
    /// existing UI Toolkit <see cref="TextElement"/> without requiring a MonoBehaviour.
    /// </summary>
    [UxmlObject]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    public sealed partial class TextStudioBinding : CustomBinding,
        IDataSourceProvider,
        ITextStudioContent,
        ITextStudioPlayback,
        ITextStudioVisibility,
        ITextStudioEffectControl,
        ITextStudioPreviewHost,
        ITextStudioReactionActionHost,
        ITextStudioReactionUnitTextSource
    {
#if UNITY_6000_7_OR_NEWER
        // This immutable sentinel array is shared by every binding instance.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly string[] s_EmptyRevealPrefixes = { string.Empty };
        static readonly List<WeakReference<TextStudioBinding>> s_EditorBindings =
            new List<WeakReference<TextStudioBinding>>();

        readonly TextStudioRuntimeCore _runtime = new TextStudioRuntimeCore();
        readonly TextStudioEditModeActionRunner _editModeActionRunner =
            new TextStudioEditModeActionRunner();
        readonly TextStudioUITKTargetDriver _targetDriver;
        readonly TextStudioLabelTextSource _labelTextSource = new TextStudioLabelTextSource();
        readonly PlacementEngine _placementEngine = new PlacementEngine();
        readonly ArcPlacement _arcPlacement = new ArcPlacement();
        readonly WavePlacement _wavePlacement = new WavePlacement();
        readonly CirclePlacement _circlePlacement = new CirclePlacement();
        readonly GridPlacement _gridPlacement = new GridPlacement();
        readonly CurvePlacement _curvePlacement = new CurvePlacement();
        readonly PlacementOrientation _placementRotation = new PlacementOrientation();
        TextStudioCurveProfile _subscribedCurveProfile;
        string _curveRange = "..";
        string _curveRangeDiagnostic = string.Empty;
        readonly TextStudioCharacterSubstitutionPresenter _characterSubstitutionPresenter =
            new TextStudioCharacterSubstitutionPresenter();
        readonly StringBuilder _visibleTextBuilder = new StringBuilder(128);
        readonly List<ITextStudioExternalEffectSource> _externalEffectSources =
            new List<ITextStudioExternalEffectSource>();

        TextElement _preparedTarget;
        VisualElement _resolvedElementSource;
        VisualElement _resolvedDataVisualSource;
        TextStudioBindingSourceMode _sourceMode = TextStudioBindingSourceMode.LabelText;
        string _sourceElementName = string.Empty;
        string _sourcePropertyPath = "value";
        object _dataSource;
        Type _dataSourceType;
        PropertyPath _dataSourcePath;
        readonly ConverterGroup _sourceToUiConverters = new ConverterGroup(string.Empty);
        readonly List<ConverterGroup> _appliedSourceConverterGroups = new List<ConverterGroup>();
        readonly SourceValueVisitor _sourceValueVisitor;
        string _sourceToUiConvertersString = string.Empty;
        string _authoredMarkup = string.Empty;
        string _attachmentFallbackMarkup = string.Empty;
        bool _hasPreparedAuthoredMarkup;
        bool _isBindingActive;
        BindingId _activeBindingId;
        TextStudioBindingDiagnostic _diagnostic = TextStudioBindingDiagnostic.None;
        TextStudioBehaviorProfile _behaviorProfile;
        TextStudioMarkupSyntax _markupSyntaxOverride;
        string _displayText = string.Empty;
        string _resolvedText = string.Empty;
        string _visibleText = string.Empty;
        string[] _revealPrefixes = s_EmptyRevealPrefixes;
        PresentationGlyph[] _presentationGlyphs;
        int _presentationGlyphCount;
        Vector3[] _layoutPositions;
        Vector3[] _sourceTextNormalizedPositions;
        TextStudioGlyphColorQuad[] _sourceColors;
        bool[] _sourceColorValidity;
        TextStudioGlyphVisualBounds[] _sourceVisualBounds;
        Vector3[] _placementLayoutPositions;
        TextStudioGlyphVisualBounds[] _placementSourceVisualBounds;
        int _layoutCaptureFingerprint;
        int _lastLayoutCaptureFingerprint;
        bool _hasLayoutCaptureFingerprint;
        PlacementMode _placementMode = PlacementMode.Linear;
        TextStudioUITKTextGeneratorPreference _textGeneratorPreference =
            TextStudioUITKCapabilities.DefaultTextGeneratorPreference;
        TextStudioUITKUpdateMode _updateMode = TextStudioUITKUpdateMode.Automatic;
        TextStudioTypewriterSettings _inlineTypewriterSettings =
            CreateDefaultBindingTypewriterSettings();
        TextStudioTypewriterSource _typewriterSource = TextStudioTypewriterSource.Inline;
        string _typewriterPresetId = BuiltIns.BuiltInTypewriterPresets.Default.Id;
        TextStudioTypewriterPreset _typewriterPreset;
        TextStudioDefaultEffectSettings _inlineDefaultEffects =
            TextStudioDefaultEffectSettings.None;
        TextStudioDefaultEffectSettings _appliedBehaviorDefaultEffects;
        TextStudioTypewriterSettings _appliedBehaviorTypewriterSettings;
        TextStudioBehaviorValueSource _appliedDefaultEffectsSource;
        TextStudioBehaviorValueSource _appliedTypewriterSource;
        bool _hasAppliedBehaviorSettings;
        TextStudioTypewriterStartMode _typewriterStartMode = TextStudioTypewriterStartMode.OnEnable;
        TextStudioTextChangeMode _textChangeMode = TextStudioTextChangeMode.Immediate;
        TextStudioTextChangeScope _textChangeScope = TextStudioTextChangeScope.WholeText;
        TextStudioMotionPolicy _motionPolicy = TextStudioMotionPolicy.Inherit;
        float _configuredRevealSpeedMultiplier = 1f;
        float _presentationTime;
        bool _presentationTimeDiscontinuous = true;
        bool _pendingStartOnAttach;
        bool _resetSpeedOnNewText = true;
        bool _startRevealAfterDeferredPipeline;
        string _requestedMarkup = string.Empty;
        TextStudioTextChangeCoordinator _textChanges;
        bool _enableInEditMode;
        bool _editorRawAuthoringView;
        bool _editorPreviewDriverActive;
        bool _editorDocumentPreviewDriverActive;
        int _placementUpdateDepth;
        bool _placementChangePending;
        TextStudioPreview _preview;
        event Action _onDisappearComplete;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEditorBindingRegistry()
        {
            s_EditorBindings.Clear();
        }

        public TextStudioBinding()
        {
            base.updateTrigger = BindingUpdateTrigger.OnSourceChanged;
            _sourceValueVisitor = new SourceValueVisitor(this);
            _targetDriver = new TextStudioUITKTargetDriver(this, ScheduledAutomaticUpdate);
            _runtime.SetActionRunner(Application.isPlaying ? TextStudioLazyRuntimeActionRunner.Shared : _editModeActionRunner);
            _runtime.PlanChanged += HandleRuntimePlanChanged;
            _runtime.ShowComplete += HandleRuntimeShowComplete;
            _runtime.DisappearComplete += HandleRuntimeDisappearComplete;
        }

        static TextStudioTypewriterSettings CreateDefaultBindingTypewriterSettings()
        {
            TextStudioTypewriterSettings settings =
                TextStudioTypewriterRecipe.CreateNoTypewriterSettings();
            settings.StartMode = TextStudioTypewriterStartMode.OnEnable;
            return settings;
        }

        TextElement _element => _targetDriver.Element;
        ITextStudioUITKRenderStrategy _strategy => _targetDriver.Strategy;
        bool _isPanelAttached => _targetDriver.IsPanelAttached;

        /// <summary>The only supported target binding mode.</summary>
        public BindingMode bindingMode
        {
            get => BindingMode.ToTarget;
            set => EnforceBindingPolicy();
        }

        /// <summary>The only supported source update trigger.</summary>
        public new BindingUpdateTrigger updateTrigger
        {
            get => BindingUpdateTrigger.OnSourceChanged;
            set => EnforceBindingPolicy();
        }

        /// <summary>Optional local source used by Data source mode.</summary>
        [CreateProperty]
        public object dataSource
        {
            get => _dataSource;
            set
            {
                if (ReferenceEquals(_dataSource, value))
                    return;
                _dataSource = value;
                MarkDirty();
            }
        }

        /// <summary>Optional design-time type hint for an inherited Data source.</summary>
        public Type dataSourceType
        {
            get => _dataSourceType;
            set => _dataSourceType = value;
        }

        /// <summary>Path from the effective Data source to the markup value.</summary>
        [CreateProperty]
        public PropertyPath dataSourcePath
        {
            get => _dataSourcePath;
            set
            {
                _dataSourcePath = value;
                MarkDirty();
            }
        }

        [UxmlAttribute("data-source")]
        internal UnityEngine.Object dataSourceAsset
        {
            get => _dataSource as UnityEngine.Object;
            set => dataSource = value;
        }

        [UxmlAttribute("data-source-type")]
        internal string dataSourceTypeString
        {
            get => _dataSourceType?.AssemblyQualifiedName ?? string.Empty;
            set => _dataSourceType = string.IsNullOrWhiteSpace(value) ? null : Type.GetType(value, false);
        }

        [UxmlAttribute("data-source-path")]
        internal string dataSourcePathString
        {
            get => _dataSourcePath.ToString();
            set => dataSourcePath = new PropertyPath(value ?? string.Empty);
        }

        /// <summary>Converters applied before global source-to-string converters.</summary>
        [CreateProperty(ReadOnly = true)]
        public ConverterGroup sourceToUiConverters => _sourceToUiConverters;

        [UxmlAttribute("source-to-ui-converters")]
        internal string sourceToUiConvertersString
        {
            get => _sourceToUiConvertersString;
            set
            {
                _sourceToUiConvertersString = value ?? string.Empty;
                _appliedSourceConverterGroups.Clear();
                string[] ids = _sourceToUiConvertersString.Split(',');
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i].Trim();
                    if (!string.IsNullOrEmpty(id) &&
                        ConverterGroups.TryGetConverterGroup(id, out ConverterGroup group))
                    {
                        _appliedSourceConverterGroups.Add(group);
                    }
                }
                MarkDirty();
            }
        }

        /// <summary>Adds a source-to-string converter group for this binding.</summary>
        public void ApplyConverterGroupToUI(ConverterGroup group)
        {
            if (group == null || _appliedSourceConverterGroups.Contains(group))
                return;
            _appliedSourceConverterGroups.Add(group);
            MarkDirty();
        }

        /// <summary>Selects Label text, a named element, or native inherited data as the markup source.</summary>
        [UxmlAttribute("source-mode"), TextStudioSourceModeAuthoring]
        public TextStudioBindingSourceMode SourceMode
        {
            get => _sourceMode;
            set
            {
                if (_sourceMode == value)
                    return;

                _sourceMode = value;
                ConfigureEffectiveSource(captureLabelText: value == TextStudioBindingSourceMode.LabelText);
            }
        }

        /// <summary>The unique same-panel element name used by Element source mode.</summary>
        [UxmlAttribute("source-element")]
        public string SourceElementName
        {
            get => _sourceElementName;
            set
            {
                value ??= string.Empty;
                if (string.Equals(_sourceElementName, value, StringComparison.Ordinal))
                    return;

                _sourceElementName = value;
                if (_sourceMode == TextStudioBindingSourceMode.Element)
                    ConfigureEffectiveSource(captureLabelText: false);
            }
        }

        /// <summary>The property path read from the named source element. Defaults to value.</summary>
        [UxmlAttribute("source-property-path")]
        public string SourcePropertyPath
        {
            get => _sourcePropertyPath;
            set
            {
                value = string.IsNullOrWhiteSpace(value) ? "value" : value.Trim();
                if (string.Equals(_sourcePropertyPath, value, StringComparison.Ordinal))
                    return;

                _sourcePropertyPath = value;
                if (_sourceMode == TextStudioBindingSourceMode.Element)
                    MarkDirty();
            }
        }

        /// <summary>The latest canonical markup captured from Label text in direct mode.</summary>
        public string AuthoredMarkup => _authoredMarkup;

        /// <summary>The latest actionable binding diagnostic.</summary>
        public TextStudioBindingDiagnostic Diagnostic => _diagnostic;

        /// <summary>Raised only when the diagnostic code or message changes.</summary>
        public event Action<TextStudioBindingDiagnostic> DiagnosticChanged;

        object IDataSourceProvider.dataSource
        {
            get
            {
                switch (_sourceMode)
                {
                    case TextStudioBindingSourceMode.LabelText:
                        return _labelTextSource;
                    case TextStudioBindingSourceMode.Element:
                        return _resolvedElementSource;
                    case TextStudioBindingSourceMode.Data:
                    default:
                        return _dataSource;
                }
            }
        }

        PropertyPath IDataSourceProvider.dataSourcePath
        {
            get
            {
                switch (_sourceMode)
                {
                    case TextStudioBindingSourceMode.LabelText:
                        return new PropertyPath(nameof(TextStudioLabelTextSource.Value));
                    case TextStudioBindingSourceMode.Element:
                        return new PropertyPath(_sourcePropertyPath);
                    case TextStudioBindingSourceMode.Data:
                    default:
                        return _dataSourcePath;
                }
            }
        }

        public TextElement Element => _element;
        public TextStudioUITKFeatureSet FeatureSet => _strategy.FeatureSet;
        public TextStudioPlanView Plan => new TextStudioPlanView(_runtime.Plan);
        public TextStudioPreview Preview => _preview ??= new TextStudioPreview(this);

        /// <summary>Focused content view for integrations that do not control playback.</summary>
        public ITextStudioContent Content => this;

        /// <summary>Focused reveal and hide playback view.</summary>
        public ITextStudioPlayback Playback => this;

        /// <summary>Focused runtime visibility view.</summary>
        public ITextStudioVisibility Visibility => this;

        /// <summary>Focused renderer-neutral effect control view.</summary>
        public ITextStudioEffectControl Effects => this;

        /// <summary>
        /// Zero-allocation transient view over the currently evaluated glyph states.
        /// Do not retain the view after the next binding update.
        /// </summary>
        public TextStudioReadOnlyBuffer<EvaluatedGlyphState> EvaluatedGlyphStates =>
            new TextStudioReadOnlyBuffer<EvaluatedGlyphState>(_runtime.States, _runtime.StateCount);

        internal EvaluatedGlyphState[] UnsafeEvaluatedGlyphStates => _runtime.States;
        public int StateCount => _runtime.StateCount;
        public string Markup => _requestedMarkup;
        public string Text => _runtime.Text;
        public string DisplayText => _displayText;
        public string ResolvedText => _resolvedText;
        public float RevealProgress => _runtime.RevealProgress;

        /// <summary>Optional language-specific word segmentation. Null uses Unicode default word boundaries.</summary>
        public ITextStudioWordBoundaryProvider WordBoundaryProvider
        {
            get => _runtime.WordBoundaryProvider;
            set
            {
                _runtime.WordBoundaryProvider = value;
                WakeAutomaticDriver();
            }
        }
        public int RevealedCount => _runtime.RevealedGlyphCount;
        public int RevealedGlyphCount => _runtime.RevealedGlyphCount;
        public int TotalGlyphCount => _runtime.TotalGlyphCount;
        public int WordCount => _runtime.WordCount;
        public bool HasVisibilityOverrides => _runtime.HasVisibilityOverrides;
        public bool IsRevealing => _runtime.IsRevealing;
        public bool IsRevealPaused => _runtime.IsRevealPaused;

        /// <summary>
        /// Whether reveal is paused while an action is still executing.
        /// </summary>
        public bool IsWaitingForAction => _runtime.IsWaitingForAction;
        public bool IsDisappearing => _runtime.IsDisappearing;
        public bool IsFullParity => FeatureSet == TextStudioUITKFeatureSet.FullParity;
        [UxmlAttribute("placement-mode")]
        public PlacementMode PlacementMode
        {
            get => _placementMode;
            set
            {
                if (_placementMode == value)
                    return;
                _placementMode = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("curve-profile")]
        public TextStudioCurveProfile CurveProfile
        {
            get => _curvePlacement.Curve.Profile;
            set
            {
                if (_curvePlacement.Curve.Profile == value) return;
                if (value != null) _curvePlacement.Curve.Link(value);
                else _curvePlacement.Curve.MakeLocal();
                SyncCurveProfileSubscription();
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("curve-height")]
        public float CurveHeight
        {
            get => _curvePlacement.Height;
            set
            {
                if (Mathf.Approximately(_curvePlacement.Height, value)) return;
                TextStudioApplicationSelector selector = _curvePlacement.Selection;
                _curvePlacement.ConfigureSettings(value, in selector, _curvePlacement.Mapping);
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("curve-mapping")]
        public TextStudioCurveMapping CurveMapping
        {
            get => _curvePlacement.Mapping;
            set
            {
                if (_curvePlacement.Mapping == value) return;
                TextStudioApplicationSelector selector = _curvePlacement.Selection;
                _curvePlacement.ConfigureSettings(_curvePlacement.Height, in selector, value);
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("curve-range")]
        public string CurveRange
        {
            get => _curveRange;
            set
            {
                _curveRange = value ?? string.Empty;
                TextStudioApplicationSelector selector;
                if (TextStudioTextRange.TryParse(_curveRange, out TextStudioTextRange range, out _curveRangeDiagnostic))
                    selector = TextStudioApplicationSelector.Create(new[] { TextStudioApplicationSelectorNode.Range(range) }, 0);
                else
                    selector = TextStudioApplicationSelector.None;
                _curvePlacement.ConfigureSettings(_curvePlacement.Height, in selector, _curvePlacement.Mapping);
                ApplyPlacementChange();
            }
        }

        public string CurveRangeDiagnostic => _curveRangeDiagnostic;

        [UxmlAttribute("arc-radius")]
        public float ArcRadius
        {
            get => _arcPlacement.radius;
            set
            {
                if (Mathf.Approximately(_arcPlacement.radius, value))
                    return;
                _arcPlacement.radius = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-auto-fit-radius")]
        public bool ArcAutoFitRadius
        {
            get => _arcPlacement.autoFitRadius;
            set
            {
                if (_arcPlacement.autoFitRadius == value)
                    return;
                _arcPlacement.autoFitRadius = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-auto-fit-padding")]
        public float ArcAutoFitPadding
        {
            get => _arcPlacement.autoFitPadding;
            set
            {
                float clamped = Mathf.Clamp(value, 1f, 2f);
                if (Mathf.Approximately(_arcPlacement.autoFitPadding, clamped))
                    return;
                _arcPlacement.autoFitPadding = clamped;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-angle")]
        public float ArcAngle
        {
            get => _arcPlacement.arcAngle;
            set
            {
                float clamped = Mathf.Clamp(value, 1f, 360f);
                if (Mathf.Approximately(_arcPlacement.arcAngle, clamped))
                    return;
                _arcPlacement.arcAngle = clamped;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-start-angle")]
        public float ArcStartAngle
        {
            get => _arcPlacement.startAngle;
            set
            {
                if (Mathf.Approximately(_arcPlacement.startAngle, value))
                    return;
                _arcPlacement.startAngle = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-path-position")]
        public PathPosition ArcPathPosition
        {
            get => _arcPlacement.pathPosition;
            set
            {
                if (_arcPlacement.pathPosition == value)
                    return;
                _arcPlacement.pathPosition = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-spacing")]
        public PathSpacing ArcSpacing
        {
            get => _arcPlacement.spacing;
            set
            {
                if (_arcPlacement.spacing == value)
                    return;
                _arcPlacement.spacing = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-fill")]
        public float ArcFill
        {
            get => _arcPlacement.fill;
            set
            {
                float clamped = Mathf.Clamp(value, 0.1f, 1f);
                if (Mathf.Approximately(_arcPlacement.fill, clamped))
                    return;
                _arcPlacement.fill = clamped;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-reverse")]
        public bool ArcReverse
        {
            get => _arcPlacement.reverse;
            set
            {
                if (_arcPlacement.reverse == value)
                    return;
                _arcPlacement.reverse = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("arc-radius-change")]
        public float ArcRadiusChange
        {
            get => _arcPlacement.radiusChange;
            set
            {
                if (Mathf.Approximately(_arcPlacement.radiusChange, value))
                    return;
                _arcPlacement.radiusChange = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-amplitude")]
        public float WaveAmplitude
        {
            get => _wavePlacement.amplitude;
            set
            {
                if (Mathf.Approximately(_wavePlacement.amplitude, value))
                    return;
                _wavePlacement.amplitude = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-depth-amplitude")]
        public float WaveDepthAmplitude
        {
            get => _wavePlacement.depthAmplitude;
            set
            {
                if (Mathf.Approximately(_wavePlacement.depthAmplitude, value)) return;
                _wavePlacement.depthAmplitude = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-spin-speed")]
        public float WaveSpinSpeed
        {
            get => _wavePlacement.spinSpeed;
            set
            {
                if (Mathf.Approximately(_wavePlacement.spinSpeed, value)) return;
                _wavePlacement.spinSpeed = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-spin-stagger")]
        public float WaveSpinStagger
        {
            get => _wavePlacement.spinStagger;
            set
            {
                if (Mathf.Approximately(_wavePlacement.spinStagger, value)) return;
                _wavePlacement.spinStagger = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-frequency")]
        public float WaveFrequency
        {
            get => _wavePlacement.frequency;
            set
            {
                if (Mathf.Approximately(_wavePlacement.frequency, value))
                    return;
                _wavePlacement.frequency = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-speed")]
        public float WaveSpeed
        {
            get => _wavePlacement.speed;
            set
            {
                if (Mathf.Approximately(_wavePlacement.speed, value))
                    return;
                _wavePlacement.speed = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("wave-phase")]
        public float WavePhase
        {
            get => _wavePlacement.phase;
            set
            {
                if (Mathf.Approximately(_wavePlacement.phase, value))
                    return;
                _wavePlacement.phase = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-radius")]
        public float CircleRadius
        {
            get => _circlePlacement.radius;
            set
            {
                if (Mathf.Approximately(_circlePlacement.radius, value))
                    return;
                _circlePlacement.radius = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-auto-fit-radius")]
        public bool CircleAutoFitRadius
        {
            get => _circlePlacement.autoFitRadius;
            set
            {
                if (_circlePlacement.autoFitRadius == value)
                    return;
                _circlePlacement.autoFitRadius = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-auto-fit-padding")]
        public float CircleAutoFitPadding
        {
            get => _circlePlacement.autoFitPadding;
            set
            {
                float clamped = Mathf.Clamp(value, 1f, 2f);
                if (Mathf.Approximately(_circlePlacement.autoFitPadding, clamped))
                    return;
                _circlePlacement.autoFitPadding = clamped;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-start-angle")]
        public float CircleStartAngle
        {
            get => _circlePlacement.startAngle;
            set
            {
                if (Mathf.Approximately(_circlePlacement.startAngle, value))
                    return;
                _circlePlacement.startAngle = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-path-position")]
        public PathPosition CirclePathPosition
        {
            get => _circlePlacement.pathPosition;
            set
            {
                if (_circlePlacement.pathPosition == value)
                    return;
                _circlePlacement.pathPosition = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-spacing")]
        public PathSpacing CircleSpacing
        {
            get => _circlePlacement.spacing;
            set
            {
                if (_circlePlacement.spacing == value)
                    return;
                _circlePlacement.spacing = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-reverse")]
        public bool CircleReverse
        {
            get => _circlePlacement.reverse;
            set
            {
                if (_circlePlacement.reverse == value)
                    return;
                _circlePlacement.reverse = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("circle-radius-change")]
        public float CircleRadiusChange
        {
            get => _circlePlacement.radiusChange;
            set
            {
                if (Mathf.Approximately(_circlePlacement.radiusChange, value))
                    return;
                _circlePlacement.radiusChange = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-columns")]
        public int GridColumns
        {
            get => _gridPlacement.columns;
            set
            {
                int clamped = Mathf.Max(1, value);
                if (_gridPlacement.columns == clamped)
                    return;
                _gridPlacement.columns = clamped;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-cell-sizing")]
        public GridCellSizing GridCellSizing
        {
            get => _gridPlacement.cellSizing;
            set
            {
                if (_gridPlacement.cellSizing == value)
                    return;
                _gridPlacement.cellSizing = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-cell-size")]
        public Vector2 GridCellSize
        {
            get => _gridPlacement.cellSize;
            set
            {
                if (_gridPlacement.cellSize == value)
                    return;
                _gridPlacement.cellSize = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-gap")]
        public Vector2 GridGap
        {
            get => _gridPlacement.gap;
            set
            {
                if (_gridPlacement.gap == value)
                    return;
                _gridPlacement.gap = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-flow")]
        public GridFlow GridFlow
        {
            get => _gridPlacement.flow;
            set
            {
                if (_gridPlacement.flow == value)
                    return;
                _gridPlacement.flow = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-reverse")]
        public bool GridReverse
        {
            get => _gridPlacement.reverse;
            set
            {
                if (_gridPlacement.reverse == value)
                    return;
                _gridPlacement.reverse = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("grid-respect-source-lines")]
        public bool GridRespectSourceLines
        {
            get => _gridPlacement.respectSourceLines;
            set
            {
                if (_gridPlacement.respectSourceLines == value)
                    return;
                _gridPlacement.respectSourceLines = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("placement-orientation")]
        public TextStudioPlacementOrientation PlacementOrientation
        {
            get => _placementRotation.orientation;
            set
            {
                if (_placementRotation.orientation == value)
                    return;
                _placementRotation.orientation = value;
                ApplyPlacementChange();
            }
        }

        [UxmlAttribute("placement-fine-rotation")]
        public Vector3 PlacementFineRotation
        {
            get => _placementRotation.fineRotation;
            set
            {
                if (_placementRotation.fineRotation == value)
                    return;
                _placementRotation.fineRotation = value;
                ApplyPlacementChange();
            }
        }
        /// <summary>
        /// Gets or applies the renderer-neutral behavior profile for this binding.
        /// Assigning null restores the binding's retained local behavior.
        /// </summary>
        [UxmlAttribute("behavior-profile")]
        public TextStudioBehaviorProfile BehaviorProfile
        {
            get => _behaviorProfile;
            set => ApplyBehaviorProfile(value);
        }
        public bool UsesBehaviorProfileTypewriter =>
            _behaviorProfile != null &&
            _behaviorProfile.Provides(TextStudioBehaviorDomain.Typewriter);

        [UxmlAttribute("typewriter-source")]
        public TextStudioTypewriterSource TypewriterSource
        {
            get => _typewriterSource;
            set
            {
                if (_typewriterSource == value)
                    return;

                _typewriterSource = value;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("typewriter-built-in-preset")]
        public string TypewriterPresetId
        {
            get => _typewriterPresetId ?? string.Empty;
            set
            {
                value = string.IsNullOrWhiteSpace(value)
                    ? BuiltIns.BuiltInTypewriterPresets.Default.Id
                    : value.Trim();
                if (string.Equals(_typewriterPresetId, value, StringComparison.Ordinal))
                    return;

                _typewriterPresetId = value;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("typewriter-preset")]
        public TextStudioTypewriterPreset TypewriterPreset
        {
            get => _typewriterPreset;
            set
            {
                if (_typewriterPreset == value)
                    return;

                _typewriterPreset = value;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("default-effects-mode")]
        public TextStudioDefaultEffectMode DefaultEffectsMode
        {
            get => _inlineDefaultEffects.Mode;
            set
            {
                _inlineDefaultEffects.Mode = value;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("default-effects")]
        public string DefaultEffects
        {
            get => _inlineDefaultEffects.AllPhaseEffects ?? string.Empty;
            set
            {
                _inlineDefaultEffects.AllPhaseEffects = value ?? string.Empty;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("default-reveal-effects")]
        public string DefaultRevealEffects
        {
            get => _inlineDefaultEffects.RevealEffects ?? string.Empty;
            set
            {
                _inlineDefaultEffects.RevealEffects = value ?? string.Empty;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("default-active-effects")]
        public string DefaultActiveEffects
        {
            get => _inlineDefaultEffects.ActiveEffects ?? string.Empty;
            set
            {
                _inlineDefaultEffects.ActiveEffects = value ?? string.Empty;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("default-hide-effects")]
        public string DefaultHideEffects
        {
            get => _inlineDefaultEffects.HideEffects ?? string.Empty;
            set
            {
                _inlineDefaultEffects.HideEffects = value ?? string.Empty;
                ApplyResolvedBehaviorSettings();
            }
        }

        [UxmlAttribute("text-change-mode")]
        public TextStudioTextChangeMode TextChangeMode
        {
            get => _textChangeMode;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.TextChangeMode = value;
                ApplyInlineTypewriterSettings();
            }
        }
        [UxmlAttribute("text-change-scope")]
        public TextStudioTextChangeScope TextChangeScope
        {
            get => _textChangeScope;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.TextChangeScope = value;
                ApplyInlineTypewriterSettings();
            }
        }
        /// <summary>
        /// Optional syntax settings for this binding. Null inherits the active project syntax.
        /// </summary>
        [UxmlAttribute("markup-syntax")]
        public TextStudioMarkupSyntax MarkupSyntaxOverride
        {
            get => _markupSyntaxOverride;
            set
            {
                if (_markupSyntaxOverride == value)
                    return;

                _markupSyntaxOverride = value;
                _runtime.Pipeline.Invalidate();
                _runtime.SetDirty(DirtyFlags.Text | DirtyFlags.Style);
                if (_runtime.Plan != null)
                {
                    bool changed = ProcessPipeline();
                    bool evaluated = EvaluateRenderedStateIfNeeded(0f);
                    ApplyStrategyText(changed || evaluated);
                }
                WakeAutomaticDriver();
            }
        }
        /// <summary>
        /// Gets the effective authoring profile after binding and project settings are resolved.
        /// </summary>
        public TextStudioAuthoringProfile ResolvedAuthoringProfile =>
            TextStudioMarkupSyntax.ResolveProfile(
                _markupSyntaxOverride != null
                    ? _markupSyntaxOverride
                    : TextStudioProjectSettings.ActiveMarkupSyntax);
        /// <summary>
        /// Gets or sets whether the binding advances from its panel scheduler or from host calls.
        /// </summary>
        [UxmlAttribute("update-mode")]
        public TextStudioUITKUpdateMode UpdateMode
        {
            get => _updateMode;
            set
            {
                if (_updateMode == value)
                    return;

                _updateMode = value;
                if (_updateMode == TextStudioUITKUpdateMode.Automatic)
                    WakeAutomaticDriver();
                else
                    PauseAutomaticDriver();
            }
        }
        /// <summary>
        /// Gets or sets whether a locally configured binding starts its reveal when attached.
        /// A behavior profile's typewriter start mode takes precedence when it owns Typewriter.
        /// </summary>
        [UxmlAttribute("play-on-attach")]
        public bool PlayOnAttach
        {
            get => _typewriterStartMode == TextStudioTypewriterStartMode.OnEnable ||
                   _typewriterStartMode == TextStudioTypewriterStartMode.OnEnableOrTextChanged;
            set
            {
                if (UsesBehaviorProfileTypewriter)
                    return;

                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.StartMode = value
                    ? TextStudioTypewriterStartMode.OnEnable
                    : TextStudioTypewriterStartMode.Manual;
                ApplyInlineTypewriterSettings();
                _pendingStartOnAttach = value && _isPanelAttached;
                if (_pendingStartOnAttach)
                    TryStartPendingAttachPlayback();
            }
        }
        internal bool HasPlacementTransform => _placementMode != PlacementMode.Linear || _placementRotation.HasEffect || _textChangeLayoutDuration > 0f;
        internal bool NeedsSourceLayoutContext =>
            _runtime.Plan != null &&
            (_runtime.Reveal.HasSpatialSequence ||
             _runtime.Plan.SourceContextRequirements != TextStudioSourceContextRequirements.None ||
             _runtime.ReactionsRequireAnchor);
        internal bool RequiresGlyphPostProcess
        {
            get
            {
                if (_editorRawAuthoringView)
                    return false;

                TextStudioCompiledPlan plan = _runtime.Plan;
                if (plan == null)
                    return false;

                return plan.EffectBindingCount > 0 ||
                       plan.CharacterSubstitutionBindingCount > 0 ||
                       HasActiveExternalEffectSources() ||
                       _runtime.RequiresContinuousEvaluation ||
                       _runtime.HasExternalEffectState ||
                       _runtime.HasVisibilityOverrides ||
                       _runtime.ReactionsRequireAnchor ||
                       _runtime.RevealMode != RevealMode.Instant ||
                       _runtime.IsDisappearing ||
                       _runtime.RevealedGlyphCount < _runtime.TotalGlyphCount ||
                       HasPlacementTransform ||
                       plan.SourceContextRequirements != TextStudioSourceContextRequirements.None;
            }
        }
        internal GlyphPlacement[] Placements => _placementEngine.Placements;
        internal int PlacementCount => _placementEngine.Count;
        internal string VisibleText => _visibleText;
        internal int RevealPrefixCacheCount => _revealPrefixes != null ? _revealPrefixes.Length : 0;
        internal bool HasPresentationGlyphStorage =>
            _presentationGlyphs != null && _presentationGlyphs.Length > 0;
        internal bool AutomaticDriverActive => _targetDriver.AutomaticDriverActive;
        internal int AutomaticUpdateCount => _targetDriver.AutomaticUpdateCount;
        internal bool EditorRawAuthoringView => _editorRawAuthoringView;
        internal bool EditorPreviewDriverActive => _editorPreviewDriverActive;
        internal bool EditorDocumentPreviewDriverActive => _editorDocumentPreviewDriverActive;
        internal bool IsPanelAttached => _isPanelAttached;
        internal int RepaintRequestCount => _strategy.RepaintRequestCount;

        internal static bool TryGetEditorBinding(
            string targetName,
            out TextStudioBinding binding)
        {
            binding = null;
            for (int i = s_EditorBindings.Count - 1; i >= 0; i--)
            {
                if (!s_EditorBindings[i].TryGetTarget(out TextStudioBinding candidate) ||
                    candidate == null ||
                    candidate._element == null ||
                    candidate._element.panel == null)
                {
                    s_EditorBindings.RemoveAt(i);
                    continue;
                }

                if (candidate._element.panel.contextType != ContextType.Editor ||
                    !string.Equals(candidate._element.name, targetName, StringComparison.Ordinal))
                {
                    continue;
                }

                binding = candidate;
                return true;
            }

            return false;
        }

        void RegisterEditorBinding()
        {
            if (_element?.panel?.contextType != ContextType.Editor)
                return;

            for (int i = s_EditorBindings.Count - 1; i >= 0; i--)
            {
                if (!s_EditorBindings[i].TryGetTarget(out TextStudioBinding candidate) ||
                    candidate == null)
                {
                    s_EditorBindings.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(candidate, this))
                    return;
            }

            s_EditorBindings.Add(new WeakReference<TextStudioBinding>(this));
        }

        void UnregisterEditorBinding()
        {
            for (int i = s_EditorBindings.Count - 1; i >= 0; i--)
            {
                if (!s_EditorBindings[i].TryGetTarget(out TextStudioBinding candidate) ||
                    candidate == null ||
                    ReferenceEquals(candidate, this))
                {
                    s_EditorBindings.RemoveAt(i);
                }
            }
        }

        [UxmlAttribute("text-generator-preference")]
        public TextStudioUITKTextGeneratorPreference TextGeneratorPreference
        {
            get => _textGeneratorPreference;
            set
            {
                if (_textGeneratorPreference == value)
                    return;

                _textGeneratorPreference = value;
                ApplyTextGeneratorPreference();
                ApplyStrategyText(true);
                WakeAutomaticDriver();
            }
        }

        /// <summary>Occurs after markup is replaced and accepted by the runtime.</summary>
        public event Action<string> OnTextSet
        {
            add => _runtime.TextSet += value;
            remove => _runtime.TextSet -= value;
        }

        public event Action OnTypewriterStarted
        {
            add => _runtime.TypewriterStarted += value;
            remove => _runtime.TypewriterStarted -= value;
        }

        /// <summary>Occurs when a visible glyph crosses its reveal boundary.</summary>
        public event Action<int, char> OnGlyphRevealed
        {
            add => _runtime.GlyphRevealed += value;
            remove => _runtime.GlyphRevealed -= value;
        }

        /// <summary>Occurs when a visible glyph crosses its disappearance boundary.</summary>
        public event Action<int> OnGlyphDisappeared
        {
            add => _runtime.GlyphDisappeared += value;
            remove => _runtime.GlyphDisappeared -= value;
        }

        /// <summary>Occurs when a semantic text unit becomes visible.</summary>
        public event Action<int, string> OnTextUnitRevealed
        {
            add => _runtime.TextUnitRevealed += value;
            remove => _runtime.TextUnitRevealed -= value;
        }

        /// <summary>Occurs when reveal reaches an authored marker.</summary>
        public event Action<string, string[]> OnMarkerReached
        {
            add => _runtime.MarkerReached += value;
            remove => _runtime.MarkerReached -= value;
        }

        /// <summary>Occurs after compilation changes the set of extension tags.</summary>
        public event Action<TextStudioPlanView> OnExtensionTagsChanged
        {
            add => _runtime.ExtensionTagsChanged += value;
            remove => _runtime.ExtensionTagsChanged -= value;
        }

        /// <summary>Occurs when reveal reaches an extension tag configured for reveal dispatch.</summary>
        public event Action<TextStudioExtensionTag> OnExtensionTagReached
        {
            add => _runtime.ExtensionTagReached += value;
            remove => _runtime.ExtensionTagReached -= value;
        }

        /// <summary>Occurs immediately before a typewriter wait suspends playback.</summary>
        public event Action<TextStudioTypewriterWaitInfo> OnTypewriterWaitStarted
        {
            add => _runtime.WaitStarted += value;
            remove => _runtime.WaitStarted -= value;
        }

        /// <summary>Occurs when a typewriter wait completes or is skipped.</summary>
        public event Action<TextStudioTypewriterWaitInfo> OnTypewriterWaitFinished
        {
            add => _runtime.WaitFinished += value;
            remove => _runtime.WaitFinished -= value;
        }

        public event Action OnShowComplete
        {
            add => _runtime.ShowComplete += value;
            remove => _runtime.ShowComplete -= value;
        }

        public event Action OnDisappearComplete
        {
            add => _onDisappearComplete += value;
            remove => _onDisappearComplete -= value;
        }

        /// <inheritdoc cref="TextStudioText.RegisterExtensionTagProcessor(ITextStudioExtensionTagProcessor)"/>
        public void RegisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            _runtime.RegisterExtensionTagProcessor(processor);
        }

        /// <inheritdoc cref="TextStudioText.UnregisterExtensionTagProcessor(ITextStudioExtensionTagProcessor)"/>
        public void UnregisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            _runtime.UnregisterExtensionTagProcessor(processor);
        }

        [UxmlAttribute("timings")]
        public TypingTimingsBase Timings
        {
            get => _runtime.Timings;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.Timings = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("built-in-timings")]
        public RevealTimingsPreset BuiltInTimingsPreset
        {
            get => _runtime.BuiltInTimingsPreset;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.BuiltInTimingsPreset = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("wait-after-final-reveal")]
        public bool WaitAfterFinalReveal
        {
            get => _runtime.WaitAfterFinalReveal;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.WaitAfterFinalReveal = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("reveal-mode")]
        public RevealMode RevealMode
        {
            get => _runtime.RevealMode;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealMode = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("speed-multiplier")]
        public float SpeedMultiplier
        {
            get => _runtime.RevealSpeedMultiplier;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealSpeedMultiplier = Mathf.Max(0.01f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("loop-reveal")]
        public bool LoopReveal
        {
            get => _runtime.LoopReveal;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.LoopReveal = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("loop-reveal-delay")]
        public float LoopRevealDelay
        {
            get => _runtime.LoopRevealDelay;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.LoopRevealDelay = Mathf.Max(0f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("reveal-duration")]
        public float RevealAppearanceDuration
        {
            get => _runtime.RevealAppearanceDuration;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealAppearanceDuration =
                    Mathf.Max(0.01f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("hide-duration")]
        public float RevealDisappearanceDuration
        {
            get => _runtime.RevealDisappearanceDuration;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealDisappearanceDuration =
                    Mathf.Max(0.01f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-start")]
        public TextStudioTypewriterStartMode TypewriterStartMode
        {
            get => _typewriterStartMode;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.StartMode = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("reset-speed-on-new-text")]
        public bool ResetSpeedOnNewText
        {
            get => _resetSpeedOnNewText;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.ResetSpeedOnNewText = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("skip-reveal-mode")]
        public TextStudioTypewriterSkipRevealMode SkipRevealMode
        {
            get => _runtime.SkipRevealMode;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.SkipRevealMode = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("trigger-remaining-markers-on-skip")]
        public bool TriggerRemainingMarkersOnSkip
        {
            get => _runtime.TriggerRemainingMarkersOnSkip;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.TriggerRemainingMarkersOnSkip = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("skip-current-wait-on-skip")]
        public bool SkipCurrentWaitOnSkip
        {
            get => _runtime.SkipCurrentWaitOnSkip;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.SkipCurrentWaitOnSkip = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("skip-hide-mode")]
        public TextStudioTypewriterSkipHideMode SkipHideMode
        {
            get => _runtime.SkipHideMode;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.SkipHideMode = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-timing")]
        public TextStudioTypewriterTimingMode TypewriterTiming
        {
            get => _inlineTypewriterSettings.TimingMode;
            set { EnsureInlineTypewriterSource(); _inlineTypewriterSettings.TimingMode = value; ApplyInlineTypewriterSettings(); }
        }

        [UxmlAttribute("reveal-sequence-direction")]
        public TextStudioSequenceDirection RevealSequenceDirection
        {
            get => _inlineTypewriterSettings.RevealSequence.Direction;
            set { EnsureInlineTypewriterSource(); _inlineTypewriterSettings.RevealSequence.Direction = value; ApplyInlineTypewriterSettings(); }
        }

        [UxmlAttribute("hide-sequence-direction")]
        public TextStudioSequenceDirection HideSequenceDirection
        {
            get => _inlineTypewriterSettings.HideSequence.Direction;
            set { EnsureInlineTypewriterSource(); _inlineTypewriterSettings.HideSequence.Direction = value; ApplyInlineTypewriterSettings(); }
        }

        [UxmlAttribute("reveal-sequence-order")]
        public TextStudioSequenceOrder RevealSequenceOrder
        {
            get => _inlineTypewriterSettings.RevealSequence.Order;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealSequence.Order = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("reveal-sequence-spread")]
        public float RevealSequenceSpread
        {
            get => _inlineTypewriterSettings.RevealSequence.Spread;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealSequence.Spread = Mathf.Max(0f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("reveal-sequence-seed")]
        public int RevealSequenceSeed
        {
            get => _inlineTypewriterSettings.RevealSequence.Seed;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealSequence.Seed = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("hide-sequence-order")]
        public TextStudioSequenceOrder HideSequenceOrder
        {
            get => _inlineTypewriterSettings.HideSequence.Order;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.HideSequence.Order = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("hide-sequence-spread")]
        public float HideSequenceSpread
        {
            get => _inlineTypewriterSettings.HideSequence.Spread;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.HideSequence.Spread = Mathf.Max(0f, value);
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("hide-sequence-seed")]
        public int HideSequenceSeed
        {
            get => _inlineTypewriterSettings.HideSequence.Seed;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.HideSequence.Seed = value;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-reveal-effect")]
        public TextStudioEffectDefinitionAsset TypewriterRevealEffect
        {
            get => _inlineTypewriterSettings.RevealEffect;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealEffect = value;
                _inlineTypewriterSettings.RevealEffectExpression = string.Empty;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-hide-effect")]
        public TextStudioEffectDefinitionAsset TypewriterHideEffect
        {
            get => _inlineTypewriterSettings.HideEffect;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.HideEffect = value;
                _inlineTypewriterSettings.HideEffectExpression = string.Empty;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-reveal-effect-tags")]
        public string TypewriterRevealEffectTags
        {
            get => _inlineTypewriterSettings.RevealEffectExpression ?? string.Empty;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.RevealEffect = null;
                _inlineTypewriterSettings.RevealEffectExpression = value ?? string.Empty;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("typewriter-hide-effect-tags")]
        public string TypewriterHideEffectTags
        {
            get => _inlineTypewriterSettings.HideEffectExpression ?? string.Empty;
            set
            {
                EnsureInlineTypewriterSource();
                _inlineTypewriterSettings.HideEffect = null;
                _inlineTypewriterSettings.HideEffectExpression = value ?? string.Empty;
                ApplyInlineTypewriterSettings();
            }
        }

        [UxmlAttribute("motion-policy")]
        public TextStudioMotionPolicy MotionPolicy
        {
            get => _motionPolicy;
            set
            {
                _motionPolicy = value;
                _runtime.MotionPolicy = value;
                ApplyImmediateRuntimeState();
            }
        }

        void ApplyInlineTypewriterSettings()
        {
            ApplyResolvedBehaviorSettings();
        }

        void EnsureInlineTypewriterSource()
        {
            if (_typewriterSource == TextStudioTypewriterSource.Inline)
                return;

            _inlineTypewriterSettings = GetLocalTypewriterRecipe().Resolve();
            _typewriterSource = TextStudioTypewriterSource.Inline;
        }

        public bool RevealEffectsEnabled
        {
            get => _runtime.RevealEffectsEnabled;
            set
            {
                _runtime.RevealEffectsEnabled = value;
                ApplyImmediateRuntimeState();
            }
        }

        public bool ActiveEffectsEnabled
        {
            get => _runtime.ActiveEffectsEnabled;
            set
            {
                _runtime.ActiveEffectsEnabled = value;
                ApplyImmediateRuntimeState();
            }
        }

        public bool HideEffectsEnabled
        {
            get => _runtime.HideEffectsEnabled;
            set
            {
                _runtime.HideEffectsEnabled = value;
                ApplyImmediateRuntimeState();
            }
        }

        [UxmlAttribute("enable-in-edit-mode")]
        public bool EnableInEditMode
        {
            get => _enableInEditMode;
            set
            {
                _enableInEditMode = value;
                if (value)
                    WakeAutomaticDriver();
            }
        }

        /// <summary>Attaches the binding runtime to its validated UI Toolkit text target.</summary>
        protected override void OnActivated(in BindingActivationContext context)
        {
            base.OnActivated(context);
            EnforceBindingPolicy();

            if (!TryValidateTarget(context.targetElement, context.bindingId, out TextElement target))
                return;

            _isBindingActive = true;
            _activeBindingId = context.bindingId;
            ActivateTarget(target, captureCurrentLabelText: !ReferenceEquals(_element, target));
            ConfigureEffectiveSource(captureLabelText: false);
            MarkDirty();
        }

        /// <summary>Detaches runtime state when UI Toolkit deactivates this binding.</summary>
        protected override void OnDeactivated(in BindingActivationContext context)
        {
            if (ReferenceEquals(context.targetElement, _element) && context.bindingId == _activeBindingId)
                Detach();

            base.OnDeactivated(context);
        }

        /// <summary>Refreshes authored text when UI Toolkit replaces the active data source.</summary>
        protected override void OnDataSourceChanged(in DataSourceContextChanged context)
        {
            base.OnDataSourceChanged(context);

            if (_sourceMode == TextStudioBindingSourceMode.Data && context.newContext.dataSource == null)
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.UnresolvedDataSource,
                    "The Text Studio Data source is unresolved. Assign a local source or provide one on the Label hierarchy.");
                return;
            }

            if (context.newContext.dataSource != null &&
                (_diagnostic.Code == TextStudioBindingDiagnosticCode.UnresolvedDataSource ||
                 _diagnostic.Code == TextStudioBindingDiagnosticCode.ConversionFailed))
            {
                SetDiagnostic(TextStudioBindingDiagnostic.None);
            }
        }

        protected override BindingResult Update(in BindingContext context)
        {
            EnforceBindingPolicy();
            if (!TryValidateTarget(context.targetElement, context.bindingId, out TextElement target))
                return new BindingResult(BindingStatus.Failure, _diagnostic.Message);

            if (!ReferenceEquals(target, _element))
                ActivateTarget(target, captureCurrentLabelText: true);

            if (context.dataSource == null)
            {
                const string message = "The Text Studio source is unresolved. Assign a local source or provide one on the Label hierarchy.";
                SetDiagnostic(TextStudioBindingDiagnosticCode.UnresolvedDataSource, message);
                return new BindingResult(BindingStatus.Failure, message);
            }

            if (context.dataSourcePath.IsEmpty)
            {
                object directValue = context.dataSource;
                return ApplyResolvedSourceValue(context, ref directValue);
            }

            object source = context.dataSource;
            _sourceValueVisitor.Begin(context);
            if (!PropertyContainer.TryAccept(
                    _sourceValueVisitor,
                    ref source,
                    context.dataSourcePath,
                    out VisitReturnCode returnCode))
            {
                string message = $"Text Studio could not read source path '{context.dataSourcePath}' ({returnCode}).";
                SetDiagnostic(TextStudioBindingDiagnosticCode.ConversionFailed, message);
                return new BindingResult(BindingStatus.Failure, message);
            }

            return _sourceValueVisitor.Result;
        }

        BindingResult ApplyResolvedSourceValue<TValue>(
            in BindingContext context,
            ref TValue value)
        {

            if (!TryConvertSourceValue(ref value, out string markup, out string conversionError))
            {
                SetDiagnostic(TextStudioBindingDiagnosticCode.ConversionFailed, conversionError);
                return new BindingResult(BindingStatus.Failure, conversionError);
            }

            if (_diagnostic.Code == TextStudioBindingDiagnosticCode.ConversionFailed ||
                _diagnostic.Code == TextStudioBindingDiagnosticCode.UnresolvedDataSource)
            {
                SetDiagnostic(TextStudioBindingDiagnostic.None);
            }

            ApplyResolvedSourceMarkup(markup);
            return new BindingResult(BindingStatus.Success);
        }

        internal bool TryConvertSourceValue<TValue>(
            ref TValue value,
            out string markup,
            out string error)
        {
            if (value is null)
            {
                markup = string.Empty;
                error = string.Empty;
                return true;
            }

            if (value is string directText)
            {
                markup = directText;
                error = string.Empty;
                return true;
            }

            try
            {
                if (sourceToUiConverters.TryConvert(ref value, out markup) ||
                    TryConvertWithAppliedGroups(ref value, out markup) ||
                    ConverterGroups.TryConvert(ref value, out markup))
                {
                    markup ??= string.Empty;
                    error = string.Empty;
                    return true;
                }
            }
            catch (Exception exception)
            {
                markup = null;
                error =
                    $"Text Studio could not convert {typeof(TValue).Name} to markup: {exception.Message}";
                return false;
            }

            markup = null;
            error =
                $"Text Studio cannot convert {typeof(TValue).Name} to string markup. Add a source-to-UI converter for this binding.";
            return false;
        }

        bool TryConvertWithAppliedGroups<TValue>(ref TValue value, out string markup)
        {
            for (int i = 0; i < _appliedSourceConverterGroups.Count; i++)
            {
                if (_appliedSourceConverterGroups[i].TryConvert(ref value, out markup))
                    return true;
            }

            markup = null;
            return false;
        }

        sealed class SourceValueVisitor : IPropertyVisitor
        {
            readonly TextStudioBinding _owner;
            BindingContext _context;

            public BindingResult Result { get; private set; }

            public SourceValueVisitor(TextStudioBinding owner)
            {
                _owner = owner;
            }

            public void Begin(in BindingContext context)
            {
                _context = context;
                Result = new BindingResult(BindingStatus.Pending);
            }

            public void Visit<TContainer, TValue>(
                Property<TContainer, TValue> property,
                ref TContainer container)
            {
                TValue value = property.GetValue(ref container);
                Result = _owner.ApplyResolvedSourceValue(_context, ref value);
            }
        }

        /// <summary>Attaches the binding directly for hosts that do not use VisualElement.SetBinding.</summary>
        public void Attach(TextElement element)
        {
            if (element == null)
            {
                Detach();
                return;
            }

            PrepareForRegistration(element);
            _isBindingActive = true;
            _activeBindingId = "text";
            ActivateTarget(element, captureCurrentLabelText: true);
            ConfigureEffectiveSource(captureLabelText: false);
        }

        /// <summary>Stops scheduling, restores the target element, and releases all attachment callbacks.</summary>
        public void Detach()
        {
            if (_subscribedCurveProfile != null)
            {
                _subscribedCurveProfile.Changed -= HandleCurveProfileChanged;
                _subscribedCurveProfile = null;
            }
            _runtime.ReleaseReactions(TextStudioReactionCancelReason.HostDetached);
            _reactionParticles?.Dispose();
            _reactionParticles = null;
#if UNITY_6000_2_OR_NEWER
            _reactionPanelCapture?.Dispose();
            _reactionPanelCapture = null;
#endif
            _layoutInterpolation = null;
            UnregisterEditorBinding();
            string restoredText = _sourceMode == TextStudioBindingSourceMode.LabelText
                ? _authoredMarkup
                : _attachmentFallbackMarkup;
            UntrackResolvedElementEvents();
            UntrackResolvedDataVisualEvents();
            _targetDriver.Detach(restoredText);
            _pendingStartOnAttach = false;
            _isBindingActive = false;
            _editorRawAuthoringView = false;
            _editorPreviewDriverActive = false;
            _editorDocumentPreviewDriverActive = false;
            _editModeActionRunner.Clear();
            _runtime.SetActionRunner(Application.isPlaying ? TextStudioLazyRuntimeActionRunner.Shared : _editModeActionRunner);
        }

        internal void PrepareForRegistration(TextElement target)
        {
            if (target == null)
                return;

            _preparedTarget = target;
            _attachmentFallbackMarkup = target.text ?? string.Empty;
            if (_sourceMode == TextStudioBindingSourceMode.LabelText)
                SetAuthoredMarkup(_attachmentFallbackMarkup, applyImmediately: false);
        }

        internal void CompleteBindingRemoval()
        {
            _preparedTarget = null;
            SetResolvedElementSource(null);
            SetResolvedDataVisualSource(null);
            _activeBindingId = BindingId.Invalid;
        }

        void ActivateTarget(TextElement target, bool captureCurrentLabelText)
        {
            RefreshReactionTargets();
            if (ReferenceEquals(_element, target))
            {
                _targetDriver.RefreshAttachment();
                return;
            }

            Detach();
            _preparedTarget = target;
            _attachmentFallbackMarkup = target.text ?? string.Empty;

            if (_sourceMode == TextStudioBindingSourceMode.LabelText)
            {
                if (captureCurrentLabelText || !_hasPreparedAuthoredMarkup)
                    SetAuthoredMarkup(_attachmentFallbackMarkup, applyImmediately: false);
                SetTextInternal(_authoredMarkup, applyStartPolicy: false);
            }
            else
            {
                SetTextInternal(_attachmentFallbackMarkup, applyStartPolicy: false);
            }

            _targetDriver.Attach(target, _textGeneratorPreference);
            SyncCurveProfileSubscription();
            ApplyStrategyText(true);
        }

        void SyncCurveProfileSubscription()
        {
            TextStudioCurveProfile next = _element != null ? _curvePlacement.Curve.Profile : null;
            if (_subscribedCurveProfile == next) return;
            if (_subscribedCurveProfile != null)
                _subscribedCurveProfile.Changed -= HandleCurveProfileChanged;
            _subscribedCurveProfile = next;
            if (next != null)
                next.Changed += HandleCurveProfileChanged;
        }

        void HandleCurveProfileChanged(TextStudioCurveProfile profile)
        {
            if (profile == _subscribedCurveProfile)
                ApplyPlacementChange();
        }

        bool TryValidateTarget(
            VisualElement candidate,
            in BindingId bindingId,
            out TextElement target)
        {
            target = candidate as TextElement;
            if (bindingId != (BindingId)"text")
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.InvalidTargetProperty,
                    "TextStudioBinding supports only the text property of a non-editable TextElement.");
                return false;
            }

            if (target == null)
            {
                string guidance = candidate is TextField
                    ? "Do not animate an editable TextField. Use the TextField as an Element source for a separate Label."
                    : "TextStudioBinding requires a non-editable TextElement such as Label.";
                SetDiagnostic(
                    candidate is TextField
                        ? TextStudioBindingDiagnosticCode.EditableTarget
                        : TextStudioBindingDiagnosticCode.UnsupportedTarget,
                    guidance);
                return false;
            }

            if (_diagnostic.Code == TextStudioBindingDiagnosticCode.InvalidTargetProperty ||
                _diagnostic.Code == TextStudioBindingDiagnosticCode.UnsupportedTarget ||
                _diagnostic.Code == TextStudioBindingDiagnosticCode.EditableTarget)
            {
                SetDiagnostic(TextStudioBindingDiagnostic.None);
            }

            return true;
        }

        void ConfigureEffectiveSource(bool captureLabelText)
        {
            EnforceBindingPolicy();
            SetResolvedElementSource(null);
            SetResolvedDataVisualSource(null);

            switch (_sourceMode)
            {
                case TextStudioBindingSourceMode.LabelText:
                    if (captureLabelText && _element != null)
                        SetAuthoredMarkup(_element.text, applyImmediately: true);
                    SetDiagnostic(TextStudioBindingDiagnostic.None);
                    break;
                case TextStudioBindingSourceMode.Element:
                    ResolveElementSource();
                    break;
                case TextStudioBindingSourceMode.Data:
                    SetResolvedDataVisualSource(_dataSource as VisualElement);
                    if (_diagnostic.Code == TextStudioBindingDiagnosticCode.MissingSourceElementName ||
                        _diagnostic.Code == TextStudioBindingDiagnosticCode.SourceElementNotFound ||
                        _diagnostic.Code == TextStudioBindingDiagnosticCode.DuplicateSourceElementName)
                    {
                        SetDiagnostic(TextStudioBindingDiagnostic.None);
                    }
                    break;
            }

            MarkDirty();
        }

        void ResolveElementSource()
        {
            if (string.IsNullOrWhiteSpace(_sourceElementName))
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.MissingSourceElementName,
                    "Choose a uniquely named source element for Text Studio Element mode.");
                return;
            }

            VisualElement root = _element?.panel?.visualTree;
            if (root == null)
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.SourceElementNotFound,
                    $"Source element '{_sourceElementName}' cannot be resolved until the Label is attached to a panel.");
                return;
            }

            List<VisualElement> matches = root.Query<VisualElement>(name: _sourceElementName).ToList();
            if (matches.Count == 0)
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.SourceElementNotFound,
                    $"No element named '{_sourceElementName}' exists in the Label's panel.");
                return;
            }

            if (matches.Count > 1)
            {
                SetDiagnostic(
                    TextStudioBindingDiagnosticCode.DuplicateSourceElementName,
                    $"More than one element is named '{_sourceElementName}'. Give the source a unique name.");
                return;
            }

            SetResolvedElementSource(matches[0]);
            SetDiagnostic(TextStudioBindingDiagnostic.None);
        }

        void SetResolvedElementSource(VisualElement source)
        {
            if (ReferenceEquals(_resolvedElementSource, source))
            {
                TrackResolvedElementEvents();
                return;
            }

            UntrackResolvedElementEvents();
            _resolvedElementSource = source;
            TrackResolvedElementEvents();
        }

        void TrackResolvedElementEvents()
        {
            _resolvedElementSource?.UnregisterCallback<ChangeEvent<string>>(
                HandleResolvedElementStringChanged);
            _resolvedElementSource?.RegisterCallback<ChangeEvent<string>>(
                HandleResolvedElementStringChanged);
        }

        void UntrackResolvedElementEvents()
        {
            _resolvedElementSource?.UnregisterCallback<ChangeEvent<string>>(
                HandleResolvedElementStringChanged);
        }

        void HandleResolvedElementStringChanged(ChangeEvent<string> evt)
        {
            if (_sourceMode != TextStudioBindingSourceMode.Element)
                return;

            if (IsDirectStringPropertyPath(_sourcePropertyPath))
                ApplyStringSourceEvent(evt.newValue);
            else
                MarkDirty();
        }

        void SetResolvedDataVisualSource(VisualElement source)
        {
            if (ReferenceEquals(_resolvedDataVisualSource, source))
            {
                TrackResolvedDataVisualEvents();
                return;
            }

            UntrackResolvedDataVisualEvents();
            _resolvedDataVisualSource = source;
            TrackResolvedDataVisualEvents();
        }

        void TrackResolvedDataVisualEvents()
        {
            _resolvedDataVisualSource?.UnregisterCallback<ChangeEvent<string>>(
                HandleResolvedDataVisualStringChanged);
            _resolvedDataVisualSource?.RegisterCallback<ChangeEvent<string>>(
                HandleResolvedDataVisualStringChanged);
        }

        void UntrackResolvedDataVisualEvents()
        {
            _resolvedDataVisualSource?.UnregisterCallback<ChangeEvent<string>>(
                HandleResolvedDataVisualStringChanged);
        }

        void HandleResolvedDataVisualStringChanged(ChangeEvent<string> evt)
        {
            if (_sourceMode != TextStudioBindingSourceMode.Data)
                return;

            string path = _dataSourcePath.ToString();
            if (IsDirectStringPropertyPath(path))
                ApplyStringSourceEvent(evt.newValue);
            else
                MarkDirty();
        }

        void ApplyStringSourceEvent(string value)
        {
            string source = value;
            if (!TryConvertSourceValue(ref source, out string markup, out string conversionError))
            {
                SetDiagnostic(TextStudioBindingDiagnosticCode.ConversionFailed, conversionError);
                return;
            }

            SetDiagnostic(TextStudioBindingDiagnostic.None);
            ApplyResolvedSourceMarkup(markup);
        }

        static bool IsDirectStringPropertyPath(string path)
        {
            return string.Equals(path, "value", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(path, "text", StringComparison.OrdinalIgnoreCase);
        }

        void ApplyResolvedSourceMarkup(string markup)
        {
            markup ??= string.Empty;
            if (_sourceMode == TextStudioBindingSourceMode.LabelText)
                SetAuthoredMarkup(markup, applyImmediately: false);
            SetTextInternal(markup, applyStartPolicy: true);
        }

        void SetAuthoredMarkup(string markup, bool applyImmediately)
        {
            markup ??= string.Empty;
            _authoredMarkup = markup;
            _hasPreparedAuthoredMarkup = true;
            _labelTextSource.Value = markup;
            if (applyImmediately)
                SetTextInternal(markup, applyStartPolicy: true);
        }

        internal void HandleTargetTextChanged(ChangeEvent<string> evt)
        {
            if (_targetDriver.OwnsTextWrite ||
                _targetDriver.IsOwnedTextValue(evt.newValue) ||
                _sourceMode != TextStudioBindingSourceMode.LabelText)
            {
                return;
            }

            SetAuthoredMarkup(evt.newValue, applyImmediately: true);
        }

        void SetDiagnostic(TextStudioBindingDiagnosticCode code, string message)
        {
            SetDiagnostic(new TextStudioBindingDiagnostic(code, message));
        }

        void SetDiagnostic(TextStudioBindingDiagnostic diagnostic)
        {
            if (_diagnostic == diagnostic)
                return;

            _diagnostic = diagnostic;
            DiagnosticChanged?.Invoke(_diagnostic);
        }

        void EnforceBindingPolicy()
        {
            if (base.updateTrigger != BindingUpdateTrigger.OnSourceChanged)
                base.updateTrigger = BindingUpdateTrigger.OnSourceChanged;
        }

        void ApplyTextGeneratorPreference()
        {
            _targetDriver.ApplyTextGeneratorPreference(_textGeneratorPreference);
        }

        internal bool WriteTargetText(string value)
        {
            return _targetDriver.WriteText(value);
        }

        internal void SetEditorRawAuthoringView(bool raw)
        {
            if (_editorRawAuthoringView == raw)
            {
                if (raw)
                    _targetDriver.SetRawAuthoringView(true, GetRawAuthoringMarkup());
                return;
            }

            _editorRawAuthoringView = raw;
            _targetDriver.SetRawAuthoringView(raw, GetRawAuthoringMarkup());
            if (raw)
            {
                PauseAutomaticDriver();
                return;
            }

            bool processed = ProcessPipeline();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            _strategy.ApplyText(processed || presentationChanged || evaluated || _element != null);
            WakeAutomaticDriver();
        }

        string GetRawAuthoringMarkup()
        {
            return _sourceMode == TextStudioBindingSourceMode.LabelText
                ? _authoredMarkup ?? string.Empty
                : _runtime.Markup ?? string.Empty;
        }

        void ApplyStrategyText(bool changed)
        {
            if (_editorRawAuthoringView)
            {
                _targetDriver.SetRawAuthoringView(true, GetRawAuthoringMarkup());
                return;
            }

            _strategy.ApplyText(changed);
            _runtime.SampleReactionsAfterPresentation();
        }

        void ApplyBehaviorProfile(TextStudioBehaviorProfile profile)
        {
            _behaviorProfile = profile;
            ApplyResolvedBehaviorSettings();
        }

        /// <summary>Refreshes effective behavior after an assigned profile asset changes.</summary>
        public void RefreshBehaviorProfile()
        {
            ApplyResolvedBehaviorSettings();
        }

        void ApplyResolvedBehaviorSettings(bool synchronizeReactions = true)
        {
            if (synchronizeReactions && NeedsReactionSynchronization)
                SyncReactions();
            TextStudioResolvedBehaviorSettings resolved =
                TextStudioBehaviorResolver.Resolve(
                    _behaviorProfile,
                    _inlineDefaultEffects,
                    GetLocalTypewriterRecipe());
            TextStudioTypewriterSettings settings = resolved.Typewriter.Resolve();
            if (_hasAppliedBehaviorSettings &&
                _appliedBehaviorDefaultEffects.Equals(resolved.DefaultEffects) &&
                _appliedBehaviorTypewriterSettings.Equals(settings) &&
                _appliedDefaultEffectsSource == resolved.DefaultEffectsSource &&
                _appliedTypewriterSource == resolved.TypewriterSource)
            {
                return;
            }

            _appliedBehaviorDefaultEffects = resolved.DefaultEffects;
            _appliedBehaviorTypewriterSettings = settings;
            _appliedDefaultEffectsSource = resolved.DefaultEffectsSource;
            _appliedTypewriterSource = resolved.TypewriterSource;
            _hasAppliedBehaviorSettings = true;
            _runtime.ApplyDefaultEffects(resolved.DefaultEffects);
            ApplyTypewriterSettings(in settings);
            _typewriterStartMode = settings.StartMode;
            _textChangeMode = settings.TextChangeMode;
            _textChangeScope = settings.TextChangeScope;
            _pendingStartOnAttach = _isPanelAttached &&
                                    string.IsNullOrEmpty(_runtime.Markup) &&
                                    ShouldStartOnAttach();

            bool processed = ProcessPipeline();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(processed || evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        TextStudioTypewriterRecipe GetLocalTypewriterRecipe()
        {
            switch (_typewriterSource)
            {
                case TextStudioTypewriterSource.BuiltIn:
                    return TextStudioTypewriterRecipe.BuiltIn(_typewriterPresetId);
                case TextStudioTypewriterSource.Custom:
                    return TextStudioTypewriterRecipe.Custom(_typewriterPreset);
                case TextStudioTypewriterSource.Inline:
                    return TextStudioTypewriterRecipe.Inline(_inlineTypewriterSettings);
                case TextStudioTypewriterSource.None:
                default:
                    return TextStudioTypewriterRecipe.None;
            }
        }

        void ApplyTypewriterSettings(in TextStudioTypewriterSettings settings)
        {
            _configuredRevealSpeedMultiplier = settings.RevealSpeedMultiplier;
            _resetSpeedOnNewText = settings.ResetSpeedOnNewText;
            _runtime.Configure(
                settings.RevealMode,
                settings.Timings,
                settings.BuiltInTimingsPreset,
                settings.WaitAfterFinalReveal,
                settings.RevealSpeedMultiplier,
                settings.RevealAppearanceDuration,
                settings.RevealDisappearanceDuration,
                settings.LoopReveal,
                settings.LoopRevealDelay,
                settings.RevealEffectExpression,
                settings.HideEffectExpression,
                string.IsNullOrEmpty(settings.RevealEffectExpression) &&
                string.IsNullOrEmpty(settings.HideEffectExpression)
                    ? TextStudioDefaultEffectMode.None
                    : TextStudioDefaultEffectMode.Constant,
                settings.SkipRevealMode,
                settings.TriggerRemainingMarkersOnSkip,
                settings.SkipCurrentWaitOnSkip,
                settings.SkipHideMode,
                settings.RevealSequence,
                settings.HideSequence,
                settings.RevealEffect,
                settings.HideEffect,
                settings.TimingMode);
        }

        internal void HandleTargetAttachedToPanel()
        {
            ApplyTextGeneratorPreference();
            SyncReactions();
            RegisterEditorBinding();
            if (_sourceMode == TextStudioBindingSourceMode.Element)
                ConfigureEffectiveSource(captureLabelText: false);
            _pendingStartOnAttach = ShouldStartOnAttach();
            TryStartPendingAttachPlayback();
            WakeAutomaticDriver();
        }

        internal void HandleTargetDetachedFromPanel()
        {
            RefreshReactionTargets();
            _runtime.ReleaseReactions(TextStudioReactionCancelReason.HostDetached);
            _layoutInterpolation = null;
            UnregisterEditorBinding();
            _pendingStartOnAttach = false;
            _editorPreviewDriverActive = false;
            _editorDocumentPreviewDriverActive = false;
        }

        bool ShouldStartOnAttach()
        {
            return _typewriterStartMode == TextStudioTypewriterStartMode.OnEnable ||
                   _typewriterStartMode == TextStudioTypewriterStartMode.OnEnableOrTextChanged;
        }

        bool ShouldStartForTextChange()
        {
            if (_pendingStartOnAttach)
                return true;

            return _typewriterStartMode == TextStudioTypewriterStartMode.OnTextChanged ||
                   _typewriterStartMode == TextStudioTypewriterStartMode.OnEnableOrTextChanged;
        }

        void TryStartPendingAttachPlayback()
        {
            if (!_pendingStartOnAttach ||
                string.IsNullOrEmpty(_runtime.Markup) ||
                !_isPanelAttached)
            {
                return;
            }

            _pendingStartOnAttach = false;
            StartReveal();
        }

        void SetTextInternal(string markup, bool applyStartPolicy)
        {
            RequestTextChange(markup, applyStartPolicy && ShouldStartForTextChange(), false);
        }

        void RequestTextChange(string markup, bool forceReveal, bool deferred,
            bool preferIncrementalAppend = false, bool restartUnchanged = false,
            bool forceImmediate = false, TextStudioTextChangeScope? scopeOverride = null)
        {
            _startRevealAfterDeferredPipeline = false;
            ResetRevealSpeedForNewTextIfNeeded();
            TextStudioTextChangeRequest request = new TextStudioTextChangeRequest
            {
                Markup = markup ?? string.Empty,
                Deferred = deferred,
                RaiseTextSetEvent = true,
                RevealOnChange = forceReveal,
                RestartUnchanged = restartUnchanged,
                Append = preferIncrementalAppend,
                CanAnimate = !forceImmediate && !_editorRawAuthoringView,
                NeedsFrameChangeMap = _textChangeLayoutDuration > 0f,
                NeedsReactionChangeMap = _runtime.ReactionsRequireChangedUnits,
                NeedsReactionOutgoingCapture = _runtime.ReactionsRequireOutgoingCapture,
                MarkupSyntax = _markupSyntaxOverride,
                Mode = forceImmediate ? TextStudioTextChangeMode.Immediate : _textChangeMode,
                Scope = scopeOverride ?? _textChangeScope
            };
            _textChanges.Request(this, in request);
        }

        void CommitTextChange(
            string markup,
            bool startReveal,
            bool deferred,
            TextStudioGlyphChangeSet glyphChanges = default,
            bool preferIncrementalAppend = false,
            bool preserveContinuity = false,
            TextStudioGlyphChangeSet reactionChanges = default)
        {
            _layoutInterpolation?.Prepare(in glyphChanges);
            if (preserveContinuity && glyphChanges.IsDefined)
                _runtime.PrepareTextChangeContinuity(glyphChanges);
            else
                _runtime.DiscardTextChangeContinuity();
            _runtime.PrepareReactionTextChange(in reactionChanges);
            if (preferIncrementalAppend)
                _runtime.AppendCanonicalText(markup);
            else
                _runtime.SetText(markup);
            if (deferred)
            {
                _startRevealAfterDeferredPipeline = startReveal;
                WakeAutomaticDriver();
                return;
            }

            bool processed = ProcessPipeline();
            if (startReveal && _runtime.Plan != null)
            {
                _pendingStartOnAttach = false;
                _runtime.StartTextChangeReveal();
            }

            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(processed || evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        void CompletePendingTextChange() => _textChanges.Complete(this);

        void HandleRuntimeDisappearComplete()
        {
            _preview?.OnDisappearComplete();
            _onDisappearComplete?.Invoke();
            CompletePendingTextChange();
        }

        void HandleRuntimeShowComplete()
        {
            _preview?.OnRevealComplete();
        }

        void ClearPendingTextChange() => _textChanges.Cancel(this, false);

        bool RequiresAutomaticUpdate()
        {
            if (_editorRawAuthoringView)
                return false;

            bool previewAllowsContinuousWork =
                _preview == null || _preview.ShowEndState || _preview.IsPlaying;

            return (_preview != null && _preview.IsPlaying) ||
                   (_layoutInterpolation != null && _layoutInterpolation.HasWork) ||
                   _startRevealAfterDeferredPipeline ||
                   _runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style) ||
                   (previewAllowsContinuousWork && _runtime.RequiresContinuousEvaluation) ||
                   _runtime.ActiveActionCount > 0 ||
                   (previewAllowsContinuousWork && RequiresAnimatedPlacementUpdate()) ||
                   (previewAllowsContinuousWork && HasActiveExternalEffectSources()) ||
                   (previewAllowsContinuousWork && _characterSubstitutionPresenter.HasActiveSubstitutions);
        }

        bool RequiresAnimatedPlacementUpdate()
        {
            return _placementMode == PlacementMode.Wave && _wavePlacement.IsAnimated;
        }

        void WakeAutomaticDriver()
        {
            if (_editorPreviewDriverActive)
            {
                PauseAutomaticDriver();
                return;
            }

            _targetDriver.WakeAutomaticDriver(
                UsesAutomaticTargetDriver(),
                CanAdvanceInCurrentContext(),
                RequiresAutomaticUpdate());
        }

        bool UsesAutomaticTargetDriver()
        {
            return _updateMode == TextStudioUITKUpdateMode.Automatic ||
                   (_editorDocumentPreviewDriverActive &&
                    _element?.panel?.contextType == ContextType.Editor);
        }

        bool CanAdvanceInCurrentContext()
        {
            return Application.isPlaying ||
                   _enableInEditMode ||
                   _element?.panel?.contextType == ContextType.Editor;
        }

        void PauseAutomaticDriver()
        {
            _targetDriver.PauseAutomaticDriver();
        }

        void PauseAutomaticDriverIfIdle()
        {
            if (!RequiresAutomaticUpdate())
                _targetDriver.PauseAutomaticDriver();
        }

        void ScheduledAutomaticUpdate(float deltaTime)
        {
            if (!UsesAutomaticTargetDriver() ||
                _editorPreviewDriverActive ||
                !_isPanelAttached ||
                !CanAdvanceInCurrentContext())
            {
                PauseAutomaticDriver();
                return;
            }

            Update(deltaTime);
            PauseAutomaticDriverIfIdle();
        }

        public void SetText(string markup)
        {
            RecordProgrammaticAuthoredMarkup(markup);
            SetTextInternal(markup, true);
        }

        /// <summary>Replaces markup using a scope for this change only, preserving the authored settings and start policy.</summary>
        public void SetText(string markup, TextStudioTextChangeScope scope)
        {
            RecordProgrammaticAuthoredMarkup(markup);
            RequestTextChange(markup, ShouldStartForTextChange(), false, scopeOverride: scope);
        }

        /// <summary>
        /// Replaces pending markup without compiling it until the next automatic or manual update.
        /// Repeated calls before that update compile only the newest value.
        /// </summary>
        public void SetTextDeferred(string markup)
        {
            RecordProgrammaticAuthoredMarkup(markup);
            RequestTextChange(markup, ShouldStartForTextChange(), true);
        }

        void ResetRevealSpeedForNewTextIfNeeded()
        {
            if (_resetSpeedOnNewText)
                _runtime.RevealSpeedMultiplier = _configuredRevealSpeedMultiplier;
        }

        public void AppendText(string markup)
        {
            if (string.IsNullOrEmpty(markup))
                return;
            string appendedMarkup = (_requestedMarkup ?? string.Empty) + markup;
            RecordProgrammaticAuthoredMarkup(appendedMarkup);
            RequestTextChange(
                appendedMarkup,
                ShouldStartForTextChange(),
                false,
                preferIncrementalAppend: true);
        }

        /// <summary>Appends markup and defers compilation until the next automatic or host update.</summary>
        public void AppendTextDeferred(string markup)
        {
            if (string.IsNullOrEmpty(markup))
                return;
            string appendedMarkup = (_requestedMarkup ?? string.Empty) + markup;
            RecordProgrammaticAuthoredMarkup(appendedMarkup);
            RequestTextChange(
                appendedMarkup,
                ShouldStartForTextChange(),
                true,
                preferIncrementalAppend: true);
        }

        public void ClearText()
        {
            RecordProgrammaticAuthoredMarkup(string.Empty);
            ClearPendingTextChange();
            if (_runtime.ReactionsRequireOutgoingCapture)
                RequestTextChange(string.Empty, false, false, forceImmediate: true);
            else
                _requestedMarkup = string.Empty;
            _runtime.ClearText();
            _startRevealAfterDeferredPipeline = false;
            bool changed = ProcessPipeline();
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(changed || evaluated);
            WakeAutomaticDriver();
        }

        public void ShowText(string markup)
        {
            _preview?.TakeRuntimePlaybackOwnership();
            RecordProgrammaticAuthoredMarkup(markup);
            RequestTextChange(markup, true, false, restartUnchanged: true);
        }

        /// <summary>Reveals markup using a scope for this change only, without modifying the authored settings.</summary>
        /// <remarks>Scopes that retain matching glyphs reveal only changed glyphs, so a change that leaves every glyph matching keeps the current reveal. <see cref="TextStudioTextChangeScope.WholeText"/> replays the complete text.</remarks>
        public void ShowText(string markup, TextStudioTextChangeScope scope)
        {
            _preview?.TakeRuntimePlaybackOwnership();
            RecordProgrammaticAuthoredMarkup(markup);
            RequestTextChange(markup, true, false,
                restartUnchanged: scope == TextStudioTextChangeScope.WholeText, scopeOverride: scope);
        }

        public void ShowTextInstant(string markup)
        {
            _preview?.TakeRuntimePlaybackOwnership();
            _layoutInterpolation?.Reset();
            RecordProgrammaticAuthoredMarkup(markup);
            ClearPendingTextChange();
            if (_runtime.ReactionsRequireOutgoingCapture)
                RequestTextChange(markup, false, false, forceImmediate: true);
            else
            {
                _requestedMarkup = markup ?? string.Empty;
                CommitTextChange(_requestedMarkup, false, false);
            }
            SkipReveal();
        }

        void RecordProgrammaticAuthoredMarkup(string markup)
        {
            if (_sourceMode != TextStudioBindingSourceMode.LabelText)
                return;

            markup ??= string.Empty;
            SetAuthoredMarkup(markup, applyImmediately: false);
            if (!_isBindingActive && _preparedTarget != null)
                _preparedTarget.text = markup;
        }

        public void StartReveal()
        {
            SyncReactions();
            _preview?.TakeRuntimePlaybackOwnership();
            if (_textChanges.IsPending)
            {
                _textChanges.PendingReveal = true;
                return;
            }
            bool processed = false;
            if (_runtime.Plan == null || _runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
                processed = ProcessPipeline();
            if (_runtime.Plan == null)
                return;

            _runtime.StartReveal();
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            _presentationTime = 0f;
            _presentationTimeDiscontinuous = true;
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(processed || evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        /// <summary>Continues a prepared text change without replaying retained glyphs.</summary>
        public void ContinueReveal()
        {
            _preview?.TakeRuntimePlaybackOwnership();
            if (_textChanges.IsPending)
            {
                _textChanges.PendingReveal =
                    _textChanges.PendingChanges.IncomingChangedGlyphCount > 0;
                return;
            }

            _runtime.ContinueTextChangeReveal();
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            _presentationTimeDiscontinuous = true;
            ApplyImmediateRuntimeState();
        }

        public void SkipReveal()
        {
            _runtime.SkipReveal();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        public void StopReveal()
        {
            _runtime.StopShowing();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        /// <inheritdoc cref="ITypewriterProvider.TriggerRemainingEvents"/>
        public void TriggerRemainingEvents()
        {
            _runtime.TriggerRemainingEvents();
        }

        public bool AdvanceOrSkip()
        {
            bool shouldAdvance = _runtime.AdvanceOrSkip();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
            return shouldAdvance;
        }

        public void StartDisappear()
        {
            bool processed = false;
            if (_runtime.Plan == null)
                processed = ProcessPipeline();
            if (_runtime.Plan == null)
                return;

            _runtime.StartDisappear();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(processed || evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        public void SkipDisappear()
        {
            _runtime.SkipDisappear();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        /// <inheritdoc cref="ITypewriterProvider.StopDisappear"/>
        public void StopDisappear()
        {
            if (_textChanges.IsPending)
            {
                ClearPendingTextChange();
                _requestedMarkup = _runtime.Markup;
            }
            _runtime.StopDisappear();
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        /// <inheritdoc cref="ITypewriterProvider.GetApproximateHideDuration"/>
        public float GetApproximateHideDuration()
        {
            return _runtime.GetApproximateHideDuration();
        }

        public void SetRevealSpeed(float multiplier)
        {
            SpeedMultiplier = multiplier;
        }

        public void PauseReveal()
        {
            _runtime.PauseReveal();
            PauseAutomaticDriverIfIdle();
        }

        public void ResumeReveal()
        {
            _preview?.TakeRuntimePlaybackOwnership();
            _runtime.ResumeReveal();
            WakeAutomaticDriver();
        }

        public void SkipCurrentWait()
        {
            _runtime.SkipCurrentWait();
            WakeAutomaticDriver();
        }

        public void SetRevealProgress(float progress)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();

            _presentationTimeDiscontinuous = true;
            _runtime.SetRevealProgress(progress);
            ApplyImmediateRuntimeState();
        }

        public bool SetGlyphVisibility(int glyphIndex, bool visible, bool playTransition = true)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();

            bool changed = _runtime.SetGlyphVisibility(glyphIndex, visible, playTransition);
            if (changed)
                ApplyImmediateRuntimeState();
            return changed;
        }

        public bool SetWordVisibility(int wordIndex, bool visible, bool playTransition = true)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();

            bool changed = _runtime.SetWordVisibility(wordIndex, visible, playTransition);
            if (changed)
                ApplyImmediateRuntimeState();
            return changed;
        }

        public bool SetTextVisibility(bool visible, bool playTransition = true)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();

            bool changed = _runtime.SetTextVisibility(visible, playTransition);
            if (changed)
                ApplyImmediateRuntimeState();
            return changed;
        }

        public bool ClearGlyphVisibilityOverride(int glyphIndex)
        {
            bool changed = _runtime.ClearGlyphVisibilityOverride(glyphIndex);
            if (changed)
                ApplyImmediateRuntimeState();
            return changed;
        }

        public bool ClearVisibilityOverrides()
        {
            bool changed = _runtime.ClearVisibilityOverrides();
            if (changed)
                ApplyImmediateRuntimeState();
            return changed;
        }

        /// <inheritdoc cref="TextStudioText.ConfigureEffectFamilies(bool, bool, bool)"/>
        public void ConfigureEffectFamilies(bool reveal, bool active, bool hide)
        {
            _runtime.ConfigureEffectFamilies(reveal, active, hide);
            ApplyImmediateRuntimeState();
        }

        /// <inheritdoc cref="TextStudioText.RestartEffects"/>
        public void RestartEffects()
        {
            _runtime.RestartEffects();
            ApplyImmediateRuntimeState();
        }

        /// <inheritdoc cref="TextStudioText.RefreshEffectBindings"/>
        public void RefreshEffectBindings()
        {
            _runtime.RefreshEffectBindings();
            ApplyImmediateRuntimeState();
        }

        /// <inheritdoc cref="TextStudioText.ApplyExternalEffect"/>
        public bool ApplyExternalEffect(
            ITextStudioEffectDefinition effect,
            float progress,
            float intensity = 1f,
            TextStudioTextRange range = default)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();

            bool applied = _runtime.ApplyExternalEffect(
                effect,
                progress,
                intensity,
                range);
            if (applied)
                WakeAutomaticDriver();
            return applied;
        }

        /// <summary>Registers an ordered external effect source with this binding.</summary>
        public void RegisterExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (source == null || _externalEffectSources.Contains(source))
                return;
            _externalEffectSources.Add(source);
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            WakeAutomaticDriver();
        }

        /// <summary>Stops polling a previously registered external effect source.</summary>
        public void UnregisterExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (source == null || !_externalEffectSources.Remove(source))
                return;
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            ApplyImmediateRuntimeState();
        }

        /// <inheritdoc cref="TextStudioText.SetRevealLoop(bool, float)"/>
        public void SetRevealLoop(bool loop, float delay = 0.75f)
        {
            _inlineTypewriterSettings.LoopReveal = loop;
            _inlineTypewriterSettings.LoopRevealDelay = Mathf.Max(0f, delay);
            if (!UsesBehaviorProfileTypewriter)
                _runtime.SetRevealLoop(loop, delay);
            WakeAutomaticDriver();
        }

        public void Update(float deltaTime)
        {
            if (_element == null)
                return;
            if (!CanAdvanceInCurrentContext())
                return;

            if (NeedsReactionSynchronization)
                SyncReactions();
            _runtime.EnsureEffectContentCurrent();
            if (Application.isPlaying)
                _editModeActionRunner.Clear();
            _runtime.SetActionRunner(
                Application.isPlaying
                    ? TextStudioLazyRuntimeActionRunner.Shared
                    : _editModeActionRunner);

            if (_behaviorProfile != null)
                ApplyResolvedBehaviorSettings(synchronizeReactions: false);

            if (Application.isPlaying)
            {
                int frame = Time.frameCount;
                if (!_targetDriver.TryBeginFrame(frame))
                    return;
            }

            float hostDeltaTime = _runtime.ResolvePresentationDeltaTime(deltaTime);
            float effectiveDeltaTime = _preview != null
                ? _preview.GetEffectiveDeltaTime(hostDeltaTime)
                : hostDeltaTime;
            float presentationDeltaTime = _preview != null
                ? _preview.GetEffectivePlacementDeltaTime(hostDeltaTime)
                : hostDeltaTime;
            _presentationTime += presentationDeltaTime;
            bool layoutTransitionChanged = _layoutInterpolation?.Advance(presentationDeltaTime) == true;

            if (_preview != null)
                _preview.Tick(effectiveDeltaTime);
            bool previewStartedDisappear =
                _preview != null && _preview.ConsumeStartedDisappearThisTick();
            bool previewDroveReveal =
                _preview != null && _preview.ConsumeDroveRevealThisTick();

            bool pipelineChanged = false;
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Pipeline))
            {
                if (_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
                    pipelineChanged = ProcessPipeline();
            }

            if (_startRevealAfterDeferredPipeline && _runtime.Plan != null)
            {
                _startRevealAfterDeferredPipeline = false;
                _pendingStartOnAttach = false;
                _runtime.StartTextChangeReveal();
                _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
                _presentationTime = 0f;
                _presentationTimeDiscontinuous = true;
                pipelineChanged = true;
            }

            PollExternalEffectSources();

            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Evaluation))
            {
                if (!Application.isPlaying)
                    _editModeActionRunner.Tick(effectiveDeltaTime);

                float revealDeltaTime = previewStartedDisappear || previewDroveReveal
                    ? 0f
                    : effectiveDeltaTime;
                _runtime.TickReveal(
                    revealDeltaTime,
                    _preview == null || !_preview.IsScrubbing);
            }
            bool presentationChanged;
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Layout))
                presentationChanged = UpdatePresentation(presentationDeltaTime);
            bool evaluated;
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Evaluation))
                evaluated = EvaluateRenderedStateIfNeeded(effectiveDeltaTime);
            bool placementChanged = RequiresAnimatedPlacementUpdate() || layoutTransitionChanged;
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Dispatch))
                ApplyStrategyText(pipelineChanged || presentationChanged || evaluated || placementChanged);
            _runtime.FlushReactionSignals();
        }

        /// <summary>
        /// Advances a Manual binding from a host-owned clock. Automatic bindings already advance
        /// through the target panel scheduler and ignore duplicate work within one player frame.
        /// </summary>
        public void UpdateFromHost(float deltaTime)
        {
            Update(deltaTime);
        }

        internal void SetEditorPreviewDriverActive(bool active)
        {
            if (_editorPreviewDriverActive == active)
            {
                if (active)
                    PauseAutomaticDriver();
                return;
            }

            _editorPreviewDriverActive = active;
            if (active)
            {
                _editorDocumentPreviewDriverActive = false;
                PauseAutomaticDriver();
                return;
            }

            RefreshPreviewScheduling();
        }

        internal void ContinueEditorPreviewInDocument()
        {
            _editorPreviewDriverActive = false;
            _editorDocumentPreviewDriverActive =
                _element?.panel?.contextType == ContextType.Editor;
            RefreshPreviewScheduling();
        }

        internal void UpdateFromEditorPreviewDriver(float deltaTime)
        {
            if (!_editorPreviewDriverActive || !RequiresAutomaticUpdate())
                return;

            Update(Mathf.Clamp(deltaTime, 0f, 0.1f));
        }

        void ApplyImmediateRuntimeState()
        {
            bool presentationChanged = UpdatePresentation(0f);
            bool evaluated = EvaluateRenderedStateIfNeeded(0f);
            ApplyStrategyText(evaluated || presentationChanged);
            WakeAutomaticDriver();
        }

        /// <summary>Gets plain visible text suitable for UI Toolkit fallback rendering without Text Studio tags.</summary>
        public string GetBasicVisibleText()
        {
            if (_runtime.Plan == null)
                return string.Empty;

            if (IsFullParity || (_runtime.RevealMode == RevealMode.Instant && !_runtime.HasCueTimeline))
                return _visibleText;

            int revealed = Mathf.Clamp(_runtime.RevealedGlyphCount, 0, _resolvedText.Length);
            if (!string.IsNullOrEmpty(_visibleText) &&
                _visibleText.Length == _resolvedText.Length &&
                !string.Equals(_visibleText, _displayText, StringComparison.Ordinal))
            {
                return _visibleText.Substring(0, revealed);
            }

            if (_revealPrefixes == null || _revealPrefixes.Length != _resolvedText.Length + 1)
                BuildRevealPrefixCache();
            return _revealPrefixes[revealed];
        }

        /// <inheritdoc cref="TextStudioText.ConfigureLinearPlacement"/>
        public void ConfigureLinearPlacement()
        {
            _placementMode = PlacementMode.Linear;
            ApplyPlacementChange();
        }

        /// <summary>Selects curve placement with a copied inline definition and updates its height, text selection, and match mapping.</summary>
        public void ConfigureCurvePlacement(in TextStudioCurveDefinition definition, float height = 0.5f,
            TextStudioApplicationSelector selection = default,
            TextStudioCurveMapping mapping = TextStudioCurveMapping.EachMatch)
        {
            _curvePlacement.ConfigureInline(in definition, height, in selection, mapping);
            _placementMode = PlacementMode.Curve;
            SyncCurveProfileSubscription();
            ApplyPlacementChange();
        }

        /// <summary>Selects curve placement with a linked shared profile and updates its height, text selection, and match mapping.</summary>
        public void ConfigureCurvePlacement(TextStudioCurveProfile profile, float height = 0.5f,
            TextStudioApplicationSelector selection = default,
            TextStudioCurveMapping mapping = TextStudioCurveMapping.EachMatch)
        {
            _curvePlacement.ConfigureProfile(profile, height, in selection, mapping);
            _placementMode = PlacementMode.Curve;
            SyncCurveProfileSubscription();
            ApplyPlacementChange();
        }

        /// <summary>Copies the linked curve profile into local placement settings and updates the binding.</summary>
        public void MakeCurvePlacementLocal()
        {
            _curvePlacement.Curve.MakeLocal();
            SyncCurveProfileSubscription();
            ApplyPlacementChange();
        }

        /// <inheritdoc cref="TextStudioText.ConfigureArcPlacement(float, float, float, PathSpacing, float, bool, float, PathPosition)"/>
        public void ConfigureArcPlacement(
            float radius,
            float arcAngle,
            float startAngle = 0f,
            PathSpacing spacing = PathSpacing.Even,
            float fill = 1f,
            bool reverse = false,
            float radiusChange = 0f,
            PathPosition pathPosition = PathPosition.Above)
        {
            _arcPlacement.radius = radius;
            _arcPlacement.arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
            _arcPlacement.startAngle = startAngle;
            _arcPlacement.spacing = spacing;
            _arcPlacement.fill = Mathf.Clamp(fill, 0.1f, 1f);
            _arcPlacement.reverse = reverse;
            _arcPlacement.radiusChange = radiusChange;
            _arcPlacement.pathPosition = pathPosition;
            _placementMode = PlacementMode.Arc;
            ApplyPlacementChange();
        }

        /// <inheritdoc cref="TextStudioText.ConfigureWavePlacement(float, float, float, float)"/>
        public void ConfigureWavePlacement(
            float amplitude,
            float frequency,
            float speed,
            float phase = 0f)
        {
            ConfigureSpatialWavePlacement(amplitude, 0f, frequency, speed, 0f, 0f, phase);
        }

        /// <summary>Configures vertical/depth wave placement and glyph roll using the host playback clock. Spin values are revolutions per second and revolutions across the text.</summary>
        public void ConfigureSpatialWavePlacement(
            float amplitude,
            float depthAmplitude,
            float frequency,
            float speed,
            float spinSpeed = 0f,
            float spinStagger = 0f,
            float phase = 0f)
        {
            _wavePlacement.amplitude = amplitude;
            _wavePlacement.frequency = frequency;
            _wavePlacement.phase = phase;
            _wavePlacement.speed = speed;
            _wavePlacement.depthAmplitude = depthAmplitude;
            _wavePlacement.spinSpeed = spinSpeed;
            _wavePlacement.spinStagger = spinStagger;
            _placementMode = PlacementMode.Wave;
            ApplyPlacementChange();
        }

        /// <inheritdoc cref="TextStudioText.ConfigureCirclePlacement(float, float, PathSpacing, bool, float, PathPosition)"/>
        public void ConfigureCirclePlacement(
            float radius,
            float startAngle = 0f,
            PathSpacing spacing = PathSpacing.Even,
            bool reverse = false,
            float radiusChange = 0f,
            PathPosition pathPosition = PathPosition.Above)
        {
            _circlePlacement.radius = radius;
            _circlePlacement.startAngle = startAngle;
            _circlePlacement.spacing = spacing;
            _circlePlacement.reverse = reverse;
            _circlePlacement.radiusChange = radiusChange;
            _circlePlacement.pathPosition = pathPosition;
            _placementMode = PlacementMode.Circle;
            ApplyPlacementChange();
        }

        /// <summary>Configures row- or column-major grid placement with explicit or automatic cell sizing.</summary>
        public void ConfigureGridPlacement(
            int columns,
            GridCellSizing sizing = GridCellSizing.Auto,
            Vector2 cellSize = default,
            Vector2 gap = default,
            GridFlow flow = GridFlow.RowsFirst,
            bool reverse = false,
            bool respectSourceLines = false)
        {
            _gridPlacement.columns = Mathf.Max(1, columns);
            _gridPlacement.cellSizing = sizing;
            _gridPlacement.cellSize = cellSize == default
                ? new Vector2(0.6f, 1f)
                : cellSize;
            _gridPlacement.gap = gap;
            _gridPlacement.flow = flow;
            _gridPlacement.reverse = reverse;
            _gridPlacement.respectSourceLines = respectSourceLines;
            _placementMode = PlacementMode.Grid;
            ApplyPlacementChange();
        }

        /// <inheritdoc cref="TextStudioText.SetArcStartAngle(float)"/>
        public void SetArcStartAngle(float startAngle)
        {
            ArcStartAngle = startAngle;
        }

        /// <inheritdoc cref="TextStudioText.SetCircleStartAngle(float)"/>
        public void SetCircleStartAngle(float startAngle)
        {
            CircleStartAngle = startAngle;
        }

        /// <summary>Sets glyph orientation along the selected placement plus an optional local Euler rotation in degrees.</summary>
        public void SetPlacementOrientation(
            TextStudioPlacementOrientation orientation,
            Vector3 fineRotation = default)
        {
            if (_placementRotation.orientation == orientation &&
                _placementRotation.fineRotation == fineRotation)
            {
                return;
            }

            _placementRotation.orientation = orientation;
            _placementRotation.fineRotation = fineRotation;
            ApplyPlacementChange();
        }

        internal void BeginPlacementUpdate()
        {
            _placementUpdateDepth++;
        }

        internal void EndPlacementUpdate()
        {
            if (_placementUpdateDepth <= 0)
                return;

            _placementUpdateDepth--;
            if (_placementUpdateDepth != 0 || !_placementChangePending)
                return;

            _placementChangePending = false;
            ApplyPlacementChangeImmediately();
        }

        void ApplyPlacementChange()
        {
            if (_placementUpdateDepth > 0)
            {
                _placementChangePending = true;
                return;
            }

            ApplyPlacementChangeImmediately();
        }

        void ApplyPlacementChangeImmediately()
        {
            _runtime.SetDirty(DirtyFlags.Placement);
            ApplyStrategyText(true);
            WakeAutomaticDriver();
        }

        internal void EnsureLayoutCapacity(int glyphCount)
        {
            if (_layoutPositions == null || _layoutPositions.Length < glyphCount)
            {
                _layoutPositions = new Vector3[TextStudioBufferCapacity.ForCount(glyphCount)];
                _sourceTextNormalizedPositions = new Vector3[_layoutPositions.Length];
                _sourceColors = new TextStudioGlyphColorQuad[_layoutPositions.Length];
                _sourceColorValidity = new bool[_layoutPositions.Length];
                _sourceVisualBounds = new TextStudioGlyphVisualBounds[_layoutPositions.Length];
            }
            else
            {
                _sourceTextNormalizedPositions ??= new Vector3[_layoutPositions.Length];
                _sourceColors ??= new TextStudioGlyphColorQuad[_layoutPositions.Length];
                _sourceColorValidity ??= new bool[_layoutPositions.Length];
                _sourceVisualBounds ??= new TextStudioGlyphVisualBounds[_layoutPositions.Length];
            }
        }

        internal void BeginLayoutCapture(int glyphCount)
        {
            EnsureLayoutCapacity(glyphCount);
            if (_sourceColorValidity != null && glyphCount > 0)
                Array.Clear(_sourceColorValidity, 0, glyphCount);
            _layoutCaptureFingerprint = 17;
        }

        internal void SetLayoutPosition(int glyphIndex, Vector3 position)
        {
            _layoutPositions[glyphIndex] = position;
        }

        internal void SetLayoutSource(
            int glyphIndex,
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Vector3 bottomRight,
            Color32 bottomLeftColor,
            Color32 topLeftColor,
            Color32 topRightColor,
            Color32 bottomRightColor,
            bool isVisible)
        {
            _sourceColors[glyphIndex] = new TextStudioGlyphColorQuad(
                bottomLeftColor,
                topLeftColor,
                topRightColor,
                bottomRightColor);
            _sourceColorValidity[glyphIndex] = true;
            Vector2 minimum = new Vector2(bottomLeft.x, bottomLeft.y);
            Vector2 maximum = minimum;
            minimum = Vector2.Min(minimum, new Vector2(topLeft.x, topLeft.y));
            minimum = Vector2.Min(minimum, new Vector2(topRight.x, topRight.y));
            minimum = Vector2.Min(minimum, new Vector2(bottomRight.x, bottomRight.y));
            maximum = Vector2.Max(maximum, new Vector2(topLeft.x, topLeft.y));
            maximum = Vector2.Max(maximum, new Vector2(topRight.x, topRight.y));
            maximum = Vector2.Max(maximum, new Vector2(bottomRight.x, bottomRight.y));
            _sourceVisualBounds[glyphIndex] = new TextStudioGlyphVisualBounds(
                minimum,
                maximum,
                isVisible);

            unchecked
            {
                _layoutCaptureFingerprint = _layoutCaptureFingerprint * 31 + glyphIndex;
                _layoutCaptureFingerprint = _layoutCaptureFingerprint * 31 + bottomLeft.GetHashCode();
                _layoutCaptureFingerprint = _layoutCaptureFingerprint * 31 + topRight.GetHashCode();
                _layoutCaptureFingerprint = _layoutCaptureFingerprint * 31 + bottomLeftColor.GetHashCode();
                _layoutCaptureFingerprint = _layoutCaptureFingerprint * 31 + topRightColor.GetHashCode();
            }
        }

        internal void CompleteLayoutCapture(int glyphCount)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null)
                return;

            TextStudioSourceContextRequirements requirements = plan.SourceContextRequirements;
            if ((requirements & TextStudioSourceContextRequirements.SourceTextNormalizedPosition) != 0)
                RebuildNormalizedSourcePositions(glyphCount);

            ApplySpatialContext(glyphCount);
            bool changed = !_hasLayoutCaptureFingerprint ||
                           _lastLayoutCaptureFingerprint != _layoutCaptureFingerprint;
            _lastLayoutCaptureFingerprint = _layoutCaptureFingerprint;
            _hasLayoutCaptureFingerprint = true;
            if (changed && _runtime.Reveal.HasSpatialSequence)
            {
                bool restoreScrub = _preview != null && _preview.IsScrubbing;
                _runtime.Reveal.SetSequenceLayout(_layoutPositions, glyphCount, true, !restoreScrub);
                if (restoreScrub) _preview.ApplyScrubProgress();
            }
            if (!changed || requirements == TextStudioSourceContextRequirements.None)
                return;

            _runtime.SetDirty(DirtyFlags.Animation);
            WakeAutomaticDriver();
        }

        void RebuildNormalizedSourcePositions(int glyphCount)
        {
            bool hasVisible = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            int count = Mathf.Min(glyphCount, _sourceVisualBounds.Length);
            for (int i = 0; i < count; i++)
            {
                TextStudioGlyphVisualBounds bounds = _sourceVisualBounds[i];
                if (!bounds.IsValid || !bounds.IsVisible)
                    continue;
                minimum = Vector2.Min(minimum, bounds.Minimum);
                maximum = Vector2.Max(maximum, bounds.Maximum);
                hasVisible = true;
            }
            if (!hasVisible)
            {
                for (int i = 0; i < count; i++)
                {
                    TextStudioGlyphVisualBounds bounds = _sourceVisualBounds[i];
                    if (!bounds.IsValid)
                        continue;
                    minimum = Vector2.Min(minimum, bounds.Minimum);
                    maximum = Vector2.Max(maximum, bounds.Maximum);
                    hasVisible = true;
                }
            }
            if (!hasVisible)
            {
                minimum = Vector2.zero;
                maximum = Vector2.zero;
            }

            Vector2 size = maximum - minimum;
            bool hasWidth = Mathf.Abs(size.x) > 0.0001f;
            bool hasHeight = Mathf.Abs(size.y) > 0.0001f;
            for (int i = 0; i < count; i++)
            {
                Vector2 center = _sourceVisualBounds[i].IsValid
                    ? _sourceVisualBounds[i].Center
                    : (Vector2)_layoutPositions[i];
                _sourceTextNormalizedPositions[i] = new Vector3(
                    hasWidth ? Mathf.Clamp01((center.x - minimum.x) / size.x) : 0.5f,
                    hasHeight ? Mathf.Clamp01((center.y - minimum.y) / size.y) : 0.5f,
                    0f);
            }
        }

        void ApplySpatialContext(int glyphCount)
        {
            Matrix4x4 localToWorld = _element != null
                ? _element.worldTransform
                : Matrix4x4.identity;
            _runtime.Animator.SetSpatialContext(
                _layoutPositions,
                _sourceTextNormalizedPositions,
                _sourceColors,
                _sourceColorValidity,
                glyphCount,
                _placementEngine.Placements,
                _placementEngine.Count,
                _placementMode,
                localToWorld,
                false,
                Vector3.zero,
                Vector3.zero,
                default,
                _sourceVisualBounds,
                _layoutCaptureFingerprint);
        }

        internal void ComputePlacement(int glyphCount)
        {
            ApplyPlacementStrategy();

            Vector3 anchor = Vector3.zero;
            Vector3[] layoutPositions = _layoutPositions;
            bool usePlacementSpaceConversion = _placementMode != PlacementMode.Linear;
            if (usePlacementSpaceConversion && TryBuildPlacementLayoutPositions(glyphCount, out anchor))
                layoutPositions = _placementLayoutPositions;

            TextStudioCompiledPlan plan = _runtime.Plan;
            if (_placementMode == PlacementMode.Curve)
                _curvePlacement.PrepareSelection(plan, glyphCount);
            LineRange[] placementLines = plan != null ? plan.LineRanges : null;
            int placementLineCount = plan != null ? plan.LineCount : 0;
            Vector2 sourceAlignment = _element != null
                ? ResolveSourceAlignment(_element.resolvedStyle.unityTextAlign)
                : new Vector2(0.5f, 0.5f);
            _placementEngine.ComputePlacements(
                layoutPositions,
                glyphCount,
                0f,
                null,
                placementLines,
                placementLineCount,
                true,
                sourceAlignment,
                evaluationTime: TextStudioMotionSettings.Resolve(_motionPolicy) == TextStudioMotionPolicy.Full
                    ? _presentationTime
                    : 0f,
                sourceVisualBounds: usePlacementSpaceConversion
                    ? _placementSourceVisualBounds
                    : _sourceVisualBounds);
            if (usePlacementSpaceConversion)
                ConvertPlacementsToElementSpace(anchor, glyphCount);

            _placementEngine.ApplyOrientation(_placementRotation);
            InterpolateLayout(glyphCount);
        }

        static Vector2 ResolveSourceAlignment(TextAnchor alignment)
        {
            float horizontal = alignment switch
            {
                TextAnchor.LowerCenter => 0.5f,
                TextAnchor.MiddleCenter => 0.5f,
                TextAnchor.UpperCenter => 0.5f,
                TextAnchor.LowerRight => 1f,
                TextAnchor.MiddleRight => 1f,
                TextAnchor.UpperRight => 1f,
                _ => 0f
            };
            float vertical = alignment switch
            {
                TextAnchor.UpperLeft => 1f,
                TextAnchor.UpperCenter => 1f,
                TextAnchor.UpperRight => 1f,
                TextAnchor.MiddleLeft => 0.5f,
                TextAnchor.MiddleCenter => 0.5f,
                TextAnchor.MiddleRight => 0.5f,
                _ => 0f
            };
            return new Vector2(horizontal, vertical);
        }

        bool TryBuildPlacementLayoutPositions(int glyphCount, out Vector3 anchor)
        {
            anchor = Vector3.zero;
            if (_layoutPositions == null || glyphCount <= 0)
                return false;

            int count = Mathf.Min(glyphCount, _layoutPositions.Length);
            if (count <= 0)
                return false;

            if (_placementLayoutPositions == null || _placementLayoutPositions.Length < glyphCount)
            {
                _placementLayoutPositions = new Vector3[TextStudioBufferCapacity.ForCount(glyphCount)];
                _placementSourceVisualBounds =
                    new TextStudioGlyphVisualBounds[_placementLayoutPositions.Length];
            }
            else if (_placementSourceVisualBounds == null ||
                     _placementSourceVisualBounds.Length < _placementLayoutPositions.Length)
            {
                _placementSourceVisualBounds =
                    new TextStudioGlyphVisualBounds[_placementLayoutPositions.Length];
            }

            anchor = GetLayoutCenter(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = _layoutPositions[i];
                _placementLayoutPositions[i] = new Vector3(
                    position.x - anchor.x,
                    anchor.y - position.y,
                    position.z - anchor.z);

                TextStudioGlyphVisualBounds sourceBounds =
                    _sourceVisualBounds != null && i < _sourceVisualBounds.Length
                        ? _sourceVisualBounds[i]
                        : default;
                _placementSourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    new Vector2(
                        sourceBounds.Minimum.x - anchor.x,
                        anchor.y - sourceBounds.Maximum.y),
                    new Vector2(
                        sourceBounds.Maximum.x - anchor.x,
                        anchor.y - sourceBounds.Minimum.y),
                    sourceBounds.IsVisible);
            }

            return true;
        }

        Vector3 GetLayoutCenter(int glyphCount)
        {
            int count = Mathf.Min(glyphCount, _layoutPositions.Length);
            Vector3 min = _layoutPositions[0];
            Vector3 max = _layoutPositions[0];

            for (int i = 1; i < count; i++)
            {
                Vector3 position = _layoutPositions[i];
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            return (min + max) * 0.5f;
        }

        void ConvertPlacementsToElementSpace(Vector3 anchor, int glyphCount)
        {
            GlyphPlacement[] placements = _placementEngine.Placements;
            if (placements == null)
                return;

            int count = Mathf.Min(glyphCount, _placementEngine.Count);
            for (int i = 0; i < count; i++)
            {
                GlyphPlacement placement = placements[i];
                placement.position = new Vector3(
                    anchor.x + placement.position.x,
                    anchor.y - placement.position.y,
                    anchor.z + placement.position.z);
                Vector3 tangent = new Vector3(placement.tangent.x, -placement.tangent.y, placement.tangent.z);
                if (tangent.sqrMagnitude > 1e-6f)
                {
                    tangent.Normalize();
                    placement.tangent = tangent;
                    placement.up = Vector3.Cross(Vector3.forward, tangent).normalized;
                }
                if (_placementMode == PlacementMode.Curve && _curvePlacement.IsSelected(i))
                    placement.rotation = Quaternion.AngleAxis(
                        Mathf.Atan2(placement.tangent.y, placement.tangent.x) * Mathf.Rad2Deg,
                        Vector3.forward);
                placements[i] = placement;
            }
        }

        bool ProcessPipeline()
        {
            bool changed = _runtime.ProcessPipeline(
                snapNonInstantRevealToEnd: false,
                renderMarkupLiterally: false,
                _markupSyntaxOverride);
            _preview?.ConfigureRevealActiveBlendForCurrentLifecycleInternal();
            _runtime.ClearProcessedTextAndStyleDirty();
            if (!changed)
                return false;

            return true;
        }

        bool EvaluateRenderedStateIfNeeded(float deltaTime)
        {
            if (_runtime.Plan != null &&
                _layoutPositions != null &&
                _runtime.Plan.VisibleGlyphCount <= _layoutPositions.Length)
            {
                ApplySpatialContext(_runtime.Plan.VisibleGlyphCount);
            }
            bool evaluated = _runtime.EvaluateIfNeeded(deltaTime);
            return evaluated && _runtime.LastEvaluationChangedRenderedState;
        }

        bool UpdatePresentation(float deltaTime)
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null)
                return false;

            int glyphCount = plan.VisibleGlyphCount;
            if (glyphCount <= 0 || _presentationGlyphs == null || _presentationGlyphCount <= 0)
            {
                if (!string.Equals(_visibleText, _displayText, StringComparison.Ordinal))
                    _visibleText = _displayText;
                return false;
            }

            bool changed = _characterSubstitutionPresenter.Update(
                plan,
                _presentationGlyphs,
                glyphCount,
                deltaTime,
                _presentationTime,
                _presentationTimeDiscontinuous,
                _runtime.Reveal,
                _runtime.MotionPolicy);
            _presentationTimeDiscontinuous = false;
            if (!changed)
                return false;

            _visibleText = BuildVisibleText(plan, _displayText, _presentationGlyphs, glyphCount);
            _runtime.SetDirty(DirtyFlags.Visual);
            return true;
        }

        void PollExternalEffectSources()
        {
            TextStudioCompiledPlan plan = _runtime.Plan;
            if (plan == null || _externalEffectSources.Count == 0)
                return;

            TextStudioExternalEffectSubmissionContext context =
                new TextStudioExternalEffectSubmissionContext(_runtime, Effects, plan);
            for (int i = 0; i < _externalEffectSources.Count; i++)
            {
                ITextStudioExternalEffectSource source = _externalEffectSources[i];
                if (source == null ||
                    (source is UnityEngine.Object unityObject && unityObject == null))
                {
                    _externalEffectSources.RemoveAt(i);
                    i--;
                    continue;
                }
                if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    continue;

                try
                {
                    if (source.IsActive)
                        source.Submit(in context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        bool HasActiveExternalEffectSources()
        {
            for (int i = _externalEffectSources.Count - 1; i >= 0; i--)
            {
                ITextStudioExternalEffectSource source = _externalEffectSources[i];
                if (source == null ||
                    (source is UnityEngine.Object unityObject && unityObject == null))
                {
                    _externalEffectSources.RemoveAt(i);
                    continue;
                }
                if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    continue;

                try
                {
                    if (source.IsActive)
                        return true;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            return false;
        }

        string BuildVisibleText(
            TextStudioCompiledPlan plan,
            string displayText,
            PresentationGlyph[] presentationGlyphs,
            int glyphCount)
        {
            if (plan == null ||
                string.IsNullOrEmpty(displayText) ||
                presentationGlyphs == null ||
                glyphCount <= 0)
            {
                return displayText ?? string.Empty;
            }

            _visibleTextBuilder.Length = 0;
            int resolvedIndex = 0;
            for (int i = 0; i < displayText.Length; i++)
            {
                char c = displayText[i];
                if (c == '<')
                {
                    int tagEnd = displayText.IndexOf('>', i + 1);
                    if (tagEnd > i)
                    {
                        _visibleTextBuilder.Append(displayText, i, tagEnd - i + 1);
                        i = tagEnd;
                        continue;
                    }
                }

                _visibleTextBuilder.Append(ResolvePresentationCharacter(
                    plan,
                    presentationGlyphs,
                    glyphCount,
                    resolvedIndex,
                    c));
                resolvedIndex++;
            }

            return _visibleTextBuilder.ToString();
        }

        static char ResolvePresentationCharacter(
            TextStudioCompiledPlan plan,
            PresentationGlyph[] presentationGlyphs,
            int glyphCount,
            int resolvedIndex,
            char fallback)
        {
            if (plan == null ||
                presentationGlyphs == null ||
                resolvedIndex < 0 ||
                resolvedIndex >= (plan.ResolvedText != null ? plan.ResolvedText.Length : 0))
            {
                return fallback;
            }

            int unitIndex = plan.GetUnitIndexForResolvedChar(resolvedIndex);
            if (unitIndex < 0 ||
                unitIndex >= glyphCount ||
                unitIndex >= presentationGlyphs.Length ||
                plan.GetResolvedLength(unitIndex) != 1)
            {
                return fallback;
            }

            PresentationGlyph glyph = presentationGlyphs[unitIndex];
            if (!glyph.HasFlag(PresentationFlags.Substituted) || glyph.glyphId <= 0 || glyph.glyphId > char.MaxValue)
                return fallback;

            return (char)glyph.glyphId;
        }

        void HandleRuntimePlanChanged(TextStudioCompiledPlan plan)
        {
            if (plan == null)
            {
                _displayText = string.Empty;
                _resolvedText = string.Empty;
                _visibleText = string.Empty;
                _presentationGlyphCount = 0;
                _characterSubstitutionPresenter.OnTextChanged(null);
                _presentationTime = 0f;
                _presentationTimeDiscontinuous = true;
                _revealPrefixes = s_EmptyRevealPrefixes;
                return;
            }

            _resolvedText = plan.ResolvedText ?? string.Empty;
            _displayText = plan.DisplayText ?? _resolvedText;
            _visibleText = _displayText;
            InitializePresentationGlyphs(plan);
            _presentationTime = 0f;
            _presentationTimeDiscontinuous = true;
            _revealPrefixes = s_EmptyRevealPrefixes;
        }

        void InitializePresentationGlyphs(TextStudioCompiledPlan plan)
        {
            int glyphCount = plan != null ? plan.VisibleGlyphCount : 0;
            if (glyphCount <= 0 || plan.CharacterSubstitutionBindingCount <= 0)
            {
                _presentationGlyphs = null;
                _presentationGlyphCount = 0;
                _characterSubstitutionPresenter.OnTextChanged(plan);
                return;
            }

            _presentationGlyphCount = glyphCount;
            if (_presentationGlyphs == null || _presentationGlyphs.Length < glyphCount)
                _presentationGlyphs = new PresentationGlyph[TextStudioBufferCapacity.ForCount(glyphCount)];

            for (int i = 0; i < glyphCount; i++)
                _presentationGlyphs[i] = PresentationGlyph.Default(plan.GetPrimaryChar(i), i);

            _characterSubstitutionPresenter.OnTextChanged(plan);
        }

        void BuildRevealPrefixCache()
        {
            int length = _resolvedText != null ? _resolvedText.Length : 0;
            if (length == 0)
            {
                _revealPrefixes = s_EmptyRevealPrefixes;
                return;
            }

            if (_revealPrefixes == null || _revealPrefixes.Length != length + 1)
                _revealPrefixes = new string[length + 1];

            _revealPrefixes[0] = string.Empty;
            for (int i = 1; i < length; i++)
                _revealPrefixes[i] = _resolvedText.Substring(0, i);
            _revealPrefixes[length] = _resolvedText;
        }

        void ApplyPlacementStrategy()
        {
            switch (_placementMode)
            {
                case PlacementMode.Arc:
                    _placementEngine.SetStrategy(_arcPlacement);
                    break;
                case PlacementMode.Circle:
                    _placementEngine.SetStrategy(_circlePlacement);
                    break;
                case PlacementMode.Wave:
                    _placementEngine.SetStrategy(_wavePlacement);
                    break;
                case PlacementMode.Grid:
                    _placementEngine.SetStrategy(_gridPlacement);
                    break;
                case PlacementMode.Curve:
                    _placementEngine.SetStrategy(_curvePlacement);
                    break;
                case PlacementMode.Linear:
                default:
                    _placementEngine.SetStrategy(null);
                    break;
            }
        }

        internal void RefreshPreviewScheduling()
        {
            if (RequiresAutomaticUpdate())
                WakeAutomaticDriver();
            else
                PauseAutomaticDriver();
        }

        bool ITextStudioPreviewHost.LoopReveal => LoopReveal;
        TextStudioTypewriterSource ITextStudioPreviewHost.TypewriterSource =>
            UsesBehaviorProfileTypewriter
                ? _appliedTypewriterSource == TextStudioBehaviorValueSource.BehaviorProfile
                    ? _behaviorProfile.TypewriterSource
                    : _typewriterSource
                : _typewriterSource;
        RevealMode ITextStudioPreviewHost.RevealMode => RevealMode;
        float ITextStudioPreviewHost.RevealProgress => RevealProgress;
        int ITextStudioPreviewHost.RevealedGlyphCount => RevealedGlyphCount;
        bool ITextStudioPreviewHost.HasActiveRevealAppearance => _runtime.HasActiveRevealAppearance;
        bool ITextStudioPreviewHost.HasCueTimeline => _runtime.HasCueTimeline;
        bool ITextStudioPreviewHost.HasActiveReveal => _runtime.Reveal.HasActiveReveal;
        float ITextStudioPreviewHost.DisappearProgressInternal => _runtime.DisappearProgress;
        float ITextStudioPreviewHost.GetApproximateShowDuration() =>
            _runtime.GetApproximateShowDuration();
        float ITextStudioPreviewHost.GetApproximateVisualShowDuration() =>
            _runtime.GetApproximateVisualShowDuration();
        float ITextStudioPreviewHost.GetApproximateHideDuration() =>
            _runtime.GetApproximateHideDuration();

        void ITextStudioPreviewHost.StartRevealInternal(bool resetProgress)
        {
            if (_runtime.Plan == null)
                ProcessPipeline();
            if (_runtime.Plan == null)
                return;

            _runtime.StartReveal(resetProgress);
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            if (resetProgress)
                _presentationTime = 0f;
            _presentationTimeDiscontinuous = true;
            ApplyImmediateRuntimeState();
        }

        void ITextStudioPreviewHost.StartDisappear() => StartDisappear();
        void ITextStudioPreviewHost.StopDisappear() => StopDisappear();

        void ITextStudioPreviewHost.ResetInternal()
        {
            ClearPendingTextChange();
            _runtime.ResetReveal();
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            _presentationTime = 0f;
            _presentationTimeDiscontinuous = true;
            ApplyImmediateRuntimeState();
        }

        void ITextStudioPreviewHost.SetPreviewRevealTimeInternal(float elapsedTime)
        {
            _presentationTimeDiscontinuous |= _preview != null && _preview.IsScrubbing;
            _runtime.SetPreviewRevealTime(elapsedTime);
        }

        void ITextStudioPreviewHost.SetPreviewEffectTimeInternal(float elapsedTime)
        {
            _presentationTime = Mathf.Max(0f, elapsedTime);
            _presentationTimeDiscontinuous |= _preview != null && _preview.IsScrubbing;
            _runtime.SetPreviewEffectTime(elapsedTime);
        }

        void ITextStudioPreviewHost.SetPreviewRevealActiveBlendEnabledInternal(bool enabled)
        {
            _runtime.SetPreviewRevealActiveBlendEnabled(enabled);
        }

        void ITextStudioPreviewHost.SetDisappearProgressInternal(float progress)
        {
            _runtime.SetDisappearProgress(progress);
        }

        void ITextStudioPreviewHost.PreparePreviewProgressInternal()
        {
            _runtime.PrepareRevealForProgress();
        }

        void ITextStudioPreviewHost.FlushPreviewVisualStateInternal()
        {
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            ApplyImmediateRuntimeState();
        }

        void ITextStudioPreviewHost.SetPreviewRevealLoopSuppressedInternal(bool suppressed)
        {
            _runtime.SetPreviewRevealLoopSuppressed(suppressed);
        }

        void ITextStudioPreviewHost.SnapToEndStateInternal()
        {
            _runtime.SnapToEndState();
            ApplyImmediateRuntimeState();
        }

        void ITextStudioPreviewHost.RestartPreviewInvocationInternal()
        {
            ClearPendingTextChange();
            _runtime.RestartPreviewInvocation();
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            _presentationTime = 0f;
            _presentationTimeDiscontinuous = true;
            ApplyImmediateRuntimeState();
        }

        void ITextStudioPreviewHost.CancelPreviewInvocationInternal()
        {
            _runtime.CancelPreviewInvocation();
        }

        // Bindings never park outside a scheduler loop.
        void ITextStudioPreviewHost.PreviewTookPlaybackInternal()
        {
        }
    }

    internal sealed class TextStudioUITKTargetDriver
    {
        readonly TextStudioBinding _binding;
        readonly Action<float> _scheduledUpdate;
        readonly TextStudioUITKTargetStateLease _stateLease =
            new TextStudioUITKTargetStateLease();

        IVisualElementScheduledItem _scheduledUpdateLoop;
        bool _strategyAttached;
        bool _ownsTextWrite;
        string _lastOwnedText = string.Empty;
        int _lastFrameCount = -1;
        double _lastAutomaticUpdateTime;

        public TextElement Element { get; private set; }
        public ITextStudioUITKRenderStrategy Strategy { get; }
        public bool IsPanelAttached { get; private set; }
        public bool OwnsTextWrite => _ownsTextWrite;

        public bool IsOwnedTextValue(string value)
        {
            return string.Equals(_lastOwnedText, value ?? string.Empty, StringComparison.Ordinal);
        }
        public bool AutomaticDriverActive =>
            _scheduledUpdateLoop != null && _scheduledUpdateLoop.isActive;
        public int AutomaticUpdateCount { get; private set; }

        public TextStudioUITKTargetDriver(
            TextStudioBinding binding,
            Action<float> scheduledUpdate)
        {
            _binding = binding;
            _scheduledUpdate = scheduledUpdate;
#if UNITY_6000_2_OR_NEWER
            Strategy = new TextStudioUITKPostProcessStrategy();
#else
            Strategy = new TextStudioUITKBasicStrategy();
#endif
        }

        public void Attach(
            TextElement element,
            TextStudioUITKTextGeneratorPreference textGeneratorPreference)
        {
            if (ReferenceEquals(Element, element))
            {
                ApplyTextGeneratorPreference(textGeneratorPreference);
                RefreshAttachment();
                return;
            }

            if (Element != null)
                Detach(Element.text);

            Element = element;
            if (Element == null)
                return;

            _stateLease.Capture(Element);
            Element.RegisterCallback<AttachToPanelEvent>(HandleAttachToPanel);
            Element.RegisterCallback<DetachFromPanelEvent>(HandleDetachFromPanel);
            Element.RegisterCallback<ChangeEvent<string>>(_binding.HandleTargetTextChanged);
            WriteRichText(true);
            ApplyTextGeneratorPreference(textGeneratorPreference);
            EnsureStrategyAttached();
            RefreshAttachment();
        }

        public void RefreshAttachment()
        {
            if (Element == null)
                return;

            EnsureStrategyAttached();
            if (Element.panel != null)
                HandleAttachToPanel(null);
        }

        public void Detach(string restoredText)
        {
            PauseAutomaticDriver();
            _scheduledUpdateLoop = null;
            if (Element != null)
            {
                Element.UnregisterCallback<AttachToPanelEvent>(HandleAttachToPanel);
                Element.UnregisterCallback<DetachFromPanelEvent>(HandleDetachFromPanel);
                Element.UnregisterCallback<ChangeEvent<string>>(_binding.HandleTargetTextChanged);
            }

            if (_strategyAttached)
                Strategy.Detach();

            if (Element != null)
                _stateLease.Restore(Element, restoredText);

            _strategyAttached = false;
            IsPanelAttached = false;
            _ownsTextWrite = false;
            _lastFrameCount = -1;
            Element = null;
        }

        public bool WriteText(string value)
        {
            if (Element == null)
                return false;

            value ??= string.Empty;
            if (string.Equals(Element.text, value, StringComparison.Ordinal))
                return false;

            _ownsTextWrite = true;
            try
            {
                _lastOwnedText = value;
                Element.text = value;
                _stateLease.RecordText(value);
            }
            finally
            {
                _ownsTextWrite = false;
            }

            return true;
        }

        public void SetRawAuthoringView(bool raw, string markup)
        {
            if (Element == null)
                return;

            WriteRichText(!raw);
            if (raw)
                WriteText(markup ?? string.Empty);
        }

        public void ApplyTextGeneratorPreference(
            TextStudioUITKTextGeneratorPreference preference)
        {
#if UNITY_6000_2_OR_NEWER
            if (Element == null)
                return;

            StyleEnum<TextGeneratorType> nextValue;
            switch (preference)
            {
                case TextStudioUITKTextGeneratorPreference.PreferAdvanced:
                    nextValue = new StyleEnum<TextGeneratorType>(
                        TextStudioUITKCapabilities.GetAdvancedTextAvailability(Element.panel) == false
                            ? TextGeneratorType.Standard
                            : TextGeneratorType.Advanced);
                    break;
                case TextStudioUITKTextGeneratorPreference.Standard:
                    nextValue = new StyleEnum<TextGeneratorType>(TextGeneratorType.Standard);
                    break;
                case TextStudioUITKTextGeneratorPreference.ProjectDefault:
                default:
                    nextValue = new StyleEnum<TextGeneratorType>(StyleKeyword.Null);
                    break;
            }

            if (!Element.style.unityTextGenerator.Equals(nextValue))
            {
                Element.style.unityTextGenerator = nextValue;
                _stateLease.RecordTextGenerator(Element.style.unityTextGenerator);
            }
#endif
        }

        public void WakeAutomaticDriver(
            bool automatic,
            bool editModeAllowed,
            bool requiresUpdate)
        {
            if (!automatic ||
                !IsPanelAttached ||
                !editModeAllowed ||
                !requiresUpdate ||
                Element == null)
            {
                return;
            }

            if (_scheduledUpdateLoop == null)
            {
                _scheduledUpdateLoop = Element.schedule.Execute(ScheduledAutomaticUpdate).Every(1);
                _scheduledUpdateLoop.Pause();
            }

            _lastAutomaticUpdateTime = Time.realtimeSinceStartupAsDouble;
            _scheduledUpdateLoop.Resume();
        }

        public void PauseAutomaticDriver()
        {
            _scheduledUpdateLoop?.Pause();
        }

        public bool TryBeginFrame(int frame)
        {
            if (frame == _lastFrameCount)
                return false;

            _lastFrameCount = frame;
            return true;
        }

        void WriteRichText(bool value)
        {
            if (Element == null || Element.enableRichText == value)
                return;

            Element.enableRichText = value;
            _stateLease.RecordRichText(value);
        }

        void EnsureStrategyAttached()
        {
            if (Element == null || _strategyAttached)
                return;

            Strategy.Attach(Element, _binding);
            _strategyAttached = true;
        }

        void HandleAttachToPanel(AttachToPanelEvent evt)
        {
            if (Element == null || IsPanelAttached)
                return;

            IsPanelAttached = true;
            _lastFrameCount = -1;
            _lastAutomaticUpdateTime = Time.realtimeSinceStartupAsDouble;
            EnsureStrategyAttached();
            _binding.HandleTargetAttachedToPanel();
        }

        void HandleDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (!IsPanelAttached)
                return;

            PauseAutomaticDriver();
            _scheduledUpdateLoop = null;
            IsPanelAttached = false;
            _lastFrameCount = -1;
            if (_strategyAttached)
                Strategy.Detach();
            _strategyAttached = false;
            _binding.HandleTargetDetachedFromPanel();
        }

        void ScheduledAutomaticUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float deltaTime = Application.isPlaying
                ? Mathf.Max(0f, Time.deltaTime)
                : Mathf.Clamp((float)(now - _lastAutomaticUpdateTime), 0f, 0.1f);
            _lastAutomaticUpdateTime = now;
            AutomaticUpdateCount++;
            _scheduledUpdate?.Invoke(deltaTime);
        }
    }

    internal sealed class TextStudioUITKTargetStateLease
    {
        bool _isCaptured;
        string _originalText;
        string _lastWrittenText;
        bool _hasWrittenText;
        bool _originalRichText;
        bool _lastWrittenRichText;
        bool _hasWrittenRichText;
#if UNITY_6000_2_OR_NEWER
        StyleEnum<TextGeneratorType> _originalTextGenerator;
        StyleEnum<TextGeneratorType> _lastWrittenTextGenerator;
        bool _hasWrittenTextGenerator;
#endif

        public void Capture(TextElement element)
        {
            if (element == null)
            {
                Reset();
                return;
            }

            _isCaptured = true;
            _originalText = element.text ?? string.Empty;
            _originalRichText = element.enableRichText;
            _hasWrittenText = false;
            _hasWrittenRichText = false;
#if UNITY_6000_2_OR_NEWER
            _originalTextGenerator = element.style.unityTextGenerator;
            _hasWrittenTextGenerator = false;
#endif
        }

        public void RecordText(string value)
        {
            if (!_isCaptured)
                return;
            _lastWrittenText = value ?? string.Empty;
            _hasWrittenText = true;
        }

        public void RecordRichText(bool value)
        {
            if (!_isCaptured)
                return;
            _lastWrittenRichText = value;
            _hasWrittenRichText = true;
        }

#if UNITY_6000_2_OR_NEWER
        public void RecordTextGenerator(StyleEnum<TextGeneratorType> value)
        {
            if (!_isCaptured)
                return;
            _lastWrittenTextGenerator = value;
            _hasWrittenTextGenerator = true;
        }
#endif

        public void Restore(TextElement element, string restoredText)
        {
            if (!_isCaptured || element == null)
            {
                Reset();
                return;
            }

            if (_hasWrittenText &&
                string.Equals(element.text, _lastWrittenText, StringComparison.Ordinal))
            {
                element.text = restoredText ?? _originalText ?? string.Empty;
            }

            if (_hasWrittenRichText && element.enableRichText == _lastWrittenRichText)
                element.enableRichText = _originalRichText;

#if UNITY_6000_2_OR_NEWER
            if (_hasWrittenTextGenerator &&
                element.style.unityTextGenerator.Equals(_lastWrittenTextGenerator))
            {
                element.style.unityTextGenerator = _originalTextGenerator;
            }
#endif

            Reset();
        }

        void Reset()
        {
            _isCaptured = false;
            _originalText = null;
            _lastWrittenText = null;
            _hasWrittenText = false;
            _hasWrittenRichText = false;
#if UNITY_6000_2_OR_NEWER
            _hasWrittenTextGenerator = false;
#endif
        }
    }

    internal interface ITextStudioUITKRenderStrategy
    {
        TextStudioUITKFeatureSet FeatureSet { get; }
        int RepaintRequestCount { get; }
        void Attach(TextElement element, TextStudioBinding binding);
        void Detach();
        void ApplyText(bool visualChanged);
    }

    internal sealed class TextStudioUITKBasicStrategy : ITextStudioUITKRenderStrategy
    {
        TextElement _element;
        TextStudioBinding _binding;
        int _repaintRequestCount;

        public TextStudioUITKFeatureSet FeatureSet => TextStudioUITKFeatureSet.Basic;
        public int RepaintRequestCount => _repaintRequestCount;

        public void Attach(TextElement element, TextStudioBinding binding)
        {
            _element = element;
            _binding = binding;
        }

        public void Detach()
        {
            _element = null;
            _binding = null;
        }

        public void ApplyText(bool visualChanged)
        {
            if (_element == null || _binding == null)
                return;

            string target = _binding.GetBasicVisibleText();
            bool textChanged = _binding.WriteTargetText(target);
            if (!textChanged && visualChanged)
            {
                _element.MarkDirtyRepaint();
                _repaintRequestCount++;
            }
        }
    }

#if UNITY_6000_2_OR_NEWER
    internal sealed class TextStudioUITKPostProcessStrategy : ITextStudioUITKRenderStrategy
    {
        TextElement _element;
        TextStudioBinding _binding;
        int _repaintRequestCount;
        // The glyph mapping mode depends only on these inputs, and resolving it scans the whole text on every repaint.
        string _mappingModeText;
        int _mappingModeStateCount = -1;
        int _mappingModeQuadCount = -1;
        TextStudioGlyphIndexMappingMode _mappingMode;

        public TextStudioUITKFeatureSet FeatureSet => TextStudioUITKFeatureSet.FullParity;
        public int RepaintRequestCount => _repaintRequestCount;

        public void Attach(TextElement element, TextStudioBinding binding)
        {
            _element = element;
            _binding = binding;
            _element.PostProcessTextVertices += OnPostProcessTextVertices;
        }

        public void Detach()
        {
            if (_element != null)
                _element.PostProcessTextVertices -= OnPostProcessTextVertices;
            _element = null;
            _binding = null;
            _mappingModeText = null;
            _mappingModeStateCount = -1;
            _mappingModeQuadCount = -1;
        }

        public void ApplyText(bool visualChanged)
        {
            if (_element == null || _binding == null)
                return;

            string target = _binding.VisibleText;
            bool textChanged = _binding.WriteTargetText(target);
            if (!textChanged && visualChanged)
            {
                _element.MarkDirtyRepaint();
                _repaintRequestCount++;
            }
        }

        void OnPostProcessTextVertices(TextElement.GlyphsEnumerable glyphs)
        {
            if (_element == null || _binding == null)
                return;
            if (!_binding.RequiresGlyphPostProcess)
            {
                _binding.CapturePresentedReactionGlyphs(glyphs);
                return;
            }

            using TextStudioPerformanceDiagnostics.MeasurementScope measurement =
                TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Dispatch);
            EvaluatedGlyphState[] states = _binding.UnsafeEvaluatedGlyphStates;
            int stateCount = _binding.StateCount;
            if (states == null || stateCount == 0)
            {
                _binding.CapturePresentedReactionGlyphs(glyphs);
                return;
            }

            TextStudioPlanView plan = _binding.Plan;
            string resolvedText = _binding.ResolvedText;
            int renderedQuadCount = CountRenderedQuads(glyphs);
            TextStudioGlyphIndexMappingMode mappingMode = ResolveMappingMode(
                resolvedText,
                stateCount,
                renderedQuadCount);
            float effectUnitScale = Mathf.Max(1f, _element.resolvedStyle.fontSize);
            bool usePlacement = _binding.HasPlacementTransform;
            if (usePlacement || _binding.NeedsSourceLayoutContext)
                CaptureLayoutPositions(glyphs, stateCount, mappingMode);
            GlyphPlacement[] placements = usePlacement ? _binding.Placements : null;
            int placementCount = usePlacement ? _binding.PlacementCount : 0;

            int searchIndex = 0;
            bool hasPreviousRenderedGlyph = false;
            Vector3 previousRenderedGlyphCenter = Vector3.zero;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                NativeSlice<Vertex> vertices = glyph.vertices;
                if (vertices.Length < 4)
                {
                    if (!TextStudioGlyphIndexMapper.TryConsumeUnrenderedGlyph(resolvedText, ref searchIndex, stateCount, mappingMode))
                        TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(resolvedText, ref searchIndex, stateCount, mappingMode, out _);
                    continue;
                }

                Vertex v0 = vertices[0];
                Vertex v1 = vertices[1];
                Vertex v2 = vertices[2];
                Vertex v3 = vertices[3];
                Vector3 center = (v0.position + v2.position) * 0.5f;
                bool startsNewVisualLine = IsNewVisualLine(
                    hasPreviousRenderedGlyph,
                    previousRenderedGlyphCenter,
                    center);

                if (!TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(
                        resolvedText,
                        ref searchIndex,
                        resolvedText.Length,
                        mappingMode,
                        startsNewVisualLine,
                        out int resolvedCharIndex))
                {
                    HideGlyph(vertices);
                    continue;
                }

                int stateIndex = plan.IsCreated
                    ? plan.GetUnitIndexForResolvedChar(resolvedCharIndex)
                    : resolvedCharIndex;
                if (stateIndex < 0 || stateIndex >= stateCount || stateIndex >= states.Length)
                {
                    HideGlyph(vertices);
                    continue;
                }

                GlyphPlacement placement = default;
                bool useGlyphPlacement = usePlacement && stateIndex < placementCount;
                if (useGlyphPlacement)
                    placement = placements[stateIndex];

                ref readonly EvaluatedGlyphState state = ref states[stateIndex];
                TextStudioRenderChannels renderChannels = state.renderChannels;
                bool applyPositions = useGlyphPlacement ||
                                      (renderChannels & TextStudioRenderChannels.Vertex) != 0;
                bool applyColors =
                    (renderChannels & TextStudioRenderChannels.VertexColor) != 0;
                if (!applyPositions && !applyColors)
                {
                    previousRenderedGlyphCenter = center;
                    hasPreviousRenderedGlyph = true;
                    continue;
                }

                // Without a pivot or corner offsets the state is already in vertex space, so the transform reads it in place instead of a copy.
                TextStudioGlyphQuadTransform transform;
                TextStudioCornerOffsets cornerOffsets;
                if ((renderChannels &
                     (TextStudioRenderChannels.Pivot |
                      TextStudioRenderChannels.CornerOffsets)) != 0)
                {
                    EvaluatedGlyphState vertexState = ToVertexSpaceState(in state);
                    transform = TextStudioGlyphQuadTransformer.CreateFromQuad(
                        v0.position,
                        v1.position,
                        v2.position,
                        v3.position,
                        center,
                        in vertexState,
                        in placement,
                        useGlyphPlacement,
                        effectUnitScale,
                        1f);
                    cornerOffsets = vertexState.cornerOffsets;
                }
                else
                {
                    transform = TextStudioGlyphQuadTransformer.CreateFromQuad(
                        v0.position,
                        v1.position,
                        v2.position,
                        v3.position,
                        center,
                        in state,
                        in placement,
                        useGlyphPlacement,
                        effectUnitScale,
                        1f);
                    cornerOffsets = state.cornerOffsets;
                }

                if (applyPositions)
                {
                    if (cornerOffsets.HasAny)
                    {
                        v0.position = transform.ApplyPosition(
                            v0.position,
                            cornerOffsets.BottomLeft,
                            effectUnitScale);
                        v1.position = transform.ApplyPosition(
                            v1.position,
                            cornerOffsets.TopLeft,
                            effectUnitScale);
                        v2.position = transform.ApplyPosition(
                            v2.position,
                            cornerOffsets.TopRight,
                            effectUnitScale);
                        v3.position = transform.ApplyPosition(
                            v3.position,
                            cornerOffsets.BottomRight,
                            effectUnitScale);
                    }
                    else
                    {
                        v0.position = transform.ApplyPosition(v0.position);
                        v1.position = transform.ApplyPosition(v1.position);
                        v2.position = transform.ApplyPosition(v2.position);
                        v3.position = transform.ApplyPosition(v3.position);
                    }
                }

                if (applyColors)
                {
                    v0.tint = transform.ApplyColor(v0.tint);
                    v1.tint = transform.ApplyColor(v1.tint);
                    v2.tint = transform.ApplyColor(v2.tint);
                    v3.tint = transform.ApplyColor(v3.tint);
                }

                vertices[0] = v0;
                vertices[1] = v1;
                vertices[2] = v2;
                vertices[3] = v3;

                previousRenderedGlyphCenter = center;
                hasPreviousRenderedGlyph = true;
            }
            _binding.CapturePresentedReactionGlyphs(glyphs);
        }

        void CaptureLayoutPositions(
            TextElement.GlyphsEnumerable glyphs,
            int stateCount,
            TextStudioGlyphIndexMappingMode mappingMode)
        {
            _binding.BeginLayoutCapture(stateCount);
            string resolvedText = _binding.ResolvedText;
            int searchIndex = 0;
            int lastMappedIndex = -1;
            Vector3 lastPosition = Vector3.zero;
            bool hasPreviousRenderedGlyph = false;
            Vector3 previousRenderedGlyphCenter = Vector3.zero;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                NativeSlice<Vertex> vertices = glyph.vertices;
                if (vertices.Length < 4)
                {
                    if (!TextStudioGlyphIndexMapper.TryConsumeUnrenderedGlyph(resolvedText, ref searchIndex, resolvedText.Length, mappingMode))
                        TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(resolvedText, ref searchIndex, resolvedText.Length, mappingMode, out _);
                    continue;
                }

                Vertex v0 = vertices[0];
                Vertex v1 = vertices[1];
                Vertex v2 = vertices[2];
                Vertex v3 = vertices[3];
                Vector3 center = (v0.position + v2.position) * 0.5f;
                bool startsNewVisualLine = IsNewVisualLine(
                    hasPreviousRenderedGlyph,
                    previousRenderedGlyphCenter,
                    center);

                if (!TextStudioGlyphIndexMapper.TryGetNextGlyphIndex(
                        resolvedText,
                        ref searchIndex,
                        resolvedText.Length,
                        mappingMode,
                        startsNewVisualLine,
                        out int resolvedCharIndex))
                {
                    continue;
                }

                int stateIndex = _binding.Plan.IsCreated
                    ? _binding.Plan.GetUnitIndexForResolvedChar(resolvedCharIndex)
                    : resolvedCharIndex;
                if (stateIndex < 0 || stateIndex >= stateCount)
                    continue;

                for (int i = lastMappedIndex + 1; i < stateIndex; i++)
                    _binding.SetLayoutPosition(i, center);

                _binding.SetLayoutPosition(stateIndex, center);
                bool isVisible = resolvedCharIndex >= 0 &&
                                 resolvedCharIndex < resolvedText.Length &&
                                 !char.IsWhiteSpace(resolvedText[resolvedCharIndex]);
                _binding.SetLayoutSource(
                    stateIndex,
                    v0.position,
                    v1.position,
                    v2.position,
                    v3.position,
                    v0.tint,
                    v1.tint,
                    v2.tint,
                    v3.tint,
                    isVisible);
                lastMappedIndex = stateIndex;
                lastPosition = center;
                previousRenderedGlyphCenter = center;
                hasPreviousRenderedGlyph = true;
            }

            for (int i = lastMappedIndex + 1; i < stateCount; i++)
                _binding.SetLayoutPosition(i, lastPosition);

            _binding.ComputePlacement(stateCount);
            _binding.CompleteLayoutCapture(stateCount);
        }

        static bool IsNewVisualLine(
            bool hasPreviousRenderedGlyph,
            Vector3 previousRenderedGlyphCenter,
            Vector3 currentRenderedGlyphCenter)
        {
            const float horizontalResetTolerance = 0.5f;
            const float verticalLineTolerance = 0.5f;

            return hasPreviousRenderedGlyph &&
                   currentRenderedGlyphCenter.x + horizontalResetTolerance < previousRenderedGlyphCenter.x &&
                   Mathf.Abs(currentRenderedGlyphCenter.y - previousRenderedGlyphCenter.y) > verticalLineTolerance;
        }

        internal static EvaluatedGlyphState ToVertexSpaceState(in EvaluatedGlyphState state)
        {
            if (state.pivotOffset.sqrMagnitude <= 0.000001f &&
                state.cornerOffsets.BottomLeft.sqrMagnitude <= 0.000001f &&
                state.cornerOffsets.TopLeft.sqrMagnitude <= 0.000001f &&
                state.cornerOffsets.TopRight.sqrMagnitude <= 0.000001f &&
                state.cornerOffsets.BottomRight.sqrMagnitude <= 0.000001f)
                return state;

            EvaluatedGlyphState vertexState = state;
            Vector2 pivot = vertexState.Pivot;
            pivot.y = 1f - pivot.y;
            vertexState.Pivot = pivot;
            vertexState.cornerOffsets = vertexState.cornerOffsets.FlipVerticalAxis();
            return vertexState;
        }

        TextStudioGlyphIndexMappingMode ResolveMappingMode(
            string resolvedText,
            int stateCount,
            int renderedQuadCount)
        {
            // Strings are immutable, so the same text reference with the same counts resolves the same mode.
            if (!ReferenceEquals(_mappingModeText, resolvedText) ||
                _mappingModeStateCount != stateCount ||
                _mappingModeQuadCount != renderedQuadCount)
            {
                _mappingModeText = resolvedText;
                _mappingModeStateCount = stateCount;
                _mappingModeQuadCount = renderedQuadCount;
                _mappingMode = TextStudioGlyphIndexMapper.ResolveMappingMode(
                    resolvedText,
                    stateCount,
                    renderedQuadCount);
            }

            return _mappingMode;
        }

        static int CountRenderedQuads(TextElement.GlyphsEnumerable glyphs)
        {
            int count = 0;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                if (glyph.vertices.Length >= 4)
                    count++;
            }

            return count;
        }

        static void HideGlyph(NativeSlice<Vertex> vertices)
        {
            int count = Mathf.Min(vertices.Length, 4);
            for (int i = 0; i < count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.tint.a = 0;
                vertices[i] = vertex;
            }
        }
    }
#endif

    /// <summary>Attaches, retrieves, and detaches Text Studio through Unity's native text binding slot.</summary>
    public static class TextStudioUITK
    {
        static readonly BindingId s_TextBindingId = "text";

        /// <summary>Gets or creates a binding, attaches it to the element, and returns it for configuration.</summary>
        public static TextStudioBinding AttachTextStudio(this TextElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (TryAttachTextStudio(element, out TextStudioBinding binding))
                return binding;

            Binding existing = element.GetBinding(s_TextBindingId);
            string owner = existing != null ? existing.GetType().Name : "an unsupported target";
            throw new InvalidOperationException(
                $"Text Studio cannot bind '{element.name ?? element.GetType().Name}.text' because {owner} already owns that slot. Remove the existing binding or use a separate Label.");
        }

        /// <summary>Tries to create or retrieve a Text Studio text binding without replacing another binding.</summary>
        public static bool TryAttachTextStudio(
            this TextElement element,
            out TextStudioBinding binding)
        {
            binding = null;
            if (element == null)
                return false;

            Binding existing = element.GetBinding(s_TextBindingId);
            if (existing is TextStudioBinding textStudioBinding)
            {
                binding = textStudioBinding;
                return true;
            }

            if (existing != null)
                return false;

            binding = new TextStudioBinding();
            binding.Attach(element);
            element.SetBinding(s_TextBindingId, binding);
            return true;
        }

        /// <summary>Gets an existing binding without creating or attaching one.</summary>
        public static TextStudioBinding GetTextStudioBinding(this TextElement element)
        {
            if (element == null)
                return null;
            return element.GetBinding(s_TextBindingId) as TextStudioBinding;
        }

        /// <summary>Restores the element and clears only a Text Studio-owned text binding.</summary>
        public static void DetachTextStudio(this TextElement element)
        {
            if (element == null)
                return;
            if (element.GetBinding(s_TextBindingId) is not TextStudioBinding binding)
                return;

            binding.Detach();
            element.ClearBinding(s_TextBindingId);
            binding.CompleteBindingRemoval();
        }
    }
}
