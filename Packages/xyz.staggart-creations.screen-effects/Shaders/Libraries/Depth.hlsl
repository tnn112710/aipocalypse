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

//Depth sampling
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#define SAMPLE_DEPTH(uv) SampleSceneDepth(uv)
#define SAMPLE_LINEAR_DEPTH(uv) LINEAR_DEPTH(SampleSceneDepth(uv))
#define SAMPLE_DEPTH_LOD(uv) SampleDepthLOD(uv)
#define LINEAR_DEPTH(depth) RawToLinearDepth(depth)
#define LINEAR_EYE_DEPTH(depth) LinearEyeDepth(depth, _ZBufferParams)
#define SAMPLE_EYE_DEPTH(uv) LINEAR_EYE_DEPTH(SAMPLE_DEPTH(uv))

#define SKYBOX_LINEAR_DEPTH 0.999

//Scale linear values to the clipping planes for orthographic projection (unity_OrthoParams.w = 1 = orthographic)
#define DEPTH_SCALAR lerp(1.0, FAR_CLIP - NEAR_CLIP, unity_OrthoParams.w)

float RawToLinearDepth(float rawDepth)
{
    if(unity_OrthoParams.w == 0) //Perspective
    {
        return Linear01Depth(rawDepth, _ZBufferParams);
    }
	
    //Orthographic
    #if UNITY_REVERSED_Z
    rawDepth = 1.0 - rawDepth;
    #endif
	
    float linearDepth = ((_ProjectionParams.z - _ProjectionParams.y) * (rawDepth) + _ProjectionParams.y);

    return linearDepth;
}


float SampleDepthLOD(float2 uv)
{
    return LoadSceneDepth(uint2(uv * _ScreenParams.xy));
}