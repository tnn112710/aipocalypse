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

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#define SAMPLE_NORMALS(uv) SampleNormals(uv)
#define SAMPLE_NORMALS_LOD(uv) SampleNormalsLOD(uv)

half3 SampleNormals(float2 uv)
{
	half3 normals = SampleSceneNormals(uv).xyz;
	
	return normals;
}

#if UNITY_VERSION >= 600000
#define NORMALS_RESOLUTION _CameraNormalsTexture_TexelSize.zw
#else
#define NORMALS_RESOLUTION _ScreenParams.xy
#endif

float3 SampleNormalsLOD(float2 uv)
{
	//Scale UV to account also for render scale
	uint2 pixelCoords = uint2(uv * NORMALS_RESOLUTION);
	float3 normals = LoadSceneNormals(pixelCoords).xyz;
	
	//Note: does not take the "Accurate GBuffer normals" option into account!
	
	return normals;
}

float2 SampleViewSpaceNormals(float2 uv)
{
	float3 normalWS = SAMPLE_NORMALS_LOD(uv).rgb;
	float3 normalVS = (mul((real3x3)GetWorldToViewMatrix(), normalWS));
	return normalVS.xy;
}