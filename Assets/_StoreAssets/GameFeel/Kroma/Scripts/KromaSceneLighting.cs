using UnityEngine;
using UnityEngine.Rendering;

namespace FayLabs.Kroma
{
    /// <summary>
    /// Scene-instance component that references the applied Kroma profile.
    /// Provides quick access to reset settings or switch profiles.
    /// </summary>
    [AddComponentMenu("Kroma/Scene Lighting")]
    public class KromaSceneLighting : MonoBehaviour
    {
        [Header("Applied Profile")]
#if UNITY_EDITOR
        [Tooltip("Reference to the KromaProfile applied to this scene (Editor Only)")]
        public ScriptableObject kromaProfile;
#endif
        
        [Header("Visual Identity")]
        [Tooltip("Banner image for this profile (shared across all profiles for now)")]
        public Texture2D bannerTexture;
        
        [Header("Atmosphere Prefabs")]
        [Tooltip("Scene objects spawned by this profile (lights, VFX, etc.)")]
        [HideInInspector]
        public GameObject[] appliedSceneObjects = new GameObject[0];
        
        [HideInInspector]
        public float environmentLightingBoost = 0f;
        
        [HideInInspector]
        public float volumeWeight = 1f;
        
        [HideInInspector]
        public float fogIntensity = 1f;
    }
}
