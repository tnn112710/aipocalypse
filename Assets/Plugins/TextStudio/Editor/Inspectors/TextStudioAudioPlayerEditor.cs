using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioAudioPlayer))]
    internal sealed class TextStudioAudioPlayerEditor : UnityEditor.Editor
    {
        VisualElement _root;
        bool _rebuildPending;

        public override VisualElement CreateInspectorGUI()
        {
            _root = TextStudioInspectorElements.CreateRoot("ts-audio-player-editor-root");

            BuildInspector();
            return _root;
        }

        void OnDisable()
        {
            CommonEditorAudioPreview.StopAll();
        }

        void BuildInspector()
        {
            if (_root == null)
                return;

            _root.Clear();
            serializedObject.UpdateIfRequiredOrScript();

            _root.Add(CreateHeaderSection());
            _root.Add(CreateTargetSection());
            _root.Add(CreateSettingsSourceSection());
            _root.Add(CreatePreviewSection());
        }

        VisualElement CreateHeaderSection()
        {
            VisualElement section = CreateSection("Audio Player");
            section.name = "ts-audio-player-header";

            Label summary = new Label();
            summary.name = "ts-audio-player-summary";
            summary.AddToClassList("ts-muted-text");
            section.Add(summary);

            void RefreshSummary()
            {
                TextStudioAudioPlayerSettingsSource source = GetSettingsSource();
                TextStudioAudioPlayerCueTrigger trigger = (TextStudioAudioPlayerCueTrigger)FindProperty("_cueTrigger").enumValueIndex;
                TextStudioAudioSet audioSet = GetAudioSet();
                bool usingSet = source == TextStudioAudioPlayerSettingsSource.AudioSet && audioSet != null;
                int glyphCount = usingSet ? CountClips(audioSet.GlyphClips) : CountClips(FindProperty("_clips"));
                int markerCount = usingSet ? CountMarkerCues(audioSet.MarkerCues) : CountMarkerCues(FindProperty("_markerCues"));
                string targetStatus = FindProperty("_target").objectReferenceValue != null ? "target assigned" : "same-object target";
                summary.text = (trigger == TextStudioAudioPlayerCueTrigger.Reactions ? "Reaction cues" : "Typewriter events") +
                               ", " + FormatSource(source, audioSet) + ", " + glyphCount + " glyph clips, " +
                               markerCount + " marker cues, " + targetStatus + ".";
            }
            string[] summaryPaths = { "_cueTrigger", "_settingsSource", "_audioSet", "_clips", "_markerCues", "_target" };
            for (int i = 0; i < summaryPaths.Length; i++)
            {
                SerializedProperty property = FindProperty(summaryPaths[i]);
                if (property != null)
                    section.TrackPropertyValue(property, _ => RefreshSummary());
            }
            RefreshSummary();

            return section;
        }

        VisualElement CreateTargetSection()
        {
            VisualElement section = CreateSection("Target And Output");
            section.name = "ts-audio-player-target-output";
            section.Add(CreatePropertyField("_target", "Text Studio Target", "ts-audio-player-target"));
            section.Add(CreatePropertyField("_cueTrigger", "Cue Trigger", "ts-audio-player-cue-trigger"));
            section.Add(CreatePropertyField("_sourceTemplate", "Audio Source Template", "ts-audio-player-source-template"));
            section.Add(CreatePropertyField("_playGlyphCues", "Enable Glyph Clips", "ts-audio-player-play-glyph-cues"));
            section.Add(CreatePropertyField("_playMarkerCues", "Enable Named Cues", "ts-audio-player-play-marker-cues"));
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Typewriter Events plays glyph and marker cues automatically. Reactions plays only cues requested by the assigned Reaction stack. The clip switches mute either pool."));
            return section;
        }

        VisualElement CreateSettingsSourceSection()
        {
            VisualElement section = CreateSection("Settings Source");
            section.name = "ts-audio-player-settings-source";

            TextStudioAudioPlayerSettingsSource renderedSource = GetSettingsSource();
            PropertyField source = CreatePropertyField("_settingsSource", "Source", "ts-audio-player-settings-source-field");
            source.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.UpdateIfRequiredOrScript();
                if (GetSettingsSource() != renderedSource)
                    RequestDeferredRebuild();
            });
            section.Add(source);

            section.Add(CreateHint("Inline stores this component's own clips and playback tuning. Audio Set uses a reusable asset and hides inline-owned settings."));

            if (renderedSource == TextStudioAudioPlayerSettingsSource.AudioSet)
                section.Add(CreateAudioSetSection());
            else
                section.Add(CreateInlineSettingsSection());

            return section;
        }

        VisualElement CreateAudioSetSection()
        {
            VisualElement section = CreateSection("Audio Set");
            section.name = "ts-audio-player-audio-set-section";
            section.Add(CreatePropertyField("_audioSet", "Audio Set", "ts-audio-player-audio-set"));

            Label summary = new Label();
            summary.name = "ts-audio-player-audio-set-summary";
            summary.AddToClassList("ts-muted-text");
            section.Add(summary);

            void RefreshSummary()
            {
                TextStudioAudioSet audioSet = GetAudioSet();
                summary.text = audioSet != null
                    ? BuildAudioSetSummary(audioSet)
                    : "Assign an Audio Set asset, or switch Source to Inline to edit local cue settings.";
            }
            section.TrackPropertyValue(FindProperty("_audioSet"), _ => RefreshSummary());
            RefreshSummary();

            return section;
        }

        VisualElement CreateInlineSettingsSection()
        {
            VisualElement inline = new VisualElement
            {
                name = "ts-audio-player-inline-settings"
            };
            inline.AddToClassList("ts-audio-player-inline-settings");

            VisualElement clips = CreateSection("Clips And Marker Cues");
            clips.Add(CreatePropertyField("_clips", "Glyph Clips", "ts-audio-player-glyph-clips"));
            clips.Add(CreatePropertyField("_markerCues", "Marker Cues", "ts-audio-player-marker-cues"));
            inline.Add(clips);

            VisualElement playback = CreateSection("Playback");
            playback.Add(CreatePropertyField("_clipSequenceMode", "Clip Sequence", "ts-audio-player-clip-sequence"));
            playback.Add(CreatePropertyField("_playbackMode", "Playback", "ts-audio-player-playback"));
            playback.Add(CreatePropertyField("_sourcePoolSize", "Source Pool Size", "ts-audio-player-source-pool-size"));
            playback.Add(CreatePropertyField("_cooldownSeconds", "Cooldown", "ts-audio-player-cooldown"));
            inline.Add(playback);

            VisualElement tuning = CreateSection("Filtering And Tuning");
            tuning.Add(CreatePropertyField("_ignoreWhitespaceAndPunctuation", "Ignore Whitespace And Punctuation", "ts-audio-player-ignore-punctuation"));
            tuning.Add(CreatePropertyField("_ignoredPunctuation", "Ignored Punctuation", "ts-audio-player-ignored-punctuation"));
            tuning.Add(CreatePropertyField("_volume", "Volume", "ts-audio-player-volume"));
            tuning.Add(CreatePropertyField("_pitchRange", "Pitch Range", "ts-audio-player-pitch-range"));
            inline.Add(tuning);

            return inline;
        }

        VisualElement CreatePreviewSection()
        {
            VisualElement section = CreateSection("Preview");
            section.name = "ts-audio-player-preview";

            VisualElement controls = new VisualElement();
            controls.AddToClassList("ts-button-row");
            section.Add(controls);

            Label status = new Label("Ready");
            status.name = "ts-audio-player-preview-status";
            status.AddToClassList("ts-muted-text");

            Button glyph = new Button(() => PreviewGlyphCue(status))
            {
                name = "ts-audio-player-preview-glyph",
                text = "Glyph Cue",
                tooltip = "Play the first effective glyph cue clip."
            };
            glyph.AddToClassList("ts-preview-button");
            controls.Add(glyph);

            Button marker = new Button(() => PreviewMarkerCue(status))
            {
                name = "ts-audio-player-preview-marker",
                text = "Marker Cue",
                tooltip = "Play the first effective marker cue clip."
            };
            marker.AddToClassList("ts-preview-button");
            controls.Add(marker);
            section.Add(status);

            void RefreshAvailability()
            {
                glyph.SetEnabled(FindFirstGlyphClip() != null);
                marker.SetEnabled(FindFirstMarkerClip() != null);
            }
            string[] previewPaths = { "_settingsSource", "_audioSet", "_clips", "_markerCues" };
            for (int i = 0; i < previewPaths.Length; i++)
            {
                SerializedProperty property = FindProperty(previewPaths[i]);
                if (property != null)
                    section.TrackPropertyValue(property, _ => RefreshAvailability());
            }
            RefreshAvailability();

            return section;
        }

        void PreviewGlyphCue(Label status)
        {
            AudioClip clip = FindFirstGlyphClip();
            if (clip == null)
            {
                status.text = "No playable glyph clip assigned.";
                return;
            }

            CommonEditorAudioPreview.Play(clip);
            status.text = "Played glyph cue.";
        }

        void PreviewMarkerCue(Label status)
        {
            AudioClip clip = FindFirstMarkerClip();
            if (clip == null)
            {
                status.text = "No playable marker cue assigned.";
                return;
            }

            CommonEditorAudioPreview.Play(clip);
            status.text = "Played marker cue.";
        }

        AudioClip FindFirstGlyphClip()
        {
            TextStudioAudioSet audioSet = GetActiveAudioSet();
            if (audioSet != null)
                return FindFirstClip(audioSet.GlyphClips);

            return FindFirstClip(FindProperty("_clips"));
        }

        AudioClip FindFirstMarkerClip()
        {
            TextStudioAudioSet audioSet = GetActiveAudioSet();
            if (audioSet != null)
                return FindFirstMarkerClip(audioSet.MarkerCues);

            return FindFirstMarkerClip(FindProperty("_markerCues"));
        }

        TextStudioAudioSet GetActiveAudioSet()
        {
            return GetSettingsSource() == TextStudioAudioPlayerSettingsSource.AudioSet ? GetAudioSet() : null;
        }

        TextStudioAudioSet GetAudioSet()
        {
            SerializedProperty property = FindProperty("_audioSet");
            return property != null ? property.objectReferenceValue as TextStudioAudioSet : null;
        }

        TextStudioAudioPlayerSettingsSource GetSettingsSource()
        {
            return GetSettingsSource(FindProperty("_settingsSource"));
        }

        static TextStudioAudioPlayerSettingsSource GetSettingsSource(SerializedProperty property)
        {
            return property != null && property.enumValueIndex == (int)TextStudioAudioPlayerSettingsSource.AudioSet
                ? TextStudioAudioPlayerSettingsSource.AudioSet
                : TextStudioAudioPlayerSettingsSource.Inline;
        }

        SerializedProperty FindProperty(string propertyName)
        {
            return serializedObject.FindProperty(propertyName);
        }

        void RequestDeferredRebuild()
        {
            if (_rebuildPending || _root == null)
                return;

            _rebuildPending = true;
            _root.schedule.Execute(() =>
            {
                _rebuildPending = false;
                BuildInspector();
                _root.Bind(serializedObject);
            });
        }

        static string FormatSource(TextStudioAudioPlayerSettingsSource source, TextStudioAudioSet audioSet)
        {
            if (source == TextStudioAudioPlayerSettingsSource.AudioSet)
                return audioSet != null ? "Audio Set" : "Audio Set missing";
            return "Inline";
        }

        static string BuildAudioSetSummary(TextStudioAudioSet audioSet)
        {
            int glyphCount = CountClips(audioSet.GlyphClips);
            int markerCount = CountMarkerCues(audioSet.MarkerCues);
            int weakCount = CountWeakClips(audioSet);
            return glyphCount + " glyph clips, " + markerCount + " marker cues" +
                   (weakCount > 0 ? ", " + weakCount + " quiet or missing clips." : ".");
        }

        static AudioClip FindFirstClip(AudioClip[] clips)
        {
            if (clips == null)
                return null;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    return clips[i];
            }

            return null;
        }

        static AudioClip FindFirstClip(SerializedProperty clips)
        {
            if (clips == null || !clips.isArray)
                return null;

            for (int i = 0; i < clips.arraySize; i++)
            {
                AudioClip clip = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static AudioClip FindFirstMarkerClip(IReadOnlyList<TextStudioAudioPlayerMarkerCue> markerCues)
        {
            if (markerCues == null)
                return null;

            for (int i = 0; i < markerCues.Count; i++)
            {
                TextStudioAudioPlayerMarkerCue cue = markerCues[i];
                AudioClip clip = cue != null ? FindFirstClip(cue.Clips) : null;
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static AudioClip FindFirstMarkerClip(SerializedProperty markerCues)
        {
            if (markerCues == null || !markerCues.isArray)
                return null;

            for (int i = 0; i < markerCues.arraySize; i++)
            {
                SerializedProperty cue = markerCues.GetArrayElementAtIndex(i);
                SerializedProperty clips = cue.FindPropertyRelative("_clips");
                AudioClip clip = FindFirstClip(clips);
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static int CountClips(AudioClip[] clips)
        {
            if (clips == null)
                return 0;

            int count = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    count++;
            }

            return count;
        }

        static int CountClips(SerializedProperty clips)
        {
            if (clips == null || !clips.isArray)
                return 0;

            int count = 0;
            for (int i = 0; i < clips.arraySize; i++)
            {
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    count++;
            }

            return count;
        }

        static int CountMarkerCues(IReadOnlyList<TextStudioAudioPlayerMarkerCue> cues)
        {
            return cues != null ? cues.Count : 0;
        }

        static int CountMarkerCues(SerializedProperty cues)
        {
            return cues != null && cues.isArray ? cues.arraySize : 0;
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

        PropertyField CreatePropertyField(string propertyPath, string label, string name)
        {
            return TextStudioInspectorElements.CreatePropertyField(serializedObject, propertyPath, label, name);
        }

        static Label CreateHint(string text)
        {
            return TextStudioInspectorElements.CreateMutedText(text);
        }

        static VisualElement CreateSection(string title)
        {
            return TextStudioInspectorElements.CreateSection(title, null);
        }
    }
}
