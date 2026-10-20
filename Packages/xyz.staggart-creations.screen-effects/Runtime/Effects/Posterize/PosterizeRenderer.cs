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
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace sc.screeneffects.runtime
{
    public class PosterizeRenderer : PostEffectFeature<Posterize>
    {
        class PosterizeRenderPass : PostEffectRenderer<Posterize>
        {
            public PosterizeRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Posterize;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Vector4 parameters;
                internal float intensity;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                int pass = volumeSettings.hsvMode.value ? 1 : 0;
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);
                    
                    //Setup pass data
                    passData.source = cameraColor;
                    passData.material = Material;

                    passData.parameters = new Vector4(volumeSettings.hue.value, volumeSettings.saturation.value, volumeSettings.value.value, volumeSettings.levels.value);
                    passData.intensity = volumeSettings.intensity.value;
                    passData.passIndex = pass;
                    
                    //Input & output
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    
                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        //Base properties
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        
                        //Set material properties
                        data.material.SetVector(ShaderParameters.Params, data.parameters);
                        data.material.SetFloat(ShaderParameters.Intensity, data.intensity);
                        
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                    
                    //Next pass can use the modified copy as the source again
                    resourceData.cameraColor = destination;
                }
            }
        }

        PosterizeRenderPass renderPass;

        public override void Create()
        {
            renderPass = new PosterizeRenderPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (GetBlendedVolumeSettings() && volumeSettings.IsActive())
            {
                renderPass.volumeSettings = volumeSettings;
                
                renderPass.Setup(renderer, renderingData);
            }
        }

        protected override void Dispose(bool disposing)
        {
            renderPass?.Dispose();
        }
    }
}