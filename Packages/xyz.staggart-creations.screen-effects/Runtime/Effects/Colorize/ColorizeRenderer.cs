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
    public class ColorizeRenderer : PostEffectFeature<Colorize>
    {
        class ColorizeRenderPass : PostEffectRenderer<Colorize>
        {
            public ColorizeRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Colorize;
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
                internal TextureHandle colorRamp;
                internal float intensity;
                internal float blendMode;
                internal Vector4 parameters;
            }

            private RTHandle gradientTextureHandle;
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    
                    Texture gradientTex = volumeSettings.source.value == Colorize.Source.Gradient
                        ? volumeSettings.gradient.value.GetTexture()
                        : volumeSettings.gradientTexture.value;
                    
                    passData.colorRamp = ImportTexture(ref gradientTextureHandle, renderGraph, builder, gradientTex);
                    passData.intensity = volumeSettings.intensity.value;
                    passData.blendMode = (int)volumeSettings.blendMode.value;
                    passData.parameters = new Vector4(volumeSettings.intensity.value, volumeSettings.range.value.x, base.volumeSettings.range.value.y, (int)volumeSettings.blendMode.value);
                    
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        data.material.SetTexture("_ColorRamp", data.colorRamp);
                        data.material.SetVector(ShaderParameters.Params, data.parameters);
                        data.material.SetFloat("_Intensity", data.intensity);
                        data.material.SetFloat("_BlendMode", data.blendMode);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        ColorizeRenderPass renderPass;

        public override void Create()
        {
            renderPass = new ColorizeRenderPass(settings);
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