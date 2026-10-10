using UnityEngine;
using UnityEditor;

namespace Rowlan.Genesis.Terraform
{
    public class TerraformEditorEditorWindow : EditorWindow
    {
        private TerraformEditorEditorWindow editorWindow;
        private TerraformEditor editor;
        private TerraformData data;

        public static bool helpBoxVisible = false;

        [MenuItem( ProjectSetup.MENU)]
        public static void CreateWindow()
        {
            // Get existing open window or if none, make a new one:
            TerraformEditorEditorWindow window = (TerraformEditorEditorWindow)EditorWindow.GetWindow(typeof(TerraformEditorEditorWindow));

            window.titleContent = new GUIContent( "Terraform");

            window.Show();
        }


        void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<TerraformData> settingsManager = new ScriptableObjectManager<TerraformData>(ProjectSetup.SETTINGS_DIRECTORY, ProjectSetup.SETTINGS_FILENAME);
            data = settingsManager.GetAsset();

            editor = Editor.CreateEditor(data) as TerraformEditor;

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
            GUIUtils.DrawHeader("Terraform", ref helpBoxVisible);

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