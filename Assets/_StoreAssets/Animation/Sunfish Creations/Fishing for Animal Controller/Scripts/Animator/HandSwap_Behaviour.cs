using MalbersAnimations;
using UnityEngine;

namespace SunfishCreations.Animations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/animation-behaviours/hand-swap")]
    public class HandSwap_Behaviour : StateMachineBehaviour
    {
        [Tooltip("Range on the Animation that the hands will be swapped")]
        [MinMaxRange(0, 1)]
        public RangedFloat SwapperActivation = new(0.3f, 0.6f);

        public bool keepOnOffHand = false;

        [Hide(nameof(keepOnOffHand), false)]
        public bool restoreOriginalHandOnExit = false;

        public bool MiddleRod;

        private IHandSwapper[] swappers;

        private bool isOn;
        private bool isOff;


        override public void OnStateEnter(Animator anim, AnimatorStateInfo stateInfo, int layerIndex)
        {
            isOn = false;
            isOff = false;
            swappers = anim.GetComponentsInChildren<IHandSwapper>();

            if (swappers.Length < 1)
            {
                swappers = anim.GetComponentsInParent<IHandSwapper>();
            }
            if(SwapperActivation.minValue == 0)
            {
                SwapToOffHand();
            }
        }

        private void SwapToOffHand()
        {
            foreach (var swapper in swappers)
            {
                swapper.OnSwapToSecondaryHand();
            }
            isOn = true;
            if (keepOnOffHand)
            {
                isOff = true;
            }

        }


        override public void OnStateUpdate(Animator anim, AnimatorStateInfo state, int layer)
        {
            var time = state.normalizedTime % 1;

            if (!isOn && (time >= SwapperActivation.minValue))
            {
                SwapToOffHand();
            }

            if (!isOff && (time >= SwapperActivation.maxValue))
            {
                foreach (var swapper in swappers)
                {
                    swapper.OnSwapToOriginalHand(MiddleRod);
                }
                isOff = true;
            }

        }

        override public void OnStateExit(Animator anim, AnimatorStateInfo state, int layer)
        {
            if (anim.GetCurrentAnimatorStateInfo(layer).fullPathHash == state.fullPathHash)
                return;

            if (restoreOriginalHandOnExit)
            {
                foreach (var swapper in swappers)
                {
                    swapper.OnSwapToOriginalHand(MiddleRod);
                }
            }

            isOn = false;
            isOff = false;
        }
    }

    public interface IHandSwapper
    {
        void OnSwapToSecondaryHand();
        void OnSwapToOriginalHand(bool middleRod);
    }
}