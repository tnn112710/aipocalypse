using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// The "Viewer" of the tool.
    /// Handles all GUI drawing, Styles, Colors, and Layouts.
    /// Updated to match Unity Design System (Foundations) - Text Version.
    /// NOTE: Constants are in KromaBrowserDrawer.Constants.cs
    /// NOTE: Styles and fields are in KromaBrowserDrawer.Styles.cs
    /// </summary>
    public partial class KromaBrowserDrawer
    {

        public void Draw(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            if (_badgeTextStyle == null) InitStyles();
            // LIST VIEW DISABLED FOR V1
            // if (_tabContents == null) InitStyles();

            // --- Detect volume selection changes ---
            GameObject currentSelection = Selection.activeGameObject;
            if (currentSelection != _lastSelectedGameObject)
            {
                // Volume selection changed - reset everything
                logic.ResetSelection(); // Hide blue border (selection = -1)
                _scrollPos = Vector2.zero; // Reset scroll to top
                _lastSelectedGameObject = currentSelection;
                
                // Don't auto-select - let user navigate with arrow keys to show blue border
            }

            // Draw Banner
            // 1. Responsive Header (Replaces Banner + Toolbar)
            // SHOW ONLY if requirements met (initialized)
            if (logic.AllRequirementsMet())
            {
                DrawResponsiveHeader(position, logic, window);
            }

            // 2. Setup vs Main View
            // Check ALL requirements (camera, light, volume), not just volumes
            if (!logic.AllRequirementsMet())
            {
                DrawSetupView(logic, window);
            }
            else
            {
                DrawMainInterface(position, logic, window);
            }
            
            // 3. Draw temporary notification overlays on top of everything
            DrawNotificationOverlay(position, window);
        }

        private void DrawSetupView(KromaBrowserLogic logic, EditorWindow window)
        {
            // Initialize dependencies if not done yet
            if (logic.Dependencies == null)
            {
                logic.InitializeDependencies();
            }
            
            // ONBOARDING BANNER (Draw flush at the top)
            Texture2D onboardingBanner = AssetDatabase.LoadAssetAtPath<Texture2D>(ONBOARDING_BANNER_PATH);
            if (onboardingBanner != null)
            {
                // Full width at top
                float aspectRatio = (float)onboardingBanner.width / onboardingBanner.height;
                float bannerWidth = window.position.width; // Use full window width
                float bannerHeight = bannerWidth / aspectRatio;
                
                // Reserve space for banner
                Rect bannerRect = GUILayoutUtility.GetRect(bannerWidth, bannerHeight);
                // Force rect to start at 0,0 relative to current layout group (setup view)
                // Since we are at start of DrawSetupView, this should be effectively at top
                
                // Draw texture
                GUI.DrawTexture(bannerRect, onboardingBanner, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback text if banner missing
                GUILayout.Space(16);
                GUILayout.Label(KromaBrowserStrings.SETUP_SCREEN_TITLE, EditorStyles.boldLabel);
                GUILayout.Label(KromaBrowserStrings.SETUP_SCREEN_SUBTITLE, EditorStyles.miniLabel);
                GUILayout.Space(16);
            }
            
            // CENTERING CONTAINER FOR CONTENT
            // GUILayout.FlexibleSpace();            // CENTERING CONTAINER FOR CONTENT
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Calculate responsive width:
            // Use window width minus horizontal padding (e.g. 20px total), but cap at 450px max width
            float contentWidth = Mathf.Min(window.position.width - 40f, 450f);
            
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));

            // Content padding top (reduced slightly since it's now top-aligned)
            GUILayout.Space(20);

            // Check URP status
            bool urpActive = logic.IsURPActive();
            
            if (!urpActive)
            {
                // URP Error Banner
                Rect errorRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(errorRect, new Color(0.8f, 0.2f, 0.2f, 0.3f));
                
                Rect errorTextRect = new Rect(errorRect.x + 8, errorRect.y, errorRect.width - 16, errorRect.height);
                GUI.Label(errorTextRect, KromaBrowserStrings.SETUP_URP_ERROR_BANNER, EditorStyles.wordWrappedLabel);
                
                GUILayout.Space(8);
            }

            // Scene Objects Section
            // Greeting Text
            GUILayout.Label(KromaBrowserStrings.SETUP_GREETING_HEADLINE, EditorStyles.boldLabel);
            GUILayout.Space(2);
            
            // Body text with wrapping and lighter color/smaller style if needed
            // Using standard label with WordWrap for body
            GUIStyle bodyStyle = new GUIStyle(EditorStyles.label);
            bodyStyle.wordWrap = true;
            bodyStyle.fontSize = 12; // Slightly larger than mini, standard size
            // bodyStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f); // Optional: if specific grey needed
            
            GUILayout.Label(KromaBrowserStrings.SETUP_GREETING_BODY, bodyStyle);
            GUILayout.Space(16); // Gap before section header

            // REMOVED "Scene Objects" Title as per request
            // GUILayout.Label(KromaBrowserStrings.SETUP_SECTION_SCENE_OBJECTS, EditorStyles.boldLabel);
            // GUILayout.Space(4);

            GUI.enabled = urpActive;

            // Draw dependency fields
            DrawDependencyField(KromaBrowserStrings.SETUP_LABEL_CAMERA, logic.DetectedCamera, typeof(Camera), logic);
            DrawDependencyField(KromaBrowserStrings.SETUP_LABEL_LIGHT, logic.DetectedLight, typeof(Light), logic);
            DrawDependencyField(KromaBrowserStrings.SETUP_LABEL_VOLUME, logic.DetectedVolume, typeof(Volume), logic);

            GUI.enabled = true;

            GUILayout.Space(12);

            // Status Message
            string statusMessage = GetStatusMessage(logic, urpActive);
            Rect statusRect = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true)); // Increased height slightly for better vertical centering
            
            // Draw Container: 1px Black Border + Opaque Grey Background
            EditorGUI.DrawRect(statusRect, new Color(0.1f, 0.1f, 0.1f, 1f)); // Dark border
            Rect innerBgRect = new Rect(statusRect.x + 1, statusRect.y + 1, statusRect.width - 2, statusRect.height - 2);
            EditorGUI.DrawRect(innerBgRect, new Color(0.22f, 0.22f, 0.22f, 1f)); // Opaque Background (standard dark unity grey)
            
            // Content Area (with padding)
            Rect statusContentRect = new Rect(statusRect.x + 10, statusRect.y, statusRect.width - 20, statusRect.height);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            
            if (logic.AllRequirementsMet())
            {
                GUI.Label(statusContentRect, KromaBrowserStrings.SYMBOL_CHECKMARK + statusMessage, statusStyle);
            }
            else
            {
                // Draw Info Icon + Text
                float iconSize = 18f;
                // Icon (using small info icon as requested, not large banner icon)
                Rect iconRect = new Rect(statusContentRect.x, statusContentRect.y + (statusContentRect.height - iconSize)/2, iconSize, iconSize);
                GUI.Label(iconRect, EditorGUIUtility.IconContent("console.infoicon.sml"));
                
                // Text (shifted right)
                Rect msgRect = new Rect(iconRect.xMax + 6, statusContentRect.y, statusContentRect.width - iconSize - 6, statusContentRect.height);
                GUI.Label(msgRect, statusMessage, statusStyle);
            }

            GUILayout.Space(12);

            // Initialize Button
            GUI.enabled = urpActive;
            if (GUILayout.Button(KromaBrowserStrings.SETUP_BTN_INITIALIZE, GUILayout.Height(32)))
            {
                logic.InitializeKromaScene();
                logic.DetectDependencies(); // Refresh after initialization
                
                // RESIZE WINDOW TO FULL SIZE
                // Calculate ideal full width (same formula as in KromaProfileBrowser.ShowWindow)
                float idealWidth = (230f * 4) + (8f * 3) + (20f * 2) + 30; // ~1000px
                
                // Get current position
                Rect currentPos = window.position;
                
                // Validate if width is actually different to avoid jitter
                if (Mathf.Abs(currentPos.width - idealWidth) > 5f)
                {
                    // Expand width and RESTORE HEIGHT to standard (800f)
                    // Optional: Center expansion or expand to right? Standard is expand right.
                    window.position = new Rect(currentPos.x, currentPos.y, idealWidth, 800f);
                }
                
                window.ShowNotification(new GUIContent(KromaBrowserStrings.SETUP_NOTIFICATION_INITIALIZED));
            }
            GUI.enabled = true;

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawNotificationOverlay(Rect position, EditorWindow window)
        {
            var browser = window as KromaProfileBrowser;
            if (browser == null || !browser.IsNotificationActive) return;

            // Calculate fade out (1.5s solid, 0.5s fade to 0)
            double elapsed = EditorApplication.timeSinceStartup - browser.NotificationStartTime;
            float solidDuration = 1.5f;
            float fadeDuration = 0.5f;
            float totalDuration = solidDuration + fadeDuration;
            
            float alpha = 1f;
            if (elapsed > solidDuration)
            {
                // Linearly interpolate alpha from 1 to 0 over the fade duration
                float fadeTime = (float)(elapsed - solidDuration);
                alpha = Mathf.Clamp01(1f - (fadeTime / fadeDuration));
            }

            // Stop drawing if fully faded
            if (alpha <= 0f) return;

            // Dimensions and layout
            string message = browser.ActiveNotificationMessage;
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            labelStyle.alignment = TextAnchor.MiddleLeft; // Aligned left next to icon
            labelStyle.fontSize = 13;

            // Load icon based on notification type
            GUIContent iconContent;
            Color iconColor;

            if (browser.ActiveNotificationType == KromaNotificationType.ProfileApplied)
            {
                iconContent = EditorGUIUtility.IconContent("FilterSelectedOnly"); // Unity checkmark icon
                iconColor = new Color(37f/255f, 175f/255f, 37f/255f, alpha); // Vibrant green
            }
            else
            {
                iconContent = EditorGUIUtility.IconContent("Favorite");
                Color baseColor = (browser.ActiveNotificationType == KromaNotificationType.FavoriteAdded) ? _colStarActive : Color.white;
                iconColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }

            float iconSize = 16f;
            float iconPadding = 6f; // Space between icon and text

            // Calculate width based on text length + icon width + padding
            float textWidth = labelStyle.CalcSize(new GUIContent(message)).x;
            float pillWidth = textWidth + iconSize + iconPadding + 40f; // 20px padding on each side
            float pillHeight = 36f;
            
            // Center in the lower third of the window
            float xPos = (position.width - pillWidth) / 2f;
            float yPos = position.height - 100f; // Floating above the bottom edge
            
            Rect pillRect = new Rect(xPos, yPos, pillWidth, pillHeight);
            
            // Draw background pill (80% black max)
            Color bgColor = new Color(0f, 0f, 0f, alpha * 0.8f);
            EditorGUI.DrawRect(pillRect, bgColor);
            
            // --- Draw contents ---
            // 1. Draw Icon
            Rect iconRect = new Rect(pillRect.x + 20f, pillRect.y + (pillHeight - iconSize) / 2f, iconSize, iconSize);
            Color oldColor = GUI.color;
            GUI.color = iconColor;
            GUI.Label(iconRect, iconContent);
            GUI.color = oldColor;
            
            // 2. Draw Text
            Rect textRect = new Rect(iconRect.xMax + iconPadding, pillRect.y, textWidth, pillHeight);
            GUI.Label(textRect, message, labelStyle);
        }

        private void DrawDependencyField(string label, Object detectedObject, System.Type objectType, KromaBrowserLogic logic)
        {
            GUILayout.BeginHorizontal();
            
            // Label
            GUILayout.Label(label, GUILayout.Width(100));
            
            // Object Field
            EditorGUI.BeginChangeCheck();
            Object newObject = EditorGUILayout.ObjectField(detectedObject, objectType, true);
            if (EditorGUI.EndChangeCheck())
            {
                // User manually assigned an object - update detection AND configure it
                if (label == KromaBrowserStrings.SETUP_LABEL_CAMERA)
                {
                    logic.SetDetectedCamera(newObject);
                }
                else if (label == KromaBrowserStrings.SETUP_LABEL_LIGHT)
                {
                    logic.SetDetectedSun(newObject);
                }
                else if (label == KromaBrowserStrings.SETUP_LABEL_VOLUME)
                {
                    logic.SetDetectedVolume(newObject);
                }
            }
            
            GUILayout.Space(8);

            // Status Badge (Right Side)
            float badgeWidth = 65f; // Reduced from 70 by 5 as requested
            // Height 18px as requested for pixel-perfect alignment
            Rect badgeRect = GUILayoutUtility.GetRect(badgeWidth, 18f, GUILayout.Width(badgeWidth), GUILayout.Height(18f));
            
            string statusText = "";
            Color badgeColor;

            if (detectedObject != null)
            {
                // Green - #25af25 -> (37, 175, 37)
                badgeColor = new Color(37f/255f, 175f/255f, 37f/255f, 1f);
                statusText = "Detected";
            }
            else
            {
                // Blue - #1674be -> (22, 116, 190)
                badgeColor = new Color(22f/255f, 116f/255f, 190f/255f, 1f);
                statusText = "Missing";
            }

            // Draw Background
            EditorGUI.DrawRect(badgeRect, badgeColor);

            // Draw Text
            // Bold, White, Left Aligned
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.boldLabel);
            badgeStyle.normal.textColor = Color.white;
            badgeStyle.alignment = TextAnchor.MiddleLeft;
            badgeStyle.fontSize = 11;
            badgeStyle.padding = new RectOffset(6, 0, 0, 0); // Keep padding

            GUI.Label(badgeRect, statusText, badgeStyle);
            
            GUILayout.EndHorizontal();
            GUILayout.Space(4); // Reverted to 4 as 2 was too tight
        }

        private string GetStatusMessage(KromaBrowserLogic logic, bool urpActive)
        {
            if (!urpActive)
            {
                return KromaBrowserStrings.SETUP_STATUS_URP_REQUIRED;
            }
            
            if (logic.AllRequirementsMet())
            {
                return KromaBrowserStrings.SETUP_STATUS_READY;
            }
            
            return KromaBrowserStrings.SETUP_STATUS_AUTO_CREATE;
        }

        private string GetDependencyTooltip(string label)
        {
            if (label == KromaBrowserStrings.SETUP_LABEL_CAMERA)
                return KromaBrowserStrings.SETUP_TOOLTIP_CAMERA;
            else if (label == KromaBrowserStrings.SETUP_LABEL_LIGHT)
                return KromaBrowserStrings.SETUP_TOOLTIP_LIGHT;
            else if (label == KromaBrowserStrings.SETUP_LABEL_VOLUME)
                return KromaBrowserStrings.SETUP_TOOLTIP_VOLUME;
            else
                return "";
        }

        private void DrawMainInterface(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            // 1. Warning Banner (Custom Styled - Full Width, Black BG, Orange Text)
            bool isValidSelection = logic.HasValidSelection();
            if (!isValidSelection)
            {
                // Full width banner
                Rect bannerRect = GUILayoutUtility.GetRect(position.width, WARNING_BANNER_HEIGHT);
                bannerRect.x = 0;
                bannerRect.width = position.width;
                
                // Background color from constant with opacity
                // Background color from constant with opacity
                Color warningBg = _cachedWarningBg;
                warningBg.a = WARNING_BG_OPACITY; // Apply opacity
                EditorGUI.DrawRect(bannerRect, warningBg);
                
                // Calculate centered content area
                GUIContent warningContent = new GUIContent(KromaBrowserStrings.WARNING_NO_SELECTION);
                GUIStyle textStyle = new GUIStyle(EditorStyles.label);
                
                // Text color from constant
                // Text color from constant
                textStyle.normal.textColor = _cachedWarningText;
                textStyle.fontSize = 12;
                
                Vector2 textSize = textStyle.CalcSize(warningContent);
                float totalWidth = WARNING_ICON_SIZE + 8 + textSize.x;
                float startX = (position.width - totalWidth) / 2;
                
                // Draw warning icon (size from constant, with adjustable offset)
                float iconY = bannerRect.y + (bannerRect.height - WARNING_ICON_SIZE) / 2 + WARNING_ICON_OFFSET_Y;
                Rect iconRect = new Rect(startX, iconY, WARNING_ICON_SIZE, WARNING_ICON_SIZE);
                GUI.Label(iconRect, EditorGUIUtility.IconContent("console.warnicon.sml"));
                
                // Draw text
                Rect textRect = new Rect(startX + WARNING_ICON_SIZE + 8, bannerRect.y, textSize.x, bannerRect.height);
                textStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(textRect, KromaBrowserStrings.WARNING_NO_SELECTION, textStyle);
            }

            // 1b. Play Mode Warning Banner
            if (EditorApplication.isPlaying)
            {
                // Full width banner
                Rect bannerRect = GUILayoutUtility.GetRect(position.width, WARNING_BANNER_HEIGHT);
                bannerRect.x = 0;
                bannerRect.width = position.width;
                
                // Yellow/orange background for Play Mode warning
                Color playModeBg = new Color(1f, 0.6f, 0f, 0.15f); // Orange with transparency
                EditorGUI.DrawRect(bannerRect, playModeBg);
                
                // Calculate centered content area
                GUIContent warningContent = new GUIContent("⚠️ Play Mode Active - Exit Play Mode to change profiles.");
                GUIStyle textStyle = new GUIStyle(EditorStyles.label);
                
                // Orange text color
                textStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
                textStyle.fontSize = 12;
                textStyle.fontStyle = FontStyle.Bold;
                
                Vector2 textSize = textStyle.CalcSize(warningContent);
                float startX = (position.width - textSize.x) / 2;
                
                // Draw text (centered)
                Rect textRect = new Rect(startX, bannerRect.y, textSize.x, bannerRect.height);
                textStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(textRect, warningContent, textStyle);
            }

            // --- KEYBOARD SHORTCUTS HANDLING ---
            HandleKeyboardInput(logic, window, position);



            // 3. Content Area with dark background
            // Apply disabled opacity if no valid selection
            Color oldColor = GUI.color;
            if (!isValidSelection)
            {
                GUI.color = new Color(1f, 1f, 1f, CARD_OPACITY_DISABLED);
            }
            
            EditorGUI.BeginDisabledGroup(!isValidSelection);
            
            // Draw dark background behind content
            // Get current Y position after toolbar/header
            Rect currentRect = GUILayoutUtility.GetRect(0, 0);
            Rect contentBgRect = new Rect(0, currentRect.y, position.width, position.height - currentRect.y);
            // Use adaptive background color (switches between Pro/Light skin)
            EditorGUI.DrawRect(contentBgRect, _colContentBg);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            // Add top padding to content
            GUILayout.Space(CONTENT_TOP_PADDING);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(PADDING);
            GUILayout.BeginVertical();

            // Switch between Gallery and Settings views
            if (logic.ShowSettingsView)
            {
                DrawSettingsView(position, logic, window);
            }
            else
            {
                DrawGalleryView(position, logic, window);
            }

            GUILayout.EndVertical();
            GUILayout.Space(PADDING);
            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUI.EndDisabledGroup();
            
            // Restore GUI.color
            GUI.color = oldColor;

            // 4. Footer
            DrawFooter(position, logic, window);
        }



        private void DrawSearchField(KromaBrowserLogic logic)
        {
            // Reserve layout space
            // Reserve layout space with minimum width to prevent collapse
            Rect rect = GUILayoutUtility.GetRect(100f, SEARCH_HEIGHT, GUILayout.ExpandWidth(true));
            
            // Handle Focus Name
            string controlName = "KromaSearchField";
            GUI.SetNextControlName(controlName);

            // Draw Field using Custom Style
            string newSearch = EditorGUI.TextField(rect, logic.SearchTerm, _customSearchStyle);
            
            // Draw Placeholder Overlay (If empty and not focused)
            if (string.IsNullOrEmpty(newSearch) && GUI.GetNameOfFocusedControl() != controlName)
            {
                // Offset text to account for the padding (20px)
                Rect placeRect = rect;
                placeRect.x += 18f; // Slightly less than 20 to align nicely with the icon
                placeRect.width -= 18f;
                // Center vertically manually if needed, or let style handle it
                GUI.Label(placeRect, KromaBrowserStrings.SEARCH_PLACEHOLDER, _placeholderStyle);
            }

            // Detect clicks outside to unfocus
            if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
            {
                if (GUI.GetNameOfFocusedControl() == controlName)
                {
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }

            if (newSearch != logic.SearchTerm)
            {
                logic.SearchTerm = newSearch;
                logic.ApplySearchFilter();
            }
        }

        private void DrawGalleryView(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            // Show empty state if favorites filter is active but no favorites exist
            if (logic.CurrentSourceFilter == ProfileSourceFilter.Favorites && logic.FilteredProfiles.Count == 0)
            {
                GUILayout.FlexibleSpace();
                
                // 1. Headline
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUIStyle headlineStyle = new GUIStyle(EditorStyles.boldLabel);
                headlineStyle.fontSize = 18;
                headlineStyle.alignment = TextAnchor.MiddleCenter;
                headlineStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
                GUILayout.Label(KromaBrowserStrings.MSG_NO_FAVORITES_TITLE, headlineStyle, GUILayout.Width(400));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                // 2. Body Text
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUIStyle bodyStyle = new GUIStyle(EditorStyles.label);
                bodyStyle.fontSize = 13;
                bodyStyle.alignment = TextAnchor.MiddleCenter;
                bodyStyle.wordWrap = true;
                bodyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label(KromaBrowserStrings.MSG_NO_FAVORITES_BODY, bodyStyle, GUILayout.Width(400));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                
                // 3. Action Button
                GUILayout.Space(25);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                GUIStyle backBtnStyle = new GUIStyle(GUI.skin.button);
                backBtnStyle.fontSize = 13;
                
                if (GUILayout.Button(KromaBrowserStrings.BTN_BROWSE_PROFILES, backBtnStyle, GUILayout.Width(150), GUILayout.Height(34)))
                {
                    logic.CurrentSourceFilter = ProfileSourceFilter.AllProfiles;
                    logic.ApplySearchFilter();
                    logic.ResetSelection();
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                
                GUILayout.FlexibleSpace();
                return;
            }

            // Show empty state if My Profiles filter is active but no profiles exist
            if (logic.CurrentSourceFilter == ProfileSourceFilter.MyProfiles && logic.FilteredProfiles.Count == 0)
            {
                GUILayout.FlexibleSpace();
                
                // 1. Headline
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUIStyle headlineStyle = new GUIStyle(EditorStyles.boldLabel);
                headlineStyle.fontSize = 18;
                headlineStyle.alignment = TextAnchor.MiddleCenter;
                headlineStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
                GUILayout.Label(KromaBrowserStrings.MSG_NO_MY_PROFILES_TITLE, headlineStyle, GUILayout.Width(400));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                // 2. Body Text (RichText enabled for the link)
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUIStyle bodyStyle = new GUIStyle(EditorStyles.label);
                bodyStyle.fontSize = 13;
                bodyStyle.alignment = TextAnchor.MiddleCenter;
                bodyStyle.wordWrap = true;
                bodyStyle.richText = true;
                bodyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                
                // Parse the text to create a clickable 'Learn more' link 
                // We'll just display it as text for now, but style it like a link
                string bodyText = KromaBrowserStrings.MSG_NO_MY_PROFILES_BODY.Replace("[Learn more]", "<color=#4A90E2>Learn more</color>");
                
                if (GUILayout.Button(bodyText, bodyStyle, GUILayout.Width(400))) 
                {
                    // If they click the text, assuming they want to click Learn more
                    Application.OpenURL("https://docs.faylabs.com/kroma"); // Fallback documentation link
                }
                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(25);

                // 3. Action Buttons
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Create Profile Button
                GUIStyle createBtnStyle = new GUIStyle(GUI.skin.button);
                createBtnStyle.fontSize = 13;
                createBtnStyle.fontStyle = FontStyle.Bold;
                
                if (GUILayout.Button(KromaBrowserStrings.BTN_CREATE_PROFILE, createBtnStyle, GUILayout.Width(150), GUILayout.Height(34)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Kroma/Create Profile");
                }

                GUILayout.Space(15);
                
                // Go Back Button
                GUIStyle backBtnStyle = new GUIStyle(GUI.skin.button);
                backBtnStyle.fontSize = 13;
                
                if (GUILayout.Button(KromaBrowserStrings.BTN_GO_BACK, backBtnStyle, GUILayout.Width(100), GUILayout.Height(34)))
                {
                    logic.CurrentSourceFilter = ProfileSourceFilter.AllProfiles;
                    logic.ApplySearchFilter();
                    logic.ResetSelection();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
                return;
            }
            
            // --- VIRTUALIZATION LAYOUT CALCULATION ---
            
            // 1. Calculate Dimensions
            float containerWidth = position.width - (PADDING * 2) - 20; // Subtract padding + scrollbar approximation
            
            // Determine columns based on minimum card width
            int columns = Mathf.FloorToInt(containerWidth / (CARD_WIDTH_MIN + SPACING));
            if (columns < 1) columns = 1;

            // Calculate actual card width to fill space
            float availableSpace = containerWidth - ((columns - 1) * SPACING);
            float calculatedCardWidth = availableSpace / columns;
            
            // Clamp max width: Limit single-card expansion
            float xOffset = 0;
            if (calculatedCardWidth > CARD_WIDTH_MAX)
            {
                calculatedCardWidth = CARD_WIDTH_MAX;
                // If clamped, we center the grid in the container
                float totalRowWidth = (columns * calculatedCardWidth) + ((columns - 1) * SPACING);
                xOffset = (containerWidth - totalRowWidth) / 2;
            }

            // Make height proportional to width (1:1 square aspect ratio)
            float calculatedCardHeight = calculatedCardWidth;

            int totalCards = logic.FilteredProfiles.Count;
            int totalRows = Mathf.CeilToInt((float)totalCards / columns);
            float totalHeight = (totalRows * calculatedCardHeight) + ((totalRows - 1) * SPACING) + SPACING; // +SPACING for bottom padding

            // 2. Reserve Layout Space (Single GUILayout Call)
            // This tells the scrollview how huge the content is without drawing it all.
            Rect canvasRect = GUILayoutUtility.GetRect(containerWidth, totalHeight);
            
            // 3. Determine Visible Range (Culling)
            // The scroll position tells us where the "camera" is.
            float scrollY = _scrollPos.y; 
            float viewHeight = position.height;
            
            // Calculate which rows are visible
            float rowHeightWithSpacing = calculatedCardHeight + SPACING;
            
            int firstVisibleRow = Mathf.FloorToInt(scrollY / rowHeightWithSpacing);
            int lastVisibleRow = Mathf.CeilToInt((scrollY + viewHeight) / rowHeightWithSpacing);
            
            // Clamp range
            firstVisibleRow = Mathf.Max(0, firstVisibleRow);
            lastVisibleRow = Mathf.Min(totalRows - 1, lastVisibleRow);
            
            // 4. Draw Only Visible Cards
            // Iterate only through the rows that are actually on screen (plus maybe 1 buffer row)
            for (int r = firstVisibleRow; r <= lastVisibleRow; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int index = (r * columns) + c;
                    
                    // Calculate precise position for this card using GUI coordinates (relative to canvasRect)
                    float cardX = canvasRect.x + xOffset + (c * (calculatedCardWidth + SPACING));
                    float cardY = canvasRect.y + (r * (calculatedCardHeight + SPACING));
                    
                    Rect cardRect = new Rect(cardX, cardY, calculatedCardWidth, calculatedCardHeight);

                    if (index < totalCards) 
                    {
                        // Draw actual card
                        DrawCardGUI(cardRect, logic.FilteredProfiles[index], logic, window, index);
                    }
                    else if (index < totalRows * columns) // Don't draw past the very last row
                    {
                        // Draw empty dummy slot for alignment
                        EditorGUI.DrawRect(cardRect, _colCardDefault);
                    }
                }
            }
        }

        private void DrawCardGUI(Rect rect, KromaProfile profile, KromaBrowserLogic logic, EditorWindow window, int profileIndex)
        {
            // Remove GetRect (rect is passed in)
            // Rect rect = GUILayoutUtility.GetRect(width, CARD_HEIGHT);
            
            // --- 1. Card Background (using configurable color) ---
            bool isSelected = (logic.SelectedProfileIndex == profileIndex);
            // Fix: Check hover on ALL events, not just Repaint, so "showButtons" logic is consistent
            bool isHovering = rect.Contains(Event.current.mousePosition); 
            Color cardBgColor = isHovering ? _colCardHover2 : _colCardDefault;
            
            // Check for duplicate warning
            bool isDuplicate = false;
            string duplicateSource = "";
            if (logic.DuplicateWarnings != null && logic.DuplicateWarnings.TryGetValue(profile, out duplicateSource))
            {
                isDuplicate = true;
                // Dim non-selected duplicate cards
                if (!isSelected) cardBgColor.a = CARD_OPACITY_DISABLED;
            }
            
            EditorGUI.DrawRect(rect, cardBgColor);
            
            // Track position for auto-scroll if selected
            if (isSelected)
            {
                _lastSelectedCardY = rect.y;
            }

            // --- Layout Calculation ---
            // NEW: Image fills entire card height, buttons overlay at bottom
            Rect imageRect = new Rect(rect.x + CARD_PAD_SIDES, rect.y + CARD_PAD_TOP, rect.width - (CARD_PAD_SIDES * 2), rect.height - CARD_PAD_TOP - CARD_PAD_BOTTOM);
            
            // Buttons at bottom spanning full width (same padding as star/overlay)
            float btnPadding = 4f; // Match star padding for consistency
            float btnGap = 4f;
            float btnY = imageRect.yMax - BUTTON_HEIGHT - 4f; // Reduced from 8px to 4px for tighter spacing
            float totalBtnWidth = imageRect.width - (btnPadding * 2);
            float btnW = (totalBtnWidth - btnGap) / 2; // Two buttons side by side
            Rect btnLeft = new Rect(imageRect.x + btnPadding, btnY, btnW, BUTTON_HEIGHT);
            Rect btnRight = new Rect(btnLeft.xMax + btnGap, btnY, btnW, BUTTON_HEIGHT);


            // --- State ---
            bool isActive = logic.IsProfileActiveOnSelection(profile.volumeProfile);
            bool isPreviewing = logic.IsPreviewing && logic.ActivePreviewProfile == profile;

            // --- Draw drop shadow FIRST (behind everything) ---
            if (isActive && ENABLE_ACTIVE_SHADOW)
            {
                Rect shadowRect = new Rect(
                    rect.x + SHADOW_OFFSET,
                    rect.y + SHADOW_OFFSET,
                    rect.width,
                    rect.height
                );
                EditorGUI.DrawRect(shadowRect, SHADOW_COLOR);
            }

            // --- Draw Card Background (uses Unity's adaptive button colors) ---
            Color cardColor = isActive ? _colCardActive : (isPreviewing ? Color.white : _colCardDefault);
            if (isDuplicate && !isActive && !isSelected) cardColor.a = CARD_OPACITY_DISABLED;
            EditorGUI.DrawRect(rect, cardColor);

            // --- Thumbnail ---
            Texture2D thumb = profile.thumbnail;
            if (thumb == null) thumb = logic.DefaultThumbnail;
            
            if (thumb != null) 
            {
                Color oldColor = GUI.color;
                if (isDuplicate) GUI.color = new Color(1f, 1f, 1f, 0.5f); // Fade thumbnail
                GUI.DrawTexture(imageRect, thumb, ScaleMode.ScaleAndCrop);
                GUI.color = oldColor;
            }
            else GUI.Box(imageRect, KromaBrowserStrings.MSG_NO_IMAGE, EditorStyles.centeredGreyMiniLabel);

            // --- Duplicate Warning Overlay ---
            if (isDuplicate)
            {
                // Draw warning icon in center
                float warnSize = 32f;
                Rect warnRect = new Rect(
                    imageRect.x + (imageRect.width - warnSize) / 2,
                    imageRect.y + (imageRect.height - warnSize) / 2 - 10f,
                    warnSize, warnSize
                );
                GUI.Label(warnRect, EditorGUIUtility.IconContent("console.warnicon"));
                
                // Draw "Conflict" label
                Rect labelRect = new Rect(imageRect.x, warnRect.yMax, imageRect.width, 20f);
                GUIStyle warnStyle = new GUIStyle(EditorStyles.boldLabel);
                warnStyle.alignment = TextAnchor.MiddleCenter;
                warnStyle.normal.textColor = new Color(1f, 0.5f, 0f); // Orange
                GUI.Label(labelRect, "Shared Volume!", warnStyle);
            }

            // --- Border for active profile (no shadow here anymore) ---
            if (isActive)
            {
                // Draw border as 4 rectangles (top, bottom, left, right)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, BORDER_WIDTH), BORDER_COLOR_ACTIVE); // Top
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BORDER_WIDTH, rect.width, BORDER_WIDTH), BORDER_COLOR_ACTIVE); // Bottom
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, BORDER_WIDTH, rect.height), BORDER_COLOR_ACTIVE); // Left
                EditorGUI.DrawRect(new Rect(rect.xMax - BORDER_WIDTH, rect.y, BORDER_WIDTH, rect.height), BORDER_COLOR_ACTIVE); // Right
            }
            else if (isPreviewing)
            {
                // Draw white border for preview card
                Color whiteBorder = Color.white;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, BORDER_WIDTH), whiteBorder); // Top
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BORDER_WIDTH, rect.width, BORDER_WIDTH), whiteBorder); // Bottom
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, BORDER_WIDTH, rect.height), whiteBorder); // Left
                EditorGUI.DrawRect(new Rect(rect.xMax - BORDER_WIDTH, rect.y, BORDER_WIDTH, rect.height), whiteBorder); // Right
            }

            // --- Draw selection border ON TOP (after everything else) ---
            if (isSelected)
            {
                // Draw border as 4 rectangles (top, bottom, left, right)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, BORDER_WIDTH), BORDER_COLOR_SELECTION); // Top
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - BORDER_WIDTH, rect.width, BORDER_WIDTH), BORDER_COLOR_SELECTION); // Bottom
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, BORDER_WIDTH, rect.height), BORDER_COLOR_SELECTION); // Left
                EditorGUI.DrawRect(new Rect(rect.xMax - BORDER_WIDTH, rect.y, BORDER_WIDTH, rect.height), BORDER_COLOR_SELECTION); // Right
            }

            // --- NEW: Top Overlay Bar (Star + Name + Badge) ---
            Rect overlayRect = new Rect(imageRect.x, imageRect.y, imageRect.width, CARD_OVERLAY_HEIGHT);
            EditorGUI.DrawRect(overlayRect, new Color(0, 0, 0, CARD_OVERLAY_OPACITY));

            // --- 1. Star Icon (Favorites) - Far Right ---
            bool isFavorite = logic.IsFavorite(profile);
            
            // Calculate proportional size based on texture
            float starHeight = CARD_BADGE_HEIGHT; // Match badge height exactly
            float starWidth = starHeight; // Default to square
            
            if (_texFavDefault != null)
            {
                // Calculate proportional width: height * (aspect ratio)
                float aspect = (float)_texFavDefault.width / (float)_texFavDefault.height;
                starWidth = starHeight * aspect;
            }
            
            float starBtnPadding = 4f; // Distance from the right edge
            
            // Position star on the far right
            Rect starRect = new Rect(
                overlayRect.xMax - starWidth - starBtnPadding,
                overlayRect.y + (overlayRect.height - starHeight) / 2, // Centered vertically in overlay
                starWidth,
                starHeight
            );

            // --- 2. Badge (Active/Preview) - Left of Star ---
            float badgeStartX = starRect.x; // Anchor for the text 
            float badgeWidthUsed = 0f;
            
            if (isActive || isPreviewing)
            {
                string badgeText = isActive ? KromaBrowserStrings.BADGE_ACTIVE : KromaBrowserStrings.BADGE_PREVIEW;
                Color badgeBg = isActive ? _colActiveBg : _colPreviewBg;
                Color badgeTextColor = isActive ? _colActiveText : _colPreviewText;
                
                // Calculate badge size
                GUIContent badgeContent = new GUIContent(badgeText);
                Vector2 badgeSize = _badgeTextStyle.CalcSize(badgeContent);
                float badgeWidth = badgeSize.x + (CARD_BADGE_PADDING * 2);
                
                // Position badge to the left of the star
                Rect badgeRect = new Rect(
                    starRect.x - badgeWidth - CARD_OVERLAY_PADDING,
                    overlayRect.y + (overlayRect.height - CARD_BADGE_HEIGHT) / 2,
                    badgeWidth,
                    CARD_BADGE_HEIGHT
                );
                
                // Draw badge
                EditorGUI.DrawRect(badgeRect, badgeBg);
                Color oldContent = GUI.contentColor;
                GUI.contentColor = badgeTextColor;
                GUI.Label(badgeRect, badgeText, _badgeTextStyle);
                GUI.contentColor = oldContent;

                badgeStartX = badgeRect.x; // Update anchor for the name label
                badgeWidthUsed = badgeWidth;
            }
            
            // Check if mouse is hovering over the button area
            bool isStarHovered = starRect.Contains(Event.current.mousePosition);
            
            // Draw custom texture icon perfectly centered
            Texture2D starTex;
            
            if (isFavorite)
            {
                starTex = _texFavActive; 
            }
            else
            {
                starTex = isStarHovered ? _texFavHover : _texFavDefault;
            }
            
            if (starTex != null)
            {
                GUI.DrawTexture(starRect, starTex, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback to built-in if textures are missing
                GUIContent fbIcon = EditorGUIUtility.IconContent("Favorite");
                Color oldColor2 = GUI.color;
                GUI.color = isFavorite ? _colStarActive : (isStarHovered ? _colStarHover : Color.white);
                GUI.Label(starRect, fbIcon);
                GUI.color = oldColor2;
            }
            
            // Handle star click uses the whole button rect
            if (Event.current.type == EventType.MouseDown && starRect.Contains(Event.current.mousePosition))
            {
                logic.ToggleFavorite(profile);
                
                // Get display name for notification
                string notifName = !string.IsNullOrEmpty(profile.profileName) ? profile.profileName : logic.GetCleanName(profile.name);
                
                // Show notification overlay
                if (logic.IsFavorite(profile))
                {
                    (window as KromaProfileBrowser)?.ShowCustomNotification($"{notifName} added to favorites", true);
                }
                else
                {
                    (window as KromaProfileBrowser)?.ShowCustomNotification($"{notifName} removed from favorites", false);
                }
                
                Event.current.Use();
                window.Repaint();
            }
            
            // Add cursor effect on star hover
            if (isStarHovered)
            {
                EditorGUIUtility.AddCursorRect(starRect, MouseCursor.Link);
            }
            
            // Add tooltip
            if (isStarHovered)
            {
                GUIContent tooltipContent = new GUIContent("", isFavorite ? KromaBrowserStrings.TOOLTIP_UNFAVORITE : KromaBrowserStrings.TOOLTIP_FAVORITE);
                GUI.Label(starRect, tooltipContent);
            }

            // --- 3. Name - Far Left (Fills remaining space) ---
            float nameStartX = overlayRect.x + CARD_OVERLAY_PADDING;
            // Use the badge position if it exists, otherwise the star position
            float rightObstructionX = (badgeWidthUsed > 0) ? badgeStartX : starRect.x;
            float nameEndX = rightObstructionX - 8f; // Stop 8px before the obstruction
            Rect nameRect = new Rect(nameStartX, overlayRect.y, nameEndX - nameStartX, overlayRect.height);
            
            // Update name style to left-aligned and bold
            GUIStyle leftAlignedNameStyle = new GUIStyle(_nameOverlayStyle);
            leftAlignedNameStyle.alignment = TextAnchor.MiddleLeft;
            leftAlignedNameStyle.fontStyle = FontStyle.Bold;
            // Removed fixed font size constraint to allow default body sizing
            
            // Display Logic: Use Profile Name if available, else Clean Name
            string displayName = !string.IsNullOrEmpty(profile.profileName) ? profile.profileName : logic.GetCleanName(profile.name);
            
            // --- Truncation Logic ---
            float availableWidth = nameRect.width;
            GUIContent nameContent = new GUIContent(displayName);
            if (leftAlignedNameStyle.CalcSize(nameContent).x > availableWidth)
            {
                // Iteratively remove characters and add ellipsis until it fits
                string ellipsis = "...";
                float ellipsisWidth = leftAlignedNameStyle.CalcSize(new GUIContent(ellipsis)).x;
                
                // If it's too small for even an ellipsis, just don't draw text (edge case)
                if (availableWidth <= ellipsisWidth)
                {
                    displayName = "";
                }
                else
                {
                    int keepChars = displayName.Length;
                    while (keepChars > 0 && leftAlignedNameStyle.CalcSize(new GUIContent(displayName.Substring(0, keepChars) + ellipsis)).x > availableWidth)
                    {
                        keepChars--;
                    }
                    if (keepChars > 0)
                        displayName = displayName.Substring(0, keepChars) + ellipsis;
                    else
                        displayName = ellipsis; // Extreme edge case
                }
            }
            
            // User request: active title gets the same yellow as the active badge background
            if (isActive)
            {
                leftAlignedNameStyle.normal.textColor = _colActiveBg; 
            }
            
            GUI.Label(nameRect, displayName, leftAlignedNameStyle);

            // --- Buttons (Text) ---
            // Conditions from user:
            // 1. Hover/Keyboard Selected -> Show Preview/Apply
            // 2. Previewing -> Show Cancel/Apply
            // 3. Active -> Hide
            // 4. Default -> Hide
            
            bool showButtons = !isActive && (isPreviewing || isSelected || isHovering);
            
            if (showButtons)
            {
                // Disable all buttons during Play Mode to prevent profile changes
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                
                if (isDuplicate)
                {
                     // DUPLICATE MODE: Show "Fix Issue" button
                     Rect btnFull = new Rect(imageRect.x + btnPadding, btnY, totalBtnWidth, BUTTON_HEIGHT);
                     if (GUI.Button(btnFull, "Fix Issue (Select)"))
                     {
                         EditorGUIUtility.PingObject(profile); // Ping the WRAPPER (since user needs to see warning on wrapper)
                         Selection.activeObject = profile;
                     }
                }
                else if (isPreviewing)
                {
                    if (GUI.Button(btnLeft, KromaBrowserStrings.BTN_CANCEL)) logic.StopPreview();
                    
                    // Colorize Apply button for emphasis
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); // Soft Green
                    if (GUI.Button(btnRight, KromaBrowserStrings.BTN_APPLY)) logic.ApplyToSelected(profile, window);
                    GUI.backgroundColor = old;
                }
                else
                {
                    // Default Show State (Hover/Selected)
                    if (GUI.Button(btnLeft, KromaBrowserStrings.BTN_PREVIEW)) logic.StartPreview(profile);
                    if (GUI.Button(btnRight, KromaBrowserStrings.BTN_APPLY)) logic.ApplyToSelected(profile, window);
                }
                
                EditorGUI.EndDisabledGroup();
            }

            // --- Handle Selection Click (Background) ---
            // We check this LAST so buttons/stars have a chance to eat the event first.
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                logic.SelectedProfileIndex = profileIndex;
                Event.current.Use();
                GUI.changed = true; 
                window.Repaint();
            }
        }





        #if false // LIST VIEW DISABLED FOR V1 - See KromaBrowserDrawer_ListViewBackup.cs to restore
        private void DrawListView(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            // USE FILTERED LIST - Changed to indexed loop for selection tracking
            for (int i = 0; i < logic.FilteredProfiles.Count; i++)
            {
                var profile = logic.FilteredProfiles[i];
                Rect rowRect = GUILayoutUtility.GetRect(position.width - (PADDING * 2), LIST_ROW_HEIGHT);
                
                // --- 1. Selection Border (if selected) ---
                bool isSelected = (logic.SelectedProfileIndex == i);
                if (isSelected)
                {
                    // Draw border as 4 rectangles
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, rowRect.width, BORDER_WIDTH), BORDER_COLOR_SELECTION); // Top
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - BORDER_WIDTH, rowRect.width, BORDER_WIDTH), BORDER_COLOR_SELECTION); // Bottom
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, BORDER_WIDTH, rowRect.height), BORDER_COLOR_SELECTION); // Left
                    EditorGUI.DrawRect(new Rect(rowRect.xMax - BORDER_WIDTH, rowRect.y, BORDER_WIDTH, rowRect.height), BORDER_COLOR_SELECTION); // Right
                }
                
                // --- 2. Hover Effect (Background Layer) ---
                if (Event.current.type == EventType.Repaint && rowRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(rowRect, _colCardHover);
                }
                
                GUI.Box(rowRect, GUIContent.none, EditorStyles.helpBox);

                // --- Star Icon (Favorites) - BEFORE name ---
                bool isFavorite = logic.IsFavorite(profile);
                float starSize = 20f;
                
                // Position star at the start
                float starX = rowRect.x + LIST_PADDING_LEFT;
                Rect starRect = new Rect(
                    starX,
                    rowRect.y + (LIST_ROW_HEIGHT - starSize) / 2,
                    starSize,
                    starSize
                );
                
                // Check if mouse is hovering over star
                bool isStarHovered = starRect.Contains(Event.current.mousePosition);
                
                // Draw star icon
                GUIContent starIcon;
                Color starColor;
                
                if (isFavorite)
                {
                    starIcon = EditorGUIUtility.IconContent("Favorite");
                    starColor = isStarHovered ? new Color(1f, 0.93f, 0.31f) : new Color(1f, 0.84f, 0f); // Gold
                }
                else
                {
                    starIcon = EditorGUIUtility.IconContent("Favorite");
                    starColor = isStarHovered ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f); // Gray
                }
                
                Color oldColor = GUI.color;
                GUI.color = starColor;
                GUI.Label(starRect, starIcon);
                GUI.color = oldColor;
                
                // Handle star click
                if (Event.current.type == EventType.MouseDown && starRect.Contains(Event.current.mousePosition))
                {
                    logic.ToggleFavorite(profile);
                    Event.current.Use();
                    window.Repaint();
                }
                
                // Add cursor effect on star hover
                if (isStarHovered)
                {
                    EditorGUIUtility.AddCursorRect(starRect, MouseCursor.Link);
                }
                
                // Add tooltip
                if (isStarHovered)
                {
                    GUIContent tooltipContent = new GUIContent("", isFavorite ? KromaBrowserStrings.TOOLTIP_UNFAVORITE : KromaBrowserStrings.TOOLTIP_FAVORITE);
                    GUI.Label(starRect, tooltipContent);
                }

                // Name (after star)
                float nameStartX = starRect.xMax + 6f; // 6px gap after star
                Rect labelRect = new Rect(nameStartX, rowRect.y, 250, LIST_ROW_HEIGHT);
                GUI.Label(labelRect, logic.GetProfileDisplayName(profile), _listLabelStyle);

                // Logic
                bool isActive = logic.IsProfileActiveOnSelection(profile);
                bool isPreviewing = logic.IsPreviewing && logic.ActivePreviewProfile == profile;

                // Buttons Right-Aligned
                float btnY = rowRect.y + (LIST_ROW_HEIGHT - LIST_BUTTON_HEIGHT) / 2;
                float xCursor = rowRect.xMax - 8;

                xCursor -= LIST_BUTTON_WIDTH;
                Rect rectApply = new Rect(xCursor, btnY, LIST_BUTTON_WIDTH, LIST_BUTTON_HEIGHT);
                xCursor -= (LIST_BUTTON_WIDTH + 4);
                Rect rectPrev = new Rect(xCursor, btnY, LIST_BUTTON_WIDTH, LIST_BUTTON_HEIGHT);
                
                // Badge Logic
                Rect badgeRect = new Rect(xCursor - 12, rowRect.y, 0, LIST_ROW_HEIGHT); 

                if (isActive)
                {
                    DrawBadgeList(badgeRect, KromaBrowserStrings.BADGE_ACTIVE, _colActiveBg, _colActiveText);
                    EditorGUI.BeginDisabledGroup(true);
                    GUI.Button(rectPrev, KromaBrowserStrings.BTN_PREVIEW);
                    GUI.Button(rectApply, KromaBrowserStrings.BTN_APPLY);
                    EditorGUI.EndDisabledGroup();
                }
                else if (isPreviewing)
                {
                    DrawBadgeList(badgeRect, KromaBrowserStrings.BADGE_PREVIEW, _colPreviewBg, _colPreviewText);
                    if (GUI.Button(rectPrev, KromaBrowserStrings.BTN_CANCEL)) logic.StopPreview();
                    
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUI.Button(rectApply, KromaBrowserStrings.BTN_APPLY)) logic.ApplyToSelected(profile, window);
                    GUI.backgroundColor = old;
                }
                else
                {
                    if (GUI.Button(rectPrev, KromaBrowserStrings.BTN_PREVIEW)) logic.StartPreview(profile);
                    if (GUI.Button(rectApply, KromaBrowserStrings.BTN_APPLY)) logic.ApplyToSelected(profile, window);
                }
                
                // Add spacing between rows
                GUILayout.Space(LIST_ROW_SPACING);
            }
        }
        #endif // LIST VIEW DISABLED FOR V1

        private void DrawFooter(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            // Separator line above footer
            Rect separatorRect = GUILayoutUtility.GetRect(position.width, 1);
            EditorGUI.DrawRect(separatorRect, _cachedSeparator);
            
            // Draw background matching toolbar area (not dark content background)
            // Calculate footer height
            float footerHeight = TOOLBAR_PADDING_TOP + 28 + TOOLBAR_PADDING_BOTTOM;
            Rect footerBgRect = new Rect(0, separatorRect.yMax, position.width, footerHeight);
            
            // Use default Unity background color (same as search bar area)
            Color footerBg = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f);
            EditorGUI.DrawRect(footerBgRect, footerBg);
            
            GUILayout.Space(TOOLBAR_PADDING_TOP);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(PADDING); // Match search bar left padding
            
            // Left side: Documentation & Community links
            GUIStyle linkStyle = new GUIStyle(EditorStyles.miniLabel);
            linkStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            linkStyle.richText = true;
            linkStyle.margin = new RectOffset(0, 0, 0, 0);
            linkStyle.padding = new RectOffset(0, 0, 0, 0);
            
            // Online Documentation link
            if (GUILayout.Button(KromaBrowserStrings.BANNER_ONLINE_DOCS_LINK_TEXT, linkStyle, GUILayout.ExpandWidth(false)))
            {
                Application.OpenURL(KromaBrowserStrings.BANNER_ONLINE_DOCS_URL);
            }
            
            // Separator
            GUILayout.Space(4);
            GUILayout.Label("|", linkStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(4);
            
            // Discord Community link
            if (GUILayout.Button(KromaBrowserStrings.BANNER_DISCORD_LINK_TEXT, linkStyle, GUILayout.ExpandWidth(false)))
            {
                Application.OpenURL(KromaBrowserStrings.BANNER_DISCORD_URL);
            }
            
            GUILayout.FlexibleSpace();
            
            // Right side: Apply To / Target Selection
            
            // Settings Button (height matching dropdown)
            GUIContent settingsContent = new GUIContent(" Settings", EditorGUIUtility.IconContent("_Popup").image);
            if (GUILayout.Button(settingsContent, GUILayout.Width(80), GUILayout.Height(18)))
            {
                logic.ShowSettingsView = !logic.ShowSettingsView;
            }
            
            GUILayout.Space(PADDING); // Match search bar right padding
            GUILayout.EndHorizontal();
            GUILayout.Space(TOOLBAR_PADDING_BOTTOM);
        }

        // --- HELPERS ---

        private void DrawBadge(Rect container, string text, Color bgCol, Color txtCol)
        {
            GUIContent content = new GUIContent(text);
            Vector2 size = _badgeTextStyle.CalcSize(content);
            
            // Padding & Margins
            Rect badgeRect = new Rect(container.xMax - _fixedBadgeWidth - 6, container.y + 6, _fixedBadgeWidth, size.y + 4);

            // FIX: Reverted to DrawRect for solid opacity as requested
            EditorGUI.DrawRect(badgeRect, bgCol);

            Color oldContent = GUI.contentColor;
            GUI.contentColor = txtCol;
            GUI.Label(badgeRect, text, _badgeTextStyle);
            GUI.contentColor = oldContent;
        }

        private void DrawBadgeList(Rect anchor, string text, Color bgCol, Color txtCol)
        {
            // Calculate badge size using same logic as card badges
            GUIContent badgeContent = new GUIContent(text);
            Vector2 badgeSize = _badgeTextStyle.CalcSize(badgeContent);
            float badgeWidth = badgeSize.x + (CARD_BADGE_PADDING * 2);
            
            // Position badge to the left of the anchor point, vertically centered
            Rect badgeRect = new Rect(
                anchor.x - badgeWidth - 6,
                anchor.y + (anchor.height - CARD_BADGE_HEIGHT) / 2,
                badgeWidth,
                CARD_BADGE_HEIGHT
            );

            // Draw badge (same as card badges)
            EditorGUI.DrawRect(badgeRect, bgCol);
            Color oldContent = GUI.contentColor;
            GUI.contentColor = txtCol;
            GUI.Label(badgeRect, text, _badgeTextStyle);
            GUI.contentColor = oldContent;
        }

        // --- COMPACT FAVORITES BUTTON ---
        
        private void DrawCompactFavoritesButton(KromaBrowserLogic logic)
        {
            // Clean up stale favorites (deleted profiles) before getting count
            logic.CleanupStaleFavorites();
            
            int favCount = logic.GetFavoritesCount();
            bool hasFavorites = favCount > 0;
            
            // Compact format: ⭐ (0)
            GUIContent favContent = new GUIContent(
                $" ({favCount})",
                EditorGUIUtility.IconContent("Favorite").image
            );
            
            // Tooltip based on state
            string tooltip;
            if (!hasFavorites)
            {
                tooltip = KromaBrowserStrings.TOOLTIP_NO_FAVORITES;
            }
            else if (logic.ShowOnlyFavorites)
            {
                tooltip = KromaBrowserStrings.TOOLTIP_FAVORITES_FILTER_ACTIVE;
            }
            else
            {
                tooltip = KromaBrowserStrings.TOOLTIP_FAVORITES_FILTER;
            }
            favContent.tooltip = tooltip;
            
            // Highlight when active
            Color oldBgColor = GUI.backgroundColor;
            if (logic.ShowOnlyFavorites)
            {
                GUI.backgroundColor = new Color(1f, 0.84f, 0f, 0.3f); // Gold tint when active
            }
            
            // Button with fixed height - always enabled (acts as toggle)
            if (GUILayout.Button(favContent, GUILayout.Width(60), GUILayout.Height(SEARCH_HEIGHT)))
            {
                logic.ShowOnlyFavorites = !logic.ShowOnlyFavorites;
                logic.ApplySearchFilter();
                logic.ResetSelection(); // Reset keyboard selection when filter changes
            }
            
            GUI.backgroundColor = oldBgColor;
        }

        // --- TOOLBAR BUTTON HELPERS ---
        
        private void DrawRefreshButton(KromaBrowserLogic logic)
        {
            GUIContent refreshIcon = new GUIContent(
                EditorGUIUtility.IconContent("d_Refresh").image,
                "Refresh profile list"
            );
            if (GUILayout.Button(refreshIcon, GUILayout.Width(SEARCH_HEIGHT), GUILayout.Height(SEARCH_HEIGHT)))
            {
                // Force AssetDatabase refresh to ensure new files are found immediately
                AssetDatabase.Refresh();
                logic.RefreshProfileList();
            }
        }



        // --- KEYBOARD INPUT HANDLING ---
        
        private void HandleKeyboardInput(KromaBrowserLogic logic, EditorWindow window, Rect position)
        {
            Event e = Event.current;
            
            // Only process keyboard events
            if (e.type != EventType.KeyDown) return;
            
            // Don't process if search field is focused
            if (GUI.GetNameOfFocusedControl() == "KromaSearchField") 
            {
                // Allow "/" to focus search even when already focused
                if (e.keyCode == KeyCode.Slash)
                {
                    e.Use();
                }
                return;
            }
            
            // Calculate columns for grid navigation (gallery view only in V1)
            int columns = 1;
            float containerWidth = position.width - (PADDING * 2) - 20;
            columns = Mathf.FloorToInt(containerWidth / (CARD_WIDTH_MIN + SPACING));
            if (columns < 1) columns = 1;
            
            bool handled = false;
            
            // Arrow key navigation
            if (e.keyCode == KeyCode.UpArrow)
            {
                logic.NavigateUp(columns);
                handled = true;
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                logic.NavigateDown(columns);
                handled = true;
            }
            else if (e.keyCode == KeyCode.LeftArrow)
            {
                logic.NavigateLeft();
                handled = true;
            }
            else if (e.keyCode == KeyCode.RightArrow)
            {
                logic.NavigateRight();
                handled = true;
            }
            // Enter key - Preview then Apply
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                KromaProfile selected = logic.GetSelectedProfile();
                if (selected != null)
                {
                    // If already previewing this profile, apply it
                    if (logic.IsPreviewing && logic.ActivePreviewProfile == selected)
                    {
                        logic.ApplyToSelected(selected, window);
                    }
                    else
                    {
                        // Otherwise, preview it
                        logic.StartPreview(selected);
                    }
                    handled = true;
                }
            }
            // Escape - Stop preview
            else if (e.keyCode == KeyCode.Escape)
            {
                if (logic.IsPreviewing)
                {
                    logic.StopPreview();
                    handled = true;
                }
            }
            // "/" - Focus search
            else if (e.keyCode == KeyCode.Slash)
            {
                GUI.FocusControl("KromaSearchField");
                handled = true;
            }
            
            if (handled)
            {
                e.Use();
                
                // Auto-scroll to selected item
                // Need to wait one frame for _lastSelectedCardY to be updated during drawing
                EditorApplication.delayCall += () =>
                {
                    if (_lastSelectedCardY > 0)
                    {
                        // Calculate visible area
                        float visibleTop = _scrollPos.y;
                        float visibleBottom = _scrollPos.y + (position.height - 300f); // Approximate content area height
                        
                        float selectedTop = _lastSelectedCardY;
                        float selectedBottom = _lastSelectedCardY + CARD_HEIGHT;
                        
                        // Scroll if selected item is outside visible area
                        if (selectedTop < visibleTop)
                        {
                            // Scroll up to show top of selected item
                            _scrollPos.y = selectedTop - 20f; // 20px padding
                        }
                        else if (selectedBottom > visibleBottom)
                        {
                            // Scroll down to show bottom of selected item
                            _scrollPos.y = selectedBottom - (position.height - 300f) + 20f; // 20px padding
                        }
                        
                        window.Repaint();
                    }
                };
                
                window.Repaint();
            }
        }


        private void DrawResponsiveHeader(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            float width = position.width;
            bool isNarrow = width < 500f; // Threshold for splitting layout
            
            // Calculate dynamic height based on layout mode
            float baseHeight = 50f;
            float row2Height = 4f + SEARCH_HEIGHT + 8f; // 4px top + 28px search + 8px bottom
            float headerHeight = isNarrow ? (baseHeight + row2Height) : baseHeight;
            
            // 1. Draw Background (Full Width)
            Rect bgRect = new Rect(0, 0, position.width, headerHeight); 
            EditorGUI.DrawRect(bgRect, _cachedBannerBg); 

            // 2. Main Row (Logo + Buttons)
            GUILayout.BeginHorizontal(GUILayout.Height(baseHeight));
            GUILayout.Space(PADDING);

            // -- Logo --
            if (logic.BannerTexture != null)
            {
                float logoH = 50f;
                // Protection against divide by zero
                float aspect = (logic.BannerTexture.height > 0) 
                    ? (float)logic.BannerTexture.width / logic.BannerTexture.height 
                    : 1f;
                float logoW = logoH * aspect;
                
                // Reserve layout space
                Rect logoRect = GUILayoutUtility.GetRect(logoW, baseHeight, GUILayout.Width(logoW));
                
                // Draw texture centered vertically
                if (Event.current.type == EventType.Repaint)
                {
                    float lY = logoRect.y + (baseHeight - logoH) / 2;
                    Rect texRect = new Rect(logoRect.x, lY, logoW, logoH);
                    GUI.DrawTexture(texRect, logic.BannerTexture, ScaleMode.ScaleToFit, true);
                }
            }
            
            // Safety Padding (Never touch the logo)
            GUILayout.Space(20);

            // Flexible Space Logic
            // The user wants Search/Filter forced to the right in ALL modes.
            GUILayout.FlexibleSpace();

            if (!isNarrow)
            {
                // -- Search Bar (Wide Mode Only) --
                GUILayout.BeginVertical(GUILayout.MaxWidth(500));
                float vCenter = (baseHeight - SEARCH_HEIGHT) / 2;
                GUILayout.Space(vCenter);
                
                GUILayout.BeginHorizontal();
                
                DrawSearchField(logic);
                GUILayout.Space(8); // Small gap between search and dropdown
                
                // --- SOURCE FILTER DROPDOWN ---
                EditorGUI.BeginChangeCheck();
                
                // Create a custom popup style that matches the exact height of our custom search bar
                GUIStyle customPopupStyle = new GUIStyle(EditorStyles.popup);
                customPopupStyle.fixedHeight = SEARCH_HEIGHT;
                customPopupStyle.alignment = TextAnchor.MiddleLeft;
                customPopupStyle.margin = new RectOffset(0, 0, 0, 0); // Remove any external margins
                
                // Construct display names dynamically to include favorite count
                string[] filterNames = new string[] {
                    "All Profiles",
                    "Kroma Profiles",
                    "My Profiles",
                    $"Favorites ({logic.FavoriteProfileGUIDs.Count})"
                };
                
                int newFilterInt = EditorGUILayout.Popup(
                    (int)logic.CurrentSourceFilter,
                    filterNames,
                    customPopupStyle,
                    GUILayout.Width(130), 
                    GUILayout.Height(SEARCH_HEIGHT)
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    logic.CurrentSourceFilter = (ProfileSourceFilter)newFilterInt;
                    logic.ApplySearchFilter();
                    logic.ResetSelection();
                }
                
                GUILayout.Space(7); // Match search bar gap (updated to perfectly match)
                
                // --- CREATE PROFILE BUTTON ---
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                
                GUIStyle createBtnStyle = new GUIStyle(GUI.skin.button);
                createBtnStyle.margin = new RectOffset(0, 0, 0, 0); // Remove default margin for perfect vertical alignment
                
                GUIContent createContent = new GUIContent("+", "Create Profile");
                if (GUILayout.Button(createContent, createBtnStyle, GUILayout.Width(SEARCH_HEIGHT), GUILayout.Height(SEARCH_HEIGHT)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Kroma/Create Profile");
                    EditorApplication.delayCall += () => logic.RefreshProfileList();
                }
                EditorGUI.EndDisabledGroup();
                
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.Space(PADDING);
            GUILayout.EndHorizontal();
            
            // 3. Second Row (Narrow Mode Only - Search Bar)
            if (isNarrow)
            {
                GUILayout.BeginHorizontal(); 
                GUILayout.Space(PADDING); // Match padding
                
                GUILayout.BeginVertical();
                GUILayout.Space(4f); // 4px padding between rows
                
                GUILayout.BeginHorizontal();
                DrawSearchField(logic); // Will expand to fill row
                GUILayout.Space(8); // Small gap between search and dropdown
                
                // --- SOURCE FILTER DROPDOWN ---
                EditorGUI.BeginChangeCheck();
                
                GUIStyle customPopupStyle = new GUIStyle(EditorStyles.popup);
                customPopupStyle.fixedHeight = SEARCH_HEIGHT;
                customPopupStyle.alignment = TextAnchor.MiddleLeft;
                customPopupStyle.margin = new RectOffset(0, 0, 0, 0);
                
                // Construct display names dynamically to include favorite count
                string[] filterNames = new string[] {
                    "All Profiles",
                    "Kroma Profiles",
                    "My Profiles",
                    $"Favorites ({logic.FavoriteProfileGUIDs.Count})"
                };
                
                int newFilterInt = EditorGUILayout.Popup(
                    (int)logic.CurrentSourceFilter, 
                    filterNames,
                    customPopupStyle,
                    GUILayout.Width(130), 
                    GUILayout.Height(SEARCH_HEIGHT)
                );
                
                if (EditorGUI.EndChangeCheck())
                {
                    logic.CurrentSourceFilter = (ProfileSourceFilter)newFilterInt;
                    logic.ApplySearchFilter();
                    logic.ResetSelection();
                }
                
                GUILayout.Space(7); // Match search bar gap (updated to perfectly match)
                
                // --- CREATE PROFILE BUTTON ---
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                
                GUIStyle createBtnStyle = new GUIStyle(GUI.skin.button);
                createBtnStyle.margin = new RectOffset(0, 0, 0, 0); // Remove default margin for perfect vertical alignment
                
                GUIContent createContent = new GUIContent("+", "Create Profile");
                if (GUILayout.Button(createContent, createBtnStyle, GUILayout.Width(SEARCH_HEIGHT), GUILayout.Height(SEARCH_HEIGHT)))
                {
                    EditorApplication.ExecuteMenuItem("Tools/Kroma/Create Profile");
                    EditorApplication.delayCall += () => logic.RefreshProfileList();
                }
                EditorGUI.EndDisabledGroup();
                
                GUILayout.EndHorizontal();
                
                GUILayout.Space(8f); // 8px padding at bottom
                GUILayout.EndVertical();

                GUILayout.Space(PADDING);
                GUILayout.EndHorizontal();
            }
            
            // 4. Header Separator
            Rect sepRect = GUILayoutUtility.GetRect(position.width, 1);
            EditorGUI.DrawRect(sepRect, new Color(0.1f, 0.1f, 0.1f, 1f));
        }

        private void DrawSettingsView(Rect position, KromaBrowserLogic logic, EditorWindow window)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.BeginVertical();
            
            // Headline Title
            GUIStyle headlineStyle = new GUIStyle(EditorStyles.boldLabel);
            headlineStyle.fontSize = 24;
            headlineStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);
            GUILayout.Label(KromaBrowserStrings.SETTINGS_TITLE, headlineStyle);
            GUILayout.Space(30);

            // Standard styles for sections
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionStyle.fontSize = 16;
            GUIStyle bodyStyle = new GUIStyle(EditorStyles.label);
            bodyStyle.wordWrap = true;
            bodyStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            // --- Active Library Section ---
            GUILayout.Label("Active Library", sectionStyle);
            GUILayout.Space(4);
            GUILayout.Label("The library stores and displays your Kroma profiles so you can organize them.", bodyStyle);
            GUILayout.Space(10);


            
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            KromaProfileLibrary newLib = (KromaProfileLibrary)EditorGUILayout.ObjectField(logic.ProfileLibrary, typeof(KromaProfileLibrary), false, GUILayout.Width(250), GUILayout.Height(22));
            if (EditorGUI.EndChangeCheck())
            {
                logic.SetPreferredLibrary(newLib);
                window.Repaint();
            }

            GUILayout.Space(10);

            if (logic.ProfileLibrary == null)
            {
                if (GUILayout.Button("Create Library", GUILayout.Width(110), GUILayout.Height(22)))
                {
                    logic.CreateNewLibrary();
                    window.Repaint();
                }
                GUILayout.Space(5);
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(22)))
            {
                AssetDatabase.Refresh();
                logic.RefreshProfileList();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);
            DrawSettingsSeparator();
            GUILayout.Space(20);

            // --- Scene Impact Section ---
            GUILayout.Label("Scene Impact", sectionStyle);
            GUILayout.Space(4);
            GUILayout.Label("Select which scene components Kroma is allowed to override.", bodyStyle);
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Override: ", GUILayout.Width(70));
            
            // Determine button label based on active settings
            int activeCount = 0;
            if (logic.CurrentApplySettings.applyPostProcessing) activeCount++;
            if (logic.CurrentApplySettings.applySkybox) activeCount++;
            if (logic.CurrentApplySettings.applySunLight) activeCount++;
            if (logic.CurrentApplySettings.applyFog) activeCount++;
            if (logic.CurrentApplySettings.applySceneObjects) activeCount++;
            
            string btnLabel = "Mixed...";
            if (activeCount == 0) btnLabel = "Nothing";
            else if (activeCount == 5) btnLabel = "Everything";
            else
            {
                // Create a comma-separated list of active items
                List<string> activeNames = new List<string>();
                if (logic.CurrentApplySettings.applyPostProcessing) activeNames.Add("Post Processing");
                if (logic.CurrentApplySettings.applySkybox) activeNames.Add("Skybox");
                if (logic.CurrentApplySettings.applySunLight) activeNames.Add("Sun Light");
                if (logic.CurrentApplySettings.applyFog) activeNames.Add("Fog");
                if (logic.CurrentApplySettings.applySceneObjects) activeNames.Add("Scene Objects");
                
                // If too long, use summaries
                if (activeNames.Count > 2) btnLabel = "Mixed...";
                else btnLabel = string.Join(", ", activeNames);
            }

            // Draw Dropdown Button
            if (EditorGUILayout.DropdownButton(new GUIContent(btnLabel), FocusType.Keyboard, GUILayout.Width(150), GUILayout.Height(22)))
            {
                GenericMenu menu = new GenericMenu();
                
                void ToggleSetting(string name)
                {
                    var s = logic.CurrentApplySettings; // Copy struct
                    if (name == "PP") s.applyPostProcessing = !s.applyPostProcessing;
                    else if (name == "Skybox") s.applySkybox = !s.applySkybox;
                    else if (name == "Sun") s.applySunLight = !s.applySunLight;
                    else if (name == "Fog") s.applyFog = !s.applyFog;
                    else if (name == "Scene") s.applySceneObjects = !s.applySceneObjects;
                    else if (name == "All") s = ApplySettings.All;
                    else if (name == "None") s = new ApplySettings(); // default is all false
                    
                    logic.CurrentApplySettings = s; // Write back
                    logic.SaveApplySettings();
                    window.Repaint();
                }

                menu.AddItem(new GUIContent("Post Processing"), logic.CurrentApplySettings.applyPostProcessing, () => ToggleSetting("PP"));
                menu.AddItem(new GUIContent("Skybox"), logic.CurrentApplySettings.applySkybox, () => ToggleSetting("Skybox"));
                menu.AddItem(new GUIContent("Sun Light"), logic.CurrentApplySettings.applySunLight, () => ToggleSetting("Sun"));
                menu.AddItem(new GUIContent("Fog"), logic.CurrentApplySettings.applyFog, () => ToggleSetting("Fog"));
                menu.AddItem(new GUIContent("Scene Objects"), logic.CurrentApplySettings.applySceneObjects, () => ToggleSetting("Scene"));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Everything"), false, () => ToggleSetting("All"));
                menu.AddItem(new GUIContent("Nothing"), false, () => ToggleSetting("None"));
                menu.ShowAsContext();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.Space(20);
            DrawSettingsSeparator();
            GUILayout.Space(20);
            
            // --- Simulation Section ---
            GUILayout.Label("Particles Simulation", sectionStyle);
            GUILayout.Space(4);
            GUILayout.Label("Check this to preview effects without hitting Play.", bodyStyle);
            GUILayout.Space(10);
            
            bool newSimState = GUILayout.Toggle(logic.AutoSimulateParticles, " Auto-Simulate Particles");
            if (newSimState != logic.AutoSimulateParticles)
            {
                logic.AutoSimulateParticles = newSimState;
                logic.SaveApplySettings();
            }
            
            GUILayout.Space(20);
            DrawSettingsSeparator();
            GUILayout.Space(30);

            // Back Button
            GUIStyle backBtnStyle = new GUIStyle(GUI.skin.button);
            backBtnStyle.fontSize = 13;
            if (GUILayout.Button("Go back", backBtnStyle, GUILayout.Width(100), GUILayout.Height(30)))
            {
                logic.ShowSettingsView = false;
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(30);
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawSettingsSeparator()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        }
    }
}
