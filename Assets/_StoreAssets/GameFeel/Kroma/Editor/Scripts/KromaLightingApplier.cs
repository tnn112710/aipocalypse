using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using FayLabs.Kroma;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Static utility class for applying lighting, skybox, and fog settings to the active scene.
    /// Separated from data layer (KromaLightingSettings) for clean architecture and editor/runtime separation.
    /// </summary>
    public static class KromaLightingApplier
    {
        // --- NEW METHOD FOR KROMA PROFILE ASSETS ---
        public static void ApplyLightingSettings(KromaProfile profile, bool applySkybox = true, bool applySun = true, bool applyFog = true, bool applyVolume = true)
        {
            if (profile == null) return;

            // Apply Volume Profile (NEW)
            if (applyVolume && profile.volumeProfile != null)
            {
                // Safety: Remove null components to prevent Editor crashes
                if (profile.volumeProfile.components.RemoveAll(c => c == null) > 0)
                {
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(profile.volumeProfile);
                    #endif
                }

                Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
                Volume globalVolume = null;
                foreach (var vol in volumes)
                {
                    if (vol.isGlobal)
                    {
                        globalVolume = vol;
                        break;
                    }
                }

                if (globalVolume != null)
                {
                    Undo.RecordObject(globalVolume, "Apply Kroma Profile");
                    globalVolume.sharedProfile = profile.volumeProfile;
                }
                else
                {
                    Debug.LogWarning("[Kroma] No Global Volume found in scene to apply profile to!");
                }
            }

            // Apply Skybox
            if (applySkybox && profile.skyboxMaterial != null)
            {
                #if UNITY_EDITOR
                if (Application.unityVersion.StartsWith("6000.0"))
                {
                    profile.skyboxMaterial.EnableKeyword("_KROMA_6000_0_COMPATIBILITY");
                }
                else
                {
                    profile.skyboxMaterial.DisableKeyword("_KROMA_6000_0_COMPATIBILITY");
                }
                EditorUtility.SetDirty(profile.skyboxMaterial);
                #endif

                RenderSettings.skybox = profile.skyboxMaterial;
                // Note: New profile doesn't seem to store Exposure/Rotation explicitly yet? 
                // Or maybe they are properties of the material itself now?
                // The old struct had them separate. The new one doesn't. 
                // We'll rely on the material's saved state.
                DynamicGI.UpdateEnvironment();
            }

            // Apply Lighting Intensity
            if (applySkybox)
            {
                RenderSettings.ambientIntensity = profile.environmentMultiplier;
            }

            // Apply Fog
            if (applyFog)
            {
                RenderSettings.fog = profile.fogEnabled;
                if (profile.fogEnabled)
                {
                    RenderSettings.fogColor = profile.fogColor;
                    RenderSettings.fogMode = profile.fogMode;
                    switch (profile.fogMode)
                    {
                        case FogMode.Linear:
                            RenderSettings.fogStartDistance = profile.fogStartDistance;
                            RenderSettings.fogEndDistance = profile.fogEndDistance;
                            break;
                        case FogMode.Exponential:
                        case FogMode.ExponentialSquared:
                            RenderSettings.fogDensity = profile.fogDensity;
                            break;
                    }
                }
            }

            // Apply Environment Lighting
            if (applyFog) // Re-using fog toggle for "Atmosphere" generally, or should it be separate? 
                          // The UI groups it as "Fog & Environment Settings", so using 'applyFog' is consistent with UI.
                          // Ideally we might split them, but for now this matches the grouping.
            {
                RenderSettings.ambientMode = profile.environmentSource;
                
                if (profile.environmentSource == AmbientMode.Skybox)
                {
                    RenderSettings.ambientIntensity = profile.environmentMultiplier;
                }
                else if (profile.environmentSource == AmbientMode.Trilight)
                {
                    RenderSettings.ambientSkyColor = profile.ambientSkyColor;
                    RenderSettings.ambientEquatorColor = profile.ambientEquatorColor;
                    RenderSettings.ambientGroundColor = profile.ambientGroundColor;
                }
                else if (profile.environmentSource == AmbientMode.Flat)
                {
                    RenderSettings.ambientSkyColor = profile.ambientColor;
                }
            }

            // Apply Sun Light
            if (applySun && profile.configureSunLight)
            {
                Light sunLight = KromaLightingUtils.FindSunLight();
                if (sunLight != null)
                {
                    sunLight.color = profile.sunColor;
                    sunLight.intensity = profile.sunIntensity;
                    sunLight.shadows = profile.shadowType;
                    sunLight.shadowStrength = profile.shadowStrength;

                    // Rotation Logic
                    if (profile.cameraRelativeSun)
                    {
                        Camera cam = Camera.main;
                            // Use legacy method to ensure exact behavior match
                            Quaternion cameraRelativeRotation = CalculateCameraRelativeSunRotation(profile.sunAzimuth, profile.sunElevation);
                            
                            if (cameraRelativeRotation != Quaternion.identity)
                            {
                                sunLight.transform.rotation = cameraRelativeRotation;
                            }
                    }
                    else
                    {
                        sunLight.transform.rotation = Quaternion.Euler(profile.sunRotation);
                    }
                    
                    // Update Helper
                    GetMainLightDirection lightDirectionScript = sunLight.GetComponent<GetMainLightDirection>();
                    if (lightDirectionScript != null && profile.skyboxMaterial != null)
                    {
                        var field = typeof(GetMainLightDirection).GetField("skyboxMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null) field.SetValue(lightDirectionScript, profile.skyboxMaterial);
                    }
                    
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(sunLight);
                    #endif
                }
            }

            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );
            #endif
        }


        
        /// <summary>
        /// Calculate sun rotation relative to the Main Camera in the scene
        /// Uses camera's local coordinate system to build sun direction
        /// </summary>
        /// <param name="azimuth">Horizontal angle from camera forward (-180 to 180). 0=front, 90=right, -90=left, 180=behind</param>
        /// <param name="elevation">Vertical angle above horizon (-90 to 90). 0=horizon, 90=overhead</param>
        /// <returns>Sun rotation quaternion, or Quaternion.identity if camera not available</returns>
        private static Quaternion CalculateCameraRelativeSunRotation(float azimuth, float elevation)
        {
            // Find the Main Camera in the scene
            Camera mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                Debug.LogWarning("[Kroma] No Main Camera found in scene! Make sure your camera has the 'MainCamera' tag.");
                return Quaternion.identity;
            }

            Transform camTransform = mainCamera.transform;
            
            // Get camera's local coordinate system
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            Vector3 camUp = camTransform.up;
            
            // Convert azimuth and elevation to radians
            float azimuthRad = azimuth * Mathf.Deg2Rad;
            float elevationRad = elevation * Mathf.Deg2Rad;
            
            // Build sun direction in camera's local space using spherical coordinates
            float cosElev = Mathf.Cos(elevationRad);
            float sinElev = Mathf.Sin(elevationRad);
            float cosAzim = Mathf.Cos(azimuthRad);
            float sinAzim = Mathf.Sin(azimuthRad);
            
            float forwardComponent = cosElev * cosAzim;
            float rightComponent = cosElev * sinAzim;
            float upComponent = sinElev;
            
            // Transform to world space using camera's basis vectors
            Vector3 sunDirection = 
                camForward * forwardComponent +
                camRight * rightComponent +
                camUp * upComponent;
            
            sunDirection.Normalize();
            
            // Light shines FROM sun TOWARD ground (opposite direction)
            Vector3 lightForward = -sunDirection;
            
            // Convert to rotation
            return Quaternion.LookRotation(lightForward, Vector3.up);
        }


    }
}
