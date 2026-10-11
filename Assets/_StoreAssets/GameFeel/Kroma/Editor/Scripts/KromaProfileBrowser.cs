using UnityEngine;
using UnityEditor;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// The Main Window for the Kroma tool.
    /// It initializes the Logic and Drawer classes and connects them.
    /// </summary>
    public enum KromaNotificationType
    {
        FavoriteAdded,
        FavoriteRemoved,
        ProfileApplied
    }

    /// <summary>
    /// The Main Window for the Kroma tool.
    /// It initializes the Logic and Drawer classes and connects them.
    /// </summary>
    public class KromaProfileBrowser : EditorWindow
    {
        private KromaBrowserLogic _logic;
        public KromaBrowserLogic Logic => _logic;
        private KromaBrowserDrawer _drawer;

        // --- Notification State ---
        public string ActiveNotificationMessage { get; private set; }
        public double NotificationStartTime { get; private set; }
        public bool IsNotificationActive { get; private set; }
        public bool IsNotificationFavoriteEvent { get; private set; }
        public KromaNotificationType ActiveNotificationType { get; private set; }
        private const double NOTIFICATION_DURATION = 2.0; // Total time (1.5s solid + 0.5s fade)

        [MenuItem("Tools/Kroma/Kroma Browser", false, 0)]
        public static void ShowWindow()
        {
            KromaProfileBrowser window = GetWindow<KromaProfileBrowser>(KromaBrowserStrings.WINDOW_TITLE);
            // Calculate ideal width based on 4 cards + padding
            float idealWidth = (230f * 4) + (8f * 3) + (20f * 2) + 30;
            float idealHeight = 800f;
            
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            float x = mainWindow.x + (mainWindow.width - idealWidth) * 0.5f;
            float y = mainWindow.y + (mainWindow.height - idealHeight) * 0.5f;
            window.position = new Rect(x, y, idealWidth, idealHeight);
            
            window.minSize = new Vector2(300, 500);
            window.maxSize = new Vector2(idealWidth + 50, 3000f);
            window.InitializeBrowser(); // Ensure browser state is initialized
            
            // Check if setup is needed and resize window
            if (window.Logic != null && !window.Logic.AllRequirementsMet())
            {
                float halfWidth = idealWidth / 2f;
                float targetHeight = 550f;
                if (window.position.width > halfWidth + 50f)
                {
                    float diffX = (window.position.width - halfWidth) * 0.5f;
                    float diffY = (window.position.height - targetHeight) * 0.5f;
                    window.position = new Rect(window.position.x + diffX, window.position.y + diffY, halfWidth, targetHeight);
                }
            }
        }

        /// <summary>
        /// Public method to initialize/refresh browser state.
        /// Call this after GetWindow() to ensure proper initialization.
        /// </summary>
        public void InitializeBrowser()
        {
            if (_logic != null)
            {
                _logic.RefreshProfileList();
                
                // Auto-select first profile
                if (_logic.FilteredProfiles.Count > 0)
                {
                    // logic.SelectedProfileIndex = 0; // DISABLED: Selection only appears on key press
                }
            }
        }

        private void OnEnable()
        {
            // Initialize the sub-systems
            _logic = new KromaBrowserLogic();
            _drawer = new KromaBrowserDrawer();

            // Use delayCall to prevent "loading during domain backup" errors during reload
            EditorApplication.delayCall += () => 
            {
                if (_logic != null)
                {
                    _logic.RefreshProfileList();
                    // Auto-select first profile for immediate keyboard navigation
                    if (_logic.FilteredProfiles.Count > 0)
                    {
                        // _logic.SelectedProfileIndex = 0; // DISABLED: Selection only appears on key press
                    }
                    
                    Repaint(); // Ensure UI updates after load
                }
            };
            
            // REMOVED: _drawer.InitStyles() caused a NullReferenceException because 
            // Unity's EditorStyles aren't ready during OnEnable. 
            // The Drawer handles lazy initialization inside OnGUI automatically.
            
            wantsMouseMove = true; // Enable hover updates
        }

        private void OnDisable()
        {
            if (_logic != null) _logic.StopPreview();
        }

        // private void OnInspectorUpdate() => Repaint();
        
        private void OnSelectionChange()
        {
            Repaint();
        }
        
        private void OnHierarchyChange() => Repaint();

        // OPTIMIZATION: Auto-refresh disabled to prevent lag on ProjectChange.
        // Use OnEnable or the Manual "Refresh List" button instead.
        // private void OnProjectChange() => _logic.RefreshProfileList();

        private void OnGUI()
        {
            // Ensure systems exist
            // Fix: Do NOT initialize synchronously in OnGUI, as this can trigger "loading during domain backup" errors.
            // Rely on OnEnable's delayCall to initialize _logic.
            if (_logic == null) 
            {
                GUILayout.Label("Initializing Kroma...", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            if (_drawer == null) _drawer = new KromaBrowserDrawer();

            // Ensure responsive hover effects
            if (Event.current.type == EventType.MouseMove) Repaint();

            // Delegate drawing to the Drawer, passing the Logic and Window reference
            _drawer.Draw(position, _logic, this);
        }

        private void Update()
        {
            if (IsNotificationActive)
            {
                // Force continuous repaints while notification is active for smooth fade animation
                if (EditorApplication.timeSinceStartup - NotificationStartTime > NOTIFICATION_DURATION)
                {
                    IsNotificationActive = false; // Animation finished
                }
                Repaint();
            }
        }

        public void ShowCustomNotification(string message, bool isFavorite)
        {
            ShowCustomNotification(message, isFavorite ? KromaNotificationType.FavoriteAdded : KromaNotificationType.FavoriteRemoved);
        }

        public void ShowCustomNotification(string message, KromaNotificationType type)
        {
            ActiveNotificationMessage = message;
            ActiveNotificationType = type;
            IsNotificationFavoriteEvent = (type == KromaNotificationType.FavoriteAdded);
            NotificationStartTime = EditorApplication.timeSinceStartup;
            IsNotificationActive = true;
            Repaint();
        }
    }
}