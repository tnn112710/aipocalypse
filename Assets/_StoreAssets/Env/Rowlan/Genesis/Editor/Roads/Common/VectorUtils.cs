#if __MICROVERSE_ROADS__
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class VectorUtils
    {
        /// <summary>
        /// Check if 2 transforms points are facing each other
        /// </summary>
        /// <param name="pointA"></param>
        /// <param name="pointB"></param>
        /// <param name="angleLimit">The limit at which the both transforms are facing each other. This applies for each side, ie 30f means total of 60f</param>
        /// <returns></returns>
        public static bool IsFacingEachOther(Transform transformA, Transform transformB, out float angleA, out float angleB, float angleLimit = 30f)
        {
            Vector3 lookDir = (transformB.position - transformA.position).normalized;

            Vector3 forward = transformA.forward.normalized;
            Vector3 toOther = transformB.forward.normalized;

            angleA = Vector3.Angle(forward, lookDir);
            angleB = Vector3.Angle(toOther, -lookDir);

            return angleA < angleLimit && angleB < angleLimit;

        }
    }
}
#endif