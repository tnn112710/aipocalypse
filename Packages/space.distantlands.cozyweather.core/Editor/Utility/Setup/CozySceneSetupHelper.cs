//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace DistantLands.Cozy.EditorScripts
{
    public class CozySceneSetupHelper : Editor
    {
        [MenuItem("Tools/COZY: Weather 4/Setup Scene (No Modules)", priority = 10)]
        public static void SetupSceneNoModules()
        {
            RunSetupChecks();
            SpawnCozyPrefab();
        }
        [MenuItem("Tools/COZY: Weather 4/Setup Scene (Basic Modules)", priority = 10)]
        public static void SetupSceneBasicModules()
        {
            RunSetupChecks();
            CozyWeather weather = SpawnCozyPrefab();

            weather.AddModule<CozyAtmosphereModule>();
            CozyTimeModule timemodule = weather.AddModule<CozyTimeModule>();
            timemodule.StaticEvents = new List<StaticEvent>();
            foreach (object obj in AssetDatabase.LoadAllAssetsAtPath("Packages/space.distantlands.cozyweather.core/Content/Profiles/Meridiem Events"))
            {
                if (obj is StaticEvent)
                    timemodule.StaticEvents.Add(obj as StaticEvent);
            }
            weather.AddModule<CozyWeatherModule>();
            weather.AddModule<CozyClimateModule>();
        }

        /// <summary>
        /// Dynamically build COZY prefab instead of relying on prefab system (we can't trust it)
        /// </summary>
        private static CozyWeather SpawnCozyPrefab()
        {
            GameObject gameObject = new("COZY Weather");
            CozyWeather cozyWeather = gameObject.AddComponent<CozyWeather>();

            GameObject sunLightGO = new("Sun Light");
            sunLightGO.transform.localEulerAngles = new Vector3(45, 0, 0);
            Light sunLight = sunLightGO.AddComponent<Light>();
            sunLight.bounceIntensity = 0;
            sunLight.type = LightType.Directional;
            sunLightGO.transform.parent = gameObject.transform;

            GameObject moonLightGO = new("Moon Light");
            Light moonLight = moonLightGO.AddComponent<Light>();
            moonLight.enabled = false;
            moonLight.bounceIntensity = 0;
            moonLight.type = LightType.Directional;
            moonLightGO.transform.parent = gameObject.transform;

            GameObject sunLightFlareGO = new("Sun Flare");
            Light sunFlareLight = sunLightFlareGO.AddComponent<Light>();
            sunFlareLight.type = LightType.Directional;
            sunFlareLight.bounceIntensity = 0;
            sunFlareLight.enabled = false;
            LensFlareComponentSRP sunFlare = sunLightFlareGO.AddComponent<LensFlareComponentSRP>();
            sunFlare.enabled = false;
            sunFlare.lightOverride = sunFlareLight;
            sunFlare.useOcclusion = true;
            sunLightFlareGO.transform.parent = gameObject.transform;

            GameObject moonLightFlareGO = new("Moon Flare");
            Light moonFlareLight = moonLightFlareGO.AddComponent<Light>();
            moonFlareLight.type = LightType.Directional;
            moonFlareLight.bounceIntensity = 0;
            moonFlareLight.enabled = false;
            LensFlareComponentSRP moonFlare = moonLightFlareGO.AddComponent<LensFlareComponentSRP>();
            moonFlare.lightOverride = moonFlareLight;
            moonFlare.useOcclusion = true;
            moonLightFlareGO.transform.parent = gameObject.transform;

            GameObject windZoneGO = new("Wind Zone");
            windZoneGO.transform.parent = gameObject.transform;
            windZoneGO.AddComponent<WindZone>();

            return cozyWeather;
        }

        private static void RunSetupChecks()
        {
            var projectSetupStatus = CozyProjectSetup.CheckForSetup();
            if (projectSetupStatus == StatusType.Fail || projectSetupStatus == StatusType.Warning)
            {
                Debug.Log("Project not fully setup for COZY. Results may not be correct.");
            }
            if (CheckForDirectionalLight(out List<Light> directionalLights))
            {
                if (EditorUtility.DisplayDialog($"Directional Light(s) found",
                                                "Would you like to automatically delete the directional lights? COZY will handle your scene lighting for you.",
                                                "Yes (Delete)",
                                                "No (Keep)"))
                {
                    foreach (Light directionalLight in directionalLights)
                    {
                        DestroyImmediate(directionalLight.gameObject);
                    }
                }
                else if (EditorUtility.DisplayDialog($"Scene Lighting",
                                                "Would you like to disable scene lighting for COZY? This is done globally in COZY Preferences and will prevent COZY from modifying your scene lighting at all.",
                                                "Disable Scene Lighting Control",
                                                "Leave Enabled"))
                {
                    CozyPreferences.HandleSceneLighting = false;
                    Debug.Log("Scene lighting disabled. You can re-enable this in the preferences if you want COZY to light your scene.");
                }
            }
        }

        private static bool CheckForDirectionalLight(out List<Light> directionalLights)
        {
            directionalLights = new();

            #if UNITY_6000_4_OR_NEWER
            Light[] lights = FindObjectsByType<Light>();
            #else
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            #endif

            foreach (Light light in lights)
                if (light.type == LightType.Directional)
                {
                    directionalLights.Add(light);
                }

            return directionalLights.Count > 0;
        }


        [MenuItem("Tools/COZY: Weather 4/Create Local Biome", priority = 50)]
        [MenuItem("GameObject/Distant Lands/COZY: Weather 4/Create Local Biome")]
        public static void CreateLocalBiome()
        {
            GameObject gameObject = new("New Biome");
            CozyBiome biome = gameObject.AddComponent<CozyBiome>();
            biome.biomeType = BiomeType.Local;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 10;
        }

        [MenuItem("Tools/COZY: Weather 4/Create Global Biome", priority = 50)]
        [MenuItem("GameObject/Distant Lands/COZY: Weather 4/Create Global Biome")]
        public static void CreateGlobalBiome()
        {
            GameObject gameObject = new("New Biome");
            CozyBiome biome = gameObject.AddComponent<CozyBiome>();
            biome.biomeType = BiomeType.Global;
        }
    }
}