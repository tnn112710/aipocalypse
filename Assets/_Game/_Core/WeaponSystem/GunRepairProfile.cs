using MB13.Aipocalypse.Core.QuickTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Repair Profile", fileName = "GunRepairProfile")]
    public sealed class GunRepairProfile : ScriptableObject
    {
        [Tooltip("Clearing waits for a reload press instead of starting by itself after Jam Pause")]
        public bool RequireReloadPress;
        [Min(0f), HideIf(nameof(RequireReloadPress)), Tooltip("Seconds the jammed gun stays stuck (trigger only clicks) before clearing starts automatically.")]
        public float JamPause = 0.6f;
        [Min(0.1f), Tooltip("Seconds a quick repair takes, before misses add to it")] public float QuickDuration = 4f;
        [Min(0.1f), Tooltip("Seconds a stubborn repair takes (its own clip)")] public float StubbornDuration = 5f;
        [Range(0f, 1f), Tooltip("Chance a repair is the stubborn one")] public float StubbornChance = 0.5f;
        [Range(0f, 1f), Tooltip("Fraction of repair elapsed before the optional QTE appears. 0.5 = halfway; lower values appear earlier.")]
        public float QteStartProgress = 0.5f;
        [Min(0f), Tooltip("Seconds each missed QTE press adds to the remaining repair.")]
        public float MissPenalty = 0.5f;
        [Min(0f), Tooltip("Most time misses can add to one repair.")]
        public float MaxMissPenalty = 1.5f;
        [Tooltip("The optional QTE that finishes the repair early; none = no QTE")] public QteChallenge Qte;
        [Tooltip("Shared cosmetic preset; duplicate it to customize one gun independently.")]
        public RepairFeedbackSettings Feedback;
        [Tooltip("Show the repair as a shake of the gun, hands staying on. None = the gun's repair clips")]
        public GunShakeSettings Shake;
        [Range(0f, 1f), ShowIf(nameof(Shake)), Tooltip("Chance a repair is shown as the shake; the others play the gun's repair clips (quick: reseat the magazine, stubborn: rack the bolt)")]
        public float ShakeChance = 1f;
    }
}
