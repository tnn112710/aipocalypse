using System;
using UnityEngine;

namespace TextStudio
{
    internal interface ITextStudioRevealClockOwner
    {
        bool ControlsRevealClock(TextStudioText target);
    }

    public sealed partial class TextStudioText : ITextStudioPreviewHost
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Reveal / typewriter
        // ─────────────────────────────────────────────────────────────────────

        public void StartReveal()
        {
            RecordInitialRevealRequest(true);
            _preview?.TakeRuntimePlaybackOwnership();
            if (_textChanges.IsPending)
            {
                _textChanges.PendingReveal = true;
                return;
            }
            StartRevealInternal(resetProgress: true);
        }

        /// <summary>Starts reveal from the current progress instead of resetting to the beginning.</summary>
        public void ContinueReveal()
        {
            _preview?.TakeRuntimePlaybackOwnership();
            _runtime.ContinueTextChangeReveal();
        }

        public void SkipReveal()
        {
            _runtime.SkipReveal();
            FlushVisualStateImmediate();
        }

        public bool AdvanceOrSkip() => _runtime.AdvanceOrSkip();
        public void SetRevealSpeed(float multiplier)
        {
            if (!EnsureInlineTypewriterSource())
                return;
            _revealSpeedMultiplier = Mathf.Max(0.01f, multiplier);
            _runtime.RevealSpeedMultiplier = Mathf.Max(0.01f, multiplier);
        }
        public void PauseReveal() => _runtime.PauseReveal();

        public void ResumeReveal()
        {
            _preview?.TakeRuntimePlaybackOwnership();
            _runtime.ResumeReveal();
        }

        public void SkipCurrentWait() => _runtime.SkipCurrentWait();
        public void TriggerRemainingEvents() => _runtime.TriggerRemainingEvents();
        public void NotifyTypewriterWaitStarted(ActionMarker marker, TextStudioTypewriterWaitKind kind, float duration, bool isHide)
            => _runtime.NotifyTypewriterWaitStarted(marker, kind, duration, isHide);
        public void NotifyTypewriterWaitFinished(ActionMarker marker, TextStudioTypewriterWaitKind kind, float duration, bool isHide)
            => _runtime.NotifyTypewriterWaitFinished(marker, kind, duration, isHide);

        /// <summary>Selects whether visibility changes are immediate or animated.</summary>
        public void SetRevealMode(RevealMode mode) => RevealMode = mode;
        /// <summary>Sets the per-glyph appearance transition duration in seconds.</summary>
        public void SetRevealAppearanceDuration(float seconds) => RevealAppearanceDuration = seconds;
        /// <summary>Sets the per-glyph disappearance transition duration in seconds.</summary>
        public void SetRevealDisappearanceDuration(float seconds) => RevealDisappearanceDuration = seconds;
        /// <summary>Controls whether reveal restarts after completion and configures the delay before restarting.</summary>
        public void SetRevealLoop(bool loop, float delay = 0.75f)
        {
            LoopReveal = loop;
            LoopRevealDelay = delay;
        }

        /// <summary>Maps a count of started glyphs to the shared timeline. Grouped letters start together; the full count resolves to the completed reveal endpoint.</summary>
        public float GetRevealProgressForGlyphCount(int glyphCount)
        {
            return _runtime.GetRevealProgressForGlyphCount(glyphCount);
        }

        ITextStudioRevealClockOwner _externalRevealClockOwner;

        bool HasExternalRevealClockOwner
        {
            get
            {
                if (_externalRevealClockOwner != null && !_externalRevealClockOwner.ControlsRevealClock(this))
                    _externalRevealClockOwner = null;
                return _externalRevealClockOwner != null;
            }
        }

        internal void SampleExternalRevealProgress(ITextStudioRevealClockOwner owner, float progress)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            _externalRevealClockOwner = owner;
            SetRevealProgress(progress);
        }

        internal void ReleaseExternalRevealClock(ITextStudioRevealClockOwner owner)
        {
            if (ReferenceEquals(_externalRevealClockOwner, owner)) _externalRevealClockOwner = null;
        }

        public void SetRevealProgress(float progress)
        {
            _presentationTimeDiscontinuous = true;
            _runtime.SetRevealProgress(progress);
        }

        /// <summary>Applies externally driven normalized reveal progress without advancing the internal reveal clock.</summary>
        public void DriveRevealProgress(float progress)
        {
            _presentationTimeDiscontinuous = true;
            _runtime.DriveRevealProgress(progress);
        }

        public bool SetGlyphVisibility(int glyphIndex, bool visible, bool playTransition = true)
        {
            return _runtime.SetGlyphVisibility(glyphIndex, visible, playTransition);
        }

        public bool SetWordVisibility(int wordIndex, bool visible, bool playTransition = true)
        {
            return _runtime.SetWordVisibility(wordIndex, visible, playTransition);
        }

        public bool SetTextVisibility(bool visible, bool playTransition = true)
        {
            return _runtime.SetTextVisibility(visible, playTransition);
        }

        public bool ClearGlyphVisibilityOverride(int glyphIndex)
        {
            return _runtime.ClearGlyphVisibilityOverride(glyphIndex);
        }

        public bool ClearVisibilityOverrides()
        {
            return _runtime.ClearVisibilityOverrides();
        }

        /// <summary>Enables or disables evaluation of reveal, active, and hide effect families.</summary>
        public void ConfigureEffectFamilies(bool reveal, bool active, bool hide)
        {
            _runtime.ConfigureEffectFamilies(reveal, active, hide);
        }

        /// <summary>Resets effect-local clocks and state while preserving text and reveal progress.</summary>
        public void RestartEffects()
        {
            _runtime.RestartEffects();
        }

        /// <summary>Re-resolves effect definitions and parameters after registry or asset changes.</summary>
        public void RefreshEffectBindings()
        {
            _runtime.RefreshEffectBindings();
        }

        public void StopReveal() => _runtime.StopShowing();
        void ITypewriterProvider.SkipTypewriter() => _runtime.SkipTypewriter();
        void ITypewriterProvider.StartShowing() => StartReveal();
        void ITypewriterProvider.StopShowing() => StopReveal();
        void ITypewriterProvider.SetTypewriterSpeed(float multiplier) => SetRevealSpeed(multiplier);
        public float GetApproximateShowDuration() => _runtime.GetApproximateShowDuration();

        internal float GetApproximateVisualShowDuration() =>
            _runtime.GetApproximateVisualShowDuration();
        public void StartDisappear() => _runtime.StartDisappear();
        public void SkipDisappear() => _runtime.SkipDisappear();
        public void StopDisappear()
        {
            bool hadPendingTextChange = _textChanges.IsPending;
            CancelPendingTextChange(true);
            _runtime.StopDisappear();
            if (hadPendingTextChange)
                SyncTextImmediateForCurrentMode();
        }
        public float GetApproximateHideDuration() => _runtime.GetApproximateHideDuration();

        /// <summary>
        /// Adds an externally driven effect layer for the current frame. Submit it every frame
        /// while active; normal effects are evaluated first and remain intact on untouched channels.
        /// </summary>
        public bool ApplyExternalEffect(
            ITextStudioEffectDefinition effect,
            float progress,
            float intensity = 1f,
            TextStudioTextRange range = default)
        {
            return _runtime.ApplyExternalEffect(effect, progress, intensity, range);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Preview bridges
        // ─────────────────────────────────────────────────────────────────────

        internal void StartRevealInternal(bool resetProgress)
        {
            using (RevealStartProfilerMarker.Auto())
                _runtime.StartReveal(resetProgress);
            if (resetProgress)
                FlushVisualStateImmediate();
        }

        void StartTextChangeRevealOrDeferUntilPipelineIsCurrent()
        {
            if (_runtime.IsProcessingPipeline || _runtime.HasDeferredTextOrStyleDirty)
            {
                _startRevealAfterDeferredPipeline = true;
                return;
            }

            _runtime.StartTextChangeReveal();
            FlushVisualStateImmediate();
        }

        internal void ResetInternal()
        {
            ClearPendingTextChangeState();
            _runtime.ResetReveal();
        }

        internal void SetRevealProgressInternal(float progress)
        {
            _presentationTimeDiscontinuous = true;
            _runtime.SetRevealProgress(progress);
        }

        internal void SetPreviewRevealTimeInternal(float elapsedTime)
        {
            _presentationTimeDiscontinuous |= Preview.IsScrubbing;
            _runtime.SetPreviewRevealTime(elapsedTime);
        }

        internal void SetPreviewEffectTimeInternal(float elapsedTime)
        {
            _presentationTimeDiscontinuous |= Preview.IsScrubbing;
            _runtime.SetPreviewEffectTime(elapsedTime);
        }

        internal void SetPreviewRevealActiveBlendEnabledInternal(bool enabled)
        {
            _runtime.SetPreviewRevealActiveBlendEnabled(enabled);
        }

        internal void SetDisappearProgressInternal(float progress)
        {
            _runtime.SetDisappearProgress(progress);
        }

        internal float DisappearProgressInternal => _runtime.DisappearProgress;

        internal void PreparePreviewProgressInternal()
        {
            _runtime.PrepareRevealForProgress();
        }

        internal void FlushPreviewVisualStateInternal()
        {
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
            FlushVisualStateImmediate();
        }

        internal void SetPreviewRevealLoopSuppressedInternal(bool suppressed)
        {
            _runtime.SetPreviewRevealLoopSuppressed(suppressed);
        }

        internal void SnapToEndStateInternal()
        {
            _runtime.SnapToEndState();
        }

        internal void RestartPreviewInvocationInternal()
        {
            ClearPendingTextChangeState();
            _runtime.RestartPreviewInvocation();
            _characterSubstitutionPresenter.OnTextChanged(_runtime.Plan);
            _presentationTimeDiscontinuous = true;
            NotifyRenderersRevealReset();
        }

        internal void CancelPreviewInvocationInternal()
        {
            _runtime.CancelPreviewInvocation();
        }

        bool ITextStudioPreviewHost.LoopReveal => LoopReveal;
        TextStudioTypewriterSource ITextStudioPreviewHost.TypewriterSource => TypewriterSource;
        RevealMode ITextStudioPreviewHost.RevealMode => RevealMode;
        float ITextStudioPreviewHost.RevealProgress => RevealProgress;
        int ITextStudioPreviewHost.RevealedGlyphCount => RevealedGlyphCount;
        bool ITextStudioPreviewHost.HasCueTimeline => _runtime.HasCueTimeline;
        bool ITextStudioPreviewHost.HasActiveReveal => _runtime.Reveal.HasActiveReveal;
        bool ITextStudioPreviewHost.HasActiveRevealAppearance => HasActiveRevealAppearance;
        float ITextStudioPreviewHost.DisappearProgressInternal => DisappearProgressInternal;
        float ITextStudioPreviewHost.GetApproximateShowDuration() => GetApproximateShowDuration();
        float ITextStudioPreviewHost.GetApproximateVisualShowDuration() => GetApproximateVisualShowDuration();
        float ITextStudioPreviewHost.GetApproximateHideDuration() => GetApproximateHideDuration();
        void ITextStudioPreviewHost.StartRevealInternal(bool resetProgress) => StartRevealInternal(resetProgress);
        void ITextStudioPreviewHost.StartDisappear() => StartDisappear();
        void ITextStudioPreviewHost.StopDisappear() => StopDisappear();
        void ITextStudioPreviewHost.ResetInternal() => ResetInternal();
        void ITextStudioPreviewHost.SetPreviewRevealTimeInternal(float elapsedTime) =>
            SetPreviewRevealTimeInternal(elapsedTime);
        void ITextStudioPreviewHost.SetPreviewEffectTimeInternal(float elapsedTime) =>
            SetPreviewEffectTimeInternal(elapsedTime);
        void ITextStudioPreviewHost.SetPreviewRevealActiveBlendEnabledInternal(bool enabled) =>
            SetPreviewRevealActiveBlendEnabledInternal(enabled);
        void ITextStudioPreviewHost.SetDisappearProgressInternal(float progress) =>
            SetDisappearProgressInternal(progress);
        void ITextStudioPreviewHost.PreparePreviewProgressInternal() => PreparePreviewProgressInternal();
        void ITextStudioPreviewHost.FlushPreviewVisualStateInternal() => FlushPreviewVisualStateInternal();
        void ITextStudioPreviewHost.SetPreviewRevealLoopSuppressedInternal(bool suppressed) =>
            SetPreviewRevealLoopSuppressedInternal(suppressed);
        void ITextStudioPreviewHost.SnapToEndStateInternal() => SnapToEndStateInternal();
        void ITextStudioPreviewHost.RestartPreviewInvocationInternal() => RestartPreviewInvocationInternal();
        void ITextStudioPreviewHost.CancelPreviewInvocationInternal() => CancelPreviewInvocationInternal();
        void ITextStudioPreviewHost.PreviewTookPlaybackInternal() => Unpark();
    }
}
