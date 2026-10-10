using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Maps canonical font em coordinates through the backend's final quad, including atlas padding and deformation.</summary>
    internal readonly struct TextStudio2DTMPGlyphPlane
    {
        internal readonly Vector3 BottomLeft, TopLeft, TopRight, BottomRight;
        internal readonly Vector2 EmMinimum, EmSize;
        internal readonly float WritingSample;
        internal readonly float NativeFaceOffsetEm;
        readonly Color _bottomLeft, _topLeft, _topRight, _bottomRight;
        internal float MaximumAlpha => Mathf.Max(Mathf.Max(_bottomLeft.a, _topLeft.a), Mathf.Max(_topRight.a, _bottomRight.a));
        internal bool HasVisibleSource => _bottomLeft.a > 0 || _topLeft.a > 0 || _topRight.a > 0 || _bottomRight.a > 0;

        internal TextStudio2DTMPGlyphPlane(in TMP_MeshInfo mesh, int vertex, Vector2 minimum, Vector2 size, float encodedNormal, float nativeFaceOffsetEm = 0)
        {
            BottomLeft = mesh.vertices[vertex]; TopLeft = mesh.vertices[vertex + 1];
            TopRight = mesh.vertices[vertex + 2]; BottomRight = mesh.vertices[vertex + 3];
            _bottomLeft = mesh.colors32[vertex]; _topLeft = mesh.colors32[vertex + 1];
            _topRight = mesh.colors32[vertex + 2]; _bottomRight = mesh.colors32[vertex + 3];
            EmMinimum = minimum; EmSize = size; NativeFaceOffsetEm = nativeFaceOffsetEm;
            float encoded = Mathf.Abs(encodedNormal);
            WritingSample = encoded >= 1.5f ? Mathf.Round(encoded) - 1 : 0;
        }

        internal static bool TryCreate(TMP_Text text, in TextStudioVisualGlyph glyph, out TextStudio2DTMPGlyphPlane plane,
            TextStudioTMPFrameApplier frameApplier = null, TextStudioPreparedFieldDemand sourceDemand = null)
            => TryCreate(text, glyph.VisualIndex, glyph.AtlasToEm, out plane, frameApplier, sourceDemand);

        internal static bool TryCreate(TMP_Text text, int visualIndex, out TextStudio2DTMPGlyphPlane plane)
        {
            plane = default;
            if (text == null || text.textInfo?.characterInfo == null || visualIndex < 0 || visualIndex >= text.textInfo.characterCount || visualIndex >= text.textInfo.characterInfo.Length) return false;
            TMP_CharacterInfo character = text.textInfo.characterInfo[visualIndex];
            return TryCreate(text, visualIndex, TextStudioTMPFontMetrics.AtlasToEm(character.fontAsset, character.textElement?.glyph), out plane);
        }

        static bool TryCreate(TMP_Text text, int visualIndex, Vector4 map, out TextStudio2DTMPGlyphPlane plane,
            TextStudioTMPFrameApplier frameApplier = null, TextStudioPreparedFieldDemand sourceDemand = null)
        {
            plane = default;
            TMP_TextInfo info = text != null ? text.textInfo : null;
            if (info?.characterInfo == null || info.meshInfo == null || visualIndex < 0 ||
                visualIndex >= info.characterCount || visualIndex >= info.characterInfo.Length) return false;
            TMP_CharacterInfo character = info.characterInfo[visualIndex];
            int material = character.materialReferenceIndex, vertex = character.vertexIndex;
            if (material < 0 || material >= info.meshInfo.Length || vertex < 0) return false;
            ref readonly TMP_MeshInfo mesh = ref info.meshInfo[material];
            if (mesh.vertices == null || mesh.colors32 == null || mesh.uvs0 == null || vertex + 3 >= mesh.vertices.Length ||
                vertex + 3 >= mesh.colors32.Length || vertex + 3 >= mesh.uvs0.Length) return false;
            Vector2 minimum = new Vector2(mesh.uvs0[vertex].x * map.x + map.z, mesh.uvs0[vertex].y * map.y + map.w);
            Vector2 maximum = new Vector2(mesh.uvs0[vertex + 2].x * map.x + map.z, mesh.uvs0[vertex + 2].y * map.y + map.w);
            Vector2 size = maximum - minimum;
            if (!float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= .000001f || size.y <= .000001f) return false;
            Material nativeMaterial = sourceDemand?.ResolveNativeMaterial(text, material) ?? character.material ?? mesh.material;
            float faceOffset = TextStudioTMPFontMetrics.NativeFaceOffsetEm(nativeMaterial,
                character.fontAsset != null ? character.fontAsset.faceInfo.pointSize : 0, mesh.uvs0[vertex].w <= 0);
            plane = new TextStudio2DTMPGlyphPlane(in mesh, vertex, minimum, size,
                frameApplier != null ? frameApplier.GetSourceCoordinateNormal(material, vertex).z : -1, faceOffset);
            return true;
        }

        internal Vector2 NativeCoordinate(Vector2 point) => new Vector2((point.x - EmMinimum.x) / EmSize.x, (point.y - EmMinimum.y) / EmSize.y);
        internal Vector3 Position(Vector2 point)
        {
            Vector2 uv = NativeCoordinate(point);
            return Vector3.LerpUnclamped(Vector3.LerpUnclamped(BottomLeft, BottomRight, uv.x),
                Vector3.LerpUnclamped(TopLeft, TopRight, uv.x), uv.y);
        }

        internal Color32 SampleColor(Vector2 point)
        {
            Vector2 uv = NativeCoordinate(point);
            return Color.Lerp(Color.Lerp(_bottomLeft, _bottomRight, uv.x), Color.Lerp(_topLeft, _topRight, uv.x), uv.y);
        }
    }
}
