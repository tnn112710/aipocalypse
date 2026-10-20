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
    public class MotionTrailsRenderer : PostEffectFeature<MotionTrails>
    {
        class MotionTrailsRenderPass : PostEffectRenderer<MotionTrails>
        {
            public MotionTrailsRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.MotionTrails;
                ProfilerTag = GetProfilerTag();
            }
            
            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresMotionVectors = volumeSettings.useMotionVectors.value;

                base.Setup(renderer, renderingData);

                if (!render) return;

                renderer.EnqueuePass(this);
            }
            
            //Single persistent buffer storing accumulated results
            private RTHandle accumulationBuffer;
            
            enum Pass
            {
                Mask,
                Accumulate,
                Blend,
            }

            private class MaskPassData : BasePassData
            {
                public bool useMotionVectors;
                
                public TextureHandle motionVectors;
                public float motionVectorsIntensity;
            }
            
            private class AccumulatePassData
            {
                public TextureHandle current;
                public TextureHandle prev;
                public Material material;
            }
            
            private class BlendPassData : BasePassData
            {
                public TextureHandle tracers;
            }
            
            private readonly int _MotionTrailsTextureCurrent = Shader.PropertyToID("_MotionTrailsTextureCurrent");
            private readonly int _MotionTrailsTexturePrev = Shader.PropertyToID("_MotionTrailsTexturePrev");
            private readonly int _MotionTrailsTexture = Shader.PropertyToID("_MotionTrailsTexture");
            private readonly int _MotionTrailsDirection = Shader.PropertyToID("_MotionTrailsDirection");
            
            private void InitializeAccumulationBuffer(TextureDesc desc)
            {
                if (accumulationBuffer == null)
                {
                    accumulationBuffer = RTHandles.Alloc(desc.width, desc.height, desc.slices, 
                        desc.depthBufferBits, desc.colorFormat, desc.filterMode, 
                        desc.wrapMode, desc.dimension, false, false, false, name: "MotionTrailsAccumulationPrev");
                }
            }

            private void CleanupAccumulationBuffer()
            {
                RTHandles.Release(accumulationBuffer);
                accumulationBuffer = null;
            }
            
            private float elapsedTime;
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);

                var desc = new TextureDesc(cameraColorDesc);
                desc.format = cameraData.cameraTargetDescriptor.graphicsFormat;

                desc.width /= 1;
                desc.height /= 1;
                
                // Create temporary textures
                desc.name = "MotionTrailsAccumulationA";
                var accumulationA = renderGraph.CreateTexture(desc);
                desc.name = "MotionTrailsAccumulationB";
                var accumulationB = renderGraph.CreateTexture(desc);
                
                InitializeAccumulationBuffer(desc);
                //RenderingUtils.ReAllocateHandleIfNeeded(ref accumulationBuffer, desc, "MotionTrailsAccumulationPrev");
                //Import the persistent accumulation buffer for reading (previous frame)
                TextureHandle previousAccumulation = renderGraph.ImportTexture(accumulationBuffer);
                
                bool updateAccumulationBuffer = volumeSettings.intervalMode.value == MotionTrails.Interval.EveryFrame;

                //Remapped to a range that works best for the effect, whilst keeping the front-end "length" parameter normalized
                float dissipation = Mathf.Lerp(0.9f, 0.998f, volumeSettings.length.value);
                float scaledDissipation = dissipation;
                
                if (volumeSettings.intervalMode.value == MotionTrails.Interval.Seconds)
                {
                    elapsedTime += Time.deltaTime;

                    if (elapsedTime >= volumeSettings.captureInterval.value)
                    {
                        elapsedTime = 0;
                        
                        updateAccumulationBuffer = true;
                    }
                    
                    //Need to account for the time in frames between captures and accelerate the dissipation based on the delta time between captures.
                    scaledDissipation *= Mathf.Lerp(1f, 0.1f, volumeSettings.captureInterval.value);
                }
                
                Vector4 parameters = new Vector4(volumeSettings.luminanceThreshold.value, volumeSettings.intensity.value, scaledDissipation, (int)volumeSettings.blendMode.value);
                
                //Shared between all passes
                Material.SetVector(ShaderParameters.Params, parameters);
                Material.SetInt("_Clamped", volumeSettings.clamped.value ? 1 : 0);
                Material.SetVector(_MotionTrailsDirection, volumeSettings.constantDirection.value);
                
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Motion Trails: Mask", out var passData))
                {
                    passData.source = cameraColor;
                    passData.destination = accumulationA;
                    passData.material = Material;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(accumulationA, 0);
                    builder.UseTexture(cameraColor, AccessFlags.Read);
                    
                    SetupRenderingLayers(builder, passData);

                    passData.useMotionVectors = volumeSettings.useMotionVectors.value && volumeSettings.motionVectorsIntensity.value > 0;
                    
                    if (passData.useMotionVectors)
                    {
                        TextureHandle motionVectors = resourceData.motionVectorColor;
                        if (motionVectors.IsValid())
                        {
                            passData.motionVectors = motionVectors;
                            passData.motionVectorsIntensity = volumeSettings.motionVectorsIntensity.value;
                            builder.UseTexture(motionVectors, AccessFlags.Read);
                        }
                        else
                        {
                            passData.useMotionVectors = false;
                            passData.motionVectorsIntensity = -1;
                        }
                    }
                    
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                    {
                        data.material.SetInteger(ShaderParameters.RenderingLayer, (int)data.renderingLayerMask);

                        if (passData.useMotionVectors)
                        {
                            data.material.SetTexture(TextureNames._MotionVectorTexture, data.motionVectors);
                        }
                        data.material.SetFloat("_MotionVectorsIntensity", data.motionVectorsIntensity);
                        
                        Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, (int)Pass.Mask);
                    });
                }

                //Accumulate pass: blend previous frame with current mask
                using (var builder = renderGraph.AddRasterRenderPass<AccumulatePassData>("Motion Trails: Accumulate", out var passData))
                {
                    passData.current = accumulationA;
                    passData.prev = previousAccumulation;
                    passData.material = Material;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(accumulationB, 0);
                    builder.UseTexture(accumulationA, AccessFlags.Read);
                    builder.UseTexture(previousAccumulation, AccessFlags.Read);

                    builder.SetRenderFunc((AccumulatePassData data, RasterGraphContext context) =>
                    {
                        data.material.SetTexture(_MotionTrailsTextureCurrent, data.current);
                        data.material.SetTexture(_MotionTrailsTexturePrev, data.prev);
                        
                        Blitter.BlitTexture(context.cmd, data.current, blitScale, data.material, (int)Pass.Accumulate);
                    });
                }
                
                if (updateAccumulationBuffer)
                {
                    //Copy accumulation temp result back to persistent buffer for next frame
                    BlitCopy(renderGraph, accumulationB, previousAccumulation);
                }

                //Blend the accumulated tracers with the current screen color
                using (var builder = renderGraph.AddRasterRenderPass<BlendPassData>("Motion Trails: Blend", out var passData))
                {
                    passData.source = cameraColor;
                    passData.destination = destination;
                    passData.material = Material;
                    passData.tracers = accumulationB;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(destination, 0);
                    builder.UseTexture(accumulationB, AccessFlags.Read);
                    builder.UseTexture(cameraColor, AccessFlags.Read);

                    builder.SetRenderFunc((BlendPassData data, RasterGraphContext context) =>
                    {
                        data.material.SetTexture(_MotionTrailsTexture, data.tracers);

                        Blitter.BlitTexture(context.cmd, cameraColor, blitScale, data.material, (int)Pass.Blend);
                    });
                }
                
                resourceData.cameraColor = destination;
            }
            
            public new void Dispose()
            {
                base.Dispose();
                CleanupAccumulationBuffer();
            }
        }
        
        MotionTrailsRenderPass renderPass;

        public override void Create()
        {
            renderPass = new MotionTrailsRenderPass(settings);
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