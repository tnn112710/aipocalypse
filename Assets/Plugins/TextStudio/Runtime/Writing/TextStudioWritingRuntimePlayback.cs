using UnityEngine;

namespace TextStudio
{
    /// <summary>Absolute shared-table ranges for one renderer's Writing frame.</summary>
    internal readonly struct TextStudioWritingGpuRanges
    {
        internal readonly int SampleOffset;
        internal readonly int TransformOffset;
        internal readonly int GlyphCount;

        internal TextStudioWritingGpuRanges(
            int sampleOffset,
            int transformOffset,
            int glyphCount)
        {
            SampleOffset = sampleOffset;
            TransformOffset = transformOffset;
            GlyphCount = glyphCount;
        }

        internal int ResolveInvocationSampleIndex(
            int invocationIndex,
            int glyphIndex)
        {
            return SampleOffset +
                   GlyphCount * (invocationIndex + 1) +
                   glyphIndex;
        }

        internal int ResolveTransformIndex(int glyphIndex)
        {
            return TransformOffset + glyphIndex;
        }
    }

    /// <summary>
    /// Reports which renderer-facing retained rows changed during one shared Writing update.
    /// Renderers use this to synchronize only the transport mirrors they actually need.
    /// </summary>
    internal readonly struct TextStudioWritingRuntimePlaybackUpdate
    {
        internal readonly bool SamplesChanged;
        internal readonly bool SourceSamplesChanged;
        internal readonly bool TransformsChanged;

        internal TextStudioWritingRuntimePlaybackUpdate(
            bool samplesChanged,
            bool sourceSamplesChanged,
            bool transformsChanged)
        {
            SamplesChanged = samplesChanged;
            SourceSamplesChanged = sourceSamplesChanged;
            TransformsChanged = transformsChanged;
        }
    }

    /// <summary>
    /// Shared allocation-stable Writing playback for every renderer. Source resolution and
    /// topology stay renderer-specific, while transition sampling, reduced motion, packed samples,
    /// animated glyph transforms, dirty comparison, and GPU arena lifetime remain canonical.
    /// </summary>
    internal sealed class TextStudioWritingRuntimePlayback
    {
        TextStudioWritingGpuHandle _gpuHandle;
        int _sampleOffset = -1;
        int _transformOffset = -1;
        int _sampleEvaluationGlyphCount = -1;
        ulong _sampleEvaluationFingerprint;
        ulong _sampleEvaluationProgress;
        TextStudioMotionPolicy _sampleEvaluationMotionPolicy;
        bool _hasSampleEvaluationState;

        internal int SourceSampleOffset => _sampleOffset;
        internal bool IsAllocated => _gpuHandle.IsValid;

        internal bool TryEnsureRanges(
            int glyphCount,
            int invocationCount,
            out TextStudioWritingGpuRanges ranges,
            out bool allocationChanged)
        {
            ranges = default;
            allocationChanged = false;
            if (glyphCount <= 0 || invocationCount < 0 ||
                invocationCount >= TextStudioWritingGpuTable.MaximumCapacity ||
                glyphCount > TextStudioWritingGpuTable.MaximumCapacity /
                (invocationCount + 1))
            {
                Release();
                return false;
            }

            int requiredSamples = glyphCount * (invocationCount + 1);
            TextStudioWritingGpuHandle previousHandle = _gpuHandle;
            int previousSampleOffset = _sampleOffset;
            int previousTransformOffset = _transformOffset;
            if (!TextStudioWritingGpuTable.Shared.TryEnsureRanges(
                    ref _gpuHandle,
                    requiredSamples,
                    glyphCount,
                    out int sampleOffset,
                    out int transformOffset))
            {
                Release();
                return false;
            }

            _sampleOffset = sampleOffset;
            _transformOffset = transformOffset;
            allocationChanged = !_gpuHandle.Equals(previousHandle) ||
                                sampleOffset != previousSampleOffset ||
                                transformOffset != previousTransformOffset;
            ranges = new TextStudioWritingGpuRanges(
                sampleOffset,
                transformOffset,
                glyphCount);
            return true;
        }

        internal TextStudioWritingRuntimePlaybackUpdate EvaluateAndUpload(
            in TextStudioFrame frame,
            TextStudioWritingRuntimeInvocationSet invocations,
            TextStudioMotionPolicy motionPolicy,
            bool topologyChanged)
        {
            int glyphCount = frame.VisualSnapshot.GlyphCount;
            if (!_gpuHandle.IsValid || invocations == null ||
                glyphCount <= 0)
            {
                return default;
            }

            bool samplesChanged = false;
            bool sourceSamplesChanged = false;
            if (invocations.Count > 0 &&
                !CanReuseDirectSamples(
                    in frame,
                    invocations,
                    motionPolicy,
                    glyphCount,
                    topologyChanged))
            {
                samplesChanged = EvaluateInvocations(
                    in frame,
                    invocations,
                    motionPolicy,
                    glyphCount,
                    out sourceSamplesChanged);
                CaptureSampleEvaluation(
                    in frame,
                    invocations,
                    motionPolicy,
                    glyphCount);
            }
            else if (invocations.Count == 0)
            {
                InvalidateSampleEvaluation();
            }
            bool transformsChanged = UpdateTransforms(
                in frame,
                glyphCount,
                topologyChanged);
            TextStudioWritingGpuTable.Shared.FlushAndBind();
            return new TextStudioWritingRuntimePlaybackUpdate(
                samplesChanged,
                sourceSamplesChanged,
                transformsChanged);
        }

        internal TextStudioGpuWritingSample[] GetSamples(
            out int sampleOffset,
            out int sampleCount)
        {
            return TextStudioWritingGpuTable.Shared.GetSampleBuffer(
                _gpuHandle,
                out sampleOffset,
                out sampleCount);
        }

        internal TextStudioGpuWritingTransform[] GetTransforms(
            out int transformOffset,
            out int transformCount)
        {
            return TextStudioWritingGpuTable.Shared.GetTransformBuffer(
                _gpuHandle,
                out transformOffset,
                out transformCount);
        }

        internal void UploadTransformRange(int firstGlyph, int count)
        {
            TextStudioWritingGpuTable table = TextStudioWritingGpuTable.Shared;
            table.MarkTransformsDirty(_gpuHandle, firstGlyph, count);
            table.FlushAndBind();
        }

        bool CanReuseDirectSamples(
            in TextStudioFrame frame,
            TextStudioWritingRuntimeInvocationSet invocations,
            TextStudioMotionPolicy motionPolicy,
            int glyphCount,
            bool topologyChanged)
        {
            if (topologyChanged ||
                !_hasSampleEvaluationState ||
                invocations.Count != 1 ||
                invocations[0].HasRegistration ||
                _sampleEvaluationGlyphCount != glyphCount ||
                _sampleEvaluationFingerprint != invocations.BuildFingerprint() ||
                _sampleEvaluationMotionPolicy !=
                    TextStudioMotionSettings.Resolve(motionPolicy))
            {
                return false;
            }

            return _sampleEvaluationProgress.Equals(
                ResolveDirectProgress(in frame, invocations[0]));
        }

        void CaptureSampleEvaluation(
            in TextStudioFrame frame,
            TextStudioWritingRuntimeInvocationSet invocations,
            TextStudioMotionPolicy motionPolicy,
            int glyphCount)
        {
            if (invocations.Count != 1 || invocations[0].HasRegistration)
            {
                InvalidateSampleEvaluation();
                return;
            }

            _sampleEvaluationGlyphCount = glyphCount;
            _sampleEvaluationFingerprint = invocations.BuildFingerprint();
            _sampleEvaluationProgress = ResolveDirectProgress(
                in frame,
                invocations[0]);
            _sampleEvaluationMotionPolicy =
                TextStudioMotionSettings.Resolve(motionPolicy);
            _hasSampleEvaluationState = true;
        }

        static ulong ResolveDirectProgress(in TextStudioFrame frame, TextStudioWritingRuntimeInvocation invocation)
        {
            // Hash every local phase: aggregate progress can remain fixed while a paused letter settles.
            unchecked
            {
                ulong hash = 17UL;
                for (int i = 0; i < frame.VisualSnapshot.GlyphCount; i++)
                {
                    int logical = frame.VisualSnapshot.GetGlyph(i).LogicalIndex;
                    TextStudioGlyphTransition transition = frame.Transitions.GetGlyph(logical);
                    hash = hash * 31 + (uint)transition.GetWritingProgress(frame.EraseWritingOnHide).GetHashCode();
                    hash = hash * 31 + (uint)transition.Phase;
                }
                return hash;
            }
        }

        bool EvaluateInvocations(
            in TextStudioFrame frame,
            TextStudioWritingRuntimeInvocationSet invocations,
            TextStudioMotionPolicy motionPolicy,
            int glyphCount,
            out bool sourceSamplesChanged)
        {
            sourceSamplesChanged = false;
            TextStudioWritingGpuTable table = TextStudioWritingGpuTable.Shared;
            TextStudioGpuWritingSample[] samples = table.GetSampleBuffer(
                _gpuHandle,
                out int sampleOffset,
                out _);
            if (samples == null)
                return false;

            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            int invocationCount = invocations.Count;
            int minimumChanged = glyphCount * (invocationCount + 1);
            int maximumChanged = -1;
            for (int invocationIndex = 0;
                 invocationIndex < invocationCount;
                 invocationIndex++)
            {
                TextStudioWritingRuntimeInvocation invocation =
                    invocations[invocationIndex];
                TextStudioWritingConfiguration configuration =
                    invocation.Style.Configuration;
                invocation.EnsureCapacity(glyphCount);
                float strokeGap = configuration.StrokeGap;
                bool settle = TextStudioWritingPlaybackPolicy.Resolve(
                    motionPolicy, configuration.ReducedMotionBehavior, ref strokeGap);
                int invocationLocalOffset = glyphCount * (invocationIndex + 1);
                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    ResolveInvocationProgress(
                        invocation,
                        in frame,
                        glyphIndex,
                        settle,
                        out float rawProgress,
                        out float opacity);
                    float progress = rawProgress;
                    float sourceAlpha =
                        TextStudioWritingScheduleMath.ResolveSourceAlpha(
                            in configuration,
                            invocation.SourceKinds[glyphIndex],
                            progress);
                    invocation.Progress[glyphIndex] = progress;
                    invocation.Opacity[glyphIndex] = opacity;
                    invocation.SourceAlpha[glyphIndex] = sourceAlpha;
                    TextStudioGpuWritingSample packed =
                        TextStudioWritingGpuPacking.Pack(
                            progress,
                            opacity,
                            sourceAlpha,
                            TextStudioWritingScheduleMath.ResolveGpuMode(
                                configuration.Mode,
                                invocation.SourceKinds[glyphIndex]),
                            configuration.Direction,
                            configuration.Reverse,
                            configuration.EdgeSoftness,
                            configuration.Brush,
                            opacity > 0f);
                    SetSampleIfChanged(
                        samples,
                        sampleOffset,
                        invocationLocalOffset + glyphIndex,
                        in packed,
                        ref minimumChanged,
                        ref maximumChanged);
                }
            }

            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                float scalarAlpha = 1f;
                bool active = false;
                float selectedProgress = 1f;
                float selectedOpacity = 1f;
                TextStudioWritingMode selectedMode = TextStudioWritingMode.None;
                TextStudioWritingDirection selectedDirection =
                    TextStudioWritingDirection.LeftToRight;
                bool selectedReverse = false;
                float selectedSoftness = 0f;
                for (int invocationIndex = 0;
                     invocationIndex < invocationCount;
                     invocationIndex++)
                {
                    TextStudioWritingRuntimeInvocation invocation =
                        invocations[invocationIndex];
                    float opacity = invocation.Opacity[glyphIndex];
                    if (opacity <= 0f)
                        continue;
                    TextStudioWritingConfiguration configuration =
                        invocation.Style.Configuration;
                    active = true;
                    TextStudioWritingSourceKind sourceKind =
                        invocation.SourceKinds[glyphIndex];
                    if (TextStudioWritingScheduleMath.ResolveGpuMode(
                            configuration.Mode, sourceKind) != TextStudioWritingMode.None)
                    {
                        selectedProgress = invocation.Progress[glyphIndex];
                        selectedOpacity = opacity;
                        selectedMode =
                            TextStudioWritingScheduleMath.ResolveGpuMode(
                                configuration.Mode,
                                sourceKind);
                        selectedDirection = configuration.Direction;
                        selectedReverse = configuration.Reverse;
                        selectedSoftness = configuration.EdgeSoftness;
                    }
                    else
                    {
                        scalarAlpha *= Mathf.Lerp(
                            1f,
                            invocation.SourceAlpha[glyphIndex],
                            opacity);
                    }
                }
                TextStudioGpuWritingSample packed =
                    TextStudioWritingGpuPacking.Pack(
                        selectedProgress,
                        selectedOpacity,
                        scalarAlpha,
                        selectedMode,
                        selectedDirection,
                        selectedReverse,
                        selectedSoftness,
                        TextStudioWritingBrush.Solid,
                        active);
                SetSampleIfChanged(
                    samples,
                    sampleOffset,
                    glyphIndex,
                    in packed,
                    ref minimumChanged,
                    ref maximumChanged);
            }
            if (maximumChanged >= minimumChanged)
            {
                table.MarkSamplesDirty(
                    _gpuHandle,
                    minimumChanged,
                    maximumChanged - minimumChanged + 1);
            }
            sourceSamplesChanged = minimumChanged < glyphCount;
            return maximumChanged >= minimumChanged;
        }

        static void SetSampleIfChanged(
            TextStudioGpuWritingSample[] samples,
            int sampleOffset,
            int localIndex,
            in TextStudioGpuWritingSample value,
            ref int minimumChanged,
            ref int maximumChanged)
        {
            int targetIndex = sampleOffset + localIndex;
            if (samples[targetIndex].Equals(in value))
                return;
            samples[targetIndex] = value;
            minimumChanged = Mathf.Min(minimumChanged, localIndex);
            maximumChanged = Mathf.Max(maximumChanged, localIndex);
        }

        static void ResolveInvocationProgress(
            TextStudioWritingRuntimeInvocation invocation,
            in TextStudioFrame frame,
            int glyphIndex,
            bool settle,
            out float progress,
            out float opacity)
        {
            TextStudioGlyphTransition transition = frame.Transitions.GetGlyph(frame.VisualSnapshot.GetGlyph(glyphIndex).LogicalIndex);
            progress = transition.GetWritingProgress(frame.EraseWritingOnHide);
            opacity = 1f;
            if (settle && transition.Visible && transition.Phase == AnimationPhase.Appearance)
                progress = 1f;
            if (!invocation.HasRegistration) return;
            if (frame.RenderProperties.TryGet(
                    in invocation.Registration,
                    glyphIndex,
                    out TextStudioRenderPropertyValue value,
                    out _))
            {
                Vector4 packed = value.Vector;
                opacity = Mathf.Clamp01(packed.y);
                progress = opacity > 0.000001f ? invocation.Layer.EvaluateWritingCurve(progress) : 0f;
                return;
            }
            progress = 0f;
            opacity = 0f;
        }

        bool UpdateTransforms(
            in TextStudioFrame frame,
            int glyphCount,
            bool force)
        {
            TextStudioWritingGpuTable table = TextStudioWritingGpuTable.Shared;
            TextStudioGpuWritingTransform[] transforms = table.GetTransformBuffer(
                _gpuHandle,
                out int transformOffset,
                out _);
            if (transforms == null)
                return false;

            int minimumChanged = glyphCount;
            int maximumChanged = -1;
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                if (!force && frame.Dirty.IsCreated &&
                    !frame.Dirty.IsDirty(glyphIndex))
                {
                    continue;
                }
                TextStudioGpuWritingTransform next = BuildTransform(
                    in frame,
                    glyphIndex);
                int targetIndex = transformOffset + glyphIndex;
                if (!force && transforms[targetIndex].Equals(in next))
                    continue;
                transforms[targetIndex] = next;
                minimumChanged = Mathf.Min(minimumChanged, glyphIndex);
                maximumChanged = Mathf.Max(maximumChanged, glyphIndex);
            }
            if (maximumChanged >= minimumChanged)
            {
                table.MarkTransformsDirty(
                    _gpuHandle,
                    minimumChanged,
                    maximumChanged - minimumChanged + 1);
                return true;
            }
            return false;
        }

        internal static TextStudioGpuWritingTransform BuildTransform(
            in TextStudioFrame frame,
            int glyphIndex)
        {
            TextStudioVisualGlyph glyph =
                frame.VisualSnapshot.GetGlyph(glyphIndex);
            EvaluatedGlyphState state = frame.Animated.IsCreated &&
                                        glyphIndex < frame.AnimatedCount
                ? frame.Animated[glyphIndex]
                : EvaluatedGlyphState.Identity;
            GlyphPlacement placement = default;
            bool hasPlacement = frame.Placements.IsCreated &&
                                glyphIndex < frame.PlacementCount;
            if (hasPlacement)
                placement = frame.Placements[glyphIndex];
            Vector3 bottomLeft = new Vector3(
                glyph.BoundsMin.x,
                glyph.BoundsMin.y,
                0f);
            Vector3 bottomRight = new Vector3(
                glyph.BoundsMax.x,
                glyph.BoundsMin.y,
                0f);
            Vector3 topLeft = new Vector3(
                glyph.BoundsMin.x,
                glyph.BoundsMax.y,
                0f);
            Vector3 topRight = new Vector3(
                glyph.BoundsMax.x,
                glyph.BoundsMax.y,
                0f);
            Vector3 center = (bottomLeft + topRight) * 0.5f;
            TextStudioGlyphQuadTransform transform =
                TextStudioGlyphQuadTransformer.CreateFromQuad(
                    bottomLeft,
                    topLeft,
                    topRight,
                    bottomRight,
                    center,
                    in state,
                    in placement,
                    hasPlacement,
                    frame.GetEffectUnitScale(glyphIndex),
                    1f);
            Vector3 origin = transform.ApplyPosition(Vector3.zero);
            Vector3 axisX =
                transform.ApplyPosition(Vector3.right) - origin;
            Vector3 axisY = transform.ApplyPosition(Vector3.up) - origin;
            Vector3 axisZ =
                transform.ApplyPosition(Vector3.forward) - origin;
            TextStudioCornerOffsets corners = state.cornerOffsets;
            return new TextStudioGpuWritingTransform
            {
                MatrixRow0 = new Vector4(
                    axisX.x,
                    axisY.x,
                    axisZ.x,
                    origin.x),
                MatrixRow1 = new Vector4(
                    axisX.y,
                    axisY.y,
                    axisZ.y,
                    origin.y),
                MatrixRow2 = new Vector4(
                    axisX.z,
                    axisY.z,
                    axisZ.z,
                    origin.z),
                CornerBottomLeftTopLeft = new Vector4(
                    corners.BottomLeft.x,
                    corners.BottomLeft.y,
                    corners.TopLeft.x,
                    corners.TopLeft.y),
                CornerTopRightBottomRight = new Vector4(
                    corners.TopRight.x,
                    corners.TopRight.y,
                    corners.BottomRight.x,
                    corners.BottomRight.y),
                ColorMultiplierAndAlpha = new Vector4(
                    state.colorMultiplier.r,
                    state.colorMultiplier.g,
                    state.colorMultiplier.b,
                    state.alpha),
                ColorAdditiveAndEffectScale = new Vector4(
                    state.colorAdditive.r,
                    state.colorAdditive.g,
                    state.colorAdditive.b,
                    frame.GetEffectUnitScale(glyphIndex))
            };
        }

        internal void InvalidateSampleEvaluation()
        {
            _sampleEvaluationGlyphCount = -1;
            _sampleEvaluationFingerprint = 0UL;
            _sampleEvaluationProgress = 0UL;
            _sampleEvaluationMotionPolicy = TextStudioMotionPolicy.Inherit;
            _hasSampleEvaluationState = false;
        }

        internal void Release()
        {
            if (_gpuHandle.IsValid)
                TextStudioWritingGpuTable.Shared.Release(ref _gpuHandle);
            _sampleOffset = -1;
            _transformOffset = -1;
            InvalidateSampleEvaluation();
        }
    }
}
