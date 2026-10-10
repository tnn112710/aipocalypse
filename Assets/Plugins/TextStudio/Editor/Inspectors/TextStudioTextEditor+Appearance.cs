using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        UnityEditor.Editor _inlineWritingEditor;
        VisualElement _inlineWritingRoot;

        void ReleaseInlineWritingEditor()
        {
            _inlineWritingRoot?.Unbind();
            _inlineWritingRoot = null;
            if (_inlineWritingEditor != null)
                UnityEngine.Object.DestroyImmediate(_inlineWritingEditor);
            _inlineWritingEditor = null;
        }

        VisualElement BuildUitkAppearanceTab(TextStudioText hub)
        {
            _inlineAppearanceEditor?.Dispose();
            _inlineAppearanceEditor = null;
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-appearance-tab");
            VisualElement styleSection = CreateSection("2D Appearance");
            styleSection.Add(BuildAppearanceSourceField(hub));
            root.Add(styleSection);

            TextStudioAppearanceSource source = hub.AppearanceSource;
            string choice = TextStudio2DStyleSelection.Choice(hub);
            bool mixed = HasMixedAppearanceSources(hub);
            Action refreshConversion = null;
            VisualElement renderingSection = null;
            VisualElement saveActions = null;
            if (mixed)
            {
                styleSection.Add(CreateAlignedHint(
                    "The selected objects use different appearance sources. Choose a source to edit them together."));
            }
            else if (choice == "Catalog")
            {
                AddCatalogAppearanceControls(styleSection);
            }
            else if (choice == "Custom")
            {
                AddStyleAppearanceControls(styleSection);
            }
            else
            {
                Button save = TextStudioInspectorElements.CreateButton(
                    "Save as Style…",
                    "Save this inline appearance as a reusable asset, then link this object to the saved asset.",
                    CreateAppearanceStyleAsset);
                save.name = "ts-appearance-save-inline-asset";
                save.SetEnabled(!serializedObject.isEditingMultipleObjects);
                if (serializedObject.isEditingMultipleObjects)
                    save.tooltip = "Select one object to save its inline appearance as a style.";
                Button saveAndRegister = TextStudioInspectorElements.CreateButton(
                    "Save & Register…", "Save a reusable style and include it in the catalog and global style tags.",
                    () => CreateAppearanceStyleAsset(true));
                saveAndRegister.name = "ts-appearance-save-register";
                saveAndRegister.SetEnabled(!serializedObject.isEditingMultipleObjects);
                saveActions = TextStudioInspectorElements.CreateButtonRow(save, saveAndRegister);
                _inlineAppearanceEditor = new TextStudio2DStyleEditor(
                    serializedObject, "_inlineAppearanceSettings",
                    () => TextStudio2DStyleCompiler.Compile(hub.InlineAppearanceSettings));
                VisualElement inline = _inlineAppearanceEditor.Build();
                inline.name = "ts-inline-appearance-settings";
                inline.RemoveFromClassList("ts-inspector-root");
                inline.RemoveFromClassList("common-inspector-root");
                root.Add(inline);
                renderingSection = inline.Q<VisualElement>("ts-2d-style-rendering");
                renderingSection?.RemoveFromHierarchy();
            }

            if (mixed || choice != "Catalog")
                styleSection.Add(TextStudioInspectorElements.CreateButtonRow(CreateAppearanceBrowserControl()));
            if (saveActions != null)
                styleSection.Add(saveActions);
            if (!mixed && source == TextStudioAppearanceSource.Inline && hub.InlineAppearanceSettings.IsEmpty)
                styleSection.Add(CreateAlignedHint("No appearance layers. The text keeps its native look; named appearance tags remain available.", "ts-appearance-empty-state"));

            if (mixed || source == TextStudioAppearanceSource.Style)
                refreshConversion = AddAppearanceConversionControls(styleSection, hub);

            HelpBox backendSupport = CreateHelpBox(string.Empty, HelpBoxMessageType.Info);
            backendSupport.name = "ts-appearance-backend-support";
            styleSection.Add(backendSupport);
            VisualElement diagnosticsSection = CreateSection("Appearance Issues");
            diagnosticsSection.name = "ts-appearance-diagnostics";
            diagnosticsSection.style.display = DisplayStyle.None;
            VisualElement diagnostics = new VisualElement { name = "ts-appearance-diagnostic-messages" };
            diagnosticsSection.Add(diagnostics);
            root.Add(diagnosticsSection);
            if (renderingSection != null) root.Add(renderingSection);
            VisualElement preparationSection = BuildFontAppearanceSection(hub);
            root.Add(preparationSection);

            CompiledTextStudio2DStyle lastCompiled = null;
            Action refresh = () =>
            {
                preparationSection.style.display = FindAppearancePreparationTarget() != null ? DisplayStyle.Flex : DisplayStyle.None;
                bool hasAppearance = false;
                foreach (UnityEngine.Object owner in targets)
                {
                    if (owner is TextStudioText text &&
                        (text.AppearanceSource == TextStudioAppearanceSource.Inline || text.AppearanceStyle != null))
                        hasAppearance = true;
                }
                RefreshAppearanceBackendSupport(
                    TextStudioAppearanceBackendSupport.EvaluateEditorTargets(targets), hasAppearance, backendSupport);
                refreshConversion?.Invoke();
                CompiledTextStudio2DStyle compiled = hub.CompiledAppearanceStyle;
                if (ReferenceEquals(compiled, lastCompiled))
                    return;
                lastCompiled = compiled;
                if (_inlineAppearanceEditor != null)
                    _inlineAppearanceEditor.RefreshValidation();
                else
                    RefreshAppearanceIssues(compiled, diagnosticsSection, diagnostics);
            };
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(root, () =>
            {
                if (CanRunScheduledInspectorUpdate(root, hub))
                    refresh();
            }, 250);
            return root;
        }

        static void RefreshAppearanceBackendSupport(
            TextStudioAppearanceBackendSummary summary,
            bool hasAssignedAppearance,
            HelpBox support)
        {
            if (!summary.HasUnsupportedTargets)
            {
                support.style.display = DisplayStyle.None;
                return;
            }

            string owner = string.IsNullOrWhiteSpace(summary.BackendName)
                ? "The active text backends"
                : summary.BackendName;
            string affected = summary.TargetCount > 1
                ? summary.UnsupportedCount + " of " + summary.TargetCount +
                  " selected Text Studio objects use backends that own glyph styling. "
                : owner + " owns glyph styling. ";
            string inactive = hasAssignedAppearance
                ? "The assigned Text Studio 2D Appearance is preserved but inactive on " +
                  (summary.TargetCount > 1 ? "those objects" : "this object") + ". "
                : "Text Studio 2D Appearance can still be assigned, but it remains inactive on " +
                  (summary.TargetCount > 1 ? "those objects" : "this object") + ". ";
            string supported = summary.HasMixedSupport
                ? "It continues to render on the other selected objects. "
                : string.Empty;
            support.text = affected + inactive + supported +
                "It will render after switching to a backend that supports Text Studio 2D Appearance.";
            TextStudioInspectorElements.SetHelpBoxMessageType(
                support,
                hasAssignedAppearance
                    ? HelpBoxMessageType.Warning
                    : HelpBoxMessageType.Info);
            support.style.display = DisplayStyle.Flex;
        }

        VisualElement BuildWritingSection(TextStudioText hub, TextStudioTypewriterModeView modeView)
        {
            VisualElement section = CreateSection("Writing");
            SerializedProperty enabled = serializedObject.FindProperty("_writingEnabled");
            section.Add(CreateBoundPropertyField(enabled, "Enable Writing"));
            Label instantGuidance = CreateAlignedHintLabel(string.Empty, "ts-writing-instant-guidance");
            VisualElement instantGuidanceRow = CreateAlignedHintRow(instantGuidance);
            section.Add(instantGuidanceRow);
            modeView.SetWritingGuidance(instantGuidance, instantGuidanceRow);
            VisualElement controls = new VisualElement();
            section.Add(controls);
            modeView.Add(TextStudioTypewriterModeField.WritingOptions, controls);
            controls.Add(CreateBoundPropertyField(serializedObject.FindProperty("_eraseOnHide"), "Erase on Hide"));
            ObjectField styleField = new ObjectField("Style")
            {
                name = BuildPropertyFieldName(_writingStyle),
                objectType = typeof(TextStudioWritingStyleAsset),
                allowSceneObjects = false,
                bindingPath = _writingStyle.propertyPath,
                tooltip = "Choose how each letter is drawn. Drawing Options edits this shared asset; Typewriter owns timing."
            };
            styleField.AddToClassList("ts-property-field");
            styleField.AddToClassList(UnityAlignedFieldClass);
            Button createButton = TextStudioInspectorElements.CreateCompactCreateButton(
                CreateWritingStyleAsset,
                "ts-writing-style-create-button",
                "Create and assign a reusable writing style.");
            VisualElement styleRow = TextStudioInspectorElements.CreateAssetReferenceRow(
                styleField,
                createButton,
                "ts-writing-style-row");
            controls.Add(styleRow);
            styleRow.TrackPropertyValue(_writingStyle, _ =>
            {
                TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                    createButton,
                    _writingStyle.objectReferenceValue == null);
                RequestUitkActiveTabRebuild(hub);
            });
            TextStudioInspectorElements.SetCompactCreateButtonAvailable(
                createButton,
                _writingStyle.objectReferenceValue == null);

            TextStudioWritingStyleAsset assigned =
                _writingStyle.objectReferenceValue as TextStudioWritingStyleAsset;
            if (assigned == null)
            {
                controls.Add(CreateAlignedHint(
                    "Assign a style to draw or reveal letters over time.",
                    "ts-writing-empty-state"));
                return section;
            }

            controls.Add(CreateAlignedHint("Use Play or Scrub in Preview to see the writing."));
            _inlineWritingEditor = UnityEditor.Editor.CreateEditor(assigned);
            _inlineWritingRoot = ((TextStudioWritingStyleAssetEditor)_inlineWritingEditor).BuildInspector(false);
            _inlineWritingRoot.RemoveFromClassList("ts-inspector-root");
            _inlineWritingRoot.RemoveFromClassList("common-inspector-root");
            Foldout drawing = new Foldout { text = "Drawing Options", value = true, name = "ts-writing-drawing-options" };
            drawing.Add(CreateAlignedHint("Edits the shared Writing Style asset."));
            drawing.Add(_inlineWritingRoot);
            controls.Add(drawing);
            Foldout details = new Foldout {text = "Diagnostics", value = false, name = "ts-writing-details"};
            Label status = AddAppearanceSummaryRow(details, "Status");
            Label mode = AddAppearanceSummaryRow(details, "Effect");
            Label promise = AddAppearanceSummaryRow(details, "Requested Effect");
            Label sources = AddAppearanceSummaryRow(details, "Drawing Sources");
            Label schedule = AddAppearanceSummaryRow(details, "Grouping");
            VisualElement diagnostics = new VisualElement {name = "ts-writing-diagnostic-messages"};
            controls.Add(diagnostics);
            controls.Add(details);

            ulong renderedFingerprint = ulong.MaxValue;
            Action refresh = () =>
            {
                CompiledTextStudioWritingStyle compiled = hub != null
                    ? hub.CompiledWritingStyle
                    : null;
                ulong fingerprint = compiled?.Fingerprint.Complete ?? 0UL;
                if (fingerprint == renderedFingerprint)
                    return;
                renderedFingerprint = fingerprint;
                RefreshWritingSummary(
                    compiled,
                    status,
                    mode,
                    promise,
                    sources,
                    schedule,
                    diagnostics);
            };
            refresh();
            TextStudioInspectorElements.SchedulePeriodicRefresh(section, () =>
            {
                if (CanRunScheduledInspectorUpdate(section, hub))
                    refresh();
            }, 250);
            return section;
        }

        static void RefreshWritingSummary(
            CompiledTextStudioWritingStyle compiled,
            Label status,
            Label mode,
            Label promise,
            Label sources,
            Label schedule,
            VisualElement diagnostics)
        {
            diagnostics.Clear();
            if (compiled == null)
            {
                status.text = "Preparing";
                mode.text = "-";
                promise.text = "-";
                sources.text = "-";
                schedule.text = "-";
                return;
            }

            TextStudioWritingConfiguration configuration = compiled.Configuration;
            status.text = compiled.IsValid ? "Ready" : "Needs attention";
            mode.text = ObjectNames.NicifyVariableName(configuration.Mode.ToString());
            promise.text = FormatWritingDataLevel(
                TextStudioWritingModeUtility.RequestedDataLevel(configuration.Mode));
            int glyphs = compiled.Font?.GlyphCount ?? 0;
            bool phrase = compiled.Phrase != null &&
                          (compiled.Phrase.Centerline?.IsEmpty == false ||
                           compiled.Phrase.Outline?.IsEmpty == false);
            sources.text = phrase
                ? glyphs > 0
                    ? "Exact phrase + " + glyphs + " glyph variation" +
                      (glyphs == 1 ? string.Empty : "s")
                    : "Exact shaped phrase"
                : glyphs > 0
                    ? glyphs + " glyph variation" +
                      (glyphs == 1 ? string.Empty : "s")
                    : "No drawing assets";
            schedule.text = "Typewriter";

            if (compiled.DiagnosticCount == 0)
            {
                return;
            }
            const int visibleDiagnosticLimit = 4;
            int count = Math.Min(compiled.DiagnosticCount, visibleDiagnosticLimit);
            for (int i = 0; i < count; i++)
            {
                TextStudioStackDiagnostic diagnostic = compiled.GetDiagnostic(i);
                diagnostics.Add(CreateHelpBox(
                    diagnostic.Message,
                    ToHelpBoxMessageType(diagnostic.Severity)));
            }
            if (compiled.DiagnosticCount > visibleDiagnosticLimit)
            {
                diagnostics.Add(CreateAlignedHint(
                    (compiled.DiagnosticCount - visibleDiagnosticLimit) +
                    " additional diagnostics are available on the writing asset."));
            }
        }

        static string FormatWritingDataLevel(TextStudioWritingDataLevel level)
        {
            return level switch
            {
                TextStudioWritingDataLevel.AuthoredCenterline => "True Handwriting",
                TextStudioWritingDataLevel.PreparedOutline => "Outline Trace",
                TextStudioWritingDataLevel.InkCoverage => "Ink Reveal",
                TextStudioWritingDataLevel.OrdinaryTypewriter => "Ordinary Typewriter",
                _ => "Disabled"
            };
        }

        static Label AddAppearanceSummaryRow(VisualElement parent, string label)
        {
            VisualElement row = TextStudioInspectorElements.CreateKeyValueRow(label, "-");
            Label value = row.Q<Label>(className: "ts-key-value-value");
            parent.Add(row);
            return value;
        }

        static void RefreshAppearanceIssues(
            CompiledTextStudio2DStyle compiled,
            VisualElement section,
            VisualElement diagnostics)
        {
            diagnostics.Clear();
            if (compiled == null)
            {
                section.style.display = DisplayStyle.None;
                return;
            }

            const int visibleDiagnosticLimit = 5;
            int issueCount = 0;
            for (int i = 0; i < compiled.DiagnosticCount; i++)
            {
                TextStudioStackDiagnostic diagnostic = compiled.GetDiagnostic(i);
                if (diagnostic.Severity == TextStudioStackDiagnosticSeverity.Information)
                    continue;

                issueCount++;
                if (issueCount > visibleDiagnosticLimit)
                    continue;
                diagnostics.Add(CreateHelpBox(
                    diagnostic.Message,
                    ToHelpBoxMessageType(diagnostic.Severity)));
            }
            if (!compiled.IsValid && issueCount == 0)
            {
                issueCount = 1;
                diagnostics.Add(CreateHelpBox(
                    "The appearance could not be prepared. Open its style asset for details.",
                    HelpBoxMessageType.Error));
            }
            if (issueCount > visibleDiagnosticLimit)
            {
                diagnostics.Add(CreateAlignedHint(
                    (issueCount - visibleDiagnosticLimit) +
                    " additional issues are available on the style asset."));
            }
            section.style.display = issueCount > 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        static string FormatCount(int count, string singular, string plural)
        {
            return count + " " + (count == 1 ? singular : plural);
        }

        static HelpBoxMessageType ToHelpBoxMessageType(
            TextStudioStackDiagnosticSeverity severity)
        {
            return severity switch
            {
                TextStudioStackDiagnosticSeverity.Error => HelpBoxMessageType.Error,
                TextStudioStackDiagnosticSeverity.Warning => HelpBoxMessageType.Warning,
                _ => HelpBoxMessageType.Info
            };
        }

        void CreateAppearanceStyleAsset() => CreateAppearanceStyleAsset(false);

        void CreateAppearanceStyleAsset(bool register)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create 2D Appearance Style",
                "Text Studio 2D Style",
                "asset",
                "Choose where to save the reusable Text Studio 2D appearance style.");
            if (string.IsNullOrEmpty(path))
                return;

            SaveAppearanceStyleAsset(path, register);
        }

        internal void SaveAppearanceStyleAsset(string path, bool register = false)
        {
            if (string.IsNullOrEmpty(path)) return;
            serializedObject.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save Inline Appearance as Style");

            TextStudio2DStyleAsset style =
                ScriptableObject.CreateInstance<TextStudio2DStyleAsset>();
            style.name = System.IO.Path.GetFileNameWithoutExtension(path);
            style.ApplySettings(((TextStudioText)target).InlineAppearanceSettings);
            AssetDatabase.CreateAsset(style, path);
            Undo.RegisterCreatedObjectUndo(style, "Create Text Studio 2D Style");
            AssetDatabase.SaveAssetIfDirty(style);
            bool registered = false;
            if (register)
            {
                registered = TextStudioAppearanceCatalogUtility.TryRegister(style,
                    TextStudioAppearanceCatalogUtility.SuggestAvailableId(style.name, style), out string registrationError);
                if (!registered)
                    EditorUtility.DisplayDialog("Style Saved", "The style was saved, but could not be added to the catalog. " + registrationError, "OK");
            }

            ApplyAppearanceChange("Assign Appearance Style", text => TextStudio2DStyleSelection.Assign(text, style,
                registered ? TextStudioStyleSelectionMode.Catalog : TextStudioStyleSelectionMode.Custom));
            Undo.CollapseUndoOperations(undoGroup);
            RequestUitkActiveTabRebuild((TextStudioText)target);
            EditorGUIUtility.PingObject(style);
        }

        void CreateWritingStyleAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Writing Style",
                "Text Studio Writing Style",
                "asset",
                "Choose where to save the reusable Text Studio writing treatment.");
            if (string.IsNullOrEmpty(path))
                return;

            TextStudioWritingStyleAsset style =
                ScriptableObject.CreateInstance<TextStudioWritingStyleAsset>();
            style.SetConfiguration(TextStudioWritingConfiguration.Default);
            AssetDatabase.CreateAsset(style, path);
            Undo.RegisterCreatedObjectUndo(style, "Create Text Studio Writing Style");
            AssetDatabase.SaveAssets();

            _writingStyle.objectReferenceValue = style;
            serializedObject.ApplyModifiedProperties();
            serializedObject.UpdateIfRequiredOrScript();
            EditorGUIUtility.PingObject(style);
        }
    }
}
