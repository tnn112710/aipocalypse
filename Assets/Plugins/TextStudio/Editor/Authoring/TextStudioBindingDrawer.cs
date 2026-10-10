using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomPropertyDrawer(typeof(TextStudioBinding.UxmlSerializedData))]
    internal sealed partial class TextStudioBindingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new BindingDrawerController(property).Build();
        }

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        sealed partial class BindingDrawerController : ITextStudioAuthoringSurfaceProvider
        {
            const string UnityAlignedFieldClass = "unity-base-field__aligned";

#if UNITY_6000_7_OR_NEWER
            // Tab descriptors are immutable inspector metadata. s_SelectedTab remains auto-reset session state.
            [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
            static readonly GUIContent[] s_Tabs =
            {
                new GUIContent("Content", "Show source and content settings."),
                new GUIContent("Typewriter", "Show reveal and playback settings."),
                new GUIContent("Placement", "Show glyph placement settings."),
                new GUIContent("Events", "Show the UI Toolkit runtime event surface."),
                new GUIContent("Debug", "Show diagnostics and advanced binding settings.")
            };
#if UNITY_6000_7_OR_NEWER
            [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
            static readonly CommonUITK.SegmentedControlClasses s_PreviewModeClasses =
                new CommonUITK.SegmentedControlClasses
                {
                    RootClass = "ts-preview-mode-segmented",
                    ButtonClass = "ts-preview-mode-segment-button",
                    ActiveButtonClass = "ts-preview-mode-segment-active",
                    FirstButtonClass = "ts-preview-mode-segment-first",
                    LastButtonClass = "ts-preview-mode-segment-last"
                };
            static int s_SelectedTab;

            readonly SerializedObject _serializedObject;
            readonly string _bindingPropertyPath;
            readonly IUIToolkitUxmlAuthoringStore _ownerStore;
            readonly TextStudioAuthoringSession _session;

            VisualElement _root;
            VisualElement _sourceBody;
            VisualElement _diagnostics;
            VisualElement _previewControlsHost;
            VisualElement _previewLiveBody;
            VisualElement _previewModeControl;
            Toggle _previewModeState;
            Label _previewTargetStatus;
            TextField _sourceField;
            Button _elementPickerButton;
            TextStudioInlineTagHelpElement _inlineHelp;
            Label _tagSummary;
            TextStudioBinding _previewBinding;
            TextStudioAuthoringPreviewControls _previewControls;
            IVisualElementScheduledItem _previewUpdateLoop;
            double _lastPreviewUpdateTime;
            bool _previewModeInitialized;
            bool _customTimingSelected;
            TextStudioAuthoringSurface _surface;

            public BindingDrawerController(SerializedProperty property)
            {
                _serializedObject = property?.serializedObject;
                _bindingPropertyPath = property?.propertyPath ?? string.Empty;
                SerializedProperty current = BindingProperty;
                _ownerStore = new TextStudioUIToolkitUxmlAuthoringStore(current);
                _session = new TextStudioAuthoringSession(
                    _ownerStore.IsAvailable ? _ownerStore.Text : string.Empty);
                _session.SourceChanged += OnSessionSourceChanged;
            }

            SerializedProperty BindingProperty =>
                _serializedObject != null && !string.IsNullOrEmpty(_bindingPropertyPath)
                    ? _serializedObject.FindProperty(_bindingPropertyPath)
                    : null;

            TextStudioUxmlBindingAuthoringStorage Storage =>
                new TextStudioUxmlBindingAuthoringStorage(BindingProperty, _ownerStore);

            public VisualElement Build()
            {
                _surface = new TextStudioAuthoringSurface(this);
                return _surface.Build();
            }

            void ConfigureRoot(VisualElement root)
            {
                _root = root;
                _root.AddToClassList("ts-uitk-binding-drawer");
                _root.viewDataKey = "text-studio-binding-" + _bindingPropertyPath;
                StyleSheet bindingStyle = CommonUITK.LoadStyleSheetFromAnchor(
                    "TextStudioBindingDrawer",
                    "/Editor/Authoring/TextStudioBindingDrawer.cs",
                    "/Editor/Authoring/TextStudioBindingDrawer.uss");
                if (bindingStyle != null)
                    _root.styleSheets.Add(bindingStyle);
            }

            VisualElement BuildSourceSection()
            {
                VisualElement section = CreateSection("Source", "ts-uitk-binding-source");
                SerializedProperty modeProperty =
                    Storage.FindProperty(TextStudioAuthoringPropertyId.SourceMode);
                int selected = modeProperty != null
                    ? Mathf.Clamp(modeProperty.enumValueIndex, 0, 2)
                    : 0;
                List<string> options = new List<string>
                {
                    "Label Text",
                    "Element",
                    "Data"
                };
                DropdownField selector = new DropdownField("Text Source", options, selected)
                {
                    tooltip = "Label Text is the normal direct authoring flow. Element and Data are optional external source modes."
                };
                selector.name = "ts-uitk-binding-source-mode";
                selector.AddToClassList(UnityAlignedFieldClass);
                selector.RegisterValueChangedCallback(evt =>
                    SetSourceMode(Mathf.Max(0, options.IndexOf(evt.newValue))));
                section.Add(selector);

                _sourceBody = new VisualElement();
                _sourceBody.AddToClassList("ts-uitk-binding-source-body");
                section.Add(_sourceBody);
                RebuildSourceBody();

                _diagnostics = new VisualElement();
                _diagnostics.AddToClassList("ts-source-diagnostics");
                section.Add(_diagnostics);
                RefreshDiagnostics();
                return section;
            }

            void RebuildSourceBody()
            {
                _inlineHelp?.DetachSourceField();
                _inlineHelp = null;
                _sourceField = null;
                _elementPickerButton = null;
                _tagSummary = null;
                _sourceBody?.Clear();
                if (_sourceBody == null)
                    return;

                switch (GetSourceMode())
                {
                    case TextStudioBindingSourceMode.Element:
                        BuildElementSource(_sourceBody);
                        break;
                    case TextStudioBindingSourceMode.Data:
                        BuildDataSource(_sourceBody);
                        break;
                    case TextStudioBindingSourceMode.LabelText:
                    default:
                        BuildLabelTextSource(_sourceBody);
                        break;
                }
            }

            void BuildLabelTextSource(VisualElement container)
            {
                Label linked = new Label(
                    "This is the Label's normal Text property. Edit Text Studio markup here or in the main UI Builder Inspector.");
                linked.AddToClassList("ts-inline-info-text");
                container.Add(linked);

                if (!_ownerStore.IsAvailable)
                {
                    container.Add(TextStudioInspectorElements.CreateMutedText(_ownerStore.Guidance));
                    return;
                }

                _session.SetSource(_ownerStore.Text, _ownerStore.Text.Length, _ownerStore.Text.Length);
                _sourceField = new TextField
                {
                    multiline = true,
                    value = _ownerStore.Text
                };
                _sourceField.AddToClassList("ts-source-field");
                _sourceField.AddToClassList("ts-source-field-wrap");
                _sourceField.selectAllOnFocus = false;
                _sourceField.selectAllOnMouseUp = false;
                TextStudioInspectorElements.ConfigureCodeTextField(_sourceField);
                _sourceField.RegisterValueChangedCallback(OnSourceFieldChanged);
                container.Add(_sourceField);

                _inlineHelp = new TextStudioInlineTagHelpElement(
                    CreateTagContext,
                    () => _sourceField != null && _sourceField.panel != null && _sourceField.visible,
                    ApplySnippet);
                _inlineHelp.AttachSourceField(_sourceField);
                container.Add(_inlineHelp);

                VisualElement toolbar = new VisualElement();
                toolbar.AddToClassList("ts-source-toolbar");
                _tagSummary = new Label();
                _tagSummary.AddToClassList("ts-tag-summary");
                toolbar.Add(_tagSummary);
                toolbar.Add(new VisualElement { style = { flexGrow = 1f } });
                Button tagButton = new Button
                {
                    name = "ts-tag-popup-button",
                    text = "+ Tag",
                    tooltip = "Browse the shared Text Studio tag catalog and insert a balanced tag at the captured selection."
                };
                TextStudioTagInsertionCapture tagInsertion =
                    new TextStudioTagInsertionCapture(_sourceField, tagButton);
                tagButton.clicked += () => OpenTagBrowser(tagInsertion.Consume());
                tagButton.AddToClassList("ts-tag-popup-button");
                toolbar.Add(tagButton);
                container.Add(toolbar);
            }

            void BuildElementSource(VisualElement container)
            {
                container.Add(CreatePropertyField(
                    TextStudioAuthoringPropertyId.SourceElement,
                    "Source Element"));

                IReadOnlyList<string> elementNames = _ownerStore.GetElementNames();
                VisualElement pickerRow = new VisualElement();
                pickerRow.AddToClassList("ts-uitk-binding-picker-row");
                Label pickerBadge = new Label("FIND IN UXML");
                pickerBadge.AddToClassList("ts-uitk-binding-link-badge");
                pickerRow.Add(pickerBadge);
                _elementPickerButton = new Button(OpenElementPicker)
                {
                    text = elementNames.Count > 0
                        ? "Choose Element..."
                        : "No Named Elements",
                    tooltip = elementNames.Count > 0
                        ? "Search named elements in the owning UXML document. The Source Element field remains editable for template and runtime names."
                        : "No named elements could be read from the owning UXML document. You can still enter a runtime or template name manually."
                };
                _elementPickerButton.style.flexGrow = 1f;
                _elementPickerButton.SetEnabled(elementNames.Count > 0);
                pickerRow.Add(_elementPickerButton);
                container.Add(pickerRow);

                container.Add(CreatePropertyField(
                    TextStudioAuthoringPropertyId.SourcePropertyPath,
                    "Property Path"));
                container.Add(TextStudioInspectorElements.CreateMutedText(
                    "Enter a unique element name. TextField.value is the common source, so the default path is value."));
            }

            void BuildDataSource(VisualElement container)
            {
                container.Add(CreatePropertyField(TextStudioAuthoringPropertyId.DataSource, "Data Source"));
                container.Add(CreatePropertyField(TextStudioAuthoringPropertyId.DataSourceType, "Data Source Type"));
                container.Add(CreatePropertyField(TextStudioAuthoringPropertyId.DataSourcePath, "Data Source Path"));
                container.Add(CreatePropertyField(TextStudioAuthoringPropertyId.SourceConverter, "Converter"));
                container.Add(TextStudioInspectorElements.CreateMutedText(
                    "The binding inherits a data source when no explicit object is assigned. Values are converted to string before Text Studio parses markup."));
            }

            VisualElement BuildContentTab()
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("ts-content-tab");
                root.Add(BuildSourceSection());
                root.Add(BuildBehaviorSection());
                root.Add(BuildDefaultEffectsSection());
                return root;
            }

            VisualElement BuildBehaviorSection()
            {
                VisualElement section = CreateSection(
                    "Behavior Profile",
                    "ts-uitk-binding-behavior");
                PropertyField profile = CreatePropertyField(
                    TextStudioAuthoringPropertyId.BehaviorProfile,
                    "Behavior Profile");
                Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                    CreateBehaviorProfileAsset,
                    "ts-behavior-profile-create-button",
                    "Create and assign a reusable Text Studio behavior profile.");
                section.Add(TextStudioInspectorElements.CreateAssetReferenceRow(
                    profile,
                    create,
                    "ts-behavior-profile-row"));
                section.Add(TextStudioInspectorElements.CreateMutedText(
                    "A profile can provide Default Effects, Typewriter behavior, and Reactions. Disabled domains use local settings."));
                TextStudioBehaviorProfile assigned = GetObject<TextStudioBehaviorProfile>(
                    TextStudioAuthoringPropertyId.BehaviorProfile);
                if (assigned != null)
                    section.Add(CreateEditBehaviorProfileButton(assigned));
                return section;
            }

            VisualElement BuildDefaultEffectsSection()
            {
                Foldout section = TextStudioInspectorElements.CreateFoldoutSection(
                    "Default Effects",
                    "ts-default-effects-foldout",
                    false);
                TextStudioBehaviorProfile profile = GetObject<TextStudioBehaviorProfile>(
                    TextStudioAuthoringPropertyId.BehaviorProfile);
                if (profile != null && profile.Provides(TextStudioBehaviorDomain.DefaultEffects))
                {
                    section.Add(TextStudioInspectorElements.CreateMutedText(
                        "These default effects come from the assigned Behavior Profile and stay synchronized for every target using it."));
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Mode",
                        profile.DefaultEffectMode.ToString()));
                    section.Add(CreateEditBehaviorProfileButton(profile));
                    return section;
                }

                section.Add(TextStudioInspectorElements.CreateMutedText(
                    "Fallback applies these effects only where markup has no explicit effect. Constant applies them to the complete text."));
                PropertyField mode = CreateBindingPropertyField(
                    nameof(TextStudioBinding.DefaultEffectsMode),
                    "Default Effects Mode",
                    "Choose whether local default effects are disabled, fallback-only, or constant.");
                section.Add(mode);

                VisualElement expressions = new VisualElement
                {
                    name = "ts-default-effects-expressions"
                };
                expressions.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.DefaultEffects),
                    "All Phase Effects",
                    "Effects applied to Reveal, Active, and Hide."));
                expressions.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.DefaultRevealEffects),
                    "Reveal Effects",
                    "Effects applied while text reveals."));
                expressions.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.DefaultActiveEffects),
                    "Active Effects",
                    "Effects applied while text is fully visible."));
                expressions.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.DefaultHideEffects),
                    "Hide Effects",
                    "Effects applied while text hides."));
                section.Add(expressions);

                SerializedProperty modeProperty = FindBindingProperty(
                    nameof(TextStudioBinding.DefaultEffectsMode));
                Action refresh = () =>
                {
                    TextStudioDefaultEffectMode value = modeProperty != null
                        ? (TextStudioDefaultEffectMode)modeProperty.enumValueIndex
                        : TextStudioDefaultEffectMode.None;
                    expressions.style.display = value == TextStudioDefaultEffectMode.None
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                };
                if (modeProperty != null)
                    section.TrackPropertyValue(modeProperty, _ => refresh());
                refresh();
                return section;
            }

            VisualElement BuildTypewriterTab()
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("ts-typewriter-tab");
                TextStudioBehaviorProfile profile = GetObject<TextStudioBehaviorProfile>(
                    TextStudioAuthoringPropertyId.BehaviorProfile);
                if (profile != null && profile.Provides(TextStudioBehaviorDomain.Typewriter))
                {
                    root.Add(BuildProfileTypewriterSummary(profile));
                    return root;
                }

                VisualElement source = CreateSection(
                    "Typewriter",
                    "ts-uitk-binding-typewriter");
                SerializedProperty sourceProperty = FindBindingProperty(
                    nameof(TextStudioBinding.TypewriterSource));
                PropertyField sourceField = CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterSource),
                    "Source",
                    "None keeps text immediately visible. Built-In and Custom use reusable presets. Inline stores the complete settings in this UXML binding.");
                source.Add(sourceField);
                source.Add(TextStudioInspectorElements.CreateMutedText(
                    "None keeps text visible immediately. Built-In and Custom are live preset references. Inline exposes the complete local settings."));
                root.Add(source);

                TextStudioTypewriterSource typewriterSource = sourceProperty != null
                    ? (TextStudioTypewriterSource)sourceProperty.enumValueIndex
                    : TextStudioTypewriterSource.None;
                if (sourceProperty != null)
                {
                    source.TrackPropertyValue(sourceProperty, _ =>
                        _surface?.RebuildSelectedTab());
                }

                if (typewriterSource == TextStudioTypewriterSource.None)
                    return root;

                if (typewriterSource == TextStudioTypewriterSource.BuiltIn)
                {
                    source.Add(BuildBuiltInTypewriterPresetPicker());
                    return root;
                }

                if (typewriterSource == TextStudioTypewriterSource.Custom)
                {
                    source.Add(CreateBindingPropertyField(
                        nameof(TextStudioBinding.TypewriterPreset),
                        "Custom Asset",
                        "Reusable Text Studio typewriter preset used by this binding."));
                    return root;
                }

                SerializedProperty revealModeProperty = FindBindingProperty(nameof(TextStudioBinding.RevealMode));
                SerializedProperty timingsProperty = FindBindingProperty(nameof(TextStudioBinding.Timings));
                SerializedProperty loopProperty = FindBindingProperty(nameof(TextStudioBinding.LoopReveal));
                SerializedProperty revealEffectProperty = FindBindingProperty(nameof(TextStudioBinding.TypewriterRevealEffect));
                SerializedProperty hideEffectProperty = FindBindingProperty(nameof(TextStudioBinding.TypewriterHideEffect));
                _customTimingSelected = timingsProperty != null && timingsProperty.objectReferenceValue != null;
                TextStudioTypewriterModeView modeView = new TextStudioTypewriterModeView(() =>
                    TextStudioTypewriterModeState.Read(
                        revealModeProperty,
                        _customTimingSelected,
                        timingsProperty,
                        loopProperty,
                        null,
                        revealEffectProperty,
                        hideEffectProperty));

                VisualElement timing = CreateSection("Reveal Timing", "ts-typewriter-timing");
                timing.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.RevealMode),
                    TextStudioTypewriterModePresentation.ModeLabel,
                    TextStudioTypewriterModePresentation.ModeTooltip));
                Label modeGuidance = TextStudioInspectorElements.CreateMutedText(
                    string.Empty,
                    "ts-typewriter-mode-guidance");
                timing.Add(modeGuidance);
                modeView.SetGuidance(modeGuidance);
                PropertyField timingMode = CreateBindingPropertyField(nameof(TextStudioBinding.TypewriterTiming), "Timing", "Cadence permits overlap; Finish Current Unit waits for each unit's reveal before its reading pause.");
                timing.Add(timingMode);
                modeView.Add(TextStudioTypewriterModeField.TimingMode, timingMode);
                AddTimingPresetControls(timing, modeView);
                timing.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.SpeedMultiplier),
                    TextStudioTypewriterModePresentation.PlaybackSpeedLabel,
                    TextStudioTypewriterModePresentation.PlaybackSpeedTooltip));
                root.Add(timing);

                VisualElement duration = CreateSection(
                    TextStudioTypewriterModePresentation.RevealDurationSectionTitle,
                    "ts-typewriter-duration");
                modeView.SetDurationSection(duration);
                PropertyField revealDuration = CreateBindingPropertyField(
                    nameof(TextStudioBinding.RevealAppearanceDuration),
                    "Reveal Duration",
                    "Seconds each glyph spends in its appearance phase.");
                duration.Add(revealDuration);
                modeView.Add(TextStudioTypewriterModeField.RevealDuration, revealDuration);
                duration.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.RevealDisappearanceDuration),
                    "Hide Duration",
                    "Seconds each glyph spends in its disappearance phase."));
                duration.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.LoopReveal),
                    "Repeat Reveal",
                    "Repeat the authored reveal lifecycle at runtime. Preview Repeat is independent."));
                PropertyField loopDelay = CreateBindingPropertyField(
                    nameof(TextStudioBinding.LoopRevealDelay),
                    "Repeat Delay",
                    "Seconds to hold completed text before repeating reveal.");
                duration.Add(loopDelay);
                modeView.Add(TextStudioTypewriterModeField.RepeatDelay, loopDelay);
                root.Add(duration);

                VisualElement behavior = CreateSection("Behavior", "ts-typewriter-behavior");
                behavior.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterStartMode),
                    "Start",
                    "Choose whether reveal starts manually, on attach, on text changes, or both."));
                behavior.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.TextChangeMode),
                    "When Replacing Text",
                    "Replace immediately, hide before reveal, or overlap outgoing and incoming visuals."));
                behavior.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.TextChangeScope),
                    "Affected Text",
                    "Animate only changed glyphs or the complete old and new text."));
                behavior.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.ResetSpeedOnNewText),
                    "Reset Speed On New Text",
                    "Restore the authored reveal speed when the source changes."));
                VisualElement revealSequence = BuildSequenceSettings("Reveal Sequence", true);
                behavior.Add(revealSequence);
                modeView.Add(TextStudioTypewriterModeField.RevealSequence, revealSequence);
                behavior.Add(BuildSequenceSettings("Hide Sequence", false));
                behavior.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.MotionPolicy),
                    "Motion Policy",
                    "Choose how Typewriter transition effects combine with normal text effects."));
                root.Add(behavior);

                VisualElement skip = CreateSection("Skip", "ts-typewriter-skip");
                PropertyField skipReveal = CreateBindingPropertyField(
                    nameof(TextStudioBinding.SkipRevealMode),
                    "Reveal Effects",
                    "Settle reveal effects immediately or allow them to finish when reveal is skipped.");
                skip.Add(skipReveal);
                modeView.Add(TextStudioTypewriterModeField.SkipReveal, skipReveal);
                skip.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.SkipCurrentWaitOnSkip),
                    "Skip Current Wait",
                    "Skip the current timing or input wait together with reveal."));
                skip.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.TriggerRemainingMarkersOnSkip),
                    "Trigger Remaining Markers",
                    "Dispatch remaining marker callbacks when reveal is skipped."));
                skip.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.SkipHideMode),
                    "Hide Effect",
                    "Hide immediately or let the active hide effect finish."));
                root.Add(skip);

                VisualElement phaseEffects = CreateSection(
                    TextStudioTypewriterModePresentation.RevealEffectsSectionTitle,
                    "ts-typewriter-phase-effects");
                modeView.SetEffectsSection(phaseEffects);
                PropertyField revealEffect = CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterRevealEffect),
                    "Reveal Effect",
                    "Optional shared text effect applied only while each semantic unit appears.");
                PropertyField hideEffect = CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterHideEffect),
                    "Hide Effect",
                    "Optional shared text effect applied only while each semantic unit disappears.");
                PropertyField revealTags = CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterRevealEffectTags),
                    "Inline Reveal Tags",
                    "Optional built-in or inline effect tags applied only while each semantic unit appears. A custom effect asset replaces these tags.");
                PropertyField hideTags = CreateBindingPropertyField(
                    nameof(TextStudioBinding.TypewriterHideEffectTags),
                    "Inline Hide Tags",
                    "Optional built-in or inline effect tags applied only while each semantic unit disappears. A custom effect asset replaces these tags.");
                phaseEffects.Add(revealEffect);
                phaseEffects.Add(revealTags);
                phaseEffects.Add(hideEffect);
                phaseEffects.Add(hideTags);
                modeView.Add(TextStudioTypewriterModeField.RevealEffect, revealEffect);
                modeView.Add(TextStudioTypewriterModeField.RevealEffectTags, revealTags);
                modeView.Add(TextStudioTypewriterModeField.HideEffectTags, hideTags);
                phaseEffects.Add(TextStudioInspectorElements.CreateMutedText(
                    "Each phase uses the shared effect library. Choose one reusable asset or inline tags; leave both empty to use the selected Typewriter preset's treatment."));
                root.Add(phaseEffects);

                modeView.Track(root, revealModeProperty, loopProperty, revealEffectProperty, hideEffectProperty);
                modeView.Refresh();
                return root;
            }

            void AddTimingPresetControls(VisualElement timing, TextStudioTypewriterModeView modeView)
            {
                RevealTimingsPreset[] presets;
                List<string> choices =
                    TextStudioInspectorElements.CreateTimingPresetChoices(out presets);
                SerializedProperty builtInProperty = FindBindingProperty(
                    nameof(TextStudioBinding.BuiltInTimingsPreset));
                SerializedProperty customProperty = FindBindingProperty(
                    nameof(TextStudioBinding.Timings));
                int selected = TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                    presets,
                    builtInProperty != null
                        ? (RevealTimingsPreset)builtInProperty.enumValueIndex
                        : RevealTimingsPreset.Uniform,
                    _customTimingSelected);
                DropdownField picker = new DropdownField("Preset", choices, selected)
                {
                    name = "ts-timings-preset-picker",
                    tooltip = "Choose a built-in reveal cadence or Custom to assign a reusable timing asset."
                };
                picker.AddToClassList(UnityAlignedFieldClass);
                timing.Add(picker);
                modeView.Add(TextStudioTypewriterModeField.CadencePreset, picker);

                Label description = TextStudioInspectorElements.CreateMutedText(string.Empty);
                description.name = "ts-timings-preset-description";
                timing.Add(description);
                modeView.Add(TextStudioTypewriterModeField.CadencePreset, description);

                PropertyField customAsset = CreateBindingPropertyField(
                    nameof(TextStudioBinding.Timings),
                    "Custom Asset",
                    "Reusable timing asset used instead of the selected built-in cadence. In Custom mode it also defines reveal groups.");
                Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                    CreateTimingsAsset,
                    "ts-timings-create-button",
                    "Create and assign a custom character timing asset.");
                VisualElement customRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                    customAsset,
                    create,
                    "ts-timings-row");
                timing.Add(customRow);
                modeView.Add(TextStudioTypewriterModeField.TimingAsset, customRow);
                modeView.SetCreateTimingAsset(create);
                HelpBox missing = TextStudioInspectorElements.CreateHelpBox(
                    TextStudioTypewriterModePresentation.GetMissingTimingAssetMessage(RevealMode.ByGlyph),
                    HelpBoxMessageType.Warning);
                missing.name = "ts-timings-missing-help";
                timing.Add(missing);
                modeView.SetMissingTimingAsset(missing);

                PropertyField waitAfterFinalReveal = CreateBindingPropertyField(
                    nameof(TextStudioBinding.WaitAfterFinalReveal),
                    "Wait After Final Reveal",
                    "With a built-in cadence, include the final unit's trailing delay before completion. Custom timing assets own this choice.");
                waitAfterFinalReveal.name = "ts-typewriter-wait-after-final-reveal";
                timing.Add(waitAfterFinalReveal);
                modeView.Add(TextStudioTypewriterModeField.FinalRevealWait, waitAfterFinalReveal);

                Action refresh = () =>
                {
                    if (customProperty != null && customProperty.objectReferenceValue != null)
                        _customTimingSelected = true;
                    int next = _customTimingSelected
                        ? presets.Length
                        : TextStudioInspectorElements.GetTimingPresetChoiceIndex(
                            presets,
                            builtInProperty != null
                                ? (RevealTimingsPreset)builtInProperty.enumValueIndex
                                : RevealTimingsPreset.Uniform,
                            false);
                    picker.SetValueWithoutNotify(choices[Mathf.Clamp(next, 0, choices.Count - 1)]);
                    description.text = TextStudioInspectorElements.GetTimingPresetDescription(
                        presets,
                        next);
                    modeView.Refresh();
                };
                picker.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index < 0 || index > presets.Length)
                        return;
                    _customTimingSelected = index == presets.Length;
                    if (!_customTimingSelected)
                        SetBuiltInTimingPreset(presets[index]);
                    refresh();
                });
                if (customProperty != null)
                    timing.TrackPropertyValue(customProperty, _ => refresh());
                if (builtInProperty != null)
                    timing.TrackPropertyValue(builtInProperty, _ => refresh());
                refresh();
            }

            VisualElement BuildProfileTypewriterSummary(TextStudioBehaviorProfile profile)
            {
                VisualElement section = CreateSection(
                    "Typewriter",
                    "ts-uitk-binding-profile-typewriter");
                section.Add(TextStudioInspectorElements.CreateMutedText(
                    "This Typewriter recipe comes from the assigned Behavior Profile. Editing the profile updates every Text Studio target using it."));
                section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                    "Profile",
                    profile.name));
                section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                    "Source",
                    profile.TypewriterSource.ToString()));

                if (profile.TypewriterSource == TextStudioTypewriterSource.BuiltIn)
                {
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Preset",
                        GetBuiltInTypewriterDisplayName(profile.TypewriterPresetId)));
                }
                else if (profile.TypewriterSource == TextStudioTypewriterSource.Custom)
                {
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Custom Asset",
                        profile.TypewriterPreset != null
                            ? profile.TypewriterPreset.name
                            : "Not assigned"));
                }
                else if (profile.TypewriterSource == TextStudioTypewriterSource.Inline)
                {
                    TextStudioTypewriterSettings settings = profile.GetTypewriterSettings();
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Reveal",
                        settings.RevealMode.ToString()));
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Start",
                        settings.StartMode.ToString()));
                    section.Add(TextStudioInspectorElements.CreateKeyValueRow(
                        "Speed",
                        settings.RevealSpeedMultiplier.ToString("0.##")));
                }

                section.Add(CreateEditBehaviorProfileButton(profile));
                return section;
            }

            static string GetBuiltInTypewriterDisplayName(string presetId)
            {
                IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets =
                    BuiltInTypewriterPresets.All();
                for (int i = 0; i < presets.Count; i++)
                {
                    if (string.Equals(presets[i].Id, presetId, StringComparison.Ordinal))
                        return presets[i].DisplayName;
                }

                return string.IsNullOrWhiteSpace(presetId) ? "Default" : presetId;
            }

            static Button CreateEditBehaviorProfileButton(TextStudioBehaviorProfile profile)
            {
                Button edit = new Button(() =>
                {
                    Selection.activeObject = profile;
                    EditorGUIUtility.PingObject(profile);
                })
                {
                    name = "ts-edit-behavior-profile",
                    text = "Edit Assigned Profile",
                    tooltip = "Select the shared Behavior Profile in the Inspector. Changes affect every target using it."
                };
                edit.AddToClassList("ts-inline-action-button");
                return edit;
            }

            VisualElement BuildBuiltInTypewriterPresetPicker()
            {
                IReadOnlyList<TextStudioBuiltInTypewriterPreset> presets =
                    BuiltInTypewriterPresets.All();
                List<string> choices = new List<string>(presets.Count);
                SerializedProperty property = FindBindingProperty(
                    nameof(TextStudioBinding.TypewriterPresetId));
                int selected = 0;
                for (int i = 0; i < presets.Count; i++)
                {
                    choices.Add(presets[i].DisplayName);
                    if (property != null && string.Equals(
                            property.stringValue,
                            presets[i].Id,
                            StringComparison.Ordinal))
                    {
                        selected = i;
                    }
                }

                VisualElement root = new VisualElement();
                DropdownField picker = new DropdownField("Preset", choices, selected)
                {
                    name = "ts-typewriter-preset-picker",
                    tooltip = "Choose a complete built-in timing and Reveal/Hide effect recipe."
                };
                picker.AddToClassList(UnityAlignedFieldClass);
                Label description = TextStudioInspectorElements.CreateMutedText(
                    presets.Count > 0 ? presets[selected].Description : string.Empty);
                picker.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index < 0 || index >= presets.Count)
                        return;
                    SetBindingString(
                        nameof(TextStudioBinding.TypewriterPresetId),
                        presets[index].Id,
                        "Choose Text Studio Typewriter Preset");
                    description.text = presets[index].Description;
                });
                root.Add(picker);
                root.Add(description);
                return root;
            }

            VisualElement BuildSequenceSettings(string label, bool reveal)
            {
                Foldout sequence = new Foldout
                {
                    text = label,
                    value = false
                };
                sequence.AddToClassList("ts-nested-settings-foldout");
                sequence.Add(CreateBindingPropertyField(reveal ? nameof(TextStudioBinding.RevealSequenceDirection) : nameof(TextStudioBinding.HideSequenceDirection), "Sequence Direction", "Order units by logical text order or their original layout position."));
                sequence.Add(CreateBindingPropertyField(
                    reveal
                        ? nameof(TextStudioBinding.RevealSequenceOrder)
                        : nameof(TextStudioBinding.HideSequenceOrder),
                    "Order",
                    "Choose the semantic order used for this transition."));
                sequence.Add(CreateBindingPropertyField(
                    reveal
                        ? nameof(TextStudioBinding.RevealSequenceSpread)
                        : nameof(TextStudioBinding.HideSequenceSpread),
                    "Spread",
                    "Delay spread applied across the sequence."));
                sequence.Add(CreateBindingPropertyField(
                    reveal
                        ? nameof(TextStudioBinding.RevealSequenceSeed)
                        : nameof(TextStudioBinding.HideSequenceSeed),
                    "Seed",
                    "Stable randomization seed for randomized sequence orders."));
                return sequence;
            }

            VisualElement BuildPlacementTab()
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("ts-placement-tab");
                VisualElement placement = CreateSection("Placement", "ts-placement-settings");
                PlacementMode[] modes =
                {
                    PlacementMode.Linear,
                    PlacementMode.Arc,
                    PlacementMode.Wave,
                    PlacementMode.Circle,
                    PlacementMode.Grid
                };
                List<string> choices = new List<string>
                {
                    "Linear",
                    "Arc",
                    "Wave",
                    "Circle",
                    "Grid"
                };
                SerializedProperty modeProperty = FindBindingProperty(
                    nameof(TextStudioBinding.PlacementMode));
                PlacementMode mode = modeProperty != null
                    ? (PlacementMode)modeProperty.enumValueIndex
                    : PlacementMode.Linear;
                int selected = Array.IndexOf(modes, mode);
                if (selected < 0)
                    selected = 0;
                DropdownField picker = new DropdownField("Placement Mode", choices, selected)
                {
                    tooltip = "Choose a built-in UI Toolkit glyph placement. Changes preview directly in the UI Builder canvas."
                };
                picker.AddToClassList(UnityAlignedFieldClass);
                picker.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index < 0 || index >= modes.Length)
                        return;
                    SetBindingEnum(
                        nameof(TextStudioBinding.PlacementMode),
                        (int)modes[index],
                        "Change Text Studio Placement");
                    _surface?.RebuildSelectedTab();
                });
                placement.Add(picker);
                if (mode == PlacementMode.Spline || mode == PlacementMode.Extension)
                {
                    placement.Add(TextStudioInspectorElements.CreateHelpBox(
                        "Spline and component-provided placement strategies require a GameObject host. Choose a built-in placement for a UXML binding.",
                        HelpBoxMessageType.Warning));
                }

                switch (mode)
                {
                    case PlacementMode.Arc:
                        AddArcPlacementFields(placement);
                        break;
                    case PlacementMode.Wave:
                        AddWavePlacementFields(placement);
                        break;
                    case PlacementMode.Circle:
                        AddCirclePlacementFields(placement);
                        break;
                    case PlacementMode.Grid:
                        AddGridPlacementFields(placement);
                        break;
                    case PlacementMode.Linear:
                    default:
                        placement.Add(TextStudioInspectorElements.CreateMutedText(
                            "Linear keeps the Label's authored layout positions."));
                        break;
                }
                root.Add(placement);

                Foldout orientation = TextStudioInspectorElements.CreateFoldoutSection(
                    "Orientation",
                    "ts-placement-orientation",
                    false);
                orientation.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.PlacementOrientation),
                    "Glyph Orientation",
                    "Keep the original facing, follow the path, face outward or inward, or use the placement default."));
                orientation.Add(CreateBindingPropertyField(
                    nameof(TextStudioBinding.PlacementFineRotation),
                    "Fine Rotation",
                    "Additional local Euler rotation applied after placement orientation."));
                root.Add(orientation);
                return root;
            }

            void AddArcPlacementFields(VisualElement section)
            {
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcAutoFitRadius), "Auto Fit Radius", "Increase radius when required to avoid glyph overlap."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcRadius), "Radius", "Authored arc radius."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcAutoFitPadding), "Auto Fit Padding", "Additional breathing room for automatic radius fitting."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcAngle), "Arc Angle", "Total arc angle in degrees."));
                Foldout advanced = CreatePlacementAdvancedFoldout();
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcPathPosition), "Path Position", "Place glyphs above, centered on, or below the path."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcStartAngle), "Start Angle", "Angular offset in degrees."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcSpacing), "Spacing", "Use even path spacing or preserve proportional source spacing."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcFill), "Fill", "Fraction of the arc used by proportional spacing."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcReverse), "Reverse", "Reverse progression around the arc."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.ArcRadiusChange), "Radius Change", "Signed radius change from first to last glyph."));
                section.Add(advanced);
            }

            void AddWavePlacementFields(VisualElement section)
            {
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveAmplitude), "Amplitude", "Vertical wave amplitude."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveFrequency), "Frequency", "Number of wave cycles across the text."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveSpeed), "Speed", "Continuous wave animation speed."));
                Foldout advanced = CreatePlacementAdvancedFoldout();
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WavePhase), "Phase", "Starting phase offset."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveDepthAmplitude), "Depth Amplitude", "Depth wave amplitude in local units, a quarter cycle ahead of vertical motion."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveSpinSpeed), "Spin Speed", "Continuous glyph roll in revolutions per second, using the host playback clock."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.WaveSpinStagger), "Spin Stagger", "Roll offset in revolutions from the first glyph to the last."));
                section.Add(advanced);
            }

            void AddCirclePlacementFields(VisualElement section)
            {
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleAutoFitRadius), "Auto Fit Radius", "Increase radius when required to avoid glyph overlap."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleRadius), "Radius", "Authored circle radius."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleAutoFitPadding), "Auto Fit Padding", "Additional breathing room for automatic radius fitting."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleStartAngle), "Start Angle", "Angular offset in degrees."));
                Foldout advanced = CreatePlacementAdvancedFoldout();
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CirclePathPosition), "Path Position", "Place glyphs above, centered on, or below the path."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleSpacing), "Spacing", "Use even path spacing or preserve proportional source spacing."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleReverse), "Reverse", "Reverse progression around the circle."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.CircleRadiusChange), "Radius Change", "Signed radius change from first to last glyph."));
                section.Add(advanced);
            }

            void AddGridPlacementFields(VisualElement section)
            {
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridColumns), "Columns", "Number of grid columns."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridCellSizing), "Cell Sizing", "Derive cell size from glyph bounds or use a fixed size."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridCellSize), "Cell Size", "Fixed cell dimensions."));
                section.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridGap), "Gap", "Additional horizontal and vertical cell spacing."));
                Foldout advanced = CreatePlacementAdvancedFoldout();
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridFlow), "Flow", "Fill rows first or columns first."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridReverse), "Reverse", "Reverse the grid fill direction."));
                advanced.Add(CreateBindingPropertyField(nameof(TextStudioBinding.GridRespectSourceLines), "Respect Source Lines", "Start each source line on a new grid row."));
                section.Add(advanced);
            }

            static Foldout CreatePlacementAdvancedFoldout()
            {
                Foldout advanced = new Foldout
                {
                    text = "Advanced",
                    value = false
                };
                advanced.AddToClassList("ts-placement-advanced");
                return advanced;
            }

            VisualElement BuildEventsTab()
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("ts-events-tab");
                root.Add(BuildBindingReactionsSection());
                VisualElement events = CreateSection("Events", "ts-uitk-binding-events");
                events.Add(TextStudioInspectorElements.CreateHelpBox(
                    "UI Toolkit bindings support the same runtime callbacks, but a UXML binding is not a UnityEngine.Object and cannot store persistent UnityEvent listeners. Subscribe from your controller or presenter code.",
                    HelpBoxMessageType.Info));
                events.Add(TextStudioInspectorElements.CreateHelpBox(
                    "Like the Text Studio Text inspector, edit-mode preview runs only preview-safe actions such as waits and speed changes. Runtime-only event markers and gameplay actions run in Play Mode.",
                    HelpBoxMessageType.Info));
                string[,] callbacks =
                {
                    { "OnTextSet", "Markup was accepted" },
                    { "OnTypewriterStarted", "Reveal playback started" },
                    { "OnGlyphRevealed", "A glyph became visible" },
                    { "OnGlyphDisappeared", "A glyph finished hiding" },
                    { "OnTextUnitRevealed", "A semantic reveal unit became visible" },
                    { "OnMarkerReached", "An authored marker was reached" },
                    { "OnTypewriterWaitStarted", "A timed or input wait started" },
                    { "OnTypewriterWaitFinished", "A wait completed or was skipped" },
                    { "OnShowComplete", "The complete text was revealed" },
                    { "OnDisappearComplete", "The complete text disappeared" }
                };
                for (int i = 0; i < callbacks.GetLength(0); i++)
                {
                    VisualElement callback = TextStudioInspectorElements.CreateKeyValueRow(
                        callbacks[i, 0],
                        callbacks[i, 1]);
                    callback.AddToClassList("ts-uitk-event-row");
                    events.Add(callback);
                }
                events.Add(TextStudioInspectorElements.CreateMutedText(
                    "Keep the TextStudioBinding reference returned by SetBinding or your view setup, then add and remove C# event handlers with the view lifecycle."));
                root.Add(events);
                return root;
            }

            VisualElement BuildBindingReactionsSection()
            {
                VisualElement section = CreateSection("Reactions", "ts-uitk-binding-reactions");
                void AddPanelTargets()
                {
                    SerializedProperty targets = FindBindingProperty(nameof(TextStudioBinding.ReactionTargets));
                    if (targets == null)
                        return;
                    PropertyField targetsField = new PropertyField(targets, "Panel Targets")
                    {
                        name = "ts-uitk-reaction-targets",
                        tooltip = "Map recipe target keys to named elements in this binding's panel."
                    };
                    targetsField.BindProperty(targets);
                    section.Add(targetsField);
                }
                TextStudioBehaviorProfile profile = GetObject<TextStudioBehaviorProfile>(
                    TextStudioAuthoringPropertyId.BehaviorProfile);
                if (profile != null && profile.Provides(TextStudioBehaviorDomain.Reactions))
                {
                    section.Add(TextStudioInspectorElements.CreateMutedText(
                        "The assigned Behavior Profile supplies Reactions for this binding."));
                    section.Add(CreateEditBehaviorProfileButton(profile));
                    AddPanelTargets();
                    section.Add(TextStudioInspectorElements.CreateMutedText(
                        "Reactions run in Play Mode. Panel Targets stay on this binding."));
                    return section;
                }

                SerializedProperty source = FindBindingProperty(nameof(TextStudioBinding.ReactionSource));
                SerializedProperty presetId = FindBindingProperty(nameof(TextStudioBinding.BuiltInReactionPresetId));
                SerializedProperty customAsset = FindBindingProperty(nameof(TextStudioBinding.ReactionAsset));
                SerializedProperty inlineLayers = FindBindingProperty(nameof(TextStudioBinding.InlineReactionLayers));
                SerializedProperty capacity = FindBindingProperty(nameof(TextStudioBinding.InlineReactionCapacity));
                SerializedProperty seed = FindBindingProperty(nameof(TextStudioBinding.InlineReactionSeed));
                if (source == null || presetId == null || customAsset == null || inlineLayers == null || capacity == null || seed == null)
                {
                    section.Add(TextStudioInspectorElements.CreateHelpBox(
                        "Reaction source data is unavailable in this UI Toolkit binding. Update the Text Studio package to author reactions here.",
                        HelpBoxMessageType.Warning));
                    return section;
                }

                PropertyField sourceField = new PropertyField(source, "Source")
                {
                    name = "ts-uitk-reaction-source",
                    tooltip = "Choose no reactions, a curated built-in recipe, a reusable asset, or responses stored in this UXML binding."
                };
                sourceField.BindProperty(source);
                section.Add(sourceField);

                IReadOnlyList<TextStudioBuiltInReactionPreset> presets = null;
                List<string> choices = null;
                DropdownField presetPicker = null;
                Label presetDescription = null;
                VisualElement presetRow = null;
                Action refresh = null;
                void EnsurePresetPicker()
                {
                    if (presetPicker != null)
                        return;
                    presets = BuiltInReactionPresets.All();
                    choices = new List<string>(presets.Count);
                    int selected = 0;
                    for (int i = 0; i < presets.Count; i++)
                    {
                        choices.Add(presets[i].DisplayName);
                        if (string.Equals(presetId.stringValue, presets[i].Id, StringComparison.Ordinal))
                            selected = i;
                    }
                    presetPicker = new DropdownField(choices, selected)
                    {
                        name = "ts-uitk-reaction-preset",
                        tooltip = "Select a curated recipe. Convert it to Inline to edit this binding independently."
                    };
                    presetPicker.showMixedValue = presetId.hasMultipleDifferentValues;
                    presetPicker.AddToClassList(UnityAlignedFieldClass);
                    presetDescription = TextStudioInspectorElements.CreateMutedText(
                        presets.Count > 0 ? presets[selected].Description : string.Empty);
                    presetPicker.RegisterValueChangedCallback(evt =>
                    {
                        int index = choices.IndexOf(evt.newValue);
                        if (index < 0 || index >= presets.Count)
                            return;
                        SetBindingString(nameof(TextStudioBinding.BuiltInReactionPresetId),
                            presets[index].Id, "Choose Text Studio Reaction Preset");
                        presetDescription.text = presets[index].Description;
                    });
                    presetRow = TextStudioInspectorElements.CreateAlignedControlRow(
                        "Built-In", presetPicker, "ts-uitk-reaction-preset-row");
                    presetPicker.TrackPropertyValue(presetId, _ =>
                    {
                        _serializedObject.UpdateIfRequiredOrScript();
                        presetPicker.showMixedValue = presetId.hasMultipleDifferentValues;
                        refresh?.Invoke();
                    });
                }

                ObjectField assetField = new ObjectField("Custom Asset")
                {
                    name = "ts-uitk-reaction-asset",
                    objectType = typeof(TextStudioReactionAsset),
                    tooltip = "Reusable reaction recipe shared by hosts and Behavior Profiles."
                };
                assetField.BindProperty(customAsset);
                Button createAsset = TextStudioInspectorElements.CreateCompactCreateButton(
                    CreateBindingReactionAsset, "ts-uitk-reaction-create", "Create a reusable reaction recipe.");
                VisualElement customRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                    assetField, createAsset, "ts-uitk-reaction-custom-row");
                customRow.TrackPropertyValue(customAsset, _ =>
                    TextStudioInspectorElements.SetCompactCreateButtonAvailable(createAsset, customAsset.objectReferenceValue == null));
                TextStudioInspectorElements.SetCompactCreateButtonAvailable(createAsset, customAsset.objectReferenceValue == null);

                PropertyField capacityField = new PropertyField(capacity, "Concurrent Responses")
                {
                    name = "ts-uitk-reaction-capacity",
                    tooltip = "Maximum simultaneous inline responses for this binding. New responses are suppressed at this limit."
                };
                capacityField.BindProperty(capacity);
                PropertyField seedField = new PropertyField(seed, "Seed")
                {
                    name = "ts-uitk-reaction-seed",
                    tooltip = "Stable random seed for chance and randomized reaction choices."
                };
                seedField.BindProperty(seed);
                TextStudioReactionLayerStackView inlineStack = new TextStudioReactionLayerStackView(
                    _serializedObject, inlineLayers.propertyPath, "ts-uitk-reaction-inline");
                Button convert = TextStudioInspectorElements.CreateButton(
                    "Convert to Inline",
                    "Copy the selected preset or asset into editable reactions stored in this UXML binding.",
                    ConvertBindingReactionsToInline);
                convert.name = "ts-uitk-reaction-convert-inline";
                Button saveInline = TextStudioInspectorElements.CreateButton(
                    "Save as Reaction Asset",
                    "Create one reusable asset per selected binding and use it as that binding's Custom reaction source.",
                    SaveBindingInlineReactionsAsAssets);
                saveInline.name = "ts-uitk-reaction-save-inline";

                VisualElement choicesRoot = new VisualElement { name = "ts-uitk-reaction-source-body" };
                section.Add(choicesRoot);
                void AddConversionControls()
                {
                    string reason = TextStudioReactionRecipeInspector.GetBindingInlineConversionUnavailableReason(
                        _serializedObject.targetObjects, _bindingPropertyPath);
                    choicesRoot.Add(convert);
                    convert.SetEnabled(string.IsNullOrEmpty(reason));
                    if (!string.IsNullOrEmpty(reason))
                        choicesRoot.Add(TextStudioInspectorElements.CreateMutedText(reason));
                }
                refresh = () =>
                {
                    _serializedObject.UpdateIfRequiredOrScript();
                    choicesRoot.Clear();
                    TextStudioReactionSource value = (TextStudioReactionSource)source.enumValueIndex;
                    if (source.hasMultipleDifferentValues)
                    {
                        choicesRoot.Add(TextStudioInspectorElements.CreateMutedText(
                            "Selected bindings use different reaction sources. Each can keep its own source when converted to Inline."));
                        AddConversionControls();
                        return;
                    }
                    if (value == TextStudioReactionSource.BuiltIn)
                    {
                        EnsurePresetPicker();
                        choicesRoot.Add(presetDescription);
                        choicesRoot.Add(presetRow);
                        AddConversionControls();
                    }
                    else if (value == TextStudioReactionSource.Custom)
                    {
                        choicesRoot.Add(customRow);
                        if (customAsset.objectReferenceValue == null)
                            choicesRoot.Add(TextStudioInspectorElements.CreateMutedText(
                                "Assign a reusable reaction asset or use + to create one."));
                        AddConversionControls();
                    }
                    else if (value == TextStudioReactionSource.Inline)
                    {
                        choicesRoot.Add(capacityField);
                        choicesRoot.Add(seedField);
                        choicesRoot.Add(inlineStack.Root);
                        choicesRoot.Add(saveInline);
                    }
                    else
                    {
                        choicesRoot.Add(TextStudioInspectorElements.CreateMutedText(
                            "Reactions are off. This binding creates no reaction scheduler."));
                        convert.SetEnabled(false);
                    }
                };
                sourceField.TrackPropertyValue(source, _ => refresh());
                assetField.TrackPropertyValue(customAsset, _ => refresh());
                refresh();
                inlineStack.BindStructureTracking();
                AddPanelTargets();
                section.Add(TextStudioInspectorElements.CreateMutedText(
                    "Reactions run in Play Mode. A Behavior Profile with Reactions enabled replaces these local reactions."));
                return section;
            }

            void CreateBindingReactionAsset()
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Create Reactions", "Text Studio Reactions", "asset",
                    "Choose where to save the reusable reaction recipe.");
                if (string.IsNullOrEmpty(path))
                    return;
                TextStudioReactionAsset asset = ScriptableObject.CreateInstance<TextStudioReactionAsset>();
                AssetDatabase.CreateAsset(asset, path);
                Undo.RegisterCreatedObjectUndo(asset, "Create Text Studio Reactions");
                SetBindingObject(nameof(TextStudioBinding.ReactionAsset), asset, "Create Text Studio Reactions");
                SetBindingEnum(nameof(TextStudioBinding.ReactionSource), (int)TextStudioReactionSource.Custom,
                    "Choose Custom Text Studio Reactions");
                EditorGUIUtility.PingObject(asset);
            }

            void ConvertBindingReactionsToInline()
            {
                if (!TextStudioReactionRecipeInspector.ConvertBindingToInline(
                        _serializedObject, _bindingPropertyPath, out string error))
                {
                    Debug.LogWarning(error, _serializedObject.targetObject);
                    return;
                }
                ApplyPreviewSettings();
            }

            void SaveBindingInlineReactionsAsAssets()
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Inline Reactions", "Text Studio Reactions", "asset",
                    "Choose the asset name and folder. Multiple selected bindings create separate assets in this folder.");
                if (string.IsNullOrEmpty(path))
                    return;
                if (!TextStudioReactionRecipeInspector.TrySaveInlineBindingAsAssets(
                        _serializedObject, _bindingPropertyPath, path,
                        out TextStudioReactionAsset[] saved, out string error))
                {
                    Debug.LogWarning(error, _serializedObject.targetObject);
                    return;
                }
                ApplyPreviewSettings();
                if (saved.Length > 0)
                    EditorGUIUtility.PingObject(saved[0]);
            }

            VisualElement BuildPreviewSection()
            {
                VisualElement section = CreateSection("Preview", "ts-preview-panel");
                section.AddToClassList("ts-preview-section");
                section.style.display = DisplayStyle.Flex;

                _previewModeState = new Toggle
                {
                    name = "ts-uitk-preview-mode-state",
                    value = true
                };
                _previewModeState.style.display = DisplayStyle.None;
                _previewModeState.RegisterValueChangedCallback(_ =>
                {
                    _previewModeInitialized = true;
                    ApplyPreviewMode();
                });
                section.Add(_previewModeState);

                GUIContent[] previewModes =
                {
                    new GUIContent("Raw", "Show canonical Text Studio markup on the UI Builder Label."),
                    new GUIContent("Live", "Render and animate the binding on the actual UI Builder Label.")
                };
                _previewModeControl = CommonUITK.CreateSegmentedControl(
                    previewModes,
                    1,
                    index =>
                    {
                        if (_previewModeState != null)
                            _previewModeState.value = index != 0;
                    },
                    s_PreviewModeClasses);
                _previewModeControl.name = "ts-authoring-mode-control";
                int previewModeIndex = 0;
                foreach (VisualElement child in _previewModeControl.Children())
                {
                    if (!(child is Button button))
                        continue;
                    button.name = previewModeIndex == 0
                        ? "ts-authoring-raw-button"
                        : "ts-authoring-preview-button";
                    previewModeIndex++;
                }
                section.Add(_previewModeControl);

                _previewLiveBody = new VisualElement
                {
                    name = "ts-preview-live-body"
                };
                _previewLiveBody.AddToClassList("ts-preview-live-body");
                section.Add(_previewLiveBody);

                _previewTargetStatus = new Label();
                _previewTargetStatus.name = "ts-uitk-live-preview-status";
                _previewTargetStatus.AddToClassList("ts-inline-info-text");
                _previewLiveBody.Add(_previewTargetStatus);
                _previewControlsHost = new VisualElement
                {
                    name = "ts-uitk-live-preview-controls"
                };
                _previewLiveBody.Add(_previewControlsHost);
                _previewControls = new TextStudioAuthoringPreviewControls(
                    new TextStudioAuthoringPreviewContext
                    {
                        PreviewProvider = () => _previewBinding?.Preview,
                        DisplayProgress = () =>
                            _previewBinding != null && _previewBinding.Preview.ShowEndState
                                ? _previewBinding.RevealProgress
                                : _previewBinding?.Preview.Progress ?? 0f,
                        IsRevealing = () =>
                            _previewBinding != null &&
                            _previewBinding.IsRevealing &&
                            _previewBinding.TotalGlyphCount > 0 &&
                            _previewBinding.RevealedGlyphCount < _previewBinding.TotalGlyphCount,
                        IsDisappearing = () => _previewBinding?.IsDisappearing ?? false,
                        IsWaitingForAction = () => _previewBinding?.IsWaitingForAction ?? false,
                        IsRevealPaused = () => _previewBinding?.IsRevealPaused ?? false,
                        SkipReveal = () => _previewBinding?.SkipReveal(),
                        ResumeReveal = () => _previewBinding?.ResumeReveal(),
                        SkipCurrentWait = () => _previewBinding?.SkipCurrentWait(),
                        StateChanged = OnPreviewStateChanged
                    });
                _previewControls.AddTo(_previewControlsHost);
                RefreshLivePreviewTarget();
                ApplyPreviewMode();
                _previewUpdateLoop?.Pause();
                _previewUpdateLoop = section.schedule.Execute(TickLivePreview).Every(16);
                TextStudioInspectorElements.SchedulePeriodicRefresh(section, () =>
                {
                    if (_root?.panel != null && _root.visible)
                    {
                        RefreshLivePreviewTarget();
                        _previewControls?.Refresh();
                    }
                }, 120);
                section.RegisterCallback<AttachToPanelEvent>(_ =>
                    section.schedule.Execute(() =>
                    {
                        _lastPreviewUpdateTime = EditorApplication.timeSinceStartup;
                        ApplyPreviewMode();
                        RefreshLivePreviewTarget();
                        OnPreviewStateChanged();
                    }));

                return section;
            }

            VisualElement BuildDiagnosticsSection()
            {
                VisualElement section = CreateSection("Diagnostics", "ts-uitk-binding-diagnostics-section");
                _diagnostics = new VisualElement();
                _diagnostics.AddToClassList("ts-uitk-binding-diagnostics");
                section.Add(_diagnostics);
                return section;
            }

            VisualElement BuildDebugTab()
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("ts-debug-tab");
                root.Add(BuildDiagnosticsSection());
                root.Add(BuildAdvancedSection());
                RefreshDiagnostics();
                return root;
            }

            VisualElement BuildAdvancedSection()
            {
                VisualElement section = CreateSection(
                    "Binding",
                    "ts-uitk-binding-advanced");
                section.Add(CreatePropertyField(TextStudioAuthoringPropertyId.UpdateMode, "Update Mode"));
                if (TextStudioUITKCapabilities.TextGeneratorPreferenceAvailable)
                {
                    section.Add(CreatePropertyField(
                        TextStudioAuthoringPropertyId.TextGeneratorPreference,
                        "Text Generator"));
                }
                else
                {
                    section.Add(TextStudioInspectorElements.CreateMutedText(
                        "Text generator selection becomes available with Unity 6000.2."));
                }

                SerializedProperty targetProperty =
                    Storage.FindProperty(TextStudioAuthoringPropertyId.TargetProperty);
                PropertyField targetField = CreatePropertyField(targetProperty, "Target Property");
                targetField.SetEnabled(false);
                section.Add(targetField);
                section.Add(TextStudioInspectorElements.CreateMutedText(
                    "Direction is fixed to To Target and updates react to source changes. Text Studio owns its animation scheduler independently."));

                Foldout developer = new Foldout
                {
                    text = "Developer Diagnostics",
                    value = false,
                    viewDataKey = "text-studio-uitk-developer-diagnostics"
                };
                developer.Add(TextStudioInspectorElements.CreateMutedText(
                    "Binding property path: " + _bindingPropertyPath));
                developer.Add(TextStudioInspectorElements.CreateMutedText(
                    "Runtime feature set: " + TextStudioUITKCapabilities.CurrentFeatureSet));
                section.Add(developer);
                return section;
            }

            VisualElement CreateSection(string title, string name)
            {
                return TextStudioInspectorElements.CreateSection(title, name);
            }

            PropertyField CreatePropertyField(TextStudioAuthoringPropertyId id, string label)
            {
                return CreatePropertyField(Storage.FindProperty(id), label);
            }

            PropertyField CreatePropertyField(SerializedProperty property, string label)
            {
                PropertyField field = new PropertyField(property, label);
                field.AddToClassList("ts-property-field");
                field.AddToClassList(UnityAlignedFieldClass);
                if (property == null)
                {
                    field.SetEnabled(false);
                    return field;
                }

                field.tooltip = TextStudioAuthoringSchema.Get(ResolvePropertyId(property)).Tooltip;
                field.BindProperty(property);
                field.RegisterCallback<SerializedPropertyChangeEvent>(OnSerializedPropertyChanged);
                return field;
            }

            SerializedProperty FindBindingProperty(string propertyName)
            {
                SerializedProperty binding = BindingProperty;
                return binding == null || string.IsNullOrEmpty(propertyName)
                    ? null
                    : binding.FindPropertyRelative(propertyName);
            }

            PropertyField CreateBindingPropertyField(
                string propertyName,
                string label,
                string tooltip)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                PropertyField field = new PropertyField(property, label)
                {
                    name = "ts-binding-" + propertyName,
                    tooltip = tooltip ?? string.Empty
                };
                field.AddToClassList("ts-property-field");
                field.AddToClassList(UnityAlignedFieldClass);
                if (property == null)
                {
                    field.SetEnabled(false);
                    return field;
                }

                field.BindProperty(property);
                if (UsesStablePlacementCommit(propertyName))
                {
                    ConfigureDelayedNumericFields(field);
                    field.RegisterCallback<AttachToPanelEvent>(_ =>
                        field.schedule.Execute(() => ConfigureDelayedNumericFields(field)));
                }
                field.RegisterCallback<SerializedPropertyChangeEvent>(OnSerializedPropertyChanged);
                return field;
            }

            static bool UsesStablePlacementCommit(string propertyName)
            {
                return propertyName == nameof(TextStudioBinding.ArcRadius) ||
                       propertyName == nameof(TextStudioBinding.ArcAutoFitPadding) ||
                       propertyName == nameof(TextStudioBinding.ArcAngle) ||
                       propertyName == nameof(TextStudioBinding.ArcStartAngle) ||
                       propertyName == nameof(TextStudioBinding.ArcFill) ||
                       propertyName == nameof(TextStudioBinding.ArcRadiusChange) ||
                       propertyName == nameof(TextStudioBinding.WaveAmplitude) ||
                       propertyName == nameof(TextStudioBinding.WaveDepthAmplitude) ||
                       propertyName == nameof(TextStudioBinding.WaveSpinSpeed) ||
                       propertyName == nameof(TextStudioBinding.WaveSpinStagger) ||
                       propertyName == nameof(TextStudioBinding.WaveFrequency) ||
                       propertyName == nameof(TextStudioBinding.WaveSpeed) ||
                       propertyName == nameof(TextStudioBinding.WavePhase) ||
                       propertyName == nameof(TextStudioBinding.CircleRadius) ||
                       propertyName == nameof(TextStudioBinding.CircleAutoFitPadding) ||
                       propertyName == nameof(TextStudioBinding.CircleStartAngle) ||
                       propertyName == nameof(TextStudioBinding.CircleRadiusChange) ||
                       propertyName == nameof(TextStudioBinding.GridColumns) ||
                       propertyName == nameof(TextStudioBinding.GridCellSize) ||
                       propertyName == nameof(TextStudioBinding.GridGap) ||
                       propertyName == nameof(TextStudioBinding.PlacementFineRotation);
            }

            static void ConfigureDelayedNumericFields(PropertyField propertyField)
            {
                propertyField.Query<FloatField>().ForEach(field => field.isDelayed = true);
                propertyField.Query<IntegerField>().ForEach(field => field.isDelayed = true);
            }

            void SetBindingString(string propertyName, string value, string undoName)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                if (property == null)
                    return;

                UnityEngine.Object[] targets = _serializedObject.targetObjects;
                Undo.RecordObjects(targets, undoName);
                _serializedObject.UpdateIfRequiredOrScript();
                property = FindBindingProperty(propertyName);
                if (property == null)
                    return;
                property.stringValue = value ?? string.Empty;
                MarkOverridden(property);
                _serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty(targets);
                ApplyPreviewSettings();
            }

            void SetBindingEnum(string propertyName, int value, string undoName)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                if (property == null)
                    return;

                UnityEngine.Object[] targets = _serializedObject.targetObjects;
                Undo.RecordObjects(targets, undoName);
                _serializedObject.UpdateIfRequiredOrScript();
                property = FindBindingProperty(propertyName);
                if (property == null)
                    return;
                property.enumValueIndex = value;
                MarkOverridden(property);
                _serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty(targets);
                ApplyPreviewSettings();
            }

            void SetBindingObject(
                string propertyName,
                UnityEngine.Object value,
                string undoName)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                if (property == null)
                    return;

                UnityEngine.Object[] targets = _serializedObject.targetObjects;
                Undo.RecordObjects(targets, undoName);
                _serializedObject.UpdateIfRequiredOrScript();
                property = FindBindingProperty(propertyName);
                if (property == null)
                    return;
                property.objectReferenceValue = value;
                MarkOverridden(property);
                _serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty(targets);
                ApplyPreviewSettings();
            }

            void SetBuiltInTimingPreset(RevealTimingsPreset preset)
            {
                UnityEngine.Object[] targets = _serializedObject.targetObjects;
                Undo.RecordObjects(targets, "Choose Text Studio Timing Preset");
                _serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty builtIn = FindBindingProperty(
                    nameof(TextStudioBinding.BuiltInTimingsPreset));
                SerializedProperty custom = FindBindingProperty(
                    nameof(TextStudioBinding.Timings));
                if (builtIn == null || custom == null)
                    return;
                builtIn.enumValueIndex = (int)preset;
                custom.objectReferenceValue = null;
                MarkOverridden(builtIn);
                MarkOverridden(custom);
                _serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty(targets);
                ApplyPreviewSettings();
            }

            static void MarkTargetsDirty(UnityEngine.Object[] targets)
            {
                if (targets == null)
                    return;
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                        EditorUtility.SetDirty(targets[i]);
                }
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
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                SerializedProperty property = Storage.FindProperty(
                    TextStudioAuthoringPropertyId.BehaviorProfile);
                if (property == null)
                    return;
                _serializedObject.UpdateIfRequiredOrScript();
                property = Storage.FindProperty(TextStudioAuthoringPropertyId.BehaviorProfile);
                property.objectReferenceValue = profile;
                MarkOverridden(property);
                _serializedObject.ApplyModifiedProperties();
                MarkTargetsDirty(_serializedObject.targetObjects);
                ApplyPreviewSettings();
            }

            void CreateTimingsAsset()
            {
                CharacterTimings timings =
                    TextStudioInspectorElements.CreateCharacterTimingsAsset();
                if (timings == null)
                    return;

                SetBindingObject(
                    nameof(TextStudioBinding.Timings),
                    timings,
                    "Assign Text Studio Timing Asset");
            }

            TextStudioAuthoringPropertyId ResolvePropertyId(SerializedProperty property)
            {
                IReadOnlyList<TextStudioAuthoringProperty> properties = TextStudioAuthoringSchema.Properties;
                TextStudioUxmlBindingAuthoringStorage storage = Storage;
                for (int i = 0; i < properties.Count; i++)
                {
                    SerializedProperty candidate = storage.FindProperty(properties[i].Id);
                    if (candidate != null && candidate.propertyPath == property.propertyPath)
                        return properties[i].Id;
                }
                return TextStudioAuthoringPropertyId.TargetProperty;
            }

            void SetSourceMode(int index)
            {
                SerializedProperty mode = Storage.FindProperty(TextStudioAuthoringPropertyId.SourceMode);
                if (mode == null)
                    return;

                TextStudioBindingSourceMode previous = GetSourceMode();
                TextStudioBindingSourceMode next =
                    (TextStudioBindingSourceMode)Mathf.Clamp(index, 0, 2);
                if (previous == next)
                    return;

                if (next == TextStudioBindingSourceMode.LabelText &&
                    _ownerStore.IsAvailable &&
                    string.IsNullOrEmpty(_ownerStore.Text) &&
                    !string.IsNullOrEmpty(_previewBinding?.Markup))
                {
                    _ownerStore.TrySetText(
                        _previewBinding.Markup,
                        "Preserve Text Studio Source Text");
                }

                _serializedObject.UpdateIfRequiredOrScript();
                mode = Storage.FindProperty(TextStudioAuthoringPropertyId.SourceMode);
                mode.enumValueIndex = (int)next;
                MarkOverridden(mode);
                _serializedObject.ApplyModifiedProperties();
                RebuildSourceBody();
                RefreshDiagnostics();
                ApplyPreviewSettings();
            }

            void OpenElementPicker()
            {
                IReadOnlyList<string> elementNames = _ownerStore.GetElementNames();
                if (_elementPickerButton == null || elementNames.Count == 0)
                    return;

                TextStudioElementNameDropdown dropdown = new TextStudioElementNameDropdown(
                    elementNames,
                    SetSourceElementName);
                dropdown.Show(ResolveScreenRect(_elementPickerButton));
            }

            void SetSourceElementName(string elementName)
            {
                SerializedProperty sourceElement =
                    Storage.FindProperty(TextStudioAuthoringPropertyId.SourceElement);
                if (sourceElement == null)
                    return;

                UnityEngine.Object[] targets = _serializedObject.targetObjects;
                Undo.RecordObjects(targets, "Choose Text Studio Source Element");
                _serializedObject.UpdateIfRequiredOrScript();
                sourceElement = Storage.FindProperty(TextStudioAuthoringPropertyId.SourceElement);
                if (sourceElement == null)
                    return;

                sourceElement.stringValue = elementName ?? string.Empty;
                MarkOverridden(sourceElement);
                _serializedObject.ApplyModifiedProperties();
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                        EditorUtility.SetDirty(targets[i]);
                }

                RefreshDiagnostics();
                ApplyPreviewSettings();
            }

            TextStudioBindingSourceMode GetSourceMode()
            {
                SerializedProperty property = Storage.FindProperty(TextStudioAuthoringPropertyId.SourceMode);
                return property != null
                    ? (TextStudioBindingSourceMode)Mathf.Clamp(property.enumValueIndex, 0, 2)
                    : TextStudioBindingSourceMode.LabelText;
            }

            void OnSourceFieldChanged(ChangeEvent<string> evt)
            {
                int cursor = _sourceField != null ? _sourceField.cursorIndex : (evt.newValue?.Length ?? 0);
                int selection = _sourceField != null ? _sourceField.selectIndex : cursor;
                if (!_ownerStore.TrySetText(evt.newValue, "Edit Text Studio Label Markup"))
                    return;

                _session.SetSource(evt.newValue, cursor, selection);
                RefreshDiagnostics();
            }

            void ApplySnippet(TextStudioTagSourceEditor.EditResult edit)
            {
                if (_sourceField == null || !_ownerStore.IsAvailable)
                    return;

                _ownerStore.TrySetText(edit.Text, "Insert Text Studio Tag");
                _session.SetSource(edit.Text, edit.SelectionStart, edit.SelectionEnd);
                _sourceField.SetValueWithoutNotify(edit.Text);
                _sourceField.Focus();
                _sourceField.schedule.Execute(() =>
                {
                    _sourceField.cursorIndex = edit.SelectionEnd;
                    _sourceField.selectIndex = edit.SelectionStart;
                    _inlineHelp?.Refresh(true);
                });
                RefreshDiagnostics();
            }

            void OpenTagBrowser(TextStudioTagInsertionSnapshot insertion)
            {
                if (_sourceField == null || !_ownerStore.IsAvailable)
                    return;

                Rect anchor = ResolveScreenRect(_sourceField);
                TextStudioTagBrowserWindow.Show(
                    anchor,
                    CreateTagContext(insertion.SourceText),
                    insertion,
                    ApplySnippet);
            }

            TextStudioTagAnalysisContext CreateTagContext(string source)
            {
                TextStudioTagBackend backend = TextStudioUITKCapabilities.FullParityAvailable
                    ? TextStudioTagBackend.UIToolkitFullParity
                    : TextStudioTagBackend.UIToolkitBasic;
                return TextStudioTagInspectorContext.BuildForBackend(
                    source,
                    backend,
                    TextStudioUITKCapabilities.FullParityAvailable
                        ? TextStudioRenderDomain.All
                        : TextStudioRenderDomain.FlatText);
            }

            static Rect ResolveScreenRect(VisualElement element)
            {
                Rect bounds = element?.worldBound ?? default;
                EditorWindow owner = EditorWindow.focusedWindow;
                return owner == null
                    ? bounds
                    : new Rect(
                        owner.position.x + bounds.x,
                        owner.position.y + bounds.y,
                        bounds.width,
                        bounds.height);
            }

            void OnSerializedPropertyChanged(SerializedPropertyChangeEvent evt)
            {
                SerializedProperty changed = evt.changedProperty;
                if (changed != null)
                    MarkOverridden(changed);
                _serializedObject.ApplyModifiedProperties();
                ApplyPreviewSettings();
                RefreshDiagnostics();
            }

            void MarkOverridden(SerializedProperty property)
            {
                if (property == null)
                    return;
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                    property.serializedObject,
                    property.propertyPath);
            }

            void OnSessionSourceChanged()
            {
                RefreshPreviewSource();
            }

            void RefreshLivePreviewTarget()
            {
                string targetName = _ownerStore.ElementName;
                TextStudioBinding next = null;
                if (!string.IsNullOrWhiteSpace(targetName))
                    TextStudioBinding.TryGetEditorBinding(targetName, out next);

                if (!ReferenceEquals(_previewBinding, next))
                {
                    if (_previewBinding != null)
                        _previewBinding.ContinueEditorPreviewInDocument();

                    _previewBinding = next;
                    if (_previewBinding != null)
                    {
                        if (!_previewModeInitialized && _previewModeState != null)
                        {
                            _previewModeState.SetValueWithoutNotify(
                                !_previewBinding.EditorRawAuthoringView);
                            _previewModeInitialized = true;
                        }

                        _previewBinding.SetEditorPreviewDriverActive(true);
                        ApplyPreviewSettings();
                        _previewBinding.Preview.ShowEndStateAgain();
                        _previewBinding.RefreshPreviewScheduling();
                    }
                }

                ApplyPreviewMode();

                bool available = _previewBinding != null;
                if (_previewControlsHost != null)
                    _previewControlsHost.SetEnabled(available);
                if (_previewTargetStatus != null)
                {
                    _previewTargetStatus.text = available
                        ? "Live preview is driving #" + targetName + " in the UI Builder canvas."
                        : string.IsNullOrWhiteSpace(targetName)
                            ? "Name this Label to connect the preview controls to the UI Builder canvas."
                            : "Open this UXML document in UI Builder to connect the preview controls to #" + targetName + ".";
                    _previewTargetStatus.EnableInClassList(
                        "ts-uitk-live-preview-connected",
                        available);
                }
            }

            void TickLivePreview()
            {
                double now = EditorApplication.timeSinceStartup;
                if (_lastPreviewUpdateTime <= 0d)
                {
                    _lastPreviewUpdateTime = now;
                    return;
                }

                float deltaTime = Mathf.Clamp(
                    (float)(now - _lastPreviewUpdateTime),
                    0f,
                    0.1f);
                _lastPreviewUpdateTime = now;
                bool live = _previewModeState == null || _previewModeState.value;
                if (_root?.panel == null || !_root.visible || !live || _previewBinding == null)
                    return;

                bool wasPlaying = _previewBinding.Preview.IsPlaying;
                _previewBinding.UpdateFromEditorPreviewDriver(deltaTime);
                if (wasPlaying || _previewBinding.Preview.IsPlaying)
                    _previewControls?.Refresh();
            }

            void ApplyPreviewMode()
            {
                bool live = _previewModeState == null || _previewModeState.value;
                if (_previewLiveBody != null)
                    _previewLiveBody.style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
                if (_previewModeControl != null)
                {
                    CommonUITK.RefreshSegmentedControl(
                        _previewModeControl,
                        live ? 1 : 0,
                        "ts-preview-mode-segment-active");
                }

                _previewBinding?.SetEditorRawAuthoringView(!live);
                if (live)
                    _previewBinding?.RefreshPreviewScheduling();
            }

            void RefreshPreviewSource()
            {
                if (_previewBinding?.Element == null ||
                    GetSourceMode() != TextStudioBindingSourceMode.LabelText)
                    return;

                TextStudioPreview preview = _previewBinding.Preview;
                bool wasPlaying = preview.IsPlaying;
                bool wasScrubbing = preview.IsScrubbing;
                bool showedEndState = preview.ShowEndState;
                float progress = wasScrubbing ? preview.ScrubProgress : preview.Progress;
                _previewBinding.SetText(_ownerStore.Text);
                if (showedEndState)
                    preview.ShowEndStateAgain();
                else if (wasScrubbing)
                    preview.Scrub(progress);
                else if (wasPlaying)
                    preview.Restart();
                else
                    preview.Scrub(progress);
                _previewBinding.RefreshPreviewScheduling();
                _previewControls?.Refresh();
            }

            void ApplyPreviewSettings()
            {
                if (_previewBinding == null)
                    return;

                _previewBinding.BehaviorProfile = GetObject<TextStudioBehaviorProfile>(TextStudioAuthoringPropertyId.BehaviorProfile);
                _previewBinding.DefaultEffectsMode = GetBindingEnum(
                    nameof(TextStudioBinding.DefaultEffectsMode),
                    TextStudioDefaultEffectMode.None);
                _previewBinding.DefaultEffects = GetBindingString(nameof(TextStudioBinding.DefaultEffects));
                _previewBinding.DefaultRevealEffects = GetBindingString(nameof(TextStudioBinding.DefaultRevealEffects));
                _previewBinding.DefaultActiveEffects = GetBindingString(nameof(TextStudioBinding.DefaultActiveEffects));
                _previewBinding.DefaultHideEffects = GetBindingString(nameof(TextStudioBinding.DefaultHideEffects));

                TextStudioTypewriterSource typewriterSource = GetBindingEnum(
                    nameof(TextStudioBinding.TypewriterSource),
                    TextStudioTypewriterSource.None);
                _previewBinding.TypewriterSource = typewriterSource;
                if (typewriterSource == TextStudioTypewriterSource.BuiltIn)
                {
                    _previewBinding.TypewriterPresetId = GetBindingString(
                        nameof(TextStudioBinding.TypewriterPresetId));
                }
                else if (typewriterSource == TextStudioTypewriterSource.Custom)
                {
                    _previewBinding.TypewriterPreset = GetBindingObject<TextStudioTypewriterPreset>(
                        nameof(TextStudioBinding.TypewriterPreset));
                }
                else if (typewriterSource == TextStudioTypewriterSource.Inline)
                {
                    _previewBinding.RevealMode = GetBindingEnum(nameof(TextStudioBinding.RevealMode), RevealMode.Instant);
                    _previewBinding.BuiltInTimingsPreset = GetBindingEnum(nameof(TextStudioBinding.BuiltInTimingsPreset), RevealTimingsPreset.Uniform);
                    _previewBinding.Timings = GetBindingObject<TypingTimingsBase>(nameof(TextStudioBinding.Timings));
                    _previewBinding.WaitAfterFinalReveal = GetBindingBool(nameof(TextStudioBinding.WaitAfterFinalReveal), true);
                    _previewBinding.SpeedMultiplier = GetBindingFloat(nameof(TextStudioBinding.SpeedMultiplier), 1f);
                    _previewBinding.RevealAppearanceDuration = GetBindingFloat(nameof(TextStudioBinding.RevealAppearanceDuration), 0.3f);
                    _previewBinding.RevealDisappearanceDuration = GetBindingFloat(nameof(TextStudioBinding.RevealDisappearanceDuration), 0.3f);
                    _previewBinding.LoopReveal = GetBindingBool(nameof(TextStudioBinding.LoopReveal), false);
                    _previewBinding.LoopRevealDelay = GetBindingFloat(nameof(TextStudioBinding.LoopRevealDelay), 0.75f);
                    _previewBinding.TypewriterStartMode = GetBindingEnum(nameof(TextStudioBinding.TypewriterStartMode), TextStudioTypewriterStartMode.Manual);
                    _previewBinding.TextChangeMode = GetBindingEnum(nameof(TextStudioBinding.TextChangeMode), TextStudioTextChangeMode.Immediate);
                    _previewBinding.TextChangeScope = GetBindingEnum(nameof(TextStudioBinding.TextChangeScope), TextStudioTextChangeScope.ChangedGlyphs);
                    _previewBinding.ResetSpeedOnNewText = GetBindingBool(nameof(TextStudioBinding.ResetSpeedOnNewText), true);
                    _previewBinding.SkipRevealMode = GetBindingEnum(nameof(TextStudioBinding.SkipRevealMode), TextStudioTypewriterSkipRevealMode.SettleRevealEffectsImmediately);
                    _previewBinding.SkipCurrentWaitOnSkip = GetBindingBool(nameof(TextStudioBinding.SkipCurrentWaitOnSkip), true);
                    _previewBinding.TriggerRemainingMarkersOnSkip = GetBindingBool(nameof(TextStudioBinding.TriggerRemainingMarkersOnSkip), false);
                    _previewBinding.SkipHideMode = GetBindingEnum(nameof(TextStudioBinding.SkipHideMode), TextStudioTypewriterSkipHideMode.HideImmediately);
                    _previewBinding.RevealSequenceOrder = GetBindingEnum(nameof(TextStudioBinding.RevealSequenceOrder), TextStudioSequenceOrder.Forward);
                    _previewBinding.RevealSequenceSpread = GetBindingFloat(nameof(TextStudioBinding.RevealSequenceSpread), 0f);
                    _previewBinding.TypewriterTiming = GetBindingEnum(nameof(TextStudioBinding.TypewriterTiming), TextStudioTypewriterTimingMode.Cadence);
                    _previewBinding.RevealSequenceDirection = GetBindingEnum(nameof(TextStudioBinding.RevealSequenceDirection), TextStudioSequenceDirection.Logical);
                    _previewBinding.HideSequenceDirection = GetBindingEnum(nameof(TextStudioBinding.HideSequenceDirection), TextStudioSequenceDirection.Logical);
                    _previewBinding.RevealSequenceSeed = GetBindingInt(nameof(TextStudioBinding.RevealSequenceSeed), 0);
                    _previewBinding.HideSequenceOrder = GetBindingEnum(nameof(TextStudioBinding.HideSequenceOrder), TextStudioSequenceOrder.Forward);
                    _previewBinding.HideSequenceSpread = GetBindingFloat(nameof(TextStudioBinding.HideSequenceSpread), 0f);
                    _previewBinding.HideSequenceSeed = GetBindingInt(nameof(TextStudioBinding.HideSequenceSeed), 0);
                    _previewBinding.TypewriterRevealEffect = GetBindingObject<TextStudioEffectDefinitionAsset>(
                        nameof(TextStudioBinding.TypewriterRevealEffect));
                    _previewBinding.TypewriterHideEffect = GetBindingObject<TextStudioEffectDefinitionAsset>(
                        nameof(TextStudioBinding.TypewriterHideEffect));
                    _previewBinding.TypewriterRevealEffectTags = GetBindingString(
                        nameof(TextStudioBinding.TypewriterRevealEffectTags));
                    _previewBinding.TypewriterHideEffectTags = GetBindingString(
                        nameof(TextStudioBinding.TypewriterHideEffectTags));
                    _previewBinding.MotionPolicy = GetBindingEnum(nameof(TextStudioBinding.MotionPolicy), TextStudioMotionPolicy.Inherit);
                }

                ApplyPreviewPlacementSettings();
                _previewBinding.TextGeneratorPreference = GetEnum(
                    TextStudioAuthoringPropertyId.TextGeneratorPreference,
                    TextStudioUITKCapabilities.DefaultTextGeneratorPreference);
                _previewBinding.SourceElementName = GetBindingString(nameof(TextStudioBinding.SourceElementName));
                _previewBinding.SourcePropertyPath = GetBindingString(nameof(TextStudioBinding.SourcePropertyPath), "value");
                _previewBinding.SourceMode = GetSourceMode();
                RefreshPreviewSource();
                _previewBinding.SetEditorRawAuthoringView(
                    _previewModeState != null && !_previewModeState.value);
            }

            void ApplyPreviewPlacementSettings()
            {
                _previewBinding.BeginPlacementUpdate();
                try
                {
                    _previewBinding.ArcRadius = GetBindingFloat(nameof(TextStudioBinding.ArcRadius), 5f);
                    _previewBinding.ArcAutoFitRadius = GetBindingBool(nameof(TextStudioBinding.ArcAutoFitRadius), true);
                    _previewBinding.ArcAutoFitPadding = GetBindingFloat(nameof(TextStudioBinding.ArcAutoFitPadding), 1.08f);
                    _previewBinding.ArcAngle = GetBindingFloat(nameof(TextStudioBinding.ArcAngle), 180f);
                    _previewBinding.ArcStartAngle = GetBindingFloat(nameof(TextStudioBinding.ArcStartAngle), 0f);
                    _previewBinding.ArcPathPosition = GetBindingEnum(nameof(TextStudioBinding.ArcPathPosition), PathPosition.Above);
                    _previewBinding.ArcSpacing = GetBindingEnum(nameof(TextStudioBinding.ArcSpacing), PathSpacing.Even);
                    _previewBinding.ArcFill = GetBindingFloat(nameof(TextStudioBinding.ArcFill), 1f);
                    _previewBinding.ArcReverse = GetBindingBool(nameof(TextStudioBinding.ArcReverse), false);
                    _previewBinding.ArcRadiusChange = GetBindingFloat(nameof(TextStudioBinding.ArcRadiusChange), 0f);
                    _previewBinding.WaveAmplitude = GetBindingFloat(nameof(TextStudioBinding.WaveAmplitude), 0.5f);
                    _previewBinding.WaveDepthAmplitude = GetBindingFloat(nameof(TextStudioBinding.WaveDepthAmplitude), 0f);
                    _previewBinding.WaveSpinSpeed = GetBindingFloat(nameof(TextStudioBinding.WaveSpinSpeed), 0f);
                    _previewBinding.WaveSpinStagger = GetBindingFloat(nameof(TextStudioBinding.WaveSpinStagger), 0f);
                    _previewBinding.WaveFrequency = GetBindingFloat(nameof(TextStudioBinding.WaveFrequency), 1f);
                    _previewBinding.WaveSpeed = GetBindingFloat(nameof(TextStudioBinding.WaveSpeed), 1f);
                    _previewBinding.WavePhase = GetBindingFloat(nameof(TextStudioBinding.WavePhase), 0f);
                    _previewBinding.CircleRadius = GetBindingFloat(nameof(TextStudioBinding.CircleRadius), 3f);
                    _previewBinding.CircleAutoFitRadius = GetBindingBool(nameof(TextStudioBinding.CircleAutoFitRadius), true);
                    _previewBinding.CircleAutoFitPadding = GetBindingFloat(nameof(TextStudioBinding.CircleAutoFitPadding), 1.08f);
                    _previewBinding.CircleStartAngle = GetBindingFloat(nameof(TextStudioBinding.CircleStartAngle), 0f);
                    _previewBinding.CirclePathPosition = GetBindingEnum(nameof(TextStudioBinding.CirclePathPosition), PathPosition.Above);
                    _previewBinding.CircleSpacing = GetBindingEnum(nameof(TextStudioBinding.CircleSpacing), PathSpacing.Even);
                    _previewBinding.CircleReverse = GetBindingBool(nameof(TextStudioBinding.CircleReverse), false);
                    _previewBinding.CircleRadiusChange = GetBindingFloat(nameof(TextStudioBinding.CircleRadiusChange), 0f);
                    _previewBinding.GridColumns = GetBindingInt(nameof(TextStudioBinding.GridColumns), 10);
                    _previewBinding.GridCellSizing = GetBindingEnum(nameof(TextStudioBinding.GridCellSizing), GridCellSizing.Auto);
                    _previewBinding.GridCellSize = GetBindingVector2(nameof(TextStudioBinding.GridCellSize), new Vector2(0.6f, 1f));
                    _previewBinding.GridGap = GetBindingVector2(nameof(TextStudioBinding.GridGap), Vector2.zero);
                    _previewBinding.GridFlow = GetBindingEnum(nameof(TextStudioBinding.GridFlow), GridFlow.RowsFirst);
                    _previewBinding.GridReverse = GetBindingBool(nameof(TextStudioBinding.GridReverse), false);
                    _previewBinding.GridRespectSourceLines = GetBindingBool(nameof(TextStudioBinding.GridRespectSourceLines), false);
                    _previewBinding.PlacementOrientation = GetBindingEnum(nameof(TextStudioBinding.PlacementOrientation), TextStudioPlacementOrientation.PlacementDefault);
                    _previewBinding.PlacementFineRotation = GetBindingVector3(nameof(TextStudioBinding.PlacementFineRotation), Vector3.zero);
                    _previewBinding.PlacementMode = GetBindingEnum(nameof(TextStudioBinding.PlacementMode), PlacementMode.Linear);
                }
                finally
                {
                    _previewBinding.EndPlacementUpdate();
                }
            }

            T GetObject<T>(TextStudioAuthoringPropertyId id) where T : UnityEngine.Object
            {
                SerializedProperty property = Storage.FindProperty(id);
                return property?.objectReferenceValue as T;
            }

            T GetEnum<T>(TextStudioAuthoringPropertyId id, T fallback) where T : struct
            {
                SerializedProperty property = Storage.FindProperty(id);
                return property != null && Enum.IsDefined(typeof(T), property.enumValueIndex)
                    ? (T)Enum.ToObject(typeof(T), property.enumValueIndex)
                    : fallback;
            }

            T GetBindingObject<T>(string propertyName) where T : UnityEngine.Object
            {
                return FindBindingProperty(propertyName)?.objectReferenceValue as T;
            }

            T GetBindingEnum<T>(string propertyName, T fallback) where T : struct
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null && Enum.IsDefined(typeof(T), property.enumValueIndex)
                    ? (T)Enum.ToObject(typeof(T), property.enumValueIndex)
                    : fallback;
            }

            string GetBindingString(string propertyName, string fallback = "")
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.stringValue ?? string.Empty : fallback;
            }

            float GetBindingFloat(string propertyName, float fallback)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.floatValue : fallback;
            }

            int GetBindingInt(string propertyName, int fallback)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.intValue : fallback;
            }

            bool GetBindingBool(string propertyName, bool fallback)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.boolValue : fallback;
            }

            Vector2 GetBindingVector2(string propertyName, Vector2 fallback)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.vector2Value : fallback;
            }

            Vector3 GetBindingVector3(string propertyName, Vector3 fallback)
            {
                SerializedProperty property = FindBindingProperty(propertyName);
                return property != null ? property.vector3Value : fallback;
            }

            void OnPreviewStateChanged()
            {
                _previewBinding?.RefreshPreviewScheduling();
                _previewControls?.Refresh();
            }

            void RefreshDiagnostics()
            {
                if (_diagnostics == null)
                    return;
                _diagnostics.Clear();

                string source = GetSourceMode() == TextStudioBindingSourceMode.LabelText
                    ? _ownerStore.Text
                    : string.Empty;
                int shown = 0;
                if (GetSourceMode() == TextStudioBindingSourceMode.LabelText)
                {
                    if (!_ownerStore.IsAvailable)
                    {
                        AddDiagnostic(_ownerStore.Guidance, HelpBoxMessageType.Warning);
                        shown++;
                    }
                    else
                    {
                        _session.SetSource(source, _session.SelectionStart, _session.SelectionEnd);
                        TextStudioDiagnosticReport report = _session.Analyze(CreateTagContext(source));
                        if (_tagSummary != null)
                            _tagSummary.text = TextStudioAuthoringSession.BuildTagStatus(report);
                        if (string.IsNullOrWhiteSpace(source))
                        {
                            _diagnostics.Add(TextStudioInspectorElements.CreateMutedText(
                                "Markup is empty. Type text or insert a tag to see Text Studio render."));
                        }
                        else
                        {
                            for (int i = 0; i < report.Diagnostics.Length && shown < 4; i++)
                            {
                                TextStudioDiagnostic diagnostic = report.Diagnostics[i];
                                if (diagnostic.Severity == TextStudioDiagnosticSeverity.Info)
                                    continue;
                                AddDiagnostic(
                                    diagnostic.Message,
                                    diagnostic.Severity == TextStudioDiagnosticSeverity.Error
                                        ? HelpBoxMessageType.Error
                                        : HelpBoxMessageType.Warning);
                                shown++;
                            }
                        }
                    }
                }
                else if (GetSourceMode() == TextStudioBindingSourceMode.Element)
                {
                    SerializedProperty element = Storage.FindProperty(TextStudioAuthoringPropertyId.SourceElement);
                    if (element == null || string.IsNullOrWhiteSpace(element.stringValue))
                    {
                        AddDiagnostic(
                            "Choose a unique Source Element name before this binding can resolve markup.",
                            HelpBoxMessageType.Warning);
                        shown++;
                    }
                    else
                    {
                        int matches = TextStudioUIToolkitUxmlAuthoringStore.CountNamedElementMatches(
                            _ownerStore.GetElementNames(),
                            element.stringValue);
                        if (matches == 0)
                        {
                            AddDiagnostic(
                                "No element named '" + element.stringValue +
                                "' exists in the owning UXML. Keep the manual name only when a template or runtime parent supplies it.",
                                HelpBoxMessageType.Warning);
                            shown++;
                        }
                        else if (matches > 1)
                        {
                            AddDiagnostic(
                                "The owning UXML contains " + matches + " elements named '" +
                                element.stringValue + "'. Use a unique Source Element name.",
                                HelpBoxMessageType.Error);
                            shown++;
                        }
                    }
                }
                else
                {
                    SerializedProperty path = Storage.FindProperty(TextStudioAuthoringPropertyId.DataSourcePath);
                    if (path == null || string.IsNullOrWhiteSpace(path.stringValue))
                    {
                        AddDiagnostic(
                            "Set a Data Source Path for the markup value.",
                            HelpBoxMessageType.Warning);
                        shown++;
                    }
                }

                SerializedProperty target = Storage.FindProperty(TextStudioAuthoringPropertyId.TargetProperty);
                if (target != null && !string.Equals(target.stringValue, "text", StringComparison.Ordinal))
                {
                    AddDiagnostic(
                        "Text Studio can bind only Label.text. For editable text, use a TextField as an Element source for a separate Label.",
                        HelpBoxMessageType.Error);
                    shown++;
                }

                if (shown == 0 && _diagnostics.childCount == 0)
                {
                    _diagnostics.Add(TextStudioInspectorElements.CreateMutedText(
                        "No authoring issues detected for the selected UI Toolkit target."));
                }
            }

            void AddDiagnostic(string message, HelpBoxMessageType type)
            {
                _diagnostics.Add(TextStudioInspectorElements.CreateHelpBox(message, type));
            }

            void OnDetached(DetachFromPanelEvent evt)
            {
                _inlineHelp?.DetachSourceField();
                _inlineHelp = null;
                _previewUpdateLoop?.Pause();
                _previewUpdateLoop = null;
                _previewBinding?.ContinueEditorPreviewInDocument();
                _previewBinding = null;
                _previewControls = null;
                _previewControlsHost = null;
                _previewLiveBody = null;
                _previewModeControl = null;
                _previewModeState = null;
                _previewTargetStatus = null;
                _session.SourceChanged -= OnSessionSourceChanged;
            }

            string ITextStudioAuthoringSurfaceProvider.RootName => "ts-uitk-binding-drawer";
            IReadOnlyList<GUIContent> ITextStudioAuthoringSurfaceProvider.Tabs => s_Tabs;
            int ITextStudioAuthoringSurfaceProvider.SelectedTab
            {
                get => s_SelectedTab;
                set => s_SelectedTab = value;
            }

            VisualElement ITextStudioAuthoringSurfaceProvider.BuildPreview()
            {
                VisualElement preview = BuildPreviewSection();
                ApplyPreviewSettings();
                return preview;
            }

            VisualElement ITextStudioAuthoringSurfaceProvider.BuildTab(int tabIndex)
            {
                return tabIndex switch
                {
                    0 => BuildContentTab(),
                    1 => BuildTypewriterTab(),
                    2 => BuildPlacementTab(),
                    3 => BuildEventsTab(),
                    4 => BuildDebugTab(),
                    _ => null
                };
            }

            bool ITextStudioAuthoringSurfaceProvider.IsTabApplicable(int tabIndex)
            {
                return tabIndex >= 0 && tabIndex < s_Tabs.Length;
            }

            void ITextStudioAuthoringSurfaceProvider.OnSurfaceCreated(VisualElement root)
            {
                ConfigureRoot(root);
            }

            void ITextStudioAuthoringSurfaceProvider.OnTabsCreated(CommonTabbedPane tabs)
            {
            }

            void ITextStudioAuthoringSurfaceProvider.BeforeTabRebuild(VisualElement body)
            {
                _inlineHelp?.DetachSourceField();
                _inlineHelp = null;
                _sourceField = null;
                _sourceBody = null;
                _diagnostics = null;
                _tagSummary = null;
                _elementPickerButton = null;
            }

            void ITextStudioAuthoringSurfaceProvider.AfterTabRebuild(
                VisualElement body,
                int tabIndex)
            {
                RefreshDiagnostics();
            }

            void ITextStudioAuthoringSurfaceProvider.OnSurfaceDetached(VisualElement root)
            {
                OnDetached(null);
            }

            sealed class TextStudioElementNameDropdown : AdvancedDropdown
            {
                readonly IReadOnlyList<string> _elementNames;
                readonly Action<string> _onSelected;

                public TextStudioElementNameDropdown(
                    IReadOnlyList<string> elementNames,
                    Action<string> onSelected)
                    : base(new AdvancedDropdownState())
                {
                    _elementNames = elementNames ?? Array.Empty<string>();
                    _onSelected = onSelected;
                }

                protected override AdvancedDropdownItem BuildRoot()
                {
                    AdvancedDropdownItem root = new AdvancedDropdownItem("Named UXML Elements");
                    SortedSet<string> uniqueNames = new SortedSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < _elementNames.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(_elementNames[i]))
                            uniqueNames.Add(_elementNames[i]);
                    }

                    foreach (string elementName in uniqueNames)
                        root.AddChild(new TextStudioElementNameItem(elementName));
                    return root;
                }

                protected override void ItemSelected(AdvancedDropdownItem item)
                {
                    if (item is TextStudioElementNameItem namedItem)
                        _onSelected?.Invoke(namedItem.ElementName);
                }
            }

            sealed class TextStudioElementNameItem : AdvancedDropdownItem
            {
                public string ElementName { get; }

                public TextStudioElementNameItem(string elementName)
                    : base(elementName)
                {
                    ElementName = elementName;
                }
            }
        }
    }

    [CustomPropertyDrawer(typeof(TextStudioSourceModeAuthoringAttribute))]
    internal sealed class TextStudioSourceModeAuthoringDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            List<string> choices = new List<string>
            {
                "Label Text",
                "Element",
                "Data"
            };
            DropdownField dropdown = new DropdownField(
                property.displayName,
                choices,
                Mathf.Clamp(property.enumValueIndex, 0, choices.Count - 1))
            {
                tooltip = "Choose the canonical text source. Label Text is the normal direct-authoring flow."
            };
            dropdown.AddToClassList("unity-base-field__aligned");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0)
                    return;
                property.serializedObject.UpdateIfRequiredOrScript();
                property.enumValueIndex = index;
                TextStudioUIToolkitUxmlAuthoringStore.MarkAttributeOverridden(
                    property.serializedObject,
                    property.propertyPath);
                property.serializedObject.ApplyModifiedProperties();
            });
            return dropdown;
        }
    }
}
