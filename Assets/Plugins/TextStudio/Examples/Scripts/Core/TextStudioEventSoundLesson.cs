using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Observes native events while Reactions choose audio moments and Audio Player owns the clips.</summary>
    [UnityEngine.AddComponentMenu("")]
    [DefaultExecutionOrder(100)]
    public sealed class TextStudioEventSoundLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _subject;
        [SerializeField] TextStudioAudioPlayer _audio;
        [SerializeField] RawImage _gate;
        [SerializeField] Texture2D _closed, _open;
        [SerializeField] TMP_Text _status, _gateLabel, _soundLabel, _glyphLabel, _eventText, _logLabel;
        readonly List<string> _events = new List<string>();
        bool _sound, _glyphs, _logGlyphs;
        int _gateEvents, _bellEvents, _glyphEvents, _starts, _completions, _audioBaseline;
        float _startedAt;
        public TextStudioText Subject => _subject;
        public TextStudioAudioPlayer AudioPlayer => _audio;
        public int GateEvents => _gateEvents;
        public int BellEvents => _bellEvents;
        public int CompletionEvents => _completions;
        public int AudioPlays => _audio.PlaybackCount - _audioBaseline;
        public bool SoundEnabled => _sound;
        public void Configure(TextStudioText subject, TextStudioAudioPlayer audio, RawImage gate, Texture2D closed, Texture2D open,
            TMP_Text status, TMP_Text gateLabel, TMP_Text sound, TMP_Text glyphs, TMP_Text events, TMP_Text log)
        {
            _subject = subject; _audio = audio; _gate = gate; _closed = closed; _open = open; _status = status;
            _gateLabel = gateLabel; _soundLabel = sound; _glyphLabel = glyphs; _eventText = events; _logLabel = log; ResetLesson();
        }
        void OnEnable()
        {
            if (_subject == null) return;
            _subject.OnTypewriterStarted += Started; _subject.OnShowComplete += Completed;
            _subject.OnGlyphRevealed += Glyph; _subject.OnMarkerReached += Marker;
            _subject.OnTypewriterWaitStarted += WaitStarted; _subject.OnTypewriterWaitFinished += WaitFinished;
            ResetLesson();
        }
        void OnDisable()
        {
            if (_subject == null) return;
            _subject.OnTypewriterStarted -= Started; _subject.OnShowComplete -= Completed;
            _subject.OnGlyphRevealed -= Glyph; _subject.OnMarkerReached -= Marker;
            _subject.OnTypewriterWaitStarted -= WaitStarted; _subject.OnTypewriterWaitFinished -= WaitFinished;
        }
        void Update()
        {
            if (_subject == null) return;
            _status.SetSampleText("Native events: " + _starts + " start / " + _glyphEvents + " glyphs / " + _completions + " complete. Audio plays: " + AudioPlays);
        }
        public void ToggleSound() { _sound = !_sound; ApplyAudio(); }
        public void ToggleGlyphAudio() { _glyphs = !_glyphs; ApplyAudio(); }
        public void ToggleGlyphLog() { _logGlyphs = !_logGlyphs; _logLabel.SetSampleText(_logGlyphs ? "Log glyph events" : "Log key events"); }
        public void Advance() { _subject.AdvanceOrSkip(); }
        public void InspectAudio() { TextStudioLessonActions.Inspect(_audio.AudioSet); }
        public void ResetLesson() { _glyphs = _logGlyphs = false; _logLabel.SetSampleText("Log key events"); ApplyAudio(); Replay(); }
        void ApplyAudio()
        {
            // Disabling the native player immediately releases its voices and event subscriptions.
            _audio.PlayGlyphCues = _glyphs; _audio.PlayMarkerCues = true; _audio.enabled = _sound;
            _soundLabel.SetSampleText(_sound ? "Sound on" : "Sound off"); _glyphLabel.SetSampleText(_glyphs ? "Glyph + bell cues" : "Bell cue only");
        }
        public void Replay()
        {
            _gateEvents = _bellEvents = _glyphEvents = _starts = _completions = 0; _events.Clear(); _eventText.SetSampleText("Waiting for native events");
            _gate.texture = _closed; _gateLabel.SetSampleText("Gate closed"); _startedAt = Time.unscaledTime; _audioBaseline = _audio.PlaybackCount;
            _subject.SetText("The lantern wakes.<wait seconds=0.6><event=course_gate,open> The gate opens.<event=bell> Welcome home.");
            if (Application.isPlaying) _subject.StartReveal(); else _subject.ShowTextInstant(_subject.Markup);
        }
        void Record(string value)
        {
            if (_events.Count == 8) _events.RemoveAt(0);
            _events.Add((Time.unscaledTime - _startedAt).ToString("0.00") + "s  " + value);
            _eventText.SetSampleText("Latest native events\n" + string.Join("\n", _events));
        }
        void Started() { _starts++; Record("Reveal started"); }
        void Completed() { _completions++; Record("Reveal completed"); }
        void Glyph(int index, char value) { _glyphEvents++; if (_logGlyphs) Record("Glyph " + index + ": " + value); }
        void WaitStarted(TextStudioTypewriterWaitInfo info) { Record("Wait started: " + info.Kind); }
        void WaitFinished(TextStudioTypewriterWaitInfo info) { Record("Wait ended: " + info.Kind); }
        void Marker(string name, string[] payload)
        {
            Record("Marker " + name + " / " + string.Join(", ", payload ?? System.Array.Empty<string>()));
            if (name != TextStudioBuiltInActionIds.Event || payload == null || payload.Length == 0) return;
            if (payload[0] == "course_gate") { _gateEvents++; _gate.texture = _open; _gateLabel.SetSampleText("Gate marker received: " + _gateEvents); }
            if (payload[0] == "bell") _bellEvents++;
        }
    }
}
