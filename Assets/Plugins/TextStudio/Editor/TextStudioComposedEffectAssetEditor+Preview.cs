using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioComposedEffectAssetEditor
    {
        VisualElement CreatePreview()
        {
            VisualElement section = CreateSection("Live Preview");
            section.name = "ts-composed-preview";
            section.AddToClassList("ts-preview-section");
            section.Add(TextStudioPreviewExportWindow.CreateMenuButton(
                "ts-composed-preview-export-menu",
                (TextStudioComposedEffectAsset)target,
                TryCreateExportSnapshot));

            _previewContext = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-composed-preview-context");
            _previewContext.tooltip =
                "Shows the production preview's authoring space, lifecycle phase, Solo context, and highest active layer cost class.";
            _previewContext.AddToClassList("ts-composed-preview-context");
            section.Add(_previewContext);

            VisualElement phaseRow = new VisualElement();
            phaseRow.AddToClassList("ts-preview-segment-row");
            section.Add(phaseRow);
            _phaseButtons.Clear();
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Reveal);
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Active);
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Hide);

            VisualElement scrubRow = new VisualElement
            {
                name = "ts-composed-preview-scrub-row"
            };
            scrubRow.AddToClassList("ts-composed-preview-scrub-row");
            section.Add(scrubRow);

            _previewScrub = new Slider("Scrub", 0f, 1f)
            {
                name = "ts-composed-preview-scrub",
                value = GetPreviewProgress(
                    _previewTime,
                    _previewCycleDuration,
                    _previewRepeat)
            };
            _previewScrub.AddToClassList("ts-scrub-slider");
            _previewScrub.RegisterValueChangedCallback(evt => ScrubPreviewAt(evt.newValue));
            _previewScrub.RegisterCallback<PointerDownEvent>(_ => PausePreviewPlayback());
            scrubRow.Add(_previewScrub);
            _activePreviewDurationButton = new Button(CycleActivePreviewDurationMultiplier)
            {
                name = "ts-composed-preview-active-duration"
            };
            _activePreviewDurationButton.AddToClassList("ts-composed-active-duration-button");
            scrubRow.Add(_activePreviewDurationButton);
            RefreshActivePreviewDurationButton();

            VisualElement speedRow = new VisualElement();
            speedRow.AddToClassList("ts-button-row");
            section.Add(speedRow);
            _previewPlaybackButton = new Button(TogglePreviewPlayback)
            {
                name = "ts-composed-preview-playback"
            };
            _previewPlaybackButton.AddToClassList("ts-preview-button");
            speedRow.Add(_previewPlaybackButton);
            _speedButtons.Clear();
            for (int i = 0; i < TextStudioEffectPreviewUtility.SpeedPresetCount; i++)
            {
                AddSpeedButton(
                    speedRow,
                    TextStudioEffectPreviewUtility.GetSpeedLabel(i),
                    TextStudioEffectPreviewUtility.GetSpeed(i));
            }
            _previewRepeatButton = new Button(TogglePreviewRepeat)
            {
                name = "ts-composed-preview-repeat"
            };
            _previewRepeatButton.AddToClassList("ts-preview-button");
            _previewRepeatButton.AddToClassList("ts-preview-repeat-button");
            speedRow.Add(_previewRepeatButton);
            RefreshPreviewRepeatButton();
            RefreshPreviewPlaybackButton();

            _previewCanvas = new VisualElement
            {
                name = "ts-composed-preview-canvas"
            };
            _previewCanvas.AddToClassList("ts-effect-preview-canvas");
            section.Add(_previewCanvas);
            _rendererPreviewImage = new Image
            {
                name = "ts-composed-renderer-preview",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _rendererPreviewImage.AddToClassList("ts-composed-renderer-preview");
            _previewCanvas.Add(_rendererPreviewImage);
            _rendererPreviewStatus = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-composed-renderer-preview-status");
            _previewCanvas.Add(_rendererPreviewStatus);
            _previewGlyphCanvas = new VisualElement
            {
                name = "ts-composed-preview-glyph-fallback"
            };
            _previewGlyphCanvas.AddToClassList("ts-composed-preview-glyph-fallback");
            _previewCanvas.Add(_previewGlyphCanvas);
            CreateSpatialPreviewElements(section);
            _previewSourceColorStatus = TextStudioInspectorElements.CreateMutedText(
                "Preview colors are intentionally varied so Color Selection is easy to tune. Runtime selection uses each renderer's original glyph colors.",
                "ts-composed-preview-source-color-status");
            _previewSourceColorStatus.style.display = DisplayStyle.None;
            section.Add(_previewSourceColorStatus);
            RebuildPreviewGlyphs();
            section.Add(CreateInspirationAction());

            RefreshPhaseButtons();
            return section;
        }

        void RenderPreview(
            bool evaluateGlyphFallback = false,
            bool refreshInspectorState = true)
        {
            if (refreshInspectorState)
            {
                EnsureLayerTimingRowsCurrent();
                RefreshLayerTimingScrub();
            }
            if (_previewCanvas == null || target is not TextStudioComposedEffectAsset effect)
                return;
            InvalidatePreviewPlanIfTargetChanged();
            if (!effect.TryCompileForValidation(out string validationError))
            {
                DisposeLivePreview();
                HideSpatialPreviewForUnavailablePlan();
                ShowRendererPreviewStatus(validationError);
                return;
            }

            if (string.IsNullOrWhiteSpace(effect.Id))
            {
                HideSpatialPreviewForUnavailablePlan();
                return;
            }
            string previewId = effect.Id.Trim();
            bool usesSourceColorSelection = HasSourceColorSelection(effect);
            string sampleMarkup = usesSourceColorSelection
                ? BuildVariedSourceColorMarkup(PreviewSampleText)
                : PreviewSampleText;
            string markup = "<" + previewId + ">" + sampleMarkup + "</" + previewId + ">";
            if (_previewSourceColorStatus != null)
            {
                _previewSourceColorStatus.style.display = usesSourceColorSelection
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            _previewSource.SetText(markup);
            _previewPipeline.Process(_previewSource, effect, SoloLayerStableId);
            TextStudioCompiledPlan plan = _previewPipeline.Plan;
            if (plan == null || plan.VisibleGlyphCount == 0)
            {
                HideSpatialPreviewForUnavailablePlan();
                return;
            }
            RefreshSpatialPreviewDescriptors(plan, effect);

            float revealDuration = GetPreviewCycleDuration(plan, AnimationPhase.Appearance);
            float activeDuration = GetPreviewCycleDuration(plan, AnimationPhase.Persistent);
            float hideDuration = GetPreviewCycleDuration(plan, AnimationPhase.Disappearance);
            UpdatePreviewCycleDuration(revealDuration, activeDuration, hideDuration);
            _previewRevealDuration = revealDuration;
            _previewActiveDuration = activeDuration;
            _previewHideDuration = hideDuration;

            if (_previewGlyphs.Count == 0)
                RebuildPreviewGlyphs();

            float lifecycleTime = !_isPreviewPlaying && _previewScrub != null
                ? Mathf.Clamp01(_previewScrub.value) * _previewCycleDuration
                : GetPreviewLifecycleTime(
                    _previewTime,
                    _previewCycleDuration,
                    _previewRepeat);
            float continuousTime = Mathf.Max(0f, _previewTime);

            TextStudioEffectPreviewUtility.ResolveFrame(
                lifecycleTime,
                _previewSegments,
                revealDuration,
                activeDuration,
                hideDuration,
                _activePreviewDurationMultiplier,
                out _previewPhase,
                out float progress,
                out float phaseTime,
                out _);
            RefreshPreviewContext(effect);
            _layerTimingPhaseTime = Mathf.Max(0f, phaseTime);
            RefreshSpatialPreviewPresentation();
            if (refreshInspectorState)
            {
                RefreshLayerTimingScrub();
                RefreshLayerTimingPreviewState();
            }

            bool hasLivePreview = RenderLivePreview(effect, continuousTime, lifecycleTime);
            if (effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD)
                return;
            if (hasLivePreview && !evaluateGlyphFallback)
                return;
            if (!hasLivePreview)
            {
                if (_rendererPreviewImage != null)
                    _rendererPreviewImage.style.display = DisplayStyle.None;
                if (_rendererPreviewStatus != null)
                    _rendererPreviewStatus.style.display = DisplayStyle.None;
                if (_previewGlyphCanvas != null)
                    _previewGlyphCanvas.style.display = DisplayStyle.Flex;
            }

            RevealController reveal = _previewReveal;
            reveal.Mode = RevealMode.ByGlyph;
            reveal.AppearanceDuration = revealDuration;
            reveal.DisappearanceDuration = hideDuration;
            reveal.Initialize(plan);
            reveal.SetRevealActiveBlendEnabledForPreview(
                (_previewSegments & TextStudioPreviewSegment.Reveal) != 0);
            if (_previewPhase == AnimationPhase.Appearance)
            {
                reveal.StartReveal();
                float revealScrubDuration = reveal.GetApproximateVisualRevealDuration();
                reveal.SetScrubElapsedTime(progress * revealScrubDuration);
            }
            else if (_previewPhase == AnimationPhase.Disappearance)
            {
                float activeElapsedBeforeHide =
                    (_previewSegments & TextStudioPreviewSegment.Active) != 0
                        ? activeDuration * Mathf.Max(1, _activePreviewDurationMultiplier)
                        : 0f;
                reveal.SetActiveElapsedTimeForPreview(
                    activeElapsedBeforeHide + Mathf.Max(0f, phaseTime));
                reveal.StartDisappear();
                reveal.SetDisappearanceProgressForPreview(progress);
            }
            else
            {
                reveal.SetActiveElapsedTimeForPreview(Mathf.Max(0f, phaseTime));
            }

            _previewEvaluator.Initialize(plan.VisibleGlyphCount);
            _previewEvaluator.BindEffects(plan);
            ConfigurePreviewSourceContext(plan, usesSourceColorSelection);
            float playbackTime = lifecycleTime;
            if (_previewPhase == AnimationPhase.Persistent)
            {
                _previewEvaluator.EvaluateWithPersistentPhaseProgress(
                    continuousTime,
                    playbackTime,
                    reveal,
                    plan,
                    progress);
            }
            else
            {
                _previewEvaluator.EvaluateWithPlaybackTime(
                    continuousTime,
                    playbackTime,
                    reveal,
                    plan);
            }
            EvaluatedGlyphState[] states = _previewEvaluator.States;
            for (int i = 0; i < _previewGlyphs.Count; i++)
            {
                PreviewGlyph glyph = _previewGlyphs[i];
                EvaluatedGlyphState state = glyph.GlyphIndex >= 0 && glyph.GlyphIndex < plan.VisibleGlyphCount
                    ? states[glyph.GlyphIndex]
                    : EvaluatedGlyphState.Identity;
                Color32 sourceColor = usesSourceColorSelection &&
                                      glyph.GlyphIndex >= 0 &&
                                      glyph.GlyphIndex < _previewSourceColors.Length
                    ? _previewSourceColors[glyph.GlyphIndex].Sample(0.5f, 0.5f)
                    : new Color32(238, 241, 248, 255);
                TextStudioEffectPreviewUtility.ApplyState(glyph.Label, state, sourceColor);
            }

        }

        void RefreshPreviewContext(TextStudioComposedEffectAsset effect)
        {
            if (_previewContext == null || effect == null)
                return;
            int dirtyCount = EditorUtility.GetDirtyCount(effect);
            if (_previewContextDirtyCount == dirtyCount &&
                _previewContextPhase == _previewPhase &&
                string.Equals(
                    _previewContextSoloLayerStableId,
                    SoloLayerStableId,
                    StringComparison.Ordinal))
            {
                return;
            }

            _previewContextDirtyCount = dirtyCount;
            _previewContextPhase = _previewPhase;
            _previewContextSoloLayerStableId = SoloLayerStableId;
            _previewContext.text =
                TextStudioComposedLayerPresentation.BuildPreviewContext(
                    effect,
                    _previewPhase,
                    SoloLayerStableId);
        }

        static bool HasSourceColorSelection(TextStudioComposedEffectAsset effect)
        {
            if (effect == null)
                return false;
            IReadOnlyList<TextStudioCompositionLayer> layers = effect.Layers;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer != null && layer.Enabled && layer.SourceColorSelector.Enabled)
                    return true;
            }
            return false;
        }

        static string BuildVariedSourceColorMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length * 24);
            int paletteIndex = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '\n')
                {
                    builder.Append(character);
                    continue;
                }
                Color32 color = GetPreviewSourceColor(paletteIndex++);
                builder.Append("<color=#");
                builder.Append(ColorUtility.ToHtmlStringRGBA(color));
                builder.Append('>');
                builder.Append(character);
                builder.Append("</color>");
            }
            return builder.ToString();
        }

        void ConfigurePreviewSourceContext(
            TextStudioCompiledPlan plan,
            bool usesSourceColorSelection)
        {
            int count = plan.VisibleGlyphCount;
            if (_previewSourcePositions == null || _previewSourcePositions.Length < count)
            {
                int capacity = TextStudioBufferCapacity.ForCount(count);
                _previewSourcePositions = new Vector3[capacity];
                _previewSourceVisualBounds = new TextStudioGlyphVisualBounds[capacity];
                _previewNormalizedPositions = new Vector3[capacity];
                _previewSourceColors = new TextStudioGlyphColorQuad[capacity];
                _previewSourceColorValidity = new bool[capacity];
            }

            int lineCount = Mathf.Max(1, plan.LineCount);
            for (int i = 0; i < count; i++)
            {
                int lineIndex = plan.GlyphToLineIndex != null && i < plan.GlyphToLineIndex.Length
                    ? Mathf.Max(0, plan.GlyphToLineIndex[i])
                    : 0;
                float x = count <= 1 ? 0.5f : i / (float)(count - 1);
                float y = lineCount <= 1 ? 0.5f : 1f - lineIndex / (float)(lineCount - 1);
                _previewSourcePositions[i] = new Vector3(i, -lineIndex, 0f);
                _previewSourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    new Vector2(i - 0.45f, -lineIndex - 0.5f),
                    new Vector2(i + 0.45f, -lineIndex + 0.5f),
                    true);
                _previewNormalizedPositions[i] = new Vector3(x, y, 0f);
                Color32 color = usesSourceColorSelection
                    ? GetPreviewSourceColor(i)
                    : new Color32(238, 241, 248, 255);
                _previewSourceColors[i] = TextStudioGlyphColorQuad.Uniform(color);
                _previewSourceColorValidity[i] = true;
            }

            _previewEvaluator.SetSpatialContext(
                _previewSourcePositions,
                _previewNormalizedPositions,
                _previewSourceColors,
                _previewSourceColorValidity,
                count,
                null,
                0,
                PlacementMode.Linear,
                Matrix4x4.identity,
                false,
                Vector3.zero,
                Vector3.zero,
                default,
                _previewSourceVisualBounds,
                unchecked((int)plan.SourceVersion));
        }

        static Color32 GetPreviewSourceColor(int index)
        {
            return (index % 8) switch
            {
                0 => new Color32(246, 82, 82, 255),
                1 => new Color32(245, 164, 70, 220),
                2 => new Color32(232, 221, 88, 190),
                3 => new Color32(72, 202, 130, 160),
                4 => new Color32(73, 185, 224, 130),
                5 => new Color32(91, 112, 231, 100),
                6 => new Color32(184, 91, 220, 75),
                _ => new Color32(224, 224, 224, 45)
            };
        }

        internal void RenderPreviewAt(float time)
        {
            _previewTime = Mathf.Max(0f, time);
            _previewScrub?.SetValueWithoutNotify(GetPreviewProgress(
                _previewTime,
                _previewCycleDuration,
                _previewRepeat));
            RenderPreview(evaluateGlyphFallback: true);
        }

        internal void ScrubPreviewAt(float normalizedProgress)
        {
            float progress = Mathf.Clamp01(normalizedProgress);
            PausePreviewPlayback();
            _previewTime = _previewRepeat
                ? GetPreviewTimeFromScrub(
                    _previewTime,
                    progress,
                    _previewCycleDuration)
                : progress * _previewCycleDuration;
            _previewScrub?.SetValueWithoutNotify(progress);
            RenderPreview();
        }

        internal bool IsPreviewPlaying => _isPreviewPlaying;

        internal void TogglePreviewPlayback()
        {
            if (!_isPreviewPlaying &&
                !_previewRepeat &&
                !ShouldHoldActivePreview(_previewSegments) &&
                _previewTime >= _previewCycleDuration - 0.0001f)
            {
                _previewTime = 0f;
                _previewScrub?.SetValueWithoutNotify(0f);
            }
            _isPreviewPlaying = !_isPreviewPlaying;
            _lastPreviewTick = EditorApplication.timeSinceStartup;
            RefreshPreviewPlaybackButton();
            Repaint();
        }

        internal void RenderPreviewAt(float time, AnimationPhase phase)
        {
            _previewSegments = GetPreviewSegment(phase);
            RefreshPhaseButtons();
            RenderPreview();
            RenderPreviewAt(time);
        }

        void RebuildPreviewGlyphs()
        {
            if (_previewGlyphCanvas == null)
                return;
            _previewGlyphCanvas.Clear();
            _previewGlyphs.Clear();
            _previewGlyphMap.Clear();
            TextStudioEffectPreviewUtility.BuildGlyphIndexMap(PreviewSampleText, _previewGlyphMap);
            int mapIndex = 0;
            string[] lines = PreviewSampleText.Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                VisualElement line = new VisualElement();
                line.AddToClassList("ts-effect-preview-line");
                _previewGlyphCanvas.Add(line);
                string text = lines[lineIndex];
                for (int i = 0; i < text.Length; i++)
                {
                    Label label = TextStudioEffectPreviewUtility.CreateGlyph(text[i].ToString());
                    label.AddToClassList("ts-effect-preview-glyph");
                    line.Add(label);
                    _previewGlyphs.Add(new PreviewGlyph
                    {
                        Label = label,
                        GlyphIndex = mapIndex < _previewGlyphMap.Count ? _previewGlyphMap[mapIndex] : -1
                    });
                    mapIndex++;
                }
            }
        }

        bool RenderLivePreview(
            TextStudioComposedEffectAsset effect,
            float continuousTime,
            float lifecycleTime)
        {
            string rendererId = effect.AuthoringSpace == TextStudioCompositionAuthoringSpace.ThreeD
                ? GetThreeDPreviewRendererId()
                : TextStudioPreviewExportRendererRegistry.FlatRendererId;
            if (string.IsNullOrEmpty(rendererId))
            {
                DisposeLivePreview();
                ShowRendererPreviewStatus(
                    TextStudioAuthoringCapabilities.Supports(TextStudioRenderDomain.Extruded3D)
                        ? "The 3D preview renderer is unavailable. Assign a default TextMeshPro font asset, then reopen this inspector."
                        : "Install the 3D add-on to preview this effect with real extruded text.");
                return false;
            }

            int dirtyCount = EditorUtility.GetDirtyCount(effect);
            if (_livePreviewRenderer == null ||
                _livePreviewSnapshot == null ||
                _livePreviewDirtyCount != dirtyCount ||
                _livePreviewSegments != _previewSegments ||
                _livePreviewActiveDurationMultiplier != _activePreviewDurationMultiplier ||
                !string.Equals(
                    _livePreviewSoloLayerStableId,
                    SoloLayerStableId,
                    StringComparison.Ordinal) ||
                !string.Equals(_livePreviewRendererId, rendererId, StringComparison.Ordinal))
            {
                DisposeLivePreview();
                if (!TryCreateExportSnapshot(out _livePreviewSnapshot, out string error))
                {
                    ShowRendererPreviewStatus(error);
                    return false;
                }

                try
                {
                    _livePreviewRenderer = TextStudioPreviewExportRendererRegistry.Create(
                        rendererId,
                        _livePreviewSnapshot,
                        720,
                        260,
                        false);
                    _livePreviewDirtyCount = dirtyCount;
                    _livePreviewRendererId = rendererId;
                    _livePreviewSegments = _previewSegments;
                    _livePreviewActiveDurationMultiplier = _activePreviewDurationMultiplier;
                    _livePreviewSoloLayerStableId = SoloLayerStableId;
                }
                catch (Exception exception)
                {
                    DisposeLivePreview();
                    ShowRendererPreviewStatus(
                        "The renderer-backed preview is unavailable: " + exception.Message);
                    return false;
                }
            }

            ApplySpatialPreviewTargetToRenderer();
            _livePreviewRenderer.RenderToTarget(continuousTime, lifecycleTime);
            SetRendererPreviewCanvas(effect.AuthoringSpace);
            if (_rendererPreviewImage != null)
            {
                _rendererPreviewImage.image = _livePreviewRenderer.TargetTexture;
                _rendererPreviewImage.style.display = DisplayStyle.Flex;
            }
            if (_rendererPreviewStatus != null)
                _rendererPreviewStatus.style.display = DisplayStyle.None;
            if (_previewGlyphCanvas != null)
                _previewGlyphCanvas.style.display = DisplayStyle.None;
            return true;
        }

        void ShowRendererPreviewStatus(string message)
        {
            SetSpatialRendererAvailability(false);
            SetClass(_previewCanvas, "ts-composed-renderer-canvas", false);
            SetClass(_previewCanvas, "ts-composed-renderer-canvas--3d", false);
            if (_rendererPreviewImage != null)
                _rendererPreviewImage.style.display = DisplayStyle.None;
            if (_previewGlyphCanvas != null)
                _previewGlyphCanvas.style.display = DisplayStyle.None;
            if (_rendererPreviewStatus != null)
            {
                _rendererPreviewStatus.text = string.IsNullOrWhiteSpace(message)
                    ? "Preview unavailable."
                    : message;
                _rendererPreviewStatus.style.display = DisplayStyle.Flex;
            }
        }

        void DisposeLivePreview()
        {
            if (_rendererPreviewImage != null)
                _rendererPreviewImage.image = null;
            _livePreviewRenderer?.Dispose();
            _livePreviewRenderer = null;
            _livePreviewSnapshot?.Dispose();
            _livePreviewSnapshot = null;
            _livePreviewDirtyCount = int.MinValue;
            _livePreviewRendererId = string.Empty;
            _livePreviewSegments = TextStudioPreviewSegment.None;
            _livePreviewActiveDurationMultiplier = 0;
            _livePreviewSoloLayerStableId = string.Empty;
        }

        void SetRendererPreviewCanvas(TextStudioCompositionAuthoringSpace authoringSpace)
        {
            SetClass(_previewCanvas, "ts-composed-renderer-canvas", true);
            SetClass(
                _previewCanvas,
                "ts-composed-renderer-canvas--3d",
                authoringSpace == TextStudioCompositionAuthoringSpace.ThreeD);
        }

        static string GetThreeDPreviewRendererId()
        {
            return TextStudioPreviewExportRendererRegistry.GetFirstExtensionRendererId();
        }

        void InvalidatePreviewPlanIfTargetChanged()
        {
            UnityEngine.Object currentTarget = target;
            int dirtyCount = currentTarget != null ? EditorUtility.GetDirtyCount(currentTarget) : 0;
            if (_previewDirtyTarget == currentTarget && _previewDirtyCount == dirtyCount)
                return;

            _previewDirtyTarget = currentTarget;
            _previewDirtyCount = dirtyCount;
            _previewPipeline.Invalidate();
            InvalidateSpatialPreviewDescriptors();
        }

        void InvalidatePreviewPlan()
        {
            _previewPipeline.Invalidate();
            InvalidateSpatialPreviewDescriptors();
            _previewDirtyTarget = target;
            _previewDirtyCount = target != null ? EditorUtility.GetDirtyCount(target) : 0;
        }

        void AddPhaseButton(VisualElement parent, TextStudioPreviewSegment segment)
        {
            string label = TextStudioTextEditor.GetPreviewSegmentLabel(segment);
            Button button = TextStudioTextEditor.CreatePreviewSegmentButton(
                label,
                "Include the " + label.ToLowerInvariant() + " phase in preview playback.",
                () => SetPreviewSegmentEnabled(
                    segment,
                    (_previewSegments & segment) == 0));
            button.name = "ts-composed-preview-segment-" +
                label.ToLowerInvariant();
            button.userData = segment;
            _phaseButtons.Add(button);
            parent.Add(button);
        }

        void RefreshPhaseButtons()
        {
            for (int i = 0; i < _phaseButtons.Count; i++)
            {
                Button button = _phaseButtons[i];
                if (button.userData is not TextStudioPreviewSegment segment)
                    continue;

                TextStudioTextEditor.SyncPreviewSegmentButton(
                    button,
                    segment,
                    (_previewSegments & segment) != 0);
            }

            RefreshActivePreviewDurationButton();
        }

        internal void SetPreviewSegmentEnabled(
            TextStudioPreviewSegment segment,
            bool enabled)
        {
            TextStudioPreviewSegment nextSegments =
                TextStudioEffectPreviewUtility.SetSegmentEnabled(
                    _previewSegments,
                    segment,
                    enabled);
            if (nextSegments == _previewSegments)
                return;

            _previewSegments = nextSegments;
            TextStudioComposedEffectPreviewPreferences.Segments = _previewSegments;
            _previewTime = 0f;
            _previewScrub?.SetValueWithoutNotify(0f);
            _isPreviewPlaying = true;
            _lastPreviewTick = EditorApplication.timeSinceStartup;
            RefreshPhaseButtons();
            RefreshPreviewPlaybackButton();
            RenderPreview();
        }

        void CycleActivePreviewDurationMultiplier()
        {
            int nextMultiplier = _activePreviewDurationMultiplier >= MaximumActivePreviewDurationMultiplier
                ? MinimumActivePreviewDurationMultiplier
                : _activePreviewDurationMultiplier * 2;
            SetActivePreviewDurationMultiplier(nextMultiplier);
        }

        internal void SetActivePreviewDurationMultiplier(int multiplier)
        {
            int normalizedMultiplier = NormalizeActivePreviewDurationMultiplier(multiplier);
            if (normalizedMultiplier == _activePreviewDurationMultiplier)
                return;

            float previousCycleDuration = Mathf.Max(0.0001f, _previewCycleDuration);
            float previousProgress = !_isPreviewPlaying && _previewScrub != null
                ? Mathf.Clamp01(_previewScrub.value)
                : GetPreviewProgress(_previewTime, previousCycleDuration, _previewRepeat);
            float previousLifecycleTime = previousProgress * previousCycleDuration;
            TextStudioEffectPreviewUtility.ResolveFrame(
                previousLifecycleTime,
                _previewSegments,
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier,
                out AnimationPhase phase,
                out _,
                out float phaseTime,
                out _);

            float cycle = _previewRepeat
                ? Mathf.Floor(Mathf.Max(0f, _previewTime) / previousCycleDuration)
                : 0f;
            if (_previewRepeat && !_isPreviewPlaying && previousProgress >= 0.999999f && cycle > 0f)
                cycle -= 1f;
            float continuousOverrun = _previewRepeat
                ? 0f
                : Mathf.Max(0f, _previewTime - previousCycleDuration);

            _activePreviewDurationMultiplier = normalizedMultiplier;
            _previewCycleDuration = TextStudioEffectPreviewUtility.GetCycleDuration(
                _previewSegments,
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier);
            float remappedLifecycleTime = GetLifecycleTimeForPhase(
                phase,
                phaseTime,
                _previewSegments,
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier);
            _previewTime = cycle * _previewCycleDuration + remappedLifecycleTime + continuousOverrun;
            _previewScrub?.SetValueWithoutNotify(
                Mathf.Clamp01(remappedLifecycleTime / _previewCycleDuration));
            RefreshActivePreviewDurationButton();
            RenderPreview();
        }

        void RefreshActivePreviewDurationButton()
        {
            if (_activePreviewDurationButton == null)
                return;

            _activePreviewDurationButton.text = "Active ×" + _activePreviewDurationMultiplier;
            _activePreviewDurationButton.tooltip =
                "Extend the Active phase in this preview without restarting its effects. " +
                "This does not change the Composed Effect or runtime playback.";
            _activePreviewDurationButton.SetEnabled(
                (_previewSegments & TextStudioPreviewSegment.Active) != 0);
        }

        void AddSpeedButton(VisualElement parent, string label, float speed)
        {
            Button button = new Button(() =>
            {
                _previewSpeed = speed;
                if (!_isPreviewPlaying)
                    TogglePreviewPlayback();
                else
                {
                    _lastPreviewTick = EditorApplication.timeSinceStartup;
                    Repaint();
                }
                RefreshSpeedButtons();
            })
            {
                name = "ts-composed-preview-speed-" +
                       label.Replace(".", "-").ToLowerInvariant(),
                text = label,
                tooltip = "Set preview playback to " + label +
                          " and resume from the current position without changing authored timing."
            };
            button.AddToClassList("ts-preview-button");
            button.userData = speed;
            SetClass(button, "ts-authoring-mode-active", Mathf.Approximately(_previewSpeed, speed));
            _speedButtons.Add(button);
            parent.Add(button);
        }

        void RefreshSpeedButtons()
        {
            for (int i = 0; i < _speedButtons.Count; i++)
            {
                Button button = _speedButtons[i];
                SetClass(
                    button,
                    "ts-authoring-mode-active",
                    button.userData is float speed && Mathf.Approximately(_previewSpeed, speed));
            }
        }

        void TogglePreviewRepeat()
        {
            SetPreviewRepeat(!_previewRepeat);
        }

        internal void SetPreviewRepeat(bool repeat)
        {
            if (_previewRepeat == repeat)
                return;

            if (!repeat)
            {
                _previewTime = GetPreviewLifecycleTime(
                    _previewTime,
                    _previewCycleDuration,
                    true);
            }

            _previewRepeat = repeat;
            _lastPreviewTick = EditorApplication.timeSinceStartup;
            _previewScrub?.SetValueWithoutNotify(GetPreviewProgress(
                _previewTime,
                _previewCycleDuration,
                _previewRepeat));
            RefreshPreviewRepeatButton();
            RenderPreview();
        }

        void RefreshPreviewRepeatButton()
        {
            if (_previewRepeatButton == null)
                return;

            _previewRepeatButton.text = _previewRepeat ? "Repeat: On" : "Repeat: Off";
            _previewRepeatButton.tooltip =
                "Repeat the selected preview lifecycle. This does not change runtime playback.";
            SetClass(_previewRepeatButton, "ts-authoring-mode-active", _previewRepeat);
        }

        void PausePreviewPlayback()
        {
            if (!_isPreviewPlaying)
                return;

            _isPreviewPlaying = false;
            _lastPreviewTick = EditorApplication.timeSinceStartup;
            RefreshPreviewPlaybackButton();
        }

        void RefreshPreviewPlaybackButton()
        {
            if (_previewPlaybackButton == null)
                return;

            _previewPlaybackButton.text = _isPreviewPlaying ? "Pause" : "Play";
            _previewPlaybackButton.tooltip = _isPreviewPlaying
                ? "Pause automatic preview playback."
                : "Resume automatic preview playback from the current scrub position.";
        }

        void AdvancePreviewTime()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = _lastPreviewTick > 0.0 ? (float)(now - _lastPreviewTick) : 0f;
            _lastPreviewTick = now;
            if (!_isPreviewPlaying)
                return;
            _previewTime += Mathf.Max(0f, delta) * Mathf.Max(0f, _previewSpeed);
            if (!_previewRepeat &&
                _previewTime >= _previewCycleDuration &&
                !ShouldHoldActivePreview(_previewSegments))
            {
                _previewTime = _previewCycleDuration;
                _isPreviewPlaying = false;
                RefreshPreviewPlaybackButton();
            }
            if (_previewScrub != null)
            {
                _previewScrub.SetValueWithoutNotify(
                    GetPreviewProgress(
                        _previewTime,
                        _previewCycleDuration,
                        _previewRepeat));
            }
        }

        void UpdatePreviewCycleDuration(
            float revealDuration,
            float activeDuration,
            float hideDuration)
        {
            float nextDuration = TextStudioEffectPreviewUtility.GetCycleDuration(
                _previewSegments,
                revealDuration,
                activeDuration,
                hideDuration,
                _activePreviewDurationMultiplier);
            if (Mathf.Approximately(nextDuration, _previewCycleDuration))
                return;

            float previousDuration = Mathf.Max(0.0001f, _previewCycleDuration);
            float progress = _previewScrub != null
                ? Mathf.Clamp01(_previewScrub.value)
                : GetPreviewProgress(_previewTime, previousDuration, _previewRepeat);
            float cycle = _previewRepeat
                ? Mathf.Floor(Mathf.Max(0f, _previewTime) / previousDuration)
                : 0f;
            float continuousOverrun = _previewRepeat
                ? 0f
                : Mathf.Max(0f, _previewTime - previousDuration);
            _previewCycleDuration = nextDuration;
            _previewTime = (cycle + progress) * _previewCycleDuration + continuousOverrun;
            _previewScrub?.SetValueWithoutNotify(progress);
        }

        internal static float GetPreviewCycleDuration(
            TextStudioCompiledPlan plan,
            AnimationPhase phase)
        {
            if (plan?.EffectBindings == null)
                return DefaultPreviewCycleDuration;

            TextStudioPhaseMask requiredPhase = GetPhaseMask(phase);
            float cycleDuration = DefaultPreviewCycleDuration;
            int bindingCount = plan.EffectBindingCount > 0
                ? Mathf.Min(plan.EffectBindingCount, plan.EffectBindings.Length)
                : plan.EffectBindings.Length;
            for (int i = 0; i < bindingCount; i++)
            {
                EffectBinding binding = plan.EffectBindings[i];
                if ((binding.phaseMask != TextStudioPhaseMask.None &&
                     (binding.phaseMask & requiredPhase) == 0))
                {
                    continue;
                }

                float finiteDuration;
                if (binding.effect is TextStudioCompositionLayer layer)
                {
                    if (!layer.TryGetFinitePlaybackDuration(out finiteDuration))
                        continue;
                }
                else
                {
                    if (!binding.parameters.TryGetString("playback", out string playback))
                        continue;

                    float duration = DefaultRawEffectDuration;
                    if (!binding.parameters.TryGetFloat("duration", out duration) &&
                        !binding.parameters.TryGetFloat("d", out duration))
                    {
                        duration = DefaultRawEffectDuration;
                    }

                    if (!BuiltInEffectTiming.TryGetFinitePlaybackDuration(
                            playback,
                            duration,
                            out finiteDuration))
                    {
                        continue;
                    }
                }

                float speed = Mathf.Max(0.01f, binding.phaseParams.speed);
                float localDuration = Mathf.Max(0f, finiteDuration - binding.timeOffset) / speed;
                float totalDuration = ComputeMaxBindingDelay(plan, in binding) + localDuration;
                cycleDuration = Mathf.Max(cycleDuration, totalDuration);
            }

            return Mathf.Max(0.0001f, cycleDuration);
        }

        static TextStudioPreviewSegment GetPreviewSegment(AnimationPhase phase)
        {
            return phase switch
            {
                AnimationPhase.Appearance => TextStudioPreviewSegment.Reveal,
                AnimationPhase.Disappearance => TextStudioPreviewSegment.Hide,
                _ => TextStudioPreviewSegment.Active
            };
        }

        static TextStudioPhaseMask GetPhaseMask(AnimationPhase phase)
        {
            return phase switch
            {
                AnimationPhase.Appearance => TextStudioPhaseMask.Reveal,
                AnimationPhase.Disappearance => TextStudioPhaseMask.Hide,
                _ => TextStudioPhaseMask.Active
            };
        }

        internal static float GetPreviewProgress(float previewTime, float cycleDuration)
        {
            return GetPreviewProgress(previewTime, cycleDuration, true);
        }

        internal static float GetPreviewProgress(
            float previewTime,
            float cycleDuration,
            bool repeat)
        {
            float duration = Mathf.Max(0.0001f, cycleDuration);
            float safeTime = Mathf.Max(0f, previewTime);
            return repeat
                ? Mathf.Repeat(safeTime, duration) / duration
                : Mathf.Clamp01(safeTime / duration);
        }

        internal static float GetPreviewLifecycleTime(
            float previewTime,
            float cycleDuration,
            bool repeat)
        {
            float duration = Mathf.Max(0.0001f, cycleDuration);
            float safeTime = Mathf.Max(0f, previewTime);
            return repeat
                ? Mathf.Repeat(safeTime, duration)
                : Mathf.Min(safeTime, duration);
        }

        internal static bool ShouldHoldActivePreview(TextStudioPreviewSegment segments)
        {
            return (segments & TextStudioPreviewSegment.Active) != 0 &&
                   (segments & TextStudioPreviewSegment.Hide) == 0;
        }

        internal static float GetPreviewTimeFromScrub(
            float currentTime,
            float scrubProgress,
            float cycleDuration)
        {
            float duration = Mathf.Max(0.0001f, cycleDuration);
            float cycle = Mathf.Floor(Mathf.Max(0f, currentTime) / duration);
            return (cycle + Mathf.Clamp01(scrubProgress)) * duration;
        }

        internal static float GetLifecycleTimeForPhase(
            AnimationPhase phase,
            float phaseTime,
            TextStudioPreviewSegment segments,
            float revealDuration,
            float activeDuration,
            float hideDuration,
            int activeDurationMultiplier)
        {
            float reveal = Mathf.Max(0.0001f, revealDuration);
            float active = Mathf.Max(0.0001f, activeDuration);
            float activeWindow = active * Mathf.Max(1, activeDurationMultiplier);
            float hide = Mathf.Max(0.0001f, hideDuration);
            float lifecycleTime = 0f;

            if (phase == AnimationPhase.Appearance)
                return Mathf.Min(Mathf.Max(0f, phaseTime), reveal);

            if ((segments & TextStudioPreviewSegment.Reveal) != 0)
                lifecycleTime += reveal;

            if (phase == AnimationPhase.Persistent)
                return lifecycleTime + Mathf.Min(Mathf.Max(0f, phaseTime), activeWindow);

            if ((segments & TextStudioPreviewSegment.Active) != 0)
                lifecycleTime += activeWindow;

            return lifecycleTime + Mathf.Min(Mathf.Max(0f, phaseTime), hide);
        }

        static int NormalizeActivePreviewDurationMultiplier(int multiplier)
        {
            return multiplier switch
            {
                1 => 1,
                2 => 2,
                4 => 4,
                8 => 8,
                _ => DefaultActivePreviewDurationMultiplier
            };
        }

        static float ComputeMaxBindingDelay(TextStudioCompiledPlan plan, in EffectBinding binding)
        {
            int glyphStart = Mathf.Clamp(binding.glyphStart, 0, plan.VisibleGlyphCount);
            int glyphEnd = Mathf.Clamp(
                binding.glyphStart + binding.glyphCount,
                glyphStart,
                plan.VisibleGlyphCount);
            TextStudioEffectAreaSettings area = binding.area.Equals(default)
                ? binding.effect?.DefaultArea ?? TextStudioEffectAreaSettings.Default
                : binding.area;
            float maxDelay = 0f;
            for (int glyphIndex = glyphStart; glyphIndex < glyphEnd; glyphIndex++)
            {
                if (!TextStudioEffectAreaResolver.TryResolveUnit(
                        plan,
                        binding.glyphStart,
                        binding.glyphCount,
                        area,
                        glyphIndex,
                        out TextStudioEffectUnitContext unit))
                {
                    continue;
                }

                int wordIndex = plan.GlyphToWordIndex != null && glyphIndex < plan.GlyphToWordIndex.Length
                    ? Mathf.Max(0, plan.GlyphToWordIndex[glyphIndex])
                    : 0;
                int lineIndex = plan.GlyphToLineIndex != null && glyphIndex < plan.GlyphToLineIndex.Length
                    ? Mathf.Max(0, plan.GlyphToLineIndex[glyphIndex])
                    : 0;
                maxDelay = Mathf.Max(
                    maxDelay,
                    binding.phaseParams.ComputeDelay(unit.unitIndex, wordIndex, lineIndex));
            }

            return Mathf.Max(0f, maxDelay);
        }
    }
}
