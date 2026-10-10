using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal struct TextStudio2DStyleDecorationMaterialLease
    {
        internal Material Source;
        internal Texture Atlas;
        internal Material Replacement;
        internal bool Hero;

        internal bool HasIdentity => !ReferenceEquals(Replacement, null);
        internal bool IsValid =>
            HasIdentity &&
            Replacement != null &&
            TextStudio2DShaderResources.IsUsable(Replacement.shader, requiresDataTables: true);
    }

#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DStyleDecorationMaterialCache
    {
        static readonly Dictionary<CacheKey, CacheEntry> s_entries =
            new Dictionary<CacheKey, CacheEntry>();
        static bool s_missingShaderWarningLogged;
        static readonly int s_decorationHeroProperty = Shader.PropertyToID("_TextStudio2DDecorationHero");

        readonly struct CacheKey : IEquatable<CacheKey>
        {
            internal readonly Material Source;
            internal readonly Texture Atlas;
            internal readonly bool Hero;

            internal CacheKey(Material source, Texture atlas, bool hero)
            {
                Source = source;
                Atlas = atlas;
                Hero = hero;
            }

            public bool Equals(CacheKey other)
            {
                return ReferenceEquals(Source, other.Source) &&
                       ReferenceEquals(Atlas, other.Atlas) && Hero == other.Hero;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int sourceHash = !ReferenceEquals(Source, null)
                        ? RuntimeHelpers.GetHashCode(Source)
                        : 0;
                    int atlasHash = !ReferenceEquals(Atlas, null)
                        ? RuntimeHelpers.GetHashCode(Atlas)
                        : 0;
                    return (sourceHash * 397 ^ atlasHash) * 397 ^ (Hero ? 1 : 0);
                }
            }
        }

        sealed class CacheEntry
        {
            internal readonly CacheKey Key;
            internal readonly Material Material;
            internal int ReferenceCount;
            internal int SourceCrc;

            internal CacheEntry(CacheKey key, Material material, int sourceCrc)
            {
                Key = key;
                Material = material;
                SourceCrc = sourceCrc;
                ReferenceCount = 0;
            }
        }

        internal static int ActiveEntryCount => s_entries.Count;

        internal static TextStudio2DStyleDecorationMaterialLease Acquire(
            Material source,
            Texture atlas, bool hero = false)
        {
            CacheKey key = new CacheKey(source, atlas, hero);
            int sourceCrc = source != null ? source.ComputeCRC() : 0;
            if (!TryGetOrCreate(in key, sourceCrc, out CacheEntry entry))
                return default;
            entry.ReferenceCount++;
            Synchronize(entry);
            return new TextStudio2DStyleDecorationMaterialLease
            {
                Source = source,
                Atlas = atlas,
                Hero = hero,
                Replacement = entry.Material
            };
        }

        internal static void Synchronize(
            in TextStudio2DStyleDecorationMaterialLease lease)
        {
            if (!lease.IsValid)
                return;
            CacheKey key = new CacheKey(lease.Source, lease.Atlas, lease.Hero);
            if (s_entries.TryGetValue(key, out CacheEntry entry) &&
                ReferenceEquals(entry.Material, lease.Replacement))
            {
                Synchronize(entry);
            }
        }

        internal static void Release(
            ref TextStudio2DStyleDecorationMaterialLease lease)
        {
            if (!lease.HasIdentity)
            {
                lease = default;
                return;
            }
            CacheKey key = new CacheKey(lease.Source, lease.Atlas, lease.Hero);
            if (s_entries.TryGetValue(key, out CacheEntry entry) &&
                ReferenceEquals(entry.Material, lease.Replacement))
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount <= 0)
                {
                    s_entries.Remove(key);
                    Destroy(entry.Material);
                }
            }
            lease = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetRuntimeState()
        {
            Reset();
        }

        internal static void ResetForTests()
        {
            Reset();
        }

        static void Reset()
        {
            foreach (KeyValuePair<CacheKey, CacheEntry> pair in s_entries)
                Destroy(pair.Value.Material);
            s_entries.Clear();
            s_missingShaderWarningLogged = false;
        }

        static bool TryGetOrCreate(
            in CacheKey key,
            int sourceCrc,
            out CacheEntry entry)
        {
            if (s_entries.TryGetValue(key, out entry) &&
                entry.Material != null &&
                TextStudio2DShaderResources.IsUsable(entry.Material.shader, requiresDataTables: true))
                return true;
            if (entry != null)
            {
                s_entries.Remove(key);
                Destroy(entry.Material);
            }

            Shader shader = TextStudio2DShaderResources.LoadSupported(
                TextStudio2DResourceKeys.StyleDecorationShader,
                "retained 2D decorations",
                ref s_missingShaderWarningLogged,
                out string _);
            if (shader == null)
            {
                entry = null;
                return false;
            }

            Material replacement = key.Source != null
                ? new Material(key.Source)
                : new Material(shader);
            replacement.name = key.Source != null
                ? key.Source.name + " (Text Studio 2D Retained)"
                : "Text Studio 2D Retained Style";
            replacement.shader = shader;
            replacement.hideFlags = HideFlags.HideAndDontSave;
            Configure(in key, replacement);
            entry = new CacheEntry(key, replacement, sourceCrc);
            s_entries.Add(key, entry);
            return true;
        }

        static void Synchronize(CacheEntry entry)
        {
            if (entry.Key.Source == null || entry.Material == null)
                return;
            int sourceCrc = entry.Key.Source.ComputeCRC();
            if (sourceCrc == entry.SourceCrc)
                return;
            Configure(in entry.Key, entry.Material);
            entry.SourceCrc = sourceCrc;
        }

        static void Configure(in CacheKey key, Material replacement)
        {
            Shader shader = replacement.shader;
            string name = replacement.name;
            if (key.Source != null)
                replacement.CopyPropertiesFromMaterial(key.Source);
            replacement.shader = shader;
            replacement.name = name;
            replacement.hideFlags = HideFlags.HideAndDontSave;
            if (replacement.HasProperty("_MainTex"))
            {
                replacement.SetTexture(
                    "_MainTex",
                    key.Atlas != null ? key.Atlas : Texture2D.whiteTexture);
            }
            if (key.Atlas != null)
            {
                if (replacement.HasProperty("_TextureWidth"))
                    replacement.SetFloat("_TextureWidth", Mathf.Max(1, key.Atlas.width));
                if (replacement.HasProperty("_TextureHeight"))
                    replacement.SetFloat("_TextureHeight", Mathf.Max(1, key.Atlas.height));
            }
            // Hero coverage replays share the style table; a material value selects their per-operation filtering.
            replacement.SetFloat(s_decorationHeroProperty, key.Hero ? 1f : 0f);
            replacement.DisableKeyword("OUTLINE_ON");
            replacement.DisableKeyword("UNDERLAY_ON");
            replacement.DisableKeyword("UNDERLAY_INNER");
        }

        static void Destroy(Material material)
        {
            if (material == null)
                return;
            TextStudio2DHeroSubmission.Flush();
            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }
    }

    internal sealed partial class TextStudio2DStyleDecorations : IDisposable
    {
        const float MinimumExtent = 0.0001f;

        readonly bool _hero;
        internal int GeometryRevision { get; private set; }
        readonly List<StyleBatch> _batches = new List<StyleBatch>(4);
        readonly List<DecorationRun> _decorationRuns = new List<DecorationRun>(8);
        readonly List<int> _runMembers = new List<int>();

        bool[] _selected = Array.Empty<bool>();
        int _selectionStart, _selectionEnd;
        bool IsSelected(int unit) => unit >= _selectionStart && unit < _selectionEnd && _selected[unit - _selectionStart];

        static int MappingGlyphIndex(in TextStudioFrame frame, int renderGlyph) => frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
            ? frame.AppearanceBinding.GetMappingGlyphIndex(frame.AppearanceOccurrenceIndex, renderGlyph) : renderGlyph;

        static IReadOnlyList<int> ScopedRenderGlyphs(in TextStudioFrame frame) => frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
            ? frame.AppearanceBinding.GetOccurrenceRenderGlyphs(frame.AppearanceOccurrenceIndex) : null;
        int _activeBatches;

        internal long ManagedPayloadBytes
        {
            get
            {
                long bytes = TextStudioAppearanceManagedMemory.ListBytes(_decorationRuns) + TextStudioAppearanceManagedMemory.ListBytes(_runMembers) +
                    TextStudioAppearanceManagedMemory.ArrayBytes(_selected) + (long)_batches.Capacity * IntPtr.Size;
                bytes += TextStudioAppearanceManagedMemory.ListBytes(_surfaceBands) + TextStudioAppearanceManagedMemory.ListBytes(_surfaceMembers) +
                    TextStudioAppearanceManagedMemory.ListBytes(_nativeInkSources) + _ownedBinding.ManagedPayloadBytes +
                    (long)(_surfaces.Capacity + _inkShapes.Capacity + _unusedStrokes.Capacity + 2 * _strokeSurfaces.EnsureCapacity(0)) * IntPtr.Size;
                foreach (Vector2[] shape in _inkShapes) bytes += TextStudioAppearanceManagedMemory.ArrayBytes(shape);
                foreach (ConnectedSurface surface in _surfaces)
                    if (surface.Geometry != null) bytes += TextStudioAppearanceManagedMemory.ArrayBytes(surface.Geometry.Edges) +
                        TextStudioAppearanceManagedMemory.ArrayBytes(surface.Geometry.NativeInkSources);
                bytes += (long)_surfacePlacementRows.Capacity * IntPtr.Size + TextStudioAppearanceManagedMemory.ListBytes(_surfaceColumns) + TextStudioAppearanceManagedMemory.ListBytes(_surfaceRowCoordinates);
                foreach (SurfacePlacementRow row in _surfacePlacementRows) bytes += TextStudioAppearanceManagedMemory.ListBytes(row.Glyphs);
                foreach (StyleBatch batch in _batches) bytes += batch.Geometry.ManagedPayloadBytes;
                return bytes;
            }
        }

        internal int DebugVertexCount => CountVertices(
            _batches,
            _activeBatches);
        internal int DebugBatchCount => _activeBatches;
        internal ulong GeometryFingerprint { get; private set; }
        internal ulong ContourPrecisionFingerprint { get; private set; }
        internal int StyleSlot { get; private set; }
        internal bool IsEmpty => _activeBatches == 0;

        internal TextStudio2DStyleDecorations(bool hero = false)
        {
            _hero = hero;
        }

        internal void Rebuild(
            in TextStudioFrame frame,
            TMP_Text tmpText,
            int styleSlot,
            TextStudio2DBoundStyle binding = null,
            TextStudioPreparedFieldDemand sourceDemand = null,
            TextStudioTMPFrameApplier frameApplier = null)
        {
            BeginBatches();
            GeometryRevision++;

            CompiledTextStudio2DStyle style = frame.AppearanceStyle;
            GeometryFingerprint = style?.GeometryFingerprint ?? 0;
            ContourPrecisionFingerprint = sourceDemand?.GeometryFingerprint ?? 0;
            StyleSlot = styleSlot;
            _binding = binding ?? _ownedBinding;
            _binding.Bind(style);
            _activeSurfaces = 0;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            if (tmpText == null || style == null || styleSlot <= 0 || !snapshot.IsCreated)
            {
                EndBatches();
                return;
            }

            int glyphCount = snapshot.GlyphCount;
            if (glyphCount <= 0)
            {
                EndBatches();
                return;
            }
            _selectionStart = 0; _selectionEnd = glyphCount;
            if (frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null)
            {
                TextStudioAppearanceRange domain = frame.AppearanceBinding.Plan.Occurrences[frame.AppearanceOccurrenceIndex].Domain;
                _selectionStart = domain.Start; _selectionEnd = Mathf.Min(glyphCount, domain.End);
            }
            EnsureSelectionCapacity(_selectionEnd - _selectionStart);

            for (int operationIndex = style.OperationCount - 1; operationIndex >= 0; operationIndex--)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
                if (_hero ? operation.Kind != TextStudio2DLayerKind.Decoration && !UsesContourStroke(in operation) :
                    operation.ExecutionPath != TextStudio2DExecutionPath.RetainedGeometry)
                {
                    continue;
                }

                if (operation.SurfaceOwnerOperationIndex >= 0) continue;
                PopulateSelection(style, in operation, in frame, in snapshot, glyphCount);
                if (UsesContourStroke(in operation))
                {
                    EmitSelectedStrokeGlyphs(in frame, in snapshot, tmpText, in operation, operationIndex, styleSlot, sourceDemand, frameApplier);
                }
                else if (operation.Kind == TextStudio2DLayerKind.Decoration)
                {
                    StyleBatch target = GetDecorationBatch(_batches, ref _activeBatches);
                    if (target == null)
                        continue;
                    EmitSelectedRuns(
                        target.Geometry,
                        in frame,
                        in snapshot,
                        tmpText,
                        in operation,
                        operationIndex,
                        styleSlot,
                        glyphCount, sourceDemand, frameApplier);
                }
                else if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
                {
                    EmitSelectedDepthGlyphs(
                        in frame,
                        in snapshot,
                        tmpText,
                        in operation,
                        operationIndex,
                        styleSlot,
                        glyphCount);
                }
            }
            EndBatches();

            if (_hero)
            {
                for (int index = 0; index < _activeBatches; index++) _batches[index].UploadHero();
                return;
            }
        }

        internal void IncludeHeroBounds(ref Vector2 minimum, ref Vector2 maximum, bool[] operations)
        {
            IncludeHeroBounds(ref minimum, ref maximum, operations, Matrix4x4.identity);
        }

        internal void IncludeHeroBounds(ref Vector2 minimum, ref Vector2 maximum, bool[] operations, Matrix4x4 projection)
        {
            for (int index = 0; index < _activeBatches; index++)
            {
                StyleBatch batch = _batches[index];
                foreach (Vector3 vertex in batch.Geometry.Vertices)
                {
                    Vector3 projected = projection.MultiplyPoint(vertex);
                    minimum = Vector2.Min(minimum, projected);
                    maximum = Vector2.Max(maximum, projected);
                }
                for (int operation = 0; operation < operations.Length; operation++)
                    if ((batch.HeroOwners & (1UL << operation)) != 0) operations[operation] = true;
            }
        }

        internal int DrawHero(CommandBuffer commands, int ownerIndex, bool unionCoverage = false)
        {
            return DrawHero(commands, ownerIndex, _batches, _activeBatches, unionCoverage);
        }

        internal void AppendOrderedOperation(TextStudio2DOrderedRenderer renderer, int operationIndex)
        {
            for (int index = 0; index < _activeBatches; index++)
            {
                StyleBatch batch = _batches[index];
                renderer.AppendRetainedOperation(batch.Geometry, batch.Material, operationIndex);
            }
        }

        static int DrawHero(CommandBuffer commands, int ownerIndex, List<StyleBatch> batches, int count, bool unionCoverage = false)
        {
            int draws = 0;
            for (int index = 0; index < count; index++)
            {
                StyleBatch batch = batches[index];
                if (batch.HeroMesh == null || batch.Material == null || (batch.HeroOwners & (1UL << ownerIndex)) == 0) continue;
                commands.DrawMesh(batch.HeroMesh, Matrix4x4.identity, batch.Material, 0, unionCoverage ? 2 : 0);
                draws++;
            }
            return draws;
        }

        void BeginBatches()
        {
            BeginStrokes();
            _activeBatches = 0;
        }

        void EndBatches()
        {
            ReleaseUnusedStrokes();
            ReleaseUnusedSurfaces();
            TextStudio2DSurfaceTable.Shared.FlushAndBind();
            TrimTrailingEmptyBatches(_batches, ref _activeBatches);
            DeactivateUnusedBatches(_batches, _activeBatches);
        }

        static void TrimTrailingEmptyBatches(
            List<StyleBatch> batches,
            ref int activeCount)
        {
            while (activeCount > 0 &&
                   batches[activeCount - 1].Geometry.Vertices.Count == 0)
            {
                activeCount--;
            }
        }

        static void DeactivateUnusedBatches(List<StyleBatch> batches, int activeCount)
        {
            for (int index = activeCount; index < batches.Count; index++)
                batches[index].Deactivate();
        }

        static int CountVertices(List<StyleBatch> batches, int activeCount)
        {
            int count = 0;
            for (int index = 0; index < activeCount; index++)
                count += batches[index].Geometry.Vertices.Count;
            return count;
        }

        StyleBatch GetDecorationBatch(
            List<StyleBatch> batches,
            ref int activeCount)
        {
            if (activeCount > 0)
                return batches[activeCount - 1];
            return PrepareBatch(
                batches,
                ref activeCount,
                null,
                null,
                true, _hero);
        }

        static StyleBatch GetDepthBatch(
            List<StyleBatch> batches,
            ref int activeCount,
            Material source,
            Texture atlas)
        {
            if (activeCount > 0)
            {
                StyleBatch current = batches[activeCount - 1];
                if (current.Matches(source, atlas))
                    return current;
                if (current.DecorationOnly)
                {
                    if (!current.Configure(source, atlas, false, false))
                        return null;
                    return current;
                }
            }
            return PrepareBatch(
                batches,
                ref activeCount,
                source,
                atlas,
                false);
        }

        static StyleBatch PrepareBatch(
            List<StyleBatch> batches,
            ref int activeCount,
            Material source,
            Texture atlas,
            bool decorationOnly, bool hero = false)
        {
            StyleBatch batch;
            if (activeCount < batches.Count)
            {
                batch = batches[activeCount];
            }
            else
            {
                batch = new StyleBatch();
                batches.Add(batch);
            }
            if (!batch.Configure(source, atlas, decorationOnly, true, hero))
                return null;
            activeCount++;
            return batch;
        }

        void EmitSelectedDepthGlyphs(
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text tmpText,
            in TextStudio2DCompiledOperation operation,
            int operationIndex,
            int styleSlot,
            int glyphCount)
        {
            IReadOnlyList<int> renderGlyphs = ScopedRenderGlyphs(in frame);
            int renderCount = renderGlyphs?.Count ?? snapshot.RenderGlyphCount;
            for (int index = 0; index < renderCount; index++)
            {
                int glyphIndex = renderGlyphs != null ? renderGlyphs[index] : index;
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                if (!IsSelected(glyph.TextUnitIndex)) continue;
                if (!glyph.IsVisible ||
                    !TryResolveDepthSource(
                        tmpText,
                        in glyph,
                        out DepthSource source))
                {
                    continue;
                }

                StyleBatch target = GetDepthBatch(
                    _batches,
                    ref _activeBatches,
                    source.Material,
                    source.Atlas);
                if (target == null)
                    continue;
                EmitDepthGlyph(
                    target.Geometry,
                    in frame,
                    in snapshot,
                    in glyph,
                    glyphIndex,
                    in operation,
                    operationIndex,
                    styleSlot,
                    in source);
            }
        }

        static bool TryResolveDepthSource(
            TMP_Text tmpText,
            in TextStudioVisualGlyph glyph,
            out DepthSource source)
        {
            source = default;
            TMP_TextInfo textInfo = tmpText != null ? tmpText.textInfo : null;
            if (textInfo == null ||
                textInfo.characterInfo == null ||
                glyph.VisualIndex < 0 ||
                glyph.VisualIndex >= textInfo.characterCount)
            {
                return false;
            }

            ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[glyph.VisualIndex];
            Material material = character.material;
            if (material == null &&
                textInfo.meshInfo != null &&
                character.materialReferenceIndex >= 0 &&
                character.materialReferenceIndex < textInfo.meshInfo.Length)
            {
                material = textInfo.meshInfo[character.materialReferenceIndex].material;
            }
            if (material == null)
                material = tmpText.fontSharedMaterial;
            TMP_FontAsset font = character.fontAsset;
            Texture atlas = null;
            int atlasPage = glyph.AtlasPage;
            if (font != null &&
                font.atlasTextures != null &&
                atlasPage >= 0 &&
                atlasPage < font.atlasTextures.Length)
            {
                atlas = font.atlasTextures[atlasPage];
            }
            if (atlas == null && material != null)
                atlas = material.mainTexture;
            if (material == null || atlas == null)
                return false;

            float gradientScale = material.HasProperty("_GradientScale")
                ? Mathf.Max(1f, material.GetFloat("_GradientScale"))
                : Mathf.Max(1f, glyph.DistanceFieldScale);
            bool bold = (character.style & FontStyles.Bold) != 0;
            Vector2 bottomLeft = character.bottomLeft;
            Vector2 bottomRight = character.bottomRight;
            Vector2 topLeft = character.topLeft;
            if ((bottomRight - bottomLeft).sqrMagnitude < MinimumExtent * MinimumExtent ||
                (topLeft - bottomLeft).sqrMagnitude < MinimumExtent * MinimumExtent)
            {
                bottomLeft = glyph.BoundsMin;
                bottomRight = new Vector2(glyph.BoundsMax.x, glyph.BoundsMin.y);
                topLeft = new Vector2(glyph.BoundsMin.x, glyph.BoundsMax.y);
            }
            source = new DepthSource(
                material,
                atlas,
                gradientScale,
                bold,
                bottomLeft,
                bottomRight,
                topLeft);
            return true;
        }

        static void EmitDepthGlyph(
            GeometryBuffer target,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            in TextStudioVisualGlyph glyph,
            int glyphIndex,
            in TextStudio2DCompiledOperation operation,
            int operationIndex,
            int styleSlot,
            in DepthSource source)
        {
            TextStudio2DFauxDepthSpec depth = operation.FauxDepth;
            if (!depth.HasDepth)
                return;
            float effectUnitScale = frame.GetEffectUnitScale(glyph.TextUnitIndex);
            Vector2 emLocal = new Vector2(effectUnitScale / Mathf.Max(MinimumExtent, source.AxisX.magnitude),
                effectUnitScale / Mathf.Max(MinimumExtent, source.AxisY.magnitude));
            Vector2 offsetLocal = Vector2.Scale(depth.Offset, emLocal);
            float endScale = 1f - depth.ResolveTaper(depth.Offset.magnitude);
            Vector2 endCenterLocal = new Vector2(0.5f, 0.5f) + offsetLocal;
            Vector2 endHalfSizeLocal = Vector2.one * (0.5f * endScale);
            Vector2 softnessLocal = emLocal * (
                Mathf.Max(0f, depth.Softness) +
                Mathf.Max(0f, depth.SilhouetteOutset));
            Vector2 minimumLocal = Vector2.Min(
                Vector2.zero,
                endCenterLocal - endHalfSizeLocal) - softnessLocal;
            Vector2 maximumLocal = Vector2.Max(
                Vector2.one,
                endCenterLocal + endHalfSizeLocal) + softnessLocal;

            Vector3 bottomLeft = DepthPosition(
                minimumLocal.x,
                minimumLocal.y,
                in source);
            Vector3 bottomRight = DepthPosition(
                maximumLocal.x,
                minimumLocal.y,
                in source);
            Vector3 topLeft = DepthPosition(
                minimumLocal.x,
                maximumLocal.y,
                in source);
            Vector3 topRight = DepthPosition(
                maximumLocal.x,
                maximumLocal.y,
                in source);
            Vector3 sourceCenter = DepthPosition(0.5f, 0.5f, in source);

            EvaluatedGlyphState state = frame.Animated.IsCreated && glyph.TextUnitIndex < frame.AnimatedCount
                ? frame.Animated[glyph.TextUnitIndex]
                : EvaluatedGlyphState.Identity;
            GlyphPlacement placement = default;
            bool hasPlacement = frame.Placements.IsCreated && glyph.TextUnitIndex < frame.PlacementCount;
            if (hasPlacement)
                placement = frame.Placements[glyph.TextUnitIndex];
            TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.Create(
                sourceCenter,
                in state,
                in placement,
                hasPlacement,
                effectUnitScale,
                1f);
            Color32 sourceColor = frame.GetSourceColors(glyph.TextUnitIndex).Sample(0.5f, 0.5f);
            Color32 animatedColor = transform.ApplyColor(sourceColor);

            Vector4 programBottomLeft = Program(
                bottomLeft.x,
                bottomLeft.y,
                in snapshot,
                styleSlot,
                operationIndex);
            Vector4 programBottomRight = Program(
                bottomRight.x,
                bottomRight.y,
                in snapshot,
                styleSlot,
                operationIndex);
            Vector4 programTopLeft = Program(
                topLeft.x,
                topLeft.y,
                in snapshot,
                styleSlot,
                operationIndex);
            Vector4 programTopRight = Program(
                topRight.x,
                topRight.y,
                in snapshot,
                styleSlot,
                operationIndex);
            Vector4 effectBottomLeft = DepthLocal(minimumLocal.x, minimumLocal.y);
            Vector4 effectBottomRight = DepthLocal(maximumLocal.x, minimumLocal.y);
            Vector4 effectTopLeft = DepthLocal(minimumLocal.x, maximumLocal.y);
            Vector4 effectTopRight = DepthLocal(maximumLocal.x, maximumLocal.y);
            Vector4 geometry = new Vector4(
                glyph.FieldPixelsPerEm,
                0f,
                source.Bold ? 1f : 0f,
                MappingGlyphIndex(in frame, glyphIndex) + 1f);

            target.EmitQuad(
                transform.ApplyPosition(bottomLeft),
                transform.ApplyPosition(bottomRight),
                transform.ApplyPosition(topLeft),
                transform.ApplyPosition(topRight),
                animatedColor,
                effectBottomLeft,
                effectBottomRight,
                effectTopLeft,
                effectTopRight,
                programBottomLeft,
                programBottomRight,
                programTopLeft,
                programTopRight,
                geometry,
                glyph.AtlasUvRect);
        }

        static Vector4 Program(
            float x,
            float y,
            in TextStudioVisualSnapshot snapshot,
            int styleSlot,
            int operationIndex)
        {
            return new Vector4(
                NormalizeTextX(x, in snapshot),
                NormalizeTextY(y, in snapshot),
                styleSlot,
                operationIndex);
        }

        static Vector4 DepthLocal(float x, float y)
        {
            return new Vector4(x, y, x, y);
        }

        static Vector3 DepthPosition(
            float x,
            float y,
            in DepthSource source)
        {
            Vector2 position = source.BottomLeft +
                               source.AxisX * x +
                               source.AxisY * y;
            return new Vector3(position.x, position.y, 0f);
        }

        void PopulateSelection(
            CompiledTextStudio2DStyle style,
            in TextStudio2DCompiledOperation operation,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            int glyphCount)
        {
            bool hasSelector = operation.SelectorIndex >= 0 &&
                               operation.SelectorIndex < style.SelectorCount;
            TextStudio2DResolvedSelection selection = hasSelector ? _binding.GetSelection(in frame, operation.SelectorIndex) : null;
            for (int glyphIndex = _selectionStart; glyphIndex < _selectionEnd; glyphIndex++)
            {
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                _selected[glyphIndex - _selectionStart] =
                    IsRenderableGlyph(in glyph) &&
                    TextStudio2DSourceApplicabilityUtility.Includes(in operation, glyph.SourceKind) &&
                    hasSelector &&
                    selection.GetOwner(glyphIndex) >= 0;
            }
        }

        void EmitSelectedRuns(
            GeometryBuffer target,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text tmpText,
            in TextStudio2DCompiledOperation operation,
            int operationIndex,
            int styleSlot,
            int glyphCount, TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            if (operation.Decoration.Shape == TextStudio2DDecorationShape.GlyphSilhouette && IsBackground(operation.Decoration.Kind))
            {
                EmitGlyphSurfaces(target, in frame, in snapshot, tmpText, in operation, operationIndex, styleSlot, sourceDemand, frameApplier);
                return;
            }
            if (operation.Decoration.Topology == TextStudio2DDecorationTopology.ConnectedRange &&
                IsBackground(operation.Decoration.Kind))
            {
                EmitConnectedSurfaces(target, in frame, in snapshot, tmpText, in operation, operationIndex, styleSlot);
                return;
            }
            _decorationRuns.Clear();
            _runMembers.Clear();
            TextStudio2DResolvedSelection resolved = _binding.GetSelection(in frame, operation.SelectorIndex);
            for (int fragmentIndex = 0; fragmentIndex < resolved.FragmentCount; fragmentIndex++)
            {
                TextStudio2DVisualFragment fragment = resolved.GetFragment(fragmentIndex);
                int runStart = _runMembers.Count;
                for (int member = fragment.FirstMember; member < fragment.FirstMember + fragment.MemberCount; member++)
                {
                    int glyphIndex = resolved.GetMember(member);
                    if (IsSelected(glyphIndex)) _runMembers.Add(glyphIndex);
                }
                if (_runMembers.Count == runStart) continue;
                TextStudio2DDecorationSpec decoration = operation.Decoration;
                bool line = !IsBackground(decoration.Kind);
                if (line && decoration.Line.FollowFontRuns)
                {
                    int groupStart = runStart;
                    while (groupStart < _runMembers.Count)
                    {
                        ulong font = snapshot.GetGlyph(_runMembers[groupStart]).FontIdentity;
                        float fontSize = frame.GetEffectUnitScale(_runMembers[groupStart]);
                        int groupEnd = groupStart + 1;
                        while (groupEnd < _runMembers.Count && snapshot.GetGlyph(_runMembers[groupEnd]).FontIdentity == font &&
                            frame.GetEffectUnitScale(_runMembers[groupEnd]).Equals(fontSize)) groupEnd++;
                        _decorationRuns.Add(ResolveDecorationRun(in frame, in snapshot, tmpText, in decoration,
                            groupStart, groupEnd, fragment.LineIndex, fragment.MatchIndex, resolved));
                        groupStart = groupEnd;
                    }
                }
                else
                {
                    _decorationRuns.Add(ResolveDecorationRun(in frame, in snapshot, tmpText, in decoration,
                        runStart, _runMembers.Count, fragment.LineIndex, fragment.MatchIndex, resolved));
                }
            }

            for (int runIndex = 0; runIndex < _decorationRuns.Count; runIndex++)
            {
                DecorationRun run = _decorationRuns[runIndex];
                EmitRun(
                    target,
                    in frame,
                    in snapshot,
                    tmpText,
                    in operation,
                    operationIndex,
                    styleSlot,
                    in run, sourceDemand, frameApplier);
            }
        }

        DecorationRun ResolveDecorationRun(
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text tmpText,
            in TextStudio2DDecorationSpec decoration,
            int runStart,
            int runEnd,
            int lineIndex, int matchIndex, TextStudio2DResolvedSelection selection)
        {
            float runLeft = float.PositiveInfinity, runRight = float.NegativeInfinity;
            float runBottom = float.PositiveInfinity, runTop = float.NegativeInfinity, runEmScale = MinimumExtent;
            FaceInfo lineFace = default;
            float lineEm = 0f;
            bool stableLineFace = !IsBackground(decoration.Kind) && !decoration.Line.FollowFontRuns && runStart < runEnd;
            if (stableLineFace)
            {
                int firstIndex = _runMembers[runStart];
                for (int fragment = 0; fragment < selection.FragmentCount; fragment++)
                {
                    TextStudio2DVisualFragment candidate = selection.GetFragment(fragment);
                    if (candidate.MatchIndex != matchIndex || candidate.MemberCount == 0) continue;
                    firstIndex = selection.GetMember(candidate.FirstMember);
                    break;
                }
                TextStudioVisualGlyph firstGlyph = snapshot.GetGlyph(firstIndex);
                lineFace = ResolveFaceInfo(tmpText, in firstGlyph);
                lineEm = frame.GetEffectUnitScale(firstIndex);
            }
            for (int member = runStart; member < runEnd; member++)
            {
                int glyphIndex = _runMembers[member];
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                GetGlyphHorizontalSpan(in glyph, decoration.Kind, out float left, out float right);
                ResolveRunVerticalBounds(in frame, in snapshot, tmpText, in decoration, glyphIndex, glyphIndex + 1,
                    in lineFace, lineEm, stableLineFace, out float bottom, out float top, out float em);
                runLeft = Mathf.Min(runLeft, left); runRight = Mathf.Max(runRight, right);
                runBottom = Mathf.Min(runBottom, bottom); runTop = Mathf.Max(runTop, top);
                runEmScale = Mathf.Max(runEmScale, em);
            }
            Vector4 padding = decoration.Padding;
            float automaticLeft = runLeft - Mathf.Max(0f, padding.x) * runEmScale;
            float automaticRight = runRight + Mathf.Max(0f, padding.z) * runEmScale;
            ResolvePlateHorizontalBounds(in decoration, runEmScale, ref runLeft, ref runRight);
            runLeft -= Mathf.Max(0f, padding.x) * runEmScale;
            runRight += Mathf.Max(0f, padding.z) * runEmScale;
            if (runRight < runLeft)
                Swap(ref runLeft, ref runRight);
            if (runRight - runLeft < MinimumExtent)
                runRight = runLeft + MinimumExtent;

            float runWidth = runRight - runLeft;
            float runHeight = Mathf.Max(MinimumExtent, runTop - runBottom);
            return new DecorationRun(
                runStart,
                runEnd,
                lineIndex,
                runLeft,
                runBottom,
                runWidth,
                runHeight,
                runEmScale, matchIndex, automaticLeft, Mathf.Max(MinimumExtent, automaticRight - automaticLeft));
        }

        static void ResolvePlateHorizontalBounds(in TextStudio2DDecorationSpec decoration, float emScale,
            ref float left, ref float right)
        {
            if (!IsBackground(decoration.Kind) || decoration.PlateWidth <= MinimumExtent)
                return;
            float center = (left + right) * .5f;
            float halfWidth = decoration.PlateWidth * emScale * .5f;
            left = center - halfWidth;
            right = center + halfWidth;
        }

        void EmitRun(
            GeometryBuffer target,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text text,
            in TextStudio2DCompiledOperation operation,
            int operationIndex,
            int styleSlot,
            in DecorationRun run, TextStudioPreparedFieldDemand sourceDemand, TextStudioTMPFrameApplier frameApplier)
        {
            TextStudio2DDecorationSpec decoration = operation.Decoration;
            if (!IsBackground(decoration.Kind) && decoration.Thickness <= 0f &&
                decoration.Line.ThicknessMode == TextStudio2DMetricMode.Custom)
                return;
            ResolveDecorationSurfaceExpansion(
                in decoration,
                run.EmScale,
                out Vector4 expansion);
            if (!IsBackground(decoration.Kind) && decoration.Line.Cap != TextStudio2DLineCap.Butt)
            {
                float cap = run.Height * .5f;
                expansion.x = Mathf.Max(expansion.x, cap);
                expansion.z = Mathf.Max(expansion.z, cap);
            }

            float previousRight = 0f;
            _surfaceMembers.Clear();
            TextStudio2DResolvedSelection selection = _binding.GetSelection(in frame, operation.SelectorIndex);
            for (int member = selection.UnitStart; _binding.HasColorAdjustments && member < selection.UnitEnd; member++)
            {
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(member);
                if (decoration.Topology == TextStudio2DDecorationTopology.PerLine &&
                    selection.GetOwner(member) == run.MatchIndex && glyph.LineIndex == run.LineIndex && IsSelected(member))
                    _surfaceMembers.Add(member);
            }
            if (_binding.HasColorAdjustments && _surfaceMembers.Count == 0)
                for (int member = run.Start; member < run.End; member++) _surfaceMembers.Add(_runMembers[member]);
            Vector4 adjustmentMask = _binding.SurfaceAdjustmentMask(in frame, operationIndex, _surfaceMembers);
            TextStudio2DSurfaceHandle inkMask = IsBackground(decoration.Kind) ? default :
                PrepareSkipInk(in frame, in snapshot, text, in decoration, in run, adjustmentMask, sourceDemand, frameApplier);
            if (!IsBackground(decoration.Kind) && decoration.Line.Units == TextStudio2DUnit.Pixels)
            {
                EmitPixelLine(target, in frame, in snapshot, in run, operationIndex, styleSlot, expansion, adjustmentMask, inkMask);
                return;
            }
            for (int member = run.Start; member < run.End; member++)
            {
                int glyphIndex = _runMembers[member];
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                GetGlyphHorizontalSpan(in glyph, decoration.Kind, out float glyphLeft, out float glyphRight);
                float automaticTileLeft = member == run.Start
                    ? run.AutomaticLeft
                    : (previousRight + glyphLeft) * 0.5f;
                float automaticTileRight;
                if (member == run.End - 1)
                {
                    automaticTileRight = run.AutomaticLeft + run.AutomaticWidth;
                }
                else
                {
                    TextStudioVisualGlyph next = snapshot.GetGlyph(_runMembers[member + 1]);
                    GetGlyphHorizontalSpan(in next, decoration.Kind, out float nextLeft, out _);
                    automaticTileRight = (glyphRight + nextLeft) * 0.5f;
                }
                previousRight = glyphRight;
                float tileLeft = decoration.PlateWidth > MinimumExtent && IsBackground(decoration.Kind)
                    ? run.Left + (automaticTileLeft - run.AutomaticLeft) / run.AutomaticWidth * run.Width
                    : automaticTileLeft;
                float tileRight = decoration.PlateWidth > MinimumExtent && IsBackground(decoration.Kind)
                    ? run.Left + (automaticTileRight - run.AutomaticLeft) / run.AutomaticWidth * run.Width
                    : automaticTileRight;
                if (member == run.Start) tileLeft = run.Left;
                if (member == run.End - 1) tileRight = run.Right;
                if (tileRight < tileLeft)
                    Swap(ref tileLeft, ref tileRight);
                if (tileRight - tileLeft < MinimumExtent)
                    tileRight = tileLeft + MinimumExtent;

                float renderLeft = member == run.Start
                    ? tileLeft - expansion.x
                    : tileLeft;
                float renderRight = member == run.End - 1
                    ? tileRight + expansion.z
                    : tileRight;

                EmitGlyphTile(
                    target,
                    in frame,
                    in snapshot,
                    in glyph,
                    glyphIndex,
                    styleSlot,
                    operationIndex,
                    run.Left,
                    run.Bottom,
                    run.Width,
                    run.Height,
                    run.EmScale,
                    renderLeft,
                    renderRight,
                    run.Bottom - expansion.y,
                    run.Top + expansion.w,
                    0f, adjustmentMask, inkMask);
            }
        }

        static void ResolveDecorationSurfaceExpansion(in TextStudio2DDecorationSpec decoration, float emScale, out Vector4 expansion)
        {
            expansion = decoration.GetTreatmentExpansion(TextStudio2DUnit.Em) * emScale +
                decoration.GetTreatmentExpansion(TextStudio2DUnit.Pixels);
        }

        static void ResolveRunVerticalBounds(
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text tmpText,
            in TextStudio2DDecorationSpec decoration,
            int runStart,
            int runEnd,
            out float bottom,
            out float top,
            out float runEmScale)
        {
            FaceInfo unused = default;
            ResolveRunVerticalBounds(in frame, in snapshot, tmpText, in decoration, runStart, runEnd,
                in unused, 0f, false, out bottom, out top, out runEmScale);
        }

        static void ResolveRunVerticalBounds(
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            TMP_Text tmpText,
            in TextStudio2DDecorationSpec decoration,
            int runStart,
            int runEnd,
            in FaceInfo faceOverride,
            float emOverride,
            bool useFaceOverride,
            out float bottom,
            out float top,
            out float runEmScale)
        {
            bottom = float.PositiveInfinity;
            top = float.NegativeInfinity;
            runEmScale = MinimumExtent;
            Vector4 padding = decoration.Padding;
            bool background = IsBackground(decoration.Kind);
            for (int glyphIndex = runStart; glyphIndex < runEnd; glyphIndex++)
            {
                TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                float emScale = useFaceOverride ? emOverride : frame.GetEffectUnitScale(glyphIndex);
                runEmScale = Mathf.Max(runEmScale, emScale);
                float offset = decoration.Offset * emScale;
                FaceInfo faceInfo = useFaceOverride ? faceOverride : ResolveFaceInfo(tmpText, in glyph);
                float pointSize = Mathf.Abs(faceInfo.pointSize) > MinimumExtent
                    ? Mathf.Abs(faceInfo.pointSize)
                    : 1f;
                float normalizedMetric = ResolveNormalizedDecorationMetric(
                    decoration.Kind,
                    in faceInfo,
                    pointSize);
                float glyphBottom;
                float glyphTop;
                if (background)
                {
                    if (decoration.PlateHeight > MinimumExtent)
                    {
                        float center = ResolvePlateCenter(
                            in glyph,
                            in faceInfo,
                            pointSize,
                            emScale,
                            decoration.VerticalAnchor) + offset;
                        float halfHeight = decoration.PlateHeight * emScale * 0.5f;
                        glyphBottom = center - halfHeight -
                                      Mathf.Max(0f, padding.y) * emScale;
                        glyphTop = center + halfHeight +
                                   Mathf.Max(0f, padding.w) * emScale;
                    }
                    else
                    {
                        ResolveGlyphVerticalSpan(in glyph, emScale, out glyphBottom, out glyphTop);
                        glyphBottom += offset - Mathf.Max(0f, padding.y) * emScale;
                        glyphTop += offset + Mathf.Max(0f, padding.w) * emScale;
                    }
                }
                else
                {
                    float thickness = ResolveLineThickness(in decoration, in faceInfo, pointSize, emScale);
                    if (thickness <= 0f) continue;
                    float resolvedOffset = decoration.Offset * ResolveLineUnit(in decoration, emScale);
                    if (decoration.Line.OffsetMode != TextStudio2DMetricMode.Custom)
                        resolvedOffset += normalizedMetric * emScale;
                    float baseline = tmpText != null && tmpText.textInfo != null && glyph.LineIndex >= 0 && glyph.LineIndex < tmpText.textInfo.lineCount
                        ? tmpText.textInfo.lineInfo[glyph.LineIndex].baseline : glyph.Baseline;
                    float center = baseline + resolvedOffset;
                    if (decoration.Kind == TextStudio2DDecorationKind.Overline)
                        center += thickness * 0.5f;
                    float extent = thickness * .5f;
                    float unit = ResolveLineUnit(in decoration, emScale);
                    if (decoration.Line.Pattern == TextStudio2DLinePattern.Double)
                        extent = thickness + decoration.Line.DoubleSeparation * unit * .5f;
                    else if (decoration.Line.Pattern == TextStudio2DLinePattern.Wavy)
                        extent += decoration.Line.WaveAmplitude * unit;
                    glyphBottom = center - extent;
                    glyphTop = center + extent;
                }
                bottom = Mathf.Min(bottom, glyphBottom);
                top = Mathf.Max(top, glyphTop);
            }

            if (!float.IsFinite(bottom) || !float.IsFinite(top))
            {
                bottom = 0f;
                top = MinimumExtent;
            }
            if (top - bottom < MinimumExtent)
                top = bottom + MinimumExtent;
        }

        static float ResolveLineThickness(in TextStudio2DDecorationSpec decoration, in FaceInfo faceInfo,
            float pointSize, float emScale)
        {
            if (decoration.Line.ThicknessMode == TextStudio2DMetricMode.Custom)
                return Mathf.Max(0f, decoration.Thickness) * ResolveLineUnit(in decoration, emScale);
            float fontThickness = decoration.Kind == TextStudio2DDecorationKind.Strikethrough
                ? faceInfo.strikethroughThickness / pointSize
                : faceInfo.underlineThickness / pointSize;
            if (!float.IsFinite(fontThickness) || fontThickness <= 0f || fontThickness > .25f)
                fontThickness = .05f;
            if (decoration.Line.ThicknessMode == TextStudio2DMetricMode.Auto)
                fontThickness = Mathf.Clamp(fontThickness, .035f, .075f);
            return fontThickness * emScale;
        }

        static float ResolveLineUnit(in TextStudio2DDecorationSpec decoration, float emScale)
        {
            // Pixel-authored pattern details are converted per fragment in the shader from screen derivatives.
            return decoration.Line.Units == TextStudio2DUnit.Pixels ? 1f : emScale;
        }

        void EmitPixelLine(GeometryBuffer target, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot,
            in DecorationRun run, int operationIndex, int styleSlot, Vector4 expansion, Vector4 adjustmentMask, TextStudio2DSurfaceHandle inkMask)
        {
            float left = run.Left - expansion.x, right = run.Right + expansion.z;
            float bottom = run.Bottom - expansion.y, top = run.Top + expansion.w;
            int representative = _runMembers[run.Start];
            Color32 color = frame.GetSourceColors(representative).Sample(.5f, .5f);
            float opacity = 0f;
            for (int member = run.Start; member < run.End; member++)
            {
                int glyph = _runMembers[member];
                float alpha = frame.Animated.IsCreated && glyph < frame.AnimatedCount ? frame.Animated[glyph].alpha : 1f;
                opacity = Mathf.Max(opacity, alpha * frame.GetSourceColors(glyph).Sample(.5f, .5f).a / 255f);
            }
            color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);
            Vector4 bottomLeft = new Vector4((left - run.Left) / run.Width, (bottom - run.Bottom) / run.Height, 0f, 0f);
            Vector4 bottomRight = new Vector4((right - run.Left) / run.Width, bottomLeft.y, 1f, 0f);
            Vector4 topLeft = new Vector4(bottomLeft.x, (top - run.Bottom) / run.Height, 0f, 1f);
            Vector4 topRight = new Vector4(bottomRight.x, topLeft.y, 1f, 1f);
            target.EmitQuad(new Vector3(left, bottom), new Vector3(right, bottom), new Vector3(left, top), new Vector3(right, top), color,
                bottomLeft, bottomRight, topLeft, topRight,
                Program(left, bottom, in snapshot, styleSlot, operationIndex), Program(right, bottom, in snapshot, styleSlot, operationIndex),
                Program(left, top, in snapshot, styleSlot, operationIndex), Program(right, top, in snapshot, styleSlot, operationIndex),
                new Vector4(run.Width, run.Height, run.EmScale, inkMask.IsValid ? -representative - 1 : (representative + 1f) * 2f),
                inkMask.IsValid ? new Vector4(inkMask.Slot, inkMask.Generation, run.Left, run.Bottom) : adjustmentMask);
        }

        static float ResolveNormalizedDecorationMetric(
            TextStudio2DDecorationKind kind,
            in FaceInfo faceInfo,
            float pointSize)
        {
            return kind switch
            {
                TextStudio2DDecorationKind.Underline => faceInfo.underlineOffset / pointSize,
                TextStudio2DDecorationKind.Strikethrough => faceInfo.strikethroughOffset / pointSize,
                TextStudio2DDecorationKind.Overline => faceInfo.ascentLine / pointSize,
                _ => 0f
            };
        }

        static float ResolvePlateCenter(
            in TextStudioVisualGlyph glyph,
            in FaceInfo faceInfo,
            float pointSize,
            float emScale,
            TextStudio2DDecorationVerticalAnchor anchor)
        {
            if (anchor == TextStudio2DDecorationVerticalAnchor.Baseline)
                return glyph.Baseline;
            if (anchor == TextStudio2DDecorationVerticalAnchor.LineBox)
            {
                float lineCenter =
                    (faceInfo.ascentLine + faceInfo.descentLine) *
                    0.5f / pointSize;
                return glyph.Baseline + lineCenter * emScale;
            }

            ResolveGlyphVerticalSpan(in glyph, emScale, out float bottom, out float top);
            return (bottom + top) * 0.5f;
        }

        static FaceInfo ResolveFaceInfo(
            TMP_Text tmpText,
            in TextStudioVisualGlyph glyph)
        {
            TMP_TextInfo textInfo = tmpText != null ? tmpText.textInfo : null;
            if (textInfo != null &&
                textInfo.characterInfo != null &&
                glyph.VisualIndex >= 0 &&
                glyph.VisualIndex < textInfo.characterCount)
            {
                TMP_FontAsset font = textInfo.characterInfo[glyph.VisualIndex].fontAsset;
                if (font != null)
                    return font.faceInfo;
            }
            return tmpText != null && tmpText.font != null
                ? tmpText.font.faceInfo
                : default;
        }

        static void EmitGlyphTile(
            GeometryBuffer target,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            in TextStudioVisualGlyph glyph,
            int glyphIndex,
            int styleSlot,
            int operationIndex,
            float runLeft,
            float runBottom,
            float runWidth,
            float runHeight,
            float runEmScale,
            float tileLeft,
            float tileRight,
            float tileBottom,
            float tileTop,
            float geometryKind, Vector4 adjustmentMask, TextStudio2DSurfaceHandle inkMask = default)
        {
            Vector3 bottomLeft = new Vector3(tileLeft, tileBottom, 0f);
            Vector3 bottomRight = new Vector3(tileRight, tileBottom, 0f);
            Vector3 topLeft = new Vector3(tileLeft, tileTop, 0f);
            Vector3 topRight = new Vector3(tileRight, tileTop, 0f);

            Vector3 sourceCenter = glyph.BoundsMax.y > glyph.BoundsMin.y
                ? new Vector3(
                    (glyph.BoundsMin.x + glyph.BoundsMax.x) * 0.5f,
                    (glyph.BoundsMin.y + glyph.BoundsMax.y) * 0.5f,
                    0f)
                : new Vector3(
                    glyph.Origin.x + glyph.Advance * 0.5f,
                    glyph.Baseline,
                    0f);
            EvaluatedGlyphState state = frame.Animated.IsCreated && glyphIndex < frame.AnimatedCount
                ? frame.Animated[glyphIndex]
                : EvaluatedGlyphState.Identity;
            GlyphPlacement placement = default;
            bool hasPlacement = frame.Placements.IsCreated && glyphIndex < frame.PlacementCount;
            if (hasPlacement)
                placement = frame.Placements[glyphIndex];
            float effectUnitScale = frame.GetEffectUnitScale(glyphIndex);
            TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.Create(
                sourceCenter,
                in state,
                in placement,
                hasPlacement,
                effectUnitScale,
                1f);
            Color32 sourceColor = frame.GetSourceColors(glyphIndex).Sample(0.5f, 0.5f);
            Color32 animatedColor = transform.ApplyColor(sourceColor);

            Vector4 program = new Vector4(
                NormalizeTextX(tileLeft, in snapshot),
                NormalizeTextY(tileBottom, in snapshot),
                styleSlot,
                operationIndex);
            Vector4 programRight = new Vector4(
                NormalizeTextX(tileRight, in snapshot),
                program.y,
                styleSlot,
                operationIndex);
            Vector4 programTop = new Vector4(
                program.x,
                NormalizeTextY(tileTop, in snapshot),
                styleSlot,
                operationIndex);
            Vector4 programTopRight = new Vector4(
                programRight.x,
                programTop.y,
                styleSlot,
                operationIndex);
            Vector4 geometry = new Vector4(
                runWidth,
                runHeight,
                runEmScale,
                (MappingGlyphIndex(in frame, snapshot.GetRenderGlyphIndexForUnit(glyphIndex)) + 1f) * 2f + geometryKind);
            if (inkMask.IsValid)
            {
                geometry.w = -(MappingGlyphIndex(in frame, snapshot.GetRenderGlyphIndexForUnit(glyphIndex)) + 1f);
                adjustmentMask = new Vector4(inkMask.Slot, inkMask.Generation, runLeft, runBottom);
            }

            float shapeLeft = (tileLeft - runLeft) / runWidth;
            float shapeRight = (tileRight - runLeft) / runWidth;
            float shapeBottom = (tileBottom - runBottom) / runHeight;
            float shapeTop = (tileTop - runBottom) / runHeight;
            Vector4 uvBottomLeft = new Vector4(
                shapeLeft,
                shapeBottom,
                NormalizeGlyphX(tileLeft, in glyph),
                NormalizeGlyphY(tileBottom, in glyph));
            Vector4 uvBottomRight = new Vector4(
                shapeRight,
                shapeBottom,
                NormalizeGlyphX(tileRight, in glyph),
                uvBottomLeft.w);
            Vector4 uvTopLeft = new Vector4(
                shapeLeft,
                shapeTop,
                uvBottomLeft.z,
                NormalizeGlyphY(tileTop, in glyph));
            Vector4 uvTopRight = new Vector4(
                shapeRight,
                shapeTop,
                uvBottomRight.z,
                uvTopLeft.w);

            target.EmitQuad(
                transform.ApplyPosition(bottomLeft),
                transform.ApplyPosition(bottomRight),
                transform.ApplyPosition(topLeft),
                transform.ApplyPosition(topRight),
                animatedColor,
                uvBottomLeft,
                uvBottomRight,
                uvTopLeft,
                uvTopRight,
                program,
                programRight,
                programTop,
                programTopRight,
                geometry,
                adjustmentMask);
        }

        static float NormalizeGlyphX(float value, in TextStudioVisualGlyph glyph)
        {
            float minimum = glyph.BoundsMin.x;
            float maximum = glyph.BoundsMax.x;
            if (maximum - minimum < MinimumExtent)
            {
                minimum = Mathf.Min(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
                maximum = Mathf.Max(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
            }
            return (value - minimum) / Mathf.Max(MinimumExtent, maximum - minimum);
        }

        static float NormalizeGlyphY(float value, in TextStudioVisualGlyph glyph)
        {
            float minimum = glyph.BoundsMin.y;
            float maximum = glyph.BoundsMax.y;
            if (maximum - minimum < MinimumExtent)
            {
                minimum = glyph.Baseline - 0.2f;
                maximum = glyph.Baseline + 0.8f;
            }
            return (value - minimum) / Mathf.Max(MinimumExtent, maximum - minimum);
        }

        static float NormalizeTextX(float value, in TextStudioVisualSnapshot snapshot)
        {
            return (value - snapshot.SourceBounds.x) /
                   Mathf.Max(MinimumExtent, snapshot.SourceBounds.z - snapshot.SourceBounds.x);
        }

        static float NormalizeTextY(float value, in TextStudioVisualSnapshot snapshot)
        {
            return (value - snapshot.SourceBounds.y) /
                   Mathf.Max(MinimumExtent, snapshot.SourceBounds.w - snapshot.SourceBounds.y);
        }

        static void ResolveGlyphVerticalSpan(
            in TextStudioVisualGlyph glyph,
            float emScale,
            out float bottom,
            out float top)
        {
            bottom = glyph.BoundsMin.y;
            top = glyph.BoundsMax.y;
            if (top - bottom >= MinimumExtent)
                return;
            bottom = glyph.Baseline - emScale * 0.2f;
            top = glyph.Baseline + emScale * 0.8f;
        }

        static void GetGlyphHorizontalSpan(
            in TextStudioVisualGlyph glyph,
            TextStudio2DDecorationKind kind,
            out float left,
            out float right)
        {
            left = Mathf.Min(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
            right = Mathf.Max(glyph.Origin.x, glyph.Origin.x + glyph.Advance);
            if (IsBackground(kind) && glyph.IsVisible)
            {
                left = Mathf.Min(left, glyph.BoundsMin.x);
                right = Mathf.Max(right, glyph.BoundsMax.x);
            }
            if (right - left < MinimumExtent)
                right = left + MinimumExtent;
        }

        static bool IsRenderableGlyph(in TextStudioVisualGlyph glyph)
        {
            return glyph.IsVisible || Mathf.Abs(glyph.Advance) > MinimumExtent;
        }

        static bool IsBackground(TextStudio2DDecorationKind kind)
        {
            return kind == TextStudio2DDecorationKind.Background ||
                   kind == TextStudio2DDecorationKind.Border;
        }

        void EnsureSelectionCapacity(int glyphCount)
        {
            if (_selected.Length < glyphCount)
                _selected = new bool[TextStudioBufferCapacity.ForCount(glyphCount)];
        }

        static void DestroyObject(Object value)
        {
            if (value == null)
                return;
            TextStudio2DHeroSubmission.Flush();
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }

        static void Swap(ref float first, ref float second)
        {
            float value = first;
            first = second;
            second = value;
        }

        public void Dispose()
        {
            _glyphSurfaceInputs.Clear();
            _glyphSurfaceContours.Clear();
            BeginStrokes();
            ReleaseUnusedStrokes();
            _activeSurfaces = 0;
            ReleaseUnusedSurfaces();
            _ownedBinding.Bind(null);
            DisposeBatches(_batches);
        }

        static void DisposeBatches(List<StyleBatch> batches)
        {
            for (int index = 0; index < batches.Count; index++)
                batches[index].Dispose();
            batches.Clear();
        }

        readonly struct DepthSource
        {
            internal readonly Material Material;
            internal readonly Texture Atlas;
            internal readonly float GradientScale;
            internal readonly bool Bold;
            internal readonly Vector2 BottomLeft;
            internal readonly Vector2 AxisX;
            internal readonly Vector2 AxisY;

            internal DepthSource(
                Material material,
                Texture atlas,
                float gradientScale,
                bool bold,
                Vector2 bottomLeft,
                Vector2 bottomRight,
                Vector2 topLeft)
            {
                Material = material;
                Atlas = atlas;
                GradientScale = gradientScale;
                Bold = bold;
                BottomLeft = bottomLeft;
                AxisX = bottomRight - bottomLeft;
                AxisY = topLeft - bottomLeft;
            }
        }

        readonly struct DecorationRun
        {
            internal readonly int MatchIndex;
            internal readonly int Start;
            internal readonly int End;
            internal readonly int LineIndex;
            internal readonly float Left;
            internal readonly float Bottom;
            internal readonly float Width;
            internal readonly float Height;
            internal readonly float EmScale;
            internal readonly float AutomaticLeft;
            internal readonly float AutomaticWidth;

            internal float Right => Left + Width;
            internal float Top => Bottom + Height;
            internal float CenterX => Left + Width * .5f;
            internal float CenterY => Bottom + Height * .5f;

            internal DecorationRun(
                int start,
                int end,
                int lineIndex,
                float left,
                float bottom,
                float width,
                float height,
                float emScale, int matchIndex, float automaticLeft, float automaticWidth)
            {
                MatchIndex = matchIndex;
                Start = start;
                End = end;
                LineIndex = lineIndex;
                Left = left;
                Bottom = bottom;
                Width = width;
                Height = height;
                EmScale = emScale;
                AutomaticLeft = automaticLeft;
                AutomaticWidth = automaticWidth;
            }
        }

        sealed class StyleBatch : IDisposable
        {
            TextStudio2DStyleDecorationMaterialLease _materialLease;
            internal Mesh HeroMesh { get; private set; }
            internal ulong HeroOwners { get; private set; }

            internal void UploadHero()
            {
                if (HeroMesh == null)
                {
                    HeroMesh = new Mesh { name = "Text Studio Hero Surfaces", hideFlags = HideFlags.HideAndDontSave };
                    HeroMesh.MarkDynamic();
                }
                HeroMesh.Clear();
                HeroMesh.indexFormat = Geometry.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                HeroMesh.SetVertices(Geometry.Vertices);
                HeroMesh.SetColors(Geometry.Colors);
                HeroMesh.SetUVs(0, Geometry.Uv0);
                HeroMesh.SetUVs(1, Geometry.Uv1);
                HeroMesh.SetUVs(2, Geometry.Uv2);
                HeroMesh.SetUVs(3, Geometry.Uv3);
                HeroMesh.SetTriangles(Geometry.Triangles, 0, true);
                HeroOwners = 0;
                for (int index = 0; index < Geometry.Uv1.Count; index += 4)
                    HeroOwners |= 1UL << Mathf.RoundToInt(Geometry.Uv1[index].w);
            }


            internal readonly GeometryBuffer Geometry = new GeometryBuffer();
            internal Material Source { get; private set; }
            internal Texture Atlas { get; private set; }
            internal bool DecorationOnly { get; private set; }
            internal Material Material => _materialLease.IsValid
                ? _materialLease.Replacement
                : null;

            internal bool Matches(Material source, Texture atlas, bool hero = false)
            {
                return _materialLease.IsValid && _materialLease.Hero == hero &&
                       ReferenceEquals(Source, source) &&
                       ReferenceEquals(Atlas, atlas);
            }

            internal bool Configure(
                Material source,
                Texture atlas,
                bool decorationOnly,
                bool clearGeometry, bool hero = false)
            {
                if (Matches(source, atlas, hero))
                {
                    TextStudio2DStyleDecorationMaterialCache.Synchronize(
                        in _materialLease);
                    DecorationOnly &= decorationOnly;
                    if (clearGeometry)
                        Geometry.Clear();
                    return true;
                }

                TextStudio2DStyleDecorationMaterialLease next =
                    TextStudio2DStyleDecorationMaterialCache.Acquire(
                        source,
                        atlas, hero);
                if (!next.IsValid)
                    return false;
                TextStudio2DStyleDecorationMaterialCache.Release(
                    ref _materialLease);
                _materialLease = next;
                Source = source;
                Atlas = atlas;
                DecorationOnly = decorationOnly;
                if (clearGeometry)
                    Geometry.Clear();
                return true;
            }

            internal void Deactivate()
            {
                DestroyObject(HeroMesh);
                HeroMesh = null;
                HeroOwners = 0;
                Geometry.Clear();
                TextStudio2DStyleDecorationMaterialCache.Release(
                    ref _materialLease);
                Source = null;
                Atlas = null;
                DecorationOnly = false;
            }

            public void Dispose()
            {
                Deactivate();
            }
        }

        internal sealed class GeometryBuffer
        {
            internal long ManagedPayloadBytes => TextStudioAppearanceManagedMemory.ListBytes(Vertices) +
                TextStudioAppearanceManagedMemory.ListBytes(Colors) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv0) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv1) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv2) +
                TextStudioAppearanceManagedMemory.ListBytes(Uv3) +
                TextStudioAppearanceManagedMemory.ListBytes(Triangles);

            internal readonly List<Vector3> Vertices = new List<Vector3>(64);
            internal readonly List<Color32> Colors = new List<Color32>(64);
            internal readonly List<Vector4> Uv0 = new List<Vector4>(64);
            internal readonly List<Vector4> Uv1 = new List<Vector4>(64);
            internal readonly List<Vector4> Uv2 = new List<Vector4>(64);
            internal readonly List<Vector4> Uv3 = new List<Vector4>(64);
            internal readonly List<int> Triangles = new List<int>(96);

            internal void Clear()
            {
                Vertices.Clear();
                Colors.Clear();
                Uv0.Clear();
                Uv1.Clear();
                Uv2.Clear();
                Uv3.Clear();
                Triangles.Clear();
            }

            internal void EmitQuad(
                Vector3 bottomLeft,
                Vector3 bottomRight,
                Vector3 topLeft,
                Vector3 topRight,
                Color32 color,
                Vector4 uv0BottomLeft,
                Vector4 uv0BottomRight,
                Vector4 uv0TopLeft,
                Vector4 uv0TopRight,
                Vector4 uv1BottomLeft,
                Vector4 uv1BottomRight,
                Vector4 uv1TopLeft,
                Vector4 uv1TopRight,
                Vector4 uv2,
                Vector4 uv3)
            {
                int start = Vertices.Count;
                Vertices.Add(bottomLeft);
                Vertices.Add(bottomRight);
                Vertices.Add(topLeft);
                Vertices.Add(topRight);
                Colors.Add(color);
                Colors.Add(color);
                Colors.Add(color);
                Colors.Add(color);
                Uv0.Add(uv0BottomLeft);
                Uv0.Add(uv0BottomRight);
                Uv0.Add(uv0TopLeft);
                Uv0.Add(uv0TopRight);
                Uv1.Add(uv1BottomLeft);
                Uv1.Add(uv1BottomRight);
                Uv1.Add(uv1TopLeft);
                Uv1.Add(uv1TopRight);
                Uv2.Add(uv2);
                Uv2.Add(uv2);
                Uv2.Add(uv2);
                Uv2.Add(uv2);
                Uv3.Add(uv3);
                Uv3.Add(uv3);
                Uv3.Add(uv3);
                Uv3.Add(uv3);
                Triangles.Add(start);
                Triangles.Add(start + 2);
                Triangles.Add(start + 1);
                Triangles.Add(start + 1);
                Triangles.Add(start + 2);
                Triangles.Add(start + 3);
            }
        }

    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class TextStudio2DStyleDecorationGraphic : MaskableGraphic
    {
        TextStudio2DStyleDecorations.GeometryBuffer _geometry;
        Material _runtimeMaterial;

        internal int DebugVertexCount => _geometry?.Vertices.Count ?? 0;
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        public override Texture mainTexture
        {
            get
            {
                Texture texture = _runtimeMaterial != null && _runtimeMaterial.HasProperty(MainTextureId)
                    ? _runtimeMaterial.GetTexture(MainTextureId) : null;
                return texture != null ? texture : Texture2D.whiteTexture;
            }
        }

        public override Material material
        {
            get => _runtimeMaterial != null ? _runtimeMaterial : base.material;
            set => _runtimeMaterial = value;
        }

        internal void ApplyMaterial(Material source)
        {
            if (_runtimeMaterial == source)
                return;
            _runtimeMaterial = source;
            SetMaterialDirty();
        }

        internal void SetGeometry(TextStudio2DStyleDecorations.GeometryBuffer geometry)
        {
            _geometry = geometry;
            SetVerticesDirty();
        }

        internal void ClearGeometry()
        {
            if (_geometry == null)
                return;
            _geometry = null;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_geometry == null)
                return;
            int vertexCount = _geometry.Vertices.Count;
            for (int index = 0; index < vertexCount; index++)
            {
                helper.AddVert(new UIVertex
                {
                    position = _geometry.Vertices[index],
                    color = _geometry.Colors[index],
                    uv0 = _geometry.Uv0[index],
                    uv1 = _geometry.Uv1[index],
                    uv2 = _geometry.Uv2[index],
                    uv3 = _geometry.Uv3[index],
                    normal = new Vector3(0f, 0f, -1f),
                    tangent = new Vector4(1f, 0f, 0f, -1f)
                });
            }
            for (int index = 0;
                 index + 2 < _geometry.Triangles.Count;
                 index += 3)
            {
                helper.AddTriangle(
                    _geometry.Triangles[index],
                    _geometry.Triangles[index + 1],
                    _geometry.Triangles[index + 2]);
            }
        }
    }
}
