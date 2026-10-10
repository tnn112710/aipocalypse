#if __MICROVERSE_ROADS__

using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class JitterModule
    {

        public enum FilterType
        {
            Selection,
            Children
        }

        FilterType filterType;
        RoadSystem roadSystem;

        SerializedProperty positionOffset;
        SerializedProperty rotationOffset;
        SerializedProperty heightOffset;

        /// <summary>
        /// Required for filter type Children, can be null otherwise
        /// </summary>
        GameObject childrenFilterParent;

        public JitterModule(RoadSystem roadSystem, FilterType filterType, SerializedProperty jitterSettings, GameObject childrenFilterParent)
        {
            this.roadSystem = roadSystem;
            this.filterType = filterType;

            rotationOffset = jitterSettings.FindPropertyRelative("rotationOffset");
            heightOffset = jitterSettings.FindPropertyRelative("heightOffset");
            positionOffset = jitterSettings.FindPropertyRelative("positionOffset");

            this.childrenFilterParent = childrenFilterParent;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Intersection Jitter", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.PropertyField(positionOffset);

                    if (GUILayout.Button("Random Position"))
                    {
                        RandomPositions();
                    }

                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.PropertyField(rotationOffset);

                    if (GUILayout.Button("Random Rotation"))
                    {
                        RandomRotations();
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.PropertyField(heightOffset);

                    if (GUILayout.Button("Random Height"))
                    {
                        RandomHeights();
                    }

                }
                EditorGUILayout.EndVertical();

            }
            EditorGUILayout.EndVertical();

        }

        private List<Intersection> GetIntersections()
        {
            switch (filterType)
            {
                case FilterType.Selection:
                    return SelectionUtils.GetSelectedIntersections().Cast<Intersection>().ToList();

                case FilterType.Children:
                    return childrenFilterParent.GetComponentsInChildren<Intersection>().ToList();
            }

            return new List<Intersection>();
        }

        private void RandomPositions()
        {
            List<Intersection> intersections = GetIntersections();

            foreach (Intersection intersection in intersections)
            {
                float min = positionOffset.vector2Value.x;
                float max = positionOffset.vector2Value.y;

                IntersectionUtils.RandomPosition(roadSystem, intersection, min, max);
            }
        }

        private void RandomRotations()
        {
            List<Intersection> intersections = GetIntersections();

            foreach (Intersection intersection in intersections)
            {
                float min = rotationOffset.vector2Value.x;
                float max = rotationOffset.vector2Value.y;

                IntersectionUtils.RandomRotation(roadSystem, intersection, min, max);
            }
        }

        private void RandomHeights()
        {
            List<Intersection> intersections = GetIntersections();

            foreach (Intersection intersection in intersections)
            {
                float min = heightOffset.vector2Value.x;
                float max = heightOffset.vector2Value.y;

                IntersectionUtils.RandomHeight(roadSystem, intersection, min, max);
            }
        }
    }
}
#endif