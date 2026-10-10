using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioEffectAreaResolver
    {
        public static bool TryResolveUnit(
            TextStudioCompiledPlan plan,
            int bindingStartGlyph,
            int bindingGlyphCount,
            TextStudioEffectAreaSettings area,
            int glyphIndex,
            out TextStudioEffectUnitContext unit)
        {
            unit = default;
            if (plan == null || bindingGlyphCount <= 0)
                return false;

            int bindingEndGlyph = bindingStartGlyph + bindingGlyphCount;
            if (glyphIndex < bindingStartGlyph || glyphIndex >= bindingEndGlyph)
                return false;

            switch (area.Mode)
            {
                case TextStudioEffectAreaMode.Word:
                    return TryResolveIndexedRangeUnit(plan, TextStudioEffectAreaMode.Word, bindingStartGlyph, bindingEndGlyph, area, glyphIndex, out unit);
                case TextStudioEffectAreaMode.Line:
                    return TryResolveIndexedRangeUnit(plan, TextStudioEffectAreaMode.Line, bindingStartGlyph, bindingEndGlyph, area, glyphIndex, out unit);
                case TextStudioEffectAreaMode.Paragraph:
                    return TryResolveIndexedRangeUnit(plan, TextStudioEffectAreaMode.Paragraph, bindingStartGlyph, bindingEndGlyph, area, glyphIndex, out unit);
                case TextStudioEffectAreaMode.Range:
                    unit = CreateUnit(area, 0, 1, bindingStartGlyph, bindingGlyphCount, glyphIndex - bindingStartGlyph, bindingStartGlyph, bindingGlyphCount);
                    return true;
                case TextStudioEffectAreaMode.Glyph:
                default:
                    return TryResolveGlyphGroup(bindingStartGlyph, bindingGlyphCount, area, glyphIndex, out unit);
            }
        }

        static bool TryResolveGlyphGroup(
            int bindingStartGlyph,
            int bindingGlyphCount,
            TextStudioEffectAreaSettings area,
            int glyphIndex,
            out TextStudioEffectUnitContext unit)
        {
            int groupSize = area.GroupSize;
            int localIndex = glyphIndex - bindingStartGlyph;
            int unitIndex = localIndex / groupSize;
            int unitCount = Mathf.CeilToInt(bindingGlyphCount / (float)groupSize);
            int unitStart = bindingStartGlyph + unitIndex * groupSize;
            int unitGlyphCount = Mathf.Min(groupSize, bindingStartGlyph + bindingGlyphCount - unitStart);
            unit = CreateUnit(area, unitIndex, unitCount, unitStart, unitGlyphCount, glyphIndex - unitStart, bindingStartGlyph, bindingGlyphCount);
            return true;
        }

        static bool TryResolveIndexedRangeUnit(
            TextStudioCompiledPlan plan,
            TextStudioEffectAreaMode mode,
            int bindingStartGlyph,
            int bindingEndGlyph,
            TextStudioEffectAreaSettings area,
            int glyphIndex,
            out TextStudioEffectUnitContext unit)
        {
            unit = default;
            int rangeCount = GetRangeCount(plan, mode);
            int sourceRangeIndex = GetRangeIndex(plan, mode, glyphIndex);
            if (rangeCount <= 0 || sourceRangeIndex < 0 || sourceRangeIndex >= rangeCount)
                return false;

            int firstRange = FindFirstOverlappingRange(plan, mode, rangeCount, bindingStartGlyph, bindingEndGlyph);
            int lastRangeExclusive = FindLastOverlappingRangeExclusive(plan, mode, rangeCount, bindingStartGlyph, bindingEndGlyph);
            if (firstRange < 0 || lastRangeExclusive <= firstRange)
                return false;

            if (sourceRangeIndex < firstRange || sourceRangeIndex >= lastRangeExclusive)
                return false;

            int activeRangeCount = lastRangeExclusive - firstRange;
            int localRangeIndex = sourceRangeIndex - firstRange;
            int groupSize = area.GroupSize;
            int unitIndex = localRangeIndex / groupSize;
            int unitCount = Mathf.CeilToInt(activeRangeCount / (float)groupSize);
            int groupStartRange = firstRange + unitIndex * groupSize;
            int groupEndRange = Mathf.Min(groupStartRange + groupSize, lastRangeExclusive);
            int start = Mathf.Max(bindingStartGlyph, GetRangeStart(plan, mode, groupStartRange));
            int end = Mathf.Min(bindingEndGlyph, GetRangeEnd(plan, mode, groupEndRange - 1));
            if (end <= start)
                return false;

            unit = CreateUnit(area, unitIndex, unitCount, start, end - start, glyphIndex - start, bindingStartGlyph, bindingEndGlyph - bindingStartGlyph);
            return true;
        }

        static TextStudioEffectUnitContext CreateUnit(
            TextStudioEffectAreaSettings area,
            int unitIndex,
            int unitCount,
            int unitStartGlyph,
            int unitGlyphCount,
            int indexInUnit,
            int bindingStartGlyph,
            int bindingGlyphCount)
        {
            float normalizedBinding = bindingGlyphCount > 1
                ? (float)(unitStartGlyph + Mathf.Clamp(indexInUnit, 0, unitGlyphCount - 1) - bindingStartGlyph) / (bindingGlyphCount - 1)
                : 0f;
            return new TextStudioEffectUnitContext(area.Mode, area.GroupSize, unitIndex, unitCount, unitStartGlyph, unitGlyphCount, indexInUnit, normalizedBinding, 0f);
        }

        static int GetRangeCount(TextStudioCompiledPlan plan, TextStudioEffectAreaMode mode)
        {
            switch (mode)
            {
                case TextStudioEffectAreaMode.Word:
                    return plan.WordCount;
                case TextStudioEffectAreaMode.Line:
                    return plan.LineCount;
                case TextStudioEffectAreaMode.Paragraph:
                    return plan.ParagraphCount;
                default:
                    return 0;
            }
        }

        static int GetRangeIndex(TextStudioCompiledPlan plan, TextStudioEffectAreaMode mode, int glyphIndex)
        {
            switch (mode)
            {
                case TextStudioEffectAreaMode.Word:
                    return plan.GlyphToWordIndex != null
                        ? TextSegmenter.GetWordIndexFast(plan.GlyphToWordIndex, glyphIndex)
                        : TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, glyphIndex);
                case TextStudioEffectAreaMode.Line:
                    return plan.GlyphToLineIndex != null
                        ? TextSegmenter.GetLineIndexFast(plan.GlyphToLineIndex, glyphIndex)
                        : TextSegmenter.GetLineIndex(plan.LineRanges, plan.LineCount, glyphIndex);
                case TextStudioEffectAreaMode.Paragraph:
                    return TextSegmenter.GetParagraphIndexFast(plan.GlyphToParagraphIndex, glyphIndex);
                default:
                    return -1;
            }
        }

        static int FindFirstOverlappingRange(
            TextStudioCompiledPlan plan,
            TextStudioEffectAreaMode mode,
            int rangeCount,
            int bindingStartGlyph,
            int bindingEndGlyph)
        {
            for (int i = 0; i < rangeCount; i++)
            {
                if (GetRangeEnd(plan, mode, i) > bindingStartGlyph &&
                    GetRangeStart(plan, mode, i) < bindingEndGlyph)
                    return i;
            }

            return -1;
        }

        static int FindLastOverlappingRangeExclusive(
            TextStudioCompiledPlan plan,
            TextStudioEffectAreaMode mode,
            int rangeCount,
            int bindingStartGlyph,
            int bindingEndGlyph)
        {
            int last = -1;
            for (int i = 0; i < rangeCount; i++)
            {
                if (GetRangeEnd(plan, mode, i) > bindingStartGlyph &&
                    GetRangeStart(plan, mode, i) < bindingEndGlyph)
                    last = i;
            }

            return last >= 0 ? last + 1 : -1;
        }

        static int GetRangeStart(TextStudioCompiledPlan plan, TextStudioEffectAreaMode mode, int index)
        {
            switch (mode)
            {
                case TextStudioEffectAreaMode.Word:
                    return plan.WordRanges[index].glyphStart;
                case TextStudioEffectAreaMode.Line:
                    return plan.LineRanges[index].glyphStart;
                case TextStudioEffectAreaMode.Paragraph:
                    return plan.ParagraphRanges[index].glyphStart;
                default:
                    return 0;
            }
        }

        static int GetRangeEnd(TextStudioCompiledPlan plan, TextStudioEffectAreaMode mode, int index)
        {
            switch (mode)
            {
                case TextStudioEffectAreaMode.Word:
                    return plan.WordRanges[index].GlyphEnd;
                case TextStudioEffectAreaMode.Line:
                    return plan.LineRanges[index].GlyphEnd;
                case TextStudioEffectAreaMode.Paragraph:
                    return plan.ParagraphRanges[index].GlyphEnd;
                default:
                    return 0;
            }
        }
    }
}
