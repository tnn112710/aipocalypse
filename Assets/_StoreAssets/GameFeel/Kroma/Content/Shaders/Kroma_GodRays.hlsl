#ifndef KROMA_GODRAYS_INCLUDED
#define KROMA_GODRAYS_INCLUDED

// Properties
float4 _GodRaysColor;
float _GodRaysDensity;
float _GodRaysContrast;
float _GodRaysSpeed;
float _GodRaysPackCount;
float _GodRaysMinPackWidth;
float _GodRaysMaxPackWidth;
float _GodRaysHoleSize;
float _GodRaysHoleSoftness;
float _GodRaysHoleNoise;
float _GodRaysLength;
float _GodRaysFalloff;
float _GodRaysBeamBlur;
float _GodRaysBeamSweep;
float _GodRaysRotateWithCamera;

// New Properties
float _GodRaysHorizonFade;
float _GodRaysHorizonMaskPower;
float _GodRaysHorizonCruft;
// 2D Random Hash
float2 hash22(float2 p) 
{
    float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// 2D Value Noise
float noise(float2 p) 
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(hash22(i).x, hash22(i + float2(1.0, 0.0)).x, f.x),
        lerp(hash22(i + float2(0.0, 1.0)).x, hash22(i + float2(1.0, 1.0)).x, f.x),
        f.y
    );
}

// FBM
float fbm(float2 p) 
{
    float v = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 3; i++) {
        v += amp * noise(p);
        p *= 2.0;
        amp *= 0.5;
    }
    return v;
}

float3 CalculateGodRays(float3 viewDir, float3 lightDir, float3 timeParams)
{
    // Direction Check
    if (length(lightDir) < 0.1) lightDir = float3(0, 1, 0);
    else lightDir = normalize(lightDir);

    // --- Polar Coordinates ---
    float3 right, up;
    
    if (_GodRaysRotateWithCamera > 0.5)
    {
        // "Soft" Rotation: Blend world-up and camera-up so it feels like a subtle shift, not strapped to screen space
        float3 camUp = normalize(float3(unity_CameraToWorld._m01, unity_CameraToWorld._m11, unity_CameraToWorld._m21));
        up = normalize(lerp(float3(0, 1, 0), camUp, 0.125)); // 12.5% influence
        
        // Edge case fallback
        if (abs(dot(up, lightDir)) > 0.99) 
        {
             up = normalize(float3(unity_CameraToWorld._m00, unity_CameraToWorld._m10, unity_CameraToWorld._m20)); 
        }
    }
    else
    {
        up = float3(0, 1, 0);
    }

    right = normalize(cross(lightDir, up));
    up = cross(right, lightDir);
    
    float x = dot(viewDir, right);
    float y = dot(viewDir, up);
    
    float angle = atan2(y, x); 
    float distFromSun = 1.0 - saturate(dot(viewDir, lightDir));

    // --- 1. RAY TEXTURE (PRIMARY) ---
    float time = timeParams.x * _GodRaysSpeed;
    
    // To prevent the sharp visual seam caused by atan2 wrapping from PI to -PI, we map the angle into a continuous 2D circle for noise sampling
    float2 polarUV = float2(cos(angle), sin(angle));
    
    // Primary Rays
    float2 noiseUV1 = polarUV * _GodRaysDensity + float2(time, 0.0);
    float n1 = fbm(noiseUV1);
    float2 noiseUV2 = polarUV * (_GodRaysDensity * 0.9) - float2(time * 0.7, -10.5);
    float n2 = fbm(noiseUV2);
    
    // Standard input noise
    float rayNoise = n1 * n2;
    float rays = pow(saturate(rayNoise * 2.0), _GodRaysContrast); 

    // Primary Packing (This dictates the hard edges of the beams)
    float sweep = timeParams.x * _GodRaysBeamSweep;
    float packAngle = angle + sweep;
    float2 packPolar = float2(cos(packAngle), sin(packAngle));
    
    float packBase = noise(packPolar * _GodRaysPackCount + float2(timeParams.x * 0.02, 0.0));
    float sizeRandom = noise(packPolar * 2.0 + float2(50.0, 50.0)); 
    float targetSize = lerp(_GodRaysMinPackWidth, _GodRaysMaxPackWidth, sizeRandom);
    
    // Use Beam Blur to soften the edges of the pack mask
    float beamSoftness = 0.2 + (_GodRaysBeamBlur * 0.4); 
    
    float threshold = 1.0 - targetSize;
    float packMask = smoothstep(threshold, threshold + beamSoftness, packBase);
    rays *= packMask;
    
    // Combine lengths
    float primLengthMask = saturate(1.0 - (distFromSun / _GodRaysLength));
    primLengthMask = pow(primLengthMask, _GodRaysFalloff);
    rays *= primLengthMask;

    // --- HOLE & FALLOFF (Shared Hole) ---
    float holeDistortion = (noise(polarUV * 5.0) - 0.5) * _GodRaysHoleNoise;
    float effectiveDist = distFromSun + holeDistortion;
    float holeMask = smoothstep(_GodRaysHoleSize, _GodRaysHoleSize + _GodRaysHoleSoftness, effectiveDist);

    // --- HORIZON FADE ---
    float horizonNoise = noise(polarUV * 5.0 + float2(time * 0.1, 0.0)) * _GodRaysHorizonCruft;
    float horizonLevel = _GodRaysHorizonFade + horizonNoise;
    float horizonMask = smoothstep(horizonLevel, horizonLevel + (1.0/_GodRaysHorizonMaskPower), viewDir.y);
    
    return rays * holeMask * horizonMask * _GodRaysColor.rgb;
}

#endif // KROMA_GODRAYS_INCLUDED
