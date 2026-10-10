using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal readonly struct TextAnimatorProjectDefaultsImportResult
    {
        public readonly TextStudioProjectSettings ProjectSettings;
        public readonly int TimingAssetsCreated;
        public readonly int TimingAssetsUpdated;

        public TextAnimatorProjectDefaultsImportResult(
            TextStudioProjectSettings projectSettings,
            int timingAssetsCreated,
            int timingAssetsUpdated)
        {
            ProjectSettings = projectSettings;
            TimingAssetsCreated = timingAssetsCreated;
            TimingAssetsUpdated = timingAssetsUpdated;
        }
    }

    internal static class TextAnimatorProjectDefaultsImporter
    {
        public const string ImportedNameSuffix = " (Text Studio)";

        /// <summary>
        /// Converts Text Animator character-delay assets into Text Studio Character Timings and uses the first one as
        /// the project typing speed. Repeated imports update the converted asset in the destination folder instead of
        /// creating duplicates, and the Text Animator sources are never modified.
        /// </summary>
        public static TextAnimatorProjectDefaultsImportResult Import(
            string destinationFolder,
            IReadOnlyList<UnityEngine.Object> timingSources)
        {
            string folder = TextAnimatorMigrationActions.EnsureFolder(destinationFolder);

            TextStudioProjectSettings settings = TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
            if (settings == null)
                settings = TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical();

            Undo.RecordObject(settings, "Import Text Animator Project Defaults");
            int created = 0;
            int updated = 0;
            float firstBaseDelay = -1f;
            int sourceCount = timingSources != null ? timingSources.Count : 0;
            for (int i = 0; i < sourceCount; i++)
            {
                UnityEngine.Object source = timingSources[i];
                if (!IsTimingSource(source))
                    continue;

                string timingName = GetImportedName(source);
                string path = folder + "/" + SanitizeFileName(timingName) + ".asset";
                CharacterTimings timing = AssetDatabase.LoadAssetAtPath<CharacterTimings>(path);
                bool isNew = timing == null;
                if (isNew)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                    timing = ScriptableObject.CreateInstance<CharacterTimings>();
                }
                else
                {
                    Undo.RecordObject(timing, "Update Imported Text Animator Character Timings");
                }

                SerializedObject serialized = new SerializedObject(source);
                timing.BaseDelay = ReadFloat(serialized, "waitForNormalChars", 0.03f);
                timing.LongPunctuationDelay = ReadFloat(serialized, "waitLong", 0.6f);
                timing.MediumPunctuationDelay = ReadFloat(serialized, "waitMiddle", 0.2f);
                timing.CollapseMultiplePunctuation = ReadBool(serialized, "avoidMultiplePunctuationWait", false);
                timing.WaitForNewLines = ReadBool(serialized, "waitForNewLines", true);
                timing.NewLineDelay = timing.BaseDelay;
                timing.WaitAfterFinalReveal = ReadBool(serialized, "waitForLastCharacter", true);
                timing.SkipLastPunctuationDelay = ReadBool(serialized, "skipLastPunctuationWait", false);
                timing.AbbreviationWhitelist = ReadStringArray(serialized, "punctuationWhitelist");
                timing.name = timingName;

                if (isNew)
                {
                    AssetDatabase.CreateAsset(timing, path);
                    Undo.RegisterCreatedObjectUndo(timing, "Import Text Animator Character Timings");
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(timing);
                    updated++;
                }

                if (firstBaseDelay < 0f)
                    firstBaseDelay = timing.BaseDelay;
            }

            if (firstBaseDelay >= 0f)
            {
                settings.overrideDefaults = true;
                settings.defaultCharDelay = firstBaseDelay;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry(settings);
            return new TextAnimatorProjectDefaultsImportResult(settings, created, updated);
        }

        /// <summary>Returns true for Text Animator character-delay assets that this importer can convert.</summary>
        public static bool IsTimingSource(UnityEngine.Object asset)
        {
            if (asset == null)
                return false;

            Type type = asset.GetType();
            return type.FullName != null &&
                   type.FullName.StartsWith("Febucci.TextAnimator", StringComparison.Ordinal) &&
                   type.Name.IndexOf("TypingDelaysByCharacter", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Name of the Character Timings asset created for a Text Animator source.</summary>
        public static string GetImportedName(UnityEngine.Object source)
        {
            string sourceName = source != null && !string.IsNullOrEmpty(source.name)
                ? source.name
                : "Imported Character Timings";
            return sourceName + ImportedNameSuffix;
        }

        static float ReadFloat(SerializedObject serialized, string name, float fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property != null ? Mathf.Max(0f, property.floatValue) : fallback;
        }

        static bool ReadBool(SerializedObject serialized, string name, bool fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property != null ? property.boolValue : fallback;
        }

        static string ReadStringArray(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null || !property.isArray)
                return string.Empty;

            List<string> values = new List<string>(property.arraySize);
            for (int i = 0; i < property.arraySize; i++)
            {
                string value = property.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }
            return string.Join(",", values);
        }

        static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                value = value.Replace(invalid[i], '_');
            return value;
        }
    }
}
