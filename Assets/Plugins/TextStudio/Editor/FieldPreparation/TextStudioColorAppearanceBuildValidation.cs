using System;
using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    public static class TextStudioColorAppearanceBuildValidation
    {
        public static bool ValidateAsset(TextStudioColorAppearanceData data, out string error)
        {
            error = string.Empty;
            if (data == null) { error = "A prepared image asset is missing."; return false; }
            if (!data.Validate(out error)) return false;
            for (int i = 0; i < data.FieldCount; i++)
                if (!data.GetField(i).SourceContentMatches())
                { error = "An image source changed. Prepare its image silhouettes again before building."; return false; }
            return true;
        }

        /// <summary>Checks only prepared image data reached by the supplied content roots.</summary>
        public static string ValidateAssets(IEnumerable<string> rootAssetPaths)
        {
            if (rootAssetPaths == null) throw new ArgumentNullException(nameof(rootAssetPaths));
            HashSet<string> roots = new HashSet<string>(rootAssetPaths, StringComparer.Ordinal);
            roots.RemoveWhere(string.IsNullOrEmpty);
            if (roots.Count == 0) return string.Empty;
            string[] paths = new string[roots.Count]; roots.CopyTo(paths);
            foreach (string path in AssetDatabase.GetDependencies(paths, true))
            {
                TextStudioColorAppearanceData data = AssetDatabase.LoadAssetAtPath<TextStudioColorAppearanceData>(path);
                if (data != null && !ValidateAsset(data, out string error)) return "[TextStudio] Prepared image data '" + path + "' cannot be included in this build. " + error;
            }
            return string.Empty;
        }
    }
}
