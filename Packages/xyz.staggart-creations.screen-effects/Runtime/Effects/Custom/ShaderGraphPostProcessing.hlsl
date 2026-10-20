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

#ifndef UNITY_GRAPHFUNCTIONS_LW_INCLUDED //Shader Graph
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#if defined(SHADERGRAPH_PREVIEW)
#define TEXTURE2D_X TEXTURE2D
#define SAMPLE_TEXTURE2D_X SAMPLE_TEXTURE2D
#endif

#include "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/Common.hlsl"
#include "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/RenderingLayerMask.hlsl"

TEXTURE2D_X(_BlitTexture);
#ifndef UNITY_CORE_SAMPLERS_INCLUDED
SAMPLER(sampler_LinearRepeat);
#endif

void GetSourceImage_float(float2 uv, out float4 color)
{
    color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearRepeat, uv);
}

void WorldSpacePosition_float(float4 uv, float rawDepth, out float3 positionWS)
{
    positionWS = GetWorldPosition(uv.xy, rawDepth);
}

void GetScreenLuminance_float(float4 color, out float luminance)
{
    luminance = Luminance(color.rgb);
}

void DistanceFactor_float(float3 positionWS, float start, float end, out float factor)
{
    float pixelDist = length(_WorldSpaceCameraPos.xyz - positionWS.xyz);

    //Distance based scalar
    factor = saturate((end - pixelDist ) / (end-start));
}

void LinearDepthFade_float(float linearDepth, float start, float end, out float fadeFactor)
{
    fadeFactor = LinearDepthFade(linearDepth, start, end, 0, 1);
}

void GetRenderingLayerMask_float(float2 uv, out half renderingLayerMask)
{
    renderingLayerMask = RenderingLayerMask(uv); 
}