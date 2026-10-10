#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Intersection;

namespace Rowlan.Genesis.Roads
{
    public static class RoadExtensions
    {
        /// <summary>
        /// Align road to terrain
        /// </summary>
        /// <param name="road"></param>
        public static void AlignToTerrain(this Road road)
        {
            MicroVerse.instance.AlignSplines(road.splineContainer);

            EditorUtility.SetDirty(road.splineContainer);
        }

        /// <summary>
        /// Align intersection to terrain
        /// </summary>
        /// <param name="intersection"></param>
        public static void AlignToTerrain(this Intersection intersection)
        {
            Vector3 worldPosition = intersection.transform.position;

            if (MicroVerse.instance.GetTerrainHeight(worldPosition, out float height))
            {
                intersection.transform.position = new Vector3(intersection.transform.position.x, height, intersection.transform.position.z);
            }
        }
        public static List<Road> GetRoads(this Intersection intersection)
        {
            List<Road> roads = new List<Road>();

            foreach (ConnectionPoint point in intersection.connectionPoints)
            {
                if (point.road == null)
                    continue;

                roads.Add(point.road);
            }

            return roads;
        }


    }
}
#endif