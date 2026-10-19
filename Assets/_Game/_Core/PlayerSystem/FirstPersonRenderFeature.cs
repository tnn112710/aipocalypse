using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Aipocalypse.Core.PlayerSystem
{
    // Draws first-person held models (the HeldWeapon layer) inside the world camera instead of a second, overlay camera,
    // which cost a whole extra culling and render-graph pass per frame. For a camera with a FirstPersonView it clears depth
    // (held models stay in front of walls) and draws with that view's lens; other cameras (Scene view, previews) draw the
    // layer normally. Needs the layer removed from the renderer's opaque/transparent layer masks and kept in the camera's
    // culling mask.
    public sealed class FirstPersonRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] LayerMask _layerMask;
        [SerializeField, Tooltip("Before post-processing, so the grade and bloom cover the held model too")]
        RenderPassEvent _passEvent = RenderPassEvent.AfterRenderingTransparents;

        FirstPersonPass _pass;

        public override void Create() => _pass = new FirstPersonPass();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_layerMask == 0 || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;
            _pass.renderPassEvent = _passEvent;
            _pass.LayerMask = _layerMask;
            renderer.EnqueuePass(_pass);
        }

        sealed class FirstPersonPass : ScriptableRenderPass
        {
            static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit")
            };

            public LayerMask LayerMask;

            public FirstPersonPass() => profilingSampler = new ProfilingSampler("First Person");

            sealed class PassData
            {
                public TextureHandle Color;
                public RendererListHandle Opaque;
                public RendererListHandle Transparent;
                public bool FirstPerson;
                public Matrix4x4 View;
                public Matrix4x4 Projection;
                public Matrix4x4 CameraProjection;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var resources = frameData.Get<UniversalResourceData>();

                using var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var data, profilingSampler);

                data.Color = resources.activeColorTexture;
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                if (resources.activeDepthTexture.IsValid())
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                if (resources.mainShadowsTexture.IsValid())
                    builder.UseTexture(resources.mainShadowsTexture);
                if (resources.additionalShadowsTexture.IsValid())
                    builder.UseTexture(resources.additionalShadowsTexture);

                var filtering = new FilteringSettings(RenderQueueRange.opaque, LayerMask);
                var opaque = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                data.Opaque = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, opaque, filtering));
                filtering.renderQueueRange = RenderQueueRange.transparent;
                var transparent = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                data.Transparent = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, transparent, filtering));
                builder.UseRendererList(data.Opaque);
                builder.UseRendererList(data.Transparent);
                builder.AllowGlobalStateModification(true);

                Camera camera = cameraData.camera;
                FirstPersonView firstPerson = null;
                data.FirstPerson = cameraData.isGameCamera && !cameraData.xr.enabled && camera.TryGetComponent(out firstPerson);
                if (data.FirstPerson)
                {
                    Rect pixels = camera.pixelRect;
                    data.View = cameraData.GetViewMatrix();
                    data.Projection = Matrix4x4.Perspective(firstPerson.GetFieldOfView(camera), pixels.width / pixels.height, firstPerson.NearClip, firstPerson.FarClip);
                    data.CameraProjection = cameraData.GetProjectionMatrix();
                }

                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    if (d.FirstPerson)
                    {
                        bool flip = context.GetTextureUVOrigin(d.Color) == TextureUVOrigin.BottomLeft;
                        cmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1f, 0);
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, d.View, GL.GetGPUProjectionMatrix(d.Projection, flip), false);
                        cmd.DrawRendererList(d.Opaque);
                        cmd.DrawRendererList(d.Transparent);
                        RenderingUtils.SetViewAndProjectionMatrices(cmd, d.View, GL.GetGPUProjectionMatrix(d.CameraProjection, flip), false);
                    }
                    else
                    {
                        cmd.DrawRendererList(d.Opaque);
                        cmd.DrawRendererList(d.Transparent);
                    }
                });
            }
        }
    }
}
