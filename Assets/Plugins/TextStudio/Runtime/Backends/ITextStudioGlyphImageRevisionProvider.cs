namespace TextStudio
{
    /// <summary>Optional cheap image-content or binding revision. Change before publishing new pixels, palette data or atlas bindings, even when shaping is unchanged. Backends without this interface must change ShapedGlyphRevision when image availability or content changes.</summary>
    public interface ITextStudioGlyphImageRevisionProvider
    {
        long GlyphImageRevision { get; }
    }
}
