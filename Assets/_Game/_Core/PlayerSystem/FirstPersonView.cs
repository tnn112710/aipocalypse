using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // On the world camera: the lens FirstPersonRenderFeature draws the HeldWeapon layer with (its own field of view and
    // clip planes), so held models keep their size while the world zooms and never clip into walls.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class FirstPersonView : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] float _nearClip = 0.01f;
        [SerializeField, Min(0.01f)] float _farClip = 10f;

        // 0 = follow the camera's field of view.
        float _fieldOfView;

        public float NearClip => _nearClip;
        public float FarClip => _farClip;

        // The held model's own field of view, kept while the world camera zooms.
        public void SetFieldOfView(float fieldOfView) => _fieldOfView = fieldOfView;

        public float GetFieldOfView(Camera camera) => _fieldOfView > 0f ? _fieldOfView : camera.fieldOfView;
    }
}
