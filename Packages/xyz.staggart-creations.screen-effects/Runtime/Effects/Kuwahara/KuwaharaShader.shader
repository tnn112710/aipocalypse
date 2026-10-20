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

Shader "Hidden/SC Screen Effects/Kuwahara"
{
	HLSLINCLUDE
	
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float _Radius;
	float4 _FadeParams;

	struct Window {
		int x1, y1, x2, y2;
	};
	
	static const int MAX_RADIUS = 8;
	
	//Based on the LightweightBloom method as described in GPU Pro chapter 5.1, translated from GLHSL
	float4 Kuwahara(float2 uv, float4 inputColor, half falloff = 1.0) 
	{
	    int radius = _Radius;
		
		radius *= falloff;
		
		//Sub-pixel radius, exit early
		if (radius < INPUT_TEXELSIZE.x) return inputColor;

	    float n = rcp((radius + 1) * (radius + 1));
	    float2 texel = INPUT_TEXELSIZE.xy;

	    float3 m0 = 0, m1 = 0, m2 = 0, m3 = 0;
	    float3 s0 = 0, s1 = 0, s2 = 0, s3 = 0;

	    for (int j = -MAX_RADIUS; j <= MAX_RADIUS; j++)
	    {
	        for (int i = -MAX_RADIUS; i <= MAX_RADIUS; i++)
	        {
	            if (i < -radius || i > radius || j < -radius || j > radius) continue;

	            float2 sampleUV = uv + float2(i, j) * texel;
	            float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUV, 0).rgb;
	            float3 cc = c * c;

	            if (i <= 0 && j <= 0)
	            {
	                m0 += c;
	                s0 += cc;
	            }
	            if (i >= 0 && j <= 0)
	            {
	                m1 += c;
	                s1 += cc;
	            }
	            if (i >= 0 && j >= 0)
	            {
	                m2 += c;
	                s2 += cc;
	            }
	            if (i <= 0 && j >= 0)
	            {
	                m3 += c;
	                s3 += cc;
	            }
	        }
	    }

	    float3 mean0 = m0 * n;
	    float3 var0 = abs(s0 * n - mean0 * mean0);
	    float bestSigma = var0.r + var0.g + var0.b;
	    float3 bestMean = mean0;

	    float3 mean1 = m1 * n;
	    float3 var1 = abs(s1 * n - mean1 * mean1);
	    float sigma1 = var1.r + var1.g + var1.b;
	    if (sigma1 < bestSigma) { bestSigma = sigma1; bestMean = mean1; }

	    float3 mean2 = m2 * n;
	    float3 var2 = abs(s2 * n - mean2 * mean2);
	    float sigma2 = var2.r + var2.g + var2.b;
	    if (sigma2 < bestSigma) { bestSigma = sigma2; bestMean = mean2; }

	    float3 mean3 = m3 * n;
	    float3 var3 = abs(s3 * n - mean3 * mean3);
	    float sigma3 = var3.r + var3.g + var3.b;
	    if (sigma3 < bestSigma) { bestMean = mean3; }

	    inputColor.rgb = bestMean;
	    return inputColor;
	}

	float4 Frag(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK

		float3 finalColor = Kuwahara(SCREEN_COORDS, screenColor).rgb;
		
		OUTPUT(finalColor);
	}

	float4 FragDepthAware(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float fadeFactor = LinearDepthFade(SAMPLE_LINEAR_DEPTH(SCREEN_COORDS), _FadeParams.x, _FadeParams.y, 0.0, 1.0);
		//return float4(fadeFactor.xxx, 1.0);
		
		float3 finalColor = Kuwahara(SCREEN_COORDS, screenColor, fadeFactor).rgb;
				
		OUTPUT(finalColor);
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Kuwahara"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}

		Pass
		{
			Name "Kuwahara (Depth-based)"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragDepthAware

			ENDHLSL
		}
	}
}