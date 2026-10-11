using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace FayLabs.Kroma.Editor
{
    public enum KromaTimeOfDay
    {
        Sunrise,
        Daytime,
        Sunset,
        Night,
        Other
    }
    
    [Flags]
    public enum KromaProfileCategory
    {
        None = 0,
        Nature = 1 << 0,
        Urban = 1 << 1,
        SciFi = 1 << 2,
        Studio = 1 << 3,
        Abstract = 1 << 4,
        Interior = 1 << 5,
        Exterior = 1 << 6,
        Stylized = 1 << 7,
        Realistic = 1 << 8,
        Other = 1 << 9
    }

    [System.Serializable]
    public struct SceneObjectConfig
    {
        [Tooltip("If checked, this object config is active and will be spawned when the profile is applied.")]
        public bool enabled;

        [Tooltip("The prefab to spawn (e.g. particle systems, custom lights, meshes).")]
        public GameObject prefab;

        [Tooltip("Position offset of the spawned object relative to its parent/anchor.")]
        public Vector3 position;

        [Tooltip("Rotation offset of the spawned object.")]
        public Vector3 rotation;

        [Tooltip("Scale multiplier of the spawned object.")]
        public Vector3 scale;
        
        // --- NEW FIELDS FOR REFINED CONTROL ---
        [Tooltip("If true, the object is placed relative to the camera instead of absolute world position.")]
        public bool useCameraRelative;

        [Tooltip("If true, the object is parented directly to the Main Camera.")]
        public bool parentToCamera;

        [Tooltip("The snapping behavior: None, Ground (snaps Y to floor), or Camera (snaps to camera position).")]
        public SceneObjectSnapMode snapMode;

        [Tooltip("Y-offset height above the ground when Snap Mode is set to Ground.")]
        public float groundOffset;

        [Tooltip("XYZ positional offset from the camera when Snap Mode is set to Camera.")]
        public Vector3 cameraOffset;

        [Tooltip("Maximum raycast distance used for ground snapping detection.")]
        public float raycastDistance;
        
        // --- LEGACY / ADVANCED POSITIONING ---
        [Range(-180f, 180f)]
        [Tooltip("Legacy: Horizontal angle relative to camera view.")]
        public float azimuth;

        [Range(-90f, 90f)]
        [Tooltip("Legacy: Vertical angle relative to camera view.")]
        public float elevation;

        [Tooltip("Legacy: Radial distance from camera.")]
        public float distance;

        [Tooltip("Legacy: Rotation offset to apply relative to camera face direction.")]
        public Vector3 rotationOffset;

        [Tooltip("Legacy: Absolute world rotation to force on spawn.")]
        public Vector3 absoluteRotation;

        [Tooltip("Legacy: Name of the scene object to parent to.")]
        public string parentObjectName;
        
        // Rotation Control
        [Tooltip("Determines how the object rotates in relation to the camera:\n- Fully Relative: Rotates dynamically with the camera's view rotation.\n- Keep World Up: Rotates on Y-axis only (billboard style, keeping vertical orientation - good for rain/snow).\n- Fully Absolute: Ignores camera rotation entirely and forces world-aligned angles.")]
        public SceneObjectRotationMode rotationMode;

        [Tooltip("Determines where the object is nested in the hierarchy (e.g. under the Kroma Scene Profile).")]
        public SceneParentMode parentMode;

        [Tooltip("If checked, the spawned prefab will automatically have the KromaCameraFollow component attached and enabled.")]
        public bool followCamera;

        // Default constructor
        public SceneObjectConfig(bool enabled)
        {
            this.enabled = enabled;
            this.prefab = null;
            this.position = Vector3.zero;
            this.rotation = Vector3.zero;
            this.scale = Vector3.one;
            this.useCameraRelative = true;
            this.parentToCamera = false;
            this.parentMode = SceneParentMode.KromaSceneProfile;
            this.snapMode = SceneObjectSnapMode.None;
            this.groundOffset = 0f;
            this.cameraOffset = Vector3.zero;
            this.raycastDistance = 400f;
            this.rotationMode = SceneObjectRotationMode.KeepWorldUp;
            this.azimuth = 0f;
            this.elevation = 0f; 
            this.distance = 5f;
            this.rotationOffset = Vector3.zero;
            this.absoluteRotation = Vector3.zero;
            this.parentObjectName = "";
            this.followCamera = false;
        }
    }
    
    public enum SceneObjectRotationMode
    {
        KeepWorldUp = 1,
        FullyRelative = 0,
        FullyAbsolute = 2
    }

    public enum SceneParentMode
    {
        KromaSceneProfile = 0,
        Camera = 1,
        Other = 2
    }

    public enum SceneObjectSnapMode
    {
        None = 0,
        Ground = 1,
        Camera = 2
    }

    public class KromaProfile : ScriptableObject
    {
        [Tooltip("Reference to the Post-Processing Volume Profile used by this style.")]
        public VolumeProfile volumeProfile;

        [Tooltip("Reference to the Skybox Material used by this style.")]
        public Material skyboxMaterial;

        [Tooltip("If checked, the profile will control and apply sun light settings (color, intensity, direction).")]
        public bool configureSunLight = true;

        [Tooltip("If checked, the sun rotation is positioned relative to the editor camera direction.")]
        public bool cameraRelativeSun = true;

        [Range(-180f, 180f)]
        [Tooltip("Sun horizontal angle relative to camera view.")]
        public float sunAzimuth = 9.10f;

        [Range(-90f, 90f)]
        [Tooltip("Sun vertical angle relative to camera view.")]
        public float sunElevation = 83.23f;

        [Tooltip("The raw rotation angles for the directional sun light.")]
        public Vector3 sunRotation = new Vector3(83.23f, 9.10f, 0);

        [Tooltip("The color of the sun light.")]
        public Color sunColor = new Color(1f, 0.9333f, 0.7804f);

        [Range(0f, 10f)]
        [Tooltip("The intensity multiplier of the sun light source.")]
        public float sunIntensity = 2.0f;

        [Tooltip("The shadows type (None, Hard, Soft) cast by the sun light.")]
        public LightShadows shadowType = LightShadows.Soft;

        [Range(0f, 1f)]
        [Tooltip("Shadow details strength cast by the sun light.")]
        public float shadowStrength = 0.7f;

        // Environment Lighting
        [Tooltip("Specifies the environment source mode: Skybox, Color (Flat), or Gradient (Trilight).")]
        public AmbientMode environmentSource = AmbientMode.Skybox;

        [ColorUsage(false, true)]
        [Tooltip("The gradient sky color for ambient lighting (used in Gradient/Trilight mode).")]
        public Color ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);

        [ColorUsage(false, true)]
        [Tooltip("The gradient equator color for ambient lighting (used in Gradient/Trilight mode).")]
        public Color ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);

        [ColorUsage(false, true)]
        [Tooltip("The gradient ground color for ambient lighting (used in Gradient/Trilight mode).")]
        public Color ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);

        [ColorUsage(false, true)]
        [Tooltip("The flat ambient color (used in Flat/Color mode).")]
        public Color ambientColor = new Color(0.212f, 0.227f, 0.259f);
        
        [Range(0f, 5f)]
        [Tooltip("The global environment lighting multiplier.")]
        public float environmentMultiplier = 1.0f;

        [Tooltip("If checked, fog will be enabled in the scene when this profile is applied.")]
        public bool fogEnabled = true;

        [Tooltip("The color of the scene fog.")]
        public Color fogColor = new Color(0.5255f, 0.6863f, 0.7882f);

        [Tooltip("The fog rendering calculation mode: Linear, Exponential, or Exponential Squared.")]
        public FogMode fogMode = FogMode.ExponentialSquared;

        [Range(0f, 1f)]
        [Tooltip("The thickness/density of the fog (used in Exponential/Squared modes).")]
        public float fogDensity = 0.01f;

        [Tooltip("The starting distance of the fog fade-in (used in Linear mode).")]
        public float fogStartDistance = 0f;

        [Tooltip("The ending distance of the fog fade-out (used in Linear mode).")]
        public float fogEndDistance = 300f;

        [Tooltip("List of custom atmosphere prefabs (VFX, weather, secondary lights) associated with this profile.")]
        public SceneObjectConfig[] sceneObjects;
        
        [Tooltip("The thumbnail image representing this profile visually in the browser.")]
        public Texture2D thumbnail;

        [Tooltip("The clean display name of the profile.")]
        public string profileName;

        [Tooltip("The visual category of this profile (Nature, Urban, Stylized, Abstract, etc.).")]
        public KromaProfileCategory category = KromaProfileCategory.None;

        [Tooltip("The time-of-day category for this profile (Daytime, Sunrise, Night, etc.).")]
        public KromaTimeOfDay timeOfDay = KromaTimeOfDay.Daytime;

        [Tooltip("Comma-separated keyword tags for filtering search terms in the browser.")]
        public string searchTags;
        
        // Helper property to get clean tags array
        public string[] Tags => !string.IsNullOrEmpty(searchTags) 
            ? searchTags.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries) 
            : new string[0];
    }
}
