using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Inspector-local controls for the qualified, isolated reaction preview.</summary>
    internal sealed class TextStudioReactionPreviewElement : VisualElement, IDisposable
    {
        readonly TextStudioReactionAsset _asset;
        readonly SerializedObject _serializedObject;
        readonly TextField _before;
        readonly TextField _after;
        readonly IntegerField _seed;
        readonly Slider _intensity;
        readonly Slider _scrub;
        readonly Image _image;
        readonly VisualElement _body;
        readonly HelpBox _status;
        readonly Button _play;
        readonly Button _pause;
        readonly Button _restart;
        readonly Button _stop;
        readonly Button _export;
        TextStudioReactionPreviewFrameRenderer _renderer;
        IVisualElementScheduledItem _schedule;
        double _lastTime;
        string _soloLayerId;
        bool _playing;

        internal string SoloLayerId => _soloLayerId;
        internal bool HasRenderer => _renderer != null;

        internal TextStudioReactionPreviewElement(TextStudioReactionAsset asset,
            SerializedObject serializedObject)
        {
            _asset = asset;
            _serializedObject = serializedObject;
            name = "ts-reaction-preview";
            Foldout foldout = new Foldout { text = "Preview", value = false };
            Add(foldout);
            foldout.Add(TextStudioInspectorElements.CreateMutedText(
                "Text Changed preview for retained outgoing motion. Other reactions run in Play Mode. Preview playback is quiet and does not edit a scene."));
            _body = new VisualElement { name = "ts-reaction-preview-body" };
            foldout.Add(_body);
            _before = new TextField("Before") { name = "ts-reaction-preview-before", value = "AB" };
            _after = new TextField("After") { name = "ts-reaction-preview-after", value = "AC" };
            _seed = new IntegerField("Seed") { name = "ts-reaction-preview-seed", value = (int)asset.Seed };
            _intensity = new Slider("Intensity", 0f, 1f)
            {
                name = "ts-reaction-preview-intensity", value = 1f, showInputField = true
            };
            _body.Add(_before);
            _body.Add(_after);
            _body.Add(_seed);
            _body.Add(_intensity);
            _image = new Image { name = "ts-reaction-preview-image", scaleMode = ScaleMode.ScaleToFit };
            _image.style.height = 190;
            _image.style.minWidth = 120;
            _image.style.display = DisplayStyle.None;
            _body.Add(_image);
            _scrub = new Slider("Time", 0f, 1f)
            {
                name = "ts-reaction-preview-scrub", showInputField = true
            };
            _scrub.RegisterValueChangedCallback(change => Seek(change.newValue));
            _body.Add(_scrub);
            VisualElement controls = new VisualElement { name = "ts-reaction-preview-controls" };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            _play = TextStudioInspectorElements.CreateButton("Play", "Play this isolated preview.", Play);
            _pause = TextStudioInspectorElements.CreateButton("Pause", "Pause at the current time.", Pause);
            _restart = TextStudioInspectorElements.CreateButton("Restart", "Restart at time zero.", Restart);
            _stop = TextStudioInspectorElements.CreateButton("Stop", "Release the preview and its temporary resources.", Stop);
            _export = TextStudioInspectorElements.CreateButton("Export PNG", "Save the current isolated preview frame.", Export);
            controls.Add(_play);
            controls.Add(_pause);
            controls.Add(_restart);
            controls.Add(_stop);
            controls.Add(_export);
            _body.Add(controls);
            _status = new HelpBox("Choose Before and After text, then Play. Only qualified retained motion is previewable here.",
                HelpBoxMessageType.Info) { name = "ts-reaction-preview-status" };
            foldout.Add(_status);
            _before.RegisterValueChangedCallback(_ => Invalidate("Text sample changed. Play to rebuild the preview."));
            _after.RegisterValueChangedCallback(_ => Invalidate("Text sample changed. Play to rebuild the preview."));
            _seed.RegisterValueChangedCallback(_ => Invalidate("Seed changed. Play to rebuild the preview."));
            _intensity.RegisterValueChangedCallback(_ => Invalidate("Intensity changed. Play to rebuild the preview."));
            foldout.TrackSerializedObjectValue(_serializedObject, _ =>
                Invalidate("Reactions changed. Play to rebuild the preview."));
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _lastTime = EditorApplication.timeSinceStartup;
                _schedule = schedule.Execute(Tick).Every(33);
                if (!_playing) _schedule.Pause();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
            RefreshEligibility();
            RefreshControls();
        }

        internal void ToggleSolo(string layerId)
        {
            _soloLayerId = _soloLayerId == layerId ? null : layerId;
            Invalidate(_soloLayerId == null
                ? "Preview Solo cleared. Play to show every enabled reaction."
                : "Preview Solo selected. Play to show only this reaction.");
        }

        internal void Play()
        {
            if (_renderer == null && !TryPrepare()) return;
            _playing = true;
            _lastTime = EditorApplication.timeSinceStartup;
            _schedule?.Resume();
            RefreshControls();
        }

        void Pause()
        {
            _playing = false;
            _schedule?.Pause();
            RefreshControls();
        }

        void Restart()
        {
            if (_renderer == null && !TryPrepare()) return;
            RenderAt(0f);
            _playing = true;
            _lastTime = EditorApplication.timeSinceStartup;
            _schedule?.Resume();
            RefreshControls();
        }

        internal void Stop()
        {
            _playing = false;
            _schedule?.Pause();
            _image.image = null;
            _image.style.display = DisplayStyle.None;
            _renderer?.Dispose();
            _renderer = null;
            _scrub.SetValueWithoutNotify(0f);
            RefreshControls();
        }

        void Invalidate(string message)
        {
            Stop();
            if (!string.IsNullOrEmpty(_soloLayerId))
            {
                bool found = false;
                foreach (TextStudioReactionLayer layer in _asset.Layers)
                    if (layer != null && layer.Enabled && layer.Id == _soloLayerId)
                    {
                        found = true;
                        break;
                    }
                if (!found) _soloLayerId = null;
            }
            _status.text = message;
            _status.messageType = HelpBoxMessageType.Info;
            RefreshEligibility();
        }

        void RefreshEligibility()
        {
            string reason = null;
            bool eligible = _asset != null &&
                TextStudioReactionPreviewSession.CanPreview(_asset.Layers,
                    _soloLayerId, out reason);
            _body.style.display = eligible ? DisplayStyle.Flex : DisplayStyle.None;
            if (eligible) return;
            _status.text = _asset == null ? "The reaction asset is unavailable." : reason;
            _status.messageType = HelpBoxMessageType.Warning;
        }

        bool TryPrepare()
        {
            Stop();
            if (_asset == null) return false;
            uint seed = (uint)Mathf.Max(1, _seed.value);
            if (!TextStudioReactionPreviewSession.TryCreate(_asset.Layers, _asset.Capacity,
                    seed, _before.value, _after.value, TextStudioMotionPolicy.Full,
                    _soloLayerId, _intensity.value, out TextStudioReactionPreviewSession session,
                    out string reason))
            {
                _status.text = reason;
                _status.messageType = HelpBoxMessageType.Warning;
                return false;
            }
            try
            {
                _renderer = new TextStudioReactionPreviewFrameRenderer(session, 640, 280);
                float end = .5f;
                foreach (TextStudioReactionLayer layer in _asset.Layers)
                    if (layer != null && layer.Enabled &&
                        (string.IsNullOrEmpty(_soloLayerId) || layer.Id == _soloLayerId))
                        end = Mathf.Max(end, layer.Delay + layer.Duration + .25f);
                _scrub.highValue = Mathf.Max(.5f, end);
                RenderAt(0f);
                _status.text = "Isolated Text Changed preview. Scrubbing is quiet; the asset and open scene stay unchanged.";
                _status.messageType = HelpBoxMessageType.Info;
                RefreshControls();
                return true;
            }
            catch (Exception exception)
            {
                session.Dispose();
                Stop();
                _status.text = "This preview could not start safely: " + exception.Message;
                _status.messageType = HelpBoxMessageType.Warning;
                return false;
            }
        }

        void Seek(float seconds)
        {
            if (_renderer == null) return;
            Pause();
            RenderAt(seconds);
        }

        void RenderAt(float seconds)
        {
            if (_renderer == null) return;
            try
            {
                float time = Mathf.Clamp(seconds, 0f, _scrub.highValue);
                _renderer.RenderAt(time);
                _scrub.SetValueWithoutNotify(time);
                _image.image = _renderer.Target;
                _image.style.display = DisplayStyle.Flex;
                _image.MarkDirtyRepaint();
            }
            catch (Exception exception)
            {
                Stop();
                _status.text = "Preview stopped after a rendering error: " + exception.Message;
                _status.messageType = HelpBoxMessageType.Error;
            }
        }

        void Tick()
        {
            if (!_playing || _renderer == null) return;
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - _lastTime), 0f, .1f);
            _lastTime = now;
            float next = _scrub.value + delta;
            RenderAt(next);
            if (next >= _scrub.highValue) Pause();
        }

        void Export()
        {
            if (_renderer == null) return;
            try
            {
                ExportPng(EditorUtility.SaveFilePanel("Export Reaction Preview", string.Empty,
                    "Text Studio Reaction Preview", "png"));
            }
            catch (Exception exception)
            {
                Stop();
                _status.text = "Preview export failed: " + exception.Message;
                _status.messageType = HelpBoxMessageType.Error;
            }
        }

        internal void ExportPng(string path)
        {
            if (_renderer == null) return;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    _status.text = "Export canceled. Preview resources released.";
                    _status.messageType = HelpBoxMessageType.Info;
                    return;
                }
                File.WriteAllBytes(path, _renderer.Readback().EncodeToPNG());
                _status.text = "Preview frame exported to " + path;
                _status.messageType = HelpBoxMessageType.Info;
            }
            catch (Exception exception)
            {
                Stop();
                _status.text = "Preview export failed: " + exception.Message;
                _status.messageType = HelpBoxMessageType.Error;
            }
            finally { Stop(); }
        }

        void RefreshControls()
        {
            bool ready = _renderer != null;
            _play.SetEnabled(!_playing);
            _pause.SetEnabled(_playing);
            _restart.SetEnabled(ready);
            _stop.SetEnabled(ready);
            _export.SetEnabled(ready);
            _scrub.SetEnabled(ready);
        }

        public void Dispose()
        {
            _schedule?.Pause();
            _schedule = null;
            Stop();
        }
    }
}
