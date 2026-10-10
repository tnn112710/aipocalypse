using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public class TerraformModule
    {
        private TerraformModule editor;
        private TerraformData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty autoAssignment;

        private ITerraformModule splinePathModule;
        private ITerraformModule heightStampModule;
        private ITerraformModule falloffModule;

        public void OnEnable(SerializedObject serializedObject, TerraformData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            autoAssignment = serializedObject.FindProperty("autoAssignment");

            this.splinePathModule = new SplinePathModule(editor, serializedObject);
            this.heightStampModule = new HeightStampModule(editor, serializedObject);
            this.falloffModule = new FalloffModule(editor, serializedObject);
        }

        public void OnDisable()
        {
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }

        public TerraformData GetEditorTarget()
        {
            return editorTarget;
        }

        #region Inspector
        public void OnInspectorGUI()
        {
            serializedObject.Update();

            // data
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("General Data", GUIStyles.BoxTitleStyle);

                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(autoAssignment, new GUIContent("Auto Assignment", "Automatically assign a gameobject on selection change"));
                    }
                    if( EditorGUI.EndChangeCheck())
                    {
                        AssignSelectedGameObject();
                    }

                    
                    GUI.backgroundColor = HasMicroverseGameObject() ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                    {
                        editorTarget.gameObject = EditorGUILayout.ObjectField(new GUIContent("MicroVerse Object"), editorTarget.gameObject, typeof(GameObject), true) as GameObject; // object isn't serialized

                        
                        // visualize the stamp
                        /*
                        GUI.enabled = false;
                        {
                            EditorGUILayout.ObjectField(new GUIContent("Type"), editorTarget..., typeof(.Name.Length.), true); // type isn't serialized
                        }
                        GUI.enabled = true;
                        */

                        if (!HasMicroverseGameObject())
                        {
                            EditorGUILayout.HelpBox("Please select a MicroVerse object", MessageType.Info);
                        }

                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
            }
            EditorGUILayout.EndVertical();

            // microverse gameobject details
            if(HasMicroverseGameObject())
            {
                // separator
                GUIStyles.DrawSeparator();

                // modules
                if (splinePathModule.IsSupported( editorTarget.gameObject))
                {
                    splinePathModule.OnInspectorGUI();
                }
                else if (heightStampModule.IsSupported(editorTarget.gameObject))
                {
                    heightStampModule.OnInspectorGUI();
                }
                else if (falloffModule.IsSupported(editorTarget.gameObject))
                {
                    falloffModule.OnInspectorGUI();
                }
                else
                {
                    EditorGUILayout.HelpBox( "Not available for " + editorTarget.gameObject.GetType().Name, MessageType.Error);
                }
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();
        }

        private bool HasMicroverseGameObject()
        {
            return editorTarget.gameObject != null;
        }
        #endregion Inspector


        public void AssignSelectedGameObject()
        {
            if (!autoAssignment.boolValue)
                return;

            GameObject selectedObject = Selection.activeGameObject;

            // the stamp which is about to be assigned
            GameObject gameObject = null;

            // get the stamp from the selected object
            // note: selected object can actually be null
            if (selectedObject != null)
            {
                gameObject = selectedObject.GetComponent<Stamp>()?.gameObject;

                if( gameObject == null)
                {
                    gameObject = selectedObject.GetComponent<FalloffOverride>()?.gameObject;
                }


            }

            // assign stamp
            editorTarget.gameObject = gameObject;

            EditorUtility.SetDirty(serializedObject.targetObject);
            
        }
        
    }
}

