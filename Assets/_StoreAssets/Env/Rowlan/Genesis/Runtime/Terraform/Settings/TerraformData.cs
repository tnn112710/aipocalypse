using UnityEngine;
using JBooth.MicroVerseCore;
using System;

namespace Rowlan.Genesis.Terraform
{
    public class TerraformData : ScriptableObject
    {
        public enum HeightStampMode
        {
            Transform,
        }

        public enum SplineMode
        {
            Operations,
            Shape,
            Mask
        }

        public enum FalloffMode
        {
            Hierarchy
        }

        /// <summary>
        /// The stamp that's being processed. Don't serialize it.
        /// </summary>
        [System.NonSerialized]
        public GameObject gameObject;

        /// <summary>
        /// Automatically assigns the selected gameobject
        /// </summary>
        [SerializeField]
        public bool autoAssignment = false;

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public SplineMode splineMode = SplineMode.Operations;

        [Serializable]
        public class SplineSettings
        {
            [MinMax(-20f, 20f, false)]
            public Vector2 deformationAngle = new Vector2(-20f, 20f);

            [MinMax(0.2f, 0.8f, false)]
            public Vector2 subdivisionRange = new Vector2( 0.5f, 0.5f);

            /// <summary>
            /// If this value is greater than 0, then a Dougles Peucker algorithm will be used with this tolerance level in order to reduce the number of points.
            /// </summary>
            [Range(0f,20f)]
            public float douglasPeuckerReductionTolerance = 0f;
        }

        public SplineSettings splineSettings = new SplineSettings();


        public HeightStampMode heightStampMode = HeightStampMode.Transform;

        public FalloffMode falloffMode = FalloffMode.Hierarchy;

        public void Reset()
        {
        }
    }
}