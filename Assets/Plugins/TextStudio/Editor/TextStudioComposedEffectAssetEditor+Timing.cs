using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioComposedEffectAssetEditor
    {
        const float TimingCompactWidth = 520f;

        readonly struct LayerTimingDisplay
        {
            public readonly bool IsFinite;
            public readonly bool IsResizable;
            public readonly float BaseDuration;
            public readonly float Duration;
            public readonly string Label;
            public readonly string Description;

            public LayerTimingDisplay(
                bool isFinite,
                bool isResizable,
                float baseDuration,
                float duration,
                string label,
                string description)
            {
                IsFinite = isFinite;
                IsResizable = isResizable;
                BaseDuration = baseDuration;
                Duration = duration;
                Label = label ?? string.Empty;
                Description = description ?? string.Empty;
            }
        }

        sealed class RevealBlendCurveElement : VisualElement
        {
            const int Samples = 16;
            bool _fadesOut;

            public RevealBlendCurveElement()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetFadesOut(bool fadesOut)
            {
                if (_fadesOut == fadesOut)
                    return;

                _fadesOut = fadesOut;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                    return;

                float left = rect.x + 1f;
                float right = rect.xMax - 1f;
                float top = rect.y + 3f;
                float bottom = rect.yMax - 3f;
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.93f, 0.74f, 0.32f, 0.16f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, bottom));
                for (int i = 0; i <= Samples; i++)
                {
                    float t = i / (float)Samples;
                    float weight = t * t * (3f - 2f * t);
                    if (_fadesOut)
                        weight = 1f - weight;
                    painter.LineTo(new Vector2(
                        Mathf.Lerp(left, right, t),
                        Mathf.Lerp(bottom, top, weight)));
                }
                painter.LineTo(new Vector2(right, bottom));
                painter.ClosePath();
                painter.Fill();

                painter.strokeColor = new Color(0.93f, 0.77f, 0.41f, 0.95f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                for (int i = 0; i <= Samples; i++)
                {
                    float t = i / (float)Samples;
                    float weight = t * t * (3f - 2f * t);
                    if (_fadesOut)
                        weight = 1f - weight;
                    Vector2 point = new Vector2(
                        Mathf.Lerp(left, right, t),
                        Mathf.Lerp(bottom, top, weight));
                    if (i == 0)
                        painter.MoveTo(point);
                    else
                        painter.LineTo(point);
                }
                painter.Stroke();
            }
        }

        VisualElement CreateLayerViewToolbar()
        {
            VisualElement toolbar = new VisualElement
            {
                name = "ts-composed-layer-view-toolbar"
            };
            toolbar.AddToClassList("ts-composed-layer-view-toolbar");

            Label label = new Label("View");
            label.AddToClassList("ts-composed-layer-view-label");
            toolbar.Add(label);

            _layerListViewButton = new Button(() => SetLayerView(false))
            {
                name = "ts-composed-layer-list-view-button",
                text = "Layers",
                tooltip = "Edit every layer setting in the detailed layer cards."
            };
            _layerListViewButton.AddToClassList("ts-authoring-mode-button");
            toolbar.Add(_layerListViewButton);

            _layerTimingViewButton = new Button(() => SetLayerView(true))
            {
                name = "ts-composed-layer-timing-view-button",
                text = "Timing",
                tooltip = "Compare phase coverage, phase shifts, and playback duration across all layers."
            };
            _layerTimingViewButton.AddToClassList("ts-authoring-mode-button");
            toolbar.Add(_layerTimingViewButton);
            return toolbar;
        }

        internal void SetLayerView(bool showTiming)
        {
            if (_layerListView == null || _layerTimingView == null)
                return;

            _layerTimingViewSelected = showTiming;
            _layerListView.style.display = showTiming ? DisplayStyle.None : DisplayStyle.Flex;
            _layerTimingView.style.display = showTiming ? DisplayStyle.Flex : DisplayStyle.None;
            SetClass(_layerListViewButton, "ts-authoring-mode-active", !showTiming);
            SetClass(_layerTimingViewButton, "ts-authoring-mode-active", showTiming);
            SessionState.SetBool(GetFoldoutStateKey("layer-timing-view"), showTiming);
            if (showTiming)
            {
                if (!_layerTimingRowsBuilt)
                    RebuildLayerTimingRows();
                RefreshLayerTimingScrub();
            }
        }

        VisualElement CreateLayerTimingView()
        {
            _layerTimingRowsBuilt = false;
            _layerTimingBands.Clear();
            _layerTimingRevealBlendBands.Clear();
            _layerTimingPlayheads.Clear();
            _layerTimingRowRefreshers.Clear();
            InvalidateLayerTimingLayout();
            Array.Clear(_layerTimingPhaseHeaders, 0, _layerTimingPhaseHeaders.Length);

            VisualElement view = new VisualElement
            {
                name = "ts-composed-layer-timing-view"
            };
            view.AddToClassList("ts-composed-timing-view");
            view.RegisterCallback<GeometryChangedEvent>(evt =>
                SetClass(view, "ts-composed-timing-compact", evt.newRect.width < TimingCompactWidth));
            if (_revealActiveBlendDuration != null)
            {
                view.TrackPropertyValue(_revealActiveBlendDuration, _ =>
                {
                    InvalidateLayerTimingLayout();
                    RefreshLayerTimingLayout();
                });
            }

            VisualElement previewRow = new VisualElement
            {
                name = "ts-composed-timing-preview-row"
            };
            previewRow.AddToClassList("ts-composed-timing-preview-row");
            Label previewLabel = new Label("Live Preview");
            previewLabel.AddToClassList("ts-composed-timing-preview-label");
            previewRow.Add(previewLabel);
            _layerTimingScrub = new Slider(0f, 1f)
            {
                name = "ts-composed-timing-preview-scrub",
                tooltip = "Scrub the same preview lifecycle shown in Live Preview."
            };
            _layerTimingScrub.RegisterValueChangedCallback(evt => ScrubPreviewAt(evt.newValue));
            _layerTimingScrub.RegisterCallback<PointerDownEvent>(_ => PausePreviewPlayback());
            previewRow.Add(_layerTimingScrub);
            view.Add(previewRow);

            VisualElement legend = new VisualElement
            {
                name = "ts-composed-timing-legend"
            };
            legend.AddToClassList("ts-composed-timing-legend");
            AddTimingLegend(legend, "Reveal", "ts-composed-timing-reveal");
            AddTimingLegend(legend, "Active", "ts-composed-timing-active");
            AddTimingLegend(legend, "Hide", "ts-composed-timing-hide");
            AddTimingLegend(legend, "Reveal Blend", "ts-composed-timing-reveal-blend");
            view.Add(legend);

            VisualElement phaseHeader = new VisualElement
            {
                name = "ts-composed-timing-phase-header"
            };
            phaseHeader.AddToClassList("ts-composed-timing-row");
            phaseHeader.AddToClassList("ts-composed-timing-phase-header");
            Label layerHeader = new Label("Layer");
            layerHeader.AddToClassList("ts-composed-timing-layer-header");
            phaseHeader.Add(layerHeader);
            VisualElement phaseLanes = new VisualElement();
            phaseLanes.AddToClassList("ts-composed-timing-lanes");
            phaseHeader.Add(phaseLanes);
            _layerTimingPhaseHeaders[0] = AddTimingPhaseHeader(
                phaseLanes,
                "Reveal",
                "ts-composed-timing-reveal",
                TextStudioPreviewSegment.Reveal);
            _layerTimingPhaseHeaders[1] = AddTimingPhaseHeader(
                phaseLanes,
                "Active",
                "ts-composed-timing-active",
                TextStudioPreviewSegment.Active);
            _layerTimingPhaseHeaders[2] = AddTimingPhaseHeader(
                phaseLanes,
                "Hide",
                "ts-composed-timing-hide",
                TextStudioPreviewSegment.Hide);
            view.Add(phaseHeader);

            _layerTimingRows = new VisualElement
            {
                name = "ts-composed-timing-rows"
            };
            _layerTimingRows.AddToClassList("ts-composed-timing-rows");
            view.Add(_layerTimingRows);
            Label guidance = TextStudioInspectorElements.CreateMutedText(
                "Drag bars to shift their start; drag finite end handles to change speed. Joined phases play continuously.",
                "ts-composed-timing-guidance");
            view.Add(guidance);
            Label caveat = TextStudioInspectorElements.CreateMutedText(
                "Lane widths follow Live Preview. Reveal Blend curves appear only on lanes that participate in the Reveal-to-Active transition.",
                "ts-composed-timing-caveat");
            view.Add(caveat);
            return view;
        }

        static void AddTimingLegend(VisualElement parent, string label, string colorClass)
        {
            VisualElement item = new VisualElement();
            item.AddToClassList("ts-composed-timing-legend-item");
            VisualElement swatch = new VisualElement();
            swatch.AddToClassList("ts-composed-timing-swatch");
            swatch.AddToClassList(colorClass);
            item.Add(swatch);
            item.Add(new Label(label));
            parent.Add(item);
        }

        static Label AddTimingPhaseHeader(
            VisualElement parent,
            string label,
            string colorClass,
            TextStudioPreviewSegment segment)
        {
            Label phase = new Label(label)
            {
                name = "ts-composed-timing-phase-header-" + label.ToLowerInvariant(),
                tooltip = "This column is " + label.ToLowerInvariant() +
                    " timing. Live Preview controls whether this phase window plays.",
                userData = segment
            };
            phase.AddToClassList("ts-composed-timing-phase-label");
            phase.AddToClassList(colorClass);
            parent.Add(phase);
            return phase;
        }

        void RebuildLayerTimingRows()
        {
            if (_layerTimingRows == null)
                return;

            _layerTimingRows.Clear();
            _layerTimingBands.Clear();
            _layerTimingRevealBlendBands.Clear();
            _layerTimingPlayheads.Clear();
            _layerTimingRowRefreshers.Clear();
            InvalidateLayerTimingLayout();
            _layerTimingRowsBuilt = true;
            serializedObject.UpdateIfRequiredOrScript();
            if (_layers == null || !_layers.isArray || _layers.arraySize == 0)
            {
                _layerTimingRows.Add(TextStudioInspectorElements.CreateMutedText(
                    "No visual layers yet. Add a layer below to build a timing map.",
                    "ts-composed-timing-empty"));
                RefreshLayerTimingLayout();
                return;
            }

            for (int i = 0; i < _layers.arraySize; i++)
                _layerTimingRows.Add(CreateLayerTimingRow(_layers.GetArrayElementAtIndex(i), i));
            RefreshLayerTimingScrub();
            RefreshLayerTimingPreviewState();
        }

        internal void EnsureLayerTimingRowsCurrent()
        {
            if (!_layerTimingViewSelected || _layerTimingRows == null || target == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            int layerCount = _layers != null && _layers.isArray ? _layers.arraySize : 0;
            int expectedChildCount = layerCount > 0 ? layerCount : 1;
            if (!_layerTimingRowsBuilt || _layerTimingRows.childCount != expectedChildCount)
                RebuildLayerTimingRows();
        }

        void RefreshLayerTimingRowsAfterStructureChange()
        {
            if (_layerTimingRows == null || !_layerTimingRowsBuilt)
                return;

            if (_layerTimingViewSelected)
            {
                RebuildLayerTimingRows();
                return;
            }

            _layerTimingRows.Clear();
            _layerTimingRowsBuilt = false;
        }

        VisualElement CreateLayerTimingRow(SerializedProperty layer, int index)
        {
            SerializedProperty kind = layer.FindPropertyRelative("_kind");
            SerializedProperty channel = layer.FindPropertyRelative("_channel");
            SerializedProperty effectId = layer.FindPropertyRelative("_effectId");
            SerializedProperty writingStyle = layer.FindPropertyRelative("_writingStyle");
            SerializedProperty name = layer.FindPropertyRelative("_name");
            SerializedProperty enabled = layer.FindPropertyRelative("_enabled");
            SerializedProperty phases = layer.FindPropertyRelative("_phases");
            SerializedProperty stableId = layer.FindPropertyRelative("_stableId");
            SerializedProperty phaseShift = layer.FindPropertyRelative("_timeOffset");
            SerializedProperty playback = layer.FindPropertyRelative("_playback");
            SerializedProperty customPlayback = layer.FindPropertyRelative("_customPlayback");
            SerializedProperty timing = layer.FindPropertyRelative("_timing");
            SerializedProperty speed = timing?.FindPropertyRelative("speed");
            string stableIdValue = stableId?.stringValue ?? string.Empty;

            VisualElement row = new VisualElement
            {
                name = "ts-composed-timing-row-" + index
            };
            row.AddToClassList("ts-composed-timing-row");

            VisualElement header = new VisualElement();
            header.AddToClassList("ts-composed-timing-layer");
            row.Add(header);
            Button title = new Button(() => OpenLayerFromTiming(stableIdValue))
            {
                name = "ts-composed-timing-edit-" + index,
                tooltip = "Open this layer in the detailed Layers view."
            };
            title.AddToClassList("ts-composed-timing-layer-title");
            header.Add(title);
            Label details = new Label();
            details.AddToClassList("ts-composed-timing-layer-details");
            header.Add(details);

            VisualElement lanes = new VisualElement();
            lanes.AddToClassList("ts-composed-timing-lanes");
            row.Add(lanes);
            VisualElement clipOverlay = new VisualElement
            {
                name = "ts-composed-timing-clips-" + index
            };
            clipOverlay.AddToClassList("ts-composed-timing-clip-overlay");

            VisualElement[] bands = new VisualElement[3];
            VisualElement[] clips = new VisualElement[3];
            Label[] clipLabels = new Label[3];
            Label[] infinitePatterns = new Label[3];
            VisualElement[] resizeHandles = new VisualElement[3];
            TextStudioPhaseMask[] phaseValues =
            {
                TextStudioPhaseMask.Reveal,
                TextStudioPhaseMask.Active,
                TextStudioPhaseMask.Hide
            };
            string[] phaseClasses =
            {
                "ts-composed-timing-reveal",
                "ts-composed-timing-active",
                "ts-composed-timing-hide"
            };
            TextStudioPreviewSegment[] previewSegments =
            {
                TextStudioPreviewSegment.Reveal,
                TextStudioPreviewSegment.Active,
                TextStudioPreviewSegment.Hide
            };

            Action refresh = null;
            for (int phaseIndex = 0; phaseIndex < phaseValues.Length; phaseIndex++)
            {
                VisualElement band = new VisualElement();
                band.AddToClassList("ts-composed-timing-band");
                band.AddToClassList(phaseClasses[phaseIndex]);
                band.userData = previewSegments[phaseIndex];
                bands[phaseIndex] = band;
                _layerTimingBands.Add(band);
                lanes.Add(band);

                VisualElement clip = new VisualElement
                {
                    name = "ts-composed-timing-clip-" + index + "-" + phaseIndex
                };
                clip.AddToClassList("ts-composed-timing-clip");
                clip.AddToClassList(phaseClasses[phaseIndex]);
                clips[phaseIndex] = clip;
                clipOverlay.Add(clip);

                Label clipLabel = new Label();
                clipLabel.AddToClassList("ts-composed-timing-clip-label");
                clipLabels[phaseIndex] = clipLabel;
                clip.Add(clipLabel);

                Label infinitePattern = new Label("╱  ╱  ╱  ╱  ╱  ╱  ╱  ╱")
                {
                    pickingMode = PickingMode.Ignore
                };
                infinitePattern.AddToClassList("ts-composed-timing-infinite-pattern");
                infinitePatterns[phaseIndex] = infinitePattern;
                clip.Insert(0, infinitePattern);

                VisualElement resizeHandle = new VisualElement
                {
                    name = "ts-composed-timing-resize-" + index + "-" + phaseIndex,
                    tooltip = "Drag to change finite playback duration. Hold Alt for 0.01-second precision."
                };
                resizeHandle.AddToClassList("ts-composed-timing-resize");
                resizeHandles[phaseIndex] = resizeHandle;
                clip.Add(resizeHandle);

                AttachPhaseShiftDrag(clip, phaseShift, () => refresh?.Invoke());
                AttachDurationResize(
                    resizeHandle,
                    kind,
                    playback,
                    customPlayback,
                    speed,
                    () => refresh?.Invoke());
            }

            VisualElement revealBlendBand = CreateRevealBlendWindow(
                "ts-composed-timing-reveal-blend-" + index,
                "ts-composed-timing-reveal-blend-band");
            _layerTimingRevealBlendBands.Add(revealBlendBand);
            lanes.Add(clipOverlay);
            clipOverlay.Add(revealBlendBand);
            for (int phaseIndex = 0; phaseIndex < previewSegments.Length; phaseIndex++)
            {
                VisualElement playhead = new VisualElement
                {
                    name = "ts-composed-timing-playhead-" + index + "-" + phaseIndex,
                    pickingMode = PickingMode.Ignore,
                    userData = previewSegments[phaseIndex]
                };
                playhead.AddToClassList("ts-composed-timing-playhead");
                _layerTimingPlayheads.Add(playhead);
                clipOverlay.Add(playhead);
            }

            refresh = () =>
            {
                if (target == null)
                    return;

                serializedObject.UpdateIfRequiredOrScript();
                TextStudioCompositionLayerKind layerKind =
                    (TextStudioCompositionLayerKind)kind.enumValueIndex;
                LayerTimingDisplay display = ResolveLayerTimingDisplay(
                    layerKind,
                    playback,
                    customPlayback,
                    speed);
                float shift = phaseShift != null ? phaseShift.floatValue : 0f;
                float visualStart = TextStudioComposedTimingMath.VisualStartFromPhaseShift(shift);
                float phaseWindow = GetLayerTimingPhaseWindow(1);
                float maximumDisplayedShift = Mathf.Max(0.85f, phaseWindow * 0.85f);
                float displayedStart = Mathf.Clamp(
                    visualStart,
                    -maximumDisplayedShift,
                    maximumDisplayedShift);
                float width = display.IsFinite
                    ? Mathf.Max(display.Duration, 0.08f)
                    : 1.7f;
                bool runStartClamped = !Mathf.Approximately(visualStart, displayedStart);
                TextStudioPhaseMask mask = (TextStudioPhaseMask)phases.intValue;
                float timelineDuration = GetLayerTimingTimelineDuration();
                float revealWindow = GetLayerTimingPhaseWindow(0);
                float configuredBlend = _revealActiveBlendDuration != null
                    ? Mathf.Max(0f, _revealActiveBlendDuration.floatValue)
                    : 0f;
                float blendStart = 0f;
                float blendDuration = 0f;
                bool showsRevealBlend =
                    enabled.boolValue &&
                    TextStudioComposedTimingMath.TryResolveRevealBlendWindow(
                        mask,
                        shift,
                        revealWindow,
                        configuredBlend,
                        out blendStart,
                        out blendDuration);
                bool includesReveal = (mask & TextStudioPhaseMask.Reveal) != 0;
                bool includesActive = (mask & TextStudioPhaseMask.Active) != 0;
                bool fadesOut = includesReveal && !includesActive;
                bool extendsActiveClipForBlend =
                    showsRevealBlend &&
                    !includesReveal &&
                    includesActive;

                title.text = BuildLayerTitle(
                    layerKind,
                    channel,
                    effectId,
                    writingStyle,
                    name,
                    enabled,
                    phases);
                details.text = BuildLayerTimingSummary(layerKind, display, shift);
                SetClass(row, "ts-composed-timing-row-off", !enabled.boolValue);
                for (int phaseIndex = 0; phaseIndex < phaseValues.Length; phaseIndex++)
                {
                    bool active = (mask & phaseValues[phaseIndex]) != 0;
                    SetClass(bands[phaseIndex], "ts-composed-timing-band-empty", !active);
                    if (!active)
                    {
                        clips[phaseIndex].style.display = DisplayStyle.None;
                        clipLabels[phaseIndex].text = string.Empty;
                        SetClass(
                            clips[phaseIndex],
                            "ts-composed-timing-clip-continues-in",
                            false);
                        SetClass(
                            clips[phaseIndex],
                            "ts-composed-timing-clip-continues-out",
                            false);
                        continue;
                    }

                    int runStartIndex = phaseIndex;
                    while (runStartIndex > 0 &&
                           (mask & phaseValues[runStartIndex - 1]) != 0)
                    {
                        runStartIndex--;
                    }

                    int runEndIndex = phaseIndex;
                    while (runEndIndex < phaseValues.Length - 1 &&
                           (mask & phaseValues[runEndIndex + 1]) != 0)
                    {
                        runEndIndex++;
                    }

                    float runPhaseStart = GetLayerTimingPhaseStart(runStartIndex);
                    float runPhaseEnd =
                        GetLayerTimingPhaseStart(runEndIndex) +
                        GetLayerTimingPhaseWindow(runEndIndex);
                    float runStart = runPhaseStart + displayedStart;
                    if (runStartIndex == 1 && extendsActiveClipForBlend)
                        runStart = Mathf.Min(runStart, blendStart);
                    float runPlaybackEnd = display.IsFinite
                        ? Mathf.Min(runStart + width, runPhaseEnd)
                        : runPhaseEnd;
                    float phaseStart = GetLayerTimingPhaseStart(phaseIndex);
                    float phaseEnd =
                        phaseStart + GetLayerTimingPhaseWindow(phaseIndex);
                    float timelineStart = phaseIndex == runStartIndex
                        ? runStart
                        : Mathf.Max(phaseStart, runStart);
                    float timelineEnd = Mathf.Min(phaseEnd, runPlaybackEnd);
                    bool visible = timelineEnd > timelineStart + 0.0001f;
                    clips[phaseIndex].style.display = visible
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    if (!visible)
                    {
                        clipLabels[phaseIndex].text = string.Empty;
                        SetClass(
                            clips[phaseIndex],
                            "ts-composed-timing-clip-continues-in",
                            false);
                        SetClass(
                            clips[phaseIndex],
                            "ts-composed-timing-clip-continues-out",
                            false);
                        continue;
                    }

                    bool continuesIn =
                        phaseIndex > runStartIndex &&
                        runStart <= phaseStart + 0.0001f;
                    bool continuesOut =
                        phaseIndex < runEndIndex &&
                        runPlaybackEnd >= phaseEnd - 0.0001f;
                    int labelOwnerIndex = runStartIndex;
                    for (int candidate = runStartIndex; candidate <= runEndIndex; candidate++)
                    {
                        float candidateEnd =
                            GetLayerTimingPhaseStart(candidate) +
                            GetLayerTimingPhaseWindow(candidate);
                        if (runStart < candidateEnd - 0.0001f || candidate == runEndIndex)
                        {
                            labelOwnerIndex = candidate;
                            break;
                        }
                    }

                    clips[phaseIndex].style.left =
                        Length.Percent(timelineStart / timelineDuration * 100f);
                    clips[phaseIndex].style.width =
                        Length.Percent(
                            Mathf.Max(0f, timelineEnd - timelineStart) /
                            timelineDuration *
                            100f);
                    float segmentWidth = Mathf.Max(0f, timelineEnd - timelineStart);
                    float hiddenStart = Mathf.Max(0f, -timelineStart);
                    float labelStart = segmentWidth > 0.0001f
                        ? Mathf.Clamp01(hiddenStart / segmentWidth)
                        : 0f;
                    clipLabels[phaseIndex].style.left = Length.Percent(labelStart * 100f);
                    bool ownsTrailingEnd = !continuesOut;
                    clipLabels[phaseIndex].style.right =
                        display.IsResizable && ownsTrailingEnd ? 8f : 0f;
                    clipLabels[phaseIndex].text =
                        phaseIndex == labelOwnerIndex ? display.Label : string.Empty;
                    resizeHandles[phaseIndex].style.display =
                        display.IsResizable && ownsTrailingEnd
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    infinitePatterns[phaseIndex].style.display = display.IsFinite
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                    SetClass(clips[phaseIndex], "ts-composed-timing-clip-infinite", !display.IsFinite);
                    SetClass(
                        clips[phaseIndex],
                        "ts-composed-timing-clip-clamped",
                        runStartClamped && phaseIndex == labelOwnerIndex);
                    SetClass(
                        clips[phaseIndex],
                        "ts-composed-timing-clip-continues-in",
                        continuesIn);
                    SetClass(
                        clips[phaseIndex],
                        "ts-composed-timing-clip-continues-out",
                        continuesOut);
                    clips[phaseIndex].tooltip = BuildTimingClipTooltip(
                        display,
                        shift,
                        runStartClamped,
                        continuesIn,
                        continuesOut);
                }

                string blendTooltip = showsRevealBlend
                    ? "This lane " +
                      (fadesOut ? "fades out" : "fades into Active") +
                      " for " +
                      blendDuration.ToString("0.##") +
                      " seconds along the shown curve."
                    : "This lane does not participate in the Reveal-to-Active transition.";
                ConfigureRevealBlendWindow(
                    revealBlendBand,
                    showsRevealBlend
                        ? blendStart / timelineDuration * 100f
                        : 0f,
                    showsRevealBlend
                        ? blendDuration / timelineDuration * 100f
                        : 0f,
                    blendTooltip,
                    fadesOut);
            };

            row.TrackPropertyValue(name, _ => refresh());
            row.TrackPropertyValue(enabled, _ => refresh());
            row.TrackPropertyValue(phases, _ => refresh());
            row.TrackPropertyValue(phaseShift, _ => refresh());
            row.TrackPropertyValue(playback, _ => refresh());
            row.TrackPropertyValue(customPlayback, _ => refresh());
            if (speed != null)
                row.TrackPropertyValue(speed, _ => refresh());
            _layerTimingRowRefreshers.Add(refresh);
            refresh();
            return row;
        }

        void RefreshLayerTimingPreviewState()
        {
            RefreshLayerTimingLayout();
            TextStudioPreviewSegment currentSegment = GetPreviewSegment(_previewPhase);
            for (int i = 0; i < _layerTimingPhaseHeaders.Length; i++)
            {
                VisualElement header = _layerTimingPhaseHeaders[i];
                if (header?.userData is not TextStudioPreviewSegment segment)
                    continue;

                bool enabled = (_previewSegments & segment) != 0;
                SetClass(header, "ts-composed-timing-preview-off", !enabled);
                SetClass(
                    header,
                    "ts-composed-timing-preview-current",
                    enabled && segment == currentSegment);
            }

            for (int i = 0; i < _layerTimingBands.Count; i++)
            {
                VisualElement band = _layerTimingBands[i];
                if (band?.userData is not TextStudioPreviewSegment segment)
                    continue;

                bool enabled = (_previewSegments & segment) != 0;
                SetClass(band, "ts-composed-timing-preview-off", !enabled);
                SetClass(
                    band,
                    "ts-composed-timing-preview-current",
                    enabled && segment == currentSegment);
            }

            bool revealPreviewEnabled =
                (_previewSegments & TextStudioPreviewSegment.Reveal) != 0;
            for (int i = 0; i < _layerTimingRevealBlendBands.Count; i++)
            {
                SetClass(
                    _layerTimingRevealBlendBands[i],
                    "ts-composed-timing-preview-off",
                    !revealPreviewEnabled);
            }

            for (int i = 0; i < _layerTimingPlayheads.Count; i++)
            {
                VisualElement playhead = _layerTimingPlayheads[i];
                if (playhead?.userData is not TextStudioPreviewSegment segment)
                    continue;

                bool visible = (_previewSegments & segment) != 0 &&
                               segment == currentSegment;
                playhead.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    int phaseIndex = GetPreviewSegmentIndex(segment);
                    float timelineDuration = GetLayerTimingTimelineDuration();
                    float phaseWindow = GetLayerTimingPhaseWindow(phaseIndex);
                    float phaseTime = Mathf.Clamp(
                        _layerTimingPhaseTime,
                        0f,
                        phaseWindow);
                    playhead.style.left = Length.Percent(
                        (GetLayerTimingPhaseStart(phaseIndex) + phaseTime) /
                        timelineDuration *
                        100f);
                }
            }
        }

        static int GetPreviewSegmentIndex(TextStudioPreviewSegment segment)
        {
            return segment switch
            {
                TextStudioPreviewSegment.Reveal => 0,
                TextStudioPreviewSegment.Hide => 2,
                _ => 1
            };
        }

        void RefreshLayerTimingLayout()
        {
            float revealWindow = GetLayerTimingPhaseWindow(0);
            float activeWindow = GetLayerTimingPhaseWindow(1);
            float hideWindow = GetLayerTimingPhaseWindow(2);
            float configuredRevealBlend =
                _revealActiveBlendDuration != null
                    ? Mathf.Max(0f, _revealActiveBlendDuration.floatValue)
                    : 0f;
            float revealBlendWindow = configuredRevealBlend;
            if (Mathf.Approximately(_layerTimingRevealWindow, revealWindow) &&
                Mathf.Approximately(_layerTimingActiveWindow, activeWindow) &&
                Mathf.Approximately(_layerTimingHideWindow, hideWindow) &&
                Mathf.Approximately(_layerTimingRevealBlendWindow, revealBlendWindow))
            {
                return;
            }

            _layerTimingRevealWindow = revealWindow;
            _layerTimingActiveWindow = activeWindow;
            _layerTimingHideWindow = hideWindow;
            _layerTimingRevealBlendWindow = revealBlendWindow;
            float timelineDuration = revealWindow + activeWindow + hideWindow;
            for (int phaseIndex = 0; phaseIndex < _layerTimingPhaseHeaders.Length; phaseIndex++)
            {
                float width = GetLayerTimingPhaseWindow(phaseIndex) /
                              timelineDuration *
                              100f;
                VisualElement header = _layerTimingPhaseHeaders[phaseIndex];
                if (header != null)
                {
                    header.style.flexGrow = 0f;
                    header.style.flexBasis = Length.Percent(width);
                    header.style.width = Length.Percent(width);
                    header.tooltip = BuildLayerTimingPhaseTooltip(phaseIndex);
                }
            }

            for (int i = 0; i < _layerTimingBands.Count; i++)
            {
                VisualElement band = _layerTimingBands[i];
                int phaseIndex = GetPreviewSegmentIndex(
                    band.userData is TextStudioPreviewSegment segment
                        ? segment
                        : TextStudioPreviewSegment.Active);
                float width = GetLayerTimingPhaseWindow(phaseIndex) /
                              timelineDuration *
                              100f;
                band.style.flexGrow = 0f;
                band.style.flexBasis = Length.Percent(width);
                band.style.width = Length.Percent(width);
            }

            for (int i = 0; i < _layerTimingRowRefreshers.Count; i++)
                _layerTimingRowRefreshers[i]?.Invoke();
        }

        void InvalidateLayerTimingLayout()
        {
            _layerTimingRevealWindow = -1f;
            _layerTimingActiveWindow = -1f;
            _layerTimingHideWindow = -1f;
            _layerTimingRevealBlendWindow = -1f;
        }

        static VisualElement CreateRevealBlendWindow(string name, string modifierClass)
        {
            RevealBlendCurveElement window = new RevealBlendCurveElement
            {
                name = name
            };
            window.AddToClassList("ts-composed-timing-reveal-blend-window");
            window.AddToClassList("ts-composed-timing-reveal-blend-curve");
            window.AddToClassList(modifierClass);
            return window;
        }

        static void ConfigureRevealBlendWindow(
            VisualElement window,
            float left,
            float width,
            string tooltip,
            bool fadesOut)
        {
            if (window == null)
                return;

            window.style.display = width > 0.0001f
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            window.style.left = Length.Percent(left);
            window.style.width = Length.Percent(width);
            window.tooltip = tooltip;
            if (window is RevealBlendCurveElement curve)
                curve.SetFadesOut(fadesOut);
            SetClass(
                window,
                "ts-composed-timing-reveal-blend-fade-out",
                fadesOut);
        }

        float GetLayerTimingTimelineDuration()
        {
            return TextStudioComposedTimingMath.ResolveTimelineDuration(
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier);
        }

        float GetLayerTimingPhaseStart(int phaseIndex)
        {
            return TextStudioComposedTimingMath.ResolvePhaseStart(
                phaseIndex,
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier);
        }

        float GetLayerTimingPhaseWindow(int phaseIndex)
        {
            return TextStudioComposedTimingMath.ResolvePhaseDuration(
                phaseIndex,
                _previewRevealDuration,
                _previewActiveDuration,
                _previewHideDuration,
                _activePreviewDurationMultiplier);
        }

        string BuildLayerTimingPhaseTooltip(int phaseIndex)
        {
            string phase = phaseIndex switch
            {
                0 => "Reveal",
                2 => "Hide",
                _ => "Active"
            };
            float duration = GetLayerTimingPhaseWindow(phaseIndex);
            return phase + " occupies " + duration.ToString("0.##") +
                   " seconds on the current preview timeline.";
        }

        static LayerTimingDisplay ResolveLayerTimingDisplay(
            TextStudioCompositionLayerKind kind,
            SerializedProperty playback,
            SerializedProperty customPlayback,
            SerializedProperty speed)
        {
            if (kind == TextStudioCompositionLayerKind.Effect)
            {
                return new LayerTimingDisplay(
                    false,
                    false,
                    0f,
                    0f,
                    "Effect",
                    "The nested effect owns its playback duration.");
            }
            TextStudioEffectPlaybackPreset preset = playback != null
                ? (TextStudioEffectPlaybackPreset)playback.enumValueIndex
                : TextStudioEffectPlaybackPreset.Loop;
            if (kind == TextStudioCompositionLayerKind.Writing &&
                preset == TextStudioEffectPlaybackPreset.External)
            {
                return new LayerTimingDisplay(
                    false,
                    false,
                    0f,
                    0f,
                    "Phase",
                    "Writing progress follows the enabled composition phases.");
            }
            float baseDuration = 1f;
            bool finite = preset == TextStudioEffectPlaybackPreset.Once ||
                          preset == TextStudioEffectPlaybackPreset.Appearance ||
                          preset == TextStudioEffectPlaybackPreset.Weighted;
            string label = preset.ToString();
            string subject = kind == TextStudioCompositionLayerKind.Writing
                ? "Writing"
                : "Property";
            string description = finite
                ? "Finite " + subject.ToLowerInvariant() + " playback."
                : "Continuous " + subject.ToLowerInvariant() + " playback.";

            if (preset == TextStudioEffectPlaybackPreset.Custom)
            {
                EffectPlaybackAsset asset =
                    customPlayback?.objectReferenceValue as EffectPlaybackAsset;
                if (asset == null)
                {
                    return new LayerTimingDisplay(
                        false,
                        false,
                        0f,
                        0f,
                        "Missing",
                        "Custom playback has no assigned Playback asset.");
                }

                finite = !asset.IsInfinite;
                label = finite ? "Custom" : "Custom ∞";
                description = finite
                    ? "Finite duration from the assigned Playback asset."
                    : "The assigned Playback asset continues indefinitely.";
                if (finite)
                    baseDuration = asset.GetTotalDuration();
            }
            else if (preset == TextStudioEffectPlaybackPreset.External)
            {
                label = "External";
                description = "Runtime state drives this property playback.";
            }

            float speedValue = speed != null ? speed.floatValue : 1f;
            float duration = finite
                ? TextStudioComposedTimingMath.ResolveDuration(baseDuration, speedValue)
                : 0f;
            if (finite)
                label = duration.ToString("0.##") + " s";
            return new LayerTimingDisplay(
                finite,
                finite,
                baseDuration,
                duration,
                label,
                description);
        }

        static string BuildLayerTimingSummary(
            TextStudioCompositionLayerKind kind,
            LayerTimingDisplay display,
            float phaseShift)
        {
            string start = Mathf.Approximately(phaseShift, 0f)
                ? "Starts on phase boundary"
                : phaseShift > 0f
                    ? "Starts " + phaseShift.ToString("0.##") + " s early"
                    : "Starts " + Mathf.Abs(phaseShift).ToString("0.##") + " s late";
            string timing = kind == TextStudioCompositionLayerKind.Effect
                ? "Nested timing"
                : display.Description;
            return start + " · " + timing;
        }

        static string BuildTimingClipTooltip(
            LayerTimingDisplay display,
            float phaseShift,
            bool clippedStart,
            bool continuesIn,
            bool continuesOut)
        {
            string start = Mathf.Approximately(phaseShift, 0f)
                ? "The run starts on its first enabled phase boundary."
                : phaseShift > 0f
                    ? "The bar starts " + phaseShift.ToString("0.##") +
                      " seconds before its first enabled phase."
                    : "The bar starts " + Mathf.Abs(phaseShift).ToString("0.##") +
                      " seconds after its first enabled phase begins.";
            string tooltip = display.Description + " " + start +
                              " Drag the bar to change its start. Hold Alt for 0.01-second precision.";
            if (display.IsResizable)
                tooltip += " Drag the trailing handle to change duration.";
            if (continuesIn)
                tooltip += " Playback continues from the previous enabled phase without restarting.";
            if (continuesOut)
                tooltip += " Playback continues into the next enabled phase without restarting.";
            if (clippedStart)
                tooltip += " The exact start is outside this compact phase lane.";
            return tooltip;
        }

        void OpenLayerFromTiming(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
                return;

            _expandedLayerStableId = stableId;
            SessionState.SetString(GetFoldoutStateKey("layer-accordion"), stableId);
            SetLayerView(false);
            SynchronizeLayerAccordion();
            if (_layerFoldouts.TryGetValue(stableId, out Foldout foldout))
            {
                foldout.SetValueWithoutNotify(true);
                foldout.schedule.Execute(foldout.Focus).StartingIn(0);
            }
        }

        void AttachPhaseShiftDrag(
            VisualElement clip,
            SerializedProperty phaseShift,
            Action refresh)
        {
            if (clip == null || phaseShift == null)
                return;

            bool dragging = false;
            int pointerId = -1;
            float startPosition = 0f;
            float initialShift = 0f;
            clip.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                dragging = true;
                pointerId = evt.pointerId;
                startPosition = evt.position.x;
                initialShift = phaseShift.floatValue;
                Undo.RecordObjects(serializedObject.targetObjects, "Adjust Composed Effect Phase Shift");
                clip.CapturePointer(pointerId);
                SetClass(clip, "ts-composed-timing-dragging", true);
                evt.StopPropagation();
            });
            clip.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId || !clip.HasPointerCapture(pointerId))
                    return;
                float overlayWidth = clip.parent?.resolvedStyle.width ?? 0f;
                if (overlayWidth <= 1f)
                    return;
                float pixelsPerSecond =
                    overlayWidth / GetLayerTimingTimelineDuration();
                float delta = (evt.position.x - startPosition) / pixelsPerSecond;
                float next = TextStudioComposedTimingMath.SnapPhaseShift(
                    initialShift - delta,
                    evt.altKey);
                if (Mathf.Approximately(phaseShift.floatValue, next))
                    return;
                phaseShift.floatValue = next;
                ApplyLayerTimingEdit(refresh);
                evt.StopPropagation();
            });
            clip.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId)
                    return;
                dragging = false;
                if (clip.HasPointerCapture(pointerId))
                    clip.ReleasePointer(pointerId);
                SetClass(clip, "ts-composed-timing-dragging", false);
                UpdateValidation();
                evt.StopPropagation();
            });
            clip.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                dragging = false;
                SetClass(clip, "ts-composed-timing-dragging", false);
            });
        }

        void AttachDurationResize(
            VisualElement handle,
            SerializedProperty kind,
            SerializedProperty playback,
            SerializedProperty customPlayback,
            SerializedProperty speed,
            Action refresh)
        {
            if (handle == null || speed == null)
                return;

            bool dragging = false;
            int pointerId = -1;
            float startPosition = 0f;
            float initialDuration = 1f;
            float baseDuration = 1f;
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                TextStudioCompositionLayerKind layerKind =
                    (TextStudioCompositionLayerKind)kind.enumValueIndex;
                LayerTimingDisplay display = ResolveLayerTimingDisplay(
                    layerKind,
                    playback,
                    customPlayback,
                    speed);
                if (!display.IsResizable)
                    return;
                dragging = true;
                pointerId = evt.pointerId;
                startPosition = evt.position.x;
                initialDuration = display.Duration;
                baseDuration = display.BaseDuration;
                Undo.RecordObjects(serializedObject.targetObjects, "Resize Composed Effect Playback");
                handle.CapturePointer(pointerId);
                SetClass(handle, "ts-composed-timing-dragging", true);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId || !handle.HasPointerCapture(pointerId))
                    return;
                VisualElement clipOverlay = handle.parent?.parent;
                float overlayWidth = clipOverlay?.resolvedStyle.width ?? 0f;
                if (overlayWidth <= 1f)
                    return;
                float pixelsPerSecond =
                    overlayWidth / GetLayerTimingTimelineDuration();
                float delta = (evt.position.x - startPosition) / pixelsPerSecond;
                float duration = TextStudioComposedTimingMath.SnapDuration(
                    initialDuration + delta,
                    evt.altKey);
                float nextSpeed = TextStudioComposedTimingMath.ResolveSpeed(baseDuration, duration);
                if (Mathf.Approximately(speed.floatValue, nextSpeed))
                    return;
                speed.floatValue = nextSpeed;
                ApplyLayerTimingEdit(refresh);
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging || evt.pointerId != pointerId)
                    return;
                dragging = false;
                if (handle.HasPointerCapture(pointerId))
                    handle.ReleasePointer(pointerId);
                SetClass(handle, "ts-composed-timing-dragging", false);
                UpdateValidation();
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                dragging = false;
                SetClass(handle, "ts-composed-timing-dragging", false);
            });
        }

        void ApplyLayerTimingEdit(Action refresh)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            MarkTargetsDirty();
            InvalidatePreviewPlan();
            refresh?.Invoke();
            RenderPreview();
        }

        void RefreshLayerTimingScrub()
        {
            if (_layerTimingScrub == null)
                return;

            float progress = _previewScrub != null
                ? Mathf.Clamp01(_previewScrub.value)
                : GetPreviewProgress(_previewTime, _previewCycleDuration, _previewRepeat);
            _layerTimingScrub.SetValueWithoutNotify(progress);
        }
    }
}
