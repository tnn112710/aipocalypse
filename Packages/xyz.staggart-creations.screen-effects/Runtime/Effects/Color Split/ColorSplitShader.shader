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

Shader "Hidden/SC Screen Effects/Color Split"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _Params;

	#define OFFSET _Params.x
	#define EDGEFADE _Params.y
	#define LUM_THRESHOLD _Params.z

	#define EDGE_SIZE 2.5
	#define EDGE_FALLOFF 2.0

	float Mask(float2 uv, float3 sourceColor, float minLuminance)
	{
		float mask = lerp(1.0, EdgeMask(uv, EDGE_SIZE, EDGE_FALLOFF), EDGEFADE);

		float luminance = smoothstep(0, minLuminance, Luminance(sourceColor.rgb)) * (minLuminance + 1);
		mask = (mask * luminance);

		/* Scale down by depth?
		float depth = LINEAR_EYE_DEPTH(SAMPLE_DEPTH(uv));
		mask /= (depth * 0.01);
		*/

		return saturate(mask);
	}

	float4 FragSingle(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float mask = Mask(SCREEN_COORDS, screenColor.rgb, LUM_THRESHOLD);

		float offset = OFFSET * mask;
		float red = ScreenColor(SCREEN_COORDS - float2(offset, 0)).r;
		float blue = ScreenColor(SCREEN_COORDS + float2(offset, 0)).b;

		float4 splitColors = float4(red, screenColor.g, blue, screenColor.a);
		
		//return mask;

		OUTPUT(lerp(screenColor.rgb, splitColors.rgb, mask))
	}

	float4 FragDouble(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float mask = Mask(SCREEN_COORDS, screenColor.rgb, LUM_THRESHOLD);

		float offset = OFFSET * mask;
		float redX = ScreenColor(SCREEN_COORDS - float2(offset, 0)).r;
		float redY = ScreenColor(SCREEN_COORDS - float2(0, offset)).r;
		
		float blueX = ScreenColor(SCREEN_COORDS + float2(offset, 0)).b;
		float blueY = ScreenColor(SCREEN_COORDS + float2(0, offset)).b;

		float4 splitColorsX = float4(redX, screenColor.g, blueX, screenColor.a);
		float4 splitColorsY = float4(redY, screenColor.g, blueY, screenColor.a);

		float4 blendedColors = (splitColorsX + splitColorsY) * 0.5;
		
		//return mask;

		OUTPUT(lerp(screenColor.rgb, blendedColors.rgb, mask))
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Color Split: Horizontal"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragSingle

			ENDHLSL
		}
		Pass
		{
			Name "Color Split: Horizontal + Vertical"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragDouble

			ENDHLSL
		}
	}
}