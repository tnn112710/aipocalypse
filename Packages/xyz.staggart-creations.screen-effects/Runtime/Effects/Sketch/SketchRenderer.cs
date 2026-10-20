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
    public class SketchRenderer : PostEffectFeature<Sketch>
    {
        class SketchRenderPass : PostEffectRenderer<Sketch>
        {
            public SketchRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Sketch;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = volumeSettings.projectionMode == Sketch.SketchProjectionMode.WorldSpace;
                
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Texture strokes;
                internal Vector4 parameters;
                internal float contrast;
                internal Vector2 thresholds;
                internal int passIndex;
            }
            
            TimeAnimation animation = new TimeAnimation();

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                if(volumeSettings.animationInterval.value > 0) animation.Update(1f, volumeSettings.animationInterval.value);
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.strokes = volumeSettings.strokeTex.value;
                    passData.parameters = new Vector4(
                        volumeSettings.intensity.value,
                        animation.value,
                        (int)volumeSettings.blendMode.value,
                        ((int)volumeSettings.projectionMode.value == 1) ? volumeSettings.tiling.value * 0.1f : volumeSettings.tiling.value
                    );
                    passData.contrast = volumeSettings.contrast.value;
                    passData.thresholds = new Vector2(volumeSettings.darkThreshold.value, volumeSettings.lightThreshold.value);
                    passData.passIndex = (int)volumeSettings.projectionMode.value;

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    if (requiresDepth)
                    {
                        builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        if (data.strokes) data.material.SetTexture("_Strokes", data.strokes);
                        data.material.SetVector("_Params", data.parameters);
                        data.material.SetFloat("_Contrast", data.contrast);
                        data.material.SetVector("_Thresholds", data.thresholds);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        SketchRenderPass renderPass;
        
        public override void Create()
        {
            renderPass = new SketchRenderPass(settings);
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