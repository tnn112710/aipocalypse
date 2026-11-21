#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyCloudsVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyFogVariables.hlsl"
#include "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

TEXTURE2D_X(_FogTex); SAMPLER(sampler_FogTex);
TEXTURE2D_X(_SkyTex); SAMPLER(sampler_SkyTex);
TEXTURE2D_X(_HistoryTex); SAMPLER(sampler_HistoryTex);
TEXTURE2D_X(_DepthHistoryTex); SAMPLER(sampler_DepthHistoryTex);
float4x4 _CozyPrevViewProjMatrix[2];
float _HistoryWeight;

static const float DEPTH_DISTANCE = 50000.0;

float Jitter(float2 uv) {
    return max(SAMPLE_TEXTURE2D(CZY_Clouds_Volumetric_BlueNoise, samplerCZY_Clouds_Volumetric_BlueNoise, uv + frac(_Time.y * 10)).r, 0.01);
}

void ComputeAltoVolumetricLights(float3 pos, out float lighting)
{
    float3 position = pos + CZY_Base_Weather_CloudWorldOffset;
    lighting = 1;

    for (int i = 0; i < 4; i++)
    {
        position -= normalize(CZY_Base_Orientation_SunDirection - float3(0, 0.3, 0)) * CZY_Clouds_Volumetric_HighlightStepSize;
        float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z);

        float halfHeight = CZY_Clouds_Volumetric_Height;
        float cloudTop = CZY_Clouds_Volumetric_PlacementHeight * 2 + halfHeight;
        float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight * 2 - halfHeight;

        float2 heightRamp = (position.y - cloudBottom) / (cloudTop - cloudBottom);

        float2 heightInCloud = float2(saturate((position.y - cloudBottom) / (cloudTop - cloudBottom)), 0);
        float2 baseUV = float2(position.x, position.z) * 0.000001;
        float3 remappedPosition = position * 0.0002;
        float3 remappedPosition2 = position * 0.001;

        float heightValue = SAMPLE_TEXTURE2D(CZY_Clouds_Volumetric_Ramp, samplerCZY_Clouds_Volumetric_Ramp, heightInCloud);
        heightValue = RemapValue(heightValue, 0, 1, 0.5, 1);

        float2 uv1 = baseUV * 1;
        float2 uv2 = baseUV * 5 * CZY_Clouds_Volumetric_PlacementScale1 + windOffset * 0.0051;
        float2 uv3 = baseUV * 10 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
        float cloudCoverage = RemapValue(CZY_Base_Weather_CumulusCoverage, 0, 1, 0.15, 1.15);

        float cloudSample1 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv2), 3.0).g;
        float cloudSample2 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv3), 3.0).r;

        float scatteredCloudPlacement1 = cloudSample1 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement2 = cloudSample2 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2) * 5;

        lighting -= saturate(scatteredCloudPlacement) * 0.33;

        if (lighting <= 0.002) {
            lighting = 0;
            return;
        }
    }
}

float GetAltoVolumetricDensity(float3 pos, float3 viewDir, out float lightingRatio, out float highlightRatio) {
    float clip;
    float depth;
    float3 domeNormal;
    lightingRatio = 0;
    highlightRatio = 0;

    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z);

    float halfHeight = CZY_Clouds_Volumetric_Height;
    float cloudTop = CZY_Clouds_Volumetric_PlacementHeight * 4 + halfHeight;
    float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight * 4 - halfHeight;

    float2 heightRamp = (pos.y - cloudBottom) / (cloudTop - cloudBottom);

    float2 heightInCloud = float2(saturate((pos.y - cloudBottom) / (cloudTop - cloudBottom)), 0);
    float3 samplePos = pos + CZY_Base_Weather_CloudWorldOffset;
    float2 baseUV = samplePos.xz * 0.000005 + float2(0.64, 0.25) * 0.05 * (1 - sin(heightRamp * 3.1415)) * 0.1;
    float3 remappedPosition = samplePos * 0.0002;
    float3 remappedPosition2 = pos * 0.001;

    float heightValue = SAMPLE_TEXTURE2D(CZY_Clouds_Volumetric_Ramp, samplerCZY_Clouds_Volumetric_Ramp, heightInCloud);
    heightValue = RemapValue(heightValue, 0, 1, 0.5, 1);

    float2 uv1 = baseUV * 1;
    float2 uv2 = baseUV * 5 * CZY_Clouds_Volumetric_PlacementScale1 + windOffset * 0.0071;
    float2 uv3 = baseUV * 10 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.0013;
    float cloudCoverage = RemapValue(CZY_Base_Weather_CumulusCoverage, 0, 1, 0.15, 1.15);

    float cloudSample1 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv2), 3.0).g;
    float cloudSample2 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv3), 3.0).r;

    float scatteredCloudPlacement1 = cloudSample1 - (1 - cloudCoverage * heightValue);
    float scatteredCloudPlacement2 = cloudSample2 - (1 - cloudCoverage * heightValue);
    float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2) * 5;

    float cloudPlacement = saturate(scatteredCloudPlacement);

    if (cloudPlacement <= 0.001) {
        lightingRatio = 0;
        return 0;
    }

    float noise3D = (1 - SAMPLE_TEXTURE3D_LOD(CZY_Clouds_Volumetric_NoiseTexture, samplerCZY_Clouds_Volumetric_NoiseTexture, (remappedPosition * CZY_Clouds_Volumetric_NoiseScale + float3(CZY_Base_Weather_CloudNoiseTime * 0.05, CZY_Base_Weather_CloudNoiseTime * 0.01, 0)), 0).g) * CZY_Clouds_Volumetric_NoiseAmount;
    // float cloudNoise2 = (1 - SAMPLE_TEXTURE3D(CZY_Clouds_Volumetric_NoiseTexture, samplerCZY_Clouds_Volumetric_NoiseTexture, (remappedPosition2)).r) * CZY_Clouds_Volumetric_NoiseAmount;

    float density = saturate(cloudPlacement - (noise3D)) * CZY_Clouds_Volumetric_Density * 0.01;

    lightingRatio = ((0.5 - (heightRamp)) * CZY_Clouds_Volumetric_DarkeningIntensity) + 0.5;
    lightingRatio *= 0.6;
    return density;

}

void ComputeAltoVolumetricCloudLayer(float3 viewDir, float2 screenUV, float sceneDepth, float worldDepth, out float depth, out float4 output) {

    float noiseOffset = Jitter(screenUV);
    depth = _ProjectionParams.z;
    output = float4(0, 0, 0, 0);

    float distance = 0;
    float raymarchedDistance = 0;
    float3 position = _WorldSpaceCameraPos;
    float3 cameraPos = position;
    float farClip = sceneDepth == 1 ? 1000000 : min(worldDepth, _ProjectionParams.z);
    farClip = 1000000;

    float halfHeight = CZY_Clouds_Volumetric_Height;
    float cloudTop = CZY_Clouds_Volumetric_PlacementHeight * 4 + halfHeight;
    float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight * 4 - halfHeight;

    float aboveClouds = step(cloudTop, position.y);
    float belowClouds = step(position.y, cloudBottom);

    float isAboveLookingDown = aboveClouds * step( -0.01, -viewDir.y);
    float isBelowLookingUp = belowClouds * step(0.01, viewDir.y);

    float safeDirYAbove = min(viewDir.y, -0.01);
    float safeDirYBelow = max(abs(viewDir.y), 0.01);

    float tAbove = (cloudTop - position.y) / safeDirYAbove;
    float tBelow = (cloudBottom - position.y) / safeDirYBelow;

    float t = isAboveLookingDown * tAbove + isBelowLookingUp * tBelow;
    float3 offset = viewDir * t;

    position += offset;
    distance += t;
    float currentStepSize = CZY_Clouds_Volumetric_StepSize;

    float transmittance = 1.0;
    float density = 0;

    float3 finalColor = 0;

    [loop]
    for (int j = 0; j < CZY_Clouds_Volumetric_LODSteps; j++)
    {
        position += viewDir * noiseOffset * currentStepSize;
        distance += noiseOffset * currentStepSize;
        float3 stepDir = viewDir * currentStepSize;

        [loop]
        for (int i = 0; i < CZY_Clouds_Volumetric_Steps; i++)
        {
            if (distance > farClip || density >= 1 || abs(position.y - CZY_Clouds_Volumetric_PlacementHeight * 4) > CZY_Clouds_Volumetric_Height)
                break;

            float lightRatio;
            float highlightRatio;
            float sampleDensity = GetAltoVolumetricDensity(position, viewDir, lightRatio, highlightRatio);

            if (sampleDensity > 0) {
                density += sampleDensity * currentStepSize;

                float3 stepColor = lerp(CZY_Clouds_Volumetric_ShadeColor.rgb * CZY_Clouds_Volumetric_LitColor.rgb, CZY_Clouds_Volumetric_LitColor.rgb, saturate(1 - lightRatio));
                stepColor = lerp(stepColor, CZY_Clouds_Volumetric_LitColor.rgb * CZY_Clouds_Volumetric_HighlightBrightness * CZY_Base_Lighting_SunColor, highlightRatio * saturate(length(CZY_Base_Lighting_SunColor)));
                finalColor += stepColor * sampleDensity * currentStepSize;
            }

            position += viewDir * currentStepSize;
            distance += currentStepSize;
        }

        if (distance > farClip || density >= 1 || abs(position.y - CZY_Clouds_Volumetric_PlacementHeight * 4) > halfHeight)
            break;

        currentStepSize *= CZY_Clouds_Volumetric_LODCoefficient;
    }

    depth = distance;

    finalColor = finalColor / (density + 0.0000001);
    density = saturate(density);

    output = float4(finalColor, density);

}

void ComputeVolumetricLights(float3 position, out float lighting)
{
    float3 pos = position + CZY_Base_Weather_CloudWorldOffset;
    lighting = 1;

    for (int i = 0; i < 4; i++)
    {
        pos -= normalize(CZY_Base_Orientation_SunDirection - float3(0, 0.3, 0)) * CZY_Clouds_Volumetric_HighlightStepSize;
        float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z) * CZY_Clouds_Volumetric_WindStrength;

        float halfHeight = CZY_Clouds_Volumetric_Height;
        float cloudTop = CZY_Clouds_Volumetric_PlacementHeight + halfHeight;
        float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight - halfHeight;

        float2 heightRamp = (pos.y - cloudBottom) / (cloudTop - cloudBottom);

        float2 heightInCloud = float2(saturate((pos.y - cloudBottom) / (cloudTop - cloudBottom)), 0);
        float2 baseUV = float2(pos.x, pos.z) * 0.000005 + float2(0.64, 0.25) * 0.005 * (1 - sin(heightRamp * 3.1415)) * 0.1;
        float3 remappedPosition = pos * 0.0002;
        float3 remappedPosition2 = pos * 0.001;

        float2 uv1 = baseUV * 1;
        float2 uv2 = baseUV * 5 * CZY_Clouds_Volumetric_PlacementScale1 + windOffset * 0.0051;
        float2 uv3 = baseUV * 10 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
        float2 uv4 = baseUV * 1 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
        float cloudCoverage = 0;
        float oneMinusCoverage = RemapValue(CZY_Base_Weather_CumulusCoverage, 0, 1, -0.1, 1) - 1;
        cloudCoverage = 1.2 - (oneMinusCoverage * oneMinusCoverage);
        // cloudCoverage = CZY_Base_Weather_CumulusCoverage * 2; // <- Why does changing the 2 here break the gradient calculated in lighting ratio?

        float cloudSample1 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv2), 3.0).g;
        float cloudSample2 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv3), 3.0).r;

        float cumulusRamp = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_Ramp, samplerCZY_Clouds_Volumetric_Ramp, heightInCloud, 0.0).r;
        
        #if defined(CZY_USE_CLOUDSCUMULONIMBUS)
        float nimbusRamp = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Cumulonimbus_Ramp, samplerCZY_Clouds_Cumulonimbus_Ramp, heightInCloud, 0.0).r;

        float nimbusPlacement = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv4), 3.0).r;

        cumulusRamp = lerp(cumulusRamp, nimbusRamp, saturate(-nimbusPlacement + 3 * CZY_Base_Weather_CumulonimbusCoverage));
        #endif
        float heightValue = cumulusRamp;  

        float scatteredCloudPlacement1 = cloudSample1 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement2 = cloudSample2 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2) * 5;
        
        lighting -= saturate(scatteredCloudPlacement) * 0.33;

        if (lighting <= 0.002) {
            lighting = 0;
            return;
        }
    }
}

void ComputeVolumetricSelfShadow(float3 position, out float lighting)
{
    float3 pos = position + CZY_Base_Weather_CloudWorldOffset;
    lighting = 1;

    for (int i = 0; i < 6; i++)
    {
        pos += float3(0, 0.1 * CZY_Clouds_Volumetric_Height, 0);
        float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z) * CZY_Clouds_Volumetric_WindStrength;

        float halfHeight = CZY_Clouds_Volumetric_Height;
        float cloudTop = CZY_Clouds_Volumetric_PlacementHeight + halfHeight;
        float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight - halfHeight;

        float2 heightRamp = (pos.y - cloudBottom) / (cloudTop - cloudBottom);

        if (heightRamp.x > 1)
        {
            lighting = saturate(lighting);
            return;
        }

        float2 heightInCloud = float2(saturate((pos.y - cloudBottom) / (cloudTop - cloudBottom)), 0);
        float2 baseUV = float2(pos.x, pos.z) * 0.000005 + float2(0.64, 0.25) * 0.005 * (1 - sin(heightRamp * 3.1415)) * 0.1;
        float3 remappedPosition = pos * 0.0002;
        float3 remappedPosition2 = pos * 0.001;

        float2 uv1 = baseUV * 1;
        float2 uv2 = baseUV * 5 * CZY_Clouds_Volumetric_PlacementScale1 + windOffset * 0.0051;
        float2 uv3 = baseUV * 10 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
        float2 uv4 = baseUV * 1 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
        float cloudCoverage = 0;
        float oneMinusCoverage = RemapValue(CZY_Base_Weather_CumulusCoverage, 0, 1, -0.1, 1) - 1;
        cloudCoverage = 1.2 - (oneMinusCoverage * oneMinusCoverage);
        // cloudCoverage = CZY_Base_Weather_CumulusCoverage * 2; // <- Why does changing the 2 here break the gradient calculated in lighting ratio?

        float cloudSample1 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv2), 3.0).g;
        float cloudSample2 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv3), 3.0).r;

        float cumulusRamp = SAMPLE_TEXTURE2D(CZY_Clouds_Volumetric_Ramp, samplerCZY_Clouds_Volumetric_Ramp, heightInCloud).r;
        
        #if defined(CZY_USE_CLOUDSCUMULONIMBUS)
        float nimbusRamp = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Cumulonimbus_Ramp, samplerCZY_Clouds_Cumulonimbus_Ramp, heightInCloud, 0.0).r;

        float nimbusPlacement = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv4), 3.0).r;

        cumulusRamp = lerp(cumulusRamp, nimbusRamp, saturate(-nimbusPlacement + 2 * CZY_Base_Weather_CumulonimbusCoverage));
        #endif
        float heightValue = cumulusRamp;  

        float scatteredCloudPlacement1 = cloudSample1 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement2 = cloudSample2 - (1 - cloudCoverage * heightValue);
        float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2) * 5;
        
        lighting -= saturate(scatteredCloudPlacement) * 0.2;
    }
    
    lighting = saturate(lighting);
}

float GetVolumetricDensity(float3 pos, float3 viewDir, out float lightingRatio, out float shadowRatio, out float highlightRatio) {
    float clip;
    float depth;
    float3 domeNormal;
    lightingRatio = 0;
    highlightRatio = 0;
    shadowRatio = 1;

    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z) * CZY_Clouds_Volumetric_WindStrength;

    float halfHeight = CZY_Clouds_Volumetric_Height;
    float cloudTop = CZY_Clouds_Volumetric_PlacementHeight + halfHeight;
    float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight - halfHeight;

    float2 heightRamp = (pos.y - cloudBottom) / (cloudTop - cloudBottom);

    float2 heightInCloud = float2(saturate((pos.y - cloudBottom) / (cloudTop - cloudBottom)), 0);
    float3 samplePos = pos + CZY_Base_Weather_CloudWorldOffset;
    float2 baseUV = samplePos.xz * 0.000005 + float2(0.64, 0.25) * 0.005 * (1 - sin(heightRamp * 3.1415)) * 0.1;
    float3 remappedPosition = samplePos * 0.0002;
    float3 remappedPosition2 = pos * 0.001;

    float2 uv1 = baseUV * 1;
    float2 uv2 = baseUV * 5 * CZY_Clouds_Volumetric_PlacementScale1 + windOffset * 0.0051;
    float2 uv3 = baseUV * 10 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
    float2 uv4 = baseUV * 1 * CZY_Clouds_Volumetric_PlacementScale2 + windOffset * 0.001;
    float cloudCoverage = 0;
    float oneMinusCoverage = RemapValue(CZY_Base_Weather_CumulusCoverage, 0, 1, -0.1, 1) - 1;
    cloudCoverage = 1.2 - (oneMinusCoverage * oneMinusCoverage);

    float cloudSample1 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv2), 2.0).g;
    float cloudSample2 = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Volumetric_MainTexture, samplerCZY_Clouds_Volumetric_MainTexture, (uv3), 2.0).r;

    float cumulusRamp = SAMPLE_TEXTURE2D(CZY_Clouds_Volumetric_Ramp, samplerCZY_Clouds_Volumetric_Ramp, heightInCloud).r;

    #if defined(CZY_USE_CLOUDSCUMULONIMBUS)
    float nimbusRamp = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Cumulonimbus_Ramp, samplerCZY_Clouds_Cumulonimbus_Ramp, heightInCloud, 0).r;

    float nimbusPlacement = SAMPLE_TEXTURE2D_LOD(CZY_Clouds_Cumulonimbus_MainTexture, samplerCZY_Clouds_Cumulonimbus_MainTexture, (uv4), 2.0).r;

    cumulusRamp = lerp(cumulusRamp, nimbusRamp, saturate(-nimbusPlacement + 2 * CZY_Base_Weather_CumulonimbusCoverage));
    #endif
    float heightValue = cumulusRamp;    

    float scatteredCloudPlacement1 = cloudSample1 - (1 - cloudCoverage * heightValue);
    float scatteredCloudPlacement2 = cloudSample2 - (1 - cloudCoverage * heightValue);
    float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2) * 5;

    float cloudPlacement = (scatteredCloudPlacement);

    if (cloudPlacement <= 0.001) {
        lightingRatio = 0;
        return 0;
    }

    float noise3D = (1 - SAMPLE_TEXTURE3D_LOD(CZY_Clouds_Volumetric_NoiseTexture, samplerCZY_Clouds_Volumetric_NoiseTexture, (remappedPosition * CZY_Clouds_Volumetric_NoiseScale + float3(CZY_Base_Weather_CloudNoiseTime * 0.05, CZY_Base_Weather_CloudNoiseTime * 0.01, 0)), 0).g) * CZY_Clouds_Volumetric_NoiseAmount;
    // float cloudNoise2 = (1 - SAMPLE_TEXTURE3D(CZY_Clouds_Volumetric_NoiseTexture, samplerCZY_Clouds_Volumetric_NoiseTexture, (remappedPosition2)).r) * CZY_Clouds_Volumetric_NoiseAmount;

    float density = saturate(cloudPlacement - (noise3D)) * CZY_Clouds_Volumetric_Density * 0.01;

    lightingRatio = ((0.5 - (heightRamp.x)) * CZY_Clouds_Volumetric_DarkeningIntensity) + 0.5;
    #if CZY_USE_CLOUDSVOLUMETRICHIGHLIGHTS
    ComputeVolumetricLights(pos, highlightRatio);
    #endif
    #if CZY_USE_CLOUDSVOLUMETRICSELFSHADOWING
    ComputeVolumetricSelfShadow(pos, shadowRatio);
    #endif
    // lightingRatio = lerp(0, lightingRatio, cloudPlacement);
    return density;

}

void ComputeVolumetricCloudLayer(float3 viewDir, float2 screenUV, float sceneDepth, float worldDepth, out float depth, out float4 output) {

    float noiseOffset = Jitter(screenUV);
    depth = DEPTH_DISTANCE;
    output = float4(0, 0, 0, 0);

    float distance = 0;
    float3 position = _WorldSpaceCameraPos;
    float farClip = sceneDepth == 1 ? DEPTH_DISTANCE : min(worldDepth, _ProjectionParams.z);
    farClip = DEPTH_DISTANCE;

    float halfHeight = CZY_Clouds_Volumetric_Height;
    float cloudTop = CZY_Clouds_Volumetric_PlacementHeight + halfHeight;
    float cloudBottom = CZY_Clouds_Volumetric_PlacementHeight - halfHeight;

    float aboveClouds = step(cloudTop, position.y);
    float belowClouds = step(position.y, cloudBottom);

    if ((aboveClouds > 0.5 && viewDir.y >= 0.0) || (belowClouds > 0.5 && viewDir.y <= 0.01)) {
        depth = DEPTH_DISTANCE;
        return;
    }

    float isAboveLookingDown = aboveClouds * step( -0.01, -viewDir.y);
    float isBelowLookingUp = belowClouds * step(0.01, viewDir.y);

    float safeDirYAbove = min(viewDir.y, -0.01);
    float safeDirYBelow = max(abs(viewDir.y), 0.01);

    float tAbove = (cloudTop - position.y) / safeDirYAbove;
    float tBelow = (cloudBottom - position.y) / safeDirYBelow;

    float t = isAboveLookingDown * tAbove + isBelowLookingUp * tBelow + 0.5;
    float3 offset = viewDir * t;

    position += offset;
    distance += t;
    float currentStepSize = CZY_Clouds_Volumetric_StepSize;

    float density = 0;
    float3 finalColor = 0;
    float accumulatedDepth = 0;

    [loop]
    for (int j = 0; j < CZY_Clouds_Volumetric_LODSteps; j++ )
    {
        position += viewDir * noiseOffset * currentStepSize;
        distance += noiseOffset * currentStepSize;

        [loop]
        for (int i = 0; i < CZY_Clouds_Volumetric_Steps; i++ )
        {
            if (distance > farClip || density >= 1)
                break;
                
            if (abs(position.y - CZY_Clouds_Volumetric_PlacementHeight) > CZY_Clouds_Volumetric_Height)
                break;

            float lightRatio, highlightRatio, selfShadow;
            float sampleDensity = GetVolumetricDensity(position, viewDir, lightRatio, selfShadow, highlightRatio);

            if (sampleDensity > 0) {
                float weight = sampleDensity * currentStepSize;
                density += weight;
                accumulatedDepth += distance * weight;

                float3 stepColor = lerp(CZY_Clouds_Volumetric_ShadeColor.rgb * CZY_Clouds_Volumetric_LitColor.rgb, CZY_Clouds_Volumetric_LitColor.rgb, saturate(1 - lightRatio));
                stepColor += CZY_Clouds_Volumetric_HighlightBrightness * CZY_Base_Lighting_SunColor * CZY_Clouds_Volumetric_LitColor * CZY_Base_Weather_SunFilter * highlightRatio * saturate(length(CZY_Base_Lighting_SunColor));
                stepColor *= lerp(0.5, 1, selfShadow);
                finalColor += stepColor * weight;
            }

            position += viewDir * currentStepSize;
            distance += currentStepSize;
        }

        if (distance > farClip || density >= 1 || abs(position.y - CZY_Clouds_Volumetric_PlacementHeight) > CZY_Clouds_Volumetric_Height)
            break;

        currentStepSize *= CZY_Clouds_Volumetric_LODCoefficient;
    }

    depth = (density > 0.001) ? (accumulatedDepth / (density + 0.0000001)) : DEPTH_DISTANCE;

    finalColor = finalColor / (density + 0.0000001);
    density = saturate(density);

    output = float4(finalColor, density);
    output *= float4(CZY_Base_Weather_CloudFilter.xyz, 1);
}

void COZYVolumetricClouds_float(float2 screenUV, out float4 output, out float depth) {
    depth = 0;
    output = float4(0,0,0,0);

    #if defined(CZY_USE_CLOUDSVOLUMETRIC)
    float rawDepth = SampleSceneDepth(screenUV);
    float depth01 = Linear01Depth(rawDepth, _ZBufferParams);
    float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
    float worldDepth = distance(_WorldSpaceCameraPos, worldPos);


    float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos.xyz);
    float radialToPlanarLinear = depth01 / (worldDepth + 0.00001);

    float4 fogColor = SAMPLE_TEXTURE2D_X(_FogTex, sampler_FogTex, screenUV);
    float4 skyColor = SAMPLE_TEXTURE2D_X(_SkyTex, sampler_SkyTex, screenUV);
    output = float4(skyColor.rgb, 0);

    // float4 altoVolumetricColor;
    // float altoVolumetricDepth;
    // ComputeAltoVolumetricCloudLayer(viewDir, screenUV, depth01, worldDepth, altoVolumetricDepth, altoVolumetricColor);
    // altoVolumetricColor = float4(lerp(altoVolumetricColor.rgb, fogColor.rgb, ComputeFogDensity(altoVolumetricDepth / 100) * CZY_Fog_Layers_CloudsFogAmount), altoVolumetricColor.a);

    // output.rgb = (altoVolumetricColor.rgb * altoVolumetricColor.a) + output.rgb * (1.0 - altoVolumetricColor.a);
    // output.a = altoVolumetricColor.a;

    float4 volumetricColor;
    float volumetricDepth;
    ComputeVolumetricCloudLayer(viewDir, screenUV, depth01, worldDepth, volumetricDepth, volumetricColor);
    // volumetricColor = float4(lerp(volumetricColor.rgb, fogColor.rgb, ComputeFogDensity(volumetricDepth)), volumetricColor.a);

    output.rgb = (volumetricColor.rgb * volumetricColor.a) + output.rgb * (1.0 - volumetricColor.a);
    float volumetricLinear01 = saturate(volumetricDepth * radialToPlanarLinear);
    depth = saturate(volumetricDepth / DEPTH_DISTANCE);
    output.a = volumetricColor.a + output.a * (1.0 - volumetricColor.a);

    output = float4(AdjustHSV(output.rgb, CZY_Base_Weather_SaturationFilter, 0), output.a);

    if (_HistoryWeight > 0.0 && volumetricColor.a > 0.001)
    {
        float3 cloudWorldPosition = _WorldSpaceCameraPos.xyz + viewDir * volumetricDepth;
        uint eyeIndex = 0;
        #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
        eyeIndex = unity_StereoEyeIndex;
        #endif

        float4 previousClipPosition = mul(_CozyPrevViewProjMatrix[eyeIndex], float4(cloudWorldPosition, 1.0));
        if (previousClipPosition.w > 0.0)
        {
            float2 historyUV = previousClipPosition.xy / previousClipPosition.w * 0.5 + 0.5;
            #if UNITY_UV_STARTS_AT_TOP
            historyUV.y = 1.0 - historyUV.y;
            #endif

            if (all(historyUV >= 0.0) && all(historyUV <= 1.0))
            {
                float4 historyColor = SAMPLE_TEXTURE2D_X(_HistoryTex, sampler_HistoryTex, historyUV);
                output = lerp(output, historyColor, _HistoryWeight * step(0.001, historyColor.a));
            }
        }
    }
    // output = historyColor;

    #endif
}