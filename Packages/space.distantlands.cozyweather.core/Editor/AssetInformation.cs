//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class AssetInformation
    {
        public const string PACKAGE_ID = "space.distantlands.cozyweather.core";
        public const string ASSET_ID = "389284";
        public const string ASSET_NAME = "COZY: Weather 4";
        public static Version INSTALLED_VERSION;
        public static Version UAS_VERSION;
        public const string INTEGRATION_PATH = "Packages/space.distantlands.cozyweather.core/Content/Integration/";

    }
}