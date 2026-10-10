using JBooth.MicroVerseCore;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JBooth.MicroVerseCore.FilterSet;

namespace Rowlan.Genesis
{
    public class FilterUtils
    {
        public static void DisableTextureFilter(List<Stamp> stamps)
        {
            FilterSet[] filterSets = GetTextureFilterSets(stamps);
            foreach (FilterSet filterSet in filterSets)
            {
                DisableTextureFilter(filterSet);
            }

            MicroVerse.instance.Invalidate();
        }

        private static void DisableTextureFilter(FilterSet filterSet)
        {
            if (filterSet == null)
                return;

            filterSet.textureFilterEnabled = false;
            filterSet.textureFilters.Clear();
        }

        public static void ApplyTextureFilter(List<Stamp> stamps, TerrainLayer[] layers, float[] weights, int maxLayers)
        {
            FilterSet[] filterSets = GetTextureFilterSets(stamps);

            foreach (FilterSet filterSet in filterSets)
            {
                ApplyTextureFilter(filterSet, layers, weights, maxLayers);
            }

            MicroVerse.instance.Invalidate();
        }
        private static void ApplyTextureFilter(FilterSet filterSet, TerrainLayer[] layers, float[] weights, int maxLayers)
        {
            if (filterSet == null)
                return;

            filterSet.textureFilterEnabled = true;
            filterSet.textureFilters.Clear();

            filterSet.otherTextureWeight = 0f;

            float weightSum = 0f;

            for (int i = 0; i < maxLayers; i++)
            {
                // array index out of bounds check
                if (i >= weights?.Length)
                    break;

                weightSum += weights[i];
            }

            for (int i = 0; i < maxLayers; i++)
            {
                // array index out of bounds check
                if (i >= layers?.Length)
                    break;

                TerrainLayer pickedLayer = layers[i];

                // relative weight depending on the splatmap percentage and the number of layers being used
                // 2 layers at 50% and 25% means 75% distribution is split accordingly to match 100% in total
                float weight = weights[i];

                // override weight. all layers filled equally. user can always adapt
                weight = 1f;

                TextureFilter textureFilter = new TextureFilter();
                textureFilter.layer = pickedLayer;
                textureFilter.weight = weight;

                filterSet.textureFilters.Add(textureFilter);
            }

        }

        public static void RemoveNoise(List<Stamp> stamps)
        {
            FilterSet[] filterSets = GetTextureFilterSets(stamps);
            foreach (FilterSet filterSet in filterSets)
            {
                filterSet.weightNoise.noiseType = Noise.NoiseType.None;
                filterSet.weight2Noise.noiseType = Noise.NoiseType.None;
                filterSet.weight3Noise.noiseType = Noise.NoiseType.None;
            }

            MicroVerse.instance.Invalidate();
        }

        private static FilterSet[] GetTextureFilterSets(List<Stamp> stamps)
        {
            return stamps.Where(stamp => stamp != null).Select(stamp => stamp.GetFilterSet()).Where( filterSet => filterSet != null).ToArray();
        }
    }
}