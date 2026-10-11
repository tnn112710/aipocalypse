#ifndef KROMA_CELESTIAL_INCLUDED
#define KROMA_CELESTIAL_INCLUDED

#include "Kroma_Shared.hlsl"

// Helper: Convert spherical Azimuth/Altitude to Direction Vector
float3 DirectionFromAzimuthAltitude(float azimuth, float altitude)
{
    float azRad = radians(azimuth);
    float altRad = radians(altitude);
    
    // Y is Up.
    float y = sin(altRad);
    float h = cos(altRad);
    float x = h * sin(azRad);
    float z = h * cos(azRad);
    
    return normalize(float3(x, y, z));
}

// Core Rendering Logic (Reusable)
// Updated signature: Replaced glowFresnel/glowSmoothness/glowOuterRadius with glowPower/glowIntensity
float4 RenderCelestialBody(float3 viewDir, float3 lightDir, float size, float rotationDeg, float rotateWithCamera, float phase, float invertPhase, float phaseMaskSize, float phaseSmooth, float darkSideOcclusion, float4 tint, float4 glowColor, float glowPower, float glowIntensity, float dim, Texture2D tex, SamplerState smp, float2 tiling, float edgeSoftness, float glowEnabled, float atmosphereEnabled, float4 atmosphereColor, float atmosphereSize, float atmosphereBlur, float atmosphereGlowPosition, float atmosphereOpacity, float atmosphere2Enabled, float4 atmosphere2Color, float atmosphere2Size, float atmosphere2Blur, float atmosphere2GlowPosition, float atmosphere2Opacity, out float occlusion)
{
    // Initialize occlusion
    occlusion = 0.0;
    
    float fadeVal = dim; 

    // Direction Check
    if (length(lightDir) < 0.1) lightDir = float3(0, 1, 0);
    else lightDir = normalize(lightDir);

    // Alignment (View relative to Light)
    float cosAngle = dot(viewDir, lightDir);
    if (cosAngle < 0.0) return float4(0,0,0,0);
    
    // Construct Basis
    float3 up = float3(0, 1, 0);
    
    if (rotateWithCamera > 0.5)
    {
        up = normalize(float3(unity_CameraToWorld._m01, unity_CameraToWorld._m11, unity_CameraToWorld._m21));
    }
    
    if (abs(dot(up, lightDir)) > 0.99) 
    {
        if (rotateWithCamera > 0.5)
             up = normalize(float3(unity_CameraToWorld._m00, unity_CameraToWorld._m10, unity_CameraToWorld._m20)); 
        else
             up = float3(0, 0, 1); 
    }

    float3 right = normalize(cross(lightDir, up));
    up = cross(right, lightDir);
    
    float x = dot(viewDir, right);
    float y = dot(viewDir, up);
    float2 localUV = float2(x, y);
    
    // Scale for Size
    float sVal = max(0.001, size);
    localUV /= sVal;
    
    // Rotation
    float finalRotation = rotationDeg;
    float rotRad = radians(finalRotation);
    float s = sin(rotRad);
    float c = cos(rotRad);
    float2x2 rotMat = float2x2(c, -s, s, c);
    localUV = mul(rotMat, localUV);
    
    // Texture Sample
    float2 texUV = localUV * tiling + 0.5;
    float dist = length(localUV);

    // ----------------------------
    // 1. CELESTIAL BODY GEOMETRY (Hard blocking disc)
    // ----------------------------
    float smoothness = max(0.001, edgeSoftness);
    float disc = 1.0 - smoothstep(0.5 - smoothness, 0.5, dist); 
    
    // ----------------------------
    // 2. PHASE & LIGHT MASK
    // ----------------------------
    float2 phaseOffset = float2(phase * 2.0, 0); 
    float lightDist = length(localUV - phaseOffset); 
    float smoothVal = max(0.001, phaseSmooth);
    float maskRadius = max(0.01, phaseMaskSize);
    float lightMask = 1.0 - smoothstep(maskRadius - smoothVal, maskRadius, lightDist); // 1 = Lit, 0 = Shadow

    // INVERT PHASE (New Feature)
    if (invertPhase > 0.5)
    {
        lightMask = 1.0 - lightMask;
    }

    // ----------------------------
    // 3. OUTER GLOW (Atmosphere)
    // Simple colored circle behind the planet
    // ----------------------------
    float3 totalGlow = float3(0,0,0);
    float totalGlowAlpha = 0.0;
    
    // Manual Glow Position Offset
    // We slide the glow center along the X axis of the local space (which is aligned with Light Dir)
    float2 glowOffset1 = float2(atmosphereGlowPosition, 0);
    float2 glowUV1 = localUV - glowOffset1;
    float glowDist1 = length(glowUV1);

    // Outer Glow 1 (Atmosphere)
    if (atmosphereEnabled > 0.5)
    {
        // Radius extends beyond 0.5
        float R = 0.5 + atmosphereSize; 
        float blur = max(0.001, atmosphereBlur);
        
        // Base Circle (The Glow itself)
        float glow1 = 1.0 - smoothstep(R - blur, R, glowDist1);
        
        totalGlow += atmosphereColor.rgb * glow1 * atmosphereOpacity * fadeVal;
        totalGlowAlpha = max(totalGlowAlpha, glow1 * atmosphereOpacity * fadeVal);
    }

    // Outer Glow 2 (Distant Light)
    if (atmosphere2Enabled > 0.5)
    {
        float R = 0.5 + atmosphere2Size;
        float blur = max(0.001, atmosphere2Blur);
        
        // Offset Logic
        float2 glowOffset2 = float2(atmosphere2GlowPosition, 0);
        float2 glowUV2 = localUV - glowOffset2;
        float glowDist2 = length(glowUV2);
        
        float glow2 = 1.0 - smoothstep(R - blur, R, glowDist2);
        
        totalGlow += atmosphere2Color.rgb * glow2 * atmosphere2Opacity * fadeVal; // Additive blend
        totalGlowAlpha = max(totalGlowAlpha, glow2 * atmosphere2Opacity * fadeVal);
    }

    // ----------------------------
    // 4. PLANET TEXTURE & SHADING
    // ----------------------------
    // Occlusion Logic:
    float effectiveMask = lerp(1.0, lightMask, darkSideOcclusion);
    
    // Combine with disc (base shape) and fadeVal
    occlusion = disc * fadeVal * effectiveMask;

    float4 texColor = SAMPLE_TEXTURE2D(tex, smp, texUV);
    float bodyAlpha = texColor.a * disc * lightMask * fadeVal;
    float3 bodyColor = tint.rgb * texColor.rgb;
    
    // ----------------------------
    // 5. INNER GLOW (SPHERICAL FRESNEL)
    // ----------------------------
    if (glowEnabled > 0.5)
    {
        // Calculate Sphere Normal Z (NdotV)
        // Normalized distance from center (0 to 1 at edge of disc)
        float r = saturate(dist * 2.0); 
        float nDotV = sqrt(1.0 - r * r); // Standard sphere equation: z = sqrt(1 - x^2 - y^2)
        
        // Fresnel calculation
        float fresnel = pow(1.0 - nDotV, glowPower);
        
        // Apply Intensity
        fresnel *= glowIntensity;
        
        // Mask by disc (contain to planet) and shadow (don't glow on dark side)
        float shadowFactor = lightMask; // Matches planet phase
        
        float3 innerGlow = glowColor.rgb * fresnel * disc * fadeVal * shadowFactor;
        
        bodyColor += innerGlow;
        bodyAlpha = max(bodyAlpha, length(innerGlow)); // Ensure glow contributes to alpha for blending
    }
    
    // ----------------------------
    // 6. COMPOSITE
    // ----------------------------
    // Planet body is drawn ON TOP of the glow.
    // The glow is drawn behind the planet (where disc is 0).
    
    // Glow is blocked by the LIT part of the planet.
    // The dark side is transparent, so glow can bleed through (unless occluded manually).
    float glowBlocker = disc * lightMask;
    
    float3 finalColor = bodyColor * bodyAlpha + totalGlow * (1.0 - glowBlocker);
    
    // Final Alpha
    float finalAlpha = max(bodyAlpha, totalGlowAlpha * (1.0 - glowBlocker));
    
    return float4(finalColor, finalAlpha);
}

// Main Function
float4 CalculateCelestial(float3 viewDir, out float totalOcclusion)
{
    float4 accumulator = float4(0,0,0,0);
    totalOcclusion = 0.0;

    // 1. Main Sun / Moon
    #if defined(_CELESTIAL_ON)
        float3 lightDir = _MainLightPosition.xyz;
        float occ0;
        float4 c0 = RenderCelestialBody(viewDir, lightDir, _Size, _Rotation, _RotateWithCamera, _Phase, _InvertPhase, _PhaseSize, _PhaseSmoothness, _DarkSideOcclusion, _Tint, _GlowColor, _GlowPower, _GlowIntensity, _Fade, _Texture, sampler_Texture, _Tiling, _CelestialEdgeSoftness, _GlowEnabled, _AtmosphereEnabled, _AtmosphereColor, _AtmosphereSize, _AtmosphereBlur, _AtmosphereGlowPosition, _AtmosphereOpacity, _Atmosphere2Enabled, _Atmosphere2Color, _Atmosphere2Size, _Atmosphere2Blur, _Atmosphere2GlowPosition, _Atmosphere2Opacity, occ0);
        accumulator = c0 + accumulator * (1.0 - c0.a);
        totalOcclusion = max(totalOcclusion, occ0);
    #endif

    // 2. Celestial 1
    #if defined(_CELESTIAL1_ON)
        float3 dir1 = DirectionFromAzimuthAltitude(_C1_Azimuth, _C1_Altitude);
        float occ1;
        float4 c1 = RenderCelestialBody(viewDir, dir1, _C1_Size, _C1_Rotation, _C1_RotateWithCamera, _C1_Phase, _C1_InvertPhase, _C1_PhaseSize, _C1_PhaseSmoothness, _C1_DarkSideOcclusion, _C1_Tint, _C1_GlowColor, _C1_GlowPower, _C1_GlowIntensity, _C1_Fade, _C1_Texture, sampler_C1_Texture, _C1_Tiling, _C1_EdgeSoftness, _C1_GlowEnabled, _C1_AtmosphereEnabled, _C1_AtmosphereColor, _C1_AtmosphereSize, _C1_AtmosphereBlur, _C1_AtmosphereGlowPosition, _C1_AtmosphereOpacity, _C1_Atmosphere2Enabled, _C1_Atmosphere2Color, _C1_Atmosphere2Size, _C1_Atmosphere2Blur, _C1_Atmosphere2GlowPosition, _C1_Atmosphere2Opacity, occ1);
        accumulator.rgb = c1.rgb + accumulator.rgb * (1.0 - c1.a);
        accumulator.a = max(accumulator.a, c1.a); 
        totalOcclusion = max(totalOcclusion, occ1);
    #endif

    // 3. Celestial 2
    #if defined(_CELESTIAL2_ON)
        float3 dir2 = DirectionFromAzimuthAltitude(_C2_Azimuth, _C2_Altitude);
        float occ2;
        float4 c2 = RenderCelestialBody(viewDir, dir2, _C2_Size, _C2_Rotation, _C2_RotateWithCamera, _C2_Phase, _C2_InvertPhase, _C2_PhaseSize, _C2_PhaseSmoothness, _C2_DarkSideOcclusion, _C2_Tint, _C2_GlowColor, _C2_GlowPower, _C2_GlowIntensity, _C2_Fade, _C2_Texture, sampler_C2_Texture, _C2_Tiling, _C2_EdgeSoftness, _C2_GlowEnabled, _C2_AtmosphereEnabled, _C2_AtmosphereColor, _C2_AtmosphereSize, _C2_AtmosphereBlur, _C2_AtmosphereGlowPosition, _C2_AtmosphereOpacity, _C2_Atmosphere2Enabled, _C2_Atmosphere2Color, _C2_Atmosphere2Size, _C2_Atmosphere2Blur, _C2_Atmosphere2GlowPosition, _C2_Atmosphere2Opacity, occ2);
        
        accumulator.rgb = c2.rgb + accumulator.rgb * (1.0 - c2.a);
        accumulator.a = max(accumulator.a, c2.a);
        totalOcclusion = max(totalOcclusion, occ2);
    #endif

    return accumulator;
}

// Overload for backward compatibility / simplicity
float4 CalculateCelestial(float3 viewDir)
{
    float dummyOcc;
    return CalculateCelestial(viewDir, dummyOcc);
}

#endif // KROMA_CELESTIAL_INCLUDED
