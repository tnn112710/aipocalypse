using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>Owns game state and notification ordering; Text Studio owns editable text and temporary presentation.</summary>
    [DefaultExecutionOrder(100), UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioHudLesson : MonoBehaviour
    {
        enum NoticeKind { Objective, Pickup, Achievement, Warning }
        struct Notice { public NoticeKind Kind; public string Text; public int Value; }
        [SerializeField] TextStudioText _scoreText, _objective, _prompt, _comboText;
        [SerializeField] TMP_InputField _callsign;
        [SerializeField] RectTransform _comboFill;
        [SerializeField] TextStudioEmitter _notifications;
        [SerializeField] TextStudioEmissionPreset _messagePreset, _pickupPreset, _warningPreset;
        [SerializeField] Transform _noticeAnchor;
        [SerializeField] TMP_Text _queueStatus, _signalStatus, _audioLabel;
        [SerializeField] TextStudioSignalDriver[] _signals;
        [SerializeField] AudioSource _audio;
        [SerializeField] Slider _valueSlider, _tempoSlider;
        readonly List<Notice> _queue = new List<Notice>(8);
        TextStudioEmissionHandle _active;
        Notice _current;
        int _score = 2480, _progress, _combo, _merged, _superseded;
        float _comboRemaining;
        bool _alternatePrompt;
        public int Score => _score;
        public int ObjectiveProgress => _progress;
        public int ComboCount => _combo;
        public float ComboRemaining => _comboRemaining;
        public int QueueCount => _queue.Count;
        public int MergedCount => _merged;
        public TextStudioEmitter Notifications => _notifications;
        public TMP_InputField Callsign => _callsign;
        public TextStudioSignalDriver[] Signals => _signals;
        public AudioSource SignalAudio => _audio;
        public void Configure(TextStudioText score, TextStudioText objective, TextStudioText prompt, TextStudioText combo,
            TMP_InputField callsign, RectTransform comboFill, TextStudioEmitter notifications, TextStudioEmissionPreset message,
            TextStudioEmissionPreset pickup, TextStudioEmissionPreset warning, Transform noticeAnchor, TMP_Text queueStatus,
            TextStudioSignalDriver[] signals, AudioSource audio, TMP_Text signalStatus, TMP_Text audioLabel, Slider valueSlider, Slider tempoSlider)
        {
            _scoreText = score; _objective = objective; _prompt = prompt; _comboText = combo; _callsign = callsign; _comboFill = comboFill;
            _notifications = notifications; _messagePreset = message; _pickupPreset = pickup; _warningPreset = warning; _noticeAnchor = noticeAnchor;
            _valueSlider = valueSlider; _tempoSlider = tempoSlider; _queueStatus = queueStatus; _signals = signals; _audio = audio; _signalStatus = signalStatus; _audioLabel = audioLabel; ResetLesson();
        }
        void OnEnable() { if (_scoreText != null) ResetLesson(); }
        void OnDisable() { if (_notifications != null) _notifications.ReleaseAll(); if (_audio != null) _audio.Stop(); }
        void Update()
        {
            if (_scoreText == null || !Application.isPlaying) return;
            if (_comboRemaining > 0)
            {
                _comboRemaining = Mathf.Max(0, _comboRemaining - Time.unscaledDeltaTime);
                _comboFill.anchorMax = new Vector2(_comboRemaining / 4, 1);
                if (_comboRemaining == 0) { _combo = 0; _comboText.SetText("READY"); }
            }
            if (!_active.IsValid && _queue.Count > 0) ShowNext();
            if (_signalStatus != null && _signalStatus.gameObject.activeInHierarchy)
            {
                _signalStatus.SetSampleText("Value " + (_signals[0].CurrentSignal * 100).ToString("0") + "% / Audio " + (_signals[1].CurrentSignal * 100).ToString("0") + "% / Beat " + (_signals[2].CurrentSignal * 100).ToString("0") + "%");
                _audioLabel.SetSampleText(_audio.isPlaying ? "Stop audio sample" : "Play audio sample");
            }
        }
        public void AddPickup()
        {
            _score += 120; UpdateScore();
            if (_active.IsValid && _current.Kind == NoticeKind.Pickup && _active.SetValue(_current.Value + 120))
            {
                _current.Value += 120; _merged++; RefreshQueue(); return;
            }
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Kind != NoticeKind.Pickup) continue;
                Notice notice = _queue[i]; notice.Value += 120; _queue[i] = notice; _merged++; RefreshQueue(); return;
            }
            Enqueue(new Notice { Kind = NoticeKind.Pickup, Value = 120 });
        }
        public void ComboHit()
        {
            _combo++; _comboRemaining = 4;
            int tier = _combo >= 6 ? 3 : _combo >= 3 ? 2 : 1;
            _score += 20 * tier; UpdateScore(); _comboText.SetText("COMBO " + _combo + " / x" + tier);
        }
        public void ReachRelay()
        {
            if (_progress >= 3) return;
            _progress++;
            _objective.SetText("<sprite name=Relay> " + (_progress == 3 ? "RELAY RESTORED" : "REACH THE RELAY / " + _progress + " OF 3"));
            Enqueue(new Notice { Kind = _progress == 3 ? NoticeKind.Achievement : NoticeKind.Objective, Text = _progress == 3 ? "ACHIEVEMENT / SIGNAL KEEPER" : "WAYPOINT " + _progress + " REACHED" });
        }
        public void ReplacePrompt()
        {
            _alternatePrompt = !_alternatePrompt;
            _prompt.SetText("<sprite name=Energy> " + (_alternatePrompt ? "HOLD E / RESTORE POWER" : "E / CHECK THE RELAY"));
        }
        public void PriorityWarning()
        {
            if (_active.IsValid && _current.Kind == NoticeKind.Warning) { _active.SetText("WARNING / SIGNAL LOST"); return; }
            if (_active.IsValid) { MakeRoom(); _queue.Insert(0, _current); _notifications.ReleaseAll(); }
            MakeRoom(); _queue.Insert(0, new Notice { Kind = NoticeKind.Warning, Text = "WARNING / SIGNAL LOST" }); ShowNext();
        }
        void MakeRoom() { if (_queue.Count < 8) return; _queue.RemoveAt(_queue.Count - 1); _superseded++; }
        void Enqueue(Notice notice) { MakeRoom(); _queue.Add(notice); if (!_active.IsValid) ShowNext(); else RefreshQueue(); }
        void ShowNext()
        {
            if (_queue.Count == 0) return;
            _current = _queue[0]; _queue.RemoveAt(0);
            _active = _current.Kind == NoticeKind.Pickup ? _notifications.EmitValue(_pickupPreset, _current.Value, _noticeAnchor)
                : _notifications.EmitText(_current.Kind == NoticeKind.Warning ? _warningPreset : _messagePreset, _current.Text, _noticeAnchor);
            RefreshQueue();
        }
        public void SetValueSignal(float value) { _signals[0].Value = value; }
        public void SetBeatTempo(float bpm) { _signals[2].BeatsPerMinute = bpm; }
        public void PlaySignalAudio() { if (_audio.isPlaying) _audio.Stop(); else _audio.Play(); }
        public void RestartBeat() { _signals[2].BeatOriginSeconds = AudioSettings.dspTime; }
        public void InspectHud() { TextStudioLessonActions.Inspect(_scoreText); }
        public void ResetLesson()
        {
            _notifications.ReleaseAll(); _active = default; _queue.Clear(); _score = 2480; _progress = _combo = _merged = _superseded = 0; _comboRemaining = 0; _alternatePrompt = false;
            _comboFill.anchorMax = new Vector2(0, 1); _callsign.text = "ROWAN"; _audio.Stop();
            // The input presets reveal changes; seed their unchanged initial values explicitly.
            _callsign.textComponent.GetComponent<TextStudioText>().ShowTextInstant(_callsign.text);
            _scoreText.ShowTextInstant(ScoreMarkup());
            _valueSlider.SetSampleValue(.5f); _tempoSlider.SetSampleValue(120);
            _signals[0].Value = .5f; _signals[2].BeatsPerMinute = 120; RestartBeat();
            _objective.SetText("<sprite name=Relay> REACH THE RELAY / 0 OF 3"); _comboText.SetText("READY"); _prompt.SetText("<sprite name=Energy> E / CHECK THE RELAY"); RefreshQueue();
        }
        void UpdateScore() { _scoreText.SetText(ScoreMarkup()); }
        string ScoreMarkup() => "<sprite name=Credits> " + _score.ToString("N0", CultureInfo.InvariantCulture);
        void RefreshQueue() { _queueStatus.SetSampleText("Waiting " + _queue.Count + " / merged " + _merged + " / superseded " + _superseded + ". Priority preempts the current notice; it resumes afterward."); }
    }
}
