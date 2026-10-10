using JBooth.MicroVerseCore;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// This class is partially derived from the Unity Terrain Tools
/// <see cref="https://docs.unity3d.com/Packages/com.unity.terrain-tools@4.0/manual/index.html"/>
/// The Unity Companion License applies.
/// </summary>
namespace Rowlan.Genesis
{
    public class TerrainCreator
    { 
        public static void Create( GameObject parent, GenesisData genesisData)
        {
            GenesisData.TerrainSettings settings = genesisData.terrainSettings;

            // check if file path is valid
            while (!IsDirectoryWithinAssets(settings.assetDirectory))
            {
                EditorUtility.DisplayDialog("Terrain Data Directory", "Terrain data must be saved within Assets directory", "Ok");
                GUIUtility.keyboardControl = 0;
                return;
            }

            // remove existing terrains
            if (settings.clearExisting)
            {
                ClearExistingTerrains();
            }

            // create tiles
            int tileCount = settings.tilesX * settings.tilesZ;
            Vector2Int tileOffset = Vector2Int.zero;
            Vector2Int tileOffsetSource = Vector2Int.zero;
            Vector2Int tileResolution = new Vector2Int((int)settings.totalWidth / settings.tilesX, (int)settings.totalLength / settings.tilesZ);
            Vector3 tileSize = new Vector3(tileResolution.x, settings.totalHeight, tileResolution.y);
            Vector3 tilePosition = settings.startPosition;
            Terrain[] terrains = new Terrain[tileCount];

            string assetFolderPath = GetAssetPathFromFullPath(settings.assetDirectory);
            int tileIndex = 0;

            try
            {
                GameObject terrainGroup = parent;
                terrainGroup.transform.position = settings.startPosition;

                // <rowlan> added this:
                terrainGroup.transform.parent = parent.transform;

                Undo.RegisterCreatedObjectUndo(terrainGroup, "Create terrain");

                // heightmap offset
                tileOffsetSource = tileResolution;

                for (int x = 0; x < settings.tilesX; x++, tileOffset.x += tileOffsetSource.x, tilePosition.x += tileResolution.x)
                {
                    tileOffset.y = 0;
                    tilePosition.z = settings.startPosition.z;

                    for (int y = 0; y < settings.tilesZ; y++, tileOffset.y += tileOffsetSource.y, tilePosition.z += tileResolution.y)
                    {
                        EditorUtility.DisplayProgressBar("Creating terrains", string.Format("Updating terrain tile ({0}, {1})", x, y), ((float)tileIndex / tileCount));

                        TerrainData terrainData = new TerrainData();
                        terrainData.alphamapResolution = settings.controlTextureResolution;
                        terrainData.baseMapResolution = settings.baseTextureResolution;
                        terrainData.SetDetailResolution(settings.detailResolution, settings.detailResolutionPerPatch);

                        GameObject newGO = Terrain.CreateTerrainGameObject(terrainData);
                        Terrain newTerrain = newGO.GetComponent<Terrain>();
                        newTerrain.groupingID = settings.groupingID;
                        newTerrain.allowAutoConnect = settings.autoConnect;
                        newTerrain.drawInstanced = settings.drawInstanced;
                        newTerrain.heightmapPixelError = settings.pixelError;
                        newTerrain.basemapDistance = settings.baseMapDistance;
   
                        string terrainName = $"Terrain_{x}_{y}";
                        
                        if (settings.enableGuid)
                        {
                            Guid newGuid = Guid.NewGuid();
                            terrainName = $"Terrain_{x}_{y}_{newGuid}";
                        }

                        newGO.name = terrainName;
                        newTerrain.transform.position = tilePosition;
                        newTerrain.transform.SetParent(terrainGroup.transform);

                        terrains[tileIndex] = newTerrain;
                        tileIndex++;

                        // save terrain data asset
                        terrainData.size = tileSize; // set terrain size after heightmap process
                        string assetPath = $"{assetFolderPath}/{terrainName}.asset";
                        if (!Directory.Exists(assetFolderPath))
                        {
                            Directory.CreateDirectory(assetFolderPath);
                        }
                        AssetDatabase.CreateAsset(terrainData, assetPath);

                        // finally, resize height resolution if needed
                        if (terrainData.heightmapResolution != settings.heightmapResolution)
                        {
                            ResizeHeightmap(terrainData, settings.heightmapResolution);
                        }

                        Undo.RegisterCreatedObjectUndo(newGO, "Create terrain");
                    }
                }

                CalculateAdjacencies(terrains, settings.tilesX, settings.tilesZ);
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            }
        }

        private static string GetAssetPathFromFullPath(string fullPath)
        {
            string newPath = fullPath;
            string assetsFolder = "Assets";
            string assetsDataPath = Application.dataPath;
            if (fullPath.Contains(assetsDataPath))
            {
                newPath = assetsFolder + fullPath.Replace(assetsDataPath, string.Empty);
            }

            return newPath;
        }

        /// <summary>
        /// Get a path relative to the project directory for saving assets.
        /// If the path is outside the assets directory it will return the Assets folder.
        /// </summary>
        /// <param name="requestedDirectory">The directory we want to save to, absolute or relative</param>
        /// <param name="forceToAssets">If we want the directory to be forced into the assets directory</param>
        /// <returns>A relative path, inside the project directory</returns>
        internal static string GetProjectRelativeSaveDirectory(string requestedDirectory, bool forceToAssets = true)
        {
            var referenceUri = new Uri(Application.dataPath + "/..");
            if (Path.IsPathRooted(requestedDirectory))
            {
                try
                {
                    var fileUri = new Uri(requestedDirectory);
                    requestedDirectory = Uri.UnescapeDataString(referenceUri.MakeRelativeUri(fileUri).ToString());
                }
                catch
                {
                    if (forceToAssets)
                        requestedDirectory = "Assets";

                    return requestedDirectory;
                }
            }
            else
            {
                var fileUri = new Uri(Application.dataPath + "/../" + requestedDirectory);
                requestedDirectory = Uri.UnescapeDataString(referenceUri.MakeRelativeUri(fileUri).ToString());
            }

            // if the directory doesn't start with "Assets+Path.DirectorySeparatorChar"
            // then the terraindata will not end up in the assetdatabase, and not correctly be loaded
            // so force it to the correct location
            if (forceToAssets && (!requestedDirectory.StartsWith("Assets") || !requestedDirectory.StartsWith("Assets/")))
                requestedDirectory = "Assets";

            return requestedDirectory;
        }

        /// <summary>
        /// Reports if a directory is within the Project/Assets folder.
        /// Directory does not need to exist.
        /// </summary>
        /// <param name="requestedDirectory"></param>
        /// <returns></returns>
        internal static bool IsDirectoryWithinAssets(string requestedDirectory)
        {
            return GetProjectRelativeSaveDirectory(requestedDirectory, false) == GetProjectRelativeSaveDirectory(requestedDirectory);
        }

        public static void ClearExistingTerrains()
        {
            Terrain[] childTerrains = MicroVerse.instance.GetTerrains();

            List<string> dataPaths = new List<string>();

            if (childTerrains != null && childTerrains.Length > 0)
            {
                foreach (Terrain terrain in childTerrains)
                {
                    TerrainData terrainData = terrain.terrainData;
                    dataPaths.Add(AssetDatabase.GetAssetPath(terrainData));
                }
            }

            foreach (string path in dataPaths)
            {
                Debug.Log( $"Deleting {path}");
                AssetDatabase.DeleteAsset(path);
            }

            foreach (Terrain terrain in childTerrains)
            {
                UnityEngine.Object.DestroyImmediate(terrain.gameObject);
            }

            AssetDatabase.SaveAssets();
        }

        public static void CalculateAdjacencies(Terrain[] terrains, int tilesX, int tilesZ)
        {
            if (terrains == null || terrains.Length == 0)
            {
                return;
            }

            // set neighbor terrains to update normal maps
            for (int y = 0; y < tilesZ; y++)
            {
                for (int x = 0; x < tilesX; x++)
                {
                    int index = (y * tilesX) + x;
                    Terrain terrain = terrains[index];
                    Terrain leftTerrain = (x > 0) ? terrains[index - 1] : null;
                    Terrain rightTerrain = (x < tilesX - 1) ? terrains[index + 1] : null;
                    Terrain topTerrain = (y > 0) ? terrains[index - tilesX] : null;
                    Terrain bottomTerrain = (y < tilesZ - 1) ? terrains[index + tilesX] : null;

                    // NOTE: "top" and "bottom" are reversed because of the way the terrain is handled...
                    terrain.SetNeighbors(leftTerrain, bottomTerrain, rightTerrain, topTerrain);
                }
            }
        }


        public static void ResizeHeightmap(TerrainData terrainData, int resolution)
        {
            RenderTexture oldRT = RenderTexture.active;
            {
                RenderTexture oldHeightmap = RenderTexture.GetTemporary(terrainData.heightmapTexture.descriptor);
                Graphics.Blit(terrainData.heightmapTexture, oldHeightmap);

#if UNITY_2019_3_OR_NEWER
                // terrain holes
                RenderTexture oldHoles = RenderTexture.GetTemporary(terrainData.holesTexture.width, terrainData.holesTexture.height);
                Graphics.Blit(terrainData.holesTexture, oldHoles);
#endif

                Undo.RegisterCompleteObjectUndo(terrainData, "Resize heightmap");

                float sUV = 1.0f;
                int dWidth = terrainData.heightmapResolution;
                int sWidth = resolution;

                Vector3 oldSize = terrainData.size;
                terrainData.heightmapResolution = resolution;
                terrainData.size = oldSize;

                oldHeightmap.filterMode = FilterMode.Bilinear;

                // Make sure textures are offset correctly when resampling
                // tsuv = (suv * swidth - 0.5) / (swidth - 1)
                // duv = (tsuv(dwidth - 1) + 0.5) / dwidth
                // duv = (((suv * swidth - 0.5) / (swidth - 1)) * (dwidth - 1) + 0.5) / dwidth
                // k = (dwidth - 1) / (swidth - 1) / dwidth
                // duv = suv * (swidth * k)		+ 0.5 / dwidth - 0.5 * k

                float k = (dWidth - 1.0f) / (sWidth - 1.0f) / dWidth;
                float scaleX = sUV * (sWidth * k);
                float offsetX = (float)(0.5 / dWidth - 0.5 * k);
                Vector2 scale = new Vector2(scaleX, scaleX);
                Vector2 offset = new Vector2(offsetX, offsetX);

                Graphics.Blit(oldHeightmap, terrainData.heightmapTexture, scale, offset);
                RenderTexture.ReleaseTemporary(oldHeightmap);

#if UNITY_2019_3_OR_NEWER
                oldHoles.filterMode = FilterMode.Point;
                Graphics.Blit(oldHoles, (RenderTexture)terrainData.holesTexture);
                RenderTexture.ReleaseTemporary(oldHoles);
#endif
            }
            RenderTexture.active = oldRT;

            terrainData.DirtyHeightmapRegion(new RectInt(0, 0, terrainData.heightmapTexture.width, terrainData.heightmapTexture.height), TerrainHeightmapSyncControl.HeightAndLod);
#if UNITY_2019_3_OR_NEWER
            terrainData.DirtyTextureRegion(TerrainData.HolesTextureName, new RectInt(0, 0, terrainData.holesTexture.width, terrainData.holesTexture.height), false);
#endif
        }

    }
}