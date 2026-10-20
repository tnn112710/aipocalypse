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
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace sc.screeneffects.runtime
{
    public class SunshaftsRenderer : PostEffectFeature<Sunshafts>
    {
        class SunshaftsRenderPass : PostEffectRenderer<Sunshafts>
        {
            public SunshaftsRenderPass(RenderFeatureSettings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.GetInjectionPoint();
                shaderName = ShaderNames.Sunshafts;
                requiresDepth = true;
                ProfilerTag = GetProfilerTag();
            }
            
            public enum Pass
            {
                SkySource,
                RadialBlur,
                Blend
            }

            public override void Setup(ScriptableRenderer renderer, RenderingData renderingData)
            {
                requiresDepth = true;
                
                base.Setup(renderer, renderingData);

                if (!render) return;
                
                renderer.EnqueuePass(this);
            }

            VisibleLight sun;
            private Light sunLight;

            struct SunSettings
            {
                public Vector3 worldPosition;
                public Vector4 screenPosition;
                
                public float intensity;
                public Color color;
                public float temperature;
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                ConfigurePass(renderGraph, frameData);
                
                var lightData = frameData.Get<UniversalLightData>();
                
                //Realtime light available (not the case when using baked lighting)
                if (lightData.mainLightIndex > -1)
                {
                    sun = lightData.visibleLights[lightData.mainLightIndex];

                    if (sun.lightType != LightType.Directional)
                    {
                        return;
                    }
                    sunLight = sun.light;
                }
                //Fallback
                else
                {
                    sunLight = RenderSettings.sun;
                }
                
                if(!sunLight) return;
                
                var desc = renderGraph.GetTextureDesc(cameraColor);
                
                Utilities.DownscaleBuffer(ref desc, volumeSettings.resolution.value+1);
                
                desc.format = volumeSettings.highQuality.value ? GraphicsFormat.R16_UNorm : GraphicsFormat.R8_UNorm;
                
                // Create temporary textures
                desc.name = "SunshaftsBlurBuffer1";
                var blurBuffer1 = renderGraph.CreateTexture(desc);
                desc.name = "SunshaftsBlurBuffer2";
                var blurBuffer2 = renderGraph.CreateTexture(desc);

                SunSettings sunSettings = new SunSettings()
                {
                    worldPosition = -RenderSettings.sun.transform.forward * 1E10f,
                    screenPosition = GetSunScreenPosition(cameraData),
                };

                if (cameraData.xrRendering)
                {
                    Vector2 sunPositionRightEye = GetSunScreenPosition(cameraData, 1);
                    sunSettings.screenPosition.z = sunPositionRightEye.x;
                    sunSettings.screenPosition.w = sunPositionRightEye.y;
                    
                    //if(sunPositionRightEye.x == 0 && sunPositionRightEye.y == 0) Debug.LogError("Sun position is zero, this is not supported in XR");
                }
       
                sunSettings.intensity = volumeSettings.intensity.value * volumeSettings.maxStrength.value * Mathf.Lerp(1f, sunLight.intensity, volumeSettings.casterIntensityInfluence.value);
                if (sunSettings.intensity == 0) return; 
                
                sunSettings.color = volumeSettings.sunColor.value * Color.Lerp(Color.white, sunLight.color, volumeSettings.casterColorInfluence.value);
                
                float kelvin = 5000f;
                if (sunLight.useColorTemperature && GraphicsSettings.lightsUseColorTemperature)
                {
                    kelvin = sunLight.colorTemperature * volumeSettings.casterColorInfluence.value;
                }

                kelvin += volumeSettings.temperatureShift.value;
                
                Color lightTemperatureColor = Mathf.CorrelatedColorTemperatureToRGB(kelvin);
                sunSettings.color *= lightTemperatureColor;
                
                //Sky source pass
                RenderSkySource(renderGraph, cameraColor, cameraDepth, blurBuffer1, cameraData, sunSettings);
                
                //Blur passes
                RenderBlur(renderGraph, blurBuffer1, blurBuffer2, cameraData, sunSettings);

                //Final blend pass
                RenderFinalBlend(renderGraph, blurBuffer1, cameraColor, destination, sunSettings);
                
                resourceData.cameraColor = destination;
            }
            
            private class SunshaftsSkySourcePassData : BasePassData
            {
                public Sunshafts volumeSettings;
                public Vector3 cameraForwardVector;
                public Vector3 sunWorldPosition;
                
                //2nd eye position in ZW components, if XR is enabled
                public Vector4 sunScreenPositions;
            }

            //Reproduction of the original shader code, converting a world-space position to view-space
            private Vector2 GetSunScreenPosition(UniversalCameraData m_cameraData, int eyeIndex = 0)
            {
                if (m_cameraData.xrRendering)
                {
                    int viewCount = m_cameraData.xr.viewCount;

                    if (eyeIndex < 0 || eyeIndex >= viewCount)
                    {
                        eyeIndex = 0;
                    }
                }
                else
                {
                    eyeIndex = 0;
                }
                
                Matrix4x4 viewProjection = m_cameraData.GetProjectionMatrix(eyeIndex) * m_cameraData.GetViewMatrix(eyeIndex);
                
                //Position 10k units away along the direction
                Vector3 sunWorldPosition = -RenderSettings.sun.transform.forward * 1E10f;
                
                Vector4 positionCS = viewProjection * new Vector4(sunWorldPosition.x, sunWorldPosition.y, sunWorldPosition.z, 1);

                int projectionSign = SystemInfo.graphicsUVStartsAtTop ? 1 : -1;
                
                //Clip to view-space
                Vector2 positionVS = positionCS * 0.5f;
                positionVS = new Vector2(positionVS.x + (positionCS.w * 0.5f), (positionVS.y * projectionSign) + (positionCS.w * 0.5f));
                positionVS /= positionCS.w;

                return positionVS;
            }
            
            private void RenderSkySource(RenderGraph renderGraph, TextureHandle colorHandle, TextureHandle depthHandle, 
                TextureHandle targetHandle, UniversalCameraData m_cameraData, SunSettings sunSettings)
            {
                using (var builder = renderGraph.AddRasterRenderPass<SunshaftsSkySourcePassData>("Sunshafts Sky Source", out var passData))
                {
                    passData.source = colorHandle;
                    passData.material = Material;
                    passData.volumeSettings = volumeSettings;
                    passData.cameraForwardVector = m_cameraData.camera.transform.forward;
                    passData.sunWorldPosition = sunSettings.worldPosition;
                    passData.sunScreenPositions = sunSettings.screenPosition;
                    
                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.UseTexture(depthHandle, AccessFlags.Read);

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(targetHandle, 0);
   
                    builder.SetRenderFunc((SunshaftsSkySourcePassData data, RasterGraphContext context) =>
                    {
                        ExecuteSkySourcePass(data, context);
                    });
                }
            }
            
            private static void ExecuteSkySourcePass(SunshaftsSkySourcePassData data, RasterGraphContext context)
            {
                var cmd = context.cmd;

                data.material.SetInteger("_SourceMode", (int)data.volumeSettings.sourceMode.value);
                data.material.SetFloat("_LuminanceThreshold", data.volumeSettings.skyLuminanceThreshold.value);
                
                data.material.SetFloat("_Length", data.volumeSettings.falloff.value);

                data.material.SetVector("_SunPosition", data.sunWorldPosition);
                data.material.SetVector("_SunScreenPosition", data.sunScreenPositions);
                
                data.material.SetColor("_SunThreshold", data.volumeSettings.sunThreshold.value);
                data.material.SetVector("_CameraForwardVector", -data.cameraForwardVector);
                
                Blitter.BlitTexture(cmd, data.source, blitScale, data.material, (int)Pass.SkySource);
            }
            
            private class SunshaftsBlurPassData
            {
                public TextureHandle sourceHandle;
                public TextureHandle targetHandle;
                public Material material;
                public float blurOffset;
                public Vector4 sunScreenPosition;
            }
            
            private void RenderBlur(RenderGraph renderGraph, TextureHandle blurBuffer1, TextureHandle blurBuffer2, UniversalCameraData cameraData, SunSettings sun)
            {
                float blurAmount = volumeSettings.length.value * 0.01f;

                //1st pass
                using (var builder = renderGraph.AddRasterRenderPass<SunshaftsBlurPassData>($"Sunshafts Blur A", out var passData))
                {
                    passData.sourceHandle = blurBuffer1;
                    passData.targetHandle = blurBuffer2;
                    passData.material = Material;
                    passData.blurOffset = blurAmount;
                    passData.sunScreenPosition = sun.screenPosition;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(passData.targetHandle, 0);
                    
                    builder.UseTexture(blurBuffer1);

                    builder.SetRenderFunc((SunshaftsBlurPassData data, RasterGraphContext context) =>
                    {
                        ExecuteBlurPass(data, context);
                    });
                }

                //2nd pass
                using (var builder = renderGraph.AddRasterRenderPass<SunshaftsBlurPassData>($"Sunshafts Blur B", out var passData))
                {
                    passData.sourceHandle = blurBuffer2;
                    passData.targetHandle = blurBuffer1;
                    passData.material = Material;
                    passData.blurOffset = blurAmount;
                    passData.sunScreenPosition = sun.screenPosition;

                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(passData.targetHandle, 0);
                    builder.UseTexture(blurBuffer2);

                    builder.SetRenderFunc((SunshaftsBlurPassData data, RasterGraphContext context) =>
                    {
                        ExecuteBlurPass(data, context);
                    });
                }
            }
            
            private static void ExecuteBlurPass(SunshaftsBlurPassData data, RasterGraphContext context)
            {
                var cmd = context.cmd;
                data.material.SetVector("_SunScreenPosition", data.sunScreenPosition);
                data.material.SetFloat("_BlurRadius", data.blurOffset);
                Blitter.BlitTexture(cmd, data.sourceHandle, blitScale, data.material, (int)Pass.RadialBlur);
            }
            
            private class SunshaftsBlendPassData
            {
                public TextureHandle colorHandle;
                public TextureHandle sunshaftsHandle;
                public TextureHandle targetHandle;
                public Material material;
                public Sunshafts volumeSettings;

                public float intensity;
                public Color color;
            }
            
            private void RenderFinalBlend(RenderGraph renderGraph, TextureHandle sunshaftsBuffer, TextureHandle colorHandle, TextureHandle destination, SunSettings sun)
            {
                using (var builder = renderGraph.AddRasterRenderPass<SunshaftsBlendPassData>("Sunshafts Blend", out var passData))
                {
                    passData.colorHandle = colorHandle;
                    passData.sunshaftsHandle = sunshaftsBuffer;
                    passData.targetHandle = destination;
                    passData.material = Material;
                    passData.volumeSettings = volumeSettings;
                    
                    passData.intensity = sun.intensity;
                    passData.color = sun.color;
                    
                    builder.AllowPassCulling(false);
                    builder.SetRenderAttachment(passData.targetHandle, 0);
                    builder.UseTexture(colorHandle);
                    builder.UseTexture(sunshaftsBuffer);

                    builder.SetRenderFunc((SunshaftsBlendPassData data, RasterGraphContext context) =>
                    {
                        ExecuteBlendPass(data, context);
                    });
                }
            }
            
            private static void ExecuteBlendPass(SunshaftsBlendPassData data, RasterGraphContext context)
            {
                //Represent final values
                data.material.SetColor("_SunColor", data.color);
                data.material.SetFloat("_Intensity", data.intensity);
                
                data.material.SetTexture("_SunshaftBuffer", data.sunshaftsHandle);
                Blitter.BlitTexture(context.cmd, data.colorHandle, blitScale, data.material, (int)Pass.Blend);
            }
        }

        SunshaftsRenderPass renderPass;

        public override void Create()
        {
            renderPass = new SunshaftsRenderPass(settings);
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
