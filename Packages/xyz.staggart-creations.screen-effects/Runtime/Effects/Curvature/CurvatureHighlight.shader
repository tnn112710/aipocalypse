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

Shader "Hidden/SC Screen Effects/Curvature Highlight"
{
    HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	#include "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/Blending.hlsl"
	
	half _Threshold;
	half _Softness;
	half _Convexity;
	half _Convavity;
	int _Radius;
	
	float SampleCurvature(float2 uv, float exponent)
	{
	    float2 x = float2(INPUT_TEXELSIZE.x, 0);
	    float2 y = float2(0, INPUT_TEXELSIZE.y);
		
	    float2 left = SampleViewSpaceNormals(uv + x);
	    float2 right = SampleViewSpaceNormals(uv - x);
	    float2 down = SampleViewSpaceNormals(uv - y);
	    float2 up = SampleViewSpaceNormals(uv + y);
		
	    float sum = (left.x - right.x) + (up.y - down.y);
		
	    float curvature = 0.5 + sign(sum) * pow(abs(sum), exponent);
		
	    return saturate(curvature);
	}

	float AverageCurvature(float2 screenPos, int radius, float exponent, float softness) 
	{
	    float totalWeight = 0.0;
	    float curvature = 0.0;
		
		UNITY_LOOP
	    for (int i = -radius; i <= radius; i++)
	    {
			UNITY_LOOP
	        for (int j = -radius; j <= radius; j++)
	        {
	            int2 pixelOffset = int2(i, j);
	            half2 uv = screenPos + (pixelOffset * INPUT_TEXELSIZE.xy);
	        	
	            float weight = 1.0 / (dot(pixelOffset, pixelOffset) + softness);
	            totalWeight += weight;
	            curvature += weight * SampleCurvature(uv, exponent);
	        }
	    }

	    curvature /= totalWeight;
		
		return curvature;
	}
	
	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		SAMPLE_SCREEN_COLOR;
		//return screenColor;
		APPLY_MASK

		float curvature = AverageCurvature(SCREEN_COORDS, _Radius.x, _Threshold, _Softness);
		
		//Highlights
		half convexity = lerp(0, saturate(curvature * 2 - 1), _Convexity);
		//return float4(convexity.xxx, 1.0);
		
		//Crevices
		half concavity = lerp(1, saturate(curvature * 2.0), _Convavity);
		//return float4(concavity.xxx, 1.0);

		float3 finalColor = screenColor.rgb + convexity;
		finalColor.rgb *= concavity;
		
		OUTPUT(finalColor)
	}
	ENDHLSL

    SubShader
    {
    	Cull Off ZWrite Off ZTest Always

        Pass
        {
        	Name "Curvature Highlight"
        	
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            ENDHLSL
        }
    }
}