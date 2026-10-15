using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Core.PlayerSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponOverlayCamera : MonoBehaviour
    {
        [SerializeField] Camera _worldCamera;
        [SerializeField] Camera _weaponCamera;

        void OnEnable() => RenderPipelineManager.beginCameraRendering += SyncCamera;

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= SyncCamera;

        // Cinemachine has finished placing the world camera before URP starts rendering it.
        void SyncCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _worldCamera || _weaponCamera == null)
                return;
            _weaponCamera.fieldOfView = _worldCamera.fieldOfView;
            _weaponCamera.aspect = _worldCamera.aspect;
            _weaponCamera.rect = _worldCamera.rect;
        }
    }
}
