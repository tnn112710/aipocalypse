using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    public class GameManagerModule : IGameplayModule
    {
        private GameplayModule editor;
        private GameplayData editorTarget;
        private SerializedObject serializedObject;

        private GameManagerController controller1;

        public GameManagerModule(GameplayModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new GameManagerController(editor);

        }

        public void OnInspectorGUI()
        {

            // EditorGUILayout.LabelField("Game Manager", GUIStyles.BoxTitleStyle);

            controller1.OnInspectorGUI();

        }


    }
}