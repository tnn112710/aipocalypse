using JBooth.MicroVerseCore;
using UnityEngine;

namespace Rowlan.Genesis
{
    public static class MicroVerseExtensions
    {
        /// <summary>
        /// Synchronize heightmap and collider terrain data
        /// </summary>
        /// <param name="microVerse"></param>
        public static void SyncHeightmaps(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            Terrain[] terrains = microVerse.GetComponentsInChildren<Terrain>();

            foreach (Terrain terrain in terrains)
            {
                terrain.terrainData.SyncHeightmap();
            }
        }

        /// <summary>
        /// Get the terrain height for the given world position
        /// </summary>
        /// <param name="microVerse"></param>
        /// <param name="worldPosition"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static bool GetTerrainHeight(this JBooth.MicroVerseCore.MicroVerse microVerse, Vector3 worldPosition, out float height)
        {
            Terrain[] terrains = microVerse.GetComponentsInChildren<Terrain>();

            foreach (Terrain terrain in terrains)
            {
                if (!terrain.isActiveAndEnabled)
                    continue;

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
            }

            height = 0f;
            return false;
        }

        /// <summary>
        /// Get the bounds of all terrains in world space
        /// </summary>
        /// <param name="microVerse"></param>
        /// <returns></returns>
        public static Bounds GetTerrainWorldBounds(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            Terrain[] terrains = microVerse.GetComponentsInChildren<Terrain>();

            Bounds totalWorldBounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                Bounds terrainWorldBounds = new Bounds(terrain.terrainData.bounds.center + terrain.transform.position, terrain.terrainData.bounds.size);

                if (i == 0)
                {
                    totalWorldBounds = terrainWorldBounds;
                }
                else
                {
                    totalWorldBounds.Encapsulate(terrainWorldBounds);
                }
            }


            return totalWorldBounds;
        }

        /// <summary>
        /// Get the maximum height of the terrain mesh.
        /// </summary>
        /// <param name="microVerse"></param>
        /// <returns></returns>
        public static float GetTerrainMeshHeight(this JBooth.MicroVerseCore.MicroVerse microVerse)
        {
            Terrain[] terrains = microVerse.GetComponentsInChildren<Terrain>();

            float height = 0f;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];

                if (i == 0)
                {
                    height = terrain.terrainData.size.y;
                }
                else
                {
                    height = Mathf.Max( height, terrain.terrainData.size.y);
                }
            }


            return height;
        }
    }
}