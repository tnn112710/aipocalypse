
using UnityEngine;
using UnityEditor;

using UnityEngine.Rendering; // For Volume
using UnityEditorInternal; // For ReorderableList

namespace FayLabs.Kroma.Editor
{
    [CustomEditor(typeof(KromaProfile))]
    public class KromaProfileEditor : UnityEditor.Editor
    {
        // Foldout states
        private bool _foldoutVolume = true;
        private bool _foldoutSun = true;
        private bool _foldoutFog = true;
        private bool _foldoutSceneObjects = true;
        private bool _foldoutIndexing = true;
        
        private ReorderableList _sceneObjectList;

        private void OnEnable()
        {
            // Safety: Auto-clean Volume Profile to prevent "Object at index 0 is null"
            KromaProfile profile = (KromaProfile)target;
            if (profile != null && profile.volumeProfile != null)
            {
                if (profile.volumeProfile.components.RemoveAll(c => c == null) > 0)
                {
                    EditorUtility.SetDirty(profile.volumeProfile);
                    // Defer save to avoid "Objects loaded during domain backup" error
                    EditorApplication.delayCall += () => AssetDatabase.SaveAssets(); 
                }
            }

            SerializedProperty sceneObjectsProp = serializedObject.FindProperty("sceneObjects");
            // Set displayHeader (4th arg) to false to avoid double header inside foldout
            _sceneObjectList = new ReorderableList(serializedObject, sceneObjectsProp, true, false, true, true);
            
            // No drawHeaderCallback needed since header is disabled
            
            _sceneObjectList.onAddCallback = (ReorderableList list) =>
            {
                // Add new element
                int index = list.serializedProperty.arraySize;
                list.serializedProperty.arraySize++;
                list.index = index;
                
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                
                // Set Defaults for new item (Default struct values are often 0)
                element.FindPropertyRelative("prefab").objectReferenceValue = null; // Clear copied prefab
                element.FindPropertyRelative("enabled").boolValue = true;
                element.FindPropertyRelative("scale").vector3Value = Vector3.one;
                element.FindPropertyRelative("raycastDistance").floatValue = 400f;
                // Default to KeepWorldUp (First option in Enum)
                element.FindPropertyRelative("rotationMode").enumValueIndex = 0; 
                // Default to KromaSceneProfile (First option in Enum)
                element.FindPropertyRelative("parentMode").enumValueIndex = 0;
                
                // Clear other fields to be safe
                element.FindPropertyRelative("position").vector3Value = Vector3.zero;
                element.FindPropertyRelative("rotation").vector3Value = Vector3.zero;
                element.FindPropertyRelative("parentObjectName").stringValue = "";
            };

            _sceneObjectList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = _sceneObjectList.serializedProperty.GetArrayElementAtIndex(index);
                
                // Fix overlap: ReorderableList drag handle is on the left. 
                // We need to shift the content slightly right.
                // Standard drag handle width is around 20px. IndentLevel handles some, but RL ignores it for element rect.
                // Let's manually offset the rect.
                
                rect.x += 10;
                rect.width -= 10;
                
                EditorGUI.PropertyField(rect, element, true);
            };
            
            _sceneObjectList.elementHeightCallback = (int index) =>
            {
                SerializedProperty element = _sceneObjectList.serializedProperty.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, true);
            };
            
            EditorApplication.delayCall += CheckInstanceWarning;
        }

        private void DrawTopHeader()
        {
            Texture2D headerIcon = null;
            string[] headerGuids = AssetDatabase.FindAssets("Kroma_Scene_Header t:Texture2D");
            if (headerGuids.Length > 0)
                headerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(headerGuids[0]));

            float headerHeight = 40f;
            Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
            headerRect.xMin = 0;
            headerRect.width = EditorGUIUtility.currentViewWidth;

            Color bgColor;
            ColorUtility.TryParseHtmlString("#191919", out bgColor);
            EditorGUI.DrawRect(headerRect, bgColor);

            if (headerIcon != null)
            {
                float aspect = (float)headerIcon.width / (float)headerIcon.height;
                float imageWidth = headerHeight * aspect;
                Rect imageRect = new Rect(headerRect.x, headerRect.y, imageWidth, headerHeight);
                GUI.DrawTexture(imageRect, headerIcon, ScaleMode.ScaleToFit);
            }

            // Use profileName field, fall back to clean asset name
            KromaProfile kProfile = (KromaProfile)target;
            string displayName = !string.IsNullOrEmpty(kProfile.profileName)
                ? kProfile.profileName
                : KromaLightingUtils.GetCleanProfileName(kProfile.name);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.label);
            headerStyle.fontSize = 13;
            headerStyle.alignment = TextAnchor.MiddleRight;
            headerStyle.normal.textColor  = Color.white;
            headerStyle.hover.textColor   = Color.white;
            headerStyle.active.textColor  = Color.white;
            headerStyle.focused.textColor = Color.white;

            Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - 15f, headerHeight);
            GUI.Label(labelRect, displayName.ToUpper(), headerStyle);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Top Header Banner
            DrawTopHeader();
            GUILayout.Space(-4); // Collapse the gap between header bar and the banner image
            
            // Draw Thumbnail Banner if available
            SerializedProperty thumbnailProp = serializedObject.FindProperty("thumbnail");
            if (thumbnailProp.objectReferenceValue != null)
            {
                Texture2D bannerTex = (Texture2D)thumbnailProp.objectReferenceValue;
                if (bannerTex != null)
                {
                    float bannerHeight = 130f; // Match KromaSceneLightingEditor banner height
                    float viewWidth = EditorGUIUtility.currentViewWidth;

                    Rect bannerRect = EditorGUILayout.GetControlRect(false, bannerHeight);
                    bannerRect.x = 0;
                    bannerRect.width = viewWidth;
                    
                    // Draw formatting - ScaleToFit means it focuses on aspect ratio, ScaleAndCrop fills. 
                    // ScaleToFit is safer, but ScaleAndCrop looks more "Banner-like" if we clamped height.
                    EditorGUI.DrawPreviewTexture(bannerRect, bannerTex, null, ScaleMode.ScaleAndCrop);
                    
                    // Add a separator line right under the banner
                    Rect lineRect = new Rect(0, bannerRect.yMax, viewWidth, 1);
                    EditorGUI.DrawRect(lineRect, Color.black);
                    
                    EditorGUILayout.Space(5);
                }
            }

            // 1. SKYBOX & POST-PROCESSING
            _foldoutVolume = EditorGUILayout.Foldout(_foldoutVolume, "Skybox & Post-Processing", true, EditorStyles.foldoutHeader);
            if (_foldoutVolume)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(8);
                
                // Check for duplicate usage
                DrawInstanceWarning((KromaProfile)target);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("skyboxMaterial"));
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeProfile"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            DrawSeparator();

            // 2. DIRECT LIGHT
            EditorGUILayout.BeginHorizontal();
            _foldoutSun = EditorGUILayout.Foldout(_foldoutSun, "Direct Light", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("Capture from Scene", GUILayout.Width(130), GUILayout.Height(18)))
            {
                CaptureSunPosition((KromaProfile)target);
            }
            EditorGUILayout.EndHorizontal();
            if (_foldoutSun)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(8);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("configureSunLight"));
                if (serializedObject.FindProperty("configureSunLight").boolValue)
                {
                    SerializedProperty camRelProp = serializedObject.FindProperty("cameraRelativeSun");
                    EditorGUILayout.PropertyField(camRelProp);
                    
                    if (camRelProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sunAzimuth"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("sunElevation"));
                    }
                    else
                    {
                         EditorGUILayout.PropertyField(serializedObject.FindProperty("sunRotation"));
                    }
                    
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("sunColor"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("sunIntensity"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowType"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowStrength"));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            DrawSeparator();

            // 3. FOG & AMBIENT LIGHT
            EditorGUILayout.BeginHorizontal();
            _foldoutFog = EditorGUILayout.Foldout(_foldoutFog, "Fog \u0026 Ambient Light", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("Capture from Scene", GUILayout.Width(130), GUILayout.Height(18)))
            {
                CaptureFogDetails((KromaProfile)target);
            }
            EditorGUILayout.EndHorizontal();
            if (_foldoutFog)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(8);
                // Environment Source
                SerializedProperty sourceProp = serializedObject.FindProperty("environmentSource");
                // Custom Dropdown for naming consistency with Unity Lighting Window
                GUIContent[] sourceOptions = new GUIContent[] {
                    new GUIContent("Skybox"),
                    new GUIContent("Gradient"),
                    new GUIContent("Color")
                };
                int[] sourceValues = new int[] { 0, 1, 3 }; // Skybox=0, Trilight=1, Flat=3

                int currentSource = sourceProp.intValue;
                int newSource = EditorGUILayout.IntPopup(new GUIContent("Source"), currentSource, sourceOptions, sourceValues);
                if (newSource != currentSource)
                {
                    sourceProp.intValue = newSource;
                }
                
                UnityEngine.Rendering.AmbientMode ambientMode = (UnityEngine.Rendering.AmbientMode)sourceProp.intValue;
                
                if (ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("environmentMultiplier"), new GUIContent("Intensity Multiplier"));
                }
                else if (ambientMode == UnityEngine.Rendering.AmbientMode.Trilight) // Gradient
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientSkyColor"), new GUIContent("Sky Color"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientEquatorColor"), new GUIContent("Equator Color"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientGroundColor"), new GUIContent("Ground Color"));
                }
                else if (ambientMode == UnityEngine.Rendering.AmbientMode.Flat) // Color
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientColor"), new GUIContent("Ambient Color"));
                }
                
                EditorGUILayout.Space(10);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fogEnabled"));
                if (serializedObject.FindProperty("fogEnabled").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fogMode"), new GUIContent("Fog type"));
                    
                    // Fixed Fog Logic using int value of enum
                    int modeInt = serializedObject.FindProperty("fogMode").intValue;
                    FogMode mode = (FogMode)modeInt;
                    
                    if (mode == FogMode.Linear)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fogStartDistance"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fogEndDistance"));
                    }
                    else // Exponential or ExponentialSquared
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fogDensity"));
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            DrawSeparator();

            // 4. SCENE OBJECTS
            _foldoutSceneObjects = EditorGUILayout.Foldout(_foldoutSceneObjects, "Atmosphere Prefabs", true, EditorStyles.foldoutHeader);
            if (_foldoutSceneObjects)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(8); // Margin between title and list
                _sceneObjectList.DoLayoutList();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            DrawSeparator();

            // 5. PROFILE INDEXING
            _foldoutIndexing = EditorGUILayout.Foldout(_foldoutIndexing, "Profile Indexing", true, EditorStyles.foldoutHeader);
            if (_foldoutIndexing)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(6);
                
                // Smart Rename Logic
                SerializedProperty nameProp = serializedObject.FindProperty("profileName");
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.DelayedTextField(new GUIContent("Profile Name", "Edit to rename asset file automatically."), nameProp.stringValue);
                
                if (EditorGUI.EndChangeCheck())
                {
                    nameProp.stringValue = newName;
                    serializedObject.ApplyModifiedProperties(); // Apply immediately so we can read it
                    
                    // Rename Asset File
                    string assetPath = AssetDatabase.GetAssetPath(target);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        string directory = System.IO.Path.GetDirectoryName(assetPath);
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                        
                        string prefix = "";
                        string[] parts = fileName.Split('_');
                        
                        // Check for naming convention: Kroma_MyProfile_XX_[name] or Kroma_Profile_XX_[name]
                        if (fileName.StartsWith("Kroma_MyProfile_") || fileName.StartsWith("Kroma_Profile_"))
                        {
                            if (parts.Length >= 3 && int.TryParse(parts[2], out int _))
                            {
                                prefix = $"{parts[0]}_{parts[1]}_{parts[2]}_";
                            }
                        }
                        // Fallback to old naming convention: Profile_XX_...
                        else if (fileName.StartsWith("Profile_"))
                        {
                             if (parts.Length >= 2 && int.TryParse(parts[1], out int _))
                             {
                                 prefix = $"Profile_{parts[1]}_";
                             }
                        }
                        
                        string safeName = newName.Replace(" ", ""); // No spaces in file names
                        string newFileName;
                        
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            newFileName = $"{prefix}{safeName}";
                        }
                        else
                        {
                            newFileName = safeName;
                        }
                        
                        // Only rename if changed
                        if (fileName != newFileName)
                        {
                            AssetDatabase.RenameAsset(assetPath, newFileName);
                            AssetDatabase.SaveAssets(); // Ensure it sticks
                        }
                    }
                }
                // Thumbnail + Capture Button
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("thumbnail"), new GUIContent("Profile Thumbnail"));
                
                GUIContent btnContent = EditorGUIUtility.IconContent("Camera Icon");
                btnContent.text = " Capture";
                if (GUILayout.Button(btnContent, GUILayout.Width(80), GUILayout.Height(18)))
                {
                    // Use new interactive capture tool
                    KromaThumbnailCapture.BeginCapture((KromaProfile)target);
                }
                EditorGUILayout.EndHorizontal();

                // Hidden — not currently used, kept for future use
                // EditorGUILayout.PropertyField(serializedObject.FindProperty("category"), new GUIContent("Profile Category"));
                // EditorGUILayout.PropertyField(serializedObject.FindProperty("timeOfDay"), new GUIContent("Profile Time"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("searchTags"));
                EditorGUI.indentLevel--;
            }
            DrawSeparator();

            EditorGUILayout.Space(10);
            
            // 6. TOOLS (Buttons at bottom) - ALWAYS VISIBLE
            /* Capture Volume — commented out, kept for potential future use
            if (GUILayout.Button("Capture Volume", GUILayout.Height(25)))
            {
                CaptureVolume((KromaProfile)target);
            }
            */

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Profile", GUILayout.Height(30)))
            {
                KromaProfile profile = (KromaProfile)target;
                KromaLightingApplier.ApplyLightingSettings(profile);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawSeparator()
        {
            EditorGUILayout.Space(5);
            // Get a rect that spans the full width of the window, ignoring padding
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.x = 0;
            rect.width = EditorGUIUtility.currentViewWidth;
            EditorGUI.DrawRect(rect, Color.black);
            EditorGUILayout.Space(5);
        }

        private void CaptureSunPosition(KromaProfile profile)
        {
            KromaLightingUtils.CaptureSunPosition(profile);
        }

        private void CaptureFogDetails(KromaProfile profile)
        {
            KromaLightingUtils.CaptureFogDetails(profile);
        }
        private void CaptureVolume(KromaProfile profile)
        {
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            
            // Filter and Sort Volumes
            // We want Global Volumes, Enabled, Active, Not Hidden/Preview
            var validVolumes = new System.Collections.Generic.List<Volume>();
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            foreach (var vol in volumes)
            {
                // Strict Check: Must belong to the ACTIVE SCENE
                if (vol.gameObject.scene != activeScene) 
                {
                    // Debug.Log($"[Kroma] Unwanted volume found in scene '{vol.gameObject.scene.name}': {vol.name} (Ignored)");
                    continue;
                }

                // Skip if disabled or inactive
                if (!vol.enabled || !vol.gameObject.activeInHierarchy) continue;
                
                // Skip Kroma's Ghost Preview Volume (double check name variants)
                if (vol.name.Contains("Kroma_Preview") || vol.name.Contains("Ghost")) continue;
                if ((vol.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;

                // We are looking for the 'Main' Global Volume usually
                if (vol.isGlobal)
                {
                    validVolumes.Add(vol);
                }
            }

            // Sort by Priority (Descending: Highest priority wins)
            validVolumes.Sort((a, b) => b.priority.CompareTo(a.priority));

            if (validVolumes.Count == 0)
            {
                 // No valid volumes found
                 try 
                 {
                     EditorWindow.focusedWindow.ShowNotification(new GUIContent("No active Global Volume found in Active Scene!"));
                 }
                 catch
                 {
                     Debug.LogWarning("[Kroma] No active Global Volume found in Active Scene!");
                 }
            }
            else
            {
                // We found at least one. If more than one, we pick the highest priority one.
                Volume bestVolume = validVolumes[0];
                
                // Warn if ambiguity exists
                if (validVolumes.Count > 1)
                {
                    Debug.LogWarning($"[Kroma] Found {validVolumes.Count} active global volumes. Capturing '{bestVolume.name}' (Priority: {bestVolume.priority}). Other candidates were ignored.");
                }

                // Capture
                Undo.RecordObject(profile, "Capture Volume Profile");
                profile.volumeProfile = bestVolume.sharedProfile;
                
                // Auto-fill Profile Name if empty (or always update? User said "can be overwritten", suggesting prefill logic)
                // Let's prefill:
                if (bestVolume.sharedProfile != null)
                {
                    profile.profileName = bestVolume.sharedProfile.name;
                }

                EditorUtility.SetDirty(profile);
                
                try 
                {
                     // Show clearer notification
                     EditorWindow.focusedWindow.ShowNotification(new GUIContent($"Captured: {bestVolume.name}"));
                }
                catch
                {
                     // Debug.Log($"[Kroma] Volume Profile Captured from '{bestVolume.name}'");
                }
                
                // Force Inspector Repaint?
            }
        }


        private bool _hasInstanceWarning = false;
        private string _instanceWarningMessage = "";

        private void CheckInstanceWarning()
        {
            KromaProfile current = (KromaProfile)target;
            if (current.volumeProfile == null) 
            {
                _hasInstanceWarning = false;
                return;
            }
            
            // Find library - we look for ANY usage in other profiles
            string[] guids = AssetDatabase.FindAssets("t:KromaProfileLibrary");
            if (guids.Length == 0) return;
            
            // Just load the first library found (usually there is only one)
            string libPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            KromaProfileLibrary lib = AssetDatabase.LoadAssetAtPath<KromaProfileLibrary>(libPath);
            
            if (lib != null && lib.profiles != null)
            {
                foreach (var p in lib.profiles)
                {
                    // Check if another profile uses the SAME volume profile asset
                    if (p != null && p != current && p.volumeProfile == current.volumeProfile)
                    {
                        _hasInstanceWarning = true;
                        _instanceWarningMessage = "This volume profile is already in use. Assign an unused profile or duplicate the existing one.";
                        return;
                    }
                }
            }
            _hasInstanceWarning = false;
        }

        private void DrawInstanceWarning(KromaProfile current)
        {
             if (_hasInstanceWarning)
             {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(_instanceWarningMessage, MessageType.Warning);
                EditorGUILayout.Space(5);
             }
        }


    }


    /// <summary>
    /// Custom property drawer for SceneObjectConfig to add capture tool
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneObjectConfig))]
    public class SceneObjectConfigDrawer : PropertyDrawer
    {
        private static readonly System.Collections.Generic.Dictionary<string, GameObject> _tempSourceObjects = new System.Collections.Generic.Dictionary<string, GameObject>();

            // Calculate Dynamic Height properly
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
                
                float height = EditorGUIUtility.singleLineHeight + 2; // Foldout header
                
                SerializedProperty prefabProp = property.FindPropertyRelative("prefab");
                bool hasPrefab = prefabProp != null && prefabProp.objectReferenceValue != null;
                
                if (!hasPrefab)
                {
                    // Source object row
                    height += 5; 
                    height += EditorGUIUtility.singleLineHeight + 2;
                }
                else
                {
                    // Prefab row with remove button
                    height += 5;
                    height += EditorGUIUtility.singleLineHeight + 4;
                    
                    // Camera Relative
                    height += EditorGUIUtility.singleLineHeight + 2;
                    SerializedProperty camRelProp = property.FindPropertyRelative("useCameraRelative");
                    if (camRelProp.boolValue)
                    {
                        height += EditorGUIUtility.singleLineHeight + 2; // info string
                    }
                    else
                    {
                        height += (EditorGUIUtility.singleLineHeight + 2) * 3; // Pos, Rot, Scale
                    }
                    
                    // Snap mode dropdown
                    height += EditorGUIUtility.singleLineHeight + 2;
                    SerializedProperty snapModeProp = property.FindPropertyRelative("snapMode");
                    int snapModeVal = snapModeProp.enumValueIndex;
                    if (snapModeVal == 1) // Ground
                    {
                        height += EditorGUIUtility.singleLineHeight + 2; // Ground Y Offset
                    }
                    else if (snapModeVal == 2) // Camera
                    {
                        height += EditorGUIUtility.singleLineHeight + 2; // Camera Offset
                    }
                    
                    height += 2; // spacing
                    
                    // Rotation mode
                    height += EditorGUIUtility.singleLineHeight + 2;
                    SerializedProperty rotModeProp = property.FindPropertyRelative("rotationMode");
                    if (rotModeProp.enumValueIndex == 2) // Absolute
                    {
                        height += EditorGUIUtility.singleLineHeight + 2;
                    }
                    
                    height += 2; // spacing
                    
                    // Parent mode
                    height += EditorGUIUtility.singleLineHeight + 2;
                    SerializedProperty parentModeProp = property.FindPropertyRelative("parentMode");
                    if (parentModeProp.enumValueIndex == 2) // Other
                    {
                        height += EditorGUIUtility.singleLineHeight + 2;
                    }
                    
                    // Follow Camera
                    height += EditorGUIUtility.singleLineHeight + 2;
                }
                
                height += 15; // Increased Bottom padding
                return height;
            }

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);

                // Custom Label
                SerializedProperty prefabProp = property.FindPropertyRelative("prefab");
                GUIContent customLabel = new GUIContent(label);
                
                bool hasPrefab = prefabProp != null && prefabProp.objectReferenceValue != null;
                
                if (hasPrefab)
                {
                    string rawName = prefabProp.objectReferenceValue.name;
                    // Strip "Kroma_" prefix and "_Prefab" suffix
                    if (rawName.StartsWith("Kroma_")) rawName = rawName.Substring(6);
                    if (rawName.EndsWith("_Prefab")) rawName = rawName.Substring(0, rawName.Length - 7);
                    // Strip any remaining leading type segment (e.g. "Particle_", "Object_")
                    int firstUnderscore = rawName.IndexOf('_');
                    if (firstUnderscore > 0) rawName = rawName.Substring(firstUnderscore + 1);
                    customLabel.text = rawName;
                }
                else
                {
                    customLabel.text = "New Item";
                }

                // Draw foldout
                float y = position.y;
                Rect foldoutRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, customLabel, true);
                y += EditorGUIUtility.singleLineHeight + 2;

                if (property.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    
                    // Enforce Raycast Distance
                    SerializedProperty raycastProp = property.FindPropertyRelative("raycastDistance");
                    if (raycastProp != null) raycastProp.floatValue = 400f;

                    if (!hasPrefab)
                    {
                        // --- GROUP A: OBJECT SETUP (ONLY SHOWN IF NO PREFAB IS SET) ---
                        y += 5;
                        Rect sourceRowRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        float btnWidth = 60; // Compact width for "Grab" label
                        Rect objFieldRect = new Rect(sourceRowRect.x, sourceRowRect.y, sourceRowRect.width - btnWidth - 4, sourceRowRect.height);
                        Rect captureBtnRect = new Rect(sourceRowRect.xMax - btnWidth, sourceRowRect.y, btnWidth, sourceRowRect.height);
                        
                        string key = property.propertyPath;
                        GameObject sourceObject = null;
                        _tempSourceObjects.TryGetValue(key, out sourceObject);
                        
                        EditorGUI.BeginChangeCheck();
                        GameObject newSourceObject = EditorGUI.ObjectField(objFieldRect, "Source Object", sourceObject, typeof(GameObject), true) as GameObject;
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (newSourceObject != null) _tempSourceObjects[key] = newSourceObject;
                            else _tempSourceObjects.Remove(key);
                            sourceObject = newSourceObject;
                        }
                        
                        // Button with Icon and Text
                        GUIContent btnIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit"); 
                        btnIcon.text = " Grab";
                        btnIcon.tooltip = "Grab Details from Source";
                        
                        if (GUI.Button(captureBtnRect, btnIcon))
                        {
                            if (sourceObject != null)
                            {
                                CaptureObjectDetails(property, sourceObject);
                                _tempSourceObjects.Remove(key);
                                GUI.FocusControl(null); // Unfocus to refresh
                            }
                        }
                        y += EditorGUIUtility.singleLineHeight + 2;
                    }
                    else
                    {
                        // --- PREFAB PLACEMENT FIRST ---
                        y += 5;
                        Rect prefabRowRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        float removeBtnWidth = 70;
                        Rect prefabFieldRect = new Rect(prefabRowRect.x, prefabRowRect.y, prefabRowRect.width - removeBtnWidth - 4, prefabRowRect.height);
                        Rect removeBtnRect = new Rect(prefabRowRect.xMax - removeBtnWidth, prefabRowRect.y, removeBtnWidth, prefabRowRect.height);
                        
                        EditorGUI.PropertyField(prefabFieldRect, prefabProp);
                        
                        Color originalColor = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // Soft Red
                        if (GUI.Button(removeBtnRect, "Remove"))
                        {
                            prefabProp.objectReferenceValue = null;
                            GUI.FocusControl(null); // Unfocus to avoid orphaned property errors
                        }
                        GUI.backgroundColor = originalColor;
                        y += EditorGUIUtility.singleLineHeight + 4;
                        
                        // --- GROUP B: PLACEMENT (SMART SECTION) ---
                        // Camera Relative
                        SerializedProperty camRelProp = property.FindPropertyRelative("useCameraRelative");
                        Rect camRelRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(camRelRect, camRelProp, new GUIContent("Camera Relative"));
                        y += EditorGUIUtility.singleLineHeight + 2;
                        
                        if (camRelProp.boolValue)
                        {
                            // Azimuth, Elevation, Distance
                            SerializedProperty azProp = property.FindPropertyRelative("azimuth");
                            SerializedProperty elProp = property.FindPropertyRelative("elevation");
                            SerializedProperty distProp = property.FindPropertyRelative("distance");
                            
                            Rect infoRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            
                            // Format explicitly as requested: "Azimuth: -17.02 | Elevation: -3.01 | Distance: 17.69"
                            string infoText = $"Azimuth: {azProp.floatValue:F2} | Elevation: {elProp.floatValue:F2} | Distance: {distProp.floatValue:F2}";
                            
                            // Draw as disabled label to make it clear it's read-only info
                            GUI.enabled = false;
                            EditorGUI.LabelField(infoRect, infoText);
                            GUI.enabled = true;
                            
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }
                        else
                        {
                            // Standard Position, Rotation, Scale
                            SerializedProperty posProp = property.FindPropertyRelative("position");
                            SerializedProperty rotProp = property.FindPropertyRelative("rotation");
                            SerializedProperty sclProp = property.FindPropertyRelative("scale");
                            
                            Rect posRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(posRect, posProp);
                            y += EditorGUIUtility.singleLineHeight + 2;
                            
                            Rect rotRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(rotRect, rotProp);
                            y += EditorGUIUtility.singleLineHeight + 2;
                            
                            Rect sclRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(sclRect, sclProp);
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }
                        
                        // Snap Mode
                        SerializedProperty snapModePropGUI = property.FindPropertyRelative("snapMode");
                        Rect snapRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(snapRect, snapModePropGUI, new GUIContent("Snap to"));
                        y += EditorGUIUtility.singleLineHeight + 2;
                        
                        int snapModeValGUI = snapModePropGUI.enumValueIndex;
                        if (snapModeValGUI == 1) // Ground
                        {
                            SerializedProperty offsetProp = property.FindPropertyRelative("groundOffset");
                            Rect offsetRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(offsetRect, offsetProp, new GUIContent("Ground Y Offset"));
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }
                        else if (snapModeValGUI == 2) // Camera
                        {
                            SerializedProperty camOffsetProp = property.FindPropertyRelative("cameraOffset");
                            Rect camOffsetRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(camOffsetRect, camOffsetProp, new GUIContent("Camera Offset"));
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }
                        
                        y += 2; // Extra space
                        
                        // --- GROUP C: ROTATION CONSTRAINTS (SMART SECTION) ---
                        SerializedProperty rotModeProp = property.FindPropertyRelative("rotationMode");
                        Rect rotModeRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(rotModeRect, rotModeProp);
                        y += EditorGUIUtility.singleLineHeight + 2;
                        
                        // If Fully Absolute, show Absolute Rotation fields
                        // SceneObjectRotationMode.FullyAbsolute is index 2 (from definition, verified in KromaProfile Step 1159)
                        if (rotModeProp.enumValueIndex == 2)
                        {
                            SerializedProperty absRotProp = property.FindPropertyRelative("absoluteRotation");
                            Rect absRotRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(absRotRect, absRotProp, new GUIContent("Absolute Rotation"));
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }
                        
                        y += 2;
                        
                        // --- GROUP D: HIERARCHY ---
                        SerializedProperty parentModeProp = property.FindPropertyRelative("parentMode");
                        Rect parentModeRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(parentModeRect, parentModeProp, new GUIContent("Child to"));
                        y += EditorGUIUtility.singleLineHeight + 2;
                        
                        // If Other, show name field
                        // SceneParentMode.Other is index 2 (verified Step 1159)
                        if (parentModeProp.enumValueIndex == 2)
                        {
                            SerializedProperty parentNameProp = property.FindPropertyRelative("parentObjectName");
                            Rect parentNameRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.PropertyField(parentNameRect, parentNameProp, new GUIContent("Parent Name"));
                            y += EditorGUIUtility.singleLineHeight + 2;
                        }

                        // Follow Camera (Added at the end)
                        SerializedProperty followCamProp = property.FindPropertyRelative("followCamera");
                        Rect followCamRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(followCamRect, followCamProp, new GUIContent("Follow Camera"));
                        y += EditorGUIUtility.singleLineHeight + 2;
                    } // end if (hasPrefab)
                    
                    y += 5; // Bottom padding
                EditorGUI.indentLevel--;
            }

            // Draw a subtle separator line at the bottom of the item's allocated rect
            Rect separatorRect = new Rect(position.x, position.yMax - 1, position.width, 1);
            EditorGUI.DrawRect(separatorRect, new Color(0.1f, 0.1f, 0.1f, 0.4f));

            EditorGUI.EndProperty();
        }



        private void CaptureObjectDetails(SerializedProperty property, GameObject sourceObj)
        {
            if (sourceObj == null) return;

            // 1. Find prefab in Profiles_Assets folder
            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(sourceObj);
            
            if (prefab == null)
            {
                // Search by name globally in the project
                string[] guids = AssetDatabase.FindAssets(
                    sourceObj.name + " t:GameObject"
                );
                
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Prefab Not Found",
                        $"Could not find prefab for '{sourceObj.name}' anywhere in the project.\n\nMake sure the object is a prefab instance or has a matching prefab saved.",
                        "OK"
                    );
                    return;
                }
            }

            // 2. Get MainCamera
            Camera cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog(
                    "MainCamera Not Found",
                    "Could not find MainCamera in the scene. Please ensure a camera is tagged as 'MainCamera'.",
                    "OK"
                );
                return;
            }

            // 3. Calculate camera-relative position (spherical coordinates)
            Vector3 toObject = sourceObj.transform.position - cam.transform.position;
            float distance = toObject.magnitude;
            Vector3 direction = toObject.normalized;

            // Convert to camera's local space
            Vector3 localDir = cam.transform.InverseTransformDirection(direction);
            float azimuth = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float elevation = Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg;

            // 4. Calculate rotation offset (relative to camera)
            Quaternion rotOffset = Quaternion.Inverse(cam.transform.rotation) * sourceObj.transform.rotation;

            // 5. Update properties
            property.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            property.FindPropertyRelative("useCameraRelative").boolValue = true;
            property.FindPropertyRelative("azimuth").floatValue = azimuth;
            property.FindPropertyRelative("elevation").floatValue = elevation;
            property.FindPropertyRelative("distance").floatValue = distance;
            property.FindPropertyRelative("rotationOffset").vector3Value = rotOffset.eulerAngles;
            
            // Also set absolute backups
            property.FindPropertyRelative("position").vector3Value = sourceObj.transform.position;
            property.FindPropertyRelative("rotation").vector3Value = sourceObj.transform.eulerAngles;
            property.FindPropertyRelative("scale").vector3Value = sourceObj.transform.localScale;
            
            // Auto Set Rotation Mode? (Default to Fully Relative as per general expectation when capturing relative)
            // But user defaults new profiles to KeepWorldUp.
            // If capturing, we usually want what we see. 
            // If we captured azimuth/elevation (Relative), we probably want FullyRelative?
            // Existing code defaults to nothing specific. 
            // I'll leave it as is or set manual overrides if needed.
        }
    }
}
