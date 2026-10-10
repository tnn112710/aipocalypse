using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TextStudio
{
    /// <summary>
    /// Segments resolved text into graphemes, words, and lines.
    /// Handles Unicode grapheme clusters (emoji, combining marks) safely.
    /// Precomputes glyph→word and glyph→line lookup arrays for O(1) per-glyph queries.
    /// </summary>
    internal sealed class TextSegmenter
    {
        readonly List<WordRange> _words = new List<WordRange>();
        readonly List<LineRange> _lines = new List<LineRange>();
        readonly List<ParagraphRange> _paragraphs = new List<ParagraphRange>();

        // Precomputed lookup arrays: glyph index → word/line index (avoids O(n) scans per glyph)
        int[] _glyphToWordIndex;
        int[] _glyphToLineIndex;
        int[] _glyphToParagraphIndex;

        internal ITextStudioWordBoundaryProvider WordBoundaryProvider { get; set; }

        public void Segment(TextStudioCompiledPlan plan)
        {
            Segment(plan, WordBoundaryProvider);
        }

        internal void Segment(
            TextStudioCompiledPlan plan,
            ITextStudioWordBoundaryProvider wordBoundaryProvider)
        {
            // Segmenting now replaces a deferred segmentation, which would otherwise run inside the reads below.
            plan.DeferSegmentation(null);
            _words.Clear();
            _lines.Clear();
            _paragraphs.Clear();

            string text = plan.ResolvedText;
            if (string.IsNullOrEmpty(text))
            {
                plan.WordRanges = Array.Empty<WordRange>();
                plan.WordCount = 0;
                plan.LineRanges = Array.Empty<LineRange>();
                plan.LineCount = 0;
                plan.ParagraphRanges = Array.Empty<ParagraphRange>();
                plan.ParagraphCount = 0;
                plan.GlyphToWordIndex = Array.Empty<int>();
                plan.GlyphToLineIndex = Array.Empty<int>();
                plan.GlyphToParagraphIndex = Array.Empty<int>();
                return;
            }

            int len = plan.VisibleGlyphCount;
            SegmentWords(plan, wordBoundaryProvider);
            SegmentLines(plan);
            SegmentParagraphs(plan);

            // Copy to plan (reuse arrays when possible). Readers bound every array by its count, so growth capacity lets appended text keep them.
            WordRange[] wordRanges = plan.WordRanges;
            if (wordRanges == null || wordRanges.Length < _words.Count)
                wordRanges = new WordRange[TextStudioBufferCapacity.ForCount(_words.Count)];
            _words.CopyTo(wordRanges);
            plan.WordRanges = wordRanges;
            plan.WordCount = _words.Count;

            LineRange[] lineRanges = plan.LineRanges;
            if (lineRanges == null || lineRanges.Length < _lines.Count)
                lineRanges = new LineRange[TextStudioBufferCapacity.ForCount(_lines.Count)];
            _lines.CopyTo(lineRanges);
            plan.LineRanges = lineRanges;
            plan.LineCount = _lines.Count;

            ParagraphRange[] paragraphRanges = plan.ParagraphRanges;
            if (paragraphRanges == null || paragraphRanges.Length < _paragraphs.Count)
                paragraphRanges = new ParagraphRange[TextStudioBufferCapacity.ForCount(_paragraphs.Count)];
            _paragraphs.CopyTo(paragraphRanges);
            plan.ParagraphRanges = paragraphRanges;
            plan.ParagraphCount = _paragraphs.Count;

            // Build precomputed glyph→word/line lookups
            BuildGlyphLookups(plan, len);
        }

        void SegmentWords(
            TextStudioCompiledPlan plan,
            ITextStudioWordBoundaryProvider wordBoundaryProvider)
        {
            int len = plan.VisibleGlyphCount;
            int index = 0;
            while (index < len)
            {
                if (plan.IsWhiteSpaceUnit(index))
                {
                    index++;
                    continue;
                }
                int start = index;
                int offset = plan.GetTextUnit(start).resolvedStart;
                int end = wordBoundaryProvider != null
                    ? wordBoundaryProvider.GetWordEnd(plan.ResolvedText, offset)
                    : TextStudioWordBoundary.GetWordEnd(plan.ResolvedText, offset);
                if (end <= offset || end > plan.ResolvedText.Length)
                    throw new InvalidOperationException("The word boundary provider must advance within the supplied text.");
                do
                {
                    index++;
                }
                while (index < len && plan.GetTextUnit(index).resolvedStart < end && !plan.IsWhiteSpaceUnit(index));
                _words.Add(new WordRange
                {
                    glyphStart = start,
                    glyphCount = index - start,
                    wordIndex = _words.Count
                });
            }
        }

        void SegmentLines(TextStudioCompiledPlan plan)
        {
            int len = plan.VisibleGlyphCount;
            int lineStart = 0;
            int lineIndex = 0;

            for (int i = 0; i < len; i++)
            {
                if (IsLineBreak(plan, i))
                {
                    _lines.Add(new LineRange
                    {
                        glyphStart = lineStart,
                        glyphCount = i - lineStart,
                        lineIndex = lineIndex++,
                        width = 0f,
                        baseline = 0f
                    });
                    lineStart = i + 1;
                }
            }

            // Final line (or single line with no newlines)
            if (lineStart <= len)
            {
                _lines.Add(new LineRange
                {
                    glyphStart = lineStart,
                    glyphCount = len - lineStart,
                    lineIndex = lineIndex,
                    width = 0f,
                    baseline = 0f
                });
            }
        }

        void SegmentParagraphs(TextStudioCompiledPlan plan)
        {
            int len = plan.VisibleGlyphCount;
            int paragraphStart = 0;
            int paragraphIndex = 0;
            int i = 0;

            while (i < len)
            {
                bool blankBreak = IsLineBreak(plan, i) &&
                    i + 1 < len &&
                    IsLineBreak(plan, i + 1);

                if (!blankBreak)
                {
                    i++;
                    continue;
                }

                AddParagraph(paragraphStart, i - paragraphStart, paragraphIndex++);

                while (i < len && IsLineBreak(plan, i))
                    i++;

                paragraphStart = i;
            }

            if (paragraphStart <= len)
                AddParagraph(paragraphStart, len - paragraphStart, paragraphIndex);
        }

        void AddParagraph(int glyphStart, int glyphCount, int paragraphIndex)
        {
            if (glyphCount <= 0)
                return;

            _paragraphs.Add(new ParagraphRange
            {
                glyphStart = glyphStart,
                glyphCount = glyphCount,
                paragraphIndex = paragraphIndex
            });
        }

        static bool IsLineBreak(TextStudioCompiledPlan plan, int index)
        {
            char value = plan.GetPrimaryChar(index);
            return value == '\r' || value == '\n' || value == '\u0085' || value == '\u2028' || value == '\u2029';
        }

        /// <summary>
        /// Get the word index for a given glyph index via precomputed lookup.
        /// Falls back to binary search if lookup not available.
        /// Returns -1 if the glyph is whitespace (between words).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWordIndex(WordRange[] words, int wordCount, int glyphIndex)
        {
            // Binary search: words are sorted by glyphStart
            int lo = 0, hi = wordCount - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (glyphIndex < words[mid].glyphStart)
                    hi = mid - 1;
                else if (glyphIndex >= words[mid].GlyphEnd)
                    lo = mid + 1;
                else
                    return mid;
            }
            return -1;
        }

        /// <summary>
        /// Get the line index for a given glyph index via binary search.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLineIndex(LineRange[] lines, int lineCount, int glyphIndex)
        {
            int lo = 0, hi = lineCount - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (glyphIndex < lines[mid].glyphStart)
                    hi = mid - 1;
                else if (glyphIndex >= lines[mid].GlyphEnd)
                    lo = mid + 1;
                else
                    return mid;
            }
            // Default to last line
            return lineCount > 0 ? lineCount - 1 : 0;
        }

        /// <summary>
        /// Get the word index for a glyph from a precomputed lookup array. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWordIndexFast(int[] glyphToWord, int glyphIndex)
        {
            if (glyphToWord == null || glyphIndex < 0 || glyphIndex >= glyphToWord.Length)
                return -1;
            return glyphToWord[glyphIndex];
        }

        /// <summary>
        /// Get the line index for a glyph from a precomputed lookup array. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetLineIndexFast(int[] glyphToLine, int glyphIndex)
        {
            if (glyphToLine == null || glyphIndex < 0 || glyphIndex >= glyphToLine.Length)
                return 0;
            return glyphToLine[glyphIndex];
        }

        /// <summary>
        /// Get the paragraph index for a glyph from a precomputed lookup array. O(1).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetParagraphIndexFast(int[] glyphToParagraph, int glyphIndex)
        {
            if (glyphToParagraph == null || glyphIndex < 0 || glyphIndex >= glyphToParagraph.Length)
                return 0;
            return glyphToParagraph[glyphIndex];
        }

        void BuildGlyphLookups(TextStudioCompiledPlan plan, int glyphCount)
        {
            // Resize only when needed
            if (_glyphToWordIndex == null || _glyphToWordIndex.Length < glyphCount)
                _glyphToWordIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];
            if (_glyphToLineIndex == null || _glyphToLineIndex.Length < glyphCount)
                _glyphToLineIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];
            if (_glyphToParagraphIndex == null || _glyphToParagraphIndex.Length < glyphCount)
                _glyphToParagraphIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];

            // Fill word lookup (-1 for whitespace/gaps)
            for (int i = 0; i < glyphCount; i++)
                _glyphToWordIndex[i] = -1;

            WordRange[] words = plan.WordRanges;
            int wordCount = plan.WordCount;
            for (int w = 0; w < wordCount; w++)
            {
                ref var word = ref words[w];
                int end = Math.Min(word.GlyphEnd, glyphCount);
                for (int g = word.glyphStart; g < end; g++)
                    _glyphToWordIndex[g] = w;
            }

            // Fill line lookup
            LineRange[] lines = plan.LineRanges;
            int lineCount = plan.LineCount;
            for (int l = 0; l < lineCount; l++)
            {
                ref var line = ref lines[l];
                int end = Math.Min(line.GlyphEnd, glyphCount);
                for (int g = line.glyphStart; g < end; g++)
                    _glyphToLineIndex[g] = l;
            }

            Array.Clear(_glyphToParagraphIndex, 0, glyphCount);

            ParagraphRange[] paragraphs = plan.ParagraphRanges;
            int paragraphCount = plan.ParagraphCount;
            for (int p = 0; p < paragraphCount; p++)
            {
                ref var paragraph = ref paragraphs[p];
                int end = Math.Min(paragraph.GlyphEnd, glyphCount);
                for (int g = paragraph.glyphStart; g < end; g++)
                    _glyphToParagraphIndex[g] = p;
            }

            plan.GlyphToWordIndex = _glyphToWordIndex;
            plan.GlyphToLineIndex = _glyphToLineIndex;
            plan.GlyphToParagraphIndex = _glyphToParagraphIndex;
        }
    }
}
