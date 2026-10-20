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

Shader "Hidden/SC Screen Effects/Ripples"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_RipplesTexture);
	float _Strength;
	float _Distance;
	float _TimeOffset;
	float2 _Size;

	float4 FragRadial(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;

		float2 center = float2(0.5, 0.5);
		float x = (center.x - SCREEN_SPAN_COORDS.x) * _Size.x;
		float y = (center.y - SCREEN_SPAN_COORDS.y) * _Size.y;

		float r = -(x * x + y * y) * _Distance * _ScreenSize.x * 0.1;
		float ripples = 0.5 * sin(r + _TimeOffset) + 0.5;

		uv += (ripples * _Strength);
		SAMPLE_SCREEN_COLOR;
		
		APPLY_MASK_AT(uv);
		
		return ScreenColor(uv);
	}
	
	float4 FragTexture(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		float2 uv = SCREEN_SPAN_COORDS;
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		uv = uv * _Distance;
		float2 uv1 = uv;
		float2 uv2 = uv * 0.85;
		
		half offset = _TimeOffset * 0.01;
		uv1 += offset;
		uv2 -= offset;
		
		half ripplesTex1 = SAMPLE_TEXTURE2D(_RipplesTexture, sampler_LinearRepeat, uv1).r;
		half ripplesTex2 = SAMPLE_TEXTURE2D(_RipplesTexture, sampler_LinearRepeat, uv2).r;
		
		half ripples = (ripplesTex1 + ripplesTex2) * 2.0 - 1.0;
		//return float4(ripples.xxx, 1.0);

		float2 distortedUV = SCREEN_COORDS + ripples * _Strength;

		//APPLY_MASK_AT(distortedUV)
		screenColor = ScreenColor(distortedUV);
		
		OUTPUT(screenColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		//Ripples
		Pass
		{
			Name "Ripples: Radial"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragRadial

			ENDHLSL
		}
		//Bi-directional
		Pass
		{
			Name "Ripples: Displacement Texture"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragTexture

			ENDHLSL
		}
	}
}