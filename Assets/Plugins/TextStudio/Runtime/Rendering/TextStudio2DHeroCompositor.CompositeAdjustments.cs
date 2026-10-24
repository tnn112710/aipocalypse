using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DHeroCompositor
    {
        static readonly int s_geometryCoverageProperty = Shader.PropertyToID("_TextStudio2DGeometryCoveragePass");
        static readonly int s_compositeCoverageProperty = Shader.PropertyToID("_TextStudioHeroCompositeCoverage");
        int _coverageScratchStart, _coveragePairCount, _coverageReplayDepth;
        bool _geometryCoveragePass;

        void BuildCompositeScopes(in TextStudioFrame frame, CompiledTextStudio2DStyle style)
        {
            GetOccurrenceUnitRange(in frame, out int unitStart, out int unitEnd);
            for (int index = 0; index < style.OperationCount; index++)
            {
                _occurrenceState.WholeCompositeScope[index] = false;
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind != TextStudio2DLayerKind.CompositeAdjustment) continue;
                bool any = false, whole = true;
                for (int unit = unitStart; unit < unitEnd; unit++)
                {
                    if (frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null &&
                        !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, unit)) continue;
                    TextStudioVisualSourceKind sourceKind = frame.VisualSnapshot.GetGlyph(unit).SourceKind;
                    if (operation.ParentOperationIndex >= 0 && !OperationApplies(style, in frame, operation.ParentOperationIndex, unit, sourceKind)) continue;
                    bool selected = OperationApplies(style, in frame, index, unit, sourceKind);
                    any |= selected; whole &= selected;
                }
                _occurrenceState.WholeCompositeScope[index] = any && whole;
                _occurrenceState.OperationHasSelection[index] |= any;
            }
        }

        RenderTexture RenderCompositeCoverage(TextStudio2DHeroProgram program, int adjustmentIndex, int nestedDepth, Material composite)
        {
            if (_coverageReplayDepth >= _coveragePairCount) { _compositionFailed = true; return null; }
            int pairIndex = _coverageScratchStart + _coverageReplayDepth++ * 2;
            CompositionPair pair = new CompositionPair(_scratchTextures[pairIndex], _scratchTextures[pairIndex + 1]);
            bool previousCoverage = _geometryCoveragePass;
            int previousGlyph = _glyphFilter;
            SetGeometryCoveragePass(true);
            try
            {
                int scope = program.Style.OperationAt(adjustmentIndex).ParentOperationIndex;
                int last = scope < 0 ? program.LastRootOperationIndex : program.GetNode(scope).LastChildOperationIndex;
                bool characterOrder = program.GetPainterOrder(scope) == TextStudio2DPainterOrder.GlyphMajor;
                // Character-scope image boundaries execute one level beyond that scope's reusable glyph pair.
                return characterOrder
                    ? ComposeGlyphMajor(program, scope, last, in pair, nestedDepth - 1, composite, adjustmentIndex)
                    : ComposeRange(program, last, in pair, nestedDepth, composite, adjustmentIndex);
            }
            finally
            {
                SetGeometryCoveragePass(previousCoverage);
                SetGlyphFilter(previousGlyph);
                _coverageReplayDepth--;
            }
        }

        void SetGeometryCoveragePass(bool enabled)
        {
            _geometryCoveragePass = enabled;
            _commands.SetGlobalFloat(s_geometryCoverageProperty, enabled ? 1 : 0);
        }

        RenderTexture ComposeAdjustment(TextStudio2DHeroProgram program, int operationIndex,
            in TextStudio2DCompiledOperation operation, RenderTexture backdrop, RenderTexture destination, int nestedDepth, Material composite)
        {
            if (_geometryCoveragePass) return backdrop;
            RenderTexture coverage = operation.ColorAdjustment.RequiresIndependentCoverage
                ? RenderCompositeCoverage(program, operationIndex, nestedDepth, composite) : null;
            if (_compositionFailed) return backdrop;
            MaskState mask = PrepareMask(program, operation.MaskOperationIndex, nestedDepth, composite);
            if (_compositionFailed) return backdrop;
            RenderTexture selector = null;
            if (!_occurrenceState.WholeCompositeScope[operationIndex])
            {
                RenderLayer(operationIndex, operationIndex, HeroLayerSourceMode.SelectionMask);
                selector = _layerTexture;
            }
            // At full contribution the composite would only copy the adjusted image, so the filter writes the destination.
            bool direct = PassElisionEnabled && operation.BlendMode == TextStudio2DBlendMode.Normal && mask.Texture == null &&
                          Mathf.Clamp01(operation.Opacity) >= 1f;
            RenderTexture source = ApplyColorAdjustment(in operation, backdrop, selector, coverage, direct ? destination : _effectScratchTexture0);
            if (_compositionFailed || source == null) return backdrop;
            if (direct) return destination;
            Composite(backdrop, source, destination, operation.BlendMode, Mathf.Clamp01(operation.Opacity), true, in mask, composite);
            return destination;
        }
    }
}
