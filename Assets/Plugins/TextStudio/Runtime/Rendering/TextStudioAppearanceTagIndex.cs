using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>Source-only interval lookup. Repeated names do not introduce per-glyph scans of source spans.</summary>
    internal sealed class TextStudioAppearanceTagIndex
    {
        // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(TextStudioIgnoreCaseComparer.Instance);
        internal TextStudioAppearanceTagIndex(TextStudioCompiledPlan plan)
        {
            if (plan == null || plan.AppearanceSpanCount == 0) return;
            Dictionary<string, List<TextStudioAppearanceRange>> ranges = new Dictionary<string, List<TextStudioAppearanceRange>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < plan.AppearanceSpanCount; index++)
            {
                TextStudioAppearanceSpan span = plan.AppearanceSpans[index];
                if (string.IsNullOrEmpty(span.Id) || span.Length <= 0) continue;
                if (!ranges.TryGetValue(span.Id, out List<TextStudioAppearanceRange> named))
                { named = new List<TextStudioAppearanceRange>(); ranges.Add(span.Id, named); }
                named.Add(new TextStudioAppearanceRange(span.Start, span.Length));
            }
            foreach (KeyValuePair<string, List<TextStudioAppearanceRange>> named in ranges)
            {
                named.Value.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));
                _entries.Add(named.Key, new Entry(named.Value.ToArray()));
            }
        }

        internal bool TryGetRange(string id, int unit, out TextStudioAppearanceRange range)
        {
            range = default;
            return !string.IsNullOrEmpty(id) && _entries.TryGetValue(id, out Entry entry) && entry.TryFind(unit, out range);
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = 0;
            foreach (KeyValuePair<string, Entry> entry in _entries)
                bytes += memory.Text(entry.Key) + entry.Value.CaptureManagedPayload(memory);
            return bytes;
        }

        sealed class Entry
        {
            readonly TextStudioAppearanceRange[] _ranges;
            readonly int[] _maximumEnds;
            internal Entry(TextStudioAppearanceRange[] ranges)
            {
                _ranges = ranges; _maximumEnds = new int[ranges.Length];
                int end = 0;
                for (int index = 0; index < ranges.Length; index++) _maximumEnds[index] = end = Math.Max(end, ranges[index].End);
            }
            internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory) => memory.Array(_ranges) + memory.Array(_maximumEnds);
            internal bool TryFind(int unit, out TextStudioAppearanceRange range)
            {
                int low = 0, high = _ranges.Length;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (_maximumEnds[middle] <= unit) low = middle + 1;
                    else high = middle;
                }
                range = low < _ranges.Length ? _ranges[low] : default;
                return low < _ranges.Length && range.Contains(unit);
            }
        }
    }
}
