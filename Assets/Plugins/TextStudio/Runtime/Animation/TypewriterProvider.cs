using System;

namespace TextStudio
{
    /// <summary>
    /// Compatibility contract for components that drive reveal and hide sequences through a typewriter-style API.
    /// </summary>
    public interface ITypewriterProvider
    {
        /// <summary>Replaces the current content and begins reveal playback.</summary>
        /// <param name="text">Markup to compile and reveal.</param>
        void ShowText(string text);

        /// <summary>Completes reveal immediately and dispatches remaining reveal events.</summary>
        void SkipTypewriter();

        /// <summary>Starts reveal playback for the current content.</summary>
        void StartShowing();

        /// <summary>Stops reveal at its current visibility.</summary>
        void StopShowing();

        /// <summary>Changes reveal timing without modifying authored timing assets.</summary>
        /// <param name="multiplier">Non-negative multiplier, where 1 uses authored timing.</param>
        void SetTypewriterSpeed(float multiplier);

        /// <summary>Dispatches pending marker and cue events without advancing the reveal animation.</summary>
        void TriggerRemainingEvents();

        /// <summary>Estimates reveal duration using the current content and timing configuration.</summary>
        /// <returns>Approximate duration in seconds, including configured waits that have known durations.</returns>
        float GetApproximateShowDuration();

        /// <summary>Starts hide playback for the current content.</summary>
        void StartDisappear();

        /// <summary>Completes hide playback immediately.</summary>
        void SkipDisappear();

        /// <summary>Stops hide playback at its current visibility.</summary>
        void StopDisappear();

        /// <summary>Estimates hide duration using the current content and timing configuration.</summary>
        /// <returns>Approximate duration in seconds.</returns>
        float GetApproximateHideDuration();

        /// <summary>Gets whether reveal playback is currently active.</summary>
        bool IsShowingText { get; }

        /// <summary>Gets whether hide playback is currently active.</summary>
        bool IsDisappearing { get; }

        /// <summary>Occurs when reveal playback starts or restarts.</summary>
        event Action OnTypewriterStarted;

        /// <summary>Occurs after reveal and its final configured delay complete.</summary>
        event Action OnShowComplete;

        /// <summary>Occurs after hide playback makes the complete text invisible.</summary>
        event Action OnDisappearComplete;
    }

}
