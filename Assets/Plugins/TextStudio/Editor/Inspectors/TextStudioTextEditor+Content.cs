using System;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using LegacyText = UnityEngine.UI.Text;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal partial class TextStudioTextEditor
    {
        VisualElement BuildUitkRawSourcePanel(TextStudioText hub)
        {
            VisualElement root = CreateSection("Source");
            root.name = "ts-raw-source-panel";
            root.AddToClassList("ts-raw-source-panel");

            bool externalTextSource = HasExternalTextSource(hub);
            if (externalTextSource)
            {
                root.Add(CreateAlignedHint(
                    "External source mode is active. Edit the source component's text; Text Studio still controls reveal, placement, renderers and effects here."));
                return root;
            }

            string backendName = hub.Backends.ActiveBackend != null
                ? hub.Backends.ActiveBackend.DisplayName
                : "TextMeshPro";
            Label tmpDriver = new Label(
                "Edit the markup directly in the " + backendName +
                " text field. Text Studio mirrors it internally.");
            tmpDriver.name = "ts-raw-tmp-driver-message";
            tmpDriver.AddToClassList("ts-inline-info-text");
            root.Add(tmpDriver);

            VisualElement sourceToolbar = new VisualElement();
            sourceToolbar.AddToClassList("ts-source-toolbar");
            _uitkTagSummary = new Label();
            _uitkTagSummary.AddToClassList("ts-tag-summary");
            sourceToolbar.Add(_uitkTagSummary);
            root.Add(sourceToolbar);

            _uitkDiagnostics = new VisualElement();
            _uitkDiagnostics.AddToClassList("ts-source-diagnostics");
            root.Add(_uitkDiagnostics);
            RefreshUitkDiagnostics(hub);
            return root;
        }

        VisualElement BuildUitkSourceEditorPanel(TextStudioText hub)
        {
            VisualElement root = CreateSection("Source");
            root.name = "ts-preview-source-panel";
            root.AddToClassList("ts-preview-source-panel");

            bool externalTextSource = HasExternalTextSource(hub);
            if (externalTextSource)
            {
                root.Add(CreateAlignedHint(
                    "External source mode is active. Edit the source component's text; Text Studio still controls reveal, placement, renderers and effects here."));
                return root;
            }

            _uitkSourceField = new TextField
            {
                multiline = true,
                value = _markup.stringValue
            };
            ConfigureSourceFieldLayout(_uitkSourceField);
            PreserveSourceFocusAcrossInspectorRebuilds(_uitkSourceField, hub);
#if UNITY_6000_0_OR_NEWER
            _uitkSourceField.verticalScrollerVisibility = ScrollerVisibility.Auto;
#endif
            _uitkSourceField.AddToClassList("ts-source-field");
            _uitkSourceField.AddToClassList("ts-source-field-wrap");
            _uitkSourceHighlight = TextStudioSourceHighlightElement.AttachTo(_uitkSourceField);
            _uitkSourceHighlight?.Refresh(
                _uitkSourceField.value,
                ResolveEffectiveMarkupSyntax(hub));
            _uitkSourceField.RegisterValueChangedCallback(evt =>
            {
                _uitkSourceHighlight?.Refresh(
                    evt.newValue,
                    ResolveEffectiveMarkupSyntax(hub));
                ApplySourceText(hub, evt.newValue);
                RefreshUitkDiagnostics(hub);
            });
            root.Add(_uitkSourceField);

            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (!CanRunScheduledInspectorUpdate(_uitkSourceField, hub))
                    return;

                serializedObject.Update();
                string canonicalMarkup = _markup.stringValue ?? string.Empty;
                _uitkSourceHighlight?.Refresh(
                    canonicalMarkup,
                    ResolveEffectiveMarkupSyntax(hub));
                if (string.Equals(
                        _uitkSourceField.value,
                        canonicalMarkup,
                        StringComparison.Ordinal))
                {
                    return;
                }

                _uitkSourceField.SetValueWithoutNotify(canonicalMarkup);
                RefreshUitkInlineTagHelp(hub);
                RefreshUitkDiagnostics(hub);
            }, 120);

            _uitkInlineTagHelp = new TextStudioInlineTagHelpElement(
                source => TextStudioTagInspectorContext.Build(hub, source),
                () => CanRunScheduledInspectorUpdate(_uitkSourceField, hub),
                edit => ApplyMarkupEdit(hub, edit, _uitkSourceField),
                () => ResolveEffectiveMarkupSyntax(hub),
                state => ConvertAdHocAppearance(hub, state, _uitkSourceField),
                () => hub != null ? hub.Markup : string.Empty,
                () =>
                {
                    if (!CanRunScheduledInspectorUpdate(_uitkSourceField, hub)) return;
                    serializedObject.Update();
                    _uitkSourceHighlight?.Refresh(_markup.stringValue, ResolveEffectiveMarkupSyntax(hub));
                    RefreshUitkDiagnostics(hub);
                });
            _uitkInlineTagHelp.AttachSourceField(_uitkSourceField);
            root.Add(_uitkInlineTagHelp);

            VisualElement sourceToolbar = new VisualElement();
            sourceToolbar.AddToClassList("ts-source-toolbar");
            _uitkTagSummary = new Label();
            _uitkTagSummary.AddToClassList("ts-tag-summary");
            sourceToolbar.Add(_uitkTagSummary);
            sourceToolbar.Add(new VisualElement { style = { flexGrow = 1f } });
            Button tagButton = new Button
            {
                name = "ts-tag-popup-button",
                text = "+ Tag",
                tooltip = "Browse the shared Text Studio tag catalog and insert a balanced tag at the captured selection."
            };
            TextStudioTagInsertionCapture tagInsertion =
                new TextStudioTagInsertionCapture(_uitkSourceField, tagButton);
            tagButton.clicked += () => OpenTagBrowser(hub, tagButton, tagInsertion.Consume());
            tagButton.AddToClassList("ts-tag-popup-button");
            sourceToolbar.Add(tagButton);
            root.Add(sourceToolbar);

            _uitkDiagnostics = new VisualElement();
            _uitkDiagnostics.AddToClassList("ts-source-diagnostics");
            root.Add(_uitkDiagnostics);
            RefreshUitkInlineTagHelp(hub);
            RefreshUitkDiagnostics(hub);
            return root;
        }

        static TextStudioMarkupSyntax ResolveEffectiveMarkupSyntax(TextStudioText hub)
        {
            return hub != null && hub.MarkupSyntaxOverride != null
                ? hub.MarkupSyntaxOverride
                : TextStudioProjectSettings.ActiveMarkupSyntax;
        }

        internal static void ConfigureSourceFieldLayout(TextField sourceField)
        {
            if (sourceField == null)
                return;

            TextStudioInspectorElements.ConfigureCodeTextField(sourceField);
            sourceField.style.minHeight = 82f;
            sourceField.style.alignItems = Align.FlexStart;

            VisualElement baseInput =
                sourceField.Q<VisualElement>(className: "unity-base-field__input");
            if (baseInput != null)
            {
                baseInput.style.minWidth = 0f;
                baseInput.style.minHeight = 78f;
                baseInput.style.flexGrow = 1f;
                baseInput.style.flexShrink = 1f;
            }

            TextElement sourceTextElement = sourceField.Q<TextElement>(
                className: TextElement.selectableUssClassName);
            if (sourceTextElement == null)
                return;

            sourceTextElement.style.minHeight = 74f;
            sourceTextElement.style.flexGrow = 1f;
            sourceTextElement.style.flexShrink = 1f;
        }

        static void PreserveSourceFocusAcrossInspectorRebuilds(TextField sourceField, TextStudioText hub)
        {
            sourceField.name = "ts-source-field-" + UnityUtils.GetStableId(hub);
            sourceField.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                IPanel panel = evt.originPanel;
                VisualElement focused = panel?.focusController.focusedElement as VisualElement;
                if (hub == null || focused == null ||
                    (focused != sourceField && !sourceField.Contains(focused)))
                {
                    return;
                }

                int cursor = sourceField.cursorIndex;
                int selection = sourceField.selectIndex;
                VisualElement panelRoot = panel.visualTree;
                // A TMP material change can replace the entire native inspector while typing.
                // Schedule on the surviving panel, since the old field is no longer attached.
                panelRoot.schedule.Execute(() =>
                {
                    if (hub == null || panelRoot.panel != panel || sourceField.panel != null)
                        return;

                    VisualElement currentFocus = panel.focusController.focusedElement as VisualElement;
                    if (currentFocus != null && currentFocus != sourceField &&
                        !sourceField.Contains(currentFocus))
                    {
                        return;
                    }

                    TextField replacement = panelRoot.Q<TextField>(sourceField.name);
                    if (replacement == null || !replacement.enabledInHierarchy)
                        return;

                    replacement.Focus();
                    replacement.SelectRange(
                        Mathf.Clamp(cursor, 0, replacement.value.Length),
                        Mathf.Clamp(selection, 0, replacement.value.Length));
                });
            });
        }

        void ApplyAuthoringMode(TextStudioText hub, TextStudioAuthoringMode mode)
        {
            if (hub == null || hub.AuthoringMode == mode)
                return;

            Undo.RecordObject(hub, "Switch Text Studio Authoring Mode");
            hub.SetAuthoringMode(mode);
            serializedObject.UpdateIfRequiredOrScript();
            BuildUitkActiveTab(hub);
            EditorUtility.SetDirty(hub);
        }

        VisualElement BuildUitkContentTab(TextStudioText hub)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-content-tab");

            TMP_Text tmp = hub.GetComponent<TMP_Text>();
            if (tmp == null &&
                hub.Backends.ActiveBackend == null &&
                hub.Renderers.Count == 0)
            {
                LegacyText legacyText = hub.GetComponent<LegacyText>();
                if (legacyText != null &&
                    TextStudioUnityUITextSetup.IsEditableInputFieldTarget(legacyText))
                {
                    root.Add(CreateHelpBox(
                        "This Unity UI Text is owned by an editable Input Field. Use a separate presentation Text component for Text Studio output.",
                        HelpBoxMessageType.Warning));
                }
                else if (legacyText != null)
                {
                    VisualElement repair = CreateSection("Unity UI Text Setup");
                    TextStudioUnityUITextBackend existingBackend =
                        legacyText.GetComponent<TextStudioUnityUITextBackend>();
                    bool disabled = existingBackend != null && !existingBackend.enabled;
                    repair.Add(CreateHelpBox(
                        disabled
                            ? "The Unity UI Text backend is disabled, so this Text Studio hub has no active text output."
                            : "This object has a Unity UI Text component, but its Text Studio backend is missing. Add the backend to preserve the Text component and enable reveal, placement, and vertex effects.",
                        HelpBoxMessageType.Warning));
                    Button addBackend = TextStudioInspectorElements.CreateButton(
                        disabled ? "Enable Unity UI Text Backend" : "Add Unity UI Text Backend",
                        disabled
                            ? "Enable the existing backend and restore this Unity UI Text output."
                            : "Configure this legacy Text component as the visible Text Studio output.",
                        () =>
                        {
                            if (disabled)
                            {
                                Undo.RecordObject(existingBackend, "Enable Unity UI Text Backend");
                                existingBackend.enabled = true;
                                EditorUtility.SetDirty(existingBackend);
                                if (PrefabUtility.IsPartOfPrefabInstance(existingBackend))
                                {
                                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                                        existingBackend);
                                }
                                hub.Backends.Refresh();
                            }
                            else
                            {
                                TextStudioUnityUITextSetup.ConfigureTarget(
                                    legacyText,
                                    recordUndo: true);
                            }
                            serializedObject.UpdateIfRequiredOrScript();
                            BuildUitkActiveTab(hub);
                        },
                        primary: true);
                    addBackend.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
                    repair.Add(addBackend);
                    root.Add(repair);
                }
                else
                {
                    root.Add(CreateHelpBox(
                        "No text backend or renderer is attached. Add TextMeshPro, a supported text backend, or a Text Studio renderer.",
                        HelpBoxMessageType.Warning));
                }
            }

            root.Add(hub.AuthoringMode == TextStudioAuthoringMode.Raw
                ? BuildUitkRawSourcePanel(hub)
                : BuildUitkSourceEditorPanel(hub));
            root.Add(BuildContentSettingsSection(hub));
            return root;
        }

        VisualElement BuildContentSettingsSection(TextStudioText hub)
        {
            VisualElement root = new VisualElement();
            root.Add(BuildProfileAssignmentSection(hub));
            root.Add(BuildDefaultEffectsSection(hub));
            return root;
        }

        VisualElement BuildProfileAssignmentSection(TextStudioText hub)
        {
            VisualElement root = CreateSection("Behavior Profile");
            Label modeHint = new Label
            {
                name = "ts-profile-mode-hint"
            };
            modeHint.AddToClassList("ts-inline-info-text");

            ObjectField profileField;
            VisualElement profileRow = BuildAssetReferenceRow(
                _behaviorProfile,
                "Behavior Profile",
                typeof(TextStudioBehaviorProfile),
                "ts-behavior-profile-row",
                "ts-behavior-profile-create-button",
                "Create Text Studio behavior profile",
                CreateBehaviorProfileAsset,
                out profileField);
            profileField.SetValueWithoutNotify(hub.BehaviorProfile);
            profileField.RegisterValueChangedCallback(evt =>
            {
                TextStudioBehaviorProfile nextProfile =
                    evt.newValue as TextStudioBehaviorProfile;
                serializedObject.Update();
                if (evt.previousValue == evt.newValue &&
                    _behaviorProfile.objectReferenceValue == nextProfile &&
                    hub.BehaviorProfile == nextProfile)
                {
                    return;
                }

                Undo.RecordObject(hub, "Assign Text Studio Behavior Profile");
                _isEditingAssignedProfile = false;
                hub.SetBehaviorProfile(nextProfile);
                serializedObject.UpdateIfRequiredOrScript();
                EditorUtility.SetDirty(hub);
                RequestUitkActiveTabRebuild(hub);
            });
            root.Add(profileRow);

            TextStudioBehaviorProfile profile = hub.BehaviorProfile;
            modeHint.text = profile == null
                ? "No behavior profile is assigned. This component uses local behavior."
                : "This behavior profile may independently provide Default Effects, Typewriter settings, and Reactions.";
            root.Add(modeHint);

            if (profile != null)
            {
                root.Add(CreateProfileEditButton(hub, "ts-profile-edit-button"));

                if (_isEditingAssignedProfile)
                    root.Add(CreateProfileGlobalEditWarning());
            }

            return root;
        }

        Button CreateProfileEditButton(TextStudioText hub, string name)
        {
            Button edit = new Button(() =>
            {
                _isEditingAssignedProfile = !_isEditingAssignedProfile;
                BuildUitkActiveTab(hub);
            })
            {
                name = name,
                text = _isEditingAssignedProfile
                    ? "Stop Editing Behavior Profile"
                    : "Edit Behavior Profile",
                tooltip = _isEditingAssignedProfile
                    ? "Return behavior-profile values to read-only mode."
                    : "Enable editing for settings owned by the assigned behavior profile."
            };
            edit.AddToClassList("ts-inline-action-button");
            return edit;
        }

        static HelpBox CreateProfileGlobalEditWarning()
        {
            HelpBox warning = CreateHelpBox(
                "Editing this behavior profile affects every Text Studio component using it.",
                HelpBoxMessageType.Warning);
            warning.name = "ts-profile-global-edit-warning";
            return warning;
        }

        VisualElement BuildDefaultEffectsSection(TextStudioText hub)
        {
            TextStudioBehaviorProfile profile = hub.BehaviorProfile;
            bool profileBacked = profile != null &&
                                 profile.Provides(
                                     TextStudioBehaviorDomain.DefaultEffects);
            bool editable = !profileBacked || _isEditingAssignedProfile;
            SerializedObject profileObject = profileBacked ? new SerializedObject(profile) : null;

            Foldout root = CreateFoldoutSection("Default Effects", "ts-default-effects-foldout", false);
            Label modeHint = new Label
            {
                name = "ts-default-effects-source-mode-hint"
            };
            modeHint.AddToClassList("ts-inline-info-text");
            modeHint.text = profileBacked
                ? "These default effects come from the assigned behavior profile."
                : "These default effects are stored on this Text Studio component.";
            root.Add(modeHint);

            VisualElement inlineDefaults = new VisualElement
            {
                name = "ts-inline-default-effects"
            };
            inlineDefaults.AddToClassList("ts-property-group");
            inlineDefaults.AddToClassList("ts-default-effects-group");
            SerializedProperty mode = profileBacked ? profileObject.FindProperty("_defaultEffectMode") : _inlineDefaultEffectMode;
            SerializedProperty all = profileBacked ? profileObject.FindProperty("_defaultEffects") : _inlineDefaultEffects;
            SerializedProperty reveal = profileBacked ? profileObject.FindProperty("_defaultRevealEffects") : _inlineDefaultRevealEffects;
            SerializedProperty active = profileBacked ? profileObject.FindProperty("_defaultActiveEffects") : _inlineDefaultActiveEffects;
            SerializedProperty hide = profileBacked ? profileObject.FindProperty("_defaultHideEffects") : _inlineDefaultHideEffects;
            TextStudioDefaultEffectMode observedMode = (TextStudioDefaultEffectMode)mode.enumValueIndex;
            string observedAll = all.stringValue ?? string.Empty;
            string observedReveal = reveal.stringValue ?? string.Empty;
            string observedActive = active.stringValue ?? string.Empty;
            string observedHide = hide.stringValue ?? string.Empty;
            PropertyField modeField = CreateVisualSettingsPropertyField(mode, "Default Effects Mode", editable, profileBacked);
            modeField.tooltip = DefaultEffectModeTooltip;
            modeField.AddToClassList("ts-default-effects-mode-field");
            inlineDefaults.Add(modeField);

            VisualElement expressions = new VisualElement
            {
                name = "ts-default-effects-expressions"
            };
            AddDefaultEffectsExpressionField(
                expressions,
                all,
                "All Phase Effects",
                "Effects applied to every preview phase before phase-specific effects.",
                editable,
                profileBacked);
            AddDefaultEffectsExpressionField(
                expressions,
                reveal,
                "Reveal Effects",
                "Effects applied while text reveals.",
                editable,
                profileBacked);
            AddDefaultEffectsExpressionField(
                expressions,
                active,
                "Active Effects",
                "Effects applied while text is fully visible.",
                editable,
                profileBacked);
            AddDefaultEffectsExpressionField(
                expressions,
                hide,
                "Hide Effects",
                "Effects applied while text hides.",
                editable,
                profileBacked);
            inlineDefaults.Add(expressions);
            RefreshDefaultEffectsExpressionVisibility(expressions, observedMode);
            Action refreshDefaultEffects = () =>
            {
                if (!TryGetLiveSerializedObject(mode, out SerializedObject settingsObject))
                    return;
                settingsObject.UpdateIfRequiredOrScript();

                TextStudioDefaultEffectMode nextMode = (TextStudioDefaultEffectMode)mode.enumValueIndex;
                string nextAll = all.stringValue ?? string.Empty;
                string nextReveal = reveal.stringValue ?? string.Empty;
                string nextActive = active.stringValue ?? string.Empty;
                string nextHide = hide.stringValue ?? string.Empty;
                RefreshDefaultEffectsExpressionVisibility(expressions, nextMode);
                if (nextMode == observedMode &&
                    string.Equals(nextAll, observedAll, StringComparison.Ordinal) &&
                    string.Equals(nextReveal, observedReveal, StringComparison.Ordinal) &&
                    string.Equals(nextActive, observedActive, StringComparison.Ordinal) &&
                    string.Equals(nextHide, observedHide, StringComparison.Ordinal))
                {
                    return;
                }

                observedMode = nextMode;
                observedAll = nextAll;
                observedReveal = nextReveal;
                observedActive = nextActive;
                observedHide = nextHide;
                hub.ForceMeshRefresh(0f);
            };
            inlineDefaults.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (!TryGetLiveSerializedObject(mode, out SerializedObject settingsObject))
                    return;
                settingsObject.ApplyModifiedProperties();
                refreshDefaultEffects();
            });
            inlineDefaults.TrackPropertyValue(mode, _ => refreshDefaultEffects());
            inlineDefaults.TrackPropertyValue(all, _ => refreshDefaultEffects());
            inlineDefaults.TrackPropertyValue(reveal, _ => refreshDefaultEffects());
            inlineDefaults.TrackPropertyValue(active, _ => refreshDefaultEffects());
            inlineDefaults.TrackPropertyValue(hide, _ => refreshDefaultEffects());
            root.Add(inlineDefaults);

            return root;
        }

        static void RefreshDefaultEffectsExpressionVisibility(
            VisualElement expressions,
            TextStudioDefaultEffectMode mode)
        {
            expressions.style.display = mode == TextStudioDefaultEffectMode.None
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }
}
