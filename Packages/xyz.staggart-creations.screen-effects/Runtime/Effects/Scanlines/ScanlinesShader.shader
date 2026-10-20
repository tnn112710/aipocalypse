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

Shader "Hidden/SC Screen Effects/Scanlines"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _Params;
	//X: Amount
	//Y: Intensity
	//Z: Offset (Script controlled)
	//0=Dark, 1=Light

	float4 Frag(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		half linesY = saturate(sin((SCREEN_COORDS.y * _Params.x) + _Params.z + _ScreenParams.w) );
		linesY = step(0.5, linesY);
		//half linesY = saturate(sin((SCREEN_COORDS.y * _ScreenParams.y / _Params.x * 4.0) + _Params.z) * 0.5 + 0.5);
		
		float3 color = (_Params.w == 0.0) ? screenColor.rgb * lerp(1, linesY, _Params.y) : screenColor.rgb + (linesY * _Params.y);

		OUTPUT(color)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always Blend Off

		Pass
		{
			Name "Scanlines"
			
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment Frag
			ENDHLSL
		}
	}

	Fallback Off
}
