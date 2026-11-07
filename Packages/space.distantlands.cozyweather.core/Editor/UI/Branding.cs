//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{

#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public static class Branding
    {
        public static Color red = new Color(0.937f, 0.278f, 0.435f, 1f);
        public static Color deepRed = new Color(0.937f, 0.178f, 0.235f, 1f);
        public static Color yellow = new Color(1.0f, 0.820f, 0.400f, 1f);
        public static Color orange = new Color(1.0f, 0.620f, 0.305f, 1f);
        public static Color green = new Color(0.024f, 0.839f, 0.627f, 1f);
        public static Color deepGreen = new Color(0.024f, 0.639f, 0.427f, 1f);
        public static Color blue = new Color(0.265f, 0.665f, 0.902f, 1f);
        public static Color deepBlue = new Color(0.0f, 0.435f, 0.829f, 1f);
        public static Color purple = new Color(0.392f, 0.314f, 0.471f, 1f);
        public static Color pink = new Color(0.722f, 0.490f, 0.741f, 1.000f);
        public static Color charcoal = new Color(0.149f, 0.329f, 0.486f, 1f);
        public static Color white = new Color(0.853f, 0.853f, 0.853f, 1f);
        public static Color whiteAccent = new Color(1f, 1f, 1f, 0.25f);
        public static Color lightGreyAccent = new Color(1f, 1f, 1f, 0.1f);
        public static Color darkGreyAccent = new Color(0f, 0f, 0f, 0.1f);
        public static Color darkAccent = new Color(0f, 0f, 0f, 0.25f);
    }
}