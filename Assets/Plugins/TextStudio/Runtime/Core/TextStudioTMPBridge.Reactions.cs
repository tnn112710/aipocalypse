using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal sealed partial class TextStudioTMPBridge :
        ITextStudioReactionCaptureProvider, ITextStudioReactionVisualOutputProvider
    {
        static readonly int RetainedWritingSourceProperty = Shader.PropertyToID("_TextStudioWritingSourceEnabled");

        sealed class RetainedSlot
        {
            internal GameObject Root;
            internal Mesh Mesh;
            internal MeshRenderer Renderer;
            internal TextStudioReactionCanvasGraphic Graphic;
            internal Material Material;
            internal Material SourceMaterial;
            internal Texture CapturedTexture;
            internal readonly List<Vector3> Vertices = new List<Vector3>(4);
            internal readonly List<Vector3> Normals = new List<Vector3>(4);
            internal readonly List<Vector4> Tangents = new List<Vector4>(4);
            internal readonly List<Vector4> Uv0 = new List<Vector4>(4);
            internal readonly List<Vector4> Uv1 = new List<Vector4>(4);
            internal readonly List<Vector4> Uv2 = new List<Vector4>(4);
            internal readonly List<Vector4> Uv3 = new List<Vector4>(4);
            internal readonly List<Vector4> Uv4 = new List<Vector4>(4);
            internal readonly List<Color32> BaseColors = new List<Color32>(4);
            internal readonly List<Color32> OutputColors = new List<Color32>(4);
            internal readonly List<int> Triangles = new List<int>(6);
            internal TextStudioReactionAnchor Anchor;
            internal Vector3 CenterLocal;
            internal uint Version;
            internal int Next;
            internal bool Active;
            internal float LastAlpha = -1f;
            internal Color32 LastTint = new Color32(255, 255, 255, 255);
        }

        sealed class ReactionSourceBuffers
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector3> Normals = new List<Vector3>();
            internal readonly List<Vector4> Tangents = new List<Vector4>();
            internal readonly List<Vector4> Uv0 = new List<Vector4>();
            internal readonly List<Vector4> Uv1 = new List<Vector4>();
            internal readonly List<Vector4> Uv2 = new List<Vector4>();
            internal readonly List<Vector4> Uv3 = new List<Vector4>();
            internal readonly List<Vector4> Uv4 = new List<Vector4>();
            internal readonly List<Color32> Colors = new List<Color32>();
        }

        List<RetainedSlot> _retainedSlots;
        int _retainedFreeHead = -1;
        ReactionSourceBuffers _reactionSourceBuffers;

        internal bool HasAppearanceProgramOutsideRetainedCapture
        {
            get
            {
                // The TextMeshPro route keeps the existing contract: retained capture copies no styled appearance.
                if (_appearanceAdapter.HasHeroProgram || _appearanceAdapter.HasActiveProgram || _appearanceAdapter.UsesTextMeshProRoute) return true;
                if (_appearancePlanAdapter.Plan == null) return false;
                for (int index = 0; index < _appearancePlanAdapter.Programs.Count; index++)
                {
                    TextStudioTMPAppearanceAdapter program = _appearancePlanAdapter.Programs[index];
                    if (program.HasHeroProgram || program.HasActiveProgram) return true;
                }
                return false;
            }
        }

        public TextStudioReactionCaptureCapability QuerySupport(
            in TextStudioReactionCaptureRequest request)
        {
            return QuerySupport(in request, out _, out _, out _);
        }

        TextStudioReactionCaptureCapability QuerySupport(
            in TextStudioReactionCaptureRequest request, out int materialIndex,
            out int firstCharacter, out int lastCharacter)
        {
            materialIndex = firstCharacter = lastCharacter = -1;
            if (!Application.isPlaying)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "Retained TMP output runs only in Play Mode.");
            if (!(_tmpText is TextMeshPro) && !(_tmpText is TextMeshProUGUI))
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The active text renderer is not TMP world text or TMP Canvas text.");
            if (!_layoutValid)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "TMP has no prepared layout for outgoing capture.");
            if (!IsVisualOutputEnabled)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "TMP visual output is inactive or culled.");
            if (_skippedVisualFrameWhileSuppressed)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "TMP's last visual frame was suppressed by another renderer.");
            if (HasAppearanceProgramOutsideRetainedCapture)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The active appearance program is not included in retained TMP capture.");
            if (_styleDecorations != null)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "Decorations are not included in retained TMP capture.");
            bool settledWriting = _writingRenderer != null;
            if (settledWriting && !_writingRenderer.HasSettledGlyphFaces())
                return TextStudioReactionCaptureCapability.Unsupported(
                    "Writing is still drawing or erasing; retained TMP capture copies settled Writing glyphs only.");
            if (_requiresExternalMaterialIntent || _worldShadowRenderers.Count > 0)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "External material effects and shadow outputs are not included in retained TMP capture.");
            if (request.Intent != TextStudioReactionCaptureIntent.Copy)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "TMP retained output currently supports Copy, not Detach.");
            if (request.Resources != (TextStudioReactionResourceClass.GlyphGeometry |
                                      TextStudioReactionResourceClass.MaterialState))
                return TextStudioReactionCaptureCapability.Unsupported(
                    "TMP retained output requires glyph geometry and material state together.");

            TextStudioPlanView plan = _hub.Plan;
            TMP_TextInfo textInfo = _tmpText.textInfo;
            int unit = request.Context.UnitIndex;
            if (!plan.IsCreated || unit < 0 || unit >= plan.VisibleGlyphCount ||
                textInfo == null || textInfo.characterInfo == null || textInfo.meshInfo == null)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The requested semantic unit has no prepared TMP layout.");

            int visible = 0;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[i];
                if (plan.GetUnitIndexForDisplayChar(character.index) != unit) continue;
                if (character.elementType != TMP_TextElementType.Character)
                    return TextStudioReactionCaptureCapability.Unsupported(
                        "Inline sprites are not included in retained TMP glyph capture.");
                if (character.fontAsset == null ||
                    character.fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
                    return TextStudioReactionCaptureCapability.Unsupported(
                        "The TMP font atlas is dynamic or unavailable; its texture lifetime cannot be retained.");
                if (GetDecorationBits(plan.CompiledPlan, _tmpText.fontStyle, in character,
                        unit) != DecorationBits.None)
                    return TextStudioReactionCaptureCapability.Unsupported(
                        "Underline and strikethrough geometry are not included in retained TMP capture.");
                if (!character.isVisible) continue;
                if (!IsCharacterVertexRangeValid(textInfo, in character))
                    return TextStudioReactionCaptureCapability.Unsupported(
                        "TMP has no complete final quad for the requested glyph.");
                if (materialIndex >= 0 && materialIndex != character.materialReferenceIndex)
                    return TextStudioReactionCaptureCapability.Unsupported(
                        "One semantic unit spans multiple TMP material references.");
                materialIndex = character.materialReferenceIndex;
                if (firstCharacter < 0) firstCharacter = i;
                lastCharacter = i;
                visible++;
            }
            if (visible == 0 || materialIndex < 0)
                return TextStudioReactionCaptureCapability.Unsupported(
                    "The requested semantic unit has no visible TMP glyph quad.");
            ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[materialIndex];
            return meshInfo.mesh != null && meshInfo.material != null &&
                   meshInfo.material.mainTexture != null
                ? new TextStudioReactionCaptureCapability(
                    TextStudioReactionCaptureSupport.SupportedWithDeclaredLimitations,
                    settledWriting
                        ? "TMP Copy capture copies settled Writing glyph faces without their stroke overlay, in one material only."
                        : "TMP Copy capture supports plain static-font glyphs in one material only.")
                : TextStudioReactionCaptureCapability.Unsupported(
                    "TMP has no final mesh, material, or live atlas texture for the requested glyph.");
        }

        public bool TryCapture(in TextStudioReactionCaptureRequest request,
            out TextStudioReactionLease lease)
        {
            lease = default;
            if (!QuerySupport(in request, out int materialIndex,
                    out int firstCharacter, out int lastCharacter).IsSupported ||
                !_hub.TryGetReactionAnchor(in request.Context,
                    out TextStudioReactionAnchor anchor)) return false;

            TextStudioPlanView plan = _hub.Plan;
            TMP_TextInfo textInfo = _tmpText.textInfo;
            int unit = request.Context.UnitIndex;
            ref readonly TMP_MeshInfo sourceInfo = ref textInfo.meshInfo[materialIndex];
            Mesh sourceMesh = sourceInfo.mesh;
            ReactionSourceBuffers sourceBuffers = ReadReactionSourceMesh(sourceMesh);
            int slotIndex = AcquireRetainedSlot();
            if (slotIndex < 0) return false;
            RetainedSlot slot = _retainedSlots[slotIndex];
            bool accepted = false;
            try
            {
                slot.Anchor = anchor;
                slot.CenterLocal = _tmpText.transform.InverseTransformPoint(anchor.Position);
                slot.Vertices.Clear();
                slot.Normals.Clear();
                slot.Tangents.Clear();
                slot.Uv0.Clear();
                slot.Uv1.Clear();
                slot.Uv2.Clear();
                slot.Uv3.Clear();
                slot.Uv4.Clear();
                slot.BaseColors.Clear();
                slot.OutputColors.Clear();
                slot.Triangles.Clear();
                for (int i = firstCharacter; i <= lastCharacter; i++)
                {
                    ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[i];
                    if (!character.isVisible ||
                        plan.GetUnitIndexForDisplayChar(character.index) != unit) continue;
                    int first = character.vertexIndex;
                    if (first < 0 || first + 3 >= sourceBuffers.Vertices.Count)
                    {
                        Release(slotIndex, slot.Version);
                        return false;
                    }
                    int quad = slot.Vertices.Count;
                    for (int v = 0; v < 4; v++)
                    {
                        int source = first + v;
                        slot.Vertices.Add(sourceBuffers.Vertices[source] - slot.CenterLocal);
                        slot.Normals.Add(ReadOrDefault(sourceBuffers.Normals, source, Vector3.back));
                        slot.Tangents.Add(ReadOrDefault(sourceBuffers.Tangents, source,
                            new Vector4(1f, 0f, 0f, -1f)));
                        slot.Uv0.Add(ReadOrDefault(sourceBuffers.Uv0, source, default));
                        slot.Uv1.Add(ReadOrDefault(sourceBuffers.Uv1, source, default));
                        slot.Uv2.Add(ReadOrDefault(sourceBuffers.Uv2, source, default));
                        slot.Uv3.Add(ReadOrDefault(sourceBuffers.Uv3, source, default));
                        slot.Uv4.Add(ReadOrDefault(sourceBuffers.Uv4, source, default));
                        Color32 color = ReadOrDefault(sourceBuffers.Colors, source,
                            new Color32(255, 255, 255, 255));
                        slot.BaseColors.Add(color);
                        slot.OutputColors.Add(color);
                    }
                    slot.Triangles.Add(quad);
                    slot.Triangles.Add(quad + 1);
                    slot.Triangles.Add(quad + 2);
                    slot.Triangles.Add(quad + 2);
                    slot.Triangles.Add(quad + 3);
                    slot.Triangles.Add(quad);
                }
                if (slot.Vertices.Count == 0)
                {
                    Release(slotIndex, slot.Version);
                    return false;
                }
                PrepareRetainedMaterial(slot, sourceInfo.material);
                Mesh mesh = slot.Mesh;
                mesh.Clear();
                mesh.SetVertices(slot.Vertices);
                mesh.SetNormals(slot.Normals);
                mesh.SetTangents(slot.Tangents);
                mesh.SetColors(slot.OutputColors);
                mesh.SetUVs(0, slot.Uv0);
                mesh.SetUVs(1, slot.Uv1);
                mesh.SetUVs(2, slot.Uv2);
                mesh.SetUVs(3, slot.Uv3);
                mesh.SetUVs(4, slot.Uv4);
                mesh.SetTriangles(slot.Triangles, 0, false);
                mesh.RecalculateBounds();
                slot.Root.transform.localPosition = slot.CenterLocal;
                slot.Root.transform.localRotation = Quaternion.identity;
                slot.Root.transform.localScale = Vector3.one;
                slot.LastAlpha = 1f;
                slot.LastTint = new Color32(255, 255, 255, 255);
                lease = new TextStudioReactionLease(this, slotIndex, slot.Version);
                accepted = true;
                return true;
            }
            finally
            {
                if (!accepted) Release(slotIndex, slot.Version);
            }
        }

        public bool TryGetAnchor(int slot, uint version,
            out TextStudioReactionAnchor anchor)
        {
            if (TryGetRetainedSlot(slot, version, out RetainedSlot output))
            {
                anchor = output.Anchor;
                return true;
            }
            anchor = default;
            return false;
        }

        public bool TrySubmit(int slot, uint version,
            in TextStudioReactionVisualSample sample)
        {
            if (!TryGetRetainedSlot(slot, version, out RetainedSlot output) ||
                output.Root == null) return false;
            if (output.CapturedTexture == null)
            {
                Release(slot, version);
                return false;
            }
            Transform transform = output.Root.transform;
            transform.localPosition = output.CenterLocal + sample.LocalOffset;
            transform.localRotation = sample.LocalRotation;
            transform.localScale = sample.LocalScale;
            if (sample.NeedsColorUpdate(output.LastAlpha, output.LastTint))
            {
                for (int i = 0; i < output.BaseColors.Count; i++)
                    output.OutputColors[i] = sample.ApplyColor(output.BaseColors[i]);
                output.Mesh.SetColors(output.OutputColors);
                output.Graphic?.SetVerticesDirty();
                output.LastAlpha = sample.Alpha;
                output.LastTint = sample.Tint;
            }
            output.Root.SetActive(sample.Alpha > 0f);
            return true;
        }

        public void Release(int slot, uint version)
        {
            if (!TryGetRetainedSlot(slot, version, out RetainedSlot output)) return;
            if (output.Root != null) output.Root.SetActive(false);
            output.Active = false;
            output.Anchor = default;
            output.CapturedTexture = null;
            output.Next = _retainedFreeHead;
            _retainedFreeHead = slot;
        }

        int AcquireRetainedSlot()
        {
            _retainedSlots ??= new List<RetainedSlot>(4);
            int index;
            if (_retainedFreeHead >= 0)
            {
                index = _retainedFreeHead;
                _retainedFreeHead = _retainedSlots[index].Next;
            }
            else
            {
                if (_retainedSlots.Count == 4096) return -1;
                index = _retainedSlots.Count;
                bool canvas = _tmpText is TextMeshProUGUI;
                GameObject root = canvas
                    ? new GameObject("Text Studio Retained Unit", typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(TextStudioReactionCanvasGraphic))
                    : new GameObject("Text Studio Retained Unit", typeof(MeshFilter),
                        typeof(MeshRenderer));
                root.SetActive(false);
                root.transform.SetParent(_tmpText.transform, false);
                Mesh mesh = new Mesh { name = "Text Studio Retained Glyph" };
                mesh.MarkDynamic();
                if (!canvas) root.GetComponent<MeshFilter>().sharedMesh = mesh;
                RetainedSlot slot = new RetainedSlot
                {
                    Root = root,
                    Mesh = mesh,
                    Renderer = canvas ? null : root.GetComponent<MeshRenderer>(),
                    Graphic = canvas ? root.GetComponent<TextStudioReactionCanvasGraphic>() : null
                };
                slot.Graphic?.SetSource(mesh);
                _retainedSlots.Add(slot);
            }
            RetainedSlot selected = _retainedSlots[index];
            selected.Active = true;
            unchecked { selected.Version++; }
            if (selected.Version == 0) selected.Version = 1;
            selected.Next = -1;
            return index;
        }

        bool TryGetRetainedSlot(int slot, uint version,
            out RetainedSlot output)
        {
            if (_retainedSlots != null && slot >= 0 && slot < _retainedSlots.Count)
            {
                output = _retainedSlots[slot];
                if (output.Active && output.Version == version) return true;
            }
            output = null;
            return false;
        }

        ReactionSourceBuffers ReadReactionSourceMesh(Mesh mesh)
        {
            ReactionSourceBuffers buffers = _reactionSourceBuffers ??= new ReactionSourceBuffers();
            mesh.GetVertices(buffers.Vertices);
            mesh.GetNormals(buffers.Normals);
            mesh.GetTangents(buffers.Tangents);
            mesh.GetColors(buffers.Colors);
            mesh.GetUVs(0, buffers.Uv0);
            mesh.GetUVs(1, buffers.Uv1);
            mesh.GetUVs(2, buffers.Uv2);
            mesh.GetUVs(3, buffers.Uv3);
            mesh.GetUVs(4, buffers.Uv4);
            return buffers;
        }

        static T ReadOrDefault<T>(List<T> source, int index, T fallback) =>
            index < source.Count ? source[index] : fallback;

        void PrepareRetainedMaterial(RetainedSlot slot, Material source)
        {
            if (slot.Material == null || slot.SourceMaterial != source ||
                slot.Material.shader != source.shader)
            {
                if (slot.Material != null) Object.Destroy(slot.Material);
                slot.Material = new Material(source) { name = "Text Studio Retained Material" };
                slot.SourceMaterial = source;
                if (slot.Renderer != null) slot.Renderer.sharedMaterial = slot.Material;
            }
            else slot.Material.CopyPropertiesFromMaterial(source);
            // A copy must never sample live Writing coverage: the next text reuses those samples from zero.
            if (slot.Material.HasProperty(RetainedWritingSourceProperty))
                slot.Material.SetFloat(RetainedWritingSourceProperty, 0f);
            slot.CapturedTexture = slot.Material.mainTexture;
            if (slot.Graphic != null)
            {
                slot.Graphic.SetSourceMaterial(slot.Material);
                slot.Graphic.SetVerticesDirty();
                return;
            }
            MeshRenderer sourceRenderer = _tmpText.GetComponent<MeshRenderer>();
            if (sourceRenderer == null) return;
            slot.Renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            slot.Renderer.sortingOrder = sourceRenderer.sortingOrder;
            slot.Renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            slot.Renderer.receiveShadows = sourceRenderer.receiveShadows;
        }

        void DisposeRetainedReactionOutputs()
        {
            if (_retainedSlots == null) return;
            for (int i = 0; i < _retainedSlots.Count; i++)
            {
                RetainedSlot slot = _retainedSlots[i];
                if (slot.Root != null) DestroyRetainedObject(slot.Root);
                if (slot.Mesh != null) DestroyRetainedObject(slot.Mesh);
                if (slot.Material != null) DestroyRetainedObject(slot.Material);
            }
            _retainedSlots.Clear();
            _retainedFreeHead = -1;
        }

        static void DestroyRetainedObject(Object instance)
        {
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }
}
