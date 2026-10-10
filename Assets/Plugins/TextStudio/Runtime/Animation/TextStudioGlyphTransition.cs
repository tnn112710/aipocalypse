namespace TextStudio
{
    /// <summary>Canonical Typewriter state for one shaped text unit, independent of its renderer.</summary>
    public readonly struct TextStudioGlyphTransition
    {
        public bool Visible { get; }
        public AnimationPhase Phase { get; }
        public float RevealProgress { get; }
        public float HideProgress { get; }

        internal TextStudioGlyphTransition(bool visible, AnimationPhase phase, float revealProgress, float hideProgress)
        {
            Visible = visible;
            Phase = phase;
            RevealProgress = revealProgress;
            HideProgress = hideProgress;
        }

        /// <summary>Drawing coverage for this unit. Hide optionally erases from the coverage at hide start.</summary>
        public float GetWritingProgress(bool eraseOnHide)
        {
            if (!Visible) return 0f;
            return RevealProgress * (eraseOnHide ? 1f - HideProgress : 1f);
        }
    }

    /// <summary>Allocation-free frame view over the owning Typewriter. Valid for the current frame only.</summary>
    public readonly struct TextStudioGlyphTransitionView
    {
        readonly RevealController _controller;
        internal TextStudioGlyphTransitionView(RevealController controller) => _controller = controller;
        /// <summary>Returns the glyph's current transition, or a fully visible persistent state when no controller is attached.</summary>
        public TextStudioGlyphTransition GetGlyph(int glyphIndex) => _controller != null
            ? _controller.GetTransition(glyphIndex)
            : new TextStudioGlyphTransition(true, AnimationPhase.Persistent, 1f, 0f);
    }
}
