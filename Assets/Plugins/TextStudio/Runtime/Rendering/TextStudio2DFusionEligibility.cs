using System;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>A conservative proof that per-source fusion cannot change painter order.</summary>
    internal sealed class TextStudio2DFusionEligibility
    {
        const int MaximumPairChecks = 32768;
        Rect[] _bounds = Array.Empty<Rect>();

        internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ArrayBytes(_bounds);

        internal bool CanFuse(in TextStudioFrame frame, TMP_Text text, TextStudioTMPMaterialEffectAdapter materials)
        {
            // One operation on one native material already draws in the required glyph order, including overlapping and posed glyphs.
            if (frame.AppearanceStyle.OperationCount == 1 && text.textInfo.materialCount == 1) return true;
            Vector2 commonOffset = default;
            for (int index = 0; index < text.textInfo.materialCount; index++)
            {
                Material material = materials.ResolveRenderingMaterial(text, index);
                Vector2 offset = new Vector2(material != null && material.HasProperty("_VertexOffsetX") ? material.GetFloat("_VertexOffsetX") : 0,
                    material != null && material.HasProperty("_VertexOffsetY") ? material.GetFloat("_VertexOffsetY") : 0);
                if (index == 0) commonOffset = offset;
                else if (offset != commonOffset) return false;
            }
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            if (_bounds.Length < snapshot.RenderGlyphCount) _bounds = new Rect[TextStudioBufferCapacity.ForCount(snapshot.RenderGlyphCount)];
            int count = 0;
            float plane = float.NaN;
            for (int index = 0; index < snapshot.RenderGlyphCount; index++)
            {
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (!glyph.IsVisible || glyph.VisualIndex < 0 || glyph.VisualIndex >= text.textInfo.characterCount) continue;
                ref readonly TMP_CharacterInfo character = ref text.textInfo.characterInfo[glyph.VisualIndex];
                ref readonly TMP_MeshInfo mesh = ref text.textInfo.meshInfo[character.materialReferenceIndex];
                Vector2 minimum = Vector2.positiveInfinity;
                Vector2 maximum = Vector2.negativeInfinity;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 vertex = mesh.vertices[character.vertexIndex + corner];
                    if (float.IsNaN(plane)) plane = vertex.z;
                    // Different local planes can overlap in a perspective camera even when their XY bounds do not.
                    if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z) || Mathf.Abs(vertex.z - plane) > .00001f) return false;
                    minimum = Vector2.Min(minimum, vertex); maximum = Vector2.Max(maximum, vertex);
                }
                _bounds[count++] = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            }
            return AreDisjoint(_bounds, count);
        }

        internal static bool AreDisjoint(Rect[] bounds, int count)
        {
            // Unity's Mono Array.Sort allocates a comparison delegate even after warmup.
            // An in-place heap keeps this proof allocation-free and bounds sorting to O(n log n).
            for (int root = count / 2 - 1; root >= 0; root--) SiftDown(bounds, root, count);
            for (int end = count - 1; end > 0; end--)
            {
                Rect value = bounds[end]; bounds[end] = bounds[0]; bounds[0] = value;
                SiftDown(bounds, 0, end);
            }
            int checks = 0;
            for (int first = 0; first < count; first++)
                for (int second = first + 1; second < count && bounds[second].xMin < bounds[first].xMax; second++)
                {
                    if (++checks > MaximumPairChecks) return false;
                    if (bounds[first].Overlaps(bounds[second])) return false;
                }
            return true;
        }

        static void SiftDown(Rect[] bounds, int root, int count)
        {
            Rect value = bounds[root];
            for (int child = root * 2 + 1; child < count; child = root * 2 + 1)
            {
                if (child + 1 < count && bounds[child].xMin < bounds[child + 1].xMin) child++;
                if (value.xMin >= bounds[child].xMin) break;
                bounds[root] = bounds[child];
                root = child;
            }
            bounds[root] = value;
        }

    }
}
