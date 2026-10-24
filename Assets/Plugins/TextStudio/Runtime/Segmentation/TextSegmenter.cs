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

        // Appended text, such as a growing chat log, segments again after every append. Words and lines never cross a line
        // break and the built-in word boundaries stop at one, so every unit up to the last line break segments as before while
        // those units and their resolved characters are unchanged, and segmenting resumes after it. A custom word boundary
        // provider may read any context, so its text always segments in full.
        string _segmentedText;
        // The plan the recorded segmentation was copied to, whose arrays an append resumes after.
        TextStudioCompiledPlan _segmentedPlan;
        int _reusableUnitCount;
        int[] _segmentedUnitStarts = Array.Empty<int>();
        int[] _segmentedUnitLengths = Array.Empty<int>();
        // The paragraph scan restarts after every blank line; an appended scan resumes at the last restart.
        int _paragraphResumeUnit;
        int _paragraphResumeIndex;
        int _paragraphResumeCount;
        // An appended markup compile kept the units before _keptUnitCount of the plan whose resolved text was _keptBaseText,
        // and that text starts the plan's resolved text of _keptVersion (NoteKeptUnits).
        string _keptBaseText;
        uint _keptVersion;
        int _keptUnitCount;

        internal ITextStudioWordBoundaryProvider WordBoundaryProvider { get; set; }

        // Set by the pipeline for text compiled from an append, whose segmentation keeps the units its next append can reuse.
        internal bool RetainsAppendPrefix { get; set; }

        // Units the last Segment kept from the segmentation before it; zero when it segmented the whole text.
        internal int LastReusedUnitCount { get; private set; }

        /// <summary>
        /// Notes that the plan of <paramref name="version"/> keeps the first <paramref name="keptUnitCount"/> units of the plan
        /// whose resolved text was <paramref name="baseText"/>, and starts with that text, so a segmentation of
        /// <paramref name="baseText"/> need not compare them again.
        /// </summary>
        internal void NoteKeptUnits(string baseText, uint version, int keptUnitCount)
        {
            _keptBaseText = baseText;
            _keptVersion = version;
            _keptUnitCount = keptUnitCount;
        }

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

            string text = plan.ResolvedText;
            if (string.IsNullOrEmpty(text))
            {
                _words.Clear();
                _lines.Clear();
                _paragraphs.Clear();
                _segmentedText = null;
                _reusableUnitCount = 0;
                LastReusedUnitCount = 0;
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
            TextStudioTextUnit[] units = plan.TextUnits;
            int unitCount = plan.TextUnitCount;
            // The plan arrays still hold the segmentation recorded with _segmentedText, which an append resumes after.
            bool resumesPlanArrays = _segmentedText != null && ReferenceEquals(_segmentedPlan, plan);
            int reused = wordBoundaryProvider == null
                ? ResolveReusableUnitCount(text, units, unitCount, len, plan.SourceVersion)
                : 0;
            LastReusedUnitCount = reused;
            // Recorded again once this segmentation completes; a provider that throws leaves nothing to resume after.
            _segmentedText = null;
            _reusableUnitCount = 0;
            _segmentedPlan = null;
            int paragraphStart = 0;
            int paragraphIndex = 0;
            int paragraphScanStart = 0;
            int keptWords = 0;
            int keptLines = 0;
            int keptParagraphs = 0;
            if (reused > 0)
            {
                keptWords = _words.Count;
                while (keptWords > 0 && _words[keptWords - 1].glyphStart >= reused)
                    keptWords--;
                _words.RemoveRange(keptWords, _words.Count - keptWords);
                // The final line starts after the last line break; every earlier line ends at a kept break.
                _lines.RemoveAt(_lines.Count - 1);
                keptLines = _lines.Count;
                if (_paragraphResumeUnit < reused)
                {
                    _paragraphs.RemoveRange(_paragraphResumeCount, _paragraphs.Count - _paragraphResumeCount);
                    paragraphStart = _paragraphResumeUnit;
                    paragraphIndex = _paragraphResumeIndex;
                    keptParagraphs = _paragraphResumeCount;
                    // The scan found no blank break after the restart in kept units, so it resumes at the last kept break,
                    // whose pair with the next unit appended text can complete.
                    paragraphScanStart = Math.Max(paragraphStart, reused - 1);
                }
                else
                {
                    _paragraphs.Clear();
                }
            }
            else
            {
                _words.Clear();
                _lines.Clear();
                _paragraphs.Clear();
            }

            if (!resumesPlanArrays)
            {
                keptWords = 0;
                keptLines = 0;
                keptParagraphs = 0;
            }

            SegmentWords(text, units, unitCount, len, reused, wordBoundaryProvider);
            int lastLineBreak = SegmentLines(text, units, unitCount, len, reused);
            SegmentParagraphs(text, units, unitCount, len, paragraphStart, paragraphIndex, paragraphScanStart);

            // Copy to plan (reuse arrays when possible). Readers bound every array by its count, so growth capacity lets appended text keep them.
            plan.WordRanges = CopyRanges(_words, plan.WordRanges, keptWords);
            plan.WordCount = _words.Count;
            plan.LineRanges = CopyRanges(_lines, plan.LineRanges, keptLines);
            plan.LineCount = _lines.Count;
            plan.ParagraphRanges = CopyRanges(_paragraphs, plan.ParagraphRanges, keptParagraphs);
            plan.ParagraphCount = _paragraphs.Count;

            // Build precomputed glyph→word/line lookups
            BuildGlyphLookups(plan, len, reused, paragraphScanStart, keptWords, keptLines, keptParagraphs);
            RecordReusablePrefix(
                text,
                units,
                unitCount,
                reused,
                wordBoundaryProvider == null && RetainsAppendPrefix ? lastLineBreak + 1 : 0);
            if (_segmentedText != null)
                _segmentedPlan = plan;
        }

        // Copies the list to the plan's array from keep on: the array holds the entries before, which the segmentation before
        // this one copied from the same kept list entries. A new array is filled in full.
        static T[] CopyRanges<T>(List<T> list, T[] array, int keep)
        {
            int count = list.Count;
            if (array == null || array.Length < count)
            {
                array = new T[TextStudioBufferCapacity.ForCount(count)];
                keep = 0;
            }
            for (int i = keep; i < count; i++)
                array[i] = list[i];
            return array;
        }

        // The units of the last segmentation up to its last line break, when the new plan holds the same units and resolved
        // characters there; otherwise zero. Units an appended markup compile kept from that segmentation's plan, which it
        // verified to start the new resolved text, compare equal without reading them (NoteKeptUnits).
        int ResolveReusableUnitCount(string text, TextStudioTextUnit[] units, int unitCount, int len, uint version)
        {
            int reusable = _reusableUnitCount;
            string previous = _segmentedText;
            if (reusable <= 0 || previous == null || units == null || _lines.Count == 0 ||
                reusable > len || reusable > unitCount || reusable > _segmentedUnitStarts.Length)
            {
                return 0;
            }

            bool keptByAppend = ReferenceEquals(previous, _keptBaseText) && version == _keptVersion;
            int verifiedUnits = keptByAppend ? Math.Min(reusable, _keptUnitCount) : 0;
            int[] starts = _segmentedUnitStarts;
            int[] lengths = _segmentedUnitLengths;
            int prefixLength = verifiedUnits > 0
                ? starts[verifiedUnits - 1] + lengths[verifiedUnits - 1]
                : 0;
            for (int i = verifiedUnits; i < reusable; i++)
            {
                ref readonly TextStudioTextUnit unit = ref units[i];
                if (unit.resolvedStart != starts[i] || unit.resolvedLength != lengths[i])
                    return 0;
                int end = unit.resolvedStart + unit.resolvedLength;
                if (end > prefixLength)
                    prefixLength = end;
            }

            if (prefixLength > text.Length || prefixLength > previous.Length)
                return 0;
            return ReferenceEquals(text, previous) ||
                   keptByAppend ||
                   string.CompareOrdinal(text, 0, previous, 0, prefixLength) == 0
                ? reusable
                : 0;
        }

        void RecordReusablePrefix(
            string text,
            TextStudioTextUnit[] units,
            int unitCount,
            int validatedUnitCount,
            int reusable)
        {
            if (reusable <= 0 || units == null || reusable > unitCount)
            {
                _segmentedText = null;
                _reusableUnitCount = 0;
                return;
            }

            if (_segmentedUnitStarts.Length < reusable)
            {
                int capacity = TextStudioBufferCapacity.ForCount(reusable);
                Array.Resize(ref _segmentedUnitStarts, capacity);
                Array.Resize(ref _segmentedUnitLengths, capacity);
            }

            // A resumed segmentation compared its kept units with this snapshot already.
            int copyFrom = Math.Min(validatedUnitCount, reusable);
            for (int i = copyFrom; i < reusable; i++)
            {
                _segmentedUnitStarts[i] = units[i].resolvedStart;
                _segmentedUnitLengths[i] = units[i].resolvedLength;
            }

            _segmentedText = text;
            _reusableUnitCount = reusable;
        }

        void SegmentWords(
            string text,
            TextStudioTextUnit[] units,
            int unitCount,
            int len,
            int startIndex,
            ITextStudioWordBoundaryProvider wordBoundaryProvider)
        {
            int index = startIndex;
            while (index < len)
            {
                if (IsWhiteSpaceUnit(units, unitCount, text, index))
                {
                    index++;
                    continue;
                }
                int start = index;
                int offset = UnitResolvedStart(units, unitCount, start);
                int end = wordBoundaryProvider != null
                    ? wordBoundaryProvider.GetWordEnd(text, offset)
                    : TextStudioWordBoundary.GetWordEnd(text, offset);
                if (end <= offset || end > text.Length)
                    throw new InvalidOperationException("The word boundary provider must advance within the supplied text.");
                do
                {
                    index++;
                }
                while (index < len && UnitResolvedStart(units, unitCount, index) < end &&
                       !IsWhiteSpaceUnit(units, unitCount, text, index));
                _words.Add(new WordRange
                {
                    glyphStart = start,
                    glyphCount = index - start,
                    wordIndex = _words.Count
                });
            }
        }

        // Returns the last line break unit, which is startIndex - 1 when the units after startIndex hold none.
        int SegmentLines(string text, TextStudioTextUnit[] units, int unitCount, int len, int startIndex)
        {
            int lineStart = startIndex;
            int lineIndex = _lines.Count;
            int lastLineBreak = startIndex - 1;

            for (int i = startIndex; i < len; i++)
            {
                if (IsLineBreak(units, unitCount, text, i))
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
                    lastLineBreak = i;
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

            return lastLineBreak;
        }

        // scanStart skips units after paragraphStart that the scan before this one found no blank break in.
        void SegmentParagraphs(
            string text,
            TextStudioTextUnit[] units,
            int unitCount,
            int len,
            int paragraphStart,
            int paragraphIndex,
            int scanStart)
        {
            int i = Math.Max(paragraphStart, scanStart);
            _paragraphResumeUnit = paragraphStart;
            _paragraphResumeIndex = paragraphIndex;
            _paragraphResumeCount = _paragraphs.Count;

            while (i < len)
            {
                bool blankBreak = IsLineBreak(units, unitCount, text, i) &&
                    i + 1 < len &&
                    IsLineBreak(units, unitCount, text, i + 1);

                if (!blankBreak)
                {
                    i++;
                    continue;
                }

                AddParagraph(paragraphStart, i - paragraphStart, paragraphIndex++);

                while (i < len && IsLineBreak(units, unitCount, text, i))
                    i++;

                paragraphStart = i;
                _paragraphResumeUnit = paragraphStart;
                _paragraphResumeIndex = paragraphIndex;
                _paragraphResumeCount = _paragraphs.Count;
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

        // TextStudioCompiledPlan.GetTextUnit(unitIndex).resolvedStart over the plan's unit array.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int UnitResolvedStart(TextStudioTextUnit[] units, int unitCount, int unitIndex)
        {
            return units == null || unitIndex < 0 || unitIndex >= unitCount
                ? 0
                : units[unitIndex].resolvedStart;
        }

        // TextStudioCompiledPlan.IsWhiteSpaceUnit over the plan's unit array and resolved text.
        static bool IsWhiteSpaceUnit(TextStudioTextUnit[] units, int unitCount, string text, int unitIndex)
        {
            if (units == null ||
                unitIndex < 0 ||
                unitIndex >= unitCount ||
                string.IsNullOrEmpty(text))
                return false;

            ref readonly TextStudioTextUnit unit = ref units[unitIndex];
            int start = unit.resolvedStart;
            int end = unit.ResolvedEnd;
            if (start < 0 || unit.resolvedLength <= 0 || end > text.Length)
                return false;

            for (int i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return false;
            }

            return true;
        }

        // Whether the primary character of TextStudioCompiledPlan.GetPrimaryChar is a line break.
        static bool IsLineBreak(TextStudioTextUnit[] units, int unitCount, string text, int unitIndex)
        {
            int resolvedStart = units == null || unitIndex < 0 || unitIndex >= unitCount
                ? unitIndex
                : units[unitIndex].resolvedStart;
            if (string.IsNullOrEmpty(text) || resolvedStart < 0 || resolvedStart >= text.Length)
                return false;
            char value = text[resolvedStart];
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

        // Units before wordAndLineFrom, and before paragraphFrom, keep the lookups the segmentation before this one wrote for
        // their same ranges; a reallocated array is filled in full. Ranges before the kept counts end before those units.
        void BuildGlyphLookups(
            TextStudioCompiledPlan plan,
            int glyphCount,
            int wordAndLineFrom,
            int paragraphFrom,
            int keptWords,
            int keptLines,
            int keptParagraphs)
        {
            int wordFrom = wordAndLineFrom;
            int lineFrom = wordAndLineFrom;
            // Resize only when needed
            if (_glyphToWordIndex == null || _glyphToWordIndex.Length < glyphCount)
            {
                _glyphToWordIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];
                wordFrom = 0;
            }
            if (_glyphToLineIndex == null || _glyphToLineIndex.Length < glyphCount)
            {
                _glyphToLineIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];
                lineFrom = 0;
            }
            if (_glyphToParagraphIndex == null || _glyphToParagraphIndex.Length < glyphCount)
            {
                _glyphToParagraphIndex = new int[TextStudioBufferCapacity.ForCount(glyphCount)];
                paragraphFrom = 0;
            }

            // Fill word lookup (-1 for whitespace/gaps)
            for (int i = wordFrom; i < glyphCount; i++)
                _glyphToWordIndex[i] = -1;

            WordRange[] words = plan.WordRanges;
            int wordCount = plan.WordCount;
            for (int w = wordFrom > 0 ? keptWords : 0; w < wordCount; w++)
            {
                ref var word = ref words[w];
                if (word.glyphStart < wordFrom)
                    continue;
                int end = Math.Min(word.GlyphEnd, glyphCount);
                for (int g = word.glyphStart; g < end; g++)
                    _glyphToWordIndex[g] = w;
            }

            // Fill line lookup
            LineRange[] lines = plan.LineRanges;
            int lineCount = plan.LineCount;
            for (int l = lineFrom > 0 ? keptLines : 0; l < lineCount; l++)
            {
                ref var line = ref lines[l];
                if (line.glyphStart < lineFrom)
                    continue;
                int end = Math.Min(line.GlyphEnd, glyphCount);
                for (int g = line.glyphStart; g < end; g++)
                    _glyphToLineIndex[g] = l;
            }

            Array.Clear(_glyphToParagraphIndex, paragraphFrom, glyphCount - paragraphFrom);

            // A paragraph that the appended scan continued fills only its glyphs from paragraphFrom on.
            ParagraphRange[] paragraphs = plan.ParagraphRanges;
            int paragraphCount = plan.ParagraphCount;
            for (int p = paragraphFrom > 0 ? keptParagraphs : 0; p < paragraphCount; p++)
            {
                ref var paragraph = ref paragraphs[p];
                if (paragraph.GlyphEnd <= paragraphFrom)
                    continue;
                int end = Math.Min(paragraph.GlyphEnd, glyphCount);
                for (int g = Math.Max(paragraph.glyphStart, paragraphFrom); g < end; g++)
                    _glyphToParagraphIndex[g] = p;
            }

            plan.GlyphToWordIndex = _glyphToWordIndex;
            plan.GlyphToLineIndex = _glyphToLineIndex;
            plan.GlyphToParagraphIndex = _glyphToParagraphIndex;
        }
    }
}
