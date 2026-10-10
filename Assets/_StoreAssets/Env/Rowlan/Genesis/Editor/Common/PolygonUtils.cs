using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class PolygonUtils
    {
        /// <summary>
        /// Get a random point within the bounds on the X/Z plane
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Vector2 GetRandomPointXZ(Bounds bounds)
        {
            float xmin = bounds.center.x - bounds.extents.x;
            float xmax = bounds.center.x + bounds.extents.x;

            float zmin = bounds.center.z - bounds.extents.z;
            float zmax = bounds.center.z + bounds.extents.z;

            return new Vector2(UnityEngine.Random.Range(xmin, xmax), UnityEngine.Random.Range(zmin, zmax));
        }

        /// <summary>
        /// Get a random point within the bounds on the X/Z plane with a scale applied. The final positions will be relative to the bounds center
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Vector2 GetRandomPointXZ(Bounds bounds, float scale)
        {
            float xmin = bounds.center.x - bounds.extents.x * scale;
            float xmax = bounds.center.x + bounds.extents.x * scale;

            float zmin = bounds.center.z - bounds.extents.z * scale;
            float zmax = bounds.center.z + bounds.extents.z * scale;

            return new Vector2(UnityEngine.Random.Range(xmin, xmax), UnityEngine.Random.Range(zmin, zmax));
        }

        /// <summary>
        /// Subdivide segments longer than maxSegmentLength, up to maxSubdivisions passes.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="maxSegmentLength">Max length until a new point is added</param>
        /// <param name="maxSubdivisions">Number of subdivision passes</param>
        /// <returns></returns>
        public static List<Vector3> SubdividePoints(List<Vector3> points, float maxSegmentLength, int maxSubdivisions)
        {
            var result = new List<Vector3>(points);
            for (int pass = 0; pass < maxSubdivisions; pass++)
            {
                var next = new List<Vector3>();

                int count = result.Count;

                for (int i = 0; i < count; i++)
                {
                    var a = result[i];
                    var b = result[(i + 1) % count];

                    next.Add(a);

                    float distance = Vector3.Distance(a, b);
                    if (distance > maxSegmentLength)
                    {
                        int segments = Mathf.CeilToInt(distance / maxSegmentLength);

                        for (int segment = 1; segment < segments; segment++)
                        {
                            next.Add(Vector3.Lerp(a, b, segment / (float)segments));
                        }
                    }
                }
                result = next;
            }
            return result;
        }

        /// <summary>
        /// Chaikin's corner-cutting smoothing algorithm. Remove peaks.
        /// Note: Adds points for smoothing with each iterations, so keep the iteration count low
        /// </summary>
        /// <param name="points"></param>
        /// <param name="iterations">Number of iterations, limit maximum to 5 for performance reasons</param>
        /// <returns></returns>
        public static List<Vector3> ApplyChaikinSmoothing(List<Vector3> points, int iterations, int maxIterations = 5)
        {
            // limit the number of smoothing iterations to prevent excessive subdivision
            if (iterations > maxIterations)
                iterations = maxIterations;

            var result = new List<Vector3>(points);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var next = new List<Vector3>();
                int count = result.Count;

                for (int i = 0; i < count; i++)
                {
                    // current and next point, wrap around to the first point at the end
                    Vector3 p0 = result[i];
                    Vector3 p1 = result[(i + 1) % count];

                    // cut 1/4 of the corner: 25% along the segment from p0 -> p1
                    next.Add(Vector3.Lerp(p0, p1, 0.25f));

                    // cut 3/4 of the corner: 75% along the segment from p0 -> p1
                    next.Add(Vector3.Lerp(p0, p1, 0.75f));
                }

                result = next;
            }

            return result;
        }

        /// <summary>
        /// Evenly sample the input list of points down to <paramref name="targetCount"/> elements, wrap around if necessary.
        /// Picks indices via floor-based stepping over the normalized parameter space [0, 1)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="targetCount">Number of points in the output list</param>
        /// <returns></returns>
        public static List<Vector3> LimitPoints(List<Vector3> points, int targetCount)
        {
            // early out, avoid division by zero
            if (points.Count == 0)
                return points;

            var result = new List<Vector3>(targetCount);

            int count = points.Count;

            for (int i = 0; i < targetCount; i++)
            {
                // Compute a normalized parameter in [0, 1) for this sample.
                // i/targetCount ranges from 0 up to but not including 1.
                float t = i / (float)targetCount;

                // Map t to the original index range [0, count), then floor to get an integer index.
                // The modulo ensures wrap-around if t*count ever reaches count.
                int index = Mathf.FloorToInt(t * count) % count;

                result.Add(points[index]);
            }

            return result;
        }

        /// <summary>
        /// Applies random lateral offsets to each point along the spline, moving them
        /// left or right relative to the local tangent direction.
        /// </summary>
        /// <param name="points">Input list of 3D points forming a closed loop (first and last wrap).</param>
        /// <param name="amplitude">Maximum magnitude of the offset perpendicular to the tangent.</param>
        /// <param name="seed">Seed for the pseudo-random number generator, for reproducible noise.</param>
        /// <returns>
        /// A new list of points where each original point has been shifted along the
        /// perpendicular direction by a random amount in [-amplitude, +amplitude].
        /// </returns>
        public static List<Vector3> ApplyTangentNoise(List<Vector3> points, float amplitude, int seed)
        {
            // seed for repeatability
            var randomRange = new System.Random(seed);

            // prepare the output list with the same capacity as the input
            var result = new List<Vector3>(points.Count);

            int count = points.Count;
            for (int i = 0; i < count; i++)
            {
                var point = points[i];

                // previous and next points, wrapping via modulo for a closed loop
                var prev = points[(i - 1 + count) % count];
                var next = points[(i + 1) % count];

                // compute local tangent vector (direction of the polygon/spline)
                var tangent = (next - prev).normalized;

                // compute a perpendicular normal vector in the XZ plane
                // by rotating the tangent 90 degree around the Y axis
                var normal = new Vector3(-tangent.z, 0f, tangent.x);

                // randomly choose direction: -1 or +1
                float sign = randomRange.Next(0, 2) == 0 ? -1f : 1f;

                // random offset magnitude in [0, amplitude], then apply sign
                float offset = (float)randomRange.NextDouble() * amplitude * sign;

                // shift the current point along the normal by the computed offset
                result.Add(point + normal * offset);
            }

            return result;
        }

    }
}