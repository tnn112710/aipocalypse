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

#ifndef POST_PROCESSING_INCLUDED
#define POST_PROCESSING_INCLUDED

#pragma multi_compile_instancing

#ifdef UNITY_GRAPHFUNCTIONS_LW_INCLUDED //Shader Graph
#define SHADER_GRAPH
#endif

//Will get stripped out in release builds
#pragma multi_compile _ DEBUG_DISPLAY

//Expirement, will now throw the "undeclared identifier '_UnityFBInput0'" error
//#pragma multi_compile_local_fragment _ _ON_TILE

uniform bool _DeferredRendering;

#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
#if UNITY_VERSION >= 600000
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

//vertex shader
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

#include "Blending.hlsl"
#include "Utilities.hlsl"
#include_with_pragmas "RenderingLayerMask.hlsl"
#include "Common.hlsl"
#include "Depth.hlsl"
#include "Normals.hlsl"
#include "ScreenColor.hlsl"

#define FAR_CLIP _ProjectionParams.z
#define NEAR_CLIP _ProjectionParams.y
#define SKYBOX_LINEAR_DEPTH 0.99
#define WorldViewDirection -GetWorldToViewMatrix()[2].xyz
#define Resolution _ScreenParams.xy

#if DEBUG_DISPLAY
uniform half _PostProcessingDebugSlider;
#define OUTPUT(finalColor) return float4(lerp(screenColor.rgb, finalColor.rgb, CreateDebugSplit(SCREEN_COORDS, _PostProcessingDebugSlider)), screenColor.a);
#else
//Note: Always maintain input's alpha channel for FXAA support
#define OUTPUT(finalColor) return float4(finalColor.rgb, screenColor.a);
#endif
#endif