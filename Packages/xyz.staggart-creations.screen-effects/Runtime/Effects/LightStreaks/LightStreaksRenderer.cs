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
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    public class LightStreaksRenderer : PostEffectFeature<LightStreaks>
    {
        class LightStreaksRenderPass : PostEffectRenderer<LightStreaks>
        {
            private RenderFeatureSettings.Focus renderingFocus;
            private readonly int emissionTexID = Shader.PropertyToID("_BloomTex");

            enum Pass
            {
                LuminanceDiff,
                BlurFast,
                Blur,
                Blend,
                Debug
            }

            public LightStreaksRenderPass(RenderFeatureSettings settings, LightStreaksSettings effectSettings)
            {
                this.settings = settings;
                renderingFocus = effectSettings.focus;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.LightStreaks;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;

                renderer.EnqueuePass(this);
            }

            private class LightStreaksPassData : BasePassData
            {
                public LightStreaks volumeSettings;

                public int passIndex;
                public Vector4 blurOffsets;
                public float luminanceThreshold;
                public float brightness;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                var desc = new TextureDesc(cameraColorDesc);

                // Create temporary textures
                desc.name = "LightStreaksEmissionTex";
                var emissionTex = renderGraph.CreateTexture(desc);

                // Create downscaled blur buffers
                int downscale = Mathf.Max(1, volumeSettings.downscaling.value);
                desc.width /= downscale;
                desc.height /= downscale;
                desc.name = "LightStreaksBlurBuffer1";
                var blurBuffer1 = renderGraph.CreateTexture(desc);
                desc.name = "LightStreaksBlurBuffer2";
                var blurBuffer2 = renderGraph.CreateTexture(desc);

                // Luminance difference extraction pass
                using (var builder = renderGraph.AddRasterRenderPass<LightStreaksPassData>("Light Streaks: Mask", out var passData))
                {
                    passData.source = cameraColor;
                    passData.destination = emissionTex;
                    passData.material = Material;
                    passData.passIndex = (int)Pass.LuminanceDiff;
                    passData.luminanceThreshold = Mathf.GammaToLinearSpace(volumeSettings.luminanceThreshold.value);
                    passData.brightness = volumeSettings.brightness.value;
                    passData.volumeSettings = volumeSettings;
                    
                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(emissionTex, 0);
                    builder.UseTexture(cameraColor, AccessFlags.Read);

                    builder.SetRenderFunc((LightStreaksPassData data, RasterGraphContext context) =>
                    {
                        data.material.SetVector(ShaderParameters.Params, new Vector4(data.luminanceThreshold, data.volumeSettings.intensity.value, data.brightness, 0f));
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                }

                // Copy emission to first blur buffer
                using (var builder = renderGraph.AddRasterRenderPass<LightStreaksPassData>("Light Streaks Copy", out var passData))
                {
                    passData.source = emissionTex;
                    passData.destination = blurBuffer1;
                    passData.material = Material;
                    passData.passIndex = (int)Pass.Blend;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(blurBuffer1, 0);
                    builder.UseTexture(emissionTex, AccessFlags.Read);

                    builder.SetRenderFunc((LightStreaksPassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                }

                float length = volumeSettings.length.value;
                float sampleDistance = volumeSettings.sampleDistance.value;
                
                if ((renderingFocus == RenderFeatureSettings.Focus.BestPerformance))
                {
                    length = Mathf.Max(length, 2f);
                }
                
                BlurDirectionalPass(renderGraph, desc.width, blurBuffer1, blurBuffer2, BlurDirection.Horizontal, sampleDistance, length, 
                    renderingFocus == RenderFeatureSettings.Focus.BestAppearance, volumeSettings.angle.value);
                //BlurDirectionalPass(renderGraph, desc.height, blurBuffer2, blurBuffer1, BlurDirection.Horizontal, length, iterations, volumeSettings.quality.value == PostProcessingComponent.Focus.BestAppearance);
                
                /*
                // Blur iterations
                for (int i = 0; i < iterations; i++)
                {
                    // Horizontal blur
                    using (var builder = renderGraph.AddRasterRenderPass<LightStreaksPassData>($"Light Streaks Blur H {i}", out var passData))
                    {
                        passData.source = blurBuffer1;
                        passData.destination = blurBuffer2;
                        passData.material = Material;
                        passData.volumeSettings = volumeSettings;
                        passData.passIndex = blurMode;
                        passData.blurOffsets = new Vector4(
                            rw / pixelWidth,
                            rh / pixelHeight,
                            0, 0);

                        builder.AllowPassCulling(false);
                        builder.SetRenderAttachment(blurBuffer2, 0);
                        builder.UseTexture(blurBuffer1, AccessFlags.Read);

                        builder.SetRenderFunc((LightStreaksPassData data, RasterGraphContext context) =>
                        {
                            data.material.SetVector(ShaderParameters.BlurOffsets, data.blurOffsets);
                            Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                        });
                    }

                    // Vertical blur
                    using (var builder = renderGraph.AddRasterRenderPass<LightStreaksPassData>($"Light Streaks Blur V {i}", out var passData))
                    {
                        passData.source = blurBuffer2;
                        passData.destination = blurBuffer1;
                        passData.material = Material;
                        passData.volumeSettings = volumeSettings;
                        passData.passIndex = blurMode;
                        passData.blurOffsets = new Vector4(
                            (rw) * 2f / pixelWidth,
                            rh * 2f / pixelHeight,
                            0, 0);

                        builder.AllowPassCulling(false);
                        builder.SetRenderAttachment(blurBuffer1, 0);
                        builder.UseTexture(blurBuffer2, AccessFlags.Read);

                        builder.SetRenderFunc((LightStreaksPassData data, RasterGraphContext context) =>
                        {
                            data.material.SetVector(ShaderParameters.BlurOffsets, data.blurOffsets);
                            Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                        });
                    }
                }
                */

                // Final blend pass
                using (var builder = renderGraph.AddRasterRenderPass<LightStreaksPassData>("Light Streaks: Blend", out var passData))
                {
                    passData.source = cameraColor;
                    passData.destination = destination;
                    passData.material = Material;
                    passData.volumeSettings = volumeSettings;
                    passData.brightness = volumeSettings.brightness.value;
                    passData.passIndex = volumeSettings.debug.value ? (int)Pass.Debug : (int)Pass.Blend;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(destination, 0);
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.UseTexture(blurBuffer1, AccessFlags.Read);

                    builder.SetRenderFunc((LightStreaksPassData data, RasterGraphContext context) =>
                    {
                        data.material.SetTexture(emissionTexID, blurBuffer1);
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                }

                resourceData.cameraColor = destination;
            }
        }

        LightStreaksRenderPass renderPass;
        
        [Serializable]
        public class LightStreaksSettings
        {
            public RenderFeatureSettings.Focus focus = RenderFeatureSettings.Focus.BestAppearance;
        }
        
        public LightStreaksSettings effectSettings = new LightStreaksSettings();
        
        public override void Create()
        {
            renderPass = new LightStreaksRenderPass(settings, effectSettings);
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