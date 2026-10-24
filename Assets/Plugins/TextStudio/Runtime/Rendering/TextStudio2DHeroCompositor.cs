using System;
using ImpossibleRobert.Common;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_2023_2_OR_NEWER
using TmpUv0 = UnityEngine.Vector4;
#else
using TmpUv0 = UnityEngine.Vector2;
#endif

namespace TextStudio
{
    internal enum TextStudio2DHeroCompositorStatus : byte
    {
        Inactive = 0,
        Ready = 1,
        MissingVisualSnapshot = 2,
        MissingGlyphGeometry = 3,
        MissingMaterial = 4,
        MissingShader = 5,
        TargetCapacityExceeded = 6,
        TargetMemoryBudgetExceeded = 7,
        UnsupportedTarget = 8,
        TargetCreationFailed = 9,
        RelationshipDepthExceeded = 10,
        RenderFailed = 11,
        InvalidOrderingScopes = 12,
        ContributionRunBudgetExceeded = 13
    }

    internal readonly struct TextStudio2DHeroCompositorStatistics
    {
        internal readonly int RenderCount;
        internal readonly int SkippedStaticFrameCount;
        internal readonly int GeometryUploadCount;
        internal readonly int GeometryReuseCount;
        internal readonly int OutputGeometryUploadCount;
        internal readonly int LayerDrawCount;
        internal readonly int CompositePassCount;
        internal readonly int EffectPassCount;
        internal readonly int HistoryCaptureCount;
        internal readonly int HistorySurfaceCount;
        internal readonly int TargetWidth;
        internal readonly int TargetHeight;
        /// <summary>Copies of completed compositions into shared output page slices during the last render.</summary>
        internal readonly int PublishPassCount;
        /// <summary>Layer draws of the last render whose row read its record from material constants (uniform row program).</summary>
        internal readonly int UniformRowDrawCount;
        /// <summary>Layer draws of the last render that shaded only inside their row mask.</summary>
        internal readonly int RowMaskDrawCount;
        /// <summary>Glyphs of the current layer geometry whose row mask submeshes hold quads trimmed to each row's support.</summary>
        internal readonly int RowMaskGlyphCount;
        /// <summary>Layer draws of the last render that drew a material, graphic or animated row with its plain variant.</summary>
        internal readonly int PlainRowDrawCount;
        /// <summary>Rows the last render composited from the static row cache instead of drawing them.</summary>
        internal readonly int CachedRowCount;
        /// <summary>Renders that composed static rows into the cache.</summary>
        internal readonly int StaticRowBuildCount;
        /// <summary>Layer draws of the last render that drew a cached run's rows only where the composition below it was not empty.</summary>
        internal readonly int SplitRowDrawCount;

        internal TextStudio2DHeroCompositorStatistics(
            int renderCount,
            int skippedStaticFrameCount,
            int geometryUploadCount,
            int geometryReuseCount,
            int outputGeometryUploadCount,
            int layerDrawCount,
            int compositePassCount,
            int effectPassCount,
            int historyCaptureCount,
            int historySurfaceCount,
            int targetWidth,
            int targetHeight,
            int publishPassCount,
            int uniformRowDrawCount = 0,
            int rowMaskDrawCount = 0,
            int rowMaskGlyphCount = 0,
            int plainRowDrawCount = 0,
            int cachedRowCount = 0,
            int staticRowBuildCount = 0,
            int splitRowDrawCount = 0)
        {
            RowMaskDrawCount = rowMaskDrawCount;
            RowMaskGlyphCount = rowMaskGlyphCount;
            PlainRowDrawCount = plainRowDrawCount;
            CachedRowCount = cachedRowCount;
            StaticRowBuildCount = staticRowBuildCount;
            SplitRowDrawCount = splitRowDrawCount;
            RenderCount = renderCount;
            SkippedStaticFrameCount = skippedStaticFrameCount;
            GeometryUploadCount = geometryUploadCount;
            GeometryReuseCount = geometryReuseCount;
            OutputGeometryUploadCount = outputGeometryUploadCount;
            LayerDrawCount = layerDrawCount;
            CompositePassCount = compositePassCount;
            EffectPassCount = effectPassCount;
            HistoryCaptureCount = historyCaptureCount;
            HistorySurfaceCount = historySurfaceCount;
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            PublishPassCount = publishPassCount;
            UniformRowDrawCount = uniformRowDrawCount;
        }
    }

    /// <summary>
    /// Bounded offscreen renderer for complete 2D paint trees. The source TMP mesh remains the
    /// layout and animation oracle while this class owns only transient layer geometry, pooled
    /// composition surfaces, and one replacement output primitive.
    /// </summary>
    internal sealed partial class TextStudio2DHeroCompositor : IDisposable
    {
        const string WorldOutputName = "_TSHeroOutput";
        const string UiOutputName = "_TSHeroOutputUI";
        const int MaximumScopeDepth = TextStudio2DHeroProgram.MaximumRelationshipDepth;
        const float FieldAntialiasGuardPixels = 1f;
        const float OutputSamplingGuardPixels = 2f;
        const int MaximumHistoryFrames =
            TextStudio2DAnimatedEffectUtility.MaximumHistoryFrames;
        const float MinimumExtent = 0.0001f;
        const float PostProcessGuardPixels = 4f;
        internal const int ProgressiveBlurDownsampleFactor = 4;
        internal const int TextWideBlurDownsampleFactor = 2;
        internal const float HeroSamplingScale = 2.25f;
        const int TextWideBlurHorizontalPass = 5;
        const int TextWideBlurVerticalPass = 6;
        const int ColorAdjustmentPass = 7;
        const int FauxDepthPass = 8;
        const int TextWideBlurCoverageVerticalPass = 9;
        static readonly int s_textWideCoverageProperty = Shader.PropertyToID("_TextStudioHeroBlurCoverage");

        static readonly int s_operationIndexProperty =
            Shader.PropertyToID("_TextStudio2DHeroOperationIndex");
        static readonly int s_selectorIndexProperty =
            Shader.PropertyToID("_TextStudio2DHeroSelectorIndex");
        static readonly int s_styleSlotProperty =
            Shader.PropertyToID("_TextStudio2DHeroStyleSlot");
        static readonly int s_sourceModeProperty =
            Shader.PropertyToID("_TextStudio2DHeroSourceMode");
        static readonly int s_glyphFilterProperty =
            Shader.PropertyToID("_TextStudio2DHeroGlyphFilter");
        static readonly int s_scopeSelectors0Property =
            Shader.PropertyToID("_TextStudio2DHeroScopeSelectors0");
        static readonly int s_scopeSelectors1Property =
            Shader.PropertyToID("_TextStudio2DHeroScopeSelectors1");
        static readonly int s_sourceBoundsProperty =
            Shader.PropertyToID("_TextStudioSourceBounds");
        static readonly int s_sourceTextureProperty =
            Shader.PropertyToID("_TextStudioHeroSourceTex");
        static readonly int s_maskTextureProperty =
            Shader.PropertyToID("_TextStudioHeroMaskTex");
        static readonly int s_blendModeProperty =
            Shader.PropertyToID("_TextStudioHeroBlendMode");
        static readonly int s_opacityProperty =
            Shader.PropertyToID("_TextStudioHeroOpacity");
        static readonly int s_useMaskProperty =
            Shader.PropertyToID("_TextStudioHeroUseMask");
        static readonly int s_invertMaskProperty =
            Shader.PropertyToID("_TextStudioHeroInvertMask");
        static readonly int s_maskStrengthProperty =
            Shader.PropertyToID("_TextStudioHeroMaskStrength");
        static readonly int s_blurRadiusProperty =
            Shader.PropertyToID("_TextStudioHeroBlurRadius");
        static readonly int s_focusBlurRadiusProperty =
            Shader.PropertyToID("_TextStudioHeroFocusBlurRadius");
        static readonly int s_focusBlurTexelSizeProperty =
            Shader.PropertyToID("_TextStudioHeroFocusBlurTexelSize");
        static readonly int s_lensCenterRadiusProperty =
            Shader.PropertyToID("_TextStudioHeroLensCenterRadius");
        static readonly int s_lensProfileProperty =
            Shader.PropertyToID("_TextStudioHeroLensProfile");
        static readonly int s_timeProperty =
            Shader.PropertyToID("_TextStudio2DHeroTime");
        static readonly int s_motionPolicyProperty =
            Shader.PropertyToID("_TextStudio2DHeroMotionPolicy");
        static readonly int s_effectKindProperty =
            Shader.PropertyToID("_TextStudioHeroEffectKind");
        static readonly int s_effectParameters0Property =
            Shader.PropertyToID("_TextStudioHeroEffectParameters0");
        static readonly int s_effectParameters1Property =
            Shader.PropertyToID("_TextStudioHeroEffectParameters1");
        static readonly int s_effectDirectionProperty =
            Shader.PropertyToID("_TextStudioHeroEffectDirection");
        static readonly int s_effectCoordinateTransformProperty =
            Shader.PropertyToID("_TextStudioHeroEffectCoordinateTransform");
        static readonly int s_effectPrimaryProperty =
            Shader.PropertyToID("_TextStudioHeroEffectPrimary");
        static readonly int s_effectSecondaryProperty =
            Shader.PropertyToID("_TextStudioHeroEffectSecondary");
        static readonly int s_effectReducedBehaviorProperty =
            Shader.PropertyToID("_TextStudioHeroEffectReducedBehavior");
        static readonly int s_effectBackdropProperty =
            Shader.PropertyToID("_TextStudioHeroBackdropTex");
        static readonly int s_effectMaskProperty =
            Shader.PropertyToID("_TextStudioHeroEffectMaskTex");
        static readonly int s_colorAdjustmentRedProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentRed");
        static readonly int s_colorAdjustmentGreenProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentGreen");
        static readonly int s_colorAdjustmentBlueProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentBlue");
        static readonly int s_colorAdjustmentAlphaProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentAlpha");
        static readonly int s_colorAdjustmentOffsetProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentOffset");
        static readonly int s_colorAdjustmentSettingsProperty =
            Shader.PropertyToID("_TextStudioHeroColorAdjustmentSettings");
        static readonly int s_sourceIsBackdropFilterProperty =
            Shader.PropertyToID("_TextStudioHeroSourceIsBackdropFilter");
        static readonly int s_historyCountProperty =
            Shader.PropertyToID("_TextStudioHeroHistoryCount");
        static readonly int[] s_historyTextureProperties =
        {
            Shader.PropertyToID("_TextStudioHeroHistory0"),
            Shader.PropertyToID("_TextStudioHeroHistory1"),
            Shader.PropertyToID("_TextStudioHeroHistory2"),
            Shader.PropertyToID("_TextStudioHeroHistory3")
        };
        static readonly int s_faceUvSpeedXProperty =
            Shader.PropertyToID("_FaceUVSpeedX");
        static readonly int s_faceUvSpeedYProperty =
            Shader.PropertyToID("_FaceUVSpeedY");
        static readonly int s_outlineUvSpeedXProperty =
            Shader.PropertyToID("_OutlineUVSpeedX");
        static readonly int s_outlineUvSpeedYProperty =
            Shader.PropertyToID("_OutlineUVSpeedY");

        static readonly int s_surfacePixelUnitProperty = Shader.PropertyToID("_TextStudio2DSurfacePixelUnit");
        readonly TextStudioText _hub;
        CompiledTextStudio2DStyle _surfaceStyle;
        float _surfacePixelsPerUnit;
        float _projectedPixelsPerUnit;
        static readonly int s_heroPixelScaleProperty = Shader.PropertyToID("_TextStudioHeroPixelScale");
        // Owners record into the frame's shared stream; TextStudio2DHeroSubmission submits it once before rendering.
        static CommandBuffer _commands => TextStudio2DHeroSubmission.Stream;
        internal long ManagedPayloadBytes
        {
            get
            {
                long bytes = CaptureOccurrenceManagedBytes(_defaultOccurrenceState);
                foreach (OccurrenceState state in _appearanceStates) if (state != null) bytes += CaptureOccurrenceManagedBytes(state);
                return bytes + TextStudioAppearanceManagedMemory.ArrayBytes(_outputVertices) + TextStudioAppearanceManagedMemory.ArrayBytes(_outputColors) +
                    TextStudioAppearanceManagedMemory.ArrayBytes(_outputUv) + TextStudioAppearanceManagedMemory.ArrayBytes(_outputTriangles) +
                    TextStudioAppearanceManagedMemory.ArrayBytes(_scopeSelectors) + TextStudioAppearanceManagedMemory.ArrayBytes(_scratchLeases) +
                    TextStudioAppearanceManagedMemory.ArrayBytes(_appearanceAggregateLeases) +
                    (long)(_scratchTextures.Length + _appearanceAggregateTextures.Length + _appearanceStates.Capacity + _activeAppearanceStates.Capacity) * IntPtr.Size;
            }
        }

        int _glyphFilter = -1;
        readonly int[] _scopeSelectors = new int[MaximumScopeDepth];
        /// <summary>Selector evaluation pass of the frame being rendered; begun by each render entry point.</summary>
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        readonly TextStudio2DHeroTargetLease[] _scratchLeases =
            new TextStudio2DHeroTargetLease[TextStudio2DHeroTargetPool.MaximumTargets - 1];
        readonly RenderTexture[] _scratchTextures =
            new RenderTexture[TextStudio2DHeroTargetPool.MaximumTargets - 1];
        static readonly int s_uiVertexColorGammaProperty = Shader.PropertyToID("_UIVertexColorAlwaysGammaSpace");
        readonly MaterialPropertyBlock _layerProperties = new MaterialPropertyBlock();
        readonly Vector3[] _outputVertices = new Vector3[4];
        readonly Color32[] _outputColors =
        {
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255),
            new Color32(255, 255, 255, 255)
        };
        readonly Vector4[] _outputUv = new Vector4[4];
        readonly int[] _outputTriangles = { 0, 1, 2, 2, 3, 0 };

        // The composition target lives for one render; the published slice of a shared page keeps the output between renders.
        TextStudio2DHeroTargetLease _finalLease;
        TextStudio2DHeroOutputLease _output;
        TextStudio2DHeroOutputLease _shadowOutput;
        int _lastPublishPassCount;
        RenderTexture _finalTexture;
        RenderTexture _layerTexture;
        RenderTexture _rootScratchTexture;
        RenderTexture _effectScratchTexture0;
        RenderTexture _blurScratchTexture0;
        RenderTexture _blurScratchTexture1;
        RenderTexture _blurCoverageTexture;
        float _blurScratchScaleX = 1f;
        float _blurScratchScaleY = 1f;
        Material _effectMaterial;
        int _nestedPairCount;
        int _scratchCount;
        int _scopeDepth;
        int _styleSlot;
        float _pixelsPerUnit;
        float _effectUnitScale;
        Rect _renderBounds;
        Vector4 _sourceBounds;
        bool _compositionFailed;
        // Whether the text's effect plan uses material-tier effects: only then do Hero row materials compile them and rows keep the
        // union extent. Read from the plan itself, because the material adapter's MaterialEffectsRequired keeps its conservative
        // default whenever the adapter restores the source material, as for every Hero text without an external or writing material.
        bool _heroMaterialEffects = true;
        bool _hasRenderedOutput;
        bool _requiresContinuousRendering;
        TextStudio2DWorldShadowMode _worldShadowMode;
        TextStudioStackFingerprint _lastFingerprint;
        TextStudioVisualVersion _lastVisualVersion;
        int _lastGlyphCount = -1;
        TextStudioPreparedFieldDemand _sourceDemand;
        float _currentRenderTime;

        GameObject _worldOutput;
        Mesh _worldOutputMesh;
        MeshRenderer _worldOutputRenderer;
        GameObject _uiOutput;
        RectTransform _uiOutputTransform;
        TextStudio2DHeroOutputGraphic _uiOutputGraphic;
        Rect _worldOutputBounds;
        Material _worldOutputMaterial;
        Vector4 _worldOutputSlices;
        // A world batch group draws the output quad in its shards; the own renderer stays hidden meanwhile.
        bool _worldSuppressed;
        bool _hasWorldOutputBounds;

        int _renderCount;
        int _skippedStaticFrameCount;
        int _geometryUploadCount;
        int _geometryReuseCount;
        int _outputGeometryUploadCount;
        int _lastLayerDrawCount;
        int _lastUniformRowDrawCount;
        int _lastRowMaskDrawCount;
        int _lastPlainRowDrawCount;
        // Uniform row records of _uniformRowStyle, packed once per compiled style; ineligible rows keep the table program.
        CompiledTextStudio2DStyle _uniformRowStyle;
        TextStudio2DHeroUniformRow[] _uniformRows = Array.Empty<TextStudio2DHeroUniformRow>();
        bool[] _uniformRowEligible = Array.Empty<bool>();
        int _lastCompositePassCount;
        int _lastEffectPassCount;
        int _historyCaptureCount;
        bool _disposed;

        internal TextStudio2DHeroCompositorStatus Status { get; private set; }
        internal string CompatibilityWarning { get; private set; } = string.Empty;
        internal string GeometryCompatibilityWarning
        {
            get
            {
                if (_appearancePlan != null)
                    foreach (OccurrenceState state in _activeAppearanceStates)
                        if (!string.IsNullOrEmpty(state.SurfaceGeometry.CompatibilityWarning)) return state.SurfaceGeometry.CompatibilityWarning;
                return _appearancePlan == null ? _occurrenceState.SurfaceGeometry.CompatibilityWarning : string.Empty;
            }
        }
        internal bool HasOutput => _hasRenderedOutput && _output.IsValid;
        internal TextStudio2DHeroCompositorStatistics Statistics =>
            new TextStudio2DHeroCompositorStatistics(
                _renderCount,
                _skippedStaticFrameCount,
                _geometryUploadCount,
                _geometryReuseCount,
                _outputGeometryUploadCount,
                _lastLayerDrawCount,
                _lastCompositePassCount,
                _lastEffectPassCount,
                _historyCaptureCount,
                TotalHistorySurfaceCount,
                _output.IsValid ? _output.Width : 0,
                _output.IsValid ? _output.Height : 0,
                _lastPublishPassCount,
                _lastUniformRowDrawCount,
                _lastRowMaskDrawCount,
                _occurrenceState.RowMaskGlyphCount,
                _lastPlainRowDrawCount,
                _lastCachedRowCount,
                _staticRowBuildCount,
                _lastSplitRowDrawCount);

        internal TextStudio2DHeroCompositor(TextStudioText hub)
        {
            _hub = hub;
            _worldSuppressed = hub.IsWorldBatched;
            _occurrenceState = _defaultOccurrenceState;
            for (int index = 0; index < _scopeSelectors.Length; index++)
                _scopeSelectors[index] = -1;
        }

        internal bool Render(
            in TextStudioFrame frame,
            TMP_Text tmpText,
            TextStudio2DHeroProgram program,
            int styleSlot,
            in TextStudioLensMagnifierDescriptor lensMagnifier,
            TextStudioTMPMaterialEffectAdapter materialAdapter,
            TextStudioTMPFrameApplier frameApplier,
            bool forceRefresh,
            float projectedPixelsPerUnit = 0,
            TextStudioPreparedFieldDemand sourceDemand = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextStudio2DHeroCompositor));
            _heroMaterialEffects = TextStudioTMPMaterialEffectAdapter.RequiresProductionMaterial(frame.Plan.CompiledPlan);
            _selectorPass.Begin();
            if (tmpText != null) SetViewSource(null);
            if (_appearancePlan != null) ReleaseAppearanceOccurrences();
            _occurrenceState = _defaultOccurrenceState;
            if (tmpText == null && _viewExternal == null || program == null || styleSlot <= 0)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.Inactive);
                return false;
            }
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            if (!snapshot.IsCreated)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.MissingVisualSnapshot);
                return false;
            }

            bool outputScaleChanged = !Mathf.Approximately(_projectedPixelsPerUnit, projectedPixelsPerUnit);
            _projectedPixelsPerUnit = projectedPixelsPerUnit;
            _sourceDemand = sourceDemand;
            bool colorSourcesChanged = UpdateColorSources(in frame, program.Style);
            forceRefresh |= colorSourcesChanged;
            bool needsRender = forceRefresh || outputScaleChanged || NeedsRender(in frame, program);
            if (!needsRender && HasOutput)
            {
                _skippedStaticFrameCount++;
                SynchronizeOutput(tmpText);
                Status = TextStudio2DHeroCompositorStatus.Ready;
                CompatibilityWarning = string.Empty;
                return true;
            }

            _styleSlot = styleSlot;
            _surfaceStyle = program.Style;
            _worldShadowMode = program.Style.WorldShadowMode;
            _scopeDepth = 0;
            _compositionFailed = false;
            _lastLayerDrawCount = 0;
            _lastUniformRowDrawCount = 0;
            _lastRowMaskDrawCount = 0;
            _lastPlainRowDrawCount = 0;
            _lastCachedRowCount = 0;
            _lastSplitRowDrawCount = 0;
            _lastCompositePassCount = 0;
            _lastEffectPassCount = 0;
            _lastPublishPassCount = 0;
            _surfacePixelsPerUnit = ResolveSourcePixelsPerUnit(in frame, styleSlot);
            if (_projectedPixelsPerUnit > 0) _surfacePixelsPerUnit = _projectedPixelsPerUnit;
            Rect geometryBounds;
            TextStudio2DQualityTier qualityTier;
            if (CanReuseGeometry(
                    in frame,
                    program,
                    forceRefresh || outputScaleChanged && program.Style.RequiresCameraPixels ||
                    program.Style.RequiresContourGeometry && _occurrenceState.GeometryPrecisionFingerprint != (sourceDemand?.GeometryFingerprint ?? 0),
                    out geometryBounds,
                    out qualityTier))
            {
                _geometryReuseCount++;
            }
            else if (!BuildGeometry(
                         in frame,
                         tmpText,
                         program,
                         materialAdapter,
                         frameApplier,
                         out geometryBounds,
                         out qualityTier))
            {
                return false;
            }
            else
            {
                CaptureGeometryCache(
                    in frame,
                    program,
                    in geometryBounds);
            }

            _effectUnitScale = ResolveAverageEffectUnitScale(in frame);
            _sourceBounds = snapshot.SourceBounds;
            _viewText = tmpText;
            _viewProgram = program;
            _viewLens = lensMagnifier;
            _viewQuality = qualityTier;
            _viewDensity = Mathf.Max(_surfacePixelsPerUnit, ResolveSourcePixelsPerUnit(in frame, styleSlot));
            _currentRenderTime = frame.PresentationTime;
            SynchronizeProjectionCallbacks();
            _staticInputsUnchanged = !forceRefresh && !outputScaleChanged && HasOutput && !StaticInputsChanged(in frame, program);
            bool rendered = RenderCachedGeometry(ShouldResetHistory(in frame, program));
            if (rendered)
            {
                _lastVisualVersion = snapshot.Version;
                _lastGlyphCount = snapshot.GlyphCount;
            }
            return rendered;
        }

        /// <summary>Composes one program over quads a text engine published with its own glyph atlas.</summary>
        internal bool RenderExternal(in TextStudioFrame frame, TextStudio2DExternalGlyphSource source, TextStudio2DHeroProgram program,
            int styleSlot, bool forceRefresh, TextStudioPreparedFieldDemand sourceDemand = null)
        {
            SetViewSource(source);
            return source != null && Render(in frame, null, program, styleSlot, default, null, null, forceRefresh, 0, sourceDemand);
        }

        /// <summary>Composes every occurrence of an appearance plan over engine-published quads.</summary>
        internal bool RenderAppearanceExternal(in TextStudioFrame frame, TextStudio2DExternalGlyphSource source,
            IReadOnlyList<TextStudioTMPAppearanceAdapter> programAdapters, TextStudio2DAppearanceBinding binding, bool forceRefresh)
        {
            SetViewSource(source);
            return source != null && RenderAppearance(in frame, null, programAdapters, binding, default, null, null, forceRefresh);
        }

        void SetViewSource(TextStudio2DExternalGlyphSource source)
        {
            if (ReferenceEquals(_viewExternal, source)) return;
            // Engine sources bind their field bank per layer draw; a TMP text must not inherit those overrides.
            _layerProperties.Clear();
            _viewExternal = source;
        }

        bool RenderCachedGeometry(bool resetHistory)
        {
            TextStudio2DHeroProgram program = _viewProgram;
            TextStudioLensMagnifierDescriptor lensMagnifier = _viewLens;
            bool hasLensMagnifier = lensMagnifier.IsActive;
            // Only the render Render prepared may reuse cached static rows; camera-time renders draw every row.
            bool staticInputsUnchanged = _staticInputsUnchanged;
            _staticInputsUnchanged = false;
            Rect geometryBounds = ResolveProjectedGeometryBounds();
            _scopeDepth = 0;
            _compositionFailed = false;
            _lastLayerDrawCount = _lastUniformRowDrawCount = _lastRowMaskDrawCount = _lastPlainRowDrawCount = _lastCachedRowCount = _lastSplitRowDrawCount = _lastCompositePassCount = _lastEffectPassCount = _lastPublishPassCount = 0;
            Rect targetBounds = ExpandForHeroPostProcessing(
                in geometryBounds,
                program);
            if (hasLensMagnifier)
            {
                targetBounds = ExpandForLensMagnifier(
                    in targetBounds,
                    in lensMagnifier);
            }
            if (!ResolveTargetLayout(
                _viewDensity,
                in targetBounds,
                _viewQuality,
                program.RequiresPostProcess || hasLensMagnifier,
                hasLensMagnifier
                    ? lensMagnifier.MaximumMagnification
                    : 1f,
                program,
                hasLensMagnifier,
                out int targetWidth,
                out int targetHeight,
                out _pixelsPerUnit,
                out _renderBounds))
            {
                SetFailure(TextStudio2DHeroCompositorStatus.TargetCapacityExceeded);
                return false;
            }
            int requiredTargets = program.EstimatedConcurrentSurfaceCount + (hasLensMagnifier && program.SpatialScratchCount == 0 ? 1 : 0);
            if (requiredTargets > TextStudio2DHeroTargetPool.MaximumTargets)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.TargetCapacityExceeded);
                CompatibilityWarning = $"This composition requires {requiredTargets} simultaneous render targets; the shared Hero pool supports {TextStudio2DHeroTargetPool.MaximumTargets}. Reduce nested groups, masks, or history frames.";
                return false;
            }
            if (!TryPrepareTargets(
                    targetWidth,
                    targetHeight,
                    _viewQuality,
                    program.CompositionPairCount,
                    Mathf.Max(
                        program.SpatialScratchCount,
                        hasLensMagnifier ? 1 : 0),
                    program.DownsampledBlurScratchCount,
                    program.DownsampledBlurFactor,
                    program.CoveragePairCount,
                    program.NeedsGeneratedAlpha) ||
                !TryPrepareHistoryTargets(
                    targetWidth,
                    targetHeight,
                    _viewQuality,
                    program.MaximumHistoryFrames, program.NeedsHistoryCoverage))
            {
                return false;
            }
            if (resetHistory)
                ResetHistoryState();

            Material composite = TextStudio2DHeroMaterials.GetComposite();
            Material effects = program.RequiresPostProcess || hasLensMagnifier
                ? TextStudio2DHeroMaterials.GetEffects()
                : null;
            Material output = TextStudio2DHeroMaterials.GetOutput();
            if (composite == null || output == null ||
                (program.RequiresPostProcess || hasLensMagnifier) && effects == null)
            {
                ReleaseScratchTargets();
                SetFailure(TextStudio2DHeroCompositorStatus.MissingShader);
                return false;
            }

            try
            {
                _effectMaterial = effects;
                PrepareUniformRows();
                PrepareRowMasks(composite);
                PrepareStaticRows(program, composite, staticInputsUnchanged);
                TextStudio2DHeroSubmission.Begin();
                BuildCompositionCommands(
                    program,
                    composite,
                    _currentRenderTime,
                    TextStudioMotionSettings.Resolve(_hub.MotionPolicy));
                if (hasLensMagnifier)
                    ApplyLensMagnifier(in lensMagnifier);
                if (_compositionFailed)
                {
                    SetFailure(
                        TextStudio2DHeroCompositorStatus.RelationshipDepthExceeded);
                    return false;
                }
                PublishOutput(_finalTexture, ref _output);
                TextStudio2DHeroSubmission.Recorded();
                if (_occurrenceState.HistoryCapturePending)
                {
                    _occurrenceState.HistoryCursor = (_occurrenceState.HistoryCursor + 1) % _occurrenceState.HistorySurfaceCount;
                    _occurrenceState.HistoryValidCount = Mathf.Min(_occurrenceState.HistoryValidCount + 1, _occurrenceState.HistorySurfaceCount);
                    _historyCaptureCount++;
                    _occurrenceState.HistoryCapturePending = false;
                }
                _renderCount++;
                _hasRenderedOutput = true;
                _lastFingerprint = program.Fingerprint;
                _occurrenceState.HistoryFingerprint = program.Fingerprint;
                _occurrenceState.LastRenderTime = _currentRenderTime;
                _occurrenceState.HasHistoryTime = true;
                _requiresContinuousRendering =
                    program.Style.RequiresContinuousRenderingForPolicy(
                        _hub.MotionPolicy) ||
                    HasContinuouslyAnimatedMaterial();
                CommitStaticRows();
                SynchronizeOutput(_viewText);
                Status = TextStudio2DHeroCompositorStatus.Ready;
                CompatibilityWarning = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ResetHistoryState();
                Debug.LogWarning(
                    "[TextStudio] Hero composition failed and preserved the source TMP output. " +
                    exception.Message,
                    _hub);
                SetFailure(TextStudio2DHeroCompositorStatus.RenderFailed);
                return false;
            }
            finally
            {
                _effectMaterial = null;
                // A render that did not complete leaves the cached rows invalid (PrepareStaticRows).
                EndStaticRows();
                ReleaseScratchTargets();
            }
        }

        bool NeedsRender(
            in TextStudioFrame frame,
            TextStudio2DHeroProgram program)
        {
            return !HasOutput ||
                   !TextStudio2DHeroOutputPages.EnsureResident(_output) ||
                   _requiresContinuousRendering ||
                   frame.GlobalDirty != DirtyFlags.None ||
                   StaticInputsChanged(in frame, program);
        }

        /// <summary>
        /// Whether anything static rows depend on changed since the last completed render. An animating text raises the generic
        /// Animation and Visual requests every tick, so they count only through the glyph dirty bits, visual version and geometry
        /// they cause.
        /// </summary>
        bool StaticInputsChanged(
            in TextStudioFrame frame,
            TextStudio2DHeroProgram program)
        {
            if (!_lastFingerprint.Equals(program.Fingerprint) ||
                !_lastVisualVersion.Equals(frame.VisualSnapshot.Version) ||
                _lastGlyphCount != frame.VisualSnapshot.GlyphCount ||
                (frame.GlobalDirty & ~(DirtyFlags.Animation | DirtyFlags.Visual)) != DirtyFlags.None ||
                !frame.Dirty.IsCreated)
            {
                return true;
            }
            return frame.Dirty.AnyDirtyInRange(0, frame.AnimatedCount);
        }

        bool BuildGeometry(
            in TextStudioFrame frame,
            TMP_Text tmpText,
            TextStudio2DHeroProgram program,
            TextStudioTMPMaterialEffectAdapter materialAdapter,
            TextStudioTMPFrameApplier frameApplier,
            out Rect geometryBounds,
            out TextStudio2DQualityTier qualityTier)
        {
            _occurrenceState.HasCachedGeometry = false;
            _geometryRevision++;
            Array.Clear(_occurrenceState.OperationHasSelection, 0, _occurrenceState.OperationHasSelection.Length);
            geometryBounds = default;
            qualityTier = ResolveQualityTier(program.Style);
            BuildRowMaskClasses(frame.AppearanceStyle);
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
                _occurrenceState.Batches[index].BeginBuild(_occurrenceState.RowMaskClassCount);
            _occurrenceState.ActiveBatchCount = 0;

            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            BuildOccurrenceDrawingOrder(in frame);
            if (!BuildGeneratedContributionRuns(in frame, program.Style))
            {
                SetFailure(TextStudio2DHeroCompositorStatus.ContributionRunBudgetExceeded);
                CompatibilityWarning = "Generated depth adjustments exceed the 256 contiguous source-run budget. Use fewer alternating adjustment selections or split the text.";
                return false;
            }
            if (!_occurrenceState.PainterScopes.Build(in frame, program, _occurrenceState.DrawingUnits))
            {
                SetFailure(TextStudio2DHeroCompositorStatus.InvalidOrderingScopes);
                CompatibilityWarning = _occurrenceState.PainterScopes.Diagnostic;
                return false;
            }
            Vector2 boundsMinimum = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);
            Vector2 boundsMaximum = new Vector2(
                float.NegativeInfinity,
                float.NegativeInfinity);
            int visibleGlyphCount = 0;
            int usableGlyphCount = 0;
            IReadOnlyList<int> occurrenceGlyphs = OccurrenceRenderGlyphs(in frame);
            int sourceCount = occurrenceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            TMP_TextInfo textInfo = tmpText != null ? tmpText.textInfo : null;
            if (_viewExternal != null)
                BuildExternalGeometry(in frame, program, occurrenceGlyphs, sourceCount, ref visibleGlyphCount, ref usableGlyphCount,
                    ref boundsMinimum, ref boundsMaximum);
            else if (textInfo == null ||
                textInfo.characterInfo == null ||
                textInfo.meshInfo == null)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.MissingGlyphGeometry);
                return false;
            }
            for (int source = 0; _viewExternal == null && source < sourceCount; source++)
            {
                int glyphIndex = occurrenceGlyphs == null ? source : occurrenceGlyphs[source];
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                if (!glyph.IsVisible || frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null &&
                    !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, glyph.TextUnitIndex))
                    continue;
                visibleGlyphCount++;
                if (glyph.VisualIndex < 0 ||
                    glyph.VisualIndex >= textInfo.characterCount ||
                    glyph.VisualIndex >= textInfo.characterInfo.Length)
                {
                    continue;
                }

                ref readonly TMP_CharacterInfo character = ref textInfo.characterInfo[glyph.VisualIndex];
                int materialIndex = character.materialReferenceIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                    continue;
                ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[materialIndex];
                int vertexIndex = character.vertexIndex;
                if (!HasQuad(meshInfo.vertices, vertexIndex) ||
                    !HasQuad(meshInfo.uvs0, vertexIndex) ||
                    !HasQuad(meshInfo.uvs2, vertexIndex) ||
                    !HasQuad(meshInfo.colors32, vertexIndex))
                {
                    continue;
                }

                Material sourceMaterial = materialAdapter.ResolveSourceMaterial(
                    tmpText,
                    materialIndex);
                if (glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph)
                {
                    if (EmitColorGlyph(in frame, in snapshot, in glyph, glyphIndex, in meshInfo, materialIndex, vertexIndex,
                        sourceMaterial, frameApplier, ref boundsMinimum, ref boundsMaximum)) usableGlyphCount++;
                    continue;
                }
                Texture atlas = TextStudio2DPrimitiveSupport.ResolveAtlas(in character, in glyph, sourceMaterial);
                if (sourceMaterial == null || atlas == null)
                    continue;
                LayerBatch batch = GetBatch(sourceMaterial, atlas, glyph.FieldPixelsPerEm);
                if (batch == null)
                    continue;

                Vector4 selectors = BuildSelectorChunks(
                    program.Style,
                    in frame,
                    glyph.TextUnitIndex);
                EmitGlyph(
                    batch,
                    in frame,
                    in snapshot,
                    in glyph,
                    glyphIndex,
                    in meshInfo,
                    materialIndex,
                    vertexIndex,
                    atlas,
                    sourceMaterial,
                    frameApplier,
                    selectors,
                    ref boundsMinimum,
                    ref boundsMaximum);
                usableGlyphCount++;
            }

            // Engine-owned output declines retained surface rows before it reaches Hero composition.
            if (tmpText != null)
                _occurrenceState.SurfaceGeometry.Rebuild(in frame, tmpText, _styleSlot, sourceDemand: _sourceDemand, frameApplier: frameApplier);
            _occurrenceState.SurfaceGeometry.IncludeHeroBounds(ref boundsMinimum, ref boundsMaximum, _occurrenceState.OperationHasSelection);
            BuildCompositeScopes(in frame, program.Style);
            RemoveUnusedBatches();
            if (usableGlyphCount == 0 && (visibleGlyphCount > 0 ||
                !float.IsFinite(boundsMinimum.x) || !float.IsFinite(boundsMaximum.x)))
            {
                SetFailure(visibleGlyphCount > 0
                    ? TextStudio2DHeroCompositorStatus.MissingMaterial
                    : TextStudio2DHeroCompositorStatus.MissingGlyphGeometry);
                return false;
            }
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                if (!batch.Upload())
                {
                    SetFailure(TextStudio2DHeroCompositorStatus.MissingGlyphGeometry);
                    return false;
                }
                _geometryUploadCount++;
            }

            geometryBounds = Rect.MinMaxRect(
                boundsMinimum.x,
                boundsMinimum.y,
                boundsMaximum.x,
                boundsMaximum.y);
            return geometryBounds.width > MinimumExtent &&
                   geometryBounds.height > MinimumExtent;
        }

        bool CanReuseGeometry(
            in TextStudioFrame frame,
            TextStudio2DHeroProgram program,
            bool forceRefresh,
            out Rect geometryBounds,
            out TextStudio2DQualityTier qualityTier)
        {
            geometryBounds = _occurrenceState.CachedGeometryBounds;
            qualityTier = ResolveQualityTier(program.Style);
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            GetOccurrenceUnitRange(in frame, out int unitStart, out int unitEnd);
            if (forceRefresh ||
                !_occurrenceState.HasCachedGeometry ||
                _occurrenceState.GeometryFingerprint != program.Style.GeometryFingerprint ||
                _occurrenceState.GeometryStyleSlot != _styleSlot ||
                !_occurrenceState.GeometryVisualVersion.Equals(snapshot.Version) ||
                _occurrenceState.GeometryGlyphCount != snapshot.RenderGlyphCount ||
                !ReferenceEquals(_occurrenceState.GeometryExternalSource, _viewExternal) ||
                (_viewExternal != null && _occurrenceState.GeometryExternalRevision != _viewExternal.GeometryRevision) ||
                !frame.Dirty.IsCreated ||
                frame.Dirty.AnyDirtyInRange(unitStart, unitEnd - unitStart))
            {
                return false;
            }

            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                if (!batch.Active || !batch.IsUsable)
                    return false;
                TextStudio2DHeroLayerMaterialLease lease = batch.Lease;
                TextStudio2DHeroMaterials.SynchronizeLayer(in lease);
            }
            return true;
        }

        void CaptureGeometryCache(
            in TextStudioFrame frame,
            TextStudio2DHeroProgram program,
            in Rect geometryBounds)
        {
            _occurrenceState.GeometryFingerprint = program.Style.GeometryFingerprint;
            _occurrenceState.GeometryStyleSlot = _styleSlot;
            _occurrenceState.GeometryVisualVersion = frame.VisualSnapshot.Version;
            _occurrenceState.GeometryGlyphCount = frame.VisualSnapshot.RenderGlyphCount;
            _occurrenceState.GeometryExternalSource = _viewExternal;
            _occurrenceState.GeometryExternalRevision = _viewExternal?.GeometryRevision ?? 0;
            _occurrenceState.GeometryPrecisionFingerprint = _sourceDemand?.GeometryFingerprint ?? 0;
            _occurrenceState.CachedGeometryBounds = geometryBounds;
            _occurrenceState.HasCachedGeometry = true;
        }

        void BuildExternalGeometry(in TextStudioFrame frame, TextStudio2DHeroProgram program, IReadOnlyList<int> occurrenceGlyphs,
            int sourceCount, ref int visibleGlyphCount, ref int usableGlyphCount, ref Vector2 boundsMinimum, ref Vector2 boundsMaximum)
        {
            TextStudio2DExternalGlyphSource external = _viewExternal;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            for (int source = 0; source < sourceCount; source++)
            {
                int glyphIndex = occurrenceGlyphs == null ? source : occurrenceGlyphs[source];
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(glyphIndex);
                if (!glyph.IsVisible || frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null &&
                    !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, glyph.TextUnitIndex))
                    continue;
                visibleGlyphCount++;
                if ((uint)glyphIndex >= (uint)external.Count) continue;
                ref readonly TextStudio2DExternalGlyphSource.Quad quad = ref external.GetQuad(glyphIndex);
                ref readonly TextStudio2DExternalGlyphSource.Field field = ref external.GetField(glyphIndex);
                if (!quad.IsPresent || !field.IsValid) continue;
                Material material = external.ResolveMaterial(field.Bank);
                // The layer material keeps UniText's bank out of its own properties; each draw binds it (DrawLayer).
                LayerBatch batch = material != null ? GetBatch(material, Texture2D.whiteTexture, glyph.FieldPixelsPerEm) : null;
                if (batch == null) continue;
                Vector4 selectors = BuildSelectorChunks(program.Style, in frame, glyph.TextUnitIndex);
                EmitExternalGlyph(batch, in frame, in snapshot, in glyph, glyphIndex, in quad, selectors, ref boundsMinimum, ref boundsMaximum);
                usableGlyphCount++;
            }
        }

        /// <summary>
        /// Emits one engine-published glyph. Native coordinates are em (the mapping row names the engine atlas entry); the
        /// support expansion, source coordinates and payload layout match <see cref="EmitGlyph"/>.
        /// </summary>
        void EmitExternalGlyph(LayerBatch batch, in TextStudioFrame frame, in TextStudioVisualSnapshot snapshot, in TextStudioVisualGlyph glyph,
            int glyphIndex, in TextStudio2DExternalGlyphSource.Quad quad, Vector4 selectors, ref Vector2 boundsMinimum, ref Vector2 boundsMaximum)
        {
            const float pixelsPerEm = TextStudio2DExternalGlyphSource.FieldPixelsPerEm;
            Vector4 em = quad.FieldEm;
            Vector2 fieldPixelLocal = new Vector2(1f / Mathf.Max(MinimumExtent, Mathf.Abs(em.z - em.x) * pixelsPerEm),
                1f / Mathf.Max(MinimumExtent, Mathf.Abs(em.w - em.y) * pixelsPerEm));
            Vector4 extent = ResolveGlyphExtent(frame.AppearanceStyle, in glyph, fieldPixelLocal, quad.Position0, quad.Position1, quad.Position3);
            float objectPerEm = Vector3.Distance(quad.Position0, quad.Position1) / Mathf.Max(MinimumExtent, Mathf.Abs(em.w - em.y));
            Transform host = _viewExternal.Host;
            float scale = objectPerEm / pixelsPerEm * Mathf.Abs(host != null ? host.lossyScale.y : 1f);
            Vector4 visualSourceBounds = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetDomainBounds(frame.AppearanceOccurrenceIndex) : snapshot.SourceBounds;
            int mappingGlyphIndex = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetMappingGlyphIndex(frame.AppearanceOccurrenceIndex, glyphIndex) : glyphIndex;
            float packedGlyph = PackGlyphAndPixelPayload(0f, mappingGlyphIndex);
            // Local functions cannot capture 'in' parameters.
            TextStudio2DExternalGlyphSource.Quad corners = quad;
            Vector2 glyphMinimum = glyph.BoundsMin, glyphMaximum = glyph.BoundsMax;
            Vector2 emittedMinimum = boundsMinimum, emittedMaximum = boundsMaximum;
            EmitCorner(extent.x, extent.y, corners.Color0);
            EmitCorner(extent.x, extent.w, corners.Color1);
            EmitCorner(extent.z, extent.w, corners.Color2);
            EmitCorner(extent.z, extent.y, corners.Color3);
            batch.EmitTriangles();
            boundsMinimum = emittedMinimum;
            boundsMaximum = emittedMaximum;

            void EmitCorner(float localX, float localY, Color32 color)
            {
                Vector3 position = TextStudio2DPrimitiveSupport.Bilinear(corners.Position0, corners.Position1, corners.Position2, corners.Position3, localX, localY);
                Vector4 uv0 = new Vector4(Mathf.LerpUnclamped(em.x, em.z, localX), Mathf.LerpUnclamped(em.y, em.w, localY), 0f, scale);
                Vector2 textCoordinate = NormalizeSourcePosition(new Vector2(
                    Mathf.LerpUnclamped(glyphMinimum.x, glyphMaximum.x, localX),
                    Mathf.LerpUnclamped(glyphMinimum.y, glyphMaximum.y, localY)), visualSourceBounds);
                batch.EmitVertex(position, new Vector3(textCoordinate.x, textCoordinate.y, -1f), color, uv0, new Vector4(localX, localY, 0f, 0f),
                    Vector4.zero, Vector4.zero, selectors, new Vector4(localX, localY, packedGlyph, 0f));
                Vector2 position2D = new Vector2(position.x, position.y);
                emittedMinimum = Vector2.Min(emittedMinimum, position2D);
                emittedMaximum = Vector2.Max(emittedMaximum, position2D);
            }
        }

        /// <summary>
        /// Local primitive rectangle (min x, min y, max x, max y) covering every operation's support around a glyph quad whose
        /// unit square spans <paramref name="fieldPixelLocal"/> per field pixel.
        /// </summary>
        Vector4 ResolveGlyphExtent(CompiledTextStudio2DStyle style, in TextStudioVisualGlyph glyph, Vector2 fieldPixelLocal,
            Vector3 bottomLeft, Vector3 topLeft, Vector3 bottomRight)
        {
            TextStudio2DBoundsExpansion expansion = default;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!DrawsGlyphRow(in operation)) continue;
                TextStudio2DBoundsExpansion local = LocalRowExpansion(in operation, in glyph, fieldPixelLocal, bottomLeft, topLeft, bottomRight);
                expansion = expansion.Max(in local);
            }
            return RowExtent(in expansion, fieldPixelLocal);
        }

        void EmitGlyph(
            LayerBatch batch,
            in TextStudioFrame frame,
            in TextStudioVisualSnapshot snapshot,
            in TextStudioVisualGlyph glyph,
            int glyphIndex,
            in TMP_MeshInfo meshInfo,
            int materialIndex,
            int vertexIndex,
            Texture atlas,
            Material material,
            TextStudioTMPFrameApplier frameApplier,
            Vector4 selectors,
            ref Vector2 boundsMinimum,
            ref Vector2 boundsMaximum)
        {
            Vector3 bottomLeft = meshInfo.vertices[vertexIndex];
            Vector3 topLeft = meshInfo.vertices[vertexIndex + 1];
            Vector3 topRight = meshInfo.vertices[vertexIndex + 2];
            Vector3 bottomRight = meshInfo.vertices[vertexIndex + 3];

            Vector4 uvBottomLeft = TextStudio2DPrimitiveSupport.ToVector4(meshInfo.uvs0[vertexIndex]);
            Vector4 uvTopLeft = TextStudio2DPrimitiveSupport.ToVector4(meshInfo.uvs0[vertexIndex + 1]);
            Vector4 uvTopRight = TextStudio2DPrimitiveSupport.ToVector4(meshInfo.uvs0[vertexIndex + 2]);
            Vector4 uvBottomRight = TextStudio2DPrimitiveSupport.ToVector4(meshInfo.uvs0[vertexIndex + 3]);
            float uvWidth = Maximum(uvBottomLeft.x, uvTopLeft.x, uvTopRight.x, uvBottomRight.x) -
                            Minimum(uvBottomLeft.x, uvTopLeft.x, uvTopRight.x, uvBottomRight.x);
            float uvHeight = Maximum(uvBottomLeft.y, uvTopLeft.y, uvTopRight.y, uvBottomRight.y) -
                             Minimum(uvBottomLeft.y, uvTopLeft.y, uvTopRight.y, uvBottomRight.y);
            Vector2 fieldPixelLocal = new Vector2(1f / Mathf.Max(MinimumExtent, uvWidth * atlas.width),
                1f / Mathf.Max(MinimumExtent, uvHeight * atlas.height));
            Vector4 extent = ResolveGlyphExtent(frame.AppearanceStyle, in glyph, fieldPixelLocal, bottomLeft, topLeft, bottomRight);
            float minimumX = extent.x, minimumY = extent.y, maximumX = extent.z, maximumY = extent.w;

            Vector2 materialUvBottomLeft = meshInfo.uvs2[vertexIndex];
            Vector2 materialUvTopLeft = meshInfo.uvs2[vertexIndex + 1];
            Vector2 materialUvTopRight = meshInfo.uvs2[vertexIndex + 2];
            Vector2 materialUvBottomRight = meshInfo.uvs2[vertexIndex + 3];
            Color32[] colors = meshInfo.colors32;
            Vector2 emittedMinimum = boundsMinimum;
            Vector2 emittedMaximum = boundsMaximum;
            Vector2 glyphBoundsMinimum = glyph.BoundsMin;
            Vector2 glyphBoundsMaximum = glyph.BoundsMax;
            Vector4 visualSourceBounds = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetDomainBounds(frame.AppearanceOccurrenceIndex) : snapshot.SourceBounds;
            int mappingGlyphIndex = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetMappingGlyphIndex(frame.AppearanceOccurrenceIndex, glyphIndex) : glyphIndex;
            for (int corner = 0; corner < 4; corner++)
            {
                ref RowCorner data = ref _rowCorners[corner];
                data.Color = colors[vertexIndex + corner];
                data.Squiggle = frameApplier.GetSquigglePayload(materialIndex, vertexIndex + corner);
                frameApplier.TryGetMaterialPayload(materialIndex, vertexIndex + corner, out data.Payload0, out data.Payload1, out data.Local);
                data.NormalZ = frameApplier.GetSourceCoordinateNormal(materialIndex, vertexIndex + corner).z;
            }
            int rowClasses = _occurrenceState.RowMaskClassCount;
            bool trimmed = rowClasses > 1 && TrimsToRowMasks(bottomLeft, topLeft, topRight, bottomRight,
                uvBottomLeft, uvTopLeft, uvTopRight, uvBottomRight);

            EmitCorner(0, minimumX, minimumY, true);
            EmitCorner(1, minimumX, maximumY, true);
            EmitCorner(2, maximumX, maximumY, true);
            EmitCorner(3, maximumX, minimumY, true);
            if (trimmed)
            {
                _occurrenceState.RowMaskGlyphCount++;
                batch.EmitTriangles(0);
                // Trimmed quads lie inside the shared quad, so the emitted bounds stay those of class 0.
                for (int rowClass = 1; rowClass < rowClasses; rowClass++)
                {
                    ref readonly TextStudio2DCompiledOperation representative =
                        ref frame.AppearanceStyle.OperationAt(_occurrenceState.RowMaskRepresentative[rowClass]);
                    TextStudio2DBoundsExpansion expansion = LocalRowExpansion(in representative, in glyph, fieldPixelLocal, bottomLeft, topLeft, bottomRight);
                    Vector4 rowExtent = RowExtent(in expansion, fieldPixelLocal);
                    EmitCorner(0, rowExtent.x, rowExtent.y, false);
                    EmitCorner(1, rowExtent.x, rowExtent.w, false);
                    EmitCorner(2, rowExtent.z, rowExtent.w, false);
                    EmitCorner(3, rowExtent.z, rowExtent.y, false);
                    batch.EmitTriangles(rowClass);
                }
            }
            else
                batch.EmitTriangles();
            boundsMinimum = emittedMinimum;
            boundsMaximum = emittedMaximum;

            void EmitCorner(int sourceCorner, float localX, float localY, bool includeBounds)
            {
                RowCorner data = _rowCorners[sourceCorner];
                Color32 color = data.Color;
                Vector3 position = TextStudio2DPrimitiveSupport.Bilinear(
                    bottomLeft,
                    topLeft,
                    topRight,
                    bottomRight,
                    localX,
                    localY);
                Vector4 uv0 = TextStudio2DPrimitiveSupport.Bilinear(
                    uvBottomLeft,
                    uvTopLeft,
                    uvTopRight,
                    uvBottomRight,
                    localX,
                    localY);
                Vector4 uv1 = TextStudio2DPrimitiveSupport.Bilinear(
                    materialUvBottomLeft,
                    materialUvTopLeft,
                    materialUvTopRight,
                    materialUvBottomRight,
                    localX,
                    localY);
                Vector2 squiggle = data.Squiggle;
                uv1.z = squiggle.x;
                uv1.w = squiggle.y;
                Vector4 payload0 = data.Payload0;
                Vector4 payload1 = data.Payload1;
                Vector4 localPayload = data.Local;
                localPayload.x = localX;
                localPayload.y = localY;
                localPayload.z = PackGlyphAndPixelPayload(
                    localPayload.z,
                    mappingGlyphIndex);
                float normalZ = data.NormalZ;
                Vector2 sourcePosition = new Vector2(
                    Mathf.LerpUnclamped(glyphBoundsMinimum.x, glyphBoundsMaximum.x, localX),
                    Mathf.LerpUnclamped(glyphBoundsMinimum.y, glyphBoundsMaximum.y, localY));
                Vector2 textCoordinate = NormalizeSourcePosition(
                    sourcePosition,
                    visualSourceBounds);
                batch.EmitVertex(
                    position,
                    new Vector3(textCoordinate.x, textCoordinate.y, normalZ),
                    color,
                    uv0,
                    uv1,
                    payload0,
                    payload1,
                    selectors,
                    localPayload);
                if (!includeBounds) return;
                Vector2 position2D = new Vector2(position.x, position.y);
                emittedMinimum = Vector2.Min(emittedMinimum, position2D);
                emittedMaximum = Vector2.Max(emittedMaximum, position2D);
            }
        }

        static float PackGlyphAndPixelPayload(float signedPixelCellSize, int glyphIndex)
        {
            const float glyphIndexStride = 128f;
            float sign = signedPixelCellSize < 0f ? -1f : 1f;
            float pixelCellSize = Mathf.Repeat(
                Mathf.Abs(signedPixelCellSize),
                glyphIndexStride);
            return ((Mathf.Max(0, glyphIndex) + 1) * glyphIndexStride +
                    pixelCellSize) * sign;
        }

        Vector4 BuildSelectorChunks(
            CompiledTextStudio2DStyle style,
            in TextStudioFrame frame,
            int glyphIndex, TextStudioVisualSourceKind sourceKind = TextStudioVisualSourceKind.FontGlyph, bool nativeColor = false)
        {
            int chunk0 = 0;
            int chunk1 = 0;
            int chunk2 = 0;
            int chunk3 = 0;
            int colorFaceOwner = -1;
            int operationCount = Mathf.Min(
                style.OperationCount,
                TextStudio2DHeroProgram.MaximumOperations);
            for (int operationIndex = 0; operationIndex < operationCount; operationIndex++)
            {
                if (!OperationApplies(style, in frame, operationIndex, glyphIndex, sourceKind))
                    continue;
                if (nativeColor && style.OperationAt(operationIndex).Kind == TextStudio2DLayerKind.Fill)
                {
                    if (style.OperationAt(operationIndex).SurfaceOwnerOperationIndex >= 0 ||
                        style.OperationAt(operationIndex).Fill.ColorFace == TextStudio2DColorFaceMode.SilhouetteFill) continue;
                    if (colorFaceOwner < 0) colorFaceOwner = operationIndex;
                }
                _occurrenceState.OperationHasSelection[operationIndex] = true;
                int bit = 1 << (operationIndex & 15);
                switch (operationIndex >> 4)
                {
                    case 0:
                        chunk0 |= bit;
                        break;
                    case 1:
                        chunk1 |= bit;
                        break;
                    case 2:
                        chunk2 |= bit;
                        break;
                    default:
                        chunk3 |= bit;
                        break;
                }
            }
            Vector4 chunks = new Vector4(chunk0, chunk1, chunk2, chunk3);
            if (nativeColor)
                for (int index = 0; index < operationCount; index++)
                    if (index != colorFaceOwner && style.OperationAt(index).Kind == TextStudio2DLayerKind.Fill)
                        chunks[index >> 4] = (int)chunks[index >> 4] & ~(1 << (index & 15));
            return chunks;
        }

        bool OperationApplies(
            CompiledTextStudio2DStyle style,
            in TextStudioFrame frame,
            int operationIndex,
            int glyphIndex, TextStudioVisualSourceKind sourceKind = TextStudioVisualSourceKind.FontGlyph)
        {
            int current = operationIndex;
            int depth = 0;
            while (current >= 0 && depth++ <= TextStudio2DHeroProgram.MaximumOperations)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(current);
                if (!TextStudio2DSourceApplicabilityUtility.Includes(in operation, sourceKind)) return false;
                int selectorIndex = operation.SelectorIndex;
                if (selectorIndex < 0 || selectorIndex >= style.SelectorCount)
                    return false;
                TextStudio2DCompiledSelector selector = style.GetSelector(selectorIndex);
                bool selected = frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                    ? frame.AppearanceBinding.IncludesOperation(frame.AppearanceOccurrenceIndex, current, glyphIndex)
                    : TextStudio2DSelectorEvaluator.Evaluate(in selector, in frame, glyphIndex, _selectorPass);
                if (!selected)
                {
                    return false;
                }
                current = operation.ParentOperationIndex;
            }
            return current < 0;
        }

        LayerBatch GetBatch(Material source, Texture atlas, float pixelsPerEm, int colorMode = 0, float distanceRange = 0)
        {
            if (_occurrenceState.ActiveBatchCount > 0 && _occurrenceState.Batches[_occurrenceState.ActiveBatchCount - 1].Matches(source, atlas, pixelsPerEm, colorMode, distanceRange))
                return _occurrenceState.Batches[_occurrenceState.ActiveBatchCount - 1];
            // Reuse storage, but keep every noncontiguous native material run in its draw position.
            for (int index = _occurrenceState.ActiveBatchCount; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                if (!batch.Matches(source, atlas, pixelsPerEm, colorMode, distanceRange))
                    continue;
                _occurrenceState.Batches[index] = _occurrenceState.Batches[_occurrenceState.ActiveBatchCount];
                _occurrenceState.Batches[_occurrenceState.ActiveBatchCount++] = batch;
                if (!batch.Active)
                {
                    batch.Active = true;
                    TextStudio2DHeroLayerMaterialLease currentLease = batch.Lease;
                    TextStudio2DHeroMaterials.SynchronizeLayer(in currentLease);
                }
                return batch;
            }

            if (!TextStudio2DHeroMaterials.TryAcquireLayer(
                    source,
                    atlas,
                    out TextStudio2DHeroLayerMaterialLease lease, pixelsPerEm, colorMode, distanceRange))
            {
                return null;
            }
            LayerBatch created = new LayerBatch(in lease);
            created.BeginBuild(_occurrenceState.RowMaskClassCount);
            created.Active = true;
            _occurrenceState.Batches.Insert(_occurrenceState.ActiveBatchCount++, created);
            return created;
        }

        void RemoveUnusedBatches()
        {
            for (int index = _occurrenceState.Batches.Count - 1; index >= 0; index--)
            {
                if (_occurrenceState.Batches[index].Active)
                    continue;
                _occurrenceState.Batches[index].Dispose();
                _occurrenceState.Batches.RemoveAt(index);
            }
        }

        float ResolveSourcePixelsPerUnit(in TextStudioFrame frame, int styleSlot)
        {
            float density = .25f;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            IReadOnlyList<int> occurrenceGlyphs = OccurrenceRenderGlyphs(in frame);
            int sourceCount = occurrenceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            for (int source = 0; source < sourceCount; source++)
            {
                int index = occurrenceGlyphs == null ? source : occurrenceGlyphs[source];
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph)
                {
                    TextStudioColorSource color = snapshot.GetColorSource(in glyph);
                    if (color != null) density = Mathf.Max(density, ResolveColorDensity(color) / Mathf.Max(MinimumExtent, glyph.LocalEmSize));
                    continue;
                }
                Vector4 prepared = TextStudio2DMappingTable.Shared.GetPreparedQuality(styleSlot, index);
                float pixels = prepared.x > 0 ? prepared.z : glyph.FieldPixelsPerEm;
                float em = glyph.LocalEmSize > 0 ? glyph.LocalEmSize : frame.GetEffectUnitScale(glyph.TextUnitIndex);
                density = Mathf.Max(density, pixels / Mathf.Max(MinimumExtent, em));
            }
            return density;
        }

        bool ResolveTargetLayout(
            float sourcePixelsPerUnit,
            in Rect geometryBounds,
            TextStudio2DQualityTier qualityTier,
            bool requiresPostProcessGuard,
            float samplingScale,
            TextStudio2DHeroProgram program,
            bool hasLensMagnifier,
            out int targetWidth,
            out int targetHeight,
            out float pixelsPerUnit,
            out Rect renderBounds)
        {
            renderBounds = default;
            float qualityScale = qualityTier switch
            {
                TextStudio2DQualityTier.Responsive => 0.8f,
                TextStudio2DQualityTier.Hero => HeroSamplingScale,
                _ => 1f
            };
            pixelsPerUnit = Mathf.Max(
                0.25f,
                sourcePixelsPerUnit * qualityScale *
                Mathf.Clamp(samplingScale, 1f, 2.5f));
            float guardPixels = requiresPostProcessGuard
                ? PostProcessGuardPixels
                : OutputSamplingGuardPixels;
            float geometryPixelWidth = Mathf.Max(
                MinimumExtent,
                geometryBounds.width * pixelsPerUnit);
            float geometryPixelHeight = Mathf.Max(
                MinimumExtent,
                geometryBounds.height * pixelsPerUnit);
            float availableDimension = Mathf.Max(
                8f,
                TextStudio2DHeroTargetPool.MaximumDimension - guardPixels * 2f);
            float fitScale = Mathf.Min(
                1f,
                Mathf.Min(
                    availableDimension / geometryPixelWidth,
                    availableDimension / geometryPixelHeight));
            pixelsPerUnit *= fitScale;
            targetWidth = Mathf.CeilToInt(
                geometryBounds.width * pixelsPerUnit + guardPixels * 2f);
            targetHeight = Mathf.CeilToInt(
                geometryBounds.height * pixelsPerUnit + guardPixels * 2f);
            TextStudio2DHeroTargetPool.TryResolveDescriptor(
                targetWidth,
                targetHeight,
                qualityTier,
                out TextStudio2DHeroTargetDescriptor descriptor,
                out _);
            targetWidth = descriptor.Width;
            targetHeight = descriptor.Height;
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            long replacementBytes = pool.GetByteSize(_finalLease);
            for (int index = 0; index < _occurrenceState.HistoryLeases.Length; index++)
            {
                replacementBytes += pool.GetByteSize(_occurrenceState.HistoryLeases[index]);
                replacementBytes += pool.GetByteSize(_occurrenceState.HistoryCoverageLeases[index]);
            }
            if (_appearancePlan != null)
            {
                replacementBytes = pool.GetByteSize(_finalLease) + pool.GetByteSize(_appearanceShadowLeases[0]);
                foreach (OccurrenceState state in _activeAppearanceStates)
                    for (int index = 0; index < state.HistoryLeases.Length; index++)
                        replacementBytes += pool.GetByteSize(state.HistoryLeases[index]) + pool.GetByteSize(state.HistoryCoverageLeases[index]);
            }
            long budget = pool.AvailableBytes + replacementBytes;
            // Reserve both generations during target replacement, including retained history.
            int fullResolutionCount = program.EstimatedConcurrentSurfaceCount - program.DownsampledBlurScratchCount - program.HistorySurfaceCount +
                                      (hasLensMagnifier && program.SpatialScratchCount == 0 ? 1 : 0);
            int originalWidth = targetWidth;
            int originalHeight = targetHeight;
            if (!TextStudio2DHeroTargetPool.TryFitCompositionBudget(ref targetWidth, ref targetHeight,
                    qualityTier, _appearancePlan != null ? _appearanceFullResolutionCount : fullResolutionCount,
                    _appearancePlan != null ? _appearanceBlurCount : program.DownsampledBlurScratchCount,
                    _appearancePlan != null ? _appearanceBlurFactor : program.DownsampledBlurFactor, budget,
                    _appearancePlan != null && _hasSelectiveAppearanceShadows ? 2 : 1, replacementBytes,
                    _appearancePlan != null ? _appearanceHistoryCount : program.MaximumHistoryFrames,
                    _appearancePlan != null ? _appearanceHistoryCoverageCount : program.NeedsHistoryCoverage ? program.MaximumHistoryFrames : 0,
                    (_appearancePlan != null ? AppearanceRequiresConnectedSurfaceUnion() : RequiresConnectedSurfaceUnion(program)) ? 1 : 0)) return false;
            pixelsPerUnit *= Mathf.Min(targetWidth / (float)originalWidth, targetHeight / (float)originalHeight);
            Vector2 center = geometryBounds.center;
            Vector2 size = new Vector2(
                targetWidth / pixelsPerUnit,
                targetHeight / pixelsPerUnit);
            renderBounds = new Rect(center - size * 0.5f, size);
            return true;
        }

        static bool RequiresConnectedSurfaceUnion(TextStudio2DHeroProgram program)
        {
            if (program == null) return false;
            for (int index = 0; index < program.Style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref program.Style.OperationAt(index);
                if (operation.Kind == TextStudio2DLayerKind.Decoration &&
                    operation.Decoration.Topology == TextStudio2DDecorationTopology.ConnectedRange) return true;
            }
            return false;
        }

        bool TryPrepareTargets(
            int width,
            int height,
            TextStudio2DQualityTier qualityTier,
            int nestedPairCount,
            int spatialScratchCount,
            int blurScratchCount,
            int blurDownsampleFactor,
            int coveragePairCount,
            bool needsGeneratedAlpha)
        {
            spatialScratchCount = Mathf.Clamp(spatialScratchCount, 0, 1);
            blurScratchCount = Mathf.Clamp(blurScratchCount, 0, 3);
            blurDownsampleFactor = Mathf.Clamp(
                blurDownsampleFactor,
                1,
                ProgressiveBlurDownsampleFactor);
            int fullResolutionScratchCount = 2 +
                                             nestedPairCount * 2 +
                                             spatialScratchCount + coveragePairCount * 2 + (needsGeneratedAlpha ? 1 : 0);
            int requiredScratchCount = fullResolutionScratchCount +
                                       blurScratchCount;
            if (nestedPairCount < 0 || requiredScratchCount > _scratchLeases.Length)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.TargetCapacityExceeded);
                return false;
            }
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            TextStudio2DHeroTargetPool.TryResolveDescriptor(
                width,
                height,
                qualityTier,
                out TextStudio2DHeroTargetDescriptor descriptor,
                out TextStudio2DHeroTargetStatus descriptorStatus);
            if (descriptorStatus != TextStudio2DHeroTargetStatus.Ready)
            {
                SetTargetFailure(descriptorStatus);
                return false;
            }

            TextStudio2DHeroTargetLease newFinalLease = default;
            RenderTexture currentFinal = pool.GetTexture(_finalLease);
            bool replaceFinal = currentFinal == null ||
                                currentFinal.width != descriptor.Width ||
                                currentFinal.height != descriptor.Height ||
                                currentFinal.graphicsFormat != descriptor.Format;
            if (replaceFinal &&
                !pool.TryAcquire(
                    width,
                    height,
                    qualityTier,
                    out newFinalLease,
                    out TextStudio2DHeroTargetStatus finalStatus))
            {
                SetTargetFailure(finalStatus);
                return false;
            }

            _scratchCount = 0;
            for (int index = 0; index < fullResolutionScratchCount; index++)
            {
                if (pool.TryAcquire(
                        width,
                        height,
                        qualityTier,
                        out TextStudio2DHeroTargetLease lease,
                        out TextStudio2DHeroTargetStatus status,
                        withDepth: index == 0 && (_appearancePlan != null ? AppearanceRequiresConnectedSurfaceUnion() : RequiresConnectedSurfaceUnion(_viewProgram))))
                {
                    _scratchLeases[_scratchCount] = lease;
                    _scratchTextures[_scratchCount] = pool.GetTexture(lease);
                    _scratchCount++;
                    continue;
                }

                ReleaseScratchTargets();
                if (newFinalLease.IsValid)
                    pool.Release(ref newFinalLease);
                SetTargetFailure(status);
                return false;
            }

            int blurWidth = Mathf.Max(
                8,
                (width + blurDownsampleFactor - 1) /
                blurDownsampleFactor);
            int blurHeight = Mathf.Max(
                8,
                (height + blurDownsampleFactor - 1) /
                blurDownsampleFactor);
            for (int index = fullResolutionScratchCount;
                 index < requiredScratchCount;
                 index++)
            {
                if (pool.TryAcquire(
                        blurWidth,
                        blurHeight,
                        qualityTier,
                        out TextStudio2DHeroTargetLease lease,
                        out TextStudio2DHeroTargetStatus status))
                {
                    _scratchLeases[_scratchCount] = lease;
                    _scratchTextures[_scratchCount] = pool.GetTexture(lease);
                    _scratchCount++;
                    continue;
                }

                ReleaseScratchTargets();
                if (newFinalLease.IsValid)
                    pool.Release(ref newFinalLease);
                SetTargetFailure(status);
                return false;
            }

            if (replaceFinal)
            {
                pool.Release(ref _finalLease);
                _finalLease = newFinalLease;
                _finalTexture = pool.GetTexture(_finalLease);
            }
            else
            {
                _finalTexture = currentFinal;
            }
            if (!TryAcquireOutput(ref _output, _finalTexture))
            {
                ReleaseScratchTargets();
                SetTargetFailure(TextStudio2DHeroTargetStatus.MemoryBudgetExceeded);
                return false;
            }
            _layerTexture = _scratchTextures[0];
            _rootScratchTexture = _scratchTextures[1];
            _nestedPairCount = nestedPairCount;
            int effectScratchStart = 2 + nestedPairCount * 2;
            _coverageScratchStart = effectScratchStart + spatialScratchCount;
            _coveragePairCount = coveragePairCount;
            _generatedAlphaTexture = needsGeneratedAlpha ? _scratchTextures[fullResolutionScratchCount - 1] : null;
            _effectScratchTexture0 = spatialScratchCount > 0
                ? _scratchTextures[effectScratchStart]
                : null;
            int blurScratchStart = fullResolutionScratchCount;
            _blurScratchTexture0 = blurScratchCount > 0
                ? _scratchTextures[blurScratchStart]
                : null;
            _blurScratchTexture1 = blurScratchCount > 1
                ? _scratchTextures[blurScratchStart + 1]
                : null;
            _blurCoverageTexture = blurScratchCount > 2 ? _scratchTextures[blurScratchStart + 2] : null;
            _blurScratchScaleX = _blurScratchTexture0 != null
                ? width / (float)_blurScratchTexture0.width
                : 1f;
            _blurScratchScaleY = _blurScratchTexture0 != null
                ? height / (float)_blurScratchTexture0.height
                : 1f;
            return true;
        }

        bool TryPrepareHistoryTargets(
            int width,
            int height,
            TextStudio2DQualityTier qualityTier,
            int historyFrameCount, bool needsCoverage)
        {
            historyFrameCount = Mathf.Clamp(
                historyFrameCount,
                0,
                MaximumHistoryFrames);
            if (historyFrameCount == 0)
            {
                ReleaseHistoryTargets();
                return true;
            }

            int historyWidth = Mathf.Max(8, (width + 1) / 2);
            int historyHeight = Mathf.Max(8, (height + 1) / 2);
            TextStudio2DHeroTargetPool.TryResolveDescriptor(
                historyWidth,
                historyHeight,
                qualityTier,
                out TextStudio2DHeroTargetDescriptor descriptor,
                out TextStudio2DHeroTargetStatus descriptorStatus);
            if (descriptorStatus != TextStudio2DHeroTargetStatus.Ready)
            {
                ReleaseScratchTargets();
                SetTargetFailure(descriptorStatus);
                return false;
            }

            TextStudio2DHeroTargetPool.TryResolveDescriptor(historyWidth, historyHeight, qualityTier,
                out TextStudio2DHeroTargetDescriptor coverageDescriptor, out TextStudio2DHeroTargetStatus coverageStatus, true);
            if (needsCoverage && coverageStatus != TextStudio2DHeroTargetStatus.Ready)
            {
                ReleaseScratchTargets(); SetTargetFailure(coverageStatus); return false;
            }
            bool canReuse = _occurrenceState.HistorySurfaceCount == historyFrameCount && _occurrenceState.HistoryCoverageCount == (needsCoverage ? historyFrameCount : 0);
            for (int index = 0; canReuse && index < historyFrameCount; index++)
            {
                RenderTexture texture = _occurrenceState.HistoryTextures[index];
                canReuse = texture != null &&
                           texture.width == descriptor.Width &&
                           texture.height == descriptor.Height &&
                           texture.graphicsFormat == descriptor.Format;
                if (needsCoverage)
                {
                    RenderTexture coverage = _occurrenceState.HistoryCoverageTextures[index];
                    canReuse &= coverage != null && coverage.width == coverageDescriptor.Width &&
                                coverage.height == coverageDescriptor.Height && coverage.graphicsFormat == coverageDescriptor.Format;
                }
            }
            if (canReuse)
                return true;

            ReleaseHistoryTargets();
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            for (int index = 0; index < historyFrameCount; index++)
            {
                if (!pool.TryAcquire(
                        historyWidth,
                        historyHeight,
                        qualityTier,
                        out TextStudio2DHeroTargetLease lease,
                        out TextStudio2DHeroTargetStatus status))
                {
                    ReleaseHistoryTargets();
                    ReleaseScratchTargets();
                    SetTargetFailure(status);
                    return false;
                }
                _occurrenceState.HistoryLeases[index] = lease;
                _occurrenceState.HistoryTextures[index] = pool.GetTexture(lease);
                _occurrenceState.HistorySurfaceCount++;
                if (needsCoverage)
                {
                    if (!pool.TryAcquire(historyWidth, historyHeight, qualityTier, out TextStudio2DHeroTargetLease coverageLease,
                            out TextStudio2DHeroTargetStatus coverageTargetStatus, true))
                    {
                        ReleaseHistoryTargets(); ReleaseScratchTargets(); SetTargetFailure(coverageTargetStatus); return false;
                    }
                    _occurrenceState.HistoryCoverageLeases[index] = coverageLease;
                    _occurrenceState.HistoryCoverageTextures[index] = pool.GetTexture(coverageLease);
                    _occurrenceState.HistoryCoverageCount++;
                }
            }
            ResetHistoryState();
            return true;
        }

        bool ShouldResetHistory(
            in TextStudioFrame frame,
            TextStudio2DHeroProgram program)
        {
            if (!program.RequiresHistory)
                return false;
            if (!_occurrenceState.HasHistoryTime ||
                !_occurrenceState.HistoryFingerprint.Equals(program.Fingerprint) ||
                _occurrenceState.HistorySurfaceCount != program.MaximumHistoryFrames)
            {
                return true;
            }

            DirtyFlags structural = DirtyFlags.Text |
                                    DirtyFlags.Style |
                                    DirtyFlags.Topology |
                                    DirtyFlags.Layout |
                                    DirtyFlags.Placement |
                                    DirtyFlags.AppearanceTopology |
                                    DirtyFlags.AppearanceRenderState;
            // Invocation programs and membership already have independent revisions. A sibling's
            // render-state change must not clear this occurrence's temporal accumulation.
            if (frame.AppearanceOccurrenceIndex >= 0)
                structural &= ~(DirtyFlags.AppearanceTopology | DirtyFlags.AppearanceRenderState);
            if ((frame.GlobalDirty & structural) != 0)
                return true;

            float elapsed = frame.PresentationTime - _occurrenceState.LastRenderTime;
            if (elapsed < -0.0001f || elapsed > 0.25f)
                return true;
            if (Mathf.Abs(elapsed) > 0.0001f && frame.DeltaTime <= 0f)
                return true;
            if (frame.DeltaTime > 0f &&
                Mathf.Abs(elapsed - frame.DeltaTime) >
                Mathf.Max(0.05f, frame.DeltaTime * 0.5f))
            {
                return true;
            }
            return false;
        }

        void ResetHistoryState()
        {
            _occurrenceState.HistoryValidCount = 0;
            _occurrenceState.HistoryCursor = 0;
            _occurrenceState.PendingHistoryOperation = -1;
            _occurrenceState.HistoryCapturePending = false;
            _occurrenceState.HistoryLayerCaptured = false;
            _occurrenceState.HasHistoryTime = false;
        }

        void BuildCompositionCommands(
            TextStudio2DHeroProgram program,
            Material composite,
            float time,
            TextStudioMotionPolicy motionPolicy)
        {
            _occurrenceState.PendingHistoryOperation = -1;
            _occurrenceState.HistoryCapturePending = false;
            _occurrenceState.HistoryLayerCaptured = false;
            float surfacePixelUnit = 1f / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit);
            _commands.SetGlobalVector(s_surfacePixelUnitProperty, new Vector4(surfacePixelUnit, surfacePixelUnit,
                _pixelsPerUnit / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit), 0f));
            _commands.SetGlobalFloat(s_heroPixelScaleProperty, _pixelsPerUnit / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit));
            _commands.SetGlobalFloat(s_timeProperty, time);
            _commands.SetGlobalInt(s_motionPolicyProperty, (int)motionPolicy);
            _commands.SetGlobalInt(s_rowMaskProperty, (int)CompareFunction.Disabled);
            Matrix4x4 projection = Matrix4x4.Ortho(
                _renderBounds.xMin,
                _renderBounds.xMax,
                _renderBounds.yMin,
                _renderBounds.yMax,
                -1f,
                1f);
            // Composition follows painter order on the text's XY plane. Glyph rotations
            // can produce arbitrary local Z, which must not clip the isolated capture.
            projection.m22 = 0f;
            projection.m23 = 0f;
            _commands.SetViewProjectionMatrices(
                Matrix4x4.identity,
                GL.GetGPUProjectionMatrix(projection, true) * _captureProjection);
            CompositionPair root = new CompositionPair(
                _finalTexture,
                _rootScratchTexture);
            _glyphFilter = -1;
            _commands.SetGlobalInt(s_glyphFilterProperty, -1);
            RenderTexture result = ComposeScope(program, -1, in root, 0, composite);
            if (result != null && result != _finalTexture)
            {
                if (PassElisionEnabled && result == _rootScratchTexture && _scratchCount > 1 && _scratchTextures[1] == result &&
                    _finalTexture == TextStudio2DHeroTargetPool.Shared.GetTexture(_finalLease))
                {
                    // Both surfaces are leases of one descriptor; the composed one becomes the final image. The leases swap with the
                    // textures, so the final lease keeps owning the surface that holds the composite after this render releases its
                    // scratch targets, and the released scratch lease returns the previous final surface to the pool.
                    (_finalLease, _scratchLeases[1]) = (_scratchLeases[1], _finalLease);
                    _scratchTextures[1] = _finalTexture;
                    _rootScratchTexture = _finalTexture;
                    _finalTexture = result;
                }
                else
                {
                    _commands.Blit(result, _finalTexture);
                    _lastCompositePassCount++;
                }
            }
            if (!_compositionFailed && _occurrenceState.PendingHistoryOperation >= 0) CaptureHistory();
            // A failed frame advances no history, but an early capture already replaced the oldest frame of the ring.
            else if (_occurrenceState.HistoryLayerCaptured) ResetHistoryState();
        }

        void SetGlyphFilter(int unit)
        {
            if (_glyphFilter == unit) return;
            _glyphFilter = unit;
            _commands.SetGlobalInt(s_glyphFilterProperty, unit);
        }

        // A glyph's run equals its isolated composite drawn over the scope target when every row blends Normal: masks and
        // opacity only scale each source, so premultiplied source-over stays associative and the per-glyph surface is redundant.
        static bool ComposesOntoBackdrop(TextStudio2DHeroProgram program, int lastOperationIndex, int stopOperationIndex)
        {
            for (int index = lastOperationIndex, guard = 0; index >= 0 && index != stopOperationIndex && guard <= program.OperationCount; guard++)
            {
                if (program.Style.OperationAt(index).BlendMode != TextStudio2DBlendMode.Normal) return false;
                index = program.GetNode(index).PreviousSiblingOperationIndex;
            }
            return true;
        }

        static bool IsImageBoundary(in TextStudio2DCompiledOperation operation) =>
            operation.Kind == TextStudio2DLayerKind.CompositeAdjustment ||
            operation.Kind == TextStudio2DLayerKind.AnimatedEffect && TextStudio2DAnimatedEffectUtility.RequiresPostProcess(operation.AnimatedEffect.Kind);

        RenderTexture ComposeScope(TextStudio2DHeroProgram program, int scopeIndex, in CompositionPair pair,
            int nestedDepth, Material composite)
        {
            int last = scopeIndex < 0 ? program.LastRootOperationIndex : program.GetNode(scopeIndex).LastChildOperationIndex;
            return program.GetPainterOrder(scopeIndex) == TextStudio2DPainterOrder.GlyphMajor
                ? ComposeGlyphMajor(program, scopeIndex, last, in pair, nestedDepth, composite)
                : ComposeRange(program, last, in pair, nestedDepth, composite, root: scopeIndex < 0);
        }

        RenderTexture ComposeGlyphMajor(TextStudio2DHeroProgram program, int scopeIndex, int lastOperationIndex,
            in CompositionPair root, int nestedDepth, Material composite, int stopOperationIndex = -1)
        {
            if (!TryGetNestedPair(nestedDepth, out CompositionPair glyphPair))
            {
                _compositionFailed = true;
                return null;
            }
            ClearTarget(root.First);
            RenderTexture current = root.First;
            RenderTexture alternate = root.Second;
            ComposeSharedSurfaces(program, lastOperationIndex, TextStudio2DSharedSurfaceOrder.BeforeCharacters,
                ref current, ref alternate, nestedDepth + 1, composite);
            int runStart = lastOperationIndex;
            int cursor = lastOperationIndex;
            // Neighborhood filters and composite adjustments consume the completed image of the lower character run.
            for (int guard = 0; guard <= program.OperationCount; guard++)
            {
                bool end = cursor < 0 || cursor == stopOperationIndex;
                bool boundary = !end && IsImageBoundary(program.Style.OperationAt(cursor));
                if (end || boundary)
                {
                    bool composesOnto = runStart != cursor && ComposesOntoBackdrop(program, runStart, cursor);
                    for (int draw = 0; draw < _occurrenceState.DrawingUnits.Count && runStart != cursor; draw++)
                    {
                        int unit = _occurrenceState.DrawingUnits[draw];
                        if (scopeIndex >= 0 && !_occurrenceState.PainterScopes.Contains(scopeIndex, unit)) continue;
                        SetGlyphFilter(unit);
                        if (composesOnto)
                        {
                            RenderTexture drawn = ComposeRange(program, runStart, new CompositionPair(current, alternate),
                                nestedDepth + 1, composite, cursor, true, clear: false);
                            if (_compositionFailed || drawn == null) return null;
                            if (drawn != current) { alternate = current; current = drawn; }
                            continue;
                        }
                        RenderTexture glyphResult = ComposeRange(program, runStart, in glyphPair,
                            nestedDepth + 1, composite, cursor, true);
                        if (_compositionFailed || glyphResult == null) return null;
                        MaskState noMask = default;
                        Composite(current, glyphResult, alternate, TextStudio2DBlendMode.Normal, 1f, false, in noMask, composite);
                        RenderTexture swap = current; current = alternate; alternate = swap;
                    }
                    SetGlyphFilter(-1);
                    if (end) break;
                    RenderTexture next = ComposeNode(program, cursor, current, alternate, nestedDepth + 1, composite);
                    if (next != current) { alternate = current; current = next; }
                    runStart = program.GetNode(cursor).PreviousSiblingOperationIndex;
                }
                cursor = program.GetNode(cursor).PreviousSiblingOperationIndex;
            }
            if (stopOperationIndex < 0)
                ComposeSharedSurfaces(program, lastOperationIndex, TextStudio2DSharedSurfaceOrder.AfterCharacters,
                    ref current, ref alternate, nestedDepth + 1, composite);
            return current;
        }

        void ComposeSharedSurfaces(TextStudio2DHeroProgram program, int last, TextStudio2DSharedSurfaceOrder order,
            ref RenderTexture current, ref RenderTexture alternate, int nestedDepth, Material composite)
        {
            SetGlyphFilter(-1);
            for (int index = last, guard = 0; index >= 0 && guard < program.OperationCount; guard++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref program.Style.OperationAt(index);
                if (operation.Kind == TextStudio2DLayerKind.Decoration && operation.SharedSurfaceOrder == order)
                {
                    RenderTexture next = ComposeNode(program, index, current, alternate, nestedDepth, composite);
                    if (next != current) { alternate = current; current = next; }
                }
                index = program.GetNode(index).PreviousSiblingOperationIndex;
            }
        }

        RenderTexture ComposeRange(
            TextStudio2DHeroProgram program,
            int lastOperationIndex,
            in CompositionPair pair,
            int nestedDepth,
            Material composite,
            int stopOperationIndex = -1,
            bool skipSharedSurfaces = false,
            bool clear = true,
            bool root = false)
        {
            if (!pair.IsValid)
            {
                _compositionFailed = true;
                return null;
            }
            // The first composed node clears the target when it reads it; a filter that overwrites it completely reads black.
            bool pendingClear = clear && PassElisionEnabled;
            if (clear && !pendingClear) ClearTarget(pair.First);
            RenderTexture current = pair.First;
            RenderTexture alternate = pair.Second;
            int operationIndex = lastOperationIndex;
            int guard = 0;
            while (operationIndex >= 0 && operationIndex != stopOperationIndex &&
                   guard++ <= TextStudio2DHeroProgram.MaximumOperations)
            {
                if (root && operationIndex == _staticRunBottom && _staticRowTexture != null)
                {
                    current = ComposeStaticRun(program, current, alternate, nestedDepth, composite, ref pendingClear);
                    if (_compositionFailed) return current;
                    operationIndex = program.GetNode(_staticRunTop).PreviousSiblingOperationIndex;
                    continue;
                }
                TextStudio2DHeroNode node = program.GetNode(operationIndex);
                RenderTexture next;
                if (skipSharedSurfaces && program.Style.OperationAt(operationIndex).Kind == TextStudio2DLayerKind.Decoration)
                    next = current;
                else
                {
                    next = ComposeNode(
                        program,
                        operationIndex,
                        current,
                        alternate,
                        nestedDepth,
                        composite,
                        pendingClear);
                    pendingClear = false;
                }
                if (next != current)
                {
                    alternate = current;
                    current = next;
                }
                operationIndex = node.PreviousSiblingOperationIndex;
            }
            if (pendingClear) ClearTarget(current);
            if (guard > TextStudio2DHeroProgram.MaximumOperations + 1)
                _compositionFailed = true;
            return current;
        }

        /// <param name="clearBackdrop">The backdrop is a range target nothing has cleared yet; it holds transparent black by definition.</param>
        RenderTexture ComposeNode(TextStudio2DHeroProgram program, int operationIndex, RenderTexture backdrop,
            RenderTexture destination, int nestedDepth, Material composite, bool clearBackdrop = false)
        {
            if (clearBackdrop && !FiltersBackdropDirectly(program, operationIndex))
            {
                ClearTarget(backdrop);
                clearBackdrop = false;
            }
            bool group = program.Style.OperationAt(operationIndex).Kind == TextStudio2DLayerKind.Group;
            int previousFilter = _glyphFilter;
            if (group && previousFilter >= 0)
            {
                if (_scopeDepth == 0 && _occurrenceState.PainterScopes.FirstUnit(operationIndex) != previousFilter) return backdrop;
                SetGlyphFilter(-1);
            }
            try { return ComposeNodeCore(program, operationIndex, backdrop, destination, nestedDepth, composite, clearBackdrop); }
            finally { SetGlyphFilter(previousFilter); }
        }

        // A Normal, unmasked backdrop filter composites as lerp(backdrop, filtered, 1): its full-resolution effect pass can write the
        // destination directly. Matches the conditions of the direct branch in ComposeNodeCore, which every such row reaches.
        bool FiltersBackdropDirectly(TextStudio2DHeroProgram program, int operationIndex)
        {
            ref readonly TextStudio2DCompiledOperation operation = ref program.Style.OperationAt(operationIndex);
            return PassElisionEnabled && operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                   WritesFullResolutionFilter(operation.AnimatedEffect.Kind) && operation.BlendMode == TextStudio2DBlendMode.Normal &&
                   operation.MaskOperationIndex < 0 && _occurrenceState.OperationHasSelection[operationIndex];
        }

        /// <summary>Post-process kinds whose pass writes a complete full-resolution image; progressive blur writes downsampled scratch.</summary>
        static bool WritesFullResolutionFilter(TextStudio2DAnimatedEffectKind kind) =>
            kind == TextStudio2DAnimatedEffectKind.GlitchDisplacement || kind == TextStudio2DAnimatedEffectKind.HeatHaze ||
            kind == TextStudio2DAnimatedEffectKind.LiquidWarp || kind == TextStudio2DAnimatedEffectKind.EchoTrails ||
            kind == TextStudio2DAnimatedEffectKind.MotionSmear;

        RenderTexture ComposeNodeCore(
            TextStudio2DHeroProgram program,
            int operationIndex,
            RenderTexture backdrop,
            RenderTexture destination,
            int nestedDepth,
            Material composite,
            bool clearBackdrop)
        {
            ref readonly TextStudio2DCompiledOperation operation =
                ref program.Style.OperationAt(operationIndex);
            if (operation.Kind == TextStudio2DLayerKind.Mask || operation.Kind == TextStudio2DLayerKind.ColorAdjustment)
                return backdrop;
            bool requiresTextWideBlur =
                TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(
                    in operation,
                    out _,
                    out _,
                    out _);

            RenderTexture source;
            int maskDepth = nestedDepth;
            if (operation.Kind == TextStudio2DLayerKind.Group)
            {
                if (!TryGetNestedPair(nestedDepth, out CompositionPair groupPair))
                {
                    _compositionFailed = true;
                    return backdrop;
                }
                source = ComposeScope(program, operationIndex, in groupPair, nestedDepth + 1, composite);
                maskDepth = nestedDepth + 1;
            }
            else
            {
                if (!_occurrenceState.OperationHasSelection[operationIndex])
                    return backdrop;
                source = null;
            }

            if (operation.Kind == TextStudio2DLayerKind.CompositeAdjustment)
                return ComposeAdjustment(program, operationIndex, in operation, backdrop, destination, maskDepth, composite);

            MaskState mask = PrepareMask(
                program,
                operation.MaskOperationIndex,
                maskDepth,
                composite);
            if (_compositionFailed)
                return backdrop;

            if (!_geometryCoveragePass && operation.Kind == TextStudio2DLayerKind.FauxDepth && _occurrenceState.GeneratedContributionRuns[operationIndex]?.Count > 0)
                return ComposeAdjustedDepth(program, operationIndex, in operation, backdrop, destination, in mask, composite);

            if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
            {
                source = RenderDepthContribution(program, operationIndex, in operation);
                if (_compositionFailed || source == null) return backdrop;
                Composite(backdrop, source, destination, operation.BlendMode, 1, false, in mask, composite);
                return destination;
            }

            // A normal, unmasked row needs no isolated surface: premultiplied source-over is associative, so drawing it
            // straight into the backdrop equals clearing a layer target, drawing the row there and compositing it.
            if (source == null && operation.BlendMode == TextStudio2DBlendMode.Normal && mask.Texture == null &&
                !requiresTextWideBlur && !_geometryCoveragePass && !UsesUnionCoverage(operationIndex) &&
                !(operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                  TextStudio2DAnimatedEffectUtility.RequiresPostProcess(operation.AnimatedEffect.Kind)))
            {
                RenderLayer(operationIndex, operationIndex, HeroLayerSourceMode.Operation, target: backdrop, clear: false);
                return backdrop;
            }

            if (source == null)
            {
                bool requiresPaintedFace = requiresTextWideBlur;
                RenderLayer(
                    operationIndex,
                    operationIndex,
                    requiresPaintedFace
                        ? HeroLayerSourceMode.PaintedFace
                        : HeroLayerSourceMode.Operation);
                source = _layerTexture;
            }
            bool sourceIsBackdropFilter = false;
            if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect &&
                TextStudio2DAnimatedEffectUtility.RequiresPostProcess(
                    operation.AnimatedEffect.Kind))
            {
                // With full contribution the composite below would only copy the filtered image, so the filter writes the destination.
                bool direct = PassElisionEnabled && operation.BlendMode == TextStudio2DBlendMode.Normal && mask.Texture == null &&
                              WritesFullResolutionFilter(operation.AnimatedEffect.Kind);
                source = ApplyAnimatedPostProcess(
                    operationIndex,
                    in operation,
                    source,
                    backdrop,
                    clearBackdrop ? Texture2D.blackTexture : backdrop,
                    direct ? destination : _effectScratchTexture0);
                if (_compositionFailed || source == null)
                    return backdrop;
                if (direct)
                    return destination;
                sourceIsBackdropFilter = true;
            }
            else if (requiresTextWideBlur)
            {
                RenderLayer(operationIndex, operationIndex, HeroLayerSourceMode.NeighborhoodCoverage, target: _effectScratchTexture0);
                source = ApplyTextWideSoftEffect(in operation, source);
                if (_compositionFailed || source == null)
                    return backdrop;
            }
            float opacity = operation.Kind == TextStudio2DLayerKind.Group
                ? Mathf.Clamp01(operation.Opacity)
                : 1f;
            Composite(
                backdrop,
                source,
                destination,
                operation.BlendMode,
                opacity,
                sourceIsBackdropFilter,
                in mask,
                composite);
            return destination;
        }

        RenderTexture ApplyColorAdjustment(
            in TextStudio2DCompiledOperation operation,
            RenderTexture backdrop,
            RenderTexture selectorMask,
            RenderTexture coverage,
            RenderTexture target)
        {
            if (_effectMaterial == null || _effectScratchTexture0 == null)
            {
                _compositionFailed = true;
                return backdrop;
            }

            TextStudio2DColorAdjustmentSpec filter = operation.ColorAdjustment;
            _commands.SetGlobalVector(s_colorAdjustmentRedProperty, filter.Red);
            _commands.SetGlobalVector(s_colorAdjustmentGreenProperty, filter.Green);
            _commands.SetGlobalVector(s_colorAdjustmentBlueProperty, filter.Blue);
            _commands.SetGlobalVector(s_colorAdjustmentAlphaProperty, filter.Alpha);
            _commands.SetGlobalVector(s_colorAdjustmentOffsetProperty, filter.Offset);
            _commands.SetGlobalVector(
                s_colorAdjustmentSettingsProperty,
                new Vector4(filter.Strength, filter.ClampOutput ? 1f : 0f, coverage != null ? 1f : 0f, 0f));
            _commands.SetGlobalTexture(s_compositeCoverageProperty, coverage != null ? coverage : Texture2D.whiteTexture);
            _commands.SetGlobalTexture(
                s_effectMaskProperty,
                selectorMask != null ? selectorMask : Texture2D.whiteTexture);
            _commands.Blit(
                backdrop,
                target,
                _effectMaterial,
                ColorAdjustmentPass);
            CountEffectPass();
            return target;
        }

        RenderTexture ApplyTextWideSoftEffect(
            in TextStudio2DCompiledOperation operation,
            RenderTexture source)
        {
            if (_effectMaterial == null ||
                _blurScratchTexture0 == null ||
                _blurScratchTexture1 == null ||
                _blurCoverageTexture == null ||
                _effectScratchTexture0 == null ||
                !TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(
                    in operation,
                    out Vector2 offset,
                    out float radius,
                    out float spread))
            {
                _compositionFailed = true;
                return source;
            }

            float normalizedRadius = Mathf.Max(0f, radius + spread);
            if (normalizedRadius <= Mathf.Epsilon)
            {
                ClearTarget(_blurScratchTexture1);
                return _blurScratchTexture1;
            }

            TextStudio2DUnit units =
                operation.Kind == TextStudio2DLayerKind.OuterShadow
                    ? operation.Shadow.Units
                    : operation.Glow.Units;
            float fullResolutionRadius = ResolveSampledBlurRadius(
                normalizedRadius, units, _effectUnitScale, _surfacePixelsPerUnit, _pixelsPerUnit);
            float downsampledRadius = Mathf.Max(
                0.5f,
                fullResolutionRadius / Mathf.Max(1f, _blurScratchScaleX));
            float downsampledRadiusY = Mathf.Max(
                0.5f,
                fullResolutionRadius / Mathf.Max(1f, _blurScratchScaleY));
            _commands.SetGlobalVector(
                s_focusBlurRadiusProperty,
                new Vector4(
                    downsampledRadius,
                    downsampledRadiusY,
                    0f,
                    0f));
            _commands.SetGlobalVector(
                s_focusBlurTexelSizeProperty,
                new Vector4(
                    1f / _blurScratchTexture0.width,
                    1f / _blurScratchTexture0.height,
                    _blurScratchTexture0.width,
                    _blurScratchTexture0.height));
            _commands.SetGlobalVector(
                s_effectParameters0Property,
                new Vector4(
                    Mathf.Max(0f, spread),
                    Mathf.Max(0.0001f, radius),
                    operation.Kind == TextStudio2DLayerKind.OuterShadow ? 1f : 0f,
                    0f));
            Vector2 localOffset = ResolveEffectLocalOffset(offset, units);
            _commands.SetGlobalVector(
                s_effectDirectionProperty,
                new Vector4(
                    localOffset.x /
                    Mathf.Max(MinimumExtent, _renderBounds.width),
                    localOffset.y /
                    Mathf.Max(MinimumExtent, _renderBounds.height),
                    0f,
                    0f));
            _commands.SetGlobalTexture(
                s_effectMaskProperty,
                _effectScratchTexture0);
            _commands.Blit(_effectScratchTexture0, _blurScratchTexture0, _effectMaterial, TextWideBlurHorizontalPass);
            _commands.Blit(_blurScratchTexture0, _blurCoverageTexture, _effectMaterial, TextWideBlurCoverageVerticalPass);
            _commands.SetGlobalTexture(s_textWideCoverageProperty, _blurCoverageTexture);
            _commands.Blit(
                source,
                _blurScratchTexture0,
                _effectMaterial,
                TextWideBlurHorizontalPass);
            _commands.Blit(
                _blurScratchTexture0,
                _blurScratchTexture1,
                _effectMaterial,
                TextWideBlurVerticalPass);
            CountEffectPass(4);
            return _blurScratchTexture1;
        }

        RenderTexture ApplyHeroFauxDepth(
            in TextStudio2DCompiledOperation operation,
            RenderTexture source, Vector4 adjustmentSelection, bool surfacePaint, float paintAlpha)
        {
            _commands.SetGlobalVector(s_generatedAdjustmentSelectionProperty, adjustmentSelection);
            bool samplePaintAlpha = surfacePaint && float.IsNaN(paintAlpha);
            _commands.SetGlobalTexture(s_generatedAlphaProperty, samplePaintAlpha ? _generatedAlphaTexture : Texture2D.whiteTexture);
            _commands.SetGlobalFloat(s_generatedPaintAlphaProperty, samplePaintAlpha ? 1 : paintAlpha);
            if (_effectMaterial == null || _effectScratchTexture0 == null)
            {
                _compositionFailed = true;
                return source;
            }

            TextStudio2DFauxDepthSpec depth = operation.FauxDepth;
            Vector2 localOffset = ResolveEffectLocalOffset(
                depth.Offset,
                depth.Units);
            Vector2 pivot = new Vector2(
                (_sourceBounds.x + _sourceBounds.z) * 0.5f,
                (_sourceBounds.y + _sourceBounds.w) * 0.5f);
            _commands.SetGlobalVector(
                s_effectDirectionProperty,
                new Vector4(
                    localOffset.x /
                    Mathf.Max(MinimumExtent, _renderBounds.width),
                    localOffset.y /
                    Mathf.Max(MinimumExtent, _renderBounds.height),
                    (pivot.x - _renderBounds.xMin) /
                    Mathf.Max(MinimumExtent, _renderBounds.width),
                    (pivot.y - _renderBounds.yMin) /
                    Mathf.Max(MinimumExtent, _renderBounds.height)));
            _commands.SetGlobalVector(
                s_effectParameters0Property,
                new Vector4(
                    depth.ResolveTaper(localOffset.magnitude / Mathf.Max(MinimumExtent, _effectUnitScale)),
                    Mathf.Clamp01(depth.EndFade),
                    (float)operation.QualityTier,
                    samplePaintAlpha ? 2f : surfacePaint ? 1f : 0f));
            _commands.Blit(
                source,
                _effectScratchTexture0,
                _effectMaterial,
                FauxDepthPass);
            CountEffectPass();
            return _effectScratchTexture0;
        }

        Vector2 ResolveEffectLocalOffset(
            Vector2 offset,
            TextStudio2DUnit units)
        {
            return units == TextStudio2DUnit.Pixels
                ? offset / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit)
                : offset * _effectUnitScale;
        }

        /// <param name="backdropSample">What full-resolution passes read as the backdrop: the backdrop, or black for an uncleared one.</param>
        /// <param name="target">Where full-resolution passes write: the effect scratch, or the composite destination they replace.</param>
        RenderTexture ApplyAnimatedPostProcess(
            int operationIndex,
            in TextStudio2DCompiledOperation operation,
            RenderTexture source,
            RenderTexture backdrop,
            Texture backdropSample,
            RenderTexture target)
        {
            TextStudio2DAnimatedEffectSpec effect = operation.AnimatedEffect;
            if (_effectMaterial == null ||
                TextStudio2DAnimatedEffectUtility.RequiresSpatialScratch(effect.Kind) &&
                _effectScratchTexture0 == null)
            {
                _compositionFailed = true;
                return source;
            }

            ConfigureAnimatedEffect(in effect, backdropSample, source);
            switch (effect.Kind)
            {
                case TextStudio2DAnimatedEffectKind.GlitchDisplacement:
                case TextStudio2DAnimatedEffectKind.HeatHaze:
                case TextStudio2DAnimatedEffectKind.LiquidWarp:
                    _commands.Blit(
                        backdropSample,
                        target,
                        _effectMaterial,
                        0);
                    CountEffectPass();
                    return target;
                case TextStudio2DAnimatedEffectKind.EchoTrails:
                    BindHistoryTextures();
                    _commands.Blit(
                        backdropSample,
                        target,
                        _effectMaterial,
                        1);
                    CountEffectPass();
                    if (_coverageReplayDepth == 0 && !_geometryCoveragePass)
                    {
                        _occurrenceState.PendingHistoryOperation = operationIndex;
                        // The trail pass has read the ring, and the end-of-frame capture would redraw this same layer: copy it now.
                        if (PassElisionEnabled && source == _layerTexture && _glyphFilter < 0 && _scopeDepth == 0 &&
                            _occurrenceState.HistoryCoverageCount == 0 && !_occurrenceState.HistoryLayerCaptured)
                            _occurrenceState.HistoryLayerCaptured = CaptureHistoryLayer();
                    }
                    return target;
                case TextStudio2DAnimatedEffectKind.MotionSmear:
                    _commands.SetGlobalInt(s_historyCountProperty, 0);
                    _commands.Blit(
                        backdropSample,
                        target,
                        _effectMaterial,
                        1);
                    CountEffectPass();
                    return target;
                case TextStudio2DAnimatedEffectKind.ProgressiveBlurFocus:
                    if (_blurScratchTexture0 == null ||
                        _blurScratchTexture1 == null)
                    {
                        _compositionFailed = true;
                        return source;
                    }
                    float fullResolutionRadius =
                        ResolveProgressiveBlurRadius(in effect);
                    _commands.SetGlobalVector(
                        s_focusBlurRadiusProperty,
                        new Vector4(
                            fullResolutionRadius /
                            Mathf.Max(1f, _blurScratchScaleX),
                            fullResolutionRadius /
                            Mathf.Max(1f, _blurScratchScaleY),
                            0f,
                            0f));
                    _commands.SetGlobalVector(
                        s_focusBlurTexelSizeProperty,
                        new Vector4(
                            1f / _blurScratchTexture0.width,
                            1f / _blurScratchTexture0.height,
                            _blurScratchTexture0.width,
                            _blurScratchTexture0.height));
                    _commands.Blit(
                        backdrop,
                        _blurScratchTexture0,
                        _effectMaterial,
                        2);
                    _commands.Blit(
                        _blurScratchTexture0,
                        _blurScratchTexture1,
                        _effectMaterial,
                        3);
                    CountEffectPass(2);
                    return _blurScratchTexture1;
                default:
                    return source;
            }
        }

        void ConfigureAnimatedEffect(
            in TextStudio2DAnimatedEffectSpec effect,
            Texture backdrop,
            RenderTexture effectMask)
        {
            _commands.SetGlobalInt(s_effectKindProperty, (int)effect.Kind);
            _commands.SetGlobalVector(
                s_effectParameters0Property,
                new Vector4(
                    effect.Amount,
                    effect.Speed,
                    effect.Phase,
                    effect.Scale));
            _commands.SetGlobalVector(
                s_effectParameters1Property,
                new Vector4(
                    effect.Width,
                    effect.Softness,
                    effect.Amplitude,
                    effect.Frequency));
            _commands.SetGlobalVector(
                s_effectDirectionProperty,
                new Vector4(effect.Direction.x, effect.Direction.y, 0f, 0f));
            _commands.SetGlobalVector(
                s_effectCoordinateTransformProperty,
                ResolveEffectCoordinateTransform(effect.Mapping));
            _commands.SetGlobalColor(
                s_effectPrimaryProperty,
                ConvertEffectColor(effect.PrimaryColor));
            _commands.SetGlobalColor(
                s_effectSecondaryProperty,
                ConvertEffectColor(effect.SecondaryColor));
            _commands.SetGlobalInt(
                s_effectReducedBehaviorProperty,
                (int)effect.ReducedMotionBehavior);
            _commands.SetGlobalTexture(
                s_effectBackdropProperty,
                backdrop != null ? backdrop : Texture2D.blackTexture);
            _commands.SetGlobalTexture(
                s_effectMaskProperty,
                effectMask != null ? effectMask : Texture2D.blackTexture);
        }

        void BindHistoryTextures()
        {
            int count = Mathf.Min(_occurrenceState.HistoryValidCount, _occurrenceState.HistorySurfaceCount);
            _commands.SetGlobalInt(s_historyCountProperty, count);
            for (int age = 0; age < MaximumHistoryFrames; age++)
            {
                RenderTexture texture = null;
                if (age < count && _occurrenceState.HistorySurfaceCount > 0)
                {
                    int index = (_occurrenceState.HistoryCursor - 1 - age +
                                 _occurrenceState.HistorySurfaceCount * 2) %
                                _occurrenceState.HistorySurfaceCount;
                    texture = _geometryCoveragePass ? _occurrenceState.HistoryCoverageTextures[index] : _occurrenceState.HistoryTextures[index];
                }
                _commands.SetGlobalTexture(
                    s_historyTextureProperties[age],
                    texture != null ? texture : Texture2D.blackTexture);
            }
        }

        // Copies the current echo layer into the oldest history slot; only after every pass that reads the ring this frame.
        bool CaptureHistoryLayer()
        {
            RenderTexture destination = _occurrenceState.HistorySurfaceCount > 0
                ? _occurrenceState.HistoryTextures[_occurrenceState.HistoryCursor]
                : null;
            if (destination == null)
                return false;
            _commands.Blit(_layerTexture, destination);
            CountEffectPass();
            return true;
        }

        void CaptureHistory()
        {
            if (_occurrenceState.HistorySurfaceCount <= 0 || _occurrenceState.PendingHistoryOperation < 0)
                return;
            RenderTexture destination = _occurrenceState.HistoryTextures[_occurrenceState.HistoryCursor];
            if (destination == null)
                return;
            if (_occurrenceState.HistoryLayerCaptured)
            {
                _occurrenceState.HistoryCapturePending = true;
                return;
            }
            // All color and coverage replays read the previous frames before the oldest slot is replaced.
            int previousGlyph = _glyphFilter;
            SetGlyphFilter(-1);
            RenderLayer(_occurrenceState.PendingHistoryOperation, _occurrenceState.PendingHistoryOperation, HeroLayerSourceMode.Operation);
            _commands.Blit(_layerTexture, destination);
            CountEffectPass();
            if (_occurrenceState.HistoryCoverageCount > 0)
            {
                SetGeometryCoveragePass(true);
                RenderLayer(_occurrenceState.PendingHistoryOperation, _occurrenceState.PendingHistoryOperation, HeroLayerSourceMode.Operation);
                _commands.Blit(_layerTexture, _occurrenceState.HistoryCoverageTextures[_occurrenceState.HistoryCursor]);
                CountEffectPass();
                SetGeometryCoveragePass(false);
            }
            SetGlyphFilter(previousGlyph);
            _occurrenceState.HistoryCapturePending = true;
        }

        float ResolveProgressiveBlurRadius(
            in TextStudio2DAnimatedEffectSpec effect)
        {
            TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(
                _hub.MotionPolicy);
            float rate = TextStudioVisualPrimitiveMath.ResolveMotionRate(
                policy,
                effect.ReducedMotionBehavior,
                out _);
            float phase = effect.Phase + _currentRenderTime * effect.Speed * rate;
            float focus = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            float normalizedRadius = effect.Softness + effect.Amplitude * focus;
            if (policy == TextStudioMotionPolicy.Reduced &&
                effect.ReducedMotionBehavior == TextStudioReducedMotionBehavior.Reduce)
            {
                normalizedRadius *= 0.35f;
            }
            return ResolveSampledBlurRadius(
                normalizedRadius, TextStudio2DUnit.Em, _effectUnitScale, _surfacePixelsPerUnit, _pixelsPerUnit);
        }

        internal static float ResolveSampledBlurRadius(
            float radius, TextStudio2DUnit units, float localEmSize, float surfacePixelsPerUnit, float renderPixelsPerUnit)
        {
            // Bounds and target fitting already limit raster cost. A pixel cap here changes authored em sizes on zoom.
            float localRadius = units == TextStudio2DUnit.Pixels
                ? radius / Mathf.Max(MinimumExtent, surfacePixelsPerUnit)
                : radius * localEmSize;
            return Mathf.Max(0f, localRadius) * Mathf.Max(0f, renderPixelsPerUnit);
        }

        Rect ExpandForHeroPostProcessing(
            in Rect geometryBounds,
            TextStudio2DHeroProgram program)
        {
            Vector4 depthExpansion = Vector4.zero;
            Vector2 depthPivot = new Vector2(
                (_sourceBounds.x + _sourceBounds.z) * 0.5f,
                (_sourceBounds.y + _sourceBounds.w) * 0.5f);
            Vector4 taperRadii = Vector4.Max(Vector4.zero, new Vector4(
                depthPivot.x - geometryBounds.xMin,
                depthPivot.y - geometryBounds.yMin,
                geometryBounds.xMax - depthPivot.x,
                geometryBounds.yMax - depthPivot.y));
            Vector2 textSize = new Vector2(
                Mathf.Max(MinimumExtent, _sourceBounds.z - _sourceBounds.x),
                Mathf.Max(MinimumExtent, _sourceBounds.w - _sourceBounds.y));
            float left = 0f;
            float bottom = 0f;
            float right = 0f;
            float top = 0f;
            for (int index = 0; index < program.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation =
                    ref program.Style.OperationAt(index);
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
                {
                    TextStudio2DFauxDepthSpec depth = operation.FauxDepth;
                    if (!depth.HasDepth)
                        continue;
                    Vector2 depthOffset = ResolveEffectLocalOffset(depth.Offset, depth.Units);
                    float taperGrowth = Mathf.Max(0f, -depth.ResolveTaper(depthOffset.magnitude / Mathf.Max(MinimumExtent, _effectUnitScale)));
                    Vector4 taperExpansion = taperRadii * taperGrowth;
                    Vector4 offsetExpansion = new Vector4(
                        Mathf.Max(0f, -depthOffset.x),
                        Mathf.Max(0f, -depthOffset.y),
                        Mathf.Max(0f, depthOffset.x),
                        Mathf.Max(0f, depthOffset.y));
                    depthExpansion = Vector4.Max(
                        depthExpansion,
                        offsetExpansion + taperExpansion);
                    continue;
                }
                if (TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(
                        in operation,
                        out Vector2 offset,
                        out float radius,
                        out float spread))
                {
                    TextStudio2DUnit units = operation.Kind == TextStudio2DLayerKind.OuterShadow ? operation.Shadow.Units : operation.Glow.Units;
                    float scale = units == TextStudio2DUnit.Pixels ? 1f / Mathf.Max(MinimumExtent, _surfacePixelsPerUnit) : _effectUnitScale;
                    float blur = Mathf.Max(0f, radius + spread) * scale;
                    Vector2 localOffset = offset * scale;
                    left += blur + Mathf.Max(0f, -localOffset.x);
                    bottom += blur + Mathf.Max(0f, -localOffset.y);
                    right += blur + Mathf.Max(0f, localOffset.x);
                    top += blur + Mathf.Max(0f, localOffset.y);
                    continue;
                }
                if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect)
                    continue;
                TextStudio2DAnimatedEffectSpec effect = operation.AnimatedEffect;
                TextStudio2DBoundsExpansion expansion =
                    TextStudioVisualPrimitiveMath.CalculateHeroPostProcessBounds(
                        in effect,
                        textSize,
                        _effectUnitScale);
                left += expansion.Left;
                bottom += expansion.Bottom;
                right += expansion.Right;
                top += expansion.Top;
            }
            return Rect.MinMaxRect(
                geometryBounds.xMin - Mathf.Max(left, depthExpansion.x),
                geometryBounds.yMin - Mathf.Max(bottom, depthExpansion.y),
                geometryBounds.xMax + Mathf.Max(right, depthExpansion.z),
                geometryBounds.yMax + Mathf.Max(top, depthExpansion.w));
        }

        static Rect ExpandForLensMagnifier(
            in Rect geometryBounds,
            in TextStudioLensMagnifierDescriptor lens)
        {
            float radius = Mathf.Max(MinimumExtent, lens.Radius);
            return Rect.MinMaxRect(
                Mathf.Min(geometryBounds.xMin, lens.TargetLocal.x - radius),
                Mathf.Min(geometryBounds.yMin, lens.TargetLocal.y - radius),
                Mathf.Max(geometryBounds.xMax, lens.TargetLocal.x + radius),
                Mathf.Max(geometryBounds.yMax, lens.TargetLocal.y + radius));
        }

        void ApplyLensMagnifier(
            in TextStudioLensMagnifierDescriptor lens)
        {
            if (_effectMaterial == null ||
                _effectScratchTexture0 == null ||
                _finalTexture == null ||
                _renderBounds.width <= MinimumExtent ||
                _renderBounds.height <= MinimumExtent)
            {
                _compositionFailed = true;
                return;
            }

            Vector2 centerUv = ResolveLensTextureCenter(
                lens.TargetLocal,
                in _renderBounds,
                SystemInfo.graphicsUVStartsAtTop);
            _commands.SetGlobalVector(
                s_lensCenterRadiusProperty,
                new Vector4(
                    centerUv.x,
                    centerUv.y,
                    lens.Radius / _renderBounds.width,
                    lens.Radius / _renderBounds.height));
            _commands.SetGlobalVector(
                s_lensProfileProperty,
                new Vector4(
                    lens.CenterMagnification,
                    lens.EdgeMagnification,
                    lens.Bulge,
                    lens.Feather));
            _commands.Blit(
                _finalTexture,
                _effectScratchTexture0,
                _effectMaterial,
                4);
            _commands.Blit(_effectScratchTexture0, _finalTexture);
            CountEffectPass(2);
        }

        internal static Vector2 ResolveLensTextureCenter(
            Vector2 targetLocal,
            in Rect renderBounds,
            bool graphicsUvStartsAtTop)
        {
            float x = (targetLocal.x - renderBounds.xMin) /
                      Mathf.Max(MinimumExtent, renderBounds.width);
            float y = (targetLocal.y - renderBounds.yMin) /
                      Mathf.Max(MinimumExtent, renderBounds.height);
            return new Vector2(x, graphicsUvStartsAtTop ? 1f - y : y);
        }

        Vector4 ResolveEffectCoordinateTransform(TextStudio2DMappingSpace mapping)
        {
            Vector2 basis = mapping == TextStudio2DMappingSpace.Text
                ? new Vector2(
                    Mathf.Max(MinimumExtent, _sourceBounds.z - _sourceBounds.x),
                    Mathf.Max(MinimumExtent, _sourceBounds.w - _sourceBounds.y))
                : Vector2.one * Mathf.Max(MinimumExtent, _effectUnitScale);
            return new Vector4(
                _renderBounds.width / basis.x,
                _renderBounds.height / basis.y,
                (_renderBounds.xMin - _sourceBounds.x) / basis.x,
                (_renderBounds.yMin - _sourceBounds.y) / basis.y);
        }

        void CountEffectPass(int count = 1)
        {
            _lastEffectPassCount += count;
            _lastCompositePassCount += count;
        }

        MaskState PrepareMask(
            TextStudio2DHeroProgram program,
            int maskOperationIndex,
            int nestedDepth,
            Material composite)
        {
            if (maskOperationIndex < 0)
                return default;
            if (maskOperationIndex >= program.OperationCount ||
                _scopeDepth >= MaximumScopeDepth ||
                !TryGetNestedPair(nestedDepth, out CompositionPair maskPair))
            {
                _compositionFailed = true;
                return default;
            }
            ref readonly TextStudio2DCompiledOperation maskOperation =
                ref program.Style.OperationAt(maskOperationIndex);
            if (maskOperation.Kind != TextStudio2DLayerKind.Mask)
            {
                _compositionFailed = true;
                return default;
            }
            int sourceIndex = maskOperation.MaskOperationIndex;
            if (sourceIndex < 0 || sourceIndex >= program.OperationCount)
            {
                _compositionFailed = true;
                return default;
            }

            _scopeSelectors[_scopeDepth++] = maskOperationIndex;
            ClearTarget(maskPair.First);
            bool previousCoverage = _geometryCoveragePass;
            SetGeometryCoveragePass(false);
            RenderTexture result;
            try { result = ComposeNode(program, sourceIndex, maskPair.First, maskPair.Second, nestedDepth + 1, composite); }
            finally { SetGeometryCoveragePass(previousCoverage); _scopeSelectors[--_scopeDepth] = -1; }

            float blurRadius = Mathf.Max(0f, maskOperation.Mask.Feather) *
                               _effectUnitScale * _pixelsPerUnit;
            if (blurRadius > 0.01f && result != null)
            {
                RenderTexture temporary = result == maskPair.First
                    ? maskPair.Second
                    : maskPair.First;
                _commands.SetGlobalFloat(
                    s_blurRadiusProperty,
                    Mathf.Min(64f, blurRadius));
                _commands.Blit(result, temporary, composite, 1);
                _commands.Blit(temporary, result, composite, 2);
                _lastCompositePassCount += 2;
            }
            return new MaskState(
                result,
                maskOperation.Mask.Invert,
                Mathf.Clamp01(maskOperation.Opacity));
        }

        enum HeroLayerSourceMode
        {
            Operation,
            PaintedFace,
            SelectionMask,
            GeneratedColor,
            GeneratedAlpha,
            NeighborhoodCoverage
        }

        void RenderLayer(
            int operationIndex,
            int selectorIndex,
            HeroLayerSourceMode sourceMode = HeroLayerSourceMode.Operation, int firstSource = 0, int endSource = int.MaxValue, RenderTexture target = null,
            bool clear = true)
        {
            _commands.SetGlobalVector(s_sourceRangeProperty, new Vector4(firstSource, endSource, 0, 0));
            bool unionCoverage = sourceMode == HeroLayerSourceMode.Operation && UsesUnionCoverage(operationIndex);
            bool surface = sourceMode == HeroLayerSourceMode.Operation && _surfaceStyle != null &&
                           (_surfaceStyle.OperationAt(operationIndex).Kind == TextStudio2DLayerKind.Decoration ||
                            TextStudio2DStyleDecorations.UsesContourStroke(_surfaceStyle.OperationAt(operationIndex)));
            int rowClass = surface ? 0 : RowMaskClass(operationIndex);
            RenderTexture destination = clear && target == null ? _layerTexture : target;
            if (_staticRunSplit && !clear && target != null) BindSplitTarget(target);
            else if (rowClass > 0 && destination != null) BindRowMask(destination, clear, rowClass);
            else if (clear) ClearTarget(destination, unionCoverage);
            else BindTarget(target);
            _commands.SetGlobalInt(s_operationIndexProperty, operationIndex);
            _commands.SetGlobalInt(s_selectorIndexProperty, selectorIndex);
            _commands.SetGlobalInt(s_styleSlotProperty, _styleSlot);
            _commands.SetGlobalInt(s_sourceModeProperty, (int)sourceMode);
            _commands.SetGlobalVector(s_sourceBoundsProperty, _sourceBounds);
            _commands.SetGlobalVector(
                s_scopeSelectors0Property,
                ScopeVector(0));
            _commands.SetGlobalVector(
                s_scopeSelectors1Property,
                ScopeVector(4));
            if (surface)
            {
                _lastLayerDrawCount += _occurrenceState.SurfaceGeometry.DrawHero(_commands, operationIndex, unionCoverage);
                return;
            }
            // These meshes use raw TMP colors and bypass CanvasRenderer's vertex-color conversion.
            _layerProperties.SetInt(s_uiVertexColorGammaProperty, ViewIsCanvas ? 1 : 0);
            // The plain draw, whose per-draw values the plain family row variants compile as constants.
            bool plainDraw = sourceMode == HeroLayerSourceMode.Operation && firstSource == 0 && endSource == int.MaxValue &&
                             _glyphFilter < 0 && !_geometryCoveragePass;
            // Masked rows still draw the full-size quads of class 0; only the stencil written by BindRowMask limits them. The rows
            // of a split static run test the stencil BACKDROP_MASK wrote in the same way.
            bool stencilTested = rowClass > 0 || _staticRunSplit;
            if (stencilTested) _commands.SetGlobalInt(s_rowMaskProperty, (int)CompareFunction.Equal);
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                if (batch.Material == null || batch.Mesh == null)
                    continue;
                // Command buffers copy the block per draw, so each engine bank binds exactly for its own batches.
                _viewExternal?.ApplyLayerProperties(_layerProperties, batch.Source);
                ref readonly TextStudio2DCompiledOperation drawingOperation = ref _surfaceStyle.OperationAt(operationIndex);
                if (batch.Lease.ColorMode > 0 && batch.Lease.ColorMode < 3 &&
                    (sourceMode != HeroLayerSourceMode.Operation || drawingOperation.Kind != TextStudio2DLayerKind.Fill ||
                     drawingOperation.Fill.ColorFace == TextStudio2DColorFaceMode.SilhouetteFill)) continue;
                if (batch.Lease.ColorMode == 3 && sourceMode == HeroLayerSourceMode.Operation &&
                    drawingOperation.Kind == TextStudio2DLayerKind.Fill && drawingOperation.Fill.ColorFace != TextStudio2DColorFaceMode.SilhouetteFill) continue;
                // PrepareUniformRows resolved the uniform row materials of this render; others keep the table program.
                Material rowMaterial = _appearancePlan == null ? batch.UniformRowMaterial(operationIndex) : null;
                if (rowMaterial != null)
                    _lastUniformRowDrawCount++;
                else
                {
                    TextStudio2DShaderTier familyTier = TextStudio2DShaderTiers.IsolatedTierFor(drawingOperation.Kind);
                    rowMaterial = TextStudio2DHeroMaterials.ResolveRowMaterial(
                        batch.Lease,
                        familyTier,
                        TextStudio2DShaderTiers.UsesOnlySolidPaint(_surfaceStyle),
                        _heroMaterialEffects,
                        plainDraw);
                    if (TextStudio2DHeroMaterials.UsesPlainRow(batch.Lease, familyTier, plainDraw))
                        _lastPlainRowDrawCount++;
                }
                if (rowClass > 0)
                    _lastRowMaskDrawCount++;
                if (_staticRunSplit)
                    _lastSplitRowDrawCount++;
                _commands.DrawMesh(
                    batch.Mesh,
                    Matrix4x4.identity,
                    rowMaterial,
                    0,
                    0,
                    _layerProperties);
                _lastLayerDrawCount++;
            }
            if (stencilTested) _commands.SetGlobalInt(s_rowMaskProperty, (int)CompareFunction.Disabled);
        }

        // Resolves every batch's uniform row materials before the render records commands. Acquiring and releasing them can
        // create and destroy materials, and destroying one flushes the pending submission, which must never split a recording.
        // A row draws uniformly when TextStudio2DHeroMaterials.SupportsUniformRow admits it, the render composes no appearance
        // occurrence, whose glyph rows carry appearance bounds, and the batch is no colour source, whose native face reads its
        // row from the table. Every other draw keeps the table program.
        void PrepareUniformRows()
        {
            bool enabled = TextStudio2DHeroMaterials.UniformRowsEnabled && _appearancePlan == null;
            if (enabled && !ReferenceEquals(_uniformRowStyle, _surfaceStyle))
            {
                // Records are packed once per compiled style.
                _uniformRowStyle = _surfaceStyle;
                int count = _surfaceStyle != null ? _surfaceStyle.OperationCount : 0;
                if (_uniformRowEligible.Length < count)
                {
                    _uniformRowEligible = new bool[count];
                    _uniformRows = new TextStudio2DHeroUniformRow[count];
                }
                for (int index = 0; index < _uniformRowEligible.Length; index++)
                {
                    bool eligible = index < count && TextStudio2DHeroMaterials.SupportsUniformRow(_surfaceStyle, index);
                    _uniformRowEligible[index] = eligible;
                    _uniformRows[index] = eligible ? new TextStudio2DHeroUniformRow(_surfaceStyle, index) : default;
                }
            }
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                LayerBatch batch = _occurrenceState.Batches[index];
                bool drawsUniformRows = enabled && batch.Lease.ColorMode != 1 && batch.Lease.ColorMode != 2;
                batch.PrepareUniformRows(drawsUniformRows ? _uniformRowEligible : null, _uniformRows, _heroMaterialEffects);
            }
        }

        void Composite(
            RenderTexture backdrop,
            RenderTexture source,
            RenderTexture destination,
            TextStudio2DBlendMode blendMode,
            float opacity,
            bool sourceIsBackdropFilter,
            in MaskState mask,
            Material material,
            Color? invocationTint = null)
        {
            _commands.SetGlobalColor(s_invocationTintProperty, invocationTint ?? Color.white);
            _commands.SetGlobalTexture(s_sourceTextureProperty, source);
            _commands.SetGlobalTexture(
                s_maskTextureProperty,
                mask.Texture != null ? mask.Texture : Texture2D.whiteTexture);
            _commands.SetGlobalInt(s_blendModeProperty, (int)blendMode);
            _commands.SetGlobalFloat(s_opacityProperty, opacity);
            _commands.SetGlobalFloat(
                s_sourceIsBackdropFilterProperty,
                sourceIsBackdropFilter ? 1f : 0f);
            _commands.SetGlobalFloat(s_useMaskProperty, mask.Texture != null ? 1f : 0f);
            _commands.SetGlobalFloat(s_invertMaskProperty, mask.Invert ? 1f : 0f);
            _commands.SetGlobalFloat(s_maskStrengthProperty, mask.Strength);
            _commands.Blit(backdrop, destination, material, 0);
            _lastCompositePassCount++;
        }

        bool UsesUnionCoverage(int operationIndex) => _surfaceStyle != null &&
            _surfaceStyle.OperationAt(operationIndex).Kind == TextStudio2DLayerKind.Decoration &&
            _surfaceStyle.OperationAt(operationIndex).Decoration.Topology == TextStudio2DDecorationTopology.ConnectedRange;

        void BindTarget(RenderTexture target)
        {
            if (target == null)
            {
                _compositionFailed = true;
                return;
            }
            _commands.SetRenderTarget(target);
            _commands.SetViewport(new Rect(0f, 0f, target.width, target.height));
        }

        void ClearTarget(RenderTexture target, bool clearDepth = false)
        {
            if (target == null)
            {
                _compositionFailed = true;
                return;
            }
            _commands.SetRenderTarget(target);
            _commands.SetViewport(new Rect(0f, 0f, target.width, target.height));
            _commands.ClearRenderTarget(clearDepth, true, Color.clear);
        }

        bool TryGetNestedPair(int depth, out CompositionPair pair)
        {
            if (depth < 0 || depth >= _nestedPairCount)
            {
                pair = default;
                return false;
            }
            int index = 2 + depth * 2;
            pair = new CompositionPair(
                _scratchTextures[index],
                _scratchTextures[index + 1]);
            return pair.IsValid;
        }

        Vector4 ScopeVector(int start)
        {
            return new Vector4(
                ScopeAt(start),
                ScopeAt(start + 1),
                ScopeAt(start + 2),
                ScopeAt(start + 3));
        }

        int ScopeAt(int index)
        {
            return index >= 0 && index < _scopeDepth
                ? _scopeSelectors[index]
                : -1;
        }

        void SynchronizeOutput(TMP_Text tmpText)
        {
            TextStudio2DExternalGlyphSource external = tmpText == null ? _viewExternal : null;
            if (tmpText is TextMeshProUGUI uiText)
            {
                DestroyWorldOutput();
                EnsureUiOutput(uiText.transform, uiText.rectTransform.pivot, TextStudio2DHeroMaterials.GetOutputCanvas());
            }
            else if (external != null && external.IsCanvas && external.Host != null)
            {
                // Engine Canvas output sits under the engine's render root, which applies its window offset and masks.
                DestroyWorldOutput();
                EnsureUiOutput(external.Host, external.Host is RectTransform root ? root.pivot : new Vector2(.5f, .5f),
                    TextStudio2DHeroMaterials.GetOutputCanvas());
                if (external.Suppressed && _uiOutputGraphic != null) _uiOutputGraphic.enabled = false;
            }
            else
            {
                DestroyUiOutput();
                EnsureWorldOutput(tmpText, TextStudio2DHeroMaterials.GetOutput(), external);
            }
        }

        void EnsureWorldOutput(TMP_Text tmpText, Material outputMaterial, TextStudio2DExternalGlyphSource external = null)
        {
            if (_worldOutput == null)
            {
                _worldOutput = new GameObject(WorldOutputName)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = _hub.gameObject.layer
                };
                _hub.ParentRendererOutput(_worldOutput.transform, _hub.transform);
                MeshFilter filter = _worldOutput.AddComponent<MeshFilter>();
                _worldOutputRenderer = _worldOutput.AddComponent<MeshRenderer>();
                _worldOutputMesh = new Mesh
                {
                    name = "Text Studio Hero Output Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                filter.sharedMesh = _worldOutputMesh;
                _worldOutputRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _worldOutputRenderer.receiveShadows = false;
                _worldOutputRenderer.lightProbeUsage = LightProbeUsage.Off;
                _worldOutputRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _worldOutputRenderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
                _worldOutputRenderer.SetDynamicOcclusionCulling(false);
                _hasWorldOutputBounds = false;
                _worldOutputMaterial = null;
            }
            if (_worldOutput.layer != _hub.gameObject.layer)
                _worldOutput.layer = _hub.gameObject.layer;
            ShadowCastingMode shadowCastingMode = _worldShadowMode ==
                                                  TextStudio2DWorldShadowMode.AlphaClipped
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            if (_worldOutputRenderer.shadowCastingMode != shadowCastingMode)
                _worldOutputRenderer.shadowCastingMode = shadowCastingMode;
            Vector4 slices = OutputSlices();
            if (!_hasWorldOutputBounds || !_worldOutputBounds.Equals(_renderBounds) || _worldOutputSlices != slices)
            {
                UploadOutputMesh(_worldOutputMesh, _renderBounds, slices);
                _worldOutputBounds = _renderBounds;
                _worldOutputSlices = slices;
                _hasWorldOutputBounds = true;
                _outputGeometryUploadCount++;
            }
            if (external != null)
            {
                // Engine-owned output sorts where the engine's own standalone renderer would.
                if (_worldOutputRenderer.sortingLayerID != external.SortingLayerId)
                    _worldOutputRenderer.sortingLayerID = external.SortingLayerId;
                if (_worldOutputRenderer.sortingOrder != external.SortingOrder)
                    _worldOutputRenderer.sortingOrder = external.SortingOrder;
            }
            MeshRenderer source = tmpText != null ? tmpText.GetComponent<MeshRenderer>() : null;
            if (source != null)
            {
                TextStudioSurfaceGroup.ResolveWorldSorting(tmpText, source, out int layer, out int order);
                if (_worldOutputRenderer.sortingLayerID != layer)
                    _worldOutputRenderer.sortingLayerID = layer;
                if (_worldOutputRenderer.sortingOrder != order)
                    _worldOutputRenderer.sortingOrder = order;
                if (_worldOutputRenderer.renderingLayerMask != source.renderingLayerMask)
                    _worldOutputRenderer.renderingLayerMask = source.renderingLayerMask;
            }
            // Owners on the same pages share one material; slices travel in the quad UVs, never in a property block.
            Material pageMaterial = TextStudio2DHeroOutputPages.Shared.GetWorldMaterial(outputMaterial, _output, _shadowOutput);
            if (!ReferenceEquals(_worldOutputMaterial, pageMaterial))
            {
                _worldOutputRenderer.sharedMaterial = pageMaterial;
                _worldOutputMaterial = pageMaterial;
            }
            bool visible = _hasRenderedOutput && !_worldSuppressed && external?.Suppressed != true;
            if (_worldOutputRenderer.enabled != visible)
                _worldOutputRenderer.enabled = visible;
        }

        void EnsureUiOutput(Transform parent, Vector2 pivot, Material outputMaterial)
        {
            if (_uiOutput == null)
            {
                Transform existing = parent.Find(UiOutputName);
                if (existing != null)
                {
                    _uiOutput = existing.gameObject;
                    _uiOutputTransform = existing as RectTransform;
                    _uiOutputGraphic =
                        _uiOutput.GetComponent<TextStudio2DHeroOutputGraphic>();
                }
                if (_uiOutput == null)
                {
                    _uiOutput = new GameObject(UiOutputName)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        layer = _hub.gameObject.layer
                    };
                    _uiOutputTransform = _uiOutput.AddComponent<RectTransform>();
                    _hub.ParentRendererOutput(_uiOutputTransform, parent);
                }
                if (_uiOutput.GetComponent<CanvasRenderer>() == null)
                    _uiOutput.AddComponent<CanvasRenderer>();
                LayoutElement layout = _uiOutput.GetComponent<LayoutElement>();
                if (layout == null)
                    layout = _uiOutput.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                if (_uiOutputGraphic == null)
                {
                    _uiOutputGraphic =
                        _uiOutput.AddComponent<TextStudio2DHeroOutputGraphic>();
                }
                _uiOutputGraphic.raycastTarget = false;
            }
            if (_uiOutput.layer != _hub.gameObject.layer)
                _uiOutput.layer = _hub.gameObject.layer;
            if (_uiOutputTransform.anchorMin != Vector2.zero)
                _uiOutputTransform.anchorMin = Vector2.zero;
            if (_uiOutputTransform.anchorMax != Vector2.one)
                _uiOutputTransform.anchorMax = Vector2.one;
            if (_uiOutputTransform.pivot != pivot)
                _uiOutputTransform.pivot = pivot;
            if (_uiOutputTransform.offsetMin != Vector2.zero)
                _uiOutputTransform.offsetMin = Vector2.zero;
            if (_uiOutputTransform.offsetMax != Vector2.zero)
                _uiOutputTransform.offsetMax = Vector2.zero;
            if (_uiOutputTransform.anchoredPosition3D != Vector3.zero)
                _uiOutputTransform.anchoredPosition3D = Vector3.zero;
            if (_uiOutputTransform.localRotation != Quaternion.identity)
                _uiOutputTransform.localRotation = Quaternion.identity;
            if (_uiOutputTransform.localScale != Vector3.one)
                _uiOutputTransform.localScale = Vector3.one;
            Transform outputParent = _uiOutputTransform.parent;
            if (outputParent != null &&
                _uiOutputTransform.GetSiblingIndex() != outputParent.childCount - 1)
            {
                _uiOutputTransform.SetAsLastSibling();
            }
            _uiOutputGraphic.SetCameraProjection(_projectionCallbacks, ViewTransform.localToWorldMatrix);
            _uiOutputGraphic.SetOutput(
                _output.Texture,
                TextStudio2DHeroOutputPages.Shared.GetCanvasMaterial(outputMaterial, _output),
                _renderBounds,
                OutputSlices());
            if (_uiOutputGraphic.enabled != _hasRenderedOutput)
                _uiOutputGraphic.enabled = _hasRenderedOutput;
        }

        bool HasContinuouslyAnimatedMaterial()
        {
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
            {
                Material source = _occurrenceState.Batches[index].Source;
                if (source == null)
                    continue;
                if (HasNonZeroProperty(source, s_faceUvSpeedXProperty) ||
                    HasNonZeroProperty(source, s_faceUvSpeedYProperty) ||
                    HasNonZeroProperty(source, s_outlineUvSpeedXProperty) ||
                    HasNonZeroProperty(source, s_outlineUvSpeedYProperty))
                {
                    return true;
                }
            }
            return false;
        }

        static bool HasNonZeroProperty(Material material, int property)
        {
            return material.HasProperty(property) &&
                   Mathf.Abs(material.GetFloat(property)) > 0.000001f;
        }

        void ReleaseScratchTargets()
        {
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            for (int index = 0; index < _scratchCount; index++)
            {
                pool.Release(ref _scratchLeases[index]);
                _scratchTextures[index] = null;
            }
            _scratchCount = 0;
            _layerTexture = null;
            _rootScratchTexture = null;
            _effectScratchTexture0 = null;
            _blurScratchTexture0 = null;
            _blurScratchTexture1 = null;
            _blurCoverageTexture = null;
            _blurScratchScaleX = 1f;
            _blurScratchScaleY = 1f;
            _nestedPairCount = 0;
            _coveragePairCount = _coverageReplayDepth = 0;
            _generatedAlphaTexture = null;
            ReleaseRowMask();
            pool.Release(ref _finalLease);
            _finalTexture = null;
        }

        /// <summary>The world output quad and its page material for a batch group; the revision changes with its geometry.</summary>
        internal bool TryGetWorldBatch(out Mesh mesh, out Material material, out Transform space, out int revision)
        {
            mesh = _worldOutputMesh;
            material = _worldOutputMaterial;
            space = _worldOutput != null ? _worldOutput.transform : null;
            revision = _outputGeometryUploadCount;
            return HasOutput && mesh != null && material != null && space != null;
        }

        internal void SetWorldOutputSuppressed(bool suppressed)
        {
            _worldSuppressed = suppressed;
            if (_worldOutputRenderer != null) _worldOutputRenderer.enabled = _hasRenderedOutput && !suppressed;
        }

        // A slice changes only when the output bucket changes; the previous slice stays presented until then.
        bool TryAcquireOutput(ref TextStudio2DHeroOutputLease lease, RenderTexture source)
        {
            TextStudio2DHeroOutputLease previous = lease;
            if (source == null ||
                !TextStudio2DHeroOutputPages.Shared.TryAcquire(source.width, source.height, source.graphicsFormat, ref lease))
                return false;
            if (lease.Page != previous.Page || lease.Slice != previous.Slice || lease.Generation != previous.Generation)
                _hasRenderedOutput = false;
            return true;
        }

        void PublishOutput(RenderTexture source, ref TextStudio2DHeroOutputLease lease)
        {
            TextStudio2DHeroOutputPages.RecordPublish(_commands, source, lease);
            _lastPublishPassCount++;
        }

        // Valid-rectangle extent in xy, color slice in z and shadow slice in w; the shader samples the page with them.
        Vector4 OutputSlices()
        {
            Vector2 scale = _output.UvScale;
            return new Vector4(scale.x, scale.y, _output.Slice, _shadowOutput.IsValid ? _shadowOutput.Slice : _output.Slice);
        }

        void ReleaseHistoryTargets()
        {
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            for (int index = 0; index < _occurrenceState.HistorySurfaceCount; index++)
            {
                pool.Release(ref _occurrenceState.HistoryLeases[index]);
                _occurrenceState.HistoryTextures[index] = null;
                pool.Release(ref _occurrenceState.HistoryCoverageLeases[index]);
                _occurrenceState.HistoryCoverageTextures[index] = null;
            }
            _occurrenceState.HistorySurfaceCount = 0;
            _occurrenceState.HistoryCoverageCount = 0;
            ResetHistoryState();
        }

        void SetTargetFailure(TextStudio2DHeroTargetStatus status)
        {
            SetFailure(status switch
            {
                TextStudio2DHeroTargetStatus.TargetCapacityExceeded =>
                    TextStudio2DHeroCompositorStatus.TargetCapacityExceeded,
                TextStudio2DHeroTargetStatus.MemoryBudgetExceeded =>
                    TextStudio2DHeroCompositorStatus.TargetMemoryBudgetExceeded,
                TextStudio2DHeroTargetStatus.UnsupportedFormat =>
                    TextStudio2DHeroCompositorStatus.UnsupportedTarget,
                TextStudio2DHeroTargetStatus.CreationFailed =>
                    TextStudio2DHeroCompositorStatus.TargetCreationFailed,
                _ => TextStudio2DHeroCompositorStatus.UnsupportedTarget
            });
        }

        void SetFailure(TextStudio2DHeroCompositorStatus status)
        {
            Status = status;
            CompatibilityWarning = status switch
            {
                TextStudio2DHeroCompositorStatus.MissingVisualSnapshot =>
                    "The Hero compositor is waiting for renderer-neutral glyph geometry.",
                TextStudio2DHeroCompositorStatus.MissingGlyphGeometry =>
                    "The Hero compositor could not resolve visible TMP glyph geometry.",
                TextStudio2DHeroCompositorStatus.MissingMaterial =>
                    "The Hero compositor could not resolve a compatible SDF material and atlas.",
                TextStudio2DHeroCompositorStatus.MissingShader =>
                    "The packaged Hero compositor shaders are missing.",
                TextStudio2DHeroCompositorStatus.TargetCapacityExceeded =>
                    "The bounded Hero surface pool reached its 48-target capacity.",
                TextStudio2DHeroCompositorStatus.TargetMemoryBudgetExceeded =>
                    "The bounded Hero surface pool reached its 192 MiB memory budget.",
                TextStudio2DHeroCompositorStatus.UnsupportedTarget =>
                    "This graphics device does not support the required Hero render-target format.",
                TextStudio2DHeroCompositorStatus.TargetCreationFailed =>
                    "The graphics device could not create the required Hero render targets.",
                TextStudio2DHeroCompositorStatus.RelationshipDepthExceeded =>
                    "The Hero composition graph exceeded its bounded group and mask depth.",
                TextStudio2DHeroCompositorStatus.RenderFailed =>
                    "The Hero composition command buffer failed. Text Studio preserved TMP rendering.",
                _ => string.Empty
            };
        }

        internal void HideOutput()
        {
            if (_worldOutputRenderer != null)
                _worldOutputRenderer.enabled = false;
            if (_uiOutputGraphic != null)
                _uiOutputGraphic.enabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            // Pending recordings may reference this owner's meshes and targets.
            TextStudio2DHeroSubmission.Flush();
            ReleaseProjectionCallbacks();
            ReleaseAppearanceOccurrences();
            _occurrenceState = _defaultOccurrenceState;
            ReleaseScratchTargets();
            ReleaseHistoryTargets();
            ReleaseStaticRows();
            TextStudio2DHeroTargetPool.Shared.Release(ref _finalLease);
            _finalTexture = null;
            TextStudio2DHeroOutputPages.Shared.Release(ref _output);
            TextStudio2DHeroOutputPages.Shared.Release(ref _shadowOutput);
            for (int index = 0; index < _occurrenceState.Batches.Count; index++)
                _occurrenceState.Batches[index].Dispose();
            _occurrenceState.Batches.Clear();
            ReleaseColorSources();
            DestroyWorldOutput();
            DestroyUiOutput();
            _hasRenderedOutput = false;
            _disposed = true;
            _occurrenceState.SurfaceGeometry.Dispose();
            _surfaceStyle = null;
            _sourceDemand = null;
        }

        void DestroyWorldOutput()
        {
            Destroy(_worldOutputMesh);
            Destroy(_worldOutput);
            _worldOutput = null;
            _worldOutputMesh = null;
            _worldOutputRenderer = null;
            _hasWorldOutputBounds = false;
            _worldOutputMaterial = null;
        }

        void DestroyUiOutput()
        {
            if (_uiOutput != null &&
                _hub != null &&
                !_hub.gameObject.activeInHierarchy)
            {
                _uiOutput = null;
                _uiOutputTransform = null;
                _uiOutputGraphic = null;
                return;
            }
            Destroy(_uiOutput);
            _uiOutput = null;
            _uiOutputTransform = null;
            _uiOutputGraphic = null;
        }

        static void Destroy(Object target)
        {
            if (target == null)
                return;
            TextStudio2DHeroSubmission.Flush();
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        static TextStudio2DQualityTier ResolveQualityTier(
            CompiledTextStudio2DStyle style)
        {
            TextStudio2DQualityTier result = TextStudio2DQualityTier.Responsive;
            for (int index = 0; index < style.OperationCount; index++)
            {
                TextStudio2DQualityTier tier = style.OperationAt(index).QualityTier;
                if ((int)tier > (int)result)
                    result = tier;
            }
            return result;
        }

        static float ResolveAverageEffectUnitScale(in TextStudioFrame frame)
        {
            if (!frame.EffectUnitScales.IsCreated || frame.EffectUnitScales.Length == 0)
                return 1f;
            float total = 0f;
            int end = Mathf.Min(frame.EffectUnitScales.Length, frame.VisualSnapshot.GlyphCount);
            int start = 0;
            if (frame.AppearanceOccurrenceIndex >= 0 && frame.AppearancePlan != null)
            {
                TextStudioAppearanceRange domain = frame.AppearancePlan.Occurrences[frame.AppearanceOccurrenceIndex].Domain;
                start = Mathf.Clamp(domain.Start, 0, end);
                end = Mathf.Clamp(domain.End, start, end);
            }
            for (int index = start; index < end; index++) total += Mathf.Max(MinimumExtent, frame.EffectUnitScales[index]);
            return end > start ? total / (end - start) : 1f;
        }

        static Color ConvertEffectColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear
                ? color.linear
                : color;
        }

        static Vector2 NormalizeSourcePosition(
            Vector2 position,
            Vector4 bounds)
        {
            float width = bounds.z - bounds.x;
            float height = bounds.w - bounds.y;
            return new Vector2(
                Mathf.Abs(width) > MinimumExtent
                    ? (position.x - bounds.x) / width
                    : 0.5f,
                Mathf.Abs(height) > MinimumExtent
                    ? (position.y - bounds.y) / height
                    : 0.5f);
        }

        void UploadOutputMesh(Mesh mesh, Rect bounds, Vector4 slices)
        {
            mesh.Clear();
            _outputVertices[0] = new Vector3(bounds.xMin, bounds.yMin, 0f);
            _outputVertices[1] = new Vector3(bounds.xMin, bounds.yMax, 0f);
            _outputVertices[2] = new Vector3(bounds.xMax, bounds.yMax, 0f);
            _outputVertices[3] = new Vector3(bounds.xMax, bounds.yMin, 0f);
            mesh.vertices = _outputVertices;
            mesh.colors32 = _outputColors;
            // Corners span the valid rectangle of the owner's slice in render-texture orientation.
            float bottom = (SystemInfo.graphicsUVStartsAtTop ? 1f : 0f) * slices.y, top = slices.y - bottom;
            _outputUv[0] = new Vector4(0f, bottom, slices.z, slices.w); _outputUv[1] = new Vector4(0f, top, slices.z, slices.w);
            _outputUv[2] = new Vector4(slices.x, top, slices.z, slices.w); _outputUv[3] = new Vector4(slices.x, bottom, slices.z, slices.w);
            mesh.SetUVs(0, _outputUv);
            mesh.triangles = _outputTriangles;
            mesh.RecalculateBounds();
        }

        static bool HasQuad<T>(T[] values, int vertexIndex)
        {
            return values != null &&
                   vertexIndex >= 0 &&
                   vertexIndex + 3 < values.Length;
        }

        static float Minimum(float a, float b, float c, float d)
        {
            return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
        }

        static float Maximum(float a, float b, float c, float d)
        {
            return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
        }

        readonly struct CompositionPair
        {
            internal readonly RenderTexture First;
            internal readonly RenderTexture Second;
            internal bool IsValid => First != null && Second != null;

            internal CompositionPair(RenderTexture first, RenderTexture second)
            {
                First = first;
                Second = second;
            }
        }

        readonly struct MaskState
        {
            internal readonly RenderTexture Texture;
            internal readonly bool Invert;
            internal readonly float Strength;

            internal MaskState(RenderTexture texture, bool invert, float strength)
            {
                Texture = texture;
                Invert = invert;
                Strength = strength;
            }
        }

        sealed class LayerBatch : IDisposable
        {
            // Material runs can contain a single icon. Grow with emitted geometry and retain capacity on rebuild.
            readonly List<Vector3> _vertices = new List<Vector3>(4);
            readonly List<Vector3> _normals = new List<Vector3>(4);
            readonly List<Color32> _colors = new List<Color32>(4);
            readonly List<Vector4> _uv0 = new List<Vector4>(4);
            readonly List<Vector4> _uv1 = new List<Vector4>(4);
            readonly List<Vector4> _payload0 = new List<Vector4>(4);
            readonly List<Vector4> _payload1 = new List<Vector4>(4);
            readonly List<Vector4> _selectors = new List<Vector4>(4);
            readonly List<Vector4> _localPayload = new List<Vector4>(4);
            // One index list per row mask class (submesh); class 0 holds every glyph at the shared extent.
            List<int>[] _triangles = { new List<int>(6) };
            int _classCount = 1;
            TextStudio2DHeroLayerMaterialLease _lease;
            // Uniform row materials this batch draws, by operation index.
            TextStudio2DHeroUniformRowLease[] _uniformRows = Array.Empty<TextStudio2DHeroUniformRowLease>();

            internal long ManagedPayloadBytes
            {
                get
                {
                    long bytes = TextStudioAppearanceManagedMemory.ListBytes(_vertices) +
                        TextStudioAppearanceManagedMemory.ListBytes(_normals) +
                        TextStudioAppearanceManagedMemory.ListBytes(_colors) +
                        TextStudioAppearanceManagedMemory.ListBytes(_uv0) +
                        TextStudioAppearanceManagedMemory.ListBytes(_uv1) +
                        TextStudioAppearanceManagedMemory.ListBytes(_payload0) +
                        TextStudioAppearanceManagedMemory.ListBytes(_payload1) +
                        TextStudioAppearanceManagedMemory.ListBytes(_selectors) +
                        TextStudioAppearanceManagedMemory.ListBytes(_localPayload) + (long)_triangles.Length * IntPtr.Size;
                    foreach (List<int> triangles in _triangles) bytes += TextStudioAppearanceManagedMemory.ListBytes(triangles);
                    return bytes;
                }
            }

            internal bool Active;
            internal bool IsUsable => _lease.IsValid && Mesh != null;
            internal Material Source => _lease.Source;
            internal Material Material => _lease.Material;
            internal Mesh Mesh { get; }
            internal TextStudio2DHeroLayerMaterialLease Lease => _lease;
            /// <summary>Row mask classes of the uploaded mesh, one submesh each.</summary>
            internal int SubmeshCount { get; private set; } = 1;

            internal LayerBatch(in TextStudio2DHeroLayerMaterialLease lease)
            {
                _lease = lease;
                Mesh = new Mesh
                {
                    name = "Text Studio Hero Layer Geometry",
                    hideFlags = HideFlags.HideAndDontSave
                };
                Mesh.MarkDynamic();
            }

            internal bool Matches(Material source, Texture atlas, float pixelsPerEm, int colorMode, float distanceRange)
            {
                return _lease.IsValid &&
                       ReferenceEquals(_lease.Source, source) &&
                       ReferenceEquals(_lease.Atlas, atlas) && _lease.PixelsPerEm.Equals(pixelsPerEm) &&
                       _lease.ColorMode == colorMode && _lease.DistanceRange.Equals(distanceRange);
            }

            internal void BeginBuild(int classCount)
            {
                Active = false;
                _vertices.Clear();
                _normals.Clear();
                _colors.Clear();
                _uv0.Clear();
                _uv1.Clear();
                _payload0.Clear();
                _payload1.Clear();
                _selectors.Clear();
                _localPayload.Clear();
                if (_triangles.Length < classCount)
                {
                    int previous = _triangles.Length;
                    Array.Resize(ref _triangles, classCount);
                    for (int index = previous; index < classCount; index++) _triangles[index] = new List<int>(6);
                }
                foreach (List<int> triangles in _triangles) triangles.Clear();
                _classCount = Mathf.Max(1, classCount);
            }

            internal bool HasSpatialVertices()
            {
                for (int i = 0; i < _vertices.Count; i++)
                    if (Mathf.Abs(_vertices[i].z) > .00001f) return true;
                return false;
            }

            internal void IncludeProjectedBounds(Matrix4x4 projection, ref Vector2 minimum, ref Vector2 maximum)
            {
                for (int i = 0; i < _vertices.Count; i++)
                {
                    Vector3 point = projection.MultiplyPoint(_vertices[i]);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }

            internal void EmitVertex(
                Vector3 position,
                Vector3 normal,
                Color32 color,
                Vector4 uv0,
                Vector4 uv1,
                Vector4 payload0,
                Vector4 payload1,
                Vector4 selectors,
                Vector4 localPayload)
            {
                _vertices.Add(position);
                _normals.Add(normal);
                _colors.Add(color);
                _uv0.Add(uv0);
                _uv1.Add(uv1);
                _payload0.Add(payload0);
                _payload1.Add(payload1);
                _selectors.Add(selectors);
                _localPayload.Add(localPayload);
            }

            /// <summary>Adds the last emitted quad to every row mask class: every row's mask covers its shared extent.</summary>
            internal void EmitTriangles()
            {
                for (int index = 0; index < _classCount; index++)
                    EmitTriangles(index);
            }

            /// <summary>Adds the last emitted quad to one row mask class, in glyph order.</summary>
            internal void EmitTriangles(int rowClass)
            {
                int start = _vertices.Count - 4;
                List<int> triangles = _triangles[rowClass];
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
                triangles.Add(start);
            }

            internal bool Upload()
            {
                Mesh.Clear();
                if (_vertices.Count == 0)
                    return false;
                Mesh.indexFormat = _vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
                Mesh.SetVertices(_vertices);
                Mesh.SetNormals(_normals);
                Mesh.SetColors(_colors);
                Mesh.SetUVs(0, _uv0);
                Mesh.SetUVs(1, _uv1);
                Mesh.SetUVs(2, _payload0);
                Mesh.SetUVs(3, _payload1);
                Mesh.SetUVs(4, _selectors);
                Mesh.SetTangents(_localPayload);
                Mesh.subMeshCount = _classCount;
                for (int index = 0; index < _classCount; index++)
                    Mesh.SetTriangles(_triangles[index], index, true);
                SubmeshCount = _classCount;
                return true;
            }

            /// <summary>
            /// Holds the uniform row material of every eligible operation and releases the others; a null
            /// <paramref name="eligible"/> releases them all. Operations whose material cannot be resolved keep the table program.
            /// </summary>
            internal void PrepareUniformRows(bool[] eligible, TextStudio2DHeroUniformRow[] rows, bool materialEffects)
            {
                int count = eligible?.Length ?? 0;
                if (_uniformRows.Length < count)
                    Array.Resize(ref _uniformRows, count);
                for (int index = 0; index < _uniformRows.Length; index++)
                {
                    if (index < count && eligible[index])
                        TextStudio2DHeroMaterials.ResolveUniformRow(in _lease, ref _uniformRows[index], in rows[index], materialEffects);
                    else if (_uniformRows[index].IsHeld)
                        TextStudio2DHeroMaterials.ReleaseUniformRow(in _lease, ref _uniformRows[index]);
                }
            }

            /// <summary>The uniform row material prepared for an operation, or null when the row keeps the table program.</summary>
            internal Material UniformRowMaterial(int operationIndex) =>
                (uint)operationIndex < (uint)_uniformRows.Length && _uniformRows[operationIndex].IsHeld ? _uniformRows[operationIndex].Material : null;

            public void Dispose()
            {
                PrepareUniformRows(null, null, false);
                TextStudio2DHeroMaterials.ReleaseLayer(ref _lease);
                Destroy(Mesh);
            }
        }
    }

    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class TextStudio2DHeroOutputGraphic : MaskableGraphic
    {
        Texture _outputTexture;
        Material _outputMaterial;
        Rect _outputBounds;
        Vector4 _outputSlices;
        Material _cameraMaterial;
        bool _cameraProjection;
        Matrix4x4 _outputToWorld;
        static readonly int s_cameraOutput = Shader.PropertyToID("_TextStudioHeroCameraOutput");
        static readonly int s_cameraBounds = Shader.PropertyToID("_TextStudioHeroCameraBounds");
        static readonly int s_cameraSlice = Shader.PropertyToID("_TextStudioHeroCameraSlice");
        static readonly int s_outputToWorld = Shader.PropertyToID("_TextStudioHeroOutputToWorld");

        // Canvas accepts only a 2D main texture; the page array is a property of the shared page material.
        public override Texture mainTexture => Texture2D.whiteTexture;

        /// <summary>The page this graphic presents.</summary>
        internal Texture OutputPage => _outputTexture;

        /// <summary>Valid-rectangle extent in xy and page slice in zw, as written into the quad UVs.</summary>
        internal Vector4 OutputSlices => _outputSlices;

        public override Material material
        {
            get => _outputMaterial != null ? _outputMaterial : base.material;
            set => _outputMaterial = value;
        }

        internal void SetOutput(Texture texture, Material material, Rect bounds, Vector4 slices)
        {
            if (_cameraProjection)
            {
                if (_cameraMaterial == null)
                    _cameraMaterial = new Material(material) { name = "Text Studio Hero Camera Output", hideFlags = HideFlags.HideAndDontSave };
                material = _cameraMaterial;
            }
            bool materialChanged = !ReferenceEquals(_outputMaterial, material) ||
                                   !ReferenceEquals(_outputTexture, texture);
            bool geometryChanged = !_outputBounds.Equals(bounds) || _outputSlices != slices;
            _outputTexture = texture;
            _outputMaterial = material;
            _outputBounds = bounds;
            _outputSlices = slices;
            if (materialChanged)
                SetMaterialDirty();
            if (geometryChanged)
                SetVerticesDirty();
            if (_cameraProjection)
            {
                SetCameraUniforms(_cameraMaterial);
                Material renderedMaterial = materialForRendering;
                if (renderedMaterial != _cameraMaterial) SetCameraUniforms(renderedMaterial);
            }
        }

        internal void SetCameraProjection(bool enabled, Matrix4x4 outputToWorld)
        {
            _cameraProjection = enabled;
            _outputToWorld = outputToWorld;
        }

        void SetCameraUniforms(Material target)
        {
            target.SetFloat(s_cameraOutput, 1f);
            target.SetTexture(TextStudio2DHeroOutputShader.Pages, _outputTexture);
            target.SetVector(s_cameraSlice, _outputSlices);
            target.SetVector(s_cameraBounds, new Vector4(_outputBounds.xMin, _outputBounds.yMin, _outputBounds.width, _outputBounds.height));
            target.SetMatrix(s_outputToWorld, _outputToWorld);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_cameraMaterial != null)
            {
                if (Application.isPlaying) Destroy(_cameraMaterial);
                else DestroyImmediate(_cameraMaterial);
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_outputTexture == null ||
                _outputBounds.width <= 0f ||
                _outputBounds.height <= 0f)
            {
                return;
            }
            // Match the render-texture orientation used by the world output mesh; z selects the page slice.
            float bottom = (SystemInfo.graphicsUVStartsAtTop ? 1f : 0f) * _outputSlices.y;
            float top = _outputSlices.y - bottom;
            float right = _outputSlices.x, slice = _outputSlices.z;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = Color.white;
            vertex.position = new Vector3(_outputBounds.xMin, _outputBounds.yMin, 0f);
            vertex.uv0 = new Vector4(0f, bottom, slice, slice);
            helper.AddVert(vertex);
            vertex.position = new Vector3(_outputBounds.xMin, _outputBounds.yMax, 0f);
            vertex.uv0 = new Vector4(0f, top, slice, slice);
            helper.AddVert(vertex);
            vertex.position = new Vector3(_outputBounds.xMax, _outputBounds.yMax, 0f);
            vertex.uv0 = new Vector4(right, top, slice, slice);
            helper.AddVert(vertex);
            vertex.position = new Vector3(_outputBounds.xMax, _outputBounds.yMin, 0f);
            vertex.uv0 = new Vector4(right, bottom, slice, slice);
            helper.AddVert(vertex);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
