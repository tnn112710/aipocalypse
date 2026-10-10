using System;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioEffectCodeWindow : EditorWindow
    {
        const string LastFolderPreference = "TextStudio.EffectCode.LastFolder";
        const string DefaultFolder = "Assets/Text Studio/Shared Effects";

        TextField _code;
        TextField _tag;
        Label _tagHint;
        TextField _folder;
        Label _previewTitle;
        Label _previewDetails;
        HelpBox _status;
        Button _import;
        TextStudioEffectCodePackage _package;

        internal static void ShowWindow()
        {
            TextStudioEffectCodeWindow window = GetWindow<TextStudioEffectCodeWindow>(true, "Import Effect Code", true);
            window.minSize = new Vector2(480f, 500f);
            window.Show();
        }

        internal static void Copy(TextStudioEffectDefinitionAsset effect)
        {
            if (!TryCopy(
                    effect,
                    out string _,
                    out string error))
            {
                EditorUtility.DisplayDialog("Copy Effect Code", error, "OK");
                return;
            }

            EditorWindow notificationWindow = focusedWindow;
            if (notificationWindow != null)
                notificationWindow.ShowNotification(new GUIContent("Effect Code copied"), 2.5);
            else
                Debug.Log("[TextStudio] Effect Code copied to the clipboard.");
        }

        internal static bool TryCopy(
            TextStudioEffectDefinitionAsset effect,
            out string copiedText,
            out string error)
        {
            copiedText = string.Empty;
            if (!TextStudioEffectCodeExporter.TryCreatePackage(
                    effect,
                    out TextStudioEffectCodePackage package,
                    out error))
            {
                return false;
            }

            string token = TextStudioEffectCodeFormat.Encode(package, out _);
            copiedText = TextStudioEffectCodeExporter.CreateClipboardText(
                package,
                token);
            EditorGUIUtility.systemCopyBuffer = copiedText;
            return true;
        }

        public void CreateGUI()
        {
            VisualElement host = rootVisualElement;
            host.Clear();
            host.RemoveFromClassList("ts-inspector-root");
            host.RemoveFromClassList("ts-effect-code-window");

            VisualElement root = TextStudioInspectorElements.CreateRoot("ts-effect-code-window-root");
            root.AddToClassList("ts-effect-code-window");
            host.Add(root);

            Label eyebrow = new Label("TEXT STUDIO");
            eyebrow.AddToClassList("ts-muted-text");
            eyebrow.AddToClassList("ts-effect-code-eyebrow");
            root.Add(eyebrow);
            Label title = new Label("Import Effect Code");
            title.AddToClassList("ts-section-title");
            title.AddToClassList("ts-effect-code-title");
            root.Add(title);
            Label explanation = new Label(
                "Paste the whole forum snippet or only its TSFX code. Text Studio validates everything before creating an asset.");
            explanation.AddToClassList("ts-muted-text");
            explanation.AddToClassList("ts-effect-code-copy");
            root.Add(explanation);

            _code = new TextField("Effect Code")
            {
                multiline = true,
                value = GetClipboardCode()
            };
            _code.name = "ts-effect-code-input";
            _code.tooltip = "Paste a complete shared snippet or a TSFX code. Long codes wrap inside the field.";
            _code.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            _code.AddToClassList("ts-source-field-wrap");
            _code.AddToClassList("ts-effect-code-input");
            TextStudioInspectorElements.ConfigureCodeTextField(_code);
#if UNITY_6000_0_OR_NEWER
            _code.verticalScrollerVisibility = ScrollerVisibility.Auto;
#endif
            _code.RegisterValueChangedCallback(_ => RefreshPreview(true));
            root.Add(_code);

            _tag = new TextField("Imported Tag");
            _tag.name = "ts-effect-code-tag";
            _tag.tooltip = "The tag used to invoke the imported effect in markup. Enter the name without angle brackets.";
            _tag.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            _tag.RegisterValueChangedCallback(_ => RefreshPreview(false));
            root.Add(_tag);
            _tagHint = TextStudioInspectorElements.CreateMutedText(
                "Paste a valid Effect Code to see and optionally rename its tag.",
                "ts-effect-code-tag-hint");
            _tagHint.AddToClassList("ts-effect-code-field-hint");
            root.Add(_tagHint);

            VisualElement destinationRow = new VisualElement();
            destinationRow.AddToClassList("ts-effect-code-destination-row");
            destinationRow.style.flexDirection = FlexDirection.Row;
            destinationRow.style.alignItems = Align.Center;
            _folder = new TextField("Destination")
            {
                value = EditorPrefs.GetString(LastFolderPreference, DefaultFolder)
            };
            _folder.tooltip = "Project folder where the imported effect asset will be created.";
            _folder.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            _folder.style.flexGrow = 1f;
            _folder.style.minWidth = 0f;
            destinationRow.Add(_folder);
            Button browse = new Button(BrowseFolder)
            {
                text = "…",
                tooltip = "Choose a destination inside Assets."
            };
            browse.AddToClassList("ts-effect-code-browse");
            destinationRow.Add(browse);
            root.Add(destinationRow);

            VisualElement preview = new VisualElement();
            preview.name = "ts-effect-code-preview";
            preview.AddToClassList("ts-effect-code-preview");
            preview.style.flexShrink = 0f;
            _previewTitle = new Label("Waiting for an Effect Code");
            _previewTitle.AddToClassList("ts-tag-inline-help-name");
            _previewTitle.AddToClassList("ts-effect-code-preview-title");
            preview.Add(_previewTitle);
            _previewDetails = new Label();
            _previewDetails.AddToClassList("ts-muted-text");
            _previewDetails.AddToClassList("ts-effect-code-preview-details");
            _previewDetails.style.flexShrink = 0f;
            preview.Add(_previewDetails);
            root.Add(preview);

            _status = TextStudioInspectorElements.CreateHelpBox(
                string.Empty,
                HelpBoxMessageType.Info,
                "ts-effect-code-status");
            _status.AddToClassList("ts-effect-code-status");
            _status.style.flexShrink = 0f;
            _status.style.display = DisplayStyle.None;
            root.Add(_status);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            root.Add(spacer);
            VisualElement actions = CommonUITK.CreateWindowFooter(16f, 14f, "ts-effect-code-actions");
            _import = new Button(Import)
            {
                text = "Import Effect",
                tooltip = "Create and register the validated effect asset."
            };
            _import.name = "ts-effect-code-import";
            _import.AddToClassList("ts-button");
            _import.AddToClassList("ts-primary-button");
            actions.Add(_import);
            root.Add(actions);
            RefreshPreview(true);
        }

        void RefreshPreview(bool synchronizeTag)
        {
            _package = null;
            if (_previewTitle == null || _previewDetails == null || _tag == null || _tagHint == null ||
                _status == null || _import == null)
                return;
            string source = _code?.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                if (synchronizeTag)
                    _tag.SetValueWithoutNotify(string.Empty);
                _previewTitle.text = "Waiting for an Effect Code";
                _previewDetails.text = "Copy an effect in another project, then paste it here.";
                _tagHint.text = "Paste a valid Effect Code to see and optionally rename its tag.";
                SetStatus(string.Empty, HelpBoxMessageType.Info, false);
                _import.SetEnabled(false);
                return;
            }
            if (!TextStudioEffectCodeFormat.TryDecode(source, out TextStudioEffectCodePackage package, out string error) ||
                !TextStudioEffectCodeImporter.TryCreatePreview(package, out TextStudioEffectCodePreview preview, out error))
            {
                _previewTitle.text = "Cannot import this code";
                _previewDetails.text = string.Empty;
                if (synchronizeTag)
                    _tag.SetValueWithoutNotify(string.Empty);
                _tagHint.text = "Fix the Effect Code before choosing an imported tag.";
                SetStatus(error, HelpBoxMessageType.Error, true);
                _import.SetEnabled(false);
                return;
            }
            _package = package;
            if (synchronizeTag)
                _tag.SetValueWithoutNotify(preview.Id);

            if (!TextStudioEffectCodeImporter.TryNormalizeTagOverride(
                    _tag.value,
                    out string tagOverride,
                    out error))
            {
                _previewTitle.text = string.IsNullOrWhiteSpace(preview.Name) ? "Shared Effect" : preview.Name;
                _previewDetails.text = BuildPreviewDetails(preview, preview.Id);
                _tagHint.text = "Enter the tag name without angle brackets.";
                SetStatus(error, HelpBoxMessageType.Error, true);
                _import.SetEnabled(false);
                return;
            }

            string effectiveTag = string.IsNullOrEmpty(tagOverride) ? preview.Id : tagOverride;
            _previewTitle.text = string.IsNullOrWhiteSpace(preview.Name) ? "Shared Effect" : preview.Name;
            _previewDetails.text = BuildPreviewDetails(preview, effectiveTag);
            _tagHint.text = string.IsNullOrEmpty(tagOverride)
                ? "Leave empty to keep <" + preview.Id + ">."
                : "Use <" + effectiveTag + "> in markup. Change it now to rename the imported tag.";
            SetStatus("Ready to import <" + effectiveTag +
                      ">. If that tag already exists, Text Studio adds a numeric suffix.",
                HelpBoxMessageType.Info,
                true);
            _import.SetEnabled(true);
        }

        void Import()
        {
            if (_package == null)
                return;
            if (!TextStudioEffectCodeImporter.TryImport(
                    _package,
                    _folder.value,
                    _tag.value,
                    out TextStudioEffectDefinitionAsset imported,
                    out string error))
            {
                SetStatus(error, HelpBoxMessageType.Error, true);
                return;
            }
            EditorPrefs.SetString(LastFolderPreference, _folder.value);
            ShowNotification(new GUIContent("Imported " + imported.name), 2.5);
            Close();
        }

        void BrowseFolder()
        {
            string current = _folder?.value ?? DefaultFolder;
            string absolute = current.StartsWith("Assets", StringComparison.Ordinal)
                ? Application.dataPath + current.Substring("Assets".Length)
                : Application.dataPath;
            string selected = EditorUtility.OpenFolderPanel("Effect Code Destination", absolute, string.Empty);
            if (string.IsNullOrEmpty(selected))
                return;
            string dataPath = Application.dataPath.Replace('\\', '/');
            string normalized = selected.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.Ordinal))
            {
                SetStatus("Choose a folder inside this project's Assets folder.", HelpBoxMessageType.Warning, true);
                return;
            }
            _folder.value = "Assets" + normalized.Substring(dataPath.Length);
        }

        void SetStatus(string message, HelpBoxMessageType type, bool visible)
        {
            _status.text = message;
            TextStudioInspectorElements.SetHelpBoxMessageType(_status, type);
            _status.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string BuildPreviewDetails(TextStudioEffectCodePreview preview, string tag)
        {
            string tagLine = string.IsNullOrWhiteSpace(tag) ? string.Empty : "<" + tag + ">\n";
            return tagLine + preview.Summary + "\nCreated with Text Studio " + preview.WriterVersion;
        }

        static string GetClipboardCode()
        {
            string clipboard = EditorGUIUtility.systemCopyBuffer ?? string.Empty;
            return clipboard.IndexOf(TextStudioEffectCodeFormat.RawPrefix, StringComparison.Ordinal) >= 0 ||
                   clipboard.IndexOf(TextStudioEffectCodeFormat.BrotliPrefix, StringComparison.Ordinal) >= 0
                ? clipboard
                : string.Empty;
        }
    }
}
