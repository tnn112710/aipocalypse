using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class GeometryUtils
    {

        #region Vector3 intersection check, with intersection point

        public static bool IsIntersecting(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 intersection)
        {
            Vector3 aDirection = a2 - a1;
            Vector3 bDirection = b2 - b1;

            // lines intersect using position & direction
            // => now check if the intersection point is between [a1,a2] and [b1,b2]
            if (LineLineIntersection(a1, aDirection, b1, bDirection, out intersection))
            {
                float aSqrMagnitude = aDirection.sqrMagnitude;
                float bSqrMagnitude = bDirection.sqrMagnitude;

                if ((intersection - a1).sqrMagnitude <= aSqrMagnitude
                     && (intersection - a2).sqrMagnitude <= aSqrMagnitude
                     && (intersection - b1).sqrMagnitude <= bSqrMagnitude
                     && (intersection - b2).sqrMagnitude <= bSqrMagnitude)
                {
                    // there is an intersection between the two segments and it is at intersection
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Check if the lines are intersecting at all towards infinity, ie parameters position and direction
        /// <see href="https://stackoverflow.com/questions/59449628/check-when-two-vector3-lines-intersect-unity3d"/>
        /// <see href="https://wiki.unity3d.com/index.php/3d_Math_functions"/>
        /// </summary>
        /// <param name="p1Position"></param>
        /// <param name="p1Direction"></param>
        /// <param name="p2Position"></param>
        /// <param name="p2Direction"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public static bool LineLineIntersection(Vector3 p1Position, Vector3 p1Direction, Vector3 p2Position, Vector3 p2Direction, out Vector3 intersection)
        {
            float epsilon = 0.0001f;

            Vector3 lineVec3 = p2Position - p1Position;
            Vector3 crossVec1and2 = Vector3.Cross(p1Direction, p2Direction);
            Vector3 crossVec3and2 = Vector3.Cross(lineVec3, p2Direction);
            float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

            // is coplanar, and not parallel
            if (Mathf.Abs(planarFactor) < epsilon && crossVec1and2.sqrMagnitude > epsilon)
            {
                float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
                intersection = p1Position + (p1Direction * s);
                return true;
            }
            else
            {
                intersection = Vector3.zero;
                return false;
            }
        }

        #endregion Vector3 intersection check, with intersection point

        #region Vector2 intersection check, without intersection point

        /// <summary>
        /// Check if lines intersect at all in Vector2 (Vector3.xyz converted to Vector2.xz)
        /// <see href="https://forum.unity.com/threads/line-intersection.17384/#post-4442284"/>
        /// </summary>
        /// <param name="a1"></param>
        /// <param name="a2"></param>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <returns></returns>
        public static bool CheckLineIntersection2D_XZ(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
        {
            return CheckLineIntersection2D(
                new Vector2(a1.x, a1.z),
                new Vector2(a2.x, a2.z),
                new Vector2(b1.x, b1.z),
                new Vector2(b2.x, b2.z)
                );

        }
        /// <summary>
        /// Check if lines intersect at all in Vector2
        /// <see href="https://forum.unity.com/threads/line-intersection.17384/#post-4442284"/>
        /// </summary>
        /// <param name="a1"></param>
        /// <param name="a2"></param>
        /// <param name="b1"></param>
        /// <param name="b2"></param>
        /// <returns></returns>
        public static bool CheckLineIntersection2D(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            Vector2 a = a2 - a1;
            Vector2 b = b1 - b2;
            Vector2 c = a1 - b1;

            float alphaNumerator = b.y * c.x - b.x * c.y;
            float betaNumerator = a.x * c.y - a.y * c.x;
            float denominator = a.y * b.x - a.x * b.y;

            if (denominator == 0)
            {
                return false;
            }
            else if (denominator > 0)
            {
                if (alphaNumerator < 0 || alphaNumerator > denominator || betaNumerator < 0 || betaNumerator > denominator)
                {
                    return false;
                }
            }
            else if (alphaNumerator > 0 || alphaNumerator < denominator || betaNumerator > 0 || betaNumerator < denominator)
            {
                return false;
            }
            return true;
        }

        #endregion Vector2 intersection check, without intersection point
    }
}