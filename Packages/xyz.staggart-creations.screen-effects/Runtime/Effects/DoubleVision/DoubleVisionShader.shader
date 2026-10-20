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

Shader "Hidden/SC Screen Effects/Double Vision"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	half4 _Params;
	
	#define INTENSITY _Params.x
	#define ANGLE _Params.y //in radians
	#define EDGE_MASK _Params.z //0-1

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR
		
		// Calculate direction from angle
	    float2 dir;
	    sincos(ANGLE, dir.y, dir.x);

		half2 uv = SCREEN_SPAN_COORDS;
	    // Compute distance from center [-1, 1]
	    float2 coords = 2.0 * uv - 1.0;
	    float2 edge = uv - coords * dot(coords, coords);
		float2 delta = (edge - uv);
		
	    // Blend falloff: 1.0 when EDGE_MASK is 0, fading to 0 at center when EDGE_MASK is 1
	    float2 mask = lerp(1.0, delta, EDGE_MASK);
	    
	    float2 offset = dir * (INTENSITY * mask);

	    screenColor.rgb += SCREEN_COLOR_RGB(SCREEN_COORDS - offset);
	    screenColor.rgb += SCREEN_COLOR_RGB(SCREEN_COORDS + offset);
				
		float3 finalColor = screenColor.rgb * 0.33;
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}