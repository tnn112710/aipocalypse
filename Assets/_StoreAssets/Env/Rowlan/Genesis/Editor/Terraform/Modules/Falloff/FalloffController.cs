using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public class FalloffController
    {
        private TerraformModule editor;

        public FalloffController(TerraformModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Hierarchy", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Reset Child Transforms"))
                    {
                        ResetChildTransforms();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void ResetChildTransforms()
        {
            GameObject target = editor.GetEditorTarget().gameObject;

            if (target == null)
                return;

            Transform[] transforms = target?.GetComponentsInChildren<Transform>();

            bool isContinue = EditorUtility.DisplayDialog("Reset Child Transforms", $"This will reset the transforms of all {transforms.Length - 1} children of {target.name}. Continue?", "Yes", "No");

            if (!isContinue)
                return;

            foreach( Transform transform in transforms)
            {
                // skip self
                if (transform == editor.GetEditorTarget().gameObject.transform)
                    continue;

                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;
            }
        }
    }
}