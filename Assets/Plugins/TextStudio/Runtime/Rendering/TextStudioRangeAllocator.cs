using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>First-fit record ranges inside a growable shared GPU payload. Freed neighbours merge, so reuse stays compact.</summary>
    internal sealed class TextStudioRangeAllocator
    {
        readonly List<Range> _free = new List<Range>();

        internal int Capacity { get; private set; }
        internal int UsedCount { get; private set; }

        internal bool TryAllocate(int count, out int start)
        {
            start = 0;
            if (count <= 0) return true;
            for (int index = 0; index < _free.Count; index++)
            {
                Range range = _free[index];
                if (range.Count < count) continue;
                start = range.Start;
                if (range.Count == count) _free.RemoveAt(index);
                else _free[index] = new Range(range.Start + count, range.Count - count);
                UsedCount += count;
                return true;
            }
            return false;
        }

        internal void Free(int start, int count)
        {
            if (count <= 0) return;
            UsedCount -= count;
            int index = 0;
            while (index < _free.Count && _free[index].Start < start) index++;
            _free.Insert(index, new Range(start, count));
            if (index + 1 < _free.Count && _free[index].End == _free[index + 1].Start)
            {
                _free[index] = new Range(_free[index].Start, _free[index].Count + _free[index + 1].Count);
                _free.RemoveAt(index + 1);
            }
            if (index > 0 && _free[index - 1].End == _free[index].Start)
            {
                _free[index - 1] = new Range(_free[index - 1].Start, _free[index - 1].Count + _free[index].Count);
                _free.RemoveAt(index);
            }
        }

        /// <summary>Extends the payload; the new tail merges with a trailing free range.</summary>
        internal void Grow(int capacity)
        {
            if (capacity <= Capacity) return;
            int added = capacity - Capacity;
            int start = Capacity;
            Capacity = capacity;
            UsedCount += added;
            Free(start, added);
        }

        internal void Clear()
        {
            _free.Clear();
            Capacity = 0;
            UsedCount = 0;
        }

        readonly struct Range
        {
            internal readonly int Start, Count;
            internal int End => Start + Count;
            internal Range(int start, int count) { Start = start; Count = count; }
        }
    }
}
