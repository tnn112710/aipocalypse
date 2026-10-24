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
        // Read-only identity states that Initialize publishes until a write copies them (EnsureWritableStates).
        static readonly Dictionary<int, EvaluatedGlyphState[]> s_identityStates =
            new Dictionary<int, EvaluatedGlyphState[]>();
        const int MaximumSharedIdentityCapacity = 4096;
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
        // The bound effect, glyph and unit of every binding entry the counting pass resolved, in fill order.
        int[] _resolvedLookupBindings = Array.Empty<int>();
        int[] _resolvedLookupGlyphs = Array.Empty<int>();
        TextStudioEffectUnitContext[] _resolvedLookupUnits = Array.Empty<TextStudioEffectUnitContext>();
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
            // A built-in transition effect at full influence without scope, envelope or blend group: in Reveal and Hide
            // phases it evaluates from its timing alone (CodeEffectBase.EvaluateTransitionFast).
            public bool transitionFastPath;
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
            else
            {
                // Every state starts as identity, so text that is never evaluated, such as plain TMP pass-through text, reads a
                // shared identity array instead of allocating and filling its own. The first write resets an owned array to the
                // identity states this branch used to write (EnsureWritableStates), reusing the array this evaluator already owns.
                // Long text keeps owned states, so the shared arrays stay small.
                int capacity = _states != null && _states.Length >= glyphCount
                    ? _states.Length
                    : TextStudioBufferCapacity.ForCount(glyphCount);
                if (capacity <= MaximumSharedIdentityCapacity)
                {
                    if (_states != null && _states.Length > 0)
                        _writableStates = _states;
                    _states = GetIdentityStates(capacity);
                    _statesAreShared = capacity > 0;
                    _resetSharedStatesOnWrite = _statesAreShared;
                }
                else if (_states == null || _states.Length < glyphCount)
                {
                    _states = new EvaluatedGlyphState[capacity];
                    _resetSharedStatesOnWrite = false;
                }
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

            _boundPlan = null;
            BeginEvaluatorReuse();
            if (plan.EffectBindings == null)
            {
                EndEvaluatorReuse();
                return;
            }

            _boundEffectsExtendable = true;
            for (int i = 0; i < plan.EffectBindingCount; i++)
                BindPlanEffect(plan, i);
            EndEvaluatorReuse();
            BuildGlyphEffectLookup(plan);
            RecordBoundPlan(plan);
        }

        /// <summary>
        /// Binds the effect bindings an appended markup compile added from <paramref name="keptBindingCount"/> on, keeping
        /// the effects bound for the bindings before, when <see cref="BindEffects"/> would bind those to the same objects:
        /// it reuses every evaluator it created for them in order, while their bindings and the glyphs before
        /// <paramref name="keptGlyphCount"/> are unchanged in the plan of version <paramref name="baseSourceVersion"/> they
        /// were bound from. The glyph lookup keeps the entries of those glyphs when no kept effect reads segmentation that the
        /// append can change. Returns false, changing nothing, when <see cref="BindEffects"/> must run instead.
        /// </summary>
        internal bool TryBindAppendedEffects(
            TextStudioCompiledPlan plan,
            int keptBindingCount,
            int keptGlyphCount,
            uint baseSourceVersion)
        {
            if (!_boundEffectsExtendable ||
                !ReferenceEquals(plan, _boundPlan) ||
                plan.EffectBindings == null ||
                _boundPlanSourceVersion != baseSourceVersion ||
                _boundPlanBindingCount != keptBindingCount ||
                keptBindingCount > plan.EffectBindingCount ||
                _rendererDeferredBoundEffectCount != 0 ||
                _reusableEvaluatorCount != 0)
            {
                return false;
            }

            for (int i = keptBindingCount; i < plan.EffectBindingCount; i++)
            {
                if (plan.EffectBindings[i].selectorScope != null)
                    return false;
            }

            bool extendsLookup =
                _lookupGeneration == EffectBindingGeneration &&
                !_lookupStale &&
                keptGlyphCount <= _lookupGlyphCount &&
                _allBoundEffectsKeepAppendedLookup;
            unchecked
            {
                EffectBindingGeneration++;
            }

            // What ClearBoundEffects resets besides the bound effects themselves; the lookup computes the rest again.
            _evaluatedStatesAreUniform = false;
            _hasEvaluatedSinglePersistentFrame = false;
            _hasUniformSinglePersistentEffect = false;
            _hasSharedPersistentTiming = false;
            _hasUniformPersistentState = false;
            // A rebind takes each evaluator it created before for the same kept binding, in creation order, and resets it.
            for (int i = 0; i < _createdEvaluatorCount; i++)
                _createdEvaluators[i].evaluator.Reset();
            ReusedEvaluatorCount = _createdEvaluatorCount;
            // A rebind resolves capabilities again, which a changed render property registration can change.
            if (_boundRenderPropertyRegistrationVersion != TextStudioRenderPropertyRegistry.RegistrationVersion)
                RefreshBoundEffectCapabilities();

            int firstAppendedEffect = _boundEffectCount;
            for (int i = keptBindingCount; i < plan.EffectBindingCount; i++)
                BindPlanEffect(plan, i);
            if (extendsLookup)
                BuildAppendedGlyphEffectLookup(plan, keptGlyphCount, firstAppendedEffect);
            else
                BuildGlyphEffectLookup(plan);
            RecordBoundPlan(plan);
            return true;
        }

        // The plan bindings the bound effects were bound from, which TryBindAppendedEffects extends.
        TextStudioCompiledPlan _boundPlan;
        uint _boundPlanSourceVersion;
        int _boundPlanBindingCount;
        int _boundRenderPropertyRegistrationVersion;
        // Whether a rebind would bind every bound effect to the same object: the binding's own effect, no effect, or a
        // reusable code evaluator this evaluator created; selector members also depend on the unit count.
        bool _boundEffectsExtendable;
        // Every bound effect resolves its glyph units from its own range and times them without word or line offsets, so an
        // append that keeps the glyphs before it keeps their lookup entries.
        bool _allBoundEffectsKeepAppendedLookup = true;
        uint _lookupGeneration;
        int _lookupGlyphCount;
        bool _lookupStale;

        void RecordBoundPlan(TextStudioCompiledPlan plan)
        {
            _boundPlan = plan;
            _boundPlanSourceVersion = plan.SourceVersion;
            _boundPlanBindingCount = plan.EffectBindingCount;
            _boundRenderPropertyRegistrationVersion = TextStudioRenderPropertyRegistry.RegistrationVersion;
        }

        void BindPlanEffect(TextStudioCompiledPlan plan, int i)
        {
            ref EffectBinding binding = ref plan.EffectBindings[i];

            ITextStudioEffect boundEffect = binding.effect ?? CreateOrReuseEvaluator(in binding);
            if (binding.selectorScope != null ||
                binding.effect == null && boundEffect != null && !_lastEvaluatorIsReusable)
            {
                _boundEffectsExtendable = false;
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

        // A detached evaluator created for an effect id, kept for the next rebind of an equal invocation.
        struct CreatedEvaluator
        {
            public string effectId;
            public ITextStudioEffectDefinition definition;
            public TextStudioTagParameters parameters;
            public AnimationPhase phase;
            public CodeEffectBase evaluator;
        }

        // The previous binding generation's evaluators, offered to this one, and the evaluators this one created.
        CreatedEvaluator[] _reusableEvaluators = Array.Empty<CreatedEvaluator>();
        int _reusableEvaluatorCount;
        CreatedEvaluator[] _createdEvaluators = Array.Empty<CreatedEvaluator>();
        int _createdEvaluatorCount;

        // Evaluators the last BindEffects reused instead of creating.
        internal int ReusedEvaluatorCount { get; private set; }

        void BeginEvaluatorReuse()
        {
            (_reusableEvaluators, _createdEvaluators) = (_createdEvaluators, _reusableEvaluators);
            _reusableEvaluatorCount = _createdEvaluatorCount;
            _createdEvaluatorCount = 0;
            ReusedEvaluatorCount = 0;
        }

        void EndEvaluatorReuse()
        {
            if (_reusableEvaluatorCount > 0)
                Array.Clear(_reusableEvaluators, 0, _reusableEvaluatorCount);
            _reusableEvaluatorCount = 0;
        }

        // An explicit refresh re-resolves definitions, so the next rebind creates every evaluator again.
        internal void DiscardReusableEvaluators()
        {
            if (_createdEvaluatorCount > 0)
                Array.Clear(_createdEvaluators, 0, _createdEvaluatorCount);
            _createdEvaluatorCount = 0;
            _boundEffectsExtendable = false;
        }

        // Every text change rebinds its effects, and creating each evaluator cloned its definition and parsed its
        // parameters again. A CodeEffectBase evaluator is a stateless clone of its registered definition with the
        // invocation's parameters applied, so one of the previous generation from the same definition, id, parameters and
        // phase is the evaluator a new clone would be. Code Effect assets and other evaluators may hold per-glyph state and
        // are always created.
        // Whether the evaluator the last CreateOrReuseEvaluator returned is one the next rebind reuses.
        bool _lastEvaluatorIsReusable;

        ITextStudioEffect CreateOrReuseEvaluator(in EffectBinding binding)
        {
            _lastEvaluatorIsReusable = false;
            if (!TextStudioEffects.TryGetDefinition(binding.effectId, out ITextStudioEffectDefinition definition) ||
                definition == null)
            {
                return null;
            }

            ITextStudioEffect effect = TakeReusableEvaluator(binding.effectId, definition, in binding.parameters, binding.phase);
            if (effect != null)
            {
                ReusedEvaluatorCount++;
                effect.Reset();
            }
            else if (!TextStudioEffects.TryCreate(definition, in binding.parameters, out effect))
            {
                return null;
            }

            if (definition is CodeEffectBase && effect is CodeEffectBase evaluator)
            {
                if (_createdEvaluators.Length <= _createdEvaluatorCount)
                    Array.Resize(ref _createdEvaluators, Math.Max(4, _createdEvaluators.Length * 2));
                _createdEvaluators[_createdEvaluatorCount++] = new CreatedEvaluator
                {
                    effectId = binding.effectId,
                    definition = definition,
                    parameters = binding.parameters,
                    phase = binding.phase,
                    evaluator = evaluator
                };
                _lastEvaluatorIsReusable = true;
            }
            else if (ReferenceEquals(effect, definition))
            {
                // A definition that is not a factory is its own evaluator in every rebind.
                _lastEvaluatorIsReusable = true;
            }

            return effect;
        }

        CodeEffectBase TakeReusableEvaluator(
            string effectId,
            ITextStudioEffectDefinition definition,
            in TextStudioTagParameters parameters,
            AnimationPhase phase)
        {
            if (!(definition is CodeEffectBase))
                return null;

            // Unchanged texts bind in the same order, so the evaluator at this creation index matches first.
            int preferred = _createdEvaluatorCount;
            if (preferred < _reusableEvaluatorCount &&
                IsReusableFor(in _reusableEvaluators[preferred], effectId, definition, in parameters, phase))
            {
                return TakeReusableEvaluatorAt(preferred);
            }

            for (int i = 0; i < _reusableEvaluatorCount; i++)
            {
                if (i != preferred &&
                    IsReusableFor(in _reusableEvaluators[i], effectId, definition, in parameters, phase))
                {
                    return TakeReusableEvaluatorAt(i);
                }
            }

            return null;
        }

        CodeEffectBase TakeReusableEvaluatorAt(int index)
        {
            CodeEffectBase evaluator = _reusableEvaluators[index].evaluator;
            _reusableEvaluators[index] = default;
            return evaluator;
        }

        static bool IsReusableFor(
            in CreatedEvaluator candidate,
            string effectId,
            ITextStudioEffectDefinition definition,
            in TextStudioTagParameters parameters,
            AnimationPhase phase)
        {
            return candidate.evaluator != null &&
                   ReferenceEquals(candidate.definition, definition) &&
                   candidate.phase == phase &&
                   string.Equals(candidate.effectId, effectId, StringComparison.Ordinal) &&
                   HaveSameValues(in candidate.parameters, in parameters);
        }

        static bool HaveSameValues(in TextStudioTagParameters left, in TextStudioTagParameters right)
        {
            int positionalCount = left.PositionalCount;
            int namedCount = left.NamedCount;
            if (positionalCount != right.PositionalCount ||
                namedCount != right.NamedCount ||
                !string.Equals(left.Raw, right.Raw, StringComparison.Ordinal))
            {
                return false;
            }

            if (positionalCount > 0)
            {
                IReadOnlyList<string> leftPositional = left.Positional;
                IReadOnlyList<string> rightPositional = right.Positional;
                for (int i = 0; i < positionalCount; i++)
                {
                    if (!string.Equals(leftPositional[i], rightPositional[i], StringComparison.Ordinal))
                        return false;
                }
            }

            if (namedCount > 0)
            {
                IReadOnlyList<TextStudioNamedTagParameter> leftNamed = left.Named;
                IReadOnlyList<TextStudioNamedTagParameter> rightNamed = right.Named;
                for (int i = 0; i < namedCount; i++)
                {
                    TextStudioNamedTagParameter a = leftNamed[i];
                    TextStudioNamedTagParameter b = rightNamed[i];
                    if (a.IsFlag != b.IsFlag ||
                        a.Operation != b.Operation ||
                        !string.Equals(a.Name, b.Name, StringComparison.Ordinal) ||
                        !string.Equals(a.Value, b.Value, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
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
            _lookupStale = true;
            _boundEffectsExtendable = false;
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
            _allBoundEffectsKeepAppendedLookup = true;
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
            effect.transitionFastPath =
                effect.effect is CodeEffectBase transitionEffect &&
                transitionEffect.SupportsTransitionFastPath &&
                effect.scope == null &&
                effect.envelope == null &&
                effect.intensity >= 1f &&
                effect.phaseBlendGroupId == 0;
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
            // Word, line and paragraph units and word or line offsets read segmentation, which the segmentation after an append
            // can give other values for earlier glyphs: a word joined across the boundary, or a line break's unassigned line slot.
            _allBoundEffectsKeepAppendedLookup &=
                effect.area.Mode != TextStudioEffectAreaMode.Word &&
                effect.area.Mode != TextStudioEffectAreaMode.Line &&
                effect.area.Mode != TextStudioEffectAreaMode.Paragraph &&
                effect.phaseParams.wordOffset == 0f &&
                effect.phaseParams.lineOffset == 0f;
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
            _lookupStale = true;
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
            bool fullMotion = TextStudioMotionSettings.Resolve(_motionPolicy) == TextStudioMotionPolicy.Full;

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
                        if (reveal != null)
                            reveal.GetGlyphPhaseAndProgress(i, out phase, out phaseProgress);
                        else
                        {
                            phase = AnimationPhase.Persistent;
                            phaseProgress = 1f;
                        }
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
                            spatialContext = WithSpatialContext(in context, i);
                        }
                        // A binding of another phase, such as a hide effect while revealing, skips the call that would fail.
                        else if (!AppliesInGlyphPhase(in bound, phase))
                        {
                            hasReusableContext = false;
                            continue;
                        }
                        // EvaluateScopedEffect reaches Evaluate unchanged at full influence and full motion, so a built-in
                        // transition effect evaluates its Reveal or Hide frame without the glyph's spatial context.
                        else if (bound.transitionFastPath &&
                                 fullMotion &&
                                 TryEvaluateTransitionFast(
                                     in bound,
                                     effectDelay,
                                     reveal,
                                     phase,
                                     phaseProgress,
                                     in unit,
                                     ref _states[i]))
                        {
                            anyApplied = true;
                            hasReusableContext = false;
                            continue;
                        }
                        // TryCreateEffectContext already returns the spatial context; deriving it again recomputed the same fields.
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
                                     out spatialContext))
                        {
                            hasReusableContext = false;
                            continue;
                        }

                        // Later effects that reuse this base context resolve to the same spatial context.
                        hasReusableContext = true;
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

                // The context already carries this glyph's spatial source.
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

        static EvaluatedGlyphState[] GetIdentityStates(int capacity)
        {
            if (capacity <= 0)
                return Array.Empty<EvaluatedGlyphState>();

            if (s_identityStates.TryGetValue(capacity, out EvaluatedGlyphState[] states))
                return states;

            states = new EvaluatedGlyphState[capacity];
            for (int i = 0; i < states.Length; i++)
                states[i] = EvaluatedGlyphState.Identity;
            s_identityStates.Add(capacity, states);
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
            TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(_motionPolicy);
            if (policy == TextStudioMotionPolicy.Full)
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
                renderPropertyInfluence,
                policy);
            TextStudioRenderPropertyWriter renderProperties =
                new TextStudioRenderPropertyWriter(
                    renderPropertySink,
                    propertyGlyphIndex,
                    1,
                    resolvedInfluence);
            float motionRate = TextStudioVisualPrimitiveMath.ResolveMotionRate(
                policy,
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

        static float ResolveRenderPropertyInfluence(
            in TextStudioEffectCapabilities capabilities,
            float influence,
            TextStudioMotionPolicy policy)
        {
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

        // Whether TryCreateEffectContext evaluates the binding in this glyph phase: its effect family is enabled, a forced
        // phase matches and its phase mask includes the effective phase.
        bool AppliesInGlyphPhase(in BoundEffect bound, AnimationPhase glyphPhase)
        {
            AnimationPhase effectPhase = bound.forcePhase == AnimationPhase.None ? glyphPhase : bound.forcePhase;
            return IsEffectFamilyEnabled(effectPhase) &&
                   (bound.forcePhase == AnimationPhase.None || glyphPhase == bound.forcePhase) &&
                   IncludesPhase(bound.phaseMask, effectPhase);
        }

        // The effect phase, progress and clocks a binding that applies in this glyph phase evaluates with.
        void ResolveEffectTiming(
            in BoundEffect bound,
            float effectDelay,
            RevealController reveal,
            AnimationPhase glyphPhase,
            float glyphPhaseProgress,
            out AnimationPhase effectPhase,
            out float effectProgress,
            out float effectTime,
            out float effectPlaybackTime,
            out bool usesContinuousPhaseClock)
        {
            effectPhase = bound.forcePhase == AnimationPhase.None ? glyphPhase : bound.forcePhase;

            effectProgress = glyphPhaseProgress;
            if (bound.forcePhase != AnimationPhase.None)
                effectProgress = GetStaggeredPhaseProgress(
                    reveal,
                    effectDelay,
                    bound.phaseSpeed,
                    effectPhase,
                    glyphPhaseProgress);

            effectTime = _time;
            effectPlaybackTime = _playbackTime;
            usesContinuousPhaseClock =
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
        }

        // Evaluates a transition fast-path binding in a Reveal or Hide frame on a discrete clock, as the general path would
        // through Evaluate, and reports false for every other frame.
        bool TryEvaluateTransitionFast(
            in BoundEffect bound,
            float effectDelay,
            RevealController reveal,
            AnimationPhase glyphPhase,
            float glyphPhaseProgress,
            in TextStudioEffectUnitContext unit,
            ref EvaluatedGlyphState state)
        {
            ResolveEffectTiming(
                in bound,
                effectDelay,
                reveal,
                glyphPhase,
                glyphPhaseProgress,
                out AnimationPhase effectPhase,
                out float effectProgress,
                out float effectTime,
                out float effectPlaybackTime,
                out bool usesContinuousPhaseClock);
            if (usesContinuousPhaseClock ||
                (effectPhase != AnimationPhase.Appearance && effectPhase != AnimationPhase.Disappearance))
            {
                return false;
            }

            ((CodeEffectBase)bound.effect).EvaluateTransitionFast(
                ref state,
                effectPhase,
                effectProgress,
                effectTime,
                effectPlaybackTime,
                unit.unitIndex,
                unit.normalizedUnitIndex);
            state.renderChannels |= bound.renderChannels;
            return true;
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
            if (!AppliesInGlyphPhase(in bound, glyphPhase))
            {
                context = default;
                return false;
            }

            ResolveEffectTiming(
                in bound,
                effectDelay,
                reveal,
                glyphPhase,
                glyphPhaseProgress,
                out AnimationPhase effectPhase,
                out float effectProgress,
                out float effectTime,
                out float effectPlaybackTime,
                out bool usesContinuousPhaseClock);
            bool patchesContinuousPhaseClock = usesContinuousPhaseClock && effectPhase != AnimationPhase.Persistent;
            if (patchesContinuousPhaseClock || _hasInfluenceTarget)
            {
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
                if (patchesContinuousPhaseClock)
                    context = context.WithContinuousPhaseClock();
                context = WithSpatialContext(in context, glyphIndex);
                return true;
            }

            // One construction writes what the base constructor and WithSpatialContext derive in three: the base fields,
            // then the glyph's spatial source over the base context's defaults.
            float normalizedIndex = unit.normalizedUnitIndex;
            ResolveSpatialSource(
                glyphIndex,
                normalizedIndex,
                unit.unitStartGlyph,
                unit.unitGlyphCount,
                new Vector3(normalizedIndex, 0f, 0f),
                out Vector3 source,
                out Vector3 spatial,
                out float effectUnitScale,
                out Vector3 normalized,
                out Color sourceColor,
                out bool hasSourceColor,
                out TextStudioGlyphVisualBounds sourceAreaBounds,
                out bool hasSourceAreaBounds);
            context = EffectEvaluationContext.CreateSpatial(
                glyphIndex,
                _stateCount,
                wordIndex,
                lineIndex,
                effectPhase,
                effectProgress,
                effectTime,
                effectPlaybackTime,
                deltaTime,
                normalizedIndex,
                in unit,
                source,
                normalized,
                sourceColor,
                hasSourceColor,
                spatial,
                _localToWorld.MultiplyPoint3x4(spatial),
                hasSourceAreaBounds ? sourceAreaBounds : default,
                hasSourceAreaBounds,
                effectUnitScale);
            return true;
        }

        EffectEvaluationContext WithSpatialContext(
            in EffectEvaluationContext context,
            int glyphIndex)
        {
            ResolveSpatialSource(
                glyphIndex,
                context.normalizedIndex,
                context.unitStartGlyph,
                context.unitGlyphCount,
                context.sourcePosition,
                out Vector3 source,
                out Vector3 spatial,
                out float effectUnitScale,
                out Vector3 normalized,
                out Color sourceColor,
                out bool hasSourceColor,
                out TextStudioGlyphVisualBounds sourceAreaBounds,
                out bool hasSourceAreaBounds);
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

        // The spatial source WithSpatialContext writes over a base context with this normalized index, unit range and
        // source position.
        void ResolveSpatialSource(
            int glyphIndex,
            float normalizedIndex,
            int unitStartGlyph,
            int unitGlyphCount,
            Vector3 baseSourcePosition,
            out Vector3 source,
            out Vector3 spatial,
            out float effectUnitScale,
            out Vector3 normalized,
            out Color sourceColor,
            out bool hasSourceColor,
            out TextStudioGlyphVisualBounds sourceAreaBounds,
            out bool hasSourceAreaBounds)
        {
            source = _sourcePositions != null &&
                glyphIndex >= 0 && glyphIndex < _spatialCount && glyphIndex < _sourcePositions.Length
                ? _sourcePositions[glyphIndex]
                : new Vector3(normalizedIndex, 0f, 0f);
            spatial = _usePlacements &&
                glyphIndex >= 0 && glyphIndex < _spatialCount &&
                glyphIndex < _placementCount && glyphIndex < _placements.Length
                ? _placements[glyphIndex].position
                : source;
            effectUnitScale = _effectUnitScales != null &&
                              glyphIndex >= 0 &&
                              glyphIndex < _effectUnitScales.Length
                ? _effectUnitScales[glyphIndex]
                : 1f;
            normalized = _sourceTextNormalizedPositions != null &&
                glyphIndex >= 0 && glyphIndex < _spatialCount &&
                glyphIndex < _sourceTextNormalizedPositions.Length
                ? _sourceTextNormalizedPositions[glyphIndex]
                : new Vector3(normalizedIndex, 0f, 0f);
            hasSourceColor = _sourceColorValidity != null &&
                             glyphIndex >= 0 &&
                             glyphIndex < _spatialCount &&
                             glyphIndex < _sourceColorValidity.Length &&
                             _sourceColorValidity[glyphIndex] &&
                             _sourceColors != null &&
                             glyphIndex < _sourceColors.Length;
            sourceColor = hasSourceColor
                ? (Color)_sourceColors[glyphIndex].SampleCenter()
                : Color.white;
            hasSourceAreaBounds = TryResolveSourceAreaBounds(
                unitStartGlyph,
                unitGlyphCount,
                baseSourcePosition,
                out sourceAreaBounds);
        }

        bool TryResolveSourceAreaBounds(
            int unitStartGlyph,
            int unitGlyphCount,
            Vector3 fallbackSourcePosition,
            out TextStudioGlyphVisualBounds bounds)
        {
            int start = Mathf.Clamp(unitStartGlyph, 0, _spatialCount);
            int end = Mathf.Clamp(
                unitStartGlyph + unitGlyphCount,
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
                Vector2 point = new Vector2(fallbackSourcePosition.x, fallbackSourcePosition.y);
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

            int totalBindings = ResolveGlyphEffectUnits(plan, 0, 0, glyphCount);

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

            WriteGlyphEffectEntries(plan, totalBindings);
            ResolveReusedEffectContexts(0, glyphCount);
            FinishGlyphEffectLookup(glyphCount);
        }

        // Builds lookup entries for the glyphs from keptGlyphCount on, which only the bound effects from firstEffect on cover,
        // and keeps the entries before, which the lookup built for the same earlier effects resolved (TryBindAppendedEffects).
        void BuildAppendedGlyphEffectLookup(TextStudioCompiledPlan plan, int keptGlyphCount, int firstEffect)
        {
            int glyphCount = _stateCount;
            int keptEntries = keptGlyphCount > 0
                ? _glyphEffectStart[keptGlyphCount - 1] + _glyphEffectCount[keptGlyphCount - 1]
                : 0;
            _glyphEffectStart = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectStart, glyphCount, keptGlyphCount);
            _glyphEffectCount = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectCount, glyphCount, keptGlyphCount);
            _glyphEffectWrite = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectWrite, glyphCount, keptGlyphCount);
            if (glyphCount > keptGlyphCount)
            {
                Array.Clear(_glyphEffectStart, keptGlyphCount, glyphCount - keptGlyphCount);
                Array.Clear(_glyphEffectCount, keptGlyphCount, glyphCount - keptGlyphCount);
                Array.Clear(_glyphEffectWrite, keptGlyphCount, glyphCount - keptGlyphCount);
            }

            int resolvedCount = ResolveGlyphEffectUnits(plan, firstEffect, keptGlyphCount, glyphCount);
            int totalBindings = keptEntries + resolvedCount;
            _glyphEffectIndices = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectIndices, totalBindings, keptEntries);
            _glyphEffectDelays = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectDelays, totalBindings, keptEntries);
            _glyphEffectUnits = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectUnits, totalBindings, keptEntries);
            _glyphEffectReusesPreviousContext = TextStudioBufferCapacity.EnsureKeeping(_glyphEffectReusesPreviousContext, totalBindings, keptEntries);
            if (resolvedCount > 0)
                Array.Clear(_glyphEffectReusesPreviousContext, keptEntries, resolvedCount);

            int cursor = keptEntries;
            for (int i = keptGlyphCount; i < glyphCount; i++)
            {
                _glyphEffectStart[i] = cursor;
                cursor += _glyphEffectCount[i];
            }

            WriteGlyphEffectEntries(plan, resolvedCount);
            ResolveReusedEffectContexts(keptGlyphCount, glyphCount);
            FinishGlyphEffectLookup(glyphCount);
        }

        // The counting pass over the bound effects from firstEffect on, for glyphs from firstGlyph on. It keeps each resolved
        // unit, so the fill pass writes the entries without resolving them again.
        int ResolveGlyphEffectUnits(TextStudioCompiledPlan plan, int firstEffect, int firstGlyph, int glyphCount)
        {
            int totalBindings = 0;
            for (int e = firstEffect; e < _boundEffectCount; e++)
            {
                ref BoundEffect bound = ref _boundEffects[e];
                if (bound.rendererDeferred)
                    continue;
                int start = Mathf.Clamp(bound.glyphStart, firstGlyph, glyphCount);
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
                            out TextStudioEffectUnitContext resolvedUnit))
                    {
                        if (_resolvedLookupUnits.Length <= totalBindings)
                        {
                            int resolvedCapacity = TextStudioBufferCapacity.ForCount(totalBindings + 1);
                            Array.Resize(ref _resolvedLookupBindings, resolvedCapacity);
                            Array.Resize(ref _resolvedLookupGlyphs, resolvedCapacity);
                            Array.Resize(ref _resolvedLookupUnits, resolvedCapacity);
                        }
                        _resolvedLookupBindings[totalBindings] = e;
                        _resolvedLookupGlyphs[totalBindings] = i;
                        _resolvedLookupUnits[totalBindings] = resolvedUnit;
                        _glyphEffectCount[i]++;
                        totalBindings++;
                    }
                }
            }

            return totalBindings;
        }

        void WriteGlyphEffectEntries(TextStudioCompiledPlan plan, int resolvedCount)
        {
            for (int resolved = 0; resolved < resolvedCount; resolved++)
            {
                int e = _resolvedLookupBindings[resolved];
                int i = _resolvedLookupGlyphs[resolved];
                ref BoundEffect bound = ref _boundEffects[e];
                ref readonly TextStudioEffectUnitContext unit = ref _resolvedLookupUnits[resolved];
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

        void ResolveReusedEffectContexts(int startGlyph, int glyphCount)
        {
            for (int i = startGlyph; i < glyphCount; i++)
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
        }

        void FinishGlyphEffectLookup(int glyphCount)
        {
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
            _lookupGeneration = EffectBindingGeneration;
            _lookupGlyphCount = glyphCount;
            _lookupStale = false;
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
