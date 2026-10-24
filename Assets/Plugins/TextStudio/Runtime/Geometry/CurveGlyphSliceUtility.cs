using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Geometry
{
    /// <summary>Creates filled vector slices using the source outline and its exact normalized texture domain.</summary>
    public static class CurveGlyphSliceUtility
    {
        public static bool TryCreate(CurveGlyph source, Rect sourceBoundsEm, Vector4 sourceUv,
            Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight,
            out CurveGlyph result, out string error, float tolerance = 0.0025f)
        {
            result = null;
            error = null;
            if (source == null || sourceBoundsEm.width <= 0f || sourceBoundsEm.height <= 0f ||
                !Finite(sourceUv) || !Finite(bottomLeft) || !Finite(topLeft) || !Finite(topRight) ||
                !Finite(bottomRight) || !Finite(sourceBoundsEm.position) || !Finite(sourceBoundsEm.size) ||
                !Finite(tolerance) || tolerance <= 0f)
            {
                error = "A vector slice requires a finite source domain, target quad and positive finite tolerance.";
                return false;
            }
            Vector2 horizontal = bottomRight - bottomLeft;
            Vector2 vertical = topLeft - bottomLeft;
            float determinant = Cross(horizontal, vertical);
            if (!Finite(horizontal) || !Finite(vertical) || !Finite(determinant) ||
                (bottomLeft + horizontal + vertical - topRight).sqrMagnitude > 0.0000001f ||
                Mathf.Abs(determinant) <= 0.0000000001f)
            {
                error = "The vector slice target must be a nondegenerate affine quad.";
                return false;
            }
            float safeTolerance = Mathf.Max(0.000001f, tolerance);
            List<Vector2[]> loops = new List<Vector2[]>(source.ContourCount);
            for (int i = 0; i < source.ContourCount; i++)
            {
                CurveContour contour = source.GetContour(i);
                if (!contour.Closed || contour.Count == 0) { error = "A filled vector slice requires closed, nonempty contours."; return false; }
                for (int p = 0; p < contour.Count; p++)
                {
                    CurveSegment segment = contour[p];
                    if (Finite(segment.Start) && Finite(segment.End) && Finite(segment.Control1) && Finite(segment.Control2)) continue;
                    error = "A filled vector slice requires finite source outline points.";
                    return false;
                }
                Vector2[] points = contour.Flatten(safeTolerance);
                if (points.Length >= 3) loops.Add(points);
            }
            float firstX = sourceBoundsEm.x + sourceUv.x * sourceBoundsEm.width;
            float firstY = sourceBoundsEm.y + sourceUv.y * sourceBoundsEm.height;
            float lastX = sourceBoundsEm.x + sourceUv.z * sourceBoundsEm.width;
            float lastY = sourceBoundsEm.y + sourceUv.w * sourceBoundsEm.height;
            if (!Finite(firstX) || !Finite(firstY) || !Finite(lastX) || !Finite(lastY))
            { error = "The vector slice source domain exceeds finite coordinates."; return false; }
            bool column = firstX == lastX;
            bool row = firstY == lastY;
            if (column && row) { error = "A slice cannot collapse both source dimensions."; return false; }
            Vector2[][] filled;
            if (column || row)
            {
                filled = SweepCrossSection(loops, column, column ? firstX : firstY,
                    column ? firstY : firstX, column ? lastY : lastX, bottomLeft, topLeft, topRight, bottomRight);
            }
            else
            {
                Vector2[] clip =
                {
                    new Vector2(Mathf.Min(firstX, lastX), Mathf.Min(firstY, lastY)),
                    new Vector2(Mathf.Max(firstX, lastX), Mathf.Min(firstY, lastY)),
                    new Vector2(Mathf.Max(firstX, lastX), Mathf.Max(firstY, lastY)),
                    new Vector2(Mathf.Min(firstX, lastX), Mathf.Max(firstY, lastY))
                };
                filled = PolygonGeometry.Intersect(loops, new[] { clip });
                Dictionary<Vector2, Vector2> knownVertices = PolygonGeometry.CreateBooleanInputVertexLookup(loops);
                List<Crossing> firstColumn = CollectCrossings(loops, true, firstX);
                List<Crossing> lastColumn = CollectCrossings(loops, true, lastX);
                List<Crossing> firstRow = CollectCrossings(loops, false, firstY);
                List<Crossing> lastRow = CollectCrossings(loops, false, lastY);
                for (int i = 0; i < filled.Length; i++)
                    for (int p = 0; p < filled[i].Length; p++)
                    {
                        Vector2 rounded = filled[i][p];
                        Vector2 point = knownVertices.TryGetValue(rounded, out Vector2 original) ? original : rounded;
                        bool onFirstX = PolygonGeometry.BooleanCoordinateMatchesInput(rounded.x, firstX);
                        bool onLastX = PolygonGeometry.BooleanCoordinateMatchesInput(rounded.x, lastX);
                        bool onFirstY = PolygonGeometry.BooleanCoordinateMatchesInput(rounded.y, firstY);
                        bool onLastY = PolygonGeometry.BooleanCoordinateMatchesInput(rounded.y, lastY);
                        if (onFirstX) point.y = RestoreSectionCoordinate(rounded.y, point.y, firstColumn);
                        if (onLastX) point.y = RestoreSectionCoordinate(rounded.y, point.y, lastColumn);
                        if (onFirstY) point.x = RestoreSectionCoordinate(rounded.x, point.x, firstRow);
                        if (onLastY) point.x = RestoreSectionCoordinate(rounded.x, point.x, lastRow);
                        // Clip corners remain on both authoritative planes even if a
                        // rounded source-edge intersection shares their grid identity.
                        if (onFirstX) point.x = firstX;
                        if (onLastX) point.x = lastX;
                        if (onFirstY) point.y = firstY;
                        if (onLastY) point.y = lastY;
                        filled[i][p] = MapTarget((point.x - firstX) / (lastX - firstX),
                            (point.y - firstY) / (lastY - firstY), bottomLeft, topLeft, topRight, bottomRight);
                    }
            }
            Vector2[][] mappedInputs = filled;
            filled = PolygonGeometry.Union(mappedInputs);
            PolygonGeometry.RestoreBooleanInputVertices(filled, mappedInputs);
            if (filled.Length == 0) { error = "The source slice contains no filled outline."; return false; }
            CurveContour[] curves = new CurveContour[filled.Length];
            Bounds bounds = new Bounds(new Vector3(filled[0][0].x, filled[0][0].y, 0f), Vector3.zero);
            for (int i = 0; i < filled.Length; i++)
            {
                Vector2[] points = filled[i];
                CurveSegment[] segments = new CurveSegment[points.Length];
                for (int p = 0; p < points.Length; p++)
                {
                    segments[p] = CurveSegment.Line(points[p], points[(p + 1) % points.Length]);
                    bounds.Encapsulate(new Vector3(points[p].x, points[p].y, 0f));
                }
                curves[i] = new CurveContour(segments);
            }
            result = new CurveGlyph(curves, bounds, bounds.size.x);
            return true;
        }

        static Vector2[][] SweepCrossSection(IReadOnlyList<Vector2[]> loops, bool column, float coordinate,
            float first, float last, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
        {
            List<Crossing> crossings = CollectCrossings(loops, column, coordinate);
            List<Vector2[]> result = new List<Vector2[]>();
            int winding = 0;
            float start = 0f;
            for (int i = 0; i < crossings.Count;)
            {
                float position = crossings[i].Position;
                int change = 0;
                do { change += crossings[i++].Winding; }
                while (i < crossings.Count && Mathf.Abs(crossings[i].Position - position) <= 0.0000001f);
                int next = winding + change;
                if (winding == 0 && next != 0) start = position;
                if (winding != 0 && next == 0)
                {
                    float low = Mathf.Max(start, Mathf.Min(first, last));
                    float high = Mathf.Min(position, Mathf.Max(first, last));
                    if (high > low)
                    {
                        float a = (low - first) / (last - first);
                        float b = (high - first) / (last - first);
                        result.Add(column
                            ? new[] { MapTarget(0f, a, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(1f, a, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(1f, b, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(0f, b, bottomLeft, topLeft, topRight, bottomRight) }
                            : new[] { MapTarget(a, 0f, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(b, 0f, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(b, 1f, bottomLeft, topLeft, topRight, bottomRight),
                                MapTarget(a, 1f, bottomLeft, topLeft, topRight, bottomRight) });
                    }
                }
                winding = next;
            }
            return result.ToArray();
        }

        static List<Crossing> CollectCrossings(IReadOnlyList<Vector2[]> loops, bool column, float coordinate)
        {
            List<Crossing> crossings = new List<Crossing>();
            for (int i = 0; i < loops.Count; i++)
            {
                Vector2[] points = loops[i];
                for (int p = 0; p < points.Length; p++)
                {
                    Vector2 a = points[p];
                    Vector2 b = points[(p + 1) % points.Length];
                    float axisA = column ? a.x : a.y;
                    float axisB = column ? b.x : b.y;
                    if (axisA == axisB || coordinate < Mathf.Min(axisA, axisB) || coordinate >= Mathf.Max(axisA, axisB)) continue;
                    float t = (coordinate - axisA) / (axisB - axisA);
                    float position = column ? Mathf.LerpUnclamped(a.y, b.y, t) : Mathf.LerpUnclamped(a.x, b.x, t);
                    crossings.Add(new Crossing(position, axisB > axisA ? 1 : -1));
                }
            }
            crossings.Sort((a, b) => a.Position.CompareTo(b.Position));
            return crossings;
        }

        static float RestoreSectionCoordinate(float rounded, float current, List<Crossing> crossings)
        {
            foreach (Crossing crossing in crossings)
                if (PolygonGeometry.BooleanCoordinateMatchesInput(rounded, crossing.Position)) return crossing.Position;
            return current;
        }

        static Vector2 MapTarget(float x, float y, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
        {
            // An affine quad has the same edge interpolation on both adjacent slices.
            // Retain the supplied shared edge instead of reconstructing it from a
            // different pen origin, which can move it by one float ULP.
            Vector2 left = InterpolateExactEndpoints(bottomLeft, topLeft, y);
            Vector2 right = InterpolateExactEndpoints(bottomRight, topRight, y);
            return InterpolateExactEndpoints(left, right, x);
        }

        static Vector2 InterpolateExactEndpoints(Vector2 first, Vector2 last, float parameter) =>
            parameter == 0f ? first : parameter == 1f ? last : Vector2.LerpUnclamped(first, last, parameter);

        readonly struct Crossing
        {
            internal readonly float Position;
            internal readonly int Winding;
            internal Crossing(float position, int winding) { Position = position; Winding = winding; }
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
