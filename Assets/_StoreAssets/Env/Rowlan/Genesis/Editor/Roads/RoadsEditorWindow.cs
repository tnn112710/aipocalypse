#if __MICROVERSE_ROADS__

using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Roads
{
    public class RoadsEditorWindow : EditorWindow
    {
        private RoadsEditorWindow editorWindow;
        private RoadsEditor editor;
        private RoadsData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            RoadsEditorWindow window = (RoadsEditorWindow)EditorWindow.GetWindow(typeof(RoadsEditorWindow));

            window.titleContent = new GUIContent( "Roads");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<RoadsData> settingsManager = new ScriptableObjectManager<RoadsData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as RoadsEditor;

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
            editor.OnInspectorGUI();
        }

        /// <summary>
        /// Analyze selection, assign gameobject
        /// </summary>
        void OnSelectionChange()
        {
            editor.AssignSelectedGameObject();

            this.Repaint();
        }

        #region Header
        private void DrawDialogHeader()
        {
            // common header
            GUIUtils.DrawHeader("Roads", ref helpBoxVisible);

            // help
            if (helpBoxVisible)
            {
                EditorGUILayout.HelpBox(
                    "Tools for MicroVerse objects"
                    + "\n"
                    + "Please assign a MicroVerse object or select one with Auto Assignment active"
                    , MessageType.Info);
            }
        }
        #endregion Header
    }
}
#endif