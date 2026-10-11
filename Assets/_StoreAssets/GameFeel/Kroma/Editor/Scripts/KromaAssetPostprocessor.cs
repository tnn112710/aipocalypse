using UnityEngine;
using UnityEditor;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Monitors asset changes to automatically refresh the Kroma Browser.
    /// Triggers when KromaProfiles are created, imported, moved, or deleted.
    /// </summary>
    public class KromaAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            bool needsRefresh = false;

            // Check imports and moves
            foreach (string str in importedAssets)
            {
                if (IsKromaProfile(str))
                {
                    needsRefresh = true;
                    break;
                }
            }
            if (!needsRefresh)
            {
                foreach (string str in movedAssets)
                {
                    if (IsKromaProfile(str))
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }
            if (!needsRefresh)
            {
                foreach (string str in deletedAssets)
                {
                    if (IsKromaProfile(str))
                    {
                        needsRefresh = true;
                        break;
                    }
                }
            }

            if (needsRefresh)
            {
                // Delay refresh by one frame to ensure asset is fully initialized/serialized
                EditorApplication.delayCall += RefreshBrowser;
            }
        }

        private static bool IsKromaProfile(string path)
        {
            // Simple extension check first to avoid loading unrelated assets
            if (!path.EndsWith(".asset")) return false;

            // Load to confirm type
            KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
            return profile != null;
        }

        private static void RefreshBrowser()
        {
            // Only refresh if the window is already open.
            // GetWindow() would force it open, which is annoying.
            var windows = Resources.FindObjectsOfTypeAll<KromaProfileBrowser>();
            if (windows != null && windows.Length > 0)
            {
                foreach (var window in windows)
                {
                    window.InitializeBrowser();
                    window.Repaint();

                    // Auto-fallback: if the active profile was deleted, switch to the first in the list
                    var logic = window.Logic;
                    if (logic == null) continue;

                    var vol = logic.GetTargetVolume();
                    if (vol != null && vol.sharedProfile == null && logic.Profiles.Count > 0)
                    {
                        var fallback = logic.Profiles[0];
                        logic.ApplyToSelected(fallback, window);
                        Debug.Log($"Kroma: Active profile was deleted. Switched to: {fallback.name}");
                    }
                }
            }
        }
    }
}
