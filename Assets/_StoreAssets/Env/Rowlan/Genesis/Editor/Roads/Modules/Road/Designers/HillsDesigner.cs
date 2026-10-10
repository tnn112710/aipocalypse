#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class HillsDesigner : IRoadDesigner
    {
        private RoadDesignController controller;

        #region Settings

        private float roadPercentage = 100f;
        private float heightOffset = 20f;
        private int hillCount = 3;

        #endregion Settings

        public HillsDesigner(RoadDesignController controller)
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
                // settings
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    roadPercentage = EditorGUILayout.FloatField(new GUIContent("Road Percentage", "Percentage of the road where the knot should start"), roadPercentage);
                    heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);
                    hillCount = EditorGUILayout.IntField("Hill Count", hillCount);

                    if (check.changed)
                    {
                        CreateKnot();
                    }
                }

                // creation
                if (GUILayout.Button("Create Hills"))
                {
                    CreateKnot();
                }
            }
            EditorGUILayout.EndVertical();

        }

        private void CreateKnot()
        {
            if (controller.GetEditor().GetUndoableSpline("Hills", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.Hills(splineContainer, spline, firstKnotIndex, lastKnotIndex, roadPercentage, hillCount, heightOffset);
                }
            }
        }
    }
}
#endif