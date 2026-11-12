#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozySunshaftsVariables.hlsl"
#include "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

TEXTURE2D_X(_CloudTex);
TEXTURE2D_X(_VolumetricCloudDepthTex);
TEXTURE2D_X(_CozySunshaftsMaskTex);
SamplerState linear_clamp_sampler;

void COZYSunshaftsMask_float(float2 uv, out float4 output)
{
    float rawDepth = SampleSceneDepth(uv);
    float depth01 = Linear01Depth(rawDepth, _ZBufferParams);
    float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);

    float flatCloud = SAMPLE_TEXTURE2D_X(_CloudTex, linear_clamp_sampler, uv).a;
    float cloudDepth = SAMPLE_TEXTURE2D_X(_VolumetricCloudDepthTex, linear_clamp_sampler, uv).a;
    float depthMask = 1 - (step(depth01, 0.999));
    float cloudMask = (1 - cloudDepth * CZY_Sunshafts_Screenspace_CloudMaskAmount);
    float flatCloudMask = 1 - flatCloud * CZY_Sunshafts_Screenspace_CloudMaskAmount;
    float horizonMask = saturate(dot(viewDir, float3(0, -1, 0)));
    float sunMask = saturate(dot(viewDir, CZY_Base_Orientation_SunDirection));
    float mask = depthMask * min(cloudMask, flatCloudMask) * horizonMask * sunMask;

    output = float4(mask, 0, 0, 1);
}

float2 COZYSunshafts_WorldDirToScreenUV(float3 dirWS)
{
    float3 pointWS = _WorldSpaceCameraPos.xyz + normalize(dirWS) * 5000.0;
    float4 clipPos = mul(UNITY_MATRIX_VP, float4(pointWS, 1.0));
    float2 ndc = clipPos.xy / clipPos.w;
    float2 uv = ndc * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    return uv;
}

void COZYSunshafts_float(float2 uv, out float4 output)
{
    float2 sunUV = COZYSunshafts_WorldDirToScreenUV(CZY_Base_Orientation_SunDirection);

    float3 camForwardWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(0, 0, -1)));
    float sunFacing = dot(normalize(CZY_Base_Orientation_SunDirection), camForwardWS);
    float facingFade = saturate(-sunFacing * 1.0);

    int samples = (int)clamp(CZY_Sunshafts_Screenspace_Steps, 8.0, 128.0);
    float2 deltaUV = (uv - sunUV) * (1 / (float)samples);

    float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453); 
    float2 sampleUV = uv - (deltaUV * jitter);
    float illuminationDecay = 1.0;
    float illuminationAttack = 0.0;
    float accum = 0.0;

    [loop]
    for (int i = 0; i < samples; i++)
    {
        sampleUV -= deltaUV;
        float sampleMask = SAMPLE_TEXTURE2D_X(_CozySunshaftsMaskTex, linear_clamp_sampler, sampleUV).r;
        accum += sampleMask * illuminationDecay * illuminationAttack;
        illuminationDecay *= clamp(CZY_Sunshafts_Screenspace_Length, 0, 0.94);
        illuminationAttack = saturate(illuminationAttack + (2.0 / samples));
    }

    accum = (accum) * facingFade;

    float2 edgeDist = abs(sunUV - 0.5) * 2.0;
    float edgeFade = saturate(1.0 - max(edgeDist.x, edgeDist.y));
    accum *= lerp(0.35, 1.0, edgeFade);

    accum = saturate(accum);
    output = float4(CZY_Sunshafts_Screenspace_Color.rgb * CZY_Base_Lighting_SunColor * accum * CZY_Sunshafts_Screenspace_Intensity, accum);
}