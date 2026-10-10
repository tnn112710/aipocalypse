using JBooth.MicroVerseCore;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class ReGenesisModule
    {
        private ReGenesisModule editor;
        private ReGenesisData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty autoAssignment;

        private IReGenesisModule heightStampModule;
        private IReGenesisModule biomesModule;

        public void OnEnable(SerializedObject serializedObject, ReGenesisData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            autoAssignment = serializedObject.FindProperty("autoAssignment");

            this.heightStampModule = new HeightStampModule(editor, serializedObject);
            this.biomesModule = new BiomesModule(editor, serializedObject);
        }

        public void OnDisable()
        {
        }

        public SerializedObject GetSerializedObject()
        {
            return this.serializedObject;
        }

        public ReGenesisData GetEditorTarget()
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
                if (heightStampModule.IsSupported(editorTarget.gameObject))
                {
                    heightStampModule.OnInspectorGUI();
                }
                else if (biomesModule.IsSupported(editorTarget.gameObject))
                {
                    biomesModule.OnInspectorGUI();
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

        /// <summary>
        /// Get all transforms of the selected gameobjects which include a height stamp
        /// </summary>
        /// <returns></returns>
        public Transform[] GetSelectedHeightStampTransforms()
        {
            List<Transform> transforms = new List<Transform>();

            foreach (GameObject go in Selection.objects)
            {
                if (go == null)
                    continue;

                if (go.GetComponent<HeightStamp>() != null)
                {
                    if (!transforms.Contains(go.gameObject.transform))
                    {
                        transforms.Add(go.gameObject.transform);
                    }
                }
            }

            return transforms.ToArray();
        }

        public HeightStamp[] GetSelectedHeightStamps()
        {
            List<HeightStamp> selectedHeightStamps = new List<HeightStamp>();

            foreach (GameObject go in Selection.objects)
            {
                if (go == null)
                    continue;

                HeightStamp[] stamps = go.GetComponents<HeightStamp>();

                selectedHeightStamps.AddRange(stamps);
            }

            return selectedHeightStamps.ToArray();
        }
    }
}

