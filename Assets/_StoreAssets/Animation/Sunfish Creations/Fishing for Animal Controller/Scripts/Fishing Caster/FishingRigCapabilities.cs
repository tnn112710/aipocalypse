using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Controls whether the current fishing rig supports hand-swap and hand-IK behavior.
    /// Lives on the same "Fishing Caster" GameObject as FishingCaster, FishCatcher, and BobberController.
    /// </summary>
    public class FishingRigCapabilities : MonoBehaviour
    {
        [Tooltip("Whether this character's fishing rig supports swapping the rod between hands.")]
        [SerializeField] private bool supportsHandSwap = true;

        [Tooltip("Whether this character's fishing rig supports hand IK (reeling/holding poses).")]
        [SerializeField] private bool supportsIK = true;

        public bool SupportsHandSwap => supportsHandSwap;
        public bool SupportsIK => supportsIK;
    }
}
