using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // Jam clearing shown as a shake: both hands stay on the gun, it tilts to show the port and is jerked a few times.
    // Set on a gun's GunRepairProfile; guns without one play their repair clips (or nothing).
    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Gun Shake", fileName = "GunShake")]
    public sealed class GunShakeSettings : ScriptableObject
    {
        [BoxGroup("Tilt"), Tooltip("Held while clearing: offset of the gun in its own space (m)")] public Vector3 TiltPosition = new(-0.05f, 0.05f, -0.02f);
        [BoxGroup("Tilt"), Tooltip("Degrees; z rolls the gun so the ejection port shows")] public Vector3 TiltEuler = new(-8f, -6f, 28f);
        [BoxGroup("Tilt"), Range(0.01f, 0.4f), Tooltip("Share of the repair spent tilting in, and again tilting back")] public float TiltShare = 0.12f;

        [BoxGroup("Shakes"), Min(1)] public int QuickShakes = 3;
        [BoxGroup("Shakes"), Min(1)] public int StubbornShakes = 4;
        [BoxGroup("Shakes"), Tooltip("Peak of one shake, added to the tilt (m)")] public Vector3 ShakePosition = new(0f, -0.035f, 0.05f);
        [BoxGroup("Shakes"), Tooltip("Degrees at the peak")] public Vector3 ShakeEuler = new(9f, 2f, -5f);
        [BoxGroup("Shakes"), Range(0.02f, 0.5f), Tooltip("Share of the repair one shake rings for")] public float ShakeShare = 0.16f;
        [BoxGroup("Shakes"), Range(0.5f, 4f), Tooltip("Back-and-forth swings in one shake")] public float Swings = 1.75f;
        [BoxGroup("Shakes"), Range(0f, 0.6f), Tooltip("Pull the other way before each shake, as a share of its peak")] public float WindUp = 0.3f;
        [BoxGroup("Shakes"), Min(1f), Tooltip("A stubborn repair's even shakes are this much harder (and sound like the punch)")] public float HardScale = 1.5f;

        [Min(0.01f), Tooltip("Seconds back to rest when the repair ends early")] public float ReturnTime = 0.22f;

        // The strike itself (the snap from the wind-up to the peak), as a share of one shake.
        public const float Strike = 0.12f;

        public int Shakes(bool stubborn) => stubborn ? StubbornShakes : QuickShakes;

        // Progress (0..1) at which shake `index` starts: spread evenly between the tilt in and the tilt back.
        public float StartAt(int index, int count) => Mathf.Lerp(TiltShare, 1f - TiltShare - ShakeShare, count > 1 ? index / (count - 1f) : 0.5f);

        // Progress at which it lands (its sound and beat).
        public float BeatAt(int index, int count) => StartAt(index, count) + ShakeShare * Strike;

        // Shake `index` of a stubborn repair is a hard one.
        public bool IsHard(int index, bool stubborn) => stubborn && index % 2 == 1;
    }
}
