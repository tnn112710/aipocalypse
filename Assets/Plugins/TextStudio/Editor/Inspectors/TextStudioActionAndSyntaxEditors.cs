using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal abstract class TextStudioPlaySoundActionEditorBase : UnityEditor.Editor
    {
        protected virtual bool HasSceneSource => false;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-play-sound-action-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Play Sound Cue",
                "ts-play-sound-summary",
                TextStudioInspectorElements.CreateMutedText(
                    "Plays one configured clip from <playSound>. Enable Wait For Completion when dialogue must continue after the sound.")));

            VisualElement identity = TextStudioInspectorElements.CreateSection(
                "Tag",
                "ts-play-sound-tag",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_id", "Tag Id"));
            root.Add(identity);

            VisualElement clips = TextStudioInspectorElements.CreateSection(
                "Clips",
                "ts-play-sound-clips",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_clips", "Audio Clips"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_selection", "Selection"));
            clips.Add(CreatePreviewButton());
            root.Add(clips);

            VisualElement playback = TextStudioInspectorElements.CreateSection(
                "Playback",
                "ts-play-sound-playback",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_output", "Output Mixer"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_volume", "Volume"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_pitchRange", "Pitch Range"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_spatialBlend", "Spatial Blend"));
            if (HasSceneSource)
                playback.Insert(0, TextStudioInspectorElements.CreatePropertyField(serializedObject, "_source", "Audio Source"));
            root.Add(playback);

            root.Add(TextStudioInspectorElements.CreateSection(
                "Reveal Flow",
                "ts-play-sound-flow",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_waitForCompletion", "Wait For Completion"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_timeout", "Wait Timeout"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_stopOnSkip", "Stop On Skip"),
                TextStudioInspectorElements.CreateMutedText(
                    "Markup can override wait, timeout, volume, pitch, or select a clip by name.")));

            return root;
        }

        Button CreatePreviewButton()
        {
            Button preview = new Button(() =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty clips = serializedObject.FindProperty("_clips");
                if (clips == null)
                    return;
                for (int i = 0; i < clips.arraySize; i++)
                {
                    AudioClip clip = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                    if (clip == null)
                        continue;
                    CommonEditorAudioPreview.Play(clip);
                    break;
                }
            })
            {
                text = "Preview First Clip",
                tooltip = "Audition the first assigned clip in the editor."
            };
            preview.SetEnabled(!serializedObject.isEditingMultipleObjects);
            return preview;
        }
    }

    [CustomEditor(typeof(TextStudioPlaySoundAction))]
    internal sealed class TextStudioPlaySoundActionEditor : TextStudioPlaySoundActionEditorBase
    {
    }

    [CustomEditor(typeof(TextStudioPlaySoundActionComponent))]
    internal sealed class TextStudioPlaySoundActionComponentEditor : TextStudioPlaySoundActionEditorBase
    {
        protected override bool HasSceneSource => true;
    }

    [CustomEditor(typeof(TextStudioMarkupSyntax))]
    internal sealed class TextStudioMarkupSyntaxEditor : UnityEditor.Editor
    {
        const string PreviewSample =
            "<wave>Text Studio</wave> {fade}Text Animator{/fade} <!wait=0.2><jump>TMPEffects</>";

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-markup-syntax-inspector");
            Label summary = TextStudioInspectorElements.CreateMutedText(
                "Choose the authoring dialect accepted by this project or component. Compatibility supports safe migration syntax without requiring either provider package.");
            summary.name = "ts-markup-syntax-summary";
            root.Add(summary);

            PropertyField profileField =
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_profile", "Profile");
            profileField.tooltip =
                "Compatibility accepts every supported provider. Native accepts Text Studio only. Provider modes isolate one migration dialect.";
            Label explanation = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-markup-syntax-explanation");
            Label coverage = new Label
            {
                name = "ts-markup-syntax-coverage"
            };
            coverage.AddToClassList("ts-inline-info-text");
            VisualElement syntax = TextStudioInspectorElements.CreateSection(
                "Provider Coverage",
                "ts-markup-syntax-profile",
                profileField,
                explanation,
                coverage);
            PropertyField acceptNative = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_acceptNativeAngleTags",
                "Also Accept Angle Tags");
            PropertyField opening = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_openingDelimiter",
                "Opening Character");
            PropertyField closing = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_closingDelimiter",
                "Closing Character");
            acceptNative.name = "ts-markup-accept-native-angle-tags";
            opening.name = "ts-markup-opening-delimiter";
            closing.name = "ts-markup-closing-delimiter";
            acceptNative.tooltip =
                "When disabled, source angle tags-including Text Studio and TMP rich-text tags-remain literal text. Custom tags are still normalized internally.";
            opening.tooltip = "Enter one character. If you type more, only the first character is used.";
            closing.tooltip = "Enter one character. If you type more, only the first character is used.";
            syntax.Add(acceptNative);
            syntax.Add(opening);
            syntax.Add(closing);
            Label customLimit = TextStudioInspectorElements.CreateMutedText(
                "Custom syntax supports exactly one opening character and one closing character. Commas do not add more delimiter pairs.",
                "ts-markup-custom-limit");
            syntax.Add(customLimit);
            root.Add(syntax);

            TextField previewSource = new TextField("Source")
            {
                name = "ts-markup-syntax-preview-source",
                multiline = true,
                value = PreviewSample
            };
            previewSource.tooltip =
                "Try Text Studio, Text Animator, or TMPEffects markup and inspect the canonical Text Studio result.";
            ConfigurePreviewTextField(previewSource);
            TextField previewResult = new TextField("Canonical Text Studio")
            {
                name = "ts-markup-syntax-preview-result",
                multiline = true,
                isReadOnly = true
            };
            previewResult.tooltip =
                "Dependency-free canonical markup compiled by the normal Text Studio pipeline.";
            ConfigurePreviewTextField(previewResult);
            Label previewStatus = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-markup-syntax-preview-status");
            VisualElement diagnostics = new VisualElement
            {
                name = "ts-markup-syntax-preview-diagnostics"
            };
            diagnostics.AddToClassList("ts-source-diagnostics");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Live Translation Preview",
                "ts-markup-syntax-preview",
                previewSource,
                previewResult,
                previewStatus,
                diagnostics));

            Label canonical = TextStudioInspectorElements.CreateMutedText(
                "Standard TMP formatting remains available in every non-Custom profile. Unknown or unsafe provider tags stay visible as literal text instead of executing arbitrary behavior.");
            canonical.name = "ts-markup-syntax-canonical";
            root.Add(canonical);

            SerializedProperty profile = serializedObject.FindProperty("_profile");
            SerializedProperty acceptNativeProperty = serializedObject.FindProperty("_acceptNativeAngleTags");
            SerializedProperty openingProperty = serializedObject.FindProperty("_openingDelimiter");
            SerializedProperty closingProperty = serializedObject.FindProperty("_closingDelimiter");
            Action refresh = () => RefreshProfile(
                explanation,
                coverage,
                acceptNative,
                opening,
                closing,
                customLimit,
                previewSource,
                previewResult,
                previewStatus,
                diagnostics);
            root.TrackPropertyValue(
                profile,
                _ => refresh());
            root.TrackPropertyValue(
                acceptNativeProperty,
                _ => refresh());
            root.TrackPropertyValue(
                openingProperty,
                _ => refresh());
            root.TrackPropertyValue(
                closingProperty,
                _ => refresh());
            previewSource.RegisterValueChangedCallback(_ => refresh());
            refresh();
            return root;
        }

        void RefreshProfile(
            Label explanation,
            Label coverage,
            VisualElement acceptNative,
            VisualElement opening,
            VisualElement closing,
            VisualElement customLimit,
            TextField previewSource,
            TextField previewResult,
            Label previewStatus,
            VisualElement diagnostics)
        {
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty profile = serializedObject.FindProperty("_profile");
            TextStudioAuthoringProfile value = profile != null
                ? (TextStudioAuthoringProfile)profile.intValue
                : TextStudioAuthoringProfile.Compatibility;
            bool custom = value == TextStudioAuthoringProfile.Custom;
            acceptNative.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
            opening.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
            closing.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
            customLimit.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
            explanation.text = GetProfileExplanation(value);
            coverage.text = GetCoverageText(value);

            TextStudioMarkupSyntax syntaxAsset = target as TextStudioMarkupSyntax;
            string normalized = syntaxAsset != null
                ? syntaxAsset.Normalize(previewSource.value)
                : previewSource.value;
            TextStudioCompatibilityTranslator translator = new TextStudioCompatibilityTranslator();
            previewResult.SetValueWithoutNotify(translator.Translate(normalized, value));
            previewStatus.text = BuildPreviewStatus(value, translator);

            diagnostics.Clear();
            IReadOnlyList<TextStudioCompatibilityDiagnostic> items = translator.Diagnostics;
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioCompatibilityDiagnostic item = items[i];
                Label message = TextStudioInspectorElements.CreateStatus(
                    item.Message,
                    item.Severity == TextStudioCompatibilityDiagnosticSeverity.Warning
                        ? CommonInspectorStatusType.Warning
                        : CommonInspectorStatusType.Info);
                diagnostics.Add(message);
            }
        }

        string GetProfileExplanation(TextStudioAuthoringProfile profile)
        {
            if (profile == TextStudioAuthoringProfile.Native)
            {
                return "Text Studio effect, action, event, and lifecycle tags only. Text Animator braces and TMPEffects commands remain literal.";
            }

            if (profile == TextStudioAuthoringProfile.TextAnimator)
            {
                return "Text Animator Reveal and Hide braces are translated to native lifecycle ranges. Text Studio effect tags are not interpreted from source.";
            }

            if (profile == TextStudioAuthoringProfile.TMPEffects)
            {
                return "Supported TMPEffects animations, show and hide ranges, writer commands, and events are translated explicitly. Unsupported tags remain visible.";
            }

            if (profile == TextStudioAuthoringProfile.Custom)
            {
                SerializedProperty opening = serializedObject.FindProperty("_openingDelimiter");
                SerializedProperty closing = serializedObject.FindProperty("_closingDelimiter");
                SerializedProperty acceptNative = serializedObject.FindProperty("_acceptNativeAngleTags");
                string openingValue = FirstCharacterOr(opening != null ? opening.stringValue : string.Empty, "[");
                string closingValue = FirstCharacterOr(closing != null ? closing.stringValue : string.Empty, "]");
                string angleBehavior = acceptNative == null || acceptNative.boolValue
                    ? "Angle tags are accepted as well."
                    : "Angle tags remain literal text.";
                return "Accepts " + openingValue + "tag" + closingValue + "..." +
                       openingValue + "/tag" + closingValue + " as generic tags. " +
                       "Provider compatibility is disabled. " + angleBehavior;
            }

            return "Recommended. Accepts native Text Studio, supported Text Animator braces, and safe TMPEffects syntax. Strong TMPEffects evidence resolves ambiguous vendor names for the complete string.";
        }

        static string GetCoverageText(TextStudioAuthoringProfile profile)
        {
            switch (profile)
            {
                case TextStudioAuthoringProfile.Native:
                    return "Text Studio  •  TMP formatting";
                case TextStudioAuthoringProfile.TextAnimator:
                    return "Text Animator  •  TMP formatting";
                case TextStudioAuthoringProfile.TMPEffects:
                    return "TMPEffects safe subset  •  TMP formatting";
                case TextStudioAuthoringProfile.Custom:
                    return "Text Studio with custom delimiters";
                default:
                    return "Text Studio  •  Text Animator  •  TMPEffects safe subset  •  TMP formatting";
            }
        }

        static string BuildPreviewStatus(
            TextStudioAuthoringProfile profile,
            TextStudioCompatibilityTranslator translator)
        {
            string active = profile == TextStudioAuthoringProfile.Compatibility
                ? translator.TMPEffectsDetected
                    ? "Compatibility detected TMPEffects evidence"
                    : "Compatibility kept native Text Studio ownership"
                : FormatProfile(profile) + " profile";
            int count = translator.TranslatedTagCount;
            string translated = count == 1 ? "1 tag translated" : count + " tags translated";
            return active + ", " + translated + ".";
        }

        static string FormatProfile(TextStudioAuthoringProfile profile)
        {
            return profile == TextStudioAuthoringProfile.TextAnimator
                ? "Text Animator"
                : profile.ToString();
        }

        static void ConfigurePreviewTextField(TextField field)
        {
            TextStudioInspectorElements.ConfigureCodeTextField(field);
            field.AddToClassList("ts-markup-syntax-preview-field");
            field.style.minHeight = 64f;
            field.style.whiteSpace = WhiteSpace.Normal;
            VisualElement input = field.Q(className: "unity-text-field__input");
            if (input == null)
                return;

            input.style.minHeight = 54f;
            input.style.whiteSpace = WhiteSpace.Normal;
        }

        static string FirstCharacterOr(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value[0].ToString();
        }
    }
}
