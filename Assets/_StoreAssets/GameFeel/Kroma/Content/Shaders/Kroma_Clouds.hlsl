#ifndef KROMA_CLOUDS_INCLUDED
#define KROMA_CLOUDS_INCLUDED

#include "Kroma_Shared.hlsl"

// Triplanar or Dual-Layer Cloud Sampling


TEXTURE2D(_Cloud2_Texture);
SAMPLER(sampler_Cloud2_Texture);

// Optional Detail Textures
TEXTURE2D(_Cloud_Detail_Texture);
SAMPLER(sampler_Cloud_Detail_Texture);

TEXTURE2D(_Cloud2_Detail_Texture);
SAMPLER(sampler_Cloud2_Detail_Texture);

// Helper Function: Render Single Cloud Layer
float4 RenderCloudLayer(
    float3 viewDir, float3 timeParams,
    TEXTURE2D_PARAM(tex, smp),
    TEXTURE2D_PARAM(detailTex, detailSmp),
    float4 color, float4 highlightColor,
    float scale, float2 speed, float density, float intensity, float erosion,
    float highlightControl, float highlightContrast, float highlightSmoothness,
    float horizonLevel, float horizonFade, float topFade,
    float baseSpeedMult, float detailSpeedMult, float detailScale, float erosionMult, float alphaEdge,
    float heightScale, float lightingWrap, float sunBoost, float scatterMin, float scatterMax, float heightBias,
    float baseEpsilon, float detailEpsilon,
    bool useHighlights, bool useDetailTex
)
{
    // Volumetric Cloud Lighting (Rembrandt / Horizon Zero Dawn approx)
    
    // 1. Setup Sampling
    // Project ViewDir to UVs (Spherical / planar projection approximation for sky)
    float2 uv = viewDir.xz / (viewDir.y + heightBias);
    float time = timeParams.x;
    
    // Optimize: Reduce tiling to reasonable numbers if input is too high
    float2 baseUV = uv * scale;
    
    // Animation Speeds
    float2 speed1 = speed * baseSpeedMult * time;
    float2 speed2 = speed * detailSpeedMult * time;

    // 2. Multi-Tap Sampling and Highlight Logic
    float center, lighting;
    
    if (useHighlights)
    {
         // Step size for finite difference (epsilon). 
         float eps = baseEpsilon + highlightSmoothness * detailEpsilon; 

         float3 offsets[3]; 
         offsets[0] = float3(0,0,0);      // Center
         offsets[1] = float3(eps,0,0);    // Right
         offsets[2] = float3(0,eps,0);    // Forward
         
         float samples[3];
         
         [unroll]
         for(int i=0; i<3; i++)
         {
             float2 p = baseUV + offsets[i].xy + speed1;
             float val = SAMPLE_TEXTURE2D(tex, smp, p).r;
             
             float2 pDet = (baseUV + offsets[i].xy) * detailScale + speed2;
             float det = 0;
             if (useDetailTex) det = SAMPLE_TEXTURE2D(detailTex, detailSmp, pDet).r;
             else              det = SAMPLE_TEXTURE2D(tex, smp, pDet).r;
             
             samples[i] = saturate(val - (det * erosion * erosionMult));
         }
         
         center = samples[0];
         float right = samples[1];
         float forward = samples[2];
         
         // 3. Calculate Normal
         float3 normal = normalize(float3(center - right, heightScale * eps, center - forward));
         
         // 4. Calculate Lighting (N dot L)
         float3 lightDir = _MainLightPosition.xyz;
         if (length(lightDir) < 0.1) lightDir = float3(0, 1, 0); 
         lightDir = normalize(lightDir);
         
         float ndotl = dot(normal, lightDir);
         float remappedNL = ndotl * lightingWrap + (1.0 - lightingWrap);
         
         float edge = clamp(highlightSmoothness * 0.5, 0.0, 1.0);
         lighting = smoothstep(0.5 - edge, 0.5 + edge, remappedNL);
         lighting = pow(lighting, highlightContrast);
    }
    else
    {
         // Simple Sampling
         float2 p = baseUV + speed1;
         center = SAMPLE_TEXTURE2D(tex, smp, p).r;
         
         // Erosion still applies for shape!
         float2 pDet = baseUV * detailScale + speed2;
         float det = 0;
         if (useDetailTex) det = SAMPLE_TEXTURE2D(detailTex, detailSmp, pDet).r;
         else              det = SAMPLE_TEXTURE2D(tex, smp, pDet).r;
         
         center = saturate(center - (det * erosion * erosionMult));
         
         lighting = 0.5;
    }
    
    // 5. Final Intensity and Density
    float combinedNoise = center;
    
    float fadeVal = viewDir.y * topFade; 
    float topFadeMultiplier = 1.0 - smoothstep(0.0, 1.0, fadeVal);

    float d = density * topFadeMultiplier;
    d = max(0.001, d);
    
    float cloudVal = smoothstep(1.0 - d, 1.0 - d + alphaEdge, combinedNoise);
    
    // Horizon Fade
    float hFade = smoothstep(horizonLevel, horizonLevel + horizonFade, viewDir.y);

    float finalAlpha = cloudVal * intensity * hFade;
    finalAlpha = saturate(finalAlpha);

    // 6. Apply Lighting
    float3 finalColor = color.rgb;

    if (useHighlights)
    {
        float3 lightDir2 = _MainLightPosition.xyz;
        if (length(lightDir2) < 0.1) lightDir2 = float3(0, 1, 0);
        lightDir2 = normalize(lightDir2);

        float sunDot = max(0, dot(viewDir, lightDir2));
        float scatter = pow(sunDot, lerp(scatterMin, scatterMax, highlightControl));
        float boost = scatter * highlightControl * sunBoost;
        
        float totalLight = saturate(lighting + boost);
        finalColor = lerp(color.rgb, highlightColor.rgb, totalLight);
    }

    return float4(finalColor, finalAlpha);
}

float4 CalculateClouds(float3 viewDir, float3 timeParams)
{
    #if defined(_CLOUDS_ON)
        bool useHighlights = false;
        #if defined(_CLOUD_HIGHLIGHTS_ON)
            useHighlights = true;
        #endif

        bool useDetail = false;
        #if defined(_CLOUD_DETAIL_TEX_ON)
            useDetail = true;
        #endif

        return RenderCloudLayer(
            viewDir, timeParams,
            _Cloud_Texture, sampler_Cloud_Texture,
            _Cloud_Detail_Texture, sampler_Cloud_Detail_Texture,
            _Color_1, _CloudHighlightColor,
            _Scale, _Speed.xy, _Density, _Intensity, _CloudErosion,
            _CloudHighlightControl, _CloudHighlightContrast, _CloudHighlightSmoothness,
            _Horizon_Level, _Horizon_Fade, _Top_Fade,
            _CloudBaseSpeedMultiplier, _CloudDetailSpeedMultiplier, _CloudDetailScale, _CloudErosionMultiplier, _CloudAlphaEdge,
            _CloudHeightScale, _CloudLightingWrap, _CloudSunBoost, _CloudScatterMin, _CloudScatterMax, _CloudHeightBias,
            _CloudBaseEpsilon, _CloudDetailEpsilon,
            useHighlights, useDetail
        );
    #else
        return float4(0,0,0,0);
    #endif
}

float4 CalculateClouds2(float3 viewDir, float3 timeParams)
{
    #if defined(_CLOUDS2_ON)
        bool useHighlights = false;
        #if defined(_CLOUD2_HIGHLIGHTS_ON)
            useHighlights = true;
        #endif

        bool useDetail = false;
        #if defined(_CLOUD2_DETAIL_TEX_ON)
            useDetail = true;
        #endif

        return RenderCloudLayer(
            viewDir, timeParams,
            _Cloud2_Texture, sampler_Cloud2_Texture,
            _Cloud2_Detail_Texture, sampler_Cloud2_Detail_Texture,
            _Cloud2_Color, _Cloud2_HighlightColor,
            _Cloud2_Scale, _Cloud2_Speed.xy, _Cloud2_Density, _Cloud2_Intensity, _Cloud2_Erosion,
            _Cloud2_HighlightControl, _Cloud2_HighlightContrast, _Cloud2_HighlightSmoothness,
            _Cloud2_Horizon_Level, _Cloud2_Horizon_Fade, _Cloud2_Top_Fade,
            _Cloud2_BaseSpeedMultiplier, _Cloud2_DetailSpeedMultiplier, _Cloud2_DetailScale, _Cloud2_ErosionMultiplier, _Cloud2_AlphaEdge,
            _Cloud2_HeightScale, _Cloud2_LightingWrap, _Cloud2_SunBoost, _Cloud2_ScatterMin, _Cloud2_ScatterMax, _Cloud2_HeightBias,
            _Cloud2_BaseEpsilon, _Cloud2_DetailEpsilon,
            useHighlights, useDetail
        );
    #else
        return float4(0,0,0,0);
    #endif
}

#endif // KROMA_CLOUDS_INCLUDED
