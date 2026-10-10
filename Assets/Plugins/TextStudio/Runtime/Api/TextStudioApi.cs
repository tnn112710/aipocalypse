using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Public convenience API for common TextStudio operations. Wraps the hub with
    /// simple methods so callers can drive reveal without poking at the full
    /// <see cref="TextStudioText"/> surface area.
    /// </summary>
    public static class TextStudioApi
    {
        /// <summary>Format a number with the shared default and assign it through the content contract.</summary>
        public static void SetNumber(ITextStudioContent content, double value)
        {
            SetNumber(content, value, TextStudioNumberFormat.Default);
        }

        /// <summary>Format a number once and assign it to any Text Studio content host.</summary>
        public static void SetNumber(
            ITextStudioContent content,
            double value,
            TextStudioNumberFormat format)
        {
            if (content == null)
                return;
            content.SetText(format.Format(value));
        }

        /// <summary>Set text on a TextStudio hub and optionally start reveal.</summary>
        public static void ShowText(TextStudioText hub, string text, bool autoReveal = true)
        {
            if (hub == null) return;
            if (autoReveal)
                hub.ShowText(text);
            else
                hub.SetText(text);
        }

        /// <summary>Set text and immediately reveal it (no typewriter animation).</summary>
        public static void SetTextInstant(TextStudioText hub, string text)
        {
            if (hub == null) return;
            hub.ShowTextInstant(text);
        }

        /// <summary>Configure reveal playback in one call.</summary>
        public static void ConfigureReveal(
            TextStudioText hub,
            RevealMode revealMode,
            float speedMultiplier = 1f,
            bool loop = false,
            float loopDelay = 0.75f)
        {
            if (hub == null) return;
            hub.SetRevealMode(revealMode);
            hub.SetRevealSpeed(speedMultiplier);
            hub.SetRevealLoop(loop, loopDelay);
        }

        /// <summary>Set the active placement mode and mark placement state dirty.</summary>
        public static void ConfigurePlacement(TextStudioText hub, PlacementMode placementMode)
        {
            if (hub == null) return;
            hub.PlacementMode = placementMode;
        }

        /// <summary>Return a reveal/debug snapshot suitable for runtime UI or tests.</summary>
        public static TextStudioRevealDebugSnapshot GetRevealSnapshot(TextStudioText hub)
        {
            return hub != null ? hub.GetRevealDebugSnapshot() : default;
        }

        /// <summary>Override one glyph's visibility without changing typewriter progress.</summary>
        public static bool SetGlyphVisibility(
            TextStudioText hub,
            int glyphIndex,
            bool visible,
            bool playTransition = true)
        {
            return hub != null && hub.SetGlyphVisibility(glyphIndex, visible, playTransition);
        }

        /// <summary>Override one compiled word's visibility without changing typewriter progress.</summary>
        public static bool SetWordVisibility(
            TextStudioText hub,
            int wordIndex,
            bool visible,
            bool playTransition = true)
        {
            return hub != null && hub.SetWordVisibility(wordIndex, visible, playTransition);
        }

        /// <summary>Override every glyph's visibility while retaining the current reveal state.</summary>
        public static bool SetTextVisibility(TextStudioText hub, bool visible, bool playTransition = true)
        {
            return hub != null && hub.SetTextVisibility(visible, playTransition);
        }

        /// <summary>Return one glyph to normal reveal-owned visibility.</summary>
        public static bool ClearGlyphVisibilityOverride(TextStudioText hub, int glyphIndex)
        {
            return hub != null && hub.ClearGlyphVisibilityOverride(glyphIndex);
        }

        /// <summary>Return all glyphs to normal reveal-owned visibility.</summary>
        public static bool ClearVisibilityOverrides(TextStudioText hub)
        {
            return hub != null && hub.ClearVisibilityOverrides();
        }

        /// <summary>Enable or disable Reveal, Active, and Hide effect families independently.</summary>
        public static void ConfigureEffectFamilies(TextStudioText hub, bool reveal, bool active, bool hide)
        {
            if (hub == null) return;
            hub.ConfigureEffectFamilies(reveal, active, hide);
        }

        /// <summary>Restart effect time and state without changing text or reveal progress.</summary>
        public static void RestartEffects(TextStudioText hub)
        {
            if (hub == null) return;
            hub.RestartEffects();
        }

        /// <summary>Re-resolve effect bindings after runtime effect registrations change.</summary>
        public static void RefreshEffectBindings(TextStudioText hub)
        {
            if (hub == null) return;
            hub.RefreshEffectBindings();
        }

        /// <summary>Find a TextStudio hub on a GameObject, or add one.</summary>
        public static TextStudioText GetOrAddHub(GameObject go)
        {
            if (go == null) return null;
            TextStudioText hub = go.GetComponent<TextStudioText>();
            if (hub == null) hub = go.AddComponent<TextStudioText>();
            return hub;
        }
    }
}
