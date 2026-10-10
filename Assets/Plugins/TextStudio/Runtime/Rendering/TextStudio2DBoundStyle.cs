using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Owned selector bindings shared by paint mapping, retained surfaces, and composition. Never retains a borrowed frame.</summary>
    internal sealed class TextStudio2DBoundStyle
    {
        readonly List<SelectionEntry> _selections = new List<SelectionEntry>();
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        CompiledTextStudio2DStyle _style;
        internal int ResolutionCount { get; private set; }
        internal CompiledTextStudio2DStyle Style => _style;
        internal bool HasColorAdjustments { get; private set; }
        internal int ResolvedMatchCount
        {
            get
            {
                int count = 0;
                foreach (SelectionEntry selection in _selections)
                    if (selection.Ready) count += selection.Selection.MatchCount;
                return count;
            }
        }

        internal long ManagedPayloadBytes
        {
            get
            {
                long bytes = (long)_selections.Capacity * IntPtr.Size + _selectorPass.ManagedPayloadBytes;
                foreach (SelectionEntry entry in _selections) bytes += entry.Selection.ManagedPayloadBytes;
                return bytes;
            }
        }

        internal void Bind(CompiledTextStudio2DStyle style)
        {
            if (ReferenceEquals(_style, style)) return;
            _style = style;
            HasColorAdjustments = false;
            if (style == null)
            {
                _selections.Clear();
                return;
            }
            while (_selections.Count > style.SelectorCount) _selections.RemoveAt(_selections.Count - 1);
            for (int index = 0; index < style.OperationCount; index++)
                HasColorAdjustments |= style.OperationAt(index).Kind == TextStudio2DLayerKind.ColorAdjustment;
        }

        internal TextStudio2DResolvedSelection GetSelection(in TextStudioFrame frame, int selectorIndex)
        {
            if (_style == null || selectorIndex < 0 || selectorIndex >= _style.SelectorCount)
                throw new ArgumentOutOfRangeException(nameof(selectorIndex));
            while (_selections.Count <= selectorIndex) _selections.Add(new SelectionEntry());
            SelectionEntry entry = _selections[selectorIndex];
            TextStudio2DCompiledSelector selector = _style.GetSelector(selectorIndex);
            TextStudioDeterministicHash64 hash = default;
            hash.Add(selector.Fingerprint);
            hash.Add(frame.Plan.SourceVersion);
            hash.Add(frame.VisualSnapshot.Version.Layout);
            hash.Add(frame.VisualSnapshot.Version.Geometry);
            hash.Add(frame.VisualSnapshot.GlyphCount);
            hash.Add(frame.AppearanceOccurrenceIndex);
            hash.Add(frame.AppearancePlan?.Fingerprint.Topology ?? 0);
            _selectorPass.Begin();
            if (selector.HasDynamicMembership)
                for (int glyph = frame.AppearanceOccurrenceIndex >= 0 ? frame.AppearancePlan.Occurrences[frame.AppearanceOccurrenceIndex].Domain.Start : 0;
                    glyph < (frame.AppearanceOccurrenceIndex >= 0 ? frame.AppearancePlan.Occurrences[frame.AppearanceOccurrenceIndex].Domain.End : frame.VisualSnapshot.GlyphCount); glyph++)
                    hash.Add(TextStudio2DSelectorEvaluator.Evaluate(in selector, in frame, glyph, _selectorPass));
            if (!entry.Ready || entry.Fingerprint != hash.Value || !ReferenceEquals(entry.PlanIdentity, frame.Plan.CompiledPlan))
            {
                entry.Selection.Resolve(in selector, in frame);
                entry.Fingerprint = hash.Value;
                entry.PlanIdentity = frame.Plan.CompiledPlan;
                entry.Ready = true;
                ResolutionCount++;
            }
            return entry.Selection;
        }

        internal Vector4 SurfaceAdjustmentMask(in TextStudioFrame frame, int operationIndex, IReadOnlyList<int> members)
        {
            Vector4 mask = Vector4.zero;
            if (!HasColorAdjustments || members.Count == 0) return mask;
            for (int index = 0; index < Mathf.Min(operationIndex, 64); index++)
            {
                ref readonly TextStudio2DCompiledOperation adjustment = ref _style.OperationAt(index);
                if (adjustment.Kind != TextStudio2DLayerKind.ColorAdjustment ||
                    !TextStudio2DColorAdjustmentUtility.Affects(_style, index, operationIndex)) continue;
                TextStudio2DResolvedSelection selection = GetSelection(in frame, adjustment.SelectorIndex);
                bool requireAll = adjustment.ColorAdjustment.SurfaceSelection == TextStudio2DSurfaceSelection.ExcludePartial;
                bool selected = requireAll;
                for (int member = 0; member < members.Count; member++)
                {
                    int unit = members[member];
                    bool matches = selection.GetOwner(unit) >= 0 && (!frame.VisualSnapshot.IsCreated || unit >= frame.VisualSnapshot.GlyphCount ||
                        TextStudio2DSourceApplicabilityUtility.Includes(in adjustment, frame.VisualSnapshot.GetGlyph(unit).SourceKind));
                    if (requireAll && !matches) { selected = false; break; }
                    if (!requireAll && matches) { selected = true; break; }
                }
                if (selected) mask[index >> 4] = (uint)mask[index >> 4] | 1u << (index & 15);
            }
            return mask;
        }

        sealed class SelectionEntry
        {
            internal readonly TextStudio2DResolvedSelection Selection = new TextStudio2DResolvedSelection();
            internal ulong Fingerprint;
            internal TextStudioCompiledPlan PlanIdentity;
            internal bool Ready;
        }
    }
}
