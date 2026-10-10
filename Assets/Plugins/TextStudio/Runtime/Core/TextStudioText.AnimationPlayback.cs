namespace TextStudio
{
    public sealed partial class TextStudioText : ITextStudioAnimationPlayback
    {
        /// <summary>Whether Text Studio-owned presentation time is paused. Content and layout updates remain available.</summary>
        public bool IsAnimationPaused => _runtime.IsAnimationPaused;
        /// <summary>Freezes effects, reveal, hide, writing, and reaction time without changing authored settings.</summary>
        public void PauseAnimation() => _runtime.PauseAnimation();
        /// <summary>Continues presentation from its current time without catching up elapsed wall time.</summary>
        public void ResumeAnimation() => _runtime.ResumeAnimation();
    }
}
