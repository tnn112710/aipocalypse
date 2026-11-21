//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    [Serializable]
    [CozyIntegration(false, true, "Dynamic Stylized Water", "COZY Icons/Polyart Water", "https://assetstore.unity.com/packages/vfx/shaders/dynamic-stylized-water-all-in-one-stylized-water-system-431230")]
    public class CozyDynamicStylizedWaterIntegration : CozyIntegration
    {
        private const string OceanPassGuid = "695ae2caf8361204b9c8ca032e190430";
        private const string WorldPositionBlend = "colour = BlendStylizedFog(float4(colour.rgb, 1), positionWS).rgb;";
        private const string InputPositionBlend = "colour = BlendStylizedFog(float4(colour.rgb, 1), input.positionWS).rgb;";
        private const string FoamBlend = "colour.rgb = BlendStylizedFog(float4(colour.rgb, 1), input.positionWS).rgb;";

        private static readonly string[] PassNames =
        {
            "OceanPass.hlsl", "WaterLakePass.hlsl", "WaterRiverPass.hlsl",
            "WaterWaterfallPass.hlsl", "WaterWaterfallFoamPass.hlsl", "WaterWaterfallFoamWavesPass.hlsl"
        };

        private static readonly string[] BlendLines =
        {
            WorldPositionBlend, WorldPositionBlend, WorldPositionBlend,
            WorldPositionBlend, FoamBlend, InputPositionBlend
        };

        private static string IncludeDirectory
        {
            get
            {
#if UNITY_EDITOR
                string path = AssetDatabase.GUIDToAssetPath(OceanPassGuid);
                return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path).Replace('\\', '/');
#else
                return null;
#endif
            }
        }

        public override bool PackageInstalled => !string.IsNullOrEmpty(IncludeDirectory);

        public override bool IntegrationInstalled
        {
            get
            {
                string directory = IncludeDirectory;
                if (string.IsNullOrEmpty(directory))
                    return false;

                for (int i = 0; i < PassNames.Length; i++)
                    if (!HlslPatcher.Installed(directory + "/" + PassNames[i], BlendLines[i]))
                        return false;
                return true;
            }
        }

        public override StatusType CheckStatus(out string message)
        {
            message = "";
            return StatusType.Pass;
        }

        public override void Initialize() { }
        public override void Deinitialize() { }

        public override void ApplyIntegration()
        {
            string directory = IncludeDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                UnityEngine.Debug.LogError("Dynamic Stylized Water's Ocean pass was not found.");
                return;
            }

            for (int i = 0; i < PassNames.Length; i++)
                HlslPatcher.PatchShader(directory + "/" + PassNames[i], BlendLines[i]);
        }

        public override void UnapplyIntegration()
        {
            string directory = IncludeDirectory;
            if (string.IsNullOrEmpty(directory))
            {
                UnityEngine.Debug.LogError("Dynamic Stylized Water's Ocean pass was not found.");
                return;
            }

            for (int i = 0; i < PassNames.Length; i++)
                HlslPatcher.UnpatchShader(directory + "/" + PassNames[i], BlendLines[i]);
        }
    }
}
