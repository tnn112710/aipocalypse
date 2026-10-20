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

Shader "Hidden/SC Screen Effects/Kaleidoscope"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _Params;
	//X: Split count
	//Y: Maintain aspect ratio (bool)
	//Z: Pivot X
	//W: Pivot Y
	half _Intensity;
	bool _ReflectBorder;

	float2 _KaleidoscopeSplits; //Horizontal & Vertical

	#define SPLIT_COUNT _Params.x
	
	const float PI2 = 6.2831853;

	float2 Kaleidoscope(Varyings input)
	{
		float2 center = SCREEN_COORDS - float2(_Params.z, _Params.w);

		//Maintain aspect ratio, otherwise converted to 1:1
		if(_Params.y == 0) center.x *= _ScreenParams.x / _ScreenParams.y;
		
		//Horizontal/vertical tiling
		center.x *= _KaleidoscopeSplits.x;
		center.y *= _KaleidoscopeSplits.y;
		
		// Convert to polar coordinate.
		float phi = atan2(center.y, center.x);
		float r = sqrt(dot(center, center));

		// Angular repeating.
		half splitCount = SPLIT_COUNT;
		
		half2 uv = 0;

		phi = phi - splitCount * floor(phi / splitCount);
		phi = min(phi, splitCount - phi);
		
		// Convert back to the cartesian coordinate.
		uv = float2(cos(phi), sin(phi)) * r + 0.5;

		// Reflection at the border of the screen.
		if (_ReflectBorder) uv = max(min(uv, 2.0 - uv), -uv);

		return uv;
	}

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = lerp(SCREEN_COORDS, Kaleidoscope(input), _Intensity);
		
		return ScreenColorTiled(uv);
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Kaleidoscope"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}