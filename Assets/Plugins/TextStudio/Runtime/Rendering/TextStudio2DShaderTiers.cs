using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    /// <summary>Program features compiled into a field material independently of its tier.</summary>
    [System.Flags]
    internal enum TextStudio2DShaderOptions : byte
    {
        None = 0,
        // Material-tier effects (pixel grid, squiggle, outline glow, chromatic split, sweep, dissolve, burn).
        MaterialEffects = 1 << 0,
        // Every paint of the program is solid, so paint evaluation compiles to the operation colour.
        SolidPaint = 1 << 1
    }

    internal enum TextStudio2DShaderTier : byte
    {
        Full = 0,
        Basic = 1,
        // One isolated operation per primitive, compiled per heavy family; ordered primitives and Hero rows use them only for
        // that family's rows, so fused runs and light rows keep lean programs.
        IsolatedMaterial = 2,
        IsolatedGraphic = 3,
        IsolatedAnimated = 4,
        IsolatedDepth = 5
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DShaderTiers
    {
        internal const string BasicKeyword = "TEXT_STUDIO_APPEARANCE_BASIC";
        static readonly string[] FamilyKeywords =
        {
            "TEXT_STUDIO_FAMILY_MATERIAL", "TEXT_STUDIO_FAMILY_GRAPHIC", "TEXT_STUDIO_FAMILY_ANIMATED", "TEXT_STUDIO_FAMILY_DEPTH"
        };
        internal const string MaterialEffectsKeyword = "TEXT_STUDIO_MATERIAL_EFFECTS";
        internal const string SolidPaintKeyword = "TEXT_STUDIO_SOLID_PAINT";
        const TextStudio2DShaderFeatures BasicFeatures =
            TextStudio2DShaderFeatures.SolidPaint |
            TextStudio2DShaderFeatures.GradientPaint |
            TextStudio2DShaderFeatures.Stroke |
            TextStudio2DShaderFeatures.Selector |
            TextStudio2DShaderFeatures.WorldShadowCaster;
        const TextStudio2DShaderFeatures PaintFeatures =
            TextStudio2DShaderFeatures.SolidPaint |
            TextStudio2DShaderFeatures.GradientPaint;

        // Keep the experiment off until matched rendering and resource measurements justify adoption.
        static bool s_basicPrototypeEnabled = false;
        internal static bool BasicPrototypeEnabled
        {
            get => s_basicPrototypeEnabled;
            set => s_basicPrototypeEnabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetPrototype()
        {
            s_basicPrototypeEnabled = false;
        }

        internal static TextStudio2DShaderTier Select(CompiledTextStudio2DStyle style,
            CompiledTextStudioAppearancePlan plan = null)
        {
            if (!s_basicPrototypeEnabled) return TextStudio2DShaderTier.Full;
            if (plan == null || !plan.HasInvocations) return Classify(style);
            if (plan.Programs.Count == 0) return TextStudio2DShaderTier.Full;
            for (int index = 0; index < plan.Programs.Count; index++)
                if (Classify(plan.Programs[index]) != TextStudio2DShaderTier.Basic)
                    return TextStudio2DShaderTier.Full;
            return TextStudio2DShaderTier.Basic;
        }

        /// <summary>Selects the program options of a text: material effects when its plan uses one, solid paint when every program paint is solid.</summary>
        internal static TextStudio2DShaderOptions SelectOptions(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan,
            bool materialEffects)
        {
            TextStudio2DShaderOptions options = materialEffects ? TextStudio2DShaderOptions.MaterialEffects : TextStudio2DShaderOptions.None;
            bool solid;
            if (plan == null || !plan.HasInvocations)
                solid = UsesOnlySolidPaint(style);
            else
            {
                solid = plan.Programs.Count > 0;
                for (int index = 0; index < plan.Programs.Count && solid; index++)
                    solid = UsesOnlySolidPaint(plan.Programs[index]);
            }

            return solid ? options | TextStudio2DShaderOptions.SolidPaint : options;
        }

        internal static bool UsesOnlySolidPaint(CompiledTextStudio2DStyle style)
        {
            if (style == null || !style.IsValid || style.OperationCount == 0)
                return false;
            for (int index = 0; index < style.PaintCount; index++)
            {
                if (style.GetPaint(index).Kind != TextStudio2DPaintKind.Solid)
                    return false;
            }

            return true;
        }

        internal static TextStudio2DShaderTier ClassifyFeatures(TextStudio2DShaderFeatures features)
        {
            return (features & ~BasicFeatures) == 0 && (features & PaintFeatures) != 0
                ? TextStudio2DShaderTier.Basic
                : TextStudio2DShaderTier.Full;
        }

        internal static TextStudio2DShaderTier Classify(CompiledTextStudio2DStyle style)
        {
            if (style == null || !style.IsValid || style.IsPassThrough || style.BucketCount == 0)
                return TextStudio2DShaderTier.Full;

            for (int index = 0; index < style.BucketCount; index++)
            {
                TextStudio2DCompiledBucket bucket = style.GetBucket(index);
                // Fused batch keys deliberately omit features. RequiredFeatures retains their complete union.
                if (bucket.BatchKey.ExecutionPath != TextStudio2DExecutionPath.FusedField ||
                    ClassifyFeatures(bucket.RequiredFeatures) != TextStudio2DShaderTier.Basic)
                    return TextStudio2DShaderTier.Full;
            }

            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.Kind != TextStudio2DLayerKind.Fill && operation.Kind != TextStudio2DLayerKind.Stroke)
                    return TextStudio2DShaderTier.Full;
                if (operation.ExecutionPath != TextStudio2DExecutionPath.FusedField ||
                    operation.BlendMode != TextStudio2DBlendMode.Normal ||
                    operation.PaintIndex < 0 || operation.PaintIndex >= style.PaintCount)
                    return TextStudio2DShaderTier.Full;
                if (operation.Kind == TextStudio2DLayerKind.Stroke &&
                    (operation.Stroke.Join != TextStudio2DStrokeJoin.Round || operation.Stroke.DashCount != 0))
                    return TextStudio2DShaderTier.Full;
                if (!IsBasicPaint(style.GetPaint(operation.PaintIndex).Kind))
                    return TextStudio2DShaderTier.Full;
            }
            return TextStudio2DShaderTier.Basic;
        }

        static bool IsBasicPaint(TextStudio2DPaintKind kind)
        {
            switch (kind)
            {
                case TextStudio2DPaintKind.Solid:
                case TextStudio2DPaintKind.LinearGradient:
                case TextStudio2DPaintKind.RadialGradient:
                case TextStudio2DPaintKind.ConicGradient:
                case TextStudio2DPaintKind.FourCornerGradient:
                case TextStudio2DPaintKind.DistanceGradient:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsIsolated(TextStudio2DShaderTier tier) => tier >= TextStudio2DShaderTier.IsolatedMaterial;

        /// <summary>The isolated tier compiling the heavy family of an operation kind, or Full for light kinds.</summary>
        internal static TextStudio2DShaderTier IsolatedTierFor(TextStudio2DLayerKind kind)
        {
            switch (kind)
            {
                case TextStudio2DLayerKind.Bevel:
                case TextStudio2DLayerKind.MaterialResponse:
                    return TextStudio2DShaderTier.IsolatedMaterial;
                case TextStudio2DLayerKind.GraphicEffect:
                    return TextStudio2DShaderTier.IsolatedGraphic;
                case TextStudio2DLayerKind.AnimatedEffect:
                    return TextStudio2DShaderTier.IsolatedAnimated;
                case TextStudio2DLayerKind.FauxDepth:
                    return TextStudio2DShaderTier.IsolatedDepth;
                default:
                    return TextStudio2DShaderTier.Full;
            }
        }

        internal static TextStudio2DShaderTier SupportedTier(Shader shader, TextStudio2DShaderTier tier)
        {
            if (tier == TextStudio2DShaderTier.Full || shader == null)
                return TextStudio2DShaderTier.Full;
            int family = (int)tier - (int)TextStudio2DShaderTier.IsolatedMaterial;
            if (tier != TextStudio2DShaderTier.Basic && (family < 0 || family >= FamilyKeywords.Length))
                return TextStudio2DShaderTier.Full;
            string keyword = tier == TextStudio2DShaderTier.Basic
                ? BasicKeyword
                : FamilyKeywords[family];
            return shader.keywordSpace.FindKeyword(keyword).isValid ? tier : TextStudio2DShaderTier.Full;
        }

        /// <summary>Selects the tier variant; material-tier effect code is compiled only when the text uses such an effect.</summary>
        internal static void Configure(Material material, TextStudio2DShaderTier tier,
            TextStudio2DShaderOptions options = TextStudio2DShaderOptions.MaterialEffects)
        {
            LocalKeyword keyword = material.shader.keywordSpace.FindKeyword(BasicKeyword);
            if (keyword.isValid)
                material.SetKeyword(keyword, tier == TextStudio2DShaderTier.Basic);
            for (int family = 0; family < FamilyKeywords.Length; family++)
            {
                LocalKeyword familyKeyword = material.shader.keywordSpace.FindKeyword(FamilyKeywords[family]);
                if (familyKeyword.isValid)
                    material.SetKeyword(familyKeyword, (int)tier == (int)TextStudio2DShaderTier.IsolatedMaterial + family);
            }
            LocalKeyword effects = material.shader.keywordSpace.FindKeyword(MaterialEffectsKeyword);
            if (effects.isValid)
                material.SetKeyword(effects, (options & TextStudio2DShaderOptions.MaterialEffects) != 0);
            LocalKeyword solid = material.shader.keywordSpace.FindKeyword(SolidPaintKeyword);
            if (solid.isValid)
                material.SetKeyword(solid, (options & TextStudio2DShaderOptions.SolidPaint) != 0);
        }
    }
}
