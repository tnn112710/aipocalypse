using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Evaluates all active effects per glyph, producing the final EvaluatedGlyphState array.
    /// Uses dirty-region-only evaluation: skips unchanged glyphs.
    /// Three-phase model: Appearance → Persistent → Disappearance.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal sealed partial class AnimationEvaluator
    {
        static readonly Dictionary<int, EvaluatedGlyphState[]> s_persistentIdentityStates =
            new Dictionary<int, EvaluatedGlyphState[]>();
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly EvaluatedGlyphState s_persistentIdentity = CreatePersistentIdentity();

        EvaluatedGlyphState[] _states;
        // States this evaluator owns, kept while it publishes shared identity states so copy-on-write reuses them.
        EvaluatedGlyphState[] _writableStates;
        bool _statesAreShared;
        bool _resetSharedStatesOnWrite;
        int _stateCount;
        float _time;
        float _playbackTime;
        readonly GlyphDirtyBits _animDirty = new GlyphDirtyBits();
        TextStudioRenderPropertyBuffer _renderProperties =
            new TextStudioRenderPropertyBuffer();
        TextStudioRenderPropertyBuffer _previousRenderProperties =
            new TextStudioRenderPropertyBuffer();
        readonly TextStudioRenderPropertyGlyphAccumulator _revealRenderProperties =
            new TextStudioRenderPropertyGlyphAccumulator();
        readonly TextStudioRenderPropertyGlyphAccumulator _activeRenderProperties =
            new TextStudioRenderPropertyGlyphAccumulator();
        int _renderPropertyRegistrationVersion = -1;
        bool _renderPropertyRegistryChanged;

        // Bound effects for current text
        BoundEffect[] _boundEffects;
        int _boundEffectCount;
        readonly TextStudioSelectionResolver _effectSelectionResolver = new TextStudioSelectionResolver();
        int _cpuBoundEffectCount;
        int _rendererDeferredBoundEffectCount;
        int _settledOutputBoundEffectCount;
        bool _allBoundEffectsApplyToPersistentPhase = true;
        bool _allBoundEffectsSupportPersistentFastPath = true;
        int[] _glyphEffectStart;
        int[] _glyphEffectCount;
        int[] _glyphEffectWrite;
        int[] _glyphEffectIndices;
        float[] _glyphEffectDelays;
        TextStudioEffectUnitContext[] _glyphEffectUnits;
        bool[] _glyphEffectReusesPreviousContext;
        bool _glyphEffectLookupDirty = true;
        bool _hasUniformSinglePersistentEffect;
        bool _hasSharedPersistentTiming;
        bool _hasUniformPersistentState;
        bool _evaluatedStatesAreUniform;
        bool _hasEvaluatedSinglePersistentFrame;
        // Shortcut that produced the last single persistent frame, valid only while _hasEvaluatedSinglePersistentFrame is set.
        PersistentShortcut _reusableSingleShortcut;
        GlyphVisibilityOverride[] _visibilityOverrides;
        int _visibilityOverrideCount;
        int _activeVisibilityTransitionCount;
        bool _allEvaluatedGlyphsDirty;
        bool _appearanceEffectsEnabled = true;
        bool _persistentEffectsEnabled = true;
        bool _disappearanceEffectsEnabled = true;
        Vector3[] _sourcePositions;
        TextStudioGlyphVisualBounds[] _sourceVisualBounds;
        Vector3[] _sourceTextNormalizedPositions;
        TextStudioGlyphColorQuad[] _sourceColors;
        bool[] _sourceColorValidity;
        float[] _effectUnitScales;
        GlyphPlacement[] _placements;
        int _spatialCount;
        int _sourceLayoutRevision;
        readonly Dictionary<long, TextStudioGlyphVisualBounds> _sourceAreaBoundsCache =
            new Dictionary<long, TextStudioGlyphVisualBounds>();
        int _placementCount;
        bool _usePlacements;
        Matrix4x4 _localToWorld = Matrix4x4.identity;
        bool _hasInfluenceTarget;
        Vector3 _influenceTargetLocalPosition;
        Vector3 _influenceTargetWorldPosition;
        Vector3 _influenceTargetSourceTextNormalizedPosition;
        TextStudioMotionPolicy _motionPolicy = TextStudioMotionPolicy.Inherit;

        public EvaluatedGlyphState[] States => _states;
        public int StateCount => _stateCount;
        public GlyphDirtyBits AnimationDirtyBits => _animDirty;
        public bool HasVisibilityOverrides => _visibilityOverrideCount > 0;
        public bool HasActiveVisibilityTransitions => _activeVisibilityTransitionCount > 0;

        // Every glyph held hidden without a running transition lets plain output drop the text whole.
        internal bool HidesEveryGlyphByOverride()
        {
            if (_stateCount == 0 ||
                _visibilityOverrideCount != _stateCount ||
                _activeVisibilityTransitionCount != 0)
            {
                return false;
            }

            for (int i = 0; i < _stateCount; i++)
            {
                if (_visibilityOverrides[i].target >= 0)
                    return false;
            }

            return true;
        }
        public bool AllEvaluatedGlyphsDirty => _allEvaluatedGlyphsDirty;
        public bool EvaluatedStatesAreUniform => _evaluatedStatesAreUniform;
        public TextStudioRenderPropertyView RenderProperties => _renderProperties.View;
        internal float ContinuousTimeForDebug => _time;
        internal float PlaybackTimeForDebug => _playbackTime;

        public TextStudioMotionPolicy MotionPolicy
        {
            get => _motionPolicy;
            set
            {
                if (_motionPolicy == value)
                    return;
                _motionPolicy = value;
                _animDirty.SetAllDirty(_stateCount);
            }
        }

        public bool AppearanceEffectsEnabled
        {
            get => _appearanceEffectsEnabled;
            set => SetEffectFamilyEnabled(ref _appearanceEffectsEnabled, value);
        }

        public bool PersistentEffectsEnabled
        {
            get => _persistentEffectsEnabled;
            set => SetEffectFamilyEnabled(ref _persistentEffectsEnabled, value);
        }

        public bool DisappearanceEffectsEnabled
        {
            get => _disappearanceEffectsEnabled;
            set => SetEffectFamilyEnabled(ref _disappearanceEffectsEnabled, value);
        }

        internal void SetSpatialContext(
            Vector3[] sourcePositions,
            Vector3[] sourceTextNormalizedPositions,
            TextStudioGlyphColorQuad[] sourceColors,
            bool[] sourceColorValidity,
            int glyphCount,
            GlyphPlacement[] placements,
            int placementCount,
            PlacementMode placementMode,
            Matrix4x4 localToWorld,
            bool hasInfluenceTarget,
            Vector3 influenceTargetLocalPosition,
            Vector3 influenceTargetWorldPosition,
            Vector3 influenceTargetSourceTextNormalizedPosition = default,
            TextStudioGlyphVisualBounds[] sourceVisualBounds = null,
            int sourceLayoutRevision = 0,
            float[] effectUnitScales = null)
        {
            bool sourceBoundsChanged =
                !ReferenceEquals(_sourcePositions, sourcePositions) ||
                !ReferenceEquals(_sourceVisualBounds, sourceVisualBounds) ||
                _sourceLayoutRevision != sourceLayoutRevision ||
                _spatialCount != Mathf.Max(0, glyphCount);
            _sourcePositions = sourcePositions;
            _sourceVisualBounds = sourceVisualBounds;
            _sourceTextNormalizedPositions = sourceTextNormalizedPositions;
            _sourceColors = sourceColors;
            _sourceColorValidity = sourceColorValidity;
            _effectUnitScales = effectUnitScales;
            _placements = placements;
            _spatialCount = Mathf.Max(0, glyphCount);
            _sourceLayoutRevision = sourceLayoutRevision;
            if (sourceBoundsChanged)
                _sourceAreaBoundsCache.Clear();
            _placementCount = Mathf.Max(0, placementCount);
            _usePlacements = placements != null && _placementCount > 0;
            _localToWorld = localToWorld;
            _hasInfluenceTarget = hasInfluenceTarget;
            _influenceTargetLocalPosition = influenceTargetLocalPosition;
            _influenceTargetWorldPosition = influenceTargetWorldPosition;
            _influenceTargetSourceTextNormalizedPosition =
                influenceTargetSourceTextNormalizedPosition;
        }

        struct BoundEffect
        {
            public ITextStudioEffect effect;
            public int sourceBindingIndex;
            public bool rendererDeferred;
            public int glyphStart;
            public int glyphEnd; // exclusive
            public AnimationPhase forcePhase; // None = use reveal phase
            public TextStudioPhaseMask phaseMask;
            public PhaseParams phaseParams;
            public float phaseSpeed;
            public float intensity;
            public float timeOffset;
            public TextStudioEffectAreaSettings area;
            public TextStudioSequenceSettings sequence;
            public TextStudioRenderChannels renderChannels;
            public TextStudioEffectCapabilities capabilities;
            public bool appliesToPersistentPhase;
            public bool writesRenderProperties;
            public int phaseBlendGroupId;
            public float revealActiveBlendDuration;
            public TextStudioPhaseMask revealActiveBlendPhaseMask;
            public float revealActiveBlendTimeOffset;
            public TextStudioEffectScopeChain scope;
            public bool[] selectorMembers;
            public TextStudioComposedEnvelopeStack envelope;
            // Resolved at the start of each fast stack evaluation together with the shortcut's frame-invariant direction or pivot offset.
            public PersistentShortcut persistentShortcut;
            public Vector2 persistentShortcutVector;
        }

        // Playback-free persistent shortcuts that compute exactly what an effect's general fast path computes with an envelope of 1.
        enum PersistentShortcut : byte
        {
            None,
            Wave,
            Swing,
            Pendulum,
            Size,
            Wiggle,
            Bounce,
            Dangle
        }

        struct GlyphVisibilityOverride
        {
            public sbyte target;
            public AnimationPhase transitionPhase;
            public float transitionStartTime;
        }

        internal readonly struct GlyphVisibilitySnapshot
        {
            internal readonly sbyte Target;
            internal readonly AnimationPhase TransitionPhase;
            internal readonly float TransitionStartTime;

            internal GlyphVisibilitySnapshot(
                sbyte target,
                AnimationPhase transitionPhase,
                float transitionStartTime)
            {
                Target = target;
                TransitionPhase = transitionPhase;
                TransitionStartTime = transitionStartTime;
            }
        }

        internal readonly struct TextChangeState
        {
            internal readonly bool IsValid;
            internal readonly float Time;
            internal readonly float PlaybackTime;
            internal readonly GlyphVisibilitySnapshot[] VisibilityOverrides;

            internal TextChangeState(
                float time,
                float playbackTime,
                GlyphVisibilitySnapshot[] visibilityOverrides)
            {
                IsValid = true;
                Time = time;
                PlaybackTime = playbackTime;
                VisibilityOverrides = visibilityOverrides;
            }
        }

        enum LaneBlendEvaluation
        {
            None,
            Skip,
            Reveal,
            Blend,
            Active
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPersistentIdentityStateCache()
        {
            s_persistentIdentityStates.Clear();
        }

        public void Initialize(int glyphCount, bool preservePlaybackTime = false)
        {
            float previousTime = _time;
            float previousPlaybackTime = _playbackTime;
            bool preserveVisibilityOverrides =
                preservePlaybackTime && _stateCount == glyphCount;
            if (_statesAreShared)
            {
                int capacity = _states != null && _states.Length >= glyphCount
                    ? _states.Length
                    : TextStudioBufferCapacity.ForCount(glyphCount);
                _states = GetPersistentIdentityStates(capacity);
                _statesAreShared = capacity > 0;
                _resetSharedStatesOnWrite = _statesAreShared;
            }
            else if (_states == null || _states.Length < glyphCount)
            {
                _states = new EvaluatedGlyphState[TextStudioBufferCapacity.ForCount(glyphCount)];
                _resetSharedStatesOnWrite = false;
            }

            _stateCount = glyphCount;
            ResetRenderPropertyBuffers(glyphCount);
            _time = preservePlaybackTime ? previousTime : 0f;
            _playbackTime = preservePlaybackTime ? previousPlaybackTime : 0f;
            _animDirty.EnsureCapacity(glyphCount);
            _animDirty.SetAllDirty(glyphCount);
            _glyphEffectLookupDirty = true;
            if (_visibilityOverrides != null)
            {
                if (_visibilityOverrides.Length < glyphCount)
                {
                    _visibilityOverrides = new GlyphVisibilityOverride[
                        TextStudioBufferCapacity.ForCount(glyphCount)];
                    preserveVisibilityOverrides = false;
                }
                else if (glyphCount > 0)
                {
                    if (!preserveVisibilityOverrides)
                        Array.Clear(_visibilityOverrides, 0, glyphCount);
                }
            }
            if (!preserveVisibilityOverrides)
            {
                _visibilityOverrideCount = 0;
                _activeVisibilityTransitionCount = 0;
            }
            _allEvaluatedGlyphsDirty = false;
            _hasEvaluatedSinglePersistentFrame = false;

            if (!_statesAreShared)
            {
                for (int i = 0; i < glyphCount; i++)
                    _states[i] = EvaluatedGlyphState.Identity;
            }
        }

        internal TextChangeState CaptureTextChangeState(
            TextStudioGlyphChangeSet glyphChanges)
        {
            if (!glyphChanges.HasRetainedGlyphs)
                return default;

            int incomingCount = glyphChanges.IncomingGlyphCount;
            GlyphVisibilitySnapshot[] visibility = _visibilityOverrideCount > 0
                ? new GlyphVisibilitySnapshot[incomingCount]
                : null;
            if (visibility != null)
            {
                for (int incomingIndex = 0; incomingIndex < incomingCount; incomingIndex++)
                {
                    int outgoingIndex = glyphChanges.GetOutgoingGlyphForIncoming(incomingIndex);
                    if (_visibilityOverrides == null ||
                        outgoingIndex < 0 ||
                        outgoingIndex >= _stateCount)
                    {
                        continue;
                    }

                    GlyphVisibilityOverride source = _visibilityOverrides[outgoingIndex];
                    visibility[incomingIndex] = new GlyphVisibilitySnapshot(
                        source.target,
                        source.transitionPhase,
                        source.transitionStartTime);
                }
            }

            return new TextChangeState(_time, _playbackTime, visibility);
        }

        internal void RestoreTextChangeState(in TextChangeState state)
        {
            if (!state.IsValid)
                return;

            _time = state.Time;
            _playbackTime = state.PlaybackTime;
            GlyphVisibilitySnapshot[] visibility = state.VisibilityOverrides;
            if (visibility == null || visibility.Length == 0)
                return;

            EnsureVisibilityOverrideCapacity();
            int count = Mathf.Min(_stateCount, visibility.Length);
            for (int i = 0; i < count; i++)
            {
                GlyphVisibilitySnapshot source = visibility[i];
                if (source.Target == 0)
                    continue;

                _visibilityOverrides[i] = new GlyphVisibilityOverride
                {
                    target = source.Target,
                    transitionPhase = source.TransitionPhase,
                    transitionStartTime = source.TransitionStartTime
                };
                _visibilityOverrideCount++;
                if (source.TransitionPhase != AnimationPhase.None)
                    _activeVisibilityTransitionCount++;
            }
        }

        /// <summary>
        /// Rewind evaluator to its initial state so a preview restart produces identical
        /// output: zero accumulated time, every glyph re-marked dirty, and each bound
        /// effect's internal state cleared via <see cref="ITextStudioEffect.Reset"/>.
        /// The effect binding list itself is preserved (same text, same bindings).
        /// </summary>
        public void Reset()
        {
            EnsureWritableStates();
            _time = 0f;
            _playbackTime = 0f;

            if (_states != null)
            {
                for (int i = 0; i < _stateCount; i++)
                    _states[i] = EvaluatedGlyphState.Identity;
            }

            _animDirty.EnsureCapacity(_stateCount);
            _animDirty.SetAllDirty(_stateCount);
            ResetRenderPropertyBuffers(_stateCount);
            _hasEvaluatedSinglePersistentFrame = false;

            for (int i = 0; i < _boundEffectCount; i++)
            {
                ITextStudioEffect e = _boundEffects[i].effect;
                if (e != null) e.Reset();
            }

            ClearVisibilityOverrides();
        }

        /// <summary>
        /// Restarts effect time and effect-owned state without changing reveal progress or
        /// manual visibility ownership.
        /// </summary>
        public void RestartEffects()
        {
            EnsureWritableStates();
            _time = 0f;
            _playbackTime = 0f;
            ResetRenderPropertyBuffers(_stateCount);

            if (_states != null)
            {
                for (int i = 0; i < _stateCount; i++)
                    _states[i] = EvaluatedGlyphState.Identity;
            }

            if (_visibilityOverrides != null)
            {
                for (int i = 0; i < _stateCount; i++)
                {
                    if (_visibilityOverrides[i].transitionPhase != AnimationPhase.None)
                        _visibilityOverrides[i].transitionStartTime = 0f;
                }
            }

            for (int i = 0; i < _boundEffectCount; i++)
            {
                ITextStudioEffect effect = _boundEffects[i].effect;
                if (effect != null)
                    effect.Reset();
            }

            _animDirty.EnsureCapacity(_stateCount);
            _animDirty.SetAllDirty(_stateCount);
            _hasEvaluatedSinglePersistentFrame = false;
        }

        /// <summary>
        /// Samples the evaluator at an absolute preview time. Effect-owned state is reset
        /// first so direct scrubbing produces the same result regardless of the prior frame.
        /// </summary>
        public void SetPreviewTime(float elapsedTime)
        {
            EnsureWritableStates();
            float time = Mathf.Max(0f, elapsedTime);
            _time = time;
            _playbackTime = time;

            // Preserve the last evaluated frame so change detection can compare the new sample.

            for (int i = 0; i < _boundEffectCount; i++)
            {
                ITextStudioEffect effect = _boundEffects[i].effect;
                if (effect != null)
                    effect.Reset();
            }

            _animDirty.EnsureCapacity(_stateCount);
            _animDirty.SetAllDirty(_stateCount);
        }

        public bool SetGlyphVisibility(int glyphIndex, bool visible, bool playTransition = true)
        {
            if (glyphIndex < 0 || glyphIndex >= _stateCount)
                return false;

            EnsureVisibilityOverrideCapacity();
            ref GlyphVisibilityOverride visibilityOverride = ref _visibilityOverrides[glyphIndex];
            sbyte target = visible ? (sbyte)1 : (sbyte)-1;
            bool currentlyVisible = _states != null && _states[glyphIndex].alpha > 0.0001f;
            AnimationPhase transition = playTransition && currentlyVisible != visible
                ? (visible ? AnimationPhase.Appearance : AnimationPhase.Disappearance)
                : AnimationPhase.None;
            if (visibilityOverride.target == target && visibilityOverride.transitionPhase == transition)
                return false;

            if (visibilityOverride.target == 0)
                _visibilityOverrideCount++;
            if (visibilityOverride.transitionPhase != AnimationPhase.None)
                _activeVisibilityTransitionCount--;

            visibilityOverride.target = target;
            visibilityOverride.transitionPhase = transition;
            visibilityOverride.transitionStartTime = _time;
            if (transition != AnimationPhase.None)
                _activeVisibilityTransitionCount++;

            _animDirty.SetDirty(glyphIndex);
            return true;
        }

        void EnsureVisibilityOverrideCapacity()
        {
            if (_visibilityOverrides == null || _visibilityOverrides.Length < _stateCount)
            {
                _visibilityOverrides = new GlyphVisibilityOverride[
                    TextStudioBufferCapacity.ForCount(_stateCount)];
            }
        }

        public int SetGlyphRangeVisibility(int glyphStart, int glyphCount, bool visible, bool playTransition = true)
        {
            long rawEnd = (long)glyphStart + glyphCount;
            if (glyphCount <= 0 || glyphStart >= _stateCount || rawEnd <= 0)
                return 0;

            int start = Mathf.Clamp(glyphStart, 0, _stateCount);
            int end = rawEnd >= _stateCount ? _stateCount : (int)rawEnd;
            int changed = 0;
            for (int i = start; i < end; i++)
            {
                if (SetGlyphVisibility(i, visible, playTransition))
                    changed++;
            }
            return changed;
        }

        public bool ClearGlyphVisibilityOverride(int glyphIndex)
        {
            if (_visibilityOverrides == null || glyphIndex < 0 || glyphIndex >= _stateCount)
                return false;

            ref GlyphVisibilityOverride visibilityOverride = ref _visibilityOverrides[glyphIndex];
            if (visibilityOverride.target == 0)
                return false;

            if (visibilityOverride.transitionPhase != AnimationPhase.None)
                _activeVisibilityTransitionCount--;
            visibilityOverride = default;
            _visibilityOverrideCount--;
            _animDirty.SetDirty(glyphIndex);
            return true;
        }

        public bool ClearVisibilityOverrides()
        {
            if (_visibilityOverrides == null || _visibilityOverrideCount == 0)
                return false;

            Array.Clear(_visibilityOverrides, 0, _stateCount);
            _visibilityOverrideCount = 0;
            _activeVisibilityTransitionCount = 0;
            _animDirty.SetAllDirty(_stateCount);
            return true;
        }

        /// <summary>
        /// True while a bound effect can still change output after every glyph rests in its persistent or hidden phase.
        /// Reveal- and hide-only effects rest with their glyphs; the reveal clock and visibility transitions keep the
        /// text evaluating until then.
        /// </summary>
        internal bool HasSettledOutputEffects => _settledOutputBoundEffectCount > 0;

        // Renderer-owned evaluation and reveal-to-active blends keep their own timing, so they count as continuous.
        static bool ChangesSettledOutput(in BoundEffect effect) =>
            effect.appliesToPersistentPhase ||
            effect.rendererDeferred ||
            effect.phaseBlendGroupId > 0 ||
            effect.revealActiveBlendDuration > 0f;

        /// <summary>Changes whenever <see cref="BindEffects"/> rebinds the compiled plan's effect bindings.</summary>
        internal uint EffectBindingGeneration { get; private set; }

        /// <summary>
        /// Bind effects from the compiled plan's effect bindings.
        /// </summary>
        public void BindEffects(TextStudioCompiledPlan plan)
        {
            ClearBoundEffects();
            unchecked
            {
                EffectBindingGeneration++;
            }

            if (plan.EffectBindings == null) return;

            for (int i = 0; i < plan.EffectBindingCount; i++)
            {
                ref EffectBinding binding = ref plan.EffectBindings[i];

                ITextStudioEffect boundEffect = binding.effect;
                if (boundEffect == null &&
                         TextStudioEffects.TryCreate(
                             binding.effectId,
                             in binding.parameters,
                             out ITextStudioEffect createdEffect))
                {
                    boundEffect = createdEffect;
                }

                if (boundEffect != null)
                {
                    bool[] selectorMembers = ResolveEffectSelector(in binding, plan);
                    AddBoundEffect(new BoundEffect
                    {
                        effect = boundEffect,
                        sourceBindingIndex = i,
                        glyphStart = binding.glyphStart,
                        glyphEnd = binding.glyphStart + binding.glyphCount,
                        forcePhase = binding.phase,
                        phaseMask = binding.phaseMask,
                        phaseParams = binding.phaseParams,
                        phaseSpeed = Mathf.Max(0.01f, binding.phaseParams.speed),
                        intensity = Mathf.Clamp01(binding.intensity),
                        timeOffset = binding.timeOffset,
                        area = binding.area.Equals(default)
                            ? boundEffect.DefaultArea
                            : binding.area,
                        sequence = binding.sequence.Equals(default)
                            ? TextStudioSequenceSettings.Unstaggered
                            : binding.sequence,
                        phaseBlendGroupId = binding.phaseBlendGroupId,
                        revealActiveBlendDuration = binding.revealActiveBlendDuration,
                        revealActiveBlendPhaseMask = binding.revealActiveBlendPhaseMask,
                        revealActiveBlendTimeOffset = binding.revealActiveBlendTimeOffset,
                        scope = binding.scope,
                        selectorMembers = selectorMembers,
                        envelope = binding.envelope
                    });
                }
            }
            BuildGlyphEffectLookup(plan);
        }

        bool[] ResolveEffectSelector(in EffectBinding binding, TextStudioCompiledPlan plan)
        {
            if (binding.selectorScope == null)
                return null;
            int unitCount = Math.Max(plan.TextUnitCount, plan.VisibleGlyphCount);
            bool[] members = new bool[unitCount];
            Array.Fill(members, true);
            for (TextStudioEffectSelectorChain chain = binding.selectorScope;
                 chain != null; chain = chain.Parent)
            {
                if (!TextStudioSelectorCompiler.TryCompile(in chain.Selector,
                        TextStudioSelectorCapabilities.LayoutStable,
                        out TextStudioCompiledSelector compiled, out _))
                {
                    Array.Clear(members, 0, unitCount);
                    break;
                }
                _effectSelectionResolver.Resolve(in compiled, plan, null, 0,
                    binding.glyphStart, binding.glyphStart + binding.glyphCount);
                for (int unit = 0; unit < unitCount; unit++)
                    members[unit] &= _effectSelectionResolver.GetOwner(unit) >= 0;
            }
            return members;
        }

        /// <summary>
        /// Add a runtime effect binding that applies to a glyph range.
        /// </summary>
        public void AddEffect(ITextStudioEffect effect, int glyphStart, int glyphCount)
        {
            AddBoundEffect(new BoundEffect
            {
                effect = effect,
                sourceBindingIndex = -1,
                glyphStart = glyphStart,
                glyphEnd = glyphStart + glyphCount,
                forcePhase = AnimationPhase.None,
                phaseMask = TextStudioPhaseMask.All,
                phaseParams = PhaseParams.Default,
                phaseSpeed = 1f,
                intensity = 1f,
                timeOffset = 0f,
                area = effect != null ? effect.DefaultArea : TextStudioEffectAreaSettings.Default,
                sequence = TextStudioSequenceSettings.Unstaggered,
                phaseBlendGroupId = 0,
                revealActiveBlendDuration = 0f,
                revealActiveBlendPhaseMask = TextStudioPhaseMask.None,
                revealActiveBlendTimeOffset = 0f
            });
            _glyphEffectLookupDirty = true;
        }

        void ClearBoundEffects()
        {
            if (_boundEffects != null && _boundEffectCount > 0)
                Array.Clear(_boundEffects, 0, _boundEffectCount);
            _boundEffectCount = 0;
            _cpuBoundEffectCount = 0;
            _rendererDeferredBoundEffectCount = 0;
            _settledOutputBoundEffectCount = 0;
            _allBoundEffectsApplyToPersistentPhase = true;
            _allBoundEffectsSupportPersistentFastPath = true;
            _hasUniformSinglePersistentEffect = false;
            _hasSharedPersistentTiming = false;
            _hasUniformPersistentState = false;
            _evaluatedStatesAreUniform = false;
            _hasEvaluatedSinglePersistentFrame = false;
        }

        void AddBoundEffect(BoundEffect effect)
        {
            effect.capabilities = ResolveCapabilities(effect.effect);
            effect.renderChannels = effect.capabilities.RenderChannels;
            effect.appliesToPersistentPhase =
                (effect.forcePhase == AnimationPhase.None ||
                 effect.forcePhase == AnimationPhase.Persistent) &&
                IncludesPhase(effect.phaseMask, AnimationPhase.Persistent);
            effect.writesRenderProperties = !(effect.effect is CodeEffectBase code) || code.WritesPersistentRenderProperties;
            if (ChangesSettledOutput(in effect))
                _settledOutputBoundEffectCount++;
            _allBoundEffectsApplyToPersistentPhase &=
                effect.appliesToPersistentPhase && effect.intensity > 0f;
            _allBoundEffectsSupportPersistentFastPath &=
                effect.phaseBlendGroupId <= 0 &&
                effect.scope == null &&
                effect.envelope == null &&
                effect.effect is CodeEffectBase codeEffect &&
                codeEffect.SupportsPersistentFastPath;
            if (_boundEffects == null || _boundEffects.Length <= _boundEffectCount)
            {
                int capacity = _boundEffects == null ? 8 : _boundEffects.Length * 2;
                Array.Resize(ref _boundEffects, capacity);
            }

            _boundEffects[_boundEffectCount++] = effect;
            _cpuBoundEffectCount++;
        }

        /// <summary>
        /// Applies the hub's final all-renderer execution decision. Runtime and externally applied
        /// effects have no compiled binding index and always remain on the shared CPU path.
        /// </summary>
        internal bool SetRendererDeferredEffects(
            bool[] rendererOwnedBindings,
            int bindingCount)
        {
            int cpuCount = 0;
            int deferredCount = 0;
            bool changed = false;
            for (int index = 0; index < _boundEffectCount; index++)
            {
                ref BoundEffect bound = ref _boundEffects[index];
                bool rendererDeferred = bound.sourceBindingIndex >= 0 &&
                    rendererOwnedBindings != null &&
                    bound.sourceBindingIndex < bindingCount &&
                    bound.sourceBindingIndex < rendererOwnedBindings.Length &&
                    rendererOwnedBindings[bound.sourceBindingIndex];
                if (bound.rendererDeferred != rendererDeferred)
                {
                    bound.rendererDeferred = rendererDeferred;
                    changed = true;
                }

                if (rendererDeferred)
                    deferredCount++;
                else
                    cpuCount++;
            }

            _cpuBoundEffectCount = cpuCount;
            _rendererDeferredBoundEffectCount = deferredCount;
            if (!changed)
                return false;
            _settledOutputBoundEffectCount = 0;
            for (int index = 0; index < _boundEffectCount; index++)
                if (ChangesSettledOutput(in _boundEffects[index]))
                    _settledOutputBoundEffectCount++;

            _glyphEffectLookupDirty = true;
            _hasUniformSinglePersistentEffect = false;
            _hasSharedPersistentTiming = false;
            _hasUniformPersistentState = false;
            _evaluatedStatesAreUniform = false;
            _hasEvaluatedSinglePersistentFrame = false;
            _animDirty.EnsureCapacity(_stateCount);
            _animDirty.SetAllDirty(_stateCount);
            return true;
        }

        /// <summary>
        /// Apply a single externally driven effect to a specific glyph after normal evaluation.
        /// </summary>
        public void ApplyExternalEffect(
            TextStudioCompiledPlan plan,
            int glyphIndex,
            int bindingStartGlyph,
            int bindingGlyphCount,
            ITextStudioEffect effect,
            TextStudioEffectAreaSettings area,
            float progress,
            float timeOffset,
            float intensity,
            TextStudioEffectScopeChain scope = null)
        {
            ApplyExternalEffect(
                plan,
                glyphIndex,
                bindingStartGlyph,
                bindingGlyphCount,
                effect,
                area,
                progress,
                timeOffset,
                intensity,
                scope,
                null);
        }

        internal void ApplyExternalEffect(
            TextStudioCompiledPlan plan,
            int glyphIndex,
            int bindingStartGlyph,
            int bindingGlyphCount,
            ITextStudioEffect effect,
            TextStudioEffectAreaSettings area,
            float progress,
            float timeOffset,
            float intensity,
            TextStudioEffectScopeChain scope,
            TextStudioComposedEnvelopeStack envelope,
            TextStudioRenderPropertySink renderPropertySink = null)
        {
            // External effects, and the runtime's partial-intensity blend after them, write states after evaluation. The next frame must rebuild every state instead of reusing shortcut or stepped output, and this frame's states may no longer be uniform.
            _hasEvaluatedSinglePersistentFrame = false;
            _evaluatedStatesAreUniform = false;
            if (_states == null || plan == null || effect == null ||
                glyphIndex < 0 || glyphIndex >= _stateCount)
            {
                return;
            }

            EnsureWritableStates();

            if (!TextStudioEffectAreaResolver.TryResolveUnit(
                    plan,
                    bindingStartGlyph,
                    bindingGlyphCount,
                    area,
                    glyphIndex,
                    out TextStudioEffectUnitContext unit))
            {
                return;
            }

            int wordIndex = plan.GlyphToWordIndex != null
                ? TextSegmenter.GetWordIndexFast(plan.GlyphToWordIndex, glyphIndex)
                : TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, glyphIndex);
            int lineIndex = plan.GlyphToLineIndex != null
                ? TextSegmenter.GetLineIndexFast(plan.GlyphToLineIndex, glyphIndex)
                : TextSegmenter.GetLineIndex(plan.LineRanges, plan.LineCount, glyphIndex);
            float clampedProgress = Mathf.Clamp01(progress);

            EffectEvaluationContext context = new EffectEvaluationContext(
                glyphIndex: glyphIndex - bindingStartGlyph,
                totalGlyphs: bindingGlyphCount,
                wordIndex: Mathf.Max(0, wordIndex),
                lineIndex: Mathf.Max(0, lineIndex),
                phase: AnimationPhase.Persistent,
                phaseProgress: clampedProgress,
                time: Mathf.Max(0f, clampedProgress + timeOffset),
                deltaTime: 0f,
                normalizedIndex: unit.normalizedUnitIndex,
                unit
            );
            context = WithSpatialContext(in context, glyphIndex);

            TextStudioEffectCapabilities capabilities = ResolveCapabilities(effect);
            bool applied = EvaluateScopedEffect(
                effect,
                in capabilities,
                ResolveRenderChannels(effect),
                scope,
                envelope,
                Mathf.Clamp01(intensity),
                ref _states[glyphIndex],
                in context,
                renderPropertySink ?? _renderProperties,
                1f,
                glyphIndex);
            if (applied)
                _animDirty.SetDirty(glyphIndex);
        }

        /// <summary>
        /// Evaluate all effects for the current frame.
        /// Only processes glyphs that are dirty or have active animations.
        /// </summary>
        public void Evaluate(float deltaTime, RevealController reveal, TextStudioCompiledPlan plan)
        {
            Evaluate(deltaTime, deltaTime, reveal, plan, false, 0f);
        }

        internal void EvaluateWithPersistentPhaseProgress(
            float deltaTime,
            RevealController reveal,
            TextStudioCompiledPlan plan,
            float persistentPhaseProgress)
        {
            Evaluate(
                deltaTime,
                deltaTime,
                reveal,
                plan,
                true,
                Mathf.Clamp01(persistentPhaseProgress));
        }

        internal void EvaluateWithPlaybackTime(
            float deltaTime,
            float playbackDeltaTime,
            RevealController reveal,
            TextStudioCompiledPlan plan)
        {
            Evaluate(
                deltaTime,
                playbackDeltaTime,
                reveal,
                plan,
                false,
                0f);
        }

        internal void EvaluateWithPersistentPhaseProgress(
            float deltaTime,
            float playbackDeltaTime,
            RevealController reveal,
            TextStudioCompiledPlan plan,
            float persistentPhaseProgress)
        {
            Evaluate(
                deltaTime,
                playbackDeltaTime,
                reveal,
                plan,
                true,
                Mathf.Clamp01(persistentPhaseProgress));
        }

        void Evaluate(
            float deltaTime,
            float playbackDeltaTime,
            RevealController reveal,
            TextStudioCompiledPlan plan,
            bool overridePersistentPhaseProgress,
            float persistentPhaseProgress)
        {
            _time += deltaTime;
            _playbackTime += playbackDeltaTime;
            _evaluatedStatesAreUniform = false;
            if (_cpuBoundEffectCount == 0 && _visibilityOverrideCount == 0)
            {
                EvaluateRevealOnly(reveal, overridePersistentPhaseProgress, persistentPhaseProgress);
                return;
            }

            EnsureWritableStates();

            if (_glyphEffectLookupDirty) BuildGlyphEffectLookup(plan);
            bool allGlyphsPersistent = _visibilityOverrideCount == 0 &&
                                       (reveal == null || reveal.AreAllGlyphsPersistent);
            if (allGlyphsPersistent &&
                _persistentEffectsEnabled &&
                _rendererDeferredBoundEffectCount == 0 &&
                _hasUniformSinglePersistentEffect &&
                TextStudioMotionSettings.Resolve(_motionPolicy) ==
                TextStudioMotionPolicy.Full)
            {
                EvaluateUniformSinglePersistentEffect(
                    deltaTime,
                    plan,
                    overridePersistentPhaseProgress ? persistentPhaseProgress : 1f);
                return;
            }

            if (allGlyphsPersistent &&
                _persistentEffectsEnabled &&
                _rendererDeferredBoundEffectCount == 0 &&
                _allBoundEffectsApplyToPersistentPhase &&
                _allBoundEffectsSupportPersistentFastPath &&
                TextStudioMotionSettings.Resolve(_motionPolicy) == TextStudioMotionPolicy.Full)
            {
                EvaluatePersistentFastEffectStack(
                    overridePersistentPhaseProgress ? persistentPhaseProgress : 1f);
                return;
            }

            _hasEvaluatedSinglePersistentFrame = false;

            _allEvaluatedGlyphsDirty = _stateCount > 0;
            BeginRenderPropertyEvaluation();

            for (int i = 0; i < _stateCount; i++)
            {
                int effectStart = _glyphEffectStart != null && i < _glyphEffectStart.Length
                    ? _glyphEffectStart[i]
                    : 0;
                int effectCount = _glyphEffectCount != null && i < _glyphEffectCount.Length
                    ? _glyphEffectCount[i]
                    : 0;
                bool guaranteedPersistentEffect = allGlyphsPersistent &&
                                                  _persistentEffectsEnabled &&
                                                  _allBoundEffectsApplyToPersistentPhase &&
                                                  effectCount > 0;
                EvaluatedGlyphState previousState = guaranteedPersistentEffect
                    ? default
                    : _states[i];
                AnimationPhase phase;
                float phaseProgress;
                bool hasVisibilityOverride = false;
                bool hiddenByVisibilityOverride = false;
                if (allGlyphsPersistent)
                {
                    phase = AnimationPhase.Persistent;
                    phaseProgress = overridePersistentPhaseProgress ? persistentPhaseProgress : 1f;
                }
                else
                {
                    hasVisibilityOverride = TryResolveVisibilityOverride(
                        i,
                        reveal,
                        out phase,
                        out phaseProgress,
                        out hiddenByVisibilityOverride);
                    if (!hasVisibilityOverride)
                    {
                        phase = reveal != null
                            ? reveal.GetGlyphPhase(i, _time)
                            : AnimationPhase.Persistent;
                        phaseProgress = GetPhaseProgress(reveal, i, phase);
                    }

                    if (overridePersistentPhaseProgress && phase == AnimationPhase.Persistent)
                        phaseProgress = persistentPhaseProgress;
                }

                bool hiddenByReveal = !hasVisibilityOverride &&
                                      reveal != null &&
                                      !reveal.IsGlyphRevealed(i) &&
                                      reveal.Mode != RevealMode.Instant;
                if (phase == AnimationPhase.None || hiddenByReveal || hiddenByVisibilityOverride)
                {
                    _states[i] = EvaluatedGlyphState.Identity;
                    _states[i].alpha = 0f;
                    _states[i].renderChannels = TextStudioRenderChannels.Alpha;
                    _states[i].currentPhase = AnimationPhase.None;
                    SetRenderedStateDirty(i, in previousState);
                    continue;
                }

                // Reset to identity before effect accumulation
                _states[i] = EvaluatedGlyphState.Identity;
                _states[i].currentPhase = phase;
                _states[i].phaseProgress = phaseProgress;

                // Use precomputed O(1) lookups when available, fall back to binary search
                int wordIndex = plan.GlyphToWordIndex != null
                    ? TextSegmenter.GetWordIndexFast(plan.GlyphToWordIndex, i)
                    : TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, i);
                int lineIndex = plan.GlyphToLineIndex != null
                    ? TextSegmenter.GetLineIndexFast(plan.GlyphToLineIndex, i)
                    : TextSegmenter.GetLineIndex(plan.LineRanges, plan.LineCount, i);
                bool anyApplied = false;
                EffectEvaluationContext context = default;
                EffectEvaluationContext spatialContext = default;
                bool hasReusableContext = false;
                for (int e = 0; e < effectCount; e++)
                {
                    int lookupIndex = effectStart + e;
                    int boundIndex = _glyphEffectIndices[lookupIndex];
                    ref BoundEffect bound = ref _boundEffects[boundIndex];
                    if (bound.phaseBlendGroupId != 0)
                    {
                        int groupEnd = e + 1;
                        while (groupEnd < effectCount)
                        {
                            int candidateLookupIndex = effectStart + groupEnd;
                            ref BoundEffect candidate = ref _boundEffects[_glyphEffectIndices[candidateLookupIndex]];
                            if (candidate.phaseBlendGroupId != bound.phaseBlendGroupId)
                                break;
                            groupEnd++;
                        }

                        if (bound.phaseBlendGroupId > 0)
                        {
                            LaneBlendEvaluation laneEvaluation =
                                ResolveLaneBlendEvaluation(
                                    in bound,
                                    reveal,
                                    phase,
                                    phaseProgress,
                                    out float phaseBlendWeight,
                                    out AnimationPhase lanePhase,
                                    out float lanePhaseProgress);
                            if (laneEvaluation != LaneBlendEvaluation.None)
                            {
                                if (laneEvaluation == LaneBlendEvaluation.Blend)
                                {
                                    EvaluatedGlyphState groupInput = _states[i];
                                    TextStudioRenderPropertyView groupProperties =
                                        _renderProperties.View;
                                    _revealRenderProperties.Begin(i, in groupProperties);
                                    _activeRenderProperties.Begin(i, in groupProperties);
                                    EvaluatedGlyphState revealState = groupInput;
                                    bool revealApplied = EvaluatePhaseBlendGroup(
                                        effectStart + e,
                                        effectStart + groupEnd,
                                        reveal,
                                        i,
                                        Mathf.Max(0, wordIndex),
                                        lineIndex,
                                        AnimationPhase.Appearance,
                                        lanePhaseProgress,
                                        deltaTime,
                                        ref revealState,
                                        _revealRenderProperties);
                                    EvaluatedGlyphState activeState = groupInput;
                                    bool activeApplied = EvaluatePhaseBlendGroup(
                                        effectStart + e,
                                        effectStart + groupEnd,
                                        reveal,
                                        i,
                                        Mathf.Max(0, wordIndex),
                                        lineIndex,
                                        AnimationPhase.Persistent,
                                        1f,
                                        deltaTime,
                                        ref activeState,
                                        _activeRenderProperties);

                                    _states[i] = EvaluatedGlyphState.Lerp(
                                        revealState,
                                        activeState,
                                        phaseBlendWeight);
                                    _renderProperties.BlendGlyph(
                                        i,
                                        _revealRenderProperties,
                                        _activeRenderProperties,
                                        phaseBlendWeight);
                                    anyApplied |= revealApplied || activeApplied;
                                }
                                else if (laneEvaluation == LaneBlendEvaluation.Reveal ||
                                         laneEvaluation == LaneBlendEvaluation.Active)
                                {
                                    anyApplied |= EvaluatePhaseBlendGroup(
                                        effectStart + e,
                                        effectStart + groupEnd,
                                        reveal,
                                        i,
                                        Mathf.Max(0, wordIndex),
                                        lineIndex,
                                        lanePhase,
                                        lanePhaseProgress,
                                        deltaTime,
                                        ref _states[i],
                                        _renderProperties);
                                }

                                _states[i].currentPhase = phase;
                                _states[i].phaseProgress = phaseProgress;
                                hasReusableContext = false;
                                e = groupEnd - 1;
                                continue;
                            }
                        }
                        else if (TryGetWrapperRevealActiveBlendWeight(
                                     in bound,
                                     reveal,
                                     phase,
                                     phaseProgress,
                                     out float wrapperBlendWeight))
                        {
                            EvaluatedGlyphState groupInput = _states[i];
                            TextStudioRenderPropertyView groupProperties =
                                _renderProperties.View;
                            _revealRenderProperties.Begin(i, in groupProperties);
                            _activeRenderProperties.Begin(i, in groupProperties);
                            EvaluatedGlyphState revealState = groupInput;
                            bool revealApplied = EvaluatePhaseBlendGroup(
                                effectStart + e,
                                effectStart + groupEnd,
                                reveal,
                                i,
                                Mathf.Max(0, wordIndex),
                                lineIndex,
                                AnimationPhase.Appearance,
                                phaseProgress,
                                deltaTime,
                                ref revealState,
                                _revealRenderProperties);
                            EvaluatedGlyphState activeState = groupInput;
                            bool activeApplied = EvaluatePhaseBlendGroup(
                                effectStart + e,
                                effectStart + groupEnd,
                                reveal,
                                i,
                                Mathf.Max(0, wordIndex),
                                lineIndex,
                                AnimationPhase.Persistent,
                                1f,
                                deltaTime,
                                ref activeState,
                                _activeRenderProperties);

                            _states[i] = EvaluatedGlyphState.Lerp(
                                revealState,
                                activeState,
                                wrapperBlendWeight);
                            _renderProperties.BlendGlyph(
                                i,
                                _revealRenderProperties,
                                _activeRenderProperties,
                                wrapperBlendWeight);
                            _states[i].currentPhase = phase;
                            _states[i].phaseProgress = phaseProgress;
                            anyApplied |= revealApplied || activeApplied;
                            hasReusableContext = false;
                            e = groupEnd - 1;
                            continue;
                        }
                    }

                    bool reuseContext = hasReusableContext &&
                                        _glyphEffectReusesPreviousContext != null &&
                                        _glyphEffectReusesPreviousContext[lookupIndex];
                    if (!reuseContext)
                    {
                        ref readonly TextStudioEffectUnitContext unit =
                            ref _glyphEffectUnits[lookupIndex];
                        float effectDelay = _glyphEffectDelays[lookupIndex];
                        if (allGlyphsPersistent)
                        {
                            if (!_persistentEffectsEnabled || !bound.appliesToPersistentPhase)
                            {
                                hasReusableContext = false;
                                continue;
                            }

                            float effectTime = Mathf.Max(
                                0f,
                                GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _time) +
                                bound.timeOffset);
                            float effectPlaybackTime = Mathf.Max(
                                0f,
                                GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _playbackTime) +
                                bound.timeOffset);
                            context = new EffectEvaluationContext(
                                glyphIndex: i,
                                totalGlyphs: _stateCount,
                                wordIndex: Mathf.Max(0, wordIndex),
                                lineIndex: lineIndex,
                                phase: AnimationPhase.Persistent,
                                phaseProgress: phaseProgress,
                                time: effectTime,
                                playbackTime: effectPlaybackTime,
                                deltaTime: deltaTime,
                                normalizedIndex: unit.normalizedUnitIndex,
                                unit);
                        }
                        else if (!TryCreateEffectContext(
                                     in bound,
                                     effectDelay,
                                     reveal,
                                     i,
                                     Mathf.Max(0, wordIndex),
                                     lineIndex,
                                     phase,
                                     phaseProgress,
                                     deltaTime,
                                     in unit,
                                     out context))
                        {
                            hasReusableContext = false;
                            continue;
                        }

                        hasReusableContext = true;
                        // Later effects that reuse this base context resolve to the same spatial context.
                        spatialContext = WithSpatialContext(in context, i);
                    }

                    anyApplied |= EvaluateScopedEffect(
                        bound.effect,
                        in bound.capabilities,
                        bound.renderChannels,
                        bound.scope,
                        bound.envelope,
                        bound.intensity,
                        ref _states[i],
                        in spatialContext);
                }

                if (anyApplied)
                    _animDirty.SetDirty(i);
                else
                    SetRenderedStateDirty(i, in previousState);
            }
        }

        void EvaluatePersistentFastEffectStack(float phaseProgress)
        {
            _hasEvaluatedSinglePersistentFrame = false;
            _allEvaluatedGlyphsDirty = _stateCount > 0;
            _animDirty.SetAllDirty(_stateCount);
            BeginRenderPropertyEvaluation();
            ResolvePersistentStackShortcuts();
            bool sharedEffectClocks = _time == _playbackTime;

            for (int glyphIndex = 0; glyphIndex < _stateCount; glyphIndex++)
            {
                // Evaluate in place: a local copy and a final store would move the whole state twice per glyph.
                ref EvaluatedGlyphState state = ref _states[glyphIndex];
                state = EvaluatedGlyphState.Identity;
                state.currentPhase = AnimationPhase.Persistent;
                state.phaseProgress = phaseProgress;
                int effectStart = _glyphEffectStart[glyphIndex];
                int effectCount = _glyphEffectCount[glyphIndex];
                float effectTime = 0f;
                float effectPlaybackTime = 0f;
                bool hasReusableClock = false;

                for (int effectIndex = 0; effectIndex < effectCount; effectIndex++)
                {
                    int lookupIndex = effectStart + effectIndex;
                    ref BoundEffect bound = ref _boundEffects[_glyphEffectIndices[lookupIndex]];
                    CodeEffectBase effect = (CodeEffectBase)bound.effect;
                    ref readonly TextStudioEffectUnitContext unit = ref _glyphEffectUnits[lookupIndex];
                    bool reuseClock = hasReusableClock &&
                                      _glyphEffectReusesPreviousContext[lookupIndex];
                    if (!reuseClock)
                    {
                        float effectDelay = _glyphEffectDelays[lookupIndex];
                        effectTime = Mathf.Max(
                            0f,
                            GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _time) +
                            bound.timeOffset);
                        effectPlaybackTime = sharedEffectClocks
                            ? effectTime
                            : Mathf.Max(
                                0f,
                                GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _playbackTime) +
                                bound.timeOffset);
                        hasReusableClock = true;
                    }
                    float intensity = bound.intensity;
                    if (intensity < 1f)
                    {
                        EvaluatedGlyphState preEffectState = state;
                        EvaluateFastStackEffect(in bound, effect, ref state, effectTime, effectPlaybackTime, phaseProgress, in unit, glyphIndex);
                        state = EvaluatedGlyphState.Lerp(preEffectState, state, intensity);
                    }
                    else
                        EvaluateFastStackEffect(in bound, effect, ref state, effectTime, effectPlaybackTime, phaseProgress, in unit, glyphIndex);

                    if (intensity > 0f)
                        state.renderChannels |= bound.renderChannels;
                }
            }
        }

        // Effect parameters can change between frames, so every fast stack evaluation resolves which bound effects have a playback-free shortcut and the shortcut's frame-invariant vector once instead of per glyph.
        void ResolvePersistentStackShortcuts()
        {
            for (int index = 0; index < _boundEffectCount; index++)
            {
                ref BoundEffect bound = ref _boundEffects[index];
                bound.persistentShortcut = PersistentShortcut.None;
                bound.persistentShortcutVector = default;
                if (bound.writesRenderProperties)
                    continue;

                switch (bound.effect)
                {
                    case WaveEffect wave when wave.SupportsInfinitePersistentFastPath:
                        bound.persistentShortcut = PersistentShortcut.Wave;
                        bound.persistentShortcutVector = wave.NormalizedDirection;
                        break;
                    case SwingEffect swing when swing.SupportsInfinitePersistentFastPath:
                        bound.persistentShortcut = PersistentShortcut.Swing;
                        bound.persistentShortcutVector = swing.PivotOffset;
                        break;
                    case PendulumEffect pendulum when pendulum.SupportsInfinitePersistentFastPath:
                        bound.persistentShortcut = PersistentShortcut.Pendulum;
                        bound.persistentShortcutVector = pendulum.PivotOffset;
                        break;
                    case SizeEffect size when size.SupportsInfinitePersistentFastPath:
                        bound.persistentShortcut = PersistentShortcut.Size;
                        break;
                }
            }
        }

        // Effects without render properties skip the writer and the overload that only forwards to this call.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void EvaluateFastStackEffect(
            in BoundEffect bound,
            CodeEffectBase effect,
            ref EvaluatedGlyphState state,
            float effectTime,
            float effectPlaybackTime,
            float phaseProgress,
            in TextStudioEffectUnitContext unit,
            int glyphIndex)
        {
            // Shortcuts compose with the state earlier stacked effects left exactly as the general fast path does.
            switch (bound.persistentShortcut)
            {
                case PersistentShortcut.Wave:
                    ((WaveEffect)effect).EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.normalizedUnitIndex,
                        in bound.persistentShortcutVector);
                    return;
                case PersistentShortcut.Swing:
                    ((SwingEffect)effect).EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex,
                        in bound.persistentShortcutVector);
                    return;
                case PersistentShortcut.Pendulum:
                    ((PendulumEffect)effect).EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        in bound.persistentShortcutVector);
                    return;
                case PersistentShortcut.Size:
                    ((SizeEffect)effect).EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex);
                    return;
            }

            if (bound.writesRenderProperties)
            {
                TextStudioRenderPropertyWriter renderProperties =
                    _renderProperties.CreateWriter(
                        glyphIndex,
                        1,
                        bound.intensity);
                effect.EvaluatePersistentFast(
                    ref state,
                    effectTime,
                    effectPlaybackTime,
                    phaseProgress,
                    unit.unitIndex,
                    unit.normalizedUnitIndex,
                    in renderProperties);
            }
            else
            {
                effect.EvaluatePersistentFast(
                    ref state,
                    effectTime,
                    effectPlaybackTime,
                    phaseProgress,
                    unit.unitIndex,
                    unit.normalizedUnitIndex);
            }
        }

        bool EvaluatePhaseBlendGroup(
            int lookupStart,
            int lookupEnd,
            RevealController reveal,
            int glyphIndex,
            int wordIndex,
            int lineIndex,
            AnimationPhase phase,
            float phaseProgress,
            float deltaTime,
            ref EvaluatedGlyphState state,
            TextStudioRenderPropertySink renderPropertySink)
        {
            bool anyApplied = false;
            for (int lookupIndex = lookupStart; lookupIndex < lookupEnd; lookupIndex++)
            {
                int boundIndex = _glyphEffectIndices[lookupIndex];
                ref BoundEffect bound = ref _boundEffects[boundIndex];
                float effectDelay = _glyphEffectDelays[lookupIndex];
                ref readonly TextStudioEffectUnitContext unit =
                    ref _glyphEffectUnits[lookupIndex];
                if (!TryCreateEffectContext(
                        in bound,
                        effectDelay,
                        reveal,
                        glyphIndex,
                        wordIndex,
                        lineIndex,
                        phase,
                        phaseProgress,
                        deltaTime,
                        in unit,
                        out EffectEvaluationContext context))
                {
                    continue;
                }

                context = WithSpatialContext(in context, glyphIndex);
                anyApplied |= EvaluateScopedEffect(
                    bound.effect,
                    in bound.capabilities,
                    bound.renderChannels,
                    bound.scope,
                    bound.envelope,
                    bound.intensity,
                    ref state,
                    in context,
                    renderPropertySink,
                    1f);
            }

            return anyApplied;
        }

        LaneBlendEvaluation ResolveLaneBlendEvaluation(
            in BoundEffect bound,
            RevealController reveal,
            AnimationPhase phase,
            float phaseProgress,
            out float weight,
            out AnimationPhase lanePhase,
            out float lanePhaseProgress)
        {
            weight = 0f;
            lanePhase = phase;
            lanePhaseProgress = phaseProgress;
            if (bound.phaseBlendGroupId <= 0 ||
                bound.revealActiveBlendDuration <= 0f)
            {
                return LaneBlendEvaluation.None;
            }

            // Active-only previews bypass the transition and must use the ordinary shared clock.
            if (reveal != null && !reveal.RevealActiveBlendEnabled)
                return LaneBlendEvaluation.None;

            TextStudioPhaseMask laneMask =
                bound.revealActiveBlendPhaseMask == TextStudioPhaseMask.None
                    ? bound.phaseMask
                    : bound.revealActiveBlendPhaseMask;
            float appearanceDuration = reveal != null
                ? reveal.AppearanceDuration
                : 0.3f;
            appearanceDuration = Mathf.Max(
                TextStudioRevealBlendTiming.Epsilon,
                appearanceDuration);
            bool includesReveal = (laneMask & TextStudioPhaseMask.Reveal) != 0;
            bool includesActive = (laneMask & TextStudioPhaseMask.Active) != 0;
            if (!includesReveal && !includesActive)
                return LaneBlendEvaluation.None;

            if (phase == AnimationPhase.Disappearance)
            {
                if ((laneMask & TextStudioPhaseMask.Hide) == 0)
                    return LaneBlendEvaluation.Skip;

                lanePhase = AnimationPhase.Disappearance;
                return LaneBlendEvaluation.Active;
            }

            if (phase != AnimationPhase.Appearance &&
                phase != AnimationPhase.Persistent)
            {
                return LaneBlendEvaluation.None;
            }

            float transitionTime = reveal != null
                ? reveal.GetRevealActiveBlendTime()
                : phase == AnimationPhase.Appearance
                    ? (Mathf.Clamp01(phaseProgress) - 1f) * appearanceDuration
                    : Mathf.Max(0f, _playbackTime);
            float revealProgress = Mathf.Clamp01(
                1f + transitionTime / appearanceDuration);

            if (includesReveal)
            {
                float blendDuration = Mathf.Min(
                    bound.revealActiveBlendDuration,
                    appearanceDuration);
                float blendStart = -blendDuration;
                if (transitionTime < blendStart)
                {
                    lanePhase = AnimationPhase.Appearance;
                    lanePhaseProgress = revealProgress;
                    return LaneBlendEvaluation.Reveal;
                }

                if (transitionTime < -TextStudioRevealBlendTiming.Epsilon)
                {
                    weight = TextStudioRevealBlendTiming.SmoothWeight(
                        transitionTime,
                        blendStart,
                        0f);
                    lanePhase = AnimationPhase.Appearance;
                    lanePhaseProgress = revealProgress;
                    return LaneBlendEvaluation.Blend;
                }

                if (!includesActive)
                    return LaneBlendEvaluation.Skip;

                lanePhase = AnimationPhase.Persistent;
                lanePhaseProgress = 1f;
                return LaneBlendEvaluation.Active;
            }

            float activationTime =
                transitionTime + bound.revealActiveBlendTimeOffset;
            if (activationTime < -TextStudioRevealBlendTiming.Epsilon)
                return LaneBlendEvaluation.Skip;

            float blendElapsed = Mathf.Max(0f, activationTime);
            lanePhase = AnimationPhase.Persistent;
            lanePhaseProgress = 1f;
            if ((reveal == null || reveal.RevealActiveBlendEnabled) &&
                blendElapsed <
                    bound.revealActiveBlendDuration -
                    TextStudioRevealBlendTiming.Epsilon)
            {
                weight = TextStudioRevealBlendTiming.SmoothWeight(
                    blendElapsed,
                    0f,
                    bound.revealActiveBlendDuration);
                return LaneBlendEvaluation.Blend;
            }

            return LaneBlendEvaluation.Active;
        }

        static bool TryGetWrapperRevealActiveBlendWeight(
            in BoundEffect bound,
            RevealController reveal,
            AnimationPhase phase,
            float phaseProgress,
            out float weight)
        {
            weight = 0f;
            if (phase != AnimationPhase.Appearance ||
                bound.phaseBlendGroupId == 0 ||
                bound.revealActiveBlendDuration <= 0f)
            {
                return false;
            }

            float appearanceDuration = reveal != null
                ? reveal.AppearanceDuration
                : 0.3f;
            appearanceDuration = Mathf.Max(0.0001f, appearanceDuration);
            float blendDuration = Mathf.Min(
                bound.revealActiveBlendDuration,
                appearanceDuration);
            float blendProgressLength = blendDuration / appearanceDuration;
            float blendStart = 1f - blendProgressLength;
            if (phaseProgress < blendStart)
                return false;

            float linearWeight = blendProgressLength >= 0.999999f
                ? Mathf.Clamp01(phaseProgress)
                : Mathf.Clamp01((phaseProgress - blendStart) / blendProgressLength);
            weight = linearWeight * linearWeight * (3f - 2f * linearWeight);
            return true;
        }

        void EvaluateUniformSinglePersistentEffect(
            float deltaTime,
            TextStudioCompiledPlan plan,
            float phaseProgress)
        {
            ref BoundEffect bound = ref _boundEffects[0];
            CodeEffectBase codeEffect = bound.effect as CodeEffectBase;
            bool usePersistentFastPath = codeEffect != null && codeEffect.SupportsPersistentFastPath;
            FadeEffect fadeEffect = codeEffect as FadeEffect;
            bool useFadeAlphaFastPath = fadeEffect != null && fadeEffect.SupportsPersistentAlphaFastPath;
            WaveEffect waveEffect = codeEffect as WaveEffect;
            bool useWaveFastPath =
                waveEffect != null && waveEffect.SupportsInfinitePersistentFastPath;
            Vector2 waveDirection = useWaveFastPath
                ? waveEffect.NormalizedDirection
                : default;
            SwingEffect swingEffect = codeEffect as SwingEffect;
            bool useSwingFastPath =
                swingEffect != null && swingEffect.SupportsInfinitePersistentFastPath;
            Vector2 swingPivotOffset = useSwingFastPath
                ? swingEffect.PivotOffset
                : default;
            SizeEffect sizeEffect = codeEffect as SizeEffect;
            bool useSizeFastPath =
                sizeEffect != null && sizeEffect.SupportsInfinitePersistentFastPath;
            WiggleEffect wiggleEffect = codeEffect as WiggleEffect;
            bool useWiggleFastPath =
                wiggleEffect != null && wiggleEffect.SupportsInfinitePersistentFastPath;
            BounceEffect bounceEffect = codeEffect as BounceEffect;
            bool useBounceFastPath =
                bounceEffect != null && bounceEffect.SupportsInfinitePersistentFastPath;
            Vector2 bounceDirection = useBounceFastPath
                ? bounceEffect.NormalizedDirection
                : default;
            DangleEffect dangleEffect = codeEffect as DangleEffect;
            bool useDangleFastPath =
                dangleEffect != null && dangleEffect.SupportsInfinitePersistentFastPath;
            Vector2 danglePivotOffset = useDangleFastPath
                ? dangleEffect.PivotOffset
                : default;
            // These shortcuts write only position, rotation and pivot. After a frame from the same shortcut every other field is still identity, so each glyph resets only the fields its shortcut accumulates into instead of copying the whole identity state.
            PersistentShortcut reusableShortcut =
                useWaveFastPath ? PersistentShortcut.Wave :
                useSwingFastPath ? PersistentShortcut.Swing :
                useWiggleFastPath ? PersistentShortcut.Wiggle :
                useBounceFastPath ? PersistentShortcut.Bounce :
                useDangleFastPath ? PersistentShortcut.Dangle :
                PersistentShortcut.None;
            bool reuseShortcutState =
                reusableShortcut != PersistentShortcut.None &&
                _hasEvaluatedSinglePersistentFrame &&
                _reusableSingleShortcut == reusableShortcut;
            bool reuseResetsPosition =
                reusableShortcut == PersistentShortcut.Wave ||
                reusableShortcut == PersistentShortcut.Bounce ||
                reusableShortcut == PersistentShortcut.Wiggle;
            bool reuseResetsRotation =
                reusableShortcut == PersistentShortcut.Swing ||
                reusableShortcut == PersistentShortcut.Wiggle ||
                reusableShortcut == PersistentShortcut.Dangle;
            RainbowEffect rainbowEffect = codeEffect as RainbowEffect;
            ExpandEffect expandEffect = codeEffect as ExpandEffect;
            bool sharedEffectClocks = _time == _playbackTime;
            _allEvaluatedGlyphsDirty = _stateCount > 0;
            if (_hasEvaluatedSinglePersistentFrame &&
                _hasSharedPersistentTiming &&
                codeEffect != null &&
                codeEffect.ReusesPersistentStateBetweenSteps &&
                _stateCount > 0)
            {
                float effectDelay = _glyphEffectDelays[_glyphEffectStart[0]];
                float currentEffectTime = Mathf.Max(
                    0f,
                    GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _time) +
                    bound.timeOffset);
                float previousEffectTime = Mathf.Max(
                    0f,
                    GetPersistentEffectTime(
                        effectDelay,
                        bound.phaseSpeed,
                        Mathf.Max(0f, _time - deltaTime)) +
                    bound.timeOffset);
                if (codeEffect.HasSamePersistentState(previousEffectTime, currentEffectTime))
                {
                    _animDirty.ClearAll();
                    _allEvaluatedGlyphsDirty = false;
                    return;
                }
            }

            BeginRenderPropertyEvaluation();
            if (_hasUniformPersistentState && _stateCount > 0)
            {
                float effectDelay = _glyphEffectDelays[_glyphEffectStart[0]];
                float effectTime = Mathf.Max(
                    0f,
                    GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _time) +
                    bound.timeOffset);
                float effectPlaybackTime = sharedEffectClocks
                    ? effectTime
                    : Mathf.Max(
                        0f,
                        GetPersistentEffectTime(effectDelay, bound.phaseSpeed, _playbackTime) +
                        bound.timeOffset);
                EvaluatedGlyphState uniformState = EvaluatedGlyphState.Identity;
                uniformState.currentPhase = AnimationPhase.Persistent;
                uniformState.phaseProgress = phaseProgress;
                TextStudioRenderPropertyWriter uniformRenderProperties =
                    _renderProperties.CreateWriter(
                        0,
                        _stateCount,
                        bound.intensity);
                codeEffect.EvaluatePersistentFast(
                    ref uniformState,
                    effectTime,
                    effectPlaybackTime,
                    phaseProgress,
                    0,
                    0f,
                    in uniformRenderProperties);
                uniformState.renderChannels = bound.renderChannels;
                TextStudioRenderPropertyView currentProperties =
                    _renderProperties.View;
                TextStudioRenderPropertyView previousProperties =
                    _previousRenderProperties.View;
                bool renderedStateChanged =
                    !_states[0].HasSameRenderedValues(in uniformState) ||
                    _renderPropertyRegistryChanged ||
                    !currentProperties.HasSameValues(in previousProperties);
                for (int i = 0; i < _stateCount; i++)
                    _states[i] = uniformState;
                if (renderedStateChanged)
                {
                    _animDirty.SetAllDirty(_stateCount);
                }
                else
                {
                    _animDirty.ClearAll();
                    _allEvaluatedGlyphsDirty = false;
                }
                _evaluatedStatesAreUniform = true;
                _hasEvaluatedSinglePersistentFrame = true;
                _reusableSingleShortcut = PersistentShortcut.None;
                return;
            }

            _animDirty.SetAllDirty(_stateCount);
            bool sharedGlyphTiming = _hasSharedPersistentTiming && _stateCount > 0;
            float sharedEffectTime = 0f;
            float sharedEffectPlaybackTime = 0f;
            if (sharedGlyphTiming)
            {
                float sharedDelay = _glyphEffectDelays[_glyphEffectStart[0]];
                sharedEffectTime = Mathf.Max(
                    0f,
                    GetPersistentEffectTime(sharedDelay, bound.phaseSpeed, _time) +
                    bound.timeOffset);
                sharedEffectPlaybackTime = sharedEffectClocks
                    ? sharedEffectTime
                    : Mathf.Max(
                        0f,
                        GetPersistentEffectTime(
                            sharedDelay,
                            bound.phaseSpeed,
                            _playbackTime) +
                        bound.timeOffset);
            }
            float sharedExpandFadeAlpha = 0f;
            float sharedExpandStrength = 0f;
            float sharedExpandEnvelope = 0f;
            bool useExpandFastPath = expandEffect != null && sharedGlyphTiming;
            if (useExpandFastPath)
            {
                expandEffect.PrepareSharedPersistentFast(
                    sharedEffectTime,
                    sharedEffectPlaybackTime,
                    phaseProgress,
                    out sharedExpandFadeAlpha,
                    out sharedExpandStrength,
                    out sharedExpandEnvelope);
            }

            for (int i = 0; i < _stateCount; i++)
            {
                int lookupIndex = i;
                ref readonly TextStudioEffectUnitContext unit = ref _glyphEffectUnits[lookupIndex];
                ref EvaluatedGlyphState state = ref _states[i];
                float effectTime = sharedGlyphTiming
                    ? sharedEffectTime
                    : Mathf.Max(
                        0f,
                        GetPersistentEffectTime(
                            _glyphEffectDelays[lookupIndex],
                            bound.phaseSpeed,
                            _time) +
                        bound.timeOffset);
                float effectPlaybackTime = sharedGlyphTiming
                    ? sharedEffectPlaybackTime
                    : effectTime;
                if (!sharedGlyphTiming && !useWiggleFastPath && !sharedEffectClocks)
                {
                    effectPlaybackTime = Mathf.Max(
                        0f,
                        GetPersistentEffectTime(
                            _glyphEffectDelays[lookupIndex],
                            bound.phaseSpeed,
                            _playbackTime) +
                        bound.timeOffset);
                }

                if (reuseShortcutState)
                {
                    if (reuseResetsPosition)
                        state.positionOffset = Vector3.zero;
                    if (reuseResetsRotation)
                        state.rotation = Quaternion.identity;
                    state.phaseProgress = phaseProgress;
                }
                else
                {
                    state = EvaluatedGlyphState.Identity;
                    state.currentPhase = AnimationPhase.Persistent;
                    state.phaseProgress = phaseProgress;
                }
                if (useWaveFastPath)
                {
                    waveEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.normalizedUnitIndex,
                        in waveDirection);
                }
                else if (useSwingFastPath)
                {
                    swingEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex,
                        in swingPivotOffset);
                }
                else if (useSizeFastPath)
                {
                    sizeEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex);
                }
                else if (useWiggleFastPath)
                {
                    wiggleEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex);
                }
                else if (useBounceFastPath)
                {
                    bounceEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex,
                        in bounceDirection);
                }
                else if (useDangleFastPath)
                {
                    dangleEffect.EvaluateInfinitePersistentFast(
                        ref state,
                        effectTime,
                        unit.normalizedUnitIndex,
                        in danglePivotOffset);
                }
                else if (rainbowEffect != null)
                {
                    rainbowEffect.EvaluatePersistentColorFast(
                        ref state,
                        effectTime,
                        unit.normalizedUnitIndex);
                }
                else if (useFadeAlphaFastPath)
                {
                    state.alpha = fadeEffect.EvaluatePersistentAlphaFast(
                        effectTime,
                        effectPlaybackTime,
                        phaseProgress);
                }
                else if (useExpandFastPath)
                {
                    expandEffect.EvaluateSharedPersistentFast(
                        ref state,
                        effectTime,
                        unit.unitIndex,
                        sharedExpandFadeAlpha,
                        sharedExpandStrength,
                        sharedExpandEnvelope);
                }
                else if (usePersistentFastPath)
                {
                    // Effects that write no render properties skip the per-glyph writer, as in fast stacks.
                    if (bound.writesRenderProperties)
                    {
                        TextStudioRenderPropertyWriter renderProperties =
                            _renderProperties.CreateWriter(i, 1, bound.intensity);
                        codeEffect.EvaluatePersistentFast(
                            ref state,
                            effectTime,
                            effectPlaybackTime,
                            phaseProgress,
                            unit.unitIndex,
                            unit.normalizedUnitIndex,
                            in renderProperties);
                    }
                    else
                    {
                        codeEffect.EvaluatePersistentFast(
                            ref state,
                            effectTime,
                            effectPlaybackTime,
                            phaseProgress,
                            unit.unitIndex,
                            unit.normalizedUnitIndex);
                    }
                }
                else
                {
                    int wordIndex = plan.GlyphToWordIndex != null && i < plan.GlyphToWordIndex.Length
                        ? Mathf.Max(0, plan.GlyphToWordIndex[i])
                        : 0;
                    int lineIndex = plan.GlyphToLineIndex != null && i < plan.GlyphToLineIndex.Length
                        ? Mathf.Max(0, plan.GlyphToLineIndex[i])
                        : 0;
                    EffectEvaluationContext context = new EffectEvaluationContext(
                        glyphIndex: i,
                        totalGlyphs: _stateCount,
                        wordIndex: wordIndex,
                        lineIndex: lineIndex,
                        phase: AnimationPhase.Persistent,
                        phaseProgress: phaseProgress,
                        time: effectTime,
                        playbackTime: effectPlaybackTime,
                        deltaTime: deltaTime,
                        normalizedIndex: unit.normalizedUnitIndex,
                        unit);
                    context = WithSpatialContext(in context, i);
                    EvaluateEffect(
                        bound.effect,
                        in bound.capabilities,
                        ref state,
                        in context);
                }
                state.renderChannels = bound.renderChannels;
            }
            _reusableSingleShortcut = reusableShortcut;
            _hasEvaluatedSinglePersistentFrame = true;
        }

        void EvaluateRevealOnly(
            RevealController reveal,
            bool overridePersistentPhaseProgress,
            float persistentPhaseProgress)
        {
            // Reveal-only states replace shortcut output, so a later single persistent frame starts from identity again.
            _hasEvaluatedSinglePersistentFrame = false;
            BeginRenderPropertyEvaluation();
            bool allGlyphsPersistent = reveal == null || reveal.AreAllGlyphsPersistent;
            float resolvedPersistentProgress = overridePersistentPhaseProgress
                ? persistentPhaseProgress
                : 1f;
            if (allGlyphsPersistent && Mathf.Approximately(resolvedPersistentProgress, 1f))
            {
                UseSharedPersistentIdentityStates();
                return;
            }

            EnsureWritableStates();
            _allEvaluatedGlyphsDirty = _stateCount > 0;
            // Typewriters run this loop for every glyph each frame, so resolve per-frame invariants once.
            TextStudioRenderPropertyView currentProperties = _renderProperties.View;
            TextStudioRenderPropertyView previousProperties = _previousRenderProperties.View;
            // Without properties on either side a glyph's properties only differ where a view is too short for it.
            bool compareProperties = _renderPropertyRegistryChanged ||
                                     currentProperties.PropertyCount != 0 ||
                                     previousProperties.PropertyCount != 0 ||
                                     currentProperties.IsCreated &&
                                     (currentProperties.GlyphCount < _stateCount || previousProperties.GlyphCount < _stateCount);
            bool hideUnrevealed = reveal != null && reveal.Mode != RevealMode.Instant;
            for (int i = 0; i < _stateCount; i++)
            {
                AnimationPhase phase;
                float phaseProgress;
                if (allGlyphsPersistent)
                {
                    phase = AnimationPhase.Persistent;
                    phaseProgress = overridePersistentPhaseProgress ? persistentPhaseProgress : 1f;
                }
                else
                    reveal.GetGlyphPhaseAndProgress(i, out phase, out phaseProgress);
                if (overridePersistentPhaseProgress && phase == AnimationPhase.Persistent)
                    phaseProgress = persistentPhaseProgress;

                bool hidden = phase == AnimationPhase.None ||
                              (hideUnrevealed && !reveal.IsGlyphRevealed(i));
                float alpha = hidden ? 0f : 1f;
                TextStudioRenderChannels channels = hidden
                    ? TextStudioRenderChannels.Alpha
                    : TextStudioRenderChannels.None;
                ref EvaluatedGlyphState state = ref _states[i];
                bool renderedStateChanged =
                    state.alpha != alpha ||
                    state.renderChannels != channels ||
                    compareProperties &&
                    (_renderPropertyRegistryChanged ||
                     !currentProperties.HasSameValuesForGlyph(i, in previousProperties));
                if (renderedStateChanged)
                {
                    state = EvaluatedGlyphState.Identity;
                    state.alpha = alpha;
                    state.renderChannels = channels;
                    _animDirty.SetDirty(i);
                }
                else
                {
                    _animDirty.Clear(i);
                    _allEvaluatedGlyphsDirty = false;
                }

                state.currentPhase = hidden ? AnimationPhase.None : phase;
                state.phaseProgress = phaseProgress;
            }
        }

        void UseSharedPersistentIdentityStates()
        {
            _allEvaluatedGlyphsDirty = _stateCount > 0;
            for (int i = 0; i < _stateCount; i++)
            {
                TextStudioRenderPropertyView previousProperties =
                    _previousRenderProperties.View;
                if (!_states[i].HasSameRenderedValues(in s_persistentIdentity) ||
                    HaveRenderPropertiesChangedForGlyph(
                        i,
                        in previousProperties))
                {
                    _animDirty.SetDirty(i);
                }
                else
                {
                    _animDirty.Clear(i);
                    _allEvaluatedGlyphsDirty = false;
                }
            }

            int capacity = _states != null && _states.Length >= _stateCount
                ? _states.Length
                : TextStudioBufferCapacity.ForCount(_stateCount);
            if (!_statesAreShared && _states != null && _states.Length > 0)
                _writableStates = _states;
            _states = GetPersistentIdentityStates(capacity);
            _statesAreShared = capacity > 0;
            _resetSharedStatesOnWrite = false;
        }

        void EnsureWritableStates()
        {
            if (!_statesAreShared)
                return;

            if (_states == null || _states.Length == 0)
            {
                _states = Array.Empty<EvaluatedGlyphState>();
            }
            else
            {
                // Frames that publish shared identity states and then receive external effects would otherwise
                // allocate a state array every frame.
                bool reuse = _writableStates != null && _writableStates.Length == _states.Length;
                EvaluatedGlyphState[] states = reuse ? _writableStates : new EvaluatedGlyphState[_states.Length];
                if (_resetSharedStatesOnWrite)
                {
                    for (int i = 0; i < _stateCount; i++)
                        states[i] = EvaluatedGlyphState.Identity;
                    if (reuse)
                        Array.Clear(states, _stateCount, states.Length - _stateCount);
                }
                else
                {
                    Array.Copy(_states, states, _states.Length);
                }

                _states = states;
                _writableStates = states;
            }

            _statesAreShared = false;
            _resetSharedStatesOnWrite = false;
        }

        static EvaluatedGlyphState[] GetPersistentIdentityStates(int capacity)
        {
            if (capacity <= 0)
                return Array.Empty<EvaluatedGlyphState>();

            if (s_persistentIdentityStates.TryGetValue(
                    capacity,
                    out EvaluatedGlyphState[] states))
            {
                return states;
            }

            states = new EvaluatedGlyphState[capacity];
            for (int i = 0; i < states.Length; i++)
                states[i] = s_persistentIdentity;
            s_persistentIdentityStates.Add(capacity, states);
            return states;
        }

        static EvaluatedGlyphState CreatePersistentIdentity()
        {
            EvaluatedGlyphState state = EvaluatedGlyphState.Identity;
            state.currentPhase = AnimationPhase.Persistent;
            state.phaseProgress = 1f;
            return state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetRenderedStateDirty(int glyphIndex, in EvaluatedGlyphState previousState)
        {
            TextStudioRenderPropertyView previousProperties =
                _previousRenderProperties.View;
            if (!_states[glyphIndex].HasSameRenderedValues(in previousState) ||
                HaveRenderPropertiesChangedForGlyph(
                    glyphIndex,
                    in previousProperties))
            {
                _animDirty.SetDirty(glyphIndex);
                return;
            }

            _animDirty.Clear(glyphIndex);
            _allEvaluatedGlyphsDirty = false;
        }

        void BeginRenderPropertyEvaluation()
        {
            int registrationVersion =
                TextStudioRenderPropertyRegistry.RegistrationVersion;
            _renderPropertyRegistryChanged =
                _renderPropertyRegistrationVersion != registrationVersion;
            _renderPropertyRegistrationVersion = registrationVersion;
            if (_renderPropertyRegistryChanged)
                RefreshBoundEffectCapabilities();
            TextStudioRenderPropertyBuffer previous = _previousRenderProperties;
            _previousRenderProperties = _renderProperties;
            _renderProperties = previous;
            _renderProperties.Clear(_stateCount);
        }

        void RefreshBoundEffectCapabilities()
        {
            for (int i = 0; i < _boundEffectCount; i++)
            {
                ref BoundEffect bound = ref _boundEffects[i];
                bound.capabilities = ResolveCapabilities(bound.effect);
                bound.renderChannels = bound.capabilities.RenderChannels;
            }
        }

        void ResetRenderPropertyBuffers(int glyphCount)
        {
            _renderProperties.Clear(glyphCount);
            _previousRenderProperties.Clear(glyphCount);
            _renderPropertyRegistrationVersion =
                TextStudioRenderPropertyRegistry.RegistrationVersion;
            _renderPropertyRegistryChanged = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool HaveRenderPropertiesChangedForGlyph(
            int glyphIndex,
            in TextStudioRenderPropertyView previousProperties)
        {
            return _renderPropertyRegistryChanged ||
                   !_renderProperties.View.HasSameValuesForGlyph(
                       glyphIndex,
                       in previousProperties);
        }

        void EvaluateEffect(
            ITextStudioEffect effect,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context)
        {
            TextStudioEffectCapabilities capabilities = ResolveCapabilities(effect);
            EvaluateEffect(
                effect,
                in capabilities,
                ref state,
                in context,
                _renderProperties,
                1f);
        }

        void EvaluateEffect(
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context)
        {
            EvaluateEffect(
                effect,
                in capabilities,
                ref state,
                in context,
                _renderProperties,
                1f);
        }

        void EvaluateEffect(
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context,
            TextStudioRenderPropertySink renderPropertySink,
            float renderPropertyInfluence,
            int renderPropertyGlyphIndex = -1)
        {
            int propertyGlyphIndex = renderPropertyGlyphIndex >= 0 ? renderPropertyGlyphIndex : context.glyphIndex;
            // Full motion evaluates at the authored rate without a motion-policy blend, so it needs neither the
            // pre-effect state nor a time-scaled context.
            if (TextStudioMotionSettings.Resolve(_motionPolicy) == TextStudioMotionPolicy.Full)
            {
                TextStudioRenderPropertyWriter fullMotionProperties = new TextStudioRenderPropertyWriter(
                    renderPropertySink,
                    propertyGlyphIndex,
                    1,
                    Mathf.Clamp01(renderPropertyInfluence));
                EffectEvaluationContext fullMotionContext = context.WithRenderProperties(in fullMotionProperties);
                effect.Evaluate(ref state, in fullMotionContext);
                return;
            }

            EvaluatedGlyphState previous = state;
            float resolvedInfluence = ResolveRenderPropertyInfluence(
                in capabilities,
                renderPropertyInfluence);
            TextStudioRenderPropertyWriter renderProperties =
                new TextStudioRenderPropertyWriter(
                    renderPropertySink,
                    propertyGlyphIndex,
                    1,
                    resolvedInfluence);
            float motionRate = TextStudioVisualPrimitiveMath.ResolveMotionRate(
                TextStudioMotionSettings.Resolve(_motionPolicy),
                capabilities.ReducedMotionBehavior,
                out _);
            EffectEvaluationContext motionContext = motionRate < 0.999999f
                ? context.WithTimeScale(motionRate)
                : context;
            EffectEvaluationContext propertyContext =
                motionContext.WithRenderProperties(in renderProperties);
            effect.Evaluate(ref state, in propertyContext);
            ApplyMotionPolicy(in capabilities, in previous, ref state);
        }

        bool EvaluateScopedEffect(
            ITextStudioEffect effect,
            TextStudioRenderChannels renderChannels,
            TextStudioEffectScopeChain scope,
            TextStudioComposedEnvelopeStack envelope,
            float intensity,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context)
        {
            TextStudioEffectCapabilities capabilities = ResolveCapabilities(effect);
            return EvaluateScopedEffect(
                effect,
                in capabilities,
                renderChannels,
                scope,
                envelope,
                intensity,
                ref state,
                in context,
                _renderProperties,
                1f);
        }

        bool EvaluateScopedEffect(
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            TextStudioRenderChannels renderChannels,
            TextStudioEffectScopeChain scope,
            TextStudioComposedEnvelopeStack envelope,
            float intensity,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context)
        {
            return EvaluateScopedEffect(
                effect,
                in capabilities,
                renderChannels,
                scope,
                envelope,
                intensity,
                ref state,
                in context,
                _renderProperties,
                1f);
        }

        bool EvaluateScopedEffect(
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            TextStudioRenderChannels renderChannels,
            TextStudioEffectScopeChain scope,
            TextStudioComposedEnvelopeStack envelope,
            float intensity,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context,
            TextStudioRenderPropertySink renderPropertySink,
            float renderPropertyInfluence,
            int renderPropertyGlyphIndex = -1)
        {
            float influence = Mathf.Clamp01(intensity);
            float progress = 0f;
            bool hasProgress = false;
            if (envelope != null)
            {
                if (UsesProgressPlayback(effect))
                {
                    influence *= envelope.EvaluateProgress(in context, out progress);
                    hasProgress = true;
                }
                else
                {
                    influence *= envelope.Evaluate(in context);
                }
            }
            if (scope != null)
                influence *= scope.Evaluate(in context);
            if (influence <= 0f)
                return false;

            if (hasProgress)
            {
                EffectEvaluationContext progressContext = context.WithEffectProgress(progress);
                EvaluateInfluencedEffect(effect, in capabilities, ref state, in progressContext, renderPropertySink,
                    influence, renderPropertyInfluence, renderPropertyGlyphIndex);
            }
            else
            {
                EvaluateInfluencedEffect(effect, in capabilities, ref state, in context, renderPropertySink,
                    influence, renderPropertyInfluence, renderPropertyGlyphIndex);
            }
            state.renderChannels |= renderChannels;
            return true;
        }

        void EvaluateInfluencedEffect(
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context,
            TextStudioRenderPropertySink renderPropertySink,
            float influence,
            float renderPropertyInfluence,
            int renderPropertyGlyphIndex)
        {
            float propertyInfluence = influence * Mathf.Clamp01(renderPropertyInfluence);
            // Only partial influence blends from the state before the effect, so full influence skips that copy.
            if (influence >= 1f)
            {
                EvaluateEffect(effect, in capabilities, ref state, in context, renderPropertySink, propertyInfluence,
                    renderPropertyGlyphIndex);
                return;
            }

            EvaluatedGlyphState preEffectState = state;
            EvaluateEffect(effect, in capabilities, ref state, in context, renderPropertySink, propertyInfluence,
                renderPropertyGlyphIndex);
            state = EvaluatedGlyphState.Lerp(preEffectState, state, influence);
        }

        float ResolveRenderPropertyInfluence(
            in TextStudioEffectCapabilities capabilities,
            float influence)
        {
            TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(_motionPolicy);
            if (policy == TextStudioMotionPolicy.Full)
                return Mathf.Clamp01(influence);
            if (capabilities.ReducedMotionBehavior ==
                TextStudioReducedMotionBehavior.Preserve)
            {
                return Mathf.Clamp01(influence);
            }
            if (policy == TextStudioMotionPolicy.None ||
                capabilities.ReducedMotionBehavior ==
                TextStudioReducedMotionBehavior.Disable)
            {
                return 0f;
            }
            return Mathf.Clamp01(influence);
        }

        void ApplyMotionPolicy(
            in TextStudioEffectCapabilities capabilities,
            in EvaluatedGlyphState previous,
            ref EvaluatedGlyphState state)
        {
            TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(_motionPolicy);
            if (policy == TextStudioMotionPolicy.Full)
                return;

            if (capabilities.ReducedMotionBehavior == TextStudioReducedMotionBehavior.Preserve)
                return;

            if (policy == TextStudioMotionPolicy.None ||
                capabilities.ReducedMotionBehavior == TextStudioReducedMotionBehavior.Disable)
            {
                RestoreMotionChannels(in previous, ref state, capabilities.RenderChannels);
                return;
            }

            const float ReducedMotionStrength = 0.25f;
            state.positionOffset = Vector3.LerpUnclamped(previous.positionOffset, state.positionOffset, ReducedMotionStrength);
            state.rotation = Quaternion.SlerpUnclamped(previous.rotation, state.rotation, ReducedMotionStrength);
            state.scale = Vector3.LerpUnclamped(previous.scale, state.scale, ReducedMotionStrength);
            state.skew = Vector2.LerpUnclamped(previous.skew, state.skew, ReducedMotionStrength);
            state.pivotOffset = Vector2.LerpUnclamped(previous.pivotOffset, state.pivotOffset, ReducedMotionStrength);
            state.cornerOffsets = TextStudioCornerOffsets.Lerp(
                in previous.cornerOffsets,
                in state.cornerOffsets,
                ReducedMotionStrength);
        }

        static void RestoreMotionChannels(
            in EvaluatedGlyphState previous,
            ref EvaluatedGlyphState state,
            TextStudioRenderChannels channels)
        {
            if ((channels & TextStudioRenderChannels.Position) != 0)
                state.positionOffset = previous.positionOffset;
            if ((channels & TextStudioRenderChannels.Rotation) != 0)
                state.rotation = previous.rotation;
            if ((channels & TextStudioRenderChannels.Scale) != 0)
                state.scale = previous.scale;
            if ((channels & TextStudioRenderChannels.Alpha) != 0)
                state.alpha = previous.alpha;
            if ((channels & TextStudioRenderChannels.Color) != 0)
            {
                state.colorOverride = previous.colorOverride;
                state.colorBlendMode = previous.colorBlendMode;
                state.colorBlendWeight = previous.colorBlendWeight;
                state.colorMultiplier = previous.colorMultiplier;
                state.colorAdditive = previous.colorAdditive;
                state.colorCompositionVersion = previous.colorCompositionVersion;
            }
            if ((channels & TextStudioRenderChannels.Skew) != 0)
                state.skew = previous.skew;
            if ((channels & TextStudioRenderChannels.Pivot) != 0)
                state.pivotOffset = previous.pivotOffset;
            if ((channels & TextStudioRenderChannels.CornerOffsets) != 0)
                state.cornerOffsets = previous.cornerOffsets;
            if ((channels & TextStudioRenderChannels.UvOffset) != 0)
                state.uvOffset = previous.uvOffset;
        }

        internal void BlendExternalRenderProperties(
            int glyphIndex,
            TextStudioRenderPropertyGlyphAccumulator before,
            TextStudioRenderPropertyGlyphAccumulator after,
            float intensity)
        {
            _renderProperties.BlendGlyph(glyphIndex, before, after, intensity);
        }

        internal static TextStudioEffectCapabilities ResolveCapabilities(
            ITextStudioEffect effect)
        {
            if (effect is ITextStudioEffectCapabilitiesProvider provider)
                return provider.Capabilities;

            return new TextStudioEffectCapabilities(
                ResolveRenderChannels(effect),
                TextStudioPhaseMask.All);
        }

        static TextStudioRenderChannels ResolveRenderChannels(ITextStudioEffect effect)
        {
            if (effect is ITextStudioEffectCapabilitiesProvider provider)
                return provider.Capabilities.RenderChannels;

            return effect switch
            {
                WaveEffect or ShakeEffect or BounceEffect or FloatEffect or
                    SlideHorizontalEffect or SlideVerticalEffect or DecryptEffect =>
                    TextStudioRenderChannels.Position,
                WiggleEffect =>
                    TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation,
                DangleEffect =>
                    TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Pivot,
                SwingEffect or PendulumEffect or RotateEffect =>
                    TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Pivot,
                PulseEffect => TextStudioRenderChannels.Scale | TextStudioRenderChannels.Alpha,
                BlinkEffect or TypewriterEffect => TextStudioRenderChannels.Alpha,
                ShimmerEffect => TextStudioRenderChannels.Scale | TextStudioRenderChannels.Color,
                GradientFlowEffect or ColorSweepEffect or ColorPulseEffect or RainbowEffect =>
                    TextStudioRenderChannels.Color,
                FadeEffect => TextStudioRenderChannels.Alpha,
                PopEffect => TextStudioRenderChannels.Scale | TextStudioRenderChannels.Alpha,
                SizeEffect or EmphasisEffect => TextStudioRenderChannels.Scale,
                ShearEffect => TextStudioRenderChannels.Skew,
                ExpandEffect => TextStudioRenderChannels.Scale | TextStudioRenderChannels.Alpha,
                GlitchEffect => TextStudioRenderChannels.Position | TextStudioRenderChannels.Color,
                _ => TextStudioRenderChannels.All
            };
        }

        bool TryResolveVisibilityOverride(
            int glyphIndex,
            RevealController reveal,
            out AnimationPhase phase,
            out float phaseProgress,
            out bool hidden)
        {
            phase = AnimationPhase.None;
            phaseProgress = 0f;
            hidden = false;
            if (_visibilityOverrides == null || glyphIndex < 0 || glyphIndex >= _stateCount)
                return false;

            ref GlyphVisibilityOverride visibilityOverride = ref _visibilityOverrides[glyphIndex];
            if (visibilityOverride.target == 0)
                return false;

            bool visible = visibilityOverride.target > 0;
            AnimationPhase transition = visibilityOverride.transitionPhase;
            if (transition == AnimationPhase.Appearance && !_appearanceEffectsEnabled)
                CompleteVisibilityTransition(ref visibilityOverride);
            else if (transition == AnimationPhase.Disappearance && !_disappearanceEffectsEnabled)
                CompleteVisibilityTransition(ref visibilityOverride);

            transition = visibilityOverride.transitionPhase;
            if (transition != AnimationPhase.None)
            {
                float duration = transition == AnimationPhase.Appearance
                    ? (reveal != null ? reveal.AppearanceDuration : 0.3f)
                    : (reveal != null ? reveal.DisappearanceDuration : 0.3f);
                duration = Mathf.Max(0.0001f, duration);
                float elapsed = Mathf.Max(0f, _time - visibilityOverride.transitionStartTime);
                if (elapsed + 0.0001f >= duration)
                {
                    CompleteVisibilityTransition(ref visibilityOverride);
                }
                else
                {
                    phase = transition;
                    phaseProgress = Mathf.Clamp01(elapsed / duration);
                    return true;
                }
            }

            hidden = !visible;
            phase = visible ? AnimationPhase.Persistent : AnimationPhase.None;
            phaseProgress = visible ? 1f : 0f;
            return true;
        }

        void CompleteVisibilityTransition(ref GlyphVisibilityOverride visibilityOverride)
        {
            if (visibilityOverride.transitionPhase == AnimationPhase.None)
                return;

            visibilityOverride.transitionPhase = AnimationPhase.None;
            _activeVisibilityTransitionCount = Mathf.Max(0, _activeVisibilityTransitionCount - 1);
        }

        bool TryCreateEffectContext(
            in BoundEffect bound,
            float effectDelay,
            RevealController reveal,
            int glyphIndex,
            int wordIndex,
            int lineIndex,
            AnimationPhase glyphPhase,
            float glyphPhaseProgress,
            float deltaTime,
            in TextStudioEffectUnitContext unit,
            out EffectEvaluationContext context)
        {
            AnimationPhase effectPhase = bound.forcePhase == AnimationPhase.None ? glyphPhase : bound.forcePhase;
            if (!IsEffectFamilyEnabled(effectPhase))
            {
                context = default;
                return false;
            }
            if (bound.forcePhase != AnimationPhase.None && glyphPhase != bound.forcePhase)
            {
                context = default;
                return false;
            }

            if (!IncludesPhase(bound.phaseMask, effectPhase))
            {
                context = default;
                return false;
            }

            float effectProgress = glyphPhaseProgress;
            if (bound.forcePhase != AnimationPhase.None)
                effectProgress = GetStaggeredPhaseProgress(
                    reveal,
                    effectDelay,
                    bound.phaseSpeed,
                    effectPhase,
                    glyphPhaseProgress);

            float effectTime = _time;
            float effectPlaybackTime = _playbackTime;
            bool usesContinuousPhaseClock =
                effectPhase == AnimationPhase.Persistent ||
                (effectPhase == AnimationPhase.Disappearance &&
                 IncludesPhase(bound.phaseMask, AnimationPhase.Persistent));
            if (usesContinuousPhaseClock)
            {
                effectTime = GetPersistentEffectTime(
                    effectDelay,
                    bound.phaseSpeed,
                    _time);
                effectPlaybackTime = GetPersistentEffectTime(
                    effectDelay,
                    bound.phaseSpeed,
                    _playbackTime);
            }
            effectTime += bound.timeOffset;
            effectPlaybackTime += bound.timeOffset;
            effectTime = Mathf.Max(0f, effectTime);
            effectPlaybackTime = Mathf.Max(0f, effectPlaybackTime);

            context = new EffectEvaluationContext(
                glyphIndex: glyphIndex,
                totalGlyphs: _stateCount,
                wordIndex: wordIndex,
                lineIndex: lineIndex,
                phase: effectPhase,
                phaseProgress: effectProgress,
                time: effectTime,
                playbackTime: effectPlaybackTime,
                deltaTime: deltaTime,
                normalizedIndex: unit.normalizedUnitIndex,
                unit);
            if (usesContinuousPhaseClock && effectPhase != AnimationPhase.Persistent)
                context = context.WithContinuousPhaseClock();
            context = WithSpatialContext(in context, glyphIndex);
            return true;
        }

        EffectEvaluationContext WithSpatialContext(
            in EffectEvaluationContext context,
            int glyphIndex)
        {
            Vector3 source = _sourcePositions != null &&
                glyphIndex >= 0 && glyphIndex < _spatialCount && glyphIndex < _sourcePositions.Length
                ? _sourcePositions[glyphIndex]
                : new Vector3(context.normalizedIndex, 0f, 0f);
            Vector3 spatial = _usePlacements &&
                glyphIndex >= 0 && glyphIndex < _spatialCount &&
                glyphIndex < _placementCount && glyphIndex < _placements.Length
                ? _placements[glyphIndex].position
                : source;
            float effectUnitScale = _effectUnitScales != null &&
                                    glyphIndex >= 0 &&
                                    glyphIndex < _effectUnitScales.Length
                ? _effectUnitScales[glyphIndex]
                : 1f;
            Vector3 normalized = _sourceTextNormalizedPositions != null &&
                glyphIndex >= 0 && glyphIndex < _spatialCount &&
                glyphIndex < _sourceTextNormalizedPositions.Length
                ? _sourceTextNormalizedPositions[glyphIndex]
                : new Vector3(context.normalizedIndex, 0f, 0f);
            bool hasSourceColor = _sourceColorValidity != null &&
                                  glyphIndex >= 0 &&
                                  glyphIndex < _spatialCount &&
                                  glyphIndex < _sourceColorValidity.Length &&
                                  _sourceColorValidity[glyphIndex] &&
                                  _sourceColors != null &&
                                  glyphIndex < _sourceColors.Length;
            Color sourceColor = hasSourceColor
                ? (Color)_sourceColors[glyphIndex].Sample(0.5f, 0.5f)
                : Color.white;
            bool hasSourceAreaBounds = TryResolveSourceAreaBounds(
                in context,
                out TextStudioGlyphVisualBounds sourceAreaBounds);
            EffectEvaluationContext spatialContext = context.WithSpatialSource(
                source,
                spatial,
                _localToWorld.MultiplyPoint3x4(spatial),
                effectUnitScale,
                normalized,
                sourceColor,
                hasSourceColor,
                hasSourceAreaBounds ? sourceAreaBounds : context.sourceAreaBounds,
                hasSourceAreaBounds || context.hasSourceAreaBounds);
            return _hasInfluenceTarget
                ? spatialContext.WithInfluenceTarget(
                    _influenceTargetLocalPosition,
                    _influenceTargetWorldPosition,
                    _influenceTargetSourceTextNormalizedPosition)
                : spatialContext;
        }

        bool TryResolveSourceAreaBounds(
            in EffectEvaluationContext context,
            out TextStudioGlyphVisualBounds bounds)
        {
            int start = Mathf.Clamp(context.unitStartGlyph, 0, _spatialCount);
            int end = Mathf.Clamp(
                context.unitStartGlyph + context.unitGlyphCount,
                start,
                _spatialCount);
            long key = ((long)start << 32) | (uint)(end - start);
            if (_sourceAreaBoundsCache.TryGetValue(key, out bounds))
                return true;

            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool found = AccumulateSourceBounds(start, end, true, ref minimum, ref maximum);
            bool visible = found;
            if (!found)
                found = AccumulateSourceBounds(start, end, false, ref minimum, ref maximum);
            if (!found && _sourcePositions != null)
            {
                int positionEnd = Mathf.Min(end, _sourcePositions.Length);
                for (int i = start; i < positionEnd; i++)
                {
                    Vector3 position = _sourcePositions[i];
                    if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
                        continue;
                    Vector2 point = new Vector2(position.x, position.y);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                    found = true;
                }
            }
            if (!found)
            {
                Vector2 point = new Vector2(context.sourcePosition.x, context.sourcePosition.y);
                minimum = point;
                maximum = point;
            }

            bounds = new TextStudioGlyphVisualBounds(minimum, maximum, visible);
            _sourceAreaBoundsCache[key] = bounds;
            return true;
        }

        static bool UsesProgressPlayback(ITextStudioEffect effect)
        {
            return effect is ITextStudioEffectMetadataProvider provider &&
                   provider.Metadata?.PlaybackRole == TextStudioEffectPlaybackRole.Progress;
        }

        bool AccumulateSourceBounds(
            int start,
            int end,
            bool visibleOnly,
            ref Vector2 minimum,
            ref Vector2 maximum)
        {
            if (_sourceVisualBounds == null)
                return false;

            bool found = false;
            int boundsEnd = Mathf.Min(end, _sourceVisualBounds.Length);
            for (int i = start; i < boundsEnd; i++)
            {
                TextStudioGlyphVisualBounds glyphBounds = _sourceVisualBounds[i];
                if (!glyphBounds.IsValid || (visibleOnly && !glyphBounds.IsVisible))
                    continue;
                minimum = Vector2.Min(minimum, glyphBounds.Minimum);
                maximum = Vector2.Max(maximum, glyphBounds.Maximum);
                found = true;
            }
            return found;
        }

        static bool IncludesPhase(TextStudioPhaseMask phaseMask, AnimationPhase phase)
        {
            if (phaseMask == TextStudioPhaseMask.None)
                phaseMask = TextStudioPhaseMask.All;

            switch (phase)
            {
                case AnimationPhase.Appearance:
                    return (phaseMask & TextStudioPhaseMask.Reveal) != 0;
                case AnimationPhase.Persistent:
                    return (phaseMask & TextStudioPhaseMask.Active) != 0;
                case AnimationPhase.Disappearance:
                    return (phaseMask & TextStudioPhaseMask.Hide) != 0;
                case AnimationPhase.None:
                default:
                    return false;
            }
        }

        static float GetPhaseProgress(RevealController reveal, int glyphIndex, AnimationPhase phase)
        {
            if (reveal == null)
                return phase == AnimationPhase.Persistent ? 1f : 0f;

            switch (phase)
            {
                case AnimationPhase.Appearance:
                    return reveal.GetAppearanceProgress(glyphIndex);
                case AnimationPhase.Disappearance:
                    return reveal.GetDisappearanceProgress(glyphIndex);
                case AnimationPhase.Persistent:
                    return 1f;
                case AnimationPhase.None:
                default:
                    return 0f;
            }
        }

        static float GetStaggeredPhaseProgress(
            RevealController reveal,
            float delay,
            float speed,
            AnimationPhase phase,
            float baseProgress)
        {
            if (reveal == null ||
                (phase != AnimationPhase.Appearance && phase != AnimationPhase.Disappearance))
            {
                return baseProgress;
            }

            float duration = phase == AnimationPhase.Disappearance
                ? reveal.DisappearanceDuration
                : reveal.AppearanceDuration;
            duration = Mathf.Max(0.0001f, duration);
            float elapsed = baseProgress * duration - delay;
            return Mathf.Clamp01(elapsed * speed / duration);
        }

        bool IsEffectFamilyEnabled(AnimationPhase phase)
        {
            switch (phase)
            {
                case AnimationPhase.Appearance:
                    return _appearanceEffectsEnabled;
                case AnimationPhase.Disappearance:
                    return _disappearanceEffectsEnabled;
                case AnimationPhase.Persistent:
                    return _persistentEffectsEnabled;
                default:
                    return false;
            }
        }

        void SetEffectFamilyEnabled(ref bool field, bool value)
        {
            if (field == value)
                return;

            field = value;
            _animDirty.EnsureCapacity(_stateCount);
            _animDirty.SetAllDirty(_stateCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float GetPersistentEffectTime(
            float delay,
            float speed,
            float time)
        {
            return Mathf.Max(0f, time - delay) * speed;
        }

        void BuildGlyphEffectLookup(TextStudioCompiledPlan plan)
        {
            int glyphCount = _stateCount;
            if (_glyphEffectStart == null || _glyphEffectStart.Length < glyphCount)
            {
                int capacity = TextStudioBufferCapacity.ForCount(glyphCount);
                _glyphEffectStart = new int[capacity];
                _glyphEffectCount = new int[capacity];
                _glyphEffectWrite = new int[capacity];
            }

            if (glyphCount > 0)
            {
                Array.Clear(_glyphEffectStart, 0, glyphCount);
                Array.Clear(_glyphEffectCount, 0, glyphCount);
                Array.Clear(_glyphEffectWrite, 0, glyphCount);
            }

            int totalBindings = 0;
            for (int e = 0; e < _boundEffectCount; e++)
            {
                ref BoundEffect bound = ref _boundEffects[e];
                if (bound.rendererDeferred)
                    continue;
                int start = Mathf.Clamp(bound.glyphStart, 0, glyphCount);
                int end = Mathf.Clamp(bound.glyphEnd, start, glyphCount);
                for (int i = start; i < end; i++)
                {
                    if (bound.selectorMembers != null &&
                        (i >= bound.selectorMembers.Length || !bound.selectorMembers[i]))
                        continue;
                    if (TextStudioEffectAreaResolver.TryResolveUnit(
                            plan,
                            bound.glyphStart,
                            bound.glyphEnd - bound.glyphStart,
                            bound.area,
                            i,
                            out _))
                    {
                        _glyphEffectCount[i]++;
                        totalBindings++;
                    }
                }
            }

            if (_glyphEffectIndices == null ||
                _glyphEffectIndices.Length < totalBindings ||
                _glyphEffectDelays == null ||
                _glyphEffectDelays.Length < totalBindings ||
                _glyphEffectUnits == null ||
                _glyphEffectUnits.Length < totalBindings ||
                _glyphEffectReusesPreviousContext == null ||
                _glyphEffectReusesPreviousContext.Length < totalBindings)
            {
                int capacity = TextStudioBufferCapacity.ForCount(totalBindings);
                _glyphEffectIndices = new int[capacity];
                _glyphEffectDelays = new float[capacity];
                _glyphEffectUnits = new TextStudioEffectUnitContext[capacity];
                _glyphEffectReusesPreviousContext = new bool[capacity];
            }
            else if (totalBindings > 0)
            {
                Array.Clear(_glyphEffectReusesPreviousContext, 0, totalBindings);
            }

            int cursor = 0;
            for (int i = 0; i < glyphCount; i++)
            {
                _glyphEffectStart[i] = cursor;
                cursor += _glyphEffectCount[i];
            }

            for (int e = 0; e < _boundEffectCount; e++)
            {
                ref BoundEffect bound = ref _boundEffects[e];
                if (bound.rendererDeferred)
                    continue;
                int start = Mathf.Clamp(bound.glyphStart, 0, glyphCount);
                int end = Mathf.Clamp(bound.glyphEnd, start, glyphCount);
                for (int i = start; i < end; i++)
                {
                    if (bound.selectorMembers != null &&
                        (i >= bound.selectorMembers.Length || !bound.selectorMembers[i]))
                        continue;
                    if (!TextStudioEffectAreaResolver.TryResolveUnit(
                            plan,
                            bound.glyphStart,
                            bound.glyphEnd - bound.glyphStart,
                            bound.area,
                            i,
                            out TextStudioEffectUnitContext unit))
                    {
                        continue;
                    }

                    int writeIndex = _glyphEffectStart[i] + _glyphEffectWrite[i];
                    _glyphEffectIndices[writeIndex] = e;
                    _glyphEffectUnits[writeIndex] = unit;
                    int wordIndex = plan.GlyphToWordIndex != null && i < plan.GlyphToWordIndex.Length
                        ? Mathf.Max(0, plan.GlyphToWordIndex[i])
                        : 0;
                    int lineIndex = plan.GlyphToLineIndex != null && i < plan.GlyphToLineIndex.Length
                        ? Mathf.Max(0, plan.GlyphToLineIndex[i])
                        : 0;
                    float timingDelay = bound.phaseParams.ComputeDelay(unit.unitIndex, wordIndex, lineIndex);
                    float sequenceDelay = TextStudioSequenceUtility.ComputeDelay(
                        unit.unitIndex,
                        unit.unitCount,
                        in bound.sequence);
                    _glyphEffectDelays[writeIndex] = Mathf.Max(0f, timingDelay + sequenceDelay);
                    _glyphEffectWrite[i]++;
                }
            }

            for (int i = 0; i < glyphCount; i++)
            {
                int start = _glyphEffectStart[i];
                int count = _glyphEffectCount[i];
                for (int e = 1; e < count; e++)
                {
                    int previousLookupIndex = start + e - 1;
                    int lookupIndex = start + e;
                    ref BoundEffect previous = ref _boundEffects[_glyphEffectIndices[previousLookupIndex]];
                    ref BoundEffect current = ref _boundEffects[_glyphEffectIndices[lookupIndex]];
                    _glyphEffectReusesPreviousContext[lookupIndex] = CanReuseEffectContext(
                        in previous,
                        _glyphEffectDelays[previousLookupIndex],
                        in _glyphEffectUnits[previousLookupIndex],
                        in current,
                        _glyphEffectDelays[lookupIndex],
                        in _glyphEffectUnits[lookupIndex]);
                }
            }

            _hasUniformSinglePersistentEffect =
                _boundEffectCount == 1 &&
                _boundEffects[0].appliesToPersistentPhase &&
                _boundEffects[0].intensity >= 1f &&
                _boundEffects[0].scope == null &&
                _boundEffects[0].envelope == null &&
                _boundEffects[0].phaseBlendGroupId <= 0;
            if (_hasUniformSinglePersistentEffect)
            {
                for (int i = 0; i < glyphCount; i++)
                {
                    int lookupIndex = _glyphEffectStart[i];
                    if (_glyphEffectCount[i] != 1 || _glyphEffectIndices[lookupIndex] != 0)
                    {
                        _hasUniformSinglePersistentEffect = false;
                        break;
                    }
                }
            }

            _hasSharedPersistentTiming = _hasUniformSinglePersistentEffect;
            if (_hasSharedPersistentTiming && glyphCount > 1)
            {
                float firstDelay = _glyphEffectDelays[_glyphEffectStart[0]];
                for (int i = 1; i < glyphCount; i++)
                {
                    if (_glyphEffectDelays[_glyphEffectStart[i]] == firstDelay)
                        continue;
                    _hasSharedPersistentTiming = false;
                    break;
                }
            }

            _hasUniformPersistentState =
                _hasSharedPersistentTiming &&
                _boundEffects[0].effect is CodeEffectBase codeEffect &&
                codeEffect.SupportsPersistentFastPath &&
                codeEffect.SupportsUniformPersistentState;

            _glyphEffectLookupDirty = false;
        }

        static bool CanReuseEffectContext(
            in BoundEffect previous,
            float previousDelay,
            in TextStudioEffectUnitContext previousUnit,
            in BoundEffect current,
            float currentDelay,
            in TextStudioEffectUnitContext currentUnit)
        {
            return previous.forcePhase == current.forcePhase &&
                   previous.phaseMask == current.phaseMask &&
                   previous.phaseSpeed == current.phaseSpeed &&
                   previous.timeOffset == current.timeOffset &&
                   previousDelay == currentDelay &&
                   previousUnit.area == currentUnit.area &&
                   previousUnit.groupSize == currentUnit.groupSize &&
                   previousUnit.unitIndex == currentUnit.unitIndex &&
                   previousUnit.unitCount == currentUnit.unitCount &&
                   previousUnit.unitStartGlyph == currentUnit.unitStartGlyph &&
                   previousUnit.unitGlyphCount == currentUnit.unitGlyphCount &&
                   previousUnit.indexInUnit == currentUnit.indexInUnit &&
                   previousUnit.normalizedIndexInUnit == currentUnit.normalizedIndexInUnit &&
                   previousUnit.normalizedUnitIndex == currentUnit.normalizedUnitIndex &&
                   previousUnit.normalizedIndexInBinding == currentUnit.normalizedIndexInBinding &&
                   previousUnit.randomSeedIndex == currentUnit.randomSeedIndex &&
                   previousUnit.playbackOffset == currentUnit.playbackOffset;
        }
    }
}
