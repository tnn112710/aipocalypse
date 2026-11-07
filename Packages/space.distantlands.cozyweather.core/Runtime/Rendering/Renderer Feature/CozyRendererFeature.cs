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
            cloudBuffer = TextureHandle.nullHandle;
            volumetricCloudDepthBuffer = TextureHandle.nullHandle;
            beforeCloudsBuffer = TextureHandle.nullHandle;
            afterCloudsBuffer = TextureHandle.nullHandle;
            sunShaftsBuffer = TextureHandle.nullHandle;
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

        public override void Create()
        {

            m_FogPass = new CozyFogRenderPass(fogMat, fogAccumulate, fogIntegrate)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_FogPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            m_SkyPass = new CozySkyRenderPass(skyMat)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };

            m_CloudsPass = new CozyCloudsRenderPass(volumetricCloudsMat, cloudsMat)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_CloudsPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            m_SunshaftsPass = new CozySunshaftsRenderPass(sunshaftsMat, sunshaftsMaskMat)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
            m_SunshaftsPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            
            renderBeforeClouds = new CozyRenderObjectsPass("Render Before Clouds", LayerMask.GetMask("Before Clouds"), false);
            renderAfterClouds = new CozyRenderObjectsPass("Render After Clouds", LayerMask.GetMask("After Clouds"), true);

            m_CompositePass = new CozyCompositePass(compositeMat)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 2
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!CozyWeather.Active) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;

            if (m_FogPass == null || m_SkyPass == null || m_CloudsPass == null || m_CompositePass == null)
            {
                return;
            }

            // Verify that materials are actually assigned before pushing passes to the renderer
            if (fogMat != null)
            {
                m_FogPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(m_FogPass);
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
            m_FogPass = null;
            m_CloudsPass = null;
            base.Dispose(disposing);
        }
    }
}