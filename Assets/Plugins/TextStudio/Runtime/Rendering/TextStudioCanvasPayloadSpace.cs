using UnityEngine;

namespace TextStudio
{
    /// <summary>Preserves data carried in normal and tangent channels when Canvas batches UI geometry.</summary>
    internal static class TextStudioCanvasPayloadSpace
    {
        internal static Matrix4x4 Resolve(Transform transform, Canvas canvas)
        {
            if (canvas == null) return Matrix4x4.identity;
            // Invert the actual batching matrix: composing separately computed inverses compounds rounding at packed
            // glyph-index boundaries, even when the object and root share the same scale. Positions use the original mesh.
            Matrix4x4 matrix = canvas.rootCanvas.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            matrix.SetColumn(3, new Vector4(0, 0, 0, 1));
            return matrix.inverse;
        }

        internal static Vector4 Tangent(in Matrix4x4 matrix, Vector4 value)
        {
            Vector3 xyz = matrix.MultiplyVector(new Vector3(value.x, value.y, value.z));
            return new Vector4(xyz.x, xyz.y, xyz.z, value.w);
        }
    }
}
