using MB13.Aipocalypse.Core.SpawnSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RaidSystem
{
    // When raids run, what a death costs in time, and how spawn points are picked. Read-only at runtime.
    [CreateAssetMenu(fileName = "RaidSettings", menuName = "Aipocalypse/Raids/Raid Settings")]
    public sealed class RaidSettings : ScriptableObject
    {
        [Header("Night"), Range(0f, 24f), Tooltip("Hour a raid night starts")] public float StartHour = 20f;
        [Range(0f, 24f), Tooltip("Hour the night ends: raiders still alive despawn out of sight")] public float DawnHour = 6f;
        [Min(1), Tooltip("Most raiders alive at once; groups wait while it's reached")] public int AliveCap = 12;
        [Min(0), Tooltip("Enemies prewarmed beyond AliveCap: the dead keep theirs while their bodies lie (CorpseTime + SinkTime, or " +
            "until looted when they hold loot: up to EnemySystemSettings.MaxLootBodies), and raiders spawned meanwhile would otherwise " +
            "be instantiated mid-fight")]
        public int CorpseHeadroom = 8;
        [Required, InlineEditor, Tooltip("The raid every night runs (EveryNightSchedule) until quiet nights and a ladder exist")]
        public RaidPlan Plan;

        [Header("Death"), Range(0f, 24f), Tooltip("Dying during a raid: the player wakes at the next of this hour and the raid is over")]
        public float DefeatWakeHour = 7f;
        [Min(0f), Tooltip("Dying any other time: hours that pass before the player is back")] public float HoursLostOnDeath = 1f;

        [Header("Spawn points"), InlineProperty, HideLabel] public SpawnRules Spawning = new();

        void OnValidate()
        {
            if (StartHour <= DawnHour)
                Debug.LogWarning($"{name}: a raid night must start after dawn's hour (it runs past midnight).", this);
        }
    }
}
