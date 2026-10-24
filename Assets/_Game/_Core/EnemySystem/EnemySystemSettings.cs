using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // The enemy system's frame budget: shared by every enemy kind in a scene.
    [CreateAssetMenu(fileName = "EnemySystemSettings", menuName = "Aipocalypse/Enemies/Enemy System Settings")]
    public sealed class EnemySystemSettings : ScriptableObject
    {
        [Min(0.01f), Tooltip("Seconds between two thinks of one enemy's behaviour tree, at any frame rate. Lower = sharper, costlier")]
        public float ThinkInterval = 1f / 15f;
        [Min(1), Tooltip("Most trees ticked in one frame; a crowd past this thinks a little slower instead of spiking the frame")]
        public int MaxThinksPerFrame = 200;
    }
}
