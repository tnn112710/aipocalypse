// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

//Omit from SG previews because it internally uses the built-in RP shader API
//Which throws errors about everything SRP related.
#if !defined(SHADERGRAPH_PREVIEW)
TEXTURE2D(_FogColorGradient);
TEXTURE2D_X(_SkyboxTex);
TEXTURE3D(_FogNoiseTex3D);

int4 _SceneFogMode;
//X: Mode 
//Y: Radial distance
//Z: Color source
//W: -
float4 _FogHeightParams;
//X: Height
//Y: -
//Z: Height enabled
//W: Noise enabled
float4 _FogDistanceParams;
//X: Start distance
//Y: 
//Z: Distance fog enabled
//W: 
float4 _SceneFogParams;
//X: Density: exp
//Y: Density: exp2
//Z: Start
//W: End
float2 _FogDensityParams;
//Distance density
//Height density
float4 _FogHeightNoiseParams;
//X: Tiling
//Y: Horizontal offset
//Z: Vertical offset
//W: Strength
float4 _FogColor;
//RGBA
float4 _FogSkyboxParams;
//X: Influence
//Y: Horizon Min
//Z: Horizon Max
float4 _FogDirLightParams;
//XYZ: Direction
//W: Exponent
float4 _FogDirLightColor; //(a=free)
//RGB: Color
uniform half _FarClippingPlane;	//Used for gradient distance

//Controlled by render feature, allows other assets to also toggle the integration
bool _SCPEFogEnabled;

#define HEIGHT_ENABLED _FogHeightParams.z > 0.5
#define HEIGHT_NOISE_ENABLED _FogHeightParams.w > 0.5
#define HEIGHT_INVERTED _FogHeightParams.y > 0.5
#define DISTANCE_ENABLED _FogDistanceParams.z > 0.5
#define NOISE_OCTAVE_SCALE 0.85 // Secondary octave size for height noise
#define SMALL_VALUE 1.0e-5 // Prevent division by zero

#define FOG_START_DISTANCE _FogDistanceParams.x
#define CAMERA_FAR_CLIPPING_PLANE _ProjectionParams.z

//Input must be unnormalized value in standard units
half ComputeLinearDistance(float fogDistance)
{
    float linearDistance = 0;
    
    if (_SceneFogMode.x == 1) //Linear mode: simple distance-based fade
    {
        linearDistance = fogDistance * _SceneFogParams.z + _SceneFogParams.w;
    }
    if (_SceneFogMode.x == 2) //Exponential: exp(-density)
    {
        linearDistance = _SceneFogParams.y * fogDistance; 
        linearDistance = exp2(-linearDistance);
    }
    if (_SceneFogMode.x == 3) //Exponential-squared: exp(-(density^2))
    {
        linearDistance = _SceneFogParams.x * fogDistance; 
        linearDistance = exp2(-linearDistance * linearDistance);
    }
    
    return 1.0 - saturate(linearDistance);
}

//Use unique name, may clash with other fog assets
float ComputeHeightFogSCPE(float3 positionWS)
{
    const float fogHeight = _FogHeightParams.x;
    const float heightDirection = HEIGHT_INVERTED ? -1.0 : 1.0;
    const float cameraYDifference = (_WorldSpaceCameraPos.y - fogHeight) * heightDirection;

    //Determine if camera is above (k=0) or below (k=1) fog layer
    float cameraAboveFog = (cameraYDifference <= 0.0) ? 1.0 : 0.0;

    //View ray information
    float3 cameraToPixel = _WorldSpaceCameraPos.xyz - positionWS;
    float pixelYDifference = (positionWS.y - fogHeight) * heightDirection;
    float viewDirectionY = cameraToPixel.y * heightDirection;

    //Exponential fog integration coefficients
    float c1 = cameraAboveFog * (pixelYDifference + cameraYDifference);
    float c2 = (1.0 - 2.0 * cameraAboveFog) * pixelYDifference;
    float clampedC2 = min(c2, 0.0);

    //Integrated fog density along view ray
    float3 densityWeightedRay = _FogDensityParams.y * cameraToPixel;
    float heightFogDensity = -length(densityWeightedRay) * 
        (c1 - clampedC2 * clampedC2 / abs(viewDirectionY + SMALL_VALUE));

    return heightFogDensity;
}

float4 GetFogColor(float2 screenPos, float3 positionWS, float distanceFactor)
{
    float4 fogColor = _FogColor.rgba;
    
    //Gradient texture
#ifndef DISABLE_FOG_GRADIENT
    if (_SceneFogMode.z == 1) //Gradient
    {
        fogColor = SAMPLE_TEXTURE2D_LOD(_FogColorGradient, sampler_LinearClamp, float2(distanceFactor / _FarClippingPlane, 0.0), 0.0);
    }
#endif
    
    //Skybox color
#ifndef DISABLE_FOG_SKYBOXCOLOR
    if (_SceneFogMode.z == 2) //Skybox
    {
        fogColor.rgb = SAMPLE_TEXTURE2D_X_LOD(_SkyboxTex, sampler_LinearClamp, screenPos, 0).rgb; //Mip not used
        fogColor.a = 1.0;
    }
#endif

    //Add directional light faux-scattering
#if !defined(DISABLE_FOG_DIRECTIONAL_COLOR)
    //Encoded strength/intensity
    if(_FogDirLightColor.a > 0)
    {
        float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - positionWS);
        float NdotL = saturate(pow(saturate(dot(-viewDir, _FogDirLightParams.xyz)), _FogDirLightParams.a));
        
        //Fade out towards the horizon after 10 degree angle
        half sunAngle = saturate(dot(float3(0.0, 1.0, 0.0), _FogDirLightParams.xyz));
        half angleMask = saturate(sunAngle * 10.0); /* 1.0/0.10 = 10 */
        
        NdotL *= angleMask;
        
        fogColor.rgb = lerp(fogColor.rgb, _FogDirLightColor.rgb * _FogDirLightColor.a, NdotL);
    }
#endif

    return fogColor;
}

float GetFogHeight(float3 positionWS, float skyboxMask)
{
    float heightFog = 0;
    
    if (HEIGHT_ENABLED) 
    {
        heightFog = ComputeHeightFogSCPE(positionWS);
        heightFog = 1-exp2(-heightFog);
        
#ifndef DISABLE_FOG_HEIGHTNOISE
        if (HEIGHT_NOISE_ENABLED)
        {
            float3 noiseCoords1 = (positionWS.xyz * _FogHeightNoiseParams.x);
            //noiseCoords1.xz += _WorldSpaceCameraPos.xz;
            
            noiseCoords1.xz += _FogHeightNoiseParams.y;
            noiseCoords1.y = positionWS.y * _FogHeightNoiseParams.x;
            noiseCoords1.y -= _FogHeightNoiseParams.z;
            
            half noise13D = SAMPLE_TEXTURE3D_LOD(_FogNoiseTex3D, sampler_LinearRepeat, noiseCoords1, 0).r;
            
            float3 noiseCoords2 = noiseCoords1 * 0.85;
            half noise23D = SAMPLE_TEXTURE3D_LOD(_FogNoiseTex3D, sampler_LinearRepeat, noiseCoords2, 0).r;
            
            half noise = lerp(1, min(noise13D, noise23D), _FogHeightNoiseParams.w * 2);
            noise = lerp(noise, 1, skyboxMask);
            
            heightFog = saturate(mul(noise, heightFog) + heightFog);
            //heightFog *= noise;
        }
#endif
        
    }

    return heightFog;
}

float ComputeFogDensity(float3 positionWS, float linearDepth, out float distanceFog)
{
    float skyboxMask = linearDepth >= 0.999 ? 1 : 0;
    float3 positionDelta = positionWS - _WorldSpaceCameraPos;
    float cameraDistance = length(positionDelta);
    float3 viewDir = normalize(positionDelta);
    
    float fog = 0;
    
    distanceFog = 0;
    if (DISTANCE_ENABLED)
    {
        //Radial distance in meters
        if (_SceneFogMode.y == 1)
        {
            distanceFog = cameraDistance;
        }
        //Perpendicular to camera plane
        else
        {
            distanceFog = linearDepth * CAMERA_FAR_CLIPPING_PLANE;
        }
        
        //Subtract start distance
        float startDistance = (_ProjectionParams.y + FOG_START_DISTANCE);
        distanceFog = max(0.0, distanceFog - startDistance);
    
        //Multiply by density
        distanceFog *= _FogDensityParams.x;

        //Normalized
        float linearDistanceFog = ComputeLinearDistance(distanceFog);
        
        //int step = 4;
        //linearDistanceFog = floor(linearDistanceFog * step) / (step -1.0);
        
        fog += linearDistanceFog;
    }
    
    float heightFog = GetFogHeight(positionWS, skyboxMask);
    //return heightFog;
    fog += heightFog;

    //Skybox influence
    fog = lerp(fog, 0, (1-_FogSkyboxParams.x) * skyboxMask);

    half horizon = saturate(viewDir.y);

    #define HORIZON_MIN _FogSkyboxParams.y
    #define HORIZON_MAX _FogSkyboxParams.z

    horizon = smoothstep(HORIZON_MIN, HORIZON_MAX, horizon);
    fog *= 1-horizon;
    
    return fog;
}

//Main function!
float4 ComputeFog(float3 positionWS, float2 screenPos, float linearDepth = 1.0)
{
    float distanceFog = 0;
    float fog = ComputeFogDensity(positionWS, linearDepth, distanceFog);

    //Color
    float4 fogColor = GetFogColor(screenPos, positionWS, distanceFog);
    fogColor.a *= saturate(fog);

    return fogColor;
}

//Override without the depth input. This is used in transparent shaders
float4 ComputeTransparentFog(float3 positionWS, float2 screenPos)
{
    return ComputeFog(positionWS, screenPos, 0.0);
}

//All in one function, for all shaders
void ApplyFog(float3 positionWS, float2 screenPos, float linearDepth, inout float3 color)
{
    float4 fogColor = ComputeFog(positionWS, screenPos, linearDepth);

    color = lerp(color.rgb, fogColor.rgb, fogColor.a);
}

//All in one function, for transparent shaders
void ApplyTransparencyFog(float3 positionWS, float2 screenPos, inout float3 color)
{
    if (_SCPEFogEnabled)
    {
        ApplyFog(positionWS, screenPos, 0.0, color);
    }
}
#endif

//Shader Graph subgraph function
void ApplyFog_float(in float3 positionWS, in float2 screenPos, in float3 emission, out float3 outEmission, out float fogDensity)
{
    #if !defined(SHADERGRAPH_PREVIEW)
    if (_SCPEFogEnabled)
    {
        //half linearDepth = Linear01Depth(SampleSceneDepth(screenPos), _ZBufferParams);
        const float4 fogColor = ComputeFog(positionWS, screenPos);

        fogDensity = fogColor.a;
    
        outEmission = lerp(emission, fogColor.rgb, fogDensity);
    }
    else
    {
        outEmission = emission;
        fogDensity = 0.0;
    }
    #else
    outEmission = emission;
    fogDensity = 1.0;
    #endif
}

void GetFogDensity_float(in float3 positionWS, in float linearDepth, out float fogDensity)
{
    fogDensity = 1.0;
    
    #if !defined(SHADERGRAPH_PREVIEW)
    if (_SCPEFogEnabled)
    {
        half distanceDensity = 0;
        fogDensity = ComputeFogDensity(positionWS, linearDepth, distanceDensity);
    }
    #endif
}
