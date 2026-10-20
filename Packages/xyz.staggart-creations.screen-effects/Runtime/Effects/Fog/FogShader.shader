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

Shader "Hidden/SC Screen Effects/Fog"
{
	HLSLINCLUDE
	
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	#include "Fog.hlsl"
	
	float4 FragBlend(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;
				
		half2 uv = SCREEN_COORDS;
		float depth = SAMPLE_DEPTH(uv);
		float linearDepth = LINEAR_DEPTH(depth);
		
		half mask = SAMPLE_MASK;
		
		if (mask == 1 && linearDepth < SKYBOX_LINEAR_DEPTH) return screenColor;

		float3 worldPos = GetWorldPosition(uv, depth);
		
		//float heightFog = GetFogHeight(worldPos, 0);
		//return float4(heightFog.xxx, linearDepth > 0.99 ? 1 : 0);
		
		//Alpha is density, do not modify
		float4 fogColor = ComputeFog(worldPos, uv, linearDepth);
		//return float4(fogColor.aaa, 1);
		
		//Linear blend
		float3 finalColor = lerp(screenColor.rgb, fogColor.rgb, fogColor.a);

		OUTPUT(finalColor);
	}
	
	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		
		Pass
		{
			Name "Fog: Render Color"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragBlend
			ENDHLSL
		}
	}
}