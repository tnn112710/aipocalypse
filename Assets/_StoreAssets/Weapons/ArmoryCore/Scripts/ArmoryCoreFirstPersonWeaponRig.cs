using System.Collections.Generic;
using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreFirstPersonWeaponRig : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] private Vector3 aimOffset = new(-0.19f, 0.135f, -0.035f);
        [SerializeField] private Vector3 sprintOffset = new(0.09f, -0.13f, -0.08f);
        [SerializeField] private Vector3 sprintRotation = new(12f, -8f, -15f);
        [SerializeField, Min(0.01f)] private float poseSharpness = 15f;
        [SerializeField, Min(0.01f)] private float recoilSharpness = 17f;

        [Header("Motion")]
        [SerializeField] private Vector2 walkBob = new(0.012f, 0.017f);
        [SerializeField] private Vector2 sprintBob = new(0.02f, 0.028f);
        [SerializeField, Min(0.1f)] private float walkBobFrequency = 8.5f;
        [SerializeField] private Vector2 lookSway = new(0.0022f, 0.0018f);

        [Header("Collision")]
        [SerializeField, Min(0.1f)] private float collisionProbeDistance = 1.05f;
        [SerializeField, Min(0.01f)] private float collisionProbeRadius = 0.08f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private readonly RaycastHit[] collisionHits = new RaycastHit[16];
        private ArmoryCoreWeaponController weapon;
        private ArmoryCoreShowcaseFlyCamera movement;
        private Camera viewCamera;
        private Vector3 smoothedPosition;
        private Quaternion smoothedRotation = Quaternion.identity;
        private Vector3 recoilPosition;
        private Vector3 recoilRotation;
        private Vector3 recoilPositionVelocity;
        private Vector3 recoilRotationVelocity;
        private Vector2 smoothedSway;
        private float bobPhase;
        private float aimBlend;
        private float sprintBlend;
        private float obstructionBlend;
        private Transform magazine;
        private Vector3 magazineBaseLocalPosition;
        private Quaternion magazineBaseLocalRotation = Quaternion.identity;
        private ArmoryCoreWeaponModelParts modelParts;
        private Transform pump;
        private Vector3 pumpBaseLocalPosition;
        private Quaternion pumpBaseLocalRotation = Quaternion.identity;
        private Vector3 pumpTravelDirection = Vector3.right;
        private Transform slide;
        private Vector3 slideBaseLocalPosition;
        private Quaternion slideBaseLocalRotation = Quaternion.identity;
        private Vector3 slideTravelDirection = Vector3.right;
        private float pumpCycleStartTime;
        private float pumpCycleDuration = 0.7f;
        private float shotActionStartTime = float.NegativeInfinity;
        private float shotActionDuration = 0.1f;
        private float currentPumpTravel;
        private float currentSlideTravel;
        private float currentActionRotation;
        private bool wasCycling;
        private bool wasReloading;
        private bool reloadWasEmpty;
        private float reloadStartTime;
        private float reloadDuration = 1f;
        private float reloadBlend;
        private Vector3 calibratedAimOffset;
        private Vector3 calibratedAimEuler;
        private Transform calibratedAimAnchor;
        private bool aimPoseCalibrated;
        private readonly List<Renderer> opticHiddenRenderers = new();
        private readonly List<bool> opticRendererStates = new();
        private Texture2D scopeMask;
        private bool weaponHiddenForScope;

        public float PendingRecoilAmount => recoilPosition.magnitude
            + recoilRotation.magnitude
            + recoilPositionVelocity.magnitude * 0.1f
            + recoilRotationVelocity.magnitude * 0.01f;
        public bool HasAnimatedMagazine => magazine != null;
        public bool HasAnimatedPump => pump != null;
        public bool HasAnimatedSlide => slide != null;
        public bool HasAnimatedActionPart => pump != null || slide != null;
        public bool PumpAnimationActive => currentPumpTravel > 0.0001f
            || currentSlideTravel > 0.0001f
            || (weapon != null && weapon.State == ArmoryCoreWeaponState.Cycling);
        public bool ActionAnimationActive => PumpAnimationActive;
        public float CurrentPumpTravel => currentPumpTravel;
        public float CurrentSlideTravel => currentSlideTravel;
        public float CurrentActionTravel => Mathf.Max(currentPumpTravel, currentSlideTravel);
        public float CurrentActionRotation => currentActionRotation;
        public bool ReloadAnimationActive => reloadBlend > 0.00001f;
        public bool HasCalibratedAimPose => aimPoseCalibrated;
        public float AimBlend => aimBlend;
        public bool IsOpticOverlayVisible => aimBlend > 0.65f && weapon?.ActiveSight != null;

        public void Configure(
            ArmoryCoreWeaponController weaponController,
            ArmoryCoreShowcaseFlyCamera movementController,
            Camera cameraValue)
        {
            if (weapon != null)
                weapon.RecoilApplied -= OnRecoil;
            weapon = weaponController;
            movement = movementController;
            viewCamera = cameraValue;
            if (weapon != null)
                weapon.RecoilApplied += OnRecoil;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            smoothedPosition = Vector3.zero;
            smoothedRotation = Quaternion.identity;
            recoilPosition = Vector3.zero;
            recoilRotation = Vector3.zero;
            recoilPositionVelocity = Vector3.zero;
            recoilRotationVelocity = Vector3.zero;
            shotActionStartTime = float.NegativeInfinity;
            currentPumpTravel = 0f;
            currentSlideTravel = 0f;
            FindAnimatedParts();
            CalibrateAimPose();
        }

        private void LateUpdate()
        {
            if (weapon == null || movement == null || viewCamera == null)
                return;

            var deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var moveAmount = movement.MovementAmount;
            weapon.SetMovementAmount(moveAmount);

            var currentAimAnchor = ResolveAimAnchor();
            if (!aimPoseCalibrated || currentAimAnchor != calibratedAimAnchor)
            {
                RestoreWeaponAfterScope();
                CalibrateAimPose();
            }

            var isReloading = weapon.State == ArmoryCoreWeaponState.Reloading;
            if (isReloading && !wasReloading)
                BeginReloadAnimation();
            reloadBlend = Mathf.MoveTowards(reloadBlend, isReloading ? 1f : 0f,
                deltaTime * (isReloading ? 8f : 5f));
            var reloadProgress = isReloading
                ? Mathf.Clamp01((Time.time - reloadStartTime) / Mathf.Max(0.1f, reloadDuration))
                : 1f;

            var aimTime = Mathf.Max(0.06f, weapon.Stats.AimTimeSeconds);
            var aimSpeed = 1f / aimTime;
            aimBlend = Mathf.MoveTowards(aimBlend, weapon.IsAiming && !isReloading ? 1f : 0f, aimSpeed * deltaTime);
            sprintBlend = Mathf.MoveTowards(sprintBlend, movement.IsSprinting && !isReloading ? 1f : 0f, 5.5f * deltaTime);

            if (moveAmount > 0.05f)
                bobPhase += deltaTime * walkBobFrequency * Mathf.Lerp(0.8f, 1.55f, sprintBlend);
            var bobSize = Vector2.Lerp(walkBob, sprintBob, sprintBlend) * moveAmount * (1f - aimBlend * 0.72f);
            var bob = new Vector3(
                Mathf.Cos(bobPhase * 0.5f) * bobSize.x,
                Mathf.Sin(bobPhase) * bobSize.y,
                0f);

            var desiredSway = new Vector2(
                -movement.LookDelta.x * lookSway.x,
                -movement.LookDelta.y * lookSway.y);
            smoothedSway = Vector2.Lerp(smoothedSway, desiredSway,
                1f - Mathf.Exp(-10f * deltaTime));
            var breath = new Vector3(
                Mathf.Sin(Time.unscaledTime * 1.1f) * 0.0018f,
                Mathf.Sin(Time.unscaledTime * 1.7f) * 0.0022f,
                0f) * (1f - sprintBlend) * (1f - aimBlend * 0.45f);

            var obstructed = CalculateObstruction();
            obstructionBlend = Mathf.Lerp(obstructionBlend, obstructed,
                1f - Mathf.Exp(-18f * deltaTime));

            var stancePosition = calibratedAimOffset * aimBlend
                + sprintOffset * sprintBlend;
            var stanceEuler = calibratedAimEuler * aimBlend
                + sprintRotation * sprintBlend;
            var targetPosition = stancePosition
                + bob
                + breath
                + new Vector3(smoothedSway.x, smoothedSway.y, 0f)
                + new Vector3(0.02f, -0.13f, -0.16f) * obstructionBlend;
            var targetEuler = stanceEuler
                + new Vector3(
                    smoothedSway.y * 110f,
                    -smoothedSway.x * 95f,
                    -smoothedSway.x * 45f)
                + new Vector3(18f, -5f, -6f) * obstructionBlend;

            CalculateReloadPose(reloadProgress, isReloading, out var reloadPosition, out var reloadEuler);
            targetPosition += (reloadPosition - stancePosition) * reloadBlend;
            targetEuler += (reloadEuler - stanceEuler) * reloadBlend;

            UpdateRecoilSpring(deltaTime);

            targetPosition += recoilPosition;
            var targetRotation = Quaternion.Euler(targetEuler + recoilRotation);
            smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition,
                1f - Mathf.Exp(-poseSharpness * deltaTime));
            smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRotation,
                1f - Mathf.Exp(-poseSharpness * deltaTime));
            transform.localPosition = smoothedPosition;
            transform.localRotation = smoothedRotation;
            UpdateScopeVisibility();
            AnimateMagazine(reloadProgress, isReloading);
            AnimateActionPart();
            wasReloading = isReloading;
        }

        private Transform ResolveAimAnchor()
        {
            var sight = weapon == null ? null : weapon.ActiveSight;
            return sight != null && sight.AimPoint != null && sight.AimPoint.gameObject.activeInHierarchy
                ? sight.AimPoint
                : null;
        }

        private void CalibrateAimPose()
        {
            calibratedAimOffset = aimOffset;
            calibratedAimEuler = Vector3.zero;
            calibratedAimAnchor = ResolveAimAnchor();
            aimPoseCalibrated = false;
            if (weapon == null || viewCamera == null)
                return;

            Vector3 referencePosition;
            Vector3 referenceForward;
            if (calibratedAimAnchor != null)
            {
                referencePosition = transform.InverseTransformPoint(calibratedAimAnchor.position);
                referenceForward = transform.InverseTransformDirection(calibratedAimAnchor.forward);
            }
            else
            {
                referencePosition = ResolveFallbackAimPoint();
                var forward = weapon.Muzzle == null ? weapon.transform.forward : weapon.Muzzle.forward;
                referenceForward = transform.InverseTransformDirection(forward);
            }

            if (referenceForward.sqrMagnitude < 0.0001f)
                return;
            referenceForward.Normalize();
            var correction = Quaternion.FromToRotation(referenceForward, Vector3.forward);
            var correctionEuler = NormalizeEuler(correction.eulerAngles);
            correction = Quaternion.Euler(correctionEuler);

            var correctedReference = correction * referencePosition;
            calibratedAimOffset = new Vector3(
                Mathf.Clamp(-correctedReference.x, -0.45f, 0.45f),
                Mathf.Clamp(-correctedReference.y, -0.35f, 0.45f),
                aimOffset.z);
            calibratedAimEuler = correctionEuler;
            aimPoseCalibrated = true;
        }

        private void UpdateScopeVisibility()
        {
            var sight = weapon == null ? null : weapon.ActiveSight;
            var shouldHide = sight != null && sight.IsMagnifiedSight && aimBlend >= 0.94f;
            if (shouldHide == weaponHiddenForScope)
                return;
            if (!shouldHide)
            {
                RestoreWeaponAfterScope();
                return;
            }

            opticHiddenRenderers.Clear();
            opticRendererStates.Clear();
            var renderers = weapon.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null
                    || (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                    continue;
                opticHiddenRenderers.Add(renderer);
                opticRendererStates.Add(renderer.enabled);
                renderer.enabled = false;
            }
            weaponHiddenForScope = true;
        }

        private void RestoreWeaponAfterScope()
        {
            for (var index = 0; index < opticHiddenRenderers.Count; index++)
            {
                if (opticHiddenRenderers[index] != null)
                    opticHiddenRenderers[index].enabled = opticRendererStates[index];
            }
            opticHiddenRenderers.Clear();
            opticRendererStates.Clear();
            weaponHiddenForScope = false;
        }

        private void OnGUI()
        {
            var sight = weapon == null ? null : weapon.ActiveSight;
            if (sight == null || aimBlend <= 0.65f)
                return;

            var opacity = Mathf.InverseLerp(0.65f, 0.94f, aimBlend);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var previousColor = GUI.color;
            if (sight.IsReflexSight)
            {
                GUI.color = new Color(0f, 0f, 0f, opacity * 0.8f);
                GUI.DrawTexture(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.12f, 0.04f, opacity);
                GUI.DrawTexture(new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f), Texture2D.whiteTexture);
                GUI.color = previousColor;
                return;
            }

            if (!sight.IsMagnifiedSight)
                return;
            EnsureScopeMask();
            var diameter = Mathf.Min(Screen.width, Screen.height);
            var scopeRect = new Rect(
                center.x - diameter * 0.5f,
                center.y - diameter * 0.5f,
                diameter,
                diameter);
            GUI.color = new Color(1f, 1f, 1f, opacity);
            GUI.DrawTexture(scopeRect, scopeMask, ScaleMode.StretchToFill, true);
            GUI.color = new Color(0f, 0f, 0f, opacity);
            GUI.DrawTexture(new Rect(0f, 0f, scopeRect.xMin, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(scopeRect.xMax, 0f, Screen.width - scopeRect.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(scopeRect.xMin, 0f, scopeRect.width, Mathf.Max(0f, scopeRect.yMin)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(scopeRect.xMin, scopeRect.yMax, scopeRect.width,
                Mathf.Max(0f, Screen.height - scopeRect.yMax)), Texture2D.whiteTexture);

            var lineLength = diameter * 0.19f;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, opacity * 0.9f);
            GUI.DrawTexture(new Rect(center.x - lineLength, center.y - 0.5f, lineLength - 8f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x + 8f, center.y - 0.5f, lineLength - 8f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 0.5f, center.y - lineLength, 1f, lineLength - 8f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 0.5f, center.y + 8f, 1f, lineLength - 8f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.Label(
                new Rect(center.x - 45f, scopeRect.yMax - 42f, 90f, 24f),
                $"{sight.CurrentMagnification:0.#}x");
        }

        private void EnsureScopeMask()
        {
            if (scopeMask != null)
                return;
            const int size = 256;
            scopeMask = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "ArmoryCore Scope Mask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var normalized = new Vector2(
                    (x + 0.5f) / size * 2f - 1f,
                    (y + 0.5f) / size * 2f - 1f);
                var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.98f, normalized.magnitude));
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
            scopeMask.SetPixels(pixels);
            scopeMask.Apply(false, true);
        }

        private Vector3 ResolveFallbackAimPoint()
        {
            if (modelParts != null
                && modelParts.DefaultScope != null
                && modelParts.DefaultScope.gameObject.activeInHierarchy
                && TryGetRendererBoundsInRig(modelParts.DefaultScope, out var scopeBounds))
            {
                var scopePoint = scopeBounds.center;
                scopePoint.y = Mathf.Lerp(scopeBounds.min.y, scopeBounds.max.y, 0.58f);
                return scopePoint;
            }

            var muzzlePosition = weapon.Muzzle == null
                ? transform.InverseTransformPoint(weapon.transform.position)
                : transform.InverseTransformPoint(weapon.Muzzle.position);
            if (TryGetRendererBoundsInRig(weapon.transform, out var weaponBounds))
                muzzlePosition.y = Mathf.Lerp(muzzlePosition.y, weaponBounds.max.y, 0.88f);
            return muzzlePosition;
        }

        private bool TryGetRendererBoundsInRig(Transform root, out Bounds localBounds)
        {
            localBounds = default;
            if (root == null)
                return false;
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            var foundBounds = false;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;
                var bounds = renderer.localBounds;
                var min = bounds.min;
                var max = bounds.max;
                for (var corner = 0; corner < 8; corner++)
                {
                    var rendererCorner = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    var localCorner = transform.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererCorner));
                    if (!foundBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        foundBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
            return foundBounds;
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            euler.x = Mathf.DeltaAngle(0f, euler.x);
            euler.y = Mathf.DeltaAngle(0f, euler.y);
            euler.z = Mathf.DeltaAngle(0f, euler.z);
            return euler;
        }

        private void BeginReloadAnimation()
        {
            reloadStartTime = Time.time;
            reloadWasEmpty = weapon.RoundsInMagazine == 0 && !weapon.ChamberLoaded;
            if (weapon.Definition != null
                && (weapon.Definition.reloadType == ArmoryCoreReloadType.InternalMagazine
                    || weapon.Definition.reloadType == ArmoryCoreReloadType.SingleRound))
            {
                var missing = Mathf.Max(1, weapon.Definition.magazineCapacity - weapon.RoundsInMagazine);
                reloadDuration = Mathf.Max(0.45f, weapon.Definition.perRoundReloadTime * missing);
            }
            else
            {
                reloadDuration = Mathf.Max(0.6f,
                    reloadWasEmpty ? weapon.Stats.EmptyReloadSeconds : weapon.Stats.TacticalReloadSeconds);
            }
        }

        private void CalculateReloadPose(
            float progress,
            bool isReloading,
            out Vector3 position,
            out Vector3 euler)
        {
            position = Vector3.zero;
            euler = Vector3.zero;
            if (weapon.Definition == null)
                return;

            var internalReload = weapon.Definition.reloadType == ArmoryCoreReloadType.InternalMagazine
                || weapon.Definition.reloadType == ArmoryCoreReloadType.SingleRound;
            if (internalReload)
            {
                var pulseTime = Mathf.Max(0f, Time.time - reloadStartTime)
                    / Mathf.Max(0.2f, weapon.Definition.perRoundReloadTime);
                var insertPulse = Mathf.Sin((pulseTime - Mathf.Floor(pulseTime)) * Mathf.PI);
                position = new Vector3(-0.055f, 0.08f + insertPulse * 0.01f, 0.035f);
                euler = new Vector3(5f + insertPulse * 3f, -6f, -10f - insertPulse * 3f);
                return;
            }

            var lowerIn = SmoothStep01(Mathf.InverseLerp(0f, 0.16f, progress));
            var raiseOut = 1f - SmoothStep01(Mathf.InverseLerp(0.8f, 1f, progress));
            var heldLow = Mathf.Min(lowerIn, raiseOut);
            var magazineTap = Mathf.Sin(Mathf.InverseLerp(0.15f, 0.3f, progress) * Mathf.PI)
                * (progress >= 0.15f && progress <= 0.3f ? 1f : 0f);
            var actionPulse = reloadWasEmpty && progress >= 0.76f && progress <= 0.93f
                ? Mathf.Sin(Mathf.InverseLerp(0.76f, 0.93f, progress) * Mathf.PI)
                : 0f;
            position = new Vector3(
                -0.06f * heldLow,
                (0.095f * heldLow) + magazineTap * 0.006f,
                (0.045f * heldLow) - actionPulse * 0.012f);
            euler = new Vector3(
                6f * heldLow + actionPulse * 4f,
                -6f * heldLow,
                -10f * heldLow - magazineTap * 2f);
        }

        private void AnimateMagazine(float progress, bool isReloading)
        {
            if (magazine == null)
                return;
            var definition = weapon.Definition;
            var detachable = definition != null && definition.reloadType == ArmoryCoreReloadType.DetachableMagazine;
            if (!isReloading || !detachable)
            {
                magazine.localPosition = magazineBaseLocalPosition;
                magazine.localRotation = magazineBaseLocalRotation;
                return;
            }

            float removedAmount;
            if (progress < 0.22f)
                removedAmount = 0f;
            else if (progress < 0.4f)
                removedAmount = SmoothStep01(Mathf.InverseLerp(0.22f, 0.4f, progress));
            else if (progress < 0.56f)
                removedAmount = 1f;
            else if (progress < 0.78f)
                removedAmount = 1f - SmoothStep01(Mathf.InverseLerp(0.56f, 0.78f, progress));
            else
                removedAmount = 0f;

            var baseWorldPosition = magazine.parent.TransformPoint(magazineBaseLocalPosition);
            var baseWorldRotation = magazine.parent.rotation * magazineBaseLocalRotation;
            var cameraOffset = new Vector3(0.025f, -0.115f, -0.018f) * removedAmount;
            magazine.SetPositionAndRotation(
                baseWorldPosition + viewCamera.transform.TransformVector(cameraOffset),
                Quaternion.AngleAxis(-18f * removedAmount, viewCamera.transform.forward) * baseWorldRotation);
        }

        private void AnimateActionPart()
        {
            if (weapon == null || weapon.Definition == null)
                return;

            var definition = weapon.Definition;
            var manualAction = definition.action == ArmoryCoreWeaponAction.Pump
                || definition.action == ArmoryCoreWeaponAction.Manual
                || definition.action == ArmoryCoreWeaponAction.BreakAction;
            var isCycling = manualAction && weapon.State == ArmoryCoreWeaponState.Cycling;
            if (isCycling && !wasCycling)
            {
                pumpCycleStartTime = Time.time;
                pumpCycleDuration = Mathf.Max(0.2f, definition.actionCycleTime);
            }

            var shotAmount = CalculateShotActionAmount();
            var automaticAction = !manualAction ? shotAmount : 0f;
            var manualAmount = 0f;
            if (isCycling)
            {
                var progress = Mathf.Clamp01(
                    (Time.time - pumpCycleStartTime) / Mathf.Max(0.1f, pumpCycleDuration));
                if (definition.action == ArmoryCoreWeaponAction.Pump)
                {
                    if (progress < 0.1f)
                        manualAmount = 0f;
                    else if (progress < 0.43f)
                        manualAmount = SmoothStep01(Mathf.InverseLerp(0.1f, 0.43f, progress));
                    else if (progress < 0.56f)
                        manualAmount = 1f;
                    else
                        manualAmount = 1f - SmoothStep01(Mathf.InverseLerp(0.56f, 0.96f, progress));
                }
                else
                {
                    if (progress < 0.08f)
                        manualAmount = 0f;
                    else if (progress < 0.38f)
                        manualAmount = SmoothStep01(Mathf.InverseLerp(0.08f, 0.38f, progress));
                    else if (progress < 0.52f)
                        manualAmount = 1f;
                    else if (progress < 0.92f)
                        manualAmount = 1f - SmoothStep01(Mathf.InverseLerp(0.52f, 0.92f, progress));
                }
            }

            if (pump != null)
            {
                var actionAmount = Mathf.Max(manualAmount, automaticAction);
                var travel = manualAction
                    ? (definition.action == ArmoryCoreWeaponAction.Pump ? 0.16f : 0.14f)
                    : 0.085f;
                currentPumpTravel = actionAmount * travel;
                pump.localPosition = pumpBaseLocalPosition + pumpTravelDirection * currentPumpTravel;
                pump.localRotation = pumpBaseLocalRotation;
                currentActionRotation = 0f;
            }
            else
            {
                currentPumpTravel = 0f;
                currentActionRotation = 0f;
            }

            if (slide != null)
            {
                currentSlideTravel = shotAmount * 0.095f;
                slide.localPosition = slideBaseLocalPosition + slideTravelDirection * currentSlideTravel;
                slide.localRotation = slideBaseLocalRotation;
            }
            else
            {
                currentSlideTravel = 0f;
            }

            wasCycling = isCycling;
        }

        private float CalculateShotActionAmount()
        {
            if (float.IsNegativeInfinity(shotActionStartTime))
                return 0f;
            var progress = (Time.time - shotActionStartTime) / Mathf.Max(0.05f, shotActionDuration);
            if (progress < 0f || progress >= 1f)
                return 0f;
            if (progress < 0.24f)
                return SmoothStep01(progress / 0.24f);
            if (progress < 0.42f)
                return 1f;
            return 1f - SmoothStep01(Mathf.InverseLerp(0.42f, 1f, progress));
        }

        private void FindAnimatedParts()
        {
            magazine = null;
            pump = null;
            slide = null;
            if (weapon == null)
                return;
            modelParts = weapon.GetComponent<ArmoryCoreWeaponModelParts>();
            if (modelParts != null)
            {
                modelParts.Rebind(false);
                magazine = modelParts.Magazine;
                pump = modelParts.Action;
                slide = modelParts.Slide;
            }
            var transforms = weapon.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                var candidate = transforms[index];
                if (magazine == null
                    && string.Equals(candidate.name, "ammoModel", System.StringComparison.OrdinalIgnoreCase))
                {
                    magazine = candidate;
                    magazineBaseLocalPosition = magazine.localPosition;
                    magazineBaseLocalRotation = magazine.localRotation;
                }
                else if (pump == null
                    && string.Equals(candidate.name, "pumpModel", System.StringComparison.OrdinalIgnoreCase))
                {
                    pump = candidate;
                    pumpBaseLocalPosition = pump.localPosition;
                    pumpBaseLocalRotation = pump.localRotation;
                }
                else if (slide == null
                    && string.Equals(candidate.name, "slideModel", System.StringComparison.OrdinalIgnoreCase))
                {
                    slide = candidate;
                    slideBaseLocalPosition = slide.localPosition;
                    slideBaseLocalRotation = slide.localRotation;
                }
            }
            if (magazine != null)
            {
                magazineBaseLocalPosition = magazine.localPosition;
                magazineBaseLocalRotation = magazine.localRotation;
            }
            if (pump != null)
            {
                pumpBaseLocalPosition = pump.localPosition;
                pumpBaseLocalRotation = pump.localRotation;
            }
            if (slide != null)
            {
                slideBaseLocalPosition = slide.localPosition;
                slideBaseLocalRotation = slide.localRotation;
            }
            if (pump != null && pump.parent != null && weapon.Muzzle != null)
                pumpTravelDirection = pump.parent.InverseTransformDirection(-weapon.Muzzle.forward).normalized;
            if (slide != null && slide.parent != null && weapon.Muzzle != null)
                slideTravelDirection = slide.parent.InverseTransformDirection(-weapon.Muzzle.forward).normalized;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private float CalculateObstruction()
        {
            var origin = viewCamera.transform.position;
            var direction = viewCamera.transform.forward;
            var count = Physics.SphereCastNonAlloc(
                origin,
                collisionProbeRadius,
                direction,
                collisionHits,
                collisionProbeDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            var nearest = collisionProbeDistance;
            for (var index = 0; index < count; index++)
            {
                var hitTransform = collisionHits[index].transform;
                if (hitTransform == null || hitTransform.IsChildOf(viewCamera.transform))
                    continue;
                nearest = Mathf.Min(nearest, collisionHits[index].distance);
            }
            return 1f - Mathf.InverseLerp(0.18f, collisionProbeDistance, nearest);
        }

        private void OnRecoil(Vector2 impulse)
        {
            shotActionStartTime = Time.time;
            var roundsPerMinute = weapon?.Definition == null
                ? 600f
                : Mathf.Max(60f, weapon.Definition.roundsPerMinute);
            shotActionDuration = Mathf.Clamp((60f / roundsPerMinute) * 0.72f, 0.075f, 0.16f);

            var mass = Mathf.Max(0.65f, weapon == null ? 3f : weapon.Stats.MassKilograms);
            var massFactor = Mathf.Clamp(2.8f / mass, 0.72f, 1.28f);
            var stanceFactor = Mathf.Lerp(1f, 0.82f, aimBlend);
            var vertical = Mathf.Clamp(impulse.y, 0f, 3.5f);
            var horizontal = Mathf.Clamp(impulse.x, -1.5f, 1.5f);
            var response = massFactor * stanceFactor;

            recoilPositionVelocity += new Vector3(
                horizontal * 0.048f,
                -vertical * 0.026f,
                -vertical * 0.48f) * response;
            recoilRotationVelocity += new Vector3(
                -vertical * 32f,
                horizontal * 22f,
                -horizontal * 9f) * response;
            recoilPositionVelocity = Vector3.ClampMagnitude(recoilPositionVelocity, 1.65f);
            recoilRotationVelocity = Vector3.ClampMagnitude(recoilRotationVelocity, 120f);
        }

        private void UpdateRecoilSpring(float deltaTime)
        {
            var step = Mathf.Min(deltaTime, 0.05f);
            var positionStiffness = recoilSharpness * 9.5f;
            var positionDamping = 2f * Mathf.Sqrt(positionStiffness) * 0.92f;
            recoilPositionVelocity += (
                -recoilPosition * positionStiffness
                - recoilPositionVelocity * positionDamping) * step;
            recoilPosition += recoilPositionVelocity * step;

            var rotationStiffness = recoilSharpness * 11f;
            var rotationDamping = 2f * Mathf.Sqrt(rotationStiffness) * 0.88f;
            recoilRotationVelocity += (
                -recoilRotation * rotationStiffness
                - recoilRotationVelocity * rotationDamping) * step;
            recoilRotation += recoilRotationVelocity * step;

            recoilPosition = Vector3.ClampMagnitude(recoilPosition, 0.055f);
            recoilRotation = Vector3.ClampMagnitude(recoilRotation, 5.5f);
            if (recoilPosition.sqrMagnitude < 0.00000001f
                && recoilPositionVelocity.sqrMagnitude < 0.000001f)
            {
                recoilPosition = Vector3.zero;
                recoilPositionVelocity = Vector3.zero;
            }
            if (recoilRotation.sqrMagnitude < 0.000001f
                && recoilRotationVelocity.sqrMagnitude < 0.0001f)
            {
                recoilRotation = Vector3.zero;
                recoilRotationVelocity = Vector3.zero;
            }
        }

        private void OnDestroy()
        {
            RestoreWeaponAfterScope();
            if (weapon != null)
                weapon.RecoilApplied -= OnRecoil;
            if (scopeMask != null)
                Destroy(scopeMask);
        }

        private void OnDisable()
        {
            RestoreWeaponAfterScope();
        }
    }
}
