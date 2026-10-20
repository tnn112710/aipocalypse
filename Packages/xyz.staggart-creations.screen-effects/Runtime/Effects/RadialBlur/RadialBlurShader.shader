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

Shader "Hidden/SC Screen Effects/Radial Blur"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _Params;
	//X: Amount (0-1)
	//Y: Center U
	//Z: Center V
	//W: Rotation (Radians)
	float _SampleDistance;
	//Value in pixels

	#define AMOUNT _Params.x * 0.5
	#define CENTER float2(_Params.y, _Params.z)
	#define ANGLE _Params.w

	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		//Values based on UV, so normalized
		const float2 origin = CENTER - SCREEN_SPAN_COORDS.xy;
		const float2 direction = normalize(origin);
		float scale = AMOUNT * 0.05f;
		float2 destination = origin + (direction * scale);
		float length = distance(origin, destination);
		
		float texelSize = INPUT_TEXELSIZE.y * 4;
		float sampleDistance = _SampleDistance * texelSize; // Convert percentage (0-100) to normalized (0-1)
		int samples = (int)ceil(length / sampleDistance);
		
		int sampleCount = min(samples, 64);
		float stride = rcp(sampleCount);
		float sampleRCP = rcp(sampleCount);
		
		half4 color = half4(0,0,0,0);
		float2 uv = SCREEN_COORDS;
		
		const float2 blurVector = origin * stride * AMOUNT;

		float4 screenColor = half4(0,0,0,0);
		UNITY_LOOP
		for (int j = 0; j < sampleCount; j++)
		{
			float t = (float)j / (float)sampleCount;
			
			uv = RotateUV(uv, CENTER, t * ANGLE);
			
			uv += blurVector;
			
			//Scaled beyond screen bounds
			if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) continue;
			
			color += ScreenColor(uv);
			
			if (j == 0) screenColor = color;
		}

		float3 finalColor = color.rgb * sampleRCP;
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Radial Blur"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}