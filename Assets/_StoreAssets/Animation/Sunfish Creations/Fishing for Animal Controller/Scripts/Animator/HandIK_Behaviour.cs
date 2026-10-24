using MalbersAnimations;
using UnityEngine;

namespace SunfishCreations.Animations
{
    public enum IKState
    {
        Disable = 0,
        Enable = 1
    }
    public enum SwitchRightHand
    {
        DontSwitch,
        SwitchToDefault,
        SwitchToHandle
    }
    public enum SwitchLeftHand
    {
        DontSwitch,
        SwitchToDefault,
        SwitchToReelPose
    }

    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/animation-behaviours/hand-ik")]
    public class HandIK_Behaviour : StateMachineBehaviour
    {
        [Tooltip("The Hand IK state to apply when triggered")]
        public IKState TargetIKState = IKState.Enable;


        [Tooltip("Should we also switch right hand target")]
        public SwitchRightHand RightHandSwitch = SwitchRightHand.DontSwitch;

        [Tooltip("Should we also switch left hand target")]
        public SwitchLeftHand LeftHandSwitch = SwitchLeftHand.DontSwitch;

        [Header("Activation Triggers")]
        [Tooltip("Toggle Hand IK immediately when entering this animation state")]
        public bool ExecuteOnStateEnter;

        [Tooltip("Toggle Hand IK when exiting this animation state")]
        public bool ExecuteOnStateExit;

        [Tooltip("Toggle Hand IK at a specific time during the animation")]
        public bool ExecuteOnStateUpdate;

        [Hide("ExecuteOnStateUpdate")]
        [Range(0, 1)]
        [Tooltip("Animation time (0-1) when Hand IK should be toggled. 0 = start, 1 = end")]
        public float ActivationTime = 0.5f;

        // Private variables
        private bool hasTriggered = false;
        private IHandIK[] handIKComponents;
        private float previousNormalizedTime = 0f;

        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Reset state
            hasTriggered = false;
            previousNormalizedTime = 0f;

            CacheHandIKComponents(animator);

            if (ExecuteOnStateEnter)
            {
                ToggleHandIK();
            }
        }

        override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!ExecuteOnStateUpdate || hasTriggered)
                return;

            float currentTime = stateInfo.normalizedTime;

            // Handle looping animations properly
            if (currentTime < previousNormalizedTime)
            {
                // Animation looped, reset trigger state
                hasTriggered = false;
            }

            // Use modulo to handle looping animations correctly
            float normalizedTime = currentTime % 1f;

            if (normalizedTime >= ActivationTime)
            {
                ToggleHandIK();
                hasTriggered = true;
            }

            previousNormalizedTime = currentTime;
        }

        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Prevent double execution when transitioning between states
            if (animator.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash == stateInfo.fullPathHash)
                return;

            if (ExecuteOnStateExit)
            {
                ToggleHandIK();
            }

            // Reset for next time
            hasTriggered = false;
        }

        private void CacheHandIKComponents(Animator animator)
        {
            // Try to find IK components in children first
            handIKComponents = animator.GetComponentsInChildren<IHandIK>();

            // If none found, search in parents
            if (handIKComponents.Length == 0)
            {
                handIKComponents = animator.GetComponentsInParent<IHandIK>();
            }
        }

        private void ToggleHandIK()
        {
            if (handIKComponents == null || handIKComponents.Length == 0)
                return;

            foreach (var ikComponent in handIKComponents)
            {
                if (ikComponent != null)
                {
                    ikComponent.OnToggleHandIK(TargetIKState == IKState.Enable);

                    switch (RightHandSwitch)
                    {
                        case SwitchRightHand.DontSwitch:
                            break;                    
                        default:
                            ikComponent.SwitchRightHand(RightHandSwitch);
                            break;
                    }

                    switch (LeftHandSwitch)
                    {
                        case SwitchLeftHand.DontSwitch:
                            break;                     
                        default:
                            ikComponent.SwitchLeftHand(LeftHandSwitch);
                            break;
                    }

                }
            }
        }
    }

    public interface IHandIK
    {
        void OnToggleHandIK(bool isActive);

        void SwitchLeftHand(SwitchLeftHand target);
        void SwitchRightHand(SwitchRightHand target);
    }
}