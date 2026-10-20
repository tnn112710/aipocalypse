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

Shader "Hidden/SC Screen Effects/Pixelize"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _PixelizeParams;
	//XY: Pixel scale XY
	//Z: Scale
	//W: Center pixel (bool)

	#define CENTER_PIXEL _PixelizeParams.w > 0
	#define OFFSET CENTER_PIXEL ? 0 : 0.5
	#define PIXEL_SCALE _PixelizeParams.xy
	#define SCALE _PixelizeParams.z
	
	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		half2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR;
		
		const float offset = OFFSET;

		const float2 scale = (PIXEL_SCALE / _ScreenParams.xy) * SCALE;
		float x = round((SCREEN_COORDS.x / scale.x) + offset) * scale.x;
		float y = round((SCREEN_COORDS.y / scale.y) + offset) * scale.y;
		
		uv = float2(x,y);
		
		APPLY_MASK_AT(uv)
		//APPLY_MASK
		
		//Simply sample the input texture using the rounded SCREEN_COORDS coordinates
		float3 finalColor = ScreenColorClamped(uv).rgb;

		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Pixelize"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}