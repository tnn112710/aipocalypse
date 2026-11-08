//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozyCloudsHistory : CameraHistoryItem
    {
        private int m_ColorId;
        private int m_DepthId;
        private Hash128 m_DescKey;
        private BufferedRTHandleSystem m_Storage = new();
        private int m_LastFrameNumber = -1;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_ColorId = MakeId(0);
            m_DepthId = MakeId(1);
        }

        public override void Reset()
        {
            m_Storage.Dispose();
            m_Storage = new BufferedRTHandleSystem();
            m_DescKey = default;
            m_LastFrameNumber = -1;
        }

        public RTHandle currentColor => m_Storage.GetFrameRT(m_ColorId, 0);
        public RTHandle previousColor => m_Storage.GetFrameRT(m_ColorId, 1);
        public RTHandle currentDepth => m_Storage.GetFrameRT(m_DepthId, 0);
        public RTHandle previousDepth => m_Storage.GetFrameRT(m_DepthId, 1);

        public void Update(int width, int height)
        {
            RenderTextureDescriptor colorDesc = new RenderTextureDescriptor(width, height)
            {
                dimension = TextureDimension.Tex2D,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                msaaSamples = 1,
                depthBufferBits = 0
            };

            Hash128 key = Hash128.Compute(ref colorDesc);

            if (currentColor == null || m_DescKey != key)
            {
                RenderTextureDescriptor depthDesc = colorDesc;
                depthDesc.graphicsFormat = GraphicsFormat.D32_SFloat;

                m_Storage.ReleaseBuffer(m_ColorId);
                m_Storage.ReleaseBuffer(m_DepthId);
                m_Storage.AllocBuffer(m_ColorId, 2, ref colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 0, 0, "CozyCloudsColorHistory");
                m_Storage.AllocBuffer(m_DepthId, 2, ref depthDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 0, 0, "CozyCloudsDepthHistory");
                m_DescKey = key;
                m_LastFrameNumber = -1;
            }

            // URP may advance a base camera's history again for each stacked overlay.
            if (m_LastFrameNumber != Time.frameCount)
            {
                m_Storage.SwapAndSetReferenceSize(width, height);
                m_LastFrameNumber = Time.frameCount;
            }
        }
    }

    public class CozyCloudsRenderPass : ScriptableRenderPass
    {
        private Material volumetricMaterial;
        private Material layeredMaterial;

        private MaterialPropertyBlock m_SharedPropertyBlock = new();

        public float positionThreshold = 10f;
        public float rotationThreshold = 2f;
        public float timeThreshold = 0.005f;

        private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
        private static readonly int kFogTexPropertyId = Shader.PropertyToID("_FogTex");
        private static readonly int kSkyTexPropertyId = Shader.PropertyToID("_SkyTex");
        private static readonly int kHistoryTexPropertyId = Shader.PropertyToID("_HistoryTex");
        private static readonly int kDepthHistoryTexPropertyId = Shader.PropertyToID("_DepthHistoryTex");
        private static readonly int kPrevViewProjMatrixId = Shader.PropertyToID("_PrevViewProjMatrix");
        private static readonly int kHistoryWeightPropertyId = Shader.PropertyToID("_HistoryWeight");

        private float m_PrevTime;
        private bool m_HasPrevTime;

        private class VolumetricPassData
        {
            public Material material;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle inputTexture;
            public TextureHandle historyTexture;
            public TextureHandle depthHistoryTexture;
            public TextureHandle fogTexture;
            public TextureHandle skyTexture;
            public Matrix4x4 prevViewProjMatrix;
            public float historyWeight;
        }

        private class MainPassData
        {
            public Material material;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle inputTexture;
            public TextureHandle fogTexture;
            public TextureHandle skyTexture;
        }

        private class ReadbackPassData
        {
            public TextureHandle cloud;
        }

        public CozyCloudsRenderPass(Material volumetricMaterial, Material backgroundMaterial)
        {
            this.volumetricMaterial = volumetricMaterial;
            this.layeredMaterial = backgroundMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!CozyPreferences.CurrentStyle.UseClouds) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            var cozyData = frameData.GetOrCreate<CozyFrameData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.renderType == CameraRenderType.Overlay) return;

#if UNITY_EDITOR
            if (cameraData.isSceneViewCamera && !SceneView.lastActiveSceneView.sceneViewState.showSkybox)
                return;
#endif

            if (!resourceData.cameraColor.IsValid()) return;

            Camera currentCamera = cameraData.camera;
            var cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);

            if (CozyPreferences.CurrentStyle.UseCloudsVolumetric)
            {
                int targetWidth = Mathf.Max(1, (int)(cameraDesc.width / (float)CozyPreferences.CloudDownsampling));
                int targetHeight = Mathf.Max(1, (int)(cameraDesc.height / (float)CozyPreferences.CloudDownsampling));

                TextureHandle historyWrite, historyRead, historyDepthWrite, historyDepthRead;
                bool useTemporalHistory = false;

                if (cameraData.historyManager != null)
                {
                    cameraData.historyManager.RequestAccess<CozyCloudsHistory>();
                    CozyCloudsHistory cloudsHistory = cameraData.historyManager.GetHistoryForWrite<CozyCloudsHistory>();

                    if (cloudsHistory != null)
                    {
                        cloudsHistory.Update(targetWidth, targetHeight);

                        historyWrite = renderGraph.ImportTexture(cloudsHistory.currentColor);
                        historyRead = renderGraph.ImportTexture(cloudsHistory.previousColor);
                        historyDepthWrite = renderGraph.ImportTexture(cloudsHistory.currentDepth);
                        historyDepthRead = renderGraph.ImportTexture(cloudsHistory.previousDepth);
                        useTemporalHistory = true;
                    }
                    else
                    {
                        historyWrite = historyRead = historyDepthWrite = historyDepthRead = TextureHandle.nullHandle;
                    }
                }
                else
                {
                    historyWrite = historyRead = historyDepthWrite = historyDepthRead = TextureHandle.nullHandle;
                }

                if (!useTemporalHistory)
                {
                    var scratchColorDesc = new TextureDesc(targetWidth, targetHeight)
                    {
                        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                        dimension = TextureDimension.Tex2D,
                        msaaSamples = MSAASamples.None,
                        name = "ScratchVolumetricCloudsTexture"
                    };

                    var scratchDepthDesc = scratchColorDesc;
                    scratchDepthDesc.colorFormat = GraphicsFormat.D32_SFloat;
                    scratchDepthDesc.name = "ScratchVolumetricCloudsDepth";

                    historyWrite = renderGraph.CreateTexture(scratchColorDesc);
                    historyDepthWrite = renderGraph.CreateTexture(scratchDepthDesc);
                    historyRead = renderGraph.CreateTexture(scratchColorDesc);
                    historyDepthRead = renderGraph.CreateTexture(scratchDepthDesc);
                }

                Matrix4x4 viewMatrix = currentCamera.worldToCameraMatrix;
                Matrix4x4 projMatrix = currentCamera.nonJitteredProjectionMatrix;
                Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(projMatrix, true);
                Matrix4x4 viewProj = gpuProj * viewMatrix;

                Matrix4x4 prevViewProjMatrix = viewProj;
                Vector3 prevCamPos = currentCamera.transform.position;
                Quaternion prevCamRot = currentCamera.transform.rotation;
                bool hasPrevCameraFrame = false;

                if (cameraData.historyManager != null)
                {
                    cameraData.historyManager.RequestAccess<CozyCameraMotionHistory>();
                    CozyCameraMotionHistory cameraMotion = cameraData.historyManager.GetHistoryForWrite<CozyCameraMotionHistory>();

                    if (cameraMotion != null)
                    {
                        hasPrevCameraFrame = cameraMotion.TryGetPreviousFrame(out prevCamPos, out prevCamRot, out _, out prevViewProjMatrix);
                        cameraMotion.RecordCurrentFrame(currentCamera, viewMatrix, viewProj);
                    }
                }

                float timeMovement = 0f;
                if (CozyWeather.Instance.Time != null)
                {
                    if (m_HasPrevTime)
                        timeMovement = Mathf.Abs(m_PrevTime - CozyWeather.Instance.Time.CurrentTime);
                    m_PrevTime = CozyWeather.Instance.Time.CurrentTime;
                    m_HasPrevTime = true;
                }

                float posMovement = Vector3.Distance(currentCamera.transform.position, prevCamPos);
                float rotMovement = Quaternion.Angle(currentCamera.transform.rotation, prevCamRot);

                float currentHistoryWeight = 0.8f;
                if (!useTemporalHistory || !hasPrevCameraFrame ||
                    posMovement > positionThreshold || rotMovement > 0.005f || timeMovement > timeThreshold)
                {
                    currentHistoryWeight = 0.0f;
                }

                using (var builder = renderGraph.AddRasterRenderPass<VolumetricPassData>("Cozy Compute Volumetric Clouds Pass", out var passData))
                {
                    cozyData.volumetricCloudBuffer = historyWrite;
                    cozyData.volumetricCloudDepthBuffer = historyDepthWrite;

                    passData.material = volumetricMaterial;
                    passData.propertyBlock = m_SharedPropertyBlock;
                    passData.fogTexture = cozyData.fogBuffer;
                    passData.skyTexture = cozyData.skyBuffer;
                    passData.historyTexture = historyRead;
                    passData.depthHistoryTexture = historyDepthRead;
                    passData.prevViewProjMatrix = prevViewProjMatrix;
                    passData.historyWeight = currentHistoryWeight;

                    if (passData.fogTexture.IsValid())
                        builder.UseTexture(passData.fogTexture, AccessFlags.Read);

                    if (passData.skyTexture.IsValid())
                        builder.UseTexture(passData.skyTexture, AccessFlags.Read);

                    builder.UseTexture(passData.historyTexture, AccessFlags.Read);
                    builder.UseTexture(passData.depthHistoryTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(historyWrite, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(historyDepthWrite, AccessFlags.Write);

                    builder.SetRenderFunc<VolumetricPassData>(ExecuteVolumetricsPass);
                }
            }
            else
            {
                cozyData.volumetricCloudBuffer = renderGraph.defaultResources.blackTexture;
                cozyData.volumetricCloudDepthBuffer = renderGraph.defaultResources.whiteTexture;
            }

            if (CozyPreferences.CurrentStyle.UseCloudsAltocumulus || CozyPreferences.CurrentStyle.UseCloudsCumulus || CozyPreferences.CurrentStyle.UseCloudsContrails || CozyPreferences.CurrentStyle.UseCloudsCirrus)
            {
                var desc = cameraDesc;
                desc.name = "COZYCloudBuffer";
                desc.width = Mathf.Max(1, cameraDesc.width);
                desc.height = Mathf.Max(1, cameraDesc.height);
                desc.colorFormat = GraphicsFormat.R32G32B32A32_SFloat;
                TextureHandle cloudHandle = renderGraph.CreateTexture(desc);
                cozyData.cloudBuffer = cloudHandle;

                using (var builder = renderGraph.AddRasterRenderPass<MainPassData>("Cozy Render Clouds Pass", out var passData))
                {
                    cozyData.cloudBuffer = cloudHandle;

                    passData.material = layeredMaterial;
                    passData.propertyBlock = m_SharedPropertyBlock;
                    passData.fogTexture = cozyData.fogBuffer;
                    passData.skyTexture = cozyData.skyBuffer;

                    if (passData.fogTexture.IsValid())
                        builder.UseTexture(passData.fogTexture, AccessFlags.Read);

                    if (passData.skyTexture.IsValid())
                        builder.UseTexture(passData.skyTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(cloudHandle, 0, AccessFlags.Write);
                    builder.SetRenderFunc<MainPassData>(ExecuteRenderCloudsPass);
                }
            }

            if (CozyPreferences.CurrentStyle.UseLightingLensFlareOcclusion && CozyPreferences.CurrentStyle.UseCloudsVolumetric)
            {
                if (cozyData.volumetricCloudBuffer.IsValid() && cameraData.camera == FlareOcclusion.TargetCamera)
                {
                    using (var builder = renderGraph.AddUnsafePass<ReadbackPassData>("Cozy Cloud Readback", out var rb))
                    {
                        rb.cloud = cozyData.volumetricCloudBuffer;
                        builder.UseTexture(rb.cloud, AccessFlags.Read);
                        builder.AllowPassCulling(false);
                        builder.SetRenderFunc<ReadbackPassData>((data, ctx) =>
                            FlareOcclusion.IssueReadbacks(CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd), data.cloud));
                    }
                }
            }

        }

        private static void ExecuteVolumetricsPass(VolumetricPassData data, RasterGraphContext context)
        {
            data.propertyBlock.Clear();

            if (data.inputTexture.IsValid())
                data.propertyBlock.SetTexture(kBlitTexturePropertyId, data.inputTexture);

            if (data.fogTexture.IsValid())
                data.propertyBlock.SetTexture(kFogTexPropertyId, data.fogTexture);

            if (data.skyTexture.IsValid())
                data.propertyBlock.SetTexture(kSkyTexPropertyId, data.skyTexture);

            if (data.historyTexture.IsValid())
                data.propertyBlock.SetTexture(kHistoryTexPropertyId, data.historyTexture);

            if (data.depthHistoryTexture.IsValid())
                data.propertyBlock.SetTexture(kDepthHistoryTexPropertyId, data.depthHistoryTexture);

            data.propertyBlock.SetMatrix(kPrevViewProjMatrixId, data.prevViewProjMatrix);
            data.propertyBlock.SetFloat(kHistoryWeightPropertyId, data.historyWeight);

            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }

        private static void ExecuteRenderCloudsPass(MainPassData data, RasterGraphContext context)
        {
            data.propertyBlock.Clear();

            if (data.inputTexture.IsValid())
                data.propertyBlock.SetTexture(kBlitTexturePropertyId, data.inputTexture);

            if (data.fogTexture.IsValid())
                data.propertyBlock.SetTexture(kFogTexPropertyId, data.fogTexture);

            if (data.skyTexture.IsValid())
                data.propertyBlock.SetTexture(kSkyTexPropertyId, data.skyTexture);

            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }
    }
}