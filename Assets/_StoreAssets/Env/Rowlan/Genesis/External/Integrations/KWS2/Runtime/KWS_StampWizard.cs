#if KWS_HDRP || KWS_URP || KWS_BUILTIN

using UnityEngine;

using KWS;
using JBooth.MicroVerseCore;
using System;

namespace Rowlan.Genesis.Integrations.KWS2
{
    [Serializable]
    public class MV_KWS_StampWizard : MonoBehaviour
    {
        [Serializable]
        public class HeightStampSettings
        {
            [Tooltip("The height stamp is usually subtracted resulting in the y scale value being the offset")]
            public float scaleY = 2f; 
        }

        [Serializable]
        public class FilterSettings
        {
            [Serializable]
            public class ConversionSettings
            {
                [Tooltip("If enabled, then everything that's black is 0, the rest is 255. If disabled the grayscale texture will be used.")]
                public bool useThreshold = true;
            }

            [Serializable]
            public class BlurSettings
            {
                [Tooltip("If enabled a blur filter will be applied.")]
                public bool enabled = true;
                [Min(1)]
                public int passes = 1;
                [Min(1)]
                public int radius = 3;
            }

            [Serializable]
            public class ErosionSettings
            {
                public enum Operation
                {
                    Shrink = 0,
                    Expand = 1
                }

                [Tooltip("If enabled an erosion filter will be applied.")]
                public bool enabled = true;

                [Min(1)]
                public int passes = 20;

                [Tooltip("positive = shrink, negative = expand")]
                [Range(-1f,1f)]
                public float erosionStrength = 0.2f;
            }

            public ConversionSettings conversion = new ConversionSettings();
            public BlurSettings blur = new BlurSettings();
            public ErosionSettings erosion = new ErosionSettings();
        }

        public KWS_DynamicWavesSimulationZone zone;

        public ClearStamp clearStamp;
        public HeightStamp heightStamp;
        public FalloffOverride[] falloffOverrides;

        public HeightStampSettings heightStampSettings = new HeightStampSettings();

        public FilterSettings filterSettings = new FilterSettings();

        /// <summary>
        /// Internally used: when the stamp assigned texture changes, ie in memory texture, this is set to false.
        /// When the file is saved, this is set to true.
        /// </summary>
        public bool isTextureSaved = false;

        [Tooltip("Filename without extension")]
        public string fileName = "Heightmap";

        [Tooltip("Append KWS2 cached filename suffix to filename")]
        public bool appendKwsSuffix = true;

        [Tooltip("Optionally skip overwrite check")]
        public bool overwriteFile = false;

        public Texture2D savedTexture;

    }

}
#endif