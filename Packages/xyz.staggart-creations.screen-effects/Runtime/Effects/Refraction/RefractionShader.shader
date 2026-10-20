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

Shader "Hidden/SC Screen Effects/Refraction"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

	TEXTURE2D(_RefractionNormal);
	float4 _Params;
	//X: Strength
	//Y: Tiling
	float4 _Tint;
	half2 _Offset;

	#define STRENGTH _Params.x
	#define TILING _Params.y
	
	float4 FragNormalMap(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		
		half2 uv = SCREEN_SPAN_COORDS;
		
		float4 normal = SAMPLE_TEXTURE2D(_RefractionNormal, sampler_LinearRepeat, (uv * TILING) - _Offset).rgba;

		normal.xyz = UnpackNormal(normal);
		//return float4(normal.xyz, 1.0);

		half diffuse = 1-(saturate(dot(normal.xyz, float3(0.0, 0.0, 1.0)) * 2.0 - 1.0) );
		//return diffuse;
		float scale = length(normal.xy);

		normal.xy += _ScreenParams.zw-1;
		
		float2 refraction = lerp(SCREEN_COORDS, (SCREEN_COORDS) - normal.rg, STRENGTH * scale);
		
		if (Masked(refraction))
		{
			return SCREEN_COLOR(SCREEN_COORDS);
		}
		float4 screenColor = ScreenColor(refraction);
		float3 finalColor = lerp(screenColor.rgb, _Tint.rgb, diffuse * STRENGTH * _Tint.a);
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Refraction by normal map"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment FragNormalMap

			ENDHLSL
		}
	}
}