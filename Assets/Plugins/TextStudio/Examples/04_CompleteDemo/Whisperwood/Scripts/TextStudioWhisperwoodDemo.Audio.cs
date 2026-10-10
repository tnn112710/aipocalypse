using UnityEngine;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UI.Button;
using UITKButton = UnityEngine.UIElements.Button;

namespace TextStudio.Examples.Whisperwood
{
    public sealed partial class TextStudioWhisperwoodDemo
    {
        [Header("Dialogue Sound")]
        [SerializeField] TextStudioAudioPlayer _voice;
        [SerializeField] TextStudioReactionAsset _voiceReactions;
        [SerializeField] UIButton _canvasSoundButton;
        [SerializeField] bool _soundEnabled = true;
        UITKButton _uitkSoundButton;

        public bool SoundEnabled => _soundEnabled;
        public TextStudioAudioPlayer Voice => _voice;

        public void ConfigureVoice(TextStudioAudioPlayer voice,
            TextStudioReactionAsset reactions, UIButton soundButton = null)
        {
            _voice = voice;
            _voiceReactions = reactions;
            _canvasSoundButton = soundButton;
            ApplySoundPreference();
        }

        public void ToggleSound()
        {
            _soundEnabled = !_soundEnabled;
            ApplySoundPreference();
        }

        void ApplySoundPreference()
        {
            if (_voice != null) _voice.enabled = Application.isPlaying && _soundEnabled;
            if (_clueAudio != null) _clueAudio.mute = !_soundEnabled;
            string label = _soundEnabled ? "Sound on" : "Sound off";
            _canvasSoundButton?.GetComponentInChildren<TextStudioText>(true)?.ShowTextInstant(label);
            if (_uitkSoundButton != null) _uitkSoundButton.text = label;
        }

        void SetSpeakerVoice(int npcIndex)
        {
            if (_voice == null) return;
            _voice.PitchRange = npcIndex == 1 ? new Vector2(1.18f, 1.28f)
                : npcIndex == 2 ? new Vector2(.78f, .86f) : new Vector2(.96f, 1.04f);
        }

        void HandleCanvasDialogueVoice(TextStudioReactionContext context)
        {
            PlayDialogueVoice(_canvasDialogue, in context);
        }

        void HandleUIToolkitDialogueVoice(TextStudioReactionContext context)
        {
            PlayDialogueVoice(_uitkDialogue, in context);
        }

        void PlayDialogueVoice(ITextStudioReactionUnitTextSource source,
            in TextStudioReactionContext context)
        {
            if (!Application.isPlaying || !_soundEnabled || _voice == null)
                return;
            if (_mode == DemoMode.Choice || _mode == DemoMode.Dialogue)
                _voice.TryPlayReactionCue(source, in context);
        }

        void BindSoundButton()
        {
            _uitkSoundButton = _uitkRoot?.Q<UITKButton>("sound-button");
            if (_uitkSoundButton != null) _uitkSoundButton.clicked += ToggleSound;
        }

        void UnbindSoundButton()
        {
            if (_uitkSoundButton != null) _uitkSoundButton.clicked -= ToggleSound;
            _uitkSoundButton = null;
        }
    }
}
