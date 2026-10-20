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

Shader "Hidden/SC Screen Effects/Anaglyph 3D"
{
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"
	
	float4 _Params;
	//X: Glasses type (enum)
	//Y: Eye Separation
	//Z: Focal Point
	//W: Monochrome

	float4 Frag(Varyings input): SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR;

		APPLY_MASK
		
		float rawDepth = SAMPLE_DEPTH(uv);
		
		//Skybox or void. Infinitely far away, so separation would resolve to 0 anyway.
		if (rawDepth == 0)
		{
			return screenColor;
		}
		
		float eyeDepth = LINEAR_EYE_DEPTH(rawDepth);
		//Luminance based separation, used in applications where depth isn't available, but is wilderly inaccurate.
		//eyeDepth = (1.0-Luminance(saturate(screenColor.rgb))) * 10;
		
		float focalPoint = _Params.z;
		//focalPoint = min(_Params.z, SAMPLE_EYE_DEPTH(float2(0.5, 0.5)));
		half eyeSeparation = _Params.y;
		
		float parallax = (focalPoint - eyeDepth) / focalPoint;
		//return float4(parallax.xxx, 1.0);
		half offset = max(0, eyeSeparation * parallax);
		
		float2 uvL = uv - float2(INPUT_TEXELSIZE.x * offset, 0);
		float2 uvR = uv + float2(INPUT_TEXELSIZE.x * offset, 0);
		
		float3 leftColor = (ScreenColorClamped(uvL).rgb);
		float3 rightColor = (ScreenColorClamped(uvR).rgb);
		
		if (_Params.w > 0.5)
		{
			leftColor = Luminance(leftColor);
			rightColor = Luminance(rightColor);
		}
		
		//Red/cyan (default)
		float r = leftColor.r;
		float g = rightColor.g;
		float b = rightColor.b;
		
		uint glassesType = _Params.x;
		//Green/magenta
		if (glassesType == 1)
		{
			r = rightColor.r;
			g = leftColor.g;
			b = rightColor.b;
		}
		//Green/red
		else if (glassesType == 2)
		{
			r = rightColor.r;
			g = leftColor.g;
			b = leftColor.b;
		}
		
		OUTPUT(float3(r, g, b))
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Anaglyph 3D"
			HLSLPROGRAM
			
			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
	}
}