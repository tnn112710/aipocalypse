using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    // Row masks. Every glyph quad grows to the union of all rows' support, so a fill under an echo trail would shade the trail's
    // whole reach. A masked row still draws those full-size quads, so every pixel it shades interpolates and differentiates exactly
    // as before, but a stencil pre-pass first marks the row's own support (ResolveGlyphExtent's term for that row plus the
    // one-field-pixel guard) and the row's fragments outside it are rejected before shading. Rows with equal support share one row
    // mask class: a submesh of every layer batch whose quads are trimmed to that support, every glyph in source order. Class 0 is
    // the shared extent and draws unmasked. A row contributes nothing outside its support, so the masked composition keeps the
    // unmasked one's pixels (TextStudio2DHeroRendererIntegrationTests.RowMasksRenderTheUnmaskedPixels).
    internal sealed partial class TextStudio2DHeroCompositor
    {
        /// <summary>Class 0 plus at most seven masked classes; further distinct supports draw at the shared extent.</summary>
        internal const int MaximumRowMaskClasses = 8;

        static readonly int s_rowMaskProperty = Shader.PropertyToID("_TextStudio2DHeroRowMask");
        static readonly int s_rowMaskOffsetProperty = Shader.PropertyToID("_TextStudioHeroRowMaskOffset");
        static readonly int s_vertexOffsetXProperty = Shader.PropertyToID("_VertexOffsetX");
        static readonly int s_vertexOffsetYProperty = Shader.PropertyToID("_VertexOffsetY");

        static bool s_rowMasksEnabled = true;
        static bool s_passElisionEnabled = true;
        static bool s_rowMasksCoverDisplacedRows;
        static Material s_rowMaskPassMaterial;
        static int s_rowMaskPass = -1;

        readonly MaterialPropertyBlock _rowMaskProperties = new MaterialPropertyBlock();
        // The stencil surface of the render being recorded, for row masks or a static row split; null when neither uses it.
        TextStudio2DHeroTargetLease _rowMaskLease;
        RenderTexture _rowMaskTexture;
        Material _rowMaskMaterial;
        bool _rowMasksActive;

        /// <summary>Tests and lab A/B runs turn row masks off to compare the masked rows with the shared extent.</summary>
        internal static bool RowMasksEnabled
        {
            get => s_rowMasksEnabled;
            set => s_rowMasksEnabled = value;
        }

        /// <summary>
        /// Tests only: graphic and animated rows join the mask classes too. Their coverage leaves their declared reach, so the
        /// clipped result proves that the stencil test engages on the current graphics device.
        /// </summary>
        internal static bool RowMasksCoverDisplacedRowsForTests
        {
            get => s_rowMasksCoverDisplacedRows;
            set => s_rowMasksCoverDisplacedRows = value;
        }

        /// <summary>
        /// Tests force pass elision off: backdrop filter composites that reduce to a copy, the end-of-frame redraw of the echo
        /// history layer, the copy of a root result into the final surface and the clear of a range target that a filter
        /// overwrites completely.
        /// </summary>
        internal static bool PassElisionEnabled
        {
            get => s_passElisionEnabled;
            set => s_passElisionEnabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetExactSavings()
        {
            s_rowMasksEnabled = true;
            s_passElisionEnabled = true;
            s_rowMasksCoverDisplacedRows = false;
            s_rowMaskPassMaterial = null;
            s_rowMaskPass = -1;
        }

        /// <summary>Rows drawn from glyph batches; decorations, surface-owned rows and contour strokes draw surface geometry.</summary>
        static bool DrawsGlyphRow(in TextStudio2DCompiledOperation operation) =>
            operation.Kind != TextStudio2DLayerKind.Decoration && operation.SurfaceOwnerOperationIndex < 0 &&
            !TextStudio2DStyleDecorations.UsesContourStroke(in operation);

        /// <summary>
        /// Whether a row's coverage stays inside its BoundsExpansion. Graphic and animated effects offset or displace coverage in
        /// their mapping space: a text-mapped glitch moves slices by a share of the text width, far beyond its em-based reach, so
        /// their rows keep the shared extent. A light sweep, shimmer or liquid fill only scales the face coverage, the fill's own
        /// support, so it is masked like a fill.
        /// </summary>
        static bool StaysInsideItsReach(in TextStudio2DCompiledOperation operation)
        {
            if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                operation.AnimatedEffect.Kind is TextStudio2DAnimatedEffectKind.LightSweep or TextStudio2DAnimatedEffectKind.Shimmer
                    or TextStudio2DAnimatedEffectKind.LiquidFill)
                return true;
            return operation.Kind != TextStudio2DLayerKind.GraphicEffect && operation.Kind != TextStudio2DLayerKind.AnimatedEffect;
        }

        /// <summary>
        /// Rows whose draws are coverage inside their support. A composite adjustment draws its glyph selection, not coverage;
        /// mask, colour adjustment and group rows draw nothing themselves.
        /// </summary>
        static bool DrawsCoverage(in TextStudio2DCompiledOperation operation) =>
            operation.Kind != TextStudio2DLayerKind.CompositeAdjustment && operation.Kind != TextStudio2DLayerKind.Mask &&
            operation.Kind != TextStudio2DLayerKind.ColorAdjustment && operation.Kind != TextStudio2DLayerKind.Group;

        /// <summary>A row's support around its source glyph in its own units; faux depth sweeps in a later pass.</summary>
        static TextStudio2DBoundsExpansion RowBounds(in TextStudio2DCompiledOperation operation)
        {
            if (operation.Kind != TextStudio2DLayerKind.FauxDepth) return operation.BoundsExpansion;
            float outset = operation.FauxDepth.SilhouetteOutset + operation.FauxDepth.Softness;
            return new TextStudio2DBoundsExpansion(outset, outset, outset, outset);
        }

        /// <summary>One row's support in source-quad local units, the term ResolveGlyphExtent takes the maximum of.</summary>
        TextStudio2DBoundsExpansion LocalRowExpansion(in TextStudio2DCompiledOperation operation, in TextStudioVisualGlyph glyph,
            Vector2 fieldPixelLocal, Vector3 bottomLeft, Vector3 topLeft, Vector3 bottomRight)
        {
            TextStudio2DBoundsExpansion bounds = RowBounds(in operation);
            bool pixels = TextStudio2DFieldPadding.UsesPixelUnits(in operation);
            Vector2 units = pixels ? new Vector2(
                1f / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit * (bottomRight - bottomLeft).magnitude),
                1f / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit * (topLeft - bottomLeft).magnitude))
                : fieldPixelLocal * glyph.FieldPixelsPerEm;
            return new TextStudio2DBoundsExpansion(
                bounds.Left * units.x, bounds.Bottom * units.y, bounds.Right * units.x, bounds.Top * units.y);
        }

        /// <summary>Local primitive rectangle (min x, min y, max x, max y) of an expansion plus the antialiasing guard.</summary>
        static Vector4 RowExtent(in TextStudio2DBoundsExpansion expansion, Vector2 fieldPixelLocal) =>
            new Vector4(-expansion.Left - FieldAntialiasGuardPixels * fieldPixelLocal.x,
                -expansion.Bottom - FieldAntialiasGuardPixels * fieldPixelLocal.y,
                1f + expansion.Right + FieldAntialiasGuardPixels * fieldPixelLocal.x,
                1f + expansion.Top + FieldAntialiasGuardPixels * fieldPixelLocal.y);

        /// <summary>
        /// Builds the row mask classes of the geometry being emitted. A text whose effect plan uses a material-tier effect keeps one
        /// class, since squiggle, pixel grid or chromatic split move coverage beyond a row's own support. Engine-published glyphs
        /// emit only the shared extent, and appearance occurrences keep it too.
        /// </summary>
        void BuildRowMaskClasses(CompiledTextStudio2DStyle style)
        {
            OccurrenceState state = _occurrenceState;
            state.RowMaskGlyphCount = 0;
            // Without row masks the geometry carries no trimmed quads; turning them on takes effect at the next geometry build.
            bool masked = RowMasksEnabled && !_heroMaterialEffects && _appearancePlan == null && _viewExternal == null;
            state.RowMaskClassCount = GroupRowMaskClasses(masked ? style : null, s_rowMasksCoverDisplacedRows, state.RowMaskClass,
                state.RowMaskRepresentative);
        }

        /// <summary>
        /// Groups the glyph rows of <paramref name="style"/> by support and returns the class count, 1 when no row is masked. Rows whose
        /// support is the component-wise maximum of every row in one unit system keep class 0, as do rows whose coverage leaves its
        /// reach (<see cref="StaysInsideItsReach"/>).
        /// </summary>
        internal static int GroupRowMaskClasses(CompiledTextStudio2DStyle style, bool coverDisplacedRows, int[] classes, int[] representatives)
        {
            System.Array.Clear(classes, 0, classes.Length);
            int classCount = 1;
            int count = style != null ? Mathf.Min(style.OperationCount, TextStudio2DHeroProgram.MaximumOperations) : 0;
            TextStudio2DBoundsExpansion maximum = default;
            bool anyPixels = false, anyRelative = false;
            for (int index = 0; index < count; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!DrawsGlyphRow(in operation)) continue;
                TextStudio2DBoundsExpansion bounds = RowBounds(in operation);
                maximum = maximum.Max(in bounds);
                if (TextStudio2DFieldPadding.UsesPixelUnits(in operation)) anyPixels = true;
                else anyRelative = true;
            }
            for (int index = 0; index < count; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!DrawsGlyphRow(in operation) || !DrawsCoverage(in operation) || !coverDisplacedRows && !StaysInsideItsReach(in operation)) continue;
                TextStudio2DBoundsExpansion bounds = RowBounds(in operation);
                bool pixels = TextStudio2DFieldPadding.UsesPixelUnits(in operation);
                // In one unit system the largest support is the union every glyph quad already spans.
                if (!(anyPixels && anyRelative) && bounds.Equals(maximum)) continue;
                int rowClass = 0;
                for (int existing = 1; existing < classCount && rowClass == 0; existing++)
                {
                    ref readonly TextStudio2DCompiledOperation representative = ref style.OperationAt(representatives[existing]);
                    if (TextStudio2DFieldPadding.UsesPixelUnits(in representative) == pixels && RowBounds(in representative).Equals(bounds))
                        rowClass = existing;
                }
                if (rowClass == 0 && classCount < MaximumRowMaskClasses)
                {
                    rowClass = classCount++;
                    representatives[rowClass] = index;
                }
                classes[index] = rowClass;
            }
            return classCount;
        }

        /// <summary>
        /// Whether a glyph's trimmed quads cover exactly its rows' supports: the trimmed quad rasterizes the region where the full
        /// quad interpolates the trimmed local range only when the positions are affine over the quad, and the atlas mapping must be
        /// affine for the supports to be distances in local units. Other attributes need not agree, because rows always shade the
        /// full quads and the trimmed quads only write the stencil.
        /// </summary>
        static bool TrimsToRowMasks(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            Vector4 uvBottomLeft, Vector4 uvTopLeft, Vector4 uvTopRight, Vector4 uvBottomRight) =>
            IsParallelogram(bottomLeft, topLeft, topRight, bottomRight) && IsParallelogram(uvBottomLeft, uvTopLeft, uvTopRight, uvBottomRight);

        const float ParallelogramTolerance = .00001f;

        static bool IsParallelogram(Vector4 bottomLeft, Vector4 topLeft, Vector4 topRight, Vector4 bottomRight)
        {
            float size = Mathf.Max((topRight - bottomLeft).magnitude, (topLeft - bottomRight).magnitude);
            return (bottomLeft + topRight - topLeft - bottomRight).magnitude <= size * ParallelogramTolerance;
        }

        /// <summary>
        /// Leases the stencil surface when this render can mask rows: classes exist, the text uses no material-tier effect and the
        /// capture is planar. Without the surface, or without the mask pass, every row draws unmasked.
        /// </summary>
        void PrepareRowMasks(Material composite)
        {
            if (_occurrenceState.RowMaskClassCount <= 1 || !RowMasksEnabled || _heroMaterialEffects || _appearancePlan != null ||
                _captureProjection != Matrix4x4.identity) return;
            _rowMasksActive = EnsureRowMaskSurface(composite);
        }

        /// <summary>Leases the stencil surface of the render being recorded, once; false when it or the composite passes are missing.</summary>
        bool EnsureRowMaskSurface(Material composite)
        {
            if (_rowMaskTexture != null) return true;
            if (_finalTexture == null || composite == null) return false;
            if (!ReferenceEquals(s_rowMaskPassMaterial, composite))
            {
                s_rowMaskPassMaterial = composite;
                s_rowMaskPass = composite.FindPass("ROW_MASK");
            }
            if (s_rowMaskPass < 0) return false;
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            if (!pool.TryAcquireRowMask(_finalTexture.width, _finalTexture.height, out _rowMaskLease, out _)) return false;
            _rowMaskTexture = pool.GetTexture(_rowMaskLease);
            _rowMaskMaterial = composite;
            return true;
        }

        void ReleaseRowMask()
        {
            TextStudio2DHeroTargetPool.Shared.Release(ref _rowMaskLease);
            _rowMaskTexture = null;
            _rowMaskMaterial = null;
            _rowMasksActive = false;
        }

        /// <summary>The row mask class a draw tests, or 0 for an unmasked draw at the shared extent.</summary>
        int RowMaskClass(int operationIndex) =>
            _rowMasksActive && !_staticRunSplit && _glyphFilter < 0 && (uint)operationIndex < (uint)_occurrenceState.RowMaskClass.Length
                ? _occurrenceState.RowMaskClass[operationIndex]
                : 0;

        /// <summary>
        /// Binds <paramref name="target"/> with the stencil surface, clears the stencil (and the colour when clearing) and writes
        /// the quads of <paramref name="rowClass"/> into it. The surface never leaves the render pass, so it is neither loaded nor
        /// stored.
        /// </summary>
        void BindRowMask(RenderTexture target, bool clear, int rowClass)
        {
            _commands.SetRenderTarget(target, clear ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                _rowMaskTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            _commands.SetViewport(new Rect(0f, 0f, target.width, target.height));
            _commands.ClearRenderTarget(clear ? RTClearFlags.Color | RTClearFlags.Stencil : RTClearFlags.Stencil, Color.clear, 1f, 0u);
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                if (batch.Material == null || batch.Mesh == null || rowClass >= batch.SubmeshCount) continue;
                _rowMaskProperties.SetVector(s_rowMaskOffsetProperty, new Vector4(batch.Material.GetFloat(s_vertexOffsetXProperty),
                    batch.Material.GetFloat(s_vertexOffsetYProperty), 0f, 0f));
                _commands.DrawMesh(batch.Mesh, Matrix4x4.identity, _rowMaskMaterial, rowClass, s_rowMaskPass, _rowMaskProperties);
            }
        }

        /// <summary>Per-corner payloads of the glyph being emitted; source corners 0 to 3 are bottom left, top left, top right, bottom right.</summary>
        struct RowCorner
        {
            internal Color32 Color;
            internal Vector2 Squiggle;
            internal Vector4 Payload0;
            internal Vector4 Payload1;
            internal Vector4 Local;
            internal float NormalZ;
        }

        readonly RowCorner[] _rowCorners = new RowCorner[4];
    }
}
