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

Shader "Hidden/SC Screen Effects/MotionTrails"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	
	TEXTURE2D_X(_MotionTrailsTextureCurrent);
	TEXTURE2D_X(_MotionTrailsTexturePrev);
	TEXTURE2D_X(_MotionTrailsTexture);

	float4 _Params;
	bool _Clamped;
	half3 _MotionTrailsDirection;

	#define LUMINANCE_THRESHOLD _Params.x
	#define INTENSITY _Params.y
	#define LENGTH _Params.z
	#define BLEND_MODE _Params.w
	
	TEXTURE2D_X(_MotionVectorTexture);
	half _MotionVectorsIntensity;

	float4 FragMask(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK

		half threshold = LUMINANCE_THRESHOLD;
		
		float3 luminance = LuminanceThreshold(screenColor.rgb, threshold);
		
		//Mask by rendering layer
		luminance *= RenderingLayerMask(SCREEN_COORDS);
		
		if (_Clamped) luminance = saturate(luminance);

		return float4(luminance.rgb, screenColor.a);
	}
	
	float4 FragAccumulate(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float4 currentMask = SAMPLE_TEXTURE2D_X_LOD(_MotionTrailsTextureCurrent, sampler_LinearClamp, SCREEN_COORDS.xy, _BlitMipLevel);
		
		half2 offset = 0;
		half2 scale = 1;
		if (_MotionVectorsIntensity > 0)
		{
			half2 motionVector = SAMPLE_TEXTURE2D_X_LOD(_MotionVectorTexture, sampler_LinearClamp, SCREEN_COORDS.xy, _BlitMipLevel).xy;
			offset -= motionVector * _MotionVectorsIntensity * ScreenEdgeMask(SCREEN_COORDS, 0.1);
		}
		
		float2 centerToPixel = SCREEN_COORDS - half2(0.5, 0.5);
		float2 direction = normalize(centerToPixel);
		offset += direction * _MotionTrailsDirection.z * 0.001;
		offset.xy -= _MotionTrailsDirection.xy * 0.001;
		
		float4 prevMask = SAMPLE_TEXTURE2D_X_LOD(_MotionTrailsTexturePrev, sampler_LinearClamp, (SCREEN_COORDS.xy * scale) + offset, _BlitMipLevel);
		
		float3 mask = 0;
		
		//Additive blending
		if (BLEND_MODE == 0)
		{
			mask = currentMask.rgb + (prevMask.rgb * LENGTH);
		}
		//Max blending
		else if (BLEND_MODE == 1)
		{
			mask = max(currentMask.rgb, (prevMask.rgb * LENGTH));
		}
		
		//mask = BlendScreen(currentMask.rgb, (prevMask.rgb * LENGTH));
		
		mask = min(mask, 10) * INTENSITY;
		
		return float4(mask.rgb, 1.0);
	}
	
	float4 FragBlend(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		
		float4 trailsColor = SAMPLE_TEXTURE2D_X_LOD(_MotionTrailsTexture, sampler_LinearClamp, SCREEN_COORDS, _BlitMipLevel);
		//return tracersColor;
		
		//screenColor.rgb += tracersColor.rgb;
		screenColor.rgb = max(screenColor.rgb, trailsColor.rgb);
		//screenColor.rgb = BlendOverlay(screenColor.rgb, tracersColor.rgb);
		
		OUTPUT(screenColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Motion Trails: Mask"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragMask

			ENDHLSL
		}

		Pass
		{
			Name "Motion Trails: Accumulate"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragAccumulate

			ENDHLSL
		}

		Pass
		{
			Name "Motion Trails: Blending"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment FragBlend

			ENDHLSL
		}
	}
}