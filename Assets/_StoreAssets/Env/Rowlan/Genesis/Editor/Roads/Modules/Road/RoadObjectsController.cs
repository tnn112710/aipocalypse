#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using static Rowlan.Genesis.RotationUtils;

namespace Rowlan.Genesis.Roads
{
    public class RoadObjectsController
    {

        public enum AddScript
        {
            None,
            SplineRelativeTransform,
        }

        private RoadsModule editor;

        private GameObject parent = null;
        private GameObject prefab = null;

        private RotationAxis rotationAxis;
        private Vector3 positionOffset = Vector3.zero;

        private AddScript addScript;

        public RoadObjectsController(RoadsModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Spawn objects along the spline, like e. g. struts along a rollercoaster", MessageType.None);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Objects", GUIStyles.BoxTitleStyle);

                EditorGUILayout.HelpBox("Parent is a container into which to put the prefabs. Don't use the road.", MessageType.None);
                EditorGUILayout.BeginHorizontal();
                {
                    parent = EditorGUILayout.ObjectField("Parent", parent, typeof(GameObject), true) as GameObject;
                    if (GUILayout.Button(new GUIContent("Clear", "Remove all objects from this container"), EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        GameObjectUtils.ClearContainerChildren(parent, true);
                    }
                }
                GUILayout.EndHorizontal();

                prefab = EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false) as GameObject;

                rotationAxis = (RotationAxis) EditorGUILayout.EnumPopup("Rotation Axis", rotationAxis);

                EditorGUILayout.BeginHorizontal();
                {
                    // position offset. can't use Vector3Field's label directly because it wraps to the next line
                    EditorGUILayout.PrefixLabel("Position Offset");
                    positionOffset = EditorGUILayout.Vector3Field("", positionOffset);
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("Script which will be added to the prefab instance.", MessageType.None);
                addScript = (AddScript)EditorGUILayout.EnumPopup("Add Script", addScript);

                GUILayout.Space(10);

                GUI.enabled = prefab != null && parent != null;
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Add Prefabs at Knots"))
                        {
                            AddPrefabs();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

        }

        private void AddPrefabs()
        {
            SplineContainer splineContainer = editor.GetEditorTarget().gameObject?.GetComponent<Road>()?.GetComponent<SplineContainer>();
            Spline spline = splineContainer?.Spline;

            if ( prefab == null || parent == null || spline == null)
                return;

            // Undo.RegisterFullObjectHierarchyUndo( parent, "Add prefabs at knots"); // doesn't work

            IEnumerable<BezierKnot> knots = spline.Knots;

            foreach( BezierKnot knot in knots)
            {
                // evaluate spline data
                SplineUtility.GetNearestPoint(spline, knot.Position, out var nearest, out float t);
                splineContainer.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

                tangent = math.normalizesafe(tangent);
                up = math.normalizesafe(up);

                Quaternion rotation = Quaternion.LookRotation(tangent, up);

                // constrain rotation
                Vector3 euler = rotation.eulerAngles;
                rotation.eulerAngles = new Vector3(
                    euler.x * rotationAxis.GetVector().x,
                    euler.y * rotationAxis.GetVector().y,
                    euler.z * rotationAxis.GetVector().z);

                // instantiate prefab
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.transform) as GameObject;

                instance.transform.position = (Vector3) position + positionOffset;
                instance.transform.rotation = rotation;

                // add script
                switch (addScript)
                {
                    case AddScript.None:
                        break;

                    case AddScript.SplineRelativeTransform:
                        SplineRelativeTransform script = instance.AddComponent<SplineRelativeTransform>();
                        script.splineContainer = splineContainer;
                        break;

                    default:
                        break;
                }
            }


        }
    }
}
#endif