using System;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    /// <summary>An immutable color image with its actual mesh and atlas UVs, expressed in source em coordinates.</summary>
    public sealed class TextStudioColorSource
    {
        readonly Vector2[] _positions;
        readonly Vector2[] _uv;
        readonly int[] _triangles;

        public Texture Texture { get; }
        public TextStudioReadOnlyBuffer<Vector2> Positions => new TextStudioReadOnlyBuffer<Vector2>(_positions, _positions.Length);
        public TextStudioReadOnlyBuffer<Vector2> Uv => new TextStudioReadOnlyBuffer<Vector2>(_uv, _uv.Length);
        public TextStudioReadOnlyBuffer<int> Triangles => new TextStudioReadOnlyBuffer<int>(_triangles, _triangles.Length);
        public Rect Bounds { get; }
        public float AlphaThreshold { get; }
        public bool PremultipliedAlpha { get; }
        /// <summary>Backend content revision, in addition to Unity's texture update counter.</summary>
        public uint Revision { get; }
        public bool IsEmpty { get; }
        internal ulong Identity { get; }

        /// <summary>Creates a color-image source from validated, independently copied mesh positions, UVs, and triangles with alpha and revision metadata.</summary>
        public TextStudioColorSource(Texture texture, Vector2[] positions, Vector2[] uv, int[] triangles,
            float alphaThreshold = .5f, bool premultipliedAlpha = false, uint revision = 0)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (positions == null || uv == null || positions.Length != uv.Length)
                throw new ArgumentException("Color-source positions and UVs must have the same length.");
            if (triangles == null || triangles.Length % 3 != 0)
                throw new ArgumentException("Color-source indices must describe complete triangles.", nameof(triangles));
            if (!float.IsFinite(alphaThreshold) || alphaThreshold < 0 || alphaThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(alphaThreshold));
            _positions = (Vector2[])positions.Clone();
            _uv = (Vector2[])uv.Clone();
            _triangles = (int[])triangles.Clone();
            Texture = texture;
            AlphaThreshold = alphaThreshold;
            PremultipliedAlpha = premultipliedAlpha;
            Revision = revision;
            TextStudioDeterministicHash64 hash = default;
            hash.Add(texture.GetStableId());
            hash.Add(alphaThreshold);
            hash.Add(premultipliedAlpha);
            hash.Add(revision);
            hash.Add(positions.Length);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 position = positions[index];
                Vector2 coordinate = uv[index];
                if (!float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                    !float.IsFinite(coordinate.x) || !float.IsFinite(coordinate.y))
                    throw new ArgumentException("Color-source coordinates must be finite.");
                minimum = Vector2.Min(minimum, position);
                maximum = Vector2.Max(maximum, position);
                hash.Add(position.x); hash.Add(position.y);
                hash.Add(coordinate.x); hash.Add(coordinate.y);
            }
            hash.Add(triangles.Length);
            foreach (int index in triangles)
            {
                if ((uint)index >= positions.Length) throw new ArgumentOutOfRangeException(nameof(triangles));
                hash.Add(index);
            }
            Bounds = positions.Length > 0 ? Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y) : default;
            if (!float.IsFinite(Bounds.width) || !float.IsFinite(Bounds.height))
                throw new ArgumentException("Color-source bounds must be finite.", nameof(positions));
            IsEmpty = true;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector2 a = positions[triangles[index]], b = positions[triangles[index + 1]], c = positions[triangles[index + 2]];
                double area = ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
                if (area != 0) { IsEmpty = false; break; }
            }
            Identity = hash.Value;
        }

        internal Mesh CreateCaptureMesh()
        {
            Vector3[] vertices = new Vector3[_positions.Length];
            for (int index = 0; index < vertices.Length; index++) vertices[index] = _positions[index];
            Mesh mesh = new Mesh { name = "Text Studio Color Silhouette Source", hideFlags = HideFlags.HideAndDontSave };
            mesh.indexFormat = vertices.Length > ushort.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = _uv;
            mesh.triangles = _triangles;
            return mesh;
        }
    }
}
