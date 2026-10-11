using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

namespace FayLabs.Kroma.Editor
{
    public class KromaProfileBundler
    {
        // [MenuItem("Tools/Kroma/Dev/Bundle All Profiles", false, 50)]
        public static void BundleAllProfiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:KromaProfile");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);

                if (profile != null)
                {
                    bool modified = BundleProfile(profile);
                    if (modified) count++;
                }
            }
            
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Bundle Complete", $"Successfully bundled {count} profiles.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Bundle Complete", "No profiles needed bundling.", "OK");
            }
        }

        private static bool BundleProfile(KromaProfile profile)
        {
            string profilePath = AssetDatabase.GetAssetPath(profile);
            bool isDirty = false;

            // Load all sub-assets currently in this profile for cleanup later
            Object[] existingAssets = AssetDatabase.LoadAllAssetsAtPath(profilePath);

            // 1. Bundle Volume Profile
            if (profile.volumeProfile != null)
            {
                string volPath = AssetDatabase.GetAssetPath(profile.volumeProfile);
                
                // Check if it's already a sub-asset of THIS profile
                if (volPath != profilePath)
                {
                    // It's external. We need to DEEP CLONE it component by component.
                    // Simply Instantiating the profile often fails to carry over the sub-asset relationships correctly when re-saving.
                    
                    VolumeProfile originalVol = profile.volumeProfile;
                    VolumeProfile newVol = ScriptableObject.CreateInstance<VolumeProfile>();
                    newVol.name = profile.name + "_Volume";
                    
                    // Add the Volume Profile itself to the main asset
                    AssetDatabase.AddObjectToAsset(newVol, profile);
                    
                    // Deep copy components
                    foreach (var component in originalVol.components)
                    {
                        if (component == null) continue;
                        
                        // Clone the component settings (Exposure, SplitToning, etc.)
                        VolumeComponent newComp = (VolumeComponent)Object.Instantiate(component);
                        newComp.name = component.name; // Keep name clean
                        
                        // FIX: Hide them from the Project View so they don't clutter the hierarchy
                        // This mimics how Unity handles them in standard Volume Profiles.
                        newComp.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;

                        // Add component to the new Volume Profile list
                        newVol.components.Add(newComp);
                        
                        // CRITICAL: Force the component to be a sub-asset of the Kroma Profile
                        // so it gets saved to disk inside the file.
                        AssetDatabase.AddObjectToAsset(newComp, profile);
                    }
                    
                    profile.volumeProfile = newVol;
                    isDirty = true;
                    // Debug.Log($"[Kroma] Bundled Volume and {newVol.components.Count} components for {profile.name}");
                }
            }

            // 2. Bundle Skybox
            if (profile.skyboxMaterial != null)
            {
                string matPath = AssetDatabase.GetAssetPath(profile.skyboxMaterial);
                
                // Check if it's already a sub-asset
                if (matPath != profilePath)
                {
                    // It's external. Clone it in!
                    Material newMat = new Material(profile.skyboxMaterial);
                    newMat.name = profile.name + "_Skybox";

                    AssetDatabase.AddObjectToAsset(newMat, profile);
                    profile.skyboxMaterial = newMat;
                    isDirty = true;
                    // Debug.Log($"[Kroma] Bundled Skybox for {profile.name}");
                }
            }

            // 3. Cleanup old sub-assets if we updated references
            if (isDirty)
            {
                System.Collections.Generic.List<Object> validComponents = new System.Collections.Generic.List<Object>();
                if (profile.volumeProfile != null)
                {
                    foreach (var c in profile.volumeProfile.components) validComponents.Add(c);
                }

                foreach (Object obj in existingAssets)
                {
                    if (obj == profile) continue; // Skip main profile
                    if (obj == null) continue;

                    // If it's a VolumeProfile and it is NOT the one currently assigned, destroy it.
                    if (obj is VolumeProfile vol && vol != profile.volumeProfile)
                    {
                        Object.DestroyImmediate(vol, true);
                    }
                    // If it's a Material and it is NOT the one currently assigned, destroy it.
                    else if (obj is Material mat && mat != profile.skyboxMaterial)
                    {
                        Object.DestroyImmediate(mat, true);
                    }
                    // If it's a VolumeComponent and it is NOT inside our active volume profile's list, destroy it.
                    else if (obj is VolumeComponent comp && !validComponents.Contains(comp))
                    {
                        Object.DestroyImmediate(comp, true);
                    }
                }
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(profile);
            }

            return isDirty;
        }

        // [MenuItem("Tools/Kroma/Dev/Use Fix Visibility", false, 51)]
        public static void FixBundleVisibility()
        {
            // ... (keep existing implementation of FixBundleVisibility, just renaming menu item for consistency if needed, but actually I'm replacing context so I should keep it or if I am appending I need careful range)
            // Wait, I am appending/replacing at end?
            // "Adding CleanupOrphans tool to KromaProfileBundler."
            // I should just ADD the new method.
            
            // Re-pasting FixBundleVisibility to ensure it stays, or if I can just append?
            // I'll replace the existing method with itself + the new method to be safe.
            
            string[] guids = AssetDatabase.FindAssets("t:KromaProfile");
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
                
                // Load all sub-assets
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                bool modified = false;

                foreach (Object obj in assets)
                {
                    if (obj is VolumeComponent comp)
                    {
                        if (comp.hideFlags != (HideFlags.HideInHierarchy | HideFlags.HideInInspector))
                        {
                            comp.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                            EditorUtility.SetDirty(comp);
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    EditorUtility.SetDirty(profile);
                    count++;
                }
            }
            
            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Fix Complete", $"Fixed visibility for {count} profiles.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Fix Complete", "No profiles needed fixing.", "OK");
            }
        }

        // [MenuItem("Tools/Kroma/Dev/Cleanup Orphans", false, 52)]
        public static void CleanupOrphans()
        {
            string[] guids = AssetDatabase.FindAssets("t:KromaProfile");
            int deletedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
                
                // key references
                VolumeProfile activeVol = profile.volumeProfile;
                Material activeSky = profile.skyboxMaterial;
                
                System.Collections.Generic.List<Object> validComponents = new System.Collections.Generic.List<Object>();
                if (activeVol != null)
                {
                    foreach(var c in activeVol.components) validComponents.Add(c);
                }

                // Load all assets
                Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                System.Collections.Generic.List<Object> toDestroy = new System.Collections.Generic.List<Object>();

                foreach (Object obj in allAssets)
                {
                    if (obj == profile) continue; // Skip main
                    if (obj == null) continue;

                    // CHECK 1: Volume Profiles
                    if (obj is VolumeProfile vol)
                    {
                        if (vol != activeVol)
                        {
                            toDestroy.Add(vol);
                        }
                    }
                    // CHECK 2: Skybox Materials
                    else if (obj is Material mat)
                    {
                        // Be careful distinguishing skybox from other things? 
                        // Usually only skyboxes are sub-assets here.
                        if (mat != activeSky)
                        {
                            toDestroy.Add(mat);
                        }
                    }
                    // CHECK 3: Volume Components
                    else if (obj is VolumeComponent comp)
                    {
                        // If it's not in the active volume's component list, kill it
                        if (!validComponents.Contains(comp))
                        {
                            toDestroy.Add(comp);
                        }
                    }
                }

                if (toDestroy.Count > 0)
                {
                    foreach (var dead in toDestroy)
                    {
                        // Debug.Log($"[Kroma] Removing orphan: {dead.name} ({dead.GetType().Name}) from {profile.name}");
                        Object.DestroyImmediate(dead, true);
                        deletedCount++;
                    }
                    EditorUtility.SetDirty(profile);
                }
            }

            if (deletedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Cleanup Complete", $"Removed {deletedCount} orphan assets.", "OK");
            }
            else
            {
                 EditorUtility.DisplayDialog("Cleanup Complete", "No orphans found.", "OK");
            }
        }
        [MenuItem("Assets/Kroma/Bundle Selected Profiles", false, 20)]
        public static void BundleSelectedProfiles()
        {
            int count = 0;
            foreach (Object obj in Selection.objects)
            {
                if (obj is KromaProfile profile)
                {
                    bool modified = BundleProfile(profile);
                    if (modified) count++;
                }
            }

            if (count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Bundle Complete", $"Successfully bundled {count} selected profiles.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Bundle Complete", "No changes needed for selected profiles.", "OK");
            }
        }

        [MenuItem("Assets/Kroma/Bundle Selected Profiles", true)]
        public static bool ValidateBundleSelectedProfiles()
        {
            // Only show if at least one KromaProfile is selected
            foreach (Object obj in Selection.objects)
            {
                if (obj is KromaProfile) return true;
            }
            return false;
        }
    }
}
