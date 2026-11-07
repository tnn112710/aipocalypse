//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
        [System.Serializable]
        public class DefaultAtmosphereReferences : ScriptableObject
        {

        public LensFlareDataSRP defaultLightingSunLensFlareReference;
        public LensFlareDataSRP defaultLightingMoonLensFlareReference;
        public Cubemap defaultSkyStarsCubemap;
        public Cubemap defaultSkyStarsTwinkleNoise;
        public Cubemap defaultSkyConstellationsCubemap;
        public Cubemap defaultSkyGalaxyCubemap;
        public Texture2D defaultSkyRainbowTexture;
        public Texture2D defaultSkyLightColumnsTexture;
        public Texture2D defaultSkySunDiskTexture;
        public Texture2D defaultSkyMoonDiskTexture;
        public Texture2D defaultSkyMoonDiskNormalMap;
        public Texture3D defaultFogHeightVariationNoise;
        public Texture2D defaultCloudsVolumetricMainTexture;
        public Texture3D defaultCloudsVolumetricNoiseTexture;
        public Texture2D defaultCloudsVolumetricRamp;
        public Texture2D defaultCloudsVolumetricBlueNoise;
        public Texture2D defaultCloudsCumulonimbusMainTexture;
        public Texture2D defaultCloudsCumulonimbusRamp;
        public Texture2D defaultCloudsCumulusMainTexture;
        public Texture2D defaultCloudsCumulusErosionTexture;
        public Texture2D defaultCloudsAltocumulusMainTexture;
        public Texture2D defaultCloudsCirrusMainTexture;
        public Texture2D defaultCloudsContrailsMainTexture;


#if UNITY_EDITOR
                public void AssignDefaults()
                {
          defaultLightingSunLensFlareReference = (LensFlareDataSRP)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Flares/Sun Flare.asset", typeof(LensFlareDataSRP));
          defaultLightingMoonLensFlareReference = (LensFlareDataSRP)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Flares/Sun Flare.asset", typeof(LensFlareDataSRP));
          defaultSkyStarsCubemap = (Cubemap)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Star Cubemap.png", typeof(Cubemap));
          defaultSkyStarsTwinkleNoise = (Cubemap)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/White Noise.png", typeof(Cubemap));
          defaultSkyConstellationsCubemap = (Cubemap)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Constellations Cubemap.tif", typeof(Cubemap));
          defaultSkyGalaxyCubemap = (Cubemap)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Galaxy Cubemap.png", typeof(Cubemap));
          defaultSkyRainbowTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Rainbow.png", typeof(Texture2D));
          defaultSkyLightColumnsTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Light Columns.png", typeof(Texture2D));
          defaultSkySunDiskTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/COZY Sun 1.png", typeof(Texture2D));
          defaultSkyMoonDiskTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Moon.png", typeof(Texture2D));
          defaultSkyMoonDiskNormalMap = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Stars & Night FX/Moon Bump.png", typeof(Texture2D));
          defaultFogHeightVariationNoise = (Texture3D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/3D Cloud Noise.asset", typeof(Texture3D));
          defaultCloudsVolumetricMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png", typeof(Texture2D));
          defaultCloudsVolumetricNoiseTexture = (Texture3D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/3D Cloud Noise.asset", typeof(Texture3D));
          defaultCloudsVolumetricRamp = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Fluffy Clouds Density Ramp.png", typeof(Texture2D));
          defaultCloudsVolumetricBlueNoise = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Blue Noise.png", typeof(Texture2D));
          defaultCloudsCumulonimbusMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png", typeof(Texture2D));
          defaultCloudsCumulonimbusRamp = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Nimbus Clouds Density Ramp.png", typeof(Texture2D));
          defaultCloudsCumulusMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Generated Cloud Noise.png", typeof(Texture2D));
          defaultCloudsCumulusErosionTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/GeneratedNoise Normal.png", typeof(Texture2D));
          defaultCloudsAltocumulusMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Altocumulus.png", typeof(Texture2D));
          defaultCloudsCirrusMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/VFX Graph/Textures/Cirrus Clouds.png", typeof(Texture2D));
          defaultCloudsContrailsMainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/space.distantlands.cozyweather.core/Content/Textures/Cloud Textures/Contrails.png", typeof(Texture2D));

                }
#endif
        }
}