using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    public class KromaSkyboxShaderGUI : ShaderGUI
    {
        // Foldout states
        private bool _showSkyboxColors = true;
        private bool _showClouds = false;
        private bool _showClouds2 = false;
        private bool _showSunMoon = false;
        private bool _showStars = false;
        private bool _showGodRays = false;

        private bool _showCelestial1 = false;
        private bool _showCelestial2 = false;
        
        // Advanced
        private bool _showCloudAdvanced = false;
        private bool _showCloud2Advanced = false;

        // Banners
        private Texture2D _headerBanner;
        private Texture2D _skyboxBanner;
        private Texture2D _sunMoonBanner;
        private Texture2D _starsBanner;
        private Texture2D _clouds1Banner;
        private Texture2D _clouds2Banner;
        private Texture2D _godraysBanner;
        private Texture2D _planet1Banner;
        private Texture2D _planet2Banner;
        // Sub-banners removed as per user request

            // Celestial Property Mapping (Aligned by index)
        private static readonly string[] _sunProps = new string[] {
            "_Texture", "_Tiling", "_Tint", "_Size", "_CelestialEdgeSoftness", 
            "_Phase", "_InvertPhase", "_PhaseSize", "_PhaseSmoothness", "_Rotation", "_Fade", "_DarkSideOcclusion",
            "_GlowEnabled", "_GlowColor", "_GlowPower", "_GlowIntensity",
            "_AtmosphereEnabled", "_AtmosphereColor", "_AtmosphereOpacity", "_AtmosphereSize", "_AtmosphereBlur", "_AtmosphereGlowPosition",
            "_Atmosphere2Enabled", "_Atmosphere2Color", "_Atmosphere2Opacity", "_Atmosphere2Size", "_Atmosphere2Blur", "_Atmosphere2GlowPosition"
        };

        private static readonly string[] _cel1Props = new string[] {
            "_C1_Texture", "_C1_Tiling", "_C1_Tint", "_C1_Size", "_C1_EdgeSoftness", 
            "_C1_Phase", "_C1_InvertPhase", "_C1_PhaseSize", "_C1_PhaseSmoothness", "_C1_Rotation", "_C1_Fade", "_C1_DarkSideOcclusion",
            "_C1_GlowEnabled", "_C1_GlowColor", "_C1_GlowPower", "_C1_GlowIntensity",
            "_C1_AtmosphereEnabled", "_C1_AtmosphereColor", "_C1_AtmosphereOpacity", "_C1_AtmosphereSize", "_C1_AtmosphereBlur", "_C1_AtmosphereGlowPosition",
            "_C1_Atmosphere2Enabled", "_C1_Atmosphere2Color", "_C1_Atmosphere2Opacity", "_C1_Atmosphere2Size", "_C1_Atmosphere2Blur", "_C1_Atmosphere2GlowPosition"
        };

        private static readonly string[] _cel2Props = new string[] {
            "_C2_Texture", "_C2_Tiling", "_C2_Tint", "_C2_Size", "_C2_EdgeSoftness", 
            "_C2_Phase", "_C2_InvertPhase", "_C2_PhaseSize", "_C2_PhaseSmoothness", "_C2_Rotation", "_C2_Fade", "_C2_DarkSideOcclusion",
            "_C2_GlowEnabled", "_C2_GlowColor", "_C2_GlowPower", "_C2_GlowIntensity",
            "_C2_AtmosphereEnabled", "_C2_AtmosphereColor", "_C2_AtmosphereOpacity", "_C2_AtmosphereSize", "_C2_AtmosphereBlur", "_C2_AtmosphereGlowPosition",
            "_C2_Atmosphere2Enabled", "_C2_Atmosphere2Color", "_C2_Atmosphere2Opacity", "_C2_Atmosphere2Size", "_C2_Atmosphere2Blur", "_C2_Atmosphere2GlowPosition"
        };
// ... (skip down to DrawGlowSection replacement - I will just replace the properties definition block first)
// Wait, I need to replace DrawGlowSection separately or use multi-replace. I'll use separate calls or chunks.
// Multi-replace is safer.


        // Cloud Property Mapping (Aligned by index for Copy/Paste)
        private static readonly string[] _c1Props = new string[] {
            "_Cloud_Texture", "_Color_1", "_Scale", "_Density", "_Intensity", "_CloudErosion",
            "_CloudHighlightsToggle", "_CloudHighlightColor", "_CloudHighlightControl", "_CloudHighlightContrast", "_CloudHighlightSmoothness",
            "_Horizon_Level", "_Horizon_Fade", "_Top_Fade",
            "_Speed",
            "_CloudBaseSpeedMultiplier", "_CloudDetailSpeedMultiplier", "_CloudDetailScale", "_CloudErosionMultiplier", "_CloudAlphaEdge",
            "_CloudHeightScale", "_CloudLightingWrap", "_CloudSunBoost", "_CloudScatterMin", "_CloudScatterMax", "_CloudHeightBias",
            "_CloudBaseEpsilon", "_CloudDetailEpsilon",
            "_CloudDetailTexToggle", "_Cloud_Detail_Texture"
        };

        private static readonly string[] _c2Props = new string[] {
            "_Cloud2_Texture", "_Cloud2_Color", "_Cloud2_Scale", "_Cloud2_Density", "_Cloud2_Intensity", "_Cloud2_Erosion",
            "_Cloud2HighlightsToggle", "_Cloud2_HighlightColor", "_Cloud2_HighlightControl", "_Cloud2_HighlightContrast", "_Cloud2_HighlightSmoothness",
            "_Cloud2_Horizon_Level", "_Cloud2_Horizon_Fade", "_Cloud2_Top_Fade",
            "_Cloud2_Speed",
            "_Cloud2_BaseSpeedMultiplier", "_Cloud2_DetailSpeedMultiplier", "_Cloud2_DetailScale", "_Cloud2_ErosionMultiplier", "_Cloud2_AlphaEdge",
            "_Cloud2_HeightScale", "_Cloud2_LightingWrap", "_Cloud2_SunBoost", "_Cloud2_ScatterMin", "_Cloud2_ScatterMax", "_Cloud2_HeightBias",
            "_Cloud2_BaseEpsilon", "_Cloud2_DetailEpsilon",
            "_Cloud2DetailTexToggle", "_Cloud2_Detail_Texture"
        };

        private static readonly string[] _godRaysProps = new string[] {
            "_GodRaysColor", "_GodRaysDensity", "_GodRaysContrast", "_GodRaysBeamBlur", "_GodRaysBeamSweep", "_GodRaysRotateWithCamera", "_GodRaysSpeed",
            "_GodRaysPackCount", "_GodRaysMinPackWidth", "_GodRaysMaxPackWidth",
            "_GodRaysHoleSize", "_GodRaysHoleSoftness", "_GodRaysHoleNoise",
            "_GodRaysLength", "_GodRaysFalloff",
            "_GodRaysHorizonFade", "_GodRaysHorizonMaskPower", "_GodRaysHorizonCruft"
        };

        // Cache for Presets to avoid AssetDatabase calls in OnGUI
        private Dictionary<string, List<KromaShaderPreset>> _presetCache = new Dictionary<string, List<KromaShaderPreset>>();
        private Dictionary<string, string[]> _presetNamesCache = new Dictionary<string, string[]>();

        private string BANNER_PATH => $"{KromaAssetManager.GetKromaRootPath()}/Editor/Resources/UI/Shader_UI/";
        private string PRESET_PATH_ROOT => $"{KromaAssetManager.GetKromaRootPath()}/Content/Shaders/Presets/";

        private void LoadBanners()
        {
            if (_headerBanner != null) return;

            _headerBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_Header.png");
            _skyboxBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_Skybox_Banner.png");
            _sunMoonBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_SunMoon_Banner.png");
            _starsBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_Stars_Banner.png");
            
            _clouds1Banner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_Clouds1_Banner.png");
            _clouds2Banner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Shader_Clouds2_Banner.png");
            _godraysBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Godrays_Banner.png");
            _planet1Banner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Planet1_Banner.png");
            _planet2Banner = AssetDatabase.LoadAssetAtPath<Texture2D>(BANNER_PATH + "Planet2_Banner.png");
        }
// ... (start CopyCloudSettings remains, so I will select ApplyCloudPreset only)
// Wait, I cannot replace LoadBanners and ApplyCloudPreset in one block easily as they are far apart.
// I will do LoadBanners first.


        // Track pending loads to avoid spamming delayCall
        private HashSet<string> _pendingLoads = new HashSet<string>();

        private void LoadPresets(string folderName, string defaultLabel)
        {
             // Clear pending flag
             if (_pendingLoads.Contains(folderName)) _pendingLoads.Remove(folderName);

             string searchFolder = (PRESET_PATH_ROOT + folderName).TrimEnd('/');
             List<KromaShaderPreset> loadedPresets = new List<KromaShaderPreset>();
             List<string> paramsList = new List<string> { defaultLabel };

             string[] guids = new string[0];
             if (AssetDatabase.IsValidFolder(searchFolder))
             {
                  guids = AssetDatabase.FindAssets("t:KromaShaderPreset", new[] { searchFolder });
             }
             else
             {
                  // Fallback searches using dynamic root
                  guids = AssetDatabase.FindAssets("t:KromaShaderPreset", new[] { KromaAssetManager.GetKromaRootPath() });
             }

             foreach (var guid in guids)
             {
                 string path = AssetDatabase.GUIDToAssetPath(guid);
                 KromaShaderPreset p = AssetDatabase.LoadAssetAtPath<KromaShaderPreset>(path);
                 
                 // Relaxed check: Allow if name matches OR if it resides in the requested folder
                 // This allows Celestial 1/2 to load "SunMoon" presets even if metadata is stale
                 if (p != null && (p.sectionName == folderName || path.Contains("/" + folderName + "/"))) 
                 {
                     loadedPresets.Add(p);
                     paramsList.Add(p.name);
                 }
             }

             _presetCache[folderName] = loadedPresets;
             _presetNamesCache[folderName] = paramsList.ToArray();
        }

        private void DrawMasterPresetGUI(MaterialEditor materialEditor, MaterialProperty[] currentProps)
        {
            float height = 28f;
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            
            // Background exactly edge-to-edge, expanded upwards and downwards to remove visual gaps
            float vSpacing = EditorGUIUtility.standardVerticalSpacing;
            Rect fullRect = new Rect(0, rect.y - vSpacing, EditorGUIUtility.currentViewWidth, height + vSpacing * 2); 
            EditorGUI.DrawRect(fullRect, new Color(0.098f, 0.098f, 0.098f, 1)); 

            // Standard layout rect for controls
            float controlHeight = 18f;
            float verticalOffset = (height - controlHeight) / 2f;
            
            float buttonWidth = 75f;
            float spacing = 5f;
            
            // Align dropdown left edge — reduced to compensate for Popup's internal padding
            float startX = rect.x;
            float usableWidth = EditorGUIUtility.currentViewWidth - startX - 18f; // Account for scrollbar
            
            Rect dropdownRect = new Rect(startX, rect.y + verticalOffset, usableWidth - buttonWidth - spacing, controlHeight);
            Rect buttonRect = new Rect(dropdownRect.xMax + spacing, rect.y + verticalOffset, buttonWidth, controlHeight);

            string folderName = "Master";
            string defaultLabel = "Select Skybox Preset...";

            if (!_presetCache.ContainsKey(folderName) || !_presetNamesCache.ContainsKey(folderName))
            {
                EditorGUI.LabelField(dropdownRect, "Loading...");
                if (!_pendingLoads.Contains(folderName))
                {
                    _pendingLoads.Add(folderName);
                    EditorApplication.delayCall += () => { LoadPresets(folderName, defaultLabel); };
                }
                return;
            }

            List<KromaShaderPreset> presets = _presetCache[folderName];
            string[] options = _presetNamesCache[folderName];
            if (options.Length == 0) options = new string[] { defaultLabel };

            int selected = EditorGUI.Popup(dropdownRect, 0, options); 
            if (selected > 0)
            {
                if (selected - 1 < presets.Count)
                {
                    KromaShaderPreset selectedPreset = presets[selected - 1];
                    EditorApplication.delayCall += () =>
                    {
                        Undo.RecordObjects(materialEditor.targets, "Apply Preset");
                        foreach (Object t in materialEditor.targets)
                        {
                            if (t is Material m) 
                            {
                                selectedPreset.ApplyToMaterial(m);
                                RefreshKeywords(m);
                                SynchronizeFoldouts(m);
                            }
                        }
                        if (materialEditor != null) materialEditor.Repaint();
                    };
                }
            }

            if (GUI.Button(buttonRect, new GUIContent("Save New", "Save Skybox preset")))
            {
                Material mat = materialEditor.target as Material;
                EditorApplication.delayCall += () => { SaveNewPreset(mat, folderName, _masterProps); };
            }
        }

        private void DrawSectionPresetGUI(MaterialEditor materialEditor, MaterialProperty[] currentProps, string sectionName, string[] propertyNames, 
            string presetFolderOverride = null, System.Action<KromaShaderPreset, Material> customApplyAction = null, string defaultLabel = "Select Preset...")
        {
            EditorGUILayout.BeginHorizontal();
            
            // 1. Find Assets (Cached)
            string folderName = presetFolderOverride ?? sectionName;

            if (!_presetCache.ContainsKey(folderName) || !_presetNamesCache.ContainsKey(folderName))
            {
                EditorGUILayout.LabelField("Loading...", GUILayout.Width(EditorGUIUtility.labelWidth + 100));
                
                if (!_pendingLoads.Contains(folderName))
                {
                    _pendingLoads.Add(folderName);
                    EditorApplication.delayCall += () => 
                    {
                        LoadPresets(folderName, defaultLabel);
                    };
                }
                
                EditorGUILayout.EndHorizontal();
                return;
            }

            // 2. Dropdown
            List<KromaShaderPreset> presets = _presetCache[folderName];
            string[] options = _presetNamesCache[folderName];
            
            // Safety check
            if (options.Length == 0) options = new string[] { defaultLabel };

            int selected = EditorGUILayout.Popup(0, options); 
            if (selected > 0)
            {
                // Index is shifted by 1 because of "Select Preset..."
                if (selected - 1 < presets.Count)
                {
                    KromaShaderPreset selectedPreset = presets[selected - 1];
                    EditorApplication.delayCall += () =>
                    {
                        Undo.RecordObjects(materialEditor.targets, "Apply Preset");
                        foreach (Object t in materialEditor.targets)
                        {
                            if (t is Material m) 
                            {
                                if (customApplyAction != null) customApplyAction(selectedPreset, m);
                                else selectedPreset.ApplyToMaterial(m);
                            }
                        }
                        if (materialEditor != null) materialEditor.Repaint();
                    };
                }
            }

            // 3. Save Button
            string saveTooltip = "";
            switch (sectionName)
            {
                case "Sun_Moon":
                case "Planet1":
                case "Planet2":
                case "Planet 1":
                case "Planet 2":
                    saveTooltip = "Save Celestial Preset";
                    break;
                case "Skybox Colors":
                case "SkyboxColors":
                    saveTooltip = "Save Skybox Colors Preset";
                    break;
                case "Clouds1":
                case "Cloud1":
                case "Clouds2":
                case "Cloud2":
                    saveTooltip = "Save Clouds Preset";
                    break;
                case "GodRays":
                case "God Rays":
                    saveTooltip = "Save God Rays Preset";
                    break;
                default:
                    saveTooltip = $"Save {sectionName} Preset";
                    break;
            }

            if (GUILayout.Button(new GUIContent("Save New", saveTooltip), GUILayout.Width(70)))
            {
                // Fix for Recursive GUI Error: Delay the file panel opening until after OnGUI finishes
                Material mat = materialEditor.target as Material;
                EditorApplication.delayCall += () => 
                {
                    SaveNewPreset(mat, folderName, propertyNames);
                };
            }
            
            EditorGUILayout.EndHorizontal();
        }




        private void SaveNewPreset(Material mat, string sectionName, string[] propsToSave)
        {
            if (mat == null) return;

            string path = EditorUtility.SaveFilePanelInProject("Save Preset", sectionName + "_NewPreset", "asset", "Save preset to project", PRESET_PATH_ROOT + sectionName);
            if (string.IsNullOrEmpty(path)) return;

            KromaShaderPreset newPreset = ScriptableObject.CreateInstance<KromaShaderPreset>();
            newPreset.sectionName = sectionName;
            
            // It is now SAFE to call GetMaterialProperties because we are in a delayed callback (outside OnGUI)
            MaterialProperty[] matProps = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { mat });
            
            newPreset.properties.Clear();
            foreach (var name in propsToSave)
            {
                if (!mat.HasProperty(name)) continue;

                // Find the MaterialProperty object
                MaterialProperty p = FindProperty(name, matProps, false);
                
                var propData = new ShaderPropertyData { name = name };
                
                // If we found the property object, we can get exact type and metadata
                if (p != null)
                {
#if UNITY_6000_1_OR_NEWER
                    var pFlags = p.propertyFlags;
                    var pType = p.propertyType;
#else
                    var pFlags = (UnityEngine.Rendering.ShaderPropertyFlags)p.flags;
                    var pType = (UnityEngine.Rendering.ShaderPropertyType)p.type;
#endif
                    propData.isHDR = (pFlags & UnityEngine.Rendering.ShaderPropertyFlags.HDR) != 0;
                    
                    if (pType == UnityEngine.Rendering.ShaderPropertyType.Color)
                    {
                        propData.type = ShaderPropertyData.Type.Color;
                        propData.colorValue = p.colorValue;
                    }
                    else if (pType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                    {
                        propData.type = ShaderPropertyData.Type.Vector;
                        propData.vectorValue = p.vectorValue;
                    }
                    else if (pType == UnityEngine.Rendering.ShaderPropertyType.Float || pType == UnityEngine.Rendering.ShaderPropertyType.Range)
                    {
                        propData.type = ShaderPropertyData.Type.Float;
                        propData.floatValue = p.floatValue;
                        
                        if (pType == UnityEngine.Rendering.ShaderPropertyType.Range)
                        {
                            propData.isRange = true;
                            propData.rangeMin = p.rangeLimits.x;
                            propData.rangeMax = p.rangeLimits.y;
                        }
                    }
                    else if (pType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        propData.type = ShaderPropertyData.Type.Texture;
                        propData.textureValue = p.textureValue;
                    }
                }
                else
                {
                    // Fallback using direct Material getter if property object missing (rare)
                    // (Simplified fallback logic removed for brevity, assume successful FindProperty)
                    continue;
                }
                
                newPreset.properties.Add(propData);
            }

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Invalidate Cache for this section so new preset shows up immediately
            // We invalidate using the folderName which is passed as sectionName context in most cases here, or we just clear all
            if (_presetCache.ContainsKey(sectionName)) _presetCache.Remove(sectionName);
            if (_presetNamesCache.ContainsKey(sectionName)) _presetNamesCache.Remove(sectionName);
        }

        private void CopyCloudSettings(Material mat, bool fromC1)
        {
            Undo.RecordObject(mat, fromC1 ? "Capture C1 to C2" : "Capture C2 to C1");
            
            string[] src = fromC1 ? _c1Props : _c2Props;
            string[] dst = fromC1 ? _c2Props : _c1Props;

            for (int i = 0; i < src.Length; i++)
            {
                if (mat.HasProperty(src[i]) && mat.HasProperty(dst[i]))
                {
                    // Copy based on type logic (simplified since we know types usually match)
                    // But we should check.
                    // For efficiency, we just try common types.
                    UnityEngine.Rendering.ShaderPropertyType type = mat.shader.GetPropertyType(mat.shader.FindPropertyIndex(src[i]));
                    
                    switch (type)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            mat.SetColor(dst[i], mat.GetColor(src[i]));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Vector:
                            mat.SetVector(dst[i], mat.GetVector(src[i]));
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Float: 
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            float val = mat.GetFloat(src[i]);
                            mat.SetFloat(dst[i], val);
                            
                            // Handle Keywords for Toggles
                            if (src[i].Contains("Toggle") || src[i].Contains("Highlights"))
                            {
                                // Special handling for our specific keywords
                                if (i == 6) // Highlights Toggle Index
                                {
                                    bool state = val > 0.5f;
                                    if (fromC1) // C1 -> C2
                                    {
                                        if (state) mat.EnableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                                        else mat.DisableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                                    }
                                    else // C2 -> C1
                                    {
                                        if (state) mat.EnableKeyword("_CLOUD_HIGHLIGHTS_ON");
                                        else mat.DisableKeyword("_CLOUD_HIGHLIGHTS_ON");
                                    }
                                }
                                else if (src[i].Contains("DetailTex"))
                                {
                                    bool state = val > 0.5f;
                                    if (fromC1) // C1 (src) has it ON -> Enable C2 (dst) keyword
                                    {
                                         if (state) mat.EnableKeyword("_CLOUD2_DETAIL_TEX_ON");
                                         else mat.DisableKeyword("_CLOUD2_DETAIL_TEX_ON");
                                    }
                                    else // C2 (src) has it ON -> Enable C1 (dst) keyword
                                    {
                                         if (state) mat.EnableKeyword("_CLOUD_DETAIL_TEX_ON");
                                         else mat.DisableKeyword("_CLOUD_DETAIL_TEX_ON");
                                    }
                                }
                            }
                            break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            mat.SetTexture(dst[i], mat.GetTexture(src[i]));
                            break;
                    }
                }
            }
        }

        private void ApplyCloudPreset(KromaShaderPreset preset, Material mat, bool targetIsC2)
        {
            Undo.RecordObject(mat, "Apply Cloud Preset");

            string[] targetProps = targetIsC2 ? _c2Props : _c1Props;
            string[] otherProps = targetIsC2 ? _c1Props : _c2Props;

            // Iterate through the PRESET'S properties
            foreach (var prop in preset.properties)
            {
                // No change needed here, just confirming logic.h index this property corresponds to
                // It might match a target prop name OR an other prop name (if saved from the other cloud layer)
                
                int matchIndex = -1;

                // 1. Check direct match (e.g. preset has _Cloud2_Color and we are targetting C2)
                for (int i = 0; i < targetProps.Length; i++)
                {
                    if (prop.name == targetProps[i]) { matchIndex = i; break; }
                }

                // 2. If no match, check cross match (e.g. preset has _Color_1 and we are targetting C2)
                if (matchIndex == -1)
                {
                    for (int i = 0; i < otherProps.Length; i++)
                    {
                        if (prop.name == otherProps[i]) { matchIndex = i; break; }
                    }
                }

                if (matchIndex != -1)
                {
                    string destName = targetProps[matchIndex];
                    if (!mat.HasProperty(destName)) continue;
                    
                    // Safety Check: Get actual property type from Shader to prevent mismatches
                    int shaderPropIndex = mat.shader.FindPropertyIndex(destName);
                    if (shaderPropIndex == -1) continue;
                    var actualType = mat.shader.GetPropertyType(shaderPropIndex);

                    // Apply value based on PRESET type, but only if ACTUAL type matches
                    switch (prop.type)
                    {
                        case ShaderPropertyData.Type.Color: 
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Color || actualType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                                mat.SetColor(destName, prop.colorValue); 
                            break;
                            
                        case ShaderPropertyData.Type.Vector: 
                             if (actualType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                                mat.SetVector(destName, prop.vectorValue);
                             break;
                             
                        case ShaderPropertyData.Type.Float: 
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Float || actualType == UnityEngine.Rendering.ShaderPropertyType.Range)
                            {
                                mat.SetFloat(destName, prop.floatValue); 
                                // Handle Keyword for Highlights (Index 6)
                                if (matchIndex == 6)
                                {
                                    bool state = prop.floatValue > 0.5f;
                                    if (targetIsC2)
                                    {
                                        if (state) mat.EnableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                                        else mat.DisableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                                    }
                                    else
                                    {
                                        if (state) mat.EnableKeyword("_CLOUD_HIGHLIGHTS_ON");
                                        else mat.DisableKeyword("_CLOUD_HIGHLIGHTS_ON");
                                    }
                                }
                                else if (destName.Contains("DetailTexToggle"))
                                {
                                    bool state = prop.floatValue > 0.5f;
                                    string kw = targetIsC2 ? "_CLOUD2_DETAIL_TEX_ON" : "_CLOUD_DETAIL_TEX_ON";
                                    if (state) mat.EnableKeyword(kw);
                                    else mat.DisableKeyword(kw);
                                }
                            }
                            break;
                            
                        case ShaderPropertyData.Type.Texture: 
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                                mat.SetTexture(destName, prop.textureValue); 
                            break;
                    }
                }
            }
        }

        private void ApplyCelestialPreset(KromaShaderPreset preset, Material mat, int targetIndex)
        {
            // targetIndex: 0 = Sun/Moon, 1 = C1, 2 = C2
            Undo.RecordObject(mat, "Apply Celestial Preset");

            string[] targetProps = (targetIndex == 0) ? _sunProps : (targetIndex == 1) ? _cel1Props : _cel2Props;
            string[][] allProps = new string[][] { _sunProps, _cel1Props, _cel2Props };

            foreach (var prop in preset.properties)
            {
                int matchIndex = -1;

                // 1. Check direct match and cross matches to find INDEX
                for(int setIdx = 0; setIdx < allProps.Length; setIdx++)
                {
                    string[] currentSet = allProps[setIdx];
                    for (int i = 0; i < currentSet.Length; i++)
                    {
                        if (prop.name == currentSet[i])
                        {
                            matchIndex = i; // Found the property index (e.g. "Tint" is index 2)
                            break;
                        }
                    }
                    if (matchIndex != -1) break;
                }

                if (matchIndex != -1 && matchIndex < targetProps.Length)
                {
                    string destName = targetProps[matchIndex];
                    if (!mat.HasProperty(destName)) continue;

                    // Type Safety Check
                    int shaderPropIndex = mat.shader.FindPropertyIndex(destName);
                    if (shaderPropIndex == -1) continue;
                    var actualType = mat.shader.GetPropertyType(shaderPropIndex);

                    switch (prop.type)
                    {
                        case ShaderPropertyData.Type.Color:
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Color || actualType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                                mat.SetColor(destName, prop.colorValue);
                            break;
                        case ShaderPropertyData.Type.Vector:
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Vector)
                                mat.SetVector(destName, prop.vectorValue);
                            break;
                        case ShaderPropertyData.Type.Float:
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Float || actualType == UnityEngine.Rendering.ShaderPropertyType.Range)
                                mat.SetFloat(destName, prop.floatValue);
                            break;
                        case ShaderPropertyData.Type.Texture:
                            if (actualType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                                mat.SetTexture(destName, prop.textureValue);
                            break;
                    }
                }
            }
        }



        private struct SkyboxPresetLegacy
        {
            public string Name;
            public Color Top;
            public Color Mid;
            public Color Horizon;
            public Color Ground;
            public float HorizonOffset;
            public float MidSkyHeight;
            public float HorizonSoftness;
            public float MidSkySoftness;

            public SkyboxPresetLegacy(string name, Color top, Color mid, Color horizon, Color ground, float hOffset, float mHeight, float hSoft, float mSoft)
            {
                Name = name;
                Top = top; Mid = mid; Horizon = horizon; Ground = ground;
                HorizonOffset = hOffset; MidSkyHeight = mHeight;
                HorizonSoftness = hSoft; MidSkySoftness = mSoft;
            }
        }

        private static readonly SkyboxPresetLegacy[] _legacyPresets = new SkyboxPresetLegacy[]
        {
            new SkyboxPresetLegacy("Legacy Day", 
                new Color(0.098f, 0.400f, 0.800f), new Color(0.475f, 0.651f, 0.918f), new Color(0.522f, 0.694f, 0.863f), new Color(0.016f, 0.208f, 0.133f), -0.1f, 0.2f, 0.4f, 0.5f),
            new SkyboxPresetLegacy("Legacy Sunset",  
                new Color(0.2f, 0.1f, 0.4f), new Color(0.8f, 0.3f, 0.2f), new Color(1.0f, 0.6f, 0.1f), new Color(0.1f, 0.05f, 0.05f), 0.0f, 0.4f, 0.6f, 0.4f),
            new SkyboxPresetLegacy("Legacy Night", 
                new Color(0.012f, 0.012f, 0.051f), new Color(0.043f, 0.043f, 0.278f), new Color(0.016f, 0.063f, 0.220f), new Color(0.043f, 0.035f, 0.149f), -0.01f, 0.12f, 0.4f, 0.76f),
            new SkyboxPresetLegacy("Legacy Sunrise", 
                new Color(0.063f, 0.180f, 0.400f), new Color(0.624f, 0.227f, 0.294f), new Color(0.945f, 0.596f, 0.188f) * Mathf.Pow(2, 1), new Color(0.082f, 0.016f, 0.157f), -0.48f, -0.06f, 0.688f, 0.93f)
        };

        private string[] _masterProps;

        private void InitializeMasterProps()
        {
            if (_masterProps != null) return;

            List<string> props = new List<string>();

            // 1. Skybox Colors
            props.AddRange(new[] { 
                "_SimulateGround", "_UseTwoColorMode",
                "_TopColor", "_MidColor", "_HorizonColor", "_Ground_Color", 
                "_Horizon_Offset", "_Mid_Sky_Height", "_Horizon_Softness", "_Mid_Sky_Softness", "_GlobalRotation",
                "_Ground_Blend", "_Ground_Horizon_Smoothness"
            });

            // 2. Sun / Moon (Base)
            props.Add("_CelestialToggle");
            props.AddRange(_sunProps);

            // 3. Stars (include _StarsToggle)
            props.Add("_StarsToggle");
            props.AddRange(new[] {
                "_StarCubeMap", "_StarExposure", "_StarSize", "_StarTint",
                "_StarLatitude", "_StarRotation", "_StarSpeed",
                "_Star_Blend", "_Star_Blend_Contrast"
            });

            // 4. Clouds 1
            props.Add("_CloudsToggle");
            props.AddRange(_c1Props);

            // 5. Clouds 2
            props.Add("_Clouds2Toggle");
            props.AddRange(_c2Props);

            // 6. God Rays
            props.Add("_GodRaysToggle");
            props.AddRange(_godRaysProps);

            // 7. Celestial 1
            props.Add("_C1_Enabled");
            props.AddRange(_cel1Props);

            // 7. Celestial 2
            props.Add("_C2_Enabled");
            props.AddRange(_cel2Props);

            _masterProps = props.ToArray();
        }

        private void SynchronizeFoldouts(Material mat)
        {
            if (mat == null) return;
            
            _showSunMoon = mat.HasProperty("_CelestialToggle") && mat.GetFloat("_CelestialToggle") > 0.5f;
            _showStars = mat.HasProperty("_StarsToggle") && mat.GetFloat("_StarsToggle") > 0.5f;
            _showClouds = mat.HasProperty("_CloudsToggle") && mat.GetFloat("_CloudsToggle") > 0.5f;
            _showClouds2 = mat.HasProperty("_Clouds2Toggle") && mat.GetFloat("_Clouds2Toggle") > 0.5f;
            _showCelestial1 = mat.HasProperty("_C1_Enabled") && mat.GetFloat("_C1_Enabled") > 0.5f;
            _showCelestial2 = mat.HasProperty("_C2_Enabled") && mat.GetFloat("_C2_Enabled") > 0.5f;
        }

        private void RefreshKeywords(Material mat)
        {
            if (mat == null) return;

            // Helper to sync a toggle property to a keyword
            void Sync(string prop, string kw)
            {
                if (mat.HasProperty(prop))
                {
                    bool on = mat.GetFloat(prop) > 0.5f;
                    if (on) mat.EnableKeyword(kw);
                    else mat.DisableKeyword(kw);
                }
            }

            Sync("_SimulateGround", "_SIMULATE_GROUND");
            Sync("_UseTwoColorMode", "_TWO_COLOR_MODE");
            Sync("_GodRaysToggle", "_GODRAYS_ON");
            Sync("_CelestialToggle", "_CELESTIAL_ON");
            Sync("_CloudsToggle", "_CLOUDS_ON");
            Sync("_Clouds2Toggle", "_CLOUDS2_ON");
            Sync("_CloudHighlightsToggle", "_CLOUD_HIGHLIGHTS_ON");
            Sync("_Cloud2HighlightsToggle", "_CLOUD2_HIGHLIGHTS_ON"); 
            Sync("_CloudDetailTexToggle", "_CLOUD_DETAIL_TEX_ON");
            Sync("_Cloud2DetailTexToggle", "_CLOUD2_DETAIL_TEX_ON");
            Sync("_StarsToggle", "_STARS_ON");
            Sync("_GodRaysRotateWithCamera", "_GODRAYS_ROTATE_WITH_CAMERA");
            Sync("_C1_Enabled", "_CELESTIAL1_ON");
            Sync("_C2_Enabled", "_CELESTIAL2_ON");
            
            // Sync Glow/Atmosphere sub-keywords if needed (simplified for now as main toggles are key)
            // But let's be thorough for the main sections:
            Sync("_GlowEnabled", "_GLOW_ON");
            Sync("_AtmosphereEnabled", "_ATMOSPHERE_ON");
            Sync("_C1_GlowEnabled", "_C1_GLOW_ON");
            Sync("_C1_AtmosphereEnabled", "_C1_ATMOSPHERE_ON");
            Sync("_C2_GlowEnabled", "_C2_GLOW_ON");
            Sync("_C2_AtmosphereEnabled", "_C2_ATMOSPHERE_ON");
            
            // Unity 6000.0 RenderGraph compatibility
            if (Application.unityVersion.StartsWith("6000.0"))
            {
                mat.EnableKeyword("_KROMA_6000_0_COMPATIBILITY");
            }
            else
            {
                mat.DisableKeyword("_KROMA_6000_0_COMPATIBILITY");
            }
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            LoadBanners();
            InitializeMasterProps();
            
            // NOTE: Removed SetDefaultGUIWidths() as it might be interfering. 
            // We will control widths manually.
            EditorGUIUtility.labelWidth = 0; // Reset to let Unity decide, or we override manually below.

            // --- HEADER (Responsive) ---
            DrawFullWidthBanner(_headerBanner);

            // --- MASTER PRESET ---
            DrawMasterPresetGUI(materialEditor, properties);

            // --- SKYBOX COLORS SECTION ---
            _showSkyboxColors = DrawSectionHeaderWithToggle(_showSkyboxColors, "Skybox Colors", null, _skyboxBanner);
            
            if (_showSkyboxColors)
            {
                // Push content down slightly
                EditorGUILayout.Space(10);
                
                // --- Presets Case ---
                DrawSectionPresetGUI(materialEditor, properties, "Skybox Colors", new string[] { 
                    "_SimulateGround", "_UseTwoColorMode",
                    "_TopColor", "_MidColor", "_HorizonColor", "_Ground_Color", 
                    "_Horizon_Offset", "_Mid_Sky_Height", "_Horizon_Softness", "_Mid_Sky_Softness", "_GlobalRotation",
                    "_Ground_Blend", "_Ground_Horizon_Smoothness"
                }, null, (preset, m) => {
                    preset.ApplyToMaterial(m);
                    RefreshKeywords(m);
                });

                EditorGUILayout.Space(5);
                
                // Group 1: Palette
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                
                MaterialProperty useTwoColorModeProp = FindProperty("_UseTwoColorMode", properties, false);
                bool useTwoColorMode = useTwoColorModeProp != null && useTwoColorModeProp.floatValue > 0.5f;

                DrawColor(materialEditor, properties, "_TopColor", "Top Sky Color");
                if (!useTwoColorMode)
                {
                    DrawColor(materialEditor, properties, "_MidColor", "Mid Sky Color");
                }
                DrawColor(materialEditor, properties, "_HorizonColor", "Horizon Color");
                
                if (useTwoColorModeProp != null)
                {
                    EditorGUI.BeginChangeCheck();
                    string twoColorTooltip = KromaSkyboxTooltipData.GetTooltip("_UseTwoColorMode");
                    bool newUseTwoColorMode = EditorGUILayout.Toggle(new GUIContent("Limit to 2 Colors", twoColorTooltip), useTwoColorMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        useTwoColorModeProp.floatValue = newUseTwoColorMode ? 1.0f : 0.0f;
                        foreach (var t in materialEditor.targets)
                        {
                            if (t is Material m)
                            {
                                if (newUseTwoColorMode) m.EnableKeyword("_TWO_COLOR_MODE");
                                else m.DisableKeyword("_TWO_COLOR_MODE");
                            }
                        }
                        useTwoColorMode = newUseTwoColorMode;
                    }
                }

                // Simulate Ground Toggle (Moved under Horizon Color)
                MaterialProperty simGround = FindProperty("_SimulateGround", properties, false);
                if (simGround != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float val = simGround.floatValue;
                    string groundTooltip = KromaSkyboxTooltipData.GetTooltip("_SimulateGround");
                    bool isSimulating = EditorGUILayout.Toggle(new GUIContent("Simulate Ground", groundTooltip), val > 0.5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        simGround.floatValue = isSimulating ? 1.0f : 0.0f;
                        foreach(var t in materialEditor.targets)
                        {
                            if (t is Material m)
                            {
                                if (isSimulating) m.EnableKeyword("_SIMULATE_GROUND");
                                else m.DisableKeyword("_SIMULATE_GROUND");
                            }
                        }
                    }
                }
                
                // Only show Ground Color if Simulate Ground is ON
                if (simGround == null || simGround.floatValue > 0.5f)
                {
                     DrawColor(materialEditor, properties, "_Ground_Color", "Ground Color");
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);

                // Group 2: Position and Blending
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Position and Blending", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                
                if (!useTwoColorMode)
                {
                    // 1. Sky Gradient Height
                    DrawProperty(materialEditor, properties, "_Mid_Sky_Height", "Sky Gradient Height");
                    // 2. Sky Gradient Falloff
                    DrawProperty(materialEditor, properties, "_Mid_Sky_Softness", "Sky Gradient Falloff");
                }
                
                // 3. Horizon Height
                DrawProperty(materialEditor, properties, "_Horizon_Offset", "Horizon Height");
                // 4. Horizon Falloff
                DrawProperty(materialEditor, properties, "_Horizon_Softness", "Horizon Falloff");
                
                // Ground Height & Falloff (Only if Ground is ON)
                if (simGround == null || simGround.floatValue > 0.5f)
                {
                    // 5. Ground Height
                    DrawProperty(materialEditor, properties, "_Ground_Blend", "Ground Height");
                    // 6. Ground Falloff
                    DrawProperty(materialEditor, properties, "_Ground_Horizon_Smoothness", "Ground Falloff");
                }

                DrawProperty(materialEditor, properties, "_GlobalRotation", "Global Rotation");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
            }

            DrawSeparator();

            // --- SUN / MOON SECTION ---
            MaterialProperty sunOn = FindProperty("_CelestialToggle", properties, false);
            _showSunMoon = DrawSectionHeaderWithToggle(_showSunMoon, "Sun / Moon", sunOn, _sunMoonBanner, "_CELESTIAL_ON");
            
            if (_showSunMoon)
            {
                bool isEnabled = sunOn != null && sunOn.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);

                // Sub-banner removed
                EditorGUILayout.Space(10);
                
                // --- PRESETS ---
                DrawSectionPresetGUI(materialEditor, properties, "Sun_Moon", _sunProps, null, (preset, mat) => ApplyCelestialPreset(preset, mat, 0));

                EditorGUILayout.Space(5);

                // --- Rendering & Shape ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Rendering & Shape", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTextureObjectField(materialEditor, properties, "_Texture", "Texture");
                DrawTwoFloatVector(materialEditor, properties, "_Tiling", "Tiling");
                DrawColor(materialEditor, properties, "_Tint", "Tint");
                DrawProperty(materialEditor, properties, "_CelestialEdgeSoftness", "Edge Blur"); 
                DrawProperty(materialEditor, properties, "_Fade", "Fade");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // --- Transform ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_Size", "Size");
                DrawProperty(materialEditor, properties, "_Rotation", "Rotation"); 
                DrawCheckbox(materialEditor, properties, "_RotateWithCamera", "Rotate With Camera", "_ROTATE_WITH_CAMERA"); 
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // --- Lunar Phase ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Lunar Phase", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_Phase", "Phase Amount");
                DrawCheckbox(materialEditor, properties, "_InvertPhase", "Invert Phase", "_INVERT_PHASE");
                DrawProperty(materialEditor, properties, "_PhaseSize", "Mask Size");
                DrawProperty(materialEditor, properties, "_PhaseSmoothness", "Phase Blur");
                DrawProperty(materialEditor, properties, "_DarkSideOcclusion", "Unlit Opacity");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
                
                // --- Glow Effects ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Glow Effects", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawGlowSection(materialEditor, properties, "_", "_GLOW_ON"); // Sun/Moon Glow
                EditorGUILayout.Space(5);
                DrawAtmosphereSection(materialEditor, properties, "_Atmosphere", "_ATMOSPHERE_ON", "Outer Glow 1"); // Sun/Moon Atm 1
                EditorGUILayout.Space(); // Add spacing for consistency
                DrawAtmosphereSection(materialEditor, properties, "_Atmosphere2", "_ATMOSPHERE2_ON", "Outer Glow 2"); // Sun/Moon Atm 2
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
                
                EditorGUI.EndDisabledGroup();
            }

            DrawSeparator();

            // --- CLOUDS 1 SECTION ---
            MaterialProperty cloudsOn = FindProperty("_CloudsToggle", properties, false);
            _showClouds = DrawSectionHeaderWithToggle(_showClouds, "Clouds 1", cloudsOn, _clouds1Banner, "_CLOUDS_ON");
            
            // If Foldout is OPEN, show content (Disabled if toggle is OFF)
            if (_showClouds)
            {
                bool isEnabled = cloudsOn != null && cloudsOn.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);
                
                EditorGUILayout.Space(10);

                // --- PRESETS ---
                DrawSectionPresetGUI(materialEditor, properties, "Clouds1", _c1Props, "Clouds", (preset, mat) => ApplyCloudPreset(preset, mat, false));

                EditorGUILayout.Space(5);

                // 2. Core Visuals & Highlights
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Core Visuals", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTextureObjectField(materialEditor, properties, "_Cloud_Texture", "Cloud Texture");
                DrawColor(materialEditor, properties, "_Color_1", "Base Color");
                DrawProperty(materialEditor, properties, "_Intensity", "Opacity");
                DrawProperty(materialEditor, properties, "_Scale", "Scale");
                DrawProperty(materialEditor, properties, "_Density");
                DrawProperty(materialEditor, properties, "_CloudErosion", "Erosion");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // Cloud Highlights
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                MaterialProperty lightsOn = FindProperty("_CloudHighlightsToggle", properties, false);
                if (lightsOn != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool newVal = EditorGUILayout.ToggleLeft("Cloud Highlights", lightsOn.floatValue > 0.5f, EditorStyles.boldLabel);
                    if (EditorGUI.EndChangeCheck())
                    {
                        lightsOn.floatValue = newVal ? 1.0f : 0.0f;
                        foreach (var target in lightsOn.targets)
                        {
                            if (target is Material mat)
                            {
                                if (newVal) mat.EnableKeyword("_CLOUD_HIGHLIGHTS_ON");
                                else mat.DisableKeyword("_CLOUD_HIGHLIGHTS_ON");
                            }
                        }
                    }
                    
                    if (lightsOn.floatValue > 0.5f)
                    {
                        EditorGUILayout.Space(2);
                        DrawColor(materialEditor, properties, "_CloudHighlightColor", "Highlight Color");
                        DrawProperty(materialEditor, properties, "_CloudHighlightControl");
                        DrawProperty(materialEditor, properties, "_CloudHighlightContrast");
                        DrawProperty(materialEditor, properties, "_CloudHighlightSmoothness");
                    }
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // 4. World Blending
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("World Blending", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_Horizon_Level");
                DrawProperty(materialEditor, properties, "_Horizon_Fade");
                DrawProperty(materialEditor, properties, "_Top_Fade");
                DrawProperty(materialEditor, properties, "_CloudHeightBias", "Height Bias");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);

                // 5. Movement
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTwoFloatVector(materialEditor, properties, "_Speed", "Speed (X,Y)");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                
                // Advanced Settings (Foldout)
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                _showCloudAdvanced = EditorGUILayout.ToggleLeft("Advanced Settings", _showCloudAdvanced, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                
                if (_showCloudAdvanced)
                {

                    // Detail Texture Support
                    MaterialProperty detailToggle = FindProperty("_CloudDetailTexToggle", properties, false);
                    if (detailToggle != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        // Draw Checkbox
                        DrawCheckbox(materialEditor, properties, "_CloudDetailTexToggle", "Use Detail Texture", "_CLOUD_DETAIL_TEX_ON");
                        if (detailToggle.floatValue > 0.5f)
                        {
                            DrawTextureObjectField(materialEditor, properties, "_Cloud_Detail_Texture", "Detail Texture");
                        }
                    }
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Shape & Detail", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_CloudBaseSpeedMultiplier", "Base Speed");
                    DrawProperty(materialEditor, properties, "_CloudDetailSpeedMultiplier", "Detail Speed");
                    DrawProperty(materialEditor, properties, "_CloudDetailScale", "Detail Scale");
                    DrawProperty(materialEditor, properties, "_CloudErosionMultiplier", "Erosion Strength");
                    DrawProperty(materialEditor, properties, "_CloudAlphaEdge", "Edge Softness");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Lighting & Scatter", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_CloudHeightScale", "Normal Strength");
                    DrawProperty(materialEditor, properties, "_CloudLightingWrap", "Lighting Wrap");
                    DrawProperty(materialEditor, properties, "_CloudSunBoost", "Sun Boost Intensity");
                    DrawProperty(materialEditor, properties, "_CloudScatterMin", "Scatter Power (Sharp)");
                    DrawProperty(materialEditor, properties, "_CloudScatterMax", "Scatter Power (Broad)");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Quality & Sampling", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_CloudBaseEpsilon", "Base Epsilon");
                    DrawProperty(materialEditor, properties, "_CloudDetailEpsilon", "Detail Epsilon Scale");
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Capture Clouds 2 Settings", GUILayout.Height(30)))
                {
                     foreach(Object t in materialEditor.targets)
                     {
                         if(t is Material m) CopyCloudSettings(m, false); // From C2 -> C1
                     }
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUI.EndDisabledGroup();
            }

            DrawSeparator();

            // --- CLOUDS 2 SECTION (New) ---
            MaterialProperty clouds2On = FindProperty("_Clouds2Toggle", properties, false);
            _showClouds2 = DrawSectionHeaderWithToggle(_showClouds2, "Clouds 2", clouds2On, _clouds2Banner, "_CLOUDS2_ON");
            
            if (_showClouds2)
            {
                bool isEnabled = clouds2On != null && clouds2On.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);
                
                EditorGUILayout.Space(10);

                // --- PRESETS ---
                DrawSectionPresetGUI(materialEditor, properties, "Clouds2", _c2Props, "Clouds", (preset, mat) => ApplyCloudPreset(preset, mat, true));

                EditorGUILayout.Space(5);

                // 2. Core Visuals & Highlights
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Core Visuals", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTextureObjectField(materialEditor, properties, "_Cloud2_Texture", "Texture");
                DrawColor(materialEditor, properties, "_Cloud2_Color", "Base Color");
                DrawProperty(materialEditor, properties, "_Cloud2_Intensity", "Opacity");
                DrawProperty(materialEditor, properties, "_Cloud2_Scale", "Scale");
                DrawProperty(materialEditor, properties, "_Cloud2_Density");
                DrawProperty(materialEditor, properties, "_Cloud2_Erosion", "Erosion");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // Cloud Highlights
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                MaterialProperty lightsOn2 = FindProperty("_Cloud2HighlightsToggle", properties, false);
                if (lightsOn2 != null)
                {
                    EditorGUI.BeginChangeCheck();
                    bool newVal = EditorGUILayout.ToggleLeft("Cloud Highlights", lightsOn2.floatValue > 0.5f, EditorStyles.boldLabel);
                    if (EditorGUI.EndChangeCheck())
                    {
                        lightsOn2.floatValue = newVal ? 1.0f : 0.0f;
                        foreach (var target in lightsOn2.targets)
                        {
                            if (target is Material mat)
                            {
                                if (newVal) mat.EnableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                                else mat.DisableKeyword("_CLOUD2_HIGHLIGHTS_ON");
                            }
                        }
                    }
                    
                    if (lightsOn2.floatValue > 0.5f)
                    {
                        EditorGUILayout.Space(2);
                        DrawColor(materialEditor, properties, "_Cloud2_HighlightColor", "Highlight Color");
                        DrawProperty(materialEditor, properties, "_Cloud2_HighlightControl");
                        DrawProperty(materialEditor, properties, "_Cloud2_HighlightContrast");
                        DrawProperty(materialEditor, properties, "_Cloud2_HighlightSmoothness");
                    }
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // 4. World Blending (Shared)
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("World Blending", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_Cloud2_Horizon_Level");
                DrawProperty(materialEditor, properties, "_Cloud2_Horizon_Fade");
                DrawProperty(materialEditor, properties, "_Cloud2_Top_Fade");
                DrawProperty(materialEditor, properties, "_Cloud2_HeightBias", "Height Bias");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);

                // 5. Movement
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTwoFloatVector(materialEditor, properties, "_Cloud2_Speed", "Speed (X,Y)");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                
                // Advanced Settings (Foldout)
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                _showCloud2Advanced = EditorGUILayout.ToggleLeft("Advanced Settings", _showCloud2Advanced, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                
                if (_showCloud2Advanced)
                {

                    // Detail Texture Support
                    MaterialProperty detailToggle2 = FindProperty("_Cloud2DetailTexToggle", properties, false);
                    if (detailToggle2 != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        // Draw Checkbox
                        DrawCheckbox(materialEditor, properties, "_Cloud2DetailTexToggle", "Use Detail Texture", "_CLOUD2_DETAIL_TEX_ON");
                        if (detailToggle2.floatValue > 0.5f)
                        {
                             DrawTextureObjectField(materialEditor, properties, "_Cloud2_Detail_Texture", "Detail Texture");
                        }
                    }
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Shape & Detail", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_Cloud2_BaseSpeedMultiplier", "Base Speed");
                    DrawProperty(materialEditor, properties, "_Cloud2_DetailSpeedMultiplier", "Detail Speed");
                    DrawProperty(materialEditor, properties, "_Cloud2_DetailScale", "Detail Scale");
                    DrawProperty(materialEditor, properties, "_Cloud2_ErosionMultiplier", "Erosion Strength");
                    DrawProperty(materialEditor, properties, "_Cloud2_AlphaEdge", "Edge Softness");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Lighting & Scatter", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_Cloud2_HeightScale", "Normal Strength");
                    DrawProperty(materialEditor, properties, "_Cloud2_LightingWrap", "Lighting Wrap");
                    DrawProperty(materialEditor, properties, "_Cloud2_SunBoost", "Sun Boost Intensity");
                    DrawProperty(materialEditor, properties, "_Cloud2_ScatterMin", "Scatter Power (Sharp)");
                    DrawProperty(materialEditor, properties, "_Cloud2_ScatterMax", "Scatter Power (Broad)");

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Quality & Sampling", EditorStyles.boldLabel);
                    DrawProperty(materialEditor, properties, "_Cloud2_BaseEpsilon", "Base Epsilon");
                    DrawProperty(materialEditor, properties, "_Cloud2_DetailEpsilon", "Detail Epsilon Scale");
                }
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Capture Clouds 1 Settings", GUILayout.Height(30)))
                {
                     foreach(Object t in materialEditor.targets)
                     {
                         if(t is Material m) CopyCloudSettings(m, true); // From C1 -> C2
                     }
                }
                
                EditorGUILayout.Space(5);
                
                EditorGUI.EndDisabledGroup();
            }

            DrawSeparator();

            // --- STARS SECTION ---
            MaterialProperty starsOn = FindProperty("_StarsToggle", properties, false);
            _showStars = DrawSectionHeaderWithToggle(_showStars, "Stars", starsOn, _starsBanner, "_STARS_ON");
            
            if (_showStars)
            {
                bool isEnabled = starsOn != null && starsOn.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);
                
                EditorGUILayout.Space(10);
                
                // --- Map ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                DrawTextureObjectField(materialEditor, properties, "_StarCubeMap", "Star Map (Cubemap)");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);

                // --- Settings ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_StarExposure", "Exposure");
                DrawProperty(materialEditor, properties, "_StarSize", "Size");
                DrawColor(materialEditor, properties, "_StarTint", "Tint");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
                
                // --- Orientation ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_StarLatitude", "Latitude");
                DrawProperty(materialEditor, properties, "_StarRotation", "Rotation (Manual)");
                DrawProperty(materialEditor, properties, "_StarSpeed", "Animation Speed");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
                
                // --- Blending ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Blending", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, "_Star_Blend", "Horizon Fade");
                DrawProperty(materialEditor, properties, "_Star_Blend_Contrast", "Fade Contrast");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
                
                EditorGUI.EndDisabledGroup();
            }
            
            DrawSeparator();

            // --- GOD RAYS SECTION ---
            MaterialProperty godRaysOn = FindProperty("_GodRaysToggle", properties, false);
            _showGodRays = DrawSectionHeaderWithToggle(_showGodRays, "God Rays", godRaysOn, _godraysBanner, "_GODRAYS_ON");
            
            if (_showGodRays)
            {
                bool isEnabled = godRaysOn != null && godRaysOn.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);
                
                DrawGodRaysSection(materialEditor, properties);

                
                EditorGUI.EndDisabledGroup();
            }

            DrawSeparator();

            // --- CELESTIAL 1 ---
            MaterialProperty c1On = FindProperty("_C1_Enabled", properties, false);
            _showCelestial1 = DrawSectionHeaderWithToggle(_showCelestial1, "Planet 1", c1On, _planet1Banner, "_CELESTIAL1_ON");
            if (_showCelestial1)
            {
                 DrawExtraCelestialSection(materialEditor, properties, ref _showCelestial1, "_CELESTIAL1_ON", "_C1_", "Planet 1");
            }

            DrawSeparator();

            // --- CELESTIAL 2 ---
            MaterialProperty c2On = FindProperty("_C2_Enabled", properties, false);
            _showCelestial2 = DrawSectionHeaderWithToggle(_showCelestial2, "Planet 2", c2On, _planet2Banner, "_CELESTIAL2_ON");
            if (_showCelestial2)
            {
                 DrawExtraCelestialSection(materialEditor, properties, ref _showCelestial2, "_CELESTIAL2_ON", "_C2_", "Planet 2");
            }
            
            EditorGUILayout.Space(20);
        }

        // --- HELPERS ---

        private void DrawSeparator()
        {
            // Negate standard spacing to make sections flush vertically
            EditorGUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
            
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.x = 0; 
            rect.width = EditorGUIUtility.currentViewWidth;
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1));
            
            EditorGUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
        }

        private void DrawFullWidthBanner(Texture2D banner)
        {
            if (banner != null)
            {
                // Note: Unity Material Editors often have a default 4-8px padding at the top.
                // We use negative space to pull the banner up to the very border.
                EditorGUILayout.Space(-8);

                // Fixed height as requested
                float height = 40f; 
                float viewWidth = EditorGUIUtility.currentViewWidth;

                // 1. Calculate aspect-correct width for the fixed 40px height
                float aspect = (float)banner.width / banner.height;
                float targetWidth = height * aspect;

                // Reserve Rect in layout (force height 40)
                Rect fullRect = EditorGUILayout.GetControlRect(false, height);
                fullRect.x = 0;
                fullRect.width = viewWidth;

                // 2. Draw solid background to ensure it "sticks to border" 
                // Color matches the master preset bar background (#191919)
                EditorGUI.DrawRect(fullRect, new Color(0.098f, 0.098f, 0.098f, 1));
                
                // 3. Draw Texture aligned to the LEFT
                Rect bannerRect = new Rect(fullRect.x, fullRect.y, targetWidth, height);
                GUI.DrawTexture(bannerRect, banner, ScaleMode.StretchToFill);
            }
        }

        private void DrawProperty(MaterialEditor editor, MaterialProperty[] props, string name, string labelOverride = null)
        {
            MaterialProperty prop = FindProperty(name, props, false);
            if (prop != null)
            {
                string labelText = labelOverride ?? prop.displayName;
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(prop.name);
                editor.ShaderProperty(prop, new GUIContent(labelText, tooltipText));
            }
        }
        
        private void DrawTextureObjectField(MaterialEditor editor, MaterialProperty[] props, string name, string label = null)
        {
            MaterialProperty prop = FindProperty(name, props, false);
            if (prop != null)
            {
                // Detect Cubemap
                System.Type type = typeof(Texture);
                if (prop.textureDimension == UnityEngine.Rendering.TextureDimension.Cube)
                {
                    type = typeof(Cubemap);
                }

                // Use Unity's built-in TextureProperty which handles dimensions safely
                // We just need to ensure the label width is reset or handled if needed, 
                // but GetControlRect + TextureProperty is the standard way.
                
                // However, TextureProperty usually draws a small thumbnail slot. 
                // If we want the ObjectField style (row), we should use ObjectField 
                // BUT we must ensure the type is strictly checked BEFORE assignment.
                
                // The error "Dimensions must match" comes from prop.textureValue = newTex
                // even if newTex is a valid Texture but wrong dimension.
                
                // Let's use a specialized check that NEVER assigns unless dimensions match.
                
                EditorGUI.BeginChangeCheck();
                string labelText = label ?? prop.displayName;
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(prop.name);
                var newTex = (Texture)EditorGUI.ObjectField(EditorGUILayout.GetControlRect(), new GUIContent(labelText, tooltipText), prop.textureValue, type, false);
                if (EditorGUI.EndChangeCheck())
                {
                    try
                    {
                        if (newTex == null)
                        {
                            prop.textureValue = null;
                            return;
                        }

                        // Check if dimensions match
                        if (prop.textureDimension == UnityEngine.Rendering.TextureDimension.Cube)
                        {
                            if (newTex.dimension == UnityEngine.Rendering.TextureDimension.Cube)
                            {
                                prop.textureValue = newTex;
                            }
                            else
                            {
                                Debug.LogWarning($"[Kroma] Invalid texture '{newTex.name}'. Expected Cubemap, got {newTex.dimension}.");
                            }
                        }
                        else
                        {
                            prop.textureValue = newTex;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[Kroma] Failed to assign texture: {ex.Message}");
                    }
                }
            }
        }

        private void DrawColor(MaterialEditor editor, MaterialProperty[] props, string name, string labelOverride = null)
        {
            MaterialProperty prop = FindProperty(name, props, false);
            if (prop != null)
            {
                string label = labelOverride ?? prop.displayName;
                
                // Get one single rect for the entire line
                Rect totalRect = EditorGUILayout.GetControlRect();
                
                // FIXED SPLIT: Do NOT use PrefixLabel.
                // We define strict pixel areas.
                float labelW = EditorGUIUtility.labelWidth;
                
                // Use a reasonable default if labelWidth is glitching (e.g. > 50% of view)
                if (labelW > totalRect.width * 0.5f || labelW == 0) labelW = totalRect.width * 0.4f;

                Rect labelRect = new Rect(totalRect.x, totalRect.y, labelW, totalRect.height);
                Rect fieldRect = new Rect(totalRect.x + labelW, totalRect.y, totalRect.width - labelW, totalRect.height);
                
                // Draw Label
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(prop.name);
                EditorGUI.LabelField(labelRect, new GUIContent(label, tooltipText));
                
                // Draw Field
                EditorGUI.BeginChangeCheck();
#if UNITY_6000_1_OR_NEWER
                var pFlags = prop.propertyFlags;
#else
                var pFlags = (UnityEngine.Rendering.ShaderPropertyFlags)prop.flags;
#endif
                bool hdr = (pFlags & UnityEngine.Rendering.ShaderPropertyFlags.HDR) != 0;
                Color newValue = EditorGUI.ColorField(fieldRect, GUIContent.none, prop.colorValue, true, true, hdr);
                if (EditorGUI.EndChangeCheck())
                {
                    prop.colorValue = newValue;
                }
            }
        }

        private void DrawTwoFloatVector(MaterialEditor editor, MaterialProperty[] props, string name, string label)
        {
            MaterialProperty prop = FindProperty(name, props, false);
            if (prop != null)
            {
                Vector4 current = prop.vectorValue;

                // Get one single rect for the entire line
                Rect totalRect = EditorGUILayout.GetControlRect();
                
                // FIXED SPLIT: Do NOT use PrefixLabel.
                float labelW = EditorGUIUtility.labelWidth;
                if (labelW > totalRect.width * 0.5f || labelW == 0) labelW = totalRect.width * 0.4f;

                Rect labelRect = new Rect(totalRect.x, totalRect.y, labelW, totalRect.height);
                Rect fieldArea = new Rect(totalRect.x + labelW, totalRect.y, totalRect.width - labelW, totalRect.height);
                
                // Draw Main Label
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(prop.name);
                EditorGUI.LabelField(labelRect, new GUIContent(label, tooltipText));

                // Internal Layout: "X Value  Y Value"
                float fieldSpacing = 4f;
                float singleFieldWidth = (fieldArea.width - fieldSpacing) / 2f;
                
                Rect xRect = new Rect(fieldArea.x, fieldArea.y, singleFieldWidth, fieldArea.height);
                Rect yRect = new Rect(fieldArea.x + singleFieldWidth + fieldSpacing, fieldArea.y, singleFieldWidth, fieldArea.height);

                // Draw X and Y
                // Force label width small for the X/Y sub-labels
                float oldLabelW = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;
                
                // Indent level kills manual rects if not careful. 
                // Since we are using exact world-space rects derived from totalRect (which is indented), 
                // we should set indent to 0 for the sub-fields to avoid double indenting.
                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                EditorGUI.BeginChangeCheck();
                float newX = EditorGUI.FloatField(xRect, "X", current.x);
                float newY = EditorGUI.FloatField(yRect, "Y", current.y);
                
                // Restore state
                EditorGUIUtility.labelWidth = oldLabelW;
                EditorGUI.indentLevel = oldIndent;

                if (EditorGUI.EndChangeCheck())
                {
                    prop.vectorValue = new Vector4(newX, newY, current.z, current.w);
                }
            }
        }

        private void DrawExtraCelestialSection(MaterialEditor materialEditor, MaterialProperty[] properties, ref bool foldout, string keyword, string prefix, string label)
        {
            // Master Switch handled by Header now
            MaterialProperty enabled = FindProperty(prefix + "Enabled", properties, false);
            
            // Render if foldout is open (Disable if toggle is OFF)
            if (foldout)
            {
                bool isEnabled = enabled != null && enabled.floatValue > 0.5f;
                EditorGUI.BeginDisabledGroup(!isEnabled);
                
                EditorGUILayout.Space(10);
                
                // --- PRESETS ---
                int index = prefix == "_C1_" ? 1 : 2;
                string secName = prefix == "_C1_" ? "Planet1" : "Planet2";
                string[] secProps = prefix == "_C1_" ? _cel1Props : _cel2Props;
                DrawSectionPresetGUI(materialEditor, properties, secName, secProps, "Sun_Moon", (preset, mat) => ApplyCelestialPreset(preset, mat, index));
                
                EditorGUILayout.Space(5);
                
                // --- Rendering & Shape ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Rendering & Shape", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawTextureObjectField(materialEditor, properties, prefix + "Texture", "Texture");
                DrawTwoFloatVector(materialEditor, properties, prefix + "Tiling", "Tiling");
                DrawColor(materialEditor, properties, prefix + "Tint", "Tint");
                DrawProperty(materialEditor, properties, prefix + "EdgeSoftness", "Edge Blur");
                DrawProperty(materialEditor, properties, prefix + "Fade", "Fade");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);

                // --- Transform ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                // Position (Azimuth / Altitude) remains at the top of Transform for Planets
                DrawProperty(materialEditor, properties, prefix + "Azimuth", "Azimuth");
                DrawProperty(materialEditor, properties, prefix + "Altitude", "Altitude");
                DrawProperty(materialEditor, properties, prefix + "Size", "Size");
                DrawProperty(materialEditor, properties, prefix + "Rotation", "Rotation");
                DrawCheckbox(materialEditor, properties, prefix + "RotateWithCamera", "Rotate With Camera", prefix.ToUpper() + "ROTATE_WITH_CAMERA");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);

                // --- Lunar Phase ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Lunar Phase", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                DrawProperty(materialEditor, properties, prefix + "Phase", "Phase Amount");
                DrawCheckbox(materialEditor, properties, prefix + "InvertPhase", "Invert Phase", prefix.ToUpper() + "INVERT_PHASE");
                DrawProperty(materialEditor, properties, prefix + "PhaseSize", "Mask Size");
                DrawProperty(materialEditor, properties, prefix + "PhaseSmoothness", "Phase Blur");
                DrawProperty(materialEditor, properties, prefix + "DarkSideOcclusion", "Unlit Opacity");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(5);
                
                // --- Glow Effects ---
                EditorGUILayout.BeginVertical("helpBox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Glow Effects", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
                string glowKey = prefix == "_C1_" ? "_C1_GLOW_ON" : (prefix == "_C2_" ? "_C2_GLOW_ON" : "_GLOW_ON");
                DrawGlowSection(materialEditor, properties, prefix, glowKey);
                
                EditorGUILayout.Space(5);
                
                string atmoKey = prefix == "_C1_" ? "_C1_ATMOSPHERE_ON" : (prefix == "_C2_" ? "_C2_ATMOSPHERE_ON" : "_ATMOSPHERE_ON");
                DrawAtmosphereSection(materialEditor, properties, prefix + "Atmosphere", atmoKey, "Outer Glow 1");

                EditorGUILayout.Space(); 

                string atmo2Key = prefix == "_C1_" ? "_C1_ATMOSPHERE2_ON" : (prefix == "_C2_" ? "_C2_ATMOSPHERE2_ON" : "_ATMOSPHERE2_ON");
                DrawAtmosphereSection(materialEditor, properties, prefix + "Atmosphere2", atmo2Key, "Outer Glow 2");
                EditorGUILayout.Space(2);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
                
                EditorGUI.EndDisabledGroup();
            }
        }
        
        private void DrawGlowSection(MaterialEditor materialEditor, MaterialProperty[] properties, string prefix, string keyword)
        {
            // Removed Title "Inner Glow"
            
            // Glow (Toggle)
            MaterialProperty toggle = FindProperty(prefix + "GlowEnabled", properties, false);
            if (toggle != null)
            {
                EditorGUI.BeginChangeCheck();
                // Custom bold header toggle as requested (LEFT)
                bool enabled = EditorGUILayout.ToggleLeft("Inner Glow", toggle.floatValue > 0.5f, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    toggle.floatValue = enabled ? 1.0f : 0.0f;
                    // Handle Keyword
                    foreach (var target in toggle.targets)
                    {
                        if (target is Material mat)
                        {
                            if (enabled) mat.EnableKeyword(keyword);
                            else mat.DisableKeyword(keyword);
                        }
                    }
                }
                
                if (enabled)
                {
                    EditorGUI.indentLevel++;
                    DrawColor(materialEditor, properties, prefix + "GlowColor", "Color");
                    
                    // New Spherical Fresnel Controls
                    DrawProperty(materialEditor, properties, prefix + "GlowPower", "Fresnel");
                    DrawProperty(materialEditor, properties, prefix + "GlowIntensity", "Intensity");
                    
                    // Removed: Fresnel, Smoothness, OuterRadius (replaced by above)
                    
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawAtmosphereSection(MaterialEditor materialEditor, MaterialProperty[] properties, string baseName, string keyword, string customLabel = "Outer Glow")
        {
            // Removed Title "Outer Glow"
            
            MaterialProperty toggle = FindProperty(baseName + "Enabled", properties, false);
            if (toggle != null)
            {
                EditorGUI.BeginChangeCheck();
                // Custom bold header toggle as requested (LEFT)
                bool enabled = EditorGUILayout.ToggleLeft(customLabel, toggle.floatValue > 0.5f, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    toggle.floatValue = enabled ? 1.0f : 0.0f;
                    // Handle Keyword
                    foreach (var target in toggle.targets)
                    {
                        if (target is Material mat)
                        {
                            if (enabled) mat.EnableKeyword(keyword);
                            else mat.DisableKeyword(keyword);
                        }
                    }
                }
                
                 if (enabled)
                {
                    EditorGUI.indentLevel++;
                    DrawColor(materialEditor, properties, baseName + "Color", "Color");
                    DrawProperty(materialEditor, properties, baseName + "Size", "Size");
                    DrawProperty(materialEditor, properties, baseName + "Opacity", "Opacity");
                    DrawProperty(materialEditor, properties, baseName + "Blur", "Blur");
                    
                    // Glow Position (formerly Phase Influence)
                    // Robust Lookup (handles missing leading underscore issue for Sun/Moon if any)
                    MaterialProperty glowPosProp = FindProperty(baseName + "GlowPosition", properties, false);
                    if (glowPosProp == null) glowPosProp = FindProperty("_" + baseName + "GlowPosition", properties, false);
                    
                    if (glowPosProp != null)
                    {
                        materialEditor.ShaderProperty(glowPosProp, "Glow Position");
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawGodRaysSection(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.Space(10);

            // 1. Preset System for God Rays
            DrawSectionPresetGUI(materialEditor, properties, "GodRays", _godRaysProps, "GodRays", (preset, mat) => {
                Undo.RecordObject(mat, "Apply God Rays Preset");
                foreach(var prop in preset.properties) {
                    if (mat.HasProperty(prop.name)) {
                        var actualType = mat.shader.GetPropertyType(mat.shader.FindPropertyIndex(prop.name));
                        switch (prop.type) {
                            case ShaderPropertyData.Type.Color: if (actualType == UnityEngine.Rendering.ShaderPropertyType.Color || actualType == UnityEngine.Rendering.ShaderPropertyType.Vector) mat.SetColor(prop.name, prop.colorValue); break;
                            case ShaderPropertyData.Type.Float: if (actualType == UnityEngine.Rendering.ShaderPropertyType.Float || actualType == UnityEngine.Rendering.ShaderPropertyType.Range) mat.SetFloat(prop.name, prop.floatValue); break;
                        }
                    }
                }
            });

            EditorGUILayout.Space(5);

            // 2. Appearance
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            DrawColor(materialEditor, properties, "_GodRaysColor", "Ray Color");
            DrawProperty(materialEditor, properties, "_GodRaysDensity", "Ray Density");
            DrawProperty(materialEditor, properties, "_GodRaysContrast", "Ray Contrast");
            DrawProperty(materialEditor, properties, "_GodRaysSpeed", "Motion Speed");
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            // 3. Ray Beams
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Ray Beams", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_GodRaysPackCount", "Beam Count");
            DrawMinMaxProperty(materialEditor, properties, "_GodRaysMinPackWidth", "_GodRaysMaxPackWidth", 0.0f, 1.0f, "Beam Size");
            DrawProperty(materialEditor, properties, "_GodRaysBeamBlur", "Beam Blur");
            DrawProperty(materialEditor, properties, "_GodRaysBeamSweep", "Beam Sweep Speed");
            DrawCheckbox(materialEditor, properties, "_GodRaysRotateWithCamera", "Rotate With Camera", "_GODRAYS_ROTATE_WITH_CAMERA");
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            // 4. Rays Top
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Rays Top", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_GodRaysHoleSize", "Height");
            DrawProperty(materialEditor, properties, "_GodRaysHoleSoftness", "Top Falloff");
            DrawProperty(materialEditor, properties, "_GodRaysHoleNoise", "Noise");
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            // 5. Ray Falloff & Horizon
            EditorGUILayout.BeginVertical("helpBox");
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Ray Falloff", EditorStyles.boldLabel);
            DrawProperty(materialEditor, properties, "_GodRaysLength", "Throw Distance");
            DrawProperty(materialEditor, properties, "_GodRaysFalloff", "Bottom Falloff");
            
            // Horizon Fade (Included in Falloff box)
            DrawProperty(materialEditor, properties, "_GodRaysHorizonFade", "Horizon Level");
            DrawProperty(materialEditor, properties, "_GodRaysHorizonMaskPower", "Fade Softness");
            DrawProperty(materialEditor, properties, "_GodRaysHorizonCruft", "Horizon Variability");
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

        }
        
        private void DrawMinMaxProperty(MaterialEditor editor, MaterialProperty[] props, string minName, string maxName, float rangeMin, float rangeMax, string label)
        {
            MaterialProperty minProp = FindProperty(minName, props, false);
            MaterialProperty maxProp = FindProperty(maxName, props, false);
            
            if (minProp != null && maxProp != null)
            {
                // Rects
                Rect totalRect = EditorGUILayout.GetControlRect();
                float labelW = EditorGUIUtility.labelWidth;
                if (labelW > totalRect.width * 0.5f || labelW == 0) labelW = totalRect.width * 0.4f;

                Rect labelRect = new Rect(totalRect.x, totalRect.y, labelW, totalRect.height);
                Rect fieldRect = new Rect(totalRect.x + labelW, totalRect.y, totalRect.width - labelW, totalRect.height);

                // Draw Label
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(minProp.name);
                if (string.IsNullOrEmpty(tooltipText)) tooltipText = KromaSkyboxTooltipData.GetTooltip(maxProp.name);
                EditorGUI.LabelField(labelRect, new GUIContent(label, tooltipText));
                
                // Values
                float minVal = minProp.floatValue;
                float maxVal = maxProp.floatValue;
                
                EditorGUI.BeginChangeCheck();
                EditorGUI.MinMaxSlider(fieldRect, ref minVal, ref maxVal, rangeMin, rangeMax);
                if (EditorGUI.EndChangeCheck())
                {
                    minProp.floatValue = minVal;
                    maxProp.floatValue = maxVal;
                }
            }
        }
        
        private bool DrawSectionHeaderWithToggle(bool foldout, string title, MaterialProperty toggleProp, Texture2D backgroundBanner = null, string keyword = null)
        {
            // Increase height when a banner is present for a more premium look
            float height = backgroundBanner != null ? 30f : EditorGUIUtility.singleLineHeight;
            
            // Note: We use 0 as X and EditorGUIUtility.currentViewWidth to bypass standard inspector padding
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            
            if (backgroundBanner != null)
            {
                // Full width rect for background (True edge-to-edge)
                Rect fullRect = new Rect(0, rect.y, EditorGUIUtility.currentViewWidth, height);
                
                // Draw banner aligned to the RIGHT (aspect corrected height)
                // No vertical or right padding: we anchor directly to fullRect.xMax and full height
                float aspect = (float)backgroundBanner.width / backgroundBanner.height;
                float targetWidth = height * aspect;
                
                // Align to the absolute right edge of the window
                // Apply a 1-pixel vertical overhang to absorb the texture's built-in top/bottom padding
                float vPadding = 1f;
                Rect bannerRect = new Rect(fullRect.xMax - targetWidth, fullRect.y - vPadding, targetWidth, height + (vPadding * 2));
                GUI.DrawTexture(bannerRect, backgroundBanner, ScaleMode.StretchToFill);
            }
            else
            {
                // Draw a subtle dark background for sections without banners to maintain structure
                Rect bgRect = new Rect(0, rect.y, EditorGUIUtility.currentViewWidth, height);
                EditorGUI.DrawRect(bgRect, new Color(0.12f, 0.12f, 0.12f, 1));
            }

            // Vertically center the controls
            float controlHeight = 18f;
            float verticalOffset = (height - controlHeight) / 2f;

            // Foldout Arrow
            Rect arrowRect = new Rect(rect.x + 3, rect.y + verticalOffset, 14, controlHeight);
            foldout = EditorGUI.Foldout(arrowRect, foldout, GUIContent.none, true);
            
            // Toggle
            Rect toggleRect = new Rect(rect.x + 16, rect.y + verticalOffset, 20, controlHeight);
            string headerTooltip = toggleProp != null ? KromaSkyboxTooltipData.GetTooltip(toggleProp.name) : "";
            
            if (toggleProp != null)
            {
                EditorGUI.BeginChangeCheck();
                bool val = toggleProp.floatValue > 0.5f;
                bool newVal = EditorGUI.Toggle(toggleRect, new GUIContent("", headerTooltip), val);
                if (EditorGUI.EndChangeCheck())
                {
                    toggleProp.floatValue = newVal ? 1.0f : 0.0f;
                    
                    // Handle Keyword (Performance Optimization)
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        foreach (UnityEngine.Object target in toggleProp.targets)
                        {
                            if (target is Material mat)
                            {
                                if (newVal) mat.EnableKeyword(keyword);
                                else mat.DisableKeyword(keyword);
                            }
                        }
                    }
                }
            }
            else
            {
                // Visual-only disabled toggle (for Skybox Colors consistency)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.Toggle(toggleRect, new GUIContent("", headerTooltip), true); // Always "on" but disabled
                EditorGUI.EndDisabledGroup();
            }
            
            // Label
            Rect labelRect = new Rect(rect.x + 34, rect.y + verticalOffset, rect.width - 34, controlHeight);
            EditorGUI.LabelField(labelRect, new GUIContent(title, headerTooltip), EditorStyles.boldLabel);
            
            // Click to fold
            if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                foldout = !foldout;
                Event.current.Use();
            }
            
            return foldout;
        }

        private void DrawCheckbox(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName, string label, string keyword)
        {
            MaterialProperty prop = FindProperty(propertyName, properties, false);
            if (prop != null)
            {
                EditorGUI.BeginChangeCheck();
                bool isEnabled = prop.floatValue > 0.5f;
                string tooltipText = KromaSkyboxTooltipData.GetTooltip(prop.name);
                bool newState = EditorGUILayout.Toggle(new GUIContent(label, tooltipText), isEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    prop.floatValue = newState ? 1.0f : 0.0f;
                    foreach (var target in materialEditor.targets)
                    {
                        if (target is Material mat)
                        {
                            if (newState) mat.EnableKeyword(keyword);
                            else mat.DisableKeyword(keyword);
                        }
                    }
                }
            }
        }
    }
}
