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
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using ShaderKeywordFilter = UnityEditor.ShaderKeywordFilter;
#endif

namespace sc.screeneffects.runtime
{
    [Serializable]
    public class RenderFeatureSettings
    {
        [Flags]
        public enum CameraTypeFlags
        {
            None = 0,
            [InspectorName("Game (Base)")]
            GameBase = 1,
            [InspectorName("Game (Overlay)")]
            GameOverlay = 2,
            [InspectorName("Scene View")]
            SceneView = 4,
            [InspectorName("Preview")]
            Preview = 8,
            [InspectorName("Reflections")]
            Reflection = 16,
            [InspectorName("Hidden (HideFlags)")]
            Hidden = 32
        }

        public enum ExecutionOrder
        {
            [InspectorName("Before Unity's post processing effects")]
            BeforePostProcessing,
            [InspectorName("After Unity's post processing effects")]
            AfterPostProcessing
        }
        
        public enum Focus
        {
            BestPerformance,
            BestAppearance
        }
        
        [Tooltip("Configure the effect to render either before/after Unity's built-in post processing.")]
        public ExecutionOrder executionOrder = ExecutionOrder.BeforePostProcessing;
        
        [Tooltip("Effect will render, even if the camera has post-processing disabled")]
        public bool alwaysEnable;
        
        public RenderingLayerMask renderingLayerMask = 0xFFFFFFFF;
        
        //If not set to "Nothing" or "Everything"
        public bool UseRenderingLayers => renderingLayerMask > 0;
        
#if UNITY_EDITOR
        //Track the state of the rendering layer usage, to prevent the shader keyword from being stripped in a build
        [ShaderKeywordFilter.ApplyRulesIfNotGraphicsAPI(GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.OpenGLCore)]
        [ShaderKeywordFilter.SelectIf(true, overridePriority: true, keywordNames: ShaderKeywordStrings.LightLayers)]
#endif
        [SerializeField] [HideInInspector]
        private bool enableRenderingLayers;

        [Tooltip("Configure which camera types the effect is allowed to render on when using camera stacking" +
                 "\n\nNote that some depth-based effects will not work with camera stacking, due to how the stacking system handles the depth texture")]
        public CameraTypeFlags cameraTypes = CameraTypeFlags.GameBase | CameraTypeFlags.SceneView | CameraTypeFlags.GameOverlay;

        public bool IsAllowedCameraType(CameraTypeFlags type)
        {
            return Utilities.IsAllowedCameraType(cameraTypes, type);
        }
        
        public RenderFeatureSettings(bool disableInSceneView = false)
        {
            if(disableInSceneView) cameraTypes &= ~CameraTypeFlags.SceneView;
        }

        public RenderPassEvent GetInjectionPoint()
        {
            return executionOrder == ExecutionOrder.BeforePostProcessing ? RenderPassEvent.BeforeRenderingPostProcessing : RenderPassEvent.AfterRenderingPostProcessing;
        }

        public void OnValidate()
        {
            enableRenderingLayers = UseRenderingLayers;
        }
    }
}