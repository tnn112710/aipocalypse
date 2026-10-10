using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioRuntimeLoadingBuildValidator :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        const string SharedProductionShaderInclude =
            "Runtime/Resources/TextStudio2DProductionSDFCommon.cginc";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string error = ValidateRuntimeLoadingContract();
            if (!string.IsNullOrEmpty(error))
                throw new BuildFailedException(error);
            error = TextStudioFontAppearanceBuildValidation.ValidatePlayerAssets();
            if (!string.IsNullOrEmpty(error))
                throw new BuildFailedException(error);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            TextStudioProjectSettings settings =
                TextStudioProjectSettingsEditorUtility.FindCanonical();
            if (settings == null)
                return;

            string[] dependencies = AssetDatabase.GetDependencies(
                TextStudioProjectSettings.CanonicalAssetPath,
                true);
            long sourceBytes = 0L;
            for (int i = 0; i < dependencies.Length; i++)
            {
                string fullPath = Path.GetFullPath(dependencies[i]);
                if (File.Exists(fullPath))
                    sourceBytes += new FileInfo(fullPath).Length;
            }

            Debug.Log(
                "[TextStudio] Runtime settings build graph: " +
                dependencies.Length + " assets, " + sourceBytes +
                " source bytes, " + CountCatalogEntries(settings) +
                " catalog entries. Player output size: " +
                report.summary.totalSize + " bytes.");
        }

        internal static string ValidateRuntimeLoadingContract()
        {
            List<TextStudioProjectSettings> settings =
                TextStudioProjectSettingsEditorUtility.FindAll();
            TextStudioProjectSettings canonical =
                TextStudioProjectSettingsEditorUtility.FindCanonical();

            if (settings.Count > 1)
            {
                return "[TextStudio] Player build stopped because multiple Text Studio Project " +
                    "Settings assets exist. Keep one asset at " +
                    TextStudioProjectSettings.CanonicalAssetPath + ".";
            }

            if (settings.Count == 1 && canonical == null)
            {
                return "[TextStudio] Player build stopped because Text Studio Project Settings " +
                    "cannot be loaded at runtime. Move the asset to " +
                    TextStudioProjectSettings.CanonicalAssetPath + ".";
            }

            int matchingResourceKeys = CountMatchingResourceKeys(
                TextStudioProjectSettings.ResourcePath);
            if (matchingResourceKeys > 1)
            {
                return "[TextStudio] Player build stopped because more than one asset uses " +
                    "Resources key '" + TextStudioProjectSettings.ResourcePath + "'.";
            }

            string[] shaderResourceKeys =
            {
                TextStudio2DResourceKeys.ProductionShader,
                TextStudio2DResourceKeys.OverlayShader,
                TextStudio2DResourceKeys.FieldShader,
                TextStudio2DResourceKeys.FieldCanvasShader,
                TextStudio2DResourceKeys.FieldOverlayShader,
                TextStudio2DResourceKeys.StyleDecorationShader,
                TextStudio2DResourceKeys.WritingShader,
                TextStudio2DResourceKeys.HeroLayerShader,
                TextStudio2DResourceKeys.HeroCompositeShader,
                TextStudio2DResourceKeys.HeroEffectsShader,
                TextStudio2DResourceKeys.HeroOutputShader,
                TextStudio2DResourceKeys.HeroOutputCanvasShader
            };
            for (int i = 0; i < shaderResourceKeys.Length; i++)
            {
                Shader shader = Resources.Load<Shader>(shaderResourceKeys[i]);
                if (shader == null)
                {
                    return "[TextStudio] Player build stopped because packaged 2D shader " +
                           "resource '" + shaderResourceKeys[i] + "' is missing.";
                }
                if (!TextStudio2DShaderResources.IsUsable(shader) ||
                    ShaderUtil.ShaderHasError(shader))
                {
                    return "[TextStudio] Player build stopped because packaged 2D shader " +
                           "resource '" + shaderResourceKeys[i] +
                           "' does not compile for the active graphics device.";
                }
            }

            string[] packagedResourceKeys =
            {
                TextStudio2DResourceKeys.ProductionShader,
                TextStudio2DResourceKeys.OverlayShader,
                TextStudio2DResourceKeys.FieldShader,
                TextStudio2DResourceKeys.FieldCanvasShader,
                TextStudio2DResourceKeys.FieldOverlayShader,
                TextStudio2DResourceKeys.StyleDecorationShader,
                TextStudio2DResourceKeys.WritingShader,
                TextStudio2DResourceKeys.HeroLayerShader,
                TextStudio2DResourceKeys.HeroCompositeShader,
                TextStudio2DResourceKeys.HeroEffectsShader,
                TextStudio2DResourceKeys.HeroOutputShader,
                TextStudio2DResourceKeys.HeroOutputCanvasShader
            };
            for (int i = 0; i < packagedResourceKeys.Length; i++)
            {
                if (CountMatchingResourceKeys(packagedResourceKeys[i]) != 1)
                {
                    return "[TextStudio] Player build stopped because packaged resource key '" +
                        packagedResourceKeys[i] +
                        "' is missing or duplicated.";
                }
            }

            string sharedIncludeAssetPath = ResolvePackagedAssetPath(
                SharedProductionShaderInclude);
            if (string.IsNullOrEmpty(sharedIncludeAssetPath) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    sharedIncludeAssetPath) == null)
            {
                return "[TextStudio] Player build stopped because the shared production shader " +
                    "include is missing.";
            }

            if (canonical == null)
                return string.Empty;

            string duplicateError = FindDuplicateCatalogEntry(canonical);
            return duplicateError ?? string.Empty;
        }

        internal static string ResolvePackagedAssetPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string packageRoot =
                TextStudioPackagePaths.FindFullRoot(
                    TextStudioPackagePaths.BasePackageName);
            if (string.IsNullOrEmpty(packageRoot))
                return null;

            string fullPath = Path.GetFullPath(
                Path.Combine(packageRoot, relativePath));
            string assetPath = FileUtil.GetProjectRelativePath(
                fullPath.Replace('\\', '/'));
            return string.IsNullOrEmpty(assetPath)
                ? null
                : assetPath.Replace('\\', '/');
        }

        internal static int CountMatchingResourceKeys(string resourceKey)
        {
            int count = 0;
            string[] paths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                int resourcesIndex = path.LastIndexOf(
                    "/Resources/",
                    StringComparison.OrdinalIgnoreCase);
                if (resourcesIndex < 0)
                    continue;

                string candidate = path.Substring(
                    resourcesIndex + "/Resources/".Length);
                int extensionIndex = candidate.LastIndexOf('.');
                int slashIndex = candidate.LastIndexOf('/');
                if (extensionIndex > slashIndex)
                    candidate = candidate.Substring(0, extensionIndex);
                if (string.Equals(
                        candidate,
                        resourceKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
            return count;
        }

        internal static string FindDuplicateCatalogEntry(
            TextStudioProjectSettings settings)
        {
            string styleError = TextStudioStyleCatalog.FindError(settings.appearanceStyleCatalog, style => style.Id, "2D style catalog");
            if (!string.IsNullOrEmpty(styleError)) return "[TextStudio] Player build stopped because " + styleError;

            string duplicate = FindDuplicate(
                settings.effectCatalog,
                effect => effect != null ? effect.Id : string.Empty);
            if (!string.IsNullOrEmpty(duplicate))
                return DuplicateMessage("effect", duplicate);

            duplicate = FindDuplicate(
                settings.actionCatalog,
                action => action != null ? action.Id : string.Empty);
            if (!string.IsNullOrEmpty(duplicate))
                return DuplicateMessage("action", duplicate);

            duplicate = FindDuplicate(
                settings.curveCatalog,
                curve => curve != null ? curve.name : string.Empty);
            if (!string.IsNullOrEmpty(duplicate))
                return DuplicateMessage("curve", duplicate);

            duplicate = FindDuplicate(
                settings.playbackCatalog,
                playback => playback != null ? playback.name : string.Empty);
            if (!string.IsNullOrEmpty(duplicate))
                return DuplicateMessage("playback", duplicate);

            HashSet<string> expansionIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (settings.tagExpansionSheets == null)
                return null;

            for (int sheetIndex = 0;
                sheetIndex < settings.tagExpansionSheets.Count;
                sheetIndex++)
            {
                TextStudioTagExpansionSheet sheet =
                    settings.tagExpansionSheets[sheetIndex];
                if (sheet == null || sheet.Entries == null)
                    continue;

                IReadOnlyList<TextStudioTagExpansionEntry> entries =
                    sheet.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    TextStudioTagExpansionEntry entry = entries[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.TagId))
                        continue;
                    string id = entry.TagId.Trim();
                    if (!expansionIds.Add(id))
                        return DuplicateMessage("tag expansion", id);
                }
            }
            return null;
        }

        static string FindDuplicate<T>(
            IReadOnlyList<T> entries,
            Func<T, string> keySelector)
            where T : class
        {
            if (entries == null)
                return null;

            HashSet<string> ids =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null)
                    continue;
                string id = keySelector(entry);
                if (!string.IsNullOrWhiteSpace(id) && !ids.Add(id))
                    return id;
            }
            return null;
        }

        static string DuplicateMessage(string kind, string id)
        {
            return "[TextStudio] Player build stopped because project settings contain " +
                "duplicate " + kind + " id '" + id +
                "'. Keep one catalog entry for each id.";
        }

        static int CountCatalogEntries(TextStudioProjectSettings settings)
        {
            return Count(settings.appearanceStyleCatalog) +
                Count(settings.effectCatalog) +
                Count(settings.actionCatalog) +
                Count(settings.curveCatalog) +
                Count(settings.playbackCatalog) +
                Count(settings.tagExpansionSheets);
        }

        static int Count<T>(IReadOnlyCollection<T> items)
        {
            return items != null ? items.Count : 0;
        }
    }
}
