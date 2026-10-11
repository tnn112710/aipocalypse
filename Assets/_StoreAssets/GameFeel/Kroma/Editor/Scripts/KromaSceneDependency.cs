using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using FayLabs.Kroma;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Base class for scene dependency detection, validation, and creation.
    /// Provides scalable architecture for adding new requirements.
    /// </summary>
    public abstract class KromaSceneDependency
    {
        public abstract string DisplayName { get; }
        public abstract System.Type RequiredType { get; }
        
        /// <summary>
        /// Auto-detect the dependency in the current scene.
        /// Returns null if not found.
        /// </summary>
        public abstract Object DetectInScene();
        
        /// <summary>
        /// Validate if the detected object meets requirements.
        /// Returns true if valid, false if needs configuration.
        /// </summary>
        public abstract bool Validate(Object obj);
        
        /// <summary>
        /// Create the dependency if missing.
        /// Returns the created object.
        /// </summary>
        public abstract Object Create();
        
        /// <summary>
        /// Configure the object to meet Kroma requirements.
        /// Called on both existing and newly created objects.
        /// </summary>
        public abstract void Configure(Object obj);
        
        /// <summary>
        /// Get status message for UI display.
        /// </summary>
        public virtual string GetStatusMessage(Object obj)
        {
            if (obj == null) return "Missing";
            if (!Validate(obj)) return "Needs configuration";
            return "Ready";
        }
    }
    
    /// <summary>
    /// Camera dependency - detects Main Camera and ensures post-processing is enabled.
    /// </summary>
    public class CameraDependency : KromaSceneDependency
    {
        public override string DisplayName => "Main Camera";
        public override System.Type RequiredType => typeof(Camera);
        
        public override Object DetectInScene()
        {
            // Priority 1: MainCamera tag
            Camera mainCam = Camera.main;
            if (mainCam != null) return mainCam;
            
            // Priority 2: First camera found
            Camera[] cameras = KromaLightingUtils.FindObjects<Camera>();
            return cameras.Length > 0 ? cameras[0] : null;
        }
        
        public override bool Validate(Object obj)
        {
            Camera cam = obj as Camera;
            if (cam == null) return false;
            
            // Check if post-processing is enabled
            var additionalData = cam.GetUniversalAdditionalCameraData();
            if (additionalData == null) return false;
            
            return additionalData.renderPostProcessing;
        }
        
        public override Object Create()
        {
            GameObject camObj = new GameObject(KromaConstants.MAIN_CAMERA_NAME);
            camObj.tag = KromaConstants.TAG_MAIN_CAMERA;
            
            Camera cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            
            // Set default position
            camObj.transform.position = new Vector3(0, 1, -10);
            
            Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
            
            return cam;
        }
        
        public override void Configure(Object obj)
        {
            Camera cam = obj as Camera;
            if (cam == null) return;
            
            Undo.RecordObject(cam.gameObject, "Configure Camera for Kroma");
            
            // Set MainCamera tag if not already set (required for camera-relative sun positioning)
            // Set MainCamera tag if not already set (required for camera-relative sun positioning)
            if (cam.tag != KromaConstants.TAG_MAIN_CAMERA)
            {
                cam.tag = KromaConstants.TAG_MAIN_CAMERA;
                // Debug.Log($"[{KromaConstants.LOG_PREFIX}] Set '{cam.name}' tag to '{KromaConstants.TAG_MAIN_CAMERA}' for camera-relative features");
            }
            
            // Add UniversalAdditionalCameraData if missing
            var additionalData = cam.GetUniversalAdditionalCameraData();
            if (additionalData == null)
            {
                additionalData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            
            // Enable post-processing
            additionalData.renderPostProcessing = true;
            
            // Set anti-aliasing to FXAA
            additionalData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
            additionalData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
            
            EditorUtility.SetDirty(cam.gameObject);
        }
    }
    
    /// <summary>
    /// Directional Light dependency - detects brightest directional light.
    /// </summary>
    public class LightDependency : KromaSceneDependency
    {
        public override string DisplayName => "Sun Source";
        public override System.Type RequiredType => typeof(Light);
        
        public override Object DetectInScene()
        {
            Light[] lights = KromaLightingUtils.FindObjects<Light>();
            Light brightest = null;
            float maxIntensity = 0f;
            
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && light.intensity > maxIntensity)
                {
                    brightest = light;
                    maxIntensity = light.intensity;
                }
            }
            
            return brightest;
        }
        
        public override bool Validate(Object obj)
        {
            Light light = obj as Light;
            if (light == null || light.type != LightType.Directional) return false;
            
            // STRICT CHECK: Must also have the GetMainLightDirection component
            // This ensures skybox sync works correctly
            return light.GetComponent<GetMainLightDirection>() != null;
        }
        
        public override Object Create()
        {
            GameObject lightObj = new GameObject(KromaConstants.DIRECTIONAL_LIGHT_NAME);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            
            // Default configuration (can be overridden per-profile)
            light.color = new Color(1f, 0.96f, 0.84f); // #FFF4D6
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            
            // Add GetMainLightDirection component for skybox shader synchronization
            GetMainLightDirection lightDirectionScript = lightObj.AddComponent<GetMainLightDirection>();
            
            // Initialize with current skybox material if available
            if (RenderSettings.skybox != null)
            {
                // Use reflection to set the private field
                var field = typeof(GetMainLightDirection).GetField("skyboxMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(lightDirectionScript, RenderSettings.skybox);
                }
            }
            
            Undo.RegisterCreatedObjectUndo(lightObj, "Create Directional Light");
            
            return light;
        }
        
        public override void Configure(Object obj)
        {
            Light light = obj as Light;
            if (light == null) return;
            
            Undo.RecordObject(light.gameObject, "Configure Light for Kroma");
            
            // Ensure GetMainLightDirection component exists
            GetMainLightDirection lightDirectionScript = light.GetComponent<GetMainLightDirection>();
            if (lightDirectionScript == null)
            {
                lightDirectionScript = light.gameObject.AddComponent<GetMainLightDirection>();
            }
            
            // Update skybox material reference to current scene skybox
            if (RenderSettings.skybox != null)
            {
                // Use reflection to set the private field
                var field = typeof(GetMainLightDirection).GetField("skyboxMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(lightDirectionScript, RenderSettings.skybox);
                }
            }
            
            EditorUtility.SetDirty(light.gameObject);
        }
    }
    
    /// <summary>
    /// Volume dependency - detects Global Volume or creates one.
    /// </summary>
    public class VolumeDependency : KromaSceneDependency
    {
        public override string DisplayName => "PP Volume";
        public override System.Type RequiredType => typeof(Volume);
        
        public override Object DetectInScene()
        {
            Volume[] volumes = KromaLightingUtils.FindObjects<Volume>();
            
            // Priority 1: Volume named "Global Volume" that is global
            foreach (Volume vol in volumes)
            {
                if (vol.name == KromaConstants.GLOBAL_VOLUME_NAME && vol.isGlobal)
                    return vol;
            }
            
            // Priority 2: Any global volume
            foreach (Volume vol in volumes)
            {
                if (vol.isGlobal)
                    return vol;
            }
            
            // Priority 3: First volume found
            return volumes.Length > 0 ? volumes[0] : null;
        }
        
        public override bool Validate(Object obj)
        {
            Volume vol = obj as Volume;
            return vol != null && vol.isGlobal;
        }
        
        public override Object Create()
        {
            GameObject volObj = new GameObject(KromaConstants.GLOBAL_VOLUME_NAME);
            Volume vol = volObj.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0;
            
            Undo.RegisterCreatedObjectUndo(volObj, "Create Global Volume");
            
            return vol;
        }
        
        public override void Configure(Object obj)
        {
            Volume vol = obj as Volume;
            if (vol == null) return;
            
            Undo.RecordObject(vol, "Configure Volume for Kroma");
            
            // Ensure it's global
            vol.isGlobal = true;
            
            // Set priority to 0 if not already set
            if (vol.priority != 0)
                vol.priority = 0;
            
            EditorUtility.SetDirty(vol);
        }
    }
}
