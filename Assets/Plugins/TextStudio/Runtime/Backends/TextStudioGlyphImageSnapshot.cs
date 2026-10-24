using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Owned, immutable, bottom-row-first RGBA image in a glyph's baseline plane. Workers may read this object without a Texture or backend lifetime.</summary>
    public sealed class TextStudioGlyphImageSnapshot
    {
        public const int MaximumDimension = 1024;
        public const int MaximumPixelCount = 1024 * 1024;
        readonly Color32[] _pixels;
        public TextStudioGlyphImageRequest Request { get; }
        public int Width { get; }
        public int Height { get; }
        public Rect BaselineBounds { get; }
        public Vector2 PixelsPerEm => new Vector2(Width / BaselineBounds.width, Height / BaselineBounds.height);
        public TextStudioGlyphImageColorSpace ColorSpace { get; }
        public TextStudioGlyphImageAlpha Alpha { get; }
        /// <summary>Atlas/binding generation, deliberately excluded from image and silhouette content identity.</summary>
        public long BindingRevision { get; }
        public string ContentHash { get; }
        public string AlphaContentHash { get; }
        public ReadOnlySpan<Color32> Pixels => _pixels;
        public long PixelByteCount => (long)_pixels.Length * 4;
        TextStudioGlyphImageSnapshot(in TextStudioGlyphImageRequest request, int width, int height, Rect bounds,
            TextStudioGlyphImageColorSpace colorSpace, TextStudioGlyphImageAlpha alpha, Color32[] pixels, long bindingRevision,
            string contentHash = null, string alphaHash = null)
        {
            Request = request; Width = width; Height = height; BaselineBounds = bounds; ColorSpace = colorSpace; Alpha = alpha;
            _pixels = pixels; BindingRevision = bindingRevision;
            ContentHash = contentHash ?? ComputeHash(false); AlphaContentHash = alphaHash ?? ComputeHash(true);
        }
        /// <summary>Defensively copies complete RGBA bytes. Transparent pixels remain represented and no atlas rectangle is sampled implicitly.</summary>
        public static TextStudioGlyphImageSnapshot CopyFrom(in TextStudioGlyphImageRequest request, int width, int height, Rect baselineBounds,
            TextStudioGlyphImageColorSpace colorSpace, TextStudioGlyphImageAlpha alpha, ReadOnlySpan<Color32> pixels, long bindingRevision = 0)
        {
            if (!request.IsValid) throw new ArgumentException("An exact shaped image request is required.", nameof(request));
            if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
                (long)width * height > MaximumPixelCount || pixels.Length != (long)width * height)
                throw new ArgumentException("Owned images require complete RGBA pixels within the 1024 by 1024 pixel budget.");
            if (!float.IsFinite(baselineBounds.xMin) || !float.IsFinite(baselineBounds.yMin) || !float.IsFinite(baselineBounds.xMax) ||
                !float.IsFinite(baselineBounds.yMax) || baselineBounds.width <= 0 || baselineBounds.height <= 0 ||
                !float.IsFinite(width / baselineBounds.width) || !float.IsFinite(height / baselineBounds.height))
                throw new ArgumentException("Image baseline-plane bounds must be finite and have positive area.", nameof(baselineBounds));
            if (colorSpace < TextStudioGlyphImageColorSpace.Srgb || colorSpace > TextStudioGlyphImageColorSpace.Linear ||
                alpha < TextStudioGlyphImageAlpha.Straight || alpha > TextStudioGlyphImageAlpha.Premultiplied)
                throw new ArgumentOutOfRangeException(nameof(colorSpace));
            return new TextStudioGlyphImageSnapshot(in request, width, height, baselineBounds, colorSpace, alpha, pixels.ToArray(), bindingRevision);
        }
        /// <summary>Returns a caller-owned copy of all RGBA pixels, including transparent pixels.</summary>
        public Color32[] GetPixelsSnapshot() => (Color32[])_pixels.Clone();
        /// <summary>Reuses owned pixels for the same exact raster identity at another shaped occurrence or binding generation.</summary>
        public TextStudioGlyphImageSnapshot ForOccurrence(in TextStudioGlyphImageRequest request, long bindingRevision)
        {
            if (!request.IsValid || !Request.Identity.Equals(request.Identity)) throw new ArgumentException("Prepared pixels do not match this exact shaped identity.");
            return new TextStudioGlyphImageSnapshot(in request, Width, Height, BaselineBounds, ColorSpace, Alpha, _pixels, bindingRevision, ContentHash, AlphaContentHash);
        }
        string ComputeHash(bool alphaOnly)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(alphaOnly ? "TextStudio.ImageAlpha.1" : "TextStudio.ImageRGBA.1");
                writer.Write(Width); writer.Write(Height); writer.Write(BaselineBounds.x); writer.Write(BaselineBounds.y);
                writer.Write(BaselineBounds.width); writer.Write(BaselineBounds.height);
                if (!alphaOnly)
                {
                    var identity = Request.Identity; writer.Write(identity.FontContentHash); writer.Write(identity.FaceIndex); writer.Write(identity.GlyphIndex);
                    writer.Write(identity.ClusterText); writer.Write(identity.PaletteIdentity); writer.Write(identity.VariationIdentity);
                    writer.Write((int)ColorSpace); writer.Write((int)Alpha);
                }
                for (int i = 0; i < _pixels.Length; i++)
                {
                    Color32 p = _pixels[i]; if (!alphaOnly) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); } writer.Write(p.a);
                }
                writer.Flush(); using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.GetBuffer(), 0, (int)stream.Length)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
