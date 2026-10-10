using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudioProjectSettings))]
    internal sealed class TextStudioProjectSettingsEditor : UnityEditor.Editor
    {
        SerializedProperty _overrideDefaults;
        SerializedProperty _defaultRevealMode;
        SerializedProperty _defaultPlacementMode;
        SerializedProperty _defaultCharDelay;
        SerializedProperty _warnOnMissingTags;
        SerializedProperty _motionPolicy;
        SerializedProperty _markupSyntax;
        SerializedProperty _effectCatalog;
        SerializedProperty _appearanceStyleCatalog;
        SerializedProperty _actionCatalog;
        SerializedProperty _curveCatalog;
        SerializedProperty _playbackCatalog;
        SerializedProperty _tagExpansionSheets;

        void OnEnable()
        {
            CacheProperties();
        }

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();
            serializedObject.Update();
            TextStudioProjectSettingsEditorUtility.ActivateDeterministic(false);

            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-project-settings-root");
            ConstrainToViewport(root);
            root.AddToClassList("ts-project-settings-root");

            root.Add(CreateHeaderSection());
            root.Add(CreateLifecycleSection());
            root.Add(CreateAccessibilitySection());

            VisualElement defaultsFields;
            root.Add(CreateDefaultsSection(out defaultsFields));
            root.Add(CreateCatalogSection());

            RefreshDefaultsVisibility(defaultsFields);
            RefreshActiveStatus(root);

            root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                serializedObject.ApplyModifiedProperties();
                TextStudioProjectSettingsEditorUtility.RefreshProjectCatalog();
                RefreshDefaultsVisibility(defaultsFields);
                RefreshActiveStatus(root);
            });

            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (root.panel == null)
                    return;

                RefreshActiveStatus(root);
            }, 500);

            return root;
        }

        void CacheProperties()
        {
            _overrideDefaults = serializedObject.FindProperty("overrideDefaults");
            _defaultRevealMode = serializedObject.FindProperty("defaultRevealMode");
            _defaultPlacementMode = serializedObject.FindProperty("defaultPlacementMode");
            _defaultCharDelay = serializedObject.FindProperty("defaultCharDelay");
            _warnOnMissingTags = serializedObject.FindProperty("warnOnMissingTags");
            _motionPolicy = serializedObject.FindProperty("motionPolicy");
            _markupSyntax = serializedObject.FindProperty("markupSyntax");
            _effectCatalog = serializedObject.FindProperty("effectCatalog");
            _appearanceStyleCatalog = serializedObject.FindProperty("appearanceStyleCatalog");
            _actionCatalog = serializedObject.FindProperty("actionCatalog");
            _curveCatalog = serializedObject.FindProperty("curveCatalog");
            _playbackCatalog = serializedObject.FindProperty("playbackCatalog");
            _tagExpansionSheets = serializedObject.FindProperty("tagExpansionSheets");
        }

        VisualElement CreateHeaderSection()
        {
            VisualElement section = CreateSection("Text Studio Project Settings");
            section.name = "ts-project-settings-header";
            section.Add(CreateSectionIntro("Choose the global defaults and reusable tag assets for this project."));

            HelpBox activeStatus = CreateHelpBox(string.Empty, HelpBoxMessageType.Info);
            activeStatus.name = "ts-project-active-status";
            section.Add(activeStatus);

            HelpBox duplicateWarning = CreateHelpBox(string.Empty, HelpBoxMessageType.Warning);
            duplicateWarning.name = "ts-project-duplicate-warning";
            section.Add(duplicateWarning);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("ts-button-row");
            Button pingActive = new Button(() =>
            {
                TextStudioProjectSettings active = TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
                if (active == null)
                    return;

                Selection.activeObject = active;
                EditorGUIUtility.PingObject(active);
            })
            {
                text = "Ping Active",
                tooltip = "Select the Text Studio Project Settings asset currently used by Text Studio."
            };
            actions.Add(pingActive);

            Button moveToCanonical = new Button(() =>
            {
                TextStudioProjectSettings inspected = target as TextStudioProjectSettings;
                if (!TextStudioProjectSettingsEditorUtility.TryMoveToCanonical(
                        inspected,
                        out string error))
                {
                    EditorUtility.DisplayDialog(
                        "Text Studio Project Settings",
                        error,
                        "OK");
                    return;
                }

                TextStudioProjectSettings moved =
                    TextStudioProjectSettingsEditorUtility.FindCanonical();
                Selection.activeObject = moved;
                EditorGUIUtility.PingObject(moved);
            })
            {
                name = "ts-project-move-canonical",
                text = "Move to Runtime Location",
                tooltip = "Move this settings asset to Text Studio's canonical Resources path so players can load it before the first scene."
            };
            actions.Add(moveToCanonical);
            section.Add(actions);
            return section;
        }

        VisualElement CreateLifecycleSection()
        {
            VisualElement section = CreateSection("Lifecycle");
            section.name = "ts-project-settings-lifecycle-section";
            VisualElement lifecycle = new VisualElement
            {
                name = "ts-project-settings-lifecycle"
            };
            ConstrainToViewport(lifecycle);
            lifecycle.AddToClassList("ts-project-guidance-list");
            lifecycle.Add(CreateGuidanceItem(
                "Project asset",
                "Text Studio loads one optional Project Settings asset from " +
                TextStudioProjectSettings.CanonicalAssetPath + "."));
            lifecycle.Add(CreateGuidanceItem(
                "Defaults",
                "Override Defaults applies to newly created or reset Text Studio Text components."));
            lifecycle.Add(CreateGuidanceItem(
                "Catalog scope",
                "Catalog entries are global. Keep per-element choices on components, Shared Settings, or Profiles.",
                true));
            section.Add(lifecycle);
            return section;
        }

        VisualElement CreateDefaultsSection(out VisualElement defaultsFields)
        {
            VisualElement section = CreateSection("Core Defaults");
            section.name = "ts-project-defaults";
            section.Add(CreateBoundPropertyField(_overrideDefaults, "Override Defaults", "ts-project-override-defaults"));
            section.Add(CreateFieldHint(
                "Keep this off to use Text Studio's built-in defaults. Turn it on to apply the values below whenever a Text Studio Text component is created or reset."));

            defaultsFields = new VisualElement
            {
                name = "ts-project-defaults-fields"
            };
            defaultsFields.AddToClassList("ts-property-group");
            defaultsFields.Add(CreateBoundPropertyField(_defaultRevealMode, "Reveal Mode", "ts-project-default-reveal-mode"));
            defaultsFields.Add(CreateBoundPropertyField(_defaultPlacementMode, "Placement Mode", "ts-project-default-placement-mode"));
            defaultsFields.Add(CreateBoundPropertyField(_defaultCharDelay, "Character Delay", "ts-project-default-char-delay"));
            defaultsFields.Add(CreateBoundPropertyField(_warnOnMissingTags, "Missing Tag Warnings", "ts-project-warn-on-missing-tags"));
            section.Add(defaultsFields);
            section.Add(CreateMarkupSyntaxField());
            section.Add(CreateFieldHint(
                "Leave this unassigned for Compatibility: Text Studio, supported Text Animator braces, safe TMPEffects syntax, and standard TMP formatting. Assign a syntax asset to isolate one provider or use custom delimiters."));
            return section;
        }

        VisualElement CreateAccessibilitySection()
        {
            VisualElement section = CreateSection("Motion & Accessibility");
            section.name = "ts-project-accessibility";
            section.Add(CreateSectionIntro(
                "Set one project-wide motion policy. Individual Text Studio Text components can inherit it or explicitly override it."));
            PropertyField field = CreateBoundPropertyField(
                _motionPolicy,
                "Motion Policy",
                "ts-project-motion-policy");
            field.tooltip = "Full preserves authored animation. Reduced limits displacement and deformation while keeping readable emphasis. None removes motion channels.";
            section.Add(field);
            section.Add(CreateFieldHint(
                "Reduced Motion preserves color, alpha, and compatible material emphasis so important feedback remains visible without strong movement."));
            return section;
        }

        VisualElement CreateMarkupSyntaxField()
        {
            PropertyField field = CreateBoundPropertyField(
                _markupSyntax,
                "Markup Syntax",
                "ts-project-markup-syntax");
            Button create = TextStudioInspectorElements.CreateCompactCreateButton(
                CreateMarkupSyntaxAsset,
                "ts-project-create-markup-syntax",
                "Create and assign a new Text Studio Markup Syntax asset.");
            VisualElement row = TextStudioInspectorElements.CreateAssetReferenceRow(
                field,
                create,
                "ts-project-markup-syntax-row");
            row.TrackPropertyValue(
                _markupSyntax,
                _ => TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                    create,
                    _markupSyntax.objectReferenceValue == null));
            TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                create,
                _markupSyntax.objectReferenceValue == null);
            return row;
        }

        void CreateMarkupSyntaxAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Markup Syntax",
                "Text Studio Markup Syntax",
                "asset",
                "Choose where to save the Text Studio markup syntax asset.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioMarkupSyntax syntax = ScriptableObject.CreateInstance<TextStudioMarkupSyntax>();
            AssetDatabase.CreateAsset(syntax, path);
            AssetDatabase.SaveAssets();
            serializedObject.Update();
            _markupSyntax.objectReferenceValue = syntax;
            serializedObject.ApplyModifiedProperties();
            TextStudioProjectSettingsEditorUtility.RefreshProjectCatalog();
        }

        VisualElement CreateCatalogSection()
        {
            VisualElement section = CreateSection("Catalog Assets");
            section.name = "ts-project-catalogs";
            section.Add(CreateSectionIntro(
                "Register reusable assets here to make their names available globally in markup. Use Profiles, Shared Settings, or inline fields for per-element behavior."));
            section.Add(CreateCatalogField(_appearanceStyleCatalog, "Appearance Styles", "ts-project-appearance-catalog"));
            section.Add(CreateFieldHint("Appearance Tag IDs stay stable when assets are renamed. Invoke a registered style with <$warning>text</$warning>."));
            HelpBox styleCatalogError = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            void RefreshStyleCatalogError()
            {
                string error = TextStudioStyleCatalog.FindError(((TextStudioProjectSettings)target).appearanceStyleCatalog, style => style.Id, "2D style catalog");
                styleCatalogError.text = error ?? string.Empty;
                styleCatalogError.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            section.Add(styleCatalogError);
            section.TrackSerializedObjectValue(serializedObject, _ => RefreshStyleCatalogError());
            RefreshStyleCatalogError();
            section.Add(CreateCatalogField(_effectCatalog, "Effect Catalog", "ts-project-effect-catalog"));
            section.Add(CreateCatalogField(_actionCatalog, "Action Catalog", "ts-project-action-catalog"));
            section.Add(CreateCatalogField(_curveCatalog, "Curve Catalog", "ts-project-curve-catalog"));
            PropertyField playbackCatalog = CreateCatalogField(
                _playbackCatalog,
                "Playback Catalog",
                "ts-project-playback-catalog");
            playbackCatalog.tooltip = "Registered Playback asset names become valid inline effect modifiers and playback= values.";
            section.Add(playbackCatalog);
            section.Add(CreateFieldHint(
                "Playback asset names become reusable markup, for example <bounce impactPop> or playback=impactPop."));
            section.Add(CreateCatalogField(_tagExpansionSheets, "Tag Expansion Sheets", "ts-project-tag-expansion-sheets"));
            return section;
        }

        static PropertyField CreateCatalogField(SerializedProperty property, string label, string name)
        {
            if (property != null)
                property.isExpanded = true;
            return CreateBoundPropertyField(property, label, name);
        }

        static PropertyField CreateBoundPropertyField(SerializedProperty property, string label, string name)
        {
            PropertyField field = new PropertyField(property, label)
            {
                name = name
            };
            field.AddToClassList("ts-property-field");
            return field;
        }

        static VisualElement CreateSection(string title)
        {
            VisualElement section = new VisualElement();
            ConstrainToViewport(section);
            section.AddToClassList("ts-section");
            Label label = new Label(title);
            ConstrainToViewport(label);
            label.AddToClassList("ts-section-title");
            section.Add(label);
            return section;
        }

        static HelpBox CreateHelpBox(string message, HelpBoxMessageType type)
        {
            return TextStudioInspectorElements.CreateHelpBox(message, type);
        }

        static Label CreateSectionIntro(string message)
        {
            Label label = new Label(message);
            ConstrainToViewport(label);
            label.AddToClassList("ts-muted-text");
            label.AddToClassList("ts-project-section-intro");
            return label;
        }

        static VisualElement CreateGuidanceItem(string title, string message, bool isLast = false)
        {
            VisualElement item = new VisualElement();
            ConstrainToViewport(item);
            item.AddToClassList("ts-project-guidance-item");
            if (isLast)
                item.AddToClassList("ts-project-guidance-item--last");

            Label titleLabel = new Label(title);
            ConstrainToViewport(titleLabel);
            titleLabel.AddToClassList("ts-project-guidance-title");
            item.Add(titleLabel);

            Label messageLabel = new Label(message);
            ConstrainToViewport(messageLabel);
            messageLabel.AddToClassList("ts-muted-text");
            messageLabel.AddToClassList("ts-project-guidance-text");
            item.Add(messageLabel);
            return item;
        }

        static VisualElement CreateFieldHint(string message)
        {
            Label label = new Label(message);
            ConstrainToViewport(label);
            label.AddToClassList("ts-muted-text");
            label.AddToClassList("ts-form-hint-text");
            label.AddToClassList("ts-project-field-hint-text");

            VisualElement row = new VisualElement();
            ConstrainToViewport(row);
            row.AddToClassList("ts-form-hint-row");
            row.AddToClassList("ts-project-field-hint-row");
            row.Add(label);
            return row;
        }

        static void ConstrainToViewport(VisualElement element)
        {
            element.style.width = Length.Percent(100f);
            element.style.maxWidth = Length.Percent(100f);
            element.style.minWidth = 0f;
            element.style.flexGrow = 1f;
            element.style.flexShrink = 1f;
            element.style.whiteSpace = WhiteSpace.Normal;
        }

        void RefreshDefaultsVisibility(VisualElement defaultsFields)
        {
            if (defaultsFields == null || _overrideDefaults == null)
                return;

            _overrideDefaults.serializedObject.UpdateIfRequiredOrScript();
            defaultsFields.style.display = _overrideDefaults.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void RefreshActiveStatus(VisualElement root)
        {
            HelpBox activeStatus = root.Q<HelpBox>("ts-project-active-status");
            HelpBox duplicateWarning = root.Q<HelpBox>("ts-project-duplicate-warning");
            List<TextStudioProjectSettings> settings = TextStudioProjectSettingsEditorUtility.FindAll();
            TextStudioProjectSettings active =
                TextStudioProjectSettingsEditorUtility.FindCanonical();
            TextStudioProjectSettings inspected = target as TextStudioProjectSettings;
            string inspectedPath =
                TextStudioProjectSettingsEditorUtility.GetAssetPath(inspected);
            Button moveToCanonical = root.Q<Button>("ts-project-move-canonical");

            if (activeStatus != null)
            {
                if (active == null && inspected != null)
                {
                    activeStatus.text =
                        "This asset is not runtime-loadable. Move it to " +
                        TextStudioProjectSettings.CanonicalAssetPath + ".";
                }
                else if (active == null)
                {
                    activeStatus.text =
                        "No Text Studio Project Settings asset is active. Text Studio will use its built-in defaults.";
                }
                else if (active == inspected)
                {
                    activeStatus.text =
                        "This asset is active in the editor and players through Resources key '" +
                        TextStudioProjectSettings.ResourcePath + "'.";
                }
                else
                {
                    activeStatus.text =
                        "Another project settings asset is active at " +
                        TextStudioProjectSettings.CanonicalAssetPath + ".";
                }
            }

            if (moveToCanonical != null)
            {
                bool isCanonical = string.Equals(
                    inspectedPath,
                    TextStudioProjectSettings.CanonicalAssetPath,
                    System.StringComparison.OrdinalIgnoreCase);
                moveToCanonical.style.display =
                    inspected != null && !isCanonical && active == null
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            if (duplicateWarning == null)
                return;

            bool hasInvalidSettings =
                settings.Count > 1 ||
                (settings.Count == 1 && active == null);
            string duplicateCatalogError =
                inspected != null
                    ? TextStudioRuntimeLoadingBuildValidator
                        .FindDuplicateCatalogEntry(inspected)
                    : string.Empty;
            duplicateWarning.style.display =
                hasInvalidSettings ||
                !string.IsNullOrEmpty(duplicateCatalogError)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (settings.Count > 1)
            {
                duplicateWarning.text =
                    "Multiple Text Studio Project Settings assets exist. Keep one and move it to " +
                    TextStudioProjectSettings.CanonicalAssetPath +
                    "; player builds reject ambiguous settings.";
            }
            else if (hasInvalidSettings)
            {
                duplicateWarning.text =
                    "Project Settings outside the canonical Resources path are ignored by players.";
            }
            else if (!string.IsNullOrEmpty(duplicateCatalogError))
            {
                duplicateWarning.text = duplicateCatalogError;
            }
        }
    }
}
