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

Shader "Hidden/SC Screen Effects/Edge Detection" {

	HLSLINCLUDE
	
	#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

	//Parameters
	uniform half3 _Influences;
	//X: Depth sensitivity
	//Y: Normal sensitivity
	//Z: Luminance sensitivity
	uniform float _EdgeSize;
	uniform float4 _EdgeColor;
	uniform float _EdgeOpacity;
	uniform float _Exponent;
	
	float4 _NoiseParams;
	//X: Enabled
	//Y: Scale
	//Z: Offset
	//W: Thickness
	float4 _NoiseParams2;
	//X: Clipping scale
	//Y: Clipping strength
	float _NoiseOffset;
	TEXTURE3D(_NoiseTex3D);
	
	float4 _FadeParams;
	//X: Start
	//Y: End
	//Z: Invert
	//W: Enabled
	
	uniform half _DebugSlider;
	
	#define DEPTH_THRESHOLD _Influences.x
	#define NORMAL_THRESHOLD _Influences.y
	#define COLOR_THRESHOLD _Influences.z

	inline half IsSame(half2 centerNormal, float centerDepth, half2 normal, float depth)
	{
		// difference in normals
		half2 diff = abs(centerNormal - normal.xy) * NORMAL_THRESHOLD;
		half isSameNormal = (diff.x + diff.y) * NORMAL_THRESHOLD < 0.1;
		// difference in depth
		float sampleDepth = depth;
		float zdiff = abs(centerDepth - sampleDepth);
		// scale the required threshold by the distance
		half isSameDepth = zdiff * DEPTH_THRESHOLD < 0.09 * centerDepth;

		// return:
		// 1 - if normals and depth are similar enough
		// 0 - otherwise

		return isSameNormal * isSameDepth;
	}
	
	half4 fragEdgeDetection(Varyings input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

		float2 uv = SCREEN_COORDS;
		SAMPLE_SCREEN_COLOR;
		APPLY_MASK
		
		float edge = 1.0;
		float centerDepth = SAMPLE_DEPTH(uv);
		float centerLinearDepth = LINEAR_DEPTH(centerDepth);
		//return float4(centerDepth, 0, 0, 1);

		//return float4(SAMPLE_NORMALS(uv), 1.0);
		
		half noiseSample = 0;
		half noiseThicknesss = 0;
		bool noiseEnabled = _NoiseParams.x > 0.5 && (_NoiseParams.z > 0 || _NoiseParams2.y > 0);
		float3 noiseCoords = 0;
		
		if (noiseEnabled && centerLinearDepth < 1.0)
		{
			float3 positionWS = GetWorldPosition(uv, centerDepth);
			//positionWS -= _WorldSpaceCameraPos;
			
			noiseCoords = (positionWS * _NoiseParams.y);
			noiseSample = SAMPLE_TEXTURE3D_LOD(_NoiseTex3D, sampler_LinearRepeat, noiseCoords + _NoiseOffset, 0).r * 2.0 - 1.0;
			//return float4(noiseSample.xxx, 1.0);

			//3D noise textures do not contain mipmaps, so exibit high frequency noise further away.
			//Mitigate this by attentuating it by depth
			noiseSample *= LinearDistanceFade(positionWS, 0, 50, 0, 1);
			noiseSample *= _NoiseParams.z;
			
			noiseThicknesss = noiseSample * _NoiseParams.w;
			float2 offset = (noiseSample) * INPUT_TEXELSIZE.xy;
			offset *= 2.0 - 1.0;
			//return float4(uv.xy + offset.xy, 0, 1.0);
			
			float offsetDepth = SAMPLE_DEPTH(uv + (offset * 2.0));
			
			//Reject offsets that have a large depth difference
			half depthDiff = offsetDepth - centerDepth;
			half threshold = 0.0001 ;
			if (abs(depthDiff) > threshold)
			{
				//return float4(inverseLinearDepth,0,0,1);
				//offset *= inverseLinearDepth;
			}
			
			uv += offset;
		}

		half2 radius = (_EdgeSize + noiseThicknesss) * INPUT_TEXELSIZE.xy;
		
		if (DEPTH_THRESHOLD > 0 || NORMAL_THRESHOLD > 0)
		{
			#if _SOBEL
			float4 depthsDiag;
			float4 depthsAxis;
		
			depthsDiag.x = SAMPLE_LINEAR_DEPTH(uv + half2(radius.x, radius.y)); // TR
			depthsDiag.y = SAMPLE_LINEAR_DEPTH(uv + half2(-radius.x, radius.y)); // TL
			depthsDiag.z = SAMPLE_LINEAR_DEPTH(uv - half2(-radius.x, radius.y)); // BR
			depthsDiag.w = SAMPLE_LINEAR_DEPTH(uv - half2(radius.x, radius.y)); // BL

			depthsAxis.x = SAMPLE_LINEAR_DEPTH(uv + half2(0, radius.y)); // T
			depthsAxis.y = SAMPLE_LINEAR_DEPTH(uv - half2(radius.x, 0)); // L
			depthsAxis.z = SAMPLE_LINEAR_DEPTH(uv + half2(radius.x, 0)); // R
			depthsAxis.w = SAMPLE_LINEAR_DEPTH(uv - half2(0, radius.y)); // B	
		
			half depthThreshold = clamp(1-DEPTH_THRESHOLD, 0.05, 1.0);
			depthsDiag -= centerLinearDepth * depthThreshold;
			depthsAxis /= centerLinearDepth * depthThreshold;

			const float4 HorizDiagCoeff = float4(1,1,-1,-1);
			const float4 VertDiagCoeff = float4(-1,1,-1,1);
			const float4 HorizAxisCoeff = float4(1,0,0,-1);
			const float4 VertAxisCoeff = float4(0,1,-1,0);

			float4 h = depthsDiag * HorizDiagCoeff + depthsAxis * HorizAxisCoeff;
			float4 v = depthsDiag * VertDiagCoeff + depthsAxis * VertAxisCoeff;

			float sobelX = dot(h, float4(1,1,1,1));
			float sobelY = dot(v, float4(1,1,1,1));
			float sobelEdge = sqrt(sobelX * sobelX + sobelY * sobelY);

			sobelEdge = 1.0 - pow(saturate(sobelEdge), _Exponent);
		
			edge *= sobelEdge;
			#else
			#if _HQ_EDGE_DETECTION
			//4 diagonal samples (Robert's cross operator)
		
			half2 uv0 = uv + half2(radius.x, radius.y);
			half4 sample1 = half4(SAMPLE_NORMALS(uv0), SAMPLE_LINEAR_DEPTH(uv0));
		
			half2 uv1 = uv + half2(-radius.x, -radius.y);
			half4 sample2 = half4(SAMPLE_NORMALS(uv1), SAMPLE_LINEAR_DEPTH(uv1));
		
			half2 uv2 = uv + half2(-radius.x, radius.y);
			half4 sample3 = half4(SAMPLE_NORMALS(uv2), SAMPLE_LINEAR_DEPTH(uv2));
		
			half2 uv3 = uv + half2(radius.x, -radius.y);
			half4 sample4 = half4(SAMPLE_NORMALS(uv3), SAMPLE_LINEAR_DEPTH(uv3));

			edge *= IsSame(sample1.xy, sample1.w, sample2.xy, sample2.w);
			edge *= IsSame(sample3.xy, sample3.w, sample4.xy, sample4.w);
			#else
			//2 diagonal samples (top-left and top-right). Axis-aligned operator.
			half3 centerNormal = SAMPLE_NORMALS(uv);
		
			half3 sample1 = SAMPLE_NORMALS(uv + half2(-radius.x, -radius.y));
			float depth1 = SAMPLE_LINEAR_DEPTH(uv + half2(-radius.x, -radius.y));
		
			half3 sample2 = SAMPLE_NORMALS(uv + half2(+radius.x, -radius.y));
			float depth2 = SAMPLE_LINEAR_DEPTH(uv + half2(+radius.x, -radius.y));

			edge *= IsSame(centerNormal.xy, centerLinearDepth, sample1.xy, depth1);
			edge *= IsSame(centerNormal.xy, centerLinearDepth, sample2.xy, depth2);
			#endif
			#endif
		}
		
		if (COLOR_THRESHOLD > 0)
		{
		    half3 sample1 = SCREEN_COLOR_RGB(uv + half2(-radius.x, -radius.y));
		    half3 sample2 = SCREEN_COLOR_RGB(uv + half2(+radius.x, -radius.y));
		    half3 sample3 = SCREEN_COLOR_RGB(uv + half2(-radius.x, +radius.y));
		    half3 sample4 = SCREEN_COLOR_RGB(uv + half2(+radius.x, +radius.y));
			
			half3 diff = screenColor.rgb * 4.0 - sample1 - sample2 - sample3 - sample4;
			half colorEdge = saturate(dot(diff, diff * COLOR_THRESHOLD));
			
			half influence = max(0.001, 1-COLOR_THRESHOLD);
			colorEdge = smoothstep(0, influence, colorEdge);
			
			edge = saturate(edge - colorEdge);
		}
		
		if (_FadeParams.w > 0.5)
		{
			half distanceFade = LinearDepthFade(centerLinearDepth, _FadeParams.x, _FadeParams.y, _FadeParams.z, _FadeParams.w);
			edge = saturate(edge + distanceFade);
		}
		
		half clipping = _NoiseParams2.y;
		if (clipping > 0)
		{
			//Resampled without animated offset
			half clipNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTex3D, sampler_LinearRepeat, noiseCoords * _NoiseParams2.x, 0).r;
			
			half clipMask = 1-step(clipping, clipNoise);
			edge = saturate(edge + clipMask);
		}
		
		half3 edgeColor = _EdgeColor.rgb;
		//edgeColor.rgb = 1-screenColor.rgb;
		float3 finalColor = lerp(screenColor.rgb, edgeColor, (1-edge) * _EdgeColor.a * _EdgeOpacity).rgb;
		
		if (_DebugSlider > 0)
		{
			half mask = CreateDebugSplit(SCREEN_COORDS, _DebugSlider);
			return float4(lerp(finalColor, edge, mask), screenColor.a);
		}
		
		OUTPUT(finalColor)
	}

	ENDHLSL

	Subshader 
	{
		ZTest Always Cull Off ZWrite Off
		
		Pass
		{
			Name "Edge Detection"
			
			HLSLPROGRAM
			
			#pragma multi_compile_local_fragment _ _SOBEL
			#pragma multi_compile_local_fragment _ _HQ_EDGE_DETECTION
			
			#pragma vertex Vert
			#pragma fragment fragEdgeDetection
			ENDHLSL
		}
	}

	Fallback off
} // shader