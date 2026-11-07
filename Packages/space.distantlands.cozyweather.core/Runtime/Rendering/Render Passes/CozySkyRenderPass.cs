//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozySkyRenderPass : ScriptableRenderPass
    {
        private Material material;

        private class MainPassData
        {
            public Material material;
            public TextureHandle inputTexture;
        }

        public CozySkyRenderPass(Material material)
        {
            this.material = material;
        }

        private static void ExecuteMainPass(RasterCommandBuffer cmd, Material material)
        {
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
        }


        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.cameraColor.IsValid()) return;
            var cozyData = frameData.GetOrCreate<CozyFrameData>();
            
#if UNITY_EDITOR
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.isSceneViewCamera && !SceneView.lastActiveSceneView.sceneViewState.showSkybox)
                return;
#endif

            if (!CozyPreferences.CurrentStyle.UseSky) return;

            using (var builder = renderGraph.AddRasterRenderPass<MainPassData>("Cozy Sky Pass", out var passData))
            {
                var cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);

                var desc = cameraDesc;
                desc.name = "COZYSkyBuffer";
                desc.width = Mathf.Max(1, cameraDesc.width);
                desc.height = Mathf.Max(1, cameraDesc.height);
                desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;

                TextureHandle skyHandle = renderGraph.CreateTexture(desc);
                cozyData.skyBuffer = skyHandle;

                passData.material = material;
                passData.inputTexture = resourceData.cameraColor;

                builder.SetRenderAttachment(skyHandle, 0, AccessFlags.Write);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc((MainPassData data, RasterGraphContext context) =>
                {
                    ExecuteMainPass(context.cmd, data.material);
                });
            }
        }
    }
}