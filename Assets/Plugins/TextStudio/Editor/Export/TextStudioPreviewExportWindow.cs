using System;
using System.Collections.Generic;
using System.IO;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioPreviewExportWindow : EditorWindow
    {
        const string LastFolderPreference = "TextStudio.PreviewExport.LastFolder";
        const string LastRendererPreference = "TextStudio.PreviewExport.LastRenderer";

        TextStudioPreviewExportSnapshot _snapshot;
        TextStudioPreviewExportFormat _format = TextStudioPreviewExportJob.DefaultFormat;
        string _rendererId;
        readonly List<TextStudioPreviewExportRendererOption> _rendererOptions =
            new List<TextStudioPreviewExportRendererOption>();
        readonly List<Button> _rendererButtons = new List<Button>();
        Button _mp4Button;
        Button _gifButton;
        Label _details;
        Label _warning;
        bool _ownershipTransferred;
        bool _uiBuilt;

        internal delegate bool TryCreateSnapshot(
            out TextStudioPreviewExportSnapshot snapshot,
            out string error);

        internal static Button CreateMenuButton(
            string name,
            TextStudioEffectDefinitionAsset effect,
            TryCreateSnapshot createSnapshot,
            Action<GenericMenu> extendMenu = null)
        {
            Button button = null;
            button = new Button(() => ShowExportMenu(button, effect, createSnapshot, extendMenu))
            {
                name = name,
                tooltip = "Copy, import, or export this effect."
            };
            button.AddToClassList("ts-preview-export-menu");

            GUIContent menuIconContent = EditorGUIUtility.IconContent("_Menu");
            if (menuIconContent.image != null)
            {
                Image menuIcon = new Image
                {
                    image = menuIconContent.image,
                    pickingMode = PickingMode.Ignore,
                    scaleMode = ScaleMode.ScaleToFit
                };
                menuIcon.AddToClassList("ts-preview-export-menu-icon");
                button.Add(menuIcon);
            }
            else
            {
                button.text = "⋮";
            }

            return button;
        }

        internal static void ShowFor(TextStudioPreviewExportSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            TextStudioPreviewExportWindow window = CreateInstance<TextStudioPreviewExportWindow>();
            window._snapshot = snapshot;
            window.titleContent = new GUIContent("Export Preview Loop");
            Vector2 initialSize = new Vector2(
                460f,
                string.IsNullOrEmpty(snapshot.RendererId) ? 430f : 350f);
            window.minSize = initialSize;
            window.maxSize = new Vector2(640f, 620f);
            window.ShowUtility();
            Rect position = window.position;
            position.size = initialSize;
            window.position = position;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            if (_snapshot == null)
            {
                root.Clear();
                root.schedule.Execute(() =>
                {
                    if (!_uiBuilt && _snapshot != null)
                        CreateGUI();
                });
                return;
            }

            _uiBuilt = true;
            root.Clear();
            root.AddToClassList("ts-inspector-root");
            root.AddToClassList("ts-preview-export-window");
            StyleSheet styleSheet = TextStudioTextEditor.LoadTextStudioInspectorStyleSheet();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            Label eyebrow = new Label("TEXT STUDIO");
            eyebrow.AddToClassList("ts-muted-text");
            root.Add(eyebrow);

            Label title = new Label("Export Preview Loop");
            title.AddToClassList("ts-section-title");
            title.AddToClassList("ts-preview-export-title");
            root.Add(title);

            Label description = new Label(
                "Export one complete pass of the lifecycle currently selected in the live preview.");
            description.AddToClassList("ts-muted-text");
            description.AddToClassList("ts-preview-export-copy");
            root.Add(description);

            TextStudioPreviewExportRendererRegistry.GetOptions(_rendererOptions);
            bool rendererDefinedByEffect = !string.IsNullOrEmpty(_snapshot.RendererId);
            _rendererId = rendererDefinedByEffect
                ? _snapshot.RendererId
                : EditorPrefs.GetString(
                    LastRendererPreference,
                    TextStudioPreviewExportRendererRegistry.DefaultRendererId);
            if (!rendererDefinedByEffect &&
                FindRendererOption(_rendererId) < 0 &&
                _rendererOptions.Count > 0)
                _rendererId = _rendererOptions[0].Id;
            _rendererButtons.Clear();
            if (!rendererDefinedByEffect)
            {
                Label rendererLabel = new Label("Render as");
                rendererLabel.AddToClassList("ts-preview-export-choice-label");
                root.Add(rendererLabel);
                VisualElement rendererRow = new VisualElement();
                rendererRow.AddToClassList("ts-preview-export-format-row");
                rendererRow.AddToClassList("ts-preview-export-renderer-row");
                for (int i = 0; i < _rendererOptions.Count; i++)
                {
                    TextStudioPreviewExportRendererOption option = _rendererOptions[i];
                    Button rendererButton = CreateRendererButton(option);
                    _rendererButtons.Add(rendererButton);
                    rendererRow.Add(rendererButton);
                }
                root.Add(rendererRow);
            }

            Label formatLabel = new Label("Format");
            formatLabel.AddToClassList("ts-preview-export-choice-label");
            root.Add(formatLabel);

            VisualElement formatRow = new VisualElement();
            formatRow.AddToClassList("ts-preview-export-format-row");
            bool mp4Available = TextStudioPreviewExportJob.IsFormatAvailable(
                TextStudioPreviewExportFormat.Mp4);
            _mp4Button = CreateFormatButton(
                "MP4",
                mp4Available ? "Smooth web video" : "Unavailable in Linux Editor",
                TextStudioPreviewExportFormat.Mp4);
            _mp4Button.SetEnabled(mp4Available);
            if (!mp4Available)
            {
                _mp4Button.tooltip = TextStudioPreviewExportJob.GetFormatUnavailableReason(
                    TextStudioPreviewExportFormat.Mp4);
            }
            _gifButton = CreateFormatButton(
                "GIF",
                "Transparent web loop",
                TextStudioPreviewExportFormat.Gif);
            formatRow.Add(_mp4Button);
            formatRow.Add(_gifButton);
            root.Add(formatRow);

            _details = new Label();
            _details.AddToClassList("ts-preview-export-details");
            root.Add(_details);

            _warning = new Label();
            _warning.AddToClassList("ts-preview-export-warning");
            root.Add(_warning);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            root.Add(spacer);

            VisualElement actions = CommonUITK.CreateWindowFooter(
                18f,
                14f,
                "ts-preview-export-actions");
            Button export = new Button(Export)
            {
                name = "ts-preview-export-confirm",
                text = "Export…",
                tooltip = "Choose a destination and export this preview loop."
            };
            export.AddToClassList("ts-composed-inspire-button");
            actions.Add(export);
            root.Add(actions);
            Refresh();
        }

        void OnDisable()
        {
            if (!_ownershipTransferred)
                _snapshot?.Dispose();
            _snapshot = null;
        }

        Button CreateFormatButton(
            string title,
            string subtitle,
            TextStudioPreviewExportFormat format)
        {
            Button button = new Button(() =>
            {
                if (!TextStudioPreviewExportJob.IsFormatAvailable(format))
                    return;
                _format = format;
                Refresh();
            });
            button.AddToClassList("ts-preview-export-format");
            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("ts-preview-export-format-title");
            button.Add(titleLabel);
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("ts-muted-text");
            subtitleLabel.AddToClassList("ts-preview-export-format-subtitle");
            button.Add(subtitleLabel);
            return button;
        }

        Button CreateRendererButton(TextStudioPreviewExportRendererOption option)
        {
            Button button = new Button(() =>
            {
                _rendererId = option.Id;
                EditorPrefs.SetString(LastRendererPreference, _rendererId);
                Refresh();
            })
            {
                tooltip = option.Description
            };
            button.AddToClassList("ts-preview-export-format");
            Label titleLabel = new Label(option.DisplayName);
            titleLabel.AddToClassList("ts-preview-export-format-title");
            button.Add(titleLabel);
            Label subtitleLabel = new Label(option.Description);
            subtitleLabel.AddToClassList("ts-muted-text");
            subtitleLabel.AddToClassList("ts-preview-export-format-subtitle");
            button.Add(subtitleLabel);
            return button;
        }

        void Refresh()
        {
            if (_snapshot == null || _details == null)
                return;
            if (!TextStudioPreviewExportJob.IsFormatAvailable(_format))
                _format = TextStudioPreviewExportJob.DefaultFormat;

            SetSelected(_mp4Button, _format == TextStudioPreviewExportFormat.Mp4);
            SetSelected(_gifButton, _format == TextStudioPreviewExportFormat.Gif);
            for (int i = 0; i < _rendererButtons.Count && i < _rendererOptions.Count; i++)
            {
                SetSelected(
                    _rendererButtons[i],
                    string.Equals(_rendererOptions[i].Id, _rendererId, StringComparison.Ordinal));
            }
            TextStudioPreviewExportPreset preset = TextStudioPreviewExportPreset.For(_format);
            int frameCount = preset.GetFrameCount(_snapshot.DisplayDuration);
            string loopBehavior = _format == TextStudioPreviewExportFormat.Gif
                ? "Transparent infinite loop embedded in the file"
                : "One cycle; loop it in the website or social platform";
            _details.text =
                GetRendererDisplayName() + "  •  " +
                preset.Width + " × " + preset.Height + "  •  " +
                preset.FramesPerSecond + " fps  •  " +
                FormatDuration(_snapshot.DisplayDuration) + "  •  " +
                frameCount + " frames\n" + loopBehavior;

            bool longGif = _format == TextStudioPreviewExportFormat.Gif && frameCount > 300;
            _warning.style.display = longGif ? DisplayStyle.Flex : DisplayStyle.None;
            _warning.text = longGif
                ? GetLongGifWarning()
                : string.Empty;
        }

        void Export()
        {
            if (_snapshot == null)
                return;
            if (!TextStudioPreviewExportJob.IsFormatAvailable(_format))
            {
                EditorUtility.DisplayDialog(
                    "Text Studio Export",
                    TextStudioPreviewExportJob.GetFormatUnavailableReason(_format),
                    "OK");
                return;
            }

            string extension = _format == TextStudioPreviewExportFormat.Gif ? "gif" : "mp4";
            string folder = EditorPrefs.GetString(LastFolderPreference, string.Empty);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    folder = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            }

            string destination = EditorUtility.SaveFilePanel(
                "Export Text Studio Preview Loop",
                folder,
                SanitizeFileName(_snapshot.DisplayName) + "_Loop." + extension,
                extension);
            if (string.IsNullOrEmpty(destination))
                return;

            string selectedFolder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(selectedFolder))
                EditorPrefs.SetString(LastFolderPreference, selectedFolder);
            if (!TextStudioPreviewExportJob.TryStart(
                    _snapshot,
                    _format,
                    _rendererId,
                    destination,
                    out string error))
            {
                EditorUtility.DisplayDialog("Text Studio Export", error, "OK");
                return;
            }

            _ownershipTransferred = true;
            _snapshot = null;
            Close();
        }

        static string GetLongGifWarning()
        {
            if (TextStudioPreviewExportJob.IsFormatAvailable(TextStudioPreviewExportFormat.Mp4))
                return "This GIF contains more than 300 frames and may be large. MP4 is usually better for long or gradient-heavy loops.";
            return "This GIF contains more than 300 frames and may be large. A shorter loop will export faster and produce a smaller file.";
        }

        static void ShowExportMenu(
            Button button,
            TextStudioEffectDefinitionAsset effect,
            TryCreateSnapshot createSnapshot,
            Action<GenericMenu> extendMenu)
        {
            GenericMenu menu = new GenericMenu();
            extendMenu?.Invoke(menu);
            if (extendMenu != null)
                menu.AddSeparator(string.Empty);
            if (effect != null)
                menu.AddItem(new GUIContent("Copy Effect Code"), false, () => TextStudioEffectCodeWindow.Copy(effect));
            else
                menu.AddDisabledItem(new GUIContent("Copy Effect Code"));
            menu.AddItem(new GUIContent("Import Effect Code"), false, TextStudioEffectCodeWindow.ShowWindow);
            menu.AddSeparator(string.Empty);
            if (TextStudioPreviewExportJob.IsExporting)
            {
                menu.AddDisabledItem(new GUIContent("Export Loop (export already running)"));
            }
            else
            {
                menu.AddItem(new GUIContent("Export Loop"), false, () =>
                {
                    TextStudioPreviewExportSnapshot snapshot = null;
                    string error = string.Empty;
                    if (createSnapshot != null && createSnapshot(out snapshot, out error))
                    {
                        ShowFor(snapshot);
                        return;
                    }

                    EditorUtility.DisplayDialog(
                        "Text Studio Export",
                        string.IsNullOrWhiteSpace(error)
                            ? "This preview is not currently available for export."
                            : error,
                        "OK");
                });
            }

            if (button != null && button.panel != null)
                menu.DropDown(button.worldBound);
            else
                menu.ShowAsContext();
        }

        static void SetSelected(VisualElement element, bool selected)
        {
            if (element == null)
                return;
            if (selected)
                element.AddToClassList("ts-preview-export-format-selected");
            else
                element.RemoveFromClassList("ts-preview-export-format-selected");
        }

        int FindRendererOption(string rendererId)
        {
            for (int i = 0; i < _rendererOptions.Count; i++)
            {
                if (string.Equals(_rendererOptions[i].Id, rendererId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        string GetRendererDisplayName()
        {
            int index = FindRendererOption(_rendererId);
            return index >= 0 ? _rendererOptions[index].DisplayName : "Flat Text";
        }

        static string FormatDuration(float seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return duration.TotalMinutes >= 1.0
                ? duration.ToString(@"m\:ss\.f")
                : duration.ToString(@"s\.f") + " s";
        }

        static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "TextStudioPreview" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            return result;
        }
    }
}
