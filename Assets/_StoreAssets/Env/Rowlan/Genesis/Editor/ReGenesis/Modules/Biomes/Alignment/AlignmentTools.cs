using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    /// <summary>
    /// Tools for prefab alignment
    /// Example: 
    ///     Have backdrop mountains distributed and inside a parent gameobject.
    ///     Align Center would reposition the direct children, so that the main handle is in the center.
    ///     Dragging back to the content browser would create a preset which when dragged back to the scene would have the gameobjects positioned around the mouse cursor.
    /// </summary>
    public class AlignmentTools
    {
        private enum OffsetType
        {
            Center,
            BottomCenter
        }

        private ReGenesisModule editor;

        private float distance = 10f;
        private bool lockYAxis = true;

        public AlignmentTools(ReGenesisModule editor)
        {
            this.editor = editor;

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Align main handle (eg set pivot to center of a circle of background mountains)", MessageType.None);

            EditorGUILayout.HelpBox("Selected gameobject\n* calculate bounds of selected gameobject\n* reposition direct children of selected gameobject\n* drag to content browser and create preset\n* drag from content browser and have preset eg centered at mouse\n* example: use eg to calculate bounds of backdrop mountains and center the main handle", MessageType.None);

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Align Center", GUILayout.Height(30)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Align Center");

                        AlignChildren(OffsetType.Center);

                    }

                    if (GUILayout.Button("Align Bottom Center", GUILayout.Height(30)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Align Bottom Center");

                        AlignChildren(OffsetType.BottomCenter);

                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.HelpBox("Reposition prefabs (eg move background mountains away from center or pull towards center)", MessageType.None);

                distance = EditorGUILayout.FloatField(new GUIContent("Distance", "Distance to move the prefabs from center"), distance);
                lockYAxis = EditorGUILayout.Toggle( new GUIContent( "Lock Y Axis", "Calculate distance only for XZ if locked, XYZ if unlocked"), lockYAxis);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Change Distance from Center", GUILayout.Height(30)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Change Distance");

                        ChangeDistance(distance);

                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Reposition the direct children depending on the offset type.
        /// Direct children only, others aren't necessary and eg would break if we'd also reposition eg children of a LODGroup.
        /// </summary>
        /// <param name="offsetType"></param>
        private void AlignChildren( OffsetType offsetType)
        {
            GameObject go = editor.GetEditorTarget().gameObject;

            if (go == null)
                return;

            if (BoundsUtils.CalculateBounds(go, out Bounds bounds))
            {
                Transform[] transforms = go.GetComponentsInChildren<Transform>();

                Vector3 offset = Vector3.zero;
                
                // calculate offset
                switch( offsetType)
                {
                    case OffsetType.Center:
                        offset = go.transform.position - bounds.center;
                        break;
                    case OffsetType.BottomCenter:
                        offset = go.transform.position - bounds.center + new Vector3(0f, bounds.extents.y, 0f);
                        break;
                }

                // reposition direct children
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform transform = go.transform.GetChild(i);

                    transform.position += offset;
                }
            }
        }

        /// <summary>
        /// Change distance of prefabs. Move away from center or pull towards center depending on distance being positive or negative
        /// </summary>
        /// <param name="distance"></param>
        private void ChangeDistance(float distance)
        {
            GameObject go = editor.GetEditorTarget().gameObject;

            if (go == null)
                return;

            if (BoundsUtils.CalculateBounds(go, out Bounds bounds))
            {
                Transform[] transforms = go.GetComponentsInChildren<Transform>();

                Vector3 offset = Vector3.zero;

                // reposition direct children
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform transform = go.transform.GetChild(i);

                    Vector3 direction = transform.position - bounds.center;
                    direction = direction.normalized;

                    if( lockYAxis)
                    {
                        direction.y = 0f;
                    }

                    transform.position += direction * distance;


                }
            }
        }
    }
}