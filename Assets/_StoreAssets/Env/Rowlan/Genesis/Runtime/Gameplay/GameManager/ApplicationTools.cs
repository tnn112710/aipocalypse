using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rowlan.Genesis.Gameplay
{
    /// <summary>
    /// Various application related tools
    /// 
    /// Keybindings:
    /// 
    /// + ESC: end application in editor mode or quit the compiled application
    /// + F5: restart scene
    /// 
    /// Hide Cursor:
    /// 
    /// + in Editor Player or Build or Both
    /// 
    /// Supports both Legacy Input Manager and the new Input System package.
    /// The active input handling is detected automatically via preprocessor defines:
    /// 
    /// + ENABLE_INPUT_SYSTEM: defined when the Input System package is active
    /// + ENABLE_LEGACY_INPUT_MANAGER: defined when the legacy Input Manager is active
    /// + Both are defined when Player Settings → Active Input Handling is set to "Both"
    /// 
    /// </summary>
    public class ApplicationTools : MonoBehaviour
    {
        #region Input

        [Header("Input")]

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Key for quitting the game
        /// </summary>
        public Key quitGameKey = Key.Escape;

        /// <summary>
        /// Key for restarting the game
        /// </summary>
        public Key restartKey = Key.F5;
#else
        /// <summary>
        /// Key for quitting the game
        /// </summary>
        public KeyCode quitGameKey = KeyCode.Escape;

        /// <summary>
        /// Key for restarting the game
        /// </summary>
        public KeyCode restartKey = KeyCode.F5;
#endif

        #endregion Input

        #region Settings
        public enum HideCursor
        {
            None,
            Editor,
            Build,
            Both
        }

        [Header("Cursor")]

        public HideCursor hideCursor = HideCursor.Build;
        #endregion Settings

        void Start()
        {
            ApplyCursorLogic();
        }

        /// <summary>
        /// Hide the cursor depending on the settings
        /// </summary>
        void ApplyCursorLogic()
        {
            bool cursorHidden = Application.isPlaying
                && ((Application.isEditor && hideCursor == HideCursor.Editor)
                || (!Application.isEditor && hideCursor == HideCursor.Build)
                || hideCursor == HideCursor.Both);

            if (cursorHidden)
            {
                // set Cursor to not be visible
                Cursor.visible = false;

                // lock the cursor
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        void Update()
        {
            HandleInputKeys();
        }

        private void HandleInputKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[quitGameKey].wasPressedThisFrame)
            {
                QuitGame();
            }
            else if (keyboard[restartKey].wasPressedThisFrame)
            {
                RestartScene();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(quitGameKey))
            {
                QuitGame();
            }
            else if (Input.GetKeyDown(restartKey))
            {
                RestartScene();
            }
#endif
        }

        public void RestartScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitGame()
        {
            // save any game data here
#if UNITY_EDITOR
            // Application.Quit() does not work in the editor so
            // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}