using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioCompatibilityMigrationWindow : EditorWindow
    {
        enum SourceProvider
        {
            TextAnimator,
            TMPEffects
        }

        EnumField _provider;
        TextField _source;
        TextField _converted;
        Label _status;
        VisualElement _diagnostics;
        VisualElement _projectMigrationSection;
        Label _selectionStatus;
        Button _apply;
        bool _projectMigrationAvailable;

        [MenuItem(TextStudioMenuPaths.Migration, false, TextStudioMenuPaths.MigrationPriority)]
        internal static void ShowWindow()
        {
            TextStudioCompatibilityMigrationWindow window =
                GetWindow<TextStudioCompatibilityMigrationWindow>();
            window.titleContent = new GUIContent("Text Studio Migration");
            window.minSize = new Vector2(540f, 560f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-compatibility-migration-window",
                LoadLocalStyleSheet(),
                "ts-compatibility-migration-window");
            root.style.flexGrow = 1f;
            CommonUITK.ApplyWindowLayout(root);
            rootVisualElement.Add(root);

            ScrollView content = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-compatibility-migration-content"
            };
            content.AddToClassList("ts-compatibility-migration-content");
            content.style.flexGrow = 1f;
            root.Add(content);

            Label introduction = TextStudioInspectorElements.CreateMutedText(
                "Convert one text value at a time. Choose the source syntax, paste or load its markup, review the Text Studio result, then copy it or apply it to a selected Text Studio component.",
                "ts-migration-introduction");
            introduction.AddToClassList("ts-migration-introduction");
            content.Add(introduction);

            _provider = new EnumField("Source Provider", SourceProvider.TMPEffects)
            {
                name = "ts-migration-provider"
            };
            ConfigureAlignedField<Enum>(_provider);
            _provider.tooltip =
                "Choose the provider whose syntax owns the source. This avoids ambiguous tag-name guesses during migration.";

            _source = new TextField("Source Markup")
            {
                name = "ts-migration-source",
                multiline = true,
                value = "<!wait=0.25><jump amp=8>Ready</>"
            };
            TextStudioInspectorElements.ConfigureCodeTextField(_source);
            ConfigureAlignedField<string>(_source);
            _source.AddToClassList("ts-migration-markup-field");
            _source.tooltip =
                "Paste provider markup here, or load it from the selected Text Studio component or TextAsset.";

            Button load = TextStudioInspectorElements.CreateButton(
                "Load Markup from Selection",
                "Load markup from the selected Text Studio component or TextAsset.",
                LoadSelection);
            load.name = "ts-migration-load-selection";
            VisualElement sourceActions = TextStudioInspectorElements.CreateButtonRow(load);
            sourceActions.AddToClassList("ts-migration-section-actions");
            content.Add(TextStudioInspectorElements.CreateSection(
                "1. Choose Source",
                "ts-migration-source-section",
                TextStudioInspectorElements.CreateMutedText(
                    "Select the tool that authored the syntax. Paste markup below, or select a Text Studio component or TextAsset and load it.",
                    "ts-migration-source-guidance"),
                _provider,
                _source,
                sourceActions));

            Button projectMigration = TextStudioInspectorElements.CreateButton(
                "Open Guided Text Animator Migration...",
                "Open the guided migration: see what already works, run one-click conversions for texts, sounds, events, typing speed, playbacks and styles, and get instructions for the rest.",
                TextAnimatorMigrationWindow.ShowWindow,
                primary: true);
            projectMigration.name = "ts-migration-open-project-wizard";
            projectMigration.AddToClassList("ts-migration-primary-action");
            _projectMigrationSection = TextStudioInspectorElements.CreateSection(
                "Migrating a Text Animator Project?",
                "ts-migration-project-wizard",
                TextStudioInspectorElements.CreateMutedText(
                    "The converter above handles one text value. For an existing project, the guided migration checks all Text Animator content, converts what it can in one click without touching Text Animator, and explains what needs you.",
                    "ts-migration-project-wizard-guidance"),
                projectMigration);
            _projectMigrationSection.AddToClassList("ts-migration-project-wizard");
            content.Add(_projectMigrationSection);

            _converted = new TextField("Text Studio Markup")
            {
                name = "ts-migration-converted",
                multiline = true,
                isReadOnly = true
            };
            TextStudioInspectorElements.ConfigureCodeTextField(_converted);
            ConfigureAlignedField<string>(_converted);
            _converted.AddToClassList("ts-migration-markup-field");
            _converted.tooltip =
                "Dependency-free Text Studio markup produced by the same translator used at runtime.";
            _status = TextStudioInspectorElements.CreateStatus(
                string.Empty,
                CommonInspectorStatusType.Info,
                "ts-migration-status");
            _diagnostics = new VisualElement
            {
                name = "ts-migration-diagnostics"
            };
            _diagnostics.AddToClassList("ts-source-diagnostics");
            Button copy = TextStudioInspectorElements.CreateButton(
                "Copy Text Studio Markup",
                "Copy the reviewed Text Studio markup to the clipboard.",
                CopyConverted);
            copy.name = "ts-migration-copy";
            VisualElement reviewActions = TextStudioInspectorElements.CreateButtonRow(copy);
            reviewActions.AddToClassList("ts-migration-section-actions");
            content.Add(TextStudioInspectorElements.CreateSection(
                "2. Review Conversion",
                "ts-migration-review",
                TextStudioInspectorElements.CreateMutedText(
                    "Compare the generated syntax before using it. Unsupported content stays visible so it can be corrected manually.",
                    "ts-migration-review-guidance"),
                _converted,
                _status,
                _diagnostics,
                reviewActions));

            _selectionStatus = TextStudioInspectorElements.CreateMutedText(
                string.Empty,
                "ts-migration-selection-status");
            _apply = TextStudioInspectorElements.CreateButton(
                "Apply to Selected Text Studio Component",
                "Apply the reviewed result to the selected Text Studio component with Undo support.",
                ApplyToSelectedTextStudio,
                primary: true);
            _apply.name = "ts-migration-apply";
            _apply.AddToClassList("ts-migration-primary-action");
            content.Add(TextStudioInspectorElements.CreateSection(
                "3. Use the Result",
                "ts-migration-apply-section",
                _selectionStatus,
                _apply));

            _projectMigrationAvailable = TextAnimatorMigrationAvailability.IsInstalled;
            _provider.RegisterValueChangedCallback(_ => Refresh());
            _source.RegisterValueChangedCallback(_ => Refresh());
            TextStudioInspectorElements.SchedulePeriodicRefresh(root, RefreshSelectionState, 250);
            Refresh();
        }

        void Refresh()
        {
            if (_provider == null || _source == null)
                return;

            SourceProvider provider = (SourceProvider)_provider.value;
            _projectMigrationSection.style.display =
                provider == SourceProvider.TextAnimator && _projectMigrationAvailable
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            TextStudioAuthoringProfile profile = provider == SourceProvider.TextAnimator
                ? TextStudioAuthoringProfile.TextAnimator
                : TextStudioAuthoringProfile.TMPEffects;
            TextStudioCompatibilityTranslator translator = new TextStudioCompatibilityTranslator();
            string converted = translator.Translate(_source.value, profile);
            _converted.SetValueWithoutNotify(converted);

            int count = translator.TranslatedTagCount;
            int warningCount = 0;
            IReadOnlyList<TextStudioCompatibilityDiagnostic> items = translator.Diagnostics;
            _diagnostics.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioCompatibilityDiagnostic item = items[i];
                if (item.Severity == TextStudioCompatibilityDiagnosticSeverity.Warning)
                    warningCount++;

                Label message = TextStudioInspectorElements.CreateMutedText(
                    FormatSourceLocation(item) + item.Message);
                message.AddToClassList(
                    item.Severity == TextStudioCompatibilityDiagnosticSeverity.Warning
                        ? "ts-diagnostic-warning"
                        : "ts-diagnostic-info");
                _diagnostics.Add(message);
            }

            string tagSummary = count == 1 ? "1 tag translated." : count + " tags translated.";
            string warningSummary = warningCount == 0
                ? " No warnings."
                : warningCount == 1 ? " 1 item needs review." : " " + warningCount + " items need review.";
            TextStudioInspectorElements.SetStatus(
                _status,
                tagSummary + warningSummary,
                warningCount > 0
                    ? CommonInspectorStatusType.Warning
                    : count > 0
                        ? CommonInspectorStatusType.Success
                        : CommonInspectorStatusType.Info);
            RefreshSelectionState();
        }

        void LoadSelection()
        {
            TextStudioText hub = GetSelectedTextStudio();
            if (hub != null)
            {
                _source.value = hub.Markup;
                return;
            }

            TextAsset textAsset = Selection.activeObject as TextAsset;
            if (textAsset != null)
            {
                _source.value = textAsset.text;
                return;
            }

            ShowNotification(new GUIContent(
                "Select a GameObject with a Text Studio component or a TextAsset first."));
        }

        void CopyConverted()
        {
            EditorGUIUtility.systemCopyBuffer = _converted.value ?? string.Empty;
            ShowNotification(new GUIContent("Text Studio markup copied."));
        }

        void ApplyToSelectedTextStudio()
        {
            TextStudioText hub = GetSelectedTextStudio();
            if (hub == null)
            {
                ShowNotification(new GUIContent(
                    "Select a Text Studio component before applying."));
                return;
            }

            Undo.RecordObject(hub, "Apply Converted Text Studio Markup");
            hub.SetText(_converted.value);
            EditorUtility.SetDirty(hub);
            ShowNotification(new GUIContent("Converted markup applied to " + hub.gameObject.name + "."));
        }

        void RefreshSelectionState()
        {
            if (_apply == null || _selectionStatus == null)
                return;

            TextStudioText selectedText = GetSelectedTextStudio();
            bool canApply = selectedText != null;
            _apply.SetEnabled(canApply);
            _selectionStatus.text = canApply
                ? "Ready to apply to \"" + selectedText.gameObject.name + "\". The change supports Undo."
                : "Select a GameObject with a Text Studio component to apply directly. You can still copy the converted markup above.";
        }

        static void ConfigureAlignedField<T>(BaseField<T> field)
        {
            field.AddToClassList(BaseField<T>.alignedFieldUssClassName);
            field.AddToClassList("ts-property-field");
        }

        static StyleSheet LoadLocalStyleSheet()
        {
            return TextStudioInspectorElements.LoadStyleSheetFromAnchor(
                "TextStudioCompatibilityMigrationWindow",
                "Editor/Migration/TextStudioCompatibilityMigrationWindow.cs",
                "Editor/Migration/TextStudioCompatibilityMigrationWindow.uss");
        }

        static TextStudioText GetSelectedTextStudio()
        {
            GameObject gameObject = Selection.activeGameObject;
            return gameObject != null ? gameObject.GetComponent<TextStudioText>() : null;
        }

        static string FormatSourceLocation(TextStudioCompatibilityDiagnostic diagnostic)
        {
            return diagnostic.SourceStart >= 0
                ? "Character " + diagnostic.SourceStart + ": "
                : string.Empty;
        }
    }
}
