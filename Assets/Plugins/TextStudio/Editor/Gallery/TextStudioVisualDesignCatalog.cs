using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioVisualDesignAssetKind
    {
        Appearance,
        Writing,
        CompleteDesign
    }

    internal enum TextStudioVisualDesignCollection
    {
        Featured,
        CoreAppearance,
        Materials,
        EditorialGraphic,
        AnimatedAppearance,
        Writing,
        CompleteDesign,
        GameTitles,
        CinematicTitles
    }

    internal readonly struct TextStudioVisualDesignEntry
    {
        internal string Id { get; }
        internal string Title { get; }
        internal string Description { get; }
        internal string SampleText { get; }
        internal string AssetRelativePath { get; }
        internal string PreviewFileName { get; }
        internal TextStudioVisualDesignAssetKind Kind { get; }
        internal TextStudioVisualDesignCollection Collection { get; }
        internal TextGalleryProductionMetadata ProductionMetadata { get; }
        internal string[] SearchKeywords { get; }

        internal TextStudioVisualDesignEntry(
            string id,
            string title,
            string description,
            string sampleText,
            string assetRelativePath,
            string previewFileName,
            TextStudioVisualDesignAssetKind kind,
            TextStudioVisualDesignCollection collection,
            TextGalleryProductionMetadata productionMetadata,
            params string[] searchKeywords)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            SampleText = sampleText ?? string.Empty;
            AssetRelativePath = assetRelativePath ?? string.Empty;
            PreviewFileName = previewFileName ?? string.Empty;
            Kind = kind;
            Collection = collection;
            ProductionMetadata = productionMetadata;
            SearchKeywords = searchKeywords ?? Array.Empty<string>();
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Authored design inventories stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioVisualDesignCatalog
    {
        internal const string PresetRoot =
            "Runtime/Presets/Visual Design";
        internal const string WritingDisplayFontRelativePath =
            "Runtime/Fonts/Writing/Kalam/Kalam Writing SDF.asset";

        static readonly TextStudioVisualDesignEntry[] s_featuredAppearance =
        {
            StaticAppearance("AccessibleSubtitle", "Accessible Subtitle", "High-contrast white lettering with a dark fused edge for subtitles, captions, and dialogue over unpredictable backgrounds.", "Readable in every scene", "Accessible Subtitle.asset", "2d-accessible-subtitle.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "subtitle", "caption", "dialogue", "accessibility"),
            StaticAppearance("CleanEditorial", "Clean Editorial", "Restrained cool gradient, precise edge, and soft depth for editorial cards, product UI, and refined lower thirds.", "THE EDIT", "Clean Editorial.asset", "2d-clean-editorial.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "editorial", "lower third", "clean", "minimal"),
            StaticAppearance("CinematicGold", "Cinematic Gold", "Warm directional gradient, brushed foil detail, controlled shadow, and calibrated gold response for premium titles.", "GOLDEN HOUR", "Cinematic Gold.asset", "2d-cinematic-gold.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.BuiltInAssets | TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "gold", "foil", "cinematic", "title"),
            StaticAppearance("NeonCyan", "Neon Cyan", "Crisp dark core, cyan edge, and bounded emissive glow for readable neon signage without a separate bloom stack.", "OPEN LATE", "Neon Cyan.asset", "2d-neon-cyan.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "neon", "cyan", "sign", "glow"),
            StaticAppearance("HolographicFoil", "Holographic Foil", "Iridescent surface response with a seamless micro-speckle resource for rewards, rarity labels, and futuristic branding.", "PRISMATIC", "Holographic Foil.asset", "2d-holographic-foil.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.BuiltInAssets | TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "holographic", "iridescent", "reward", "rarity"),
            StaticAppearance("IceCrystal", "Ice Crystal", "Branching frost structure, cold internal light, crystalline edge hierarchy, and calibrated ice response for fantasy titles and status effects.", "FROZEN", "Ice Crystal.asset", "2d-ice-crystal.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.BuiltInAssets | TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "ice", "fantasy", "frozen", "crystal"),
            StaticAppearance("BubbleGel", "Bubble Gel", "Soft candy gradient, glossy gel relief, and a compact shadow for playful games and expressive UI.", "SWEET!", "Bubble Gel.asset", "2d-bubble-gel.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "bubble", "gel", "playful", "candy"),
            StaticAppearance("ChromeTitle", "Chrome Title", "Dense silver reflection bands, calibrated chrome response, controlled red rim, and fused broadcast depth for automotive, sci-fi, and title design.", "CHROME", "Chrome Title.asset", "2d-chrome-title.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "chrome", "metal", "broadcast", "automotive"),
            StaticAppearance("RetroPoster", "Retro Poster", "Horizontal sunset inks, fine procedural print grain, layered color registration, and one retained long-shadow batch for poster lettering and vintage title cards.", "SUMMER '86", "Retro Poster.asset", "2d-retro-poster.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.AdditionalGeometry, "retro", "poster", "vintage", "sunset"),
            StaticAppearance("ComicImpact", "Comic Impact", "Tuned halftone ink, a nested black-and-white sticker edge, magenta offset plate, and grounded panel shadow for combat callouts and comic captions.", "BOOM!", "Comic Impact.asset", "2d-comic-impact.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "comic", "combat", "impact", "reward"),
            StaticAppearance("ScifiHud", "Sci-Fi HUD", "A crisp cyan dot-matrix face, technical edge hierarchy, bounded signal glow, and one batched retained panel for polished HUD labels and status readouts.", "SYSTEM READY", "Sci-Fi HUD.asset", "2d-scifi-hud.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.AdditionalGeometry, "sci-fi", "hud", "status", "technical"),
            StaticAppearance("MobileUi", "Mobile UI", "A two-layer, single-draw recipe with strong small-size readability and no texture dependency for dense mobile interfaces.", "LEVEL 24", "Mobile UI.asset", "2d-mobile-ui.png", TextStudioVisualDesignCollection.Featured, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "mobile", "efficient", "ui", "small text")
        };

        static readonly TextStudioVisualDesignEntry[] s_coreAppearance =
        {
            StaticAppearance("AuroraRadial", "Aurora Radial", "A luminous radial color field mapped across the full text block for premium identity cards and title lockups.", "AURORA", "Aurora Radial.asset", "2d-aurora-radial.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "radial", "gradient", "color field", "core"),
            StaticAppearance("SpectrumAngle", "Spectrum Angle", "A controlled conic spectrum with a dark precision edge for energetic branding and rarity typography.", "SPECTRUM", "Spectrum Angle.asset", "2d-spectrum-angle.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "conic", "angle gradient", "spectrum", "core"),
            StaticAppearance("LayeredOutline", "Layered Outline", "Three concentric fused strokes create a crisp broadcast outline without duplicating text objects or materials.", "ON AIR", "Layered Outline.asset", "2d-layered-outline.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "multiple strokes", "concentric", "outline", "broadcast"),
            StaticAppearance("EditorialMultiShadow", "Editorial Multi-Shadow", "A compact stack of colored offset shadows gives editorial depth while remaining in one compatible field draw.", "OFFSET", "Editorial Multi-Shadow.asset", "2d-editorial-multishadow.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "multi shadow", "offset", "editorial", "depth"),
            StaticAppearance("InnerLight", "Inner Light", "Paired inner shadow and inner glow shape the face without external bloom or supporting geometry.", "INNER LIGHT", "Inner Light.asset", "2d-inner-light.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "inner shadow", "inner glow", "relief", "core"),
            StaticAppearance("HighlightPlate", "Highlight Plate", "A retained rounded plate, precise underline, and readable face form a polished label treatment with an honestly declared supporting-geometry cost.", "FEATURED", "Highlight Plate.asset", "2d-highlight-plate.png", TextStudioVisualDesignCollection.CoreAppearance, TextGalleryResourceRequirements.None, TextGalleryRenderCost.AdditionalGeometry, "background", "plate", "underline", "decoration")
        };

        static readonly TextStudioVisualDesignEntry[] s_materialAppearance =
        {
            StaticAppearance("FacetedBevel", "Faceted Bevel", "A clean face gradient and explicit bevel produce a dimensional title that remains on the fused field route.", "FACET", "Faceted Bevel.asset", "2d-faceted-bevel.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "bevel", "faceted", "dimensional", "material"),
            StaticAppearance("SatinRose", "Satin Rose", "Directional satin highlights and a restrained rose gradient create a soft luxury finish for beauty, fashion, and editorial work.", "SATIN", "Satin Rose.asset", "2d-satin-rose.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "satin", "rose", "fashion", "luxury"),
            StaticAppearance("GlassPrism", "Glass Prism", "A translucent cool gradient, bright internal edge, and calibrated glass response provide a clean optical title treatment.", "REFRACT", "Glass Prism.asset", "2d-glass-prism.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "glass", "refraction", "optical", "material"),
            StaticAppearance("BrushedTitanium", "Brushed Titanium", "Seamless directional metal grain, cool edge bands, and anisotropic response create a premium industrial title.", "TITANIUM", "Brushed Titanium.asset", "2d-brushed-titanium.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "titanium", "brushed metal", "industrial", "anisotropic"),
            StaticAppearance("CarbonFiber", "Carbon Fiber", "A compact woven carbon pattern with a polished dark edge targets automotive, racing, and equipment interfaces.", "CARBON", "Carbon Fiber.asset", "2d-carbon-fiber.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "carbon fiber", "racing", "automotive", "woven"),
            StaticAppearance("MotherOfPearl", "Mother of Pearl", "Soft nacre color shifts and a pearlescent surface response create an elegant collectible and beauty-title finish.", "NACRE", "Mother of Pearl.asset", "2d-mother-of-pearl.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "mother of pearl", "nacre", "pearlescent", "luxury"),
            StaticAppearance("VelvetFlock", "Velvet Flock", "Fine directional nap and deep soft shading create a tactile velvet treatment without a secondary material stack.", "VELVET", "Velvet Flock.asset", "2d-velvet-flock.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "velvet", "flock", "fabric", "soft"),
            StaticAppearance("MoltenObsidian", "Molten Obsidian", "Seamless crack detail, ember color, and a dark glassy edge create a static molten-rock title with no persistent animation.", "OBSIDIAN", "Molten Obsidian.asset", "2d-molten-obsidian.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "molten", "obsidian", "lava", "cracks"),
            StaticAppearance("FrostBloom", "Frost Bloom", "Branching frost detail, an icy face gradient, and a controlled aura create a premium frozen-surface treatment.", "WINTER", "Frost Bloom.asset", "2d-frost-bloom.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "frost", "winter", "ice", "crystal"),
            StaticAppearance("GlitterResin", "Glitter Resin", "A bounded sparkle distribution, glossy resin response, and vivid depth provide a polished collectible finish.", "GLITTER", "Glitter Resin.asset", "2d-glitter-resin.png", TextStudioVisualDesignCollection.Materials, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "glitter", "resin", "sparkle", "collectible")
        };

        static readonly TextStudioVisualDesignEntry[] s_editorialAppearance =
        {
            EditorialAppearance("PrintHalftone", "Print Halftone", "A tuned dot screen and warm ink palette for comic panels, posters, and graphic title cards.", "HALFTONE", "Print Halftone.asset", "2d-print-halftone.png", TextGalleryResourceRequirements.None, "halftone", "dots", "print", "comic"),
            EditorialAppearance("FineGrain", "Fine Grain", "Fine paper fiber and deterministic grain add tactile print character without a large source texture.", "GRAIN", "Fine Grain.asset", "2d-fine-grain.png", TextGalleryResourceRequirements.Texture, "grain", "paper", "fiber", "editorial"),
            EditorialAppearance("SignalDuotone", "Signal Duotone", "A high-contrast two-color map designed for music, broadcast, and contemporary editorial graphics.", "DUOTONE", "Signal Duotone.asset", "2d-signal-duotone.png", TextGalleryResourceRequirements.None, "duotone", "color map", "broadcast", "music"),
            EditorialAppearance("WeatheredDistress", "Weathered Distress", "Scale-aware seeded erosion creates an honest broken white print face with restrained edge and grounding while preserving display-size legibility.", "WEATHERED", "Weathered Distress.asset", "2d-weathered-distress.png", TextGalleryResourceRequirements.None, "distress", "erosion", "weathered", "grunge"),
            EditorialAppearance("InkBleed", "Ink Bleed", "Soft edge spread and dark print color mimic ink absorption for packaging, zines, and analog title work.", "INK BLEED", "Ink Bleed.asset", "2d-ink-bleed.png", TextGalleryResourceRequirements.Texture, "ink bleed", "print", "analog", "zine"),
            EditorialAppearance("DeepLetterpress", "Deep Letterpress", "Directional highlight and recessed shadow reproduce a premium pressed-paper title in one field route.", "LETTERPRESS", "Deep Letterpress.asset", "2d-deep-letterpress.png", TextGalleryResourceRequirements.None, "letterpress", "pressed", "paper", "editorial"),
            EditorialAppearance("StencilCutout", "Stencil Cutout", "A deliberate internal cutout rhythm gives labels and industrial headings a crisp stencil construction.", "STENCIL", "Stencil Cutout.asset", "2d-stencil-cutout.png", TextGalleryResourceRequirements.None, "stencil", "cutout", "industrial", "label"),
            EditorialAppearance("StickerPlate", "Sticker Plate", "A bold offset plate and clean face create a ready-to-use sticker treatment for rewards and social graphics.", "STICKER", "Sticker Plate.asset", "2d-sticker-plate.png", TextGalleryResourceRequirements.None, "sticker", "offset plate", "reward", "social"),
            EditorialAppearance("CmykMisregister", "CMYK Misregister", "Controlled cyan, magenta, yellow, and black offsets emulate premium screen-print misregistration.", "CMYK", "CMYK Misregister.asset", "2d-cmyk-misregister.png", TextGalleryResourceRequirements.None, "cmyk", "misregister", "screen print", "offset"),
            EditorialAppearance("RgbSplit", "RGB Split", "A restrained channel separation adds digital tension while keeping the center face readable.", "RGB SPLIT", "RGB Split.asset", "2d-rgb-split.png", TextGalleryResourceRequirements.None, "rgb", "channel separation", "digital", "chromatic"),
            EditorialAppearance("PrismaticEdge", "Prismatic Edge", "A microprism face and spectral contour create a sharp chromatic-edge treatment for rarity and identity graphics.", "PRISM EDGE", "Prismatic Edge.asset", "2d-prismatic-edge.png", TextGalleryResourceRequirements.Texture, "prismatic", "chromatic edge", "microprism", "rarity"),
            EditorialAppearance("EngravedLine", "Engraved Line", "Cross-etched internal line rhythm, a luminous cyan contour, deep face, and restrained energy aura produce an engraved technical title for premium labels and maps.", "ENGRAVED", "Engraved Line.asset", "2d-engraved-line.png", TextGalleryResourceRequirements.None, "engraved", "outline trace", "technical", "line")
        };

        static readonly TextStudioVisualDesignEntry[] s_animatedAppearance =
        {
            AnimatedAppearance("StudioLightSweep", "Studio Light Sweep", "A restrained studio highlight travels across the whole object as persistent Appearance, ideal for logos and hero titles.", "LIGHT SWEEP", "Studio Light Sweep.asset", "2d-studio-light-sweep.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Reduced, "light sweep", "persistent", "logo", "highlight"),
            AnimatedAppearance("CelestialShimmer", "Celestial Shimmer", "Deterministic fine shimmer adds premium motion to rewards and rarity text while staying on the fused route.", "SHIMMER", "Celestial Shimmer.asset", "2d-celestial-shimmer.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Reduced, "shimmer", "glitter", "rarity", "reward"),
            AnimatedAppearance("LiquidTide", "Liquid Tide", "A text-wide liquid meniscus rises through the face with a readable static reduced-motion result.", "HIGH TIDE", "Liquid Tide.asset", "2d-liquid-tide.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Reduced, "liquid fill", "meniscus", "water", "persistent"),
            AnimatedAppearance("EmberEdge", "Ember Edge", "A bounded burning contour animates around a dark readable face for fantasy alerts and dramatic titles.", "IGNITE", "Ember Edge.asset", "2d-ember-edge.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Reduced, "burning edge", "ember", "fire", "fantasy"),
            AnimatedAppearance("ElectricOutline", "Electric Outline", "A procedural electric contour energizes sci-fi labels and power-up text without a separate particle system.", "CHARGED", "Electric Outline.asset", "2d-electric-outline.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Reduced, "electric", "outline", "charged", "sci-fi"),
            AnimatedAppearance("PixelDissolve", "Pixel Dissolve", "A field-driven pixel threshold cycles across the whole object for holograms, transitions, and digital signage.", "DIGITIZE", "Pixel Dissolve.asset", "2d-pixel-dissolve.png", TextGalleryRenderCost.FusedSingleDraw, TextGalleryReducedMotionSupport.Disabled, "pixel dissolve", "field", "digital", "hologram"),
            AnimatedAppearance("GlitchSignal", "Glitch Signal", "A hero-route chromatic displacement treatment for featured signal loss and cyberpunk title moments.", "NO SIGNAL", "Glitch Signal.asset", "2d-glitch-signal.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Disabled, "glitch", "chromatic displacement", "hero", "cyberpunk"),
            AnimatedAppearance("HeatMirage", "Heat Mirage", "A bounded spatial heat distortion gives featured fire and desert titles a premium refractive shimmer.", "MIRAGE", "Heat Mirage.asset", "2d-heat-mirage.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Reduced, "heat haze", "mirage", "distortion", "hero"),
            AnimatedAppearance("LiquidDrift", "Liquid Drift", "Smooth field warping creates a floating liquid face for music, fantasy, and abstract hero titles.", "DRIFT", "Liquid Drift.asset", "2d-liquid-drift.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Reduced, "liquid warp", "drift", "music", "hero"),
            AnimatedAppearance("EchoSpectrum", "Echo Spectrum", "A fixed four-frame history ring produces bounded spectral echoes for featured motion graphics.", "ECHO", "Echo Spectrum.asset", "2d-echo-spectrum.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Reduced, "echo trails", "history", "spectrum", "hero"),
            AnimatedAppearance("VelocitySmear", "Velocity Smear", "A phase-driven shutter envelope creates directional speed trails without rebuilding glyph geometry.", "VELOCITY", "Velocity Smear.asset", "2d-velocity-smear.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Reduced, "motion smear", "velocity", "speed", "hero", "shutter"),
            AnimatedAppearance("FocusPull", "Focus Pull", "A pooled progressive blur resolves into a crisp title for cinematic focus transitions and reveals.", "IN FOCUS", "Focus Pull.asset", "2d-focus-pull.png", TextGalleryRenderCost.HeroComposite, TextGalleryReducedMotionSupport.Reduced, "progressive blur", "focus", "cinematic", "hero")
        };

        static readonly TextStudioVisualDesignEntry[] s_appearance = Combine(
            s_featuredAppearance,
            s_coreAppearance,
            s_materialAppearance,
            s_editorialAppearance,
            s_animatedAppearance,
            CreateTitleAppearances());

        static TextStudioVisualDesignEntry[] CreateTitleAppearances()
        {
            return new[]
            {
                StaticAppearance("VictoryGold", "Victory Gold", "Sculpted golden enamel, a clean ink outline, and compact amber depth for victory screens, ranks, and tournament rewards.", "VICTORY!", "Victory Gold.asset", "2d-victory-gold.png", TextStudioVisualDesignCollection.GameTitles, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "victory", "win", "rank", "championship", "game title"),
                StaticAppearance("LegendaryRelic", "Legendary Relic", "Brushed precious gold, a luminous jewelry edge, and violet relief for legendary loot, achievements, and fantasy rewards.", "LEGENDARY", "Legendary Relic.asset", "2d-legendary-relic.png", TextStudioVisualDesignCollection.GameTitles, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "legendary", "loot", "rarity", "achievement", "rpg"),
                StaticAppearance("ArcadeChampion", "Arcade Champion", "Polished sunset reflection bands, a bright magenta silhouette, and indigo depth for arcade scores, racing games, and synthwave titles.", "HIGH SCORE", "Arcade Champion.asset", "2d-arcade-champion.png", TextStudioVisualDesignCollection.GameTitles, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "arcade", "high score", "racing", "synthwave", "retro game"),
                StaticAppearance("ToxicWarning", "Toxic Warning", "Acid-green lacquer, fine mineral fractures, and a dark protective outline for radioactive hazards, poison status, and mutant bosses.", "BIOHAZARD", "Toxic Warning.asset", "2d-toxic-warning.png", TextStudioVisualDesignCollection.GameTitles, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "toxic", "poison", "hazard", "mutant", "boss"),
                StaticAppearance("EpicSteel", "Epic Steel", "Weathered gunmetal with machined highlights, a restrained copper rim, and shaded steel depth for action trailers and military titles.", "VANGUARD", "Epic Steel.asset", "2d-epic-steel.png", TextStudioVisualDesignCollection.CinematicTitles, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "action", "trailer", "steel", "military", "movie"),
                StaticAppearance("AncientRelic", "Ancient Relic", "Fine limestone pores, carved warm bevels, and aged stone depth for archaeological adventures, fantasy chapters, and historical titles.", "ANCIENT", "Ancient Relic.asset", "2d-ancient-relic.png", TextStudioVisualDesignCollection.CinematicTitles, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "ancient", "stone", "adventure", "history", "fantasy"),
                StaticAppearance("CrimsonHorror", "Crimson Horror", "Scarred crimson lacquer, a sharp wet edge, and near-black depth create a restrained horror and thriller title without sacrificing the letter silhouette.", "DREAD", "Crimson Horror.asset", "2d-crimson-horror.png", TextStudioVisualDesignCollection.CinematicTitles, TextGalleryResourceRequirements.Texture, TextGalleryRenderCost.FusedSingleDraw, "horror", "thriller", "crimson", "survival", "movie"),
                StaticAppearance("SilverScreen", "Silver Screen", "Restrained platinum reflections, finely cut bevels, and shallow charcoal depth for classic cinema, opening credits, and elegant premieres.", "PREMIERE", "Silver Screen.asset", "2d-silver-screen.png", TextStudioVisualDesignCollection.CinematicTitles, TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "cinema", "credits", "platinum", "premiere", "classic")
            };
        }

        static readonly TextStudioVisualDesignEntry[] s_writing =
        {
            CreateWritingEntry("CleanInkReveal", "Clean Ink Reveal", "A shaped-cluster ink reveal that remains readable across scripts and ordinary TMP fonts.", "Signed in ink", "Clean Ink Reveal.asset", "writing-clean-ink.png", TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "ink", "shaped cluster", "handwriting"),
            CreateWritingEntry("CaptionLineWipe", "Caption Line Wipe", "A low-motion line-level reveal for captions, lower thirds, and information panels.", "A clear line\narrives", "Caption Line Wipe.asset", "writing-caption-line.png", TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "caption", "line", "low motion"),
            CreateWritingEntry("ReverseErase", "Reverse Erase", "A reversible backwrite treatment for corrections, rewinds, and intentional text removal.", "REWIND", "Reverse Erase.asset", "writing-reverse-erase.png", TextGalleryResourceRequirements.None, TextGalleryRenderCost.FusedSingleDraw, "erase", "reverse", "rewind"),
            PreparedWriting("OutlineTrace", "Outline Trace", "A true contour trace backed by the included exact phrase outline. Other text keeps the deterministic prepared-outline to ink fallback ladder.", "TRACE", "Outline Trace.asset", "writing-outline-trace.png", "outline", "trace", "prepared path", "contour"),
            PreparedWriting("AuthoredSignature", "Authored Signature", "Reviewed centerline strokes, explicit pen lifts, variable pressure, and a finishing flourish demonstrate true phrase-specific handwriting.", "SIGNED", "Authored Signature.asset", "writing-authored-signature.png", "centerline", "signature", "pen lift", "true handwriting"),
            PreparedWriting("CalligraphyPressure", "Calligraphy Pressure", "An included broad-nib phrase uses authored centerlines, direction-aware pressure, crisp joins, and a controlled fill handoff.", "FLOW", "Calligraphy Pressure.asset", "writing-calligraphy-pressure.png", "calligraphy", "pressure", "broad nib", "prepared path"),
            PreparedWriting("MarkerStroke", "Marker Stroke", "An included authored phrase combines rounded high-coverage ribbons with an analytical marker profile and no extra texture or draw.", "MARKER", "Marker Stroke.asset", "writing-marker-stroke.png", "marker", "rounded", "annotation", "prepared path"),
            PreparedWriting("ChalkLettering", "Chalk Lettering", "An included authored phrase uses deterministic multiscale grain and rough ribbon edges in the shared Writing draw, with honest ink fallback for unmatched text.", "CHALK", "Chalk Lettering.asset", "writing-chalk-lettering.png", "chalk", "lettering", "texture", "prepared path"),
            PreparedWriting("StrokeToFill", "Stroke to Fill", "Reviewed phrase centerlines transition cleanly into the shaped source glyphs for logos, signatures, and title reveals.", "STROKE TO FILL", "Stroke to Fill.asset", "writing-stroke-to-fill.png", "stroke to fill", "handoff", "logo", "prepared path")
        };

        static readonly TextStudioVisualDesignEntry[] s_completeDesigns =
        {
            Design("ReadableDialogue", "Readable Dialogue", "Accessible subtitle appearance with the established smooth dialogue Typewriter preset and no persistent motion.", "The signal is clear.", "Readable Dialogue.asset", "design-readable-dialogue.png", TextGalleryResourceRequirements.BuiltInAssets, TextGalleryReducedMotionSupport.Reduced, "dialogue", "subtitle", "accessible"),
            Design("CinematicGoldReveal", "Cinematic Gold Reveal", "Gold foil appearance, cinematic line arrival, and a restrained fallback surface sweep for a complete title recipe.", "A NEW ERA", "Cinematic Gold Reveal.asset", "design-cinematic-gold.png", TextGalleryResourceRequirements.BuiltInAssets | TextGalleryResourceRequirements.Texture, TextGalleryReducedMotionSupport.Reduced, "cinematic", "gold", "title", "surface sweep"),
            Design("NeonReward", "Neon Reward", "Neon appearance with a punchy UI arrival and gentle uncovered-text pulse for premium rewards and unlocks.", "UNLOCKED", "Neon Reward.asset", "design-neon-reward.png", TextGalleryResourceRequirements.BuiltInAssets, TextGalleryReducedMotionSupport.Reduced, "reward", "unlock", "neon"),
            Design("HolographicSignal", "Holographic Signal", "Holographic surface, terminal cadence, and restrained uncovered-text glitch for a futuristic signal treatment.", "SIGNAL FOUND", "Holographic Signal.asset", "design-holographic-signal.png", TextGalleryResourceRequirements.BuiltInAssets | TextGalleryResourceRequirements.Texture, TextGalleryReducedMotionSupport.Reduced, "holographic", "signal", "terminal"),
            Design("EditorialInk", "Editorial Ink", "Clean editorial appearance paired with cluster-aware ink writing and a deliberate cinematic reveal schedule.", "Field Notes", "Editorial Ink.asset", "design-editorial-ink.png", TextGalleryResourceRequirements.BuiltInAssets, TextGalleryReducedMotionSupport.Reduced, "editorial", "ink", "writing"),
            Design("AccessibleInstant", "Accessible Instant", "High-contrast styling with instant text, no transition effects, and no persistent animation for accessibility-first UI.", "OBJECTIVE UPDATED", "Accessible Instant.asset", "design-accessible-instant.png", TextGalleryResourceRequirements.BuiltInAssets, TextGalleryReducedMotionSupport.MotionFree, "accessible", "instant", "low motion", "objective")
        };

        internal static IReadOnlyList<TextStudioVisualDesignEntry> Appearance =>
            s_appearance;
        internal static IReadOnlyList<TextStudioVisualDesignEntry> Writing =>
            s_writing;
        internal static IReadOnlyList<TextStudioVisualDesignEntry> CompleteDesigns =>
            s_completeDesigns;

        internal static T LoadAsset<T>(in TextStudioVisualDesignEntry entry)
            where T : UnityEngine.Object
        {
            string root = ResolveProjectRoot();
            if (!string.IsNullOrEmpty(root))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    root + "/" + entry.AssetRelativePath);
                if (asset != null)
                    return asset;
            }

            string name = Path.GetFileNameWithoutExtension(
                entry.AssetRelativePath);
            string[] guids = AssetDatabase.FindAssets(
                name + " t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i])
                    .Replace('\\', '/');
                if (path.EndsWith(
                        "/" + entry.AssetRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<T>(path);
                }
            }
            return null;
        }

        internal static TMP_FontAsset LoadWritingDisplayFont()
        {
            string projectRoot = TextStudioPackagePaths.FindAssetRoot(
                TextStudioPackagePaths.BasePackageName);
            if (!string.IsNullOrEmpty(projectRoot))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<
                    TMP_FontAsset>(
                    projectRoot.TrimEnd('/') + "/" +
                    WritingDisplayFontRelativePath);
                if (font != null)
                    return font;
            }

            string[] guids = AssetDatabase.FindAssets(
                "Kalam Writing SDF t:TMP_FontAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i])
                    .Replace('\\', '/');
                if (path.EndsWith(
                        "/" + WritingDisplayFontRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                }
            }
            return null;
        }

        internal static string ResolveProjectRoot()
        {
            string projectRoot = TextStudioPackagePaths.FindAssetRoot(
                TextStudioPackagePaths.BasePackageName);
            return string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : projectRoot.TrimEnd('/') + "/" + PresetRoot;
        }

        static TextStudioVisualDesignEntry StaticAppearance(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextStudioVisualDesignCollection collection,
            TextGalleryResourceRequirements resources,
            TextGalleryRenderCost renderCost,
            params string[] searchKeywords)
        {
            return CreateAppearanceEntry(
                id,
                title,
                description,
                sampleText,
                fileName,
                previewFileName,
                collection,
                renderCost,
                resources,
                TextGalleryReducedMotionSupport.MotionFree,
                searchKeywords);
        }

        static TextStudioVisualDesignEntry EditorialAppearance(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextGalleryResourceRequirements resources,
            params string[] searchKeywords)
        {
            return StaticAppearance(
                id,
                title,
                description,
                sampleText,
                fileName,
                previewFileName,
                TextStudioVisualDesignCollection.EditorialGraphic,
                resources,
                TextGalleryRenderCost.FusedSingleDraw,
                searchKeywords);
        }

        static TextStudioVisualDesignEntry AnimatedAppearance(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextGalleryRenderCost renderCost,
            TextGalleryReducedMotionSupport reducedMotion,
            params string[] searchKeywords)
        {
            return CreateAppearanceEntry(
                id,
                title,
                description,
                sampleText,
                fileName,
                previewFileName,
                TextStudioVisualDesignCollection.AnimatedAppearance,
                renderCost,
                TextGalleryResourceRequirements.None,
                reducedMotion,
                searchKeywords);
        }

        static TextStudioVisualDesignEntry CreateAppearanceEntry(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextStudioVisualDesignCollection collection,
            TextGalleryRenderCost renderCost,
            TextGalleryResourceRequirements resources,
            TextGalleryReducedMotionSupport reducedMotion,
            params string[] searchKeywords)
        {
            return new TextStudioVisualDesignEntry(
                "Appearance" + id,
                title,
                description,
                sampleText,
                "Appearance/" + fileName,
                previewFileName,
                TextStudioVisualDesignAssetKind.Appearance,
                collection,
                new TextGalleryProductionMetadata(
                    TextGalleryWorkflow.Appearance,
                    TextGalleryRendererSupport.TextMeshPro,
                    renderCost,
                    resources,
                    reducedMotion),
                searchKeywords);
        }

        static TextStudioVisualDesignEntry CreateWritingEntry(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextGalleryResourceRequirements resources,
            TextGalleryRenderCost renderCost,
            params string[] searchKeywords)
        {
            return new TextStudioVisualDesignEntry(
                "Writing" + id,
                title,
                description,
                sampleText,
                "Writing/" + fileName,
                previewFileName,
                TextStudioVisualDesignAssetKind.Writing,
                TextStudioVisualDesignCollection.Writing,
                new TextGalleryProductionMetadata(
                    TextGalleryWorkflow.Writing,
                    TextGalleryRendererSupport.TextMeshPro,
                    renderCost,
                    resources,
                    TextGalleryReducedMotionSupport.Reduced),
                searchKeywords);
        }

        static TextStudioVisualDesignEntry PreparedWriting(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            params string[] searchKeywords)
        {
            return CreateWritingEntry(
                id,
                title,
                description,
                sampleText,
                fileName,
                previewFileName,
                TextGalleryResourceRequirements.PreparedWritingData |
                TextGalleryResourceRequirements.BuiltInAssets,
                TextGalleryRenderCost.AdditionalGeometry,
                searchKeywords);
        }

        static TextStudioVisualDesignEntry Design(
            string id,
            string title,
            string description,
            string sampleText,
            string fileName,
            string previewFileName,
            TextGalleryResourceRequirements resources,
            TextGalleryReducedMotionSupport reducedMotion,
            params string[] searchKeywords)
        {
            return new TextStudioVisualDesignEntry(
                "Design" + id,
                title,
                description,
                sampleText,
                "Designs/" + fileName,
                previewFileName,
                TextStudioVisualDesignAssetKind.CompleteDesign,
                TextStudioVisualDesignCollection.CompleteDesign,
                new TextGalleryProductionMetadata(
                    TextGalleryWorkflow.CompleteDesign,
                    TextGalleryRendererSupport.TextMeshPro,
                    TextGalleryRenderCost.FusedSingleDraw,
                    resources,
                    reducedMotion),
                searchKeywords);
        }

        static TextStudioVisualDesignEntry[] Combine(
            params TextStudioVisualDesignEntry[][] groups)
        {
            int count = 0;
            for (int i = 0; i < groups.Length; i++)
                count += groups[i]?.Length ?? 0;

            TextStudioVisualDesignEntry[] combined =
                new TextStudioVisualDesignEntry[count];
            int destination = 0;
            for (int groupIndex = 0;
                 groupIndex < groups.Length;
                 groupIndex++)
            {
                TextStudioVisualDesignEntry[] group = groups[groupIndex];
                if (group == null || group.Length == 0)
                    continue;
                Array.Copy(group, 0, combined, destination, group.Length);
                destination += group.Length;
            }
            return combined;
        }
    }
}
