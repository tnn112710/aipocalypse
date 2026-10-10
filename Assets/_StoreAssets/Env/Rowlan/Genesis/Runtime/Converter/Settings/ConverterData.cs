using System;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class ConverterData : ScriptableObject
    {
        public enum Module
        {
            Terrain,
            Heightmap,
            Assets,
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.Terrain;

        public HeightmapSettings heightmapSettings = new HeightmapSettings();

        [Serializable]
        public class HeightmapSettings
        {
            [Tooltip("Path to heightmaps")]
            public string inputPath;

            [Tooltip("Path where heightstamps are stored")]
            public string outputPath;

            [Tooltip("Name of the heightmap collection")]
            public string collectionName = "Collection";
        }

        public void Reset()
        {
            heightmapSettings = new HeightmapSettings();
        }
    }
}