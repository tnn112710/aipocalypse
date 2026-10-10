#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class LoopingDesigner : IRoadDesigner
    {
        private RoadDesignController controller;

        #region Settings

        private float roadPercentage = 50f;
        private float height = 40f;
        private float tangentSize = 10f;
        private float separation = 3f;

        #endregion Settings

        public LoopingDesigner(RoadDesignController controller)
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
                EditorGUILayout.HelpBox("The selected nodes shouldn't be Auto, this could lead to undesired behaviour (rotation etc)", MessageType.Info);

                // settings
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    roadPercentage = EditorGUILayout.FloatField("Road Percentage", roadPercentage);
                    height = EditorGUILayout.FloatField("Height", height);
                    tangentSize = EditorGUILayout.FloatField("Tangent Size", tangentSize);
                    separation = EditorGUILayout.FloatField("Separation", separation);

                    if (check.changed)
                    {
                        CreateLooping();
                    }
                }

                // creation
                if (GUILayout.Button("Create Looping"))
                {
                    CreateLooping();
                }
            }
            EditorGUILayout.EndVertical();
        }


        private void CreateLooping()
        {
            if (controller.GetEditor().GetUndoableSpline("Looping", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.Looping(splineContainer, spline, firstKnotIndex, lastKnotIndex, roadPercentage, height, tangentSize, separation);
                }
            }
        }
    }
}
#endif