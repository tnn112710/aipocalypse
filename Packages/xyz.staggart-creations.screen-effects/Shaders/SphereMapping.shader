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

Shader "Hidden/SC Screen Effects/SphereMapper"
{
    HLSLINCLUDE
    #include_with_pragmas "Libraries/PostProcessing.hlsl"
    
    TEXTURE2D(_Texture);
    SAMPLER(sampler_Texture);
    float _Offset;
    float _Tiling;
    bool _WorldSpace;
    
    struct SphereAttributes
    {
        float4 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct SphereVaryings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 positionOS : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };
    
    SphereVaryings SphereVert(SphereAttributes input)
    {
        SphereVaryings output = (SphereVaryings)0;

        UNITY_SETUP_INSTANCE_ID(input);
	    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        //Mesh is rendered at Matrix4x4.identity, hence vertices are transformed manually

        //Scale to the projection's field of view
        //Additional scale increases noise frequency, but also offers more variety
        output.positionOS = input.positionOS.xyz;
        
        //Position to camera origin
        output.positionWS.xyz = _WorldSpaceCameraPos.xyz + output.positionOS;
        //if (_WorldSpace == false) output.positionWS = TransformObjectToWorld(output.positionOS);

        output.positionCS = TransformWorldToHClip(output.positionWS);

        return output;
    }
    
    half SampleTexture(float2 uv, half blendWeight)
    {
        //_Tiling *= 0.1;
        float2 uv1 = (uv * _Tiling) + _Offset;
        float2 uv2 = (uv * _Tiling * 0.85) - _Offset;
        
        half tex1 = SAMPLE_TEXTURE2D_LOD(_Texture, sampler_Texture, uv1, 0).r;
        half tex2 = SAMPLE_TEXTURE2D_LOD(_Texture, sampler_Texture, uv2, 0).r;
        
        return (mul(tex1, tex2) * 4.0) * blendWeight;
    }
    
    half4 Frag(SphereVaryings input) : SV_Target
    {
	    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float3 coords = input.positionOS;
        //if (_WorldSpace == true) coords = -input.positionWS;
        
        float3 normal = normalize(input.positionOS); //Unit sphere
        
        float3 blendWeights = pow(abs(normal), 1.0);

        //Sample textures from three planes
        half texX = SampleTexture(coords.zy, blendWeights.x);
        half texY = SampleTexture(coords.xz, blendWeights.y);
        half texZ = SampleTexture(coords.xy, blendWeights.z);
        
        //return float4(texX.xxx, 1);
        
        //Normalize blend weights so they sum to 1
        blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);
        
        //Blend textures together
        half3 finalTexture = texX * blendWeights.x + texY * blendWeights.y + texZ * blendWeights.z;
        
        return float4(finalTexture.xxx, 1);
    }
    
    ENDHLSL

    SubShader
    {
        ZTest Always ZWrite Off Cull Off ZClip Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex SphereVert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}