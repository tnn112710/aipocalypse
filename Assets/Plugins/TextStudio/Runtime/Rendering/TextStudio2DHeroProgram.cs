using System;

namespace TextStudio
{
    internal enum TextStudio2DHeroProgramStatus : byte
    {
        Empty = 0,
        Ready = 1,
        InvalidStyle = 2,
        OperationCapacityExceeded = 3,
        RelationshipDepthExceeded = 4
    }

    internal readonly struct TextStudio2DHeroNode
    {
        internal readonly int OperationIndex;
        internal readonly int ParentOperationIndex;
        internal readonly int FirstChildOperationIndex;
        internal readonly int LastChildOperationIndex;
        internal readonly int PreviousSiblingOperationIndex;
        internal readonly int NextSiblingOperationIndex;
        internal readonly bool IsMaskSource;

        internal TextStudio2DHeroNode(
            int operationIndex,
            int parentOperationIndex,
            int firstChildOperationIndex,
            int lastChildOperationIndex,
            int previousSiblingOperationIndex,
            int nextSiblingOperationIndex,
            bool isMaskSource)
        {
            OperationIndex = operationIndex;
            ParentOperationIndex = parentOperationIndex;
            FirstChildOperationIndex = firstChildOperationIndex;
            LastChildOperationIndex = lastChildOperationIndex;
            PreviousSiblingOperationIndex = previousSiblingOperationIndex;
            NextSiblingOperationIndex = nextSiblingOperationIndex;
            IsMaskSource = isMaskSource;
        }

        internal TextStudio2DHeroNode WithChildren(int first, int last)
        {
            return new TextStudio2DHeroNode(
                OperationIndex,
                ParentOperationIndex,
                first,
                last,
                PreviousSiblingOperationIndex,
                NextSiblingOperationIndex,
                IsMaskSource);
        }

        internal TextStudio2DHeroNode WithSiblings(int previous, int next)
        {
            return new TextStudio2DHeroNode(
                OperationIndex,
                ParentOperationIndex,
                FirstChildOperationIndex,
                LastChildOperationIndex,
                previous,
                next,
                IsMaskSource);
        }
    }

    /// <summary>
    /// Immutable paint tree for Path C. Mask rows are non-display operators and their source
    /// rows are mask-only, so a visible copy is authored explicitly instead of being drawn twice.
    /// </summary>
    internal sealed class TextStudio2DHeroProgram
    {
        internal const int MaximumOperations = 64;
        internal const int MaximumRelationshipDepth = 8;

        readonly TextStudio2DHeroNode[] _nodes;
        readonly TextStudio2DPainterOrder[] _orders;

        internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ArrayBytes(_nodes) +
            TextStudioAppearanceManagedMemory.ArrayBytes(_orders) + TextStudioAppearanceManagedMemory.ArrayBytes(_generatedPaintAlpha);

        internal CompiledTextStudio2DStyle Style { get; }
        internal TextStudioStackFingerprint Fingerprint => Style.Fingerprint;
        internal int OperationCount => _nodes.Length;
        internal int FirstRootOperationIndex { get; }
        internal int LastRootOperationIndex { get; }
        internal int MaximumGroupDepth { get; }
        internal int MaximumMaskDepth { get; }
        internal int CompositionPairCount { get; }
        internal int SpatialScratchCount { get; }
        internal int DownsampledBlurScratchCount { get; }
        internal int DownsampledBlurFactor { get; }
        internal int PostProcessScratchCount =>
            SpatialScratchCount + DownsampledBlurScratchCount;
        internal int MaximumHistoryFrames { get; }
        internal bool NeedsHistoryCoverage { get; }
        internal int HistorySurfaceCount => MaximumHistoryFrames * (NeedsHistoryCoverage ? 2 : 1);
        internal bool HasCompositeAdjustments { get; }
        internal int CoveragePairCount { get; }
        internal bool NeedsGeneratedAlpha { get; }
        readonly float[] _generatedPaintAlpha;
        internal float GetGeneratedPaintAlpha(int operationIndex) => _generatedPaintAlpha[operationIndex];
        internal bool RequiresPostProcess => PostProcessScratchCount > 0 || HasCompositeAdjustments;
        internal bool RequiresHistory => MaximumHistoryFrames > 0;
        internal int EstimatedConcurrentSurfaceCount { get; }
        /// <summary>
        /// Surfaces a composition leases only while they fit beside its layout: the row mask stencil when rows have distinct
        /// supports, and the retained static rows when an animated program has a cacheable run.
        /// </summary>
        internal int OptionalSurfaceCount { get; }

        TextStudio2DHeroProgram(
            CompiledTextStudio2DStyle style,
            TextStudio2DHeroNode[] nodes,
            int firstRoot,
            int lastRoot,
            int maximumGroupDepth,
            int maximumMaskDepth,
            TextStudio2DPainterOrder[] orders,
            int compositionPairCount)
        {
            Style = style;
            _nodes = nodes;
            _generatedPaintAlpha = new float[nodes.Length];
            _orders = orders;
            CompositionPairCount = compositionPairCount;
            FirstRootOperationIndex = firstRoot;
            LastRootOperationIndex = lastRoot;
            MaximumGroupDepth = maximumGroupDepth;
            MaximumMaskDepth = maximumMaskDepth;
            int spatialScratchCount = 0;
            int downsampledBlurScratchCount = 0;
            int downsampledBlurFactor = 1;
            int maximumHistoryFrames = 0;
            bool hasCompositeAdjustments = false;
            bool hasIndependentCoverage = false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind == TextStudio2DLayerKind.CompositeAdjustment)
                {
                    hasCompositeAdjustments = true;
                    hasIndependentCoverage |= operation.ColorAdjustment.RequiresIndependentCoverage;
                    spatialScratchCount = 1;
                    continue;
                }
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
                {
                    spatialScratchCount = 1;
                    if (operation.FauxDepth.PaintMapping == TextStudio2DFauxDepthPaintMapping.Surface)
                    {
                        _generatedPaintAlpha[index] = ResolveConstantPaintAlpha(style, operation.PaintIndex);
                        NeedsGeneratedAlpha |= float.IsNaN(_generatedPaintAlpha[index]);
                    }
                }
                if (TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(
                        in operation,
                        out _,
                        out _,
                        out _))
                {
                    spatialScratchCount = 1;
                    downsampledBlurScratchCount = 3;
                    downsampledBlurFactor = downsampledBlurFactor == 1
                        ? TextStudio2DHeroCompositor.TextWideBlurDownsampleFactor
                        : Math.Min(
                            downsampledBlurFactor,
                            TextStudio2DHeroCompositor.TextWideBlurDownsampleFactor);
                }
                if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect)
                    continue;
                TextStudio2DAnimatedEffectSpec effect = operation.AnimatedEffect;
                if (TextStudio2DAnimatedEffectUtility.RequiresSpatialScratch(effect.Kind))
                    spatialScratchCount = 1;
                if (TextStudio2DAnimatedEffectUtility.RequiresDownsampledBlur(effect.Kind))
                {
                    downsampledBlurScratchCount = Math.Max(downsampledBlurScratchCount, 2);
                    downsampledBlurFactor = downsampledBlurFactor == 1
                        ? TextStudio2DHeroCompositor.ProgressiveBlurDownsampleFactor
                        : Math.Min(
                            downsampledBlurFactor,
                            TextStudio2DHeroCompositor.ProgressiveBlurDownsampleFactor);
                }
                if (TextStudio2DAnimatedEffectUtility.RequiresHistory(effect.Kind))
                {
                    maximumHistoryFrames = Math.Max(
                        maximumHistoryFrames,
                        effect.HistoryFrames);
                }
            }
            SpatialScratchCount = spatialScratchCount;
            DownsampledBlurScratchCount = downsampledBlurScratchCount;
            DownsampledBlurFactor = downsampledBlurScratchCount > 0
                ? downsampledBlurFactor
                : 1;
            MaximumHistoryFrames = maximumHistoryFrames;
            NeedsHistoryCoverage = maximumHistoryFrames > 0 && hasIndependentCoverage;
            HasCompositeAdjustments = hasCompositeAdjustments;
            CoveragePairCount = hasIndependentCoverage ? maximumMaskDepth + 1 : 0;
            EstimatedConcurrentSurfaceCount = 3 + CompositionPairCount * 2 +
                                              spatialScratchCount + downsampledBlurScratchCount + HistorySurfaceCount + CoveragePairCount * 2 + (NeedsGeneratedAlpha ? 1 : 0);
            bool rowMasks = TextStudio2DHeroCompositor.GroupRowMaskClasses(style, false, new int[MaximumOperations],
                new int[TextStudio2DHeroCompositor.MaximumRowMaskClasses]) > 1;
            bool staticRows = TextStudio2DHeroCompositor.FindStaticRun(this, TextStudioMotionPolicy.Full, out _, out _) > 0;
            OptionalSurfaceCount = (rowMasks ? 1 : 0) + (staticRows ? 1 : 0);
        }

        static float ResolveConstantPaintAlpha(CompiledTextStudio2DStyle style, int paintIndex)
        {
            TextStudio2DCompiledPaint paint = style.GetPaint(paintIndex);
            if (paint.Kind == TextStudio2DPaintKind.Solid) return paint.Color.a;
            if (paint.Kind == TextStudio2DPaintKind.Texture || paint.Kind == TextStudio2DPaintKind.Pattern ||
                paint.Kind == TextStudio2DPaintKind.ProceduralNoise || paint.GradientStopCount == 0) return float.NaN;
            float alpha = style.GetGradientStop(paint.GradientStopStart).Color.a;
            for (int index = 1; index < paint.GradientStopCount; index++)
                if (style.GetGradientStop(paint.GradientStopStart + index).Color.a != alpha) return float.NaN;
            return alpha;
        }

        internal TextStudio2DPainterOrder GetPainterOrder(int operationIndex) => _orders[operationIndex + 1];

        static TextStudio2DPainterOrder ResolvePainterOrder(CompiledTextStudio2DStyle style, int owner)
        {
            for (int depth = 0; owner >= 0 && depth < MaximumRelationshipDepth; depth++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(owner);
                if (operation.GroupOrder != TextStudio2DGroupOrder.Inherit)
                    return operation.GroupOrder == TextStudio2DGroupOrder.ByCharacter
                        ? TextStudio2DPainterOrder.GlyphMajor : TextStudio2DPainterOrder.LayerMajor;
                owner = operation.ParentOperationIndex;
            }
            return style.PainterOrder;
        }

        internal TextStudio2DHeroNode GetNode(int operationIndex)
        {
            if (operationIndex < 0 || operationIndex >= _nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(operationIndex));
            return _nodes[operationIndex];
        }

        internal int GetMaskSourceOperationIndex(int operationIndex)
        {
            if (operationIndex < 0 || operationIndex >= _nodes.Length)
                return -1;
            ref readonly TextStudio2DCompiledOperation operation = ref Style.OperationAt(operationIndex);
            int maskIndex = operation.MaskOperationIndex;
            if (maskIndex < 0 || maskIndex >= _nodes.Length)
                return -1;
            ref readonly TextStudio2DCompiledOperation mask = ref Style.OperationAt(maskIndex);
            return mask.Kind == TextStudio2DLayerKind.Mask
                ? mask.MaskOperationIndex
                : -1;
        }

        internal static bool RequiresHero(CompiledTextStudio2DStyle style) =>
            ResolveHeroReason(style) != TextStudio2DRouteReason.None;

        /// <summary>The image dependency that requires isolated composition, or None. Operation count is never one.</summary>
        internal static TextStudio2DRouteReason ResolveHeroReason(CompiledTextStudio2DStyle style)
        {
            if (style == null || !style.IsValid || style.IsPassThrough)
                return TextStudio2DRouteReason.None;
            if ((style.Requirements & TextStudio2DStyleRequirements.GlyphMajorPainterOrder) != 0)
                return TextStudio2DRouteReason.GlyphMajorOrder;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.SurfaceOwnerOperationIndex >= 0) continue;
                // Retained depth geometry is glyph-relative. Pixel-sized depth
                // needs the compositor's target-pixel coverage and tight bounds.
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth &&
                    operation.FauxDepth.Units == TextStudio2DUnit.Pixels)
                    return TextStudio2DRouteReason.PixelUnitDepth;
                if (operation.ExecutionPath == TextStudio2DExecutionPath.HeroCompositor)
                    return TextStudio2DRouteReason.CompositorExecution;
                if (operation.BlendMode != TextStudio2DBlendMode.Normal)
                    return TextStudio2DRouteReason.BlendMode;
                if (operation.Kind == TextStudio2DLayerKind.Group ||
                    operation.Kind == TextStudio2DLayerKind.Mask ||
                    operation.ParentOperationIndex >= 0 ||
                    operation.MaskOperationIndex >= 0)
                {
                    return TextStudio2DRouteReason.GroupOrMask;
                }
            }
            return TextStudio2DRouteReason.None;
        }

        internal static bool TryCreate(
            CompiledTextStudio2DStyle style,
            out TextStudio2DHeroProgram program,
            out TextStudio2DHeroProgramStatus status)
        {
            program = null;
            if (style == null || style.IsPassThrough)
            {
                status = TextStudio2DHeroProgramStatus.Empty;
                return true;
            }
            if (!style.IsValid)
            {
                status = TextStudio2DHeroProgramStatus.InvalidStyle;
                return false;
            }
            if (style.HeroProgramStatus != TextStudio2DHeroProgramStatus.Empty)
            {
                program = style.HeroProgram;
                status = style.HeroProgramStatus;
                return status == TextStudio2DHeroProgramStatus.Ready;
            }
            if (style.OperationCount > MaximumOperations)
            {
                status = TextStudio2DHeroProgramStatus.OperationCapacityExceeded;
                return false;
            }

            int operationCount = style.OperationCount;
            bool[] maskSources = new bool[operationCount];
            for (int index = 0; index < operationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind != TextStudio2DLayerKind.Mask)
                    continue;
                int sourceIndex = operation.MaskOperationIndex;
                if (sourceIndex < 0 ||
                    sourceIndex >= operationCount ||
                    style.OperationAt(sourceIndex).Kind == TextStudio2DLayerKind.Mask)
                {
                    status = TextStudio2DHeroProgramStatus.InvalidStyle;
                    return false;
                }
                maskSources[sourceIndex] = true;
            }

            TextStudio2DHeroNode[] nodes = new TextStudio2DHeroNode[operationCount];
            for (int index = 0; index < operationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                nodes[index] = new TextStudio2DHeroNode(
                    index,
                    operation.ParentOperationIndex,
                    -1,
                    -1,
                    -1,
                    -1,
                    maskSources[index]);
            }

            int firstRoot = -1;
            int lastRoot = -1;
            for (int index = 0; index < operationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind == TextStudio2DLayerKind.Mask || maskSources[index] || operation.SurfaceOwnerOperationIndex >= 0)
                    continue;

                int parentIndex = operation.ParentOperationIndex;
                if (parentIndex >= 0)
                {
                    if (parentIndex >= operationCount ||
                        style.OperationAt(parentIndex).Kind != TextStudio2DLayerKind.Group)
                    {
                        status = TextStudio2DHeroProgramStatus.InvalidStyle;
                        return false;
                    }
                    AppendChild(nodes, parentIndex, index);
                }
                else
                {
                    AppendRoot(nodes, ref firstRoot, ref lastRoot, index);
                }
            }

            int maximumGroupDepth = ResolveMaximumGroupDepth(style);
            if (maximumGroupDepth > MaximumRelationshipDepth)
            {
                status = TextStudio2DHeroProgramStatus.RelationshipDepthExceeded;
                return false;
            }

            TextStudio2DPainterOrder[] orders = new TextStudio2DPainterOrder[operationCount + 1];
            orders[0] = style.PainterOrder;
            for (int index = 0; index < operationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                orders[index + 1] = ResolvePainterOrder(style, operation.Kind == TextStudio2DLayerKind.Group ? index : operation.ParentOperationIndex);
            }
            bool valid = true;
            CompositionDemand demand = ResolveScopeDemand(style, nodes, orders, -1, lastRoot,
                new byte[operationCount], new CompositionDemand[operationCount], ref valid);
            if (!valid)
            {
                status = TextStudio2DHeroProgramStatus.InvalidStyle;
                return false;
            }
            if (demand.MaskDepth > MaximumRelationshipDepth)
            {
                status = TextStudio2DHeroProgramStatus.RelationshipDepthExceeded;
                return false;
            }

            program = new TextStudio2DHeroProgram(
                style,
                nodes,
                firstRoot,
                lastRoot,
                maximumGroupDepth,
                demand.MaskDepth,
                orders,
                demand.Pairs);
            status = TextStudio2DHeroProgramStatus.Ready;
            return true;
        }

        static void AppendRoot(
            TextStudio2DHeroNode[] nodes,
            ref int firstRoot,
            ref int lastRoot,
            int operationIndex)
        {
            if (firstRoot < 0)
                firstRoot = operationIndex;
            if (lastRoot >= 0)
            {
                nodes[lastRoot] = nodes[lastRoot].WithSiblings(
                    nodes[lastRoot].PreviousSiblingOperationIndex,
                    operationIndex);
                nodes[operationIndex] = nodes[operationIndex].WithSiblings(
                    lastRoot,
                    -1);
            }
            lastRoot = operationIndex;
        }

        static void AppendChild(
            TextStudio2DHeroNode[] nodes,
            int parentIndex,
            int operationIndex)
        {
            TextStudio2DHeroNode parent = nodes[parentIndex];
            int first = parent.FirstChildOperationIndex;
            int last = parent.LastChildOperationIndex;
            if (first < 0)
                first = operationIndex;
            if (last >= 0)
            {
                nodes[last] = nodes[last].WithSiblings(
                    nodes[last].PreviousSiblingOperationIndex,
                    operationIndex);
                nodes[operationIndex] = nodes[operationIndex].WithSiblings(
                    last,
                    -1);
            }
            nodes[parentIndex] = parent.WithChildren(first, operationIndex);
        }

        static int ResolveMaximumGroupDepth(CompiledTextStudio2DStyle style)
        {
            int maximum = 0;
            for (int index = 0; index < style.OperationCount; index++)
            {
                int depth = 0;
                int parent = style.OperationAt(index).ParentOperationIndex;
                while (parent >= 0 && depth <= style.OperationCount)
                {
                    depth++;
                    parent = style.OperationAt(parent).ParentOperationIndex;
                }
                maximum = Math.Max(maximum, depth);
            }
            return maximum;
        }

        readonly struct CompositionDemand
        {
            internal readonly int Pairs, MaskDepth;
            internal CompositionDemand(int pairs, int maskDepth) { Pairs = pairs; MaskDepth = maskDepth; }
        }

        static CompositionDemand ResolveScopeDemand(CompiledTextStudio2DStyle style, TextStudio2DHeroNode[] nodes,
            TextStudio2DPainterOrder[] orders, int scope, int last, byte[] states, CompositionDemand[] demands, ref bool valid)
        {
            int pairs = 0, masks = 0;
            for (int index = last; index >= 0; index = nodes[index].PreviousSiblingOperationIndex)
            {
                CompositionDemand child = ResolveNodeDemand(style, nodes, orders, index, states, demands, ref valid);
                pairs = Math.Max(pairs, child.Pairs);
                masks = Math.Max(masks, child.MaskDepth);
            }
            return new CompositionDemand(pairs + (orders[scope + 1] == TextStudio2DPainterOrder.GlyphMajor ? 1 : 0), masks);
        }

        static CompositionDemand ResolveNodeDemand(CompiledTextStudio2DStyle style, TextStudio2DHeroNode[] nodes,
            TextStudio2DPainterOrder[] orders, int index, byte[] states, CompositionDemand[] demands, ref bool valid)
        {
            if (states[index] == 2) return demands[index];
            if (states[index] == 1) { valid = false; return default; }
            states[index] = 1;
            ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
            CompositionDemand mask = default;
            if (operation.MaskOperationIndex >= 0)
            {
                int source = style.OperationAt(operation.MaskOperationIndex).MaskOperationIndex;
                CompositionDemand sourceDemand = ResolveNodeDemand(style, nodes, orders, source, states, demands, ref valid);
                mask = new CompositionDemand(1 + sourceDemand.Pairs, 1 + sourceDemand.MaskDepth);
            }
            CompositionDemand result = mask;
            if (operation.Kind == TextStudio2DLayerKind.Group)
            {
                CompositionDemand children = ResolveScopeDemand(style, nodes, orders, index, nodes[index].LastChildOperationIndex, states, demands, ref valid);
                // Keep the group's composed image alive while its mask source uses the deeper pairs.
                result = new CompositionDemand(1 + Math.Max(children.Pairs, mask.Pairs), Math.Max(children.MaskDepth, mask.MaskDepth));
            }
            states[index] = 2;
            return demands[index] = result;
        }
    }
}
