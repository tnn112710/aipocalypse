//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if SPLINES
using UnityEngine.Splines;
using Unity.Mathematics;
#endif

namespace DistantLands.Cozy
{
    public enum BiomeType
    {
        Global,
        Local
    }
    /// <summary>
    /// COZY system that is defined by a weight that is blended into the main system. Can be defined globally with a single float, or using colliders or splines to define a local area.
    /// </summary>
    [AddComponentMenu("Distant Lands/Cozy/Biome")]
    [ExecuteAlways]
    public class CozyBiome : CozySystem
    {
        public BiomeType biomeType = BiomeType.Local;
        private List<Collider> colliders = new List<Collider>();
        public List<Collider> Colliders => colliders;
#if SPLINES
        [SerializeField]
        private float splineRadius = 10f;
        private List<SplineContainer> splines = new List<SplineContainer>();
        public List<SplineContainer> Splines => splines;
#endif

        [SerializeField]
        private float smoothingRadius = 5f;

        public int ColliderCount
        {
            get
            {
                int colliderCount = colliders.Count;
#if SPLINES 
                colliderCount += splines.Count;
#endif
                return colliderCount;
            }
        }

        void OnEnable()
        {
            ResetModules();
            UpdateColliders();
#if UNITY_EDITOR
            SceneView.duringSceneGui += UpdateBiomeWeightFromSceneView;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            SceneView.duringSceneGui -= UpdateBiomeWeightFromSceneView;
#endif
        }

        /// <summary>
        /// Update is called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        void Update()
        {
            weight = CalculateWeight();

#if UNITY_EDITOR
            UpdateColliders();
#endif
        }

        /// <summary>
        /// Update the colliders and splines to include any on this object or on the children of this.
        /// </summary>
        public void UpdateColliders()
        {
            GetComponentsInChildren(colliders);
#if SPLINES
            GetComponentsInChildren(splines);
#endif
        }

        private float CalculateWeight()
        {
            Vector3 pos = Vector3.zero;

            if (CozyTrackPoint.Instance != null)
                pos = CozyTrackPoint.Instance.transform.position;

#if UNITY_EDITOR
            if (!Application.isPlaying && SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.camera)
                pos = SceneView.lastActiveSceneView.camera.transform.position;
#endif

            return CalculateWeight(pos);
        }

        private float CalculateWeight(Vector3 pos)
        {
            if (biomeType == BiomeType.Global)
                return TargetWeight;

            float calculatedWeight = 0;

            foreach (Collider collider in colliders)
            {
                if (collider == null) continue;
                if (!collider.isTrigger) collider.isTrigger = true;

                if (smoothingRadius <= 0)
                {
                    calculatedWeight = Mathf.Max(calculatedWeight, (pos - collider.ClosestPoint(pos)).sqrMagnitude > 0 ? 1 : 0);
                }
                else
                {
                    float distSqr = (pos - collider.ClosestPoint(pos)).sqrMagnitude;
                    float remappedDistance = Mathf.Clamp01(distSqr / (smoothingRadius * smoothingRadius));
                    calculatedWeight = Mathf.Max(calculatedWeight, 1 - remappedDistance);
                }

                if (calculatedWeight >= 0.9999f)
                {
                    return TargetWeight;
                }
            }

#if SPLINES

            foreach (SplineContainer spline in splines)
            {
                if (spline == null) continue;
                Vector3 localPos = spline.transform.InverseTransformPoint(pos);

                SplineUtility.GetNearestPoint(
                    spline.Spline,
                    (float3)localPos,
                    out float3 nearestLocalPos,
                    out float t
                );

                Vector3 nearestWorldPos = spline.transform.TransformPoint((Vector3)nearestLocalPos);

                if (smoothingRadius <= 0)
                {
                    calculatedWeight = Mathf.Max(calculatedWeight, (pos - nearestWorldPos).sqrMagnitude > 0 ? 1 : 0);
                }
                else
                {
                    float distSqr = (pos - nearestWorldPos).sqrMagnitude - splineRadius;
                    float remappedDistance = Mathf.Clamp01(distSqr / (smoothingRadius * smoothingRadius));
                    calculatedWeight = Mathf.Max(calculatedWeight, 1 - remappedDistance);
                }

                if (calculatedWeight >= 0.9999f)
                {
                    return TargetWeight;
                }
            }

#endif

            return calculatedWeight * TargetWeight;
        }

#if UNITY_EDITOR
        private void UpdateBiomeWeightFromSceneView(SceneView sceneView)
        {
            if (Application.isPlaying || sceneView != SceneView.lastActiveSceneView || !sceneView.camera)
                return;

            weight = CalculateWeight(sceneView.camera.transform.position);
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Matrix4x4 originalMatrix = Gizmos.matrix;

            if (colliders != null)
                foreach (Collider collider in colliders)
                {
                    if (collider == null) continue;

                    Transform t = collider.transform;

                    if (collider is BoxCollider box)
                    {
                        Gizmos.matrix = t.localToWorldMatrix;
                        Gizmos.DrawCube(box.center, box.size);
                    }
                    else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                    {
                        Gizmos.matrix = t.localToWorldMatrix;
                        Gizmos.DrawMesh(meshCollider.sharedMesh);
                    }
                    else if (collider is SphereCollider sphere)
                    {
                        Gizmos.matrix = originalMatrix;

                        Vector3 worldCenter = t.TransformPoint(sphere.center);
                        float maxScale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Max(Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));
                        Gizmos.DrawSphere(worldCenter, sphere.radius * maxScale);
                    }
                }

            Gizmos.matrix = originalMatrix;

#if SPLINES
            if (splines != null)
            {

            }
#endif
        }

    }
}