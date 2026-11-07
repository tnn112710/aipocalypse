//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozyRenderObjectsPass : ScriptableRenderPass
    {
        private readonly List<ShaderTagId> m_ShaderTagIds;
        private readonly FilteringSettings m_FilteringSettings;
        private readonly RenderQueueRange m_RenderQueueRange;
        private readonly string m_PassName;
        private readonly bool m_AfterClouds;

        private class PassData
        {
            public RendererListHandle rendererListHandle;
        }

        public CozyRenderObjectsPass(
            string passName,
            LayerMask layerMask,
            bool afterClouds,
            RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox,
            RenderQueueType renderQueueType = RenderQueueType.Transparent,
            int renderingLayerMask = -1)
        {
            m_PassName = passName;
            this.renderPassEvent = renderPassEvent;

            m_RenderQueueRange = RenderQueueRange.all;

            m_FilteringSettings = new FilteringSettings(m_RenderQueueRange, layerMask);
            if (renderingLayerMask != -1)
                m_FilteringSettings.renderingLayerMask = (uint)renderingLayerMask;

            m_ShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };

            m_AfterClouds = afterClouds;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cozyData = frameData.GetOrCreate<CozyFrameData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

#if UNITY_EDITOR
            if (cameraData.isSceneViewCamera && !SceneView.lastActiveSceneView.sceneViewState.showSkybox)
                return;
#endif

            var sortingCriteria = SortingCriteria.RenderQueue;

            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                m_ShaderTagIds, renderingData, cameraData, lightData, sortingCriteria);

            var rendererListParams = new RendererListParams(
                renderingData.cullResults, drawingSettings, m_FilteringSettings);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName, out var passData))
            {
                if (!resourceData.cameraColor.IsValid()) return;

                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParams);
                builder.UseRendererList(passData.rendererListHandle);

                var cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);

                var desc = cameraDesc;
                if (m_AfterClouds)
                    desc.name = "COZYAfterCloudsObjectsBuffer";
                else
                    desc.name = "COZYBeforeCloudsObjectsBuffer";
                desc.width = Mathf.Max(1, cameraDesc.width);
                desc.height = Mathf.Max(1, cameraDesc.height);
                desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                desc.clearBuffer = true;
                desc.clearColor = new Color(0, 0, 0, 0);

                TextureHandle renderHandle = renderGraph.CreateTexture(desc);

                if (m_AfterClouds)
                    cozyData.afterCloudsBuffer = renderHandle;
                else
                    cozyData.beforeCloudsBuffer = renderHandle;

                builder.SetRenderAttachment(renderHandle, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.rendererListHandle);
                });
            }
        }
    }
}