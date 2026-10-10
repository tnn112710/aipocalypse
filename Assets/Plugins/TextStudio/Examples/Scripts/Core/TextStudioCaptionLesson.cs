using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>A scene-owned cinematic clock stays independent of native caption accessibility settings.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCaptionLesson : MonoBehaviour
    {
        [Serializable]
        public struct Cue
        {
            public string Speaker;
            [TextArea] public string Text;
            public float Duration;
        }
        [SerializeField] TextStudioText _caption;
        [SerializeField] TMP_Text _speaker, _status, _sizeLabel, _backgroundLabel, _instantLabel, _motionLabel, _widthLabel, _pauseLabel;
        [SerializeField] Image _background;
        [SerializeField] RectTransform _safeArea;
        [SerializeField] RawImage _landscape;
        [SerializeField] Cue[] _cues;
        int _index, _size = 1, _opacity = 2;
        float _elapsed;
        bool _instant, _reduced, _narrow, _paused;
        public TextStudioText Caption => _caption;
        public int CueIndex => _index;
        public float CueElapsed => _elapsed;
        public bool Instant => _instant;
        public bool ReducedMotion => _reduced;
        public Cue CurrentCue => _cues[_index];
        public RectTransform SafeArea => _safeArea;
        public void Configure(TextStudioText caption, TMP_Text speaker, Image background, RectTransform safeArea, RawImage landscape,
            TMP_Text status, TMP_Text size, TMP_Text opacity, TMP_Text instant, TMP_Text motion, TMP_Text width, TMP_Text pause, Cue[] cues)
        {
            _caption = caption; _speaker = speaker; _background = background; _safeArea = safeArea; _landscape = landscape;
            _status = status; _sizeLabel = size; _backgroundLabel = opacity; _instantLabel = instant; _motionLabel = motion; _widthLabel = width; _pauseLabel = pause; _cues = cues;
            ResetLesson();
        }
        void OnEnable() { if (_caption != null) ResetLesson(); }
        void Update()
        {
            if (_caption == null || _paused || !Application.isPlaying) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= CurrentCue.Duration) ShowCue((_index + 1) % _cues.Length);
            // A slow camera pan belongs to the cinematic, not to text reveal.
            float progress = (_index + Mathf.Clamp01(_elapsed / CurrentCue.Duration)) / _cues.Length;
            _landscape.uvRect = new Rect(.15f * progress, 0, .85f, 1);
        }
        public void NextCue() { ShowCue((_index + 1) % _cues.Length); }
        public void RapidReplace() { ShowCue(0); ShowCue(1); ShowCue(2); }
        public void CycleSize() { _size = (_size + 1) % 3; ApplySettings(); }
        public void CycleBackground() { _opacity = (_opacity + 1) % 4; ApplySettings(); }
        public void ToggleWidth() { _narrow = !_narrow; ApplySettings(); }
        public void ToggleMotion() { _reduced = !_reduced; ApplySettings(); }
        public void ToggleInstant()
        {
            _instant = !_instant; ApplySettings();
            if (_instant) _caption.SkipReveal();
        }
        public void TogglePause()
        {
            _paused = !_paused;
            if (_paused) _caption.PauseReveal(); else _caption.ResumeReveal();
            _pauseLabel.SetSampleText(_paused ? "Resume scene" : "Pause scene");
        }
        public void InspectCaptions() { TextStudioLessonActions.Inspect(this); }
        public void ResetLesson()
        {
            _size = 1; _opacity = 2; _instant = _reduced = _narrow = _paused = false;
            _pauseLabel.SetSampleText("Pause scene"); ApplySettings(); ShowCue(0);
        }
        void ShowCue(int index)
        {
            _index = index; _elapsed = 0;
            _speaker.SetSampleText(CurrentCue.Speaker);
            if (_instant || !Application.isPlaying) _caption.ShowTextInstant(CurrentCue.Text);
            else _caption.ShowText(CurrentCue.Text);
            if (_paused) _caption.PauseReveal();
        }
        void ApplySettings()
        {
            // A reader's size is exact: auto-sizing would clamp it back to the authored size, so the protected area grows instead.
            float size = _size == 0 ? 27 : _size == 1 ? 33 : 40;
            TMP_Text text = _caption.TMPText; text.enableAutoSizing = false; text.fontSize = size;
            Color color = _background.color; color.a = _opacity == 0 ? .35f : _opacity == 1 ? .62f : _opacity == 2 ? .86f : 1; _background.color = color;
            _safeArea.anchorMin = new Vector2(_narrow ? .18f : .04f, .035f); _safeArea.anchorMax = new Vector2(_narrow ? .82f : .96f, .035f + .325f * size / 33);
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
            _caption.MotionPolicy = _reduced ? TextStudioMotionPolicy.Reduced : TextStudioMotionPolicy.Inherit;
            _sizeLabel.SetSampleText("Text: " + (_size == 0 ? "small" : _size == 1 ? "standard" : "large"));
            _backgroundLabel.SetSampleText("Background: " + (color.a * 100).ToString("0") + "%");
            _instantLabel.SetSampleText(_instant ? "Instant text" : "Paced text"); _motionLabel.SetSampleText(_reduced ? "Reduced motion" : "System motion");
            _widthLabel.SetSampleText(_narrow ? "Narrow captions" : "Full safe area");
            _status.SetSampleText("Caption settings preserve the cinematic clock. Speaker, offscreen and sound cues stay visible.");
        }
    }
}
