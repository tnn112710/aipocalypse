#if __MICROVERSE_SPLINES__
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Environment
{
    [ExecuteInEditMode]
    [ExecuteAlways]
    public class AutoRiverShape : MonoBehaviour
    {
        [Tooltip("If active, then the spline will be recreated during movement of the knots. Otherwise manual sampling invocation will be required")]
        public bool continuous = true;

        [Tooltip("If active, then MicroVerse will be refreshed during the spline creation. This may affect performance")]
        public bool autoRefresh = true;

        [Tooltip("The number of spline knot positions")]
        public int riverLength = 20;

        [Tooltip("The sampling distance from one world position to the next from where to sample the terrain")]
        public int samplingDistance = 2;

        [Tooltip("The spline for which the river shall be created")]
        public SplinePath splinePath;
    }
}
#endif