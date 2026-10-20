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
    public class TiltShiftRenderer : PostEffectFeature<TiltShift>
    {
        class TiltShiftRenderPass : PostEffectRenderer<TiltShift>
        {
            private TiltShiftSettings effectSettings;
            LocalKeyword _HIGH_QUALITY;
            
            public TiltShiftRenderPass(RenderFeatureSettings settings, TiltShiftSettings effectSettings)
            {
                this.settings = settings;
                this.effectSettings = effectSettings;
                
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.TiltShift;
                ProfilerTag = GetProfilerTag();
            }
            
            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                _HIGH_QUALITY = new LocalKeyword(Shader, "_HIGH_QUALITY");
                
                renderer.EnqueuePass(this);
            }

            private class PassData : BasePassData
            {
                internal Vector4 parameters;
                internal float offset;
                internal float angle;
                internal int passIndex;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                int pass = TiltShift.debug ? 1 : 0;
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);
                    
                    //Setup pass data
                    passData.source = cameraColor;
                    passData.material = Material;

                    float blurRadius = CalculateRadiusScale(cameraColorDesc.height) * 10f;
                    passData.parameters = new Vector4(volumeSettings.areaSize.value, volumeSettings.areaFalloff.value, volumeSettings.intensity.value * blurRadius, (int)volumeSettings.mode.value);
                    passData.offset = volumeSettings.offset.value;
                    passData.angle = volumeSettings.angle.value;
                    passData.passIndex = pass;
                    
                    passData.material.SetKeyword(_HIGH_QUALITY, effectSettings.focus == RenderFeatureSettings.Focus.BestAppearance);
                    
                    //Input & output
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    
                    SetupRenderingLayers(builder, passData);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        //Base properties
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);
                        
                        //Set material properties
                        data.material.SetVector(ShaderParameters.Params, data.parameters);
                        data.material.SetFloat("_Offset", data.offset);
                        data.material.SetFloat("_Angle", data.angle);
                        
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, data.passIndex);
                    });
                    
                    //Next pass can use the modified copy as the source again
                    resourceData.cameraColor = destination;
                }
            }
        }

        TiltShiftRenderPass renderPass;
        
        [Serializable]
        public class TiltShiftSettings
        {
            public RenderFeatureSettings.Focus focus = RenderFeatureSettings.Focus.BestAppearance;
        }
        
        public TiltShiftSettings effectSettings = new TiltShiftSettings();
        
        public override void Create()
        {
            renderPass = new TiltShiftRenderPass(settings, effectSettings);
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