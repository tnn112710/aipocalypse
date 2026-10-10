#if __MICROVERSE_SPLINES__
using JBooth.MicroVerseCore;
using System;
using UnityEngine;

namespace Rowlan.Genesis.BrowserWizard
{
    public class BrowserWizardData : ScriptableObject
    {
        public enum Module
        {
            Biomes,
        }

        public enum BiomesMode
        {
            Shapes,
            Heights,
            Biomes
        }

        public enum BiomesTangentMode
        {
            Linear,
            AutoSmooth,
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.Biomes;

        [Tooltip("Container gameobject for the shapes")]
        public GameObject rootContainer = null;

        [Tooltip("Container gameobject for splines")]
        public GameObject splineContainer = null;

        [Tooltip("Container gameobject for biomes")]
        public GameObject biomesContainer = null;

        [Tooltip("Container gameobject for height")]
        public GameObject heightContainer = null;


        public BiomesMode biomesMode = BiomesMode.Shapes;

        public ShapeSettings shapeSettings = new ShapeSettings();
        public HeightSettings heightSettings = new HeightSettings();
        public BiomesSettings biomesSettings = new BiomesSettings();

        [Serializable]
        public class ShapeSettings
        {
            [Serializable]
            public class Layout
            {
                public int seed = 500;

                [Tooltip("Number of sites (ie Voronoi polygons) to generate")]
                [Range(1, 20)]
                public int sitesToCreate = 10;
            }

            [Serializable]
            public class Spline
            {
                [Tooltip("Tangent mode of the created splines")]
                public BiomesTangentMode tangentMode = BiomesTangentMode.AutoSmooth;

                [Tooltip("Tension affects how curvy splines are at knots. 0 is a sharp corner, 1 is maximum curvitude.")]
                [Range(0f,1f)]
                public float tension = 1 / 4f;
            }

            [Serializable]
            public class Deformer
            {
                public bool enabled = true;

                [Tooltip("Maximum length along an edge for subdivisions")]
                [Range(10, 100)]
                public int maxSubdivisionLength = 50;

                [Tooltip("Overlapping of adjacent edges")]
                public bool edgeOverlap = true;

                [Tooltip("Perlin noise frequency")]
                public float frequency = 10.0f;

                [Tooltip("Perlin noise amplitude")]
                public float amplitude = 30.0f;
            }

            public Layout layout = new Layout();
            public Spline spline = new Spline();
            public Deformer deformer = new Deformer();

        }

        [Serializable]
        public class HeightSettings
        {
            [Tooltip("Range of height stamp randomization")]
            [MinMax(0, 100, true)]
            public Vector2 heightRange = new Vector2(10, 30);

            [Tooltip("Combine mode for height stamps")]
            public HeightStamp.CombineMode combineMode = HeightStamp.CombineMode.Blend;

            [Tooltip("Filter type to use")]
            public FalloffFilter.FilterType filterType = FalloffFilter.FilterType.Range;

            [Tooltip("Filter falloff range randomization")]
            [MinMax(0, 1, true)]
            public Vector2 filterFalloffRange = new Vector2(0.4f, 1.0f);

            [Tooltip("If enabled the bounds of the spline will used for the scale. Otherwise the default scale will be used")]
            public bool splineBoundsScale = true;

            [Tooltip("Use the maximum of x/z for the dimension")]
            public bool keepRatio = true;

        }

        [Serializable]
        public class BiomesSettings
        {
            [Tooltip("Change seed and noise of the stamps after biome instantiation")]
            public bool autoSeedAndNoise = true;

            public NoiseRandomization noiseRandomization = NoiseRandomization.SharedOffset;

            // [Header("Spline Area")]

            [Tooltip("Spline area falloff randomness range")]
            [MinMax(0, 100, true)]
            public Vector2 falloff = new Vector2(10f,20f);

            [InspectorName("Width Boost")]
            [Tooltip("Spline area width boost randomness range")]
            [MinMax(0, 100, true)]
            public Vector2 widthBoost = new Vector2(20f, 40f);
        }


        public void Reset()
        {
            rootContainer = null;
            splineContainer = null;
            biomesContainer = null;
            heightContainer = null;

            shapeSettings = new ShapeSettings();
            heightSettings = new HeightSettings();
            biomesSettings = new BiomesSettings();
        }
    }
}
#endif