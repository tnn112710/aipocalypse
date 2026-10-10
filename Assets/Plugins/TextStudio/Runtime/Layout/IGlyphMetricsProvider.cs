namespace TextStudio
{
    /// <summary>
    /// Backend-neutral source of per-character advance widths and kerning adjustments.
    /// Implemented by renderers that have font metrics available (e.g. the
    /// <c>TextStudio.3D</c> addon feeds a TTF outline provider), and consumed by
    /// <see cref="TextStudioLayout"/> to produce typographically correct layouts without
    /// the base package taking a hard dependency on any particular font format.
    ///
    /// Values are expected in the same unit space <see cref="TextStudioLayout"/> uses for
    /// positions (typically EM-normalized units). TMP-based adapters normally do NOT
    /// supply a metrics provider since TMP's own layout engine handles metrics upstream.
    /// </summary>
    public interface IGlyphMetricsProvider
    {
        /// <summary>
        /// Advance width of a single character. Return 0 when the glyph is missing so the
        /// layout can fall back to a default spacing.
        /// </summary>
        float GetAdvance(char c);

        /// <summary>
        /// Horizontal kerning adjustment between two adjacent characters. Negative values
        /// tighten the pair (e.g. AV, Te). Return 0 when the pair has no kern entry.
        /// </summary>
        float GetKerning(char left, char right);
    }
}
