Shader "FayLabs/Kroma/Skybox V2"
{
    Properties
    {
        [HDR]_TopColor("Top Color", Color) = (0.08627451, 0.3803922, 0.7450981, 1)
        [HDR]_MidColor("Mid Color", Color) = (0, 0.7254902, 0.9490196, 1)
        [HDR]_HorizonColor("Horizon Color", Color) = (0.3882353, 0.6862745, 0.7607843, 1)
        [Toggle(_SIMULATE_GROUND)] _SimulateGround("Simulate Ground", Float) = 1
        _Ground_Color("Ground Color", Color) = (0.01960784, 0.01960784, 0.01960784, 1)
        _Ground_Blend("Ground Height", Range(0, 0.1)) = 0.01
        _Ground_Horizon_Smoothness("Ground Falloff", Range(0, 20)) = 0.2
        _Horizon_Offset("Horizon Offset", Range(-1, 1)) = 0.1
        [Toggle(_TWO_COLOR_MODE)] _UseTwoColorMode("Use 2 color mode", Float) = 0
        _Mid_Sky_Height("Sky Gradient Height", Range(-1, 1)) = 0.1
        _Horizon_Softness("Horizon Falloff", Range(0, 2)) = 0.3
        _Mid_Sky_Softness("Sky Gradient Falloff", Range(0, 2)) = 0.7
        _Top_Fade("Top Fade", Range(0, 10)) = 0
        _Horizon_Level("Horizon Level", Range(-1, 1)) = 0.5
        _Horizon_Fade("Horizon Fade", Range(0, 1)) = 0.1
        _GlobalRotation("Global Rotation", Range(0, 360)) = 0

        [Toggle(_CELESTIAL_ON)] _CelestialToggle("Sun / Moon Enabled", Float) = 1
        [HDR]_Tint("Celestial Tint", Color) = (0.8679245, 0.6753216, 0.3575412, 1)
        _Size("Celestial Size", Range(0, 1)) = 0.04
        _CelestialEdgeSoftness("Celestial Softness", Range(0, 1)) = 0.02
        _Rotation("Celestial Rotation", Range(0, 360)) = 0
        [Toggle(_ROTATE_WITH_CAMERA)] _RotateWithCamera("Rotate With Camera", Float) = 0
        _Phase("Celestial Phase", Range(-1, 1)) = 0
        [Toggle(_INVERT_PHASE)] _InvertPhase("Invert Phase", Float) = 0
        _PhaseSize("Phase Mask Size", Range(0.01, 2.0)) = 0.5
        _PhaseSmoothness("Phase Smoothness", Range(0, 1.0)) = 0.05
        _DarkSideOcclusion("Dark Side Occlusion", Range(0, 1)) = 0.0
        _Fade("Celestial Fade", Range(0, 1)) = 1
        
        [Toggle(_GLOW_ON)] _GlowEnabled("Glow Enabled", Float) = 1
        [HDR]_GlowColor("Glow Color", Color) = (0,0,0,0)
        _GlowPower("Glow Power", Range(0.1, 10)) = 5.0
        _GlowIntensity("Glow Intensity", Range(0, 10)) = 2.0
        
        [Toggle(_ATMOSPHERE_ON)] _AtmosphereEnabled("Outer Glow 1 Enabled", Float) = 0
        [HDR]_AtmosphereColor("Outer Glow 1 Color", Color) = (0,0,0,0)
        _AtmosphereSize("Outer Glow 1 Size", Range(0, 5)) = 0.3
        _AtmosphereBlur("Outer Glow 1 Blur", Range(0, 10)) = 0.3
        _AtmosphereGlowPosition("Outer Glow 1 Glow Position", Range(-1, 1)) = 0 // Was Phase Influence
        _AtmosphereOpacity("Outer Glow 1 Opacity", Range(0, 1)) = 1.0

        [Toggle(_ATMOSPHERE2_ON)] _Atmosphere2Enabled("Outer Glow 2 (Space) Enabled", Float) = 0
        [HDR]_Atmosphere2Color("Outer Glow 2 (Space) Color", Color) = (0,0,0,0)
        _Atmosphere2Size("Outer Glow 2 (Space) Size", Range(0, 5)) = 1.5
        _Atmosphere2Blur("Outer Glow 2 (Space) Blur", Range(0, 10)) = 1.5
        _Atmosphere2GlowPosition("Outer Glow 2 (Space) Glow Position", Range(-1, 1)) = 0 // Was Phase Influence
        _Atmosphere2Opacity("Outer Glow 2 (Space) Opacity", Range(0, 1)) = 1.0
        _Tiling("Celestial Tiling", Vector) = (0.4, 0.4, 0, 0)
        [NoScaleOffset]_Texture("Celestial Texture", 2D) = "white" {}

        // Clouds 1
        [HDR]_Color_1("Cloud Base Color", Color) = (1, 1, 1, 1)
        [HDR]_CloudHighlightColor("Cloud Highlight Color", Color) = (1, 1, 1, 1)
        _CloudHighlightControl("Highlight Throw", Range(0, 1)) = 0.5
        _CloudHighlightContrast("Highlight Contrast", Range(0.1, 5)) = 1.0
        _CloudHighlightSmoothness("Highlight Smoothness", Range(0, 5)) = 0.5
        _CloudErosion("Cloud Erosion", Range(0, 1)) = 0.0
        _Scale("Cloud Scale", Range(0, 2.0)) = 0.1
        _Speed("Cloud Speed", Vector) = (0, 0, 0, 0)
        _Density("Density", Range(0.0, 1.0)) = 0.52
        _Intensity("Cloud Intensity", Range(0, 2)) = 1
        [NoScaleOffset]_Cloud_Texture("Cloud Texture", 2D) = "white" {}
        [Toggle(_CLOUD_DETAIL_TEX_ON)] _CloudDetailTexToggle("Use Detail Texture", Float) = 0
        [NoScaleOffset]_Cloud_Detail_Texture("Cloud Detail Texture", 2D) = "white" {}
        [Toggle(_CLOUDS_ON)] _CloudsToggle("Clouds 1 Enabled", Float) = 1
        [Toggle(_CLOUD_HIGHLIGHTS_ON)] _CloudHighlightsToggle("Highlights Enabled", Float) = 1
        
        // Dev / Debug Properties (Exposed Hardcoded Values)
        _CloudDetailScale("Detail Scale", Range(0.1, 5)) = 1.5
        _CloudDetailSpeedMultiplier("Detail Speed Multiplier", Range(0, 2)) = 0.45
        _CloudHeightScale("Normal Height Scale", Range(0.1, 10)) = 2.0
        _CloudErosionMultiplier("Erosion Strength", Range(0, 2)) = 0.8
        _CloudSunBoost("Sun Boost Multiplier", Range(0, 10)) = 2.0
        _CloudBaseSpeedMultiplier("Base Speed Multiplier", Range(0, 1)) = 0.2
        _CloudBaseEpsilon("Base Detail Epsilon", Range(0, 0.1)) = 0.01
        _CloudDetailEpsilon("Detail Epsilon Scale", Range(0, 0.2)) = 0.05
        _CloudLightingWrap("Lighting Wrap (0.5=Half)", Range(0, 1)) = 0.5
        _CloudAlphaEdge("Alpha Edge Softness", Range(0.01, 1)) = 0.2
        _CloudScatterMin("Scatter Power Min (Sharp)", Range(1, 100)) = 20.0
        _CloudScatterMax("Scatter Power Max (Broad)", Range(0.1, 10)) = 1.0
        _CloudHeightBias("Height Bias", Range(-0.5, 0.5)) = 0.15

        // Clouds 2
        [Toggle(_CLOUDS2_ON)] _Clouds2Toggle("Clouds 2 Enabled", Float) = 0
        [Toggle(_CLOUD2_HIGHLIGHTS_ON)] _Cloud2HighlightsToggle("C2 Highlights Enabled", Float) = 1
        [HDR]_Cloud2_Color("Base Color", Color) = (1, 1, 1, 1)
        [HDR]_Cloud2_HighlightColor("Highlight Color", Color) = (1, 1, 1, 1)
        _Cloud2_HighlightControl("Highlight Throw", Range(0, 1)) = 0.5
        _Cloud2_HighlightContrast("Highlight Contrast", Range(0.1, 5)) = 1.0
        _Cloud2_HighlightSmoothness("Highlight Smoothness", Range(0, 5)) = 0.5
        _Cloud2_Erosion("Erosion", Range(0, 1)) = 0.0
        _Cloud2_Scale("Scale", Range(0, 2.0)) = 0.1
        _Cloud2_Speed("Speed", Vector) = (0, 0, 0, 0)
        _Cloud2_Density("Density", Range(0.0, 1.0)) = 0.52
        _Cloud2_Intensity("Intensity", Range(0, 2)) = 1
        [NoScaleOffset]_Cloud2_Texture("Cloud Texture", 2D) = "white" {}
        [Toggle(_CLOUD2_DETAIL_TEX_ON)] _Cloud2DetailTexToggle("Use Detail Texture", Float) = 0
        [NoScaleOffset]_Cloud2_Detail_Texture("Detail Texture", 2D) = "white" {}
        
        // C2 Dev / Debug
        _Cloud2_DetailScale("Detail Scale", Range(0.1, 5)) = 1.5
        _Cloud2_DetailSpeedMultiplier("Detail Speed Multiplier", Range(0, 2)) = 0.45
        _Cloud2_HeightScale("Normal Height Scale", Range(0.1, 10)) = 2.0
        _Cloud2_ErosionMultiplier("Erosion Strength", Range(0, 2)) = 0.8
        _Cloud2_SunBoost("Sun Boost Multiplier", Range(0, 10)) = 2.0
        _Cloud2_BaseSpeedMultiplier("Base Speed Multiplier", Range(0, 1)) = 0.2
        _Cloud2_BaseEpsilon("Base Detail Epsilon", Range(0, 0.1)) = 0.01
        _Cloud2_DetailEpsilon("Detail Epsilon Scale", Range(0, 0.2)) = 0.05
        _Cloud2_LightingWrap("Lighting Wrap", Range(0, 1)) = 0.5
        _Cloud2_AlphaEdge("Alpha Edge", Range(0.01, 1)) = 0.2
        _Cloud2_ScatterMin("Scatter Min", Range(1, 100)) = 20.0
        _Cloud2_ScatterMax("Scatter Max", Range(0.1, 10)) = 1.0
        _Cloud2_HeightBias("Height Bias", Range(-0.5, 0.5)) = 0.15

        // C2 World Blending
        _Cloud2_Horizon_Level("Horizon Level", Range(-1, 1)) = 0.0
        _Cloud2_Horizon_Fade("Horizon Fade", Range(0, 1)) = 0.1
        _Cloud2_Top_Fade("Top Fade", Range(0, 10)) = 0.5

        // Celestial 1
        [Toggle(_CELESTIAL1_ON)] _C1_Enabled("Celestial 1 Enabled", Float) = 0
        [HDR]_C1_Tint("C1 Tint", Color) = (1, 1, 1, 1)
        _C1_Size("C1 Size", Range(0, 2)) = 0.2
        _C1_EdgeSoftness("C1 Softness", Range(0, 1)) = 0.02
        _C1_Rotation("C1 Rotation", Range(0, 360)) = 0
        [Toggle(_C1_ROTATE_WITH_CAMERA)] _C1_RotateWithCamera("C1 Rotate With Camera", Float) = 0
        _C1_Phase("C1 Phase", Range(-1, 1)) = 0
        [Toggle(_C1_INVERT_PHASE)] _C1_InvertPhase("C1 Invert Phase", Float) = 0
        _C1_PhaseSize("C1 Phase Mask Size", Range(0.01, 2.0)) = 0.5
        _C1_PhaseSmoothness("C1 Phase Smoothness", Range(0, 1.0)) = 0.05
        _C1_DarkSideOcclusion("C1 Dark Side Occlusion", Range(0, 1)) = 0.0
        _C1_Azimuth("C1 Azimuth", Range(0, 360)) = 45
        _C1_Altitude("C1 Altitude", Range(-90, 90)) = 45
        _C1_Fade("C1 Fade", Range(0, 1)) = 1
        [Toggle(_C1_GLOW_ON)] _C1_GlowEnabled("C1 Glow Enabled", Float) = 1
        [HDR]_C1_GlowColor("C1 Glow Color", Color) = (0,0,0,0)
        _C1_GlowPower("C1 Glow Power", Range(0.1, 10)) = 5.0
        _C1_GlowIntensity("C1 Glow Intensity", Range(0, 10)) = 2.0
        
        [Toggle(_C1_ATMOSPHERE_ON)] _C1_AtmosphereEnabled("C1 Outer Glow 1 Enabled", Float) = 0
        [HDR]_C1_AtmosphereColor("C1 Outer Glow 1 Color", Color) = (0,0,0,0)
        _C1_AtmosphereSize("C1 Outer Glow 1 Size", Range(0, 5)) = 0.3
        _C1_AtmosphereBlur("C1 Outer Glow 1 Blur", Range(0, 10)) = 0.3
        _C1_AtmosphereGlowPosition("C1 Glow Position", Range(-1, 1)) = 0
        _C1_AtmosphereOpacity("C1 Outer Glow 1 Opacity", Range(0, 1)) = 1.0

        [Toggle(_C1_ATMOSPHERE2_ON)] _C1_Atmosphere2Enabled("C1 Outer Glow 2 Enabled", Float) = 0
        [HDR]_C1_Atmosphere2Color("C1 Outer Glow 2 Color", Color) = (0,0,0,0)
        _C1_Atmosphere2Size("C1 Outer Glow 2 Size", Range(0, 5)) = 1.5
        _C1_Atmosphere2Blur("C1 Outer Glow 2 Blur", Range(0, 10)) = 1.5
        _C1_Atmosphere2GlowPosition("C1 Glow Position", Range(-1, 1)) = 0
        _C1_Atmosphere2Opacity("C1 Outer Glow 2 Opacity", Range(0, 1)) = 1.0
        _C1_Tiling("C1 Tiling", Vector) = (0.4, 0.4, 0, 0)
        [NoScaleOffset]_C1_Texture("C1 Texture", 2D) = "white" {}

        // Celestial 2
        [Toggle(_CELESTIAL2_ON)] _C2_Enabled("Celestial 2 Enabled", Float) = 0
        [HDR]_C2_Tint("C2 Tint", Color) = (1, 1, 1, 1)
        _C2_Size("C2 Size", Range(0, 2)) = 0.2
        _C2_EdgeSoftness("C2 Softness", Range(0, 1)) = 0.02
        _C2_Rotation("C2 Rotation", Range(0, 360)) = 0
        [Toggle(_C2_ROTATE_WITH_CAMERA)] _C2_RotateWithCamera("C2 Rotate With Camera", Float) = 0
        _C2_Phase("C2 Phase", Range(-1, 1)) = 0
        [Toggle(_C2_INVERT_PHASE)] _C2_InvertPhase("C2 Invert Phase", Float) = 0
        _C2_PhaseSize("C2 Phase Mask Size", Range(0.01, 2.0)) = 0.5
        _C2_PhaseSmoothness("C2 Phase Smoothness", Range(0, 1.0)) = 0.05
        _C2_DarkSideOcclusion("C2 Dark Side Occlusion", Range(0, 1)) = 0.0
        _C2_Azimuth("C2 Azimuth", Range(0, 360)) = 225
        _C2_Altitude("C2 Altitude", Range(-90, 90)) = 60
        _C2_Fade("C2 Fade", Range(0, 1)) = 1
        [Toggle(_C2_GLOW_ON)] _C2_GlowEnabled("C2 Glow Enabled", Float) = 1
        [HDR]_C2_GlowColor("C2 Glow Color", Color) = (0,0,0,0)
        _C2_GlowPower("C2 Glow Power", Range(0.1, 10)) = 5.0
        _C2_GlowIntensity("C2 Glow Intensity", Range(0, 10)) = 2.0
        
        [Toggle(_C2_ATMOSPHERE_ON)] _C2_AtmosphereEnabled("C2 Outer Glow 1 Enabled", Float) = 0
        [HDR]_C2_AtmosphereColor("C2 Outer Glow 1 Color", Color) = (0,0,0,0)
        _C2_AtmosphereSize("C2 Outer Glow 1 Size", Range(0, 5)) = 0.3
        _C2_AtmosphereBlur("C2 Outer Glow 1 Blur", Range(0, 10)) = 0.3
        _C2_AtmosphereGlowPosition("C2 Glow Position", Range(-1, 1)) = 0
        _C2_AtmosphereOpacity("C2 Outer Glow 1 Opacity", Range(0, 1)) = 1.0

        [Toggle(_C2_ATMOSPHERE2_ON)] _C2_Atmosphere2Enabled("C2 Outer Glow 2 Enabled", Float) = 0
        [HDR]_C2_Atmosphere2Color("C2 Outer Glow 2 Color", Color) = (0,0,0,0)
        _C2_Atmosphere2Size("C2 Outer Glow 2 Size", Range(0, 5)) = 1.5
        _C2_Atmosphere2Blur("C2 Outer Glow 2 Blur", Range(0, 10)) = 1.5
        _C2_Atmosphere2GlowPosition("C2 Glow Position", Range(-1, 1)) = 0
        _C2_Atmosphere2Opacity("C2 Outer Glow 2 Opacity", Range(0, 1)) = 1.0
        _C2_Tiling("C2 Tiling", Vector) = (0.4, 0.4, 0, 0)
        [NoScaleOffset]_C2_Texture("C2 Texture", 2D) = "white" {}

        [Toggle(_STARS_ON)] _StarsToggle("Stars Enabled", Float) = 1
        [NoScaleOffset] _StarCubeMap ("Star Cube Map", Cube) = "black" {}

        _StarExposure ("Star Exposure", Range(-16, 16)) = 0
        _StarSize ("Star Size", Range(0.02, 2.0)) = 1.0

        _StarLatitude ("Star Latitude", Range(-90, 90)) = 0
        _StarSpeed ("Star Speed (Animation)", Range(0.0, 0.01)) = 0.001
        _StarRotation ("Star Rotation (Manual)", Range(0, 1)) = 0
        
        [HDR]_StarTint ("Star Tint", Color) = (1, 1, 1, 1)
        _Star_Blend ("Horizon Fade", Range(0, 5)) = 0.2
        _Star_Blend_Contrast ("Fade Contrast", Range(0.1, 4)) = 1.0

        // God Rays
        [Toggle(_GODRAYS_ON)] _GodRaysToggle("God Rays Enabled", Float) = 0
        [HDR]_GodRaysColor ("Ray Color", Color) = (1, 0.85, 0.65, 1)
        _GodRaysDensity ("Ray Texture Density", Range(1, 20)) = 10.0 
        _GodRaysContrast ("Ray Contrast", Range(1.0, 10.0)) = 4.0 
        _GodRaysBeamBlur ("Beam Blur", Range(0.0, 5.0)) = 0.5
        _GodRaysBeamSweep ("Beam Sweep Speed", Range(-5.0, 5.0)) = 0.5
        [Toggle(_GODRAYS_ROTATE_WITH_CAMERA)] _GodRaysRotateWithCamera("Rotate With Camera", Float) = 0
        _GodRaysSpeed ("Ray Breathe Speed", Range(0, 2)) = 0.1      
        _GodRaysPackCount ("Pack Count", Range(0, 10)) = 5.0            
        _GodRaysMinPackWidth ("Min Pack Size", Range(0.0, 1.0)) = 0.2 
        _GodRaysMaxPackWidth ("Max Pack Size", Range(0.0, 1.0)) = 0.8 
        _GodRaysHoleSize ("Cloud Hole Radius", Range(-1.0, 1.0)) = 0.15    
        _GodRaysHoleSoftness ("Hole Edge Softness", Range(0.01, 1.0)) = 0.5
        _GodRaysHoleNoise ("Hole Irregularity", Range(0.0, 0.5)) = 0.2   
        _GodRaysLength ("Ray Throw Distance", Range(0.0, 5.0)) = 3.5  
        _GodRaysFalloff ("Bottom Falloff", Range(0.1, 5.0)) = 1.0
        
        // New God Ray Properties
        _GodRaysHorizonFade ("Horizon Fade Level", Range(-0.5, 0.5)) = 0.0
        _GodRaysHorizonMaskPower ("Horizon Fade Softness", Range(0.1, 20.0)) = 5.0
        _GodRaysHorizonCruft ("Horizon Variablity", Range(0, 1)) = 0.2
        

        // Legacy properties might be hidden or reused
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Background" 
            "Queue"="Background" 
            "PreviewType"="Skybox" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "Skybox"
            ZWrite Off
            ZTest LEqual
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Keywords
            #pragma shader_feature _SIMULATE_GROUND
            #pragma shader_feature _TWO_COLOR_MODE
            #pragma shader_feature _CLOUDS_ON
            #pragma shader_feature _CLOUDS2_ON
            #pragma shader_feature _CLOUD_HIGHLIGHTS_ON
            #pragma shader_feature _CLOUD2_HIGHLIGHTS_ON
            #pragma shader_feature _CLOUD_DETAIL_TEX_ON
            #pragma shader_feature _CLOUD2_DETAIL_TEX_ON
            #pragma shader_feature _CELESTIAL_ON
            #pragma shader_feature _CELESTIAL1_ON
            #pragma shader_feature _CELESTIAL2_ON
            #pragma shader_feature _STARS_ON
            #pragma shader_feature _ATMOSPHERE2_ON
            #pragma shader_feature _C1_ATMOSPHERE2_ON
            #pragma shader_feature _C2_ATMOSPHERE2_ON
            #pragma shader_feature _GODRAYS_ON
            #pragma shader_feature _GODRAYS_ROTATE_WITH_CAMERA
            #pragma shader_feature _KROMA_6000_0_COMPATIBILITY
            
            // Includes
            #include "Kroma_Shared.hlsl"
            #include "Kroma_Colors.hlsl"
            #include "Kroma_Clouds.hlsl"
            #include "Kroma_Stars.hlsl"
            #include "Kroma_Celestial.hlsl"
            #include "Kroma_GodRays.hlsl"

            // Vertex Shader
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                
                output.positionOS = input.positionOS.xyz;
                
                output.uv = input.uv;
                
                return output;
            }

            // Fragment Shader
            half4 frag(Varyings input) : SV_Target
            {
                // Skybox needs direction FROM Camera TO Surface.
                float3 viewDir;
                #if defined(_KROMA_6000_0_COMPATIBILITY)
                    float4 clipPos = TransformObjectToHClip(input.positionOS);
                    float4 worldPos = mul(UNITY_MATRIX_I_VP, clipPos);
                    float3 posWS = worldPos.xyz / worldPos.w;
                    viewDir = normalize(posWS - GetCameraPositionWS());
                #else
                    viewDir = normalize(input.positionOS);
                #endif
                
                // 1. Sky Gradient
                // Gradient remains unrotated (it's atmospheric/physical)
                float4 skyGradient = CalculateSkyGradient(viewDir);
                float3 color = skyGradient.rgb;
                float skyVis = skyGradient.a; // 1.0 = Sky, 0.0 = Ground

                // 2. Celestial (Sun/Moon)
                // We calculate celestial BEFORE stars to get the Occlusion Mask
                // Celestial bodies usually track a light source direction, so they stay locked to world light.
                float totalOcclusion = 0.0;
                float4 celestial = CalculateCelestial(viewDir, totalOcclusion);
                celestial.a *= skyVis; // Occlude if behind ground
                
                // ROTATION LOGIC
                // We rotate the viewDir used for sampling Stars and Clouds to simulate sky rotation.
                float3 rotatedViewDir = RotateAroundYInDegrees(viewDir, -_GlobalRotation);
                
                // 3. Stars
                // Use rotated viewDir
                float3 stars = CalculateStars(rotatedViewDir, _Time.y, input.positionOS);
                stars *= skyVis; // Occlude if behind ground
                
                // MASK STARS WITH CELESTIAL OCCLUSION
                // If a planet is there (occlusion > 0), hide stars.
                stars *= (1.0 - totalOcclusion);
                
                color += stars;

                // Pre-Multiplied Alpha Blend (Allows Additive Glow + Opaque Body)
                color = color * (1.0 - celestial.a) + celestial.rgb;
                
                // 4. Clouds 2 (Background)
                // Use rotated viewDir
                float4 clouds2 = CalculateClouds2(rotatedViewDir, float3(_Time.y, _Time.y, _Time.y));
                
                // Composite Clouds 2 (Alpha blend)
                color = lerp(color, clouds2.rgb, clouds2.a);

                // 5. Clouds 1 (Foreground)
                float4 clouds = CalculateClouds(rotatedViewDir, float3(_Time.y, _Time.y, _Time.y));
                
                // Composite Clouds 1 (Alpha blend)
                color = lerp(color, clouds.rgb, clouds.a);
                
                // 5. Dithering (Anti-Banding)
                // Use raw pixel coordinates (positionCS) instead of dividing by _ScreenParams 
                // to prevent scale-blowing grid artifacts during Unity render passes.
                // 0.05 is the mathematically optimal intensity to shatter 8-bit color banding
                // without the interleaving grid becoming visible to the human eye.
                color = ApplyDithering(color, input.positionCS.xy, 0.05);
                
                // 6. God Rays (Additive on top of everything)
                #if defined(_GODRAYS_ON)
                    // Use rotated viewDir? The user said "related to sun position".
                    // Standard God Rays emanate from the light source. 
                    // If we rotate the viewDir, we rotate the sky. The light source might be static or moving.
                    // CalculateGodRays uses _MainLightPosition. If _MainLightPosition is world space, and we want rays to match the sky...
                    // If the Skybox rotates, the "World" rotates. So the Light direction relative to the camera should arguably effectively rotate if it's part of the sky.
                    // However, Unity's MainLight is usually a transform in the scene.
                    // If we use the REAL viewDir (non-rotated), the rays will align with the real Directional Light object.
                    // This is the safest bet for "God Rays" that match the lighting in the scene.
                    // But if the user wants them to emanate from the "Sun" in the skybox texture, that sun might be rotated by _GlobalRotation.
                    
                    // Kroma's Celestial bodies (Sun) track the Main ViewDir normally, BUT we apply rotation to the celestial bodies themselves? 
                    // Wait, CalculateCelestial uses `viewDir` (non-rotated).
                    // And Kroma's Sun logic forces alignment to `_MainLightPosition`.
                    // So using non-rotated `viewDir` is consistent with Kroma's Sun.
                    
                    float3 godRays = CalculateGodRays(viewDir, _MainLightPosition.xyz, float3(_Time.y, 0, 0));
                    color += godRays;
                #endif

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "FayLabs.Kroma.Editor.KromaSkyboxShaderGUI"
}
