namespace TextStudio
{
    /// <summary>
    /// Addon extension point for presentation glyph substitution.
    /// Terminal FX, Mechanical Displays, and custom addons implement this
    /// to substitute visual glyphs while preserving the canonical text model.
    ///
    /// V1 ownership model: single provider per controller instance.
    /// </summary>
    public interface IPresentationGlyphProvider
    {
        /// <summary>
        /// Whether this provider owns final glyph identity. When true, Text Studio skips generic
        /// character substitution and lets the provider supply every visible character.
        /// </summary>
        bool OwnsGlyphIdentity { get; }

        /// <summary>
        /// Process the presentation glyph array in-place.
        /// Called once per frame when the presentation layer is dirty.
        /// </summary>
        /// <param name="glyphs">The glyph array to modify. Length matches visible glyph count.</param>
        /// <param name="glyphCount">Number of valid glyphs in the array.</param>
        /// <param name="deltaTime">Time since last update.</param>
        void UpdatePresentation(PresentationGlyph[] glyphs, int glyphCount, float deltaTime);

        /// <summary>
        /// Returns true if the provider has pending changes that need a presentation update.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Called when the source text changes. Provider should reset its internal state.
        /// </summary>
        void OnTextChanged(int newGlyphCount);
    }
}
