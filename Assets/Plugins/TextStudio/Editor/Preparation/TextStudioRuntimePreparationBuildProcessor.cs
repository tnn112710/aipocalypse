using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Build callbacks and build-root tracking have their own editor lifecycle.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioRuntimePreparationBuildProcessor : BuildPlayerProcessor, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        internal static string[] CurrentRoots;
        internal static event Action<TextStudioText> BindPreparedAddons;
        public override int callbackOrder => -1000;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            CurrentRoots = PlayerRoots(context.BuildPlayerOptions.scenes);
            EditorApplication.delayCall += ClearFinishedBuild;
            if (TextStudioRuntimePreparationSettings.instance.automaticInBuilds)
            {
                TextStudioPreparationReport report = TextStudioRuntimePreparation.PrepareAssets(CurrentRoots, context.BuildPlayerOptions.target);
                if (!report.Succeeded) throw new BuildFailedException("Text Studio runtime preparation needs attention:\n" +
                    string.Join("\n", report.Items.Where(item => item.Required && item.State == TextStudioPreparationState.NeedsAttention)
                        .Select(item => item.Target + " (" + item.Provider + "): " + item.Message)));
            }
            // Scene references are checked after their generated bindings have been applied.
            string[] assetRoots = CurrentRoots.Where(path => !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)).ToArray();
            string error = TextStudioFontAppearanceBuildValidation.ValidateAssets(assetRoots);
            if (error.Length == 0) error = TextStudioColorAppearanceBuildValidation.ValidateAssets(assetRoots);
            if (error.Length > 0) throw new BuildFailedException(error);
        }

        internal static string[] PlayerRoots(IEnumerable<string> scenes)
        {
            HashSet<string> roots = new HashSet<string>(scenes ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (UnityEngine.Object asset in PlayerSettings.GetPreloadedAssets()) if (asset != null) roots.Add(AssetDatabase.GetAssetPath(asset));
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (TextStudioFontAppearanceBuildValidation.IsPlayerResource(path) && !AssetDatabase.IsValidFolder(path)) roots.Add(path);
            roots.RemoveWhere(string.IsNullOrEmpty);
            return roots.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            BindSceneCopy(scene);
        }

        internal static void BindSceneCopy(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TextStudioText text in root.GetComponentsInChildren<TextStudioText>(true))
            {
                if (TextStudioAppearancePreparationProvider.NeedsSources(text))
                    foreach (TextStudioFontAppearanceRequest request in TextStudioAppearancePreparationProvider.Requests(text))
                    {
                        TextStudioFontAppearanceData data = AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(request.OutputPath);
                        if (!TextStudioAppearancePreparationProvider.Covers(data, request, TextStudioAppearancePreparationProvider.NeedsFields(text)))
                            throw new BuildFailedException("Text Studio appearance coverage changed after preparation for '" + text.name + "'. Prepare its runtime data before building.");
                        // Only the build's scene copy changes; authoring scenes and their dirty state are untouched.
                        text.SetAppearanceFont(data);
                    }
                BindPreparedAddons?.Invoke(text);
                RemoveStaleOptionalBindings(text);
            }
            foreach (UnityEngine.Object dependency in EditorUtility.CollectDependencies(scene.GetRootGameObjects()))
            {
                if (dependency is TextStudioFontAppearanceData font && !TextStudioFontAppearanceBuildValidation.ValidateAsset(font, out string fontError))
                    throw new BuildFailedException("Text Studio prepared font '" + font.name + "': " + fontError);
                if (dependency is TextStudioColorAppearanceData image && !TextStudioColorAppearanceBuildValidation.ValidateAsset(image, out string imageError))
                    throw new BuildFailedException("Text Studio prepared image '" + image.name + "': " + imageError);
            }
        }

        internal static void RemoveStaleOptionalBindings(TextStudioText text)
        {
            HashSet<UnityEngine.Object> needed = new HashSet<UnityEngine.Object>();
            if (TextStudioAppearancePreparationProvider.NeedsSources(text))
                foreach (TextStudioFontAppearanceRequest request in TextStudioAppearancePreparationProvider.Requests(text)) needed.Add(request.NativeFont);
            SerializedObject serialized = new SerializedObject(text);
            void RemoveInvalid(string propertyName, Func<UnityEngine.Object, bool> invalid)
            {
                SerializedProperty values = serialized.FindProperty(propertyName);
                for (int i = values.arraySize - 1; i >= 0; i--)
                {
                    UnityEngine.Object value = values.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (value != null && !invalid(value)) continue;
                    values.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    values.DeleteArrayElementAtIndex(i);
                }
            }
            RemoveInvalid("_appearanceFonts", value => value is TextStudioFontAppearanceData data &&
                !needed.Contains(data.Font) && !TextStudioFontAppearanceBuildValidation.ValidateAsset(data, out _));
            RemoveInvalid("_appearanceImages", value => value is TextStudioColorAppearanceData data && !TextStudioColorAppearanceBuildValidation.ValidateAsset(data, out _));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public void OnPostprocessBuild(BuildReport report) => CurrentRoots = null;
        static void ClearFinishedBuild() { if (!BuildPipeline.isBuildingPlayer) CurrentRoots = null; }
    }
}
