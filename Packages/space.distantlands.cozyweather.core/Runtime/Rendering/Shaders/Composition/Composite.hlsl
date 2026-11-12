#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyFogVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"

TEXTURE2D_X(_BlitTex);
TEXTURE2D_X(_BeforeCloudsTex);
TEXTURE2D_X(_AfterCloudsTex);
TEXTURE2D_X(_SkyTex);
TEXTURE2D_X(_FogTex);
TEXTURE2D_X(_CloudTex);
TEXTURE2D_X(_SunshaftsTex);
TEXTURE2D_X(_VolumetricCloudTex);
TEXTURE2D_X(_VolumetricCloudDepthTex);
SamplerState linear_clamp_sampler;

static const float DEPTH_DISTANCE = 50000.0;


void CompositeSky_float(float2 uv, out float4 output) {
    
    float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTex, linear_clamp_sampler, uv);
    float4 fogColor = SAMPLE_TEXTURE2D_X(_FogTex, linear_clamp_sampler, uv);
    float4 beforeClouds = SAMPLE_TEXTURE2D_X(_BeforeCloudsTex, linear_clamp_sampler, uv);
    float4 afterClouds = SAMPLE_TEXTURE2D_X(_AfterCloudsTex, linear_clamp_sampler, uv);
    float4 skyColor = SAMPLE_TEXTURE2D_X(_SkyTex, linear_clamp_sampler, uv);
    float4 cloudColor = SAMPLE_TEXTURE2D_X(_CloudTex, linear_clamp_sampler, uv);
    float4 sunshaftsColor = SAMPLE_TEXTURE2D_X(_SunshaftsTex, linear_clamp_sampler, uv);
    float4 volumetricCloudColor = SAMPLE_TEXTURE2D_X(_VolumetricCloudTex, linear_clamp_sampler, uv);
    float cloudDepth = SAMPLE_TEXTURE2D_X(_VolumetricCloudDepthTex, linear_clamp_sampler, uv).r;

    float rawDepth = SampleSceneDepth(uv);
    float depth01 = Linear01Depth(rawDepth, _ZBufferParams);

    float3 sceneWithFog = lerp(sceneColor.rgb, fogColor.rgb, fogColor.a);
    float3 skyWithFog = lerp(skyColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_SkyFogAmount);
    float3 cloudsWithFog = lerp(cloudColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_CloudsFogAmount);
    float3 volumetricCloudsWithFog = lerp(volumetricCloudColor.rgb, fogColor.rgb, (fogColor.a * saturate(cloudDepth * 10 * CZY_Fog_Base_DensityMultiplier * CZY_Base_Weather_FogCoverage)) * CZY_Fog_Layers_CloudsFogAmount);

    skyWithFog = lerp(skyWithFog, beforeClouds.rgb, beforeClouds.a);
    skyWithFog = lerp(skyWithFog, cloudsWithFog, cloudColor.a); 
    skyWithFog = lerp(skyWithFog, afterClouds.rgb, afterClouds.a);

    float3 combinedSkyAndFog = lerp(sceneWithFog, skyWithFog, step(1.0, depth01 * max(skyColor.a, cloudColor.a)));

    // Volumetric Cloud Depth Blending & Occlusion

    float cloudFadeRange = 250.0;
    float depthMix = smoothstep(depth01 * _ProjectionParams.z - cloudFadeRange, depth01 * _ProjectionParams.z, cloudDepth * DEPTH_DISTANCE);

    // depthMix = step(depth01 * _ProjectionParams.z, cloudDepth * DEPTH_DISTANCE);
    
    if (depth01 >= 1.0 && volumetricCloudColor.a > 0.0) {
        depthMix = lerp(depthMix, 0, volumetricCloudColor.a);
    }
    
    // Combine depth occlusion with the cloud's intrinsic alpha channel
    float finalCloudAlpha = volumetricCloudColor.a * (1-depthMix);
    float3 finalColor = lerp(combinedSkyAndFog, volumetricCloudsWithFog.rgb, finalCloudAlpha);

    finalColor.rgb += sunshaftsColor.rgb;


    output = float4(finalColor.rgb, 1.0);
}