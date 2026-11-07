Shader "Hidden/MicroVerse/HeightAreaEffectStamp"
{
    Properties
    {
        [HideInInspector] _MainTex ("Heightmap Texture", 2D) = "white" {}
        [HideInInspector] _FalloffTexture("Falloff", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ _USEFALLOFF _USEFALLOFFRANGE _USEFALLOFFTEXTURE _USEFALLOFFSPLINEAREA
            #pragma shader_feature_local_fragment _ _USEFALLOFFPAINTAREA

            #pragma shader_feature_local_fragment _ _FALLOFFSMOOTHSTEP _FALLOFFEASEIN _FALLOFFEASEOUT _FALLOFFEASEINOUT
            #pragma shader_feature_local_fragment _ _FALLOFFNOISE _FALLOFFFBM _FALLOFFWORLEY _FALLOFFWORM _FALLOFFWORMFBM _FALLOFFNOISETEXTURE

            #pragma shader_feature_local_fragment _ _TERRACE _REMAP _BEACH _GAUSSIANBLUR _HIGHPASS _SIMPLEWIND _SIMPLEEROSION
            #pragma shader_feature_local_fragment _ _NOISENOISE _NOISEFBM _NOISEWORLEY _NOISENOISETEXTURE _NOISEWORM _NOISEWORMFBM
            #pragma shader_feature_local_fragment _ _GAUSSIANHORIZONTAL _GAUSSIANVERTICAL
            

            // because unity's height format is stupid and only uses half the possible
            // precision.
            #define kMaxHeight          (32766.0f/65535.0f)

            #include_with_pragmas "UnityCG.cginc"
            #include_with_pragmas "Packages/com.jbooth.microverse/Scripts/Shaders/Noise.cginc"
            #include_with_pragmas "/../HeightStampFiltering.cginc"

            struct vertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 stampUV: TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _RemapCurve;

            #if _HIGHPASS
            sampler2D _BlurredTex;
            float4 _BlurredTex_TexelSize;
            #endif

            sampler2D _NoiseNoiseTexture;
            float4 _NoiseNoiseTexture_ST;
            float4 _NoiseNoise;
            float4 _NoiseNoise2;
            int _NoiseNoiseChannel;

            
            float3 _NoiseUV;
            float _TerraceSize;
            float _TerraceStrength;
            float _SmoothSize;
            float _BeachDistance;
            float _BeachPower;
            float _GaussianStrength;
            float _GaussianXDistance;
            float _GaussianYDistance;
            float _HighPassStrength;
            float _ThermalErosionStrength;
            float _ThermalErosionThreshold;
            float _WindSlopeThreshold;
            float _WindStrength;
            float _WindAngle;
            float _WorldPosY;
 
            float4x4 _Transform;
            float3 _RealSize;

            float GetNoise(float2 noiseUV, float2 stampUV)
            {
                float2 uv0 = noiseUV;

                if (_NoiseNoise2.x > 0)
                    uv0 = stampUV;

                float result = 0;
                #if _NOISENOISE
                    result = Noise(uv0, _NoiseNoise);
                #elif _NOISEFBM
                    result = NoiseFBM(uv0, _NoiseNoise);
                #elif _NOISEWORLEY
                    result = NoiseWorley(uv0, _NoiseNoise);
                #elif _NOISEWORM
                    result = NoiseWorm(uv0, _NoiseNoise);
                #elif _NOISEWORMFBM
                    result = NoiseWormFBM(uv0, _NoiseNoise);
                #elif _NOISENOISETEXTURE
                    result = ((tex2D(_NoiseNoiseTexture, uv0 * _NoiseNoiseTexture_ST.xy + _NoiseNoiseTexture_ST.zw)[_NoiseNoiseChannel]) * _NoiseNoise.y + _NoiseNoise.w);
                #endif
                return result;
            }

            static const float gaussweights[] = {
                0.2734375000,
	            0.2187500000,
	            0.1093750000,
	            0.03125000000,
	            0.003906250000
            };

            float Gaussian(float2 uv, float center)
            {
                float orig = center;
                center *= gaussweights[0];
                float2 offset = float2(0, 0);
                #if _GAUSSIANHORIZONTAL
                offset.x = 1 * _GaussianXDistance;
                #elif _GAUSSIANVERTICAL
                offset.y = 1 * _GaussianYDistance;
                #endif
                for(int i = 1; i < 5; i++)
                {
                    center += tex2D(_MainTex, uv + _MainTex_TexelSize * offset * i) * gaussweights[i];
                    center += tex2D(_MainTex, uv - _MainTex_TexelSize * offset * i) * gaussweights[i];
                }
                return lerp(orig, center, _GaussianStrength);
            }

            float2 Slope(float2 uv, float center)
            {
                float step = 2;
                float left = tex2D(_MainTex, uv - _MainTex_TexelSize * float2(step, 0));
                float right = tex2D(_MainTex, uv + _MainTex_TexelSize * float2(step, 0));
                float up = tex2D(_MainTex, uv + _MainTex_TexelSize * float2(0, step));
                float down = tex2D(_MainTex, uv - _MainTex_TexelSize * float2(0, step));

                float horizontal = left - right;
                horizontal = clamp(horizontal, -_WindSlopeThreshold, _WindSlopeThreshold);
                float vertical = down - up;
                vertical = clamp(vertical, -_WindSlopeThreshold, _WindSlopeThreshold);
                return float2(horizontal, vertical);
            }

            

            v2f vert(vertexInput v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                o.stampUV = mul(_Transform, float4(v.uv, 0, 1)).xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 heightSample = tex2D(_MainTex, i.uv);
                bool cp = (i.stampUV.x < 0 || i.stampUV.x > 1 || i.stampUV.y < 0|| i.stampUV.y > 1);
                if (cp)
                    return heightSample;
                float height = UnpackHeightmap(heightSample);

                float2 noiseUV = (i.uv * _NoiseUV.z) + _NoiseUV.xy;
                float2 stampUV = i.stampUV;

                float2 falloffuv = noiseUV;
                if (_FalloffNoise2.x > 0)
                    falloffuv = stampUV;

                float noise = 0;
                float falloff = ComputeFalloff(i.uv, i.stampUV, noiseUV, 0);

                #if _FALLOFFNOISE
                    noise = (Noise(falloffuv, _FalloffNoise)) / _RealSize.y;
                #elif _FALLOFFFBM
                    noise = (NoiseFBM(falloffuv, _FalloffNoise)) / _RealSize.y;
                #elif _FALLOFFWORLEY
                    noise = (NoiseWorley(falloffuv, _FalloffNoise)) / _RealSize.y;
                #elif _FALLOFFWORM
                    noise = (NoiseWorm(falloffuv, _FalloffNoise)) / _RealSize.y;
                #elif _FALLOFFWORMFBM
                    noise = (NoiseWormFBM(falloffuv, _FalloffNoise)) / _RealSize.y;
                #elif _FALLOFFNOISETEXTURE
                    noise = (tex2D(_FalloffNoiseTexture, falloffuv * _FalloffNoiseTexture_ST.xy + _FalloffNoiseTexture_ST.zw)[_FalloffNoiseChannel] * 2.0 - 1.0) / _RealSize.y * _FalloffNoise.y + _FalloffNoise.w;
                #endif


                #if _FALLOFFNOISE || _FALLOFFFBM || _FALLOFFWORLEY || _FALLOFFWORM || _FALLOFFWORMFBM || _FALLOFFNOISETEXTURE
                    noise *= 1-falloff;
                    falloff = ComputeFalloff(i.uv, stampUV, noiseUV, noise);
                #endif

                float newHeight = height;
                #if _TERRACE
                    float scaledHeight = height * _RealSize.y / _TerraceSize;
				    newHeight = round(scaledHeight) * _TerraceSize / _RealSize.y;
                    newHeight = lerp(height, newHeight, _TerraceStrength);
                #elif _BEACH
                    float scaledHeight = height * _RealSize.y;
                    float dist = abs(scaledHeight - _WorldPosY);
                    dist /= _BeachDistance;
                    dist = saturate(dist);
                    dist = pow(dist, _BeachPower);
                    newHeight = lerp(_WorldPosY, scaledHeight, dist);
                    newHeight /= _RealSize.y;
                #elif _REMAP
                    newHeight = tex2D(_RemapCurve, float2(height, 0));
                #elif _GAUSSIANBLUR
                    newHeight = Gaussian(i.uv, newHeight);
                #elif _HIGHPASS
                    float blurred = tex2D(_BlurredTex, i.uv);
                    float diff = (newHeight - blurred);
                    newHeight = newHeight + diff * _HighPassStrength * (diff < 0.002 ? 1 : 0);
                #elif _SIMPLEWIND
                    float windX = cos(_WindAngle);
                    float windY = sin(_WindAngle);
                    float2 slope = Slope(i.uv, newHeight);
                    float rotatedSlope = (slope.x * windX) + (slope.y * windY);
                    newHeight = newHeight + (rotatedSlope * _WindStrength * (rotatedSlope < 0 ? 1 : 0));
                #elif _SIMPLEEROSION
                    newHeight = newHeight;
                #endif


                #if _NOISENOISE || _NOISEFBM || _NOISEWORLEY || _NOISENOISETEXTURE || _NOISEWORM || _NOISEWORMFBM
                    newHeight = CombineHeight(newHeight, GetNoise(i.uv, stampUV) / _RealSize.y, _CombineMode);
                #endif

                return PackHeightmap(clamp(lerp(height, newHeight, falloff), 0, kMaxHeight));
            }
            ENDCG
        }
    }
}