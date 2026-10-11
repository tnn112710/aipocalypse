using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FayLabs.Kroma;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// View mode for the browser window
    /// </summary>
    public enum ViewMode
    {
        Gallery,
        List
    }

    /// <summary>
    /// Flags enum for selective profile application (used for MaskField UI)
    /// </summary>
    [System.Flags]
    public enum ApplyFlags
    {
        None = 0,
        PostProcessing = 1 << 0,    // 1
        Skybox = 1 << 1,            // 2
        SunLight = 1 << 2,          // 4
        Fog = 1 << 3,               // 8
        SceneObjects = 1 << 4,      // 16
        Everything = ~0             // All bits set
    }

    /// <summary>
    /// Filter mode for profile source (Base vs Created)
    /// </summary>
    public enum ProfileSourceFilter
    {
        AllProfiles,
        KromaProfiles, // Base folder
        MyProfiles,    // Created folder
        Favorites
    }

    /// <summary>
    /// Settings for selective profile application
    /// </summary>
    [System.Serializable]
    public struct ApplySettings
    {
        public bool applyPostProcessing;
        public bool applySkybox;
        public bool applySunLight;
        public bool applyFog;
        public bool applySceneObjects;

        public static ApplySettings All => new ApplySettings
        {
            applyPostProcessing = true,
            applySkybox = true,
            applySunLight = true,
            applyFog = true,
            applySceneObjects = true
        };
        
        /// <summary>
        /// Convert ApplySettings struct to ApplyFlags enum
        /// </summary>
        public ApplyFlags ToFlags()
        {
            ApplyFlags flags = ApplyFlags.None;
            if (applyPostProcessing) flags |= ApplyFlags.PostProcessing;
            if (applySkybox) flags |= ApplyFlags.Skybox;
            if (applySunLight) flags |= ApplyFlags.SunLight;
            if (applyFog) flags |= ApplyFlags.Fog;
            if (applySceneObjects) flags |= ApplyFlags.SceneObjects;
            return flags;
        }
        
        /// <summary>
        /// Create ApplySettings from ApplyFlags enum
        /// </summary>
        public static ApplySettings FromFlags(ApplyFlags flags)
        {
            return new ApplySettings
            {
                applyPostProcessing = (flags & ApplyFlags.PostProcessing) != 0,
                applySkybox = (flags & ApplyFlags.Skybox) != 0,
                applySunLight = (flags & ApplyFlags.SunLight) != 0,
                applyFog = (flags & ApplyFlags.Fog) != 0,
                applySceneObjects = (flags & ApplyFlags.SceneObjects) != 0
            };
        }
    }

    /// <summary>
    /// The "Brain" of the tool. 
    /// Handles asset loading, preview logic (Ghost Volumes), and application logic.
    /// Does NOT handle drawing or UI styles.
    /// </summary>
    public class KromaBrowserLogic
    {
        // --- DATA STORAGE ---
        public List<KromaProfile> Profiles = new List<KromaProfile>();
        // NEW: We allow the UI to read this filtered list instead of the raw 'Profiles'
        public List<KromaProfile> FilteredProfiles = new List<KromaProfile>();
        
        // Track duplicates: Wrapper -> Original Volume Name
        public Dictionary<KromaProfile, string> DuplicateWarnings = new Dictionary<KromaProfile, string>();

        // REMOVED: Thumbnails dictionary (now stored on KromaProfile wrapper)
        // public Dictionary<string, Texture2D> Thumbnails = new Dictionary<string, Texture2D>();
        public Texture2D BannerTexture;
        public Texture2D DefaultThumbnail;

        // --- SEARCH STATE ---
        public string SearchTerm = "";
        
        // --- TIME FILTER STATE ---
        public int SelectedTimeFilterIndex = 0; // 0=All, 1=Sunrise, etc.
        public ProfileSourceFilter CurrentSourceFilter = ProfileSourceFilter.AllProfiles; // 0=All, 1=Kroma, 2=My Profiles
        
        // --- KEYBOARD NAVIGATION STATE ---
        public int SelectedProfileIndex = -1; // -1 means no selection, used for arrow key navigation

        // --- FAVORITES STATE ---
        public HashSet<string> FavoriteProfileGUIDs = new HashSet<string>(); // Stores favorited profile GUIDs
        public bool ShowOnlyFavorites = false; // Filter state for favorites view

        // --- VIEW MODE STATE ---
        public ViewMode CurrentView = ViewMode.Gallery; // Current view mode (Gallery or Settings)

        // --- LIGHTING SETTINGS STATE ---
        public bool ApplySkyboxSettings = true; // Whether to apply skybox settings when applying a profile
        public bool ApplyFogSettings = true; // Whether to apply fog settings when applying a profile
        public KromaProfileLibrary ProfileLibrary; // Reference to the new Library ScriptableObject

        
        private const string APPLY_SKYBOX_PREF_KEY = KromaConstants.PREF_APPLY_SKYBOX;
        private const string APPLY_FOG_PREF_KEY = KromaConstants.PREF_APPLY_FOG;

        // --- SELECTIVE APPLY SETTINGS ---
        public ApplySettings CurrentApplySettings = ApplySettings.All; // Current toggle state for selective application
        private const string SHOW_FAVORITES_PREF_KEY = "Kroma_ShowFavorites";
        private const string APPLY_PP_PREF_KEY = KromaConstants.PREF_APPLY_PP;
        private const string APPLY_SUN_PREF_KEY = KromaConstants.PREF_APPLY_SUN;
        private const string APPLY_SCENE_OBJECTS_PREF_KEY = KromaConstants.PREF_APPLY_SCENE_OBJECTS;
        private const string SOURCE_FILTER_PREF_KEY = "Kroma_SourceFilter";
        
        // --- PARTICLE SETTINGS ---
        public bool AutoSimulateParticles = true;
        private const string AUTO_SIM_PARTICLES_PREF_KEY = KromaConstants.PREF_AUTO_SIMULATE_PARTICLES;

        // --- PREVIEW STATE ---
        public bool IsPreviewing { get; private set; }
        public KromaProfile ActivePreviewProfile { get; private set; }
        public bool ShowSettingsView { get; set; } = false;      
        private Volume _previewVolume; // The "Ghost" volume
        private Volume _cachedTargetVolume; // The original volume we hid
        private float _cachedOriginalWeight;
        private VolumeProfile _cachedOriginalProfile; // The original profile before preview
        private KromaProfile _cachedOriginalKromaProfile; // Original Kroma profile (state capture)
        
        // --- SCENE OBJECTS STATE ---
        private List<GameObject> _spawnedSceneObjects = new List<GameObject>(); // Track spawned objects for cleanup
        private List<GameObject> _hiddenAppliedObjects = new List<GameObject>(); // Track applied objects hidden during preview

        // --- ONBOARDING / DEPENDENCY STATE ---
        public KromaSceneDependency[] Dependencies;
        public UnityEngine.Object DetectedCamera;
        public UnityEngine.Object DetectedLight;
        public UnityEngine.Object DetectedVolume;
        private bool _dependenciesDetected = false;

        // --- MVC ENCAPSULATION PROPERTIES ---
        public bool IsVolumeDetected => DetectedVolume != null;
        public bool IsSunDetected => DetectedLight != null;
        public bool IsCameraDetected => DetectedCamera != null;
        public bool IsDependencyMissing => !IsVolumeDetected || !IsSunDetected || !IsCameraDetected;

        // --- MVC ACTION METHODS ---
        public void SetDetectedCamera(UnityEngine.Object obj)
        {
             DetectedCamera = obj;
             if (DetectedCamera != null && Dependencies != null && Dependencies.Length > 0) 
                 Dependencies[0].Configure(DetectedCamera);
        }

        public void SetDetectedSun(UnityEngine.Object obj)
        {
             DetectedLight = obj;
             if (DetectedLight != null && Dependencies != null && Dependencies.Length > 1) 
                 Dependencies[1].Configure(DetectedLight);
        }

        public void SetDetectedVolume(UnityEngine.Object obj)
        {
             DetectedVolume = obj;
             if (DetectedVolume != null && Dependencies != null && Dependencies.Length > 2) 
                 Dependencies[2].Configure(DetectedVolume);
        }

        // --- CAMERA STATE CACHE ---
        private CameraRenderType _originalMainCameraType = CameraRenderType.Base;
        private bool _didForceOverlay = false;

        // Preview State Capture (Temporary for Preview session)
        private CameraRenderType _previewOriginalRenderType;
        private CameraClearFlags _previewOriginalClearFlags;
        private bool _previewOriginalDidForceOverlay;

        // --- DATA OPERATIONS ---

        public void RefreshProfileList()
        {
            Profiles.Clear();
            // Thumbnails.Clear(); // REMOVED
            DuplicateWarnings.Clear();
            BannerTexture = null;

            // 1. Load Profile Library
            LoadLibrary();
            
            // 1b. Auto-Populate Library from Content Folder
            AutoPopulateLibrary();
            
            Dictionary<VolumeProfile, string> usedVolumes = new Dictionary<VolumeProfile, string>();
            
            // 2. Add profiles from the Library
            if (ProfileLibrary != null && ProfileLibrary.profiles != null)
            {
                foreach (var kromaProfile in ProfileLibrary.profiles)
                {
                    if (kromaProfile != null && kromaProfile.volumeProfile != null)
                    {
                        // Check for duplicate volume usage
                        if (usedVolumes.ContainsKey(kromaProfile.volumeProfile))
                        {
                            // This is a duplicate!
                            DuplicateWarnings[kromaProfile] = usedVolumes[kromaProfile.volumeProfile];
                        }
                        else
                        {
                            // First time seeing this volume
                            // Store the NAME of the profile that owns it
                            usedVolumes[kromaProfile.volumeProfile] = kromaProfile.name;
                        }

                        // Always add to list
                        Profiles.Add(kromaProfile);
                    }
                }
            }
            
            // 3. Find remaining Profiles in the project (append to end) - OPTIONAL FALLBACK
            // ... (rest of search logic logic)
            
            // 4. Find Banner
            string[] bannerGuids = AssetDatabase.FindAssets(KromaConstants.BANNER_SEARCH_QUERY);
            if (bannerGuids.Length > 0)
            {
                string bannerPath = AssetDatabase.GUIDToAssetPath(bannerGuids[0]);
                BannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(bannerPath);
            }
            
            // 4b. Find Default Thumbnail
            string[] defThumbGuids = AssetDatabase.FindAssets(KromaConstants.DEFAULT_THUMBNAIL_NAME);
            if (defThumbGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(defThumbGuids[0]);
                DefaultThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            // 5. Load Favorites from EditorPrefs
            LoadFavorites();

            // 6. Load Lighting Settings preference (Legacy - maybe remove?)
            // 6. Load Lighting Settings preference (Legacy - maybe remove?)
            // Legacy reference removed

            // 8. Load Apply Settings (toggle state)
            LoadApplySettings();

            // 9. Initialize Filter
            ApplySearchFilter();
            
            // 9. Auto-select active profile if one exists
            // FIX: Disabled auto-selection. Selection only on keyboard input.
            // SelectActiveProfile();
            SelectedProfileIndex = -1;
        }

        private void LoadLibrary()
        {
            // If we already have a library reference, check if it's still valid
            if (ProfileLibrary != null) return;
            
            // First check if user has a explicitly selected library
            string savedLibGuid = EditorPrefs.GetString("Kroma_PreferredLibraryGUID", "");
            if (!string.IsNullOrEmpty(savedLibGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(savedLibGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    ProfileLibrary = AssetDatabase.LoadAssetAtPath<KromaProfileLibrary>(path);
                    if (ProfileLibrary != null) return;
                }
            }
            
            // Fallback: Try to find ANY library in the project
            string[] guids = AssetDatabase.FindAssets("t:KromaProfileLibrary");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                ProfileLibrary = AssetDatabase.LoadAssetAtPath<KromaProfileLibrary>(path);
            }
        }
        
        public void SetPreferredLibrary(KromaProfileLibrary newLibrary)
        {
            ProfileLibrary = newLibrary;
            
            if (newLibrary != null)
            {
                string path = AssetDatabase.GetAssetPath(newLibrary);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString("Kroma_PreferredLibraryGUID", guid);
            }
            else
            {
                EditorPrefs.DeleteKey("Kroma_PreferredLibraryGUID");
            }
            
            // Force reload profiles
            RefreshProfileList();
        }

        public void CreateNewLibrary()
        {
            // Fully dynamic path to support moving the asset folder
            string folderPath = KromaAssetManager.GetProfilesFolder();
            
            // Absolute fallback just in case the asset manager fails to find the root folder
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                folderPath = "Assets"; 
            }
            
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/Kroma Library.asset");
            
            KromaProfileLibrary newLib = ScriptableObject.CreateInstance<KromaProfileLibrary>();
            AssetDatabase.CreateAsset(newLib, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            SetPreferredLibrary(newLib);
        }
        
        private void AutoPopulateLibrary()
        {
            if (ProfileLibrary == null) return;

            string searchPath = "";
            string libPath = AssetDatabase.GetAssetPath(ProfileLibrary);
            if (!string.IsNullOrEmpty(libPath))
            {
                // Library is in .../Content/Profiles/Data/Kroma Library.asset
                // We want to search .../Content/Profiles
                // Up two levels from the library
                string dataDir = Path.GetDirectoryName(libPath);
                string profilesDir = Path.GetDirectoryName(dataDir);
                if (Directory.Exists(profilesDir))
                {
                    searchPath = profilesDir;
                }
            }
            
            if (string.IsNullOrEmpty(searchPath))
            {
                 // Fallback
                 searchPath = KromaAssetManager.GetProfilesFolder();
            }


            string[] guids = AssetDatabase.FindAssets("t:KromaProfile", new[] { searchPath });
            // 1. Gather all profiles currently on disk
            List<KromaProfile> foundProfiles = new List<KromaProfile>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Ignore backup folder to prevent duplicates
                if (path.Contains("/ProfileBackup/")) continue;
                
                KromaProfile profile = AssetDatabase.LoadAssetAtPath<KromaProfile>(path);
                if (profile != null)
                {
                    foundProfiles.Add(profile);
                }
            }
            
            // 2. Build new list: Keep existing valid profiles (preserve order), then append new ones
            List<KromaProfile> mergedList = new List<KromaProfile>();
            HashSet<KromaProfile> processedProfiles = new HashSet<KromaProfile>();

            // A. Keep existing profiles if they still exist on disk
            if (ProfileLibrary.profiles != null)
            {
                foreach (var existing in ProfileLibrary.profiles)
                {
                    if (existing != null && foundProfiles.Contains(existing))
                    {
                        mergedList.Add(existing);
                        processedProfiles.Add(existing);
                    }
                }
            }

            // B. Find brand new profiles (on disk but not in library)
            List<KromaProfile> brandNewProfiles = new List<KromaProfile>();
            foreach (var found in foundProfiles)
            {
                if (!processedProfiles.Contains(found))
                {
                    brandNewProfiles.Add(found);
                }
            }

            // C. Sort NEW profiles by name (so they append neatly)
            brandNewProfiles.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            
            // D. Append new profiles
            mergedList.AddRange(brandNewProfiles);

            // Diff check to avoid dirtying asset if nothing changed
            bool changed = false;
            if (ProfileLibrary.profiles == null || ProfileLibrary.profiles.Count != mergedList.Count)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < mergedList.Count; i++)
                {
                    if (ProfileLibrary.profiles[i] != mergedList[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                ProfileLibrary.profiles = mergedList;
                EditorUtility.SetDirty(ProfileLibrary);
                AssetDatabase.SaveAssets(); // Ensure we persist immediately
            }
        }

        public void ApplySearchFilter()
        {
            // Start with full list
            IEnumerable<KromaProfile> query = Profiles;

            // 1. Text Search Filter (Name/Tags)
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(p => 
                {
                    // Check Profile Name
                    bool nameMatch = false;
                    if (!string.IsNullOrEmpty(p.profileName))
                    {
                         nameMatch = KromaLightingUtils.GetCleanProfileName(p.profileName).IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (!nameMatch)
                    {
                        // Fallback to asset name
                        nameMatch = KromaLightingUtils.GetCleanProfileName(p.name).IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    
                    // Check Tags
                    bool tagMatch = false;
                    if (!string.IsNullOrEmpty(p.searchTags))
                    {
                        if (p.searchTags.IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tagMatch = true;
                        }
                    }
                    
                    return nameMatch || tagMatch;
                });
            }

            // 2. Time of Day Filter
            if (SelectedTimeFilterIndex > 0)
            {
                // Enum mapping:
                // 0: All (Ignored here)
                // 1: Sunrise
                // 2: Daytime
                // 3: Sunset
                // 4: Night
                // 5: Other
                
                // Map Filter Index to Enum (Offset by -1 since 0 is 'All')
                KromaTimeOfDay targetTime = (KromaTimeOfDay)(SelectedTimeFilterIndex - 1);
                
                query = query.Where(p => p.timeOfDay == targetTime);
            }

            // 3. Source Folder Filter (Base vs Created vs Favorites)
            if (CurrentSourceFilter != ProfileSourceFilter.AllProfiles)
            {
                if (CurrentSourceFilter == ProfileSourceFilter.Favorites)
                {
                    query = query.Where(p => 
                    {
                        string path = AssetDatabase.GetAssetPath(p);
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        return FavoriteProfileGUIDs.Contains(guid);
                    });
                }
                else
                {
                    query = query.Where(p => 
                    {
                        string path = AssetDatabase.GetAssetPath(p);
                        if (CurrentSourceFilter == ProfileSourceFilter.KromaProfiles)
                        {
                            return path.Contains("/Base/");
                        }
                        else // MyProfiles
                        {
                            return path.Contains("/Created/");
                        }
                    });
                }
            }
            
            if (CurrentSourceFilter == ProfileSourceFilter.MyProfiles)
            {
                query = query.OrderByDescending(p => System.IO.File.GetCreationTimeUtc(AssetDatabase.GetAssetPath(p)));
            }

            FilteredProfiles = query.ToList();
        }

        public string GetCleanName(string originalName)
        {
            return KromaLightingUtils.GetCleanProfileName(originalName);
        }

        // --- SCENE OPERATIONS ---

        /// <summary>
        /// Initialize dependency system and detect scene requirements.
        /// Call this once on browser open.
        /// </summary>
        public void InitializeDependencies()
        {
            Dependencies = new KromaSceneDependency[]
            {
                new CameraDependency(),
                new LightDependency(),
                new VolumeDependency()
            };
            
            DetectDependencies();
        }
        
        /// <summary>
        /// Detect all dependencies in the scene.
        /// Updates DetectedCamera, DetectedLight, DetectedVolume.
        /// </summary>
        public void DetectDependencies()
        {
            if (Dependencies == null) InitializeDependencies();
            
            DetectedCamera = Dependencies[0].DetectInScene();
            DetectedLight = Dependencies[1].DetectInScene();
            DetectedVolume = Dependencies[2].DetectInScene();
            
            _dependenciesDetected = true;
        }
        
        /// <summary>
        /// Check if URP is set up in the project.
        /// </summary>
        public bool IsURPActive()
        {
            return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        }
        
        /// <summary>
        /// Check if all requirements are met.
        /// </summary>
        public bool AllRequirementsMet()
        {
            // Re-detect if dependencies haven't been detected yet OR if cached references are null
            // (can happen after exiting play mode when Unity destroys runtime references)
            if (!_dependenciesDetected || DetectedCamera == null || DetectedLight == null || DetectedVolume == null)
            {
                DetectDependencies();
            }
            
            // STRICT CHECK: Validate each object against its dependency rules
            bool camValid = Dependencies[0].Validate(DetectedCamera);
            bool lightValid = Dependencies[1].Validate(DetectedLight);
            bool volValid = Dependencies[2].Validate(DetectedVolume);
            
            return camValid && lightValid && volValid && IsURPActive();
        }
        
        /// <summary>
        /// Initialize Kroma scene - creates missing objects and configures existing ones.
        /// Replaces old SetupScene() method with dependency-based approach.
        /// </summary>
        public void InitializeKromaScene()
        {
            if (!IsURPActive())
            {
                Debug.LogError("Kroma: URP is not active. Please set up Universal Render Pipeline before initializing.");
                return;
            }
            
            Undo.SetCurrentGroupName("Initialize Kroma Scene");
            int undoGroup = Undo.GetCurrentGroup();
            
            // Detect current state
            DetectDependencies();
            
            // Create missing objects
            if (DetectedCamera == null)
            {
                DetectedCamera = Dependencies[0].Create();
            }
            
            if (DetectedLight == null)
            {
                DetectedLight = Dependencies[1].Create();
            }
            
            if (DetectedVolume == null)
            {
                DetectedVolume = Dependencies[2].Create();
                // Select the newly created volume
                if (DetectedVolume is Volume vol)
                {
                    Selection.activeGameObject = vol.gameObject;
                }
            }
            
            // Configure all objects (both existing and newly created)
            Dependencies[0].Configure(DetectedCamera); // Camera: enable post-processing, FXAA
            Dependencies[1].Configure(DetectedLight);  // Light: no forced config
            Dependencies[2].Configure(DetectedVolume); // Volume: ensure global, priority 0
            
            // Create Kroma Scene Profile GameObject (empty profile, ready for user to apply)
            CreateSceneProfileGameObject();
            
            Undo.CollapseUndoOperations(undoGroup);
            
            // Log result
            // string message = "Kroma: Scene initialized.";
            // Debug.Log(message);

            // Enable "Always Refresh" in Scene View to ensure effects (clouds, god rays) are visible
            foreach (SceneView sv in SceneView.sceneViews)
            {
                sv.sceneViewState.alwaysRefresh = true;
            }

            // Disable Game View Gizmos to prevent depth buffer errors (UUM-104383)
            System.Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                var windows = Resources.FindObjectsOfTypeAll(gameViewType);
                foreach (var window in windows)
                {
                    var showGizmosField = gameViewType.GetField("m_Gizmos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (showGizmosField != null)
                    {
                        showGizmosField.SetValue(window, false);
                    }
                }
            }
        }

        public bool SceneHasVolumes()
        {
            return Object.FindAnyObjectByType<Volume>() != null;
        }

        public void AutoSelectGlobalVolume()
        {
            // Find all volumes in the scene
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            
            // Only auto-select if there's exactly ONE volume
            if (volumes.Length != 1) return;
            
            // Select the single volume
            Selection.activeGameObject = volumes[0].gameObject;
            // Debug.Log($"[Kroma] Auto-selected volume: {volumes[0].gameObject.name}");
        }

        public bool HasValidSelection()
        {
            // Check if a volume is currently selected
            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Volume>() != null)
            {
                return true;
            }
            
            // If no volume selected, check if there's exactly ONE global volume
            // In that case, we can use it automatically (no selection needed)
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            int globalVolumeCount = 0;
            foreach (Volume vol in volumes)
            {
                if (vol.isGlobal) globalVolumeCount++;
            }
            
            return globalVolumeCount == 1;
        }
        
        /// <summary>
        /// Get the target volume - either the selected one, or the single global volume if there's only one
        /// </summary>
        public Volume GetTargetVolume()
        {
            // First check if a volume is selected
            if (Selection.activeGameObject != null)
            {
                Volume selectedVol = Selection.activeGameObject.GetComponent<Volume>();
                if (selectedVol != null) return selectedVol;
            }
            
            // If no volume selected, find the single global volume (if there's only one)
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            Volume singleGlobalVolume = null;
            int globalVolumeCount = 0;
            
            foreach (Volume vol in volumes)
            {
                if (vol.isGlobal)
                {
                    singleGlobalVolume = vol;
                    globalVolumeCount++;
                }
            }
            
            // Return the single global volume if there's exactly one
            return globalVolumeCount == 1 ? singleGlobalVolume : null;
        }

        public bool IsProfileActiveOnSelection(VolumeProfile profile)
        {
            // Use GetTargetVolume to support auto-detection of single global volume
            Volume vol = GetTargetVolume();
            return vol != null && vol.sharedProfile == profile;
        }

        // --- PREVIEW LOGIC (GHOST SYSTEM) ---

        public void StartPreview(KromaProfile profile)
        {
            // Stop any existing preview first (allows switching between previews)
            if (IsPreviewing)
            {
                StopPreview();
            }

            // Cache current Camera state BEFORE previewing
            if (Camera.main != null)
            {
                var data = Camera.main.GetUniversalAdditionalCameraData();
                _previewOriginalRenderType = data.renderType;
                _previewOriginalClearFlags = Camera.main.clearFlags;
                _previewOriginalDidForceOverlay = _didForceOverlay;
            }
            
            // 1. Isolate: Disable the current selection so it doesn't bleed through (only if PP enabled)
            if (CurrentApplySettings.applyPostProcessing)
            {
                Volume vol = GetTargetVolume();
                if (vol != null)
                {
                    _cachedTargetVolume = vol;
                    _cachedOriginalWeight = vol.weight;
                    _cachedOriginalProfile = vol.profile;
                    vol.weight = 0f;
                }
            }

            // 2. Cache current lighting settings and apply preview lighting (if any lighting toggles enabled)
            bool applyAnyLighting = CurrentApplySettings.applySkybox || CurrentApplySettings.applySunLight || CurrentApplySettings.applyFog;
            
            if (applyAnyLighting && ProfileLibrary != null)
            {
                Volume targetVol = GetTargetVolume();
                bool isLocalVolume = targetVol != null && !targetVol.isGlobal;
                
                if (!isLocalVolume)
                {
                    _cachedOriginalKromaProfile = ProfileLibrary.GetProfileByAsset(_cachedTargetVolume != null ? _cachedTargetVolume.sharedProfile : null);
                    // If no KromaProfile for original, we might lose lighting state if we don't capture raw?
                    // V1.0 captured raw lighting. V2 should use KromaProfile logic.
                    // For now, let's try to apply the lighting from the NEW KromaProfile associated with 'profile'.
                    
                    if (profile != null)
                    {
                        KromaLightingApplier.ApplyLightingSettings(
                            profile, 
                            CurrentApplySettings.applySkybox,
                            CurrentApplySettings.applySunLight,
                            CurrentApplySettings.applyFog,
                            applyVolume: false // DO NOT apply volume here (Ghost Volume handles it without persistent changes)
                        );
                    }
                }
            }

            // 3. Create Ghost Volume (only if PP toggle is enabled)
            if (CurrentApplySettings.applyPostProcessing)
            {
                if (_previewVolume == null)
                {
                    GameObject obj = new GameObject("Kroma_Preview_Volume");
                    obj.hideFlags = HideFlags.DontSave;
                    _previewVolume = obj.AddComponent<Volume>();
                }
                _previewVolume.isGlobal = true;
                _previewVolume.priority = 9999;
                _previewVolume.profile = profile.volumeProfile;
            }
            
            // 4. Hide previously applied scene objects (so preview shows true final result)
            HideAppliedSceneObjects();
            
            // 5. Spawn scene objects (if toggle enabled and not a local volume)
            if (CurrentApplySettings.applySceneObjects && ProfileLibrary != null)
            {
                Volume targetVol = GetTargetVolume();
                bool isLocalVolume = targetVol != null && !targetVol.isGlobal;
                
                if (!isLocalVolume && profile != null && profile.sceneObjects != null && profile.sceneObjects.Length > 0)
                {
                    SpawnSceneObjects(profile.sceneObjects);
                }
            }
            
            IsPreviewing = true;
            ActivePreviewProfile = profile;
            
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void StopPreview()
        {
            if (!IsPreviewing) return;

            // 1. Destroy Ghost
            if (_previewVolume != null) Object.DestroyImmediate(_previewVolume.gameObject);

            // 2. Restore Original Volume Weight AND Profile
            if (_cachedTargetVolume != null)
            {
                // Restore the original profile that was on the volume before preview
                if (_cachedOriginalProfile != null)
                {
                    _cachedTargetVolume.profile = _cachedOriginalProfile;
                    _cachedOriginalProfile = null;
                }
                
                _cachedTargetVolume.weight = _cachedOriginalWeight;
                _cachedTargetVolume = null;
            }

            // 3. Restore Original Lighting Settings
            if (_cachedOriginalKromaProfile != null)
            {
                KromaLightingApplier.ApplyLightingSettings(_cachedOriginalKromaProfile);
                _cachedOriginalKromaProfile = null;
            }
            
            // 4. Cleanup spawned scene objects
            CleanupSceneObjects();
            
            // 5. Restore specific Preview Camera changes
            if (Camera.main != null)
            {
                var data = Camera.main.GetUniversalAdditionalCameraData();
                // Check if current state differs from cached original
                // Only restore if we actually changed something or if needed
                data.renderType = _previewOriginalRenderType;
                Camera.main.clearFlags = _previewOriginalClearFlags;
            }
            // Restore tracked state
            _didForceOverlay = _previewOriginalDidForceOverlay;
            
            // 5. Restore previously hidden applied objects
            ShowAppliedSceneObjects();

            IsPreviewing = false;
            ActivePreviewProfile = null;
            
            // Force scene to update with restored profile
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void ApplyToSelected(KromaProfile profile, EditorWindow window)
        {
            Volume vol = GetTargetVolume();
            if (vol == null)
            {
                EditorUtility.DisplayDialog(KromaBrowserStrings.WINDOW_TITLE, KromaBrowserStrings.ERROR_SELECT_VOLUME, "OK");
                return;
            }

            StopPreview();

            Undo.RecordObject(vol, KromaBrowserStrings.UNDO_APPLY);
            
            // Apply post-processing if enabled
            if (CurrentApplySettings.applyPostProcessing)
            {
                // IMPORTANT: Reset volume to clear any previous profile's overrides
                VolumeProfile tempProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                vol.profile = tempProfile; // Use instance profile to reset
                
                // Now apply the new profile
                vol.sharedProfile = profile.volumeProfile;
                vol.weight = 1f;
                
                // Clean up temporary profile
                Object.DestroyImmediate(tempProfile);
            }

            // Apply lighting settings if any lighting toggles are enabled
            bool applyAnyLighting = CurrentApplySettings.applySkybox || CurrentApplySettings.applySunLight || CurrentApplySettings.applyFog;
            
            if (applyAnyLighting && ProfileLibrary != null)
            {
                Volume targetVol = GetTargetVolume();
                bool isLocalVolume = targetVol != null && !targetVol.isGlobal;
                
                if (!isLocalVolume)
                {
                    if (profile != null)
                    {
                        KromaLightingApplier.ApplyLightingSettings(
                            profile, 
                            CurrentApplySettings.applySkybox,
                            CurrentApplySettings.applySunLight,
                            CurrentApplySettings.applyFog,
                            applyVolume: false // Volume handled manually above
                        );
                    }
                }
            }
            
            // Spawn scene objects if toggle enabled and not a local volume
            if (CurrentApplySettings.applySceneObjects && ProfileLibrary != null)
            {
                Volume targetVol = GetTargetVolume();
                bool isLocalVolume = targetVol != null && !targetVol.isGlobal;
 
                if (!isLocalVolume)
                {
                    // ALWAYS call Cleanup first (this handles camera revert if needed)
                    CleanupAppliedSceneObjects();
 
                    if (profile != null && profile.sceneObjects != null && profile.sceneObjects.Length > 0)
                    {
                        // Spawn new objects
                        SpawnSceneObjects(profile.sceneObjects);
 
                        // Save spawned objects to KromaSceneLighting for future hiding during preview
                        SaveAppliedSceneObjects();
                    }
 
                    if (Camera.main != null)
                    {
                        // Ensure Background Type is Skybox
                        Camera.main.clearFlags = CameraClearFlags.Skybox;
                    }
                }
            }
            
            // Update the scene profile GameObject with the applied profile
            UpdateSceneProfileGameObject(profile);


            EditorUtility.SetDirty(vol);
            if (window != null)
            {
                window.Repaint();
            }
            
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            
            string profileName = GetCleanName(profile.profileName);
            if (string.IsNullOrEmpty(profileName)) profileName = GetCleanName(profile.name);
            
            // Convert to title case (first letter uppercase, rest lowercase)
            profileName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(profileName.ToLower());
            if (window != null)
            {
                if (window is KromaProfileBrowser browserWindow)
                {
                    browserWindow.ShowCustomNotification(
                        string.Format(KromaBrowserStrings.MSG_STYLE_APPLIED, profileName),
                        KromaNotificationType.ProfileApplied
                    );
                }
                else
                {
                    window.ShowNotification(new GUIContent(string.Format(KromaBrowserStrings.MSG_STYLE_APPLIED, profileName)));
                }
            }
        }

        /// <summary>
        /// Creates the Kroma Scene Profile GameObject during initial setup (empty profile)
        /// </summary>
        private void CreateSceneProfileGameObject()
        {
            // Check if one already exists
            KromaSceneLighting existing = Object.FindAnyObjectByType<KromaSceneLighting>();
            if (existing != null)
            {
                // Debug.Log("[Kroma] Scene profile GameObject already exists.");
                return;
            }
            
            // Create new GameObject with fixed name
            GameObject profileObj = new GameObject("Kroma Scene Profile");
            Undo.RegisterCreatedObjectUndo(profileObj, "Create Kroma Scene Profile");
            
            // Add component
            KromaSceneLighting sceneLighting = profileObj.AddComponent<KromaSceneLighting>();
            
            // Set banner texture (profile will be null until user applies one)
            if (BannerTexture != null)
            {
                sceneLighting.bannerTexture = BannerTexture;
            }
            
            EditorUtility.SetDirty(sceneLighting);
            // Debug.Log("[Kroma] Created scene profile GameObject (no profile applied yet).");
        }

        /// <summary>
        /// Updates the existing Kroma Scene Profile GameObject with the applied profile
        /// </summary>
        private void UpdateSceneProfileGameObject(KromaProfile profile)
        {
            // Find existing component (component-based detection, not name-based!)
            KromaSceneLighting sceneLighting = Object.FindAnyObjectByType<KromaSceneLighting>();
            
            if (sceneLighting == null)
            {
                // Fallback: create if it doesn't exist (shouldn't happen if setup was run)
                Debug.LogWarning("[Kroma] Scene profile GameObject not found. Creating one now...");
                GameObject profileObj = new GameObject("Kroma Scene Profile");
                Undo.RegisterCreatedObjectUndo(profileObj, "Create Kroma Scene Profile");
                sceneLighting = profileObj.AddComponent<KromaSceneLighting>();
            }
            
            Undo.RecordObject(sceneLighting, "Update Kroma Scene Profile");
            
            // Update profile reference and reset boost
            sceneLighting.kromaProfile = profile;
            if (profile.environmentSource == AmbientMode.Skybox)
            {
                sceneLighting.environmentLightingBoost = profile.environmentMultiplier;
            }
            else
            {
                sceneLighting.environmentLightingBoost = 0f;
            }
            sceneLighting.volumeWeight = 1f;
            sceneLighting.fogIntensity = 1f;
            
            // Update banner texture with profile-specific thumbnail (fallback to BannerTexture if no thumbnail exists)
            if (profile.thumbnail != null)
            {
                sceneLighting.bannerTexture = profile.thumbnail;
            }
            else if (BannerTexture != null)
            {
                sceneLighting.bannerTexture = BannerTexture;
            }
            
            EditorUtility.SetDirty(sceneLighting);
            // Debug.Log($"[Kroma] Updated scene profile to: {profile.name}");
        }

        // --- KEYBOARD NAVIGATION LOGIC ---

        public void NavigateUp(int columns = 1)
        {
            if (FilteredProfiles.Count == 0) return;
            
            if (SelectedProfileIndex == -1)
            {
                // No selection yet, select first item
                SelectedProfileIndex = 0;
            }
            else
            {
                // Move up by one row (subtract columns)
                SelectedProfileIndex -= columns;
                if (SelectedProfileIndex < 0) SelectedProfileIndex = 0;
            }
        }

        public void NavigateDown(int columns = 1)
        {
            if (FilteredProfiles.Count == 0) return;
            
            if (SelectedProfileIndex == -1)
            {
                // No selection yet, select first item
                SelectedProfileIndex = 0;
            }
            else
            {
                // Move down by one row (add columns)
                SelectedProfileIndex += columns;
                if (SelectedProfileIndex >= FilteredProfiles.Count)
                {
                    SelectedProfileIndex = FilteredProfiles.Count - 1;
                }
            }
        }

        public void NavigateLeft()
        {
            if (FilteredProfiles.Count == 0) return;
            
            if (SelectedProfileIndex == -1)
            {
                SelectedProfileIndex = 0;
            }
            else
            {
                SelectedProfileIndex--;
                if (SelectedProfileIndex < 0) SelectedProfileIndex = 0;
            }
        }

        public void NavigateRight()
        {
            if (FilteredProfiles.Count == 0) return;
            
            if (SelectedProfileIndex == -1)
            {
                SelectedProfileIndex = 0;
            }
            else
            {
                SelectedProfileIndex++;
                if (SelectedProfileIndex >= FilteredProfiles.Count)
                {
                    SelectedProfileIndex = FilteredProfiles.Count - 1;
                }
            }
        }

        public KromaProfile GetSelectedProfile()
        {
            if (SelectedProfileIndex >= 0 && SelectedProfileIndex < FilteredProfiles.Count)
            {
                return FilteredProfiles[SelectedProfileIndex];
            }
            return null;
        }

        public void ResetSelection()
        {
            SelectedProfileIndex = -1;
        }

        public void SelectActiveProfile()
        {
            // Find the active profile in the filtered list
            KromaProfile activeProfile = null;
            foreach (var profile in FilteredProfiles)
            {
                if (IsProfileActiveOnSelection(profile.volumeProfile))
                {
                    activeProfile = profile;
                    break;
                }
            }
            
            if (activeProfile != null)
            {
                int activeIndex = FilteredProfiles.IndexOf(activeProfile);
                if (activeIndex >= 0)
                {
                    // FIX: User requested NO SELECTION on start, even if profile is active.
                    // Selection/Buttons should only appear on keyboard interaction.
                    SelectedProfileIndex = -1; 
                    return;
                }
            }
            
            // If no active profile or not found in filtered list, ensure no selection
            SelectedProfileIndex = -1;
        }

        // --- FAVORITES MANAGEMENT ---
        
        public void ToggleFavorite(KromaProfile profile)
        {
            if (profile == null) return;
            string path = AssetDatabase.GetAssetPath(profile);
            string guid = AssetDatabase.AssetPathToGUID(path);
            
            if (FavoriteProfileGUIDs.Contains(guid))
            {
                FavoriteProfileGUIDs.Remove(guid);
            }
            else
            {
                FavoriteProfileGUIDs.Add(guid);
            }
            SaveFavorites();
            ApplySearchFilter();
        }

        public bool IsFavorite(KromaProfile profile)
        {
            if (profile == null) return false;
            string path = AssetDatabase.GetAssetPath(profile);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return FavoriteProfileGUIDs.Contains(guid);
        }

        private void SaveFavorites()
        {
            string data = string.Join(";", FavoriteProfileGUIDs);
            EditorPrefs.SetString("Kroma_FavoriteGUIDs", data);
        }

        private void LoadFavorites()
        {
            FavoriteProfileGUIDs.Clear();
            if (EditorPrefs.HasKey("Kroma_FavoriteGUIDs"))
            {
                string data = EditorPrefs.GetString("Kroma_FavoriteGUIDs");
                string[] guids = data.Split(new char[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (string guid in guids)
                {
                    FavoriteProfileGUIDs.Add(guid);
                }
            }
        }

        public int GetFavoritesCount()
        {
            // Count only favorites that actually exist
            int count = 0;
            foreach (var guid in FavoriteProfileGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Remove stale favorites (profiles that no longer exist) from EditorPrefs
        /// </summary>
        public void CleanupStaleFavorites()
        {
            List<string> staleFavorites = new List<string>();
            
            foreach (var guid in FavoriteProfileGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // If path is empty or asset doesn't exist at that path (unlikely if path exists but check anyway)
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<Object>(path) == null)
                {
                    staleFavorites.Add(guid);
                }
            }
            
            foreach (var stale in staleFavorites)
            {
                FavoriteProfileGUIDs.Remove(stale);
            }
            
            if (staleFavorites.Count > 0)
            {
                SaveFavorites();
                // Debug.Log($"[Kroma] Cleaned up {staleFavorites.Count} stale favorite(s).");
            }
        }

        // --- LIGHTING SETTINGS MANAGEMENT ---

        public void LoadLightingSettingsPreference()
        {
            ApplySkyboxSettings = EditorPrefs.GetBool(APPLY_SKYBOX_PREF_KEY, true); // Default to true
            ApplyFogSettings = EditorPrefs.GetBool(APPLY_FOG_PREF_KEY, true); // Default to true
        }

        public void SaveLightingSettingsPreference()
        {
            EditorPrefs.SetBool(APPLY_SKYBOX_PREF_KEY, ApplySkyboxSettings);
            EditorPrefs.SetBool(APPLY_FOG_PREF_KEY, ApplyFogSettings);
        }

        public void ToggleSkyboxSettings()
        {
            ApplySkyboxSettings = !ApplySkyboxSettings;
            SaveLightingSettingsPreference();
        }

        public void ToggleFogSettings()
        {
            ApplyFogSettings = !ApplyFogSettings;
            SaveLightingSettingsPreference();
        }

        public void ToggleView()
        {
            CurrentView = (CurrentView == ViewMode.Gallery) ? ViewMode.List : ViewMode.Gallery;
        }

        // Legacy LoadLightingSettingsAsset method removed. 
        // Logic now relies entirely on KromaProfileLibrary (LoadLibrary).

        // --- APPLY SETTINGS MANAGEMENT ---

        public void LoadApplySettings()
        {
            CurrentApplySettings = new ApplySettings
            {
                applyPostProcessing = EditorPrefs.GetBool(APPLY_PP_PREF_KEY, true),
                applySkybox = EditorPrefs.GetBool(APPLY_SKYBOX_PREF_KEY, true),
                applySunLight = EditorPrefs.GetBool(APPLY_SUN_PREF_KEY, true),
                applyFog = EditorPrefs.GetBool(APPLY_FOG_PREF_KEY, true),
                applySceneObjects = EditorPrefs.GetBool(APPLY_SCENE_OBJECTS_PREF_KEY, true)
            };
            
            CurrentSourceFilter = (ProfileSourceFilter)EditorPrefs.GetInt(SOURCE_FILTER_PREF_KEY, 0);
            
            // Legacy migration: If they had favorites toggled on previously, convert it to the dropdown enum
            ShowOnlyFavorites = EditorPrefs.GetBool(SHOW_FAVORITES_PREF_KEY, false);
            if (ShowOnlyFavorites && CurrentSourceFilter != ProfileSourceFilter.Favorites)
            {
                 CurrentSourceFilter = ProfileSourceFilter.Favorites;
                 ShowOnlyFavorites = false; // Turn off legacy flag so it doesn't overwrite manually
                 EditorPrefs.SetBool(SHOW_FAVORITES_PREF_KEY, false);
                 EditorPrefs.SetInt(SOURCE_FILTER_PREF_KEY, (int)CurrentSourceFilter);
            }
            
            // Load Particle Setting
            AutoSimulateParticles = EditorPrefs.GetBool(AUTO_SIM_PARTICLES_PREF_KEY, true);
        }

        public void SaveApplySettings()
        {
            EditorPrefs.SetBool(APPLY_PP_PREF_KEY, CurrentApplySettings.applyPostProcessing);
            EditorPrefs.SetBool(APPLY_SKYBOX_PREF_KEY, CurrentApplySettings.applySkybox);
            EditorPrefs.SetBool(APPLY_SUN_PREF_KEY, CurrentApplySettings.applySunLight);
            EditorPrefs.SetBool(APPLY_FOG_PREF_KEY, CurrentApplySettings.applyFog);
            EditorPrefs.SetBool(APPLY_SCENE_OBJECTS_PREF_KEY, CurrentApplySettings.applySceneObjects);
            
            // Save Source Filter
            EditorPrefs.SetInt(SOURCE_FILTER_PREF_KEY, (int)CurrentSourceFilter);
            
            // Save Particle Setting
            EditorPrefs.SetBool(AUTO_SIM_PARTICLES_PREF_KEY, AutoSimulateParticles);
        }

        // --- SCENE OBJECTS MANAGEMENT ---

        /// <summary>
        /// Spawn scene objects from profile configuration
        /// </summary>
        private void SpawnSceneObjects(SceneObjectConfig[] sceneObjects)
        {
            if (sceneObjects == null || sceneObjects.Length == 0) return;

            // Find or create "Applied Profile Assets" container under "Kroma Scene Profile"
            GameObject assetsContainer = GetOrCreateProfileAssetsContainer();

            foreach (var objData in sceneObjects)
            {
                if (!objData.enabled) continue; // Skip disabled
                if (objData.prefab == null) continue;

                // Instantiate prefab
                GameObject spawnedObj = (GameObject)PrefabUtility.InstantiatePrefab(objData.prefab);
                
                if (spawnedObj != null)
                {
                    // --- 1. POSITIONING & ROTATION ---
                    Vector3 finalPosition = Vector3.zero;
                    Quaternion finalRotation = Quaternion.identity;
                    Vector3 finalScale = objData.scale;
                    Camera cam = Camera.main;
                    
                    // A. Calculate Position
                    if (objData.useCameraRelative && cam != null)
                    {
                        finalPosition = CalculateCameraRelativePosition(cam.transform, objData.azimuth, objData.elevation, objData.distance);
                    }
                    else
                    {
                        finalPosition = objData.position;
                    }

                    // B. Snap Mode
                    if (objData.snapMode == SceneObjectSnapMode.Ground)
                    {
                        // Cast from above the target position to find ground
                        float rayDist = 400f;
                        Vector3 rayOrigin = new Vector3(finalPosition.x, finalPosition.y + 20f, finalPosition.z);

                        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDist))
                        {
                            finalPosition = hit.point;
                            finalPosition.y += objData.groundOffset;
                        }
                        else
                        {
                            finalPosition.y = objData.groundOffset;
                        }
                    }
                    else if (objData.snapMode == SceneObjectSnapMode.Camera)
                    {
                        // Snap to the exact camera position + user offset
                        if (cam != null)
                        {
                            finalPosition = cam.transform.position + objData.cameraOffset;
                        }
                    }

                    // C. Calculate Rotation
                    switch (objData.rotationMode)
                    {
                        case SceneObjectRotationMode.FullyRelative:
                            if (cam != null && objData.useCameraRelative)
                                finalRotation = cam.transform.rotation * Quaternion.Euler(objData.rotationOffset);
                            else if (cam != null)
                                finalRotation = cam.transform.rotation * Quaternion.Euler(objData.rotation); // If absolute pos but relative rot
                            else
                                finalRotation = Quaternion.Euler(objData.rotation);
                            break;
                            
                        case SceneObjectRotationMode.KeepWorldUp:
                            // Billboard Y
                            float baseYaw = 0f;
                            if (cam != null) baseYaw = cam.transform.eulerAngles.y;
                            
                            float offset = objData.useCameraRelative ? objData.rotationOffset.y : objData.rotation.y;
                            finalRotation = Quaternion.Euler(0f, baseYaw + offset, 0f);
                            break;
                            
                        case SceneObjectRotationMode.FullyAbsolute:
                            // Use the explicit absolute rotation fields
                            finalRotation = Quaternion.Euler(objData.absoluteRotation);
                            // Valid fallback if absoluteRotation was not set in UI but 'rotation' was?
                            // UI shows 'absoluteRotation' when this mode is picked.
                            break;
                            
                        default:
                            finalRotation = Quaternion.Euler(objData.rotation);
                            break;
                    }

                    // APPLY TRANSFORM
                    spawnedObj.transform.position = finalPosition;
                    spawnedObj.transform.rotation = finalRotation;
                    spawnedObj.transform.localScale = finalScale;


                    // --- 3. PARENTING ---
                    // Default container
                    if (assetsContainer != null)
                    {
                        spawnedObj.transform.SetParent(assetsContainer.transform, true);
                    }

                    // Specific Modes
                    // Check for legacy bool OR new enum
                    bool isCameraParent = (objData.parentMode == SceneParentMode.Camera) || objData.parentToCamera;
                    
                    if (isCameraParent && cam != null)
                    {
                        spawnedObj.transform.SetParent(cam.transform, true); // worldPositionStays = true
                    }
                    else if (objData.parentMode == SceneParentMode.Other)
                    {
                        if (!string.IsNullOrEmpty(objData.parentObjectName))
                        {
                            GameObject customParent = GameObject.Find(objData.parentObjectName);
                            if (customParent != null)
                            {
                                spawnedObj.transform.SetParent(customParent.transform, true);
                            }
                            else
                            {
                                Debug.LogWarning($"[Kroma] Parent object '{objData.parentObjectName}' not found. Leaving in default container.");
                            }
                        }
                    }
                    
                    // Register with undo system
                    Undo.RegisterCreatedObjectUndo(spawnedObj, "Spawn Kroma Scene Object");

                    // Auto-Add Camera Follow
                    if (objData.followCamera)
                    {
                        var follow = spawnedObj.GetComponent<KromaCameraFollow>();
                        if (follow == null)
                        {
                            follow = spawnedObj.AddComponent<KromaCameraFollow>();
                        }
                        follow.enabled = true;
                    }

                    // Auto-Simulate Particles in Editor
                    if (spawnedObj.GetComponentInChildren<ParticleSystem>() != null)
                    {
                        KromaParticleController controller = spawnedObj.GetComponent<KromaParticleController>();
                        if (controller == null)
                        {
                            controller = spawnedObj.AddComponent<KromaParticleController>();
                        }
                        
                        // Restart simulation at the new position to avoid (0,0,0) prewarm artifacts
                        controller.RestartSimulation();
                    }

                    // Track for cleanup
                    _spawnedSceneObjects.Add(spawnedObj);
                }
            }
        }


        /// <summary>
        /// Calculate world position from camera-relative spherical coordinates
        /// </summary>
        private Vector3 CalculateCameraRelativePosition(Transform camTransform, float azimuth, float elevation, float distance)
        {
            // Convert spherical coordinates to direction in camera's local space
            float azimuthRad = azimuth * Mathf.Deg2Rad;
            float elevationRad = elevation * Mathf.Deg2Rad;
            
            Vector3 localDirection = new Vector3(
                Mathf.Cos(elevationRad) * Mathf.Sin(azimuthRad),  // Right
                Mathf.Sin(elevationRad),                           // Up
                Mathf.Cos(elevationRad) * Mathf.Cos(azimuthRad)   // Forward
            );
            
            // Convert to world space
            Vector3 worldDirection = camTransform.TransformDirection(localDirection);
            
            // Calculate world position
            return camTransform.position + worldDirection * distance;
        }

        /// <summary>
        /// Get or create "Applied Profile Assets" container under "Kroma Scene Profile"
        /// </summary>
        private GameObject GetOrCreateProfileAssetsContainer()
        {
            // Find Kroma Scene Profile GameObject
            KromaSceneLighting sceneLighting = Object.FindAnyObjectByType<KromaSceneLighting>();
            if (sceneLighting == null)
            {
                Debug.LogWarning("[Kroma] Kroma Scene Profile not found. Scene objects will spawn at world root.");
                return null;
            }

            // Look for existing "Applied Profile Assets" container
            Transform assetsTransform = sceneLighting.transform.Find("Applied Profile Assets");
            if (assetsTransform != null)
            {
                return assetsTransform.gameObject;
            }

            // Create new container
            GameObject assetsContainer = new GameObject("Applied Profile Assets");
            assetsContainer.transform.SetParent(sceneLighting.transform, false);
            Undo.RegisterCreatedObjectUndo(assetsContainer, "Create Applied Profile Assets Container");

            // Debug.Log("[Kroma] Created 'Applied Profile Assets' container under 'Kroma Scene Profile'.");
            return assetsContainer;
        }

        /// <summary>
        /// Cleanup all spawned scene objects
        /// </summary>
        private void CleanupSceneObjects()
        {
            // if (_spawnedSceneObjects.Count == 0) return; // REMOVED: Need to run to check camera revert

            foreach (var obj in _spawnedSceneObjects)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }

            _spawnedSceneObjects.Clear();
            // Debug.Log("[Kroma] Cleaned up spawned scene objects.");
        }

        /// <summary>
        /// Hide applied scene objects during preview
        /// </summary>
        private void HideAppliedSceneObjects()
        {
            _hiddenAppliedObjects.Clear();
            
            KromaSceneLighting sceneLighting = Object.FindAnyObjectByType<KromaSceneLighting>();
            if (sceneLighting == null || sceneLighting.appliedSceneObjects == null) return;

            foreach (var obj in sceneLighting.appliedSceneObjects)
            {
                if (obj != null && obj.activeSelf)
                {
                    // FIX: If we forced overlay, we must remove Main Camera from this object's stack
                    // BEFORE hiding it, otherwise Unity warns "Stack can only contain Overlay cameras"
                    // if the Main Camera is later temporarily set to Base during preview.
                    if (_didForceOverlay)
                    {
                        var objCam = obj.GetComponentInChildren<Camera>();
                        if (objCam != null)
                        {
                            var data = objCam.GetUniversalAdditionalCameraData();
                            if (data.cameraStack.Contains(Camera.main))
                            {
                                data.cameraStack.Remove(Camera.main);
                            }
                        }
                    }

                    obj.SetActive(false);
                    _hiddenAppliedObjects.Add(obj);
                }
            }

            // FIX: If we forced overlay, temporarily revert Main Camera to Base to avoid black screen
            // The Main Camera will be re-configured by StartPreview logic immediately after,
            // but this ensures we don't have a frame where Main is Overlay but host is hidden.
            if (_didForceOverlay && Camera.main != null)
            {
                Camera.main.GetUniversalAdditionalCameraData().renderType = _originalMainCameraType;
                Camera.main.clearFlags = CameraClearFlags.Skybox; 
            }

            if (_hiddenAppliedObjects.Count > 0)
            {
                // Debug.Log($"[Kroma] Hid {_hiddenAppliedObjects.Count} applied scene object(s) for preview.");
            }
        }

        /// <summary>
        /// Show previously hidden applied scene objects after preview
        /// </summary>
        private void ShowAppliedSceneObjects()
        {
            foreach (var obj in _hiddenAppliedObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    
                    // FIX: Re-add Main Camera to stack if needed
                    if (_didForceOverlay)
                    {
                        var objCam = obj.GetComponentInChildren<Camera>();
                        if (objCam != null && Camera.main != null)
                        {
                            var data = objCam.GetUniversalAdditionalCameraData();
                            if (!data.cameraStack.Contains(Camera.main))
                            {
                                data.cameraStack.Add(Camera.main);
                            }
                            // Ensure Main Camera is Overlay (restored by StopPreview, but double check)
                            Camera.main.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                        }
                    }
                }
            }

            if (_hiddenAppliedObjects.Count > 0)
            {
                // Debug.Log($"[Kroma] Restored {_hiddenAppliedObjects.Count} applied scene object(s).");
            }

            _hiddenAppliedObjects.Clear();
        }

        /// <summary>
        /// Save spawned objects to KromaSceneLighting component
        /// </summary>
        private void SaveAppliedSceneObjects()
        {
            KromaSceneLighting sceneLighting = Object.FindAnyObjectByType<KromaSceneLighting>();
            if (sceneLighting == null) return;

            Undo.RecordObject(sceneLighting, "Save Applied Scene Objects");
            sceneLighting.appliedSceneObjects = _spawnedSceneObjects.ToArray();
            EditorUtility.SetDirty(sceneLighting);
            
            _spawnedSceneObjects.Clear(); // Clear preview list since objects are now "applied"
            
            // Debug.Log($"[Kroma] Saved {sceneLighting.appliedSceneObjects.Length} applied scene object(s) to KromaSceneLighting.");
        }

        /// <summary>
        /// Cleanup applied scene objects from KromaSceneLighting component
        /// </summary>
        private void CleanupAppliedSceneObjects()
        {
            KromaSceneLighting sceneLighting = Object.FindAnyObjectByType<KromaSceneLighting>();
            if (sceneLighting == null || sceneLighting.appliedSceneObjects == null) return;

            int count = 0;
            foreach (var obj in sceneLighting.appliedSceneObjects)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                    count++;
                }
            }

            if (count > 0)
            {
                Undo.RecordObject(sceneLighting, "Cleanup Applied Scene Objects");
                sceneLighting.appliedSceneObjects = new GameObject[0];
                EditorUtility.SetDirty(sceneLighting);
                // Debug.Log($"[Kroma] Cleaned up {count} applied scene object(s).");
            }

            // Restore Main Camera State if we forced it
            if (_didForceOverlay)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    mainCam.GetUniversalAdditionalCameraData().renderType = _originalMainCameraType;
                    // Debug.Log("[Kroma] Restored Main Camera render type.");
                }
                _didForceOverlay = false;
            }
        }


        public string GetProfileDisplayName(VolumeProfile volumeProfile)
        {
            if (volumeProfile == null) return "Unknown Profile";

            // Try to find Kroma wrapper
            if (ProfileLibrary != null)
            {
                KromaProfile kProfile = ProfileLibrary.GetProfileByAsset(volumeProfile);
                if (kProfile != null && !string.IsNullOrEmpty(kProfile.profileName))
                {
                    return kProfile.profileName;
                }
            }

            // Fallback to asset name cleaning
            return GetCleanName(volumeProfile.name);
        }

    }

    // Helper class for JSON serialization of favorites
    [System.Serializable]
    internal class FavoritesList
    {
        public List<string> names = new List<string>();
    }
}