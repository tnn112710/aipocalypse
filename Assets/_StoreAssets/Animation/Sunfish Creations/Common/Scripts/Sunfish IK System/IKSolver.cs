using System.Collections;
using UnityEngine;

namespace SunfishCreations.IK
{
    /// <summary>
    /// Placed on the character. Applies hand and foot IK in LateUpdate (order 2000),
    /// running after the ParentConstraint and Malbers IKManager (order 1500).
    /// Caches post-animation bone rotations after each FixedUpdate and restores them
    /// every LateUpdate for idempotent solves. Uses character-orientation-derived
    /// pole vectors to avoid degenerate cross-product flips.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    public class IKSolver : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("Per-hand multiplier for the left hand. Set to 0 to disable left hand IK independently.")]
        [SerializeField] protected float leftHandWeight = 1f;

        [Range(0f, 1f)]
        [Tooltip("Per-hand multiplier for the right hand. Set to 0 to disable right hand IK independently.")]
        [SerializeField] protected float rightHandWeight = 1f;

        [Range(0f, 1f)]
        [SerializeField] protected float footIKWeight = 1f;

        public void SetLeftHandWeight(float weight) => leftHandWeight = weight;
        public float GetLeftHandWeight() => leftHandWeight;
        public void SetRightHandWeight(float weight) => rightHandWeight = weight;
        public float GetRightHandWeight() => rightHandWeight;

        // Runtime weights lerped toward the serialized target weights.
        private float currentLeftHandWeight = 0f;
        private float currentRightHandWeight = 0f;
        private float currentFootWeight = 0f;

        private IIKBridge activeBridge;
        private Animator animator;
        private Transform characterRoot;

        // Bone references
        private Transform leftUpperArm;
        private Transform leftForearm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightForearm;
        private Transform rightHand;
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;

        // Cached bone lengths (bind-pose)
        private float leftUpperArmLen;
        private float leftLowerArmLen;
        private float rightUpperArmLen;
        private float rightLowerArmLen;
        private float leftUpperLegLen;
        private float leftLowerLegLen;
        private float rightUpperLegLen;
        private float rightLowerLegLen;

        // Cached post-animation local rotations, refreshed after each FixedUpdate
        private Quaternion cachedLeftUpperArmRot;        private Quaternion cachedLeftForearmRot;
        private Quaternion cachedLeftHandRot;
        private Quaternion cachedRightUpperArmRot;
        private Quaternion cachedRightForearmRot;
        private Quaternion cachedRightHandRot;
        private Quaternion cachedLeftUpperLegRot;
        private Quaternion cachedLeftLowerLegRot;
        private Quaternion cachedLeftFootRot;
        private Quaternion cachedRightUpperLegRot;
        private Quaternion cachedRightLowerLegRot;
        private Quaternion cachedRightFootRot;

        private Coroutine cacheCoroutine;
        private Coroutine lerpCoroutine;

        private void Awake()
        {
            animator = GetComponentInParent<Animator>();
            characterRoot = transform;

            leftUpperArm  = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftForearm   = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand      = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightForearm  = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand     = animator.GetBoneTransform(HumanBodyBones.RightHand);

            leftUpperLeg  = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg  = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot      = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot     = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            // Measure bone lengths from the bind pose
            leftUpperArmLen  = (leftForearm.position  - leftUpperArm.position).magnitude;
            leftLowerArmLen  = (leftHand.position      - leftForearm.position).magnitude;
            rightUpperArmLen = (rightForearm.position  - rightUpperArm.position).magnitude;
            rightLowerArmLen = (rightHand.position     - rightForearm.position).magnitude;

            leftUpperLegLen  = (leftLowerLeg.position  - leftUpperLeg.position).magnitude;
            leftLowerLegLen  = (leftFoot.position       - leftLowerLeg.position).magnitude;
            rightUpperLegLen = (rightLowerLeg.position  - rightUpperLeg.position).magnitude;
            rightLowerLegLen = (rightFoot.position      - rightLowerLeg.position).magnitude;
        }

        /// <summary>Activates IK against the given bridge.</summary>
        public void Activate(IIKBridge bridge)
        {
            activeBridge = bridge;

            // Seed the cache so the first LateUpdate doesn't use zeroed quaternions.
            SnapshotRotations();
            cacheCoroutine = StartCoroutine(CacheRotationsAfterAnimation());

            if (lerpCoroutine != null)
            {
                StopCoroutine(lerpCoroutine);
                lerpCoroutine = null;
            }

            float leftTarget = leftHandWeight;
            float rightTarget = rightHandWeight;
            float lerpDuration = bridge.IKLerpDuration;
            if (lerpDuration > 0f)
            {
                lerpCoroutine = StartCoroutine(WeightLerpRoutine(
                    currentLeftHandWeight, leftTarget,
                    currentRightHandWeight, rightTarget,
                    currentFootWeight, footIKWeight, lerpDuration));
            }
            else
            {
                currentLeftHandWeight = leftTarget;
                currentRightHandWeight = rightTarget;
                currentFootWeight = footIKWeight;
            }
        }

        /// <summary>Deactivates IK, optionally lerping weights to zero.</summary>
        public void Deactivate()
        {
            if (lerpCoroutine != null)
            {
                StopCoroutine(lerpCoroutine);
                lerpCoroutine = null;
            }

            float lerpDuration = activeBridge != null ? activeBridge.IKLerpDuration : 0f;
            if (lerpDuration > 0f && activeBridge != null)
            {
                lerpCoroutine = StartCoroutine(DeactivateLerpRoutine(lerpDuration));
            }
            else
            {
                FullyDeactivate();
            }
        }

        private void FullyDeactivate()
        {
            currentLeftHandWeight = 0f;
            currentRightHandWeight = 0f;
            currentFootWeight = 0f;

            if (cacheCoroutine != null)
            {
                StopCoroutine(cacheCoroutine);
                cacheCoroutine = null;
            }

            // Restore the clean animation pose so no stale IK result leaks
            // into the frame when the Malbers system takes over again.
            RestoreRotations();

            activeBridge = null;
        }

        private IEnumerator DeactivateLerpRoutine(float duration)
        {
            float startLeftHand = currentLeftHandWeight;
            float startRightHand = currentRightHandWeight;
            float startFoot = currentFootWeight;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Min(elapsed / duration, 1f);
                currentLeftHandWeight = Mathf.Lerp(startLeftHand, 0f, t);
                currentRightHandWeight = Mathf.Lerp(startRightHand, 0f, t);
                currentFootWeight = Mathf.Lerp(startFoot, 0f, t);
                yield return null;
            }

            lerpCoroutine = null;
            FullyDeactivate();
        }

        private IEnumerator WeightLerpRoutine(
            float fromLeftHand, float toLeftHand,
            float fromRightHand, float toRightHand,
            float fromFoot, float toFoot,
            float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Min(elapsed / duration, 1f);
                currentLeftHandWeight = Mathf.Lerp(fromLeftHand, toLeftHand, t);
                currentRightHandWeight = Mathf.Lerp(fromRightHand, toRightHand, t);
                currentFootWeight = Mathf.Lerp(fromFoot, toFoot, t);
                yield return null;
            }

            currentLeftHandWeight = toLeftHand;
            currentRightHandWeight = toRightHand;
            currentFootWeight = toFoot;
            lerpCoroutine = null;
        }

        /// <summary>
        /// Snapshots post-animation bone rotations after each FixedUpdate
        /// so LateUpdate can restore them before each IK solve.
        /// </summary>
        private IEnumerator CacheRotationsAfterAnimation()
        {
            var waitFixed = new WaitForFixedUpdate();
            while (true)
            {
                yield return waitFixed;
                SnapshotRotations();
            }
        }

        private void SnapshotRotations()
        {
            cachedLeftUpperArmRot  = leftUpperArm.localRotation;
            cachedLeftForearmRot   = leftForearm.localRotation;
            cachedLeftHandRot      = leftHand.localRotation;

            cachedRightUpperArmRot = rightUpperArm.localRotation;
            cachedRightForearmRot  = rightForearm.localRotation;
            cachedRightHandRot     = rightHand.localRotation;

            if (leftUpperLeg  != null) cachedLeftUpperLegRot  = leftUpperLeg.localRotation;
            if (leftLowerLeg  != null) cachedLeftLowerLegRot  = leftLowerLeg.localRotation;
            if (rightUpperLeg != null) cachedRightUpperLegRot = rightUpperLeg.localRotation;
            if (rightLowerLeg != null) cachedRightLowerLegRot = rightLowerLeg.localRotation;
            cachedLeftFootRot  = leftFoot.localRotation;
            cachedRightFootRot = rightFoot.localRotation;
        }

        private void RestoreRotations()
        {
            leftUpperArm.localRotation  = cachedLeftUpperArmRot;
            leftForearm.localRotation   = cachedLeftForearmRot;
            leftHand.localRotation      = cachedLeftHandRot;

            rightUpperArm.localRotation = cachedRightUpperArmRot;
            rightForearm.localRotation  = cachedRightForearmRot;
            rightHand.localRotation     = cachedRightHandRot;

            if (leftUpperLeg  != null) leftUpperLeg.localRotation  = cachedLeftUpperLegRot;
            if (leftLowerLeg  != null) leftLowerLeg.localRotation  = cachedLeftLowerLegRot;
            if (rightUpperLeg != null) rightUpperLeg.localRotation = cachedRightUpperLegRot;
            if (rightLowerLeg != null) rightLowerLeg.localRotation = cachedRightLowerLegRot;
            leftFoot.localRotation  = cachedLeftFootRot;
            rightFoot.localRotation = cachedRightFootRot;
        }

        private void LateUpdate()
        {
            if (activeBridge == null) return;

            // Start from the clean animation pose for idempotency.
            RestoreRotations();

            // Track serialized target weights when no lerp coroutine is running.
            if (lerpCoroutine == null)
            {
                float catchUpSpeed = 5f * Time.deltaTime;
                float leftTarget = leftHandWeight;
                float rightTarget = rightHandWeight;
                currentLeftHandWeight = Mathf.MoveTowards(currentLeftHandWeight, leftTarget, catchUpSpeed);
                currentRightHandWeight = Mathf.MoveTowards(currentRightHandWeight, rightTarget, catchUpSpeed);
                currentFootWeight = Mathf.MoveTowards(currentFootWeight, footIKWeight, catchUpSpeed);
            }

            if (currentLeftHandWeight > 0f)
            {
                Vector3 leftPole = characterRoot.TransformPoint(
                    characterRoot.InverseTransformPoint(leftUpperArm.position) + activeBridge.LeftElbowPoleLocal);

                SolveTwoBoneIK(
                    leftUpperArm, leftForearm, leftHand,
                    leftUpperArmLen, leftLowerArmLen,
                    activeBridge.LeftHandPosition, activeBridge.LeftHandRotation,
                    leftPole, currentLeftHandWeight);
            }

            if (currentRightHandWeight > 0f)
            {
                Vector3 rightPole = characterRoot.TransformPoint(
                    characterRoot.InverseTransformPoint(rightUpperArm.position) + activeBridge.RightElbowPoleLocal);

                SolveTwoBoneIK(
                    rightUpperArm, rightForearm, rightHand,
                    rightUpperArmLen, rightLowerArmLen,
                    activeBridge.RightHandPosition, activeBridge.RightHandRotation,
                    rightPole, currentRightHandWeight);
            }

            if (currentFootWeight > 0f)
            {
                Vector3 leftKneePole  = characterRoot.TransformPoint(
                    characterRoot.InverseTransformPoint(leftUpperLeg.position) + activeBridge.LeftKneePoleLocal);
                Vector3 rightKneePole = characterRoot.TransformPoint(
                    characterRoot.InverseTransformPoint(rightUpperLeg.position) + activeBridge.RightKneePoleLocal);

                SolveTwoBoneIK(
                    leftUpperLeg, leftLowerLeg, leftFoot,
                    leftUpperLegLen, leftLowerLegLen,
                    activeBridge.LeftFootPosition, leftFoot.rotation,
                    leftKneePole, currentFootWeight);

                SolveTwoBoneIK(
                    rightUpperLeg, rightLowerLeg, rightFoot,
                    rightUpperLegLen, rightLowerLegLen,
                    activeBridge.RightFootPosition, rightFoot.rotation,
                    rightKneePole, currentFootWeight);
            }
        }

        /// <summary>
        /// Two-bone IK solve with pole-vector bend direction using pre-cached bone lengths.
        /// </summary>
        private static void SolveTwoBoneIK(
            Transform root, Transform mid, Transform tip,
            float upperLen, float lowerLen,
            Vector3 targetPos, Quaternion targetRot,
            Vector3 poleWorldPos, float weight)
        {
            Vector3 rootPos = root.position;

            // Direction and clamped distance to target
            Vector3 rootToTarget = targetPos - rootPos;
            float targetDist = rootToTarget.magnitude;
            float chainLen = upperLen + lowerLen;
            targetDist = Mathf.Clamp(targetDist,
                Mathf.Abs(upperLen - lowerLen) + 0.001f,
                chainLen - 0.001f);
            Vector3 targetDir = rootToTarget.normalized;

            // Law of cosines: angle at root
            float cosRoot = (upperLen * upperLen + targetDist * targetDist - lowerLen * lowerLen)                            / (2f * upperLen * targetDist);
            cosRoot = Mathf.Clamp(cosRoot, -1f, 1f);
            float sinRoot = Mathf.Sqrt(1f - cosRoot * cosRoot);

            // Derive bend direction from pole vector
            Vector3 rootToPole = poleWorldPos - rootPos;
            Vector3 bendDir = rootToPole - Vector3.Dot(rootToPole, targetDir) * targetDir;
            if (bendDir.sqrMagnitude < 0.0001f)
                bendDir = Vector3.Cross(targetDir, Vector3.up);
            bendDir.Normalize();

            // Desired mid (elbow) position on the bend plane
            Vector3 desiredMidPos = rootPos                + targetDir * (cosRoot * upperLen)
                + bendDir   * (sinRoot * upperLen);

            // Rotate root to swing mid toward desired position
            Vector3 currentRootToMid = mid.position - rootPos;
            Vector3 desiredRootToMid = desiredMidPos - rootPos;
            Quaternion rootRot = Quaternion.FromToRotation(currentRootToMid, desiredRootToMid);
            root.rotation = Quaternion.Slerp(Quaternion.identity, rootRot, weight) * root.rotation;

            // Rotate mid to aim tip at target
            Vector3 currentMidToTip = tip.position - mid.position;
            Vector3 desiredMidToTip = targetPos - mid.position;
            Quaternion midRot = Quaternion.FromToRotation(currentMidToTip, desiredMidToTip);
            mid.rotation = Quaternion.Slerp(Quaternion.identity, midRot, weight) * mid.rotation;

            // Align tip (hand) rotation to target
            tip.rotation = Quaternion.Slerp(tip.rotation, targetRot, weight);
        }

    }
}
