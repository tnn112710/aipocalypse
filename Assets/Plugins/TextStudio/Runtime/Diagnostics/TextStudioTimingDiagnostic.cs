namespace TextStudio
{
    public enum TextStudioRevealState
    {
        Idle,
        Revealing,
        Paused,
        LoopWaiting,
        Complete
    }

    /// <summary>Snapshot of reveal timing, active wait state, and estimated completion for diagnostics UI.</summary>
    public readonly struct TextStudioTimingDiagnostic
    {
        public readonly TextStudioRevealState State;
        public readonly int RevealedGlyphCount;
        public readonly int TotalGlyphCount;
        public readonly float RevealProgress;
        public readonly float SpeedMultiplier;
        public readonly float ApproximateDuration;
        public readonly int ActiveActionCount;
        public readonly int ActionMarkerCount;
        public readonly int EffectBindingCount;
        public readonly string Summary;

        /// <summary>Creates a timing snapshot from reveal state, progress, durations, active indices, and wait metadata.</summary>
        public TextStudioTimingDiagnostic(
            TextStudioRevealState state,
            int revealedGlyphCount,
            int totalGlyphCount,
            float revealProgress,
            float speedMultiplier,
            float approximateDuration,
            int activeActionCount,
            int actionMarkerCount,
            int effectBindingCount,
            string summary)
        {
            State = state;
            RevealedGlyphCount = revealedGlyphCount;
            TotalGlyphCount = totalGlyphCount;
            RevealProgress = revealProgress;
            SpeedMultiplier = speedMultiplier;
            ApproximateDuration = approximateDuration;
            ActiveActionCount = activeActionCount;
            ActionMarkerCount = actionMarkerCount;
            EffectBindingCount = effectBindingCount;
            Summary = summary ?? string.Empty;
        }
    }

    /// <summary>Converts runtime reveal snapshots into stable timing diagnostics.</summary>
    public static class TextStudioTimingDiagnostics
    {
        /// <summary>Builds timing status from one immutable reveal snapshot.</summary>
        public static TextStudioTimingDiagnostic FromSnapshot(TextStudioRevealDebugSnapshot snapshot)
        {
            TextStudioRevealState state = ResolveState(snapshot);
            string summary = BuildSummary(snapshot, state);
            return new TextStudioTimingDiagnostic(
                state,
                snapshot.RevealedGlyphCount,
                snapshot.TotalGlyphCount,
                snapshot.RevealProgress,
                snapshot.SpeedMultiplier,
                snapshot.ApproximateDuration,
                snapshot.ActiveActionCount,
                snapshot.ActionMarkerCount,
                snapshot.EffectBindingCount,
                summary);
        }

        static TextStudioRevealState ResolveState(TextStudioRevealDebugSnapshot snapshot)
        {
            if (snapshot.IsPaused)
                return TextStudioRevealState.Paused;
            if (snapshot.IsLoopWaiting)
                return TextStudioRevealState.LoopWaiting;
            if (snapshot.IsRevealing)
                return TextStudioRevealState.Revealing;
            if (snapshot.IsComplete)
                return TextStudioRevealState.Complete;

            return TextStudioRevealState.Idle;
        }

        static string BuildSummary(TextStudioRevealDebugSnapshot snapshot, TextStudioRevealState state)
        {
            return state + " - " +
                   snapshot.RevealedGlyphCount + "/" + snapshot.TotalGlyphCount +
                   " glyphs, " +
                   snapshot.RevealProgress.ToString("P0") +
                   " at " +
                   snapshot.SpeedMultiplier.ToString("0.###") +
                   "x.";
        }
    }
}
