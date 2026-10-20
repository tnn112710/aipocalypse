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
    public class HueShift3DRenderer : PostEffectFeature<HueShift3D>
    {
        class HueShift3DRenderPass : PostEffectRenderer<HueShift3D>
        {
            private HueShift3DSettings effectSettings;
            
            enum Pass
            {
                ColorSpectrum,
                GradientTexture
            }

            public HueShift3DRenderPass(RenderFeatureSettings settings, HueShift3DSettings effectSettings)
            {
                this.settings = settings;
                this.effectSettings = effectSettings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.HueShift3D;
                ProfilerTag = GetProfilerTag();
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;

                requiresNormals = volumeSettings.geoInfluence.value > 0f;
                
                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Vector4 parameters;
                internal Texture gradientTexture;
                internal int passIndex;
            }
            
            TimeAnimation noiseAnimation = new TimeAnimation();

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                int passIndex = volumeSettings.colorSource.value == HueShift3D.ColorSource.RGBSpectrum
                    ? (int)Pass.ColorSpectrum
                    : (int)Pass.GradientTexture;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    noiseAnimation.Update(volumeSettings.speed.value);
                    
                    passData.source = cameraColor;
                    passData.material = Material;
                    passData.parameters = new Vector4(
                        noiseAnimation.value,
                        volumeSettings.size.value,
                        volumeSettings.geoInfluence.value,
                        volumeSettings.intensity.value
                    );
                    passData.gradientTexture = volumeSettings.gradientTex.value;
                    passData.passIndex = passIndex;

                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    
                    SetupInputs(builder, passData);

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        SetupMaterial(data);
                        
                        data.material.SetVector("_Params", data.parameters);
                        if (data.gradientTexture) data.material.SetTexture("_GradientTex", data.gradientTexture);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        HueShift3DRenderPass renderPass;

        [Serializable]
        public class HueShift3DSettings
        {
            [Tooltip("Reconstruct the scene geometry's normals from the depth texture." +
                     "\n\nDisabling this will have the effect use the Depth-Normals prepass, which is more accurate. This will have all object re-render, if the scene isn't already optimized for draw calls, this will negatively affect performance")]
            public bool reconstructDepthNormals = false;
        }
        
        public HueShift3DSettings effectSettings = new HueShift3DSettings();

        public override void Create()
        {
            renderPass = new HueShift3DRenderPass(settings, effectSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (GetBlendedVolumeSettings() && volumeSettings.IsActive())
            {
                renderPass.volumeSettings = volumeSettings;

                renderPass.reconstructNormalsFromDepth = effectSettings.reconstructDepthNormals;
                renderPass.Setup(renderer, renderingData);
            }
        }

        protected override void Dispose(bool disposing)
        {
            renderPass?.Dispose();
        }
    }
}