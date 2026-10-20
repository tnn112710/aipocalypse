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

Shader "Hidden/SC Screen Effects/Sun Shafts"
{
	HLSLINCLUDE

	#define REQUIRE_DEPTH
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	TEXTURE2D_X(_SunshaftBuffer);
	
	uint _SourceMode;
	//Disc
	//Luminance

	half _LuminanceThreshold;
	
	float4 _SunColor;
	half _BlurRadius;

	float3 _SunPosition; //In world-space
	half4 _SunScreenPosition; //In view-space
	//XY: Left eye
	//ZW: Right eye
	half3 _CameraForwardVector; //In world-space
	
	float _Intensity;
	float _Length;

	#define INTENSITY _Intensity
	#define LENGTH _Length

	#define BLUR_ITTERATIONS 12
	#define LOOP_RCP 0.09090 // 1/11

	#define VIEW_VECTOR _CameraForwardVector.xyz
	
	#if defined(UNITY_STEREO_INSTANCING_ENABLED)
	//Left and right eye positions in view-space
    #define SUN_POSITION (unity_StereoEyeIndex == 0 ? _SunScreenPosition.xy : _SunScreenPosition.zw)
    #else
	#define SUN_POSITION _SunScreenPosition.xy
	#endif
	
	//Converts the world-space position to screen space
	half2 GetSunPosition()
	{
		//World to clip space
		float4 positionCS = mul(UNITY_MATRIX_VP, float4(_SunPosition, 1.0));

		//Clip to view space
		float4 positionVS = positionCS * 0.5f;
	    positionVS.xy = float2(positionVS.x, positionVS.y *_ProjectionParams.x) + positionVS.w;
		positionVS.xy /= positionCS.w;

		return positionVS.xy;
	}

	//Without this, the sun shafts will also be visible in the opposite direction
	half GetViewingFactor()
	{
		float3 sunDir = normalize(_WorldSpaceCameraPos - _SunPosition.xyz);

		//Check the degree to which the camera is facing the sun's origin
		float viewFactor = saturate(dot(sunDir, VIEW_VECTOR));
		
		return saturate(viewFactor);
	}

	half FragSky(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		half2 sunPosition = SUN_POSITION - uv;
		//return float4(vec, 0, 1);

		//Correct for aspect ratio so the gradient remains circular
		float aspect = _ScreenParams.x / _ScreenParams.y;
		sunPosition.x *= aspect;

		half viewingFactor = GetViewingFactor();
		if (viewingFactor == 0) return 0;
		
		float dist = length(sunPosition.xy);
		half rayLength = saturate(LENGTH - dist);
		
		half emission = 0;
		
		//Source is a procedural disk
		if (_SourceMode == 0)
		{
			float dx = 2.0 * sunPosition.x * 0.5;
		    float dy = 2.0 * sunPosition.y * 0.5;
		    float hyp = (dx * dx + dy * dy);
			
			half disc = smoothstep(0, 1.0, 1-abs(hyp) * 2048);
			
			emission = saturate(disc) * viewingFactor * rayLength;
		}
		//Derrive emission source from skybox luminance
		//This does have a caveat: other emissive surfaces in front of the skybox will also contribute
		else
		{
			SAMPLE_SCREEN_COLOR;
			half3 luminance = LuminanceThreshold(screenColor.rgb, _LuminanceThreshold) * rayLength;
			
			emission = Luminance(luminance) * viewingFactor;
		}
		
		float depth = SAMPLE_DEPTH(uv);
		//Mask by skybox
		if (depth != 0) emission = 0;
		
		return emission;
	}

	float3 FragRadialBlur(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		half3 c = half3(0,0,0);

		float2 uv = SCREEN_COORDS;
		half2 sunPosition = SUN_POSITION - uv;
		
		UNITY_UNROLL
		for (int s = 0; s < BLUR_ITTERATIONS; s++)
		{
			c += SCREEN_COLOR_RGB(uv);

			uv.xy += sunPosition * _BlurRadius;
		}
		
		return c * LOOP_RCP;
	}

	float4 FragBlend(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		
		//return float4(_SunColor.rgb, 1.0);
		float3 sunshafts = SAMPLE_TEXTURE2D_X(_SunshaftBuffer, sampler_LinearClamp, SCREEN_COORDS).rrr;
		//return float4(sunshafts.rrr, screenColor.a);
		
		float3 color = sunshafts * _SunColor.rgb * INTENSITY;
		//return float4(color.rgb, 1.0);

		float3 finalColor = BlendAdditive(screenColor.rgb, color.rgb);

		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Sunshafts sky mask"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragSky

			ENDHLSL
		}
		Pass
		{
			Name "Sunshafts blur"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragRadialBlur

			ENDHLSL
		}
		Pass
		{
			Name "Sunshafts composite"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragBlend

			ENDHLSL
		}
	}
}