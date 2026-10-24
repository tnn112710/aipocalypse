using System;
using System.Collections.Generic;
using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Core text API
        // ─────────────────────────────────────────────────────────────────────

        public void SetText(string markup)
        {
            RequestTextChange(markup, true, false);
        }

        /// <summary>Replaces markup using a scope for this change only, preserving the authored settings and start policy.</summary>
        public void SetText(string markup, TextStudioTextChangeScope scope)
        {
            RequestTextChange(markup, true, false, scopeOverride: scope);
        }

        /// <summary>Replaces markup and optionally processes visible output before returning.</summary>
        /// <param name="markup">Markup to compile. Null is treated as empty text.</param>
        /// <param name="syncTextImmediate">Whether to process the current authoring mode synchronously when safe.</param>
        public void SetText(string markup, bool syncTextImmediate)
        {
            RequestTextChange(markup, syncTextImmediate, false);
        }

        public void SetTextDeferred(string markup)
        {
            RequestTextChange(markup, false, false);
        }

        /// <summary>Defers a text change using a scope for this change only, preserving authored settings.</summary>
        public void SetTextDeferred(string markup, TextStudioTextChangeScope scope)
        {
            RequestTextChange(markup, false, false, scopeOverride: scope);
        }

        public void AppendText(string markup)
        {
            AppendText(markup, false);
        }

        /// <summary>Appends markup while retaining the prefix, optionally starting the new text regardless of the authored start policy.</summary>
        /// <param name="markup">Markup to add after the current text.</param>
        /// <param name="reveal">Whether to explicitly reveal the appended text. False preserves the authored start policy.</param>
        public void AppendText(string markup, bool reveal)
        {
            if (string.IsNullOrEmpty(markup)) return;
            string appendedMarkup = (_markup ?? string.Empty) + markup;
            RequestTextChange(
                appendedMarkup,
                false,
                reveal,
                preferIncrementalAppend: true);
        }

        /// <summary>Appends markup while preserving the existing prefix's reveal and animation state where possible.</summary>
        public void AppendTextDeferred(string markup)
        {
            AppendText(markup);
        }

        public void ClearText()
        {
            ClearPendingTextChangeState();
            if (_runtime.ReactionsRequireOutgoingCapture)
                RequestTextChange(string.Empty, true, false, forceImmediate: true);
            else
                SetCanonicalMarkup(string.Empty);
            _runtime.ClearText();
            SyncTextImmediateForCurrentMode();
        }

        /// <summary>Switches how authored markup is previewed in edit mode and rebuilds affected pipeline stages.</summary>
        public void SetAuthoringMode(TextStudioAuthoringMode mode)
        {
            if (_authoringMode == mode)
                return;

            _authoringMode = mode;
            if (!Application.isPlaying && mode == TextStudioAuthoringMode.Raw)
                Preview.ShowEndStateAgain();
            else if (!Application.isPlaying)
                _placementTime = 0f;
            ApplyPreviewStageSettings();
            _runtime.SetDirty(DirtyFlags.Text | DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual);
            SyncTextImmediateForCurrentMode();
        }

        /// <summary>Sets how external backend text writes are handled for this component.</summary>
        public void SetBackendWriteHandling(TextStudioBackendWriteHandling handling)
        {
            _backendWriteHandling = handling;
            // Pulls of imported sources read an input field, which the steady lane never polls.
            EndSteadyLane();
        }

        /// <summary>Controls whether preview-safe actions may produce scene side effects in edit mode.</summary>
        public void SetRunActionsInPreview(bool runActions)
        {
            _runActionsInPreview = runActions;
            _runtime.RunActionsInEditModePreview = _runActionsInPreview;
        }

        /// <summary>
        /// Configures the shared edit-mode preview stage without changing runtime rendering.
        /// Attached renderer providers receive the same view and quality selection.
        /// </summary>
        public void ConfigurePreviewStage(
            TextStudioPreviewStageMode mode,
            TextStudioPreviewView view,
            TextStudioPreviewQuality quality,
            Vector2 orbitAngles,
            float compareSpacing = 4f)
        {
            _previewStageMode = mode;
            _previewView = view;
            _previewQuality = quality;
            _previewOrbitAngles = new Vector2(
                Mathf.Repeat(orbitAngles.x + 180f, 360f) - 180f,
                Mathf.Clamp(orbitAngles.y, -85f, 85f));
            _previewCompareSpacing = Mathf.Max(0f, compareSpacing);
            ApplyPreviewStageSettings();
            _runtime.SetDirty(DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Visual);
            FlushVisualStateImmediate();
        }

        /// <summary>
        /// Assigns a renderer-neutral behavior profile, or restores complete local behavior when
        /// passed <see langword="null"/>.
        /// </summary>
        public void SetBehaviorProfile(TextStudioBehaviorProfile profile)
        {
            // Reassigning the profile the last tick applied, unchanged since, would only recompile identical text.
            if (_hasSyncedBehaviorProfile &&
                ReferenceEquals(_behaviorProfile, profile) &&
                ReferenceEquals(_syncedBehaviorProfile, profile) &&
                (profile is null || _syncedBehaviorProfileRevision == profile.ReactionSerializationRevision))
            {
                return;
            }

            _behaviorProfile = profile;
            SyncReactions();
            ApplyBehaviorProfile();
            _runtime.SetDirty(DirtyFlags.Text | DirtyFlags.Style | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>
        /// Selects Style and assigns a persistent 2D appearance stack. A null reference keeps Style
        /// unassigned and preserves native appearance. A backend that owns glyph styling can retain the assignment
        /// without rendering it until a supporting backend becomes active.
        /// </summary>
        public void SetAppearanceStyle(TextStudio2DStyleAsset style)
        {
            TextStudioAppearanceSource source = TextStudioAppearanceSource.Style;
            if (_appearanceStyle == style && _appearanceSource == source)
                return;

            _appearanceStyle = style;
            _appearanceSource = source;
            unchecked { AppearanceAuthoringRevision++; }
            EnsureAppearanceStyleCurrent(true);
            FlushVisualStateImmediate();
        }

        /// <summary>Snapshots the current Typewriter recipe to inline settings and selects its drawing treatment.</summary>
        public void SetTypewriterWritingStyle(TextStudioWritingStyleAsset style)
        {
            TextStudioTypewriterSettings settings = ResolveTypewriterSettings();
            settings.WritingEnabled = style != null;
            settings.WritingStyle = style;
            ApplyTypewriterSettings(settings);
            EnsureWritingStyleCurrent(true);
            FlushVisualStateImmediate();
        }

        /// <summary>Borrowed current layout metadata. Empty while source/layout work is pending; never triggers rendering.</summary>
        internal TextStudioVisualSnapshot CurrentVisualSnapshot => _runtime.Plan == null ||
            _runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Topology | DirtyFlags.Layout) || _layout.VisualGlyphs == null
            ? default
            : new TextStudioVisualSnapshot(_layout.VisualGlyphs, _layout.GlyphCount, _layout.VisualCapabilities,
                _layout.VisualVersion, _layout.SourceTextBounds, _layout.FontSources, _layout.FontSourceCount,
                _layout.RenderGlyphs, _layout.RenderGlyphCount, _layout.UnitToRenderGlyph, _layout.ColorSources, _layout.ColorSourceCount);

        /// <summary>Reads the resolved text and native shaping identity used to match a prepared Writing phrase. Returns false until the current layout is ready; does not rebuild the mesh.</summary>
        public bool TryCaptureWritingPhraseIdentity(
            out string resolvedText,
            out ulong shapingFingerprint,
            out Rect designBounds)
        {
            resolvedText = _runtime.Plan?.ResolvedText ?? string.Empty;
            shapingFingerprint = 0UL;
            designBounds = default;
            if (_runtime.Plan == null ||
                _runtime.IsDirty(DirtyFlags.Text | DirtyFlags.Topology | DirtyFlags.Layout) ||
                _layout.VisualGlyphs == null ||
                _layout.GlyphCount <= 0)
            {
                return false;
            }

            Vector4 sourceBounds = _layout.SourceTextBounds;
            designBounds = Rect.MinMaxRect(
                sourceBounds.x,
                sourceBounds.y,
                sourceBounds.z,
                sourceBounds.w);
            TextStudioVisualSnapshot snapshot = CurrentVisualSnapshot;
            shapingFingerprint = TextStudioWritingShapingFingerprint.Compute(
                resolvedText,
                in snapshot);
            return shapingFingerprint != 0UL;
        }

        /// <summary>
        /// Assigns component-specific syntax settings, or null to inherit the project setting.
        /// </summary>
        public void SetMarkupSyntaxOverride(TextStudioMarkupSyntax syntax)
        {
            if (_markupSyntaxOverride == syntax)
                return;

            _markupSyntaxOverride = syntax;
            _runtime.Pipeline.Invalidate();
            _runtime.SetDirty(
                DirtyFlags.Text |
                DirtyFlags.Style |
                DirtyFlags.Topology |
                DirtyFlags.Layout |
                DirtyFlags.Animation |
                DirtyFlags.Visual);
            SyncTextImmediateForCurrentMode();
        }

        /// <summary>Configures this component's retained local default-effect fallback.</summary>
        public void SetInlineDefaultEffects(
            TextStudioDefaultEffectMode mode,
            string allPhaseEffects = "",
            string revealEffects = "",
            string activeEffects = "",
            string hideEffects = "")
        {
            _inlineDefaultEffectMode = mode;
            _inlineDefaultEffects = allPhaseEffects ?? string.Empty;
            _inlineDefaultRevealEffects = revealEffects ?? string.Empty;
            _inlineDefaultActiveEffects = activeEffects ?? string.Empty;
            _inlineDefaultHideEffects = hideEffects ?? string.Empty;
            ApplyDefaultEffects();
        }

        internal void SyncNowForTests()
        {
            SyncRuntimeConfiguration();
            ApplyDefaultEffects();
            ProcessTMPTextPull();
            SyncValidatedTextImmediate();
        }

        internal void SynchronizeCurrentVisualStateImmediate()
        {
            SyncRuntimeConfiguration();
            ApplyDefaultEffects();
            ProcessTMPTextPull();
            SyncValidatedTextImmediate();
            FlushVisualStateImmediate();
        }

        public void ShowText(string markup)
        {
            RecordInitialRevealRequest(true);
            _preview?.TakeRuntimePlaybackOwnership();
            RequestTextChange(markup, true, true);
        }

        /// <summary>Reveals markup using a scope for this change only, without modifying the authored settings.</summary>
        /// <remarks>Scopes that retain matching glyphs reveal only changed glyphs, so a change that leaves every glyph matching keeps the current reveal. <see cref="TextStudioTextChangeScope.WholeText"/> replays the complete text.</remarks>
        public void ShowText(string markup, TextStudioTextChangeScope scope)
        {
            RecordInitialRevealRequest(true);
            _preview?.TakeRuntimePlaybackOwnership();
            RequestTextChange(markup, true, true, scopeOverride: scope,
                restartUnchanged: scope == TextStudioTextChangeScope.WholeText);
        }

        public void ShowTextInstant(string markup)
        {
            RecordInitialRevealRequest(false);
            _preview?.TakeRuntimePlaybackOwnership();
            _layoutInterpolation?.Reset();
            ClearPendingTextChangeState();
            _runtime.DiscardTextChangeContinuity();
            if (_runtime.ReactionsRequireOutgoingCapture)
                RequestTextChange(markup, true, false, forceImmediate: true);
            else
            {
                SetCanonicalMarkup(markup);
                _runtime.SetText(_markup);
            }
            _startRevealAfterDeferredPipeline = false;
            SkipReveal();
        }

        /// <summary>Marks topology and all dependent presentation stages for refresh on the next update.</summary>
        public void ScheduleMeshRefresh()
        {
            ScheduleTopologyRebuild();
        }

        /// <summary>Marks authored text for recompilation on the next update.</summary>
        public void ScheduleTextRebuild()
        {
            _runtime.SetDirty(DirtyFlags.Text);
        }

        /// <summary>Marks layout, placement, animation, and visual output for recomputation.</summary>
        public void ScheduleLayoutRebuild()
        {
            _runtime.SetDirty(DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>Marks renderer topology and every dependent presentation stage for recomputation.</summary>
        public void ScheduleTopologyRebuild()
        {
            _runtime.SetDirty(DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>Marks evaluated animation and visual output for refresh without rebuilding text or layout.</summary>
        public void ScheduleVisualStateUpdate()
        {
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>Marks simulation-backed channels and visual output for refresh.</summary>
        public void ScheduleSimulationUpdate()
        {
            _runtime.SetDirty(DirtyFlags.Simulation | DirtyFlags.Visual);
        }

        /// <summary>Registers a component-local external text source and rediscovers source priority.</summary>
        public void RegisterExternalTextSource(ITextStudioExternalTextSource source)
        {
            if (source == null || _registeredExternalSources.Contains(source))
                return;

            _registeredExternalSources.Add(source);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
        }

        /// <summary>Stops using a previously registered component-local external text source.</summary>
        public void UnregisterExternalTextSource(ITextStudioExternalTextSource source)
        {
            if (source == null)
                return;

            _registeredExternalSources.Remove(source);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
            else
            {
                _externalSources.Clear();
                _hasExternalTextSource = false;
            }
        }

        /// <summary>Registers an ordered external effect source with this host.</summary>
        public void RegisterExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (source == null || _registeredExternalEffectSources.Contains(source))
                return;

            _registeredExternalEffectSources.Add(source);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
            else
                _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>Stops polling a previously registered external effect source.</summary>
        public void UnregisterExternalEffectSource(ITextStudioExternalEffectSource source)
        {
            if (source == null)
                return;

            _registeredExternalEffectSources.Remove(source);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
            else
                _externalEffectSources.Clear();
            _runtime.SetDirty(DirtyFlags.Animation | DirtyFlags.Visual);
        }

        /// <summary>Registers a component-local processor for plan and reveal extension-tag callbacks.</summary>
        public void RegisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (processor == null || _registeredExtensionProcessors.Contains(processor))
                return;

            _registeredExtensionProcessors.Add(processor);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
        }

        /// <summary>Stops dispatching extension-tag callbacks to a component-local processor.</summary>
        public void UnregisterExtensionTagProcessor(ITextStudioExtensionTagProcessor processor)
        {
            if (processor == null)
                return;

            _registeredExtensionProcessors.Remove(processor);
            if (isActiveAndEnabled)
                DiscoverRenderersAndProviders();
            else
            {
                _runtime.UnregisterExtensionTagProcessor(processor);
                _extensionProcessors.Remove(processor);
            }
        }

        /// <summary>Performs one synchronous presentation update using an explicit non-negative delta time in seconds.</summary>
        public void ForceMeshRefresh(float deltaTime = 0f)
        {
            ScheduleMeshRefresh();
            Tick(new TimeStep(Mathf.Max(0f, deltaTime), _time + Mathf.Max(0f, deltaTime), Time.frameCount));
            TextStudio2DHeroSubmission.Flush();
        }

        /// <summary>Performs one synchronous presentation update using a host-supplied deterministic time step.</summary>
        public void ForceMeshRefresh(in TimeStep step)
        {
            ScheduleMeshRefresh();
            Tick(step);
            // A synchronous refresh completes its offscreen composition before returning.
            TextStudio2DHeroSubmission.Flush();
        }

        /// <summary>Marks component-provided placement output for recomputation.</summary>
        public void SchedulePlacementRefresh()
        {
            _runtime.SetDirty(DirtyFlags.Placement | DirtyFlags.Visual);
        }

        /// <summary>Selects an attached placement extension by its stable identifier.</summary>
        public bool SelectPlacementExtension(string placementId)
        {
            ITextStudioPlacementExtension extension = FindPlacementExtension(placementId);
            if (extension == null)
                return false;

            _extensionPlacementId = extension.PlacementId;
            _placementMode = PlacementMode.Extension;
            ApplyPlacementStrategy();
            SchedulePlacementRefresh();
            return true;
        }

        /// <summary>Selects source-linear placement without changing the current orientation policy.</summary>
        public void ConfigureLinearPlacement()
        {
            PlacementMode = PlacementMode.Linear;
            SchedulePlacementRefresh();
        }

        /// <summary>Bends selected text along an independent inline baseline profile.</summary>
        public void ConfigureCurvePlacement(in TextStudioCurveDefinition definition, float height = 0.5f,
            TextStudioApplicationSelector selection = default,
            TextStudioCurveMapping mapping = TextStudioCurveMapping.EachMatch)
        {
            _curvePlacement ??= new CurvePlacement();
            _curvePlacement.ConfigureInline(in definition, height, in selection, mapping);
            SyncCurveProfileSubscription();
            PlacementMode = PlacementMode.Curve;
            SchedulePlacementRefresh();
        }

        /// <summary>Bends selected text along a shared curve profile.</summary>
        public void ConfigureCurvePlacement(TextStudioCurveProfile profile, float height = 0.5f,
            TextStudioApplicationSelector selection = default,
            TextStudioCurveMapping mapping = TextStudioCurveMapping.EachMatch)
        {
            _curvePlacement ??= new CurvePlacement();
            _curvePlacement.ConfigureProfile(profile, height, in selection, mapping);
            SyncCurveProfileSubscription();
            PlacementMode = PlacementMode.Curve;
            SchedulePlacementRefresh();
        }

        /// <summary>Copies the linked curve into independent inline settings.</summary>
        public void MakeCurvePlacementLocal()
        {
            _curvePlacement ??= new CurvePlacement();
            _curvePlacement.Curve.MakeLocal();
            SyncCurveProfileSubscription();
            SchedulePlacementRefresh();
        }

        internal int CurveGuideCount => _placementMode == PlacementMode.Curve
            ? _curvePlacement?.GuideDomainCount ?? 0 : 0;
        internal int GetCurveGuideOwner(int unit) => _placementMode == PlacementMode.Curve
            ? _curvePlacement?.GetGuideOwner(unit) ?? -1 : -1;

        internal bool TryGetCurvePlacementGuide(int owner, int lineIndex,
            out TextStudioCurvePlacementGuide guide)
        {
            guide = default;
            if (_placementMode != PlacementMode.Curve || _curvePlacement == null ||
                !_curvePlacement.TryGetDomain(owner, out float domainMin, out float domainMax) ||
                _layout.BasePositions == null || _layout.VisualGlyphs == null)
                return false;
            float segmentMin = float.PositiveInfinity, segmentMax = float.NegativeInfinity;
            float baseline = 0f, depth = 0f;
            int selectedLine = lineIndex;
            int count = Mathf.Min(_layout.GlyphCount,
                Mathf.Min(_layout.BasePositions.Length, _layout.VisualGlyphs.Length));
            for (int unit = 0; unit < count; unit++)
            {
                if (!_layout.HasSourceLayout(unit) || _curvePlacement.GetGuideOwner(unit) != owner) continue;
                TextStudioVisualGlyph glyph = _layout.VisualGlyphs[unit];
                if (selectedLine < 0) selectedLine = glyph.LineIndex;
                if (glyph.LineIndex != selectedLine) continue;
                Vector3 center = _layout.BasePositions[unit];
                if (!float.IsFinite(segmentMin))
                {
                    float offset = _layout.HasSourceCenterBaselineOffsets &&
                                   unit < _layout.SourceCenterBaselineOffsets.Length
                        ? _layout.SourceCenterBaselineOffsets[unit] : 0f;
                    baseline = center.y - offset;
                    depth = center.z;
                }
                segmentMin = Mathf.Min(segmentMin, center.x);
                segmentMax = Mathf.Max(segmentMax, center.x);
                if (_layout.SourceVisualBounds != null && unit < _layout.SourceVisualBounds.Length)
                {
                    TextStudioGlyphVisualBounds bounds = _layout.SourceVisualBounds[unit];
                    if (bounds.IsValid)
                    {
                        segmentMin = Mathf.Min(segmentMin, bounds.Minimum.x);
                        segmentMax = Mathf.Max(segmentMax, bounds.Maximum.x);
                    }
                }
            }
            if (!float.IsFinite(segmentMin) || !float.IsFinite(segmentMax)) return false;
            guide = new TextStudioCurvePlacementGuide(_curvePlacement.Curve, _curvePlacement.Height,
                domainMin, domainMax, segmentMin, segmentMax, baseline, depth);
            return true;
        }

        /// <summary>Configures planar arc placement in local units and degrees without changing orientation.</summary>
        public void ConfigureArcPlacement(
            float radius,
            float arcAngle,
            float startAngle = 0f,
            PathSpacing spacing = PathSpacing.Even,
            float fill = 1f,
            bool reverse = false,
            float radiusChange = 0f,
            PathPosition pathPosition = PathPosition.Above)
        {
            _arcPlacement.radius = radius;
            _arcPlacement.arcAngle = Mathf.Clamp(arcAngle, 1f, 360f);
            _arcPlacement.startAngle = startAngle;
            _arcPlacement.spacing = spacing;
            _arcPlacement.fill = Mathf.Clamp(fill, 0.1f, 1f);
            _arcPlacement.reverse = reverse;
            _arcPlacement.radiusChange = radiusChange;
            _arcPlacement.pathPosition = pathPosition;
            PlacementMode = PlacementMode.Arc;
            SchedulePlacementRefresh();
        }

        /// <summary>Configures animated planar wave placement in local units and cycles per second.</summary>
        public void ConfigureWavePlacement(
            float amplitude,
            float frequency,
            float speed,
            float phase = 0f)
        {
            ConfigureSpatialWavePlacement(amplitude, 0f, frequency, speed, 0f, 0f, phase);
        }

        /// <summary>Configures vertical/depth wave placement and glyph roll using the host playback clock. Spin values are revolutions per second and revolutions across the text.</summary>
        public void ConfigureSpatialWavePlacement(
            float amplitude,
            float depthAmplitude,
            float frequency,
            float speed,
            float spinSpeed = 0f,
            float spinStagger = 0f,
            float phase = 0f)
        {
            _wavePlacement.amplitude = amplitude;
            _wavePlacement.frequency = frequency;
            _wavePlacement.phase = phase;
            _wavePlacement.speed = speed;
            _wavePlacement.depthAmplitude = depthAmplitude;
            _wavePlacement.spinSpeed = spinSpeed;
            _wavePlacement.spinStagger = spinStagger;
            PlacementMode = PlacementMode.Wave;
            SchedulePlacementRefresh();
        }

        /// <summary>Configures circular placement in local units and degrees without changing orientation.</summary>
        public void ConfigureCirclePlacement(
            float radius,
            float startAngle = 0f,
            PathSpacing spacing = PathSpacing.Even,
            bool reverse = false,
            float radiusChange = 0f,
            PathPosition pathPosition = PathPosition.Above)
        {
            _circlePlacement.radius = radius;
            _circlePlacement.startAngle = startAngle;
            _circlePlacement.spacing = spacing;
            _circlePlacement.reverse = reverse;
            _circlePlacement.radiusChange = radiusChange;
            _circlePlacement.pathPosition = pathPosition;
            PlacementMode = PlacementMode.Circle;
            SchedulePlacementRefresh();
        }

        /// <summary>Configures row- or column-major grid placement with explicit or automatic cell sizing.</summary>
        public void ConfigureGridPlacement(
            int columns,
            GridCellSizing sizing = GridCellSizing.Auto,
            Vector2 cellSize = default,
            Vector2 gap = default,
            GridFlow flow = GridFlow.RowsFirst,
            bool reverse = false,
            bool respectSourceLines = false)
        {
            _gridPlacement.columns = Mathf.Max(1, columns);
            _gridPlacement.cellSizing = sizing;
            _gridPlacement.cellSize = cellSize == default
                ? new Vector2(0.6f, 1f)
                : cellSize;
            _gridPlacement.gap = gap;
            _gridPlacement.flow = flow;
            _gridPlacement.reverse = reverse;
            _gridPlacement.respectSourceLines = respectSourceLines;
            PlacementMode = PlacementMode.Grid;
            SchedulePlacementRefresh();
        }

        /// <summary>Changes the arc's start angle in degrees and refreshes placement.</summary>
        public void SetArcStartAngle(float startAngle)
        {
            _arcPlacement.startAngle = startAngle;
            _runtime.SetDirty(DirtyFlags.Placement);
        }

        /// <summary>Changes the circle's start angle in degrees and refreshes placement.</summary>
        public void SetCircleStartAngle(float startAngle)
        {
            _circlePlacement.startAngle = startAngle;
            _runtime.SetDirty(DirtyFlags.Placement);
        }

        /// <summary>Sets glyph orientation along the selected placement plus an optional local Euler rotation in degrees.</summary>
        public void SetPlacementOrientation(
            TextStudioPlacementOrientation orientation,
            Vector3 fineRotation = default)
        {
            _placementRotation.orientation = orientation;
            _placementRotation.fineRotation = fineRotation;
            _runtime.SetDirty(DirtyFlags.Placement);
        }

    }
}
