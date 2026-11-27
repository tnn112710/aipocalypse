namespace Sergio.Rendering
{
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;

    [ExecuteInEditMode]
    public class ObjectSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class PrefabData
        {
            public GameObject prefab;
            [Range(0f, 100f)] public float weight = 100f;

            [HideInInspector] public Mesh mesh;
            [HideInInspector] public Material material;
        }

        [Header("Spawn Area")]
        public float spawnRadius = 10f;
        public string spawnTag = "Terrain";

        [Header("Slope Filtering")]
        [Range(0f, 90f)] public float minSlopeAngle = 0f;
        [Range(0f, 90f)] public float maxSlopeAngle = 30f;

        [Header("Object Prefabs")]
        public PrefabData[] objectPrefabs;

        [Header("Poisson Disk Sampling Settings")]
        [Tooltip("Minimum distance between objects")]
        [Range(0.5f, 20f)]
        public float minDistance = 2f;

        [Tooltip("Number of attempts to place each point")]
        [Range(1, 50)]
        public int poissonSampleAttempts = 30;

        [Tooltip("Maximum number of objects to spawn")]
        public int maxPoissonObjects = 100;

        [Tooltip("If enabled, failed spawns are discarded. If disabled, the system will keep trying to find valid positions")]
        public bool discardFailedSpawns = false;

        [Tooltip("Maximum attempts to find a valid position for objects that fail to spawn (only used when Discard Failed Spawns is off)")]
        [Range(1, 100)]
        public int maxRetryAttempts = 10;

        [Header("Size Range")]
        public Vector2 objectSizeRange = new(0.5f, 1.5f);

        [Header("Rotation Options")]
        public bool alignToGround = false;
        public Vector2 rotationYRange = new(0f, 360f);
        public bool randomFullRotation = false;
        public Vector2 rotationXRange = new(0f, 360f);
        public Vector2 rotationZRange = new(0f, 360f);

        [Header("Random Seed")]
        [Range(0, 100)]
        public int seed = 0;

        [Header("Gizmo Settings")]
        public bool drawGizmo = true;

        [Header("GPU Instancing")]
        public bool enableShadows = true;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        public bool receiveShadows = true;

        [Range(10f, 1000f)] public float cullingDistance = 200f;
        [Range(10f, 1000f)] public float shadowCullingDistance = 100f;

        // =============================

        private class InstanceData
        {
            public Matrix4x4 matrix;
            public Vector3 position;
            public Bounds worldBounds;
        }

        private class InstanceBatch
        {
            public Mesh mesh;
            public Material material;
            public List<InstanceData> instances = new();
        }

        private Dictionary<string, InstanceBatch> instanceBatches = new();
        private MaterialPropertyBlock propertyBlock;

        // Reusable buffer
        private readonly Matrix4x4[] batchBuffer = new Matrix4x4[1023];

        // Cached squared distances
        private float cullingDistanceSqr;
        private float shadowCullingDistanceSqr;

        // Cached gizmo points
        private List<Vector3> cachedGizmoPoints = new();
        private List<Vector3> actualSpawnedPositions = new();

        // =============================

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            UpdateCachedValues();
            ExtractMeshesAndMaterials();
            RegenerateInstances();
        }

        private void OnValidate()
        {
            NormalizeWeights();
            ExtractMeshesAndMaterials();
            UpdateCachedValues();
        }

        private void UpdateCachedValues()
        {
            cullingDistanceSqr = cullingDistance * cullingDistance;
            shadowCullingDistanceSqr = shadowCullingDistance * shadowCullingDistance;
        }

        // =============================

        private void NormalizeWeights()
        {
            if (objectPrefabs == null || objectPrefabs.Length == 0) return;

            float total = 0f;
            foreach (var p in objectPrefabs) total += p.weight;

            if (total <= 0f) return;

            float accum = 0f;
            for (int i = 0; i < objectPrefabs.Length - 1; i++)
            {
                objectPrefabs[i].weight = (objectPrefabs[i].weight / total) * 100f;
                accum += objectPrefabs[i].weight;
            }

            objectPrefabs[^1].weight = 100f - accum;
        }

        public void ExtractMeshesAndMaterials()
        {
            if (objectPrefabs == null) return;

            foreach (var data in objectPrefabs)
            {
                if (!data.prefab) continue;

                var mf = data.prefab.GetComponent<MeshFilter>();
                var mr = data.prefab.GetComponent<MeshRenderer>();

                if (mf) data.mesh = mf.sharedMesh;

                if (mr && mr.sharedMaterial)
                {
#if UNITY_EDITOR
                    data.material = mr.sharedMaterial;
                    if (!data.material.enableInstancing)
                        data.material.enableInstancing = true;
#else
                    data.material = new Material(mr.sharedMaterial);
                    data.material.enableInstancing = true;
#endif
                }
            }
        }

        public void RegenerateInstances()
        {
            ClearInstances();
            actualSpawnedPositions.Clear();
            Random.InitState(seed);
            GeneratePoissonDisk();
            cachedGizmoPoints = new List<Vector3>(actualSpawnedPositions);
        }

        private void GeneratePoissonDisk()
        {
            if (objectPrefabs == null || objectPrefabs.Length == 0)
                return;

            List<Vector3> points = GeneratePoissonDiskPoints();
            int spawnedCount = 0;

            foreach (var point in points)
            {
                if (spawnedCount >= maxPoissonObjects) break;

                bool spawned = TrySpawnAtPoint(point);

                if (!spawned && !discardFailedSpawns)
                {
                    for (int retry = 0; retry < maxRetryAttempts; retry++)
                    {
                        Vector3 retryPoint = GenerateRandomPointInRadius();
                        bool validDistance = true;
                        foreach (var existing in actualSpawnedPositions)
                        {
                            Vector3 diff = retryPoint - existing;
                            diff.y = 0;
                            if (diff.magnitude < minDistance)
                            {
                                validDistance = false;
                                break;
                            }
                        }

                        if (validDistance && TrySpawnAtPoint(retryPoint))
                        {
                            spawned = true;
                            break;
                        }
                    }
                }

                if (spawned) spawnedCount++;
            }
        }

        private Vector3 GenerateRandomPointInRadius()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(0f, spawnRadius);
            return transform.position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
        }

        private bool TrySpawnAtPoint(Vector3 point)
        {
            var prefab = GetRandomWeightedPrefab(objectPrefabs);
            if (prefab == null) return false;

            Vector3 spawnPos = point;
            spawnPos.y = 1000f;

            if (Physics.Raycast(spawnPos, Vector3.down, out var hit) && hit.collider.CompareTag(spawnTag))
            {
                float angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle < minSlopeAngle || angle > maxSlopeAngle) return false;

                AddInstance(prefab, CreateMatrix(hit.point, objectSizeRange, alignToGround ? hit.normal : null));
                actualSpawnedPositions.Add(hit.point);
                return true;
            }

            return false;
        }

        private List<Vector3> GeneratePoissonDiskPoints()
        {
            List<Vector3> points = new() { transform.position };
            List<Vector3> activeList = new() { transform.position };

            while (activeList.Count > 0 && points.Count < maxPoissonObjects)
            {
                int randomIndex = Random.Range(0, activeList.Count);
                Vector3 point = activeList[randomIndex];
                bool found = false;

                for (int i = 0; i < poissonSampleAttempts; i++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float radius = Random.Range(minDistance, minDistance * 2f);
                    Vector3 newPoint = point + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

                    Vector3 offset = newPoint - transform.position;
                    offset.y = 0;
                    if (offset.magnitude > spawnRadius) continue;

                    bool valid = true;
                    foreach (var existingPoint in points)
                    {
                        Vector3 diff = newPoint - existingPoint;
                        diff.y = 0;
                        if (diff.magnitude < minDistance)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        points.Add(newPoint);
                        activeList.Add(newPoint);
                        found = true;
                        break;
                    }
                }

                if (!found) activeList.RemoveAt(randomIndex);
            }

            return points;
        }

        private void AddInstance(PrefabData data, Matrix4x4 matrix)
        {
            string key = data.mesh.GetEntityId() + "_" + data.material.GetEntityId();

            if (!instanceBatches.TryGetValue(key, out var batch))
            {
                batch = new InstanceBatch { mesh = data.mesh, material = data.material };
                instanceBatches.Add(key, batch);
            }

            Vector3 position = matrix.GetColumn(3);
            Bounds worldBounds = TransformBounds(data.mesh.bounds, matrix);

            batch.instances.Add(new InstanceData
            {
                matrix = matrix,
                position = position,
                worldBounds = worldBounds
            });
        }

        private void Update()
        {
            if (instanceBatches.Count == 0) return;

            Camera cam = Application.isPlaying
                ? Camera.main
#if UNITY_EDITOR
                : SceneView.lastActiveSceneView?.camera;
#else
                : null;
#endif

            if (!cam) return;
            RenderInstances(cam);
        }

        private void RenderInstances(Camera cam)
        {
            Vector3 camPos = cam.transform.position;

            foreach (var batch in instanceBatches.Values)
            {
                List<Matrix4x4> visibleMatrices = new();
                List<Matrix4x4> visibleNoShadowMatrices = new();

                foreach (var instance in batch.instances)
                {
                    Vector3 offset = instance.position - camPos;
                    float distSqr = offset.sqrMagnitude;

                    if (distSqr > cullingDistanceSqr) continue;

                    if (distSqr <= shadowCullingDistanceSqr)
                        visibleMatrices.Add(instance.matrix);
                    else
                        visibleNoShadowMatrices.Add(instance.matrix);
                }

                DrawOptimized(batch, visibleMatrices, shadowCastingMode, receiveShadows);
                DrawOptimized(batch, visibleNoShadowMatrices, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
        }

        private void DrawOptimized(InstanceBatch batch, List<Matrix4x4> list,
            UnityEngine.Rendering.ShadowCastingMode shadows, bool receive)
        {
            const int max = 1023;

            for (int i = 0; i < list.Count; i += max)
            {
                int count = Mathf.Min(max, list.Count - i);
                for (int j = 0; j < count; j++) batchBuffer[j] = list[i + j];
                Graphics.DrawMeshInstanced(batch.mesh, 0, batch.material, batchBuffer, count, propertyBlock, shadows, receive);
            }
        }

        private static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            Vector3 center = m.MultiplyPoint3x4(b.center);
            Vector3 e = b.extents;
            Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0, 0));
            Vector3 ay = m.MultiplyVector(new Vector3(0, e.y, 0));
            Vector3 az = m.MultiplyVector(new Vector3(0, 0, e.z));
            e.x = Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x);
            e.y = Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y);
            e.z = Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z);
            return new Bounds(center, e * 2f);
        }

        public void ClearInstances() => instanceBatches.Clear();

        private Matrix4x4 CreateMatrix(Vector3 pos, Vector2 size, Vector3? normal)
        {
            Quaternion rot = normal.HasValue
                ? Quaternion.FromToRotation(Vector3.up, normal.Value)
                : Quaternion.identity;

            rot *= Quaternion.Euler(0, Random.Range(rotationYRange.x, rotationYRange.y), 0);
            float s = Random.Range(size.x, size.y);

            return Matrix4x4.TRS(pos, rot, Vector3.one * s);
        }

        private PrefabData GetRandomWeightedPrefab(PrefabData[] arr)
        {
            if (arr == null || arr.Length == 0) return null;

            float total = 0;
            foreach (var p in arr) total += p.weight;

            float r = Random.Range(0, total);
            float acc = 0;
            foreach (var p in arr)
            {
                acc += p.weight;
                if (r <= acc) return p;
            }

            return arr[0];
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            if (cachedGizmoPoints != null && cachedGizmoPoints.Count > 0)
            {
                Gizmos.color = Color.yellow;
                foreach (var point in cachedGizmoPoints)
                    Gizmos.DrawWireSphere(point, minDistance * 0.5f);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ObjectSpawner))]
    public class ObjectSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var spawner = (ObjectSpawner)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            if (GUILayout.Button("Regenerate Instances"))
            {
                spawner.ExtractMeshesAndMaterials();
                spawner.RegenerateInstances();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Instances"))
            {
                spawner.ClearInstances();
                SceneView.RepaintAll();
            }
        }
    }
#endif
}
