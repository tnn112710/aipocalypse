using MalbersAnimations.Scriptables;
using UnityEngine;

namespace SunfishCreations
{
    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fishing-reeler")]
    public class RodReeler : MonoBehaviour
    {
        private const string animSpeedName = "AnimSpeed";

        private AudioSource _reelSound;

        [SerializeField] private Animator _rodAnimator;

        [Header("Reeler Settings")]
        public FloatReference animatorSpeedModifier = 1f;

        protected virtual void Awake()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            _reelSound = GetComponentInChildren<AudioSource>();
            _rodAnimator.SetFloat(animSpeedName, 0f);
            if (_reelSound != null)
            {
                _reelSound.Stop();
            }
        }

        public virtual void StartReeling(float speed)
        {
            _rodAnimator.SetFloat(animSpeedName, speed * animatorSpeedModifier);

            if (_reelSound != null && !_reelSound.isPlaying)
            {
                _reelSound.Play();
            }
        }

        public virtual void StopReeling()
        {
            _rodAnimator.SetFloat(animSpeedName, 0f);

            if (_reelSound != null && _reelSound.isPlaying)
            {
                _reelSound.Stop();
            }
        }

        protected virtual void OnDisable()
        {
            StopReeling();
        }
    }
}