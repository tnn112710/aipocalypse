using System.Collections;
using UnityEngine;

namespace SunfishCreations.IK
{
    /// <summary>
    /// Raycast-based IIKBridge for ground foot placement during vehicle enter/exit.
    /// Place on the same GameObject as IKSolver.
    /// </summary>
    public class FootPlacementIK : MonoBehaviour, IIKBridge    {
        [Header("Ground Detection")]
        [Tooltip("Layers considered ground for the foot raycast.")]
        [SerializeField] private LayerMask groundMask;

        [Tooltip("Upward offset from the foot bone where the ray starts.")]
        [SerializeField] private float raycastOriginOffset = 0.3f;

        [Tooltip("Maximum downward ray distance below the foot bone.")]
        [SerializeField] private float raycastLength = 0.6f;

        [Tooltip("Vertical offset from the hit point to the foot target (sole thickness).")]
        [SerializeField] private float footHeightOffset = 0.05f;

        [Header("Enter Transition")]
        [Tooltip("Seconds of ground-check IK before transitioning to vehicle targets.")]
        [SerializeField] private float groundCheckDuration = 1f;

        [Tooltip("Seconds to lerp foot positions from ground to vehicle targets.")]
        [SerializeField] private float transitionToTargetDuration = 0.5f;

        [Header("Exit Transition")]
        [Tooltip("Seconds to hold feet at vehicle height before dropping.")]
        [SerializeField] private float holdVehicleHeightDuration = 0.3f;

        [Tooltip("Seconds to lerp foot positions from vehicle targets to ground.")]
        [SerializeField] private float transitionFromVehicleDuration = 0.5f;

        [Header("Deactivation")]
        [Tooltip("Seconds to lerp foot positions back to the animation pose before cutting IK.")]
        [SerializeField] private float deactivateDuration = 0.3f;

        private Vector3 leftKneePoleLocal = new Vector3(-0.03f, 0f, 0.5f);
        private Vector3 rightKneePoleLocal = new Vector3(0f, 0f, 0.5f);

        [Header("Debug")]
        [Tooltip("Draw raycast lines and hit spheres in the Scene view.")]
        [SerializeField] private bool debugDrawRaycasts = false;

        private enum FootIKPhase { Inactive, GroundCheck, TransitionToTarget, HoldingTarget, Deactivating, TransitionFromVehicle }

        private Animator animator;
        private IKSolver ikSolver;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform leftHand;
        private Transform rightHand;

        private float savedLeftHandWeight;
        private float savedRightHandWeight;

        // Guards against re-capturing the saved weight as 0 if a new Activate call
        // interrupts an in-flight restore.
        private bool handWeightsSuppressed;

        private bool leftFootHit;
        private Vector3 leftFootHitPoint;
        private bool rightFootHit;
        private Vector3 rightFootHitPoint;

        private FootIKPhase phase = FootIKPhase.Inactive;
        private IIKBridge vehicleBridge;
        private bool yPositionOnly;
        private Vector3 transitionStartLeft;
        private Vector3 transitionStartRight;
        private float transitionT;
        private Coroutine routine;

        // Effective duration used during the Deactivating phase.
        private float _effectiveDeactivateDuration;

        // Captured during GetFootPosition so Deactivate has a valid lerp start point.
        private Vector3 lastFootTargetLeft;
        private Vector3 lastFootTargetRight;

        private void Awake()
        {
            animator = GetComponentInParent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: No Animator found in parent. Disabling.", this);
                enabled = false;
                return;
            }

            ikSolver = GetComponent<IKSolver>();
            if (ikSolver == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: No IKSolver on this GameObject. Disabling.", this);
                enabled = false;
                return;
            }

            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: No humanoid foot bones. Disabling.", this);
                enabled = false;
                return;
            }

            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        /// <summary>Activates ground-check raycasting IK. Used for vehicle exit.</summary>
        public void Activate()        {
            if (ikSolver == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: Cannot activate — IKSolver is null.", this);
                return;
            }

            StopRoutine();
            vehicleBridge = null;
            yPositionOnly = false;
            phase = FootIKPhase.GroundCheck;

            SuppressHandWeights();

            ikSolver.Activate(this);
        }

        /// <summary>
        /// Activates with a transition from vehicle foot targets down to the ground.
        /// Pass override values >= 0 to bypass the serialized durations.
        /// </summary>
        public void ActivateWithTransitionFromVehicle(IIKBridge vehicleIKBridge, float holdDurationOverride = -1f, float transitionDurationOverride = -1f)
        {
            if (ikSolver == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: Cannot activate — IKSolver is null.", this);
                return;
            }

            StopRoutine();
            vehicleBridge = vehicleIKBridge;
            yPositionOnly = false;

            transitionStartLeft = vehicleIKBridge != null ? vehicleIKBridge.LeftFootPosition : leftFoot.position;
            transitionStartRight = vehicleIKBridge != null ? vehicleIKBridge.RightFootPosition : rightFoot.position;

            phase = FootIKPhase.TransitionFromVehicle;
            transitionT = 0f;

            SuppressHandWeights();

            float effectiveHoldDuration = holdDurationOverride >= 0f ? holdDurationOverride : holdVehicleHeightDuration;
            float effectiveTransitionDuration = transitionDurationOverride >= 0f ? transitionDurationOverride : transitionFromVehicleDuration;

            ikSolver.Activate(this);
            routine = StartCoroutine(TransitionFromVehicleRoutine(effectiveHoldDuration, effectiveTransitionDuration));
        }

        /// <summary>
        /// Activates with a ground-check phase then transitions toward vehicle foot targets.
        /// When yPositionOnly is true, only the vehicle target's Y is used.
        /// </summary>
        public void ActivateWithTransition(IIKBridge vehicleIKBridge, bool yPositionOnly = false)
        {
            if (ikSolver == null)
            {
                Debug.LogWarning($"[FootPlacementIK] {name}: Cannot activate — IKSolver is null.", this);
                return;
            }

            StopRoutine();
            vehicleBridge = vehicleIKBridge;
            this.yPositionOnly = yPositionOnly;
            phase = FootIKPhase.GroundCheck;

            SuppressHandWeights();

            ikSolver.Activate(this);
            routine = StartCoroutine(TransitionToVehicleRoutine());
        }

        /// <summary>
        /// Lerps foot positions back to the animation pose and fades IK weight to zero.
        /// Pass deactivateDurationOverride >= 0 to bypass the serialized duration.
        /// </summary>
        public void Deactivate(float deactivateDurationOverride = -1f)
        {
            if (ikSolver == null) return;
            if (phase == FootIKPhase.Inactive || phase == FootIKPhase.Deactivating) return;

            transitionStartLeft = lastFootTargetLeft;
            transitionStartRight = lastFootTargetRight;

            _effectiveDeactivateDuration = deactivateDurationOverride >= 0f
                ? deactivateDurationOverride
                : deactivateDuration;

            StopRoutine();
            phase = FootIKPhase.Deactivating;
            transitionT = 0f;

            // Start the solver's weight fade in sync with the position lerp below.
            ikSolver.Deactivate();

            routine = StartCoroutine(DeactivateSmoothRoutine());
        }

        /// <summary>Prepares for a seamless handoff to the vehicle IK bridge.</summary>
        public void HandoffToVehicleIK()
        {
            StopRoutine();

            // Only restore hand weights when they are actually suppressed — on no-door vehicles
            // this can be called before any activation, so saved weights are still 0f.
            RestoreHandWeights();
        }

        private void StopRoutine()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        /// <summary>
        /// Captures the IKSolver's current hand IK weights and zeroes them, but only the first
        /// time suppression starts. If weights are already suppressed (a save is still pending
        /// restore — e.g. this call interrupted an in-flight Deactivate), the existing saved
        /// values are preserved instead of being overwritten with the already-zeroed current value.
        /// </summary>
        private void SuppressHandWeights()
        {
            if (!handWeightsSuppressed)
            {
                savedLeftHandWeight = ikSolver.GetLeftHandWeight();
                savedRightHandWeight = ikSolver.GetRightHandWeight();
                handWeightsSuppressed = true;
            }

            ikSolver.SetLeftHandWeight(0f);
            ikSolver.SetRightHandWeight(0f);
        }

        /// <summary>Restores previously-suppressed hand IK weights, if any are pending.</summary>
        private void RestoreHandWeights()
        {
            if (!handWeightsSuppressed)
                return;

            ikSolver.SetLeftHandWeight(savedLeftHandWeight);
            ikSolver.SetRightHandWeight(savedRightHandWeight);
            handWeightsSuppressed = false;
        }

        private IEnumerator TransitionToVehicleRoutine()
        {
            yield return new WaitForSeconds(groundCheckDuration);

            transitionStartLeft = ProbeFoot(leftFoot, out leftFootHit, out leftFootHitPoint);
            transitionStartRight = ProbeFoot(rightFoot, out rightFootHit, out rightFootHitPoint);

            phase = FootIKPhase.TransitionToTarget;
            transitionT = 0f;

            while (transitionT < 1f)
            {
                transitionT = Mathf.Min(transitionT + Time.deltaTime / transitionToTargetDuration, 1f);
                yield return null;
            }

            phase = FootIKPhase.HoldingTarget;
        }

        private IEnumerator TransitionFromVehicleRoutine(float holdDuration, float transitionDuration)
        {
            // transitionT stays at 0 during the hold so GetFootPosition returns
            // the snapshotted vehicle targets.
            yield return new WaitForSeconds(holdDuration);

            while (transitionT < 1f)
            {
                transitionT = Mathf.Min(transitionT + Time.deltaTime / transitionDuration, 1f);
                yield return null;
            }

            phase = FootIKPhase.GroundCheck;
            vehicleBridge = null;
            routine = null;
        }

        private IEnumerator DeactivateSmoothRoutine()
        {
            while (transitionT < 1f)
            {
                transitionT = Mathf.Min(transitionT + Time.deltaTime / _effectiveDeactivateDuration, 1f);
                yield return null;
            }

            ikSolver.SetLeftHandWeight(savedLeftHandWeight);
            ikSolver.SetRightHandWeight(savedRightHandWeight);
            handWeightsSuppressed = false;
            phase = FootIKPhase.Inactive;
            vehicleBridge = null;
            routine = null;
        }

        public Vector3 LeftFootPosition => GetFootPosition(leftFoot, true);
        public Vector3 RightFootPosition => GetFootPosition(rightFoot, false);

        private Vector3 GetFootPosition(Transform footBone, bool isLeft)
        {
            if (footBone == null) return Vector3.zero;

            Vector3 result;

            switch (phase)
            {
                case FootIKPhase.GroundCheck:
                    result = isLeft
                        ? ProbeFoot(footBone, out leftFootHit, out leftFootHitPoint)
                        : ProbeFoot(footBone, out rightFootHit, out rightFootHitPoint);
                    break;

                case FootIKPhase.TransitionFromVehicle:
                {
                    Vector3 animPos = footBone.position;
                    Vector3 ground = isLeft
                        ? ProbeFoot(footBone, out leftFootHit, out leftFootHitPoint)
                        : ProbeFoot(footBone, out rightFootHit, out rightFootHitPoint);
                    Vector3 start = isLeft ? transitionStartLeft : transitionStartRight;
                    result = new Vector3(
                        Mathf.Lerp(start.x, animPos.x, transitionT),
                        Mathf.Lerp(start.y, ground.y, transitionT),
                        Mathf.Lerp(start.z, animPos.z, transitionT));
                    break;
                }

                case FootIKPhase.TransitionToTarget:
                    result = Vector3.Lerp(
                        isLeft ? transitionStartLeft : transitionStartRight,
                        GetVehicleFootTarget(isLeft), transitionT);
                    break;

                case FootIKPhase.Deactivating:
                    // footBone.position is the restored animation pose here.
                    result = Vector3.Lerp(
                        isLeft ? transitionStartLeft : transitionStartRight,
                        footBone.position, transitionT);
                    break;

                default:
                    result = GetVehicleFootTarget(isLeft);
                    break;
            }

            if (isLeft)
                lastFootTargetLeft = result;
            else
                lastFootTargetRight = result;

            return result;
        }

        private Vector3 GetVehicleFootTarget(bool isLeft)
        {
            if (vehicleBridge == null)
                return leftFoot != null ? leftFoot.position : Vector3.zero;

            Vector3 vehicleTarget = isLeft ? vehicleBridge.LeftFootPosition : vehicleBridge.RightFootPosition;
            if (!yPositionOnly) return vehicleTarget;

            Transform footBone = isLeft ? leftFoot : rightFoot;
            Vector3 ground = isLeft
                ? ProbeFoot(footBone, out leftFootHit, out leftFootHitPoint)
                : ProbeFoot(footBone, out rightFootHit, out rightFootHitPoint);
            return new Vector3(ground.x, vehicleTarget.y, ground.z);
        }

        // Hand targets — unused while hand IK is disabled.

        public Vector3 LeftHandPosition => leftHand != null ? leftHand.position : Vector3.zero;
        public Vector3 RightHandPosition => rightHand != null ? rightHand.position : Vector3.zero;
        public Quaternion LeftHandRotation => leftHand != null ? leftHand.rotation : Quaternion.identity;
        public Quaternion RightHandRotation => rightHand != null ? rightHand.rotation : Quaternion.identity;

        // Elbow poles — unused while hand IK is disabled.

        public Vector3 LeftElbowPoleLocal => Vector3.zero;
        public Vector3 RightElbowPoleLocal => Vector3.zero;

        public Vector3 LeftKneePoleLocal => GetKneePoleLocal(true);
        public Vector3 RightKneePoleLocal => GetKneePoleLocal(false);

        private Vector3 GetKneePoleLocal(bool isLeft)
        {
            Vector3 defaultPole = isLeft ? leftKneePoleLocal : rightKneePoleLocal;
            if (vehicleBridge == null || yPositionOnly) return defaultPole;

            Vector3 vehiclePole = isLeft ? vehicleBridge.LeftKneePoleLocal : vehicleBridge.RightKneePoleLocal;

            switch (phase)
            {
                case FootIKPhase.TransitionToTarget:
                    return Vector3.Lerp(defaultPole, vehiclePole, transitionT);

                case FootIKPhase.HoldingTarget:
                    return vehiclePole;

                case FootIKPhase.TransitionFromVehicle:
                    return Vector3.Lerp(vehiclePole, defaultPole, transitionT);

                default:
                    return defaultPole;
            }
        }

        // Returns the effective deactivation duration during deactivation so the solver
        // fades weight in sync with the position lerp. Zero otherwise for instant activation.
        public float IKLerpDuration => phase == FootIKPhase.Deactivating ? _effectiveDeactivateDuration : 0f;

        private Vector3 ProbeFoot(Transform footBone, out bool hit, out Vector3 hitPoint)
        {
            hit = false;
            hitPoint = footBone != null ? footBone.position : Vector3.zero;

            if (footBone == null) return Vector3.zero;

            Vector3 origin = footBone.position + Vector3.up * raycastOriginOffset;
            float totalLength = raycastOriginOffset + raycastLength;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit rayHit, totalLength, groundMask))
            {
                hit = true;
                hitPoint = rayHit.point;
                return rayHit.point + Vector3.up * footHeightOffset;
            }

            return footBone.position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!debugDrawRaycasts) return;
            if (leftFoot == null && rightFoot == null) return;

            if (leftFoot != null)
                DrawFootRay(leftFoot, leftFootHit, leftFootHitPoint);

            if (rightFoot != null)
                DrawFootRay(rightFoot, rightFootHit, rightFootHitPoint);
        }

        private void DrawFootRay(Transform footBone, bool hit, Vector3 hitPoint)
        {
            Vector3 origin = footBone.position + Vector3.up * raycastOriginOffset;
            float totalLength = raycastOriginOffset + raycastLength;
            Vector3 end = origin + Vector3.down * totalLength;

            Gizmos.color = hit ? Color.green : Color.red;
            Gizmos.DrawLine(origin, end);

            if (hit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(hitPoint, 0.03f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(hitPoint + Vector3.up * footHeightOffset, 0.02f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(footBone.position, 0.02f);
            }
        }
#endif
    }
}
