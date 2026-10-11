using UnityEngine;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Constants partial class for KromaBrowserDrawer.
    /// Contains all layout constants, color definitions, and UI configuration values.
    /// Part of the partial class refactoring for improved code organization.
    /// </summary>
    public partial class KromaBrowserDrawer
    {
        // --- LAYOUT CONSTANTS (Unity 4pt Grid System) ---
        private const float CARD_WIDTH_MIN = 180f; // Lowered to keep columns fitting longer
        private const float CARD_WIDTH_MAX = 260f; // Lowered to prevent bloated wrapping sizes
        private const float CARD_HEIGHT = 260f;
        private const float BUTTON_HEIGHT = 28f; 
        private const float SPACING = 12f;        
        private const float PADDING = 16f;       
        
        private const float TOOLBAR_SPACING = 8f;  // Reduced from 12f for more compact toolbar
        
        // NEW: Search Bar & Tab Height
        private const float SEARCH_HEIGHT = 28f; // Increased to 28px for better touch/click targets

        // Internal Padding inside the Card
        private const float CARD_PAD_SIDES = 2f;
        private const float CARD_PAD_TOP = 2f;
        private const float CARD_PAD_BOTTOM = 2f;
        private const float GAP_IMAGE_BUTTON = 2f;

        // Placeholder Card Opacity (adjust to make placeholders more/less visible)
        private const float PLACEHOLDER_OPACITY = 0.2f; // Range: 0.0 (invisible) to 1.0 (fully visible)

        // Card Overlay Styling (for name and badge display)
        private const float CARD_OVERLAY_HEIGHT = 28f; // Height of the name/badge overlay bar
        private const float CARD_OVERLAY_OPACITY = 0.7f; // Opacity of the overlay background (Increased 5%)
        private const float CARD_OVERLAY_PADDING = 4f; // Horizontal padding inside overlay
        private const float CARD_NAME_FONT_SIZE = 12f; // Font size for card name
        private const float CARD_BADGE_HEIGHT = 20f; // Height of status badge pill
        private const float CARD_BADGE_PADDING = 8f; // Horizontal padding inside badge

        // Card Colors (use hex format: #RRGGBB)
        private const string COLOR_ACTIVE_BG = "#F97316";        // Active badge background (Orange) - KEEP for legacy or fallback
        private const string COLOR_ACTIVE_TEXT = "#FFFFFF";      // Active badge text (White)
        private const string COLOR_PREVIEW_BG = "#FFFFFF";       // Preview badge background (White)
        private const string COLOR_PREVIEW_TEXT = "#000000";     // Preview badge text (Black)
        
        // --- STAR ICONS ---
        private const string COLOR_STAR_DEFAULT = "#a7a7a7"; 
        private const string COLOR_STAR_HOVER = "#dcdcdc";
        private const string COLOR_STAR_ACTIVE = "#ffd361";

        // --- NEW STATUS COLORS ---
        private const string COLOR_STATUS_ACTIVE_BG = "#FFBD4D";     // Gold (Header)
        private const string COLOR_STATUS_CONFIRM_BTN = "#2A9D8F";   // Green (Apply Button)
        private const string COLOR_STATUS_PREVIEW_HEADER = "#FFFFFF"; // White (Header)
        private const string COLOR_STATUS_HOVER_GREY = "#B3B3B3";    // Grey (Time/Category Text)
        private const string COLOR_CARD_BG = "#2F2F2F";              // Dark Grey (Card Background / Disabled)
        private const string COLOR_CARD_HOVER_BG = "#9d9d9d";        // User requested hover background

        private const float CARD_HOVER_OPACITY = 0.2f;           // Card hover highlight opacity (0.0-1.0)

        // List View Styling
        private const float LIST_ROW_HEIGHT = 44f;             // Height of each list row
        private const float LIST_ROW_SPACING = 4f;             // Vertical spacing between rows
        private const float LIST_PADDING_LEFT = 12f;           // Left padding for name text
        private const float LIST_BUTTON_WIDTH = 80f;           // Width of Preview/Apply buttons
        private const float LIST_BUTTON_HEIGHT = 28f;          // Height of Preview/Apply buttons

        // Banner Styling
        private const string BANNER_BG_COLOR = "#000000";      // Background color for banner area (hex format)
        private const float BANNER_HEIGHT = 40f; 

        // Warning Banner Styling
        private const string WARNING_BG_COLOR = "#ffbe76";     // Warning banner background (hex format)
        private const string WARNING_TEXT_COLOR = "#000000";   // Warning text color (hex format)
        private const float WARNING_BANNER_HEIGHT = 36f;       // Height of warning banner
        private const float WARNING_ICON_SIZE = 28f;           // Size of warning icon
        private const float WARNING_BG_OPACITY = 1.0f;         // Background opacity (0.0-1.0)
        private const float WARNING_ICON_OFFSET_Y = 2f;        // Vertical offset for icon (adjust for perfect centering)

        // Content Area Styling
        private const string SEPARATOR_COLOR = "#1A1A1A";      // Separator line color (hex format)
        // CONTENT_BG_COLOR moved to Styles.cs as adaptive color field
        private const float TOOLBAR_PADDING_TOP = 8f;          // Top padding in toolbar area
        private const float TOOLBAR_PADDING_BOTTOM = 8f;       // Bottom padding in toolbar area
        private const float CONTENT_TOP_PADDING = 16f;         // Top padding for gallery/list content

        // Card Opacity States
        private const float CARD_OPACITY_DEFAULT = 0.3f;       // Default card opacity (0.0-1.0)
        private const float CARD_OPACITY_HOVER = 1.0f;         // Hover card opacity (0.0-1.0)
        private const float CARD_OPACITY_DISABLED = 0.4f;      // Disabled card opacity when no volume selected (0.0-1.0)
        
        // Border Styles (editable)
        private const float BORDER_WIDTH = 2f;                 // Width of selection and active borders
        private static readonly Color BORDER_COLOR_SELECTION = new Color(0.3f, 0.6f, 1f, 1f);  // Blue for keyboard selection
        private static readonly Color BORDER_COLOR_ACTIVE = new Color(1f, 0.65f, 0.22f, 0f);   // Invisible (Orange #ffa83a) - kept invisible for now unless needed
        
        // Drop Shadow for Active Profile (editable)
        private const bool ENABLE_ACTIVE_SHADOW = false;        // Enable/disable drop shadow for active profile
        private const float SHADOW_OFFSET = 4f;                // Shadow offset distance
        private static readonly Color SHADOW_COLOR = new Color(0f, 0f, 0f, 0.4f); // Shadow color (black with 30% opacity)

        // --- NEW: Onboarding Banner ---
        public static string ONBOARDING_BANNER_PATH => $"{KromaAssetManager.GetKromaRootPath()}/Editor/Resources/UI/Onboarding_Banner.png";
        private const float ONBOARDING_BANNER_HEIGHT = 160f; // Height of the onboarding banner (adjust as needed)
        
        // Card Background Colors - moved to Styles.cs as adaptive Unity button colors
        // Uses --unity-colors-button-background, button-background-hover, button-background-focus
    }
}
