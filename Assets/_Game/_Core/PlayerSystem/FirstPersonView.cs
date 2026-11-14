using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // On the world camera: the lens FirstPersonRenderFeature draws the HeldWeapon layer with (its own field of view and
    // clip planes), so held models never clip into walls and only zoom with the aim.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class FirstPersonView : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] float _nearClip = 0.01f;
        [SerializeField, Min(0.01f)] float _farClip = 10f;

        // 0 = follow the camera's field of view.
        float _fieldOfView;

        public float NearClip => _nearClip;
        public float FarClip => _farClip;
        // Skip drawing the held layer (gun, hands, flash): a full scope's view covers the screen.
        public bool HideHeld { get; set; }

        // The held model's own field of view (PlayerZoom: it follows the aim zoom, not the shot punch or sprint).
        public void SetFieldOfView(float fieldOfView) => _fieldOfView = fieldOfView;

        public float GetFieldOfView(Camera camera) => _fieldOfView > 0f ? _fieldOfView : camera.fieldOfView;
    }
}
