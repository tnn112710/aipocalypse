using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Consumer-owned selector evaluation pass. Call <see cref="Begin"/> whenever the evaluated frame data may differ from
    /// the previous evaluation. Composite Within runs, cluster decisions and cluster members are reused only within the
    /// pass that produced them.
    /// </summary>
    internal sealed class TextStudio2DSelectorPass
    {
#if UNITY_6000_7_OR_NEWER
        // Pass identities must stay unique for the code lifetime because selector workspaces outlive play sessions.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static long s_lastId;

        readonly Dictionary<long, int> _clusterTails = new Dictionary<long, int>();
        int[] _clusterNext = Array.Empty<int>();
        long _clusterPass;

        /// <summary>Identity of the current pass; zero before the first <see cref="Begin"/>.</summary>
        internal long Id { get; private set; }

        internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ArrayBytes(_clusterNext) +
            (long)_clusterTails.EnsureCapacity(0) * (sizeof(long) + sizeof(int));

        internal void Begin() => Id = Interlocked.Increment(ref s_lastId);

        internal static long ClusterKey(in TextStudioVisualGlyph glyph) =>
            ((long)glyph.ClusterStart << 32) | (uint)glyph.ClusterLength;

        /// <summary>
        /// Next glyph of <paramref name="glyph"/>'s multi-glyph cluster in a circular member list, or the glyph itself
        /// when it has no cluster. Members are collected once per pass; the snapshot must not change within the pass.
        /// </summary>
        internal int NextClusterMember(in TextStudioVisualSnapshot snapshot, int glyph)
        {
            if (_clusterPass != Id)
                CollectClusters(in snapshot);
            return _clusterNext[glyph];
        }

        void CollectClusters(in TextStudioVisualSnapshot snapshot)
        {
            _clusterPass = Id;
            _clusterTails.Clear();
            int count = snapshot.GlyphCount;
            if (_clusterNext.Length < count)
                _clusterNext = new int[TextStudioBufferCapacity.ForCount(count)];
            for (int glyph = 0; glyph < count; glyph++)
            {
                TextStudioVisualGlyph visual = snapshot.GetGlyph(glyph);
                _clusterNext[glyph] = glyph;
                if (visual.ClusterLength <= 1)
                    continue;
                long key = ClusterKey(in visual);
                if (_clusterTails.TryGetValue(key, out int tail))
                {
                    _clusterNext[glyph] = _clusterNext[tail];
                    _clusterNext[tail] = glyph;
                    _clusterTails[key] = glyph;
                }
                else
                    _clusterTails.Add(key, glyph);
            }
        }
    }

    /// <summary>Allocation-free CPU evaluator used to encode one fused-span operation mask per glyph.</summary>
    internal static class TextStudio2DSelectorEvaluator
    {
        /// <summary>Selected operations of fused span <paramref name="span"/>: bit n selects operation span * 8 + n.</summary>
        internal static byte EvaluateOperationMask(
            CompiledTextStudio2DStyle style,
            in TextStudioFrame frame,
            int glyphIndex,
            int span = 0,
            TextStudio2DSelectorPass pass = null)
        {
            if (style == null || glyphIndex < 0 || span < 0)
                return 0;

            int first = span * TextStudio2DStyleTable.MaximumFusedOperations;
            int count = Mathf.Min(
                style.OperationCount - first,
                TextStudio2DStyleTable.MaximumFusedOperations);
            byte mask = 0;
            for (int bit = 0; bit < count; bit++)
            {
                int operationIndex = first + bit;
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
                if (frame.VisualSnapshot.IsCreated && glyphIndex < frame.VisualSnapshot.GlyphCount &&
                    !TextStudio2DSourceApplicabilityUtility.Includes(in operation, frame.VisualSnapshot.GetGlyph(glyphIndex).SourceKind)) continue;
                if (operation.SelectorIndex < 0 ||
                    operation.SelectorIndex >= style.SelectorCount)
                {
                    continue;
                }

                TextStudio2DCompiledSelector selector =
                    style.GetSelector(operation.SelectorIndex);
                if (Evaluate(in selector, in frame, glyphIndex, pass))
                    mask |= (byte)(1 << bit);
            }
            return mask;
        }

        /// <summary>
        /// Whether the selector includes <paramref name="glyphIndex"/> in the whole text. A multi-glyph cluster is
        /// selected when any member is. Pass a begun <paramref name="pass"/> to reuse runs across the glyphs of one frame.
        /// </summary>
        internal static bool Evaluate(
            in TextStudio2DCompiledSelector selector,
            in TextStudioFrame frame,
            int glyphIndex,
            TextStudio2DSelectorPass pass = null)
        {
            if (selector.IsTrivialAll)
                return true;
            if (selector.NodeCount <= 0 ||
                selector.Root < 0 ||
                selector.Root >= selector.NodeCount)
            {
                return false;
            }
            Workspace workspace = selector.EvaluationWorkspace;
            long passId = workspace.ResolvePass(pass);
            if (workspace.EvaluateUnit(selector.Root, in frame, glyphIndex, 0, frame.Plan.VisibleGlyphCount, passId))
                return true;
            if (!frame.VisualSnapshot.IsCreated || glyphIndex < 0 || glyphIndex >= frame.VisualSnapshot.GlyphCount)
                return false;
            TextStudioVisualGlyph glyph = frame.VisualSnapshot.GetGlyph(glyphIndex);
            return glyph.ClusterLength > 1 && EvaluateCluster(in selector, in frame, glyphIndex, in glyph, pass, passId);
        }

        /// <summary>A multi-glyph cluster is selected when any member is, so one decision serves every member within a pass.</summary>
        static bool EvaluateCluster(in TextStudio2DCompiledSelector selector, in TextStudioFrame frame, int glyphIndex,
            in TextStudioVisualGlyph glyph, TextStudio2DSelectorPass pass, long passId)
        {
            Workspace workspace = selector.EvaluationWorkspace;
            long cluster = TextStudio2DSelectorPass.ClusterKey(in glyph);
            if (workspace.TryGetClusterDecision(passId, cluster, out bool selected))
                return selected;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            int scopeEnd = frame.Plan.VisibleGlyphCount;
            if (pass != null && pass.Id == passId)
            {
                for (int member = pass.NextClusterMember(in snapshot, glyphIndex);
                     !selected && member != glyphIndex;
                     member = pass.NextClusterMember(in snapshot, member))
                    selected = workspace.EvaluateUnit(selector.Root, in frame, member, 0, scopeEnd, passId);
            }
            else
            {
                for (int member = 0; !selected && member < snapshot.GlyphCount; member++)
                {
                    if (member == glyphIndex)
                        continue;
                    TextStudioVisualGlyph candidate = snapshot.GetGlyph(member);
                    selected = candidate.ClusterStart == glyph.ClusterStart && candidate.ClusterLength == glyph.ClusterLength &&
                        workspace.EvaluateUnit(selector.Root, in frame, member, 0, scopeEnd, passId);
                }
            }
            workspace.StoreClusterDecision(passId, cluster, selected);
            return selected;
        }

        /// <summary>Whether the selector includes <paramref name="unit"/> when evaluated inside [start, end), without cluster closure.</summary>
        internal static bool EvaluateInScope(in TextStudio2DCompiledSelector selector, in TextStudioFrame frame,
            int unit, int start, int end, TextStudio2DSelectorPass pass = null) => unit >= start && unit < end &&
            (selector.IsTrivialAll || selector.Root >= 0 && selector.Root < selector.NodeCount &&
                selector.EvaluationWorkspace.EvaluateUnit(selector.Root, in frame, unit, start, end, selector.EvaluationWorkspace.ResolvePass(pass)));

        /// <summary>
        /// Selector-owned evaluation scratch. A program that reaches a node through several paths memoizes each node once
        /// per distinct scope of one unit, so its work is bounded by the program instead of its expanded tree; a tree needs
        /// no memo. Main-thread only; buffers grow during warm-up and are reused by every copy of the selector.
        /// </summary>
        internal sealed class Workspace
        {
            // The selector compiler admits at most 256 nodes, so no acyclic path of a validated program reaches this depth.
            const int MaximumDepth = 256;
            const int RunWays = 4;

            readonly TextStudioApplicationSelectorNode[] _nodes;
            readonly int _nodeCount;
            readonly bool _memoize;
            int[] _contextUnits = Array.Empty<int>();
            int[] _contextStarts = Array.Empty<int>();
            int[] _contextEnds = Array.Empty<int>();
            long[] _contextStamps = Array.Empty<long>();
            long[] _memoStamps = Array.Empty<long>();
            bool[] _memoValues = Array.Empty<bool>();
            long[] _runPasses = Array.Empty<long>();
            int[] _runScopeStarts = Array.Empty<int>();
            int[] _runScopeEnds = Array.Empty<int>();
            int[] _runStarts = Array.Empty<int>();
            int[] _runEnds = Array.Empty<int>();
            int[] _runVictims = Array.Empty<int>();
            int _contextCount;
            int _sessionStart;
            int _unit;
            int _unitScopeStart;
            int _unitScopeEnd;
            long _generation;
            long _passId;
            long _localPass;
            long _clusterPass;
            long _clusterKey;
            bool _clusterSelected;

            internal Workspace(TextStudioApplicationSelectorNode[] nodes, int root)
            {
                _nodes = nodes;
                _nodeCount = nodes.Length;
                DepthLimit = Math.Min(_nodeCount, MaximumDepth);
                _memoize = SharesNodes(nodes, root);
            }

            /// <summary>Recursion bound: deeper evaluation only occurs in cyclic programs, which the compiler rejects.</summary>
            internal int DepthLimit { get; }

            /// <summary>Starts evaluating <paramref name="unit"/> inside [<paramref name="scopeStart"/>, <paramref name="scopeEnd"/>).</summary>
            internal void BeginUnit(int unit, int scopeStart, int scopeEnd, long passId)
            {
                _passId = passId;
                _unit = unit;
                _unitScopeStart = scopeStart;
                _unitScopeEnd = scopeEnd;
                _contextCount = 0;
                _sessionStart = 0;
                if (_memoize)
                    PushContext(unit, scopeStart, scopeEnd);
            }

            /// <summary>Evaluates one node for the unit and scope of the latest <see cref="BeginUnit"/>, sharing its memo.</summary>
            internal bool EvaluateNode(int node, in TextStudioFrame frame) =>
                Evaluate(node, in frame, _unit, _unitScopeStart, _unitScopeEnd, _memoize ? 0 : -1, 0);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool EvaluateUnit(int node, in TextStudioFrame frame, int unit, int scopeStart, int scopeEnd, long passId)
            {
                if (!_memoize)
                {
                    _passId = passId;
                    return Evaluate(node, in frame, unit, scopeStart, scopeEnd, -1, 0);
                }
                BeginUnit(unit, scopeStart, scopeEnd, passId);
                return Evaluate(node, in frame, unit, scopeStart, scopeEnd, 0, 0);
            }

            /// <summary>
            /// The identity of a begun pass, or a fresh one for a call without a pass. Fresh identities are negative and local
            /// to this workspace, which alone holds the caches keyed by them, so they never match a begun pass.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal long ResolvePass(TextStudio2DSelectorPass pass) => pass != null && pass.Id != 0 ? pass.Id : --_localPass;

            internal bool TryGetClusterDecision(long passId, long cluster, out bool selected)
            {
                bool cached = _clusterPass == passId && _clusterKey == cluster;
                selected = cached && _clusterSelected;
                return cached;
            }

            internal void StoreClusterDecision(long passId, long cluster, bool selected)
            {
                _clusterPass = passId;
                _clusterKey = cluster;
                _clusterSelected = selected;
            }

            /// <summary>Whether a node is reachable from the root through more than one path, including cycles.</summary>
            static bool SharesNodes(TextStudioApplicationSelectorNode[] nodes, int root)
            {
                if (root < 0 || root >= nodes.Length)
                    return false;
                int[] parents = new int[nodes.Length];
                int[] pending = new int[nodes.Length];
                int count = 0;
                parents[root] = 1;
                pending[count++] = root;
                while (count > 0)
                {
                    TextStudioApplicationSelectorNode node = nodes[pending[--count]];
                    bool binary = node.Kind == TextStudioSelectorNodeKind.And || node.Kind == TextStudioSelectorNodeKind.Or ||
                                  node.Kind == TextStudioSelectorNodeKind.Within;
                    if (!binary && node.Kind != TextStudioSelectorNodeKind.Not)
                        continue;
                    for (int edge = binary ? 2 : 1; edge > 0; edge--)
                    {
                        int child = edge == 1 ? node.Left : node.Right;
                        if (child < 0 || child >= nodes.Length)
                            continue;
                        if (++parents[child] > 1)
                            return true;
                        pending[count++] = child;
                    }
                }
                return false;
            }

            /// <summary><paramref name="context"/> is the memo context of (unit, scope), or -1 for a program without shared nodes.</summary>
            bool Evaluate(int nodeIndex, in TextStudioFrame frame, int unit, int scopeStart, int scopeEnd, int context, int depth)
            {
                if (depth >= DepthLimit ||
                    nodeIndex < 0 ||
                    nodeIndex >= _nodeCount ||
                    unit < scopeStart || unit >= scopeEnd)
                {
                    return false;
                }
                int slot = 0;
                long stamp = 0;
                if (context >= 0)
                {
                    slot = context * _nodeCount + nodeIndex;
                    stamp = _contextStamps[context];
                    if (_memoStamps[slot] == stamp)
                        return _memoValues[slot];
                }

                TextStudioApplicationSelectorNode node = _nodes[nodeIndex];
                bool result;
                switch (node.Kind)
                {
                    case TextStudioSelectorNodeKind.All:
                        result = true;
                        break;
                    case TextStudioSelectorNodeKind.None:
                        result = false;
                        break;
                    case TextStudioSelectorNodeKind.Range:
                        if (node.UsesTextRange)
                        {
                            node.TextRange.Resolve(scopeEnd - scopeStart, out int rangeStart, out int rangeEnd);
                            result = unit >= scopeStart + rangeStart && unit < scopeStart + rangeEnd;
                        }
                        else
                            result = unit >= scopeStart + (long)node.Start &&
                                     unit < scopeStart + (long)node.Start + node.Count;
                        break;
                    case TextStudioSelectorNodeKind.Tag:
                        result = MatchesRegion(in frame, unit, node.Key);
                        break;
                    case TextStudioSelectorNodeKind.AppearanceTag:
                        result = MatchesAppearance(in frame, unit, node.Key);
                        break;
                    case TextStudioSelectorNodeKind.SourceColor:
                        result = MatchesSourceColor(in frame, unit, node.Color, node.Tolerance);
                        break;
                    case TextStudioSelectorNodeKind.SemanticState:
                        result = MatchesSemanticState(
                            in frame,
                            unit,
                            node.SemanticState);
                        break;
                    case TextStudioSelectorNodeKind.Coverage:
                        result = Compare(
                            ResolveCoverage(in frame, unit),
                            node.Value,
                            node.Comparison);
                        break;
                    case TextStudioSelectorNodeKind.RenderProperty:
                        result = Compare(
                            ResolveRenderProperty(in frame, unit, node.Key),
                            node.Value,
                            node.Comparison);
                        break;
                    case TextStudioSelectorNodeKind.And:
                        result = Evaluate(node.Left, in frame, unit, scopeStart, scopeEnd, context, depth + 1) &&
                                 Evaluate(node.Right, in frame, unit, scopeStart, scopeEnd, context, depth + 1);
                        break;
                    case TextStudioSelectorNodeKind.Or:
                        result = Evaluate(node.Left, in frame, unit, scopeStart, scopeEnd, context, depth + 1) ||
                                 Evaluate(node.Right, in frame, unit, scopeStart, scopeEnd, context, depth + 1);
                        break;
                    case TextStudioSelectorNodeKind.Not:
                        result = !Evaluate(node.Left, in frame, unit, scopeStart, scopeEnd, context, depth + 1);
                        break;
                    case TextStudioSelectorNodeKind.Within:
                        result = TryResolveParentScope(node.Left, in frame, unit, scopeStart, scopeEnd, context, depth + 1,
                                     out int childStart, out int childEnd) &&
                                 Evaluate(node.Right, in frame, unit, childStart, childEnd,
                                     context >= 0 ? ChildContext(unit, childStart, childEnd) : -1, depth + 1);
                        break;
                    default:
                        result = false;
                        break;
                }

                if (node.Invert)
                    result = !result;
                if (context >= 0)
                {
                    // Nested scopes may have grown the memo; its context-major layout keeps this slot in place.
                    _memoStamps[slot] = stamp;
                    _memoValues[slot] = result;
                }
                return result;
            }

            bool TryResolveParentScope(int parentIndex, in TextStudioFrame frame, int unit, int scopeStart, int scopeEnd,
                int context, int depth, out int start, out int end)
            {
                start = end = unit;
                if (parentIndex < 0 || parentIndex >= _nodeCount || depth >= DepthLimit)
                    return false;
                TextStudioApplicationSelectorNode parent = _nodes[parentIndex];
                if (parent.Kind == TextStudioSelectorNodeKind.AppearanceTag && !parent.Invert)
                {
                    if (frame.AppearancePlan != null)
                    {
                        if (!frame.AppearancePlan.TagIndex.TryGetRange(parent.Key, unit, out TextStudioAppearanceRange range)) return false;
                        start = Math.Max(scopeStart, range.Start); end = Math.Min(scopeEnd, range.End);
                        return start < end;
                    }
                    TextStudioReadOnlyBuffer<TextStudioAppearanceSpan> spans = frame.Plan.AppearanceSpans;
                    for (int index = 0; index < spans.Length; index++)
                    {
                        TextStudioAppearanceSpan span = spans[index];
                        if (!string.Equals(span.Id, parent.Key, StringComparison.OrdinalIgnoreCase) ||
                            unit < span.Start || unit >= (long)span.Start + span.Length) continue;
                        start = Math.Max(scopeStart, span.Start);
                        end = Math.Min(scopeEnd, span.Start + span.Length);
                        return start < end;
                    }
                    return false;
                }
                if (parent.Kind == TextStudioSelectorNodeKind.Tag && !parent.Invert)
                {
                    TextStudioReadOnlyBuffer<RegionToGlyphMap> regions = frame.Plan.RegionMaps;
                    for (int index = 0; index < regions.Length; index++)
                    {
                        RegionToGlyphMap region = regions[index];
                        if (!string.Equals(region.regionName, parent.Key, StringComparison.Ordinal) ||
                            unit < region.glyphStart || unit >= (long)region.glyphStart + region.glyphCount)
                            continue;
                        start = Math.Max(scopeStart, region.glyphStart);
                        end = Math.Min(scopeEnd, region.glyphStart + region.glyphCount);
                        return start < end;
                    }
                    return false;
                }
                if (parent.Kind == TextStudioSelectorNodeKind.Range && !parent.Invert)
                {
                    int localStart;
                    int localEnd;
                    if (parent.UsesTextRange)
                        parent.TextRange.Resolve(scopeEnd - scopeStart, out localStart, out localEnd);
                    else
                    {
                        localStart = Math.Max(0, parent.Start);
                        localEnd = (int)Math.Min((long)scopeEnd - scopeStart,
                            (long)parent.Start + parent.Count);
                    }
                    start = scopeStart + localStart;
                    end = scopeStart + localEnd;
                    return unit >= start && unit < end;
                }
                // A composite parent selects each maximal contiguous run of units; runs are reused within one pass.
                if (TryGetRun(parentIndex, unit, scopeStart, scopeEnd, out start, out end))
                    return true;
                if (!Evaluate(parentIndex, in frame, unit, scopeStart, scopeEnd, context, depth))
                    return false;
                start = unit;
                end = unit + 1;
                while (start > scopeStart && EvaluateNeighbor(parentIndex, in frame, start - 1, scopeStart, scopeEnd, depth)) start--;
                while (end < scopeEnd && EvaluateNeighbor(parentIndex, in frame, end, scopeStart, scopeEnd, depth)) end++;
                StoreRun(parentIndex, scopeStart, scopeEnd, start, end);
                return true;
            }

            /// <summary>Evaluates another unit of the same scope, in a memo session nested above the current one.</summary>
            bool EvaluateNeighbor(int nodeIndex, in TextStudioFrame frame, int unit, int scopeStart, int scopeEnd, int depth)
            {
                if (!_memoize)
                    return Evaluate(nodeIndex, in frame, unit, scopeStart, scopeEnd, -1, depth);
                int sessionStart = _sessionStart;
                int contextCount = _contextCount;
                _sessionStart = contextCount;
                int context = PushContext(unit, scopeStart, scopeEnd);
                bool result = Evaluate(nodeIndex, in frame, unit, scopeStart, scopeEnd, context, depth);
                _contextCount = contextCount;
                _sessionStart = sessionStart;
                return result;
            }

            /// <summary>The memo context of this unit in a child scope; equal scopes share results.</summary>
            int ChildContext(int unit, int start, int end)
            {
                for (int context = _sessionStart; context < _contextCount; context++)
                    if (_contextUnits[context] == unit && _contextStarts[context] == start && _contextEnds[context] == end)
                        return context;
                return PushContext(unit, start, end);
            }

            int PushContext(int unit, int start, int end)
            {
                int context = _contextCount++;
                if (context >= _contextUnits.Length)
                {
                    int capacity = Math.Max(4, _contextUnits.Length * 2);
                    Array.Resize(ref _contextUnits, capacity);
                    Array.Resize(ref _contextStarts, capacity);
                    Array.Resize(ref _contextEnds, capacity);
                    Array.Resize(ref _contextStamps, capacity);
                    Array.Resize(ref _memoStamps, capacity * _nodeCount);
                    Array.Resize(ref _memoValues, capacity * _nodeCount);
                }
                _contextUnits[context] = unit;
                _contextStarts[context] = start;
                _contextEnds[context] = end;
                _contextStamps[context] = ++_generation;
                return context;
            }

            bool TryGetRun(int parentIndex, int unit, int scopeStart, int scopeEnd, out int start, out int end)
            {
                start = end = unit;
                if (_runPasses.Length == 0)
                    return false;
                int first = parentIndex * RunWays;
                for (int way = first; way < first + RunWays; way++)
                {
                    if (_runPasses[way] != _passId || _runScopeStarts[way] != scopeStart || _runScopeEnds[way] != scopeEnd ||
                        unit < _runStarts[way] || unit >= _runEnds[way])
                        continue;
                    start = _runStarts[way];
                    end = _runEnds[way];
                    return true;
                }
                return false;
            }

            void StoreRun(int parentIndex, int scopeStart, int scopeEnd, int start, int end)
            {
                if (_runPasses.Length == 0)
                {
                    _runPasses = new long[_nodeCount * RunWays];
                    _runScopeStarts = new int[_nodeCount * RunWays];
                    _runScopeEnds = new int[_nodeCount * RunWays];
                    _runStarts = new int[_nodeCount * RunWays];
                    _runEnds = new int[_nodeCount * RunWays];
                    _runVictims = new int[_nodeCount];
                }
                // One way per scope keeps its latest run; stale passes are reused before live scopes are evicted.
                int first = parentIndex * RunWays;
                int slot = -1;
                for (int way = first; way < first + RunWays && slot < 0; way++)
                    if (_runPasses[way] == _passId && _runScopeStarts[way] == scopeStart && _runScopeEnds[way] == scopeEnd)
                        slot = way;
                for (int way = first; way < first + RunWays && slot < 0; way++)
                    if (_runPasses[way] != _passId)
                        slot = way;
                if (slot < 0)
                {
                    slot = first + _runVictims[parentIndex];
                    _runVictims[parentIndex] = (_runVictims[parentIndex] + 1) % RunWays;
                }
                _runPasses[slot] = _passId;
                _runScopeStarts[slot] = scopeStart;
                _runScopeEnds[slot] = scopeEnd;
                _runStarts[slot] = start;
                _runEnds[slot] = end;
            }
        }

        static bool MatchesAppearance(in TextStudioFrame frame, int unit, string id)
        {
            if (frame.AppearancePlan != null) return frame.AppearancePlan.TagIndex.TryGetRange(id, unit, out _);
            TextStudioReadOnlyBuffer<TextStudioAppearanceSpan> spans = frame.Plan.AppearanceSpans;
            for (int index = 0; index < spans.Length; index++)
            {
                TextStudioAppearanceSpan span = spans[index];
                if (string.Equals(span.Id, id, StringComparison.OrdinalIgnoreCase) && unit >= span.Start && unit < (long)span.Start + span.Length)
                    return true;
            }
            return false;
        }

        static bool MatchesRegion(
            in TextStudioFrame frame,
            int glyphIndex,
            string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
                return false;

            TextStudioReadOnlyBuffer<RegionToGlyphMap> regions = frame.Plan.RegionMaps;
            for (int index = 0; index < regions.Length; index++)
            {
                RegionToGlyphMap region = regions[index];
                if (!string.Equals(region.regionName, regionId, StringComparison.Ordinal) ||
                    glyphIndex < region.glyphStart ||
                    glyphIndex >= (long)region.glyphStart + region.glyphCount)
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        static bool MatchesSourceColor(
            in TextStudioFrame frame,
            int glyphIndex,
            Color target,
            float tolerance)
        {
            Color source = frame.GetSourceColors(glyphIndex).Sample(0.5f, 0.5f);
            Color sourceLinear = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? source.linear
                : source;
            Color targetLinear = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? target.linear
                : target;
            float red = sourceLinear.r - targetLinear.r;
            float green = sourceLinear.g - targetLinear.g;
            float blue = sourceLinear.b - targetLinear.b;
            float alpha = sourceLinear.a - targetLinear.a;
            float distance = Mathf.Sqrt(
                red * red +
                green * green +
                blue * blue +
                alpha * alpha);
            return distance <= Mathf.Max(0f, tolerance);
        }

        static bool MatchesSemanticState(
            in TextStudioFrame frame,
            int glyphIndex,
            TextStudioSelectorSemanticState requested)
        {
            if (requested == TextStudioSelectorSemanticState.None)
                return false;

            ResolveAnimatedState(
                in frame,
                glyphIndex,
                out AnimationPhase phase,
                out float phaseProgress,
                out float alpha,
                out bool sourceVisible);
            bool visible = sourceVisible && alpha > 0.0001f;
            TextStudioSelectorSemanticState state = visible
                ? TextStudioSelectorSemanticState.Visible
                : TextStudioSelectorSemanticState.Hidden;
            state |= phase switch
            {
                AnimationPhase.Appearance => TextStudioSelectorSemanticState.Revealing,
                AnimationPhase.Disappearance => TextStudioSelectorSemanticState.Hiding,
                _ => TextStudioSelectorSemanticState.Active
            };

            TextStudioReadOnlyBuffer<int> wordIndices = frame.Plan.GlyphToWordIndex;
            TextStudioReadOnlyBuffer<WordRange> words = frame.Plan.WordRanges;
            if (glyphIndex < wordIndices.Length)
            {
                int wordIndex = wordIndices[glyphIndex];
                if (wordIndex >= 0 && wordIndex < words.Length)
                {
                    WordRange word = words[wordIndex];
                    if (glyphIndex == word.glyphStart)
                        state |= TextStudioSelectorSemanticState.FirstInWord;
                    if (glyphIndex == word.GlyphEnd - 1)
                        state |= TextStudioSelectorSemanticState.LastInWord;
                }
            }

            TextStudioReadOnlyBuffer<int> lineIndices = frame.Plan.GlyphToLineIndex;
            TextStudioReadOnlyBuffer<LineRange> lines = frame.Plan.LineRanges;
            if (glyphIndex < lineIndices.Length)
            {
                int lineIndex = lineIndices[glyphIndex];
                if (lineIndex >= 0 && lineIndex < lines.Length)
                {
                    LineRange line = lines[lineIndex];
                    if (glyphIndex == line.glyphStart)
                        state |= TextStudioSelectorSemanticState.FirstInLine;
                    if (glyphIndex == line.GlyphEnd - 1)
                        state |= TextStudioSelectorSemanticState.LastInLine;
                }
            }

            _ = phaseProgress;
            return (state & requested) != 0;
        }

        static float ResolveCoverage(in TextStudioFrame frame, int glyphIndex)
        {
            ResolveAnimatedState(
                in frame,
                glyphIndex,
                out _,
                out float phaseProgress,
                out float alpha,
                out bool sourceVisible);
            if (!sourceVisible)
                return 0f;
            return Mathf.Clamp01(Mathf.Min(alpha, phaseProgress));
        }

        static void ResolveAnimatedState(
            in TextStudioFrame frame,
            int glyphIndex,
            out AnimationPhase phase,
            out float phaseProgress,
            out float alpha,
            out bool sourceVisible)
        {
            sourceVisible = !frame.VisualSnapshot.IsCreated ||
                            glyphIndex >= frame.VisualSnapshot.GlyphCount ||
                            frame.VisualSnapshot.GetGlyph(glyphIndex).IsVisible;
            if (frame.GlyphStates.IsCreated && glyphIndex < frame.GlyphStateCount)
            {
                TextStudioGlyphInstanceState state = frame.GlyphStates[glyphIndex];
                phase = state.phase;
                phaseProgress = Mathf.Clamp01(state.phaseProgress);
                alpha = Mathf.Clamp01(state.alpha);
                sourceVisible &= state.IsVisible;
                return;
            }
            if (frame.Animated.IsCreated && glyphIndex < frame.AnimatedCount)
            {
                EvaluatedGlyphState state = frame.Animated[glyphIndex];
                phase = state.currentPhase;
                phaseProgress = Mathf.Clamp01(state.phaseProgress);
                alpha = Mathf.Clamp01(state.alpha);
                return;
            }

            phase = AnimationPhase.Persistent;
            phaseProgress = 1f;
            alpha = 1f;
        }

        static float ResolveRenderProperty(
            in TextStudioFrame frame,
            int glyphIndex,
            string propertyId)
        {
            if (frame.RenderProperties.TryGet(
                    propertyId,
                    glyphIndex,
                    out TextStudioRenderPropertyValue value,
                    out _))
            {
                return value.ValueType == TextStudioRenderPropertyValueType.UInt
                    ? value.Unsigned
                    : value.Vector.x;
            }
            if (TextStudioRenderPropertyRegistry.TryGet(propertyId, out TextStudioRenderPropertyDescriptor descriptor))
            {
                return descriptor.ValueType == TextStudioRenderPropertyValueType.UInt
                    ? descriptor.DefaultUnsigned
                    : descriptor.DefaultValue.x;
            }
            return 0f;
        }

        static bool Compare(
            float value,
            float target,
            TextStudioSelectorComparison comparison)
        {
            return comparison switch
            {
                TextStudioSelectorComparison.Equal => Mathf.Approximately(value, target),
                TextStudioSelectorComparison.NotEqual => !Mathf.Approximately(value, target),
                TextStudioSelectorComparison.Less => value < target,
                TextStudioSelectorComparison.LessOrEqual => value <= target,
                TextStudioSelectorComparison.Greater => value > target,
                TextStudioSelectorComparison.GreaterOrEqual => value >= target,
                _ => false
            };
        }
    }
}
