using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Optional exact planar geometry accompanying the backend's existing shaped glyph revision.</summary>
    public interface ITextStudioShapedGeometryBackend
    {
        /// <summary>Gets planar geometry for one glyph in the current shaped revision, when available.</summary>
        bool TryGetShapedGlyphGeometry(int shapedGlyphIndex, out TextStudioBackendShapedGlyphGeometry geometry);
        int ShapedPrimitiveCount { get; }
        /// <summary>Gets one filled primitive owned by the current shaped revision.</summary>
        bool TryGetShapedPrimitive(int primitiveIndex, out TextStudioBackendShapedPrimitive primitive);
    }

    /// <summary>Explains why a complete shaped geometry revision cannot currently be published.</summary>
    public interface ITextStudioShapedGeometryDiagnostics
    {
        string ShapedGeometryDiagnostic { get; }
    }

    /// <summary>Additional font-EM to reference-EM mapping without replacing shaped glyph identity.</summary>
    public readonly struct TextStudioBackendShapedGlyphGeometry
    {
        public Matrix4x4 PlanarTransform { get; }
        public float GeometryScale { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public int AssemblyId { get; }
        public bool IsPresent { get; }
        public TextStudioGlyphColorQuad Colors { get; }
        public bool IsValid => GeometryScale > 0f && !float.IsInfinity(GeometryScale) &&
            TextStudioShapedGeometryValidation.IsLinearPlanar(PlanarTransform);

        /// <summary>Maps one shaped glyph's planar outline into the backend's reference space and source range.</summary>
        public TextStudioBackendShapedGlyphGeometry(Matrix4x4 planarTransform, float geometryScale,
            int sourceStart, int sourceLength, int assemblyId = 0, TextStudioGlyphColorQuad colors = default)
        {
            PlanarTransform = planarTransform;
            GeometryScale = geometryScale;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            AssemblyId = assemblyId;
            IsPresent = true;
            Colors = colors;
        }
    }

    /// <summary>Owned filled vector output which has no shaped font-glyph record, such as a formula rule.</summary>
    public readonly struct TextStudioBackendShapedPrimitive
    {
        public int TextUnitIndex { get; }
        public int ClusterIndex { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public ulong OccurrenceId { get; }
        public CurveGlyph Geometry { get; }
        public Vector2 BaselineOrigin { get; }
        public float GeometryScale { get; }
        public TextStudioGlyphColorQuad Colors { get; }
        public int AssemblyId { get; }
        public Hash128 SourceContentIdentity { get; }
        public bool IsValid => TextUnitIndex >= 0 && Geometry != null && Geometry.ContourCount > 0 &&
            GeometryScale > 0f && !float.IsInfinity(GeometryScale) &&
            !float.IsNaN(BaselineOrigin.x) && !float.IsInfinity(BaselineOrigin.x) &&
            !float.IsNaN(BaselineOrigin.y) && !float.IsInfinity(BaselineOrigin.y);

        /// <summary>Describes a filled vector primitive and its placement, source identity, and colors.</summary>
        public TextStudioBackendShapedPrimitive(int textUnitIndex, int clusterIndex, int sourceStart,
            int sourceLength, ulong occurrenceId, CurveGlyph geometry, Vector2 baselineOrigin,
            float geometryScale, TextStudioGlyphColorQuad colors, int assemblyId = 0, Hash128 sourceContentIdentity = default)
        {
            TextUnitIndex = textUnitIndex;
            ClusterIndex = clusterIndex;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            OccurrenceId = occurrenceId;
            Geometry = geometry;
            BaselineOrigin = baselineOrigin;
            GeometryScale = geometryScale;
            Colors = colors;
            AssemblyId = assemblyId;
            SourceContentIdentity = sourceContentIdentity;
        }
    }

    internal static class TextStudioShapedGeometryValidation
    {
        internal static bool IsLinearPlanar(Matrix4x4 value)
        {
            for (int i = 0; i < 16; i++)
                if (float.IsNaN(value[i]) || float.IsInfinity(value[i])) return false;
            float determinant = value.m00 * value.m11 - value.m01 * value.m10;
            if (float.IsNaN(determinant) || float.IsInfinity(determinant)) return false;
            return value.m02 == 0f && value.m12 == 0f && value.m20 == 0f && value.m21 == 0f &&
                value.m03 == 0f && value.m13 == 0f && value.m23 == 0f && value.m30 == 0f &&
                value.m31 == 0f && value.m32 == 0f && value.m22 == 1f && value.m33 == 1f &&
                Mathf.Abs(determinant) > 0.00000001f;
        }
    }
}
