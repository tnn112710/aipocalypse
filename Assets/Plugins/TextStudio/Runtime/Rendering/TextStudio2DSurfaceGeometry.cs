using System;
using System.Collections.Generic;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio
{
    internal readonly struct TextStudio2DNativeInkSource
    {
        internal const int RecordStride = 4;
        internal readonly Vector4 Metadata;
        internal readonly Vector4 BottomCorners;
        internal readonly Vector4 TopCorners;
        internal readonly Vector4 FontPlane;

        internal TextStudio2DNativeInkSource(int edgeStart, int edgeCount, in TextStudio2DTMPGlyphPlane plane, float clearanceEm)
        {
            Metadata = new Vector4(edgeStart, edgeCount, plane.WritingSample, clearanceEm);
            BottomCorners = new Vector4(plane.BottomLeft.x, plane.BottomLeft.y, plane.BottomRight.x, plane.BottomRight.y);
            TopCorners = new Vector4(plane.TopLeft.x, plane.TopLeft.y, plane.TopRight.x, plane.TopRight.y);
            FontPlane = new Vector4(plane.EmMinimum.x, plane.EmMinimum.y, plane.EmSize.x, plane.EmSize.y);
        }

        internal TextStudio2DNativeInkSource(int edgeStart, int edgeCount)
        {
            Metadata = new Vector4(edgeStart, edgeCount, 0, 0);
            BottomCorners = TopCorners = FontPlane = Vector4.zero;
        }
    }

    internal readonly struct TextStudio2DSurfaceBand
    {
        internal readonly Rect Bounds;
        internal readonly int Line;
        internal readonly int LogicalStart;
        internal readonly int LogicalEnd;
        internal TextStudio2DSurfaceBand(Rect bounds, int line, int start, int end)
        {
            Bounds = bounds; Line = line; LogicalStart = start; LogicalEnd = end;
        }
    }

    /// <summary>A union boundary or partitioned native glyph contours, including disjoint components and holes.</summary>
    internal sealed class TextStudio2DSurfaceGeometry
    {
        internal const int MaximumEdges = 2048;
        internal readonly Vector4[] Edges;
        internal readonly Vector4 Bounds;
        internal readonly Vector4 MetricBounds;
        internal readonly TextStudioPixelContourProjection? PixelProjection;
        internal readonly TextStudio2DNativeInkSource[] NativeInkSources;
        internal readonly ulong Fingerprint;
        /// <summary>True when <see cref="Edges"/> starts with the <see cref="BandEdgeCount"/> edges that meet <see cref="InkBand"/>.</summary>
        internal readonly bool HasInkBand;
        /// <summary>Leading edges whose vertical extent meets <see cref="InkBand"/>; every later edge lies wholly above or below it.</summary>
        internal readonly int BandEdgeCount;
        /// <summary>Bottom and top of the band a static ink mask orders first.</summary>
        internal readonly Vector2 InkBand;

        /// <param name="inkBand">
        /// Bottom and top of a horizontal band. A static ink mask stores the edges that meet it first, so the lean line program
        /// measures only those for pixels whose antialiasing and treatment reach stay inside the band. Edge order changes no
        /// distance or winding.
        /// </param>
        internal TextStudio2DSurfaceGeometry(IReadOnlyList<Vector2[]> contours, Vector4? coordinateBounds = null,
            TextStudioPixelContourProjection? pixelProjection = null, IReadOnlyList<TextStudio2DNativeInkSource> nativeInkSources = null,
            Vector2? inkBand = null)
        {
            int count = CountEdges(contours);
            if (count > MaximumEdges) throw new InvalidOperationException("This surface exceeds the 2048-edge geometry budget. Reduce corner detail or split the selected range.");
            Vector4 bounds = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2[] contour in contours)
            {
                foreach (Vector2 point in contour)
                {
                    bounds.x = Mathf.Min(bounds.x, point.x); bounds.y = Mathf.Min(bounds.y, point.y);
                    bounds.z = Mathf.Max(bounds.z, point.x); bounds.w = Mathf.Max(bounds.w, point.y);
                }
            }
            if (inkBand.HasValue && (pixelProjection.HasValue || nativeInkSources?.Count > 0 || !(inkBand.Value.y >= inkBand.Value.x)))
                throw new ArgumentException("An ink band orders the edges of a static local mask and needs a valid vertical range.");
            PixelProjection = pixelProjection;
            if (pixelProjection.HasValue && nativeInkSources?.Count > 0)
                throw new ArgumentException("Native ink sources use the glyph's local plane, independently of pixel contour projection.");
            if (nativeInkSources?.Count > count)
                throw new ArgumentException("Native ink sources must partition the surface's edges without empty ranges.");
            NativeInkSources = nativeInkSources?.Count > 0 ? new TextStudio2DNativeInkSource[nativeInkSources.Count] : Array.Empty<TextStudio2DNativeInkSource>();
            for (int index = 0; index < NativeInkSources.Length; index++) NativeInkSources[index] = nativeInkSources[index];
            if (NativeInkSources.Length > 0)
            {
                Vector4 nativeBounds = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                foreach (TextStudio2DNativeInkSource source in NativeInkSources)
                {
                    if (source.FontPlane.z <= 0 || source.FontPlane.w <= 0)
                    {
                        IncludePoint(ref nativeBounds, new Vector2(bounds.x, bounds.y));
                        IncludePoint(ref nativeBounds, new Vector2(bounds.z, bounds.w));
                        continue;
                    }
                    Vector2 clearance = new Vector2(source.Metadata.w / source.FontPlane.z, source.Metadata.w / source.FontPlane.w);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float u = (corner & 1) == 0 ? -clearance.x : 1 + clearance.x;
                        float v = (corner & 2) == 0 ? -clearance.y : 1 + clearance.y;
                        Vector2 bottom = Vector2.LerpUnclamped(new Vector2(source.BottomCorners.x, source.BottomCorners.y), new Vector2(source.BottomCorners.z, source.BottomCorners.w), u);
                        Vector2 top = Vector2.LerpUnclamped(new Vector2(source.TopCorners.x, source.TopCorners.y), new Vector2(source.TopCorners.z, source.TopCorners.w), u);
                        IncludePoint(ref nativeBounds, Vector2.LerpUnclamped(bottom, top, v));
                    }
                }
                bounds = nativeBounds;
            }
            MetricBounds = count > 0 ? bounds : Vector4.zero;
            Bounds = coordinateBounds ?? (pixelProjection.HasValue && count > 0 ? pixelProjection.Value.UnprojectBounds(bounds, 0) : MetricBounds);
            Edges = new Vector4[count];
            int offset = 0;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(Bounds.x); hash.Add(Bounds.y); hash.Add(Bounds.z); hash.Add(Bounds.w);
            hash.Add(pixelProjection.HasValue);
            if (pixelProjection.HasValue) pixelProjection.Value.AddFingerprint(ref hash);
            int nextEdge = 0;
            hash.Add(NativeInkSources.Length);
            foreach (TextStudio2DNativeInkSource source in NativeInkSources)
            {
                if (source.Metadata.x != nextEdge || source.Metadata.y <= 0 || source.Metadata.x + source.Metadata.y > count)
                    throw new ArgumentException("Native ink sources must partition the surface's edges without overlap or empty ranges.");
                nextEdge += (int)source.Metadata.y;
                AddVector(ref hash, source.Metadata); AddVector(ref hash, source.BottomCorners);
                AddVector(ref hash, source.TopCorners); AddVector(ref hash, source.FontPlane);
            }
            if (NativeInkSources.Length > 0 && nextEdge != count)
                throw new ArgumentException("Native ink sources must cover every edge of their surface.");
            foreach (Vector2[] contour in contours)
                for (int index = 0; index < contour.Length; index++)
                {
                    Vector2 a = contour[index], b = contour[(index + 1) % contour.Length];
                    Edges[offset++] = new Vector4(a.x, a.y, b.x, b.y);
                }
            if (inkBand.HasValue)
            {
                HasInkBand = true;
                InkBand = inkBand.Value;
                BandEdgeCount = OrderBandEdgesFirst(Edges, InkBand);
                hash.Add(InkBand.x); hash.Add(InkBand.y); hash.Add(BandEdgeCount);
            }
            foreach (Vector4 edge in Edges)
            {
                hash.Add(edge.x); hash.Add(edge.y); hash.Add(edge.z); hash.Add(edge.w);
            }
            Fingerprint = hash.Value;
        }

        internal static int CountEdges(IReadOnlyList<Vector2[]> contours)
        {
            int count = 0;
            foreach (Vector2[] contour in contours) count = checked(count + contour.Length);
            return count;
        }

        // Stable partition: edges whose vertical extent meets the band keep their order ahead of all others.
        static int OrderBandEdgesFirst(Vector4[] edges, Vector2 band)
        {
            Vector4[] outside = null;
            int inside = 0, outsideCount = 0;
            for (int index = 0; index < edges.Length; index++)
            {
                Vector4 edge = edges[index];
                if (Mathf.Min(edge.y, edge.w) <= band.y && Mathf.Max(edge.y, edge.w) >= band.x) edges[inside++] = edge;
                else (outside ??= new Vector4[edges.Length - index])[outsideCount++] = edge;
            }
            if (outsideCount > 0) Array.Copy(outside, 0, edges, inside, outsideCount);
            return inside;
        }

        static void IncludePoint(ref Vector4 bounds, Vector2 point)
        {
            bounds.x = Mathf.Min(bounds.x, point.x); bounds.y = Mathf.Min(bounds.y, point.y);
            bounds.z = Mathf.Max(bounds.z, point.x); bounds.w = Mathf.Max(bounds.w, point.y);
        }

        static void AddVector(ref TextStudioDeterministicHash64 hash, Vector4 value)
        {
            hash.Add(value.x); hash.Add(value.y); hash.Add(value.z); hash.Add(value.w);
        }

        internal static TextStudio2DSurfaceGeometry Connected(IReadOnlyList<TextStudio2DSurfaceBand> bands,
            in TextStudioPlanView plan, bool acrossLineBreaks, float radius, float tolerance)
        {
            List<Vector2[]> polygons = new List<Vector2[]>(bands.Count * 2);
            for (int index = 0; index < bands.Count; index++)
            {
                TextStudio2DSurfaceBand band = bands[index];
                if (band.Bounds.width <= 0 || band.Bounds.height <= 0) continue;
                polygons.Add(Rectangle(band.Bounds));
                for (int previous = 0; previous < index; previous++)
                {
                    TextStudio2DSurfaceBand other = bands[previous];
                    if (Mathf.Abs(band.Line - other.Line) != 1) continue;
                    int start = Math.Min(other.LogicalEnd, band.LogicalEnd);
                    int end = Math.Max(other.LogicalStart, band.LogicalStart);
                    bool separated = false;
                    for (int unit = start; !acrossLineBreaks && unit <= end && unit < plan.TextUnitCount; unit++)
                        if (plan.IsLineBreakUnit(unit)) { separated = true; break; }
                    if (separated) continue;
                    float left = Mathf.Max(band.Bounds.xMin, other.Bounds.xMin);
                    float right = Mathf.Min(band.Bounds.xMax, other.Bounds.xMax);
                    if (right <= left) continue;
                    Rect upper = band.Bounds.center.y > other.Bounds.center.y ? band.Bounds : other.Bounds;
                    Rect lower = band.Bounds.center.y > other.Bounds.center.y ? other.Bounds : band.Bounds;
                    if (upper.yMin > lower.yMax)
                    {
                        // Rect stores a minimum and a size. Reconstructing its far edge
                        // can round away from the adjoining band, leaving an internal
                        // boundary for strokes and bevels. Union overlapping interiors.
                        float overlap = Mathf.Min(Mathf.Min(lower.height, upper.height) * .25f,
                            Mathf.Max(.00001f, Mathf.Max(Mathf.Abs(lower.yMax), Mathf.Abs(upper.yMin)) * .000001f));
                        polygons.Add(Rectangle(Rect.MinMaxRect(left, lower.yMax - overlap, right, upper.yMin + overlap)));
                    }
                }
            }
            Vector2[][] union = PolygonGeometry.Union(polygons);
            return new TextStudio2DSurfaceGeometry(radius > 0 ? PolygonGeometry.RoundCorners(union, radius, tolerance) : union);
        }

        internal float SignedDistance(Vector2 point)
        {
            if (NativeInkSources.Length > 0)
                throw new NotSupportedException("Native ink surfaces require the active GPU Writing state.");
            if (PixelProjection.HasValue) point = PixelProjection.Value.Project(point);
            float distanceSquared = float.PositiveInfinity;
            int winding = 0;
            foreach (Vector4 edge in Edges)
            {
                Vector2 a = new Vector2(edge.x, edge.y), b = new Vector2(edge.z, edge.w);
                Vector2 direction = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, direction) / Mathf.Max(1e-16f, direction.sqrMagnitude));
                distanceSquared = Mathf.Min(distanceSquared, (point - a - direction * t).sqrMagnitude);
                float cross = direction.x * (point.y - a.y) - direction.y * (point.x - a.x);
                if (a.y <= point.y && b.y > point.y && cross > 0) winding++;
                if (a.y > point.y && b.y <= point.y && cross < 0) winding--;
            }
            return Mathf.Sqrt(distanceSquared) * (winding == 0 ? 1 : -1);
        }

        static Vector2[] Rectangle(Rect bounds) => new[] {
            new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMax), new Vector2(bounds.xMin, bounds.yMax)
        };
    }
}
