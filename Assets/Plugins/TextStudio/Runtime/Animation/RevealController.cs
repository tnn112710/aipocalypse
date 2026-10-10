using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Controls the reveal (typewriter) progression through text.
    /// Supports glyph/word/line/region reveal modes with marker dispatch.
    /// </summary>
    internal sealed class RevealController : ITextStudioRevealState
    {
        RevealMode _mode = RevealMode.ByGlyph;
        float _revealProgress;
        internal TextStudioTypewriterTimingMode TimingMode { get; set; }
        float[] _hideStartCoverage;
        TextStudioVisualGlyph[] _layoutGlyphs;
        int _layoutGlyphCount;
        Vector3[] _sequencePositions;
        bool _sequenceYDown;
        internal bool HasSpatialSequence => _revealSequenceSettings.Direction != TextStudioSequenceDirection.Logical || _hideSequenceSettings.Direction != TextStudioSequenceDirection.Logical;
        readonly UnitProjectionComparer _projectionComparer = new UnitProjectionComparer();
        sealed class UnitProjectionComparer : IComparer<int>
        {
            internal float[] Projections;
            public int Compare(int a, int b)
            {
                int comparison = Projections[a].CompareTo(Projections[b]);
                return comparison != 0 ? comparison : a.CompareTo(b);
            }
        }
        TextStudioCompiledPlan _plan;
        TypingTimingsBase _groupTimings;
        int[] _spatialUnits = Array.Empty<int>();
        float[] _unitProjections = Array.Empty<float>();
        int[] _sequenceScratch = Array.Empty<int>();
        int _scheduleUnitCount;
        int _hideUnitCount;
        int _revealedCount;
        int _totalCount;
        float _speedMultiplier = 1f;
        float _loopDelay = 0.75f;
        float _loopWaitRemaining;
        bool _isRevealing;
        bool _isComplete;
        bool _loop;
        bool _changedThisUpdate;
        int _pauseDepth;
        float _accumulatedTime;
        float _completionTime;
        float _scheduledCompletionTime;
        float _appearanceTime;
        bool _waitAfterFinalReveal = true;
        bool _revealActiveBlendEnabled = true;
        bool _appearancesSettled;
        float _settledActiveStartTime;
        bool _isDisappearing;
        float _disappearanceDuration = 0.3f;
        float _disappearanceElapsed;
        float _disappearProgress;
        TextStudioSequenceSettings _revealSequenceSettings = TextStudioSequenceSettings.Default;
        TextStudioSequenceSettings _hideSequenceSettings = TextStudioSequenceSettings.Default;

        // Per-glyph reveal times (precomputed)
        float[] _revealTimes;
        float[] _postRevealDelays;
        float[] _appearanceStartTimes;
        bool[] _instantFlags;
        bool[] _revealedFlags;
        bool[] _appearanceCompleteNotified;
        uint _appearanceNotificationVersion;
        int[] _hideGlyphSequence;
        int[] _revealSequence;
        int[] _hideOrderByGlyph;
        bool[] _disappearAffectedFlags;
        int[] _selectedHideOrderByGlyph;
        bool[] _disappearStartedFlags;
        int _disappearAffectedCount;
        bool _hasSelectiveDisappear;
        float[] _orderedRevealTimes;
        int[] _unitStarts;
        int[] _unitEnds;
        int[] _unitSequence;
        int[] _wordRevealGroups;
        float[] _unitTimes;
        float[] _unitDelays;
        bool[] _unitInstantFlags;
        // Leading glyph slots whose schedule arrays hold the Instant layout for _instantScheduleDelay: zero reveal times, the delay, no instant flags, identity reveal and hide order and hide order index 0. Every other schedule writer clears it.
        int _instantScheduleCount;
        float _instantScheduleDelay;

        // Marker tracking
        int _lastDispatchedMarkerIndex;
        int _lastDispatchedExtensionTagIndex;
        bool[] _dispatchedMarkerFlags;
        bool[] _dispatchedExtensionTagFlags;
        int _markerStateCount;
        int _extensionTagStateCount;

        // Configurable appearance phase duration
        float _appearanceDuration = 0.3f;
        const float AppearanceCompletionTolerance = 0.0001f;

        // Callbacks
        public event Action<int> OnGlyphRevealed;
        public event Action<int> OnGlyphAppearanceCompleted;
        public event Action<int> OnGlyphDisappearStarted;
        public event Action<int> OnGlyphDisappeared;
        public event Action<string, string[]> OnMarkerReached;
        public event Action<ActionMarker> OnActionMarkerReached;
        public event Action<TextStudioExtensionTag> OnExtensionTagReached;
        public event Action OnRevealComplete;
        public event Action OnDisappearComplete;

        internal readonly struct TextChangeState
        {
            internal readonly bool IsValid;
            internal readonly bool[] RevealedGlyphs;
            internal readonly float[] AppearanceElapsed;
            internal readonly float AppearanceTime;
            internal readonly bool IsRevealing;
            internal readonly ActionMarker[] DispatchedMarkers;
            internal readonly TextStudioExtensionTag[] DispatchedExtensionTags;

            internal TextChangeState(
                bool[] revealedGlyphs,
                float[] appearanceElapsed,
                float appearanceTime,
                bool isRevealing,
                ActionMarker[] dispatchedMarkers,
                TextStudioExtensionTag[] dispatchedExtensionTags)
            {
                IsValid = true;
                RevealedGlyphs = revealedGlyphs;
                AppearanceElapsed = appearanceElapsed;
                AppearanceTime = appearanceTime;
                IsRevealing = isRevealing;
                DispatchedMarkers = dispatchedMarkers;
                DispatchedExtensionTags = dispatchedExtensionTags;
            }
        }

        public RevealMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public float RevealProgress => _revealProgress;
        public int RevealedCount => _revealedCount;
        public int TotalCount => _totalCount;
        public bool IsRevealing => _isRevealing;
        public bool IsComplete => _isComplete;
        public bool IsPaused => _pauseDepth > 0;
        public bool IsLoopWaiting => _loop && _isComplete && _totalCount > 0;
        public bool HasActiveReveal => _isRevealing || IsLoopWaiting || HasActiveAppearance || _isDisappearing;
        internal bool AreAllGlyphsPersistent =>
            _isComplete &&
            !_isDisappearing &&
            !HasActiveAppearance &&
            _revealedCount >= _totalCount;
        public bool ChangedThisUpdate => _changedThisUpdate;
        public bool IsDisappearing => _isDisappearing;
        public float DisappearProgress => _disappearProgress;
        internal bool RevealActiveBlendEnabled => _revealActiveBlendEnabled;
        internal bool NotifyGlyphDisappearStarted { get; set; }

        internal void SetAppearanceCompletionNotifications(bool enabled)
        {
            unchecked { _appearanceNotificationVersion++; }
            if (!enabled)
            {
                _appearanceCompleteNotified = null;
                return;
            }
            EnsureAppearanceCompletionCapacity();
            SyncAppearanceCompletionNotifications();
        }

        void EnsureAppearanceCompletionCapacity()
        {
            if (_appearanceCompleteNotified == null || _appearanceCompleteNotified.Length < _totalCount)
                _appearanceCompleteNotified = new bool[TextStudioBufferCapacity.ForCount(_totalCount)];
        }

        void SyncAppearanceCompletionNotifications()
        {
            if (_appearanceCompleteNotified == null) return;
            EnsureAppearanceCompletionCapacity();
            for (int i = 0; i < _totalCount; i++)
                _appearanceCompleteNotified[i] = _revealedFlags != null && _revealedFlags[i] &&
                                                 GetGlyphPhase(i, 0f) == AnimationPhase.Persistent;
        }

        void DispatchAppearanceCompletions()
        {
            if (OnGlyphAppearanceCompleted == null || _appearanceCompleteNotified == null ||
                _isDisappearing || _revealedFlags == null)
                return;
            EnsureAppearanceCompletionCapacity();
            uint version = _appearanceNotificationVersion;
            int count = _totalCount;
            bool[] notified = _appearanceCompleteNotified;
            Action<int> callback = OnGlyphAppearanceCompleted;
            for (int i = 0; i < count; i++)
            {
                if (!_revealedFlags[i] || notified[i] ||
                    GetGlyphPhase(i, 0f) != AnimationPhase.Persistent)
                    continue;
                notified[i] = true;
                callback(i);
                if (_isDisappearing || version != _appearanceNotificationVersion ||
                    count != _totalCount || notified != _appearanceCompleteNotified ||
                    callback != OnGlyphAppearanceCompleted)
                    return;
            }
        }
        public TextStudioSequenceSettings RevealSequence
        {
            get => _revealSequenceSettings;
            set => _revealSequenceSettings = NormalizeSequence(value);
        }

        public TextStudioSequenceSettings HideSequence
        {
            get => _hideSequenceSettings;
            set
            {
                TextStudioSequenceSettings normalized = NormalizeSequence(value);
                // Every schedule rebuild also rebuilds the hide order, so unchanged settings already have a current order. Reveal initialization assigns the setting before every text, which rebuilt the same order twice.
                if (normalized.Equals(_hideSequenceSettings) && _hideGlyphSequence != null)
                    return;
                _hideSequenceSettings = normalized;
                BuildHideOrder();
            }
        }

        public float AppearanceDuration
        {
            get => _appearanceDuration;
            set => _appearanceDuration = Mathf.Max(0.01f, value);
        }

        public float DisappearanceDuration
        {
            get => _disappearanceDuration;
            set => _disappearanceDuration = Mathf.Max(0.01f, value);
        }

        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        public float LoopDelay
        {
            get => _loopDelay;
            set => _loopDelay = Mathf.Max(0f, value);
        }

        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// Initialize reveal state for a new text.
        /// </summary>
        /// <param name="plan">The compiled plan providing text, word/line ranges, and visible count.</param>
        /// <param name="timings">Optional custom timings asset. Takes precedence over <paramref name="builtInTimings"/>.</param>
        /// <param name="builtInTimings">Optional built-in preset evaluator. Used only when <paramref name="timings"/> is null.</param>
        public void Initialize(
            TextStudioCompiledPlan plan,
            TypingTimingsBase timings = null,
            TextStudioTimingEvaluator builtInTimings = null,
            bool waitAfterFinalBuiltInReveal = true)
        {
            _plan = plan;
            _groupTimings = timings;
            _totalCount = plan.VisibleGlyphCount;
            _revealedCount = 0;
            _revealProgress = 0f;
            _accumulatedTime = 0f;
            _completionTime = 0f;
            _appearanceTime = 0f;
            _waitAfterFinalReveal = timings != null
                ? timings.IncludesFinalRevealDelay
                : waitAfterFinalBuiltInReveal;
            _revealActiveBlendEnabled = true;
            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
            CancelDisappear();
            _loopWaitRemaining = 0f;
            _changedThisUpdate = false;
            _isRevealing = false;
            _isComplete = false;
            _pauseDepth = 0;
            ResetDispatchState(plan);

            // Pre-compute reveal times
            if (_revealTimes == null || _revealTimes.Length < _totalCount)
            {
                _revealTimes = new float[TextStudioBufferCapacity.ForCount(_totalCount)];
                _postRevealDelays = new float[_revealTimes.Length];
                _appearanceStartTimes = new float[_revealTimes.Length];
                _instantFlags = new bool[_revealTimes.Length];
                _revealedFlags = new bool[_revealTimes.Length];
                _revealSequence = new int[_revealTimes.Length];
                _orderedRevealTimes = new float[_revealTimes.Length];
                _unitStarts = new int[_revealTimes.Length];
                _unitEnds = new int[_revealTimes.Length];
                _unitSequence = new int[_revealTimes.Length];
                _unitTimes = new float[_revealTimes.Length];
                _unitDelays = new float[_revealTimes.Length];
                _unitInstantFlags = new bool[_revealTimes.Length];
                _instantScheduleCount = 0;
            }
            else
            {
                if (_postRevealDelays == null || _postRevealDelays.Length < _revealTimes.Length)
                {
                    _postRevealDelays = new float[_revealTimes.Length];
                    _instantScheduleCount = 0;
                }
                if (_appearanceStartTimes == null || _appearanceStartTimes.Length < _revealTimes.Length)
                    _appearanceStartTimes = new float[_revealTimes.Length];
                if (_instantFlags == null || _instantFlags.Length < _revealTimes.Length)
                {
                    _instantFlags = new bool[_revealTimes.Length];
                    _instantScheduleCount = 0;
                }
            }
            EnsureSequenceCapacity(_revealTimes.Length);
            ClearRevealFlags();

            float baseDelay = TextStudioDefaultSettings.Instance.defaultCharDelay;
            if (CanUseInstantSchedule(plan, timings, builtInTimings))
            {
                ApplyInstantSchedule(Mathf.Max(0f, GetDefaultDelay(baseDelay)));
                return;
            }

            _instantScheduleCount = 0;
            int lastVisibleUnitIndex = GetLastVisibleUnitIndex(plan);

            for (int i = 0; i < _totalCount; i++)
            {
                bool isInstant = IsInstantGlyph(plan, i);
                TextStudioTimingContext context = new TextStudioTimingContext(
                    plan,
                    i,
                    isInstant,
                    lastVisibleUnitIndex);
                float delay;
                if (timings != null)
                {
                    delay = timings.GetRevealDelayAfter(in context);
                }
                else if (builtInTimings != null)
                {
                    delay = builtInTimings(in context);
                }
                else
                {
                    delay = GetDefaultDelay(baseDelay);
                }

                _revealTimes[i] = 0f;
                _postRevealDelays[i] = Mathf.Max(0f, delay);
                _instantFlags[i] = isInstant;
            }

            BuildRevealSchedule(plan);
            BuildHideOrder();
        }

        internal TextChangeState CaptureTextChangeState(
            TextStudioCompiledPlan plan,
            TextStudioGlyphChangeSet glyphChanges)
        {
            if (!glyphChanges.IsDefined)
                return default;

            int incomingCount = glyphChanges.IncomingGlyphCount;
            bool[] revealedGlyphs = incomingCount > 0
                ? new bool[incomingCount]
                : Array.Empty<bool>();
            float[] appearanceElapsed = incomingCount > 0
                ? new float[incomingCount]
                : Array.Empty<float>();
            for (int incomingIndex = 0; incomingIndex < incomingCount; incomingIndex++)
            {
                int outgoingIndex = glyphChanges.GetOutgoingGlyphForIncoming(incomingIndex);
                if (outgoingIndex < 0 || !IsGlyphRevealed(outgoingIndex))
                    continue;

                revealedGlyphs[incomingIndex] = true;
                appearanceElapsed[incomingIndex] = Mathf.Max(
                    0f,
                    GetAppearanceElapsed(outgoingIndex));
            }

            return new TextChangeState(
                revealedGlyphs,
                appearanceElapsed,
                glyphChanges.HasRetainedGlyphs ? _appearanceTime : 0f,
                _isRevealing,
                CaptureDispatchedMarkers(plan),
                CaptureDispatchedExtensionTags(plan));
        }

        /// <summary>
        /// Start revealing text.
        /// </summary>
        /// <param name="resetProgress">
        /// When true (default), clears already-revealed flags, counters, and marker
        /// dispatch state so a replay starts from glyph 0. Set to false to resume
        /// from the current revealed count (used by seamless AppendText).
        /// </param>
        public void StartReveal(bool resetProgress = true)
        {
            if (_totalCount == 0 && (_plan == null ||
                (_plan.ActionMarkerCount == 0 && _plan.ExtensionTagCount == 0)))
            {
                _isComplete = true;
                _revealProgress = 1f;
                OnRevealComplete?.Invoke();
                return;
            }

            if (resetProgress)
            {
                BuildRevealSchedule(_plan);
                BuildHideOrder();
                _completionTime = _scheduledCompletionTime;
                _revealedCount = 0;
                _revealProgress = 0f;
                ClearDispatchState();
                ClearRevealFlags();
            }

            CancelDisappear();
            _isRevealing = true;
            _isComplete = false;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _accumulatedTime = resetProgress ? 0f : GetScrubAccumulatedTime(_revealedCount);
            _appearanceTime = _accumulatedTime;
            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
        }

        internal void RestoreTextChangeState(
            in TextChangeState state,
            TextStudioCompiledPlan plan,
            TextStudioGlyphChangeSet glyphChanges)
        {
            if (!state.IsValid ||
                state.RevealedGlyphs == null ||
                state.RevealedGlyphs.Length != _totalCount)
            {
                return;
            }

            _instantScheduleCount = 0;
            int revealedCount = 0;
            for (int order = 0; order < _totalCount; order++)
            {
                int glyphIndex = GetRevealGlyphAt(order);
                if (state.RevealedGlyphs[glyphIndex])
                    _unitSequence[revealedCount++] = glyphIndex;
            }

            float firstUnrevealedTime = -1f;
            int nextOrder = revealedCount;
            for (int order = 0; order < _totalCount; order++)
            {
                int glyphIndex = GetRevealGlyphAt(order);
                if (state.RevealedGlyphs[glyphIndex])
                    continue;

                _unitSequence[nextOrder++] = glyphIndex;
                if (firstUnrevealedTime < 0f)
                    firstUnrevealedTime = _orderedRevealTimes[order];
            }

            float originalCompletionTime = _completionTime;
            for (int order = 0; order < _totalCount; order++)
            {
                int glyphIndex = _unitSequence[order];
                _revealSequence[order] = glyphIndex;
                float scheduledTime = order < revealedCount
                    ? 0f
                    : Mathf.Max(
                        0f,
                        _revealTimes[glyphIndex] - Mathf.Max(0f, firstUnrevealedTime));
                _orderedRevealTimes[order] = scheduledTime;
                _revealTimes[glyphIndex] = scheduledTime;
            }

            _completionTime = firstUnrevealedTime >= 0f
                ? Mathf.Max(0f, originalCompletionTime - firstUnrevealedTime)
                : 0f;
            _appearanceTime = state.AppearanceTime;
            _accumulatedTime = 0f;
            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
            ClearRevealFlags();
            for (int glyphIndex = 0; glyphIndex < _totalCount; glyphIndex++)
            {
                if (!state.RevealedGlyphs[glyphIndex])
                    continue;

                float elapsed = state.AppearanceElapsed != null &&
                                glyphIndex < state.AppearanceElapsed.Length
                    ? Mathf.Max(0f, state.AppearanceElapsed[glyphIndex])
                    : _appearanceDuration;
                MarkGlyphRevealed(glyphIndex, _appearanceTime - elapsed);
            }

            _revealedCount = revealedCount;
            _revealProgress = _totalCount > 0
                ? (float)revealedCount / _totalCount
                : 1f;
            CancelDisappear();
            _isComplete = revealedCount >= _totalCount;
            _isRevealing = state.IsRevealing && !_isComplete;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _changedThisUpdate = true;
            RestoreDispatchState(in state, plan, glyphChanges);
            SyncAppearanceCompletionNotifications();
        }

        internal void StartPreparedTextChangeReveal()
        {
            if (_totalCount == 0)
            {
                _isComplete = true;
                _revealProgress = 1f;
                OnRevealComplete?.Invoke();
                return;
            }

            CancelDisappear();
            _isRevealing = true;
            _isComplete = false;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _accumulatedTime = 0f;
        }

        internal void RestoreRevealedPrefix(int revealedCount, TextStudioCompiledPlan plan)
        {
            int count = Mathf.Clamp(revealedCount, 0, _totalCount);
            ClearRevealFlags();
            for (int i = 0; i < count; i++)
                MarkGlyphRevealed(i, GetScheduledRevealTime(i));
            _revealedCount = count;
            _revealProgress = _totalCount > 0 ? (float)count / _totalCount : 1f;
            _isComplete = count >= _totalCount;
            SyncAppearanceCompletionNotifications();

            ClearDispatchState();
            if (plan != null && plan.ActionMarkers != null)
            {
                for (int i = 0; i < plan.ActionMarkerCount; i++)
                {
                    if (plan.ActionMarkers[i].sourceIndex > count)
                        break;

                    SetMarkerDispatched(i);
                    _lastDispatchedMarkerIndex = i;
                }
            }

            if (plan != null && plan.ExtensionTags != null)
            {
                for (int i = 0; i < plan.ExtensionTagCount; i++)
                {
                    if (plan.ExtensionTags[i].GlyphStart > count)
                        break;

                    SetExtensionTagDispatched(i);
                    _lastDispatchedExtensionTagIndex = i;
                }
            }
        }

        /// <summary>
        /// Fully reset reveal state so a replay produces identical glyph-by-glyph output.
        /// Clears accumulated time, progress, per-glyph flags, and marker dispatch index.
        /// The precomputed reveal timing array is preserved (same text, no need to rebuild).
        /// </summary>
        public void Reset()
        {
            _completionTime = _scheduledCompletionTime;
            _revealProgress = 0f;
            _revealedCount = 0;
            _accumulatedTime = 0f;
            _appearanceTime = 0f;
            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
            CancelDisappear();
            _loopWaitRemaining = 0f;
            _changedThisUpdate = false;
            _isRevealing = false;
            _isComplete = false;
            _pauseDepth = 0;
            ClearDispatchState();

            ClearRevealFlags();
        }

        /// <summary>
        /// Reveal all remaining glyphs instantly.
        /// </summary>
        public void SkipToEnd(bool settleAppearances = true)
        {
            bool wasAlreadyComplete = _isComplete && !_isDisappearing && _revealedCount >= _totalCount;

            for (int order = _revealedCount; order < _totalCount; order++)
            {
                int glyphIndex = GetRevealGlyphAt(order);
                MarkGlyphRevealed(glyphIndex, settleAppearances ? 0f : _appearanceTime);
                OnGlyphRevealed?.Invoke(glyphIndex);
            }
            _revealedCount = _totalCount;
            _revealProgress = 1f;
            CancelDisappear();
            _isRevealing = false;
            _isComplete = true;
            _pauseDepth = 0;
            if (settleAppearances)
                CompleteActiveAppearances();
            else
            {
                _completionTime = _accumulatedTime + _appearanceDuration;
                _isComplete = false;
                _isRevealing = true;
                _revealProgress = ResolveTimelineProgress(_accumulatedTime);
            }
            DispatchAppearanceCompletions();
            _loopWaitRemaining = _loop ? _loopDelay : 0f;
            if (!wasAlreadyComplete && settleAppearances)
                OnRevealComplete?.Invoke();
        }

        /// <summary>
        /// Reveal everything immediately without firing per-glyph events (for instant mode).
        /// </summary>
        public void RevealAll()
        {
            if (_revealedFlags != null)
            {
                // MarkGlyphRevealed for every glyph without its start time: CompleteActiveAppearances settles every revealed glyph's start time below.
                bool[] notified = _appearanceCompleteNotified;
                int notifiedCount = notified != null ? Math.Min(_totalCount, notified.Length) : 0;
                for (int i = 0; i < _totalCount; i++)
                {
                    if (_revealedFlags[i])
                        continue;
                    _revealedFlags[i] = true;
                    if (i < notifiedCount)
                        notified[i] = false;
                }
            }
            _revealedCount = _totalCount;
            _revealProgress = 1f;
            CancelDisappear();
            _isRevealing = false;
            _isComplete = true;
            _pauseDepth = 0;
            CompleteActiveAppearances();
            DispatchAppearanceCompletions();
            _loopWaitRemaining = _loop ? _loopDelay : 0f;
        }

        public void StartDisappear()
        {
            StartDisappear(default(TextStudioGlyphChangeSet));
        }

        internal void StartDisappear(in TextStudioGlyphChangeSet affectedGlyphs)
        {
            if (_totalCount == 0 || _revealedCount == 0)
            {
                CompleteDisappear();
                return;
            }

            ConfigureDisappearSelection(affectedGlyphs);
            if (_hasSelectiveDisappear && _disappearAffectedCount == 0)
            {
                CancelDisappear();
                OnDisappearComplete?.Invoke();
                return;
            }

            if (_hideStartCoverage == null || _hideStartCoverage.Length < _totalCount)
                _hideStartCoverage = new float[TextStudioBufferCapacity.ForCount(_totalCount)];
            for (int i = 0; i < _totalCount; i++) _hideStartCoverage[i] = GetAppearanceProgress(i);
            _isDisappearing = true;
            _isRevealing = false;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _disappearanceElapsed = 0f;
            _disappearProgress = 0f;
            if (NotifyGlyphDisappearStarted)
            {
                if (_disappearStartedFlags == null || _disappearStartedFlags.Length < _totalCount)
                    _disappearStartedFlags = new bool[TextStudioBufferCapacity.ForCount(_totalCount)];
                System.Array.Clear(_disappearStartedFlags, 0, _totalCount);
                DispatchDisappearStarts();
            }
            _changedThisUpdate = true;
        }

        public void SkipDisappear()
        {
            if (!_isDisappearing && _revealedCount == 0)
                return;

            CompleteDisappear();
        }

        public void StopDisappear()
        {
            if (!_isDisappearing)
                return;

            CancelDisappear();
            CompleteActiveAppearances();
            _changedThisUpdate = true;
        }

        public void FlushSkippableExtensionTags(TextStudioCompiledPlan plan)
        {
            DispatchReadyExtensionTags(plan, flushingSkip: true);
        }

        public void FlushRemainingMarkers(TextStudioCompiledPlan plan)
        {
            if (plan == null || plan.ActionMarkers == null)
                return;

            for (int m = 0; m < plan.ActionMarkerCount; m++)
            {
                if (IsMarkerDispatched(m))
                    continue;
                ActionMarker marker = plan.ActionMarkers[m];
                OnMarkerReached?.Invoke(marker.actionId, marker.parameters);
                OnActionMarkerReached?.Invoke(marker);
                SetMarkerDispatched(m);
                _lastDispatchedMarkerIndex = m;
            }
        }

        /// <summary>
        /// Set reveal progress directly (0-1). Used by Timeline integration.
        /// Reveals/unreveals glyphs to match the target progress.
        /// </summary>
        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f) { Reset(); return; }
            SetScrubElapsedTime(GetApproximateDuration() * progress);
        }

        internal void SetScrubElapsedTime(float elapsedTime)
        {
            if (_revealedFlags == null || _totalCount == 0) return;

            unchecked { _appearanceNotificationVersion++; }

            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
            float scaledTime = Mathf.Max(0f, elapsedTime) * _speedMultiplier;
            int low = 0;
            int high = _totalCount;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (scaledTime >= _orderedRevealTimes[middle])
                    low = middle + 1;
                else
                    high = middle;
            }

            int targetCount = low;
            while (_revealedCount < targetCount)
            {
                _revealedFlags[GetRevealGlyphAt(_revealedCount)] = true;
                _revealedCount++;
            }

            while (_revealedCount > targetCount)
            {
                _revealedCount--;
                _revealedFlags[GetRevealGlyphAt(_revealedCount)] = false;
            }

            _isComplete = _revealedCount >= _totalCount &&
                          scaledTime + AppearanceCompletionTolerance >= _completionTime;
            _revealProgress = _isComplete ? 1f : ResolveTimelineProgress(scaledTime);
            _isRevealing = !_isComplete && (_revealedCount > 0 || scaledTime > 0f);
            CancelDisappear();
            _pauseDepth = 0;
            _accumulatedTime = scaledTime;
            _appearanceTime = scaledTime;
            SyncAppearanceStartTimesToRevealTimes(_revealedCount);
            SyncAppearanceCompletionNotifications();
            _loopWaitRemaining = _isComplete && _loop ? _loopDelay : 0f;
        }

        public void SetDisappearanceProgress(float progress)
        {
            SetDisappearanceProgress(progress, true);
        }

        internal void SetDisappearanceProgressForPreview(float progress)
        {
            SetDisappearanceProgress(progress, false);
        }

        void SetDisappearanceProgress(float progress, bool settleAppearances)
        {
            if (_revealedFlags == null || _totalCount == 0) return;
            progress = Mathf.Clamp01(progress);

            if (progress >= 1f)
            {
                ClearRevealFlags();
                _revealedCount = 0;
                _revealProgress = 0f;
                _isRevealing = false;
                _isComplete = false;
                _pauseDepth = 0;
                _loopWaitRemaining = 0f;
                _isDisappearing = false;
                _disappearanceElapsed = _disappearanceDuration;
                _disappearProgress = 1f;
                _changedThisUpdate = true;
                return;
            }

            for (int i = 0; i < _totalCount; i++)
                _revealedFlags[i] = true;

            _revealedCount = _totalCount;
            _revealProgress = 1f;
            _isRevealing = false;
            _isComplete = true;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            if (settleAppearances)
                CompleteActiveAppearances();
            if (_hideStartCoverage == null || _hideStartCoverage.Length < _totalCount) _hideStartCoverage = new float[TextStudioBufferCapacity.ForCount(_totalCount)];
            for (int i = 0; i < _totalCount; i++) _hideStartCoverage[i] = 1f;
            _isDisappearing = true;
            _disappearanceElapsed = progress * _disappearanceDuration;
            _disappearProgress = progress;
            _changedThisUpdate = true;
        }

        public void DriveProgressForward(float progress, TextStudioCompiledPlan plan = null)
        {
            if (_revealedFlags == null || _totalCount == 0) return;
            bool completeBefore = _isComplete;
            float targetTime = Mathf.Max(_accumulatedTime, Mathf.Clamp01(progress) * _completionTime);
            _isRevealing = true;
            _isComplete = false;
            DispatchReadyMarkers(plan);
            while (!IsPaused && _revealedCount < _totalCount && _orderedRevealTimes[_revealedCount] <= targetTime)
            {
                int glyphIndex = GetRevealGlyphAt(_revealedCount);
                MarkGlyphRevealed(glyphIndex, GetAppearanceStartTimeForReveal(glyphIndex));
                _revealedCount++;
                OnGlyphRevealed?.Invoke(glyphIndex);
                DispatchReadyMarkers(plan);
            }
            if (!IsPaused)
            {
                _appearanceTime += targetTime - _accumulatedTime;
                _accumulatedTime = targetTime;
            }
            _isComplete = !IsPaused && _revealedCount == _totalCount && _accumulatedTime + AppearanceCompletionTolerance >= _completionTime;
            _revealProgress = _isComplete ? 1f : ResolveTimelineProgress(_accumulatedTime);
            _isRevealing = false;
            _changedThisUpdate = true;
            DispatchAppearanceCompletions();
            if (_isComplete)
            {
                _loopWaitRemaining = _loop ? _loopDelay : 0f;
                if (!completeBefore) OnRevealComplete?.Invoke();
            }
        }

        public void PauseReveal()
        {
            if (!_isRevealing || _isComplete) return;
            _pauseDepth++;
        }

        public void ResumeReveal()
        {
            if (_pauseDepth > 0)
                _pauseDepth--;
        }

        float GetScrubAccumulatedTime(int targetCount)
        {
            if (_revealTimes == null || _totalCount == 0 || targetCount <= 0) return 0f;

            int revealIndex = Mathf.Clamp(targetCount - 1, 0, _totalCount - 1);
            float scrubTime = _orderedRevealTimes[revealIndex];
            if (targetCount >= _totalCount)
            {
                scrubTime = Mathf.Max(
                    _completionTime,
                    scrubTime + _appearanceDuration);
            }

            return scrubTime;
        }

        /// <summary>
        /// Update reveal progression. Call once per frame.
        /// </summary>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <param name="plan">The compiled plan (for marker dispatch).</param>
        /// <returns>Number of newly revealed glyphs this frame.</returns>
        public int Update(float deltaTime, TextStudioCompiledPlan plan)
        {
            _changedThisUpdate = false;
            float scaledDeltaTime = Mathf.Max(0f, deltaTime) * _speedMultiplier;
            if (_isDisappearing)
            {
                _appearanceTime += scaledDeltaTime;
                _disappearanceElapsed += scaledDeltaTime;
                _disappearProgress = Mathf.Clamp01(_disappearanceElapsed / _disappearanceDuration);
                if (NotifyGlyphDisappearStarted)
                    DispatchDisappearStarts();
                _changedThisUpdate = scaledDeltaTime > 0f;
                if (_disappearProgress >= 1f)
                    CompleteDisappear();
                return 0;
            }

            if (_isComplete)
            {
                bool wasActive = HasActiveAppearance;
                _appearanceTime += scaledDeltaTime;
                _changedThisUpdate = wasActive && scaledDeltaTime > 0f;
                DispatchAppearanceCompletions();
                if (HasActiveAppearance)
                {
                    return 0;
                }

                if (!UpdateLoopWait(deltaTime)) return 0;
                if (_changedThisUpdate) return 0;
            }
            else if (!_isRevealing)
            {
                return 0;
            }

            if (IsPaused)
            {
                if (HasActiveAppearance && scaledDeltaTime > 0f)
                    _changedThisUpdate = true;
                _appearanceTime += scaledDeltaTime;
                DispatchAppearanceCompletions();
                return 0;
            }

            bool appearanceWasActive = HasActiveAppearance;
            _accumulatedTime += scaledDeltaTime;
            _appearanceTime += scaledDeltaTime;

            int newlyRevealed = 0;
            DispatchReadyMarkers(plan);
            if (IsPaused)
            {
                DispatchAppearanceCompletions();
                return newlyRevealed;
            }

            // Reveal glyphs whose time has come
            while (_revealedCount < _totalCount && _accumulatedTime >= _orderedRevealTimes[_revealedCount])
            {
                int glyphIndex = GetRevealGlyphAt(_revealedCount);
                MarkGlyphRevealed(glyphIndex, GetAppearanceStartTimeForReveal(glyphIndex));
                OnGlyphRevealed?.Invoke(glyphIndex);
                _revealedCount++;
                newlyRevealed++;

                DispatchReadyMarkers(plan);
                if (IsPaused)
                    break;
            }

            _revealProgress = ResolveTimelineProgress(_accumulatedTime);
            _changedThisUpdate |= newlyRevealed > 0 || (scaledDeltaTime > 0f && (appearanceWasActive || HasActiveAppearance));
            DispatchAppearanceCompletions();

            if (IsPaused)
                return newlyRevealed;

            if (_revealedCount >= _totalCount &&
                _accumulatedTime + AppearanceCompletionTolerance >= _completionTime)
            {
                DispatchReadyMarkers(plan);
                if (IsPaused)
                    return newlyRevealed;

                _isRevealing = false;
                _isComplete = true;
                _revealProgress = 1f;
                _changedThisUpdate = true;
                _loopWaitRemaining = _loop ? _loopDelay : 0f;
                OnRevealComplete?.Invoke();
            }

            return newlyRevealed;
        }

        void DispatchReadyMarkers(TextStudioCompiledPlan plan)
        {
            if (plan == null)
                return;

            int revealedSourcePrefix = GetRevealedSourcePrefix();
            if (plan.ActionMarkers != null)
            {
                for (int m = 0; m < plan.ActionMarkerCount; m++)
                {
                    if (plan.ActionMarkers[m].sourceIndex > revealedSourcePrefix)
                        break;
                    if (IsMarkerDispatched(m))
                        continue;

                    ActionMarker marker = plan.ActionMarkers[m];
                    OnMarkerReached?.Invoke(marker.actionId, marker.parameters);
                    OnActionMarkerReached?.Invoke(marker);
                    SetMarkerDispatched(m);
                    _lastDispatchedMarkerIndex = m;
                    // A blocking action also holds later markers at the same source boundary.
                    if (IsPaused)
                        return;
                }
            }

            DispatchReadyExtensionTags(plan, flushingSkip: false, revealedSourcePrefix);
        }

        void DispatchReadyExtensionTags(
            TextStudioCompiledPlan plan,
            bool flushingSkip,
            int revealedSourcePrefix = -1)
        {
            if (plan == null || plan.ExtensionTags == null)
                return;

            for (int m = 0; m < plan.ExtensionTagCount; m++)
            {
                TextStudioExtensionTag tag = plan.ExtensionTags[m];
                int threshold = revealedSourcePrefix >= 0 ? revealedSourcePrefix : GetRevealedSourcePrefix();
                if (!flushingSkip && tag.GlyphStart > threshold)
                    break;
                if (IsExtensionTagDispatched(m))
                    continue;

                if (tag.DispatchesOnReveal && (!flushingSkip || tag.FlushesOnSkip))
                    OnExtensionTagReached?.Invoke(tag);

                SetExtensionTagDispatched(m);
                _lastDispatchedExtensionTagIndex = m;
            }
        }

        internal bool HasActiveAppearance
        {
            get
            {
                if (_mode == RevealMode.Instant || _appearancesSettled || _revealedCount <= 0 || _totalCount <= 0 || _appearanceStartTimes == null) return false;
                for (int glyphIndex = 0; glyphIndex < _totalCount; glyphIndex++)
                {
                    if (_revealedFlags[glyphIndex] && !_instantFlags[glyphIndex] &&
                        IsAppearanceStillActive(GetAppearanceElapsed(glyphIndex)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        bool UpdateLoopWait(float deltaTime)
        {
            if (!_loop || !_isComplete || _totalCount == 0) return false;

            _loopWaitRemaining -= Mathf.Max(0f, deltaTime);
            if (_loopWaitRemaining > 0f) return false;

            RestartLoop();
            return true;
        }

        void RestartLoop()
        {
            _completionTime = _scheduledCompletionTime;
            _revealedCount = 0;
            _revealProgress = 0f;
            _accumulatedTime = 0f;
            _appearanceTime = 0f;
            _appearancesSettled = false;
            _settledActiveStartTime = 0f;
            CancelDisappear();
            ClearDispatchState();
            _isRevealing = true;
            _isComplete = false;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _changedThisUpdate = true;

            ClearRevealFlags();
        }

        void ClearRevealFlags()
        {
            unchecked { _appearanceNotificationVersion++; }
            if (_revealedFlags != null && _totalCount > 0)
                Array.Clear(_revealedFlags, 0, _totalCount);
            if (_appearanceStartTimes != null && _totalCount > 0)
                Array.Clear(_appearanceStartTimes, 0, _totalCount);
            if (_appearanceCompleteNotified != null && _totalCount > 0)
                Array.Clear(_appearanceCompleteNotified, 0, Mathf.Min(_totalCount, _appearanceCompleteNotified.Length));
        }

        void CancelDisappear()
        {
            _isDisappearing = false;
            _disappearanceElapsed = 0f;
            _disappearProgress = 0f;
            _hasSelectiveDisappear = false;
            _disappearAffectedCount = 0;
        }

        internal float ShowCompletionOverrun => Mathf.Max(0f, _accumulatedTime - _completionTime);
        internal float HideCompletionOverrun { get; private set; }

        void CompleteDisappear()
        {
            HideCompletionOverrun = Mathf.Max(0f, _disappearanceElapsed - _disappearanceDuration);
            int retainedCount = 0;
            if (_revealedFlags != null)
            {
                for (int i = 0; i < _totalCount; i++)
                {
                    if (!_revealedFlags[i])
                        continue;

                    if (_hasSelectiveDisappear && !IsGlyphAffectedByDisappear(i))
                    {
                        retainedCount++;
                        continue;
                    }

                    _revealedFlags[i] = false;
                    OnGlyphDisappeared?.Invoke(i);
                }
            }

            _revealedCount = retainedCount;
            _revealProgress = _totalCount > 0 ? (float)retainedCount / _totalCount : 0f;
            _isRevealing = false;
            _isComplete = false;
            _pauseDepth = 0;
            _loopWaitRemaining = 0f;
            _isDisappearing = false;
            _disappearanceElapsed = _disappearanceDuration;
            _disappearProgress = 1f;
            _changedThisUpdate = true;
            _hasSelectiveDisappear = false;
            _disappearAffectedCount = 0;
            OnDisappearComplete?.Invoke();
        }

        void CompleteActiveAppearances()
        {
            if (_appearanceStartTimes != null && _totalCount > 0)
            {
                int count = Mathf.Clamp(_revealedCount, 0, _totalCount);
                for (int i = 0; i < _totalCount; i++)
                {
                    if (_revealedFlags[i])
                        _appearanceStartTimes[i] = 0f;
                }
            }

            _appearanceTime = Mathf.Max(_appearanceTime, _appearanceDuration);
            if (!_appearancesSettled)
            {
                float visualCompletionTime = _totalCount > 0 && _orderedRevealTimes != null
                    ? Mathf.Max(
                        _completionTime,
                        _orderedRevealTimes[_totalCount - 1] + _appearanceDuration)
                    : _appearanceDuration;
                _settledActiveStartTime = Mathf.Min(
                    _appearanceTime,
                    visualCompletionTime);
            }

            _appearancesSettled = true;
        }

        /// <summary>
        /// Check if a specific glyph has been revealed.
        /// </summary>
        public bool IsGlyphRevealed(int glyphIndex)
        {
            if (_revealedFlags == null || glyphIndex >= _totalCount) return false;
            return _revealedFlags[glyphIndex];
        }

        /// <summary>
        /// Get the animation phase for a glyph based on reveal state.
        /// </summary>
        public AnimationPhase GetGlyphPhase(int glyphIndex, float time)
        {
            if (!IsGlyphRevealed(glyphIndex))
                return AnimationPhase.None;

            if (_isDisappearing)
            {
                if (!IsGlyphAffectedByDisappear(glyphIndex))
                    return AnimationPhase.Persistent;
                float disappearanceProgress = GetDisappearanceProgress(glyphIndex);
                if (_hideSequenceSettings.Order != TextStudioSequenceOrder.AllAtOnce)
                {
                    if (disappearanceProgress >= 1f)
                        return AnimationPhase.None;

                    if (disappearanceProgress <= 0f && GetHideOrderIndex(glyphIndex) > 0)
                        return AnimationPhase.Persistent;
                }

                return AnimationPhase.Disappearance;
            }

            float timeSinceReveal = GetAppearanceElapsed(glyphIndex);

            if (_mode != RevealMode.Instant && !_instantFlags[glyphIndex] && IsAppearanceStillActive(timeSinceReveal))
                return AnimationPhase.Appearance;

            return AnimationPhase.Persistent;
        }

        /// <summary>
        /// Resolves <see cref="GetGlyphPhase"/> and the matching phase progress in one pass, reading the reveal flag and
        /// appearance time once per glyph.
        /// </summary>
        internal void GetGlyphPhaseAndProgress(int glyphIndex, out AnimationPhase phase, out float progress)
        {
            if (!IsGlyphRevealed(glyphIndex))
            {
                phase = AnimationPhase.None;
                progress = 0f;
                return;
            }

            if (_isDisappearing)
            {
                phase = GetGlyphPhase(glyphIndex, 0f);
                progress = phase == AnimationPhase.Disappearance
                    ? GetDisappearanceProgress(glyphIndex)
                    : phase == AnimationPhase.Persistent ? 1f : 0f;
                return;
            }

            bool instant = _mode == RevealMode.Instant || _instantFlags[glyphIndex];
            float elapsed = GetAppearanceElapsed(glyphIndex);
            if (!instant && IsAppearanceStillActive(elapsed))
            {
                phase = AnimationPhase.Appearance;
                progress = _appearancesSettled ? 1f : Mathf.Clamp01(elapsed / _appearanceDuration);
                return;
            }

            phase = AnimationPhase.Persistent;
            progress = 1f;
        }

        /// <summary>
        /// Get appearance progress (0–1) for a glyph in its appearance phase.
        /// </summary>
        public float GetAppearanceProgress(int glyphIndex)
        {
            if (!IsGlyphRevealed(glyphIndex)) return 0f;
            if (_mode == RevealMode.Instant || _instantFlags[glyphIndex] || _appearancesSettled) return 1f;
            float elapsed = GetAppearanceElapsed(glyphIndex);
            return Mathf.Clamp01(elapsed / _appearanceDuration);
        }

        internal float GetRevealActiveBlendTime()
        {
            if (_totalCount <= 0)
                return 0f;

            if (_appearancesSettled)
                return _appearanceTime - _settledActiveStartTime;

            float visualCompletionTime = Mathf.Max(
                _completionTime,
                _orderedRevealTimes[_totalCount - 1] + _appearanceDuration);
            if (visualCompletionTime <= AppearanceCompletionTolerance)
                return 0f;

            if (_appearanceTime < visualCompletionTime)
            {
                float revealProgress = Mathf.Clamp01(
                    _appearanceTime / visualCompletionTime);
                return (revealProgress - 1f) * _appearanceDuration;
            }

            return _appearanceTime - visualCompletionTime;
        }

        internal void SetRevealActiveBlendEnabledForPreview(bool enabled)
        {
            _revealActiveBlendEnabled = enabled;
        }

        internal void SetActiveElapsedTimeForPreview(float elapsedTime)
        {
            StartReveal();
            SetScrubElapsedTime(
                GetApproximateVisualRevealDuration() + Mathf.Max(0f, elapsedTime));
        }

        public float GetDisappearanceProgress(int glyphIndex)
        {
            if (!IsGlyphRevealed(glyphIndex)) return 1f;
            if (!_isDisappearing) return 0f;
            if (!IsGlyphAffectedByDisappear(glyphIndex)) return 0f;

            switch (_hideSequenceSettings.Order)
            {
                case TextStudioSequenceOrder.Backward:
                case TextStudioSequenceOrder.Forward:
                case TextStudioSequenceOrder.CenterOut:
                case TextStudioSequenceOrder.EdgesIn:
                case TextStudioSequenceOrder.SeededRandom:
                    return GetOrderedDisappearanceProgress(glyphIndex);
                case TextStudioSequenceOrder.AllAtOnce:
                default:
                    return _disappearProgress;
            }
        }

        float GetOrderedDisappearanceProgress(int glyphIndex)
        {
            int affectedCount = _hasSelectiveDisappear ? _disappearAffectedCount : _hideUnitCount;
            if (affectedCount <= 1)
                return _disappearProgress;

            int orderIndex = GetHideOrderIndex(glyphIndex);
            TextStudioSequenceSettings settings = NormalizeSequence(_hideSequenceSettings);
            float spread = Mathf.Max(0f, settings.Spread);
            float timelineLength = 1f + spread * (affectedCount - 1);
            return Mathf.Clamp01(_disappearProgress * timelineLength - orderIndex * spread);
        }

        int GetHideOrderIndex(int glyphIndex)
        {
            if (_hasSelectiveDisappear &&
                _selectedHideOrderByGlyph != null &&
                glyphIndex >= 0 &&
                glyphIndex < _totalCount)
            {
                return Mathf.Max(0, _selectedHideOrderByGlyph[glyphIndex]);
            }

            if (_hideOrderByGlyph != null &&
                glyphIndex >= 0 &&
                glyphIndex < _totalCount)
            {
                return Mathf.Clamp(_hideOrderByGlyph[glyphIndex], 0, Mathf.Max(0, _totalCount - 1));
            }

            return Mathf.Clamp(glyphIndex, 0, Mathf.Max(0, _totalCount - 1));
        }

        void DispatchDisappearStarts()
        {
            if (_disappearStartedFlags == null)
                return;

            int affectedCount = _hasSelectiveDisappear ? _disappearAffectedCount : _hideUnitCount;
            if (affectedCount <= 0)
                return;

            float spread = _hideSequenceSettings.Order == TextStudioSequenceOrder.AllAtOnce
                ? 0f
                : Mathf.Max(0f, _hideSequenceSettings.Spread);
            float timelineLength = 1f + spread * (affectedCount - 1);
            for (int i = 0; i < _totalCount; i++)
            {
                if (_disappearStartedFlags[i] || !IsGlyphRevealed(i) || !IsGlyphAffectedByDisappear(i))
                    continue;
                int orderIndex = GetHideOrderIndex(i);
                float startProgress = orderIndex * spread / timelineLength;
                if (_disappearProgress < startProgress)
                    continue;
                _disappearStartedFlags[i] = true;
                OnGlyphDisappearStarted?.Invoke(i);
            }
        }

        void ConfigureDisappearSelection(in TextStudioGlyphChangeSet affectedGlyphs)
        {
            _hasSelectiveDisappear = affectedGlyphs.IsDefined &&
                affectedGlyphs.OutgoingGlyphCount == _totalCount;
            _disappearAffectedCount = 0;
            if (!_hasSelectiveDisappear)
                return;

            if (_disappearAffectedFlags == null || _disappearAffectedFlags.Length < _totalCount)
                _disappearAffectedFlags = new bool[TextStudioBufferCapacity.ForCount(_totalCount)];
            if (_selectedHideOrderByGlyph == null || _selectedHideOrderByGlyph.Length < _totalCount)
                _selectedHideOrderByGlyph = new int[TextStudioBufferCapacity.ForCount(_totalCount)];
            Array.Clear(_disappearAffectedFlags, 0, _totalCount);
            for (int i = 0; i < _totalCount; i++)
                _selectedHideOrderByGlyph[i] = -1;

            for (int order = 0; order < _totalCount; order++)
            {
                int glyphIndex = _hideGlyphSequence != null
                    ? _hideGlyphSequence[order]
                    : order;
                if (!affectedGlyphs.IsOutgoingGlyphChanged(glyphIndex))
                    continue;
                _disappearAffectedFlags[glyphIndex] = true;
                _selectedHideOrderByGlyph[glyphIndex] = _disappearAffectedCount++;
            }
        }

        bool IsGlyphAffectedByDisappear(int glyphIndex)
        {
            return !_hasSelectiveDisappear ||
                   (_disappearAffectedFlags != null &&
                    glyphIndex >= 0 &&
                    glyphIndex < _totalCount &&
                    _disappearAffectedFlags[glyphIndex]);
        }

        void ResetDispatchState(TextStudioCompiledPlan plan)
        {
            _markerStateCount = plan?.ActionMarkerCount ?? 0;
            _extensionTagStateCount = plan?.ExtensionTagCount ?? 0;
            if (_dispatchedMarkerFlags == null ||
                _dispatchedMarkerFlags.Length < _markerStateCount)
            {
                _dispatchedMarkerFlags = new bool[
                    TextStudioBufferCapacity.ForCount(_markerStateCount)];
            }
            if (_dispatchedExtensionTagFlags == null ||
                _dispatchedExtensionTagFlags.Length < _extensionTagStateCount)
            {
                _dispatchedExtensionTagFlags = new bool[
                    TextStudioBufferCapacity.ForCount(_extensionTagStateCount)];
            }

            ClearDispatchState();
        }

        void ClearDispatchState()
        {
            if (_dispatchedMarkerFlags != null && _markerStateCount > 0)
                Array.Clear(_dispatchedMarkerFlags, 0, _markerStateCount);
            if (_dispatchedExtensionTagFlags != null && _extensionTagStateCount > 0)
                Array.Clear(_dispatchedExtensionTagFlags, 0, _extensionTagStateCount);
            _lastDispatchedMarkerIndex = -1;
            _lastDispatchedExtensionTagIndex = -1;
        }

        bool IsMarkerDispatched(int markerIndex)
        {
            return _dispatchedMarkerFlags != null &&
                   markerIndex >= 0 &&
                   markerIndex < _markerStateCount &&
                   _dispatchedMarkerFlags[markerIndex];
        }

        void SetMarkerDispatched(int markerIndex)
        {
            if (_dispatchedMarkerFlags == null ||
                markerIndex < 0 ||
                markerIndex >= _markerStateCount)
            {
                return;
            }

            _dispatchedMarkerFlags[markerIndex] = true;
        }

        bool IsExtensionTagDispatched(int tagIndex)
        {
            return _dispatchedExtensionTagFlags != null &&
                   tagIndex >= 0 &&
                   tagIndex < _extensionTagStateCount &&
                   _dispatchedExtensionTagFlags[tagIndex];
        }

        void SetExtensionTagDispatched(int tagIndex)
        {
            if (_dispatchedExtensionTagFlags == null ||
                tagIndex < 0 ||
                tagIndex >= _extensionTagStateCount)
            {
                return;
            }

            _dispatchedExtensionTagFlags[tagIndex] = true;
        }

        ActionMarker[] CaptureDispatchedMarkers(TextStudioCompiledPlan plan)
        {
            if (plan == null || plan.ActionMarkers == null || plan.ActionMarkerCount == 0)
                return Array.Empty<ActionMarker>();

            int count = 0;
            for (int i = 0; i < plan.ActionMarkerCount; i++)
            {
                if (IsMarkerDispatched(i))
                    count++;
            }
            if (count == 0)
                return Array.Empty<ActionMarker>();

            ActionMarker[] result = new ActionMarker[count];
            int write = 0;
            for (int i = 0; i < plan.ActionMarkerCount; i++)
            {
                if (IsMarkerDispatched(i))
                    result[write++] = plan.ActionMarkers[i];
            }
            return result;
        }

        TextStudioExtensionTag[] CaptureDispatchedExtensionTags(
            TextStudioCompiledPlan plan)
        {
            if (plan == null || plan.ExtensionTags == null || plan.ExtensionTagCount == 0)
                return Array.Empty<TextStudioExtensionTag>();

            int count = 0;
            for (int i = 0; i < plan.ExtensionTagCount; i++)
            {
                if (IsExtensionTagDispatched(i))
                    count++;
            }
            if (count == 0)
                return Array.Empty<TextStudioExtensionTag>();

            TextStudioExtensionTag[] result = new TextStudioExtensionTag[count];
            int write = 0;
            for (int i = 0; i < plan.ExtensionTagCount; i++)
            {
                if (IsExtensionTagDispatched(i))
                    result[write++] = plan.ExtensionTags[i];
            }
            return result;
        }

        void RestoreDispatchState(
            in TextChangeState state,
            TextStudioCompiledPlan plan,
            TextStudioGlyphChangeSet glyphChanges)
        {
            ClearDispatchState();
            if (plan == null)
                return;

            ActionMarker[] oldMarkers = state.DispatchedMarkers;
            if (oldMarkers != null && plan.ActionMarkers != null)
            {
                for (int oldIndex = 0; oldIndex < oldMarkers.Length; oldIndex++)
                {
                    ActionMarker oldMarker = oldMarkers[oldIndex];
                    if (!glyphChanges.TryMapOutgoingBoundary(
                            oldMarker.sourceIndex,
                            out int incomingBoundary))
                    {
                        continue;
                    }

                    for (int incomingIndex = 0;
                         incomingIndex < plan.ActionMarkerCount;
                         incomingIndex++)
                    {
                        if (IsMarkerDispatched(incomingIndex))
                            continue;
                        ActionMarker incomingMarker = plan.ActionMarkers[incomingIndex];
                        if (incomingMarker.sourceIndex != incomingBoundary ||
                            !MarkersEqual(in oldMarker, in incomingMarker))
                        {
                            continue;
                        }

                        SetMarkerDispatched(incomingIndex);
                        _lastDispatchedMarkerIndex = Mathf.Max(
                            _lastDispatchedMarkerIndex,
                            incomingIndex);
                        break;
                    }
                }
            }

            TextStudioExtensionTag[] oldTags = state.DispatchedExtensionTags;
            if (oldTags == null || plan.ExtensionTags == null)
                return;

            for (int oldIndex = 0; oldIndex < oldTags.Length; oldIndex++)
            {
                TextStudioExtensionTag oldTag = oldTags[oldIndex];
                if (!glyphChanges.TryMapOutgoingBoundary(
                        oldTag.GlyphStart,
                        out int incomingStart) ||
                    !glyphChanges.TryMapOutgoingBoundary(
                        oldTag.GlyphStart + oldTag.GlyphCount,
                        out int incomingEnd))
                {
                    continue;
                }

                for (int incomingIndex = 0;
                     incomingIndex < plan.ExtensionTagCount;
                     incomingIndex++)
                {
                    if (IsExtensionTagDispatched(incomingIndex))
                        continue;
                    TextStudioExtensionTag incomingTag = plan.ExtensionTags[incomingIndex];
                    if (incomingTag.GlyphStart != incomingStart ||
                        incomingTag.GlyphCount != incomingEnd - incomingStart ||
                        !ExtensionTagsEqual(in oldTag, in incomingTag))
                    {
                        continue;
                    }

                    SetExtensionTagDispatched(incomingIndex);
                    _lastDispatchedExtensionTagIndex = Mathf.Max(
                        _lastDispatchedExtensionTagIndex,
                        incomingIndex);
                    break;
                }
            }
        }

        static bool MarkersEqual(in ActionMarker left, in ActionMarker right)
        {
            return string.Equals(
                       left.actionId,
                       right.actionId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.TagParameters.Raw,
                       right.TagParameters.Raw,
                       StringComparison.Ordinal);
        }

        static bool ExtensionTagsEqual(
            in TextStudioExtensionTag left,
            in TextStudioExtensionTag right)
        {
            return string.Equals(left.TagId, right.TagId, StringComparison.Ordinal) &&
                   string.Equals(
                       left.AuthoredName,
                       right.AuthoredName,
                       StringComparison.Ordinal) &&
                   left.Boundary == right.Boundary &&
                   string.Equals(
                       left.Parameters.Raw,
                       right.Parameters.Raw,
                       StringComparison.Ordinal);
        }

        internal int[] GetHideOrderForDebug()
        {
            int[] result = new int[_totalCount];
            for (int i = 0; i < _totalCount; i++)
                result[i] = GetHideOrderIndex(i);
            return result;
        }

        void MarkGlyphRevealed(int glyphIndex, float appearanceStartTime)
        {
            if (_revealedFlags == null || glyphIndex < 0 || glyphIndex >= _totalCount) return;

            if (!_revealedFlags[glyphIndex] && _appearanceCompleteNotified != null &&
                glyphIndex < _appearanceCompleteNotified.Length)
                _appearanceCompleteNotified[glyphIndex] = false;
            _revealedFlags[glyphIndex] = true;
            if (_appearanceStartTimes != null && glyphIndex < _appearanceStartTimes.Length)
                _appearanceStartTimes[glyphIndex] = appearanceStartTime;
        }

        float GetAppearanceStartTimeForReveal(int glyphIndex)
        {
            if (_revealTimes == null || glyphIndex < 0 || glyphIndex >= _totalCount)
                return _appearanceTime;

            float overshoot = Mathf.Max(0f, _accumulatedTime - GetScheduledRevealTime(glyphIndex));
            return _appearanceTime - overshoot;
        }

        internal float GetAppearanceElapsed(int glyphIndex)
        {
            if (_appearanceStartTimes == null || glyphIndex < 0 || glyphIndex >= _totalCount)
                return 0f;

            return _appearanceTime - _appearanceStartTimes[glyphIndex];
        }

        float ITextStudioRevealState.GetGlyphRevealElapsedTime(int glyphIndex)
        {
            return IsGlyphRevealed(glyphIndex) ? Mathf.Max(0f, GetAppearanceElapsed(glyphIndex)) : 0f;
        }

        void SyncAppearanceStartTimesToRevealTimes(int revealedCount)
        {
            if (_appearanceStartTimes == null || _revealTimes == null || _totalCount <= 0) return;

            int count = Mathf.Clamp(revealedCount, 0, _totalCount);
            Array.Clear(_appearanceStartTimes, 0, _totalCount);
            for (int order = 0; order < count; order++)
            {
                int glyphIndex = GetRevealGlyphAt(order);
                _appearanceStartTimes[glyphIndex] = _orderedRevealTimes[order];
            }
        }

        bool IsAppearanceStillActive(float elapsed)
        {
            return elapsed + AppearanceCompletionTolerance < _appearanceDuration;
        }

        /// <summary>
        /// Get approximate total duration for the reveal.
        /// </summary>
        public float GetApproximateDuration()
        {
            if (_totalCount == 0) return 0f;
            return _completionTime / _speedMultiplier;
        }

        internal float GetProgressForGlyphCount(int glyphCount)
        {
            if (glyphCount <= 0 || _totalCount == 0) return 0f;
            if (glyphCount >= _totalCount || _completionTime <= 0f) return 1f;
            float start = _orderedRevealTimes[glyphCount - 1];
            int next = glyphCount;
            while (next < _totalCount && _orderedRevealTimes[next] <= start) next++;
            float end = next < _totalCount ? _orderedRevealTimes[next] : _completionTime;
            return Mathf.Clamp01((start + (end - start) * 0.5f) / _completionTime);
        }

        public float GetApproximateHideDuration()
        {
            if (_totalCount == 0) return 0f;
            return _disappearanceDuration / _speedMultiplier;
        }

        internal float GetApproximateVisualRevealDuration()
        {
            if (_totalCount == 0)
                return 0f;

            return _completionTime / _speedMultiplier;
        }

        internal float GetApproximateRevealTime(int glyphIndex)
        {
            if (_totalCount == 0 || _revealTimes == null) return 0f;
            int index = Mathf.Clamp(glyphIndex, 0, _totalCount - 1);
            return GetScheduledRevealTime(index) / _speedMultiplier;
        }

        static float GetDefaultDelay(float baseDelay)
        {
            // Uniform delay for every character by default. Punctuation pauses are
            // opt-in via a TypingTimingsBase asset or the built-in "Expressive" preset -
            // they're disruptive when authors just want smooth typewriter-style reveal.
            return baseDelay;
        }

        float ITextStudioRevealState.GetApproximateRevealTime(int glyphIndex)
        {
            return GetApproximateRevealTime(glyphIndex);
        }

        static bool IsInstantGlyph(TextStudioCompiledPlan plan, int glyphIndex)
        {
            if (plan == null || plan.InstantRanges == null)
                return false;

            for (int i = 0; i < plan.InstantRangeCount; i++)
            {
                if (plan.InstantRanges[i].Contains(glyphIndex))
                    return true;
            }

            return false;
        }

        static int GetLastVisibleUnitIndex(TextStudioCompiledPlan plan)
        {
            if (plan == null)
                return -1;

            for (int i = plan.VisibleGlyphCount - 1; i >= 0; i--)
            {
                if (!plan.IsWhiteSpaceUnit(i))
                    return i;
            }

            return -1;
        }

        void EnsureSequenceCapacity(int capacity)
        {
            if (_revealSequence == null || _revealSequence.Length < capacity)
            {
                _revealSequence = new int[capacity];
                _instantScheduleCount = 0;
            }
            if (_orderedRevealTimes == null || _orderedRevealTimes.Length < capacity)
            {
                _orderedRevealTimes = new float[capacity];
                _instantScheduleCount = 0;
            }
            if (_unitStarts == null || _unitStarts.Length < capacity)
                _unitStarts = new int[capacity];
            if (_unitEnds == null || _unitEnds.Length < capacity)
                _unitEnds = new int[capacity];
            if (_unitSequence == null || _unitSequence.Length < capacity)
                _unitSequence = new int[capacity];
            if (_unitTimes == null || _unitTimes.Length < capacity)
                _unitTimes = new float[capacity];
            if (_unitDelays == null || _unitDelays.Length < capacity)
                _unitDelays = new float[capacity];
            if (_unitInstantFlags == null || _unitInstantFlags.Length < capacity)
                _unitInstantFlags = new bool[capacity];
        }

        bool CanUseInstantSchedule(
            TextStudioCompiledPlan plan,
            TypingTimingsBase timings,
            TextStudioTimingEvaluator builtInTimings)
        {
            return _mode == RevealMode.Instant &&
                   timings == null &&
                   builtInTimings == null &&
                   _totalCount > 0 &&
                   !HasSpatialSequence &&
                   (plan.InstantRanges == null || plan.InstantRangeCount <= 0);
        }

        // Instant text reveals as one unit at time zero in logical order, so its schedule is what the per-glyph setup, BuildRevealSchedule and BuildHideOrder write for it. Slots that already hold that layout are left untouched.
        void ApplyInstantSchedule(float delay)
        {
            int count = _totalCount;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            if (_hideGlyphSequence == null || _hideGlyphSequence.Length < count)
            {
                _hideGlyphSequence = new int[capacity];
                _instantScheduleCount = 0;
            }
            if (_hideOrderByGlyph == null || _hideOrderByGlyph.Length < count)
            {
                _hideOrderByGlyph = new int[capacity];
                _instantScheduleCount = 0;
            }
            if (!_instantScheduleDelay.Equals(delay))
                _instantScheduleCount = 0;

            for (int i = _instantScheduleCount; i < count; i++)
            {
                _revealTimes[i] = 0f;
                _postRevealDelays[i] = delay;
                _instantFlags[i] = false;
                _revealSequence[i] = i;
                _orderedRevealTimes[i] = 0f;
                _hideGlyphSequence[i] = i;
                _hideOrderByGlyph[i] = 0;
            }
            _instantScheduleCount = Math.Max(_instantScheduleCount, count);
            _instantScheduleDelay = delay;

            _unitStarts[0] = 0;
            _unitEnds[0] = count;
            _unitTimes[0] = 0f;
            _unitDelays[0] = delay;
            _unitInstantFlags[0] = false;
            _unitSequence[0] = 0;
            _scheduleUnitCount = 1;
            _hideUnitCount = 1;
            _completionTime = _scheduledCompletionTime = 0f;
        }

        void BuildRevealSchedule(TextStudioCompiledPlan plan)
        {
            _instantScheduleCount = 0;
            if (_totalCount <= 0)
            {
                _completionTime = _scheduledCompletionTime = 0f;
                return;
            }

            EnsureSequenceCapacity(_totalCount);
            if (_mode == RevealMode.ByWord)
                BuildWordRevealGroups(plan);
            int unitCount = 0;
            int unitStart = 0;
            int previousGroup = GetRevealGroup(plan, 0);
            for (int glyphIndex = 1; glyphIndex <= _totalCount; glyphIndex++)
            {
                int group = glyphIndex < _totalCount ? GetRevealGroup(plan, glyphIndex) : int.MinValue;
                bool continuesInstantRange =
                    glyphIndex < _totalCount &&
                    (_mode == RevealMode.ByGlyph || _mode == RevealMode.ByLetter) &&
                    _instantFlags[glyphIndex - 1] &&
                    _instantFlags[glyphIndex];
                if (glyphIndex < _totalCount &&
                    (group == previousGroup || continuesInstantRange))
                {
                    continue;
                }

                _unitStarts[unitCount] = unitStart;
                _unitEnds[unitCount] = glyphIndex;
                _unitTimes[unitCount] = 0f;
                bool allInstant = true;
                float unitDelay = 0f;
                for (int sourceGlyph = unitStart; sourceGlyph < glyphIndex; sourceGlyph++)
                {
                    allInstant &= _instantFlags[sourceGlyph];
                    unitDelay = Mathf.Max(unitDelay, _postRevealDelays[sourceGlyph]);
                }

                if (allInstant)
                    unitDelay = _postRevealDelays[glyphIndex - 1];

                _unitDelays[unitCount] = unitDelay;
                _unitInstantFlags[unitCount] = allInstant;
                unitCount++;
                unitStart = glyphIndex;
                previousGroup = group;
            }

            TextStudioSequenceSettings settings = NormalizeSequence(_revealSequenceSettings);
            TextStudioSequenceUtility.BuildOrder(
                unitCount,
                in settings,
                _unitSequence,
                null);

            _scheduleUnitCount = unitCount;
            ApplySpatialOrder(unitCount, in settings, _unitSequence);
            int orderedGlyph = 0;
            float visualCompletion = 0f;
            float scheduledTime = 0f;
            float completionTime = 0f;
            for (int orderedUnit = 0; orderedUnit < unitCount; orderedUnit++)
            {
                int sourceUnit = _unitSequence[orderedUnit];
                _unitTimes[sourceUnit] = scheduledTime;

                for (int glyphIndex = _unitStarts[sourceUnit]; glyphIndex < _unitEnds[sourceUnit]; glyphIndex++)
                {
                    _revealSequence[orderedGlyph] = glyphIndex;
                    _orderedRevealTimes[orderedGlyph] = scheduledTime;
                    _revealTimes[glyphIndex] = scheduledTime;
                    orderedGlyph++;
                }

                float delayAfter =
                    _mode == RevealMode.Instant ||
                    settings.Order == TextStudioSequenceOrder.AllAtOnce
                    ? 0f
                    : _unitDelays[sourceUnit] * settings.Spread;
                bool instantRangeReachesTextEnd =
                    _unitInstantFlags[sourceUnit] &&
                    _unitEnds[sourceUnit] >= _totalCount;
                if (instantRangeReachesTextEnd)
                    delayAfter = 0f;

                float transitionDuration = _mode == RevealMode.Instant || _unitInstantFlags[sourceUnit] ? 0f : _appearanceDuration;
                visualCompletion = Mathf.Max(visualCompletion, scheduledTime + transitionDuration);
                bool sequential = TimingMode == TextStudioTypewriterTimingMode.FinishCurrentUnit && settings.Order != TextStudioSequenceOrder.AllAtOnce;
                bool finalOrderedUnit = orderedUnit + 1 >= unitCount;
                if (finalOrderedUnit)
                {
                    completionTime = scheduledTime + (sequential ? transitionDuration : 0f) +
                                     (_waitAfterFinalReveal ? delayAfter : 0f);
                }
                else
                {
                    scheduledTime += delayAfter + (sequential ? transitionDuration : 0f);
                }
            }

            _completionTime = _scheduledCompletionTime = Mathf.Max(completionTime, visualCompletion);
        }

        internal void SetLayout(TextStudioVisualGlyph[] glyphs, int count, bool preserveStarted = true)
        {
            _sequencePositions = null;
            _sequenceYDown = false;
            _layoutGlyphs = glyphs;
            _layoutGlyphCount = count;
            if (_plan == null || _totalCount == 0) return;
            if (_mode == RevealMode.ByLetter || HasSpatialSequence)
                RebuildLayoutSchedule(preserveStarted);
        }

        internal void SetSequenceLayout(Vector3[] positions, int count, bool yDown, bool preserveStarted = true)
        {
            _layoutGlyphs = null;
            _sequencePositions = positions;
            _sequenceYDown = yDown;
            _layoutGlyphCount = count;
            if (_plan == null || _totalCount == 0) return;
            RebuildLayoutSchedule(preserveStarted);
        }

        void RebuildLayoutSchedule(bool preserveStarted)
        {
            float nextStart = _revealedCount < _totalCount ? _orderedRevealTimes[_revealedCount] : _completionTime;
            BuildRevealSchedule(_plan);
            BuildHideOrder();
            if (!preserveStarted)
            {
                // A scrub is an absolute evaluation, not a live continuation of the
                // provisional order used before layout became available.
                Array.Clear(_revealedFlags, 0, _totalCount);
                _revealedCount = 0;
                _appearancesSettled = false;
                return;
            }
            if (_revealedCount == 0 || _revealedCount == _totalCount) return;

            // Layout can arrive after text replacement retained noncontiguous letters. Keep
            // those letters ahead of pending units so the count still indexes the next start.
            int started = 0;
            for (int order = 0; order < _totalCount; order++)
            {
                int glyph = _revealSequence[order];
                if (_revealedFlags[glyph]) _unitSequence[started++] = glyph;
            }
            int next = started;
            float firstPending = -1f;
            for (int order = 0; order < _totalCount; order++)
            {
                int glyph = _revealSequence[order];
                if (_revealedFlags[glyph]) continue;
                if (firstPending < 0f) firstPending = _revealTimes[glyph];
                _unitSequence[next++] = glyph;
            }
            float offset = Mathf.Max(_accumulatedTime, nextStart) - Mathf.Max(0f, firstPending);
            for (int order = 0; order < _totalCount; order++)
            {
                int glyph = _unitSequence[order];
                _revealSequence[order] = glyph;
                float time = order < started ? 0f : _revealTimes[glyph] + offset;
                _orderedRevealTimes[order] = time;
                _revealTimes[glyph] = time;
            }
            _completionTime = Mathf.Max(_accumulatedTime, _completionTime + offset);
            _revealProgress = ResolveTimelineProgress(_accumulatedTime);
        }

        void ApplySpatialOrder(int unitCount, in TextStudioSequenceSettings settings, int[] order)
        {
            if (settings.Direction == TextStudioSequenceDirection.Logical || (_layoutGlyphs == null && _sequencePositions == null) || _layoutGlyphCount < _totalCount) return;
            if (_spatialUnits.Length < unitCount)
            {
                int capacity = TextStudioBufferCapacity.ForCount(unitCount);
                _spatialUnits = new int[capacity];
                _unitProjections = new float[capacity];
                _sequenceScratch = new int[capacity];
            }
            Vector2 direction = settings.Direction switch
            {
                TextStudioSequenceDirection.RightToLeft => Vector2.left,
                TextStudioSequenceDirection.TopToBottom => Vector2.down,
                TextStudioSequenceDirection.BottomToTop => Vector2.up,
                TextStudioSequenceDirection.TopLeftToBottomRight => new Vector2(1f, -1f),
                TextStudioSequenceDirection.TopRightToBottomLeft => new Vector2(-1f, -1f),
                TextStudioSequenceDirection.BottomLeftToTopRight => new Vector2(1f, 1f),
                TextStudioSequenceDirection.BottomRightToTopLeft => new Vector2(-1f, 1f),
                _ => Vector2.right
            };
            for (int i = 0; i < unitCount; i++)
            {
                float projection = 0f;
                for (int g = _unitStarts[i]; g < _unitEnds[i]; g++)
                {
                    Vector2 position = _layoutGlyphs != null ? (_layoutGlyphs[g].BoundsMin + _layoutGlyphs[g].BoundsMax) * 0.5f : (Vector2)_sequencePositions[g];
                    if (_sequenceYDown) position.y = -position.y;
                    projection += Vector2.Dot(position, direction);
                }
                _unitProjections[i] = projection / Mathf.Max(1, _unitEnds[i] - _unitStarts[i]);
                _spatialUnits[i] = i;
            }
            _projectionComparer.Projections = _unitProjections;
            Array.Sort(_spatialUnits, 0, unitCount, _projectionComparer);
            for (int i = 0; i < unitCount; i++) _sequenceScratch[i] = _spatialUnits[order[i]];
            Array.Copy(_sequenceScratch, order, unitCount);
        }

        void BuildHideOrder()
        {
            _instantScheduleCount = 0;
            if (_totalCount <= 0) return;
            TextStudioSequenceSettings settings = NormalizeSequence(_hideSequenceSettings);
            int capacity = TextStudioBufferCapacity.ForCount(_totalCount);
            if (_hideGlyphSequence == null || _hideGlyphSequence.Length < _totalCount) _hideGlyphSequence = new int[capacity];
            if (_hideOrderByGlyph == null || _hideOrderByGlyph.Length < _totalCount) _hideOrderByGlyph = new int[capacity];
            _hideUnitCount = _scheduleUnitCount;
            TextStudioSequenceUtility.BuildOrder(_scheduleUnitCount, in settings, _unitSequence, null);
            ApplySpatialOrder(_scheduleUnitCount, in settings, _unitSequence);
            int index = 0;
            for (int order = 0; order < _scheduleUnitCount; order++)
            {
                int unit = _unitSequence[order];
                for (int g = _unitStarts[unit]; g < _unitEnds[unit]; g++)
                {
                    _hideGlyphSequence[index++] = g;
                    _hideOrderByGlyph[g] = order;
                }
            }
        }

        static TextStudioSequenceSettings NormalizeSequence(TextStudioSequenceSettings settings)
        {
            if (settings.Equals(default))
                settings = TextStudioSequenceSettings.Default;
            settings.Spread = Mathf.Max(0f, settings.Spread);
            if (settings.Seed == 0)
                settings.Seed = 1729;
            return settings;
        }

        int GetRevealGlyphAt(int order)
        {
            if (_revealSequence == null || _totalCount <= 0)
                return Mathf.Clamp(order, 0, Mathf.Max(0, _totalCount - 1));
            return _revealSequence[Mathf.Clamp(order, 0, _totalCount - 1)];
        }

        float GetScheduledRevealTime(int glyphIndex)
        {
            if (_revealTimes == null || _totalCount <= 0)
                return 0f;
            return _revealTimes[Mathf.Clamp(glyphIndex, 0, _totalCount - 1)];
        }

        int GetRevealedSourcePrefix()
        {
            if (_revealedFlags == null)
                return 0;
            int prefix = 0;
            while (prefix < _totalCount && _revealedFlags[prefix])
                prefix++;
            return prefix;
        }

        float ResolveTimelineProgress(float elapsed) => _completionTime > 0f
            ? Mathf.Clamp01(elapsed / _completionTime)
            : _revealedCount >= _totalCount ? 1f : 0f;

        internal TextStudioGlyphTransition GetTransition(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= _totalCount || !IsGlyphRevealed(glyphIndex))
                return new TextStudioGlyphTransition(false, AnimationPhase.None, 0f, 1f);
            float coverage = _isDisappearing && _hideStartCoverage != null
                ? _hideStartCoverage[glyphIndex] : GetAppearanceProgress(glyphIndex);
            AnimationPhase phase = GetGlyphPhase(glyphIndex, _appearanceTime);
            return new TextStudioGlyphTransition(phase != AnimationPhase.None, phase, coverage, GetDisappearanceProgress(glyphIndex));
        }

        int GetRevealGroup(TextStudioCompiledPlan plan, int glyphIndex)
        {
            switch (_mode)
            {
                case RevealMode.ByWord:
                    return _wordRevealGroups[glyphIndex];
                case RevealMode.ByLine:
                    return GetLineRevealGroup(plan, glyphIndex);
                case RevealMode.Instant:
                    return 0;
                case RevealMode.ByLetter:
                    return _layoutGlyphs != null && glyphIndex < _layoutGlyphCount && _layoutGlyphs[glyphIndex].ClusterStart >= 0
                        ? _layoutGlyphs[glyphIndex].ClusterStart : glyphIndex;
                case RevealMode.ByRegion:
                    for (int i = 0; i < plan.RegionMapCount; i++)
                    {
                        RegionToGlyphMap region = plan.RegionMaps[i];
                        if (glyphIndex >= region.glyphStart && glyphIndex < region.glyphStart + region.glyphCount)
                            return i;
                    }
                    return -glyphIndex - 1;
                case RevealMode.Custom:
                    if (_groupTimings != null)
                    {
                        TextStudioTimingContext context = new TextStudioTimingContext(plan, glyphIndex, _instantFlags[glyphIndex], _totalCount - 1);
                        return _groupTimings.GetRevealGroup(in context);
                    }
                    return glyphIndex;
                default:
                    return glyphIndex;
            }
        }

        void BuildWordRevealGroups(TextStudioCompiledPlan plan)
        {
            if (_wordRevealGroups == null || _wordRevealGroups.Length < _totalCount)
                _wordRevealGroups = new int[TextStudioBufferCapacity.ForCount(_totalCount)];
            int previousGroup = -1;
            int leadingStart = 0;
            for (int i = 0; i < _totalCount; i++)
            {
                if (plan.IsLineBreakUnit(i))
                {
                    _wordRevealGroups[i] = previousGroup;
                    previousGroup = -(i + 2);
                    leadingStart = i + 1;
                    continue;
                }
                int word = TextSegmenter.GetWordIndexFast(plan.GlyphToWordIndex, i);
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(plan.ResolvedText, plan.GetTextUnit(i).resolvedStart);
                bool punctuation = category >= UnicodeCategory.ConnectorPunctuation && category <= UnicodeCategory.OtherPunctuation;
                if (word >= 0 && !punctuation)
                {
                    if (previousGroup < 0)
                        for (int leading = leadingStart; leading < i; leading++) _wordRevealGroups[leading] = word;
                    previousGroup = word;
                }
                // Reveal punctuation and spacing with the adjacent word, while keeping UAX word ranges intact.
                _wordRevealGroups[i] = previousGroup;
            }
        }

        static int GetLineRevealGroup(TextStudioCompiledPlan plan, int glyphIndex)
        {
            if (glyphIndex > 0 && plan.IsLineBreakUnit(glyphIndex))
                return TextSegmenter.GetLineIndexFast(plan.GlyphToLineIndex, glyphIndex - 1);

            return TextSegmenter.GetLineIndexFast(plan.GlyphToLineIndex, glyphIndex);
        }
    }
}
