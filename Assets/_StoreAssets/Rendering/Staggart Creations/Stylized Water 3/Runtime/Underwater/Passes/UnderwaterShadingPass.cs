// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if URP
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StylizedWater3.UnderwaterRendering
{
    public class UnderwaterShadingPass : ScriptableRenderPass
    {
        /// <summary>
        /// For indirect lighting the rendering uses Unity's internal global reflection probe. For custom or third-party dynamic lighting systems you can provide a custom environment cubemap instead
        /// </summary>
        public static ReflectionProbe AmbientLightOverride;

        private Material material;
        private StylizedWaterRenderFeature.UnderwaterRenderingSettings settings;
        
        //Default to full screen
        private static Vector4 blitScale = new Vector4(1, 1, 0, 0);
        
        public void Setup(StylizedWaterRenderFeature.UnderwaterRenderingSettings m_settings, StylizedWaterRenderFeature.UnderwaterSurface surface)
        {
            this.settings = m_settings;
            this.material = surface.shadingMaterial;
        }

        private class PassData
        {
            public TextureHandle sourceColor;
            public Material material;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Water: Underwater Rendering", out var passData))
            {
                StylizedWaterRenderFeature.UnderwaterRenderingData underwaterRenderingData = frameData.Get<StylizedWaterRenderFeature.UnderwaterRenderingData>();
                
                passData.material = material;
                //passData.material.SetTexture("_UnderwaterMask", data.underwaterMask);
                passData.sourceColor = resourceData.activeColorTexture;
                
                TextureDesc cameraColorDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                TextureDesc destinationDesc = cameraColorDesc;
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
                
                //Require the opaque texture
                if (passData.material.IsKeywordEnabled(ShaderParams.Keywords.Refraction))
                {
                    ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                }
                else
                {
                    ConfigureInput(ScriptableRenderPassInput.Depth);
                }
                
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                
                builder.UseTexture(passData.sourceColor, AccessFlags.Read);
                builder.UseTexture(underwaterRenderingData.underwaterMask, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.sourceColor, blitScale, data.material, 1);
                });
                
                resourceData.cameraColor = destination;
            }
        }
        
#if !UNITY_6000_4_OR_NEWER
        #pragma warning disable CS0672
        #pragma warning disable CS0618
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) { }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
        #pragma warning restore CS0672
        #pragma warning restore CS0618
#endif
    }
}
#endif