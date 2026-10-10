using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioAudioSet))]
    internal sealed class TextStudioAudioSetEditor : UnityEditor.Editor
    {
        const double PreviewTickIntervalMs = 33.0;
        const string DefaultPreviewText = "The signal is live. Typewriter audio ready.";

        readonly TextStudioAudioSetPreviewState _previewState = new TextStudioAudioSetPreviewState();
        uint _randomState = 0xA2752B31u;
        int _nextClip;
        int _lastClipIndex = -1;
        double _lastTickTime;
        double _lastSoundTime = -999.0;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();

            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-audio-set-editor-root");

            root.Add(CreateOverviewSection());
            root.Add(CreatePreviewSection(root));
            root.Add(CreatePlaybackSection());
            root.Add(CreateClipSection());
            return root;
        }

        void OnDisable()
        {
            CommonEditorAudioPreview.StopAll();
        }

        VisualElement CreateOverviewSection()
        {
            VisualElement section = CreateSection("Audio Set");
            section.name = "ts-audio-set-overview";

            Label summary = new Label();
            summary.name = "ts-audio-set-summary";
            summary.AddToClassList("ts-muted-text");
            section.Add(summary);

            void RefreshSummary()
            {
                if (target == null)
                    return;

                TextStudioAudioSet audioSet = (TextStudioAudioSet)target;
                int glyphCount = audioSet.GlyphClips != null ? audioSet.GlyphClips.Length : 0;
                int markerCount = audioSet.MarkerCues != null ? audioSet.MarkerCues.Count : 0;
                int weakCount = CountWeakClips(audioSet);
                summary.text = glyphCount + " glyph clips, " + markerCount + " marker cues" +
                               (weakCount > 0 ? ", " + weakCount + " quiet or missing clips" : string.Empty);
            }
            section.TrackPropertyValue(serializedObject.FindProperty("_glyphClips"), _ => RefreshSummary());
            section.TrackPropertyValue(serializedObject.FindProperty("_markerCues"), _ => RefreshSummary());
            RefreshSummary();

            return section;
        }

        VisualElement CreatePreviewSection(VisualElement inspectorRoot)
        {
            VisualElement section = CreateSection("Typewriter Preview");
            section.name = "ts-audio-set-preview";
            section.AddToClassList("ts-preview-section");

            Slider speed = new Slider("Characters Per Second", 4f, 60f)
            {
                name = "ts-audio-set-preview-speed",
                value = 22f
            };
            speed.AddToClassList("ts-speed-slider");
            section.Add(speed);

            VisualElement controls = new VisualElement();
            controls.AddToClassList("ts-button-row");
            section.Add(controls);

            Label previewText = new Label();
            previewText.name = "ts-audio-set-preview-text";
            previewText.text = DefaultPreviewText;
            previewText.style.whiteSpace = WhiteSpace.Normal;
            previewText.style.minHeight = 44f;
            previewText.style.paddingLeft = 8f;
            previewText.style.paddingRight = 8f;
            previewText.style.paddingTop = 7f;
            previewText.style.paddingBottom = 7f;
            previewText.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
            previewText.style.unityFontStyleAndWeight = FontStyle.Bold;

            Label status = new Label("Ready");
            status.name = "ts-audio-set-preview-status";
            status.AddToClassList("ts-muted-text");

            Button stop = null;
            Button play = new Button(() => StartPreview(speed.value, previewText, status, stop))
            {
                name = "ts-audio-set-preview-play",
                text = "Play"
            };
            play.tooltip = "Play the built-in preview line with this audio set's glyph clips.";
            play.AddToClassList("ts-preview-button");
            controls.Add(play);

            stop = new Button(() => StopPreview(previewText, status, stop))
            {
                name = "ts-audio-set-preview-stop",
                text = "Stop"
            };
            stop.tooltip = "Stop the typewriter preview and any editor audio preview clips.";
            stop.AddToClassList("ts-preview-button");
            stop.SetEnabled(false);
            controls.Add(stop);

            Button marker = new Button(() => PlayFirstMarkerCue(status))
            {
                name = "ts-audio-set-preview-marker",
                text = "Marker Cue"
            };
            marker.tooltip = "Play the first configured marker cue.";
            marker.AddToClassList("ts-preview-button");
            controls.Add(marker);

            section.Add(previewText);
            section.Add(status);

            TextStudioInspectorElements.SchedulePeriodicRefresh(inspectorRoot, () =>
            {
                if (!CanRefresh(section))
                    return;

                serializedObject.UpdateIfRequiredOrScript();
                SyncMarkerButton(marker);
                TickPreview(previewText, status);
                stop.SetEnabled(_previewState.IsPlaying);
            }, (long)PreviewTickIntervalMs);

            return section;
        }

        VisualElement CreatePlaybackSection()
        {
            VisualElement section = CreateSection("Playback");
            section.name = "ts-audio-set-playback";
            AddProperty(section, "_clipSequenceMode", "Clip Sequence");
            AddProperty(section, "_playbackMode", "Playback");
            AddProperty(section, "_sourcePoolSize", "Source Pool Size");
            AddProperty(section, "_cooldownSeconds", "Cooldown");
            AddProperty(section, "_volume", "Volume");
            AddProperty(section, "_pitchRange", "Pitch Range");
            AddProperty(section, "_ignoreWhitespaceAndPunctuation", "Ignore Whitespace And Punctuation");
            AddProperty(section, "_ignoredPunctuation", "Ignored Punctuation");
            return section;
        }

        VisualElement CreateClipSection()
        {
            VisualElement section = CreateSection("Clips And Marker Cues");
            section.name = "ts-audio-set-clips";
            AddProperty(section, "_glyphClips", "Glyph Clips");
            AddProperty(section, "_markerCues", "Marker Cues");

            Label health = new Label();
            health.name = "ts-audio-set-clip-health";
            health.AddToClassList("ts-muted-text");
            section.Add(health);

            void RefreshHealth()
            {
                if (target == null)
                    return;

                TextStudioAudioSet audioSet = (TextStudioAudioSet)target;
                int weakCount = CountWeakClips(audioSet);
                health.text = weakCount == 0
                    ? "All assigned clips have audible waveform energy."
                    : weakCount + " assigned clips are missing or below the preview energy threshold.";
            }
            section.TrackPropertyValue(serializedObject.FindProperty("_glyphClips"), _ => RefreshHealth());
            section.TrackPropertyValue(serializedObject.FindProperty("_markerCues"), _ => RefreshHealth());
            RefreshHealth();

            return section;
        }

        void StartPreview(float charactersPerSecond, Label previewText, Label status, Button stop)
        {
            TextStudioAudioSet audioSet = target as TextStudioAudioSet;
            if (audioSet == null)
                return;

            if (!HasGlyphClips(audioSet))
            {
                status.text = "Assign at least one glyph clip before previewing.";
                return;
            }

            CommonEditorAudioPreview.StopAll();
            _nextClip = 0;
            _lastClipIndex = -1;
            _lastSoundTime = -999.0;
            _lastTickTime = EditorApplication.timeSinceStartup;
            _previewState.Start(DefaultPreviewText, charactersPerSecond);
            previewText.text = string.Empty;
            status.text = "Typing...";
            stop?.SetEnabled(true);
        }

        void StopPreview(Label previewText, Label status, Button stop)
        {
            _previewState.Stop();
            CommonEditorAudioPreview.StopAll();
            status.text = "Stopped";
            stop?.SetEnabled(false);
        }

        void TickPreview(Label previewText, Label status)
        {
            if (!_previewState.IsPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            float delta = _lastTickTime > 0.0 ? Mathf.Max(0f, (float)(now - _lastTickTime)) : 0f;
            _lastTickTime = now;

            int previousCount = _previewState.VisibleCount;
            int added = _previewState.Tick(delta);
            previewText.text = _previewState.VisibleText;

            TextStudioAudioSet audioSet = target as TextStudioAudioSet;
            if (audioSet != null && added > 0)
            {
                for (int i = 0; i < added; i++)
                    TryPlayCharacter(audioSet, _previewState.GetVisibleCharacter(previousCount + i), now);
            }

            status.text = _previewState.IsPlaying ? "Typing..." : "Complete";
        }

        void TryPlayCharacter(TextStudioAudioSet audioSet, char character, double time)
        {
            if (!TextStudioAudioPlayerRules.ShouldPlayForCharacter(
                    character,
                    audioSet.IgnoreWhitespaceAndPunctuation,
                    audioSet.IgnoredPunctuation))
            {
                return;
            }

            if (time - _lastSoundTime < audioSet.CooldownSeconds)
                return;

            AudioClip[] clips = audioSet.GlyphClips;
            if (!TextStudioAudioPlayer.TryGetNextClip(
                    clips,
                    ref _nextClip,
                    audioSet.ClipSequenceMode,
                    ref _randomState,
                    ref _lastClipIndex,
                    out AudioClip clip))
            {
                return;
            }

            CommonEditorAudioPreview.Play(clip);
            _lastSoundTime = time;
        }

        void PlayFirstMarkerCue(Label status)
        {
            TextStudioAudioSet audioSet = target as TextStudioAudioSet;
            if (audioSet == null || audioSet.MarkerCues == null || audioSet.MarkerCues.Count == 0)
            {
                status.text = "No marker cues assigned.";
                return;
            }

            TextStudioAudioPlayerMarkerCue cue = audioSet.MarkerCues[0];
            if (cue == null || !cue.TryGetNextClip(out AudioClip clip))
            {
                status.text = "The first marker cue has no playable clip.";
                return;
            }

            CommonEditorAudioPreview.Play(clip);
            status.text = "Played marker cue " + (string.IsNullOrEmpty(cue.RouteId) ? "(unnamed)" : cue.RouteId) + ".";
        }

        void SyncMarkerButton(Button marker)
        {
            TextStudioAudioSet audioSet = target as TextStudioAudioSet;
            bool hasMarker = audioSet != null && audioSet.MarkerCues != null && audioSet.MarkerCues.Count > 0;
            marker.SetEnabled(hasMarker);
        }

        static bool HasGlyphClips(TextStudioAudioSet audioSet)
        {
            AudioClip[] clips = audioSet.GlyphClips;
            if (clips == null)
                return false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return true;
            }

            return false;
        }

        static int CountWeakClips(TextStudioAudioSet audioSet)
        {
            if (audioSet == null)
                return 0;

            int count = 0;
            CountWeak(audioSet.GlyphClips, ref count);
            IReadOnlyList<TextStudioAudioPlayerMarkerCue> markerCues = audioSet.MarkerCues;
            if (markerCues != null)
            {
                for (int i = 0; i < markerCues.Count; i++)
                {
                    if (markerCues[i] != null)
                        CountWeak(markerCues[i].Clips, ref count);
                }
            }

            return count;
        }

        static void CountWeak(AudioClip[] clips, ref int count)
        {
            if (clips == null)
                return;

            for (int i = 0; i < clips.Length; i++)
            {
                if (!TextStudioAudioClipAnalysis.Analyze(clips[i]).IsAudible)
                    count++;
            }
        }

        void AddProperty(VisualElement parent, string propertyPath, string label)
        {
            PropertyField field = TextStudioInspectorElements.CreatePropertyField(serializedObject, propertyPath, label);
            if (field != null)
                parent.Add(field);
        }

        static VisualElement CreateSection(string title)
        {
            return TextStudioInspectorElements.CreateSection(title, null);
        }

        static bool CanRefresh(VisualElement element)
        {
            return element != null && element.panel != null;
        }
    }

    internal sealed class TextStudioAudioSetPreviewState
    {
        string _sourceText = string.Empty;
        float _charactersPerSecond = 22f;
        float _elapsed;

        public bool IsPlaying { get; private set; }
        public int VisibleCount { get; private set; }
        public string VisibleText => VisibleCount <= 0 ? string.Empty : _sourceText.Substring(0, VisibleCount);

        public void Start(string sourceText, float charactersPerSecond)
        {
            _sourceText = sourceText ?? string.Empty;
            _charactersPerSecond = Mathf.Max(1f, charactersPerSecond);
            _elapsed = 0f;
            VisibleCount = 0;
            IsPlaying = _sourceText.Length > 0;
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        public int Tick(float deltaSeconds)
        {
            if (!IsPlaying)
                return 0;

            int before = VisibleCount;
            _elapsed += Mathf.Max(0f, deltaSeconds);
            VisibleCount = Mathf.Clamp(
                Mathf.FloorToInt(_elapsed * _charactersPerSecond + 0.0001f),
                0,
                _sourceText.Length);
            if (VisibleCount >= _sourceText.Length)
                IsPlaying = false;
            return Mathf.Max(0, VisibleCount - before);
        }

        public char GetVisibleCharacter(int index)
        {
            return index >= 0 && index < _sourceText.Length ? _sourceText[index] : '\0';
        }
    }

    internal readonly struct TextStudioAudioClipStats
    {
        public readonly float PeakDb;
        public readonly float RmsDb;
        public readonly int SampleCount;
        public bool IsAudible => PeakDb > -20f && RmsDb > -60f;

        public TextStudioAudioClipStats(float peakDb, float rmsDb, int sampleCount)
        {
            PeakDb = peakDb;
            RmsDb = rmsDb;
            SampleCount = sampleCount;
        }
    }

    internal static class TextStudioAudioClipAnalysis
    {
        public static TextStudioAudioClipStats Analyze(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0)
                return new TextStudioAudioClipStats(-120f, -120f, 0);

            float[] samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
                return new TextStudioAudioClipStats(-120f, -120f, 0);

            double sumSquares = 0d;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                peak = Mathf.Max(peak, abs);
                sumSquares += abs * abs;
            }

            float rms = samples.Length > 0 ? Mathf.Sqrt((float)(sumSquares / samples.Length)) : 0f;
            return new TextStudioAudioClipStats(ToDecibels(peak), ToDecibels(rms), samples.Length);
        }

        static float ToDecibels(float amplitude)
        {
            return amplitude > 0f ? 20f * Mathf.Log10(amplitude) : -120f;
        }
    }

}
