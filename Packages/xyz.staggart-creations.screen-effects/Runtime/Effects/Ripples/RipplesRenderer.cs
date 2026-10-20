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
    public class RipplesRenderer : PostEffectFeature<Ripples>
    {
        class RipplesRenderPass : PostEffectRenderer<Ripples>
        {
            public RipplesRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Ripples;
                ProfilerTag = GetProfilerTag();
            }

            private TimeAnimation animation = new TimeAnimation();
            
            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;

                if (volumeSettings.mode.value == Ripples.RipplesMode.DistortionTexture &&
                    volumeSettings.textureProjection.value == PostProcessingComponent.TextureProjection.Spherical)
                {
                    InitializeSphereMapper();
                }

                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Texture texture;
                public bool useSphereMapping;
                internal float strength;
                internal float distance;
                internal float offset;
                internal Vector2 size;
                internal int passIndex;
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                animation.Update(volumeSettings.speed.value);

                var sphericalMapping = volumeSettings.mode.value == Ripples.RipplesMode.DistortionTexture && volumeSettings.textureProjection.value == PostProcessingComponent.TextureProjection.Spherical;
                
                TextureHandle sphereMapperOutput = default;
                if (sphericalMapping)
                {
                    sphereMapper.SetProperties(cameraData.worldSpaceCameraPos, volumeSettings.distortionMap.value, volumeSettings.speed.value, volumeSettings.distance.value * 0.2f);
                    
                    TextureDesc desc = cameraColorDesc;
                    desc.name = "Ripples-SphericalDistortion";
                    sphereMapper.Render(renderGraph, desc, 4);
                    
                    sphereMapperOutput = sphereMapper.Output;
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.texture = volumeSettings.distortionMap.value;
                    passData.strength = volumeSettings.strength.value * volumeSettings.intensity.value * 0.1f;
                    passData.distance = volumeSettings.distance.value * 0.1f;
                    passData.offset = animation.value;
                    
                    passData.size = new Vector2(volumeSettings.width.value, volumeSettings.height.value);
                    passData.passIndex = (int)volumeSettings.mode.value;

                    passData.useSphereMapping = sphericalMapping;
                    if (passData.useSphereMapping && sphereMapperOutput.IsValid())
                    {
                        builder.UseTexture(sphereMapperOutput, AccessFlags.Read);
                        
                        passData.distance = 1;
                        passData.offset = 0;
                        passData.size = Vector2.one;
                    }
                    
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        
                        Texture textureToUse = data.useSphereMapping ? (Texture)sphereMapperOutput : data.texture;
                        data.material.SetTexture("_RipplesTexture", textureToUse);
                        
                        data.material.SetFloat("_Strength", data.strength);
                        data.material.SetFloat("_Distance", data.distance);
                        data.material.SetFloat("_TimeOffset", data.offset);
                        data.material.SetVector("_Size", data.size);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        RipplesRenderPass renderPass;

        public override void Create()
        {
            renderPass = new RipplesRenderPass(settings);
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