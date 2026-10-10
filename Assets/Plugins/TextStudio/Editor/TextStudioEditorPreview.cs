using ImpossibleRobert.Common.Timing;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioEditorPreview : VisualElement
    {
        readonly bool _loopReveal;
        readonly float _replayInterval;

        IVisualElementScheduledItem _scheduledItem;
        double _lastHostTime;
        float _replayTime;
        bool _paused;

        internal Label PreviewLabel { get; }
        internal TextStudioBinding Binding { get; }
        internal bool IsPaused => _paused;
        internal bool HasLiveSchedule => _scheduledItem != null;

        internal TextStudioEditorPreview(
            string markup,
            RevealMode revealMode,
            bool loopReveal,
            float replayInterval,
            float fontSize)
        {
            _loopReveal = loopReveal;
            _replayInterval = Mathf.Max(1f, replayInterval);

            name = "ts-editor-live-preview";
            AddToClassList("ts-editor-live-preview");
            pickingMode = PickingMode.Ignore;

            PreviewLabel = new Label(markup)
            {
                name = "ts-editor-live-preview-label",
                pickingMode = PickingMode.Ignore
            };
            PreviewLabel.AddToClassList("ts-editor-live-preview-label");
            PreviewLabel.style.fontSize = fontSize;
            Binding = PreviewLabel.AttachTextStudio();
            Binding.EnableInEditMode = true;
            Binding.UpdateMode = TextStudioUITKUpdateMode.Manual;
            Binding.RevealMode = revealMode;
            Binding.SpeedMultiplier = 1f;
            Binding.SetText(markup);
            if (revealMode == RevealMode.Instant)
                Binding.SkipReveal();
            else
                Binding.StartReveal();
            Add(PreviewLabel);

            RegisterCallback<AttachToPanelEvent>(HandleAttached);
            RegisterCallback<DetachFromPanelEvent>(HandleDetached);
        }

        internal void Replay()
        {
            _replayTime = 0f;
            Binding.StartReveal();
        }

        internal void SetPaused(bool paused)
        {
            if (_paused == paused)
                return;

            _paused = paused;
            if (_paused)
            {
                if (Binding.RevealMode != RevealMode.Instant)
                {
                    Binding.SkipReveal();
                    Binding.UpdateFromHost(0f);
                }
                _scheduledItem?.Pause();
                return;
            }

            if (panel != null)
                StartSchedule();
        }

        void HandleAttached(AttachToPanelEvent evt)
        {
            if (!_paused)
                StartSchedule();
        }

        void HandleDetached(DetachFromPanelEvent evt)
        {
            _scheduledItem?.Pause();
            _scheduledItem = null;
        }

        void StartSchedule()
        {
            _lastHostTime = HostTime.CurrentHostTime(ClockMode.Realtime);
            if (_scheduledItem == null)
            {
                _scheduledItem = schedule.Execute(Tick).Every(16);
            }
            else
            {
                _scheduledItem.Resume();
            }
        }

        void Tick()
        {
            double now = HostTime.CurrentHostTime(ClockMode.Realtime);
            float deltaTime = HostTime.ClampDelta(
                (float)(now - _lastHostTime),
                HostTime.DefaultMaxDeltaTime);
            _lastHostTime = now;
            Binding.UpdateFromHost(deltaTime);

            if (!_loopReveal || Binding.RevealMode == RevealMode.Instant)
                return;

            _replayTime += deltaTime;
            if (_replayTime < _replayInterval)
                return;

            Replay();
        }
    }
}
