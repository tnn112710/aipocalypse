#if KWS_HDRP || KWS_URP || KWS_BUILTIN
using KWS;
using UnityEngine;
using static Rowlan.Genesis.Integrations.KWS2.MV_KWS_StampWizard;

namespace Rowlan.Genesis.Integrations.KWS2
{
    public class TextureFilter
    {
        private static ComputeShader ConversionShader = null;
        private static ComputeShader BlurShader = null;
        private static ComputeShader ErodeShader = null;
        
        private static int ConversionKernel;
        private static int BlurKernel;
        private static int ErodeKernel;

        public Texture2D GetInMemoryStampTexture(KWS_DynamicWavesSimulationZone zone, FilterSettings settings)
        {
            RenderTexture inputRT = zone.GetSimulationDataTexture;

            RenderTexture result = ProcessHeightMask(inputRT, settings);

            Texture2D r16Tex = TextureUtils.ConvertRenderTextureToTexture2D(result);

            RenderTexture.ReleaseTemporary(result); // Clean up when done
            result = null;

            return r16Tex;

        }

        private static void InitComputShaders()
        {
            if (ConversionShader == null)
            {
                ConversionShader = Resources.Load<ComputeShader>("MV_KWS_Conversion");
                ConversionKernel = ConversionShader.FindKernel("MV_KWS_Conversion");

            }

            if (BlurShader == null)
            {
                BlurShader = Resources.Load<ComputeShader>("MV_KWS_Blur");
                BlurKernel = BlurShader.FindKernel("MV_KWS_Blur");

            }

            if (ErodeShader == null)
            {
                ErodeShader = Resources.Load<ComputeShader>("MV_KWS_Erosion");
                ErodeKernel = ErodeShader.FindKernel("MV_KWS_Erosion");

            }

        }

        /// <summary>
        /// Process height mask with various passes. Important: Caller must release the output render texture.
        /// </summary>
        /// <param name="originalRT"></param>
        /// <param name="parameters"></param>
        /// <returns>Important: Caller must release the output render texture</returns>
        private RenderTexture ProcessHeightMask(RenderTexture originalRT, FilterSettings settings)
        {
            // static initialization for performance
            InitComputShaders();

            int width = originalRT.width;
            int height = originalRT.height;

            RenderTexture tmpInputRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.RFloat);
            tmpInputRT.enableRandomWrite = true;
            tmpInputRT.Create();

            RenderTexture targetRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.RFloat);
            targetRT.enableRandomWrite = true;

            /* not required, we set it in the texture2d creation
            rt.useMipMap = true;
            rt.autoGenerateMips = true;
            */

            targetRT.Create();

            // copy heightmap, apply optional threshold (if black value = 0, otherwise 255)

            ConversionShader.SetInt("UseThreshold", settings.conversion.useThreshold ? 1 : 0);
            ConversionShader.SetTexture(ConversionKernel, "Source", originalRT);
            ConversionShader.SetTexture(ConversionKernel, "Result", targetRT);

            DispatchShader(ConversionShader, ConversionKernel, width, height);

            if(settings.blur.enabled)
            {
                for (int i = 0; i < settings.blur.passes; i++)
                {
                    // ping-pong the input and target textures, target is always the output
                    var tmp = tmpInputRT;
                    tmpInputRT = targetRT;
                    targetRT = tmp;

                    // Set textures
                    BlurShader.SetTexture(BlurKernel, "Source", tmpInputRT);
                    BlurShader.SetTexture(BlurKernel, "Result", targetRT);

                    BlurShader.SetInt("Radius", settings.blur.radius);

                    DispatchShader(BlurShader, BlurKernel, width, height);
                }
            }

            if(settings.erosion.enabled)
            {
                for (int i = 0; i < settings.erosion.passes; i++)
                {
                    // ping-pong the input and target textures, target is always the output
                    var tmp = tmpInputRT;
                    tmpInputRT = targetRT;
                    targetRT = tmp;

                    // Set textures
                    ErodeShader.SetTexture(ErodeKernel, "Source", tmpInputRT);
                    ErodeShader.SetTexture(ErodeKernel, "Result", targetRT);

                    ErodeShader.SetFloat("ErosionStrength", settings.erosion.erosionStrength);

                    DispatchShader(ErodeShader, ErodeKernel, width, height);
                }
            }

            tmpInputRT.Release();
            tmpInputRT = null;

            return targetRT; // caller must release this
        }

        private void DispatchShader(ComputeShader shader, int kernel, int width, int height)
        {
            int threadGroupsX = Mathf.CeilToInt(width / 8f);
            int threadGroupsY = Mathf.CeilToInt(height / 8f);
            shader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        }
    }
}
#endif