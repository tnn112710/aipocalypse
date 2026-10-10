namespace TextStudio
{
    public sealed partial class TextStudioBinding : ITextStudioAnimationPlayback
    {
        /// <summary>Whether Text Studio-owned presentation time is paused. Native bindings and layout continue to update.</summary>
        public bool IsAnimationPaused => _runtime.IsAnimationPaused;
        /// <summary>Pauses Text Studio-owned presentation time and wakes the driver to apply the pause while layout remains live.</summary>
        public void PauseAnimation() { _runtime.PauseAnimation(); WakeAutomaticDriver(); }
        /// <summary>Resumes paused presentation time and wakes the binding's automatic driver.</summary>
        public void ResumeAnimation() { _runtime.ResumeAnimation(); WakeAutomaticDriver(); }
    }
}
