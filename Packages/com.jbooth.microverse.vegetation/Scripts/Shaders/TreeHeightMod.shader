Shader "Hidden/MicroVerse/TreeHeightMod"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _TreeSDF;
            sampler2D _PlacementMask;
            float _RealHeight;
            float _Amount;
            float _Width;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float BlurSample(float2 stampUV)
            {
                float col = 0;
                float totalWeight = 0.0;
                int blurSize = int(_Width);
                float blurSkip = 1 + _Width / 4;
                // Sum all pixels around the current pixel

                float2 uv = stampUV;

                [loop]
                for (int x = -blurSize; x <= blurSize; x += 1)
                {
                    [loop]
                    for (int y = -blurSize; y <= blurSize; y += 1)
                    {
                        float hs = SAMPLE_DEPTH_TEXTURE(_MainTex, uv + float2(x, y) * _MainTex_TexelSize.xy * blurSkip).r;

                        col += hs;
                    }
                }
                // Average the colors
                int totalPixels = (2 * blurSize + 1) * (2 * blurSize + 1);
                col /= totalPixels;
    
                return col;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 raw = tex2D(_MainTex, i.uv);
                float2 sdfUV = i.uv;
                sdfUV *= (_MainTex_TexelSize.z) / (_MainTex_TexelSize.z - 1);
                float sdf = tex2D(_TreeSDF, sdfUV).r * 256;
                if (sdf < 0)
                    return raw;

                float origHeight = UnpackHeightmap(raw);
                float w = 1.0 - saturate(sdf / _Width);

                float mask = 1.0 - tex2D(_PlacementMask, i.uv).r;
                w *= mask;
                w = smoothstep(0,1, w);

                //origHeight += (_Amount / _RealHeight) * w;
                origHeight += (_Amount / _RealHeight) * w;
                //origHeight = lerp(origHeight, BlurSample(i.uv), w);
                return PackHeightmap(origHeight);
            }
            ENDCG
        }
    }
}
