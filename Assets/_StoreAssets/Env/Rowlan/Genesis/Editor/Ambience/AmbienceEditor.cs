using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(AmbienceData))]
    public class AmbienceEditor : Editor
    {
        private AmbienceEditor editor;
        private AmbienceData editorTarget;

        private AmbienceModule module;

        public AmbienceEditor()
        {
            this.module = new AmbienceModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (AmbienceData)target;

            module.OnEnable(serializedObject, editorTarget);

        }

        public void OnDisable()
        {
            module.OnDisable();
        }
        public override void OnInspectorGUI()
        {
            module.OnInspectorGUI();
        }

    }
}

