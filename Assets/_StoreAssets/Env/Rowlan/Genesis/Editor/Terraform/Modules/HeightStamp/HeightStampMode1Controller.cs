using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Terraform
{
    public class HeightStampMode1Controller
    {
        private TerraformModule editor;

        private float scaleChange = 0.2f;

        public HeightStampMode1Controller(TerraformModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Common", GUIStyles.BoxTitleStyle);

                if(GUILayout.Button("Fit & Center", GUILayout.Height(30)))
                {

                    HeightStampUtils.SetScale(GetEditorTargetTransform(), HeightStampUtils.ScaleOption.XYZ);
                    HeightStampUtils.SetPosition(GetEditorTargetTransform(), HeightStampUtils.StampPosition.Center);

                }

                EditorGUILayout.LabelField("Scale", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Fit to Terrain"))
                    {
                        HeightStampUtils.SetScale(GetEditorTargetTransform(), HeightStampUtils.ScaleOption.XYZ);
                    }

                    if (GUILayout.Button("Fit XZ"))
                    {
                        HeightStampUtils.SetScale(GetEditorTargetTransform(), HeightStampUtils.ScaleOption.XZ);
                    }

                    if (GUILayout.Button("Fit Y"))
                    {
                        HeightStampUtils.SetScale(GetEditorTargetTransform(), HeightStampUtils.ScaleOption.Y);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Increase Size"))
                    {
                        HeightStampUtils.IncreaseSize(GetEditorTargetTransform(), scaleChange);
                    }

                    if (GUILayout.Button("Decrease Size"))
                    {
                        HeightStampUtils.DecreaseSize(GetEditorTargetTransform(), scaleChange);
                    }

                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Position", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Center"))
                    {
                        HeightStampUtils.SetPosition(GetEditorTargetTransform(), HeightStampUtils.StampPosition.Center);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Rotation", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("45 Left", GUILayout.Width(80)))
                    {
                        HeightStampUtils.Rotate(GetEditorTargetTransform(), - 45f);
                    }

                    if (GUILayout.Button("Reset"))
                    {
                        HeightStampUtils.ResetRotation(GetEditorTargetTransform());
                    }

                    if (GUILayout.Button("45 Right", GUILayout.Width(80)))
                    {
                        HeightStampUtils.Rotate(GetEditorTargetTransform(), 45f);
                    }
                }
                EditorGUILayout.EndHorizontal();

#if __MICROVERSE_SPLINES__
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Splines", GUIStyles.BoxTitleStyle);

                if (GUILayout.Button("Fit Transform to Spline Area"))
                {
                    FitTransformToSplineArea();
                }
#endif
            }
            EditorGUILayout.EndVertical();
        }

        private Transform GetEditorTargetTransform()
        {
            return editor.GetEditorTarget().gameObject.transform;
        }

#if __MICROVERSE_SPLINES__
        private void FitTransformToSplineArea()
        {
            HeightStamp heightStamp = editor.GetEditorTarget().gameObject?.GetComponent<HeightStamp>();
            bool enabled = heightStamp?.falloff?.filterType == FalloffFilter.FilterType.SplineArea && heightStamp?.falloff?.splineArea != null;

            // check if criteria are met; can't do enabled/visibility changes depending on the height stamp inspector changes
            // because we don't have a hook there
            if ( !enabled)
            {
                EditorUtility.DisplayDialog("Error", "Falloff type spline area with spline area gameobject required", "Ok");
                return;
            }
            
            if(SplineUtils.GetSplineAreaBounds( GetEditorTargetTransform(), heightStamp.falloff.splineArea, out Bounds bounds))
            {
                heightStamp.transform.position = bounds.center;
                heightStamp.transform.localScale = bounds.size + heightStamp.falloff.splineAreaFalloffBoost * 2f * Vector3.one;

                EditorUtility.SetDirty(heightStamp.transform);

                MicroVerse.instance.Invalidate();

                SceneView.lastActiveSceneView.Repaint();
            }
        }
#endif
    }
}