using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal readonly struct TextStudioWritingPrepareResult
    {
        internal readonly bool HasWriting;
        internal readonly bool RequiresSourceMaterial;
        internal readonly int SourceSampleOffset;
        internal readonly bool SourceStateChanged;

        internal TextStudioWritingPrepareResult(
            bool hasWriting,
            bool requiresSourceMaterial,
            int sourceSampleOffset,
            bool sourceStateChanged)
        {
            HasWriting = hasWriting;
            RequiresSourceMaterial = requiresSourceMaterial;
            SourceSampleOffset = sourceSampleOffset;
            SourceStateChanged = sourceStateChanged;
        }
    }

    /// <summary>
    /// One retained Writing renderer for direct and composed invocation paths. Glyph coverage is
    /// applied in the existing TMP production draw, while authored vector paths use one shared
    /// overlay draw and compact progress/transform buffers.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // The shared native material is reference-counted by renderer owners, including surviving Edit Mode owners.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioWritingRenderer : IDisposable
    {
        const string WorldHostName = "_TSWriting";
        const string UiHostName = "_TSWriting";
        const HideFlags HostHideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        const float MinimumExtent = 0.0001f;
        const int RoundSegments = 8;

        static Material s_material;
        static int s_materialReferences;
        static bool s_missingShaderWarningLogged;

        readonly TextStudioText _hub;
        readonly TextStudioWritingRuntimeInvocationSet _invocations =
            new TextStudioWritingRuntimeInvocationSet();
        readonly TextStudioWritingRuntimePlayback _playback =
            new TextStudioWritingRuntimePlayback();
        readonly TextStudio2DStyleDecorations.GeometryBuffer _geometry =
            new TextStudio2DStyleDecorations.GeometryBuffer();
        Vector2[] _scratchPoints = Array.Empty<Vector2>();
        Vector2[] _scratchTangents = Array.Empty<Vector2>();
        float[] _scratchPressure = Array.Empty<float>();
        float[] _scratchDistance = Array.Empty<float>();
        int[] _scratchGlyphs = Array.Empty<int>();
        int _invocationCount;
        TextStudioVisualVersion _topologyVisualVersion;
        int _topologyGlyphCount = -1;
        int _topologyInvocationCount = -1;
        ulong _topologyFingerprint;
        int _sourceSampleOffset = -1;
        bool _hadWriting;

        GameObject _worldHost;
        Mesh _worldMesh;
        MeshRenderer _worldRenderer;
        GameObject _uiHost;
        RectTransform _uiTransform;
        TextStudio2DStyleDecorationGraphic _uiGraphic;
        TextStudioCanvasShaderChannelLease _canvasChannelLease;

        internal int DebugVertexCount => _geometry.Vertices.Count;
        internal int DebugInvocationCount => _invocationCount;
        internal int SourceSampleOffset => _sourceSampleOffset;
        internal string CompatibilityWarning { get; private set; } = string.Empty;

        /// <summary>
        /// True when Writing is neither drawing nor erasing: every active glyph sample is fully written with its
        /// source face fully visible, so the TMP glyph faces alone show the settled text.
        /// </summary>
        internal bool HasSettledGlyphFaces()
        {
            if (!_hadWriting) return true;
            TextStudioGpuWritingSample[] samples = _playback.GetSamples(out int offset, out int count);
            if (samples == null || count <= 0) return false;
            int end = Mathf.Min(samples.Length, offset + count);
            for (int index = offset; index < end; index++)
            {
                if (!TextStudioWritingGpuPacking.UnpackActive(samples[index])) continue;
                if (TextStudioWritingGpuPacking.UnpackProgress(samples[index]) < 1f ||
                    TextStudioWritingGpuPacking.UnpackSourceAlpha(samples[index]) < 1f)
                    return false;
            }
            return true;
        }

        internal TextStudioWritingRenderer(TextStudioText hub)
        {
            _hub = hub;
            AcquireMaterial();
        }

        internal static bool RequiresWritingMaterial(
            TextStudioCompiledPlan plan,
            CompiledTextStudioWritingStyle directStyle)
        {
            if (directStyle != null && directStyle.IsValid && !directStyle.IsPassThrough)
                return true;
            if (plan?.EffectBindings == null)
                return false;
            for (int index = 0; index < plan.EffectBindingCount; index++)
            {
                if (plan.EffectBindings[index].effect is TextStudioCompositionLayer layer &&
                    layer.Kind == TextStudioCompositionLayerKind.Writing &&
                    layer.Enabled)
                {
                    CompiledTextStudioWritingStyle style =
                        layer.ResolveCompiledWritingStyle();
                    if (style != null && style.IsValid && !style.IsPassThrough)
                        return true;
                }
            }
            return false;
        }

        internal TextStudioWritingPrepareResult Prepare(
            in TextStudioFrame frame,
            TMP_Text tmpText,
            TextStudioMotionPolicy motionPolicy)
        {
            BuildInvocationList(in frame);
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            int glyphCount = snapshot.GlyphCount;
            EnsureMaterialAvailable();
            bool hasWriting = _invocationCount > 0 &&
                              snapshot.IsCreated &&
                              glyphCount > 0 &&
                              TextStudioWritingGpuTable.IsGpuSupported &&
                              IsMaterialUsable();
            CompatibilityWarning = ResolveCompatibilityWarning();
            bool previousWriting = _hadWriting;
            int previousSourceOffset = _sourceSampleOffset;
            _hadWriting = hasWriting;
            if (!hasWriting)
            {
                ReleaseGpuHandle();
                _sourceSampleOffset = -1;
                _geometry.Clear();
                InvalidateTopology();
                InvalidateSampleEvaluation();
                HideHosts();
                return new TextStudioWritingPrepareResult(
                    false,
                    false,
                    -1,
                    previousWriting || previousSourceOffset >= 0);
            }

            if (!_playback.TryEnsureRanges(
                    glyphCount,
                    _invocationCount,
                    out TextStudioWritingGpuRanges ranges,
                    out bool allocationChanged))
            {
                CompatibilityWarning = TextStudioWritingGpuTable.Shared.MemoryBudgetExceeded
                    ? TextStudioAppearanceMemory.LastRejection.ToString()
                    : "Writing reached its shared sample or transform capacity.";
                ReleaseGpuHandle();
                _sourceSampleOffset = -1;
                _geometry.Clear();
                InvalidateTopology();
                InvalidateSampleEvaluation();
                HideHosts();
                return new TextStudioWritingPrepareResult(
                    false,
                    false,
                    -1,
                    previousWriting || previousSourceOffset >= 0);
            }

            _sourceSampleOffset = ranges.SampleOffset;
            bool topologyChanged =
                allocationChanged ||
                NeedsTopologyRebuild(in frame, glyphCount);
            if (topologyChanged)
            {
                RebuildGeometry(
                    in frame,
                    ranges.SampleOffset,
                    ranges.TransformOffset,
                    glyphCount);
            }
            _playback.EvaluateAndUpload(
                in frame,
                _invocations,
                motionPolicy,
                topologyChanged);
            if (topologyChanged)
            {
                UploadGeometry(tmpText);
                CaptureTopology(in frame, glyphCount);
            }
            else
            {
                SynchronizeHost(tmpText);
            }
            bool sourceChanged = !previousWriting ||
                                 previousSourceOffset != _sourceSampleOffset ||
                                 (frame.GlobalDirty & (DirtyFlags.WritingTopology | DirtyFlags.WritingBounds | DirtyFlags.WritingRenderState)) != 0;
            return new TextStudioWritingPrepareResult(
                true,
                true,
                _sourceSampleOffset,
                sourceChanged);
        }

        void ReleaseGpuHandle()
        {
            _playback.Release();
        }

        string ResolveCompatibilityWarning()
        {
            if (_invocationCount == 0)
                return string.Empty;
            if (!TextStudioWritingGpuTable.IsGpuSupported)
            {
                return "Writing needs structured GPU buffers on this graphics device. " +
                       "Text Studio is preserving readable source text without the Writing treatment.";
            }
            if (!IsMaterialUsable())
            {
                return "The packaged Writing shader is unavailable. Text Studio is preserving readable source text without the Writing treatment.";
            }
            return string.Empty;
        }

        void BuildInvocationList(in TextStudioFrame frame)
        {
            _invocations.Build(in frame);
            _invocationCount = _invocations.Count;
        }

        bool NeedsTopologyRebuild(in TextStudioFrame frame, int glyphCount)
        {
            if (_topologyGlyphCount != glyphCount ||
                _topologyInvocationCount != _invocationCount ||
                !_topologyVisualVersion.Equals(frame.VisualSnapshot.Version))
            {
                return true;
            }
            ulong fingerprint = BuildTopologyFingerprint();
            if (_topologyFingerprint != fingerprint)
                return true;
            DirtyFlags topologyDirty =
                DirtyFlags.Text |
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.WritingTopology |
                DirtyFlags.WritingBounds |
                DirtyFlags.WritingRenderState;
            return (frame.GlobalDirty & topologyDirty) != 0;
        }

        void CaptureTopology(in TextStudioFrame frame, int glyphCount)
        {
            _topologyGlyphCount = glyphCount;
            _topologyInvocationCount = _invocationCount;
            _topologyVisualVersion = frame.VisualSnapshot.Version;
            _topologyFingerprint = BuildTopologyFingerprint();
        }

        void InvalidateTopology()
        {
            _topologyGlyphCount = -1;
            _topologyInvocationCount = -1;
            _topologyVisualVersion = default;
            _topologyFingerprint = 0UL;
        }

        void InvalidateSampleEvaluation()
        {
            _playback.InvalidateSampleEvaluation();
        }

        ulong BuildTopologyFingerprint()
        {
            return _invocations.BuildFingerprint();
        }

        void RebuildGeometry(
            in TextStudioFrame frame,
            int sampleRangeOffset,
            int transformRangeOffset,
            int glyphCount)
        {
            _geometry.Clear();
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            ulong shapingFingerprint = TextStudioWritingShapingFingerprint.Compute(
                frame.Plan.ResolvedText,
                in snapshot);
            for (int invocationIndex = 0;
                 invocationIndex < _invocationCount;
                 invocationIndex++)
            {
                TextStudioWritingRuntimeInvocation invocation =
                    _invocations[invocationIndex];
                TextStudioWritingConfiguration configuration =
                    invocation.Style.Configuration;
                invocation.EnsureCapacity(glyphCount);
                Array.Clear(invocation.SourceKinds, 0, glyphCount);
                bool hasSignedDistanceField =
                    (snapshot.Capabilities &
                     TextStudioVisualCapabilities.SignedDistanceField) != 0;
                TextStudioWritingResolveRequest phraseRequest =
                    new TextStudioWritingResolveRequest(
                        frame.Plan.ResolvedText,
                        shapingFingerprint,
                        hasSignedDistanceField);
                TextStudioWritingResolution phraseResolution =
                    TextStudioWritingResolver.Resolve(
                        invocation.Style,
                        in phraseRequest);
                if (phraseResolution.SourceKind ==
                        TextStudioWritingSourceKind.PhrasePath &&
                    phraseResolution.HasPreparedPath)
                {
                    for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                    {
                        invocation.SourceKinds[glyphIndex] =
                            TextStudioWritingSourceKind.PhrasePath;
                    }
                    Rect phraseBounds = invocation.Style.Phrase != null
                        ? invocation.Style.Phrase.DesignBounds
                        : phraseResolution.Path.Bounds;
                    EmitPhrasePath(
                        phraseResolution.Path,
                        phraseBounds,
                        in frame,
                        in configuration,
                        sampleRangeOffset,
                        transformRangeOffset,
                        invocationIndex,
                        glyphCount);
                    continue;
                }

                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    TextStudioVisualGlyph glyph = snapshot.GetGlyph(glyphIndex);
                    snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource fontSource);
                    TextStudioWritingResolveRequest request =
                        new TextStudioWritingResolveRequest(
                            frame.Plan.ResolvedText,
                            shapingFingerprint,
                            hasSignedDistanceField,
                            new TextStudioWritingGlyphKey(
                                glyph.FontIdentity,
                                glyph.GlyphIndex),
                            hasGlyph: true, fontData: fontSource.AppearanceData);
                    TextStudioWritingResolution resolution =
                        TextStudioWritingResolver.Resolve(
                            invocation.Style,
                            in request);
                    invocation.SourceKinds[glyphIndex] = resolution.SourceKind;
                    if (resolution.SourceKind !=
                            TextStudioWritingSourceKind.GlyphPath ||
                        !resolution.HasPreparedPath)
                    {
                        continue;
                    }
                    Rect designBounds = resolution.GetDesignBounds(in glyph);
                    if (invocation.Style.Font != null &&
                        invocation.Style.Font.TryGetGlyph(
                            request.GlyphKey,
                            out TextStudioCompiledWritingGlyph compiledGlyph))
                    {
                        designBounds = compiledGlyph.DesignBounds;
                    }
                    int firstVertex = _geometry.Uv0.Count;
                    EmitGlyphPath(
                        resolution.Path,
                        designBounds,
                        in frame,
                        glyphIndex,
                        in configuration,
                        sampleRangeOffset,
                        transformRangeOffset,
                        invocationIndex,
                        glyphCount);
                    if (resolution.IsAutomatic && configuration.Mode != TextStudioWritingMode.OutlineTrace)
                        for (int vertex = firstVertex; vertex < _geometry.Uv0.Count; vertex++)
                        {
                            Vector4 uv = _geometry.Uv0[vertex]; uv.z = 1f; _geometry.Uv0[vertex] = uv;
                        }
                }
            }
        }

        void EmitGlyphPath(
            TextStudioPreparedWritingPath path,
            Rect designBounds,
            in TextStudioFrame frame,
            int glyphIndex,
            in TextStudioWritingConfiguration configuration,
            int sampleRangeOffset,
            int transformRangeOffset,
            int invocationIndex,
            int glyphCount)
        {
            TextStudioVisualGlyph glyph = frame.VisualSnapshot.GetGlyph(glyphIndex);
            TextStudioWritingPathMapping.ResolveGlyphBounds(
                in frame,
                glyphIndex,
                out Vector2 minimum,
                out Vector2 maximum);
            for (int strokeIndex = 0; strokeIndex < path.StrokeCount; strokeIndex++)
            {
                TextStudioPreparedWritingStroke stroke = path.GetStroke(strokeIndex);
                EnsureScratchCapacity(stroke.PointCount, false);
                for (int pointIndex = 0; pointIndex < stroke.PointCount; pointIndex++)
                {
                    TextStudioPreparedWritingPoint point =
                        path.GetPoint(stroke.PointStart + pointIndex);
                    _scratchPoints[pointIndex] = MapRect(
                        point.Position,
                        designBounds,
                        minimum,
                        maximum);
                    _scratchPressure[pointIndex] = point.Pressure;
                    _scratchDistance[pointIndex] = TextStudioWritingScheduleMath.ResolveStrokeDistance(
                        in stroke,
                        strokeIndex,
                        path.StrokeCount,
                        point.NormalizedDistance,
                        configuration.StrokeGap,
                        configuration.Reverse);
                }
                EmitStroke(
                    _scratchPoints,
                    _scratchPressure,
                    _scratchDistance,
                    0,
                    stroke.PointCount,
                    glyphIndex,
                    in glyph,
                    frame.GetEffectUnitScale(glyphIndex),
                    in configuration,
                    sampleRangeOffset +
                    glyphCount * (invocationIndex + 1) +
                    glyphIndex,
                    transformRangeOffset + glyphIndex);
            }
        }

        void EmitPhrasePath(
            TextStudioPreparedWritingPath path,
            Rect designBounds,
            in TextStudioFrame frame,
            in TextStudioWritingConfiguration configuration,
            int sampleRangeOffset,
            int transformRangeOffset,
            int invocationIndex,
            int glyphCount)
        {
            Vector4 sourceBounds = frame.VisualSnapshot.SourceBounds;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            Vector2 minimum = new Vector2(sourceBounds.x, sourceBounds.y);
            Vector2 maximum = new Vector2(sourceBounds.z, sourceBounds.w);
            for (int strokeIndex = 0; strokeIndex < path.StrokeCount; strokeIndex++)
            {
                TextStudioPreparedWritingStroke stroke = path.GetStroke(strokeIndex);
                int runStart = 0;
                EnsureScratchCapacity(stroke.PointCount, true);
                for (int pointIndex = 0; pointIndex < stroke.PointCount; pointIndex++)
                {
                    TextStudioPreparedWritingPoint point =
                        path.GetPoint(stroke.PointStart + pointIndex);
                    Vector2 mapped = MapRect(
                        point.Position,
                        designBounds,
                        minimum,
                        maximum);
                    int glyphIndex = FindNearestGlyph(
                        in snapshot,
                        mapped);
                    _scratchPoints[pointIndex] = mapped;
                    _scratchPressure[pointIndex] = point.Pressure;
                    _scratchDistance[pointIndex] = TextStudioWritingScheduleMath.ResolveStrokeDistance(
                        in stroke,
                        strokeIndex,
                        path.StrokeCount,
                        point.NormalizedDistance,
                        configuration.StrokeGap,
                        configuration.Reverse);
                    _scratchGlyphs[pointIndex] = glyphIndex;
                }

                while (runStart < stroke.PointCount - 1)
                {
                    int runGlyph = _scratchGlyphs[runStart];
                    int runEnd = runStart + 1;
                    while (runEnd < stroke.PointCount &&
                           _scratchGlyphs[runEnd] == runGlyph)
                    {
                        runEnd++;
                    }
                    if (runEnd - runStart < 2 && runEnd < stroke.PointCount)
                        runEnd++;
                    int count = runEnd - runStart;
                    TextStudioVisualGlyph glyph =
                        frame.VisualSnapshot.GetGlyph(runGlyph);
                    EmitStroke(
                        _scratchPoints,
                        _scratchPressure,
                        _scratchDistance,
                        runStart,
                        count,
                        runGlyph,
                        in glyph,
                        frame.GetEffectUnitScale(runGlyph),
                        in configuration,
                        sampleRangeOffset +
                        glyphCount * (invocationIndex + 1) +
                        runGlyph,
                        transformRangeOffset + runGlyph);
                    runStart = Mathf.Max(runStart + 1, runEnd - 1);
                }
            }
        }

        void EmitStroke(
            Vector2[] points,
            float[] pressure,
            float[] distance,
            int start,
            int count,
            int glyphIndex,
            in TextStudioVisualGlyph glyph,
            float effectUnitScale,
            in TextStudioWritingConfiguration configuration,
            int sampleIndex,
            int transformIndex)
        {
            if (points == null ||
                pressure == null ||
                distance == null ||
                start < 0 ||
                count < 2 ||
                start + count > points.Length ||
                start + count > pressure.Length ||
                start + count > distance.Length)
            {
                return;
            }
            float baseHalfWidth = Mathf.Max(
                MinimumExtent,
                configuration.Width * effectUnitScale * 0.5f);
            float softness = Mathf.Max(0f, configuration.EdgeSoftness) *
                             effectUnitScale;
            EnsureTangentScratchCapacity(count);
            for (int index = 0; index < count; index++)
            {
                Vector2 tangent;
                if (index == 0)
                    tangent = points[start + 1] - points[start];
                else if (index == count - 1)
                    tangent = points[start + count - 1] - points[start + count - 2];
                else
                    tangent = points[start + index + 1] - points[start + index - 1];
                _scratchTangents[index] = tangent.sqrMagnitude > 0.00000001f
                    ? tangent.normalized
                    : Vector2.right;
            }

            int vertexStart = _geometry.Vertices.Count;
            Color32 color = configuration.Color;
            for (int index = 0; index < count; index++)
            {
                Vector2 normal = ResolveJoinNormal(
                    _scratchTangents,
                    index,
                    count,
                    configuration.Join,
                    configuration.MiterLimit,
                    out float joinScale);
                float halfWidth = baseHalfWidth *
                                  Mathf.Max(0.05f, pressure[start + index]);
                float outerWidth = halfWidth + softness;
                Vector2 offset = normal * outerWidth * joinScale;
                float cross = outerWidth / Mathf.Max(MinimumExtent, halfWidth);
                Vector2 position = points[start + index];
                if (configuration.Cap == TextStudioWritingStrokeCap.Square)
                {
                    if (index == 0)
                        position -= _scratchTangents[0] * baseHalfWidth;
                    else if (index == count - 1)
                        position += _scratchTangents[count - 1] * baseHalfWidth;
                }
                AddVertex(
                    position - offset,
                    color,
                    distance[start + index],
                    -cross,
                    sampleIndex,
                    transformIndex,
                    in glyph);
                AddVertex(
                    position + offset,
                    color,
                    distance[start + index],
                    cross,
                    sampleIndex,
                    transformIndex,
                    in glyph);
                if (index > 0)
                {
                    int previous = vertexStart + (index - 1) * 2;
                    int current = vertexStart + index * 2;
                    _geometry.Triangles.Add(previous);
                    _geometry.Triangles.Add(current + 1);
                    _geometry.Triangles.Add(previous + 1);
                    _geometry.Triangles.Add(previous);
                    _geometry.Triangles.Add(current);
                    _geometry.Triangles.Add(current + 1);
                }
            }

            if (configuration.Join == TextStudioWritingStrokeJoin.Round)
            {
                for (int index = 1; index < count - 1; index++)
                {
                    EmitRoundDisc(
                        points[start + index],
                        baseHalfWidth * Mathf.Max(
                            0.05f,
                            pressure[start + index]),
                        softness,
                        color,
                        distance[start + index],
                        sampleIndex,
                        transformIndex,
                        in glyph);
                }
            }
            if (configuration.Cap == TextStudioWritingStrokeCap.Round)
            {
                EmitRoundDisc(
                    points[start],
                    baseHalfWidth * Mathf.Max(0.05f, pressure[start]),
                    softness,
                    color,
                    distance[start],
                    sampleIndex,
                    transformIndex,
                    in glyph);
                EmitRoundDisc(
                    points[start + count - 1],
                    baseHalfWidth * Mathf.Max(
                        0.05f,
                        pressure[start + count - 1]),
                    softness,
                    color,
                    distance[start + count - 1],
                    sampleIndex,
                    transformIndex,
                    in glyph);
            }
        }

        void EmitRoundDisc(
            Vector2 center,
            float radius,
            float softness,
            Color32 color,
            float distance,
            int sampleIndex,
            int transformIndex,
            in TextStudioVisualGlyph glyph)
        {
            int centerIndex = _geometry.Vertices.Count;
            AddVertex(
                center,
                color,
                distance,
                0f,
                sampleIndex,
                transformIndex,
                in glyph);
            float outer = radius + softness;
            float cross = outer / Mathf.Max(MinimumExtent, radius);
            for (int segment = 0; segment <= RoundSegments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / RoundSegments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(
                    center + direction * outer,
                    color,
                    distance,
                    cross,
                    sampleIndex,
                    transformIndex,
                    in glyph);
                if (segment == 0)
                    continue;
                _geometry.Triangles.Add(centerIndex);
                _geometry.Triangles.Add(centerIndex + segment);
                _geometry.Triangles.Add(centerIndex + segment + 1);
            }
        }

        void AddVertex(
            Vector2 position,
            Color32 color,
            float distance,
            float cross,
            int sampleIndex,
            int transformIndex,
            in TextStudioVisualGlyph glyph)
        {
            _geometry.Vertices.Add(new Vector3(position.x, position.y, 0f));
            _geometry.Colors.Add(color);
            _geometry.Uv0.Add(new Vector4(distance, cross, 0f, 0f));
            _geometry.Uv1.Add(new Vector4(
                sampleIndex,
                transformIndex,
                0f,
                0f));
            _geometry.Uv2.Add(new Vector4(
                Normalize(position.x, glyph.BoundsMin.x, glyph.BoundsMax.x),
                Normalize(position.y, glyph.BoundsMin.y, glyph.BoundsMax.y),
                0f,
                0f));
            _geometry.Uv3.Add(Vector4.zero);
        }

        static Vector2 ResolveJoinNormal(
            Vector2[] tangents,
            int index,
            int count,
            TextStudioWritingStrokeJoin join,
            float miterLimit,
            out float scale)
        {
            Vector2 currentNormal = new Vector2(-tangents[index].y, tangents[index].x);
            if (index == 0 || index == count - 1)
            {
                scale = 1f;
                return currentNormal;
            }
            Vector2 previousNormal = new Vector2(
                -tangents[index - 1].y,
                tangents[index - 1].x);
            Vector2 miter = previousNormal + currentNormal;
            if (miter.sqrMagnitude <= 0.00000001f)
            {
                scale = 1f;
                return currentNormal;
            }
            miter.Normalize();
            float denominator = Mathf.Abs(Vector2.Dot(miter, currentNormal));
            float resolved = 1f / Mathf.Max(0.1f, denominator);
            scale = join == TextStudioWritingStrokeJoin.Miter
                ? Mathf.Min(Mathf.Max(1f, miterLimit), resolved)
                : Mathf.Min(1.5f, resolved);
            return miter;
        }

        void EnsureScratchCapacity(int count, bool includeGlyphs)
        {
            if (count <= 0)
                return;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            if (_scratchPoints.Length < count)
                Array.Resize(ref _scratchPoints, capacity);
            if (_scratchPressure.Length < count)
                Array.Resize(ref _scratchPressure, capacity);
            if (_scratchDistance.Length < count)
                Array.Resize(ref _scratchDistance, capacity);
            if (_scratchTangents.Length < count)
                Array.Resize(ref _scratchTangents, capacity);
            if (includeGlyphs && _scratchGlyphs.Length < count)
                Array.Resize(ref _scratchGlyphs, capacity);
        }

        void EnsureTangentScratchCapacity(int count)
        {
            if (_scratchTangents.Length >= count)
                return;
            Array.Resize(
                ref _scratchTangents,
                TextStudioBufferCapacity.ForCount(count));
        }

        static Vector2 MapRect(
            Vector2 point,
            Rect source,
            Vector2 targetMinimum,
            Vector2 targetMaximum)
        {
            return TextStudioWritingPathMapping.MapRect(
                point,
                source,
                targetMinimum,
                targetMaximum);
        }

        static int FindNearestGlyph(
            in TextStudioVisualSnapshot snapshot,
            Vector2 position)
        {
            return TextStudioWritingPathMapping.FindNearestGlyph(
                in snapshot,
                position);
        }

        static float Normalize(float value, float minimum, float maximum)
        {
            return TextStudioWritingPathMapping.Normalize(
                value,
                minimum,
                maximum);
        }

        void UploadGeometry(TMP_Text tmpText)
        {
            if (_geometry.Vertices.Count == 0 || s_material == null)
            {
                HideHosts();
                return;
            }
            if (tmpText is TextMeshProUGUI)
            {
                DestroyWorldHost();
                EnsureUiHost();
                SynchronizeUiTransform();
                _uiGraphic.ApplyMaterial(s_material);
                _uiGraphic.SetGeometry(_geometry);
                PlaceUiAfterSource();
                Canvas canvas = _hub.GetComponentInParent<Canvas>();
                TextStudioCanvasShaderChannelLeases.Acquire(
                    canvas,
                    AdditionalCanvasShaderChannels.TexCoord2 |
                    AdditionalCanvasShaderChannels.TexCoord3,
                    ref _canvasChannelLease);
            }
            else
            {
                DestroyUiHost();
                EnsureWorldHost();
                UploadWorldMesh();
                SynchronizeWorldRenderer(tmpText);
            }
        }

        void SynchronizeHost(TMP_Text tmpText)
        {
            if (_geometry.Vertices.Count == 0)
                return;
            if (tmpText is TextMeshProUGUI)
            {
                SynchronizeUiTransform();
                PlaceUiAfterSource();
            }
            else
            {
                SynchronizeWorldRenderer(tmpText);
            }
        }

        void EnsureWorldHost()
        {
            if (_worldHost != null)
                return;
            _worldHost = new GameObject(WorldHostName)
            {
                hideFlags = HostHideFlags,
                layer = _hub.gameObject.layer
            };
            _worldHost.transform.SetParent(_hub.transform, false);
            MeshFilter filter = _worldHost.AddComponent<MeshFilter>();
            _worldRenderer = _worldHost.AddComponent<MeshRenderer>();
            _worldMesh = new Mesh
            {
                name = WorldHostName + " Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            _worldMesh.MarkDynamic();
            filter.sharedMesh = _worldMesh;
            _worldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _worldRenderer.receiveShadows = false;
            _worldRenderer.lightProbeUsage = LightProbeUsage.Off;
            _worldRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _worldRenderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            _worldRenderer.allowOcclusionWhenDynamic = false;
        }

        void UploadWorldMesh()
        {
            _worldMesh.Clear();
            _worldMesh.indexFormat = _geometry.Vertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            _worldMesh.SetVertices(_geometry.Vertices);
            _worldMesh.SetColors(_geometry.Colors);
            _worldMesh.SetUVs(0, _geometry.Uv0);
            _worldMesh.SetUVs(1, _geometry.Uv1);
            _worldMesh.SetUVs(2, _geometry.Uv2);
            _worldMesh.SetUVs(3, _geometry.Uv3);
            _worldMesh.SetTriangles(_geometry.Triangles, 0, true);
            _worldRenderer.sharedMaterial = s_material;
            _worldRenderer.enabled = true;
        }

        void SynchronizeWorldRenderer(TMP_Text tmpText)
        {
            if (_worldRenderer == null)
                return;
            MeshRenderer source = tmpText != null
                ? tmpText.GetComponent<MeshRenderer>()
                : null;
            _worldRenderer.sortingLayerID = source != null
                ? source.sortingLayerID
                : 0;
            _worldRenderer.sortingOrder = (source != null ? source.sortingOrder : 0) + 1;
            if (source != null)
                _worldRenderer.renderingLayerMask = source.renderingLayerMask;
        }

        void EnsureUiHost()
        {
            RectTransform source = _hub.transform as RectTransform;
            Transform parent = source != null ? source.parent : null;
            if (source == null || parent == null)
                return;
            if (_uiHost != null && _uiTransform != null && _uiTransform.parent == parent)
                return;
            DestroyUiHost();
            _uiHost = new GameObject(UiHostName)
            {
                hideFlags = HostHideFlags,
                layer = _hub.gameObject.layer
            };
            _uiTransform = _uiHost.AddComponent<RectTransform>();
            _uiTransform.SetParent(parent, false);
            _uiHost.AddComponent<CanvasRenderer>();
            LayoutElement layout = _uiHost.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            _uiGraphic = _uiHost.AddComponent<TextStudio2DStyleDecorationGraphic>();
            _uiGraphic.raycastTarget = false;
        }

        void SynchronizeUiTransform()
        {
            RectTransform source = _hub.transform as RectTransform;
            if (_uiTransform == null || source == null)
                return;
            _uiTransform.anchorMin = source.anchorMin;
            _uiTransform.anchorMax = source.anchorMax;
            _uiTransform.pivot = source.pivot;
            _uiTransform.anchoredPosition3D = source.anchoredPosition3D;
            _uiTransform.sizeDelta = source.sizeDelta;
            _uiTransform.localRotation = source.localRotation;
            _uiTransform.localScale = source.localScale;
        }

        void PlaceUiAfterSource()
        {
            if (_uiTransform == null)
                return;
            Transform source = _hub.transform;
            int current = _uiTransform.GetSiblingIndex();
            int reference = source.GetSiblingIndex();
            int desired = current < reference ? reference : reference + 1;
            if (current != desired)
                _uiTransform.SetSiblingIndex(desired);
        }

        void HideHosts()
        {
            if (_worldRenderer != null)
                _worldRenderer.enabled = false;
            if (_worldMesh != null)
                _worldMesh.Clear();
            _uiGraphic?.ClearGeometry();
            TextStudioCanvasShaderChannelLeases.Release(ref _canvasChannelLease);
        }

        void DestroyWorldHost()
        {
            if (_worldRenderer != null) _worldRenderer.enabled = false;
            if (_worldMesh != null)
                DestroyObject(_worldMesh);
            if (_worldHost != null)
                DestroyObject(_worldHost);
            _worldHost = null;
            _worldMesh = null;
            _worldRenderer = null;
        }

        void DestroyUiHost()
        {
            _uiGraphic?.ClearGeometry();
            TextStudioCanvasShaderChannelLeases.Release(ref _canvasChannelLease);
            if (_uiHost != null)
                DestroyObject(_uiHost);
            _uiHost = null;
            _uiTransform = null;
            _uiGraphic = null;
        }

        static void DestroyObject(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(value);
            else if (!(value is GameObject) || !TextStudioEditorHooks.TryDestroyAfterEditorLifecycle(value))
                Object.DestroyImmediate(value);
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void ReleaseSharedMaterialForPlayMode()
        {
            DestroyObject(s_material);
            s_material = null;
            s_missingShaderWarningLogged = false;
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseSharedMaterialForCodeUnload()
        {
            ReleaseSharedMaterialForPlayMode();
            s_materialReferences = 0;
        }
#endif

        static void AcquireMaterial()
        {
            s_materialReferences++;
            EnsureMaterialAvailable();
        }

        static void EnsureMaterialAvailable()
        {
            if (IsMaterialUsable())
                return;
            DestroyObject(s_material);
            s_material = null;
            Shader shader = TextStudio2DShaderResources.LoadSupported(
                TextStudio2DResourceKeys.WritingShader,
                "Writing treatments",
                ref s_missingShaderWarningLogged,
                out string _);
            if (shader == null)
                return;
            s_material = new Material(shader)
            {
                name = "Text Studio Writing",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static bool IsMaterialUsable()
        {
            return s_material != null &&
                   TextStudio2DShaderResources.IsUsable(s_material.shader);
        }

        static void ReleaseMaterial()
        {
            s_materialReferences = Mathf.Max(0, s_materialReferences - 1);
            if (s_materialReferences > 0 || s_material == null)
                return;
            DestroyObject(s_material);
            s_material = null;
        }

        public void Dispose()
        {
            ReleaseGpuHandle();
            DestroyWorldHost();
            DestroyUiHost();
            CompatibilityWarning = string.Empty;
            ReleaseMaterial();
        }
    }
}
