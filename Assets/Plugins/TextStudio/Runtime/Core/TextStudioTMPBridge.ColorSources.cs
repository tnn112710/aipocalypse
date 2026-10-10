using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudioTMPBridge
    {
        const float ColorCoordinatesPerEm = 1048576;
        readonly Dictionary<ulong, int> _colorSourceIndices = new Dictionary<ulong, int>();
        TextStudioColorSource[] _colorSources = Array.Empty<TextStudioColorSource>();
        int _colorSourceCount;

        void ResetColorSources()
        {
            if (_colorSourceCount > 0) Array.Clear(_colorSources, 0, _colorSourceCount);
            _colorSourceCount = 0;
            _colorSourceIndices.Clear();
        }

        int RegisterColorSource(TMP_TextInfo textInfo, in TMP_CharacterInfo character)
        {
            if (character.elementType != TMP_TextElementType.Sprite || !character.isVisible || !IsCharacterVertexRangeValid(textInfo, in character)) return -1;
            Material material = _materialEffectAdapter.ResolveSourceMaterial(_tmpText, character.materialReferenceIndex);
            Texture texture = material != null ? material.mainTexture : null;
            if (texture == null) return -1;
            ref readonly TMP_MeshInfo mesh = ref textInfo.meshInfo[character.materialReferenceIndex];
            int start = character.vertexIndex;
            if (mesh.uvs0 == null || start + 3 >= mesh.uvs0.Length) return -1;
            float em = Mathf.Max(.0001f, ResolveEffectUnitScale(_tmpText, in character));
            Vector3 origin = mesh.vertices[start];
            TextStudioDeterministicHash64 hash = default;
            hash.Add(texture.GetStableId()); hash.Add(_hub.InlineColorAlphaThreshold);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 position = ResolveColorSourcePosition(mesh.vertices[start + corner], origin, em);
                Vector4 uv = mesh.uvs0[start + corner];
                hash.Add(position.x); hash.Add(position.y); hash.Add(uv.x); hash.Add(uv.y);
            }
            if (_colorSourceIndices.TryGetValue(hash.Value, out int existing)) return existing;
            Vector2[] positions = new Vector2[4];
            Vector2[] coordinates = new Vector2[4];
            for (int corner = 0; corner < 4; corner++)
            {
                positions[corner] = ResolveColorSourcePosition(mesh.vertices[start + corner], origin, em);
                Vector4 uv = mesh.uvs0[start + corner];
                coordinates[corner] = new Vector2(uv.x, uv.y);
            }
            TextStudioColorSource source = new TextStudioColorSource(texture, positions, coordinates, new[] { 0, 1, 2, 2, 3, 0 }, _hub.InlineColorAlphaThreshold);
            if (source.IsEmpty) return -1;
            if (_colorSourceCount == _colorSources.Length) Array.Resize(ref _colorSources, Math.Max(4, _colorSourceCount * 2));
            int index = _colorSourceCount++;
            _colorSources[index] = source;
            _colorSourceIndices.Add(hash.Value, index);
            return index;
        }

        static Vector2 ResolveColorSourcePosition(Vector3 position, Vector3 origin, float em)
        {
            // Layout subtraction introduces float roundoff for otherwise identical sprites. This grid is
            // finer than 1/1000 of a field texel at maximum density; final drawing still uses the native quad.
            Vector3 local = (position - origin) / em;
            return new Vector2(Mathf.Round(local.x * ColorCoordinatesPerEm) / ColorCoordinatesPerEm,
                Mathf.Round(local.y * ColorCoordinatesPerEm) / ColorCoordinatesPerEm);
        }
    }
}
