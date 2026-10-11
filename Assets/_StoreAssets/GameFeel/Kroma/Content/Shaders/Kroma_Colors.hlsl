#ifndef KROMA_COLORS_INCLUDED
#define KROMA_COLORS_INCLUDED

#include "Kroma_Shared.hlsl"

// Helper for vibrant color blending (pseudo-linear / gamma corrected interpolation)
// This prevents muddy, desaturated grayish bands when mixing distinctly different colors.
inline float3 LerpColorBlend(float3 c1, float3 c2, float t)
{
    // Square the colors to move them into a more linear-feeling space for mixing, lerp, then square root back
    return sqrt(lerp(c1 * c1, c2 * c2, t));
}

// Hyperbolic rounding to prevent sharp V-shape center lines.
// Replaces standard `abs(x)` with `sqrt(x^2 + epsilon)` to create a tiny smooth parabola exactly at `x=0`.
inline float SmoothAbs(float x, float smoothing = 0.05)
{
    return sqrt(x * x + smoothing * smoothing);
}

float4 CalculateSkyGradient(float3 viewDir)
{
    // Ground Simulation Logic
    // If _SIMULATE_GROUND is ON: Normal Sky + Ground
    // If _SIMULATE_GROUND is OFF: Mirrored Sky (No Ground)
    
    #if defined(_SIMULATE_GROUND)
        float y = viewDir.y;
    #else
        float y = abs(viewDir.y);
    #endif

    // 1. Calculate Base Gradient (Horizon -> Mid -> Top)
    float3 skyColor = _TopColor.rgb;
    
    #if defined(_TWO_COLOR_MODE)
        // 2-Color Mode: Exponential blend from Horizon to Top
        float h = y - _Horizon_Offset; 
        
        // Classic exponential falloff. If h is negative (below horizon), exp(positive) > 1, so saturate cleanly caps it at 1.0
        float density = saturate(exp(-(h * 5.0) / max(0.001, _Horizon_Softness)));
        
        skyColor = LerpColorBlend(_TopColor.rgb, _HorizonColor.rgb, density);
    #else
        // 3-Color Mode: Horizon -> Mid -> Top
        // 1. Calculate Base Sky (Top + Mid)
        // Ensure distance is always subtly smoothed at the peak to prevent sharp center lines
        float midDistance = SmoothAbs(y - _Mid_Sky_Height, 0.05);
        float midDensity = saturate(exp(-(midDistance * 5.0) / max(0.001, _Mid_Sky_Softness)));

        // Base is TopColor. MidColor layers over it as a soft band.
        skyColor = LerpColorBlend(_TopColor.rgb, _MidColor.rgb, midDensity); 
        
        // 2. Horizon ALWAYS wins at the bottom
        // Keep the horizon soft by treating the height above it like an absolute distance that's slightly smoothed, 
        // to prevent the exponential peak from cutting a sharp line in the sky. If y is below the offset (h1 is negative),
        // we lock it to exactly 0 so the density guarantees to saturate at 1.0 (pure Horizon color).
        float rawH1 = max(0.0, y - _Horizon_Offset);
        float h1 = SmoothAbs(rawH1, 0.05);
        
        // Classic exponential falloff. Saturate handles the below-horizon values perfectly.
        float horizonDensity = saturate(exp(-(h1 * 5.0) / max(0.001, _Horizon_Softness)));
        
        // Layer Horizon Color OVER the Top/Mid blend
        // Note: The pseudo-linear LerpColorBlend makes transitions smooth
        skyColor = LerpColorBlend(skyColor, _HorizonColor.rgb, horizonDensity);
    #endif

    float3 finalColor = skyColor;
    float skyVisibility = 1.0;

    #if defined(_SIMULATE_GROUND)
        // 2. Ground Handling (Only if Ground is Enabled)
        // If y < 0, blend towards ground color
        // Use _Ground_Blend to control base width.
        // Use _Ground_Horizon_Smoothness to extend the blend further DOWNWARDS.
        // The blend stops exactly at 0.0 (Horizon), preventing sky bleed.
        float groundBlend = max(0.001, _Ground_Blend);
        // Multiplier boosted by 10x to allow significant softening even with small base blend.
        float totalBlendWidth = groundBlend * (1.0 + _Ground_Horizon_Smoothness * 10.0);
        
        float groundFactor = smoothstep(-totalBlendWidth, 0.0, y);
        finalColor = lerp(_Ground_Color.rgb, skyColor, groundFactor);
        
        // Sky Visibility: 1.0 = Sky, 0.0 = Ground
        // Since groundFactor goes 0->1 as we go UP from ground to sky, it works as visibility too.
        skyVisibility = groundFactor;
    #endif

    return float4(finalColor, skyVisibility);
}

#endif // KROMA_COLORS_INCLUDED
