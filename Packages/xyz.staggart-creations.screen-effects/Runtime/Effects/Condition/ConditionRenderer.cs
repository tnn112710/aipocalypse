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
    public class ConditionRenderer : PostEffectFeature<Condition>
    {
        class ConditionRenderPass : PostEffectRenderer<Condition>
        {
            public ConditionRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Condition;
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
                internal Vector4 shadingParams;
                internal Vector4 textureParams1;
                internal Vector4 textureParams2;
                
                internal Color color;
                internal Texture overlayTexture;
            }
            
            TimeAnimation animationA = new TimeAnimation();
            TimeAnimation animationB = new TimeAnimation();

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                float speedMultiplier = volumeSettings.circular.value ? 0.33f : 1f;
                
                animationA.Update(volumeSettings.textureAnimation1.speed * speedMultiplier, volumeSettings.animationInterval.value);

                if (volumeSettings.dualLayer.value)
                {
                    animationB.Update(volumeSettings.textureAnimation2.speed * speedMultiplier, volumeSettings.animationInterval.value);
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    float intensity = volumeSettings.intensity.value;
                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.parameters = new Vector4(intensity, volumeSettings.vignetting.value, 0, volumeSettings.circular.value ? 1f : 0f);
                    passData.color = volumeSettings.color.value;
                    passData.overlayTexture = volumeSettings.overlayTex.value == null ? Texture2D.blackTexture : volumeSettings.overlayTex.value;
                    
                    passData.shadingParams = new Vector4(volumeSettings.dualLayer.value ? 1f : 0, volumeSettings.falloff.value, volumeSettings.hardFalloff.value ? 1f : 0, 0);
                    passData.textureParams1 = new Vector4(volumeSettings.textureAnimation1.value.direction.x * animationA.value, volumeSettings.textureAnimation1.value.direction.y * animationA.value, volumeSettings.textureAnimation1.value.tiling, 0f);
                    passData.textureParams2 = new Vector4(volumeSettings.textureAnimation2.direction.x * animationB.value, volumeSettings.textureAnimation2.direction.y * animationB.value, volumeSettings.textureAnimation2.tiling, 0f);
                    
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        data.material.SetVector(ShaderParameters.Params, data.parameters);
                        
                        data.material.SetVector("_ShadingParams", data.shadingParams);
                        data.material.SetVector("_Texture1Params", data.textureParams1);
                        data.material.SetVector("_Texture2Params", data.textureParams2);
                        
                        data.material.SetColor("_Color", data.color);
                        data.material.SetTexture("_Overlay", data.overlayTexture);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        ConditionRenderPass renderPass;

        public override void Create()
        {
            renderPass = new ConditionRenderPass(settings);
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