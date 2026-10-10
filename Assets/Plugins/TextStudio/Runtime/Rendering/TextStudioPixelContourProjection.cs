using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Canonical em to an output-pixel plane, with screen translation and rotation removed.</summary>
    internal readonly struct TextStudioPixelContourProjection
    {
        internal readonly Matrix4x4 Matrix;
        readonly Matrix4x4 _inverse;

        internal TextStudioPixelContourProjection(Matrix4x4 matrix)
        { Matrix = matrix; _inverse = matrix.inverse; }

        internal Vector2 Project(Vector2 point) => Transform(Matrix, point);
        internal Vector2 Unproject(Vector2 point) => Transform(_inverse, point);

        static Vector2 Transform(Matrix4x4 matrix, Vector2 point)
        {
            Vector4 value = matrix * new Vector4(point.x, point.y, 0, 1);
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.w) || value.w <= .000001f)
                throw new InvalidOperationException("The pixel outline crosses the camera's projection horizon. Move the text away from the camera plane.");
            return new Vector2(value.x, value.y) / value.w;
        }

        internal Vector4 UnprojectBounds(Vector4 bounds, float padding)
        {
            Vector2 a = Unproject(new Vector2(bounds.x - padding, bounds.y - padding));
            Vector2 b = Unproject(new Vector2(bounds.z + padding, bounds.y - padding));
            Vector2 c = Unproject(new Vector2(bounds.x - padding, bounds.w + padding));
            Vector2 d = Unproject(new Vector2(bounds.z + padding, bounds.w + padding));
            Vector2 minimum = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            Vector2 maximum = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            return new Vector4(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        internal void AddFingerprint(ref TextStudioDeterministicHash64 hash)
        {
            for (int row = 0; row < 4; row++)
            {
                if (row == 2) continue;
                hash.Add(Matrix[row, 0]); hash.Add(Matrix[row, 1]); hash.Add(Matrix[row, 3]);
            }
        }

        internal bool EquivalentTo(in TextStudioPixelContourProjection other, Vector2 minimum, Vector2 size)
        {
            float minimumW = float.PositiveInfinity, maximumNumeratorDelta = 0, maximumWDelta = 0, maximumOther = 0;
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
            {
                Vector2 point = minimum + Vector2.Scale(size, new Vector2(x, y));
                Vector4 homogeneous = new Vector4(point.x, point.y, 0, 1);
                Vector4 a = Matrix * homogeneous, b = other.Matrix * homogeneous;
                if (!float.IsFinite(a.x) || !float.IsFinite(a.y) || !float.IsFinite(a.w) ||
                    !float.IsFinite(b.x) || !float.IsFinite(b.y) || !float.IsFinite(b.w) || a.w <= .000001f || b.w <= .000001f) return false;
                minimumW = Mathf.Min(minimumW, a.w);
                maximumNumeratorDelta = Mathf.Max(maximumNumeratorDelta, new Vector2(a.x - b.x, a.y - b.y).magnitude);
                maximumWDelta = Mathf.Max(maximumWDelta, Mathf.Abs(a.w - b.w));
                maximumOther = Mathf.Max(maximumOther, new Vector2(b.x, b.y).magnitude / b.w);
            }
            // With positive homogeneous denominators, the projected rectangle is convex.
            // Bound the whole domain, including its interior, rather than sampling a grid.
            float error = (maximumNumeratorDelta + maximumOther * maximumWDelta) / minimumW;
            return float.IsFinite(error) && error <= .02f;
        }
    }
}
