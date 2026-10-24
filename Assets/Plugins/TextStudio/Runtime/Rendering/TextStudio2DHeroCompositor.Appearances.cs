using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal sealed partial class TextStudio2DHeroCompositor
    {
        readonly OccurrenceState _defaultOccurrenceState = new OccurrenceState();
        OccurrenceState _occurrenceState;
        readonly List<OccurrenceState> _appearanceStates = new List<OccurrenceState>();
        readonly List<OccurrenceState> _activeAppearanceStates = new List<OccurrenceState>();
        CompiledTextStudioAppearancePlan _appearancePlan;
        readonly TextStudio2DHeroTargetLease[] _appearanceAggregateLeases = new TextStudio2DHeroTargetLease[2];
        readonly RenderTexture[] _appearanceAggregateTextures = new RenderTexture[2];
        readonly TextStudio2DHeroTargetLease[] _appearanceShadowLeases = new TextStudio2DHeroTargetLease[2];
        readonly RenderTexture[] _appearanceShadowTextures = new RenderTexture[2];
        bool _hasSelectiveAppearanceShadows;
        static readonly int s_invocationTintProperty = Shader.PropertyToID("_TextStudioHeroInvocationTint");

        int _appearanceFullResolutionCount, _appearanceBlurCount, _appearanceBlurFactor;
        int _appearanceHistoryCount, _appearanceHistoryCoverageCount;
        Vector4 _appearanceSourceBounds;
        float _appearanceEffectUnitScale;

        internal bool RenderAppearance(in TextStudioFrame frame, TMP_Text tmpText,
            IReadOnlyList<TextStudioTMPAppearanceAdapter> programAdapters, TextStudio2DAppearanceBinding binding,
            in TextStudioLensMagnifierDescriptor lensMagnifier, TextStudioTMPMaterialEffectAdapter materialAdapter,
            TextStudioTMPFrameApplier frameApplier, bool forceRefresh, float projectedPixelsPerUnit = 0,
            TextStudioPreparedFieldDemand sourceDemand = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TextStudio2DHeroCompositor));
            _selectorPass.Begin();
            if (tmpText != null) SetViewSource(null);
            CompiledTextStudioAppearancePlan plan = frame.AppearancePlan;
            if (tmpText == null && _viewExternal == null || plan == null || binding == null || !frame.VisualSnapshot.IsCreated)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.MissingVisualSnapshot);
                return false;
            }
            bool firstAppearanceFrame = _appearancePlan == null;
            bool planChanged = !ReferenceEquals(_appearancePlan, plan);
            if (firstAppearanceFrame)
            {
                _occurrenceState = _defaultOccurrenceState;
                ReleaseHistoryTargets();
                ReleaseStaticRows();
            }
            _appearancePlan = plan;
            bool boundsChanged = !Mathf.Approximately(_projectedPixelsPerUnit, projectedPixelsPerUnit);
            _projectedPixelsPerUnit = projectedPixelsPerUnit;
            bool needsRender = forceRefresh || planChanged || boundsChanged || !HasOutput || _requiresContinuousRendering ||
                !_lastVisualVersion.Equals(frame.VisualSnapshot.Version) || _lastGlyphCount != frame.VisualSnapshot.GlyphCount ||
                frame.GlobalDirty != DirtyFlags.None || !frame.Dirty.IsCreated || frame.Dirty.AnyDirtyInRange(0, frame.AnimatedCount);
            while (_appearanceStates.Count < plan.Occurrences.Count) _appearanceStates.Add(null);
            for (int index = _appearanceStates.Count - 1; index >= plan.Occurrences.Count; index--)
            {
                ReleaseOccurrenceState(_appearanceStates[index]);
                _appearanceStates.RemoveAt(index);
            }
            _activeAppearanceStates.Clear();
            for (int index = 0; index < plan.Occurrences.Count; index++)
            {
                TextStudioAppearanceOccurrence occurrence = plan.Occurrences[index];
                if (!occurrence.IsActive || occurrence.VisibleRanges.Count == 0 ||
                    occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay && plan.Programs[occurrence.ProgramIndex].IsPassThrough)
                {
                    ReleaseOccurrenceState(_appearanceStates[index]);
                    _appearanceStates[index] = null;
                    continue;
                }
                if ((uint)occurrence.ProgramIndex >= (uint)programAdapters.Count ||
                    programAdapters[occurrence.ProgramIndex]?.HeroProgram == null)
                {
                    SetFailure(TextStudio2DHeroCompositorStatus.MissingMaterial);
                    CompatibilityWarning = "An active appearance program could not acquire its shared Hero resources.";
                    return false;
                }
                TextStudioTMPAppearanceAdapter adapter = programAdapters[occurrence.ProgramIndex];
                OccurrenceState state = _appearanceStates[index] ??= new OccurrenceState();
                bool scopeChanged = state.BindingRevision != binding.GetOccurrenceRevision(index);
                bool historyScopeChanged = state.HistoryBindingRevision != binding.GetOccurrenceHistoryRevision(index);
                state.BindingRevision = binding.GetOccurrenceRevision(index);
                state.HistoryBindingRevision = binding.GetOccurrenceHistoryRevision(index);
                state.Occurrence = occurrence;
                state.Program = adapter.HeroProgram;
                state.StyleSlot = adapter.HeroStyleSlot;
                _occurrenceState = state;
                _styleSlot = state.StyleSlot;
                _surfaceStyle = state.Program.Style;
                _sourceDemand = adapter.SourceDemand ?? sourceDemand;
                TextStudioFrame scopedFrame = new TextStudioFrame(in frame, state.Program.Style, index, binding);
                bool colorsChanged = UpdateColorSources(in scopedFrame, state.Program.Style);
                needsRender |= colorsChanged || scopeChanged;
                _surfacePixelsPerUnit = ResolveSourcePixelsPerUnit(in scopedFrame, _styleSlot);
                if (projectedPixelsPerUnit > 0) _surfacePixelsPerUnit = projectedPixelsPerUnit;
                state.SurfacePixelsPerUnit = _surfacePixelsPerUnit;
                bool refreshGeometry = forceRefresh || scopeChanged || colorsChanged ||
                    boundsChanged && state.Program.Style.RequiresCameraPixels ||
                    state.Program.Style.RequiresContourGeometry && state.GeometryPrecisionFingerprint != (_sourceDemand?.GeometryFingerprint ?? 0);
                if (CanReuseGeometry(in scopedFrame, state.Program, refreshGeometry, out Rect geometryBounds, out TextStudio2DQualityTier quality))
                    _geometryReuseCount++;
                else if (!BuildGeometry(in scopedFrame, tmpText, state.Program, materialAdapter, frameApplier, out geometryBounds, out quality))
                {
                    // A valid occurrence can currently contain only unrevealed or layout-free units.
                    if (Status != TextStudio2DHeroCompositorStatus.MissingGlyphGeometry) return false;
                    continue;
                }
                else CaptureGeometryCache(in scopedFrame, state.Program, in geometryBounds);
                state.Quality = quality;
                state.SourceBounds = binding.GetDomainBounds(index);
                state.SourceDensity = Mathf.Max(_surfacePixelsPerUnit, ResolveSourcePixelsPerUnit(in scopedFrame, _styleSlot));
                state.EffectUnitScale = ResolveAverageEffectUnitScale(in scopedFrame);
                state.ResetHistory = historyScopeChanged || ShouldResetHistory(in scopedFrame, state.Program);
                _activeAppearanceStates.Add(state);
            }
            if (_activeAppearanceStates.Count == 0)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.MissingGlyphGeometry);
                return false;
            }
            _viewText = tmpText;
            _viewLens = lensMagnifier;
            _currentRenderTime = frame.PresentationTime;
            _appearanceSourceBounds = frame.VisualSnapshot.SourceBounds;
            _appearanceEffectUnitScale = ResolveAverageEffectUnitScale(in frame);
            SynchronizeProjectionCallbacks();
            if (!needsRender && HasOutput)
            {
                _skippedStaticFrameCount++;
                SynchronizeOutput(tmpText);
                Status = TextStudio2DHeroCompositorStatus.Ready;
                CompatibilityWarning = string.Empty;
                return true;
            }
            bool rendered = RenderCachedAppearance(false);
            if (rendered)
            {
                _lastVisualVersion = frame.VisualSnapshot.Version;
                _lastGlyphCount = frame.VisualSnapshot.GlyphCount;
            }
            return rendered;
        }

        static IReadOnlyList<int> OccurrenceRenderGlyphs(in TextStudioFrame frame) =>
            frame.AppearanceOccurrenceIndex >= 0 && frame.AppearanceBinding != null
                ? frame.AppearanceBinding.GetOccurrenceRenderGlyphs(frame.AppearanceOccurrenceIndex) : null;

        static void GetOccurrenceUnitRange(in TextStudioFrame frame, out int start, out int end)
        {
            start = 0;
            end = frame.VisualSnapshot.GlyphCount;
            if (frame.AppearanceOccurrenceIndex < 0 || frame.AppearanceBinding == null) return;
            TextStudioAppearanceRange domain = frame.AppearanceBinding.Plan.Occurrences[frame.AppearanceOccurrenceIndex].Domain;
            start = Mathf.Clamp(domain.Start, 0, end);
            end = Mathf.Clamp(domain.End, start, end);
        }

        void BuildOccurrenceDrawingOrder(in TextStudioFrame frame)
        {
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            IReadOnlyList<int> glyphs = OccurrenceRenderGlyphs(in frame);
            if (glyphs == null)
            {
                TextStudio2DVisualOrder.Build(in snapshot, _occurrenceState.DrawingUnits, ref _occurrenceState.SeenDrawingUnits);
                return;
            }
            GetOccurrenceUnitRange(in frame, out int start, out int end);
            int count = end - start;
            _occurrenceState.DrawingUnits.Clear();
            if (_occurrenceState.SeenDrawingUnits.Length < count)
                _occurrenceState.SeenDrawingUnits = new bool[TextStudioBufferCapacity.ForCount(count)];
            Array.Clear(_occurrenceState.SeenDrawingUnits, 0, count);
            for (int index = 0; index < glyphs.Count; index++)
            {
                int unit = snapshot.RenderGlyphAt(glyphs[index]).TextUnitIndex;
                int local = unit - start;
                if ((uint)local >= (uint)count || _occurrenceState.SeenDrawingUnits[local] ||
                    !frame.AppearanceBinding.Contains(frame.AppearanceOccurrenceIndex, unit)) continue;
                _occurrenceState.SeenDrawingUnits[local] = true;
                _occurrenceState.DrawingUnits.Add(unit);
            }
        }

        void SelectOccurrence(OccurrenceState state)
        {
            _occurrenceState = state;
            _styleSlot = state.StyleSlot;
            _surfaceStyle = state.Program.Style;
            _viewProgram = state.Program;
            _sourceBounds = state.SourceBounds;
            _effectUnitScale = state.EffectUnitScale;
            _surfacePixelsPerUnit = state.SurfacePixelsPerUnit;
        }

        bool RenderCachedAppearance(bool resetAllHistory)
        {
            if (_activeAppearanceStates.Count == 0) return false;
            _scopeDepth = 0;
            _compositionFailed = false;
            _lastLayerDrawCount = _lastUniformRowDrawCount = _lastRowMaskDrawCount = _lastPlainRowDrawCount = _lastCachedRowCount = _lastSplitRowDrawCount = _lastCompositePassCount = _lastEffectPassCount = _lastPublishPassCount = 0;
            int pairs = 0, spatial = 0, coverage = 0;
            bool generatedAlpha = false, hasBounds = false, postProcess = _viewLens.IsActive;
            bool anyShadow = false, anyWithoutShadow = false;
            _appearanceBlurCount = 0; _appearanceBlurFactor = ProgressiveBlurDownsampleFactor;
            _appearanceHistoryCount = _appearanceHistoryCoverageCount = 0;
            _viewQuality = TextStudio2DQualityTier.Responsive;
            _viewDensity = 0;
            Rect targetBounds = default;
            foreach (OccurrenceState state in _activeAppearanceStates)
            {
                SelectOccurrence(state);
                bool castsShadow = state.Program.Style.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped;
                anyShadow |= castsShadow;
                anyWithoutShadow |= !castsShadow;
                Rect bounds = ResolveProjectedGeometryBounds();
                bounds = ExpandForHeroPostProcessing(in bounds, state.Program);
                targetBounds = hasBounds ? UnionBounds(targetBounds, bounds) : bounds;
                hasBounds = true;
                pairs = Mathf.Max(pairs, state.Program.CompositionPairCount);
                spatial = Mathf.Max(spatial, state.Program.SpatialScratchCount);
                coverage = Mathf.Max(coverage, state.Program.CoveragePairCount);
                generatedAlpha |= state.Program.NeedsGeneratedAlpha;
                _appearanceBlurCount = Mathf.Max(_appearanceBlurCount, state.Program.DownsampledBlurScratchCount);
                if (state.Program.DownsampledBlurScratchCount > 0)
                    _appearanceBlurFactor = Mathf.Min(_appearanceBlurFactor, state.Program.DownsampledBlurFactor);
                _appearanceHistoryCount += state.Program.MaximumHistoryFrames;
                if (state.Program.NeedsHistoryCoverage) _appearanceHistoryCoverageCount += state.Program.MaximumHistoryFrames;
                postProcess |= state.Program.RequiresPostProcess;
                _viewDensity = Mathf.Max(_viewDensity, state.SourceDensity);
                if ((int)state.Quality > (int)_viewQuality) _viewQuality = state.Quality;
            }
            _sourceBounds = _appearanceSourceBounds;
            _effectUnitScale = _appearanceEffectUnitScale;
            if (_viewLens.IsActive)
            {
                targetBounds = ExpandForLensMagnifier(in targetBounds, in _viewLens);
                spatial = Mathf.Max(spatial, 1);
            }
            _worldShadowMode = anyShadow ? TextStudio2DWorldShadowMode.AlphaClipped : TextStudio2DWorldShadowMode.Off;
            _hasSelectiveAppearanceShadows = anyShadow && anyWithoutShadow && !ViewIsCanvas;
            if (!_hasSelectiveAppearanceShadows) ReleaseAppearanceShadowTargets(true);
            // One output, one layer target, one root ping-pong partner, and two aggregate partners.
            _appearanceFullResolutionCount = 5 + pairs * 2 + spatial + coverage * 2 + (generatedAlpha ? 1 : 0) +
                (_hasSelectiveAppearanceShadows ? 2 : 0);
            int required = _appearanceFullResolutionCount + _appearanceBlurCount + _appearanceHistoryCount + _appearanceHistoryCoverageCount;
            if (required > TextStudio2DHeroTargetPool.MaximumTargets)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.TargetCapacityExceeded);
                CompatibilityWarning = $"These appearance occurrences require {required} simultaneous Hero targets, including independent histories. The shared pool supports {TextStudio2DHeroTargetPool.MaximumTargets}. Reduce temporal history or nested isolation.";
                return false;
            }
            if (!ResolveTargetLayout(_viewDensity, in targetBounds, _viewQuality, postProcess,
                _viewLens.IsActive ? _viewLens.MaximumMagnification : 1, _viewProgram, _viewLens.IsActive,
                out int width, out int height, out _pixelsPerUnit, out _renderBounds))
            {
                SetFailure(TextStudio2DHeroCompositorStatus.TargetMemoryBudgetExceeded);
                return false;
            }
            if (!TryPrepareTargets(width, height, _viewQuality, pairs, spatial, _appearanceBlurCount,
                _appearanceBlurFactor, coverage, generatedAlpha)) return false;
            Material composite = TextStudio2DHeroMaterials.GetComposite();
            _effectMaterial = postProcess ? TextStudio2DHeroMaterials.GetEffects() : null;
            if (composite == null || TextStudio2DHeroMaterials.GetOutput() == null || postProcess && _effectMaterial == null)
            {
                ReleaseScratchTargets();
                SetFailure(TextStudio2DHeroCompositorStatus.MissingShader);
                return false;
            }
            try
            {
                if (!PrepareAppearanceAggregateTargets(width, height, _viewQuality) ||
                    _hasSelectiveAppearanceShadows && !PrepareAppearanceShadowTargets(width, height, _viewQuality)) return false;
                foreach (OccurrenceState state in _activeAppearanceStates)
                {
                    SelectOccurrence(state);
                    if (!TryPrepareHistoryTargets(width, height, _viewQuality, state.Program.MaximumHistoryFrames, state.Program.NeedsHistoryCoverage)) return false;
                    if (resetAllHistory || state.ResetHistory) ResetHistoryState();
                    state.ResetHistory = false;
                }
                RenderTexture current = _appearanceAggregateTextures[0], alternate = _appearanceAggregateTextures[1];
                RenderTexture shadowCurrent = _appearanceShadowTextures[0], shadowAlternate = _appearanceShadowTextures[1];
                TextStudio2DHeroSubmission.Begin();
                ClearTarget(current);
                if (_hasSelectiveAppearanceShadows) ClearTarget(shadowCurrent);
                _requiresContinuousRendering = false;
                foreach (OccurrenceState state in _activeAppearanceStates)
                {
                    SelectOccurrence(state);
                    _scopeDepth = 0;
                    BuildCompositionCommands(state.Program, composite, _currentRenderTime, TextStudioMotionSettings.Resolve(_hub.MotionPolicy));
                    if (_compositionFailed)
                    {
                        SetFailure(TextStudio2DHeroCompositorStatus.RelationshipDepthExceeded);
                        return false;
                    }
                    MaskState noMask = default;
                    Composite(current, _finalTexture, alternate, TextStudio2DBlendMode.Normal, state.Occurrence.Opacity,
                        false, in noMask, composite, ConvertEffectColor(state.Occurrence.Tint));
                    if (_hasSelectiveAppearanceShadows && state.Program.Style.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped)
                    {
                        Composite(shadowCurrent, _finalTexture, shadowAlternate, TextStudio2DBlendMode.Normal, state.Occurrence.Opacity,
                            false, in noMask, composite, ConvertEffectColor(state.Occurrence.Tint));
                        RenderTexture shadowSwap = shadowCurrent; shadowCurrent = shadowAlternate; shadowAlternate = shadowSwap;
                    }
                    if (state.HistoryCapturePending)
                    {
                        state.HistoryCursor = (state.HistoryCursor + 1) % state.HistorySurfaceCount;
                        state.HistoryValidCount = Mathf.Min(state.HistoryValidCount + 1, state.HistorySurfaceCount);
                        _historyCaptureCount++;
                        state.HistoryCapturePending = false;
                    }
                    state.HistoryFingerprint = state.Program.Fingerprint;
                    state.LastRenderTime = _currentRenderTime;
                    state.HasHistoryTime = true;
                    _requiresContinuousRendering |= state.Program.Style.RequiresContinuousRenderingForPolicy(_hub.MotionPolicy) || HasContinuouslyAnimatedMaterial();
                    RenderTexture swap = current; current = alternate; alternate = swap;
                }
                _commands.Blit(current, _finalTexture);
                _lastCompositePassCount++;
                if (_hasSelectiveAppearanceShadows && shadowCurrent != _appearanceShadowTextures[0])
                {
                    _commands.Blit(shadowCurrent, _appearanceShadowTextures[0]);
                    _lastCompositePassCount++;
                }
                _sourceBounds = _appearanceSourceBounds;
                _effectUnitScale = _appearanceEffectUnitScale;
                if (_viewLens.IsActive)
                {
                    ApplyLensMagnifier(in _viewLens);
                    if (_hasSelectiveAppearanceShadows)
                    {
                        RenderTexture output = _finalTexture;
                        _finalTexture = _appearanceShadowTextures[0];
                        try { ApplyLensMagnifier(in _viewLens); }
                        finally { _finalTexture = output; }
                    }
                }
                if (_compositionFailed) { SetFailure(TextStudio2DHeroCompositorStatus.RenderFailed); return false; }
                if (_hasSelectiveAppearanceShadows)
                {
                    if (!TryAcquireOutput(ref _shadowOutput, _appearanceShadowTextures[0]))
                    {
                        SetTargetFailure(TextStudio2DHeroTargetStatus.MemoryBudgetExceeded);
                        return false;
                    }
                    PublishOutput(_appearanceShadowTextures[0], ref _shadowOutput);
                }
                else TextStudio2DHeroOutputPages.Shared.Release(ref _shadowOutput);
                PublishOutput(_finalTexture, ref _output);
                TextStudio2DHeroSubmission.Recorded();
                _renderCount++;
                _hasRenderedOutput = true;
                _lastFingerprint = _appearancePlan.Fingerprint;
                SynchronizeOutput(_viewText);
                Status = TextStudio2DHeroCompositorStatus.Ready;
                CompatibilityWarning = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetFailure(TextStudio2DHeroCompositorStatus.RenderFailed);
                CompatibilityWarning = "Appearance composition failed: " + exception.Message;
                foreach (OccurrenceState state in _activeAppearanceStates) { _occurrenceState = state; ResetHistoryState(); }
                return false;
            }
            finally
            {
                _effectMaterial = null;
                ReleaseScratchTargets();
                ReleaseAppearanceAggregateTargets();
                ReleaseAppearanceShadowTargets(false);
            }
        }

        bool PrepareAppearanceAggregateTargets(int width, int height, TextStudio2DQualityTier quality)
        {
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            for (int index = 0; index < 2; index++)
            {
                if (!pool.TryAcquire(width, height, quality, out _appearanceAggregateLeases[index], out TextStudio2DHeroTargetStatus status))
                { SetTargetFailure(status); return false; }
                _appearanceAggregateTextures[index] = pool.GetTexture(_appearanceAggregateLeases[index]);
            }
            return true;
        }

        bool PrepareAppearanceShadowTargets(int width, int height, TextStudio2DQualityTier quality)
        {
            TextStudio2DHeroTargetPool pool = TextStudio2DHeroTargetPool.Shared;
            TextStudio2DHeroTargetPool.TryResolveDescriptor(width, height, quality, out TextStudio2DHeroTargetDescriptor descriptor,
                out TextStudio2DHeroTargetStatus descriptorStatus);
            if (descriptorStatus != TextStudio2DHeroTargetStatus.Ready) { SetTargetFailure(descriptorStatus); return false; }
            RenderTexture previous = pool.GetTexture(_appearanceShadowLeases[0]);
            if (previous == null || previous.width != descriptor.Width || previous.height != descriptor.Height || previous.graphicsFormat != descriptor.Format)
            {
                if (!pool.TryAcquire(width, height, quality, out TextStudio2DHeroTargetLease replacement, out TextStudio2DHeroTargetStatus status))
                { SetTargetFailure(status); return false; }
                pool.Release(ref _appearanceShadowLeases[0]);
                _appearanceShadowLeases[0] = replacement;
            }
            _appearanceShadowTextures[0] = pool.GetTexture(_appearanceShadowLeases[0]);
            if (!pool.TryAcquire(width, height, quality, out _appearanceShadowLeases[1], out TextStudio2DHeroTargetStatus scratchStatus))
            { SetTargetFailure(scratchStatus); return false; }
            _appearanceShadowTextures[1] = pool.GetTexture(_appearanceShadowLeases[1]);
            return true;
        }

        void ReleaseAppearanceShadowTargets(bool includeOutput)
        {
            for (int index = includeOutput ? 0 : 1; index < _appearanceShadowLeases.Length; index++)
            {
                TextStudio2DHeroTargetPool.Shared.Release(ref _appearanceShadowLeases[index]);
                _appearanceShadowTextures[index] = null;
            }
        }

        void ReleaseAppearanceAggregateTargets()
        {
            for (int index = 0; index < _appearanceAggregateLeases.Length; index++)
            {
                TextStudio2DHeroTargetPool.Shared.Release(ref _appearanceAggregateLeases[index]);
                _appearanceAggregateTextures[index] = null;
            }
        }

        void ReleaseOccurrenceState(OccurrenceState state)
        {
            if (state == null) return;
            OccurrenceState previous = _occurrenceState;
            _occurrenceState = state;
            ReleaseHistoryTargets();
            ReleaseColorSources();
            foreach (LayerBatch batch in state.Batches) batch.Dispose();
            state.Batches.Clear();
            state.SurfaceGeometry.Dispose();
            _occurrenceState = previous;
        }

        void ReleaseAppearanceOccurrences()
        {
            foreach (OccurrenceState state in _appearanceStates) ReleaseOccurrenceState(state);
            _appearanceStates.Clear();
            _activeAppearanceStates.Clear();
            ReleaseAppearanceAggregateTargets();
            ReleaseAppearanceShadowTargets(true);
            _hasSelectiveAppearanceShadows = false;
            _appearancePlan = null;
            _occurrenceState = _defaultOccurrenceState;
        }

        bool AppearanceRequiresConnectedSurfaceUnion()
        {
            foreach (OccurrenceState state in _activeAppearanceStates)
                if (RequiresConnectedSurfaceUnion(state.Program)) return true;
            return false;
        }

        int TotalHistorySurfaceCount
        {
            get
            {
                int count = _defaultOccurrenceState.HistorySurfaceCount + _defaultOccurrenceState.HistoryCoverageCount;
                foreach (OccurrenceState state in _appearanceStates)
                    if (state != null) count += state.HistorySurfaceCount + state.HistoryCoverageCount;
                return count;
            }
        }

        static long CaptureOccurrenceManagedBytes(OccurrenceState state)
        {
            long bytes = TextStudioAppearanceManagedMemory.ListBytes(state.DrawingUnits) + TextStudioAppearanceManagedMemory.ArrayBytes(state.SeenDrawingUnits) +
                TextStudioAppearanceManagedMemory.ArrayBytes(state.ColorBindings) + TextStudioAppearanceManagedMemory.ArrayBytes(state.NextColorBindings) +
                (long)state.ColorBindingIndices.EnsureCapacity(0) * 5 * sizeof(int) + TextStudioAppearanceManagedMemory.ListBytes(state.ColorSourceIndices) +
                TextStudioAppearanceManagedMemory.ArrayBytes(state.SourceAdjustmentSelections) + TextStudioAppearanceManagedMemory.ArrayBytes(state.UnitSourceRanges) +
                TextStudioAppearanceManagedMemory.ArrayBytes(state.OperationHasSelection) + TextStudioAppearanceManagedMemory.ArrayBytes(state.WholeCompositeScope) +
                TextStudioAppearanceManagedMemory.ArrayBytes(state.RowMaskClass) + TextStudioAppearanceManagedMemory.ArrayBytes(state.RowMaskRepresentative) +
                TextStudioAppearanceManagedMemory.ArrayBytes(state.HistoryLeases) + TextStudioAppearanceManagedMemory.ArrayBytes(state.HistoryCoverageLeases) +
                (long)(state.Batches.Capacity + state.GeneratedContributionRuns.Length + state.HistoryTextures.Length + state.HistoryCoverageTextures.Length) * IntPtr.Size;
            foreach (LayerBatch batch in state.Batches) bytes += batch.ManagedPayloadBytes;
            foreach (List<GeneratedContributionRun> runs in state.GeneratedContributionRuns) bytes += TextStudioAppearanceManagedMemory.ListBytes(runs);
            return bytes + state.PainterScopes.ManagedPayloadBytes + state.SurfaceGeometry.ManagedPayloadBytes;
        }

        static Rect UnionBounds(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        // Immutable programs and material leases are shared. Only geometry, scope membership, and temporal state vary per call.
        sealed class OccurrenceState
        {
            internal TextStudio2DStyleDecorations SurfaceGeometry = new TextStudio2DStyleDecorations(true);
            internal List<LayerBatch> Batches = new List<LayerBatch>(2);
            internal int ActiveBatchCount = 0;
            internal List<int> DrawingUnits = new List<int>();
            internal TextStudio2DPainterScopeBinding PainterScopes = new TextStudio2DPainterScopeBinding();
            internal bool[] SeenDrawingUnits = Array.Empty<bool>();
            internal bool[] OperationHasSelection = new bool[TextStudio2DHeroProgram.MaximumOperations];
            // Row mask class (batch submesh) of every operation and the operation whose support defines each class.
            internal int[] RowMaskClass = new int[TextStudio2DHeroProgram.MaximumOperations];
            internal int[] RowMaskRepresentative = new int[MaximumRowMaskClasses];
            internal int RowMaskClassCount = 1;
            internal int RowMaskGlyphCount = 0;
            internal bool[] WholeCompositeScope = new bool[TextStudio2DHeroProgram.MaximumOperations];
            internal List<GeneratedContributionRun>[] GeneratedContributionRuns = new List<GeneratedContributionRun>[TextStudio2DHeroProgram.MaximumOperations];
            internal ulong[] SourceAdjustmentSelections = Array.Empty<ulong>();
            internal Vector2Int[] UnitSourceRanges = Array.Empty<Vector2Int>();
            internal int SourceUnitStart;
            internal readonly Dictionary<int, int> ColorBindingIndices = new Dictionary<int, int>();
            internal readonly List<int> ColorSourceIndices = new List<int>();
            internal ColorBinding[] ColorBindings = Array.Empty<ColorBinding>();
            internal ColorBinding[] NextColorBindings = Array.Empty<ColorBinding>();
            internal int ColorBindingCount = 0;
            internal ulong GeometryFingerprint = 0;
            internal int GeometryStyleSlot = 0;
            internal TextStudioVisualVersion GeometryVisualVersion = default;
            internal int GeometryGlyphCount = -1;
            internal TextStudio2DExternalGlyphSource GeometryExternalSource;
            internal int GeometryExternalRevision;
            internal ulong GeometryPrecisionFingerprint = 0;
            internal Rect CachedGeometryBounds = default;
            internal bool HasCachedGeometry = false;
            internal TextStudio2DHeroTargetLease[] HistoryLeases = new TextStudio2DHeroTargetLease[MaximumHistoryFrames];
            internal RenderTexture[] HistoryTextures = new RenderTexture[MaximumHistoryFrames];
            internal TextStudio2DHeroTargetLease[] HistoryCoverageLeases = new TextStudio2DHeroTargetLease[MaximumHistoryFrames];
            internal RenderTexture[] HistoryCoverageTextures = new RenderTexture[MaximumHistoryFrames];
            internal int HistorySurfaceCount = 0;
            internal int HistoryValidCount = 0;
            internal int HistoryCursor = 0;
            internal int HistoryCoverageCount = 0;
            internal int PendingHistoryOperation = -1;
            internal bool HistoryCapturePending = false;
            // The echo layer already filled this frame's history slot right after its trail pass read the ring.
            internal bool HistoryLayerCaptured = false;
            internal float LastRenderTime = 0;
            internal bool HasHistoryTime = false;
            internal int BindingRevision = -1;
            internal int HistoryBindingRevision = -1;
            internal string ColorCompatibilityWarning = string.Empty;
            internal string ColorQualityHint = string.Empty;
            internal TextStudio2DHeroProgram Program;
            internal int StyleSlot;
            internal TextStudioStackFingerprint HistoryFingerprint;
            internal TextStudioAppearanceOccurrence Occurrence;
            internal Vector4 SourceBounds;
            internal float SourceDensity;
            internal float SurfacePixelsPerUnit;
            internal float EffectUnitScale;
            internal TextStudio2DQualityTier Quality;
            internal bool ResetHistory;
        }
    }
}
