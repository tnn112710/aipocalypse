using UnityEngine;

namespace Rowlan.Genesis.Integrations.KWS2
{
    public class TextureUtils
    {
        public static Texture2D ConvertRenderTextureToTexture2D(RenderTexture rt)
        {
            var targetFormat = IsHDR(rt) ? TextureFormat.RGBAHalf : TextureFormat.RGBA32;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.R16, true, true);
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;

            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            return tex;
        }

        public static bool IsHDR(Texture texture)
        {
            if (texture is RenderTexture rt)
            {
                return rt.format == RenderTextureFormat.ARGBHalf ||
                       rt.format == RenderTextureFormat.ARGBFloat;
            }
            else if (texture is Texture2D tex2D)
            {
                return tex2D.format == TextureFormat.RGBAHalf ||
                       tex2D.format == TextureFormat.RGBAFloat;
            }

            return false;
        }
    }
}