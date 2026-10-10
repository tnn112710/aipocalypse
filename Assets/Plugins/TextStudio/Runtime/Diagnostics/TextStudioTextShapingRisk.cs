namespace TextStudio
{
    /// <summary>Detects Unicode patterns that may require a complex-shaping-capable text backend.</summary>
    public static class TextStudioTextShapingRisk
    {
        /// <summary>Reports whether text contains combining, bidirectional, joiner, or script ranges that make simple glyph mapping unsafe.</summary>
        public static bool HasComplexShapingRisk(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsSurrogate(c) ||
                    IsCombiningMark(c) ||
                    IsRightToLeftLikely(c) ||
                    c == '\u200D' ||
                    c == '\uFE0E' ||
                    c == '\uFE0F')
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsCombiningMark(char c)
        {
            return (c >= '\u0300' && c <= '\u036F') ||
                   (c >= '\u1AB0' && c <= '\u1AFF') ||
                   (c >= '\u1DC0' && c <= '\u1DFF') ||
                   (c >= '\u20D0' && c <= '\u20FF') ||
                   (c >= '\uFE20' && c <= '\uFE2F');
        }

        static bool IsRightToLeftLikely(char c)
        {
            return (c >= '\u0590' && c <= '\u08FF') ||
                   (c >= '\uFB1D' && c <= '\uFDFF') ||
                   (c >= '\uFE70' && c <= '\uFEFF');
        }
    }
}
