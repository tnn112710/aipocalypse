Shader "Custom/FoliageWind"
{
    // =========================================================================
    // Properties
    // =========================================================================
    Properties
    {
        [Header(Albedo)][Space(5)]
        _MainTex        ("Albedo (RGB) Alpha (A)", 2D)    = "white" {}
        _TintColor      ("Tint Color",              Color) = (1,1,1,1)
        _AlphaCutoff    ("Alpha Cutoff",   Range(0,1))    = 0.33

        [Header(Surface)][Space(5)]
        _NormalMap      ("Normal Map",              2D)    = "bump"  {}
        _NormalStrength ("Normal Strength", Range(0,2))   = 1.0
        _SmoothnessMap  ("Smoothness Map",          2D)    = "white" {}
        _SmoothnessScale("Smoothness Scale", Range(0,1))  = 0.1

        [Header(Wind)][Space(5)]
        [Toggle(_WIND_ON)] _WindEnable("Enable Wind",     Float) = 1
        _WindStrength   ("Strength",     Range(0,5))      = 0.25
        _WindFrequency  ("Frequency",    Range(0,4))      = 1.0
        _WindTurbulence ("Turbulence",   Range(0,5))      = 0.4
        _WindDirX       ("Direction X",  Range(-1,1))     = 0.7
        _WindDirZ       ("Direction Z",  Range(-1,1))     = 0.3
        [Toggle(_ROOTPIN_ON)] _RootPin("Pin Base (leaves/tall grass)", Float) = 1

        [HideInInspector] _QueueOffset   ("_QueueOffset",  Float) = 0
        [HideInInspector] _QueueControl  ("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps   ("unity_Lightmaps",    2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks",  2DArray) = "" {}
    }

    // =========================================================================
    // SubShader
    // =========================================================================
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "UniversalMaterialType"="Lit"
        }

        Cull Off
        ZWrite On
        ZTest LEqual
        AlphaToMask Off

        // -----------------------------------------------------------------
        // Shared HLSL: hash-based value noise and wind functions
        // These are written from scratch using integer hashing — a completely
        // different algorithm and implementation from simplex noise.
        // -----------------------------------------------------------------
        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

        // ------------------------------------------------------------------
        // Hash noise — based on classic integer hash chain.
        // Reference: "Hash without Sine" concept (domain: public knowledge).
        // Implementation below is independently written.
        // ------------------------------------------------------------------
        uint FoliageHash(uint x)
        {
            x ^= x >> 16u;
            x *= 0x45d9f3bu;
            x ^= x >> 16u;
            return x;
        }

        uint FoliageHash3(uint3 p)
        {
            return FoliageHash(p.x ^ FoliageHash(p.y ^ FoliageHash(p.z)));
        }

        // Smooth 3D value noise in [-1, 1].
        // Grid-based interpolation, quintic smoothstep curve.
        float ValueNoise3D(float3 p)
        {
            float3 i  = floor(p);
            float3 f  = frac(p);
            // Quintic fade curve: 6t^5 - 15t^4 + 10t^3
            float3 u  = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

            // Eight corner hashes
            uint3  ip = (uint3)(i);
            float n000 = (float)FoliageHash3(ip + uint3(0,0,0)) / 4294967295.0;
            float n100 = (float)FoliageHash3(ip + uint3(1,0,0)) / 4294967295.0;
            float n010 = (float)FoliageHash3(ip + uint3(0,1,0)) / 4294967295.0;
            float n110 = (float)FoliageHash3(ip + uint3(1,1,0)) / 4294967295.0;
            float n001 = (float)FoliageHash3(ip + uint3(0,0,1)) / 4294967295.0;
            float n101 = (float)FoliageHash3(ip + uint3(1,0,1)) / 4294967295.0;
            float n011 = (float)FoliageHash3(ip + uint3(0,1,1)) / 4294967295.0;
            float n111 = (float)FoliageHash3(ip + uint3(1,1,1)) / 4294967295.0;

            // Trilinear interpolation
            float nx00 = lerp(n000, n100, u.x);
            float nx10 = lerp(n010, n110, u.x);
            float nx01 = lerp(n001, n101, u.x);
            float nx11 = lerp(n011, n111, u.x);
            float nxy0 = lerp(nx00, nx10, u.y);
            float nxy1 = lerp(nx01, nx11, u.y);
            return lerp(nxy0, nxy1, u.z) * 2.0 - 1.0; // remap to [-1,1]
        }

        // Fractional Brownian Motion — two octaves for turbulence.
        float FoliageFBM(float3 p)
        {
            float v  = ValueNoise3D(p)          * 0.65;
                  v += ValueNoise3D(p * 2.1 + 3.7) * 0.35;
            return v;
        }

        // ------------------------------------------------------------------
        // Wind displacement.
        // Uses layered sine waves for primary sway and FBM for turbulence.
        // Primary wave: driven by world XZ position + time, giving each
        // plant a unique phase so they don't all move in sync.
        // ------------------------------------------------------------------

        // Shared constants so all passes compile the same way.
        CBUFFER_START(_FoliageWindGlobals)
        // (empty — we pull from per-material CBUFFER below)
        CBUFFER_END

        float2 ComputeWindXZ(
            float3  posWS,
            float2  uv,
            float   vertMask,
            float   windStrength,
            float   windFrequency,
            float   windTurbulence,
            float   windDirX,
            float   windDirZ,
            bool    rootPin,
            float   t)          // pass _Time.y from the vertex shader
        {
            // Primary sway along each world axis independently.
            // windDirX / windDirZ are used as direct per-axis weights,
            // NOT normalized, so X and Z truly control independent axes.
            float phaseX = posWS.x * 0.5;
            float phaseZ = posWS.z * 0.5;
            float swayX  = sin(phaseX + t * windFrequency * 3.14159)         * 0.6
                         + sin(phaseX * 1.7 + t * windFrequency * 2.0 + 1.3) * 0.4;
            float swayZ  = sin(phaseZ + t * windFrequency * 3.14159 + 0.9)   * 0.6
                         + sin(phaseZ * 1.7 + t * windFrequency * 2.0 + 2.4) * 0.4;

            // Turbulence: two independent FBM samples so it visibly
            // shakes foliage in both X and Z simultaneously.
            float turbX = FoliageFBM(posWS * 0.4 + float3(t * 0.41, 0.0, t * 0.17));
            float turbZ = FoliageFBM(posWS * 0.4 + float3(t * 0.23, 0.0, t * 0.37) + 5.3);

            float dispX = swayX * windDirX + turbX * windTurbulence;
            float dispZ = swayZ * windDirZ + turbZ * windTurbulence;

            // Height mask: leaves/tall grass use UV.y^2 so the base stays still
            float heightMask = rootPin ? (uv.y * uv.y) : 1.0;
            float mask       = heightMask * vertMask * windStrength * 0.12;

            return float2(dispX, dispZ) * mask;
        }

        ENDHLSL

        // =================================================================
        // PASS 0 — Forward (main shading)
        // =================================================================
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            ColorMask RGBA

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON

            // URP multi_compiles
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS

            // Feature keywords
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON

            #pragma vertex   FoliageVert
            #pragma fragment FoliageFrag

            #define SHADERPASS SHADERPASS_FORWARD

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            // ------------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float  _AlphaCutoff;
                float  _NormalStrength;
                float  _SmoothnessScale;
                float  _WindStrength;
                float  _WindFrequency;
                float  _WindTurbulence;
                float  _WindDirX;
                float  _WindDirZ;
            CBUFFER_END

            sampler2D _MainTex;
            sampler2D _NormalMap;
            sampler2D _SmoothnessMap;

            // ------------------------------------------------------------------
            struct VertIn
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS: TANGENT;
                float4 uv0      : TEXCOORD0;
                float4 vcol     : COLOR;        // R = wind freedom mask
                #if defined(LIGHTMAP_ON)
                    float2 lmUV : TEXCOORD1;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    float2 dlmUV: TEXCOORD2;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertOut
            {
                float4 posCS            : SV_POSITION;
                float3 posWS            : TEXCOORD0;
                float3 normalWS         : TEXCOORD1;
                float4 tangentWS        : TEXCOORD2;
                float4 shOrLmUV         : TEXCOORD3;
                half4  fogAndVLight     : TEXCOORD4;
                #if defined(DYNAMICLIGHTMAP_ON)
                    float2 dlmUV        : TEXCOORD5;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                    float4 probeOcc     : TEXCOORD6;
                #endif
                float2 uv0              : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ------------------------------------------------------------------
            VertOut FoliageVert(VertIn v)
            {
                VertOut o = (VertOut)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Wind displacement in world space, converted back to object space
                float3 posWS_pre = TransformObjectToWorld(v.posOS.xyz);

                #ifdef _WIND_ON
                    float2 dXZ = ComputeWindXZ(
                        posWS_pre,
                        v.uv0.xy,
                        v.vcol.r,
                        _WindStrength,
                        _WindFrequency,
                        _WindTurbulence,
                        _WindDirX,
                        _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    float3 windWS = float3(dXZ.x, 0.0, dXZ.y);
                    // Convert displacement to object space so we can add to posOS
                    float3 windOS = mul((float3x3)GetWorldToObjectMatrix(), windWS);
                    v.posOS.xyz  += windOS;
                #endif

                VertexPositionInputs vpi = GetVertexPositionInputs(v.posOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.posCS   = vpi.positionCS;
                o.posWS   = vpi.positionWS;
                o.normalWS  = vni.normalWS;
                o.tangentWS = float4(vni.tangentWS,
                    (v.tangentOS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale());
                o.uv0 = v.uv0.xy;

                OUTPUT_LIGHTMAP_UV(v.lmUV, unity_LightmapST, o.shOrLmUV.xy);
                OUTPUT_SH4(vpi.positionWS, vni.normalWS,
                    GetWorldSpaceNormalizeViewDir(vpi.positionWS),
                    o.shOrLmUV.xyz, o.probeOcc);

                o.fogAndVLight = 0;
                #if !defined(_FOG_FRAGMENT)
                    o.fogAndVLight.x = ComputeFogFactor(vpi.positionCS.z);
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    o.fogAndVLight.yzw = VertexLighting(vpi.positionWS, vni.normalWS);
                #endif

                return o;
            }

            // ------------------------------------------------------------------
            half4 FoliageFrag(VertOut i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.posCS);
                #endif

                // ---- UV sampling ----------------------------------------
                float2 uv = i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;

                float4 albedoSample = tex2D(_MainTex, uv);
                float  alpha        = albedoSample.a;
                AlphaDiscard(alpha, _AlphaCutoff);

                float3 albedo = albedoSample.rgb * _TintColor.rgb;

                // ---- Normal map -----------------------------------------
                float2 uvN      = i.uv0 * _NormalMap_ST.xy + _NormalMap_ST.zw;
                float3 normalTS = UnpackNormalScale(tex2D(_NormalMap, uvN), _NormalStrength);

                float  renorm       = 1.0 / max(FLT_MIN, length(i.normalWS));
                float3 normalWS_v   = i.normalWS  * renorm;
                float3 tangentWS_v  = i.tangentWS.xyz * renorm;
                float3 bitangentWS  = cross(i.normalWS, i.tangentWS.xyz)
                                      * i.tangentWS.w * renorm;
                float3 normalWS     = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS,
                        half3x3(tangentWS_v, bitangentWS, normalWS_v)));

                // ---- Smoothness -----------------------------------------
                float2 uvS      = i.uv0 * _SmoothnessMap_ST.xy + _SmoothnessMap_ST.zw;
                float  smooth   = saturate(tex2D(_SmoothnessMap, uvS).r * _SmoothnessScale);

                // ---- PBR surface data -----------------------------------
                float4 shadowCoord2 = TransformWorldToShadowCoord(i.posWS);

                InputData inputData     = (InputData)0;
                inputData.positionWS    = i.posWS;
                inputData.positionCS    = i.posCS;
                inputData.normalWS      = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.posWS);
                inputData.shadowCoord   = shadowCoord2;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.posCS);
                inputData.fogCoord      = InitializeInputDataFog(
                    float4(i.posWS, 1.0), i.fogAndVLight.x);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    inputData.vertexLighting = i.fogAndVLight.yzw;
                #endif

                float3 SH = i.shOrLmUV.xyz;
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI    = SAMPLE_GI(i.shOrLmUV.xy, i.dlmUV, SH, normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(i.shOrLmUV.xy);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI = SAMPLE_GI(SH,
                        GetAbsolutePositionWS(i.posWS),
                        normalWS,
                        inputData.viewDirectionWS,
                        i.posCS.xy,
                        i.probeOcc,
                        inputData.shadowMask);
                #else
                    inputData.bakedGI    = SAMPLE_GI(i.shOrLmUV.xy, SH, normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(i.shOrLmUV.xy);
                #endif

                SurfaceData surf          = (SurfaceData)0;
                surf.albedo               = albedo;
                surf.alpha                = saturate(alpha);
                surf.metallic             = 0.0;
                surf.specular             = 0.04;
                surf.smoothness           = smooth;
                surf.occlusion            = 1.0;
                surf.emission             = 0;
                surf.normalTS             = normalTS;
                surf.clearCoatMask        = 0;
                surf.clearCoatSmoothness  = 1;

                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(i.posCS, surf, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surf);
                color.rgb   = MixFog(color.rgb, inputData.fogCoord);

                return half4(color.rgb, OutputAlpha(color.a, false));
            }

            ENDHLSL
        }

        // =================================================================
        // PASS 1 — ShadowCaster
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask Off

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON

            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #define SHADERPASS SHADERPASS_SHADOWCASTER

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float  _AlphaCutoff;
                float  _NormalStrength;
                float  _SmoothnessScale;
                float  _WindStrength;
                float  _WindFrequency;
                float  _WindTurbulence;
                float  _WindDirX;
                float  _WindDirZ;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;
            sampler2D _MainTex;

            struct VertInS
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float2 uv0      : TEXCOORD0;
                float4 vcol     : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertOutS
            {
                float4 posCS : SV_POSITION;
                float2 uv0   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VertOutS ShadowVert(VertInS v)
            {
                VertOutS o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS_pre = TransformObjectToWorld(v.posOS.xyz);

                #ifdef _WIND_ON
                    float2 dXZ = ComputeWindXZ(
                        posWS_pre, v.uv0, v.vcol.r,
                        _WindStrength, _WindFrequency, _WindTurbulence,
                        _WindDirX, _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    float3 windOS = mul((float3x3)GetWorldToObjectMatrix(),
                                       float3(dXZ.x, 0.0, dXZ.y));
                    v.posOS.xyz += windOS;
                #endif

                float3 posWS   = TransformObjectToWorld(v.posOS.xyz);
                float3 normalWS = TransformObjectToWorldDir(v.normalOS);

                #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, normalWS, lightDir));
                o.posCS = ApplyShadowClamping(posCS);
                o.uv0   = v.uv0;
                return o;
            }

            half4 ShadowFrag(VertOutS i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 uv   = i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
                float  a    = tex2D(_MainTex, uv).a;
                AlphaDiscard(a, _AlphaCutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.posCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 2 — DepthOnly
        // =================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0
            AlphaToMask Off

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON

            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #define SHADERPASS SHADERPASS_DEPTHONLY

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float  _AlphaCutoff;
                float  _NormalStrength;
                float  _SmoothnessScale;
                float  _WindStrength;
                float  _WindFrequency;
                float  _WindTurbulence;
                float  _WindDirX;
                float  _WindDirZ;
            CBUFFER_END

            sampler2D _MainTex;

            struct VertInD { float4 posOS:POSITION; float2 uv0:TEXCOORD0; float4 vcol:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VertOutD { float4 posCS:SV_POSITION; float2 uv0:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            VertOutD DepthVert(VertInD v)
            {
                VertOutD o = (VertOutD)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #ifdef _WIND_ON
                    float3 posWS_pre = TransformObjectToWorld(v.posOS.xyz);
                    float2 dXZ = ComputeWindXZ(
                        posWS_pre, v.uv0, v.vcol.r,
                        _WindStrength, _WindFrequency, _WindTurbulence,
                        _WindDirX, _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    v.posOS.xyz += mul((float3x3)GetWorldToObjectMatrix(), float3(dXZ.x, 0.0, dXZ.y));
                #endif

                VertexPositionInputs vpi = GetVertexPositionInputs(v.posOS.xyz);
                o.posCS = vpi.positionCS;
                o.uv0   = v.uv0;
                return o;
            }

            half4 DepthFrag(VertOutD i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float a = tex2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
                AlphaDiscard(a, _AlphaCutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.posCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 3 — DepthNormals
        // =================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On
            Blend One Zero
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON
            #define _NORMAL_DROPOFF_TS 1

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON

            #pragma vertex   DNVert
            #pragma fragment DNFrag

            #define SHADERPASS SHADERPASS_DEPTHNORMALSONLY

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float  _AlphaCutoff;
                float  _NormalStrength;
                float  _SmoothnessScale;
                float  _WindStrength;
                float  _WindFrequency;
                float  _WindTurbulence;
                float  _WindDirX;
                float  _WindDirZ;
            CBUFFER_END

            sampler2D _MainTex;
            sampler2D _NormalMap;

            struct VertInDN  { float4 posOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv0:TEXCOORD0; float4 vcol:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VertOutDN { float4 posCS:SV_POSITION; float3 posWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv0:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            VertOutDN DNVert(VertInDN v)
            {
                VertOutDN o = (VertOutDN)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #ifdef _WIND_ON
                    float3 posWS_pre = TransformObjectToWorld(v.posOS.xyz);
                    float2 dXZ = ComputeWindXZ(
                        posWS_pre, v.uv0, v.vcol.r,
                        _WindStrength, _WindFrequency, _WindTurbulence,
                        _WindDirX, _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    v.posOS.xyz += mul((float3x3)GetWorldToObjectMatrix(), float3(dXZ.x, 0.0, dXZ.y));
                #endif

                VertexPositionInputs vpi = GetVertexPositionInputs(v.posOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.posCS    = vpi.positionCS;
                o.posWS    = vpi.positionWS;
                o.normalWS  = vni.normalWS;
                o.tangentWS = float4(vni.tangentWS,
                    (v.tangentOS.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale());
                o.uv0 = v.uv0;
                return o;
            }

            void DNFrag(VertOutDN i, out half4 outNormal : SV_Target0)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float a = tex2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
                AlphaDiscard(a, _AlphaCutoff);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.posCS);
                #endif

                float  renorm   = 1.0 / max(FLT_MIN, length(i.normalWS));
                float3 Nws      = i.normalWS * renorm;
                float3 Tws      = i.tangentWS.xyz * renorm;
                float3 Bws      = cross(i.normalWS, i.tangentWS.xyz) * i.tangentWS.w * renorm;
                float3 normalTS = UnpackNormalScale(
                    tex2D(_NormalMap, i.uv0 * _NormalMap_ST.xy + _NormalMap_ST.zw),
                    _NormalStrength);
                float3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(Tws, Bws, Nws)));

                #if defined(_GBUFFER_NORMALS_OCT)
                    outNormal = half4(PackFloat2To888(saturate(
                        PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5)), 0.0);
                #else
                    outNormal = half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 4 — SceneSelectionPass
        // =================================================================
        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON
            #define SCENESELECTIONPASS 1
            #define SHADERPASS SHADERPASS_DEPTHONLY

            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON
            #pragma vertex   SelVert
            #pragma fragment SelFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor; float4 _MainTex_ST; float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float _AlphaCutoff; float _NormalStrength; float _SmoothnessScale;
                float _WindStrength; float _WindFrequency; float _WindTurbulence;
                float _WindDirX; float _WindDirZ;
            CBUFFER_END

            int _ObjectId; int _PassValue;
            sampler2D _MainTex;

            struct VertInSel  { float4 posOS:POSITION; float2 uv0:TEXCOORD0; float4 vcol:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VertOutSel { float4 posCS:SV_POSITION; float2 uv0:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            VertOutSel SelVert(VertInSel v)
            {
                VertOutSel o; ZERO_INITIALIZE(VertOutSel, o);
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                #ifdef _WIND_ON
                    float3 pwp = TransformObjectToWorld(v.posOS.xyz);
                    float2 dXZ = ComputeWindXZ(pwp, v.uv0, v.vcol.r,
                        _WindStrength, _WindFrequency, _WindTurbulence, _WindDirX, _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    v.posOS.xyz += mul((float3x3)GetWorldToObjectMatrix(), float3(dXZ.x,0,dXZ.y));
                #endif
                VertexPositionInputs vpi = GetVertexPositionInputs(v.posOS.xyz);
                o.posCS = vpi.positionCS; o.uv0 = v.uv0;
                return o;
            }

            half4 SelFrag(VertOutSel i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
                clip(a - _AlphaCutoff);
                return half4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }

        // =================================================================
        // PASS 5 — ScenePickingPass
        // =================================================================
        Pass
        {
            Name "ScenePickingPass"
            Tags { "LightMode"="Picking" }

            HLSLPROGRAM
            #pragma target 4.5
            #define _ALPHATEST_ON
            #define SCENEPICKINGPASS 1
            #define SHADERPASS SHADERPASS_DEPTHONLY

            #pragma shader_feature_local _WIND_ON
            #pragma shader_feature_local _ROOTPIN_ON
            #pragma vertex   PickVert
            #pragma fragment PickFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor; float4 _MainTex_ST; float4 _NormalMap_ST;
                float4 _SmoothnessMap_ST;
                float _AlphaCutoff; float _NormalStrength; float _SmoothnessScale;
                float _WindStrength; float _WindFrequency; float _WindTurbulence;
                float _WindDirX; float _WindDirZ;
            CBUFFER_END

            float4 _SelectionID;
            sampler2D _MainTex;

            struct VertInPick  { float4 posOS:POSITION; float2 uv0:TEXCOORD0; float4 vcol:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VertOutPick { float4 posCS:SV_POSITION; float2 uv0:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            VertOutPick PickVert(VertInPick v)
            {
                VertOutPick o; ZERO_INITIALIZE(VertOutPick, o);
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                #ifdef _WIND_ON
                    float3 pwp = TransformObjectToWorld(v.posOS.xyz);
                    float2 dXZ = ComputeWindXZ(pwp, v.uv0, v.vcol.r,
                        _WindStrength, _WindFrequency, _WindTurbulence, _WindDirX, _WindDirZ,
                        #ifdef _ROOTPIN_ON
                            true,
                        #else
                            false,
                        #endif
                        _Time.y
                    );
                    v.posOS.xyz += mul((float3x3)GetWorldToObjectMatrix(), float3(dXZ.x,0,dXZ.y));
                #endif
                VertexPositionInputs vpi = GetVertexPositionInputs(v.posOS.xyz);
                o.posCS = vpi.positionCS; o.uv0 = v.uv0;
                return o;
            }

            half4 PickFrag(VertOutPick i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
                clip(a - _AlphaCutoff);
                return unity_SelectionID;
            }
            ENDHLSL
        }

    } // SubShader

    CustomEditor "UnityEditor.ShaderGraphLitGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
    Fallback Off
}
