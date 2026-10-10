#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class KnotDesigner : IRoadDesigner
    {
        private RoadDesignController controller;

        #region Settings

        private float roadPercentage = 60f;
        private float heightOffset = 0f;
        private float separation = 40f;
        private float tangentSize = 40f;

        #endregion Settings

        public KnotDesigner(RoadDesignController controller)
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
                    separation = EditorGUILayout.FloatField("Separation", separation);
                    tangentSize = EditorGUILayout.FloatField("Tangent", tangentSize);

                    if (check.changed)
                    {
                        CreateKnot();
                    }
                }

                // creation
                if (GUILayout.Button("Create Knot"))
                {
                    CreateKnot();
                }
            }
            EditorGUILayout.EndVertical();

        }

        private void CreateKnot()
        {
            if (controller.GetEditor().GetUndoableSpline("Knot", out Road road, out SplineContainer splineContainer, out Spline spline))
            {
                if (RoadSplineUtils.GetSelectedKnotsBounds(splineContainer, spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex))
                {
                    RoadSplineUtils.Knot(splineContainer, spline, firstKnotIndex, lastKnotIndex, roadPercentage, heightOffset, separation, tangentSize);
                }
            }
        }
    }
}
#endif