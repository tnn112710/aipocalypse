using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioCodeEffectAsset), editorForChildClasses: true)]
    internal sealed class TextStudioCodeEffectAssetEditor : UnityEditor.Editor
    {
        const double PreviewTickIntervalMs = 33.0;
        const float PreviewPhaseDuration = 1f;

        readonly List<PreviewGlyph> _previewGlyphs = new List<PreviewGlyph>(96);
        readonly List<int> _previewGlyphMap = new List<int>(96);
        readonly List<Button> _phaseButtons = new List<Button>(3);
        readonly List<Button> _speedButtons = new List<Button>(5);
        readonly TextStudioPipeline _previewPipeline = new TextStudioPipeline();

        TextStudioCompiledPlan _previewPlan;
        ITextStudioEffect _previewEffect;
        VisualElement _previewCanvas;
        Slider _previewScrub;
        Button _playbackButton;
        Button _repeatButton;
        Label _previewStatus;
        TextStudioPreviewSegment _previewSegments = TextStudioPreviewSegment.Reveal |
            TextStudioPreviewSegment.Active;
        float _previewTime = 0.5f;
        float _previewSpeed = 1f;
        double _lastPreviewTick;
        int _previewDirtyCount = int.MinValue;
        int _previewCycleIndex = -1;
        bool _previewPlaying = true;
        bool _previewRepeat = true;
        bool _updatingScrub;

        internal bool IsPreviewPlaying => _previewPlaying;
        internal float PreviewTime => _previewTime;

        struct PreviewGlyph
        {
            public Label Label;
            public int GlyphIndex;
        }

        public override bool RequiresConstantRepaint()
        {
            return target != null && _previewPlaying;
        }

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-code-effect-editor-root");
            root.AddToClassList("ts-code-effect-editor-root");

            root.Add(CreateIdentitySection(root));
            root.Add(CreatePreviewSection());
            root.Add(CreateDefaultParametersSection());
            root.Add(CreateCapabilitiesSection());
            root.TrackSerializedObjectValue(serializedObject, _ => InvalidatePreviewEffect());
            root.RegisterCallback<DetachFromPanelEvent>(_ => DisposePreview());

            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (!CanRefresh(root))
                    return;
                serializedObject.UpdateIfRequiredOrScript();
                TickAndRenderPreview();
            }, () =>
            {
                if (!CanRefresh(root))
                    return;
                TickAndRenderPreview();
            }, (long)PreviewTickIntervalMs);

            _lastPreviewTick = EditorApplication.timeSinceStartup;
            RebuildPreviewGlyphs();
            RenderPreview();
            return root;
        }

        VisualElement CreateIdentitySection(VisualElement inspectorRoot)
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Code Effect",
                "ts-code-effect-identity");
            AddProperty(section, "_id", "Tag ID", "ts-code-effect-id");
            AddProperty(section, "_category", "Category", "ts-code-effect-category");
            AddProperty(section, "_displayName", "Display Name", "ts-code-effect-display-name");
            AddProperty(section, "_description", "Description", "ts-code-effect-description");
            AddProperty(section, "_markupSnippet", "Markup Example", "ts-code-effect-markup");
            AddProperty(section, "_suggestedPhases", "Suggested Phases", "ts-code-effect-suggested-phases");

            Label contract = TextStudioInspectorElements.CreateMutedText(
                "Code Effects implement programmable behavior and publish typed defaults. Lifecycle, curves, playback, sequence, and scope are authored by Composed Effects.",
                "ts-code-effect-authoring-contract");
            section.Add(contract);

            Label catalogStatus = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-code-effect-catalog-status");
            section.Add(catalogStatus);
            Button catalogButton = new Button(() =>
            {
                TextStudioCodeEffectAsset asset = target as TextStudioCodeEffectAsset;
                if (TextStudioEffectCatalogUtility.Contains(asset))
                    TextStudioEffectCatalogUtility.Remove(asset);
                else
                    TextStudioEffectCatalogUtility.Add(asset);
            })
            {
                name = "ts-code-effect-catalog-button"
            };
            Button openCatalog = new Button(() =>
            {
                TextStudioProjectSettings settings = TextStudioEffectCatalogUtility.FindSettings();
                if (settings == null)
                    settings = TextStudioEffectCatalogUtility.CreateSettingsNear(target as TextStudioEffectDefinitionAsset);
                if (settings != null)
                    Selection.activeObject = settings;
            })
            {
                name = "ts-code-effect-open-catalog"
            };
            section.Add(TextStudioInspectorElements.CreateButtonRow(catalogButton, openCatalog));

            TextStudioInspectorElements.SchedulePeriodicRefresh(inspectorRoot, () =>
            {
                if (!CanRefresh(section))
                    return;
                TextStudioCodeEffectAsset asset = target as TextStudioCodeEffectAsset;
                bool persistent = asset != null && EditorUtility.IsPersistent(asset);
                bool inCatalog = TextStudioEffectCatalogUtility.Contains(asset);
                bool hasId = asset != null && !string.IsNullOrWhiteSpace(asset.Id);
                bool canCreate = asset != null &&
                    TextStudioEffects.TryCreate(asset, in TextStudioTagParameters.Empty, out _);
                string message;
                CommonInspectorStatusType type;
                if (!persistent)
                {
                    message = "Save this Code Effect as an asset before adding it to the effect catalog.";
                    type = CommonInspectorStatusType.Info;
                }
                else if (!hasId || !canCreate)
                {
                    message = "Resolve the Code Effect identity or parameter validation error before cataloging it.";
                    type = CommonInspectorStatusType.Warning;
                }
                else if (inCatalog)
                {
                    message = "Ready. This Code Effect is available as tag and Existing Effect id '" + asset.Id + "'.";
                    type = CommonInspectorStatusType.Success;
                }
                else
                {
                    message = "Add this Code Effect to the catalog so its id is available to markup and Composed Effects.";
                    type = CommonInspectorStatusType.Info;
                }

                TextStudioInspectorElements.SetStatus(catalogStatus, message, type);
                catalogButton.text = inCatalog ? "Remove" : "Add to Catalog";
                catalogButton.SetEnabled(inCatalog || (persistent && hasId && canCreate));
                openCatalog.text = TextStudioEffectCatalogUtility.FindSettings() == null
                    ? "Create Catalog"
                    : "Open Catalog";
            }, 250);
            return section;
        }

        VisualElement CreateDefaultParametersSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Default Parameters",
                "ts-code-effect-default-parameters");
            AddProperty(section, "_defaultParameters", null, "ts-code-effect-parameters");
            Label snapshot = TextStudioInspectorElements.CreateMutedText(
                "A new Existing Effect layer snapshots these values. Later default changes apply only when that layer is explicitly reset.",
                "ts-code-effect-snapshot-guidance");
            section.Add(snapshot);
            return section;
        }

        VisualElement CreateCapabilitiesSection()
        {
            Foldout foldout = TextStudioInspectorElements.CreateFoldoutSection(
                "Capabilities",
                "ts-code-effect-capabilities",
                false);
            AddProperty(foldout, "_defaultArea", "Default Area", "ts-code-effect-default-area");
            AddProperty(foldout, "_defaultGroupSize", "Default Group Size", "ts-code-effect-default-group-size");
            AddProperty(foldout, "_blendMode", "Default Blend", "ts-code-effect-blend-mode");
            AddProperty(foldout, "_renderChannels", "Render Channels", "ts-code-effect-render-channels");
            AddProperty(foldout, "_supportedPhases", "Supported Phases", "ts-code-effect-supported-phases");
            AddProperty(foldout, "_performanceTier", "Performance Tier", "ts-code-effect-performance-tier");
            AddProperty(foldout, "_reducedMotionBehavior", "Reduced Motion", "ts-code-effect-reduced-motion");
            AddProperty(foldout, "_isDeterministic", "Deterministic", "ts-code-effect-deterministic");
            AddProperty(foldout, "_canFlash", "Can Flash", "ts-code-effect-can-flash");
            AddProperty(foldout, "_supportedRenderDomains", "Render Domains", "ts-code-effect-render-domains");
            AddProperty(foldout, "_boundsBehavior", "Bounds Behavior", "ts-code-effect-bounds-behavior");
            return foldout;
        }

        VisualElement CreatePreviewSection()
        {
            VisualElement section = TextStudioInspectorElements.CreateSection(
                "Live Preview",
                "ts-code-effect-preview");
            section.AddToClassList("ts-preview-section");
            section.Add(TextStudioPreviewExportWindow.CreateMenuButton(
                "ts-code-effect-preview-export-menu",
                target as TextStudioCodeEffectAsset,
                TryCreateExportSnapshot));

            VisualElement phaseRow = new VisualElement();
            phaseRow.AddToClassList("ts-preview-segment-row");
            section.Add(phaseRow);
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Reveal);
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Active);
            AddPhaseButton(phaseRow, TextStudioPreviewSegment.Hide);

            _previewScrub = new Slider("Scrub", 0f, 1f)
            {
                name = "ts-code-effect-preview-scrub",
                value = 0.5f
            };
            _previewScrub.AddToClassList("ts-scrub-slider");
            _previewScrub.RegisterValueChangedCallback(evt =>
            {
                if (_updatingScrub)
                    return;
                _previewPlaying = false;
                _previewTime = Mathf.Clamp01(evt.newValue) * GetPreviewCycleDuration();
                RefreshPlaybackButton();
                RenderPreview();
            });
            section.Add(_previewScrub);

            VisualElement controls = new VisualElement();
            controls.AddToClassList("ts-button-row");
            _playbackButton = new Button(() =>
            {
                _previewPlaying = !_previewPlaying;
                if (_previewPlaying && !_previewRepeat &&
                    _previewTime >= GetPreviewCycleDuration())
                {
                    _previewTime = 0f;
                    ResetPreviewEffect();
                }
                _lastPreviewTick = EditorApplication.timeSinceStartup;
                RefreshPlaybackButton();
            })
            {
                name = "ts-code-effect-preview-playback"
            };
            controls.Add(_playbackButton);

            _speedButtons.Clear();
            for (int i = 0; i < TextStudioEffectPreviewUtility.SpeedPresetCount; i++)
            {
                float speed = TextStudioEffectPreviewUtility.GetSpeed(i);
                Button button = new Button(() =>
                {
                    _previewSpeed = speed;
                    RefreshSpeedButtons();
                })
                {
                    text = TextStudioEffectPreviewUtility.GetSpeedLabel(i)
                };
                button.AddToClassList("ts-preview-button");
                _speedButtons.Add(button);
                controls.Add(button);
            }

            _repeatButton = new Button(() =>
            {
                _previewRepeat = !_previewRepeat;
                RefreshRepeatButton();
            })
            {
                name = "ts-code-effect-preview-repeat"
            };
            _repeatButton.AddToClassList("ts-preview-button");
            controls.Add(_repeatButton);
            section.Add(controls);

            _previewCanvas = new VisualElement
            {
                name = "ts-code-effect-preview-canvas"
            };
            _previewCanvas.AddToClassList("ts-effect-preview-canvas");
            section.Add(_previewCanvas);
            _previewStatus = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-code-effect-preview-status");
            section.Add(_previewStatus);

            RefreshPhaseButtons();
            RefreshPlaybackButton();
            RefreshRepeatButton();
            RefreshSpeedButtons();
            return section;
        }

        void AddPhaseButton(VisualElement parent, TextStudioPreviewSegment segment)
        {
            Button button = TextStudioTextEditor.CreatePreviewSegmentButton(
                TextStudioTextEditor.GetPreviewSegmentLabel(segment),
                "Include the " + TextStudioTextEditor.GetPreviewSegmentLabel(segment).ToLowerInvariant() +
                " phase in Code Effect preview playback.",
                () =>
                {
                    bool enabled = (_previewSegments & segment) != 0;
                    _previewSegments = TextStudioEffectPreviewUtility.SetSegmentEnabled(
                        _previewSegments,
                        segment,
                        !enabled);
                    _previewTime = Mathf.Min(_previewTime, GetPreviewCycleDuration());
                    RefreshPhaseButtons();
                    RenderPreview();
                });
            button.name = "ts-code-effect-preview-segment-" +
                TextStudioTextEditor.GetPreviewSegmentLabel(segment).ToLowerInvariant();
            button.userData = segment;
            _phaseButtons.Add(button);
            parent.Add(button);
        }

        void AddProperty(VisualElement parent, string propertyPath, string label, string elementName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
                return;
            PropertyField field = string.IsNullOrEmpty(label)
                ? new PropertyField(property.Copy())
                : new PropertyField(property.Copy(), label);
            field.name = elementName;
            field.tooltip = property.tooltip;
            field.AddToClassList("ts-property-field");
            parent.Add(field);
        }

        void TickAndRenderPreview()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = _lastPreviewTick > 0.0
                ? Mathf.Max(0f, (float)(now - _lastPreviewTick))
                : 0f;
            _lastPreviewTick = now;
            if (_previewPlaying)
            {
                float duration = GetPreviewCycleDuration();
                _previewTime += delta * Mathf.Max(0f, _previewSpeed);
                if (!_previewRepeat && _previewTime >= duration)
                {
                    _previewTime = duration;
                    _previewPlaying = false;
                    RefreshPlaybackButton();
                }
            }

            RenderPreview(delta);
        }

        void RenderPreview(float deltaTime = 0f)
        {
            if (_previewCanvas == null || target is not TextStudioCodeEffectAsset asset)
                return;
            EnsurePreviewEffect(asset);
            if (_previewPlan == null || _previewEffect == null)
            {
                if (_previewStatus != null)
                    _previewStatus.text = "The Code Effect could not create a preview evaluator. Check its default parameter validation.";
                return;
            }

            if (_previewStatus != null)
                _previewStatus.text = string.Empty;
            float duration = GetPreviewCycleDuration();
            float lifecycleTime = _previewRepeat
                ? Mathf.Repeat(Mathf.Max(0f, _previewTime), duration)
                : Mathf.Clamp(_previewTime, 0f, duration);
            int cycleIndex = Mathf.FloorToInt(Mathf.Max(0f, _previewTime) / duration);
            if (_previewRepeat && cycleIndex != _previewCycleIndex)
            {
                _previewCycleIndex = cycleIndex;
                ResetPreviewEffect();
            }

            TextStudioEffectPreviewUtility.ResolveFrame(
                lifecycleTime,
                _previewSegments,
                PreviewPhaseDuration,
                PreviewPhaseDuration,
                PreviewPhaseDuration,
                out AnimationPhase phase,
                out float phaseProgress,
                out float phaseTime,
                out _);
            int totalGlyphs = Mathf.Max(1, _previewPlan.VisibleGlyphCount);
            TextStudioEffectAreaSettings area = asset.DefaultArea;
            for (int i = 0; i < _previewGlyphs.Count; i++)
            {
                PreviewGlyph previewGlyph = _previewGlyphs[i];
                int glyphIndex = previewGlyph.GlyphIndex;
                if (glyphIndex < 0 || glyphIndex >= _previewPlan.VisibleGlyphCount)
                {
                    TextStudioEffectPreviewUtility.ApplyState(
                        previewGlyph.Label,
                        EvaluatedGlyphState.Identity);
                    continue;
                }

                int wordIndex = _previewPlan.GlyphToWordIndex != null
                    ? TextSegmenter.GetWordIndexFast(_previewPlan.GlyphToWordIndex, glyphIndex)
                    : TextSegmenter.GetWordIndex(
                        _previewPlan.WordRanges,
                        _previewPlan.WordCount,
                        glyphIndex);
                int lineIndex = _previewPlan.GlyphToLineIndex != null
                    ? TextSegmenter.GetLineIndexFast(_previewPlan.GlyphToLineIndex, glyphIndex)
                    : TextSegmenter.GetLineIndex(
                        _previewPlan.LineRanges,
                        _previewPlan.LineCount,
                        glyphIndex);
                if (!TextStudioEffectAreaResolver.TryResolveUnit(
                        _previewPlan,
                        0,
                        _previewPlan.VisibleGlyphCount,
                        area,
                        glyphIndex,
                        out TextStudioEffectUnitContext unit))
                {
                    unit = TextStudioEffectUnitContext.SingleGlyph(glyphIndex, totalGlyphs);
                }

                EvaluatedGlyphState state = EvaluatedGlyphState.Identity;
                state.currentPhase = phase;
                state.phaseProgress = phaseProgress;
                EffectEvaluationContext context = new EffectEvaluationContext(
                    glyphIndex,
                    totalGlyphs,
                    Mathf.Max(0, wordIndex),
                    Mathf.Max(0, lineIndex),
                    phase,
                    phaseProgress,
                    phaseTime,
                    Mathf.Max(0f, _previewTime),
                    Mathf.Max(0f, deltaTime),
                    unit.normalizedUnitIndex,
                    unit);
                _previewEffect.Evaluate(ref state, in context);
                TextStudioEffectPreviewUtility.ApplyState(previewGlyph.Label, state);
            }

            _updatingScrub = true;
            _previewScrub?.SetValueWithoutNotify(
                duration > 0f ? Mathf.Clamp01(lifecycleTime / duration) : 0f);
            _updatingScrub = false;
        }

        void EnsurePreviewEffect(TextStudioCodeEffectAsset asset)
        {
            int dirtyCount = asset != null ? EditorUtility.GetDirtyCount(asset) : 0;
            if (_previewEffect != null && dirtyCount == _previewDirtyCount)
                return;
            _previewDirtyCount = dirtyCount;
            _previewCycleIndex = -1;
            TextStudioEffects.TryCreate(
                asset,
                in TextStudioTagParameters.Empty,
                out _previewEffect);
        }

        void RebuildPreviewGlyphs()
        {
            if (_previewCanvas == null)
                return;
            _previewCanvas.Clear();
            _previewGlyphs.Clear();
            _previewPipeline.Process(new StringTextSource(TextStudioEffectPreviewUtility.SampleText));
            _previewPlan = _previewPipeline.Plan;
            string text = _previewPlan != null && !string.IsNullOrEmpty(_previewPlan.ResolvedText)
                ? _previewPlan.ResolvedText
                : "Preview";
            TextStudioEffectPreviewUtility.BuildGlyphIndexMap(text, _previewGlyphMap);
            int previewIndex = 0;
            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("ts-effect-preview-line");
                _previewCanvas.Add(row);
                string line = lines[lineIndex].Length > 0 ? lines[lineIndex] : " ";
                for (int i = 0; i < line.Length; i++)
                {
                    string character = line[i] == ' ' ? "\u00A0" : line[i].ToString();
                    Label label = TextStudioEffectPreviewUtility.CreateGlyph(character);
                    label.AddToClassList("ts-effect-preview-glyph");
                    row.Add(label);
                    int glyphIndex = previewIndex < _previewGlyphMap.Count
                        ? _previewGlyphMap[previewIndex]
                        : -1;
                    _previewGlyphs.Add(new PreviewGlyph
                    {
                        Label = label,
                        GlyphIndex = glyphIndex
                    });
                    previewIndex++;
                }
            }
        }

        void InvalidatePreviewEffect()
        {
            _previewDirtyCount = int.MinValue;
            _previewEffect = null;
            _previewCycleIndex = -1;
            RenderPreview();
        }

        void ResetPreviewEffect()
        {
            _previewEffect?.Reset();
        }

        void DisposePreview()
        {
            _previewEffect = null;
            _previewPlan = null;
            _previewGlyphs.Clear();
            _previewGlyphMap.Clear();
        }

        float GetPreviewCycleDuration()
        {
            return TextStudioEffectPreviewUtility.GetCycleDuration(
                _previewSegments,
                PreviewPhaseDuration,
                PreviewPhaseDuration,
                PreviewPhaseDuration);
        }

        void RefreshPhaseButtons()
        {
            for (int i = 0; i < _phaseButtons.Count; i++)
            {
                Button button = _phaseButtons[i];
                if (button?.userData is not TextStudioPreviewSegment segment)
                    continue;
                TextStudioTextEditor.SyncPreviewSegmentButton(
                    button,
                    segment,
                    (_previewSegments & segment) != 0);
            }
        }

        void RefreshPlaybackButton()
        {
            if (_playbackButton != null)
                _playbackButton.text = _previewPlaying ? "Pause" : "Play";
        }

        void RefreshRepeatButton()
        {
            if (_repeatButton == null)
                return;
            _repeatButton.text = "Repeat: " + (_previewRepeat ? "On" : "Off");
            _repeatButton.EnableInClassList("ts-preview-segment-selected", _previewRepeat);
        }

        void RefreshSpeedButtons()
        {
            for (int i = 0; i < _speedButtons.Count; i++)
            {
                float speed = TextStudioEffectPreviewUtility.GetSpeed(i);
                _speedButtons[i].EnableInClassList(
                    "ts-preview-speed-selected",
                    Mathf.Approximately(speed, _previewSpeed));
            }
        }

        static bool CanRefresh(VisualElement element)
        {
            return element != null && element.panel != null;
        }

        bool TryCreateExportSnapshot(
            out TextStudioPreviewExportSnapshot snapshot,
            out string error)
        {
            if (serializedObject != null && serializedObject.targetObject != null)
                serializedObject.UpdateIfRequiredOrScript();
            return TextStudioPreviewExportSnapshot.TryCreate(
                target as TextStudioCodeEffectAsset,
                _previewSegments,
                _previewSpeed,
                out snapshot,
                out error);
        }
    }
}
