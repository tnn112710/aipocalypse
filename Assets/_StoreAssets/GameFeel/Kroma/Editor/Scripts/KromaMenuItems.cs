using UnityEngine;
using UnityEditor;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Provides menu items for creating Kroma-specific assets.
    /// </summary>
    public static class KromaMenuItems
    {
        [MenuItem("Tools/Kroma/Create Skybox", false, 12)]
        public static void CreateKromaSkyboxFromMenu()
        {
            string folderPath = Path.Combine(KromaAssetManager.GetContentFolder(), "Skybox").Replace("\\", "/");
            KromaCreateSkyboxWindow.ShowPrompt(folderPath);
        }

        [MenuItem("Assets/Create/Kroma/Kroma Skybox", false, 2)]
        public static void CreateKromaSkyboxFromContext()
        {
            // Context behavior: Target selected folder
            string folderPath = "Assets";
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                {
                    path = Path.GetDirectoryName(path);
                }
                folderPath = path.Replace("\\", "/");
            }
            else
            {
                folderPath = KromaAssetManager.GetCreatedDataFolder();
            }

            KromaCreateSkyboxWindow.ShowPrompt(folderPath);
        }
    }
}
