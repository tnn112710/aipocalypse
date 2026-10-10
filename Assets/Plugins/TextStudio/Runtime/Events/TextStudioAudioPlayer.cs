using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TextStudio
{
    public enum TextStudioAudioPlayerClipSequenceMode
    {
        Sequence,
        Random,
        RandomNoRepeat
    }

    public enum TextStudioAudioPlayerPlaybackMode
    {
        Overlap,
        InterruptPrevious
    }

    public enum TextStudioAudioPlayerSettingsSource
    {
        Inline,
        AudioSet
    }

    public enum TextStudioAudioPlayerCueTrigger
    {
        TypewriterEvents,
        Reactions
    }

    /// <summary>Serializable marker route and clip set used by a Text Studio audio player.</summary>
    [Serializable]
    public sealed class TextStudioAudioPlayerMarkerCue
    {
        [SerializeField] string _routeId;
        [SerializeField] AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField] TextStudioAudioPlayerClipSequenceMode _clipSequenceMode = TextStudioAudioPlayerClipSequenceMode.Sequence;
        [SerializeField, Range(0f, 1f)] float _volume = 0.65f;
        [SerializeField] Vector2 _pitchRange = new Vector2(0.96f, 1.04f);

        int _nextClip;
        int _lastClipIndex = -1;
        uint _randomState = 0x8A53F5A5u;

        public string RouteId
        {
            get => _routeId;
            set => _routeId = value;
        }

        public AudioClip[] Clips
        {
            get => _clips;
            set
            {
                _clips = value ?? Array.Empty<AudioClip>();
                _nextClip = 0;
                _lastClipIndex = -1;
            }
        }

        public TextStudioAudioPlayerClipSequenceMode ClipSequenceMode
        {
            get => _clipSequenceMode;
            set => _clipSequenceMode = value;
        }

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp01(value);
        }

        public Vector2 PitchRange
        {
            get => _pitchRange;
            set => _pitchRange = TextStudioAudioPlayer.NormalizePitchRange(value);
        }

        /// <summary>Reports whether an authored marker resolves to this cue's route identifier.</summary>
        public bool Matches(string markerId, string[] parameters)
        {
            return TextStudioAudioPlayerRules.MarkerRouteMatches(_routeId, markerId, parameters);
        }

        internal bool TryGetNextClip(out AudioClip clip)
        {
            return TextStudioAudioPlayer.TryGetNextClip(
                _clips,
                ref _nextClip,
                _clipSequenceMode,
                ref _randomState,
                ref _lastClipIndex,
                out clip);
        }
    }

    /// <summary>Plays pooled Audio Set cues from Typewriter events, Reactions, or explicit callers.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Text Studio/Events/Audio Player")]
    [HelpURL("https://www.wetzold.com/tools/text-studio")]
    public sealed class TextStudioAudioPlayer : MonoBehaviour
    {
        public const string GlyphCueRoute = "typewriter.glyph";

        [SerializeField] TextStudioText _target;
        [SerializeField] TextStudioAudioPlayerCueTrigger _cueTrigger;
        [SerializeField] TextStudioAudioPlayerSettingsSource _settingsSource = TextStudioAudioPlayerSettingsSource.Inline;
        [SerializeField] TextStudioAudioSet _audioSet;
        [SerializeField] AudioSource _sourceTemplate;
        [SerializeField] AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField] List<TextStudioAudioPlayerMarkerCue> _markerCues = new List<TextStudioAudioPlayerMarkerCue>();
        [SerializeField, Min(1)] int _sourcePoolSize = 4;
        [SerializeField] bool _playGlyphCues = true;
        [SerializeField] bool _playMarkerCues = true;
        [SerializeField] TextStudioAudioPlayerClipSequenceMode _clipSequenceMode = TextStudioAudioPlayerClipSequenceMode.Sequence;
        [SerializeField] TextStudioAudioPlayerPlaybackMode _playbackMode = TextStudioAudioPlayerPlaybackMode.Overlap;
        [SerializeField] bool _ignoreWhitespaceAndPunctuation = true;
        [SerializeField] string _ignoredPunctuation = ".,;:!?";
        [SerializeField, Min(0f)] float _cooldownSeconds = 0.015f;
        [SerializeField] Vector2 _pitchRange = new Vector2(0.96f, 1.04f);
        [SerializeField, Range(0f, 1f)] float _volume = 0.65f;

        readonly List<AudioSource> _activeSources = new List<AudioSource>(8);
        readonly List<AudioSource> _prewarmSources = new List<AudioSource>(8);
        ObjectPool<AudioSource> _sourcePool;
        int _sourcePoolCapacity;
        int _sourceSerial;
        TextStudioText _subscribedTarget;
        TextStudioAudioPlayerCueTrigger _subscribedCueTrigger;
        int _nextClip;
        int _lastClipIndex = -1;
        int _playbackCount;
        float _lastGlyphPlayTime = -999f;
        float _lastMarkerPlayTime = -999f;
        uint _randomState = 0x6C8E9CF5u;

        public TextStudioText Target
        {
            get => ResolveTarget();
            set => SetTarget(value);
        }

        public TextStudioAudioPlayerCueTrigger CueTrigger
        {
            get => _cueTrigger;
            set
            {
                if (_cueTrigger == value)
                    return;
                _cueTrigger = value;
                TextStudioText target = _subscribedTarget;
                if (target != null)
                {
                    UnsubscribeTarget();
                    SubscribeTarget(target);
                }
            }
        }

        public int SourcePoolSize
        {
            get => _sourcePoolSize;
            set
            {
                int size = Mathf.Max(1, value);
                if (_sourcePoolSize == size)
                    return;

                _sourcePoolSize = size;
                DestroySourcePool();
            }
        }

        public int ActiveSourceCount => _sourcePool != null ? _sourcePool.CountAll : 0;
        public int PlaybackCount => _playbackCount;

        public bool PlayGlyphCues
        {
            get => _playGlyphCues;
            set => _playGlyphCues = value;
        }

        public bool PlayMarkerCues
        {
            get => _playMarkerCues;
            set => _playMarkerCues = value;
        }

        public TextStudioAudioPlayerSettingsSource SettingsSource
        {
            get => _settingsSource;
            set
            {
                if (_settingsSource == value)
                    return;

                _settingsSource = value;
                _nextClip = 0;
                _lastClipIndex = -1;
                DestroySourcePool();
            }
        }

        public TextStudioAudioSet AudioSet
        {
            get => _audioSet;
            set
            {
                if (_audioSet == value)
                    return;

                _audioSet = value;
                _nextClip = 0;
                _lastClipIndex = -1;
                DestroySourcePool();
            }
        }

        public AudioSource SourceTemplate
        {
            get => _sourceTemplate;
            set
            {
                if (_sourceTemplate == value)
                    return;

                _sourceTemplate = value;
                DestroySourcePool();
            }
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
            set => _pitchRange = NormalizePitchRange(value);
        }

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp01(value);
        }

        public IReadOnlyList<TextStudioAudioPlayerMarkerCue> MarkerCues => _markerCues;

        internal bool IsSubscribed => _subscribedTarget != null && ShouldMaintainSubscription();

        void Reset()
        {
            if (_target == null)
                _target = GetComponent<TextStudioText>();
        }

        void OnEnable()
        {
            TextStudioText target = ResolveTarget();
            SubscribeTarget(target);

            if (Application.isPlaying)
                PrewarmPool();
        }

        void OnDisable()
        {
            UnsubscribeTarget();
            StopPlayback();
        }

        void OnDestroy()
        {
            DestroySourcePool();
        }

        void OnValidate()
        {
            _sourcePoolSize = Mathf.Max(1, _sourcePoolSize);
            _cooldownSeconds = Mathf.Max(0f, _cooldownSeconds);
            _pitchRange = NormalizePitchRange(_pitchRange);
            _volume = Mathf.Clamp01(_volume);
            if (_ignoredPunctuation == null)
                _ignoredPunctuation = string.Empty;
            if (!enabled)
                UnsubscribeTarget();
            else if (_subscribedTarget != null && _subscribedCueTrigger != _cueTrigger)
                SubscribeTarget(_subscribedTarget);
        }

        /// <summary>Replaces inline glyph clips, removing null entries.</summary>
        public void SetClips(params AudioClip[] clips)
        {
            _clips = clips ?? Array.Empty<AudioClip>();
            _nextClip = 0;
            _lastClipIndex = -1;
        }

        /// <summary>Adds an inline marker route with a cleaned clip set and returns it for further configuration.</summary>
        public TextStudioAudioPlayerMarkerCue AddMarkerCue(string routeId, params AudioClip[] clips)
        {
            TextStudioAudioPlayerMarkerCue cue = new TextStudioAudioPlayerMarkerCue
            {
                RouteId = routeId,
                Clips = clips ?? Array.Empty<AudioClip>(),
                Volume = _volume,
                PitchRange = _pitchRange
            };
            _markerCues.Add(cue);
            return cue;
        }

        /// <summary>Creates the configured number of reusable AudioSource instances.</summary>
        public void PrewarmPool()
        {
            EnsureSourcePool();
        }

        /// <summary>Applies character eligibility and cooldown rules, then plays a glyph cue when possible.</summary>
        public bool TryPlayCharacterCue(char character)
        {
            return TryPlayCharacterCue(character, Time.unscaledTime);
        }

        /// <summary>Resolves an authored marker route and plays a matching configured cue.</summary>
        public bool TryPlayMarkerCue(string markerId, string[] parameters)
        {
            return TryPlayMarkerCue(markerId, parameters, Time.unscaledTime);
        }

        /// <summary>Plays the Audio Set marker cue whose route matches a reaction's Audio Cue field.</summary>
        public bool TryPlayReactionCue(string cueId, float intensity = 1f)
        {
            if (!Application.isPlaying)
                return false;
            return TryPlayReactionCue(cueId, intensity, Time.unscaledTime);
        }

        internal bool TryPlayCharacterCue(char character, float time)
        {
            return TryPlayCharacterCue(character, 1f, time);
        }

        internal bool TryPlayCharacterCue(char character, float intensity, float time)
        {
            if (!_playGlyphCues || float.IsNaN(intensity) || float.IsInfinity(intensity) || intensity <= 0f)
                return false;

            if (!TextStudioAudioPlayerRules.ShouldPlayForCharacter(character, EffectiveIgnoreWhitespaceAndPunctuation, EffectiveIgnoredPunctuation))
                return false;

            if (!CanPlayAtTime(time))
                return false;

            if (!TryGetNextClip(
                    EffectiveGlyphClips,
                    ref _nextClip,
                    EffectiveClipSequenceMode,
                    ref _randomState,
                    ref _lastClipIndex,
                    out AudioClip clip))
                return false;

            return TryPlayClip(clip, EffectiveVolume * Mathf.Clamp01(intensity), EffectivePitchRange, time);
        }

        internal bool TryPlayMarkerCue(string markerId, string[] parameters, float time)
        {
            if (!_playMarkerCues)
                return false;

            if (!CanPlayAtTime(time, true))
                return false;

            IReadOnlyList<TextStudioAudioPlayerMarkerCue> cues = EffectiveMarkerCues;
            for (int i = 0; i < cues.Count; i++)
            {
                TextStudioAudioPlayerMarkerCue cue = cues[i];
                if (cue == null || !cue.Matches(markerId, parameters))
                    continue;

                if (!cue.TryGetNextClip(out AudioClip clip))
                    return false;

                return TryPlayClip(clip, cue.Volume, cue.PitchRange, time, true);
            }

            return false;
        }

        internal bool TryPlayReactionCue(string cueId, float intensity, float time)
        {
            if (!_playMarkerCues || string.IsNullOrEmpty(cueId) ||
                float.IsNaN(intensity) || float.IsInfinity(intensity) || intensity <= 0f ||
                !CanPlayAtTime(time, true))
                return false;

            IReadOnlyList<TextStudioAudioPlayerMarkerCue> cues = EffectiveMarkerCues;
            for (int i = 0; i < cues.Count; i++)
            {
                TextStudioAudioPlayerMarkerCue cue = cues[i];
                if (cue == null || !string.Equals(cue.RouteId, cueId, StringComparison.OrdinalIgnoreCase) ||
                    !cue.TryGetNextClip(out AudioClip clip))
                    continue;
                return TryPlayClip(clip, cue.Volume * Mathf.Clamp01(intensity), cue.PitchRange, time, true);
            }

            return false;
        }

        /// <summary>Plays a cue from a component or UI Toolkit Reaction using this player's clips and pool.</summary>
        public bool TryPlayReactionCue(ITextStudioReactionUnitTextSource source,
            in TextStudioReactionContext context)
        {
            if (!Application.isPlaying)
                return false;
            return TryPlayReactionCue(source, in context, Time.unscaledTime);
        }

        internal bool TryPlayReactionCue(ITextStudioReactionUnitTextSource source,
            in TextStudioReactionContext context, float time)
        {
            if (string.Equals(context.Name, GlyphCueRoute, StringComparison.Ordinal))
            {
                return source != null &&
                       (source is not UnityEngine.Object unitySource || unitySource != null) &&
                       source.TryGetReactionUnitCharacter(in context, out char character) &&
                       TryPlayCharacterCue(character, context.Intensity, time);
            }
            return TryPlayReactionCue(context.Name, context.Intensity, time);
        }

        bool CanPlayAtTime(float time, bool marker = false)
        {
            return time - (marker ? _lastMarkerPlayTime : _lastGlyphPlayTime) >= EffectiveCooldownSeconds;
        }

        internal static bool TryGetNextClip(AudioClip[] clips, ref int cursor, out AudioClip clip)
        {
            uint randomState = 0u;
            int lastClipIndex = -1;
            return TryGetNextClip(
                clips,
                ref cursor,
                TextStudioAudioPlayerClipSequenceMode.Sequence,
                ref randomState,
                ref lastClipIndex,
                out clip);
        }

        internal static bool TryGetNextClip(
            AudioClip[] clips,
            ref int cursor,
            TextStudioAudioPlayerClipSequenceMode sequenceMode,
            ref uint randomState,
            ref int lastClipIndex,
            out AudioClip clip)
        {
            clip = null;
            int count = clips != null ? clips.Length : 0;
            if (count == 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                int index;
                if (sequenceMode == TextStudioAudioPlayerClipSequenceMode.Random ||
                    sequenceMode == TextStudioAudioPlayerClipSequenceMode.RandomNoRepeat)
                {
                    randomState = randomState * 1664525u + 1013904223u;
                    index = (int)((randomState & 0x7FFFFFFFu) % (uint)count);
                    if (sequenceMode == TextStudioAudioPlayerClipSequenceMode.RandomNoRepeat &&
                        count > 1 &&
                        index == lastClipIndex)
                    {
                        continue;
                    }
                }
                else
                {
                    index = cursor % count;
                    cursor = (cursor + 1) % count;
                }

                clip = clips[index];
                if (clip != null)
                {
                    lastClipIndex = index;
                    return true;
                }
            }

            if (sequenceMode == TextStudioAudioPlayerClipSequenceMode.RandomNoRepeat && count > 1)
            {
                for (int i = 0; i < count; i++)
                {
                    int index = (lastClipIndex + 1 + i) % count;
                    clip = clips[index];
                    if (clip != null)
                    {
                        lastClipIndex = index;
                        return true;
                    }
                }
            }

            clip = null;
            return false;
        }

        internal static Vector2 NormalizePitchRange(Vector2 range)
        {
            float min = Mathf.Max(0.01f, range.x);
            float max = Mathf.Max(min, range.y);
            return new Vector2(min, max);
        }

        bool TryPlayClip(AudioClip clip, float volume, Vector2 pitchRange, float time, bool marker = false)
        {
            if (clip == null)
                return false;

            EnsureSourcePool();
            if (_sourcePool == null)
                return false;

            AudioSource source;
            if (EffectivePlaybackMode == TextStudioAudioPlayerPlaybackMode.InterruptPrevious)
            {
                if (_activeSources.Count == 0)
                    _activeSources.Add(_sourcePool.Get());
                source = _activeSources[0];
                source.Stop();
            }
            else
            {
                ReleaseStoppedSources();
                if (_activeSources.Count >= _sourcePoolCapacity)
                {
                    AudioSource oldest = _activeSources[0];
                    _activeSources.RemoveAt(0);
                    _sourcePool.Release(oldest);
                }
                source = _sourcePool.Get();
                _activeSources.Add(source);
            }
            source.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, NextRandom01());
            source.volume = Mathf.Clamp01(volume);
            if (Application.isPlaying)
                source.PlayOneShot(clip);
            if (marker) _lastMarkerPlayTime = time; else _lastGlyphPlayTime = time;
            _playbackCount++;
            return true;
        }

        bool UseAudioSet => _settingsSource == TextStudioAudioPlayerSettingsSource.AudioSet && _audioSet != null;

        AudioClip[] EffectiveGlyphClips => UseAudioSet ? _audioSet.GlyphClips : _clips;

        IReadOnlyList<TextStudioAudioPlayerMarkerCue> EffectiveMarkerCues
            => UseAudioSet ? _audioSet.MarkerCues : _markerCues;

        int EffectiveSourcePoolSize => UseAudioSet ? _audioSet.SourcePoolSize : _sourcePoolSize;
        TextStudioAudioPlayerClipSequenceMode EffectiveClipSequenceMode => UseAudioSet ? _audioSet.ClipSequenceMode : _clipSequenceMode;
        TextStudioAudioPlayerPlaybackMode EffectivePlaybackMode => UseAudioSet ? _audioSet.PlaybackMode : _playbackMode;
        bool EffectiveIgnoreWhitespaceAndPunctuation => UseAudioSet ? _audioSet.IgnoreWhitespaceAndPunctuation : _ignoreWhitespaceAndPunctuation;
        string EffectiveIgnoredPunctuation => UseAudioSet ? _audioSet.IgnoredPunctuation : _ignoredPunctuation;
        float EffectiveCooldownSeconds => UseAudioSet ? _audioSet.CooldownSeconds : _cooldownSeconds;
        Vector2 EffectivePitchRange => UseAudioSet ? _audioSet.PitchRange : _pitchRange;
        float EffectiveVolume => UseAudioSet ? _audioSet.Volume : _volume;

        float NextRandom01()
        {
            _randomState = _randomState * 1664525u + 1013904223u;
            return (_randomState & 0x00FFFFFFu) / 16777215f;
        }

        TextStudioText ResolveTarget()
        {
            if (_target != null)
                return _target;

            SetTarget(GetComponent<TextStudioText>());
            return _target;
        }

        void SetTarget(TextStudioText target)
        {
            if (_target == target)
            {
                if (_subscribedTarget == null && ShouldMaintainSubscription() && _target != null)
                    SubscribeTarget(_target);
                return;
            }

            UnsubscribeTarget();
            _target = target;

            if (ShouldMaintainSubscription() && _target != null)
                SubscribeTarget(_target);
        }

        bool ShouldMaintainSubscription()
        {
            return enabled && gameObject.activeInHierarchy;
        }

        void SubscribeTarget(TextStudioText target)
        {
            if (_subscribedTarget == target && _subscribedCueTrigger == _cueTrigger)
                return;

            UnsubscribeTarget();
            if (target == null)
                return;

            if (_cueTrigger == TextStudioAudioPlayerCueTrigger.Reactions)
                target.OnReactionAudioCue += HandleReactionAudioCue;
            else
            {
                target.OnGlyphRevealed += HandleGlyphRevealed;
                target.OnMarkerReached += HandleMarkerReached;
            }
            _subscribedTarget = target;
            _subscribedCueTrigger = _cueTrigger;
        }

        void UnsubscribeTarget()
        {
            if (_subscribedTarget == null)
                return;

            _subscribedTarget.OnGlyphRevealed -= HandleGlyphRevealed;
            _subscribedTarget.OnMarkerReached -= HandleMarkerReached;
            _subscribedTarget.OnReactionAudioCue -= HandleReactionAudioCue;
            _subscribedTarget = null;
        }

        void HandleGlyphRevealed(int glyphIndex, char character)
        {
            if (ShouldMaintainSubscription())
                TryPlayCharacterCue(character, Time.unscaledTime);
        }

        void HandleMarkerReached(string markerId, string[] parameters)
        {
            if (ShouldMaintainSubscription())
                TryPlayMarkerCue(markerId, parameters, Time.unscaledTime);
        }

        void HandleReactionAudioCue(TextStudioReactionContext context)
        {
            if (!ShouldMaintainSubscription())
                return;
            TryPlayReactionCue(_subscribedTarget, in context, Time.unscaledTime);
        }

        void EnsureSourcePool()
        {
            int size = Mathf.Max(1, EffectiveSourcePoolSize);
            if (_sourcePool != null && _sourcePoolCapacity == size)
                return;

            DestroySourcePool();
            _sourcePoolCapacity = size;
            _sourcePool = new ObjectPool<AudioSource>(
                CreateSource,
                ActivateSource,
                ResetSource,
                DestroySource,
                true,
                size,
                size);
            _prewarmSources.Clear();
            for (int i = 0; i < size; i++)
                _prewarmSources.Add(_sourcePool.Get());
            for (int i = 0; i < _prewarmSources.Count; i++)
                _sourcePool.Release(_prewarmSources[i]);
            _prewarmSources.Clear();
        }

        AudioSource CreateSource()
        {
            _sourceSerial++;
            GameObject child = new GameObject("Audio Player Source " + _sourceSerial);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            ApplySourceTemplate(source);
            child.SetActive(false);
            return source;
        }

        void ActivateSource(AudioSource source)
        {
            if (source == null)
                return;
            ApplySourceTemplate(source);
            source.gameObject.SetActive(true);
        }

        static void ResetSource(AudioSource source)
        {
            if (source == null)
                return;
            source.Stop();
            source.gameObject.SetActive(false);
        }

        static void DestroySource(AudioSource source)
        {
            if (source == null)
                return;
            GameObject sourceObject = source.gameObject;
            if (Application.isPlaying)
                Destroy(sourceObject);
            else
                DestroyImmediate(sourceObject);
        }

        void ReleaseStoppedSources()
        {
            for (int i = _activeSources.Count - 1; i >= 0; i--)
            {
                AudioSource source = _activeSources[i];
                if (source != null && source.isPlaying)
                    continue;
                _activeSources.RemoveAt(i);
                if (source != null)
                    _sourcePool.Release(source);
            }
        }

        void ApplySourceTemplate(AudioSource source)
        {
            AudioSource template = _sourceTemplate;
            if (template != null)
            {
                source.outputAudioMixerGroup = template.outputAudioMixerGroup;
                source.mute = template.mute;
                source.bypassEffects = template.bypassEffects;
                source.bypassListenerEffects = template.bypassListenerEffects;
                source.bypassReverbZones = template.bypassReverbZones;
                source.priority = template.priority;
                source.volume = template.volume;
                source.pitch = template.pitch;
                source.panStereo = template.panStereo;
                source.spatialBlend = template.spatialBlend;
                source.reverbZoneMix = template.reverbZoneMix;
                source.dopplerLevel = template.dopplerLevel;
                source.spread = template.spread;
                source.rolloffMode = template.rolloffMode;
                source.minDistance = template.minDistance;
                source.maxDistance = template.maxDistance;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.playOnAwake = false;
            source.loop = false;
        }

        internal AudioSource GetSourceForDebug(int index)
        {
            if (index < 0)
                return null;

            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            return index < sources.Length ? sources[index] : null;
        }

        /// <summary>Stops current voices and returns them to the reusable pool without disabling future event delivery.</summary>
        public void StopPlayback()
        {
            for (int i = _activeSources.Count - 1; i >= 0; i--)
            {
                AudioSource source = _activeSources[i];
                if (source != null && _sourcePool != null)
                    _sourcePool.Release(source);
            }
            _activeSources.Clear();
        }

        void DestroySourcePool()
        {
            if (_sourcePool == null)
                return;

            StopPlayback();
            _sourcePool.Clear();
            _sourcePool = null;
            _sourcePoolCapacity = 0;
            _sourceSerial = 0;
        }
    }
}
