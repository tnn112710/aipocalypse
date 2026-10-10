using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__
    // IDEAS: consider terrain bounds, point should never go outside bounds
    // IDEAS: reduce terrain bounds, so that point isn't at the edge
    // IDEAS: consider terrain geo, ie float3 height (flat gives longer lines)
    // IDEAS: more points in random direction

    public class SplineCreator
    {
        /// <summary>
        /// Create a spline with predefines positions
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="positions"></param>
        public static void CreateSpline(Spline spline, params Vector3[] positions)
        {
            spline.Clear();

            for( int i=0; i < positions.Length; i++)
            {
                BezierKnot knot = new BezierKnot(new float3(positions[i]));
                spline.Add(knot, TangentMode.AutoSmooth);
            }
        }


        public static void UnsubdivideSpline(Spline spline)
        {
            // ensure there is at least the specified number of knots left
            int minimumNodeCount = 2;

            if (spline.Count <= minimumNodeCount)
                return;

            // remove every 2nd knot
            // values are set to ensure first and last knot remain as they are
            for (var i = spline.Count - 2; i > 0; i -= 2)
            {
                spline.RemoveAt(i);

                if (spline.Count < minimumNodeCount)
                    break;

            }
        }

        /// <summary>
        /// Subdivide the entire spline
        /// </summary>
        /// <param name="spline"></param>
        public static void SubdivideSpline(Spline spline, Vector2 subdivisionRange, Vector2 deformationAngle)
        {
            // the start knot index
            int startKnotIndex = 0;

            // the number of spline points to process; using count instead of end index because we need to consider closing of the spline as well
            int count = spline.Closed ? spline.Count : spline.Count - 1;

            // perform subdivision
            SubdivideRange(spline, startKnotIndex, count, subdivisionRange, deformationAngle);
        }

        /// <summary>
        /// Subdivide the spline from start to end knot index
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="startKnotIndex">The start knot index</param>
        /// <param name="count">The number of knots to process, starting at the start knot index</param>
        public static void SubdivideRange(Spline spline, int startKnotIndex, int count, Vector2 subdivisionRange, Vector2 deformationAngle)
        {
            // list gets extended => we iterate backwards
            for( int i = count -1; i >= 0; i--)
            {
                // t is a random point on the segment
                float randomMin = subdivisionRange.x; // 0.5f;
                float randomMax = subdivisionRange.y; // 0.5f;
                float t = UnityEngine.Random.Range(randomMin, randomMax);

                BezierKnot lerpKnot = SubdivideAt(spline, startKnotIndex, i, t, deformationAngle);
            }
        }

        /// <summary>
        /// Subdivide the spline at the end knot index
        /// </summary>
        /// <param name="spline"></param>
        /// <param name="endKnotIndex"></param>
        /// <param name="t">Relative split position [0,1]</param>
        public static BezierKnot SubdivideAt(Spline spline, int startKnotIndex, int i, float t, Vector2 deformationAngle)
        {
            // calculate start knot index
            int segmentStartIndex = startKnotIndex + i;

            // calculate end knot index, consider overlap, ie the closing of the spline
            bool overlap = false;

            int segmentEndIndex = segmentStartIndex + 1;
            if (segmentEndIndex >= spline.Count)
            {
                overlap = true;
                segmentEndIndex = 0;
            }

            // get the knots
            BezierKnot startKnot = spline[segmentStartIndex];
            BezierKnot endKnot = spline[segmentEndIndex];

            // interpolate
            float3 startPosition = startKnot.Position;
            float3 endPosition = endKnot.Position;

            float3 lerpPosition = new float3(
                    Mathf.Lerp(startPosition.x, endPosition.x, t),
                    Mathf.Lerp(startPosition.y, endPosition.y, t),
                    Mathf.Lerp(startPosition.z, endPosition.z, t)
                    );

            // rotate lerp position around the start knot
            float angle = UnityEngine.Random.Range(deformationAngle.x, deformationAngle.y);
            lerpPosition = RotateAroundRandom(lerpPosition, startPosition, angle);
    
            // create lerp knot
            BezierKnot lerpKnot = new BezierKnot(lerpPosition);
            
            // insert/add the lerp knot depending on whether we are on the spline or in the closing range
            if (overlap)
            {
                spline.Add(lerpKnot, TangentMode.AutoSmooth);
            }
            else
            {
                spline.Insert(segmentEndIndex, lerpKnot, TangentMode.AutoSmooth);
            }

            return lerpKnot;
        }

        /// <summary>
        /// Rotate point around pivot by angle
        /// </summary>
        /// <param name="point"></param>
        /// <param name="pivot"></param>
        /// <param name="angle">Angle in degree</param>
        /// <returns></returns>
        private static float3 RotateAroundRandom( float3 point, float3 pivot, float angle)
        {
            float3 direction = point - pivot;
            direction = Quaternion.Euler(0, angle, 0) * direction;

            return direction + pivot;
        }
    }
#endif
}