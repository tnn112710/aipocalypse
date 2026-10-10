Shader "Hidden/GlobalSnow/DecalDraw" {
Properties {
	_MainTex ("Main RGBA", 2D) = "black" {}
	_TargetUV("Target UV", Vector) = (0,0,0,0)
	_DrawDist("Draw Distance", Float) = 0
	_EraseSpeed("Erase Speed", Float) = 0.005
	_WorldPos ("WorldPos", Vector) = (0,0,0)
}

SubShader {
   	ZTest Always Cull Off ZWrite Off
   	Fog { Mode Off }

	CGINCLUDE
	#include "UnityCG.cginc"
    
	uniform sampler2D_float _MainTex;
    uniform sampler2D_float _GS_DepthTexture;
	uniform float4 _MainTex_TexelSize;
	uniform float4 _TargetUV;
    uniform float4 _TargetUVArray[64];
	uniform float3 _WorldPos;
    uniform float4 _WorldPosArray[64];
    uniform int _TargetCount;
	uniform float _DrawDist;
	uniform float _EraseSpeed;
	uniform float4 _GS_SnowCamPos;
	uniform float4 _GS_SnowData2;

	struct v2f {
	    float4 pos : SV_POSITION;
	    float2 uv: TEXCOORD0;
	};
	
	v2f vert( appdata_base v ) {
	    v2f o;
		o.pos = UnityObjectToClipPos(v.vertex);
    	o.uv = v.texcoord;
    	return o;
	}

	float4 fragFoot(v2f i) : SV_Target {
		float4 pixel = tex2D(_MainTex, i.uv);
		float2 dist = abs(i.uv - _TargetUV.xy);
		if (max(dist.x, dist.y)<_DrawDist) {
			// don't draw incorrectly over roof
			float4 st = float4(_WorldPos.xz - _GS_SnowCamPos.xz, 0, 0);
			st *= _GS_SnowData2.z;
			st += 0.5;
			float zd = tex2Dlod(_GS_DepthTexture, st).r;
			#if UNITY_REVERSED_Z
				zd = 1.0 - zd;
			#endif
			float aboveRoof = zd > (_WorldPos.y * 0.999);
			float3 newPixel = float3(_TargetUV.z, 1.0, _TargetUV.w);
			pixel.xyz = lerp(pixel.xyz, newPixel, aboveRoof);
		} else {
			pixel.y = saturate(pixel.y - _EraseSpeed);
		}
		return pixel;
	}
		
	float4 fragStamp(v2f i) : SV_Target {
		float4 pixel = tex2D(_MainTex, i.uv);
        pixel.y = max(pixel.y - _EraseSpeed, 0);

        for (int k=0;k<64;k++) {
            if (k>=_TargetCount) break;
		    // clear marks at far borders
            float4 targetUV = _TargetUVArray[k].xyzz;
            float drawDist = _TargetUVArray[k].w;
	    	float2 dd = abs(i.uv - targetUV.xy);
		    float inRange = dd.x < 0.498 || dd.x>0.5;
    		float inRange2 = dd.y < 0.498 || dd.y>0.5;
	    	pixel.xyz *= inRange * inRange2;

    		// avoid seams on edges
	    	dd = min(abs(dd - 1.0.xx), dd);
		
    		// draw or clear
	    	float dist = dot(dd,dd);
	        if (dist<drawDist) {
	            float mark = 1.25 - clamp(dist / drawDist, 0, 0.8);
		        pixel.y = max(pixel.y, mark);
                pixel.w = _WorldPosArray[k].w;
	        }
        }
		return pixel;
	}

    float4 fragRestore(v2f i) : SV_Target {
        float4 pixel = tex2D(_MainTex, i.uv);
        pixel.y = max(pixel.y - _EraseSpeed, 0);
		return pixel;
	}

	float4 fragCleaner(v2f i) : SV_Target {
		return float4(0,0,0,0);
	}

	// batched footprint producer: legacy pair stamps and per-foot slot stamps
    uniform float4 _FootTargetArray[8];
    uniform float4 _FootWorldArray[8];
    uniform int _FootTargetCount;

	#define FOOT_PACK_EMPTY 32898.0

	float FootAboveRoof(float3 worldPos) {
		float4 st = float4(worldPos.xz - _GS_SnowCamPos.xz, 0, 0);
		st *= _GS_SnowData2.z;
		st += 0.5;
		float zd = tex2Dlod(_GS_DepthTexture, st).r;
		#if UNITY_REVERSED_Z
			zd = 1.0 - zd;
		#endif
		return zd > (worldPos.y * 0.999);
	}

	float4 fragFootBatch(v2f i) : SV_Target {
		float4 pixel = tex2D(_MainTex, i.uv);
		pixel.y = max(pixel.y - _EraseSpeed, 0);
		pixel.w = max(pixel.w - _EraseSpeed, 0);
		const float texelRadius = 0.5 / 2048.0;
		[loop] for (int k=0;k<8;k++) {
			if (k>=_FootTargetCount) break;
			float2 dist = abs(i.uv - _FootTargetArray[k].xy);
			if (max(dist.x, dist.y) >= texelRadius) continue;
			int cmd = (int)_FootWorldArray[k].w;
			// plants come from a verified foot contact; the roof test would reject them under the character itself
			if (cmd == 0 && FootAboveRoof(_FootWorldArray[k].xyz) < 0.5) continue;
			if (cmd == 0) {
				pixel = float4(_FootTargetArray[k].z, 1.0, _FootTargetArray[k].w, 0.0);
			} else {
				float ax = abs(pixel.x);
				if (ax < 1.5 || ax > 8388610.0) {
					pixel = float4(FOOT_PACK_EMPTY, 0.0, FOOT_PACK_EMPTY, 0.0);
				}
				if (cmd == 1) pixel.xy = float2(_FootTargetArray[k].z, 1.0);
				else          pixel.zw = float2(_FootTargetArray[k].z, 1.0);
			}
		}
		return pixel;
	}

	float4 fragFootRestore(v2f i) : SV_Target {
		float4 pixel = tex2D(_MainTex, i.uv);
		pixel.y = max(pixel.y - _EraseSpeed, 0);
		pixel.w = max(pixel.w - _EraseSpeed, 0);
		return pixel;
	}

	ENDCG
	
	Pass { // 0: footprint render
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment fragFoot
		#pragma target 3.0
		ENDCG
	}
	
	Pass { // 1: generic stamp render
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment fragStamp
		#pragma target 3.0
		ENDCG
	}
	
	Pass { // 2: rt initializer
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment fragCleaner
		ENDCG
	}	

    Pass { // 3: restore
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragRestore
        ENDCG
    }

    Pass { // 4: batched footprint render
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragFootBatch
        #pragma target 3.0
        ENDCG
    }

    Pass { // 5: footprint restore
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragFootRestore
        ENDCG
    }
}
}
