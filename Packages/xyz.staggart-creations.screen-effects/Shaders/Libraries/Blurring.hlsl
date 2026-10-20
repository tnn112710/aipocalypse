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

#include "PostProcessing.hlsl"

//Value is set up through script and represents texel radius. Once horizontally, once vertically, per blur pass
float4 _BlurOffsets;

#define BOX_RCP 0.25

//Standard box filtering
half4 UpsampleBox(TEXTURE2D_X_PARAM(tex, samplerTex), float2 uv, float2 amount)
{
	float4 d = float4(-1.0, -1.0, 1.0, 1.0) * amount.xyxy;

	half4 s = 0;
	s += (SAMPLE_TEXTURE2D_X(tex, samplerTex, (uv + d.xy)));
	s += (SAMPLE_TEXTURE2D_X(tex, samplerTex, (uv + d.zy)));
	s += (SAMPLE_TEXTURE2D_X(tex, samplerTex, (uv + d.xw)));
	s += (SAMPLE_TEXTURE2D_X(tex, samplerTex, (uv + d.zw)));

	return s * BOX_RCP;
}

float4 FragBlurBox(Varyings input) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	half2 uv = SCREEN_COORDS;
	return UpsampleBox(TEXTURE2D_X_ARGS(_BlitTexture, sampler_LinearClamp), uv, _BlurOffsets.xy * 2.0).rgba;
}

static const uint GaussianKernelSize = 7;
static const float GaussianWeights[GaussianKernelSize] = { 0.40, 0.15, 0.15, 0.10, 0.10, 0.05, 0.05 };

static float2 GaussianOffsetKernel[GaussianKernelSize] =
{
	float2(0,0),
	+ _BlurOffsets.xy,
	- _BlurOffsets.xy,
	+ _BlurOffsets.xy * 2.0,
	- _BlurOffsets.xy * 2.0,
	+ _BlurOffsets.xy * 6.0,
	- _BlurOffsets.xy * 6.0,
};

float4 FragBlurGaussian(Varyings input) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float4 color = 0;
	
	half2 uv = SCREEN_COORDS;
	UNITY_UNROLL
 	for(uint k = 0; k < GaussianKernelSize; k++)
	{
		color += GaussianWeights[k] * SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + GaussianOffsetKernel[k]);
	}

	return color;
}