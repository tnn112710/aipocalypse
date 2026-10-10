using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioBehaviorProfile))]
    internal sealed class TextStudioBehaviorProfileEditor : UnityEditor.Editor
    {
        SerializedProperty _domains;
        SerializedProperty _defaultEffectMode;
        SerializedProperty _defaultEffects;
        SerializedProperty _defaultRevealEffects;
        SerializedProperty _defaultActiveEffects;
        SerializedProperty _defaultHideEffects;
        SerializedProperty _typewriter;
        VisualElement _root;
        bool _rootRebuildPending;

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();
            _root = TextStudioInspectorElements.CreateRoot("ts-profile-editor-root");
            _root.AddToClassList("ts-profile-editor-root");
            RebuildRoot();
            return _root;
        }

        void OnDisable()
        {
            _root?.Unbind();
            _root = null;
            _rootRebuildPending = false;
        }

        void RebuildRoot()
        {
            if (_root == null)
                return;

            _rootRebuildPending = false;
            _root.Unbind();
            _root.Clear();
            CacheProperties();
            serializedObject.Update();

            _root.Add(CreateDomainsSection());
            _root.Add(CreateDefaultsSection());
            _root.Add(CreateTypewriterSection());
            VisualElement reactions = CreateSection("Reactions");
            reactions.name = "ts-profile-reactions";
            reactions.Add(TextStudioReactionRecipeInspector.Create(
                serializedObject, "_reactions", CreateReactionAsset, "ts-profile-reactions-source"));
            reactions.Add(TextStudioInspectorElements.CreateMutedText(
                "This recipe replaces local Reactions only when the Reactions domain is enabled."));
            _root.Add(reactions);
            _root.Bind(serializedObject);
        }

        void CacheProperties()
        {
            _domains = serializedObject.FindProperty("_domains");
            _defaultEffectMode = serializedObject.FindProperty("_defaultEffectMode");
            _defaultEffects = serializedObject.FindProperty("_defaultEffects");
            _defaultRevealEffects = serializedObject.FindProperty("_defaultRevealEffects");
            _defaultActiveEffects = serializedObject.FindProperty("_defaultActiveEffects");
            _defaultHideEffects = serializedObject.FindProperty("_defaultHideEffects");
            _typewriter = serializedObject.FindProperty("_typewriter");
        }

        void CreateReactionAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Reactions", "Text Studio Reactions", "asset",
                "Choose where to save the reusable reaction recipe.");
            if (string.IsNullOrEmpty(path))
                return;
            TextStudioReactionAsset asset = ScriptableObject.CreateInstance<TextStudioReactionAsset>();
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Text Studio Reactions");
            serializedObject.Update();
            serializedObject.FindProperty("_reactions.CustomAsset").objectReferenceValue = asset;
            serializedObject.FindProperty("_reactions.Source").enumValueIndex = (int)TextStudioReactionSource.Custom;
            serializedObject.ApplyModifiedProperties();
            EditorGUIUtility.PingObject(asset);
        }

        VisualElement CreateDomainsSection()
        {
            VisualElement section = CreateSection("Behavior Domains");
            section.name = "ts-behavior-profile-domains";
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Enable only the reusable behavior this asset should provide. Components retain their own values for disabled domains."));
            section.Add(CreateBehaviorDomainToggle(
                TextStudioBehaviorDomain.DefaultEffects,
                "Default Effects",
                "Provide fallback or constant effect expressions to assigned hosts."));
            section.Add(CreateBehaviorDomainToggle(
                TextStudioBehaviorDomain.Typewriter,
                "Typewriter",
                "Provide the reveal, replacement, phase-effect, and skip recipe to assigned hosts."));
            section.Add(CreateBehaviorDomainToggle(TextStudioBehaviorDomain.Reactions, "Reactions",
                "Provide finite event responses independently of Typewriter and default effects."));
            HelpBox warning = CreateHelpBox(
                "This Behavior Profile currently provides no behavior. Enable a behavior domain.",
                HelpBoxMessageType.Warning);
            warning.name = "ts-behavior-profile-empty-warning";
            section.Add(warning);
            Action refresh = () =>
            {
                warning.style.display = _domains.intValue == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            };
            section.TrackPropertyValue(_domains, _ => refresh());
            refresh();
            return section;
        }

        Toggle CreateBehaviorDomainToggle(
            TextStudioBehaviorDomain domain,
            string label,
            string tooltip)
        {
            Toggle toggle = new Toggle(label)
            {
                tooltip = tooltip
            };
            toggle.AddToClassList("ts-behavior-profile-domain-toggle");
            Action refresh = () =>
            {
                toggle.SetValueWithoutNotify(
                    (_domains.intValue & (int)domain) != 0);
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                int domains = _domains.intValue;
                domains = evt.newValue
                    ? domains | (int)domain
                    : domains & ~(int)domain;
                _domains.intValue = domains;
                serializedObject.ApplyModifiedProperties();
            });
            toggle.TrackPropertyValue(_domains, _ => refresh());
            refresh();
            return toggle;
        }

        VisualElement CreateDefaultsSection()
        {
            VisualElement section = CreateSection("Defaults");
            section.name = "ts-profile-defaults";
            section.Add(CreateBoundPropertyField(_defaultEffectMode, "Default Effects Mode"));

            VisualElement expressions = new VisualElement
            {
                name = "ts-profile-default-effects-expressions"
            };
            expressions.Add(CreateBoundPropertyField(_defaultEffects, "All Phase Effects"));
            expressions.Add(CreateBoundPropertyField(_defaultRevealEffects, "Reveal Effects"));
            expressions.Add(CreateBoundPropertyField(_defaultActiveEffects, "Active Effects"));
            expressions.Add(CreateBoundPropertyField(_defaultHideEffects, "Hide Effects"));
            section.Add(expressions);
            RefreshDefaultEffectsExpressionVisibility(expressions, _defaultEffectMode);
            section.TrackPropertyValue(
                _defaultEffectMode,
                mode => RefreshDefaultEffectsExpressionVisibility(expressions, mode));

            return section;
        }

        static void RefreshDefaultEffectsExpressionVisibility(
            VisualElement expressions,
            SerializedProperty mode)
        {
            expressions.style.display =
                (TextStudioDefaultEffectMode)mode.enumValueIndex == TextStudioDefaultEffectMode.None
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
        }

        VisualElement CreateTypewriterSection()
        {
            VisualElement root = CreateSection("Typewriter");
            root.name = "ts-profile-typewriter";
            if (_typewriter == null)
            {
                root.Add(CreateHelpBox("Missing profile typewriter recipe.", HelpBoxMessageType.Error));
                return root;
            }

            SerializedProperty source = _typewriter.FindPropertyRelative("Source");
            TextStudioTypewriterSource renderedSource = GetTypewriterSource(source);
            PropertyField sourceField = CreateBoundPropertyField(source, "Source");
            sourceField.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                HandleTypewriterSourceChanged(renderedSource, GetTypewriterSource(evt.changedProperty)));
            root.Add(sourceField);
            root.Add(TextStudioInspectorElements.CreateMutedText(
                "None shows text immediately. Built-In and Custom use live preset references. Inline stores this profile's own typewriter settings."));

            switch (renderedSource)
            {
                case TextStudioTypewriterSource.BuiltIn:
                    root.Add(CreateBuiltInTypewriterPresetSection());
                    break;
                case TextStudioTypewriterSource.Custom:
                    root.Add(CreateCustomTypewriterPresetSection());
                    break;
                case TextStudioTypewriterSource.Inline:
                    Button save = TextStudioTypewriterPresetConversion.CreateSaveButton(() =>
                    {
                        string path = EditorUtility.SaveFilePanelInProject(
                            "Create Typewriter Preset", "Text Studio Typewriter Preset", "asset",
                            "Choose where to save the reusable typewriter preset.");
                        if (string.IsNullOrEmpty(path)) return;
                        serializedObject.ApplyModifiedProperties();
                        TextStudioTypewriterPresetConversion.SaveProfilePreset((TextStudioBehaviorProfile)target, path);
                        serializedObject.Update();
                        RequestDeferredRootRebuild();
                    });
                    save.SetEnabled(!serializedObject.isEditingMultipleObjects);
                    root.Add(save);
                    root.Add(CreateInlineTypewriterSettingsSection());
                    break;
            }

            return root;
        }

        internal void HandleTypewriterSourceChanged(
            TextStudioTypewriterSource renderedSource,
            TextStudioTypewriterSource nextSource)
        {
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            if (nextSource == renderedSource)
                return;

            RequestDeferredRootRebuild();
        }

        void RequestDeferredRootRebuild()
        {
            if (_root == null || _rootRebuildPending)
                return;

            _rootRebuildPending = true;
            VisualElement scheduledRoot = _root;
            scheduledRoot.schedule.Execute(() =>
            {
                _rootRebuildPending = false;
                if (!ReferenceEquals(_root, scheduledRoot) || scheduledRoot.panel == null)
                    return;

                RebuildRoot();
            });
        }

        static TextStudioTypewriterSource GetTypewriterSource(SerializedProperty source)
        {
            return source != null
                ? (TextStudioTypewriterSource)source.enumValueIndex
                : TextStudioTypewriterSource.None;
        }

        VisualElement CreateBuiltInTypewriterPresetSection()
        {
            VisualElement section = CreateSection("Preset");
            section.name = "ts-profile-typewriter-built-in";
            IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets = BuiltInTypewriterPresets.All();
            List<string> choices = new List<string>(presets.Count);
            for (int i = 0; i < presets.Count; i++)
                choices.Add(presets[i].DisplayName);

            SerializedProperty presetId = _typewriter.FindPropertyRelative("BuiltInPresetId");
            int currentIndex = GetTypewriterPresetIndex(presets, presetId.stringValue);
            DropdownField picker = new DropdownField(
                "Built-In Preset",
                choices,
                currentIndex >= 0 ? currentIndex : 0)
            {
                name = "ts-typewriter-preset-picker"
            };
            picker.AddToClassList("unity-base-field__aligned");
            Label description = new Label(GetTypewriterPresetDescription(presets, currentIndex))
            {
                name = "ts-typewriter-preset-description"
            };
            description.AddToClassList("ts-muted-text");
            picker.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || index >= presets.Count)
                    return;

                serializedObject.Update();
                presetId.stringValue = presets[index].Id;
                serializedObject.ApplyModifiedProperties();
                description.text = presets[index].Description;
            });
            section.TrackPropertyValue(presetId, trackedPresetId =>
            {
                int syncedIndex = GetTypewriterPresetIndex(presets, trackedPresetId.stringValue);
                if (syncedIndex >= 0 && syncedIndex < choices.Count)
                    picker.SetValueWithoutNotify(choices[syncedIndex]);
                description.text = GetTypewriterPresetDescription(presets, syncedIndex);
            });

            section.Add(picker);
            section.Add(description);
            section.Add(CreateTypewriterPresetConversionButton());
            return section;
        }

        VisualElement CreateCustomTypewriterPresetSection()
        {
            VisualElement section = CreateSection("Preset");
            section.name = "ts-profile-typewriter-custom";
            SerializedProperty customPreset = _typewriter.FindPropertyRelative("CustomPreset");
            section.Add(CreateBoundPropertyField(customPreset, "Custom Asset"));
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "The selected preset is a live reference. Editing it updates every profile and component using that preset."));
            Button convert = CreateTypewriterPresetConversionButton();
            convert.SetEnabled(customPreset.objectReferenceValue != null);
            section.Add(convert);
            section.TrackPropertyValue(
                customPreset,
                _ => convert.SetEnabled(customPreset.objectReferenceValue != null));
            return section;
        }

        Button CreateTypewriterPresetConversionButton()
        {
            Button button = TextStudioInspectorElements.CreateButton(
                "Convert Preset to Inline",
                "Copy the selected preset into editable inline settings and stop following future changes to the preset.",
                ConvertTypewriterPresetToInline);
            button.name = "ts-typewriter-convert-inline-button";
            return button;
        }

        void ConvertTypewriterPresetToInline()
        {
            TextStudioBehaviorProfile profile =
                target as TextStudioBehaviorProfile;
            if (!TextStudioTypewriterPresetConversion.ConvertToInline(profile))
                return;

            serializedObject.Update();
            RequestDeferredRootRebuild();
        }

        VisualElement CreateInlineTypewriterSettingsSection()
        {
            VisualElement root = new VisualElement
            {
                name = "ts-inline-typewriter-settings"
            };
            root.AddToClassList("ts-inline-typewriter-settings");
            SerializedProperty inline = _typewriter.FindPropertyRelative("InlineSettings");
            SerializedProperty revealMode = inline.FindPropertyRelative("RevealMode");
            SerializedProperty builtInTimings = inline.FindPropertyRelative("BuiltInTimingsPreset");
            SerializedProperty customTimings = inline.FindPropertyRelative("Timings");
            SerializedProperty loopReveal = inline.FindPropertyRelative("LoopReveal");
            SerializedProperty writingEnabled = inline.FindPropertyRelative("WritingEnabled");
            SerializedProperty revealEffect = inline.FindPropertyRelative("RevealEffect");
            SerializedProperty hideEffect = inline.FindPropertyRelative("HideEffect");
            RevealTimingsPreset[] timingPresets;
            List<string> timingChoices = TextStudioInspectorElements.CreateTimingPresetChoices(out timingPresets);
            int timingChoiceIndex = TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                timingPresets,
                (RevealTimingsPreset)builtInTimings.enumValueIndex,
                customTimings.objectReferenceValue != null);
            bool customTimingSelected = timingChoiceIndex == timingPresets.Length;
            TextStudioTypewriterModeView modeView = new TextStudioTypewriterModeView(() =>
                TextStudioTypewriterModeState.Read(
                    revealMode,
                    customTimingSelected,
                    customTimings,
                    loopReveal,
                    writingEnabled,
                    revealEffect,
                    hideEffect));

            VisualElement timing = CreateSection("Reveal Timing");
            PropertyField modeField = CreateBoundPropertyField(revealMode, TextStudioTypewriterModePresentation.ModeLabel);
            modeField.tooltip = TextStudioTypewriterModePresentation.ModeTooltip;
            timing.Add(modeField);
            Label modeGuidance = TextStudioInspectorElements.CreateMutedText(string.Empty, "ts-typewriter-mode-guidance");
            timing.Add(modeGuidance);
            modeView.SetGuidance(modeGuidance);
            PropertyField timingMode = CreateBoundPropertyField(inline.FindPropertyRelative("TimingMode"), "Timing");
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
            Label timingDescription = new Label
            {
                name = "ts-timings-preset-description"
            };
            timingDescription.AddToClassList("ts-muted-text");
            modeView.Add(TextStudioTypewriterModeField.CadencePreset, timingDescription);
            PropertyField customTimingField = CreateBoundPropertyField(customTimings, "Custom Asset");
            customTimingField.tooltip = "Reusable timing asset used instead of the selected built-in cadence. In Custom mode it also defines reveal groups.";
            Button createTimings = TextStudioInspectorElements.CreateCompactCreateButton(
                () => CreateInlineTimingsAsset(customTimings),
                "ts-timings-create-button",
                "Create and assign a custom timing asset.");
            VisualElement customTimingRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                customTimingField,
                createTimings,
                "ts-timings-row");
            modeView.Add(TextStudioTypewriterModeField.TimingAsset, customTimingRow);
            modeView.SetCreateTimingAsset(createTimings);
            HelpBox missingCustomTimings = CreateHelpBox(
                TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(RevealMode.ByGlyph),
                HelpBoxMessageType.Warning);
            missingCustomTimings.name = "ts-timings-missing-help";
            modeView.SetMissingTimingAsset(missingCustomTimings);
            timing.Add(timingDescription);
            timing.Add(customTimingRow);
            timing.Add(missingCustomTimings);
            PropertyField waitAfterFinalReveal = CreateBoundPropertyField(
                inline.FindPropertyRelative("WaitAfterFinalReveal"),
                "Wait After Final Reveal");
            waitAfterFinalReveal.name = "ts-typewriter-wait-after-final-reveal";
            waitAfterFinalReveal.tooltip =
                "With a built-in cadence, include the final unit's trailing delay before completion. Custom timing assets own this choice.";
            timing.Add(waitAfterFinalReveal);
            modeView.Add(TextStudioTypewriterModeField.FinalRevealWait, waitAfterFinalReveal);
            PropertyField playbackSpeed = CreateBoundPropertyField(
                inline.FindPropertyRelative("RevealSpeedMultiplier"),
                TextStudioTypewriterModePresentation.PlaybackSpeedLabel);
            playbackSpeed.tooltip = TextStudioTypewriterModePresentation.PlaybackSpeedTooltip;
            timing.Add(playbackSpeed);

            Action refreshTimingState = () =>
            {
                if (customTimings.objectReferenceValue != null)
                    customTimingSelected = true;
                int selectedIndex = customTimingSelected
                    ? timingPresets.Length
                    : TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                        timingPresets,
                        (RevealTimingsPreset)builtInTimings.enumValueIndex,
                        false);
                timingPreset.SetValueWithoutNotify(timingChoices[selectedIndex]);
                timingDescription.text = TextStudioInspectorElements.GetTimingPresetDescription(
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
                    builtInTimings.enumValueIndex = (int)timingPresets[selectedIndex];
                    customTimings.objectReferenceValue = null;
                    serializedObject.ApplyModifiedProperties();
                }

                refreshTimingState();
            });
            timing.TrackPropertyValue(builtInTimings, _ => refreshTimingState());
            timing.TrackPropertyValue(customTimings, _ => refreshTimingState());
            root.Add(timing);

            VisualElement duration = CreateSection(TextStudioTypewriterModePresentation.RevealDurationSectionTitle);
            modeView.SetDurationSection(duration);
            PropertyField revealDuration = CreateBoundPropertyField(inline.FindPropertyRelative("RevealAppearanceDuration"), "Reveal Duration");
            duration.Add(revealDuration);
            modeView.Add(TextStudioTypewriterModeField.RevealDuration, revealDuration);
            duration.Add(CreateBoundPropertyField(inline.FindPropertyRelative("RevealDisappearanceDuration"), "Hide Duration"));
            PropertyField repeat = CreateBoundPropertyField(loopReveal, "Repeat Reveal");
            repeat.tooltip = "Repeat the authored reveal lifecycle at runtime. The Live Preview Repeat button is independent.";
            duration.Add(repeat);
            PropertyField repeatDelay = CreateBoundPropertyField(inline.FindPropertyRelative("LoopRevealDelay"), "Repeat Delay");
            repeatDelay.tooltip = "Seconds to wait before an authored runtime reveal repeats.";
            duration.Add(repeatDelay);
            modeView.Add(TextStudioTypewriterModeField.RepeatDelay, repeatDelay);
            root.Add(duration);

            VisualElement behavior = CreateSection("Behavior");
            behavior.Add(CreateBoundPropertyField(inline.FindPropertyRelative("StartMode"), "Start"));
            behavior.Add(CreateBoundPropertyField(inline.FindPropertyRelative("TextChangeMode"), "When Replacing Text"));
            behavior.Add(CreateBoundPropertyField(inline.FindPropertyRelative("TextChangeScope"), "Affected Text"));
            behavior.Add(CreateBoundPropertyField(inline.FindPropertyRelative("ResetSpeedOnNewText"), "Reset Speed On New Text"));
            PropertyField revealSequence = CreateBoundPropertyField(inline.FindPropertyRelative("RevealSequence"), "Reveal Sequence");
            behavior.Add(revealSequence);
            modeView.Add(TextStudioTypewriterModeField.RevealSequence, revealSequence);
            behavior.Add(CreateBoundPropertyField(inline.FindPropertyRelative("HideSequence"), "Hide Sequence"));
            root.Add(behavior);

            VisualElement skip = CreateSection("Skip Policy");
            PropertyField skipReveal = CreateBoundPropertyField(inline.FindPropertyRelative("SkipRevealMode"), "Reveal Effects");
            skip.Add(skipReveal);
            modeView.Add(TextStudioTypewriterModeField.SkipReveal, skipReveal);
            skip.Add(CreateBoundPropertyField(inline.FindPropertyRelative("SkipCurrentWaitOnSkip"), "Skip Current Wait"));
            skip.Add(CreateBoundPropertyField(inline.FindPropertyRelative("TriggerRemainingMarkersOnSkip"), "Trigger Remaining Markers"));
            skip.Add(CreateBoundPropertyField(inline.FindPropertyRelative("SkipHideMode"), "Hide Effects on Skip"));
            root.Add(skip);

            root.Add(CreateInlineTransitionStyleSection(inline, modeView));

            VisualElement writing = CreateSection("Writing");
            writing.Add(CreateBoundPropertyField(writingEnabled, "Enable Writing"));
            Label writingGuidance = TextStudioInspectorElements.CreateMutedText(string.Empty, "ts-writing-instant-guidance");
            writing.Add(writingGuidance);
            modeView.SetWritingGuidance(writingGuidance);
            VisualElement writingOptions = new VisualElement();
            writingOptions.Add(CreateBoundPropertyField(inline.FindPropertyRelative("WritingStyle"), "Writing Style"));
            writingOptions.Add(CreateBoundPropertyField(inline.FindPropertyRelative("EraseOnHide"), "Erase on Hide"));
            writing.Add(writingOptions);
            modeView.Add(TextStudioTypewriterModeField.WritingOptions, writingOptions);
            root.Add(writing);

            modeView.Track(root, revealMode, loopReveal, writingEnabled, revealEffect, hideEffect);
            refreshTimingState();
            return root;
        }

        VisualElement CreateInlineTransitionStyleSection(SerializedProperty inline, TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection(TextStudioTypewriterModePresentation.RevealEffectsSectionTitle);
            modeView.SetEffectsSection(section);
            SerializedProperty revealEffect = inline.FindPropertyRelative("RevealEffect");
            SerializedProperty hideEffect = inline.FindPropertyRelative("HideEffect");
            SerializedProperty revealExpression = inline.FindPropertyRelative("RevealEffectExpression");
            SerializedProperty hideExpression = inline.FindPropertyRelative("HideEffectExpression");
            PropertyField revealField = CreateBoundPropertyField(revealEffect, "Reveal Effect");
            revealField.name = "ts-typewriter-reveal-effect";
            revealField.tooltip =
                "Optional shared text effect applied only while each semantic unit appears. A Composed Effect can contain multiple layers.";
            PropertyField hideField = CreateBoundPropertyField(hideEffect, "Hide Effect");
            hideField.name = "ts-typewriter-hide-effect";
            hideField.tooltip =
                "Optional shared text effect applied only while each semantic unit disappears. A Composed Effect can contain multiple layers.";
            PropertyField revealExpressionField = CreateBoundPropertyField(revealExpression, "Inline Reveal Tags");
            revealExpressionField.name = "ts-typewriter-reveal-expression";
            revealExpressionField.tooltip =
                "Optional built-in or inline effect tags applied when each semantic unit appears. A custom effect asset replaces these tags.";
            PropertyField hideExpressionField = CreateBoundPropertyField(hideExpression, "Inline Hide Tags");
            hideExpressionField.name = "ts-typewriter-hide-expression";
            hideExpressionField.tooltip =
                "Optional built-in or inline effect tags applied when each semantic unit disappears. A custom effect asset replaces these tags.";
            modeView.Add(TextStudioTypewriterModeField.RevealEffect, revealField);
            modeView.Add(TextStudioTypewriterModeField.RevealEffectTags, revealExpressionField);
            modeView.Add(TextStudioTypewriterModeField.HideEffectTags, hideExpressionField);
            revealField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (revealEffect.objectReferenceValue != null)
                    revealExpression.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            hideField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (hideEffect.objectReferenceValue != null)
                    hideExpression.stringValue = string.Empty;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            revealExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(revealExpression.stringValue))
                    revealEffect.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            hideExpressionField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(hideExpression.stringValue))
                    hideEffect.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                modeView.Refresh();
            });
            section.Add(revealField);
            section.Add(hideField);
            section.Add(revealExpressionField);
            section.Add(hideExpressionField);
            section.Add(TextStudioInspectorElements.CreateMutedText(
                "Choose a shared effect asset or inline tags independently for Reveal and Hide. Both use the same effect library as normal text effects; Typewriter timing stays separate."));
            return section;
        }
        static VisualElement CreateSection(string title)
        {
            return TextStudioInspectorElements.CreateSection(title, null);
        }

        static PropertyField CreateBoundPropertyField(SerializedProperty property, string label)
        {
            if (property == null)
                return null;

            PropertyField field = CommonInspectorElements.CreatePropertyField(
                property.serializedObject,
                property.propertyPath,
                label,
                "ts-field-" + property.propertyPath.Replace(".", "-"));
            field.AddToClassList("ts-property-field");
            return field;
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

        static string GetTypewriterPresetDescription(IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets, int choiceIndex)
        {
            if (choiceIndex >= 0 && choiceIndex < presets.Count)
                return presets[choiceIndex].Description;

            return BuiltInTypewriterPresets.Default.Description;
        }

        void CreateInlineTimingsAsset(SerializedProperty customTimings)
        {
            CharacterTimings timings = TextStudioInspectorElements.CreateCharacterTimingsAsset();
            if (timings == null)
                return;

            serializedObject.Update();
            customTimings.objectReferenceValue = timings;
            serializedObject.ApplyModifiedProperties();
        }

        static HelpBox CreateHelpBox(string message, HelpBoxMessageType type)
        {
            HelpBox box = CommonInspectorElements.CreateHelpBox(message, type);
            box.AddToClassList("ts-help-box");
            return box;
        }
    }
}
