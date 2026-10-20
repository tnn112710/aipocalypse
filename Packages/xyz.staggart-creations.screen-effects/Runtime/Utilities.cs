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

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    public static class Utilities
    {
        private const string renderDataListFieldName = "m_RendererDataList";
        private const string renderFeaturesListFieldName = "m_RendererFeatures";
        private const string defaultRendererIndexFieldName = "m_DefaultRendererIndex";
        private const string supportsLightLayersFieldName = "m_SupportsLightLayers";
        
        /// <summary>
        /// Checks if post-processing pass should execute, based on current settings
        /// </summary>
        /// <returns></returns>
        public static bool ShouldRenderForCamera(RenderFeatureSettings settings, Camera camera, CameraType cameraType, CameraRenderType cameraRenderType)
        {
            #if UNITY_EDITOR
            if (cameraType == CameraType.SceneView)
            {
                if (settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.SceneView) == false) return false;
                
                //Bail out if post processing is disabled in the scene view
                if (UnityEditor.SceneView.lastActiveSceneView && UnityEditor.SceneView.lastActiveSceneView.sceneViewState.showImageEffects == false) return false;
            }
            
            //Don't interfere with the Rendering Debugger
            //if (Shader.IsKeywordEnabled(ShaderKeywordStrings.DEBUG_DISPLAY)) return false;
            #endif
            
            if (cameraType == CameraType.Game)
            {
                //Hidden cameras, created through script
                if(camera.hideFlags != HideFlags.None && settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.Hidden) == false) return false;
                
                if(cameraRenderType == CameraRenderType.Base && settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.GameBase) == false) return false;
                if(cameraRenderType == CameraRenderType.Overlay && settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.GameOverlay) == false) return false;
            } 
            
            //Reflection prove captures
            if(cameraType == CameraType.Reflection && settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.Reflection) == false) return false;
            //Preview box in the scene-view
            if(cameraType == CameraType.Preview && settings.IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags.Preview) == false) return false;

            //Passed all checks
            return true;
        }

        public static bool Is2DRenderer(ScriptableRenderer renderer)
        {
            return renderer.GetType() != typeof(UniversalRenderer);
        }
        
        public static bool Using2DRenderer()
        {
            ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            int defaultRendererIndex = (int)typeof(UniversalRenderPipelineAsset).GetField(defaultRendererIndexFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            ScriptableRendererData rendererData = rendererDataList[defaultRendererIndex];
            
            return rendererData.GetType() == typeof(Renderer2DData);
        }
        
        public static void GetRendererType(ScriptableRenderer renderer, out bool is2D, out bool isDeferred)
        {
            //Renderer type we're looking for is "Univeral2D", but of course that's been made internal. Assume anything that isn't 3D to be the 2D renderer
            is2D = Is2DRenderer(renderer);

            isDeferred = false;
            
            if (is2D == false)
            {
                ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
                int defaultRendererIndex = (int)typeof(UniversalRenderPipelineAsset).GetField(defaultRendererIndexFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
                UniversalRendererData r = (UniversalRendererData)rendererDataList[defaultRendererIndex];
                    
                isDeferred = r.renderingMode == RenderingMode.Deferred;
#if UNITY_6000_1_OR_NEWER
                isDeferred |= r.renderingMode == RenderingMode.DeferredPlus;
#endif
            }
        }

        public static List<UniversalRenderPipelineAsset> GetPipelineAssetsWithRenderingLayersDisabled()
        {
            List<UniversalRenderPipelineAsset> renderers = new List<UniversalRenderPipelineAsset>();

            for (int i = 0; i < GraphicsSettings.allConfiguredRenderPipelines.Length; i++)
            {
                if (GraphicsSettings.allConfiguredRenderPipelines[i].GetType() != typeof(UniversalRenderPipelineAsset)) continue;

                UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.allConfiguredRenderPipelines[i];
                
                if (pipeline.useRenderingLayers == false)
                {
                    renderers.Add(pipeline);
                }
            }
            
            return renderers;
        }
        
        public static void EnableRenderingLayers(UniversalRenderPipelineAsset renderer)
        {
            FieldInfo fieldInfo = typeof(UniversalRenderPipelineAsset).GetField(supportsLightLayersFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(renderer, true);
        }
                
        //Flag check without CG allocation
        public static bool IsAllowedCameraType(RenderFeatureSettings.CameraTypeFlags settings, RenderFeatureSettings.CameraTypeFlags type)
        {
            return (settings & type) == type;
        }
        
        public static float CalculateLuminanceThreshold(float value)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Gamma ? Mathf.LinearToGammaSpace(value) : value;
        }
        
        public static float CalculateRadiusScale(int resolutionHeight, int min, int max)
        {
            float range = (float)max - min;
            //Normalized blend value between 720p and 4k
            float relativeResolution = Mathf.Clamp01((resolutionHeight - (float)min) / range);
            
            //Value is 1.0 at 720p, 2.0 at 1080p, 3.0 at 4K
            float radiusScale = Mathf.Lerp(1f, 3f, relativeResolution);

            return radiusScale;
        }
        
        public static void DownscaleBuffer(ref TextureDesc textureDesc, PostProcessingComponent.Resolution resolution)
        {
            textureDesc.width /= (int)resolution;
            textureDesc.height /= (int)resolution;
        }
        
        #region renderer reflection
        public static ScriptableRendererFeature IsRenderFeatureAdded<T>()
        {
            if(UniversalRenderPipeline.asset == null) return null;

            ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            ScriptableRendererData forwardRenderer = rendererDataList[0];
            
            foreach (ScriptableRendererFeature item in forwardRenderer.rendererFeatures)
            {
                if (item == null) continue;

                if (item.GetType() == typeof(T))
                {
                    return item;
                }
            }

            return null;
        }
        
        public static ScriptableRendererFeature IsRenderFeatureAdded(string typeName)
        {
            if(UniversalRenderPipeline.asset == null) return null;

            ScriptableRendererData[] rendererDataList = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField(renderDataListFieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(UniversalRenderPipeline.asset);
            ScriptableRendererData forwardRenderer = rendererDataList[0];
            
            foreach (ScriptableRendererFeature item in forwardRenderer.rendererFeatures)
            {
                if (item == null) continue;

                if (item.GetType().FullName == typeName)
                {
                    return item;
                }
            }

            return null;
        }

        public static bool OnTileRenderingEnabled(UniversalRendererData rendererData)
        {
            #if UNITY_6000_6_OR_NEWER
            return rendererData.tileOnlyMode;
            #else
            return false;
            #endif
        }
        #endregion
    }
}