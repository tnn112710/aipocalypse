using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

namespace DistantLands.Cozy.Horizon
{
    [DisallowMultipleRendererFeature]
    public class HorizonFeature : ScriptableRendererFeature
    {
        HorizonRenderPass m_ScriptablePass;
        [SerializeField] private Material horizonMaterial;

        private CozyHorizonModule horizonModule;

        public override void Create()
        {
            if (CozyWeather.Instance)
                CozyWeather.Instance.GetModule(out horizonModule);

            if (!horizonModule)
            {
                Debug.LogWarning("Horizon pass will not render without a Horizon Module on the current weather sphere. Disabling for now.");
                return;
            }

            m_ScriptablePass = new HorizonRenderPass(horizonMaterial, horizonModule)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_ScriptablePass == null) return;
            renderer.EnqueuePass(m_ScriptablePass);
        }


        class HorizonRenderPass : ScriptableRenderPass
        {
            private Material material;
            CozyHorizonModule horizonModule;

            private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();
            public float renderScale = 1;

            private static readonly int InputBlitPropertyID = Shader.PropertyToID("_InputBlit");
            private static readonly int FogTexPropertyID = Shader.PropertyToID("_FogTex");
            private static readonly int LayerTypePropertyID = Shader.PropertyToID("_LayerType");
            private static readonly int TexturePropertyID = Shader.PropertyToID("_Texture");
            private static readonly int CubemapPropertyID = Shader.PropertyToID("_Cubemap");
            private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");
            private static readonly int FogAmountPropertyID = Shader.PropertyToID("_FogAmount");
            private static readonly int PlacementHeightPropertyID = Shader.PropertyToID("_PlacementHeight");
            private static readonly int VerticalScalePropertyID = Shader.PropertyToID("_VerticalScale");
            private static readonly int AnglePropertyID = Shader.PropertyToID("_Angle");
            private static readonly int TilingPropertyID = Shader.PropertyToID("_Tiling");
            private static readonly int AlphaPropertyID = Shader.PropertyToID("_Alpha");
            private static readonly int RowsPropertyID = Shader.PropertyToID("_Rows");
            private static readonly int ColumnsPropertyID = Shader.PropertyToID("_Columns");
            private static readonly int PitchPropertyID = Shader.PropertyToID("_Pitch");
            private static readonly int YawPropertyID = Shader.PropertyToID("_Yaw");
            private static readonly int RollPropertyID = Shader.PropertyToID("_Roll");
            private static readonly int ScalePropertyID = Shader.PropertyToID("_Scale");
            private static readonly int FrameratePropertyID = Shader.PropertyToID("_Framerate");

            private class PassData
            {
                public Material material;
                public TextureHandle beforeCloudsTextureHandle;
                public TextureHandle afterCloudsTextureHandle;
                public TextureHandle fogTextureHandle;
                public SortedList<int, HorizonLayerInstance> horizonLayers;
            }

            public HorizonRenderPass(Material material, CozyHorizonModule horizonModule)
            {
                this.material = material;
                this.horizonModule = horizonModule;
            }
            private static void ExecuteMainPass(PassData data, RasterGraphContext context, bool beforeClouds)
            {
                s_SharedPropertyBlock.Clear();

                TextureHandle textureHandle = beforeClouds ? data.beforeCloudsTextureHandle : data.afterCloudsTextureHandle;
                bool handleIsValid = textureHandle.IsValid();

                if (handleIsValid)
                    s_SharedPropertyBlock.SetTexture(InputBlitPropertyID, textureHandle);

                if (data.fogTextureHandle.IsValid())
                    s_SharedPropertyBlock.SetTexture(FogTexPropertyID, data.fogTextureHandle);

                var layers = data.horizonLayers;
                var values = layers.Values;
                int count = layers.Count;

                for (int i = count - 1; i >= 0; --i)
                {
                    var layerRef = values[i];
                    if (layerRef.layer == null) continue;

                    var layer = layerRef.layer;
                    if (layerRef.blend == 0 || layer == null) continue;

                    bool isBehindClouds = layer.placementLocation == CozyHorizonProfile.PlacementLocation.behindClouds;
                    if (isBehindClouds != beforeClouds) continue;
                    if (layer.alpha == 0) continue;

                    s_SharedPropertyBlock.SetFloat(LayerTypePropertyID, (float)layer.layerType);
                    if (layer.layerType == CozyHorizonProfile.LayerType.Cubemap)
                        s_SharedPropertyBlock.SetTexture(CubemapPropertyID, layer.texture);
                    else
                        s_SharedPropertyBlock.SetTexture(TexturePropertyID, layer.texture);
                    s_SharedPropertyBlock.SetColor(ColorPropertyID, layer.color);
                    s_SharedPropertyBlock.SetFloat(FogAmountPropertyID, layer.fogAmount);
                    s_SharedPropertyBlock.SetFloat(PlacementHeightPropertyID, layer.placementHeight);
                    s_SharedPropertyBlock.SetFloat(VerticalScalePropertyID, layer.verticalScale);
                    s_SharedPropertyBlock.SetFloat(AnglePropertyID, layer.angle);
                    s_SharedPropertyBlock.SetFloat(TilingPropertyID, layer.tiling);
                    s_SharedPropertyBlock.SetFloat(AlphaPropertyID, layer.alpha * layerRef.blend);
                    s_SharedPropertyBlock.SetFloat(RowsPropertyID, layer.rows);
                    s_SharedPropertyBlock.SetFloat(ColumnsPropertyID, layer.columns);
                    s_SharedPropertyBlock.SetFloat(PitchPropertyID, layer.pitch);
                    s_SharedPropertyBlock.SetFloat(YawPropertyID, layer.yaw);
                    s_SharedPropertyBlock.SetFloat(RollPropertyID, layer.roll);
                    s_SharedPropertyBlock.SetFloat(ScalePropertyID, layer.size);
                    s_SharedPropertyBlock.SetFloat(FrameratePropertyID, layer.framerate);

                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!horizonModule) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                var cozyData = frameData.GetOrCreate<CozyFrameData>();

                if (cozyData.beforeCloudsBuffer.IsValid())
                {
                    TextureHandle beforeCloudsTex = cozyData.beforeCloudsBuffer;
                    TextureHandle afterCloudsTex = cozyData.afterCloudsBuffer.IsValid()
                        ? cozyData.afterCloudsBuffer
                        : renderGraph.defaultResources.clearTextureXR;

                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Cozy Horizon (Before Clouds)", out var passData))
                    {
                        passData.material = material;
                        passData.beforeCloudsTextureHandle = beforeCloudsTex;
                        passData.afterCloudsTextureHandle = afterCloudsTex;
                        passData.fogTextureHandle = cozyData.fogBuffer;
                        passData.horizonLayers = horizonModule.ActiveLayers;

                        builder.SetRenderAttachment(beforeCloudsTex, 0, AccessFlags.ReadWrite);
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                        builder.UseTexture(afterCloudsTex, AccessFlags.Read);

                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            ExecuteMainPass(data, context, true);
                        });
                    }
                }

                if (cozyData.afterCloudsBuffer.IsValid())
                {
                    TextureHandle beforeCloudsTex = cozyData.beforeCloudsBuffer.IsValid()
                        ? cozyData.beforeCloudsBuffer
                        : renderGraph.defaultResources.clearTextureXR;
                    TextureHandle afterCloudsTex = cozyData.afterCloudsBuffer;

                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Cozy Horizon (After Clouds)", out var passData))
                    {
                        passData.material = material;
                        passData.beforeCloudsTextureHandle = beforeCloudsTex;
                        passData.afterCloudsTextureHandle = afterCloudsTex;
                        passData.fogTextureHandle = cozyData.fogBuffer;
                        passData.horizonLayers = horizonModule.ActiveLayers;

                        builder.SetRenderAttachment(afterCloudsTex, 0, AccessFlags.ReadWrite);
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                        builder.UseTexture(beforeCloudsTex, AccessFlags.Read);

                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            ExecuteMainPass(data, context, false);
                        });
                    }
                }
            }
        }
    }
}