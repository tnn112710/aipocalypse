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

Shader "Hidden/SC Screen Effects/Blur"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	#include "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/Blurring.hlsl"

	TEXTURE2D_X(_BlurredTex);
	SAMPLER(sampler_BlurredTex);
	float4 _FadeParams;
	
	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		return ScreenColor(SCREEN_COORDS);
	}

	float4 FragDepthFade(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;
		float3 blurredColor = SAMPLE_TEXTURE2D_X(_BlurredTex, sampler_LinearClamp, SCREEN_COORDS).rgb;
		
		float depth = SAMPLE_DEPTH(SCREEN_COORDS);

		float fadeDist = LinearDepthFade(LINEAR_DEPTH(depth), _FadeParams.x, _FadeParams.y, 1.0, 1.0);
		//return float4(fadeDist.xxx, 1.0);

		screenColor.rgb = lerp(screenColor.rgb, blurredColor.rgb, fadeDist);
		
		OUTPUT(screenColor.rgb)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		
		Pass
		{
			Name "Copy"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}

		Pass
		{
			Name "Blur Depth Fade"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragDepthFade

			ENDHLSL
		}
	}
}