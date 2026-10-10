#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using static JBooth.MicroVerseCore.Intersection;

namespace Rowlan.Genesis.Roads
{
    /// <summary>
    /// Provided an intersection and a road find the merge points
    /// </summary>
    public class RoadMerge
    {
        private bool debug = true;

        /// <summary>
        /// Structure holding data for the merge process
        /// </summary>
        struct MergePoint
        {
            /// <summary>
            /// The intersection's connection point
            /// </summary>
            public ConnectionPoint point;

            /// <summary>
            /// Distance used for finding nearest connection point
            /// </summary>
            public float distance;

            /// <summary>
            /// t on the spline
            /// </summary>
            public float t;
        }

        private RoadSystem roadSystem;
        private SplineContainer splineContainer;
        private Spline spline;
        private Intersection intersection;
        private Road road;


        private MergePoint mergePointA;
        private MergePoint mergePointB;

        public RoadMerge(Intersection intersection, Road road) 
        { 
            this.roadSystem = RoadSplineUtils.GetRoadSystem(road);
            this.intersection = intersection;
            this.road = road;

            this.splineContainer = road.splineContainer;
            this.spline = splineContainer.Spline;
        }

        /// <summary>
        /// Create a structure that contains the information about the connection points and their distance to the spline.
        /// The merge points are those with the closest distance.
        /// </summary>
        /// <returns>true if 2 merge points were found, false otherwise</returns>
        public bool Evaluate()
        {
            // find the merge connections on the intersection
            List<MergePoint> mergePoints = new List<MergePoint>();

            foreach (var connectionPoint in intersection.connectionPoints)
            {
                // only free ones supported
                if (connectionPoint.road != null)
                    continue;

                var pointWorldPos = connectionPoint.connector.transform.position;

                // find the merge point on the road
                float3 splineLocalPos = splineContainer.transform.worldToLocalMatrix.MultiplyPoint(pointWorldPos);
                SplineUtility.GetNearestPoint(spline, splineLocalPos, out var nearest, out float t);
                float3 splineWorldPos = splineContainer.transform.TransformPoint(nearest); // pos on spline

                float distance = Vector3.Distance(pointWorldPos, splineWorldPos);

                MergePoint mergePoint = new MergePoint
                {
                    point = connectionPoint,
                    distance = distance,
                    t = t
                };

                mergePoints.Add(mergePoint);

                if( debug)
                {
                    //Debug.Log($"connector {connectionPoint.connector.name} dist {distance}");
                    Debug.DrawLine(pointWorldPos, splineWorldPos, Color.red, 5f);
                }
            }

            mergePoints = mergePoints.OrderBy(x => x.distance).ToList();

            // at least 2 connection points
            if (mergePoints.Count < 2)
            {
                Debug.LogError($"At least 2 free connection points required on intersection");
                return false;
            }

            mergePointA = mergePoints[0];
            mergePointB = mergePoints[1];

            if (debug)
            {
                Debug.DrawRay(mergePointA.point.connector.transform.position, Vector3.up * 3, Color.green, 5f);
                Debug.DrawRay(mergePointB.point.connector.transform.position, Vector3.up * 3, Color.green, 5f);
            }

            return true;
        }

        public void Merge()
        {
            Undo.RegisterCompleteObjectUndo(splineContainer, "Road Merge");

            // get the connection points and intersections of the road
            ConnectionPoint roadConnectionPointA = IntersectionUtils.GetConnectionPoint(road, true);
            ConnectionPoint roadConnectionPointB = IntersectionUtils.GetConnectionPoint(road, false);
            Intersection intersectionA = IntersectionUtils.GetIntersection( roadConnectionPointA);
            Intersection intersectionB = IntersectionUtils.GetIntersection( roadConnectionPointB);

            Debug.Log($"{intersectionA} <-> {intersectionB}");

            /*
            float indexA = SplineUtility.ConvertIndexUnit(spline, mergePointA.t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            float indexB = SplineUtility.ConvertIndexUnit(spline, mergePointB.t, PathIndexUnit.Normalized, PathIndexUnit.Knot);

            Debug.Log($"index {indexA} <-> {indexB}");

            for (int i = 0; i < spline.Count; i++)
            {
                float t = SplineUtility.GetNormalizedInterpolation(spline, i, PathIndexUnit.Knot);
                float j = SplineUtility.ConvertIndexUnit(spline, t, PathIndexUnit.Normalized, PathIndexUnit.Knot);

                Debug.Log($"i {i} => t {t} => {j}");

            }
            */
            
            // remove the knots between the split indexes
            int indexA = RoadSplineUtils.AddKnotAt(spline, mergePointA.t);
            int indexB = RoadSplineUtils.AddKnotAt(spline, mergePointB.t);


            /*
            // disconnect
            roadConnectionPointA.road = null;
            roadConnectionPointB.road = null;

            // segments
            List<BezierKnot> knotsA = spline.Knots.ToList();
            List<BezierKnot> knotsB = spline.Knots.ToList();

            // remove spline data, set up new segments
            // RoadSplineUtils.ClearInBetween(spline, indexA, indexB);
            knotsA.RemoveRange(indexA, spline.Count - indexA);
            knotsB.RemoveRange(0, indexB);

            spline.Knots = knotsA;

            // reconnect A
            roadConnectionPointA.road = road;
            roadConnectionPointA.front = true;
            mergePointA.point.road = road;
            mergePointA.point.front = false;

            IntersectionUtils.UpdateIntersection(roadSystem, intersectionA);
            IntersectionUtils.UpdateIntersection(roadSystem, intersectionB);
            IntersectionUtils.UpdateIntersection(roadSystem, intersection);
            // TODO
            /*
            IntersectionUtils.UpdateIntersection(roadSystem, intersectionA);
            IntersectionUtils.UpdateIntersection(roadSystem, intersectionB);

            roadSystem.ClearCacheAndUpdateSystem();

            IntersectionUtils.ClearSpawnedRoads(roadSystem, intersectionA);
            IntersectionUtils.ClearSpawnedRoads(roadSystem, intersectionB);

            roadSystem.ClearCacheAndUpdateSystem();

            IntersectionUtils.UpdateIntersection(roadSystem, intersectionA);
            IntersectionUtils.UpdateIntersection(roadSystem, intersectionB);

            roadSystem.ClearCacheAndUpdateSystem();
            */
            //GameObject.DestroyImmediate(road.gameObject);



            /*

            IntersectionUtils.AutoConnect(roadSystem, intersectionA, intersection, roadConnectionPointA, mergePointA.point);
            IntersectionUtils.AutoConnect(roadSystem, intersection, intersectionB, mergePointB.point, roadConnectionPointB);
            */
        }

    }
}
#endif