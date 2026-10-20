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

Shader "Hidden/SC Screen Effects/Mosaic"
{
		HLSLINCLUDE

		#include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Shaders/Libraries/PostProcessing.hlsl"

		float4 _Params;
		
		#define INTENSITY _Params.x
		#define SIZE _Params.y
		#define HEX_ASPECT _Params.z

		float2 TrianglesUV(float2 uv, float size) 
		{
			float2 coord = floor(uv*size) / size;
			uv -= coord;
			uv *= size;

			//Intersect
			return coord + float2(
				//X
				step(1 - uv.y, uv.x) / (size),
				//Y                                            
				step(uv.x, uv.y) / (size)
			);
		}
		
		float2 triChecker(float2 uv, float size)
		{
			float2 m = mod(uv, size);
			float2 base = uv - m;
			uv = m / size;

			base.x *=  step(uv.x, uv.y);

			return base;
		}

		float2 SquareUV(float2 uv, float size) {
			float dx = 10*(1.0 /size);
			float dy = 10*(1.0 / size);
			float2 coord = float2(dx*floor(uv.x / dx),
				dy*floor(uv.y / dy));

			return coord;
		}

		//Translated with persmission from http://coding-experiments.blogspot.nl/2010/06/pixelation.html
		//Modified for Unity
		float2 HexUV(float2 hexIndex, float scale) 
		{
			int i = hexIndex.x ;
			int j = hexIndex.y;
			float2 r;
			r.x = i * scale;
			r.y = j * HEX_ASPECT + (i % 2.0) * HEX_ASPECT / 2.0;
			return r;
		}

		float2 HexIndex(float2 uv, float size) 
		{
			float2 r;

			int it = int(floor(uv.x / size));
			float yts = uv.y - float(it % 2.0) * HEX_ASPECT / 2.0;
			int jt = int(floor((1.0 / HEX_ASPECT) * yts));
			float xt = uv.x - it * size;
			float yt = yts - jt * HEX_ASPECT;
			int deltaj = (yt > HEX_ASPECT / 2.0) ? 1 : 0;
			float fcond = size * (2.0 / 3.0) * abs(0.5 - yt / HEX_ASPECT);

			if (xt > fcond) {
				r.x = it;
				r.y = jt;
			}
			else {
				r.x = it - 1;
				r.y = jt - (r.x % 2) + deltaj;
			}

			return r;
		}

		float4 FragTriangles(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float size = max(0, SIZE / INTENSITY);
			
			return SCREEN_COLOR(TrianglesUV(SCREEN_COORDS, size));
		}

		float4 FragHex(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float size = max(0, SIZE * INTENSITY);
			
			float2 hexIndex = HexIndex(SCREEN_COORDS, size);
			float2 hexUV = HexUV(hexIndex, size);
			
			float4 screenColor = SCREEN_COLOR(hexUV);
			
			return screenColor;
		}

		float4 FragCircle(Varyings input) : SV_Target
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

			float size = max(0, SIZE / INTENSITY);
			float circleSize = 1.0 / size;
	
			float aspect = _ScreenParams.y / _ScreenParams.x;
			float2 uv = SCREEN_COORDS;
			uv.x = uv.x / aspect;

			float2 blockPos = floor(uv * size);
			float2 blockCenter = blockPos * circleSize + circleSize * 0.5;

			float dist = length(uv - blockCenter) * size;

			blockCenter.x /= _ScreenParams.x / _ScreenParams.y;
			float4 finalColor = SCREEN_COLOR(blockCenter);

			if (dist > 0.45) finalColor = 0;
			//if (dist > 0.45 || dist < 0.23) return 0;

			return finalColor;
		}


		ENDHLSL

	SubShader
		{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			Name "Mosaic: Triangles"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment FragTriangles

			ENDHLSL
		}

		Pass
		{
			Name "Mosaic: Hex"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment FragHex

			ENDHLSL
		}
		Pass
		{
			Name "Mosaic: Circle"
			HLSLPROGRAM


			#pragma vertex Vert
			#pragma fragment FragCircle

			ENDHLSL
		}
	}
}