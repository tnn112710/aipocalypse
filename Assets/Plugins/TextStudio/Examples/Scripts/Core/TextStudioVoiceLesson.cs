using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Auditions native Audio Sets using a session copy for cadence changes, preserving the packaged profiles.</summary>
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioVoiceLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _subject;
        [SerializeField] TextStudioAudioPlayer _audio;
        [SerializeField] TextStudioAudioSet[] _profiles;
        [SerializeField] Image[] _buttons;
        [SerializeField] TMP_Text _speaker, _status, _textLabel, _markerLabel, _soundLabel, _cadenceLabel;
        [SerializeField] Slider _cadence;
        TextStudioAudioSet _session;
        int _selected, _baseline, _lastDisplayedCount = -1;
        bool _sameText = true, _marker = true, _sound;
        public TextStudioText Subject => _subject;
        public TextStudioAudioSet SelectedProfile => _profiles[_selected];
        public TextStudioAudioSet SessionProfile => _session;
        public TextStudioAudioPlayer AudioPlayer => _audio;
        public int SelectedIndex => _selected;
        public bool SoundEnabled => _sound;
        public void Configure(TextStudioText subject, TextStudioAudioPlayer audio, TextStudioAudioSet[] profiles, Image[] buttons,
            TMP_Text speaker, TMP_Text status, TMP_Text text, TMP_Text marker, TMP_Text sound, TMP_Text cadenceLabel, Slider cadence)
        {
            _subject = subject; _audio = audio; _profiles = profiles; _buttons = buttons; _speaker = speaker; _status = status;
            _textLabel = text; _markerLabel = marker; _soundLabel = sound; _cadenceLabel = cadenceLabel; _cadence = cadence; ResetLesson();
        }
        void OnEnable() { if (_subject != null) ResetLesson(); }
        void OnDisable()
        {
            if (_audio != null) { _audio.enabled = false; _audio.AudioSet = SelectedProfile; }
            ReleaseSession();
        }
        void ReleaseSession()
        {
            if (_session == null) return;
            if (Application.isPlaying) Destroy(_session); else DestroyImmediate(_session); _session = null;
        }
        void Update()
        {
            if (_subject == null || _session == null) return;
            int count = _audio.PlaybackCount - _baseline;
            if (count == _lastDisplayedCount) return;
            _lastDisplayedCount = count;
            _status.SetSampleText((_sound ? "Auditioning" : "Muted") + " / native audio plays: " + (_audio.PlaybackCount - _baseline) + " / minimum cue interval: " + (_session.CooldownSeconds * 1000).ToString("0") + " ms");
        }
        public void Classic() { Select(0); }
        public void Human() { Select(1); }
        public void Terminal() { Select(2); }
        public void ToggleText() { _sameText = !_sameText; Replay(); }
        public void ToggleMarker() { _marker = !_marker; Replay(); }
        // Turning sound on auditions the current voice at once, so the toggle alone demonstrates the cue set.
        public void ToggleSound() { _sound = !_sound; ApplySound(); if (_sound) Replay(); }
        public void Audition() { _sound = true; ApplySound(); Replay(); }
        public void ResetLesson() { _sameText = _marker = true; Select(0); }
        public void InspectProfile() { TextStudioLessonActions.Inspect(SelectedProfile); }
        public void SetCadence(float seconds)
        {
            _lastDisplayedCount = -1;
            if (_session != null) _session.CooldownSeconds = seconds; _cadence.SetSampleValue(seconds); _cadenceLabel.SetSampleText("Cue interval " + (seconds * 1000).ToString("0") + " ms");
        }
        void ApplySound() { _lastDisplayedCount = -1; _audio.enabled = _sound; _soundLabel.SetSampleText(_sound ? "Sound on" : "Sound off"); }
        void Select(int index)
        {
            _selected = index;
            TextStudioAudioSet previous = _session;
            _session = Application.isPlaying ? Instantiate(SelectedProfile) : null;
            if (_session != null) { _session.name = SelectedProfile.name + " / session cadence"; _session.hideFlags = HideFlags.DontSave; }
            _audio.SettingsSource = TextStudioAudioPlayerSettingsSource.AudioSet; _audio.AudioSet = _session != null ? _session : SelectedProfile;
            if (previous != null) { if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous); }
            SetCadence(SelectedProfile.CooldownSeconds);
            for (int i = 0; i < _buttons.Length; i++) _buttons[i].color = i == _selected ? new Color(.96f, .84f, .61f) : new Color(.89f, .91f, .85f);
            _speaker.SetSampleText(_selected == 0 ? "ROWAN / CLASSIC TYPEWRITER" : _selected == 1 ? "MIRA / HUMAN DIALOGUE" : "RELAY / SCI-FI TERMINAL");
            ApplySound(); Replay();
        }
        public void Replay()
        {
            _baseline = _audio.PlaybackCount; _lastDisplayedCount = -1;
            _subject.BuiltInTimingsPreset = _selected == 0 ? RevealTimingsPreset.Typist : _selected == 1 ? RevealTimingsPreset.Expressive : RevealTimingsPreset.Staccato;
            string text = _sameText ? "The gate opens at dawn." : _selected == 0 ? "The carriage returns at dawn." : _selected == 1 ? "A softer line lands naturally." : "SIGNAL LOCKED. ACCESS GRANTED.";
            string route = _selected == 0 ? "bell" : _selected == 1 ? "choice" : "alert";
            _subject.SetText(text + (_marker ? "<event=" + route + ">" : ""));
            _textLabel.SetSampleText(_sameText ? "Same line" : "Character line"); _markerLabel.SetSampleText(_marker ? "Marker: " + route : "No marker cue");
            if (Application.isPlaying) _subject.StartReveal(); else _subject.ShowTextInstant(_subject.Markup);
        }
    }
}
