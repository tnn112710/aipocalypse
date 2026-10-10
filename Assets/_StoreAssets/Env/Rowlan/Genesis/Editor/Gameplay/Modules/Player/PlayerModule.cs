using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Gameplay
{
    public class PlayerModule : IGameplayModule
    {
        private GameplayModule editor;
        private GameplayData editorTarget;
        private SerializedObject serializedObject;

        private PlayerController controller1;

        public PlayerModule(GameplayModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new PlayerController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}