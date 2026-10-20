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
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    public sealed class CurvatureHighlightRenderer : PostEffectFeature<CurvatureHighlight>
    {
        class CurvatureHighlightPass : PostEffectRenderer<CurvatureHighlight>
        {
            private EffectSettings effectSettings;
            public CurvatureHighlightPass(RenderFeatureSettings settings, EffectSettings effectSettings)
            {
                this.settings = settings;
                this.effectSettings = effectSettings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.CurvatureHighlight;
                ProfilerTag = GetProfilerTag();

                reconstructNormalsFromDepth = effectSettings.reconstructNormals;
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresNormals = true;
                reconstructNormalsFromDepth = effectSettings.reconstructNormals;
                
                base.Setup(renderer, renderingData);

                if (!render) return;

                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Texture colorRamp;
                internal float threshold;
                internal float softness;
                internal float convexity;
                internal float concavity;
                internal float radius;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.cameraNormalsTexture = cameraNormalsTexture;

                    passData.threshold = volumeSettings.threshold.value;
                    passData.softness = Mathf.Max(0.001f, volumeSettings.softness.value);
                    passData.convexity = volumeSettings.convexity.value * volumeSettings.intensity.value;
                    passData.concavity = volumeSettings.concavity.value * volumeSettings.intensity.value;
                    passData.radius = Mathf.Max(1, volumeSettings.radius.value);

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    SetupInputs(builder, passData);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        SetupMaterial(data);

                        data.material.SetFloat("_Threshold", data.threshold);
                        data.material.SetFloat("_Softness", data.softness);
                        data.material.SetFloat("_Convexity", data.convexity);
                        data.material.SetFloat("_Convavity", data.concavity);
                        data.material.SetFloat("_Radius", data.radius);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        private CurvatureHighlightPass renderPass;

        [Serializable]
        public class EffectSettings
        {
            public bool reconstructNormals = false;
        }

        [SerializeField] public EffectSettings effectSettings = new EffectSettings();

        public override void Create()
        {
            renderPass = new CurvatureHighlightPass(settings, effectSettings);
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