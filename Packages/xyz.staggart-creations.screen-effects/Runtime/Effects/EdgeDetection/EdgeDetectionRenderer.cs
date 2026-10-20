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
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    public class EdgeDetectionRenderer : PostEffectFeature<EdgeDetection>
    {
        class EdgeDetectionRenderPass : PostEffectRenderer<EdgeDetection>
        {
            private EdgeDetectionSettings effectSettings;
            public EdgeDetectionRenderPass(RenderFeatureSettings settings, EdgeDetectionSettings effectSettings)
            {
                this.settings = settings;
                this.effectSettings = effectSettings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.EdgeDetection;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = true;
                requiresNormals = true;
                reconstructNormalsFromDepth = effectSettings.reconstructNormals;

                base.Setup(renderer, renderingData);

                if (!render) return;
                
                renderer.EnqueuePass(this);
            }
            
            private class PassData : BasePassData
            {
                internal Vector3 sensitivity;
                internal float debugSlider;
                internal float edgeSize;
                internal float exponent;
                internal Color edgeColor;
                internal float edgeOpacity;
                internal Vector4 fadeParams;
            }
            
            public const string _SOBEL = "_SOBEL";
            public const string _HQ_EDGE_DETECTION = "_HQ_EDGE_DETECTION";
            
            TimeAnimation noiseAnimation = new TimeAnimation();
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                CoreUtils.SetKeyword(Material, _SOBEL, volumeSettings.mode.value == EdgeDetection.Solver.Gradient);
                CoreUtils.SetKeyword(Material, _HQ_EDGE_DETECTION, effectSettings.focus == RenderFeatureSettings.Focus.BestAppearance);

                if (volumeSettings.noiseAnimationInterval.value > 0)
                {
                    noiseAnimation.Update(1f, volumeSettings.noiseAnimationInterval.value);
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.sensitivity = new Vector3(volumeSettings.depthInfluence.value, volumeSettings.normalInfluence.value, volumeSettings.colorInfluence.value);
                    passData.debugSlider = EdgeDetection.debug;
                    passData.edgeSize = volumeSettings.edgeSize.value * Utilities.CalculateRadiusScale(cameraColorDesc.height, 1080, 2160) * 0.5f;
                    passData.exponent = volumeSettings.edgeExp.value + 1f;
                    passData.edgeColor = volumeSettings.edgeColor.value;
                    passData.edgeOpacity = volumeSettings.intensity.value;
                    passData.fadeParams = new Vector4(
                        volumeSettings.startFadeDistance.value,
                        volumeSettings.endFadeDistance.value,
                        volumeSettings.invertFadeDistance.value ? 1f : 0f,
                        volumeSettings.distanceFade.value ? 1f : 0f
                    );
                    
                    Material.SetVector("_NoiseParams", new Vector4(volumeSettings.noise.value ? 1 : 0, 1f/volumeSettings.noiseOffsetScale.value,
                        volumeSettings.noiseOffsetStrength.value * 20f, Mathf.Min(0.999f, volumeSettings.noiseThicknessStrength.value)));
                    Material.SetVector("_NoiseParams2", new Vector4(volumeSettings.noiseClippingScale.value, volumeSettings.noiseClippingStrength.value));
                    Material.SetFloat("_NoiseOffset", noiseAnimation.value);
                    Material.SetTexture("_NoiseTex3D", volumeSettings.noiseTex3D.value);

                    builder.UseTexture(cameraColor, AccessFlags.Read);

                    SetupInputs(builder, passData);
                    
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        SetupMaterial(data);
                        
                        data.material.SetVector("_Influences", data.sensitivity);
                        data.material.SetFloat("_DebugSlider", data.debugSlider);
                        data.material.SetFloat("_EdgeSize", data.edgeSize);
                        data.material.SetFloat("_Exponent", data.exponent);
                        data.material.SetColor("_EdgeColor", data.edgeColor);
                        data.material.SetFloat("_EdgeOpacity", data.edgeOpacity);
                        data.material.SetVector(ShaderParameters.FadeParams, data.fadeParams);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        EdgeDetectionRenderPass renderPass;

        [Serializable]
        public class EdgeDetectionSettings
        {
            public RenderFeatureSettings.Focus focus = RenderFeatureSettings.Focus.BestAppearance;
            
            [FormerlySerializedAs("reconstructDepthNormals")]
            [Header("Effect specific")]
            [Tooltip("Reconstruct the scene geometry's normals from the depth texture." +
                     "\n\nDisabling this will have the effect use the Depth-Normals prepass, which is more accurate. This will have all objects re-render, if the scene isn't already optimized for draw calls, this will negatively affect performance")]
            public bool reconstructNormals;
            [Tooltip("Executes the effect before transparent materials are rendered.")]
            public bool skipTransparents;
        }
        
        public EdgeDetectionSettings effectSettings = new EdgeDetectionSettings();

        public override void Create()
        {
            renderPass = new EdgeDetectionRenderPass(settings, effectSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (GetBlendedVolumeSettings() && volumeSettings.IsActive())
            {
                renderPass.volumeSettings = volumeSettings;
                renderPass.renderPassEvent = effectSettings.skipTransparents ? RenderPassEvent.BeforeRenderingTransparents : settings.GetInjectionPoint();

                renderPass.reconstructNormalsFromDepth = effectSettings.reconstructNormals;
                renderPass.Setup(renderer, renderingData);
            }
        }

        protected override void Dispose(bool disposing)
        {
            renderPass?.Dispose();
        }
    }
}