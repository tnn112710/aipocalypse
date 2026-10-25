// Fork of Kronnect Skybox Plus 2.1 "Skybox/Skybox Plus/Clouds" (Assets/_StoreAssets/Rendering/Kronnect/SkyboxPlus): the vendor shader
// unchanged except for STAR_MAP, a star cubemap in place of the procedural stars, turned by EnvironmentDirector's _StarMapRotation,
// and STAR_TWINKLE (global, GraphicsService), which makes the map's point stars twinkle.
Shader "Aipocalypse/Skybox/Skybox Plus Star Map" {
Properties {
	[Toggle(SKYBOX_HAZE)] _SkyboxHaze("Procedural Haze", Float) = 0
	_NoiseTex ("Noise", 2D) = "black" {}
	_HazeColor ("Color", Color) = (1,1,1,1)
	_HazeScale ("Scale", Range(0,1)) = 0.2
	_HazeThreshold("Threshold", Range(0,1)) = 0
	_HazeIntensity("Intensity", Float) = 1.0
	_HazeRotationAxis("Rotation Axis", Vector) = (0.0,-1.0,0.0)
	_HazeRotationSpeed("Rotation Speed", Float) = 0.2
    [Toggle(SKYBOX_LAYER_1)] _SkyboxLayer1 ("Layer 1 (Clouds)", Float) = 0
    [NoScaleOffset] _Tex1 ("Cubemap", Cube) = "black" {}
	_AlphaLayer1 ("Alpha", Range(0,1)) = 1.0
    _HShiftLayer1 ("Horizontal Shift", Range(-7, 7)) = 0
    _RotationSpeed1 ("Horizontal Speed", Float) = 0.05
	_VShiftLayer1 ("Vertical Shift", Range(-0.5,0.25)) = 0
	_PhaseLayer1 ("Vertical Motion", Range(-0.1,0.1)) = 0.01
    [Toggle(SKYBOX_LAYER_2)] _SkyboxLayer2 ("Layer 2 (Clouds)", Float) = 0
    [NoScaleOffset] _Tex2 ("Cubemap", Cube) = "black" {}
	_AlphaLayer2 ("Alpha", Range(0,1)) = 1.0
    _HShiftLayer2 ("Horizontal Shift", Range(-7, 7)) = 0
    _RotationSpeed2 ("Horizontal Speed", Float) = 0.08
	_VShiftLayer2 ("Vertical Shift", Range(-0.5,0.25)) = 0
	_PhaseLayer2 ("Vertical Motion", Range(-0.1,0.1)) = -0.015
    [Toggle(SKYBOX_LAYER_3)] _SkyboxLayer3 ("Layer 3 (Clouds)", Float) = 0
    [NoScaleOffset] _Tex3 ("Cubemap", Cube) = "black" {}
	_AlphaLayer3 ("Alpha", Range(0,1)) = 1.0
    _HShiftLayer3 ("Horizontal Shift", Range(-7, 7)) = 0
    _RotationSpeed3 ("Horizontal Speed", Float) = 0.11
	_VShiftLayer3 ("Vertical Shift", Range(-0.5,0.25)) = 0
	_PhaseLayer3 ("Vertical Motion", Range(-0.1,0.1)) = 0.02
    [Toggle(SKYBOX_LAYER_4)] _SkyboxLayer4 ("Layer 4 (Clouds)", Float) = 0
    [NoScaleOffset] _Tex4 ("Cubemap", Cube) = "black" {}
	_AlphaLayer4 ("Alpha", Range(0,1)) = 1.0
    _HShiftLayer4 ("Horizontal Shift", Range(-7, 7)) = 0
    _RotationSpeed4 ("Horizontal Speed", Float) = 0.14
	_VShiftLayer4 ("Vertical Shift", Range(-0.5,0.25)) = 0
	_PhaseLayer4 ("Vertical Motion", Range(-0.1,0.1)) = -0.025
    _SunColor ("Sun Tint", Color) = (1,0.9568,0.8392)
    _SunSize ("Sun Size", Range(0,1)) = 0.15
	_SunFlare("Sun Flare", Range(0, 1.0)) = 0.4
    [Toggle(SKYBOX_NIGHT)] _SkyboxNight ("Enable Night Time", Float) = 0
	[NoScaleOffset] _MoonTex ("Moon Texture", 2D) = "black" {}
    _MoonSize ("Moon Size Factor", Float) = 5
	_MoonFlare("Moon Flare", Range(0, 1.0)) = 0.03
	_StarSize ("Star Size", Range(10,1000)) = 200
	_StarDensity("Star Density", Range(0,0.35)) = 0.04
	_StarBrightness ("Star Brightness", Range(0, 1)) = 0.5
	_StarDarkColor("Star Darker Color", Color) = (1.0,0.3,0.1)
	_StarBrightColor("Star Brighter Color", Color) = (0.2,0.9,1.0)
	_StarBlinking("Star Blinking", Range(0, 1)) = 0.2
    _MoonPhase ("Moon Phase", Range(-1,1)) = 1
    _MoonPhaseAngle ("Moon Phase Angle", Range(0,360)) = 0
    _StarClustering ("Star Clustering", Range(0,1)) = 0
    [Toggle(STAR_MAP)] _StarMapOn ("Star Map (replaces procedural stars)", Float) = 0
    [NoScaleOffset] _StarMap ("Star Map", Cube) = "black" {}
    _StarMapIntensity ("Star Map Intensity", Float) = 1
    _StarTwinkle ("Star Map Twinkle Level (0 calm .. 1 strong)", Range(0, 1)) = 0.5
    _StarTwinkleSpeed ("Star Map Twinkle Speed (changes per second)", Range(0.5, 12)) = 4
    _StarTwinkleHorizon ("Star Map Twinkle Horizon Boost", Range(0, 3)) = 1.5
    _StarTwinkleCells ("Star Map Twinkle Cells (per radian, ~one per star)", Float) = 250
    _StarBandAxis ("Star Band Axis", Vector) = (0.35,0.85,0.4)
    _StarBandWidth ("Star Band Width", Range(0.05,1)) = 0.28
    _Moon2Size ("Second Moon Size", Range(0,0.5)) = 0
    [NoScaleOffset] _Moon2Tex ("Second Moon Texture", 2D) = "black" {}
    _Moon2Direction ("Second Moon Direction", Vector) = (0.6,0.5,-0.6)
    _Moon2Tint ("Second Moon Tint", Color) = (0.8,0.75,0.9,1)
    _Moon2Brightness ("Second Moon Brightness", Range(0,4)) = 1
    _Moon2Phase ("Second Moon Phase", Range(-1,1)) = 0.6
    _Moon2PhaseAngle ("Second Moon Phase Angle", Range(0,360)) = 30
    _RainbowIntensity ("Rainbow Intensity", Range(0,2)) = 0
    _RainbowRadius ("Rainbow Radius", Range(20,60)) = 42
    _RainbowWidth ("Rainbow Width", Range(0.5,12)) = 2.25
    _RainbowSecondary ("Secondary Bow", Range(0,1)) = 0.35
    _RainbowSaturation ("Rainbow Saturation", Range(0,1)) = 0.9
    _MeteorColor ("Color", Color) = (0.75,0.85,1,1)
    _MeteorFrequency ("Frequency", Range(0,1)) = 0
    _MeteorCount ("Simultaneous Meteors", Range(1,4)) = 2
    _MeteorDuration ("Duration (seconds)", Range(0.2,3)) = 0.7
    _MeteorSize ("Size", Range(0.5,4)) = 1
    _MeteorTrail ("Trail Length", Range(0.1,1)) = 0.5
    _MeteorBrightness ("Brightness", Range(0,4)) = 1.5
    _MeteorRadiant ("Radiant Direction", Vector) = (-0.35,0.8,0.45)
    _MeteorSpread ("Direction Spread", Range(0,1)) = 0.2
    _AuroraColor1 ("Base Color", Color) = (0.1,0.9,0.5,1)
    _AuroraColor2 ("High Color", Color) = (0.5,0.2,0.8,1)
    _AuroraIntensity ("Intensity", Range(0,4)) = 0
    _AuroraElevation ("Elevation", Range(0.1,0.9)) = 0.42
    _AuroraSpan ("Vertical Span", Range(0.1,1)) = 0.4
    _AuroraWaviness ("Waviness", Range(0,1)) = 0.55
    _AuroraRayDensity ("Ray Density", Range(0.5,8)) = 3
    _AuroraSpeed ("Speed", Range(0,2)) = 0.5
	_SkyTint ("Sky Tint", Color) = (0.52,0.5,1)
	_SkyNightColor ("Sky Night Color", Color) = (0,0,0)
	_NightTintColor ("Night Tint Color", Color) = (0,0,0.0002)
	[Toggle(USE_UNITY_FOG_COLOR)] _UseUnityFogColor ("Use Unity Fog Color", Float) = 0.0
	_FogColor ("Fog Color", Color) = (0.8,0.8,0.8)
	_FogBaseHeight("Fog Base Height", Range(-1, 1)) = 0
	_FogHeight("Fog Height", Range(0.1, 100)) = 1
	_FogPower("Fog Power", Range(0.1, 100)) = 3.0
	_FogMinAmount ("Fog Min Amount", Range(0, 1)) = 0
	_FogAmount ("Fog Amount Multiplier", Range(0, 1)) = 1.0
	_FogDaylightInfluence("Fog Daylight Influence", Range(0, 1)) = 1
	_FogAlpha("Fog Alpha", Range(0, 1)) = 1
	_Exposure ("Exposure", Float) = 1.0
}

SubShader {
	Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
	Cull Off ZWrite Off

	Pass {

		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		#pragma target 3.0
		#pragma shader_feature_local SKYBOX_LAYER_1
		#pragma shader_feature_local SKYBOX_LAYER_2
		#pragma shader_feature_local SKYBOX_LAYER_3
		#pragma shader_feature_local SKYBOX_LAYER_4
		#pragma shader_feature_local SKYBOX_NIGHT
		#pragma shader_feature_local SKYBOX_HAZE
		#pragma shader_feature_local USE_UNITY_FOG_COLOR
		#pragma shader_feature_local STAR_MAP
		// Global, set by GraphicsService (a player setting): off compiles the twinkle out entirely.
		#pragma multi_compile_fragment _ STAR_TWINKLE

		#include "UnityCG.cginc"
		#include "Lighting.cginc"

		samplerCUBE _Tex1, _Tex2, _Tex3, _Tex4;
        float _RotationSpeed1, _RotationSpeed2, _RotationSpeed3, _RotationSpeed4;
		half _AlphaLayer1, _AlphaLayer2, _AlphaLayer3, _AlphaLayer4;
		float _VShiftLayer1, _VShiftLayer2, _VShiftLayer3, _VShiftLayer4;
		float _PhaseLayer1, _PhaseLayer2, _PhaseLayer3, _PhaseLayer4;
		float _HShiftLayer1, _HShiftLayer2, _HShiftLayer3, _HShiftLayer4;
		half3 _SkyTint, _SkyNightColor, _NightTintColor;
		half _Exposure;

		#if USE_UNITY_FOG_COLOR
			#define _FogColor unity_FogColor
		#else
			half3 _FogColor;
		#endif
		half _FogMinAmount, _FogHeight, _FogAmount, _FogBaseHeight, _FogPower, _FogDaylightInfluence, _FogAlpha;
		float _StarSize,  _StarDensity, _StarBrightness, _StarBlinking;
		half3 _StarDarkColor, _StarBrightColor;
		half3 _MeteorColor;
		half _MeteorFrequency, _MeteorCount, _MeteorDuration, _MeteorSize, _MeteorTrail, _MeteorBrightness, _MeteorSpread;
		float3 _MeteorRadiant;
		half3 _AuroraColor1, _AuroraColor2;
		half _AuroraIntensity, _AuroraElevation, _AuroraSpan, _AuroraWaviness, _AuroraRayDensity, _AuroraSpeed;
		half _SunSize, _SunFlare;
		half3 _SunColor;
		sampler2D _MoonTex;
		float _MoonSize;
		half _MoonPhase, _MoonPhaseAngle;
		half _StarClustering, _StarBandWidth;
		samplerCUBE _StarMap;
		half _StarMapIntensity;
		half _StarTwinkle, _StarTwinkleSpeed, _StarTwinkleHorizon;
		float _StarTwinkleCells;
		// Global from EnvironmentDirector: sky direction -> star map direction (latitude and sidereal time).
		float4x4 _StarMapRotation;
		float3 _StarBandAxis;
		sampler2D _Moon2Tex;
		float3 _Moon2Direction;
		half3 _Moon2Tint;
		half _Moon2Size, _Moon2Brightness, _Moon2Phase, _Moon2PhaseAngle;
		half _RainbowIntensity, _RainbowRadius, _RainbowWidth, _RainbowSecondary, _RainbowSaturation;
		half _MoonFlare;
		sampler2D _NoiseTex;
		half _HazeScale;
		half4 _HazeColor;
		half _HazeIntensity, _HazeThreshold;
		float3 _HazeRotationAxis;
		float _HazeRotationSpeed;
		float4x4 _LightWL;
		float4x4 _MoonLightWL;
		float4 _WorldSpaceMoonDir;
		float3 _WorldSpaceSunDir;
		half _SBPCustomPositions;

		struct appdata
		{
			float4 vertex   : POSITION;
			float2 texcoord : TEXCOORD0;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f
		{
			float4 pos : SV_POSITION;
			float3 uvLayer1: TEXCOORD0;
			float3 uvLayer2: TEXCOORD1;
			float3 uvLayer3: TEXCOORD2;
			float3 uvLayer4: TEXCOORD3;
			float3 ray : TEXCOORD4;
			float4 scrPos : TEXCOORD5;
			float3 hazeRay : TEXCOORD6;
			// moon screen pos (custom positions) or light clip pos
			float4 posAux : TEXCOORD7;
			UNITY_VERTEX_OUTPUT_STEREO
		};


        float3 RotateAroundYInDegrees (float3 vertex, float angle) {
            float sina, cosa;
            sincos(angle, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float3(mul(m, vertex.xz), vertex.y).xzy;
        }

		float3x3 AngleAxis3x3(float angle, float3 axis) {
		    float c, s;
			sincos(angle, s, c);

			float t = 1 - c;
			float x = axis.x;
			float y = axis.y;
			float z = axis.z;

			return float3x3(
				t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
				t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
				t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
			);
		}


		float rand3(float3 xyz) {
			return frac(sin(dot(xyz, float3(12.9898,78.233,39.789))) * 43758.5453);
		}

		float hash13(float3 p3) {
			p3  = frac(p3 * float3(.1031,.11369,.13787));
			p3 += dot(p3, p3.yzx + 19.19);
			return frac((p3.x + p3.y) * p3.z);
		}

		// How much a star dims now (0..1). Each cell (about one star wide, turning with the star map) eases
		// between random values `_StarTwinkleSpeed` times a second at its own offset, faster and deeper low in
		// the sky. Not blended between cells: averaging neighbours cancels the flicker out.
		half StarTwinkleDim(float3 starDir, float altitude) {
			float3 cell = floor(normalize(starDir) * _StarTwinkleCells);
			half low = 1.0 - saturate(altitude * 2.0);
			float t = _Time.y * _StarTwinkleSpeed * (0.7 + 0.6 * hash13(cell + 5.1)) * (1.0 + low) + hash13(cell) * 64.0;
			float i = floor(t);
			float f = t - i;
			f = f * f * (3.0 - 2.0 * f);
			// Wrapped so the hash input stays small (precise) however long the game runs; both ends wrap together.
			float n = lerp(hash13(cell + float3(0, 0, fmod(i, 4096.0) * 7.13)),
			               hash13(cell + float3(0, 0, fmod(i + 1.0, 4096.0) * 7.13)), f);
			half amount = saturate(_StarTwinkle * 0.9 * (1.0 + _StarTwinkleHorizon * low));
			return amount * n * n;
		}

		float3 SafeNormalize(float3 value, float3 fallback) {
			float lengthSq = dot(value, value);
			return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
		}

		half TriplanarNoise(float3 position, float3 direction) {
			float3 weights = abs(direction);
			weights /= max(weights.x + weights.y + weights.z, 1e-4);
			return dot(half3(
				tex2D(_NoiseTex, position.zy).r,
				tex2D(_NoiseTex, position.xz).r,
				tex2D(_NoiseTex, position.xy).r), weights);
		}

		half3 GalacticProfile(float3 direction, float3 axis, half width) {
			half patch = TriplanarNoise(direction * 0.8 + 11.3, direction);
			half detail = TriplanarNoise(direction * 2.9 - 4.7, direction);
			half invWidth = rcp(max(width, 1e-3));
			half signedHeight = dot(direction, axis);
			half warpedHeight = signedHeight + (patch - 0.5) * width * 0.26 + (detail - 0.5) * width * 0.12;
			half height = abs(warpedHeight) * invWidth;
			half halo = 1.0 - smoothstep(0.12, 1.0, height);
			half core = 1.0 - smoothstep(0.02, 0.38, height);
			half knots = smoothstep(0.42, 0.78, patch);

			// frayed edge
			half fray = lerp(0.25, 1.2, saturate(detail * 0.72 + patch * 0.28));
			halo *= lerp(1.0, fray, smoothstep(0.16, 0.85, height));

			// dust clouds and filaments
			half dustNoise = saturate((1.0 - patch) * 0.62 + (1.0 - detail) * 0.38);
			half cloudMask = smoothstep(0.44, 0.72, dustNoise);
			half cloudHeight = abs(signedHeight + (patch - 0.5) * width * 0.34 + (detail - 0.5) * width * 0.26) * invWidth;
			half cloudWidth = lerp(0.12, 0.56, cloudMask);
			half dustClouds = (1.0 - smoothstep(cloudWidth * 0.22, cloudWidth, cloudHeight)) * cloudMask;

			half filamentMask = smoothstep(0.54, 0.76, saturate((1.0 - detail) * 0.70 + patch * 0.30));
			half filamentHeight = abs(signedHeight + (detail - 0.5) * width * 0.20) * invWidth;
			half dustFilament = (1.0 - smoothstep(0.018, 0.095, filamentHeight)) * filamentMask;

			half dustLane = saturate(dustClouds + dustFilament * 0.48);
			// central veil
			half axisHeight = abs(signedHeight) * invWidth;
			half centralVeil = (1.0 - smoothstep(0.0, 0.42, axisHeight)) * lerp(0.85, 1.0, dustNoise);
			half stellarMass = saturate(halo * (0.30 + patch * 0.45) + core * (0.35 + knots * 0.85));
			stellarMass *= 1.0 - dustLane * 0.68;
			stellarMass *= 1.0 - centralVeil * 0.18;
			half diffuseGlow = saturate(halo * 0.24 + core * (0.18 + knots * 0.32)) * (1.0 - dustLane * 0.90) * (1.0 - centralVeil * 0.78);
			half extinction = saturate(centralVeil * 0.90 + dustLane * 0.18);
			return half3(stellarMass, diffuseGlow, extinction);
		}

		inline half3 BlendClouds(half3 color, samplerCUBE cube, float3 uv, half alpha, half3 skyColor) {
			half4 tex = texCUBE (cube, uv);
			tex.rgb *= skyColor;
			tex.a *= alpha;
			color = color * (1.0 - tex.a) + tex.rgb * tex.a;
			return color;
        }


		half MoonPhaseMask(float2 disc, half phase, half angle) {
			float s, c;
			sincos(angle, s, c);
			float2 r = float2(disc.x * c - disc.y * s, disc.x * s + disc.y * c);
			float terminator = -phase * sqrt(saturate(1.0 - r.y * r.y));
			return smoothstep(terminator - 0.06, terminator + 0.06, r.x);
		}

		half3 Spectrum(half t, half saturation) {
			// spectral ramp
			half hue = lerp(0.78, 0.0, saturate(t));
			half3 rgb = saturate(abs(frac(hue + half3(1.0, 0.66667, 0.33333)) * 6.0 - 3.0) - 1.0);
			rgb = rgb * rgb * (3.0 - 2.0 * rgb);
			return lerp(1.0.xxx, rgb, saturation);
		}

		inline half3 LinearColor(half3 color) {
			#if defined(UNITY_COLORSPACE_GAMMA)
				// convert gamma color properties to linear
				color *= color;
			#endif
			return color;
        }

		v2f vert (appdata v)
		{
			v2f o;
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_OUTPUT(v2f, o);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
			o.pos = UnityObjectToClipPos(v.vertex);
			o.ray = v.vertex.xyz;
			#if SKYBOX_LAYER_1
				o.uvLayer1 = RotateAroundYInDegrees(v.vertex.xyz, _Time.x * _RotationSpeed1 + _HShiftLayer1);
				o.uvLayer1.y += _SinTime.x * _PhaseLayer1 - _VShiftLayer1;
			#else
				o.uvLayer1 = 0;
			#endif
			#if SKYBOX_LAYER_2
				o.uvLayer2 = RotateAroundYInDegrees(v.vertex.xyz, _Time.x * _RotationSpeed2 + _HShiftLayer2);
				o.uvLayer2.y += _SinTime.x * _PhaseLayer2 - _VShiftLayer2;
			#else
				o.uvLayer2 = 0;
			#endif
			#if SKYBOX_LAYER_3
				o.uvLayer3 = RotateAroundYInDegrees(v.vertex.xyz, _Time.x * _RotationSpeed3 + _HShiftLayer3);
				o.uvLayer3.y += _SinTime.x * _PhaseLayer3 - _VShiftLayer3;
			#else
				o.uvLayer3 = 0;
			#endif
			#if SKYBOX_LAYER_4
				o.uvLayer4 = RotateAroundYInDegrees(v.vertex.xyz, _Time.x * _RotationSpeed4 + _HShiftLayer4);
				o.uvLayer4.y += _SinTime.x * _PhaseLayer4 - _VShiftLayer4;
			#else
				o.uvLayer4 = 0;
			#endif
			#if SKYBOX_HAZE
				float3x3 rotHaze = AngleAxis3x3(_Time.x * _HazeRotationSpeed, normalize(_HazeRotationAxis));
				o.hazeRay = normalize(mul(rotHaze, v.vertex.xyz));
			#else
				o.hazeRay = 0;
			#endif

			if (_SBPCustomPositions) {
				o.scrPos = mul(_LightWL, float4(v.vertex.xyz, 0));
				o.posAux = mul(_MoonLightWL, float4(v.vertex.xyz * _WorldSpaceMoonDir.w, 0));
			} else {
				o.scrPos = o.pos;
				o.posAux = UnityObjectToClipPos(_WorldSpaceLightPos0.xyz);
			}

			return o;
		}

		half4 frag (v2f i) : SV_Target
		{
			float3 ray    = i.ray;

			float3 sunDir = _SBPCustomPositions ? _WorldSpaceSunDir : _WorldSpaceLightPos0.xyz;

			float3 delta  = sunDir - ray;
			float dist    = dot(delta, delta);

			// base color
			half3 skyColor = LinearColor(_SkyTint);

			// fog
			float y = ray.y;
			half fog = saturate(exp(_FogPower * (-y / _FogHeight + _FogBaseHeight)) * _FogAmount + _FogMinAmount);
			fog *= _FogAlpha;

			y = abs(y);

			// sky tint
			float hy = abs(sunDir.y) + y;
			half t = saturate( (0.4 - hy) * 2.2) / (1.0 + dist * 0.8);
			skyColor.r = lerp(skyColor.r, 1.0, t);
			skyColor.b = lerp(skyColor.b, 0.0, t);

			// daylight + obscure opposite side of sky
			half daylight = saturate(1.0 + sunDir.y * 2.0 - dist * 0.03);
			skyColor = lerp(_SkyNightColor, skyColor, daylight);

			// sun
			half sunFlare = _SunFlare / pow(1.0 + dist, 64.0);

			float2 scrDist, scrDistMoon;
			UNITY_BRANCH
			if (_SBPCustomPositions) {
				scrDist = i.scrPos.xy * 1.8;
				scrDistMoon = i.posAux.xy * 1.8;
			} else {
				scrDist = i.scrPos.xy/i.scrPos.w - i.posAux.xy/i.posAux.w;
				scrDist.x *= _ScreenParams.x/_ScreenParams.y;
				#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(SINGLE_PASS_STEREO)
					scrDist.x *= 0.5;
				#endif
				scrDistMoon = scrDist;
			}

			half sunDist = length(scrDist);
            half sunIntensity = 1.0 - smoothstep(0.0, _SunSize, sunDist);
			sunIntensity *= 1.5;
			sunIntensity *= sunIntensity;
			half3 sunColor = LinearColor(_SunColor);
			if (_SBPCustomPositions) sunColor *= (i.scrPos.z <= 0);
			half3 sunFlareColor = sunColor * sunFlare;
			sunColor *= sunIntensity;
			half3 moonColor = 0.0.xxx;
			half galacticExtinction = 0.0;

			#if SKYBOX_NIGHT
				// moon
				float3 moonDir = _SBPCustomPositions ? _WorldSpaceMoonDir.xyz : -sunDir;
				delta = moonDir - ray;
				dist = dot(delta, delta);
				half  moonFlare = _MoonFlare / pow(1.0 + dist, 64.0);
				moonFlare *= saturate(_MoonPhase * 0.5 + 0.5);

				if (dist < 0.3) {
					moonColor = moonFlare.xxx;
					half3 moonTex = tex2Dlod(_MoonTex, float4(scrDistMoon.xy * _MoonSize + 0.5, 0, 0)).rgb;
					moonTex *= MoonPhaseMask(scrDistMoon.xy * _MoonSize * 2.0, _MoonPhase, radians(_MoonPhaseAngle));
					moonColor += moonTex;
					if (_SBPCustomPositions) {
						moonColor = max(0.0.xxx, moonColor - sunColor);
					} else {
						sunColor = 0;
					}
				}

				#if STAR_MAP
					float3 starDir = mul((float3x3)_StarMapRotation, ray);
					if (dot(starDir, starDir) < 1e-6) starDir = ray;
					half3 starColor = texCUBE(_StarMap, starDir).rgb;
					#if STAR_TWINKLE
						// Only the point stars twinkle: a blurrier mip holds the Milky Way's glow, which stays steady.
						half3 steady = texCUBEbias(_StarMap, float4(starDir, 3.0)).rgb;
						starColor -= max(starColor - steady, 0.0) * StarTwinkleDim(starDir, normalize(ray).y);
					#endif
					starColor *= _StarMapIntensity;
					starColor *= saturate(1.0 - fog) * saturate(1.0 - moonFlare * 25.0 - daylight * daylight * 64.0);
				#else
				// stars
				float starDensity = _StarDensity;
				half starBoost = 1.0;
				half galacticGlow = 0.0;
				UNITY_BRANCH
				if (_StarClustering > 0) {
					float3 sRay = normalize(ray);
					float3 bandAxis = SafeNormalize(_StarBandAxis, float3(0.35, 0.85, 0.4));
					half3 galaxy = GalacticProfile(sRay, bandAxis, _StarBandWidth);
					starDensity = _StarDensity * lerp(1.0, 0.22 + galaxy.x * 2.5, _StarClustering);
					starBoost = lerp(1.0, 0.65 + galaxy.x * 1.25, _StarClustering);
					galacticGlow = galaxy.y * _StarClustering;
					galacticExtinction = galaxy.z * _StarClustering;
				}
				float3 p = ray * _StarSize;
				float br = smoothstep(1.0 - starDensity, 1.0, hash13(floor(p)));
				float star = smoothstep(_StarBrightness, 0., length(frac(p) - 0.5)) * br;
				star *= saturate(1.0 - saturate(frac(br * 10000.0 + _Time.w) - 0.3) * _StarBlinking);
				star = saturate(star * (1.0 - fog) - moonFlare * 25.0 - daylight * daylight * 64.0);
				half3 starColor = star * LinearColor(lerp(_StarDarkColor, _StarBrightColor, br));
				starColor *= starColor;
				starColor *= starBoost;
				half galaxyVisibility = saturate(1.0 - fog - moonFlare * 25.0 - daylight * daylight * 64.0);
				starColor += LinearColor(lerp(_StarDarkColor, _StarBrightColor, 0.55)) * galacticGlow * galaxyVisibility * 0.18;
				#endif
			#else
				half3 starColor = 0.0.xxx;
			#endif

			half deepSkyMask = saturate(1.0 - fog - daylight * daylight * 64.0);
			#if SKYBOX_NIGHT
				deepSkyMask = saturate(deepSkyMask - moonFlare * 25.0);
			#endif
			// dust extinction
			skyColor *= 1.0 - galacticExtinction * deepSkyMask * 0.55;

			half3 moon2Color = 0.0.xxx;
			half moon2Occlusion = 0.0;
			UNITY_BRANCH
			if (_Moon2Size > 0) {
				float3 m2Dir = SafeNormalize(_Moon2Direction, float3(0.6, 0.5, -0.6));
				float3 m2Ray = normalize(ray);
				UNITY_BRANCH
				if (dot(m2Ray, m2Dir) > 0.5) {
					float3 m2Up = abs(m2Dir.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
					float3 m2X = SafeNormalize(cross(m2Up, m2Dir), float3(1, 0, 0));
					float3 m2Y = cross(m2Dir, m2X);
					float2 m2Disc = float2(dot(m2Ray, m2X), dot(m2Ray, m2Y)) / max(_Moon2Size, 1e-4);
					half m2Sq = dot(m2Disc, m2Disc);
					UNITY_BRANCH
					if (m2Sq < 1.0) {
						half3 m2Tex = tex2Dlod(_Moon2Tex, float4(m2Disc * 0.5 + 0.5, 0, 0)).rgb;
						half m2Mask = MoonPhaseMask(m2Disc, _Moon2Phase, radians(_Moon2PhaseAngle));
						moon2Occlusion = smoothstep(1.0, 0.9, m2Sq);
						moon2Color = m2Tex * m2Mask * moon2Occlusion * LinearColor(_Moon2Tint) * _Moon2Brightness;
					}
				}
			}

			half3 rainbowTint = 0.0.xxx;
			half rainbowAlpha = 0.0;
			UNITY_BRANCH
			if (_RainbowIntensity > 0) {
				float3 rRay = normalize(ray);
				half rAngle = degrees(acos(clamp(dot(rRay, -sunDir), -1.0, 1.0)));
				half rT = saturate((rAngle - _RainbowRadius + _RainbowWidth) / (2.0 * _RainbowWidth));
				half rBand = smoothstep(0.0, 0.08, rT) * smoothstep(1.0, 0.90, rT);
				rainbowTint = LinearColor(Spectrum(rT, _RainbowSaturation)) * rBand;
				rainbowAlpha = rBand;
				UNITY_BRANCH
				if (_RainbowSecondary > 0) {
					half rWide = _RainbowWidth * 1.6;
					half rT2 = saturate((rAngle - _RainbowRadius * 1.22 + rWide) / (2.0 * rWide));
					half rBand2 = smoothstep(0.0, 0.18, rT2) * smoothstep(1.0, 0.82, rT2) * _RainbowSecondary * 0.5;
					rainbowTint += LinearColor(Spectrum(1.0 - rT2, _RainbowSaturation)) * rBand2;
					rainbowAlpha += rBand2;
				}
				half rVisible = _RainbowIntensity * 0.72 * saturate(sunDir.y * 6.0) * smoothstep(0.0, 0.06, rRay.y) * (1.0 - fog);
				rainbowTint *= rVisible;
				rainbowAlpha = saturate(rainbowAlpha * rVisible);
			}

			half3 meteorColor = 0.0.xxx;
			UNITY_BRANCH
			if (_MeteorFrequency > 0) {
				float3 mRay = normalize(ray);
				float3 mRadiant = SafeNormalize(_MeteorRadiant, float3(-0.35, 0.8, 0.45));
				float mCycleLen = _MeteorDuration * (1.0 + (1.0 - _MeteorFrequency) * 12.0);
				half mAcc = 0;
				UNITY_UNROLL
				for (int mk = 0; mk < 4; mk++) {
					UNITY_BRANCH
					if (mk >= (int)round(_MeteorCount)) continue;
					float mt = (_Time.y + mk * mCycleLen * 0.377) / mCycleLen;
					float mCyc = floor(mt);
					float mPh = frac(mt) * mCycleLen / _MeteorDuration;
					UNITY_BRANCH
					if (mPh < 1.0) {
						float3 mSeed = float3(mCyc, mk * 17.0, mCyc * 0.73 + mk);
						float mAng = hash13(mSeed) * 6.2832;
						float mElev = hash13(mSeed + 1.71) * 0.82 + 0.15;
						float mHoriz = sqrt(saturate(1.0 - mElev * mElev));
						float3 mStart = float3(cos(mAng) * mHoriz, mElev, sin(mAng) * mHoriz);
						float mAng2 = hash13(mSeed + 3.13) * 6.2832;
						float3 mRandom = SafeNormalize(cross(mStart, float3(cos(mAng2), hash13(mSeed + 4.27) * 2.0 - 1.0, sin(mAng2))), float3(0,-1,0));
						float3 mCoherent = SafeNormalize(mStart * dot(mStart, mRadiant) - mRadiant, mRandom);
						float3 mDir = SafeNormalize(lerp(mCoherent, mRandom, _MeteorSpread), mCoherent);
						float3 mHead = normalize(mStart + mDir * mPh * 0.6);
						float3 mTail = normalize(mStart + mDir * max(mPh - _MeteorTrail * 0.3, 0.0) * 0.6);
						float3 mSeg = mHead - mTail;
						float mS = saturate(dot(mRay - mTail, mSeg) / (dot(mSeg, mSeg) + 1e-5));
						float mDist = length(mRay - normalize(mTail + mSeg * mS));
						half mW = 0.002 * _MeteorSize;
						half mStreak = smoothstep(mW, mW * 0.2, mDist);
						mStreak *= lerp(0.1, 1.0, mS * mS);
						mStreak *= saturate(mPh * 6.0) * saturate((1.0 - mPh) * 2.5);
						mAcc += mStreak * (0.6 + 0.4 * hash13(mSeed + 6.51));
					}
				}
				meteorColor = mAcc * saturate(_MeteorFrequency * 20.0) * LinearColor(_MeteorColor) * _MeteorBrightness * deepSkyMask;
			}

			half3 auroraColor = 0.0.xxx;
			UNITY_BRANCH
			if (_AuroraIntensity > 0) {
				float3 aRay = normalize(ray);
				float aEl = aRay.y;
				UNITY_BRANCH
				if (aEl > 0.0) {
					float aAz = atan2(aRay.z, aRay.x);
					float aT = _Time.y * _AuroraSpeed;
					float3 aHDir = SafeNormalize(float3(aRay.x, 0.0, aRay.z), float3(1, 0, 0));
					half zenithFade = 1.0 - smoothstep(0.88, 0.99, aEl);
					UNITY_UNROLL
					for (int ak = 0; ak < 3; ak++) {
						half envN = TriplanarNoise(aHDir * (0.36 + ak * 0.09) + float3(ak * 5.37, aT * 0.014, 2.11 - ak * 3.9), aHDir);
						half envelope = lerp(0.1, 1.0, smoothstep(0.34, 0.78, envN));
						UNITY_BRANCH
						if (envelope > 0.0008) {
							float waveAmp = _AuroraWaviness * (1.0 - ak * 0.25);
							float center = _AuroraElevation + (ak - 1.0) * 0.14 + waveAmp * (0.2 * sin(aAz * (1.0 + ak) + aT * 0.31 + ak * 2.1) + 0.07 * sin(aAz * (3.0 + ak) - aT * 0.24 + ak * 4.3));
							float d = aEl - center;
							half falloff = exp(-max(d, 0.0) * (2.4 / (_AuroraSpan * (1.0 - ak * 0.22))));
							half soft = smoothstep(-0.3, 0.02, d);
							UNITY_BRANCH
							if (soft * envelope * zenithFade > 0.001) {
								half r1 = TriplanarNoise(float3(aRay.x * _AuroraRayDensity + aT * 0.05, d * 0.18 + ak * 0.61, aRay.z * _AuroraRayDensity - aT * 0.033), aRay);
								half r2 = TriplanarNoise(float3(aRay.x * _AuroraRayDensity * 2.7 - aT * 0.041, d * 0.22 + ak * 1.13, aRay.z * _AuroraRayDensity * 2.7 + aT * 0.027), aRay);
								half rays = lerp(0.3, 1.0, smoothstep(0.3, 0.72, r1 * 0.65 + r2 * 0.35));
								float edgeD = d + (r1 - 0.5) * 0.09;
								half lowEdge = smoothstep(-0.11, 0.04, edgeD);
								half profile = lowEdge * falloff * envelope * zenithFade;
								half baseGlow = exp(-abs(edgeD) * 12.0) * 0.45 * envelope * zenithFade * soft;
								half patchN = TriplanarNoise(aHDir * 1.4 + float3(aT * 0.02, ak * 7.7, -aT * 0.017), aHDir);
								half patches = lerp(0.3, 1.0, patchN);
								half curtain = (profile * 0.85 + baseGlow) * rays * patches;
								half3 aTint = lerp(LinearColor(_AuroraColor1), LinearColor(_AuroraColor2), saturate(saturate(d / _AuroraSpan) + (patchN - 0.5) * 0.35));
								auroraColor += aTint * curtain;
							}
						}
					}
					auroraColor *= _AuroraIntensity * deepSkyMask * smoothstep(0.0, 0.08, aEl);
					half aLum = max(auroraColor.r, max(auroraColor.g, auroraColor.b));
					auroraColor = max(0.0.xxx, auroraColor + (hash13(aRay * 419.3) - 0.5) * 0.008 * saturate(aLum * 60.0));
				}
			}

			// final color
			half3 col = skyColor + sunColor + sunFlareColor * 0.5 + (moonColor + starColor * (1.0 - moon2Occlusion) + meteorColor + auroraColor + moon2Color) * saturate(ray.y + 0.2);

			// rainbow
			col = col * (1.0 - rainbowAlpha) + rainbowTint;

			// sky tint
			half3 skyTint = half3(1,1,1);
			skyTint.r = lerp(skyTint.r, 1.0, t);
			skyTint.b = lerp(skyTint.b, 0.0, t);
			skyTint = lerp(_NightTintColor, skyTint, daylight);
			
			// blend haze
			#if SKYBOX_HAZE
				float3 hazeRay = i.hazeRay;
				float3 hazeUV = hazeRay * _HazeScale;
				half n1 = tex2D(_NoiseTex, hazeUV.zy).r;
				half n2 = tex2D(_NoiseTex, hazeUV.xz).r;
				half n3 = tex2D(_NoiseTex, hazeUV.xy).r;
				float3 triW = abs(hazeRay);
				float3 weights = triW / (triW.x + triW.y + triW.z);
				half haze = dot(half3(n1, n2, n3), weights);
				haze = saturate( haze * _HazeIntensity - _HazeThreshold );
				half3 hazeColor = LinearColor(_HazeColor.rgb);
				haze *= _HazeColor.a;
				col = col * (1.0 - haze) + haze * (hazeColor * skyTint);
			#endif

			// blend clouds
			#if SKYBOX_LAYER_1
				col = BlendClouds(col, _Tex1, i.uvLayer1, _AlphaLayer1, skyTint);
			#endif
			#if SKYBOX_LAYER_2
				col = BlendClouds(col, _Tex2, i.uvLayer2, _AlphaLayer2, skyTint);
			#endif
			#if SKYBOX_LAYER_3
				col = BlendClouds(col, _Tex3, i.uvLayer3, _AlphaLayer3, skyTint);
			#endif
			#if SKYBOX_LAYER_4
				col = BlendClouds(col, _Tex4, i.uvLayer4, _AlphaLayer4, skyTint);
			#endif

			// apply fog
			col += sunFlareColor * 0.5;

			half3 fogColor = LinearColor(_FogColor);
			half3 fogSkyTint = lerp(1.0, skyTint, _FogDaylightInfluence);
			col = lerp(col, fogColor * fogSkyTint, fog);

			// exposure
			col *= _Exposure;

			// gamma
			#if defined(UNITY_COLORSPACE_GAMMA)
				col = sqrt(col);
			#endif

			return half4(col, 1.0);
		}
		ENDCG
	}
}

Fallback Off
}
