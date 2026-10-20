// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal.Internal;

namespace sc.screeneffects.runtime
{
    public partial class PostEffectRenderer<T>
    {
        internal TextureHandle cameraNormalsTexture;
        
        private Material DepthNormalsMat;
        private static Shader DepthNormalsShader;
        
        private void SetupNormals(RenderGraph renderGraph)
        {
            if (!requiresNormals) return;
            
            cameraNormalsTexture = TextureHandle.nullHandle;
            
            //Deferred rendering
            if (resourceData.gBuffer[2].IsValid())
            {
                //In this case RT2 can simply be used
                cameraNormalsTexture = resourceData.gBuffer[2];
                
                //Debug.Log("Requiring normals texture, using Deferred GBuffer2");
                return;
            }
            
            //Prepass was executed? 
            var hasNormalsPrepass = resourceData.cameraNormalsTexture.IsValid() && !reconstructNormalsFromDepth;

            if (hasNormalsPrepass)
            {
                TextureDesc cameraNormalsDesc = resourceData.cameraNormalsTexture.GetDescriptor(renderGraph);
                TextureDesc cameraDepthDesc = cameraDepth.GetDescriptor(renderGraph);
                
                //Debug.Log(cameraNormalsDesc.name);

                hasNormalsPrepass = cameraNormalsDesc.format != cameraDepthDesc.format;
            }
            //if(requiresDepthNormals) Debug.Log($"[SC Screen Effects] Has normals prepass: {hasNormalsPrepass}");
            
            //Use high quality normals from the prepass if available, otherwise reconstruct from depth
            if (hasNormalsPrepass)
            {
                //Debug.Log($"{ProfilerTag} requires normals texture, using prepass texture.");
                cameraNormalsTexture = resourceData.cameraNormalsTexture;
                //return;
            }
            
            if (reconstructNormalsFromDepth)
            {
                //Reconstruct from depth
                if (!DepthNormalsShader) DepthNormalsShader = Shader.Find(ShaderNames.DepthNormals);
                
                #if DEBUG
                if (!DepthNormalsShader)
                {
                    Debug.LogError("[SC Screen Effects] Shader with the name <i>" + shaderName +
                                   "</i> could not be found, this means it was not included in the build." +
                                   "This will happen when effects are entirely initialized through scripting. Be sure to reference the shader somewhere.");
                    return;
                }
                #endif
                
                GenerateDepthNormals(renderGraph);
            }
        }

        private class NormalsPassData
        {
            public UniversalCameraData cameraData;
            public Material material;
        }
        
        /// <summary>
        /// Reconstructs view-space normals from depth texture
        /// </summary>
        private void GenerateDepthNormals(RenderGraph renderGraph)
        {
            CreateMaterialIfNull(ref DepthNormalsMat, DepthNormalsShader);

            TextureDesc cameraNormalsDesc = cameraColorDesc;
            cameraNormalsDesc.name = TextureNames._CameraNormalsTexture;
            cameraNormalsDesc.format = DepthNormalOnlyPass.GetGraphicsFormat();
            cameraNormalsDesc.msaaSamples = MSAASamples.None;
                
            cameraNormalsTexture = renderGraph.CreateTexture(cameraNormalsDesc);
            
            using (var builder = renderGraph.AddRasterRenderPass<NormalsPassData>("Depth To World Normal", out var passData))
            {
                builder.AllowPassCulling(true);
                
                passData.material = DepthNormalsMat;
                passData.cameraData = cameraData;
                
                builder.UseTexture(cameraDepth, AccessFlags.Read);
                builder.SetRenderAttachment(cameraNormalsTexture, 0, AccessFlags.Write, 0, 0);
                
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(cameraNormalsTexture, Shader.PropertyToID(TextureNames._CameraNormalsTexture));
                
                builder.SetRenderFunc((NormalsPassData data, RasterGraphContext context) =>
                {
                    NormalReconstruction.SetupProperties(context.cmd, data.cameraData);
                    data.material.SetTexture(TextureNames._CameraDepthTexture, cameraDepth);
                    //context.cmd.SetGlobalMatrixArray("_NormalReconstructionMatrix", new Matrix4x4[] { lightToLocalMatrix, Matrix4x4.identity });
                    
                    Blitter.BlitTexture(context.cmd, cameraDepth, blitScale, data.material, 0);
                });
            }
        }
    }
}