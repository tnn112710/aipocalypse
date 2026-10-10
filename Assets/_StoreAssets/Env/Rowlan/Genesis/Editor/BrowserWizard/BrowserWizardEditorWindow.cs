#if __MICROVERSE_SPLINES__
using UnityEngine;
using UnityEditor;
using JBooth.MicroVerseCore;

namespace Rowlan.Genesis.BrowserWizard
{
    public class BrowserWizardEditorWindow : EditorWindow
    {
        private BrowserWizardEditorWindow editorWindow;
        private BrowserWizardEditor editor;
        private BrowserWizardData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            BrowserWizardEditorWindow window = (BrowserWizardEditorWindow)EditorWindow.GetWindow(typeof(BrowserWizardEditorWindow));

            window.titleContent = new GUIContent("Browser Wizard");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<BrowserWizardData> settingsManager = new ScriptableObjectManager<BrowserWizardData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as BrowserWizardEditor;

            editor.OnEnable();

        }

        public void OnDisable()
        {
            editor.OnDisable();

            DestroyImmediate(editor);

        }

        void OnGUI()
        {
            if (editor == null)
                return;

            // header
            DrawDialogHeader();

            // editor
            if( MicroVerse.instance == null)
            {
                EditorGUILayout.HelpBox("MicroVerse instance required", MessageType.Info);
            }
            else
            {
                editor.OnInspectorGUI();
            }
        }


        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Browser Wizard (Beta)", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Browser Wizard for MicroVerse"
                    + "\n"
                    + "Placement of biomes using the MicroVerse Content Browser"
                    , MessageType.Info);
            }
        }
        #endregion Header

    }
}
#endif