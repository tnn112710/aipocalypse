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
    public class PixelizeRenderer : PostEffectFeature<Pixelize>
    {
        class PixelizeRenderPass : PostEffectRenderer<Pixelize>
        {
            public PixelizeRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Pixelize;
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
                internal Vector4 pixelizeParams;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                int resolution = volumeSettings.resolutionPreset.value == Pixelize.ResolutionPreset.Custom
                    ? volumeSettings.resolution.value
                    : (int)volumeSettings.resolutionPreset.value;

                Vector4 pixelizeParams = new Vector4(
                    (volumeSettings.preserveAspectRatio.value ? cameraData.camera.scaledPixelWidth : cameraData.camera.scaledPixelHeight) / (float)resolution,
                    cameraData.camera.scaledPixelHeight / (float)resolution,
                    volumeSettings.intensity.value,
                    volumeSettings.centerPixel.value ? 1f : 0f
                );

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.pixelizeParams = pixelizeParams;
                    passData.passIndex = 0;

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        data.material.SetVector("_PixelizeParams", data.pixelizeParams);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        PixelizeRenderPass renderPass;
        
        public override void Create()
        {
            renderPass = new PixelizeRenderPass(settings);
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