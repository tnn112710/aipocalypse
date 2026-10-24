using UnityEngine;

namespace SunfishCreations.Animations
{
    public enum RodBendType
    {
        Cast,
        Pullback,
        HardBiteBend
    }

    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/animation-behaviours/rod-bend")]
    public class RodBend_Behaviour : StateMachineBehaviour
    {
        public RodBendType rodBendType;

        [Tooltip("Animation time when function will be called (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float animationTime = 0.5f;

        private bool hasFinished;

        private IRodBender rodBender;

        override public void OnStateEnter(Animator anim, AnimatorStateInfo stateInfo, int layerIndex)
        {
            hasFinished = false;

            rodBender = anim.GetComponentInChildren<IRodBender>();
        }

        override public void OnStateUpdate(Animator anim, AnimatorStateInfo state, int layer)
        {
            var normalizedTime = state.normalizedTime % 1f;

            if (!hasFinished && normalizedTime >= animationTime)
            {
                switch (rodBendType)
                {
                    case RodBendType.Cast:
                        rodBender.CastBend();
                        break;
                    case RodBendType.Pullback:
                        rodBender.PullbackBend();
                        break;
                    case RodBendType.HardBiteBend:
                        rodBender.HardBiteBend();
                        break;
                }

                hasFinished = true;
            }
        }
    }

    public interface IRodBender
    {
        public void CastBend();
        public void PullbackBend();
        public void HardBiteBend();
    }
}