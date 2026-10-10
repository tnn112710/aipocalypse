using JBooth.MicroVerseCore;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Distribution
{
    public class DistributionModule
    {
        private DistributionModule editor;
        private DistributionData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty autoAssignment;

        private IDistributionModule filterModule;

        public void OnEnable(SerializedObject serializedObject, DistributionData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            autoAssignment = serializedObject.FindProperty("autoAssignment");

            this.filterModule = new FilterModule(editor, serializedObject);
        }

        public void OnDisable()
        {
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }

        public DistributionData GetEditorTarget()
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
                        bool showMultiple = autoAssignment.boolValue == true && HasMultipleSelectedObjects();
                        if (showMultiple)
                        {
                          EditorGUILayout.LabelField("MicroVerse Object", "<Multiple>");
                        }
                        else
                        {
                            editorTarget.gameObject = EditorGUILayout.ObjectField(new GUIContent("MicroVerse Object"), editorTarget.gameObject, typeof(GameObject), true) as GameObject; // object isn't serialized
                        }

                        
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
                if (filterModule.IsSupported(editorTarget.gameObject))
                {
                    filterModule.OnInspectorGUI();
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
        private bool HasMultipleSelectedObjects()
        {
            return Selection.objects != null && Selection.objects.Length > 1;
        }
        #endregion Inspector


        public void AssignSelectedGameObject()
        {
            if (!autoAssignment.boolValue)
                return;

            GameObject selectedObject = Selection.activeGameObject;

            // the stamp which is about to be assigned
            GameObject gameObject = null;

            // get any gameobject within the microverse hierarchy
            // note: selected object can actually be null
            if (selectedObject != null)
            {
                bool isInMicroVerseHierarchy = selectedObject.GetComponent<MicroVerse>() != null || selectedObject.GetComponentInParent<MicroVerse>() != null;

                if(isInMicroVerseHierarchy)
                {
                    gameObject = selectedObject;
                }
            }

            // assign stamp
            editorTarget.gameObject = gameObject;

            EditorUtility.SetDirty(serializedObject.targetObject);
            
        }
    }
}

