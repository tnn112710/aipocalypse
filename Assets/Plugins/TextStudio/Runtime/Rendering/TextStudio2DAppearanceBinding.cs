using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Layout-owned occurrence rows and selector masks. Storage follows covered units, including actual nesting.</summary>
    internal sealed class TextStudio2DAppearanceBinding
    {
        internal readonly struct GlyphBinding
        {
            internal readonly int RenderGlyphIndex, OccurrenceIndex;
            internal GlyphBinding(int glyph, int occurrence) { RenderGlyphIndex = glyph; OccurrenceIndex = occurrence; }
        }

        sealed class Scope
        {
            internal Vector4 Bounds;
            /// <summary>Selected operations per covered unit, <see cref="Words"/> 64-operation words per unit.</summary>
            internal ulong[] Masks = Array.Empty<ulong>();
            internal ulong[] ScratchMasks = Array.Empty<ulong>();
            internal int Words = 1;
            internal ulong Fingerprint, HistoryFingerprint;
            internal int Revision, HistoryRevision;
            internal readonly Dictionary<int, int> MappingIndices = new Dictionary<int, int>();
            internal readonly List<int> RenderGlyphs = new List<int>();
            internal readonly Dictionary<long, int> ClusterRows = new Dictionary<long, int>();
            internal ulong[] ClusterMasks = Array.Empty<ulong>();
        }

        readonly List<Scope> _scopes = new List<Scope>();
        readonly List<List<GlyphBinding>> _programGlyphs = new List<List<GlyphBinding>>();
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        int[] _firstRenderGlyph = Array.Empty<int>(), _nextRenderGlyph = Array.Empty<int>();
        TextStudioDeterministicHash64[] _programRevisions = Array.Empty<TextStudioDeterministicHash64>();
        TextStudioVisualVersion _version;
        int _renderGlyphCount;
        internal CompiledTextStudioAppearancePlan Plan { get; private set; }
        internal int Revision { get; private set; }
        internal int GetOccurrenceRevision(int occurrence) => _scopes[occurrence].Revision;
        internal int GetOccurrenceHistoryRevision(int occurrence) => _scopes[occurrence].HistoryRevision;
        internal ulong GetProgramRevision(int program) => _programRevisions[program].Value;
        internal long ManagedPayloadBytes
        {
            get
            {
                long bytes = TextStudioAppearanceManagedMemory.ArrayBytes(_firstRenderGlyph) + TextStudioAppearanceManagedMemory.ArrayBytes(_nextRenderGlyph) + TextStudioAppearanceManagedMemory.ArrayBytes(_programRevisions);
                foreach (Scope scope in _scopes)
                    bytes += TextStudioAppearanceManagedMemory.ArrayBytes(scope.Masks) + TextStudioAppearanceManagedMemory.ArrayBytes(scope.ScratchMasks) +
                        (long)scope.RenderGlyphs.Capacity * sizeof(int) + (long)scope.MappingIndices.Count * sizeof(int) * 2 + TextStudioAppearanceManagedMemory.ArrayBytes(scope.ClusterMasks) + (long)scope.ClusterRows.Count * (sizeof(long) + sizeof(int));
                foreach (List<GlyphBinding> rows in _programGlyphs) bytes += (long)rows.Capacity * sizeof(int) * 2;
                return bytes;
            }
        }
        internal Vector4 GetDomainBounds(int occurrence) => _scopes[occurrence].Bounds;
        internal IReadOnlyList<GlyphBinding> GetProgramGlyphs(int program) => _programGlyphs[program];
        internal IReadOnlyDictionary<int, int> GetOccurrenceMappings(int occurrence) => _scopes[occurrence].MappingIndices;
        internal IReadOnlyList<int> GetOccurrenceRenderGlyphs(int occurrence) => _scopes[occurrence].RenderGlyphs;
        internal bool Contains(int occurrence, int unit) => Plan.Occurrences[occurrence].ContainsVisibleUnit(unit);
        /// <summary>Selected operations <paramref name="word"/> * 64 through <paramref name="word"/> * 64 + 63 of one unit.</summary>
        internal ulong GetOperationMask(int occurrence, int unit, int word = 0)
        {
            TextStudioAppearanceRange domain = Plan.Occurrences[occurrence].Domain;
            Scope scope = _scopes[occurrence];
            return domain.Contains(unit) && word >= 0 && word < scope.Words ? scope.Masks[(unit - domain.Start) * scope.Words + word] : 0;
        }
        /// <summary>The eight operations of fused span <paramref name="span"/>.</summary>
        internal int GetSpanMask(int occurrence, int unit, int span) =>
            (int)((GetOperationMask(occurrence, unit, span >> 3) >> ((span & 7) << 3)) & 0xffUL);
        internal bool IncludesOperation(int occurrence, int operation, int unit) => operation >= 0 &&
            (GetOperationMask(occurrence, unit, operation >> 6) & (1UL << (operation & 63))) != 0;
        internal int GetMappingGlyphIndex(int occurrence, int renderGlyph) =>
            _scopes[occurrence].MappingIndices.TryGetValue(renderGlyph, out int index) ? index : -1;

        internal void Clear()
        {
            Plan = null; _scopes.Clear(); _programGlyphs.Clear();
            _firstRenderGlyph = Array.Empty<int>(); _nextRenderGlyph = Array.Empty<int>();
            _programRevisions = Array.Empty<TextStudioDeterministicHash64>();
            _version = default; _renderGlyphCount = 0; Revision++;
        }

        internal bool Update(in TextStudioFrame frame)
        {
            CompiledTextStudioAppearancePlan plan = frame.AppearancePlan;
            if (plan == null) return false;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            bool rebuild = !ReferenceEquals(Plan, plan) || !_version.Equals(snapshot.Version) || _renderGlyphCount != snapshot.RenderGlyphCount;
            Plan = plan;
            if (rebuild)
            {
                _version = snapshot.Version; _renderGlyphCount = snapshot.RenderGlyphCount;
                while (_scopes.Count < plan.Occurrences.Count) _scopes.Add(new Scope());
                while (_scopes.Count > plan.Occurrences.Count) _scopes.RemoveAt(_scopes.Count - 1);
                while (_programGlyphs.Count < plan.Programs.Count) _programGlyphs.Add(new List<GlyphBinding>());
                while (_programGlyphs.Count > plan.Programs.Count) _programGlyphs.RemoveAt(_programGlyphs.Count - 1);
                foreach (List<GlyphBinding> glyphs in _programGlyphs) glyphs.Clear();
                Ensure(ref _firstRenderGlyph, plan.UnitCount); Ensure(ref _nextRenderGlyph, snapshot.RenderGlyphCount);
                Array.Fill(_firstRenderGlyph, -1);
                for (int glyph = snapshot.RenderGlyphCount - 1; glyph >= 0; glyph--)
                {
                    int unit = snapshot.RenderGlyphAt(glyph).TextUnitIndex;
                    if ((uint)unit >= (uint)plan.UnitCount) continue;
                    _nextRenderGlyph[glyph] = _firstRenderGlyph[unit]; _firstRenderGlyph[unit] = glyph;
                }
                for (int occurrence = 0; occurrence < plan.Occurrences.Count; occurrence++)
                {
                    TextStudioAppearanceOccurrence invocation = plan.Occurrences[occurrence];
                    Scope scope = _scopes[occurrence];
                    scope.MappingIndices.Clear();
                    scope.RenderGlyphs.Clear();
                    scope.Words = invocation.IsActive ? Math.Max(1, (plan.Programs[invocation.ProgramIndex].OperationCount + 63) >> 6) : 1;
                    Ensure(ref scope.Masks, invocation.Domain.Length * scope.Words);
                    Ensure(ref scope.ScratchMasks, invocation.Domain.Length * scope.Words);
                    Ensure(ref scope.ClusterMasks, invocation.Domain.Length * scope.Words);
                    TextStudioDeterministicHash64 fingerprint = default;
                    fingerprint.Add(invocation.Domain.Start); fingerprint.Add(invocation.Domain.Length);
                    fingerprint.Add((byte)invocation.Resolution);
                    fingerprint.Add(snapshot.Version.Layout); fingerprint.Add(snapshot.Version.Geometry);
                    fingerprint.Add(snapshot.Version.Atlas); fingerprint.Add(snapshot.RenderGlyphCount);
                    for (int range = 0; range < invocation.VisibleRanges.Count; range++)
                    { fingerprint.Add(invocation.VisibleRanges[range].Start); fingerprint.Add(invocation.VisibleRanges[range].Length); }
                    if (invocation.IsActive)
                    {
                        CompiledTextStudio2DStyle program = plan.Programs[invocation.ProgramIndex];
                        for (int selector = 0; selector < program.SelectorCount; selector++) fingerprint.Add(program.GetSelector(selector).Fingerprint);
                    }
                    if (scope.HistoryFingerprint != fingerprint.Value) { scope.HistoryFingerprint = fingerprint.Value; scope.HistoryRevision++; }
                    // Expanded mapping rows change when shared programs split or merge. Geometry
                    // must be rebound, while otherwise identical source history remains valid.
                    fingerprint.Add(invocation.ProgramIndex);
                    if (scope.Fingerprint != fingerprint.Value) { scope.Fingerprint = fingerprint.Value; scope.Revision++; }
                    if (!invocation.IsActive) continue;
                    bool hasBounds = false;
                    Vector4 bounds = default;
                    List<GlyphBinding> rows = _programGlyphs[invocation.ProgramIndex];
                    for (int unit = invocation.Domain.Start; unit < invocation.Domain.End; unit++)
                    {
                        if ((uint)unit < (uint)snapshot.GlyphCount)
                        {
                            TextStudioVisualGlyph glyph = snapshot.GetGlyph(unit);
                            Vector4 glyphBounds = TextStudio2DResolvedSelection.GlyphBounds(in glyph, frame.GetEffectUnitScale(unit));
                            bounds = hasBounds ? TextStudio2DResolvedSelection.Union(bounds, glyphBounds) : glyphBounds;
                            hasBounds = true;
                        }
                        for (int glyph = _firstRenderGlyph[unit]; glyph >= 0; glyph = _nextRenderGlyph[glyph])
                        {
                            ref readonly TextStudioVisualGlyph renderGlyph = ref snapshot.RenderGlyphAt(glyph);
                            Vector4 renderBounds = TextStudio2DResolvedSelection.GlyphBounds(in renderGlyph, frame.GetEffectUnitScale(unit));
                            bounds = hasBounds ? TextStudio2DResolvedSelection.Union(bounds, renderBounds) : renderBounds;
                            hasBounds = true;
                            scope.MappingIndices.Add(glyph, rows.Count);
                            scope.RenderGlyphs.Add(glyph);
                            rows.Add(new GlyphBinding(glyph, occurrence));
                        }
                    }
                    scope.Bounds = hasBounds ? bounds : snapshot.SourceBounds;
                    scope.RenderGlyphs.Sort();
                }
            }
            bool changed = rebuild;
            _selectorPass.Begin();
            for (int occurrence = 0; occurrence < plan.Occurrences.Count; occurrence++)
            {
                TextStudioAppearanceOccurrence invocation = plan.Occurrences[occurrence];
                if (!invocation.IsActive) continue;
                CompiledTextStudio2DStyle style = plan.Programs[invocation.ProgramIndex];
                Scope scope = _scopes[occurrence];
                bool dynamic = false;
                for (int selector = 0; selector < style.SelectorCount; selector++) dynamic |= style.GetSelector(selector).HasDynamicMembership;
                if (!rebuild && !dynamic) continue;
                scope.ClusterRows.Clear();
                int words = scope.Words;
                for (int unit = invocation.Domain.Start; unit < invocation.Domain.End; unit++)
                {
                    int row = (unit - invocation.Domain.Start) * words;
                    Array.Clear(scope.ScratchMasks, row, words);
                    if (style.IsPassThrough && invocation.ApplyMode == TextStudioAppearanceApplyMode.Replace && invocation.ContainsVisibleUnit(unit))
                        scope.ScratchMasks[row] = 1;
                    if (invocation.ContainsVisibleUnit(unit))
                        for (int operation = 0; operation < style.OperationCount; operation++)
                        {
                            ref readonly TextStudio2DCompiledOperation op = ref style.OperationAt(operation);
                            if ((uint)unit < (uint)snapshot.GlyphCount && !TextStudio2DSourceApplicabilityUtility.Includes(in op, snapshot.GetGlyph(unit).SourceKind)) continue;
                            TextStudio2DCompiledSelector selector = style.GetSelector(op.SelectorIndex);
                            if (TextStudio2DSelectorEvaluator.EvaluateInScope(in selector, in frame, unit, invocation.Domain.Start, invocation.Domain.End, _selectorPass))
                                scope.ScratchMasks[row + (operation >> 6)] |= 1UL << (operation & 63);
                        }
                    if ((uint)unit < (uint)snapshot.GlyphCount)
                    {
                        // One cluster shares one selection: every visible member receives the union of all members.
                        TextStudioVisualGlyph glyph = snapshot.GetGlyph(unit);
                        long cluster = ClusterKey(in glyph);
                        if (!scope.ClusterRows.TryGetValue(cluster, out int clusterRow))
                        {
                            clusterRow = scope.ClusterRows.Count * words;
                            scope.ClusterRows.Add(cluster, clusterRow);
                            Array.Clear(scope.ClusterMasks, clusterRow, words);
                        }
                        for (int word = 0; word < words; word++)
                            scope.ClusterMasks[clusterRow + word] |= scope.ScratchMasks[row + word];
                    }
                }
                for (int unit = invocation.Domain.Start; unit < invocation.Domain.End && unit < snapshot.GlyphCount; unit++)
                {
                    if (!invocation.ContainsVisibleUnit(unit)) continue;
                    TextStudioVisualGlyph glyph = snapshot.GetGlyph(unit);
                    Array.Copy(scope.ClusterMasks, scope.ClusterRows[ClusterKey(in glyph)], scope.ScratchMasks, (unit - invocation.Domain.Start) * words, words);
                }
                bool membershipChanged = false;
                for (int local = 0; local < invocation.Domain.Length * words; local++)
                {
                    membershipChanged |= scope.Masks[local] != scope.ScratchMasks[local];
                    scope.Masks[local] = scope.ScratchMasks[local];
                }
                if (membershipChanged) { scope.Revision++; scope.HistoryRevision++; changed = true; }
            }
            if (changed)
            {
                Revision++;
                Ensure(ref _programRevisions, plan.Programs.Count);
                Array.Clear(_programRevisions, 0, _programRevisions.Length);
                for (int occurrence = 0; occurrence < plan.Occurrences.Count; occurrence++)
                {
                    TextStudioAppearanceOccurrence invocation = plan.Occurrences[occurrence];
                    if (!invocation.IsActive) continue;
                    _programRevisions[invocation.ProgramIndex].Add(occurrence);
                    _programRevisions[invocation.ProgramIndex].Add(_scopes[occurrence].Revision);
                }
            }
            return changed;
        }

        static void Ensure<T>(ref T[] data, int count)
        { if (data.Length < count) Array.Resize(ref data, TextStudioBufferCapacity.ForCount(count)); }

        static long ClusterKey(in TextStudioVisualGlyph glyph) => glyph.ClusterStart >= 0 && glyph.ClusterLength > 0
            ? ((long)glyph.ClusterStart << 32) | (uint)glyph.ClusterLength : ((long)glyph.TextUnitIndex << 32);
    }
}
