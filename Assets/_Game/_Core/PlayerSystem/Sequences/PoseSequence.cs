using System;
using Ami.BroAudio;
using DG.Tweening;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    public enum PoseSpace
    {
        // Beats sit on the ground around where the sequence started (dying, kneeling, lying in the grass).
        Feet,
        // Beats are exact, in an anchor's space (a seat: x right, y up, z the way it faces).
        Anchor,
    }

    // A scripted first-person moment: the eye moves through beats (kneel, fall, sit down, lean back…), then settles on the
    // last one, where the player may look around a little while the view drifts. Played by PoseSequencePlayer.
    [CreateAssetMenu(fileName = "PoseSequence", menuName = "Aipocalypse/Player/Pose Sequence")]
    public sealed class PoseSequence : ScriptableObject
    {
        [field: SerializeField, Tooltip("Feet: beats are placed on the ground around the start. Anchor: in the anchor's space, exact")]
        public PoseSpace Space { get; private set; } = PoseSpace.Feet;
        [field: SerializeField, Tooltip("Puts the held item away while it plays")]
        public bool LowerHands { get; private set; } = true;
        [field: SerializeField] public PoseBeat[] Beats { get; private set; } = Array.Empty<PoseBeat>();
        [field: SerializeField, Tooltip("On: the eye flows through the beats on one smooth path and never stops (beat eases are ignored, a hold only " +
            "adds time). Off: each beat eases in and out, and holds stand still")]
        public bool Continuous { get; private set; }

        [field: Header("Handheld (whole sequence)"), SerializeField, Tooltip("Small wandering of the view, like a hand-held camera (degrees): x = pitch, y = yaw, z = roll")]
        public Vector3 HandheldAngles { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("Same for the eye position (m)")]
        public float HandheldOffset { get; private set; }
        [field: SerializeField, Min(0.01f), Tooltip("How fast it wanders (about cycles per second)")]
        public float HandheldSpeed { get; private set; } = 0.35f;

        [field: Header("Settled (last beat reached)"), SerializeField, Tooltip("Free look around the last pose (degrees): x = pitch, y = yaw. 0 = locked")]
        public Vector2 LookRange { get; private set; }
        [field: SerializeField, Tooltip("Slow elliptical circling of the view (degrees): x = pitch, y = yaw. Fades in from the start of the last " +
            "beat's move and never stops")]
        public Vector2 DriftAmplitude { get; private set; }
        [field: SerializeField, Min(0.1f), Tooltip("Seconds per drift circle")]
        public float DriftPeriod { get; private set; } = 14f;
        [field: SerializeField, Tooltip("Seconds after settling until the sequence counts as ended (a fade, a respawn…). Negative = never")]
        public float EndAfter { get; private set; } = -1f;

        [field: Header("Music"), SerializeField, Tooltip("Plays from the beat below until the sequence stops")]
        public SoundID Music { get; private set; }
        [field: SerializeField, Min(0), Tooltip("Index of the beat whose move starts the music")]
        public int MusicBeat { get; private set; }
        [field: SerializeField, Min(0f), Tooltip("Seconds after that beat starts")]
        public float MusicDelay { get; private set; }
        [field: SerializeField, Min(0f)] public float MusicFadeIn { get; private set; } = 1.5f;
        [field: SerializeField, Min(0f)] public float MusicFadeOut { get; private set; } = 1f;

        [field: Header("Placement (Feet)"), SerializeField, Tooltip("What the eye lands on and is kept out of")]
        public LayerMask GroundMask { get; private set; } = Physics.DefaultRaycastLayers;
        [field: SerializeField, Min(0.01f), Tooltip("Room kept around the eye near walls (m)")]
        public float HeadRadius { get; private set; } = 0.15f;
    }

    // One move of the eye, then an optional hold. Angles in degrees.
    [Serializable]
    public sealed class PoseBeat
    {
        public string Name;
        [Min(0f)] public float Duration = 0.5f;
        [Tooltip("Ignored when the sequence is Continuous")] public Ease Ease = Ease.InOutSine;
        [Tooltip("Feet: eye height above the ground. Anchor: height above the anchor (m)")]
        public float Height = 1.65f;
        [Tooltip("Eye offset (m): x = right, z = forward. Feet: from where the sequence started, in the body's facing")]
        public Vector2 Offset;
        [Tooltip("Positive = down")] public float Pitch;
        public float Yaw;
        [Tooltip("Positive = head tilts left, negative = right")] public float Roll;
        [Range(0.5f, 1.5f), Tooltip("World field of view multiplier reached at the end of the move")]
        public float FovScale = 1f;
        [Min(0f), Tooltip("Seconds held after arriving (Continuous: extra time before the next beat; the eye keeps moving)")] public float Hold;
        [Tooltip("When the move starts")] public SoundID StartSound;
        [Tooltip("When the move ends")] public SoundID ArriveSound;

        public Vector3 Euler => new(Pitch, Yaw, Roll);
    }
}
