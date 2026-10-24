using System;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText : ITextStudioTextChangeHost
    {
        TextStudioTextChangeCoordinator _textChanges;
        TextStudioGlyphChangeSet _latestCommittedGlyphChanges;

        /// <summary>Decimal separator used by Numeric Value matching. Set this to the formatter's NumberDecimalSeparator.</summary>
        public string NumericDecimalSeparator
        {
            get => _runtime.NumericDecimalSeparator;
            set => _runtime.NumericDecimalSeparator = string.IsNullOrEmpty(value) ? "." : value;
        }

        /// <summary>Whether a replacement is waiting for the current hide sequence to finish.</summary>
        public bool IsTextChangePending => _textChanges.IsPending;

        /// <summary>Whether a configured Reaction can attempt an outgoing overlap handoff. A particular capture may still fail.</summary>
        public bool CanOverlapTextChanges
        {
            get
            {
                if (_runtime.ReactionsRequireOutgoingCapture ||
                    !Application.isPlaying &&
                    (_behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions)
                        ? _behaviorProfile.Reactions : _reactions).HasOutgoingCaptureLayer())
                    return true;
                return false;
            }
        }

        void RequestTextChange(
            string markup,
            bool syncTextImmediate,
            bool forceReveal,
            bool preferIncrementalAppend = false,
            bool raiseTextSetEvent = true,
            bool forceImmediate = false,
            TextStudioTextChangeScope? scopeOverride = null,
            bool? restartUnchanged = null)
        {
            using (TextChangeProfilerMarker.Auto())
            {
                RequestTextChangeCore(markup, syncTextImmediate, forceReveal, preferIncrementalAppend, raiseTextSetEvent,
                    forceImmediate, scopeOverride, restartUnchanged);
            }
        }

        void RequestTextChangeCore(
            string markup,
            bool syncTextImmediate,
            bool forceReveal,
            bool preferIncrementalAppend,
            bool raiseTextSetEvent,
            bool forceImmediate,
            TextStudioTextChangeScope? scopeOverride,
            bool? restartUnchanged)
        {
            TextStudioTypewriterSettings settings = ResolveTypewriterSettings();
            if (settings.ResetSpeedOnNewText)
                _runtime.RevealSpeedMultiplier = settings.RevealSpeedMultiplier;
            TextStudioTextChangeRequest request = new TextStudioTextChangeRequest
            {
                Markup = markup ?? string.Empty,
                Deferred = !syncTextImmediate,
                RaiseTextSetEvent = raiseTextSetEvent,
                RevealOnChange = !forceImmediate &&
                    (forceReveal || ShouldStartTypewriterOnTextChanged(settings.StartMode)),
                RestartUnchanged = restartUnchanged ?? forceReveal,
                Append = preferIncrementalAppend,
                AppendDiscardsContinuity = settings.RevealMode == RevealMode.Instant &&
                    _runtime.RevealMode == RevealMode.Instant,
                CanAnimate = !forceImmediate && CanRunTextChangePlayback(),
                NeedsFrameChangeMap = _textChangeLayoutDuration > 0f || (_extensions != null && _extensions.HasActiveFrameComposers),
                NeedsReactionChangeMap = _runtime.ReactionsRequireChangedUnits,
                NeedsReactionOutgoingCapture = _runtime.ReactionsRequireOutgoingCapture,
                RenderMarkupLiterally = IsRenderingRawExternalOutput,
                MarkupSyntax = _markupSyntaxOverride,
                Mode = forceImmediate ? TextStudioTextChangeMode.Immediate : settings.TextChangeMode,
                Scope = scopeOverride ?? settings.TextChangeScope
            };
            _textChanges.Request(this, in request);
        }

        TextStudioRuntimeCore ITextStudioTextChangeHost.TextChangeRuntime => _runtime;
        void ITextStudioTextChangeHost.SetRequestedMarkup(string markup) => SetCanonicalMarkup(markup);
        void ITextStudioTextChangeHost.RestartTextReveal() => StartRevealInternal(resetProgress: true);
        void ITextStudioTextChangeHost.WakeTextChangeDriver() => ScheduleMeshRefresh();
        bool ITextStudioTextChangeHost.CaptureOutgoingReactions(in TextStudioGlyphChangeSet changes,
            bool append, bool deferUntilCommit) =>
            _runtime.CaptureOutgoingReactions(in changes, append, deferUntilCommit);
        void ITextStudioTextChangeHost.AbortPendingOutgoingReactions() =>
            _runtime.AbortPendingOutgoingReactions();
        void ITextStudioTextChangeHost.CommitTextChange(in TextStudioTextChangeCommit commit) =>
            CommitTextChange(commit.Request.Markup, !commit.Request.Deferred, commit.StartReveal,
                commit.Changes, commit.Request.Append, commit.Request.RaiseTextSetEvent,
                commit.PreserveContinuity, commit.ReactionChanges);

        void CommitTextChange(
            string markup,
            bool syncTextImmediate,
            bool revealReplacement,
            TextStudioGlyphChangeSet glyphChanges = default,
            bool preferIncrementalAppend = false,
            bool raiseTextSetEvent = true,
            bool preserveContinuity = false,
            TextStudioGlyphChangeSet reactionChanges = default)
        {
            using (TextChangeCommitProfilerMarker.Auto())
            {
                _latestCommittedGlyphChanges = glyphChanges;
                _layoutInterpolation?.Prepare(in glyphChanges);
                bool deferImmediateProcessing = _runtime.IsProcessingPipeline;
                if (_runtime.IsDisappearing)
                    _runtime.StopDisappear();
                if (preserveContinuity && glyphChanges.IsDefined)
                    _runtime.PrepareTextChangeContinuity(glyphChanges);
                else
                    _runtime.DiscardTextChangeContinuity();
                _runtime.PrepareReactionTextChange(in reactionChanges);
                if (preferIncrementalAppend)
                    _runtime.AppendCanonicalText(markup);
                else
                    _runtime.SetText(markup, raiseTextSetEvent);
                if (syncTextImmediate && !deferImmediateProcessing)
                    SyncTextImmediateForCurrentMode();
                if (revealReplacement)
                    StartTextChangeRevealOrDeferUntilPipelineIsCurrent();
            }
        }

        void CompletePendingTextChange() => _textChanges.Complete(this);

        void CancelPendingTextChange(bool restoreCommittedMarkup) =>
            _textChanges.Cancel(this, restoreCommittedMarkup);

        void ClearPendingTextChangeState() => _textChanges.Cancel(this, false);

        bool CanRunTextChangePlayback()
        {
            return Application.isPlaying ||
                   _authoringMode == TextStudioAuthoringMode.Preview;
        }

    }
}
