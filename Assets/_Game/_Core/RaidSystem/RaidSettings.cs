using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.RaidSystem
{
    // When raids run, what a death costs in time, and how spawn points are picked. Read-only at runtime.
    [CreateAssetMenu(fileName = "RaidSettings", menuName = "Aipocalypse/Raids/Raid Settings")]
    public sealed class RaidSettings : ScriptableObject
    {
        [Header("Night"), Range(0f, 24f), Tooltip("Hour a raid night starts")] public float StartHour = 20f;
        [Range(0f, 24f), Tooltip("Hour the night ends: raiders still alive despawn out of sight")] public float DawnHour = 6f;
        [Min(1), Tooltip("Most raiders alive at once; groups wait while it's reached")] public int AliveCap = 12;
        [Min(0), Tooltip("Enemies prewarmed beyond AliveCap: the dead keep theirs while their bodies lie (CorpseTime + SinkTime), " +
            "and raiders spawned meanwhile would otherwise be instantiated mid-fight")]
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

    [Serializable]
    public sealed class SpawnRules
    {
        [Min(1), Tooltip("Points sampled in the zone per pick")] public int Candidates = 8;
        [Min(0f), Tooltip("Metres a sampled point may move to reach the navmesh")] public float SnapDistance = 2f;
        [Min(0f), Tooltip("Never spawn closer than this to the player (m)")] public float MinPlayerDistance = 30f;
        [Min(0f), Tooltip("Points farther than this from the player count as unseen even in view (m)")] public float VisibleDistance = 90f;
        [Range(0f, 30f), Tooltip("Degrees added round the camera's field of view when deciding what the player sees")]
        public float ViewMargin = 10f;
        [Min(0f), Tooltip("Seconds a pick waits for a hidden point before taking the visible one farthest from the view centre")]
        public float MaxHiddenWait = 5f;
        [Min(0f), Tooltip("Height (m) above a spawn point the line-of-sight ray aims at (an enemy's chest)")] public float TargetHeight = 1.2f;
        [Tooltip("What finds the ground under a sampled point")] public LayerMask GroundMask = 1;
        [Tooltip("What blocks the player's view of a spawn point (terrain, rocks, buildings)")] public LayerMask SightBlockers = 1;
    }
}
