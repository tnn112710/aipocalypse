//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DistantLands.Cozy
{
    [CozyModule("ReSound", "ReSound", "Manage music using COZY's weather, ambience, time, biomes, and temporary gameplay overrides with optimized pooling and mixing.", "Audio", true)]
    public class CozyResoundModule : CozyStateMachineModule<ResoundTrack>
    {
        [SerializeField]
        private CozyResoundDJ dj;
        public CozyResoundDJ DJ => dj;

        private bool playing = true;
        public bool Playing { get => Application.isPlaying && playing; set => playing = value; }

        [SerializeField, Tooltip("All ReSound music, including gameplay overrides, is routed through this Audio Mixer group.")]
        private AudioMixerGroup mixerGroup;
        public AudioMixerGroup MixerGroup => mixerGroup;

        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateMixerVolumes();
            }
        }

        [SerializeField]
        private Transform resoundParent;

        [SerializeField]
        private bool allowRepeats;
        public bool AllowRepeats => allowRepeats;

        [SerializeField]
        private bool useSilence;
        public bool UseSilence => useSilence;

        [SerializeField]
        private float minSilence;
        public float MinSilence => minSilence;

        [SerializeField]
        private float maxSilence;
        public float MaxSilence => maxSilence;

        public enum InitializationBehavior
        {
            StartWithRandomSong,
            StartWithInitialSong
        }
        public InitializationBehavior initializationBehavior;

        [SerializeField]
        private ResoundTrack initialTrack;
        public ResoundTrack InitialTrack => initialTrack;

        public override List<ResoundTrack> GetAllProfiles()
        {
            var profiles = dj != null && dj.tracks != null ? new List<ResoundTrack>(dj.tracks) : new List<ResoundTrack>();
            if (initialTrack) profiles.Add(initialTrack);
            return profiles;
        }

        /// <summary>
        /// One playing (or fading) track. Each voice owns its source and its own fade routine,
        /// so interrupting one transition can never orphan another voice's source.
        /// </summary>
        private class Voice
        {
            public ResoundTrack track;
            public AudioSource source;
            public Coroutine routine;
            public bool fadingOut;
        }

        private readonly List<Voice> voices = new();
        private readonly Queue<AudioSource> audioSourcePool = new();
        private readonly List<TrackOverrideRequest> trackOverrides = new();
        private long nextTrackOverrideId;
        private long nextTrackOverrideOrder;
        private bool audioPoolInitialized;
        private ResoundTrack activeTrack;
        private ResoundTrack baseTrack;
        ConditionSnapshot conditionSnapshot;

        private sealed class TrackOverrideRequest
        {
            public long id;
            public long order;
            public int priority;
            public ResoundTrack track;
            public bool hasCustomTransitions;
            public SongTransitionType transitionOnStart;
            public SongTransitionType transitionOnEnd;
        }

        /// <summary>
        /// A disposable request to temporarily play a track over the normal COZY-selected soundtrack.
        /// </summary>
        public sealed class TrackOverrideHandle : System.IDisposable
        {
            private CozyResoundModule module;
            private readonly long id;

            internal TrackOverrideHandle(CozyResoundModule module, long id)
            {
                this.module = module;
                this.id = id;
            }

            /// <summary>
            /// Releases this request. Calling this more than once has no effect.
            /// </summary>
            public void Dispose()
            {
                CozyResoundModule owner = module;
                module = null;
                if (owner != null)
                    owner.ReleaseTrackOverride(id);
            }
        }

        /// <summary>
        /// The track currently selected for playback, including any active gameplay override.
        /// </summary>
        public ResoundTrack CurrentTrack => GetEffectiveTrack();

        [SerializeField]
        private bool skipTrackIfChanceIsZero = false;
        public bool SkipTrackIfChanceIsZero => skipTrackIfChanceIsZero;

        public enum SongTransitionType
        {
            Crossfade,
            FadeOutThenPlayInstant,
            FadeOutThenFadeIn,
            InstantCut,
            PlayAfterCurrentTrack
        }

        [SerializeField]
        private SongTransitionType transitionType = SongTransitionType.Crossfade;
        public SongTransitionType TransitionType => transitionType;

        private Coroutine transitionRoutine;


        /// <summary>Playback position and clip length (seconds) of the current track, if it has an active source.</summary>
        public bool TryGetPlayback(out float time, out float length)
        {
            time = 0f;
            length = 0f;

            if (!Application.isPlaying || CurrentTrack == null) return false;

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice v = voices[i];
                if (v.fadingOut || v.track != CurrentTrack) continue;
                if (v.source == null || v.source.clip == null) continue;

                time = v.source.time;
                length = v.source.clip.length;
                return true;
            }

            return false;
        }

        public override bool UpdateStateMachineTimer => true;

        // Use the state machine's existing minute-per-second scale independently of COZY time.
        protected override float StateMachineDeltaTime => Time.deltaTime / 1440f * TimerSpeedMultiplier;

        public override void InitializeModule()
        {
            base.InitializeModule();

            if (!Application.isPlaying)
                return;

            SetupParent();
            InitializeAudioPool();

            OnProfileChange += HandleProfileChange;
            baseTrack = CurrentProfile;
            TransitionToEffectiveTrack();
        }

        private void SetupParent()
        {
            if (resoundParent != null) return;

            var existingParent = GameObject.Find("ReSound Parent");
            if (existingParent != null)
            {
                resoundParent = existingParent.transform;
                return;
            }

            GameObject parentObj = new GameObject("ReSound Parent");
            resoundParent = parentObj.transform;
            if (WeatherSphere != null)
                parentObj.transform.SetParent(WeatherSphere.transform);
        }

        private void InitializeAudioPool()
        {
            if (audioPoolInitialized) return;

            for (int i = 0; i < 3; i++)
            {
                audioSourcePool.Enqueue(CreatePooledSource());
            }

            audioPoolInitialized = true;
        }

        private AudioSource CreatePooledSource()
        {
            GameObject sourceObj = new GameObject("ReSound_AudioSource");
            sourceObj.transform.SetParent(resoundParent);
            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = mixerGroup;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sourceObj.SetActive(false);
            return source;
        }

        private AudioSource GetAudioSource()
        {
            AudioSource source = null;
            while (source == null && audioSourcePool.Count > 0)
                source = audioSourcePool.Dequeue();

            if (source == null)
                source = CreatePooledSource();

            source.outputAudioMixerGroup = mixerGroup;
            source.gameObject.SetActive(true);
            return source;
        }

        private void ReturnAudioSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.gameObject.SetActive(false);
            audioSourcePool.Enqueue(source);
        }

        /// <summary>
        /// Requests temporary playback of a track. The highest-priority active request is used;
        /// the most recently requested track wins when priorities are equal.
        /// Dispose the returned handle to release this request and resume the next request or
        /// COZY's normal weather- and biome-selected track.
        /// </summary>
        /// <param name="track">The track to play for the duration of this request.</param>
        /// <param name="priority">Higher values take precedence over lower-priority requests.</param>
        /// <returns>A handle that releases this request when disposed.</returns>
        /// <exception cref="System.ArgumentNullException">The track is null.</exception>
        /// <exception cref="System.ArgumentException">The track has no audio clip.</exception>
        /// <exception cref="System.InvalidOperationException">The request is made outside Play Mode.</exception>
        public TrackOverrideHandle RequestTrackOverride(ResoundTrack track, int priority = 0)
        {
            return RequestTrackOverride(track, priority, null, null);
        }

        /// <summary>
        /// Requests temporary playback with transitions specific to this override.
        /// </summary>
        public TrackOverrideHandle RequestTrackOverride(
            ResoundTrack track,
            int priority,
            SongTransitionType transitionOnStart,
            SongTransitionType transitionOnEnd)
        {
            return RequestTrackOverride(track, priority, (SongTransitionType?)transitionOnStart, transitionOnEnd);
        }

        private TrackOverrideHandle RequestTrackOverride(
            ResoundTrack track,
            int priority,
            SongTransitionType? transitionOnStart,
            SongTransitionType? transitionOnEnd)
        {
            if (!Application.isPlaying)
                throw new System.InvalidOperationException("Track overrides can only be requested in Play Mode.");
            if (track == null)
                throw new System.ArgumentNullException(nameof(track));
            if (track.Clip == null)
                throw new System.ArgumentException("The track must have an audio clip.", nameof(track));

            SetupParent();
            InitializeAudioPool();

            long id = ++nextTrackOverrideId;
            TrackOverrideRequest request = new TrackOverrideRequest
            {
                id = id,
                order = ++nextTrackOverrideOrder,
                priority = priority,
                track = track,
                hasCustomTransitions = transitionOnStart.HasValue && transitionOnEnd.HasValue,
                transitionOnStart = transitionOnStart.GetValueOrDefault(),
                transitionOnEnd = transitionOnEnd.GetValueOrDefault()
            };
            trackOverrides.Add(request);
            TransitionToTrack(GetEffectiveTrack(), request.hasCustomTransitions ? request.transitionOnStart : (SongTransitionType?)null);

            return new TrackOverrideHandle(this, id);
        }

        private void ReleaseTrackOverride(long id)
        {
            for (int i = trackOverrides.Count - 1; i >= 0; i--)
            {
                TrackOverrideRequest request = trackOverrides[i];
                if (request.id == id)
                {
                    trackOverrides.RemoveAt(i);
                    TransitionToTrack(GetEffectiveTrack(), request.hasCustomTransitions ? request.transitionOnEnd : (SongTransitionType?)null);
                    return;
                }
            }
        }

        private ResoundTrack GetEffectiveTrack()
        {
            TrackOverrideRequest highestPriorityRequest = null;
            foreach (TrackOverrideRequest request in trackOverrides)
            {
                if (highestPriorityRequest == null
                    || request.priority > highestPriorityRequest.priority
                    || (request.priority == highestPriorityRequest.priority
                        && request.order > highestPriorityRequest.order))
                {
                    highestPriorityRequest = request;
                }
            }

            if (highestPriorityRequest != null)
                return highestPriorityRequest.track;

            return baseTrack != null ? baseTrack : CurrentProfile;
        }

        private void TransitionToEffectiveTrack()
        {
            TransitionToTrack(GetEffectiveTrack());
        }

        private void TransitionToTrack(ResoundTrack effectiveTrack, SongTransitionType? requestedTransition = null)
        {
            if (activeTrack == effectiveTrack) return;

            activeTrack = effectiveTrack;
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            transitionRoutine = StartCoroutine(ExecuteTransition(effectiveTrack, requestedTransition ?? transitionType));
        }

        public override void OnStateMachineUpdate()
        {
            conditionSnapshot = ConditionSnapshot.GetConditions(WeatherSphere);

            if (skipTrackIfChanceIsZero
                && CurrentLocalProfile
                && CurrentLocalProfile.GetChance(conditionSnapshot) <= 0
                && HasAlternativeWithChance(CurrentLocalProfile))
            {
                ChangeProfile();
            }
        }

        private bool HasAlternativeWithChance(ResoundTrack current)
        {
            if (DJ == null || DJ.tracks == null) return false;

            foreach (var track in DJ.tracks)
            {
                if (track != null && track != current && track.GetChance(conditionSnapshot) > 0f)
                    return true;
            }
            return false;
        }

        public override void SetupStateMachine()
        {
            ResoundTrack track = initialTrack;
            if (initializationBehavior == InitializationBehavior.StartWithRandomSong || track == null)
            {
                track = DJ != null ? DJ.GetRandomTrack() : null;
            }

            if (track != null)
            {
                SetProfile(track);
            }
        }

        public override ResoundTrack GetNextProfile(ResoundTrack lastProfile, ConditionSnapshot conditionSnapshot)
        {
            if (DJ == null || DJ.tracks == null || DJ.tracks.Count == 0)
                return null;

            List<ResoundTrack> validTracks = DJ.tracks;
            if (!allowRepeats && validTracks.Count > 1 && lastProfile != null)
            {
                validTracks = new List<ResoundTrack>(validTracks);
                validTracks.Remove(lastProfile);
            }

            if (validTracks.Count == 0)
                return lastProfile;

            ResoundTrack randomTrack = null;
            float totalChance = 0f;
            foreach (var track in validTracks)
            {
                totalChance += track.GetChance(conditionSnapshot);
            }

            if (totalChance <= 0f)
                return validTracks[Random.Range(0, validTracks.Count)];

            float selection = Random.Range(0f, totalChance);
            float currentSum = 0f;

            foreach (var track in validTracks)
            {
                currentSum += track.GetChance(conditionSnapshot);
                if (selection <= currentSum)
                {
                    randomTrack = track;
                    break;
                }
            }

            return randomTrack ?? validTracks[0];
        }

        public override float GetProfilePlayTime(ResoundTrack profile)
        {
            if (profile == null || profile.Clip == null)
                return 10f;

            float time = Random.Range(profile.MinTime, profile.MaxTime) * 60f;

            if (UseSilence)
            {
                float silenceTime = Random.Range(minSilence, maxSilence);
                time += silenceTime / 1440f;
            }

            return time;
        }

        private void HandleProfileChange(ResoundTrack oldProfile, ResoundTrack newProfile)
        {
            baseTrack = newProfile;
            TransitionToEffectiveTrack();
        }

        private IEnumerator ExecuteTransition(ResoundTrack newProfile, SongTransitionType transition)
        {
            switch (transition)
            {
                case SongTransitionType.Crossfade:
                    FadeOutAllVoices();
                    SpawnAndFadeInVoice(newProfile);
                    break;

                case SongTransitionType.FadeOutThenPlayInstant:
                    yield return StartCoroutine(FadeOutAllVoicesRoutine());
                    SpawnAndPlayInstantVoice(newProfile);
                    break;

                case SongTransitionType.FadeOutThenFadeIn:
                    yield return StartCoroutine(FadeOutAllVoicesRoutine());
                    SpawnAndFadeInVoice(newProfile);
                    break;

                case SongTransitionType.InstantCut:
                    StopAllVoicesInstant();
                    SpawnAndPlayInstantVoice(newProfile);
                    break;

                case SongTransitionType.PlayAfterCurrentTrack:
                    AudioSource currentSource = null;
                    for (int i = voices.Count - 1; i >= 0; i--)
                    {
                        Voice voice = voices[i];
                        if (!voice.fadingOut && voice.track != newProfile && voice.source != null)
                        {
                            currentSource = voice.source;
                            break;
                        }
                    }

                    if (currentSource != null && currentSource.isPlaying && currentSource.clip != null)
                    {
                        float previousTime = currentSource.time;
                        float elapsed = 0f;
                        while (currentSource != null
                            && currentSource.isPlaying
                            && currentSource.clip != null)
                        {
                            yield return null;
                            if (currentSource == null || currentSource.clip == null)
                                break;

                            elapsed += Time.unscaledDeltaTime * Mathf.Abs(currentSource.pitch);
                            float playbackTime = currentSource.time;
                            if (playbackTime < previousTime || elapsed >= currentSource.clip.length)
                                break;
                            previousTime = playbackTime;
                        }
                    }

                    StopAllVoicesInstant();
                    SpawnAndFadeInVoice(newProfile);
                    break;
            }

            transitionRoutine = null;
        }

        private void FadeOutAllVoices()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                Voice v = voices[i];
                if (v.fadingOut) continue;

                v.fadingOut = true;
                if (v.routine != null) StopCoroutine(v.routine);
                v.routine = StartCoroutine(FadeVoice(v));
            }
        }

        private IEnumerator FadeOutAllVoicesRoutine()
        {
            List<Voice> voicesToFade = new List<Voice>(voices);
            foreach (var v in voicesToFade)
            {
                if (v.fadingOut) continue;
                v.fadingOut = true;
                if (v.routine != null) StopCoroutine(v.routine);
                v.routine = StartCoroutine(FadeVoice(v));
            }

            // Wait until all voices being faded have been removed from the active voices list
            while (true)
            {
                bool anyFading = false;
                foreach (var v in voicesToFade)
                {
                    if (voices.Contains(v))
                    {
                        anyFading = true;
                        break;
                    }
                }
                if (!anyFading) break;
                yield return null;
            }
        }

        private void StopAllVoicesInstant()
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice v = voices[i];
                if (v.routine != null) StopCoroutine(v.routine);
                ReturnAudioSource(v.source);
            }
            voices.Clear();
        }

        private void SpawnAndFadeInVoice(ResoundTrack newProfile)
        {
            if (newProfile == null || newProfile.Clip == null)
                return;

            Voice targetVoice = null;
            foreach (var v in voices)
            {
                if (v.track == newProfile)
                {
                    targetVoice = v;
                    break;
                }
            }

            if (targetVoice != null)
            {
                targetVoice.fadingOut = false;
                if (targetVoice.routine != null) StopCoroutine(targetVoice.routine);
                targetVoice.routine = StartCoroutine(FadeVoice(targetVoice));
            }
            else
            {
                AudioSource source = GetAudioSource();
                ConfigureSource(source, newProfile);
                source.volume = 0f;
                source.Play();

                Voice voice = new Voice { track = newProfile, source = source };
                voices.Add(voice);
                voice.routine = StartCoroutine(FadeVoice(voice));
            }
        }

        private void SpawnAndPlayInstantVoice(ResoundTrack newProfile)
        {
            if (newProfile == null || newProfile.Clip == null)
                return;

            Voice targetVoice = null;
            foreach (var v in voices)
            {
                if (v.track == newProfile)
                {
                    targetVoice = v;
                    break;
                }
            }

            if (targetVoice != null)
            {
                targetVoice.fadingOut = false;
                if (targetVoice.routine != null) StopCoroutine(targetVoice.routine);
                targetVoice.source.volume = masterVolume * targetVoice.track.Volume;
                if (!targetVoice.source.isPlaying) targetVoice.source.Play();
            }
            else
            {
                AudioSource source = GetAudioSource();
                ConfigureSource(source, newProfile);
                source.volume = masterVolume * newProfile.Volume;
                source.Play();

                Voice voice = new Voice { track = newProfile, source = source };
                voices.Add(voice);
            }
        }

        private static void ConfigureSource(AudioSource source, ResoundTrack track)
        {
            source.clip = track.Clip;
            source.loop = track.LoopMode == ResoundTrack.LoopMethod.Loop;
        }

        /// <summary>
        /// Fades a single voice from its current volume toward its target. Fade-ins track the live
        /// master volume / track volume, fade-outs go to zero and then release the source to the pool.
        /// </summary>
        private IEnumerator FadeVoice(Voice voice)
        {
            float duration = Mathf.Max(0.001f, TransitionTime);
            float start = voice.source != null ? voice.source.volume : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (voice.source == null)
                    break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float target = voice.fadingOut ? 0f : masterVolume * voice.track.Volume;
                voice.source.volume = Mathf.Lerp(start, target, t);

                yield return null;
            }

            voice.routine = null;

            if (voice.fadingOut)
            {
                voices.Remove(voice);
                ReturnAudioSource(voice.source);
            }
            else if (voice.source != null)
            {
                voice.source.volume = masterVolume * voice.track.Volume;
            }
        }

        private void UpdateMixerVolumes()
        {
            foreach (Voice v in voices)
            {
                if (v.fadingOut || v.routine != null) continue;

                if (v.source != null && v.track != null)
                {
                    v.source.volume = masterVolume * v.track.Volume;
                }
            }
        }
    }
}