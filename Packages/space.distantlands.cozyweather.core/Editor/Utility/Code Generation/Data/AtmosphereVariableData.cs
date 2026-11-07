//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.Rendering;

namespace DistantLands.Cozy.EditorScripts.Generation
{
    /// <summary>
    /// Store the data for the atmosphere stack generation
    /// </summary>
    public partial class GenerateVariableStack
    {
        private static readonly LayerData[] layers = new[]
        {
            new LayerData("Lighting", hasShader: false)
                .Has(new FeatureData("Sun", "Calculate the sun lighting in your world using a directional light.", FeatureUsage.ToggleAndCreateOverrides, PerformanceOverhead.Lightweight)
                    .WithColor("LightColor", "Sets the color of the sun light at a certain time. Starts and ends at midnight.",
                        presetValue: "FourStateInterpolated<ColorValue>(preset.SunColor, Color.black, preset.SunColor, preset.SunColor)",
                        disabledValue: Color.white)
                    .WithFloat("LightIntensity", "Sets the intensity of the sun light at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<FloatValue>(preset.LightingIntensity * 2, 0)",
                        disabledValue: 0)
                    .WithEnum<LightShadows>("ShadowType", "Determines the type of shadows used for this light.",
                        presetValue: "Constant<EnumValue<LightShadows>>(LightShadows.Soft)",
                        disabledValue: LightShadows.Soft)
                    .WithFloat("ShadowIntensity", "Set the intensity of the sun light shadows", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(1f)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("SunCloudShadows", "Adds shadows for the volumetric clouds to the sun's light cookie", FeatureUsage.All)
                    .WithFloat("Resolution", "Sets the resolution of the cloud shadow cookie", 
                        presetValue:"Constant<FloatValue>(2048)",
                        disabledValue: 2048)
                    .WithFloat("Size", "Sets the size of the cloud shadow cookie", 
                        presetValue:"Constant<FloatValue>(20000)",
                        disabledValue: 20000)
                    .WithFloat("Steps", "Sets the number of steps used when raymarching the cloud shadow", 
                        presetValue:"Constant<FloatValue>(32)",
                        disabledValue: 32)
                    .WithFloat("BlurRadius", "Sets the radius of the cloud blurring", 
                        presetValue:"Constant<FloatValue>(1)",
                        disabledValue: 1)
                    .WithFloat("Intensity", "Sets the intensity of the shadows", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue:"Constant<FloatValue>(0.5f)",
                        disabledValue: 0.5f)
                )
                .Has(new FeatureData("SunLensFlare", "Draws a lens flare around the sun using Unity's SRP flare system.", FeatureUsage.ToggleAndCreateOverrides, PerformanceOverhead.Lightweight)
                    .WithObject<LensFlareDataSRP>("Reference", "Sets the flare used for the sun at a certain time. Starts and ends at midnight.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Flares/Sun Flare.asset",
                        disabledValue: null)
                    .WithFloat("Intensity", "Sets the intensity of the sun flare at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<FloatValue>(1, 0)",
                        disabledValue: 1)
                    .WithFloat("Scale", "Sets the scale of the sun flare at a certain time. Starts and ends at midnight.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("Moon", "Calculate the moon lighting in your world using a directional light.", FeatureUsage.ToggleAndCreateOverrides, PerformanceOverhead.Lightweight)
                    .WithColor("LightColor", "Sets the color of the moon light at a certain time. Starts and ends at midnight.",
                        presetValue: "Constant<ColorValue>(preset.MoonColor)",
                        disabledValue: Color.white)
                    .WithFloat("LightIntensity", "Sets the intensity of the moon light at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.4f * preset.LightingIntensity)",
                        disabledValue: 0)
                    .WithEnum<LightShadows>("ShadowType", "Determines the method used for the ambient lighting.",
                        presetValue: "Constant<EnumValue<LightShadows>>(LightShadows.Soft)",
                        disabledValue: LightShadows.Soft)
                    .WithFloat("ShadowIntensity", "Set the intensity of the moon light shadows", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("MoonLensFlare", "Draws a lens flare around the moon using Unity's SRP flare system.", FeatureUsage.ToggleAndCreateOverrides, PerformanceOverhead.Lightweight)
                    .WithObject<LensFlareDataSRP>("Reference", "Sets the flare used for the moon at a certain time. Starts and ends at midnight.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Flares/Sun Flare.asset",
                        disabledValue: null)
                    .WithFloat("Intensity", "Sets the intensity of the sun flare at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.2f)",
                        disabledValue: 0.2f)
                    .WithFloat("Scale", "Sets the scale of the sun flare at a certain time. Starts and ends at midnight.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("LensFlareOcclusion", "Should lens flares be occluded by the clouds", FeatureUsage.ToggleOnly, PerformanceOverhead.Normal))
                .Has(new FeatureData("Ambient", "Control the ambient light in your world with COZY.", FeatureUsage.ToggleAndCreateOverrides, PerformanceOverhead.Lightweight)
                    .WithEnum<AmbientLightingType>("AmbientMode", "Determines the method used for the ambient lighting.")
                    .WithColor("ZenithColor", "Sets the color of the sun light at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.32f, 0.75f), AdjustHSV(preset.NightColor, 0, -0.45f, 0.2f))",
                        disabledValue: Color.black)
                    .WithColor("HorizonColor", "Sets the color of the horizon (or middle) of the skybox at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.15f, 0.1f), AdjustHSV(preset.NightColor, 0, -0.15f, 0.1f))",
                        disabledValue: Color.black)
                    .WithColor("GroundColor", "Sets the color of the ground (or bottom) of the skybox at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.5f, -0.3f), AdjustHSV(preset.NightColor, 0, -0.5f, -0.3f))",
                        disabledValue: Color.black)
                    .WithFloat("Intensity", "Sets the intensity of the ambient lighting.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                ),

            new LayerData("Sky", hasShader: true)
                .Has(new FeatureData("Gradient", "Applies a base gradient to your sky.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("ZenithColor", "Sets the color of the zenith (or top) of the skybox at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, 0.22f, -0.15f), AdjustHSV(preset.NightColor, 0, 0.24f, -0.825f))",
                        disabledValue: Color.black)
                    .WithColor("HorizonColor", "Sets the color of the horizon (or middle) of the skybox at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.44f, 0.8f, true), AdjustHSV(preset.NightColor, 0, 0.0f, 0.0f, true))",
                        disabledValue: new Color(0.000f, 0.000f, 0.000f, 1.000f))
                    .WithColor("GroundColor", "Sets the color of the ground (or bottom) of the skybox at a certain time. Starts and ends at midnight.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.4f, -0.4f), AdjustHSV(preset.NightColor, 0, -0.4f, -0.4f))",
                        disabledValue: Color.black)
                    .WithFloat("ZenithExponent", "Controls the exponent used to modulate from the horizon color to the zenith color of the sky.", additionalAttributeProperties: "minFloat = 0.1f, maxFloat = 10f",
                        presetValue: "Constant<FloatValue>(5)",
                        disabledValue: 5)
                    .WithFloat("GroundExponent", "Controls the exponent used to modulate from the horizon color to the ground color of the sky.", additionalAttributeProperties: "minFloat = 0.1f, maxFloat = 10f",
                        presetValue: "Constant<FloatValue>(10)",
                        disabledValue: 5)
                    .WithFloat("Offset", "Controls the vertical position offset of the sky gradient.", additionalAttributeProperties: "minFloat = -1, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0)
                )
                .Has(new FeatureData("TwoTone", "Shades the horizon two different tones based on the sun direction.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("SunSideColor", "Sets the color of the sun side of the horizon at a certain time. Starts and ends at midnight.",
                        presetValue: "DotAccent<ColorValue>(Color.white, Color.Lerp(Color.white, preset.SunColor, 0.25f))",
                        disabledValue: Color.white)
                    .WithColor("AntiSunSideColor", "Sets the color opposite to the sun side of the horizon at a certain time. Starts and ends at midnight.",
                        presetValue: "Constant<ColorValue>(Color.white)",
                        disabledValue: Color.white)
                    .WithFloat("Offset", "Controls the offset used to modulate from the sun side color to the anti-sun side color of the sky.", additionalAttributeProperties: "minFloat = -1f, maxFloat = 1f",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0)
                    .WithFloat("Exponent", "Controls the exponent used to modulate from the sun side color to the anti-sun side color of the sky.", additionalAttributeProperties: "minFloat = 0.1f, maxFloat = 10f",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("Stars", "Draws stars onto your sky using a cubemap.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color", "Sets the color of the star cubemap", 
                        presetValue: "Constant<ColorValue>(Color.white)",
                        disabledValue: Color.white)
                    .WithCubemap("Cubemap", "The cubemap used for the stars.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Star Cubemap.png")
                    .WithFloat("Intensity", "Multiplies the color for the stars by a set value", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 1 * preset.StarBrightness)",
                        disabledValue: 0)
                    .WithFloat("RotationOffset", "Rotates the cubemap around the primary axis. For pitch rotation, use the pitch setting in the COZY Preferences section.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        disabledValue: 0)
                    .WithEnum<StarDomePlacementMode>("Mode", "How often should the star dome rotate?")
                    .WithFloat("TwinkleAmount", "Sets the amount for stars to fade out when twinkling. A twinkle amount of 0 means no fade, and a twinkle amount of 1 fades completely.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0.0f)",
                        disabledValue: 0)
                    .WithFloat("TwinkleSpeed", "Sets the rate at which stars twinkle.",
                        presetValue: "Constant<FloatValue>(3)",
                        disabledValue: 3)
                    .WithCubemap("TwinkleNoise", "Gives a noise offset for how much stars twinkle.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/White Noise.png")
                )
                .Has(new FeatureData("Constellations", "Draws a secondary star cubemap onto your sky to display constellations.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color", "Sets the color of the constellation cubemap. This is multiplied with the star color.", 
                        presetValue: "Constant<ColorValue>(Color.white)",
                        disabledValue: Color.white)
                    .WithCubemap("Cubemap", "The cubemap used for the constellations. Aligned with the stars cubemap. Uses the red channel for the alpha of the constellations",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Constellations Cubemap.tif")
                    .WithFloat("Intensity", "Multiplies the color for the constellations by a set value", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.02f * preset.StarBrightness)",
                        disabledValue: 0f)
                    .WithFloat("ShimmerAmount", "Sets the amount for constellations to fade out one at a time. Constellations use the value in the green channel to determine their offset.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 0)
                    .WithFloat("ShimmerSpeed", "How quickly will constellations fade in and out.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                )
                .Has(new FeatureData("Galaxy", "Draws a dynamically colored galaxy onto your sky using a cubemap.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("PrimaryColor", "Sets the main tint color of the galaxy.", 
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.DayColor, 0.1f, 0.4f, 0.6f))",
                        disabledValue: Color.pink)
                    .WithColor("SecondaryColor", "Sets the secondary accent color of the galaxy.", 
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.DayColor, -0.1f, 0.3f, 0.5f))",
                        disabledValue: Color.skyBlue)
                    .WithColor("TertiaryColor", "Sets the tertiary accent color of the galaxy.", 
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.DayColor, 0.2f, 0.25f, 0.6f))",
                        disabledValue: Color.mediumBlue)
                    .WithFloat("Intensity", "Multiplies the overall intensity of the galaxy.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.5f * preset.StarBrightness)",
                        disabledValue: 0)
                    .WithCubemap("Cubemap", "Set the texture for the galaxy",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Galaxy Cubemap.png")
                )
                .Has(new FeatureData("Rainbow", "Draws a rainbow when the current wetness in greater than 0.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithFloat("Placement", "Controls the rotational offset and position of the rainbow in the sky.", additionalAttributeProperties: "minFloat = -1, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(-0.427f)",
                        disabledValue: -0.427f)
                    .WithTexture2D("Texture", "Sets the rainbow gradient texture.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Rainbow.png")
                    .WithFloat("Width", "Sets the width of the rainbow arc.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                    .WithFloat("Intensity", "Sets the maximum opacity and brightness of the rainbow. This is modulated by the Climate module.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 0)
                )
                .Has(new FeatureData("LightColumns", "Draws a ribbon texture to simulate light scattering on the horizon.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color", "Sets the color of the light columns.", 
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.DayColor, 0.0f, 0.1f, 0f, true))",
                        disabledValue: Color.black)
                    .WithTexture2D("Texture", "Sets the ribbon texture used for light columns.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Light Columns.png")
                    .WithFloat("Intensity", "Sets the brightness intensity of the light columns.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue: "LateNight<FloatValue>(0, 0.5f * preset.StarBrightness)",
                        disabledValue: 0)
                    .WithFloat("Height", "Controls the vertical height scale of the light columns.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                    .WithFloat("Position", "Controls the horizontal positioning of the light columns.", additionalAttributeProperties: "minFloat = -1, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0)
                )
                .Has(new FeatureData("ShootingStars", "Adds shooting stars to your sky.", FeatureUsage.All, PerformanceOverhead.Normal)
                    .WithColor("Color", "Sets the main color of the shooting stars.", 
                        presetValue: "Constant<ColorValue>(Color.white)",
                        disabledValue: Color.white)
                    .WithColor("SecondaryColor", "Sets the secondary trailing color of the shooting stars.", 
                        presetValue: "Constant<ColorValue>(Color.white)",
                        disabledValue: Color.gray7)
                    .WithFloat("Intensity", "Sets the brightness intensity of the shooting stars.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1", 
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.2f * preset.StarBrightness)",
                        disabledValue: 0)
                    .WithFloat("Lifetime", "Controls how long each shooting star remains visible.",
                        presetValue: "Constant<FloatValue>(0.4f)",
                        disabledValue: 0.4f)
                    .WithFloat("ParticleCount", "Sets the maximum number of shooting stars that can spawn concurrently.",
                        presetValue: "Constant<FloatValue>(8)",
                        disabledValue: 8)
                    .WithFloat("Width", "Sets the width of the shooting star trail.",
                        presetValue: "Constant<FloatValue>(1f)",
                        disabledValue: 1)
                    .WithFloat("TravelDistance", "Sets the distance traveled by each shooting star during its lifetime.",
                        presetValue: "Constant<FloatValue>(0.5f)",
                        disabledValue: 0.5f)
                    .WithFloat("Delay", "Sets the delay interval between shooting star spawns.",
                        presetValue: "Constant<FloatValue>(32f)",
                        disabledValue: 32f)
                )
                .Has(new FeatureData("Eclipse", "", FeatureUsage.HLSLOnly, PerformanceOverhead.None)
                    .WithVector3("Direction", "Direction vector pointing toward the eclipse source.")
                    .WithFloat("Size", "How much of the moon covers the sun.")
                    .WithFloat("Progress", "The intensity of the eclipse.")
                )
                .Has(new FeatureData("SunDisk", "Draws a sun texture and halo in the sky.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("HaloColor", "Sets the color of the solar halo.",
                        presetValue: "Constant<ColorValue>(preset.SunColor * 0.5f)",
                        disabledValue: Color.black)
                    .WithFloat("HaloFalloff", "Controls the falloff rate for the solar halo fading outwards.",
                        presetValue: "Constant<FloatValue>(7f)",
                        disabledValue: 7)
                    .WithColor("Color", "Sets the color of the sun disk sprite.",
                        presetValue: "Constant<ColorValue>(preset.SunColor * 1.6f)",
                        disabledValue: Color.black)
                    .WithFloat("Size", "Sets the scale size of the sun disk in the sky.",
                        presetValue: "Constant<FloatValue>(2f)",
                        disabledValue: 2)
                    .WithTexture2D("Texture", "Sets the texture used for rendering the sun disk.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/COZY Sun 1.png")
                )
                .Has(new FeatureData("MoonDisk", "Draws a moon texture and halo in the sky.", FeatureUsage.All, PerformanceOverhead.Normal)
                    .WithEnum<MoonDisplayStyle>("DisplayStyle", "Sets the display style for the moon.")
                    .WithEnum<MoonPlacementMode>("PlacementMode", "Sets the placement mode for the moon.")
                    .WithEnum<MoonPhaseMode>("PhaseMode", "Determines the phase mode used for the moon.")
                    .WithColor("Color", "Sets the color of the moon sprite in the sky.",
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.MoonColor, 0, -0.5f, 0.2f, true))",
                        disabledValue: Color.black)
                    .WithFloat("Size", "Sets the size of the moon sprite in the sky.",
                        presetValue: "Constant<FloatValue>(2f)",
                        disabledValue: 2)
                    .WithFloat("Rotation", "Changes the orientation of the moon sprite in the sky.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1")
                    .WithTexture2D("Texture", "Sets the texture used for the moon.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Moon.png")
                    .WithTexture2D("NormalMap", "Sets the normal map used for the moon.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Moon Bump.png")
                    .WithFloat("NormalStrength", "Sets the strength of the normal map for the moon.",
                        presetValue: "Constant<FloatValue>(0.2f)",
                        disabledValue: 0.2f)
                    .WithFloat("Declination", "Sets the strength of the declination. Declination offsets the moon from the celestial plane (in line with the sun). Moves back and forth following a sine wave.",
                        presetValue: "Constant<FloatValue>(15f)",
                        disabledValue: 15f)
                    .WithFloat("DeclinationPeriod", "Set the number of days between full cycles of declination.",
                        presetValue: "Constant<FloatValue>(278f)",
                        disabledValue: 278)
                    .WithFloat("DeclinationOffset", "Offsets the period for the sine wave of declination.")
                    .WithFloat("OrbitPeriod", "Set the number of days between full cycles the orbit.",
                        presetValue: "Constant<FloatValue>(27.4f)",
                        disabledValue: 27.4f)
                    .WithFloat("OrbitOffset", "Offsets the period for the orbit.")
                ),

            new LayerData("Fog", hasShader: true)
                .Has(new FeatureData("Gradient", "Set the gradient color of the stylized fog.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color1", "Sets the primary fog color band.",
                        presetValue: "Constant<ColorValue>(Color.black)",
                        disabledValue: Color.black)
                    .WithColor("Color2", "Sets the secondary fog color band.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, 0.22f, -0.5f, true), AdjustHSV(preset.NightColor, 0, 0.0f, -0.5f, true))",
                        disabledValue: Color.black)
                    .WithColor("Color3", "Sets the tertiary fog color band.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, 0.12f, -0.2f, true), AdjustHSV(preset.NightColor, 0, 0.0f, -0.2f, true))",
                        disabledValue: Color.black)
                    .WithColor("Color4", "Sets the quaternary fog color band.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.02f, 0.1f, true), AdjustHSV(preset.NightColor, 0, -0.00f, -0.1f, true))",
                        disabledValue: Color.black)
                    .WithColor("Color5", "Sets the quinary fog color band.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.14f, 0.4f, true), AdjustHSV(preset.NightColor, 0, 0.0f, 0.0f, true))",
                        disabledValue: Color.black)
                    .WithFloat("Density1", "Sets the density value for the primary fog band.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0f)
                    .WithFloat("Density2", "Sets the density value for the secondary fog band.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0.2f)",
                        disabledValue: 0f)
                    .WithFloat("Density3", "Sets the density value for the tertiary fog band.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0.5f)",
                        disabledValue: 0f)
                    .WithFloat("Density4", "Sets the density value for the quaternary fog band.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(0.9f)",
                        disabledValue: 0f)
                    .WithFloat("Density5", "Sets the density value for the quinary fog band.", additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 0f)
                    .WithFloat("Start1", "Sets the start distance for the primary fog band.",
                        presetValue: "Constant<FloatValue>(10)",
                        disabledValue: 10f)
                    .WithFloat("Start2", "Sets the start distance for the secondary fog band.",
                        presetValue: "Constant<FloatValue>(50)",
                        disabledValue: 50f)
                    .WithFloat("Start3", "Sets the start distance for the tertiary fog band.",
                        presetValue: "Constant<FloatValue>(100)",
                        disabledValue: 100f)
                    .WithFloat("Start4", "Sets the start distance for the quaternary fog band.",
                        presetValue: "Constant<FloatValue>(160)",
                        disabledValue: 160f)
                    .WithFloat("Start5", "Sets the start distance for the quinary fog band.",
                        presetValue: "Constant<FloatValue>(300)",
                        disabledValue: 300f)
                )
                .Has(new FeatureData("SunLightFlare", "Adds a flare around the sun.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color", "Sets the color tint of the sun fog light flare.",
                        presetValue: "FourStateInterpolated<ColorValue>(AdjustHSV(preset.SunColor, 0, 0.12f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, 0.12f, 0.15f, true), AdjustHSV(preset.MorningColor, 0, 0.12f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, 0.12f, 0.15f, true))",
                        disabledValue: Color.orange)
                    .WithFloat("Squish", "Controls the squish/distortion shape of the light flare.",
                        presetValue: "Constant<FloatValue>(8)",
                        disabledValue: 8)
                    .WithFloat("Intensity", "Sets the intensity brightness of the sun light flare.",
                        presetValue: "FourStateInterpolated<FloatValue>(0f, 0f, 1, 1)",
                        disabledValue: 0)
                    .WithFloat("Falloff", "Controls the falloff rate for the light flare effect.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                    .WithFloat("MinDistance", "Sets the minimum rendering distance for the light flare.",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0)
                    .WithFloat("MaxDistance", "Sets the maximum rendering distance for the light flare.",
                        presetValue: "Constant<FloatValue>(150)",
                        disabledValue: 120)
                )
                .Has(new FeatureData("MoonLightFlare", "Adds a flare around the moon.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("Color", "Sets the color tint of the moon fog light flare.",
                        presetValue: "Constant<ColorValue>(AdjustHSV(preset.MoonColor, 0, 0.32f, -0.35f))",
                        disabledValue: Color.softBlue)
                    .WithFloat("Squish", "Controls the squish/distortion shape of the light flare.",
                        presetValue: "Constant<FloatValue>(1)",
                        disabledValue: 1)
                    .WithFloat("Intensity", "Sets the intensity brightness of the moon light flare.",
                        presetValue: "TwoStateInterpolated<FloatValue>(0, 0.35f)",
                        disabledValue: 0)
                    .WithFloat("Falloff", "Controls the falloff rate for the light flare effect.",
                        presetValue: "Constant<FloatValue>(15)",
                        disabledValue: 15)
                    .WithFloat("MinDistance", "Sets the minimum rendering distance for the light flare.",
                        presetValue: "Constant<FloatValue>(75)",
                        disabledValue: 75)
                    .WithFloat("MaxDistance", "Sets the maximum rendering distance for the light flare.",
                        presetValue: "Constant<FloatValue>(150)",
                        disabledValue: 150)
                )
                .Has(new FeatureData("Volumetric", "Adds volumetric raymarching to the fog.", FeatureUsage.ToggleOnly, PerformanceOverhead.Heavy)
                )
                .Has(new FeatureData("TwoTone", "Splits the fog into two colors based on the sun direction.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithColor("SunSideColor", "Sets the fog color facing toward the sun direction.",
                        disabledValue: Color.white)
                    .WithColor("AntiSunSideColor", "Sets the fog color facing away from the sun direction.",
                        disabledValue: Color.white)
                    .WithFloat("SunSideDensity", "Controls the density of the fog facing toward the sun direction.", additionalAttributeProperties: "minFloat = 0, maxFloat = 3",
                        disabledValue: 1)
                    .WithFloat("AntiSunSideDensity", "Controls the density of the fog facing away from the sun direction.", additionalAttributeProperties: "minFloat = 0, maxFloat = 3",
                        disabledValue: 1)
                    .WithFloat("Smoothness", "Controls the blend smoothness between sun side and moon side fog.",
                        disabledValue: 1)
                    .WithFloat("Offset", "Controls the directional offset separating the sun and moon fog colors.",
                        disabledValue: 0)
                )
                .Has(new FeatureData("Height", "Adds height fog to your world.", FeatureUsage.All, PerformanceOverhead.Normal)
                    .WithColor("Color", "Sets the color tint of the height fog.",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.80f, 0.25f), AdjustHSV(preset.NightColor, 0, -0.80f, 0.1f))")
                    .WithFloat("Falloff", "Controls how quickly height fog dissipates vertically.",
                        presetValue: "Constant<FloatValue>(5)")
                    .WithFloat("Smoothness", "Controls the softness transition for height fog bounds.",
                        presetValue: "Constant<FloatValue>(2)")
                    .WithFloat("BaseHeight", "Sets the base elevation plane where height fog starts.",
                        presetValue: "Constant<FloatValue>(15)")
                    .WithFloat("Density", "Sets the overall density of the height fog layer.",
                        presetValue: "Constant<FloatValue>(0.1f)",
                        disabledValue: 0)
                    .WithTexture3D("VariationNoise", "Set the texture used for noise variation.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/3D Cloud Noise.asset")
                    .WithFloat("VariationAmount", "Control the amount of variation", 
                        additionalAttributeProperties: "minFloat = 0, maxFloat = 1",
                        disabledValue:0.5f,
                        presetValue: "Constant<FloatValue>(0.5f)")
                    .WithVector3("VariationTextureScale", "Sets the scale of the height fog variation",
                        presetValue: "Constant<Vector3Value>(new Vector3(1f, 1f, 1f))")
                    .WithVector3("VariationTextureFlow", "Sets the movement speed and direction of the height fog variation",
                        presetValue: "Constant<Vector3Value>(new Vector3(0.3f, 0.1f, 0))")
                )
                .Has(new FeatureData("Base", "Control the base variables of the fog.", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithFloat("DensityMultiplier", "Global multiplier applied to overall fog density.",
                        presetValue: "Constant<FloatValue>(1 * preset.Fogginess)",
                        disabledValue: 0)
                    .WithFloat("Height", "Sets the base elevation height for fog in the background.",
                        presetValue: "Constant<FloatValue>(0)",
                        additionalAttributeProperties: "minFloat = -1, maxFloat = 1",
                        disabledValue: 0)
                    .WithFloat("Smoothness", "Controls the transition smoothness of the fog in the background.",
                        presetValue: "Constant<FloatValue>(2)",
                        disabledValue: 7)
                    .WithFloat("BaseHeight", "Sets the base height (in meters) for the foreground fog.",
                        presetValue: "Constant<FloatValue>(0)",
                        disabledValue: 0)
                    .WithFloat("BaseSmoothness", "Sets the base smoothness (in meters) for the foreground fog.",
                        presetValue: "Constant<FloatValue>(100)",
                        disabledValue: 100)
                    .WithFloat("TransitionDistance", "Sets the distance (in meters) that the fog will transition from the height-based fog (foreground) to the angle-based fog (background).",
                        presetValue: "Constant<FloatValue>(650)",
                        disabledValue: 650)
                )
                .Has(new FeatureData("Layers", "", FeatureUsage.HLSLOnly, PerformanceOverhead.Lightweight)
                    .WithFloat("SkyFogAmount", "Controls how much fog affects the sky background layer.")
                    .WithFloat("SkyLightAmount", "Controls how much lighting affects sky fog interactions.")
                    .WithFloat("CloudsFogAmount", "Controls the influence of fog over cloud rendering layers.")
                    .WithFloat("CloudsLightAmount", "Controls how much light passes through fog into cloud layers.")
                )
                ,

            new LayerData("Clouds", hasShader: true)
                .Has(new FeatureData("Volumetric", "Adds volumetric support to the clouds", FeatureUsage.All, PerformanceOverhead.Heavy)
                    .UseShader("VolumetricClouds")
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement. Textures are packed such that the B value determines the cloud density",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png")
                    .WithTexture3D("NoiseTexture", "Sets the texture volumetric for erosion and detailing",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/3D Cloud Noise.asset")
                    .WithFloat("Height", "Sets the height of the cumulus layer",
                        presetValue: "Constant<FloatValue>(1300)")
                    .WithFloat("DarkeningIntensity", "Darkens the cloud layer in thick clouds",
                        presetValue: "Constant<FloatValue>(1.5f)")
                    .WithColor("LitColor", "Sets the color of the lit clouds",
                        presetValue: "FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.80f, 0.25f), AdjustHSV(preset.NightColor, 0, -0.40f, -0.1f), AdjustHSV(preset.MorningColor, 0, -0.22f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.22f, 0.15f, true))")
                    .WithColor("ShadeColor", "Sets the color of the shaded clouds",
                        presetValue: "TwoStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.50f, -0.05f), AdjustHSV(preset.NightColor, 0, -0.20f, 0.05f))")
                    .WithFloat("PlacementScale1", "Sets the primary scale for volumetric cloud placement mask.",
                        presetValue: "Constant<FloatValue>(1.5f)")
                    .WithFloat("PlacementScale2", "Sets the secondary scale for volumetric cloud placement mask.",
                        presetValue: "Constant<FloatValue>(0.82f)")
                    .WithFloat("Density", "Sets the density of the volumetric layer",
                        presetValue: "Constant<FloatValue>(2)")
                    .WithFloat("NoiseScale", "Sets the scale of the noise for the volumetric layer",
                        presetValue: "Constant<FloatValue>(7)")
                    .WithFloat("NoiseAmount", "Controls the strength of noise erosion on cloud edges.",
                        presetValue: "Constant<FloatValue>(1.5f)")
                    .WithFloat("PlacementHeight", "Sets the base elevation height where volumetric cloud rendering starts.",
                        presetValue: "Constant<FloatValue>(1500)")
                    .WithTexture2D("Ramp", "Ramp texture driving height-based density and lighting gradients.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Fluffy Clouds Density Ramp.png")
                    .WithFloat("StepSize", "Sets the raymarching sample step distance inside the clouds.",
                        presetValue: "Constant<FloatValue>(16)")
                    .WithFloat("Steps", "Sets the total number of raymarching sample iterations.",
                        presetValue: "Constant<FloatValue>(64)")
                    .WithFloat("LODCoefficient", "Controls the distance-based level of detail reduction factor.",
                        presetValue: "Constant<FloatValue>(4)")
                    .WithFloat("LODSteps", "Sets the reduced raymarching step count applied at distance.",
                        presetValue: "Constant<FloatValue>(3)")
                    .WithTexture2D("BlueNoise", "Blue noise texture used for jittering raymarch offsets to reduce banding.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Blue Noise.png")
                    .WithFloat("WindStrength", "Controls how strongly wind speeds move the volumetric cloud noise.",
                        presetValue: "Constant<FloatValue>(1)")
                    .WithFloat("HighlightBrightness", "Sets the intensity brightness of light highlights along cloud edges.",
                        presetValue: "Constant<FloatValue>(0.6f)")
                    .WithFloat("HighlightStepSize", "Sets the raymarching step size for computing light highlights.",
                        presetValue: "Constant<FloatValue>(75)")
                )
                .Has(new FeatureData("VolumetricHighlights", "Adds raymarched lighting to the clouds. Requires volumetric clouds", FeatureUsage.ToggleOnly, PerformanceOverhead.Heavy)
                    .UseShader("VolumetricClouds"))
                .Has(new FeatureData("VolumetricSelfShadowing", "Adds raymarched self-shadowing to the clouds. Requires volumetric clouds", FeatureUsage.ToggleOnly, PerformanceOverhead.Heavy)
                    .UseShader("VolumetricClouds"))
                .Has(new FeatureData("Cumulonimbus", "Massive dark storm clouds that form on the horizon. Requires volumetric clouds", FeatureUsage.All, PerformanceOverhead.Heavy)
                    .UseShader("VolumetricClouds")
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement. Textures are packed such that the B value determines the cloud density",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png")
                    .WithTexture2D("Ramp", "Ramp texture driving height-based density and lighting gradients.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Nimbus Clouds Density Ramp.png")
                )
                .Has(new FeatureData("Cumulus", "Draws a 2D cloud layer based on the properties of cumulus clouds", FeatureUsage.All, PerformanceOverhead.Normal)
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement. Textures are packed such that the B value determines the cloud density",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png")
                    .WithTexture2D("ErosionTexture", "Sets the texture used for erosion and detailing",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/GeneratedNoise Normal.png")
                    .WithFloat("Height", "Sets the height of the cumulus layer",
                        presetValue: "Constant<FloatValue>(8000)")
                    .WithColor("LitColor", "Sets the color of the lit clouds",
                        presetValue: "FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.80f, 0.25f), AdjustHSV(preset.NightColor, 0, -0.40f, -0.1f), AdjustHSV(preset.MorningColor, 0, -0.32f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.32f, 0.15f, true))")
                    .WithColor("ShadeColor", "Sets the color of the shaded clouds",
                        presetValue: "FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.50f, -0.05f), AdjustHSV(preset.NightColor, 0, -0.20f, 0.05f), AdjustHSV(preset.MorningColor, 0, -0.32f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.32f, 0.15f, true))")
                    .WithFloat("Thickness", "Sets the thickness of the cumulus layer for fake lighting",
                        presetValue: "Constant<FloatValue>(1)")
                    .WithFloat("Density", "Sets the density of the cumulus layer",
                        presetValue: "Constant<FloatValue>(2)")
                    .WithFloat("WindStrength", "Controls the movement speed of the cumulus cloud layer based on wind.",
                        presetValue: "Constant<FloatValue>(1)")
                    .WithFloat("Cohesion", "Sets the cohesion of the cumulus layer",
                        presetValue: "Constant<FloatValue>(0.8f)")
                )
                .Has(new FeatureData("Altocumulus", "Draws a 2D cloud layer based on the properties of altocumulus clouds", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Altocumulus.png")
                    .WithFloat("Height", "Sets the height of the layer",
                        presetValue: "Constant<FloatValue>(13000)")
                    .WithVector3("TextureScale", "Sets the scale of the cirrus texture",
                        presetValue:"Constant<Vector3Value>(new Vector3(1,1,1))")
                    .WithColor("Color", "Sets the color of the clouds",
                        presetValue:"FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.52f, 0.65f, true), AdjustHSV(preset.NightColor, 0, -0.32f, -0.25f, true), AdjustHSV(preset.MorningColor, 0, -0.32f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.32f, 0.15f, true))")
                    .WithFloat("Density", "Sets the density of the layer",
                        presetValue: "Constant<FloatValue>(1.5f)")
                    .WithFloat("Ramp", "Controls the power for each individual cloud.",
                        presetValue: "Constant<FloatValue>(2.5f)")
                    .WithFloat("WindStrength", "Controls the movement speed of the layer based on wind.",
                        presetValue: "Constant<FloatValue>(1)")
                )
                .Has(new FeatureData("Cirrus", "Draws a 2D cloud layer based on the properties of cirrus clouds", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement. Textures are packed such that the B value determines the cloud density",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Cirrus Clouds.png")
                    .WithFloat("Height", "Sets the height of the layer",
                        presetValue: "Constant<FloatValue>(15000)")
                    .WithVector3("TextureScale", "Sets the scale of the texture",
                        presetValue:"Constant<Vector3Value>(new Vector3(0.5f, 0.5f, 0.5f))")
                    .WithColor("Color", "Sets the color of the clouds",
                        presetValue:"FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.52f, 0.35f, true), AdjustHSV(preset.NightColor, 0, -0.52f, -0.15f, true), AdjustHSV(preset.MorningColor, 0, -0.32f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.32f, 0.15f, true))")
                    .WithFloat("Density", "Sets the density of the layer",
                        presetValue: "Constant<FloatValue>(1.25f)")
                    .WithFloat("WindStrength", "Controls the movement speed of the cloud layer based on wind.",
                        presetValue: "Constant<FloatValue>(1)")
                    .WithFloat("WispFactor", "Sets the power of the layer",
                        presetValue: "Constant<FloatValue>(1.5f)")
                )
                .Has(new FeatureData("Contrails", "Add 2D contrails to your skybox", FeatureUsage.All, PerformanceOverhead.Lightweight)
                    .WithTexture2D("MainTexture", "Sets the main texture used for placement.",
                        defaultValue: "Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Contrails.png")
                    .WithFloat("Height", "Sets the height of the layer",
                        presetValue: "Constant<FloatValue>(12000)")
                    .WithVector3("TextureScale", "Sets the scale of the texture",
                        presetValue:"Constant<Vector3Value>(new Vector3(1,1,1))")
                    .WithColor("Color", "Sets the color of the lit clouds",
                        presetValue:"FourStateInterpolated<ColorValue>(AdjustHSV(preset.DayColor, 0, -0.52f, 0.35f, true), AdjustHSV(preset.NightColor, 0, -0.52f, -0.15f, true), AdjustHSV(preset.MorningColor, 0, -0.32f, 0.15f, true), AdjustHSV(preset.EveningColor, 0, -0.32f, 0.15f, true))")
                    .WithFloat("Density", "Sets the density of the layer",
                        presetValue: "Constant<FloatValue>(1)")
                    .WithFloat("WindStrength", "Controls the movement speed of the cloud layer based on wind.",
                        presetValue: "Constant<FloatValue>(1)")
                ),

            new LayerData("Sunshafts", hasShader: true)
                .Has(new FeatureData("Screenspace", "Adds a sunshafts post effect to your world.", FeatureUsage.All, PerformanceOverhead.Normal)
                    .WithColor("Color", "Set the color of the sunshafts",
                        presetValue: "Constant<ColorValue>(preset.SunColor)")
                    .WithFloat("Intensity", "Set the brightness of the sunshafts", additionalAttributeProperties:"minFloat = 0, maxFloat = 1",
                        presetValue:"TwoStateInterpolated<FloatValue>(0.25f, 0)")
                    .WithFloat("Length", "Blurs the sunshafts to sets the length", additionalAttributeProperties:"minFloat = 0, maxFloat = 1",
                        presetValue:"Constant<FloatValue>(0.8f)")
                    .WithFloat("Steps", "Set the step count",
                        presetValue:"Constant<FloatValue>(32)")
                    .WithFloat("CloudMaskAmount", "How strongly do clouds occlude sunrays", additionalAttributeProperties:"minFloat = 0, maxFloat = 1",
                        presetValue:"Constant<FloatValue>(1.0f)")
                ),

            new LayerData("Base", hasShader: false)
                .Has(new FeatureData("Orientation", "", FeatureUsage.HLSLOnly, PerformanceOverhead.None)
                    .WithVector3("SunDirection", "Direction vector pointing toward the primary directional light source.")
                    .WithVector3("MoonDirection", "Direction vector pointing toward the secondary directional light source.")
                    .WithVector3("North", "World vector representing North orientation.")
                    .WithVector3("West", "World vector representing West orientation.")
                    .WithVector3("CameraPosition", "Current camera position vector passed into shader operations.")
                    .WithVector3("StarDomeRotation", "Euler rotation angles applied to the star dome sky cubemap.")
                    .WithFloat("SkyRotation", "Yaw rotation of the skybox in degrees.", additionalAttributeProperties: "minFloat = 0, maxFloat = 360")
                    .WithFloat("SkyPitch", "Pitch rotation angle of the skybox in degrees.", additionalAttributeProperties: "minFloat = -90, maxFloat = 90")
                )
                .Has(new FeatureData("Weather", "", FeatureUsage.HLSLOnly, PerformanceOverhead.None)
                    .WithFloat("CumulusCoverage", "Controls the global coverage amount of cumulus clouds.")
                    .WithFloat("CumulonimbusCoverage", "Controls the global coverage amount of cumulonimbus storm clouds.")
                    .WithFloat("StratusCoverage", "Controls the global coverage amount of low stratus clouds.")
                    .WithFloat("CirrusCoverage", "Controls the global coverage amount of high cirrus clouds.")
                    .WithFloat("AltocumulusCoverage", "Controls the global coverage amount of mid-level altocumulus clouds.")
                    .WithFloat("CirrostratusCoverage", "Controls the global coverage amount of high cirrostratus clouds.")
                    .WithFloat("ContrailCoverage", "Controls the global coverage amount of vapor contrails.")
                    .WithFloat("BorderCoverage", "Controls horizon cloud border blending coverage.")
                    .WithFloat("FogCoverage", "Controls overall atmospheric fog weather density.")
                    .WithFloat("FogHeightCoverage", "Controls vertical height fog coverage limits.")
                    .WithFloat("SaturationFilter", "Color saturation adjustment filter applied globally.")
                    .WithFloat("ValueFilter", "Brightness value adjustment filter applied globally.")
                    .WithColor("FilterColor", "Tint color filter applied to the rendered scene.")
                    .WithColor("SunFilter", "Color filter applied specifically to direct sunlight.")
                    .WithColor("CloudFilter", "Color filter applied specifically to cloud rendering.")
                    .WithVector3("CloudWindOffset", "3D movement offset vector driven by wind velocity.")
                    .WithFloat("Wetness", "Ground and surface wetness factor driven by rain weather.")
                    .WithFloat("Snow", "Snow accumulation factor on scene surfaces.")
                )
                .Has(new FeatureData("Time", "", FeatureUsage.HLSLOnly, PerformanceOverhead.None)
                    .WithFloat("DayPercentage", "Normalized value (0 to 1) representing progression through the day cycle.")
                    .WithFloat("YearPercentage", "Normalized value (0 to 1) representing progression through the seasonal year cycle.")
                    .WithFloat("MoonPhasePercentage", "Normalized value (0 to 1) representing the lunar phase state.")
                    .WithFloat("MoonPhaseSign", "-1 if waxing, +1 if waning. 0 if full moon or new moon")
                )
                .Has(new FeatureData("Lighting", "", FeatureUsage.HLSLOnly, PerformanceOverhead.None)
                    .WithColor("SunColor", "Computed active solar illumination color.")
                    .WithColor("MoonColor", "Computed active lunar illumination color.")
                )
        };
    }
}