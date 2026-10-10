using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if __MICROVERSE_SPLINES__
using UnityEngine.Splines;
#endif

namespace Rowlan.Genesis
{
#if __MICROVERSE_SPLINES__

    /// <summary>
    /// Spline related MicroVerse extensions
    /// </summary>
    public static class SplineExtensions
    {
        /// <summary>
        /// Align the knots of the splines in the container to the terrain height
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="splineContainer"></param>
        public static void AlignSplines(this MicroVerse microVerse, SplineContainer splineContainer)
        {
            foreach (Spline spline in splineContainer.Splines)
            {
                Matrix4x4 matrix = splineContainer.transform.localToWorldMatrix;

                for (int i = 0; i < spline.Count; i++)
                {
                    BezierKnot knot = spline[i];

                    Vector3 worldPosition = matrix.MultiplyPoint(knot.Position);

                    if (microVerse.GetTerrainHeight(worldPosition, out float height))
                    {
                        knot.Position.y = height - splineContainer.transform.position.y;
                        spline[i] = knot;
                    }

                }
            }
        }

        /// <summary>
        /// Align all splinecontainer children of the provided gameobject to the terrain
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="root"></param>
        public static void AlignSplines(this MicroVerse microVerse, GameObject root)
        {
            if (root == null)
                return;

            SplineContainer[] containers = root.GetComponentsInChildren<SplineContainer>();

            AlignSplines( microVerse, containers);

        }

        /// <summary>
        /// Align all splinecontainer children of the provided gameobject to the terrain. Center the pivot
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="root"></param>
        public static void AlignAndCenterSplines(this MicroVerse microVerse, GameObject root)
        {
            if (root == null)
                return;

            SplineContainer[] containers = root.GetComponentsInChildren<SplineContainer>();

            AlignAndCenter(microVerse, containers);

        }

        /// <summary>
        /// Align all splinecontainers to the terrain
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="containers"></param>
        public static void AlignSplines(this MicroVerse microVerse, SplineContainer[] containers)
        {
            foreach (SplineContainer container in containers)
            {
                MicroVerse.instance.AlignSplines(container);
            }
        }

        /// <summary>
        /// Center the main handle of all containers
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="containers"></param>
        public static void CenterMainHandle(this MicroVerse microVerse, SplineContainer[] containers)
        {
            foreach (SplineContainer container in containers)
            {
                SplineUtils.CenterMainHandle(container);
            }
        }

        /// <summary>
        /// Align knots to terrain and center main handle
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="containers"></param>
        public static void AlignAndCenter(this MicroVerse microVerse, SplineContainer[] containers)
        {
            foreach (SplineContainer container in containers)
            {
                AlignSplines( microVerse, container);
                SplineUtils.CenterMainHandle(container);
            }
        }

    }
#endif
}