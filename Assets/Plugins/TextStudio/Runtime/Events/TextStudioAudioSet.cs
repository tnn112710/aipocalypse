using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Reusable glyph clip pool and marker-cue routing asset for Text Studio audio players.</summary>
    [CreateAssetMenu(
        fileName = "TextStudioAudioSet",
        menuName = "Text Studio/Animation/Typewriter Audio Set",
        order = 105)]
    public sealed class TextStudioAudioSet : ScriptableObject
    {
        [SerializeField] AudioClip[] _glyphClips = Array.Empty<AudioClip>();
        [SerializeField] List<TextStudioAudioPlayerMarkerCue> _markerCues = new List<TextStudioAudioPlayerMarkerCue>();
        [SerializeField, Min(1)] int _sourcePoolSize = 4;
        [SerializeField] TextStudioAudioPlayerClipSequenceMode _clipSequenceMode = TextStudioAudioPlayerClipSequenceMode.Sequence;
        [SerializeField] TextStudioAudioPlayerPlaybackMode _playbackMode = TextStudioAudioPlayerPlaybackMode.Overlap;
        [SerializeField] bool _ignoreWhitespaceAndPunctuation = true;
        [SerializeField] string _ignoredPunctuation = ".,;:!?";
        [SerializeField, Min(0f)] float _cooldownSeconds = 0.015f;
        [SerializeField] Vector2 _pitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField, Range(0f, 1f)] float _volume = 0.65f;

        public AudioClip[] GlyphClips
        {
            get => _glyphClips;
            set => _glyphClips = value ?? Array.Empty<AudioClip>();
        }

        public IReadOnlyList<TextStudioAudioPlayerMarkerCue> MarkerCues => _markerCues;

        public int SourcePoolSize
        {
            get => _sourcePoolSize;
            set => _sourcePoolSize = Mathf.Max(1, value);
        }

        public TextStudioAudioPlayerClipSequenceMode ClipSequenceMode
        {
            get => _clipSequenceMode;
            set => _clipSequenceMode = value;
        }

        public TextStudioAudioPlayerPlaybackMode PlaybackMode
        {
            get => _playbackMode;
            set => _playbackMode = value;
        }

        public bool IgnoreWhitespaceAndPunctuation
        {
            get => _ignoreWhitespaceAndPunctuation;
            set => _ignoreWhitespaceAndPunctuation = value;
        }

        public string IgnoredPunctuation
        {
            get => _ignoredPunctuation;
            set => _ignoredPunctuation = value ?? string.Empty;
        }

        public float CooldownSeconds
        {
            get => _cooldownSeconds;
            set => _cooldownSeconds = Mathf.Max(0f, value);
        }

        public Vector2 PitchRange
        {
            get => _pitchRange;
            set => _pitchRange = TextStudioAudioPlayer.NormalizePitchRange(value);
        }

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp01(value);
        }

        /// <summary>Replaces glyph clips, discarding null entries.</summary>
        public void SetGlyphClips(params AudioClip[] clips)
        {
            _glyphClips = clips ?? Array.Empty<AudioClip>();
        }

        /// <summary>Adds a marker cue with a cleaned clip set and returns it for further configuration.</summary>
        public TextStudioAudioPlayerMarkerCue AddMarkerCue(string routeId, params AudioClip[] clips)
        {
            TextStudioAudioPlayerMarkerCue cue = new TextStudioAudioPlayerMarkerCue
            {
                RouteId = routeId,
                Clips = clips ?? Array.Empty<AudioClip>(),
                Volume = _volume,
                PitchRange = _pitchRange,
                ClipSequenceMode = _clipSequenceMode
            };
            _markerCues.Add(cue);
            return cue;
        }

        void OnValidate()
        {
            _sourcePoolSize = Mathf.Max(1, _sourcePoolSize);
            _cooldownSeconds = Mathf.Max(0f, _cooldownSeconds);
            _pitchRange = TextStudioAudioPlayer.NormalizePitchRange(_pitchRange);
            _volume = Mathf.Clamp01(_volume);
            if (_glyphClips == null)
                _glyphClips = Array.Empty<AudioClip>();
            if (_markerCues == null)
                _markerCues = new List<TextStudioAudioPlayerMarkerCue>();
            if (_ignoredPunctuation == null)
                _ignoredPunctuation = string.Empty;
        }
    }
}
