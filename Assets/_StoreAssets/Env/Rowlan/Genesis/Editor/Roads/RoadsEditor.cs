#if __MICROVERSE_ROADS__

using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    [ExecuteInEditMode]
    [CustomEditor(typeof(RoadsData))]
    public class RoadsEditor : Editor
    {
        private RoadsEditor editor;
        private RoadsData editorTarget;

        private RoadsModule module;

        public RoadsEditor()
        {
            this.module = new RoadsModule();
        }
        public void OnEnable()
        {
            editor = this;
            editorTarget = (RoadsData)target;

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