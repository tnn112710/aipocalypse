using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreShowcaseTarget : MonoBehaviour, IArmoryCoreBallisticTarget
    {
        [SerializeField] private Transform platePivot;
        [SerializeField] private Renderer plateRenderer;
        [SerializeField, Min(0.1f)] private float resetDelay = 1.6f;
        [SerializeField, Min(1f)] private float rotationSpeed = 540f;

        private Quaternion uprightRotation = Quaternion.identity;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private float knockedDownUntil;
        private float flashUntil;

        public void Configure(Transform pivot, Renderer targetRenderer)
        {
            platePivot = pivot;
            plateRenderer = targetRenderer;
            CaptureInitialState();
        }

        private void Awake()
        {
            CaptureInitialState();
        }

        public void ReceiveBallisticHit(ArmoryCoreBallisticHit hit)
        {
            knockedDownUntil = Time.time + resetDelay;
            flashUntil = Time.time + 0.12f;
        }

        private void Update()
        {
            if (platePivot != null)
            {
                var targetRotation = Time.time < knockedDownUntil
                    ? uprightRotation * Quaternion.Euler(82f, 0f, 0f)
                    : uprightRotation;
                platePivot.localRotation = Quaternion.RotateTowards(
                    platePivot.localRotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }

            if (plateRenderer == null)
                return;
            propertyBlock ??= new MaterialPropertyBlock();
            plateRenderer.GetPropertyBlock(propertyBlock);
            var color = Time.time < flashUntil ? Color.white * 2.5f : baseColor;
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color * 0.35f);
            plateRenderer.SetPropertyBlock(propertyBlock);
        }

        private void CaptureInitialState()
        {
            if (platePivot != null)
                uprightRotation = platePivot.localRotation;
            var material = plateRenderer == null ? null : plateRenderer.sharedMaterial;
            if (material == null)
                return;
            if (material.HasProperty("_BaseColor"))
                baseColor = material.GetColor("_BaseColor");
            else if (material.HasProperty("_Color"))
                baseColor = material.GetColor("_Color");
        }
    }
}
