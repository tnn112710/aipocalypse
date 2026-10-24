// The painted panorama of Skybox/Panoramic (same texture, rotation, tint × exposure) plus the time of day: a night gradient,
// a horizon glow toward the sun, a sun disc, stars and a moon with phases. EnvironmentDirector sets the _TOD_ globals each
// frame, so the material asset is never written; with no director the material's own tint and exposure are used.
Shader "Aipocalypse/Skybox/Time Of Day"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Painted Sky (lat-long HDR)", 2D) = "grey" {}
        _Rotation ("Rotation", Range(0, 360)) = 0
        _Tint ("Fallback Tint", Color) = (0.5, 0.5, 0.5, 1)
        [Gamma] _Exposure ("Fallback Exposure", Range(0, 8)) = 1
        _SunSize ("Sun Size (degrees)", Range(0.2, 6)) = 1.6
        _MoonSize ("Moon Size (degrees)", Range(0.5, 12)) = 4
        _StarDensity ("Star Density", Range(40, 400)) = 160
        _StarSize ("Star Size", Range(0.05, 0.45)) = 0.2
        _StarCoverage ("Star Coverage", Range(0, 0.5)) = 0.16
        _HorizonGlowHeight ("Horizon Glow Falloff", Range(0.5, 20)) = 5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_HDR;
                float _Rotation;
                float4 _Tint;
                float _Exposure;
                float _SunSize;
                float _MoonSize;
                float _StarDensity;
                float _StarSize;
                float _StarCoverage;
                float _HorizonGlowHeight;
            CBUFFER_END

            float4 _TOD_SkyTint;
            float _TOD_SkyExposure;
            float4 _TOD_NightTop;
            float4 _TOD_NightHorizon;
            float _TOD_NightBlend;
            float4 _TOD_HorizonGlow;
            float3 _TOD_SunDir;
            float4 _TOD_SunColor;
            float _TOD_SunDisc;
            float3 _TOD_MoonDir;
            float4 _TOD_MoonColor;
            float _TOD_MoonPhase;
            float _TOD_MoonGlow;
            float _TOD_Stars;

            // Matches Skybox/Panoramic, so the painted sky sits exactly where it did.
            static const float ColorSpaceDouble = 4.59479380;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 texDir : TEXCOORD0;
                float3 worldDir : TEXCOORD1;
            };

            float3 RotateAroundY(float3 v, float degrees)
            {
                float a = radians(degrees);
                float s = sin(a);
                float c = cos(a);
                return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z);
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float2 RadialCoords(float3 d)
            {
                float latitude = acos(clamp(d.y, -1.0, 1.0));
                float longitude = atan2(d.z, d.x);
                return float2(0.5, 1.0) - float2(longitude * (0.5 / PI), latitude / PI);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 rotated = RotateAroundY(input.positionOS.xyz, _Rotation);
                output.positionCS = TransformObjectToHClip(rotated);
                output.texDir = input.positionOS.xyz;
                output.worldDir = rotated;
                return output;
            }

            float Stars(float3 d)
            {
                float3 p = d * _StarDensity;
                float3 cell = floor(p);
                float pick = Hash13(cell);
                if (pick > _StarCoverage)
                    return 0.0;
                float3 offset = (Hash33(cell) - 0.5) * 0.6;
                float distance = length(frac(p) - 0.5 - offset);
                float brightness = 0.35 + 0.65 * (1.0 - pick / max(_StarCoverage, 1e-4));
                float twinkle = 0.75 + 0.25 * sin(_Time.y * (1.5 + pick * 40.0) + pick * 300.0);
                return smoothstep(_StarSize, 0.0, distance) * brightness * twinkle;
            }

            // Lit disc of the moon; alpha covers what is behind it. Phase 0 = new (lit from behind), 0.5 = full.
            float4 Moon(float3 d, float3 moon)
            {
                float radius = sin(radians(_MoonSize * 0.5));
                if (dot(d, moon) < cos(radians(_MoonSize)))
                    return 0.0;
                float3 helper = abs(moon.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(helper, moon));
                float3 up = cross(moon, right);
                float2 xy = float2(dot(d, right), dot(d, up)) / radius;
                float r2 = dot(xy, xy);
                if (r2 >= 1.0)
                    return 0.0;

                float3 normal = float3(xy, sqrt(1.0 - r2));
                float angle = _TOD_MoonPhase * 2.0 * PI;
                float3 light = float3(sin(angle), 0.0, -cos(angle));
                float lit = smoothstep(-0.05, 0.2, dot(normal, light));
                float maria = 0.82 + 0.18 * Hash13(floor(float3(xy * 5.0, 1.0)));
                float3 color = _TOD_MoonColor.rgb * (lit * maria * 2.2 + 0.02);
                float alpha = smoothstep(1.0, 0.92, r2);
                return float4(color, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 texDir = normalize(input.texDir);
                float3 d = normalize(input.worldDir);

                bool driven = _TOD_SkyTint.a > 0.0;
                float3 tint = driven ? _TOD_SkyTint.rgb : _Tint.rgb;
                float exposure = driven ? _TOD_SkyExposure : _Exposure;

                float4 encoded = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, RadialCoords(texDir), 0);
                float hdrAlpha = _MainTex_HDR.w * (encoded.a - 1.0) + 1.0;
                float3 painted = encoded.rgb * (_MainTex_HDR.x * pow(abs(hdrAlpha), _MainTex_HDR.y));
                float3 color = painted * tint * ColorSpaceDouble * exposure;

                if (!driven)
                    return half4(color, 1.0);

                float height = saturate(d.y);
                float3 night = lerp(_TOD_NightHorizon.rgb, _TOD_NightTop.rgb, sqrt(height));
                color = lerp(color, night, _TOD_NightBlend);

                float3 sun = normalize(_TOD_SunDir);
                float2 sunFlat = normalize(sun.xz + 1e-5);
                float2 dirFlat = normalize(d.xz + 1e-5);
                float facing = saturate(dot(sunFlat, dirFlat) * 0.5 + 0.5);
                color += _TOD_HorizonGlow.rgb * (facing * facing * facing) * exp(-abs(d.y) * _HorizonGlowHeight);

                float aboveHorizon = saturate(d.y * 12.0 + 0.2);
                float3 moonDir = normalize(_TOD_MoonDir);
                float4 moon = Moon(d, moonDir);
                moon.a *= aboveHorizon;

                color += Stars(d) * _TOD_Stars * saturate(d.y * 5.0) * (1.0 - moon.a);

                float sunCos = dot(d, sun);
                float sunEdge = cos(radians(_SunSize * 0.5));
                float disc = smoothstep(sunEdge - 0.00004, sunEdge + 0.00002, sunCos);
                float halo = pow(saturate(sunCos), 900.0) * 0.6 + pow(saturate(sunCos), 60.0) * 0.12;
                color += _TOD_SunColor.rgb * (disc * 6.0 + halo) * _TOD_SunDisc * aboveHorizon;

                float fullness = 1.0 - abs(_TOD_MoonPhase - 0.5) * 2.0;
                float moonHalo = pow(saturate(dot(d, moonDir)), 500.0) * 0.5 + pow(saturate(dot(d, moonDir)), 30.0) * 0.06;
                color += _TOD_MoonColor.rgb * moonHalo * _TOD_MoonGlow * fullness * aboveHorizon;
                color = lerp(color, moon.rgb, moon.a);

                // Dark gradients band in 8 bits; a little noise hides it.
                color += (Hash13(d * 1731.0) - 0.5) * (1.0 / 255.0);
                return half4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
