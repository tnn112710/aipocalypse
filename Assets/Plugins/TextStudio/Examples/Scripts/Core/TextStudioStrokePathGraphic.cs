using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Shows the exact prepared Bezier paths, pressure and knot positions beside the rendered writing.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioStrokePathGraphic : MaskableGraphic
    {
        [SerializeField] WritingPhraseAsset _phrase;
        [SerializeField] bool _outlines;
        public void Configure(WritingPhraseAsset phrase, bool outlines) { _phrase = phrase; _outlines = outlines; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (_phrase == null) return;
            Rect source = _phrase.DesignBounds, area = GetPixelAdjustedRect();
            float scale = Mathf.Min(area.width / source.width, area.height / source.height) * .88f;
            Vector2 offset = area.center - source.center * scale;
            int count = _outlines ? _phrase.OutlineStrokeCount : _phrase.CenterlineStrokeCount;
            for (int i = 0; i < count; i++)
            {
                TextStudioWritingStroke stroke = _outlines ? _phrase.GetOutlineStroke(i) : _phrase.GetCenterlineStroke(i);
                Color tint = Color.Lerp(new Color(.12f, .38f, .34f), new Color(.77f, .34f, .19f), i / (float)Mathf.Max(1, count - 1));
                int segments = stroke.Closed ? stroke.KnotCount : stroke.KnotCount - 1;
                for (int j = 0; j < segments; j++)
                {
                    TextStudioWritingKnot a = stroke.GetKnot(j), b = stroke.GetKnot((j + 1) % stroke.KnotCount);
                    Vector2 last = a.Position * scale + offset;
                    for (int step = 1; step <= 12; step++)
                    {
                        float t = step / 12f, u = 1 - t;
                        Vector2 p = (u * u * u * a.Position + 3 * u * u * t * a.OutgoingControl + 3 * u * t * t * b.IncomingControl + t * t * t * b.Position) * scale + offset;
                        Segment(vh, last, p, Mathf.Lerp(a.Pressure, b.Pressure, t) * 1.5f + .65f, tint); last = p;
                    }
                    Vector2 knot = a.Position * scale + offset;
                    Segment(vh, knot - Vector2.right * 2.2f, knot + Vector2.right * 2.2f, 4.4f, new Color(.85f, .59f, .20f));
                }
            }
        }
        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = Vector2.Perpendicular((b - a).normalized) * (width * .5f);
            int n = vh.currentVertCount;
            vh.AddVert(a - normal, tint, Vector2.zero); vh.AddVert(a + normal, tint, Vector2.zero);
            vh.AddVert(b + normal, tint, Vector2.zero); vh.AddVert(b - normal, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
