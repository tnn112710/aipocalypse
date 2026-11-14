using System;
using UnityEngine;

namespace ArmoryCore
{
    /// <summary>Optic module with an aim point, adjustable zoom and zero distance.</summary>
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreSightModule : ArmoryCoreAttachmentModule
    {
        [Header("Sight")]
        [SerializeField] private Transform aimPoint;
        [SerializeField] private bool useDefinitionMagnification = true;
        [SerializeField, Min(1f)] private float minimumMagnification = 1f;
        [SerializeField, Min(1f)] private float maximumMagnification = 1f;
        [SerializeField, Min(1f)] private float currentMagnification = 1f;
        [SerializeField, Min(1f)] private float zeroDistanceMeters = 100f;
        [SerializeField] private float[] zoomSteps = Array.Empty<float>();
        [SerializeField, Min(0.01f)] private float aimTimeMultiplier = 1f;

        public override bool OverridesOptic => true;
        public override float CurrentMagnification => currentMagnification;
        public override float CurrentZeroDistanceMeters => zeroDistanceMeters;
        public override float AimTimeMultiplier => aimTimeMultiplier;
        public override Transform AimPoint => aimPoint;

        public float MinimumMagnification => minimumMagnification;
        public float MaximumMagnification => maximumMagnification;
        public bool IsReflexSight => Definition != null && Definition.IsReflexSight;
        public bool IsMagnifiedSight => !IsReflexSight && maximumMagnification > 1.01f;

        public void ConfigureAimPoint(Transform value)
        {
            aimPoint = value;
            SynchronizeDefinitionDefaults();
            CalibrateAimPointFromGeometry();
        }

        public void SetMagnification(float value)
        {
            var clamped = Mathf.Clamp(value, minimumMagnification, maximumMagnification);
            if (Mathf.Approximately(currentMagnification, clamped))
                return;
            currentMagnification = clamped;
            RequestStatsRefresh();
        }

        public float CycleMagnification(int direction = 1)
        {
            if (zoomSteps != null && zoomSteps.Length > 0)
            {
                var closest = 0;
                var closestDistance = float.PositiveInfinity;
                for (var i = 0; i < zoomSteps.Length; i++)
                {
                    var distance = Mathf.Abs(zoomSteps[i] - currentMagnification);
                    if (distance >= closestDistance)
                        continue;
                    closest = i;
                    closestDistance = distance;
                }
                var next = (closest + Math.Sign(direction) + zoomSteps.Length) % zoomSteps.Length;
                SetMagnification(zoomSteps[next]);
            }
            else
            {
                SetMagnification(direction >= 0 && currentMagnification < maximumMagnification
                    ? maximumMagnification
                    : minimumMagnification);
            }
            return currentMagnification;
        }

        public void SetZeroDistance(float meters)
        {
            zeroDistanceMeters = Mathf.Max(1f, meters);
            RequestStatsRefresh();
        }

        private void Awake()
        {
            SynchronizeDefinitionDefaults();
            CalibrateAimPointFromGeometry();
        }

        private void OnValidate()
        {
            SynchronizeDefinitionDefaults();
            aimTimeMultiplier = Mathf.Max(0.01f, aimTimeMultiplier);
        }

        private void SynchronizeDefinitionDefaults()
        {
            if (useDefinitionMagnification && Definition != null)
            {
                minimumMagnification = Definition.GameplayMinimumMagnification;
                maximumMagnification = Definition.GameplayMaximumMagnification;
                if (zeroDistanceMeters <= 1f || Mathf.Approximately(zeroDistanceMeters, 100f))
                    zeroDistanceMeters = Mathf.Max(1f, Definition.defaultZeroDistanceMeters);
            }
            minimumMagnification = Mathf.Max(1f, minimumMagnification);
            maximumMagnification = Mathf.Max(minimumMagnification, maximumMagnification);
            currentMagnification = Mathf.Clamp(currentMagnification, minimumMagnification, maximumMagnification);
            zeroDistanceMeters = Mathf.Max(1f, zeroDistanceMeters);
        }

        private void CalibrateAimPointFromGeometry()
        {
            if (aimPoint == null || aimPoint.localPosition.sqrMagnitude > 0.0000001f)
                return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            var foundBounds = false;
            var localBounds = default(Bounds);
            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;
                var bounds = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var rendererPoint = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var modulePoint = transform.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererPoint));
                    if (!foundBounds)
                    {
                        localBounds = new Bounds(modulePoint, Vector3.zero);
                        foundBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(modulePoint);
                    }
                }
            }

            if (!foundBounds)
                return;
            aimPoint.localPosition = new Vector3(
                Mathf.Lerp(localBounds.max.x, localBounds.min.x, 0.08f),
                Mathf.Lerp(localBounds.min.y, localBounds.max.y, 0.58f),
                localBounds.center.z);
            aimPoint.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        }
    }
}
