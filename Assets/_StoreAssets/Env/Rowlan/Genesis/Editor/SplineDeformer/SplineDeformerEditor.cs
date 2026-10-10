#if __MICROVERSE_SPLINES__

using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.SplineDeformer
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(SplineDeformerData))]
    public class SplineDeformerEditor : Editor
    {
        private SplineDeformerEditor editor;
        private SplineDeformerData editorTarget;

        private SplineDeformerModule module;

        public SplineDeformerEditor()
        {
            this.module = new SplineDeformerModule();
        }

        public void OnEnable()
        {
            editor = this;
            editorTarget = (SplineDeformerData)target;

            module.OnEnable(serializedObject, editorTarget);

            if (editorTarget.autoAssignment)
            {
                AssignSelectedGameObject();
            }
        }

        public void OnDisable()
        {
            module.OnDisable();
        }
        public override void OnInspectorGUI()
        {
            module.OnInspectorGUI();
        }

        public void AssignSelectedGameObject()
        {
            module.AssignSelectedGameObject();
        }
    }
}
#endif