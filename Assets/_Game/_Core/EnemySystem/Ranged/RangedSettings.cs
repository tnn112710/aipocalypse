using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // Tuning of the Ranged subtree (keep a distance, wind up, throw or shoot), read by its tasks. Damage per hit is the
    // AttackDamage stat; target choice, approach pace and the stance radius still come from the kind's MeleeSettings.
    [CreateAssetMenu(fileName = "RangedSettings", menuName = "Aipocalypse/Enemies/Ranged Settings")]
    public sealed class RangedSettings : ScriptableObject
    {
        [BoxGroup("Distance"), MinMaxSlider(1f, 60f, true), Tooltip("Metres from the target it likes to stand: each life picks its own in this band")]
        public Vector2 StandOff = new(10f, 15f);
        [BoxGroup("Distance"), Min(1f), Tooltip("It throws from anywhere inside this many metres (a fence in the way, a target walking off)")]
        public float MaxRange = 20f;
        [BoxGroup("Distance"), Min(0f), Tooltip("Closer than this it backs away to its stand-off distance before throwing again; 0 = it never backs away")]
        public float MinRange = 4f;
        [BoxGroup("Distance"), Min(0f), Tooltip("A target this close (m) is fought hand to hand with the kind's MeleeSettings instead of " +
            "backed away from; it throws again once the target is further off. 0 = it only backs away")]
        public float MeleeRange = 2.5f;
        [BoxGroup("Path"), Tooltip("Layers of its own side: it holds a straight throw while one of them stands in the line (a lob goes over)")]
        public LayerMask Friends = 1 << 23;

        [BoxGroup("Throw"), Tooltip("What it throws")] public ProjectileDefinition Projectile;
        [BoxGroup("Throw"), Min(0.1f), Tooltip("Seconds the throw takes, standing (cover the attack clip)")] public float AttackDuration = 1.6f;
        [BoxGroup("Throw"), Min(0f), Tooltip("Seconds into it the projectile leaves the hand: the wind-up the player can read")]
        public float ReleaseTime = 0.8f;
        [BoxGroup("Throw"), MinMaxSlider(0f, 10f, true), Tooltip("Seconds between two throws, picked each time")]
        public Vector2 Cooldown = new(1.5f, 3f);
        [BoxGroup("Throw"), Min(0f), Tooltip("Height above the target's feet it aims at (m)")] public float AimHeight = 1.1f;

        [BoxGroup("Path"), Min(1f), Tooltip("Speed of a straight throw (m/s), used when nothing stands between it and the target")]
        public float StraightSpeed = 24f;
        [BoxGroup("Path"), MinMaxSlider(10f, 80f, true), Tooltip("Degrees above the ground a lob leaves at, picked each time: no two arcs are the same")]
        public Vector2 LobAngle = new(32f, 52f);
        [BoxGroup("Path"), Min(1f), Tooltip("A lob needing more than this speed (m/s) isn't thrown: the target is too far or too high")]
        public float MaxLobSpeed = 30f;
        [BoxGroup("Path"), Range(0f, 1f), Tooltip("Chance it lobs even with a clear line (variety, and harder to read)")]
        public float LobAnyway = 0.15f;
        [BoxGroup("Path"), Tooltip("What blocks a throw: checked along the straight line and along a lob")]
        public LayerMask Obstacles = Physics.DefaultRaycastLayers;

        [BoxGroup("Accuracy"), Range(0f, 15f), Tooltip("Degrees a throw may go off its aim (a cone)")] public float Spread = 2.5f;
        [BoxGroup("Accuracy"), MinMaxSlider(0f, 1.5f, true), Tooltip("Share of the target's movement it leads by, picked each time: " +
            "0 = aims where the target is, 1 = where it will be if it keeps going")]
        public Vector2 Lead = new(0.3f, 0.9f);

        [BoxGroup("Sound"), Tooltip("3D at the thrower when the wind-up starts (a bow drawn, a grunt)")] public SoundID WindUp;
        [BoxGroup("Sound"), Tooltip("3D at the thrower when the projectile leaves")] public SoundID Launch;

        void OnValidate()
        {
            ReleaseTime = Mathf.Min(ReleaseTime, AttackDuration);
            MaxRange = Mathf.Max(MaxRange, StandOff.y);
            MinRange = Mathf.Min(MinRange, StandOff.x * 0.9f);
        }
    }
}
