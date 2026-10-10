using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace TextStudio.Editor
{
    internal static class TextAnimatorStyleSheetImporter
    {
        public static TextAnimatorMigrationStyleSheet ImportFromAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return TextAnimatorMigrationStyleSheet.Empty;

            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
                return TextAnimatorMigrationStyleSheet.Empty;

            return ImportFromText(File.ReadAllText(fullPath));
        }

        public static TextAnimatorMigrationStyleSheet ImportFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return TextAnimatorMigrationStyleSheet.Empty;

            List<TextAnimatorMigrationStyle> styles = new List<TextAnimatorMigrationStyle>();
            string currentTag = string.Empty;
            string currentOpening = string.Empty;
            string currentClosing = string.Empty;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("- styleTag:", StringComparison.Ordinal))
                {
                    AddCurrent(styles, currentTag, currentOpening, currentClosing);
                    currentTag = ReadValue(line, "- styleTag:");
                    currentOpening = string.Empty;
                    currentClosing = string.Empty;
                }
                else if (line.StartsWith("styleTag:", StringComparison.Ordinal))
                {
                    AddCurrent(styles, currentTag, currentOpening, currentClosing);
                    currentTag = ReadValue(line, "styleTag:");
                    currentOpening = string.Empty;
                    currentClosing = string.Empty;
                }
                else if (line.StartsWith("openingTag:", StringComparison.Ordinal))
                {
                    currentOpening = ReadValue(line, "openingTag:");
                }
                else if (line.StartsWith("closingTag:", StringComparison.Ordinal))
                {
                    currentClosing = ReadValue(line, "closingTag:");
                }
            }

            AddCurrent(styles, currentTag, currentOpening, currentClosing);
            return styles.Count > 0
                ? new TextAnimatorMigrationStyleSheet(styles)
                : TextAnimatorMigrationStyleSheet.Empty;
        }

        public static TextStudioTagExpansionSheet CreateExpansionSheet(TextAnimatorMigrationStyleSheet styleSheet)
        {
            TextStudioTagExpansionSheet sheet = UnityEngine.ScriptableObject.CreateInstance<TextStudioTagExpansionSheet>();
            if (styleSheet == null || styleSheet.Count == 0)
                return sheet;

            List<TextStudioTagExpansionEntry> entries = new List<TextStudioTagExpansionEntry>(styleSheet.Count);
            foreach (KeyValuePair<string, TextAnimatorMigrationStyle> pair in styleSheet.Styles)
                entries.Add(CreateEntry(pair.Value));

            sheet.SetEntries(entries);
            return sheet;
        }

        public const string AliasSheetName = "Text Animator Style Aliases";

        /// <summary>
        /// Adds or updates the given styles in the migration alias sheet inside <paramref name="destinationFolder"/>
        /// and registers that sheet in the active Project Settings, so existing style tags keep working unchanged.
        /// </summary>
        public static TextStudioTagExpansionSheet ImportAliases(
            IReadOnlyList<TextAnimatorMigrationStyle> styles,
            string destinationFolder,
            out int writtenCount)
        {
            writtenCount = 0;
            if (styles == null || styles.Count == 0)
                return null;

            string folder = TextAnimatorMigrationActions.EnsureFolder(destinationFolder);
            string path = folder + "/" + AliasSheetName + ".asset";
            TextStudioTagExpansionSheet sheet = AssetDatabase.LoadAssetAtPath<TextStudioTagExpansionSheet>(path);
            bool isNew = sheet == null;
            List<TextStudioTagExpansionEntry> entries = new List<TextStudioTagExpansionEntry>();
            if (isNew)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                sheet = UnityEngine.ScriptableObject.CreateInstance<TextStudioTagExpansionSheet>();
            }
            else
            {
                Undo.RecordObject(sheet, "Import Text Animator Style Aliases");
                for (int i = 0; i < sheet.Entries.Count; i++)
                {
                    if (sheet.Entries[i] != null)
                        entries.Add(sheet.Entries[i]);
                }
            }

            for (int i = 0; i < styles.Count; i++)
            {
                TextAnimatorMigrationStyle style = styles[i];
                if (string.IsNullOrWhiteSpace(style.StyleTag))
                    continue;

                TextStudioTagExpansionEntry entry = CreateEntry(style);
                int existing = entries.FindIndex(candidate =>
                    string.Equals(candidate.TagId, entry.TagId, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                    entries[existing] = entry;
                else
                    entries.Add(entry);
                writtenCount++;
            }

            sheet.SetEntries(entries);
            if (isNew)
            {
                AssetDatabase.CreateAsset(sheet, path);
                Undo.RegisterCreatedObjectUndo(sheet, "Import Text Animator Style Aliases");
            }
            else
            {
                EditorUtility.SetDirty(sheet);
            }

            TextStudioProjectSettings settings = TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
            if (settings == null)
                settings = TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical();

            Undo.RecordObject(settings, "Register Text Animator Style Aliases");
            settings.tagExpansionSheets ??= new List<TextStudioTagExpansionSheet>();
            if (!settings.tagExpansionSheets.Contains(sheet))
                settings.tagExpansionSheets.Add(sheet);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry(settings);
            return sheet;
        }

        static TextStudioTagExpansionEntry CreateEntry(TextAnimatorMigrationStyle style)
        {
            string opening = style.OpeningTag ?? string.Empty;
            string closing = style.ClosingTag ?? string.Empty;
            string derivedClosing = TextStudioTagExpansionUtility.DeriveClosingMarkup(opening);
            bool useAutoClosing = string.IsNullOrWhiteSpace(closing) ||
                                  string.Equals(closing, derivedClosing, StringComparison.Ordinal);
            TextStudioTagExpansionClosingMode mode = useAutoClosing
                ? TextStudioTagExpansionClosingMode.Auto
                : TextStudioTagExpansionClosingMode.Custom;

            return new TextStudioTagExpansionEntry(
                style.StyleTag.Trim(),
                opening,
                mode,
                mode == TextStudioTagExpansionClosingMode.Custom ? closing : string.Empty);
        }

        static void AddCurrent(
            List<TextAnimatorMigrationStyle> styles,
            string styleTag,
            string openingTag,
            string closingTag)
        {
            if (string.IsNullOrWhiteSpace(styleTag))
                return;

            styles.Add(new TextAnimatorMigrationStyle(
                styleTag.Trim(),
                openingTag ?? string.Empty,
                closingTag ?? string.Empty));
        }

        static string ReadValue(string line, string key)
        {
            string value = line.Length > key.Length ? line.Substring(key.Length).Trim() : string.Empty;
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    value = value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}
