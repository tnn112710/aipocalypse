using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // How an animated enemy's body reacts to its movement: glances while standing, idle fidgets, leaning and looking into
    // turns. Shared by kinds through EnemyDefinition.Animation; the clips themselves live in each kind's controller.
    [CreateAssetMenu(fileName = "EnemyAnimationSettings", menuName = "Aipocalypse/Enemies/Enemy Animation Settings")]
    public sealed class EnemyAnimationSettings : ScriptableObject
    {
        [Min(0f), Tooltip("Below this speed (m/s) the enemy counts as standing")] public float StillSpeed = 0.15f;
        [Min(0f), Tooltip("Seconds the Speed parameter takes to settle")] public float SpeedDampTime = 0.12f;

        [Tooltip("Seconds between two glances while standing (min, max)")] public Vector2 GlanceInterval = new(1.2f, 2.8f);
        [Tooltip("How far a glance turns the head: x left/right, y up/down (1 = the full additive pose)")] public Vector2 GlanceRange = new(0.9f, 0.35f);
        [Range(0f, 1f), Tooltip("Chance a glance looks straight ahead instead")] public float GlanceForwardChance = 0.25f;
        [Min(0f), Tooltip("Head turn into a turn while walking, per degree/s of turning (1 = full pose)")] public float LookIntoTurn = 1f / 90f;
        [Min(0f), Tooltip("Seconds the look parameters take to settle")] public float LookDampTime = 0.35f;

        [Min(0f), Tooltip("Lean into a turn while walking, per degree/s of turning (1 = full pose)")] public float LeanIntoTurn = 1f / 150f;
        [Min(0f), Tooltip("Seconds the lean takes to settle")] public float LeanDampTime = 0.3f;

        [Tooltip("Seconds standing still before an idle fidget may play (min, max)")] public Vector2 FidgetDelay = new(1.5f, 3f);
        [Range(0f, 1f), Tooltip("Chance a stop gets a fidget once the delay has passed")] public float FidgetChance = 0.6f;
    }
}
