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

Shader "Hidden/SC Screen Effects/Condition"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D(_Overlay);
	float4 _Color;
	float4 _Params;
	//X: Intensity
	//Y: Vignetting
	//Z: ...
	//W: Polar
	
	float4 _Texture1Params;
	//XY: Offset
	//Z: Tiling
	float4 _Texture2Params;
		
	float4 _ShadingParams;
	//X: Dual layer
	//Y: Falloff
	//Z: Falloff blend mode
	//W: 
	
	#define INTENSITY _Params.x
	#define POLAR_COORDS _Params.w > 0
	#define TILING_1 _Texture1Params.z
	#define TILING_2 _Texture2Params.z
	#define DUAL_LAYER _ShadingParams.x
	#define FALLOFF _ShadingParams.y
	
	#define VIGNETTE_SIZE _Params.y

	float Vignette(float2 uv)
	{
		float vignette = uv.x * uv.y * (1 - uv.x) * (1 - uv.y);
		return clamp(16.0 * vignette, 0, 1);
	}

	float4 Frag(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		half2 screenUV = SCREEN_SPAN_COORDS;
		half aspect = _ScreenParams.x / _ScreenParams.y;
		
		#if defined(UNITY_STEREO_INSTANCING_ENABLED)
		aspect *= 2;
		#endif
		
		float2 baseUV = POLAR_COORDS ? CartesianToPolar(screenUV) : screenUV;
		if (!POLAR_COORDS) baseUV.x *= aspect;
		if (POLAR_COORDS) baseUV.x *= 64;
		
		float2 uv1 = baseUV -_Texture1Params.xy;
		uv1 *= TILING_1;
		
		if (POLAR_COORDS)
		{
			uv1.y *= 2;
		}

		half3 sample1 = SAMPLE_TEXTURE2D_LOD(_Overlay, sampler_LinearRepeat, uv1, 0).rgb;
		float3 overlay = sample1;
		
		if (DUAL_LAYER)
		{
			half2 uv2 = baseUV -_Texture2Params.xy;
			uv2 *= TILING_2;
			
			half3 sample2 = SAMPLE_TEXTURE2D_LOD(_Overlay, sampler_LinearRepeat, uv2, 0).rgb * 0.75;
			overlay = max(sample1, sample2);
		}

		//Hard falloff
		if (_ShadingParams.z > 0)
		{
			half falloff = saturate(1-Vignette(screenUV, 1-(FALLOFF), 1, 16 * INTENSITY));
			//overlay = AddSub(overlay, (falloff)) * falloff;
			
			half gradient = saturate(1.0 - falloff);
			overlay = smoothstep(gradient, gradient + 1.0, overlay);
		}
		else
		{
			half falloff = RadialMask(screenUV);
			falloff = smoothstep(FALLOFF * 0.5, 0.5 + FALLOFF, falloff * INTENSITY);
			overlay = overlay * falloff * INTENSITY;
		}
		//return float4(overlay.rgb, 1);

		//Square
		//float vignette = (1-saturate(ScreenEdgeMask(screenUV, VIGNETTE_SIZE * INTENSITY))) * INTENSITY;
		float vignette = saturate(RadialMask(screenUV,  ((1-VIGNETTE_SIZE * INTENSITY)) * 0.5, 4) * 4.0);
		//return half4(vignette.xxx, 1);
		
		half3 color = overlay * _Color.rgb;
		//return float4(color.rgb, 1);
		half overlayAlpha = saturate(length(color) + vignette);
		//return float4(overlayAlpha.xxx, 1.0);
		
		//Incorperates the vignette color
		color.rgb = lerp(color, _Color.rgb, overlayAlpha);
		
		float alpha = saturate(vignette + overlayAlpha) * _Color.a  * INTENSITY;

		half3 finalColor = lerp(screenColor.rgb, color, alpha);
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Condition"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}