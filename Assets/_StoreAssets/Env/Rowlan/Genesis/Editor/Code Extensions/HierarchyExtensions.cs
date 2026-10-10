using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public static class HierarchyExtensions
    {
        public static TerrainGroup GetTerrainGroup(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            return microVerse.GetComponentInChildren<TerrainGroup>();
        }

        public static StampGroup GetStampGroup(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            return microVerse.GetComponentInChildren<StampGroup>();
        }

        public static LayerGroup GetLayerGroup(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            return microVerse.GetComponentInChildren<LayerGroup>();
        }

        public static PathGroup GetPathGroup(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            return microVerse.GetComponentInChildren<PathGroup>();
        }

        public static TreeGroup GetTreeGroup(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            return microVerse.GetComponentInChildren<TreeGroup>();
        }

        /// <summary>
        /// Get all terrains of terrain group containers
        /// </summary>
        /// <param name="microVerse"></param>
        /// <returns></returns>
        public static Terrain[] GetTerrains(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            List<Terrain> list = new List<Terrain>();

            TerrainGroup[] terrainGroups = microVerse.GetComponentsInChildren<TerrainGroup>();

            foreach( TerrainGroup group in terrainGroups)
            {
                Terrain[] terrains = group.GetComponentsInChildren<Terrain>();
                list.AddRange(terrains);
            }

            return list.ToArray();
        }
    }
}