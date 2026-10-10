using UnityEngine;
using System.Collections.Generic;

namespace Rowlan.Genesis
{
    // Static class for generating a Voronoi diagram in Unity.
    // Each cell is clipped to the given rectangular bounds.
    public static class VoronoiGenerator
    {
        // Generates 'siteCount' random sites within 'bounds2D', seeded by 'seed',
        // then builds and returns clipped Voronoi cells as lists of 2D points.
        public static List<List<Vector2>> GenerateVoronoi(Rect bounds2D, int siteCount, int seed)
        {
            // save old global RNG state so we can restore it later
            var oldState = Random.state;

            // initialize UnityEngine.Random with our seed for reproducibility
            Random.InitState(seed);

            // pick random site positions inside the bounds
            List<Vector2> sites = new List<Vector2>(siteCount);
            for (int i = 0; i < siteCount; i++)
            {
                float x = Random.Range(bounds2D.xMin, bounds2D.xMax);
                float y = Random.Range(bounds2D.yMin, bounds2D.yMax);
                sites.Add(new Vector2(x, y));
            }

            // restore previous RNG state to avoid side effects
            Random.state = oldState;

            // compute Voronoi cells for these sites within bounds2D
            return GenerateVoronoi(sites, bounds2D);
        }

        // Builds clipped Voronoi cells for a given list of sites.
        // Each cell is the intersection of the bounding rectangle and
        // all half-planes closer to its site than to any other site.
        private static List<List<Vector2>> GenerateVoronoi(List<Vector2> sites, Rect bounds2D)
        {
            int siteCount = sites.Count;
            var cells = new List<List<Vector2>>(siteCount);

            // for each site, start with full rectangle and clip by bisectors
            for (int i = 0; i < siteCount; i++)
            {
                // initial region: full bounds as a convex quad
                List<Vector2> region = new List<Vector2>
                {
                    new Vector2(bounds2D.xMin, bounds2D.yMin),
                    new Vector2(bounds2D.xMax, bounds2D.yMin),
                    new Vector2(bounds2D.xMax, bounds2D.yMax),
                    new Vector2(bounds2D.xMin, bounds2D.yMax)
                };

                Vector2 site = sites[i];

                // clip region by every other site
                for (int j = 0; j < siteCount; j++)
                {
                    // skip self
                    if (j == i) 
                        continue;  

                    Vector2 other = sites[j];
                    Vector2 direction = other - site;  // direction from site to other

                    // mid-dot value defines the bisector line: direction.x*x + direction.y*y = midDot
                    Vector2 mid = (site + other) * 0.5f;
                    float midDot = Vector2.Dot(direction, mid);

                    // apply half-plane clipping (inside if dot <= midDot)
                    region = ClipPolygon(region, direction, midDot);

                    // if region emptied, no need to continue
                    if (region.Count == 0)
                        break;
                }

                cells.Add(region);
            }

            return cells;
        }

        // Clips the convex polygon 'poly' by the half-plane direction.x*x + direction.y*y <= midDot.
        // Uses the Sutherland-Hodgman algorithm for one line.
        private static List<Vector2> ClipPolygon(List<Vector2> poly, Vector2 direction, float midDot)
        {
            var output = new List<Vector2>();
            int n = poly.Count;
            if (n == 0)
                return output;

            Vector2 prev = poly[n - 1];
            float prevDot = Vector2.Dot(direction, prev);
            bool prevInside = (prevDot <= midDot);

            // process each edge of the polygon
            for (int i = 0; i < n; i++)
            {
                Vector2 curr = poly[i];
                float currDot = Vector2.Dot(direction, curr);
                bool currInside = (currDot <= midDot);

                // edge enters the half-plane
                if (currInside && !prevInside)
                {
                    Vector2 intersectionPoint = LineIntersect(prev, curr, direction, midDot);
                    output.Add(intersectionPoint);
                }

                // current point inside: keep it
                if (currInside)
                {
                    output.Add(curr);
                }

                // edge exits the half-plane
                if (!currInside && prevInside)
                {
                    Vector2 inter = LineIntersect(prev, curr, direction, midDot);
                    output.Add(inter);
                }

                prev = curr;
                prevInside = currInside;
            }

            return output;
        }

        /// <summary>
        /// Finds the intersection of the segment p1 -> p2 with the infinite line
        /// defined by direction dot X = midDot, and clamps it to lie on the segment.
        /// </summary>
        private static Vector2 LineIntersect(Vector2 p1, Vector2 p2, Vector2 otherDirection, float midDot)
        {
            // 1) Build the segment vector from p1 to p2.
            Vector2 lineDirection = p2 - p1;

            // 2) Compute the numerator: how far p1 is from the line along 'direction'.
            //    If direction * p = midDot exactly, p1 is on the line and num = 0.
            float numerator = midDot - Vector2.Dot(otherDirection, p1);

            // 3) Compute the denominator: how much moving along 'dir' changes the dot with 'direction'.
            //    If den == 0, the segment is parallel to the line (no unique intersection).
            float denominator = Vector2.Dot(otherDirection, lineDirection);

            // 4) Compute t = num/den giving the fraction along p1 -> p2 where the line is crossed.
            //    If segment is nearly parallel (|den|<eps), default t=0 to avoid divide by zero.
            float t = (Mathf.Abs(denominator) < 1e-6f) ? 0f : numerator / denominator;

            // 5) Clamp t into [0,1] so the intersection point stays between p1 and p2.
            t = Mathf.Clamp01(t);

            // 6) Return the point p1 + t*(p2-p1), which is the intersection on (or projected onto) the segment.
            return p1 + lineDirection * t;
        }

    }
}
