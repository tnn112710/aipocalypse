using System;
using System.Collections;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using UnityEngine;
using BuiltInWaitAction = TextStudio.BuiltIns.WaitAction;

namespace TextStudio
{
    internal sealed partial class TextStudioRuntimeCore : ITextStudioController, ITextStudioWaitLifecycleSink, ITypewriterProvider
    {
        readonly StringTextSource _source = new StringTextSource();
        readonly TextStudioPipeline _pipeline = new TextStudioPipeline();
        StringTextSource _textChangePreviewSource;
        TextStudioPipeline _textChangePreviewPipeline;
        string _supplementalEffects;
        internal string NumericDecimalSeparator = ".";
        readonly RevealController _reveal = new RevealController();
        readonly AnimationEvaluator _animator = new AnimationEvaluator();
        readonly GlyphDirtyBits _meshDirty = new GlyphDirtyBits();
        readonly List<ActionContext> _activeActionContexts = new List<ActionContext>();
        int _actionGeneration;
        readonly List<ITextStudioAction> _localActions = new List<ITextStudioAction>();
        readonly List<ITextStudioExtensionTagProcessor> _extensionTagProcessors = new List<ITextStudioExtensionTagProcessor>();
        readonly List<ExternalEffectRequest> _pendingExternalEffects = new List<ExternalEffectRequest>();
        readonly Dictionary<ITextStudioEffectDefinition, ExternalEffectPlan> _externalEffectPlans =
            new Dictionary<ITextStudioEffectDefinition, ExternalEffectPlan>();
        readonly TextStudioEffectResolver _externalEffectResolver = new TextStudioEffectResolver();
        readonly List<TextStudioResolvedEffectBinding> _externalResolutionBuffer =
            new List<TextStudioResolvedEffectBinding>();
        TextStudioRenderPropertyGlyphAccumulator _externalPropertiesBefore;
        TextStudioRenderPropertyGlyphAccumulator _externalPropertiesAfter;
        GlyphDirtyBits _previousExternalEffectGlyphs = new GlyphDirtyBits();
        GlyphDirtyBits _currentExternalEffectGlyphs = new GlyphDirtyBits();
        uint _compiledEffectContentRevision;
        bool _effectContentRefreshPending;
        uint _externalEffectRegistryRevision;
        uint _externalEffectContentRevision;
        bool _externalEffectsAppliedLastFrame;
        bool _externalMaterialEffectAppliedLastFrame;
        TextStudioSourceContextRequirements _externalSourceRequirementsLastFrame;

        readonly struct ExternalEffectRequest
        {
            public readonly ExternalEffectPlan Plan;
            public readonly float Intensity;
            public readonly float Progress;
            public readonly int StartUnit;
            public readonly int EndUnitExclusive;

            public ExternalEffectRequest(
                ExternalEffectPlan plan,
                float intensity,
                float progress,
                int startUnit,
                int endUnitExclusive)
            {
                Plan = plan;
                Intensity = intensity;
                Progress = progress;
                StartUnit = startUnit;
                EndUnitExclusive = endUnitExclusive;
            }
        }

        readonly struct ExternalEffectBinding
        {
            public readonly ITextStudioEffect Effect;
            public readonly float Intensity;
            public readonly float TimeOffset;
            public readonly TextStudioEffectAreaSettings Area;
            public readonly TextStudioEffectScopeChain Scope;
            public readonly TextStudioComposedEnvelopeStack Envelope;

            public ExternalEffectBinding(
                ITextStudioEffect effect,
                float intensity,
                float timeOffset,
                TextStudioEffectAreaSettings area,
                TextStudioEffectScopeChain scope,
                TextStudioComposedEnvelopeStack envelope)
            {
                Effect = effect;
                Intensity = intensity;
                TimeOffset = timeOffset;
                Area = area;
                Scope = scope;
                Envelope = envelope;
            }
        }

        sealed class ExternalEffectPlan
        {
            public readonly ExternalEffectBinding[] Bindings;
            public readonly bool RequiresMaterial;
            public readonly TextStudioSourceContextRequirements SourceRequirements;

            public ExternalEffectPlan(
                ExternalEffectBinding[] bindings,
                bool requiresMaterial,
                TextStudioSourceContextRequirements sourceRequirements)
            {
                Bindings = bindings;
                RequiresMaterial = requiresMaterial;
                SourceRequirements = sourceRequirements;
            }
        }

        DirtyTracker _dirty = new DirtyTracker();
        DirtyFlags _dirtySetDuringPipeline;
        ITextStudioActionRunner _actionRunner;
        TypingTimingsBase _timings;
        TextStudioDefaultEffectSettings _defaultEffects = TextStudioDefaultEffectSettings.None;
        RevealTimingsPreset _builtInTimingsPreset = RevealTimingsPreset.Uniform;
        bool _waitAfterFinalReveal = true;
        RevealMode _revealMode = RevealMode.Instant;
        bool _revealScheduleDirty;
        float _revealSpeedMultiplier = 1f;
        bool _hasRuntimeRevealSpeedOverride;
        float _revealAppearanceDuration = 0.3f;
        float _revealDisappearanceDuration = 0.3f;
        bool _loopReveal;
        float _loopRevealDelay = 0.75f;
        string _revealTransitionStyle = string.Empty;
        string _hideTransitionStyle = string.Empty;
        TextStudioDefaultEffectMode _transitionEffectMode = TextStudioDefaultEffectMode.None;
        ITextStudioEffectDefinition _typewriterRevealEffect;
        ITextStudioEffectDefinition _typewriterHideEffect;
        TextStudioSequenceSettings _revealSequence = TextStudioSequenceSettings.Default;
        TextStudioSequenceSettings _hideSequence = TextStudioSequenceSettings.Default;
        TextStudioTypewriterSkipRevealMode _skipRevealMode = TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately;
        TextStudioTypewriterSkipHideMode _skipHideMode = TextStudioTypewriterSkipHideMode.HideImmediately;
        bool _triggerRemainingMarkersOnSkip;
        bool _skipCurrentWaitOnSkip = true;
        bool _runActionsInEditModePreview;
        bool _previewSuppressRevealLoop;
        bool _isProcessingPipeline;
        bool _hasPendingTextChangeContinuity;
        bool _hasPreparedTextChangeContinuity;
        TextStudioGlyphChangeSet _pendingGlyphChanges;
        RevealController.TextChangeState _pendingRevealTextChangeState;
        AnimationEvaluator.TextChangeState _pendingAnimatorTextChangeState;

        public event Action<string> TextSet;
        public event Action<TextStudioCompiledPlan> PlanChanged;
        public event Action<int, char> GlyphRevealed;
        public event Action<int> GlyphDisappeared;
        public event Action<int, string> TextUnitRevealed;
        public event Action<string, string[]> MarkerReached;
        public event Action<TextStudioPlanView> ExtensionTagsChanged;
        public event Action<TextStudioExtensionTag> ExtensionTagReached;
        public event Action<TextStudioTypewriterWaitInfo> WaitStarted;
        public event Action<TextStudioTypewriterWaitInfo> WaitFinished;
        public event Action TypewriterStarted;
        public event Action ShowComplete;
        public event Action DisappearComplete;
        public event Action<bool> RevealReset;

        event Action<string, string[]> ITextStudioController.OnMarkerReached
        {
            add => MarkerReached += value;
            remove => MarkerReached -= value;
        }

        event Action ITypewriterProvider.OnShowComplete
        {
            add => ShowComplete += value;
            remove => ShowComplete -= value;
        }

        event Action ITypewriterProvider.OnTypewriterStarted
        {
            add => TypewriterStarted += value;
            remove => TypewriterStarted -= value;
        }

        event Action ITypewriterProvider.OnDisappearComplete
        {
            add => DisappearComplete += value;
            remove => DisappearComplete -= value;
        }

        public StringTextSource Source => _source;
        public TextStudioPipeline Pipeline => _pipeline;
        public TextStudioCompiledPlan Plan => _pipeline.Plan;
        public RevealController Reveal => _reveal;
        public AnimationEvaluator Animator => _animator;
        public GlyphDirtyBits MeshDirty => _meshDirty;
        public EvaluatedGlyphState[] States => _animator.States;
        public int StateCount => _animator.StateCount;
        internal bool LastEvaluationChangedRenderedState { get; private set; }
        public DirtyFlags DirtyFlags => _dirty.GlobalFlags;
        public int ActiveActionCount => _activeActionContexts.Count;
        public string Markup => _source.GetCanonicalText();
        public string Text => _pipeline.Plan != null ? _pipeline.Plan.ResolvedText : string.Empty;
        public bool HasPlan => _pipeline.Plan != null;
        public bool HasActiveEffects => _pipeline.Plan != null && _pipeline.Plan.EffectBindingCount > 0;
        // Settled text whose effects only apply while revealing or hiding produces no new output until one starts.
        bool HasContinuousEffects => HasActiveEffects && _animator.HasSettledOutputEffects;
        internal bool HasExternalEffectState =>
            _pendingExternalEffects.Count > 0 ||
            _externalEffectsAppliedLastFrame;
        internal bool RequiresExternalMaterialEffect =>
            _reactionRequiresMaterial ||
            PendingExternalEffectsRequireMaterial() ||
            (_externalEffectsAppliedLastFrame && _externalMaterialEffectAppliedLastFrame);
        internal TextStudioSourceContextRequirements ExternalSourceContextRequirements =>
            _reactionSourceRequirements |
            PendingExternalSourceRequirements() |
            (_externalEffectsAppliedLastFrame
                ? _externalSourceRequirementsLastFrame
                : TextStudioSourceContextRequirements.None);
        internal bool RequiresContinuousEvaluation =>
            _reveal.HasActiveReveal ||
            HasContinuousEffects ||
            _animator.HasActiveVisibilityTransitions ||
            HasExternalEffectState ||
            HasReactionWork;
        internal bool HasPreparedTextChangeContinuity =>
            _hasPendingTextChangeContinuity || _hasPreparedTextChangeContinuity;
        internal bool HasOnlyPersistentEffectEvaluation =>
            HasActiveEffects &&
            !_reveal.HasActiveReveal &&
            !_animator.HasActiveVisibilityTransitions &&
            _pendingExternalEffects.Count == 0 &&
            !_externalEffectsAppliedLastFrame;

        public RevealMode RevealMode
        {
            get => _revealMode;
            set
            {
                _revealMode = value;
                _reveal.Mode = value;
                _revealScheduleDirty = true;
                SetDirty(DirtyFlags.Animation);
            }
        }

        public TypingTimingsBase Timings
        {
            get => _timings;
            set
            {
                _timings = value;
                _revealScheduleDirty = true;
            }
        }

        public RevealTimingsPreset BuiltInTimingsPreset
        {
            get => _builtInTimingsPreset;
            set
            {
                _builtInTimingsPreset = value;
                _revealScheduleDirty = true;
            }
        }

        public bool WaitAfterFinalReveal
        {
            get => _waitAfterFinalReveal;
            set
            {
                _waitAfterFinalReveal = value;
                _revealScheduleDirty = true;
            }
        }

        public float RevealSpeedMultiplier
        {
            get => _revealSpeedMultiplier;
            set => ApplyConfiguredRevealSpeed(value);
        }

        public float RevealAppearanceDuration
        {
            get => _revealAppearanceDuration;
            set
            {
                _revealAppearanceDuration = Mathf.Max(0.01f, value);
                _reveal.AppearanceDuration = _revealAppearanceDuration;
                SetDirty(DirtyFlags.Animation);
            }
        }

        public float RevealDisappearanceDuration
        {
            get => _revealDisappearanceDuration;
            set
            {
                _revealDisappearanceDuration = Mathf.Max(0.01f, value);
                _reveal.DisappearanceDuration = _revealDisappearanceDuration;
                SetDirty(DirtyFlags.Animation);
            }
        }

        public bool LoopReveal
        {
            get => _loopReveal;
            set
            {
                _loopReveal = value;
                ApplyRevealLoopConfiguration();
            }
        }

        public float LoopRevealDelay
        {
            get => _loopRevealDelay;
            set
            {
                _loopRevealDelay = Mathf.Max(0f, value);
                ApplyRevealLoopConfiguration();
            }
        }

        public float RevealProgress => _reveal.RevealProgress;
        internal float DisappearProgress => _reveal.DisappearProgress;
        public int RevealedGlyphCount => _reveal.RevealedCount;
        public int TotalGlyphCount => _reveal.TotalCount;
        public int WordCount => _pipeline.Plan != null ? _pipeline.Plan.WordCount : 0;
        public bool HasVisibilityOverrides => _animator.HasVisibilityOverrides;
        internal bool HidesEveryGlyphByOverride() => _animator.HidesEveryGlyphByOverride();
        public bool IsRevealing => _reveal.IsRevealing;
        internal bool HasActiveRevealAppearance => _reveal.HasActiveAppearance;
        public bool IsRevealPaused => _reveal.IsPaused;
        public bool IsWaitingForAction => _reveal.IsPaused && _activeActionContexts.Count > 0;
        public bool IsShowingText => _reveal.IsRevealing;
        public bool IsDisappearing => _reveal.IsDisappearing;
        public bool RevealEffectsEnabled
        {
            get => _animator.AppearanceEffectsEnabled;
            set => SetEffectFamilyEnabled(AnimationPhase.Appearance, value);
        }
        public bool ActiveEffectsEnabled
        {
            get => _animator.PersistentEffectsEnabled;
            set => SetEffectFamilyEnabled(AnimationPhase.Persistent, value);
        }
        public bool HideEffectsEnabled
        {
            get => _animator.DisappearanceEffectsEnabled;
            set => SetEffectFamilyEnabled(AnimationPhase.Disappearance, value);
        }
        public TextStudioSequenceSettings RevealSequence
        {
            get => _revealSequence;
            set
            {
                _revealSequence = value;
                _reveal.RevealSequence = value;
                _revealScheduleDirty = true;
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            }
        }

        public TextStudioSequenceSettings HideSequence
        {
            get => _hideSequence;
            set
            {
                _hideSequence = value;
                _reveal.HideSequence = value;
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            }
        }

        public TextStudioMotionPolicy MotionPolicy
        {
            get => _animator.MotionPolicy;
            set
            {
                _animator.MotionPolicy = value;
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            }
        }

        public TextStudioTypewriterSkipRevealMode SkipRevealMode
        {
            get => _skipRevealMode;
            set => _skipRevealMode = value;
        }

        public TextStudioTypewriterSkipHideMode SkipHideMode
        {
            get => _skipHideMode;
            set => _skipHideMode = value;
        }

        public bool TriggerRemainingMarkersOnSkip
        {
            get => _triggerRemainingMarkersOnSkip;
            set => _triggerRemainingMarkersOnSkip = value;
        }

        public bool SkipCurrentWaitOnSkip
        {
            get => _skipCurrentWaitOnSkip;
            set => _skipCurrentWaitOnSkip = value;
        }
        internal UnityEngine.Object ActionOwner { get; set; }

        public MonoBehaviour CoroutineHost => _actionRunner != null ? _actionRunner.CoroutineHost : null;
        public bool RunActionsInEditModePreview
        {
            get => _runActionsInEditModePreview;
            set => _runActionsInEditModePreview = value;
        }

        internal bool IsProcessingPipeline => _isProcessingPipeline;
        internal bool HasDeferredTextOrStyleDirty =>
            IsDirty(DirtyFlags.Text | DirtyFlags.Style) &&
            (_dirtySetDuringPipeline & (DirtyFlags.Text | DirtyFlags.Style)) != DirtyFlags.None;

        public TextStudioRuntimeCore()
        {
            _compiledEffectContentRevision = TextStudioEffectDefinitionAsset.ContentRevision;
            _reveal.OnGlyphRevealed += ForwardGlyphRevealed;
            _reveal.OnGlyphDisappeared += ForwardGlyphDisappeared;
            _reveal.OnActionMarkerReached += ForwardActionMarkerReached;
            _reveal.OnExtensionTagReached += ForwardExtensionTagReached;
            _reveal.OnRevealComplete += ForwardRevealComplete;
            _reveal.OnDisappearComplete += ForwardDisappearComplete;
        }

        public void SetActionRunner(ITextStudioActionRunner actionRunner)
        {
            _actionRunner = actionRunner;
        }

        public void SetLocalActions(IReadOnlyList<ITextStudioAction> actions)
        {
            // Owned render outputs re-run discovery when they parent under the hub; an unchanged
            // action set must not force a full text, style and topology rebuild.
            if (HasSameLocalActions(actions))
                return;

            _localActions.Clear();
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    ITextStudioAction action = actions[i];
                    if (action != null && !_localActions.Contains(action))
                        _localActions.Add(action);
                }
            }

            _pipeline.SetLocalActions(_localActions);
            SetDirty(DirtyFlags.Text | DirtyFlags.Style);
        }

        bool HasSameLocalActions(IReadOnlyList<ITextStudioAction> actions)
        {
            int index = 0;
            int count = actions?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                ITextStudioAction action = actions[i];
                if (action == null || IndexOfLocalAction(actions, action, i) >= 0)
                    continue;
                if (index >= _localActions.Count || !ReferenceEquals(_localActions[index], action))
                    return false;
                index++;
            }
            return index == _localActions.Count;
        }

        static int IndexOfLocalAction(IReadOnlyList<ITextStudioAction> actions, ITextStudioAction action, int end)
        {
            for (int i = 0; i < end; i++)
            {
                if (ReferenceEquals(actions[i], action))
                    return i;
            }
            return -1;
        }

        public void RegisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (processor == null || _extensionTagProcessors.Contains(processor))
                return;

            _extensionTagProcessors.Add(processor);
            if (_pipeline.Plan != null)
                NotifyExtensionTagsChanged(_pipeline.Plan);
        }

        public void UnregisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (processor == null)
                return;

            _extensionTagProcessors.Remove(processor);
        }

        public void Configure(
            RevealMode revealMode,
            TypingTimingsBase timings,
            RevealTimingsPreset builtInTimingsPreset,
            bool waitAfterFinalReveal,
            float revealSpeedMultiplier,
            float revealAppearanceDuration,
            float revealDisappearanceDuration,
            bool loopReveal,
            float loopRevealDelay,
            string revealTransitionStyle = "",
            string hideTransitionStyle = "",
            TextStudioDefaultEffectMode transitionEffectMode = TextStudioDefaultEffectMode.None,
            TextStudioTypewriterSkipRevealMode skipRevealMode = TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately,
            bool triggerRemainingMarkersOnSkip = false,
            bool skipCurrentWaitOnSkip = true,
            TextStudioTypewriterSkipHideMode skipHideMode = TextStudioTypewriterSkipHideMode.HideImmediately,
            TextStudioSequenceSettings revealSequence = default,
            TextStudioSequenceSettings hideSequence = default,
            ITextStudioEffectDefinition typewriterRevealEffect = null,
            ITextStudioEffectDefinition typewriterHideEffect = null,
            TextStudioTypewriterTimingMode timingMode = TextStudioTypewriterTimingMode.Cadence)
        {
            _revealScheduleDirty = true;
            if (transitionEffectMode == TextStudioDefaultEffectMode.None &&
                (typewriterRevealEffect != null || typewriterHideEffect != null))
            {
                transitionEffectMode = TextStudioDefaultEffectMode.Constant;
            }
            _timings = timings;
            _builtInTimingsPreset = builtInTimingsPreset;
            _waitAfterFinalReveal = waitAfterFinalReveal;
            _revealMode = revealMode;
            if (!_hasRuntimeRevealSpeedOverride || !_reveal.HasActiveReveal)
                ApplyConfiguredRevealSpeed(revealSpeedMultiplier);
            _revealAppearanceDuration = Mathf.Max(0.01f, revealAppearanceDuration);
            _revealDisappearanceDuration = Mathf.Max(0.01f, revealDisappearanceDuration);
            _loopReveal = loopReveal;
            _loopRevealDelay = Mathf.Max(0f, loopRevealDelay);
            _revealTransitionStyle = typewriterRevealEffect != null ? string.Empty : revealTransitionStyle ?? string.Empty;
            _hideTransitionStyle = typewriterHideEffect != null ? string.Empty : hideTransitionStyle ?? string.Empty;
            _transitionEffectMode = transitionEffectMode;
            _typewriterRevealEffect = typewriterRevealEffect;
            _typewriterHideEffect = typewriterHideEffect;
            _reveal.TimingMode = timingMode;
            _revealSequence = !revealSequence.Equals(default)
                ? revealSequence
                : TextStudioSequenceSettings.Default;
            _hideSequence = !hideSequence.Equals(default)
                ? hideSequence
                : TextStudioSequenceSettings.Default;
            _skipRevealMode = skipRevealMode;
            _triggerRemainingMarkersOnSkip = triggerRemainingMarkersOnSkip;
            _skipCurrentWaitOnSkip = skipCurrentWaitOnSkip;
            _skipHideMode = skipHideMode;
            _pipeline.ApplyTypewriterTransitionStyle(
                _revealTransitionStyle,
                _hideTransitionStyle,
                _transitionEffectMode,
                _typewriterRevealEffect,
                _typewriterHideEffect);
            ApplyRevealConfiguration();
        }

        internal void ApplySupplementalEffects(string expression)
        {
            if (!_pipeline.ApplySupplementalEffects(expression))
                return;
            _supplementalEffects = expression;
            _textChangePreviewPipeline?.ApplySupplementalEffects(expression);
            SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void ApplyDefaultEffects(TextStudioDefaultEffectSettings settings)
        {
            if (_defaultEffects.Equals(settings))
                return;

            _defaultEffects = settings;
            _pipeline.ApplyDefaultEffects(settings);
            SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void ApplyTypewriterTransitionStyle(
            string revealStyle,
            string hideStyle,
            TextStudioDefaultEffectMode transitionEffectMode,
            ITextStudioEffectDefinition revealEffect = null,
            ITextStudioEffectDefinition hideEffect = null)
        {
            revealStyle = revealEffect != null ? string.Empty : revealStyle ?? string.Empty;
            hideStyle = hideEffect != null ? string.Empty : hideStyle ?? string.Empty;
            if (transitionEffectMode == TextStudioDefaultEffectMode.None &&
                (revealEffect != null || hideEffect != null))
            {
                transitionEffectMode = TextStudioDefaultEffectMode.Constant;
            }

            if (string.Equals(_revealTransitionStyle, revealStyle, StringComparison.Ordinal) &&
                string.Equals(_hideTransitionStyle, hideStyle, StringComparison.Ordinal) &&
                _transitionEffectMode == transitionEffectMode &&
                ReferenceEquals(_typewriterRevealEffect, revealEffect) &&
                ReferenceEquals(_typewriterHideEffect, hideEffect))
            {
                return;
            }

            _revealTransitionStyle = revealStyle;
            _hideTransitionStyle = hideStyle;
            _transitionEffectMode = transitionEffectMode;
            _typewriterRevealEffect = revealEffect;
            _typewriterHideEffect = hideEffect;
            _pipeline.ApplyTypewriterTransitionStyle(
                revealStyle,
                hideStyle,
                transitionEffectMode,
                revealEffect,
                hideEffect);
            SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void SetText(string markup, bool raiseEvent = true)
        {
            if (_reveal.IsDisappearing)
                _reveal.StopDisappear();
            string nextMarkup = markup ?? string.Empty;
            bool textChanged = _reactions != null &&
                               !string.Equals(_source.GetCanonicalText(), nextMarkup, StringComparison.Ordinal);
            if (textChanged)
            {
                EnsureReactionTextChangeMap(nextMarkup);
                RecordReactionTextChange(TextStudioReactionChangeKind.Replace);
            }
            _source.SetText(nextMarkup);
            SetDirty(DirtyFlags.Text);
            if (textChanged)
                DispatchReaction(TextStudioReactionTrigger.TextChanged);
            if (raiseEvent)
                TextSet?.Invoke(_source.GetCanonicalText());
        }

        public void AppendText(string markup)
        {
            if (string.IsNullOrEmpty(markup))
                return;

            bool textChanged = _reactions != null && !string.IsNullOrEmpty(markup);
            _source.AppendText(markup);
            if (textChanged)
            {
                EnsureReactionTextChangeMap(_source.GetCanonicalText());
                RecordReactionTextChange(TextStudioReactionChangeKind.Append);
            }
            if (_reveal.IsDisappearing)
                _reveal.StopDisappear();
            SetDirty(DirtyFlags.Text);
            if (textChanged)
                DispatchReaction(TextStudioReactionTrigger.TextChanged);
        }

        internal void AppendCanonicalText(string canonicalMarkup)
        {
            canonicalMarkup ??= string.Empty;
            string existingMarkup = _source.GetCanonicalText() ?? string.Empty;
            bool textChanged = _reactions != null &&
                               !string.Equals(existingMarkup, canonicalMarkup, StringComparison.Ordinal);
            bool isAppend = canonicalMarkup.Length >= existingMarkup.Length &&
                string.CompareOrdinal(
                    existingMarkup,
                    0,
                    canonicalMarkup,
                    0,
                    existingMarkup.Length) == 0;
            if (textChanged)
            {
                EnsureReactionTextChangeMap(canonicalMarkup);
                RecordReactionTextChange(isAppend
                    ? TextStudioReactionChangeKind.Append
                    : TextStudioReactionChangeKind.Replace);
            }
            if (isAppend)
            {
                _source.SetAppendedText(canonicalMarkup, existingMarkup.Length);
            }
            else
            {
                _source.SetText(canonicalMarkup);
            }

            if (_reveal.IsDisappearing)
                _reveal.StopDisappear();
            SetDirty(DirtyFlags.Text);
            if (textChanged)
                DispatchReaction(TextStudioReactionTrigger.TextChanged);
        }

        public void ClearText()
        {
            DiscardTextChangeContinuity();
            SetText(string.Empty);
            _reveal.Reset();
            _animator.Reset();
        }

        internal void PrepareTextChangeContinuity(
            TextStudioGlyphChangeSet glyphChanges)
        {
            if (!glyphChanges.IsDefined)
            {
                DiscardTextChangeContinuity();
                return;
            }

            _pendingGlyphChanges = glyphChanges;
            _pendingRevealTextChangeState = _reveal.CaptureTextChangeState(
                _pipeline.Plan,
                glyphChanges);
            _pendingAnimatorTextChangeState = _animator.CaptureTextChangeState(
                glyphChanges);
            _hasPendingTextChangeContinuity = true;
            _hasPreparedTextChangeContinuity = false;
        }

        internal void DiscardTextChangeContinuity()
        {
            _hasPendingTextChangeContinuity = false;
            _hasPreparedTextChangeContinuity = false;
            _pendingGlyphChanges = default;
            _pendingRevealTextChangeState = default;
            _pendingAnimatorTextChangeState = default;
        }

        public void ShowText(string text)
        {
            SetText(text);
            StartReveal();
        }

        public void ShowTextInstant(string text)
        {
            SetText(text);
            SkipReveal();
        }

        public void SetDirty(DirtyFlags flags)
        {
            _dirty.SetDirty(flags);
            if (_isProcessingPipeline)
                _dirtySetDuringPipeline |= flags;
        }

        public bool IsDirty(DirtyFlags flags)
        {
            return _dirty.IsDirty(flags);
        }

        public bool ConsumeDirty(DirtyFlags flags)
        {
            return _dirty.Consume(flags);
        }

        public void ClearDirty(DirtyFlags flags)
        {
            _dirty.Clear(flags);
        }

        public void ClearAllDirty()
        {
            _dirty.ClearAll();
        }

        public void ClearProcessedTextAndStyleDirty()
        {
            DirtyFlags protectedDirty = _dirtySetDuringPipeline & (DirtyFlags.Text | DirtyFlags.Style);
            ClearDirty((DirtyFlags.Text | DirtyFlags.Style) & ~protectedDirty);
        }

        public bool ProcessPipeline(
            bool snapNonInstantRevealToEnd,
            bool renderMarkupLiterally = false,
            TextStudioMarkupSyntax markupSyntax = null)
        {
            EnsureEffectContentCurrent();
            if (_isProcessingPipeline)
                return false;

            TextStudioCompiledPlan previousPlan = _pipeline.Plan;
            string previousCanonicalText = previousPlan?.CanonicalText;
            bool preserveEffectTime = _effectContentRefreshPending;
            // Appending keeps the effect clock of the text already shown; restarting it made every append jump
            // persistent effects such as Wave back to their first frame. Read before Process consumes the range.
            bool appendsProcessedText = previousCanonicalText != null &&
                _source.HasAppendRangeSince(previousPlan.SourceVersion, _source.GetVersion());
            _isProcessingPipeline = true;
            _dirtySetDuringPipeline = DirtyFlags.None;

            try
            {
                if (IsDirty(DirtyFlags.Style))
                    _pipeline.Invalidate();

                bool changed = _pipeline.Process(
                    _source,
                    renderMarkupLiterally,
                    markupSyntax != null
                        ? markupSyntax
                        : TextStudioProjectSettings.ActiveMarkupSyntax);
                if (!changed)
                {
                    DiscardTextChangeContinuity();
                    return false;
                }

                TextStudioCompiledPlan plan = _pipeline.Plan;
                int count = plan != null ? plan.VisibleGlyphCount : 0;

                bool preserveCurrentEffectTime = appendsProcessedText ||
                    preserveEffectTime &&
                    previousPlan != null &&
                    plan != null &&
                    string.Equals(
                        previousCanonicalText,
                        plan.CanonicalText,
                        StringComparison.Ordinal);
                _animator.Initialize(count, preserveCurrentEffectTime);
                _effectContentRefreshPending = false;
                _animator.BindEffects(plan);
                _meshDirty.EnsureCapacity(count);
                _meshDirty.SetAllDirty(count);
                _pendingExternalEffects.Clear();
                _previousExternalEffectGlyphs.ClearAll();
                _currentExternalEffectGlyphs.ClearAll();
                _externalEffectsAppliedLastFrame = false;
                _externalMaterialEffectAppliedLastFrame = false;
                _externalSourceRequirementsLastFrame =
                    TextStudioSourceContextRequirements.None;
                NotifyExtensionTagsChanged(plan);
                PlanChanged?.Invoke(plan);

                if (_revealMode == RevealMode.Instant)
                {
                    InitializeReveal(useBuiltInTimings: false);
                    _reveal.RevealAll();
                    DiscardTextChangeContinuity();
                }
                else if (_hasPendingTextChangeContinuity)
                {
                    TextStudioGlyphChangeSet glyphChanges = _pendingGlyphChanges;
                    RevealController.TextChangeState revealState =
                        _pendingRevealTextChangeState;
                    AnimationEvaluator.TextChangeState animatorState =
                        _pendingAnimatorTextChangeState;
                    _hasPendingTextChangeContinuity = false;
                    _pendingGlyphChanges = default;
                    _pendingRevealTextChangeState = default;
                    _pendingAnimatorTextChangeState = default;

                    InitializeReveal(useBuiltInTimings: true);
                    _reveal.RestoreTextChangeState(
                        in revealState,
                        plan,
                        glyphChanges);
                    _animator.RestoreTextChangeState(in animatorState);
                    _hasPreparedTextChangeContinuity = true;
                }
                else if (snapNonInstantRevealToEnd ||
                         (_reveal.IsComplete && !string.Equals(previousCanonicalText, plan.CanonicalText, StringComparison.Ordinal)))
                {
                    InitializeReveal(useBuiltInTimings: false);
                    _reveal.RevealAll();
                    SetDirty(DirtyFlags.Animation);
                }

                return true;
            }
            finally
            {
                _isProcessingPipeline = false;
            }
        }

        /// <summary>
        /// Compiles the units and resolved text of incoming markup for a glyph change map. Styles, effects and segments of
        /// the returned plan are not resolved, so it must not be used beyond comparing units.
        /// </summary>
        internal TextStudioCompiledPlan CompileTextChangeUnits(
            string markup,
            bool renderMarkupLiterally,
            TextStudioMarkupSyntax markupSyntax)
        {
            if (_textChangePreviewPipeline == null)
            {
                _textChangePreviewPipeline = new TextStudioPipeline();
                _textChangePreviewSource = new StringTextSource();
                _textChangePreviewPipeline.ApplySupplementalEffects(_supplementalEffects);
            }
            _textChangePreviewPipeline.WordBoundaryProvider = _pipeline.WordBoundaryProvider;
            _textChangePreviewPipeline.SetLocalActions(_localActions);
            _textChangePreviewPipeline.ApplyDefaultEffects(_defaultEffects);
            _textChangePreviewPipeline.ApplyTypewriterTransitionStyle(
                _revealTransitionStyle,
                _hideTransitionStyle,
                _transitionEffectMode,
                _typewriterRevealEffect,
                _typewriterHideEffect);
            _textChangePreviewSource.SetText(markup ?? string.Empty);
            _textChangePreviewPipeline.Process(
                _textChangePreviewSource,
                null,
                renderMarkupLiterally,
                markupSyntax != null
                    ? markupSyntax
                    : TextStudioProjectSettings.ActiveMarkupSyntax,
                unitsOnly: true);
            return _textChangePreviewPipeline.Plan;
        }

        internal ITextStudioWordBoundaryProvider WordBoundaryProvider
        {
            get => _pipeline.WordBoundaryProvider;
            set
            {
                if (ReferenceEquals(_pipeline.WordBoundaryProvider, value))
                    return;
                _pipeline.WordBoundaryProvider = value;
                SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
            }
        }

        public bool ProcessPendingTextForReveal()
        {
            EnsureEffectContentCurrent();
            if (_isProcessingPipeline)
                return false;

            if (_pipeline.Plan != null && !IsDirty(DirtyFlags.Text | DirtyFlags.Style))
                return false;

            ProcessPipeline(snapNonInstantRevealToEnd: false);
            ClearProcessedTextAndStyleDirty();
            SetDirty(DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual);
            return true;
        }

        internal bool IsEffectContentCurrent =>
            _compiledEffectContentRevision == TextStudioEffectDefinitionAsset.ContentRevision;

        internal void EnsureEffectContentCurrent()
        {
            uint contentRevision = TextStudioEffectDefinitionAsset.ContentRevision;
            if (_compiledEffectContentRevision == contentRevision)
                return;

            _compiledEffectContentRevision = contentRevision;
            _effectContentRefreshPending = true;
            _pipeline.Invalidate();
            _textChangePreviewPipeline?.Invalidate();
            SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void StartReveal()
        {
            StartReveal(resetProgress: true);
        }

        public void StartReveal(bool resetProgress)
        {
            if (_isProcessingPipeline)
                return;

            if (resetProgress)
                _hasPreparedTextChangeContinuity = false;

            int revealedPrefix = resetProgress ? 0 : _reveal.RevealedCount;
            ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;

            if (resetProgress)
                _hasRuntimeRevealSpeedOverride = false;
            InitializeReveal(useBuiltInTimings: true);
            if (_revealMode == RevealMode.Instant && !HasCueTimeline)
            {
                DispatchRevealStartedReaction();
                RevealAllWithReactions();
                int count = _animator.StateCount;
                if (count > 0)
                {
                    _meshDirty.EnsureCapacity(count);
                    _meshDirty.SetAllDirty(count);
                }
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                if (resetProgress)
                    RevealReset?.Invoke(false);
                ForwardRevealComplete();
                return;
            }

            if (!resetProgress)
                _reveal.RestoreRevealedPrefix(revealedPrefix, _pipeline.Plan);
            _reveal.StartReveal(resetProgress);

            if (resetProgress)
            {
                int count = _animator.StateCount;
                if (count > 0)
                {
                    _meshDirty.EnsureCapacity(count);
                    _meshDirty.SetAllDirty(count);
                }
                SetDirty(DirtyFlags.Animation);
                RevealReset?.Invoke(false);
                DispatchRevealStartedReaction();
                if (_revealMode != RevealMode.Instant)
                    TypewriterStarted?.Invoke();
            }
            else
            {
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                _reactionAwaitingReveal = false;
            }
            if (_revealMode == RevealMode.Instant)
                TickReveal(0f, allowTimedReveal: true);
        }

        internal void StartTextChangeReveal()
        {
            if (_isProcessingPipeline)
                return;

            ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;
            if (!_hasPreparedTextChangeContinuity)
            {
                StartReveal(resetProgress: true);
                return;
            }

            _hasPreparedTextChangeContinuity = false;
            if (_revealMode == RevealMode.Instant && !HasCueTimeline)
            {
                DispatchRevealStartedReaction();
                RevealAllWithReactions();
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                RevealReset?.Invoke(true);
                ForwardRevealComplete();
                return;
            }

            _hasRuntimeRevealSpeedOverride = false;
            _reveal.StartPreparedTextChangeReveal();
            int count = _animator.StateCount;
            if (count > 0)
            {
                _meshDirty.EnsureCapacity(count);
                _meshDirty.SetAllDirty(count);
            }
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            RevealReset?.Invoke(true);
            DispatchRevealStartedReaction();
            TypewriterStarted?.Invoke();
        }

        internal void ContinueTextChangeReveal()
        {
            if (_isProcessingPipeline)
                return;

            ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;
            if (!_hasPreparedTextChangeContinuity)
            {
                StartReveal(resetProgress: false);
                return;
            }

            _hasPreparedTextChangeContinuity = false;
            if (_revealMode == RevealMode.Instant && !HasCueTimeline)
            {
                DispatchRevealStartedReaction();
                RevealAllWithReactions();
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                ForwardRevealComplete();
                return;
            }

            _reveal.StartPreparedTextChangeReveal();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            _reactionAwaitingReveal = false;
        }

        public void SkipReveal()
        {
            if (_isProcessingPipeline)
                return;

            bool planRefreshed = ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;

            if (planRefreshed || _reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount)
                InitializeReveal(useBuiltInTimings: true);

            if (_skipCurrentWaitOnSkip)
                SkipCurrentWait();
            if (_triggerRemainingMarkersOnSkip)
                _reveal.FlushRemainingMarkers(_pipeline.Plan);
            _reveal.FlushSkippableExtensionTags(_pipeline.Plan);
            bool settleAppearances = _skipRevealMode == TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately;
            bool previousSuppression = _suppressUnitReactions;
            _suppressUnitReactions = true;
            try { _reveal.SkipToEnd(settleAppearances); }
            finally { _suppressUnitReactions = previousSuppression; }
            _meshDirty.SetAllDirty(_animator.StateCount);
            SetDirty(DirtyFlags.Animation);
        }

        public void TriggerRemainingEvents()
        {
            if (_isProcessingPipeline)
                return;

            bool planRefreshed = ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;

            if (planRefreshed || _reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount)
                InitializeReveal(useBuiltInTimings: true);

            _reveal.FlushRemainingMarkers(_pipeline.Plan);
            _reveal.FlushSkippableExtensionTags(_pipeline.Plan);
        }

        public void StartDisappear()
        {
            StartDisappear(default(TextStudioGlyphChangeSet));
        }

        internal void StartDisappear(in TextStudioGlyphChangeSet affectedGlyphs)
        {
            if (_isProcessingPipeline)
                return;

            ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;

            if (_reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount)
                InitializeReveal(useBuiltInTimings: true);

            if (_reveal.RevealedCount == 0)
                _reveal.RevealAll();

            DispatchReaction(TextStudioReactionTrigger.HideStarted);
            _reveal.StartDisappear(affectedGlyphs);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void SkipDisappear()
        {
            if (_skipHideMode == TextStudioTypewriterSkipHideMode.FinishHideEffect && _reveal.IsDisappearing)
                return;

            bool previousSuppression = _suppressUnitReactions;
            _suppressUnitReactions = true;
            try { _reveal.SkipDisappear(); }
            finally { _suppressUnitReactions = previousSuppression; }
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void StopDisappear()
        {
            _reveal.StopDisappear();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public bool AdvanceOrSkip()
        {
            if (_isProcessingPipeline)
                return false;

            if (_reveal.IsDisappearing)
            {
                SkipDisappear();
                return false;
            }

            bool planRefreshed = ProcessPendingTextForReveal();
            if (_pipeline.Plan != null && (planRefreshed || _reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount))
                InitializeReveal(useBuiltInTimings: true);

            if (_reveal.IsRevealing || _reveal.IsPaused ||
                (_reveal.TotalCount > 0 && _reveal.RevealedCount < _reveal.TotalCount))
            {
                SkipCurrentWait();
                SkipReveal();
                return false;
            }

            return true;
        }

        public void SetRevealSpeed(float multiplier)
        {
            _revealSpeedMultiplier = Mathf.Max(0.01f, multiplier);
            _hasRuntimeRevealSpeedOverride = true;
            _reveal.SpeedMultiplier = _revealSpeedMultiplier;
        }

        void ApplyConfiguredRevealSpeed(float multiplier)
        {
            _revealSpeedMultiplier = Mathf.Max(0.01f, multiplier);
            _hasRuntimeRevealSpeedOverride = false;
            _reveal.SpeedMultiplier = _revealSpeedMultiplier;
        }

        public void PauseReveal()
        {
            _reveal.PauseReveal();
            SetDirty(DirtyFlags.Animation);
        }

        public void ResumeReveal()
        {
            _reveal.ResumeReveal();
            SetDirty(DirtyFlags.Animation);
        }

        public void SkipCurrentWait()
        {
            for (int i = 0; i < _activeActionContexts.Count; i++)
                _activeActionContexts[i]?.RequestSkip();

            _reveal.ResumeReveal();
            SetDirty(DirtyFlags.Animation);
        }

        public void NotifyTypewriterWaitStarted(
            ActionMarker marker,
            TextStudioTypewriterWaitKind kind,
            float duration,
            bool isHide)
        {
            WaitStarted?.Invoke(CreateWaitInfo(marker, kind, duration, isHide));
        }

        public void NotifyTypewriterWaitFinished(
            ActionMarker marker,
            TextStudioTypewriterWaitKind kind,
            float duration,
            bool isHide)
        {
            WaitFinished?.Invoke(CreateWaitInfo(marker, kind, duration, isHide));
        }

        public void SetRevealLoop(bool loop, float delay = 0.75f)
        {
            LoopReveal = loop;
            LoopRevealDelay = delay;
        }

        internal float GetRevealProgressForGlyphCount(int glyphCount)
        {
            PrepareRevealForProgress();
            return _reveal.GetProgressForGlyphCount(glyphCount);
        }

        public void SetRevealProgress(float progress)
        {
            PrepareRevealForProgress();

            _reveal.SetProgress(progress);
            SetDirty(DirtyFlags.Animation);
        }

        internal void SetPreviewRevealTime(float elapsedTime)
        {
            PrepareRevealForProgress();

            _reveal.SetScrubElapsedTime(elapsedTime);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public bool SetGlyphVisibility(int glyphIndex, bool visible, bool playTransition = true)
        {
            PrepareRevealForProgress();
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null)
                return false;
            PrepareCurrentVisibilityForTransition(playTransition, plan);

            bool changed = _animator.SetGlyphVisibility(glyphIndex, visible, playTransition);
            if (changed)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            return changed;
        }

        public bool SetWordVisibility(int wordIndex, bool visible, bool playTransition = true)
        {
            PrepareRevealForProgress();
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null || plan.WordRanges == null || wordIndex < 0 || wordIndex >= plan.WordCount)
                return false;
            PrepareCurrentVisibilityForTransition(playTransition, plan);

            WordRange range = plan.WordRanges[wordIndex];
            bool changed = _animator.SetGlyphRangeVisibility(
                range.glyphStart,
                range.glyphCount,
                visible,
                playTransition) > 0;
            if (changed)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            return changed;
        }

        public bool SetTextVisibility(bool visible, bool playTransition = true)
        {
            PrepareRevealForProgress();
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null || plan.VisibleGlyphCount <= 0)
                return false;
            PrepareCurrentVisibilityForTransition(playTransition, plan);

            bool changed = _animator.SetGlyphRangeVisibility(
                0,
                plan.VisibleGlyphCount,
                visible,
                playTransition) > 0;
            if (changed)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            return changed;
        }

        void PrepareCurrentVisibilityForTransition(bool playTransition, TextStudioCompiledPlan plan)
        {
            if (playTransition && plan != null)
                _animator.Evaluate(0f, _reveal, plan);
        }

        public bool ClearGlyphVisibilityOverride(int glyphIndex)
        {
            bool changed = _animator.ClearGlyphVisibilityOverride(glyphIndex);
            if (changed)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            return changed;
        }

        public bool ClearVisibilityOverrides()
        {
            bool changed = _animator.ClearVisibilityOverrides();
            if (changed)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            return changed;
        }

        public void ConfigureEffectFamilies(bool reveal, bool active, bool hide)
        {
            _animator.AppearanceEffectsEnabled = reveal;
            _animator.PersistentEffectsEnabled = active;
            _animator.DisappearanceEffectsEnabled = hide;
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void RestartEffects()
        {
            _animator.RestartEffects();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void CancelPreviewInvocation()
        {
            _actionGeneration++;
            for (int i = 0; i < _activeActionContexts.Count; i++)
                _activeActionContexts[i]?.RequestSkip();

            _animator.ClearVisibilityOverrides();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void RestartPreviewInvocation()
        {
            CancelPreviewInvocation();
            _animator.RestartEffects();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void SetPreviewEffectTime(float elapsedTime)
        {
            _animator.SetPreviewTime(elapsedTime);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void SetPreviewRevealActiveBlendEnabled(bool enabled)
        {
            _reveal.SetRevealActiveBlendEnabledForPreview(enabled);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void RefreshEffectBindings()
        {
            ClearExternalEffectCache();
            ProcessPendingTextForReveal();
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null)
                return;

            _animator.BindEffects(plan);
            _animator.RestartEffects();
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void SetDisappearProgress(float progress)
        {
            PrepareRevealForProgress();

            _reveal.SetDisappearanceProgress(progress);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        internal void PrepareRevealForProgress()
        {
            ProcessPendingTextForReveal();
            if (_pipeline.Plan != null &&
                (_revealScheduleDirty || _reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount))
            {
                InitializeReveal(useBuiltInTimings: true);
            }
        }

        internal void SetPreviewRevealLoopSuppressed(bool suppressed)
        {
            _previewSuppressRevealLoop = suppressed;
            ApplyRevealLoopConfiguration();
        }

        public void DriveRevealProgress(float progress)
        {
            ProcessPendingTextForReveal();
            if (_pipeline.Plan != null && _reveal.TotalCount != _pipeline.Plan.VisibleGlyphCount)
                InitializeReveal(useBuiltInTimings: true);

            _reveal.DriveProgressForward(progress, _pipeline.Plan);
            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public void ResetReveal()
        {
            ResetReactions();
            DiscardTextChangeContinuity();
            _reveal.Reset();
            _animator.Reset();
            _meshDirty.SetAllDirty(_animator.StateCount);
            SetDirty(DirtyFlags.Animation);
            RevealReset?.Invoke(false);
        }

        public void SnapToEndState()
        {
            ProcessPendingTextForReveal();
            if (_pipeline.Plan == null)
                return;

            InitializeReveal(useBuiltInTimings: true);
            _reveal.RevealAll();
            SetDirty(DirtyFlags.Animation);
        }

        internal bool HasCueTimeline => _pipeline.Plan != null &&
            (_pipeline.Plan.ActionMarkerCount > 0 || _pipeline.Plan.ExtensionTagCount > 0);

        public int TickReveal(float deltaTime, bool allowTimedReveal)
        {
            TickReactionClock(deltaTime);
            // Presentation effects can outlive the reveal itself. Their retained glyph ages
            // still advance on active host ticks; idle hosts already skip the whole tick.
            if (!allowTimedReveal)
            {
                SampleReactionsBeforePresentation();
                return 0;
            }

            int revealed = _reveal.Update(deltaTime, _pipeline.Plan);
            if (revealed > 0 || _reveal.ChangedThisUpdate)
                SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);

            SampleReactionsBeforePresentation();
            return revealed;
        }

        public bool EvaluateIfNeeded(float deltaTime)
        {
            LastEvaluationChangedRenderedState = false;
            if (_pipeline.Plan == null)
                return false;

            bool hasActiveEffects = HasContinuousEffects;
            bool hasExternalEffects = _pendingExternalEffects.Count > 0;
            bool hasExternalMaterialEffect = PendingExternalEffectsRequireMaterial();
            TextStudioSourceContextRequirements externalSourceRequirements =
                PendingExternalSourceRequirements();
            bool mustClearExternalEffects = _externalEffectsAppliedLastFrame && !hasExternalEffects;
            if (!_reveal.HasActiveReveal &&
                !hasActiveEffects &&
                !_animator.HasActiveVisibilityTransitions &&
                !hasExternalEffects &&
                !mustClearExternalEffects &&
                !IsDirty(DirtyFlags.Animation))
                return false;

            _animator.Evaluate(deltaTime, _reveal, _pipeline.Plan);
            ApplyPendingExternalEffects();
            _externalEffectsAppliedLastFrame = hasExternalEffects;
            _externalMaterialEffectAppliedLastFrame = hasExternalMaterialEffect;
            _externalSourceRequirementsLastFrame = externalSourceRequirements;
            ClearDirty(DirtyFlags.Animation);

            int count = _animator.StateCount;
            GlyphDirtyBits animDirty = _animator.AnimationDirtyBits;
            if (_animator.AllEvaluatedGlyphsDirty)
            {
                LastEvaluationChangedRenderedState = count > 0;
                _meshDirty.SetAllDirty(count);
            }
            else
            {
                LastEvaluationChangedRenderedState = _meshDirty.MarkDirtyFromAny(
                    count,
                    animDirty,
                    _previousExternalEffectGlyphs,
                    _currentExternalEffectGlyphs);
            }

            GlyphDirtyBits reusableExternalEffectGlyphs = _previousExternalEffectGlyphs;
            _previousExternalEffectGlyphs = _currentExternalEffectGlyphs;
            _currentExternalEffectGlyphs = reusableExternalEffectGlyphs;
            _currentExternalEffectGlyphs.ClearAll();

            return true;
        }

        internal bool PrepareUnanimatedOutput()
        {
            bool hasExternalEffectState =
                _pendingExternalEffects.Count > 0 ||
                _externalEffectsAppliedLastFrame;
            if (!IsDirty(DirtyFlags.Animation) && !hasExternalEffectState)
                return false;

            _animator.Reset();
            _pendingExternalEffects.Clear();
            _externalEffectsAppliedLastFrame = false;
            _externalMaterialEffectAppliedLastFrame = false;
            _externalSourceRequirementsLastFrame = TextStudioSourceContextRequirements.None;
            _previousExternalEffectGlyphs.ClearAll();
            _currentExternalEffectGlyphs.ClearAll();
            _meshDirty.SetAllDirty(_animator.StateCount);
            ClearDirty(DirtyFlags.Animation);
            return true;
        }

        void SetEffectFamilyEnabled(AnimationPhase phase, bool enabled)
        {
            switch (phase)
            {
                case AnimationPhase.Appearance:
                    if (_animator.AppearanceEffectsEnabled == enabled)
                        return;
                    _animator.AppearanceEffectsEnabled = enabled;
                    break;
                case AnimationPhase.Persistent:
                    if (_animator.PersistentEffectsEnabled == enabled)
                        return;
                    _animator.PersistentEffectsEnabled = enabled;
                    break;
                case AnimationPhase.Disappearance:
                    if (_animator.DisappearanceEffectsEnabled == enabled)
                        return;
                    _animator.DisappearanceEffectsEnabled = enabled;
                    break;
                default:
                    return;
            }

            SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        public bool ApplyExternalEffect(
            ITextStudioEffectDefinition effect,
            float progress,
            float intensity,
            TextStudioTextRange range)
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (effect == null || plan == null || plan.VisibleGlyphCount <= 0 ||
                float.IsNaN(progress) || float.IsInfinity(progress) ||
                float.IsNaN(intensity) || float.IsInfinity(intensity))
                return false;

            if (!range.IsValid)
                return false;
            range.Resolve(plan.VisibleGlyphCount, out int resolvedStart, out int resolvedEnd);
            if (resolvedEnd <= resolvedStart) return false;

            if (intensity <= 0f)
                return true;

            EnsureExternalEffectCacheCurrent();
            if (!_externalEffectPlans.TryGetValue(effect, out ExternalEffectPlan externalPlan))
            {
                if (!TryBuildExternalEffectPlan(effect, out externalPlan))
                    return false;
                _externalEffectPlans.Add(effect, externalPlan);
            }

            bool beginsExternalEffectSession = !HasExternalEffectState && !ReactionsRequireGeometry;
            bool introducesMaterialRequirement =
                externalPlan.RequiresMaterial &&
                !RequiresExternalMaterialEffect;
            TextStudioSourceContextRequirements availableSourceRequirements =
                ExternalSourceContextRequirements;
            bool introducesSourceRequirement =
                (externalPlan.SourceRequirements & ~availableSourceRequirements) !=
                TextStudioSourceContextRequirements.None;
            _pendingExternalEffects.Add(new ExternalEffectRequest(
                externalPlan,
                Mathf.Clamp01(intensity),
                Mathf.Clamp01(progress),
                resolvedStart,
                resolvedEnd));
            SetDirty(
                DirtyFlags.Animation |
                (beginsExternalEffectSession ||
                 introducesMaterialRequirement ||
                 introducesSourceRequirement
                    ? DirtyFlags.Layout
                    : DirtyFlags.None));
            return true;
        }

        void ApplyPendingExternalEffects()
        {
            int count = _animator.StateCount;
            _currentExternalEffectGlyphs.EnsureCapacity(count);
            for (int requestIndex = 0; requestIndex < _pendingExternalEffects.Count; requestIndex++)
            {
                ExternalEffectRequest request = _pendingExternalEffects[requestIndex];
                int start = Mathf.Clamp(request.StartUnit, 0, count);
                int end = Mathf.Clamp(request.EndUnitExclusive, start, count);
                for (int glyphIndex = start; glyphIndex < end; glyphIndex++)
                {
                    _currentExternalEffectGlyphs.SetDirty(glyphIndex);
                    EvaluatedGlyphState beforeRequest = _animator.States[glyphIndex];
                    TextStudioRenderPropertySink propertySink = null;
                    if (request.Intensity < 1f)
                    {
                        _externalPropertiesBefore ??= new TextStudioRenderPropertyGlyphAccumulator();
                        _externalPropertiesAfter ??= new TextStudioRenderPropertyGlyphAccumulator();
                        TextStudioRenderPropertyView properties = _animator.RenderProperties;
                        _externalPropertiesBefore.Begin(glyphIndex, in properties);
                        _externalPropertiesAfter.Begin(glyphIndex, in properties);
                        propertySink = _externalPropertiesAfter;
                    }
                    ExternalEffectBinding[] bindings = request.Plan.Bindings;
                    for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                    {
                        ExternalEffectBinding binding = bindings[bindingIndex];
                        _animator.ApplyExternalEffect(
                            _pipeline.Plan,
                            glyphIndex,
                            start,
                            end - start,
                            binding.Effect,
                            binding.Area,
                            request.Progress,
                            binding.TimeOffset,
                            binding.Intensity,
                            binding.Scope,
                            binding.Envelope,
                            propertySink);
                    }

                    if (request.Intensity < 1f)
                    {
                        _animator.States[glyphIndex] = EvaluatedGlyphState.Lerp(
                            beforeRequest,
                            _animator.States[glyphIndex],
                            request.Intensity);
                        _animator.BlendExternalRenderProperties(
                            glyphIndex, _externalPropertiesBefore, _externalPropertiesAfter, request.Intensity);
                    }
                }
            }

            _pendingExternalEffects.Clear();
        }

        bool TryBuildExternalEffectPlan(
            ITextStudioEffectDefinition definition,
            out ExternalEffectPlan plan)
        {
            _externalResolutionBuffer.Clear();
            if (!_externalEffectResolver.TryResolve(
                    definition,
                    TextStudioPhaseMask.Active,
                    _externalResolutionBuffer,
                    null) ||
                _externalResolutionBuffer.Count == 0)
            {
                plan = null;
                return false;
            }

            ExternalEffectBinding[] bindings = new ExternalEffectBinding[_externalResolutionBuffer.Count];
            bool requiresMaterial = false;
            TextStudioSourceContextRequirements sourceRequirements =
                TextStudioSourceContextRequirements.None;
            for (int i = 0; i < _externalResolutionBuffer.Count; i++)
            {
                TextStudioResolvedEffectBinding resolved = _externalResolutionBuffer[i];
                ITextStudioEffect boundEffect = resolved.Effect;
                if (boundEffect == null &&
                         !TextStudioEffects.TryCreate(
                             resolved.EffectId,
                             in resolved.Parameters,
                             out boundEffect))
                {
                    plan = null;
                    return false;
                }

                if (boundEffect == null)
                {
                    plan = null;
                    return false;
                }

                TextStudioEffectAreaSettings area = resolved.HasAuthoringOverrides
                    ? resolved.Area
                    : boundEffect.DefaultArea;
                bindings[i] = new ExternalEffectBinding(
                    boundEffect,
                    Mathf.Clamp01(resolved.Intensity),
                    resolved.TimeOffset,
                    area,
                    resolved.Scope,
                    resolved.Envelope);
                if (resolved.Scope != null)
                    sourceRequirements |= resolved.Scope.Requirements;
                if (boundEffect is ITextStudioEffectCapabilitiesProvider provider &&
                    provider.Capabilities.PerformanceTier ==
                    TextStudioEffectPerformanceTier.Material)
                {
                    requiresMaterial = true;
                }
                if (RequiresSourceTextBounds(boundEffect))
                {
                    sourceRequirements |=
                        TextStudioSourceContextRequirements.SourceTextNormalizedPosition;
                }
            }

            plan = new ExternalEffectPlan(
                bindings,
                requiresMaterial,
                sourceRequirements);
            return true;
        }

        bool PendingExternalEffectsRequireMaterial()
        {
            for (int i = 0; i < _pendingExternalEffects.Count; i++)
            {
                if (_pendingExternalEffects[i].Plan.RequiresMaterial)
                    return true;
            }
            return false;
        }

        TextStudioSourceContextRequirements PendingExternalSourceRequirements()
        {
            TextStudioSourceContextRequirements requirements =
                TextStudioSourceContextRequirements.None;
            for (int i = 0; i < _pendingExternalEffects.Count; i++)
                requirements |= _pendingExternalEffects[i].Plan.SourceRequirements;
            return requirements;
        }

        static bool RequiresSourceTextBounds(ITextStudioEffect effect)
        {
            if (effect is BurnEffect burn)
                return string.Equals(burn.space, "text", StringComparison.OrdinalIgnoreCase);
            return effect is DissolveEffect dissolve &&
                   string.Equals(dissolve.space, "text", StringComparison.OrdinalIgnoreCase);
        }

        void EnsureExternalEffectCacheCurrent()
        {
            uint registryRevision = TextStudioRegistry.Effects.Revision;
            uint contentRevision = TextStudioEffectDefinitionAsset.ContentRevision;
            if (_externalEffectRegistryRevision == registryRevision &&
                _externalEffectContentRevision == contentRevision)
                return;
            ClearExternalEffectCache();
        }

        void ClearExternalEffectCache()
        {
            _externalEffectPlans.Clear();
            _externalResolutionBuffer.Clear();
            _externalEffectRegistryRevision = TextStudioRegistry.Effects.Revision;
            _externalEffectContentRevision = TextStudioEffectDefinitionAsset.ContentRevision;
        }

        public void SkipTypewriter()
        {
            if (_reveal.IsDisappearing)
                SkipDisappear();
            else
                SkipReveal();
        }

        public void StartShowing()
        {
            StartReveal();
        }

        public void StopShowing()
        {
            DiscardTextChangeContinuity();
            SkipCurrentWait();
            _reveal.StopDisappear();
            _reveal.Reset();
            _animator.Reset();
            SetDirty(DirtyFlags.Animation);
        }

        public void SetTypewriterSpeed(float multiplier)
        {
            SetRevealSpeed(multiplier);
        }

        public float GetApproximateShowDuration()
        {
            return GetApproximateShowDuration(_pipeline.Plan);
        }

        public float GetApproximateHideDuration()
        {
            return _reveal.GetApproximateHideDuration();
        }

        internal float GetApproximateVisualShowDuration()
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            float actionDuration = GetDeterministicActionDuration(plan);
            float revealDuration = _reveal.GetApproximateVisualRevealDuration() + actionDuration;
            float substitutionDuration = GetCharacterSubstitutionCompletionDuration(plan);
            return Mathf.Max(revealDuration, substitutionDuration);
        }

        public TextStudioRevealDebugSnapshot CreateRevealDebugSnapshot(
            PlacementMode placementMode,
            int rendererCount,
            bool hasExternalTextSource,
            int externalTextSourceCount)
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            return new TextStudioRevealDebugSnapshot(
                hasPlan: plan != null,
                markup: Markup,
                resolvedText: plan != null ? plan.ResolvedText : string.Empty,
                revealMode: _revealMode,
                placementMode: placementMode,
                revealedGlyphCount: _reveal.RevealedCount,
                totalGlyphCount: _reveal.TotalCount,
                revealProgress: _reveal.RevealProgress,
                speedMultiplier: _reveal.SpeedMultiplier,
                approximateDuration: GetApproximateShowDuration(plan),
                isRevealing: _reveal.IsRevealing,
                isComplete: _reveal.IsComplete,
                isPaused: _reveal.IsPaused,
                isLoopWaiting: _reveal.IsLoopWaiting,
                activeActionCount: _activeActionContexts.Count,
                actionMarkerCount: plan != null ? plan.ActionMarkerCount : 0,
                effectBindingCount: plan != null ? plan.EffectBindingCount : 0,
                rendererCount: rendererCount,
                hasExternalTextSource: hasExternalTextSource,
                externalTextSourceCount: externalTextSourceCount,
                wordCount: plan != null ? plan.WordCount : 0,
                hasVisibilityOverrides: HasVisibilityOverrides,
                revealEffectsEnabled: RevealEffectsEnabled,
                activeEffectsEnabled: ActiveEffectsEnabled,
                hideEffectsEnabled: HideEffectsEnabled);
        }

        internal IEnumerator ExecuteAction(ITextStudioAction action, ActionContext context)
        {
            IEnumerator routine;
            try
            {
                routine = action.Execute(context);
            }
            catch (Exception ex)
            {
                LogException(ex);
                yield break;
            }

            if (routine == null)
                yield break;

            int actionGeneration = _actionGeneration;
            bool started = false;
            _activeActionContexts.Add(context);
            try
            {
                while (actionGeneration == _actionGeneration)
                {
                    // Enter the action immediately so blocking cues can pause the reveal cursor,
                    // even when the first visible segment is evaluated at time zero.
                    if (started && _actionRunner != null && _actionRunner.DeltaTime <= 0f)
                    {
                        yield return null;
                        continue;
                    }

                    bool moveNext;
                    object current;
                    try
                    {
                        started = true;
                        moveNext = routine.MoveNext();
                        current = moveNext ? routine.Current : null;
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                        break;
                    }

                    if (!moveNext)
                        break;

                    yield return current;
                }
            }
            finally
            {
                try
                {
                    if (routine is IDisposable disposable)
                        disposable.Dispose();
                }
                finally { _activeActionContexts.Remove(context); }
            }
        }

        float GetApproximateShowDuration(TextStudioCompiledPlan plan)
        {
            float revealDuration = _reveal.GetApproximateDuration() + GetDeterministicActionDuration(plan);
            float substitutionDuration = GetCharacterSubstitutionCompletionDuration(plan);
            return Mathf.Max(revealDuration, substitutionDuration);
        }

        static float GetDeterministicActionDuration(TextStudioCompiledPlan plan)
        {
            return GetDeterministicActionDurationThroughGlyph(plan, int.MaxValue);
        }

        static float GetDeterministicActionDurationThroughGlyph(TextStudioCompiledPlan plan, int glyphIndex)
        {
            if (plan == null || plan.ActionMarkers == null || plan.ActionMarkerCount == 0)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < plan.ActionMarkerCount; i++)
            {
                ActionMarker marker = plan.ActionMarkers[i];
                if (marker.sourceIndex > glyphIndex)
                    break;

                if (!string.Equals(marker.actionId, BuiltInActions.Wait.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                duration += GetWaitDuration(marker);
            }

            return duration;
        }

        float GetCharacterSubstitutionCompletionDuration(TextStudioCompiledPlan plan)
        {
            if (plan == null ||
                plan.CharacterSubstitutionBindings == null ||
                plan.CharacterSubstitutionBindingCount == 0 ||
                string.IsNullOrEmpty(plan.ResolvedText))
            {
                return 0f;
            }

            float duration = 0f;
            for (int i = 0; i < plan.CharacterSubstitutionBindingCount; i++)
            {
                CharacterSubstitutionBinding binding = plan.CharacterSubstitutionBindings[i];
                if (!TextStudioRegistry.CharacterSubstitutions.TryGetQuiet(
                        binding.effectId,
                        out ITextStudioCharacterSubstitution substitution) ||
                    substitution == null)
                {
                    continue;
                }

                duration = Mathf.Max(
                    duration,
                    substitution.GetCompletionDuration(new TextStudioPlanView(plan), in binding, _reveal));
            }

            return duration;
        }

        static float GetWaitDuration(ActionMarker marker)
        {
            marker.TryGetFloat(0, out float seconds);
            return seconds > 0f ? seconds : BuiltInWaitAction.DefaultSeconds;
        }

        TextStudioTypewriterWaitInfo CreateWaitInfo(
            ActionMarker marker,
            TextStudioTypewriterWaitKind kind,
            float duration,
            bool isHide)
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            int glyphIndex = Mathf.Max(0, marker.sourceIndex);
            char character = '\0';
            if (plan != null && !string.IsNullOrEmpty(plan.ResolvedText))
            {
                int unitIndex = Mathf.Clamp(glyphIndex, 0, Mathf.Max(0, plan.VisibleGlyphCount - 1));
                character = plan.GetPrimaryChar(unitIndex);
            }

            return new TextStudioTypewriterWaitInfo(
                kind,
                glyphIndex,
                character,
                isHide,
                marker.actionId,
                Mathf.Max(0f, duration));
        }

        void InitializeReveal(bool useBuiltInTimings)
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null)
                return;

            _hasPreparedTextChangeContinuity = false;

            // Instant text shows every glyph at once, so built-in delays never apply and its schedule can take the single-pass
            // setup; changing the mode marks the schedule dirty, and a cue timeline still reads its delays.
            CodeTimings preset = useBuiltInTimings && (_revealMode != RevealMode.Instant || HasCueTimeline)
                ? BuiltInTimings.For(_builtInTimingsPreset)
                : null;
            _reveal.Mode = _revealMode;
            _reveal.SpeedMultiplier = _revealSpeedMultiplier;
            _reveal.AppearanceDuration = _revealAppearanceDuration;
            _reveal.DisappearanceDuration = _revealDisappearanceDuration;
            _reveal.RevealSequence = _revealSequence;
            _reveal.HideSequence = _hideSequence;
            ApplyRevealLoopConfiguration();
            _reveal.Initialize(
                plan,
                _timings,
                preset != null ? preset.GetRevealDelayAfter : null,
                _waitAfterFinalReveal);
            _revealScheduleDirty = false;
        }

        void ApplyRevealConfiguration()
        {
            _reveal.Mode = _revealMode;
            _reveal.SpeedMultiplier = _revealSpeedMultiplier;
            _reveal.AppearanceDuration = _revealAppearanceDuration;
            _reveal.DisappearanceDuration = _revealDisappearanceDuration;
            _reveal.RevealSequence = _revealSequence;
            _reveal.HideSequence = _hideSequence;
            ApplyRevealLoopConfiguration();
        }

        void ApplyRevealLoopConfiguration()
        {
            _reveal.Loop = _loopReveal && !_previewSuppressRevealLoop;
            _reveal.LoopDelay = _loopRevealDelay;
        }

        void ForwardGlyphRevealed(int glyphIndex)
        {
            TextStudioCompiledPlan plan = _pipeline.Plan;
            if (plan == null || string.IsNullOrEmpty(plan.ResolvedText) ||
                glyphIndex < 0 || glyphIndex >= plan.VisibleGlyphCount)
                return;

            if (_reactions != null) DispatchReaction(TextStudioReactionTrigger.UnitRevealed, glyphIndex, age: _reveal.GetAppearanceElapsed(glyphIndex) / Mathf.Max(.0001f, _revealSpeedMultiplier));
            GlyphRevealed?.Invoke(glyphIndex, plan.GetPrimaryChar(glyphIndex));
            TextUnitRevealed?.Invoke(glyphIndex, plan.GetUnitText(glyphIndex));
        }

        void ForwardGlyphAppearanceCompleted(int glyphIndex)
        {
            if (_reactions != null)
                DispatchReaction(TextStudioReactionTrigger.UnitRevealComplete, glyphIndex);
        }

        void ForwardGlyphDisappeared(int glyphIndex)
        {
            if (_reactions != null) DispatchReaction(TextStudioReactionTrigger.UnitHidden, glyphIndex,
                age: _reveal.HideCompletionOverrun / Mathf.Max(.0001f, _revealSpeedMultiplier));
            GlyphDisappeared?.Invoke(glyphIndex);
        }

        void ForwardGlyphDisappearStarted(int glyphIndex)
        {
            if (_reactions != null)
                DispatchReaction(TextStudioReactionTrigger.UnitHideStarted, glyphIndex);
        }

        void ForwardActionMarkerReached(ActionMarker marker)
        {
            if (_reactions != null)
            {
                TextStudioCompiledPlan plan = _pipeline.Plan;
                int unit = plan != null && marker.sourceIndex >= 0 && marker.sourceIndex < plan.ResolvedText.Length
                    ? plan.GetUnitIndexForResolvedChar(marker.sourceIndex) : -1;
                string reactionName = marker.actionId;
                if (string.Equals(reactionName, TextStudioBuiltInActionIds.Event, StringComparison.Ordinal) &&
                    marker.parameters != null && marker.parameters.Length > 0 &&
                    !string.IsNullOrEmpty(marker.parameters[0]))
                    reactionName = marker.parameters[0];
                DispatchReaction(TextStudioReactionTrigger.Marker, unit, reactionName);
            }
            string id = marker.actionId;
            string[] parameters = marker.parameters;
            ITextStudioAction action = null;
            bool hasAction = !string.IsNullOrEmpty(id) &&
                             (TryGetLocalAction(id, out action) ||
                              TextStudioRegistry.Actions.TryGet(id, out action));
            bool isEditModePreview = _actionRunner != null && _actionRunner.IsEditModePreview && !Application.isPlaying;
            bool canRunInEditModePreview = !isEditModePreview ||
                                           _runActionsInEditModePreview ||
                                           action is ITextStudioEditModePreviewAction;
            bool canExecuteInCurrentContext = hasAction &&
                                              canRunInEditModePreview;

            if (canExecuteInCurrentContext)
            {
                if (_actionRunner != null && _actionRunner.CanRunActions)
                {
                    ActionContext context = new ActionContext(marker, this) { Owner = ActionOwner };
                    context.IsEditModePreview = _actionRunner.IsEditModePreview;
                    context.SetDeltaTimeProvider(() => _actionRunner.DeltaTime);
                    _actionRunner.Run(ExecuteAction(action, context));
                }
            }

            if (isEditModePreview && (!_runActionsInEditModePreview || !hasAction || !canExecuteInCurrentContext))
                return;

            MarkerReached?.Invoke(id, parameters);
        }

        bool TryGetLocalAction(string actionId, out ITextStudioAction action)
        {
            action = null;
            if (string.IsNullOrEmpty(actionId))
                return false;

            for (int i = 0; i < _localActions.Count; i++)
            {
                ITextStudioAction candidate = _localActions[i];
                if (candidate is UnityEngine.Object unityObject && unityObject == null) continue;
                if (candidate is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                if (!TextStudioActionDefinitionUtility.MatchesName(candidate, actionId))
                    continue;

                action = candidate;
                return true;
            }

            return false;
        }

        void ForwardExtensionTagReached(TextStudioExtensionTag tag)
        {
            ExtensionTagReached?.Invoke(tag);

            for (int i = 0; i < _extensionTagProcessors.Count; i++)
            {
                ITextStudioExtensionTagProcessor processor = _extensionTagProcessors[i];
                if (processor == null)
                    continue;

                try { processor.OnExtensionTagReached(in tag); }
                catch (Exception ex) { LogException(ex); }
            }
        }

        void NotifyExtensionTagsChanged(TextStudioCompiledPlan plan)
        {
            TextStudioPlanView planView = new TextStudioPlanView(plan);
            ExtensionTagsChanged?.Invoke(planView);

            for (int i = 0; i < _extensionTagProcessors.Count; i++)
            {
                try { _extensionTagProcessors[i]?.OnExtensionTagsChanged(planView); }
                catch (Exception ex) { LogException(ex); }
            }
        }

        void ForwardRevealComplete()
        {
            if (_reactions != null) DispatchReaction(TextStudioReactionTrigger.ShowComplete,
                age: _reveal.ShowCompletionOverrun / Mathf.Max(.0001f, _revealSpeedMultiplier));
            ShowComplete?.Invoke();
        }

        void ForwardDisappearComplete()
        {
            if (_reactions != null) DispatchReaction(TextStudioReactionTrigger.HideComplete,
                age: _reveal.HideCompletionOverrun / Mathf.Max(.0001f, _revealSpeedMultiplier));
            DisappearComplete?.Invoke();
        }

        void LogException(Exception exception)
        {
            if (_actionRunner != null)
                _actionRunner.LogException(exception);
            else
                Debug.LogException(exception);
        }
    }

}
