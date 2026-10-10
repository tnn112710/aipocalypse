using System;
using System.Collections.Generic;
using System.IO;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal readonly struct TextAnimatorPlaybackDefinition
    {
        public string TagId { get; }
        public PlaybackMode Mode { get; }
        public float DelayBeforeStart { get; }
        public float ForwardDuration { get; }
        public float HoldDuration { get; }
        public float ReturnDuration { get; }
        public int CycleCount { get; }
        public float DelayBetweenCycles { get; }
        public EffectPlaybackCompletion Completion { get; }
        public float ExternalPreviewValue { get; }

        public TextAnimatorPlaybackDefinition(
            string tagId,
            PlaybackMode mode,
            float delayBeforeStart,
            float forwardDuration,
            float holdDuration,
            float returnDuration,
            int cycleCount,
            float delayBetweenCycles,
            EffectPlaybackCompletion completion,
            float externalPreviewValue)
        {
            TagId = tagId ?? string.Empty;
            Mode = mode;
            DelayBeforeStart = Mathf.Max(0f, delayBeforeStart);
            ForwardDuration = Mathf.Max(0.0001f, forwardDuration);
            HoldDuration = Mathf.Max(0f, holdDuration);
            ReturnDuration = Mathf.Max(0f, returnDuration);
            CycleCount = Mathf.Max(0, cycleCount);
            DelayBetweenCycles = Mathf.Max(0f, delayBetweenCycles);
            Completion = completion;
            ExternalPreviewValue = Mathf.Clamp01(externalPreviewValue);
        }
    }

    internal readonly struct TextAnimatorPlaybackImportResult
    {
        public int CreatedCount { get; }
        public int UpdatedCount { get; }
        public int SkippedCount { get; }
        public IReadOnlyList<EffectPlaybackAsset> Assets { get; }
        public TextStudioProjectSettings ProjectSettings { get; }

        public TextAnimatorPlaybackImportResult(
            int createdCount,
            int updatedCount,
            int skippedCount,
            IReadOnlyList<EffectPlaybackAsset> assets,
            TextStudioProjectSettings projectSettings)
        {
            CreatedCount = Mathf.Max(0, createdCount);
            UpdatedCount = Mathf.Max(0, updatedCount);
            SkippedCount = Mathf.Max(0, skippedCount);
            Assets = assets ?? Array.Empty<EffectPlaybackAsset>();
            ProjectSettings = projectSettings;
        }
    }

    internal static class TextAnimatorPlaybackImporter
    {
        const float DefaultDuration = 0.5f;

        public static bool CanImport(TextAnimatorMigrationArtifactRecord artifact)
        {
            return artifact != null && artifact.Kind == TextAnimatorMigrationArtifactKind.Playback;
        }

        public static List<Object> CollectImportableSources(IReadOnlyList<TextAnimatorMigrationArtifactRecord> artifacts)
        {
            List<Object> sources = new List<Object>();
            if (artifacts == null)
                return sources;

            HashSet<int> instanceIds = new HashSet<int>();
            for (int i = 0; i < artifacts.Count; i++)
            {
                TextAnimatorMigrationArtifactRecord artifact = artifacts[i];
                if (!CanImport(artifact))
                    continue;

                Object source = artifact.Target;
                if (source == null && !string.IsNullOrEmpty(artifact.AssetPath))
                    source = AssetDatabase.LoadMainAssetAtPath(artifact.AssetPath);
                if (source == null || !instanceIds.Add(source.GetStableId()))
                    continue;

                if (TryReadDefinition(source, out _, out _))
                    sources.Add(source);
            }

            return sources;
        }

        public static bool TryReadDefinition(
            Object source,
            out TextAnimatorPlaybackDefinition definition,
            out string error)
        {
            definition = default;
            if (source == null)
            {
                error = "The Text Animator Playback asset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(source);
            SerializedProperty playback = serialized.FindProperty("playback");
            string tagId = ReadString(serialized.FindProperty("tagID"));
            if (string.IsNullOrWhiteSpace(tagId))
                tagId = ReadString(serialized.FindProperty("tagId"));
            if (string.IsNullOrWhiteSpace(tagId))
            {
                error = "The Text Animator Playback has no Tag ID.";
                return false;
            }

            if (playback == null)
            {
                error = "The selected asset does not expose a supported Text Animator playback envelope.";
                return false;
            }

            SerializedProperty fadeIn = playback.FindPropertyRelative("fadeInDuration");
            SerializedProperty fadeOut = playback.FindPropertyRelative("fadeOutDuration");
            SerializedProperty cycles = playback.FindPropertyRelative("cycles");
            if (fadeIn != null || fadeOut != null || cycles != null)
            {
                int sourceCycles = ReadInt(cycles, 1);
                definition = new TextAnimatorPlaybackDefinition(
                    tagId.Trim(),
                    PlaybackMode.PingPong,
                    ReadFloat(playback.FindPropertyRelative("delayBeforeStart"), 0f),
                    ReadFloat(fadeIn, DefaultDuration),
                    ReadFloat(playback.FindPropertyRelative("stillDuration"), 0f),
                    ReadFloat(fadeOut, DefaultDuration),
                    sourceCycles <= 0 ? 0 : sourceCycles,
                    ReadFloat(playback.FindPropertyRelative("delayBetweenCycles"), 0f),
                    EffectPlaybackCompletion.Reset,
                    1f);
                error = string.Empty;
                return true;
            }

            SerializedProperty fadeDuration = playback.FindPropertyRelative("fadeDuration");
            if (fadeDuration != null)
            {
                definition = new TextAnimatorPlaybackDefinition(
                    tagId.Trim(),
                    PlaybackMode.Once,
                    ReadFloat(playback.FindPropertyRelative("delayBeforeStart"), 0f),
                    ReadFloat(fadeDuration, DefaultDuration),
                    ReadFloat(playback.FindPropertyRelative("stillDuration"), 0f),
                    DefaultDuration,
                    1,
                    0f,
                    EffectPlaybackCompletion.HoldFinal,
                    1f);
                error = string.Empty;
                return true;
            }

            SerializedProperty intensity = playback.FindPropertyRelative("intensity01");
            if (intensity != null)
            {
                definition = new TextAnimatorPlaybackDefinition(
                    tagId.Trim(),
                    PlaybackMode.External,
                    0f,
                    DefaultDuration,
                    0f,
                    DefaultDuration,
                    0,
                    0f,
                    EffectPlaybackCompletion.HoldFinal,
                    ReadFloat(intensity, 1f));
                error = string.Empty;
                return true;
            }

            error = "This Text Animator Playback subtype is not supported by the automatic importer.";
            return false;
        }

        public static bool TryImport(
            IReadOnlyList<Object> sources,
            string destinationFolder,
            out TextAnimatorPlaybackImportResult result,
            out string message)
        {
            result = default;
            string normalizedFolder = NormalizeAssetFolder(destinationFolder);
            if (string.IsNullOrEmpty(normalizedFolder) || !AssetDatabase.IsValidFolder(normalizedFolder))
            {
                message = "Choose an existing folder inside this Unity project's Assets folder.";
                return false;
            }

            List<EffectPlaybackAsset> imported = new List<EffectPlaybackAsset>();
            HashSet<string> importedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            int updated = 0;
            int skipped = 0;

            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    if (!TryReadDefinition(sources[i], out TextAnimatorPlaybackDefinition definition, out _))
                    {
                        skipped++;
                        continue;
                    }

                    if (!importedTags.Add(definition.TagId))
                        continue;

                    string assetPath = normalizedFolder + "/" + SanitizeFileName(definition.TagId) + ".asset";
                    EffectPlaybackAsset playback = AssetDatabase.LoadAssetAtPath<EffectPlaybackAsset>(assetPath);
                    if (playback == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                        playback = ScriptableObject.CreateInstance<EffectPlaybackAsset>();
                        playback.name = definition.TagId;
                        Configure(playback, in definition);
                        AssetDatabase.CreateAsset(playback, assetPath);
                        Undo.RegisterCreatedObjectUndo(playback, "Import Text Animator Playback");
                        created++;
                    }
                    else
                    {
                        Undo.RecordObject(playback, "Update Imported Text Animator Playback");
                        playback.name = definition.TagId;
                        Configure(playback, in definition);
                        EditorUtility.SetDirty(playback);
                        updated++;
                    }

                    imported.Add(playback);
                }
            }

            if (imported.Count == 0)
            {
                message = skipped > 0
                    ? "No supported Text Animator Playback assets were found in this selection."
                    : "No Text Animator Playback assets were selected.";
                return false;
            }

            TextStudioProjectSettings settings = EnsureProjectSettings(normalizedFolder);
            Undo.RecordObject(settings, "Register Imported Text Animator Playbacks");
            settings.playbackCatalog ??= new List<EffectPlaybackAsset>();
            for (int i = 0; i < imported.Count; i++)
            {
                EffectPlaybackAsset playback = imported[i];
                RemoveCatalogEntries(settings.playbackCatalog, playback.name);
                settings.playbackCatalog.Add(playback);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            TextStudioProjectSettingsEditorUtility.ActivateAndRebuildRegistry(settings);
            Selection.objects = imported.ToArray();

            result = new TextAnimatorPlaybackImportResult(created, updated, skipped, imported, settings);
            message = BuildResultMessage(in result);
            return true;
        }

        static void Configure(EffectPlaybackAsset playback, in TextAnimatorPlaybackDefinition definition)
        {
            playback.Configure(
                definition.Mode,
                definition.DelayBeforeStart,
                definition.ForwardDuration,
                definition.HoldDuration,
                definition.ReturnDuration,
                definition.CycleCount,
                definition.DelayBetweenCycles,
                definition.Completion,
                definition.ExternalPreviewValue);
        }

        static TextStudioProjectSettings EnsureProjectSettings(string destinationFolder)
        {
            TextStudioProjectSettings settings = TextStudioProjectSettingsEditorUtility.FindActiveOrFirst();
            if (settings != null)
                return settings;

            return TextStudioProjectSettingsEditorUtility.GetOrCreateCanonical();
        }

        static void RemoveCatalogEntries(List<EffectPlaybackAsset> catalog, string name)
        {
            for (int i = catalog.Count - 1; i >= 0; i--)
            {
                EffectPlaybackAsset entry = catalog[i];
                if (entry == null || string.Equals(entry.name, name, StringComparison.OrdinalIgnoreCase))
                    catalog.RemoveAt(i);
            }
        }

        static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return string.Empty;

            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(normalized).Replace('\\', '/').TrimEnd('/');
            if (!fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return fullPath.Substring(projectRoot.Length + 1);
        }

        static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (Array.IndexOf(invalid, characters[i]) >= 0 || characters[i] == '/' || characters[i] == '\\')
                    characters[i] = '_';
            }

            string sanitized = new string(characters).Trim();
            return string.IsNullOrEmpty(sanitized) ? "Playback" : sanitized;
        }

        static string BuildResultMessage(in TextAnimatorPlaybackImportResult result)
        {
            string message = "Imported " + result.Assets.Count + " Playback " +
                             (result.Assets.Count == 1 ? "asset" : "assets") +
                             ": " + result.CreatedCount + " created, " + result.UpdatedCount + " updated";
            if (result.SkippedCount > 0)
                message += ", " + result.SkippedCount + " unsupported skipped";
            return message + ". Registered names are ready for effect markup.";
        }

        static string ReadString(SerializedProperty property)
        {
            return property != null && property.propertyType == SerializedPropertyType.String
                ? property.stringValue ?? string.Empty
                : string.Empty;
        }

        static float ReadFloat(SerializedProperty property, float fallback)
        {
            return property != null && property.propertyType == SerializedPropertyType.Float
                ? property.floatValue
                : fallback;
        }

        static int ReadInt(SerializedProperty property, int fallback)
        {
            return property != null && property.propertyType == SerializedPropertyType.Integer
                ? property.intValue
                : fallback;
        }
    }
}
