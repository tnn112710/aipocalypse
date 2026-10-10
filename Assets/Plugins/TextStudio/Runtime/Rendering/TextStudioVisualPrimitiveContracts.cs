using System;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    internal enum TextStudioVisualPrimitiveAuthoringRoute : byte
    {
        None = 0,
        Appearance = 1 << 0,
        ComposedEffect = 1 << 1
    }

    [Flags]
    internal enum TextStudioVisualPrimitiveFeatures : ushort
    {
        None = 0,
        ContinuousTime = 1 << 0,
        Directional = 1 << 1,
        ProceduralNoise = 1 << 2,
        DeterministicHash = 1 << 3,
        NeighborhoodSampling = 1 << 4,
        TemporalHistory = 1 << 5,
        DownsampledBlur = 1 << 6,
        CanFlash = 1 << 7,
        FieldNormal = 1 << 8,
        RadialResampling = 1 << 9
    }

    internal enum TextStudioVisualPrimitiveKind : byte
    {
        None = 0,
        SweepBand = 1,
        FilteredSparkle = 2,
        LiquidFill = 3,
        AnimatedBurningEdge = 4,
        AnimatedElectricOutline = 5,
        GlitchDisplacement = 6,
        HeatHaze = 7,
        LiquidWarp = 8,
        PixelDissolve = 9,
        EchoTrails = 10,
        MotionSmear = 11,
        ProgressiveBlurFocus = 12,
        ThresholdDissolve = 13,
        BurnAway = 14,
        OutlineGlowPulse = 15,
        ChromaticSlice = 16,
        PixelGrid = 17,
        SdfFocus = 18,
        Halftone = 19,
        Grain = 20,
        Duotone = 21,
        Distress = 22,
        InkBleed = 23,
        Letterpress = 24,
        StencilCutout = 25,
        StickerPlate = 26,
        CmykOffset = 27,
        RgbSeparation = 28,
        PrismaticEdge = 29,
        EngravedLine = 30,
        SurfaceMaterialResponse = 31,
        LensMagnifier = 32,
        Squiggle = 33
    }

    internal enum TextStudioVisualPrimitiveCostClass : byte
    {
        Analytic = 0,
        Procedural = 1,
        MultiSample = 2,
        Neighborhood = 3,
        Temporal = 4
    }

    internal enum TextStudioVisualPrimitiveBoundsMode : byte
    {
        None = 0,
        UniformRadius = 1,
        DirectionalRadius = 2,
        DirectionalHistory = 3,
        TargetRadius = 4
    }

    /// <summary>
    /// Canonical immutable metadata for one irreducible flat-text visual algorithm. Authoring
    /// routes remain separate and adapt to this contract only when their rendered math matches.
    /// </summary>
    internal readonly struct TextStudioVisualPrimitiveContract
    {
        internal TextStudioVisualPrimitiveKind Kind { get; }
        internal TextStudioVisualPrimitiveAuthoringRoute AuthoringRoutes { get; }
        internal TextStudioVisualPrimitiveFeatures Features { get; }
        internal TextStudioVisualPrimitiveCostClass CostClass { get; }
        internal TextStudioVisualPrimitiveBoundsMode BoundsMode { get; }
        internal TextStudioRenderDomain SupportedRenderDomains { get; }
        internal TextStudioPhaseMask SupportedPhases { get; }
        internal TextStudioEffectPerformanceTier PerformanceTier { get; }
        internal TextStudioReducedMotionBehavior ReducedMotionBehavior { get; }
        internal int OperationWeight { get; }
        internal int AdditionalPassCount { get; }
        internal int PostProcessTextureSamples { get; }
        internal int PeakScratchSurfaceCount { get; }
        internal int MaximumHistoryFrames { get; }

        internal bool IsDefined => Kind != TextStudioVisualPrimitiveKind.None;
        internal bool UsesProceduralNoise =>
            (Features & TextStudioVisualPrimitiveFeatures.ProceduralNoise) != 0;
        internal bool RequiresPostProcess =>
            (Features & (TextStudioVisualPrimitiveFeatures.NeighborhoodSampling |
                         TextStudioVisualPrimitiveFeatures.TemporalHistory |
                         TextStudioVisualPrimitiveFeatures.DownsampledBlur)) != 0;
        internal bool RequiresHistory =>
            (Features & TextStudioVisualPrimitiveFeatures.TemporalHistory) != 0;
        internal bool RequiresSpatialScratch =>
            RequiresPostProcess &&
            (Features & TextStudioVisualPrimitiveFeatures.DownsampledBlur) == 0;
        internal bool RequiresDownsampledBlur =>
            (Features & TextStudioVisualPrimitiveFeatures.DownsampledBlur) != 0;
        internal bool CanFlash =>
            (Features & TextStudioVisualPrimitiveFeatures.CanFlash) != 0;
        internal bool UsesFieldNormal =>
            (Features & TextStudioVisualPrimitiveFeatures.FieldNormal) != 0;

        internal TextStudioVisualPrimitiveContract(
            TextStudioVisualPrimitiveKind kind,
            TextStudioVisualPrimitiveAuthoringRoute authoringRoutes,
            TextStudioVisualPrimitiveFeatures features,
            TextStudioVisualPrimitiveCostClass costClass,
            TextStudioVisualPrimitiveBoundsMode boundsMode,
            TextStudioEffectPerformanceTier performanceTier,
            TextStudioReducedMotionBehavior reducedMotionBehavior,
            int operationWeight,
            int additionalPassCount = 0,
            int postProcessTextureSamples = 0,
            int peakScratchSurfaceCount = 0,
            int maximumHistoryFrames = 0,
            TextStudioPhaseMask supportedPhases = TextStudioPhaseMask.All,
            TextStudioRenderDomain supportedRenderDomains =
                TextStudioRenderDomain.FlatText)
        {
            Kind = kind;
            AuthoringRoutes = authoringRoutes;
            Features = features;
            CostClass = costClass;
            BoundsMode = boundsMode;
            SupportedRenderDomains = supportedRenderDomains;
            SupportedPhases = supportedPhases;
            PerformanceTier = performanceTier;
            ReducedMotionBehavior = reducedMotionBehavior;
            OperationWeight = Mathf.Max(1, operationWeight);
            AdditionalPassCount = Mathf.Max(0, additionalPassCount);
            PostProcessTextureSamples = Mathf.Max(0, postProcessTextureSamples);
            PeakScratchSurfaceCount = Mathf.Max(0, peakScratchSurfaceCount);
            MaximumHistoryFrames = Mathf.Max(0, maximumHistoryFrames);
        }
    }

    /// <summary>
    /// Allocation-free mappings from Appearance and Composed Effect authoring to canonical
    /// rendering primitives. Near-name effects are deliberately not mapped together unless their
    /// visible algorithm is equivalent.
    /// </summary>
    internal static class TextStudioVisualPrimitiveCatalog
    {
        const TextStudioVisualPrimitiveAuthoringRoute Appearance =
            TextStudioVisualPrimitiveAuthoringRoute.Appearance;
        const TextStudioVisualPrimitiveAuthoringRoute Composed =
            TextStudioVisualPrimitiveAuthoringRoute.ComposedEffect;
        const TextStudioVisualPrimitiveFeatures Timed =
            TextStudioVisualPrimitiveFeatures.ContinuousTime;
        const TextStudioVisualPrimitiveFeatures Directional =
            TextStudioVisualPrimitiveFeatures.Directional;
        const TextStudioVisualPrimitiveFeatures Noise =
            TextStudioVisualPrimitiveFeatures.ProceduralNoise;
        const TextStudioVisualPrimitiveFeatures Hash =
            TextStudioVisualPrimitiveFeatures.DeterministicHash;
        const TextStudioVisualPrimitiveFeatures Neighborhood =
            TextStudioVisualPrimitiveFeatures.NeighborhoodSampling;
        const TextStudioVisualPrimitiveFeatures History =
            TextStudioVisualPrimitiveFeatures.TemporalHistory;
        const TextStudioVisualPrimitiveFeatures Blur =
            TextStudioVisualPrimitiveFeatures.DownsampledBlur;
        const TextStudioVisualPrimitiveFeatures Flash =
            TextStudioVisualPrimitiveFeatures.CanFlash;
        const TextStudioVisualPrimitiveFeatures Radial =
            TextStudioVisualPrimitiveFeatures.RadialResampling;

        static readonly TextStudioVisualPrimitiveContract SweepBand = new(
            TextStudioVisualPrimitiveKind.SweepBand,
            Appearance | Composed,
            Timed | Directional,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract FilteredSparkle = new(
            TextStudioVisualPrimitiveKind.FilteredSparkle,
            Appearance,
            Timed | Directional | Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract LiquidFill = new(
            TextStudioVisualPrimitiveKind.LiquidFill,
            Appearance,
            Timed | Directional,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract AnimatedBurningEdge = new(
            TextStudioVisualPrimitiveKind.AnimatedBurningEdge,
            Appearance,
            Timed | Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract AnimatedElectricOutline = new(
            TextStudioVisualPrimitiveKind.AnimatedElectricOutline,
            Appearance,
            Timed | Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract GlitchDisplacement = new(
            TextStudioVisualPrimitiveKind.GlitchDisplacement,
            Appearance,
            Timed | Directional | Hash | Neighborhood | Flash,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Disable,
            5,
            additionalPassCount: 1,
            postProcessTextureSamples: 8,
            peakScratchSurfaceCount: 1);

        static readonly TextStudioVisualPrimitiveContract HeatHaze = new(
            TextStudioVisualPrimitiveKind.HeatHaze,
            Appearance,
            Timed | Directional | Neighborhood,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            5,
            additionalPassCount: 1,
            postProcessTextureSamples: 4,
            peakScratchSurfaceCount: 1);

        static readonly TextStudioVisualPrimitiveContract LiquidWarp = new(
            TextStudioVisualPrimitiveKind.LiquidWarp,
            Appearance,
            Timed | Directional | Neighborhood,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            5,
            additionalPassCount: 1,
            postProcessTextureSamples: 4,
            peakScratchSurfaceCount: 1);

        static readonly TextStudioVisualPrimitiveContract PixelDissolve = new(
            TextStudioVisualPrimitiveKind.PixelDissolve,
            Appearance,
            Timed | Noise | Flash,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Disable,
            3);

        static readonly TextStudioVisualPrimitiveContract EchoTrails = new(
            TextStudioVisualPrimitiveKind.EchoTrails,
            Appearance,
            Timed | Directional | Neighborhood | History,
            TextStudioVisualPrimitiveCostClass.Temporal,
            TextStudioVisualPrimitiveBoundsMode.DirectionalHistory,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            7,
            additionalPassCount: 3,
            postProcessTextureSamples: 10,
            peakScratchSurfaceCount: 1,
            maximumHistoryFrames: 4);

        static readonly TextStudioVisualPrimitiveContract MotionSmear = new(
            TextStudioVisualPrimitiveKind.MotionSmear,
            Appearance,
            Timed | Directional | Neighborhood,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            5,
            additionalPassCount: 1,
            postProcessTextureSamples: 10,
            peakScratchSurfaceCount: 1);

        static readonly TextStudioVisualPrimitiveContract ProgressiveBlurFocus = new(
            TextStudioVisualPrimitiveKind.ProgressiveBlurFocus,
            Appearance,
            Timed | Blur,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            6,
            additionalPassCount: 2,
            postProcessTextureSamples: 10,
            peakScratchSurfaceCount: 2);

        static readonly TextStudioVisualPrimitiveContract ThresholdDissolve = new(
            TextStudioVisualPrimitiveKind.ThresholdDissolve,
            Composed,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract BurnAway = new(
            TextStudioVisualPrimitiveKind.BurnAway,
            Composed,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract OutlineGlowPulse = new(
            TextStudioVisualPrimitiveKind.OutlineGlowPulse,
            Composed,
            Timed,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Reduce,
            3);

        static readonly TextStudioVisualPrimitiveContract ChromaticSlice = new(
            TextStudioVisualPrimitiveKind.ChromaticSlice,
            Composed,
            Timed | Directional | Hash | Flash,
            TextStudioVisualPrimitiveCostClass.MultiSample,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Disable,
            4);

        static readonly TextStudioVisualPrimitiveContract PixelGrid = new(
            TextStudioVisualPrimitiveKind.PixelGrid,
            Composed,
            Timed | Flash,
            TextStudioVisualPrimitiveCostClass.MultiSample,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Disable,
            4);

        static readonly TextStudioVisualPrimitiveContract Squiggle = new(
            TextStudioVisualPrimitiveKind.Squiggle,
            Composed,
            Timed | Noise | Hash,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Disable,
            1);

        static readonly TextStudioVisualPrimitiveContract SdfFocus = new(
            TextStudioVisualPrimitiveKind.SdfFocus,
            Composed,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            2);

        static readonly TextStudioVisualPrimitiveContract LensMagnifier = new(
            TextStudioVisualPrimitiveKind.LensMagnifier,
            Composed,
            Neighborhood | Radial,
            TextStudioVisualPrimitiveCostClass.Neighborhood,
            TextStudioVisualPrimitiveBoundsMode.TargetRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            6,
            additionalPassCount: 2,
            postProcessTextureSamples: 2,
            peakScratchSurfaceCount: 1,
            supportedPhases: TextStudioPhaseMask.Active);

        static readonly TextStudioVisualPrimitiveContract Halftone = new(
            TextStudioVisualPrimitiveKind.Halftone,
            Appearance,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract Grain = new(
            TextStudioVisualPrimitiveKind.Grain,
            Appearance,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract Duotone = new(
            TextStudioVisualPrimitiveKind.Duotone,
            Appearance,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract Distress = new(
            TextStudioVisualPrimitiveKind.Distress,
            Appearance,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract InkBleed = new(
            TextStudioVisualPrimitiveKind.InkBleed,
            Appearance,
            Noise,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            4);

        static readonly TextStudioVisualPrimitiveContract Letterpress = new(
            TextStudioVisualPrimitiveKind.Letterpress,
            Appearance,
            Noise | TextStudioVisualPrimitiveFeatures.FieldNormal,
            TextStudioVisualPrimitiveCostClass.Procedural,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            4);

        static readonly TextStudioVisualPrimitiveContract StencilCutout = new(
            TextStudioVisualPrimitiveKind.StencilCutout,
            Appearance,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract StickerPlate = new(
            TextStudioVisualPrimitiveKind.StickerPlate,
            Appearance,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.MultiSample,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract CmykOffset = new(
            TextStudioVisualPrimitiveKind.CmykOffset,
            Appearance,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.MultiSample,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            5);

        static readonly TextStudioVisualPrimitiveContract RgbSeparation = new(
            TextStudioVisualPrimitiveKind.RgbSeparation,
            Appearance,
            TextStudioVisualPrimitiveFeatures.None,
            TextStudioVisualPrimitiveCostClass.MultiSample,
            TextStudioVisualPrimitiveBoundsMode.DirectionalRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            4);

        static readonly TextStudioVisualPrimitiveContract PrismaticEdge = new(
            TextStudioVisualPrimitiveKind.PrismaticEdge,
            Appearance,
            TextStudioVisualPrimitiveFeatures.FieldNormal,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract EngravedLine = new(
            TextStudioVisualPrimitiveKind.EngravedLine,
            Appearance,
            TextStudioVisualPrimitiveFeatures.FieldNormal,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.UniformRadius,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        static readonly TextStudioVisualPrimitiveContract SurfaceMaterialResponse = new(
            TextStudioVisualPrimitiveKind.SurfaceMaterialResponse,
            Appearance,
            TextStudioVisualPrimitiveFeatures.FieldNormal,
            TextStudioVisualPrimitiveCostClass.Analytic,
            TextStudioVisualPrimitiveBoundsMode.None,
            TextStudioEffectPerformanceTier.Material,
            TextStudioReducedMotionBehavior.Preserve,
            3);

        internal static bool TryGet(
            TextStudio2DAnimatedEffectKind kind,
            out TextStudioVisualPrimitiveContract contract)
        {
            switch (kind)
            {
                case TextStudio2DAnimatedEffectKind.LightSweep:
                    contract = SweepBand;
                    return true;
                case TextStudio2DAnimatedEffectKind.Shimmer:
                    contract = FilteredSparkle;
                    return true;
                case TextStudio2DAnimatedEffectKind.LiquidFill:
                    contract = LiquidFill;
                    return true;
                case TextStudio2DAnimatedEffectKind.BurningEdge:
                    contract = AnimatedBurningEdge;
                    return true;
                case TextStudio2DAnimatedEffectKind.ElectricOutline:
                    contract = AnimatedElectricOutline;
                    return true;
                case TextStudio2DAnimatedEffectKind.GlitchDisplacement:
                    contract = GlitchDisplacement;
                    return true;
                case TextStudio2DAnimatedEffectKind.HeatHaze:
                    contract = HeatHaze;
                    return true;
                case TextStudio2DAnimatedEffectKind.LiquidWarp:
                    contract = LiquidWarp;
                    return true;
                case TextStudio2DAnimatedEffectKind.PixelDissolve:
                    contract = PixelDissolve;
                    return true;
                case TextStudio2DAnimatedEffectKind.EchoTrails:
                    contract = EchoTrails;
                    return true;
                case TextStudio2DAnimatedEffectKind.MotionSmear:
                    contract = MotionSmear;
                    return true;
                case TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus:
                    contract = ProgressiveBlurFocus;
                    return true;
                default:
                    contract = default;
                    return false;
            }
        }

        internal static bool TryGet(
            TextStudio2DGraphicEffectKind kind,
            out TextStudioVisualPrimitiveContract contract)
        {
            switch (kind)
            {
                case TextStudio2DGraphicEffectKind.Halftone:
                    contract = Halftone;
                    return true;
                case TextStudio2DGraphicEffectKind.Grain:
                    contract = Grain;
                    return true;
                case TextStudio2DGraphicEffectKind.Duotone:
                    contract = Duotone;
                    return true;
                case TextStudio2DGraphicEffectKind.Distress:
                    contract = Distress;
                    return true;
                case TextStudio2DGraphicEffectKind.InkBleed:
                    contract = InkBleed;
                    return true;
                case TextStudio2DGraphicEffectKind.Letterpress:
                    contract = Letterpress;
                    return true;
                case TextStudio2DGraphicEffectKind.StencilCutout:
                    contract = StencilCutout;
                    return true;
                case TextStudio2DGraphicEffectKind.StickerPlate:
                    contract = StickerPlate;
                    return true;
                case TextStudio2DGraphicEffectKind.CmykOffset:
                    contract = CmykOffset;
                    return true;
                case TextStudio2DGraphicEffectKind.RgbSeparation:
                    contract = RgbSeparation;
                    return true;
                case TextStudio2DGraphicEffectKind.PrismaticEdge:
                    contract = PrismaticEdge;
                    return true;
                case TextStudio2DGraphicEffectKind.EngravedLine:
                    contract = EngravedLine;
                    return true;
                default:
                    contract = default;
                    return false;
            }
        }

        internal static TextStudioVisualPrimitiveContract GetMaterialResponse()
        {
            return SurfaceMaterialResponse;
        }

        internal static bool TryGet(
            string effectId,
            out TextStudioVisualPrimitiveContract contract)
        {
            switch (effectId)
            {
                case TextStudioBuiltInEffectIds.SurfaceSweep:
                    contract = SweepBand;
                    return true;
                case TextStudioBuiltInEffectIds.Dissolve:
                    contract = ThresholdDissolve;
                    return true;
                case TextStudioBuiltInEffectIds.Burn:
                    contract = BurnAway;
                    return true;
                case TextStudioBuiltInEffectIds.OutlineGlow:
                    contract = OutlineGlowPulse;
                    return true;
                case TextStudioBuiltInEffectIds.ChromaticGlitch:
                    contract = ChromaticSlice;
                    return true;
                case TextStudioBuiltInEffectIds.PixelGrid:
                    contract = PixelGrid;
                    return true;
                case TextStudioBuiltInEffectIds.Squiggle:
                    contract = Squiggle;
                    return true;
                case TextStudioBuiltInEffectIds.Focus:
                    contract = SdfFocus;
                    return true;
                case TextStudioBuiltInEffectIds.LensMagnifier:
                    contract = LensMagnifier;
                    return true;
                default:
                    contract = default;
                    return false;
            }
        }
    }

    /// <summary>CPU reference math mirrored by TextStudio2DVisualPrimitiveCommon.cginc.</summary>
    internal static class TextStudioVisualPrimitiveMath
    {
        internal const float MinimumPositive = 0.000001f;
        internal const int MaximumHistoryFrames = 4;

        internal static float ResolveMotionRate(
            TextStudioMotionPolicy policy,
            TextStudioReducedMotionBehavior behavior,
            out bool enabled)
        {
            TextStudioMotionPolicy resolved = policy == TextStudioMotionPolicy.Inherit
                ? TextStudioMotionPolicy.Full
                : policy;
            if (resolved == TextStudioMotionPolicy.Full ||
                behavior == TextStudioReducedMotionBehavior.Preserve)
            {
                enabled = true;
                return 1f;
            }
            if (resolved == TextStudioMotionPolicy.Reduced &&
                behavior == TextStudioReducedMotionBehavior.Reduce)
            {
                enabled = true;
                return 0.25f;
            }

            enabled = false;
            return 0f;
        }

        internal static Vector2 NormalizeDirection(Vector2 direction)
        {
            float magnitude = direction.magnitude;
            return magnitude > 0.0001f ? direction / magnitude : Vector2.right;
        }

        internal static float SweepBand(float distance, float innerWidth, float outerWidth)
        {
            float inner = Mathf.Max(0f, innerWidth);
            float outer = Mathf.Max(inner + MinimumPositive, outerWidth);
            return 1f - SmoothStep(inner, outer, Mathf.Abs(distance));
        }

        internal static float ThresholdAbove(
            float sample,
            float threshold,
            float softness)
        {
            float extent = Mathf.Max(MinimumPositive, Mathf.Abs(softness));
            return SmoothStep(threshold - extent, threshold + extent, sample);
        }

        internal static bool SupportsMapping(TextStudio2DMappingSpace mapping, bool requiresPostProcess)
        {
            return requiresPostProcess
                ? mapping == TextStudio2DMappingSpace.Glyph || mapping == TextStudio2DMappingSpace.Text
                : (uint)mapping <= (uint)TextStudio2DMappingSpace.Range;
        }

        internal static bool AreAnimatedParametersValid(
            in TextStudio2DAnimatedEffectSpec effect)
        {
            return TextStudioVisualPrimitiveCatalog.TryGet(effect.Kind, out TextStudioVisualPrimitiveContract contract) &&
                   IsFiniteRange(effect.Amount, 0f, 4f) &&
                   IsFiniteRange(effect.Speed, -32f, 32f) &&
                   float.IsFinite(effect.Phase) &&
                   IsFiniteRange(effect.Scale, 0.01f, 4096f) &&
                   IsFiniteRange(effect.Width, 0f, 2f) &&
                   IsFiniteRange(effect.Softness, 0f, 2f) &&
                   IsFiniteRange(effect.Amplitude, 0f, 2f) &&
                   IsFiniteRange(effect.Frequency, 0.01f, 128f) &&
                   IsFinite(effect.Direction) &&
                   effect.Direction.sqrMagnitude <= 64f &&
                   IsFinite(effect.PrimaryColor) &&
                   IsFinite(effect.SecondaryColor) &&
                   effect.HistoryFrames >= 1 &&
                   effect.HistoryFrames <= MaximumHistoryFrames &&
                   SupportsMapping(effect.Mapping, contract.RequiresPostProcess);
        }

        internal static bool AreGraphicParametersValid(
            in TextStudio2DGraphicEffectSpec effect)
        {
            bool valid = TextStudioVisualPrimitiveCatalog.TryGet(effect.Kind, out _) &&
                   IsFiniteRange(effect.Intensity, 0f, 4f) &&
                   IsFiniteRange(effect.Scale, 0.01f, 4096f) &&
                   IsFiniteRange(effect.Detail, 0f, 1f) &&
                   IsFiniteRange(effect.Width, 0f, 2f) &&
                   IsFiniteRange(effect.Softness, 0f, 2f) &&
                   float.IsFinite(effect.Angle) &&
                   IsFinite(effect.Offset) &&
                   IsFinite(effect.PrimaryColor) &&
                   IsFinite(effect.SecondaryColor) &&
                   SupportsMapping(effect.Mapping, false);
            if (!valid || effect.Kind != TextStudio2DGraphicEffectKind.Halftone)
                return valid;

            TextStudio2DHalftoneSizeRamp ramp = effect.HalftoneSizeRamp;
            return !ramp.Enabled ||
                   IsFiniteRange(ramp.MinimumRadius, 0f, 0.5f) &&
                   IsFiniteRange(ramp.MaximumRadius, 0f, 0.5f) &&
                   ramp.MinimumRadius <= ramp.MaximumRadius &&
                   float.IsFinite(ramp.AngleDegrees);
        }

        internal static TextStudio2DBoundsExpansion CalculateBounds(
            in TextStudio2DAnimatedEffectSpec effect)
        {
            if (!TextStudioVisualPrimitiveCatalog.TryGet(
                    effect.Kind,
                    out TextStudioVisualPrimitiveContract contract))
            {
                return default;
            }

            float baseRadius = Mathf.Max(0f, effect.Width) +
                               Mathf.Max(0f, effect.Softness);
            switch (contract.BoundsMode)
            {
                case TextStudioVisualPrimitiveBoundsMode.UniformRadius:
                    return UniformBounds(baseRadius + Mathf.Max(0f, effect.Amplitude));
                case TextStudioVisualPrimitiveBoundsMode.DirectionalRadius:
                    Vector2 offset = NormalizeDirection(effect.Direction) *
                                     Mathf.Max(0f, effect.Amplitude);
                    return OffsetBounds(
                        offset,
                        baseRadius + Mathf.Max(0f, effect.Amplitude));
                case TextStudioVisualPrimitiveBoundsMode.DirectionalHistory:
                    float historyDistance = Mathf.Max(0f, effect.Amplitude) *
                                            Mathf.Clamp(
                                                effect.HistoryFrames,
                                                1,
                                                MaximumHistoryFrames);
                    return OffsetBounds(
                        NormalizeDirection(effect.Direction) * historyDistance,
                        baseRadius + historyDistance);
                default:
                    return default;
            }
        }

        internal static TextStudio2DBoundsExpansion CalculateHeroPostProcessBounds(
            in TextStudio2DAnimatedEffectSpec effect,
            Vector2 textSize,
            float glyphUnitScale)
        {
            if (!TextStudio2DAnimatedEffectUtility.RequiresPostProcess(effect.Kind) ||
                effect.Amount <= 0f)
            {
                return default;
            }

            Vector2 mappingSize = effect.Mapping == TextStudio2DMappingSpace.Text
                ? new Vector2(
                    Mathf.Max(MinimumPositive, Mathf.Abs(textSize.x)),
                    Mathf.Max(MinimumPositive, Mathf.Abs(textSize.y)))
                : Vector2.one * Mathf.Max(MinimumPositive, glyphUnitScale);
            Vector2 direction = NormalizeDirection(effect.Direction);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float amplitude = Mathf.Max(0f, effect.Amplitude);
            float amount = Mathf.Max(0f, effect.Amount);

            switch (effect.Kind)
            {
                case TextStudio2DAnimatedEffectKind.GlitchDisplacement:
                    return SymmetricDirectionalBounds(
                        direction,
                        mappingSize,
                        amplitude * amount * 1.7f);
                case TextStudio2DAnimatedEffectKind.HeatHaze:
                    return SymmetricTwoAxisBounds(
                        direction,
                        perpendicular,
                        mappingSize,
                        amplitude * amount,
                        amplitude * amount * 0.28f);
                case TextStudio2DAnimatedEffectKind.LiquidWarp:
                    return SymmetricTwoAxisBounds(
                        direction,
                        perpendicular,
                        mappingSize,
                        amplitude * amount * 1.22f,
                        amplitude * amount * 0.48f);
                case TextStudio2DAnimatedEffectKind.EchoTrails:
                    float historyMultiplier = Mathf.Max(
                        2f,
                        Mathf.Clamp(
                            effect.HistoryFrames,
                            1,
                            TextStudio2DAnimatedEffectUtility.MaximumHistoryFrames) + 0.45f);
                    return DirectionalBounds(
                        direction,
                        mappingSize,
                        amplitude * 1.2f * historyMultiplier);
                case TextStudio2DAnimatedEffectKind.MotionSmear:
                    return DirectionalBounds(
                        direction,
                        mappingSize,
                        amplitude * amount);
                case TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus:
                    float radius = Mathf.Max(
                                       0f,
                                       effect.Softness + effect.Amplitude) *
                                   Mathf.Max(MinimumPositive, glyphUnitScale);
                    return UniformBounds(radius);
                default:
                    return default;
            }
        }

        internal static TextStudio2DBoundsExpansion CalculateBounds(
            in TextStudio2DGraphicEffectSpec effect)
        {
            if (!TextStudioVisualPrimitiveCatalog.TryGet(
                    effect.Kind,
                    out TextStudioVisualPrimitiveContract contract))
            {
                return default;
            }

            float radius = Mathf.Max(0f, effect.Width + effect.Softness);
            switch (contract.BoundsMode)
            {
                case TextStudioVisualPrimitiveBoundsMode.UniformRadius:
                    return UniformBounds(radius);
                case TextStudioVisualPrimitiveBoundsMode.DirectionalRadius:
                    return OffsetBounds(effect.Offset, radius);
                default:
                    return default;
            }
        }

        static float SmoothStep(float edge0, float edge1, float value)
        {
            float range = Mathf.Max(MinimumPositive, edge1 - edge0);
            float t = Mathf.Clamp01((value - edge0) / range);
            return t * t * (3f - 2f * t);
        }

        static TextStudio2DBoundsExpansion UniformBounds(float radius)
        {
            return new TextStudio2DBoundsExpansion(radius, radius, radius, radius);
        }

        static TextStudio2DBoundsExpansion OffsetBounds(Vector2 offset, float radius)
        {
            return new TextStudio2DBoundsExpansion(
                radius + Mathf.Max(0f, -offset.x),
                radius + Mathf.Max(0f, -offset.y),
                radius + Mathf.Max(0f, offset.x),
                radius + Mathf.Max(0f, offset.y));
        }

        static TextStudio2DBoundsExpansion DirectionalBounds(
            Vector2 direction,
            Vector2 mappingSize,
            float distance)
        {
            Vector2 offset = Vector2.Scale(direction, mappingSize) *
                             Mathf.Max(0f, distance);
            return new TextStudio2DBoundsExpansion(
                Mathf.Max(0f, -offset.x),
                Mathf.Max(0f, -offset.y),
                Mathf.Max(0f, offset.x),
                Mathf.Max(0f, offset.y));
        }

        static TextStudio2DBoundsExpansion SymmetricDirectionalBounds(
            Vector2 direction,
            Vector2 mappingSize,
            float distance)
        {
            Vector2 extent = Vector2.Scale(direction, mappingSize) *
                             Mathf.Max(0f, distance);
            extent = new Vector2(Mathf.Abs(extent.x), Mathf.Abs(extent.y));
            return new TextStudio2DBoundsExpansion(
                extent.x,
                extent.y,
                extent.x,
                extent.y);
        }

        static TextStudio2DBoundsExpansion SymmetricTwoAxisBounds(
            Vector2 direction,
            Vector2 perpendicular,
            Vector2 mappingSize,
            float directionalDistance,
            float perpendicularDistance)
        {
            Vector2 directional = Vector2.Scale(direction, mappingSize) *
                                  Mathf.Max(0f, directionalDistance);
            Vector2 cross = Vector2.Scale(perpendicular, mappingSize) *
                            Mathf.Max(0f, perpendicularDistance);
            Vector2 extent = new Vector2(
                Mathf.Abs(directional.x) + Mathf.Abs(cross.x),
                Mathf.Abs(directional.y) + Mathf.Abs(cross.y));
            return new TextStudio2DBoundsExpansion(
                extent.x,
                extent.y,
                extent.x,
                extent.y);
        }

        static bool IsFiniteRange(float value, float minimum, float maximum)
        {
            return float.IsFinite(value) && value >= minimum && value <= maximum;
        }

        static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        static bool IsFinite(Color value)
        {
            return float.IsFinite(value.r) &&
                   float.IsFinite(value.g) &&
                   float.IsFinite(value.b) &&
                   float.IsFinite(value.a);
        }
    }
}
