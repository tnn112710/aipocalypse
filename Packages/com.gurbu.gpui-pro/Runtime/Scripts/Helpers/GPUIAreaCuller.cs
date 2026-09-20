// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro
{
#if !UNITY_6000_3_0 && !GPUIPRO_NO_HELPURL
    [HelpURL("https://wiki.gurbu.com/index.php?title=GPU_Instancer_Pro:GettingStarted#GPUI_Area_Culler")]
#endif
    [DefaultExecutionOrder(1200)]
    [ExecuteInEditMode]
    public class GPUIAreaCuller : MonoBehaviour
    {
        [SerializeField]
        public bool useColliders = true;
        [SerializeField] 
        public Bounds bounds;
        [SerializeField]
        public List<GPUIManager> gpuiManagerFilter;
        [SerializeField]
        public List<int> prototypeIndexFilter;
        [SerializeField]
        public float offset;
        [SerializeField]
        public GPUIManagerTypeMask managerTypeFilter = 0;

        [NonSerialized]
        private Collider[] _colliders;

        private void OnEnable()
        {
            if (useColliders)
            {
                _colliders = GetComponents<Collider>();
                if (Application.isPlaying && (_colliders == null || _colliders.Length == 0))
                {
                    Debug.LogWarning(GPUIConstants.LOG_PREFIX + "GPUI Area Culler can not find any colliders on its GameObject.", gameObject);
                    enabled = false;
                    return;
                }
            }

            if (Application.isPlaying)
            {
                gpuiManagerFilter ??= new();
                AutoFilterManagerByType(GPUIManagerTypes.GPUIDetailManager);
                AutoFilterManagerByType(GPUIManagerTypes.GPUITreeManager);
                AutoFilterManagerByType(GPUIManagerTypes.GPUIPrefabManager);
            }

            GPUIRenderingSystem.OnBufferDataModified -= CullInstances;
            GPUIRenderingSystem.OnBufferDataModified += CullInstances;
            OnValuesChanged();
        }

        private void OnDisable()
        {
            GPUIRenderingSystem.OnBufferDataModified -= CullInstances;
            OnValuesChanged();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                OnValuesChanged();
            else
#endif
            if (transform.hasChanged)
                OnValuesChanged();
        }

        public void OnValuesChanged()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && useColliders)
                _colliders = GetComponents<Collider>();
#endif
            transform.hasChanged = false;
            GPUITransformBufferUtility.ResetAllCulledInstances();
        }

        private void CullInstances(GPUITransformBufferData transformBufferData)
        {
            if (useColliders && (_colliders == null || _colliders.Length == 0))
                return;

            int bufferStartIndex = 0;
            int bufferSize = transformBufferData.RenderSourceGroup.BufferSize;

            bool hasPrototypeIndexFilter = prototypeIndexFilter != null && prototypeIndexFilter.Count > 0;

            if (gpuiManagerFilter != null && (gpuiManagerFilter.Count > 0 || managerTypeFilter != 0))
            {
                for (int m = 0; m < gpuiManagerFilter.Count; m++)
                {
                    GPUIManager manager = gpuiManagerFilter[m];
                    if (manager == null)
                        continue;
                    int prototypeCount = manager.GetPrototypeCount();
                    for (int p = 0; p < prototypeCount; p++)
                    {
                        if (hasPrototypeIndexFilter && !prototypeIndexFilter.Contains(p))
                            continue;
                        if (!GPUIRenderingSystem.TryGetTransformBufferData(manager.GetRenderKey(p), out GPUITransformBufferData managerBufferData, out bufferStartIndex, out bufferSize) || managerBufferData != transformBufferData)
                            continue;

                        if (useColliders)
                        {
                            foreach (var collider in _colliders)
                                GPUITransformBufferUtility.CullInstancesInsideCollider(transformBufferData, bufferStartIndex, bufferSize, collider, offset);
                        }
                        else
                        {
                            Bounds positionedBounds = bounds;
                            positionedBounds.center += transform.position;
                            GPUITransformBufferUtility.CullInstancesInsideBounds(transformBufferData, bufferStartIndex, bufferSize, positionedBounds, offset);
                        }
                    }

                }
                return;
            }

            if (useColliders)
            {
                foreach (var collider in _colliders)
                    GPUITransformBufferUtility.CullInstancesInsideCollider(transformBufferData, bufferStartIndex, bufferSize, collider, offset);
            }
            else
            {
                Bounds positionedBounds = bounds;
                positionedBounds.center += transform.position;
                GPUITransformBufferUtility.CullInstancesInsideBounds(transformBufferData, bufferStartIndex, bufferSize, positionedBounds, offset);
            }
        }

        private void AutoFilterManagerByType(GPUIManagerTypes managerType)
        {
            if (!IsManagerTypeFiltered(managerType) || !GPUIRenderingSystem.IsActive)
                return;
            foreach (GPUIManager manager in GPUIRenderingSystem.Instance.ActiveGPUIManagers)
            {
                if (manager.GetType().Name == managerType.ToString())
                {
                    if (!gpuiManagerFilter.Contains(manager))
                        gpuiManagerFilter.Add(manager);
                }
            }
        }

        private bool IsManagerTypeFiltered(GPUIManagerTypes managerType)
        {
            return (managerTypeFilter & (GPUIManagerTypeMask)(1 << (int)managerType)) != 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            OnValuesChanged();
        }

        private void OnDrawGizmosSelected()
        {
            if (useColliders)
                return;
            Color gizmoDefaultColor = Gizmos.color;

            Gizmos.color = Color.blue;
            Bounds positionedBounds = bounds;
            positionedBounds.center += transform.position;
            Gizmos.DrawWireCube(positionedBounds.center, positionedBounds.size);

            Gizmos.color = gizmoDefaultColor;
        }
#endif

        public enum GPUIManagerTypes
        {
            GPUIDetailManager = 1,
            GPUITreeManager = 2,
            GPUIPrefabManager = 3
        }

        [Flags]
        public enum GPUIManagerTypeMask
        {
            DetailManager = 1 << GPUIManagerTypes.GPUIDetailManager,
            TreeManager = 1 << GPUIManagerTypes.GPUITreeManager,
            PrefabManager = 1 << GPUIManagerTypes.GPUIPrefabManager
        }
    }
}