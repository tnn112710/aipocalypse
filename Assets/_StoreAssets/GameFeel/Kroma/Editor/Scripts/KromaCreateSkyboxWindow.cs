using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    public class KromaCreateSkyboxWindow : EditorWindow
    {
        private string _targetFolder;
        private string _newName;
        private bool _switchSkybox = true;
        private bool _switchEnvironmentLighting = true;
        private int _selectedPresetIndex = 0;
        private GUIStyle _titleStyle;
        private Texture2D _defaultThumbnail;
        private Texture2D _presetPreviewTexture;

        private List<KromaShaderPreset> _presets = new List<KromaShaderPreset>();
        private List<string> _presetNames = new List<string>();

        public static void ShowPrompt(string targetFolder)
        {
            KromaCreateSkyboxWindow window = GetWindow<KromaCreateSkyboxWindow>("Create Kroma Skybox");
            window._targetFolder = targetFolder;
            window._newName = "Kroma_Skybox";
            window._selectedPresetIndex = 0;
            
            // Adjust window size nicely
            window.minSize = new Vector2(350, 345);
            window.maxSize = new Vector2(350, 345);
            window.CenterOnMainWin();
        }

        private void OnEnable()
        {
            // Load Default Banner
            string[] thumbGuids = AssetDatabase.FindAssets(KromaConstants.DEFAULT_THUMBNAIL_NAME);
            if (thumbGuids.Length > 0)
            {
                string thumbPath = AssetDatabase.GUIDToAssetPath(thumbGuids[0]);
                _defaultThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbPath);
            }

            LoadAllMasterPresets();
            
            // Set default selection to "01. Daylight" (or "01_Daylight" or containing "Daylight")
            _selectedPresetIndex = 0; // fallback default
            for (int i = 0; i < _presetNames.Count; i++)
            {
                if (_presetNames[i].Contains("Daylight") || _presetNames[i].StartsWith("01"))
                {
                    _selectedPresetIndex = i;
                    break;
                }
            }

            UpdatePresetPreview();
        }

        private void LoadAllMasterPresets()
        {
            _presets.Clear();
            _presetNames.Clear();

            // Scan Content/Shaders/Presets/Master or all KromaShaderPresets
            string searchFolder = Path.Combine(KromaAssetManager.GetKromaRootPath(), "Content/Shaders/Presets/Master").Replace("\\", "/");
            string[] guids;
            
            if (AssetDatabase.IsValidFolder(searchFolder))
            {
                guids = AssetDatabase.FindAssets("t:KromaShaderPreset", new[] { searchFolder });
            }
            else
            {
                guids = AssetDatabase.FindAssets("t:KromaShaderPreset");
            }

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KromaShaderPreset preset = AssetDatabase.LoadAssetAtPath<KromaShaderPreset>(path);
                if (preset != null && (preset.sectionName == "Master" || path.Contains("/Master/")))
                {
                    _presets.Add(preset);
                    _presetNames.Add(preset.name);
                }
            }
        }

        private void UpdatePresetPreview()
        {
            _presetPreviewTexture = null;

            if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presets.Count)
            {
                string presetName = _presets[_selectedPresetIndex].name;
                string kromaRoot = KromaAssetManager.GetKromaRootPath();
                string[] kromaSearchFolder = new string[] { kromaRoot };
                
                // Naming search conventions:
                // 1. Kroma_Preset_Thumbnail_[PresetName]
                // 2. [PresetName]_Thumbnail
                // 3. [PresetName]
                
                // Tier 1: Search restricted to Kroma folder (very fast)
                string[] guids = AssetDatabase.FindAssets($"Kroma_Preset_Thumbnail_{presetName} t:Texture2D", kromaSearchFolder);
                if (guids.Length == 0)
                {
                    guids = AssetDatabase.FindAssets($"{presetName}_Thumbnail t:Texture2D", kromaSearchFolder);
                }
                if (guids.Length == 0)
                {
                    guids = AssetDatabase.FindAssets($"{presetName} t:Texture2D", kromaSearchFolder);
                }

                // Tier 2: Search project-wide if not found in Kroma folder
                if (guids.Length == 0)
                {
                    guids = AssetDatabase.FindAssets($"Kroma_Preset_Thumbnail_{presetName} t:Texture2D");
                    if (guids.Length == 0)
                    {
                        guids = AssetDatabase.FindAssets($"{presetName}_Thumbnail t:Texture2D");
                    }
                    if (guids.Length == 0)
                    {
                        guids = AssetDatabase.FindAssets($"{presetName} t:Texture2D");
                    }
                }

                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _presetPreviewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
        }

        private void OnGUI()
        {
            // --- 1. BANNER & OVERLAY ---
            float bannerHeight = 150f;
            Rect bannerRect = new Rect(0, 0, position.width, bannerHeight);

            // Draw current active preview or fallback
            Texture2D activeBanner = _presetPreviewTexture != null ? _presetPreviewTexture : _defaultThumbnail;
            if (activeBanner != null)
            {
                GUI.DrawTexture(bannerRect, activeBanner, ScaleMode.StretchToFill);
            }
            else
            {
                EditorGUI.DrawRect(bannerRect, new Color(0.15f, 0.15f, 0.15f));
            }



            // --- 2. CONTENT AREA ---
            float padding = 15f;
            Rect contentRect = new Rect(padding, bannerHeight + padding, position.width - (padding * 2), position.height - bannerHeight - padding);
            
            GUILayout.BeginArea(contentRect);
            
            GUILayout.Label("New Skybox Name:", EditorStyles.label);
            GUILayout.Space(4);
            
            GUI.SetNextControlName("NameField");
            _newName = EditorGUILayout.TextField(_newName, GUILayout.Height(20));
            
            GUILayout.Space(8);

            // --- PRESET DROPDOWN ---
            int prevPresetIndex = _selectedPresetIndex;
            _selectedPresetIndex = EditorGUILayout.Popup("Choose Preset:", _selectedPresetIndex, _presetNames.ToArray());
            if (_selectedPresetIndex != prevPresetIndex)
            {
                UpdatePresetPreview();
            }

            GUILayout.Space(8);
            _switchSkybox = EditorGUILayout.ToggleLeft(" Switch to this new Skybox", _switchSkybox, EditorStyles.boldLabel);
            
            // Conditional Checkbox for Switch Environment Lighting
            bool showSwitchLightingToggle = _switchSkybox && RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Skybox;
            if (showSwitchLightingToggle)
            {
                GUILayout.Space(8);
                _switchEnvironmentLighting = EditorGUILayout.ToggleLeft(" Switch Environment Lighting to Skybox", _switchEnvironmentLighting, EditorStyles.boldLabel);
            }
            
            GUILayout.Space(15); 

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Create", GUILayout.Height(30)))
            {
                if (!string.IsNullOrEmpty(_newName))
                {
                    CreateSkyboxAsset();
                    Close();
                }
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndArea();
            
            if (Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("NameField");
            }
        }

        private void CreateSkyboxAsset()
        {
            KromaAssetManager.EnsurePathExists(_targetFolder);

            // Clean the name of spaces
            string cleanName = _newName.Replace(" ", "");
            string fullPath = $"{_targetFolder}/{cleanName}.mat";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

            Shader skyboxShader = Shader.Find(KromaConstants.SKYBOX_SHADER_NAME);
            if (skyboxShader == null)
            {
                Debug.LogError($"{KromaConstants.LOG_PREFIX} Could not find shader '{KromaConstants.SKYBOX_SHADER_NAME}'. Material created with default shader.");
                return;
            }

            Material mat = new Material(skyboxShader);

            // Apply selected preset if any
            if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presets.Count)
            {
                KromaShaderPreset selectedPreset = _presets[_selectedPresetIndex];
                selectedPreset.ApplyToMaterial(mat);
            }

            AssetDatabase.CreateAsset(mat, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_switchSkybox)
            {
                RenderSettings.skybox = mat;
                DynamicGI.UpdateEnvironment();

                // Re-sync GetMainLightDirection script if there is one on the Directional Light
                Light sunLight = KromaLightingUtils.FindSunLight();
                if (sunLight != null)
                {
                    GetMainLightDirection lightDirectionScript = sunLight.GetComponent<GetMainLightDirection>();
                    if (lightDirectionScript != null)
                    {
                        var field = typeof(GetMainLightDirection).GetField("skyboxMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null) field.SetValue(lightDirectionScript, mat);
                    }
                }

                // Conditional switch of Environment Lighting Source
                if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Skybox && _switchEnvironmentLighting)
                {
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 1.1f;
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            // Focus new asset in project window
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
        }

        private void CenterOnMainWin()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Rect pos = position;
            float centerWidth = (main.width - pos.width) * 0.5f;
            float centerHeight = (main.height - pos.height) * 0.5f;
            pos.x = main.x + centerWidth;
            pos.y = main.y + centerHeight;
            position = pos;
        }
    }
}
