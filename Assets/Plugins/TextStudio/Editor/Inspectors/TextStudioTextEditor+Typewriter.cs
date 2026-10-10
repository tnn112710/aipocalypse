using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        VisualElement BuildUitkTypewriterTab(TextStudioText hub)
        {
            if (hub.UsesBehaviorProfileTypewriter)
                return BuildProfileTypewriterTab(hub);

            VisualElement root = new VisualElement();
            root.AddToClassList("ts-typewriter-tab");

            VisualElement source = CreateSection("Typewriter");
            source.name = "ts-typewriter-recipe";
            TextStudioTypewriterSource renderedSource = GetCurrentTypewriterSource();
            PropertyField sourceField = CreateBoundPropertyField(_typewriterSource, "Source");
            sourceField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                HandleComponentTypewriterSourceChanged(
                    hub,
                    renderedSource,
                    GetTypewriterSource(evt.changedProperty)));
            source.Add(sourceField);
            source.Add(CreateAlignedHint(
                "None keeps text visible immediately. Built-In and Custom are live preset references. Inline stores values on this component.",
                "ts-typewriter-source-hint"));

            TextStudioTypewriterSource typewriterSource = renderedSource;
            if (typewriterSource == TextStudioTypewriterSource.None)
            {
                root.Add(source);
                return root;
            }

            if (typewriterSource == TextStudioTypewriterSource.BuiltIn)
            {
                AddBuiltInTypewriterPresetControls(source, hub);
                root.Add(source);
                return root;
            }

            if (typewriterSource == TextStudioTypewriterSource.Custom)
            {
                AddCustomTypewriterPresetControls(source, hub);
                root.Add(source);
                return root;
            }

            Button save = TextStudioTypewriterPresetConversion.CreateSaveButton(CreateTypewriterPresetAsset);
            save.SetEnabled(!serializedObject.isEditingMultipleObjects);
            source.Add(save);
            root.Add(source);

            VisualElement inlineSettings = new VisualElement
            {
                name = "ts-inline-typewriter-settings"
            };
            inlineSettings.AddToClassList("ts-inline-typewriter-settings");
            root.Add(inlineSettings);

            RevealTimingsPreset[] timingPresets;
            List<string> timingChoices = TextStudioInspectorElements.CreateTimingPresetChoices(out timingPresets);
            int timingChoiceIndex = TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                timingPresets,
                (RevealTimingsPreset)_builtInTimingsPreset.enumValueIndex,
                _timings.objectReferenceValue != null);
            bool customTimingSelected = timingChoiceIndex == timingPresets.Length;
            SerializedProperty writingEnabled = serializedObject.FindProperty("_writingEnabled");
            TextStudioTypewriterModeView modeView = new TextStudioTypewriterModeView(() =>
                TextStudioTypewriterModeState.Read(
                    _revealMode,
                    customTimingSelected,
                    _timings,
                    _loopReveal,
                    writingEnabled,
                    _typewriterRevealEffect,
                    _typewriterHideEffect));

            VisualElement timing = CreateSection("Reveal Timing");
            PropertyField modeField = CreateBoundPropertyField(_revealMode, TextStudioTypewriterModePresentation.ModeLabel);
            modeField.tooltip = TextStudioTypewriterModePresentation.ModeTooltip;
            timing.Add(modeField);
            Label modeGuidance = CreateAlignedHintLabel(string.Empty, "ts-typewriter-mode-guidance");
            timing.Add(CreateAlignedHintRow(modeGuidance));
            modeView.SetGuidance(modeGuidance);
            PropertyField timingMode = CreateBoundPropertyField(serializedObject.FindProperty("_typewriterTimingMode"), "Timing");
            timing.Add(timingMode);
            modeView.Add(TextStudioTypewriterModeField.TimingMode, timingMode);
            DropdownField timingPreset = new DropdownField(timingChoices, timingChoiceIndex)
            {
                name = "ts-timings-preset-picker",
                tooltip = "Choose a built-in reveal cadence or Custom to use a reusable timing asset."
            };
            VisualElement timingPresetRow = TextStudioInspectorElements.CreateAlignedControlRow(
                "Preset",
                timingPreset,
                "ts-timings-preset-row");
            timing.Add(timingPresetRow);
            modeView.Add(TextStudioTypewriterModeField.CadencePreset, timingPresetRow);
            Label presetDescription = CreateAlignedHintLabel(string.Empty, "ts-timings-preset-description");
            VisualElement presetDescriptionRow = CreateAlignedHintRow(presetDescription);
            timing.Add(presetDescriptionRow);
            modeView.Add(TextStudioTypewriterModeField.CadencePreset, presetDescriptionRow);

            ObjectField customTimingsField;
            VisualElement customRow = BuildAssetReferenceRow(
                _timings,
                "Custom Asset",
                typeof(TypingTimingsBase),
                "ts-timings-row",
                "ts-timings-create-button",
                "Create a custom timing asset.",
                CreateTimingsAsset,
                out customTimingsField,
                trackCreateAvailability: false);
            customTimingsField.tooltip = "Reusable timing asset used instead of the selected built-in cadence. In Custom mode it also defines reveal groups.";
            timing.Add(customRow);
            modeView.Add(TextStudioTypewriterModeField.TimingAsset, customRow);
            modeView.SetCreateTimingAsset(customRow.Q<Button>("ts-timings-create-button"));
            HelpBox missingCustomTimings = CreateHelpBox(
                TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(RevealMode.ByGlyph),
                HelpBoxMessageType.Warning);
            missingCustomTimings.name = "ts-timings-missing-help";
            timing.Add(missingCustomTimings);
            modeView.SetMissingTimingAsset(missingCustomTimings);
            PropertyField waitAfterFinalReveal = CreateBoundPropertyField(
                _waitAfterFinalReveal,
                "Wait After Final Reveal");
            waitAfterFinalReveal.name = "ts-typewriter-wait-after-final-reveal";
            waitAfterFinalReveal.tooltip =
                "With a built-in cadence, include the final unit's trailing delay before completion. Custom timing assets own this choice.";
            waitAfterFinalReveal.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.WaitAfterFinalReveal = _waitAfterFinalReveal.boolValue;
                RestartPreviewAfterTypewriterChange(hub);
            });
            timing.Add(waitAfterFinalReveal);
            modeView.Add(TextStudioTypewriterModeField.FinalRevealWait, waitAfterFinalReveal);
            PropertyField revealSpeed = CreateBoundPropertyField(
                _revealSpeedMultiplier,
                TextStudioTypewriterModePresentation.PlaybackSpeedLabel);
            revealSpeed.tooltip = TextStudioTypewriterModePresentation.PlaybackSpeedTooltip;
            revealSpeed.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetRevealSpeed(_revealSpeedMultiplier.floatValue);
            });
            timing.Add(revealSpeed);
            inlineSettings.Add(timing);

            inlineSettings.Add(BuildFrameTimingSettings());

            VisualElement playback = CreateSection(TextStudioTypewriterModePresentation.RevealDurationSectionTitle);
            modeView.SetDurationSection(playback);
            PropertyField appearance = CreateBoundPropertyField(_revealAppearanceDuration, "Reveal Duration");
            modeView.Add(TextStudioTypewriterModeField.RevealDuration, appearance);
            appearance.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetRevealAppearanceDuration(_revealAppearanceDuration.floatValue);
            });
            playback.Add(appearance);

            PropertyField disappearance = CreateBoundPropertyField(_revealDisappearanceDuration, "Hide Duration");
            disappearance.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetRevealDisappearanceDuration(_revealDisappearanceDuration.floatValue);
            });
            playback.Add(disappearance);

            PropertyField loopReveal = CreateBoundPropertyField(_loopReveal, "Repeat Reveal");
            loopReveal.tooltip = "Repeat the authored reveal lifecycle at runtime. The Live Preview Repeat button is independent.";
            loopReveal.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetRevealLoop(_loopReveal.boolValue, _loopRevealDelay.floatValue);
            });
            playback.Add(loopReveal);

            PropertyField loopDelay = CreateBoundPropertyField(_loopRevealDelay, "Repeat Delay");
            loopDelay.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetRevealLoop(_loopReveal.boolValue, _loopRevealDelay.floatValue);
            });
            playback.Add(loopDelay);
            modeView.Add(TextStudioTypewriterModeField.RepeatDelay, loopDelay);

            inlineSettings.Add(playback);
            inlineSettings.Add(BuildTypewriterBehaviorSection(hub, modeView));
            inlineSettings.Add(BuildTypewriterSkipSection(hub, modeView));
            inlineSettings.Add(BuildTransitionStyleSection(hub, modeView));
            inlineSettings.Add(BuildWritingSection(hub, modeView));

            Action refreshTimingState = () =>
            {
                if (_timings.objectReferenceValue != null)
                    customTimingSelected = true;
                int selectedIndex = customTimingSelected
                    ? timingPresets.Length
                    : TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                        timingPresets,
                        (RevealTimingsPreset)_builtInTimingsPreset.enumValueIndex,
                        false);
                timingPreset.SetValueWithoutNotify(timingChoices[selectedIndex]);
                presetDescription.text = TextStudioInspectorElements.GetTimingPresetDescription(
                    timingPresets,
                    selectedIndex);
                modeView.Refresh();
            };
            timingPreset.RegisterValueChangedCallback(evt =>
            {
                int selectedIndex = timingChoices.IndexOf(evt.newValue);
                if (selectedIndex < 0 || selectedIndex > timingPresets.Length)
                    return;

                customTimingSelected = selectedIndex == timingPresets.Length;
                if (!customTimingSelected)
                {
                    serializedObject.Update();
                    _builtInTimingsPreset.enumValueIndex = (int)timingPresets[selectedIndex];
                    _timings.objectReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                    RestartPreviewAfterTypewriterChange(hub);
                    EditorUtility.SetDirty(hub);
                }

                refreshTimingState();
            });
            inlineSettings.TrackPropertyValue(_timings, _ => refreshTimingState());
            inlineSettings.TrackPropertyValue(_builtInTimingsPreset, _ => refreshTimingState());
            modeView.Track(
                inlineSettings,
                _revealMode,
                _loopReveal,
                writingEnabled,
                _typewriterRevealEffect,
                _typewriterHideEffect);
            refreshTimingState();

            return root;
        }

        VisualElement BuildProfileTypewriterTab(TextStudioText hub)
        {
            TextStudioBehaviorProfile profile = hub.BehaviorProfile;
            SerializedObject profileObject = new SerializedObject(profile);
            bool editable = _isEditingAssignedProfile;
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-typewriter-tab");

            SerializedProperty sourceProperty = profileObject.FindProperty("_typewriter").FindPropertyRelative("Source");
            VisualElement source = CreateSection("Typewriter");
            source.name = "ts-typewriter-recipe";
            PropertyField sourceField = CreateVisualSettingsPropertyField(sourceProperty, "Source", editable);
            source.Add(sourceField);
            source.Add(CreateAlignedHint(
                editable
                    ? "This typewriter source edits the assigned profile and affects every component using it."
                    : "This typewriter source comes from the assigned profile. Enable profile editing here to change it.",
                "ts-typewriter-source-hint"));
            source.Add(CreateProfileEditButton(hub, "ts-profile-typewriter-edit-button"));
            if (_isEditingAssignedProfile)
                source.Add(CreateProfileGlobalEditWarning());

            sourceField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!editable)
                    return;

                profileObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                RestartPreviewAfterTypewriterChange(hub);
                RequestUitkActiveTabRebuild(hub);
            });

            TextStudioTypewriterSource typewriterSource = (TextStudioTypewriterSource)sourceProperty.enumValueIndex;
            if (typewriterSource == TextStudioTypewriterSource.None)
            {
                root.Add(source);
                BindProfileOwnedSubtree(root, profileObject);
                return root;
            }

            if (typewriterSource == TextStudioTypewriterSource.BuiltIn)
            {
                AddProfileBuiltInTypewriterPresetControls(source, hub, profile, profileObject, editable);
                root.Add(source);
                BindProfileOwnedSubtree(root, profileObject);
                return root;
            }

            if (typewriterSource == TextStudioTypewriterSource.Custom)
            {
                AddProfileCustomTypewriterPresetControls(source, hub, profile, profileObject, editable);
                root.Add(source);
                BindProfileOwnedSubtree(root, profileObject);
                return root;
            }

            Button save = TextStudioTypewriterPresetConversion.CreateSaveButton(() =>
                CreateProfileTypewriterPresetAsset(
                    profile, profileObject, hub));
            save.SetEnabled(editable && !serializedObject.isEditingMultipleObjects);
            source.Add(save);
            root.Add(source);

            VisualElement inlineSettings = new VisualElement
            {
                name = "ts-inline-typewriter-settings"
            };
            inlineSettings.AddToClassList("ts-inline-typewriter-settings");
            root.Add(inlineSettings);

            SerializedProperty inline = profileObject.FindProperty("_typewriter").FindPropertyRelative("InlineSettings");
            SerializedProperty profileRevealMode = inline.FindPropertyRelative("RevealMode");
            SerializedProperty profileBuiltInTimings = inline.FindPropertyRelative("BuiltInTimingsPreset");
            SerializedProperty profileCustomTimings = inline.FindPropertyRelative("Timings");
            SerializedProperty profileLoopRevealProperty = inline.FindPropertyRelative("LoopReveal");
            SerializedProperty writingEnabled = inline.FindPropertyRelative("WritingEnabled");
            SerializedProperty profileRevealEffect = inline.FindPropertyRelative("RevealEffect");
            SerializedProperty profileHideEffect = inline.FindPropertyRelative("HideEffect");
            RevealTimingsPreset[] profileTimingPresets;
            List<string> profileTimingChoices = TextStudioInspectorElements.CreateTimingPresetChoices(out profileTimingPresets);
            int profileTimingChoiceIndex = TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                profileTimingPresets,
                (RevealTimingsPreset)profileBuiltInTimings.enumValueIndex,
                profileCustomTimings.objectReferenceValue != null);
            bool profileCustomTimingSelected = profileTimingChoiceIndex == profileTimingPresets.Length;
            TextStudioTypewriterModeView modeView = new TextStudioTypewriterModeView(() =>
                TextStudioTypewriterModeState.Read(
                    profileRevealMode,
                    profileCustomTimingSelected,
                    profileCustomTimings,
                    profileLoopRevealProperty,
                    writingEnabled,
                    profileRevealEffect,
                    profileHideEffect));

            VisualElement timing = CreateSection("Reveal Timing");
            PropertyField profileModeField = CreateVisualSettingsPropertyField(
                profileRevealMode,
                TextStudioTypewriterModePresentation.ModeLabel,
                editable);
            profileModeField.tooltip = TextStudioTypewriterModePresentation.ModeTooltip;
            timing.Add(profileModeField);
            Label profileModeGuidance = CreateAlignedHintLabel(string.Empty, "ts-typewriter-mode-guidance");
            timing.Add(CreateAlignedHintRow(profileModeGuidance));
            modeView.SetGuidance(profileModeGuidance);
            PropertyField profileTimingMode = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("TimingMode"),
                "Timing",
                editable);
            timing.Add(profileTimingMode);
            modeView.Add(TextStudioTypewriterModeField.TimingMode, profileTimingMode);
            DropdownField profileTimingPreset = new DropdownField(
                profileTimingChoices,
                profileTimingChoiceIndex)
            {
                name = "ts-timings-preset-picker",
                tooltip = "Choose a built-in reveal cadence or Custom to use a reusable timing asset."
            };
            profileTimingPreset.SetEnabled(editable);
            VisualElement profileTimingPresetRow = TextStudioInspectorElements.CreateAlignedControlRow(
                "Preset",
                profileTimingPreset,
                "ts-timings-preset-row");
            timing.Add(profileTimingPresetRow);
            modeView.Add(TextStudioTypewriterModeField.CadencePreset, profileTimingPresetRow);
            Label profileTimingDescription = CreateAlignedHintLabel(
                string.Empty,
                "ts-timings-preset-description");
            VisualElement profileTimingDescriptionRow = CreateAlignedHintRow(profileTimingDescription);
            timing.Add(profileTimingDescriptionRow);
            modeView.Add(TextStudioTypewriterModeField.CadencePreset, profileTimingDescriptionRow);
            PropertyField profileCustomTimingField = CreateVisualSettingsPropertyField(
                profileCustomTimings,
                "Custom Asset",
                editable);
            profileCustomTimingField.tooltip = "Reusable timing asset used instead of the selected built-in cadence. In Custom mode it also defines reveal groups.";
            Button createProfileTimings = TextStudioInspectorElements.CreateCompactCreateButton(
                () => CreateProfileTimingsAsset(profileCustomTimings, profile, profileObject, hub),
                "ts-timings-create-button",
                "Create and assign a custom timing asset.");
            createProfileTimings.SetEnabled(editable);
            VisualElement profileCustomTimingRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                profileCustomTimingField,
                createProfileTimings,
                "ts-timings-row");
            timing.Add(profileCustomTimingRow);
            modeView.Add(TextStudioTypewriterModeField.TimingAsset, profileCustomTimingRow);
            modeView.SetCreateTimingAsset(createProfileTimings, editable);
            HelpBox missingProfileCustomTimings = CreateHelpBox(
                TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(RevealMode.ByGlyph),
                HelpBoxMessageType.Warning);
            missingProfileCustomTimings.name = "ts-timings-missing-help";
            timing.Add(missingProfileCustomTimings);
            modeView.SetMissingTimingAsset(missingProfileCustomTimings);
            PropertyField profileWaitAfterFinalReveal = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("WaitAfterFinalReveal"),
                "Wait After Final Reveal",
                editable);
            profileWaitAfterFinalReveal.name = "ts-typewriter-wait-after-final-reveal";
            profileWaitAfterFinalReveal.tooltip =
                "With a built-in cadence, include the final unit's trailing delay before completion. Custom timing assets own this choice.";
            timing.Add(profileWaitAfterFinalReveal);
            modeView.Add(TextStudioTypewriterModeField.FinalRevealWait, profileWaitAfterFinalReveal);
            PropertyField profileRevealSpeed = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("RevealSpeedMultiplier"),
                TextStudioTypewriterModePresentation.PlaybackSpeedLabel,
                editable);
            profileRevealSpeed.tooltip = TextStudioTypewriterModePresentation.PlaybackSpeedTooltip + " It applies to every component using this profile.";
            timing.Add(profileRevealSpeed);
            inlineSettings.Add(timing);

            Action refreshProfileTimingState = () =>
            {
                if (profileCustomTimings.objectReferenceValue != null)
                    profileCustomTimingSelected = true;
                int selectedIndex = profileCustomTimingSelected
                    ? profileTimingPresets.Length
                    : TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                        profileTimingPresets,
                        (RevealTimingsPreset)profileBuiltInTimings.enumValueIndex,
                        false);
                profileTimingPreset.SetValueWithoutNotify(profileTimingChoices[selectedIndex]);
                profileTimingDescription.text = TextStudioInspectorElements.GetTimingPresetDescription(
                    profileTimingPresets,
                    selectedIndex);
                modeView.Refresh();
            };
            profileTimingPreset.RegisterValueChangedCallback(evt =>
            {
                if (!editable)
                    return;

                int selectedIndex = profileTimingChoices.IndexOf(evt.newValue);
                if (selectedIndex < 0 || selectedIndex > profileTimingPresets.Length)
                    return;

                profileCustomTimingSelected = selectedIndex == profileTimingPresets.Length;
                if (!profileCustomTimingSelected)
                {
                    profileObject.Update();
                    profileBuiltInTimings.enumValueIndex = (int)profileTimingPresets[selectedIndex];
                    profileCustomTimings.objectReferenceValue = null;
                    profileObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(profile);
                    RestartPreviewAfterTypewriterChange(hub);
                }

                refreshProfileTimingState();
            });
            timing.TrackPropertyValue(profileBuiltInTimings, _ => refreshProfileTimingState());
            timing.TrackPropertyValue(profileCustomTimings, _ => refreshProfileTimingState());

            VisualElement playback = CreateSection(TextStudioTypewriterModePresentation.RevealDurationSectionTitle);
            modeView.SetDurationSection(playback);
            PropertyField profileRevealDuration = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("RevealAppearanceDuration"),
                "Reveal Duration",
                editable);
            playback.Add(profileRevealDuration);
            modeView.Add(TextStudioTypewriterModeField.RevealDuration, profileRevealDuration);
            playback.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("RevealDisappearanceDuration"), "Hide Duration", editable));
            PropertyField profileLoopReveal = CreateVisualSettingsPropertyField(
                profileLoopRevealProperty,
                "Repeat Reveal",
                editable);
            profileLoopReveal.tooltip = "Repeat the authored reveal lifecycle at runtime. The Live Preview Repeat button is independent.";
            playback.Add(profileLoopReveal);
            PropertyField profileLoopDelay = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("LoopRevealDelay"),
                "Repeat Delay",
                editable);
            profileLoopDelay.tooltip = "Seconds to wait before an authored runtime reveal repeats.";
            playback.Add(profileLoopDelay);
            modeView.Add(TextStudioTypewriterModeField.RepeatDelay, profileLoopDelay);
            inlineSettings.Add(playback);

            VisualElement behavior = CreateSection("Behavior");
            behavior.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("StartMode"), "Start", editable));
            behavior.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("TextChangeMode"), "When Replacing Text", editable));
            behavior.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("TextChangeScope"), "Affected Text", editable));
            behavior.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("ResetSpeedOnNewText"), "Reset Speed On New Text", editable));
            PropertyField profileRevealSequence = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("RevealSequence"),
                "Reveal Sequence",
                editable);
            behavior.Add(profileRevealSequence);
            modeView.Add(TextStudioTypewriterModeField.RevealSequence, profileRevealSequence);
            behavior.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("HideSequence"), "Hide Sequence", editable));
            inlineSettings.Add(behavior);

            VisualElement skip = CreateSection("Skip Policy");
            PropertyField profileSkipReveal = CreateVisualSettingsPropertyField(
                inline.FindPropertyRelative("SkipRevealMode"),
                "Reveal Effects",
                editable);
            skip.Add(profileSkipReveal);
            modeView.Add(TextStudioTypewriterModeField.SkipReveal, profileSkipReveal);
            skip.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("SkipCurrentWaitOnSkip"), "Skip Current Wait", editable));
            skip.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("TriggerRemainingMarkersOnSkip"), "Trigger Remaining Markers", editable));
            skip.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("SkipHideMode"), "Hide Effects on Skip", editable));
            inlineSettings.Add(skip);

            inlineSettings.Add(BuildProfileInlineTransitionStyleSection(
                inline,
                hub,
                profile,
                profileObject,
                editable,
                modeView));

            VisualElement writing = CreateSection("Writing");
            writing.Add(CreateVisualSettingsPropertyField(writingEnabled, "Enable Writing", editable));
            Label writingGuidance = CreateAlignedHintLabel(string.Empty, "ts-writing-instant-guidance");
            VisualElement writingGuidanceRow = CreateAlignedHintRow(writingGuidance);
            writing.Add(writingGuidanceRow);
            modeView.SetWritingGuidance(writingGuidance, writingGuidanceRow);
            VisualElement writingOptions = new VisualElement();
            writingOptions.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("WritingStyle"), "Writing Style", editable));
            writingOptions.Add(CreateVisualSettingsPropertyField(inline.FindPropertyRelative("EraseOnHide"), "Erase on Hide", editable));
            writing.Add(writingOptions);
            modeView.Add(TextStudioTypewriterModeField.WritingOptions, writingOptions);
            inlineSettings.Add(writing);

            inlineSettings.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!editable)
                    return;

                profileObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                RestartPreviewAfterTypewriterChange(hub);
            });
            modeView.Track(
                inlineSettings,
                profileRevealMode,
                profileLoopRevealProperty,
                writingEnabled,
                profileRevealEffect,
                profileHideEffect);
            refreshProfileTimingState();

            BindProfileOwnedSubtree(root, profileObject);
            return root;
        }

        void BindProfileOwnedSubtree(VisualElement root, SerializedObject profileObject)
        {
            root.Bind(profileObject);
        }

        VisualElement BuildProfileInlineTransitionStyleSection(
            SerializedProperty inline,
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            bool editable,
            TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection(TextStudioTypewriterModePresentation.RevealEffectsSectionTitle);
            modeView.SetEffectsSection(section);
            SerializedProperty revealEffect = inline.FindPropertyRelative("RevealEffect");
            SerializedProperty hideEffect = inline.FindPropertyRelative("HideEffect");
            PropertyField revealField = CreateVisualSettingsPropertyField(
                revealEffect,
                "Reveal Effect",
                editable);
            revealField.name = "ts-typewriter-reveal-effect";
            revealField.tooltip =
                "Optional shared text effect applied only while each semantic unit appears. A Composed Effect can contain multiple layers.";
            PropertyField hideField = CreateVisualSettingsPropertyField(
                hideEffect,
                "Hide Effect",
                editable);
            SerializedProperty revealExpression = inline.FindPropertyRelative("RevealEffectExpression");
            SerializedProperty hideExpression = inline.FindPropertyRelative("HideEffectExpression");
            PropertyField revealExpressionField = CreateVisualSettingsPropertyField(
                revealExpression,
                "Inline Reveal Tags",
                editable);
            PropertyField hideExpressionField = CreateVisualSettingsPropertyField(
                hideExpression,
                "Inline Hide Tags",
                editable);
            hideField.name = "ts-typewriter-hide-effect";
            hideField.tooltip =
                "Optional shared text effect applied only while each semantic unit disappears. A Composed Effect can contain multiple layers.";
            revealExpressionField.name = "ts-typewriter-reveal-expression";
            revealExpressionField.tooltip = "Optional built-in or inline effect tags applied when each semantic unit appears. A custom effect asset replaces these tags.";
            hideExpressionField.name = "ts-typewriter-hide-expression";
            hideExpressionField.tooltip = "Optional built-in or inline effect tags applied when each semantic unit disappears. A custom effect asset replaces these tags.";
            modeView.Add(TextStudioTypewriterModeField.RevealEffect, revealField);
            modeView.Add(TextStudioTypewriterModeField.RevealEffectTags, revealExpressionField);
            modeView.Add(TextStudioTypewriterModeField.HideEffectTags, hideExpressionField);
            Action applyEffects = () =>
            {
                if (!editable)
                    return;
                profileObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                modeView.Refresh();
                RestartPreviewAfterTypewriterChange(hub);
            };
            revealField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            hideField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            revealExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            hideExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            section.Add(revealField);
            section.Add(hideField);
            section.Add(revealExpressionField);
            section.Add(hideExpressionField);
            section.Add(CreateAlignedHint(
                "Choose a shared custom effect asset or enter built-in and inline effect tags for each phase. Typewriter timing remains independent of these visual treatments.",
                "ts-typewriter-phase-effects-hint"));
            return section;
        }
        VisualElement BuildProfileBuiltInTypewriterPresetSection(
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            bool editable)
        {
            VisualElement section = CreateSection("Preset");
            AddProfileBuiltInTypewriterPresetControls(section, hub, profile, profileObject, editable);
            return section;
        }

        void AddProfileBuiltInTypewriterPresetControls(
            VisualElement section,
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            bool editable)
        {
            IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets = BuiltInTypewriterPresets.All();
            List<string> choices = new List<string>(presets.Count);
            for (int i = 0; i < presets.Count; i++)
                choices.Add(presets[i].DisplayName);

            SerializedProperty presetId = profileObject.FindProperty("_typewriter").FindPropertyRelative("BuiltInPresetId");
            int currentIndex = GetTypewriterPresetIndex(presets, presetId.stringValue);
            DropdownField preset = new DropdownField(
                "Built-In Preset",
                choices,
                currentIndex >= 0 ? currentIndex : 0)
            {
                name = "ts-typewriter-preset-picker"
            };
            preset.AddToClassList(UnityAlignedFieldClass);
            preset.SetEnabled(editable);
            Label description = CreateAlignedHintLabel(
                GetTypewriterPresetDescription(presets, currentIndex),
                "ts-typewriter-preset-description");

            preset.RegisterValueChangedCallback(evt =>
            {
                if (!editable)
                    return;

                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || index >= presets.Count)
                    return;

                profileObject.Update();
                presetId.stringValue = presets[index].Id;
                profileObject.ApplyModifiedProperties();
                description.text = presets[index].Description;
                EditorUtility.SetDirty(profile);
                RestartPreviewAfterTypewriterChange(hub);
            });
            Action<SerializedProperty> refreshPreset = trackedPresetId =>
            {
                int syncedIndex = GetTypewriterPresetIndex(presets, trackedPresetId.stringValue);
                if (syncedIndex >= 0 && syncedIndex < choices.Count)
                    preset.SetValueWithoutNotify(choices[syncedIndex]);
                description.text = GetTypewriterPresetDescription(presets, syncedIndex);
            };
            section.TrackPropertyValue(presetId, refreshPreset);

            section.Add(preset);
            section.Add(CreateAlignedHintRow(description));
            Button convert = CreateTypewriterPresetConversionButton(() =>
                ConvertProfileTypewriterPresetToInline(hub, profile, profileObject));
            convert.SetEnabled(editable && TextStudioTypewriterPresetConversion.CanConvert(profile));
            section.Add(convert);
        }

        VisualElement BuildProfileCustomTypewriterPresetSection(
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            bool editable)
        {
            VisualElement section = CreateSection("Preset");
            AddProfileCustomTypewriterPresetControls(section, hub, profile, profileObject, editable);
            return section;
        }

        void AddProfileCustomTypewriterPresetControls(
            VisualElement section,
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            bool editable)
        {
            SerializedProperty presetProperty = profileObject.FindProperty("_typewriter").FindPropertyRelative("CustomPreset");
            ObjectField field;
            VisualElement row = BuildAssetReferenceRow(
                presetProperty,
                "Custom Asset",
                typeof(TextStudioTypewriterPreset),
                "ts-typewriter-preset-row",
                "ts-typewriter-preset-create-button",
                "Create a reusable typewriter preset asset from the current profile inline settings.",
                () => CreateProfileTypewriterPresetAsset(
                    profile,
                    profileObject,
                    hub),
                out field);
            row.SetEnabled(editable);
            field.bindingPath = string.Empty;
            field.BindProperty(presetProperty);
            field.RegisterValueChangedCallback(_ =>
            {
                if (!editable)
                    return;

                EditorUtility.SetDirty(profile);
                RestartPreviewAfterTypewriterChange(hub);
            });
            section.Add(row);
            section.Add(CreateAlignedHint(
                "The selected preset is a live profile reference. Editing the asset updates every Text Studio object using this profile.",
                "ts-typewriter-preset-description"));
            Button convert = CreateTypewriterPresetConversionButton(() =>
                ConvertProfileTypewriterPresetToInline(hub, profile, profileObject));
            convert.SetEnabled(editable && presetProperty.objectReferenceValue != null);
            section.Add(convert);
            section.TrackPropertyValue(
                presetProperty,
                _ => convert.SetEnabled(editable && presetProperty.objectReferenceValue != null));
        }

        VisualElement BuildBuiltInTypewriterPresetSection(TextStudioText hub)
        {
            VisualElement section = CreateSection("Preset");
            AddBuiltInTypewriterPresetControls(section, hub);
            return section;
        }

        void AddBuiltInTypewriterPresetControls(VisualElement section, TextStudioText hub)
        {
            IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets = BuiltInTypewriterPresets.All();
            List<string> choices = new List<string>(presets.Count);
            for (int i = 0; i < presets.Count; i++)
                choices.Add(presets[i].DisplayName);

            int currentIndex = GetTypewriterPresetIndex(presets, _typewriterPresetId.stringValue);
            DropdownField preset = new DropdownField("Preset", choices, Mathf.Max(0, currentIndex))
            {
                name = "ts-typewriter-preset-picker"
            };
            preset.AddToClassList(UnityAlignedFieldClass);
            Label description = CreateAlignedHintLabel(
                GetTypewriterPresetDescription(presets, currentIndex),
                "ts-typewriter-preset-description");

            preset.RegisterValueChangedCallback(evt =>
            {
                int selectedIndex = choices.IndexOf(evt.newValue);
                if (selectedIndex < 0 || selectedIndex >= presets.Count)
                    return;

                ApplyBuiltInTypewriterPreset(presets[selectedIndex], hub);
                description.text = presets[selectedIndex].Description;
            });

            section.Add(preset);
            section.Add(CreateAlignedHintRow(description));
            section.Add(CreateTypewriterPresetConversionButton(() =>
                ConvertComponentTypewriterPresetToInline(hub)));
            section.TrackPropertyValue(
                _typewriterPresetId,
                _ => SyncBuiltInTypewriterPresetSection(preset, description, presets));
        }

        VisualElement BuildCustomTypewriterPresetSection(TextStudioText hub)
        {
            VisualElement section = CreateSection("Preset");
            AddCustomTypewriterPresetControls(section, hub);
            return section;
        }

        void AddCustomTypewriterPresetControls(VisualElement section, TextStudioText hub)
        {
            ObjectField customAssetField;
            VisualElement customAssetRow = BuildAssetReferenceRow(
                _typewriterPreset,
                "Custom Asset",
                typeof(TextStudioTypewriterPreset),
                "ts-typewriter-preset-row",
                "ts-typewriter-preset-create-button",
                "Create a reusable typewriter preset asset from the current inline settings.",
                CreateTypewriterPresetAsset,
                out customAssetField);
            customAssetField.RegisterValueChangedCallback(evt =>
            {
                TextStudioTypewriterPreset asset = evt.newValue as TextStudioTypewriterPreset;
                ApplyCustomTypewriterPreset(asset, hub);
            });
            section.Add(customAssetRow);
            section.Add(CreateAlignedHint(
                "The selected preset is a live reference. Editing the asset updates every Text Studio object using it.",
                "ts-typewriter-preset-description"));
            Button convert = CreateTypewriterPresetConversionButton(() =>
                ConvertComponentTypewriterPresetToInline(hub));
            convert.SetEnabled(_typewriterPreset.objectReferenceValue != null);
            section.Add(convert);
            section.TrackPropertyValue(
                _typewriterPreset,
                _ => convert.SetEnabled(_typewriterPreset.objectReferenceValue != null));
        }

        VisualElement BuildTransitionStyleSection(TextStudioText hub, TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection(TextStudioTypewriterModePresentation.RevealEffectsSectionTitle);
            modeView.SetEffectsSection(section);
            PropertyField revealField = CreateBoundPropertyField(
                _typewriterRevealEffect,
                "Reveal Effect");
            revealField.name = "ts-typewriter-reveal-effect";
            revealField.tooltip =
                "Optional shared text effect applied only while each semantic unit appears. A Composed Effect can contain multiple layers.";
            PropertyField hideField = CreateBoundPropertyField(
                _typewriterHideEffect,
                "Hide Effect");
            PropertyField revealExpressionField = CreateBoundPropertyField(
                _typewriterRevealEffectExpression,
                "Inline Reveal Tags");
            PropertyField hideExpressionField = CreateBoundPropertyField(
                _typewriterHideEffectExpression,
                "Inline Hide Tags");
            hideField.name = "ts-typewriter-hide-effect";
            hideField.tooltip =
                "Optional shared text effect applied only while each semantic unit disappears. A Composed Effect can contain multiple layers.";
            revealExpressionField.name = "ts-typewriter-reveal-expression";
            revealExpressionField.tooltip = "Optional built-in or inline effect tags applied when each semantic unit appears. A custom effect asset replaces these tags.";
            hideExpressionField.name = "ts-typewriter-hide-expression";
            hideExpressionField.tooltip = "Optional built-in or inline effect tags applied when each semantic unit disappears. A custom effect asset replaces these tags.";
            modeView.Add(TextStudioTypewriterModeField.RevealEffect, revealField);
            modeView.Add(TextStudioTypewriterModeField.RevealEffectTags, revealExpressionField);
            modeView.Add(TextStudioTypewriterModeField.HideEffectTags, hideExpressionField);
            Action applyEffects = () =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SetTypewriterTransitionEffects(
                    _typewriterRevealEffect.objectReferenceValue as TextStudioEffectDefinitionAsset,
                    _typewriterHideEffect.objectReferenceValue as TextStudioEffectDefinitionAsset,
                    _typewriterRevealEffectExpression.stringValue,
                    _typewriterHideEffectExpression.stringValue);
                serializedObject.UpdateIfRequiredOrScript();
                modeView.Refresh();
                RestartPreviewAfterTypewriterChange(hub);
            };
            revealField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            hideField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            revealExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            hideExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ => applyEffects());
            section.Add(revealField);
            section.Add(hideField);
            section.Add(revealExpressionField);
            section.Add(hideExpressionField);
            section.Add(CreateAlignedHint(
                "Choose a shared custom effect asset or enter built-in and inline effect tags for each phase. Typewriter timing remains independent of these visual treatments.",
                "ts-typewriter-phase-effects-hint"));
            return section;
        }
        VisualElement BuildTypewriterBehaviorSection(TextStudioText hub, TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection("Behavior");
            PropertyField start = CreateBoundPropertyField(_typewriterStartMode, "Start");
            PropertyField textChange = CreateBoundPropertyField(_textChangeMode, "When Replacing Text");
            textChange.tooltip =
                "Replace immediately, hide the current text before revealing the next value, or overlap outgoing renderer visuals with the incoming reveal.";
            PropertyField textChangeScope = CreateBoundPropertyField(_textChangeScope, "Affected Text");
            textChangeScope.tooltip =
                "Changed Glyphs keeps matching text continuous. Whole Text replays every glyph. Numeric Value matches equal digits by decimal place; set NumericDecimalSeparator in code when formatting with a decimal separator other than a period.";
            PropertyField resetSpeed = CreateBoundPropertyField(_resetSpeedOnNewText, "Reset Speed On New Text");
            PropertyField revealSequence = CreateBoundPropertyField(_revealSequence, "Reveal Sequence");
            modeView.Add(TextStudioTypewriterModeField.RevealSequence, revealSequence);
            PropertyField hideSequence = CreateBoundPropertyField(_hideSequence, "Hide Sequence");
            PropertyField motionPolicy = CreateBoundPropertyField(_motionPolicy, "Motion Policy");
            start.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.TypewriterStartMode = (TextStudioTypewriterStartMode)_typewriterStartMode.enumValueIndex;
            });
            textChange.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.TextChangeMode = (TextStudioTextChangeMode)_textChangeMode.enumValueIndex;
            });
            textChangeScope.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.TextChangeScope = (TextStudioTextChangeScope)_textChangeScope.enumValueIndex;
            });
            resetSpeed.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.ResetSpeedOnNewText = _resetSpeedOnNewText.boolValue;
            });
            revealSequence.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.RevealSequence = ReadSequenceSettings(_revealSequence);
            });
            hideSequence.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.HideSequence = ReadSequenceSettings(_hideSequence);
            });
            motionPolicy.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.MotionPolicy = (TextStudioMotionPolicy)_motionPolicy.enumValueIndex;
            });
            section.Add(start);
            section.Add(textChange);
            section.Add(textChangeScope);
            section.Add(resetSpeed);
            section.Add(revealSequence);
            section.Add(hideSequence);
            section.Add(motionPolicy);
            SerializedProperty layoutDuration = serializedObject.FindProperty("_textChangeLayoutDuration");
            PropertyField layoutTransition = CreateBoundPropertyField(layoutDuration, "Layout Transition");
            layoutTransition.tooltip = "Seconds to move retained glyphs to their new positions after a replacement. Zero disables it. Reduced Motion shortens the movement; No Motion snaps immediately.";
            layoutTransition.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.TextChangeLayoutDuration = layoutDuration.floatValue;
            });
            section.Add(layoutTransition);
            return section;
        }

        static TextStudioSequenceSettings ReadSequenceSettings(SerializedProperty property)
        {
            if (property == null)
                return TextStudioSequenceSettings.Default;
            return new TextStudioSequenceSettings
            {
                Order = (TextStudioSequenceOrder)property.FindPropertyRelative("Order").enumValueIndex,
                Direction = (TextStudioSequenceDirection)property.FindPropertyRelative("Direction").intValue,
                Spread = Mathf.Max(0f, property.FindPropertyRelative("Spread").floatValue),
                Seed = property.FindPropertyRelative("Seed").intValue
            };
        }

        VisualElement BuildTypewriterSkipSection(TextStudioText hub, TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection("Skip");
            PropertyField reveal = CreateBoundPropertyField(_skipRevealMode, "Reveal Effects");
            modeView.Add(TextStudioTypewriterModeField.SkipReveal, reveal);
            PropertyField wait = CreateBoundPropertyField(_skipCurrentWaitOnSkip, "Skip Current Wait");
            PropertyField markers = CreateBoundPropertyField(_triggerRemainingMarkersOnSkip, "Trigger Remaining Markers");
            PropertyField hide = CreateBoundPropertyField(_skipHideMode, "Hide Effect");
            reveal.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SkipRevealMode = (TextStudioTypewriterSkipRevealMode)_skipRevealMode.enumValueIndex;
            });
            wait.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SkipCurrentWaitOnSkip = _skipCurrentWaitOnSkip.boolValue;
            });
            markers.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.TriggerRemainingMarkersOnSkip = _triggerRemainingMarkersOnSkip.boolValue;
            });
            hide.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                hub.SkipHideMode = (TextStudioTypewriterSkipHideMode)_skipHideMode.enumValueIndex;
            });
            section.Add(reveal);
            section.Add(wait);
            section.Add(markers);
            section.Add(hide);
            return section;
        }

        VisualElement BuildAssetReferenceRow(
            SerializedProperty property,
            string label,
            Type objectType,
            string rowName,
            string buttonName,
            string createTooltip,
            Action createAction,
            out ObjectField field,
            bool trackCreateAvailability = true)
        {
            field = new ObjectField(label)
            {
                name = BuildPropertyFieldName(property),
                objectType = objectType,
                allowSceneObjects = false,
                bindingPath = property.propertyPath
            };
            field.AddToClassList("ts-property-field");
            field.AddToClassList(UnityAlignedFieldClass);
            Button create = TextStudioInspectorElements.CreateCompactCreateButton(createAction, buttonName, createTooltip);
            VisualElement row = TextStudioInspectorElements.CreateAssetReferenceRow(field, create, rowName);
            if (!trackCreateAvailability)
                return row;

            row.TrackPropertyValue(
                property,
                _ => TextStudioInspectorElements.SetCompactCreateButtonAvailable(create, property.objectReferenceValue == null));
            TextStudioInspectorElements.SetCompactCreateButtonAvailable(create, property.objectReferenceValue == null);
            return row;
        }

        bool IsUitkTabApplicable(int tab)
        {
            return true;
        }

        TextStudioTypewriterSource GetCurrentTypewriterSource()
        {
            return GetTypewriterSource(_typewriterSource);
        }

        static TextStudioTypewriterSource GetTypewriterSource(SerializedProperty property)
        {
            if (property == null)
                return TextStudioTypewriterSource.None;

            int value = property.enumValueIndex;
            if (value == (int)TextStudioTypewriterSource.None)
                return TextStudioTypewriterSource.None;
            if (value == (int)TextStudioTypewriterSource.Custom)
                return TextStudioTypewriterSource.Custom;
            if (value == (int)TextStudioTypewriterSource.Inline)
                return TextStudioTypewriterSource.Inline;
            return TextStudioTypewriterSource.BuiltIn;
        }

        void HandleComponentTypewriterSourceChanged(
            TextStudioText hub,
            TextStudioTypewriterSource renderedSource,
            TextStudioTypewriterSource nextSource)
        {
            if (nextSource == renderedSource)
                return;

            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            if (hub != null)
            {
                hub.TypewriterSource = nextSource;
                EditorUtility.SetDirty(hub);
                RestartPreviewAfterTypewriterChange(hub);
            }

            RequestUitkActiveTabRebuild(hub);
        }

        static Button CreateTypewriterPresetConversionButton(Action convert)
        {
            Button button = TextStudioInspectorElements.CreateButton(
                "Convert Preset to Inline",
                "Copy the selected preset into editable inline settings and stop following future changes to the preset.",
                convert);
            button.name = "ts-typewriter-convert-inline-button";
            return button;
        }

        void ConvertComponentTypewriterPresetToInline(TextStudioText hub)
        {
            if (!TextStudioTypewriterPresetConversion.ConvertToInline(hub))
                return;

            serializedObject.Update();
            RestartPreviewAfterTypewriterChange(hub);
            RequestUitkActiveTabRebuild(hub);
        }

        void ConvertProfileTypewriterPresetToInline(
            TextStudioText hub,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject)
        {
            if (!TextStudioTypewriterPresetConversion.ConvertToInline(profile))
                return;

            profileObject.Update();
            RestartPreviewAfterTypewriterChange(hub);
            RequestUitkActiveTabRebuild(hub);
        }

        void RestartPreviewAfterTypewriterChange(TextStudioText hub)
        {
            if (hub == null)
                return;

            hub.ForceMeshRefresh(0f);
            if (hub.AuthoringMode != TextStudioAuthoringMode.Preview &&
                !hub.Preview.IsPlaying &&
                !hub.Preview.IsScrubbing &&
                hub.Preview.ShowEndState)
            {
                return;
            }

            hub.Preview.Restart();
            hub.ForceMeshRefresh(0f);
        }

        static int GetTypewriterPresetIndex(IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets, string id)
        {
            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            if (!string.Equals(id, BuiltInTypewriterPresets.Default.Id, StringComparison.OrdinalIgnoreCase))
                return GetTypewriterPresetIndex(presets, BuiltInTypewriterPresets.Default.Id);

            return presets.Count > 0 ? 0 : -1;
        }

        static string GetTypewriterPresetDescription(
            IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets,
            int choiceIndex)
        {
            if (choiceIndex >= 0 && choiceIndex < presets.Count)
                return presets[choiceIndex].Description;

            return BuiltInTypewriterPresets.Default.Description;
        }

        void SyncBuiltInTypewriterPresetSection(
            DropdownField picker,
            Label description,
            IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets)
        {
            if (picker == null)
                return;

            int syncedIndex = GetTypewriterPresetIndex(presets, _typewriterPresetId.stringValue);
            IReadOnlyList<string> choices = picker.choices;
            if (syncedIndex >= 0 && syncedIndex < choices.Count)
                picker.SetValueWithoutNotify(choices[syncedIndex]);
            if (description != null)
                description.text = GetTypewriterPresetDescription(presets, syncedIndex);
        }

        void ApplyBuiltInTypewriterPreset(TextStudioBuiltInTypewriterPreset preset, TextStudioText hub)
        {
            serializedObject.Update();
            _typewriterSource.enumValueIndex = (int)TextStudioTypewriterSource.BuiltIn;
            _typewriterPreset.objectReferenceValue = null;
            _typewriterPresetId.stringValue = preset.Id;
            serializedObject.ApplyModifiedProperties();
            hub.SetBuiltInTypewriterPreset(preset.Id);
            RestartPreviewAfterTypewriterChange(hub);
            EditorUtility.SetDirty(hub);
        }

        void ApplyCustomTypewriterPreset(TextStudioTypewriterPreset preset, TextStudioText hub)
        {
            serializedObject.Update();
            _typewriterSource.enumValueIndex = (int)TextStudioTypewriterSource.Custom;
            _typewriterPreset.objectReferenceValue = preset;
            serializedObject.ApplyModifiedProperties();
            if (preset == null)
            {
                hub.TypewriterSource = TextStudioTypewriterSource.Custom;
                RestartPreviewAfterTypewriterChange(hub);
                EditorUtility.SetDirty(hub);
                return;
            }

            hub.ApplyTypewriterPreset(preset);
            RestartPreviewAfterTypewriterChange(hub);
            serializedObject.UpdateIfRequiredOrScript();
            EditorUtility.SetDirty(hub);
        }

        bool CanRunScheduledInspectorUpdate(VisualElement element, TextStudioText hub = null)
        {
            if (element == null || element.panel == null || this == null || target == null)
                return false;
            if (!ReferenceEquals(hub, null) && hub == null)
                return false;

            return IsSerializedObjectAlive(serializedObject);
        }

        static bool TryGetLiveSerializedObject(
            SerializedProperty property,
            out SerializedObject owner)
        {
            owner = null;
            if (property == null)
                return false;

            try
            {
                owner = property.serializedObject;
                return IsSerializedObjectAlive(owner);
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        static bool IsSerializedObjectAlive(SerializedObject owner)
        {
            if (owner == null)
                return false;

            try
            {
                return owner.targetObject != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        void CreateTimingsAsset()
        {
            CharacterTimings timings = TextStudioInspectorElements.CreateCharacterTimingsAsset();
            if (timings == null)
                return;

            _timings.objectReferenceValue = timings;
            serializedObject.ApplyModifiedProperties();
        }

        void CreateProfileTimingsAsset(
            SerializedProperty customTimings,
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            TextStudioText hub)
        {
            CharacterTimings timings = TextStudioInspectorElements.CreateCharacterTimingsAsset();
            if (timings == null)
                return;

            profileObject.Update();
            customTimings.objectReferenceValue = timings;
            profileObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            RestartPreviewAfterTypewriterChange(hub);
        }

        void CreateTypewriterPresetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Typewriter Preset",
                "Text Studio Typewriter Preset",
                "asset",
                "Choose where to save the reusable typewriter preset.");
            if (string.IsNullOrEmpty(path))
                return;

            SaveTypewriterPresetAsset(path);
        }

        internal void SaveTypewriterPresetAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || serializedObject.isEditingMultipleObjects)
                return;

            serializedObject.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save Inline Typewriter as Custom Asset");
            TextStudioTypewriterPreset preset = ScriptableObject.CreateInstance<TextStudioTypewriterPreset>();
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = (RevealMode)_revealMode.enumValueIndex;
            settings.Timings = _timings.objectReferenceValue as TypingTimingsBase;
            settings.BuiltInTimingsPreset = (RevealTimingsPreset)_builtInTimingsPreset.enumValueIndex;
            settings.WaitAfterFinalReveal = _waitAfterFinalReveal.boolValue;
            settings.RevealSpeedMultiplier = Mathf.Max(0.01f, _revealSpeedMultiplier.floatValue);
            settings.TimingMode = (TextStudioTypewriterTimingMode)serializedObject.FindProperty("_typewriterTimingMode").intValue;
            settings.WritingEnabled = serializedObject.FindProperty("_writingEnabled").boolValue;
            settings.WritingStyle = _writingStyle.objectReferenceValue as TextStudioWritingStyleAsset;
            settings.EraseOnHide = serializedObject.FindProperty("_eraseOnHide").boolValue;
            settings.RevealAppearanceDuration = Mathf.Max(0.01f, _revealAppearanceDuration.floatValue);
            settings.RevealDisappearanceDuration = Mathf.Max(0.01f, _revealDisappearanceDuration.floatValue);
            settings.RevealSequence = ReadSequenceSettings(_revealSequence);
            settings.HideSequence = ReadSequenceSettings(_hideSequence);
            settings.StartMode = (TextStudioTypewriterStartMode)_typewriterStartMode.enumValueIndex;
            settings.TextChangeMode = (TextStudioTextChangeMode)_textChangeMode.enumValueIndex;
            settings.TextChangeScope = (TextStudioTextChangeScope)_textChangeScope.enumValueIndex;
            settings.ResetSpeedOnNewText = _resetSpeedOnNewText.boolValue;
            settings.SkipRevealMode = (TextStudioTypewriterSkipRevealMode)_skipRevealMode.enumValueIndex;
            settings.TriggerRemainingMarkersOnSkip = _triggerRemainingMarkersOnSkip.boolValue;
            settings.SkipCurrentWaitOnSkip = _skipCurrentWaitOnSkip.boolValue;
            settings.SkipHideMode = (TextStudioTypewriterSkipHideMode)_skipHideMode.enumValueIndex;
            settings.LoopReveal = _loopReveal.boolValue;
            settings.LoopRevealDelay = Mathf.Max(0f, _loopRevealDelay.floatValue);
            settings.RevealEffect = _typewriterRevealEffect.objectReferenceValue as TextStudioEffectDefinitionAsset;
            settings.HideEffect = _typewriterHideEffect.objectReferenceValue as TextStudioEffectDefinitionAsset;
            settings.RevealEffectExpression = _typewriterRevealEffectExpression.stringValue ?? string.Empty;
            settings.HideEffectExpression = _typewriterHideEffectExpression.stringValue ?? string.Empty;
            preset.Settings = TextStudioTypewriterSettings.NormalizeAuthoringSettings(settings);

            AssetDatabase.CreateAsset(preset, path);
            Undo.RegisterCreatedObjectUndo(preset, "Create Typewriter Preset");
            AssetDatabase.SaveAssetIfDirty(preset);
            _typewriterSource.enumValueIndex = (int)TextStudioTypewriterSource.Custom;
            _typewriterPreset.objectReferenceValue = preset;
            serializedObject.ApplyModifiedProperties();
            TextStudioText hub = target as TextStudioText;
            if (hub != null)
            {
                hub.ApplyTypewriterPreset(preset);
                RestartPreviewAfterTypewriterChange(hub);
                RequestUitkActiveTabRebuild(hub);
            }
            Undo.CollapseUndoOperations(undoGroup);
            EditorGUIUtility.PingObject(preset);
        }

        void CreateProfileTypewriterPresetAsset(
            TextStudioBehaviorProfile profile,
            SerializedObject profileObject,
            TextStudioText hub)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Typewriter Preset",
                "Text Studio Typewriter Preset",
                "asset",
                "Choose where to save the reusable typewriter preset.");
            if (string.IsNullOrEmpty(path))
                return;

            profileObject.ApplyModifiedProperties();
            TextStudioTypewriterPresetConversion.SaveProfilePreset(profile, path);
            profileObject.Update();
            RestartPreviewAfterTypewriterChange(hub);
            RequestUitkActiveTabRebuild(hub);
        }
    }
}
