#if __MICROVERSE_SPLINES__

using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.SplineDeformer
{
    public class SplineDeformerData : ScriptableObject
    {

        public enum Module
        {
            StackedDeformer,
        }

        /// <summary>
        /// The stamp that's being processed. Don't serialize it.
        /// </summary>
        [System.NonSerialized]
        public SplineContainer splineContainer;

        /// <summary>
        /// Default spline index
        /// </summary>
        public int splineIndex = 0;

        /// <summary>
        /// Automatically assigns the selected gameobject
        /// </summary>
        [SerializeField]
        public bool autoAssignment = false;

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.StackedDeformer;

        public StackedDeformerSettings stackedDeformerSettings;

        public void Reset()
        {
            stackedDeformerSettings = new StackedDeformerSettings();
        }
    }
}
#endif