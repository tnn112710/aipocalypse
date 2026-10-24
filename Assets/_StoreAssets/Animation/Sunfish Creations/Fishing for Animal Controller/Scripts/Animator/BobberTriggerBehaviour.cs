using MalbersAnimations;
using UnityEngine;

namespace SunfishCreations.Animations
{
    public enum BobberEffectType
    {
        spawn,
        retreat
    }

    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/animation-behaviours/bobber-trigger")]
    public class BobberTriggerBehaviour : StateMachineBehaviour
    {
        [SerializeField] private BobberEffectType _effectType;

        [Tooltip("Animation time when bobber should spawn (0.0 to 1.0)")]
        [Hide("_effectType", false, (int)BobberEffectType.spawn)]
        [Range(0f, 1f)]
        public float spawnTime = 0.5f;

        [Tooltip("Animation time when bobber should retreat (0.0 to 1.0)")]
        [Hide("_effectType", false, (int)BobberEffectType.retreat)]
        [Range(0f, 1f)]
        public float retreatTime = 0.5f;

        private bool hasFinished;
        private IBobberSpawner[] spawners;

        public override void OnStateEnter(Animator anim, AnimatorStateInfo stateInfo, int layerIndex)
        {
            hasFinished = false;
            spawners = anim.GetComponentsInParent<IBobberSpawner>();

            if (spawners.Length < 1)
            {
                spawners = anim.GetComponentsInChildren<IBobberSpawner>();
            }

            if (spawners != null)
            {
                foreach (var spawner in spawners)
                    spawner.OnBobberAnimationStart(stateInfo.fullPathHash);
            }
        }

        public override void OnStateUpdate(Animator anim, AnimatorStateInfo state, int layer)
        {
            var normalizedTime = state.normalizedTime % 1f;

            switch (_effectType)
            {
                case BobberEffectType.spawn:
                    if (!hasFinished && normalizedTime >= spawnTime)
                    {
                        ExecuteSpawn();
                    }
                    break;
                case BobberEffectType.retreat:
                    if (!hasFinished && normalizedTime >= retreatTime)
                    {
                        ExecuteRetreat();
                    }
                    break;
            }
        }

        public override void OnStateExit(Animator anim, AnimatorStateInfo state, int layer)
        {
            // Prevent self-trigger on same-layer re-entrance (e.g. via self-transition)
            if (anim.GetCurrentAnimatorStateInfo(layer).fullPathHash == state.fullPathHash)
                return;

            // Safety check for early exits
            if (!hasFinished)
            {
                switch (_effectType)
                {
                    case BobberEffectType.spawn:
                        ExecuteSpawn();
                        break;
                    case BobberEffectType.retreat:
                        ExecuteRetreat();
                        break;
                }
            }

            if (spawners != null)
            {
                foreach (var spawner in spawners)
                    spawner.OnBobberAnimationEnd(state.fullPathHash);
            }

            hasFinished = false;
        }

        private void ExecuteSpawn()
        {
            if (spawners != null)
            {
                foreach (var spawner in spawners)
                    spawner.OnBobberSpawn();
            }
            hasFinished = true;
        }

        private void ExecuteRetreat()
        {
            if (spawners != null)
            {
                foreach (var spawner in spawners)
                    spawner.OnBobberRetreat();
            }
            hasFinished = true;
        }
    }

    public interface IBobberSpawner
    {
        void OnBobberAnimationStart(int animationHash);
        void OnBobberAnimationEnd(int animationHash);
        void OnBobberSpawn();
        void OnBobberRetreat();
    }
}