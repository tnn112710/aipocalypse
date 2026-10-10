#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class TwistDesigner : IRoadDesigner
    {
        private RoadDesignController controller;

        #region Settings

        private float roadPercentage = 60f;
        private int resolutionDegrees = 90;

        #endregion Settings

        public TwistDesigner(RoadDesignController controller)
        {
            this.controller = controller;
        }

        public void OnInspectorGUI()
        {
            // consistency checks
            if (!controller.IsRollEnabled())
            {
                EditorGUILayout.HelpBox("Roll must be enabled on the Road", MessageType.Error);
            }

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("Note: The direction of the created road is currently important", MessageType.Info);

                // settings
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    roadPercentage = EditorGUILayout.FloatField("Road Percentage", roadPercentage);
                    resolutionDegrees = EditorGUILayout.IntField("Resolution Degrees", resolutionDegrees);

                    // data consistency
                    roadPercentage = Mathf.Clamp(roadPercentage, 0, 100);
                    resolutionDegrees = Mathf.Clamp(resolutionDegrees, 10, 120);

                    if (check.changed)
                    {
                        CreateTwist();
                    }
                }

                // creation
                if (GUILayout.Button(new GUIContent("Create Twist", "Create a twist along the forward axis using first and last knot of the spline")))
                {
                    CreateTwist();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void CreateTwist()
        {
            if( controller.GetEditor().GetUndoableSpline("Twist", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.Twist(splineContainer, spline, firstKnotIndex, lastKnotIndex, roadPercentage, resolutionDegrees);
                }
            }
        }
    }
}
#endif