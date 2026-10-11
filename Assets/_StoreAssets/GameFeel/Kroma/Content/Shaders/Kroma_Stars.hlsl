#ifndef KROMA_STARS_INCLUDED
#define KROMA_STARS_INCLUDED

#include "Kroma_Shared.hlsl"

#ifndef PI
#define PI 3.14159265359
#endif

// Defined in Properties of parent shader
TEXTURECUBE(_StarCubeMap);
SAMPLER(sampler_StarCubeMap);

float _StarSpeed;
float _StarRotation;
float _StarLatitude;
float _StarSize;
// _StarExposure and others likely defined in Kroma_Shared.hlsl or Kroma_Colors.hlsl
// float _StarExposure;
// float4 _StarTint; 
// float _Star_Blend;


// Helper: 3x3 Rotation Matrix
float3x3 AngleAxis3x3(float angle, float3 axis)
{
    float c, s;
    sincos(angle, s, c);
    float t = 1.0 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;
    return float3x3(
        t * x * x + c, t * x * y - s * z, t * x * z + s * y,
        t * x * y + s * z, t * y * y + c, t * y * z - s * x,
        t * x * z - s * y, t * y * z + s * x, t * z * z + c
    );
}

// Rotate the view direction, tilt with latitude, spin with time
float3 GetStarUVW(float3 viewDir, float latitude, float localSiderealTime)
{
    // tilt = 0 at the north pole, where latitude = 90 degrees
    float tilt = PI * (latitude - 90.0) / 180.0;
    float3x3 tiltRotation = AngleAxis3x3(tilt, float3(1, 0, 0));
    
    // 0.75 is a texture offset for lST = 0 equals noon
    float spin = (0.75 - localSiderealTime) * 2.0 * PI;
    float3x3 spinRotation = AngleAxis3x3(spin, float3(0, 1, 0));
    
    // The order of rotation is important
    float3x3 fullRotation = mul(spinRotation, tiltRotation);
    return mul(fullRotation, viewDir);
}

float3 CalculateStars(float3 viewDir, float seedTime, float3 positionOS)
{
    #if defined(_STARS_ON)
        
        // --- Rotation Logic ---
        // Combines automatic time-based rotation with manual offset
        float currentTime = (seedTime * _StarSpeed) + _StarRotation;
        float3 starUVW = GetStarUVW(viewDir, _StarLatitude, frac(currentTime));

        // --- Sampling ---
        float3 starColor = SAMPLE_TEXTURECUBE_BIAS(_StarCubeMap, sampler_StarCubeMap, starUVW, -1.0).rgb;
        
        // --- Size Logic (Simplified) ---
        // Safety: Clamp size to avoid division by zero or negative powers.
        float safeSize = max(_StarSize, 0.001);
        float effectivePower = 1.0 / safeSize;
        effectivePower = max(effectivePower, 0.1); 
        
        starColor = pow(abs(starColor), effectivePower);
        starColor = max(starColor, 0.0);
        
        starColor *= exp2(_StarExposure);

        // --- New Controls ---
        starColor *= _StarTint.rgb;

        // Horizon Fade
        float horizonMask = smoothstep(0.0, _Star_Blend, viewDir.y);
        horizonMask = pow(max(horizonMask, 0.001), _Star_Blend_Contrast);
        starColor *= horizonMask;

        return starColor;
        
    #else
        return float3(0,0,0);
    #endif
}

#endif // KROMA_STARS_INCLUDED
