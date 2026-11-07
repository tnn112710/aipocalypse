#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyCloudsVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyFogVariables.hlsl"
#include "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

TEXTURE2D(_FogTex); SAMPLER(sampler_FogTex);
TEXTURE2D(_SkyTex); SAMPLER(sampler_SkyTex);

float2 GetUVForLayer(float layerHeight, float3 viewDir, out float distanceToLayer, out float clip) {
    float cloudHeight = layerHeight - _WorldSpaceCameraPos.y;
    float viewY = viewDir.y;
    float3 intersectionPoint = -_WorldSpaceCameraPos.xyz + -viewDir * (cloudHeight / viewY);

    float cloudClip = step( -viewDir.y, 0);
    float2 cloudUV = float2(intersectionPoint.x / 100, intersectionPoint.z / 100);

    distanceToLayer = distance(_WorldSpaceCameraPos, intersectionPoint);

    clip = 1;

    return cloudUV;
}

float2 GetUVForLayerSpherical(float layerHeight, float3 viewDir, out float3 worldNormal, out float distanceToLayer, out float clip) {
    float R_earth = 63710.0;
    float R_cloud = R_earth + layerHeight;

    // Center of earth relative to camera
    float3 L = float3(0, -R_earth - _WorldSpaceCameraPos.y, 0);

    float b = dot( -viewDir, L);
    float c = dot(L, L) - (R_cloud * R_cloud);
    float discriminant = b * b - c;

    if (discriminant < 0) {
        worldNormal = float3(0, 1, 0);
        distanceToLayer = 0;
        clip = 0;
        return 0;
    }

    float t = -b + sqrt(discriminant);
    float3 intersectionPoint = _WorldSpaceCameraPos.xyz + viewDir * t;

    // Calculate the Normal : Point on sphere surface minus Earth Center
    // Earth center in world space is (0, - R_earth, 0)
    float3 earthCenterWS = float3(_WorldSpaceCameraPos.x, -R_earth, _WorldSpaceCameraPos.z);
    worldNormal = normalize(intersectionPoint - earthCenterWS);

    distanceToLayer = t;
    clip = step(0, t);
    return intersectionPoint.xz / 100.0;
}


void ComputeAltocumulusCloudLayer(float3 viewDir, float sceneDepth, float worldDepth, out float depth, out float4 output) {

    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z) * 1 * 0.05;

    float clip;
    float3 domeNormal;
    float2 baseUV = GetUVForLayerSpherical(CZY_Clouds_Altocumulus_Height, viewDir, domeNormal, depth, clip) * CZY_Clouds_Altocumulus_TextureScale.xy * 0.5 + 1000;
    float2 uv1 = baseUV / 120 + windOffset * 0.1;
    float2 uv2 = baseUV / 80 + windOffset * 0.2;
    float2 uv3 = baseUV / 500 + windOffset * 0.015;
    float2 uv4 = baseUV / 350 + windOffset * 0.025;
    float2 uv5 = baseUV / 75;
    float2 uv6 = baseUV / 5000;

    float isSky = step(0.999, sceneDepth);
    float depthClip = max(step(depth, worldDepth), isSky);
    float horizonFade = saturate((viewDir.y + 0.2) * 5);

    float cloudCoverage = RemapValue(CZY_Base_Weather_AltocumulusCoverage, 0, 1, 0.0, 1.0);

    float cloudPlacementTextureSample = SAMPLE_TEXTURE2D(CZY_Clouds_Altocumulus_MainTexture, samplerCZY_Clouds_Altocumulus_MainTexture, (uv6)).g;
    float cloudPlacement = lerp(cloudPlacementTextureSample, 1, cloudCoverage);

    float cloudTexture1 = SAMPLE_TEXTURE2D(CZY_Clouds_Altocumulus_MainTexture, samplerCZY_Clouds_Altocumulus_MainTexture, (uv1)).r;
    float cloudTexture2 = SAMPLE_TEXTURE2D(CZY_Clouds_Altocumulus_MainTexture, samplerCZY_Clouds_Altocumulus_MainTexture, (uv2)).r;

    float placementTexture1 = SAMPLE_TEXTURE2D(CZY_Clouds_Altocumulus_MainTexture, samplerCZY_Clouds_Altocumulus_MainTexture, (uv3)).g;
    float placementTexture2 = SAMPLE_TEXTURE2D(CZY_Clouds_Altocumulus_MainTexture, samplerCZY_Clouds_Altocumulus_MainTexture, (uv4)).g;
    float placementBlend = saturate(placementTexture1 * placementTexture2 * 5);
    
    float placement = lerp(placementTexture1 * placementTexture2 * CZY_Base_Weather_AltocumulusCoverage, placementBlend, CZY_Base_Weather_AltocumulusCoverage);
    float cloudCohesion = saturate((cloudTexture1 + cloudTexture2 * cloudTexture1 + cloudTexture2 * CZY_Base_Weather_AltocumulusCoverage) * placement * CZY_Clouds_Altocumulus_Ramp);

    float absorption = saturate((cloudCohesion * 0.2) * 0.5);
    float3 baseColor = lerp(CZY_Clouds_Altocumulus_Color.rgb, CZY_Clouds_Altocumulus_Color.rgb * 0.5, absorption);

    output = float4(baseColor, saturate(cloudCohesion));
}

void ComputeCumulusCloudLayer(float3 viewDir, float sceneDepth, float worldDepth, out float depth, out float4 output) {
    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z) * CZY_Clouds_Cumulus_WindStrength * 0.1;

    float clip;
    float3 domeNormal;
    float2 baseUV = GetUVForLayerSpherical(CZY_Clouds_Cumulus_Height, viewDir, domeNormal, depth, clip);
    float2 uv1 = baseUV / 150 + windOffset * 0.5;
    float2 uv2 = baseUV / 50 + windOffset * 0.75;
    float2 uv3 = baseUV / 1200 + windOffset * 0.015;
    float2 uv4 = baseUV / 1000 + windOffset * 0.025;
    float2 uv5 = baseUV / 75;
    float2 uv6 = baseUV / 5000;

    float isSky = step(0.999, sceneDepth);
    float depthClip = max(step(depth, worldDepth), isSky);
    float horizonFade = saturate((viewDir.y + 0.2) * 5);


    float cloudCoverage = 1 - pow(CZY_Base_Weather_CumulusCoverage - 1, 2);

    float cloudPlacementTextureSample = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv6)).b;
    float cloudPlacement = lerp(cloudPlacementTextureSample, 1, cloudCoverage);

    float scatteredCloudPlacement1 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv3)).g - (1 - cloudCoverage);
    float scatteredCloudPlacement2 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv4)).r - (1 - cloudCoverage);
    float scatteredCloudPlacement = (scatteredCloudPlacement1 + scatteredCloudPlacement2);

    float detailNoise1 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv1)).g;
    float detailNoise2 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv2)).g;
    float detailNoise5 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv5)).r * lerp(0.2, 0, cloudCoverage);
    float detailNoise = detailNoise1 * detailNoise2;

    float remappedScatteredPlacement = saturate(scatteredCloudPlacement) * cloudPlacement;
    float remappedCohesion = lerp(1 - CZY_Clouds_Cumulus_Cohesion, 0.00, cloudCoverage);
    float cloudShapes = lerp(scatteredCloudPlacement, detailNoise, remappedCohesion);
    cloudShapes += detailNoise5;
    float cloudCohesion = pow(saturate(cloudShapes * 10 * CZY_Clouds_Cumulus_Density), 2);


    // LIGHTING _______________________________________________
    float3 sunDir = CZY_Base_Orientation_SunDirection.xyz;
    float ndotl = saturate(dot(domeNormal, sunDir));
    float viewDotSun = saturate(dot( -viewDir, sunDir));
    float mie = (pow(viewDotSun, 8.0) * 2.0);

    float lightWrap = 1;
    float wrappedNdotL = saturate((dot(domeNormal, sunDir) + lightWrap) / (1.0 + lightWrap));

    float3 finalSunLighting = CZY_Base_Lighting_SunColor.xyz * wrappedNdotL;
    finalSunLighting += (mie * CZY_Base_Lighting_SunColor.xyz * cloudCohesion);

    float3 silverLining = viewDotSun * pow(saturate(1 - cloudShapes), 4) * CZY_Base_Lighting_SunColor.xyz * 0.5;
    float3 sunGlow = pow(viewDotSun, 5.0) * 0.5 * CZY_Base_Lighting_SunColor.xyz;


    float3 absorption = saturate((scatteredCloudPlacement + detailNoise * 0.2) * 0.5);

    // Final Color Assembly
    float3 baseColor = lerp(CZY_Clouds_Cumulus_ShadeColor.rgb, CZY_Clouds_Cumulus_ShadeColor.rgb * 0.5, absorption);

    float3 moonDir = CZY_Base_Orientation_MoonDirection.xyz;

    float2 sunLightOffsetUV = (sunDir.xz * CZY_Clouds_Cumulus_Thickness * 0.025);
    float sunLightSample1 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv3 - sunLightOffsetUV)).g - (0.7 - cloudCoverage);
    float sunLightSample2 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv4 - sunLightOffsetUV)).r - (0.7 - cloudCoverage);

    float2 moonLightOffsetUV = (moonDir.xz * CZY_Clouds_Cumulus_Thickness * 0.025);
    float moonLightSample1 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv3 + moonLightOffsetUV)).g - (0.7 - cloudCoverage);
    float moonLightSample2 = SAMPLE_TEXTURE2D(CZY_Clouds_Cumulus_MainTexture, samplerCZY_Clouds_Cumulus_MainTexture, (uv4 + moonLightOffsetUV)).r - (0.7 - cloudCoverage);

    float sunLightSample = pow((sunLightSample1 + sunLightSample2), 1) * 2;
    sunLightSample -= (1 - (detailNoise1 + detailNoise2)) * .2;

    float moonLightSample = pow((moonLightSample1 + moonLightSample2), 1) * 2;
    moonLightSample -= (1 - detailNoise1) * 0.7;

    float sunShading = saturate(1 - sunLightSample); // + 0.2 for some light bleed
    float3 sunLightShading = CZY_Base_Lighting_SunColor.xyz * CZY_Clouds_Cumulus_LitColor.rgb * sunShading;

    float moonShading = saturate(1 - moonLightSample); // + 0.2 for some light bleed
    float3 moonLightShading = CZY_Base_Lighting_MoonColor.xyz * CZY_Clouds_Cumulus_LitColor.rgb * moonShading;

    float3 finalRGB = (baseColor + (finalSunLighting * sunShading) + silverLining + sunGlow + moonLightShading);


    output = float4(finalRGB, saturate(cloudCohesion * clip));
}

void ComputeCirrusCloudLayer(float3 viewDir, float sceneDepth, float worldDepth, out float depth, out float4 output) {
    float clip;
    float3 normal;

    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z);
    float2 baseUV = GetUVForLayerSpherical(CZY_Clouds_Cirrus_Height, viewDir, normal, depth, clip);
    float2 uv1 = baseUV * float2(0.0025, 0.0025) * CZY_Clouds_Cirrus_TextureScale.xy + windOffset * CZY_Clouds_Cirrus_WindStrength * 0.005;
    float2 uv2 = baseUV * float2(0.001, 0.001) * CZY_Clouds_Cirrus_TextureScale.xy + windOffset * CZY_Clouds_Cirrus_WindStrength * 0.002;

    // Depth
    float isSky = step(0.999, sceneDepth);
    float depthClip = max(step(depth, worldDepth), isSky);
    // Horizon
    float horizonFade = saturate(viewDir.y * 5);

    float cloudCoverage = RemapValue(CZY_Base_Weather_CirrusCoverage, 0, 1, 0.0, 0.3);

    float smallPlacementSample1 = SAMPLE_TEXTURE2D(CZY_Clouds_Cirrus_MainTexture, samplerCZY_Clouds_Cirrus_MainTexture, (uv1)).g;
    float smallPlacementSample2 = SAMPLE_TEXTURE2D(CZY_Clouds_Cirrus_MainTexture, samplerCZY_Clouds_Cirrus_MainTexture, (uv2)).g;

    float coverage = ((smallPlacementSample1 * CZY_Base_Weather_CirrusCoverage)) + (smallPlacementSample2);
    coverage = saturate(pow(coverage, CZY_Clouds_Cirrus_WispFactor));

    float3 baseColor = CZY_Clouds_Cirrus_Color.rgb;

    output = float4(baseColor, coverage * CZY_Base_Weather_CirrusCoverage * CZY_Clouds_Cirrus_Density);
}

void ComputeContrailsCloudLayer(float3 viewDir, float sceneDepth, float worldDepth, out float depth, out float4 output) {
    float clip;
    float3 normal;

    float2 windOffset = float2(CZY_Base_Weather_CloudWindOffset.x, CZY_Base_Weather_CloudWindOffset.z);
    float2 baseUV = GetUVForLayerSpherical(CZY_Clouds_Contrails_Height, viewDir, normal, depth, clip);
    float2 uv1 = baseUV * float2(0.0055, 0.0025) * CZY_Clouds_Contrails_TextureScale.xy + windOffset * CZY_Clouds_Contrails_WindStrength * 0.005;
    float2 uv2 = baseUV * float2(0.0055, 0.0025) * CZY_Clouds_Contrails_TextureScale.xy + windOffset * CZY_Clouds_Contrails_WindStrength * 0.002;
    uv2 = RotateUV(uv2, float2(0.5, 0.5), 40);

    float cloudCoverage = RemapValue(CZY_Base_Weather_ContrailCoverage, 0, 1, 0.0, 0.3);

    float sinCurve1 = saturate(sin(uv1.x * 7 + _Time.y * 0.16) + CZY_Base_Weather_ContrailCoverage);
    float sinCurveNoise1 = sin(uv1.x * 8.5 + _Time.y * 0.3);
    float sinCurveLengthVariation1 = saturate(sin(uv1.y * 7 + uv1.x * 3 + _Time.y * 0.26) + CZY_Base_Weather_ContrailCoverage);
    float combinedSin1 = sinCurve1 * sinCurveNoise1 * sinCurveLengthVariation1;

    float sinCurve2 = saturate(sin(uv1.x * 3 + _Time.y * 0.16) + CZY_Base_Weather_ContrailCoverage);
    float sinCurveNoise2 = sin(uv1.x * 6.5 + _Time.y * 0.3);
    float sinCurveLengthVariation2 = saturate(sin(uv1.y * 8 + uv1.x * 3 + _Time.y * 0.26) + CZY_Base_Weather_ContrailCoverage);
    float combinedSin2 = sinCurve2 * sinCurveNoise2 * sinCurveLengthVariation2;

    float smallPlacementSample1 = SAMPLE_TEXTURE2D(CZY_Clouds_Contrails_MainTexture, samplerCZY_Clouds_Contrails_MainTexture, (uv1)).r * combinedSin1;
    float smallPlacementSample2 = SAMPLE_TEXTURE2D(CZY_Clouds_Contrails_MainTexture, samplerCZY_Clouds_Contrails_MainTexture, (uv2)).r * combinedSin2;

    float coverage = max(smallPlacementSample1, smallPlacementSample2);

    float3 baseColor = CZY_Clouds_Contrails_Color.rgb;

    output = float4(baseColor, coverage * CZY_Base_Weather_ContrailCoverage);
}

void COZYClouds_float(float2 screenUV, out float4 output) {
    float rawDepth = SampleSceneDepth(screenUV);
    float depth01 = Linear01Depth(rawDepth, _ZBufferParams);
    float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
    float worldDepth = distance(_WorldSpaceCameraPos, worldPos);

    float4 fogColor = SAMPLE_TEXTURE2D(_FogTex, sampler_FogTex, screenUV);
    float4 skyColor = SAMPLE_TEXTURE2D(_SkyTex, sampler_SkyTex, screenUV);

    float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos.xyz);
    float radialToPlanarLinear = depth01 / (worldDepth + 0.00001);


    float4 altocumulusColor;
    float altocumulusDepth;
    #if defined(CZY_USE_CLOUDSALTOCUMULUS)
    ComputeAltocumulusCloudLayer(viewDir, depth01, worldDepth, altocumulusDepth, altocumulusColor);
    altocumulusColor = float4(lerp(altocumulusColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_CloudsFogAmount), altocumulusColor.a);
    #endif

    float4 cirrusColor;
    float cirrusDepth;
    #if defined(CZY_USE_CLOUDSCIRRUS)
    ComputeCirrusCloudLayer(viewDir, depth01, worldDepth, cirrusDepth, cirrusColor);
    cirrusColor = float4(lerp(cirrusColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_CloudsFogAmount), cirrusColor.a);
    #endif

    float4 contrailsColor;
    float contrailsDepth;
    #if defined(CZY_USE_CLOUDSCONTRAILS)
    ComputeContrailsCloudLayer(viewDir, depth01, worldDepth, contrailsDepth, contrailsColor);
    contrailsColor = float4(lerp(contrailsColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_CloudsFogAmount), contrailsColor.a);
    #endif

    // Initialize an empty final color and alpha
    float4 finalColor = float4(lerp(skyColor.rgb, fogColor.rgb, fogColor.a), 0);

    #if defined(CZY_USE_CLOUDSCIRRUS)
    // Blend Cirrus (Back layer)
    finalColor.rgb = lerp(finalColor.rgb, cirrusColor.rgb, saturate(cirrusColor.a * 10));
    finalColor.a = cirrusColor.a + finalColor.a * (1.0 - cirrusColor.a);
    #endif

    #if defined(CZY_USE_CLOUDSALTOCUMULUS)
    // Blend Altocumulus
    // finalColor.rgb = (altocumulusColor.rgb * altocumulusColor.a) + finalColor.rgb * (1.0 - altocumulusColor.a);
    finalColor.rgb = lerp(finalColor.rgb, altocumulusColor.rgb, saturate(altocumulusColor.a * 10));
    // finalColor.a = altocumulusColor.a + finalColor.a * (1.0 - altocumulusColor.a);
    finalColor.a = max(finalColor.a, altocumulusColor.a);
    #endif

    #if defined(CZY_USE_CLOUDSCONTRAILS)
    finalColor.rgb = (contrailsColor.rgb * contrailsColor.a) + finalColor.rgb * (1.0 - contrailsColor.a);
    finalColor.a = contrailsColor.a + finalColor.a * (1.0 - contrailsColor.a);
    #endif

    // Blend Cumulus
    #if defined(CZY_USE_CLOUDSCUMULUS)
    float4 cumulusColor;
    float cumulusDepth;
    ComputeCumulusCloudLayer(viewDir, depth01, worldDepth, cumulusDepth, cumulusColor);
    cumulusColor = float4(lerp(cumulusColor.rgb, fogColor.rgb, fogColor.a * CZY_Fog_Layers_CloudsFogAmount), cumulusColor.a);

    finalColor.rgb = (cumulusColor.rgb * cumulusColor.a) + finalColor.rgb * (1.0 - cumulusColor.a);
    finalColor.a = cumulusColor.a + finalColor.a * (1.0 - cumulusColor.a);
    #endif

    finalColor = float4(AdjustHSV(finalColor.rgb, CZY_Base_Weather_SaturationFilter, 0), finalColor.a);

    output = finalColor;

}