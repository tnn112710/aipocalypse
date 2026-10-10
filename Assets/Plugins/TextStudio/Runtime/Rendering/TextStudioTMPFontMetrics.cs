using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace TextStudio
{
    /// <summary>Native atlas density is independent of text size, transform scale, and prepared field density.</summary>
    internal static class TextStudioTMPFontMetrics
    {
        static readonly int s_weightNormal = Shader.PropertyToID("_WeightNormal");
        static readonly int s_weightBold = Shader.PropertyToID("_WeightBold");
        static readonly int s_faceDilate = Shader.PropertyToID("_FaceDilate");
        static readonly int s_scaleRatioA = Shader.PropertyToID("_ScaleRatioA");
        static readonly int s_gradientScale = Shader.PropertyToID("_GradientScale");

        internal static float NativeFaceOffsetEm(Material material, float pixelsPerEm, bool bold)
        {
            if (material == null || pixelsPerEm <= 0) return 0;
            float weight = Read(material, bold ? s_weightBold : s_weightNormal, 0) * .25f;
            float offset = (weight + Read(material, s_faceDilate, 0)) * Read(material, s_scaleRatioA, 1) *
                Read(material, s_gradientScale, 1) / pixelsPerEm;
            return float.IsFinite(offset) ? offset : 0;
        }

        internal static void AddNativeFaceFingerprint(ref TextStudioDeterministicHash64 hash, Material material)
        {
            hash.Add(Read(material, s_weightNormal, 0)); hash.Add(Read(material, s_weightBold, 0));
            hash.Add(Read(material, s_faceDilate, 0)); hash.Add(Read(material, s_scaleRatioA, 1));
            hash.Add(Read(material, s_gradientScale, 1));
        }

        static float Read(Material material, int property, float fallback) =>
            material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;

        internal static ulong ShapingIdentity(TMP_FontAsset font)
        {
            if (font == null || string.IsNullOrEmpty(font.creationSettings.sourceFontFileGUID)) return 0;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(font.creationSettings.sourceFontFileGUID);
            hash.Add(font.creationSettings.faceIndex);
            hash.Add(font.faceInfo.familyName); hash.Add(font.faceInfo.styleName);
            return hash.Value;
        }

        internal static float PixelsPerEm(TMP_Text text, int materialIndex, Material material)
        {
            TMP_FontAsset font = text != null ? text.font : null;
            if (text != null && materialIndex > 0 && text.textInfo?.characterInfo != null)
            {
                TMP_TextInfo info = text.textInfo;
                int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
                for (int index = 0; index < count; index++)
                {
                    TMP_CharacterInfo character = info.characterInfo[index];
                    if (character.materialReferenceIndex != materialIndex || character.elementType == TMP_TextElementType.Sprite)
                        continue;
                    font = character.fontAsset;
                    break;
                }
            }
            return TextStudioGlyphFieldContract.ResolvePixelsPerEm(material, font != null ? font.faceInfo.pointSize : 0f);
        }

        internal static Vector4 AtlasToEm(TMP_FontAsset font, Glyph glyph)
        {
            if (font == null || glyph == null || font.faceInfo.pointSize <= 0 || glyph.atlasIndex < 0 ||
                glyph.atlasIndex >= font.atlasTextures.Length || font.atlasTextures[glyph.atlasIndex] == null) return default;
            Texture2D atlas = font.atlasTextures[glyph.atlasIndex];
            float density = font.faceInfo.pointSize;
            return new Vector4(atlas.width / density, atlas.height / density,
                (glyph.metrics.horizontalBearingX - glyph.glyphRect.x) / density,
                (glyph.metrics.horizontalBearingY - glyph.metrics.height - glyph.glyphRect.y) / density);
        }

        internal static float FieldUnitsPerEm(float pixelsPerEm, float gradientScale) =>
            pixelsPerEm > 0 ? pixelsPerEm / Mathf.Max(.0001f, 2f * gradientScale) : 1f;

        internal static float PaddingUnitsPerEm(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return 1f;
            float density = TextStudioGlyphFieldContract.ResolvePixelsPerEm(font.material, font.faceInfo.pointSize);
            float gradient = font.material.HasProperty("_GradientScale") ? font.material.GetFloat("_GradientScale") : 1f;
            return density > 0 ? density / Mathf.Max(.0001f, gradient) : 1f;
        }
    }
}
