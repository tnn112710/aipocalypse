using UnityEditor;
using UnityEngine;

namespace FayLabs.Kroma.Editor
{
    [InitializeOnLoad]
    public static class KromaStartup
    {
        private static string GetPrefKey()
        {
            // Use the unique product GUID to ensure this is tracked exactly once per project
            return $"Kroma_HasOpened_{PlayerSettings.productGUID}";
        }

        static KromaStartup()
        {
            // delayCall ensures the editor is fully loaded before we try to open windows
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            string prefKey = GetPrefKey();

            if (!EditorPrefs.GetBool(prefKey, false))
            {
                EditorPrefs.SetBool(prefKey, true);
                KromaProfileBrowser.ShowWindow();
            }
        }

        // [MenuItem("Tools/Kroma/Dev/Reset Startup Flag", false, 100)]
        private static void ResetStartupFlag()
        {
            EditorPrefs.DeleteKey(GetPrefKey());
            UnityEngine.Debug.Log("[Kroma] Startup flag reset. The browser will open automatically on the next assembly reload.");
        }
    }
}
