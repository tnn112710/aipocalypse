using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Yapp
{
    public class UnityTerrainDetailManager
    {
        private const int PROTOTYPE_DEFAULT_FILTER_INDEX = -1;

#pragma warning disable 0414
        PrefabPainterEditor editor;
#pragma warning restore 0414

        public UnityTerrainDetailManager(PrefabPainterEditor editor)
        {
            this.editor = editor;
        }

        private Terrain GetTerrain()
        {
            Terrain terrain = editor.GetPainter().brushSettings.targetTerrain;

            if (terrain == null)
            {
                Debug.LogError("Terrain not found");
            }

            return terrain;
        }

        private TerrainData GetTerrainData()
        {
            Terrain terrain = GetTerrain();

            if (terrain == null)
            {
                return null;
            }

            return terrain.terrainData;
        }

        public int GetPrototypeIndex(TerrainData terrainData, GameObject prefab)
        {
            DetailPrototype[] prototypes = terrainData.detailPrototypes;

            for (int i = 0; i < prototypes.Length; i++)
            {
                DetailPrototype prototype = prototypes[i];

                if (prototype.usePrototypeMesh && prototype.prototype == prefab)
                    return i;
            }

            return -1;
        }

        public void PlacePrefab(GameObject prefab, bool autoAddPrototype, Vector3 worldPosition, Vector3 worldScale, Quaternion rotation, float brushSize, int density, bool allowOverlap, float overlapThreshold)
        {
            Terrain terrain = GetTerrain();

            if (terrain == null)
                return;

            TerrainData terrainData = terrain.terrainData;

            int prototypeIndex = GetPrototypeIndex(terrainData, prefab);

            if (prototypeIndex == -1)
            {
                if (autoAddPrototype)
                {
                    prototypeIndex = AddPrototype(terrainData, prefab);
                }
                else
                {
                    Debug.LogError("Prototype not found: " + prefab.name);
                    return;
                }
            }

            Vector3 localPosition = GetLocalPosition(terrain, worldPosition);

            int detailWidth = terrainData.detailWidth;
            int detailHeight = terrainData.detailHeight;

            int centerX = Mathf.RoundToInt(localPosition.x * detailWidth);
            int centerZ = Mathf.RoundToInt(localPosition.z * detailHeight);

            float localBrushRadius = GetLocalBrushRadius(terrainData, brushSize);
            int brushRadiusInDetails = Mathf.RoundToInt(localBrushRadius * detailWidth);

            int minX = Mathf.Max(0, centerX - brushRadiusInDetails);
            int maxX = Mathf.Min(detailWidth - 1, centerX + brushRadiusInDetails);
            int minZ = Mathf.Max(0, centerZ - brushRadiusInDetails);
            int maxZ = Mathf.Min(detailHeight - 1, centerZ + brushRadiusInDetails);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            if (width <= 0 || height <= 0)
                return;

            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Paint detail");

            int[,] detailLayer = terrainData.GetDetailLayer(minX, minZ, width, height, prototypeIndex);

            DetailPrototype prototype = terrainData.detailPrototypes[prototypeIndex];
            float targetCoverage = prototype.targetCoverage;
            int maxDetailScatterPerRes = terrainData.maxDetailScatterPerRes;
            float targetValue = targetCoverage * maxDetailScatterPerRes;

            int densityThreshold = allowOverlap ? maxDetailScatterPerRes : Mathf.RoundToInt(maxDetailScatterPerRes * overlapThreshold);

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    int worldX = minX + x;
                    int worldZ = minZ + z;

                    float distanceFromCenter = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(centerX, centerZ));

                    if (distanceFromCenter <= brushRadiusInDetails)
                    {
                        int currentCellValue = detailLayer[z, x];

                        if (!allowOverlap && currentCellValue >= densityThreshold)
                            continue;

                        float normalizedDistance = distanceFromCenter / brushRadiusInDetails;
                        float brushStrength = 1f - normalizedDistance;

                        // opacity / influence factor for the brush at a given detail cell
                        // ie how much should this brush stroke push the current detail value toward the target value
                        float paintStrength = brushStrength * (density / 16f);

                        float currentValue = detailLayer[z, x];
                        float lerpedValue = Mathf.Lerp(currentValue, targetValue, paintStrength);

                        int newValue = Mathf.Min(Mathf.RoundToInt(lerpedValue - 0.5f + UnityEngine.Random.value), maxDetailScatterPerRes);

                        if (!allowOverlap)
                        {
                            newValue = Mathf.Min(newValue, densityThreshold);
                        }

                        detailLayer[z, x] = newValue;
                    }
                }
            }

            terrainData.SetDetailLayer(minX, minZ, prototypeIndex, detailLayer);
            EditorUtility.SetDirty(terrainData);
            terrain.Flush();
        }

        private int AddPrototype(TerrainData terrainData, GameObject prefab)
        {
            DetailPrototype newDetail = new DetailPrototype();

            newDetail.usePrototypeMesh = true;
            newDetail.prototype = prefab;
            newDetail.renderMode = DetailRenderMode.VertexLit;

            newDetail.minWidth = 0.8f;
            newDetail.maxWidth = 1.2f;
            newDetail.minHeight = 0.8f;
            newDetail.maxHeight = 1.2f;
            newDetail.noiseSpread = 0.1f;
            newDetail.healthyColor = Color.white;
            newDetail.dryColor = new Color(0.8f, 0.8f, 0.5f);
            newDetail.useInstancing = true;

            // TODO: settings
            newDetail.targetCoverage = 1.0f;
            newDetail.density = 1.0f;

            DetailPrototype[] prototypes = terrainData.detailPrototypes;

            System.Array.Resize(ref prototypes, prototypes.Length + 1);

            int newIndex = prototypes.Length - 1;
            prototypes[newIndex] = newDetail;

            terrainData.detailPrototypes = prototypes;

            return newIndex;
        }

        public void RemoveAllInstances()
        {
            TerrainData terrainData = GetTerrainData();

            if (terrainData == null)
                return;

            Undo.RegisterCompleteObjectUndo(terrainData, "Remove all details");

            for (int i = 0; i < terrainData.detailPrototypes.Length; i++)
            {
                int[,] emptyLayer = new int[terrainData.detailHeight, terrainData.detailWidth];
                terrainData.SetDetailLayer(0, 0, i, emptyLayer);
            }

            EditorUtility.SetDirty(terrainData);
        }

        public void RemoveAllInstancesAndPrototypes()
        {
            TerrainData terrainData = GetTerrainData();

            if (terrainData == null)
                return;

            Undo.RegisterCompleteObjectUndo(terrainData, "Remove all details and prototypes");

            for (int i = 0; i < terrainData.detailPrototypes.Length; i++)
            {
                int[,] emptyLayer = new int[terrainData.detailHeight, terrainData.detailWidth];
                terrainData.SetDetailLayer(0, 0, i, emptyLayer);
            }

            terrainData.detailPrototypes = new DetailPrototype[0];

            EditorUtility.SetDirty(terrainData);
        }

        public List<GameObject> ExtractPrefabs()
        {
            TerrainData terrainData = GetTerrainData();

            if (terrainData == null)
                return new List<GameObject>();

            List<GameObject> list = new List<GameObject>();

            DetailPrototype[] prototypes = terrainData.detailPrototypes;

            foreach (DetailPrototype prototype in prototypes)
            {
                if (prototype.usePrototypeMesh && prototype.prototype != null)
                {
                    list.Add(prototype.prototype);
                }
            }

            return list;
        }

        public void LogPrototypes()
        {
            TerrainData terrainData = GetTerrainData();

            if (terrainData == null)
                return;

            DetailPrototype[] prototypes = terrainData.detailPrototypes;

            Debug.Log("Detail Prototypes: " + prototypes.Length);

            for (int i = 0; i < prototypes.Length; i++)
            {
                DetailPrototype prototype = prototypes[i];

                if (prototype.usePrototypeMesh)
                {
                    Debug.Log($"  [{i}] Mesh: {(prototype.prototype != null ? prototype.prototype.name : "null")}");
                }
                else
                {
                    Debug.Log($"  [{i}] Texture: {(prototype.prototypeTexture != null ? prototype.prototypeTexture.name : "null")}");
                }
            }
        }

        public void RemoveOverlapping(Vector3 position, float brushSize)
        {
            Terrain terrain = GetTerrain();

            if (terrain == null)
                return;

            RemoveOverlapping(terrain, position, PROTOTYPE_DEFAULT_FILTER_INDEX, brushSize);
        }

        public void RemoveOverlapping(Terrain terrain, Vector3 position, int prototypeIndexFilter, float brushSize)
        {
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Remove details");

            TerrainData terrainData = terrain.terrainData;

            Vector3 localPosition = GetLocalPosition(terrain, position);

            int detailWidth = terrainData.detailWidth;
            int detailHeight = terrainData.detailHeight;

            int centerX = Mathf.RoundToInt(localPosition.x * detailWidth);
            int centerZ = Mathf.RoundToInt(localPosition.z * detailHeight);

            float localBrushRadius = GetLocalBrushRadius(terrainData, brushSize);
            int brushRadiusInDetails = Mathf.RoundToInt(localBrushRadius * detailWidth);

            int minX = Mathf.Max(0, centerX - brushRadiusInDetails);
            int maxX = Mathf.Min(detailWidth - 1, centerX + brushRadiusInDetails);
            int minZ = Mathf.Max(0, centerZ - brushRadiusInDetails);
            int maxZ = Mathf.Min(detailHeight - 1, centerZ + brushRadiusInDetails);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            if (width <= 0 || height <= 0)
                return;

            int startIndex = prototypeIndexFilter == PROTOTYPE_DEFAULT_FILTER_INDEX ? 0 : prototypeIndexFilter;
            int endIndex = prototypeIndexFilter == PROTOTYPE_DEFAULT_FILTER_INDEX ? terrainData.detailPrototypes.Length : prototypeIndexFilter + 1;

            for (int layerIndex = startIndex; layerIndex < endIndex; layerIndex++)
            {
                if (layerIndex >= terrainData.detailPrototypes.Length)
                    continue;

                int[,] detailLayer = terrainData.GetDetailLayer(minX, minZ, width, height, layerIndex);

                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        int worldX = minX + x;
                        int worldZ = minZ + z;

                        float distanceFromCenter = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(centerX, centerZ));

                        if (distanceFromCenter <= brushRadiusInDetails)
                        {
                            detailLayer[z, x] = 0;
                        }
                    }
                }

                terrainData.SetDetailLayer(minX, minZ, layerIndex, detailLayer);
            }

            EditorUtility.SetDirty(terrainData);
            terrain.Flush();
        }

        private static Vector3 GetLocalPosition(Terrain terrain, Vector3 position)
        {
            Vector3 localPosition = new Vector3(
                (position.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (position.y - terrain.transform.position.y) / terrain.terrainData.size.y,
                (position.z - terrain.transform.position.z) / terrain.terrainData.size.z
                );

            return localPosition;
        }

        private static float GetLocalBrushRadius(TerrainData terrainData, float brushSize)
        {
            float localBrushRadius = brushSize * 0.5f;
            localBrushRadius = localBrushRadius / terrainData.size.x;
            return localBrushRadius;
        }
    }
}
