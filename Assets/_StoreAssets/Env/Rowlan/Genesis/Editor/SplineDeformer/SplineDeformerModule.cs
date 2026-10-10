#if __MICROVERSE_SPLINES__

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.SplineDeformer
{
    public class SplineDeformerModule
    {
        private static GUIContent splineContainerContent = new GUIContent("Spline Container", "A single spline container or select multiple in the hierarchy");

        private SplineDeformerModule editor;
        private SplineDeformerData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty autoAssignment;

        private SerializedProperty module;

        private ISplineDeformerModule stackedDeformerModule;

        private GUIContent[] modeButtons;

  
        public void OnEnable(SerializedObject serializedObject, SplineDeformerData target)
        {
            this.editor = this;
            this.editorTarget = target;
            this.serializedObject = serializedObject;

            autoAssignment = serializedObject.FindProperty("autoAssignment");

            this.stackedDeformerModule = new StackedDeformerModule(editor, serializedObject);
            
            Init();

        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Stacked Deformer", "Stacked Deformer settings"),
            };

            module = serializedObject.FindProperty("module");

        }


        public void OnDisable()
        {
        }

        public SplineDeformerData GetEditorTarget()
        {
            return editorTarget;
        }

        #region Inspector
        public void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField("General Data", GUIStyles.BoxTitleStyle);

                EditorGUI.BeginChangeCheck();
                {
                    EditorGUILayout.PropertyField(autoAssignment, new GUIContent("Auto Assignment", "Automatically assign a gameobject on selection change"));
                }
                if (EditorGUI.EndChangeCheck())
                {
                    AssignSelectedGameObject();
                }

                GUI.backgroundColor = HasSplineContainer() ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                {

                    bool showMultiple = autoAssignment.boolValue == true && HasMultipleSelectedObjects();
                    if (showMultiple)
                    {
                        EditorGUILayout.LabelField( "Spline Container", "<Multiple>");
                    }
                    else
                    {
                        // the spline container
                        // note: object isn't serialized
                        editorTarget.splineContainer = EditorGUILayout.ObjectField( splineContainerContent, editorTarget.splineContainer, typeof(SplineContainer), true) as SplineContainer;
                    }

                    if (!HasSplineContainer())
                    {
                        EditorGUILayout.HelpBox("Please select a gameobject with a spline container", MessageType.Info);
                    }

                }
                GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
            }
            EditorGUILayout.EndVertical();

            // separator
            // GUIStyles.DrawSeparator(); // for some reason the horizontal line isn't drawn => reverting to simple space for now
            GUILayout.Space(10f);

            // modules
            EditorGUI.BeginChangeCheck();
            {
                module.intValue = GUILayout.Toolbar(module.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((SplineDeformerData.Module)module.intValue);
            }

            // control
            switch (editorTarget.module)
            {
                case SplineDeformerData.Module.StackedDeformer:
                    stackedDeformerModule.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported mode: " + editorTarget.module);
            }

            // apply changes to serializedProperty
            serializedObject.ApplyModifiedProperties();

        }

        private bool HasSplineContainer()
        {
            return editorTarget.splineContainer != null;
        }

        private bool HasMultipleSelectedObjects()
        {
            return Selection.objects != null && Selection.objects.Length > 1;
        }
        #endregion Inspector

        private void ModeChanged(SplineDeformerData.Module mode)
        {
            // nothing to do yet
        }

        public void AssignSelectedGameObject()
        {
            if (!autoAssignment.boolValue)
                return;

            GameObject selectedObject = Selection.activeGameObject;

            // the stamp which is about to be assigned
            SplineContainer splineContainer = null;

            // get the stamp from the selected object
            // note: selected object can actually be null
            if (selectedObject != null)
            {
                splineContainer = selectedObject.GetComponent<SplineContainer>();
            }

            // assign stamp
            editorTarget.splineContainer = splineContainer;

            EditorUtility.SetDirty(serializedObject.targetObject);

        }

        public SplineContainer[] GetSelectedSplineContainers()
        {
            List<SplineContainer> selectedComponents = new List<SplineContainer>();

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null)
                    continue;

                SplineContainer[] component = go.GetComponents<SplineContainer>();

                selectedComponents.AddRange(component);
            }

            return selectedComponents.ToArray();
        }
    }

}

#endif