using UnityEngine;
using UnityEditor; // Added for EditorGUI
using UnityEngine.Rendering; // Added for VolumeProfile
using System.Linq;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Shared utility methods for Kroma lighting operations.
    /// </summary>
    public static class KromaLightingUtils
    {
        /// <summary>
        /// Compatibility wrapper for FindObjectsByType across Unity versions.
        /// </summary>
        public static T[] FindObjects<T>(bool includeInactive = false) where T : Object
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Finds the brightest directional light in the scene to use as the Sun.
        /// </summary>
        /// <returns>The brightest Directional Light, or null if none found.</returns>
        public static Light FindSunLight()
        {
            Light[] lights = KromaLightingUtils.FindObjects<Light>();
            Light sunLight = null;
            float maxIntensity = 0f;
            
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && light.intensity > maxIntensity)
                {
                    sunLight = light;
                    maxIntensity = light.intensity;
                }
            }
            
            return sunLight;
        }

        /// <summary>
        /// Parses a standardized Kroma profile name (e.g. "Kroma_01_Day_Main") into a clean display name (e.g. "Day").
        /// </summary>
        public static string GetCleanProfileName(string originalName)
        {
            string[] parts = originalName.Split('_');
            if (parts.Length >= 4)
            {
                // Format: Kroma_XX_Name_Main
                // We want "Name" (index 2 onwards, excluding last part)
                int wordsToKeep = parts.Length - 3;
                if (wordsToKeep > 0)
                {
                    var middleParts = parts.Skip(2).Take(wordsToKeep);
                    string cleanName = string.Join(" ", middleParts).ToUpper();
                    // Convert to title case
                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName.ToLower());
                }
            }
            return originalName;
        }

        /// <summary>
        /// Helper to determine Time of Day from a profile.
        /// Currently uses name heuristics.
        /// </summary>
        public static string GetTimeOfDay(VolumeProfile profile)
        {
            if (profile == null) return "Unknown";
            
            string lowerName = profile.name.ToLower();
            if (lowerName.Contains("night") || lowerName.Contains("dark")) return "Night";
            if (lowerName.Contains("sunset") || lowerName.Contains("dusk")) return "Sunset";
            if (lowerName.Contains("day") || lowerName.Contains("bright") || lowerName.Contains("noon")) return "Day";
            if (lowerName.Contains("morning") || lowerName.Contains("dawn")) return "Morning";
            
            return "Custom";
        }

        /// <summary>
        /// Draws a colored outline around a Rect using EditorGUI.DrawRect.
        /// </summary>
        public static void DrawOutline(Rect rect, float width, Color color)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        /// <summary>
        /// Captures current scene directional light (sun) properties into the target profile.
        /// </summary>
        public static void CaptureSunPosition(KromaProfile profile)
        {
            if (profile == null) return;

            Light sunLight = FindSunLight();
            if (sunLight == null)
            {
                EditorUtility.DisplayDialog("Kroma", "No directional light found in scene! Please add a directional light first.", "OK");
                return;
            }

            Undo.RecordObject(profile, "Capture Sun Details");

            profile.sunColor = sunLight.color;
            profile.sunIntensity = sunLight.intensity;
            profile.shadowType = sunLight.shadows;
            profile.shadowStrength = sunLight.shadowStrength;

            if (profile.cameraRelativeSun)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    EditorUtility.DisplayDialog("Kroma", "No Main Camera found in scene! Make sure your camera has the 'MainCamera' tag.", "OK");
                    return;
                }

                Transform camTransform = mainCamera.transform;
                Vector3 camForward = camTransform.forward;
                Vector3 camRight = camTransform.right;
                Vector3 camUp = camTransform.up;

                Vector3 lightForward = sunLight.transform.forward;
                Vector3 sunDirection = -lightForward;

                float forwardComponent = Vector3.Dot(sunDirection, camForward);
                float rightComponent = Vector3.Dot(sunDirection, camRight);
                float upComponent = Vector3.Dot(sunDirection, camUp);

                float azimuth = Mathf.Atan2(rightComponent, forwardComponent) * Mathf.Rad2Deg;
                float elevation = Mathf.Asin(upComponent) * Mathf.Rad2Deg;

                profile.sunAzimuth = azimuth;
                profile.sunElevation = elevation;
            }
            else
            {
                SerializedObject so = new SerializedObject(sunLight.transform);
                SerializedProperty hintProp = so.FindProperty("m_LocalEulerAnglesHint");
                if (hintProp != null)
                {
                    profile.sunRotation = hintProp.vector3Value;
                }
                else
                {
                    profile.sunRotation = sunLight.transform.parent == null 
                        ? sunLight.transform.localEulerAngles 
                        : sunLight.transform.eulerAngles;
                }
            }

            EditorUtility.SetDirty(profile);
            // Debug.Log($"[Kroma] Direct light captured into profile '{profile.name}'");
        }

        /// <summary>
        /// Captures current scene fog and ambient lighting details into the target profile.
        /// </summary>
        public static void CaptureFogDetails(KromaProfile profile)
        {
            if (profile == null) return;

            Undo.RecordObject(profile, "Capture Fog Details");

            profile.environmentSource = RenderSettings.ambientMode;
            profile.environmentMultiplier = RenderSettings.ambientIntensity;
            profile.ambientSkyColor = RenderSettings.ambientSkyColor;
            profile.ambientEquatorColor = RenderSettings.ambientEquatorColor;
            profile.ambientGroundColor = RenderSettings.ambientGroundColor;
            profile.ambientColor = RenderSettings.ambientSkyColor;

            profile.fogEnabled = RenderSettings.fog;
            profile.fogColor = RenderSettings.fogColor;
            profile.fogMode = RenderSettings.fogMode;

            if (RenderSettings.fogMode == FogMode.Exponential || RenderSettings.fogMode == FogMode.ExponentialSquared)
            {
                profile.fogDensity = RenderSettings.fogDensity;
            }

            if (RenderSettings.fogMode == FogMode.Linear)
            {
                profile.fogStartDistance = RenderSettings.fogStartDistance;
                profile.fogEndDistance = RenderSettings.fogEndDistance;
            }

            EditorUtility.SetDirty(profile);
            // Debug.Log($"[Kroma] Fog & ambient lighting captured into profile '{profile.name}'");
        }
    }
}
