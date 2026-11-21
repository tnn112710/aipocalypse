//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DistantLands.Cozy
{
    public class CozyFrameData : ContextItem
    {
        public TextureHandle fogAccumulateVolume;
        public TextureHandle fogLightingVolume;
        public TextureHandle fogIntegrateVolume;
        public TextureHandle fogBuffer;
        public TextureHandle skyBuffer;
        public TextureHandle volumetricCloudBuffer;
        public TextureHandle cloudBuffer;
        public TextureHandle volumetricCloudDepthBuffer;
        public TextureHandle beforeCloudsBuffer;
        public TextureHandle afterCloudsBuffer;
        public TextureHandle sunShaftsBuffer;

        public override void Reset()
        {
            fogAccumulateVolume = TextureHandle.nullHandle;
            fogLightingVolume = TextureHandle.nullHandle;
            fogIntegrateVolume = TextureHandle.nullHandle;
            fogBuffer = TextureHandle.nullHandle;
            skyBuffer = TextureHandle.nullHandle;
            volumetricCloudBuffer = TextureHandle.nullHandle;
            cloudBuffer = TextureHandle.nullHandle;
            volumetricCloudDepthBuffer = TextureHandle.nullHandle;
            beforeCloudsBuffer = TextureHandle.nullHandle;
            afterCloudsBuffer = TextureHandle.nullHandle;
            sunShaftsBuffer = TextureHandle.nullHandle;
        }
    }

    internal static class CozyRenderGraphResources
    {
        private static Texture2D m_ClearTexture2D;
        private static Texture2DArray m_ClearTextureArray;
        private static RTHandle m_ClearTexture2DHandle;
        private static RTHandle m_ClearTextureArrayHandle;

        public static TextureHandle GetClearTexture(RenderGraph renderGraph, UniversalCameraData cameraData)
        {
            if (cameraData.xr.enabled && cameraData.xr.singlePassEnabled)
            {
                if (m_ClearTextureArrayHandle == null)
                {
                    int slices = Mathf.Max(2, cameraData.xr.viewCount);
                    m_ClearTextureArray = new Texture2DArray(1, 1, slices, TextureFormat.RGBA32, false, true)
                    {
                        name = "COZY Transparent XR Fallback",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    var clearPixel = new[] { Color.clear };
                    for (int slice = 0; slice < slices; slice++)
                        m_ClearTextureArray.SetPixels(clearPixel, slice);
                    m_ClearTextureArray.Apply();
                    m_ClearTextureArrayHandle = RTHandles.Alloc(m_ClearTextureArray);
                }

                return renderGraph.ImportTexture(m_ClearTextureArrayHandle);
            }

            if (m_ClearTexture2DHandle == null)
            {
                m_ClearTexture2D = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "COZY Transparent Fallback",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                m_ClearTexture2D.SetPixel(0, 0, Color.clear);
                m_ClearTexture2D.Apply();
                m_ClearTexture2DHandle = RTHandles.Alloc(m_ClearTexture2D);
            }

            return renderGraph.ImportTexture(m_ClearTexture2DHandle);
        }
    }


    [DisallowMultipleRendererFeature]
    public class CozyRendererFeature : ScriptableRendererFeature
    {
        CozySkyRenderPass m_SkyPass;
        CozyFogRenderPass m_FogPass;
        CozyCloudsRenderPass m_CloudsPass;
        CozyCompositePass m_CompositePass;
        CozySunshaftsRenderPass m_SunshaftsPass;
        TransparentFogCleanupPass m_TransparentFogCleanupPass;
        CozyRenderObjectsPass renderBeforeClouds;
        CozyRenderObjectsPass renderAfterClouds;

        [SerializeField] private ComputeShader fogAccumulate;
        [SerializeField] private ComputeShader fogIntegrate;
        [SerializeField] private Material skyMat;
        [SerializeField] private Material fogMat;
        [SerializeField] private Material cloudsMat;
        [SerializeField] private Material volumetricCloudsMat;
        [SerializeField] private Material sunshaftsMat;
        [SerializeField] private Material sunshaftsMaskMat;
        [SerializeField] private Material compositeMat;

        private Material runtimeSky;
        private Material runtimeFog;
        private Material runtimeClouds;
        private Material runtimeVolumetricClouds;
        private Material runtimeSunshafts;
        private Material runtimeSunshaftsMask;
        private Material runtimeComposite;

        public override void Create()
        {
            ReleaseMaterials();
            runtimeSky = CloneMaterial(skyMat);
            runtimeFog = CloneMaterial(fogMat);
            runtimeClouds = CloneMaterial(cloudsMat);
            runtimeVolumetricClouds = CloneMaterial(volumetricCloudsMat);
            runtimeSunshafts = CloneMaterial(sunshaftsMat);
            runtimeSunshaftsMask = CloneMaterial(sunshaftsMaskMat);
            runtimeComposite = CloneMaterial(compositeMat);
            CozyShaderIDs.Initialize();
            CozyMaterialProvider.KeywordsChanged += UpdateMaterialKeywords;
            UpdateMaterialKeywords();

            m_FogPass = new CozyFogRenderPass(runtimeFog, fogAccumulate, fogIntegrate)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_FogPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            m_TransparentFogCleanupPass = new TransparentFogCleanupPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };

            m_SkyPass = new CozySkyRenderPass(runtimeSky)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };

            m_CloudsPass = new CozyCloudsRenderPass(runtimeVolumetricClouds, runtimeClouds)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_CloudsPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            m_SunshaftsPass = new CozySunshaftsRenderPass(runtimeSunshafts, runtimeSunshaftsMask)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_SunshaftsPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            
            renderBeforeClouds = new CozyRenderObjectsPass("Render Before Clouds", LayerMask.GetMask("Before Clouds"), false);
            renderAfterClouds = new CozyRenderObjectsPass("Render After Clouds", LayerMask.GetMask("After Clouds"), true);

            m_CompositePass = new CozyCompositePass(runtimeComposite)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 2
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!CozyWeather.Active) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            if (renderingData.cameraData.camera.TryGetComponent(out CozyCameraRendering cameraSettings) &&
                cameraSettings.isActiveAndEnabled && cameraSettings.excludeFromCozyRendering) return;

            if (m_FogPass == null || m_SkyPass == null || m_CloudsPass == null || m_CompositePass == null)
            {
                return;
            }

            // Verify that materials are actually assigned before pushing passes to the renderer
            if (fogMat != null)
            {
                m_FogPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_FogPass);
                renderer.EnqueuePass(m_TransparentFogCleanupPass);
            }

            if (skyMat != null)
            {
                renderer.EnqueuePass(m_SkyPass);
            }

            renderBeforeClouds.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(renderBeforeClouds);

            if (cloudsMat != null)
            {
                m_CloudsPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_CloudsPass);
            }

            renderer.EnqueuePass(renderAfterClouds);

            if (sunshaftsMat != null)
            {
                m_SunshaftsPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_SunshaftsPass);
            }

            if (compositeMat != null)
            {
                m_CompositePass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_CompositePass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseMaterials();
            m_FogPass = null;
            m_CloudsPass = null;
            base.Dispose(disposing);
        }

        private static Material CloneMaterial(Material source)
        {
            if (source == null) return null;
            return new Material(source) { name = source.name + " (COZY Runtime)", hideFlags = HideFlags.HideAndDontSave };
        }

        private void UpdateMaterialKeywords()
        {
            CozyMaterialProvider.ApplyKeywords(runtimeSky, runtimeFog, runtimeClouds, runtimeVolumetricClouds, runtimeSunshafts);
        }

        private void ReleaseMaterials()
        {
            CozyMaterialProvider.KeywordsChanged -= UpdateMaterialKeywords;
            CoreUtils.Destroy(runtimeSky);
            CoreUtils.Destroy(runtimeFog);
            CoreUtils.Destroy(runtimeClouds);
            CoreUtils.Destroy(runtimeVolumetricClouds);
            CoreUtils.Destroy(runtimeSunshafts);
            CoreUtils.Destroy(runtimeSunshaftsMask);
            CoreUtils.Destroy(runtimeComposite);
            runtimeSky = runtimeFog = runtimeClouds = runtimeVolumetricClouds = null;
            runtimeSunshafts = runtimeSunshaftsMask = runtimeComposite = null;
        }

        private class TransparentFogCleanupPass : ScriptableRenderPass
        {
            private class PassData { }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddUnsafePass("COZY Transparent Fog Cleanup", out PassData passData))
                {
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        context.cmd.SetGlobalVector(CozyFogRenderPass.TransparentFogFeaturesId, Vector4.zero);
                        context.cmd.SetGlobalVector(CozyFogRenderPass.TransparentFogFeatures2Id, Vector4.zero);
                    });
                }
            }
        }
    }
}