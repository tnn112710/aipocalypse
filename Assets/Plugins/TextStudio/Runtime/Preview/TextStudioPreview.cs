using System;
using UnityEngine;

namespace TextStudio
{
    internal interface ITextStudioPreviewHost
    {
        bool LoopReveal { get; }
        TextStudioTypewriterSource TypewriterSource { get; }
        RevealMode RevealMode { get; }
        float RevealProgress { get; }
        int RevealedGlyphCount { get; }
        bool HasActiveRevealAppearance { get; }
        bool HasActiveReveal { get; }
        bool HasCueTimeline { get; }
        float DisappearProgressInternal { get; }

        float GetApproximateShowDuration();
        float GetApproximateVisualShowDuration();
        float GetApproximateHideDuration();
        void StartRevealInternal(bool resetProgress);
        void StartDisappear();
        void StopDisappear();
        void ResetInternal();
        void SetPreviewRevealTimeInternal(float elapsedTime);
        void SetPreviewEffectTimeInternal(float elapsedTime);
        void SetPreviewRevealActiveBlendEnabledInternal(bool enabled);
        void SetDisappearProgressInternal(float progress);
        void PreparePreviewProgressInternal();
        void FlushPreviewVisualStateInternal();
        void SetPreviewRevealLoopSuppressedInternal(bool suppressed);
        void SnapToEndStateInternal();
        void RestartPreviewInvocationInternal();
        void CancelPreviewInvocationInternal();
    }

    [Flags]
    public enum TextStudioPreviewSegment
    {
        None = 0,
        Reveal = 1 << 0,
        Active = 1 << 1,
        Hide = 1 << 2,
        All = Reveal | Active | Hide
    }

    /// <summary>
    /// Preview controller attached to a <see cref="TextStudioText"/> hub. Exposes
    /// Play / Pause / Restart / Scrub / ShowEndState operations that work identically in
    /// edit and play mode. In edit mode the hub drives ticks via EditorApplication.update;
    /// in play mode they come from LateUpdate. Either way, the same knobs apply.
    ///
    /// <para>
    /// <b>End-state preview (edit mode default).</b> While <see cref="ShowEndState"/> is
    /// true the hub auto-snaps reveal to fully revealed every time text or layout
    /// changes, so authors see the final placement + animation while they type. Pressing
    /// Play / Restart / Scrub turns end-state preview off and the timed pipeline takes
    /// over. <see cref="ShowEndStateAgain"/> returns to authoring mode. End-state
    /// preview is ignored in play mode - runtime code is in charge there.
    /// </para>
    /// </summary>
    public sealed class TextStudioPreview
    {
        const TextStudioPreviewSegment DefaultSegments = TextStudioPreviewSegment.Reveal | TextStudioPreviewSegment.Active;
        const float DefaultActiveDuration = 1f;
        const int DefaultActiveDurationMultiplier = 4;

        readonly ITextStudioPreviewHost _hub;

        enum PlaybackPhase
        {
            Revealing,
            Holding,
            Disappearing,
            Complete
        }

        bool _isPlaying;
        float _time;
        float _progress;
        float _holdElapsed;
        bool _pendingLoopRestart;
        bool _startedDisappearThisTick;
        bool _manualHidePlayback;
        bool _droveRevealThisTick;
        enum PlaybackOwner
        {
            EditModePreview,
            PlayModePreview,
            Runtime
        }

        PlaybackOwner _playbackOwner;
        bool _repeat;
        float _playbackSpeed = 1f;
        int _activeDurationMultiplier = DefaultActiveDurationMultiplier;
        PlaybackPhase _playbackPhase = PlaybackPhase.Complete;
        TextStudioPreviewSegment _enabledSegments = DefaultSegments;

        /// <summary>Opaque snapshot of preview playback, scrubbing, segments, and timing settings for later restoration.</summary>
        public readonly struct State
        {
            internal readonly bool IsPlaying;
            internal readonly bool IsScrubbing;
            internal readonly bool ShowEndState;
            internal readonly float Progress;
            internal readonly TextStudioPreviewSegment EnabledSegments;
            internal readonly bool Repeat;
            internal readonly float PlaybackSpeed;
            internal readonly int ActiveDurationMultiplier;

            internal State(
                bool isPlaying,
                bool isScrubbing,
                bool showEndState,
                float progress,
                TextStudioPreviewSegment enabledSegments,
                bool repeat,
                float playbackSpeed,
                int activeDurationMultiplier)
            {
                IsPlaying = isPlaying;
                IsScrubbing = isScrubbing;
                ShowEndState = showEndState;
                Progress = progress;
                EnabledSegments = enabledSegments;
                Repeat = repeat;
                PlaybackSpeed = playbackSpeed;
                ActiveDurationMultiplier = activeDurationMultiplier;
            }
        }

        // When scrubbing, full preview progress is driven directly instead of by the timer.
        bool _scrubOverride;
        float _scrubProgress;

        // True while the hub should snap to its end state on every text/layout rebuild.
        // Default true so authoring shows the final result; cleared as soon as the user
        // engages a timed preview control (Play / Restart / Scrub).
        bool _showEndState = true;

        /// <summary>
        /// Creates a preview controller for the supplied Text Studio component.
        /// In edit mode, construction suppresses authored reveal looping until the
        /// preview returns control to normal authoring.
        /// </summary>
        /// <param name="hub">The component whose compiled text and playback state will be previewed.</param>
        public TextStudioPreview(TextStudioText hub)
            : this((ITextStudioPreviewHost)hub)
        {
        }

        internal TextStudioPreview(ITextStudioPreviewHost hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _repeat = hub.LoopReveal;
            if (!Application.isPlaying)
                SuppressRevealLoopForPreview();
        }

        /// <summary>True when the preview timer is actively advancing.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>Elapsed preview time in seconds.</summary>
        public float Time => _time;

        /// <summary>Duration of the currently selected preview segments, in seconds.</summary>
        public float Duration => GetSelectedTotalDuration();

        /// <summary>Current normalized preview position across the selected reveal, active, and hide segments.</summary>
        public float Progress => _progress;

        /// <summary>True when the preview is in manual scrub mode (progress driven by user).</summary>
        public bool IsScrubbing => _scrubOverride;

        // Runtime-owned, stopped and unscrubbed: every delta and evaluation time passes through and Tick changes nothing.
        internal bool IsRuntimePassThrough =>
            _playbackOwner == PlaybackOwner.Runtime &&
            !_isPlaying &&
            !_scrubOverride &&
            !_startedDisappearThisTick &&
            !_droveRevealThisTick;

        /// <summary>Current full preview scrubber value (0..1). Only meaningful when <see cref="IsScrubbing"/>.</summary>
        public float ScrubProgress => _scrubProgress;

        /// <summary>
        /// True while the hub should auto-snap reveal to the end state on every text or
        /// layout change. Default true; cleared automatically by Play/Restart/Scrub and
        /// re-enabled by <see cref="ShowEndStateAgain"/>.
        /// </summary>
        public bool ShowEndState => _showEndState;

        public TextStudioPreviewSegment EnabledSegments => _enabledSegments;

        /// <summary>Repeat the selected preview lifecycle without changing authored typewriter playback.</summary>
        public bool Repeat
        {
            get => _repeat;
            set
            {
                _repeat = value;
                if (_repeat || !_pendingLoopRestart)
                    return;

                _pendingLoopRestart = false;
                if (HasSegment(TextStudioPreviewSegment.Active) &&
                    !HasSegment(TextStudioPreviewSegment.Hide))
                {
                    _playbackPhase = PlaybackPhase.Holding;
                    _holdElapsed = GetHoldDuration();
                    SetPlaybackProgressFromHold();
                    return;
                }

                _isPlaying = false;
                _playbackPhase = PlaybackPhase.Complete;
                ReleaseRevealLoopSuppression();
            }
        }

        /// <summary>Transient speed applied to the complete preview lifecycle.</summary>
        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = NormalizePlaybackSpeed(value);
        }

        /// <summary>Preview-only multiplier for the sampled Active phase duration.</summary>
        public int ActiveDurationMultiplier
        {
            get => _activeDurationMultiplier;
            set => SetActiveDurationMultiplier(value);
        }

        public float ActiveDuration => DefaultActiveDuration * _activeDurationMultiplier;

        /// <summary>Captures the current playback or scrub position together with preview segments and timing settings.</summary>
        public State CaptureState()
        {
            float progress = _scrubOverride ? _scrubProgress : _progress;
            return new State(
                _isPlaying,
                _scrubOverride,
                _showEndState,
                progress,
                _enabledSegments,
                _repeat,
                _playbackSpeed,
                _activeDurationMultiplier);
        }

        internal bool PrepareForPlayMode()
        {
            // EnteredPlayMode follows scene OnEnable callbacks. An explicit preview
            // request made there already belongs to this play session and must survive.
            if (_playbackOwner != PlaybackOwner.EditModePreview)
                return false;

            _isPlaying = false;
            _scrubOverride = false;
            _showEndState = false;
            _playbackOwner = PlaybackOwner.Runtime;
            _scrubProgress = 0f;
            ResetTimer();
            _playbackPhase = PlaybackPhase.Complete;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            _hub.CancelPreviewInvocationInternal();
            _hub.ResetInternal();
            _hub.SetPreviewEffectTimeInternal(0f);
            if (_hub.RevealMode == RevealMode.Instant && !_hub.HasCueTimeline)
                _hub.SnapToEndStateInternal();
            _hub.SetPreviewRevealActiveBlendEnabledInternal(true);
            ReleaseRevealLoopSuppression();
            return true;
        }

        internal void TakeRuntimePlaybackOwnership()
        {
            if (!Application.isPlaying || _playbackOwner == PlaybackOwner.Runtime)
                return;

            _playbackOwner = PlaybackOwner.Runtime;
            _isPlaying = false;
            _scrubOverride = false;
            _showEndState = false;
            _playbackPhase = PlaybackPhase.Complete;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            // Explicit runtime commands supersede preview timing without resetting
            // reveal progress, so ResumeReveal can continue a paused invocation.
            _hub.SetPreviewRevealActiveBlendEnabledInternal(true);
            ReleaseRevealLoopSuppression();
        }

        /// <summary>Restores a captured preview state, including its playback, scrub, or end-state presentation.</summary>
        public void RestoreState(in State state)
        {
            TakePreviewPlaybackOwnership();
            SetSegments(state.EnabledSegments);
            _repeat = state.Repeat;
            _playbackSpeed = NormalizePlaybackSpeed(state.PlaybackSpeed);
            _activeDurationMultiplier = NormalizeActiveDurationMultiplier(state.ActiveDurationMultiplier);
            if (state.ShowEndState)
            {
                ShowEndStateAgain();
                return;
            }

            if (state.IsScrubbing)
            {
                Scrub(state.Progress);
                return;
            }

            _time = Mathf.Clamp01(state.Progress) * GetSelectedTotalDuration();
            _holdElapsed = 0f;
            _scrubOverride = false;
            _showEndState = false;
            _isPlaying = state.IsPlaying;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            SuppressRevealLoopForPreview();
            _progress = Mathf.Clamp01(state.Progress);
            ApplyNormalizedProgress(_progress);
            if (_isPlaying)
                EnsureRevealPlaybackStartedAtBeginning();
        }

        internal float GetEffectiveDeltaTime(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (_playbackOwner == PlaybackOwner.Runtime || _showEndState)
                return safeDeltaTime;
            if (!_isPlaying || _scrubOverride)
                return 0f;
            return safeDeltaTime * _playbackSpeed;
        }

        internal float GetEffectivePlacementDeltaTime(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (_playbackOwner == PlaybackOwner.Runtime)
                return safeDeltaTime;
            if (_showEndState)
                return safeDeltaTime * _playbackSpeed;
            if (!_isPlaying || _scrubOverride)
                return 0f;
            return safeDeltaTime * _playbackSpeed;
        }

        internal float GetPlacementEvaluationTime(float runtimeTime)
        {
            return _playbackOwner == PlaybackOwner.Runtime || _showEndState ? runtimeTime : _time;
        }

        /// <summary>Determines whether any of the requested lifecycle segments participate in the preview.</summary>
        public bool IsSegmentEnabled(TextStudioPreviewSegment segment)
        {
            return (_enabledSegments & segment) != 0;
        }

        /// <summary>
        /// Replaces the preview lifecycle selection and reconciles an active preview
        /// without changing the component's authored typewriter settings.
        /// </summary>
        public void SetSegments(TextStudioPreviewSegment segments)
        {
            TextStudioPreviewSegment normalized = NormalizeSegments(segments);
            if (_enabledSegments == normalized)
                return;

            TextStudioPreviewSegment previous = _enabledSegments;
            _enabledSegments = normalized;
            ConfigureRevealActiveBlendForCurrentLifecycle();
            if (_scrubOverride)
            {
                _manualHidePlayback = false;
                ApplyScrubProgress();
                return;
            }

            if (!_showEndState)
                ReconcilePlayingSegments(previous);
        }

        /// <summary>
        /// Adds or removes lifecycle segments from the preview selection. Unknown
        /// flag values are ignored and an empty selection is normalized by the preview.
        /// </summary>
        public void SetSegmentEnabled(TextStudioPreviewSegment segment, bool enabled)
        {
            TextStudioPreviewSegment knownSegment = segment & TextStudioPreviewSegment.All;
            if (knownSegment == TextStudioPreviewSegment.None)
                return;

            TextStudioPreviewSegment candidate = enabled
                ? _enabledSegments | knownSegment
                : _enabledSegments & ~knownSegment;

            if (candidate == TextStudioPreviewSegment.None)
                return;

            SetSegments(candidate);
        }

        /// <summary>Start or resume timed preview playback through the selected segments.</summary>
        public void Play()
        {
            TakePreviewPlaybackOwnership();
            bool canResume = !_showEndState &&
                             _playbackPhase != PlaybackPhase.Complete &&
                             (_scrubOverride || !_isPlaying);
            if (canResume)
            {
                _scrubOverride = false;
                _isPlaying = true;
                _pendingLoopRestart = false;
                _startedDisappearThisTick = false;
                _manualHidePlayback = false;
                _droveRevealThisTick = false;
                SuppressRevealLoopForPreview();
                ConfigureRevealActiveBlendForCurrentLifecycle();
                EnsureRevealPlaybackStartedAtBeginning();
                return;
            }

            BeginSelectedPlayback();
        }

        /// <summary>Pause the preview timer without touching progress.</summary>
        public void Pause()
        {
            TakePreviewPlaybackOwnership();
            _isPlaying = false;
        }

        /// <summary>
        /// Fully reset reveal + effects so a replay produces identical output from glyph 0.
        /// Starts playback immediately from the selected preview start.
        /// </summary>
        public void Restart()
        {
            BeginSelectedPlayback();
        }

        /// <summary>Play the hide/disappearance phase once without enabling the Hide preview segment.</summary>
        public void Hide()
        {
            TakePreviewPlaybackOwnership();
            _hub.CancelPreviewInvocationInternal();
            _scrubOverride = false;
            _showEndState = false;
            _isPlaying = true;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = true;
            _droveRevealThisTick = false;
            SuppressRevealLoopForPreview();
            if (_hub.RevealedGlyphCount > 0)
                StartDisappearPlayback(false);
            else
                StartHidePlayback(false);
        }

        /// <summary>Jump full preview progress to a normalized point (0..1). Disables timed playback.</summary>
        public void Scrub(float normalized)
        {
            TakePreviewPlaybackOwnership();
            if (!_scrubOverride)
                _hub.RestartPreviewInvocationInternal();

            _scrubOverride = true;
            _showEndState = false;
            _isPlaying = false;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            SuppressRevealLoopForPreview();
            ConfigureRevealActiveBlendForCurrentLifecycle();
            _scrubProgress = Mathf.Clamp01(normalized);
            _progress = _scrubProgress;
            _time = _scrubProgress * GetSelectedTotalDuration();
            ApplyScrubProgress();
        }

        /// <summary>Cancel scrub mode and hand the reveal back to the timer.</summary>
        public void ClearScrub()
        {
            _scrubOverride = false;
            if (!_isPlaying)
                ReleaseRevealLoopSuppression();
        }

        /// <summary>
        /// Return to authoring mode: snap reveal to fully revealed and re-enable
        /// auto-snap on every subsequent text/layout change. The preview is paused
        /// afterwards so further edits keep showing the end state.
        /// </summary>
        public void ShowEndStateAgain()
        {
            TakePreviewPlaybackOwnership();
            _hub.CancelPreviewInvocationInternal();
            _isPlaying = false;
            _scrubOverride = false;
            _showEndState = true;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _playbackPhase = PlaybackPhase.Complete;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            if (Application.isPlaying)
                ReleaseRevealLoopSuppression();
            else
                SuppressRevealLoopForPreview();
            SnapToEndStateForCurrentLifecycle();
            SetPlaybackProgressFromReveal();
            _hub.FlushPreviewVisualStateInternal();
        }

        /// <summary>
        /// Called by the hub every tick. Advances preview time when playing; otherwise
        /// does nothing so paused/scrubbed state sticks.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            _startedDisappearThisTick = false;
            if (!_isPlaying || _scrubOverride) return;

            float dt = Mathf.Max(0f, deltaTime);
            if (_pendingLoopRestart)
            {
                _pendingLoopRestart = false;
                if (HasSegment(TextStudioPreviewSegment.Hide) && _hub.RevealedGlyphCount > 0)
                {
                    StartDisappearPlayback(true);
                }
                else
                {
                    _time = 0f;
                    _holdElapsed = 0f;
                    SuppressRevealLoopForPreview();
                    StartSelectedPlayback(resetInvocation: true);
                }
            }

            _time += dt;

            switch (_playbackPhase)
            {
                case PlaybackPhase.Revealing:
                    SetPlaybackProgressFromReveal();
                    break;
                case PlaybackPhase.Holding:
                    _holdElapsed += dt;
                    if (HasTimedRevealSegment())
                    {
                        _hub.SetPreviewRevealTimeInternal(GetShowDuration() + _holdElapsed);
                        _droveRevealThisTick = true;
                    }
                    SetPlaybackProgressFromHold();
                    if (_holdElapsed >= GetSelectedHoldDuration())
                        CompleteHoldPlayback();
                    break;
                case PlaybackPhase.Disappearing:
                    SetPlaybackProgressFromDisappear();
                    break;
            }
        }

        /// <summary>Reset timer only (used when text changes; reveal state is reset separately).</summary>
        internal void ResetTimer()
        {
            _time = 0f;
            _progress = 0f;
            _holdElapsed = 0f;
            _startedDisappearThisTick = false;
        }

        /// <summary>
        /// Notification from the hub that reveal just finished. The preview now holds
        /// the visible active state before entering hide when that segment is enabled.
        /// </summary>
        internal void OnRevealComplete()
        {
            if (!_isPlaying)
                return;

            _holdElapsed = 0f;
            if (HasSegment(TextStudioPreviewSegment.Active) || _hub.HasActiveRevealAppearance)
            {
                _playbackPhase = PlaybackPhase.Holding;
                SetPlaybackProgressFromHold();
                return;
            }

            if (HasSegment(TextStudioPreviewSegment.Hide))
            {
                StartDisappearPlayback(false);
                return;
            }

            CompletePlaybackOrLoop();
        }

        internal void OnDisappearComplete()
        {
            if (!_isPlaying)
                return;

            bool manualHidePlayback = _manualHidePlayback;
            _progress = 1f;
            _manualHidePlayback = false;
            if (manualHidePlayback)
            {
                _isPlaying = false;
                _playbackPhase = PlaybackPhase.Complete;
                ReleaseRevealLoopSuppression();
                return;
            }

            CompletePlaybackOrLoop();
        }

        internal void ApplyScrubProgress()
        {
            _time = _scrubProgress * GetSelectedTotalDuration();
            ApplyNormalizedProgress(_scrubProgress);
            _hub.FlushPreviewVisualStateInternal();
        }

        internal bool ConsumeStartedDisappearThisTick()
        {
            bool started = _startedDisappearThisTick;
            _startedDisappearThisTick = false;
            return started;
        }

        internal bool ConsumeDroveRevealThisTick()
        {
            bool droveReveal = _droveRevealThisTick;
            _droveRevealThisTick = false;
            return droveReveal;
        }

        void TakePreviewPlaybackOwnership()
        {
            _playbackOwner = Application.isPlaying ? PlaybackOwner.PlayModePreview : PlaybackOwner.EditModePreview;
        }

        void BeginSelectedPlayback()
        {
            TakePreviewPlaybackOwnership();
            _time = 0f;
            _progress = 0f;
            _holdElapsed = 0f;
            _scrubOverride = false;
            _showEndState = false;
            _isPlaying = true;
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            _manualHidePlayback = false;
            _droveRevealThisTick = false;
            SuppressRevealLoopForPreview();
            ConfigureRevealActiveBlendForCurrentLifecycle();
            StartSelectedPlayback(resetInvocation: true);
        }

        void EnsureRevealPlaybackStartedAtBeginning()
        {
            if (_playbackPhase != PlaybackPhase.Revealing ||
                !HasSegment(TextStudioPreviewSegment.Reveal) ||
                _hub.RevealProgress > 0f)
            {
                return;
            }

            if (HasInstantRevealSegment() || (_hub.RevealMode == RevealMode.Instant && _hub.HasCueTimeline))
            {
                StartInstantRevealPlayback();
                return;
            }

            if (!HasTimedRevealSegment())
            {
                StartPlaybackAfterInstantReveal();
                return;
            }

            _hub.StartRevealInternal(resetProgress: true);
        }

        void StartSelectedPlayback(bool resetInvocation)
        {
            _pendingLoopRestart = false;
            _startedDisappearThisTick = false;
            if (resetInvocation)
                _hub.RestartPreviewInvocationInternal();


            if (HasSegment(TextStudioPreviewSegment.Reveal))
            {
                if (HasInstantRevealSegment() || (_hub.RevealMode == RevealMode.Instant && _hub.HasCueTimeline))
                {
                    StartInstantRevealPlayback();
                    return;
                }

                if (!HasTimedRevealSegment())
                {
                    StartPlaybackAfterInstantReveal();
                    return;
                }

                _playbackPhase = PlaybackPhase.Revealing;
                _hub.StartRevealInternal(resetProgress: true);
                SetPlaybackProgressFromReveal();
                return;
            }

            if (HasSegment(TextStudioPreviewSegment.Active))
            {
                StartHoldPlayback();
                return;
            }

            StartHidePlayback(false);
        }

        void StartInstantRevealPlayback()
        {
            _playbackPhase = PlaybackPhase.Revealing;
            _hub.StartRevealInternal(resetProgress: true);
            if (_playbackPhase == PlaybackPhase.Revealing && !_hub.HasActiveReveal)
                StartPlaybackAfterInstantReveal();
        }

        void StartPlaybackAfterInstantReveal()
        {
            if (HasSegment(TextStudioPreviewSegment.Active))
            {
                StartHoldPlayback();
                return;
            }

            if (HasSegment(TextStudioPreviewSegment.Hide))
            {
                StartHidePlayback(false);
                return;
            }

            SnapToEndStateForCurrentLifecycle();
            _progress = 1f;
            _isPlaying = false;
            _playbackPhase = PlaybackPhase.Complete;
            ReleaseRevealLoopSuppression();
        }

        void PrepareSelectedStartState()
        {
            if (HasSegment(TextStudioPreviewSegment.Reveal))
            {
                _playbackPhase = PlaybackPhase.Complete;
                ResetForCurrentLifecycle();
                return;
            }

            if (HasSegment(TextStudioPreviewSegment.Active))
            {
                StartHoldPlayback();
                _isPlaying = false;
                return;
            }

            StartHidePlayback(false);
            _isPlaying = false;
        }

        void StartHoldPlayback()
        {
            _holdElapsed = 0f;
            _playbackPhase = PlaybackPhase.Holding;
            SnapToEndStateForCurrentLifecycle();
            SetPlaybackProgressFromHold();
        }

        void StartHidePlayback(bool suppressThisTick)
        {
            _holdElapsed = 0f;
            SnapToEndStateForCurrentLifecycle();
            StartDisappearPlayback(suppressThisTick);
        }

        void CompleteHoldPlayback()
        {
            if (HasSegment(TextStudioPreviewSegment.Reveal) && _hub.HasActiveRevealAppearance)
                return;

            if (HasSegment(TextStudioPreviewSegment.Hide))
            {
                StartDisappearPlayback(true);
                return;
            }

            if (HasSegment(TextStudioPreviewSegment.Active) && !_repeat)
            {
                _holdElapsed = GetHoldDuration();
                SetPlaybackProgressFromHold();
                return;
            }

            CompletePlaybackOrLoop();
        }

        void ReconcilePlayingSegments(TextStudioPreviewSegment previous)
        {
            bool hideWasEnabled = (previous & TextStudioPreviewSegment.Hide) != 0;
            bool hideEnabled = HasSegment(TextStudioPreviewSegment.Hide);

            if (_pendingLoopRestart && hideEnabled && !hideWasEnabled && _hub.RevealedGlyphCount > 0)
            {
                _pendingLoopRestart = false;
                StartDisappearPlayback(false);
                return;
            }

            if (_playbackPhase == PlaybackPhase.Holding)
            {
                bool revealWasEnabled = (previous & TextStudioPreviewSegment.Reveal) != 0;
                if (revealWasEnabled && !HasSegment(TextStudioPreviewSegment.Reveal))
                {
                    if (HasSegment(TextStudioPreviewSegment.Active))
                    {
                        StartHoldPlayback();
                        return;
                    }

                    StartHidePlayback(false);
                    return;
                }

                if (!HasSegment(TextStudioPreviewSegment.Active))
                {
                    CompleteHoldPlayback();
                    return;
                }

                if (hideEnabled && !hideWasEnabled && _holdElapsed >= GetHoldDuration())
                {
                    StartDisappearPlayback(false);
                    return;
                }

                SetPlaybackProgressFromHold();
                return;
            }

            if (_playbackPhase == PlaybackPhase.Disappearing && !hideEnabled)
            {
                _hub.StopDisappear();
                if (HasSegment(TextStudioPreviewSegment.Active))
                {
                    StartHoldPlayback();
                    return;
                }
            }

            if (_playbackPhase == PlaybackPhase.Revealing)
            {
                if (!HasSegment(TextStudioPreviewSegment.Reveal))
                {
                    if (HasSegment(TextStudioPreviewSegment.Active))
                    {
                        StartHoldPlayback();
                        return;
                    }

                    StartHidePlayback(false);
                    return;
                }

                SetPlaybackProgressFromReveal();
                return;
            }

            if (_playbackPhase == PlaybackPhase.Disappearing)
                SetPlaybackProgressFromDisappear();
        }

        void CompletePlaybackOrLoop()
        {
            if (_repeat && !_manualHidePlayback)
            {
                _playbackPhase = PlaybackPhase.Complete;
                _pendingLoopRestart = true;
                return;
            }

            _isPlaying = false;
            _playbackPhase = PlaybackPhase.Complete;
            ReleaseRevealLoopSuppression();
        }

        void StartDisappearPlayback(bool suppressThisTick)
        {
            if (_playbackPhase == PlaybackPhase.Disappearing)
                return;

            _playbackPhase = PlaybackPhase.Disappearing;
            _startedDisappearThisTick = suppressThisTick;
            _hub.StartDisappear();
            SetPlaybackProgressFromDisappear();
        }

        void SuppressRevealLoopForPreview()
        {
            _hub.SetPreviewRevealLoopSuppressedInternal(true);
        }

        void ReleaseRevealLoopSuppression()
        {
            _hub.SetPreviewRevealLoopSuppressedInternal(false);
        }

        void ApplyNormalizedProgress(float normalized)
        {
            _hub.PreparePreviewProgressInternal();
            ConfigureRevealActiveBlendForCurrentLifecycle();

            float progress = Mathf.Clamp01(normalized);
            float showDuration = GetSelectedShowDuration();
            float holdDuration = GetSelectedHoldDuration();
            float hideDuration = GetSelectedHideDuration();
            float totalDuration = GetTotalDuration(showDuration, holdDuration, hideDuration);
            float lifecycleTime = progress * totalDuration;
            float time = lifecycleTime;

            if (HasTimedRevealSegment())
            {
                if (time < showDuration ||
                    (!HasSegment(TextStudioPreviewSegment.Active) &&
                     !HasSegment(TextStudioPreviewSegment.Hide) &&
                     progress < 1f))
                {
                    _hub.SetPreviewRevealTimeInternal(Mathf.Min(time, showDuration));
                    _hub.SetPreviewEffectTimeInternal(lifecycleTime);
                    _playbackPhase = PlaybackPhase.Revealing;
                    return;
                }

                _hub.SetPreviewRevealTimeInternal(showDuration);
                time = Mathf.Max(0f, time - showDuration);
            }
            else if (HasInstantRevealSegment())
            {
                if (progress <= 0f)
                {
                    ResetForCurrentLifecycle();
                    _hub.SetPreviewEffectTimeInternal(0f);
                    _playbackPhase = PlaybackPhase.Revealing;
                    return;
                }

                SnapToEndStateForCurrentLifecycle();
            }
            else if (HasSegment(TextStudioPreviewSegment.Reveal))
            {
                _hub.SnapToEndStateInternal();
            }

            if (HasSegment(TextStudioPreviewSegment.Active))
            {
                if (!HasSegment(TextStudioPreviewSegment.Reveal))
                    SnapToEndStateForCurrentLifecycle();

                if (time <= holdDuration || !HasSegment(TextStudioPreviewSegment.Hide))
                {
                    if (HasTimedRevealSegment())
                        _hub.SetPreviewRevealTimeInternal(showDuration + Mathf.Min(time, holdDuration));
                    _hub.SetPreviewEffectTimeInternal(lifecycleTime);
                    _playbackPhase = PlaybackPhase.Holding;
                    _holdElapsed = Mathf.Max(0f, time);
                    return;
                }

                time = Mathf.Max(0f, time - holdDuration);
            }

            if (HasSegment(TextStudioPreviewSegment.Hide))
            {
                float hideProgress = hideDuration > 0f ? time / hideDuration : 1f;
                _hub.SetDisappearProgressInternal(hideProgress);
                _hub.SetPreviewEffectTimeInternal(lifecycleTime);
                _playbackPhase = hideProgress >= 1f ? PlaybackPhase.Complete : PlaybackPhase.Disappearing;
                return;
            }

            SnapToEndStateForCurrentLifecycle();
            _hub.SetPreviewEffectTimeInternal(lifecycleTime);
            _playbackPhase = PlaybackPhase.Complete;
        }

        void SetPlaybackProgressFromReveal()
        {
            float showDuration = GetShowDuration();
            float totalDuration = GetSelectedTotalDuration();
            _progress = totalDuration > 0f ? Mathf.Clamp01(_hub.RevealProgress * showDuration / totalDuration) : 0f;
        }

        void SetPlaybackProgressFromHold()
        {
            float showDuration = HasTimedRevealSegment() ? GetShowDuration() : 0f;
            float revealTailDuration = GetSelectedRevealTailDuration();
            float holdDuration = GetSelectedHoldDuration();
            float totalDuration = GetSelectedTotalDuration();
            if (revealTailDuration > 0f)
            {
                float tailProgress = Mathf.Clamp01(_holdElapsed / revealTailDuration);
                _progress = totalDuration > 0f
                    ? Mathf.Clamp01((showDuration + tailProgress * revealTailDuration) / totalDuration)
                    : 1f;
                return;
            }

            float holdProgress = holdDuration > 0f ? Mathf.Clamp01(_holdElapsed / holdDuration) : 1f;
            _progress = totalDuration > 0f ? Mathf.Clamp01((showDuration + holdProgress * holdDuration) / totalDuration) : 1f;
        }

        void SetPlaybackProgressFromDisappear()
        {
            float showDuration = GetSelectedShowDuration();
            float holdDuration = GetSelectedHoldDuration();
            float hideDuration = GetSelectedHideDuration();
            float totalDuration = GetSelectedTotalDuration();
            _progress = totalDuration > 0f
                ? Mathf.Clamp01((showDuration + holdDuration + _hub.DisappearProgressInternal * hideDuration) / totalDuration)
                : 1f;
        }

        float GetSelectedShowDuration()
        {
            return HasTimedRevealSegment()
                ? GetShowDuration() + GetSelectedRevealTailDuration()
                : 0f;
        }

        float GetSelectedRevealTailDuration()
        {
            if (!HasTimedRevealSegment() ||
                HasSegment(TextStudioPreviewSegment.Active))
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                _hub.GetApproximateVisualShowDuration() - GetShowDuration());
        }

        float GetSelectedHoldDuration()
        {
            return HasSegment(TextStudioPreviewSegment.Active) ? GetHoldDuration() : 0f;
        }

        float GetSelectedHideDuration()
        {
            return HasSegment(TextStudioPreviewSegment.Hide) || _manualHidePlayback ? GetHideDuration() : 0f;
        }

        float GetSelectedTotalDuration()
        {
            return GetTotalDuration(GetSelectedShowDuration(), GetSelectedHoldDuration(), GetSelectedHideDuration());
        }

        float GetShowDuration()
        {
            return Mathf.Max(0f, _hub.GetApproximateShowDuration());
        }

        float GetHoldDuration()
        {
            return ActiveDuration;
        }

        float GetHideDuration()
        {
            return Mathf.Max(0.01f, _hub.GetApproximateHideDuration());
        }

        static float GetTotalDuration(float showDuration, float holdDuration, float hideDuration)
        {
            return Mathf.Max(0.01f, showDuration + holdDuration + hideDuration);
        }

        bool HasSegment(TextStudioPreviewSegment segment)
        {
            return (_enabledSegments & segment) != 0;
        }

        bool HasTimedRevealSegment()
        {
            return HasSegment(TextStudioPreviewSegment.Reveal) &&
                   _hub.TypewriterSource != TextStudioTypewriterSource.None &&
                   _hub.RevealMode != RevealMode.Instant;
        }

        bool HasInstantRevealSegment()
        {
            return HasSegment(TextStudioPreviewSegment.Reveal) &&
                   _hub.TypewriterSource != TextStudioTypewriterSource.None &&
                   _hub.RevealMode == RevealMode.Instant;
        }

        void ConfigureRevealActiveBlendForCurrentLifecycle()
        {
            _hub.SetPreviewRevealActiveBlendEnabledInternal(
                !_showEndState && HasTimedRevealSegment());
        }

        internal void ConfigureRevealActiveBlendForCurrentLifecycleInternal()
        {
            ConfigureRevealActiveBlendForCurrentLifecycle();
        }

        void ResetForCurrentLifecycle()
        {
            _hub.ResetInternal();
            ConfigureRevealActiveBlendForCurrentLifecycle();
        }

        void SnapToEndStateForCurrentLifecycle()
        {
            _hub.SnapToEndStateInternal();
            ConfigureRevealActiveBlendForCurrentLifecycle();
        }

        static TextStudioPreviewSegment NormalizeSegments(TextStudioPreviewSegment segments)
        {
            TextStudioPreviewSegment normalized = segments & TextStudioPreviewSegment.All;
            return normalized == TextStudioPreviewSegment.None
                ? DefaultSegments
                : normalized;
        }

        void SetActiveDurationMultiplier(int multiplier)
        {
            int normalized = NormalizeActiveDurationMultiplier(multiplier);
            if (_activeDurationMultiplier == normalized)
                return;

            float progress = _scrubOverride ? _scrubProgress : _progress;
            _activeDurationMultiplier = normalized;
            if (_scrubOverride)
                ApplyScrubProgress();
            else if (!_showEndState && !_isPlaying)
                ApplyNormalizedProgress(progress);
        }

        static int NormalizeActiveDurationMultiplier(int multiplier)
        {
            return multiplier switch
            {
                1 => 1,
                2 => 2,
                4 => 4,
                8 => 8,
                _ => DefaultActiveDurationMultiplier
            };
        }

        static float NormalizePlaybackSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
                return 1f;
            return Mathf.Clamp(speed, 0.01f, 10f);
        }
    }
}
