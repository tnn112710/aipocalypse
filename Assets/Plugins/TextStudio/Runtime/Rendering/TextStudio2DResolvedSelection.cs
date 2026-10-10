using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    internal enum TextStudio2DMatchProvenance : byte
    {
        Region,
        Range,
        Derived
    }

    /// <summary>One logical occurrence. Members are semantic units, never UTF-16 offsets.</summary>
    internal readonly struct TextStudio2DResolvedMatch
    {
        internal readonly int SourceNode;
        internal readonly int SourceOccurrence;
        internal readonly int LogicalStart;
        internal readonly int LogicalEnd;
        internal readonly TextStudio2DMatchProvenance Provenance;

        internal TextStudio2DResolvedMatch(int node, int occurrence, int start, int end, TextStudio2DMatchProvenance provenance)
        {
            SourceNode = node;
            SourceOccurrence = occurrence;
            LogicalStart = start;
            LogicalEnd = end;
            Provenance = provenance;
        }
    }

    internal readonly struct TextStudio2DVisualFragment
    {
        internal readonly int MatchIndex;
        internal readonly int LineIndex;
        internal readonly int FirstMember;
        internal readonly int MemberCount;
        internal readonly Vector2 Direction;
        internal readonly Vector4 Bounds;

        internal TextStudio2DVisualFragment(int match, int line, int first, int count, Vector2 direction, Vector4 bounds)
        {
            MatchIndex = match;
            LineIndex = line;
            FirstMember = first;
            MemberCount = count;
            Direction = direction;
            Bounds = bounds;
        }
    }

    /// <summary>Reusable layout binding. Each selected cluster has one stable occurrence owner.</summary>
    internal sealed class TextStudio2DResolvedSelection
    {
        const int NoMatch = -1;
        const int DerivedMatch = -2;
        readonly List<TextStudio2DResolvedMatch> _matches = new List<TextStudio2DResolvedMatch>();
        readonly List<TextStudio2DVisualFragment> _fragments = new List<TextStudio2DVisualFragment>();
        readonly List<int> _members = new List<int>();
        readonly Dictionary<long, int> _clusterOwners = new Dictionary<long, int>();
        readonly TextStudioSelectionResolver _shared = new TextStudioSelectionResolver();
        readonly TextStudio2DSelectorPass _pass = new TextStudio2DSelectorPass();
        int[] _owners = Array.Empty<int>();
        int[] _logicalOrder = Array.Empty<int>();
        int[] _visualOrder = Array.Empty<int>();
        int[] _nodeFirst = Array.Empty<int>();
        int[] _nodeCount = Array.Empty<int>();
        Vector4[] _bounds = Array.Empty<Vector4>();
        bool[] _hasBounds = Array.Empty<bool>();
        float[] _fragmentDistances = Array.Empty<float>();
        float[] _matchDistances = Array.Empty<float>();
        int[] _matchOrder = Array.Empty<int>();
        int[] _sweepHeads = Array.Empty<int>();
        int[] _sweepTails = Array.Empty<int>();
        long[] _ownerStamps = Array.Empty<long>();
        bool[] _ownerSelected = Array.Empty<bool>();
        int[] _ownerMatches = Array.Empty<int>();
        long _ownerGeneration;

        internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ArrayBytes(_owners) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_logicalOrder) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_visualOrder) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_nodeFirst) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_nodeCount) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_bounds) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_hasBounds) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_fragmentDistances) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_matchDistances) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_matchOrder) + TextStudioAppearanceManagedMemory.ArrayBytes(_sweepHeads) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_sweepTails) + TextStudioAppearanceManagedMemory.ArrayBytes(_ownerStamps) +
                TextStudioAppearanceManagedMemory.ArrayBytes(_ownerSelected) + TextStudioAppearanceManagedMemory.ArrayBytes(_ownerMatches) +
                _pass.ManagedPayloadBytes + TextStudioAppearanceManagedMemory.ListBytes(_matches) + TextStudioAppearanceManagedMemory.ListBytes(_fragments) +
                TextStudioAppearanceManagedMemory.ListBytes(_members) + (long)_clusterOwners.EnsureCapacity(0) * (sizeof(long) + sizeof(int));

        internal int GlyphCount { get; private set; }
        int _ownerOffset, _ownerCount;
        internal int UnitStart => _ownerOffset;
        internal int UnitEnd => _ownerOffset + _ownerCount;
        internal int MatchCount => _matches.Count;
        internal int FragmentCount => _fragments.Count;
        internal int GetOwner(int glyph) => glyph >= _ownerOffset && glyph < UnitEnd ? _owners[glyph - _ownerOffset] : NoMatch;
        internal TextStudio2DResolvedMatch GetMatch(int match) => _matches[match];
        internal TextStudio2DVisualFragment GetFragment(int fragment) => _fragments[fragment];
        internal int GetMember(int member) => _members[member];
        internal float GetFragmentDistance(int fragment) => _fragmentDistances[fragment];
        internal Vector4 GetBounds(int match) => _bounds[match];

        internal void Resolve(in TextStudio2DCompiledSelector selector, in TextStudioFrame frame)
        {
            _pass.Begin();
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            GlyphCount = snapshot.IsCreated ? snapshot.GlyphCount : frame.Plan.VisibleGlyphCount;
            TextStudioAppearanceOccurrence occurrence = frame.AppearanceBinding != null && frame.AppearanceOccurrenceIndex >= 0
                ? frame.AppearanceBinding.Plan.Occurrences[frame.AppearanceOccurrenceIndex] : null;
            _ownerOffset = occurrence != null ? Mathf.Clamp(occurrence.Domain.Start, 0, GlyphCount) : 0;
            _ownerCount = (occurrence != null ? Mathf.Clamp(occurrence.Domain.End, _ownerOffset, GlyphCount) : GlyphCount) - _ownerOffset;
            Ensure(ref _owners, _ownerCount);
            Ensure(ref _logicalOrder, _ownerCount);
            Ensure(ref _visualOrder, _ownerCount);
            Ensure(ref _nodeFirst, selector.NodeCount);
            Ensure(ref _nodeCount, selector.NodeCount);
            _matches.Clear();
            _fragments.Clear();
            _members.Clear();
            _clusterOwners.Clear();

            bool shared = !selector.HasDynamicMembership && occurrence == null;
            if (shared)
            {
                TextStudioCompiledSelector sharedSelector = selector.Shared;
                _shared.Resolve(in sharedSelector, frame.Plan.CompiledPlan);
                for (int match = 0; match < _shared.MatchCount; match++)
                {
                    TextStudioSelectionMatch source = _shared.GetMatch(match);
                    TextStudio2DMatchProvenance provenance = source.SourceNode < 0
                        ? TextStudio2DMatchProvenance.Derived
                        : selector.GetNode(source.SourceNode).Kind == TextStudioSelectorNodeKind.Range
                            ? TextStudio2DMatchProvenance.Range
                            : TextStudio2DMatchProvenance.Region;
                    _matches.Add(new TextStudio2DResolvedMatch(source.SourceNode,
                        source.SourceOccurrence, source.Start, source.End, provenance));
                }
            }
            else for (int nodeIndex = 0; nodeIndex < selector.NodeCount; nodeIndex++)
            {
                TextStudioApplicationSelectorNode node = selector.GetNode(nodeIndex);
                _nodeFirst[nodeIndex] = _matches.Count;
                if (node.Kind == TextStudioSelectorNodeKind.Range)
                {
                    int start;
                    int end;
                    if (node.UsesTextRange)
                        node.TextRange.Resolve(_ownerCount, out start, out end);
                    else
                    {
                        start = node.Start;
                        end = (int)Math.Min(int.MaxValue, (long)node.Start + node.Count);
                    }
                    _matches.Add(new TextStudio2DResolvedMatch(nodeIndex, 0, _ownerOffset + start, _ownerOffset + end, TextStudio2DMatchProvenance.Range));
                }
                else if (node.Kind == TextStudioSelectorNodeKind.Tag)
                {
                    TextStudioReadOnlyBuffer<RegionToGlyphMap> regions = frame.Plan.RegionMaps;
                    for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                    {
                        RegionToGlyphMap region = regions[regionIndex];
                        if (string.Equals(region.regionName, node.Key, StringComparison.Ordinal))
                            _matches.Add(new TextStudio2DResolvedMatch(nodeIndex, regionIndex, region.glyphStart, region.glyphStart + region.glyphCount, TextStudio2DMatchProvenance.Region));
                    }
                }
                else if (node.Kind == TextStudioSelectorNodeKind.AppearanceTag)
                {
                    AddAppearanceMatches(in frame, nodeIndex, node.Key);
                }
                else if (node.Kind == TextStudioSelectorNodeKind.Within &&
                         node.Left >= 0 && node.Left < selector.NodeCount)
                {
                    TextStudioApplicationSelectorNode parent = selector.GetNode(node.Left);
                    if (parent.Kind == TextStudioSelectorNodeKind.AppearanceTag && !parent.Invert)
                        AddAppearanceMatches(in frame, nodeIndex, parent.Key);
                    else if (parent.Kind == TextStudioSelectorNodeKind.Tag && !parent.Invert)
                    {
                        TextStudioReadOnlyBuffer<RegionToGlyphMap> regions = frame.Plan.RegionMaps;
                        for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                        {
                            RegionToGlyphMap region = regions[regionIndex];
                            if (string.Equals(region.regionName, parent.Key, StringComparison.Ordinal))
                                _matches.Add(new TextStudio2DResolvedMatch(nodeIndex, regionIndex,
                                    region.glyphStart, region.glyphStart + region.glyphCount,
                                    TextStudio2DMatchProvenance.Region));
                        }
                    }
                }
                _nodeCount[nodeIndex] = _matches.Count - _nodeFirst[nodeIndex];
            }
            if (!shared)
                PrepareOwnerEvaluation(selector.NodeCount);

            for (int glyph = _ownerOffset; glyph < UnitEnd; glyph++)
            {
                int owner = DerivedMatch;
                int unit = snapshot.IsCreated ? snapshot.GetGlyph(glyph).TextUnitIndex : glyph;
                bool selected = shared
                    ? (owner = _shared.GetOwner(unit)) >= 0
                    : selector.IsTrivialAll || EvaluateOwner(in selector, in frame, glyph, out owner);
                _owners[glyph - _ownerOffset] = selected ? owner : NoMatch;
                _logicalOrder[glyph - _ownerOffset] = glyph;
                _visualOrder[glyph - _ownerOffset] = glyph;
                if (!snapshot.IsCreated)
                    continue;
                long key = ClusterKey(snapshot.GetGlyph(glyph));
                if (_clusterOwners.TryGetValue(key, out int prior))
                    _clusterOwners[key] = Earlier(prior, _owners[glyph - _ownerOffset]);
                else
                    _clusterOwners.Add(key, _owners[glyph - _ownerOffset]);
            }
            if (snapshot.IsCreated)
            {
                for (int glyph = _ownerOffset; glyph < UnitEnd; glyph++)
                    _owners[glyph - _ownerOffset] = _clusterOwners[ClusterKey(snapshot.GetGlyph(glyph))];
                // Backend order is normally already monotonic; insertion sort then has linear cost.
                Sort(_logicalOrder, in snapshot, false);
                Sort(_visualOrder, in snapshot, true);
            }

            int derived = NoMatch;
            for (int index = 0; index < _ownerCount; index++)
            {
                int glyph = _logicalOrder[index];
                if (_owners[glyph - _ownerOffset] != DerivedMatch)
                {
                    derived = NoMatch;
                    continue;
                }
                if (derived < 0)
                {
                    derived = _matches.Count;
                    _matches.Add(new TextStudio2DResolvedMatch(-1, derived, glyph, glyph + 1, TextStudio2DMatchProvenance.Derived));
                }
                _owners[glyph - _ownerOffset] = derived;
                TextStudio2DResolvedMatch match = _matches[derived];
                _matches[derived] = new TextStudio2DResolvedMatch(-1, derived, match.LogicalStart, glyph + 1, match.Provenance);
            }
            Ensure(ref _bounds, _matches.Count);
            Ensure(ref _hasBounds, _matches.Count);
            Array.Clear(_hasBounds, 0, _matches.Count);
            if (occurrence != null)
            {
                for (int glyph = _ownerOffset; glyph < UnitEnd; glyph++)
                {
                    int owner = _owners[glyph - _ownerOffset];
                    if (owner < 0) continue;
                    if (snapshot.IsCreated)
                    {
                        TextStudioVisualGlyph visual = snapshot.GetGlyph(glyph);
                        Vector4 bounds = GlyphBounds(in visual, frame.GetEffectUnitScale(glyph));
                        _bounds[owner] = _hasBounds[owner] ? Union(_bounds[owner], bounds) : bounds;
                        _hasBounds[owner] = true;
                    }
                    if (!occurrence.ContainsVisibleUnit(glyph)) _owners[glyph - _ownerOffset] = NoMatch;
                }
            }
            if (snapshot.IsCreated)
                BuildFragments(in frame, occurrence == null);
        }

        void AddAppearanceMatches(in TextStudioFrame frame, int node, string id)
        {
            TextStudioReadOnlyBuffer<TextStudioAppearanceSpan> spans = frame.Plan.AppearanceSpans;
            for (int index = 0; index < spans.Length; index++)
            {
                TextStudioAppearanceSpan span = spans[index];
                if (string.Equals(span.Id, id, StringComparison.OrdinalIgnoreCase) && span.Start < UnitEnd && span.Start + span.Length > _ownerOffset)
                    _matches.Add(new TextStudio2DResolvedMatch(node, index, span.Start, span.Start + span.Length, TextStudio2DMatchProvenance.Region));
            }
        }

        void PrepareOwnerEvaluation(int nodeCount)
        {
            Ensure(ref _matchOrder, _matches.Count);
            Ensure(ref _sweepHeads, nodeCount);
            Ensure(ref _sweepTails, nodeCount);
            Ensure(ref _ownerStamps, nodeCount);
            Ensure(ref _ownerSelected, nodeCount);
            Ensure(ref _ownerMatches, nodeCount);
            for (int node = 0; node < nodeCount; node++)
            {
                int first = _nodeFirst[node];
                int end = first + _nodeCount[node];
                _sweepHeads[node] = _sweepTails[node] = first;
                // Occurrences are normally listed in text order; insertion sort then has linear cost.
                for (int match = first; match < end; match++)
                {
                    int start = _matches[match].LogicalStart;
                    int insertion = match;
                    while (insertion > first && _matches[_matchOrder[insertion - 1]].LogicalStart > start)
                    {
                        _matchOrder[insertion] = _matchOrder[insertion - 1];
                        insertion--;
                    }
                    _matchOrder[insertion] = match;
                }
            }
        }

        bool EvaluateOwner(in TextStudio2DCompiledSelector selector, in TextStudioFrame frame, int glyph, out int owner)
        {
            TextStudio2DSelectorEvaluator.Workspace workspace = selector.EvaluationWorkspace;
            workspace.BeginUnit(glyph, _ownerOffset, UnitEnd, _pass.Id);
            _ownerGeneration++;
            return Evaluate(workspace, in selector, selector.Root, in frame, glyph, 0, out owner);
        }

        /// <summary>Selection and occurrence owner of one node, memoized per glyph so shared nodes are evaluated once.</summary>
        bool Evaluate(TextStudio2DSelectorEvaluator.Workspace workspace, in TextStudio2DCompiledSelector selector, int nodeIndex,
            in TextStudioFrame frame, int glyph, int depth, out int owner)
        {
            owner = DerivedMatch;
            if (depth >= workspace.DepthLimit || nodeIndex < 0 || nodeIndex >= selector.NodeCount)
                return false;
            if (_ownerStamps[nodeIndex] == _ownerGeneration)
            {
                owner = _ownerMatches[nodeIndex];
                return _ownerSelected[nodeIndex];
            }
            TextStudioApplicationSelectorNode node = selector.GetNode(nodeIndex);
            bool selected;
            if (node.Kind == TextStudioSelectorNodeKind.And || node.Kind == TextStudioSelectorNodeKind.Or)
            {
                bool left = Evaluate(workspace, in selector, node.Left, in frame, glyph, depth + 1, out int leftOwner);
                bool right = Evaluate(workspace, in selector, node.Right, in frame, glyph, depth + 1, out int rightOwner);
                selected = node.Kind == TextStudioSelectorNodeKind.And ? left && right : left || right;
                owner = node.Kind == TextStudioSelectorNodeKind.And
                    ? (leftOwner >= 0 ? leftOwner : rightOwner)
                    : Earlier(left ? leftOwner : NoMatch, right ? rightOwner : NoMatch);
            }
            else if (node.Kind == TextStudioSelectorNodeKind.Not)
                selected = !Evaluate(workspace, in selector, node.Left, in frame, glyph, depth + 1, out _);
            else
            {
                // Leaves and Within share the glyph's scoped evaluation; an owner only matters for a selected node.
                selected = workspace.EvaluateNode(nodeIndex, in frame);
                if (node.Invert)
                    selected = !selected;
                else if (selected)
                    owner = EarliestMatch(nodeIndex, glyph);
            }
            if (node.Invert)
            {
                selected = !selected;
                owner = DerivedMatch;
            }
            if (selected && owner == NoMatch)
                owner = DerivedMatch;
            _ownerStamps[nodeIndex] = _ownerGeneration;
            _ownerSelected[nodeIndex] = selected;
            _ownerMatches[nodeIndex] = owner;
            return selected;
        }

        /// <summary>
        /// The node's containing match with the earliest logical start, or the derived owner. Glyphs are queried in
        /// ascending order, so each match enters and leaves the sweep once per resolve.
        /// </summary>
        int EarliestMatch(int node, int glyph)
        {
            int end = _nodeFirst[node] + _nodeCount[node];
            int tail = _sweepTails[node];
            while (tail < end && _matches[_matchOrder[tail]].LogicalStart <= glyph)
                tail++;
            int head = _sweepHeads[node];
            while (head < tail && _matches[_matchOrder[head]].LogicalEnd <= glyph)
                head++;
            _sweepTails[node] = tail;
            _sweepHeads[node] = head;
            return head < tail ? _matchOrder[head] : DerivedMatch;
        }

        int Earlier(int first, int second)
        {
            if (first == NoMatch) return second;
            if (second == NoMatch) return first;
            if (first == DerivedMatch) return second;
            if (second == DerivedMatch) return first;
            TextStudio2DResolvedMatch a = _matches[first];
            TextStudio2DResolvedMatch b = _matches[second];
            if (a.LogicalStart != b.LogicalStart)
                return a.LogicalStart < b.LogicalStart ? first : second;
            return first < second ? first : second;
        }

        static long ClusterKey(TextStudioVisualGlyph glyph)
        {
            return glyph.ClusterStart >= 0 && glyph.ClusterLength > 0
                ? ((long)glyph.ClusterStart << 32) | (uint)glyph.ClusterLength
                : ((long)glyph.TextUnitIndex << 32);
        }

        void Sort(int[] order, in TextStudioVisualSnapshot snapshot, bool visual)
        {
            for (int index = 1; index < _ownerCount; index++)
            {
                int value = order[index];
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(value);
                int key = visual ? glyph.VisualIndex : glyph.LogicalIndex;
                int insertion = index;
                while (insertion > 0)
                {
                    TextStudioVisualGlyph prior = snapshot.GetGlyph(order[insertion - 1]);
                    if ((visual ? prior.VisualIndex : prior.LogicalIndex) <= key) break;
                    order[insertion] = order[insertion - 1];
                    insertion--;
                }
                order[insertion] = value;
            }
        }

        void BuildFragments(in TextStudioFrame frame, bool updateBounds)
        {
            int activeMatch = NoMatch;
            int activeLine = -1;
            int firstMember = 0;
            Vector4 fragmentBounds = default;
            Vector2 direction = Vector2.right;
            for (int index = 0; index <= _ownerCount; index++)
            {
                int glyphIndex = index < _ownerCount ? _visualOrder[index] : -1;
                int owner = GetOwner(glyphIndex);
                TextStudioVisualGlyph glyph = glyphIndex >= 0 ? frame.VisualSnapshot.GetGlyph(glyphIndex) : default;
                bool newFragment = owner != activeMatch || glyph.LineIndex != activeLine || Vector2.Dot(direction, glyph.Orientation) < .5f;
                if (newFragment && activeMatch >= 0)
                    _fragments.Add(new TextStudio2DVisualFragment(activeMatch, activeLine, firstMember, _members.Count - firstMember, direction, fragmentBounds));
                if (owner < 0)
                {
                    activeMatch = NoMatch;
                    continue;
                }
                Vector4 bounds = GlyphBounds(in glyph, frame.GetEffectUnitScale(glyphIndex));
                if (newFragment)
                {
                    firstMember = _members.Count;
                    fragmentBounds = bounds;
                    direction = glyph.Orientation;
                }
                else
                    fragmentBounds = Union(fragmentBounds, bounds);
                if (updateBounds)
                {
                    _bounds[owner] = _hasBounds[owner] ? Union(_bounds[owner], bounds) : bounds;
                    _hasBounds[owner] = true;
                }
                _members.Add(glyphIndex);
                activeMatch = owner;
                activeLine = glyph.LineIndex;
            }
            Ensure(ref _fragmentDistances, _fragments.Count);
            Ensure(ref _matchDistances, _matches.Count);
            Array.Clear(_matchDistances, 0, _matches.Count);
            for (int fragmentIndex = 0; fragmentIndex < _fragments.Count; fragmentIndex++)
            {
                TextStudio2DVisualFragment fragment = _fragments[fragmentIndex];
                _fragmentDistances[fragmentIndex] = _matchDistances[fragment.MatchIndex];
                _matchDistances[fragment.MatchIndex] += Mathf.Max(0f, fragment.Bounds.z - fragment.Bounds.x);
            }
        }

        internal static Vector4 GlyphBounds(in TextStudioVisualGlyph glyph, float em)
        {
            float left = Mathf.Min(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
            float right = Mathf.Max(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
            return glyph.IsVisible
                ? new Vector4(Mathf.Min(left, glyph.BoundsMin.x), glyph.BoundsMin.y, Mathf.Max(right, glyph.BoundsMax.x), glyph.BoundsMax.y)
                : new Vector4(left, glyph.Baseline - Mathf.Max(.2f, em * .2f), right, glyph.Baseline + Mathf.Max(.8f, em * .8f));
        }

        internal static Vector4 Union(Vector4 a, Vector4 b)
        {
            return new Vector4(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.z, b.z), Mathf.Max(a.w, b.w));
        }

        static void Ensure<T>(ref T[] buffer, int count)
        {
            if (buffer.Length < count)
                Array.Resize(ref buffer, TextStudioBufferCapacity.ForCount(count));
        }
    }
}
