using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rowlan.Genesis
{
    /// <summary>
    /// Applies noise-based deformation to Voronoi cells by subdividing edges
    /// and offsetting subdivision points using Perlin noise.
    /// </summary>
    public class VoronoiDeformer
    {

        /// <summary>
        /// Deforms a set of base cells by subdividing their edges and adding noise.
        /// Shared edges use a shared subdivision to maintain continuity.
        /// </summary>
        /// <param name="bounds2D">Rectangular bounds for clipping cells.</param>
        /// <param name="polygons">List of polygon cells, each as a list of vertices.</param>
        /// <param name="maxSubdivisionLength">Maximum allowed length of each subdivision segment.</param>
        /// <param name="noiseFrequency">Frequency parameter for Perlin noise.</param>
        /// <param name="noiseAmplitude">Amplitude parameter for Perlin noise.</param>
        /// <returns>A new list of deformed cells with added noise.</returns>
        public static List<List<Vector2>> Deform( Rect bounds2D, List<List<Vector2>> polygons, float maxSubdivisionLength, bool edgeOverlap, float noiseFrequency, float noiseAmplitude)
        {
            var edgeToSubdivisions = new Dictionary<EdgeKey, List<Vector2>>();
            var deformedCells = new List<List<Vector2>>(polygons.Count);

            foreach (var polygon in polygons)
            {
                var deformedPolygon = new List<Vector2>(polygon.Count * 2);

                // for noisy overlapping edges we use random subdivision
                float maxLength = maxSubdivisionLength;
                
                if(edgeOverlap)
                {
                    // arbitrary subdivision length
                    maxLength = Random.Range(maxSubdivisionLength * 0.5f, maxSubdivisionLength * 1.5f);
                }

                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector2 a = polygon[i];
                    Vector2 b = polygon[(i + 1) % polygon.Count];

                    // keep the original vertex
                    deformedPolygon.Add(a);

                    // skip noise on boundary edges (left/right/top/bottom), those should remain as they are
                    if (IsBoundaryEdge(bounds2D, a, b))
                        continue;

                    // for detection of adjacent edges
                    var edgeKey = new EdgeKey(a, b);

                    // get or compute noisy subdivision for this edge
                    if (!edgeToSubdivisions.TryGetValue(edgeKey, out var noisySubdivision))
                    {
                        noisySubdivision = ComputeNoisySubdivision(
                            edgeKey.A,
                            edgeKey.B,
                            maxLength,
                            noiseFrequency,
                            noiseAmplitude);

                        edgeToSubdivisions[edgeKey] = noisySubdivision;
                    }

                    // add subdivision points in the correct orientation
                    if (ApproximatelyEqual(a, edgeKey.A))
                        deformedPolygon.AddRange(noisySubdivision);
                    else
                        deformedPolygon.AddRange(noisySubdivision.AsEnumerable().Reverse());
                }

                deformedCells.Add(deformedPolygon);
            }

            return deformedCells;
        }

        /// <summary>
        /// Checks if the edge AB lies along the defined bounds (i.e., a border edge).
        /// </summary>
        private static bool IsBoundaryEdge(Rect bounds, Vector2 a, Vector2 b)
        {
            // vertical and horizontal bounds
            bool ax = Mathf.Approximately(a.x, bounds.xMin) || Mathf.Approximately(a.x, bounds.xMax);
            bool bx = Mathf.Approximately(b.x, bounds.xMin) || Mathf.Approximately(b.x, bounds.xMax);
            bool ay = Mathf.Approximately(a.y, bounds.yMin) || Mathf.Approximately(a.y, bounds.yMax);
            bool by = Mathf.Approximately(b.y, bounds.yMin) || Mathf.Approximately(b.y, bounds.yMax);

            return (ax && bx) || (ay && by);
        }

        /// <summary>
        /// Subdivides the edge AB so that no segment is longer than maxLength,
        /// then offsets each subdivision point along the perpendicular using Perlin noise.
        /// </summary>
        private static List<Vector2> ComputeNoisySubdivision( Vector2 a, Vector2 b, float maxLength, float noiseFrequency, float noiseAmplitude)
        {
            // Step 1: subdivide edge by maxLength
            float edgeLength = Vector2.Distance(a, b);
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(edgeLength / maxLength));
            var subdivisionPoints = new List<Vector2>(segmentCount - 1);

            for (int i = 1; i < segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                subdivisionPoints.Add(Vector2.Lerp(a, b, t));
            }

            // Step 2: apply Perlin noise along the perpendicular direction
            Vector2 direction = b - a;
            Vector2 perpendicular = new Vector2(direction.y, -direction.x).normalized;

            var noisyPoints = new List<Vector2>(subdivisionPoints.Count);
            foreach (var point in subdivisionPoints)
            {
                float noiseValue = Mathf.PerlinNoise(point.x * noiseFrequency, point.y * noiseFrequency) * 2f - 1f;
                noisyPoints.Add(point + perpendicular * noiseValue * noiseAmplitude);
            }

            return noisyPoints;
        }

        /// <summary>
        /// Determines if two vectors are approximately equal within a small tolerance.
        /// </summary>
        private static bool ApproximatelyEqual(Vector2 v1, Vector2 v2)
        {
            return Vector2.SqrMagnitude(v1 - v2) < 1e-6f;
        }

        /// <summary>
        /// Struct used as a key for shared edges, ordering vertices so AB == BA.
        /// </summary>
        private struct EdgeKey
        {
            public readonly Vector2 A;
            public readonly Vector2 B;

            public EdgeKey(Vector2 v1, Vector2 v2)
            {
                if (v1.x < v2.x || (Mathf.Approximately(v1.x, v2.x) && v1.y < v2.y))
                {
                    A = v1;
                    B = v2;
                }
                else
                {
                    A = v2;
                    B = v1;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is EdgeKey other))
                    return false;

                return ApproximatelyEqual(A, other.A) && ApproximatelyEqual(B, other.B);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A.GetHashCode() * 397) ^ B.GetHashCode();
                }
            }
        }
    }
}
