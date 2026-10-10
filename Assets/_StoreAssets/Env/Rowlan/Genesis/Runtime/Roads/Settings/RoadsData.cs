#if __MICROVERSE_ROADS__
using UnityEngine;
using JBooth.MicroVerseCore;
using System;

namespace Rowlan.Genesis.Roads
{
    public class RoadsData : ScriptableObject
    {
        public enum RoadSystemMode
        {
            Design,
            Batch,
            Global,
        }

        public enum IntersectionMode
        {
            Design,
            Align
        }

        public enum RoadMode
        {
            Design,
            Objects,
            Align,
            Split
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

        public RoadSystemMode roadSystemMode = RoadSystemMode.Design;

        [Serializable]
        public class RoadSystemSettings
        {
            [Serializable]
            public class JitterSettings
            {
                [MinMax(-10f,10f, false, -1)]
                public Vector2 rotationOffset = new Vector2(-2f, 2f);

                [MinMax(-10f, 10f, false, -1)]
                public Vector2 heightOffset = new Vector2( 0f, 2f);

                [MinMax(-10f, 10f, false, -1)]
                public Vector2 positionOffset = new Vector2(-2f, 2f);
            }

            public JitterSettings jitterSettings = new JitterSettings();
        }

        public RoadSystemSettings roadSystemSettings = new RoadSystemSettings(); // TODO: currently unused

        public RoadMode roadMode = RoadMode.Design;

        [Serializable]
        public class RoadSettings
        {
        }

        public RoadSettings roadSettings = new RoadSettings(); // TODO: currently unused


        public IntersectionMode intersectionMode = IntersectionMode.Design;

        public void Reset()
        {
        }
    }
}
#endif