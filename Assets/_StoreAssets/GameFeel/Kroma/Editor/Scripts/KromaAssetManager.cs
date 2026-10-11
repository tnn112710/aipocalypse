using UnityEngine;
using UnityEditor;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Static helper class to find the Kroma root directory dynamically.
    /// This ensures the tool works correctly even if the "Kroma" folder is moved.
    /// </summary>
    public static class KromaAssetManager
    {
        /// <summary>
        /// Returns the absolute path to the Kroma root folder (e.g., "Assets/FayLabs/Kroma").
        /// Uses this script as an anchor to find the location.
        /// </summary>
        public static string GetKromaRootPath()
        {
            // Find this script's asset
            string[] guids = AssetDatabase.FindAssets("KromaAssetManager t:Script");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("KromaAssetManager.cs"))
                {
                    // Path is .../Kroma/Editor/Scripts/KromaAssetManager.cs
                    // We want to go up 3 levels:
                    // 1. GetDir -> .../Kroma/Editor/Scripts
                    // 2. GetDir -> .../Kroma/Editor
                    // 3. GetDir -> .../Kroma
                    
                    string scriptDir = Path.GetDirectoryName(path);
                    string editorDir = Path.GetDirectoryName(scriptDir);
                    string rootDir = Path.GetDirectoryName(editorDir);
                    
                    // Normalize separators to forward slashes for Unity consistency
                    return rootDir.Replace("\\", "/");
                }
            }
            
            // Fallback if something is really weird (e.g. script renamed)
            Debug.LogError("[Kroma] Could not locate KromaAssetManager script! Defaulting to legacy path.");
            return "Assets/FayLabs/Kroma";
        }

        public static string GetContentFolder()
        {
            return Path.Combine(GetKromaRootPath(), "Content").Replace("\\", "/");
        }

        public static string GetProfilesFolder()
        {
            return Path.Combine(GetContentFolder(), "Profiles").Replace("\\", "/");
        }

        // --- BASE PROFILES (Read Only / Factory) ---
        public static string GetBaseProfilesFolder()
        {
            return Path.Combine(GetProfilesFolder(), "Base").Replace("\\", "/");
        }
        
        public static string GetBaseDataFolder()
        {
            return Path.Combine(GetBaseProfilesFolder(), "Data").Replace("\\", "/");
        }

        // --- CREATED PROFILES (User / Cloned) ---
        public static string GetCreatedProfilesFolder()
        {
            return Path.Combine(GetProfilesFolder(), "Created").Replace("\\", "/");
        }

        public static string GetCreatedDataFolder()
        {
            return Path.Combine(GetCreatedProfilesFolder(), "Data").Replace("\\", "/");
        }

        // LEGACY / COMPATIBILITY
        public static string GetDataFolder()
        {
            // Default to Base path for reading if not specified, 
            // OR maybe allow reading from both? 
            // This method was mostly used for "creating" new files in the old system.
            // For finding files, we use FindAssets which scans everything.
            return GetBaseDataFolder(); 
        }
        
        public static string GetThumbnailsFolder()
        {
             return Path.Combine(GetProfilesFolder(), "Thumbnails").Replace("\\", "/");
        }

        /// <summary>
        /// Ensures a directory exists in the AssetDatabase.
        /// Creates parent directories if needed. 
        /// </summary>
        /// <param name="folderPath">The relative asset path (e.g. Assets/Kroma/Content)</param>
        public static void EnsurePathExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            
            // If it doesn't exist, we must create it.
            // But AssetDatabase.CreateFolder requires the immediate parent to exist.
            // So we might need to recurse or walk up.
            
            string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            
            // If we reached root or empty, stop
            if (string.IsNullOrEmpty(parent)) return;
            
            // Ensure parent exists first
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsurePathExists(parent);
            }
            
            // Now create this folder
            string folderName = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
        /// <summary>
        /// Scans all VolumeProfiles in the project and removes null components, which causes Editor errors.
        /// </summary>
        // [MenuItem("Tools/Kroma/Debug/Fix Corrupted Profiles", false, 50)]
        public static void FixCorruptedProfiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile");
            int fixedCount = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Rendering.VolumeProfile profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
                
                if (profile != null)
                {
                    // Check for nulls in components list
                    if (profile.components.RemoveAll(c => c == null) > 0)
                    {
                        EditorUtility.SetDirty(profile);
                        fixedCount++;
                        Debug.Log($"[Kroma] Fixed corrupted profile: {path}");
                    }
                }
            }
            
            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Kroma", $"Fixed {fixedCount} corrupted profiles.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Kroma", "No corrupted profiles found.", "OK");
            }
        }
    }
}
