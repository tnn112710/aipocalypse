using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class LayoutUtils
    {
        /// <summary>
        /// Calculate the optimal size of squares to fit into a given rectangle
        /// </summary>
        /// <param name="x">Width of the rectangular area</param>
        /// <param name="y">Height of the rectangular area</param>
        /// <param name="n">Number of squares to fit</param>
        /// <returns></returns>
        public static float FitSquares(float x, float y, int n)
        {
            float sx, sy;

            float px = Mathf.Ceil(Mathf.Sqrt(n * x / y));

            if (Mathf.Floor(px * y / x) * px < n)
            {
                sx = y / Mathf.Ceil(px * y / x);
            }
            else
            {
                sx = x / px;
            }

            float py = Mathf.Ceil(Mathf.Sqrt(n * y / x));
            if (Mathf.Floor(py * x / y) * py < n)
            {
                sy = x / Mathf.Ceil(x * py / y);
            }
            else
            {
                sy = y / py;
            }

            return Mathf.Max(sx, sy);
        }
    }
}