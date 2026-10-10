using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One text's field leases. Distance values are decoded into the native field's units, not its texture.</summary>
    internal sealed class TextStudioPreparedFieldBinding
    {
        readonly TextStudioPreparedFieldPages _pages;
        readonly List<Lease> _leases = new List<Lease>();
        readonly Dictionary<GlyphKey, GlyphMapping> _mappings = new Dictionary<GlyphKey, GlyphMapping>();
        readonly List<GlyphKey> _unused = new List<GlyphKey>();
        readonly HashSet<Texture2D> _failedPages = new HashSet<Texture2D>();
        uint _generation;
        internal bool HasPendingUpgrade { get; private set; }
        internal bool HasMissingCoverage { get; private set; }
        internal TextStudioPreparedFieldBinding(TextStudioPreparedFieldPages pages) { _pages = pages; }

        internal void BeginUpdate()
        {
            if (_generation == uint.MaxValue) { _mappings.Clear(); _generation = 0; }
            _generation++;
            HasPendingUpgrade = HasMissingCoverage = false;
            _failedPages.Clear();
        }

        internal bool TryMap(in TextStudioVisualGlyph glyph, TextStudioFontAppearanceData data, int selectedTier,
            out Vector4 transform, out Vector4 bounds, out Vector4 decode)
        {
            transform = bounds = decode = Vector4.zero;
            if (selectedTier < 0 || data == null || !glyph.IsVisible ||
                glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph || glyph.DistanceFieldScale <= 0f || glyph.FieldPixelsPerEm <= 0f ||
                !data.IsPreparedDataValid) return false;
            GlyphKey key = new GlyphKey(glyph.FontIdentity, glyph.GlyphIndex);
            if (MapTier(in glyph, data, selectedTier, out transform, out bounds, out decode))
            { _mappings[key] = new GlyphMapping(data, selectedTier, _generation); return true; }
            // Keep the last successfully mapped tier even after several unsuccessful promotion requests.
            if (_mappings.TryGetValue(key, out GlyphMapping previous) && previous.Data == data && previous.Revision == data.Revision &&
                previous.Tier != selectedTier && MapTier(in glyph, data, previous.Tier, out transform, out bounds, out decode))
            { _mappings[key] = new GlyphMapping(data, previous.Tier, _generation); return true; }
            for (int tier = 0; tier < data.TierCount; tier++)
            {
                if (tier == selectedTier || !MapTier(in glyph, data, tier, out transform, out bounds, out decode)) continue;
                _mappings[key] = new GlyphMapping(data, tier, _generation);
                return true;
            }
            return false;
        }

        bool MapTier(in TextStudioVisualGlyph glyph, TextStudioFontAppearanceData data, int tierIndex,
            out Vector4 transform, out Vector4 bounds, out Vector4 decode)
        {
            transform = bounds = decode = Vector4.zero;
            TextStudioPreparedFieldTier tier = data.GetTier(tierIndex);
            if (!tier.TryGetGlyph(glyph.GlyphIndex, out TextStudioPreparedGlyphField field))
            { HasMissingCoverage = true; return false; }
            Texture2D page = tier.GetPage(field.Page);
            if (!TryMapCoordinates(glyph.AtlasToEm, field, page, out transform, out bounds)) return false;
            TextStudioPreparedFieldPages.Page lease = null;
            for (int index = 0; index < _leases.Count; index++)
            {
                Lease current = _leases[index];
                if (current.Page.Source != page) continue;
                lease = current.Page; _leases[index] = new Lease(lease, _generation); break;
            }
            if (lease == null)
            {
                if (_failedPages.Contains(page) || !_pages.TryAcquire(page, out lease))
                { _failedPages.Add(page); HasPendingUpgrade = true; return false; }
                _leases.Add(new Lease(lease, _generation));
            }
            float pixelsPerEm = glyph.FieldPixelsPerEm;
            // TMP encodes the full exterior-to-interior interval across twice its gradient scale.
            decode = new Vector4(lease.Slice + 1, tier.DistanceRangeEm * pixelsPerEm / (2f * glyph.DistanceFieldScale),
                tier.PixelsPerEm, tier.SafeDistanceEm);
            return true;
        }

        internal void EndUpdate()
        {
            for (int index = _leases.Count - 1; index >= 0; index--)
            {
                if (_leases[index].Generation == _generation) continue;
                _pages.Release(_leases[index].Page); _leases.RemoveAt(index);
            }
            _unused.Clear();
            foreach (KeyValuePair<GlyphKey, GlyphMapping> item in _mappings)
                if (item.Value.Generation != _generation) _unused.Add(item.Key);
            foreach (GlyphKey key in _unused) _mappings.Remove(key);
            _unused.Clear();
        }

        internal static bool TryMapCoordinates(Vector4 atlasToEm, in TextStudioPreparedGlyphField field,
            Texture2D page, out Vector4 transform, out Vector4 bounds)
        {
            transform = bounds = Vector4.zero;
            if (page == null || !float.IsFinite(atlasToEm.x) || !float.IsFinite(atlasToEm.y) ||
                !float.IsFinite(atlasToEm.z) || !float.IsFinite(atlasToEm.w) || atlasToEm.x <= 0 || atlasToEm.y <= 0 ||
                field.EmBounds.width <= 0 || field.EmBounds.height <= 0) return false;
            Vector2 emScale = new Vector2(atlasToEm.x, atlasToEm.y);
            Vector2 emOffset = new Vector2(atlasToEm.z, atlasToEm.w);
            Vector2 pageScale = new Vector2(page.width, page.height) / TextStudioPreparedFieldPages.PageSize;
            Vector2 uvScale = Vector2.Scale(field.AtlasUv.size / field.EmBounds.size, pageScale);
            Vector2 uvOffset = Vector2.Scale(field.AtlasUv.position, pageScale) + Vector2.Scale(emOffset - field.EmBounds.position, uvScale);
            Vector2 uvAxis = Vector2.Scale(emScale, uvScale);
            transform = new Vector4(uvAxis.x, uvAxis.y, uvOffset.x, uvOffset.y);
            float inset = .5f / TextStudioPreparedFieldPages.PageSize;
            bounds = new Vector4(field.AtlasUv.xMin * pageScale.x + inset, field.AtlasUv.yMin * pageScale.y + inset,
                field.AtlasUv.xMax * pageScale.x - inset, field.AtlasUv.yMax * pageScale.y - inset);
            return true;
        }

        internal void Release()
        {
            for (int index = 0; index < _leases.Count; index++) _pages.Release(_leases[index].Page);
            _leases.Clear();
            _mappings.Clear(); _unused.Clear(); _failedPages.Clear();
        }

        readonly struct Lease
        {
            internal readonly TextStudioPreparedFieldPages.Page Page;
            internal readonly uint Generation;
            internal Lease(TextStudioPreparedFieldPages.Page page, uint generation) { Page = page; Generation = generation; }
        }
        readonly struct GlyphKey : IEquatable<GlyphKey>
        {
            readonly ulong _font;
            readonly uint _glyph;
            internal GlyphKey(ulong font, uint glyph) { _font = font; _glyph = glyph; }
            public bool Equals(GlyphKey other) => _font == other._font && _glyph == other._glyph;
            public override bool Equals(object obj) => obj is GlyphKey other && Equals(other);
            public override int GetHashCode() => unchecked(_font.GetHashCode() * 397 ^ (int)_glyph);
        }
        readonly struct GlyphMapping
        {
            internal readonly TextStudioFontAppearanceData Data;
            internal readonly uint Revision, Generation;
            internal readonly int Tier;
            internal GlyphMapping(TextStudioFontAppearanceData data, int tier, uint generation)
            { Data = data; Revision = data.Revision; Tier = tier; Generation = generation; }
        }
    }
}
