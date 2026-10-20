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

Shader "Hidden/SC Screen Effects/Tilt Shift"
{
	Properties
    {
        //_BlitTexture ("Texture", 2D) = "white" {}
    	_Params ("_Params", Vector) = (0, 1, 0, 0)
    	_Offset ("_Offset", Float) = 1.0
    	_Angle ("_Angle", Float) = 1.0
    }
	HLSLINCLUDE

	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	float4 _BlurredTex_TexelSize;
	float4 _Params;
	//X: Size
	//Y: Falloff
	//Z: Amount
	//W: Mask type
	float _Offset;
	float _Angle;
	
	#define MASK_SIZE _Params.x
	#define MASK_FALLOFF _Params.y
	#define BLUR_SCALE _Params.z

	//Precomputed poisson disc kernel
	static const int SmallDiscKernelSamples = 12;
	static const float2 SmallDiscKernel[SmallDiscKernelSamples] =
	{
		float2(-0.326212,-0.40581),
		float2(-0.840144,-0.07358),
		float2(-0.695914,0.457137),
		float2(-0.203345,0.620716),
		float2(0.96234,-0.194983),
		float2(0.473434,-0.480026),
		float2(0.519456,0.767022),
		float2(0.185461,-0.893124),
		float2(0.507431,0.064425),
		float2(0.89642,0.412458),
		float2(-0.32194,-0.932615),
		float2(-0.791559,-0.59771)
	};

	static const int LargeDiscKernelSamples = 28;
	static const float3 LargeDiscKernel[LargeDiscKernelSamples] =
	{
		float3(0.62463,0.54337,0.82790),
		float3(-0.13414,-0.94488,0.95435),
		float3(0.38772,-0.43475,0.58253),
		float3(0.12126,-0.19282,0.22778),
		float3(-0.20388,0.11133,0.23230),
		float3(0.83114,-0.29218,0.88100),
		float3(0.10759,-0.57839,0.58831),
		float3(0.28285,0.79036,0.83945),
		float3(-0.36622,0.39516,0.53876),
		float3(0.75591,0.21916,0.78704),
		float3(-0.52610,0.02386,0.52664),
		float3(-0.88216,-0.24471,0.91547),
		float3(-0.48888,-0.29330,0.57011),
		float3(0.44014,-0.08558,0.44838),
		float3(0.21179,0.51373,0.55567),
		float3(0.05483,0.95701,0.95858),
		float3(-0.59001,-0.70509,0.91938),
		float3(-0.80065,0.24631,0.83768),
		float3(-0.19424,-0.18402,0.26757),
		float3(-0.43667,0.76751,0.88304),
		float3(0.21666,0.11602,0.24577),
		float3(0.15696,-0.85600,0.87027),
		float3(-0.75821,0.58363,0.95682),
		float3(0.99284,-0.02904,0.99327),
		float3(-0.22234,-0.57907,0.62029),
		float3(0.55052,-0.66984,0.86704),
		float3(0.46431,0.28115,0.54280),
		float3(-0.07214,0.60554,0.60982),
	};
	
	#if _HIGH_QUALITY
	#define SAMPLE_COUNT LargeDiscKernelSamples
	#else
	#define SAMPLE_COUNT SmallDiscKernelSamples
	#endif
	
	#if _HIGH_QUALITY
	#define KERNEL LargeDiscKernel
	#else
	#define KERNEL SmallDiscKernel
	#endif

	float GetMask(float2 uv)
	{		
		return _Params.w == 0 ? LinearMirroredMask(uv.xy, _Angle, MASK_SIZE, MASK_FALLOFF, _Offset) : RadialMask(uv, MASK_SIZE, MASK_FALLOFF);
	}
	
	float4 Frag(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float2 uv = SCREEN_COORDS;
		float mask = GetMask(uv);
		
		float2 poissonScale = mask * BLUR_SCALE * INPUT_TEXELSIZE.xy;

		float sampleRcp = rcp(SAMPLE_COUNT);
		float3 sum = 0;
		for (int l = 0; l < SAMPLE_COUNT; l++)
		{
			float2 sampleUV = uv.xy + KERNEL[l].xy * poissonScale.xy;
			float3 sample0 = ScreenColorClamped(sampleUV.xy).rgb;

			sum += sample0;
		}

		float3 finalColor = sum * sampleRcp;
		
		OUTPUT(finalColor)
	}

	float4 FragDebug(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		
		float mask = GetMask(SCREEN_SPAN_COORDS);

		return float4(mask.xxx, 1.0);
	}

	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass //Horizontal
		{
			Name "Tilt Shift"
			HLSLPROGRAM
			
			#pragma multi_compile_local_fragment _ _HIGH_QUALITY
			
			#pragma vertex Vert
			#pragma fragment Frag

			ENDHLSL
		}
		Pass //Debug
		{
			Name "Tilt Shift: Debug"
			HLSLPROGRAM

			#pragma vertex Vert
			#pragma fragment FragDebug

			ENDHLSL
		}
	}
}