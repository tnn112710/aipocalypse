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

Shader "Hidden/SC Screen Effects/Gradient"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_Gradient);
	float _Intensity;
	float _Rotation;
	float4 _Color1;
	float4 _Color2;
	half _BlendMode;

	inline float3 BlendColors(float3 colors, float3 screenColor, float alpha) 
	{
		float3 color = 0;

		if (_BlendMode == 0) color = lerp(screenColor, colors, alpha * _Intensity);
		if (_BlendMode == 1) color = lerp(screenColor, BlendAdditive(colors, screenColor), alpha * _Intensity);
		if (_BlendMode == 2) color = lerp(screenColor, colors * screenColor, alpha * _Intensity);
		if (_BlendMode == 3) color = lerp(screenColor, BlendScreen(colors, screenColor), alpha * _Intensity);

		return color;
	}

	float4 FragColors(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;	
		APPLY_MASK
		
		float2 gradientUV = RotateUV(SCREEN_SPAN_COORDS, float2(0.5, 0.5), _Rotation);

		float4 colors = lerp(_Color2, _Color1, gradientUV.y);

		#ifndef UNITY_COLORSPACE_GAMMA
		colors.rgb = SRGBToLinear(colors.rgb);
		#endif
		
		float3 finalColor = BlendColors(colors.rgb, screenColor.rgb, lerp(_Color2.a, _Color1.a, gradientUV.y));

		OUTPUT(finalColor)
	}

	float4 FragTexture(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float2 gradientUV = RotateUV(SCREEN_SPAN_COORDS, float2(0.5, 0.5), _Rotation);

		float4 gradient = SAMPLE_TEXTURE2D(_Gradient, sampler_LinearClamp, gradientUV);
		
		float3 finalColor = BlendColors(gradient.rgb, screenColor.rgb, gradient.a);

		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		//2-colors
		Pass
		{
			Name "Gradient by colors"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragColors
			ENDHLSL
		}
		//Gradient texture
		Pass
		{
			Name "Gradient by texture"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragTexture
			ENDHLSL
		}
	}
}