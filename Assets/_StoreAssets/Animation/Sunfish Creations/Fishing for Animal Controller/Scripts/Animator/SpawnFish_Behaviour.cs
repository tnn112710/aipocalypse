using SunfishCreations;
using UnityEngine;

namespace SunfishCreations.Animations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/animation-behaviours/spawn-fish")]
    public class SpawnFish_Behaviour : StateMachineBehaviour
    {
        [Tooltip("Animation time when Fish should spawn (0.0 to 1.0)")]
        [Range(0f, 1f)]
        public float spawnTime = 0.5f;

        private bool hasSpawned;

        private IFishSpawner[] spawners;

        public bool despawn;

        override public void OnStateEnter(Animator anim, AnimatorStateInfo stateInfo, int layerIndex)
        {
            hasSpawned = false;

            spawners = anim.GetComponentsInChildren<IFishSpawner>();

            if (spawners.Length < 1)
            {
                spawners = anim.GetComponentsInParent<IFishSpawner>();
            }
        }

        override public void OnStateUpdate(Animator anim, AnimatorStateInfo state, int layer)
        {
            var normalizedTime = state.normalizedTime % 1f;

            if (!hasSpawned && normalizedTime >= spawnTime)
            {
                if (despawn)
                {
                    foreach (var spawner in spawners)
                    {
                        spawner.DespawnFish();
                    }
                }
                else
                {
                    foreach (var spawner in spawners)
                    {
                        spawner.SpawnFish();
                    }
                }
                hasSpawned = true;
            }
        }
    }

    public interface IFishSpawner
    {
        public void SpawnFish();
        public void DespawnFish();
    }
}