using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [CreateAssetMenu(fileName = "WanderSettings", menuName = "Aipocalypse/Enemies/Wander Settings")]
    public sealed class WanderSettings : ScriptableObject
    {
        [Min(1f), Tooltip("Metres from the spawn point a wander destination can be")] public float Radius = 10f;
        [Min(0.1f), Tooltip("m/s")] public float MoveSpeed = 2f;
        [Min(0.05f), Tooltip("Metres from the destination that count as arrived")] public float StopDistance = 0.3f;
        [Min(0f), Tooltip("Seconds standing still between walks")] public float PauseDuration = 1.5f;
        [Min(1f), Tooltip("Seconds a walk may take before it's given up")] public float TravelTimeout = 20f;
        [Min(1), Tooltip("Random points tried when choosing a destination, before giving up for this think")] public int DestinationAttempts = 12;
        [Min(0f), Tooltip("Destinations closer than this (m) are skipped as not worth walking to")] public float MinTravelDistance = 1f;
        [Min(1), Tooltip("EnemyManager ticks each tree every this many frames (scene settings only)")] public int ThinkStride = 4;
    }
}
