namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Centralized constants to avoid "magic strings" and ensure consistency across the codebase.
    /// </summary>
    public static class KromaConstants
    {
        // Paths
        
        // Asset Names & Search Queries
        public const string BANNER_TEXTURE_NAME = "Kroma_Banner";
        public const string BANNER_SEARCH_QUERY = "Kroma_Banner t:Texture2D";
        public const string DEFAULT_THUMBNAIL_NAME = "Kroma_Default_Thumbnail";
        public const string PROFILE_SEARCH_QUERY = "t:VolumeProfile";
        public const string PROFILE_NAME_PREFIX = "Kroma_"; // Only load profiles starting with this
        public const string TEXTURE_SEARCH_QUERY = "t:Texture2D";
        public const string SKYBOX_SHADER_NAME = "FayLabs/Kroma/Skybox V2";
        public const string SKYBOX_MATERIAL_NAME = "New Kroma Skybox";
        
        // Scene Object Names
        public const string MAIN_CAMERA_NAME = "Main Camera";
        public const string GLOBAL_VOLUME_NAME = "Global Volume";
        public const string DIRECTIONAL_LIGHT_NAME = "Directional Light";
        
        // Tags
        public const string TAG_MAIN_CAMERA = "MainCamera";
        
        // Log Prefix
        public const string LOG_PREFIX = "[Kroma]";

        // PlayerPrefs Keys
        public const string PREF_APPLY_SKYBOX = "Kroma_ApplySkybox";
        public const string PREF_APPLY_FOG = "Kroma_ApplyFog";
        public const string PREF_APPLY_PP = "Kroma_ApplyPostProcessing";
        public const string PREF_APPLY_SUN = "Kroma_ApplySunLight";
        public const string PREF_APPLY_SCENE_OBJECTS = "Kroma_ApplySceneObjects";
        public const string PREF_AUTO_SIMULATE_PARTICLES = "Kroma_AutoSimulateParticles";
    }
}


