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
    public class DitheringRenderer : PostEffectFeature<Dithering>
    {
        class DitheringRenderPass : PostEffectRenderer<Dithering>
        {
            public DitheringRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Dithering;
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
                internal Vector4 parameters;
                internal Vector4 ditheringCoords;
                public bool useSphereMapping;
                
                internal Texture lutTexture;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                Texture lutTexture = volumeSettings.lut.value == null ? Texture2D.blackTexture : volumeSettings.lut.value;
                
                Vector4 ditherParams = new Vector4((float)lutTexture.width, volumeSettings.tiling.value * 4f, Utilities.CalculateLuminanceThreshold(volumeSettings.luminanceThreshold.value) * volumeSettings.intensity.value, volumeSettings.intensity.value);

                var sphericalMapping = volumeSettings.textureProjection.value == PostProcessingComponent.TextureProjection.Spherical;
                
                TextureHandle sphereMapperOutput = default;
                if (sphericalMapping)
                {
                    sphereMapper.SetProperties(cameraData.worldSpaceCameraPos, volumeSettings.lut.value, 0, volumeSettings.tiling.value * 16f);
                    sphereMapper.pointFiltering = true;
                    sphereMapper.worldSpace = true;
                    
                    TextureDesc desc = cameraColorDesc;
                    desc.name = "Dithering-SphericalLUT";
                    sphereMapper.Render(renderGraph, desc);
                    
                    sphereMapperOutput = sphereMapper.Output;
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    //Setup pass data
                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.lutTexture = lutTexture;
                    passData.parameters = new Vector4(volumeSettings.intensity.value, (volumeSettings.contrast.value * 0.5f) + 1f, 0, 0);
                    passData.passIndex = 0;
                    
                    passData.useSphereMapping = sphericalMapping;
                    if (passData.useSphereMapping && sphereMapperOutput.IsValid())
                    {
                        builder.UseTexture(sphereMapperOutput, AccessFlags.Read);

                        ditherParams.x = 1;
                        ditherParams.y = 1;
                    }
                    
                    passData.ditheringCoords = ditherParams;

                    //Input & output
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        // Base properties
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        
                        Texture textureToUse = data.useSphereMapping ? (Texture)sphereMapperOutput : data.lutTexture;
                        data.material.SetTexture("_LUT", textureToUse);
                        data.material.SetVector(ShaderParameters.Params, data.parameters);
                        data.material.SetVector("_Dithering_Coords", data.ditheringCoords);
                        data.material.SetInt("_ProjectionMode", data.useSphereMapping ? 1 : 0);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                    
                    resourceData.cameraColor = destination;
                }
            }
        }

        DitheringRenderPass renderPass;

        public override void Create()
        {
            renderPass = new DitheringRenderPass(settings);
            renderPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
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