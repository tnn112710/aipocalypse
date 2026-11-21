using System;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Effects;
using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
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
        PerRound, // one round at a time (tube shotgun style); pulling the trigger stops it after the round going in
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
        [Min(0f), Tooltip("Push per bullet along its flight (N·s): rigidbodies move, damageables get it in DamageInfo.Force. Falls off with the damage")]
        public float Knockback = 1f;
        [InlineProperty, Tooltip("Damage and knockback drop with the distance the bullet travelled")]
        public DamageFalloff Falloff = new();
        [Min(1), Tooltip("Bullets per shot; more than 1 is a shotgun. A shot uses one round")] public int Pellets = 1;
        [Range(0f, 45f), Tooltip("Each bullet leaves up to this many degrees off the aim")] public float SpreadAngle;
        [Min(0f), Tooltip("Pump / bolt: seconds working the action after each shot before the next can fire. 0 = self-loading")]
        public float CycleTime;
        [Min(0f), Tooltip("Stamina each shot costs (all pellets count as one). Firing pauses regen")]
        public float StaminaPerShot = 1f;

        public float SecondsPerShot => 60f / Mathf.Max(1f, RoundsPerMinute);
        bool IsBurst => Mode is FireMode.AutoBurst or FireMode.SingleBurst;
    }

    // Full damage over the first EffectiveRangeShare of the bullet's range (the effective range), easing (smoothstep) down to
    // MinShare of it where the bullet vanishes (Bullet → Range).
    [Serializable]
    public sealed class DamageFalloff
    {
        [Range(0f, 1f), Tooltip("Effective range as a share of the bullet's Range: full damage up to this distance, shown as the gun's Range. 0.3 of 300 m = 90 m")]
        public float EffectiveRangeShare = 0.3f;
        [Range(0f, 1f), Tooltip("Share of the damage left when the bullet reaches its Range (Bullet → Range), where it vanishes")]
        public float MinShare = 0.5f;

        // Metres of full damage for a bullet of this range.
        public float EffectiveRange(float range) => EffectiveRangeShare * range;

        public float At(float distance, float range)
        {
            float start = EffectiveRange(range);
            if (distance <= start || range <= start)
                return distance <= start ? 1f : MinShare;
            return Mathf.Lerp(1f, MinShare, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, range, distance)));
        }
    }

    [Serializable]
    public sealed class BulletSettings
    {
        [Min(1f), Tooltip("m/s")] public float Speed = 120f;
        [Min(1f), Tooltip("Bullets vanish after this distance (m); damage reaches Fire → Falloff's minimum here")] public float Range = 150f;
        [Min(0f), Tooltip("0 = flies straight, 1 = normal gravity")] public float GravityScale;
        [Tooltip("What bullets and the crosshair's aim ray can hit. Leave out Debris (dropped magazines) and HeldWeapon")]
        public LayerMask HitLayers = Physics.DefaultRaycastLayers;
        [Tooltip("What its hits count as (resistances, hit reactions, kill credit)")]
        public DamageType DamageType = DamageType.Gunshot;
    }

    [Serializable]
    public sealed class MagazineSettings
    {
        [Tooltip("Reloads take this from the holder's ammo reserve")] public AmmoType AmmoType;
        [Min(1)] public int Size = 30;
        public ReloadType Reload = ReloadType.Magazine;
        [Min(0f), Tooltip("Seconds. Magazine: the whole reload. Per round: each round")] public float ReloadTime = 2f;
        [Tooltip("Reload by itself when the magazine runs dry")] public bool AutoReload = true;
        [Range(0f, 1f), Tooltip("Share of the magazine counted as running low: the last-rounds click and the HUD warning (magazines of 6 or more only)")]
        public float LowAmmoShare = 0.25f;

        // Rounds left at which a magazine of `size` counts as running low (parts change the size); 0 = never (small magazines).
        public int LowRoundsOf(int size) => size >= 6 ? Mathf.FloorToInt(size * LowAmmoShare) : 0;
    }

    [Serializable]
    public sealed class JamSettings
    {
        [Range(0f, 1f), Tooltip("Chance each shot jams instead of firing")] public float Chance = 0.02f;
        [Tooltip("Repair timing, QTE and feedback. None: the jam clears by itself after Clear Time, with no QTE")]
        public GunRepairProfile Repair;
        [Min(0f), ShowIf("@Repair == null"), Tooltip("Seconds to clear a jam without a Repair profile")] public float ClearTime = 1.5f;

        public RepairFeedbackSettings Feedback => Repair != null ? Repair.Feedback : null;
    }

    // Aim kick moves the view, so it moves the crosshair and where shots go. Gun kick is extra motion of the model only,
    // on top of the view it's attached to; keep it small so the barrel stays near the crosshair.
    [Serializable]
    public sealed class RecoilValues
    {
        [Range(0f, 2f)] public float Multiplier = 1f;

        [Header("Aim kick (the view, where shots go)"), Min(0f), Tooltip("Degrees up per shot")] public float Vertical = 3.5f;
        [Min(0f), Tooltip("Degrees sideways per shot, along the spray pattern")] public float Horizontal = 2.5f;
        [Min(0f), Tooltip("Degrees of view roll per shot")] public float Roll = 0.3f;
        [Min(0f), Tooltip("Random extra degrees up / down per shot")] public float NoisePitch = 0.11f;
        [Min(0f), Tooltip("Random extra degrees sideways per shot")] public float NoiseYaw = 0.08f;
        [Min(0f), Tooltip("View shake per shot (m)")] public float CameraShake = 0.01f;

        [Header("Aim response (higher = faster)"), Min(0f)] public float VerticalReturnSpeed = 10f;
        [Min(0f)] public float HorizontalReturnSpeed = 10f;
        [Min(0f)] public float VerticalSnappiness = 10f;
        [Min(0f)] public float HorizontalSnappiness = 10f;

        [Header("Gun kick (model only, on top of the view)"), Min(0f), Tooltip("Extra degrees the muzzle lifts per shot")] public float GunPitch = 1f;
        [Min(0f), Tooltip("Extra degrees sideways per shot, along the spray pattern")] public float GunYaw = 0.5f;
        [Min(0f), Tooltip("Extra degrees of roll per shot")] public float GunRoll = 1f;
        [Min(0f), Tooltip("Metres pushed back per shot")] public float KickBack = 0.06f;
        [Min(0f), Tooltip("How fast the gun kick settles (higher = the barrel stays closer to the crosshair)")] public float GunReturnSpeed = 18f;
        [Min(0f)] public float GunSnappiness = 20f;

        // Blends hip and aiming values into `into` (no allocation), so recoil eases with the aim pose.
        public static void Lerp(RecoilValues hip, RecoilValues aim, float t, RecoilValues into)
        {
            into.Multiplier = Mathf.Lerp(hip.Multiplier, aim.Multiplier, t);
            into.Vertical = Mathf.Lerp(hip.Vertical, aim.Vertical, t);
            into.Horizontal = Mathf.Lerp(hip.Horizontal, aim.Horizontal, t);
            into.Roll = Mathf.Lerp(hip.Roll, aim.Roll, t);
            into.NoisePitch = Mathf.Lerp(hip.NoisePitch, aim.NoisePitch, t);
            into.NoiseYaw = Mathf.Lerp(hip.NoiseYaw, aim.NoiseYaw, t);
            into.CameraShake = Mathf.Lerp(hip.CameraShake, aim.CameraShake, t);
            into.VerticalReturnSpeed = Mathf.Lerp(hip.VerticalReturnSpeed, aim.VerticalReturnSpeed, t);
            into.HorizontalReturnSpeed = Mathf.Lerp(hip.HorizontalReturnSpeed, aim.HorizontalReturnSpeed, t);
            into.VerticalSnappiness = Mathf.Lerp(hip.VerticalSnappiness, aim.VerticalSnappiness, t);
            into.HorizontalSnappiness = Mathf.Lerp(hip.HorizontalSnappiness, aim.HorizontalSnappiness, t);
            into.GunPitch = Mathf.Lerp(hip.GunPitch, aim.GunPitch, t);
            into.GunYaw = Mathf.Lerp(hip.GunYaw, aim.GunYaw, t);
            into.GunRoll = Mathf.Lerp(hip.GunRoll, aim.GunRoll, t);
            into.KickBack = Mathf.Lerp(hip.KickBack, aim.KickBack, t);
            into.GunReturnSpeed = Mathf.Lerp(hip.GunReturnSpeed, aim.GunReturnSpeed, t);
            into.GunSnappiness = Mathf.Lerp(hip.GunSnappiness, aim.GunSnappiness, t);
        }
    }

    [Serializable]
    public sealed class RecoilSettings
    {
        [LabelText("Hip fire"), InlineProperty] public RecoilValues Hip = new();
        [LabelText("Aiming"), InlineProperty] public RecoilValues Aim = new();
        [Range(0f, 1f), Tooltip("Share of the aim kick (up / sideways) that turns the look for good, so the player pulls down against it; the rest springs back")]
        public float KeptShare = 0.25f;
        [Min(0f), Tooltip("Recoil multiplier while moving")] public float MovingMultiplier = 1.15f;
        [Min(0f), Tooltip("Recoil multiplier while in the air")] public float AirborneMultiplier = 1.6f;
        [Min(0f), Tooltip("Recoil multiplier while crouching (stacks with moving)")] public float CrouchMultiplier = 0.8f;
        [Min(0), Tooltip("The first this many shots of a spray kick less (0 = off)")] public int AccurateShots;
        [Range(0f, 1f), ShowIf("@AccurateShots > 0"), Tooltip("Kick multiplier for those first shots")]
        public float AccurateShotsMultiplier = 1f;

        [Header("Field of view punch"), Range(0f, 0.1f), Tooltip("Share the world view widens per shot (0.01 = 1%); 0 = off")]
        public float FovKick = 0.008f;
        [Range(0f, 0.2f), Tooltip("Most the view widens while spraying")] public float FovKickMax = 0.03f;
        [Min(0f), Tooltip("How fast the view settles back (higher = sooner)")] public float FovReturnSpeed = 14f;

        [Header("Spray pattern (randomised per spray)"), Min(0f), Tooltip("Seconds without a shot after which the next one starts a new spray")]
        public float SprayResetDelay = 0.24f;
        [MinMaxSlider(0f, 0.2f, true), Tooltip("Extra upward kick per shot into a spray, as a share of Vertical: picked per spray between these")]
        public Vector2 SprayClimb = new(0.02f, 0.075f);
        [Range(0f, 1f), Tooltip("Steady sideways lean of a spray (share of Horizontal): picked per spray up to this either way")]
        public float SprayDrift = 0.35f;
        [Range(0f, 1f), Tooltip("Shot-to-shot wobble of the upward kick (share of Vertical)")] public float SprayPitchJitter = 0.18f;
        [Range(0f, 1f), Tooltip("Shot-to-shot side-to-side wander (share of Horizontal)")] public float SprayYawJitter = 0.72f;
        [Range(0f, 1f), Tooltip("Roll that follows the sideways kick")] public float SprayRollFromYaw = 0.55f;

        // The holder's stance: in the air overrides; otherwise moving and crouching stack.
        public float StateMultiplier(bool airborne, bool moving, bool crouching) =>
            airborne ? AirborneMultiplier : (moving ? MovingMultiplier : 1f) * (crouching ? CrouchMultiplier : 1f);
    }

    // The hip envelope (GunAccuracy): how far the gun may wobble from the hip cone (shrinking to AimSpread as the sights come
    // up), airborne spread and shot bloom. Shots follow the wobbling barrel; SpreadAngle is the pellet pattern on top.
    [Serializable]
    public sealed class AccuracySettings
    {
        [Min(0f), Tooltip("Degrees the gun may wobble off the aim from the hip, standing still; the crosshair's cross shows it")]
        public float HipSpread = 2.5f;
        [Min(0f), Tooltip("Degrees the gun may wobble when fully aimed down sights (0: the aim sway alone)")] public float AimSpread;
        [Min(0f), Tooltip("Degrees the envelope grows while in the air")] public float AirborneSpread = 2f;
        [Range(0f, 1f), Tooltip("Share of the airborne spread kept while aiming down sights")] public float AimMultiplier = 0.4f;
        [Min(0f), Tooltip("How fast the envelope settles after it shrinks, e.g. landing or stopping (higher = sooner)")]
        public float SettleSpeed = 6f;
        [Min(0f), Tooltip("Degrees each hip shot adds to the envelope (and jolts the gun by)")] public float BloomPerShot = 0.5f;
        [Min(0f), Tooltip("Most degrees shot bloom can add")] public float MaxBloom = 3f;
        [Min(0f), Tooltip("Degrees per second the bloom falls")] public float BloomRecovery = 4f;
        [Min(0f), Tooltip("Seconds without a shot before the bloom starts falling, so full auto keeps building it")]
        public float BloomRecoveryDelay = 0.2f;
    }

    [Serializable]
    public sealed class HandlingSettings
    {
        [Min(0f), Tooltip("How fast the gun moves between the hip and aim poses")] public float AimSpeed = 10f;
        [Min(0f), Tooltip("How fast it moves into and out of the sprint pose")] public float SprintPoseSpeed = 8f;
        [Range(0.5f, 1f), Tooltip("Field of view multiplier while aiming down sights: 0.87 = 13% zoom")] public float AimZoom = 0.87f;
        [Min(0f), Tooltip("Seconds after a sprint ends before it can fire (a click meanwhile is kept and fires then)")]
        public float SprintRecoveryTime = 0.2f;
    }

    // BroAudio entities (Data/Audio/Guns), played 2D for the holder. Unset ids stay silent.
    [Serializable]
    public sealed class GunSounds
    {
        [Range(0f, 1f), Tooltip("Scales every sound of this gun")] public float Volume = 1f;
        [Tooltip("When it starts being raised")] public SoundID Equip;
        public SoundID Fire;
        [Tooltip("The shot ringing out: played once a spray ends (after every shot on a semi-auto)")] public SoundID FireTail;
        [Tooltip("Mechanical click on each of the last rounds (Magazine → Low Ammo Share), louder as the magazine empties")]
        public SoundID LowAmmo;
        public SoundID ReloadStart;
        [Tooltip("Per-round reload: each round")] public SoundID ReloadInsert;
        public SoundID ReloadEnd;
        [Tooltip("The trigger falls on a jammed round")] public SoundID Jam;
        [Tooltip("Each trigger pull while the gun is stuck before clearing")] public SoundID DryFire;
        [Tooltip("Each trigger pull on an empty magazine; empty = the dry fire click")] public SoundID Empty;
        public SoundID ClearJamStart;
        [Tooltip("Guns without repair feedback only")] public SoundID ClearJamEnd;
        [Tooltip("Each clip-authored repair slap (a random-clip entity)")] public SoundID RepairSlap;
        [Tooltip("Each clip-authored repair punch; empty = the slap")] public SoundID RepairPunch;
        [Tooltip("Pump / bolt pulled back after a shot")] public SoundID CycleStart;
        [Tooltip("Pump / bolt pushed home")] public SoundID CycleEnd;
    }

    // Spent cases thrown from the gun's ejection port (HeldGun → Ejection Port; no port or no shell = nothing ejects).
    [Serializable]
    public sealed class EjectionSettings
    {
        [Tooltip("Pooled case with a Rigidbody")] public PooledEffect Shell;
        [Tooltip("Throw in the port's space (m/s): x right, y up, z forward")] public Vector3 Velocity = new(2.2f, 1.4f, -0.3f);
        [Range(0f, 1f), Tooltip("Random share added to or taken off the throw")] public float VelocityJitter = 0.25f;
        [Min(0f), Tooltip("Spin (degrees/s)")] public float Spin = 900f;
        [Tooltip("Pump / bolt guns: the case leaves when the action is worked, not with the shot")] public bool OnCycle;
        [Min(0), Tooltip("Copies made when the gun is bound")] public int Prewarm = 6;
    }
}
