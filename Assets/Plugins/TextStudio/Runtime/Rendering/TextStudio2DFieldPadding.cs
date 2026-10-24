using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Shared TMP padding buckets for field-rendered appearance. Buckets keep bounds changes
    /// inside a prepared envelope from changing material identity or regenerating text geometry.
    /// </summary>
    internal enum TextStudio2DFieldPaddingTier : byte
    {
        None = 0,
        Compact = 1,
        Standard = 2,
        Wide = 3
    }

    internal static class TextStudio2DFieldPadding
    {
        internal const float CompactCapacity = 0.25f;
        internal const float StandardCapacity = 0.5f;
        internal const float WideCapacity = 1f;
        internal const float MaximumCapacity = WideCapacity;

        internal static bool UsesPixelUnits(in TextStudio2DCompiledOperation operation)
        {
            return operation.Kind switch
            {
                TextStudio2DLayerKind.Fill => operation.Fill.Units == TextStudio2DUnit.Pixels,
                TextStudio2DLayerKind.Stroke => operation.Stroke.Units == TextStudio2DUnit.Pixels,
                TextStudio2DLayerKind.InnerGlow or TextStudio2DLayerKind.OuterGlow => operation.Glow.Units == TextStudio2DUnit.Pixels,
                TextStudio2DLayerKind.InnerShadow or TextStudio2DLayerKind.OuterShadow => operation.Shadow.Units == TextStudio2DUnit.Pixels,
                TextStudio2DLayerKind.Bevel => operation.Bevel.Units == TextStudio2DUnit.Pixels,
                TextStudio2DLayerKind.FauxDepth => operation.FauxDepth.Units == TextStudio2DUnit.Pixels,
                _ => false
            };
        }

        internal static bool TryResolve(
            CompiledTextStudio2DStyle style,
            out TextStudio2DFieldPaddingTier tier)
        {
            if (style == null || style.IsPassThrough)
            {
                tier = TextStudio2DFieldPaddingTier.None;
                return true;
            }

            TextStudio2DBoundsExpansion expansion = style.BoundsExpansion;
            return TryResolve(in expansion, out tier);
        }

        internal static bool TryResolveFused(
            CompiledTextStudio2DStyle style,
            out TextStudio2DFieldPaddingTier tier,
            TMP_Text sourceText = null)
        {
            return TryResolveFused(style, out tier, out _, sourceText);
        }

        /// <summary>
        /// Resolves the source-field envelope from the distance each fused operation must sample. Translated shadows,
        /// depth offsets and taper are geometry: <paramref name="extendsGeometry"/> reports that the source quad
        /// cannot cover them, so only a direct primitive renderer that extends its quads may draw the style.
        /// </summary>
        internal static bool TryResolveFused(
            CompiledTextStudio2DStyle style,
            out TextStudio2DFieldPaddingTier tier,
            out bool extendsGeometry,
            TMP_Text sourceText = null)
        {
            extendsGeometry = false;
            if (style == null || style.IsPassThrough)
            {
                tier = TextStudio2DFieldPaddingTier.None;
                return true;
            }

            TextStudio2DBoundsExpansion reach = default;
            TextStudio2DBoundsExpansion support = default;
            TextStudio2DBoundsExpansion fadedDepthSupport = default;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.ExecutionPath != TextStudio2DExecutionPath.FusedField)
                    continue;
                // Pixel-sized depth needs the isolated renderer's target-pixel bounds.
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth && operation.FauxDepth.HasDepth &&
                    operation.FauxDepth.Units == TextStudio2DUnit.Pixels)
                {
                    tier = TextStudio2DFieldPaddingTier.None;
                    return false;
                }
                TextStudio2DBoundsExpansion operationReach = TextStudio2DPrimitiveSupport.FieldReach(in operation);
                TextStudio2DBoundsExpansion operationSupport = operation.BoundsExpansion;
                reach = reach.Max(in operationReach);
                support = support.Max(in operationSupport);
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth && operation.FauxDepth.EndFade > .0001f)
                    fadedDepthSupport = fadedDepthSupport.Max(in operationSupport);
                extendsGeometry |= TextStudio2DPrimitiveSupport.LocalGrowth(in operation) > 0f;
            }
            float paddingUnitsPerEm = ResolvePaddingUnitsPerEm(sourceText);
            if (!TryResolve(Scale(in reach, paddingUnitsPerEm), out tier))
                return false;
            // A fragment-traced ray has no exact sample where a far-end fade begins, so grazing contours can notch the
            // unfaded prefix once the ray outgrows the source field. Such depth keeps the isolated renderer.
            TextStudio2DBoundsExpansion scaledFadedDepth = Scale(in fadedDepthSupport, paddingUnitsPerEm);
            if (Mathf.Max(Mathf.Max(scaledFadedDepth.Left, scaledFadedDepth.Right),
                    Mathf.Max(scaledFadedDepth.Bottom, scaledFadedDepth.Top)) > MaximumCapacity)
            {
                tier = TextStudio2DFieldPaddingTier.None;
                return false;
            }
            TextStudio2DBoundsExpansion scaledSupport = Scale(in support, paddingUnitsPerEm);
            float capacity = GetNormalizedCapacity(tier);
            extendsGeometry |= Mathf.Max(Mathf.Max(scaledSupport.Left, scaledSupport.Right),
                Mathf.Max(scaledSupport.Bottom, scaledSupport.Top)) > capacity;
            if (sourceText != null)
            {
                float requiredEm = Mathf.Max(Mathf.Max(support.Left, support.Right), Mathf.Max(support.Bottom, support.Top));
                extendsGeometry |= requiredEm > ResolveGeometryCapacityEm(sourceText, tier);
            }
            return true;
        }

        static TextStudio2DBoundsExpansion Scale(in TextStudio2DBoundsExpansion expansion, float scale) =>
            new TextStudio2DBoundsExpansion(expansion.Left * scale, expansion.Bottom * scale, expansion.Right * scale, expansion.Top * scale);

        static float ResolveGeometryCapacityEm(TMP_Text text, TextStudio2DFieldPaddingTier tier)
        {
            bool primaryBold = (text.fontStyle & FontStyles.Bold) != 0;
            Material previousMaterial = text.fontSharedMaterial;
            float capacity = TextStudioTMPFontMetrics.FusedGeometryPaddingEm(text.font, previousMaterial, tier, primaryBold);
            TMP_TextInfo info = text.textInfo;
            if (info?.characterInfo == null) return capacity;
            TMP_FontAsset previous = text.font;
            bool previousBold = primaryBold;
            for (int index = 0; index < Mathf.Min(info.characterCount, info.characterInfo.Length); index++)
            {
                TMP_CharacterInfo character = info.characterInfo[index];
                if (character.elementType != TMP_TextElementType.Character || character.fontAsset == null) continue;
                bool bold = (character.style & FontStyles.Bold) != 0;
                int materialIndex = character.materialReferenceIndex;
                Material material = info.meshInfo != null && materialIndex >= 0 && materialIndex < info.meshInfo.Length
                    ? info.meshInfo[materialIndex].material : character.fontAsset.material;
                if (character.fontAsset == previous && material == previousMaterial && bold == previousBold) continue;
                capacity = Mathf.Min(capacity, TextStudioTMPFontMetrics.FusedGeometryPaddingEm(character.fontAsset, material, tier, bold));
                previous = character.fontAsset;
                previousMaterial = material;
                previousBold = bold;
            }
            return capacity;
        }

        static float ResolvePaddingUnitsPerEm(TMP_Text text)
        {
            if (text == null) return 1f;
            float maximum = TextStudioTMPFontMetrics.PaddingUnitsPerEm(text.font);
            TMP_TextInfo info = text.textInfo;
            TMP_FontAsset previous = text.font;
            if (info?.characterInfo == null) return maximum;
            for (int index = 0; index < Mathf.Min(info.characterCount, info.characterInfo.Length); index++)
            {
                TMP_FontAsset font = info.characterInfo[index].fontAsset;
                if (font == text.font || font == previous) continue;
                maximum = Mathf.Max(maximum, TextStudioTMPFontMetrics.PaddingUnitsPerEm(font));
                previous = font;
            }
            return maximum;
        }

        internal static bool TryResolve(
            in TextStudio2DBoundsExpansion expansion,
            out TextStudio2DFieldPaddingTier tier)
        {
            float maximum = Mathf.Max(
                Mathf.Max(expansion.Left, expansion.Right),
                Mathf.Max(expansion.Bottom, expansion.Top));
            if (maximum <= 0f)
            {
                tier = TextStudio2DFieldPaddingTier.None;
                return true;
            }
            if (maximum <= CompactCapacity)
            {
                tier = TextStudio2DFieldPaddingTier.Compact;
                return true;
            }
            if (maximum <= StandardCapacity)
            {
                tier = TextStudio2DFieldPaddingTier.Standard;
                return true;
            }
            if (maximum <= WideCapacity)
            {
                tier = TextStudio2DFieldPaddingTier.Wide;
                return true;
            }

            tier = TextStudio2DFieldPaddingTier.None;
            return false;
        }

        internal static float GetNormalizedCapacity(
            TextStudio2DFieldPaddingTier tier)
        {
            return tier switch
            {
                TextStudio2DFieldPaddingTier.Compact => CompactCapacity,
                TextStudio2DFieldPaddingTier.Standard => StandardCapacity,
                TextStudio2DFieldPaddingTier.Wide => WideCapacity,
                _ => 0f
            };
        }

        internal static TextStudio2DFieldPaddingTier ResolveOrNone(
            CompiledTextStudio2DStyle style)
        {
            return TryResolve(style, out TextStudio2DFieldPaddingTier tier)
                ? tier
                : TextStudio2DFieldPaddingTier.None;
        }
    }
}
