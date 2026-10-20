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

#ifndef SHADER_GRAPH //Shader Graph
//Vertex shader
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#define SCREEN_COORDS input.texcoord.xy
#define SCREEN_SPAN_COORDS input.texcoord.xy

#if defined(UNITY_STEREO_INSTANCING_ENABLED)
#undef SCREEN_SPAN_COORDS
#define SCREEN_SPAN_COORDS half2(input.texcoord.x * 0.5 + (unity_StereoEyeIndex * 0.5), input.texcoord.y)
#endif
#endif

#if ON_TILE
FRAMEBUFFER_INPUT_HALF(0);
#endif

#if UNITY_VERSION >= 600000
#define INPUT_RESOLUTION _BlitTexture_TexelSize.zw
#else
#define INPUT_RESOLUTION _ScreenParams.xy
#endif

#if UNITY_VERSION >= 600000
#define INPUT_TEXELSIZE _BlitTexture_TexelSize
#else
#define INPUT_TEXELSIZE _ScreenParams
#endif

//Main function
float4 ScreenColor(float2 uv, SamplerState textureSampler)
{
    #if !_ON_TILE
    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, textureSampler, uv, _BlitMipLevel);
    #else
    return LOAD_FRAMEBUFFER_INPUT(0, uv);
    #endif
}

float4 ScreenColorClamped(float2 uv) 
{
    return ScreenColor(uv, sampler_LinearClamp);
}

float4 ScreenColorTiled(float2 uv) 
{
    return ScreenColor(uv, sampler_LinearRepeat);
}

#define SCREEN_COLOR(uv) ScreenColorClamped(uv);
#define SCREEN_COLOR_RGB(uv) ScreenColorClamped(uv).rgb

float4 ScreenColor(float2 uv) 
{
    return SCREEN_COLOR(uv);
}
#define SAMPLE_SCREEN_COLOR float4 screenColor = SCREEN_COLOR(SCREEN_COORDS)