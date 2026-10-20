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
    public class LUTRenderer : PostEffectFeature<LUT>
    {
        class LUTRenderPass : PostEffectRenderer<LUT>
        {
            public LUTRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.LUT;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = volumeSettings.mode == LUT.Mode.DistanceBased;
                
                base.Setup(renderer, renderingData);

                if (!render || LUT.Bypass) return;
                
                renderer.EnqueuePass(this);
            }
            
            private class PassData : BasePassData
            {
                internal Vector4 lutParams;
                internal Vector4 fadeParams;
                internal Vector4 vibranceParams;
                internal Texture lutNear;
                internal Texture lutFar;
                internal bool useDistanceBlend;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (LUT.Bypass) return;

                ConfigurePass(renderGraph, frameData);

                bool useDistanceBlend = volumeSettings.mode == LUT.Mode.DistanceBased;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.lutParams = new Vector4(volumeSettings.lutNear.value ? volumeSettings.intensity.value : 0f, volumeSettings.invert.value, 0f, 0f);
                    passData.fadeParams = new Vector4(volumeSettings.startFadeDistance.value, volumeSettings.endFadeDistance.value, 0f, 0f);
                    passData.vibranceParams = new Vector4(
                        volumeSettings.vibranceRGBBalance.value.r,
                        volumeSettings.vibranceRGBBalance.value.g,
                        volumeSettings.vibranceRGBBalance.value.b,
                        volumeSettings.vibrance.value
                    );
                    passData.lutNear = volumeSettings.lutNear.value;
                    passData.lutFar = volumeSettings.lutFar.value;
                    passData.useDistanceBlend = useDistanceBlend;
                    passData.passIndex = (int)volumeSettings.mode.value;

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    if (useDistanceBlend)
                    {
                        builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        data.material.SetVector("_LUT_Params", data.lutParams);
                        data.material.SetVector(ShaderParameters.Params, data.vibranceParams);

                        if (data.lutNear) data.material.SetTexture("_LUT_Near", data.lutNear);
                        if (data.useDistanceBlend)
                        {
                            data.material.SetVector("_FadeParams", data.fadeParams);
                            if (data.lutFar) data.material.SetTexture("_LUT_Far", data.lutFar);
                        }

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        LUTRenderPass renderPass;

        public override void Create()
        {
            renderPass = new LUTRenderPass(settings);
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