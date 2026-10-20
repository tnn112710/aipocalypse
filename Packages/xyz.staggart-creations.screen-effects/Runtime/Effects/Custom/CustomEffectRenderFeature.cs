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
    public class CustomEffectRenderFeature : ScriptableRendererFeature
    {
        public class CustomEffectPass : PostEffectRenderer<VolumeComponent>
        {
            public CustomEffectPass(RenderFeatureSettings settings, CustomEffectSettings effectSettings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                //Fake, but ensure error free operation
                shaderName = "Legacy Shaders/Diffuse";
                ProfilerTag = "Custom Post Processing";
                
                //Assign the custom material
                Material = effectSettings.material;
            }
            
            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;
                
                if (!Utilities.ShouldRenderForCamera(settings, camera, camera.cameraType, renderingData.cameraData.renderType)) return;
                if (!Material) return;
                
                renderer.EnqueuePass(this);
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                using (var builder = renderGraph.AddRasterRenderPass<BasePassData>(ProfilerTag, out var passData))
                {
                    builder.AllowPassCulling(true);

                    passData.source = cameraColor;
                    passData.material = Material;
                    
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    
                    SetupRenderingLayers(builder, passData);
                    
                    builder.SetRenderFunc((BasePassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);

                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, 0);
                    });

                    resourceData.cameraColor = destination;
                }
            }
        }

        [Serializable]
        public class CustomEffectSettings
        {
            public Material material;
            [Tooltip("Executes the effect before transparent materials are rendered.")]
            public bool skipTransparents;
        }

        CustomEffectPass renderPass;
        [SerializeField]
        public RenderFeatureSettings settings = new RenderFeatureSettings();
        public CustomEffectSettings effectSettings = new CustomEffectSettings();
        
        public override void Create()
        {
            #if UNITY_EDITOR
            if (effectSettings.material == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("bc3f65b9e6680704d88449fd48584032");
                Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                
                effectSettings.material = mat;
            }
            #endif
            
            renderPass = new CustomEffectPass(settings, effectSettings);
            renderPass.renderPassEvent = effectSettings.skipTransparents ? RenderPassEvent.BeforeRenderingTransparents : settings.GetInjectionPoint();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderPass?.Setup(renderer, renderingData);
        }
    }
}