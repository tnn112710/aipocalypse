using UnityEngine;

namespace MB13.Aipocalypse.Core.AmbientSystem
{
    // How one kind of flock or school moves: its members' look and count, speed, how they keep together, how far they roam
    // from where they spawned and when they scatter from the player. Where it lives (air or water, how high or deep) is its
    // SpawnCard's placement.
    [CreateAssetMenu(fileName = "Flock", menuName = "Aipocalypse/Ambient/Flock Definition")]
    public sealed class FlockDefinition : ScriptableObject
    {
        [Tooltip("One member's look: a light prefab (renderers only: no Animator, collider or script)")] public GameObject Member;
        [Tooltip("Members per flock, min and max (inclusive)")] public Vector2Int Count = new(6, 12);
        [Min(0.1f), Tooltip("Cruising speed (m/s)")] public float Speed = 6f;
        [Min(0.1f), Tooltip("How fast a member changes velocity (m/s²)")] public float Acceleration = 8f;
        [Min(0f), Tooltip("Metres from its spawn point it roams before turning back")] public float RoamRadius = 25f;
        [Min(0.1f), Tooltip("Metres within which members follow each other")] public float NeighbourRadius = 5f;
        [Min(0f), Tooltip("Metres members keep between each other")] public float Separation = 1.2f;
        [Min(0f)] public float SeparationWeight = 1.5f;
        [Min(0f), Tooltip("Pull towards its neighbours' heading")] public float Alignment = 1f;
        [Min(0f), Tooltip("Pull towards its neighbours' centre")] public float Cohesion = 0.6f;
        [Min(0f), Tooltip("Pull of its own slow random drift")] public float Wander = 0.6f;
        [Min(0f), Tooltip("Pull back once past RoamRadius, growing with the overshoot")] public float HomePull = 1f;
        [Min(0f), Tooltip("Push back into its height band (air) or depth band (water)")] public float BandPull = 2f;
        [Range(0f, 1f), Tooltip("Share of its steering that may go up or down (birds climb, fish mostly don't)")]
        public float VerticalFreedom = 0.4f;
        [Min(0f), Tooltip("Members scatter from the player within this many metres; 0 = never")] public float FleeRadius = 12f;
        [Min(0f)] public float FleeWeight = 3f;
        [Min(1f), Tooltip("Speed × this while fleeing")] public float FleeSpeedMultiplier = 1.8f;
    }
}
