namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Stores all text, labels, and messages used in the Kroma Browser.
    /// Edit this file to change text or translate the tool.
    /// </summary>
    public static class KromaBrowserStrings
    {
        // ========================================
        // WINDOW & BRANDING
        // ========================================
        
        public const string WINDOW_TITLE = "Kroma Browser";
        public const string BANNER_COPYRIGHT_TEXT = "Kroma v1.0.0";
        public const string BANNER_HELP_LINK_TEXT = "<u>User Guide (PDF)</u>";
        public const string BANNER_HELP_URL = "https://docs.faylabs.com/kroma";
        public const string BANNER_ONLINE_DOCS_LINK_TEXT = "<u>Online Documentation</u>";
        public const string BANNER_ONLINE_DOCS_URL = "https://faylabs.dev/kroma/#home";
        public const string BANNER_DISCORD_LINK_TEXT = "<u>Discord Community</u>";
        public const string BANNER_DISCORD_URL = "https://discord.gg/vJahRyJZwW";
        public const string BANNER_MISSING_IMAGE = "Add 'Kroma_Banner' to Editor/Images/.";
        
        // ========================================
        // SETUP / ONBOARDING SCREEN
        // ========================================
        
        public const string SETUP_SCREEN_TITLE = "Kroma Scene Setup";
        public const string SETUP_SCREEN_SUBTITLE = "Review scene dependencies before initialization.";
        
        // New Greeting
        public const string SETUP_GREETING_HEADLINE = "Welcome to Kroma. Let’s prep your scene.";
        public const string SETUP_GREETING_BODY = "We’ll sync with your existing objects or spawn missing ones to get your environment ready.";

        public const string SETUP_SECTION_SCENE_OBJECTS = "Scene objects";
        public const string SETUP_BTN_INITIALIZE = "Initialize Kroma";
        public const string SETUP_NOTIFICATION_INITIALIZED = "Kroma scene initialized";
        
        // Setup - Dependency Labels
        public const string SETUP_LABEL_CAMERA = "Main Camera";
        public const string SETUP_LABEL_LIGHT = "Sun Source";
        public const string SETUP_LABEL_VOLUME = "PP Volume";
        
        // Setup - Status Messages
        public const string SETUP_STATUS_URP_REQUIRED = "URP Required: Your project is not using URP. Set up Universal Render Pipeline first.";
        public const string SETUP_STATUS_READY = "All requirements met. Ready to initialize.";
        public const string SETUP_STATUS_AUTO_CREATE = "The missing components will be created automatically.";
        public const string SETUP_URP_ERROR_BANNER = "⚠ URP Required: Your project is not using URP. Set up Universal Render Pipeline first.";
        
        // Setup - Dependency Tooltips
        public const string SETUP_TOOLTIP_CAMERA = "Main camera with post-processing enabled. Kroma will automatically configure post-processing and FXAA anti-aliasing.";
        public const string SETUP_TOOLTIP_LIGHT = "Directional light for scene lighting. Can be configured per-profile for different times of day.";
        public const string SETUP_TOOLTIP_VOLUME = "Global Volume component for post-processing effects. Must be set to Global with priority 0.";
        
        // Setup - Dialog
        public const string SETUP_DIALOG_INFO_TITLE = "Info";
        public const string SETUP_DIALOG_OK = "OK";
        
        // Setup - Symbols
        public const string SYMBOL_CHECKMARK = "✓ ";
        public const string SYMBOL_INFO = "ⓘ";
        
        // ========================================
        // MAIN INTERFACE - TOOLBAR
        // ========================================
        
        public const string TAB_GALLERY = "Grid";
        public const string TAB_LIST = "List";
        public const string BTN_REFRESH = "Refresh List";
        public const string SEARCH_PLACEHOLDER = "Search Profiles...";
        
        // ========================================
        // MAIN INTERFACE - WARNINGS & ERRORS
        // ========================================
        
        public const string WARNING_NO_SELECTION = "Please select a Global Volume or Kroma Zone to continue.";
        public const string ERROR_SELECT_VOLUME = "Select a Volume object in the hierarchy first.";
        
        // ========================================
        // PROFILE CARDS
        // ========================================
        
        public const string MSG_NO_IMAGE = "No Image";
        public const string BTN_PREVIEW = "Preview";
        public const string BTN_APPLY = "Apply";
        public const string BTN_CANCEL = "Cancel";
        public const string BADGE_ACTIVE = "Active";
        public const string BADGE_PREVIEW = "Preview";
        
        // ========================================
        // FAVORITES SYSTEM
        // ========================================
        
        public const string BTN_FAVORITES = "Favorites";
        public const string BTN_FAVORITES_COUNT = "Favorites ({0})"; // {0} = count
        public const string MSG_NO_FAVORITES_TITLE = "Keep your best looks handy";
        public const string MSG_NO_FAVORITES_BODY = "See a profile you like? Click the star icon to save it here.";
        public const string BTN_BROWSE_PROFILES = "Browse Profiles";
        public const string TOOLTIP_FAVORITE = "Add to favorites";
        public const string TOOLTIP_UNFAVORITE = "Remove from favorites";
        public const string TOOLTIP_FAVORITES_FILTER = "Show only favorites";
        public const string TOOLTIP_FAVORITES_FILTER_ACTIVE = "Showing favorites only (click to show all)";
        public const string TOOLTIP_NO_FAVORITES = "No favorites yet. Star profiles to add them to favorites.";
        // ========================================
        // MY PROFILES EMPTY STATE
        // ========================================
        
        public const string MSG_NO_MY_PROFILES_TITLE = "Create your first profile";
        public const string MSG_NO_MY_PROFILES_BODY = "Save your favorite skybox and post-processing settings to access them instantly in the Kroma Browser. [Learn more]";
        public const string BTN_CREATE_PROFILE = "Create Profile";
        public const string BTN_GO_BACK = "Go Back";
        
        // ========================================
        // SETTINGS VIEW
        // ========================================
        
        public const string SETTINGS_TITLE = " Settings"; // Leading space for icon alignment
        public const string SETTINGS_BTN_BACK = "← Back to gallery";
        public const string SETTINGS_APPLY_SKYBOX = "Apply Skybox Settings";
        public const string SETTINGS_APPLY_FOG = "Apply Fog Settings";
        public const string SETTINGS_SKYBOX_DESC = "When enabled, profiles will also update the skybox parameters.";
        public const string SETTINGS_FOG_DESC = "When enabled, profiles will also update fog parameters.";
        public const string SETTINGS_CURRENT_SKYBOX = "Currently using: {0}";
        public const string SETTINGS_NO_SKYBOX = "No skybox material set";
        public const string TOOLTIP_SETTINGS = "Open Kroma settings";
        
        // ========================================
        // NOTIFICATIONS & FEEDBACK
        // ========================================
        
        public const string MSG_STYLE_APPLIED = "{0} style applied"; // {0} = profile name
        
        // ========================================
        // UNDO OPERATIONS
        // ========================================
        
        public const string UNDO_INIT = "Initialize Kroma Scene";
        public const string UNDO_APPLY = "Apply Kroma Profile";
        
        // ========================================
        // KEYBOARD SHORTCUTS
        // ========================================
        
        public const string SHORTCUT_HELP = "Arrow Keys: Navigate | Enter: Preview | Shift+Enter: Apply | Esc: Stop Preview | /: Search";
        
        // ========================================
        // SELECTIVE APPLY TOGGLES
        // ========================================
        
        public const string TOGGLE_POST_PROCESSING = "Post Processing";
        public const string TOGGLE_SKYBOX = "Skybox";
        public const string TOGGLE_SUN = "Sun";
        public const string TOGGLE_FOG = "Fog";
        public const string TOGGLE_SCENE_OBJECTS = "Scene Objects";
        
        public const string TOOLTIP_TOGGLE_PP = "Apply post-processing overrides from profile";
        public const string TOOLTIP_TOGGLE_SKY = "Apply skybox settings from profile";
        public const string TOOLTIP_TOGGLE_SUN = "Apply sun light settings from profile";
        public const string TOOLTIP_TOGGLE_FOG = "Apply fog settings from profile";
        public const string TOOLTIP_TOGGLE_SCENE_OBJECTS = "Spawn scene objects (VFX, lights, etc.) from profile";
        
        // ========================================
        // SCENE LIGHTING COMPONENT
        // ========================================
        
        public const string SCENE_LIGHTING_GAMEOBJECT = "Kroma Profile: {0}"; // {0} = profile name
        public const string BTN_RESET_TO_DEFAULTS = "Reset to Profile Defaults";
        public const string BTN_CAPTURE_SCENE = "Capture Current Scene";
        public const string BTN_APPLY_TO_SCENE = "Apply to Scene Now";
        public const string MSG_RESET_CONFIRM = "Reset all lighting values to the original profile defaults?";
        public const string MSG_RESET_TITLE = "Reset Lighting";
        public const string MSG_NO_DEFAULTS = "No default lighting settings found for profile '{0}'.";
        public const string MSG_NO_DEFAULTS_TITLE = "No Defaults Found";
        public const string MSG_NO_PROFILE = "This component has no source profile assigned.";
        public const string MSG_NO_PROFILE_TITLE = "No Profile";
        public const string MSG_COMPONENT_NOT_FROM_BROWSER = "This component was not created by Kroma Browser. Use 'Capture Current Scene' to save current lighting.";
        
        public const string RESET_PROFILE_DESC = "Resetting will overwrite your manual lighting adjustments with the default Kroma Profile settings and reconnect the original Skybox and Post-Processing profile.";
    }
}