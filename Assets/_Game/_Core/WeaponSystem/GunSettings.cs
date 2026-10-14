using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Aipocalypse.Core.WeaponSystem
{
    public enum FireMode
    {
        Auto,        // fires while held
        SemiAuto,    // one shot per click
        AutoBurst,   // bursts while held
        SingleBurst, // one burst per click
    }

    public enum ReloadType
    {
        Magazine, // one timed reload refills the magazine
        PerRound, // one round at a time (shotgun style)
    }

    // Where bullets start and which way they go.
    public enum AimOrigin
    {
        Muzzle, // the gun's muzzle: follows the model, its pose and its kick
        Camera, // the screen centre
        MuzzleToCrosshair, // the muzzle, towards whatever the screen centre is on
    }

    [Serializable]
    public sealed class FireSettings
    {
        public FireMode Mode = FireMode.Auto;
        [Min(1f)] public float RoundsPerMinute = 600f;
        [ShowIf(nameof(IsBurst)), Min(1)] public int BurstCount = 3;
        [ShowIf(nameof(IsBurst)), Min(0f), Tooltip("Seconds from a burst's last shot to the next burst (at least one shot interval)")]
        public float BurstCooldown = 0.25f;
        [Min(0f), Tooltip("Per bullet")] public float Damage = 20f;
        [Min(1), Tooltip("Bullets per shot; more than 1 is a shotgun. A shot uses one round")] public int Pellets = 1;
        [Range(0f, 45f), Tooltip("Each bullet leaves up to this many degrees off the aim")] public float SpreadAngle;

        public float SecondsPerShot => 60f / Mathf.Max(1f, RoundsPerMinute);
        bool IsBurst => Mode is FireMode.AutoBurst or FireMode.SingleBurst;
    }

    [Serializable]
    public sealed class BulletSettings
    {
        [Min(1f), Tooltip("m/s")] public float Speed = 120f;
        [Min(1f), Tooltip("Bullets vanish after this distance (m)")] public float Range = 150f;
        [Min(0f), Tooltip("0 = flies straight, 1 = normal gravity")] public float GravityScale;
    }

    [Serializable]
    public sealed class MagazineSettings
    {
        [Tooltip("Reloads take this from the holder's ammo reserve")] public AmmoType AmmoType;
        [Min(1)] public int Size = 30;
        public ReloadType Reload = ReloadType.Magazine;
        [Min(0f), Tooltip("Seconds. Magazine: the whole reload. Per round: each round")] public float ReloadTime = 2f;
        [Tooltip("Reload by itself when the magazine runs dry")] public bool AutoReload = true;
    }

    [Serializable]
    public sealed class JamSettings
    {
        [Range(0f, 1f), Tooltip("Chance each shot jams instead of firing")] public float Chance = 0.02f;
        [Min(0f), Tooltip("Seconds to clear a jam. Clearing starts by itself")] public float ClearTime = 1.5f;
    }

    [Serializable]
    public sealed class RecoilValues
    {
        [Range(0f, 2f)] public float Multiplier = 1f;

        [Header("Gun kick"), Min(0f), Tooltip("Degrees up per shot")] public float Vertical = 6f;
        [Min(0f), Tooltip("Degrees sideways per shot, along the spray pattern")] public float Horizontal = 6f;
        [Min(0f), Tooltip("Metres pushed back per shot")] public float KickBack = 0.1f;

        [Header("Response (higher = faster)"), Min(0f)] public float VerticalReturnSpeed = 10f;
        [Min(0f)] public float HorizontalReturnSpeed = 10f;
        [Min(0f)] public float VerticalSnappiness = 10f;
        [Min(0f)] public float HorizontalSnappiness = 10f;

        [Header("Noise"), Min(0f)] public float NoisePitch = 0.16f;
        [Min(0f)] public float NoiseYaw = 0.24f;

        [Header("Camera"), Min(0f), Tooltip("Share of the gun kick the view follows; 0 keeps the view still")]
        public float CameraRotation = 0.7f;
        [Min(0f)] public float CameraRoll = 0.3f;
        [Min(0f), Tooltip("View shake per shot (m)")] public float CameraShake = 0.01f;
    }

    [Serializable]
    public sealed class RecoilSettings
    {
        [LabelText("Hip fire"), InlineProperty] public RecoilValues Hip = new();
        [LabelText("Aiming"), InlineProperty] public RecoilValues Aim = new();
        [Min(0f), Tooltip("Recoil multiplier while moving")] public float MovingMultiplier = 1.15f;
        [Min(0f), Tooltip("Recoil multiplier while in the air")] public float AirborneMultiplier = 1.6f;
        [Min(0), Tooltip("The first shots of a spray kick less (0 or 1 = off)")] public int AccurateShots;
        [Range(0f, 1f)] public float AccurateShotsMultiplier = 1f;

        public RecoilValues For(bool aiming) => aiming ? Aim : Hip;
    }

    [Serializable]
    public sealed class HandlingSettings
    {
        [Min(0f), Tooltip("How fast the gun moves between the hip and aim poses")] public float AimSpeed = 10f;
        [Min(0f), Tooltip("How fast it moves into and out of the sprint pose")] public float SprintPoseSpeed = 8f;
    }

    // Played on the gun's own AudioSource. Empty clips stay silent.
    [Serializable]
    public sealed class GunSounds
    {
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("When it starts being raised")] public AudioClip Equip;
        public AudioClip Fire;
        public AudioClip ReloadStart;
        [Tooltip("Per-round reload: each round")] public AudioClip ReloadInsert;
        public AudioClip ReloadEnd;
        public AudioClip Jam;
        public AudioClip ClearJamStart;
        public AudioClip ClearJamEnd;
    }
}
