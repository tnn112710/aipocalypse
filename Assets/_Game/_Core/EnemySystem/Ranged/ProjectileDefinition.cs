using Ami.BroAudio;
using MB13.Aipocalypse.Core.Effects;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Something thrown or shot at a target (a spear, an arrow): how it flies, what it looks and sounds like. Flown by
    // ProjectileSystem; who throws it and how well is in RangedSettings.
    [CreateAssetMenu(fileName = "Projectile", menuName = "Aipocalypse/Enemies/Projectile")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        [BoxGroup("Flight"), Min(0f), Tooltip("m/s²; the same value aims the throw")] public float Gravity = 9.81f;
        [BoxGroup("Flight"), Min(0.01f), Tooltip("Radius of the hit check along its path (m): bigger is easier to be hit by")]
        public float Radius = 0.12f;
        [BoxGroup("Flight"), Min(0.5f), Tooltip("Gone after this many seconds in the air")] public float MaxFlightTime = 8f;
        [BoxGroup("Flight"), Tooltip("What it can hit. Leave out Enemy (no friendly fire), Debris, HeldWeapon and Item")]
        public LayerMask HitLayers = Physics.DefaultRaycastLayers;
        [BoxGroup("Flight"), Min(0f), Tooltip("Push on what it hurts (N·s)")] public float Knockback = 2f;

        [BoxGroup("Look"), Tooltip("Pooled model with its trail, pointing along its forward (blue axis). Its PooledEffect lifetime is 0: " +
            "the system returns it")]
        public PooledEffect Visual;
        [BoxGroup("Look"), Min(0f), Tooltip("Seconds it stays stuck where it landed (0 = gone at once). Never sticks in what it hurt")]
        public float StickSeconds = 6f;
        [BoxGroup("Look"), Min(0f), Tooltip("Metres its tip sinks into what it lands on")] public float StickDepth = 0.15f;
        [BoxGroup("Look"), Tooltip("Shown on the ground where a lobbed shot will land, until it does; empty = none")]
        public PooledEffect LandingMarker;
        [BoxGroup("Look"), Min(0), Tooltip("Copies made when the first one is thrown")] public int Prewarm = 6;

        [BoxGroup("Sound"), Tooltip("3D, where it is, once it's about to pass the listener: the warning whistle")] public SoundID FlyBy;
        [BoxGroup("Sound"), Min(0f), Tooltip("It whistles when its path passes within this many metres of the listener")]
        public float FlyByRadius = 5f;
        [BoxGroup("Sound"), Min(0f), Tooltip("... and it will be there within this many seconds")] public float FlyByLead = 0.6f;
        [BoxGroup("Sound"), Tooltip("3D, where it lands on the world")] public SoundID Impact;
        [BoxGroup("Sound"), Tooltip("3D, where it hurts something")] public SoundID Hit;
    }
}
