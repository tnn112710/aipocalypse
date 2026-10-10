using System;
using System.Collections.Generic;

namespace TextStudio
{
    internal readonly struct TextStudioSelectionMatch
    {
        internal readonly int SourceNode;
        internal readonly int SourceOccurrence;
        internal readonly int Start;
        internal readonly int End;

        internal TextStudioSelectionMatch(int sourceNode, int sourceOccurrence, int start, int end)
        {
            SourceNode = sourceNode;
            SourceOccurrence = sourceOccurrence;
            Start = start;
            End = end;
        }
    }

    /// <summary>Reusable semantic membership and logical occurrence ownership before rendering.</summary>
    internal sealed class TextStudioSelectionResolver
    {
        const int NoMatch = -1;
        const int DerivedMatch = -2;
        const int NotEvaluated = -1;
        readonly List<TextStudioSelectionMatch> _matches = new List<TextStudioSelectionMatch>();
        readonly Dictionary<long, int> _clusterOwners = new Dictionary<long, int>();
        readonly Dictionary<ScopeKey, int> _scopedSlots = new Dictionary<ScopeKey, int>();
        int[] _owners = Array.Empty<int>();
        int[] _nodeFirst = Array.Empty<int>();
        int[] _nodeCount = Array.Empty<int>();
        int[] _rootSlots = Array.Empty<int>();
        int[] _slotOrigins = Array.Empty<int>();
        byte[] _selectedSlab = Array.Empty<byte>();
        int[] _ownerSlab = Array.Empty<int>();
        int _slotCount;
        int _slabLength;
        TextStudioCompiledSelector _selector;
        TextStudioCompiledPlan _plan;
        int _scopeStart;
        int _scopeEnd;

        internal int UnitCount { get; private set; }
        internal int MatchCount => _matches.Count;
        internal int GetOwner(int unit) => unit >= 0 && unit < UnitCount ? _owners[unit] : NoMatch;
        internal TextStudioSelectionMatch GetMatch(int index) => _matches[index];

        internal void Resolve(in TextStudioCompiledSelector selector, TextStudioCompiledPlan plan,
            TextStudioVisualGlyph[] visualGlyphs = null, int visualGlyphCount = 0,
            int scopeStart = 0, int scopeEnd = int.MaxValue)
        {
            _selector = selector;
            _plan = plan;
            UnitCount = plan != null ? Math.Max(plan.TextUnitCount, plan.VisibleGlyphCount) : 0;
            _scopeStart = Math.Max(0, Math.Min(UnitCount, scopeStart));
            _scopeEnd = Math.Max(_scopeStart, Math.Min(UnitCount, scopeEnd));
            Ensure(ref _owners, UnitCount);
            Ensure(ref _nodeFirst, selector.NodeCount);
            Ensure(ref _nodeCount, selector.NodeCount);
            Ensure(ref _rootSlots, selector.NodeCount);
            _matches.Clear();
            _scopedSlots.Clear();
            _slotCount = 0;
            _slabLength = 0;
            Array.Fill(_owners, NoMatch, 0, UnitCount);
            if (plan == null || selector.NodeCount == 0)
                return;

            BuildMatches();
            Array.Fill(_rootSlots, NotEvaluated, 0, selector.NodeCount);
            if (_scopeStart < _scopeEnd)
            {
                int slot = EvaluateScope(selector.Root, _scopeStart, _scopeEnd, 0);
                int origin = _slotOrigins[slot];
                for (int unit = _scopeStart; unit < _scopeEnd; unit++)
                    _owners[unit] = _selectedSlab[origin + unit] != 0 ? _ownerSlab[origin + unit] : NoMatch;
            }
            CloseClusters(visualGlyphs, visualGlyphCount);
            int derived = NoMatch;
            for (int unit = 0; unit < UnitCount; unit++)
            {
                if (_owners[unit] != DerivedMatch)
                {
                    derived = NoMatch;
                    continue;
                }
                if (derived < 0)
                {
                    derived = _matches.Count;
                    _matches.Add(new TextStudioSelectionMatch(-1, derived, unit, unit + 1));
                }
                else
                {
                    TextStudioSelectionMatch match = _matches[derived];
                    _matches[derived] = new TextStudioSelectionMatch(-1, derived, match.Start, unit + 1);
                }
                _owners[unit] = derived;
            }
        }

        void BuildMatches()
        {
            for (int nodeIndex = 0; nodeIndex < _selector.NodeCount; nodeIndex++)
            {
                TextStudioApplicationSelectorNode node = _selector.GetNode(nodeIndex);
                _nodeFirst[nodeIndex] = _matches.Count;
                if (node.Kind == TextStudioSelectorNodeKind.Range)
                {
                    ResolveRange(in node, _scopeStart, _scopeEnd, out int start, out int end);
                    _matches.Add(new TextStudioSelectionMatch(nodeIndex, 0, start, end));
                }
                else if (node.Kind == TextStudioSelectorNodeKind.AppearanceTag)
                    AddAppearanceMatches(nodeIndex, node.Key);
                else if (node.Kind == TextStudioSelectorNodeKind.Tag)
                    AddTagMatches(nodeIndex, node.Key);
                else if (node.Kind == TextStudioSelectorNodeKind.Within &&
                         node.Left >= 0 && node.Left < _selector.NodeCount)
                {
                    TextStudioApplicationSelectorNode parent = _selector.GetNode(node.Left);
                    if (parent.Kind == TextStudioSelectorNodeKind.AppearanceTag && !parent.Invert)
                        AddAppearanceMatches(nodeIndex, parent.Key);
                    else if (parent.Kind == TextStudioSelectorNodeKind.Tag && !parent.Invert)
                        AddTagMatches(nodeIndex, parent.Key);
                }
                _nodeCount[nodeIndex] = _matches.Count - _nodeFirst[nodeIndex];
            }
        }

        void AddAppearanceMatches(int nodeIndex, string key)
        {
            for (int index = 0; index < _plan.AppearanceSpanCount; index++)
            {
                TextStudioAppearanceSpan span = _plan.AppearanceSpans[index];
                if (!string.Equals(span.Id, key, StringComparison.OrdinalIgnoreCase)) continue;
                int start = Math.Max(_scopeStart, Math.Min(_scopeEnd, span.Start));
                int end = (int)Math.Max(start, Math.Min(_scopeEnd, (long)span.Start + span.Length));
                if (start < end) _matches.Add(new TextStudioSelectionMatch(nodeIndex, index, start, end));
            }
        }

        void AddTagMatches(int nodeIndex, string key)
        {
            RegionToGlyphMap[] regions = _plan.RegionMaps;
            if (regions == null) return;
            int count = Math.Min(_plan.RegionMapCount, regions.Length);
            for (int index = 0; index < count; index++)
            {
                RegionToGlyphMap region = regions[index];
                if (!string.Equals(region.regionName, key, StringComparison.Ordinal)) continue;
                int start = Math.Max(_scopeStart, Math.Min(_scopeEnd, region.glyphStart));
                int end = (int)Math.Max(start, Math.Min(_scopeEnd, (long)region.glyphStart + region.glyphCount));
                if (start == end) continue;
                _matches.Add(new TextStudioSelectionMatch(nodeIndex, index, start, end));
            }
        }

        // Each node is evaluated once per distinct scope within one Resolve call, so shared DAG nodes and repeated
        // Within occurrences reuse a slot. A slot stores selection and owner for every unit of its scope.
        int EvaluateScope(int nodeIndex, int start, int end, int depth)
        {
            // Validated programs are acyclic and never nest deeper than their node count; an unvalidated cycle
            // selects nothing there instead of recursing without end.
            if (nodeIndex < 0 || nodeIndex >= _selector.NodeCount || depth >= _selector.NodeCount)
                return AllocateSlot(start, end, false);
            int slot;
            if (start == _scopeStart && end == _scopeEnd)
            {
                slot = _rootSlots[nodeIndex];
                if (slot == NotEvaluated)
                {
                    slot = EvaluateNode(nodeIndex, start, end, depth);
                    _rootSlots[nodeIndex] = slot;
                }
                return slot;
            }
            ScopeKey key = new ScopeKey(nodeIndex, start, end);
            if (!_scopedSlots.TryGetValue(key, out slot))
            {
                slot = EvaluateNode(nodeIndex, start, end, depth);
                _scopedSlots[key] = slot;
            }
            return slot;
        }

        int EvaluateNode(int nodeIndex, int start, int end, int depth)
        {
            TextStudioApplicationSelectorNode node = _selector.GetNode(nodeIndex);
            int slot;
            switch (node.Kind)
            {
                case TextStudioSelectorNodeKind.All:
                    slot = AllocateSlot(start, end, true);
                    break;
                case TextStudioSelectorNodeKind.Range:
                    slot = AllocateSlot(start, end, false);
                    AssignMatchOwners(nodeIndex, slot, start, end, false);
                    ResolveRange(in node, start, end, out int rangeStart, out int rangeEnd);
                    if (rangeStart < rangeEnd)
                        Array.Fill(_selectedSlab, (byte)1, _slotOrigins[slot] + rangeStart, rangeEnd - rangeStart);
                    break;
                case TextStudioSelectorNodeKind.Tag:
                case TextStudioSelectorNodeKind.AppearanceTag:
                    slot = AllocateSlot(start, end, false);
                    AssignMatchOwners(nodeIndex, slot, start, end, true);
                    break;
                case TextStudioSelectorNodeKind.And:
                case TextStudioSelectorNodeKind.Or:
                    int left = EvaluateScope(node.Left, start, end, depth + 1);
                    int right = EvaluateScope(node.Right, start, end, depth + 1);
                    slot = AllocateSlot(start, end);
                    Combine(node.Kind == TextStudioSelectorNodeKind.And, left, right, slot, start, end);
                    break;
                case TextStudioSelectorNodeKind.Not:
                    int child = EvaluateScope(node.Left, start, end, depth + 1);
                    slot = AllocateSlot(start, end, false);
                    int childOrigin = _slotOrigins[child];
                    int origin = _slotOrigins[slot];
                    for (int unit = start; unit < end; unit++)
                        _selectedSlab[origin + unit] = (byte)(_selectedSlab[childOrigin + unit] ^ 1);
                    break;
                case TextStudioSelectorNodeKind.Within:
                    slot = EvaluateWithin(nodeIndex, in node, start, end, depth);
                    break;
                default:
                    slot = AllocateSlot(start, end, false);
                    break;
            }
            if (node.Invert)
            {
                int origin = _slotOrigins[slot];
                for (int unit = start; unit < end; unit++) _selectedSlab[origin + unit] ^= 1;
                Array.Fill(_ownerSlab, DerivedMatch, origin + start, end - start);
            }
            return slot;
        }

        // The child is evaluated in each parent occurrence, clamped to the scope: the first Tag or AppearanceTag
        // match owning the unit, the resolved Range, or otherwise each maximal run of parent-selected units. The
        // owner is the Within's own match, else the parent's owner at that unit; the child's owner is discarded.
        // Child evaluation can grow the slabs, so they are read through the fields after each recursive call.
        int EvaluateWithin(int nodeIndex, in TextStudioApplicationSelectorNode node, int start, int end, int depth)
        {
            if (node.Left < 0 || node.Left >= _selector.NodeCount)
                return AllocateSlot(start, end, false);
            TextStudioApplicationSelectorNode parent = _selector.GetNode(node.Left);
            int parentSlot = EvaluateScope(node.Left, start, end, depth + 1);
            int slot = AllocateSlot(start, end, false);
            int parentOrigin = _slotOrigins[parentSlot];
            int origin = _slotOrigins[slot];
            if ((parent.Kind == TextStudioSelectorNodeKind.Tag || parent.Kind == TextStudioSelectorNodeKind.AppearanceTag) &&
                !parent.Invert)
            {
                int unit = start;
                while (unit < end)
                {
                    int occurrence = _ownerSlab[parentOrigin + unit];
                    int runStart = unit++;
                    while (unit < end && _ownerSlab[parentOrigin + unit] == occurrence) unit++;
                    if (occurrence < 0) continue;
                    TextStudioSelectionMatch match = _matches[occurrence];
                    int child = EvaluateScope(node.Right, Math.Max(start, match.Start), Math.Min(end, match.End), depth + 1);
                    CopySelection(child, slot, runStart, unit);
                }
            }
            else if (parent.Kind == TextStudioSelectorNodeKind.Range && !parent.Invert)
            {
                ResolveRange(in parent, start, end, out int rangeStart, out int rangeEnd);
                if (rangeStart < rangeEnd)
                    CopySelection(EvaluateScope(node.Right, rangeStart, rangeEnd, depth + 1), slot, rangeStart, rangeEnd);
            }
            else
            {
                int unit = start;
                while (unit < end)
                {
                    if (_selectedSlab[parentOrigin + unit] == 0)
                    {
                        unit++;
                        continue;
                    }
                    int runStart = unit++;
                    while (unit < end && _selectedSlab[parentOrigin + unit] != 0) unit++;
                    CopySelection(EvaluateScope(node.Right, runStart, unit, depth + 1), slot, runStart, unit);
                }
            }
            AssignMatchOwners(nodeIndex, slot, start, end, false);
            int[] owners = _ownerSlab;
            for (int unit = start; unit < end; unit++)
                if (owners[origin + unit] < 0) owners[origin + unit] = owners[parentOrigin + unit];
            return slot;
        }

        // And keeps its left operand's real owner, else the right one's; Or keeps the earlier selected owner.
        void Combine(bool intersect, int left, int right, int slot, int start, int end)
        {
            byte[] selected = _selectedSlab;
            int[] owners = _ownerSlab;
            int leftOrigin = _slotOrigins[left];
            int rightOrigin = _slotOrigins[right];
            int origin = _slotOrigins[slot];
            for (int unit = start; unit < end; unit++)
            {
                bool leftSelected = selected[leftOrigin + unit] != 0;
                bool rightSelected = selected[rightOrigin + unit] != 0;
                int leftOwner = owners[leftOrigin + unit];
                int rightOwner = owners[rightOrigin + unit];
                if (intersect)
                {
                    selected[origin + unit] = (byte)(leftSelected && rightSelected ? 1 : 0);
                    owners[origin + unit] = leftOwner >= 0 ? leftOwner : rightOwner;
                }
                else
                {
                    selected[origin + unit] = (byte)(leftSelected || rightSelected ? 1 : 0);
                    owners[origin + unit] = Earlier(leftSelected ? leftOwner : NoMatch, rightSelected ? rightOwner : NoMatch);
                }
            }
        }

        // Equivalent to taking, per unit, the first of the node's matches in plan order that contains it.
        void AssignMatchOwners(int nodeIndex, int slot, int start, int end, bool select)
        {
            int origin = _slotOrigins[slot];
            int last = _nodeFirst[nodeIndex] + _nodeCount[nodeIndex];
            for (int index = _nodeFirst[nodeIndex]; index < last; index++)
            {
                TextStudioSelectionMatch match = _matches[index];
                int matchEnd = Math.Min(end, match.End);
                for (int unit = Math.Max(start, match.Start); unit < matchEnd; unit++)
                {
                    if (_ownerSlab[origin + unit] >= 0) continue;
                    _ownerSlab[origin + unit] = index;
                    if (select) _selectedSlab[origin + unit] = 1;
                }
            }
        }

        void CopySelection(int from, int to, int start, int end)
        {
            Array.Copy(_selectedSlab, _slotOrigins[from] + start, _selectedSlab, _slotOrigins[to] + start, end - start);
        }

        int AllocateSlot(int start, int end, bool selected)
        {
            int slot = AllocateSlot(start, end);
            int origin = _slotOrigins[slot];
            Array.Fill(_selectedSlab, (byte)(selected ? 1 : 0), origin + start, end - start);
            Array.Fill(_ownerSlab, DerivedMatch, origin + start, end - start);
            return slot;
        }

        int AllocateSlot(int start, int end)
        {
            int length = end - start;
            Ensure(ref _slotOrigins, _slotCount + 1);
            Ensure(ref _selectedSlab, _slabLength + length);
            Ensure(ref _ownerSlab, _slabLength + length);
            _slotOrigins[_slotCount] = _slabLength - start;
            _slabLength += length;
            return _slotCount++;
        }

        static void ResolveRange(in TextStudioApplicationSelectorNode node, int scopeStart, int scopeEnd,
            out int start, out int end)
        {
            int localStart;
            int localEnd;
            if (node.UsesTextRange)
                node.TextRange.Resolve(scopeEnd - scopeStart, out localStart, out localEnd);
            else
            {
                localStart = Math.Max(0, node.Start);
                localEnd = (int)Math.Max(int.MinValue, Math.Min((long)scopeEnd - scopeStart, (long)node.Start + node.Count));
            }
            // Saturate so a start past the integer limit stays beyond the scope instead of wrapping in front of it.
            start = (int)Math.Min(int.MaxValue, (long)scopeStart + localStart);
            end = scopeStart + localEnd;
        }

        int Earlier(int first, int second)
        {
            if (first == NoMatch) return second;
            if (second == NoMatch) return first;
            if (first == DerivedMatch) return second;
            if (second == DerivedMatch) return first;
            TextStudioSelectionMatch a = _matches[first];
            TextStudioSelectionMatch b = _matches[second];
            return a.Start != b.Start ? (a.Start < b.Start ? first : second) : Math.Min(first, second);
        }

        void CloseClusters(TextStudioVisualGlyph[] glyphs, int count)
        {
            _clusterOwners.Clear();
            if (glyphs == null) return;
            int limit = Math.Min(count, glyphs.Length);
            for (int index = 0; index < limit; index++)
            {
                TextStudioVisualGlyph glyph = glyphs[index];
                int unit = glyph.TextUnitIndex;
                if (unit < 0 || unit >= UnitCount) continue;
                long key = ((long)glyph.ClusterStart << 32) | (uint)Math.Max(1, glyph.ClusterLength);
                int candidate = _owners[unit];
                _clusterOwners[key] = _clusterOwners.TryGetValue(key, out int prior)
                    ? Earlier(prior, candidate) : candidate;
            }
            for (int index = 0; index < limit; index++)
            {
                TextStudioVisualGlyph glyph = glyphs[index];
                int unit = glyph.TextUnitIndex;
                if (unit < 0 || unit >= UnitCount) continue;
                long key = ((long)glyph.ClusterStart << 32) | (uint)Math.Max(1, glyph.ClusterLength);
                _owners[unit] = Earlier(_owners[unit], _clusterOwners[key]);
            }
        }

        static void Ensure<T>(ref T[] buffer, int count)
        {
            if (buffer.Length < count) Array.Resize(ref buffer, TextStudioBufferCapacity.ForCount(count));
        }

        readonly struct ScopeKey : IEquatable<ScopeKey>
        {
            readonly int _node;
            readonly int _start;
            readonly int _end;

            internal ScopeKey(int node, int start, int end)
            {
                _node = node;
                _start = start;
                _end = end;
            }

            public bool Equals(ScopeKey other) => _node == other._node && _start == other._start && _end == other._end;
            public override bool Equals(object obj) => obj is ScopeKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_node, _start, _end);
        }
    }
}
