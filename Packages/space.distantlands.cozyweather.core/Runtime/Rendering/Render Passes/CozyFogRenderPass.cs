//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy
{
    public class CozyFogVolumeHistory : CameraHistoryItem
    {
        private int m_Id;
        private Hash128 m_DescKey;

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            m_Id = MakeId(0);
        }

        public override void Reset()
        {
            ReleaseHistoryFrameRT(m_Id);
        }

        public RTHandle currentVolume => GetCurrentFrameRT(m_Id);
        public RTHandle previousVolume => GetPreviousFrameRT(m_Id);

        public void Update(RenderTextureDescriptor desc)
        {
            Hash128 key = Hash128.Compute(ref desc);
            if (currentVolume == null || m_DescKey != key)
            {
                ReleaseHistoryFrameRT(m_Id);
                AllocHistoryFrameRT(m_Id, 2, ref desc, "CozyFogVolumeHistory");
                m_DescKey = key;
            }
        }
    }

    public class CozyFogRenderPass : ScriptableRenderPass
    {
        public Material material;
        public ComputeShader accumulateShader;
        public ComputeShader integrationShader;

        private int m_InjectKernel;
        private int m_IntegrateKernel;

        public static Vector3Int volumeResolution = new(160, 90, 240);

        private MaterialPropertyBlock m_SharedPropertyBlock = new();
        private static readonly int s_FogVolumePropertyId = Shader.PropertyToID("CZY_FogVolume");
        private static readonly int s_TemporalWeightId = Shader.PropertyToID("CZY_TemporalWeight");
        private static readonly int s_InvViewProjId = Shader.PropertyToID("CZY_Base_Camera_InverseViewProjection");
        private static readonly int s_InvProjId = Shader.PropertyToID("CZY_Base_Camera_InverseProjection");
        private static readonly int s_CamToWorldId = Shader.PropertyToID("CZY_Base_Camera_CameraToWorldMatrix");
        private static readonly int s_CamWorldPosId = Shader.PropertyToID("CZY_Base_Camera_CameraWorldPos");
        private static readonly int s_NearSizeId = Shader.PropertyToID("CZY_Base_Camera_NearSize");
        private static readonly int s_FarSizeId = Shader.PropertyToID("CZY_Base_Camera_FarSize");
        private static readonly int s_VolumeSizeId = Shader.PropertyToID("CZY_FogVolumeSize");
        private static readonly int s_VolumeFroxelSizeId = Shader.PropertyToID("CZY_FogVolumeFroxelSize");
        private static readonly int s_MaxDistanceId = Shader.PropertyToID("CZY_Fog_Volumetric_MaxDistance");
        private static readonly int s_GameTimeId = Shader.PropertyToID("CZY_GameTime");
        private static readonly int s_PrevViewMatrixId = Shader.PropertyToID("CZY_PrevViewMatrix");
        private static readonly int s_PrevCameraPosId = Shader.PropertyToID("CZY_PrevCameraPos");
        private static readonly int s_FogVolumePrevId = Shader.PropertyToID("CZY_FogVolumePrevious");
        private static readonly int s_FogIntegrationId = Shader.PropertyToID("CZY_FogIntegration");
        private static readonly int s_FogAccumulationId = Shader.PropertyToID("CZY_FogAccumulation");

        public CozyFogRenderPass(Material material, ComputeShader accumulate, ComputeShader integrationShader)
        {
            this.material = material;
            this.integrationShader = integrationShader;
            accumulateShader = accumulate;

            if (accumulateShader != null)
                m_InjectKernel = accumulateShader.FindKernel("CSMain");

            if (this.integrationShader != null)
                m_IntegrateKernel = 0;
        }

        public void Dispose() { }

        private class FogPassData
        {
            public Material material;
            public ComputeShader accumulateShader;
            public ComputeShader integrationShader;
            public int injectKernel;
            public int integrateKernel;

            public TextureHandle accumulateVolume;
            public TextureHandle historyVolume;
            public TextureHandle integrateVolume;
            public MaterialPropertyBlock propertyBlock;

            public Matrix4x4 prevViewMatrix;
            public Vector3 prevCamPos;
            public bool useTemporalHistory;

            public Vector3 cameraPos;
            public Vector2 fogNearSize;
            public Vector2 fogFarSize;
            public float fogDistance;
            public float time;
            public Matrix4x4 invViewProjMatrix;
            public Matrix4x4 invProjMatrix;
            public Matrix4x4 cameraToWorldMatrix;

            public float positionDelta;
            public float rotationDelta;

            public int dispatchX;
            public int dispatchY;
            public int dispatchZ;
            public Vector4 volumeSize;
            public Vector4 volumeFroxelSize;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var data = frameData.GetOrCreate<CozyFrameData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;

            if (!resourceData.cameraColor.IsValid()) return;

#if UNITY_EDITOR
            if (cameraData.isSceneViewCamera && !SceneView.lastActiveSceneView.sceneViewState.showFog)
                return;
#endif

            if (!SystemInfo.supportsComputeShaders)
                Debug.LogWarning("Device does not support compute shaders. COZY will not render volumetric fog.");
            else if (CozyPreferences.CurrentStyle.UseFogVolumetric)
            {
                RenderTextureDescriptor volDesc = new RenderTextureDescriptor(volumeResolution.x, volumeResolution.y)
                {
                    dimension = TextureDimension.Tex3D,
                    volumeDepth = volumeResolution.z,
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true,
                    msaaSamples = 1,
                    depthBufferBits = 0
                };

                TextureDesc scratchVolDesc = new(volumeResolution.x, volumeResolution.y)
                {
                    slices = volumeResolution.z,
                    dimension = TextureDimension.Tex3D,
                    colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true,
                    msaaSamples = MSAASamples.None,
                    name = "ScratchAccumulateVolume"
                };

                TextureHandle currHistoryHandle;
                TextureHandle prevHistoryHandle;
                bool hasValidHistory = false;

                if (cameraData.historyManager != null)
                {
                    cameraData.historyManager.RequestAccess<CozyFogVolumeHistory>();
                    CozyFogVolumeHistory fogVolumeHistory = cameraData.historyManager.GetHistoryForWrite<CozyFogVolumeHistory>();

                    if (fogVolumeHistory != null)
                    {
                        fogVolumeHistory.Update(volDesc);
                        currHistoryHandle = renderGraph.ImportTexture(fogVolumeHistory.currentVolume);
                        prevHistoryHandle = renderGraph.ImportTexture(fogVolumeHistory.previousVolume);
                        hasValidHistory = true;
                    }
                    else
                    {
                        currHistoryHandle = renderGraph.CreateTexture(scratchVolDesc);
                        prevHistoryHandle = renderGraph.CreateTexture(scratchVolDesc);
                    }
                }
                else
                {
                    currHistoryHandle = renderGraph.CreateTexture(scratchVolDesc);
                    prevHistoryHandle = renderGraph.CreateTexture(scratchVolDesc);
                }

                Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
                Matrix4x4 projMatrix = camera.nonJitteredProjectionMatrix;
                Matrix4x4 gpuViewProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, true) * viewMatrix;

                Vector3 camPos = camera.transform.position;
                Quaternion camRot = camera.transform.rotation;
                Vector3 prevCamPos = camPos;
                Quaternion prevCamRot = camRot;
                Matrix4x4 prevViewMatrix = viewMatrix;
                bool hasPrevCameraFrame = false;

                if (cameraData.historyManager != null)
                {
                    cameraData.historyManager.RequestAccess<CozyCameraMotionHistory>();
                    CozyCameraMotionHistory cameraMotion = cameraData.historyManager.GetHistoryForWrite<CozyCameraMotionHistory>();

                    if (cameraMotion != null)
                    {
                        hasPrevCameraFrame = cameraMotion.TryGetPreviousFrame(out prevCamPos, out prevCamRot, out prevViewMatrix, out _);
                        cameraMotion.RecordCurrentFrame(camera, viewMatrix, gpuViewProjMatrix);
                    }
                }

                using (var builder = renderGraph.AddComputePass("COZY Fog Compute", out FogPassData passData))
                {
                    passData.accumulateShader = accumulateShader;
                    passData.integrationShader = integrationShader;
                    passData.injectKernel = m_InjectKernel;
                    passData.integrateKernel = m_IntegrateKernel;

                    passData.prevViewMatrix = prevViewMatrix;
                    passData.prevCamPos = prevCamPos;
                    passData.useTemporalHistory = hasValidHistory && hasPrevCameraFrame;

                    passData.cameraPos = camPos;
                    passData.positionDelta = Vector3.Distance(camPos, prevCamPos);
                    passData.rotationDelta = Quaternion.Angle(camRot, prevCamRot);
                    passData.time = Time.time;
                    passData.fogDistance = 500f;

                    float nearClip = camera.nearClipPlane;
                    float farClip = camera.farClipPlane;

                    Vector3 n0 = camera.ViewportToWorldPoint(new Vector3(0f, 0f, nearClip));
                    Vector3 n1 = camera.ViewportToWorldPoint(new Vector3(1f, 0f, nearClip));
                    Vector3 n2 = camera.ViewportToWorldPoint(new Vector3(0f, 1f, nearClip));
                    Vector3 f0 = camera.ViewportToWorldPoint(new Vector3(0f, 0f, farClip));
                    Vector3 f1 = camera.ViewportToWorldPoint(new Vector3(1f, 0f, farClip));
                    Vector3 f2 = camera.ViewportToWorldPoint(new Vector3(0f, 1f, farClip));

                    Vector2 frustumNearSize = new Vector2(Vector3.Distance(n1, n0), Vector3.Distance(n2, n0)) * 0.5f;
                    Vector2 frustumFarSize = new Vector2(Vector3.Distance(f1, f0), Vector3.Distance(f2, f0)) * 0.5f;

                    passData.fogNearSize = frustumNearSize;
                    passData.fogFarSize = Vector2.Lerp(frustumNearSize, frustumFarSize, (passData.fogDistance - nearClip) / (farClip - nearClip));

                    passData.invViewProjMatrix = (projMatrix * viewMatrix).inverse;
                    passData.invProjMatrix = projMatrix.inverse;
                    passData.cameraToWorldMatrix = camera.cameraToWorldMatrix;

                    passData.dispatchX = Mathf.CeilToInt(volumeResolution.x * 0.125f);
                    passData.dispatchY = Mathf.CeilToInt(volumeResolution.y * 0.125f);
                    passData.dispatchZ = Mathf.CeilToInt(volumeResolution.z * 0.125f);

                    passData.volumeSize = new Vector4(volumeResolution.x, volumeResolution.y, volumeResolution.z, 0);
                    passData.volumeFroxelSize = new Vector4(1f / volumeResolution.x, 1f / volumeResolution.y, 1f / volumeResolution.z, 0);

                    TextureDesc volTexDesc = new(volumeResolution.x, volumeResolution.y)
                    {
                        name = "IntegrateFogVolume",
                        slices = volumeResolution.z,
                        dimension = TextureDimension.Tex3D,
                        colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                        enableRandomWrite = true,
                        msaaSamples = MSAASamples.None
                    };

                    TextureHandle volumeIntegrateHandle = renderGraph.CreateTexture(volTexDesc);

                    data.fogAccumulateVolume = currHistoryHandle;
                    data.fogIntegrateVolume = volumeIntegrateHandle;

                    passData.accumulateVolume = currHistoryHandle;
                    passData.historyVolume = prevHistoryHandle;
                    passData.integrateVolume = volumeIntegrateHandle;

                    builder.UseTexture(passData.accumulateVolume, AccessFlags.Write);
                    builder.UseTexture(passData.historyVolume, AccessFlags.Read);
                    builder.UseTexture(passData.integrateVolume, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (FogPassData passData, ComputeGraphContext cgContext) => ComputeFog(passData, cgContext));
                }

            }

            using (var builder = renderGraph.AddRasterRenderPass("COZY Fog Rendering", out FogPassData passData))
            {
                passData.material = material;
                passData.propertyBlock = m_SharedPropertyBlock;

                var cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                var desc = cameraDesc;
                desc.name = "COZYFogBuffer";
                desc.width = Mathf.Max(1, (int)(cameraDesc.width / (float)CozyPreferences.FogDownsampling));
                desc.height = Mathf.Max(1, (int)(cameraDesc.height / (float)CozyPreferences.FogDownsampling));
                desc.colorFormat = GraphicsFormat.R32G32B32A32_SFloat;
                TextureHandle fogHandle = renderGraph.CreateTexture(desc);
                data.fogBuffer = fogHandle;

                if (data.fogIntegrateVolume.IsValid())
                {
                    passData.integrateVolume = data.fogIntegrateVolume;
                    builder.UseTexture(data.fogIntegrateVolume, AccessFlags.Read);
                }

                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(fogHandle, 0, AccessFlags.Write);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc(static (FogPassData passData, RasterGraphContext context) => RenderFog(passData, context.cmd));
            }
        }

        static void ComputeFog(FogPassData data, ComputeGraphContext cgContext)
        {
            if (data.accumulateShader == null || data.integrationShader == null) return;

            var cmd = cgContext.cmd;

            float finalWeight = 0f;
            if (data.useTemporalHistory)
            {
                float targetWeight = 0.9f;
                float velocityPenalty = Mathf.Clamp01(data.positionDelta * 2.0f);
                float rotationPenalty = Mathf.Clamp01(data.rotationDelta * 2.05f);
                finalWeight = Mathf.Lerp(targetWeight, 0.05f, Mathf.Clamp01(velocityPenalty + rotationPenalty));
            }

            cmd.SetComputeFloatParam(data.accumulateShader, s_TemporalWeightId, finalWeight);

            // Accumulate Shader Params
            cmd.SetComputeMatrixParam(data.accumulateShader, s_InvViewProjId, data.invViewProjMatrix);
            cmd.SetComputeMatrixParam(data.accumulateShader, s_InvProjId, data.invProjMatrix);
            cmd.SetComputeMatrixParam(data.accumulateShader, s_CamToWorldId, data.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(data.accumulateShader, s_CamWorldPosId, data.cameraPos);
            cmd.SetComputeVectorParam(data.accumulateShader, s_NearSizeId, data.fogNearSize);
            cmd.SetComputeVectorParam(data.accumulateShader, s_FarSizeId, data.fogFarSize);
            cmd.SetComputeVectorParam(data.accumulateShader, s_VolumeSizeId, data.volumeSize);
            cmd.SetComputeVectorParam(data.accumulateShader, s_VolumeFroxelSizeId, data.volumeFroxelSize);
            cmd.SetComputeFloatParam(data.accumulateShader, s_MaxDistanceId, data.fogDistance);
            cmd.SetComputeFloatParam(data.accumulateShader, s_GameTimeId, data.time);
            cmd.SetComputeMatrixParam(data.accumulateShader, s_PrevViewMatrixId, data.prevViewMatrix);
            cmd.SetComputeVectorParam(data.accumulateShader, s_PrevCameraPosId, data.prevCamPos);
            cmd.SetComputeTextureParam(data.accumulateShader, data.injectKernel, s_FogVolumePrevId, data.historyVolume);

            // Integration Shader Params
            cmd.SetComputeMatrixParam(data.integrationShader, s_InvViewProjId, data.invViewProjMatrix);
            cmd.SetComputeMatrixParam(data.integrationShader, s_InvProjId, data.invProjMatrix);
            cmd.SetComputeMatrixParam(data.integrationShader, s_CamToWorldId, data.cameraToWorldMatrix);
            cmd.SetComputeVectorParam(data.integrationShader, s_CamWorldPosId, data.cameraPos);
            cmd.SetComputeVectorParam(data.integrationShader, s_NearSizeId, data.fogNearSize);
            cmd.SetComputeVectorParam(data.integrationShader, s_FarSizeId, data.fogFarSize);
            cmd.SetComputeVectorParam(data.integrationShader, s_VolumeSizeId, data.volumeSize);
            cmd.SetComputeFloatParam(data.integrationShader, s_MaxDistanceId, data.fogDistance);

            // Dispatches
            cmd.SetComputeTextureParam(data.accumulateShader, data.injectKernel, s_FogVolumePropertyId, data.accumulateVolume);
            cmd.DispatchCompute(data.accumulateShader, data.injectKernel, data.dispatchX, data.dispatchY, data.dispatchZ);

            cmd.SetComputeTextureParam(data.integrationShader, data.integrateKernel, s_FogIntegrationId, data.integrateVolume);
            cmd.SetComputeTextureParam(data.integrationShader, data.integrateKernel, s_FogAccumulationId, data.accumulateVolume);
            cmd.DispatchCompute(data.integrationShader, data.integrateKernel, data.dispatchX, data.dispatchY, data.dispatchZ);
        }

        static void RenderFog(FogPassData data, RasterCommandBuffer cmd)
        {
            data.propertyBlock.Clear();

            if (data.integrateVolume.IsValid())
            {
                data.propertyBlock.SetTexture(s_FogVolumePropertyId, data.integrateVolume);
            }

            cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }
    }
}