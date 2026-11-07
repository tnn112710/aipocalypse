#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyBaseVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozySkyVariables.hlsl"
#include_with_pragmas "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyFogVariables.hlsl"
#include "Packages/space.distantlands.cozyweather.core/Runtime/Rendering/Shaders/Data/Includes/CozyUtilities.hlsl"

void ComputeSkyGradient(float3 viewDirection, out float4 skyGradient, out float mask)
{
    #ifndef CZY_USE_SKYGRADIENT
    skyGradient = float4(0, 0, 0, 0);
    mask = 1;
    #else
    float skyGradientDot = dot(viewDirection, float3(0, 1, 0)) + CZY_Sky_Gradient_Offset;

    float4 zenithColor = CZY_Sky_Gradient_ZenithColor;
    float4 horizonColor = CZY_Sky_Gradient_HorizonColor;
    float4 groundColor = CZY_Sky_Gradient_GroundColor;

    #if CZY_USE_SKYTWOTONE

    float twoToneDot = dot(viewDirection, CZY_Base_Orientation_SunDirection) + CZY_Sky_TwoTone_Offset;
    float twoTonePowNegative = -pow(abs( -twoToneDot), CZY_Sky_TwoTone_Exponent);
    float twoTonePow = pow(abs(twoToneDot), CZY_Sky_TwoTone_Exponent);
    float finalT = (lerp(twoTonePowNegative, twoTonePow, sign(saturate(twoToneDot))) + 1) / 2;
    horizonColor *= lerp(CZY_Sky_TwoTone_SunSideColor, CZY_Sky_TwoTone_AntiSunSideColor, saturate(finalT));
    zenithColor *= lerp(CZY_Sky_TwoTone_SunSideColor, CZY_Sky_TwoTone_AntiSunSideColor, saturate(finalT));
    groundColor *= lerp(CZY_Sky_TwoTone_SunSideColor, CZY_Sky_TwoTone_AntiSunSideColor, saturate(finalT));

    #endif

    float zenithGradientTime = pow(1 - saturate(skyGradientDot), CZY_Sky_Gradient_ZenithExponent);
    float4 zenithGradient = lerp(zenithColor, horizonColor, zenithGradientTime);

    float nadirGradientTime = pow(1 - saturate( -skyGradientDot), CZY_Sky_Gradient_GroundExponent);
    float4 nadirGradient = lerp(groundColor, horizonColor, nadirGradientTime);

    float3 compiledGradient = lerp(nadirGradient, zenithGradient, sign(saturate(skyGradientDot))).rgb;

    skyGradient = float4(compiledGradient.xyz, 1);
    mask = saturate((skyGradientDot * 20) + 0.15);
    #endif
}

void ComputeSun(float3 viewDirection, out float4 sunHalo, out float4 sunDisk)
{
    #ifndef CZY_USE_SKYSUNDISK
    sunHalo = float4(0, 0, 0, 0);
    sunDisk = float4(0, 0, 0, 0);
    #else
    float sunDot = dot( -viewDirection, CZY_Base_Orientation_SunDirection);
    sunHalo = pow(saturate(sunDot), CZY_Sky_SunDisk_HaloFalloff * 50) * CZY_Sky_SunDisk_HaloColor;

    float3 sunRight = normalize(cross(CZY_Base_Orientation_SunDirection, float3(0, 1, 0)));
    float3 sunUp = normalize(cross(CZY_Base_Orientation_SunDirection, sunRight));

    float2 sunUV = float2(dot(sunRight, viewDirection), -dot(sunUp, viewDirection)) * (50 / CZY_Sky_SunDisk_Size).xx;
    float2 uv = (sunUV + 1.0) * 0.5;
    float mask = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
    float4 sampledSunTexture = SAMPLE_TEXTURE2D(CZY_Sky_SunDisk_Texture, samplerCZY_Sky_SunDisk_Texture, saturate(uv)) * mask;

    float eclipseDirectionLength = length(CZY_Sky_Eclipse_Direction);
    if (CZY_Sky_Eclipse_Size > 0.0 &&
        (CZY_Sky_Eclipse_Progress >= 0.0 || eclipseDirectionLength > 1e-4))
    {
        float eclipseRadius = max(0.0, CZY_Sky_Eclipse_Size);
        float2 eclipseCenterUV;
        if (CZY_Sky_Eclipse_Progress >= 0.0)
        {
            eclipseCenterUV = float2(
                (1.0 + eclipseRadius) * (1.0 - saturate(CZY_Sky_Eclipse_Progress)),
                0.0);
        }
        else
        {
            float3 eclipseMoonDirection = normalize(-CZY_Sky_Eclipse_Direction);
            eclipseCenterUV = float2(
                dot(sunRight, eclipseMoonDirection),
                -dot(sunUp, eclipseMoonDirection)) * (50 / CZY_Sky_SunDisk_Size).xx;
        }

        float eclipseDistance = length(sunUV - eclipseCenterUV);
        float antialiasWidth = max(fwidth(eclipseDistance), 1e-4);
        float moonOcclusion = 1.0 - smoothstep(
            eclipseRadius - antialiasWidth,
            eclipseRadius + antialiasWidth,
            eclipseDistance);
        sampledSunTexture *= 1.0 - moonOcclusion;
        sunHalo *= 1.0 - moonOcclusion;
    }

    sunDisk = sampledSunTexture * CZY_Sky_SunDisk_Color * sign(saturate(sunDot + 0.5));
    #endif
}

float4 ComputeMoon(float3 viewDirection, out float4 moonDisk, out float cullingMask)
{
    #ifndef CZY_USE_SKYMOONDISK
    moonDisk = float4(0, 0, 0, 0);
    cullingMask = 1.0;
    #else
    
    float3 moonDir = normalize(CZY_Base_Orientation_MoonDirection);
    float3 sunDir = normalize(CZY_Base_Orientation_SunDirection);
    float moonDot = dot(viewDirection, moonDir);

    float3 upVec = abs(moonDir.z) > 0.99 ? float3(0, 1, 0) : float3(0, 0, 1);
    float3 moonRight = normalize(cross(upVec, moonDir));
    float3 moonUp = cross(moonDir, moonRight);

    float scale = 20.0 / max(0.001, CZY_Sky_MoonDisk_Size);
    float2 moonUV = float2(dot(moonRight, viewDirection), -dot(moonUp, viewDirection)) * scale;
    float2 uv = moonUV + 0.5; 

    uv = RotateUV(uv, float2(0.5, 0.5), CZY_Sky_MoonDisk_Rotation * 2.0 * 3.14159265);

    float inBounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0) * step(0.0, moonDot);

    float4 sampledMoonTexture = SAMPLE_TEXTURE2D(CZY_Sky_MoonDisk_Texture, samplerCZY_Sky_MoonDisk_Texture, uv) * inBounds;
    cullingMask = 1.0 - sampledMoonTexture.a;

    float2 remappedUV = (uv - 0.5) * 2.0;
    float r2 = dot(remappedUV, remappedUV);
    float diskMask = step(r2, 1.0);

    float z = sqrt(max(0.0, 1.0 - r2));
    float3 spherizedNormals = float3(remappedUV.x, -remappedUV.y, z);

    float3 unpackedNormalMap = UnpackNormal(SAMPLE_TEXTURE2D(CZY_Sky_MoonDisk_NormalMap, samplerCZY_Sky_MoonDisk_NormalMap, uv)) * CZY_Sky_MoonDisk_NormalStrength;
    float3 combinedNormals = normalize(spherizedNormals + float3(unpackedNormalMap.xy, 0.0));

    float2 sunTangentXY = float2(dot(sunDir, moonRight), dot(sunDir, moonUp));
    float sunTangentLen = length(sunTangentXY);

    float3 tangentLightDirection;

    if (sunTangentLen > 1e-4) 
    {
        // Realistic Phase Mode
        tangentLightDirection = normalize(float3(sunTangentXY, dot(sunDir, -moonDir)));
    }
    else 
    {
        // Fake Phase Mode
        float phaseK = saturate(CZY_Base_Time_MoonPhasePercentage) * 2.0 - 1.0;
        float phaseS = sqrt(saturate(1.0 - phaseK * phaseK));
        
        tangentLightDirection = normalize(float3(CZY_Base_Time_MoonPhaseSign * phaseS, 0.0, phaseK));
    }

    float moonPhaseMask = 1 - saturate(dot(combinedNormals, tangentLightDirection) * 3.0 + 0.5) * diskMask;

    moonDisk = sampledMoonTexture * CZY_Sky_MoonDisk_Color * moonPhaseMask * inBounds;
    #endif

    return moonDisk;
}

void ComputeStars(float3 viewDirection, float3 cubemapUVW, out float4 starColor)
{
    #ifndef CZY_USE_SKYSTARS
    starColor = float4(0, 0, 0, 0);
    #else

    float4 starTwinkleOffset = SAMPLE_TEXTURECUBE_LOD(CZY_Sky_Stars_TwinkleNoise, samplerCZY_Sky_Stars_TwinkleNoise, cubemapUVW, 0);

    float twinkleAmount = (sin((_Time.y + starTwinkleOffset.g) * starTwinkleOffset.r * CZY_Sky_Stars_TwinkleSpeed * 5) + 1) * 0.5;

    twinkleAmount = lerp(1, twinkleAmount, CZY_Sky_Stars_TwinkleAmount);

    starColor = SAMPLE_TEXTURECUBE_LOD(CZY_Sky_Stars_Cubemap, samplerCZY_Sky_Stars_Cubemap, cubemapUVW, 0) * CZY_Sky_Stars_Color * CZY_Sky_Stars_Intensity * twinkleAmount;
    #endif
}

void ComputeGalaxy(float3 viewDirection, float3 cubemapUVW, out float4 galaxyColor)
{
    #ifndef CZY_USE_SKYGALAXY
    galaxyColor = float4(0, 0, 0, 0);
    #else
    float4 galaxySample = SAMPLE_TEXTURECUBE_LOD(CZY_Sky_Galaxy_Cubemap, samplerCZY_Sky_Galaxy_Cubemap, cubemapUVW, 0);
    float3 galaxyMask = galaxySample.xyz;

    galaxyColor = (CZY_Sky_Galaxy_PrimaryColor * galaxyMask.r + CZY_Sky_Galaxy_SecondaryColor * galaxyMask.g + CZY_Sky_Galaxy_TertiaryColor * galaxyMask.b) * length(galaxySample.rgb) * CZY_Sky_Galaxy_Intensity;
    #endif
}

void ComputeConstellations(float3 viewDirection, float3 cubemapUVW, out float4 constellationColor)
{
    #ifndef CZY_USE_SKYCONSTELLATIONS
    constellationColor = float4(0, 0, 0, 0);
    #else
    float4 maskColor = CZY_Sky_Constellations_Intensity * CZY_Sky_Constellations_Color;

    float4 constellationSample = SAMPLE_TEXTURECUBE_LOD(CZY_Sky_Constellations_Cubemap, samplerCZY_Sky_Constellations_Cubemap, cubemapUVW, 0);

    float timeVal = _Time.y * 0.05 * CZY_Sky_Constellations_ShimmerSpeed;
    float offset = constellationSample.g;
    float spread = 5.0;
    float fadeDuration = 0.5;
    float t = frac(timeVal - offset * spread);
    float fade = saturate(1 - smoothstep(0.0, 0.2, t) * (1 - smoothstep(0.8, 1.0, t)));

    constellationColor = constellationSample.r * lerp(1, fade, CZY_Sky_Constellations_ShimmerAmount) * maskColor;
    #endif
}

float4 ComputeLightColumns(float3 viewDirection)
{
    #ifndef CZY_USE_SKYLIGHTCOLUMNS
    return float4(0, 0, 0, 0);
    #else
    float u = (atan2(viewDirection.x, viewDirection.z) / 12.5663706144) + 0.5;
    u = frac(u);
    float offsetU = frac(u + _Time.x * 0.1);

    float v = saturate((pow((1 - CZY_Sky_LightColumns_Height) * 3 + 1, (1 - CZY_Sky_LightColumns_Height) * 3 + 1) * (viewDirection.y - CZY_Sky_LightColumns_Position) + 1) / 2);

    float4 main = SAMPLE_TEXTURE2D(CZY_Sky_LightColumns_Texture, samplerCZY_Sky_LightColumns_Texture, float2(u, v));
    float4 secondary = SAMPLE_TEXTURE2D(CZY_Sky_LightColumns_Texture, samplerCZY_Sky_LightColumns_Texture, float2(offsetU, v));

    return min(main, secondary) * CZY_Sky_LightColumns_Color * CZY_Sky_LightColumns_Intensity.xxxx;
    #endif
}

float3 Hash3(float n)
{
    n = frac(n / 10000) * 10000;
    // Faster random generator (no trig)
    return frac(sin(float3(n, n * 1.3, n * 2.1)) * 43758.5453);
}

float3 RotateCelestialSphere(float3 viewDirection) {
    float sunPitch = radians(CZY_Base_Orientation_SkyPitch - 90);
    float sunRotation = radians(CZY_Base_Orientation_SkyRotation);
    float dayRotation = CZY_Base_Time_DayPercentage * (CZY_Sky_Stars_Mode == 0 || CZY_Sky_Stars_Mode == 1 ? 1 : 0);
    float yearRotation = CZY_Base_Time_YearPercentage * (CZY_Sky_Stars_Mode == 0 || CZY_Sky_Stars_Mode == 2 ? 1 : 0);
    float skyPhase = (dayRotation + yearRotation + CZY_Sky_Stars_RotationOffset) * 2 * 3.14159265;
    return RotateAboutAxis(RotateAboutAxis(RotateAboutAxis(viewDirection, float3(0, -1, 0), sunRotation), float3(0, 0, -1), sunPitch), float3(0, -1, 0), skyPhase);
}

float4 ComputeShootingStars(float3 viewDirection)
{
    #ifndef CZY_USE_SKYSHOOTINGSTARS
    return float4(0, 0, 0, 0);
    #else
    float4 output = 0;

    // -- - Constants pulled out of loop -- -
    const float count = CZY_Sky_ShootingStars_ParticleCount;
    const float speed = CZY_Sky_ShootingStars_TravelDistance;
    const float lifetime = CZY_Sky_ShootingStars_Lifetime;
    const float delay = CZY_Sky_ShootingStars_Delay;
    const float totalLifetime = lifetime + delay;
    const float widthFactor = 1000.0 / CZY_Sky_ShootingStars_Width;
    const float intensity = CZY_Sky_ShootingStars_Intensity;

    float3 p = normalize(viewDirection);
    float seed = 0;

    for (int i = 0; i < count; i++)
    {
        float id = i + seed;

        float3 rand = Hash3(id * 13.13);
        float3 randDir = Hash3(id * 91.73);
        float3 offset = Hash3(i * 57.31);

        float timePhase = frac((_Time.y + offset.x * totalLifetime) / totalLifetime);
        float alive = saturate(timePhase * totalLifetime / lifetime);
        float brightness = saturate(sin(alive * 3.14159) * 10.0);
        float lifeMask = step(timePhase, 1.0);

        float3 startPos = normalize(rand * 2.0 - 1.0);
        startPos.y = abs(startPos.y);
        float3 dir = normalize(randDir * 2.0 - 1.0);
        dir.y = -abs(dir.y);
        dir = normalize(normalize(startPos + dir) - startPos);

        float3 start = normalize(startPos + dir * speed * alive);
        float3 end = normalize(startPos + dir * speed * saturate(alive * 2.0));
        float3 planeNormal = normalize(cross(start, end));

        float dist = abs(dot(p, planeNormal));
        float lineMask = saturate(1.0 - dist * widthFactor);

        float dotSE = dot(start, end);
        float betweenMask = step(dotSE, dot(start, p)) * step(dotSE, dot(p, end));

        float3 color = lerp(CZY_Sky_ShootingStars_Color, CZY_Sky_ShootingStars_SecondaryColor, offset.y);

        output.rgb += color * brightness * lineMask * betweenMask * lifeMask;
        output.a += brightness * lineMask * betweenMask * lifeMask;
    }

    return output * intensity;
    #endif
}

void ComputeRainbow(float3 viewDirection, out float4 rainbowColor) 
{
    #ifndef CZY_USE_SKYRAINBOW
    rainbowColor = float4(0,0,0,0);
    #else
    float d = (dot(viewDirection, CZY_Base_Orientation_SunDirection) + CZY_Sky_Rainbow_Placement) * 10;
    float u = d / CZY_Sky_Rainbow_Width + 0.5;

    float2 uv = float2(saturate(u), 0);
    float4 sampledColor = SAMPLE_TEXTURE2D(CZY_Sky_Rainbow_Texture, samplerCZY_Sky_Rainbow_Texture, uv);
    rainbowColor = sampledColor * sampledColor.a * CZY_Sky_Rainbow_Intensity * CZY_Base_Weather_Wetness;
    #endif
}

void COZYSky_float(float2 screenUV, float4 sceneBlit, out float4 output) {


    float3 viewDirection = BetterViewDirection(screenUV);
    float3 skyDirection = RotateCelestialSphere( -viewDirection);

    // float depth = Linear01Depth(SampleSceneDepth(screenUV), _ZBufferParams);

    float4 skyGradient = float4(0, 0, 0, 0);
    float skyMask = 1;
    float4 sunDisk = float4(0, 0, 0, 0);
    float4 sunHalo = float4(0, 0, 0, 0);
    float4 moonDisk = float4(0, 0, 0, 0);
    float moonMask = 1;
    float4 rainbowColor = float4(0, 0, 0, 0);
    float4 starColor = float4(0, 0, 0, 0);
    float4 galaxyColor = float4(0, 0, 0, 0);
    float4 constellationColor = float4(0, 0, 0, 0);
    float4 lightColumns = ComputeLightColumns(viewDirection);
    float4 shootingStars = ComputeShootingStars(viewDirection);
    float mask = 1;

    ComputeSkyGradient(viewDirection, skyGradient, skyMask);
    ComputeSun(viewDirection, sunHalo, sunDisk);
    float4 moon = ComputeMoon(viewDirection, moonDisk, moonMask);
    ComputeRainbow(viewDirection, rainbowColor);
    ComputeStars(viewDirection, -skyDirection, starColor);
    ComputeGalaxy(viewDirection, -skyDirection, galaxyColor);
    ComputeConstellations(viewDirection, -skyDirection, constellationColor);

    mask = min(mask, moonMask);

    float4 background = skyGradient;

    float4 additiveLayers = sunHalo + lightColumns + (starColor + constellationColor + galaxyColor + shootingStars) * mask + moon;
    additiveLayers = lerp(additiveLayers, float4(sunDisk.xyz, 1), sunDisk.w) * skyMask;

    output = background + additiveLayers + rainbowColor;
    output = float4(AdjustHSV(output.rgb, CZY_Base_Weather_SaturationFilter, CZY_Base_Weather_ValueFilter), output.a);
}