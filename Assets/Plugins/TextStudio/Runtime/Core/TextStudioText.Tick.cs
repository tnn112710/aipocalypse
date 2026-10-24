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
        // Development players and the Editor record these; release players compile the scopes out. They attribute the
        // stage measurements above and the hub work between them.
        static readonly ProfilerMarker TickProfilerMarker =
            new ProfilerMarker("Text Studio Tick");
        static readonly ProfilerMarker SteadyTickProfilerMarker =
            new ProfilerMarker("Text Studio Steady Tick");
        static readonly ProfilerMarker ImmediateFlushProfilerMarker =
            new ProfilerMarker("Text Studio Immediate Flush");
        static readonly ProfilerMarker TextPullProfilerMarker =
            new ProfilerMarker("Text Studio Text Pull");
        static readonly ProfilerMarker BehaviorSyncProfilerMarker =
            new ProfilerMarker("Text Studio Behavior Sync");
        static readonly ProfilerMarker BackendPublicationProfilerMarker =
            new ProfilerMarker("Text Studio Backend Publication");
        static readonly ProfilerMarker PassThroughProfilerMarker =
            new ProfilerMarker("Text Studio Pass-Through");
        static readonly ProfilerMarker PresentProfilerMarker =
            new ProfilerMarker("Text Studio Present");
        static readonly ProfilerMarker AppearancePlanProfilerMarker =
            new ProfilerMarker("Text Studio Appearance Plan");
        static readonly ProfilerMarker TextChangeProfilerMarker =
            new ProfilerMarker("Text Studio Text Change");
        static readonly ProfilerMarker TextChangeCommitProfilerMarker =
            new ProfilerMarker("Text Studio Text Change Commit");
        static readonly ProfilerMarker RevealStartProfilerMarker =
            new ProfilerMarker("Text Studio Reveal Start");

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

        // Entry of this parked hub's TextMeshProUGUI in TextStudioTMPScaleWatch, otherwise -1.
        [NonSerialized] internal int TMPScaleWatchSlot = -1;

        // TextStudioHubScheduler calls this from its LateUpdate for every enabled hub in Play Mode with the frame's deltas
        // and frame count, except while the hub is parked.
        internal void ScheduledLateUpdate(float deltaTime, float unscaledDeltaTime, int frame)
        {
            ClockMode clockMode = _timing.ClockMode;
            if (_isIdleSleeping &&
                ContinueIdleSleep(clockMode == ClockMode.Unscaled ? unscaledDeltaTime : deltaTime))
            {
                TryPark();
                return;
            }

            // Scaled and Unscaled clocks step with the scheduler's frame deltas, as ContinueIdleSleep does; HostTime.Step
            // reads the same Time values again plus a host time that Tick never reads. Other clocks keep HostTime.Step.
            TimeStep step = clockMode == ClockMode.Scaled || clockMode == ClockMode.Unscaled
                ? new TimeStep(
                    HostTime.ClampDelta(clockMode == ClockMode.Unscaled ? unscaledDeltaTime : deltaTime, _timing.MaxDeltaTime) *
                    _timing.TimeScale,
                    0d,
                    frame)
                : HostTime.Step(ref _lastHostTime, frame, _timing);
            _isScheduledTick = true;
            try
            {
                Tick(step);
            }
            finally
            {
                _isScheduledTick = false;
            }
        }

        DirtyFlags _globalDirtyThisTick;
        bool _isTicking;
        bool _presentReplacementAfterReveal;
        bool _isIdleSleeping;
        bool _isParked;
        // Set while ScheduledLateUpdate's tick runs, up to its PresentationUpdated callback; only scheduled Play Mode ticks run
        // the steady lane.
        bool _isScheduledTick;
        // Set while FlushVisualStateImmediate's tick runs for a scheduled hub, up to its PresentationUpdated callback. Such a
        // flush is a full tick, so like a scheduled one it may arm the steady lane for the next scheduled tick.
        bool _isImmediateFlush;
        // Whether the next scheduled tick may run as TickSteady; Unpark and EndSteadyLane clear it, IsSteadyLaneCurrent
        // confirms it.
        bool _steadyLaneArmed;
        // HostNotificationRevision where the last tick armed or kept the steady lane.
        uint _steadyRuntimeRevision;
        // Counts steady ticks for tests and diagnostics.
        int _steadyTickCountForDebug;
        Action _unpark;
        // The dirt an awake tick found where TickInternal decides sleep entry; only IdleSleepBlockerForDebug reads it.
        DirtyFlags _lastAwakeTickDirtyForDebug;
        bool _hasSyncedBehaviorProfile;
        TextStudioBehaviorProfile _syncedBehaviorProfile;
        uint _syncedBehaviorProfileRevision;

        internal bool IsIdleSleepingForDebug => _isIdleSleeping;
        internal bool IsParkedForDebug => _isParked;
        internal bool IsSteadyLaneArmedForDebug => _steadyLaneArmed;
        internal int SteadyTickCountForDebug => _steadyTickCountForDebug;
        // Whether ticks may skip SyncBehaviorConfiguration (CanUseCachedSettingsForEarlySkip); reads without side effects.
        internal bool UsesCachedBehaviorSettingsForDebug => CanUseCachedSettingsForEarlySkip();
        // Frames the TMP bridge applied through RenderPlainFrame.
        internal int PlainTextFrameCountForDebug => _tmpBridge != null ? _tmpBridge.PlainFrameCountForDebug : 0;

        // Names the first condition that keeps the text awake, or null while it sleeps: the inputs of CanSleepWhileIdle, the
        // cached settings, unobserved text and idle work TickInternal checks before sleep entry, and the effect content
        // ContinueIdleSleep reads. Canvas payload, camera and polled appearance checks mark dirt, so a text that passes every
        // check names the dirt its last awake tick found, such as a TMP regeneration it re-presented. Reads without side effects.
        internal string IdleSleepBlockerForDebug
        {
            get
            {
                if (!Application.isPlaying) return "edit mode";
                string backend = UsesCustomTextBackend ? CustomTextBackendSleepBlocker() : null;
                if (backend != null) return backend;
                if (_hasExternalTextSource) return "external text source";
                if (!UsesCustomTextBackend && _tmpBridge == null) return "no TMP bridge";
                if (_timing.ClockMode != ClockMode.Scaled && _timing.ClockMode != ClockMode.Unscaled) return "clock " + _timing.ClockMode;
                if (NeedsReactionSync()) return "reactions";
                if (WritingStyle != null) return "writing style";
                if (_characterSubstitutionPresenter.HasActiveSubstitutions) return "character substitutions";
                if (!IsSleepingConfigurationCurrent()) return "configuration: " + SleepingConfigurationBlockerForDebug();
                string cachedSettings = CachedSettingsBlockerForDebug();
                if (cachedSettings != null) return cachedSettings;
                if (!IsObservedTextBackendCurrent()) return UsesCustomTextBackend ? "unobserved backend text" : "observed text or font";
                if (_runtime.Plan == null) return "no compiled plan";
                if (_runtime.DirtyFlags != DirtyFlags.None) return "dirty " + _runtime.DirtyFlags;
                string work = IdleWorkBlockerForDebug();
                if (work != null) return work;
                if (!_runtime.IsEffectContentCurrent) return "effect content";
                if (_isIdleSleeping) return null;
                return _lastAwakeTickDirtyForDebug != DirtyFlags.None
                    ? "last tick ran dirty " + _lastAwakeTickDirtyForDebug
                    : "settling";
            }
        }

        // Names the input CanUseCachedSettingsForEarlySkip rejects, in its order.
        string CachedSettingsBlockerForDebug()
        {
            if (!IsBehaviorProfileSyncCurrent()) return "behavior profile typewriter source";
            if (_typewriterPreset != null) return "typewriter preset";
            if (_timings != null) return "typing timings";
            if (_typewriterRevealEffect != null || _typewriterHideEffect != null) return "typewriter effect asset";
            if (!string.IsNullOrEmpty(_typewriterRevealEffectExpression) || !string.IsNullOrEmpty(_typewriterHideEffectExpression))
                return "typewriter effect expression";
            return null;
        }

        // Names the work CanSkipIdleTick waits for once the sleeping configuration is current, splitting continuous
        // evaluation into its sources.
        string IdleWorkBlockerForDebug()
        {
            if (RequiresContinuousEvaluation && !IsRenderingRawExternalOutput)
            {
                if (!_runtime.RequiresContinuousEvaluation) return "continuous appearance";
                if (_runtime.Reveal.HasActiveReveal) return "reveal";
                if (_runtime.Animator.HasActiveVisibilityTransitions) return "visibility transitions";
                if (_runtime.HasExternalEffectState) return "external effect state";
                if (_runtime.HasReactionWork) return "reaction work";
                return "continuous effects";
            }
            if (_runtime.ActiveActionCount > 0) return "actions";
            if (_placementMode == PlacementMode.Wave || _placementMode == PlacementMode.Spline) return "placement " + _placementMode;
            if (_preview != null && (_preview.IsPlaying || _preview.IsScrubbing)) return "preview playback";
            return null;
        }
        internal float PresentationTimeForDebug =>
            _isParked ? TextStudioHubScheduler.ParkedPresentationTime(SchedulerSlot) : _time;
        internal float PlacementTimeForDebug =>
            _isParked ? TextStudioHubScheduler.ParkedPlacementTime(SchedulerSlot) : _placementTime;
        Action _presentationUpdated;

        // Subscribers such as surface groups observe every presentation update, including those of a sleeping hub, so a
        // subscribed hub sleeps in the scheduler loop instead of parking.
        internal event Action PresentationUpdated
        {
            add
            {
                _presentationUpdated += value;
                Unpark();
            }
            remove => _presentationUpdated -= value;
        }
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

            // A tick reads the clocks a parked hub keeps in its scheduler slot; an awake hub keeps its steady lane.
            if (_isParked)
                Unpark();
            _isTicking = true;
            try
            {
                using (TickProfilerMarker.Auto())
                {
                    TickInternal(step);
                    // A callback that replaced the text during the reveal stage ended that pass before presentation. Present the
                    // replacement with a zero-time pass, as FlushVisualStateImmediate does after a change outside a tick; a further
                    // replacement during that pass is laid out by the next tick.
                    if (_presentReplacementAfterReveal)
                    {
                        _presentReplacementAfterReveal = false;
                        TickInternal(new TimeStep(0f, _time, step.Frame));
                        _presentReplacementAfterReveal = false;
                    }
                }
            }
            finally
            {
                _isTicking = false;
                // Ticks that PresentationUpdated subscribers start are neither scheduled nor flushes.
                _isScheduledTick = false;
                _isImmediateFlush = false;
            }
            _presentationUpdated?.Invoke();
        }

        void TickInternal(in TimeStep step)
        {
            if (_steadyLaneArmed && _isScheduledTick && IsSteadyLaneCurrent())
            {
                using (SteadyTickProfilerMarker.Auto())
                    TickSteady(step.DeltaTime);
                return;
            }

            _steadyLaneArmed = false;
            _runtime.RefreshTextProjectionBackend(_activeTextBackend as ITextStudioTextProjectionBackend);
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
            _lastAwakeTickDirtyForDebug = _runtime.DirtyFlags;
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
            bool pulledText;
            using (TextPullProfilerMarker.Auto())
                pulledText = ProcessTextBackendPull(isDrivingTextBackend);

            if (CanUseCachedSettingsForEarlySkip() &&
                (CanSkipIdleTick(pulledText) ||
                 CanSkipInvisiblePersistentEffects(isDrivingTextStudioOutput)))
            {
                return;
            }

            using (BehaviorSyncProfilerMarker.Auto())
                SyncBehaviorConfiguration();

            if (CanSkipIdleTick(pulledText))
                return;
            if (CanSkipInvisiblePersistentEffects(isDrivingTextStudioOutput))
                return;

            // TickSteady resumes the next scheduled tick at this point while nothing the checks above read has changed.
            _steadyLaneArmed = (_isScheduledTick || _isImmediateFlush) && CanArmSteadyLane();
            _steadyRuntimeRevision = _runtime.HostNotificationRevision;
            _globalDirtyThisTick = _runtime.DirtyFlags;

            // Stage 1: pipeline (text+style → compiled plan).
            if (_runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Style))
            {
                bool planChanged;
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

            bool usesPassThrough;
            using (PassThroughProfilerMarker.Auto())
                usesPassThrough = TryUseUnanimatedTMPPassThrough(isDrivingTextBackend, isDrivingTextStudioOutput);
            if (usesPassThrough)
                return;

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
            RunPlacementStage(layoutChanged);

            // Stage 4: reveal tick.
            uint laidOutSourceVersion = _runtime.Plan != null ? _runtime.Plan.SourceVersion : 0u;
            if (!Application.isPlaying)
                _editModeActionRunner.Tick(dt);

            DirtyFlags dirtyBeforeReveal = _runtime.DirtyFlags;
            float revealDeltaTime = previewStartedDisappear || previewDroveReveal ? 0f : dt;
            // This tick presents the reveal's own dirt, so it does not end the steady lane.
            _runtime.TickReveal(revealDeltaTime, !Preview.IsScrubbing && !HasExternalRevealClockOwner, false);
            _globalDirtyThisTick |= _runtime.DirtyFlags & ~dirtyBeforeReveal;
            // Completion, marker and wait callbacks can compile replacement text after this pass laid out the previous text.
            // Presenting that layout over the backend mesh regenerated for the replacement would make the next layout cache
            // the result as the replacement's source glyphs, so Tick presents the replacement in a follow-up pass instead.
            if (_runtime.Plan != null && _runtime.Plan.SourceVersion != laidOutSourceVersion)
            {
                _presentReplacementAfterReveal = true;
                return;
            }

            // Stage 5: presentation pass (addons).
            UpdatePresentation(dt);
            _globalDirtyThisTick |= _runtime.DirtyFlags;

            using (PresentProfilerMarker.Auto())
                PresentEvaluatedFrame(dt, false);
        }

        // The steady lane: a scheduled tick of animated text whose configuration has not changed since its last full tick
        // (IsSteadyLaneCurrent). It makes the full tick's state changes in the same order and skips only checks whose
        // outcome that configuration fixes (CanArmSteadyLane).
        void TickSteady(float stepDeltaTime)
        {
            unchecked { _steadyTickCountForDebug++; }
            _isIdleSleeping = false;
            // The preview passes runtime time through (IsRuntimePassThrough), so its effective deltas equal the host delta.
            float hostDeltaTime = _runtime.ResolvePresentationDeltaTime(stepDeltaTime);
            _time += hostDeltaTime;
            _placementTime += hostDeltaTime;
            // Reactions, appearance, writing, external samples, the TMP font and text, cached settings, placement-only work,
            // the action runner and the preview leave nothing to do before the pull, and the pull finds no change; it only
            // confirms a generated write that TMP still holds, such as the text an immediate flush pushed.
            _lastAwakeTickDirtyForDebug = _runtime.DirtyFlags;
            _tmpBridge.ConfirmSteadyText();
            // CanSkipInvisiblePersistentEffects with the steady answers to its conditions: no actions, renderers, character
            // substitutions, external effects, glyph provider or preview playback.
            if (_runtime.HasOnlyPersistentEffectEvaluationWithoutExternalEffects &&
                _placementMode != PlacementMode.Wave &&
                _placementMode != PlacementMode.Spline &&
                !TmpBridge.IsVisualOutputEnabled)
            {
                return;
            }

            // SyncBehaviorConfiguration finds every setting applied; its motion policy write still marks the runtime dirty.
            _runtime.MotionPolicy = _motionPolicy;
            _steadyRuntimeRevision = _runtime.HostNotificationRevision;
            _globalDirtyThisTick = _runtime.DirtyFlags;

            // No pipeline, deferred reveal or external effect source work. The last full tick pushed the plan's text, which TMP
            // still holds (IsSteadyTextCurrent), so PushTextBackend would write nothing, and text that needs per-glyph output
            // draws no pass-through and lays nothing out.
            RunPlacementStage(false);

            uint laidOutSourceVersion = _runtime.Plan != null ? _runtime.Plan.SourceVersion : 0u;
            DirtyFlags dirtyBeforeReveal = _runtime.DirtyFlags;
            _runtime.TickReveal(hostDeltaTime, !HasExternalRevealClockOwner, false);
            _globalDirtyThisTick |= _runtime.DirtyFlags & ~dirtyBeforeReveal;
            if (_runtime.Plan != null && _runtime.Plan.SourceVersion != laidOutSourceVersion)
            {
                _presentReplacementAfterReveal = true;
                return;
            }

            // UpdatePresentation without presentation glyphs or a glyph provider only ends a time discontinuity.
            _presentationTimeDiscontinuous = false;
            _globalDirtyThisTick |= _runtime.DirtyFlags;
            using (PresentProfilerMarker.Auto())
                PresentEvaluatedFrame(hostDeltaTime, true);
        }

        // What the full tick's skipped checks read that changes without notifying the hub, compared before each steady
        // tick. The runtime reports its changes through HostNotificationRevision, the hub through Unpark and EndSteadyLane.
        // A destroyed profile resolves like none in SyncBehaviorConfiguration, so its liveness is compared too. Continuous
        // evaluation, from continuous effects or a running reveal, hide or visibility transition, keeps the idle skip off.
        bool IsSteadyLaneCurrent()
        {
            return _runtime.HostNotificationRevision == _steadyRuntimeRevision &&
                   _runtime.DirtyFlags == DirtyFlags.None &&
                   _runtime.IsEffectContentCurrent &&
                   _runtime.RequiresContinuousEvaluation &&
                   _layoutInterpolation == null &&
                   ReferenceEquals(_behaviorProfile, _syncedBehaviorProfile) &&
                   (_syncedBehaviorProfile is null ||
                    _syncedBehaviorProfile != null &&
                    _syncedBehaviorProfile.ReactionSerializationRevision == _syncedBehaviorProfileRevision) &&
                   _tmpBridge.IsSteadyTextCurrent();
        }

        // Whether each check TickSteady skips has a fixed outcome until a notification or a polled input changes: the TMP
        // backend drives the text, nothing is synced, polled or sampled besides IsSteadyLaneCurrent's inputs, settings come
        // from component fields or a revisioned profile, continuous evaluation keeps the idle skip off, and output needs no
        // renderer, external effect, glyph provider or preview negotiation. Every condition is a side-effect-free read, so the
        // field checks come first.
        bool CanArmSteadyLane()
        {
            return _activeTextBackend == null &&
                   _activeTextSourceBackend == null &&
                   _activeMeshBackend == null &&
                   !_hasExternalTextSource &&
                   _tmpBridge != null &&
                   _backendWriteHandling != TextStudioBackendWriteHandling.ImportAsSource &&
                   (_externalEffectSamples == null || _externalEffectSamples.Count == 0) &&
                   _externalEffectSources.Count == 0 &&
                   _renderers.Count == 0 &&
                   _glyphProvider == null &&
                   _presentationGlyphs == null &&
                   _layoutInterpolation == null &&
                   !_startRevealAfterDeferredPipeline &&
                   _hasAppliedRuntimeSettings &&
                   _preview != null &&
                   _preview.IsRuntimePassThrough &&
                   _runtime.ActiveActionCount == 0 &&
                   !_runtime.HasExternalEffectState &&
                   _runtime.RequiresContinuousEvaluation &&
                   (_compiledAppearancePlan == null || !_compiledAppearancePlan.HasPolledDependencies) &&
                   !NeedsReactionSync() &&
                   CanUseCachedSettingsForEarlySkip() &&
                   !HasPolledAppearance() &&
                   WritingStyle == null &&
                   _tmpBridge.IsSteadyTextCurrent();
        }

        // Changes of steady lane inputs that neither mark the runtime dirty nor unpark the hub end the lane here.
        void EndSteadyLane() => _steadyLaneArmed = false;

        // Stage 3: placement after a layout or placement change, and every tick for moving placement modes.
        void RunPlacementStage(bool layoutChanged)
        {
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
        }

        // Stages 6 and 7 and the work after dispatch, shared by the full tick and the steady lane, which runs only in Play
        // Mode and so never renders raw external output.
        void PresentEvaluatedFrame(float dt, bool steadyLane)
        {
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
            bool renderRawWithoutEffects = !steadyLane && IsRenderingRawExternalOutput;
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
                if (DispatchFrame(dt, steadyFrame: steadyLane))
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
        // Its inline typewriter settings resolve from that serialized data alone unless they reference timing or effect assets
        // or effect expressions, which the component's own inline settings exclude as well.
        bool IsBehaviorProfileSyncCurrent()
        {
            if (_behaviorProfile == null)
                return true;

            if (!ReferenceEquals(_syncedBehaviorProfile, _behaviorProfile) ||
                _syncedBehaviorProfileRevision != _behaviorProfile.ReactionSerializationRevision)
            {
                return false;
            }

            if (!_behaviorProfile.Provides(TextStudioBehaviorDomain.Typewriter))
                return true;

            ref readonly TextStudioTypewriterRecipe typewriter = ref _behaviorProfile.TypewriterRecipe;
            switch (typewriter.Source)
            {
                case TextStudioTypewriterSource.None:
                case TextStudioTypewriterSource.BuiltIn:
                    return true;
                case TextStudioTypewriterSource.Inline:
                    return typewriter.InlineSettings.Timings == null &&
                           typewriter.InlineSettings.RevealEffect == null &&
                           typewriter.InlineSettings.HideEffect == null &&
                           string.IsNullOrEmpty(typewriter.InlineSettings.RevealEffectExpression) &&
                           string.IsNullOrEmpty(typewriter.InlineSettings.HideEffectExpression);
                default:
                    return false;
            }
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
        // content revision, the synced profile and TMP's text and font references, or a custom backend's sleep blocker and
        // unpulled text, wake it, and appearance output also runs the settled tick's appearance checks; hubs with inputs that
        // are only polled otherwise stay awake. Time advances with Tick's arithmetic, so later frames read the same clocks.
        bool ContinueIdleSleep(float deltaTime)
        {
            if (_runtime.DirtyFlags != DirtyFlags.None ||
                _runtime.ActiveActionCount > 0 ||
                !_runtime.IsEffectContentCurrent ||
                !IsSleepingConfigurationCurrent() ||
                UsesCustomTextBackend && CustomTextBackendSleepBlocker() != null ||
                !IsObservedTextBackendCurrent() ||
                HasPolledAppearance() && !IsPolledAppearanceCurrent())
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
            _presentationUpdated?.Invoke();
            return true;
        }

        bool CanSleepWhileIdle()
        {
            return Application.isPlaying &&
                   (UsesCustomTextBackend ? CustomTextBackendSleepBlocker() == null : _tmpBridge != null) &&
                   !_hasExternalTextSource &&
                   (_timing.ClockMode == ClockMode.Scaled || _timing.ClockMode == ClockMode.Unscaled) &&
                   !NeedsReactionSync() &&
                   WritingStyle == null &&
                   !_characterSubstitutionPresenter.HasActiveSubstitutions &&
                   IsObservedTextBackendCurrent() &&
                   IsSleepingConfigurationCurrent();
        }

        // A custom text backend lets its settled host sleep only through ITextStudioIdleTextBackend, which promises that every
        // change of its target arrives through Changed or a host rebuild request before the scheduled update.
        string CustomTextBackendSleepBlocker() =>
            _activeTextBackend is ITextStudioIdleTextBackend backend ? backend.IdleSleepBlocker : "custom text backend";

        // The backend text a sleeping hub compares: TMP's text and font references, or the custom backend's unpulled text.
        bool IsObservedTextBackendCurrent() =>
            UsesCustomTextBackend
                ? !_activeTextSourceBackend.HasUnobservedTextChange
                : _tmpBridge.AreObservedTextAndFontCurrent();

        // A cleared Style source draws no appearance, exactly like empty inline settings.
        bool HasNoAppearanceSource() => AppearanceSource == TextStudioAppearanceSource.Inline
            ? _inlineAppearanceSettings.IsEmpty
            : _appearanceStyle == null;

        // Appearance output reads inputs without change events every frame: the paint assets and textures of its styles, its
        // canvas pose and its camera demand. Styled text parks with those inputs its slot can compare (CaptureParkedAppearance);
        // text whose inputs need the hub, such as prepared-field camera demand or appearance tags, sleeps in the loop and polls.
        bool HasPolledAppearance()
        {
            if (!HasNoAppearanceSource())
                return true;
            CompiledTextStudioAppearancePlan plan = ActiveCompiledAppearancePlan;
            return plan != null && !plan.IsPassThrough;
        }

        // The appearance checks of the settled tick, in its order and marking the dirt CanSkipIdleTick marks. Like that check,
        // the host of a custom text backend polls no TMP canvas payload or camera quality.
        bool IsPolledAppearanceCurrent()
        {
            EnsureAppearanceStyleCurrent();
            if (_runtime.DirtyFlags != DirtyFlags.None)
                return false;
            if (UsesCustomTextBackend)
                return true;
            if (_tmpBridge.NeedsCanvasPayloadRefresh())
            {
                _runtime.SetDirty(DirtyFlags.Visual);
                return false;
            }
            if (_tmpBridge.NeedsCameraQualityRefresh())
            {
                _runtime.SetDirty(DirtyFlags.AppearanceParameters);
                return false;
            }
            return true;
        }

        // A sleeping hub parks: the scheduler advances its clocks without calling it, and every change of an input
        // ContinueIdleSleep reads unparks it. Unparking keeps the hub asleep, so its next scheduled update decides through
        // ContinueIdleSleep exactly as before. PresentationUpdated subscribers keep it polling, TMP types without write
        // callbacks are probed by the scheduler, and so are the polled appearance inputs of styled text. A custom text backend
        // that lets its host sleep reports every target change through Changed, which unparks the hub through SetDirty.
        void TryPark()
        {
            if (_isParked || SchedulerSlot < 0 || _presentationUpdated != null)
                return;
            TextStudioHubScheduler.ParkedAppearance appearance = default;
            if (HasPolledAppearance() && CaptureParkedAppearance(out appearance) != null)
                return;

            _isParked = true;
            _runtime.UnparkHost = _unpark ??= Unpark;
            TextStudioHubScheduler.Park(
                SchedulerSlot,
                _time,
                _placementTime,
                _timing.ClockMode == ClockMode.Unscaled,
                _timing.MaxDeltaTime,
                _timing.TimeScale,
                _runtime.IsAnimationPaused,
                UsesCustomTextBackend || _tmpBridge.ReportsTMPWrites ? null : _tmpBridge,
                in appearance);
            // While parked, the per-frame scale check TMP runs for this TextMeshProUGUI moves to one batched watch. Font writes,
            // which can give that check an environment map to update, unpark the hub through TMP's layout callback first.
            TextMeshProUGUI scaleWatchText = UsesCustomTextBackend ? null : _tmpBridge.WriteReportingText;
            if (!(scaleWatchText is null))
                TextStudioTMPScaleWatch.Watch(this, scaleWatchText);
        }

        // Captures what the scheduler slot compares in place of IsPolledAppearanceCurrent, or names the input that keeps this
        // hub polling in the loop. Exact right after a passing check, which is where TryPark runs: the style revision is the
        // one EnsureAppearanceStyleCurrent compares, and the Canvas payload inputs are those HaveCanvasPayloadInputs compares.
        // The plan must poll nothing else, and camera quality must stay unchanged until a tick (HasStaticCameraQuality);
        // prepared-field demand also follows font data and page availability, which only the hub reads. The settled checks of a
        // custom text backend's host read only the style revision.
        internal string CaptureParkedAppearance(out TextStudioHubScheduler.ParkedAppearance appearance)
        {
            appearance = default;
            if (_compiledAppearancePlan != null && _compiledAppearancePlan.HasPolledDependencies) return "appearance tag dependencies";
            if (!UsesCustomTextBackend)
            {
                if (!_tmpBridge.HasStaticCameraQuality) return "camera demand";
                if (!_tmpBridge.TryCaptureParkedCanvasPayload(ref appearance)) return "canvas pose";
            }
            appearance.Active = true;
            appearance.Inline = AppearanceSource == TextStudioAppearanceSource.Inline && !_inlineAppearanceSettings.IsEmpty;
            if (appearance.Inline) appearance.InlineSettings = _inlineAppearanceSettings;
            else appearance.Style = AppearanceStyle;
            appearance.Revision = _appearanceDependencyRevision;
            return null;
        }

        // Names why a sleeping text stays in the scheduler loop, or null while it parks or could park; reads TMP's cached
        // canvas but changes no hub state.
        internal string ParkingBlockerForDebug
        {
            get
            {
                if (_isParked || !_isIdleSleeping) return null;
                if (_presentationUpdated != null) return "presentation subscribers";
                return HasPolledAppearance() ? CaptureParkedAppearance(out _) : null;
            }
        }

        // The Canvas payload a parked styled text compares belongs to the canvas it parked under. Graphic takes another
        // canvas only in this message, OnTransformParentChanged, its OnEnable (which reports through TMP's layout callback)
        // or after that canvas is destroyed, and every component of the object receives the messages.
        void OnCanvasHierarchyChanged() => Unpark();

        // Animation writes serialized fields such as the appearance source, style and inline settings without their setters
        // or OnValidate. A parked hub compares only what it parked with, so the write hands it back to its sleep check.
        void OnDidApplyAnimationProperties() => Unpark();

        // Hands a parked hub back to its sleep check and leaves an awake hub's steady lane armed, for changes the lane polls.
        internal void UnparkIfParked()
        {
            if (_isParked)
                Unpark();
        }

        // Called by every change of a sleep input and before every read of the clocks; one branch while not parked. The
        // steady lane's checks read the same inputs, so every call also ends it.
        internal void Unpark()
        {
            _steadyLaneArmed = false;
            if (!_isParked)
                return;

            _isParked = false;
            _runtime.UnparkHost = null;
            TextStudioHubScheduler.Unpark(SchedulerSlot, ref _time, ref _placementTime);
            if (TMPScaleWatchSlot != -1)
                TextStudioTMPScaleWatch.Release(this);
        }

        // Names the first sleeping-configuration input that keeps the text awake; mirrors IsSleepingConfigurationCurrent.
        string SleepingConfigurationBlockerForDebug()
        {
            if (_preview != null && !_preview.IsRuntimePassThrough) return "preview playback";
            if (_renderers.Count != 0) return "renderers";
            if (_externalEffectSources.Count != 0 || _externalEffectSamples != null && _externalEffectSamples.Count != 0) return "external effects";
            if (_glyphProvider != null) return "glyph provider";
            if (_layoutInterpolation != null) return "layout interpolation";
            if (!ReferenceEquals(_influenceTarget, null)) return "influence target";
            return "behavior profile";
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

            // A parked hub compares the revision and style it parked with.
            Unpark();
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
            // Layout prepares the payload streams Text Studio's materials read and TextMeshPro's own material does not, so
            // entering or leaving the TextMeshPro route, also for values its source materials cannot draw, takes effect in the
            // same update.
            if (TextStudio2DTextMeshProRoute.IsCandidate(previous) != TextStudio2DTextMeshProRoute.IsCandidate(current) ||
                _tmpBridge != null && _tmpBridge.LeavesTextMeshProRoute(current, ActiveCompiledAppearancePlan))
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
            // Writing draws with Text Studio's materials, whose payload streams layout prepares, so it leaves the TextMeshPro
            // route in the same update.
            if (_tmpBridge != null && _tmpBridge.UsesTextMeshProRoute &&
                TextStudioWritingRenderer.RequiresWritingMaterial(_runtime.Plan, current))
                dirty |= DirtyFlags.Layout;
            _runtime.SetDirty(dirty);
        }

        void FlushVisualStateImmediate()
        {
            if (!isActiveAndEnabled || _isTicking || _runtime.Plan == null)
                return;

            // The flush stays an eager full tick, so everything a caller observes before the next scheduled tick is unchanged;
            // like a scheduled tick it may arm the steady lane, so that tick, such as the same frame's after an emission's
            // StartReveal, can run TickSteady.
            _isImmediateFlush = SchedulerSlot >= 0;
            try
            {
                using (ImmediateFlushProfilerMarker.Auto())
                    Tick(new TimeStep(0f, _time, Time.frameCount));
            }
            finally
            {
                _isImmediateFlush = false;
            }
        }
    }
}
