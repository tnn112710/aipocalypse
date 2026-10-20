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

Shader "Hidden/SC Screen Effects/Sharpen"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _Params;
	//X: Amount
	//Y: Radius
	//Z: Contrast

	static const int kernelSize = 4;
	static const float kernelSize_RCP = 0.25;
	static const float2 kernel[kernelSize] = {
		float2(-1,-1),
		float2(1,-1),
		float2(-1,1),
		float2(1,1)
	};

	#define Sharpening _Params.x
	#define Radius _Params.y
	#define Contrast _Params.z * 1.25

	float3 AverageNeighbors(float2 uv, float2 texelSize, float radius)
	{
		float3 sum = 0;

		UNITY_UNROLL
		for(int i = 0; i < kernelSize; i++)
		{
			sum += ScreenColor(uv + (kernel[i] * texelSize.xy) * radius).rgb;
		}

		return sum * kernelSize_RCP;
	}

	float4 FragLuminanceEnhancement(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR;
		
		if (uv.x >= 0.99 || uv.y >= 0.99 || uv.x <= 0.01 || uv.y <= 0.01) return screenColor;

		APPLY_MASK
		
		float2 texelSize = 1.0 / _ScreenParams.xy;
	    float3 sourceColor = screenColor.rgb;
	    float3 average = AverageNeighbors(uv, texelSize, Radius);
	    float3 sharpenedColor = sourceColor + (sourceColor - average) * Sharpening;

		return float4(sharpenedColor.rgb, screenColor.a);
	}

	//https://github.com/GPUOpen-Effects/FidelityFX-CAS
	// LICENSE
	// =======
	// Copyright (c) 2017-2019 Advanced Micro Devices, Inc. All rights reserved.
	// -------
	// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation
	// files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy,
	// modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
	// Software is furnished to do so, subject to the following conditions:
	// -------
	// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
	// Software.
	// -------
	// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
	// WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE AUTHORS OR
	// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
	// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
	float3 SampleOffset(float2 uv, float2 texelRadius, int x, int y)
	{
	    return ScreenColor(uv + float2(x, y) * texelRadius).rgb;
	}
	
	float3 SoftMin9(float3 a, float3 b, float3 c, float3 d, float3 e, float3 f, float3 g, float3 h, float3 i)
	{
    float3 mn = min(min(min(d, e), min(f, b)), h);
    float3 mn2 = min(mn, min(min(a, c), min(g, i)));
    return mn + mn2;
	}

	float3 SoftMax9(float3 a, float3 b, float3 c, float3 d, float3 e, float3 f, float3 g, float3 h, float3 i)
	{
	    float3 mx = max(max(max(d, e), max(f, b)), h);
	    float3 mx2 = max(mx, max(max(a, c), max(g, i)));
	    return mx + mx2;
	}
	
	//HLSL rcp function only supports SM 5.0+ (and not PS4) and throws a warning about vector truncation
	//Use helper function instead
	float3 rcp3(float3 input)
	{
		return 1.0 / input.xyz;
	}
	
	float4 FragContrastAdaptive(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR;

		APPLY_MASK
		
		if (uv.x >= 0.99 || uv.y >= 0.99 || uv.x <= 0.01 || uv.y <= 0.01) return screenColor;

		float2 radius = 1.0 / _ScreenParams.xy * Radius;
		// fetch a 3x3 neighborhood around the pixel 'e',
		// a b c
		// d(e)f
		// g h i
		float3 a = SampleOffset(uv, radius, -1, -1);
		float3 b = SampleOffset(uv, radius,  0, -1);
		float3 c = SampleOffset(uv, radius,  1, -1);
		float3 d = SampleOffset(uv, radius, -1,  0);
		float3 f = SampleOffset(uv, radius,  1,  0);
		float3 g = SampleOffset(uv, radius, -1,  1);
		float3 h = SampleOffset(uv, radius,  0,  1);
		float3 i = SampleOffset(uv, radius,  1,  1);

		// Soft min and max.
		// a b c b
		// d e f * 0.5 + d e f * 0.5
		// g h i h
		// These are 2.0x bigger (factored out the extra multiply).
		float3 mnRGB = SoftMin9(a, b, c, d, screenColor.rgb, f, g, h, i);
		float3 mxRGB = SoftMax9(a, b, c, d, screenColor.rgb, f, g, h, i);

		// Smooth minimum distance to signal limit divided by smooth max.
		float3 ampRGB = saturate(min(mnRGB, 2.0 - mxRGB) * rcp3(mxRGB));
		ampRGB = rsqrt(ampRGB);

		float peak = 8.0 - 3.0 * Contrast;
		float3 wRGB = -rcp3(ampRGB * peak);
		float3 rcpWeightRGB = rcp3(1.0 + 4.0 * wRGB);

		// 0 w 0
		// Filter shape: w 1 w
		// 0 w 0
		float3 cross = (b + d) + (f + h);
		float3 sharpenedColor = (cross * wRGB + screenColor.rgb) * rcpWeightRGB;
		
		float3 finalColor = lerp(screenColor.rgb, sharpenedColor, Sharpening);
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Sharpen (Luminance Enhancement)"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragLuminanceEnhancement

			ENDHLSL
		}
		Pass
		{
			Name "Sharpen (Contrast Adaptive)"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragContrastAdaptive

			ENDHLSL
		}
	}
}