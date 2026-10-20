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

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

CBUFFER_START(UnityPerMaterial)
    TEXTURE2D(_BaseMap);
    float4 _BaseMap_ST;
    half _Cutoff;
CBUFFER_END

#pragma shader_feature_local_fragment _ALPHATEST_ON

Varyings Vert(Attributes input)
{
    Varyings output = (Varyings)0;
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

    return output;
}

#if UNITY_VERSION >= 60010000
#define RENDERING_LAYERS_DATA_TYPE uint
#else
#define RENDERING_LAYERS_DATA_TYPE float4
#endif

#if defined(SHADERPASS_NORMALS) || defined(SHADERPASS_FORWARD)
#define RenderTarget SV_Target1
#else
//Deferred rendering
#define RenderTarget SV_Target5
#endif

void Frag(Varyings input, out RENDERING_LAYERS_DATA_TYPE outRenderingLayers : RenderTarget)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #ifdef _WRITE_RENDERING_LAYERS
    #if _ALPHATEST_ON
    half baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, input.uv).r;
    clip(baseMap - _Cutoff);
    #endif
    
    #if UNITY_VERSION >= 60010000
    outRenderingLayers = EncodeMeshRenderingLayer();
    #else
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
    #endif
    #else
    outRenderingLayers = 0;
    #endif
}