using System;
using System.Collections.Generic;
using UnityEditor;

namespace TextStudio.Editor
{
    /// <summary>
    /// Activates the canonical Text Studio project settings asset in edit mode and keeps its
    /// project-owned catalog tier synchronized with relevant asset changes.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioProjectSettingsLoader
    {
        static HashSet<string> s_trackedPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool s_activationScheduled;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            ScheduleActivation();
        }

        internal static void NotifyAssetsChanged(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (HasRelevantChange(importedAssets) ||
                HasRelevantChange(deletedAssets) ||
                HasRelevantChange(movedAssets) ||
                HasRelevantChange(movedFromAssetPaths))
            {
                ScheduleActivation();
            }
        }

        static void ScheduleActivation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            TextStudioRegistry.BeginProjectCatalogActivation();
            if (s_activationScheduled)
                return;

            s_activationScheduled = true;
            EditorApplication.delayCall += Activate;
        }

        static void Activate()
        {
            ActivateNow();
        }

        internal static void ActivateNow()
        {
            if (s_activationScheduled)
            {
                EditorApplication.delayCall -= Activate;
                s_activationScheduled = false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            TextStudioProjectSettingsEditorUtility.RefreshProjectCatalog();
            RefreshTrackedPaths();
        }

        static bool HasRelevantChange(string[] paths)
        {
            if (paths == null)
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                    continue;
                if (IsTrackedPath(path))
                    return true;
            }
            return false;
        }

        internal static void RefreshTrackedPaths()
        {
            s_trackedPaths =
                TextStudioProjectSettingsEditorUtility.GetCanonicalDependencyPaths();
        }

        internal static bool IsTrackedPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                (string.Equals(
                     path,
                     TextStudioProjectSettings.CanonicalAssetPath,
                     StringComparison.OrdinalIgnoreCase) ||
                 s_trackedPaths.Contains(path));
        }

        static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleActivation();
        }
    }

    internal class TextStudioProjectSettingsPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            TextStudioProjectSettingsLoader.NotifyAssetsChanged(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
