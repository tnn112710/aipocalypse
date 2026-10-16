using Aipocalypse.Core.QuickTimeSystem;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Repair Profile", fileName = "GunRepairProfile")]
    public sealed class GunRepairProfile : ScriptableObject
    {
        public bool RequireReloadPress;
        [Min(0f), Tooltip("Seconds the jammed gun stays stuck (trigger only clicks) before clearing starts automatically.")]
        public float JamPause = 0.6f;
        [Min(0.1f)] public float QuickDuration = 4f;
        [Min(0.1f)] public float StubbornDuration = 5f;
        [Range(0f, 1f)] public float StubbornChance = 0.5f;
        [Range(0f, 1f), Tooltip("Fraction of repair elapsed before the optional QTE appears. 0.5 = halfway; lower values appear earlier.")]
        public float QteStartProgress = 0.5f;
        [Min(0f), Tooltip("Seconds each missed QTE press adds to the remaining repair.")]
        public float MissPenalty = 0.5f;
        [Min(0f), Tooltip("Most time misses can add to one repair.")]
        public float MaxMissPenalty = 1.5f;
        public QteChallenge Qte;
        [Tooltip("Shared cosmetic preset; duplicate it to customize one gun independently.")]
        public RepairFeedbackSettings Feedback;
    }
}
