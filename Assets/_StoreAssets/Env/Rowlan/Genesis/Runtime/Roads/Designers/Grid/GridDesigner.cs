#if __MICROVERSE_ROADS__
using System;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    [Serializable]
    public class GridDesigner : MonoBehaviour
    {
        [Tooltip("Number of intersections in X direction")]
        [Range(1,10)]
        public int countX = 3;

        [Tooltip("Number of intersections in Z direction")]
        [Range(1, 10)]
        public int countZ = 3;

        [Tooltip("Distance between intersections in X direction")]
        public float distanceX = 50f;

        [Tooltip("Distance between intersections in Z direction")]
        public float distanceZ = 100f;

        [Tooltip("Gameobject with a cross intersection")]
        public GameObject intersectionCross = null;

        [Tooltip("Gameobject with a T intersection")]
        public GameObject intersectionT = null;

        [Tooltip("Layer to raycast against, usually the terrain layer")]
        public LayerMask layer = int.MaxValue; // int.MaxValue = Everything

        [Tooltip("Y position offset for placement after raycast")]
        public float positionOffsetY = 0.0f;

        [Serializable]
        public class JitterSettings
        {
            [MinMax(-10f, 10f, false, -1)]
            public Vector2 rotationOffset = new Vector2(-2f, 2f);

            [MinMax(-10f, 10f, false, -1)]
            public Vector2 heightOffset = new Vector2(0f, 2f);

            [MinMax(-10f, 10f, false, -1)]
            public Vector2 positionOffset = new Vector2(-2f, 2f);
        }

        public JitterSettings jitterSettings = new JitterSettings();
    }
}
#endif