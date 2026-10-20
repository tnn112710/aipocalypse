// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using System.Collections.Generic;
using System.Reflection;
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
#if XR
using UnityEngine.XR.OpenXR;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
#endif

namespace sc.screeneffects.editor
{
    public static class AutoSetup
    {
        private const string renderDataListFieldName = "m_RendererDataList";
        private const string renderFeaturesListFieldName = "m_RendererFeatures";
        private const string defaultRendererIndexFieldName = "m_DefaultRendererIndex";

        public static ScriptableRendererFeature IsRenderFeatureAdded<T>()
        {
            return Utilities.IsRenderFeatureAdded<T>();
        }

        public static ScriptableRendererFeature SetupEffect<T>()
        {
            if(UniversalRenderPipeline.asset == null) return null;

            ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            int defaultRendererIndex = (int)typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            ScriptableRendererData forwardRenderer = rendererDataList[defaultRendererIndex];
            
            ScriptableRendererFeature feature = (ScriptableRendererFeature)ScriptableRendererFeature.CreateInstance(typeof(T).ToString());
            feature.name = "[SC Screen Effects] " + typeof(T).Name.Replace("Renderer", string.Empty);

            //Add component https://github.com/Unity-Technologies/Graphics/blob/d0473769091ff202422ad13b7b764c7b6a7ef0be/com.unity.render-pipelines.universal/Editor/ScriptableRendererDataEditor.cs#L180
            AssetDatabase.AddObjectToAsset(feature, forwardRenderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out var guid, out long localId);

            //Get feature list
            FieldInfo renderFeaturesInfo = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(forwardRenderer);

            //Modify and set list
            m_RendererFeatures.Add(feature);
            renderFeaturesInfo.SetValue(forwardRenderer, m_RendererFeatures);

            //Onvalidate will call ValidateRendererFeatures and update m_RendererPassMap
            MethodInfo validateInfo = typeof(ScriptableRendererData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            validateInfo.Invoke(forwardRenderer, null);

#if UNITY_EDITOR
            EditorUtility.SetDirty(forwardRenderer);
            AssetDatabase.SaveAssets();
#endif
            Debug.Log("<b>" + feature.name + "</b> was added to the <i>" + forwardRenderer.name + "</i> renderer", forwardRenderer);

            return feature;
        }

        public static ScriptableRendererData[] GetRenderDataList(UniversalRenderPipelineAsset asset)
        {
            FieldInfo renderDataListField = typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (renderDataListField != null)
            {
                return (ScriptableRendererData[])renderDataListField.GetValue(asset);
            }

            throw new Exception(
                $"Reflection failed on field \"{renderDataListFieldName}\" from class \"UniversalRenderPipelineAsset\". URP API likely changed");
        }
        
        private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset asset)
        {
            FieldInfo fieldInfo = typeof(UniversalRenderPipelineAsset).GetField(defaultRendererIndexFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null)
            {
                throw new Exception(
                    $"Reflection failed on the field named \"{defaultRendererIndexFieldName}\". It may have changed in the current Unity version");
            }

            return (int)fieldInfo.GetValue(asset);
        }
        
        public static ScriptableRendererData GetDefaultRenderer(UniversalRenderPipelineAsset asset = null)
        {
            if (asset == null) asset = UniversalRenderPipeline.asset;

            if (asset)
            {
                ScriptableRendererData[] rendererDataList = GetRenderDataList(asset);
                int defaultRendererIndex = GetDefaultRendererIndex(asset);

                return rendererDataList[defaultRendererIndex];
            }

            throw new Exception("No Universal Render Pipeline is currently active.");
        }
        
        public static ScriptableRendererFeature GetCurrentRenderFeature<T>()
        {
            ScriptableRendererData renderer = GetDefaultRenderer();

            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature && feature.GetType() == typeof(T)) return feature;
            }

            return null;
        }
        
        public static bool RenderGraphEnabled()
        {
#if UNITY_6000_4_OR_NEWER
            return true;
#else
#if !UNITY_6000_3_OR_NEWER || (URP_COMPATIBILITY_MODE && UNITY_6000_3_OR_NEWER)
            RenderGraphSettings settings = UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            return settings != null ? settings.enableRenderCompatibilityMode == false : false;
#else
            return true;
#endif
#endif
        }

        public static void SetRenderGraphCompatibilityMode(bool state)
        {
#if !UNITY_6000_4_OR_NEWER
#if !UNITY_6000_3_OR_NEWER || (URP_COMPATIBILITY_MODE && UNITY_6000_3_OR_NEWER)
            UnityEngine.Rendering.Universal.RenderGraphSettings settings = UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<UnityEngine.Rendering.Universal.RenderGraphSettings>();
            settings.enableRenderCompatibilityMode = state;
#endif
#endif
        }

        #region VR
#if XR
        public static IReadOnlyList<XRLoader> GetActiveXRLoaders()
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

#if UNITY_6000_6_OR_NEWER
            string settingsKey = XRGeneralSettings.settingsKey;
#else
            string settingsKey = XRGeneralSettings.k_SettingsKey;
#endif
            
            EditorBuildSettings.TryGetConfigObject(settingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings);

            if (!buildTargetSettings) return null;
            
#if UNITY_6000_6_OR_NEWER
            IReadOnlyList<XRLoader> activeLoaders = buildTargetSettings.SettingsForBuildTarget(buildTargetGroup).Manager.activeLoaders;
#else
            IReadOnlyList<XRLoader> activeLoaders = buildTargetSettings.SettingsForBuildTarget(buildTargetGroup).AssignedSettings.activeLoaders;
#endif

            return activeLoaders;
        }
        
        public static bool IsVREnabled()
        {
            IReadOnlyList<XRLoader> loaders = GetActiveXRLoaders();
            return loaders != null && loaders.Count > 0;    
        }

        public static bool IsLoaderUsingMultiPass(XRLoader loader)
        {
            if (loader == null) return false;
            
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
            return settings != null && settings.renderMode == OpenXRSettings.RenderMode.MultiPass;
        }
        public static List<XRLoader> GetXRMultiPassLoaders()
        {
            List<XRLoader> multiPassLoaders = new List<XRLoader>();

            IReadOnlyList<XRLoader> loaders = GetActiveXRLoaders();
            if (loaders == null) return multiPassLoaders;

            foreach (XRLoader loader in loaders)
            {
                if (loader == null) continue;

                if (IsLoaderUsingMultiPass(loader))
                {
                    Debug.Log($"Found multi-pass loader: {loader.GetType().FullName}");
                    multiPassLoaders.Add(loader);
                }
            }

            return multiPassLoaders;
        }
        
        public static bool SetLoaderRenderModeToSinglePassInstanced(XRLoader loader)
        {
            if (loader == null) return false;

            // OpenXR
            if (loader.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader")
            {
                BuildTargetGroup buildTargetGroup =
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
                if (settings == null) return false;

                if (settings.renderMode == OpenXRSettings.RenderMode.SinglePassInstanced)
                {
                    return false;
                }

                settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                EditorUtility.SetDirty(settings);

                return true;
            }

            return false;
        }
#endif
        #endregion
    }
}