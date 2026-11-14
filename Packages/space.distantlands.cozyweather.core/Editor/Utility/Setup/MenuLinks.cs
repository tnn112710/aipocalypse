//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    public class MenuLinks
    {
        [MenuItem("Tools/COZY: Weather 4/Toggle Tooltips", false, 300)]
        public static void ToggleTooltips()
        {
            EditorPrefs.SetBool("CZY_Tooltips", !EditorPrefs.GetBool("CZY_Tooltips"));
        }

        [MenuItem("Tools/COZY: Weather 4/Documentation", false, 1000)]
        public static void OpenDocs()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4");
        }


        [MenuItem("Tools/COZY: Weather 4/Discord", false, 1000)]
        public static void OpenDiscord()
        {
            Application.OpenURL("https://discord.gg/eZXxGCp9ww");
        }

        [MenuItem("Tools/COZY: Weather 4/FAQs", false, 1000)]
        public static void OpenFAQs()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/appendix/faqs");
        }
        [MenuItem("Tools/COZY: Weather 4/Integrations", false, 200)]
        public static void OpenIntegrations()
        {
            SettingsService.OpenProjectSettings("Project/COZY: Stylized Weather 4/Integrations");
        }
    }
}