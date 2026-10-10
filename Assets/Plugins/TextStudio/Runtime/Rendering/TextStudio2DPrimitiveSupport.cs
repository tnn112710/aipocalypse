using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

#if UNITY_2023_2_OR_NEWER
using TmpUv0 = UnityEngine.Vector4;
#else
using TmpUv0 = UnityEngine.Vector2;
#endif

namespace TextStudio
{
    /// <summary>
    /// Shared glyph-primitive geometry: support expansion in source-quad local units and bilinear
    /// extrapolation of posed quads. Local 0..1 always spans the source TMP quad, so shaders can
    /// recover the isolated atlas rectangle after a primitive grows beyond it.
    /// </summary>
    internal static class TextStudio2DPrimitiveSupport
    {
        internal const float FieldAntialiasGuardPixels = 1f;
        const float MinimumExtent = 0.0001f;

        /// <summary>
        /// Geometry support that a field sample radius does not need: translation of shadows and depth.
        /// Direct primitives cover it with geometry; only the remaining reach must fit the source field.
        /// </summary>
        internal static TextStudio2DBoundsExpansion FieldReach(in TextStudio2DCompiledOperation operation)
        {
            if (TextStudio2DFieldPadding.UsesPixelUnits(in operation))
                return operation.BoundsExpansion;
            switch (operation.Kind)
            {
                case TextStudio2DLayerKind.OuterShadow:
                    return Uniform(Mathf.Max(0f, operation.Shadow.Blur + operation.Shadow.Spread));
                case TextStudio2DLayerKind.FauxDepth:
                    return operation.FauxDepth.HasDepth ? Uniform(DepthEdge(operation.FauxDepth)) : default;
                default:
                    return operation.BoundsExpansion;
            }
        }

        /// <summary>
        /// Growth of a tapered depth silhouette in source-quad local units. The trace scales source slices
        /// around the quad center, so this does not depend on font units.
        /// </summary>
        internal static float LocalGrowth(in TextStudio2DCompiledOperation operation)
        {
            if (operation.Kind != TextStudio2DLayerKind.FauxDepth || !operation.FauxDepth.HasDepth ||
                operation.ExecutionPath != TextStudio2DExecutionPath.FusedField)
                return 0f;
            // The far slice spans the source quad scaled by 1 - taper, so a negative taper grows each side by half of it.
            float taper = operation.FauxDepth.ResolveTaper(operation.FauxDepth.Offset.magnitude);
            return Mathf.Max(0f, -taper) * .5f;
        }

        /// <summary>
        /// Field operations whose evaluation is too large for the fused multi-operation loop: bevel, faux depth, material
        /// response, graphic and animated effects. They always receive their own single-operation primitive, and the
        /// shader's fused loop compiles without them. Matches TextStudioIsIsolatedOperationKind.
        /// </summary>
        internal static bool IsIsolatedOperation(in TextStudio2DCompiledOperation operation) =>
            operation.ExecutionPath == TextStudio2DExecutionPath.FusedField && IsIsolatedKind(operation.Kind);

        internal static bool IsIsolatedKind(TextStudio2DLayerKind kind) =>
            kind == TextStudio2DLayerKind.Bevel || kind == TextStudio2DLayerKind.FauxDepth ||
            kind == TextStudio2DLayerKind.MaterialResponse || kind == TextStudio2DLayerKind.GraphicEffect ||
            kind == TextStudio2DLayerKind.AnimatedEffect;

        internal static bool HasIsolatedOperations(CompiledTextStudio2DStyle style)
        {
            if (style == null) return false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (IsIsolatedOperation(in operation)) return true;
            }
            return false;
        }

        static float DepthEdge(TextStudio2DFauxDepthSpec depth) =>
            Mathf.Max(0f, depth.Softness) + Mathf.Max(0f, depth.SilhouetteOutset);

        /// <summary>
        /// Geometry a direct primitive must cover beyond its source glyph. Translation moves the whole silhouette, so the
        /// source quad's padding must travel with it; reach is sampled around the silhouette and may reuse that padding.
        /// </summary>
        internal struct Support
        {
            internal TextStudio2DBoundsExpansion TranslationEm;
            internal TextStudio2DBoundsExpansion ReachEm;
            /// <summary>Soft coverage a field trace adds beyond its authored edge, in field pixels.</summary>
            internal float TailPixels;
            /// <summary>Taper growth in source-quad local units.</summary>
            internal float LocalGrowth;

            internal bool IsEmpty => TranslationEm.Equals(default(TextStudio2DBoundsExpansion)) &&
                ReachEm.Equals(default(TextStudio2DBoundsExpansion)) && LocalGrowth <= 0f;

            internal void Include(in TextStudio2DCompiledOperation operation)
            {
                LocalGrowth = Mathf.Max(LocalGrowth, TextStudio2DPrimitiveSupport.LocalGrowth(in operation));
                if (operation.ExecutionPath != TextStudio2DExecutionPath.FusedField ||
                    operation.Kind == TextStudio2DLayerKind.ColorAdjustment ||
                    TextStudio2DFieldPadding.UsesPixelUnits(in operation))
                    return;
                TextStudio2DBoundsExpansion reach = FieldReach(in operation);
                ReachEm = ReachEm.Max(in reach);
                Vector2 offset = operation.Kind switch
                {
                    TextStudio2DLayerKind.OuterShadow => operation.Shadow.Offset,
                    TextStudio2DLayerKind.FauxDepth when operation.FauxDepth.HasDepth => operation.FauxDepth.Offset,
                    _ => Vector2.zero
                };
                TextStudio2DBoundsExpansion translation = new TextStudio2DBoundsExpansion(
                    Mathf.Max(0f, -offset.x), Mathf.Max(0f, -offset.y), Mathf.Max(0f, offset.x), Mathf.Max(0f, offset.y));
                TranslationEm = TranslationEm.Max(in translation);
                // The depth trace widens each slice by up to 2.25 field pixels of continuation plus half a pixel of
                // antialiasing (TextStudioDepthCoverageFromDistance). Clipping that tail leaves a flat, hard-cut fringe.
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth && operation.FauxDepth.HasDepth)
                    TailPixels = Mathf.Max(TailPixels, DepthTraceTailPixels);
            }
        }

        const float DepthTraceTailPixels = 2.75f;

        /// <summary>
        /// Support of the selected field operation, or of every field operation in <paramref name="operationMask"/> when
        /// <paramref name="operationIndex"/> is -2. Pixel-unit operations remain inside the prepared field envelope.
        /// </summary>
        internal static Support ResolveSupport(CompiledTextStudio2DStyle style, int operationIndex, int operationMask, int span = 0)
        {
            Support support = default;
            if (style == null || operationIndex == -1) return support;
            if (operationIndex >= 0)
            {
                if (operationIndex < style.OperationCount)
                {
                    ref readonly TextStudio2DCompiledOperation selected = ref style.OperationAt(operationIndex);
                    support.Include(in selected);
                }
                return support;
            }
            int first = span * TextStudio2DStyleIdentity.OperationMaskBits;
            int count = Mathf.Min(style.OperationCount - first, TextStudio2DStyleIdentity.OperationMaskBits);
            for (int bit = 0; bit < count; bit++)
            {
                if ((operationMask & (1 << bit)) == 0) continue;
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(first + bit);
                support.Include(in operation);
            }
            return support;
        }

        /// <summary>
        /// Resolves local primitive bounds (min x, min y, max x, max y) that cover <paramref name="support"/> around the
        /// tight glyph. Reach reuses padding the source quad already carries; translation always extends the quad.
        /// </summary>
        internal static Vector4 ResolveLocalBounds(
            in TextStudioVisualGlyph glyph,
            in TMP_CharacterInfo character,
            in TMP_MeshInfo mesh,
            int vertexIndex,
            Texture atlas,
            in Support support)
        {
            float growth = support.LocalGrowth;
            Vector4 bounds = new Vector4(-growth, -growth, 1f + growth, 1f + growth);
            if (support.IsEmpty || atlas == null ||
                glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph || glyph.FieldPixelsPerEm <= 0f ||
                mesh.uvs0 == null || vertexIndex < 0 || vertexIndex + 3 >= mesh.uvs0.Length)
                return bounds;
            Vector2 uvMinimum = UvXY(mesh.uvs0[vertexIndex]);
            Vector2 uvMaximum = uvMinimum;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector2 uv = UvXY(mesh.uvs0[vertexIndex + corner]);
                uvMinimum = Vector2.Min(uvMinimum, uv);
                uvMaximum = Vector2.Max(uvMaximum, uv);
            }
            Vector2 atlasSize = new Vector2(atlas.width, atlas.height);
            Vector2 quadMinimum = Vector2.Scale(uvMinimum, atlasSize);
            Vector2 quadMaximum = Vector2.Scale(uvMaximum, atlasSize);
            // Padding the source quad already carries around the tight glyph, in atlas pixels.
            Vector4 covered = Vector4.zero;
            if (TryResolveGlyphRect(in character, out GlyphRect rect))
                covered = new Vector4(
                    Mathf.Max(0f, rect.x - quadMinimum.x),
                    Mathf.Max(0f, rect.y - quadMinimum.y),
                    Mathf.Max(0f, quadMaximum.x - (rect.x + rect.width)),
                    Mathf.Max(0f, quadMaximum.y - (rect.y + rect.height)));
            return ResolveLocalBounds(in support, quadMinimum, quadMaximum, covered, glyph.FieldPixelsPerEm);
        }

        /// <summary>
        /// Resolves local primitive bounds for a source quad spanning <paramref name="quadMinimum"/> to
        /// <paramref name="quadMaximum"/> field pixels, of which <paramref name="covered"/> (left, bottom, right, top) is
        /// padding around the tight glyph.
        /// </summary>
        internal static Vector4 ResolveLocalBounds(in Support support, Vector2 quadMinimum, Vector2 quadMaximum, Vector4 covered, float pixelsPerEm)
        {
            float growth = support.LocalGrowth;
            Vector4 bounds = new Vector4(-growth, -growth, 1f + growth, 1f + growth);
            if (support.IsEmpty || pixelsPerEm <= 0f) return bounds;
            Vector2 quadSize = Vector2.Max(quadMaximum - quadMinimum, Vector2.one * MinimumExtent);
            float tail = support.TailPixels;
            bounds.x -= Extension(support.TranslationEm.Left, support.ReachEm.Left, tail, covered.x, pixelsPerEm, quadSize.x);
            bounds.y -= Extension(support.TranslationEm.Bottom, support.ReachEm.Bottom, tail, covered.y, pixelsPerEm, quadSize.y);
            bounds.z += Extension(support.TranslationEm.Right, support.ReachEm.Right, tail, covered.z, pixelsPerEm, quadSize.x);
            bounds.w += Extension(support.TranslationEm.Top, support.ReachEm.Top, tail, covered.w, pixelsPerEm, quadSize.y);
            return bounds;
        }

        static float Extension(float translationEm, float reachEm, float tailPixels, float coveredPixels, float pixelsPerEm, float quadPixels)
        {
            float reach = reachEm > 0f || tailPixels > 0f
                ? reachEm * pixelsPerEm + tailPixels + FieldAntialiasGuardPixels
                : 0f;
            float required = translationEm * pixelsPerEm + Mathf.Max(0f, reach - coveredPixels);
            return required / quadPixels;
        }

        static bool TryResolveGlyphRect(in TMP_CharacterInfo character, out GlyphRect rect)
        {
            rect = default;
            Glyph glyph = character.textElement?.glyph;
            if (glyph == null) return false;
            rect = glyph.glyphRect;
            return rect.width > 0 && rect.height > 0;
        }

        internal static Texture ResolveAtlas(in TMP_CharacterInfo character, in TextStudioVisualGlyph glyph, Material material)
        {
            if (glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph) return material != null ? material.mainTexture : null;
            TMP_FontAsset font = character.fontAsset;
            int atlasPage = glyph.AtlasPage;
            if (font != null && font.atlasTextures != null && atlasPage >= 0 && atlasPage < font.atlasTextures.Length &&
                font.atlasTextures[atlasPage] != null)
                return font.atlasTextures[atlasPage];
            return material != null ? material.mainTexture : null;
        }

        internal static Vector2 UvXY(TmpUv0 value) => new Vector2(value.x, value.y);

        internal static Vector4 ToVector4(TmpUv0 value)
        {
#if UNITY_2023_2_OR_NEWER
            return value;
#else
            return new Vector4(value.x, value.y, 0f, 1f);
#endif
        }

        internal static Vector3 Bilinear(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, float x, float y) =>
            Vector3.LerpUnclamped(Vector3.LerpUnclamped(bottomLeft, bottomRight, x), Vector3.LerpUnclamped(topLeft, topRight, x), y);

        internal static Vector4 Bilinear(Vector4 bottomLeft, Vector4 topLeft, Vector4 topRight, Vector4 bottomRight, float x, float y) =>
            Vector4.LerpUnclamped(Vector4.LerpUnclamped(bottomLeft, bottomRight, x), Vector4.LerpUnclamped(topLeft, topRight, x), y);

        internal static Vector2 Bilinear(Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, float x, float y) =>
            Vector2.LerpUnclamped(Vector2.LerpUnclamped(bottomLeft, bottomRight, x), Vector2.LerpUnclamped(topLeft, topRight, x), y);

        /// <summary>Extrapolated vertex colors saturate instead of wrapping.</summary>
        internal static Color32 Bilinear(Color32 bottomLeft, Color32 topLeft, Color32 topRight, Color32 bottomRight, float x, float y)
        {
            if (bottomLeft.Equals(topLeft) && bottomLeft.Equals(topRight) && bottomLeft.Equals(bottomRight))
                return bottomLeft;
            Color color = Color.LerpUnclamped(Color.LerpUnclamped(bottomLeft, bottomRight, x), Color.LerpUnclamped(topLeft, topRight, x), y);
            return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
        }

        static TextStudio2DBoundsExpansion Uniform(float value) => new TextStudio2DBoundsExpansion(value, value, value, value);
    }
}
