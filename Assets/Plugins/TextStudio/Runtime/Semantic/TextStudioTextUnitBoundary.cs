using System;

namespace TextStudio
{
    /// <summary>Allocation-free Unicode text-unit traversal using UTF-16 offsets. Inline objects remain atomic.</summary>
    public static class TextStudioTextUnitBoundary
    {
        // Every code point below U+0800 (Latin through NKo) reads its properties from this copy of the generated data instead of searching its ranges. Built once from the same lookup, so both always agree.
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly int[] s_twoByteProperties = CreateTwoByteProperties();

        /// <summary>Counts Unicode text units in a string, returning zero for null or empty text.</summary>
        public static int Count(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            for (int index = 0; index < text.Length; count++)
                index = GetUnitEnd(text, index);
            return count;
        }

        /// <summary>Returns the UTF-16 length of a prefix containing at most the requested number of complete text units.</summary>
        public static int GetPrefixLength(string text, int maximumUnits)
        {
            if (string.IsNullOrEmpty(text) || maximumUnits <= 0)
                return 0;

            int index = 0;
            for (int unit = 0;
                 unit < maximumUnits && index < text.Length;
                 unit++)
            {
                index = GetUnitEnd(text, index);
            }
            return index;
        }

        /// <summary>Returns the nearest text-unit boundary at or before the supplied UTF-16 offset, clamped to the string.</summary>
        public static int FloorBoundary(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
                return 0;
            if (index >= text.Length)
                return text.Length;

            int start = 0;
            while (start < text.Length)
            {
                int end = GetUnitEnd(text, start);
                if (index < end)
                    return start;
                if (index == end)
                    return end;
                start = end;
            }
            return text.Length;
        }

        /// <summary>Returns the nearest text-unit boundary at or after the supplied UTF-16 offset, clamped to the string.</summary>
        public static int CeilingBoundary(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
                return 0;
            if (index >= text.Length)
                return text.Length;

            int start = 0;
            while (start < text.Length)
            {
                int end = GetUnitEnd(text, start);
                if (index <= start)
                    return start;
                if (index <= end)
                    return end;
                start = end;
            }
            return text.Length;
        }

        /// <summary>Moves by a signed number of complete text units from the floored UTF-16 offset, stopping at string boundaries.</summary>
        public static int Move(string text, int index, int offset)
        {
            index = FloorBoundary(text, index);
            if (offset > 0)
            {
                for (int step = 0;
                     step < offset && index < (text?.Length ?? 0);
                     step++)
                {
                    index = GetUnitEnd(text, index);
                }
                return index;
            }

            for (int step = 0; step > offset && index > 0; step--)
                index = GetPreviousBoundary(text, index);
            return index;
        }

        /// <summary>Counts complete text units ending at or before the supplied UTF-16 offset.</summary>
        public static int CountBefore(string text, int index)
        {
            int boundary = FloorBoundary(text, index);
            int count = 0;
            for (int current = 0; current < boundary; count++)
                current = GetUnitEnd(text, current);
            return count;
        }

        /// <summary>Returns the preceding text-unit boundary for the supplied UTF-16 offset, or zero at the start of the string.</summary>
        public static int GetPreviousBoundary(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
                return 0;
            index = Math.Min(index, text.Length);

            int start = 0;
            while (start < text.Length)
            {
                int end = GetUnitEnd(text, start);
                if (index <= end)
                    return start;
                start = end;
            }
            return text.Length;
        }

        /// <summary>Returns the UTF-16 end offset of the text unit beginning at the supplied offset, keeping inline objects atomic.</summary>
        public static int GetUnitEnd(string text, int unitStart)
        {
            int properties = -1;
            return GetUnitEnd(text, unitStart, ref properties);
        }

        /// <summary>
        /// Returns the end of the unit at <paramref name="unitStart"/>. <paramref name="properties"/> passes the properties of the code point at the start when known, or -1, and returns those of the code point at the returned end when the scan read it, or -1, so consecutive units look up each code point once.
        /// </summary>
        internal static int GetUnitEnd(string text, int unitStart, ref int properties)
        {
            int knownProperties = properties;
            properties = -1;
            if (string.IsNullOrEmpty(text))
                return 0;

            unitStart = Math.Max(0, Math.Min(unitStart, text.Length));
            if (unitStart >= text.Length)
                return text.Length;

            int index = unitStart;
            int firstCodePoint = ReadCodePoint(
                text,
                index,
                out int codePointLength);
            index += codePointLength;
            if (firstCodePoint == 0xFFFC)
                return index;

            int firstProperties = knownProperties >= 0 ? knownProperties : GetProperties(firstCodePoint);
            TextStudioGraphemeProperty previous = (TextStudioGraphemeProperty)(firstProperties & 31);
            int regionalCount = previous == TextStudioGraphemeProperty.RegionalIndicator ? 1 : 0;
            bool pictographicChain = (firstProperties & 128) != 0;
            bool joinerAfterPictograph = false;
            bool indicConsonant = ((firstProperties >> 5) & 3) == 1;
            bool indicLinker = false;
            while (index < text.Length)
            {
                int codePoint = ReadCodePoint(text, index, out int length);
                if (codePoint == 0xFFFC)
                    break;
                int currentProperties = GetProperties(codePoint);
                TextStudioGraphemeProperty current = (TextStudioGraphemeProperty)(currentProperties & 31);
                int indic = (currentProperties >> 5) & 3;
                bool pictographic = (currentProperties & 128) != 0;
                if (Breaks(previous, current, regionalCount, joinerAfterPictograph, pictographic, indicConsonant && indicLinker && indic == 1))
                {
                    properties = currentProperties;
                    break;
                }

                joinerAfterPictograph = current == TextStudioGraphemeProperty.ZWJ && pictographicChain;
                if (current != TextStudioGraphemeProperty.Extend)
                    pictographicChain = pictographic;
                if (indic == 1)
                {
                    indicConsonant = true;
                    indicLinker = false;
                }
                else if (indic == 3 && indicConsonant)
                    indicLinker = true;
                else if (indic != 2 && indic != 3)
                {
                    indicConsonant = false;
                    indicLinker = false;
                }
                regionalCount = current == TextStudioGraphemeProperty.RegionalIndicator ? regionalCount + 1 : 0;
                previous = current;
                index += length;
            }
            return index;
        }

        static bool Breaks(TextStudioGraphemeProperty left, TextStudioGraphemeProperty right, int regionalCount, bool joinerAfterPictograph, bool rightPictographic, bool indicConjunct)
        {
            if (left == TextStudioGraphemeProperty.CR && right == TextStudioGraphemeProperty.LF)
                return false;
            if (IsControl(left) || IsControl(right))
                return true;
            if (left == TextStudioGraphemeProperty.L && (right == TextStudioGraphemeProperty.L || right == TextStudioGraphemeProperty.V || right == TextStudioGraphemeProperty.LV || right == TextStudioGraphemeProperty.LVT))
                return false;
            if ((left == TextStudioGraphemeProperty.LV || left == TextStudioGraphemeProperty.V) && (right == TextStudioGraphemeProperty.V || right == TextStudioGraphemeProperty.T))
                return false;
            if ((left == TextStudioGraphemeProperty.LVT || left == TextStudioGraphemeProperty.T) && right == TextStudioGraphemeProperty.T)
                return false;
            if (right == TextStudioGraphemeProperty.Extend || right == TextStudioGraphemeProperty.ZWJ || right == TextStudioGraphemeProperty.SpacingMark || left == TextStudioGraphemeProperty.Prepend || indicConjunct)
                return false;
            if (left == TextStudioGraphemeProperty.ZWJ && joinerAfterPictograph && rightPictographic)
                return false;
            return left != TextStudioGraphemeProperty.RegionalIndicator || right != TextStudioGraphemeProperty.RegionalIndicator || (regionalCount & 1) == 0;
        }

        static bool IsControl(TextStudioGraphemeProperty property)
        {
            return property == TextStudioGraphemeProperty.CR || property == TextStudioGraphemeProperty.LF || property == TextStudioGraphemeProperty.Control;
        }

        internal static int ReadCodePoint(string text, int index, out int length)
        {
            if (index + 1 < text.Length &&
                char.IsHighSurrogate(text[index]) &&
                char.IsLowSurrogate(text[index + 1]))
            {
                length = 2;
                return char.ConvertToUtf32(text[index], text[index + 1]);
            }

            length = 1;
            return text[index];
        }

        /// <summary>Returns the generated Unicode properties of a code point.</summary>
        internal static int GetProperties(int codePoint)
        {
            return (uint)codePoint < (uint)s_twoByteProperties.Length
                ? s_twoByteProperties[codePoint]
                : TextStudioUnicodeData.Get(codePoint);
        }

        static int[] CreateTwoByteProperties()
        {
            int[] properties = new int[0x800];
            for (int codePoint = 0; codePoint < properties.Length; codePoint++)
                properties[codePoint] = TextStudioUnicodeData.Get(codePoint);
            return properties;
        }

    }
}
