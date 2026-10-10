namespace TextStudio
{
    /// <summary>
    /// Immutable reveal/timing snapshot for inspectors, diagnostics, and integration
    /// health checks. Reading this type does not mutate the text pipeline.
    /// </summary>
    public readonly struct TextStudioRevealDebugSnapshot
    {
        public readonly bool HasPlan;
        public readonly string Markup;
        public readonly string ResolvedText;
        public readonly RevealMode RevealMode;
        public readonly PlacementMode PlacementMode;
        public readonly int RevealedGlyphCount;
        public readonly int TotalGlyphCount;
        public readonly float RevealProgress;
        public readonly float SpeedMultiplier;
        public readonly float ApproximateDuration;
        public readonly bool IsRevealing;
        public readonly bool IsComplete;
        public readonly bool IsPaused;
        public readonly bool IsLoopWaiting;
        public readonly int ActiveActionCount;
        public readonly int ActionMarkerCount;
        public readonly int EffectBindingCount;
        public readonly int RendererCount;
        public readonly bool HasExternalTextSource;
        public readonly int ExternalTextSourceCount;
        public readonly int WordCount;
        public readonly bool HasVisibilityOverrides;
        public readonly bool RevealEffectsEnabled;
        public readonly bool ActiveEffectsEnabled;
        public readonly bool HideEffectsEnabled;

        /// <summary>
        /// Creates a diagnostic snapshot from the current compiled plan, playback,
        /// renderer, and integration state. String arguments are normalized to empty
        /// strings so consumers do not need null checks.
        /// </summary>
        public TextStudioRevealDebugSnapshot(
            bool hasPlan,
            string markup,
            string resolvedText,
            RevealMode revealMode,
            PlacementMode placementMode,
            int revealedGlyphCount,
            int totalGlyphCount,
            float revealProgress,
            float speedMultiplier,
            float approximateDuration,
            bool isRevealing,
            bool isComplete,
            bool isPaused,
            bool isLoopWaiting,
            int activeActionCount,
            int actionMarkerCount,
            int effectBindingCount,
            int rendererCount,
            bool hasExternalTextSource,
            int externalTextSourceCount,
            int wordCount = 0,
            bool hasVisibilityOverrides = false,
            bool revealEffectsEnabled = true,
            bool activeEffectsEnabled = true,
            bool hideEffectsEnabled = true)
        {
            HasPlan = hasPlan;
            Markup = markup ?? string.Empty;
            ResolvedText = resolvedText ?? string.Empty;
            RevealMode = revealMode;
            PlacementMode = placementMode;
            RevealedGlyphCount = revealedGlyphCount;
            TotalGlyphCount = totalGlyphCount;
            RevealProgress = revealProgress;
            SpeedMultiplier = speedMultiplier;
            ApproximateDuration = approximateDuration;
            IsRevealing = isRevealing;
            IsComplete = isComplete;
            IsPaused = isPaused;
            IsLoopWaiting = isLoopWaiting;
            ActiveActionCount = activeActionCount;
            ActionMarkerCount = actionMarkerCount;
            EffectBindingCount = effectBindingCount;
            RendererCount = rendererCount;
            HasExternalTextSource = hasExternalTextSource;
            ExternalTextSourceCount = externalTextSourceCount;
            WordCount = wordCount;
            HasVisibilityOverrides = hasVisibilityOverrides;
            RevealEffectsEnabled = revealEffectsEnabled;
            ActiveEffectsEnabled = activeEffectsEnabled;
            HideEffectsEnabled = hideEffectsEnabled;
        }
    }
}
