using UnityEngine;

namespace TextStudio
{
    /// <summary>Advance-input provider backed by Unity's built-in <see cref="Input.anyKeyDown"/> state.</summary>
    public sealed class UnityInputManagerTextStudioInputProvider : ITextStudioInputProvider
    {
        /// <inheritdoc/>
        public bool WasAnyAdvancePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown;
#else
            // UI callbacks can release the wait when the legacy backend is disabled.
            // Applications can also register an Input System provider through runtime services.
            return false;
#endif
        }
    }
}
