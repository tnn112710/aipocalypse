using System;
using Ami.BroAudio;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    [Serializable]
    public sealed class SwingSettings
    {
        [Min(0f)] public float Damage = 35f;
        [Min(0.1f), Tooltip("Reach from the eye (m)")] public float Range = 2f;
        [Min(0f), Tooltip("Radius of the hit check (m); bigger forgives aim more")] public float Radius = 0.25f;
        [Min(0f), Tooltip("Seconds from the click to the hit")] public float WindUp = 0.18f;
        [Min(0f), Tooltip("Seconds after the hit before the next swing can start")] public float Recovery = 0.42f;
        [Min(0f), Tooltip("Push on what it hits (N·s): rigidbodies move, damageables get it in DamageInfo.Force")]
        public float Knockback = 4f;
        [Min(1), Tooltip("Most things one swing hurts (cleave), nearest first. A wall in the way stops it")] public int MaxTargets = 1;
        [Tooltip("What swings can hit")] public LayerMask HitLayers = Physics.DefaultRaycastLayers;
        [Min(0f), Tooltip("Stamina each swing costs, hit or miss. Swinging pauses regen")] public float StaminaPerSwing = 10f;
        [Min(0f), Tooltip("Swings within this many seconds of the last one alternate sides; after a longer pause the next starts on the first side")]
        public float AlternateResetDelay = 0.5f;

        [Header("Swing motion (first-person model)"), Min(0.01f), Tooltip("Seconds from wind-up to follow-through; the hit lands halfway")]
        public float StrikeTime = 0.1f;
        [Tooltip("Every other swing in a row comes from the other side (poses mirrored around Idle)")]
        public bool AlternateSides = true;
        [Min(0f), Tooltip("How fast it moves into and out of the sprint pose")]
        public float PoseSpeed = 10f;

        [Header("Hit feel"), Min(0f), Tooltip("Degrees the view jolts when a swing lands (sideways with the swing, and down)")]
        public float HitViewKick = 1.6f;
        [Min(0f), Tooltip("How fast the view settles after the jolt (higher = sooner)")] public float HitKickReturn = 14f;

        public float Duration => WindUp + Recovery;
    }

    // BroAudio entities (Data/Audio/Melee), played 2D for the holder. Unset ids stay silent.
    [Serializable]
    public sealed class MeleeSounds
    {
        [Range(0f, 1f), Tooltip("Scales every sound of this weapon")] public float Volume = 1f;
        [Tooltip("When it starts being raised")] public SoundID Equip;
        public SoundID Swing;
        [Tooltip("Hit something that takes damage")] public SoundID HitFlesh;
        [Tooltip("Hit anything else")] public SoundID HitSurface;
    }
}
