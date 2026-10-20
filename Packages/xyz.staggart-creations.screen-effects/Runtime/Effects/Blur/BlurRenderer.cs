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

using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace sc.screeneffects.runtime
{
    public class BlurRenderer : PostEffectFeature<Blur>
    {
        class BlurRenderPass : PostEffectRenderer<Blur>
        {
            public BlurRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Blur;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = volumeSettings.distanceFade.value;
                
                base.Setup(renderer, renderingData);

                if (!render) return;

                renderer.EnqueuePass(this);
            }

            private class BlurPassData : BasePassData
            {
                public Vector4 fadeParams;
                public int blurPass;
                public Vector4 blurOffset;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData, true, true);
                
                var desc = new TextureDesc(cameraColorDesc);
                desc.format = cameraData.cameraTargetDescriptor.graphicsFormat;

                // Create temporary blur buffers
                int downscale = Mathf.Max(1, volumeSettings.downscaling.value);
                desc.width /= downscale;
                desc.height /= downscale;
                desc.filterMode = FilterMode.Bilinear;
                
                #if DEBUG
                desc.name = "BlurBuffer1";
                #endif
                var blurBuffer1 = renderGraph.CreateTexture(desc);
                
                #if DEBUG
                desc.name = "BlurBuffer2";
                #endif
                var blurBuffer2 = renderGraph.CreateTexture(desc);

                BlitCopy(renderGraph, cameraColor, blurBuffer1);
                
                int resolutionHeight = desc.height;
                BlurPass(renderGraph, resolutionHeight, blurBuffer1, blurBuffer2, volumeSettings.radius.value * volumeSettings.intensity.value, volumeSettings.iterations.value, volumeSettings.mode == Blur.BlurMethod.Gaussian);
                
                //Composite blurred image with original based on scene depth
                if (volumeSettings.distanceFade.value)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>("Blur Depth Blend", out var passData))
                    {
                        passData.source = cameraColor;
                        passData.destination = destination;
                        passData.material = Material;
                        passData.fadeParams = new Vector4(volumeSettings.startFadeDistance.value, volumeSettings.endFadeDistance.value, 0, 1);

                        builder.AllowPassCulling(false);
                        builder.SetRenderAttachment(passData.destination, 0);
                        builder.UseTexture(cameraColor, AccessFlags.Read);
                        builder.UseTexture(blurBuffer1, AccessFlags.Read);
                        builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);

                        builder.SetRenderFunc((BlurPassData data, RasterGraphContext context) =>
                        {
                            data.material.SetTexture(TextureNames.Blurred, blurBuffer1);
                            data.material.SetVector(ShaderParameters.FadeParams, data.fadeParams);

                            Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 1);
                        });
                    }
                }
                else
                {
                    Blit(renderGraph, blurBuffer1, destination, Material, 0);
                }
                
                resourceData.cameraColor = destination;
            }
        }

        BlurRenderPass renderPass;

        public override void Create()
        {
            //Disable for scene view
            settings.cameraTypes &= ~RenderFeatureSettings.CameraTypeFlags.SceneView;
            
            renderPass = new BlurRenderPass(settings);
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