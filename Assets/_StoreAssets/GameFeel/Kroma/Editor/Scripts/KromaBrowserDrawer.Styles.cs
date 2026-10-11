using UnityEngine;
using UnityEditor;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// Styles partial - All GUIStyle fields and style initialization
    /// </summary>
    public partial class KromaBrowserDrawer
    {
        // --- COLOR FIELDS ---
        private Color _colActiveBg;
        private Color _colActiveText;
        private Color _colPreviewBg;
        private Color _colPreviewText;
        private Color _colCardHover;
        private Color _colCardDefault;
        private Color _colCardHover2;
        private Color _colCardDisabled;
        private Color _colContentBg; // Adaptive background color for gallery/list content area
        private Color _colCardActive; // Adaptive background color for active profile card

        // --- NEW STATUS COLOR FIELDS ---
        private Color _colStatusActiveBg;
        private Color _colStatusConfirmBtn;
        private Color _colStatusPreviewHeader;
        private Color _colStatusHoverGrey;
        private Color _colStatusCardBg;
        
        // --- STAR COLORS ---
        public Color _colStarDefault;
        public Color _colStarHover;
        public Color _colStarActive;

        // Cached constant colors
        private Color _cachedBannerBg;
        private Color _cachedWarningBg;
        private Color _cachedWarningText;
        private Color _cachedSeparator;

        // --- STYLE FIELDS ---
        private GUIStyle _nameOverlayStyle;
        private GUIStyle _badgeTextStyle;
        private GUIStyle _warningLabelStyle;
        private GUIStyle _listLabelStyle;
        private GUIStyle _setupTitleStyle;
        private GUIStyle _setupDescStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _placeholderStyle;
        
        // Custom Styles for 28px Height
        private GUIStyle _customSearchStyle;
        // LIST VIEW DISABLED FOR V1 - Uncomment to restore
        // private GUIStyle _customTabStyleLeft;  // For Gallery button
        // private GUIStyle _customTabStyleRight; // For List button

        // --- ICONS ---
        private Texture2D _texFavDefault;
        private Texture2D _texFavHover;
        private Texture2D _texFavActive;
        
        // LIST VIEW DISABLED FOR V1 - Uncomment to restore
        // private GUIContent[] _tabContents;

        // --- STATE FIELDS ---
        private Vector2 _scrollPos;
        // LIST VIEW DISABLED FOR V1 - Uncomment to restore
        // private int _selectedTab = 0;
        // Replaced string array with GUIContent array for Icons
        // private string[] _tabs = new string[] { KromaBrowserStrings.TAB_GALLERY, KromaBrowserStrings.TAB_LIST };
        private float _fixedBadgeWidth;
        private float _lastSelectedCardY = 0f; // Track Y position of selected card for auto-scroll
        private GameObject _lastSelectedGameObject; // Track volume selection changes

        /// <summary>
        /// Call this once on OnEnable to set up fonts and colors.
        /// </summary>
        public void InitStyles()
        {
            // 1. Badge Colors (from configurable constants)
            ColorUtility.TryParseHtmlString("#000000", out _colActiveText);     // Black Text
            ColorUtility.TryParseHtmlString("#ffa83a", out _colActiveBg);       // Orange #ffa83a
            ColorUtility.TryParseHtmlString(COLOR_PREVIEW_TEXT, out _colPreviewText);
            ColorUtility.TryParseHtmlString(COLOR_PREVIEW_BG, out _colPreviewBg);

            // 1b. New Status Colors
            ColorUtility.TryParseHtmlString(COLOR_STATUS_ACTIVE_BG, out _colStatusActiveBg);
            ColorUtility.TryParseHtmlString(COLOR_STATUS_CONFIRM_BTN, out _colStatusConfirmBtn);
            ColorUtility.TryParseHtmlString(COLOR_STATUS_PREVIEW_HEADER, out _colStatusPreviewHeader);
            ColorUtility.TryParseHtmlString(COLOR_STATUS_HOVER_GREY, out _colStatusHoverGrey);
            ColorUtility.TryParseHtmlString(COLOR_CARD_BG, out _colStatusCardBg);
            
            // Star Colors
            ColorUtility.TryParseHtmlString(COLOR_STAR_DEFAULT, out _colStarDefault);
            ColorUtility.TryParseHtmlString(COLOR_STAR_HOVER, out _colStarHover);
            ColorUtility.TryParseHtmlString(COLOR_STAR_ACTIVE, out _colStarActive);

            // Cache standard colors
            ColorUtility.TryParseHtmlString(BANNER_BG_COLOR, out _cachedBannerBg);
            ColorUtility.TryParseHtmlString(WARNING_BG_COLOR, out _cachedWarningBg);
            ColorUtility.TryParseHtmlString(WARNING_TEXT_COLOR, out _cachedWarningText);
            ColorUtility.TryParseHtmlString(SEPARATOR_COLOR, out _cachedSeparator);
            
            // --- Custom Fav Icons ---
            string kromaRoot = KromaAssetManager.GetKromaRootPath();
            _texFavDefault = AssetDatabase.LoadAssetAtPath<Texture2D>($"{kromaRoot}/Editor/Resources/UI/Editor_UI/Fav_Default.png");
            _texFavHover = AssetDatabase.LoadAssetAtPath<Texture2D>($"{kromaRoot}/Editor/Resources/UI/Editor_UI/Fav_Hover.png");
            _texFavActive = AssetDatabase.LoadAssetAtPath<Texture2D>($"{kromaRoot}/Editor/Resources/UI/Editor_UI/Fav_Active.png");
            
            // Card Background Colors - Using Unity's adaptive button colors
            // These automatically switch between Pro (dark) and Personal (light) skins
            if (EditorGUIUtility.isProSkin)
            {
                // Dark mode colors
                ColorUtility.TryParseHtmlString("#141414", out _colCardDefault);
                _colCardDefault.a = CARD_OPACITY_DEFAULT; // Ensure opacity is applied
                
                // User requested specific hover color #9d9d9d
                ColorUtility.TryParseHtmlString(COLOR_CARD_HOVER_BG, out _colCardHover2); // #9d9d9d
                _colCardHover2.a = CARD_OPACITY_HOVER; 

                ColorUtility.TryParseHtmlString("#ffa83a", out _colCardActive);             // Active card: #ffa83a
                _colCardDisabled = new Color(0.24f, 0.24f, 0.24f, 0.5f);                    // --unity-colors-button-background with alpha 0.5
            }
            else
            {
                // Light mode colors
                _colCardDefault = new Color(0.76f, 0.76f, 0.76f, CARD_OPACITY_DEFAULT);     // --unity-colors-button-background
                _colCardHover2 = new Color(0.70f, 0.70f, 0.70f, CARD_OPACITY_HOVER);        // --unity-colors-button-background-hover
                ColorUtility.TryParseHtmlString("#ffa83a", out _colCardActive);             // Active card: #ffa83a
                _colCardDisabled = new Color(0.76f, 0.76f, 0.76f, 0.5f);                    // --unity-colors-button-background with alpha 0.5
            }
            
            // Hover Highlight (from configurable constant)
            _colCardHover = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, CARD_HOVER_OPACITY) : new Color(0, 0, 0, CARD_HOVER_OPACITY);

            // Adaptive Content Background
            if (EditorGUIUtility.isProSkin)
            {
                ColorUtility.TryParseHtmlString("#383838", out _colContentBg);
            }
            else
            {
                _colContentBg = new Color(0.78f, 0.78f, 0.78f, 1f); // Light mode keep as is
            }

            // 2. Styles
            // 2. Styles
            _nameOverlayStyle = new GUIStyle(EditorStyles.label); // Not Bold
            _nameOverlayStyle.alignment = TextAnchor.MiddleCenter;
            _nameOverlayStyle.normal.textColor = Color.white;
            _nameOverlayStyle.hover.textColor = Color.white; // Stable Hover
            _nameOverlayStyle.fontSize = 12; 

            _badgeTextStyle = new GUIStyle(EditorStyles.miniLabel);
            _badgeTextStyle.alignment = TextAnchor.MiddleCenter;
            _badgeTextStyle.fontStyle = FontStyle.Bold;
            _badgeTextStyle.fontSize = 9; 
            _badgeTextStyle.normal.textColor = Color.white;
            _badgeTextStyle.hover.textColor = Color.white; // Stable Hover
            
            // Calculate fixed width for badges based on the widest text ("Preview") + padding
            float widthPreview = _badgeTextStyle.CalcSize(new GUIContent(KromaBrowserStrings.BADGE_PREVIEW)).x;
            _fixedBadgeWidth = widthPreview + 16f; // More breathing room inside badge

            _warningLabelStyle = new GUIStyle(EditorStyles.helpBox); 
            _warningLabelStyle.fontSize = 12;
            _warningLabelStyle.alignment = TextAnchor.MiddleLeft;

            _listLabelStyle = new GUIStyle(EditorStyles.label);
            _listLabelStyle.alignment = TextAnchor.MiddleLeft;
            _listLabelStyle.fontSize = 12;
            _listLabelStyle.normal.textColor = Color.white;
            
            _setupTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            _setupTitleStyle.fontSize = 18;
            _setupTitleStyle.alignment = TextAnchor.MiddleCenter;
            
            _setupDescStyle = new GUIStyle(EditorStyles.label);
            _setupDescStyle.fontSize = 12;
            _setupDescStyle.alignment = TextAnchor.MiddleCenter;
            _setupDescStyle.wordWrap = true;

            _cardStyle = new GUIStyle(EditorStyles.helpBox);
            _cardStyle.padding = new RectOffset(0,0,0,0);

            _placeholderStyle = new GUIStyle(EditorStyles.label);
            _placeholderStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Transparent Grey
            _placeholderStyle.fontStyle = FontStyle.Italic;
            _placeholderStyle.fontSize = 13; // Match Search Field Size
            _placeholderStyle.alignment = TextAnchor.MiddleLeft;

            // --- Custom Styles for Larger Controls ---
            
            // Search: Use ToolbarSearchField for proper look
            GUIStyle nativeSearch = EditorStyles.toolbarSearchField; 
            if (nativeSearch != null) 
                _customSearchStyle = new GUIStyle(nativeSearch);
            else 
                _customSearchStyle = new GUIStyle(EditorStyles.textField);
            
            // Set fixed height to match buttons exactly
            _customSearchStyle.fixedHeight = SEARCH_HEIGHT;
            _customSearchStyle.alignment = TextAnchor.MiddleLeft;
            _customSearchStyle.fontSize = 12; 
            
            // ADJUSTED PADDING: Reduced from 32 to 20 for better icon-to-text spacing
            _customSearchStyle.padding.left = 20; // 20px allows space for search icon
            
            // Allow vertical centering
            _customSearchStyle.padding.top = 0;
            _customSearchStyle.padding.bottom = 0;
            
            // Push content if needed
            // _customSearchStyle.contentOffset = new Vector2(0, 0);

            // LIST VIEW DISABLED FOR V1 - Uncomment to restore
            /*
            // Tabs: Create connected button styles (left and right)
            _customTabStyleLeft = new GUIStyle(EditorStyles.miniButtonLeft);
            _customTabStyleLeft.fixedHeight = 0; // Unset to allow GUILayout.Height
            _customTabStyleLeft.fontSize = 12;
            _customTabStyleLeft.alignment = TextAnchor.MiddleCenter;
            _customTabStyleLeft.padding.left = 8;
            _customTabStyleLeft.padding.right = 8;

            _customTabStyleRight = new GUIStyle(EditorStyles.miniButtonRight);
            _customTabStyleRight.fixedHeight = 0; // Unset to allow GUILayout.Height
            _customTabStyleRight.fontSize = 12;
            _customTabStyleRight.alignment = TextAnchor.MiddleCenter;
            _customTabStyleRight.padding.left = 8;
            _customTabStyleRight.padding.right = 8; 

            // --- Tab Contents (Text Only, No Icons) ---
            _tabContents = new GUIContent[]
            {
                new GUIContent(KromaBrowserStrings.TAB_GALLERY),
                new GUIContent(KromaBrowserStrings.TAB_LIST)
            };
            */
        }
    }
}
