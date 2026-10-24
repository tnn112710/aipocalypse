namespace TextStudio
{
    /// <summary>The leading glyphs a compiled plan kept from the plan with <see cref="BaseSourceVersion"/> through an append.</summary>
    internal readonly struct TextStudioKeptGlyphPrefix
    {
        internal readonly uint BaseSourceVersion;
        internal readonly int GlyphCount;

        TextStudioKeptGlyphPrefix(uint baseSourceVersion, int glyphCount)
        {
            BaseSourceVersion = baseSourceVersion;
            GlyphCount = glyphCount;
        }

        internal bool IsDefined => GlyphCount > 0;

        /// <summary>The prefix the last process of the pipeline kept, or none when it compiled the text in full.</summary>
        internal static TextStudioKeptGlyphPrefix Of(TextStudioPipeline pipeline)
        {
            return pipeline != null && pipeline.LastProcessKeptAppendPrefix && pipeline.AppendKeptGlyphCount > 0
                ? new TextStudioKeptGlyphPrefix(pipeline.AppendBaseSourceVersion, pipeline.AppendKeptGlyphCount)
                : default;
        }
    }
}
