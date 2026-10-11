using UnityEngine;
using UnityEditor;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    public class KromaProfileCreator
    {
        // Map preset enum to base profile asset names
        private static readonly string[] PresetSourceNames = new string[]
        {
            null, // Default — no source, create blank
            "Kroma_Profile_01_AzureNoon",
            "Kroma_Profile_02_StarryNight",
            "Kroma_Profile_03_RainyDay",
            "Kroma_Profile_27_GoldenHour"
        };

        [MenuItem("Tools/Kroma/Create Profile", false, 11)]
        public static void CreateProfile()
        {
            // Default behavior: Target 'Created/Data'
            string folderPath = KromaAssetManager.GetCreatedDataFolder();
            CreateProfileAtPath(folderPath);
        }

        [MenuItem("Assets/Create/Kroma/Kroma Profile", false, 1)]
        public static void CreateProfileFromContext()
        {
            // Context behavior: Target selected folder
            string path = "Assets";
            if (Selection.activeObject != null)
            {
                path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path))
                {
                    path = Path.GetDirectoryName(path);
                }
            }
            CreateProfileAtPath(path);
        }

        public static void CreateProfileAtPath(string folderPath)
        {
            // Instead of immediate creation, show the prompt
            KromaCreateWindow.ShowPrompt(folderPath);
        }

        public static int GetNextProfileNumber(string folderPath)
        {
            int nextNumber = 1;
            
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:KromaProfile", new[] { folderPath });
                string prefix = "Kroma_MyProfile_";
                
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    
                    if (fileName.StartsWith(prefix))
                    {
                        string[] parts = fileName.Split('_');
                        // Expecting Kroma_MyProfile_XX_Name
                        // parts: [0]="Kroma", [1]="MyProfile", [2]="XX", [3]="Name"
                        if (parts.Length >= 3)
                        {
                            if (int.TryParse(parts[2], out int num))
                            {
                                if (num >= nextNumber) nextNumber = num + 1;
                            }
                        }
                    }
                }
            }
            return nextNumber;
        }

        public static void CreateProfileAsset(string folderPath, string chosenName, bool applyImmediately = true, KromaProfilePreset preset = KromaProfilePreset.Default)
        {
            if (preset != KromaProfilePreset.Default)
            {
                CreateProfileFromPreset(folderPath, chosenName, applyImmediately, preset);
                return;
            }

            // --- Original Default creation logic ---
            // Ensure folder exists (handles parent creation recursively)
            KromaAssetManager.EnsurePathExists(folderPath);
            
            int nextNumber = GetNextProfileNumber(folderPath);
            string safeName = chosenName.Replace(" ", ""); // No spaces in file names
            string baseFilename = $"Kroma_MyProfile_{nextNumber:D2}_{safeName}";
            
            string baseNameWithExt = $"{baseFilename}.asset";
            string fullPath = $"{folderPath}/{baseNameWithExt}";
            
            // Generate unique just in case of collision
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            KromaProfile newProfile = ScriptableObject.CreateInstance<KromaProfile>();
            newProfile.profileName = chosenName; // Keep the core name internally
            
            // Assign Default Thumbnail
            string[] thumbGuids = AssetDatabase.FindAssets(KromaConstants.DEFAULT_THUMBNAIL_NAME);
            if (thumbGuids.Length > 0)
            {
                string thumbPath = AssetDatabase.GUIDToAssetPath(thumbGuids[0]);
                newProfile.thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbPath);
            }
            
            AssetDatabase.CreateAsset(newProfile, fullPath);

             // Create VolumeProfile sub-asset
             UnityEngine.Rendering.VolumeProfile volProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
             volProfile.name = $"{baseFilename}_Volume";
             AssetDatabase.AddObjectToAsset(volProfile, newProfile);
             newProfile.volumeProfile = volProfile;
             
             // Create Default Skybox Material sub-asset
             string skyboxTemplatePath = $"{KromaAssetManager.GetKromaRootPath()}/Editor/Resources/Profiles/Kroma_Skybox.mat";
             Material templateSkybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxTemplatePath);
             
             if (templateSkybox != null)
             {
                 Material newSkybox = new Material(templateSkybox);
                 newSkybox.name = $"{baseFilename}_Skybox";
                 AssetDatabase.AddObjectToAsset(newSkybox, newProfile);
                 newProfile.skyboxMaterial = newSkybox;
             }
             else
             {
                 Debug.LogWarning($"[Kroma] Default skybox material not found at {skyboxTemplatePath}. Skybox will be left empty on new profile.");
             }
             
             EditorUtility.SetDirty(newProfile);
             AssetDatabase.SaveAssets();
             
             if (applyImmediately)
             {
                 ApplyProfileFully(newProfile);
             }
             
             FinalizeNewProfile(newProfile);
        }

        private static void CreateProfileFromPreset(string folderPath, string chosenName, bool applyImmediately, KromaProfilePreset preset)
        {
            // 1. Find the source base profile
            string sourceAssetName = PresetSourceNames[(int)preset];
            KromaProfile sourceProfile = FindBaseProfile(sourceAssetName);

            if (sourceProfile == null)
            {
                Debug.LogWarning($"[Kroma] Preset source profile '{sourceAssetName}' not found. Falling back to Default creation.");
                CreateProfileAsset(folderPath, chosenName, applyImmediately, KromaProfilePreset.Default);
                return;
            }

            // 2. Ensure target folder exists
            KromaAssetManager.EnsurePathExists(folderPath);

            // 3. Build filename
            int nextNumber = GetNextProfileNumber(folderPath);
            string safeName = chosenName.Replace(" ", "");
            string baseFilename = $"Kroma_MyProfile_{nextNumber:D2}_{safeName}";
            string fullPath = $"{folderPath}/{baseFilename}.asset";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

            // 4. Create KromaProfile clone (Instantiate creates a deep copy of the SO fields)
            KromaProfile newProfile = Object.Instantiate(sourceProfile);
            newProfile.name = baseFilename;
            newProfile.profileName = chosenName;

            AssetDatabase.CreateAsset(newProfile, fullPath);

            // 5. Deep clone VolumeProfile + components as sub-assets
            if (sourceProfile.volumeProfile != null)
            {
                UnityEngine.Rendering.VolumeProfile newVol = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                newVol.name = $"{baseFilename}_Volume";
                AssetDatabase.AddObjectToAsset(newVol, newProfile);

                foreach (var comp in sourceProfile.volumeProfile.components)
                {
                    if (comp != null)
                    {
                        var newComp = Object.Instantiate(comp);
                        newComp.name = comp.name;
                        newComp.hideFlags = HideFlags.HideInHierarchy;
                        newVol.components.Add(newComp);
                        AssetDatabase.AddObjectToAsset(newComp, newVol);
                    }
                }

                newProfile.volumeProfile = newVol;
            }

            // 6. Deep clone Skybox Material as sub-asset
            if (sourceProfile.skyboxMaterial != null)
            {
                Material newSkybox = new Material(sourceProfile.skyboxMaterial);
                newSkybox.name = $"{baseFilename}_Skybox";
                AssetDatabase.AddObjectToAsset(newSkybox, newProfile);
                newProfile.skyboxMaterial = newSkybox;
            }

            EditorUtility.SetDirty(newProfile);
            AssetDatabase.SaveAssets();

            if (applyImmediately)
            {
                ApplyProfileFully(newProfile);
            }

            FinalizeNewProfile(newProfile);
        }

        private static KromaProfile FindBaseProfile(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:KromaProfile {assetName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == assetName)
                {
                    return AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
                }
            }
            return null;
        }

        private static void ApplyProfileFully(KromaProfile profile)
        {
            if (profile == null) return;

            // 1. Try to find an open Kroma Browser window to leverage its initialized logic state
            KromaProfileBrowser[] browsers = Resources.FindObjectsOfTypeAll<KromaProfileBrowser>();
            if (browsers.Length > 0 && browsers[0] != null && browsers[0].Logic != null)
            {
                browsers[0].Logic.ApplyToSelected(profile, browsers[0]);
            }
            else
            {
                // 2. If not open, instantiate a temporary logic, refresh it to populate, and apply
                KromaBrowserLogic tempLogic = new KromaBrowserLogic();
                tempLogic.RefreshProfileList();
                tempLogic.ApplyToSelected(profile, null);
            }
        }

        private static void FinalizeNewProfile(KromaProfile newProfile)
        {
            // Switch browser to My Profiles and focus it (Commented out to prevent overwhelming window opens)
            /*
            KromaProfileBrowser browser = EditorWindow.GetWindow<KromaProfileBrowser>("Kroma Browser");
            if (browser != null && browser.Logic != null)
            {
                browser.Logic.CurrentSourceFilter = ProfileSourceFilter.MyProfiles;
                browser.Logic.RefreshProfileList();
                browser.Logic.ResetSelection();
                
                // Force highlight
                int idx = browser.Logic.FilteredProfiles.IndexOf(newProfile);
                if (idx >= 0)
                {
                    browser.Logic.SelectedProfileIndex = idx;
                }
                
                browser.Repaint();
            }
            */
            
            // Focus and highlight
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newProfile;
            EditorGUIUtility.PingObject(newProfile);
        }

        public static void CloneProfile(KromaProfile source, string newName)
        {
            // ALWAYS save clones to the 'Created/Data' folder, regardless of source location
            string folderPath = KromaAssetManager.GetCreatedDataFolder();
            
            // Ensure path exists
            KromaAssetManager.EnsurePathExists(folderPath);
            
            // 1. Create KromaProfile Clone
            KromaProfile newProfile = Object.Instantiate(source);
            newProfile.name = newName;
            newProfile.profileName = newName; // Update internal display name too
            
            // Path for new profile
            string profilePath = $"{folderPath}/{newName}.asset";
            profilePath = AssetDatabase.GenerateUniqueAssetPath(profilePath);
            
            AssetDatabase.CreateAsset(newProfile, profilePath);
            
            // 2. Deep Clone Volume Profile
            if (source.volumeProfile != null)
            {
                UnityEngine.Rendering.VolumeProfile newVol = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                newVol.name = newName + "_Volume";

                // Add as Sub-Asset (Cleaner) first so we can add components to it
                AssetDatabase.AddObjectToAsset(newVol, newProfile);

                // Deep clone components safely
                foreach (var comp in source.volumeProfile.components)
                {
                    if (comp != null)
                    {
                        var newComp = Object.Instantiate(comp);
                        newComp.name = comp.name;
                        newComp.hideFlags = HideFlags.HideInHierarchy;
                        newVol.components.Add(newComp);
                        AssetDatabase.AddObjectToAsset(newComp, newVol);
                    }
                }

                newProfile.volumeProfile = newVol;
            }
            
            // 3. Deep Clone Skybox Material
            if (newProfile.skyboxMaterial != null)
            {
                Material newSky = new Material(newProfile.skyboxMaterial);
                newSky.name = newName + "_Skybox";
                
                AssetDatabase.AddObjectToAsset(newSky, newProfile);
                newProfile.skyboxMaterial = newSky;
            }

            EditorUtility.SetDirty(newProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Highlight
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newProfile;
            EditorGUIUtility.PingObject(newProfile);
            
            // Apply
            ApplyProfileFully(newProfile);
        }
    }
}
