using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DHeroCompositor
    {
        internal const int MaximumGeneratedContributionRuns = 256;
        static readonly int s_sourceRangeProperty = Shader.PropertyToID("_TextStudio2DHeroSourceRange");
        static readonly int s_generatedAdjustmentSelectionProperty = Shader.PropertyToID("_TextStudioHeroAdjustmentSelection");
        static readonly int s_generatedAlphaProperty = Shader.PropertyToID("_TextStudioHeroGeneratedAlpha");
        static readonly int s_generatedPaintAlphaProperty = Shader.PropertyToID("_TextStudioHeroGeneratedPaintAlpha");
        RenderTexture _generatedAlphaTexture;

        readonly struct GeneratedContributionRun
        {
            internal readonly ulong Selection;
            internal readonly int FirstSource, EndSource;
            internal GeneratedContributionRun(ulong selection, int firstSource, int endSource)
            { Selection = selection; FirstSource = firstSource; EndSource = endSource; }
            internal Vector4 PackedSelection => new Vector4(Selection & 65535, Selection >> 16 & 65535, Selection >> 32 & 65535, Selection >> 48 & 65535);
        }

        bool BuildGeneratedContributionRuns(in TextStudioFrame frame, CompiledTextStudio2DStyle style)
        {
            foreach (List<GeneratedContributionRun> runs in _occurrenceState.GeneratedContributionRuns) runs?.Clear();
            bool hasAdjustments = false;
            bool hasGeneratedPaint = false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                hasAdjustments |= operation.Kind == TextStudio2DLayerKind.ColorAdjustment;
                hasGeneratedPaint |= operation.Kind == TextStudio2DLayerKind.FauxDepth;
            }
            if (!hasAdjustments || !hasGeneratedPaint) return true;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            IReadOnlyList<int> occurrenceGlyphs = OccurrenceRenderGlyphs(in frame);
            int sourceCount = occurrenceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            GetOccurrenceUnitRange(in frame, out int unitStart, out int unitEnd);
            _occurrenceState.SourceUnitStart = unitStart;
            if (_occurrenceState.SourceAdjustmentSelections.Length < sourceCount)
                _occurrenceState.SourceAdjustmentSelections = new ulong[TextStudioBufferCapacity.ForCount(sourceCount)];
            if (_occurrenceState.UnitSourceRanges.Length < unitEnd - unitStart)
                _occurrenceState.UnitSourceRanges = new Vector2Int[TextStudioBufferCapacity.ForCount(unitEnd - unitStart)];
            Array.Fill(_occurrenceState.UnitSourceRanges, new Vector2Int(-1, 0));
            for (int localSource = 0; localSource < sourceCount; localSource++)
            {
                int source = occurrenceGlyphs == null ? localSource : occurrenceGlyphs[localSource];
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(source);
                if (glyph.TextUnitIndex < unitStart || glyph.TextUnitIndex >= unitEnd) { _occurrenceState.SourceAdjustmentSelections[localSource] = 0; continue; }
                ulong selected = 0;
                for (int index = 0; index < style.OperationCount; index++)
                    if (style.OperationAt(index).Kind == TextStudio2DLayerKind.ColorAdjustment &&
                        OperationApplies(style, in frame, index, glyph.TextUnitIndex, glyph.SourceKind)) selected |= 1UL << index;
                _occurrenceState.SourceAdjustmentSelections[localSource] = selected;
                Vector2Int range = _occurrenceState.UnitSourceRanges[glyph.TextUnitIndex - unitStart];
                _occurrenceState.UnitSourceRanges[glyph.TextUnitIndex - unitStart] = new Vector2Int(range.x < 0 ? source : range.x, source + 1);
            }
            int runCount = 0;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind != TextStudio2DLayerKind.FauxDepth) continue;
                ulong applicable = 0;
                for (int adjustment = 0; adjustment < index; adjustment++)
                    if (style.OperationAt(adjustment).Kind == TextStudio2DLayerKind.ColorAdjustment &&
                        TextStudio2DColorAdjustmentUtility.Affects(style, adjustment, index)) applicable |= 1UL << adjustment;
                if (applicable == 0) continue;
                List<GeneratedContributionRun> runs = _occurrenceState.GeneratedContributionRuns[index] ??= new List<GeneratedContributionRun>();
                for (int localSource = 0; localSource < sourceCount; localSource++)
                {
                    int source = occurrenceGlyphs == null ? localSource : occurrenceGlyphs[localSource];
                    ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(source);
                    if (!glyph.IsVisible || glyph.TextUnitIndex < unitStart || glyph.TextUnitIndex >= unitEnd ||
                        !OperationApplies(style, in frame, index, glyph.TextUnitIndex, glyph.SourceKind)) continue;
                    ulong selection = _occurrenceState.SourceAdjustmentSelections[localSource] & applicable;
                    if (runs.Count > 0 && runs[runs.Count - 1].Selection == selection)
                        runs[runs.Count - 1] = new GeneratedContributionRun(selection, runs[runs.Count - 1].FirstSource, source + 1);
                    else
                    {
                        if (++runCount > MaximumGeneratedContributionRuns) return false;
                        runs.Add(new GeneratedContributionRun(selection, source, source + 1));
                    }
                }
            }
            return true;
        }

        RenderTexture RenderDepthContribution(TextStudio2DHeroProgram program, int operationIndex,
            in TextStudio2DCompiledOperation operation, Vector4 selection = default, int firstSource = 0, int endSource = int.MaxValue)
        {
            bool surface = !_geometryCoveragePass && operation.FauxDepth.PaintMapping == TextStudio2DFauxDepthPaintMapping.Surface;
            float alpha = surface ? program.GetGeneratedPaintAlpha(operationIndex) : 1;
            if (surface && float.IsNaN(alpha))
                RenderLayer(operationIndex, operationIndex, HeroLayerSourceMode.GeneratedAlpha, firstSource, endSource, _generatedAlphaTexture);
            RenderLayer(operationIndex, operationIndex, surface ? HeroLayerSourceMode.GeneratedColor : HeroLayerSourceMode.PaintedFace, firstSource, endSource);
            return ApplyHeroFauxDepth(in operation, _layerTexture, selection, surface, alpha);
        }

        RenderTexture ComposeAdjustedDepth(TextStudio2DHeroProgram program, int operationIndex, in TextStudio2DCompiledOperation operation,
            RenderTexture backdrop, RenderTexture destination, in MaskState mask, Material composite)
        {
            foreach (GeneratedContributionRun run in _occurrenceState.GeneratedContributionRuns[operationIndex])
            {
                if (_glyphFilter >= 0)
                {
                    Vector2Int range = _occurrenceState.UnitSourceRanges[_glyphFilter - _occurrenceState.SourceUnitStart];
                    if (run.EndSource <= range.x || run.FirstSource >= range.y) continue;
                }
                RenderTexture source = RenderDepthContribution(program, operationIndex, in operation, run.PackedSelection, run.FirstSource, run.EndSource);
                if (_compositionFailed || source == null) return backdrop;
                Composite(backdrop, source, destination, operation.BlendMode, 1, false, in mask, composite);
                RenderTexture previous = backdrop; backdrop = destination; destination = previous;
            }
            return backdrop;
        }
    }
}
