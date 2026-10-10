using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioProjectSettingsEditorUtility
    {
        const string SearchFilter = "t:TextStudioProjectSettings";

        public static TextStudioProjectSettings FindActiveOrFirst()
        {
            return ActivateCanonical();
        }

        public static TextStudioProjectSettings ActivateDeterministic(bool rebuildRegistry)
        {
            TextStudioProjectSettings settings = FindCanonical();
            if (rebuildRegistry ||
                !ReferenceEquals(TextStudioProjectSettings.Active, settings))
            {
                TextStudioProjectSettings.SetActive(settings);
            }
            return settings;
        }

        public static TextStudioProjectSettings RefreshProjectCatalog()
        {
            TextStudioProjectSettings settings = FindCanonical();
            TextStudioProjectSettings.SetActive(settings);
            return settings;
        }

        public static void ActivateAndRebuildRegistry(TextStudioProjectSettings settings)
        {
            TextStudioProjectSettings.SetActive(settings);
        }

        public static TextStudioProjectSettings FindCanonical()
        {
            return AssetDatabase.LoadAssetAtPath<TextStudioProjectSettings>(
                TextStudioProjectSettings.CanonicalAssetPath);
        }

        public static List<TextStudioProjectSettings> FindAll()
        {
            string[] guids = AssetDatabase.FindAssets(SearchFilter);
            string[] paths = new string[guids != null ? guids.Length : 0];
            for (int i = 0; i < paths.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

            List<TextStudioProjectSettings> settings = new List<TextStudioProjectSettings>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                TextStudioProjectSettings asset = AssetDatabase.LoadAssetAtPath<TextStudioProjectSettings>(paths[i]);
                if (asset != null)
                    settings.Add(asset);
            }

            return settings;
        }

        public static TextStudioProjectSettings GetOrCreateCanonical()
        {
            TextStudioProjectSettings canonical = FindCanonical();
            if (canonical != null)
                return canonical;

            List<TextStudioProjectSettings> settings = FindAll();
            if (settings.Count == 1)
            {
                if (!TryMoveToCanonical(settings[0], out string moveError))
                    throw new InvalidOperationException(moveError);
                return FindCanonical();
            }
            if (settings.Count > 1)
            {
                throw new InvalidOperationException(
                    "Multiple Text Studio Project Settings assets exist. Keep one asset, then move it to " +
                    TextStudioProjectSettings.CanonicalAssetPath + ".");
            }

            EnsureCanonicalFolders();
            TextStudioProjectSettings created =
                ScriptableObject.CreateInstance<TextStudioProjectSettings>();
            AssetDatabase.CreateAsset(
                created,
                TextStudioProjectSettings.CanonicalAssetPath);
            Undo.RegisterCreatedObjectUndo(created, "Create Text Studio Project Settings");
            AssetDatabase.SaveAssetIfDirty(created);
            TextStudioProjectSettings.SetActive(created);
            return created;
        }

        public static bool TryMoveToCanonical(
            TextStudioProjectSettings settings,
            out string error)
        {
            error = string.Empty;
            if (settings == null)
            {
                error = "Choose a Text Studio Project Settings asset to move.";
                return false;
            }

            string currentPath = AssetDatabase.GetAssetPath(settings);
            if (string.Equals(
                    currentPath,
                    TextStudioProjectSettings.CanonicalAssetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (FindCanonical() != null)
            {
                error = "The canonical Text Studio Project Settings asset already exists at " +
                    TextStudioProjectSettings.CanonicalAssetPath + ".";
                return false;
            }

            EnsureCanonicalFolders();
            string moveError = AssetDatabase.MoveAsset(
                currentPath,
                TextStudioProjectSettings.CanonicalAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                error = "Text Studio could not move the settings asset: " + moveError;
                return false;
            }

            AssetDatabase.SaveAssets();
            TextStudioProjectSettings moved = FindCanonical();
            TextStudioProjectSettings.SetActive(moved);
            return moved != null;
        }

        public static string GetAssetPath(TextStudioProjectSettings settings)
        {
            return settings != null ? AssetDatabase.GetAssetPath(settings) : string.Empty;
        }

        public static HashSet<string> GetCanonicalDependencyPaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                TextStudioProjectSettings.CanonicalAssetPath
            };
            if (FindCanonical() == null)
                return paths;

            string[] dependencies = AssetDatabase.GetDependencies(
                TextStudioProjectSettings.CanonicalAssetPath,
                true);
            for (int i = 0; i < dependencies.Length; i++)
                paths.Add(dependencies[i]);
            return paths;
        }

        [MenuItem("Assets/Create/Text Studio/Setup/Project Settings", false, 301)]
        static void CreateOrSelectCanonical()
        {
            TextStudioProjectSettings settings = GetOrCreateCanonical();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        static TextStudioProjectSettings ActivateCanonical()
        {
            TextStudioProjectSettings settings = FindCanonical();
            if (!ReferenceEquals(TextStudioProjectSettings.Active, settings))
                TextStudioProjectSettings.SetActive(settings);
            return settings;
        }

        static void EnsureCanonicalFolders()
        {
            EnsureFolder("Assets", "Text Studio");
            EnsureFolder("Assets/Text Studio", "Resources");
            EnsureFolder("Assets/Text Studio/Resources", "TextStudio");
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
