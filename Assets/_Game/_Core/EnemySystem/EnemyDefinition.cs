using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // One kind of enemy: how it moves and the behaviour tuning its tree's tasks read. Lives with the enemy in
    // Content/Enemies/<Name>/; behaviour tuning (WanderSettings) can be shared by several kinds.
    [CreateAssetMenu(fileName = "Enemy", menuName = "Aipocalypse/Enemies/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Min(0.1f), Tooltip("Walking speed (m/s)")] public float MoveSpeed = 2f;
        [Min(0.05f), Tooltip("Metres from a destination that count as arrived")] public float StopDistance = 0.3f;
        [Required, InlineEditor, Tooltip("Read by the Wander subtree's tasks")] public WanderSettings Wander;
    }
}
