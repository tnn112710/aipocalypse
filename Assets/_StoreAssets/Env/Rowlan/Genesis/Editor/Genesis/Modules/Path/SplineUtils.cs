using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
using UnityEditor.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__
    public class SplineUtils
    {
        public static Vector3 GetSplineCenter(Spline spline)
        {
            float3[] worldPositions = spline.Knots.Select(x => x.Position).ToArray();
            return GetMeanVector(worldPositions);
        }

        /// <summary>
        /// Get the mean vector of the specified polygon
        /// </summary>
        /// <param name="positions"></param>
        /// <returns></returns>
        public static Vector3 GetMeanVector(float3[] positions)
        {
            if (positions.Length == 0)
                return Vector3.zero;

            float x = 0f;
            float y = 0f;
            float z = 0f;
            foreach (Vector3 pos in positions)
            {
                x += pos.x;
                y += pos.y;
                z += pos.z;
            }
            return new Vector3(x / positions.Length, y / positions.Length, z / positions.Length);
        }

        public static void GrowSpline(SplineContainer splineContainer, float percent)
        {
            ResizeSpline(splineContainer, Mathf.Abs(percent));
        }

        public static void ShrinkSpline(SplineContainer splineContainer, float percent)
        {
            ResizeSpline(splineContainer, -Mathf.Abs(percent));
        }

        /// <summary>
        /// Resize the spline
        /// </summary>
        /// <param name="factor">Positive values for growing, negative for shrinking. 0.1 means grow by 10%, -0.1 means shrink by 10%</param>
        public static void ResizeSpline(SplineContainer splineContainer, float percent)
        {
            if (!splineContainer)
                return;

            Spline spline = splineContainer.Spline;

            float3 center = SplineUtils.GetSplineCenter(spline);

            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                Vector3 distance = knot.Position - center;

                knot.Position += new float3(distance * percent);

                spline[i] = knot;
            }
        }

        /// <summary>
        /// Move the transform handle into the center of the spline
        /// </summary>
        /// <param name="splineContainer"></param>
        public static void CenterMainHandle(SplineContainer splineContainer)
        {
            if (!splineContainer)
                return;

            Spline spline = splineContainer.Spline;
            Matrix4x4 worldMatrix = splineContainer.transform.localToWorldMatrix;

            float3 splineCenter = GetSplineCenter(spline);
            Vector3 localCenter = new Vector3(splineCenter.x, splineCenter.y, splineCenter.z);
            Vector3 worldCenter = worldMatrix.MultiplyPoint(localCenter);

            SplineUtility.SetPivot(splineContainer, worldCenter);
        }

        /// <summary>
        /// Reduce spline points using Unity's internal utility (came somewhat later in 2.x)
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="reductionEpsilon">The maximum distance from the reduced poly line shape for a point to be discarded</param>
        /// <param name="tension">Tension affects how curvy splines are at the knots. 0 is a sharp corner, 1 is maximum curvature</param>
        /// <param name="log"></param>
        public static void ReduceSplinePoints(Spline spline, float reductionEpsilon, float tension, bool log=false)
        {
            if (spline == null)
                return;

            // convert spline knots to positions
            List<float3> points = spline.Knots.Select(p => p.Position).ToList();

            // reduce number of sample points
            List<float3> reduced = SplineUtility.ReducePoints(points, reductionEpsilon);

            // create new knots using reduced points; tangent and rotation will be created in next step (auto smooth)
            spline.Knots = reduced.Select(p => new BezierKnot(p));

            // get all knots as spline range
            SplineRange splineRange = new SplineRange(0, spline.Count);

            // set auto smooth to spline range
            spline.SetTangentMode(splineRange, TangentMode.AutoSmooth);

            // set tension
            spline.SetAutoSmoothTension(splineRange, tension);

            // optional logging
            if (log)
            {
                Debug.Log($"Input Sample Count: {points.Count}\nSpline Knot Count: {reduced.Count}");
            }
        }

        public static void ApplyDouglasPeucker(Spline spline, float tolerance)
        {
            if (tolerance == 0)
                return;

            float2[] float2List = spline.Knots.Select(item => new float2(item.Position.x, item.Position.z)).ToArray();

            float2List = DouglasPeuckerReduction(float2List, tolerance);

            if (float2List.Length >= 2)
            {
                float3[] positions = float2List.Select(item => new float3(item.x, 0, item.y)).ToArray();
                BezierKnot[] knots = positions.Select(item => new BezierKnot(item)).ToArray();
                
                spline.Clear();

                foreach (BezierKnot knot in knots)
                {
                    spline.Add(knot, TangentMode.AutoSmooth);
                }
            }

        }

        #region douglas peucker
        // credit for the c# implementation to Craig Selbert
        public static float2[] DouglasPeuckerReduction(float2[] pointList, float tolerance)
        {
            if (pointList == null || pointList.Length < 3)
                return pointList;

            int firstPoint = 0;
            int lastPoint = pointList.Length - 1;

            List<int> pointIndexsToKeep = new List<int> { firstPoint, lastPoint };

            while (pointList[firstPoint].Equals(pointList[lastPoint]))
            {
                lastPoint--;
            }

            DouglasPeuckerReduction(pointList, firstPoint, lastPoint, tolerance, ref pointIndexsToKeep);

            pointIndexsToKeep.Sort();

            return pointIndexsToKeep.Select(index => pointList[index]).ToArray();
        }

        private static void DouglasPeuckerReduction(float2[] points, int firstPoint, int lastPoint, float tolerance,
            ref List<int> pointIndexsToKeep)
        {
            float maxDistance = 0;
            int indexFarthest = 0;

            for (int index = firstPoint; index < lastPoint; index++)
            {
                float distance = PerpendicularDistance(points[firstPoint], points[lastPoint], points[index]);

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = index;
                }
            }

            if (!(maxDistance > tolerance) || indexFarthest == 0)
                return;

            pointIndexsToKeep.Add(indexFarthest);

            DouglasPeuckerReduction(points, firstPoint, indexFarthest, tolerance, ref pointIndexsToKeep);
            DouglasPeuckerReduction(points, indexFarthest, lastPoint, tolerance, ref pointIndexsToKeep);
        }

        public static float PerpendicularDistance(float2 p1, float2 p2, float2 p)
        {
            float area = Mathf.Abs(.5f * (p1.x * p2.y + p2.x *
                                          p.y + p.x * p1.y - p2.x * p1.y - p.x *
                                          p2.y - p1.x * p.y));

            float bottom = Mathf.Sqrt(Mathf.Pow(p1.x - p2.x, 2) +
                                      Mathf.Pow(p1.y - p2.y, 2));

            float height = area / bottom * 2;

            return height;
        }

        #endregion douglas peucker

        #region convex hull

        public static void ApplyConvexHull(Spline spline)
        {

            List<float2> float2List = spline.Knots.Select(item => new float2(item.Position.x, item.Position.z)).ToList();

            float2List = GetConvexHull(float2List);

            if (float2List.Count >= 2)
            {
                float3[] positions = float2List.Select(item => new float3(item.x, 0, item.y)).ToArray();
                BezierKnot[] knots = positions.Select(item => new BezierKnot(item)).ToArray();

                spline.Clear();

                foreach (BezierKnot knot in knots)
                {
                    spline.Add(knot, TangentMode.AutoSmooth);
                }
            }

        }

        public static List<float2> GetConvexHull(List<float2> points)
        {
            if (points == null)
                return null;

            if (points.Count <= 1)
                return points;

            int n = points.Count;
            int k = 0;
            List<float2> h = new List<float2>(new float2[2 * n]);

            points.Sort((a, b) => a.x.Equals(b.x) ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && Cross(h[k - 2], h[k - 1], points[i]) <= 0)
                    k--;

                h[k++] = points[i];
            }

            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(h[k - 2], h[k - 1], points[i]) <= 0)
                    k--;

                h[k++] = points[i];
            }

            return h.Take(k - 1).ToList();
        }

        public static double Cross(float2 o, float2 a, float2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }
        #endregion convex hull

        /// <summary>
        /// Get the nearest point on a spline and the data that come with it
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="position"></param>
        /// <param name="tangent"></param>
        /// <param name="normal"></param>
        /// <param name="nearestContainer"></param>
        /// <returns></returns>
        public static bool GetNearestPoint(Transform transform, out float3 position, out float3 tangent, out float3 normal, out SplineContainer nearestContainer)
        {
            position = float3.zero;
            tangent = float3.zero;
            normal = float3.zero;
            nearestContainer = null;

            if (transform == null)
                return false;

            SplineContainer[] containers = GameObject.FindObjectsOfType<SplineContainer>();

            if (containers == null)
                return false;

            Vector3 sourcePosition = transform.position;
            float4 nearestPosition = new float4(0, 0, 0, float.PositiveInfinity);

            float t = 0;
            bool found = false;
            float distance = 0f;
            float nearestT = 0;

            foreach (SplineContainer container in containers)
            {
                // consider only active spline containers
                if (!container.transform.gameObject.activeInHierarchy)
                    continue;

                using var native = new NativeSpline(container.Spline, container.transform.localToWorldMatrix);

                // get distance and evaluate position and t
                distance = SplineUtility.GetNearestPoint(native, sourcePosition, out position, out t);

                if (distance < nearestPosition.w)
                {
                    nearestContainer = container;
                    nearestT = t;
                    nearestPosition = new float4(position, distance);

                    found = true;
                }
            }

            if (found)
            {
                // get tangent and normal at that position
                nearestContainer.Evaluate(nearestT, out position, out tangent, out normal);
            }

            return found;
        }

        /// <summary>
        /// Get the bounds in world space for a spline area
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="splineArea"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool GetSplineAreaBounds(Transform transform, JBooth.MicroVerseCore.SplineArea splineArea, out Bounds worldBounds)
        {
            SplineContainer splineContainer = splineArea.spline;
            Spline spline = splineContainer?.Spline;

            if (spline == null)
            {
                worldBounds = new Bounds();
                return false;
            }

            Bounds splineBounds = spline.GetBounds();
            Vector3 worldPos = splineContainer.transform.position + splineBounds.center;

            worldBounds = new Bounds(worldPos, splineBounds.size);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="container"></param>
        /// <param name="autoSmooth"></param>
        /// <param name="splineTension">Tension affects how curvy splines are at knots. 0 is a sharp corner, 1 is maximum curvitude.</param>
        /// <param name="index"></param>
        public static void ApplyToSpline( List<Vector3> positions, SplineContainer container, int index = 0, bool autoSmooth = true, float splineTension = 0.25f)
        {
            if (positions == null || positions.Count < 2)
                return;

            Spline spline = container[index];

            Undo.RecordObject(container, "Apply to Spline");

            spline.Clear();
            foreach (var position in positions)
            {
                spline.Add(new BezierKnot(position));
            }

            // auto smooth, spline tension
            {
                SplineRange splineRange = new SplineRange(0, spline.Count);

                // Sets the tangent mode for all knots in the spline to "Auto Smooth."
                spline.SetTangentMode(splineRange, TangentMode.AutoSmooth);

                // Sets the tension parameter for all knots. Note that the "Tension" parameter is only applicable to
                // "Auto Smooth" mode knots.
                spline.SetAutoSmoothTension(splineRange, splineTension);
            }

            container[index].Knots = spline;

            EditorUtility.SetDirty(container);

        }
    }
#endif
}