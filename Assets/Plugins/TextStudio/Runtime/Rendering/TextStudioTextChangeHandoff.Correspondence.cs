using System;

namespace TextStudio
{
    internal static partial class TextStudioGlyphChangeSetBuilder
    {
        // Middles above the LCS table limit use exact edit-distance frontiers within these bounds. Larger rewrites keep
        // the retained prefix and suffix and treat the whole middle as changed.
        const int MaximumEditDistance = 360;
        const int MaximumEditComparisons = 1 << 20;
        const int MaximumFrontierCount = (MaximumEditDistance + 1) * (MaximumEditDistance + 2) / 2;
        const int FirstClusterKey = 0x10000;

        static void RetainMiddleCorrespondence(
            TextStudioCompiledPlan outgoing,
            TextStudioCompiledPlan incoming,
            int start,
            int outgoingCount,
            int incomingCount,
            ref MapBuilder map)
        {
            if (map.IsInline)
            {
                Span<int> inlineKeys = stackalloc int[InlineGlyphLimit * 2];
                Span<byte> inlineLengths = stackalloc byte[(InlineGlyphLimit + 1) * (InlineGlyphLimit + 1)];
                GlyphKeys inlineGlyphs = CreateGlyphKeys(outgoing, incoming, start, outgoingCount, incomingCount,
                    inlineKeys);
                RetainLongestCommonSubsequence(in inlineGlyphs, inlineLengths, ref map);
                return;
            }

            CorrespondenceWorkspace workspace = CorrespondenceWorkspace.Acquire();
            try
            {
                GlyphKeys keys = CreateGlyphKeys(outgoing, incoming, start, outgoingCount, incomingCount,
                    workspace.RentKeys(outgoingCount + incomingCount));
                long cells = (long)(outgoingCount + 1) * (incomingCount + 1);
                if (cells <= MaximumLcsCells)
                    RetainLongestCommonSubsequence(in keys, workspace.RentLengths((int)cells), ref map);
                else
                    RetainShortestEditScript(in keys, workspace, ref map);
            }
            finally
            {
                CorrespondenceWorkspace.Release(workspace);
            }
        }

        static GlyphKeys CreateGlyphKeys(
            TextStudioCompiledPlan outgoing,
            TextStudioCompiledPlan incoming,
            int start,
            int outgoingCount,
            int incomingCount,
            Span<int> storage)
        {
            Span<int> outgoingKeys = storage.Slice(0, outgoingCount);
            Span<int> incomingKeys = storage.Slice(outgoingCount, incomingCount);
            for (int i = 0; i < outgoingCount; i++)
                outgoingKeys[i] = GetGlyphKey(outgoing, start + i, -1);
            for (int i = 0; i < incomingCount; i++)
                incomingKeys[i] = GetGlyphKey(incoming, start + i, -2);
            return new GlyphKeys(outgoing, incoming, start, outgoingKeys, incomingKeys);
        }

        // A single UTF-16 unit is its own key; longer clusters share a hash key and confirm with UnitsEqual.
        // Spans that UnitsEqual rejects get a side-specific key that never matches the other side.
        static int GetGlyphKey(TextStudioCompiledPlan plan, int unitIndex, int invalidKey)
        {
            int resolvedStart = plan.GetResolvedStart(unitIndex);
            int length = plan.GetResolvedLength(unitIndex);
            string text = plan.ResolvedText ?? string.Empty;
            if (length < 0 || resolvedStart < 0 || resolvedStart + length > text.Length)
                return invalidKey;
            if (length == 1)
                return text[resolvedStart];

            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
                hash = (hash ^ text[resolvedStart + i]) * 16777619u;
            return FirstClusterKey + (int)(hash & 0x3FFFFFFFu);
        }

        static void RetainLongestCommonSubsequence(in GlyphKeys keys, Span<byte> lengths, ref MapBuilder map)
        {
            // The table limit keeps the shorter middle at 255 glyphs or fewer, so every length fits in a byte.
            int outgoingCount = keys.OutgoingCount;
            int incomingCount = keys.IncomingCount;
            int columns = incomingCount + 1;
            // Every other cell is written before it is read, so reused storage only needs the last row and column.
            lengths.Slice(outgoingCount * columns, columns).Clear();
            for (int outgoingOffset = 0; outgoingOffset < outgoingCount; outgoingOffset++)
                lengths[outgoingOffset * columns + incomingCount] = 0;

            for (int outgoingOffset = outgoingCount - 1; outgoingOffset >= 0; outgoingOffset--)
            {
                int row = outgoingOffset * columns;
                int nextRow = row + columns;
                for (int incomingOffset = incomingCount - 1; incomingOffset >= 0; incomingOffset--)
                {
                    lengths[row + incomingOffset] = keys.Equal(outgoingOffset, incomingOffset)
                        ? (byte)(lengths[nextRow + incomingOffset + 1] + 1)
                        : Math.Max(lengths[nextRow + incomingOffset], lengths[row + incomingOffset + 1]);
                }
            }

            int oldOffset = 0;
            int newOffset = 0;
            while (oldOffset < outgoingCount && newOffset < incomingCount)
            {
                if (keys.Equal(oldOffset, newOffset))
                {
                    map.RetainPair(keys.Start + oldOffset, keys.Start + newOffset);
                    oldOffset++;
                    newOffset++;
                    continue;
                }

                int skipOld = lengths[(oldOffset + 1) * columns + newOffset];
                int skipNew = lengths[oldOffset * columns + newOffset + 1];
                if (skipOld >= skipNew)
                    oldOffset++;
                else
                    newOffset++;
            }
        }

        // Reproduces the table traceback without the table. Level d records, for each diagonal k = x - y with the parity
        // of d, the largest x such that the last x outgoing and last x - k incoming middle glyphs differ by at most d
        // insertions and deletions. That distance never decreases along a diagonal, so a pair of suffixes is within d
        // edits exactly when it lies at or before the diagonal's frontier, which answers the traceback's tie test.
        static void RetainShortestEditScript(in GlyphKeys keys, CorrespondenceWorkspace workspace, ref MapBuilder map)
        {
            int outgoingCount = keys.OutgoingCount;
            int incomingCount = keys.IncomingCount;
            int endDiagonal = outgoingCount - incomingCount;
            if (Math.Abs(endDiagonal) > MaximumEditDistance)
                return;

            int[] frontiers = null;
            int comparisons = 0;
            int distance = -1;
            for (int level = 0; level <= MaximumEditDistance && distance < 0; level++)
            {
                int levelStart = level * (level + 1) / 2;
                int previousStart = levelStart - level;
                int olderStart = previousStart - level + 1;
                frontiers = workspace.GetFrontiers(levelStart + level + 1);
                int diagonal = Math.Max(-level, -incomingCount);
                if (((diagonal + level) & 1) != 0)
                    diagonal++;
                int lastDiagonal = Math.Min(level, outgoingCount);
                for (; diagonal <= lastDiagonal; diagonal += 2)
                {
                    int slot = (diagonal + level) >> 1;
                    int x = 0;
                    if (level > 0)
                    {
                        // A step blocked by the grid edge clamps to the end of this diagonal, which is adjacent to the
                        // neighbouring frontier and therefore within one more edit.
                        x = -1;
                        if (diagonal > -level && diagonal > -incomingCount)
                            x = Math.Min(frontiers[previousStart + slot - 1] + 1, outgoingCount);
                        if (diagonal < level && diagonal < outgoingCount)
                            x = Math.Max(x, Math.Min(frontiers[previousStart + slot], incomingCount + diagonal));
                        if (diagonal > 1 - level && diagonal < level - 1)
                            x = Math.Max(x, frontiers[olderStart + slot - 1]);
                    }

                    int y = x - diagonal;
                    while (x < outgoingCount && y < incomingCount)
                    {
                        if (++comparisons > MaximumEditComparisons)
                            return;
                        if (!keys.Equal(outgoingCount - 1 - x, incomingCount - 1 - y))
                            break;
                        x++;
                        y++;
                    }
                    frontiers[levelStart + slot] = x;
                }

                if (level >= Math.Abs(endDiagonal) && ((level + endDiagonal) & 1) == 0 &&
                    frontiers[levelStart + ((endDiagonal + level) >> 1)] == outgoingCount)
                    distance = level;
            }

            if (distance < 0)
                return;

            int oldOffset = 0;
            int newOffset = 0;
            int remaining = distance;
            while (oldOffset < outgoingCount && newOffset < incomingCount)
            {
                if (keys.Equal(oldOffset, newOffset))
                {
                    map.RetainPair(keys.Start + oldOffset, keys.Start + newOffset);
                    oldOffset++;
                    newOffset++;
                    continue;
                }

                // Like the table traceback, skip the outgoing glyph whenever that still finishes in the remaining edits.
                remaining--;
                int x = outgoingCount - oldOffset - 1;
                int diagonal = x - (incomingCount - newOffset);
                if (diagonal >= -remaining && diagonal <= remaining &&
                    x <= frontiers[remaining * (remaining + 1) / 2 + ((diagonal + remaining) >> 1)])
                    oldOffset++;
                else
                    newOffset++;
            }
        }

        readonly ref struct GlyphKeys
        {
            readonly TextStudioCompiledPlan _outgoing;
            readonly TextStudioCompiledPlan _incoming;
            readonly ReadOnlySpan<int> _outgoingKeys;
            readonly ReadOnlySpan<int> _incomingKeys;

            internal GlyphKeys(
                TextStudioCompiledPlan outgoing,
                TextStudioCompiledPlan incoming,
                int start,
                ReadOnlySpan<int> outgoingKeys,
                ReadOnlySpan<int> incomingKeys)
            {
                _outgoing = outgoing;
                _incoming = incoming;
                Start = start;
                _outgoingKeys = outgoingKeys;
                _incomingKeys = incomingKeys;
            }

            internal int Start { get; }
            internal int OutgoingCount => _outgoingKeys.Length;
            internal int IncomingCount => _incomingKeys.Length;

            internal bool Equal(int outgoingOffset, int incomingOffset)
            {
                int key = _outgoingKeys[outgoingOffset];
                return key == _incomingKeys[incomingOffset] &&
                       (key < FirstClusterKey ||
                        UnitsEqual(_outgoing, Start + outgoingOffset, _incoming, Start + incomingOffset));
            }
        }

#if UNITY_6000_7_OR_NEWER
        // Scratch only: every build initializes what it reads, and published change sets never reference these arrays.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        sealed partial class CorrespondenceWorkspace
        {
            const int MinimumCapacity = 256;
            const int RetainedKeyCount = 16384;

            [ThreadStatic] static CorrespondenceWorkspace s_shared;
            [ThreadStatic] static bool s_sharedInUse;

            int[] _keys;
            byte[] _lengths;
            int[] _frontiers;

            internal static CorrespondenceWorkspace Acquire()
            {
                if (s_sharedInUse)
                    return new CorrespondenceWorkspace();
                s_shared ??= new CorrespondenceWorkspace();
                s_sharedInUse = true;
                return s_shared;
            }

            internal static void Release(CorrespondenceWorkspace workspace)
            {
                if (ReferenceEquals(workspace, s_shared))
                    s_sharedInUse = false;
            }

            // Keys for unusually long middles stay temporary so one outlier does not pin a large buffer to the thread.
            internal Span<int> RentKeys(int count)
            {
                if (count > RetainedKeyCount)
                    return new int[count];
                if (_keys == null || _keys.Length < count)
                    _keys = new int[GrowCapacity(_keys?.Length ?? 0, count, RetainedKeyCount)];
                return new Span<int>(_keys, 0, count);
            }

            internal Span<byte> RentLengths(int count)
            {
                if (_lengths == null || _lengths.Length < count)
                    _lengths = new byte[GrowCapacity(_lengths?.Length ?? 0, count, MaximumLcsCells)];
                return new Span<byte>(_lengths, 0, count);
            }

            internal int[] GetFrontiers(int count)
            {
                if (_frontiers != null && _frontiers.Length >= count)
                    return _frontiers;
                int[] frontiers = new int[GrowCapacity(_frontiers?.Length ?? 0, count, MaximumFrontierCount)];
                if (_frontiers != null)
                    Array.Copy(_frontiers, frontiers, _frontiers.Length);
                _frontiers = frontiers;
                return frontiers;
            }

            static int GrowCapacity(int current, int required, int limit) =>
                Math.Min(limit, Math.Max(required, Math.Max(MinimumCapacity, current * 2)));
        }
    }
}
