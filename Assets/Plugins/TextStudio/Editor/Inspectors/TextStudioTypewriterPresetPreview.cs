using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioTypewriterPresetPreview : VisualElement, IDisposable
    {
        readonly Label _label;
        readonly Button _pause;
        IVisualElementScheduledItem _schedule;
        TextStudioTypewriterPreset _preset;
        float _hold;
        bool _hiding;
        double _lastTime;
        internal TextStudioBinding Binding { get; private set; }
        internal bool IsPaused { get; private set; }
        internal bool IsScheduled => _schedule != null;

        internal TextStudioTypewriterPresetPreview()
        {
            name = "ts-typewriter-preset-preview";
            _label = new Label { name = "ts-typewriter-preset-preview-text" };
            _label.style.minHeight = 110;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.fontSize = 26;
            _label.style.overflow = Overflow.Hidden;
            Add(_label);
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.Add(new Button(() => Replay(false)) { text = "Reveal", tooltip = "Replay this preset's reveal timing and effect." });
            controls.Add(new Button(() => Replay(true)) { text = "Hide", tooltip = "Show the sample, then replay this preset's hide effect." });
            _pause = new Button(() => SetPaused(!IsPaused)) { text = "Pause", tooltip = "Pause or resume the preview without changing the preset." };
            controls.Add(_pause);
            Add(controls);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Refresh(_preset);
                _lastTime = EditorApplication.timeSinceStartup;
                _schedule = schedule.Execute(() =>
                {
                    double now = EditorApplication.timeSinceStartup;
                    Advance(Mathf.Clamp((float)(now - _lastTime), 0, .1f));
                    _lastTime = now;
                }).Every(33);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        internal void Refresh(TextStudioTypewriterPreset preset)
        {
            _preset = preset;
            if (preset == null) return;
            Binding ??= _label.AttachTextStudio();
            Binding.EnableInEditMode = true;
            Binding.UpdateMode = TextStudioUITKUpdateMode.Manual;
            Binding.TypewriterSource = TextStudioTypewriterSource.Custom;
            Binding.TypewriterPreset = preset;
            Binding.SetText(preset.SampleText);
            Replay(false, resume: false);
        }

        internal void SetPaused(bool paused)
        {
            IsPaused = paused;
            _pause.text = paused ? "Resume" : "Pause";
        }

        internal void Replay(bool hide, bool resume = true)
        {
            if (Binding == null) return;
            if (resume) SetPaused(false);
            _hiding = hide;
            _hold = 0;
            if (hide) { Binding.SkipReveal(); Binding.StartDisappear(); }
            else Binding.StartReveal();
            Binding.UpdateFromHost(0);
        }

        internal void Advance(float delta)
        {
            if (Binding == null || IsPaused) return;
            Binding.UpdateFromHost(delta);
            if (Binding.IsRevealing || Binding.IsDisappearing) { _hold = 0; return; }
            _hold += delta;
            if (_hold >= (_hiding ? .5f : 1f)) Replay(!_hiding);
        }

        public void Dispose()
        {
            _schedule?.Pause();
            _schedule = null;
            _label.DetachTextStudio();
            Binding = null;
        }
    }
}
