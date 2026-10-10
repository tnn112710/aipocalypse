using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>
    /// Selects the outline the native field baker receives. MSDFgen orients contours by even-odd parity and cannot split
    /// self-intersections without Skia, so glyphs whose contours cross, overlap along an edge, or nest with the same winding
    /// (common in variable fonts) are replaced by their nonzero-winding union as closed polylines. Every other glyph keeps
    /// its exact curves and baker input. One instance serves one font face, because results are cached by glyph ID.
    /// </summary>
    internal sealed class TextStudioFieldContourResolver
    {
        /// <summary>Largest flattening tolerance. The shared flattener bounds each chord's control-point distance, so the
        /// polyline stays within this distance of the curve (at most half of it for quadratics, three quarters for cubics).</summary>
        internal const float MaximumToleranceEm = .0005f;
        /// <summary>Tighter bound for dense tiers: a quarter pixel at the field density, 0.000244 em at the 1,024 px/em maximum.</summary>
        internal const float MaximumTolerancePixels = .25f;
        /// <summary>Per-glyph edge and contour budgets enforced by the bundled baker.</summary>
        internal const int MaximumEdges = 65536, MaximumContours = 4096;
        /// <summary>Field pixel cache token for resolved glyphs. Change it whenever the resolved outline changes.</summary>
        internal const string CacheToken = "nonzero-union-1";
        // Far below one pixel at the maximum density; union output smaller than this is numerical residue.
        const double MinimumArea = 1e-10;

        readonly Dictionary<uint, Vector2[][]> _detected = new Dictionary<uint, Vector2[][]>();
        readonly Dictionary<(uint, float), CurveGlyph> _resolved = new Dictionary<(uint, float), CurveGlyph>();

        internal static float ToleranceFor(float pixelsPerEm) => Mathf.Min(MaximumToleranceEm, MaximumTolerancePixels / pixelsPerEm);

        /// <summary>Returns the glyph itself when the baker reads it correctly; otherwise its resolved outline and a cache token.</summary>
        internal CurveGlyph Resolve(uint glyphId, CurveGlyph glyph, float pixelsPerEm, out string preprocessing)
        {
            preprocessing = null;
            if (!_detected.TryGetValue(glyphId, out Vector2[][] detection))
            {
                // A glyph that cannot be flattened within the baker budget keeps its exact curves, as before resolution existed.
                detection = TryFlatten(glyph, MaximumToleranceEm, out Vector2[][] points) && RequiresResolution(points) ? points : null;
                _detected.Add(glyphId, detection);
            }
            if (detection == null) return glyph;
            float tolerance = ToleranceFor(pixelsPerEm);
            preprocessing = CacheToken + "/" + tolerance.ToString("R", CultureInfo.InvariantCulture);
            if (_resolved.TryGetValue((glyphId, tolerance), out CurveGlyph resolved)) return resolved;
            Vector2[][] flattened = detection;
            if (tolerance != MaximumToleranceEm && !TryFlatten(glyph, tolerance, out flattened)) throw BudgetExceeded(glyphId, tolerance);
            resolved = Union(glyph, flattened, glyphId, tolerance);
            _resolved.Add((glyphId, tolerance), resolved);
            return resolved;
        }

        /// <summary>True when the baker's even-odd contour orientation would disagree with the glyph's nonzero fill.</summary>
        internal static bool RequiresResolution(CurveGlyph glyph)
        {
            return glyph != null && TryFlatten(glyph, MaximumToleranceEm, out Vector2[][] points) && RequiresResolution(points);
        }

        /// <summary>
        /// Detects crossing or edge-overlapping boundaries, including a contour crossing itself, with a bounding-box sweep and
        /// exact orientation tests. Shared vertices are touches. A vertex inside another edge or a collinear overlap is resolved,
        /// because exact contact alone cannot distinguish a touch from a pass-through. Without crossings, contours are disjoint
        /// or nested, and even-odd equals nonzero exactly when each contour separates a filled side from an empty side.
        /// </summary>
        internal static bool RequiresResolution(IReadOnlyList<Vector2[]> contours)
        {
            int count = 0;
            for (int contour = 0; contour < contours.Count; contour++) if (contours[contour].Length >= 3) count += contours[contour].Length;
            if (count == 0) return false;
            int[] owner = new int[count], start = new int[count], order = new int[count];
            float[] left = new float[count];
            int segment = 0;
            for (int contour = 0; contour < contours.Count; contour++)
            {
                Vector2[] points = contours[contour];
                if (points.Length < 3) continue;
                for (int point = 0; point < points.Length; point++, segment++)
                {
                    owner[segment] = contour; start[segment] = point; order[segment] = segment;
                    left[segment] = Mathf.Min(points[point].x, points[(point + 1) % points.Length].x);
                }
            }
            Array.Sort(left, order);
            for (int i = 0; i < count; i++)
            {
                int first = order[i];
                Vector2[] firstPoints = contours[owner[first]];
                Vector2 a = firstPoints[start[first]], b = firstPoints[(start[first] + 1) % firstPoints.Length];
                float right = Mathf.Max(a.x, b.x), bottom = Mathf.Min(a.y, b.y), top = Mathf.Max(a.y, b.y);
                for (int j = i + 1; j < count && left[j] <= right; j++)
                {
                    int second = order[j];
                    Vector2[] secondPoints = contours[owner[second]];
                    Vector2 c = secondPoints[start[second]], d = secondPoints[(start[second] + 1) % secondPoints.Length];
                    if (Mathf.Max(c.y, d.y) < bottom || Mathf.Min(c.y, d.y) > top) continue;
                    if (owner[first] == owner[second])
                    {
                        int length = firstPoints.Length, step = start[second] - start[first];
                        if (step == 1 || step == 1 - length) { if (FoldsBack(a, b, d)) return true; continue; }
                        if (step == -1 || step == length - 1) { if (FoldsBack(c, d, b)) return true; continue; }
                    }
                    if (Intersects(a, b, c, d)) return true;
                }
            }

            Vector4[] bounds = new Vector4[contours.Count];
            for (int contour = 0; contour < contours.Count; contour++) bounds[contour] = BoundsOf(contours[contour]);
            for (int contour = 0; contour < contours.Count; contour++)
            {
                Vector2[] points = contours[contour];
                if (points.Length < 3) continue;
                double area = PolygonGeometry.SignedArea(points);
                if (Math.Abs(area) <= MinimumArea) continue;
                if (!TryGetEnclosingWinding(contours, bounds, contour, out int winding)) return true;
                if (winding != 0 && winding != (area > 0 ? -1 : 1)) return true;
            }
            return false;
        }

        static bool TryFlatten(CurveGlyph glyph, float tolerance, out Vector2[][] contours)
        {
            contours = new Vector2[glyph.ContourCount][];
            int points = 0;
            try
            {
                for (int index = 0; index < contours.Length; index++)
                {
                    contours[index] = glyph.GetContour(index).Flatten(tolerance, MaximumEdges);
                    points += contours[index].Length;
                    if (points > MaximumEdges) return false;
                }
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        static CurveGlyph Union(CurveGlyph glyph, Vector2[][] flattened, uint glyphId, float tolerance)
        {
            Vector2[][] union;
            try { union = PolygonGeometry.Union(flattened); }
            catch (InvalidOperationException) { throw BudgetExceeded(glyphId, tolerance); }
            List<Vector2[]> loops = new List<Vector2[]>(union.Length);
            List<Vector2> points = new List<Vector2>();
            double total = 0;
            int edges = 0;
            foreach (Vector2[] contour in union)
            {
                points.Clear();
                foreach (Vector2 point in contour)
                    if (points.Count == 0 || !SameVertex(points[points.Count - 1], point)) points.Add(point);
                while (points.Count > 1 && SameVertex(points[0], points[points.Count - 1])) points.RemoveAt(points.Count - 1);
                if (points.Count < 3) continue;
                double area = PolygonGeometry.SignedArea(points);
                if (Math.Abs(area) <= MinimumArea) continue;
                total += area;
                edges += points.Count;
                loops.Add(points.ToArray());
            }
            if (loops.Count > MaximumContours || edges > MaximumEdges) throw BudgetExceeded(glyphId, tolerance);
            // Union boundaries wind outer and inner loops oppositely. MSDFgen's orientContours keeps clockwise outer
            // boundaries (the TrueType convention), so emit that orientation and the baker leaves every loop as written.
            bool reverse = total > 0;
            CurveContour[] contours = new CurveContour[loops.Count];
            for (int index = 0; index < loops.Count; index++)
            {
                Vector2[] loop = loops[index];
                CurveSegment[] segments = new CurveSegment[loop.Length];
                for (int point = 0; point < loop.Length; point++)
                {
                    Vector2 from = loop[point], to = loop[(point + 1) % loop.Length];
                    segments[point] = reverse ? CurveSegment.Line(to, from) : CurveSegment.Line(from, to);
                }
                if (reverse) Array.Reverse(segments);
                contours[index] = new CurveContour(segments);
            }
            return new CurveGlyph(contours, glyph.Bounds, glyph.Advance);
        }

        static InvalidDataException BudgetExceeded(uint glyphId, float tolerance) => new InvalidDataException(FormattableString.Invariant(
            $"Glyph {glyphId} has overlapping contours whose resolved outline exceeds the baker's {MaximumEdges:N0} edges or {MaximumContours:N0} contours at a {tolerance:R} em tolerance. Prepare a lower density or a static font instance without overlaps."));

        static bool TryGetEnclosingWinding(IReadOnlyList<Vector2[]> contours, Vector4[] bounds, int contour, out int winding)
        {
            // Other contours never cross this one, so their winding is constant along it; skip vertices that touch them.
            foreach (Vector2 point in contours[contour])
            {
                winding = 0;
                bool touching = false;
                for (int other = 0; other < contours.Count && !touching; other++)
                {
                    Vector4 box = bounds[other];
                    if (other == contour || contours[other].Length < 3 || point.x < box.x || point.y < box.y || point.x > box.z || point.y > box.w) continue;
                    winding += Winding(contours[other], point, out touching);
                }
                if (!touching) return true;
            }
            winding = 0;
            return false;
        }

        static int Winding(Vector2[] polygon, Vector2 point, out bool onBoundary)
        {
            int winding = 0;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 a = polygon[index], b = polygon[(index + 1) % polygon.Length];
                double side = Orient(a, b, point);
                if (side == 0 && Between(a, b, point)) { onBoundary = true; return 0; }
                if (a.y <= point.y) { if (b.y > point.y && side > 0) winding++; }
                else if (b.y <= point.y && side < 0) winding--;
            }
            onBoundary = false;
            return winding;
        }

        static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double abC = Orient(a, b, c), abD = Orient(a, b, d), cdA = Orient(c, d, a), cdB = Orient(c, d, b);
            if (((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) && ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0))) return true;
            return (abC == 0 && Inside(a, b, c)) || (abD == 0 && Inside(a, b, d)) || (cdA == 0 && Inside(c, d, a)) || (cdB == 0 && Inside(c, d, b));
        }

        // Adjacent edges a-b and b-c share b; they overlap only when the second doubles back along the first.
        static bool FoldsBack(Vector2 a, Vector2 b, Vector2 c)
        {
            return Orient(a, b, c) == 0 && ((double)b.x - a.x) * ((double)c.x - b.x) + ((double)b.y - a.y) * ((double)c.y - b.y) < 0;
        }

        static double Orient(Vector2 a, Vector2 b, Vector2 c)
        {
            return ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        }

        // For a collinear point: strictly between the endpoints, so a shared vertex is not a contact.
        static bool Inside(Vector2 a, Vector2 b, Vector2 point) => !SameVertex(point, a) && !SameVertex(point, b) && Between(a, b, point);

        static bool Between(Vector2 a, Vector2 b, Vector2 point)
        {
            return point.x >= Mathf.Min(a.x, b.x) && point.x <= Mathf.Max(a.x, b.x) && point.y >= Mathf.Min(a.y, b.y) && point.y <= Mathf.Max(a.y, b.y);
        }

        static bool SameVertex(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;

        static Vector4 BoundsOf(Vector2[] points)
        {
            Vector4 bounds = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            foreach (Vector2 point in points)
                bounds = new Vector4(Mathf.Min(bounds.x, point.x), Mathf.Min(bounds.y, point.y), Mathf.Max(bounds.z, point.x), Mathf.Max(bounds.w, point.y));
            return bounds;
        }
    }
}
