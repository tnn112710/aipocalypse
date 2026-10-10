using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Native waits, reveal control and semantic markers drive a small gate encounter.</summary>
    [UnityEngine.AddComponentMenu("")]
    // Native text hosts must finish OnEnable before this controller starts their playback.
    [DefaultExecutionOrder(100)]
    public sealed class TextStudioBeatControlLesson : MonoBehaviour
    {
        [SerializeField] TextStudioText _subject;
        [SerializeField] RawImage _gate;
        [SerializeField] Texture2D _closed, _open;
        [SerializeField] TMP_Text _modeLabel, _status, _gateLabel, _pauseLabel, _finalLabel, _markup;
        int _mode, _gateEvents, _advances;
        bool _finalPause = true, _waiting, _complete;
        TextStudioTypewriterWaitKind _waitKind;
        public TextStudioText Subject => _subject;
        public int ModeIndex => _mode;
        public int GateEvents => _gateEvents;
        public bool IsWaiting => _waiting;
        public TextStudioTypewriterWaitKind WaitKind => _waitKind;
        public bool Complete => _complete;
        string Source => _mode == 0 ? "A short pause<wait seconds=1> lets the lantern warm up. Ready?<waitForInput input=submit><event=course_gate,open> The path is clear. Come through!"
            : _mode == 1 ? "Ready to open the gate?<waitForInput input=submit><event=course_gate,open> Come through."
            : _mode == 2 ? "<speed multiplier=0.5>Listen carefully. <speed multiplier=2>The lantern is calling.<event=course_gate,open>"
            : "<event=course_gate,open>The gate is open. Take a breath.";
        public void Configure(TextStudioText subject, RawImage gate, Texture2D closed, Texture2D open, TMP_Text mode,
            TMP_Text status, TMP_Text gateLabel, TMP_Text pause, TMP_Text final, TMP_Text markup)
        {
            _subject = subject; _gate = gate; _closed = closed; _open = open; _modeLabel = mode; _status = status;
            _gateLabel = gateLabel; _pauseLabel = pause; _finalLabel = final; _markup = markup; ResetLesson();
        }
        void OnEnable()
        {
            if (_subject == null) return;
            _subject.OnMarkerReached += OnMarker; _subject.OnTypewriterWaitStarted += OnWaitStarted;
            _subject.OnTypewriterWaitFinished += OnWaitFinished; _subject.OnShowComplete += OnComplete;
            ResetLesson();
        }
        void OnDisable()
        {
            if (_subject == null) return;
            _subject.OnMarkerReached -= OnMarker; _subject.OnTypewriterWaitStarted -= OnWaitStarted;
            _subject.OnTypewriterWaitFinished -= OnWaitFinished; _subject.OnShowComplete -= OnComplete;
        }
        void Update()
        {
            if (_subject == null) return;
            _pauseLabel.SetSampleText(_subject.IsRevealPaused ? "Resume" : "Pause");
            _status.SetSampleText(_waiting ? _waitKind == TextStudioTypewriterWaitKind.Input ? "Waiting for input. Advance releases this checkpoint." : "Timed wait. Skip wait releases it early."
                : _complete ? "Beat complete. Advance requests: " + _advances
                : _subject.IsRevealPaused ? "Reveal paused. Resume when ready."
                : _subject.RevealedGlyphCount == _subject.TotalGlyphCount ? "All letters are visible; the final reading pause is still running."
                : "Revealing at " + _subject.RevealSpeedMultiplier.ToString("0.0") + "x speed");
        }
        public void NextMode() { _mode = (_mode + 1) % 4; Replay(); }
        public void ToggleFinalPause() { _finalPause = !_finalPause; Replay(); }
        public void Advance() { if (_subject.AdvanceOrSkip()) _advances++; }
        public void SkipWait() { _subject.SkipCurrentWait(); }
        public void TogglePause() { if (_subject.IsRevealPaused) _subject.ResumeReveal(); else _subject.PauseReveal(); }
        public void ResetLesson() { _mode = 0; _finalPause = true; Replay(); }
        public void InspectText() { TextStudioLessonActions.Inspect(_subject); }
        public void Replay()
        {
            _gateEvents = _advances = 0; _waiting = _complete = false; _gate.texture = _closed; _gateLabel.SetSampleText("Gate closed / awaiting its marker");
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = RevealMode.ByGlyph; settings.BuiltInTimingsPreset = RevealTimingsPreset.Expressive;
            settings.RevealAppearanceDuration = .12f; settings.WaitAfterFinalReveal = _finalPause; settings.ResetSpeedOnNewText = true; settings.TriggerRemainingMarkersOnSkip = true;
            _subject.ApplyTypewriterSettings(settings); _subject.SetText(Source);
            _modeLabel.SetSampleText(_mode == 0 ? "Timed wait" : _mode == 1 ? "Wait for input" : _mode == 2 ? "Speed changes" : "Final reading pause");
            _finalLabel.SetSampleText(_finalPause ? "Final pause on" : "Final pause off"); _markup.SetSampleText(Source);
            if (Application.isPlaying) _subject.StartReveal();
            else _subject.ShowTextInstant(Source);
        }
        void OnMarker(string name, string[] payload)
        {
            if (name != TextStudioBuiltInActionIds.Event || payload == null || payload.Length == 0 || payload[0] != "course_gate") return;
            _gateEvents++; _gate.texture = _open; _gateLabel.SetSampleText("Gate opened by native marker / events: " + _gateEvents);
        }
        void OnWaitStarted(TextStudioTypewriterWaitInfo info) { _waiting = true; _waitKind = info.Kind; }
        void OnWaitFinished(TextStudioTypewriterWaitInfo info) { _waiting = false; }
        void OnComplete() { _complete = true; }
    }
}
