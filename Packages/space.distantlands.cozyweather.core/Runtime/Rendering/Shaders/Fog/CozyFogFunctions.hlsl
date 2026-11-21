
#ifndef COZYFogFunctions
#define COZYFogFunctions

#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyFogVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

float4 CZY_TransparentFogFeatures;
float4 CZY_TransparentFogFeatures2;
TEXTURE3D(CZY_TransparentFogVolume);
SAMPLER(sampler_CZY_TrilinearClamp);

half4 ComputeFogGradient(float depth) {

    half4 fogColor;

    half4 z1 = half4(CZY_Fog_Gradient_Color1.rgb, CZY_Fog_Gradient_Density1);
    half4 z2 = half4(CZY_Fog_Gradient_Color2.rgb, CZY_Fog_Gradient_Density2);
    half4 z3 = half4(CZY_Fog_Gradient_Color3.rgb, CZY_Fog_Gradient_Density3);
    half4 z4 = half4(CZY_Fog_Gradient_Color4.rgb, CZY_Fog_Gradient_Density4);
    half4 z5 = half4(CZY_Fog_Gradient_Color5.rgb, CZY_Fog_Gradient_Density5);

    // Calculate factors
    half t1 = saturate((CZY_Fog_Gradient_Start1 - depth) / (CZY_Fog_Gradient_Start1 - CZY_Fog_Gradient_Start2));
    half t2 = saturate((CZY_Fog_Gradient_Start2 - depth) / (CZY_Fog_Gradient_Start2 - CZY_Fog_Gradient_Start3));
    half t3 = saturate((CZY_Fog_Gradient_Start3 - depth) / (CZY_Fog_Gradient_Start3 - CZY_Fog_Gradient_Start4));
    half t4 = saturate((CZY_Fog_Gradient_Start4 - depth) / (CZY_Fog_Gradient_Start4 - CZY_Fog_Gradient_Start5));

    // Chain lerps
    half4 col = lerp(z1, z2, t1);
    col = lerp(col, z3, t2);
    col = lerp(col, z4, t3);
    fogColor = lerp(col, z5, t4) * half4(CZY_Base_Weather_FilterColor.xyz, 1);

    return fogColor;
}

half ComputeFogVerticalFalloff(float3 viewDirection, float3 pos, float depth, float normalizedDepth) {
    half y = (half)viewDirection.y;
    half offset = (1.0h - (half)CZY_Fog_Base_Height);
    half backgroundFogVerticalBlend = saturate(pow(saturate(y + offset), (half)CZY_Fog_Base_Smoothness / CZY_Base_Weather_FogHeightCoverage));
    half foregroundFogVerticalBlend = saturate(1 - ((pos.y - CZY_Fog_Base_BaseHeight) / CZY_Fog_Base_BaseSmoothness));
    float mappedDepth = max(saturate(depth / CZY_Fog_Base_TransitionDistance), normalizedDepth);
    return lerp(foregroundFogVerticalBlend, backgroundFogVerticalBlend, mappedDepth);
    // return backgroundFogVerticalBlend;  
}

half4 ComputeSunLightFlare(float3 viewDirection, float depth) {

    float depthBasedIntensity = smoothstep(CZY_Fog_SunLightFlare_MinDistance, CZY_Fog_SunLightFlare_MaxDistance, depth);

    float3 squishParams = float3(1.0, CZY_Fog_SunLightFlare_Squish, 1.0);
    float3 squishedView = normalize(viewDirection * squishParams);

    half flareDot = saturate(dot(squishedView, CZY_Base_Orientation_SunDirection));
    flareDot = pow(flareDot, abs(CZY_Fog_SunLightFlare_Falloff) + 1.0h);

    return (half4)CZY_Fog_SunLightFlare_Color * flareDot * (half)CZY_Fog_SunLightFlare_Intensity * CZY_Base_Weather_SunFilter * saturate(1 - CZY_Base_Weather_CumulusCoverage) * depthBasedIntensity;
}

half4 ComputeMoonLightFlare(float3 viewDirection, float depth) {

    float depthBasedIntensity = smoothstep(CZY_Fog_MoonLightFlare_MinDistance, CZY_Fog_MoonLightFlare_MaxDistance, depth);

    float3 squishParams = float3(1.0, CZY_Fog_MoonLightFlare_Squish, 1.0);
    float3 squishedView = normalize(viewDirection * squishParams);

    half flareDot = saturate(dot(squishedView, -CZY_Base_Orientation_MoonDirection));
    flareDot = pow(flareDot, abs(CZY_Fog_MoonLightFlare_Falloff) + 1.0h);

    return (half4)CZY_Fog_MoonLightFlare_Color * flareDot * (half)CZY_Fog_MoonLightFlare_Intensity * CZY_Base_Weather_SunFilter * saturate(1 - CZY_Base_Weather_CumulusCoverage) * depthBasedIntensity * CZY_Base_Time_MoonPhasePercentage;
}
half GetHeightFogOpticalDepth(float3 viewDirection, float3 worldPos, float depth)
{
    float fogBase = CZY_Fog_Height_BaseHeight - CZY_Fog_Height_Falloff * distance(worldPos.xz, _WorldSpaceCameraPos.xz) * CZY_Fog_Height_Smoothness * 0.01;
    float fogFalloff = CZY_Fog_Height_Falloff + CZY_Fog_Height_Falloff * distance(worldPos.xz, _WorldSpaceCameraPos.xz) * CZY_Fog_Height_Smoothness * 0.01;
    float posInFogVolume = saturate(RemapValue(worldPos.y, fogBase, fogBase + max(0, fogFalloff), 1, 0));

    float3 uvw = worldPos * CZY_Fog_Height_VariationTextureScale * 0.01 + CZY_Fog_Height_VariationTextureFlow * 0.1 * _Time.y;
    float fogVariation = SAMPLE_TEXTURE3D_LOD(CZY_Fog_Height_VariationNoise, samplerCZY_Fog_Height_VariationNoise, uvw, 0).r;
    float fogVariationFadeOut = saturate(RemapValue(depth, 25, 100, 1, 0));

    return saturate(posInFogVolume - (fogVariation * fogVariationFadeOut * CZY_Fog_Height_VariationAmount));
}

half4 ComputeTwoTone(float3 viewDirection) {
    float twoToneDot = dot(viewDirection, CZY_Base_Orientation_SunDirection) + CZY_Fog_TwoTone_Offset;
    float twoTonePowNegative = -abs( -twoToneDot) * CZY_Fog_TwoTone_Smoothness;
    float twoTonePow = abs(twoToneDot) * CZY_Fog_TwoTone_Smoothness;
    float finalT = (lerp(twoTonePowNegative, twoTonePow, sign(saturate(twoToneDot))) + 1) / 2;

    half3 colorModifier = lerp(CZY_Fog_TwoTone_AntiSunSideColor.rgb, CZY_Fog_TwoTone_SunSideColor.rgb, saturate(finalT));
    half densityModifier = lerp(CZY_Fog_TwoTone_AntiSunSideDensity, CZY_Fog_TwoTone_SunSideDensity, saturate(finalT));

    return half4(colorModifier.rgb, densityModifier);

}

half4 ComputeHeightFog(float3 viewDirection, float3 worldPos, float depth)
{
    half opticalDepth = GetHeightFogOpticalDepth(viewDirection, worldPos, depth);
    half fogFactor = 1 - saturate(exp( -depth * CZY_Fog_Height_Density));

    return half4(CZY_Fog_Height_Color.rgb, fogFactor * opticalDepth);
}

float4 BlendStylizedFog(float4 inputColor, float3 worldPos) 
{
    if (CZY_TransparentFogFeatures.x < 0.5)
        return inputColor;

    float worldDepth = distance(_WorldSpaceCameraPos, worldPos);

    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
    float3 positionNDC = ComputeNormalizedDeviceCoordinatesWithZ(worldPos, UNITY_MATRIX_VP);
    float deviceDepth = positionNDC.z;
    #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3)
    deviceDepth = deviceDepth * 0.5 + 0.5;
    #endif
    float depth01 = Linear01Depth(deviceDepth, _ZBufferParams);

    half4 combinedColor = ComputeFogGradient(worldDepth * CZY_Fog_Base_DensityMultiplier * CZY_Base_Weather_FogCoverage);
    half verticalFalloff = ComputeFogVerticalFalloff(viewDir, worldPos, length(float3(_WorldSpaceCameraPos.xz, _WorldSpaceCameraPos.y * 0.3) - float3(worldPos.xz, worldPos.y * 0.3)) * CZY_Fog_Base_DensityMultiplier * CZY_Base_Weather_FogCoverage, depth01);

    half fogLightAmount = 1;

    if (CZY_TransparentFogFeatures.y > 0.5)
        combinedColor *= ComputeTwoTone(viewDir);

    if (CZY_TransparentFogFeatures2.y > 0.5)
    {
        float sliceZ = sqrt(saturate(worldDepth / 500.0));
        combinedColor *= saturate(SAMPLE_TEXTURE3D_LOD(CZY_TransparentFogVolume, sampler_CZY_TrilinearClamp, float3(positionNDC.xy, sliceZ), 0));
    }

    half fogOpacity = verticalFalloff * combinedColor.a;

    if (CZY_TransparentFogFeatures.z > 0.5)
        combinedColor += ComputeSunLightFlare(viewDir, worldDepth) * fogLightAmount * CZY_Base_Weather_SunFilter;
    if (CZY_TransparentFogFeatures.w > 0.5)
        combinedColor += ComputeMoonLightFlare(viewDir, worldDepth) * fogLightAmount * CZY_Base_Weather_SunFilter;

    if (CZY_TransparentFogFeatures2.x > 0.5)
    {
        half4 heightFogColor = ComputeHeightFog(viewDir, worldPos, worldDepth);
        half heightFogDensity = (heightFogColor.a * heightFogColor.a);
        heightFogDensity = min(heightFogDensity, verticalFalloff * verticalFalloff * verticalFalloff);
        fogOpacity = max(heightFogDensity, fogOpacity);
        combinedColor = lerp(combinedColor, heightFogColor, heightFogDensity);
    }

    return float4(lerp(inputColor.rgb, AdjustHSV(combinedColor.rgb, CZY_Base_Weather_SaturationFilter, CZY_Base_Weather_ValueFilter), fogOpacity), max(inputColor.a, fogOpacity));
}

void BlendStylizedFog_float(float4 inputColor, float3 worldPos, out float4 output) 
{
    output = BlendStylizedFog(inputColor, worldPos);
}

#endif