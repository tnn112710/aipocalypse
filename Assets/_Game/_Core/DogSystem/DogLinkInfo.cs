using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // On an off-mesh link the dog crosses (DogJump): what's in the way and where to walk before jumping.
    [DisallowMultipleComponent]
    public sealed class DogLinkInfo : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("The top of what's between the ends, above the start (a 2 m fence: 2)")] float _obstacleHeight;
        [SerializeField, Tooltip("Walked to first, then jumped from (the house door's sill); empty: jump from the start")] Transform _via;

        public float ObstacleHeight => _obstacleHeight;
        public Transform Via => _via;

        public void Setup(float obstacleHeight, Transform via)
        {
            _obstacleHeight = obstacleHeight;
            _via = via;
        }
    }
}
