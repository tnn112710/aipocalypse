using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// The rows of a style that TextMeshPro's own SDF material draws in one pass per glyph: a face, an outline band around it
    /// and an underlay, in em and authored colors. Edges are signed distances from the glyph edge, positive inside the glyph.
    /// <see cref="TextStudio2DTextMeshProRoute.TrySelect"/> produces it, and each source material resolves it to TMP material
    /// values with <see cref="TextStudio2DTextMeshProRoute.TryResolveMaterial"/>.
    /// </summary>
    internal readonly struct TextStudio2DTextMeshProStyle : IEquatable<TextStudio2DTextMeshProStyle>
    {
        /// <summary>Fill paint with the fill opacity in alpha; transparent when the style has no fill.</summary>
        internal readonly Color FaceColor;
        /// <summary>Stroke paint with the stroke opacity in alpha.</summary>
        internal readonly Color OutlineColor;
        /// <summary>Face edge, which is the inner edge of the outline band.</summary>
        internal readonly float FaceEdgeEm;
        /// <summary>Outer edge of the outline band; equal to <see cref="FaceEdgeEm"/> without a visible outline.</summary>
        internal readonly float OutlineEdgeEm;
        /// <summary>Softness of the face and outline edges.</summary>
        internal readonly float SoftnessEm;
        internal readonly bool HasShadow;
        /// <summary>Shadow paint with the shadow opacity in alpha.</summary>
        internal readonly Color ShadowColor;
        internal readonly Vector2 ShadowOffsetEm;
        internal readonly float ShadowBlurEm;
        internal readonly float ShadowSpreadEm;
        internal readonly bool IsValid;

        internal TextStudio2DTextMeshProStyle(Color faceColor, Color outlineColor, float faceEdgeEm, float outlineEdgeEm, float softnessEm,
            bool hasShadow, Color shadowColor, Vector2 shadowOffsetEm, float shadowBlurEm, float shadowSpreadEm)
        {
            FaceColor = faceColor;
            OutlineColor = outlineColor;
            FaceEdgeEm = faceEdgeEm;
            OutlineEdgeEm = outlineEdgeEm;
            SoftnessEm = softnessEm;
            HasShadow = hasShadow;
            ShadowColor = hasShadow ? shadowColor : default;
            ShadowOffsetEm = hasShadow ? shadowOffsetEm : default;
            ShadowBlurEm = hasShadow ? shadowBlurEm : 0f;
            ShadowSpreadEm = hasShadow ? shadowSpreadEm : 0f;
            IsValid = true;
        }

        public bool Equals(TextStudio2DTextMeshProStyle other) =>
            IsValid == other.IsValid && FaceColor == other.FaceColor && OutlineColor == other.OutlineColor &&
            FaceEdgeEm.Equals(other.FaceEdgeEm) && OutlineEdgeEm.Equals(other.OutlineEdgeEm) && SoftnessEm.Equals(other.SoftnessEm) &&
            HasShadow == other.HasShadow && ShadowColor == other.ShadowColor && ShadowOffsetEm == other.ShadowOffsetEm &&
            ShadowBlurEm.Equals(other.ShadowBlurEm) && ShadowSpreadEm.Equals(other.ShadowSpreadEm);

        public override bool Equals(object obj) => obj is TextStudio2DTextMeshProStyle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsValid ? 1 : 0;
                hash = hash * 397 ^ FaceColor.GetHashCode();
                hash = hash * 397 ^ OutlineColor.GetHashCode();
                hash = hash * 397 ^ FaceEdgeEm.GetHashCode();
                hash = hash * 397 ^ OutlineEdgeEm.GetHashCode();
                hash = hash * 397 ^ SoftnessEm.GetHashCode();
                hash = hash * 397 ^ (HasShadow ? 1 : 0);
                hash = hash * 397 ^ ShadowColor.GetHashCode();
                hash = hash * 397 ^ ShadowOffsetEm.GetHashCode();
                hash = hash * 397 ^ ShadowBlurEm.GetHashCode();
                return hash * 397 ^ ShadowSpreadEm.GetHashCode();
            }
        }
    }

    /// <summary>
    /// TextMeshPro material values of one source material, before TMP applies its scale ratios. <see cref="ScaleRatioA"/> and
    /// <see cref="ScaleRatioC"/> are the ratios ShaderUtilities.UpdateShaderRatios computes for these values; the underlay ratio
    /// is zero without an underlay.
    /// </summary>
    internal readonly struct TextStudio2DTextMeshProMaterialValues
    {
        internal readonly Color FaceColor;
        internal readonly float FaceDilate;
        internal readonly Color OutlineColor;
        internal readonly float OutlineWidth;
        internal readonly float OutlineSoftness;
        /// <summary>Mobile Distance Field's OUTLINE_ON keyword. Full Distance Field draws its outline from a positive width alone.</summary>
        internal readonly bool OutlineKeyword;
        internal readonly bool Underlay;
        internal readonly Color UnderlayColor;
        internal readonly float UnderlayOffsetX;
        internal readonly float UnderlayOffsetY;
        internal readonly float UnderlayDilate;
        internal readonly float UnderlaySoftness;
        internal readonly float ScaleRatioA;
        internal readonly float ScaleRatioC;

        internal TextStudio2DTextMeshProMaterialValues(Color faceColor, float faceDilate, Color outlineColor, float outlineWidth,
            float outlineSoftness, bool outlineKeyword, bool underlay, Color underlayColor, float underlayOffsetX, float underlayOffsetY,
            float underlayDilate, float underlaySoftness, float scaleRatioA, float scaleRatioC)
        {
            FaceColor = faceColor;
            FaceDilate = faceDilate;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            OutlineSoftness = outlineSoftness;
            OutlineKeyword = outlineKeyword;
            Underlay = underlay;
            UnderlayColor = underlayColor;
            UnderlayOffsetX = underlayOffsetX;
            UnderlayOffsetY = underlayOffsetY;
            UnderlayDilate = underlayDilate;
            UnderlaySoftness = underlaySoftness;
            ScaleRatioA = scaleRatioA;
            ScaleRatioC = scaleRatioC;
        }
    }

    /// <summary>
    /// The default route of styles TextMeshPro can express: one solid fill, a round undashed stroke and a distance-field outer
    /// shadow at Native source quality draw through the text's own TextMeshPro SDF material, one pass per glyph at
    /// TextMeshPro's GPU cost, instead of Text Studio's exact layered programs. Visible differences are TextMeshPro's: a later
    /// glyph's outline or shadow can cover an earlier glyph's face where they touch, and its edge filtering and thin-outline
    /// fade apply. <see cref="CompiledTextStudio2DStyle.ExactLayering"/> keeps the exact layered path for a style.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DTextMeshProRoute
    {
        internal const string MobileShaderName = "TextMeshPro/Mobile/Distance Field";
        internal const string FullShaderName = "TextMeshPro/Distance Field";
        const string OutlineKeyword = "OUTLINE_ON";
        const string UnderlayKeyword = "UNDERLAY_ON";
        const string UnderlayInnerKeyword = "UNDERLAY_INNER";
        const string RatiosOffKeyword = "RATIOS_OFF";
        // Authored lengths are em; comparisons of edges tolerate float noise only.
        const float EdgeTolerance = .0001f;
        // A row at this alpha hides what lies below it as TextMeshPro's partition does.
        const float OpaqueAlpha = .999f;
        // Slack on TextMeshPro's scale-ratio limit before it would shrink the effects.
        const float RatioTolerance = .0001f;

        static readonly int s_faceColor = Shader.PropertyToID("_FaceColor");
        static readonly int s_faceDilate = Shader.PropertyToID("_FaceDilate");
        static readonly int s_faceTexture = Shader.PropertyToID("_FaceTex");
        static readonly int s_outlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int s_outlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int s_outlineSoftness = Shader.PropertyToID("_OutlineSoftness");
        static readonly int s_outlineTexture = Shader.PropertyToID("_OutlineTex");
        static readonly int s_underlayColor = Shader.PropertyToID("_UnderlayColor");
        static readonly int s_underlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        static readonly int s_underlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        static readonly int s_underlayDilate = Shader.PropertyToID("_UnderlayDilate");
        static readonly int s_underlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        static readonly int s_weightNormal = Shader.PropertyToID("_WeightNormal");
        static readonly int s_weightBold = Shader.PropertyToID("_WeightBold");
        static readonly int s_gradientScale = Shader.PropertyToID("_GradientScale");
        static readonly int s_scaleRatioA = Shader.PropertyToID("_ScaleRatioA");
        static readonly int s_scaleRatioC = Shader.PropertyToID("_ScaleRatioC");

        static readonly Dictionary<Shader, TextStudioTMPNativeMaterialModel> s_models =
            new Dictionary<Shader, TextStudioTMPNativeMaterialModel>(new ShaderReferenceComparer());
        static bool s_enabled = true;

        /// <summary>Tests turn the route off to keep exercising the exact layered path with TextMeshPro-expressible styles.</summary>
        internal static bool Enabled
        {
            get => s_enabled;
            set => s_enabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetRoute()
        {
            s_enabled = true;
            s_models.Clear();
        }

        /// <summary>Whether the style alone admits the route; text, effects and source materials decide the rest.</summary>
        internal static bool IsCandidate(CompiledTextStudio2DStyle style) => TrySelect(style, null, out _);

        /// <summary>
        /// Selects the TextMeshPro route for a style the uniform field program accepts (<see cref="TextStudio2DShaderTiers.TryResolveUniformRows"/>)
        /// whose rows TextMeshPro's partition into face, outline band and underlay reproduces: em units, the vertex color tinting
        /// only the face, and opaque upper rows wherever they overlap a lower row, so composing a glyph's rows equals the partition.
        /// A fill above the stroke leaves the visible part of the stroke as the outline; a stroke above the fill must be opaque and
        /// cover the fill edge. A shadow must be the bottom row under an opaque fill, because TextMeshPro hides its underlay below
        /// the face instead of knocking it out. Retained decoration lines keep the exact path.
        /// </summary>
        internal static bool TrySelect(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan,
            out TextStudio2DTextMeshProStyle selected)
        {
            selected = default;
            if (!s_enabled || style == null || style.ExactLayering ||
                !TextStudio2DShaderTiers.TryResolveUniformRows(style, plan, false, false, out int fill, out int stroke, out int shadow) ||
                TextStudio2DHeroProgram.ResolveHeroReason(style) != TextStudio2DRouteReason.None)
                return false;
            for (int index = 0; index < style.OperationCount; index++)
                if (style.OperationAt(index).ExecutionPath != TextStudio2DExecutionPath.FusedField)
                    return false;
            if (fill < 0 && stroke < 0)
                return false;

            Color face = Color.clear;
            float dilation = 0f, fillSoftness = 0f;
            if (fill >= 0)
            {
                ref readonly TextStudio2DCompiledOperation row = ref style.OperationAt(fill);
                face = ResolvePaint(style, in row);
                TextStudio2DFillSpec spec = row.Fill;
                dilation = spec.Dilation;
                fillSoftness = Mathf.Max(0f, spec.Softness);
                // TextMeshPro tints its face with the vertex color, as a fill that takes the animated tint does.
                if (!row.ParticipatesInAnimatedTint && !IsBlack(face) ||
                    spec.Units != TextStudio2DUnit.Em && (dilation != 0f || fillSoftness > 0f))
                    return false;
            }

            Color outline = Color.clear;
            float width = 0f, outside = 0f, strokeSoftness = 0f;
            if (stroke >= 0)
            {
                ref readonly TextStudio2DCompiledOperation row = ref style.OperationAt(stroke);
                outline = ResolvePaint(style, in row);
                TextStudio2DStrokeSpec spec = row.Stroke;
                width = Mathf.Max(0f, spec.Width);
                outside = Mathf.Clamp01((spec.Alignment + 1f) * .5f);
                strokeSoftness = Mathf.Max(0f, spec.Softness);
                // TextMeshPro's outline ignores the vertex color, as a stroke without the animated tint does.
                if (row.ParticipatesInAnimatedTint && !IsBlack(outline) ||
                    spec.Units != TextStudio2DUnit.Em && (width > 0f || strokeSoftness > 0f))
                    return false;
            }

            float faceEdge, outlineEdge;
            if (fill >= 0 && stroke >= 0)
            {
                // TextMeshPro softens the face and outline edges together.
                if (Mathf.Abs(fillSoftness - strokeSoftness) > EdgeTolerance)
                    return false;
                float strokeInner = width * (1f - outside);
                float strokeOuter = -width * outside;
                if (fill < stroke)
                {
                    // The stroke shows outside the fill. It must reach the fill edge, and a translucent fill must not cover any of it.
                    if (-dilation > strokeInner + EdgeTolerance ||
                        face.a < OpaqueAlpha && Mathf.Abs(dilation + strokeInner) > EdgeTolerance)
                        return false;
                    faceEdge = -dilation;
                    outlineEdge = Mathf.Min(strokeOuter, faceEdge);
                }
                else
                {
                    // An opaque stroke covers the fill edge: the fill neither reaches past the stroke nor stops inside it.
                    if (outline.a < OpaqueAlpha || dilation > width * outside + EdgeTolerance || -dilation > strokeInner + EdgeTolerance)
                        return false;
                    faceEdge = strokeInner;
                    outlineEdge = strokeOuter;
                }
            }
            else if (fill >= 0)
            {
                faceEdge = -dilation;
                outlineEdge = faceEdge;
            }
            else
            {
                faceEdge = width * (1f - outside);
                outlineEdge = -width * outside;
            }

            bool hasShadow = shadow >= 0;
            Color shadowColor = default;
            Vector2 offset = default;
            float blur = 0f, spread = 0f;
            if (hasShadow)
            {
                // The exact path removes the glyph from the shadow. TextMeshPro composes its underlay below everything it draws,
                // so the shadow must be the bottom row and everything drawn over the glyph, soft ramps included, must be opaque.
                float softness = fill >= 0 ? fillSoftness : strokeSoftness;
                if (fill < 0 || face.a < OpaqueAlpha || shadow < fill || stroke >= 0 && shadow < stroke ||
                    outlineEdge + softness > EdgeTolerance || faceEdge + softness > EdgeTolerance && outline.a < OpaqueAlpha)
                    return false;
                ref readonly TextStudio2DCompiledOperation row = ref style.OperationAt(shadow);
                shadowColor = ResolvePaint(style, in row);
                TextStudio2DShadowSpec spec = row.Shadow;
                offset = spec.Offset;
                blur = Mathf.Max(0f, spec.Blur);
                spread = Mathf.Max(0f, spec.Spread);
                // TextMeshPro's underlay ignores the vertex color, as a shadow without the animated tint does.
                if (row.ParticipatesInAnimatedTint && !IsBlack(shadowColor) ||
                    spec.Units != TextStudio2DUnit.Em && (offset != Vector2.zero || blur > 0f || spread > 0f))
                    return false;
            }

            selected = new TextStudio2DTextMeshProStyle(face, outline, faceEdge, outlineEdge, fill >= 0 ? fillSoftness : strokeSoftness,
                hasShadow, shadowColor, offset, blur, spread);
            return true;
        }

        static Color ResolvePaint(CompiledTextStudio2DStyle style, in TextStudio2DCompiledOperation row)
        {
            Color color = style.GetPaint(row.PaintIndex).Color;
            color.a *= row.Opacity;
            return color;
        }

        static bool IsBlack(Color color) => color.r <= 0f && color.g <= 0f && color.b <= 0f;

        /// <summary>
        /// Converts a TextMeshPro style to material values of one TextMeshPro Mobile or Full Distance Field source material, or
        /// rejects the source. Lengths convert through the atlas: an em spans <paramref name="pixelsPerEm"/> texels, and the SDF
        /// alpha covers 2 * _GradientScale texels, the field units of Text Studio's own programs. TMP multiplies face dilate,
        /// outline width and softness by _ScaleRatioA = (G - 1) / G and the underlay by _ScaleRatioC = (G - 1)(1 - W - D) / G
        /// (ShaderUtilities.UpdateShaderRatios, W the larger weight / 4, D the face dilate); the values divide by those ratios, and
        /// a source whose values would push either ratio below its unclamped value is rejected, because TMP would then shrink
        /// the effects to fit its field range. The face color folds in the source face color, which the exact path applies to
        /// rows that take the animated tint; under TrySelect's tint rule the outline and underlay take none. Mobile pairs
        /// UNDERLAY_ON with OUTLINE_ON at zero width, which renders identically and uses the variant TMP's Essential Resources
        /// keep in player builds; Full Distance Field shadows keep the exact path, because players compile its Underlay variant
        /// only when a material asset uses it, and so do its soft outlines, because it softens only their outer edge.
        /// </summary>
        internal static bool TryResolveMaterial(in TextStudio2DTextMeshProStyle style, Material source, float pixelsPerEm,
            out TextStudio2DTextMeshProMaterialValues values)
        {
            values = default;
            if (!style.IsValid || source == null || !(pixelsPerEm > 0f) || float.IsInfinity(pixelsPerEm))
                return false;
            TextStudioTMPNativeMaterialModel model = ResolveModel(source.shader);
            if (model == TextStudioTMPNativeMaterialModel.Unknown || style.HasShadow && model != TextStudioTMPNativeMaterialModel.Mobile)
                return false;
            if (!source.HasProperty(s_faceColor) || !source.HasProperty(s_faceDilate) || !source.HasProperty(s_outlineColor) ||
                !source.HasProperty(s_outlineWidth) || !source.HasProperty(s_outlineSoftness) || !source.HasProperty(s_weightNormal) ||
                !source.HasProperty(s_weightBold) || !source.HasProperty(s_gradientScale) || !source.HasProperty(s_scaleRatioA) ||
                style.HasShadow && (!source.HasProperty(s_underlayColor) || !source.HasProperty(s_underlayOffsetX) ||
                                    !source.HasProperty(s_underlayOffsetY) || !source.HasProperty(s_underlayDilate) ||
                                    !source.HasProperty(s_underlaySoftness) || !source.HasProperty(s_scaleRatioC)))
                return false;
            // The source must contribute nothing these values replace: TMP outline, underlay, bevel or glow, face or outline
            // textures, an atlas other than a single distance, or a translucent face.
            if (source.IsKeywordEnabled(OutlineKeyword) || source.IsKeywordEnabled(UnderlayKeyword) ||
                source.IsKeywordEnabled(UnderlayInnerKeyword) || source.IsKeywordEnabled("BEVEL_ON") || source.IsKeywordEnabled("GLOW_ON") ||
                source.IsKeywordEnabled("MASK_HARD") || source.IsKeywordEnabled("MASK_SOFT") ||
                model == TextStudioTMPNativeMaterialModel.Full && source.GetFloat(s_outlineWidth) > .0001f ||
                !IsWhite(source, s_faceTexture) || !IsWhite(source, s_outlineTexture) ||
                TextStudioGlyphFieldContract.ResolveFormat(source) != TextStudioGlyphFieldFormat.SingleDistance)
                return false;
            Color sourceFace = source.GetColor(s_faceColor);
            float gradient = source.GetFloat(s_gradientScale);
            if (sourceFace.a < OpaqueAlpha || !(gradient > 1f) || float.IsInfinity(gradient))
                return false;

            // TextMeshPro reads RATIOS_OFF from the material's keyword names (ShaderUtilities.UpdateShaderRatios), which keep it
            // even where the shader declares no such keyword, so the route reads it the same way; this runs only on cache misses.
            bool ratios = Array.IndexOf(source.shaderKeywords, RatiosOffKeyword) < 0;
            float weight = Mathf.Max(source.GetFloat(s_weightNormal), source.GetFloat(s_weightBold)) * .25f;
            float sourceDilate = source.GetFloat(s_faceDilate);
            float sourceSoftness = source.GetFloat(s_outlineSoftness);
            // The reach TMP's ratios fit every face effect into, and the ratio TMP computes for the source itself.
            float reach = (gradient - 1f) / gradient;
            float ratio = ratios ? reach : 1f;
            float sourceRatio = ratios ? reach / Mathf.Max(1f, weight + sourceDilate + source.GetFloat(s_outlineWidth) + sourceSoftness) : 1f;
            float alphaPerEm = pixelsPerEm / (2f * gradient);
            float band = Mathf.Max(0f, style.FaceEdgeEm - style.OutlineEdgeEm) * alphaPerEm;
            if (model == TextStudioTMPNativeMaterialModel.Full && band > 0f && style.SoftnessEm > EdgeTolerance)
                return false;
            // Outward shift of the band center from the glyph edge; TMP's weight term moves by half the dilate.
            float shift = -(style.FaceEdgeEm + style.OutlineEdgeEm) * .5f * alphaPerEm;
            float outlineWidth = band / ratio;
            float faceDilate = (sourceDilate * sourceRatio + 2f * shift) / ratio;
            float outlineSoftness = (sourceSoftness * sourceRatio + 2f * style.SoftnessEm * alphaPerEm) / ratio;
            if (ratio * (weight + faceDilate + outlineWidth + outlineSoftness) > reach + RatioTolerance)
                return false;

            Color faceColor = MultiplyColors(style.FaceColor, sourceFace);
            if (!style.HasShadow)
            {
                values = new TextStudio2DTextMeshProMaterialValues(faceColor, faceDilate, style.OutlineColor, outlineWidth, outlineSoftness,
                    model == TextStudioTMPNativeMaterialModel.Mobile && outlineWidth > 0f, false, default, 0f, 0f, 0f, 0f, ratio, 0f);
                return true;
            }

            // TMP measures the underlay from its dilated face, so the dilate subtracts the face shift from the shadow spread.
            float shadowReach = (gradient - 1f) * (1f - weight - faceDilate) / gradient;
            if (!(shadowReach > .0001f))
                return false;
            float shadowRatio = ratios ? shadowReach : 1f;
            float offsetX = style.ShadowOffsetEm.x * pixelsPerEm / (shadowRatio * gradient);
            float offsetY = style.ShadowOffsetEm.y * pixelsPerEm / (shadowRatio * gradient);
            float underlayDilate = 2f * (style.ShadowSpreadEm * alphaPerEm - shift) / shadowRatio;
            float underlaySoftness = 2f * style.ShadowBlurEm * alphaPerEm / shadowRatio;
            if (shadowRatio * (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetY)) + underlayDilate + underlaySoftness) > shadowReach + RatioTolerance)
                return false;
            values = new TextStudio2DTextMeshProMaterialValues(faceColor, faceDilate, style.OutlineColor, outlineWidth, outlineSoftness,
                true, true, style.ShadowColor, offsetX, offsetY, underlayDilate, underlaySoftness, ratio, shadowRatio);
            return true;
        }

        /// <summary>Writes resolved values into a copy of their source material and lets TMP compute its scale ratios.</summary>
        internal static void Apply(Material material, in TextStudio2DTextMeshProMaterialValues values)
        {
            material.SetColor(s_faceColor, values.FaceColor);
            material.SetFloat(s_faceDilate, values.FaceDilate);
            material.SetColor(s_outlineColor, values.OutlineColor);
            material.SetFloat(s_outlineWidth, values.OutlineWidth);
            material.SetFloat(s_outlineSoftness, values.OutlineSoftness);
            SetKeyword(material, OutlineKeyword, values.OutlineKeyword);
            material.DisableKeyword(UnderlayInnerKeyword);
            if (values.Underlay)
            {
                material.SetColor(s_underlayColor, values.UnderlayColor);
                material.SetFloat(s_underlayOffsetX, values.UnderlayOffsetX);
                material.SetFloat(s_underlayOffsetY, values.UnderlayOffsetY);
                material.SetFloat(s_underlayDilate, values.UnderlayDilate);
                material.SetFloat(s_underlaySoftness, values.UnderlaySoftness);
            }
            SetKeyword(material, UnderlayKeyword, values.Underlay);
            // TMP recomputes the ratios whenever it measures padding; computing them here keeps the material complete before that.
            ShaderUtilities.UpdateShaderRatios(material);
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        static bool IsWhite(Material material, int property)
        {
            if (!material.HasProperty(property))
                return true;
            Texture texture = material.GetTexture(property);
            return texture == null || texture == Texture2D.whiteTexture;
        }

        // Material colors are authored in gamma space and linearized by Unity in a linear project, like the exact path's packed
        // colors, so their product is formed in the space the shader multiplies in.
        static Color MultiplyColors(Color color, Color tint)
        {
            if (tint.r == 1f && tint.g == 1f && tint.b == 1f)
                return new Color(color.r, color.g, color.b, color.a * tint.a);
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                return color * tint;
            Color product = (color.linear * tint.linear).gamma;
            product.a = color.a * tint.a;
            return product;
        }

        // Shader names are read once per shader instance, so the per-frame source check does not allocate.
        static TextStudioTMPNativeMaterialModel ResolveModel(Shader shader)
        {
            if (shader == null)
                return TextStudioTMPNativeMaterialModel.Unknown;
            if (s_models.TryGetValue(shader, out TextStudioTMPNativeMaterialModel model))
                return model;
            string name = shader.name;
            model = string.Equals(name, MobileShaderName, StringComparison.Ordinal) ? TextStudioTMPNativeMaterialModel.Mobile
                : string.Equals(name, FullShaderName, StringComparison.Ordinal) ? TextStudioTMPNativeMaterialModel.Full
                : TextStudioTMPNativeMaterialModel.Unknown;
            s_models[shader] = model;
            return model;
        }

        sealed class ShaderReferenceComparer : IEqualityComparer<Shader>
        {
            public bool Equals(Shader x, Shader y) => ReferenceEquals(x, y);
            public int GetHashCode(Shader obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
