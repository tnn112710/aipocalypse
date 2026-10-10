using System;

namespace TextStudio
{
    internal static class TextStudioTextUnitBuilder
    {
        public static bool TryBuildAsciiIdentity(TextStudioCompiledPlan plan)
        {
            if (plan == null)
                return false;

            if (!IsAsciiIdentityText(plan.ResolvedText ?? string.Empty, 0))
                return false;

            BuildAsciiIdentity(plan);
            return true;
        }

        /// <summary>
        /// Builds one unit per character for resolved text the caller has verified with <see cref="IsAsciiIdentityText"/>. Slots that already hold identity units are kept.
        /// </summary>
        internal static void BuildAsciiIdentity(TextStudioCompiledPlan plan)
        {
            string text = plan.ResolvedText ?? string.Empty;
            int textLength = text.Length;
            if (textLength == 0)
            {
                plan.TextUnits = Array.Empty<TextStudioTextUnit>();
                plan.TextUnitCount = 0;
                plan.ResolvedCharToTextUnitIndex = Array.Empty<int>();
                return;
            }

            TextStudioTextUnit[] units = plan.TextUnits;
            int[] charToUnit = plan.ResolvedCharToTextUnitIndex;
            int identityCount = plan.IdentityUnitCount;
            if (units == null || units.Length < textLength)
            {
                units = new TextStudioTextUnit[TextStudioBufferCapacity.ForCount(textLength)];
                identityCount = 0;
            }

            if (charToUnit == null || charToUnit.Length < textLength)
            {
                charToUnit = new int[TextStudioBufferCapacity.ForCount(textLength)];
                identityCount = 0;
            }

            for (int i = identityCount; i < textLength; i++)
            {
                units[i] = new TextStudioTextUnit(i, i, 1, i, 1);
                charToUnit[i] = i;
            }

            plan.TextUnits = units;
            plan.TextUnitCount = textLength;
            plan.ResolvedCharToTextUnitIndex = charToUnit;
            plan.IdentityUnitCount = Math.Max(identityCount, textLength);
        }

        /// <summary>Reports whether every character from <paramref name="start"/> on forms its own text unit: ASCII without carriage returns.</summary>
        internal static bool IsAsciiIdentityText(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] > 127 || text[i] == '\r')
                    return false;
            }

            return true;
        }

        public static bool TryAppendAsciiIdentity(
            TextStudioCompiledPlan plan,
            int previousTextLength)
        {
            if (plan == null)
                return false;

            string text = plan.ResolvedText ?? string.Empty;
            int textLength = text.Length;
            if (previousTextLength < 0 ||
                previousTextLength > textLength ||
                plan.TextUnitCount != previousTextLength)
            {
                return false;
            }

            // Appended ASCII joins a previous carriage return or prepended mark, so the scan includes the last previous character: an ASCII one other than a carriage return always breaks before the appended text.
            if (!IsAsciiIdentityText(text, Math.Max(0, previousTextLength - 1)))
                return false;

            bool prefixIsIdentity = plan.IdentityUnitCount >= previousTextLength;
            TextStudioTextUnit[] units = plan.TextUnits;
            if (units == null || units.Length < textLength)
            {
                TextStudioTextUnit[] expandedUnits =
                    new TextStudioTextUnit[TextStudioBufferCapacity.ForCount(textLength)];
                if (units != null && previousTextLength > 0)
                    Array.Copy(units, expandedUnits, Math.Min(previousTextLength, units.Length));
                units = expandedUnits;
            }

            int[] charToUnit = plan.ResolvedCharToTextUnitIndex;
            if (charToUnit == null || charToUnit.Length < textLength)
            {
                int[] expandedMap = new int[TextStudioBufferCapacity.ForCount(textLength)];
                if (charToUnit != null && previousTextLength > 0)
                    Array.Copy(charToUnit, expandedMap, Math.Min(previousTextLength, charToUnit.Length));
                charToUnit = expandedMap;
            }

            for (int i = previousTextLength; i < textLength; i++)
            {
                units[i] = new TextStudioTextUnit(i, i, 1, i, 1);
                charToUnit[i] = i;
            }

            plan.TextUnits = units;
            plan.TextUnitCount = textLength;
            plan.ResolvedCharToTextUnitIndex = charToUnit;
            // Copied arrays keep the identity prefix only up to the previous text; the appended slots extend it.
            plan.IdentityUnitCount = prefixIsIdentity ? textLength : 0;
            return true;
        }

        public static void Build(TextStudioCompiledPlan plan)
        {
            Build(plan, null);
        }

        public static void Build(TextStudioCompiledPlan plan, System.Collections.Generic.IReadOnlyList<int> resolvedSourceIndices)
        {
            if (plan == null)
                return;

            string text = plan.ResolvedText ?? string.Empty;
            int textLength = text.Length;
            if (textLength == 0)
            {
                plan.TextUnits = Array.Empty<TextStudioTextUnit>();
                plan.TextUnitCount = 0;
                plan.ResolvedCharToTextUnitIndex = Array.Empty<int>();
                return;
            }

            // Readers bound both arrays by TextUnitCount and the resolved text, so growth capacity lets appended text keep them.
            int maxUnits = textLength;
            TextStudioTextUnit[] units = plan.TextUnits;
            if (units == null || units.Length < maxUnits)
                units = new TextStudioTextUnit[TextStudioBufferCapacity.ForCount(maxUnits)];

            int[] charToUnit = plan.ResolvedCharToTextUnitIndex;
            if (charToUnit == null || charToUnit.Length < textLength)
                charToUnit = new int[TextStudioBufferCapacity.ForCount(textLength)];

            int unitCount = 0;
            if (IsAsciiIdentityText(text, 0))
            {
                // Every such character is its own grapheme, so only the source mapping varies.
                for (; unitCount < textLength; unitCount++)
                {
                    int sourceStart = ResolveSourceStart(resolvedSourceIndices, unitCount);
                    int sourceEnd = ResolveSourceEnd(resolvedSourceIndices, unitCount, unitCount + 1);
                    units[unitCount] = new TextStudioTextUnit(
                        unitCount,
                        unitCount,
                        1,
                        sourceStart,
                        Math.Max(0, sourceEnd - sourceStart));
                    charToUnit[unitCount] = unitCount;
                }
            }
            else
            {
                int index = 0;
                int properties = -1;
                while (index < textLength)
                {
                    int start = index;
                    index = TextStudioTextUnitBoundary.GetUnitEnd(text, start, ref properties);

                    int resolvedLength = index - start;
                    int sourceStart = ResolveSourceStart(resolvedSourceIndices, start);
                    int sourceEnd = ResolveSourceEnd(resolvedSourceIndices, start, index);
                    units[unitCount] = new TextStudioTextUnit(
                        unitCount,
                        start,
                        resolvedLength,
                        sourceStart,
                        Math.Max(0, sourceEnd - sourceStart));

                    for (int c = start; c < index; c++)
                        charToUnit[c] = unitCount;

                    unitCount++;
                }
            }

            plan.TextUnits = units;
            plan.TextUnitCount = unitCount;
            plan.ResolvedCharToTextUnitIndex = charToUnit;
            plan.IdentityUnitCount = 0;
        }

        static int ResolveSourceStart(System.Collections.Generic.IReadOnlyList<int> sourceIndices, int resolvedStart)
        {
            return sourceIndices != null &&
                   resolvedStart >= 0 &&
                   resolvedStart < sourceIndices.Count
                ? sourceIndices[resolvedStart]
                : resolvedStart;
        }

        static int ResolveSourceEnd(System.Collections.Generic.IReadOnlyList<int> sourceIndices, int resolvedStart, int resolvedEnd)
        {
            if (sourceIndices == null || sourceIndices.Count == 0)
                return resolvedEnd;

            int last = resolvedEnd - 1;
            if (last < 0)
                return ResolveSourceStart(sourceIndices, resolvedStart);
            if (last >= sourceIndices.Count)
                return sourceIndices[sourceIndices.Count - 1] + 1;

            return sourceIndices[last] + 1;
        }

    }
}
