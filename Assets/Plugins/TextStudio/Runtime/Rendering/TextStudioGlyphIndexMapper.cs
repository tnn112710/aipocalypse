namespace TextStudio
{
    internal enum TextStudioGlyphIndexMappingMode
    {
        SkipUnrenderedWhitespace,
        IncludeRenderedWhitespace,
        MixedRenderedWhitespace
    }

    internal static class TextStudioGlyphIndexMapper
    {
        public static TextStudioGlyphIndexMappingMode ResolveMappingMode(string resolvedText, int stateCount, int renderedQuadCount)
        {
            int glyphCount = GetBoundedGlyphCount(resolvedText, stateCount);
            int nonWhitespaceGlyphs = CountNonWhitespaceGlyphs(resolvedText, stateCount);
            if (renderedQuadCount <= nonWhitespaceGlyphs)
                return TextStudioGlyphIndexMappingMode.SkipUnrenderedWhitespace;

            return renderedQuadCount >= glyphCount
                ? TextStudioGlyphIndexMappingMode.IncludeRenderedWhitespace
                : TextStudioGlyphIndexMappingMode.MixedRenderedWhitespace;
        }

        public static bool TryGetNextGlyphIndex(
            string resolvedText,
            ref int searchIndex,
            int stateCount,
            TextStudioGlyphIndexMappingMode mode,
            out int glyphIndex)
        {
            return mode == TextStudioGlyphIndexMappingMode.SkipUnrenderedWhitespace
                ? TryGetNextRenderedGlyphIndex(resolvedText, ref searchIndex, stateCount, out glyphIndex)
                : TryGetNextGlyphIndexIncludingWhitespace(resolvedText, ref searchIndex, stateCount, out glyphIndex);
        }

        public static bool TryGetNextGlyphIndex(
            string resolvedText,
            ref int searchIndex,
            int stateCount,
            TextStudioGlyphIndexMappingMode mode,
            bool startsNewVisualLine,
            out int glyphIndex)
        {
            if (startsNewVisualLine && mode == TextStudioGlyphIndexMappingMode.MixedRenderedWhitespace)
                TryConsumeOmittedSoftWrapWhitespace(resolvedText, ref searchIndex, stateCount);

            return TryGetNextGlyphIndex(resolvedText, ref searchIndex, stateCount, mode, out glyphIndex);
        }

        public static bool TryGetNextRenderedGlyphIndex(string resolvedText, ref int searchIndex, int stateCount, out int glyphIndex)
        {
            glyphIndex = -1;
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return false;

            int count = stateCount < resolvedText.Length ? stateCount : resolvedText.Length;
            int index = searchIndex < 0 ? 0 : searchIndex;
            while (index < count && char.IsWhiteSpace(resolvedText[index]))
                index++;

            if (index >= count)
            {
                searchIndex = count;
                return false;
            }

            glyphIndex = index;
            searchIndex = index + 1;
            return true;
        }

        public static bool TryConsumeUnrenderedGlyph(
            string resolvedText,
            ref int searchIndex,
            int stateCount,
            TextStudioGlyphIndexMappingMode mode)
        {
            return mode == TextStudioGlyphIndexMappingMode.SkipUnrenderedWhitespace
                ? TryConsumeNextWhitespaceGlyph(resolvedText, ref searchIndex, stateCount)
                : TryConsumeNextGlyph(resolvedText, ref searchIndex, stateCount);
        }

        public static bool TryConsumeNextWhitespaceGlyph(string resolvedText, ref int searchIndex, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return false;

            int count = stateCount < resolvedText.Length ? stateCount : resolvedText.Length;
            int index = searchIndex < 0 ? 0 : searchIndex;
            if (index >= count || !char.IsWhiteSpace(resolvedText[index]))
                return false;

            searchIndex = index + 1;
            return true;
        }

        static bool TryGetNextGlyphIndexIncludingWhitespace(string resolvedText, ref int searchIndex, int stateCount, out int glyphIndex)
        {
            glyphIndex = -1;
            SkipLineBreaks(resolvedText, ref searchIndex, stateCount);
            if (!TryConsumeNextGlyph(resolvedText, ref searchIndex, stateCount))
                return false;

            glyphIndex = searchIndex - 1;
            return true;
        }

        static bool TryConsumeNextGlyph(string resolvedText, ref int searchIndex, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return false;

            int count = stateCount < resolvedText.Length ? stateCount : resolvedText.Length;
            int index = searchIndex < 0 ? 0 : searchIndex;
            if (index >= count)
            {
                searchIndex = count;
                return false;
            }

            searchIndex = index + 1;
            return true;
        }

        static bool TryConsumeOmittedSoftWrapWhitespace(string resolvedText, ref int searchIndex, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return false;

            int count = GetBoundedGlyphCount(resolvedText, stateCount);
            int index = searchIndex < 0 ? 0 : searchIndex;
            if (index >= count || !CanConsumeAsOmittedSoftWrapSeparator(resolvedText, index, count))
                return false;

            do
            {
                index++;
            }
            while (index < count && IsNonLineBreakWhitespace(resolvedText[index]));

            searchIndex = index;
            return true;
        }

        static bool CanConsumeAsOmittedSoftWrapSeparator(string resolvedText, int index, int count)
        {
            if (!IsNonLineBreakWhitespace(resolvedText[index]))
                return false;

            int previous = index - 1;
            if (previous < 0 || IsLineBreak(resolvedText[previous]))
                return false;

            int next = index + 1;
            while (next < count && IsNonLineBreakWhitespace(resolvedText[next]))
                next++;

            return next < count && !IsLineBreak(resolvedText[next]);
        }

        static bool IsNonLineBreakWhitespace(char c)
        {
            return char.IsWhiteSpace(c) && !IsLineBreak(c);
        }

        static void SkipLineBreaks(string resolvedText, ref int searchIndex, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return;

            int count = GetBoundedGlyphCount(resolvedText, stateCount);
            int index = searchIndex < 0 ? 0 : searchIndex;
            while (index < count && IsLineBreak(resolvedText[index]))
                index++;

            searchIndex = index;
        }

        static bool IsLineBreak(char c)
        {
            return c == '\n' || c == '\r';
        }

        static int CountNonWhitespaceGlyphs(string resolvedText, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return 0;

            int count = GetBoundedGlyphCount(resolvedText, stateCount);
            int nonWhitespace = 0;
            for (int i = 0; i < count; i++)
            {
                if (!char.IsWhiteSpace(resolvedText[i]))
                    nonWhitespace++;
            }

            return nonWhitespace;
        }

        static int GetBoundedGlyphCount(string resolvedText, int stateCount)
        {
            if (string.IsNullOrEmpty(resolvedText) || stateCount <= 0)
                return 0;

            return stateCount < resolvedText.Length ? stateCount : resolvedText.Length;
        }
    }
}
