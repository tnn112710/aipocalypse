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

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    public partial class PostEffectRenderer<T>
    {
        private static Material _BlurMaterial;
        private static Material BlurMaterial
        {
            get
            {
                if (!_BlurMaterial)
                {
                    Shader blurShader = Shader.Find(ShaderNames.BlurPass);
                    
                    _BlurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
                }
                
                return _BlurMaterial;
            }
        }
        
        
        private class BlurPassData : BasePassData
        {
            public Vector2 direction;
        }

        internal enum BlurDirection
        {
            Horizontal,
            Vertical
        }

        internal void BlurPass(RenderGraph renderGraph, int resolutionHeight, TextureHandle source, TextureHandle target, float radius, int iterations, bool gaussian)
        {
            if (radius == 0) return;
            
            //Use the height as this is consistent between wide, ultra and super-ultra-wide aspect ratios
            float radiusScale = CalculateRadiusScale(resolutionHeight);
            radius *= radiusScale;
            
            for (int i = 1; i <= iterations; i++)
            {
                BlurPass(renderGraph, source, target, BlurDirection.Horizontal, radius * i, gaussian);
                BlurPass(renderGraph, target, source, BlurDirection.Vertical, radius * i, gaussian);
            }
        }
        
        internal void BlurDirectionalPass(RenderGraph renderGraph, int resolutionHeight, TextureHandle source, TextureHandle target, BlurDirection direction, float sampleDistance, float length, bool gaussian, float angle = 0)
        {
            if (length == 0 || sampleDistance <= 0.01f) return;
            
            float scaledLength = length * CalculateRadiusScale(resolutionHeight);
            
            int iterations = Mathf.CeilToInt(scaledLength / (sampleDistance * 5));
            iterations = Mathf.Clamp(iterations, 1, 8);
            
            float stepSize = scaledLength / iterations;
            
            for (int i = 0; i < iterations; i++)
            {
                float offset = stepSize * (i + 1);
                
                BlurPass(renderGraph, source, target, direction, offset, gaussian, angle);
                BlurPass(renderGraph, target, source, direction, offset, gaussian, angle);
            }
        }
        
        //Perform a single blur pass on the source target
        private void BlurPass(RenderGraph renderGraph, TextureHandle source, TextureHandle target, BlurDirection direction, float length, bool gaussian, float angle = 0)
        {
            if (length == 0) return;

            string id = "Blur Pass";
            
            #if DEBUG
            id = $"[{passName}] Blur {direction}";
            #endif
            
            using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>(id, out var passData))
            {
                passData.material = BlurMaterial;
                passData.source = source;
                
                TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
                
                var lengthX = length / sourceDesc.width;
                var lengthY = length / sourceDesc.height;
                
                passData.direction = direction == BlurDirection.Horizontal ? new Vector2(lengthX, 0) : new Vector2(0, lengthY);

                if (angle != 0)
                {
                    float ratio = angle * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(ratio);
                    float sin = Mathf.Sin(ratio);
                    
                    Vector2 rotatedDirection = new Vector2(
                        passData.direction.x * cos - passData.direction.y * sin,
                        passData.direction.x * sin + passData.direction.y * cos
                    );

                    passData.direction = rotatedDirection;
                }

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                
                builder.SetRenderFunc((BlurPassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(ShaderParameters.BlurOffsets, data.direction);
                    //data.material.SetVector(BlurDirectionID, data.blurKernel);
                    
                    Blitter.BlitTexture(context.cmd, data.source, blitScale, data.material, gaussian ? 1 : 0);
                });
            }
        }
    }
}