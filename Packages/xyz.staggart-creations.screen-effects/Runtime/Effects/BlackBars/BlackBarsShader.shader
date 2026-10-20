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

Shader "Hidden/SC Screen Effects/Black Bars"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float2 _Size;

	float4 FragHorizontal(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR

		half bars = min(uv.y, (1-uv.y));
		bars = step(_Size.x * _Size.y+0.01, bars);

		float3 finalColor = screenColor.rgb * bars;
		
		OUTPUT(finalColor)
	}

	float4 FragVertical(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR

		half bars = (uv.x * (1-uv.x));
		bars = step(_Size.x * (_Size.y /2), bars);

		float3 finalColor = screenColor.rgb * bars;
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragHorizontal

			ENDHLSL
		}
		Pass
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragVertical

			ENDHLSL
		}
	}
}