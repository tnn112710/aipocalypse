//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public class CozyProjectSetup : EditorWindow
    {
        public const string k_CompleteFlag = "CZY_SETUP";

        public struct SetupStatus
        {
            public StatusType statusType;
            public string message;
            public SetupStatus(StatusType statusType, string log)
            {
                this.statusType = statusType;
                message = log;
            }
        }
        public class SetupCheck
        {
            public string name;
            public string actionName;
            public Func<SetupStatus> check;
            public Action runSetup;
            public SetupCheck(string name, Func<SetupStatus> check)
            {
                this.name = name;
                this.check = check;
            }
            public SetupCheck(string name, Func<SetupStatus> check, string actionName, Action runSetup)
            {
                this.name = name;
                this.check = check;
                this.actionName = actionName;
                this.runSetup = runSetup;
            }
        }

        public static List<SetupCheck> GetSetupChecks() => new()
        {
            new SetupCheck("Unity Version", () =>
            {
                #if UNITY_6000_5_OR_NEWER
                return new(StatusType.Pass, $"Unity version is {Application.unityVersion}");
                #elif UNITY_6000_3_OR_NEWER
                return new(StatusType.Warning, $"Unity version is older than the recommended version (Unity 6000.5). The current installed version is {Application.unityVersion}");
                #else
                return new(StatusType.Fail, $"Unity version is older than the minimum version. The current installed version is {Application.unityVersion}");
                #endif
            }),
            new SetupCheck("URP Installed", () =>
            {
                #if COZY_URP
                return new(StatusType.Pass, "URP is installed");
                #else
                return new(StatusType.Fail, "URP is not installed");
                #endif
            }),
            new SetupCheck("Visual Effect Graph", () =>
            {
                #if COZY_VFX_GRAPH
                return new(StatusType.Pass, "Visual Effect Graph is installed");
                #else
                return new(StatusType.Warning, "Visual Effect Graph is not installed. Install it using Package Manager to enable rain, snow, and fog effects that use VFX Graph.");
                #endif
            }),
            new SetupCheck("Compute Shaders", () =>
            {
                if (SystemInfo.supportsComputeShaders)
                    return new(StatusType.Pass, "Compute shaders are supported on this device");
                else
                    return new(StatusType.Fail, "Compute shaders are not supported on this device");
            }),
            new SetupCheck("Target Platform Compute Shaders", () =>
            {
                BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
                BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

                switch (activeTarget)
                {
                    case BuildTarget.WebGL:
                        // WebGL only supports compute shaders via WebGPU, not WebGL2.
                        #if UNITY_6000_0_OR_NEWER
                        return new(StatusType.Warning,
                            "Target platform is WebGL2, which does not support compute shaders. " +
                            "COZY requires compute shader support. Consider targeting WebGPU or another platform.");
                        #else
                        return new(StatusType.Warning,
                            "Target platform is WebGL, which does not support compute shaders in this Unity version.");
                        #endif

                    case BuildTarget.iOS:
                        // Compute shaders require Metal; OpenGLES2/3 on iOS do not support them.
                        var iosGraphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS);
                        if (iosGraphicsAPIs == null || !iosGraphicsAPIs.Contains(GraphicsDeviceType.Metal))
                        {
                            return new(StatusType.Warning,
                                "Target platform is iOS without Metal enabled. Compute shaders require Metal on iOS.");
                        }
                        break;

                    case BuildTarget.Android:
                        // Compute shaders require Vulkan or OpenGLES3.1+; OpenGLES2 does not support them.
                        var androidGraphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                        if (androidGraphicsAPIs == null ||
                            !(androidGraphicsAPIs.Contains(GraphicsDeviceType.Vulkan) ||
                              androidGraphicsAPIs.Contains(GraphicsDeviceType.OpenGLES3)))
                        {
                            return new(StatusType.Warning,
                                "Target platform is Android without Vulkan or OpenGLES3+ enabled. " +
                                "Compute shaders are not supported on OpenGLES2.");
                        }
                        break;
                }

                return new(StatusType.Pass, $"Target platform ({activeTarget}) is configured to support compute shaders");
            }),
            new SetupCheck("Renderer Feature", () =>
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                    return new(StatusType.Fail, "URP is not the active render pipeline");

                var failures = new List<string>();
                var inactive = new List<string>();
                foreach (var urpAsset in GetConfiguredUrpAssets())
                {
                    var list = GetRendererDataList(urpAsset);
                    if (list == null || list.Length == 0)
                    {
                        failures.Add($"{urpAsset.name}: no renderer data assets found");
                        continue;
                    }

                    int idx = GetDefaultRendererIndex(urpAsset);
                    if (idx < 0 || idx >= list.Length) idx = 0;
                    var rendererData = list[idx];
                    if (rendererData == null)
                        failures.Add($"{urpAsset.name}: default renderer data is missing");
                    else if (!rendererData.rendererFeatures.Any(feature => feature is CozyRendererFeature))
                        failures.Add($"{urpAsset.name}: renderer feature is not installed");
                    else if (!rendererData.rendererFeatures.Any(feature => feature is CozyRendererFeature && feature.isActive))
                        inactive.Add(urpAsset.name);
                }

                if (failures.Count > 0)
                    return new(StatusType.Fail, string.Join("; ", failures));
                if (inactive.Count > 0)
                    return new(StatusType.Warning, $"Renderer feature is inactive: {string.Join(", ", inactive)}");
                return new(StatusType.Pass, "Renderer feature is active on all configured URP default renderers");
            }, "Fix", () =>
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                {
                    Debug.LogError("Auto-Fix Failed: The active render pipeline is not set to URP.");
                    return;
                }

                foreach (var urpAsset in GetConfiguredUrpAssets())
                {
                    var list = GetRendererDataList(urpAsset);
                    if (list == null || list.Length == 0)
                    {
                        Debug.LogError($"Auto-Fix Failed: No renderer data assets found for {urpAsset.name}.");
                        continue;
                    }

                    int idx = GetDefaultRendererIndex(urpAsset);
                    if (idx < 0 || idx >= list.Length) idx = 0;
                    var rendererData = list[idx];
                    if (rendererData == null)
                    {
                        Debug.LogError($"Auto-Fix Failed: Default renderer data is missing for {urpAsset.name}.");
                        continue;
                    }

                    if (rendererData.rendererFeatures.Any(item => item is CozyRendererFeature && item.isActive))
                        continue;

                    var feature = rendererData.rendererFeatures.FirstOrDefault(item => item is CozyRendererFeature);
                    if (feature != null)
                    {
                        if (!feature.isActive)
                        {
                            feature.SetActive(true);
                            EditorUtility.SetDirty(feature);
                            EditorUtility.SetDirty(rendererData);
                        }
                        continue;
                    }

                    var so = new SerializedObject(rendererData);
                    var featuresProp = so.FindProperty("m_RendererFeatures");
                    var mapProp = so.FindProperty("m_RendererFeatureMap");
                    if (featuresProp == null || mapProp == null)
                    {
                        Debug.LogError($"Auto-Fix Failed: Cannot find renderer feature properties for {rendererData.name}.");
                        continue;
                    }

                    var newFeature = CreateInstance<CozyRendererFeature>();
                    newFeature.name = "Cozy Renderer Feature";
                    newFeature.SetActive(true);
                    AssetDatabase.AddObjectToAsset(newFeature, rendererData);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newFeature, out _, out long localId);

                    so.Update();
                    featuresProp.arraySize++;
                    featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = newFeature;
                    mapProp.arraySize++;
                    mapProp.GetArrayElementAtIndex(mapProp.arraySize - 1).longValue = localId;

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rendererData);
                }
                AssetDatabase.SaveAssets();
            }),
            new SetupCheck("FX Blocking Renderer", () =>
            {
                if (!CozyPreferences.RenderFXBlocking)
                    return new(StatusType.Pass, "FX blocking is disabled");

                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null)
                    return new(StatusType.Warning, "FX blocking requires an active URP asset");

                var renderers = GetRendererDataList(urpAsset);
                int index = CozyPreferences.FXBlockingRendererIndex;
                if (renderers == null || index < 0 || index >= renderers.Length || renderers[index] == null)
                    return new(StatusType.Warning, "Select a valid lightweight renderer for the FX blocking camera");
                if (index == GetDefaultRendererIndex(urpAsset) || renderers[index].rendererFeatures.Any(feature => feature != null && feature.isActive))
                    return new(StatusType.Warning, "FX blocking uses the main renderer or a renderer with active features. Create a lightweight renderer to avoid unnecessary rendering.");
                return new(StatusType.Pass, $"FX blocking uses {renderers[index].name}");
            }, "Create Renderer", () =>
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null)
                {
                    Debug.LogError("[COZY Setup] Cannot create an FX blocking renderer without an active URP asset.");
                    return;
                }

                var renderers = GetRendererDataList(urpAsset);
                int defaultIndex = GetDefaultRendererIndex(urpAsset);
                if (renderers == null || defaultIndex < 0 || defaultIndex >= renderers.Length || !(renderers[defaultIndex] is UniversalRendererData source))
                {
                    Debug.LogError("[COZY Setup] Cannot create an FX blocking renderer: the default Universal Renderer data is missing.");
                    return;
                }
                if (!AssetDatabase.IsOpenForEdit(urpAsset) || !AssetDatabase.IsOpenForEdit(CozyPreferences.Instance))
                {
                    Debug.LogError("[COZY Setup] The URP asset and COZY settings must be editable to configure the FX blocking renderer.");
                    return;
                }

                var pipelineSettings = new SerializedObject(urpAsset);
                var rendererList = pipelineSettings.FindProperty("m_RendererDataList");
                if (rendererList == null || !rendererList.isArray)
                {
                    Debug.LogError("[COZY Setup] Cannot find the URP renderer list.");
                    return;
                }

                var renderer = Instantiate(source);
                renderer.name = "COZY FX Blocking Renderer";
                var rendererSettings = new SerializedObject(renderer);
                var features = rendererSettings.FindProperty("m_RendererFeatures");
                var featureMap = rendererSettings.FindProperty("m_RendererFeatureMap");
                if (features == null || featureMap == null)
                {
                    DestroyImmediate(renderer);
                    Debug.LogError("[COZY Setup] Cannot clear renderer features from the FX blocking renderer.");
                    return;
                }
                features.ClearArray();
                featureMap.ClearArray();
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(renderer, AssetDatabase.GenerateUniqueAssetPath("Assets/COZY FX Blocking Renderer.asset"));

                int index = rendererList.arraySize;
                rendererList.arraySize++;
                rendererList.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
                pipelineSettings.ApplyModifiedProperties();
                CozyPreferences.FXBlockingRendererIndex = index;
                EditorUtility.SetDirty(CozyPreferences.Instance);
                EditorUtility.SetDirty(urpAsset);
                AssetDatabase.SaveAssets();
            }),
            new SetupCheck("Linear Colorspace", () =>
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                    return new(StatusType.Pass, "Colorspace is set to linear");

                return new(StatusType.Warning, "Colorspace is not set to linear. For the best results, please use the linear colorspace.");
            }, "Fix", () =>
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }),
            new SetupCheck("Depth Texture", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                    if (pipeline.supportsCameraDepthTexture == false)
                    {
                        return new(StatusType.Fail, $"Depth texture is not enabled in {pipeline.name}");
                    }
                }
                return new(StatusType.Pass, "Depth texture is enabled");
            }, "Fix",
            () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsCameraDepthTexture = true;
                    EditorUtility.SetDirty(pipeline);
                }
            }),
            new SetupCheck("Opaque Texture", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                    if (pipeline.supportsCameraOpaqueTexture == false)
                    {
                        return new(StatusType.Fail, $"Opaque texture is not enabled in {pipeline.name}");
                    }
                }
                return new(StatusType.Pass, "Opaque texture is enabled");
            }, "Fix",
            () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsCameraOpaqueTexture = true;
                    EditorUtility.SetDirty(pipeline);
                }
            }),
            new SetupCheck("Opaque Downsampling", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                    if (!(pipeline.opaqueDownsampling == Downsampling.None))
                    {
                        return new(StatusType.Fail, $"Opaque downsampling is enabled in {pipeline.name}. This can create pixelation artifacts");
                    }
                }
                return new(StatusType.Pass, "Opaque downsampling is set to none");
            }, "Fix",
            () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset))
                        continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    var so = new SerializedObject(pipeline);
                    var p = so.FindProperty("m_OpaqueDownsampling");
                    if (p != null) { p.enumValueIndex = (int)Downsampling.None; so.ApplyModifiedProperties(); }
                    else Debug.LogError("Field 'm_OpaqueDownsampling' not found!");
                    EditorUtility.SetDirty(pipeline);
                }
            }),
            new SetupCheck("HDR Enabled", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                    if (!(pipeline.supportsHDR == true))
                    {
                        return new(StatusType.Fail, $"HDR is disabled in {pipeline.name}");
                    }
                }
                return new(StatusType.Pass, "HDR is enabled");
            }, "Fix",
            () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.supportsHDR = true;
                    EditorUtility.SetDirty(pipeline);
                }
            }),
            new SetupCheck("Depth Texture Mode", () =>
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null) return new(StatusType.Fail, "URP is not the active render pipeline");

                var propertyInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propertyInfo == null) return new(StatusType.Fail, "Could not access the renderer list.");

                var rendererDataList = propertyInfo.GetValue(urpAsset) as ScriptableRendererData[];
                if (rendererDataList == null || rendererDataList.Length == 0) return new(StatusType.Fail, "No renderer data assets found.");

                foreach (var rendererData in rendererDataList)
                {
                    if (rendererData == null) continue;
                    var so = new SerializedObject(rendererData);
                    var copyDepthProp = so.FindProperty("m_CopyDepthMode");
                    if (copyDepthProp != null && copyDepthProp.intValue != 0) // 0 corresponds to CopyDepthMode.AfterOpaques
                    {
                        return new(StatusType.Fail, $"Depth texture mode is not set to After Opaques in {rendererData.name}");
                    }
                }
                return new(StatusType.Pass, "Depth texture mode is set to After Opaques");
            }, "Fix", () =>
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null) return;

                var propertyInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propertyInfo == null) return;

                var rendererDataList = propertyInfo.GetValue(urpAsset) as ScriptableRendererData[];
                if (rendererDataList == null) return;

                foreach (var rendererData in rendererDataList)
                {
                    if (rendererData == null) continue;
                    var so = new SerializedObject(rendererData);
                    var copyDepthProp = so.FindProperty("m_CopyDepthMode");
                    if (copyDepthProp != null && copyDepthProp.intValue != 0)
                    {
                        copyDepthProp.intValue = 0;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rendererData);
                    }
                }
                AssetDatabase.SaveAssets();
            }),
            new SetupCheck("Project Layers", () =>
            {
                string[] requiredLayers = { "Before Clouds", "After Clouds", "FX Block" };
                foreach (var layer in requiredLayers)
                {
                    if (LayerMask.NameToLayer(layer) == -1)
                    {
                        return new(StatusType.Fail, $"Required layer '{layer}' is missing from the project");
                    }
                }
                return new(StatusType.Pass, "All required layers are present");
            }, "Fix", () =>
            {
                string[] requiredLayers = { "Before Clouds", "After Clouds", "FX Block" };
                var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layersProp = tagManager.FindProperty("layers");

                foreach (var layer in requiredLayers)
                {
                    if (LayerMask.NameToLayer(layer) != -1) continue;

                    bool added = false;
                    for (int i = 8; i < 32; i++)
                    {
                        var slot = layersProp.GetArrayElementAtIndex(i);
                        if (string.IsNullOrEmpty(slot.stringValue))
                        {
                            slot.stringValue = layer;
                            added = true;
                            break;
                        }
                    }
                    if (!added)
                    {
                        Debug.LogError($"Auto-Fix Failed: No empty layer slots available for '{layer}'.");
                    }
                }
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }),
            new SetupCheck("Renderer Layer Masks", () =>
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null) return new(StatusType.Fail, "URP is not the active render pipeline");

                var propertyInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propertyInfo == null) return new(StatusType.Fail, "Could not access the renderer list.");

                var rendererDataList = propertyInfo.GetValue(urpAsset) as ScriptableRendererData[];
                if (rendererDataList == null || rendererDataList.Length == 0) return new(StatusType.Fail, "No renderer data assets found.");

                string[] targetLayers = { "Before Clouds", "After Clouds", "FX Block" };
                int targetMask = 0;
                foreach (var layer in targetLayers)
                {
                    int layerIdx = LayerMask.NameToLayer(layer);
                    if (layerIdx != -1) targetMask |= 1 << layerIdx;
                }

                foreach (var rendererData in rendererDataList)
                {
                    if (rendererData == null) continue;
                    var so = new SerializedObject(rendererData);
                    var prepassProp = so.FindProperty("m_PrepassLayerMask");
                    var opaqueProp = so.FindProperty("m_OpaqueLayerMask");
                    var transparentProp = so.FindProperty("m_TransparentLayerMask");

                    if (prepassProp != null && (prepassProp.intValue & targetMask) != 0)
                    {
                        return new(StatusType.Fail, $"Prepass layer mask in {rendererData.name} does not exclude required layers.");
                    }

                    if (opaqueProp != null && (opaqueProp.intValue & targetMask) != 0)
                    {
                        return new(StatusType.Fail, $"Opaque layer mask in {rendererData.name} does not exclude required layers.");
                    }
                    if (transparentProp != null && (transparentProp.intValue & targetMask) != 0)
                    {
                        return new(StatusType.Fail, $"Transparent layer mask in {rendererData.name} does not exclude required layers.");
                    }
                }
                return new(StatusType.Pass, "Opaque and Transparent layer masks correctly exclude specified layers");
            }, "Fix", () =>
            {
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset == null) return;

                var propertyInfo = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propertyInfo == null) return;

                var rendererDataList = propertyInfo.GetValue(urpAsset) as ScriptableRendererData[];
                if (rendererDataList == null) return;

                string[] targetLayers = { "Before Clouds", "After Clouds", "FX Block" };
                int targetMask = 0;
                foreach (var layer in targetLayers)
                {
                    int layerIdx = LayerMask.NameToLayer(layer);
                    if (layerIdx != -1) targetMask |= (1 << layerIdx);
                }

                foreach (var rendererData in rendererDataList)
                {
                    if (rendererData == null) continue;
                    var so = new SerializedObject(rendererData);
                    var prepassProp = so.FindProperty("m_PrepassLayerMask");
                    var opaqueProp = so.FindProperty("m_OpaqueLayerMask");
                    var transparentProp = so.FindProperty("m_TransparentLayerMask");

                    bool changed = false;
                    if (prepassProp != null && (prepassProp.intValue & targetMask) != 0)
                    {
                        prepassProp.intValue &= ~targetMask;
                        changed = true;
                    }
                    if (opaqueProp != null && (opaqueProp.intValue & targetMask) != 0)
                    {
                        opaqueProp.intValue &= ~targetMask;
                        changed = true;
                    }
                    if (transparentProp != null && (transparentProp.intValue & targetMask) != 0)
                    {
                        transparentProp.intValue &= ~targetMask;
                        changed = true;
                    }

                    if (changed)
                    {
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(rendererData);
                    }
                }
                AssetDatabase.SaveAssets();
            }),
            new SetupCheck("Color Grading Mode", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];

                    if (pipeline.colorGradingMode != ColorGradingMode.HighDynamicRange)
                    {
                        return new(StatusType.Fail, $"Color grading mode is not set to High Dynamic Range in {pipeline.name}");
                    }
                }
                return new(StatusType.Pass, "Color grading mode is set to High Dynamic Range");
            }, "Fix", () =>
            {
                for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
                {
                    if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;
                    UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                    pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
                    EditorUtility.SetDirty(pipeline);
                }
            }),
        };


        [MenuItem("Tools/COZY: Weather 4/Project Setup", priority = 0)]
        public static void ShowSetupWizard()
        {
            CozyProjectSetup wnd = GetWindow<CozyProjectSetup>();
            wnd.titleContent = new GUIContent("COZY Setup");
        }


        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/cozy-setup-wizard.uxml"
            );

            if (asset == null)
            {
                Label errorLabel = new Label(
                    "COZY Setup Wizard UI could not be loaded. The UXML asset is missing or the package path has changed " +
                    "(expected at Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/cozy-setup-wizard.uxml).");
                errorLabel.style.whiteSpace = WhiteSpace.Normal;
                root.Add(errorLabel);
                Debug.LogError("<b>[COZY Setup]</b> Could not load cozy-setup-wizard.uxml. Aborting window setup.");
                return;
            }

            asset.CloneTree(root);

            Toolbar toolbar = new();
            root.Add(toolbar);

            ScrollView scrollView = new ScrollView();
            root.Add(scrollView);

            List<(SetupStatus status, SetupCheck check)> sortedChecks = new();

            foreach (var setupCheck in GetSetupChecks())
            {
                SetupStatus result;
                try
                {
                    result = setupCheck.check.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"<b>[COZY Setup]</b> Check '{setupCheck.name}' threw an exception and was skipped: {e}");
                    result = new SetupStatus(StatusType.Warning, $"This check could not be run: {e.Message}");
                }
                sortedChecks.Add((result, setupCheck));
            }

            sortedChecks = sortedChecks.OrderByDescending(x => x.status.statusType).ToList();

            ToolbarButton warningCount = new();
            warningCount.AddToClassList("statusCount");
            VisualElement warningIcon = new VisualElement
            {
                name = "warning"
            };
            warningCount.Add(warningIcon);
            Label warningLabel = new()
            {
                text = $"{sortedChecks.Count(x => x.status.statusType == StatusType.Warning)}"
            };
            warningCount.Add(warningLabel);
            toolbar.Add(warningCount);

            ToolbarButton errorCount = new();
            errorCount.AddToClassList("statusCount");
            VisualElement errorIcon = new VisualElement
            {
                name = "error"
            };
            errorCount.Add(errorIcon);
            Label errorLabel2 = new()
            {
                text = $"{sortedChecks.Count(x => x.status.statusType == StatusType.Fail)}"
            };
            errorCount.Add(errorLabel2);
            toolbar.Add(errorCount);

            ToolbarButton fixAll = new();
            fixAll.text = "Fix All";
            fixAll.RegisterCallback<ClickEvent>(evt =>
            {
                RunAllFixes();
                RefreshUI();
            });

            if (sortedChecks.Any(x => x.check.runSetup != null && (x.status.statusType == StatusType.Fail || x.status.statusType == StatusType.Warning)))
                toolbar.Add(fixAll);

            foreach (var check in sortedChecks)
            {
                VisualElement checkContainer = new VisualElement();
                checkContainer.AddToClassList("setupCheck");
                checkContainer.AddToClassList(check.status.statusType.ToString().ToLower());

                VisualElement titleContainer = new VisualElement();
                checkContainer.Add(titleContainer);

                Label title = new Label(check.check.name);
                title.name = "title";
                titleContainer.Add(title);

                Label description = new Label(check.status.message);
                description.name = "description";
                titleContainer.Add(description);

                if (check.status.statusType > StatusType.Pass && check.check.runSetup != null)
                {
                    Button setupButton = new Button();
                    setupButton.text = check.check.actionName;
                    setupButton.RegisterCallback<ClickEvent>(evt =>
                    {
                        try { check.check.runSetup.Invoke(); }
                        catch (Exception e) { Debug.LogError($"<b>[COZY Setup]</b> Fix for '{check.check.name}' failed: {e}"); }
                        RefreshUI();
                    });
                    checkContainer.Add(setupButton);
                }

                scrollView.Add(checkContainer);
            }
        }

        internal static StatusType CheckForSetup()
        {
            StatusType status = StatusType.Pass;
            foreach (var setupCheck in GetSetupChecks())
            {
                SetupStatus checkStatus;
                try
                {
                    checkStatus = setupCheck.check.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"<b>[COZY Setup]</b> Check '{setupCheck.name}' threw an exception during CheckForSetup: {e}");
                    checkStatus = new SetupStatus(StatusType.Warning, e.Message);
                }

                if ((int)checkStatus.statusType > (int)status)
                {
                    status = checkStatus.statusType;
                }
            }

            // Only mark setup as complete when every check actually passes.
            // Previously this flag was set unconditionally, so a project that
            // failed setup would still be treated as "already configured".
            if (status == StatusType.Pass)
            {
                PlayerPrefs.SetInt(k_CompleteFlag, 1);
            }

            return status;
        }

        private static IEnumerable<UniversalRenderPipelineAsset> GetConfiguredUrpAssets()
        {
            return GraphicsSettings.allConfiguredRenderPipelines
                .OfType<UniversalRenderPipelineAsset>()
                .Concat(new[] { GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset })
                .Where(asset => asset != null)
                .Distinct();
        }

        private static ScriptableRendererData[] GetRendererDataList(UniversalRenderPipelineAsset asset)
        {
            var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(asset) as ScriptableRendererData[];
        }

        private void RefreshUI()
        {
            AssetDatabase.SaveAssets();
            rootVisualElement.schedule.Execute(CreateGUI).ExecuteLater(50);
        }

        private static void RunAllFixes()
        {
            var checks = GetSetupChecks();
            for (int pass = 0; pass < 3; pass++)
            {
                bool ranAny = false;
                foreach (var c in checks)
                {
                    if (c.runSetup == null) continue;

                    SetupStatus s;
                    try { s = c.check(); }
                    catch { continue; }
                    if (s.statusType == StatusType.Pass) continue;

                    try { c.runSetup(); ranAny = true; }
                    catch (Exception e) { Debug.LogError($"<b>[COZY Setup]</b> Fix for '{c.name}' failed: {e}"); }

                    AssetDatabase.SaveAssets();
                }
                if (!ranAny) break;
            }
        }

        private static bool CheckForCozyRendererFeature(ScriptableRendererData[] rendererData)
        {
            foreach (var renderer in rendererData)
            {
                if (renderer.rendererFeatures.Any(x => x is CozyRendererFeature))
                {
                    Debug.Log(renderer.name);
                }
            }

            return true;
        }

        private static bool CheckForRendererFeature(out ScriptableRendererData[] rendererData)
        {
            rendererData = null;
            RenderPipelineAsset currentAsset = GraphicsSettings.currentRenderPipeline;

            if (currentAsset == null)
            {
                Debug.LogError("No active Render Pipeline Asset found in Project Settings.");
                return false;
            }

            UniversalRenderPipelineAsset urpAsset = currentAsset as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogError($"The active render pipeline is not a URP Asset. It is a: {currentAsset.GetType().Name}");
                return false;
            }

            FieldInfo rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (rendererDataListField == null)
            {
                Debug.LogError("Could not find 'm_RendererDataList' via reflection. Unity may have changed the internal property name.");
                return false;
            }

            ScriptableRendererData[] rendererDataList = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];

            if (rendererDataList == null || rendererDataList.Length == 0)
            {
                Debug.LogWarning("The active URP Asset does not have any Renderers assigned to its list.");
                return false;
            }

            rendererData = rendererDataList;

            return true;
        }

        private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset asset)
        {
            FieldInfo defaultRendererIndexField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_DefaultRendererIndex",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (defaultRendererIndexField != null)
            {
                return (int)defaultRendererIndexField.GetValue(asset);
            }
            return -1;
        }
    }
}