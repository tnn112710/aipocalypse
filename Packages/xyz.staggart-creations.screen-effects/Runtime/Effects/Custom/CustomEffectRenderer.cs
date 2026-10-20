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
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("SC Screen Effects/Custom Effect Renderer")]
    public class CustomEffectRenderer : MonoBehaviour
    {
        public Material material;
        [Tooltip("Executes the effect before transparent materials are rendered.")]
        public bool skipTransparents;
        public RenderFeatureSettings settings = new RenderFeatureSettings();
        
        private FieldInfo renderFeaturesInfo;
        private readonly List<ScriptableRenderer> renderers = new List<ScriptableRenderer>();
        
        private CustomEffectRenderFeature effect;
        
        private void OnEnable()
        {
            effect = ScriptableObject.CreateInstance<CustomEffectRenderFeature>();
            renderFeaturesInfo = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            
            RenderPipelineManager.beginCameraRendering += OnCameraRender;
        }

        private void OnCameraRender(ScriptableRenderContext context, Camera targetCamera)
        {
            CameraType cameraType = targetCamera.cameraType;
            if (cameraType is CameraType.Reflection) return;
            
            UniversalAdditionalCameraData cameraData = targetCamera.GetUniversalAdditionalCameraData();
            //if (targetCamera.cameraType != CameraType.SceneView && cameraData.renderPostProcessing == false) return;
            
            //Bail out for unwanted cameras
            if(Utilities.ShouldRenderForCamera(settings, targetCamera, cameraType, cameraData.renderType) == false) return;

            ScriptableRenderer scriptableRenderer = cameraData.scriptableRenderer;
            List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(scriptableRenderer);

            effect.effectSettings.material = material;
            effect.effectSettings.skipTransparents = skipTransparents;
            
            effect.settings.executionOrder = settings.executionOrder;
            effect.settings.cameraTypes = settings.cameraTypes;
            effect.settings.renderingLayerMask = settings.renderingLayerMask;
            effect.settings.alwaysEnable = settings.alwaysEnable;
            
            if (m_RendererFeatures.Contains(effect) == false)
            {
                effect.settings.OnValidate();
                effect.Create();
                    
                //Debug.Log($"Adding custom effect to {scriptableRenderer}");
                m_RendererFeatures.Add(effect);
            }
            
            //Keep track of the renderers we've added effects to, so we can remove them when the volume is disabled
            if (renderers.Contains(scriptableRenderer) == false)
            {
                renderers.Add(scriptableRenderer);
            }
        }

        /// <summary>
        /// Remove the effects from the renderers
        /// </summary>
        public void Clear()
        {
            foreach (ScriptableRenderer scriptableRenderer in renderers)
            {
                List<ScriptableRendererFeature> m_RendererFeatures = (List<ScriptableRendererFeature>)renderFeaturesInfo.GetValue(scriptableRenderer);
                
                for (int i = m_RendererFeatures.Count - 1; i >= 0; i--)
                {
                    ScriptableRendererFeature feature = m_RendererFeatures[i];

                    if (feature == null) continue;

                    if (feature == effect)
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
            RenderPipelineManager.beginCameraRendering -= OnCameraRender;
            
            Clear();
            CoreUtils.Destroy(effect);
        }
    }
}