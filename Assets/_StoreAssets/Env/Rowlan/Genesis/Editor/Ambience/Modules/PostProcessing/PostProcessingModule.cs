using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    public class PostProcessingModule : IAmbienceModule
    {
        private AmbienceModule editor;
        private AmbienceData editorTarget;
        private SerializedObject serializedObject;




        private PostProcessingController controller1;

        public PostProcessingModule(AmbienceModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new PostProcessingController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}