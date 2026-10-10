#if __MICROVERSE_SPLINES__

using UnityEditor;

namespace Rowlan.Genesis.SplineDeformer
{
    public class StackedDeformerModule : ISplineDeformerModule
    {
        private SplineDeformerModule editor;
        private SplineDeformerData editorTarget;
        private SerializedObject serializedObject;

        private StackedDeformerController controller1;

        public StackedDeformerModule(SplineDeformerModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new StackedDeformerController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}
#endif