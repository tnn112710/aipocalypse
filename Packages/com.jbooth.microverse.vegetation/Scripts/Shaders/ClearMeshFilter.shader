Shader "Hidden/MicroVerse/ClearMeshFilter"
{
    Properties
    {
        [HideInInspector] _MainTex("orig", 2D) = "black" {}
        [HideInInspector] _Heightmap("Heightmap", 2D) = "black" {}
        [HideInInspector] _Normalmap("Normalmap", 2D) = "black" {}
        [HideInInspector] _Curvemap ("Curvemap", 2D) = "black" {}
        [HideInInspector] _FalloffTexture("Falloff", 2D) = "white" {}
        [HideInInspector] _WeightNoiseTexture("Noise", 2D) = "grey" {}
        [HideInInspector] _SlopeNoiseTexture("Noise", 2D) = "grey" {}
        [HideInInspector] _AngleNoiseTexture("Noise", 2D) = "grey" {}
        [HideInInspector] _CurvatureNoiseTexture("Noise", 2D) = "grey" {}
        [HideInInspector] _ObjectLayerMask("Object Layer Mask", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ _TEXTUREFILTER
            #pragma shader_feature_local_fragment _ _MEGASPLAT

            #if _MEGASPLAT
                #define TEXCOUNT 256
            #else
                #define TEXCOUNT 32
            #endif

            #include "UnityCG.cginc"
            #include_with_pragmas "Packages/com.jbooth.microverse/Scripts/Shaders/Noise.cginc"
            #include_with_pragmas "Packages/com.jbooth.microverse/Scripts/Shaders/Filtering.cginc"

            sampler2D _MainTex;
            sampler2D _WeightMap;
            sampler2D _IndexMap;
            sampler2D _ObjectLayerMask;
            float4 _ObjectLayerMaskParams;   // x=paddingTexels, y=falloffTexels — SDF threshold/transition
            float _LayerIndex;
            float4 _MainTex_TexelSize;

            float3 _TextureLayerWeights[TEXCOUNT];
            

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 stampUV : TEXCOORD1;
            };



            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.stampUV = mul(_Transform, float4(v.uv, 0, 1)).xy;
                return o;
            }

            float2 frag(v2f i) : SV_Target
            {
                float2 old = tex2D(_MainTex, i.uv).xy;

                // Unity's terrain heightmap UV has its V axis inverted relative
                // to the world-space ortho silhouette capture, so flip Y when
                // sampling the mask.
                float2 maskUV = float2(i.uv.x, 1.0 - i.uv.y);

                // The bound texture is an UNSIGNED distance field from
                // MicroVerse's JumpFloodSDF: stored value is `dist / 256` in
                // texel units (0 inside the silhouette, growing outward).
                // Pixels too far for the JFA to find a seed read as 99999,
                // which falls outside any reasonable padding so maskVal goes
                // to 0 naturally.
                float sdfTexels = tex2D(_ObjectLayerMask, maskUV).r * 256.0;
                float padding   = _ObjectLayerMaskParams.x;            // texels — full-clear reach
                float falloff   = max(_ObjectLayerMaskParams.y, 1.0);  // texels — fade width past padding (min 1 for AA)

                // Full clear from 0 → padding, smooth fade from padding →
                // padding+falloff, no clear beyond. Falloff EXTENDS the reach
                // rather than eating into the padded region — otherwise
                // raising falloff above padding has no effect and the fade
                // band is too narrow for tree cells to land inside.
                float maskVal = 1.0 - smoothstep(padding, padding + falloff, sdfTexels);

                float2 noiseUV = (i.uv * _NoiseUV.z) + _NoiseUV.xy;
                float result = saturate(DoFilters(i.uv, i.stampUV, noiseUV));

                float texMask = 1;
                #if _TEXTUREFILTER
                    half4 indexes = tex2D(_IndexMap, i.uv) * TEXCOUNT;
                    half4 weights = tex2D(_WeightMap, i.uv);
                    for (int x = 0; x < 4; ++x)
                    {
                        int index = round(indexes[x]);
                        float weight = weights[x];
                        float3 tlw = _TextureLayerWeights[index];
                        texMask -= ((tlw.x * weight) + (tlw.z * weight) * tlw.y);
                    }
                    texMask = saturate(texMask);
                #endif

                // Object-layer silhouette gate. Defaults to "white" when
                // no mask is bound so the multiplication is a no-op for
                // stamps that don't use it.
                float w = result * texMask * maskVal;

                if (w > 0)
                {
                    old.x = _LayerIndex / 256;
                    old.y = old.y + w;
                }
                return old;
            }
            ENDCG
        }
    }
}
