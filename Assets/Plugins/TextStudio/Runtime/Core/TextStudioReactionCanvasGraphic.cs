using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    /// <summary>Draws a retained native text mesh through normal Canvas masking and hierarchy order.</summary>
    [AddComponentMenu("")]
    public sealed class TextStudioReactionCanvasGraphic : MaskableGraphic
    {
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        Mesh _source;
        Texture _sourceTexture;
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Vector4> _tangents = new List<Vector4>();
        readonly List<Vector4> _uv0 = new List<Vector4>();
        readonly List<Vector4> _uv1 = new List<Vector4>();
        readonly List<Vector4> _uv2 = new List<Vector4>();
        readonly List<Vector4> _uv3 = new List<Vector4>();
        readonly List<Color32> _colors = new List<Color32>();
        readonly List<int> _triangles = new List<int>();

        /// <summary>Assigns the retained text mesh, disables raycast interception, and marks Canvas geometry dirty.</summary>
        public void SetSource(Mesh mesh)
        {
            _source = mesh;
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>Assigns the retained source material and updates the Canvas texture binding when it changes.</summary>
        public void SetSourceMaterial(Material source)
        {
            if (material != source) material = source;
            Texture texture = source != null && source.HasProperty(MainTextureId)
                ? source.GetTexture(MainTextureId)
                : null;
            if (_sourceTexture == texture) return;
            _sourceTexture = texture;
            SetMaterialDirty();
        }

        public override Texture mainTexture =>
            _sourceTexture != null ? _sourceTexture : base.mainTexture;

        /// <summary>Copies the retained mesh's vertices, attributes, colors, and triangles into Canvas geometry.</summary>
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_source == null) return;
            _source.GetVertices(_vertices);
            _source.GetNormals(_normals);
            _source.GetTangents(_tangents);
            _source.GetUVs(0, _uv0);
            _source.GetUVs(1, _uv1);
            _source.GetUVs(2, _uv2);
            _source.GetUVs(3, _uv3);
            _source.GetColors(_colors);
            _source.GetTriangles(_triangles, 0);
            for (int i = 0; i < _vertices.Count; i++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = _vertices[i];
                vertex.normal = i < _normals.Count ? _normals[i] : Vector3.back;
                vertex.tangent = i < _tangents.Count ? _tangents[i] :
                    new Vector4(1f, 0f, 0f, -1f);
                vertex.uv0 = i < _uv0.Count ? _uv0[i] : default;
                vertex.uv1 = i < _uv1.Count ? _uv1[i] : default;
                vertex.uv2 = i < _uv2.Count ? _uv2[i] : default;
                vertex.uv3 = i < _uv3.Count ? _uv3[i] : default;
                vertex.color = i < _colors.Count ? _colors[i] :
                    new Color32(255, 255, 255, 255);
                helper.AddVert(vertex);
            }
            for (int i = 0; i + 2 < _triangles.Count; i += 3)
                helper.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
        }
    }
}
