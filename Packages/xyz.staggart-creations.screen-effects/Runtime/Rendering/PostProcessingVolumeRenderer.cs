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
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    /// <summary>
    /// Renders the effects on a given volume directly, without needing to add render features for them
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SC Screen Effects/Post Processing Volume Renderer")]
    public class PostProcessingVolumeRenderer : MonoBehaviour
    {
        public Volume volume;
        public RenderFeatureSettings settings = new RenderFeatureSettings();

        //[SerializeField]
        private List<PostEffectFeature> effects = new List<PostEffectFeature>();
        public List<PostEffectFeature> Effects => effects;
        
        private FieldInfo renderFeaturesInfo;
        private readonly List<ScriptableRenderer> renderers = new List<ScriptableRenderer>();
        private int componentCount;

        private void Reset()
        {
            volume = this.GetComponent<Volume>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnCameraRender;
            RenderPipelineManager.beginContextRendering += OnFrameRender;
            
            renderFeaturesInfo = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            
            GetEffectsFromVolume();
        }

        private void OnFrameRender(ScriptableRenderContext context, List<Camera> targetCameras)
        {
            if (volume&& volume.sharedProfile)
            {
                int profileComponentCount = volume.sharedProfile.components.Count;
                
                //Effects added or removed, refresh
                if (profileComponentCount != componentCount)
                {
                    componentCount = profileComponentCount;
                    
                    GetEffectsFromVolume();
                }
            }
        }

        private void OnValidate()
        {
            GetEffectsFromVolume();
        }
        
        public VolumeProfile profile => volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
        public VolumeProfile sharedProfile => volume.sharedProfile;

        private void OnCameraRender(ScriptableRenderContext context, Camera targetCamera)
        {
            if(effects.Count == 0 || !volume) return;
            
            CameraType cameraType = targetCamera.cameraType;
            if (cameraType is CameraType.Reflection) return;
            
            UniversalAdditionalCameraData cameraData = targetCamera.GetUniversalAdditionalCameraData();
            //if (targetCamera.cameraType != CameraType.SceneView && cameraData.renderPostProcessing == false) return;
            
            //Bail out for unwanted cameras
            if(Utilities.ShouldRenderForCamera(settings, targetCamera, cameraType, cameraData.renderType) == false) return;

            ScriptableRenderer scriptableRenderer = cameraData.scriptableRenderer;
            List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(scriptableRenderer);

            foreach (var effect in effects)
            {
                if(effect.isActive == false) continue;
                
                effect.settings.executionOrder = settings.executionOrder;
                effect.settings.cameraTypes = settings.cameraTypes;
                effect.settings.renderingLayerMask = settings.renderingLayerMask;
                effect.settings.alwaysEnable = settings.alwaysEnable;
                
                if (m_RendererFeatures.Contains(effect) == false)
                {
                    effect.settings.OnValidate();
                    
                    m_RendererFeatures.Add(effect);
                }
            }

            //Keep track of the renderers we've added effects to, so we can remove them when the volume is disabled
            if (renderers.Contains(scriptableRenderer) == false)
            {
                renderers.Add(scriptableRenderer);
            }
        }

        public void GetEffectsFromVolume()
        {
            if (!volume || !volume.sharedProfile) return;
            
            List<VolumeComponent> components = profile.components;

            componentCount = components.Count;
            
            ClearEffects();
            
            foreach (var component in components)
            {
                Type componentType = component.GetType();

                //Only accept SCPE effects, not other volume components
                if (componentType.BaseType == typeof(PostProcessingComponent) == false)
                {
                    continue;
                }
                PostProcessingVolumeComponentAttribute componentAttribute = PostProcessingVolumeComponentAttribute.GetAttribute(component.GetType());
                if (componentAttribute == null)
                {
                    throw new Exception("Missing PostProcessingVolumeComponent attribute on " + component.GetType());
                }
                
                Type renderFeatureType = componentAttribute.renderFeatureType;
                
                #if DEBUG
                {
                    var baseType = renderFeatureType.BaseType;
                    if (baseType == null || !baseType.IsGenericType ||
                        baseType.GetGenericTypeDefinition() != typeof(PostEffectFeature<>))
                    {
                        throw new Exception($"{renderFeatureType} is not derived from PostEffectFeature");
                    }
                }
                #endif
                
                //Check if the render feature is already present on the current renderer first.
                if(Utilities.IsRenderFeatureAdded(renderFeatureType.FullName))
                {
                    continue;
                }

                //Create the feature instance
                var featureInstance = (PostEffectFeature)ScriptableObject.CreateInstance(renderFeatureType);
                featureInstance.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                
                featureInstance.Create();
                
                effects.Add(featureInstance);
            }
        }

        public void ClearEffects()
        {
            foreach (var effect in effects)
            {
                effect.Dispose();
            }
            effects.Clear();
            
            foreach (ScriptableRenderer scriptableRenderer in renderers)
            {
                List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(scriptableRenderer);
                
                for (int i = m_RendererFeatures.Count - 1; i >= 0; i--)
                {
                    ScriptableRendererFeature feature = m_RendererFeatures[i];

                    if (feature == null) continue;

                    if (feature.hideFlags.HasFlag(HideFlags.DontSave) && feature is PostEffectFeature effect)
                    {
                        //Debug.Log("Removing effect " + effect.GetType().Name + " from renderer", effect);

                        m_RendererFeatures.RemoveAt(i);
                    }
                }
            }
            renderers.Clear();
        }

        void OnDisable()
        {
            ClearEffects();
            
            RenderPipelineManager.beginCameraRendering -= OnCameraRender;
            RenderPipelineManager.beginContextRendering -= OnFrameRender;
        }
    }
}