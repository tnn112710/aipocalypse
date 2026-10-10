using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Validates prepared fonts reached by player roots or an explicit content-build graph.</summary>
    public static class TextStudioFontAppearanceBuildValidation
    {
        internal static string ValidatePlayerAssets()
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.Ordinal);
            if (TextStudioRuntimePreparationBuildProcessor.CurrentRoots != null)
                roots.UnionWith(TextStudioRuntimePreparationBuildProcessor.CurrentRoots);
            else foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled) roots.Add(scene.path);
            foreach (Object asset in PlayerSettings.GetPreloadedAssets())
                if (asset != null) roots.Add(AssetDatabase.GetAssetPath(asset));
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (IsPlayerResource(path) && !AssetDatabase.IsValidFolder(path)) roots.Add(path);
            if (TextStudioRuntimePreparationBuildProcessor.CurrentRoots != null)
                roots.RemoveWhere(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
            string error = ValidateAssets(roots);
            return error.Length > 0 ? error : TextStudioColorAppearanceBuildValidation.ValidateAssets(roots);
        }

        internal static bool IsPlayerResource(string path) =>
            path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 &&
            path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0;

        /// <summary>Returns an actionable error for prepared fonts referenced by these assets, including transitive dependencies.</summary>
        public static string ValidateAssets(IEnumerable<string> rootAssetPaths)
        {
            if (rootAssetPaths == null) throw new ArgumentNullException(nameof(rootAssetPaths));
            HashSet<string> roots = new HashSet<string>(rootAssetPaths, StringComparer.Ordinal);
            roots.RemoveWhere(string.IsNullOrEmpty);
            string[] paths = new string[roots.Count];
            roots.CopyTo(paths);
            if (paths.Length == 0) return string.Empty;
            HashSet<string> dependencies = new HashSet<string>(AssetDatabase.GetDependencies(paths, true), StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:TextStudioFontAppearanceData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!dependencies.Contains(path)) continue;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not TextStudioFontAppearanceData data || ValidateAsset(data, out string error)) continue;
                    return "[TextStudio] Prepared font '" + path + "' cannot be included in this build. " + error;
                }
            }
            return string.Empty;
        }

        public static bool ValidateAsset(TextStudioFontAppearanceData data, out string error)
        {
            if (data == null) { error = "A prepared font asset is missing."; return false; }
            if (!data.ValidateSource(out error)) return false;
            if (data.Font is not TMP_FontAsset font) return true;
            if (data.FaceIndex != TextStudioFontAppearancePreparation.ResolveFaceIndex(font))
            { error = "The native font uses a different collection face. Prepare its appearance data again."; return false; }
            string source = TextStudioFontAppearancePreparation.ResolveSourcePath(font);
            // Embedded bytes are sufficient for deployment after the original source file is removed.
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return true;
            try
            {
                if (TextStudioFontAppearancePreparation.Hash(File.ReadAllBytes(source)) == data.SourceHash) return true;
                error = "The native font source has changed. Prepare its appearance data again before building.";
            }
            catch (IOException exception) { error = "The font source cannot be checked: " + exception.Message; }
            catch (UnauthorizedAccessException exception) { error = "The font source cannot be checked: " + exception.Message; }
            return false;
        }
    }
}
