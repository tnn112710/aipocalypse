using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Tuning of the shared Wander subtree (choose a reachable point near home, walk there, pause). Speed lives in EnemyDefinition.
    [CreateAssetMenu(fileName = "WanderSettings", menuName = "Aipocalypse/Enemies/Wander Settings")]
    public sealed class WanderSettings : ScriptableObject
    {
        [Min(1f), Tooltip("Metres from the spawn point a wander destination can be")] public float Radius = 10f;
        [Min(0f), Tooltip("Seconds standing still between walks")] public float PauseDuration = 1.5f;
        [Min(1f), Tooltip("Seconds a walk may take before it's given up")] public float TravelTimeout = 20f;
        [Min(1), Tooltip("Random points tried when choosing a destination, before giving up for this think")] public int DestinationAttempts = 12;
        [Min(0f), Tooltip("Destinations closer than this (m) are skipped as not worth walking to")] public float MinTravelDistance = 1f;
    }
}
