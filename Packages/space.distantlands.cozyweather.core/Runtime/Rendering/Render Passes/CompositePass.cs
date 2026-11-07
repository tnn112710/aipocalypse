//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;


namespace DistantLands.Cozy
{
    public class CozyCompositePass : ScriptableRenderPass
    {
        private Material material;
        private static readonly int _BlitTexID = Shader.PropertyToID("_BlitTex");
        private static readonly int _SkyTexID = Shader.PropertyToID("_SkyTex");
        private static readonly int _CloudTexID = Shader.PropertyToID("_CloudTex");
        private static readonly int _VolumetricCloudTexID = Shader.PropertyToID("_VolumetricCloudTex");
        private static readonly int _VolumetricCloudDepthTexID = Shader.PropertyToID("_VolumetricCloudDepthTex");
        private static readonly int _SunshaftsTexID = Shader.PropertyToID("_SunshaftsTex");
        private static readonly int _FogTexID = Shader.PropertyToID("_FogTex");
        private static readonly int _BeforeCloudsTexID = Shader.PropertyToID("_BeforeCloudsTex");
        private static readonly int _AfterCloudsTexID = Shader.PropertyToID("_AfterCloudsTex");

        public CozyCompositePass(Material material) => this.material = material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            
            if (!resourceData.cameraColor.IsValid()) return;

            var cozyData = frameData.GetOrCreate<CozyFrameData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Cozy Composite", out var passData))
            {
                TextureHandle cloudTex = cozyData.cloudBuffer.IsValid()
                    ? cozyData.cloudBuffer
                    : renderGraph.defaultResources.blackTexture;
                builder.UseTexture(cloudTex, AccessFlags.Read);

                TextureHandle sunshaftsTex = cozyData.sunShaftsBuffer.IsValid()
                    ? cozyData.sunShaftsBuffer
                    : renderGraph.defaultResources.blackTexture;
                builder.UseTexture(sunshaftsTex, AccessFlags.Read);

                TextureHandle volumetricCloudTex = cozyData.volumetricCloudBuffer.IsValid()
                    ? cozyData.volumetricCloudBuffer
                    : renderGraph.defaultResources.blackTexture;
                builder.UseTexture(volumetricCloudTex, AccessFlags.Read);

                TextureHandle volumetricCloudDepthTex = cozyData.volumetricCloudDepthBuffer.IsValid()
                    ? cozyData.volumetricCloudDepthBuffer
                    : renderGraph.defaultResources.whiteTexture;
                builder.UseTexture(volumetricCloudDepthTex, AccessFlags.Read);

                TextureHandle skyTex = cozyData.skyBuffer.IsValid()
                    ? cozyData.skyBuffer
                    : renderGraph.defaultResources.clearTextureXR;
                builder.UseTexture(skyTex, AccessFlags.Read);

                TextureHandle fogTex = cozyData.fogBuffer.IsValid()
                    ? cozyData.fogBuffer
                    : renderGraph.defaultResources.blackTexture;
                builder.UseTexture(fogTex, AccessFlags.Read);

                TextureHandle beforeCloudsTex = cozyData.beforeCloudsBuffer.IsValid()
                    ? cozyData.beforeCloudsBuffer
                    : renderGraph.defaultResources.clearTextureXR;

                TextureHandle afterCloudsTex = cozyData.afterCloudsBuffer.IsValid()
                    ? cozyData.afterCloudsBuffer
                    : renderGraph.defaultResources.clearTextureXR;

                builder.UseTexture(beforeCloudsTex, AccessFlags.Read);
                builder.UseTexture(afterCloudsTex, AccessFlags.Read);

                var desc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                desc.name = "_CompositeTarget";
                TextureHandle destination = renderGraph.CreateTexture(desc);

                passData.material = material;
                passData.inputTexture = resourceData.cameraColor;
                passData.skyTexture = skyTex;
                passData.cloudTexture = cloudTex;
                passData.volumetricCloudTexture = volumetricCloudTex;
                passData.sunshaftsTexture = sunshaftsTex;
                passData.volumetricCloudDepthTexture = volumetricCloudDepthTex;
                passData.fogTexture = fogTex;
                passData.beforeCloudsTexture = beforeCloudsTex;
                passData.afterCloudsTexture = afterCloudsTex;

                builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                // Set the attachments
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(_BlitTexID, data.inputTexture);
                    data.material.SetTexture(_SkyTexID, data.skyTexture);
                    data.material.SetTexture(_CloudTexID, data.cloudTexture);
                    data.material.SetTexture(_VolumetricCloudTexID, data.volumetricCloudTexture);
                    data.material.SetTexture(_VolumetricCloudDepthTexID, data.volumetricCloudDepthTexture);
                    data.material.SetTexture(_SunshaftsTexID, data.sunshaftsTexture);
                    data.material.SetTexture(_FogTexID, data.fogTexture);
                    data.material.SetTexture(_BeforeCloudsTexID, data.beforeCloudsTexture);
                    data.material.SetTexture(_AfterCloudsTexID, data.afterCloudsTexture);
                    
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3);
                });

                resourceData.cameraColor = destination;
            }
        }

        private class PassData
        {
            public Material material;
            public TextureHandle inputTexture;
            public TextureHandle skyTexture;
            public TextureHandle cloudTexture;
            public TextureHandle volumetricCloudTexture;
            public TextureHandle volumetricCloudDepthTexture;
            public TextureHandle sunshaftsTexture;
            public TextureHandle fogTexture;
            public TextureHandle beforeCloudsTexture;
            public TextureHandle afterCloudsTexture;
        }
    }
}