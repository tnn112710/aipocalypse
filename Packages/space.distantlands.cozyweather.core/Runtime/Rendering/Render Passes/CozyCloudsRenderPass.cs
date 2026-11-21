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
        private class EyeHistory
        {
            public readonly BufferedRTHandleSystem storage = new();
            public int colorId;
            public int depthId;
            public Hash128 descKey;
            public int renderingMode = -1;
            public int lastSwapFrame = -1;
            public int lastRenderedFrame = -1;
            public bool hasPreviousFrame;
            public Vector3 position;
            public Quaternion rotation;
            public float time;
            public bool hasTime;
            public Vector4 worldOffset;
            public readonly Matrix4x4[] viewProjMatrices = new Matrix4x4[2];
            public Vector3 previousPosition;
            public Quaternion previousRotation;
            public float previousTime;
            public bool previousHasTime;
            public Vector4 previousWorldOffset;
            public readonly Matrix4x4[] previousViewProjMatrices = new Matrix4x4[2];
        }

        private EyeHistory[] m_Eyes = { new EyeHistory(), new EyeHistory() };

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            for (int i = 0; i < m_Eyes.Length; i++)
            {
                m_Eyes[i].colorId = MakeId((uint)(i * 2));
                m_Eyes[i].depthId = MakeId((uint)(i * 2 + 1));
            }
        }

        public override void Reset()
        {
            foreach (var eye in m_Eyes)
                eye.storage.Dispose();

            m_Eyes = new[] { new EyeHistory(), new EyeHistory() };
            for (int i = 0; i < m_Eyes.Length; i++)
            {
                m_Eyes[i].colorId = MakeId((uint)(i * 2));
                m_Eyes[i].depthId = MakeId((uint)(i * 2 + 1));
            }
        }

        public RTHandle CurrentColor(int eyeIndex) => m_Eyes[eyeIndex].storage.GetFrameRT(m_Eyes[eyeIndex].colorId, 0);
        public RTHandle PreviousColor(int eyeIndex) => m_Eyes[eyeIndex].storage.GetFrameRT(m_Eyes[eyeIndex].colorId, 1);
        public RTHandle CurrentDepth(int eyeIndex) => m_Eyes[eyeIndex].storage.GetFrameRT(m_Eyes[eyeIndex].depthId, 0);
        public RTHandle PreviousDepth(int eyeIndex) => m_Eyes[eyeIndex].storage.GetFrameRT(m_Eyes[eyeIndex].depthId, 1);

        public void Update(int eyeIndex, int renderingMode, int width, int height, TextureDimension dimension, int slices, RenderTextureDescriptor cameraDescriptor)
        {
            EyeHistory eye = m_Eyes[eyeIndex];
            RenderTextureDescriptor colorDesc = cameraDescriptor;
            colorDesc.width = width;
            colorDesc.height = height;
            colorDesc.dimension = dimension;
            colorDesc.volumeDepth = Mathf.Max(1, slices);
            colorDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            colorDesc.msaaSamples = 1;
            colorDesc.depthBufferBits = 0;
            colorDesc.bindMS = false;

            Hash128 key = Hash128.Compute(ref colorDesc);
            if (CurrentColor(eyeIndex) == null || eye.descKey != key)
            {
                RenderTextureDescriptor depthDesc = colorDesc;
                depthDesc.graphicsFormat = GraphicsFormat.D32_SFloat;

                eye.storage.ReleaseBuffer(eye.colorId);
                eye.storage.ReleaseBuffer(eye.depthId);
                eye.storage.AllocBuffer(eye.colorId, 2, ref colorDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 0, 0, "CozyCloudsColorHistory");
                eye.storage.AllocBuffer(eye.depthId, 2, ref depthDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, false, 0, 0, "CozyCloudsDepthHistory");
                eye.descKey = key;
                eye.lastRenderedFrame = -1;
                eye.hasPreviousFrame = false;
            }

            if (eye.renderingMode != renderingMode)
            {
                eye.renderingMode = renderingMode;
                eye.lastRenderedFrame = -1;
                eye.hasPreviousFrame = false;
            }

            if (eye.lastSwapFrame != Time.frameCount)
            {
                eye.storage.SwapAndSetReferenceSize(width, height);
                eye.lastSwapFrame = Time.frameCount;
            }
        }

        public bool TryGetPreviousFrame(int eyeIndex, out Vector3 position, out Quaternion rotation,
            out float time, out bool hasTime, out Matrix4x4[] viewProjMatrices)
        {
            EyeHistory eye = m_Eyes[eyeIndex];
            if (eye.lastRenderedFrame != Time.frameCount)
            {
                eye.hasPreviousFrame = eye.lastRenderedFrame == Time.frameCount - 1 && eye.lastRenderedFrame >= 0;
                if (eye.hasPreviousFrame)
                {
                    eye.previousPosition = eye.position;
                    eye.previousRotation = eye.rotation;
                    eye.previousTime = eye.time;
                    eye.previousHasTime = eye.hasTime;
                    eye.previousWorldOffset = eye.worldOffset;
                    System.Array.Copy(eye.viewProjMatrices, eye.previousViewProjMatrices, 2);
                }
            }

            position = eye.previousPosition;
            rotation = eye.previousRotation;
            time = eye.previousTime;
            hasTime = eye.previousHasTime;
            viewProjMatrices = eye.previousViewProjMatrices;
            return eye.hasPreviousFrame &&
                eye.previousWorldOffset.Equals(Shader.GetGlobalVector(CozyShaderIDs.baseWeatherCloudWorldOffset));
        }

        public void RecordCurrentFrame(int eyeIndex, Vector3 position, Quaternion rotation,
            float time, bool hasTime, Matrix4x4[] viewProjMatrices)
        {
            EyeHistory eye = m_Eyes[eyeIndex];
            eye.position = position;
            eye.rotation = rotation;
            eye.time = time;
            eye.hasTime = hasTime;
            eye.worldOffset = Shader.GetGlobalVector(CozyShaderIDs.baseWeatherCloudWorldOffset);
            System.Array.Copy(viewProjMatrices, eye.viewProjMatrices, 2);
            eye.lastRenderedFrame = Time.frameCount;
        }
    }

    public class CozyCloudsRenderPass : ScriptableRenderPass
    {
        private Material volumetricMaterial;
        private Material layeredMaterial;

        private MaterialPropertyBlock m_SharedPropertyBlock = new();

        public float positionThreshold = 10f;
        public float rotationThreshold = 0.025f;
        public float timeThreshold = 0.005f;

        private static readonly int kFogTexPropertyId = Shader.PropertyToID("_FogTex");
        private static readonly int kSkyTexPropertyId = Shader.PropertyToID("_SkyTex");
        private static readonly int kHistoryTexPropertyId = Shader.PropertyToID("_HistoryTex");
        private static readonly int kDepthHistoryTexPropertyId = Shader.PropertyToID("_DepthHistoryTex");
        private static readonly int kPrevViewProjMatrixId = Shader.PropertyToID("_CozyPrevViewProjMatrix");
        private static readonly int kHistoryWeightPropertyId = Shader.PropertyToID("_HistoryWeight");

        private class VolumetricPassData
        {
            public Material material;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle historyTexture;
            public TextureHandle depthHistoryTexture;
            public TextureHandle fogTexture;
            public TextureHandle skyTexture;
            public Matrix4x4[] prevViewProjMatrices;
            public float historyWeight;
        }

        private class MainPassData
        {
            public Material material;
            public MaterialPropertyBlock propertyBlock;
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
            var cameraData = frameData.Get<UniversalCameraData>();
            var cozyData = frameData.GetOrCreate<CozyFrameData>();
            TextureHandle clearTexture = CozyRenderGraphResources.GetClearTexture(renderGraph, cameraData);
            cozyData.volumetricCloudBuffer = clearTexture;
            cozyData.volumetricCloudDepthBuffer = clearTexture;
            cozyData.cloudBuffer = clearTexture;

            if (!CozyPreferences.CurrentStyle.UseClouds) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
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
                bool xrEnabled = cameraData.xr.enabled;
                bool xrSinglePass = xrEnabled && cameraData.xr.singlePassEnabled;
                int eyeIndex = xrEnabled && !xrSinglePass ? cameraData.xr.multipassId : 0;
                int renderingMode = !xrEnabled ? 0 : xrSinglePass ? 1 : 2;
                CozyCloudsHistory cloudsHistory = null;
                if (eyeIndex < 0 || eyeIndex >= 2)
                {
                    Debug.LogWarning($"COZY cloud history does not support XR multipass eye index {eyeIndex}; using scratch textures.", currentCamera);
                }
                else if (cameraData.historyManager != null)
                {
                    cameraData.historyManager.RequestAccess<CozyCloudsHistory>();
                    cloudsHistory = cameraData.historyManager.GetHistoryForWrite<CozyCloudsHistory>();
                }

                if (cloudsHistory != null)
                {
                    cloudsHistory.Update(
                        eyeIndex,
                        renderingMode,
                        targetWidth,
                        targetHeight,
                        cameraDesc.dimension,
                        cameraDesc.slices,
                        cameraData.cameraTargetDescriptor);
                        
                    var clearParams = new ImportResourceParams
                    {
                        clearOnFirstUse = true,
                        clearColor = Color.clear,
                        discardOnLastUse = false 
                    };

                    historyWrite = renderGraph.ImportTexture(cloudsHistory.CurrentColor(eyeIndex), clearParams);
                    historyDepthWrite = renderGraph.ImportTexture(cloudsHistory.CurrentDepth(eyeIndex), clearParams);
                    historyRead = renderGraph.ImportTexture(cloudsHistory.PreviousColor(eyeIndex));
                    historyDepthRead = renderGraph.ImportTexture(cloudsHistory.PreviousDepth(eyeIndex));
                }
                else
                {
                    var scratchColorDesc = cameraDesc;
                    scratchColorDesc.width = targetWidth;
                    scratchColorDesc.height = targetHeight;
                    scratchColorDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                    scratchColorDesc.depthBufferBits = 0;
                    scratchColorDesc.msaaSamples = MSAASamples.None;
                    scratchColorDesc.clearBuffer = true;
                    scratchColorDesc.clearColor = Color.clear;
                    scratchColorDesc.name = "ScratchVolumetricCloudsTexture";

                    var scratchDepthDesc = scratchColorDesc;
                    scratchDepthDesc.depthBufferBits = DepthBits.Depth32;
                    scratchDepthDesc.name = "ScratchVolumetricCloudsDepth";

                    historyWrite = renderGraph.CreateTexture(scratchColorDesc);
                    historyDepthWrite = renderGraph.CreateTexture(scratchDepthDesc);
                    historyRead = renderGraph.CreateTexture(scratchColorDesc);
                    historyDepthRead = renderGraph.CreateTexture(scratchDepthDesc);
                }

                int viewCount = xrEnabled ? Mathf.Clamp(cameraData.xr.viewCount, 1, 2) : 1;
                Matrix4x4[] currentViewProjMatrices = new Matrix4x4[2];
                for (int viewIndex = 0; viewIndex < viewCount; viewIndex++)
                {
                    Matrix4x4 viewMatrix = cameraData.GetViewMatrix(viewIndex);
                    Matrix4x4 projectionMatrix = cameraData.GetProjectionMatrix(viewIndex);
                    currentViewProjMatrices[viewIndex] = GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
                }

                if (viewCount == 1)
                    currentViewProjMatrices[1] = currentViewProjMatrices[0];

                Vector3 currentCamPos = currentCamera.transform.position;
                Quaternion currentCamRot = currentCamera.transform.rotation;
                if (xrEnabled)
                {
                    Matrix4x4 inverseView = cameraData.GetViewMatrix(0).inverse;
                    currentCamPos = inverseView.GetColumn(3);
                    currentCamRot = Quaternion.LookRotation(-inverseView.GetColumn(2), inverseView.GetColumn(1));
                }

                Matrix4x4[] prevViewProjMatrices = (Matrix4x4[])currentViewProjMatrices.Clone();
                Vector3 prevCamPos = currentCamPos;
                Quaternion prevCamRot = currentCamRot;
                bool hasPrevCameraFrame = false;
                bool hasTime = CozyWeather.Instance.Time != null;
                float currentTime = hasTime ? CozyWeather.Instance.Time.CurrentTime : 0f;
                float timeMovement = 0f;
                if (cloudsHistory != null)
                {
                    hasPrevCameraFrame = cloudsHistory.TryGetPreviousFrame(
                        eyeIndex, out Vector3 previousPosition, out Quaternion previousRotation,
                        out float previousTime, out bool previousHasTime, out Matrix4x4[] previousMatrices);
                    if (hasPrevCameraFrame)
                    {
                        prevCamPos = previousPosition;
                        prevCamRot = previousRotation;
                        System.Array.Copy(previousMatrices, prevViewProjMatrices, prevViewProjMatrices.Length);
                        if (hasTime && previousHasTime)
                            timeMovement = Mathf.Abs(previousTime - currentTime);
                    }
                    cloudsHistory.RecordCurrentFrame(eyeIndex, currentCamPos, currentCamRot, currentTime, hasTime, currentViewProjMatrices);
                }

                float posMovement = Vector3.Distance(currentCamPos, prevCamPos);
                float rotMovement = Quaternion.Angle(currentCamRot, prevCamRot);

                float currentHistoryWeight = 0.8f;
                if (!hasPrevCameraFrame || posMovement > positionThreshold ||
                    rotMovement > (xrEnabled ? 5f : rotationThreshold) || timeMovement > timeThreshold)
                {
                    currentHistoryWeight = 0.0f;
                }

                using (var builder = renderGraph.AddRasterRenderPass<VolumetricPassData>("Cozy Compute Volumetric Clouds Pass", out var passData))
                {
                    cozyData.volumetricCloudBuffer = historyWrite;
                    cozyData.volumetricCloudDepthBuffer = historyDepthWrite;

                    passData.material = volumetricMaterial;
                    passData.propertyBlock = m_SharedPropertyBlock;
                    passData.fogTexture = cozyData.fogBuffer.IsValid()
                        ? cozyData.fogBuffer
                        : clearTexture;
                    passData.skyTexture = cozyData.skyBuffer.IsValid()
                        ? cozyData.skyBuffer
                        : clearTexture;
                    passData.historyTexture = historyRead;
                    passData.depthHistoryTexture = historyDepthRead;
                    passData.prevViewProjMatrices = prevViewProjMatrices;
                    passData.historyWeight = currentHistoryWeight;

                    builder.UseTexture(passData.fogTexture, AccessFlags.Read);
                    builder.UseTexture(passData.skyTexture, AccessFlags.Read);

                    builder.UseTexture(passData.historyTexture, AccessFlags.Read);
                    builder.UseTexture(passData.depthHistoryTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(historyWrite, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(historyDepthWrite, AccessFlags.Write);

                    if (cloudsHistory != null)
                        builder.AllowPassCulling(false);
                    builder.SetRenderFunc<VolumetricPassData>(ExecuteVolumetricsPass);
                }
            }
            else
            {
                cozyData.volumetricCloudBuffer = clearTexture;
                cozyData.volumetricCloudDepthBuffer = clearTexture;
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
                    passData.fogTexture = cozyData.fogBuffer.IsValid()
                        ? cozyData.fogBuffer
                        : clearTexture;
                    passData.skyTexture = cozyData.skyBuffer.IsValid()
                        ? cozyData.skyBuffer
                        : clearTexture;

                    builder.UseTexture(passData.fogTexture, AccessFlags.Read);
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

            if (data.fogTexture.IsValid())
                data.propertyBlock.SetTexture(kFogTexPropertyId, data.fogTexture);

            if (data.skyTexture.IsValid())
                data.propertyBlock.SetTexture(kSkyTexPropertyId, data.skyTexture);

            if (data.historyTexture.IsValid())
                data.propertyBlock.SetTexture(kHistoryTexPropertyId, data.historyTexture);

            if (data.depthHistoryTexture.IsValid())
                data.propertyBlock.SetTexture(kDepthHistoryTexPropertyId, data.depthHistoryTexture);

            data.propertyBlock.SetMatrixArray(kPrevViewProjMatrixId, data.prevViewProjMatrices);
            data.propertyBlock.SetFloat(kHistoryWeightPropertyId, data.historyWeight);

            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }

        private static void ExecuteRenderCloudsPass(MainPassData data, RasterGraphContext context)
        {
            data.propertyBlock.Clear();

            if (data.fogTexture.IsValid())
                data.propertyBlock.SetTexture(kFogTexPropertyId, data.fogTexture);

            if (data.skyTexture.IsValid())
                data.propertyBlock.SetTexture(kSkyTexPropertyId, data.skyTexture);

            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }
    }
}