//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DistantLands.Cozy
{
    public class CozySunshaftsRenderPass : ScriptableRenderPass
    {
        public Material material;
        public Material maskMaterial;

        // Passed by reference to the structs to avoid static state leakage
        private MaterialPropertyBlock m_SharedPropertyBlock = new();

        // Cached property IDs for better performance
        private static readonly int kCozySunshaftsMaskTexId = Shader.PropertyToID("_CozySunshaftsMaskTex");
        private static readonly int kVolumetricCloudDepthTexId = Shader.PropertyToID("_VolumetricCloudDepthTex");
        private static readonly int kCloudTexId = Shader.PropertyToID("_CloudTex");

        public CozySunshaftsRenderPass(Material material, Material maskMaterial)
        {
            this.material = material;
            this.maskMaterial = maskMaterial;
        }

        private class MaskPassData
        {
            public Material material;
            public TextureHandle cloudTexture;
            public TextureHandle volumetricCloudTexture;
            public MaterialPropertyBlock propertyBlock;
        }

        private class RadialPassData
        {
            public Material material;
            public TextureHandle maskTexture;
            public MaterialPropertyBlock propertyBlock;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!CozyPreferences.CurrentStyle.UseSunshafts) return;
            if (!CozyPreferences.CurrentStyle.UseSunshaftsScreenspace) return;

            var data = frameData.GetOrCreate<CozyFrameData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            TextureHandle clearTexture = CozyRenderGraphResources.GetClearTexture(renderGraph, cameraData);
            
            if (!resourceData.cameraColor.IsValid()) return;
            var cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);

            var maskDesc = cameraDesc;
            maskDesc.name = "COZYSunshaftsMaskBuffer";
            maskDesc.colorFormat = GraphicsFormat.R16_SFloat;
            TextureHandle maskHandle = renderGraph.CreateTexture(maskDesc);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("COZY Sunshafts Mask", out var maskPassData))
            {
                maskPassData.cloudTexture = data.cloudBuffer.IsValid()
                    ? data.cloudBuffer
                    : clearTexture;
                maskPassData.volumetricCloudTexture = data.volumetricCloudBuffer.IsValid()
                    ? data.volumetricCloudBuffer
                    : clearTexture;
                maskPassData.material = maskMaterial;
                maskPassData.propertyBlock = m_SharedPropertyBlock;

                builder.UseTexture(maskPassData.cloudTexture, AccessFlags.Read);
                builder.UseTexture(maskPassData.volumetricCloudTexture, AccessFlags.Read);

                if (resourceData.cameraDepthTexture.IsValid())
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (MaskPassData passData, RasterGraphContext context) => RenderSunshaftsMask(passData, context.cmd));
            }

            var desc = cameraDesc;
            desc.name = "COZYSunshaftsBuffer";
            desc.colorFormat = GraphicsFormat.R32G32B32A32_SFloat;
            TextureHandle sunshaftsHandle = renderGraph.CreateTexture(desc);
            data.sunShaftsBuffer = sunshaftsHandle;

            using (var builder = renderGraph.AddRasterRenderPass<RadialPassData>("COZY Sunshafts Radial", out var radialPassData))
            {
                radialPassData.maskTexture = maskHandle;
                radialPassData.material = material;
                radialPassData.propertyBlock = m_SharedPropertyBlock;

                builder.UseTexture(radialPassData.maskTexture, AccessFlags.Read);
                builder.SetRenderAttachment(sunshaftsHandle, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (RadialPassData passData, RasterGraphContext context) => RenderSunshafts(passData, context.cmd));
            }
        }

        static void RenderSunshaftsMask(MaskPassData data, RasterCommandBuffer cmd)
        {
            data.propertyBlock.Clear();

            if (data.volumetricCloudTexture.IsValid())
            {
                data.propertyBlock.SetTexture(kVolumetricCloudDepthTexId, data.volumetricCloudTexture);
            }
            if (data.cloudTexture.IsValid())
            {
                data.propertyBlock.SetTexture(kCloudTexId, data.cloudTexture);
            }

            cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }

        static void RenderSunshafts(RadialPassData data, RasterCommandBuffer cmd)
        {
            data.propertyBlock.Clear();

            if (data.maskTexture.IsValid())
            {
                data.propertyBlock.SetTexture(kCozySunshaftsMaskTexId, data.maskTexture);
            }

            cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.propertyBlock);
        }
    }
}