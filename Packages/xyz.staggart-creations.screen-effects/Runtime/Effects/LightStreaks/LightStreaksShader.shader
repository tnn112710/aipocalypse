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

Shader "Hidden/SC Screen Effects/Light Streaks"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	#include "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/Blurring.hlsl"

	TEXTURE2D(_BloomTex);

	float4 _Params;
	//X: Luminance threshold
	//Y: Intensity
	//Z: Brightness
	//W: ...

	float3 FragLuminanceDiff(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float3 luminance = LuminanceThreshold(screenColor.rgb, _Params.x) * _Params.y;

		return luminance.rgb;
	}

	float4 FragBlend(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		
		float3 bloom = SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, SCREEN_COORDS).rgb * _Params.z;

		OUTPUT(screenColor.rgb + bloom.rgb)
	}

	float4 FragDebug(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		return SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, SCREEN_COORDS) * _Params.z;
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass //0
		{
			Name "Light Streaks: Luminance filter"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragLuminanceDiff

			ENDHLSL
		}
		Pass //1
		{
			Name "Light Streaks: Streak (Performance mode)"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragBlurBox

			ENDHLSL
		}
		Pass //2
		{
			Name "Light Streaks: Streak (Appearance mode)"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragBlurGaussian

			ENDHLSL
		}
		Pass //3
		{
			Name "Light Streaks: Composite"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragBlend

			ENDHLSL
		}
		Pass //4
		{
			Name "Light Streaks: Debug"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragDebug

			ENDHLSL
		}
	}
}