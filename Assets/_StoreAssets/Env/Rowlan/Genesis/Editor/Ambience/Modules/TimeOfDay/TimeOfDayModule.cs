using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    public class TimeOfDayModule : IAmbienceModule
    {
        private AmbienceModule editor;
        private AmbienceData editorTarget;
        private SerializedObject serializedObject;

        private TimeOfDayController controller1;

        public TimeOfDayModule(AmbienceModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {

            // controllers
            controller1 = new TimeOfDayController(editor);

        }

        public void OnInspectorGUI()
        {
            controller1.OnInspectorGUI();
        }


    }
}