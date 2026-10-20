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

Shader "Hidden/SC Screen Effects/Transition"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_Gradient);
	SAMPLER(sampler_Gradient);

	float4 _Params;
	float4 _TransitionColor;

	#define PROGRESS _Params.x
	#define INVERT _Params.y
	#define SOFTNESS _Params.z

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;

		float gradientTex = SAMPLE_TEXTURE2D_LOD(_Gradient, sampler_Gradient, SCREEN_SPAN_COORDS, 0).r;
		if(INVERT > 0) gradientTex = 1-gradientTex;
		
		half progress = PROGRESS + (SOFTNESS * PROGRESS);
		float alpha = 1-smoothstep(progress - SOFTNESS, progress + SOFTNESS, gradientTex);

		float3 finalColor = lerp(screenColor.rgb, _TransitionColor.rgb, alpha * _TransitionColor.a);
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
	Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Transition"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}