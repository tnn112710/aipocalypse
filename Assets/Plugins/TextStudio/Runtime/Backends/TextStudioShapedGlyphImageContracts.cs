using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Result of an exact shaped-image request. Missing capability never implies an empty image.</summary>
    public enum TextStudioGlyphImageStatus { Ready, NotAnImage, Unavailable, Pending, Stale, InvalidData }
    /// <summary>Encoding of the image RGB channels; alpha is always linear coverage.</summary>
    public enum TextStudioGlyphImageColorSpace { Srgb, Linear }
    /// <summary>Whether stored RGB channels are independent or multiplied by alpha in the declared channel encoding. Alpha is linear coverage; providers with another premultiplication convention must normalize before capture.</summary>
    public enum TextStudioGlyphImageAlpha { Straight, Premultiplied }

    /// <summary>Stable exact rasterization identity. ClusterText is the complete UTF-16 cluster, including ligatures and joiners; a Unicode scalar is not a substitute.</summary>
    public readonly struct TextStudioGlyphImageIdentity : IEquatable<TextStudioGlyphImageIdentity>
    {
        public string FontContentHash { get; }
        public int FaceIndex { get; }
        public int GlyphIndex { get; }
        public string ClusterText { get; }
        public string PaletteIdentity { get; }
        public string VariationIdentity { get; }
        public bool IsValid => !string.IsNullOrEmpty(FontContentHash) && !string.IsNullOrEmpty(ClusterText) && FaceIndex >= 0 && GlyphIndex >= 0;
        /// <summary>Identifies exact glyph pixels by font content, face, glyph, complete cluster, palette, and variation.</summary>
        public TextStudioGlyphImageIdentity(string fontContentHash, int faceIndex, int glyphIndex,
            string clusterText, string paletteIdentity = "", string variationIdentity = "")
        {
            if (string.IsNullOrEmpty(fontContentHash) || fontContentHash.Length != 64)
                throw new ArgumentException("The exact font content requires a SHA-256 identity.", nameof(fontContentHash));
            for (int i = 0; i < fontContentHash.Length; i++)
                if (!Uri.IsHexDigit(fontContentHash[i])) throw new ArgumentException("Invalid font SHA-256 identity.", nameof(fontContentHash));
            if (faceIndex < 0 || glyphIndex < 0) throw new ArgumentOutOfRangeException(nameof(glyphIndex));
            if (string.IsNullOrEmpty(clusterText)) throw new ArgumentException("The complete shaped cluster is required.", nameof(clusterText));
            FontContentHash = fontContentHash.ToLowerInvariant(); FaceIndex = faceIndex; GlyphIndex = glyphIndex;
            ClusterText = clusterText; PaletteIdentity = paletteIdentity ?? string.Empty; VariationIdentity = variationIdentity ?? string.Empty;
        }
        public bool Equals(TextStudioGlyphImageIdentity other) => FaceIndex == other.FaceIndex && GlyphIndex == other.GlyphIndex &&
            string.Equals(FontContentHash, other.FontContentHash, StringComparison.Ordinal) && string.Equals(ClusterText, other.ClusterText, StringComparison.Ordinal) &&
            string.Equals(PaletteIdentity, other.PaletteIdentity, StringComparison.Ordinal) && string.Equals(VariationIdentity, other.VariationIdentity, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TextStudioGlyphImageIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FontContentHash, FaceIndex, GlyphIndex, ClusterText, PaletteIdentity, VariationIdentity);
    }

    /// <summary>One exact shaped occurrence. Revisions and layout positions are separate from the reusable raster identity.</summary>
    public readonly struct TextStudioGlyphImageRequest : IEquatable<TextStudioGlyphImageRequest>
    {
        public TextStudioGlyphImageIdentity Identity { get; }
        public TextStudioBackendShapedGlyph Glyph { get; }
        public int ShapedRevision { get; }
        public int ShapedGlyphIndex { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public bool IsValid => Identity.IsValid && ShapedGlyphIndex >= 0 && SourceStart >= 0 && SourceLength == Identity.ClusterText.Length;
        /// <summary>Associates an exact image identity with one validated shaped glyph occurrence and source range.</summary>
        public TextStudioGlyphImageRequest(in TextStudioGlyphImageIdentity identity, in TextStudioBackendShapedGlyph glyph,
            int shapedRevision, int shapedGlyphIndex, int sourceStart, int sourceLength)
        {
            if (!identity.IsValid || glyph.GlyphIndex != identity.GlyphIndex || glyph.TextUnitIndex < 0 || glyph.ClusterIndex < 0 ||
                shapedGlyphIndex < 0 || sourceStart < 0 || sourceLength != identity.ClusterText.Length || sourceStart > int.MaxValue - sourceLength)
                throw new ArgumentException("Image identity must match the complete shaped glyph, cluster and source range.");
            if (!Finite(glyph.BaselineOrigin) || !Finite(glyph.Advance) || !float.IsFinite(glyph.GeometryScale) || glyph.GeometryScale <= 0f ||
                !float.IsFinite(glyph.ItalicShear) || !float.IsFinite(glyph.SyntheticBoldScale) || glyph.SyntheticBoldScale <= 0f ||
                !float.IsFinite(glyph.IntrinsicRotation.x) || !float.IsFinite(glyph.IntrinsicRotation.y) || !float.IsFinite(glyph.IntrinsicRotation.z) || !float.IsFinite(glyph.IntrinsicRotation.w) ||
                Quaternion.Dot(glyph.IntrinsicRotation, glyph.IntrinsicRotation) < .999f || Quaternion.Dot(glyph.IntrinsicRotation, glyph.IntrinsicRotation) > 1.001f)
                throw new ArgumentException("Shaped image placement and advance must be finite with a positive scale.");
            Identity = identity; Glyph = glyph; ShapedRevision = shapedRevision; ShapedGlyphIndex = shapedGlyphIndex;
            SourceStart = sourceStart; SourceLength = sourceLength;
        }
        /// <summary>Rejects stale requests before and after provider capture without reading native image buffers.</summary>
        public bool IsCurrentFor(ITextStudioShapedGlyphBackend backend) => backend != null && backend.ShapedGlyphRevision == ShapedRevision &&
            backend.TryGetShapedGlyph(ShapedGlyphIndex, out TextStudioBackendShapedGlyph glyph) && MatchesGlyph(in glyph);
        /// <summary>Checks that the complete shaped glyph placement and identity still match this request.</summary>
        public bool MatchesGlyph(in TextStudioBackendShapedGlyph glyph) => Glyph.TextUnitIndex == glyph.TextUnitIndex && Glyph.ClusterIndex == glyph.ClusterIndex &&
            Glyph.FontId == glyph.FontId && Glyph.GlyphIndex == glyph.GlyphIndex && Glyph.BaselineOrigin.Equals(glyph.BaselineOrigin) &&
            Glyph.Advance.Equals(glyph.Advance) && Glyph.GeometryScale.Equals(glyph.GeometryScale) && Glyph.IntrinsicRotation.Equals(glyph.IntrinsicRotation) &&
            Glyph.ItalicShear.Equals(glyph.ItalicShear) && Glyph.SyntheticBoldScale.Equals(glyph.SyntheticBoldScale) &&
            Glyph.Codepoint == glyph.Codepoint && Glyph.IsVisible == glyph.IsVisible && Glyph.IsWhitespace == glyph.IsWhitespace && Glyph.Decorations == glyph.Decorations;
        public bool Equals(TextStudioGlyphImageRequest other) => Identity.Equals(other.Identity) && ShapedRevision == other.ShapedRevision &&
            ShapedGlyphIndex == other.ShapedGlyphIndex && SourceStart == other.SourceStart && SourceLength == other.SourceLength && MatchesGlyph(other.Glyph);
        public override bool Equals(object obj) => obj is TextStudioGlyphImageRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Identity, ShapedRevision, ShapedGlyphIndex, SourceStart, SourceLength, Glyph.FontId, Glyph.TextUnitIndex, Glyph.ClusterIndex);
        static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
    }

    /// <summary>Captures caller-owned immutable pixels on the provider's required thread. A successful snapshot must match the entire request; no callback arrays or Texture objects are retained.</summary>
    public interface ITextStudioGlyphImageProvider
    {
        /// <summary>Captures owned pixels for the exact request and reports unavailable or invalid data through the status and diagnostic.</summary>
        TextStudioGlyphImageStatus CaptureGlyphImage(in TextStudioGlyphImageRequest request, out TextStudioGlyphImageSnapshot snapshot, out string diagnostic);
    }

    /// <summary>Optional exact shaped-image capability. Request metadata can be available even when native pixel extraction is unavailable.</summary>
    public interface ITextStudioShapedGlyphImageBackend : ITextStudioGlyphImageProvider
    {
        /// <summary>Gets an exact image request for a shaped glyph, or reports why no image request is ready.</summary>
        TextStudioGlyphImageStatus GetGlyphImageRequest(int shapedGlyphIndex, out TextStudioGlyphImageRequest request, out string diagnostic);
    }
}
