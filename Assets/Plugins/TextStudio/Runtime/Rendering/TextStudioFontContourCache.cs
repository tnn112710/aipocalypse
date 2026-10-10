using System;
using System.Collections.Generic;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Bounded immutable derived outlines owned by one prepared font. Paint and placement never enter this cache.</summary>
    internal sealed class TextStudioFontContourCache
    {
        const int MaximumEntries = 256;
        const long MaximumBytes = 4 * 1024 * 1024;
        const long MaximumMeasuredBytes = 2 * 1024 * 1024;
        readonly Dictionary<ulong, Entry> _strokes = new Dictionary<ulong, Entry>();
        readonly Dictionary<ulong, MeasuredEntry> _measured = new Dictionary<ulong, MeasuredEntry>();
        ICurveFontFace _source;
        long _bytes;
        long _measuredBytes;
        ulong _clock;
        internal long Bytes => _bytes + _measuredBytes;
        internal int PreparationCount { get; private set; }
        internal int MeasuredEntryCount => _measured.Count;

        internal MeasuredContourSet GetContours(ICurveFontFace face, uint glyphId, float tolerance, float nativeFaceOffsetEm = 0)
        {
            SynchronizeSource(face);
            return face == null || glyphId >= face.GlyphCount ? null : GetMeasured(face, glyphId, tolerance, nativeFaceOffsetEm: nativeFaceOffsetEm);
        }

        void SynchronizeSource(ICurveFontFace face)
        {
            if (ReferenceEquals(face, _source)) return;
            _strokes.Clear(); _measured.Clear(); _bytes = 0; _measuredBytes = 0;
            _source = face;
        }

        internal TextStudio2DSurfaceGeometry GetStroke(ICurveFontFace face, uint glyphId,
            in TextStudio2DStrokeSpec stroke, float unitScale, float tolerance, TextStudioPixelContourProjection? projection = null, float nativeFaceOffsetEm = 0)
        {
            SynchronizeSource(face);
            if (face == null || glyphId >= face.GlyphCount) return null;
            TextStudioDeterministicHash64 hash = default;
            ulong strokeKey = StrokeKey(glyphId, in stroke, unitScale, tolerance, nativeFaceOffsetEm);
            hash.Add(strokeKey); hash.Add(projection.HasValue);
            if (projection.HasValue) projection.Value.AddFingerprint(ref hash);
            ulong key = hash.Value;
            if (_strokes.TryGetValue(key, out Entry existing)) { existing.Used = ++_clock; return existing.Geometry; }
            if (projection.HasValue)
                foreach (Entry candidate in _strokes.Values)
                {
                    if (candidate.StrokeKey != strokeKey || !candidate.Geometry.PixelProjection.HasValue) continue;
                    TextStudioPixelContourProjection previous = candidate.Geometry.PixelProjection.Value;
                    Vector4 bounds;
                    try { bounds = previous.UnprojectBounds(candidate.Geometry.MetricBounds, stroke.Softness + 2); }
                    catch (InvalidOperationException) { continue; }
                    if (!projection.Value.EquivalentTo(in previous, new Vector2(bounds.x, bounds.y), new Vector2(bounds.z - bounds.x, bounds.w - bounds.y))) continue;
                    candidate.Used = ++_clock;
                    return candidate.Geometry;
                }
            MeasuredContourSet contours = GetMeasured(face, glyphId, tolerance, projection, nativeFaceOffsetEm);
            if (contours.Count == 0) return null;
            float[] pattern = new float[stroke.DashCount];
            for (int index = 0; index < pattern.Length; index++) pattern[index] = stroke.GetDash(index) * unitScale;
            Vector2[][] outline = StrokeGeometry.DashedStroke(contours, stroke.Width * unitScale,
                (stroke.Alignment + 1f) * .5f, (PathJoin)stroke.Join, stroke.MiterLimit, pattern,
                stroke.DashOffset * unitScale, (PathCap)stroke.DashCap, stroke.ContinueAcrossContours, projection.HasValue ? .1f : tolerance);
            TextStudio2DSurfaceGeometry geometry = new TextStudio2DSurfaceGeometry(outline, pixelProjection: projection);
            long bytes = geometry.Edges.LongLength * 16;
            while (_strokes.Count > 0 && (_strokes.Count >= MaximumEntries || _bytes + bytes > MaximumBytes))
            {
                ulong oldestKey = 0, oldest = ulong.MaxValue;
                foreach (KeyValuePair<ulong, Entry> pair in _strokes)
                    if (pair.Value.Used < oldest) { oldest = pair.Value.Used; oldestKey = pair.Key; }
                _bytes -= _strokes[oldestKey].Geometry.Edges.LongLength * 16;
                _strokes.Remove(oldestKey);
            }
            _strokes.Add(key, new Entry(geometry, strokeKey, ++_clock));
            _bytes += bytes;
            return geometry;
        }

        MeasuredContourSet GetMeasured(ICurveFontFace face, uint glyphId, float tolerance, TextStudioPixelContourProjection? projection = null, float nativeFaceOffsetEm = 0)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(glyphId); hash.Add(tolerance); hash.Add(projection.HasValue); hash.Add(nativeFaceOffsetEm);
            if (projection.HasValue) projection.Value.AddFingerprint(ref hash);
            ulong key = hash.Value;
            if (_measured.TryGetValue(key, out MeasuredEntry existing)) { existing.Used = ++_clock; return existing.Contours; }
            MeasuredContourSet contours;
            if (projection.HasValue)
            {
                MeasuredContourSet canonical = GetMeasured(face, glyphId, tolerance, nativeFaceOffsetEm: nativeFaceOffsetEm);
                Vector2[][] transformed = new Vector2[canonical.Count][];
                for (int contour = 0; contour < transformed.Length; contour++)
                {
                    ReadOnlySpan<Vector2> points = canonical.GetContour(contour);
                    Vector2[] target = transformed[contour] = new Vector2[points.Length];
                    for (int point = 0; point < points.Length; point++) target[point] = projection.Value.Project(points[point]);
                }
                contours = new MeasuredContourSet(transformed);
            }
            else if (nativeFaceOffsetEm != 0)
            {
                MeasuredContourSet canonical = GetMeasured(face, glyphId, tolerance);
                Vector2[][] points = new Vector2[canonical.Count][];
                for (int contour = 0; contour < points.Length; contour++) points[contour] = canonical.GetContour(contour).ToArray();
                contours = new MeasuredContourSet(PolygonGeometry.Offset(points, nativeFaceOffsetEm, tolerance));
            }
            else
            {
                CurveGlyph glyph = face.GetGlyph((int)glyphId);
                contours = new MeasuredContourSet(Flatten(glyph, tolerance));
                PreparationCount++;
            }
            if (contours.PayloadBytes > MaximumMeasuredBytes) return contours;
            while (_measured.Count > 0 && (_measured.Count >= MaximumEntries || _measuredBytes + contours.PayloadBytes > MaximumMeasuredBytes))
            {
                ulong oldestKey = 0, oldest = ulong.MaxValue;
                foreach (KeyValuePair<ulong, MeasuredEntry> pair in _measured)
                    if (pair.Value.Used < oldest) { oldest = pair.Value.Used; oldestKey = pair.Key; }
                _measuredBytes -= _measured[oldestKey].Contours.PayloadBytes;
                _measured.Remove(oldestKey);
            }
            _measured.Add(key, new MeasuredEntry(contours, ++_clock));
            _measuredBytes += contours.PayloadBytes;
            return contours;
        }

        internal static Vector2[][] Flatten(CurveGlyph glyph, float tolerance)
        {
            if (glyph == null) return Array.Empty<Vector2[]>();
            Vector2[][] contours = new Vector2[glyph.ContourCount][];
            for (int index = 0; index < contours.Length; index++) contours[index] = glyph.GetContour(index).Flatten(tolerance, 4096);
            // Normalize outer/counter winding before choosing the outside of each offset.
            return PolygonGeometry.Union(contours);
        }

        internal static ulong StrokeKey(uint glyph, in TextStudio2DStrokeSpec stroke, float scale, float tolerance, float nativeFaceOffsetEm = 0)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(glyph); hash.Add(stroke.Width); hash.Add(stroke.Alignment); hash.Add((byte)stroke.Join);
            hash.Add(nativeFaceOffsetEm); hash.Add(stroke.MiterLimit); hash.Add(stroke.DashCount); hash.Add(stroke.DashOffset);
            hash.Add((byte)stroke.DashCap); hash.Add(stroke.ContinueAcrossContours); hash.Add(scale); hash.Add(tolerance);
            for (int index = 0; index < stroke.DashCount; index++) hash.Add(stroke.GetDash(index));
            return hash.Value;
        }

        sealed class Entry
        {
            internal readonly TextStudio2DSurfaceGeometry Geometry;
            internal readonly ulong StrokeKey;
            internal ulong Used;
            internal Entry(TextStudio2DSurfaceGeometry geometry, ulong strokeKey, ulong used) { Geometry = geometry; StrokeKey = strokeKey; Used = used; }
        }

        sealed class MeasuredEntry
        {
            internal readonly MeasuredContourSet Contours;
            internal ulong Used;
            internal MeasuredEntry(MeasuredContourSet contours, ulong used) { Contours = contours; Used = used; }
        }
    }
}
