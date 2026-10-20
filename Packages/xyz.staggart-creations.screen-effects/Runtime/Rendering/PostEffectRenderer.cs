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
using UnityEngine.Rendering.Universal;
using sc.screeneffects.runtime.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using Unity.Scripting.LifecycleManagement;

namespace sc.screeneffects.runtime
{
    /// <summary>
    /// Base class for screen-space post processing through a ScriptableRenderPass
    /// </summary>
    /// <typeparam name="T">Related settings class</typeparam>
    [DisallowMultipleRendererFeature]
    [NoAutoStaticsCleanup]
    public partial class PostEffectRenderer<T> : ScriptableRenderPass
    {
        public bool render;

        /// <summary>
        /// VolumeComponent settings instance
        /// </summary>
        public T volumeSettings;
        public RenderFeatureSettings settings;
        
        protected string shaderName;
        private Shader shader;
        internal Shader Shader => shader;
        protected string ProfilerTag;
        
        internal string GetProfilerTag()
        {
            if (Application.isEditor || Debug.isDebugBuild)
            {
                return shaderName.Replace(ShaderNames.PREFIX, "[SC Screen Effects] ");
            }
            
            return shaderName;
        }
        
        private static bool is2D;

        private bool xrRendering;
        protected bool onTileRendering;
        protected bool requiresDepth = false;
        protected bool requiresNormals = false;
        public bool reconstructNormalsFromDepth = true;
        protected bool requiresMotionVectors = false;
        private ScriptableRenderPassInput requirements;

        protected Material Material;

        internal UniversalResourceData resourceData;
        internal UniversalCameraData cameraData;
        internal TextureHandle cameraColor;
        internal TextureDesc cameraColorDesc;
        internal TextureHandle cameraDepth;
        
        private bool hasRenderingLayersTexture;
        private TextureHandle renderingLayersTexture;
        internal TextureHandle destination;

        protected SphereMapper sphereMapper;

        private const int MIN_RESOLUTION_HEIGHT = 720;
        private const int MAX_RESOLUTION_HEIGHT = 2160;
        
        //Default to full screen
        internal static Vector4 blitScale = new Vector4(1, 1, 0, 0);
        
        public class BasePassData
        {
            internal TextureHandle source;
            internal TextureHandle destination;
            
            internal Material material;
            internal uint renderingLayerMask;
            internal TextureHandle cameraNormalsTexture;
        }
        
        internal static Material _BlitMaterial;
        internal static Material BlitMaterial
        {
            get
            {
                if (!_BlitMaterial)
                {
                    var shaders = GraphicsSettings.GetRenderPipelineSettings<UniversalRenderPipelineRuntimeShaders>();
                    Shader blitShader = shaders.coreBlitPS;
                    
                    _BlitMaterial = CoreUtils.CreateEngineMaterial(blitShader);
                }
                
                return _BlitMaterial;
            }
        }

        internal void Blit(RenderGraph renderGraph, TextureHandle source, TextureHandle target, Material material, int pass)
        {
            RenderGraphUtils.BlitMaterialParameters parameters = new RenderGraphUtils.BlitMaterialParameters(source, target, material, pass);
            renderGraph.AddBlitPass(parameters);
        }
        
        internal void BlitCopy(RenderGraph renderGraph, TextureHandle source, TextureHandle target)
        {
            Blit(renderGraph, source, target, BlitMaterial, 0);
        }
        
        private static int prevRendererID = -1;
        private static int currentRendererID = 0;

        private static void DetermineRendererType(ScriptableRenderer renderer)
        {
            //Check if the renderer has changed. May be switching between 2D and 3D, or Forward and Deferred
            currentRendererID = renderer.GetHashCode();
            
            if (currentRendererID != prevRendererID)
            {
                prevRendererID = currentRendererID;

                is2D = Utilities.Is2DRenderer(renderer);
                
                #if SCPE_DEV
                //Debug.Log($"[SC Screen Effects] Renderer changed or initialized. Type:{renderer.GetType()}. is2D: {is2D}");
                #endif
            }
        }

        //Always fetch the volume component first, then call this base implementation
        public virtual void Setup(ScriptableRenderer renderer, RenderingData renderingData)
        {
            render = true;

            //Shaders are referenced on the corresponding volume profile asset. The render feature may be present, but the settings component may be absent in a build!
            if(!shader) shader = Shader.Find(shaderName);
            
            #if DEBUG
            if (!shader)
            {
                Debug.LogError("[SC Screen Effects] Shader with the name <i>" + shaderName +
                               "</i> could not be found, this means it was not included in the build. This will happen when effects are entirely initialized through scripting. Be sure to reference the shader somewhere.");
                render = false;
                return;
            }
            #endif
            
            Camera camera = renderingData.cameraData.camera;
            
            if (Utilities.ShouldRenderForCamera(settings, camera, camera.cameraType, renderingData.cameraData.renderType) == false)
            {
                render = false;
                return;
            }
            
            //Post-processing disabled and not forced
            if (renderingData.postProcessingEnabled == false && !settings.alwaysEnable)
            {
                render = false;
                return;
            }

            DetermineRendererType(renderer);
            
            //The pass will read the current color texture. That needs to be an intermediate texture. It's not supported to use the BackBuffer as input texture. 
            requiresIntermediateTexture = true;

            requirements = ScriptableRenderPassInput.None;
            if (requiresDepth) requirements |= ScriptableRenderPassInput.Depth;
            if (requiresNormals && !reconstructNormalsFromDepth) requirements |= ScriptableRenderPassInput.Normal;
            if (requiresMotionVectors) requirements |= ScriptableRenderPassInput.Motion;
            
            ConfigureInput(requirements);
        }
        
        protected virtual void ConfigurePass(RenderGraph renderGraph, ContextContainer frameData, bool requireDestination = true, bool bilinear = false)
        {
            resourceData = frameData.Get<UniversalResourceData>();
            cameraData = frameData.Get<UniversalCameraData>();
            
            cameraColor = resourceData.activeColorTexture;
            cameraColorDesc = renderGraph.GetTextureDesc(cameraColor);
            
            cameraDepth = resourceData.activeDepthTexture;
            
            renderingLayersTexture = resourceData.renderingLayersTexture;
            if (is2D == false)
            {
                hasRenderingLayersTexture = InternalRenderFeature.RequiresRenderingLayers(cameraData.renderer);
            }

            if (requireDestination && !onTileRendering)
            {
                TextureDesc destinationDesc = cameraColorDesc;

                if(bilinear) destinationDesc.filterMode = FilterMode.Bilinear;
                
                #if DEBUG
                destinationDesc.name = $"CameraColor-{ProfilerTag}";
                #endif

                destinationDesc.clearBuffer = false;
                destination = renderGraph.CreateTexture(destinationDesc);
            }
            
            CreateMaterialIfNull(ref Material, shader);
            
            PostEffectDebug.ApplySliderValue(Material);

            SetupNormals(renderGraph);
        }

        public void SetupInputs(IRasterRenderGraphBuilder builder, BasePassData passData)
        {
            if (is2D == false)
            {
                if (requiresDepth)
                {
                    builder.UseTexture(cameraDepth, AccessFlags.Read);
                }

                if (requiresNormals)
                {
                    builder.UseTexture(cameraNormalsTexture, AccessFlags.Read);
                    passData.cameraNormalsTexture = cameraNormalsTexture;
                }
            }
        }
        
        public void SetupRenderingLayers(IRasterRenderGraphBuilder builder, BasePassData data)
        {
            var readRenderingLayers = hasRenderingLayersTexture && settings.UseRenderingLayers && !is2D;
            
            CoreUtils.SetKeyword(Material, ShaderKeywordStrings.LightLayers, readRenderingLayers);

            if (!readRenderingLayers) return;
            
            builder.UseTexture(renderingLayersTexture, AccessFlags.Read);
            
            uint layerMask = settings.renderingLayerMask.value;
            if (layerMask == 0xFFFFFFFF) layerMask = 0;
            
            data.renderingLayerMask = layerMask;
            data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
        }

        public void SetupMaterial(BasePassData passData)
        {
            passData.material.SetInteger(ShaderParameters.RenderingLayer, (int)passData.renderingLayerMask);
            if (requiresNormals && passData.cameraNormalsTexture.IsValid())
            {
                passData.material.SetTexture(TextureNames._CameraNormalsTexture, passData.cameraNormalsTexture);
            }
        }
        
        protected void CreateMaterialIfNull(ref Material material, Shader m_Shader)
        {
            if (material) return;

            material = CoreUtils.CreateEngineMaterial(m_Shader);
            //Material cannot be serialized anyway
            material.hideFlags = HideFlags.DontSave;
            #if DEBUG
            material.name = m_Shader.name;
            #endif
        }

        /// <summary>
        /// Allocates a render texture handle for the given texture.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="texture"></param>
        public void AllocateTexture(ref RTHandle handle, Texture texture)
        {
            if (!texture) return;
            
            var allocate = handle == null || handle.rt != texture;
            if (allocate)
            {
                handle?.Release();
                handle = RTHandles.Alloc(texture);
            }
        }
        
        public TextureHandle ImportTexture(ref RTHandle handle, RenderGraph renderGraph, IRasterRenderGraphBuilder builder, Texture texture)
        {
            if (!texture) return TextureHandle.nullHandle;
            
            AllocateTexture(ref handle, texture);
            TextureHandle textureHandle = renderGraph.ImportTexture(handle);
            builder.UseTexture(textureHandle , AccessFlags.None);

            return textureHandle;
        }
        
        public void Dispose()
        {
            if(Material) CoreUtils.Destroy(Material);
            if(_BlurMaterial) CoreUtils.Destroy(_BlurMaterial);
            if(_BlitMaterial) CoreUtils.Destroy(_BlitMaterial);
            if(DepthNormalsMat) CoreUtils.Destroy(DepthNormalsMat);
            sphereMapper?.Dispose();
            
            DisposeEffect();
        }

        public virtual void DisposeEffect()
        {
            //Dispose any effect-specific resources here
        }
        
        /// <summary>
        /// Calculates a radius scale based on the current resolution height. Used to keep radius values consistent across different resolutions.
        /// </summary>
        /// <param name="resolutionHeight"></param>
        /// <returns></returns>
        public static float CalculateRadiusScale(int resolutionHeight)
        {
            return Utilities.CalculateRadiusScale(resolutionHeight, MIN_RESOLUTION_HEIGHT, MAX_RESOLUTION_HEIGHT);
        }
    }

    public static class PostEffectDebug
    {
        [NoAutoStaticsCleanup]
        public static float DebugSlider;

        public static void ToggleDebugKeyword(Material material)
        {
            CoreUtils.SetKeyword(material, ShaderKeywordStrings.DEBUG_DISPLAY, DebugSlider > 0);
        }
        
        public static void ApplySliderValue(Material material)
        {
            #if DEBUG
            ToggleDebugKeyword(material);
            material.SetFloat(ShaderParameters._PostProcessingDebugSlider, DebugSlider);
            #endif
        }
    }
}