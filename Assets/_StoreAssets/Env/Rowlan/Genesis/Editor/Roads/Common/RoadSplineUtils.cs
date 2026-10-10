#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class RoadSplineUtils
    {
        #region Selection

        /// <summary>
        /// Get the first and last knots of the selection and their indexes on the spline
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="splineIndex"></param>
        /// <returns></returns>
        public static bool GetSelectedKnotsBounds(SplineContainer splineContainer, int splineIndex, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex)
        {
            Spline spline = splineContainer[splineIndex];

            return GetSelectedKnotsBounds(splineContainer, spline, out firstKnot, out lastKnot, out firstKnotIndex, out lastKnotIndex);

        }

        /// <summary>
        /// Get the first and last knots of the selection and their indexes on the spline
        /// 
        /// The returned values will be in the order of the spline knots. Closed Spline isn't supported.
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="splineIndex"></param>
        /// <returns></returns>
        public static bool GetSelectedKnotsBounds(SplineContainer splineContainer, Spline spline, out BezierKnot firstKnot, out BezierKnot lastKnot, out int firstKnotIndex, out int lastKnotIndex)
        {
            bool valid;

            int splineIndex = GetSplineIndex(splineContainer, spline);

            List<BezierKnot> selectedKnots = GetSelectedKnots(splineContainer, splineIndex);

            // all
            if( selectedKnots.Count == 0)
            {
                firstKnot = spline[0];
                lastKnot = spline[spline.Count - 1];

                valid = true;
            }
            // only 1 selected => unclear whether all or only selection
            else if( selectedKnots.Count == 1)
            {
                firstKnot = new BezierKnot();
                lastKnot = new BezierKnot();

                valid = false;
            }
            // selection of at least 2
            else
            {
                firstKnot = selectedKnots[0];
                lastKnot = selectedKnots[selectedKnots.Count - 1];

                valid = true;
            }

            // get the indexes of the knots or -1 if they can't be found (ie are the default)
            firstKnotIndex = spline.IndexOf(firstKnot);
            lastKnotIndex = spline.IndexOf(lastKnot);

            // ascending order: swap the knots and indexes if necessary
            if( firstKnotIndex > lastKnotIndex )
            {
                var tmpKnot = firstKnot;
                firstKnot = lastKnot;
                lastKnot = tmpKnot;

                var tmpIndex = firstKnotIndex;
                firstKnotIndex = lastKnotIndex;
                lastKnotIndex = tmpIndex;
            }

            return valid;
        }

        /// <summary>
        /// Get the selected knots of the spline with the specified index of the container
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="splineIndex"></param>
        /// <returns></returns>
        public static List<BezierKnot> GetSelectedKnots(SplineContainer splineContainer, int splineIndex = 0)
        {
            List<SplineInfo> splineInfos = new List<SplineInfo>
                {
                    new SplineInfo(splineContainer, splineIndex)
                };

            List<SelectableKnot> selectableKnots = new List<SelectableKnot>();

            SplineSelection.GetElements(splineInfos, selectableKnots);

            List<BezierKnot> bezierKnots = new List<BezierKnot>();

            for (int i = 0; i < selectableKnots.Count; i++)
            {
                SelectableKnot selectableKnot = selectableKnots[i];
                bezierKnots.Add(selectableKnot.GetBezierKnot(false));
            }

            return bezierKnots;
        }

        /// <summary>
        /// Select all knots from first index to last index.
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="spline"></param>
        /// <param name="firstIndex"></param>
        /// <param name="lastIndex"></param>
        public static void UpdateKnotSelection(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex )
        {
            SplineSelection.Clear();

            int splineIndex = GetSplineIndex(splineContainer, spline);

            for (int i = firstIndex; i <= lastIndex; i++)
            {
                SplineSelection.Add(new SelectableKnot(new SplineInfo(splineContainer, splineIndex), i));
            }
        }

        #endregion Selection

        /// <summary>
        /// Get the index of the spline int he container
        /// </summary>
        /// <param name="splineContainer"></param>
        /// <param name="spline"></param>
        /// <returns></returns>
        public static int GetSplineIndex( SplineContainer splineContainer, Spline spline)
        {
            return splineContainer.Splines.ToList().IndexOf(spline);
        }
        

        /// <summary>
        /// Remove all knots except the first and the last one
        /// </summary>
        /// <param name="spline"></param>
        public static void ClearInBetween( Spline spline, int firstIndex, int lastIndex)
        {
            for( int i = lastIndex - 1; i > firstIndex; i--)
            {
                spline.RemoveAt(i);
            }
        }

        /// <summary>
        /// Remove all knots after the specified index
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="removeIndex"></param>
        public static void ClearAfter(Spline spline, int removeIndex)
        {
            for (int i = spline.Count - 1; i > removeIndex; i--)
            {
                spline.RemoveAt(i);
            }
        }

        /// <summary>
        /// Remove all knots except the first and the last one and update the selection
        /// </summary>
        /// <param name="spline"></param>
        public static void ClearInBetweenSelection(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex)
        {
            int originalKnotCount = spline.Count;
                
            ClearInBetween( spline, firstIndex, lastIndex );

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);

        }

        /// <summary>
        /// Add a knot at normalized position t on the spline
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="t"></param>
        /// <returns>the index at which the knot was added</returns>
        public static int AddKnotAt( Spline spline, float t)
        {
            if( CreateKnot(spline, t, out BezierKnot knot))
            {
                int insertIndex = GetInsertIndexAt( spline, t);

                if (insertIndex >= 0 && insertIndex < spline.Count)
                {
                    spline.Insert(insertIndex, knot);
                }
                else
                {
                    Debug.LogError($"Index out of bounds: {insertIndex}");
                }

            }

            return -1;
        }

        /// <summary>
        /// Get the list index of a bezierknot that could be inserted at normalized position t
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static int GetInsertIndexAt( Spline spline, float t)
        {
            float index = SplineUtility.ConvertIndexUnit(spline, t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            int insertIndex = (int)Mathf.Ceil(index);
            return insertIndex;
        }

        /// <summary>
        /// Create a knot at normalized position t
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="t"></param>
        /// <param name="knot"></param>
        /// <returns></returns>
        public static bool CreateKnot( Spline spline, float t, out BezierKnot knot)
        {
            bool valid = false;

            if (SplineUtility.Evaluate(spline, t, out float3 position, out float3 tangent, out float3 up))
            {
                tangent = math.normalizesafe(tangent);
                up = math.normalizesafe(up);
                quaternion quaternion = quaternion.LookRotation(tangent, up);

                knot = new BezierKnot(position, tangent, tangent, quaternion);

                valid = true;
            }
            else
            {
                knot = new BezierKnot();
            }

            return valid;
        }

        /// <summary>
        /// Get the road system of the road
        /// </summary>
        /// <param name="road"></param>
        /// <returns></returns>
        public static RoadSystem GetRoadSystem( Road road)
        {
            return road?.GetComponentInParent<RoadSystem>();
        }

        /// <summary>
        /// Get the road system of the intersection
        /// </summary>
        /// <param name="road"></param>
        /// <returns></returns>
        public static RoadSystem GetRoadSystem(Intersection intersection)
        {
            return intersection?.GetComponentInParent<RoadSystem>();
        }


        public static void SubdivideEqualDistribution(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex)
        {
            int originalKnotCount = spline.Count;

            for (int i = lastIndex; i > firstIndex; i--)
            {
                BezierKnot knotA = spline[i-1];
                BezierKnot knotB = spline[i];

                SplineUtility.GetNearestPoint(spline, knotA.Position, out float3 nearestA, out float tA);
                SplineUtility.GetNearestPoint(spline, knotB.Position, out float3 nearestB, out float tB);

                float tCenter = tA + (tB - tA) / 2f;

                if( SplineUtility.Evaluate(spline, tCenter, out float3 position, out float3 tangent, out float3 up))
                {
                    tangent = math.normalizesafe(tangent);
                    up = math.normalizesafe(up);
                    quaternion quaternion = quaternion.LookRotation(tangent, up);

                    int insertIndex = i;
                    spline.Insert(insertIndex, new BezierKnot(position, tangent, tangent, quaternion), TangentMode.AutoSmooth);
                }

            }

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection( splineContainer, spline, firstSelectionIndex, lastSelectionIndex );
        }

        private static void CreateInbetweenKnots(SplineContainer splineContainer, Spline spline, float tA, float tB, int segments, out List<BezierKnot> knots)
        {
            float tDelta = (tB - tA) / segments;

            // create list of t between first and last index
            List<float> tList = new();

            // start with 1 because first to last with nothing inbetween counts also as segment
            for (int i = 1; i < segments; i++)
            {
                float tNew = tA + tDelta * i;

                tList.Add(tNew);
            }

            knots = new();

            foreach (float t in tList)
            {
                if (SplineUtility.Evaluate(spline, t, out float3 position, out float3 tangent, out float3 up))
                {
                    tangent = math.normalizesafe(tangent);
                    up = math.normalizesafe(up);
                    quaternion quaternion = quaternion.LookRotation(tangent, up);

                    knots.Add(new BezierKnot(position, tangent, tangent, quaternion));
                }
            }
        }
        public static void UnsubdivideEqualDistribution(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex)
        {
            int originalKnotCount = spline.Count;

            BezierKnot firstKnot = spline[firstIndex];
            BezierKnot lastKnot = spline[lastIndex];

            SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestA, out float tA);
            SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestB, out float tB);

            // calculate segment count and new segment length to go along the spline from first to last t
            int segments = (lastIndex - firstIndex - 2) / 2 + 1;

            // create list of knots with t between first and last index
            CreateInbetweenKnots(splineContainer, spline, tA, tB, segments, out List<BezierKnot> knots);

            // first create the inbetween knots for having the approximate positions that are already set; only after that clear
            // remove all except first and last
            ClearInBetween(spline, firstIndex, lastIndex);

            // add new knots
            for( int i=0; i < knots.Count; i++)
            {
                int insertIndex = firstIndex + i + 1;

                spline.Insert(insertIndex, knots[i], TangentMode.AutoSmooth);

            }

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);
        }


        /// <summary>
        /// Remove the roll out of a spline, ie set rotation to 0
        /// </summary>
        /// <param name="spline"></param>
        public static void Unroll(Spline spline, int firstKnotIndex, int lastKnotIndex)
        {
            for (int i = firstKnotIndex; i <= lastKnotIndex; i++)
            {
                BezierKnot knot = spline[i];

                Quaternion rotation = knot.Rotation;

                Vector3 eulerAngles = rotation.eulerAngles;

                rotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, 0f);

                knot.Rotation = rotation;

                spline[i] = knot;
            }

        }

        /// <summary>
        /// Create a looping
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="spline"></param>
        /// <param name="roadPercentage">Percentage of the road which the looping occupies. Center is 0.5. From that the offset is calculated. Usually 0.25 in both directions from center</param>
        /// <param name="height"></param>
        /// <param name="tangentSize"></param>
        /// <param name="separation"></param>
        public static void Looping(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex, float roadPercentage, float height, float tangentSize, float separation)
        {
            int originalKnotCount = spline.Count;

            BezierKnot firstKnot = spline[firstIndex];
            BezierKnot lastKnot = spline[lastIndex];

            // remove all except first and last
            ClearInBetween( spline, firstIndex, lastIndex);

            SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestA, out float tA);
            SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestB, out float tB);

            // update first and last with the road percentage offset
            float tTotalDistance = tB - tA;
            float tOffset = tTotalDistance * (1f - roadPercentage / 100f) / 2f;

            tA = tA + tOffset;
            tB = tB - tOffset;

            // twist inbetween
            int segments = 5 + 1; // start -> outer -> top -> inner -> end ... +1 is because of first and last knot segment

            // create segment knots
            CreateInbetweenKnots(splineContainer, spline, tA, tB, segments, out List<BezierKnot> knots);

            #region Logic
            
            float3 outerPosition = SplineUtility.EvaluatePosition(spline, tA + (tB - tA) * 1.00f); // shift looping start past center
            float3 innerPosition = SplineUtility.EvaluatePosition(spline, tB - (tB - tA) * 1.00f); // shift looping end before center

            // outer
            BezierKnot outer = knots[1];
            
            outer.Position.x = outerPosition.x; // shift forward
            outer.Position.y += height / 2f; // half height
            outer.Position.z = outerPosition.z; // shift forward

            knots[1] = outer;

            // top
            BezierKnot top = knots[2];
            top.Position.y += height; // full height
            knots[2] = top;

            // inner
            BezierKnot inner = knots[3];
            inner.Position.x = innerPosition.x; // shift backward
            inner.Position.y += height / 2f; // half height
            inner.Position.z = innerPosition.z; // shift backward
            knots[3] = inner;

            // separation
            Vector3 direction = (lastKnot.Position - firstKnot.Position);

            Vector3 left = Vector3.Cross(direction, Vector3.up).normalized; // get vector for left/right separation

            knots[0] = knots[0] + left * separation;
            knots[1] = knots[1] + left * 2f * separation;
            knots[2] = knots[2] + left * 0f * separation;
            knots[3] = knots[3] + left * -1f * separation;
            knots[4] = knots[4] + left * -2f * separation;

            // p3 angle is angle from p1 to p3
            Transform transform = splineContainer.transform;

            Vector3 localDistance = knots[3].Position - knots[1].Position;
            float p4p2Angle = Quaternion.LookRotation(localDistance.normalized, transform.forward).eulerAngles.y;

            Vector3 directionNormalized = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles;
            float directionAngle = directionNormalized.y;

            // rotation
            Matrix4x4 matrix = transform.worldToLocalMatrix;

            Quaternion r0 = Quaternion.Euler(0, directionAngle, 0);
            Quaternion r1 = Quaternion.Euler(matrix.MultiplyVector(new Vector3(-90, directionAngle, 0)));
            Quaternion r2 = Quaternion.Euler(matrix.MultiplyVector(new Vector3(0, p4p2Angle, 180))); // angle from p2 to p4
            Quaternion r3 = Quaternion.Euler(matrix.MultiplyVector(new Vector3(90, directionAngle, 0)));
            Quaternion r4 = Quaternion.Euler(0, directionAngle, 0);

            // tangent
            Vector3 tangent = new Vector3(0f, 0f, tangentSize);

            knots[0] = new BezierKnot(knots[0].Position, -tangent, tangent, r0);
            knots[1] = new BezierKnot(knots[1].Position, -tangent, tangent, r1);
            knots[2] = new BezierKnot(knots[2].Position, -tangent, tangent, r2);
            knots[3] = new BezierKnot(knots[3].Position, -tangent, tangent, r3);
            knots[4] = new BezierKnot(knots[4].Position, -tangent, tangent, r4);
            
            #endregion Logic

            // add new knots
            for (int i = 0; i < knots.Count; i++)
            {
                int insertIndex = firstIndex + i + 1;

                spline.Insert(insertIndex, knots[i], TangentMode.Continuous);

            }

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);
        }

        private static float GetUnitCircleTangentLength()
        {
            // https://mechanicalexpressions.com/explore/geometric-modeling/circle-spline-approximation.pdf
            return (4f * (Mathf.Sqrt(2f) - 1f)) / 3f;
        }

        public static void Knot(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex, float roadPercentage = 60f, float heightOffset = 0f, float separation = 40f, float tangentSize = 40f)
        {
            BezierKnot firstKnot = spline[firstIndex];
            BezierKnot lastKnot = spline[lastIndex];

            // remove all except first and last
            ClearInBetween(spline, firstIndex, lastIndex);

            SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestA, out float tA);
            SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestB, out float tB);

            // update first and last with the road percentage offset
            float tTotalDistance = tB - tA;
            float tOffset = tTotalDistance * (1f - roadPercentage / 100f) / 2f;

            tA = tA + tOffset;
            tB = tB - tOffset;

            float tDistance = tB - tA;

            int segmentCount = 4; // [t0,t1], [t1,t2], [t2,t3], [t3,t4]

            float t0 = tA;
            float t1 = tA + tDistance / segmentCount * 1f; // 25%
            float t2 = tA + tDistance / segmentCount * 2f; // 50%
            float t3 = tA + tDistance / segmentCount * 3f; // 75%
            float t4 = tB;

            SplineUtility.Evaluate(spline, t1, out float3 position1, out float3 tangent1, out float3 up1);
            SplineUtility.Evaluate(spline, t2, out float3 position2, out float3 tangent2, out float3 up2);
            SplineUtility.Evaluate(spline, t3, out float3 position3, out float3 tangent3, out float3 up3);

            position1 = position1 + new float3(1, 0, 0) * separation;
            position2 = position2 + new float3(0, 1, 0) * separation;
            position3 = position3 + new float3(0, 0, 1) * separation;

            position1.y += heightOffset;
            position2.y += heightOffset;
            position3.y += heightOffset;

            Quaternion r1 = Quaternion.Euler(0f, 0f, 0f);
            Quaternion r2 = Quaternion.Euler(90f * 2, 0f, 0f);
            Quaternion r3 = Quaternion.Euler(90f * 3, 0f, 0f);
            
            Vector3 tangentOut = new Vector3(0f, 0f, tangentSize);

            BezierKnot k1 = new BezierKnot(position1, 0f, tangentOut, r1);
            BezierKnot k2 = new BezierKnot(position2, 0f, tangentOut, r2);
            BezierKnot k3 = new BezierKnot(position3, 0f, tangentOut, r3);

            // insert knots inbetween, ie indexes 1,2,3 ... 0 and 4 (1 initially) are given
            spline.Insert(firstIndex + 1, k1, TangentMode.Mirrored);
            spline.Insert(firstIndex + 2, k2, TangentMode.Mirrored);
            spline.Insert(firstIndex + 3, k3, TangentMode.Mirrored);

            // update selection
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = firstIndex + segmentCount;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);
        }

        public static void Twist(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex, float roadPercentage = 100f, int resolutionDegrees = 20)
        {
            int originalKnotCount = spline.Count;

            BezierKnot firstKnot = spline[firstIndex];
            BezierKnot lastKnot = spline[lastIndex];

            // remove all except first and last
            ClearInBetween(spline, firstIndex, lastIndex);

            SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestA, out float tA);
            SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestB, out float tB);

            // update first and last with the road percentage offset
            float tTotalDistance = tB - tA;
            float tOffset = tTotalDistance * (1f - roadPercentage / 100f) / 2f;

            tA = tA + tOffset;
            tB = tB - tOffset;

            // twist inbetween
            int segments = 360 / resolutionDegrees;

            // create segment knots
            CreateInbetweenKnots(splineContainer, spline, tA, tB, segments, out List<BezierKnot> knots);

            #region Logic

            Quaternion rotationStart = firstKnot.Rotation;
            Quaternion rotationEnd = lastKnot.Rotation;

            for ( int i=0; i < knots.Count; i++)
            {
                BezierKnot knot = knots[i];

                knot.Rotation = Quaternion.Euler(0f, 0f, resolutionDegrees * (i + 1)); // eg 90, 180, 270; i+1 ... first inbetween knot should already be rotated

                knot.TangentIn = 0f;
                knot.TangentOut = 0f;

                knots[i] = knot;
            }

            #endregion Logic

            // add new knots
            for (int i = 0; i < knots.Count; i++)
            {
                int insertIndex = firstIndex + i + 1;

                spline.Insert(insertIndex, knots[i], TangentMode.AutoSmooth);

            }

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);
        }

        public static void Hills(SplineContainer splineContainer, Spline spline, int firstIndex, int lastIndex, float roadPercentage = 100f, int hillCount = 1, float heightOffset = 10f)
        {
            int originalKnotCount = spline.Count;

            BezierKnot firstKnot = spline[firstIndex];
            BezierKnot lastKnot = spline[lastIndex];

            // remove all except first and last
            ClearInBetween(spline, firstIndex, lastIndex);

            SplineUtility.GetNearestPoint(spline, firstKnot.Position, out float3 nearestA, out float tA);
            SplineUtility.GetNearestPoint(spline, lastKnot.Position, out float3 nearestB, out float tB);

            // update first and last with the road percentage offset
            float tTotalDistance = tB - tA;
            float tOffset = tTotalDistance * (1f - roadPercentage / 100f) / 2f;

            tA = tA + tOffset;
            tB = tB - tOffset;

            // twist inbetween
            int segments = hillCount * 2;

            // create segment knots
            CreateInbetweenKnots(splineContainer, spline, tA, tB, segments, out List<BezierKnot> knots);

            #region Logic

            Quaternion rotationStart = firstKnot.Rotation;
            Quaternion rotationEnd = lastKnot.Rotation;

            for (int i = 0; i < knots.Count; i+=2)
            {
                BezierKnot knot = knots[i];

                knot.Position.y += heightOffset;

                knots[i] = knot;
            }

            #endregion Logic

            // add new knots
            for (int i = 0; i < knots.Count; i++)
            {
                int insertIndex = firstIndex + i + 1;

                spline.Insert(insertIndex, knots[i], TangentMode.AutoSmooth);

            }

            // update selection
            int addedKnots = (spline.Count - originalKnotCount);
            int firstSelectionIndex = firstIndex;
            int lastSelectionIndex = lastIndex + addedKnots;

            UpdateKnotSelection(splineContainer, spline, firstSelectionIndex, lastSelectionIndex);
        }
    }
}
#endif