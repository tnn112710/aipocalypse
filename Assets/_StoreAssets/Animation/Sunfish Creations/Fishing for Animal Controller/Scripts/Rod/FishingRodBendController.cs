using SunfishCreations.Animations;
using System;
using System.Collections;
using UnityEngine;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/rod-bend-system")]
    public class FishingRodBendController : MonoBehaviour, IRodBender
    {
        [Header("Rod Bones Setup")]
        [Tooltip("Array of bone transforms from Maya - arrange from handle to tip")]
        public Transform[] rodBones;

        [Header("PREVIEW SYSTEM")]
        [Tooltip("Enable live preview mode for easy configuration")]
        public bool enablePreview = false;

        [Space(5)]
        [Tooltip("Preview different bend types")]
        public PreviewType previewType = PreviewType.Windup;

        [Space(5)]
        [Tooltip("Preview bend amount (0 = neutral, 1 = full bend)")]
        [Range(0f, 1f)]
        public float previewAmount = 0f;

        [Space(5)]
        [Tooltip("Live preview shows the actual angle value for the selected bend type")]
        [SerializeField]
        private float currentPreviewAngle = 0f;

        [Header("Cast Bending Settings")]
        [Tooltip("Maximum angle the rod bends backward during windup")]
        [Range(10f, 60f)]
        public float maxWindupBend = 30f;

        [Tooltip("Maximum angle the rod bends forward during release")]
        [Range(15f, 90f)]
        public float maxReleaseBend = 45f;

        [Tooltip("How fast rod bends back during windup")]
        [Range(0.1f, 10f)]
        public float windupSpeed = 2f;

        [Tooltip("How fast rod snaps forward during release")]
        [Range(0.1f, 15f)]
        public float releaseSpeed = 8f;

        [Tooltip("How fast rod returns to neutral position")]
        [Range(0.1f, 10f)]
        public float returnSpeed = 4f;

        [Tooltip("Pause duration at full windup before release")]
        [Range(0f, 1f)]
        public float windupPauseDuration = 0.1f;

        [Header("Post-Cast Bend Settings")]
        [Tooltip("Enable keeping rod slightly bent after cast")]
        public bool enablePostCastBend = true;

        [Tooltip("Angle to maintain after cast (forward bend for line tension)")]
        [Range(0f, 30f)]
        public float postCastBendAngle = 8f;

        [Tooltip("How fast rod settles to post-cast position")]
        [Range(0.1f, 10f)]
        public float postCastSettleSpeed = 2f;

        [Tooltip("Duration to hold post-cast bend (0 = hold indefinitely until next action)")]
        [Range(0f, 10f)]
        public float postCastHoldDuration = 0f;

        [Tooltip("Automatically return to neutral after hold duration")]
        public bool autoReturnFromPostCast = false;

        [Header("Cast Parameters")]
        [Tooltip("Multiplier for bend angles - higher = more dramatic bending")]
        [Range(0.1f, 3f)]
        public float castStrength = 1f;

        [Tooltip("Multiplier for animation speeds - higher = faster casting")]
        [Range(0.1f, 10f)]
        public float castSpeed = 5f;

        [Header("Pull Back Settings")]
        [Tooltip("Angle rod bends forward when pulling fish")]
        [Range(15f, 60f)]
        public float pullBackBendAngle = 35f;

        [Tooltip("How fast rod bends forward during pull")]
        [Range(0.1f, 10f)]
        public float pullBackSpeed = 1.5f;

        [Tooltip("How long to hold the forward bend")]
        [Range(0.1f, 3f)]
        public float pullBackHoldDuration = 1f;

        [Tooltip("How fast rod returns from pull back")]
        [Range(0.1f, 10f)]
        public float pullBackReturnSpeed = 2f;

        [Header("Bite Settings")]
        [Tooltip("Angle to go after the fish is biting (forward bend for line tension)")]
        [Range(0f, 60f)]
        public float hardBiteBendAngle = 45f;

        [Tooltip("How fast rod bends to hard bite angle")]
        [Range(0.1f, 10f)]
        public float hardBiteSpeed = 3f;

        [Header("Bend Distribution")]
        [Tooltip("Curve defining how bend spreads along rod (0=handle, 1=tip)")]
        public AnimationCurve bendDistribution = AnimationCurve.EaseInOut(0, 0.1f, 1, 1f);

        [Header("Rotation Axis")]
        [Tooltip("Which axis to rotate for bending (typically Y-axis for horizontal rods)")]
        public Vector3 bendAxis = Vector3.up;

        public enum PreviewType
        {
            Windup,
            Release,
            PostCast,
            PullBack,
            HardBite
        }

        protected bool _waterHit;
        protected Vector3[] originalRotations;
        protected bool isCasting = false;
        protected bool isInPostCastBend = false;
        protected Coroutine postCastCoroutine;

        protected bool wasPreviewEnabled = false;
        protected PreviewType lastPreviewType;
        protected float lastPreviewAmount;
        protected float lastWindupBend, lastReleaseBend, lastPostCastBend, lastPullBackBend, lastHardBiteBend;
        protected float lastCastStrength;

        public virtual void InformWaterHit(bool value)
        {
            _waterHit = value;
        }

        protected virtual void Start()
        {
            originalRotations = new Vector3[rodBones.Length];
            for (int i = 0; i < rodBones.Length; i++)
            {
                originalRotations[i] = rodBones[i].localEulerAngles;
            }

            wasPreviewEnabled = enablePreview;
            lastPreviewType = previewType;
            lastPreviewAmount = previewAmount;
            UpdateLastValues();
        }

        protected virtual void Update()
        {
            HandlePreviewSystem();
        }

        protected virtual void HandlePreviewSystem()
        {
            bool previewChanged = enablePreview != wasPreviewEnabled ||
                                previewType != lastPreviewType ||
                                Mathf.Abs(previewAmount - lastPreviewAmount) > 0.001f ||
                                ParametersChanged();

            if (enablePreview && !isCasting)
            {
                if (previewChanged)
                {
                    ApplyPreview();
                }
            }
            else if (wasPreviewEnabled && !enablePreview)
            {
                ResetToOriginalRotations();
            }

            wasPreviewEnabled = enablePreview;
            lastPreviewType = previewType;
            lastPreviewAmount = previewAmount;
            UpdateLastValues();
        }

        protected virtual bool ParametersChanged()
        {
            return Mathf.Abs(maxWindupBend - lastWindupBend) > 0.1f ||
                   Mathf.Abs(maxReleaseBend - lastReleaseBend) > 0.1f ||
                   Mathf.Abs(postCastBendAngle - lastPostCastBend) > 0.1f ||
                   Mathf.Abs(pullBackBendAngle - lastPullBackBend) > 0.1f ||
                   Mathf.Abs(hardBiteBendAngle - lastHardBiteBend) > 0.1f ||
                   Mathf.Abs(castStrength - lastCastStrength) > 0.01f;
        }

        protected virtual void UpdateLastValues()
        {
            lastWindupBend = maxWindupBend;
            lastReleaseBend = maxReleaseBend;
            lastPostCastBend = postCastBendAngle;
            lastPullBackBend = pullBackBendAngle;
            lastHardBiteBend = hardBiteBendAngle;
            lastCastStrength = castStrength;
        }

        protected virtual void ApplyPreview()
        {
            if (rodBones.Length == 0) return;

            float targetAngle = 0f;

            switch (previewType)
            {
                case PreviewType.Windup:
                    targetAngle = -maxWindupBend * castStrength * previewAmount;
                    break;
                case PreviewType.Release:
                    targetAngle = maxReleaseBend * castStrength * previewAmount;
                    break;
                case PreviewType.PostCast:
                    targetAngle = postCastBendAngle * previewAmount;
                    break;
                case PreviewType.PullBack:
                    targetAngle = pullBackBendAngle * previewAmount;
                    break;
                case PreviewType.HardBite:
                    targetAngle = hardBiteBendAngle * previewAmount;
                    break;
            }

            currentPreviewAngle = targetAngle;

            ApplyBendToRod(targetAngle);
        }

        [ContextMenu("Test Windup Bend")]
        public virtual void TestWindupBend()
        {
            if (!Application.isPlaying) return;
            StartCoroutine(TestBendSequence(-maxWindupBend * castStrength));
        }

        [ContextMenu("Test Release Bend")]
        public virtual void TestReleaseBend()
        {
            if (!Application.isPlaying) return;
            StartCoroutine(TestBendSequence(maxReleaseBend * castStrength));
        }

        [ContextMenu("Test Post Cast Bend")]
        public virtual void TestPostCastBend()
        {
            if (!Application.isPlaying) return;
            StartCoroutine(TestBendSequence(postCastBendAngle));
        }

        [ContextMenu("Reset to Neutral")]
        public virtual void TestResetToNeutral()
        {
            if (!Application.isPlaying) return;
            ResetToOriginalRotations();
        }

        protected virtual IEnumerator TestBendSequence(float targetAngle)
        {
            if (isCasting) yield break;

            bool wasPreview = enablePreview;
            enablePreview = false;

            float currentBend = GetCurrentBend();
            float duration = 1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float bendAngle = Mathf.Lerp(currentBend, targetAngle, t);
                ApplyBendToRod(bendAngle);
                yield return null;
            }

            yield return new WaitForSeconds(1f);

            elapsed = 0f;
            currentBend = GetCurrentBend();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float bendAngle = Mathf.Lerp(currentBend, 0f, t);
                ApplyBendToRod(bendAngle);
                yield return null;
            }

            ResetToOriginalRotations();

            enablePreview = wasPreview;
        }

        public virtual void StartCastBend()
        {
            if (!isCasting)
            {
                bool wasPreview = enablePreview;
                enablePreview = false;

                StartCoroutine(CastBendSequenceWithPreviewRestore(wasPreview));
            }
        }

        protected virtual IEnumerator CastBendSequenceWithPreviewRestore(bool restorePreview, float? customStrength = null, float? customSpeed = null)
        {
            yield return StartCoroutine(CastBendSequence(customStrength, customSpeed));
            enablePreview = restorePreview;
        }

        public virtual void StartCastBend(float customStrength, float customSpeed)
        {
            if (!isCasting)
            {
                bool wasPreview = enablePreview;
                enablePreview = false;
                StartCoroutine(CastBendSequenceWithPreviewRestore(wasPreview, customStrength, customSpeed));
            }
        }

        public virtual void StartWindupOnly()
        {
            DisablePreviewTemporarily();
            StartCoroutine(BendToAngle(-maxWindupBend * castStrength, windupSpeed * castSpeed));
        }

        public virtual void StartReleaseOnly()
        {
            DisablePreviewTemporarily();
            StartCoroutine(BendToAngle(maxReleaseBend * castStrength, releaseSpeed * castSpeed));
        }

        public virtual void ReturnToNeutralPosition(float delay = 0)
        {
            if (postCastCoroutine != null)
            {
                StopCoroutine(postCastCoroutine);
                postCastCoroutine = null;
            }
            isInPostCastBend = false;

            DisablePreviewTemporarily();
            StartCoroutine(ReturnToNeutralCoroutine(delay));
        }

        protected virtual void DisablePreviewTemporarily()
        {
            if (enablePreview)
            {
                enablePreview = false;
            }
        }

        public virtual void SetPostCastBend(float bendAngle)
        {
            if (!isCasting)
            {
                if (postCastCoroutine != null)
                {
                    StopCoroutine(postCastCoroutine);
                }
                DisablePreviewTemporarily();
                postCastCoroutine = StartCoroutine(TransitionToPostCastBend(bendAngle));
            }
        }

        public virtual void StartHardBiteBend()
        {
            if (!isCasting)
            {
                DisablePreviewTemporarily();
                StartCoroutine(SimpleHardBite());
            }
        }

        public virtual void StartPullBackBend()
        {
            if (!isCasting)
            {
                if (postCastCoroutine != null)
                {
                    StopCoroutine(postCastCoroutine);
                    postCastCoroutine = null;
                }
                isInPostCastBend = false;

                DisablePreviewTemporarily();
                StartCoroutine(PullBackSequence());
            }
        }

        public virtual void StartPullBackBendCustom(float customAngle, float customSpeed, float customHoldTime)
        {
            if (!isCasting)
            {
                if (postCastCoroutine != null)
                {
                    StopCoroutine(postCastCoroutine);
                    postCastCoroutine = null;
                }
                isInPostCastBend = false;

                DisablePreviewTemporarily();
                StartCoroutine(PullBackSequence(customAngle, customSpeed, customHoldTime));
            }
        }

        protected virtual IEnumerator CastBendSequence(float? customStrength = null, float? customSpeed = null)
        {
            isCasting = true;

            if (postCastCoroutine != null)
            {
                StopCoroutine(postCastCoroutine);
                postCastCoroutine = null;
            }
            isInPostCastBend = false;

            float strength = customStrength ?? castStrength;
            float speed = customSpeed ?? castSpeed;

            float currentBend = 0f;
            float targetBend = -maxWindupBend * strength;

            while (Mathf.Abs(currentBend) < Mathf.Abs(targetBend))
            {
                currentBend = Mathf.MoveTowards(currentBend, targetBend, windupSpeed * speed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            yield return new WaitForSeconds(windupPauseDuration);

            targetBend = maxReleaseBend * strength;
            while (Mathf.Abs(currentBend - targetBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, targetBend, releaseSpeed * speed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            float overshoot = targetBend * 0.2f;
            ApplyBendToRod(targetBend + overshoot);
            yield return new WaitForSeconds(0.05f);

            float finalTarget = enablePostCastBend ? postCastBendAngle : 0f;

            while (Mathf.Abs(currentBend - finalTarget) > 0.1f)
            {
                float moveSpeed = (enablePostCastBend ? postCastSettleSpeed * speed : returnSpeed * speed) * Time.deltaTime * 60f;
                currentBend = Mathf.MoveTowards(currentBend, finalTarget, moveSpeed);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            if (!enablePostCastBend || !_waterHit)
            {
                ResetToOriginalRotations();
            }
            else
            {
                isInPostCastBend = true;

                if (postCastHoldDuration > 0f && autoReturnFromPostCast)
                {
                    postCastCoroutine = StartCoroutine(PostCastHoldTimer());
                }
            }

            isCasting = false;
        }

        protected virtual IEnumerator PostCastHoldTimer()
        {
            yield return new WaitForSeconds(postCastHoldDuration);

            if (isInPostCastBend && autoReturnFromPostCast)
            {
                yield return StartCoroutine(TransitionFromPostCastToNeutral());
            }
        }

        protected virtual IEnumerator TransitionToPostCastBend(float targetBend)
        {
            float currentBend = GetCurrentBend();

            while (Mathf.Abs(currentBend - targetBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, targetBend, postCastSettleSpeed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            isInPostCastBend = true;

            if (postCastHoldDuration > 0f && autoReturnFromPostCast)
            {
                yield return new WaitForSeconds(postCastHoldDuration);
                if (isInPostCastBend && autoReturnFromPostCast)
                {
                    yield return StartCoroutine(TransitionFromPostCastToNeutral());
                }
            }
        }

        protected virtual IEnumerator TransitionFromPostCastToNeutral()
        {
            float currentBend = GetCurrentBend();

            while (Mathf.Abs(currentBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, 0f, returnSpeed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            ResetToOriginalRotations();
            isInPostCastBend = false;
        }

        protected virtual IEnumerator SimpleHardBite()
        {
            float currentBend = GetCurrentBend();
            float targetBend = hardBiteBendAngle;

            while (Mathf.Abs(currentBend - targetBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, targetBend, hardBiteSpeed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            isInPostCastBend = true;
        }

        protected virtual IEnumerator PullBackSequence(float? customAngle = null, float? customSpeed = null, float? customHoldTime = null)
        {
            isCasting = true;

            float bendAngle = customAngle ?? pullBackBendAngle;
            float speed = customSpeed ?? pullBackSpeed;
            float holdTime = customHoldTime ?? pullBackHoldDuration;

            float currentBend = GetCurrentBend();
            float targetBend = bendAngle;

            while (Mathf.Abs(currentBend - targetBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, targetBend, speed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            yield return new WaitForSeconds(holdTime);

            float finalTarget = 0f;

            while (Mathf.Abs(currentBend - finalTarget) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, finalTarget, pullBackReturnSpeed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            ResetToOriginalRotations();
            isInPostCastBend = false;

            isCasting = false;
        }

        protected virtual IEnumerator BendToAngle(float targetAngle, float speed)
        {
            float currentBend = GetCurrentBend();

            while (Mathf.Abs(currentBend - targetAngle) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, targetAngle, speed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }
        }

        protected virtual IEnumerator ReturnToNeutralCoroutine(float delay = 0)
        {
            yield return new WaitForSeconds(delay);

            float currentBend = GetCurrentBend();

            while (Mathf.Abs(currentBend) > 0.1f)
            {
                currentBend = Mathf.MoveTowards(currentBend, 0f, returnSpeed * castSpeed * Time.deltaTime * 60f);
                ApplyBendToRod(currentBend);
                yield return null;
            }

            ResetToOriginalRotations();
        }

        protected virtual void ApplyBendToRod(float bendAngle)
        {
            for (int i = 0; i < rodBones.Length; i++)
            {
                float normalizedPosition = (float)i / (rodBones.Length - 1);
                float boneInfluence = bendDistribution.Evaluate(normalizedPosition);
                float boneBend = bendAngle * boneInfluence;

                Vector3 newRotation = originalRotations[i];

                if (bendAxis.y > 0.5f)
                    newRotation.y += boneBend;
                else if (bendAxis.x > 0.5f)
                    newRotation.x += boneBend;
                else if (bendAxis.z > 0.5f)
                    newRotation.z += boneBend;

                rodBones[i].localEulerAngles = newRotation;
            }
        }

        protected virtual float GetCurrentBend()
        {
            if (rodBones.Length == 0) return 0f;

            Vector3 currentRot = rodBones[rodBones.Length - 1].localEulerAngles;
            Vector3 originalRot = originalRotations[rodBones.Length - 1];

            if (bendAxis.y > 0.5f)
                return Mathf.DeltaAngle(originalRot.y, currentRot.y);
            else if (bendAxis.x > 0.5f)
                return Mathf.DeltaAngle(originalRot.x, currentRot.x);
            else if (bendAxis.z > 0.5f)
                return Mathf.DeltaAngle(originalRot.z, currentRot.z);

            return 0f;
        }

        protected virtual void ResetToOriginalRotations()
        {
            for (int i = 0; i < rodBones.Length; i++)
            {
                rodBones[i].localEulerAngles = originalRotations[i];
            }
        }

        public virtual void HandleFishingStateExit()
        {
            ForceResetToNeutral();
        }

        public virtual void ForceResetToNeutral()
        {
            StopAllCoroutines();

            isCasting = false;
            isInPostCastBend = false;
            postCastCoroutine = null;

            ResetToOriginalRotations();

            enablePreview = false;
        }

        public virtual bool IsCasting()
        {
            return isCasting;
        }

        public virtual bool IsInPostCastBend()
        {
            return isInPostCastBend;
        }

        public virtual void ApplyFishPull(Vector3 fishDirection, float pullStrength)
        {
            if (isCasting) return;

            float bendAngle = pullStrength * 25f;

            ApplyBendToRod(bendAngle);
        }

        void IRodBender.CastBend()
        {
            StartCastBend();
        }

        void IRodBender.PullbackBend()
        {
            StartPullBackBend();
        }

        void IRodBender.HardBiteBend()
        {
            StartHardBiteBend();
        }
    }
}