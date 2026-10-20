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

Shader "Hidden/SC Screen Effects/LUT"
{
	HLSLINCLUDE

	#define REQUIRE_DEPTH
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_LUT_Near);
	TEXTURE2D(_LUT_Far);
	SamplerState sampler_LUT_Near;
	SamplerState sampler_LUT_Far;

	float4 _LUT_Near_TexelSize;
	//X: 1.0 / Width
	//Y: 1.0 / Height
	//Z: Resolution Width
	//W: Resolution Height

	float4 _LUT_Params;
	//X: Intensity
	//Y: Color inversion
	float4 _FadeParams;
	//X: Start distance
	//Y: End distance

	float4 _Params;
	//XYZ: Vibrance balance
	//W: Vibrance intensity
	
	#define LUTIntensity _LUT_Params.x
	#define ColorInversion _LUT_Params.y
	#define VibranceBalance _Params.xyz
	#define VibranceStrength _Params.w

	//Note: When the GLES/OpenCL API is used TEXTURE2D_ARGS does not accept a sampler parameter
    //2.1.1, macros handle this now, safe to pass in a samplerstate

	float3 FormatStrip(half3 uvw, half3 scaleOffset)
	{
		//Strip format where `height = sqrt(width)`
		uvw.z *= scaleOffset.z;
		half shift = floor(uvw.z);
		uvw.xy = uvw.xy * scaleOffset.z * scaleOffset.xy + scaleOffset.xy * 0.5;
		uvw.x += shift * scaleOffset.y;

		uvw.z -= shift;

		return uvw;
	}

	half3 ApplyLut2d(TEXTURE2D_PARAM(tex, samplerTex), half3 uvw, half3 scaleOffset)
	{
		uvw = FormatStrip(uvw, scaleOffset);

		float3 centerLut = SAMPLE_TEXTURE2D(tex, samplerTex, uvw.xy).rgb;
		float3 offsetLut = SAMPLE_TEXTURE2D(tex, samplerTex, uvw.xy + half2(scaleOffset.y, 0)).rgb;

		uvw.xyz = lerp(centerLut, offsetLut, uvw.z);

		return uvw;
	}

	float3 GetScaleOffset()
	{
		return float3(_LUT_Near_TexelSize.x, _LUT_Near_TexelSize.y, _LUT_Near_TexelSize.w - 1.0);
	}
    
    inline float3 Grade(TEXTURE2D_PARAM(lut, samplerTex), half3 rgb) 
	{
		half3 colorGraded;

		//Working with LDR information. Effect must execute after Bloom. So this breaks the HDR buffer in URP
		rgb = saturate(rgb);
        
        #if !UNITY_COLORSPACE_GAMMA //Linear
        rgb = LinearToSRGB(rgb);
        #endif

        colorGraded = ApplyLut2d(TEXTURE2D_ARGS(lut, samplerTex), rgb, GetScaleOffset());

		#if !UNITY_COLORSPACE_GAMMA //Linear
        colorGraded = SRGBToLinear(colorGraded);
		#endif

		return colorGraded;
	}

	void ApplyColorGrading(inout float3 color, float2 uv, bool depthBlend)
	{
		if(LUTIntensity > 0)
		{
			float3 colorGraded = 0;

			//Constant at compile time, so will properly branch
			if(depthBlend)
			{
				float depth = SAMPLE_DEPTH(uv);

				half3 gradedNear = Grade(TEXTURE2D_ARGS(_LUT_Near, sampler_LUT_Near), color.rgb);
				half3 gradedFar = Grade(TEXTURE2D_ARGS(_LUT_Far, sampler_LUT_Far), color.rgb);

				float fadeDist = LinearDepthFade(LINEAR_DEPTH(depth), _FadeParams.x, _FadeParams.y, 0.0, 1.0);
		
				colorGraded = lerp(gradedFar, gradedNear, fadeDist);
			}
			else
			{
				colorGraded = Grade(TEXTURE2D_ARGS(_LUT_Near, sampler_LUT_Near), color.rgb);		
			}

			color = lerp(color.rgb, colorGraded.rgb, LUTIntensity);
		}
	}

	void ApplyVibrancy(inout float3 color)
	{
		if(VibranceStrength > 0)
		{
			float3 inputColor = saturate(color);
			const float luminance = Luminance(inputColor);

			const float maxColor = max3(inputColor);
			const float minColor = min3(inputColor);

			const float saturation = maxColor - minColor;

			const float3 vibrance = VibranceBalance * VibranceStrength;
		
			color = lerp(luminance, color, 1.0 + (vibrance * (1.0 - (sign(vibrance) * saturation))));
		}
	}

	void ApplyColorInversion(inout float3 color)
	{
		if(ColorInversion > 0)
		{
			float3 inputColor = color;
		
			#if !UNITY_COLORSPACE_GAMMA //Linear
			inputColor.rgb = LinearToSRGB(inputColor.rgb);
			#endif
		
			float3 inverted = 1.0-(inputColor.rgb);

			#if !UNITY_COLORSPACE_GAMMA //Linear
			inverted = SRGBToLinear(inverted);
			#endif

			color = lerp(color, inverted, ColorInversion);
		}
	}

	float4 FragSingle(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK

		ApplyColorGrading(screenColor.rgb, SCREEN_COORDS, false);
		
		ApplyVibrancy(screenColor.rgb);

		ApplyColorInversion(screenColor.rgb);

		OUTPUT(screenColor)
	}

	float4 FragDuo(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK

		ApplyColorGrading(screenColor.rgb, SCREEN_COORDS, true);

		ApplyVibrancy(screenColor.rgb);

		ApplyColorInversion(screenColor.rgb);

		OUTPUT(screenColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Color Grading LUT"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment FragSingle

			ENDHLSL
		}
		Pass //Depth based
		{
			Name "Dual Color Grading LUT"
			HLSLPROGRAM

			
			#pragma vertex Vert
			#pragma fragment FragDuo

			ENDHLSL
		}
	}
}