#if __MICROVERSE_ROADS__

using JBooth.MicroVerseCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class AutoConnector
    {
        public enum Priority
        {
            Distance,
            FaceAngle
        }

        Priority priority = Priority.Distance;

        private RoadSystem roadSystem;
        private List<Intersection> intersections;
        private float angleLimit;

        public AutoConnector(RoadSystem roadSystem, List<Intersection> intersections, Priority priority, float angleLimit = 30f)
        {
            this.roadSystem = roadSystem;
            this.intersections = intersections;
            this.priority = priority;
            this.angleLimit = angleLimit;
        }

        /// <summary>
        /// Algorithm:
        /// * create list of all connectable points of intersections that don't have roads
        /// * calculate distance and how they face each other
        /// * remove those which aren't facing each other
        /// * sort by priority ascending
        ///   + distance: closer ones get a higher chance of being connected
        ///   + face angle: those which face each other more get higher chance of being connected
        /// * connect roads
        /// 
        /// Can be adapted / parameterized in the future, ie favor facing ones as apposed to using the distance
        /// </summary>
        public void AutoConnect()
        {
            List<Connectable> connectables = GetConnectables();

            // Debug.Log($"connectables: {connectables.Count}");

            // get only those which face each other
            connectables = connectables.Where(x => x.isFacingEachOther).ToList();

            // Debug.Log($"facing each other: {connectables.Count}");

            // sort by priority
            switch(priority)
            {
                case Priority.Distance:
                    connectables = connectables
                        .OrderBy(x => x.distance) // distance first
                        .ThenBy(y => y.angleA + y.angleB) // then angle
                        .ToList();
                    break;

                case Priority.FaceAngle:
                    connectables = connectables
                        .OrderBy(y => y.angleA + y.angleB) // angle first
                        .ThenBy(x => x.distance) // then distance
                        .ToList();
                    break;

            }

            // log
            /*
            foreach (Connectable connectable in connectables)
            {
                Debug.Log($"connectable: {connectable.intersectionA.name}.{connectable.pointA.connector.name} => {connectable.intersectionB.name}{connectable.pointB.connector.name}, distance {connectable.distance}");
            }
            */

            ///
            /// Remove connectables which intersect by comparing lines in 2d (Vector3.xz) space
            /// 
            // line-crossing removal not working fully => disabling the code for now, ie rather not have the feature at all
            /*
            List<Connectable> removables = new List<Connectable>();

            foreach (Connectable connectableA in connectables)
            {
                foreach (Connectable connectableB in connectables)
                {
                    // skip self
                    if (connectableA == connectableB)
                        continue;

                    // skip connectors which are on the same intersection
                    if (connectableA.intersectionA == connectableB.intersectionA
                        || connectableA.intersectionA == connectableB.intersectionB
                        || connectableA.intersectionB == connectableB.intersectionA
                        || connectableA.intersectionB == connectableB.intersectionB
                        )
                        continue;

                    // check line intersection
                    if (GeometryUtils.CheckLineIntersection2D_XZ(
                        connectableA.pointA.connector.transform.position,
                        connectableA.pointB.connector.transform.position,
                        connectableB.pointA.connector.transform.position,
                        connectableB.pointB.connector.transform.position
                        ))
                    {
                        removables.Add(connectableA);
                        removables.Add(connectableB);
                    }
                }
            }

            foreach (Connectable removable in removables)
            {
                connectables.Remove(removable);
            }
            */

            ///
            /// Connect
            /// 
            foreach (Connectable connectable in connectables)
            {
                IntersectionUtils.AutoConnect(roadSystem, connectable.intersectionA, connectable.intersectionB, connectable.pointA, connectable.pointB);
            }
            

        }

        /// <summary>
        /// Get all point <-> point options, ie potential candidates for connections
        /// </summary>
        /// <param name="roadSystem"></param>
        /// <param name="intersections"></param>
        /// <returns></returns>
        private List<Connectable> GetConnectables()
        {
            // list used to check whether either [A,B] or [B,A] is already in use; we need only 1 of the pairs
            List<Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint>> tuples = new List<Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint>>();

            List<Connectable> connectables = new List<Connectable>();

            foreach (Intersection iA in intersections)
            {
                foreach (Intersection iB in intersections)
                {
                    if (iA == iB)
                        continue;

                    foreach (Intersection.ConnectionPoint pA in iA.connectionPoints)
                    {
                        // only collect unconnected points
                        if (pA == null || pA.road != null)
                            continue;

                        foreach (Intersection.ConnectionPoint pB in iB.connectionPoints)
                        {
                            // only collect unconnected points
                            if (pB == null || pB.road != null)
                                continue;

                            // avoid duplicates
                            Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint> tuple1 = new Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint>(pA, pB);
                            Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint> tuple2 = new Tuple<Intersection.ConnectionPoint, Intersection.ConnectionPoint>(pB, pA);

                            if (tuples.Contains(tuple1) || tuples.Contains(tuple2))
                            {
                                continue;
                            }

                            tuples.Add(tuple1);
                            tuples.Add(tuple2);

                            // create connectable and register it
                            Connectable connectable = new Connectable(iA, iB, pA, pB, angleLimit);

                            connectables.Add(connectable);
                        }
                    }
                }

                // just a precaution to avoid an endless loop
                if(connectables.Count > 10000)
                {
                    Debug.LogError("Connectables threshold reached, aborting");
                    return connectables;
                }
            }

            return connectables;
        }

        private class Connectable
        {
            public Intersection intersectionA;
            public Intersection intersectionB;

            public Intersection.ConnectionPoint pointA;
            public Intersection.ConnectionPoint pointB;

            public bool isFacingEachOther;
            public float distance;

            private float angleLimit;

            public float angleA;
            public float angleB;

            public Connectable(Intersection intersectionA, Intersection intersectionB, Intersection.ConnectionPoint pointA, Intersection.ConnectionPoint pointB, float angleLimit)
            {
                this.intersectionA = intersectionA;
                this.intersectionB = intersectionB;
                this.pointA = pointA;
                this.pointB = pointB;
                this.angleLimit = angleLimit;

                CalculateDistance();
                CalculateFaceAngle();
            }

            public void CalculateDistance()
            {
                this.distance = Vector3.Distance(pointA.connector.transform.position, pointB.connector.transform.position);
            }

            public void CalculateFaceAngle()
            {
                Transform transformA = pointA.connector.transform;
                Transform transformB = pointB.connector.transform;

                isFacingEachOther = VectorUtils.IsFacingEachOther(transformA, transformB, out angleA, out angleB, angleLimit);
            }
        }
    }
}
#endif