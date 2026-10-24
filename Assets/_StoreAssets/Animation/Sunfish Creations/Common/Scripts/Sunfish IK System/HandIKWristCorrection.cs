using UnityEngine;

namespace SunfishCreations.IK
{
    /// <summary>
    /// Corrects hand IK targets for Unity's wrist-as-IK-goal convention.
    /// Offsets each IK goal by the wrist-to-hand vector so the palm lands on the target.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HandIKWristCorrection : MonoBehaviour
    {
        [Header("IK Target Transforms")]
        [Tooltip("The IK Left Hand target transform.")]
        public Transform leftHandTarget;

        [Tooltip("The IK Right Hand target transform.")]
        public Transform rightHandTarget;

        [Header("IK Weights")]
        [Range(0f, 1f)]
        public float leftHandWeight = 1f;

        [Range(0f, 1f)]
        public float rightHandWeight = 1f;

        [Header("Wrist-to-Palm Offset (local to wrist bone, tweak in Play Mode)")]
        [Tooltip("Local-space offset from the wrist bone to the true palm grip centre. " +
                 "Positive Z is usually toward the fingers. Adjust until the palm sits on the grip.")]
        public Vector3 leftWristOffset = Vector3.zero;

        [Tooltip("Local-space offset from the wrist bone to the true palm grip centre.")]
        public Vector3 rightWristOffset = Vector3.zero;

        private Animator _animator;
        private Transform _leftWristBone;
        private Transform _rightWristBone;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            // Cache wrist bone transforms once the avatar is ready.
            if (_animator.isHuman)            {
                _leftWristBone  = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
                _rightWristBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else
            {
                Debug.LogWarning($"[HandIKWristCorrection] Animator on {name} is not humanoid. " +
                                  "This component requires a humanoid avatar.");
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!_animator.isHuman) return;

            if (leftHandTarget != null && leftHandWeight > 0f)
            {
                ApplyGoal(AvatarIKGoal.LeftHand, leftHandTarget, _leftWristBone,
                          leftWristOffset, leftHandWeight);
            }

            if (rightHandTarget != null && rightHandWeight > 0f)
            {
                ApplyGoal(AvatarIKGoal.RightHand, rightHandTarget, _rightWristBone,
                          rightWristOffset, rightHandWeight);
            }
        }

        /// <summary>
        /// Drives the IK goal so the palm (wrist + offset) lands on the target.
        /// </summary>
        private void ApplyGoal(AvatarIKGoal goal, Transform target,
                               Transform wristBone, Vector3 wristOffset, float weight)
        {
            // Desired world-space palm rotation matches the target.
            Quaternion palmRotation = target.rotation;

            // Wrist position = palm position - (palmRotation * local wrist offset).
            Vector3 correctedGoalPos = target.position - palmRotation * wristOffset;

            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKPosition(goal, correctedGoalPos);

            _animator.SetIKRotationWeight(goal, weight);
            _animator.SetIKRotation(goal, palmRotation);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (_animator == null || !_animator.isHuman) return;

            DrawGoalGizmo(leftHandTarget,  _leftWristBone,  leftWristOffset,  Color.cyan);
            DrawGoalGizmo(rightHandTarget, _rightWristBone, rightWristOffset, Color.magenta);
        }

        private static void DrawGoalGizmo(Transform target, Transform wristBone,
                                          Vector3 wristOffset, Color color)
        {
            if (target == null || wristBone == null) return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(target.position, 0.03f);

            // Corrected wrist goal
            Vector3 goalPos = target.position - target.rotation * wristOffset;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(goalPos, 0.02f);
            Gizmos.DrawLine(target.position, goalPos);
        }
#endif
    }
}
