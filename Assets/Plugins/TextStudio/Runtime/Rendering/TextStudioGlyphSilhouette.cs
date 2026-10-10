using System;
using System.Collections.Generic;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Derives one backing boundary before any paint, bevel or shadow is evaluated.</summary>
    internal static class TextStudioGlyphSilhouette
    {
        internal static TextStudio2DSurfaceGeometry Build(IReadOnlyList<Vector2[]> contours, float padding, float bridgeRadius, bool fillHoles, float tolerance)
        {
            if (!float.IsFinite(padding) || padding < 0 || !float.IsFinite(bridgeRadius) || bridgeRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(padding));
            Vector2[][] boundary = PolygonGeometry.Union(contours);
            // Morphological closing joins nearby letters without inventing a convex hull across distant words.
            if (bridgeRadius > 0)
                boundary = PolygonGeometry.Offset(PolygonGeometry.Offset(boundary, bridgeRadius, tolerance), -bridgeRadius, tolerance);
            if (padding > 0) boundary = PolygonGeometry.Offset(boundary, padding, tolerance);
            if (fillHoles)
            {
                List<Vector2[]> exterior = new List<Vector2[]>(boundary.Length);
                foreach (Vector2[] contour in boundary)
                    if (PolygonGeometry.SignedArea(contour) > 0) exterior.Add(contour);
                boundary = exterior.ToArray();
            }
            return new TextStudio2DSurfaceGeometry(boundary);
        }

        internal static void Append(List<Vector2[]> target, TextStudioFontAppearanceData font, uint glyph,
            in TextStudio2DTMPGlyphPlane plane, Matrix4x4 transform, float tolerance)
        {
            if (Mathf.Abs(plane.BottomLeft.z - plane.TopLeft.z) > .0001f ||
                Mathf.Abs(plane.BottomLeft.z - plane.TopRight.z) > .0001f ||
                Mathf.Abs(plane.BottomLeft.z - plane.BottomRight.z) > .0001f)
                throw new NotSupportedException("Glyph-derived backings require planar 2D glyph poses.");
            MeasuredContourSet contours = font.GetContours(glyph, tolerance, plane.NativeFaceOffsetEm);
            if (contours == null) throw new InvalidOperationException("The selected glyph has no prepared font contours.");
            float determinant = transform.m00 * transform.m11 - transform.m01 * transform.m10;
            Vector3 horizontal = plane.BottomRight - plane.BottomLeft, vertical = plane.TopLeft - plane.BottomLeft;
            bool reflected = determinant * (horizontal.x * vertical.y - horizontal.y * vertical.x) < 0;
            for (int index = 0; index < contours.Count; index++)
            {
                ReadOnlySpan<Vector2> source = contours.GetContour(index);
                Vector2[] points = new Vector2[source.Length];
                for (int point = 0; point < points.Length; point++)
                    points[reflected ? points.Length - 1 - point : point] = transform.MultiplyPoint3x4(plane.Position(source[point]));
                target.Add(points);
            }
        }

        internal static void HashPlane(ref TextStudioDeterministicHash64 hash, in TextStudio2DTMPGlyphPlane plane)
        {
            hash.Add(plane.BottomLeft); hash.Add(plane.TopLeft); hash.Add(plane.TopRight); hash.Add(plane.BottomRight);
            hash.Add(plane.EmMinimum); hash.Add(plane.EmSize); hash.Add(plane.NativeFaceOffsetEm);
        }
    }
}
