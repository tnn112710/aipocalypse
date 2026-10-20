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

Shader "Hidden/SC Screen Effects/Colorize"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_ColorRamp);

	half4 _Params;
	//X: Intensity
	//Y: Luminance remap
	//Z: Blend mode
	#define _Intensity _Params.x
	#define _Remap _Params.yz
	#define _BlendMode _Params.w

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;

		APPLY_MASK
		
		half luminance = Luminance(screenColor.rgb);
		luminance = smoothstep(_Remap.x, _Remap.y, luminance); //Remap luminance (0-1)
		//return luminance;

		float4 colors = SAMPLE_TEXTURE2D(_ColorRamp, sampler_LinearClamp, float2(luminance, 0));
	
		float3 color = 0;

		if (_BlendMode == 0) color = lerp(screenColor.rgb, colors.rgb, colors.a * _Intensity);
		if (_BlendMode == 1) color = lerp(screenColor.rgb, BlendAdditive(colors.rgb, screenColor.rgb), colors.a * _Intensity);
		if (_BlendMode == 2) color = lerp(screenColor.rgb, colors.rgb * screenColor.rgb, _Intensity);
		if (_BlendMode == 3) color = lerp(screenColor.rgb, BlendScreen(colors.rgb, screenColor.rgb), colors.a * _Intensity);

		OUTPUT(color.rgb)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Colorize"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}