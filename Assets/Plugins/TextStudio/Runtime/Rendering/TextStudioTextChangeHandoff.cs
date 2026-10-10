using System;

namespace TextStudio
{
    /// <summary>
    /// Semantic glyph correspondence for a text replacement. A changed glyph exits or enters;
    /// a retained glyph stays visually continuous across the replacement.
    /// </summary>
    public readonly struct TextStudioGlyphChangeSet
    {
        readonly bool[] _outgoingChanged;
        readonly bool[] _incomingChanged;
        readonly int[] _outgoingToIncoming;
        readonly int[] _incomingToOutgoing;
        readonly bool _inline;
        readonly int _inlineOutgoingCount;
        readonly int _inlineIncomingCount;
        readonly uint _inlineOutgoingChanged;
        readonly uint _inlineIncomingChanged;
        readonly ulong _inlineOutgoingMapLow;
        readonly ulong _inlineOutgoingMapHigh;
        readonly ulong _inlineIncomingMapLow;
        readonly ulong _inlineIncomingMapHigh;

        internal TextStudioGlyphChangeSet(
            bool[] outgoingChanged,
            bool[] incomingChanged,
            int[] outgoingToIncoming,
            int[] incomingToOutgoing)
        {
            _outgoingChanged = outgoingChanged;
            _incomingChanged = incomingChanged;
            _outgoingToIncoming = outgoingToIncoming;
            _incomingToOutgoing = incomingToOutgoing;
            _inline = false;
            _inlineOutgoingCount = 0;
            _inlineIncomingCount = 0;
            _inlineOutgoingChanged = 0;
            _inlineIncomingChanged = 0;
            _inlineOutgoingMapLow = 0;
            _inlineOutgoingMapHigh = 0;
            _inlineIncomingMapLow = 0;
            _inlineIncomingMapHigh = 0;
            OutgoingChangedGlyphCount = CountChanged(outgoingChanged);
            IncomingChangedGlyphCount = CountChanged(incomingChanged);
        }

        internal TextStudioGlyphChangeSet(int outgoingCount, int incomingCount,
            uint outgoingChanged, uint incomingChanged,
            ulong outgoingMapLow, ulong outgoingMapHigh,
            ulong incomingMapLow, ulong incomingMapHigh)
        {
            _outgoingChanged = null;
            _incomingChanged = null;
            _outgoingToIncoming = null;
            _incomingToOutgoing = null;
            _inline = true;
            _inlineOutgoingCount = outgoingCount;
            _inlineIncomingCount = incomingCount;
            _inlineOutgoingChanged = outgoingChanged;
            _inlineIncomingChanged = incomingChanged;
            _inlineOutgoingMapLow = outgoingMapLow;
            _inlineOutgoingMapHigh = outgoingMapHigh;
            _inlineIncomingMapLow = incomingMapLow;
            _inlineIncomingMapHigh = incomingMapHigh;
            OutgoingChangedGlyphCount = CountChanged(outgoingChanged);
            IncomingChangedGlyphCount = CountChanged(incomingChanged);
        }

        /// <summary>Number of glyphs from the old text that should receive exit behavior.</summary>
        public int OutgoingChangedGlyphCount { get; }

        /// <summary>Number of glyphs from the new text that should receive entry behavior.</summary>
        public int IncomingChangedGlyphCount { get; }

        /// <summary>Whether the replacement changes at least one semantic glyph.</summary>
        public bool HasChanges =>
            OutgoingChangedGlyphCount > 0 || IncomingChangedGlyphCount > 0;

        /// <summary>Reports whether an old-text glyph should receive exit behavior.</summary>
        public bool IsOutgoingGlyphChanged(int glyphIndex)
        {
            if (_inline)
                return glyphIndex >= 0 && glyphIndex < _inlineOutgoingCount &&
                       (_inlineOutgoingChanged & (1u << glyphIndex)) != 0;
            return _outgoingChanged != null &&
                   glyphIndex >= 0 &&
                   glyphIndex < _outgoingChanged.Length &&
                   _outgoingChanged[glyphIndex];
        }

        /// <summary>Reports whether a new-text glyph should receive entry behavior.</summary>
        public bool IsIncomingGlyphChanged(int glyphIndex)
        {
            if (_inline)
                return glyphIndex >= 0 && glyphIndex < _inlineIncomingCount &&
                       (_inlineIncomingChanged & (1u << glyphIndex)) != 0;
            return _incomingChanged != null &&
                   glyphIndex >= 0 &&
                   glyphIndex < _incomingChanged.Length &&
                   _incomingChanged[glyphIndex];
        }

        public int OutgoingGlyphCount => _inline ? _inlineOutgoingCount : _outgoingChanged?.Length ?? 0;
        public int IncomingGlyphCount => _inline ? _inlineIncomingCount : _incomingChanged?.Length ?? 0;
        public bool IsDefined => _inline || (_outgoingChanged != null && _incomingChanged != null);
        internal bool HasRetainedGlyphs =>
            IncomingGlyphCount > IncomingChangedGlyphCount;

        /// <summary>Returns the retained incoming glyph that corresponds to an outgoing glyph, or -1 when none exists.</summary>
        public int GetIncomingGlyphForOutgoing(int outgoingGlyphIndex)
        {
            if (_inline)
                return outgoingGlyphIndex >= 0 && outgoingGlyphIndex < _inlineOutgoingCount
                    ? ReadPackedMap(_inlineOutgoingMapLow, _inlineOutgoingMapHigh, outgoingGlyphIndex)
                    : -1;
            return _outgoingToIncoming != null &&
                   outgoingGlyphIndex >= 0 &&
                   outgoingGlyphIndex < _outgoingToIncoming.Length
                ? _outgoingToIncoming[outgoingGlyphIndex]
                : -1;
        }

        /// <summary>Returns the retained outgoing glyph that corresponds to an incoming glyph, or -1 when none exists.</summary>
        public int GetOutgoingGlyphForIncoming(int incomingGlyphIndex)
        {
            if (_inline)
                return incomingGlyphIndex >= 0 && incomingGlyphIndex < _inlineIncomingCount
                    ? ReadPackedMap(_inlineIncomingMapLow, _inlineIncomingMapHigh, incomingGlyphIndex)
                    : -1;
            return _incomingToOutgoing != null &&
                   incomingGlyphIndex >= 0 &&
                   incomingGlyphIndex < _incomingToOutgoing.Length
                ? _incomingToOutgoing[incomingGlyphIndex]
                : -1;
        }

        internal bool TryMapOutgoingBoundary(
            int outgoingBoundary,
            out int incomingBoundary)
        {
            incomingBoundary = -1;
            if (!IsDefined ||
                outgoingBoundary < 0 ||
                outgoingBoundary > OutgoingGlyphCount)
            {
                return false;
            }

            if (OutgoingGlyphCount == 0)
            {
                incomingBoundary = 0;
                return true;
            }

            int before = outgoingBoundary > 0
                ? GetIncomingGlyphForOutgoing(outgoingBoundary - 1)
                : -1;
            int after = outgoingBoundary < OutgoingGlyphCount
                ? GetIncomingGlyphForOutgoing(outgoingBoundary)
                : -1;
            if (before >= 0 && after >= 0)
            {
                if (before + 1 != after)
                    return false;
                incomingBoundary = after;
                return true;
            }

            if (before >= 0)
            {
                incomingBoundary = before + 1;
                return true;
            }

            if (after >= 0)
            {
                incomingBoundary = after;
                return true;
            }

            return outgoingBoundary == 0 && IncomingGlyphCount == 0;
        }

        internal static void WritePackedMap(ref ulong low, ref ulong high, int index, int value)
        {
            if (index < 12)
            {
                int shift = index * 5;
                low = (low & ~(31ul << shift)) | ((ulong)(value + 1) << shift);
            }
            else
            {
                int shift = (index - 12) * 5;
                high = (high & ~(31ul << shift)) | ((ulong)(value + 1) << shift);
            }
        }

        internal static int ReadPackedMap(ulong low, ulong high, int index)
        {
            ulong encoded = index < 12
                ? (low >> (index * 5)) & 31ul
                : (high >> ((index - 12) * 5)) & 31ul;
            return (int)encoded - 1;
        }

        static int CountChanged(uint values)
        {
            int count = 0;
            while (values != 0)
            {
                values &= values - 1;
                count++;
            }
            return count;
        }

        static int CountChanged(bool[] values)
        {
            int count = 0;
            if (values == null)
                return count;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                    count++;
            }
            return count;
        }
    }

    internal static partial class TextStudioGlyphChangeSetBuilder
    {
        const int MaximumLcsCells = 65536;
        const int InlineGlyphLimit = 16;

        internal struct MapBuilder
        {
            readonly int _outgoingCount;
            readonly int _incomingCount;
            readonly bool _inline;
            readonly bool[] _outgoingChanged;
            readonly bool[] _incomingChanged;
            readonly int[] _outgoingToIncoming;
            readonly int[] _incomingToOutgoing;
            uint _inlineOutgoingChanged;
            uint _inlineIncomingChanged;
            ulong _inlineOutgoingMapLow;
            ulong _inlineOutgoingMapHigh;
            ulong _inlineIncomingMapLow;
            ulong _inlineIncomingMapHigh;

            internal int IncomingCount => _incomingCount;
            internal bool IsInline => _inline;

            internal MapBuilder(int outgoingCount, int incomingCount)
            {
                _outgoingCount = outgoingCount;
                _incomingCount = incomingCount;
                _inline = outgoingCount <= InlineGlyphLimit && incomingCount <= InlineGlyphLimit;
                _outgoingChanged = _inline ? null : CreateChangedMask(outgoingCount);
                _incomingChanged = _inline ? null : CreateChangedMask(incomingCount);
                _outgoingToIncoming = _inline ? null : CreateUnmappedIndices(outgoingCount);
                _incomingToOutgoing = _inline ? null : CreateUnmappedIndices(incomingCount);
                _inlineOutgoingChanged = _inline ? (1u << outgoingCount) - 1u : 0u;
                _inlineIncomingChanged = _inline ? (1u << incomingCount) - 1u : 0u;
                _inlineOutgoingMapLow = 0;
                _inlineOutgoingMapHigh = 0;
                _inlineIncomingMapLow = 0;
                _inlineIncomingMapHigh = 0;
            }

            internal void RetainPair(int outgoingIndex, int incomingIndex)
            {
                if (_inline)
                {
                    _inlineOutgoingChanged &= ~(1u << outgoingIndex);
                    _inlineIncomingChanged &= ~(1u << incomingIndex);
                    TextStudioGlyphChangeSet.WritePackedMap(ref _inlineOutgoingMapLow,
                        ref _inlineOutgoingMapHigh, outgoingIndex, incomingIndex);
                    TextStudioGlyphChangeSet.WritePackedMap(ref _inlineIncomingMapLow,
                        ref _inlineIncomingMapHigh, incomingIndex, outgoingIndex);
                    return;
                }

                _outgoingChanged[outgoingIndex] = false;
                _incomingChanged[incomingIndex] = false;
                _outgoingToIncoming[outgoingIndex] = incomingIndex;
                _incomingToOutgoing[incomingIndex] = outgoingIndex;
            }

            internal int GetOutgoingGlyphForIncoming(int incomingIndex) => _inline
                ? TextStudioGlyphChangeSet.ReadPackedMap(_inlineIncomingMapLow,
                    _inlineIncomingMapHigh, incomingIndex)
                : _incomingToOutgoing[incomingIndex];

            internal TextStudioGlyphChangeSet Finish() => _inline
                ? new TextStudioGlyphChangeSet(_outgoingCount, _incomingCount,
                    _inlineOutgoingChanged, _inlineIncomingChanged,
                    _inlineOutgoingMapLow, _inlineOutgoingMapHigh,
                    _inlineIncomingMapLow, _inlineIncomingMapHigh)
                : new TextStudioGlyphChangeSet(_outgoingChanged, _incomingChanged,
                    _outgoingToIncoming, _incomingToOutgoing);
        }

        internal static TextStudioGlyphChangeSet Build(
            TextStudioCompiledPlan outgoing,
            TextStudioCompiledPlan incoming,
            TextStudioTextChangeScope scope,
            string decimalSeparator = ".")
        {
            int outgoingCount = outgoing?.VisibleGlyphCount ?? 0;
            int incomingCount = incoming?.VisibleGlyphCount ?? 0;
            MapBuilder map = new MapBuilder(outgoingCount, incomingCount);
            if (scope == TextStudioTextChangeScope.WholeText ||
                outgoingCount == 0 ||
                incomingCount == 0)
                return map.Finish();

            if (scope == TextStudioTextChangeScope.NumericValue &&
                TryRetainNumericValue(outgoing, incoming, decimalSeparator, ref map))
                return map.Finish();

            int prefix = 0;
            int sharedLimit = Math.Min(outgoingCount, incomingCount);
            while (prefix < sharedLimit && UnitsEqual(outgoing, prefix, incoming, prefix))
            {
                map.RetainPair(prefix, prefix);
                prefix++;
            }

            int outgoingSuffix = outgoingCount - 1;
            int incomingSuffix = incomingCount - 1;
            while (outgoingSuffix >= prefix &&
                   incomingSuffix >= prefix &&
                   UnitsEqual(outgoing, outgoingSuffix, incoming, incomingSuffix))
            {
                map.RetainPair(outgoingSuffix, incomingSuffix);
                outgoingSuffix--;
                incomingSuffix--;
            }

            int outgoingMiddleCount = outgoingSuffix - prefix + 1;
            int incomingMiddleCount = incomingSuffix - prefix + 1;
            if (outgoingMiddleCount > 0 && incomingMiddleCount > 0)
            {
                RetainMiddleCorrespondence(
                    outgoing,
                    incoming,
                    prefix,
                    outgoingMiddleCount,
                    incomingMiddleCount,
                    ref map);
            }

            return map.Finish();
        }

        static bool[] CreateChangedMask(int count)
        {
            if (count <= 0)
                return Array.Empty<bool>();
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
                result[i] = true;
            return result;
        }

        static int[] CreateUnmappedIndices(int count)
        {
            if (count <= 0)
                return Array.Empty<int>();
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = -1;
            return result;
        }

        static bool UnitsEqual(
            TextStudioCompiledPlan left,
            int leftUnitIndex,
            TextStudioCompiledPlan right,
            int rightUnitIndex)
        {
            if (left == null || right == null)
                return false;
            int leftStart = left.GetResolvedStart(leftUnitIndex);
            int rightStart = right.GetResolvedStart(rightUnitIndex);
            int leftLength = left.GetResolvedLength(leftUnitIndex);
            int rightLength = right.GetResolvedLength(rightUnitIndex);
            if (leftLength != rightLength || leftLength < 0)
                return false;

            string leftText = left.ResolvedText ?? string.Empty;
            string rightText = right.ResolvedText ?? string.Empty;
            if (leftStart < 0 || rightStart < 0 ||
                leftStart + leftLength > leftText.Length ||
                rightStart + rightLength > rightText.Length)
            {
                return false;
            }

            return string.CompareOrdinal(
                leftText,
                leftStart,
                rightText,
                rightStart,
                leftLength) == 0;
        }
    }

}
