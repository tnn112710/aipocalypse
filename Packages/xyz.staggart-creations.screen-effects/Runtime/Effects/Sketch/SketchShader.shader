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

Shader "Hidden/SC Screen Effects/Sketch"
{
	HLSLINCLUDE

	#define REQUIRE_DEPTH
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_Strokes);

	uniform float4 _Params;
	//X: Intensity
	//Y: Offset
	//Z: Blending mode
	//W: Tiling
	half _Contrast;
	uniform float2 _Thresholds;
	//X: Dark
	//Y: Light
	
	#define INTENSITY _Params.x
	#define OFFSET _Params.y
	#define BLEND_MODE _Params.z
	#define TILING _Params.w

	float Hatching(float2 uv, float NdotL) 
	{
		//R: Highlight strokes. G: Shadow strokes.
		half3 tex = SAMPLE_TEXTURE2D(_Strokes, sampler_LinearRepeat, uv).rgb;
		
		float light = smoothstep(0, _Thresholds.y, NdotL) + NdotL;
		float shadow = smoothstep(0, _Thresholds.x, NdotL) + NdotL;
		//return shadow;
		
		half hatchLight = saturate(smoothstep(tex.g, tex.g + 1.0, light));
		half hatchShadow = saturate(smoothstep(tex.r, tex.r + 1.0, shadow));

		half hatch = (hatchLight + hatchShadow);

		return saturate(hatch);
	}

	float3 Blend(float3 color, float3 hatch) 
	{
		float3 col = color.rgb;

		//Effect-only
		if (BLEND_MODE == 0)
			col = lerp(color.rgb, hatch.rgb, INTENSITY);
		//Multiply
		if (BLEND_MODE == 1)
			col = lerp(color.rgb, color.rgb * hatch.rgb, INTENSITY);
		//Add
		if (BLEND_MODE == 2)
		{
			col = lerp(color.rgb, color.rgb + hatch.rgb,INTENSITY);
		}

		return col;
	}

	float4 FragWorldSpace(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;

		APPLY_MASK
		
		float depth = SAMPLE_DEPTH(SCREEN_COORDS);

		//return DebugEyeIndex();

		float3 worldPos = GetWorldPosition(SCREEN_COORDS, depth);
		//return float4(frac(worldPos), 1.0);
		
		float3 worldUV = (worldPos.xyz + OFFSET) * 0.01 * TILING;
		float2 uvX = worldUV.zy;
		float2 uvY = worldUV.xz;
		float2 uvZ = worldUV.xy;

		//Use luminance to create a psuedo diffuse light weight
		float luminance = Luminance((screenColor.rgb));

		//return luminance;

		float hatchX = Hatching(uvX, luminance);
		float hatchY = Hatching(uvY, luminance);
		float hatchZ = Hatching(uvZ, luminance);

		float3 hatch = (hatchX + hatchY + hatchZ) * 0.33;
		//return float4(hatch, 1.0);

		if (LINEAR_DEPTH(depth) > SKYBOX_LINEAR_DEPTH) hatch = 1.0;

		AdjustContract(hatch.rgb, _Contrast);
		
		float3 col = Blend(screenColor.rgb, hatch.rgb);
		
		OUTPUT(col)
	}

	float4 FragScreenSpace(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;

		APPLY_MASK
		
		half luminance = Luminance(screenColor.rgb);

		float2 uv = SCREEN_SPAN_COORDS;
		
#if !defined(UNITY_STEREO_INSTANCING_ENABLED)
		uv.x *= _ScreenParams.x / _ScreenParams.y;
#endif
		
		float3 hatch = Hatching(uv * _Params.w, luminance);

		float3 col = Blend(screenColor.rgb, hatch.rgb);
		
		OUTPUT(col)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass //0
		{
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragWorldSpace

			ENDHLSL
		}

		Pass //1
		{
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragScreenSpace

			ENDHLSL
		}
	}
}