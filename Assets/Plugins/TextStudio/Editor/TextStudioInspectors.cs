using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioText))]
    [CanEditMultipleObjects]
    internal partial class TextStudioTextEditor : UnityEditor.Editor
    {
        TextStudioTmpAuthoringStorage _sharedAuthoringStorage;
        SerializedProperty _markup;
        SerializedProperty _authoringMode;
        SerializedProperty _backendWriteHandling;
        SerializedProperty _behaviorProfile;
        SerializedProperty _appearanceStyle;
        SerializedProperty _writingStyle;
        SerializedProperty _inlineDefaultEffectMode;
        SerializedProperty _inlineDefaultEffects;
        SerializedProperty _inlineDefaultRevealEffects;
        SerializedProperty _inlineDefaultActiveEffects;
        SerializedProperty _inlineDefaultHideEffects;
        SerializedProperty _typewriterSource;
        SerializedProperty _typewriterPreset;
        SerializedProperty _revealMode;
        SerializedProperty _timings;
        SerializedProperty _builtInTimingsPreset;
        SerializedProperty _waitAfterFinalReveal;
        SerializedProperty _revealSpeedMultiplier;
        SerializedProperty _revealAppearanceDuration;
        SerializedProperty _revealDisappearanceDuration;
        SerializedProperty _loopReveal;
        SerializedProperty _loopRevealDelay;
        SerializedProperty _revealSequence;
        SerializedProperty _hideSequence;
        SerializedProperty _motionPolicy;
        SerializedProperty _typewriterStartMode;
        SerializedProperty _textChangeMode;
        SerializedProperty _textChangeScope;
        SerializedProperty _resetSpeedOnNewText;
        SerializedProperty _skipRevealMode;
        SerializedProperty _triggerRemainingMarkersOnSkip;
        SerializedProperty _skipCurrentWaitOnSkip;
        SerializedProperty _skipHideMode;
        SerializedProperty _typewriterPresetId;
        SerializedProperty _typewriterRevealEffect;
        SerializedProperty _typewriterHideEffect;
        SerializedProperty _typewriterRevealEffectExpression;
        SerializedProperty _typewriterHideEffectExpression;
        SerializedProperty _timing;
        SerializedProperty _runActionsInPreview;
        SerializedProperty _placementMode;
        SerializedProperty _extensionPlacementId;
        SerializedProperty _arcPlacement;
        SerializedProperty _wavePlacement;
        SerializedProperty _circlePlacement;
        SerializedProperty _gridPlacement;
        SerializedProperty _curvePlacement;
        SerializedProperty _influenceTarget;
        SerializedProperty _placementRotation;
        SerializedProperty _presentationProvider;
        SerializedProperty _onSetText;
        SerializedProperty _onShowComplete;
        SerializedProperty _onDisappearComplete;

        static int s_activeTab;

        public static void SelectSettingsTabForValidation(string tabName)
        {
            for (int i = 0; i < s_AuthoringSurfaceTabs.Length; i++)
            {
                if (!string.Equals(s_AuthoringSurfaceTabs[i].text, tabName, StringComparison.Ordinal))
                    continue;

                s_activeTab = i;
                return;
            }
        }

        void OnEnable()
        {
            _isEditingAssignedProfile = false;
            CacheSerializedProperties();
        }

        void CacheSerializedProperties()
        {
            _sharedAuthoringStorage = new TextStudioTmpAuthoringStorage(serializedObject);
            _markup = _sharedAuthoringStorage.FindProperty(TextStudioAuthoringPropertyId.Markup);
            _authoringMode = serializedObject.FindProperty("_authoringMode");
            _backendWriteHandling =
                serializedObject.FindProperty("_backendWriteHandling");
            _behaviorProfile = _sharedAuthoringStorage.FindProperty(
                TextStudioAuthoringPropertyId.BehaviorProfile);
            _appearanceStyle = serializedObject.FindProperty("_appearanceStyle");
            _writingStyle = serializedObject.FindProperty("_writingStyle");
            _inlineDefaultEffectMode = serializedObject.FindProperty("_inlineDefaultEffectMode");
            _inlineDefaultEffects = serializedObject.FindProperty("_inlineDefaultEffects");
            _inlineDefaultRevealEffects = serializedObject.FindProperty("_inlineDefaultRevealEffects");
            _inlineDefaultActiveEffects = serializedObject.FindProperty("_inlineDefaultActiveEffects");
            _inlineDefaultHideEffects = serializedObject.FindProperty("_inlineDefaultHideEffects");
            _typewriterSource = serializedObject.FindProperty("_typewriterSource");
            _typewriterPreset = serializedObject.FindProperty("_typewriterPreset");
            _revealMode = _sharedAuthoringStorage.FindProperty(TextStudioAuthoringPropertyId.RevealMode);
            _timings = serializedObject.FindProperty("_timings");
            _builtInTimingsPreset = serializedObject.FindProperty("_builtInTimingsPreset");
            _waitAfterFinalReveal =
                serializedObject.FindProperty("_waitAfterFinalReveal");
            _revealSpeedMultiplier = _sharedAuthoringStorage.FindProperty(
                TextStudioAuthoringPropertyId.SpeedMultiplier);
            _revealAppearanceDuration = serializedObject.FindProperty("_revealAppearanceDuration");
            _revealDisappearanceDuration = serializedObject.FindProperty("_revealDisappearanceDuration");
            _loopReveal = _sharedAuthoringStorage.FindProperty(TextStudioAuthoringPropertyId.LoopReveal);
            _loopRevealDelay = serializedObject.FindProperty("_loopRevealDelay");
            _revealSequence = serializedObject.FindProperty("_revealSequence");
            _hideSequence = serializedObject.FindProperty("_hideSequence");
            _motionPolicy = serializedObject.FindProperty("_motionPolicy");
            _typewriterStartMode = serializedObject.FindProperty("_typewriterStartMode");
            _textChangeMode = serializedObject.FindProperty("_textChangeMode");
            _textChangeScope = serializedObject.FindProperty("_textChangeScope");
            _resetSpeedOnNewText = serializedObject.FindProperty("_resetSpeedOnNewText");
            _skipRevealMode = serializedObject.FindProperty("_skipRevealMode");
            _triggerRemainingMarkersOnSkip = serializedObject.FindProperty("_triggerRemainingMarkersOnSkip");
            _skipCurrentWaitOnSkip = serializedObject.FindProperty("_skipCurrentWaitOnSkip");
            _skipHideMode = serializedObject.FindProperty("_skipHideMode");
            _typewriterPresetId = serializedObject.FindProperty("_typewriterPresetId");
            _typewriterRevealEffect = serializedObject.FindProperty("_typewriterRevealEffect");
            _typewriterHideEffect = serializedObject.FindProperty("_typewriterHideEffect");
            _typewriterRevealEffectExpression = serializedObject.FindProperty("_typewriterRevealEffectExpression");
            _typewriterHideEffectExpression = serializedObject.FindProperty("_typewriterHideEffectExpression");
            _timing = serializedObject.FindProperty("_timing");
            _runActionsInPreview = serializedObject.FindProperty("_runActionsInPreview");
            _placementMode = serializedObject.FindProperty("_placementMode");
            _extensionPlacementId = serializedObject.FindProperty("_extensionPlacementId");
            _arcPlacement = serializedObject.FindProperty("_arcPlacement");
            _wavePlacement = serializedObject.FindProperty("_wavePlacement");
            _circlePlacement = serializedObject.FindProperty("_circlePlacement");
            _gridPlacement = serializedObject.FindProperty("_gridPlacement");
            _curvePlacement = serializedObject.FindProperty("_curvePlacement");
            _influenceTarget = serializedObject.FindProperty("_influenceTarget");
            _placementRotation = serializedObject.FindProperty("_placementRotation");
            _presentationProvider = serializedObject.FindProperty("_presentationProvider");
            _onSetText = serializedObject.FindProperty("_onSetText");
            _onShowComplete = serializedObject.FindProperty("_onShowComplete");
            _onDisappearComplete = serializedObject.FindProperty("_onDisappearComplete");
        }

        public override bool RequiresConstantRepaint()
        {
            TextStudioText hub = target as TextStudioText;
            return hub != null && (hub.IsRevealing || hub.IsDisappearing || hub.Preview.IsPlaying || hub.Preview.IsScrubbing);
        }

        void CreateBehaviorProfileAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Behavior Profile",
                "Text Studio Behavior Profile",
                "asset",
                "Choose where to save the Text Studio behavior profile.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioBehaviorProfile profile =
                ScriptableObject.CreateInstance<TextStudioBehaviorProfile>();
            TextStudioText hub = target as TextStudioText;
            if (hub != null && hub.BehaviorProfile == null)
                profile.ApplyBehaviorSettings(hub.GetLocalBehaviorSettings());
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            if (hub != null)
            {
                Undo.RecordObject(hub, "Assign Text Studio Behavior Profile");
                hub.SetBehaviorProfile(profile);
                serializedObject.UpdateIfRequiredOrScript();
                EditorUtility.SetDirty(hub);
                RequestUitkActiveTabRebuild(hub);
            }
            else
            {
                _behaviorProfile.objectReferenceValue = profile;
                serializedObject.ApplyModifiedProperties();
            }
        }

        static string BuildTagStatus(TextStudioDiagnosticReport report)
        {
            return TextStudioAuthoringSession.BuildTagStatus(report);
        }

        static bool HasExternalTextSource(TextStudioText hub)
        {
            return hub != null && hub.GetComponents<ITextStudioExternalTextSource>().Length > 0;
        }
    }

    [CustomEditor(typeof(TextStudioDefaultSettings))]
    internal class TextStudioDefaultSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-default-settings-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Pipeline Defaults",
                "ts-default-settings-values",
                TextStudioInspectorElements.CreateMutedText(
                    "These are the effective Text Studio pipeline defaults. Project Settings can override them for this project."),
                TextStudioInspectorElements.CreateDefaultProperties(serializedObject)));
            return root;
        }
    }

    [CustomEditor(typeof(CharacterTimings))]
    internal class CharacterTimingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-character-timings-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Character Pace",
                "ts-character-timings-pace",
                TextStudioInspectorElements.CreateMutedText("Each value is a wait after the visible text unit, so punctuation appears before its reading pause begins."),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_baseDelay", "Regular Character Delay")));
            root.Add(TextStudioInspectorElements.CreateSection(
                "Reading Pauses",
                "ts-character-timings-punctuation",
                TextStudioInspectorElements.CreateMutedText("Long punctuation normally marks sentence endings; medium punctuation creates a shorter reading pause."),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_longPunctChars", "Long-pause Characters"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_longPunctDelay", "Long Pause"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_mediumPunctChars", "Medium-pause Characters"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_mediumPunctDelay", "Medium Pause"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_collapseMultiplePunctuation", "Collapse Punctuation Runs"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_skipLastPunctuationDelay", "Skip Final Reading Pause"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_abbreviationWhitelist", "Abbreviations")));
            SerializedProperty waitNewLinesProperty = serializedObject.FindProperty("_waitForNewLines");
            PropertyField waitNewLines = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_waitForNewLines", "Pause At New Lines");
            PropertyField newLineDelay = TextStudioInspectorElements.CreatePropertyField(serializedObject, "_newLineDelay", "New-line Pause");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Boundaries",
                "ts-character-timings-boundaries",
                waitNewLines,
                newLineDelay,
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_waitAfterFinalReveal", "Wait After Final Reveal")));
            void RefreshNewLineDelay()
            {
                newLineDelay.style.display = waitNewLinesProperty != null && waitNewLinesProperty.boolValue
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            root.TrackPropertyValue(waitNewLinesProperty, _ => RefreshNewLineDelay());
            RefreshNewLineDelay();
            return root;
        }
    }

    [CustomEditor(typeof(WordTimings))]
    internal class WordTimingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-word-timings-inspector");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Word Pace",
                "ts-word-timings-pace",
                TextStudioInspectorElements.CreateMutedText("Whitespace advances to the next word. A word ending in one of the listed punctuation characters uses the longer punctuation pause."),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_normalWordDelay", "Between Words"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_punctuationChars", "Word-ending Punctuation"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_punctuationWordDelay", "After Punctuation"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_waitAfterFinalReveal", "Wait After Final Reveal")));
            return root;
        }

    }

    [CustomEditor(typeof(TextStudioTypewriterPreset))]
    internal class TextStudioTypewriterPresetEditor : UnityEditor.Editor
    {
        TextStudioTypewriterPresetPreview _preview;

        void OnDisable() => _preview?.Dispose();

        public override VisualElement CreateInspectorGUI()
        {
            SerializedProperty settings = serializedObject.FindProperty("_settings");
            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-typewriter-preset-inspector");
            Label guidance = TextStudioInspectorElements.CreateMutedText(
                "A reusable Typewriter recipe combines timing with optional shared Reveal and Hide effects.");
            guidance.name = "ts-typewriter-preset-intro";
            root.Add(guidance);

            _preview?.Dispose();
            _preview = new TextStudioTypewriterPresetPreview();
            root.Add(TextStudioInspectorElements.CreateSection(
                "Live Preview",
                "ts-typewriter-preset-live-preview",
                _preview,
                TextStudioInspectorElements.CreateMutedText(
                    "Preview this preset without changing scene text or creating scene objects.")));

            root.Add(TextStudioInspectorElements.CreateSection(
                "Identity",
                "ts-typewriter-preset-identity",
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_displayName", "Display Name"),
                TextStudioInspectorElements.CreatePropertyField(serializedObject, "_description", "Description")));

            PropertyField sampleText = TextStudioInspectorElements.CreatePropertyField(
                serializedObject,
                "_sampleText",
                "Preview Text");
            root.Add(TextStudioInspectorElements.CreateSection(
                "Preview Content",
                "ts-typewriter-preset-preview",
                TextStudioInspectorElements.CreateMutedText(
                    "Used by galleries and preview surfaces. Markup is allowed, so the sample can demonstrate the preset's intended visual motion."),
                sampleText));

            if (settings != null)
            {
                SerializedProperty revealMode = settings.FindPropertyRelative("RevealMode");
                SerializedProperty timings = settings.FindPropertyRelative("Timings");
                SerializedProperty builtInTimings = settings.FindPropertyRelative("BuiltInTimingsPreset");
                SerializedProperty waitAfterFinalReveal =
                    settings.FindPropertyRelative("WaitAfterFinalReveal");
                SerializedProperty loopProperty = settings.FindPropertyRelative("LoopReveal");
                SerializedProperty writingEnabled = settings.FindPropertyRelative("WritingEnabled");
                SerializedProperty revealEffect = settings.FindPropertyRelative("RevealEffect");
                SerializedProperty hideEffect = settings.FindPropertyRelative("HideEffect");
                RevealTimingsPreset[] timingPresets;
                List<string> timingChoices = TextStudioInspectorElements.CreateTimingPresetChoices(out timingPresets);
                int timingChoiceIndex = TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                    timingPresets,
                    (RevealTimingsPreset)builtInTimings.enumValueIndex,
                    timings.objectReferenceValue != null);
                bool customTimingSelected = timingChoiceIndex == timingPresets.Length;
                TextStudioTypewriterModeView modeView = new TextStudioTypewriterModeView(() =>
                    TextStudioTypewriterModeState.Read(
                        revealMode,
                        customTimingSelected,
                        timings,
                        loopProperty,
                        writingEnabled,
                        revealEffect,
                        hideEffect));

                PropertyField revealModeField = CreateSettingsField(
                    revealMode,
                    TextStudioTypewriterModePresentation.ModeLabel,
                    "ts-typewriter-preset-reveal-mode");
                revealModeField.tooltip = TextStudioTypewriterModePresentation.ModeTooltip;
                Label modeGuidance = TextStudioInspectorElements.CreateMutedText(
                    string.Empty,
                    "ts-typewriter-mode-guidance");
                modeView.SetGuidance(modeGuidance);
                PropertyField timingsField = CreateSettingsField(timings, "Custom Asset", "ts-typewriter-preset-custom-timings");
                timingsField.tooltip = "Reusable timing asset used instead of the selected built-in cadence. In Custom mode it also defines reveal groups.";
                PropertyField waitAfterFinalRevealField = CreateSettingsField(
                    waitAfterFinalReveal,
                    "Wait After Final Reveal",
                    "ts-typewriter-preset-wait-after-final-reveal");
                waitAfterFinalRevealField.tooltip =
                    "With a built-in cadence, include the final unit's trailing delay before completion. Custom timing assets own this choice.";
                modeView.Add(TextStudioTypewriterModeField.FinalRevealWait, waitAfterFinalRevealField);
                DropdownField timingPreset = new DropdownField(timingChoices, timingChoiceIndex)
                {
                    name = "ts-typewriter-preset-timing-picker",
                    tooltip = "Choose a built-in reveal cadence or Custom to use a reusable timing asset."
                };
                VisualElement timingPresetRow = TextStudioInspectorElements.CreateAlignedControlRow(
                    "Preset",
                    timingPreset,
                    "ts-typewriter-preset-timing-row");
                Button createTimings = TextStudioInspectorElements.CreateCompactCreateButton(
                    () => CreateTimingsAsset(timings),
                    "ts-typewriter-preset-create-timings",
                    "Create and assign a custom timing asset.");
                VisualElement customTimingsRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                    timingsField,
                    createTimings,
                    "ts-typewriter-preset-custom-timings-row");
                modeView.Add(TextStudioTypewriterModeField.TimingAsset, customTimingsRow);
                modeView.SetCreateTimingAsset(createTimings);
                HelpBox missingCustomTimings = TextStudioInspectorElements.CreateHelpBox(
                    TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(RevealMode.ByGlyph),
                    HelpBoxMessageType.Warning,
                    "ts-typewriter-preset-missing-custom-timings");
                modeView.SetMissingTimingAsset(missingCustomTimings);
                PropertyField revealSpeed = CreateSettingsField(
                    settings.FindPropertyRelative("RevealSpeedMultiplier"),
                    TextStudioTypewriterModePresentation.PlaybackSpeedLabel,
                    "ts-typewriter-preset-speed");
                revealSpeed.tooltip = TextStudioTypewriterModePresentation.PlaybackSpeedTooltip;
                Label timingHelp = TextStudioInspectorElements.CreateMutedText(
                    string.Empty,
                    "ts-typewriter-preset-timing-help");
                modeView.Add(TextStudioTypewriterModeField.CadencePreset, timingPresetRow);
                modeView.Add(TextStudioTypewriterModeField.CadencePreset, timingHelp);
                PropertyField timingMode = CreateSettingsField(
                    settings.FindPropertyRelative("TimingMode"),
                    "Timing",
                    "ts-typewriter-preset-timing-mode");
                modeView.Add(TextStudioTypewriterModeField.TimingMode, timingMode);
                PropertyField revealDuration = CreateSettingsField(
                    settings.FindPropertyRelative("RevealAppearanceDuration"),
                    "Reveal Duration",
                    "ts-typewriter-preset-reveal-duration");
                modeView.Add(TextStudioTypewriterModeField.RevealDuration, revealDuration);
                root.Add(TextStudioInspectorElements.CreateSection(
                    "Reveal Timing",
                    "ts-typewriter-preset-timing",
                    revealModeField,
                    modeGuidance,
                    timingMode,
                    timingPresetRow,
                    timingHelp,
                    customTimingsRow,
                    missingCustomTimings,
                    waitAfterFinalRevealField,
                    revealSpeed,
                    revealDuration,
                    CreateSettingsField(settings.FindPropertyRelative("RevealDisappearanceDuration"), "Hide Effect Duration")));

                VisualElement phaseEffects = CreateTypewriterEffectsSection(settings, modeView);
                root.Add(phaseEffects);

                VisualElement writingOptions = new VisualElement();
                writingOptions.Add(CreateSettingsField(settings.FindPropertyRelative("WritingStyle"), "Writing Style"));
                writingOptions.Add(CreateSettingsField(settings.FindPropertyRelative("EraseOnHide"), "Erase on Hide"));
                modeView.Add(TextStudioTypewriterModeField.WritingOptions, writingOptions);
                Label writingGuidance = TextStudioInspectorElements.CreateMutedText(
                    string.Empty,
                    "ts-writing-instant-guidance");
                modeView.SetWritingGuidance(writingGuidance);
                root.Add(TextStudioInspectorElements.CreateSection(
                    "Writing",
                    "ts-typewriter-preset-writing",
                    CreateSettingsField(writingEnabled, "Enable Writing"),
                    writingGuidance,
                    writingOptions));

                PropertyField loop = CreateSettingsField(
                    loopProperty,
                    "Repeat Reveal",
                    "ts-typewriter-preset-loop");
                PropertyField loopDelay = CreateSettingsField(
                    settings.FindPropertyRelative("LoopRevealDelay"),
                    "Delay Between Repeats",
                    "ts-typewriter-preset-loop-delay");
                modeView.Add(TextStudioTypewriterModeField.RepeatDelay, loopDelay);
                PropertyField revealSequence = CreateSettingsField(
                    settings.FindPropertyRelative("RevealSequence"),
                    "Reveal Sequence",
                    "ts-typewriter-preset-reveal-sequence");
                modeView.Add(TextStudioTypewriterModeField.RevealSequence, revealSequence);
                root.Add(TextStudioInspectorElements.CreateSection(
                    "Playback",
                    "ts-typewriter-preset-playback",
                    CreateSettingsField(settings.FindPropertyRelative("StartMode"), "Start"),
                    CreateSettingsField(settings.FindPropertyRelative("TextChangeMode"), "When Replacing Text"),
                    CreateSettingsField(settings.FindPropertyRelative("TextChangeScope"), "Affected Text"),
                    TextStudioInspectorElements.CreateMutedText(
                        "Manual waits for an explicit Show call. On Enable starts with the current text. On Text Changed starts after each source text change; the combined option does either."),
                    revealSequence,
                    CreateSettingsField(settings.FindPropertyRelative("HideSequence"), "Hide Sequence"),
                    CreateSettingsField(settings.FindPropertyRelative("ResetSpeedOnNewText"), "Reset Speed On New Text"),
                    loop,
                    loopDelay));

                PropertyField skipReveal = CreateSettingsField(
                    settings.FindPropertyRelative("SkipRevealMode"),
                    "Reveal Effects",
                    "ts-typewriter-preset-skip-reveal");
                Label skipRevealHelp = TextStudioInspectorElements.CreateMutedText(
                    "Settle immediately snaps reveal effects to their final state. Allow to finish reveals the remaining text while its entrance motion completes normally.");
                modeView.Add(TextStudioTypewriterModeField.SkipReveal, skipReveal);
                modeView.Add(TextStudioTypewriterModeField.SkipReveal, skipRevealHelp);
                root.Add(TextStudioInspectorElements.CreateSection(
                    "Skip Behavior",
                    "ts-typewriter-preset-skip",
                    skipReveal,
                    skipRevealHelp,
                    CreateSettingsField(settings.FindPropertyRelative("SkipCurrentWaitOnSkip"), "Skip Current Wait"),
                    TextStudioInspectorElements.CreateMutedText(
                        "When enabled, a skip also releases the currently active timed or input wait. Later waits are still processed normally."),
                    CreateSettingsField(settings.FindPropertyRelative("TriggerRemainingMarkersOnSkip"), "Trigger Remaining Markers"),
                    TextStudioInspectorElements.CreateMutedText(
                        "Enable only when markers represent required state changes. Leave disabled when skipped dialogue should not fire unseen presentation events."),
                    CreateSettingsField(settings.FindPropertyRelative("SkipHideMode"), "Hide Effect")));

                void RefreshTimingVisibility()
                {
                    if (timings.objectReferenceValue != null)
                        customTimingSelected = true;
                    int selectedIndex = customTimingSelected
                        ? timingPresets.Length
                        : TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                            timingPresets,
                            (RevealTimingsPreset)builtInTimings.enumValueIndex,
                            false);
                    timingPreset.SetValueWithoutNotify(timingChoices[selectedIndex]);
                    timingHelp.text = TextStudioInspectorElements.GetTimingPresetDescription(
                        timingPresets,
                        selectedIndex);
                    modeView.Refresh();
                }
                timingPreset.RegisterValueChangedCallback(evt =>
                {
                    int selectedIndex = timingChoices.IndexOf(evt.newValue);
                    if (selectedIndex < 0 || selectedIndex > timingPresets.Length)
                        return;

                    customTimingSelected = selectedIndex == timingPresets.Length;
                    if (!customTimingSelected)
                    {
                        serializedObject.Update();
                        builtInTimings.enumValueIndex = (int)timingPresets[selectedIndex];
                        timings.objectReferenceValue = null;
                        serializedObject.ApplyModifiedProperties();
                    }

                    RefreshTimingVisibility();
                });
                root.TrackPropertyValue(timings, _ => RefreshTimingVisibility());
                root.TrackPropertyValue(builtInTimings, _ => RefreshTimingVisibility());
                modeView.Track(
                    root,
                    revealMode,
                    loopProperty,
                    writingEnabled,
                    revealEffect,
                    hideEffect);
                RefreshTimingVisibility();
            }

            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                if (target is TextStudioTypewriterPreset preset)
                    _preview?.Refresh(preset);
            });
            if (target is TextStudioTypewriterPreset currentPreset)
                _preview.Refresh(currentPreset);

            return root;
        }

        VisualElement CreateTypewriterEffectsSection(SerializedProperty settings, TextStudioTypewriterModeView modeView)
        {
            SerializedProperty revealEffectProperty = settings.FindPropertyRelative("RevealEffect");
            SerializedProperty hideEffectProperty = settings.FindPropertyRelative("HideEffect");
            SerializedProperty revealExpressionProperty = settings.FindPropertyRelative("RevealEffectExpression");
            SerializedProperty hideExpressionProperty = settings.FindPropertyRelative("HideEffectExpression");
            PropertyField revealEffect = CreateSettingsField(
                revealEffectProperty,
                "Reveal Effect",
                "ts-typewriter-preset-reveal-effect");
            PropertyField hideEffect = CreateSettingsField(
                hideEffectProperty,
                "Hide Effect",
                "ts-typewriter-preset-hide-effect");
            PropertyField revealExpression = CreateSettingsField(
                revealExpressionProperty,
                "Inline Reveal Tags",
                "ts-typewriter-preset-reveal-expression");
            PropertyField hideExpression = CreateSettingsField(
                hideExpressionProperty,
                "Inline Hide Tags",
                "ts-typewriter-preset-hide-expression");
            revealEffect.tooltip = "Optional shared effect definition applied only while each semantic unit appears.";
            hideEffect.tooltip = "Optional shared effect definition applied only while each semantic unit disappears.";
            revealExpression.tooltip = "Optional built-in or inline effect tags applied when each semantic unit appears. A custom effect asset replaces these tags.";
            hideExpression.tooltip = "Optional built-in or inline effect tags applied when each semantic unit disappears. A custom effect asset replaces these tags.";
            modeView.Add(TextStudioTypewriterModeField.RevealEffect, revealEffect);
            modeView.Add(TextStudioTypewriterModeField.RevealEffectTags, revealExpression);
            modeView.Add(TextStudioTypewriterModeField.HideEffectTags, hideExpression);
            VisualElement section = TextStudioInspectorElements.CreateSection(
                TextStudioTypewriterModePresentation.RevealEffectsSectionTitle,
                "ts-typewriter-preset-effects",
                revealEffect,
                hideEffect,
                revealExpression,
                hideExpression,
                TextStudioInspectorElements.CreateMutedText(
                    "Each phase uses one shared custom effect asset or built-in and inline effect tags. Timing remains owned by this Typewriter preset."));
            modeView.SetEffectsSection(section);
            revealEffect.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (revealEffectProperty.objectReferenceValue != null)
                    revealExpressionProperty.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            hideEffect.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (hideEffectProperty.objectReferenceValue != null)
                    hideExpressionProperty.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            revealExpression.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(revealExpressionProperty.stringValue))
                    revealEffectProperty.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            hideExpression.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(hideExpressionProperty.stringValue))
                    hideEffectProperty.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            return section;
        }

        static PropertyField CreateSettingsField(
            SerializedProperty property,
            string label,
            string name = null)
        {
            if (property == null)
                return null;

            PropertyField field = new PropertyField(property, label)
            {
                name = name
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList("unity-base-field__aligned");
            return field;
        }

        void CreateTimingsAsset(SerializedProperty timingsProperty)
        {
            CharacterTimings timings = TextStudioInspectorElements.CreateCharacterTimingsAsset();
            if (timings == null)
                return;

            serializedObject.Update();
            timingsProperty.objectReferenceValue = timings;
            serializedObject.ApplyModifiedProperties();
        }

    }
}
