using System;
using System.Collections.Generic;

namespace TextStudio.Editor
{
    internal enum TextStudioVisualDirectionFamily
    {
        CoreAppearance,
        DimensionalMaterial,
        EditorialGraphic,
        AnimatedHero,
        WritingReveal
    }

    internal enum TextStudioVisualDirectionContractKind
    {
        Paint,
        Layer,
        MaterialResponse,
        GraphicEffect,
        AnimatedEffect,
        Writing,
        Typewriter
    }

    internal readonly struct TextStudioVisualDirectionEntry
    {
        internal string Id { get; }
        internal string Title { get; }
        internal TextStudioVisualDirectionFamily Family { get; }
        internal TextStudioVisualDirectionContractKind ContractKind { get; }
        internal int ContractValue { get; }
        internal string SampleEntryId { get; }
        internal TextGalleryRenderCost RenderCost { get; }
        internal string Fallback { get; }

        internal TextStudioVisualDirectionEntry(
            string id,
            string title,
            TextStudioVisualDirectionFamily family,
            TextStudioVisualDirectionContractKind contractKind,
            int contractValue,
            string sampleEntryId,
            TextGalleryRenderCost renderCost,
            string fallback)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Family = family;
            ContractKind = contractKind;
            ContractValue = contractValue;
            SampleEntryId = sampleEntryId ?? string.Empty;
            RenderCost = renderCost;
            Fallback = fallback ?? string.Empty;
        }
    }

    /// <summary>
    /// Source-backed coverage ledger for the sixty visual directions in the original handoff.
    /// It maps marketing language to canonical public contracts and real production samples,
    /// without creating a second renderer or claiming calibrated recipes are new algorithms.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Authored design inventories stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioVisualDirectionCatalog
    {
        const string FieldFallback =
            "Use the nearest supported fused field operation and preserve readable source text.";
        const string HeroFallback =
            "Disable the enhancement when Hero composition is unavailable and preserve the base Appearance.";
        const string WritingFallback =
            "Resolve phrase path, glyph path, outline trace, ink reveal, then ordinary Typewriter in that order.";

        static readonly TextStudioVisualDirectionEntry[] s_entries =
        {
            Paint("core.solid-fill", "Solid Fill", TextStudio2DPaintKind.Solid, "AppearanceAccessibleSubtitle"),
            Paint("core.linear-gradient", "Linear Gradient", TextStudio2DPaintKind.LinearGradient, "AppearanceCleanEditorial"),
            Paint("core.radial-gradient", "Radial Gradient", TextStudio2DPaintKind.RadialGradient, "AppearanceAuroraRadial"),
            Paint("core.conic-gradient", "Conic / Angle Gradient", TextStudio2DPaintKind.ConicGradient, "AppearanceSpectrumAngle"),
            Paint("core.texture-pattern-fill", "Texture / Pattern Fill", TextStudio2DPaintKind.Pattern, "AppearanceRetroPoster"),
            Layer("core.multiple-strokes", "Multiple Concentric Strokes", TextStudio2DLayerKind.Stroke, "AppearanceLayeredOutline", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.drop-shadow", "Drop Shadow", TextStudio2DLayerKind.OuterShadow, "AppearanceAccessibleSubtitle", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.compact-multi-shadow", "Compact Multi-Shadow", TextStudio2DLayerKind.OuterShadow, "AppearanceEditorialMultiShadow", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.long-shadow", "Long Shadow / Faux Extrusion", TextStudio2DLayerKind.FauxDepth, "AppearanceComicImpact", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.outer-glow", "Outer Glow", TextStudio2DLayerKind.OuterGlow, "AppearanceNeonCyan", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.inner-shadow-glow", "Inner Shadow / Glow", TextStudio2DLayerKind.InnerGlow, "AppearanceInnerLight", TextGalleryRenderCost.FusedSingleDraw),
            Layer("core.decorations", "Highlight Plate And Line Decorations", TextStudio2DLayerKind.Decoration, "AppearanceHighlightPlate", TextGalleryRenderCost.AdditionalGeometry),

            LayerMaterial("material.bevel", "Bevel", TextStudio2DLayerKind.Bevel, "AppearanceFacetedBevel"),
            Material("material.emboss", "Emboss", TextStudio2DMaterialResponseKind.Emboss, "AppearanceCleanEditorial"),
            Material("material.deboss", "Deboss / Letterpress", TextStudio2DMaterialResponseKind.Deboss, "AppearanceRetroPoster"),
            Material("material.satin", "Satin", TextStudio2DMaterialResponseKind.Satin, "AppearanceSatinRose"),
            Material("material.chrome", "Chrome", TextStudio2DMaterialResponseKind.Chrome, "AppearanceChromeTitle"),
            Material("material.gold-foil", "Gold / Foil", TextStudio2DMaterialResponseKind.GoldFoil, "AppearanceCinematicGold"),
            Material("material.enamel", "Enamel", TextStudio2DMaterialResponseKind.Enamel, "AppearanceScifiHud"),
            Material("material.bubble-gel", "Inflated Bubble / Gel", TextStudio2DMaterialResponseKind.BubbleGel, "AppearanceBubbleGel"),
            Material("material.neon", "Neon", TextStudio2DMaterialResponseKind.Neon, "AppearanceNeonCyan"),
            Material("material.ice", "Ice", TextStudio2DMaterialResponseKind.Ice, "AppearanceIceCrystal"),
            Material("material.glass", "Glass / Refraction", TextStudio2DMaterialResponseKind.Glass, "AppearanceGlassPrism"),
            Material("material.holographic", "Holographic / Prismatic Foil", TextStudio2DMaterialResponseKind.HolographicFoil, "AppearanceHolographicFoil"),

            Graphic("editorial.halftone", "Halftone", TextStudio2DGraphicEffectKind.Halftone, "AppearancePrintHalftone"),
            Graphic("editorial.grain", "Grain", TextStudio2DGraphicEffectKind.Grain, "AppearanceFineGrain"),
            Graphic("editorial.duotone", "Duotone / Color Map", TextStudio2DGraphicEffectKind.Duotone, "AppearanceSignalDuotone"),
            Graphic("editorial.distress", "Distress / Erosion", TextStudio2DGraphicEffectKind.Distress, "AppearanceWeatheredDistress"),
            Graphic("editorial.ink-bleed", "Ink Bleed", TextStudio2DGraphicEffectKind.InkBleed, "AppearanceInkBleed"),
            Graphic("editorial.letterpress", "Letterpress", TextStudio2DGraphicEffectKind.Letterpress, "AppearanceDeepLetterpress"),
            Graphic("editorial.stencil", "Stencil / Cutout", TextStudio2DGraphicEffectKind.StencilCutout, "AppearanceStencilCutout"),
            Graphic("editorial.sticker", "Sticker / Offset Plate", TextStudio2DGraphicEffectKind.StickerPlate, "AppearanceStickerPlate"),
            Graphic("editorial.cmyk", "CMYK Offset", TextStudio2DGraphicEffectKind.CmykOffset, "AppearanceCmykMisregister"),
            Graphic("editorial.rgb", "RGB Channel Separation", TextStudio2DGraphicEffectKind.RgbSeparation, "AppearanceRgbSplit"),
            Graphic("editorial.prism", "Prism / Chromatic Edge", TextStudio2DGraphicEffectKind.PrismaticEdge, "AppearancePrismaticEdge"),
            Graphic("editorial.engraved", "Outline Trace / Engraved Line", TextStudio2DGraphicEffectKind.EngravedLine, "AppearanceEngravedLine"),

            Animated("animated.light-sweep", "Light Sweep", TextStudio2DAnimatedEffectKind.LightSweep, "AppearanceStudioLightSweep", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.shimmer", "Shimmer / Glitter", TextStudio2DAnimatedEffectKind.Shimmer, "AppearanceCelestialShimmer", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.liquid-fill", "Liquid Fill / Meniscus", TextStudio2DAnimatedEffectKind.LiquidFill, "AppearanceLiquidTide", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.burning-edge", "Burning Edge", TextStudio2DAnimatedEffectKind.BurningEdge, "AppearanceEmberEdge", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.electric-outline", "Electric Outline", TextStudio2DAnimatedEffectKind.ElectricOutline, "AppearanceElectricOutline", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.glitch", "Glitch / Chromatic Displacement", TextStudio2DAnimatedEffectKind.GlitchDisplacement, "AppearanceGlitchSignal", TextGalleryRenderCost.HeroComposite),
            Animated("animated.heat-haze", "Heat Haze", TextStudio2DAnimatedEffectKind.HeatHaze, "AppearanceHeatMirage", TextGalleryRenderCost.HeroComposite),
            Animated("animated.liquid-warp", "Liquid Warp", TextStudio2DAnimatedEffectKind.LiquidWarp, "AppearanceLiquidDrift", TextGalleryRenderCost.HeroComposite),
            Animated("animated.pixel-dissolve", "Pixel / Field Dissolve", TextStudio2DAnimatedEffectKind.PixelDissolve, "AppearancePixelDissolve", TextGalleryRenderCost.FusedSingleDraw),
            Animated("animated.echo-trails", "Echo Trails", TextStudio2DAnimatedEffectKind.EchoTrails, "AppearanceEchoSpectrum", TextGalleryRenderCost.HeroComposite),
            Animated("animated.motion-smear", "Motion Smear", TextStudio2DAnimatedEffectKind.MotionSmear, "AppearanceVelocitySmear", TextGalleryRenderCost.HeroComposite),
            Animated("animated.progressive-focus", "Progressive Blur / Focus", TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus, "AppearanceFocusPull", TextGalleryRenderCost.HeroComposite),

            Writing("writing.line-wipe", "Line Wipe", TextStudioWritingMode.InkReveal, "WritingCaptionLineWipe", false),
            Typewriter("writing.word-cascade", "Word Fade", "RevealTypewriterEffects"),
            Typewriter("writing.cluster-cascade", "Letter Fade", "RevealTypewriterEffects"),
            Typewriter("writing.typewriter", "Typewriter", "RevealTypewriterEffects"),
            Writing("writing.ink-reveal", "Generic Ink Reveal", TextStudioWritingMode.InkReveal, "WritingCleanInkReveal", false),
            Writing("writing.outline-trace", "Outline Trace", TextStudioWritingMode.OutlineTrace, "WritingOutlineTrace", true),
            Writing("writing.authored-centerline", "Authored Centerline Writing", TextStudioWritingMode.AuthoredCenterline, "WritingAuthoredSignature", true),
            Writing("writing.calligraphy", "Calligraphy Pressure Writing", TextStudioWritingMode.CalligraphyPressure, "WritingCalligraphyPressure", true),
            Writing("writing.marker", "Marker Writing", TextStudioWritingMode.Marker, "WritingMarkerStroke", true),
            Writing("writing.chalk", "Chalk Writing", TextStudioWritingMode.Chalk, "WritingChalkLettering", true),
            Writing("writing.stroke-to-fill", "Stroke-To-Fill Transition", TextStudioWritingMode.StrokeToFill, "WritingStrokeToFill", true),
            Writing("writing.erase", "Erase / Backwrite", TextStudioWritingMode.InkReveal, "WritingReverseErase", false)
        };

        internal static IReadOnlyList<TextStudioVisualDirectionEntry> Entries =>
            s_entries;

        static TextStudioVisualDirectionEntry Paint(
            string id,
            string title,
            TextStudio2DPaintKind kind,
            string sample)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.CoreAppearance,
                TextStudioVisualDirectionContractKind.Paint,
                (int)kind,
                sample,
                TextGalleryRenderCost.FusedSingleDraw,
                FieldFallback);
        }

        static TextStudioVisualDirectionEntry Layer(
            string id,
            string title,
            TextStudio2DLayerKind kind,
            string sample,
            TextGalleryRenderCost cost)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.CoreAppearance,
                TextStudioVisualDirectionContractKind.Layer,
                (int)kind,
                sample,
                cost,
                FieldFallback);
        }

        static TextStudioVisualDirectionEntry LayerMaterial(
            string id,
            string title,
            TextStudio2DLayerKind kind,
            string sample)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.DimensionalMaterial,
                TextStudioVisualDirectionContractKind.Layer,
                (int)kind,
                sample,
                TextGalleryRenderCost.FusedSingleDraw,
                FieldFallback);
        }

        static TextStudioVisualDirectionEntry Material(
            string id,
            string title,
            TextStudio2DMaterialResponseKind kind,
            string sample)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.DimensionalMaterial,
                TextStudioVisualDirectionContractKind.MaterialResponse,
                (int)kind,
                sample,
                TextGalleryRenderCost.FusedSingleDraw,
                FieldFallback);
        }

        static TextStudioVisualDirectionEntry Graphic(
            string id,
            string title,
            TextStudio2DGraphicEffectKind kind,
            string sample)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.EditorialGraphic,
                TextStudioVisualDirectionContractKind.GraphicEffect,
                (int)kind,
                sample,
                TextGalleryRenderCost.FusedSingleDraw,
                FieldFallback);
        }

        static TextStudioVisualDirectionEntry Animated(
            string id,
            string title,
            TextStudio2DAnimatedEffectKind kind,
            string sample,
            TextGalleryRenderCost cost)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.AnimatedHero,
                TextStudioVisualDirectionContractKind.AnimatedEffect,
                (int)kind,
                sample,
                cost,
                cost == TextGalleryRenderCost.HeroComposite
                    ? HeroFallback
                    : FieldFallback);
        }

        static TextStudioVisualDirectionEntry Writing(
            string id,
            string title,
            TextStudioWritingMode mode,
            string sample,
            bool prepared)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.WritingReveal,
                TextStudioVisualDirectionContractKind.Writing,
                (int)mode,
                sample,
                prepared
                    ? TextGalleryRenderCost.AdditionalGeometry
                    : TextGalleryRenderCost.FusedSingleDraw,
                WritingFallback);
        }

        static TextStudioVisualDirectionEntry Typewriter(
            string id,
            string title,
            string sample)
        {
            return Entry(
                id,
                title,
                TextStudioVisualDirectionFamily.WritingReveal,
                TextStudioVisualDirectionContractKind.Typewriter,
                0,
                sample,
                TextGalleryRenderCost.InPlace,
                "Use the selected built-in or custom Typewriter treatment and preserve cadence, waits, and lifecycle phases.");
        }

        static TextStudioVisualDirectionEntry Entry(
            string id,
            string title,
            TextStudioVisualDirectionFamily family,
            TextStudioVisualDirectionContractKind contract,
            int contractValue,
            string sample,
            TextGalleryRenderCost cost,
            string fallback)
        {
            return new TextStudioVisualDirectionEntry(
                id,
                title,
                family,
                contract,
                contractValue,
                sample,
                cost,
                fallback);
        }
    }
}
