#ifndef KROMA_SHARED_INCLUDED
#define KROMA_SHARED_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// --------------------------------------------------
// CBUFFER Definitions
// --------------------------------------------------
CBUFFER_START(UnityPerMaterial)
    // Sky Colors
    float4 _TopColor;
    float4 _MidColor;
    float4 _HorizonColor;
    float4 _Ground_Color;
    float _Ground_Blend;
    float _Ground_Horizon_Smoothness;
    
    // Sky Settings
    float _Horizon_Offset;
    float _Mid_Sky_Height;
    float _Horizon_Softness;
    float _Mid_Sky_Softness;

    float _Top_Fade;
    float _Horizon_Level;
    float _Horizon_Fade;
    float _GlobalRotation;
    

    // Clouds
    float4 _Color_1;
    float4 _CloudHighlightColor;
    float _CloudHighlightControl;
    float _CloudLightingContrast; // Deprecated, kept for safety? No, removing as we did hard rename.
    float _CloudHighlightContrast;
    float _CloudHighlightSmoothness;
    float _CloudErosion;
    float4 _Cloud_Texture_TexelSize;
    float _Scale;
    float2 _Speed;
    float _Density;
    float _CloudHeightBias;
    
    // Cloud 1 Advanced
    float _CloudDetailScale;
    float _CloudDetailSpeedMultiplier;
    float _CloudHeightScale;
    float _CloudErosionMultiplier;
    float _CloudSunBoost;
    float _CloudBaseSpeedMultiplier;
    float _CloudBaseEpsilon;
    float _CloudDetailEpsilon;
    float _CloudLightingWrap;
    float _CloudAlphaEdge;
    float _CloudScatterMin;
    float _CloudScatterMax;

    float _Intensity;
    float _CloudsToggle;
    
    // Clouds 2
    float _Clouds2Toggle;
    float _Cloud2HighlightsToggle;
    float4 _Cloud2_Color;
    float4 _Cloud2_HighlightColor;
    float _Cloud2_HighlightControl;
    float _Cloud2_HighlightContrast;
    float _Cloud2_HighlightSmoothness;
    float _Cloud2_Erosion;
    float _Cloud2_Scale;
    float4 _Cloud2_Speed;
    float _Cloud2_Density;
    float _Cloud2_Intensity;
    
    float _Cloud2_DetailScale;
    float _Cloud2_DetailSpeedMultiplier;
    float _Cloud2_HeightScale;
    float _Cloud2_ErosionMultiplier;
    float _Cloud2_SunBoost;
    float _Cloud2_BaseSpeedMultiplier;
    float _Cloud2_BaseEpsilon;
    float _Cloud2_DetailEpsilon;
    float _Cloud2_LightingWrap;
    float _Cloud2_AlphaEdge;
    float _Cloud2_ScatterMin;
    float _Cloud2_ScatterMax;
    float _Cloud2_HeightBias;
    
    float _Cloud2_Horizon_Level;
    float _Cloud2_Horizon_Fade;
    float _Cloud2_Top_Fade;
    // AAA Stars
    float _StarsToggle;
    float4 _StarTint;
    float _StarDensity;
    float _StarSizeMin;
    float _StarSizeMax;
    float _StarExposure;
    float _StarSoftness;
    float _StarTwinkleSpeed;
    float _StarTwinkleAmount;
    float _StarColorTempMin;
    float _StarColorTempMax;
    float _StarSaturation;
    float _Stars_Scatter_Size;

    float _Star_Scatter;
    float _Scatter_Contrast;
    float _Star_Blend;
    float _Star_Blend_Contrast;
    float _ScatterDebug;

    // Celestial (Sun/Moon)
    float4 _Tint;
    float4 _Texture_TexelSize;
    float _Size;
    float _CelestialEdgeSoftness;
    float _Rotation;
    float _Phase;
    float _PhaseSize;
    float _PhaseSmoothness;
    float _DarkSideOcclusion; // NEW
    float _Fade;
    float _GlowEnabled;
    float4 _GlowColor;
    float _GlowPower;
    float _GlowIntensity;
    // Removed AlphaMultiplier/ShadowStrength as per user request
    float _AtmosphereEnabled;
    float4 _AtmosphereColor;
    float _AtmosphereSize;
    float _AtmosphereBlur;
    float _AtmosphereGlowPosition; // Was Phase Influence
    float _AtmosphereOpacity;
    
    // Atmosphere 2 (Outer Glow 2)
    float _Atmosphere2Enabled;
    float4 _Atmosphere2Color;
    float _Atmosphere2Size;
    float _Atmosphere2Blur;
    float _Atmosphere2GlowPosition; // Was Phase Influence
    float _Atmosphere2Opacity;
    float2 _Tiling;
    float _CelestialToggle;
    float _RotateWithCamera;
    float _InvertPhase; // NEW

    // Celestial 1
    float _C1_Enabled;
    float4 _C1_Tint;
    float _C1_Size;
    float _C1_EdgeSoftness;
    float _C1_Rotation;
    float _C1_Phase;
    float _C1_PhaseSize;
    float _C1_PhaseSmoothness;
    float _C1_DarkSideOcclusion; // NEW
    float _C1_Azimuth;
    float _C1_Altitude;
    float _C1_Fade;
    float _C1_GlowEnabled;
    float4 _C1_GlowColor;
    float _C1_GlowPower;
    float _C1_GlowIntensity;
    // Removed AlphaMultiplier/ShadowStrength
    float _C1_AtmosphereEnabled;
    float4 _C1_AtmosphereColor;
    float _C1_AtmosphereSize;
    float _C1_AtmosphereBlur;
    float _C1_AtmosphereGlowPosition; // Was Phase Influence
    float _C1_AtmosphereOpacity;
    
    // C1 Atmosphere 2
    float _C1_Atmosphere2Enabled;
    float4 _C1_Atmosphere2Color;
    float _C1_Atmosphere2Size;
    float _C1_Atmosphere2Blur;
    float _C1_Atmosphere2GlowPosition; // Was Phase Influence
    float _C1_Atmosphere2Opacity;
    float2 _C1_Tiling;
    float4 _C1_Texture_TexelSize;
    float _C1_RotateWithCamera;
    float _C1_InvertPhase; // NEW

    // Celestial 2
    float _C2_Enabled;
    float4 _C2_Tint;
    float _C2_Size;
    float _C2_EdgeSoftness;
    float _C2_Rotation;
    float _C2_Phase;
    float _C2_PhaseSize;
    float _C2_PhaseSmoothness;
    float _C2_DarkSideOcclusion; // NEW
    float _C2_Azimuth;
    float _C2_Altitude;
    float _C2_Fade;
    float _C2_GlowEnabled;
    float4 _C2_GlowColor;
    float _C2_GlowPower;
    float _C2_GlowIntensity;
    // Removed AlphaMultiplier/ShadowStrength
    float _C2_AtmosphereEnabled;
    float4 _C2_AtmosphereColor;
    float _C2_AtmosphereSize;
    float _C2_AtmosphereBlur;
    float _C2_AtmosphereGlowPosition; // Was Phase Influence
    float _C2_AtmosphereOpacity;
    
    // C2 Atmosphere 2
    float _C2_Atmosphere2Enabled;
    float4 _C2_Atmosphere2Color;
    float _C2_Atmosphere2Size;
    float _C2_Atmosphere2Blur;
    float _C2_Atmosphere2GlowPosition; // Was Phase Influence
    float _C2_Atmosphere2Opacity;
    float2 _C2_Tiling;
    float4 _C2_Texture_TexelSize;
    float _C2_RotateWithCamera;
    float _C2_InvertPhase; // NEW

    // Stars
    // Stars (Moved to top for AAA block)
    // float _StarsToggle;
    // float _StarHeight;
    // float _StarPower;
    // float _StarTwinkleSpeed;
    // float _Star_Blend;
    // float _Star_Scatter;    
    // float _Stars_Scatter_Size;
    // float _Scatter_Contrast;
    // float _ScatterDebug;
    // float4 _StarTint;

    // Legacy/Unused
    // float4 _StarTexture_TexelSize;
    // float _StarDensity;
    // float _StarExposure;
    // float _TwinkleSpeed;
    // float _TwinkleAmount;
    // float _StarHorizonFade;
    // float _StarTiling;
    
CBUFFER_END

// --------------------------------------------------
// Textures & Samplers
// --------------------------------------------------
TEXTURE2D(_Cloud_Texture);
SAMPLER(sampler_Cloud_Texture);

TEXTURE2D(_Texture); // Celestial Texture
SAMPLER(sampler_Texture);

TEXTURE2D(_C1_Texture);
SAMPLER(sampler_C1_Texture);

TEXTURE2D(_C2_Texture);
SAMPLER(sampler_C2_Texture);

// TEXTURE2D(_StarTexture);
// SAMPLER(sampler_StarTexture);

// --------------------------------------------------
// Structs
// --------------------------------------------------
struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionOS : TEXCOORD0; // Using this calculate ViewDir
    float2 uv         : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// --------------------------------------------------
// Helper Functions
// --------------------------------------------------

float3 RotateAroundYInDegrees(float3 vertex, float degrees)
{
    float alpha = degrees * (3.14159265359 / 180.0);
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
}

void Hash_Tchou_2_1_float(float2 cx, out float r)
{
    float2 s = float2(12.9898, 78.233);
    float2 k = float2(43758.5453, 43758.5453);
    r = frac(sin(dot(cx, s)) * k.x);
}

void Hash_Tchou_3_1_float(float3 cx, out float r)
{
    float3 s = float3(12.9898, 78.233, 151.7182);
    float3 k = float3(43758.5453, 43758.5453, 43758.5453);
    r = frac(sin(dot(cx, s)) * k.x);
}

// 2D Noise
float Unity_SimpleNoise_ValueNoise_Deterministic_float (float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);
    uv = abs(frac(uv) - 0.5);
    float2 c0 = i + float2(0.0, 0.0);
    float2 c1 = i + float2(1.0, 0.0);
    float2 c2 = i + float2(0.0, 1.0);
    float2 c3 = i + float2(1.0, 1.0);
    float r0; Hash_Tchou_2_1_float(c0, r0);
    float r1; Hash_Tchou_2_1_float(c1, r1);
    float r2; Hash_Tchou_2_1_float(c2, r2);
    float r3; Hash_Tchou_2_1_float(c3, r3);
    float bottomOfGrid = lerp(r0, r1, f.x);
    float topOfGrid = lerp(r2, r3, f.x);
    float t = lerp(bottomOfGrid, topOfGrid, f.y);
    return t;
}

// 3D Noise (AAA Stars)
float Unity_SimpleNoise_ValueNoise_3D_Deterministic_float (float3 uv)
{
    float3 i = floor(uv);
    float3 f = frac(uv);
    f = f * f * (3.0 - 2.0 * f);
    
    float3 c000 = i + float3(0,0,0);
    float3 c100 = i + float3(1,0,0);
    float3 c010 = i + float3(0,1,0);
    float3 c110 = i + float3(1,1,0);
    float3 c001 = i + float3(0,0,1);
    float3 c101 = i + float3(1,0,1);
    float3 c011 = i + float3(0,1,1);
    float3 c111 = i + float3(1,1,1);
    
    float r000; Hash_Tchou_3_1_float(c000, r000);
    float r100; Hash_Tchou_3_1_float(c100, r100);
    float r010; Hash_Tchou_3_1_float(c010, r010);
    float r110; Hash_Tchou_3_1_float(c110, r110);
    float r001; Hash_Tchou_3_1_float(c001, r001);
    float r101; Hash_Tchou_3_1_float(c101, r101);
    float r011; Hash_Tchou_3_1_float(c011, r011);
    float r111; Hash_Tchou_3_1_float(c111, r111);
    
    float x00 = lerp(r000, r100, f.x);
    float x10 = lerp(r010, r110, f.x);
    float x01 = lerp(r001, r101, f.x);
    float x11 = lerp(r011, r111, f.x);
    
    float y0 = lerp(x00, x10, f.y);
    float y1 = lerp(x01, x11, f.y);
    
    return lerp(y0, y1, f.z);
}

void Unity_SimpleNoise_Deterministic_float(float2 UV, float Scale, out float Out)
{
    float freq, amp;
    Out = 0.0f;
    freq = pow(2.0, float(0));
    amp = pow(0.5, float(3-0));
    Out += Unity_SimpleNoise_ValueNoise_Deterministic_float(float2(UV.xy*(Scale/freq)))*amp;
    freq = pow(2.0, float(1));
    amp = pow(0.5, float(3-1));
    Out += Unity_SimpleNoise_ValueNoise_Deterministic_float(float2(UV.xy*(Scale/freq)))*amp;
    freq = pow(2.0, float(2));
    amp = pow(0.5, float(3-2));
    Out += Unity_SimpleNoise_ValueNoise_Deterministic_float(float2(UV.xy*(Scale/freq)))*amp;
}

// 3D Simple Noise (AAA)
void Unity_SimpleNoise_3D_Deterministic_float(float3 UV, float Scale, out float Out)
{
    // Single octave usually enough for stars, but 2 adds variation
    float freq = 1.0;
    Out = Unity_SimpleNoise_ValueNoise_3D_Deterministic_float(UV * Scale * freq);
}

// -----------------------------------------------------------
// 3D Voronoi (Cellular) Noise for AAA Stars
// -----------------------------------------------------------
// Returns: x = distance to closest point, y = random value of cell
float2 Voronoi3D(float3 uv, float angleOffset, float cellDensity)
{
    float3 g = floor(uv * cellDensity);
    float3 f = frac(uv * cellDensity);
    float3 res = float3(8.0, 0.0, 0.0);
    float2 result = float2(1.0, 0.0);

    for(int z=-1; z<=1; z++)
    {
        for(int y=-1; y<=1; y++)
        {
            for(int x=-1; x<=1; x++)
            {
                float3 lattice = float3(x, y, z);
                float3 offset = lattice;
                
                // Random position in cell
                float hash;
                Hash_Tchou_3_1_float(g + lattice, hash);
                offset += sin(hash * angleOffset + hash * 6.28) * 0.5 + 0.5;
                
                float3 d = offset - f;
                float dist = dot(d, d);
                
                if(dist < result.x)
                {
                    result.x = dist;
                    result.y = hash; // Store hash for star brightness/color
                }
            }
        }
    }
    
    // sqrt dist for linear falloff
    result.x = sqrt(result.x);
    return result;
}

// 2D Voronoi (Cellular) Noise
// Returns: x = distance to closest point, y = random value of cell
float2 Voronoi2D(float2 uv, float angleOffset, float cellDensity)
{
    float2 g = floor(uv * cellDensity);
    float2 f = frac(uv * cellDensity);
    float2 result = float2(1.0, 0.0);

    for(int y=-1; y<=1; y++)
    {
        for(int x=-1; x<=1; x++)
        {
            float2 lattice = float2(x, y);
            float2 offset = lattice;
            
            // Random position in cell
            float hash;
            Hash_Tchou_2_1_float(g + lattice, hash);
            offset += sin(hash * angleOffset + hash * 6.28) * 0.5 + 0.5;
            
            float2 d = offset - f;
            float dist = dot(d, d);
            
            if(dist < result.x)
            {
                result.x = dist;
                result.y = hash;
            }
        }
    }
    
    result.x = sqrt(result.x);
    return result;
}

void Unity_RandomRange_float(float2 Seed, float Min, float Max, out float Out)
{
     float randomno =  frac(sin(dot(Seed, float2(12.9898, 78.233)))*43758.5453);
     Out = lerp(Min, Max, randomno);
}

// -----------------------------------------------------------
// Dithering
// -----------------------------------------------------------
float InterleavedGradientNoise(float2 pixCoord)
{
    const float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);
    float2 magicVal = magic.xy * pixCoord;
    return frac(magic.z * frac(magicVal.x + magicVal.y + magicVal.x * magicVal.y));
}

float3 ApplyDithering(float3 color, float2 screenUV, float intensity)
{
    // screenUV is now passed as RAW pixel coordinates (positionCS.xy)
    // We do NOT multiply it by _ScreenParams.xy because that causes
    // the noise grid to scale wildly or shrink when render passes (like probes)
    // use different resolutions.
    
    // We want the noise to always, permanently be exactly 1:1 with screen pixels.
    float noise = InterleavedGradientNoise(screenUV);
    
    // Convert to 0..1 range centered at 0.5? 
    // Unity's Standard dithering is usually -1/255 to +1/255
    // Remap 0..1 to -1..1 * 1/255
    
    float dither = (noise - 0.5) / 255.0;
    
    return color + (dither * intensity);
}


#endif // KROMA_SHARED_INCLUDED
