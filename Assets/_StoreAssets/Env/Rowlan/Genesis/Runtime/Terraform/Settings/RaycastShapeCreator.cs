#if __MICROVERSE_SPLINES__

using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Terraform
{
    /// <summary>
    /// Creation of a radial raycast shape. Performs horizontal radial raycasts and creates an outline. Optionally converts the raycast hit points to a spline.
    /// </summary>
    public class RaycastShapeCreator : MonoBehaviour
    {
        #region public inspector data

        [Header("General")]

        [Tooltip("Toggle whether this feature is active or not. This will modify your splines upon property change.")]
        public bool active = true;

        [Header("Raycast")]

        [Tooltip("Layer mask for the raycast")]
        public LayerMask layerMask = int.MaxValue; //everything

        [Tooltip("Maximum number of raycasts to perform")]
        [Range(1, 500)]
        public int rayCount = 20;

        [HideInInspector]
        public int maxHits = 1;

        [Tooltip("Maximum line length to use if the ray didn't hit anything")]
        [Min(1)]
        public float maxLineLength = 500;

        [Tooltip("Optional additional line length. Grow or shrink the shape with this.")]
        public float additionalLineLength = 0f;

        [Header("Visualization")]

        [Tooltip("Visualize raycast lines. Gizmos must be enabled")]
        public bool raycastLinesVisible = false;

        [Tooltip("Visualize raycast lines that hit something. Gizmos must be enabled")]
        public bool hitLinesVisible = true;

        [Tooltip("Visualize the shape that describes the hit points of the raycast. Gizmos must be enabled")]
        public bool shapeVisible = true;

        [HideInInspector]
        public bool logEnabled = false;

        [Header("Spline")]

        [Tooltip("The spline container with the Unity spline. The first spline in the container will be modified")]
        public SplineContainer splineContainer;

        [Space(10)]

        [Tooltip("Create the spline automatically when the raycast settings change")]
        public bool autoCreateSpline = false;

        [Tooltip("Reduce number of spline points automatically")]
        public bool optimizeSpline = true;

        [Space(10)]

        [Tooltip("The maximum distance from the reduced poly line shape for a point to be discarded")]
        [Min(0.01f)]
        public float reductionEpsilon = 0.15f;

        [Tooltip("Tension affects how curvy splines are at the knots. 0 is a sharp corner, 1 is maximum curvature")]
        [Range(0f, 1f)]
        public float splineTension = 1 / 4f;

        #endregion public inspector data

        #region raycast data

        /// <summary>
        /// Data structure for the raycast result.
        /// Required in the MonoBehaviour for Gizmo drawing
        /// </summary>
        [HideInInspector]
        public struct RaycastResult
        {
            public Vector3 origin;

            /// <summary>
            /// The direction in which to cast the ray from the transform's position
            /// </summary>
            public Vector3 raycastDirection;

            /// <summary>
            /// The world position of the hit gameobject. Check first if the the ray hit something
            /// </summary>
            public Vector3 hitPosition;

            /// <summary>
            /// Whether the raycast hit something or not
            /// </summary>
            public bool wasHit;
        }

        /// <summary>
        /// The raycast result, filled from the editor. Required in the MonoBehaviour for Gizmo drawing
        /// </summary>
        [HideInInspector]
        public RaycastResult[] raycastResult = new RaycastResult[0];

        #endregion raycast data

        #region gizmos

        /// <summary>
        /// Draw the raycast result
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!active)
                return;

            if (raycastResult.Length == 0)
                return;

            // draw the rays
            for (int i = 0; i < raycastResult.Length; i++)
            {
                // full ray length; just for ray visualization
                if (raycastLinesVisible)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(raycastResult[i].origin, raycastResult[i].origin + raycastResult[i].raycastDirection * maxLineLength);
                }

                // ray hit: green, ray miss: yellow
                if (hitLinesVisible)
                {
                    Gizmos.color = raycastResult[i].wasHit ? Color.green : Color.yellow;
                    Gizmos.DrawLine(raycastResult[i].origin, raycastResult[i].hitPosition);
                }
            }

            // draw blue line around the rays, area like indicator
            if (shapeVisible)
            {
                Gizmos.color = Color.blue;

                // loop
                for (int i = 0; i < raycastResult.Length; i++)
                {
                    Gizmos.DrawLine(raycastResult[i].hitPosition, raycastResult[(i + 1) % raycastResult.Length].hitPosition);
                }
            }
        }

        #endregion gizmos
    }
}
#endif
