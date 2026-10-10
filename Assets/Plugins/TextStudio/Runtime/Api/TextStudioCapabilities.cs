using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Focused content contract shared by component and UI Toolkit hosts.
    /// Use this view when a caller owns text content but not playback policy.
    /// </summary>
    public interface ITextStudioContent
    {
        /// <summary>Gets the authored markup, including Text Studio tags.</summary>
        string Markup { get; }

        /// <summary>Gets the visible plain-text content after markup processing.</summary>
        string Text { get; }

        /// <summary>
        /// Gets a transient, allocation-free view of the compiled semantic plan.
        /// Do not retain the view after the current callback or frame.
        /// </summary>
        TextStudioPlanView Plan { get; }

        /// <summary>Replaces the current markup using the host's configured text-change policy.</summary>
        /// <param name="markup">Markup to compile. A null value is treated as empty text.</param>
        void SetText(string markup);

        /// <summary>
        /// Schedules a text replacement for the host's next update, coalescing multiple pending replacements.
        /// </summary>
        /// <param name="markup">Markup to compile. A null value is treated as empty text.</param>
        void SetTextDeferred(string markup);

        /// <summary>Appends markup and recompiles the combined content.</summary>
        /// <param name="markup">Markup to append. Null or empty input leaves the content unchanged.</param>
        void AppendText(string markup);

        /// <summary>Clears authored content, compiled state, and visible output.</summary>
        void ClearText();
    }

    /// <summary>Optional control of Text Studio-owned presentation time. Independent native clocks and external coroutines retain their own timing.</summary>
    public interface ITextStudioAnimationPlayback
    {
        bool IsAnimationPaused { get; }
        /// <summary>Pauses Text Studio-owned presentation time while allowing native bindings and layout to update.</summary>
        void PauseAnimation();
        /// <summary>Resumes Text Studio-owned presentation time from its paused state.</summary>
        void ResumeAnimation();
    }

    /// <summary>Focused reveal and hide playback contract shared by every Text Studio host.</summary>
    public interface ITextStudioPlayback
    {
        /// <summary>Gets normalized reveal progress from 0 (hidden) to 1 (fully revealed).</summary>
        float RevealProgress { get; }

        /// <summary>Gets the number of semantic glyphs currently revealed.</summary>
        int RevealedGlyphCount { get; }

        /// <summary>Gets the number of visible semantic glyphs in the compiled content.</summary>
        int TotalGlyphCount { get; }

        /// <summary>Gets whether reveal playback is currently advancing.</summary>
        bool IsRevealing { get; }

        /// <summary>Gets whether reveal playback is paused without being stopped.</summary>
        bool IsRevealPaused { get; }

        /// <summary>Gets whether reveal is suspended while an action coroutine is active.</summary>
        bool IsWaitingForAction { get; }

        /// <summary>Gets whether hide playback is currently advancing.</summary>
        bool IsDisappearing { get; }

        /// <summary>Occurs after the final reveal delay has completed and the text is fully shown.</summary>
        event Action OnShowComplete;

        /// <summary>Occurs after hide playback has made the complete text invisible.</summary>
        event Action OnDisappearComplete;

        /// <summary>Occurs when reveal playback starts or restarts.</summary>
        event Action OnTypewriterStarted;

        /// <summary>Replaces the content and begins reveal playback using the current settings.</summary>
        /// <param name="markup">Markup to compile and reveal.</param>
        void ShowText(string markup);

        /// <summary>Replaces the content and displays it completely without advancing a reveal clock.</summary>
        /// <param name="markup">Markup to compile and display.</param>
        void ShowTextInstant(string markup);

        /// <summary>Starts reveal playback for the current compiled content.</summary>
        void StartReveal();

        /// <summary>Pauses reveal playback while retaining its current position.</summary>
        void PauseReveal();

        /// <summary>Resumes reveal playback from its paused position.</summary>
        void ResumeReveal();

        /// <summary>
        /// Requests cooperative cancellation of the active wait and resumes reveal.
        /// Custom non-skippable action coroutines may continue running.
        /// </summary>
        void SkipCurrentWait();

        /// <summary>Completes reveal immediately, including remaining reveal events.</summary>
        void SkipReveal();

        /// <summary>Stops reveal at its current visibility without completing it.</summary>
        void StopReveal();

        /// <summary>Starts hide playback for the current content.</summary>
        void StartDisappear();

        /// <summary>Completes hide playback immediately.</summary>
        void SkipDisappear();

        /// <summary>Sets reveal to an explicit normalized position without changing authored content.</summary>
        /// <param name="progress">Requested progress, clamped to the 0 to 1 range.</param>
        void SetRevealProgress(float progress);

        /// <summary>Changes the playback-rate multiplier used by subsequent reveal updates.</summary>
        /// <param name="multiplier">Non-negative speed multiplier, where 1 uses authored timing.</param>
        void SetRevealSpeed(float multiplier);

        /// <summary>
        /// Advances a paused or waiting reveal when possible; otherwise completes the active reveal.
        /// </summary>
        /// <returns><see langword="true"/> when the call changed playback state.</returns>
        bool AdvanceOrSkip();
    }

    /// <summary>
    /// Focused runtime visibility contract. Visibility overrides never change authored markup.
    /// </summary>
    public interface ITextStudioVisibility
    {
        /// <summary>Gets whether any runtime visibility override currently differs from authored visibility.</summary>
        bool HasVisibilityOverrides { get; }

        /// <summary>Overrides visibility for the complete text without modifying its markup.</summary>
        /// <param name="visible">Whether all glyphs should be visible.</param>
        /// <param name="playTransition">Whether configured reveal or hide transitions should be used.</param>
        /// <returns><see langword="true"/> when the override was accepted.</returns>
        bool SetTextVisibility(bool visible, bool playTransition = false);

        /// <summary>Overrides visibility for one semantic word.</summary>
        /// <param name="wordIndex">Zero-based word index in the current compiled plan.</param>
        /// <param name="visible">Whether glyphs in the word should be visible.</param>
        /// <param name="playTransition">Whether configured transitions should be used.</param>
        /// <returns><see langword="false"/> when <paramref name="wordIndex"/> is outside the current plan.</returns>
        bool SetWordVisibility(int wordIndex, bool visible, bool playTransition = false);

        /// <summary>Overrides visibility for one visible glyph.</summary>
        /// <param name="glyphIndex">Zero-based visible-glyph index in the current compiled plan.</param>
        /// <param name="visible">Whether the glyph should be visible.</param>
        /// <param name="playTransition">Whether configured transitions should be used.</param>
        /// <returns><see langword="false"/> when <paramref name="glyphIndex"/> is outside the current plan.</returns>
        bool SetGlyphVisibility(int glyphIndex, bool visible, bool playTransition = false);

        /// <summary>Restores authored visibility for one glyph.</summary>
        /// <param name="glyphIndex">Zero-based visible-glyph index in the current compiled plan.</param>
        /// <returns><see langword="true"/> when an existing override was removed.</returns>
        bool ClearGlyphVisibilityOverride(int glyphIndex);

        /// <summary>Restores authored visibility for every glyph.</summary>
        /// <returns><see langword="true"/> when at least one override was removed.</returns>
        bool ClearVisibilityOverrides();
    }

    /// <summary>
    /// Focused renderer-neutral effect control shared by component and UI Toolkit hosts.
    /// External effects contribute after authored Reveal, Active, and Hide effects without owning
    /// playback or changing authored markup.
    /// </summary>
    public interface ITextStudioEffectControl
    {
        /// <summary>Adds one externally driven effect contribution for the next evaluation.</summary>
        bool ApplyExternalEffect(
            ITextStudioEffectDefinition effect,
            float progress,
            float intensity = 1f,
            TextStudioTextRange range = default);

        /// <summary>Restarts effect-local clocks and state without changing reveal progress.</summary>
        void RestartEffects();

        /// <summary>Re-resolves authored and external effect definitions after their content changes.</summary>
        void RefreshEffectBindings();
    }

    /// <summary>
    /// Advanced text-backend host view. Everyday playback code should not depend on this contract.
    /// </summary>
    public interface ITextStudioBackendHost
    {
        /// <summary>Gets the stable identifier of the backend currently driving output.</summary>
        string ActiveBackendId { get; }

        /// <summary>Gets the backend currently driving output, or <see langword="null"/> before discovery succeeds.</summary>
        ITextStudioTextBackend ActiveBackend { get; }

        /// <summary>Gets the discovered compatible backends in selection order.</summary>
        IReadOnlyList<ITextStudioTextBackend> AvailableBackends { get; }

        /// <summary>Gets whether the active backend currently owns visible output.</summary>
        bool IsDrivingOutput { get; }

        /// <summary>Gets whether Text Studio currently writes directly to its built-in TextMeshPro target.</summary>
        bool IsDrivingBuiltInTextMeshPro { get; }

        /// <summary>Gets the last external text write ignored while Text Studio owned the backend, or an empty string.</summary>
        string LastIgnoredExternalWrite { get; }

        /// <summary>Rediscovers compatible backends and rebuilds output from the current authored state.</summary>
        void Refresh();
    }
}
