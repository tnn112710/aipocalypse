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
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace sc.screeneffects.runtime
{
    public class KuwaharaRenderer : PostEffectFeature<Kuwahara>
    {
        class KuwaharaRenderPass : PostEffectRenderer<Kuwahara>
        {
            public KuwaharaRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Kuwahara;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = volumeSettings.distanceFade.value;
                
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal float radius;
                internal Vector4 fadeParams;
                internal bool useDepthFade;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                bool useDepthFade = volumeSettings.distanceFade.value;
                int passIndex = useDepthFade ? 1 : 0;

                TextureHandle source = cameraColor;
                //Create a downscaled copy to work with
                if (volumeSettings.resolution.value != PostProcessingComponent.Resolution.Full)
                {
                    TextureDesc inputDesc = cameraColorDesc;
                    inputDesc.width /= (int)volumeSettings.resolution.value;
                    inputDesc.height /= (int)volumeSettings.resolution.value;
                    source = renderGraph.CreateTexture(inputDesc);
                    
                    BlitCopy(renderGraph,cameraColor, source);
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = source;
                    passData.material = Material;
                    passData.radius = Mathf.Min(8, volumeSettings.radius.value * volumeSettings.intensity.value);
                    passData.fadeParams = new Vector4(volumeSettings.startFadeDistance.value, volumeSettings.endFadeDistance.value, 0f, 0f);
                    passData.useDepthFade = useDepthFade;
                    passData.passIndex = passIndex;

                    builder.UseTexture(source, AccessFlags.Read);
                    if (useDepthFade)
                    {
                        builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        data.material.SetFloat("_Radius", data.radius);
                        if (data.useDepthFade) data.material.SetVector("_FadeParams", data.fadeParams);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        KuwaharaRenderPass renderPass;

        public override void Create()
        {
            renderPass = new KuwaharaRenderPass(settings);
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