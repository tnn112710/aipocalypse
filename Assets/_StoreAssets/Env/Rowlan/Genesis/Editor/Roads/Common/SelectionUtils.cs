#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Intersection;

namespace Rowlan.Genesis.Roads
{
    public class SelectionUtils
    {
        public static List<Component> GetSelectedComponents(Type[] types)
        {
            List<Component> selectedItems = new List<Component>();

            foreach (Type type in types)
            {
                foreach (GameObject go in Selection.objects)
                {
                    selectedItems.AddRange(go.GetComponents(type));
                }
            }

            return selectedItems;
        }

        /// <summary>
        /// Add all roads of the selected intersections to the current selection
        /// </summary>
        public static void AddConnectedRoadsToSelection()
        {
            GameObject prevActiveGameObject = Selection.activeGameObject;

            List<Component> intersections = GetSelectedIntersections();

            List<GameObject> roadGameObjects = new List<GameObject>();

            foreach (Intersection intersection in intersections)
            {
                List<Road> roads = intersection.GetRoads();
                foreach (Road road in roads)
                {
                    roadGameObjects.Add(road.gameObject);
                }
            }

            List<UnityEngine.Object> objects = Selection.objects.ToList();
            objects.AddRange(roadGameObjects);

            Selection.objects = objects.ToArray();
            Selection.activeGameObject = prevActiveGameObject;
        }

        public static void SelectConnectedIntersections()
        {
            List<Intersection> selectedIntersections = new List<Intersection>();

            List<Intersection> allIntersections = UnityEngine.Object.FindObjectsOfType<Intersection>().ToList();
            List<Component> selectedRoads = GetSelectedRoads();

            foreach (Intersection intersection in allIntersections)
            {
                foreach( ConnectionPoint point in intersection.connectionPoints)
                {
                    if (point.road == null)
                        continue;

                    //if (point.front)
                    //    continue;

                    Road road = point.road;

                    if (selectedRoads.Contains(road))
                    {
                        selectedIntersections.Add(intersection);
                        break;
                    }

                }
            }

            List<GameObject> selectedGameObjects = new List<GameObject>();
            foreach (Intersection intersection in selectedIntersections)
            {
                selectedGameObjects.Add(intersection.gameObject);
            }

            Selection.objects = selectedGameObjects.ToArray();
        }

        public static void SelectConnectedRoads()
        {
            List<Component> intersections = GetSelectedIntersections();

            List<GameObject> roadGameObjects = new List<GameObject>();

            foreach (Intersection intersection in intersections)
            {
                List<Road> roads = intersection.GetRoads();
                foreach (Road road in roads)
                {
                    roadGameObjects.Add(road.gameObject);
                }
            }

            Selection.objects = roadGameObjects.ToArray();
        }

        public static List<Component> GetSelectedIntersections()
        {
            return GetSelectedComponents(new Type[] { typeof(Intersection) });
        }

        public static List<Component> GetSelectedRoads()
        {
            return GetSelectedComponents(new Type[] { typeof(Road) });
        }

        /// <summary>
        /// Get the selected intersections with their connected roads
        /// </summary>
        /// <returns></returns>
        public static List<Component> GetSelectedIntersectionsWithRoads()
        {
            List<Component> intersections = GetSelectedComponents(new Type[] { typeof(Intersection) });

            List<Component> components = new List<Component>();
            components.AddRange(intersections);

            foreach (Intersection intersection in intersections)
            {
                List<Road> roads = intersection.GetRoads();
                components.AddRange(roads);
            }

            return components;
        }
    }
}
#endif