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
    public class OverlayRenderer : PostEffectFeature<Overlay>
    {
        class OverlayRenderPass : PostEffectRenderer<Overlay>
        {
            private RTHandle overlayTextureHandle;
            
            public OverlayRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Overlay;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                if (volumeSettings.textureProjection.value == PostProcessingComponent.TextureProjection.Spherical)
                {
                    InitializeSphereMapper();
                }

                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal TextureHandle overlayTexture;
                public bool useSphereMapping;
                
                internal Vector4 parameters;
                internal float luminanceThreshold;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                var sphericalMapping = volumeSettings.textureProjection.value == PostProcessingComponent.TextureProjection.Spherical;

                TextureHandle sphereMapperOutput = default;
                if (sphericalMapping)
                {
                    sphereMapper.SetProperties(cameraData.worldSpaceCameraPos, volumeSettings.overlayTex.value, 0, 1f + (volumeSettings.tiling.value * 3f));
                    
                    TextureDesc desc = cameraColorDesc;
                    desc.name = "Overlay-SphericalDistortion";
                    sphereMapper.Render(renderGraph, desc, 1);
                    
                    sphereMapperOutput = sphereMapper.Output;
                }
                
                float luminanceThreshold = Utilities.CalculateLuminanceThreshold(volumeSettings.luminanceThreshold.value);

                Vector4 parameters = new Vector4(
                    volumeSettings.intensity.value,
                    Mathf.Pow(volumeSettings.tiling.value + 1f, 2f),
                    volumeSettings.autoAspect.value ? 1f : 0f,
                    (int)volumeSettings.blendMode.value
                );

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    
                    passData.parameters = parameters;
                    passData.luminanceThreshold = luminanceThreshold;
                    
                    passData.useSphereMapping = sphericalMapping;
                    if (passData.useSphereMapping && sphereMapperOutput.IsValid())
                    {
                        builder.UseTexture(sphereMapperOutput, AccessFlags.Read);

                        passData.parameters.y = 1;
                        passData.parameters.z = 0;

                        passData.overlayTexture = sphereMapperOutput;
                    }
                    else
                    {
                        Texture overlayTexture = volumeSettings.overlayTex.value;
                        
                        if (overlayTextureHandle == null || overlayTextureHandle.rt != overlayTexture)
                        {
                            overlayTextureHandle?.Release();
                            overlayTextureHandle = RTHandles.Alloc(overlayTexture);
                        }
                        
                        TextureHandle importedOverlayTexture = renderGraph.ImportTexture(overlayTextureHandle);
                        passData.overlayTexture = importedOverlayTexture;
                        builder.UseTexture(importedOverlayTexture, AccessFlags.Read);
                    }

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        
                        data.material.SetTexture("_OverlayTex", data.overlayTexture);
                        data.material.SetVector("_Params", data.parameters);
                        data.material.SetFloat("_LuminanceThreshold", data.luminanceThreshold);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }

            public override void DisposeEffect()
            {
                overlayTextureHandle?.Release();
            }
        }

        OverlayRenderPass renderPass;

        public override void Create()
        {
            renderPass = new OverlayRenderPass(settings);
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