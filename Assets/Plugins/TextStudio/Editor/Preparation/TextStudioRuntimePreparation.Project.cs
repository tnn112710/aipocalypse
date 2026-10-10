using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    public static partial class TextStudioRuntimePreparation
    {
        static readonly HashSet<Scene> s_preparationPreviews = new HashSet<Scene>();
        internal static bool IsPreparationPreview(Scene scene) => s_preparationPreviews.Contains(scene);

        // The overview treats open scenes as live content and reads the remaining configured player content without saving it.
        internal static bool CheckBuildContent(ISet<string> paths = null) => StartProject(false, Array.Empty<TextStudioText>(), true, paths);

        /// <summary>Prepares open texts, then saved build content. Null paths visit all configured player content outside open scenes.</summary>
        internal static bool PrepareProject(IEnumerable<TextStudioText> openTexts, bool includeBuild, ISet<string> paths = null) =>
            StartProject(true, openTexts ?? Array.Empty<TextStudioText>(), includeBuild, paths);

        static bool StartProject(bool prepare, IEnumerable<TextStudioText> openTexts, bool includeBuild, ISet<string> paths)
        {
            if (IsBusy) return false;
            s_automaticOperation = false;
            LastReport = new TextStudioPreparationReport { IsAnalysis = !prepare, IsComplete = false };
            s_operation = VisitProject(prepare, openTexts.Where(text => text != null).Distinct().ToArray(), includeBuild, paths, LastReport);
            return true;
        }

        static IEnumerator VisitProject(bool prepare, TextStudioText[] openTexts, bool includeBuild, ISet<string> only, TextStudioPreparationReport report)
        {
            if (prepare && openTexts.Length > 0)
            {
                IEnumerator work = PrepareTexts(openTexts, EditorUserBuildSettings.activeBuildTarget, report);
                using (work as IDisposable) while (work.MoveNext()) yield return null;
            }
            if (includeBuild)
            {
                string[] paths = BuildContentPaths();
                if (only != null) paths = paths.Where(only.Contains).ToArray();
                IEnumerator visit = VisitBuildPaths(paths, prepare, report);
                using (visit as IDisposable) while (visit.MoveNext()) yield return null;
            }
            report.IsComplete = true;
            report.OperationLabel = string.Empty;
        }

        /// <summary>Saved player content: enabled build scenes, Resources and preloaded assets with their dependencies, excluding open scenes.</summary>
        internal static string[] BuildContentPaths()
        {
            string[] roots = TextStudioRuntimePreparationBuildProcessor.PlayerRoots(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
            if (roots.Length == 0) return Array.Empty<string>();
            HashSet<string> open = OpenScenePaths();
            return AssetDatabase.GetDependencies(roots, true).Where(path => !open.Contains(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        internal static HashSet<string> OpenScenePaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path)) paths.Add(scene.path);
            }
            return paths;
        }

        internal static string[] ExistingAssets(IEnumerable<string> paths) =>
            paths.Where(path => !string.IsNullOrEmpty(path) && AssetDatabase.GetMainAssetTypeAtPath(path) != null).Distinct().ToArray();

        internal static IEnumerator VisitBuildPaths(string[] paths, bool prepare, TextStudioPreparationReport report)
        {
            string textScript = FindTextScript();
            foreach (string path in paths)
            {
                bool isScene = path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
                bool isPrefab = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                if (!isScene && !isPrefab) continue;
                if (!AssetDatabase.GetDependencies(path, true).Contains(textScript)) continue;
                report.OperationLabel = (prepare ? "Preparing " : "Checking ") + path;
                yield return null;
                Scene scene = default;
                Scene ownedScene = default;
                GameObject prefab = null;
                try
                {
                    if (isScene) scene = EditorSceneManager.OpenPreviewScene(path);
                    else prefab = PrefabUtility.LoadPrefabContents(path);
                    ownedScene = isScene ? scene : prefab.scene;
                    s_preparationPreviews.Add(ownedScene);
                    TextStudioText[] texts = (isScene ? scene.GetRootGameObjects() : new[] { prefab })
                        .SelectMany(root => root.GetComponentsInChildren<TextStudioText>(true)).ToArray();
                    string before = prepare && prefab != null ? string.Join("", texts.Select(text => EditorJsonUtility.ToJson(text))) : null;
                    int firstItem = report.Items.Count;
                    if (prepare)
                    {
                        IEnumerator work = PrepareTexts(texts, EditorUserBuildSettings.activeBuildTarget, report);
                        using (work as IDisposable)
                            while (work.MoveNext())
                            {
                                for (int i = firstItem; i < report.Items.Count; i++) report.Items[i].SourcePath = path;
                                yield return null;
                            }
                    }
                    else foreach (TextStudioPreparationItem item in Analyze(texts).Items) report.Add(item);
                    for (int i = firstItem; i < report.Items.Count; i++) report.Items[i].SourcePath = path;
                    if (prepare && prefab != null)
                    {
                        foreach (TextStudioText text in texts) TextStudioRuntimePreparationBuildProcessor.RemoveStaleOptionalBindings(text);
                        if (before != string.Join("", texts.Select(text => EditorJsonUtility.ToJson(text)))) PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    }
                }
                finally
                {
                    s_preparationPreviews.Remove(ownedScene);
                    if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
                    if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            report.IsComplete = true;
            report.OperationLabel = string.Empty;
        }

        internal static string FindTextScript()
        {
            foreach (string guid in AssetDatabase.FindAssets("TextStudioText t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(TextStudioText)) return path;
            }
            throw new InvalidOperationException("Text Studio could not locate its text component script. Reimport Text Studio before checking build content.");
        }
    }
}
