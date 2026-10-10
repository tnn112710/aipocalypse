using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal readonly struct TextAnimatorMigrationActionResult
    {
        public readonly int ChangedCount;
        public readonly int ManualCount;
        public readonly bool ChangedScenes;
        public readonly string Message;

        public TextAnimatorMigrationActionResult(int changedCount, int manualCount, bool changedScenes, string message)
        {
            ChangedCount = Mathf.Max(0, changedCount);
            ManualCount = Mathf.Max(0, manualCount);
            ChangedScenes = changedScenes;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Additive migration operations used by the wizard. They add Text Studio components and assets, record Undo,
    /// save changed prefabs, and never modify or remove Text Animator components, assets or settings.
    /// </summary>
    internal static class TextAnimatorMigrationActions
    {
        public const string DefaultOutputFolder = "Assets/Text Studio Migration";

        public static TextAnimatorMigrationActionResult Run(
            TextAnimatorMigrationStepKind kind,
            IEnumerable<TextAnimatorMigrationPlanItem> items,
            string outputFolder)
        {
            List<TextAnimatorMigrationPlanItem> ready = new List<TextAnimatorMigrationPlanItem>();
            if (items != null)
            {
                foreach (TextAnimatorMigrationPlanItem item in items)
                {
                    if (item != null && item.State == TextAnimatorMigrationItemState.Ready)
                        ready.Add(item);
                }
            }

            if (ready.Count == 0)
                return new TextAnimatorMigrationActionResult(0, 0, false, "Nothing left to convert in this step.");

            switch (kind)
            {
                case TextAnimatorMigrationStepKind.AddTextStudio:
                    return AddTextStudio(ready);
                case TextAnimatorMigrationStepKind.SoundsAndEvents:
                    return RecreateSoundsAndEvents(ready);
                case TextAnimatorMigrationStepKind.TypingSpeed:
                    return ImportTypingSpeed(ready, outputFolder);
                case TextAnimatorMigrationStepKind.Playbacks:
                    return ImportPlaybacks(ready, outputFolder);
                case TextAnimatorMigrationStepKind.StyleAliases:
                    return ImportStyleAliases(ready, outputFolder);
                default:
                    return new TextAnimatorMigrationActionResult(0, 0, false, string.Empty);
            }
        }

        static TextAnimatorMigrationActionResult AddTextStudio(List<TextAnimatorMigrationPlanItem> items)
        {
            List<TMP_Text> texts = new List<TMP_Text>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Text != null)
                    texts.Add(items[i].Text);
            }

            IReadOnlyList<TextStudioText> created = TextStudioTMPInspectorHeader.AddTextStudioToMissingTargets(texts, false);
            bool changedScenes = SaveChangedPrefabs(created);
            string message = created.Count == 1
                ? "Added Text Studio to 1 text."
                : "Added Text Studio to " + created.Count + " texts.";
            return new TextAnimatorMigrationActionResult(created.Count, 0, changedScenes, message);
        }

        static TextAnimatorMigrationActionResult RecreateSoundsAndEvents(List<TextAnimatorMigrationPlanItem> items)
        {
            List<Object> changed = new List<Object>();
            int objects = 0;
            int copied = 0;
            int manual = 0;
            for (int i = 0; i < items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = items[i];
                GameObject gameObject = item.GameObject;
                if (gameObject == null)
                    continue;

                // Sound and event companions follow the Text Studio component on the same object.
                TMP_Text text = gameObject.GetComponent<TMP_Text>();
                if (text != null && gameObject.GetComponent<TextStudioText>() == null)
                {
                    IReadOnlyList<TextStudioText> created = TextStudioTMPInspectorHeader.AddTextStudioToMissingTargets(new[] { text }, false);
                    for (int c = 0; c < created.Count; c++)
                        changed.Add(created[c]);
                }

                bool changedObject = false;
                if (item.SoundWriter != null && gameObject.GetComponent<TextStudioAudioPlayer>() == null)
                    changedObject |= TextAnimatorMigrationCompanionUtility.TryCreateSoundWriter(item.SoundWriter);

                if (item.HasEvents && item.Typewriter != null && gameObject.GetComponent<TextStudioTypewriterEventRelay>() == null &&
                    TextAnimatorMigrationCompanionUtility.TryCreateEventRelay(item.Typewriter, out TextAnimatorEventCopyReport report))
                {
                    changedObject = true;
                    copied += report.CopiedListenerCount;
                    manual += report.ManualRewireListenerCount;
                }

                if (changedObject)
                {
                    objects++;
                    changed.Add(gameObject);
                }
            }

            bool changedScenes = SaveChangedPrefabs(changed);
            string message = (objects == 1 ? "Recreated sounds and events on 1 object" : "Recreated sounds and events on " + objects + " objects") +
                             (copied > 0 ? " and copied " + (copied == 1 ? "1 event listener" : copied + " event listeners") : string.Empty) + ".";
            if (manual > 0)
            {
                message += manual == 1
                    ? " 1 listener that uses Text Animator data still needs to be reconnected."
                    : " " + manual + " listeners that use Text Animator data still need to be reconnected.";
            }

            return new TextAnimatorMigrationActionResult(objects, manual, changedScenes, message);
        }

        static TextAnimatorMigrationActionResult ImportTypingSpeed(List<TextAnimatorMigrationPlanItem> items, string outputFolder)
        {
            List<Object> sources = new List<Object>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Source != null)
                    sources.Add(items[i].Source);
            }

            TextAnimatorProjectDefaultsImportResult result = TextAnimatorProjectDefaultsImporter.Import(outputFolder, sources);
            int converted = result.TimingAssetsCreated + result.TimingAssetsUpdated;
            if (converted == 0)
                return new TextAnimatorMigrationActionResult(0, 0, false, "No typing speed asset could be converted.");

            string message = (converted == 1 ? "Converted 1 typing speed asset" : "Converted " + converted + " typing speed assets") +
                             " and set Text Studio's default typing speed to match.";
            return new TextAnimatorMigrationActionResult(converted, 0, false, message);
        }

        static TextAnimatorMigrationActionResult ImportPlaybacks(List<TextAnimatorMigrationPlanItem> items, string outputFolder)
        {
            List<Object> sources = new List<Object>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Source != null)
                    sources.Add(items[i].Source);
            }

            string folder = EnsureFolder(outputFolder);
            bool imported = TextAnimatorPlaybackImporter.TryImport(sources, folder, out TextAnimatorPlaybackImportResult result, out string message);
            int count = imported ? result.Assets.Count : 0;
            return new TextAnimatorMigrationActionResult(count, 0, false, message);
        }

        static TextAnimatorMigrationActionResult ImportStyleAliases(List<TextAnimatorMigrationPlanItem> items, string outputFolder)
        {
            Dictionary<string, TextAnimatorMigrationStyleSheet> sheets =
                new Dictionary<string, TextAnimatorMigrationStyleSheet>(StringComparer.OrdinalIgnoreCase);
            List<TextAnimatorMigrationStyle> styles = new List<TextAnimatorMigrationStyle>(items.Count);
            int missing = 0;
            for (int i = 0; i < items.Count; i++)
            {
                TextAnimatorMigrationPlanItem item = items[i];
                if (!sheets.TryGetValue(item.StyleSheetPath, out TextAnimatorMigrationStyleSheet sheet))
                {
                    sheet = TextAnimatorStyleSheetImporter.ImportFromAsset(item.StyleSheetPath);
                    sheets.Add(item.StyleSheetPath, sheet);
                }

                if (sheet.TryGet(item.TagId, out TextAnimatorMigrationStyle style))
                    styles.Add(style);
                else
                    missing++;
            }

            TextStudioTagExpansionSheet aliases = TextAnimatorStyleSheetImporter.ImportAliases(styles, outputFolder, out int written);
            string message = written == 1 ? "Imported 1 style alias" : "Imported " + written + " style aliases";
            message += aliases != null ? " into \"" + aliases.name + "\"." : ".";
            if (missing > 0)
            {
                message += missing == 1
                    ? " 1 style could not be read from its Text Animator style sheet."
                    : " " + missing + " styles could not be read from their Text Animator style sheet.";
            }

            return new TextAnimatorMigrationActionResult(written, missing, false, message);
        }

        /// <summary>
        /// Optional rewrite that replaces Text Animator style tags with their markup in texts that can be edited.
        /// The current value of each text is analyzed again, so later edits are never overwritten.
        /// </summary>
        public static TextAnimatorMigrationActionResult ExpandStyleTags(TextAnimatorMigrationInventory inventory)
        {
            if (inventory == null)
                return new TextAnimatorMigrationActionResult(0, 0, false, string.Empty);

            List<TextAnimatorMigrationStyle> styles = new List<TextAnimatorMigrationStyle>();
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < inventory.Artifacts.Count; i++)
            {
                TextAnimatorMigrationArtifactRecord artifact = inventory.Artifacts[i];
                bool isStyleSource = artifact.Kind == TextAnimatorMigrationArtifactKind.Style ||
                                     artifact.Kind == TextAnimatorMigrationArtifactKind.StyleSheet;
                if (!isStyleSource || string.IsNullOrEmpty(artifact.AssetPath) || !paths.Add(artifact.AssetPath))
                    continue;

                foreach (KeyValuePair<string, TextAnimatorMigrationStyle> pair in TextAnimatorStyleSheetImporter.ImportFromAsset(artifact.AssetPath).Styles)
                    styles.Add(pair.Value);
            }

            if (styles.Count == 0)
                return new TextAnimatorMigrationActionResult(0, 0, false, "No Text Animator style sheet was found.");

            TextAnimatorMigrationStyleSheet styleSheet = new TextAnimatorMigrationStyleSheet(styles);
            List<Object> changed = new List<Object>();
            for (int i = 0; i < inventory.Sources.Count; i++)
            {
                TextAnimatorMigrationCandidate candidate = inventory.Sources[i].Candidate;
                if (candidate == null || !candidate.CanApply)
                    continue;

                SerializedProperty property = new SerializedObject(candidate.Target).FindProperty(candidate.PropertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.String)
                    continue;

                TextAnimatorMigrationResult result = TextAnimatorMigrationAnalyzer.Analyze(property.stringValue, styleSheet);
                if (result.HasChanges && TextAnimatorMigrationAssetScanner.TryApplyCandidate(candidate, result.ConvertedMarkup))
                    changed.Add(candidate.Target);
            }

            bool changedScenes = SaveChangedPrefabs(changed);
            string message = changed.Count == 1
                ? "Wrote style markup into 1 text."
                : "Wrote style markup into " + changed.Count + " texts.";
            return new TextAnimatorMigrationActionResult(changed.Count, 0, changedScenes, message);
        }

        /// <summary>Returns an <c>Assets/</c> folder for generated assets and creates it when missing.</summary>
        public static string EnsureFolder(string folder)
        {
            string normalized = NormalizeFolder(folder);
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return normalized;
        }

        /// <summary>Uses <see cref="DefaultOutputFolder"/> for empty paths and paths outside this project's Assets folder.</summary>
        public static string NormalizeFolder(string folder)
        {
            return TryGetAssetFolder(folder, out string assetFolder) ? assetFolder : DefaultOutputFolder;
        }

        /// <summary>Converts a project-relative or absolute folder inside this project's Assets folder to an asset path.</summary>
        public static bool TryGetAssetFolder(string folder, out string assetFolder)
        {
            assetFolder = string.Empty;
            if (string.IsNullOrWhiteSpace(folder))
                return false;

            string normalized = folder.Replace('\\', '/').Trim().TrimEnd('/');
            if (string.Equals(normalized, "Assets", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                assetFolder = normalized;
                return true;
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(normalized).Replace('\\', '/').TrimEnd('/');
            if (string.Equals(fullPath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                assetFolder = "Assets";
                return true;
            }

            if (!fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            assetFolder = "Assets" + fullPath.Substring(assetsRoot.Length);
            return true;
        }

        // Prefab assets are saved right away; scene changes stay in the open scenes until the user saves them.
        static bool SaveChangedPrefabs(IEnumerable<Object> changed)
        {
            bool changedScenes = false;
            HashSet<GameObject> roots = new HashSet<GameObject>();
            foreach (Object obj in changed)
            {
                GameObject gameObject = obj as GameObject;
                if (gameObject == null && obj is Component component)
                    gameObject = component.gameObject;
                if (gameObject == null)
                    continue;

                if (EditorUtility.IsPersistent(gameObject))
                    roots.Add(gameObject.transform.root.gameObject);
                else
                    changedScenes = true;
            }

            foreach (GameObject root in roots)
                PrefabUtility.SavePrefabAsset(root);

            return changedScenes;
        }
    }
}
