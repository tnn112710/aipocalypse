using System;

namespace TextStudio
{
    internal struct TextStudioTextChangeRequest
    {
        internal string Markup;
        internal bool Deferred;
        internal bool RaiseTextSetEvent;
        internal bool RevealOnChange;
        internal bool RestartUnchanged;
        internal bool Append;
        internal bool AppendDiscardsContinuity;
        internal bool CanAnimate;
        internal bool NeedsFrameChangeMap;
        internal bool NeedsReactionChangeMap;
        internal bool NeedsReactionOutgoingCapture;
        internal bool RenderMarkupLiterally;
        internal TextStudioMarkupSyntax MarkupSyntax;
        internal TextStudioTextChangeMode Mode;
        internal TextStudioTextChangeScope Scope;
    }

    internal struct TextStudioTextChangeCommit
    {
        internal TextStudioTextChangeRequest Request;
        internal TextStudioGlyphChangeSet Changes;
        internal TextStudioGlyphChangeSet ReactionChanges;
        internal bool StartReveal;
        internal bool PreserveContinuity;
    }

    internal interface ITextStudioTextChangeHost
    {
        TextStudioRuntimeCore TextChangeRuntime { get; }
        void SetRequestedMarkup(string markup);
        void RestartTextReveal();
        void CommitTextChange(in TextStudioTextChangeCommit commit);
        bool CaptureOutgoingReactions(in TextStudioGlyphChangeSet changes, bool append,
            bool deferUntilCommit);
        void AbortPendingOutgoingReactions();
        void WakeTextChangeDriver();
    }

    // Shared state machine, stored directly in each host. No host callbacks or scene references are retained.
    internal struct TextStudioTextChangeCoordinator
    {
        TextStudioTextChangeCommit _pending;
        internal bool IsPending { get; private set; }
        internal bool PendingReveal { get => _pending.StartReveal; set => _pending.StartReveal = value; }
        internal TextStudioGlyphChangeSet PendingChanges => _pending.Changes;

        internal void Request(ITextStudioTextChangeHost host, in TextStudioTextChangeRequest request)
        {
            TextStudioRuntimeCore runtime = host.TextChangeRuntime;
            string markup = request.Markup ?? string.Empty;
            string currentMarkup = runtime.Markup ?? string.Empty;
            bool hadPending = IsPending;
            if (hadPending)
            {
                host.AbortPendingOutgoingReactions();
                Clear();
            }
            host.SetRequestedMarkup(markup);
            if (string.Equals(markup, currentMarkup, StringComparison.Ordinal))
            {
                if (hadPending)
                    runtime.StopDisappear();
                if (request.RestartUnchanged)
                    host.RestartTextReveal();
                return;
            }

            // Instant reveals discard text-change continuity when the pipeline runs, so an append alone needs no map there.
            bool needsMap = hadPending || request.NeedsReactionChangeMap ||
                request.NeedsReactionOutgoingCapture || (request.CanAnimate &&
                (request.RevealOnChange || request.Append && !request.AppendDiscardsContinuity ||
                 request.NeedsFrameChangeMap));
            TextStudioGlyphChangeSet changes = default;
            TextStudioGlyphChangeSet reactionChanges = default;
            if (needsMap)
            {
                TextStudioCompiledPlan incoming = runtime.CompileTextChangeUnits(markup,
                    request.RenderMarkupLiterally, request.MarkupSyntax);
                // Appending always retains the existing prefix, independently of replacement policy.
                TextStudioTextChangeScope scope = request.Append
                    ? TextStudioTextChangeScope.ChangedGlyphs : request.Scope;
                changes = TextStudioGlyphChangeSetBuilder.Build(runtime.Plan, incoming, scope, runtime.NumericDecimalSeparator);
                if (request.NeedsReactionChangeMap || request.NeedsReactionOutgoingCapture)
                    reactionChanges = scope == TextStudioTextChangeScope.WholeText
                        ? TextStudioGlyphChangeSetBuilder.Build(runtime.Plan, incoming,
                            TextStudioTextChangeScope.ChangedGlyphs, runtime.NumericDecimalSeparator)
                        : changes;
            }
            TextStudioTextChangeCommit commit = new TextStudioTextChangeCommit
            {
                Request = request,
                Changes = changes,
                ReactionChanges = reactionChanges,
                StartReveal = request.RevealOnChange && changes.IsDefined && changes.IncomingChangedGlyphCount > 0,
                PreserveContinuity = request.RevealOnChange || request.Append
            };
            bool animateOutgoing = request.CanAnimate && request.RevealOnChange &&
                request.Mode != TextStudioTextChangeMode.Immediate &&
                currentMarkup.Length > 0 && runtime.TotalGlyphCount > 0 && changes.OutgoingChangedGlyphCount > 0;
            bool reactionCaptured = false;
            if (request.NeedsReactionOutgoingCapture &&
                reactionChanges.IsDefined && reactionChanges.OutgoingChangedGlyphCount > 0)
                reactionCaptured = host.CaptureOutgoingReactions(in reactionChanges, request.Append, animateOutgoing);
            bool captured = animateOutgoing && request.Mode == TextStudioTextChangeMode.OverlapHandoff &&
                reactionCaptured;
            if (!animateOutgoing || captured)
            {
                Clear();
                runtime.StopDisappear();
                host.CommitTextChange(in commit);
                // Explicit ShowText also replays a markup-only change whose glyphs all match.
                if (request.RestartUnchanged && changes.IsDefined &&
                    changes.IncomingChangedGlyphCount == 0 && changes.OutgoingChangedGlyphCount == 0)
                    host.RestartTextReveal();
                return;
            }

            _pending = commit;
            IsPending = true;
            runtime.StopDisappear();
            runtime.StartDisappear(in changes);
            if (!runtime.IsDisappearing)
                Complete(host);
            host.WakeTextChangeDriver();
        }

        internal void Complete(ITextStudioTextChangeHost host)
        {
            if (!IsPending)
                return;
            TextStudioTextChangeCommit commit = _pending;
            commit.PreserveContinuity = true;
            Clear();
            host.CommitTextChange(in commit);
        }

        internal void Cancel(ITextStudioTextChangeHost host, bool restoreCommittedMarkup)
        {
            if (!IsPending)
                return;
            host.AbortPendingOutgoingReactions();
            Clear();
            if (restoreCommittedMarkup)
                host.SetRequestedMarkup(host.TextChangeRuntime.Markup);
        }

        internal void Clear()
        {
            IsPending = false;
            _pending = default;
        }
    }
}
