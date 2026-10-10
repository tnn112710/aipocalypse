using System;
using System.Globalization;

namespace TextStudio
{
    internal static partial class TextStudioGlyphChangeSetBuilder
    {
        // Match integer digits from the decimal point backwards and fractional digits forwards.
        // Group separators never shift decimal places. Unsupported multiple-number content uses the normal diff.
        static bool TryRetainNumericValue(TextStudioCompiledPlan oldPlan, TextStudioCompiledPlan newPlan,
            string separator, ref MapBuilder map)
        {
            if (!TryGetNumericRange(oldPlan, separator, out int oldFirst, out int oldLast, out int oldDecimal) ||
                !TryGetNumericRange(newPlan, separator, out int newFirst, out int newLast, out int newDecimal)) return false;
            int oldIndex = oldDecimal - 1;
            int newIndex = newDecimal - 1;
            while (oldIndex >= oldFirst && newIndex >= newFirst)
            {
                if (!IsDecimalDigit(oldPlan, oldIndex)) { oldIndex--; continue; }
                if (!IsDecimalDigit(newPlan, newIndex)) { newIndex--; continue; }
                MatchNumericPair(oldPlan, newPlan, ref map, oldIndex--, newIndex--);
            }
            oldIndex = oldDecimal + 1;
            newIndex = newDecimal + 1;
            while (oldIndex <= oldLast && newIndex <= newLast)
            {
                MatchNumericPair(oldPlan, newPlan, ref map, oldIndex++, newIndex++);
            }
            if (oldDecimal <= oldLast && newDecimal <= newLast)
                MatchNumericPair(oldPlan, newPlan, ref map, oldDecimal, newDecimal);
            for (int i = 0; i < Math.Min(oldFirst, newFirst); i++)
                MatchNumericPair(oldPlan, newPlan, ref map, i, i);
            int suffix = Math.Min(oldPlan.VisibleGlyphCount - oldLast - 1, newPlan.VisibleGlyphCount - newLast - 1);
            for (int i = 1; i <= suffix; i++)
                MatchNumericPair(oldPlan, newPlan, ref map, oldLast + i, newLast + i);
            // Preserve grouping punctuation only when its adjacent retained digits identify the same place.
            for (int i = newFirst + 1; i < newDecimal; i++)
            {
                if (IsDecimalDigit(newPlan, i)) continue;
                int oldRight = i + 1 < map.IncomingCount
                    ? map.GetOutgoingGlyphForIncoming(i + 1) : -1;
                if (oldRight > oldFirst && !IsDecimalDigit(oldPlan, oldRight - 1))
                    MatchNumericPair(oldPlan, newPlan, ref map, oldRight - 1, i);
            }
            return true;
        }

        static void MatchNumericPair(TextStudioCompiledPlan oldPlan, TextStudioCompiledPlan newPlan,
            ref MapBuilder map, int oldUnit, int newUnit)
        {
            if (UnitsEqual(oldPlan, oldUnit, newPlan, newUnit))
                map.RetainPair(oldUnit, newUnit);
        }

        static bool TryGetNumericRange(TextStudioCompiledPlan plan, string separator, out int first, out int last, out int point)
        {
            first = -1;
            last = -1;
            point = -1;
            for (int i = 0; i < plan.VisibleGlyphCount; i++)
            {
                if (!IsDecimalDigit(plan, i)) continue;
                if (first < 0) first = i;
                last = i;
            }
            if (first < 0) return false;
            bool fractional = false;
            for (int i = first; i <= last; i++)
            {
                if (IsDecimalDigit(plan, i)) continue;
                int start = plan.GetResolvedStart(i);
                int length = plan.GetResolvedLength(i);
                if (length == separator.Length && string.CompareOrdinal(plan.ResolvedText, start, separator, 0, length) == 0)
                {
                    if (fractional) return false;
                    point = i;
                    fractional = true;
                }
                else if (fractional || length != 1 || !IsGroupingSeparator(plan.ResolvedText[start])) return false;
            }
            if (point < 0) point = last + 1;
            return true;
        }

        static bool IsDecimalDigit(TextStudioCompiledPlan plan, int unit)
        {
            int start = plan.GetResolvedStart(unit);
            return start >= 0 && start < plan.ResolvedText.Length &&
                   CharUnicodeInfo.GetUnicodeCategory(plan.ResolvedText, start) == UnicodeCategory.DecimalDigitNumber;
        }

        static bool IsGroupingSeparator(char value) =>
            value == ',' || value == '.' || value == '\'' || value == '\u066C' || value == '\u00A0' || value == '\u202F' || value == ' ';
    }
}
