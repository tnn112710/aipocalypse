using System;
using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
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
        [Tooltip("What swings can hit")] public LayerMask HitLayers = Physics.DefaultRaycastLayers;
        [Min(0f), Tooltip("Stamina each swing costs, hit or miss. Regen keeps running")] public float StaminaPerSwing = 10f;

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
