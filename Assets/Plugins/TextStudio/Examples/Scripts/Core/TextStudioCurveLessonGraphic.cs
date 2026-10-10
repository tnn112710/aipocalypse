using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Instructional graph sampled from the same native curve asset used by the composed effect.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCurveLessonGraphic : MaskableGraphic
    {
        [SerializeField] TextStudioCurveProfile _curve;
        [SerializeField] float _progress;
        [SerializeField] TextStudioText _source;
        [SerializeField] EffectPlaybackAsset _playback;
        public void Configure(TextStudioCurveProfile curve, TextStudioText source = null, EffectPlaybackAsset playback = null) { _curve = curve; _source = source; _playback = playback; raycastTarget = false; SetVerticesDirty(); }
        void Update() { if (_source != null && _playback != null) SetProgress(_playback.Evaluate(_source.Preview.Time, 0, out _)); }
        public void SetProgress(float progress) { if (Mathf.Abs(_progress - progress) < .001f) return; _progress = progress; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (_curve == null) return;
            Rect rect = GetPixelAdjustedRect();
            Vector2 Point(float t) => new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), rect.yMin + rect.height * (.12f + _curve.Evaluate(t) * .68f));
            for (int i = 1; i <= 100; i++) AddLine(mesh, Point((i - 1) / 100f), Point(i / 100f), 3, color);
            Vector2 marker = Point(Mathf.Clamp01(_progress));
            AddLine(mesh, new Vector2(marker.x, rect.yMin), marker, 2, new Color(color.r, color.g, color.b, .4f));
            AddLine(mesh, marker - Vector2.right * 5, marker + Vector2.right * 5, 10, Color.white);
        }
        static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, tint, Vector2.zero); mesh.AddVert(a + normal, tint, Vector2.zero);
            mesh.AddVert(b + normal, tint, Vector2.zero); mesh.AddVert(b - normal, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
