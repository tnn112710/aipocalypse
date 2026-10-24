using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TextStudio
{
    // Static row cache. An animated Hero text recomposes every frame, but its static rows draw the same pixels each time. The longest
    // run of consecutive root rows that neither animate nor read their backdrop, and that draw straight into it with premultiplied
    // source-over, composes once into a retained surface; later frames composite that surface over the rows below the run with the
    // same blend instead of drawing the run again. Source-over is associative, so the result matches the drawn run exactly where
    // the rows below contribute nothing or the run covers them opaquely, and to RGBA8 rounding where translucent run pixels cover
    // animated ones, because the cached run is quantized once rather than after each row. A run at the bottom of the stack is exact,
    // and a half-float target keeps that rounding far below one RGBA8 step. An RGBA8 target splits a run above composed rows: a
    // stencil marks where the composition below is not empty, the run's rows draw there over it with their own rounding, and the
    // cache lands everywhere else, where it equals the drawn rows exactly.
    // A run is reused only on frames that render for continuous animation alone: Render reports whether the program, visual
    // version, glyph count, dirty state, structural refresh, colour sources and camera demand stayed unchanged, and the cache key
    // adds the layer geometry revision, target layout and density, source material revisions, motion policy and row program
    // switches. The first such frame composes the run into the surface; every other frame draws the run and releases it. The
    // surface is a reclaimable composition-sized lease of the shared Hero pool, inside its 192 MiB budget: layout counts it as
    // free, any acquisition or memory reclamation that runs short takes it back first, and a text that stops animating releases it.
    internal sealed partial class TextStudio2DHeroCompositor
    {
        /// <summary>A run of one row would trade one row draw for one full-target composite.</summary>
        const int MinimumCachedRows = 2;

        static readonly int s_cachedRowsProperty = Shader.PropertyToID("_TextStudioHeroCachedRows");
        static readonly int s_cachedRowsStencilProperty = Shader.PropertyToID("_TextStudioHeroCachedRowsStencil");
        static readonly int s_backdropProperty = Shader.PropertyToID("_TextStudioHeroBackdrop");
        static bool s_staticRowCacheEnabled = true;
        static Material s_cachedRowsPassMaterial;
        static int s_cachedRowsPass = -1;
        static int s_backdropMaskPass = -1;

        // Longest cacheable run of the root sibling chain of _staticRunProgram under _staticRunPolicy: the bottom node, composed
        // first, the top node and the glyph or surface rows the run draws. No run while _staticRunBottom is negative.
        TextStudio2DHeroProgram _staticRunProgram;
        TextStudioMotionPolicy _staticRunPolicy;
        int _staticRunBottom = -1;
        int _staticRunTop = -1;
        int _staticRunRows;

        TextStudio2DHeroTargetLease _staticRowLease;
        StaticRowKey _staticRowKey;
        bool _staticRowsValid;
        // The surface of the render being recorded while it composes through the cache; null otherwise.
        RenderTexture _staticRowTexture;
        bool _staticRowsReused;
        bool _staticRowsComposed;
        // The render being recorded splits the run (an RGBA8 run above composed rows), and the run's rows draw only where the
        // composition below them is not empty while _staticRunSplit is set.
        bool _staticRowsSplit;
        bool _staticRunSplit;
        int _lastSplitRowDrawCount;
        // Set by Render when the frame changed nothing but presentation time; RenderCachedGeometry consumes it, so camera-time
        // renders draw every row.
        bool _staticInputsUnchanged;
        int _geometryRevision;
        int _lastCachedRowCount;
        int _staticRowBuildCount;

        /// <summary>Tests and lab A/B runs turn the static row cache off to compare it with drawing every row each frame.</summary>
        internal static bool StaticRowCacheEnabled
        {
            get => s_staticRowCacheEnabled;
            set => s_staticRowCacheEnabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetStaticRowCache()
        {
            s_staticRowCacheEnabled = true;
            s_cachedRowsPassMaterial = null;
            s_cachedRowsPass = s_backdropMaskPass = -1;
        }

        /// <summary>Everything a cached run's pixels depend on beyond the unchanged frame Render reports.</summary>
        readonly struct StaticRowKey : IEquatable<StaticRowKey>
        {
            readonly TextStudioStackFingerprint _fingerprint;
            readonly int _geometryRevision, _width, _height, _styleSlot, _materialRevision, _switches;
            readonly GraphicsFormat _format;
            readonly Rect _renderBounds;
            readonly float _pixelsPerUnit, _surfacePixelsPerUnit;
            readonly Vector4 _sourceBounds;
            readonly TextStudioMotionPolicy _policy;

            internal StaticRowKey(TextStudioStackFingerprint fingerprint, int geometryRevision, RenderTexture target, Rect renderBounds,
                float pixelsPerUnit, float surfacePixelsPerUnit, Vector4 sourceBounds, int styleSlot, int materialRevision, int switches,
                TextStudioMotionPolicy policy)
            {
                _fingerprint = fingerprint;
                _geometryRevision = geometryRevision;
                _width = target.width;
                _height = target.height;
                _format = target.graphicsFormat;
                _renderBounds = renderBounds;
                _pixelsPerUnit = pixelsPerUnit;
                _surfacePixelsPerUnit = surfacePixelsPerUnit;
                _sourceBounds = sourceBounds;
                _styleSlot = styleSlot;
                _materialRevision = materialRevision;
                _switches = switches;
                _policy = policy;
            }

            public bool Equals(StaticRowKey other) =>
                _fingerprint.Equals(other._fingerprint) && _geometryRevision == other._geometryRevision && _width == other._width &&
                _height == other._height && _format == other._format && _renderBounds.Equals(other._renderBounds) &&
                _pixelsPerUnit.Equals(other._pixelsPerUnit) && _surfacePixelsPerUnit.Equals(other._surfacePixelsPerUnit) &&
                _sourceBounds.Equals(other._sourceBounds) && _styleSlot == other._styleSlot && _materialRevision == other._materialRevision &&
                _switches == other._switches && _policy == other._policy;

            public override bool Equals(object obj) => obj is StaticRowKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_fingerprint, _geometryRevision, _width, _height, _materialRevision, _renderBounds);
        }

        void ResolveStaticRun(TextStudio2DHeroProgram program, TextStudioMotionPolicy policy)
        {
            if (ReferenceEquals(_staticRunProgram, program) && _staticRunPolicy == policy) return;
            _staticRunProgram = program;
            _staticRunPolicy = policy;
            _staticRunRows = FindStaticRun(program, policy, out _staticRunBottom, out _staticRunTop);
        }

        /// <summary>
        /// Finds the longest run of consecutive root nodes of <paramref name="program"/> that can be cached and returns the rows it
        /// draws, 0 when there is none. Ties keep the lower run, which no animated row lies under. A program that does not animate
        /// under the resolved policy needs no run.
        /// </summary>
        internal static int FindStaticRun(TextStudio2DHeroProgram program, TextStudioMotionPolicy policy, out int bottom, out int top)
        {
            bottom = top = -1;
            CompiledTextStudio2DStyle style = program.Style;
            if (program.GetPainterOrder(-1) == TextStudio2DPainterOrder.GlyphMajor || !style.RequiresContinuousRenderingForPolicy(policy)) return 0;
            int bestRows = 0, runBottom = -1, rows = 0;
            for (int index = program.LastRootOperationIndex, guard = 0; index >= 0 && guard <= program.OperationCount; guard++)
            {
                if (!CachesAsStaticRow(style, index, policy)) runBottom = -1;
                else
                {
                    if (runBottom < 0)
                    {
                        runBottom = index;
                        rows = 0;
                    }
                    TextStudio2DLayerKind kind = style.OperationAt(index).Kind;
                    if (kind != TextStudio2DLayerKind.Mask && kind != TextStudio2DLayerKind.ColorAdjustment) rows++;
                    if (rows > bestRows)
                    {
                        bottom = runBottom;
                        top = index;
                        bestRows = rows;
                    }
                }
                index = program.GetNode(index).PreviousSiblingOperationIndex;
            }
            if (bestRows >= MinimumCachedRows) return bestRows;
            bottom = top = -1;
            return 0;
        }

        /// <summary>
        /// Whether a root node can join a cached run: it composes as a row drawn straight into its backdrop with source-over and
        /// does not change with time, or it is a mask or colour adjustment row, which draws nothing itself. This is the direct
        /// branch of ComposeNodeCore; isolated rows composite with the bounded premultiplied blend instead, which is not
        /// associative where transparent backdrop pixels carry light.
        /// </summary>
        static bool CachesAsStaticRow(CompiledTextStudio2DStyle style, int index, TextStudioMotionPolicy policy)
        {
            ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
            switch (operation.Kind)
            {
                case TextStudio2DLayerKind.Mask:
                case TextStudio2DLayerKind.ColorAdjustment:
                    return true;
                case TextStudio2DLayerKind.Group:
                case TextStudio2DLayerKind.FauxDepth:
                case TextStudio2DLayerKind.CompositeAdjustment:
                    return false;
            }
            // Surface geometry (decorations, surface-owned rows, contour strokes) draws with its own shader and stencil state.
            return DrawsGlyphRow(in operation) && operation.BlendMode == TextStudio2DBlendMode.Normal && operation.MaskOperationIndex < 0 &&
                   !TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(in operation, out _, out _, out _) &&
                   !(operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                     (TextStudio2DAnimatedEffectUtility.RequiresPostProcess(operation.AnimatedEffect.Kind) ||
                      TextStudio2DAnimatedEffectUtility.RequiresHistory(operation.AnimatedEffect.Kind) ||
                      CompiledTextStudio2DStyle.AnimatesForPolicy(in operation, policy)));
        }

        /// <summary>
        /// Decides whether the render being recorded composes the run through the cache, and whether it reuses the cached image or
        /// composes the run into it. <paramref name="unchanged"/> reports a frame that changed nothing but presentation time. Runs
        /// before the submission begins: acquiring a surface can evict another and flush.
        /// </summary>
        void PrepareStaticRows(TextStudio2DHeroProgram program, Material composite, bool unchanged)
        {
            _staticRowTexture = null;
            _staticRowsReused = _staticRowsComposed = _staticRowsSplit = false;
            _lastCachedRowCount = 0;
            TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(_hub.MotionPolicy);
            if (!StaticRowCacheEnabled || _appearancePlan != null || _viewExternal != null || _captureProjection != Matrix4x4.identity ||
                _heroMaterialEffects || _finalTexture == null || composite == null || HasContinuouslyAnimatedMaterial())
            {
                ReleaseStaticRows();
                return;
            }
            ResolveStaticRun(program, policy);
            // A frame that changed static input draws the run; the cache waits for a frame that changes nothing but time.
            if (_staticRunBottom < 0 || !unchanged)
            {
                ReleaseStaticRows();
                return;
            }
            if (!ReferenceEquals(s_cachedRowsPassMaterial, composite))
            {
                s_cachedRowsPassMaterial = composite;
                s_cachedRowsPass = composite.FindPass("CACHED_ROWS");
                s_backdropMaskPass = composite.FindPass("BACKDROP_MASK");
            }
            // Above composed rows of a quantized target the run splits, which needs the stencil surface.
            bool split = _staticRunBottom != program.LastRootOperationIndex && !GraphicsFormatUtility.IsIEEE754Format(_finalTexture.graphicsFormat);
            if (s_cachedRowsPass < 0 || split && (s_backdropMaskPass < 0 || !EnsureRowMaskSurface(composite)))
            {
                ReleaseStaticRows();
                return;
            }
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            RenderTexture texture = pool.GetTexture(_staticRowLease);
            if (texture == null || texture.width != _finalTexture.width || texture.height != _finalTexture.height ||
                texture.graphicsFormat != _finalTexture.graphicsFormat || !texture.IsCreated())
            {
                ReleaseStaticRows();
                if (!pool.TryAcquireReclaimable(_finalTexture.width, _finalTexture.height, _viewQuality, out _staticRowLease, out _)) return;
                texture = pool.GetTexture(_staticRowLease);
                if (texture == null || texture.graphicsFormat != _finalTexture.graphicsFormat)
                {
                    ReleaseStaticRows();
                    return;
                }
            }
            else pool.Touch(_staticRowLease);
            StaticRowKey key = new StaticRowKey(program.Fingerprint, _geometryRevision, _finalTexture, _renderBounds, _pixelsPerUnit,
                _surfacePixelsPerUnit, _sourceBounds, _styleSlot, ResolveLayerMaterialRevision(), ResolveRowProgramSwitches(), policy);
            _staticRowsReused = _staticRowsValid && key.Equals(_staticRowKey);
            _staticRowKey = key;
            // Valid again only once this render completes.
            _staticRowsValid = false;
            _staticRowTexture = texture;
            _staticRowsSplit = split;
        }

        int ResolveLayerMaterialRevision()
        {
            int revision = _occurrenceState.Batches.Count;
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
                revision = revision * 31 + TextStudio2DHeroMaterials.LayerRevision(_occurrenceState.Batches[index].Lease);
            return revision;
        }

        /// <summary>Process-wide switches that select the row programs or the transport the cached rows were drawn with.</summary>
        static int ResolveRowProgramSwitches() =>
            (TextStudio2DHeroMaterials.UniformRowsEnabled ? 1 : 0) | (TextStudio2DHeroMaterials.PlainRowsEnabled ? 2 : 0) |
            (RowMasksEnabled ? 4 : 0) | (TextStudio2DTransport.UsesTextures ? 8 : 0);

        /// <summary>
        /// Composes the cached run at its bottom node of the root range: composes the run into the retained surface unless this
        /// render reuses it, then composites the surface over <paramref name="current"/> with source-over.
        /// </summary>
        RenderTexture ComposeStaticRun(TextStudio2DHeroProgram program, RenderTexture current, RenderTexture alternate, int nestedDepth,
            Material composite, ref bool pendingClear)
        {
            if (_staticRowsReused) _lastCachedRowCount = _staticRunRows;
            else
            {
                // Every row of the run draws straight into its backdrop, so the run composes into the retained surface itself.
                RenderTexture composed = ComposeRange(program, _staticRunBottom, new CompositionPair(_staticRowTexture, alternate), nestedDepth,
                    composite, program.GetNode(_staticRunTop).PreviousSiblingOperationIndex);
                if (_compositionFailed || composed == null) return current;
                if (composed != _staticRowTexture)
                {
                    _commands.Blit(composed, _staticRowTexture);
                    _lastCompositePassCount++;
                }
                _staticRowsComposed = true;
            }
            if (pendingClear || !_staticRowsSplit)
            {
                if (pendingClear) ClearTarget(current);
                else BindTarget(current);
                pendingClear = false;
                CompositeCachedRows(composite);
                return current;
            }

            // Mark where the composition below the run is not empty, reading it while the free alternate surface is bound.
            _commands.SetRenderTarget(alternate, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare,
                _rowMaskTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            _commands.SetViewport(new Rect(0f, 0f, alternate.width, alternate.height));
            _commands.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0u);
            _commands.SetGlobalTexture(s_backdropProperty, current);
            _commands.DrawProcedural(Matrix4x4.identity, composite, s_backdropMaskPass, MeshTopology.Triangles, 3);
            _lastCompositePassCount++;
            // The cache lands where nothing lies below it; the rows draw over the rest exactly as without the cache.
            BindSplitTarget(current);
            _commands.SetGlobalInt(s_cachedRowsStencilProperty, (int)CompareFunction.NotEqual);
            CompositeCachedRows(composite);
            _commands.SetGlobalInt(s_cachedRowsStencilProperty, (int)CompareFunction.Disabled);
            _staticRunSplit = true;
            try
            {
                ComposeRange(program, _staticRunBottom, new CompositionPair(current, alternate), nestedDepth, composite,
                    program.GetNode(_staticRunTop).PreviousSiblingOperationIndex, clear: false);
            }
            finally { _staticRunSplit = false; }
            return current;
        }

        void CompositeCachedRows(Material composite)
        {
            _commands.SetGlobalTexture(s_cachedRowsProperty, _staticRowTexture);
            _commands.DrawProcedural(Matrix4x4.identity, composite, s_cachedRowsPass, MeshTopology.Triangles, 3);
            _lastCompositePassCount++;
        }

        /// <summary>Binds a target beside the split's stencil, which later binds of the same render keep.</summary>
        void BindSplitTarget(RenderTexture target)
        {
            _commands.SetRenderTarget(target, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                _rowMaskTexture, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            _commands.SetViewport(new Rect(0f, 0f, target.width, target.height));
        }

        /// <summary>Keeps a cache this render composed or reused; releases it once the text no longer animates.</summary>
        void CommitStaticRows()
        {
            if (_staticRowTexture != null && (_staticRowsReused || _staticRowsComposed))
            {
                _staticRowsValid = true;
                if (_staticRowsComposed) _staticRowBuildCount++;
            }
            EndStaticRows();
            if (!_requiresContinuousRendering) ReleaseStaticRows();
        }

        void EndStaticRows()
        {
            _staticRowTexture = null;
            _staticRowsReused = _staticRowsComposed = _staticRowsSplit = _staticRunSplit = false;
        }

        void ReleaseStaticRows()
        {
            TextStudio2DHeroTargetPool.Shared.Release(ref _staticRowLease);
            _staticRowsValid = false;
            EndStaticRows();
        }
    }
}
