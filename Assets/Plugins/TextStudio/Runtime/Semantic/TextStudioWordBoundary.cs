using System;

namespace TextStudio
{
    /// <summary>Optional per-host language tailoring. Return a UTF-16 end offset on a text-unit boundary.</summary>
    public interface ITextStudioWordBoundaryProvider
    {
        /// <summary>Returns a language-tailored word end as a UTF-16 offset on a text-unit boundary for the supplied starting offset.</summary>
        int GetWordEnd(string text, int start);
    }

    /// <summary>Unicode default word boundaries. Dictionary-based languages may supply a host-specific provider.</summary>
    public static class TextStudioWordBoundary
    {
        /// <summary>Returns the next Unicode default word boundary after the supplied UTF-16 starting offset, clamped to the string.</summary>
        public static int GetWordEnd(string text, int start)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            start = Math.Max(0, Math.Min(start, text.Length));
            if (start == text.Length)
                return start;
            int codePoint = TextStudioTextUnitBoundary.ReadCodePoint(text, start, out int length);
            TextStudioWordProperty previous = Property(codePoint);
            TextStudioWordProperty significant = previous;
            TextStudioWordProperty beforeSignificant = TextStudioWordProperty.Other;
            int regionalCount = previous == TextStudioWordProperty.RegionalIndicator ? 1 : 0;
            int index = start + length;
            while (index < text.Length)
            {
                codePoint = TextStudioTextUnitBoundary.ReadCodePoint(text, index, out length);
                int properties = TextStudioTextUnitBoundary.GetProperties(codePoint);
                TextStudioWordProperty current = (TextStudioWordProperty)(properties >> 8);
                bool joins;
                if (previous == TextStudioWordProperty.CR && current == TextStudioWordProperty.LF)
                    joins = true;
                else if (IsNewline(previous) || IsNewline(current))
                    joins = false;
                else if (previous == TextStudioWordProperty.ZWJ && (properties & 128) != 0)
                    joins = true;
                else if (previous == TextStudioWordProperty.WSegSpace && current == TextStudioWordProperty.WSegSpace)
                    joins = true;
                else if (IsIgnored(current))
                    joins = true;
                else
                    joins = Joins(significant, current, beforeSignificant, text, index + length, regionalCount);
                if (!joins)
                    return index;
                if (!IsIgnored(current))
                {
                    beforeSignificant = significant;
                    significant = current;
                    regionalCount = current == TextStudioWordProperty.RegionalIndicator ? regionalCount + 1 : 0;
                }
                previous = current;
                index += length;
            }
            return index;
        }

        static bool Joins(TextStudioWordProperty left, TextStudioWordProperty right, TextStudioWordProperty before, string text, int nextOffset, int regionalCount)
        {
            if (IsLetter(left) && IsLetter(right))
                return true;
            if (left == TextStudioWordProperty.HebrewLetter && right == TextStudioWordProperty.SingleQuote)
                return true;
            if (IsLetter(left) && IsMidLetter(right) && IsLetter(NextSignificant(text, nextOffset)))
                return true;
            if (IsMidLetter(left) && IsLetter(before) && IsLetter(right))
                return true;
            if (left == TextStudioWordProperty.HebrewLetter && right == TextStudioWordProperty.DoubleQuote && NextSignificant(text, nextOffset) == TextStudioWordProperty.HebrewLetter)
                return true;
            if (before == TextStudioWordProperty.HebrewLetter && left == TextStudioWordProperty.DoubleQuote && right == TextStudioWordProperty.HebrewLetter)
                return true;
            if ((left == TextStudioWordProperty.Numeric && (right == TextStudioWordProperty.Numeric || IsLetter(right))) || (IsLetter(left) && right == TextStudioWordProperty.Numeric))
                return true;
            if (left == TextStudioWordProperty.Numeric && IsMidNumber(right) && NextSignificant(text, nextOffset) == TextStudioWordProperty.Numeric)
                return true;
            if (before == TextStudioWordProperty.Numeric && IsMidNumber(left) && right == TextStudioWordProperty.Numeric)
                return true;
            if (left == TextStudioWordProperty.Katakana && right == TextStudioWordProperty.Katakana)
                return true;
            if ((IsExtendedWord(left) && right == TextStudioWordProperty.ExtendNumLet) || (left == TextStudioWordProperty.ExtendNumLet && IsExtendedWord(right)))
                return true;
            return left == TextStudioWordProperty.RegionalIndicator && right == TextStudioWordProperty.RegionalIndicator && (regionalCount & 1) != 0;
        }

        static TextStudioWordProperty NextSignificant(string text, int index)
        {
            while (index < text.Length)
            {
                int point = TextStudioTextUnitBoundary.ReadCodePoint(text, index, out int length);
                TextStudioWordProperty property = Property(point);
                if (!IsIgnored(property))
                    return property;
                index += length;
            }
            return TextStudioWordProperty.Other;
        }

        static TextStudioWordProperty Property(int point) => (TextStudioWordProperty)(TextStudioTextUnitBoundary.GetProperties(point) >> 8);
        static bool IsLetter(TextStudioWordProperty property) => property == TextStudioWordProperty.ALetter || property == TextStudioWordProperty.HebrewLetter;
        static bool IsNewline(TextStudioWordProperty property) => property == TextStudioWordProperty.CR || property == TextStudioWordProperty.LF || property == TextStudioWordProperty.Newline;
        static bool IsIgnored(TextStudioWordProperty property) => property == TextStudioWordProperty.Extend || property == TextStudioWordProperty.Format || property == TextStudioWordProperty.ZWJ;
        static bool IsMidLetter(TextStudioWordProperty property) => property == TextStudioWordProperty.MidLetter || property == TextStudioWordProperty.MidNumLet || property == TextStudioWordProperty.SingleQuote;
        static bool IsMidNumber(TextStudioWordProperty property) => property == TextStudioWordProperty.MidNum || property == TextStudioWordProperty.MidNumLet || property == TextStudioWordProperty.SingleQuote;
        static bool IsExtendedWord(TextStudioWordProperty property) => IsLetter(property) || property == TextStudioWordProperty.Numeric || property == TextStudioWordProperty.Katakana || property == TextStudioWordProperty.ExtendNumLet;
    }
}
