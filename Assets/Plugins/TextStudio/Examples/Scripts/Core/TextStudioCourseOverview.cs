using System;
using ImpossibleRobert.Common.Timing;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TextStudio.Examples.Tutorials
{
    /// <summary>The entry scene owns its unattended story; a native reveal marker owns the gate consequence.</summary>
    [ExecuteAlways]
    [UnityEngine.AddComponentMenu("")]
    public sealed class TextStudioCourseOverview : MonoBehaviour
    {
        [SerializeField] TextStudioText _writing;
        [SerializeField] TextStudioText _story;
        [SerializeField] RawImage _gate;
        [SerializeField] Texture _closedGate;
        [SerializeField] Texture _openGate;
        [SerializeField] TMP_Text _feedback;
        [SerializeField] TMP_Text _soundLabel;
        [SerializeField] TMP_Text _replayLabel;
        [SerializeField] GameObject _reward;
        [SerializeField] GameObject _lyric;
        [SerializeField] AudioSource[] _audio = Array.Empty<AudioSource>();
        [SerializeField] AudioSource _gateCue;
        [SerializeField] UnityEvent _startLyric = new UnityEvent();
        [SerializeField] UnityEvent _compareLyric = new UnityEvent();
        double _lastHostTime;
        float _elapsed;
        int _phase = -1;
        bool _tour;
        bool _sound;
        bool _gateOpened;

        public float Elapsed => _elapsed;
        public bool IsTourRunning => _tour;
        public bool GateOpened => _gateOpened;
        public UnityEvent StartLyric => _startLyric;
        public UnityEvent CompareLyric => _compareLyric;

        public void Configure(TextStudioText writing, TextStudioText story, RawImage gate, Texture closedGate, Texture openGate,
            TMP_Text feedback, TMP_Text soundLabel, TMP_Text replayLabel, GameObject reward, GameObject lyric,
            AudioSource[] audio, AudioSource gateCue)
        {
            _writing = writing; _story = story; _gate = gate; _closedGate = closedGate; _openGate = openGate;
            _feedback = feedback; _soundLabel = soundLabel; _replayLabel = replayLabel; _reward = reward; _lyric = lyric;
            _audio = audio; _gateCue = gateCue;
            _story.SetRunActionsInPreview(false);
            ApplySound();
        }

        void OnEnable()
        {
            if (_story != null) _story.OnMarkerReached += OnMarker;
            if (_writing != null && _story != null) Replay();
        }

        void OnDisable()
        {
            if (_story != null) _story.OnMarkerReached -= OnMarker;
            foreach (AudioSource source in _audio) if (source != null) source.Stop();
        }

        void Update()
        {
            if (!_tour) return;
            _elapsed = Mathf.Min(40, _elapsed + HostTime.GetDeltaTime(ref _lastHostTime, clockMode: ClockMode.Unscaled));
            if (_writing != null && _elapsed < 8) _writing.SetRevealProgress(Mathf.Clamp01(_elapsed / 5));
            int phase = _elapsed < 8 ? 0 : _elapsed < 18 ? 1 : _elapsed < 21 ? 2 : _elapsed < 24.5f ? 3 : _elapsed < 28 ? 4 : 5;
            if (phase != _phase) { _phase = phase; EnterPhase(phase); }
            if (_elapsed >= 40) TakeOver();
            else HostTime.RequestEditorFrame(this, repaintAllViews: false);
        }

        public void Replay()
        {
            foreach (AudioSource source in _audio) if (source != null) source.Stop();
            HostTime.Reset(ref _lastHostTime, ClockMode.Unscaled);
            _elapsed = 0; _phase = -1; _tour = true; _gateOpened = false;
            if (_gate != null) _gate.texture = _closedGate;
            if (_reward != null) _reward.SetActive(false);
            if (_lyric != null) _lyric.SetActive(false);
            if (_story != null) { _story.gameObject.SetActive(false); _story.RevealMode = RevealMode.Instant; _story.SetText("Follow a word. Change the world."); }
            if (_writing != null) { _writing.gameObject.SetActive(true); _writing.SetRevealProgress(0); }
            if (_replayLabel != null) _replayLabel.text = "Stop overview";
            ApplySound();
        }

        public void ToggleOverview() { if (_tour) TakeOver(); else Replay(); }

        public void TakeOver()
        {
            _tour = false;
            if (_story != null) _story.PauseReveal();
            foreach (AudioSource source in _audio) if (source != null) source.Pause();
            if (_replayLabel != null) _replayLabel.text = "Replay overview";
        }

        public void ToggleSound() { _sound = !_sound; ApplySound(); }

        void ApplySound()
        {
            foreach (AudioSource source in _audio) if (source != null) source.mute = !_sound;
            if (_soundLabel != null) _soundLabel.text = _sound ? "Sound on" : "Sound off";
        }


        void EnterPhase(int phase)
        {
            if (phase == 0) _feedback.text = "A short introduction to writing, dialogue and animated text.";
            else if (phase == 1)
            {
                _writing.gameObject.SetActive(false);
                _story.gameObject.SetActive(true);
                _feedback.text = "EMBER opens a path through the forest.";
                _story.RevealMode = RevealMode.ByGlyph;
                _story.ShowText("Rowan: Say <color=#F2C978>EMBER</color>, and the path is yours.<event=course_gate>");
            }
            else if (phase == 2)
            {
                _story.gameObject.SetActive(false); _reward.SetActive(true);
                _feedback.text = "A new word, earned for the journey.";
            }
            else if (phase == 3)
            {
                _reward.SetActive(false); _lyric.SetActive(true);
                if (Application.isPlaying) _startLyric.Invoke();
                _feedback.text = Application.isPlaying
                    ? "Follow the same words with a progressive highlight."
                    : "Enter Play Mode to hear the song and follow its lyrics.";
            }
            else if (phase == 4)
            {
                if (Application.isPlaying)
                {
                    _compareLyric.Invoke();
                    _feedback.text = "Now compare a paint-on sweep on the same song.";
                }
            }
            else if (phase == 5)
            {
                _lyric.SetActive(false); _story.gameObject.SetActive(true); _story.RevealMode = RevealMode.Instant;
                _story.SetText("Your words. Your world.");
                _feedback.text = "Open any sample scene in the Project window to explore its setup.";
            }
        }

        void OnMarker(string id, string[] parameters)
        {
            if (id != TextStudioBuiltInActionIds.Event || parameters == null || Array.IndexOf(parameters, "course_gate") < 0 || _gateOpened) return;
            _gateOpened = true;
            if (_gate != null) _gate.texture = _openGate;
            if (Application.isPlaying && _gateCue != null) _gateCue.Play();
        }
    }
}
