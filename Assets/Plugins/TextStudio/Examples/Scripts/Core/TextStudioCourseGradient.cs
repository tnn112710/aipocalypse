using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>A resolution-independent scrim that keeps live text legible over the course environment.</summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer)), AddComponentMenu("")]
    public sealed class TextStudioCourseGradient : MaskableGraphic
    {
        [SerializeField] Color _bottom = Color.clear;
        [SerializeField] Color _top = Color.black;

        public void Configure(Color bottom, Color top)
        {
            _bottom = bottom; _top = top; raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear(); Rect rect = GetPixelAdjustedRect();
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), _bottom * color, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), _top * color, Vector2.up);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), _top * color, Vector2.one);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), _bottom * color, Vector2.right);
            vertices.AddTriangle(0, 1, 2); vertices.AddTriangle(2, 3, 0);
        }
    }
}
