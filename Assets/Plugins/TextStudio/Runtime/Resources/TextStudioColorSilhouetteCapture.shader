Shader "Hidden/Text Studio/Color Silhouette Capture"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input input)
            {
                Output result;
                result.position = UnityObjectToClipPos(input.position);
                result.uv = input.uv;
                return result;
            }
            float4 Frag(Output input) : SV_Target
            {
                // Threshold source texels before reconstruction so adjacent packed art cannot enter through bilinear filtering.
                float2 center = (floor(input.uv * _MainTex_TexelSize.zw) + .5) * _MainTex_TexelSize.xy;
                return float4(0, 0, 0, tex2Dlod(_MainTex, float4(center, 0, 0)).a);
            }
            ENDCG
        }
    }
}
