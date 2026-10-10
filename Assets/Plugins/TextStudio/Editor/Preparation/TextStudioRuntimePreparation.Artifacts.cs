using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace TextStudio.Editor
{
    public static partial class TextStudioRuntimePreparation
    {
        internal static event Action<ISet<string>, TextStudioPreparationReport> AnalyzeAddonAssets;

        internal static TextStudioPreparationReport AnalyzeSavedAssets()
        {
            string[] roots = TextStudioRuntimePreparationBuildProcessor.PlayerRoots(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
            HashSet<string> referenced = new HashSet<string>(roots.Length > 0 ? AssetDatabase.GetDependencies(roots, true) : Array.Empty<string>(), StringComparer.Ordinal);
            TextStudioPreparationReport report = new TextStudioPreparationReport();
            foreach (string guid in AssetDatabase.FindAssets("t:TextStudioFontAppearanceData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextStudioFontAppearanceData data = AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(path);
                bool valid = TextStudioFontAppearanceBuildValidation.ValidateAsset(data, out string error);
                AddAssetStatus(report, referenced, path, "2D Font Data", valid, error, presentation: TextStudioAppearancePreparationProvider.Display);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:TextStudioColorAppearanceData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextStudioColorAppearanceData data = AssetDatabase.LoadAssetAtPath<TextStudioColorAppearanceData>(path);
                bool valid = TextStudioColorAppearanceBuildValidation.ValidateAsset(data, out string error);
                AddAssetStatus(report, referenced, path, "Inline Image Data", valid, error, presentation: TextStudioImagePreparationProvider.Display);
            }
            AnalyzeAddonAssets?.Invoke(referenced, report);
            return report;
        }

        internal static void AddAssetStatus(TextStudioPreparationReport report, ISet<string> referenced, string path,
            string provider, bool valid, string error, string detail = "", TextStudioPreparationPresentation presentation = null)
        {
            bool included = referenced.Contains(path);
            string message = valid ? "Saved data is valid. Text-specific coverage is checked during preparation." : error;
            if (!included) message = "Not referenced by configured player scenes, Resources, or preloaded assets. Automatic binding or custom content may still use it. " + message;
            if (!string.IsNullOrEmpty(detail)) message += " " + detail;
            report.Add(new TextStudioPreparationItem(provider, path,
                valid ? TextStudioPreparationState.Ready : TextStudioPreparationState.NeedsAttention,
                message, included)
            {
                Presentation = presentation,
                BuildReference = included ? TextStudioPreparationReference.Found : TextStudioPreparationReference.NotFound,
                SourcePath = path,
                Summary = valid ? "" : "Saved data needs updating."
            });
        }
    }
}
