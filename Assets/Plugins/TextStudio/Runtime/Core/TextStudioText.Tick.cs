using System;
using ImpossibleRobert.Common.Timing;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        static readonly ProfilerMarker PipelineProfilerMarker =
            new ProfilerMarker("Text Studio Pipeline");
        static readonly ProfilerMarker LayoutProfilerMarker =
            new ProfilerMarker("Text Studio Layout");
        static readonly ProfilerMarker EvaluationProfilerMarker =
            new ProfilerMarker("Text Studio Evaluation");
        static readonly ProfilerMarker DispatchProfilerMarker =
            new ProfilerMarker("Text Studio Dispatch");

        // ─────────────────────────────────────────────────────────────────────
        //  Per-tick loop
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Called by the editor ticker to drive edit-mode ticks.</summary>
        internal void PerformTick(in TimeStep step) => Tick(step);

        internal bool TickInEditMode => _timing.TickInEditMode;

        internal void ResetHostTime(ref double lastHostTime)
        {
            HostTime.Reset(ref lastHostTime, _timing);
        }

        internal TimeStep CreateHostTimeStep(ref double lastHostTime, int frame)
        {
            return HostTime.Step(ref lastHostTime, frame, _timing);
        }

        internal void PerformTick(float dt) => Tick(new TimeStep(dt, _time + Mathf.Max(0f, dt), Time.frameCount));

        // Slot in TextStudioHubScheduler while this hub is enabled in Play Mode, otherwise -1.
        [NonSerialized] internal int SchedulerSlot = -1;

        // TextStudioHubScheduler calls this from its LateUpdate for every enabled hub in Play Mode with the frame's deltas.
        internal void ScheduledLateUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (_isIdleSleeping &&
                ContinueIdleSleep(_timing.ClockMode == ClockMode.Unscaled ? unscaledDeltaTime : deltaTime))
            {
                return;
            }
            Tick(HostTime.Step(ref _lastHostTime, Time.frameCount, _timing));
        }

        DirtyFlags _globalDirtyThisTick;
        bool _isTicking;
        bool _isIdleSleeping;
        bool _hasSyncedBehaviorProfile;
        TextStudioBehaviorProfile _syncedBehaviorProfile;
        uint _syncedBehaviorProfileRevision;

        internal bool IsIdleSleepingForDebug => _isIdleSleeping;

        // Names the first condition that keeps settled text awake, or null while it may sleep.
        internal string IdleSleepBlockerForDebug
        {
            get
            {
                if (_runtime.DirtyFlags != DirtyFlags.None) return "dirty " + _runtime.DirtyFlags;
                if (_runtime.ActiveActionCount > 0) return "actions";
                if (!_runtime.IsEffectContentCurrent) return "effect content";
                if (_tmpBridge == null || !_tmpBridge.AreObservedTextAndFontCurrent()) return "observed text or font";
                if (!IsSleepingConfigurationCurrent()) return "configuration";
                return CanSleepWhileIdle() ? null : "eligibility";
            }
        }
        internal float PresentationTimeForDebug => _time;
        internal float PlacementTimeForDebug => _placementTime;
        internal event Action PresentationUpdated;
        int _hiddenGlyphGeometryUsers;

        // Surface groups read the TMP quads of hidden source glyphs, so a text they use keeps those quads instead of
        // collapsing them. Gaining the first user rebuilds quads that already collapsed.
        internal bool RetainsHiddenGlyphGeometry => _hiddenGlyphGeometryUsers > 0;

        internal void RetainHiddenGlyphGeometry(bool retain)
        {
            bool retained = RetainsHiddenGlyphGeometry;
            _hiddenGlyphGeometryUsers = Mathf.Max(0, _hiddenGlyphGeometryUsers + (retain ? 1 : -1));
            if (!retained && RetainsHiddenGlyphGeometry)
                ScheduleMeshRefresh();
        }

        void Tick(in TimeStep step)
        {
            // Validation synchronizes text and preview progress, but renderer objects
            // can only be rebuilt or disposed after Unity leaves OnValidate.
            if (!isActiveAndEnabled || _isTicking || _isValidating)
                return;

            _isTicking = true;
            try
            {
                TickInternal(step);
            }
            finally
            {
                _isTicking = false;
            }
            PresentationUpdated?.Invoke();
        }

        void TickInternal(in TimeStep step)
        {
            _isIdleSleeping = false;
            float hostDeltaTime = _runtime.ResolvePresentationDeltaTime(step.DeltaTime);
            float dt = _preview != null
                ? _preview.GetEffectiveDeltaTime(hostDeltaTime)
                : hostDeltaTime;
            _time += dt;
            if (_layoutInterpolation?.Advance(dt) == true)
                _runtime.SetDirty(DirtyFlags.Placement | DirtyFlags.Visual);
            if (Application.isPlaying || _authoringMode == TextStudioAuthoringMode.Preview)
            {
                _placementTime += _preview != null
                    ? _preview.GetEffectivePlacementDeltaTime(hostDeltaTime)
                    : hostDeltaTime;
            }

            if (NeedsReactionSync())
                SyncReactions();
            _runtime.EnsureEffectContentCurrent();
            EnsureAppearanceStyleCurrent();
            EnsureWritingStyleCurrent();
            ApplyExternalEffectSamples();
            bool tmpFontChanged = _activeTextSourceBackend == null &&
                                  TmpBridge.TryConsumeFontAssetChange();
            if (tmpFontChanged)
            {
                _runtime.SetDirty(
                    DirtyFlags.Topology |
                    DirtyFlags.Layout |
                    DirtyFlags.Placement |
                    DirtyFlags.Animation |
                    DirtyFlags.Visual);
                if (!Application.isPlaying)
                    TextStudioEditorHooks.RequestRepaint();
            }

            bool canUseCachedSettings = CanUseCachedSettingsForEarlySkip();
            bool hasUnobservedTextChange = canUseCachedSettings &&
                                           HasUnobservedTextBackendChange;
            if (canUseCachedSettings &&
                !hasUnobservedTextChange &&
                CanSkipIdleTick(false))
            {
                _isIdleSleeping = CanSleepWhileIdle();
                return;
            }
            if (TryProcessPlacementOnlyTick(
                    dt,
                    canUseCachedSettings,
                    hasUnobservedTextChange))
            {
                return;
            }

            SyncActionRunner();
            if (_preview != null)
                _preview.Tick(dt);
            bool previewStartedDisappear = _preview != null && _preview.ConsumeStartedDisappearThisTick();
            bool previewDroveReveal = _preview != null && _preview.ConsumeDroveRevealThisTick();
            bool isDrivingTextBackend = IsDrivingTextBackend;
            bool isDrivingTextStudioOutput = IsDrivingTextStudioOutput;

            // Stage 0: text-source pull (mirror mode).
            bool pulledText = ProcessTextBackendPull(isDrivingTextBackend);

            if (CanUseCachedSettingsForEarlySkip() &&
                (CanSkipIdleTick(pulledText) ||
                 CanSkipInvisiblePersistentEffects(isDrivingTextStudioOutput)))
            {
                return;
            }

            SyncBehaviorConfiguration();

            if (CanSkipIdleTick(pulledText))
                return;
            if (CanSkipInvisiblePersistentEffects(isDrivingTextStudioOutput))
                return;

            _globalDirtyThisTick = _runtime.DirtyFlags;

            // Stage 1: pipeline (text+style → compiled plan).
            if (_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
            {
                bool planChanged;
                using (PipelineProfilerMarker.Auto())
                using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Pipeline))
                    planChanged = ProcessPipeline();
                _runtime.ClearProcessedTextAndStyleDirty();
                DirtyFlags pipelineDirty = DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual;
                _runtime.SetDirty(pipelineDirty);
                _globalDirtyThisTick |= pipelineDirty;

                if (planChanged && !Application.isPlaying && Preview.IsScrubbing)
                    Preview.ApplyScrubProgress();
            }

            if (_startRevealAfterDeferredPipeline && !_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
            {
                _startRevealAfterDeferredPipeline = false;
                _runtime.StartTextChangeReveal();
            }

            PollExternalEffectSources();

            // Stage 1b: text-source push.
            isDrivingTextBackend = IsDrivingTextBackend;
            isDrivingTextStudioOutput = IsDrivingTextStudioOutput;
            using (TextStudioPerformanceDiagnostics.Measure(
                       TextStudioPerformanceStage.BackendPublication))
            {
                PushTextBackend(isDrivingTextBackend);
            }
            if (!isDrivingTextStudioOutput)
            {
                // Losing the last external renderer outside driven output must still hand drawing back to the backend.
                UpdateTextBackendVisibility(
                    ShouldSuppressTextBackendOutput(HasAvailableExternalRenderer()));
                _runtime.ClearDirty(DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual | DirtyFlags.Simulation | DirtyFlags.Appearance | DirtyFlags.Writing);
                _runtime.MeshDirty.ClearAll();
                return;
            }

            if (TryUseUnanimatedTMPPassThrough(
                    isDrivingTextBackend,
                    isDrivingTextStudioOutput))
            {
                return;
            }

            // Stage 2: layout.
            bool layoutChanged = _runtime.IsDirty(DirtyFlags.Layout);
            bool restoreLayoutAfterPassThrough =
                !UsesCustomTextBackend &&
                TmpBridge.IsUsingUnanimatedPassThrough;
            if (layoutChanged || restoreLayoutAfterPassThrough)
            {
                bool suppressTextBackendOutput =
                    ShouldSuppressTextBackendOutput(HasAvailableExternalRenderer());
                bool cacheVisualSourceData =
                    !suppressTextBackendOutput && RequiresBackendFrameSourceData(_runtime.Plan);
                bool builtLayout;
                using (LayoutProfilerMarker.Auto())
                using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Layout))
                {
                    builtLayout = TryBuildTextBackendLayout(
                        cacheVisualSourceData,
                        _runtime.ExternalSourceContextRequirements);
                }
                if (!builtLayout)
                    return;
                bool restoreScrub = _preview != null && _preview.IsScrubbing;
                _runtime.Reveal.SetLayout(_layout.VisualGlyphs, _layout.GlyphCount, !restoreScrub);
                if (restoreScrub) _preview.ApplyScrubProgress();
                _runtime.ConsumeDirty(DirtyFlags.Layout);
            }

            // Stage 3: placement.
            bool needPlacement =
                _runtime.ConsumeDirty(DirtyFlags.Placement) ||
                _placementMode == PlacementMode.Wave ||
                _placementMode == PlacementMode.Spline;
            if (needPlacement)
                _globalDirtyThisTick |= DirtyFlags.Placement;
            if (needPlacement && _layout.GlyphCount > 0)
            {
                if (_placementMode == PlacementMode.Linear && !_placementRotation.HasEffect && _textChangeLayoutDuration <= 0f)
                {
                    _placementEngine.Clear();
                }
                else
                {
                    if (_placementMode == PlacementMode.Curve)
                        _curvePlacement.PrepareSelection(_runtime.Plan, _layout.GlyphCount, _layout.VisualGlyphs, layoutChanged);
                    _placementEngine.ComputePlacements(
                        _layout.BasePositions,
                        _layout.GlyphCount,
                        _layout.AutoFitGlyphMargin,
                        _layout.HasSourceCenterBaselineOffsets
                            ? _layout.SourceCenterBaselineOffsets
                            : null,
                        _layout.PlacementLineRanges,
                        _layout.PlacementLineCount,
                        layoutChanged,
                        _layout.SourceAlignment,
                        GetPlacementEvaluationTime(),
                        _layout.SourceVisualBounds, _layout.SourceLayoutValidity);
                    _placementEngine.ApplyOrientation(_placementRotation);
                    InterpolateLayout(_layout.GlyphCount);
                }
                _runtime.MeshDirty.SetAllDirty(_layout.GlyphCount);
            }
            else if (needPlacement)
            {
                _placementEngine.Clear();
                _layoutInterpolation?.Reset();
            }

            // Stage 4: reveal tick.
            if (!Application.isPlaying)
                _editModeActionRunner.Tick(dt);

            DirtyFlags dirtyBeforeReveal = _runtime.DirtyFlags;
            float revealDeltaTime = previewStartedDisappear || previewDroveReveal ? 0f : dt;
            _runtime.TickReveal(revealDeltaTime, !Preview.IsScrubbing && !HasExternalRevealClockOwner);
            _globalDirtyThisTick |= _runtime.DirtyFlags & ~dirtyBeforeReveal;

            // Stage 5: presentation pass (addons).
            UpdatePresentation(dt);
            _globalDirtyThisTick |= _runtime.DirtyFlags;

            // Stage 6: evaluator.
            bool hasInfluenceTarget = UpdateInfluenceTarget(
                out Vector3 influenceTargetLocalPosition,
                out Vector3 influenceTargetWorldPosition,
                out Vector3 influenceTargetSourceTextNormalizedPosition);
            _runtime.Animator.SetSpatialContext(
                _layout.BasePositions,
                _layout.SourceTextNormalizedPositions,
                _layout.SourceColors,
                _layout.SourceColorValidity,
                _layout.GlyphCount,
                _placementEngine.Placements,
                _placementEngine.Count,
                _placementMode,
                transform.localToWorldMatrix,
                hasInfluenceTarget,
                influenceTargetLocalPosition,
                influenceTargetWorldPosition,
                influenceTargetSourceTextNormalizedPosition,
                _layout.SourceVisualBounds,
                _layout.Revision,
                _layout.EffectUnitScales);
            bool renderRawWithoutEffects = IsRenderingRawExternalOutput;
            UpdateRendererEffectExecution(
                _runtime.Plan,
                renderRawWithoutEffects);
            using (EvaluationProfilerMarker.Auto())
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Evaluation))
            {
                if (renderRawWithoutEffects)
                    _runtime.PrepareUnanimatedOutput();
                else
                    _runtime.EvaluateIfNeeded(dt);
            }

            // Stage 7: dispatch frame.
            using (DispatchProfilerMarker.Auto())
            using (TextStudioPerformanceDiagnostics.Measure(TextStudioPerformanceStage.Dispatch))
            {
                if (DispatchFrame(dt))
                    _runtime.ClearDirty(DirtyFlags.Topology | DirtyFlags.Visual | DirtyFlags.Simulation | DirtyFlags.Appearance | DirtyFlags.Writing);
            }
            _runtime.SampleReactionsAfterPresentation();
            ReleaseTextBackendMaterialEffects();
            _runtime.FlushReactionSignals();
        }

        bool TryProcessPlacementOnlyTick(
            float dt,
            bool canUseCachedSettings,
            bool hasUnobservedTextChange)
        {
            if (!Application.isPlaying ||
                !canUseCachedSettings ||
                hasUnobservedTextChange ||
                (_preview != null && !_preview.IsRuntimePassThrough) ||
                _runtime.Plan == null ||
                _runtime.DirtyFlags != DirtyFlags.Placement ||
                RequiresContinuousEvaluation ||
                _runtime.ActiveActionCount > 0 ||
                _startRevealAfterDeferredPipeline ||
                _placementMode == PlacementMode.Wave ||
                _placementMode == PlacementMode.Spline ||
                _characterSubstitutionPresenter.HasActiveSubstitutions ||
                HasActiveExternalEffectSources() ||
                (_glyphProvider != null && _glyphProvider.IsDirty) ||
                _influenceTarget != null ||
                // Reactions and styled output keep the full tick; this path skips post-dispatch reaction sampling and material release.
                _runtime.HasReactionRuntime ||
                (ActiveCompiledAppearancePlan != null && !ActiveCompiledAppearancePlan.IsPassThrough) ||
                (_compiledWritingStyle != null && !_compiledWritingStyle.IsPassThrough) ||
                !IsDrivingTextBackend)
            {
                return false;
            }

            _globalDirtyThisTick = DirtyFlags.Placement;
            _runtime.ConsumeDirty(DirtyFlags.Placement);
            int glyphCount = _layout.GlyphCount;
            if (glyphCount > 0)
            {
                if (_placementMode == PlacementMode.Linear && !_placementRotation.HasEffect && _textChangeLayoutDuration <= 0f)
                {
                    _placementEngine.Clear();
                }
                else
                {
                    if (_placementMode == PlacementMode.Curve)
                        _curvePlacement.PrepareSelection(_runtime.Plan, glyphCount, _layout.VisualGlyphs);
                    _placementEngine.ComputePlacements(
                        _layout.BasePositions,
                        glyphCount,
                        _layout.AutoFitGlyphMargin,
                        _layout.HasSourceCenterBaselineOffsets
                            ? _layout.SourceCenterBaselineOffsets
                            : null,
                        _layout.PlacementLineRanges,
                        _layout.PlacementLineCount,
                        false,
                        _layout.SourceAlignment,
                        GetPlacementEvaluationTime(),
                        _layout.SourceVisualBounds, _layout.SourceLayoutValidity);
                    _placementEngine.ApplyOrientation(_placementRotation);
                    InterpolateLayout(glyphCount);
                }
                _runtime.MeshDirty.SetAllDirty(glyphCount);
            }
            else
            {
                _placementEngine.Clear();
                _layoutInterpolation?.Reset();
            }

            _runtime.Animator.SetSpatialContext(
                _layout.BasePositions,
                _layout.SourceTextNormalizedPositions,
                _layout.SourceColors,
                _layout.SourceColorValidity,
                glyphCount,
                _placementEngine.Placements,
                _placementEngine.Count,
                _placementMode,
                transform.localToWorldMatrix,
                false,
                Vector3.zero,
                Vector3.zero,
                default,
                _layout.SourceVisualBounds,
                _layout.Revision,
                _layout.EffectUnitScales);
            if (DispatchFrame(dt))
                _runtime.ClearDirty(DirtyFlags.Topology | DirtyFlags.Visual | DirtyFlags.Simulation | DirtyFlags.Appearance | DirtyFlags.Writing);
            return true;
        }

        bool UpdateInfluenceTarget(
            out Vector3 localPosition,
            out Vector3 worldPosition,
            out Vector3 sourceTextNormalizedPosition)
        {
            if (_influenceTarget == null)
            {
                localPosition = Vector3.zero;
                worldPosition = Vector3.zero;
                sourceTextNormalizedPosition = Vector3.zero;
                if (_hadInfluenceTarget)
                    _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                _hadInfluenceTarget = false;
                return false;
            }

            worldPosition = _influenceTarget.position;
            localPosition = transform.InverseTransformPoint(worldPosition);
            sourceTextNormalizedPosition = NormalizeInfluenceTarget(
                localPosition,
                _layout.SourceTextBounds);
            if (!_hadInfluenceTarget ||
                (worldPosition - _lastInfluenceTargetWorldPosition).sqrMagnitude > 0.00000001f ||
                (localPosition - _lastInfluenceTargetLocalPosition).sqrMagnitude > 0.00000001f)
            {
                _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
                _lastInfluenceTargetLocalPosition = localPosition;
                _lastInfluenceTargetWorldPosition = worldPosition;
                _hadInfluenceTarget = true;
            }
            return true;
        }

        internal static Vector3 NormalizeInfluenceTarget(
            Vector3 localPosition,
            Vector4 sourceTextBounds)
        {
            return new Vector3(
                NormalizeInfluenceTargetCoordinate(
                    localPosition.x,
                    sourceTextBounds.x,
                    sourceTextBounds.z),
                NormalizeInfluenceTargetCoordinate(
                    localPosition.y,
                    sourceTextBounds.y,
                    sourceTextBounds.w),
                0f);
        }

        static float NormalizeInfluenceTargetCoordinate(
            float value,
            float minimum,
            float maximum)
        {
            if (!float.IsFinite(value) ||
                !float.IsFinite(minimum) ||
                !float.IsFinite(maximum) ||
                Mathf.Abs(maximum - minimum) <= 0.0001f)
            {
                return 0.5f;
            }

            return Mathf.InverseLerp(minimum, maximum, value);
        }

        float GetActionDeltaTime()
        {
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            return _preview != null ? _preview.GetEffectiveDeltaTime(deltaTime) : deltaTime;
        }

        float GetPlacementEvaluationTime()
        {
            if (TextStudioMotionSettings.Resolve(_motionPolicy) != TextStudioMotionPolicy.Full)
                return 0f;
            if (_preview == null)
                return _placementTime;
            if (!Application.isPlaying && _authoringMode == TextStudioAuthoringMode.Raw)
                return 0f;
            return _preview.GetPlacementEvaluationTime(_placementTime);
        }

        float GetPresentationEvaluationTime()
        {
            float runtimeTime = Mathf.Max(0f, _time - _presentationTimeOrigin);
            if (_preview != null &&
                (Application.isPlaying || _authoringMode != TextStudioAuthoringMode.Raw))
            {
                // Explicit preview playback and scrubbing own the presentation clock in both modes.
                // Ordinary runtime playback retains its elapsed time through the same ownership rule.
                return _preview.GetPlacementEvaluationTime(runtimeTime);
            }

            return runtimeTime;
        }

        internal bool CanSkipIdleTick(bool pulledText)
        {
            bool requiresContinuousEvaluation =
                RequiresContinuousEvaluation &&
                !IsRenderingRawExternalOutput;
            if (pulledText ||
                _runtime.Plan == null ||
                _runtime.DirtyFlags != DirtyFlags.None ||
                requiresContinuousEvaluation ||
                _runtime.ActiveActionCount > 0 ||
                _placementMode == PlacementMode.Wave ||
                _placementMode == PlacementMode.Spline ||
                HasRendererRequiringContinuousFrames() ||
                _characterSubstitutionPresenter.HasActiveSubstitutions ||
                HasActiveExternalEffectSources() ||
                (_glyphProvider != null && _glyphProvider.IsDirty))
            {
                return false;
            }

            if (!UsesCustomTextBackend && _tmpBridge != null && _tmpBridge.NeedsCanvasPayloadRefresh())
            {
                _runtime.SetDirty(DirtyFlags.Visual);
                return false;
            }

            if (!UsesCustomTextBackend && _tmpBridge != null && _tmpBridge.NeedsCameraQualityRefresh())
            {
                _runtime.SetDirty(DirtyFlags.AppearanceParameters);
                return false;
            }
            return _preview == null || (!_preview.IsPlaying && !_preview.IsScrubbing);
        }

        bool HasRendererRequiringContinuousFrames()
        {
            bool hasStale = false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (!IsUsableRenderer(renderer, ref hasStale))
                    continue;
                if ((renderer.Capabilities & RendererCapabilities.SupportsIdleFrameSkipping) == 0)
                    return true;
            }

            if (hasStale)
            {
                _renderers.RemoveAll(renderer =>
                    renderer == null ||
                    (renderer is UnityEngine.Object unityObject && unityObject == null));
            }
            return false;
        }

        bool RequiresBackendFrameSourceData(TextStudioCompiledPlan plan)
        {
            if (plan == null)
                return false;

            TMP_Text tmpText = UsesCustomTextBackend ? null : TmpBridge.TMPText;
            bool requiresDecorations =
                plan.CharDecorations != null ||
                (tmpText != null &&
                 (tmpText.fontStyle & (FontStyles.Underline | FontStyles.Strikethrough)) != 0);
            bool hasActiveFrameComposers =
                _extensions != null && _extensions.HasActiveFrameComposers;
            return hasActiveFrameComposers ||
                   (ActiveCompiledAppearancePlan != null &&
                    !ActiveCompiledAppearancePlan.IsPassThrough) ||
                   (_compiledWritingStyle != null && !_compiledWritingStyle.IsPassThrough) ||
                   plan.EffectBindingCount > 0 ||
                   plan.CharacterSubstitutionBindingCount > 0 ||
                   HasActiveExternalEffectSources() ||
                   _runtime.HasExternalEffectState ||
                   _runtime.ReactionsRequireGeometry ||
                   _runtime.RevealMode != RevealMode.Instant ||
                   _runtime.HasCueTimeline ||
                   _textChangeLayoutDuration > 0f ||
                   _placementMode != PlacementMode.Linear ||
                   _placementRotation.HasEffect ||
                   _glyphProvider != null ||
                   requiresDecorations ||
                   // Hiding some glyphs needs per-glyph output; hiding all of them suppresses TMP's draw in pass-through.
                   (_runtime.HasVisibilityOverrides && !_runtime.HidesEveryGlyphByOverride());
        }

        bool TryUseUnanimatedTMPPassThrough(
            bool isDrivingTextBackend,
            bool isDrivingTextStudioOutput)
        {
            if (!CanUseUnanimatedTMPPassThrough(
                    isDrivingTextBackend,
                    isDrivingTextStudioOutput,
                    _runtime.Plan))
            {
                return false;
            }

            _placementEngine.Clear();
            TmpBridge.UseUnanimatedPassThrough(_runtime.Plan);
            TmpBridge.SetPassThroughHidden(_runtime.HasVisibilityOverrides);
            _runtime.ClearDirty(
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.Placement |
                DirtyFlags.Animation |
                DirtyFlags.Visual |
                DirtyFlags.Simulation |
                DirtyFlags.Appearance |
                DirtyFlags.Writing);
            _runtime.MeshDirty.ClearAll();
            return true;
        }

        bool CanUseUnanimatedTMPPassThrough(
            bool isDrivingTMPText,
            bool isDrivingTextStudioOutput,
            TextStudioCompiledPlan plan)
        {
            return !UsesCustomTextBackend &&
                   isDrivingTMPText &&
                   isDrivingTextStudioOutput &&
                   !IsRenderingRawExternalOutput &&
                   plan != null &&
                   !RequiresBackendFrameSourceData(plan) &&
                   _presentationGlyphs == null &&
                   !RequiresContinuousEvaluation &&
                   _runtime.ActiveActionCount == 0 &&
                   !_runtime.ReactionsRequireAnchor &&
                   !HasAvailableExternalRenderer();
        }

        internal bool CanSkipInvisiblePersistentEffects(bool isDrivingTMPText)
        {
            if (!isDrivingTMPText ||
                _runtime.Plan == null ||
                _runtime.DirtyFlags != DirtyFlags.None ||
                !_runtime.HasOnlyPersistentEffectEvaluation ||
                _runtime.ActiveActionCount > 0 ||
                _placementMode == PlacementMode.Wave ||
                _placementMode == PlacementMode.Spline ||
                _renderers.Count > 0 ||
                _characterSubstitutionPresenter.HasActiveSubstitutions ||
                HasActiveExternalEffectSources() ||
                (_glyphProvider != null && _glyphProvider.IsDirty) ||
                (_preview != null && (_preview.IsPlaying || _preview.IsScrubbing)))
            {
                return false;
            }

            return !IsTextBackendVisualOutputEnabled;
        }

        bool CanUseCachedSettingsForEarlySkip()
        {
            return IsBehaviorProfileSyncCurrent() &&
                   _typewriterPreset == null &&
                   _timings == null &&
                   _typewriterRevealEffect == null &&
                   _typewriterHideEffect == null &&
                   string.IsNullOrEmpty(_typewriterRevealEffectExpression) &&
                   string.IsNullOrEmpty(_typewriterHideEffectExpression);
        }

        // An assigned profile keeps cached settings valid until it changes; every serialized or API edit moves its revision.
        bool IsBehaviorProfileSyncCurrent()
        {
            if (_behaviorProfile == null)
                return true;

            TextStudioTypewriterSource typewriterSource = _behaviorProfile.TypewriterSource;
            return ReferenceEquals(_syncedBehaviorProfile, _behaviorProfile) &&
                   _syncedBehaviorProfileRevision == _behaviorProfile.ReactionSerializationRevision &&
                   (!_behaviorProfile.Provides(TextStudioBehaviorDomain.Typewriter) ||
                    typewriterSource == TextStudioTypewriterSource.None ||
                    typewriterSource == TextStudioTypewriterSource.BuiltIn);
        }

        void RecordBehaviorProfileSync()
        {
            _hasSyncedBehaviorProfile = true;
            _syncedBehaviorProfile = _behaviorProfile;
            _syncedBehaviorProfileRevision = _behaviorProfile != null
                ? _behaviorProfile.ReactionSerializationRevision
                : 0u;
        }

        bool NeedsReactionSync()
        {
            return _runtime.HasReactionRuntime ||
                   (_behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions)
                       ? _behaviorProfile.Reactions.Source != TextStudioReactionSource.None
                       : _reactions.Source != TextStudioReactionSource.None);
        }

        // A settled hub sleeps while every input its idle tick polls is unchanged. Runtime dirty flags and actions, the effect
        // content revision, the synced profile and TMP's text and font references wake it; hubs with inputs that are only
        // polled stay awake. Time advances with Tick's arithmetic, so later frames read the same clocks.
        bool ContinueIdleSleep(float deltaTime)
        {
            if (_runtime.DirtyFlags != DirtyFlags.None ||
                _runtime.ActiveActionCount > 0 ||
                !_runtime.IsEffectContentCurrent ||
                !IsSleepingConfigurationCurrent() ||
                !_tmpBridge.AreObservedTextAndFontCurrent())
            {
                _isIdleSleeping = false;
                return false;
            }

            float hostDeltaTime = _runtime.ResolvePresentationDeltaTime(
                HostTime.ClampDelta(deltaTime, _timing.MaxDeltaTime) * _timing.TimeScale);
            _time += _preview != null ? _preview.GetEffectiveDeltaTime(hostDeltaTime) : hostDeltaTime;
            _placementTime += _preview != null
                ? _preview.GetEffectivePlacementDeltaTime(hostDeltaTime)
                : hostDeltaTime;
            PresentationUpdated?.Invoke();
            return true;
        }

        bool CanSleepWhileIdle()
        {
            return Application.isPlaying &&
                   _tmpBridge != null &&
                   _activeTextSourceBackend == null &&
                   !UsesCustomTextBackend &&
                   !_hasExternalTextSource &&
                   (_timing.ClockMode == ClockMode.Scaled || _timing.ClockMode == ClockMode.Unscaled) &&
                   !NeedsReactionSync() &&
                   AppearanceSource == TextStudioAppearanceSource.Inline &&
                   _inlineAppearanceSettings.IsEmpty &&
                   WritingStyle == null &&
                   !_characterSubstitutionPresenter.HasActiveSubstitutions &&
                   _tmpBridge.AreObservedTextAndFontCurrent() &&
                   IsSleepingConfigurationCurrent();
        }

        bool IsSleepingConfigurationCurrent()
        {
            return (_preview == null || _preview.IsRuntimePassThrough) &&
                   _renderers.Count == 0 &&
                   _externalEffectSources.Count == 0 &&
                   (_externalEffectSamples == null || _externalEffectSamples.Count == 0) &&
                   _glyphProvider == null &&
                   _layoutInterpolation == null &&
                   ReferenceEquals(_influenceTarget, null) &&
                   ReferenceEquals(_behaviorProfile, _syncedBehaviorProfile) &&
                   (_syncedBehaviorProfile is null ||
                    _syncedBehaviorProfile.ReactionSerializationRevision == _syncedBehaviorProfileRevision);
        }

        void EnsureAppearanceStyleCurrent(bool force = false)
        {
            TextStudioAppearanceSource source = AppearanceSource;
            TextStudio2DStyleAsset style = AppearanceStyle;
            bool hasInline = source == TextStudioAppearanceSource.Inline && !_inlineAppearanceSettings.IsEmpty;
            ulong dependencyRevision = hasInline
                ? TextStudio2DStyleCompiler.ComputeDependencyRevision(in _inlineAppearanceSettings)
                : TextStudio2DStyleCompiler.ComputeDependencyRevision(style);
            if (!force &&
                _compiledAppearanceMode == source &&
                ReferenceEquals(_compiledAppearanceSource, style) &&
                _appearanceDependencyRevision == dependencyRevision &&
                _compiledAppearanceStyle != null)
            {
                EnsureAppearancePlanCurrent();
                return;
            }

            CompiledTextStudio2DStyle previous = _compiledAppearanceStyle;
            CompiledTextStudio2DStyle current = hasInline
                ? TextStudio2DStyleCompiler.Compile(in _inlineAppearanceSettings, TextStudio2DCompileContext.Full)
                : TextStudio2DStyleCompiler.Compile(style, TextStudio2DCompileContext.Full);
            _compiledAppearanceStyle = current;
            _compiledAppearanceSource = style;
            _compiledAppearanceMode = source;
            _appearanceDependencyRevision = current.DependencyRevision;
            EnsureAppearancePlanCurrent(force);

            TextStudioStackChange changes = TextStudio2DStyleCompiler.ClassifyChanges(
                previous,
                current);
            if (changes == TextStudioStackChange.None ||
                !SupportsActiveTextStudio2DAppearance)
                return;

            DirtyFlags dirty = DirtyFlags.Visual;
            if ((changes & TextStudioStackChange.Topology) != 0)
                dirty |= DirtyFlags.AppearanceTopology;
            if ((changes & TextStudioStackChange.Parameters) != 0)
                dirty |= DirtyFlags.AppearanceParameters;
            if ((changes & TextStudioStackChange.RenderState) != 0)
                dirty |= DirtyFlags.AppearanceRenderState;
            if ((changes & TextStudioStackChange.Bounds) != 0)
            {
                dirty |= DirtyFlags.AppearanceBounds;
                if (TextStudio2DFieldPadding.ResolveOrNone(previous) !=
                    TextStudio2DFieldPadding.ResolveOrNone(current))
                {
                    dirty |= DirtyFlags.Layout;
                }
            }
            bool previousPassThrough = previous == null || previous.IsPassThrough;
            bool currentPassThrough = current == null || current.IsPassThrough;
            if (previousPassThrough != currentPassThrough)
                dirty |= DirtyFlags.Layout;
            _runtime.SetDirty(dirty);
        }

        void EnsureWritingStyleCurrent(bool force = false)
        {
            TextStudioWritingStyleAsset style = WritingStyle;
            ulong dependencyRevision =
                TextStudioWritingCompiler.ComputeDependencyRevision(style);
            if (!force &&
                ReferenceEquals(_compiledWritingSource, style) &&
                _writingDependencyRevision == dependencyRevision &&
                _compiledWritingStyle != null)
            {
                return;
            }

            CompiledTextStudioWritingStyle previous = _compiledWritingStyle;
            CompiledTextStudioWritingStyle current =
                TextStudioWritingCompiler.Compile(style);
            _compiledWritingStyle = current;
            _compiledWritingSource = style;
            _writingDependencyRevision = current.DependencyRevision;

            TextStudioStackChange changes =
                TextStudioWritingCompiler.ClassifyChanges(previous, current);
            if (changes == TextStudioStackChange.None)
                return;

            DirtyFlags dirty = DirtyFlags.Visual;
            if ((changes & TextStudioStackChange.Topology) != 0)
                dirty |= DirtyFlags.WritingTopology;
            if ((changes & TextStudioStackChange.Parameters) != 0)
                dirty |= DirtyFlags.WritingParameters;
            if ((changes & TextStudioStackChange.RenderState) != 0)
                dirty |= DirtyFlags.WritingRenderState;
            if ((changes & TextStudioStackChange.Bounds) != 0)
                dirty |= DirtyFlags.WritingBounds;
            _runtime.SetDirty(dirty);
        }

        void FlushVisualStateImmediate()
        {
            if (!isActiveAndEnabled || _isTicking || _runtime.Plan == null)
                return;

            Tick(new TimeStep(0f, _time, Time.frameCount));
        }
    }
}
