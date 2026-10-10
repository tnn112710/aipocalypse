using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis.Environment
{
    public class TerrainHeightSampler
    {
        Terrain[] terrains = new Terrain[0];

        Terrain currentTerrain;

        public TerrainHeightSampler(Terrain[] terrains)
        {
            this.terrains = terrains;
            this.currentTerrain = Terrain.activeTerrain;
        }
        /// <summary>
        /// Get the terrain height for the given world position
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="worldPosition"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public bool GetTerrainHeight(Vector3 worldPosition, out float height)
        {
            // first check if the height can be found in the current terrain
            if (GetTerrainHeight(currentTerrain, worldPosition, out height))
                return true;

            // height not found on current terrain => find matching terrain tile and set new current terrain
            foreach (Terrain terrain in terrains)
            {
                if (GetTerrainHeight(terrain, worldPosition, out height))
                {
                    currentTerrain = terrain;
                    return true;
                }
            }

            height = 0f;
            return false;
        }

        public bool GetTerrainHeight(Terrain terrain, Vector3 worldPosition, out float height)
        {
            if (!terrain.isActiveAndEnabled || currentTerrain == null)
            {
                height = 0f;
                return false;
            }

            Vector3 terrainWorldPosition = terrain.transform.position;

            // world position
            float x = ((worldPosition.x - terrainWorldPosition.x) / terrain.terrainData.size.x) * terrain.terrainData.heightmapResolution;
            float z = ((worldPosition.z - terrainWorldPosition.z) / terrain.terrainData.size.z) * terrain.terrainData.heightmapResolution;

            // if the world position is within the terrain's world position return the terrain
            if (x >= 0 && z >= 0 && x < terrain.terrainData.heightmapResolution && z < terrain.terrainData.heightmapResolution)
            {
                height = terrain.SampleHeight(worldPosition) + terrain.GetPosition().y;
                return true;
            }

            height = 0f;
            return false;
        }
    }
}