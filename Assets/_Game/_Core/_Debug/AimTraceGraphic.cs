using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Draws AimTrace's path: dots oldest (faint, small) to newest (bright, big) around the panel's centre, a ring and a
    // centre cross. Positions are in this graphic's local units.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AimTraceGraphic : MaskableGraphic
    {
        const int RingSegments = 48;

        [SerializeField, Min(1f)] float _dotSize = 4f;
        [SerializeField, Min(0.5f)] float _lineWidth = 1.5f;
        [SerializeField] Color _ringColor = new(1f, 0.85f, 0.3f, 0.6f);
        [SerializeField] Color _crossColor = new(1f, 1f, 1f, 0.35f);

        readonly List<Vector2> _points = new();
        float _ring;

        public List<Vector2> Points => _points;

        // Redraws with Points as they are now and a ring of `ring` units.
        public void Redraw(float ring)
        {
            _ring = ring;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Line(vh, new Vector2(-6f, 0f), new Vector2(6f, 0f), _crossColor);
            Line(vh, new Vector2(0f, -6f), new Vector2(0f, 6f), _crossColor);
            if (_ring > 0f)
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / RingSegments, b = (i + 1) * Mathf.PI * 2f / RingSegments;
                    Line(vh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _ring, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * _ring, _ringColor);
                }
            int count = _points.Count;
            for (int i = 0; i < count; i++)
            {
                float fresh = count > 1 ? (float)i / (count - 1) : 1f;
                Color c = color;
                c.a *= Mathf.Lerp(0.15f, 1f, fresh);
                Quad(vh, _points[i], _dotSize * (i == count - 1 ? 2f : Mathf.Lerp(0.6f, 1f, fresh)) * 0.5f, c);
            }
        }

        static void Quad(VertexHelper vh, Vector2 centre, float half, Color32 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(centre + new Vector2(-half, -half), c, Vector4.zero);
            vh.AddVert(centre + new Vector2(-half, half), c, Vector4.zero);
            vh.AddVert(centre + new Vector2(half, half), c, Vector4.zero);
            vh.AddVert(centre + new Vector2(half, -half), c, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        void Line(VertexHelper vh, Vector2 from, Vector2 to, Color32 c)
        {
            Vector2 side = (to - from).normalized;
            side = new Vector2(-side.y, side.x) * (_lineWidth * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(from - side, c, Vector4.zero);
            vh.AddVert(from + side, c, Vector4.zero);
            vh.AddVert(to + side, c, Vector4.zero);
            vh.AddVert(to - side, c, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
