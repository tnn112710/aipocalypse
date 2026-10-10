using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TextStudio
{
    /// <summary>Schedules native field quads and retained surfaces together without flattening their depth.</summary>
    internal sealed class TextStudio2DOrderedRenderer : IDisposable
    {
        const int MaximumUiRunVertices = 60000;
        readonly TextStudioText _owner;
        readonly Geometry _geometry = new Geometry();
        readonly List<Run> _runs = new List<Run>();
        readonly List<Material> _materials = new List<Material>();
        readonly List<TextStudio2DOrderedGraphic> _graphics = new List<TextStudio2DOrderedGraphic>();
        /// <summary>Retained decoration, stroke and depth geometry of each scoped occurrence, drawn in its painter order.</summary>
        readonly List<TextStudio2DStyleDecorations> _occurrenceDecorations = new List<TextStudio2DStyleDecorations>();
        readonly TextStudio2DFusionEligibility _fusion = new TextStudio2DFusionEligibility();
        /// <summary>Selected operations of the glyph being emitted, 64 per word.</summary>
        ulong[] _selection = new ulong[1];
        ulong[] _glyphSelections = Array.Empty<ulong>();
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        TextStudioCanvasShaderChannelLease _canvasLease;
        /// <summary>Engine-published quads the current build emits from instead of TMP quads.</summary>
        TextStudio2DExternalGlyphSource _external;
        int _externalRevision;
        TextStudioStackFingerprint _fingerprint;
        TextStudioVisualVersion _version;
        GameObject _worldObject;
        Mesh _worldMesh;
        MeshRenderer _worldRenderer;
        bool _hasBinding;
        bool _active;
        bool _ownPrimitives;
        // A world batch group draws these primitives in its shards; the own renderer stays hidden and skips uploads.
        bool _worldSuppressed;
        bool _worldMeshStale;
        int _styleSlot;
        int _retainedRevision;
        int _appearanceVersion;

        internal long ManagedPayloadBytes => _geometry.ManagedPayloadBytes + _fusion.ManagedPayloadBytes + TextStudioAppearanceManagedMemory.ListBytes(_runs) +
            (long)(_materials.Capacity + _graphics.Capacity) * IntPtr.Size;

        internal int GeometryUploadCount { get; private set; }
        internal int DrawCount => _active ? _runs.Count : 0;

        internal TextStudio2DOrderedRenderer(TextStudioText owner)
        {
            _owner = owner;
            _worldSuppressed = owner.IsWorldBatched;
        }

        /// <summary>World primitives in painter order for a batch group; <paramref name="revision"/> changes whenever they are rebuilt.</summary>
        internal bool TryGetWorldBatch(out Geometry geometry, out List<Run> runs, out Transform space, out int revision)
        {
            geometry = _geometry;
            runs = _runs;
            space = _worldObject != null ? _worldObject.transform : null;
            revision = GeometryUploadCount;
            return _active && space != null && _runs.Count > 0;
        }

        internal void SetWorldOutputSuppressed(bool suppressed)
        {
            if (_worldSuppressed == suppressed) return;
            _worldSuppressed = suppressed;
            if (_worldRenderer == null) return;
            if (!suppressed && _worldMeshStale) UploadWorldMesh();
            _worldRenderer.enabled = _active && !suppressed;
        }

        /// <summary>Styles whose operations all draw as ordered field or retained primitives, at any operation count.</summary>
        internal static bool Supports(CompiledTextStudio2DStyle style)
        {
            if (style == null || !style.IsValid || style.IsPassThrough)
                return false;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.ExecutionPath != TextStudio2DExecutionPath.FusedField && operation.ExecutionPath != TextStudio2DExecutionPath.RetainedGeometry ||
                    operation.BlendMode != TextStudio2DBlendMode.Normal || operation.ParentOperationIndex >= 0 || operation.MaskOperationIndex >= 0)
                    return false;
            }
            return true;
        }

        internal bool Render(in TextStudioFrame frame, TMP_Text text, int styleSlot,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier, bool forceRefresh,
            TextStudio2DStyleDecorations retained = null, bool ownPrimitives = false)
        {
            TextStudioStackFingerprint fingerprint = frame.AppearanceStyle.Fingerprint;
            bool rebuild = forceRefresh || !_hasBinding || _styleSlot != styleSlot ||
                _fingerprint.Topology != fingerprint.Topology || _fingerprint.Bounds != fingerprint.Bounds ||
                _fingerprint.RenderState != fingerprint.RenderState || !_version.Equals(frame.VisualSnapshot.Version) ||
                !frame.Dirty.IsCreated || frame.Dirty.AnyDirtyInRange(0, frame.VisualSnapshot.GlyphCount) ||
                _retainedRevision != (retained?.GeometryRevision ?? 0) || _ownPrimitives != ownPrimitives;
            if (rebuild)
            {
                _styleSlot = styleSlot;
                _ownPrimitives = ownPrimitives;
                // The native TMP quad can neither grow past its padding nor isolate one operation, so such styles draw here.
                _active = ownPrimitives || retained != null && !retained.IsEmpty || !_fusion.CanFuse(in frame, text, materials);
                if (_active) Build(in frame, text, materials, applier, retained);
                _retainedRevision = retained?.GeometryRevision ?? 0;
                _fingerprint = fingerprint;
                _version = frame.VisualSnapshot.Version;
                _hasBinding = true;
            }
            if (!_active) { Hide(); return false; }
            if (text is TextMeshProUGUI ui) SynchronizeUi(ui.transform, ui, rebuild);
            else SynchronizeWorld(text, rebuild);
            return true;
        }

        /// <summary>
        /// Draws one program from quads a text engine published with its own glyph atlas. Every selected operation
        /// draws as an owned primitive; the engine's native output stays withheld while this renderer is active.
        /// </summary>
        internal bool RenderExternal(in TextStudioFrame frame, TextStudio2DExternalGlyphSource source, int styleSlot, bool forceRefresh)
        {
            TextStudioStackFingerprint fingerprint = frame.AppearanceStyle.Fingerprint;
            bool rebuild = forceRefresh || !_hasBinding || _styleSlot != styleSlot || _externalRevision != source.GeometryRevision ||
                _fingerprint.Topology != fingerprint.Topology || _fingerprint.Bounds != fingerprint.Bounds ||
                _fingerprint.RenderState != fingerprint.RenderState || !_version.Equals(frame.VisualSnapshot.Version);
            if (rebuild)
            {
                _styleSlot = styleSlot;
                _external = source;
                try
                {
                    CompiledTextStudio2DStyle style = frame.AppearanceStyle;
                    if (style.PainterOrder == TextStudio2DPainterOrder.GlyphMajor)
                    {
                        _geometry.Clear(); _runs.Clear(); _materials.Clear();
                        int words = ResolveSelections(in frame);
                        for (int glyph = 0; glyph < frame.VisualSnapshot.RenderGlyphCount; glyph++)
                            if (LoadSelection(glyph, words))
                                EmitFusedSpans(in frame, null, glyph, null, null, glyph);
                    }
                    else Build(in frame, null, null, null, null);
                }
                finally { _external = null; }
                _externalRevision = source.GeometryRevision;
                _fingerprint = fingerprint;
                _version = frame.VisualSnapshot.Version;
                _hasBinding = true;
                _active = _runs.Count > 0;
            }
            if (!_active) { Hide(); return false; }
            if (source.IsCanvas) SynchronizeUi(source.Host, null, rebuild, source);
            else SynchronizeExternalWorld(source, rebuild);
            return true;
        }

        /// <summary>Draws every occurrence of an appearance plan from engine-published quads, in painter order.</summary>
        internal bool RenderAppearanceExternal(in TextStudioFrame frame, TextStudio2DExternalGlyphSource source,
            TextStudioTMPAppearancePlanAdapter appearances, bool forceRefresh)
        {
            if (!appearances.IsReady || appearances.UsesHero) { Hide(); return false; }
            bool rebuild = forceRefresh || !_hasBinding || _appearanceVersion != appearances.Version ||
                _externalRevision != source.GeometryRevision || !_version.Equals(frame.VisualSnapshot.Version);
            if (rebuild)
            {
                _external = source;
                try { BuildAppearance(in frame, null, appearances, null, null); }
                finally { _external = null; }
                _externalRevision = source.GeometryRevision;
            }
            if (!_active) { Hide(); return false; }
            if (source.IsCanvas) SynchronizeUi(source.Host, null, rebuild, source);
            else SynchronizeExternalWorld(source, rebuild);
            return true;
        }

        internal bool RenderAppearance(in TextStudioFrame frame, TMP_Text text, TextStudioTMPAppearancePlanAdapter appearances,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier, bool forceRefresh)
        {
            if (!appearances.IsReady || appearances.UsesHero) { Hide(); return false; }
            bool rebuild = forceRefresh || !_hasBinding || _appearanceVersion != appearances.Version ||
                !_version.Equals(frame.VisualSnapshot.Version) || !frame.Dirty.IsCreated || frame.Dirty.AnyDirtyInRange(0, frame.VisualSnapshot.GlyphCount);
            if (rebuild) BuildAppearance(in frame, text, appearances, materials, applier);
            if (!_active) { Hide(); return false; }
            if (text is TextMeshProUGUI ui) SynchronizeUi(ui.transform, ui, rebuild);
            else SynchronizeWorld(text, rebuild);
            return true;
        }

        void BuildAppearance(in TextStudioFrame frame, TMP_Text text, TextStudioTMPAppearancePlanAdapter appearances,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier)
        {
            {
                _geometry.Clear(); _runs.Clear(); _materials.Clear();
                TextStudio2DAppearanceBinding binding = appearances.Binding;
                CompiledTextStudio2DStyle proofStyle = appearances.PrimaryFieldStyle;
                // The proof tests final mesh quads padded for the maximum demand of all programs.
                // Select a multi-op style when present to bypass CanFuse's single-op shortcut.
                for (int index = 0; index < appearances.Programs.Count; index++)
                    if (appearances.Programs[index].BoundStyle.Style?.OperationCount > 1)
                    { proofStyle = appearances.Programs[index].BoundStyle.Style; break; }
                TextStudioFrame proofFrame = new TextStudioFrame(in frame, proofStyle, -1, binding);
                // Engine quads carry no native padding to fuse into; their occurrences keep layer-major order.
                bool sharedFusionProof = _external == null && proofStyle != null && _fusion.CanFuse(in proofFrame, text, materials);
                for (int occurrenceIndex = 0; occurrenceIndex < binding.Plan.Occurrences.Count; occurrenceIndex++)
                {
                    TextStudioAppearanceOccurrence occurrence = binding.Plan.Occurrences[occurrenceIndex];
                    if (!occurrence.IsActive || occurrence.VisibleRanges.Count == 0) continue;
                    if (occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay && binding.Plan.Programs[occurrence.ProgramIndex].IsPassThrough) continue;
                    TextStudioTMPAppearanceAdapter adapter = appearances.Programs[occurrence.ProgramIndex];
                    CompiledTextStudio2DStyle style = adapter.BoundStyle.Style;
                    // A program of only retained rows needs no field material but still owns its program slot.
                    int slot = adapter.HasRetainedProgram ? adapter.StyleSlot : adapter.FieldStyleSlot;
                    if (style == null || slot <= 0) continue;
                    _styleSlot = slot;
                    TextStudioFrame scoped = new TextStudioFrame(in frame, style, occurrenceIndex, binding);
                    IReadOnlyList<int> glyphs = binding.GetOccurrenceRenderGlyphs(occurrenceIndex);
                    TextStudio2DStyleDecorations retained = null;
                    if (adapter.HasRetainedProgram && _external == null)
                    {
                        while (_occurrenceDecorations.Count <= occurrenceIndex) _occurrenceDecorations.Add(null);
                        retained = _occurrenceDecorations[occurrenceIndex] ??= new TextStudio2DStyleDecorations();
                        retained.Rebuild(in scoped, text, slot, adapter.BoundStyle, adapter.SourceDemand, applier);
                    }
                    // Retained rows span glyphs, so an occurrence that owns them emits operation-major.
                    bool fused = retained == null && (style.PainterOrder == TextStudio2DPainterOrder.GlyphMajor || sharedFusionProof);
                    int words = SelectionWords(style);
                    if (_selection.Length < words) _selection = new ulong[words];
                    if (fused)
                    {
                        for (int index = 0; index < glyphs.Count; index++)
                        {
                            int source = glyphs[index], unit = frame.VisualSnapshot.RenderGlyphAt(source).TextUnitIndex;
                            if (!binding.Contains(occurrenceIndex, unit)) continue;
                            for (int word = 0; word < words; word++) _selection[word] = binding.GetOperationMask(occurrenceIndex, unit, word);
                            if (!AnySelected(words) && occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay) continue;
                            EmitFusedSpans(in scoped, text, source, materials, applier, binding.GetMappingGlyphIndex(occurrenceIndex, source));
                        }
                    }
                    else
                    {
                        for (int operation = style.OperationCount - 1; operation >= -1; operation--)
                        {
                            if (operation < 0 && occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay) continue;
                            if (operation >= 0)
                            {
                                ref readonly TextStudio2DCompiledOperation row = ref style.OperationAt(operation);
                                if (row.Kind == TextStudio2DLayerKind.ColorAdjustment || row.SurfaceOwnerOperationIndex >= 0) continue;
                                if (row.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry)
                                {
                                    retained?.AppendOrderedOperation(this, operation);
                                    continue;
                                }
                            }
                            for (int index = 0; index < glyphs.Count; index++)
                            {
                                int source = glyphs[index], unit = frame.VisualSnapshot.RenderGlyphAt(source).TextUnitIndex;
                                if (!binding.Contains(occurrenceIndex, unit)) continue;
                                for (int word = 0; word < words; word++) _selection[word] = binding.GetOperationMask(occurrenceIndex, unit, word);
                                EmitSelected(in scoped, text, source, operation, materials, applier, binding.GetMappingGlyphIndex(occurrenceIndex, source));
                            }
                        }
                    }
                }
                for (int index = binding.Plan.Occurrences.Count; index < _occurrenceDecorations.Count; index++)
                {
                    _occurrenceDecorations[index]?.Dispose();
                    _occurrenceDecorations[index] = null;
                }
                _appearanceVersion = appearances.Version; _version = frame.VisualSnapshot.Version;
                _hasBinding = true; _active = _runs.Count > 0;
            }
        }

        void Build(in TextStudioFrame frame, TMP_Text text, TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier,
            TextStudio2DStyleDecorations retained)
        {
            _geometry.Clear();
            _runs.Clear();
            _materials.Clear();
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            int words = ResolveSelections(in frame);
            // A row at index zero is the topmost contribution.
            for (int operationIndex = style.OperationCount - 1; operationIndex >= 0; operationIndex--)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
                if (operation.Kind == TextStudio2DLayerKind.ColorAdjustment || operation.SurfaceOwnerOperationIndex >= 0) continue;
                if (operation.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry)
                    retained?.AppendOrderedOperation(this, operationIndex);
                else
                    for (int source = 0; source < frame.VisualSnapshot.RenderGlyphCount; source++)
                        if (LoadSelection(source, words))
                            EmitSelected(in frame, text, source, operationIndex, materials, applier);
            }
            // Preserve the field shader's native face fallback outside all selected contributions.
            for (int source = 0; source < frame.VisualSnapshot.RenderGlyphCount; source++)
                if (LoadSelection(source, words))
                    EmitSelected(in frame, text, source, -1, materials, applier);
            _active = _runs.Count > 0;
        }

        internal void AppendRetainedOperation(TextStudio2DStyleDecorations.GeometryBuffer source, Material material, int operationIndex)
        {
            if (material == null) return;
            for (int quad = 0; quad < source.Vertices.Count; quad += 4)
            {
                if (Mathf.RoundToInt(source.Uv1[quad].w) != operationIndex) continue;
                int start = BeginRun(material);
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = quad + corner;
                    _geometry.Vertices.Add(source.Vertices[vertex]);
                    _geometry.Normals.Add(Vector3.back);
                    _geometry.Colors.Add(source.Colors[vertex]);
                    _geometry.Uv0.Add(source.Uv0[vertex]); _geometry.Uv1.Add(source.Uv1[vertex]);
                    _geometry.Uv2.Add(source.Uv2[vertex]); _geometry.Uv3.Add(source.Uv3[vertex]);
                    _geometry.Local.Add(Vector4.zero);
                }
                _geometry.Indices.Add(start); _geometry.Indices.Add(start + 2); _geometry.Indices.Add(start + 1);
                _geometry.Indices.Add(start + 1); _geometry.Indices.Add(start + 2); _geometry.Indices.Add(start + 3);
                EndQuad();
            }
        }

        /// <summary>
        /// Emits one glyph's selected operations as fused spans of up to eight operations around single-operation
        /// primitives for isolated operations, bottom row first. Source-over is associative, so consecutive spans
        /// compose exactly like one program; painter order within the glyph is unchanged and the run stays one draw.
        /// </summary>
        void EmitFusedSpans(in TextStudioFrame frame, TMP_Text text, int sourceIndex,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier, int mappingGlyph)
        {
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            bool isolated = TextStudio2DPrimitiveSupport.HasIsolatedOperations(style);
            bool emitted = false;
            for (int span = (style.OperationCount - 1) / TextStudio2DStyleTable.MaximumFusedOperations; span >= 0; span--)
            {
                int mask = SpanMask(span);
                if (mask == 0) continue;
                int first = span * TextStudio2DStyleTable.MaximumFusedOperations;
                if (!isolated)
                {
                    Emit(in frame, text, sourceIndex, -2, mask, span, materials, applier, mappingGlyph);
                    emitted = true;
                    continue;
                }
                int run = 0;
                for (int bit = TextStudio2DStyleTable.MaximumFusedOperations - 1; bit >= 0; bit--)
                {
                    if ((mask & (1 << bit)) == 0) continue;
                    ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(first + bit);
                    if (!TextStudio2DPrimitiveSupport.IsIsolatedOperation(in operation)) { run |= 1 << bit; continue; }
                    if (run != 0) Emit(in frame, text, sourceIndex, -2, run, span, materials, applier, mappingGlyph);
                    run = 0;
                    Emit(in frame, text, sourceIndex, first + bit, 0, 0, materials, applier, mappingGlyph);
                }
                if (run != 0) Emit(in frame, text, sourceIndex, -2, run, span, materials, applier, mappingGlyph);
                emitted = true;
            }
            // A glyph without selected operations keeps its native face through an empty selection.
            if (!emitted) Emit(in frame, text, sourceIndex, -2, 0, 0, materials, applier, mappingGlyph);
        }

        static int SelectionWords(CompiledTextStudio2DStyle style) => Mathf.Max(1, (style.OperationCount + 63) >> 6);

        int SpanMask(int span) => (int)((_selection[span >> 3] >> ((span & 7) << 3)) & 0xffUL);

        bool IsSelected(int operation) => (_selection[operation >> 6] & (1UL << (operation & 63))) != 0;

        bool AnySelected(int words)
        {
            for (int word = 0; word < words; word++)
                if (_selection[word] != 0) return true;
            return false;
        }

        /// <summary>Evaluates the single-style selectors of every render glyph once, before operation-major emission.</summary>
        int ResolveSelections(in TextStudioFrame frame)
        {
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            int words = SelectionWords(style);
            int glyphs = frame.VisualSnapshot.RenderGlyphCount;
            if (_selection.Length < words) _selection = new ulong[words];
            if (_glyphSelections.Length < glyphs * words) _glyphSelections = new ulong[TextStudioBufferCapacity.ForCount(glyphs * words)];
            Array.Clear(_glyphSelections, 0, glyphs * words);
            _selectorPass.Begin();
            for (int glyph = 0; glyph < glyphs; glyph++)
            {
                ref readonly TextStudioVisualGlyph source = ref frame.VisualSnapshot.RenderGlyphAt(glyph);
                if (!source.IsVisible) continue;
                for (int span = 0; span * TextStudio2DStyleTable.MaximumFusedOperations < style.OperationCount; span++)
                {
                    ulong mask = TextStudio2DSelectorEvaluator.EvaluateOperationMask(style, in frame, source.TextUnitIndex, span, _selectorPass);
                    _glyphSelections[glyph * words + (span >> 3)] |= mask << ((span & 7) << 3);
                }
            }
            return words;
        }

        bool LoadSelection(int glyph, int words)
        {
            Array.Copy(_glyphSelections, glyph * words, _selection, 0, words);
            return true;
        }

        /// <summary>
        /// Emits one layer-major contribution: operation <paramref name="operationIndex"/> when selected, or with -1 the
        /// native face where no fused contribution is selected.
        /// </summary>
        void EmitSelected(in TextStudioFrame frame, TMP_Text text, int sourceIndex, int operationIndex,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier, int mappingGlyph = -1)
        {
            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            if (operationIndex >= 0)
            {
                if (IsSelected(operationIndex))
                    Emit(in frame, text, sourceIndex, operationIndex, 0, 0, materials, applier, mappingGlyph);
                return;
            }
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (IsSelected(index) && operation.ExecutionPath == TextStudio2DExecutionPath.FusedField &&
                    operation.SurfaceOwnerOperationIndex < 0 && operation.Kind != TextStudio2DLayerKind.ColorAdjustment) return;
            }
            Emit(in frame, text, sourceIndex, -1, 0, 0, materials, applier, mappingGlyph);
        }

        int BeginRun(Material material)
        {
            int start = _geometry.Vertices.Count;
            if (_runs.Count == 0 || _runs[_runs.Count - 1].Material != material ||
                start - _runs[_runs.Count - 1].VertexStart >= MaximumUiRunVertices)
            {
                _runs.Add(new Run(material, start, _geometry.Indices.Count));
                _materials.Add(material);
            }
            return start;
        }

        void EndQuad()
        {
            Run run = _runs[_runs.Count - 1];
            run.VertexCount += 4; run.IndexCount += 6;
            _runs[_runs.Count - 1] = run;
        }

        /// <summary>
        /// Emits one primitive. <paramref name="operationIndex"/> selects one operation, -1 the native face alone, or -2
        /// the operations of <paramref name="span"/> in <paramref name="spanMask"/> as one fused primitive.
        /// </summary>
        void Emit(in TextStudioFrame frame, TMP_Text text, int sourceIndex, int operationIndex, int spanMask, int span,
            TextStudioTMPMaterialEffectAdapter materials, TextStudioTMPFrameApplier applier, int mappingGlyph)
        {
            if (_external != null) { EmitExternal(in frame, sourceIndex, operationIndex, spanMask, span, mappingGlyph); return; }
            ref readonly TextStudioVisualGlyph source = ref frame.VisualSnapshot.RenderGlyphAt(sourceIndex);
            if (!source.IsVisible || source.VisualIndex < 0 || source.VisualIndex >= text.textInfo.characterCount) return;
            ref readonly TMP_CharacterInfo character = ref text.textInfo.characterInfo[source.VisualIndex];
            int materialIndex = character.materialReferenceIndex;
            ref readonly TMP_MeshInfo mesh = ref text.textInfo.meshInfo[materialIndex];
            int vertexIndex = character.vertexIndex;
            Material material = materials.ResolveRenderingMaterial(text, materialIndex);
            if (material == null || mesh.vertices == null || vertexIndex < 0 || vertexIndex + 3 >= mesh.vertices.Length) return;
            float identity = operationIndex == -2
                ? TextStudio2DStyleIdentity.Pack(_styleSlot, spanMask, span)
                : TextStudio2DStyleIdentity.PackOperation(_styleSlot, operationIndex);
            TextStudio2DPrimitiveSupport.Support support = TextStudio2DPrimitiveSupport.ResolveSupport(frame.AppearanceStyle, operationIndex, spanMask, span);
            Texture atlas = TextStudio2DPrimitiveSupport.ResolveAtlas(in character, in source, material);
            // Local 0..1 keeps spanning the source quad; the primitive extends past it to cover translated shadows and depth.
            Vector4 bounds = TextStudio2DPrimitiveSupport.ResolveLocalBounds(in source, in character, in mesh, vertexIndex, atlas, in support);
            // Payload1.y is a per-vertex sweep distance; the remaining payload channels are constant per quad.
            applier.TryGetMaterialPayload(materialIndex, vertexIndex, out _, out Vector4 payload1BottomLeft, out _);
            applier.TryGetMaterialPayload(materialIndex, vertexIndex + 1, out _, out Vector4 payload1TopLeft, out _);
            applier.TryGetMaterialPayload(materialIndex, vertexIndex + 2, out _, out Vector4 payload1TopRight, out _);
            applier.TryGetMaterialPayload(materialIndex, vertexIndex + 3, out _, out Vector4 payload1BottomRight, out _);
            int start = BeginRun(material);
            for (int corner = 0; corner < 4; corner++)
            {
                int vertex = vertexIndex + corner;
                float x = corner >= 2 ? bounds.z : bounds.x;
                float y = corner == 1 || corner == 2 ? bounds.w : bounds.y;
                applier.TryGetMaterialPayload(materialIndex, vertex, out Vector4 payload0, out _, out Vector4 local);
                local.x = x;
                local.y = y;
                if (mappingGlyph >= 0)
                {
                    float sign = local.z < 0 ? -1 : 1;
                    float pixels = Mathf.Abs(local.z) % 128;
                    local.z = ((mappingGlyph + 1) * 128 + pixels) * sign;
                }
                Vector4 uv0 = TextStudio2DPrimitiveSupport.Bilinear(
                    TextStudio2DPrimitiveSupport.ToVector4(mesh.uvs0[vertexIndex]), TextStudio2DPrimitiveSupport.ToVector4(mesh.uvs0[vertexIndex + 1]),
                    TextStudio2DPrimitiveSupport.ToVector4(mesh.uvs0[vertexIndex + 2]), TextStudio2DPrimitiveSupport.ToVector4(mesh.uvs0[vertexIndex + 3]), x, y);
                uv0.z = identity;
                Vector2 squiggle = applier.GetSquigglePayload(materialIndex, vertex);
                Vector2 materialUv = TextStudio2DPrimitiveSupport.Bilinear(mesh.uvs2[vertexIndex], mesh.uvs2[vertexIndex + 1], mesh.uvs2[vertexIndex + 2], mesh.uvs2[vertexIndex + 3], x, y);
                Vector3 normal = TextStudio2DPrimitiveSupport.Bilinear(
                    applier.GetSourceCoordinateNormal(materialIndex, vertexIndex), applier.GetSourceCoordinateNormal(materialIndex, vertexIndex + 1),
                    applier.GetSourceCoordinateNormal(materialIndex, vertexIndex + 2), applier.GetSourceCoordinateNormal(materialIndex, vertexIndex + 3), x, y);
                // The Writing sample index in normal.z is constant per quad and must not be interpolated.
                normal.z = applier.GetSourceCoordinateNormal(materialIndex, vertex).z;
                _geometry.Vertices.Add(TextStudio2DPrimitiveSupport.Bilinear(
                    mesh.vertices[vertexIndex], mesh.vertices[vertexIndex + 1], mesh.vertices[vertexIndex + 2], mesh.vertices[vertexIndex + 3], x, y));
                _geometry.Normals.Add(normal);
                _geometry.Colors.Add(TextStudio2DPrimitiveSupport.Bilinear(
                    mesh.colors32[vertexIndex], mesh.colors32[vertexIndex + 1], mesh.colors32[vertexIndex + 2], mesh.colors32[vertexIndex + 3], x, y));
                _geometry.Uv0.Add(uv0);
                _geometry.Uv1.Add(new Vector4(materialUv.x, materialUv.y, squiggle.x, squiggle.y));
                _geometry.Uv2.Add(payload0);
                _geometry.Uv3.Add(TextStudio2DPrimitiveSupport.Bilinear(payload1BottomLeft, payload1TopLeft, payload1TopRight, payload1BottomRight, x, y));
                _geometry.Local.Add(local);
            }
            _geometry.Indices.Add(start); _geometry.Indices.Add(start + 1); _geometry.Indices.Add(start + 2);
            _geometry.Indices.Add(start + 2); _geometry.Indices.Add(start + 3); _geometry.Indices.Add(start);
            EndQuad();
        }

        /// <summary>
        /// Emits one primitive of an engine-published quad. Native coordinates are em: the glyph's mapping row names the
        /// engine atlas entry the field is read from, and uv0.w converts field pixels to object units like TMP's scale.
        /// </summary>
        void EmitExternal(in TextStudioFrame frame, int sourceIndex, int operationIndex, int spanMask, int span, int mappingGlyph)
        {
            ref readonly TextStudioVisualGlyph glyph = ref frame.VisualSnapshot.RenderGlyphAt(sourceIndex);
            if (!glyph.IsVisible || (uint)sourceIndex >= (uint)_external.Count) return;
            ref readonly TextStudio2DExternalGlyphSource.Quad quad = ref _external.GetQuad(sourceIndex);
            ref readonly TextStudio2DExternalGlyphSource.Field field = ref _external.GetField(sourceIndex);
            if (!quad.IsPresent || !field.IsValid) return;
            Material material = _external.ResolveMaterial(field.Bank);
            if (material == null) return;
            float identity = operationIndex == -2
                ? TextStudio2DStyleIdentity.Pack(_styleSlot, spanMask, span)
                : TextStudio2DStyleIdentity.PackOperation(_styleSlot, operationIndex);
            TextStudio2DPrimitiveSupport.Support support = TextStudio2DPrimitiveSupport.ResolveSupport(frame.AppearanceStyle, operationIndex, spanMask, span);
            const float pixelsPerEm = TextStudio2DExternalGlyphSource.FieldPixelsPerEm;
            Vector4 em = quad.FieldEm;
            Vector4 covered = new Vector4(Mathf.Max(0, field.GlyphEm.x - em.x), Mathf.Max(0, field.GlyphEm.y - em.y),
                Mathf.Max(0, em.z - field.GlyphEm.z), Mathf.Max(0, em.w - field.GlyphEm.w)) * pixelsPerEm;
            Vector4 bounds = TextStudio2DPrimitiveSupport.ResolveLocalBounds(in support, new Vector2(em.x, em.y) * pixelsPerEm,
                new Vector2(em.z, em.w) * pixelsPerEm, covered, pixelsPerEm);
            // The quad's own edge measures object units per em, so scaled and rotated glyphs keep their edge width.
            float objectPerEm = Vector3.Distance(quad.Position0, quad.Position1) / Mathf.Max(.000001f, Mathf.Abs(em.w - em.y));
            float scale = objectPerEm / pixelsPerEm * Mathf.Abs(_external.Host != null ? _external.Host.lossyScale.y : 1f);
            float packedGlyph = ((mappingGlyph >= 0 ? mappingGlyph : sourceIndex) + 1) * 128f;
            // Source coordinates span the unanimated text; primitives beyond the quad extrapolate them.
            Vector4 sourceBounds = frame.VisualSnapshot.SourceBounds;
            Vector2 sourceSize = new Vector2(sourceBounds.z - sourceBounds.x, sourceBounds.w - sourceBounds.y);
            Vector2 sourceMinimum = NormalizeSource(new Vector2(field.SourceRect.x, field.SourceRect.y), sourceBounds, sourceSize);
            Vector2 sourceMaximum = NormalizeSource(new Vector2(field.SourceRect.z, field.SourceRect.w), sourceBounds, sourceSize);
            int start = BeginRun(material);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner >= 2 ? bounds.z : bounds.x;
                float y = corner == 1 || corner == 2 ? bounds.w : bounds.y;
                _geometry.Vertices.Add(TextStudio2DPrimitiveSupport.Bilinear(quad.Position0, quad.Position1, quad.Position2, quad.Position3, x, y));
                _geometry.Normals.Add(new Vector3(Mathf.LerpUnclamped(sourceMinimum.x, sourceMaximum.x, x),
                    Mathf.LerpUnclamped(sourceMinimum.y, sourceMaximum.y, y), -1f));
                _geometry.Colors.Add(TextStudio2DPrimitiveSupport.Bilinear(quad.Color0, quad.Color1, quad.Color2, quad.Color3, x, y));
                _geometry.Uv0.Add(new Vector4(Mathf.LerpUnclamped(em.x, em.z, x), Mathf.LerpUnclamped(em.y, em.w, y), identity, scale));
                _geometry.Uv1.Add(new Vector4(x, y, 0f, 0f));
                _geometry.Uv2.Add(Vector4.zero);
                _geometry.Uv3.Add(Vector4.zero);
                _geometry.Local.Add(new Vector4(x, y, packedGlyph, 0f));
            }
            _geometry.Indices.Add(start); _geometry.Indices.Add(start + 1); _geometry.Indices.Add(start + 2);
            _geometry.Indices.Add(start + 2); _geometry.Indices.Add(start + 3); _geometry.Indices.Add(start);
            EndQuad();
        }

        static Vector2 NormalizeSource(Vector2 position, Vector4 bounds, Vector2 size) => new Vector2(
            Mathf.Abs(size.x) <= .000001f ? .5f : Mathf.Clamp01((position.x - bounds.x) / size.x),
            Mathf.Abs(size.y) <= .000001f ? .5f : Mathf.Clamp01((position.y - bounds.y) / size.y));

        void SynchronizeWorld(TMP_Text text, bool rebuild)
        {
            rebuild |= EnsureWorldOutput();
            MeshRenderer source = text.GetComponent<MeshRenderer>();
            if (source != null)
            {
                TextStudioSurfaceGroup.ResolveWorldSorting(text, source, out int layer, out int order);
                _worldRenderer.sortingLayerID = layer;
                _worldRenderer.sortingOrder = order;
                _worldRenderer.renderingLayerMask = source.renderingLayerMask;
                _worldRenderer.shadowCastingMode = source.shadowCastingMode;
            }
            PublishWorldOutput(rebuild);
        }

        /// <summary>Sorts engine-owned output where the engine's own standalone renderer would.</summary>
        void SynchronizeExternalWorld(TextStudio2DExternalGlyphSource source, bool rebuild)
        {
            rebuild |= EnsureWorldOutput();
            _worldRenderer.sortingLayerID = source.SortingLayerId;
            _worldRenderer.sortingOrder = source.SortingOrder;
            _worldRenderer.shadowCastingMode = source.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            PublishWorldOutput(rebuild);
            if (source.Suppressed) _worldRenderer.enabled = false;
        }

        bool EnsureWorldOutput()
        {
            TextStudioCanvasShaderChannelLeases.Release(ref _canvasLease);
            foreach (TextStudio2DOrderedGraphic graphic in _graphics)
                if (graphic != null) graphic.HideOutput();
            bool created = false;
            if (_worldObject == null)
            {
                _worldObject = new GameObject("_TSOrderedOutput") { hideFlags = HideFlags.HideAndDontSave };
                _worldObject.transform.SetParent(_owner.transform, false);
                _worldMesh = new Mesh { name = "Text Studio Ordered Appearance", hideFlags = HideFlags.HideAndDontSave };
                _worldMesh.MarkDynamic();
                _worldObject.AddComponent<MeshFilter>().sharedMesh = _worldMesh;
                _worldRenderer = _worldObject.AddComponent<MeshRenderer>();
                _worldRenderer.receiveShadows = false;
                _worldRenderer.lightProbeUsage = LightProbeUsage.Off;
                _worldRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                created = true;
            }
            _worldObject.layer = _owner.gameObject.layer;
            return created;
        }

        void PublishWorldOutput(bool rebuild)
        {
            if (rebuild)
            {
                GeometryUploadCount++;
                if (_worldSuppressed) _worldMeshStale = true;
                else UploadWorldMesh();
            }
            _worldRenderer.enabled = !_worldSuppressed;
        }

        void UploadWorldMesh()
        {
            _worldMeshStale = false;
            _worldMesh.Clear();
            _worldMesh.indexFormat = _geometry.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _worldMesh.SetVertices(_geometry.Vertices);
            _worldMesh.SetNormals(_geometry.Normals);
            _worldMesh.SetColors(_geometry.Colors);
            _worldMesh.SetUVs(0, _geometry.Uv0);
            _worldMesh.SetUVs(1, _geometry.Uv1);
            _worldMesh.SetUVs(2, _geometry.Uv2);
            _worldMesh.SetUVs(3, _geometry.Uv3);
            _worldMesh.SetUVs(4, _geometry.Local);
            _worldMesh.subMeshCount = _runs.Count;
            for (int index = 0; index < _runs.Count; index++)
            {
                Run run = _runs[index];
                _worldMesh.SetTriangles(_geometry.Indices, run.IndexStart, run.IndexCount, index, false);
            }
            _worldMesh.RecalculateBounds();
            _worldRenderer.SetSharedMaterials(_materials);
        }

        /// <summary>Publishes one UI graphic per run under <paramref name="parent"/>, the TMP text or an engine's render root.</summary>
        void SynchronizeUi(Transform parent, TextMeshProUGUI text, bool rebuild, TextStudio2DExternalGlyphSource external = null)
        {
            if (_worldRenderer != null) _worldRenderer.enabled = false;
            Canvas canvas = text != null ? text.canvas : parent.GetComponentInParent<Canvas>();
            Vector2 pivot = text != null ? text.rectTransform.pivot : parent is RectTransform root ? root.pivot : new Vector2(.5f, .5f);
            TextStudioCanvasShaderChannelLeases.Acquire(canvas,
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent |
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3, ref _canvasLease);
            for (int index = 0; index < _runs.Count; index++)
            {
                bool created = index == _graphics.Count || _graphics[index] == null;
                if (created)
                {
                    GameObject host = new GameObject("_TSOrderedOutputUI", typeof(RectTransform), typeof(CanvasRenderer)) { hideFlags = HideFlags.HideAndDontSave };
                    host.transform.SetParent(parent, false);
                    host.AddComponent<LayoutElement>().ignoreLayout = true;
                    TextStudio2DOrderedGraphic graphic = host.AddComponent<TextStudio2DOrderedGraphic>();
                    graphic.raycastTarget = false;
                    if (external != null) graphic.canvasRenderer.SetColor(external.CanvasColor);
                    if (index == _graphics.Count) _graphics.Add(graphic);
                    else _graphics[index] = graphic;
                }
                TextStudio2DOrderedGraphic output = _graphics[index];
                output.gameObject.layer = parent.gameObject.layer;
                RectTransform rect = output.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.pivot = pivot; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                int sibling = parent.childCount - _graphics.Count + index;
                if (rect.GetSiblingIndex() != sibling) rect.SetSiblingIndex(sibling);
                if (rebuild || created) output.Bind(_geometry, _runs[index]);
                output.RefreshPayloadTransform();
                output.canvasRenderer.cull = false;
                output.enabled = external == null || !external.Suppressed;
            }
            for (int index = _runs.Count; index < _graphics.Count; index++)
                if (_graphics[index] != null) _graphics[index].HideOutput();
            if (rebuild) GeometryUploadCount++;
        }

        /// <summary>Tweens UI outputs with a Canvas color or alpha fade of the source they draw for.</summary>
        internal void CrossFade(Color color, float duration, bool ignoreTimeScale, bool useAlpha, bool useRgb)
        {
            foreach (TextStudio2DOrderedGraphic graphic in _graphics)
                if (graphic != null && graphic.enabled) graphic.CrossFadeColor(color, duration, ignoreTimeScale, useAlpha, useRgb);
        }

        /// <summary>Hides every output and forces the next render to rebuild and publish again.</summary>
        internal void HideOutput()
        {
            Hide();
            _hasBinding = false;
        }

        void Hide()
        {
            if (_worldRenderer != null) _worldRenderer.enabled = false;
            foreach (TextStudio2DOrderedGraphic graphic in _graphics)
                if (graphic != null) graphic.HideOutput();
            TextStudioCanvasShaderChannelLeases.Release(ref _canvasLease);
        }

        public void Dispose()
        {
            foreach (TextStudio2DStyleDecorations decorations in _occurrenceDecorations) decorations?.Dispose();
            _occurrenceDecorations.Clear();
            TextStudioCanvasShaderChannelLeases.Release(ref _canvasLease);
            foreach (TextStudio2DOrderedGraphic graphic in _graphics)
                if (graphic != null) Destroy(graphic.gameObject);
            _graphics.Clear();
            Destroy(_worldMesh); Destroy(_worldObject);
            _worldMesh = null; _worldObject = null; _worldRenderer = null;
            _geometry.Clear(); _runs.Clear(); _materials.Clear(); _hasBinding = false; _active = false;
        }

        static void Destroy(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        internal struct Run
        {
            internal readonly Material Material;
            internal readonly int VertexStart, IndexStart;
            internal int VertexCount, IndexCount;
            internal Run(Material material, int vertexStart, int indexStart)
            { Material = material; VertexStart = vertexStart; IndexStart = indexStart; VertexCount = 0; IndexCount = 0; }
        }

        internal sealed class Geometry
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector3> Normals = new List<Vector3>();
            internal readonly List<Color32> Colors = new List<Color32>();
            internal readonly List<Vector4> Uv0 = new List<Vector4>();
            internal readonly List<Vector4> Uv1 = new List<Vector4>();
            internal readonly List<Vector4> Uv2 = new List<Vector4>();
            internal readonly List<Vector4> Uv3 = new List<Vector4>();
            internal readonly List<Vector4> Local = new List<Vector4>();
            internal readonly List<int> Indices = new List<int>();
            internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ListBytes(Vertices) +
                TextStudioAppearanceManagedMemory.ListBytes(Normals) +
                TextStudioAppearanceManagedMemory.ListBytes(Colors) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv0) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv1) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv2) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv3) +
                TextStudioAppearanceManagedMemory.ListBytes(Local) +
                TextStudioAppearanceManagedMemory.ListBytes(Indices);

            internal void Clear()
            { Vertices.Clear(); Normals.Clear(); Colors.Clear(); Uv0.Clear(); Uv1.Clear(); Uv2.Clear(); Uv3.Clear(); Local.Clear(); Indices.Clear(); }
        }
    }

    [AddComponentMenu("")]
    internal sealed class TextStudio2DOrderedGraphic : MaskableGraphic
    {
        TextStudio2DOrderedRenderer.Geometry _geometry;
        TextStudio2DOrderedRenderer.Run _run;
        Matrix4x4 _payloadTransform = Matrix4x4.identity;
        internal int DebugVertexCount => _run.VertexCount;
        public override Texture mainTexture => _run.Material != null ? _run.Material.mainTexture : Texture2D.whiteTexture;

        internal void Bind(TextStudio2DOrderedRenderer.Geometry geometry, TextStudio2DOrderedRenderer.Run run)
        {
            _geometry = geometry; _run = run;
            if (material != run.Material) material = run.Material;
            SetVerticesDirty();
        }

        internal void HideOutput()
        {
            enabled = false;
            canvasRenderer.cull = true;
            canvasRenderer.Clear();
        }

        internal void RefreshPayloadTransform()
        {
            Matrix4x4 matrix = TextStudioCanvasPayloadSpace.Resolve(transform, canvas);
            if (_payloadTransform.Equals(matrix)) return;
            _payloadTransform = matrix;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_geometry == null) return;
            _payloadTransform = TextStudioCanvasPayloadSpace.Resolve(transform, canvas);
            for (int vertex = _run.VertexStart; vertex < _run.VertexStart + _run.VertexCount; vertex++)
                helper.AddVert(new UIVertex { position = _geometry.Vertices[vertex], normal = _payloadTransform.MultiplyVector(_geometry.Normals[vertex]),
                    color = _geometry.Colors[vertex], uv0 = _geometry.Uv0[vertex], uv1 = _geometry.Uv1[vertex],
                    uv2 = _geometry.Uv2[vertex], uv3 = _geometry.Uv3[vertex], tangent = TextStudioCanvasPayloadSpace.Tangent(in _payloadTransform, _geometry.Local[vertex]) });
            for (int index = _run.IndexStart; index < _run.IndexStart + _run.IndexCount; index += 3)
                helper.AddTriangle(_geometry.Indices[index] - _run.VertexStart, _geometry.Indices[index + 1] - _run.VertexStart, _geometry.Indices[index + 2] - _run.VertexStart);
        }
    }
}
