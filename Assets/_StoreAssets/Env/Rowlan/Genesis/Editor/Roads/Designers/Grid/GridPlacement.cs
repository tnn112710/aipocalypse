#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class GridPlacement
    {
        /// <summary>
        /// Start the raycast way above the gameobject's position.
        /// </summary>
        private const float raycastStartOffsetY = 1000f;

        RoadSystem roadSystem;
        GameObject container;

        private int countX;
        private int countZ;
        private float distanceX;
        private float distanceZ;
        private GameObject prefabCross;
        private GameObject prefabT;
        private LayerMask layer;

        private GameObject[,] prefabGrid;

        public GridPlacement(RoadSystem roadSystem, GameObject container, int countX, int countZ, float distanceX, float distanceZ, GameObject prefabCross, GameObject prefabT, LayerMask layer)
        {
            this.roadSystem = roadSystem;
            this.container = container;
            this.countX = countX;
            this.countZ = countZ;
            this.distanceX = distanceX;
            this.distanceZ = distanceZ;
            this.prefabCross = prefabCross;
            this.prefabT = prefabT;
            this.layer = layer;
        }

        public void CreateGrid()
        {
            prefabGrid = new GameObject[countX, countZ];

            PositionIntersections();
            ConnectIntersections();
        }

        public void PositionIntersections()
        {
            int gridMinX = 0;
            int gridMinZ = 0;
            int gridMaxX = countX - 1;
            int gridMaxZ = countZ - 1;

            // center grid at container position
            float offsetCenterX = ((countX-1) * distanceX) / 2f;
            float offsetCenterZ = ((countZ-1) * distanceZ) / 2f;

            // add the intersections
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    Vector3 localPosition = new Vector3(x * distanceX - offsetCenterX, raycastStartOffsetY, z * distanceZ - offsetCenterZ);

                    Vector3 worldPosition = container.transform.TransformPoint(localPosition);
                    
                    Vector3 worldRaycastHit;
                    if (Physics.Raycast(worldPosition, Vector3.down, out RaycastHit raycastHitDown, Mathf.Infinity, layer))
                    {
                        worldRaycastHit = raycastHitDown.point;
                    }
                    else if (Physics.Raycast(worldPosition, Vector3.up, out RaycastHit raycastHitUp, Mathf.Infinity, layer))
                    {
                        worldRaycastHit = raycastHitUp.point;
                    }
                    else
                    {
                        // create actual position, even though invalid
                        // we don't support skipping intersections yet
                        // placement will be wrong, log error ...
                        // otherwise we'd have to consider invalid placement for the grid
                        worldRaycastHit = Vector3.zero;
                        Debug.LogError( $"No raycast hit for grid {x}/{z}");
                    }

                    Vector3 localRaycastPosition = container.transform.InverseTransformPoint(worldRaycastHit);

                    localPosition.y = localRaycastPosition.y;

                    GameObject prefab;

                    bool isAtBounds = x == gridMinX || x == gridMaxX || z == gridMinZ || z == gridMaxZ;
                    if (isAtBounds)
                    {
                        // create prefab
                        prefab = PrefabUtility.InstantiatePrefab(prefabT, container.transform) as GameObject;

                        // transform changes
                        float rotationY = 0f;
                        if (x == gridMinX)
                        {
                            rotationY = 90f;
                        }
                        else if (x == gridMaxX)
                        {
                            rotationY = 270f;
                        }

                        if (z == gridMinZ)
                        {
                            rotationY = 0f;
                        }
                        else if (z == gridMaxZ)
                        {
                            rotationY = 180f;
                        }

                        prefab.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

                    }
                    else
                    {
                        // create prefab
                        prefab = PrefabUtility.InstantiatePrefab(prefabCross, container.transform) as GameObject;
                    }

                    // set prefab transform settings
                    prefab.transform.localPosition = localPosition;

                    // register prefab on grid
                    prefabGrid[x,z] = prefab;
                }
            }

        }

        /// <summary>
        /// Iterate through the grid, get the neighbours and auto-connect them.
        /// This is way faster than just getting all intersections and auto-connect them.
        /// Otherwise the number of items to compare with each other would be too large
        /// 
        /// Another algorithm could eg be to just collect the intersections that face into the same and opposite directions, considering an angle delta.
        /// </summary>
        private void ConnectIntersections()
        {
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    GameObject sourcePrefab = prefabGrid[x, z];

                    List<Intersection> intersections = getNeighbours(x, z);

                    Intersection intersection = sourcePrefab.GetComponent<Intersection>();
                    intersections.Insert(0, intersection);

                    // connect intersections
                    AutoConnector autoConnector = new AutoConnector(roadSystem, intersections, AutoConnector.Priority.Distance, 35f);
                    autoConnector.AutoConnect();

                }
            }
        }

        private List<Intersection> getNeighbours(int x, int z)
        {
            Intersection intersection;

            List<Intersection> neighbours = new List<Intersection>();

            intersection = getIntersectionSafe(x + 0, z - 1); if (intersection != null) neighbours.Add(intersection); // top
            intersection = getIntersectionSafe(x + 1, z + 0); if (intersection != null) neighbours.Add(intersection); // right
            intersection = getIntersectionSafe(x + 0, z + 1); if (intersection != null) neighbours.Add(intersection); // bottom
            intersection = getIntersectionSafe(x - 0, z + 0); if (intersection != null) neighbours.Add(intersection); // left

            return neighbours;

        }

        private Intersection getIntersectionSafe( int x, int z)
        {
            if( x >= 0 && x < countX && z >= 0 && z < countZ )
            {
                return prefabGrid[x, z].GetComponent<Intersection>();
            }

            return null;
        }

    }
}
#endif