using UnityEngine;

namespace AshTools.ControlRigPro
{
    [RequireComponent(typeof(Animator))]
    public class FootGrounder : MonoBehaviour
    {
        [Header("Foot Settings")]
        public bool enableLeftFoot = true;
        public bool enableRightFoot = true;
        public Transform leftFootBone;
        public Transform rightFootBone;
        
        [Header("Ground Detection")]
        public LayerMask groundLayers = ~0;
        public float raycastDistance = 1.5f;
        public float raycastOriginOffset = 0.5f;
        public float footOffset = 0.05f;
        
        [Header("Grounding Logic")]
        public float groundingThreshold = 0.1f;
        public float liftThreshold = 0.15f;
        public bool limitStepHeight = true;
        public float maxStepUp = 0.4f;
        public float maxStepDown = 0.6f;
        
        [Header("Smoothing")]
        [Range(0f, 1f)]
        public float ikWeight = 1f;
        public bool enableSmoothing = true;
        public float liftingSpeed = 12f;
        public float groundingSpeed = 8f;
        
        [Header("Slope Adaptation")]
        public bool adaptToSlope = true;
        
        [Header("Debug")]
        public bool showDebugRays = true;
        public bool debugLog = false;
        
        private Animator animator;
        private bool isHumanoidRig;
        
        private Vector3 leftFootAnimatedPos;
        private Vector3 rightFootAnimatedPos;
        private Vector3 leftFootTargetPos;
        private Vector3 rightFootTargetPos;
        private Vector3 leftFootSmoothedPos;
        private Vector3 rightFootSmoothedPos;
        private Quaternion leftFootTargetRot;
        private Quaternion rightFootTargetRot;
        private Quaternion leftFootSmoothedRot;
        private Quaternion rightFootSmoothedRot;
        
        private bool leftFootShouldBeGrounded;
        private bool rightFootShouldBeGrounded;
        
        void Start()
        {
            animator = GetComponent<Animator>();
            isHumanoidRig = animator.isHuman;
            
            if (!isHumanoidRig && (leftFootBone == null || rightFootBone == null))
            {
                Debug.LogWarning("FootGrounder: Generic rig detected but foot bones not assigned. Please assign leftFootBone and rightFootBone.", this);
            }
            
            InitializeFootPositions();
            
            if (debugLog)
            {
                Debug.Log($"FootGrounder initialized. Rig type: {(isHumanoidRig ? "Humanoid" : "Generic")}", this);
            }
        }
        
        void InitializeFootPositions()
        {
            Vector3 leftPos, rightPos;
            Quaternion leftRot, rightRot;
            
            if (isHumanoidRig)
            {
                leftPos = animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.position ?? Vector3.zero;
                rightPos = animator.GetBoneTransform(HumanBodyBones.RightFoot)?.position ?? Vector3.zero;
                leftRot = animator.GetBoneTransform(HumanBodyBones.LeftFoot)?.rotation ?? Quaternion.identity;
                rightRot = animator.GetBoneTransform(HumanBodyBones.RightFoot)?.rotation ?? Quaternion.identity;
            }
            else
            {
                leftPos = leftFootBone?.position ?? Vector3.zero;
                rightPos = rightFootBone?.position ?? Vector3.zero;
                leftRot = leftFootBone?.rotation ?? Quaternion.identity;
                rightRot = rightFootBone?.rotation ?? Quaternion.identity;
            }
            
            leftFootSmoothedPos = leftPos;
            rightFootSmoothedPos = rightPos;
            leftFootSmoothedRot = leftRot;
            rightFootSmoothedRot = rightRot;
            leftFootTargetPos = leftPos;
            rightFootTargetPos = rightPos;
            leftFootTargetRot = leftRot;
            rightFootTargetRot = rightRot;
        }
        
        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !isHumanoidRig) return;
            
            ProcessSmoothedFootIK(AvatarIKGoal.LeftFoot, enableLeftFoot, true);
            ProcessSmoothedFootIK(AvatarIKGoal.RightFoot, enableRightFoot, false);
        }
        
        void ProcessSmoothedFootIK(AvatarIKGoal foot, bool enabled, bool isLeftFoot)
        {
            if (!enabled) return;
            
            Vector3 footPosition = animator.GetIKPosition(foot);
            Quaternion footRotation = animator.GetIKRotation(foot);
            
            Vector3 targetPos;
            Quaternion targetRot;
            bool shouldBeGrounded;
            
            // Determine target position and rotation
            Vector3 rayOrigin = footPosition + Vector3.up * raycastOriginOffset;
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 
                raycastOriginOffset + raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                float distanceToGround = hit.distance - raycastOriginOffset;
                
                if (distanceToGround <= groundingThreshold)
                {
                    Vector3 groundedPosition = new Vector3(footPosition.x, hit.point.y + footOffset, footPosition.z);
                    
                    if (limitStepHeight)
                    {
                        float minY = footPosition.y - maxStepDown;
                        float maxY = footPosition.y + maxStepUp;
                        groundedPosition.y = Mathf.Clamp(groundedPosition.y, minY, maxY);
                    }
                    
                    targetPos = groundedPosition;
                    shouldBeGrounded = true;
                    
                    if (adaptToSlope)
                    {
                        Vector3 footForward = footRotation * Vector3.forward;
                        Vector3 footRight = Vector3.Cross(hit.normal, footForward).normalized;
                        footForward = Vector3.Cross(footRight, hit.normal).normalized;
                        targetRot = Quaternion.LookRotation(footForward, hit.normal);
                    }
                    else
                    {
                        targetRot = footRotation;
                    }
                }
                else
                {
                    targetPos = footPosition;
                    targetRot = footRotation;
                    shouldBeGrounded = false;
                }
            }
            else
            {
                targetPos = footPosition;
                targetRot = footRotation;
                shouldBeGrounded = false;
            }
            
            // Update target values and smooth towards them
            if (isLeftFoot)
            {
                leftFootAnimatedPos = footPosition;
                leftFootTargetPos = targetPos;
                leftFootTargetRot = targetRot;
                leftFootShouldBeGrounded = shouldBeGrounded;
                
                UpdateSmoothedFootPosition(true);
                
                float posWeight = shouldBeGrounded ? ikWeight : 0f;
                float rotWeight = (shouldBeGrounded && adaptToSlope) ? ikWeight : 0f;
                
                animator.SetIKPositionWeight(foot, posWeight);
                animator.SetIKRotationWeight(foot, rotWeight);
                animator.SetIKPosition(foot, leftFootSmoothedPos);
                animator.SetIKRotation(foot, leftFootSmoothedRot);
            }
            else
            {
                rightFootAnimatedPos = footPosition;
                rightFootTargetPos = targetPos;
                rightFootTargetRot = targetRot;
                rightFootShouldBeGrounded = shouldBeGrounded;
                
                UpdateSmoothedFootPosition(false);
                
                float posWeight = shouldBeGrounded ? ikWeight : 0f;
                float rotWeight = (shouldBeGrounded && adaptToSlope) ? ikWeight : 0f;
                
                animator.SetIKPositionWeight(foot, posWeight);
                animator.SetIKRotationWeight(foot, rotWeight);
                animator.SetIKPosition(foot, rightFootSmoothedPos);
                animator.SetIKRotation(foot, rightFootSmoothedRot);
            }
        }
        
        void UpdateSmoothedFootPosition(bool isLeftFoot)
        {
            if (isLeftFoot)
            {
                if (enableSmoothing)
                {
                    float deltaTime = Time.deltaTime;
                    float currentHeight = leftFootSmoothedPos.y;
                    float targetHeight = leftFootTargetPos.y;
                    float heightDifference = targetHeight - currentHeight;
                    
                    // Choose speed based on whether we're lifting (going up) or grounding (going down)
                    float speed;
                    bool isLifting = heightDifference > 0.01f; // Small threshold to avoid jitter
                    bool isGrounding = heightDifference < -0.01f;
                    
                    if (isLifting)
                    {
                        speed = liftingSpeed;
                    }
                    else if (isGrounding)
                    {
                        speed = groundingSpeed;
                    }
                    else
                    {
                        // Very close to target, use faster speed to snap
                        speed = Mathf.Max(liftingSpeed, groundingSpeed);
                    }
                    
                    // Only smooth Y-axis, follow animation exactly in XZ plane
                    float smoothedY = Mathf.Lerp(currentHeight, targetHeight, speed * deltaTime);
                    leftFootSmoothedPos = new Vector3(leftFootTargetPos.x, smoothedY, leftFootTargetPos.z);
                    
                    if (debugLog)
                    {
                        Debug.Log($"Left Foot - Current: {currentHeight:F2}, Target: {targetHeight:F2}, Diff: {heightDifference:F2}, Speed: {speed}, IsLifting: {isLifting}, IsGrounding: {isGrounding}");
                    }
                }
                else
                {
                    // No smoothing - snap directly to target
                    leftFootSmoothedPos = leftFootTargetPos;
                }
                
                // Use target rotation directly, no smoothing
                leftFootSmoothedRot = leftFootTargetRot;
            }
            else
            {
                if (enableSmoothing)
                {
                    float deltaTime = Time.deltaTime;
                    float currentHeight = rightFootSmoothedPos.y;
                    float targetHeight = rightFootTargetPos.y;
                    float heightDifference = targetHeight - currentHeight;
                    
                    // Choose speed based on whether we're lifting (going up) or grounding (going down)
                    float speed;
                    bool isLifting = heightDifference > 0.01f; // Small threshold to avoid jitter
                    bool isGrounding = heightDifference < -0.01f;
                    
                    if (isLifting)
                    {
                        speed = liftingSpeed;
                    }
                    else if (isGrounding)
                    {
                        speed = groundingSpeed;
                    }
                    else
                    {
                        // Very close to target, use faster speed to snap
                        speed = Mathf.Max(liftingSpeed, groundingSpeed);
                    }
                    
                    // Only smooth Y-axis, follow animation exactly in XZ plane
                    float smoothedY = Mathf.Lerp(currentHeight, targetHeight, speed * deltaTime);
                    rightFootSmoothedPos = new Vector3(rightFootTargetPos.x, smoothedY, rightFootTargetPos.z);
                }
                else
                {
                    // No smoothing - snap directly to target
                    rightFootSmoothedPos = rightFootTargetPos;
                }
                
                // Use target rotation directly, no smoothing
                rightFootSmoothedRot = rightFootTargetRot;
            }
        }
        
        void LateUpdate()
        {
            if (isHumanoidRig) return;
            
            ProcessSmoothedGenericFootGrounding(leftFootBone, enableLeftFoot, true);
            ProcessSmoothedGenericFootGrounding(rightFootBone, enableRightFoot, false);
        }
        
        void ProcessSmoothedGenericFootGrounding(Transform footBone, bool enabled, bool isLeftFoot)
        {
            if (!enabled || footBone == null) return;
            
            Vector3 footPosition = footBone.position;
            Quaternion footRotation = footBone.rotation;
            
            Vector3 targetPos;
            Quaternion targetRot;
            bool shouldBeGrounded;
            
            // Determine target position and rotation
            Vector3 rayOrigin = footPosition + Vector3.up * raycastOriginOffset;
            
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 
                raycastOriginOffset + raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                float distanceToGround = hit.distance - raycastOriginOffset;
                
                if (distanceToGround <= groundingThreshold)
                {
                    Vector3 groundedPosition = new Vector3(footPosition.x, hit.point.y + footOffset, footPosition.z);
                    
                    if (limitStepHeight)
                    {
                        float minY = footPosition.y - maxStepDown;
                        float maxY = footPosition.y + maxStepUp;
                        groundedPosition.y = Mathf.Clamp(groundedPosition.y, minY, maxY);
                    }
                    
                    targetPos = groundedPosition;
                    shouldBeGrounded = true;
                    
                    if (adaptToSlope)
                    {
                        Vector3 footForward = footRotation * Vector3.forward;
                        Vector3 footRight = Vector3.Cross(hit.normal, footForward).normalized;
                        footForward = Vector3.Cross(footRight, hit.normal).normalized;
                        targetRot = Quaternion.LookRotation(footForward, hit.normal);
                    }
                    else
                    {
                        targetRot = footRotation;
                    }
                }
                else
                {
                    targetPos = footPosition;
                    targetRot = footRotation;
                    shouldBeGrounded = false;
                }
            }
            else
            {
                targetPos = footPosition;
                targetRot = footRotation;
                shouldBeGrounded = false;
            }
            
            // Update target values and smooth towards them
            if (isLeftFoot)
            {
                leftFootAnimatedPos = footPosition;
                leftFootTargetPos = targetPos;
                leftFootTargetRot = targetRot;
                leftFootShouldBeGrounded = shouldBeGrounded;
                
                UpdateSmoothedFootPosition(true);
                
                if (shouldBeGrounded)
                {
                    footBone.position = Vector3.Lerp(footPosition, leftFootSmoothedPos, ikWeight);
                    if (adaptToSlope)
                    {
                        footBone.rotation = Quaternion.Slerp(footRotation, leftFootSmoothedRot, ikWeight);
                    }
                }
            }
            else
            {
                rightFootAnimatedPos = footPosition;
                rightFootTargetPos = targetPos;
                rightFootTargetRot = targetRot;
                rightFootShouldBeGrounded = shouldBeGrounded;
                
                UpdateSmoothedFootPosition(false);
                
                if (shouldBeGrounded)
                {
                    footBone.position = Vector3.Lerp(footPosition, rightFootSmoothedPos, ikWeight);
                    if (adaptToSlope)
                    {
                        footBone.rotation = Quaternion.Slerp(footRotation, rightFootSmoothedRot, ikWeight);
                    }
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (!showDebugRays) return;
            
            DrawFootDebugGizmos(true);
            DrawFootDebugGizmos(false);
        }
        
        void DrawFootDebugGizmos(bool isLeftFoot)
        {
            if (!((isLeftFoot && enableLeftFoot) || (!isLeftFoot && enableRightFoot))) return;
            
            Vector3 animatedPos = isLeftFoot ? leftFootAnimatedPos : rightFootAnimatedPos;
            Vector3 targetPos = isLeftFoot ? leftFootTargetPos : rightFootTargetPos;
            Vector3 smoothedPos = isLeftFoot ? leftFootSmoothedPos : rightFootSmoothedPos;
            bool shouldBeGrounded = isLeftFoot ? leftFootShouldBeGrounded : rightFootShouldBeGrounded;
            
            if (animatedPos == Vector3.zero) return;
            
            Vector3 rayOrigin = animatedPos + Vector3.up * raycastOriginOffset;
            Vector3 rayEnd = rayOrigin + Vector3.down * (raycastOriginOffset + raycastDistance);
            
            // Ground detection ray
            Gizmos.color = shouldBeGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(rayOrigin, rayEnd);
            
            // Animated foot position (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(animatedPos, 0.03f);
            
            if (shouldBeGrounded)
            {
                // Target ground position (cyan)
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(targetPos, 0.04f);
                
                // Smoothed current position (magenta)
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(smoothedPos, 0.05f);
                
                // Line from animated to target
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(animatedPos, targetPos);
                
                // Line from target to smoothed (shows smoothing progress)
                Gizmos.color = Color.white;
                Gizmos.DrawLine(targetPos, smoothedPos);
                
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(rayOrigin, 0.02f);
            }
            
            // Step height limits
            if (limitStepHeight && shouldBeGrounded)
            {
                float minY = animatedPos.y - maxStepDown;
                float maxY = animatedPos.y + maxStepUp;
                
                Gizmos.color = Color.gray;
                Vector3 minPos = new Vector3(animatedPos.x, minY, animatedPos.z);
                Vector3 maxPos = new Vector3(animatedPos.x, maxY, animatedPos.z);
                Gizmos.DrawWireCube(minPos, Vector3.one * 0.02f);
                Gizmos.DrawWireCube(maxPos, Vector3.one * 0.02f);
                Gizmos.DrawLine(minPos, maxPos);
            }
            
            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Vector3 labelPos = animatedPos + Vector3.up * 0.4f + (isLeftFoot ? Vector3.left : Vector3.right) * 0.3f;
                string footLabel = isLeftFoot ? "L" : "R";
                string status = shouldBeGrounded ? "GROUNDED" : "LIFTING";
                
                // Add smoothing indicator
                if (shouldBeGrounded && targetPos != smoothedPos)
                {
                    float distance = Vector3.Distance(targetPos, smoothedPos);
                    status += $" (Smooth: {distance:F2})";
                }
                
                UnityEditor.Handles.color = shouldBeGrounded ? Color.green : Color.red;
                UnityEditor.Handles.Label(labelPos, $"{footLabel}: {status}");
                
                // Speed indicator
                Vector3 speedLabel = labelPos + Vector3.down * 0.15f;
                float currentHeight = smoothedPos.y;
                float targetHeight = targetPos.y;
                bool isLifting = targetHeight > currentHeight;
                float currentSpeed = isLifting ? liftingSpeed : groundingSpeed;
                string speedText = isLifting ? $"LIFTING ({currentSpeed})" : $"GROUNDING ({currentSpeed})";
                
                UnityEditor.Handles.color = isLifting ? Color.red : Color.green;
                UnityEditor.Handles.Label(speedLabel, speedText);
            }
            #endif
        }
    }
}