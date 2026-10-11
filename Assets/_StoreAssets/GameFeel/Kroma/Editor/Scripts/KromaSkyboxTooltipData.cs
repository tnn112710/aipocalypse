using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    public static class KromaSkyboxTooltipData
    {
        private static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            // 1. Atmosphere & Gradient System
            { "_TopColor", "The color at the zenith (straight up)." },
            { "_MidColor", "The color of the sky band between the zenith and the horizon." },
            { "_HorizonColor", "The color exactly at the horizon line." },
            { "_UseTwoColorMode", "Toggle to ignore Mid Color and blend directly from Top to Horizon." },
            { "_Mid_Sky_Height", "Controls the vertical position of the Mid-Sky band." },
            { "_Mid_Sky_Softness", "Softness of the transition between Top and Mid colors." },
            { "_Horizon_Softness", "Softness of the transition between Mid and Horizon colors." },
            { "_Horizon_Offset", "Globally shifts the entire gradient up or down." },
            { "_Horizon_Level", "Center height coordinate of the horizon band." },
            { "_Horizon_Fade", "Edge blend transition softness of the horizon band." },
            { "_Top_Fade", "Fades the sky to black or transparent at the very top (useful for specific effects)." },
            { "_GlobalRotation", "Rotates the entire skybox (Clouds/Stars) around the Y-axis." },
            { "_SimulateGround", "Enables the ground plane simulation." },
            { "_Ground_Color", "The color of the 'ground' visible below the horizon." },
            { "_Ground_Blend", "Controls where the ground starts on the vertical axis." },
            { "_Ground_Horizon_Smoothness", "Softness of the blend between the Horizon and the Ground." },

            // 2. Celestial (Sun/Moon/Planets)
            { "Toggle", "Toggles the visibility of this celestial body." },
            { "Texture", "The 2D texture map representing the body." },
            { "Tint", "Tint applied to the celestial texture." },
            { "Size", "Scaling multiplier of the celestial disk." },
            { "EdgeSoftness", "Softness falloff applied to the edge of the celestial disk." },
            { "Rotation", "Local rotation of the texture around its center axis." },
            { "RotateWithCamera", "Enables billboard-facing alignment toward the active camera." },
            { "Phase", "Horizontal mask translation value (-1 to 1) for crescent phase shadows." },
            { "InvertPhase", "Inverts the crescent shadow direction." },
            { "PhaseSize", "The size scaling of the crescent shadow sphere." },
            { "PhaseSmoothness", "Softness transition along the crescent shadow boundary line." },
            { "DarkSideOcclusion", "Blends the shadow mask to block background star visibility (0 to 1)." },
            { "Fade", "Overall opacity and intensity multiplier." },
            { "Azimuth", "Horizontal rotation angle (0 to 360 degrees) around the sky." },
            { "Altitude", "Vertical elevation angle (-90 to 90 degrees) relative to the horizon." },

            // Glow / Atmosphere
            { "GlowEnabled", "Enables the primary tight atmospheric glow." },
            { "GlowColor", "Color of the primary atmospheric glow." },
            { "GlowPower", "Exponential falloff rate of the glow (higher values restrict size)." },
            { "GlowIntensity", "Brightness intensity scalar for the primary glow." },
            { "AtmosphereEnabled", "Enables the secondary large scattering glow layer." },
            { "AtmosphereColor", "Color of the large scattering atmosphere glow." },
            { "AtmosphereSize", "Radius of the large scattering atmosphere glow." },
            { "AtmosphereBlur", "Softness and falloff blending of the large atmospheric glow." },
            { "AtmosphereGlowPosition", "Offset position relative to the celestial disk." },
            { "AtmosphereOpacity", "Maximum opacity value of the large atmosphere glow." },
            { "Atmosphere2Enabled", "Enables the third ultra-broad space corona/ambient glow layer." },
            { "Atmosphere2Color", "Color of the third ambient corona glow." },
            { "Atmosphere2Size", "Radial size of the third ambient corona glow." },
            { "Atmosphere2Blur", "Falloff blending rate of the outer corona glow." },
            { "Atmosphere2GlowPosition", "Position displacement offset for the outer corona glow." },
            { "Atmosphere2Opacity", "Opacity multiplier for the corona glow." },
            { "Tiling", "UV tiling coordinates (Vector4) for texture alignment." },

            // 3. Clouds 1 & 2
            { "CloudsToggle", "Toggles the rendering of this cloud layer." },
            { "CloudTexture", "The primary noise or structure texture map." },
            { "BaseColor", "Color applied to the unlit cloud body." },
            { "Scale", "Global scaling of the cloud texture tiling." },
            { "Speed", "Vector2 (X, Y) velocity values defining continuous cloud movement." },
            { "Density", "Threshold cutoff for cloud coverage (higher values = thicker cover)." },
            { "Intensity", "Brightness and alpha multiplier of the cloud layer." },
            { "Erosion", "Edge clipping threshold to procedurally dissolve cloud borders." },
            { "HighlightsToggle", "Toggles directional light highlights on the clouds." },
            { "HighlightColor", "Highlight color applied to sun-facing cloud edges." },
            { "HighlightControl", "Soft threshold defining how far sunlight penetrates the cloud." },
            { "HighlightContrast", "Sharpness scalar applied to cloud light-to-shadow transitions." },
            { "HighlightSmoothness", "Softness of the highlight falloff gradient." },
            { "DetailTexToggle", "Blends a second micro-noise texture to add high-frequency details." },
            { "DetailTexture", "The secondary micro-noise texture map." },
            { "DetailScale", "Tiling multiplier of the secondary micro-noise detail pass." },
            { "DetailSpeed", "Movement speed scalar of the detail pass relative to the base cloud." },
            { "HeightScale", "Sculpting normal height displacement intensity." },
            { "ErosionMultiplier", "Amplification scale of the procedural erosion pass." },
            { "SunBoost", "Extra intensity boost applied to cloud lighting when facing the Sun." },
            { "BaseSpeed", "Overall baseline animation speed dampening factor." },
            { "BaseEpsilon", "Mathematical epsilon step value for primary noise sampling." },
            { "DetailEpsilon", "Step offset scale for high-frequency detail noise sampling." },
            { "LightingWrap", "Light wrapping control (0.5 = mathematically optimal light bleed)." },
            { "AlphaEdge", "Alpha mask edge feathering range." },
            { "ScatterMin", "Atmospheric forward-scattering sharpness parameter (sharp edges)." },
            { "ScatterMax", "Broad scattering atmospheric dispersion radius." },
            { "HeightBias", "Base height displacement bias shaping cloud flatness." },
            { "HorizonLevel", "Vertical height alignment coordinate." },
            { "HorizonFade", "Edge blending distance of clouds at the horizon." },
            { "TopFade", "Fading cutoff scale at the zenith." },

            // 4. Stars
            { "_StarsToggle", "Toggles the star layer." },
            { "_StarCubeMap", "Cubemap containing the stars and Milky Way galaxy." },
            { "_StarExposure", "Brightness exponent control (-16 to 16) for star intensity." },
            { "_StarSize", "Scaling parameter of the star cubemap projection." },
            { "_StarLatitude", "Vertical tilt of the star rotation axis relative to the zenith (-90 to 90)." },
            { "_StarSpeed", "Automatic background rotation speed over time." },
            { "_StarRotation", "Manual rotation value (0 to 1) for static time-of-day configuration." },
            { "_StarTint", "Color multiplier applied to stars." },
            { "_Star_Blend", "Horizon distance blending factor (fades stars near atmosphere)." },
            { "_Star_Blend_Contrast", "Sharpness curve of the horizon fade blend." },

            // 5. God Rays
            { "_GodRaysToggle", "Toggles the atmospheric god ray pass." },
            { "_GodRaysColor", "Color of the atmospheric light shafts." },
            { "_GodRaysDensity", "Tiling frequency density of the ray-marching pattern." },
            { "_GodRaysContrast", "Contrast difference between bright shafts and dark gaps." },
            { "_GodRaysBeamBlur", "Visual blurring and softness applied to ray shafts." },
            { "_GodRaysBeamSweep", "Horizontal pan/sweep velocity of the ray beams." },
            { "_GodRaysRotateWithCamera", "Toggles screen-space camera rotation tracking for the shafts." },
            { "_GodRaysSpeed", "Velocity of the procedural respiratory breathing pulse animation." },
            { "_GodRaysPackCount", "Number of distinct beam groupings/clusters." },
            { "_GodRaysMinPackWidth", "Minimum scale threshold of ray groupings." },
            { "_GodRaysMaxPackWidth", "Maximum scale threshold of ray groupings." },
            { "_GodRaysLength", "Maximum projection throw distance of ray shafts from the light source." },
            { "_GodRaysFalloff", "Exponential vertical dissipation rate at the bottom of rays." },
            { "_GodRaysHoleSize", "Mask radius carving out ray paths when blocked by clouds." },
            { "_GodRaysHoleSoftness", "Softness blend of the cloud occlusion mask." },
            { "_GodRaysHoleNoise", "Procedural noise warping the cloud shadow mask." },
            { "_GodRaysHorizonFade", "Height threshold offset filtering rays below the horizon." },
            { "_GodRaysHorizonMaskPower", "Blending sharpness applied to the horizon ray mask." },
            { "_GodRaysHorizonCruft", "Procedural variation intensity along the horizon occlusion mask." }
        };

        public static string GetTooltip(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return string.Empty;

            // 1. Check direct match
            if (Tooltips.TryGetValue(propertyName, out string tooltip))
            {
                return tooltip;
            }

            // 2. Normalization for shared sub-properties (e.g. Celestial 1/2 or Cloud 1/2)
            string normalized = propertyName;

            // Celestial prefixes
            if (normalized.StartsWith("_C1_")) normalized = normalized.Substring(4);
            else if (normalized.StartsWith("_C2_")) normalized = normalized.Substring(4);
            else if (normalized.StartsWith("_")) normalized = normalized.Substring(1);

            if (Tooltips.TryGetValue(normalized, out tooltip))
            {
                return tooltip;
            }

            // Cloud prefixes
            normalized = propertyName;
            if (normalized.StartsWith("_Cloud2_")) normalized = normalized.Substring(8);
            else if (normalized.StartsWith("_Cloud2")) normalized = normalized.Substring(7);
            else if (normalized.StartsWith("_Cloud_")) normalized = normalized.Substring(7);
            else if (normalized.StartsWith("_Cloud")) normalized = normalized.Substring(6);
            else if (normalized.StartsWith("_Color_")) normalized = "BaseColor"; // Special mapping
            else if (normalized.StartsWith("_Clouds2Toggle")) normalized = "CloudsToggle";
            else if (normalized.StartsWith("_CloudsToggle")) normalized = "CloudsToggle";
            else if (normalized.StartsWith("_")) normalized = normalized.Substring(1);

            // Special mappings
            if (normalized == "DetailScale") normalized = "DetailScale";
            else if (normalized == "DetailSpeedMultiplier") normalized = "DetailSpeed";
            else if (normalized == "DetailTexToggle") normalized = "DetailTexToggle";
            else if (normalized == "Detail_Texture") normalized = "DetailTexture";
            else if (normalized == "ErosionMultiplier") normalized = "ErosionMultiplier";
            else if (normalized == "AlphaEdge") normalized = "AlphaEdge";
            else if (normalized == "HeightScale") normalized = "HeightScale";
            else if (normalized == "LightingWrap") normalized = "LightingWrap";
            else if (normalized == "SunBoost") normalized = "SunBoost";
            else if (normalized == "ScatterMin") normalized = "ScatterMin";
            else if (normalized == "ScatterMax") normalized = "ScatterMax";
            else if (normalized == "HeightBias") normalized = "HeightBias";
            else if (normalized == "BaseEpsilon") normalized = "BaseEpsilon";
            else if (normalized == "DetailEpsilon") normalized = "DetailEpsilon";
            else if (normalized == "BaseSpeedMultiplier") normalized = "BaseSpeed";
            else if (normalized == "Horizon_Level") normalized = "HorizonLevel";
            else if (normalized == "Horizon_Fade") normalized = "HorizonFade";
            else if (normalized == "Top_Fade") normalized = "TopFade";

            if (Tooltips.TryGetValue(normalized, out tooltip))
            {
                return tooltip;
            }

            return string.Empty;
        }
    }
}
