using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioAuthoringPreviewContext
    {
        public TextStudioPreview Preview { get; set; }
        public Func<TextStudioPreview> PreviewProvider { get; set; }
        public Func<float> DisplayProgress { get; set; }
        public Func<bool> IsRevealing { get; set; }
        public Func<bool> IsDisappearing { get; set; }
        public Func<bool> IsWaitingForAction { get; set; }
        public Func<bool> IsRevealPaused { get; set; }
        public Action SkipReveal { get; set; }
        public Action ResumeReveal { get; set; }
        public Action SkipCurrentWait { get; set; }
        public Action StateChanged { get; set; }
    }

    internal sealed class TextStudioAuthoringPreviewControls
    {
        readonly TextStudioAuthoringPreviewContext _context;
        readonly List<Button> _speedButtons = new List<Button>();

        Slider _scrub;
        Button _reveal;
        Button _active;
        Button _hideSegment;
        Button _activeDuration;
        Button _playPause;
        Button _repeat;
        Button _endState;
        Button _skipReveal;
        Button _playHide;
        Button _continueReveal;
        Button _skipWait;

        public TextStudioAuthoringPreviewControls(TextStudioAuthoringPreviewContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (_context.Preview == null && _context.PreviewProvider == null)
                throw new ArgumentException("A preview controller is required.", nameof(context));
        }

        TextStudioPreview Preview => _context.PreviewProvider?.Invoke() ?? _context.Preview;

        public void AddTo(VisualElement parent)
        {
            if (parent == null)
                return;

            parent.Add(BuildSegments());
            parent.Add(BuildScrubRow());
            parent.Add(BuildPlaybackRow());

            VisualElement separator = new VisualElement
            {
                name = "ts-preview-command-separator"
            };
            separator.AddToClassList("ts-preview-command-separator");
            parent.Add(separator);
            parent.Add(BuildCommandRow());
            Refresh();
        }

        public void Refresh()
        {
            TextStudioPreview preview = Preview;
            if (preview == null)
                return;
            _scrub?.SetValueWithoutNotify(
                _context.DisplayProgress != null
                    ? Mathf.Clamp01(_context.DisplayProgress())
                    : preview.Progress);
            SyncSegmentButton(_reveal, TextStudioPreviewSegment.Reveal, preview);
            SyncSegmentButton(_active, TextStudioPreviewSegment.Active, preview);
            SyncSegmentButton(_hideSegment, TextStudioPreviewSegment.Hide, preview);

            if (_playPause != null)
            {
                _playPause.text = preview.IsPlaying ? "Pause" : "Play";
                _playPause.tooltip = preview.IsPlaying
                    ? "Pause the complete preview at its current state."
                    : "Play or resume the selected preview phases.";
            }

            if (_activeDuration != null)
            {
                _activeDuration.text = "Active ×" + preview.ActiveDurationMultiplier;
                _activeDuration.SetEnabled(preview.IsSegmentEnabled(TextStudioPreviewSegment.Active));
            }

            if (_repeat != null)
            {
                _repeat.text = preview.Repeat ? "Repeat: On" : "Repeat: Off";
                _repeat.EnableInClassList("ts-authoring-mode-active", preview.Repeat);
            }

            for (int i = 0; i < _speedButtons.Count; i++)
            {
                Button speed = _speedButtons[i];
                bool selected = speed?.userData is float value &&
                                Mathf.Approximately(preview.PlaybackSpeed, value);
                speed?.EnableInClassList("ts-authoring-mode-active", selected);
            }

            SetVisible(_endState, !preview.ShowEndState);
            SetVisible(_skipReveal, Read(_context.IsRevealing));
            SetVisible(
                _playHide,
                !preview.IsSegmentEnabled(TextStudioPreviewSegment.Hide) &&
                !Read(_context.IsDisappearing));
            SetVisible(
                _continueReveal,
                Read(_context.IsRevealPaused) && !Read(_context.IsWaitingForAction));
            SetVisible(_skipWait, Read(_context.IsWaitingForAction));
        }

        VisualElement BuildSegments()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-preview-segment-row");
            _reveal = CreateSegmentButton(
                TextStudioPreviewSegment.Reveal,
                "Include the reveal/typewriter segment in preview playback.");
            _reveal.name = "ts-preview-segment-reveal";
            _active = CreateSegmentButton(
                TextStudioPreviewSegment.Active,
                "Include the fully visible active segment in preview playback.");
            _active.name = "ts-preview-segment-active";
            _hideSegment = CreateSegmentButton(
                TextStudioPreviewSegment.Hide,
                "Include the hide/disappearance segment in preview playback.");
            _hideSegment.name = "ts-preview-segment-hide";
            row.Add(_reveal);
            row.Add(_active);
            row.Add(_hideSegment);
            return row;
        }

        Button CreateSegmentButton(TextStudioPreviewSegment segment, string tooltip)
        {
            return TextStudioTextEditor.CreatePreviewSegmentButton(
                TextStudioTextEditor.GetPreviewSegmentLabel(segment),
                tooltip,
                () =>
                {
                    TextStudioPreview preview = Preview;
                    if (preview == null)
                        return;
                    preview.SetSegmentEnabled(segment, !preview.IsSegmentEnabled(segment));
                    Changed();
                });
        }

        VisualElement BuildScrubRow()
        {
            VisualElement row = new VisualElement
            {
                name = "ts-preview-scrub-row"
            };
            row.AddToClassList("ts-composed-preview-scrub-row");
            _scrub = new Slider("Scrub", 0f, 1f)
            {
                name = "ts-preview-scrub-slider",
                tooltip = "Pause and move directly through the selected preview phases."
            };
            _scrub.AddToClassList("ts-scrub-slider");
            _scrub.RegisterValueChangedCallback(evt =>
            {
                Preview?.Scrub(evt.newValue);
                Changed();
            });
            row.Add(_scrub);

            _activeDuration = new Button(() =>
            {
                TextStudioPreview preview = Preview;
                if (preview == null)
                    return;
                int multiplier = preview.ActiveDurationMultiplier;
                preview.ActiveDurationMultiplier = multiplier >= 8 ? 1 : multiplier * 2;
                Changed();
            })
            {
                name = "ts-preview-active-duration",
                tooltip = "Extend the preview-only Active phase without changing runtime playback."
            };
            _activeDuration.AddToClassList("ts-composed-active-duration-button");
            row.Add(_activeDuration);
            return row;
        }

        VisualElement BuildPlaybackRow()
        {
            VisualElement row = new VisualElement
            {
                name = "ts-preview-playback-row"
            };
            row.AddToClassList("ts-button-row");
            _playPause = CreateButton("Play", "Play or resume the selected preview phases.", () =>
            {
                TextStudioPreview preview = Preview;
                if (preview == null)
                    return;
                if (preview.IsPlaying)
                    preview.Pause();
                else
                    preview.Play();
                Changed();
            });
            _playPause.name = "ts-preview-playback-button";
            row.Add(_playPause);

            for (int i = 0; i < TextStudioEffectPreviewUtility.SpeedPresetCount; i++)
            {
                float previewSpeed = TextStudioEffectPreviewUtility.GetSpeed(i);
                Button speed = CreateButton(
                    TextStudioEffectPreviewUtility.GetSpeedLabel(i),
                    "Set preview playback to " +
                    TextStudioEffectPreviewUtility.GetSpeedLabel(i) +
                    " and resume from the current position without changing authored reveal timing.",
                    () =>
                    {
                        TextStudioPreview preview = Preview;
                        if (preview == null)
                            return;
                        preview.PlaybackSpeed = previewSpeed;
                        if (!preview.IsPlaying)
                            preview.Play();
                        Changed();
                    });
                speed.userData = previewSpeed;
                _speedButtons.Add(speed);
                row.Add(speed);
            }

            _repeat = CreateButton(
                "Repeat: Off",
                "Repeat the selected preview lifecycle. This does not change authored typewriter repeat settings.",
                () =>
                {
                    TextStudioPreview preview = Preview;
                    if (preview == null)
                        return;
                    preview.Repeat = !preview.Repeat;
                    Changed();
                });
            _repeat.name = "ts-preview-repeat-button";
            _repeat.AddToClassList("ts-preview-repeat-button");
            row.Add(_repeat);
            return row;
        }

        VisualElement BuildCommandRow()
        {
            VisualElement row = new VisualElement
            {
                name = "ts-preview-command-row"
            };
            row.AddToClassList("ts-preview-controls-row");
            row.AddToClassList("ts-button-row");
            row.AddToClassList("ts-preview-command-row");

            Button restart = CreateButton("Restart", "Restart preview playback from the beginning.", () =>
            {
                Preview?.Restart();
                Changed();
            });
            restart.name = "ts-preview-restart-button";
            _endState = CreateButton(
                "End State",
                "Return to the continuously animated authoring end state.",
                () =>
                {
                    Preview?.ShowEndStateAgain();
                    Changed();
                });
            _endState.name = "ts-preview-end-state-button";
            _skipReveal = CreateButton(
                "Skip Reveal",
                "Skip reveal to the fully visible state without restarting active effects.",
                () =>
                {
                    _context.SkipReveal?.Invoke();
                    Changed();
                });
            _skipReveal.name = "ts-preview-skip-reveal-button";
            _playHide = CreateButton(
                "Play Hide",
                "Play the hide phase once without enabling the Hide preview segment.",
                () =>
                {
                    Preview?.Hide();
                    Changed();
                });
            _playHide.name = "ts-preview-hide-button";
            _continueReveal = CreateButton(
                "Continue",
                "Resume reveal from an action pause without skipping text.",
                () =>
                {
                    _context.ResumeReveal?.Invoke();
                    Changed();
                });
            _continueReveal.name = "ts-preview-continue-button";
            _skipWait = CreateButton(
                "Skip Wait",
                "Skip the active wait or input wait.",
                () =>
                {
                    _context.SkipCurrentWait?.Invoke();
                    Changed();
                });
            _skipWait.name = "ts-preview-skip-wait-button";

            row.Add(restart);
            row.Add(_endState);
            row.Add(_skipReveal);
            row.Add(_playHide);
            row.Add(_continueReveal);
            row.Add(_skipWait);
            return row;
        }

        void Changed()
        {
            _context.StateChanged?.Invoke();
            Refresh();
        }

        static Button CreateButton(string text, string tooltip, Action action)
        {
            Button button = new Button(action)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("ts-preview-button");
            return button;
        }

        static void SyncSegmentButton(
            Button button,
            TextStudioPreviewSegment segment,
            TextStudioPreview preview)
        {
            if (button == null || preview == null)
                return;
            TextStudioTextEditor.SyncPreviewSegmentButton(
                button,
                segment,
                preview.IsSegmentEnabled(segment));
        }

        static bool Read(Func<bool> getter)
        {
            return getter != null && getter();
        }

        static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
                return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            element.SetEnabled(visible);
        }
    }
}
