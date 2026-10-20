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

Shader "Hidden/SC Screen Effects/Posterize"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float _Depth;
	float4 _Params;
	half _Intensity;

	float PosterizeChannel(float val, float amount)
	{
		return floor(val * amount) / (amount -1.0);
	}

	float3 PosterizeRGB(float3 val, float amount)
	{
		return floor(val.rgb * amount) / (amount -1.0);
	}

	float4 FragRGB(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		#ifndef UNITY_COLORSPACE_GAMMA
		//screenColor.rgb = LinearToSRGB(screenColor.rgb);
		#endif

		float3 rgb = PosterizeRGB(screenColor.rgb, _Params.w);

		#ifndef UNITY_COLORSPACE_GAMMA
		//rgb = SRGBToLinear(rgb);
		#endif
		
		rgb = lerp(screenColor.rgb, rgb.rgb, _Intensity);
		return float4(rgb, screenColor.a);
	}

	float4 FragHSV(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		//Required
		screenColor.rgb = saturate(screenColor.rgb);
		
		#ifndef UNITY_COLORSPACE_GAMMA
		//screenColor.rgb = LinearToSRGB(screenColor.rgb);
		#endif
		
		float3 hsv = RgbToHsv(screenColor.rgb);
		hsv.x = PosterizeChannel(hsv.x, _Params.x);
		hsv.y = PosterizeChannel(hsv.y, _Params.y);
		hsv.z = PosterizeChannel(hsv.z, _Params.z);

		float3 rgb = HsvToRgb(hsv);

		#ifndef UNITY_COLORSPACE_GAMMA
		//rgb = SRGBToLinear(rgb);
		#endif
		
		rgb = lerp(screenColor.rgb, rgb.rgb, _Intensity);
		
		OUTPUT(rgb)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Posterize RGB"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragRGB

			ENDHLSL
		}
		Pass
		{
			Name "Posterize HSV"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragHSV

			ENDHLSL
		}
	}
}