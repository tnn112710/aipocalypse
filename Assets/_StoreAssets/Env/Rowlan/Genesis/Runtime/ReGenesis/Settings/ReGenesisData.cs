using UnityEngine;
using System;

namespace Rowlan.Genesis.ReGenesis
{
    public class ReGenesisData : ScriptableObject
    {
        public enum HeightStampMode
        {
            Transform,
            Heightmap
        }

        public enum BiomesMode
        {
            Settings,
            Automate
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
        public bool autoAssignment = true;

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        #region HeightStamp

        public HeightStampMode heightStampMode = HeightStampMode.Transform;

        [Serializable]
        public class HeightStampSettings
        {
            public enum HeightMapSource
            {
                ContentBrowser,
                Path,
            }

            public string path = "Assets/Rowlan/Terrain/Stamps/Examples/Heightmaps";

            [MinMax(0f, 1f, false, 3)]
            public Vector2 scaleRangeWidth;

            [MinMax(0f, 1f, false, 3)]
            public Vector2 scaleRangeHeight;

            public HeightMapSource heightMapSource;

            public void Reset()
            {
                heightMapSource = HeightMapSource.ContentBrowser;
                scaleRangeWidth = new Vector2(0.3f, 0.6f);
                scaleRangeHeight = new Vector2(0.3f, 0.6f);
            }
        }

        public HeightStampSettings heightStampSettings = new HeightStampSettings();

        #endregion HeightStamp

        #region Biomes

        public BiomesMode biomesMode = BiomesMode.Settings;

        [Serializable]
        public class BiomesSettings
        {
            [Tooltip("Apply the changes to the children as well")]
            public bool includeChildren;

            [Tooltip("Full random or single offset for all stamps")]
            public NoiseRandomization noiseRandomization;

            public void Reset()
            {
                includeChildren = true;
                noiseRandomization = NoiseRandomization.SharedOffset;
            }
        }
    
        public BiomesSettings biomesSettings = new BiomesSettings();

        #endregion Biomes

        public void Reset()
        {
            autoAssignment = true;

            heightStampSettings = new HeightStampSettings();
            heightStampSettings.Reset();

            biomesSettings = new BiomesSettings();
            biomesSettings.Reset();
        }
    }
}