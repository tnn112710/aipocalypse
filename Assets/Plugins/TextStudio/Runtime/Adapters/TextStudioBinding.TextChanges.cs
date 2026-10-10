namespace TextStudio
{
    public sealed partial class TextStudioBinding : ITextStudioTextChangeHost
    {
        /// <summary>Decimal separator used by Numeric Value matching. Set this to the formatter's NumberDecimalSeparator.</summary>
        public string NumericDecimalSeparator
        {
            get => _runtime.NumericDecimalSeparator;
            set => _runtime.NumericDecimalSeparator = string.IsNullOrEmpty(value) ? "." : value;
        }

        TextStudioRuntimeCore ITextStudioTextChangeHost.TextChangeRuntime => _runtime;
        void ITextStudioTextChangeHost.SetRequestedMarkup(string markup) => _requestedMarkup = markup;
        void ITextStudioTextChangeHost.RestartTextReveal() => StartReveal();
        void ITextStudioTextChangeHost.WakeTextChangeDriver() => WakeAutomaticDriver();

        // The public UI Toolkit backend cannot retain a separate outgoing text mesh.
        // The shared coordinator therefore uses the documented hide-then-reveal fallback.
        bool ITextStudioTextChangeHost.CaptureOutgoingReactions(in TextStudioGlyphChangeSet changes,
            bool append, bool deferUntilCommit) =>
            _runtime.CaptureOutgoingReactions(in changes, append, deferUntilCommit);
        void ITextStudioTextChangeHost.AbortPendingOutgoingReactions() =>
            _runtime.AbortPendingOutgoingReactions();

        void ITextStudioTextChangeHost.CommitTextChange(in TextStudioTextChangeCommit commit) =>
            CommitTextChange(commit.Request.Markup, commit.StartReveal, commit.Request.Deferred,
                commit.Changes, commit.Request.Append, commit.PreserveContinuity,
                commit.ReactionChanges);
    }
}
