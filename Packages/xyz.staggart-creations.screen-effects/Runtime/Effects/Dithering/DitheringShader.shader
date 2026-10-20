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

Shader "Hidden/SC Screen Effects/Dithering"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_LUT);
	SAMPLER(sampler_LUT);

	float4 _Params;
	//X: Intensity
	//Y: Contrast
	
	float4 _Dithering_Coords;
	//X: LUT size
	//Y: Tiling
	//Z: Luminance influence
	//W: Intensity
	uint _ProjectionMode;
	//0: Screen space
	//1: Spherical
	
	#define CONTRAST _Params.y
	#define TILING _Dithering_Coords.y

	// Screen-space 4x4 Bayer matrix, based on https://en.wikipedia.org/wiki/Ordered_dithering
	float BinaryDither4x4(float value, float2 sceneUVs) {
		float4x4 mtx = float4x4(
			float4(1, 9, 3, 11) / 17.0,
			float4(13, 5, 15, 7) / 17.0,
			float4(4, 12, 2, 10) / 17.0,
			float4(16, 8, 14, 6) / 17.0
			);
		float2 px = floor(sceneUVs);
		int xSmp = fmod(px.x, 4);
		int ySmp = fmod(px.y, 4);
		float4 xVec = 1 - saturate(abs(float4(0, 1, 2, 3) - xSmp));
		float4 yVec = 1 - saturate(abs(float4(0, 1, 2, 3) - ySmp));
		float4 pxMult = float4(dot(mtx[0], yVec), dot(mtx[1], yVec), dot(mtx[2], yVec), dot(mtx[3], yVec));
		return round(value + dot(pxMult, xVec));
	}
	
	half SampleDitheringLUT(float2 uv)
	{
		//Maintain square ratio
		if (_ProjectionMode == 0)
		{
			uv.x *= _ScreenParams.x / _ScreenParams.y;
		}
		
		//half2 textureSize = _Dithering_Coords.xx; //Eg: 128px
		float2 lutUV = uv * (TILING);
		
		return SAMPLE_TEXTURE2D_LOD(_LUT, sampler_LUT, lutUV, 0).r;
	}

	float SceneLuminance(float3 color)
	{
		float luminance = Luminance(LinearToSRGB(color.rgb));
		
		//Apply contrast
		float midpoint = (0.5);
		luminance = (luminance - midpoint) * CONTRAST + midpoint;
		
		return luminance;
	}
	
	float3 ApplyDithering(float3 color, half dithering)
	{
		float luminance = SceneLuminance(color.rgb);
		//return luminance;
		
		float dither = step(dithering, luminance / _Dithering_Coords.z);
		//dither = BinaryDither4x4(luminance * _Dithering_Coords.z, uv * 4);
		
		return lerp(color, color * saturate(dither), _Dithering_Coords.w);
	}

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float2 uv = SCREEN_COORDS.xy;

		half dithering = SampleDitheringLUT(uv);
		//return float4(dithering.xxx, 1);
		//dithering = BinaryDither4x4(luminance * _Dithering_Coords.z, uv * _Dithering_Coords.y);
		
		//return float4(dithering.xxx, 1);
		
		float3 ditheredColor = ApplyDithering(screenColor.rgb, dithering);
		
		OUTPUT(ditheredColor)
	}
	
	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Dithering"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}