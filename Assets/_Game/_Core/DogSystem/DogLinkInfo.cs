using MB13.Aipocalypse.Core.DefenseSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // On an off-mesh link the dog crosses (DogJump): what's in the way and where to walk before jumping. Also marks a fence
    // crossing (no link: one would join the shut yard's navmesh island to the outside for every agent): the dog scrambles
    // over from this point to Outside by hand (DogScramble), only while its segment stands.
    [DisallowMultipleComponent]
    public sealed class DogLinkInfo : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("The top of what's between the ends, above the start (a 2 m fence: 2)")] float _obstacleHeight;
        [SerializeField, Tooltip("Walked to first, then jumped from (the house door's sill); empty: jump from the start")] Transform _via;
        [SerializeField, Tooltip("A fence crossing's far end (outside the yard); this transform is the inside end")] Transform _outside;
        [SerializeField, Tooltip("A fence crossing's segment: fallen (debris, not built yet) or an open gate, it's a gap walked through")]
        BarrierSegment _segment;

        public float ObstacleHeight => _obstacleHeight;
        public Transform Via => _via;
        public Vector3 Inside => transform.position;
        public Vector3 Outside => _outside != null ? _outside.position : transform.position;
        // Something to scramble over here: the segment stands and is shut (no segment: always).
        public bool Standing => _segment == null || _segment.IsBlocking;

        public void Setup(float obstacleHeight, Transform via, Transform outside = null, BarrierSegment segment = null)
        {
            _obstacleHeight = obstacleHeight;
            _via = via;
            _outside = outside;
            _segment = segment;
        }
    }
}
