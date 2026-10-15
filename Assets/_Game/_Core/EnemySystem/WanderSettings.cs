using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    [CreateAssetMenu(menuName = "Aipocalypse/Enemies/Wander Settings")]
    public sealed class WanderSettings : ScriptableObject
    {
        [Min(1f)] public float Radius = 10f;
        [Min(0.1f)] public float MoveSpeed = 2f;
        [Min(0.05f)] public float StopDistance = 0.3f;
        [Min(0f)] public float PauseDuration = 1.5f;
        [Min(1f)] public float TravelTimeout = 20f;
        [Min(1)] public int ThinkStride = 4;
    }
}
