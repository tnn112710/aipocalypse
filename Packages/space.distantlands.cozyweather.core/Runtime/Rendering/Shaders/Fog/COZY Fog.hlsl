#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozySkyVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Fog/CozyFogFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile _ _LIGHT_COOKIES



void COZYFog_float(float2 screenUV, UnityTexture3D fogVolume, sampler ss, out float4 output) {

    #if !defined(CZY_USE_FOGBASE)
    output = float4(1,1,1, 0);
    #else

    float rawDepth = SampleSceneDepth(screenUV);
    float depth01 = Linear01Depth(rawDepth, _ZBufferParams);

    float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
    float worldDepth = distance(_WorldSpaceCameraPos, worldPos);

    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);

    half4 combinedColor = ComputeFogGradient(worldDepth * CZY_Fog_Base_DensityMultiplier * CZY_Base_Weather_FogCoverage);
    half verticalFalloff = ComputeFogVerticalFalloff(viewDir, worldPos, length(float3(_WorldSpaceCameraPos.xz, _WorldSpaceCameraPos.y * 0.3) - float3(worldPos.xz, worldPos.y * 0.3)) * CZY_Fog_Base_DensityMultiplier * CZY_Base_Weather_FogCoverage, depth01);

    half fogLightAmount = 1;
    
    #ifdef CZY_USE_FOGTWOTONE
    combinedColor *= ComputeTwoTone(viewDir);
    #endif

    #if defined(CZY_USE_FOGVOLUMETRIC)
    float maxFogDistance = 500.0;

    float worldDepthClamped = clamp(worldDepth, 0.0, maxFogDistance);

    float sliceZ = pow(worldDepthClamped / maxFogDistance, 0.5);

    float4 fogSample = fogVolume.SampleLevel(ss, float3(screenUV, sliceZ), 0);

    combinedColor *= (saturate(fogSample));
    #endif

    half fogOpacity = verticalFalloff * combinedColor.a;

    #ifdef CZY_USE_FOGSUNLIGHTFLARE
    combinedColor += ComputeSunLightFlare(viewDir, worldDepth) * fogLightAmount * CZY_Base_Weather_SunFilter;
    #endif
    #ifdef CZY_USE_FOGMOONLIGHTFLARE
    combinedColor += ComputeMoonLightFlare(viewDir, worldDepth) * fogLightAmount * CZY_Base_Weather_SunFilter;
    #endif

    #ifdef CZY_USE_FOGHEIGHT
    half4 heightFogColor = ComputeHeightFog(viewDir, worldPos, worldDepth);
    half heightFogDensity = (heightFogColor.a * heightFogColor.a);
    heightFogDensity = min(heightFogDensity, verticalFalloff * verticalFalloff * verticalFalloff);
    fogOpacity = max(heightFogDensity, fogOpacity);
    combinedColor = lerp(combinedColor, heightFogColor, heightFogDensity);
    #endif

    output = float4(AdjustHSV(combinedColor.rgb, CZY_Base_Weather_SaturationFilter, CZY_Base_Weather_ValueFilter), fogOpacity);
    
    #endif
}