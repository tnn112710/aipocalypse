using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    public enum TextStudio2DRendererCapabilities : ulong
    {
        None = 0,
        SignedDistanceField = 1UL << 0,
        MultiChannelSignedDistanceField = 1UL << 1,
        PerGlyphStyleIndex = 1UL << 2,
        VertexColors = 1UL << 3,
        Gradients = 1UL << 4,
        TextureSampling = 1UL << 5,
        ProceduralNoise = 1UL << 6,
        PerGlyphSelectors = 1UL << 7,
        ScreenMapping = 1UL << 8,
        WorldMapping = 1UL << 9,
        AdvancedBlendModes = 1UL << 10,
        RetainedGeometry = 1UL << 11,
        OffscreenCompositor = 1UL << 12,
        NestedMasks = 1UL << 13,
        StencilMasks = 1UL << 14,
        MaterialResponse = 1UL << 15,
        GraphicEffects = 1UL << 16,
        AnimatedEffects = 1UL << 17,
        ColorAdjustments = 1UL << 18,
        ExactStrokeJoins = 1UL << 19,
        ContourDashes = 1UL << 20,
        AdaptiveSourceFields = 1UL << 21,
        GlyphMajorPainterOrder = 1UL << 22,
        WorldShadowCaster = 1UL << 23,
        All = ulong.MaxValue
    }

    [Flags]
    public enum TextStudio2DStyleRequirements : uint
    {
        None = 0,
        DistanceField = 1 << 0,
        GlyphStyleIndices = 1 << 1,
        SourceVertexColors = 1 << 2,
        GradientTable = 1 << 3,
        TextureTable = 1 << 4,
        ProceduralNoise = 1 << 5,
        SelectorEvaluation = 1 << 6,
        RetainedGeometry = 1 << 7,
        OffscreenSurface = 1 << 8,
        ExpandedBounds = 1 << 9,
        ScreenCoordinates = 1 << 10,
        WorldCoordinates = 1 << 11,
        AdvancedBlend = 1 << 12,
        MaskGraph = 1 << 13,
        MaterialResponse = 1 << 14,
        GraphicEffect = 1 << 15,
        AnimatedEffect = 1 << 16,
        NeighborhoodSampling = 1 << 17,
        TemporalHistory = 1 << 18,
        ColorAdjustment = 1 << 19,
        ExactStrokeJoin = 1 << 20,
        ContourDash = 1 << 21,
        AdaptiveSourceField = 1 << 22,
        GlyphMajorPainterOrder = 1 << 23,
        WorldShadowCaster = 1 << 24,
        MultiDistanceField = 1 << 25,
        /// <summary>A paint maps in the text object's local space.</summary>
        ObjectCoordinates = 1 << 26
    }

    [Flags]
    public enum TextStudio2DShaderFeatures : uint
    {
        None = 0,
        SolidPaint = 1 << 0,
        GradientPaint = 1 << 1,
        TexturePaint = 1 << 2,
        NoisePaint = 1 << 3,
        Stroke = 1 << 4,
        Shadow = 1 << 5,
        Glow = 1 << 6,
        Bevel = 1 << 7,
        FauxDepth = 1 << 8,
        Selector = 1 << 9,
        Mask = 1 << 10,
        AdvancedBlend = 1 << 11,
        Decoration = 1 << 12,
        MaterialResponse = 1 << 13,
        GraphicEffect = 1 << 14,
        FieldNormal = 1 << 15,
        AnimatedEffect = 1 << 16,
        HeroPostProcess = 1 << 17,
        TemporalHistory = 1 << 18,
        ColorAdjustment = 1 << 19,
        WorldShadowCaster = 1 << 20
    }

    [Flags]
    public enum TextStudio2DBucketSplitReason : ushort
    {
        None = 0,
        ExecutionPath = 1 << 0,
        FusedOperationCapacity = 1 << 1,
        RetainedGeometry = 1 << 2,
        OffscreenCompositing = 1 << 3,
        RenderState = 1 << 4,
        MaskBoundary = 1 << 5,
        ResourceBoundary = 1 << 6,
        CapabilityFallback = 1 << 7
    }

    /// <summary>Renderer capability snapshot used for deterministic style compilation.</summary>
    public readonly struct TextStudio2DCompileContext
    {
        public TextStudio2DRendererCapabilities Capabilities { get; }
        public int MaxFusedOperations { get; }
        public int MaxGradientStops { get; }
        public bool AllowPathFallback { get; }

        /// <summary>Creates a renderer capability context with at least one fused operation and two gradient stops, plus the supplied fallback policy.</summary>
        public TextStudio2DCompileContext(
            TextStudio2DRendererCapabilities capabilities,
            int maxFusedOperations = 8,
            int maxGradientStops = 16,
            bool allowPathFallback = true)
        {
            Capabilities = capabilities;
            MaxFusedOperations = Mathf.Max(1, maxFusedOperations);
            MaxGradientStops = Mathf.Max(2, maxGradientStops);
            AllowPathFallback = allowPathFallback;
        }

        public static TextStudio2DCompileContext Full => new TextStudio2DCompileContext(
            TextStudio2DRendererCapabilities.All,
            8,
            16,
            true);

        public static TextStudio2DCompileContext ProductionField =>
            new TextStudio2DCompileContext(
                TextStudio2DRendererCapabilities.SignedDistanceField |
                TextStudio2DRendererCapabilities.PerGlyphStyleIndex |
                TextStudio2DRendererCapabilities.VertexColors |
                TextStudio2DRendererCapabilities.Gradients |
                TextStudio2DRendererCapabilities.TextureSampling |
                TextStudio2DRendererCapabilities.ProceduralNoise |
                TextStudio2DRendererCapabilities.PerGlyphSelectors |
                TextStudio2DRendererCapabilities.MaterialResponse |
                TextStudio2DRendererCapabilities.GraphicEffects |
                TextStudio2DRendererCapabilities.AnimatedEffects,
                8,
                16,
                true);
    }

    /// <summary>Conservative normalized expansion beyond source glyph bounds.</summary>
    public readonly struct TextStudio2DBoundsExpansion : IEquatable<TextStudio2DBoundsExpansion>
    {
        public float Left { get; }
        public float Bottom { get; }
        public float Right { get; }
        public float Top { get; }

        /// <summary>Creates a bounds expansion with negative distances clamped to zero on each side.</summary>
        public TextStudio2DBoundsExpansion(float left, float bottom, float right, float top)
        {
            Left = Mathf.Max(0f, left);
            Bottom = Mathf.Max(0f, bottom);
            Right = Mathf.Max(0f, right);
            Top = Mathf.Max(0f, top);
        }

        public bool IsZero => Left <= 0f && Bottom <= 0f && Right <= 0f && Top <= 0f;
        public Vector4 Vector => new Vector4(Left, Bottom, Right, Top);

        /// <summary>Returns the component-wise maximum expansion of this value and the supplied value.</summary>
        public TextStudio2DBoundsExpansion Max(in TextStudio2DBoundsExpansion other)
        {
            return new TextStudio2DBoundsExpansion(
                Mathf.Max(Left, other.Left),
                Mathf.Max(Bottom, other.Bottom),
                Mathf.Max(Right, other.Right),
                Mathf.Max(Top, other.Top));
        }

        public bool Equals(TextStudio2DBoundsExpansion other)
        {
            return Left.Equals(other.Left) &&
                   Bottom.Equals(other.Bottom) &&
                   Right.Equals(other.Right) &&
                   Top.Equals(other.Top);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DBoundsExpansion other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Vector.GetHashCode();
        }
    }

    /// <summary>Like-for-like workload estimate surfaced to diagnostics and authoring UI.</summary>
    public readonly struct TextStudio2DCostEstimate
    {
        public int FieldSamples { get; }
        public int TextureSamples { get; }
        public int GeometrySegments { get; }
        public int OffscreenPasses { get; }
        /// <summary>Peak concurrent Hero targets, including output, scratch, masks, and history.</summary>
        public int IntermediateSurfaces { get; }
        public int RelativeFragmentCost { get; }

        /// <summary>Creates a workload estimate from field and texture samples, geometry segments, offscreen passes, intermediate surfaces, and relative fragment cost.</summary>
        public TextStudio2DCostEstimate(
            int fieldSamples,
            int textureSamples,
            int geometrySegments,
            int offscreenPasses,
            int intermediateSurfaces,
            int relativeFragmentCost)
        {
            FieldSamples = fieldSamples;
            TextureSamples = textureSamples;
            GeometrySegments = geometrySegments;
            OffscreenPasses = offscreenPasses;
            IntermediateSurfaces = intermediateSurfaces;
            RelativeFragmentCost = relativeFragmentCost;
        }
    }

    /// <summary>Backend-neutral source field fidelity requested by a compiled appearance.</summary>
    public readonly struct TextStudio2DSourceFieldRequest
    {
        public TextStudio2DSourceFieldQuality Quality { get; }
        public float MinimumPixelsPerEm { get; }
        public float MinimumSafeDistanceEm { get; }
        public bool RequiresMultiDistanceField { get; }

        /// <summary>Creates a source-field fidelity request with minimum pixel density and safe distance clamped to nonnegative values.</summary>
        public TextStudio2DSourceFieldRequest(
            TextStudio2DSourceFieldQuality quality,
            float minimumPixelsPerEm,
            float minimumSafeDistanceEm,
            bool requiresMultiDistanceField)
        {
            Quality = quality;
            MinimumPixelsPerEm = Mathf.Max(0f, minimumPixelsPerEm);
            MinimumSafeDistanceEm = Mathf.Max(0f, minimumSafeDistanceEm);
            RequiresMultiDistanceField = requiresMultiDistanceField;
        }
    }

    /// <summary>Renderer batch identity. It intentionally contains no style asset or fingerprint.</summary>
    public readonly struct TextStudio2DBatchKey : IEquatable<TextStudio2DBatchKey>
    {
        public TextStudio2DExecutionPath ExecutionPath { get; }
        public TextStudio2DShaderFeatures Features { get; }
        public TextStudio2DBlendMode OutputBlend { get; }
        public byte SurfaceFormat { get; }

        /// <summary>Creates a batch identity from execution path, shader features, output blend mode, and surface format.</summary>
        public TextStudio2DBatchKey(
            TextStudio2DExecutionPath executionPath,
            TextStudio2DShaderFeatures features,
            TextStudio2DBlendMode outputBlend,
            byte surfaceFormat = 0)
        {
            ExecutionPath = executionPath;
            Features = features;
            OutputBlend = outputBlend;
            SurfaceFormat = surfaceFormat;
        }

        public bool Equals(TextStudio2DBatchKey other)
        {
            return ExecutionPath == other.ExecutionPath &&
                   Features == other.Features &&
                   OutputBlend == other.OutputBlend &&
                   SurfaceFormat == other.SurfaceFormat;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudio2DBatchKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ExecutionPath;
                hash = hash * 397 ^ (int)Features;
                hash = hash * 397 ^ (int)OutputBlend;
                return hash * 397 ^ SurfaceFormat;
            }
        }

        /// <summary>Returns whether two batch keys have equal execution paths, shader features, output blends, and surface formats.</summary>
        public static bool operator ==(TextStudio2DBatchKey left, TextStudio2DBatchKey right)
        {
            return left.Equals(right);
        }

        /// <summary>Returns whether two batch keys differ in execution path, shader features, output blend, or surface format.</summary>
        public static bool operator !=(TextStudio2DBatchKey left, TextStudio2DBatchKey right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Renderer-neutral paint parameters with references into a compiled style gradient-stop table.</summary>
    public readonly struct TextStudio2DCompiledPaint
    {
        public TextStudio2DPaintKind Kind { get; }
        public TextStudio2DMappingSpace Mapping { get; }
        public TextStudio2DRangeMapping RangeMapping { get; }
        public TextStudio2DGradientSpread Spread { get; }
        public Color Color { get; }
        public int GradientStopStart { get; }
        public int GradientStopCount { get; }
        public float Angle { get; }
        public Vector2 Center { get; }
        public Vector2 Scale { get; }
        public Vector2 Offset { get; }
        public Texture2D Texture { get; }
        public string ResourceId { get; }
        public TextStudio2DTextureFit TextureFit { get; }
        public TextStudio2DDistanceDomain DistanceDomain { get; }
        public Vector2 DistanceRange { get; }
        public float NoiseScale { get; }
        public float NoiseDetail { get; }
        public int NoiseSeed { get; }

        internal TextStudio2DCompiledPaint(
            in TextStudio2DPaint paint,
            int gradientStopStart,
            int gradientStopCount)
        {
            Kind = paint.Kind;
            Mapping = paint.Mapping;
            RangeMapping = paint.RangeMapping;
            Spread = paint.Spread;
            Color = paint.Color;
            GradientStopStart = gradientStopStart;
            GradientStopCount = gradientStopCount;
            Angle = paint.Angle;
            Center = paint.Center;
            Scale = paint.Scale;
            Offset = paint.Offset;
            Texture = paint.Texture;
            ResourceId = ResolveResourceId(in paint);
            TextureFit = paint.TextureFit;
            DistanceDomain = paint.DistanceDomain;
            DistanceRange = paint.DistanceRange;
            NoiseScale = paint.NoiseScale;
            NoiseDetail = paint.NoiseDetail;
            NoiseSeed = paint.NoiseSeed;
        }

        static string ResolveResourceId(in TextStudio2DPaint paint)
        {
            if (!string.IsNullOrWhiteSpace(paint.ResourceId) || paint.Texture == null)
                return paint.ResourceId;

            return "texture." + paint.Texture.GetStableId();
        }
    }

    /// <summary>A compiled application-selector graph and its shared evaluation state for 2D appearance operations.</summary>
    public readonly struct TextStudio2DCompiledSelector
    {
        readonly TextStudioApplicationSelectorNode[] _nodes;

        public int Root { get; }
        public int NodeCount => _nodes?.Length ?? 0;
        internal ulong Fingerprint { get; }
        internal bool HasDynamicMembership { get; }
        internal TextStudioCompiledSelector Shared { get; }
        /// <summary>Evaluation scratch shared by every copy of this selector; null when the selector is trivially all.</summary>
        internal TextStudio2DSelectorEvaluator.Workspace EvaluationWorkspace { get; }
        public bool IsTrivialAll => NodeCount == 0 ||
                                    Root == 0 &&
                                    _nodes[0].Kind == TextStudioSelectorNodeKind.All && !_nodes[0].Invert;

        internal TextStudio2DCompiledSelector(
            TextStudioApplicationSelectorNode[] nodes,
            int root)
        {
            _nodes = nodes != null
                ? (TextStudioApplicationSelectorNode[])nodes.Clone()
                : Array.Empty<TextStudioApplicationSelectorNode>();
            Root = root;
            Shared = new TextStudioCompiledSelector(_nodes, root);
            bool trivialAll = _nodes.Length == 0 ||
                              root == 0 && _nodes[0].Kind == TextStudioSelectorNodeKind.All && !_nodes[0].Invert;
            EvaluationWorkspace = trivialAll ? null : new TextStudio2DSelectorEvaluator.Workspace(_nodes, root);
            Fingerprint = 0;
            HasDynamicMembership = false;
            for (int index = 0; index < NodeCount; index++)
            {
                TextStudioSelectorNodeKind kind = _nodes[index].Kind;
                HasDynamicMembership |= kind == TextStudioSelectorNodeKind.SourceColor ||
                    kind == TextStudioSelectorNodeKind.SemanticState || kind == TextStudioSelectorNodeKind.Coverage ||
                    kind == TextStudioSelectorNodeKind.RenderProperty;
            }
            TextStudioDeterministicHash64 hash = default;
            TextStudio2DStyleCompiler.HashSelector(in this, ref hash);
            Fingerprint = hash.Value;
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = memory.Array(_nodes);
            if (_nodes != null) foreach (TextStudioApplicationSelectorNode node in _nodes) bytes += memory.Text(node.Key);
            return bytes;
        }

        /// <summary>Returns the selector node at the specified index, throwing if the index is outside the node array.</summary>
        public TextStudioApplicationSelectorNode GetNode(int index)
        {
            if (_nodes == null || index < 0 || index >= _nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _nodes[index];
        }
    }

    /// <summary>A compiled appearance operation containing resolved paint, selector, relationship, compositing, and effect settings.</summary>
    public readonly struct TextStudio2DCompiledOperation
    {
        public TextStudioStackItemId ItemId { get; }
        public TextStudio2DLayerKind Kind { get; }
        public int PaintIndex { get; }
        public int SelectorIndex { get; }
        public TextStudio2DGroupOrder GroupOrder { get; }
        public TextStudio2DSharedSurfaceOrder SharedSurfaceOrder { get; }
        public TextStudio2DSourceApplicability SourceApplicability { get; }
        public int ParentOperationIndex { get; }
        public int MaskOperationIndex { get; }
        /// <summary>The geometry owner for a surface treatment, or -1 for a top-level operation.</summary>
        public int SurfaceOwnerOperationIndex { get; }
        public float Opacity { get; }
        public TextStudio2DBlendMode BlendMode { get; }
        public TextStudio2DExecutionPath ExecutionPath { get; }
        public TextStudio2DQualityTier QualityTier { get; }
        public bool ParticipatesInAnimatedTint { get; }
        public Vector4 Parameters0 { get; }
        public Vector4 Parameters1 { get; }
        public TextStudio2DFillSpec Fill { get; }
        public TextStudio2DStrokeSpec Stroke { get; }
        public TextStudio2DShadowSpec Shadow { get; }
        public TextStudio2DGlowSpec Glow { get; }
        public TextStudio2DBevelSpec Bevel { get; }
        public TextStudio2DFauxDepthSpec FauxDepth { get; }
        public TextStudio2DDecorationSpec Decoration { get; }
        public TextStudio2DMaskSpec Mask { get; }
        public TextStudio2DMaterialResponseSpec MaterialResponse { get; }
        public TextStudio2DGraphicEffectSpec GraphicEffect { get; }
        public TextStudio2DAnimatedEffectSpec AnimatedEffect { get; }
        public TextStudio2DColorAdjustmentSpec ColorAdjustment { get; }
        public TextStudio2DShaderFeatures Features { get; }
        public TextStudio2DBoundsExpansion BoundsExpansion { get; }

        internal TextStudio2DCompiledOperation(
            TextStudioStackItemId itemId,
            TextStudio2DLayerKind kind,
            int paintIndex,
            int selectorIndex,
            int parentOperationIndex,
            TextStudio2DGroupOrder groupOrder,
            TextStudio2DSharedSurfaceOrder sharedSurfaceOrder,
            int maskOperationIndex,
            int surfaceOwnerOperationIndex,
            float opacity,
            TextStudio2DBlendMode blendMode,
            TextStudio2DExecutionPath executionPath,
            TextStudio2DQualityTier qualityTier,
            bool participatesInAnimatedTint,
            Vector4 parameters0,
            Vector4 parameters1,
            TextStudio2DFillSpec fill,
            TextStudio2DStrokeSpec stroke,
            TextStudio2DShadowSpec shadow,
            TextStudio2DGlowSpec glow,
            TextStudio2DBevelSpec bevel,
            TextStudio2DFauxDepthSpec fauxDepth,
            TextStudio2DDecorationSpec decoration,
            TextStudio2DMaskSpec mask,
            TextStudio2DMaterialResponseSpec materialResponse,
            TextStudio2DGraphicEffectSpec graphicEffect,
            TextStudio2DAnimatedEffectSpec animatedEffect,
            TextStudio2DColorAdjustmentSpec colorAdjustment,
            TextStudio2DShaderFeatures features,
            TextStudio2DBoundsExpansion boundsExpansion,
            TextStudio2DSourceApplicability sourceApplicability = TextStudio2DSourceApplicability.Automatic)
        {
            ItemId = itemId;
            Kind = kind;
            PaintIndex = paintIndex;
            SelectorIndex = selectorIndex;
            ParentOperationIndex = parentOperationIndex;
            GroupOrder = groupOrder;
            SharedSurfaceOrder = sharedSurfaceOrder;
            SourceApplicability = sourceApplicability;
            MaskOperationIndex = maskOperationIndex;
            SurfaceOwnerOperationIndex = surfaceOwnerOperationIndex;
            Opacity = opacity;
            BlendMode = blendMode;
            ExecutionPath = executionPath;
            QualityTier = qualityTier;
            ParticipatesInAnimatedTint = participatesInAnimatedTint;
            Parameters0 = parameters0;
            Parameters1 = parameters1;
            Fill = fill;
            Stroke = stroke.Clone();
            Shadow = shadow;
            Glow = glow;
            Bevel = bevel;
            FauxDepth = fauxDepth;
            Decoration = decoration.Clone();
            Mask = mask;
            MaterialResponse = materialResponse;
            GraphicEffect = graphicEffect;
            AnimatedEffect = animatedEffect;
            ColorAdjustment = colorAdjustment.Resolve();
            Features = features;
            BoundsExpansion = boundsExpansion;
        }
    }

    internal static class TextStudio2DSoftEffectUtility
    {
        internal static bool RequiresTextWideBlur(in TextStudio2DLayer layer)
        {
            return (layer.Kind == TextStudio2DLayerKind.OuterShadow &&
                    layer.Shadow.Rendering == TextStudio2DSoftEffectRendering.TextWideBlur) ||
                   (layer.Kind == TextStudio2DLayerKind.OuterGlow &&
                    layer.Glow.Rendering == TextStudio2DSoftEffectRendering.TextWideBlur);
        }

        internal static bool TryResolveTextWideBlur(
            in TextStudio2DCompiledOperation operation,
            out Vector2 offset,
            out float radius,
            out float spread)
        {
            if (operation.Kind == TextStudio2DLayerKind.OuterShadow &&
                operation.Shadow.Rendering == TextStudio2DSoftEffectRendering.TextWideBlur)
            {
                offset = operation.Shadow.Offset;
                radius = operation.Shadow.Blur;
                spread = operation.Shadow.Spread;
                return true;
            }
            if (operation.Kind == TextStudio2DLayerKind.OuterGlow &&
                operation.Glow.Rendering == TextStudio2DSoftEffectRendering.TextWideBlur)
            {
                offset = Vector2.zero;
                radius = operation.Glow.Radius;
                spread = operation.Glow.Spread;
                return true;
            }
            offset = Vector2.zero;
            radius = 0f;
            spread = 0f;
            return false;
        }
    }

    /// <summary>A contiguous range of compiled operations with its batch key, required shader features, and split reason.</summary>
    public readonly struct TextStudio2DCompiledBucket
    {
        public int FirstOperation { get; }
        public int OperationCount { get; }
        public TextStudio2DBatchKey BatchKey { get; }
        public TextStudio2DShaderFeatures RequiredFeatures { get; }
        public TextStudio2DBucketSplitReason SplitReason { get; }

        internal TextStudio2DCompiledBucket(
            int firstOperation,
            int operationCount,
            TextStudio2DBatchKey batchKey,
            TextStudio2DShaderFeatures requiredFeatures,
            TextStudio2DBucketSplitReason splitReason)
        {
            FirstOperation = firstOperation;
            OperationCount = operationCount;
            BatchKey = batchKey;
            RequiredFeatures = requiredFeatures;
            SplitReason = splitReason;
        }
    }

    /// <summary>Validated immutable renderer-neutral 2D appearance intermediate representation.</summary>
    public sealed class CompiledTextStudio2DStyle
    {
        readonly TextStudio2DCompiledOperation[] _operations;
        readonly TextStudio2DCompiledPaint[] _paints;
        readonly TextStudio2DGradientStop[] _gradientStops;
        readonly TextStudio2DCompiledSelector[] _selectors;
        readonly TextStudio2DCompiledBucket[] _buckets;
        readonly TextStudioStackDiagnostic[] _diagnostics;

        internal CompiledTextStudio2DStyle(
            TextStudio2DCompiledOperation[] operations,
            TextStudio2DCompiledPaint[] paints,
            TextStudio2DGradientStop[] gradientStops,
            TextStudio2DCompiledSelector[] selectors,
            TextStudio2DCompiledBucket[] buckets,
            TextStudioStackDiagnostic[] diagnostics,
            TextStudioStackFingerprint fingerprint,
            TextStudio2DStyleRequirements requirements,
            TextStudio2DBoundsExpansion boundsExpansion,
            TextStudio2DCostEstimate cost,
            ulong dependencyRevision,
            TextStudio2DSourceFieldRequest sourceFieldRequest,
            TextStudio2DPainterOrder painterOrder,
            TextStudio2DWorldShadowMode worldShadowMode)
        {
            _operations = operations ?? Array.Empty<TextStudio2DCompiledOperation>();
            _paints = paints ?? Array.Empty<TextStudio2DCompiledPaint>();
            _gradientStops = gradientStops ?? Array.Empty<TextStudio2DGradientStop>();
            _selectors = selectors ?? Array.Empty<TextStudio2DCompiledSelector>();
            _buckets = buckets ?? Array.Empty<TextStudio2DCompiledBucket>();
            _diagnostics = diagnostics ?? Array.Empty<TextStudioStackDiagnostic>();
            Fingerprint = fingerprint;
            Requirements = requirements;
            BoundsExpansion = boundsExpansion;
            Cost = cost;
            DependencyRevision = dependencyRevision;
            SourceFieldRequest = sourceFieldRequest;
            PainterOrder = painterOrder;
            WorldShadowMode = worldShadowMode;
            GeometryFingerprint = TextStudio2DStyleCompiler.ComputeGeometryFingerprint(this);

            int historyOperationCount = 0;
            int maximumHistoryFrames = 0;
            bool requiresContinuousRendering = false;
            for (int index = 0; index < _operations.Length; index++)
            {
                TextStudio2DCompiledOperation operation = _operations[index];
                if (TextStudio2DStyleDecorations.UsesContourStroke(in operation) ||
                    operation.Kind == TextStudio2DLayerKind.Decoration &&
                    (operation.Decoration.Line.SkipInk != TextStudio2DSkipInk.Off || operation.Decoration.Shape == TextStudio2DDecorationShape.GlyphSilhouette))
                    RequiresContourGeometry = true;
                if (operation.Kind == TextStudio2DLayerKind.Decoration &&
                    (operation.Decoration.Line.Units == TextStudio2DUnit.Pixels ||
                     operation.Decoration.GetTreatmentExpansion(TextStudio2DUnit.Pixels) != Vector4.zero))
                    RequiresCameraPixels = true;
                if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect)
                    continue;
                TextStudio2DAnimatedEffectSpec effect = operation.AnimatedEffect;
                if (Mathf.Abs(effect.Speed) > 0.000001f)
                    requiresContinuousRendering = true;
                if (!TextStudio2DAnimatedEffectUtility.RequiresHistory(effect.Kind))
                    continue;
                historyOperationCount++;
                maximumHistoryFrames = Mathf.Max(
                    maximumHistoryFrames,
                    effect.HistoryFrames);
            }
            RequiresContinuousRendering = requiresContinuousRendering;
            HistoryOperationCount = historyOperationCount;
            MaximumHistoryFrames = maximumHistoryFrames;

            bool valid = true;
            for (int i = 0; i < _diagnostics.Length; i++)
            {
                if (_diagnostics[i].Severity != TextStudioStackDiagnosticSeverity.Error)
                    continue;
                valid = false;
                break;
            }
            IsValid = valid;
            if (TextStudio2DHeroProgram.RequiresHero(this))
            {
                TextStudio2DHeroProgram.TryCreate(this, out TextStudio2DHeroProgram program, out TextStudio2DHeroProgramStatus status);
                HeroProgram = program;
                HeroProgramStatus = status;
                if (program != null)
                    Cost = new TextStudio2DCostEstimate(cost.FieldSamples, cost.TextureSamples, cost.GeometrySegments,
                        cost.OffscreenPasses, program.EstimatedConcurrentSurfaceCount, cost.RelativeFragmentCost);
            }
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = memory.Array(_operations) + memory.Array(_paints) + memory.Array(_gradientStops) +
                memory.Array(_selectors) + memory.Array(_buckets) + memory.Array(_diagnostics);
            foreach (TextStudio2DCompiledSelector selector in _selectors) bytes += selector.CaptureManagedPayload(memory);
            foreach (TextStudio2DCompiledOperation operation in _operations)
                bytes += operation.Stroke.CaptureManagedPayload(memory) + operation.Decoration.CaptureManagedPayload(memory);
            foreach (TextStudio2DCompiledPaint paint in _paints) bytes += memory.Text(paint.ResourceId);
            return bytes + (HeroProgram?.ManagedPayloadBytes ?? 0);
        }

        public int OperationCount => _operations.Length;
        public int PaintCount => _paints.Length;
        public int GradientStopCount => _gradientStops.Length;
        public int SelectorCount => _selectors.Length;
        public int BucketCount => _buckets.Length;
        public int DiagnosticCount => _diagnostics.Length;
        public bool IsValid { get; }
        public bool IsPassThrough => _operations.Length == 0;
        public TextStudioStackFingerprint Fingerprint { get; }
        internal ulong GeometryFingerprint { get; }
        public TextStudio2DStyleRequirements Requirements { get; }
        public TextStudio2DBoundsExpansion BoundsExpansion { get; }
        public TextStudio2DCostEstimate Cost { get; }
        internal TextStudio2DHeroProgram HeroProgram { get; }
        internal TextStudio2DHeroProgramStatus HeroProgramStatus { get; }
        public ulong DependencyRevision { get; }
        public TextStudio2DSourceFieldRequest SourceFieldRequest { get; }
        public TextStudio2DPainterOrder PainterOrder { get; }
        public TextStudio2DWorldShadowMode WorldShadowMode { get; }
        public bool RequiresContinuousRendering { get; }
        internal bool RequiresCameraPixels { get; }
        internal bool RequiresContourGeometry { get; }
        public int HistoryOperationCount { get; }
        public int MaximumHistoryFrames { get; }

        /// <summary>Returns the compiled operation at the specified index, throwing if the index is outside the operation table.</summary>
        public TextStudio2DCompiledOperation GetOperation(int index)
        {
            return OperationAt(index);
        }

        // Operations are 904-byte records. Runtime callers read them in place, since per-glyph selector and mapping
        // passes copied every operation for every glyph through the by-value accessor.
        internal ref readonly TextStudio2DCompiledOperation OperationAt(int index)
        {
            if (index < 0 || index >= _operations.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref _operations[index];
        }

        /// <summary>Returns the compiled paint at the specified index, throwing if the index is outside the paint table.</summary>
        public TextStudio2DCompiledPaint GetPaint(int index)
        {
            return GetAt(_paints, index, nameof(index));
        }

        /// <summary>Returns the gradient stop at the specified index in the shared compiled stop table.</summary>
        public TextStudio2DGradientStop GetGradientStop(int index)
        {
            return GetAt(_gradientStops, index, nameof(index));
        }

        /// <summary>Returns the compiled application selector at the specified index.</summary>
        public TextStudio2DCompiledSelector GetSelector(int index)
        {
            return GetAt(_selectors, index, nameof(index));
        }

        /// <summary>Returns the compiled render bucket at the specified index.</summary>
        public TextStudio2DCompiledBucket GetBucket(int index)
        {
            return GetAt(_buckets, index, nameof(index));
        }

        /// <summary>Returns the compilation diagnostic at the specified index.</summary>
        public TextStudioStackDiagnostic GetDiagnostic(int index)
        {
            return GetAt(_diagnostics, index, nameof(index));
        }

        internal bool RequiresContinuousRenderingForPolicy(
            TextStudioMotionPolicy policy)
        {
            TextStudioMotionPolicy resolved = TextStudioMotionSettings.Resolve(policy);
            for (int index = 0; index < _operations.Length; index++)
            {
                TextStudio2DCompiledOperation operation = _operations[index];
                if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect)
                    continue;
                TextStudio2DAnimatedEffectSpec effect = operation.AnimatedEffect;
                if (Mathf.Abs(effect.Speed) <= 0.000001f)
                    continue;
                float rate = TextStudioVisualPrimitiveMath.ResolveMotionRate(
                    resolved,
                    effect.ReducedMotionBehavior,
                    out bool enabled);
                if (enabled && Mathf.Abs(effect.Speed * rate) > 0.000001f)
                    return true;
            }
            return false;
        }

        static T GetAt<T>(T[] values, int index, string parameterName)
        {
            if (values == null || index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(parameterName);
            return values[index];
        }
    }

    /// <summary>Compiles persistent 2D style assets into one shared backend-neutral plan.</summary>
    public static class TextStudio2DStyleCompiler
    {
        const string DiagnosticPrefix = "textstudio.style2d.";
        const int MaximumCompiledLayers = 512;
        const int MaximumInheritanceDepth = 64;
        const float MaximumEfficientFusedDepthExpansion = 0.5f;
#if UNITY_6000_7_OR_NEWER
        // A resource-free immutable program is code-lifetime metadata, shared by native/None consumers.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly CompiledTextStudio2DStyle s_passThrough = CompileUncached(null, TextStudio2DCompileContext.Full, ComputeDependencyRevision(null));

        /// <summary>Compiles a style asset and its inherited layers for the supplied renderer capabilities and compilation limits.</summary>
        public static CompiledTextStudio2DStyle Compile(
            TextStudio2DStyleAsset style,
            TextStudio2DCompileContext context)
        {
            bool sharedContext = context.Capabilities == TextStudio2DRendererCapabilities.All && context.MaxFusedOperations == 8 &&
                context.MaxGradientStops == 16 && context.AllowPathFallback;
            if (style == null && sharedContext) return s_passThrough;
            ulong dependencyRevision = ComputeDependencyRevision(style);
            if (style != null && sharedContext && style.TryGetSharedProgram(dependencyRevision, out CompiledTextStudio2DStyle cached)) return cached;
            CompiledTextStudio2DStyle program = CompileUncached(style, context, dependencyRevision);
            if (style != null && sharedContext) style.PublishSharedProgram(program);
            return program;
        }

        static CompiledTextStudio2DStyle CompileUncached(TextStudio2DStyleAsset style, TextStudio2DCompileContext context, ulong dependencyRevision)
        {
            List<TextStudioStackDiagnostic> diagnostics = new List<TextStudioStackDiagnostic>();
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>();
            HashSet<int> dependencyPath = new HashSet<int>();
            FlattenStyle(style, layers, diagnostics, dependencyPath);
            TextStudio2DStyleSettings settings = style != null
                ? style.GetSettingsForCompilation()
                : default;
            return CompileLayers(in settings, context, layers, diagnostics, dependencyRevision);
        }

        /// <summary>Compiles component-owned settings through the same appearance pipeline as assets.</summary>
        public static CompiledTextStudio2DStyle Compile(in TextStudio2DStyleSettings settings)
        {
            return Compile(in settings, TextStudio2DCompileContext.Full);
        }

        /// <summary>Compiles component-owned style settings for the supplied renderer capabilities and compilation limits.</summary>
        public static CompiledTextStudio2DStyle Compile(
            in TextStudio2DStyleSettings settings,
            TextStudio2DCompileContext context)
        {
            List<TextStudioStackDiagnostic> diagnostics = new List<TextStudioStackDiagnostic>();
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>();
            FlattenSettings(in settings, layers, diagnostics, new HashSet<int>());
            return CompileLayers(in settings, context, layers, diagnostics, ComputeDependencyRevision(in settings));
        }

        static CompiledTextStudio2DStyle CompileLayers(
            in TextStudio2DStyleSettings settings,
            TextStudio2DCompileContext context,
            List<TextStudio2DLayer> layers,
            List<TextStudioStackDiagnostic> diagnostics,
            ulong dependencyRevision)
        {
            TextStudio2DSourceFieldQuality sourceFieldQuality = settings.SourceFieldQuality;
            TextStudio2DPainterOrder painterOrder = settings.PainterOrder;
            TextStudio2DWorldShadowMode worldShadowMode = settings.WorldShadowMode;
            layers = new List<TextStudio2DLayer>(TextStudioAppearanceDomains.DefaultLayers(layers.ToArray()));
            layers = ExpandSurfaceTreatments(layers, diagnostics);
            if (layers.Count > MaximumCompiledLayers)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "layer-capacity",
                    -1,
                    default,
                    "A compiled style cannot exceed 512 operations, including surface treatments.");
                layers.Clear();
            }

            bool[] active = ResolveActiveLayers(layers);
            Dictionary<TextStudioStackItemId, int> sourceIndices = BuildSourceIndex(
                layers,
                diagnostics);
            ValidateRelationships(layers, sourceIndices, diagnostics);

            List<TextStudio2DCompiledOperation> operations =
                new List<TextStudio2DCompiledOperation>(layers.Count);
            List<TextStudio2DCompiledPaint> paints =
                new List<TextStudio2DCompiledPaint>(layers.Count);
            List<TextStudio2DGradientStop> gradientStops =
                new List<TextStudio2DGradientStop>();
            List<TextStudio2DCompiledSelector> selectors =
                new List<TextStudio2DCompiledSelector>(layers.Count);
            Dictionary<TextStudioStackItemId, int> compiledIndices =
                new Dictionary<TextStudioStackItemId, int>();
            bool[] compilable = new bool[layers.Count];
            int plannedOperationIndex = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudio2DLayer candidate = layers[i];
                if (!active[i] || !ValidateLayerEnums(in candidate, i, diagnostics))
                    continue;
                if (TextStudio2DColorAdjustmentUtility.IsAdjustment(candidate.Kind) && candidate.ColorAdjustment.IsNeutral)
                    continue;
                compilable[i] = true;
                compiledIndices[candidate.ItemId] = plannedOperationIndex++;
            }

            TextStudio2DStyleRequirements requirements = TextStudio2DStyleRequirements.None;
            if (painterOrder == TextStudio2DPainterOrder.GlyphMajor)
                requirements |= TextStudio2DStyleRequirements.GlyphMajorPainterOrder;
            if (worldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped)
                requirements |= TextStudio2DStyleRequirements.WorldShadowCaster;
            if (sourceFieldQuality != TextStudio2DSourceFieldQuality.Native)
                requirements |= TextStudio2DStyleRequirements.AdaptiveSourceField;
            TextStudio2DBoundsExpansion totalBounds = default;
            TextStudioDeterministicHash64 topologyHash = default;
            TextStudioDeterministicHash64 parameterHash = default;
            TextStudioDeterministicHash64 renderStateHash = default;
            TextStudioDeterministicHash64 boundsHash = default;

            for (int i = 0; i < layers.Count; i++)
            {
                if (!compilable[i])
                    continue;

                TextStudio2DLayer layer = layers[i];
                if (UsesPaint(layer.Kind) && layer.Paint.UsesSharedSource && layer.Paint.Source == null)
                    AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "missing-paint-source", i, layer.ItemId,
                        "This layer's shared paint is missing. Assign a Paint asset or make the paint local.");
                if (UsesPaint(layer.Kind))
                    layer = layer.WithPaint(layer.Paint.Resolve());
                layer = SanitizeLayer(layer, i, diagnostics);
                if (layer.Kind == TextStudio2DLayerKind.Group && layer.GroupOrder == TextStudio2DGroupOrder.ByCharacter)
                    requirements |= TextStudio2DStyleRequirements.GlyphMajorPainterOrder;
                TextStudio2DCompiledSelector selector = CompileSurfaceSelector(in layer, layers, sourceIndices, i, diagnostics);
                int selectorIndex = selectors.Count;
                for (int existing = 0; existing < selectors.Count; existing++)
                {
                    if (selectors[existing].Fingerprint == selector.Fingerprint)
                    {
                        selectorIndex = existing;
                        break;
                    }
                }
                if (selectorIndex == selectors.Count)
                    selectors.Add(selector);

                int paintIndex = -1;
                if (UsesPaint(layer.Kind))
                {
                    paintIndex = CompilePaint(
                        layer.Paint,
                        i,
                        layer.ItemId,
                        context,
                        gradientStops,
                        paints,
                        diagnostics);
                }

                TextStudio2DExecutionPath minimumPath = DetermineMinimumPath(
                    in layer,
                    in selector,
                    context,
                    ref requirements);
                if (painterOrder == TextStudio2DPainterOrder.GlyphMajor &&
                    minimumPath == TextStudio2DExecutionPath.FusedField)
                {
                    minimumPath = TextStudio2DExecutionPath.HeroCompositor;
                }
                TextStudio2DExecutionPath executionPath = ResolveExecutionPath(
                    layer,
                    minimumPath,
                    context,
                    i,
                    diagnostics);
                AddPathRequirements(executionPath, ref requirements);

                int parentIndex = ResolveCompiledRelationship(
                    layer.ParentId,
                    compiledIndices,
                    i,
                    layer.ItemId,
                    "parent",
                    diagnostics);
                int maskIndex = ResolveCompiledRelationship(
                    layer.Kind == TextStudio2DLayerKind.Mask
                        ? layer.Mask.SourceItemId
                        : layer.MaskId,
                    compiledIndices,
                    i,
                    layer.ItemId,
                    "mask",
                    diagnostics);

                TextStudio2DBoundsExpansion layerBounds = ComputeBounds(in layer);
                totalBounds = totalBounds.Max(in layerBounds);
                if (!layerBounds.IsZero)
                    requirements |= TextStudio2DStyleRequirements.ExpandedBounds;

                TextStudio2DShaderFeatures features = ResolveFeatures(
                    in layer,
                    in selector);
                Vector4 parameters0;
                Vector4 parameters1;
                ResolveParameters(in layer, out parameters0, out parameters1);
                TextStudio2DCompiledOperation operation = new TextStudio2DCompiledOperation(
                    layer.ItemId,
                    layer.Kind,
                    paintIndex,
                    selectorIndex,
                    parentIndex,
                    layer.GroupOrder,
                    layer.SharedSurfaceOrder,
                    maskIndex,
                    layer.SurfaceOwnerId.IsValid && compiledIndices.TryGetValue(layer.SurfaceOwnerId, out int surfaceOwnerIndex) ? surfaceOwnerIndex : -1,
                    layer.Opacity,
                    layer.BlendMode,
                    executionPath,
                    layer.QualityTier,
                    layer.ParticipatesInAnimatedTint,
                    parameters0,
                    parameters1,
                    layer.Fill,
                    layer.Stroke,
                    layer.Shadow,
                    layer.Glow,
                    layer.Bevel,
                    layer.FauxDepth,
                    layer.Decoration,
                    layer.Mask,
                    layer.MaterialResponse,
                    layer.GraphicEffect,
                    layer.AnimatedEffect,
                    layer.ColorAdjustment,
                    features,
                    layerBounds, layer.SourceApplicability);
                operations.Add(operation);

                HashLayer(
                    in layer,
                    in selector,
                    paintIndex >= 0 ? paints[paintIndex] : default,
                    gradientStops,
                    executionPath,
                    layerBounds,
                    ref topologyHash,
                    ref parameterHash,
                    ref renderStateHash,
                    ref boundsHash);
            }

            ValidateAnimatedHistoryCapacity(operations, diagnostics);
            AddStrokeCapabilityDiagnostics(operations, diagnostics);

            List<TextStudio2DCompiledBucket> buckets = BuildBuckets(
                operations,
                context.MaxFusedOperations);
            HashBuckets(buckets, ref renderStateHash);
            TextStudio2DCostEstimate cost = EstimateCost(operations, paints, buckets);
            TextStudio2DSourceFieldRequest sourceFieldRequest =
                BuildSourceFieldRequest(
                    sourceFieldQuality,
                    operations);
            topologyHash.Add((byte)painterOrder);
            renderStateHash.Add((byte)sourceFieldQuality);
            renderStateHash.Add((byte)worldShadowMode);
            parameterHash.Add(sourceFieldRequest.MinimumPixelsPerEm);
            parameterHash.Add(sourceFieldRequest.MinimumSafeDistanceEm);
            parameterHash.Add(sourceFieldRequest.RequiresMultiDistanceField);
            TextStudioStackFingerprint fingerprint = BuildFingerprint(
                ref topologyHash,
                ref parameterHash,
                ref renderStateHash,
                ref boundsHash);

            return new CompiledTextStudio2DStyle(
                operations.ToArray(),
                paints.ToArray(),
                gradientStops.ToArray(),
                selectors.ToArray(),
                buckets.ToArray(),
                diagnostics.ToArray(),
                fingerprint,
                requirements,
                totalBounds,
                cost,
                dependencyRevision,
                sourceFieldRequest,
                painterOrder,
                worldShadowMode);
        }

        static List<TextStudio2DLayer> ExpandSurfaceTreatments(List<TextStudio2DLayer> layers, List<TextStudioStackDiagnostic> diagnostics)
        {
            List<TextStudio2DLayer> expanded = new List<TextStudio2DLayer>(layers.Count);
            for (int index = 0; index < layers.Count && expanded.Count <= MaximumCompiledLayers; index++)
            {
                TextStudio2DLayer owner = layers[index];
                expanded.Add(owner);
                if (owner.Kind != TextStudio2DLayerKind.Decoration) continue;
                TextStudio2DDecorationSpec decoration = owner.Decoration;
                for (int treatmentIndex = 0; treatmentIndex < decoration.TreatmentCount && expanded.Count <= MaximumCompiledLayers; treatmentIndex++)
                {
                    TextStudio2DSurfaceTreatment treatment = decoration.GetTreatmentUnsafe(treatmentIndex);
                    if (!TextStudio2DSurfaceTreatment.IsSupportedKind(treatment.Kind))
                    {
                        AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "surface-treatment-kind", index, treatment.ItemId,
                            "This treatment cannot be applied to a decoration surface.");
                        continue;
                    }
                    if (treatment.Kind == TextStudio2DLayerKind.Stroke &&
                        (treatment.Stroke.Join != TextStudio2DStrokeJoin.Round || treatment.Stroke.DashCount > 0))
                        AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "surface-stroke-source", index, treatment.ItemId,
                            "Surface strokes currently support continuous round joins. Contour strokes require a prepared boundary.");
                    if ((treatment.Kind == TextStudio2DLayerKind.InnerShadow || treatment.Kind == TextStudio2DLayerKind.OuterShadow) &&
                        treatment.Shadow.Rendering != TextStudio2DSoftEffectRendering.DistanceField ||
                        (treatment.Kind == TextStudio2DLayerKind.InnerGlow || treatment.Kind == TextStudio2DLayerKind.OuterGlow) &&
                        treatment.Glow.Rendering != TextStudio2DSoftEffectRendering.DistanceField)
                        AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "surface-soft-rendering", index, treatment.ItemId,
                            "Surface shadows and glows use the shared shape distance. Text Wide Blur applies to glyph layers.");
                    expanded.Add(treatment.ToLayer(in owner));
                }
            }
            return expanded;
        }

        /// <summary>Compiles a style asset and its inherited layers using the full renderer capability context.</summary>
        public static CompiledTextStudio2DStyle Compile(TextStudio2DStyleAsset style)
        {
            return Compile(style, TextStudio2DCompileContext.Full);
        }

        /// <summary>Classifies the invalidation required when changing from one compiled appearance program to another.</summary>
        public static TextStudioStackChange ClassifyChanges(
            CompiledTextStudio2DStyle previous,
            CompiledTextStudio2DStyle current)
        {
            if (ReferenceEquals(previous, current))
                return TextStudioStackChange.None;
            if (previous == null || current == null)
                return TextStudioStackChange.All;
            return TextStudioStackFingerprintUtility.Classify(
                previous.Fingerprint,
                current.Fingerprint);
        }

        /// <summary>Computes a revision hash from a style, its inherited base styles, and their referenced paint dependencies.</summary>
        public static ulong ComputeDependencyRevision(TextStudio2DStyleAsset style)
        {
            TextStudioDeterministicHash64 hash = default;
            TextStudio2DStyleAsset current = style;
            int depth = 0;
            while (current != null && depth < 64)
            {
                int instanceId = current.GetStableId();
                hash.Add(instanceId);
                hash.Add(current.Revision);
                hash.Add(current.SchemaVersion);
                current.HashPaintDependencies(ref hash);
                if (current.InheritanceMode == TextStudio2DStyleInheritanceMode.Replace)
                {
                    current = null;
                    break;
                }
                current = current.BaseStyle;
                depth++;
            }
            if (current != null)
                hash.Add(int.MinValue);
            return hash.Value;
        }

        internal static ulong ComputeDependencyRevision(in TextStudio2DStyleSettings settings)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(settings.Revision);
            settings.HashPaintDependencies(ref hash);
            if (settings.InheritanceMode != TextStudio2DStyleInheritanceMode.Replace)
                hash.Add(ComputeDependencyRevision(settings.BaseStyle));
            return hash.Value;
        }

        internal static bool TryCreateInlineSettings(
            TextStudio2DStyleAsset style,
            out TextStudio2DStyleSettings settings,
            out string error)
        {
            settings = default;
            error = string.Empty;
            if (style == null)
            {
                error = "Assign an appearance preset before converting it to Inline.";
                return false;
            }

            List<TextStudioStackDiagnostic> diagnostics = new List<TextStudioStackDiagnostic>();
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>();
            FlattenStyle(style, layers, diagnostics, new HashSet<int>());
            TextStudio2DStyleSettings authored = style.GetSettingsForCompilation();
            CompiledTextStudio2DStyle compiled = CompileLayers(
                in authored, TextStudio2DCompileContext.Full, layers, diagnostics,
                ComputeDependencyRevision(style));
            for (int i = 0; i < compiled.DiagnosticCount; i++)
            {
                TextStudioStackDiagnostic diagnostic = compiled.GetDiagnostic(i);
                if (diagnostic.Severity != TextStudioStackDiagnosticSeverity.Error)
                    continue;
                error = diagnostic.Message + " Fix the preset before converting it to Inline.";
                return false;
            }

            settings = new TextStudio2DStyleSettings(
                null, TextStudio2DStyleInheritanceMode.Append, authored.SourceFieldQuality,
                authored.PainterOrder, authored.WorldShadowMode, layers.ToArray(), CollectExposures(in authored));
            return true;
        }

        internal static TextStudio2DStyleSettings FlattenAuthoredSettings(in TextStudio2DStyleSettings settings, List<string> errors = null)
        {
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>();
            List<TextStudioStackDiagnostic> diagnostics = new List<TextStudioStackDiagnostic>();
            FlattenSettings(in settings, layers, diagnostics, new HashSet<int>());
            foreach (TextStudioStackDiagnostic diagnostic in diagnostics)
                if (diagnostic.Severity == TextStudioStackDiagnosticSeverity.Error) errors?.Add(diagnostic.Message);
            return new TextStudio2DStyleSettings(null, TextStudio2DStyleInheritanceMode.Replace,
                settings.SourceFieldQuality, settings.PainterOrder, settings.WorldShadowMode,
                layers.ToArray(), CollectExposures(in settings));
        }

        static TextStudioAppearanceExposure[] CollectExposures(in TextStudio2DStyleSettings settings)
        {
            List<TextStudioAppearanceExposure> exposures = new List<TextStudioAppearanceExposure>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> assets = new HashSet<int>();
            TextStudio2DStyleSettings current = settings;
            for (int depth = 0; depth < MaximumInheritanceDepth; depth++)
            {
                foreach (TextStudioAppearanceExposure exposure in current.Exposures)
                    if (names.Add(exposure.Name)) exposures.Add(exposure);
                if (current.BaseStyle == null || current.InheritanceMode == TextStudio2DStyleInheritanceMode.Replace || !assets.Add(current.BaseStyle.GetStableId())) break;
                current = current.BaseStyle.GetSettingsForCompilation();
            }
            return exposures.ToArray();
        }

        static void FlattenStyle(
            TextStudio2DStyleAsset style,
            List<TextStudio2DLayer> destination,
            List<TextStudioStackDiagnostic> diagnostics,
            HashSet<int> dependencyPath)
        {
            if (style == null)
                return;

            if (dependencyPath.Count >= MaximumInheritanceDepth)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "inheritance-depth",
                    -1,
                    default,
                    "The style inheritance chain cannot exceed 64 assets.");
                return;
            }

            int instanceId = style.GetStableId();
            if (!dependencyPath.Add(instanceId))
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "inheritance-cycle",
                    -1,
                    default,
                    "The style inheritance graph contains a cycle.");
                return;
            }

            if (style.SchemaVersion < 6 || style.SchemaVersion > TextStudio2DStyleAsset.CurrentSchemaVersion)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "schema-version",
                    -1,
                    default,
                    "The style schema version is not supported by this runtime.");
            }

            TextStudio2DStyleSettings settings = style.GetSettingsForCompilation();
            FlattenSettings(in settings, destination, diagnostics, dependencyPath);
            dependencyPath.Remove(instanceId);
        }

        static void FlattenSettings(
            in TextStudio2DStyleSettings settings,
            List<TextStudio2DLayer> destination,
            List<TextStudioStackDiagnostic> diagnostics,
            HashSet<int> dependencyPath)
        {
            TextStudio2DLayer[] local = settings.CloneAuthoredLayers();
            // Preserve all authored domains through inheritance; partition the effective stack before compilation.
            TextStudio2DStyleInheritanceMode inheritanceMode = settings.InheritanceMode;
            if ((uint)inheritanceMode >
                (uint)TextStudio2DStyleInheritanceMode.Replace)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "inheritance-mode",
                    -1,
                    default,
                    "The style inheritance mode is invalid.");
                inheritanceMode = TextStudio2DStyleInheritanceMode.Replace;
            }
            if (settings.BaseStyle == null ||
                inheritanceMode == TextStudio2DStyleInheritanceMode.Replace)
            {
                AppendLayers(local, destination);
            }
            else
            {
                FlattenStyle(
                    settings.BaseStyle,
                    destination,
                    diagnostics,
                    dependencyPath);
                ClearInheritedSoloForLocalDomains(local, destination);
                if (inheritanceMode == TextStudio2DStyleInheritanceMode.Append)
                {
                    AppendLayers(local, destination);
                }
                else
                {
                    OverrideLayers(local, destination);
                }
            }

        }

        static void AppendLayers(TextStudio2DLayer[] layers, List<TextStudio2DLayer> destination)
        {
            for (int i = 0; i < layers.Length; i++)
                destination.Add(layers[i].Clone());
        }

        static void OverrideLayers(TextStudio2DLayer[] local, List<TextStudio2DLayer> destination)
        {
            Dictionary<TextStudioStackItemId, int> indices =
                new Dictionary<TextStudioStackItemId, int>();
            for (int i = 0; i < destination.Count; i++)
                indices[destination[i].ItemId] = i;
            for (int i = 0; i < local.Length; i++)
            {
                TextStudio2DLayer layer = local[i].Clone();
                if (indices.TryGetValue(layer.ItemId, out int index))
                {
                    if (destination[index].IsEnabled &&
                        destination[index].IsSolo &&
                        layer.IsEnabled &&
                        !layer.IsSolo)
                    {
                        layer = layer.WithState(true, true);
                    }
                    destination[index] = layer;
                }
                else
                {
                    indices[layer.ItemId] = destination.Count;
                    destination.Add(layer);
                }
            }
        }

        static void ClearInheritedSoloForLocalDomains(TextStudio2DLayer[] local, List<TextStudio2DLayer> inherited)
        {
            Dictionary<TextStudioStackItemId, TextStudio2DLayer> ancestry = new Dictionary<TextStudioStackItemId, TextStudio2DLayer>();
            foreach (TextStudio2DLayer layer in inherited) ancestry[layer.ItemId] = layer;
            foreach (TextStudio2DLayer layer in local) ancestry[layer.ItemId] = layer;
            HashSet<TextStudioStackItemId> soloDomains = new HashSet<TextStudioStackItemId>();
            foreach (TextStudio2DLayer layer in local)
            {
                if (!layer.IsEnabled || !layer.IsSolo) continue;
                soloDomains.Add(TextStudioAppearanceDomains.Domain(in layer, ancestry));
            }
            for (int i = 0; i < inherited.Count; i++)
            {
                TextStudio2DLayer layer = inherited[i];
                if (layer.IsSolo && soloDomains.Contains(TextStudioAppearanceDomains.Domain(in layer, ancestry)))
                    inherited[i] = layer.WithState(layer.IsEnabled);
            }
        }

        static bool[] ResolveActiveLayers(List<TextStudio2DLayer> layers)
        {
            int soloIndex = -1;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].IsEnabled && layers[i].IsSolo)
                {
                    soloIndex = i;
                    break;
                }
            }

            bool[] active = new bool[layers.Count];
            Dictionary<TextStudioStackItemId, int> indices = new Dictionary<TextStudioStackItemId, int>();
            HashSet<TextStudioStackItemId> soloGroups = new HashSet<TextStudioStackItemId>();
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].ItemId.IsValid && !indices.ContainsKey(layers[i].ItemId))
                    indices.Add(layers[i].ItemId, i);
                active[i] = layers[i].IsEnabled &&
                            (soloIndex < 0 || i == soloIndex);
                if (active[i] && layers[i].Kind == TextStudio2DLayerKind.Group)
                    soloGroups.Add(layers[i].ItemId);
            }

            if (soloIndex < 0)
                return active;

            if (soloGroups.Count > 0)
            {
                for (int i = 0; i < layers.Count; i++)
                {
                    if (!active[i] && layers[i].IsEnabled &&
                        HasAncestorInSet(i, layers, indices, soloGroups))
                    {
                        active[i] = true;
                    }
                }
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (!active[i])
                    continue;
                IncludeDependency(layers[i].ParentId, layers, indices, active);
                IncludeDependency(layers[i].MaskId, layers, indices, active);
                if (layers[i].Kind == TextStudio2DLayerKind.Mask)
                {
                    IncludeDependency(
                        layers[i].Mask.SourceItemId,
                        layers,
                        indices,
                        active);
                }
            }
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].SurfaceOwnerId.IsValid && indices.TryGetValue(layers[i].SurfaceOwnerId, out int owner))
                    active[i] = layers[i].IsEnabled && active[owner];
            }
            return active;
        }

        static bool HasAncestorInSet(
            int layerIndex,
            List<TextStudio2DLayer> layers,
            Dictionary<TextStudioStackItemId, int> indices,
            HashSet<TextStudioStackItemId> ancestors)
        {
            TextStudioStackItemId parentId = layers[layerIndex].ParentId;
            for (int depth = 0; depth < layers.Count && parentId.IsValid; depth++)
            {
                if (ancestors.Contains(parentId))
                    return true;
                if (!indices.TryGetValue(parentId, out int parentIndex))
                    return false;
                parentId = layers[parentIndex].ParentId;
            }
            return false;
        }

        static void IncludeDependency(
            TextStudioStackItemId itemId,
            List<TextStudio2DLayer> layers,
            Dictionary<TextStudioStackItemId, int> indices,
            bool[] active)
        {
            HashSet<int> visited = new HashSet<int>();
            IncludeDependencyCore(
                itemId,
                layers,
                indices,
                active,
                visited);
        }

        static void IncludeDependencyCore(
            TextStudioStackItemId itemId,
            List<TextStudio2DLayer> layers,
            Dictionary<TextStudioStackItemId, int> indices,
            bool[] active,
            HashSet<int> visited)
        {
            if (!itemId.IsValid || !indices.TryGetValue(itemId, out int index))
                return;
            if (!visited.Add(index))
                return;
            TextStudio2DLayer dependency = layers[index];
            if (dependency.IsEnabled)
                active[index] = true;
            IncludeDependencyCore(
                dependency.ParentId,
                layers,
                indices,
                active,
                visited);
            IncludeDependencyCore(
                dependency.MaskId,
                layers,
                indices,
                active,
                visited);
            if (dependency.Kind == TextStudio2DLayerKind.Mask)
            {
                IncludeDependencyCore(
                    dependency.Mask.SourceItemId,
                    layers,
                    indices,
                    active,
                    visited);
            }
        }

        static Dictionary<TextStudioStackItemId, int> BuildSourceIndex(
            List<TextStudio2DLayer> layers,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            Dictionary<TextStudioStackItemId, int> indices =
                new Dictionary<TextStudioStackItemId, int>();
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioStackItemId id = layers[i].ItemId;
                if (!id.IsValid)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "invalid-item-id",
                        i,
                        id,
                        "Every style row needs a canonical stable item ID.");
                    continue;
                }
                if (indices.ContainsKey(id))
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "duplicate-item-id",
                        i,
                        id,
                        "Style row IDs must be unique after inheritance is flattened.");
                    continue;
                }
                indices.Add(id, i);
            }
            return indices;
        }

        static void ValidateRelationships(
            List<TextStudio2DLayer> layers,
            Dictionary<TextStudioStackItemId, int> indices,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudio2DLayer layer = layers[i];
                if (layer.Kind == TextStudio2DLayerKind.ColorAdjustment &&
                    (layer.MaskId.IsValid || layer.BlendMode != TextStudio2DBlendMode.Normal))
                {
                    AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "adjustment-compositing", i, layer.ItemId,
                        "Color Adjustment selects source contributions. Use Composite Adjustment for a spatial mask or blend mode.");
                }
                if (layer.ParentId.IsValid)
                {
                    if (!indices.TryGetValue(layer.ParentId, out int parent))
                    {
                        AddMissingRelationship(diagnostics, i, layer.ItemId, "parent");
                    }
                    else if (parent == i || layers[parent].Kind != TextStudio2DLayerKind.Group)
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "invalid-parent",
                            i,
                            layer.ItemId,
                            "A parent must reference a different group row.");
                    }
                }
                if (layer.MaskId.IsValid)
                {
                    if (!indices.TryGetValue(layer.MaskId, out int mask))
                    {
                        AddMissingRelationship(diagnostics, i, layer.ItemId, "mask");
                    }
                    else if (layers[mask].Kind != TextStudio2DLayerKind.Mask)
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "invalid-mask",
                            i,
                            layer.ItemId,
                            "A mask relationship must reference a mask row.");
                    }
                }
                if (layer.MaskId == layer.ItemId)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "self-mask",
                        i,
                        layer.ItemId,
                        "A style row cannot mask itself.");
                }
                if (layer.Kind == TextStudio2DLayerKind.Mask)
                {
                    TextStudioStackItemId sourceId = layer.Mask.SourceItemId;
                    if (!sourceId.IsValid || !indices.ContainsKey(sourceId))
                    {
                        AddMissingRelationship(
                            diagnostics,
                            i,
                            layer.ItemId,
                            "mask source");
                    }
                    else if (sourceId == layer.ItemId)
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "self-mask-source",
                            i,
                            layer.ItemId,
                            "A mask row cannot use itself as its source.");
                    }
                    else if (layers[indices[sourceId]].SurfaceOwnerId.IsValid)
                    {
                        AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error, "surface-mask-source", i, layer.ItemId,
                            "A mask must reference the surface owner rather than one of its treatments.");
                    }
                    else if (layers[indices[sourceId]].Kind == TextStudio2DLayerKind.Mask ||
                             TextStudio2DColorAdjustmentUtility.IsAdjustment(layers[indices[sourceId]].Kind))
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "invalid-mask-source",
                            i,
                            layer.ItemId,
                            "A mask source must reference a paint layer or group that produces coverage.");
                    }
                }
            }

            byte[] states = new byte[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                VisitRelationships(i, layers, indices, states, diagnostics);
        }

        static void VisitRelationships(
            int index,
            List<TextStudio2DLayer> layers,
            Dictionary<TextStudioStackItemId, int> indices,
            byte[] states,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (states[index] == 2)
                return;
            if (states[index] == 1)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "relationship-cycle",
                    index,
                    layers[index].ItemId,
                    "The parent and mask relationship graph contains a cycle.");
                return;
            }
            states[index] = 1;
            TextStudio2DLayer layer = layers[index];
            TextStudioStackItemId parentId = layer.ParentId;
            if (parentId.IsValid && indices.TryGetValue(parentId, out int parent))
                VisitRelationships(parent, layers, indices, states, diagnostics);
            if (layer.MaskId.IsValid && indices.TryGetValue(layer.MaskId, out int mask))
                VisitRelationships(mask, layers, indices, states, diagnostics);
            TextStudioStackItemId maskSourceId = layer.Kind == TextStudio2DLayerKind.Mask
                ? layer.Mask.SourceItemId
                : default;
            if (maskSourceId.IsValid &&
                indices.TryGetValue(maskSourceId, out int maskSource))
            {
                VisitRelationships(maskSource, layers, indices, states, diagnostics);
            }
            states[index] = 2;
        }

        static TextStudio2DLayer SanitizeLayer(
            TextStudio2DLayer layer,
            int index,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (!float.IsFinite(layer.Opacity) || layer.Opacity < 0f || layer.Opacity > 1f)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "opacity-range",
                    index,
                    layer.ItemId,
                    "Layer opacity must be finite and between zero and one.");
                layer = layer.WithCompositing(
                    float.IsFinite(layer.Opacity) ? Mathf.Clamp01(layer.Opacity) : 1f,
                    layer.BlendMode);
            }
            ValidateFiniteParameters(in layer, index, diagnostics);
            return layer;
        }

        static bool ValidateLayerEnums(
            in TextStudio2DLayer layer,
            int index,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            bool valid = (uint)layer.Kind <= (uint)TextStudio2DLayerKind.CompositeAdjustment &&
                         (uint)layer.BlendMode <= (uint)TextStudio2DBlendMode.Max &&
                         (uint)layer.ExecutionPath <=
                         (uint)TextStudio2DExecutionPath.HeroCompositor &&
                         (uint)layer.QualityTier <= (uint)TextStudio2DQualityTier.Hero &&
                         (uint)layer.GroupOrder <= (uint)TextStudio2DGroupOrder.ByCharacter &&
                         (uint)layer.SharedSurfaceOrder <= (uint)TextStudio2DSharedSurfaceOrder.AfterCharacters &&
                         (uint)layer.SourceApplicability <= (uint)TextStudio2DSourceApplicability.ColorOnly;
            if (layer.Kind == TextStudio2DLayerKind.Stroke)
            {
                valid &= (uint)layer.Stroke.Join <=
                         (uint)TextStudio2DStrokeJoin.Bevel &&
                         (uint)layer.Stroke.Units <= (uint)TextStudio2DUnit.Pixels;
            }
            if (layer.Kind == TextStudio2DLayerKind.Fill)
                valid &= (uint)layer.Fill.Units <= (uint)TextStudio2DUnit.Pixels &&
                    (uint)layer.Fill.ColorFace <= (uint)TextStudio2DColorFaceMode.SilhouetteFill;
            if (layer.Kind == TextStudio2DLayerKind.InnerShadow ||
                layer.Kind == TextStudio2DLayerKind.OuterShadow)
            {
                valid &= (uint)layer.Shadow.Rendering <=
                         (uint)TextStudio2DSoftEffectRendering.TextWideBlur &&
                         (uint)layer.Shadow.Units <= (uint)TextStudio2DUnit.Pixels;
            }
            if (layer.Kind == TextStudio2DLayerKind.InnerGlow ||
                layer.Kind == TextStudio2DLayerKind.OuterGlow)
            {
                valid &= (uint)layer.Glow.Rendering <=
                         (uint)TextStudio2DSoftEffectRendering.TextWideBlur &&
                         (uint)layer.Glow.Units <= (uint)TextStudio2DUnit.Pixels;
            }
            if (layer.Kind == TextStudio2DLayerKind.Bevel)
                valid &= (uint)layer.Bevel.Units <= (uint)TextStudio2DUnit.Pixels;
            if (layer.Kind == TextStudio2DLayerKind.FauxDepth)
            {
                valid &= (uint)layer.FauxDepth.PaintMapping <=
                         (uint)TextStudio2DFauxDepthPaintMapping.DepthProgress &&
                         (uint)layer.FauxDepth.Units <= (uint)TextStudio2DUnit.Pixels;
            }
            if (layer.Kind == TextStudio2DLayerKind.Decoration)
            {
                valid &= (uint)layer.Decoration.Kind <=
                         (uint)TextStudio2DDecorationKind.Border &&
                         (uint)layer.Decoration.VerticalAnchor <=
                         (uint)TextStudio2DDecorationVerticalAnchor.LineBox &&
                         (uint)layer.Decoration.LeftEnd <=
                         (uint)TextStudio2DDecorationEndStyle.Notch &&
                         (uint)layer.Decoration.RightEnd <=
                         (uint)TextStudio2DDecorationEndStyle.Notch &&
                         (uint)layer.Decoration.Shape <=
                         (uint)TextStudio2DDecorationShape.GlyphSilhouette &&
                         (uint)layer.Decoration.Topology <=
                         (uint)TextStudio2DDecorationTopology.PerFragment &&
                         layer.Decoration.Line.IsValid;
            }
            if (layer.Kind == TextStudio2DLayerKind.MaterialResponse)
            {
                valid &= TextStudio2DTreatmentCatalog.TryGet(
                    layer.MaterialResponse.Kind,
                    out _);
            }
            if (layer.Kind == TextStudio2DLayerKind.GraphicEffect)
            {
                valid &= TextStudioVisualPrimitiveCatalog.TryGet(
                             layer.GraphicEffect.Kind,
                             out _) &&
                         (uint)layer.GraphicEffect.Mapping <=
                         (uint)TextStudio2DMappingSpace.Range;
            }
            if (layer.Kind == TextStudio2DLayerKind.AnimatedEffect)
            {
                valid &= TextStudioVisualPrimitiveCatalog.TryGet(
                             layer.AnimatedEffect.Kind,
                             out _) &&
                         (uint)layer.AnimatedEffect.Mapping <=
                         (uint)TextStudio2DMappingSpace.Range &&
                         (uint)layer.AnimatedEffect.ReducedMotionBehavior <=
                         (uint)TextStudioReducedMotionBehavior.Disable;
            }
            if (valid)
                return true;

            AddDiagnostic(
                diagnostics,
                TextStudioStackDiagnosticSeverity.Error,
                "invalid-enum",
                index,
                layer.ItemId,
                "Layer kind, compositing, quality, path, and family options must use supported values.");
            return false;
        }

        static void ValidateFiniteParameters(
            in TextStudio2DLayer layer,
            int index,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            bool valid = true;
            switch (layer.Kind)
            {
                case TextStudio2DLayerKind.Fill:
                    valid = float.IsFinite(layer.Fill.Dilation) &&
                            IsFiniteNonNegative(layer.Fill.Softness);
                    break;
                case TextStudio2DLayerKind.Stroke:
                    valid = IsFiniteNonNegative(layer.Stroke.Width) &&
                            IsFiniteRange(layer.Stroke.Alignment, -1f, 1f) &&
                            IsFiniteNonNegative(layer.Stroke.Softness) &&
                            float.IsFinite(layer.Stroke.MiterLimit) &&
                            layer.Stroke.MiterLimit >= 1f && float.IsFinite(layer.Stroke.DashOffset) &&
                            (uint)layer.Stroke.DashCap <= (uint)TextStudio2DLineCap.Square;
                    for (int dashIndex = 0; dashIndex < layer.Stroke.DashCount; dashIndex++)
                    {
                        float dash = layer.Stroke.GetDash(dashIndex);
                        valid &= float.IsFinite(dash) && dash > 0f;
                    }
                    break;
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                    valid = IsFinite(layer.Shadow.Offset) &&
                            IsFiniteNonNegative(layer.Shadow.Blur) &&
                            IsFiniteNonNegative(layer.Shadow.Spread) &&
                            (layer.Kind == TextStudio2DLayerKind.OuterShadow ||
                             layer.Shadow.Rendering ==
                             TextStudio2DSoftEffectRendering.DistanceField);
                    break;
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    valid = IsFiniteNonNegative(layer.Glow.Radius) &&
                            IsFiniteNonNegative(layer.Glow.Spread) &&
                            (layer.Kind == TextStudio2DLayerKind.OuterGlow ||
                             layer.Glow.Rendering ==
                             TextStudio2DSoftEffectRendering.DistanceField);
                    break;
                case TextStudio2DLayerKind.Bevel:
                    valid = IsFiniteNonNegative(layer.Bevel.Width) &&
                            IsFiniteNonNegative(layer.Bevel.Softness) &&
                            float.IsFinite(layer.Bevel.Depth) &&
                            float.IsFinite(layer.Bevel.LightAngle) &&
                            float.IsFinite(layer.Bevel.LightElevation) &&
                            IsFinite(layer.Bevel.Highlight) &&
                            IsFinite(layer.Bevel.Shadow);
                    break;
                case TextStudio2DLayerKind.FauxDepth:
                    valid = IsFinite(layer.FauxDepth.Offset) &&
                            IsFiniteRange(layer.FauxDepth.Taper, -0.95f, 0.95f) &&
                            IsFiniteUnit(layer.FauxDepth.Falloff) &&
                            IsFiniteNonNegative(layer.FauxDepth.Softness) &&
                            IsFiniteNonNegative(layer.FauxDepth.SilhouetteOutset) &&
                            IsFiniteUnit(layer.FauxDepth.EndFade) &&
                            float.IsFinite(layer.FauxDepth.SideLightAngle) &&
                            IsFiniteUnit(layer.FauxDepth.SideLightStrength) &&
                            IsFiniteUnit(layer.FauxDepth.FrontBevelWidth) &&
                            IsFiniteUnit(layer.FauxDepth.FrontBevelStrength) &&
                            IsFiniteUnit(layer.FauxDepth.FarCapShade);
                    break;
                case TextStudio2DLayerKind.Decoration:
                    TextStudio2DDecorationSpec decoration = layer.Decoration;
                    valid = (uint)decoration.BackplateVisibility <= (uint)TextStudio2DBackplateVisibility.AlwaysVisible &&
                            IsFiniteNonNegative(decoration.GlyphPadding) &&
                            IsFiniteNonNegative(decoration.GlyphBridgeRadius) &&
                            (decoration.Shape != TextStudio2DDecorationShape.GlyphSilhouette ||
                             decoration.Kind >= TextStudio2DDecorationKind.Background) &&
                            IsFiniteNonNegative(layer.Decoration.Thickness) &&
                            float.IsFinite(decoration.Offset) &&
                            IsFinite(decoration.Padding) &&
                            IsFiniteNonNegative(decoration.CornerRadius) &&
                            IsFiniteNonNegative(decoration.PlateHeight) &&
                            IsFiniteNonNegative(decoration.PlateWidth) &&
                            IsFiniteNonNegative(decoration.CapSize) &&
                            IsFiniteNonNegative(decoration.CornerRadii.x) &&
                            IsFiniteNonNegative(decoration.CornerRadii.y) &&
                            IsFiniteNonNegative(decoration.CornerRadii.z) &&
                            IsFiniteNonNegative(decoration.CornerRadii.w) &&
                            IsFiniteRange(decoration.SquirclePower, 2f, 16f) &&
                            decoration.PolygonSides >= 3 &&
                            decoration.PolygonSides <= 8 &&
                            IsFiniteRange(decoration.StarInset, 0.05f, 0.95f) &&
                            (decoration.Shape != TextStudio2DDecorationShape.CustomPolygon ||
                             decoration.CustomPointCount >= 3 &&
                             decoration.CustomPointCount <= 8);
                    for (int pointIndex = 0;
                         valid && pointIndex < decoration.CustomPointCount;
                         pointIndex++)
                    {
                        valid &= IsFinite(decoration.GetCustomPoint(pointIndex));
                    }
                    break;
                case TextStudio2DLayerKind.Mask:
                    valid = IsFiniteNonNegative(layer.Mask.Feather);
                    break;
                case TextStudio2DLayerKind.MaterialResponse:
                    TextStudio2DMaterialResponseSpec response = layer.MaterialResponse;
                    valid = IsFiniteNonNegative(response.Width) &&
                            IsFiniteNonNegative(response.Softness) &&
                            IsFiniteNonNegative(response.Depth) &&
                            float.IsFinite(response.LightAngle) &&
                            float.IsFinite(response.LightElevation) &&
                            IsFiniteUnit(response.Roughness) &&
                            IsFiniteUnit(response.Metallic) &&
                            IsFiniteUnit(response.Clearcoat) &&
                            float.IsFinite(response.Anisotropy) &&
                            response.Anisotropy >= -1f &&
                            response.Anisotropy <= 1f &&
                            IsFiniteUnit(response.Iridescence) &&
                            IsFiniteUnit(response.Sparkle) &&
                            float.IsFinite(response.Emission) &&
                            response.Emission >= 0f &&
                            response.Emission <= 8f &&
                            IsFinite(response.Highlight) &&
                            IsFinite(response.Shadow) &&
                            IsFinite(response.Environment);
                    break;
                case TextStudio2DLayerKind.GraphicEffect:
                    TextStudio2DGraphicEffectSpec graphic = layer.GraphicEffect;
                    valid = TextStudioVisualPrimitiveMath
                        .AreGraphicParametersValid(in graphic);
                    break;
                case TextStudio2DLayerKind.AnimatedEffect:
                    TextStudio2DAnimatedEffectSpec animated = layer.AnimatedEffect;
                    valid = TextStudioVisualPrimitiveMath.AreAnimatedParametersValid(
                        in animated);
                    break;
                case TextStudio2DLayerKind.ColorAdjustment:
                case TextStudio2DLayerKind.CompositeAdjustment:
                    TextStudio2DColorAdjustmentSpec filter = layer.ColorAdjustment;
                    valid = filter.AreParametersValid && IsFinite(filter.Red) &&
                            IsFinite(filter.Green) &&
                            IsFinite(filter.Blue) &&
                            IsFinite(filter.Alpha) &&
                            IsFinite(filter.Offset) &&
                            IsFiniteUnit(filter.Strength);
                    break;
            }

            if (!valid)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "invalid-parameters",
                    index,
                    layer.ItemId,
                    "Layer dimensions and quality values must be finite and within their documented ranges.");
            }
        }

        static TextStudio2DCompiledSelector CompileSurfaceSelector(in TextStudio2DLayer layer,
            List<TextStudio2DLayer> layers, Dictionary<TextStudioStackItemId, int> sourceIndices,
            int itemIndex, List<TextStudioStackDiagnostic> diagnostics)
        {
            TextStudio2DCompiledSelector selector = CompileSelector(layer.Selector, itemIndex, layer.ItemId, diagnostics);

            TextStudioStackItemId parent = layer.ParentId;
            for (int depth = 0; depth < layers.Count && parent.IsValid && sourceIndices.TryGetValue(parent, out int parentIndex); depth++)
            {
                TextStudio2DLayer ancestor = layers[parentIndex];
                TextStudio2DCompiledSelector scope = CompileSelector(ancestor.Selector, parentIndex, ancestor.ItemId, diagnostics);
                parent = ancestor.ParentId;
                if (scope.IsTrivialAll) continue;
                if (selector.IsTrivialAll) { selector = scope; continue; }
                TextStudioApplicationSelectorNode[] nodes = new TextStudioApplicationSelectorNode[selector.NodeCount + scope.NodeCount + 1];
                for (int index = 0; index < selector.NodeCount; index++) nodes[index] = selector.GetNode(index);
                for (int index = 0; index < scope.NodeCount; index++) nodes[selector.NodeCount + index] = scope.GetNode(index).WithNodeOffset(selector.NodeCount);
                nodes[nodes.Length - 1] = TextStudioApplicationSelectorNode.And(selector.Root, selector.NodeCount + scope.Root);
                selector = CompileSelector(TextStudioApplicationSelector.Create(nodes, nodes.Length - 1), itemIndex, layer.ItemId, diagnostics);
            }
            return selector;
        }

        static TextStudio2DCompiledSelector CompileSelector(
            TextStudioApplicationSelector selector,
            int itemIndex,
            TextStudioStackItemId itemId,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (!TextStudioSelectorCompiler.TryCompile(in selector,
                    TextStudioSelectorCapabilities.Appearance,
                    out TextStudioCompiledSelector shared, out string message, out string code))
            {
                AddDiagnostic(diagnostics, TextStudioStackDiagnosticSeverity.Error,
                    code, itemIndex, itemId, message);
                return new TextStudio2DCompiledSelector(
                    new[] { TextStudioApplicationSelectorNode.None() }, 0);
            }
            TextStudioApplicationSelectorNode[] nodes = new TextStudioApplicationSelectorNode[shared.NodeCount];
            for (int index = 0; index < nodes.Length; index++)
                nodes[index] = shared.GetNode(index);
            return new TextStudio2DCompiledSelector(nodes, shared.Root);
        }

        static void ValidateAnimatedHistoryCapacity(
            List<TextStudio2DCompiledOperation> operations,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            int historyCount = 0;
            for (int index = 0; index < operations.Count; index++)
            {
                TextStudio2DCompiledOperation operation = operations[index];
                if (operation.Kind != TextStudio2DLayerKind.AnimatedEffect ||
                    !TextStudio2DAnimatedEffectUtility.RequiresHistory(
                        operation.AnimatedEffect.Kind))
                {
                    continue;
                }
                historyCount++;
                if (historyCount <= 1)
                    continue;
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "history-capacity",
                    index,
                    operation.ItemId,
                    "A style can contain one temporal-history effect. Combine additional motion with analytic or spatial Hero effects.");
            }
        }

        static int CompilePaint(
            TextStudio2DPaint paint,
            int itemIndex,
            TextStudioStackItemId itemId,
            TextStudio2DCompileContext context,
            List<TextStudio2DGradientStop> gradientStops,
            List<TextStudio2DCompiledPaint> paints,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (!IsSupportedPaintKind(paint.Kind) ||
                (uint)paint.Mapping > (uint)TextStudio2DMappingSpace.Range ||
                (uint)paint.RangeMapping > (uint)TextStudio2DRangeMapping.EntireSelection ||
                (uint)paint.Spread > (uint)TextStudio2DGradientSpread.Mirror ||
                (uint)paint.TextureFit > (uint)TextStudio2DTextureFit.Tile ||
                (uint)paint.DistanceDomain >
                (uint)TextStudio2DDistanceDomain.DepthProgress)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "invalid-paint-enum",
                    itemIndex,
                    itemId,
                    "Paint kind, mapping space, and gradient spread must use supported values.");
            }
            TextStudio2DGradientStop[] compiledStops =
                Array.Empty<TextStudio2DGradientStop>();
            if (IsStopGradient(paint.Kind))
            {
                TextStudio2DGradientStop[] stops = paint.CloneGradientStops();
                if (stops.Length < 2)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Error,
                        "gradient-stops",
                        itemIndex,
                        itemId,
                        "Gradient paints require at least two color stops.");
                    stops = new[]
                    {
                        new TextStudio2DGradientStop(0f, paint.Color),
                        new TextStudio2DGradientStop(1f, paint.Color)
                    };
                }
                StableSortGradientStops(stops);
                compiledStops = new TextStudio2DGradientStop[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    if (!float.IsFinite(stops[i].Position))
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "gradient-position",
                            itemIndex,
                            itemId,
                            "Gradient stop positions must be finite.");
                    }
                    if (!IsFinite(stops[i].Color))
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "gradient-color",
                            itemIndex,
                            itemId,
                            "Gradient stop colors must be finite.");
                    }
                    compiledStops[i] = new TextStudio2DGradientStop(
                        float.IsFinite(stops[i].Position)
                            ? Mathf.Clamp01(stops[i].Position)
                            : 0f,
                        stops[i].Color);
                }
                if (compiledStops.Length > context.MaxGradientStops)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Information,
                        "gradient-hero-path",
                        itemIndex,
                        itemId,
                        "This gradient exceeds the fused stop budget and requires a higher-capability path.");
                }
            }
            else if (paint.Kind == TextStudio2DPaintKind.FourCornerGradient)
            {
                TextStudio2DFourCornerGradient corners = paint.FourCornerGradient;
                Color[] colors =
                {
                    corners.TopLeft,
                    corners.TopRight,
                    corners.BottomLeft,
                    corners.BottomRight
                };
                compiledStops = new TextStudio2DGradientStop[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    if (!IsFinite(colors[i]))
                    {
                        AddDiagnostic(
                            diagnostics,
                            TextStudioStackDiagnosticSeverity.Error,
                            "four-corner-gradient-color",
                            itemIndex,
                            itemId,
                            "Four-corner gradient colors must be finite.");
                    }
                    compiledStops[i] = new TextStudio2DGradientStop(
                        i / (colors.Length - 1f),
                        colors[i]);
                }
            }
            if ((paint.Kind == TextStudio2DPaintKind.Texture ||
                 paint.Kind == TextStudio2DPaintKind.Pattern) &&
                paint.Texture == null)
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "missing-texture",
                    itemIndex,
                    itemId,
                    "Texture and pattern paints require a texture asset.");
            }
            if (!IsFinite(paint.Color) || !IsFinite(paint.Center) ||
                !IsFinite(paint.Scale) || !IsFinite(paint.Offset) ||
                !float.IsFinite(paint.Angle) || !float.IsFinite(paint.NoiseScale) ||
                !float.IsFinite(paint.NoiseDetail) ||
                !IsFinite(paint.DistanceRange) ||
                paint.Kind == TextStudio2DPaintKind.DistanceGradient &&
                Mathf.Abs(paint.DistanceRange.y - paint.DistanceRange.x) <= .000001f ||
                paint.Kind == TextStudio2DPaintKind.ProceduralNoise &&
                (paint.NoiseScale < 0.01f ||
                 paint.NoiseScale > 4096f ||
                 paint.NoiseDetail < 0f ||
                 paint.NoiseDetail > 1f) ||
                (IsGradient(paint.Kind) ||
                 paint.Kind == TextStudio2DPaintKind.Texture ||
                 paint.Kind == TextStudio2DPaintKind.Pattern) &&
                (Mathf.Abs(paint.Scale.x) <= 0.000001f ||
                 Mathf.Abs(paint.Scale.y) <= 0.000001f))
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "invalid-paint",
                    itemIndex,
                    itemId,
                    "Paint mapping values must be finite; procedural noise scale must be between 0.01 and 4,096 and detail between zero and one.");
            }

            TextStudio2DCompiledPaint candidate = new TextStudio2DCompiledPaint(
                paint,
                0,
                compiledStops.Length);
            for (int i = 0; i < paints.Count; i++)
            {
                TextStudio2DCompiledPaint existingPaint = paints[i];
                if (PaintsMatch(
                        in existingPaint,
                        in candidate,
                        gradientStops,
                        compiledStops))
                {
                    return i;
                }
            }

            int stopStart = gradientStops.Count;
            gradientStops.AddRange(compiledStops);
            int paintIndex = paints.Count;
            paints.Add(new TextStudio2DCompiledPaint(
                paint,
                stopStart,
                compiledStops.Length));
            return paintIndex;
        }

        static bool PaintsMatch(
            in TextStudio2DCompiledPaint first,
            in TextStudio2DCompiledPaint second,
            List<TextStudio2DGradientStop> compiledStops,
            TextStudio2DGradientStop[] candidateStops)
        {
            if (first.Kind != second.Kind ||
                first.Mapping != second.Mapping ||
                first.RangeMapping != second.RangeMapping ||
                first.Spread != second.Spread ||
                !first.Color.Equals(second.Color) ||
                !first.Angle.Equals(second.Angle) ||
                !first.Center.Equals(second.Center) ||
                !first.Scale.Equals(second.Scale) ||
                !first.Offset.Equals(second.Offset) ||
                first.Texture != second.Texture ||
                !string.Equals(first.ResourceId, second.ResourceId, StringComparison.Ordinal) ||
                first.TextureFit != second.TextureFit ||
                first.DistanceDomain != second.DistanceDomain ||
                !first.DistanceRange.Equals(second.DistanceRange) ||
                !first.NoiseScale.Equals(second.NoiseScale) ||
                !first.NoiseDetail.Equals(second.NoiseDetail) ||
                first.NoiseSeed != second.NoiseSeed ||
                first.GradientStopCount != candidateStops.Length)
            {
                return false;
            }

            for (int i = 0; i < candidateStops.Length; i++)
            {
                TextStudio2DGradientStop existing =
                    compiledStops[first.GradientStopStart + i];
                if (!existing.Position.Equals(candidateStops[i].Position) ||
                    !existing.Color.Equals(candidateStops[i].Color))
                {
                    return false;
                }
            }
            return true;
        }

        static void StableSortGradientStops(TextStudio2DGradientStop[] stops)
        {
            for (int i = 1; i < stops.Length; i++)
            {
                TextStudio2DGradientStop value = stops[i];
                int insertion = i;
                while (insertion > 0 && stops[insertion - 1].Position > value.Position)
                {
                    stops[insertion] = stops[insertion - 1];
                    insertion--;
                }
                stops[insertion] = value;
            }
        }

        static TextStudio2DExecutionPath DetermineMinimumPath(
            in TextStudio2DLayer layer,
            in TextStudio2DCompiledSelector selector,
            TextStudio2DCompileContext context,
            ref TextStudio2DStyleRequirements requirements)
        {
            requirements |= TextStudio2DStyleRequirements.DistanceField |
                            TextStudio2DStyleRequirements.GlyphStyleIndices;
            bool requiresSourceVertexColors =
                layer.ParticipatesInAnimatedTint ||
                SelectorUsesNodeKind(
                    in selector,
                    TextStudioSelectorNodeKind.SourceColor);
            if (requiresSourceVertexColors)
                requirements |= TextStudio2DStyleRequirements.SourceVertexColors;
            if (!selector.IsTrivialAll)
                requirements |= TextStudio2DStyleRequirements.SelectorEvaluation;

            if (layer.Kind == TextStudio2DLayerKind.Stroke && !layer.SurfaceOwnerId.IsValid)
            {
                if (layer.Stroke.Join != TextStudio2DStrokeJoin.Round)
                {
                    requirements |= TextStudio2DStyleRequirements.ExactStrokeJoin;
                }
                if (layer.Stroke.DashCount > 0)
                    requirements |= TextStudio2DStyleRequirements.ContourDash;
            }

            bool usesPaint = UsesPaint(layer.Kind);
            TextStudio2DPaint paint = layer.Paint;
            TextStudio2DMappingSpace mapping = layer.Kind switch
            {
                TextStudio2DLayerKind.GraphicEffect => layer.GraphicEffect.Mapping,
                TextStudio2DLayerKind.AnimatedEffect => layer.AnimatedEffect.Mapping,
                _ => paint.Mapping
            };
            if (usesPaint)
            {
                if (IsGradient(paint.Kind))
                    requirements |= TextStudio2DStyleRequirements.GradientTable;
                if (paint.Kind == TextStudio2DPaintKind.Texture ||
                    paint.Kind == TextStudio2DPaintKind.Pattern)
                {
                    requirements |= TextStudio2DStyleRequirements.TextureTable;
                }
                if (paint.Kind == TextStudio2DPaintKind.ProceduralNoise)
                    requirements |= TextStudio2DStyleRequirements.ProceduralNoise;
            }
            if (usesPaint || layer.Kind == TextStudio2DLayerKind.GraphicEffect || layer.Kind == TextStudio2DLayerKind.AnimatedEffect)
            {
                if (mapping == TextStudio2DMappingSpace.Screen)
                    requirements |= TextStudio2DStyleRequirements.ScreenCoordinates;
                if (mapping == TextStudio2DMappingSpace.World)
                    requirements |= TextStudio2DStyleRequirements.WorldCoordinates;
                if (mapping == TextStudio2DMappingSpace.Object)
                    requirements |= TextStudio2DStyleRequirements.ObjectCoordinates;
            }
            if (layer.SurfaceOwnerId.IsValid)
            {
                if (layer.Kind == TextStudio2DLayerKind.Bevel || layer.Kind == TextStudio2DLayerKind.MaterialResponse)
                    requirements |= TextStudio2DStyleRequirements.MaterialResponse;
                return TextStudio2DExecutionPath.RetainedGeometry;
            }
            bool requiresGraphicEffect =
                layer.Kind == TextStudio2DLayerKind.GraphicEffect;
            if (requiresGraphicEffect)
            {
                requirements |= TextStudio2DStyleRequirements.GraphicEffect;
                if (TextStudio2DGraphicEffectUtility.UsesNoise(
                        layer.GraphicEffect.Kind))
                    requirements |= TextStudio2DStyleRequirements.ProceduralNoise;
            }
            bool requiresAnimatedEffect =
                layer.Kind == TextStudio2DLayerKind.AnimatedEffect;
            if (requiresAnimatedEffect)
            {
                TextStudio2DAnimatedEffectKind animatedKind =
                    layer.AnimatedEffect.Kind;
                requirements |= TextStudio2DStyleRequirements.AnimatedEffect;
                if (TextStudio2DAnimatedEffectUtility.UsesNoise(animatedKind))
                    requirements |= TextStudio2DStyleRequirements.ProceduralNoise;
                if (TextStudio2DAnimatedEffectUtility.RequiresPostProcess(animatedKind))
                    requirements |= TextStudio2DStyleRequirements.NeighborhoodSampling;
                if (TextStudio2DAnimatedEffectUtility.RequiresHistory(animatedKind))
                    requirements |= TextStudio2DStyleRequirements.TemporalHistory;
            }
            bool animatedRequiresHero = requiresAnimatedEffect &&
                (TextStudio2DAnimatedEffectUtility.RequiresPostProcess(
                     layer.AnimatedEffect.Kind) ||
                 !Has(
                     context.Capabilities,
                      TextStudio2DRendererCapabilities.AnimatedEffects) ||
                  ExceedsFusedFieldBounds(in layer));
            bool requiresColorAdjustment = TextStudio2DColorAdjustmentUtility.IsAdjustment(layer.Kind);
            if (requiresColorAdjustment)
            {
                requirements |= TextStudio2DStyleRequirements.ColorAdjustment;
                if (layer.Kind == TextStudio2DLayerKind.CompositeAdjustment)
                    requirements |= TextStudio2DStyleRequirements.NeighborhoodSampling;
            }
            bool expandedFieldRequiresHero =
                layer.Kind != TextStudio2DLayerKind.FauxDepth &&
                layer.Kind != TextStudio2DLayerKind.Decoration &&
                ExceedsFusedFieldBounds(in layer);
            bool softEffectRequiresHero =
                TextStudio2DSoftEffectUtility.RequiresTextWideBlur(in layer);
            if (softEffectRequiresHero)
                requirements |= TextStudio2DStyleRequirements.NeighborhoodSampling;
            bool requiresMaterialResponse =
                layer.Kind == TextStudio2DLayerKind.Bevel ||
                layer.Kind == TextStudio2DLayerKind.MaterialResponse;
            if (requiresMaterialResponse)
                requirements |= TextStudio2DStyleRequirements.MaterialResponse;
            if (IsAdvancedBlend(layer.BlendMode))
                requirements |= TextStudio2DStyleRequirements.AdvancedBlend;
            if (layer.ParentId.IsValid ||
                layer.MaskId.IsValid ||
                layer.Kind == TextStudio2DLayerKind.Mask ||
                layer.Kind == TextStudio2DLayerKind.Group)
                requirements |= TextStudio2DStyleRequirements.MaskGraph;

            if (layer.Kind == TextStudio2DLayerKind.Group ||
                layer.Kind == TextStudio2DLayerKind.Mask ||
                layer.Kind == TextStudio2DLayerKind.Decoration &&
                layer.Decoration.Topology == TextStudio2DDecorationTopology.ConnectedRange ||
                layer.Kind == TextStudio2DLayerKind.CompositeAdjustment ||
                animatedRequiresHero ||
                layer.Kind == TextStudio2DLayerKind.FauxDepth && layer.FauxDepth.Units == TextStudio2DUnit.Pixels ||
                expandedFieldRequiresHero ||
                softEffectRequiresHero ||
                layer.ParentId.IsValid ||
                layer.MaskId.IsValid ||
                IsAdvancedBlend(layer.BlendMode) ||
                mapping == TextStudio2DMappingSpace.Screen &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.ScreenMapping) ||
                mapping == TextStudio2DMappingSpace.World &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.WorldMapping) ||
                requiresSourceVertexColors &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.VertexColors) ||
                !selector.IsTrivialAll &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.PerGlyphSelectors) ||
                requiresMaterialResponse &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.MaterialResponse) ||
                requiresGraphicEffect &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.GraphicEffects))
            {
                return TextStudio2DExecutionPath.HeroCompositor;
            }

            if (layer.Kind == TextStudio2DLayerKind.Decoration ||
                layer.Kind == TextStudio2DLayerKind.Stroke && (layer.Stroke.DashCount > 0 || layer.Stroke.Join != TextStudio2DStrokeJoin.Round) ||
                layer.Kind == TextStudio2DLayerKind.FauxDepth &&
                RequiresRetainedFauxDepth(in layer))
            {
                return TextStudio2DExecutionPath.RetainedGeometry;
            }

            if (usesPaint &&
                ((paint.Kind == TextStudio2DPaintKind.Texture ||
                 paint.Kind == TextStudio2DPaintKind.Pattern) &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.TextureSampling) ||
                paint.Kind == TextStudio2DPaintKind.ProceduralNoise &&
                !Has(context.Capabilities, TextStudio2DRendererCapabilities.ProceduralNoise) ||
                IsGradient(paint.Kind) &&
                (!Has(context.Capabilities, TextStudio2DRendererCapabilities.Gradients) ||
                 paint.GradientStopCount > context.MaxGradientStops)))
            {
                return TextStudio2DExecutionPath.HeroCompositor;
            }

            return TextStudio2DExecutionPath.FusedField;
        }

        static void AddStrokeCapabilityDiagnostics(
            List<TextStudio2DCompiledOperation> operations,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            for (int index = 0; index < operations.Count; index++)
            {
                TextStudio2DCompiledOperation operation = operations[index];
                if (operation.Kind != TextStudio2DLayerKind.Stroke || operation.SurfaceOwnerOperationIndex >= 0)
                    continue;
                if (operation.Stroke.Join != TextStudio2DStrokeJoin.Round)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Information,
                        "stroke-join-source",
                        index,
                        operation.ItemId,
                        "Miter and Bevel joins use prepared font contours. Prepare Appearance Data for every font used by this layer.");
                }
                if (operation.Stroke.DashCount > 0)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Information,
                        "stroke-dash-source",
                        index,
                        operation.ItemId,
                        "Contour dashes use prepared font outlines and measured path distance. Prepare Appearance Data for every font used by this layer.");
                }
            }
        }

        static TextStudio2DSourceFieldRequest BuildSourceFieldRequest(
            TextStudio2DSourceFieldQuality requestedQuality,
            List<TextStudio2DCompiledOperation> operations)
        {
            TextStudio2DQualityTier highestTier = TextStudio2DQualityTier.Responsive;
            bool requiresMultiDistance = false;
            float minimumSafeDistance = 0f;
            for (int index = 0; index < operations.Count; index++)
            {
                TextStudio2DCompiledOperation operation = operations[index];
                if (operation.Kind == TextStudio2DLayerKind.Decoration || operation.SurfaceOwnerOperationIndex >= 0 ||
                    operation.Kind == TextStudio2DLayerKind.Stroke && (operation.Stroke.Join != TextStudio2DStrokeJoin.Round || operation.Stroke.DashCount > 0)) continue;
                if ((int)operation.QualityTier > (int)highestTier)
                    highestTier = operation.QualityTier;
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth &&
                    operation.FauxDepth.Units == TextStudio2DUnit.Em)
                {
                    if (!operation.FauxDepth.HasDepth)
                        continue;
                    minimumSafeDistance = Mathf.Max(
                        minimumSafeDistance,
                        Mathf.Max(0f, operation.FauxDepth.SilhouetteOutset) +
                        Mathf.Max(0f, operation.FauxDepth.Softness));
                    continue;
                }
                if (!UsesEmFieldDistance(in operation))
                    continue;
                TextStudio2DBoundsExpansion expansion = operation.BoundsExpansion;
                minimumSafeDistance = Mathf.Max(
                    minimumSafeDistance,
                    Mathf.Max(
                        Mathf.Max(expansion.Left, expansion.Right),
                        Mathf.Max(expansion.Bottom, expansion.Top)));
            }

            float minimumPixelsPerEm = requestedQuality switch
            {
                TextStudio2DSourceFieldQuality.Native => 0f,
                TextStudio2DSourceFieldQuality.High => 96f,
                TextStudio2DSourceFieldQuality.Ultra => 160f,
                _ => highestTier switch
                {
                    TextStudio2DQualityTier.Hero => 128f,
                    TextStudio2DQualityTier.Balanced => 72f,
                    _ => 48f
                }
            };
            return new TextStudio2DSourceFieldRequest(
                requestedQuality,
                minimumPixelsPerEm,
                minimumSafeDistance,
                requiresMultiDistance);
        }

        static bool UsesEmFieldDistance(in TextStudio2DCompiledOperation operation)
        {
            return operation.Kind switch
            {
                TextStudio2DLayerKind.Fill =>
                    operation.Fill.Units == TextStudio2DUnit.Em,
                TextStudio2DLayerKind.Stroke =>
                    operation.Stroke.Units == TextStudio2DUnit.Em,
                TextStudio2DLayerKind.InnerShadow or
                    TextStudio2DLayerKind.OuterShadow =>
                    operation.Shadow.Units == TextStudio2DUnit.Em &&
                    operation.Shadow.Rendering ==
                    TextStudio2DSoftEffectRendering.DistanceField,
                TextStudio2DLayerKind.InnerGlow or
                    TextStudio2DLayerKind.OuterGlow =>
                    operation.Glow.Units == TextStudio2DUnit.Em &&
                    operation.Glow.Rendering ==
                    TextStudio2DSoftEffectRendering.DistanceField,
                TextStudio2DLayerKind.Bevel =>
                    operation.Bevel.Units == TextStudio2DUnit.Em,
                _ => false
            };
        }

        static bool ExceedsFusedFieldBounds(in TextStudio2DLayer layer)
        {
            TextStudio2DBoundsExpansion bounds = ComputeBounds(in layer);
            float maximum = Mathf.Max(
                Mathf.Max(bounds.Left, bounds.Right),
                Mathf.Max(bounds.Bottom, bounds.Top));
            return maximum > TextStudio2DFieldPadding.MaximumCapacity;
        }

        static bool RequiresRetainedFauxDepth(in TextStudio2DLayer layer)
        {
            TextStudio2DBoundsExpansion bounds = ComputeBounds(in layer);
            float maximum = Mathf.Max(
                Mathf.Max(bounds.Left, bounds.Right),
                Mathf.Max(bounds.Bottom, bounds.Top));
            return maximum > MaximumEfficientFusedDepthExpansion;
        }

        static bool SelectorUsesNodeKind(
            in TextStudio2DCompiledSelector selector,
            TextStudioSelectorNodeKind kind)
        {
            for (int i = 0; i < selector.NodeCount; i++)
            {
                if (selector.GetNode(i).Kind == kind)
                    return true;
            }
            return false;
        }

        static TextStudio2DExecutionPath ResolveExecutionPath(
            TextStudio2DLayer layer,
            TextStudio2DExecutionPath minimumPath,
            TextStudio2DCompileContext context,
            int itemIndex,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            TextStudio2DExecutionPath requested = layer.ExecutionPath ==
                                                  TextStudio2DExecutionPath.Automatic
                ? minimumPath
                : layer.ExecutionPath;
            if (PathRank(requested) < PathRank(minimumPath))
            {
                AddDiagnostic(
                    diagnostics,
                    TextStudioStackDiagnosticSeverity.Error,
                    "path-underpowered",
                    itemIndex,
                    layer.ItemId,
                    $"The forced {requested} path cannot preserve this layer's authored behavior, which requires {minimumPath}. Set Execution Path to Automatic or {minimumPath}.");
                requested = minimumPath;
            }

            if (SupportsPath(requested, context.Capabilities))
                return requested;

            if (context.AllowPathFallback)
            {
                TextStudio2DExecutionPath fallback = FindSupportedPath(
                    requested,
                    minimumPath,
                    context.Capabilities);
                if (fallback != TextStudio2DExecutionPath.Automatic)
                {
                    AddDiagnostic(
                        diagnostics,
                        TextStudioStackDiagnosticSeverity.Warning,
                        "path-fallback",
                        itemIndex,
                        layer.ItemId,
                        "The requested path is unavailable, so compilation selected the nearest behavior-preserving path.");
                    return fallback;
                }
            }

            AddDiagnostic(
                diagnostics,
                TextStudioStackDiagnosticSeverity.Error,
                "unsupported-path",
                itemIndex,
                layer.ItemId,
                "No available renderer path can preserve this layer's authored behavior.");
            return requested;
        }

        static TextStudio2DExecutionPath FindSupportedPath(
            TextStudio2DExecutionPath requested,
            TextStudio2DExecutionPath minimumPath,
            TextStudio2DRendererCapabilities capabilities)
        {
            for (int rank = Mathf.Max(PathRank(requested), PathRank(minimumPath)); rank <= 3; rank++)
            {
                TextStudio2DExecutionPath candidate = (TextStudio2DExecutionPath)rank;
                if (SupportsPath(candidate, capabilities))
                    return candidate;
            }
            for (int rank = PathRank(requested) - 1; rank >= PathRank(minimumPath); rank--)
            {
                TextStudio2DExecutionPath candidate = (TextStudio2DExecutionPath)rank;
                if (SupportsPath(candidate, capabilities))
                    return candidate;
            }
            return TextStudio2DExecutionPath.Automatic;
        }

        static bool SupportsPath(
            TextStudio2DExecutionPath path,
            TextStudio2DRendererCapabilities capabilities)
        {
            switch (path)
            {
                case TextStudio2DExecutionPath.FusedField:
                    return Has(capabilities, TextStudio2DRendererCapabilities.SignedDistanceField) &&
                           Has(capabilities, TextStudio2DRendererCapabilities.PerGlyphStyleIndex);
                case TextStudio2DExecutionPath.RetainedGeometry:
                    return Has(capabilities, TextStudio2DRendererCapabilities.RetainedGeometry);
                case TextStudio2DExecutionPath.HeroCompositor:
                    return Has(capabilities, TextStudio2DRendererCapabilities.OffscreenCompositor);
                default:
                    return false;
            }
        }

        static int PathRank(TextStudio2DExecutionPath path)
        {
            return (int)path;
        }

        static void AddPathRequirements(
            TextStudio2DExecutionPath path,
            ref TextStudio2DStyleRequirements requirements)
        {
            if (path == TextStudio2DExecutionPath.RetainedGeometry)
                requirements |= TextStudio2DStyleRequirements.RetainedGeometry;
            else if (path == TextStudio2DExecutionPath.HeroCompositor)
                requirements |= TextStudio2DStyleRequirements.OffscreenSurface;
        }

        static int ResolveCompiledRelationship(
            TextStudioStackItemId relationship,
            Dictionary<TextStudioStackItemId, int> compiledIndices,
            int itemIndex,
            TextStudioStackItemId itemId,
            string relationshipName,
            List<TextStudioStackDiagnostic> diagnostics)
        {
            if (!relationship.IsValid)
                return -1;
            if (compiledIndices.TryGetValue(relationship, out int index))
                return index;
            AddDiagnostic(
                diagnostics,
                TextStudioStackDiagnosticSeverity.Error,
                "relationship-order",
                itemIndex,
                itemId,
                "The " + relationshipName + " row must be active and appear before its consumer.");
            return -1;
        }

        static TextStudio2DBoundsExpansion ComputeBounds(in TextStudio2DLayer layer)
        {
            switch (layer.Kind)
            {
                case TextStudio2DLayerKind.Fill:
                    return UniformBounds(Mathf.Max(0f, layer.Fill.Dilation) +
                                         Mathf.Max(0f, layer.Fill.Softness));
                case TextStudio2DLayerKind.Stroke:
                    float stroke = Mathf.Max(0f, layer.Stroke.Width);
                    stroke *= Mathf.Clamp01((layer.Stroke.Alignment + 1f) * .5f);
                    if (layer.Stroke.Join == TextStudio2DStrokeJoin.Miter) stroke *= layer.Stroke.MiterLimit;
                    stroke += Mathf.Max(0f, layer.Stroke.Softness);
                    return UniformBounds(stroke);
                case TextStudio2DLayerKind.OuterGlow:
                    return UniformBounds(Mathf.Max(0f, layer.Glow.Radius + layer.Glow.Spread));
                case TextStudio2DLayerKind.OuterShadow:
                    float shadow = Mathf.Max(0f, layer.Shadow.Blur + layer.Shadow.Spread);
                    return OffsetBounds(layer.Shadow.Offset, shadow);
                case TextStudio2DLayerKind.FauxDepth:
                    if (!layer.FauxDepth.HasDepth)
                        return default;
                    float taperExpansion = layer.FauxDepth.Units == TextStudio2DUnit.Em
                        ? Mathf.Max(0f, -layer.FauxDepth.ResolveTaper(layer.FauxDepth.Offset.magnitude)) * 0.5f
                        : 0f;
                    return OffsetBounds(
                        layer.FauxDepth.Offset,
                        taperExpansion +
                        Mathf.Max(0f, layer.FauxDepth.Softness) +
                        Mathf.Max(0f, layer.FauxDepth.SilhouetteOutset));
                case TextStudio2DLayerKind.Decoration:
                    Vector4 padding = layer.Decoration.Padding;
                    float plateHalfHeight = Mathf.Max(
                        0f,
                        layer.Decoration.PlateHeight * 0.5f);
                    float plateHalfWidth = Mathf.Max(0f, layer.Decoration.PlateWidth * 0.5f);
                    float decorationOffset = layer.Decoration.Offset;
                    Vector4 surfaceExpansion = layer.Decoration.GetTreatmentExpansion(TextStudio2DUnit.Em) +
                                               layer.Decoration.GetTreatmentExpansion(TextStudio2DUnit.Pixels);
                    return new TextStudio2DBoundsExpansion(
                        Mathf.Max(0f, padding.x) + plateHalfWidth + surfaceExpansion.x,
                        Mathf.Max(0f, padding.y) + plateHalfHeight +
                        Mathf.Max(0f, -decorationOffset) + surfaceExpansion.y,
                        Mathf.Max(0f, padding.z) + plateHalfWidth + surfaceExpansion.z,
                        Mathf.Max(0f, padding.w) + plateHalfHeight +
                        Mathf.Max(0f, decorationOffset) + surfaceExpansion.w);
                case TextStudio2DLayerKind.GraphicEffect:
                    TextStudio2DGraphicEffectSpec effect = layer.GraphicEffect;
                    return TextStudioVisualPrimitiveMath.CalculateBounds(
                        in effect);
                case TextStudio2DLayerKind.AnimatedEffect:
                    TextStudio2DAnimatedEffectSpec animated = layer.AnimatedEffect;
                    return TextStudioVisualPrimitiveMath.CalculateBounds(in animated);
                default:
                    return default;
            }
        }

        static TextStudio2DBoundsExpansion UniformBounds(float value)
        {
            return new TextStudio2DBoundsExpansion(value, value, value, value);
        }

        static TextStudio2DBoundsExpansion OffsetBounds(Vector2 offset, float radius)
        {
            return new TextStudio2DBoundsExpansion(
                radius + Mathf.Max(0f, -offset.x),
                radius + Mathf.Max(0f, -offset.y),
                radius + Mathf.Max(0f, offset.x),
                radius + Mathf.Max(0f, offset.y));
        }

        static TextStudio2DShaderFeatures ResolveFeatures(
            in TextStudio2DLayer layer,
            in TextStudio2DCompiledSelector selector)
        {
            TextStudio2DShaderFeatures features = UsesPaint(layer.Kind)
                ? PaintFeatures(layer.Paint.Kind)
                : TextStudio2DShaderFeatures.None;
            switch (layer.Kind)
            {
                case TextStudio2DLayerKind.Stroke:
                    features |= TextStudio2DShaderFeatures.Stroke;
                    break;
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                    features |= TextStudio2DShaderFeatures.Shadow;
                    if (TextStudio2DSoftEffectUtility.RequiresTextWideBlur(
                            in layer))
                    {
                        features |= TextStudio2DShaderFeatures.HeroPostProcess;
                    }
                    break;
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    features |= TextStudio2DShaderFeatures.Glow;
                    if (TextStudio2DSoftEffectUtility.RequiresTextWideBlur(
                            in layer))
                    {
                        features |= TextStudio2DShaderFeatures.HeroPostProcess;
                    }
                    break;
                case TextStudio2DLayerKind.Bevel:
                    features |= TextStudio2DShaderFeatures.Bevel |
                                TextStudio2DShaderFeatures.FieldNormal;
                    break;
                case TextStudio2DLayerKind.MaterialResponse:
                    features |= TextStudio2DShaderFeatures.MaterialResponse;
                    if (TextStudio2DTreatmentCatalog.TryGet(
                            layer.MaterialResponse.Kind,
                            out TextStudio2DTreatmentDefinition materialDefinition) &&
                        materialDefinition.Primitive.UsesFieldNormal)
                    {
                        features |= TextStudio2DShaderFeatures.FieldNormal;
                    }
                    break;
                case TextStudio2DLayerKind.GraphicEffect:
                    features |= TextStudio2DShaderFeatures.GraphicEffect;
                    if (TextStudio2DGraphicEffectUtility.UsesFieldNormal(
                            layer.GraphicEffect.Kind))
                        features |= TextStudio2DShaderFeatures.FieldNormal;
                    break;
                case TextStudio2DLayerKind.AnimatedEffect:
                    features |= TextStudio2DShaderFeatures.AnimatedEffect;
                    if (TextStudio2DAnimatedEffectUtility.RequiresPostProcess(
                            layer.AnimatedEffect.Kind))
                    {
                        features |= TextStudio2DShaderFeatures.HeroPostProcess;
                    }
                    if (TextStudio2DAnimatedEffectUtility.RequiresHistory(
                            layer.AnimatedEffect.Kind))
                    {
                        features |= TextStudio2DShaderFeatures.TemporalHistory;
                    }
                    break;
                case TextStudio2DLayerKind.ColorAdjustment:
                    features |= TextStudio2DShaderFeatures.ColorAdjustment;
                    break;
                case TextStudio2DLayerKind.CompositeAdjustment:
                    features |= TextStudio2DShaderFeatures.HeroPostProcess;
                    break;
                case TextStudio2DLayerKind.FauxDepth:
                    features |= TextStudio2DShaderFeatures.FauxDepth;
                    break;
                case TextStudio2DLayerKind.Decoration:
                    features |= TextStudio2DShaderFeatures.Decoration;
                    break;
                case TextStudio2DLayerKind.Mask:
                case TextStudio2DLayerKind.Group:
                    features |= TextStudio2DShaderFeatures.Mask;
                    break;
            }
            if (!selector.IsTrivialAll)
                features |= TextStudio2DShaderFeatures.Selector;
            if (layer.MaskId.IsValid)
                features |= TextStudio2DShaderFeatures.Mask;
            if (IsAdvancedBlend(layer.BlendMode))
                features |= TextStudio2DShaderFeatures.AdvancedBlend;
            return features;
        }

        static TextStudio2DShaderFeatures PaintFeatures(TextStudio2DPaintKind kind)
        {
            if (IsGradient(kind))
                return TextStudio2DShaderFeatures.GradientPaint;
            if (kind == TextStudio2DPaintKind.Texture || kind == TextStudio2DPaintKind.Pattern)
                return TextStudio2DShaderFeatures.TexturePaint;
            if (kind == TextStudio2DPaintKind.ProceduralNoise)
                return TextStudio2DShaderFeatures.NoisePaint;
            return TextStudio2DShaderFeatures.SolidPaint;
        }

        static void ResolveParameters(
            in TextStudio2DLayer layer,
            out Vector4 parameters0,
            out Vector4 parameters1)
        {
            parameters0 = Vector4.zero;
            parameters1 = Vector4.zero;
            switch (layer.Kind)
            {
                case TextStudio2DLayerKind.Stroke:
                    parameters0 = new Vector4(
                        layer.Stroke.Width,
                        layer.Stroke.Alignment,
                        layer.Stroke.Softness,
                        (float)layer.Stroke.Join);
                    parameters1 = new Vector4(
                        layer.Stroke.MiterLimit,
                        layer.Stroke.DashOffset,
                        layer.Stroke.DashCount,
                        (float)layer.Stroke.Units);
                    break;
                case TextStudio2DLayerKind.Fill:
                    parameters0 = new Vector4(
                        layer.Fill.Dilation,
                        layer.Fill.Softness,
                        (float)layer.Fill.Units,
                        (float)layer.Fill.ColorFace);
                    parameters1.w = layer.Fill.PreservesNativeAppearance ? 1f : 0f;
                    break;
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                    parameters0 = new Vector4(
                        layer.Shadow.Offset.x,
                        layer.Shadow.Offset.y,
                        layer.Shadow.Blur,
                        layer.Shadow.Spread);
                    parameters1 = new Vector4(
                        (float)layer.Shadow.Rendering,
                        (float)layer.Shadow.Units,
                        0f,
                        0f);
                    break;
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    parameters0 = new Vector4(layer.Glow.Radius, layer.Glow.Spread, 0f, 0f);
                    parameters1 = new Vector4(
                        (float)layer.Glow.Rendering,
                        (float)layer.Glow.Units,
                        0f,
                        0f);
                    break;
                case TextStudio2DLayerKind.Bevel:
                    parameters0 = new Vector4(
                        layer.Bevel.Width,
                        layer.Bevel.Softness,
                        layer.Bevel.Depth,
                        layer.Bevel.LightAngle);
                    parameters1 = new Vector4(
                        layer.Bevel.LightElevation,
                        (float)layer.Bevel.Units,
                        0f,
                        0f);
                    break;
                case TextStudio2DLayerKind.FauxDepth:
                    parameters0 = new Vector4(
                        layer.FauxDepth.Offset.x,
                        layer.FauxDepth.Offset.y,
                        layer.FauxDepth.Taper,
                        layer.FauxDepth.Falloff);
                    parameters1 = new Vector4(
                        layer.FauxDepth.Softness,
                        layer.FauxDepth.EndFade,
                        (float)layer.FauxDepth.PaintMapping,
                        (float)layer.FauxDepth.Units);
                    break;
                case TextStudio2DLayerKind.Decoration:
                    parameters0 = new Vector4(
                        (float)layer.Decoration.Kind,
                        layer.Decoration.Thickness,
                        layer.Decoration.Offset,
                        layer.Decoration.CornerRadius);
                    parameters1 = layer.Decoration.Padding;
                    break;
                case TextStudio2DLayerKind.Mask:
                    parameters0 = new Vector4(
                        layer.Mask.Invert ? 1f : 0f,
                        layer.Mask.Feather,
                        0f,
                        0f);
                    break;
                case TextStudio2DLayerKind.MaterialResponse:
                    TextStudio2DMaterialResponseSpec response = layer.MaterialResponse;
                    parameters0 = new Vector4(
                        response.Width,
                        response.Softness,
                        response.Depth,
                        response.LightAngle);
                    parameters1 = new Vector4(
                        response.LightElevation,
                        response.Roughness,
                        response.Metallic,
                        response.Clearcoat);
                    break;
                case TextStudio2DLayerKind.GraphicEffect:
                    TextStudio2DGraphicEffectSpec graphic = layer.GraphicEffect;
                    parameters0 = new Vector4(
                        graphic.Intensity,
                        graphic.Scale,
                        graphic.Width,
                        graphic.Softness);
                    parameters1 = new Vector4(
                        graphic.Angle,
                        graphic.Offset.x,
                        graphic.Offset.y,
                        graphic.Detail);
                    break;
                case TextStudio2DLayerKind.AnimatedEffect:
                    TextStudio2DAnimatedEffectSpec animated = layer.AnimatedEffect;
                    parameters0 = new Vector4(
                        animated.Amount,
                        animated.Speed,
                        animated.Phase,
                        animated.Scale);
                    parameters1 = new Vector4(
                        animated.Width,
                        animated.Softness,
                        animated.Amplitude,
                        animated.Frequency);
                    break;
                case TextStudio2DLayerKind.ColorAdjustment:
                case TextStudio2DLayerKind.CompositeAdjustment:
                    parameters0 = new Vector4(
                        layer.ColorAdjustment.Strength,
                        layer.ColorAdjustment.ClampOutput ? 1f : 0f,
                        0f,
                        0f);
                    break;
            }
        }

        static List<TextStudio2DCompiledBucket> BuildBuckets(
            List<TextStudio2DCompiledOperation> operations,
            int maxFusedOperations)
        {
            List<TextStudio2DCompiledBucket> buckets = new List<TextStudio2DCompiledBucket>();
            int start = 0;
            while (start < operations.Count)
            {
                TextStudio2DExecutionPath path = operations[start].ExecutionPath;
                int count = 1;
                TextStudio2DShaderFeatures features = operations[start].Features;
                int capacity = path == TextStudio2DExecutionPath.FusedField
                    ? maxFusedOperations
                    : int.MaxValue;

                while (start + count < operations.Count)
                {
                    TextStudio2DCompiledOperation next = operations[start + count];
                    if (next.ExecutionPath != path)
                        break;
                    if (count >= capacity)
                        break;
                    if (path == TextStudio2DExecutionPath.RetainedGeometry &&
                        next.BlendMode != operations[start].BlendMode)
                        break;
                    features |= next.Features;
                    count++;
                }

                TextStudio2DBucketSplitReason splitReason = buckets.Count == 0
                    ? TextStudio2DBucketSplitReason.None
                    : ResolveIncomingSplitReason(operations, start, maxFusedOperations);
                TextStudio2DBlendMode outputBlend =
                    path == TextStudio2DExecutionPath.RetainedGeometry
                        ? operations[start].BlendMode
                        : TextStudio2DBlendMode.Normal;
                byte surfaceFormat = path == TextStudio2DExecutionPath.HeroCompositor ? (byte)1 : (byte)0;
                TextStudio2DShaderFeatures programFeatures =
                    path == TextStudio2DExecutionPath.FusedField
                        ? TextStudio2DShaderFeatures.None
                        : features;
                buckets.Add(new TextStudio2DCompiledBucket(
                    start,
                    count,
                    new TextStudio2DBatchKey(
                        path,
                        programFeatures,
                        outputBlend,
                        surfaceFormat),
                    features,
                    splitReason));
                start += count;
            }
            return buckets;
        }

        static TextStudio2DBucketSplitReason ResolveIncomingSplitReason(
            List<TextStudio2DCompiledOperation> operations,
            int start,
            int maxFusedOperations)
        {
            TextStudio2DCompiledOperation previous = operations[start - 1];
            TextStudio2DCompiledOperation current = operations[start];
            if (previous.ExecutionPath != current.ExecutionPath)
            {
                if (current.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry)
                    return TextStudio2DBucketSplitReason.ExecutionPath |
                           TextStudio2DBucketSplitReason.RetainedGeometry;
                if (current.ExecutionPath == TextStudio2DExecutionPath.HeroCompositor)
                    return TextStudio2DBucketSplitReason.ExecutionPath |
                           TextStudio2DBucketSplitReason.OffscreenCompositing;
                return TextStudio2DBucketSplitReason.ExecutionPath;
            }

            int fusedRun = 0;
            for (int i = start - 1; i >= 0 &&
                 operations[i].ExecutionPath == TextStudio2DExecutionPath.FusedField; i--)
            {
                fusedRun++;
            }
            return fusedRun >= maxFusedOperations
                ? TextStudio2DBucketSplitReason.FusedOperationCapacity
                : TextStudio2DBucketSplitReason.RenderState;
        }

        static TextStudio2DCostEstimate EstimateCost(
            List<TextStudio2DCompiledOperation> operations,
            List<TextStudio2DCompiledPaint> paints,
            List<TextStudio2DCompiledBucket> buckets)
        {
            int fieldSamples = 0;
            int textureSamples = 0;
            int geometrySegments = 0;
            int offscreenPasses = 0;
            int surfaces = 0;
            int relativeCost = 0;
            for (int i = 0; i < operations.Count; i++)
            {
                TextStudio2DCompiledOperation operation = operations[i];
                int weight = OperationWeight(in operation);
                relativeCost += weight;
                if (operation.ExecutionPath == TextStudio2DExecutionPath.FusedField)
                    fieldSamples += weight;
                else if (operation.SurfaceOwnerOperationIndex < 0 && operation.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry)
                    geometrySegments += Mathf.Max(1, weight * 2);
                else if (operation.ExecutionPath == TextStudio2DExecutionPath.HeroCompositor)
                {
                    if (TextStudio2DSoftEffectUtility.TryResolveTextWideBlur(
                            in operation,
                            out _,
                            out _,
                            out _))
                    {
                        offscreenPasses += 6;
                        textureSamples += 68;
                        surfaces = Mathf.Max(surfaces, 7);
                    }
                    else if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect)
                    {
                        TextStudio2DAnimatedEffectSpec animated =
                            operation.AnimatedEffect;
                        TextStudioVisualPrimitiveContract contract =
                            TextStudio2DAnimatedEffectUtility.GetContract(
                                animated.Kind);
                        offscreenPasses += 2 + contract.AdditionalPassCount;
                        int animatedSurfaces = 2 +
                            contract.PeakScratchSurfaceCount;
                        if (contract.RequiresHistory)
                        {
                            animatedSurfaces += animated.HistoryFrames;
                            textureSamples += animated.HistoryFrames;
                        }
                        else
                            textureSamples += contract.PostProcessTextureSamples;
                        surfaces = Mathf.Max(surfaces, animatedSurfaces);
                    }
                    else
                    {
                        offscreenPasses += Mathf.Max(1, weight / 2);
                        surfaces = Mathf.Max(surfaces, 2);
                    }
                }
                if (operation.PaintIndex >= 0 && operation.PaintIndex < paints.Count)
                {
                    TextStudio2DPaintKind kind = paints[operation.PaintIndex].Kind;
                    if (kind == TextStudio2DPaintKind.Texture || kind == TextStudio2DPaintKind.Pattern)
                        textureSamples++;
                }
            }
            relativeCost += Mathf.Max(0, buckets.Count - 1) * 4;
            return new TextStudio2DCostEstimate(
                fieldSamples,
                textureSamples,
                geometrySegments,
                offscreenPasses,
                surfaces,
                relativeCost);
        }

        static int OperationWeight(in TextStudio2DCompiledOperation operation)
        {
            int weight;
            switch (operation.Kind)
            {
                case TextStudio2DLayerKind.Fill:
                    weight = 1;
                    break;
                case TextStudio2DLayerKind.Stroke:
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    weight = 2;
                    break;
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                case TextStudio2DLayerKind.Bevel:
                    weight = 3;
                    break;
                case TextStudio2DLayerKind.MaterialResponse:
                    weight = TextStudio2DTreatmentCatalog.TryGet(
                        operation.MaterialResponse.Kind,
                        out TextStudio2DTreatmentDefinition materialDefinition)
                            ? materialDefinition.Primitive.OperationWeight
                            : 3;
                    break;
                case TextStudio2DLayerKind.FauxDepth:
                    weight = 4;
                    break;
                case TextStudio2DLayerKind.GraphicEffect:
                    weight = TextStudio2DGraphicEffectUtility.OperationWeight(
                        operation.GraphicEffect.Kind);
                    break;
                case TextStudio2DLayerKind.AnimatedEffect:
                    TextStudioVisualPrimitiveContract contract =
                        TextStudio2DAnimatedEffectUtility.GetContract(
                            operation.AnimatedEffect.Kind);
                    weight = contract.IsDefined ? contract.OperationWeight : 3;
                    break;
                case TextStudio2DLayerKind.ColorAdjustment:
                case TextStudio2DLayerKind.CompositeAdjustment:
                    weight = 4;
                    break;
                default:
                    weight = 2;
                    break;
            }
            if (operation.QualityTier == TextStudio2DQualityTier.Responsive)
                return Mathf.Max(1, weight - 1);
            if (operation.QualityTier == TextStudio2DQualityTier.Hero)
                return weight + 2;
            return weight;
        }

        static void HashLayer(
            in TextStudio2DLayer layer,
            in TextStudio2DCompiledSelector selector,
            TextStudio2DCompiledPaint paint,
            List<TextStudio2DGradientStop> gradientStops,
            TextStudio2DExecutionPath executionPath,
            TextStudio2DBoundsExpansion bounds,
            ref TextStudioDeterministicHash64 topology,
            ref TextStudioDeterministicHash64 parameters,
            ref TextStudioDeterministicHash64 renderState,
            ref TextStudioDeterministicHash64 boundsHash)
        {
            topology.Add(layer.ItemId.Value);
            topology.Add((byte)layer.Kind);
            topology.Add(layer.ParentId.Value);
            topology.Add(layer.MaskId.Value);
            topology.Add(layer.SurfaceOwnerId.Value);
            topology.Add((byte)layer.GroupOrder);
            topology.Add((byte)layer.SharedSurfaceOrder);
            topology.Add((byte)layer.SourceApplicability);
            if (layer.Kind == TextStudio2DLayerKind.Fill) topology.Add((byte)layer.Fill.ColorFace);
            HashSelector(in selector, ref topology);
            HashGeometryTopology(in layer, executionPath, ref topology);

            parameters.Add(layer.Opacity);
            parameters.Add(layer.ParticipatesInAnimatedTint);
            if (UsesPaint(layer.Kind))
                HashPaintParameters(in paint, gradientStops, ref parameters);
            HashLayerParameters(in layer, ref parameters);

            renderState.Add((byte)layer.BlendMode);
            renderState.Add((byte)executionPath);
            renderState.Add((byte)layer.QualityTier);
            if (UsesPaint(layer.Kind))
            {
                renderState.Add((byte)paint.Kind);
                renderState.Add((byte)paint.Mapping);
                renderState.Add((byte)paint.RangeMapping);
                renderState.Add(paint.ResourceId);
            }
            else if (layer.Kind == TextStudio2DLayerKind.GraphicEffect)
            {
                renderState.Add((byte)layer.GraphicEffect.Mapping);
            }
            else if (layer.Kind == TextStudio2DLayerKind.AnimatedEffect)
            {
                renderState.Add((byte)layer.AnimatedEffect.Mapping);
                renderState.Add((byte)layer.AnimatedEffect.ReducedMotionBehavior);
                renderState.Add((byte)layer.AnimatedEffect.HistoryFrames);
            }

            boundsHash.Add(bounds.Vector);
        }

        static void HashGeometryTopology(
            in TextStudio2DLayer layer,
            TextStudio2DExecutionPath executionPath,
            ref TextStudioDeterministicHash64 topology)
        {
            if (layer.Kind == TextStudio2DLayerKind.Decoration)
            {
                TextStudio2DDecorationSpec decoration = layer.Decoration;
                topology.Add((byte)layer.Decoration.Kind);
                topology.Add(layer.Decoration.Thickness);
                topology.Add(layer.Decoration.Offset);
                topology.Add(layer.Decoration.Padding);
                topology.Add(layer.Decoration.PlateHeight);
                topology.Add(layer.Decoration.PlateWidth);
                topology.Add((byte)layer.Decoration.VerticalAnchor);
                topology.Add((byte)decoration.Shape);
                topology.Add(decoration.CornerRadii);
                topology.Add(decoration.SquirclePower);
                topology.Add(decoration.PolygonSides);
                topology.Add(decoration.StarInset);
                topology.Add(decoration.GlyphPadding); topology.Add(decoration.GlyphBridgeRadius); topology.Add(decoration.GlyphFillHoles); topology.Add((int)decoration.BackplateVisibility);
                topology.Add(decoration.CustomPointCount);
                for (int pointIndex = 0;
                     pointIndex < decoration.CustomPointCount;
                     pointIndex++)
                {
                    topology.Add(decoration.GetCustomPoint(pointIndex));
                }
                topology.Add((byte)decoration.Topology);
                topology.Add(decoration.ConnectAcrossLineBreaks);
                decoration.Line.Hash(ref topology);
                topology.Add(decoration.GetTreatmentExpansion(TextStudio2DUnit.Em));
                topology.Add(decoration.GetTreatmentExpansion(TextStudio2DUnit.Pixels));
                return;
            }

            if (layer.Kind == TextStudio2DLayerKind.FauxDepth &&
                executionPath == TextStudio2DExecutionPath.RetainedGeometry)
            {
                topology.Add(layer.FauxDepth.Offset);
                topology.Add(layer.FauxDepth.Taper);
                topology.Add(layer.FauxDepth.Softness);
                topology.Add(layer.FauxDepth.SilhouetteOutset);
                topology.Add((byte)layer.FauxDepth.Units);
            }
        }

        internal static void HashSelector(
            in TextStudio2DCompiledSelector selector,
            ref TextStudioDeterministicHash64 topology)
        {
            topology.Add(selector.Root);
            topology.Add(selector.NodeCount);
            for (int i = 0; i < selector.NodeCount; i++)
            {
                TextStudioApplicationSelectorNode node = selector.GetNode(i);
                topology.Add((byte)node.Kind);
                topology.Add(node.Left);
                topology.Add(node.Right);
                topology.Add(node.Key);
                topology.Add((byte)node.Comparison);
                topology.Add((int)node.SemanticState);
                // Selector values change which glyphs carry an operation bit. Classify them as
                // topology so the TMP adapter rewrites the per-glyph mask instead of only
                // patching the shared GPU parameter table.
                topology.Add(node.Start);
                topology.Add(node.Count);
                topology.Add(node.UsesTextRange);
                if (node.UsesTextRange)
                {
                    TextStudioTextRange range = node.TextRange;
                    topology.Add(range.Start);
                    topology.Add(range.End);
                    topology.Add(range.HasStart);
                    topology.Add(range.HasEnd);
                    topology.Add(range.StartFromEnd);
                    topology.Add(range.EndFromEnd);
                }
                topology.Add(node.Color);
                topology.Add(node.Tolerance);
                topology.Add(node.Value);
                topology.Add(node.Invert);
            }

        }

        static void HashPaintParameters(
            in TextStudio2DCompiledPaint paint,
            List<TextStudio2DGradientStop> gradientStops,
            ref TextStudioDeterministicHash64 hash)
        {
            hash.Add(paint.Color);
            hash.Add((byte)paint.Spread);
            hash.Add(paint.Angle);
            hash.Add(paint.Center);
            hash.Add(paint.Scale);
            hash.Add(paint.Offset);
            hash.Add((byte)paint.TextureFit);
            hash.Add((byte)paint.DistanceDomain);
            hash.Add(paint.DistanceRange);
            hash.Add(paint.NoiseScale);
            hash.Add(paint.NoiseDetail);
            hash.Add(paint.NoiseSeed);
            TextStudioTextureContent.HashRevision(paint.Texture, ref hash);
            hash.Add(paint.GradientStopCount);
            for (int i = 0; i < paint.GradientStopCount; i++)
            {
                TextStudio2DGradientStop stop = gradientStops[paint.GradientStopStart + i];
                hash.Add(stop.Position);
                hash.Add(stop.Color);
            }
        }

        static void HashLayerParameters(
            in TextStudio2DLayer layer,
            ref TextStudioDeterministicHash64 hash)
        {
            switch (layer.Kind)
            {
                case TextStudio2DLayerKind.Fill:
                    hash.Add(layer.Fill.Dilation);
                    hash.Add(layer.Fill.Softness);
                    hash.Add((byte)layer.Fill.Units);
                    hash.Add((byte)layer.Fill.ColorFace);
                    hash.Add(layer.Fill.PreservesNativeAppearance);
                    break;
                case TextStudio2DLayerKind.Stroke:
                    TextStudio2DStrokeSpec stroke = layer.Stroke;
                    HashStroke(in stroke, ref hash);
                    break;
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                    hash.Add(layer.Shadow.Offset);
                    hash.Add(layer.Shadow.Blur);
                    hash.Add(layer.Shadow.Spread);
                    hash.Add((byte)layer.Shadow.Rendering);
                    hash.Add((byte)layer.Shadow.Units);
                    break;
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    hash.Add(layer.Glow.Radius);
                    hash.Add(layer.Glow.Spread);
                    hash.Add((byte)layer.Glow.Rendering);
                    hash.Add((byte)layer.Glow.Units);
                    break;
                case TextStudio2DLayerKind.Bevel:
                    hash.Add(layer.Bevel.Width);
                    hash.Add(layer.Bevel.Softness);
                    hash.Add(layer.Bevel.Depth);
                    hash.Add(layer.Bevel.LightAngle);
                    hash.Add(layer.Bevel.LightElevation);
                    hash.Add(layer.Bevel.Highlight);
                    hash.Add(layer.Bevel.Shadow);
                    hash.Add((byte)layer.Bevel.Units);
                    break;
                case TextStudio2DLayerKind.FauxDepth:
                    hash.Add(layer.FauxDepth.Offset);
                    hash.Add(layer.FauxDepth.Taper);
                    hash.Add(layer.FauxDepth.Falloff);
                    hash.Add(layer.FauxDepth.Softness);
                    hash.Add(layer.FauxDepth.SilhouetteOutset);
                    hash.Add(layer.FauxDepth.EndFade);
                    hash.Add((byte)layer.FauxDepth.PaintMapping);
                    hash.Add(layer.FauxDepth.SideLightAngle);
                    hash.Add(layer.FauxDepth.SideLightStrength);
                    hash.Add(layer.FauxDepth.FrontBevelWidth);
                    hash.Add(layer.FauxDepth.FrontBevelStrength);
                    hash.Add(layer.FauxDepth.FarCapShade);
                    hash.Add((byte)layer.FauxDepth.Units);
                    break;
                case TextStudio2DLayerKind.Decoration:
                    TextStudio2DDecorationSpec decoration = layer.Decoration;
                    hash.Add((byte)layer.Decoration.Kind);
                    hash.Add(layer.Decoration.Thickness);
                    hash.Add(layer.Decoration.Offset);
                    hash.Add(layer.Decoration.Padding);
                    hash.Add(layer.Decoration.CornerRadius);
                    hash.Add(layer.Decoration.PlateHeight);
                    hash.Add(layer.Decoration.PlateWidth);
                    hash.Add((byte)layer.Decoration.VerticalAnchor);
                    hash.Add((byte)layer.Decoration.LeftEnd);
                    hash.Add((byte)layer.Decoration.RightEnd);
                    hash.Add(layer.Decoration.CapSize);
                    hash.Add((byte)decoration.Shape);
                    hash.Add(decoration.CornerRadii);
                    hash.Add(decoration.SquirclePower);
                    hash.Add(decoration.PolygonSides);
                    hash.Add(decoration.StarInset);
                    hash.Add(decoration.GlyphPadding); hash.Add(decoration.GlyphBridgeRadius); hash.Add(decoration.GlyphFillHoles); hash.Add((int)decoration.BackplateVisibility);
                    hash.Add(decoration.CustomPointCount);
                    for (int pointIndex = 0;
                         pointIndex < decoration.CustomPointCount;
                         pointIndex++)
                    {
                        hash.Add(decoration.GetCustomPoint(pointIndex));
                    }
                    hash.Add((byte)decoration.Topology);
                    hash.Add(decoration.ConnectAcrossLineBreaks);
                    decoration.Line.Hash(ref hash);
                    break;
                case TextStudio2DLayerKind.Mask:
                    hash.Add(layer.Mask.SourceItemId.Value);
                    hash.Add(layer.Mask.Invert);
                    hash.Add(layer.Mask.Feather);
                    break;
                case TextStudio2DLayerKind.MaterialResponse:
                    TextStudio2DMaterialResponseSpec response = layer.MaterialResponse;
                    hash.Add((byte)response.Kind);
                    hash.Add(response.Width);
                    hash.Add(response.Softness);
                    hash.Add(response.Depth);
                    hash.Add(response.LightAngle);
                    hash.Add(response.LightElevation);
                    hash.Add(response.Roughness);
                    hash.Add(response.Metallic);
                    hash.Add(response.Clearcoat);
                    hash.Add(response.Anisotropy);
                    hash.Add(response.Iridescence);
                    hash.Add(response.Sparkle);
                    hash.Add(response.Emission);
                    hash.Add(response.Seed);
                    hash.Add(response.Highlight);
                    hash.Add(response.Shadow);
                    hash.Add(response.Environment);
                    break;
                case TextStudio2DLayerKind.GraphicEffect:
                    TextStudio2DGraphicEffectSpec graphic = layer.GraphicEffect;
                    hash.Add((byte)graphic.Kind);
                    hash.Add((byte)graphic.Mapping);
                    hash.Add(graphic.Intensity);
                    hash.Add(graphic.Scale);
                    hash.Add(graphic.Detail);
                    hash.Add(graphic.Width);
                    hash.Add(graphic.Softness);
                    hash.Add(graphic.Angle);
                    hash.Add(graphic.Offset);
                    hash.Add(graphic.PrimaryColor);
                    hash.Add(graphic.SecondaryColor);
                    hash.Add(graphic.Seed);
                    if (graphic.Kind == TextStudio2DGraphicEffectKind.Halftone)
                    {
                        TextStudio2DHalftoneSizeRamp ramp = graphic.HalftoneSizeRamp;
                        hash.Add(ramp.Enabled);
                        if (ramp.Enabled)
                        {
                            hash.Add(ramp.MinimumRadius);
                            hash.Add(ramp.MaximumRadius);
                            hash.Add(ramp.AngleDegrees);
                        }
                    }
                    break;
                case TextStudio2DLayerKind.AnimatedEffect:
                    TextStudio2DAnimatedEffectSpec animated = layer.AnimatedEffect;
                    hash.Add((byte)animated.Kind);
                    hash.Add((byte)animated.Mapping);
                    hash.Add(animated.Amount);
                    hash.Add(animated.Speed);
                    hash.Add(animated.Phase);
                    hash.Add(animated.Scale);
                    hash.Add(animated.Width);
                    hash.Add(animated.Softness);
                    hash.Add(animated.Amplitude);
                    hash.Add(animated.Frequency);
                    hash.Add(animated.Direction);
                    hash.Add(animated.PrimaryColor);
                    hash.Add(animated.SecondaryColor);
                    hash.Add(animated.Seed);
                    hash.Add(animated.HistoryFrames);
                    hash.Add((byte)animated.ReducedMotionBehavior);
                    break;
                case TextStudio2DLayerKind.ColorAdjustment:
                case TextStudio2DLayerKind.CompositeAdjustment:
                    TextStudio2DColorAdjustmentSpec filter = layer.ColorAdjustment;
                    hash.Add(filter.Red);
                    hash.Add(filter.Green);
                    hash.Add(filter.Blue);
                    hash.Add(filter.Alpha);
                    hash.Add(filter.Offset);
                    hash.Add(filter.Strength);
                    hash.Add(filter.ClampOutput);
                    hash.Add((byte)filter.SurfaceSelection);
                    break;
            }
        }

        static void HashStroke(in TextStudio2DStrokeSpec stroke, ref TextStudioDeterministicHash64 hash)
        {
            hash.Add(stroke.Width); hash.Add(stroke.Alignment); hash.Add(stroke.Softness);
            hash.Add((byte)stroke.Units); hash.Add((byte)stroke.Join); hash.Add(stroke.MiterLimit);
            hash.Add(stroke.DashOffset); hash.Add((byte)stroke.DashCap); hash.Add(stroke.ContinueAcrossContours);
            hash.Add(stroke.DashCount);
            for (int index = 0; index < stroke.DashCount; index++) hash.Add(stroke.GetDash(index));
        }

        internal static ulong ComputeGeometryFingerprint(CompiledTextStudio2DStyle style)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(style.Fingerprint.Topology); hash.Add(style.Fingerprint.Bounds);
            hash.Add((byte)style.WorldShadowMode);
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                hash.Add((byte)operation.ExecutionPath);
                hash.Add(TextStudio2DFieldPadding.UsesPixelUnits(in operation));
                if (TextStudio2DStyleDecorations.UsesContourStroke(in operation))
                {
                    TextStudio2DStrokeSpec stroke = operation.Stroke;
                    HashStroke(in stroke, ref hash);
                }
                // Shared-surface membership is encoded in retained vertices and contour handles.
                if (operation.Kind == TextStudio2DLayerKind.ColorAdjustment)
                    hash.Add((byte)operation.ColorAdjustment.SurfaceSelection);
            }
            return hash.Value;
        }

        static TextStudioStackFingerprint BuildFingerprint(
            ref TextStudioDeterministicHash64 topology,
            ref TextStudioDeterministicHash64 parameters,
            ref TextStudioDeterministicHash64 renderState,
            ref TextStudioDeterministicHash64 bounds)
        {
            TextStudioDeterministicHash64 complete = default;
            complete.Add(topology.Value);
            complete.Add(parameters.Value);
            complete.Add(renderState.Value);
            complete.Add(bounds.Value);
            return new TextStudioStackFingerprint(
                topology.Value,
                parameters.Value,
                renderState.Value,
                bounds.Value,
                complete.Value);
        }

        static void HashBuckets(
            List<TextStudio2DCompiledBucket> buckets,
            ref TextStudioDeterministicHash64 renderState)
        {
            renderState.Add(buckets.Count);
            for (int i = 0; i < buckets.Count; i++)
            {
                TextStudio2DCompiledBucket bucket = buckets[i];
                renderState.Add(bucket.FirstOperation);
                renderState.Add(bucket.OperationCount);
                renderState.Add((byte)bucket.BatchKey.ExecutionPath);
                renderState.Add((uint)bucket.BatchKey.Features);
                renderState.Add((byte)bucket.BatchKey.OutputBlend);
                renderState.Add(bucket.BatchKey.SurfaceFormat);
                renderState.Add((uint)bucket.RequiredFeatures);
                renderState.Add((uint)bucket.SplitReason);
            }
        }

        internal static bool UsesPaint(TextStudio2DLayerKind kind)
        {
            return kind != TextStudio2DLayerKind.Decoration &&
                   kind != TextStudio2DLayerKind.Bevel &&
                   kind != TextStudio2DLayerKind.MaterialResponse &&
                   kind != TextStudio2DLayerKind.GraphicEffect &&
                   kind != TextStudio2DLayerKind.AnimatedEffect &&
                   kind != TextStudio2DLayerKind.ColorAdjustment &&
                   kind != TextStudio2DLayerKind.CompositeAdjustment &&
                   kind != TextStudio2DLayerKind.Mask &&
                   kind != TextStudio2DLayerKind.Group;
        }

        static bool IsGradient(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.FourCornerGradient ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        static bool IsStopGradient(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        static bool IsSupportedPaintKind(TextStudio2DPaintKind kind)
        {
            return kind == TextStudio2DPaintKind.Solid ||
                   kind == TextStudio2DPaintKind.LinearGradient ||
                   kind == TextStudio2DPaintKind.RadialGradient ||
                   kind == TextStudio2DPaintKind.ConicGradient ||
                   kind == TextStudio2DPaintKind.Texture ||
                   kind == TextStudio2DPaintKind.Pattern ||
                   kind == TextStudio2DPaintKind.ProceduralNoise ||
                   kind == TextStudio2DPaintKind.FourCornerGradient ||
                   kind == TextStudio2DPaintKind.DistanceGradient;
        }

        static bool IsAdvancedBlend(TextStudio2DBlendMode blend)
        {
            return blend != TextStudio2DBlendMode.Normal;
        }

        static bool Has(
            TextStudio2DRendererCapabilities capabilities,
            TextStudio2DRendererCapabilities required)
        {
            return (capabilities & required) == required;
        }

        static bool IsFiniteNonNegative(float value)
        {
            return float.IsFinite(value) && value >= 0f;
        }

        static bool IsFiniteUnit(float value)
        {
            return float.IsFinite(value) && value >= 0f && value <= 1f;
        }

        static bool IsFiniteRange(float value, float minimum, float maximum)
        {
            return float.IsFinite(value) && value >= minimum && value <= maximum;
        }

        static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        static bool IsFinite(Vector4 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        static bool IsFinite(Color value)
        {
            return float.IsFinite(value.r) &&
                   float.IsFinite(value.g) &&
                   float.IsFinite(value.b) &&
                   float.IsFinite(value.a);
        }

        static void AddMissingRelationship(
            List<TextStudioStackDiagnostic> diagnostics,
            int itemIndex,
            TextStudioStackItemId itemId,
            string relationship)
        {
            AddDiagnostic(
                diagnostics,
                TextStudioStackDiagnosticSeverity.Error,
                "missing-" + relationship,
                itemIndex,
                itemId,
                "The referenced " + relationship + " row does not exist.");
        }

        static void AddDiagnostic(
            List<TextStudioStackDiagnostic> diagnostics,
            TextStudioStackDiagnosticSeverity severity,
            string code,
            int itemIndex,
            TextStudioStackItemId itemId,
            string message)
        {
            diagnostics.Add(new TextStudioStackDiagnostic(
                severity,
                DiagnosticPrefix + code,
                itemIndex,
                itemId,
                message));
        }
    }
}
