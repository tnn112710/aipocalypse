using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Core.PlayerSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponOverlayCamera : MonoBehaviour
    {
        [SerializeField] Camera _worldCamera;
        [SerializeField] Camera _weaponCamera;

        // 0 = follow the world camera's field of view.
        float _fieldOfView;

        // The held model's own field of view, kept while the world camera zooms.
        public void SetFieldOfView(float fieldOfView) => _fieldOfView = fieldOfView;

        void OnEnable() => RenderPipelineManager.beginCameraRendering += SyncCamera;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= SyncCamera;

        // Cinemachine has finished placing the world camera before URP starts rendering it.
        void SyncCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _worldCamera || _weaponCamera == null)
                return;
            _weaponCamera.fieldOfView = _fieldOfView > 0f ? _fieldOfView : _worldCamera.fieldOfView;
            _weaponCamera.aspect = _worldCamera.aspect;
            _weaponCamera.rect = _worldCamera.rect;
        }
    }
}
