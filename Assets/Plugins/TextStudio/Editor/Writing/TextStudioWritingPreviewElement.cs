using System;
using System.IO;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal sealed class TextStudioWritingPreviewElement : VisualElement
    {
        readonly TextStudioWritingStyleAsset _style;
        readonly Image _image;
        readonly Slider _scrub;
        readonly Label _status;
        readonly Button _play;
        TextStudioWritingPreviewRenderer _renderer;
        bool _playing = true;
        double _lastTime;
        float _progress;

        internal TextStudioWritingPreviewElement(TextStudioWritingStyleAsset style)
        {
            _style = style;
            name = "ts-writing-live-preview";
            _image = new Image {scaleMode = ScaleMode.ScaleToFit};
            _image.style.height = 180;
            _image.style.backgroundColor = new Color(.075f, .08f, .09f);
            _play = new Button(() => { _playing = !_playing; _play.text = _playing ? "Pause" : "Play"; })
                {text = "Pause", tooltip = "Play or pause this sample. Preview playback does not change your scene."};
            _scrub = new Slider(0, 1) {showInputField = true, tooltip = "Scrub the complete writing animation from 0 to 1."};
            _scrub.style.flexGrow = 1;
            _scrub.RegisterValueChangedCallback(evt => { _progress = evt.newValue; _playing = false; _play.text = "Play"; Render(); });
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.Add(_play); controls.Add(_scrub);
            _status = TextStudioInspectorElements.CreateMutedText("");
            Add(TextStudioInspectorElements.CreateSection("Preview", "ts-writing-preview-section", _image, controls, _status));
            RegisterCallback<AttachToPanelEvent>(_ => { _lastTime = EditorApplication.timeSinceStartup; });
            RegisterCallback<DetachFromPanelEvent>(_ => { _image.image = null; _renderer?.Dispose(); _renderer = null; });
            schedule.Execute(Tick).Every(33);
        }

        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_playing) _progress = (_progress + (float)Math.Min(.1, now - _lastTime) / 5f) % 1.15f;
            _lastTime = now;
            _scrub.SetValueWithoutNotify(Mathf.Clamp01(_progress));
            Render();
        }

        void Render()
        {
            if (_style == null || panel == null) return;
            try
            {
                _renderer ??= new TextStudioWritingPreviewRenderer(_style);
                _image.image = _renderer.Render(Mathf.Clamp01(_progress));
                _status.text = _renderer.Status;
            }
            catch (Exception exception)
            {
                _playing = false; _play.text = "Play";
                _status.text = "Preview unavailable: " + exception.Message;
                _renderer?.Dispose(); _renderer = null;
            }
        }
    }

    internal sealed class TextStudioWritingPreviewRenderer : IDisposable
    {
        readonly Scene _scene;
        readonly TextStudioWritingStyleAsset _style;
        readonly Camera _camera;
        readonly TextMeshPro _text;
        readonly TextStudioText _hub;
        readonly RenderTexture _target;
        TextStudioFontAppearanceData _temporaryFont;
        ulong _revision = ulong.MaxValue;
        float _renderedProgress = float.NaN;
        internal string Status { get; private set; }

        internal TextStudioWritingPreviewRenderer(TextStudioWritingStyleAsset style)
        {
            _style = style;
            TextStudioText subject = null;
            foreach (TextStudioText candidate in UnityUtils.FindAll<TextStudioText>())
                if (candidate.WritingStyle == style && !EditorSceneManager.IsPreviewScene(candidate.gameObject.scene)) { subject = candidate; break; }
            _scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Writing Preview Camera", HideFlags.HideAndDontSave, typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, _scene);
                _camera = cameraObject.GetComponent<Camera>(); _camera.scene = _scene;
                _camera.orthographic = true; _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.075f, .08f, .09f);
                _camera.transform.position = new Vector3(0, 0, -20);
                _camera.nearClipPlane = .1f; _camera.farClipPlane = 100;
                _camera.enabled = false;
                _target = new RenderTexture(960, 300, 24) {hideFlags = HideFlags.HideAndDontSave};
                _target.Create(); _camera.targetTexture = _target;
                GameObject textObject = EditorUtility.CreateGameObjectWithHideFlags("Writing Preview Text", HideFlags.HideAndDontSave, typeof(TextMeshPro));
                SceneManager.MoveGameObjectToScene(textObject, _scene);
                _text = textObject.GetComponent<TextMeshPro>();
                _text.font = subject != null && subject.TMPText != null ? subject.TMPText.font : TMP_Settings.defaultFontAsset;
                if (_text.font == null) throw new InvalidOperationException("Assign a default TMP font to preview writing.");
                _text.text = "A Little Magic";
                _text.fontSize = 6; _text.alignment = TextAlignmentOptions.Center;
                _text.rectTransform.sizeDelta = new Vector2(100, 10);
                _text.ForceMeshUpdate();
                _hub = textObject.AddComponent<TextStudioText>();
                _hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);
                TextStudioFontAppearanceData data = subject != null ? subject.FindAppearanceFont(_text.font) : null;
                if (data == null)
                {
                    string path = TextStudioFontAppearancePreparation.ResolveSourcePath(_text.font);
                    if (File.Exists(path))
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        _temporaryFont = ScriptableObject.CreateInstance<TextStudioFontAppearanceData>();
                        _temporaryFont.hideFlags = HideFlags.HideAndDontSave;
                        _temporaryFont.Configure(_text.font, bytes, TextStudioFontAppearancePreparation.ResolveFaceIndex(_text.font),
                            TextStudioFontAppearancePreparation.Hash(bytes), Array.Empty<TextStudioPreparedFieldTier>());
                        data = _temporaryFont;
                    }
                }
                if (data != null) _hub.SetAppearanceFont(data);
                Status = data != null ? "Sample uses " + _text.font.name + "." : "Font source unavailable. Pen effects use a simple reveal until font data is prepared.";
                Bounds bounds = _text.textBounds;
                _camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20);
                _camera.aspect = 960f / 300f;
                _camera.orthographicSize = Mathf.Max(.5f, Mathf.Max(bounds.extents.y, bounds.extents.x / _camera.aspect) * 1.3f);
            }
            catch { Dispose(); throw; }
        }

        internal RenderTexture Render(float progress)
        {
            ulong revision = TextStudioWritingCompiler.ComputeDependencyRevision(_style);
            if (_revision == revision && _renderedProgress.Equals(progress)) return _target;
            if (_revision != revision)
            {
                TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
                settings.RevealMode = RevealMode.ByLetter;
                settings.TimingMode = TextStudioTypewriterTimingMode.FinishCurrentUnit;
                settings.WritingEnabled = true;
                settings.WritingStyle = _style;
                _hub.ApplyTypewriterSettings(settings);
                _hub.ForceMeshRefresh(0);
                _revision = revision;
            }
            _hub.Preview.SetSegments(TextStudioPreviewSegment.Reveal);
            _hub.Preview.Scrub(progress);
            _camera.Render();
            _renderedProgress = progress;
            return _target;
        }

        public void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            if (_temporaryFont != null) Object.DestroyImmediate(_temporaryFont);
        }
    }
}
