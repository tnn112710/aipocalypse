using TextStudio.Examples.Production;
using UnityEngine;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UITKButton = UnityEngine.UIElements.Button;

namespace TextStudio.Examples.Whisperwood
{
    public sealed partial class TextStudioWhisperwoodDemo : ITextStudioExternalEffectSource
    {
        [Header("Luma's Clue")]
        [SerializeField] TextStudioKaraokeCueSheet _clueSheet;
        [SerializeField] AudioSource _clueAudio;
        [SerializeField] TextStudioEffectDefinitionAsset _clueHighlight;
        [SerializeField] UIButton _canvasClueButton;
        UITKButton _uitkClueButton;
        float _clueTime;
        int _activeClueWord = -1;

        public bool IsPlayingClue => _mode == DemoMode.Clue;
        public float CluePosition => _clueTime;
        public int ActiveClueWord => _activeClueWord;
        public AudioSource ClueAudio => _clueAudio;
        public TextStudioKaraokeCueSheet ClueSheet => _clueSheet;
        bool ITextStudioExternalEffectSource.IsActive => _mode == DemoMode.Clue && _activeClueWord >= 0;

        public void ConfigureClue(TextStudioKaraokeCueSheet sheet, AudioSource audio, TextStudioEffectDefinitionAsset highlight, UIButton button = null)
        {
            _clueSheet = sheet;
            _clueAudio = audio;
            _clueHighlight = highlight;
            _canvasClueButton = button;
        }

        public void ToggleClue()
        {
            if (_mode == DemoMode.Clue) { ReturnFromClue(); return; }
            if (!Application.isPlaying || _mode != DemoMode.Choice || _dialogueNpcIndex != 1 ||
                _clueSheet == null || _clueAudio == null || _clueSheet.AudioClip == null || _clueSheet.Lines.Count == 0) return;
            _voice?.StopPlayback();
            _mode = DemoMode.Clue;
            _clueTime = 0;
            _activeClueWord = -1;
            SetSpeaker("Luma's three notes");
            ShowDialogueLine(_clueSheet.Lines[0].Text);
            if (_backend == Backend.Canvas)
            {
                _canvasDialogue?.SkipReveal();
                _canvasDialogue?.RegisterExternalEffectSource(this);
            }
            else
            {
                _uitkDialogue?.SkipReveal();
                _uitkDialogue?.RegisterExternalEffectSource(this);
            }
            SetDialogueVisible(true);
            SetHudVisible(true);
            SetPrompt("Three notes to remember the words by.");
            _clueAudio.clip = _clueSheet.AudioClip;
            _clueAudio.mute = !_soundEnabled;
            _clueAudio.Play();
        }

        void UpdateClue()
        {
            if (_mode != DemoMode.Clue || _clueAudio == null) return;
            if (!_clueAudio.isPlaying) { ReturnFromClue(); return; }
            _clueTime = (float)_clueAudio.timeSamples / _clueAudio.clip.frequency;
            _activeClueWord = -1;
            KaraokeLineCue line = _clueSheet.Lines[0];
            for (int i = 0; i < line.Words.Count; i++)
                if (_clueTime >= line.Words[i].StartTime && _clueTime < line.Words[i].EndTime) { _activeClueWord = i; break; }
        }

        void ITextStudioExternalEffectSource.Submit(in TextStudioExternalEffectSubmissionContext context)
        {
            if (_activeClueWord < 0 || _clueHighlight == null) return;
            if (!context.Plan.IsCreated || _activeClueWord >= context.Plan.WordCount) return;
            WordRange word = context.Plan.WordRanges[_activeClueWord];
            KaraokeWordCue cue = _clueSheet.Lines[0].Words[_activeClueWord];
            float progress = Mathf.InverseLerp(cue.StartTime, cue.EndTime, _clueTime);
            context.Submit(_clueHighlight, progress, 1, new TextStudioTextRange(word.glyphStart, word.GlyphEnd));
        }

        void StopCluePlayback()
        {
            if (_clueAudio != null) _clueAudio.Stop();
            _canvasDialogue?.UnregisterExternalEffectSource(this);
            _uitkDialogue?.UnregisterExternalEffectSource(this);
            _activeClueWord = -1;
        }

        void ReturnFromClue()
        {
            StopCluePlayback();
            _mode = DemoMode.Exploring;
            TalkToNpc(1);
            SetHudVisible(true);
        }

        void PauseClueForOverlay()
        {
            if (_mode == DemoMode.Clue && _clueAudio != null) _clueAudio.Pause();
        }

        void ResumeClueFromOverlay()
        {
            if (_mode == DemoMode.Clue && _clueAudio != null)
            {
                _clueAudio.UnPause();
                SetPrompt("Three notes to remember the words by.");
            }
        }

        void SetClueButtonVisible(bool choiceVisible)
        {
            bool visible = _mode == DemoMode.Clue || (choiceVisible && _dialogueNpcIndex == 1);
            SetButtonVisible(_canvasClueButton, _uitkClueButton, visible);
            string label = _mode == DemoMode.Clue ? "Skip clue" : "Listen to the three notes";
            _canvasClueButton?.GetComponentInChildren<TextStudioText>(true)?.ShowTextInstant(label);
            if (_uitkClueButton != null) _uitkClueButton.text = label;
        }

        void BindClueButton()
        {
            _uitkClueButton = _uitkRoot.Q<UITKButton>("clue-button");
            if (_uitkClueButton != null) _uitkClueButton.clicked += ToggleClue;
        }

        void UnbindClueButton()
        {
            if (_uitkClueButton != null) _uitkClueButton.clicked -= ToggleClue;
            _uitkClueButton = null;
        }
    }
}
