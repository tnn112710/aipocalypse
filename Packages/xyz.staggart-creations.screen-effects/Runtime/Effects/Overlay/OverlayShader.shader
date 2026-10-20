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

Shader "Hidden/SC Screen Effects/Overlay"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_OverlayTex);
	float4 _Params;
	//X: Intensity
	//Y: Tiling
	//Z: Auto aspect (bool)
	//W: Blend mode
	float _LuminanceThreshold;

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float2 uv = SCREEN_SPAN_COORDS;
		if(_Params.z == 1) uv.x *= _ScreenParams.x / _ScreenParams.y;
		uv *= _Params.y;
		
		float4 overlay = SAMPLE_TEXTURE2D(_OverlayTex, sampler_LinearRepeat, uv);

		float luminance = smoothstep(-0.01,  _LuminanceThreshold, Luminance(screenColor));
		//return float4(luminance.xxx, 1);
		overlay.a *= luminance;
		
		float3 color = 0;

		if (_Params.w == 0) color = lerp(screenColor.rgb, overlay.rgb, overlay.a * _Params.x);
		if (_Params.w == 1) color = lerp(screenColor.rgb, BlendAdditive(overlay.rgb, screenColor.rgb), overlay.a * _Params.x);
		if (_Params.w == 2) color = lerp(screenColor.rgb, overlay.rgb * screenColor.rgb, overlay.a * _Params.x);
		if (_Params.w == 3) color = lerp(screenColor.rgb, BlendScreen(overlay.rgb, screenColor.rgb), overlay.a * _Params.x);

		OUTPUT(color)
	}


	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Texture overlay"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}