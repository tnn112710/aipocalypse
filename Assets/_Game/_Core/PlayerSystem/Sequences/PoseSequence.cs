using System;
using Ami.BroAudio;
using DG.Tweening;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // A scripted first-person moment: the eye moves through beats placed on the ground around where it started (kneel, fall,
    // lie back…), then settles on the last one while the view drifts. Played by PoseSequencePlayer.
    [CreateAssetMenu(fileName = "PoseSequence", menuName = "Aipocalypse/Player/Pose Sequence")]
    public sealed class PoseSequence : ScriptableObject
    {
        [SerializeField, Tooltip("Puts the held item away while it plays")]
        bool _lowerHands = true;
        [SerializeField] PoseBeat[] _beats = Array.Empty<PoseBeat>();
        [SerializeField, Tooltip("On: the eye flows through the beats on one smooth path and never stops (beat eases are ignored, a hold only " +
            "adds time). Off: each beat eases in and out, and holds stand still")]
        bool _continuous;

        [Header("Handheld (whole sequence)"), SerializeField, Tooltip("Small wandering of the view, like a hand-held camera (degrees): x = pitch, y = yaw, z = roll")]
        Vector3 _handheldAngles;
        [SerializeField, Min(0f), Tooltip("Same for the eye position (m)")]
        float _handheldOffset;
        [SerializeField, Min(0.01f), Tooltip("How fast it wanders (about cycles per second)")]
        float _handheldSpeed = 0.35f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the wander takes to build up from the start")]
        float _handheldFadeIn = 0.25f;

        [Header("Settled (last beat reached)"), SerializeField, Tooltip("Slow elliptical circling of the view (degrees): x = pitch, y = yaw. Fades in from the start of the last " +
            "beat's move and never stops")]
        Vector2 _driftAmplitude;
        [SerializeField, Min(0.1f), Tooltip("Seconds per drift circle")]
        float _driftPeriod = 14f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the drift takes to build up, from the start of the last beat's move")]
        float _driftFadeIn = 2f;
        [SerializeField, Tooltip("Seconds after settling until the sequence counts as ended (a fade, a respawn…). Negative = never")]
        float _endAfter = -1f;

        [Header("Music"), SerializeField, Tooltip("Plays from the beat below until the sequence stops")]
        SoundID _music;
        [SerializeField, Min(0), Tooltip("Index of the beat whose move starts the music")]
        int _musicBeat;
        [SerializeField, Min(0f), Tooltip("Seconds after that beat starts")]
        float _musicDelay;
        [SerializeField, Min(0f)] float _musicFadeIn = 1.5f;
        [SerializeField, Min(0f)] float _musicFadeOut = 1f;

        [Header("Placement"), SerializeField, Tooltip("What the eye lands on and is kept out of")]
        LayerMask _groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f), Tooltip("Room kept around the eye near walls (m)")]
        float _headRadius = 0.15f;

        public bool LowerHands => _lowerHands;
        public PoseBeat[] Beats => _beats;
        public bool Continuous => _continuous;
        public Vector3 HandheldAngles => _handheldAngles;
        public float HandheldOffset => _handheldOffset;
        public float HandheldSpeed => _handheldSpeed;
        public float HandheldFadeIn => _handheldFadeIn;
        public Vector2 DriftAmplitude => _driftAmplitude;
        public float DriftPeriod => _driftPeriod;
        public float DriftFadeIn => _driftFadeIn;
        public float EndAfter => _endAfter;
        public SoundID Music => _music;
        public int MusicBeat => _musicBeat;
        public float MusicDelay => _musicDelay;
        public float MusicFadeIn => _musicFadeIn;
        public float MusicFadeOut => _musicFadeOut;
        public LayerMask GroundMask => _groundMask;
        public float HeadRadius => _headRadius;
    }

    // One move of the eye, then an optional hold. Angles in degrees.
    [Serializable]
    public sealed class PoseBeat
    {
        public string Name;
        [Min(0f)] public float Duration = 0.5f;
        [Tooltip("Ignored when the sequence is Continuous")] public Ease Ease = Ease.InOutSine;
        [Tooltip("Eye height above the ground (m)")]
        public float Height = 1.65f;
        [Tooltip("Eye offset (m) from where the sequence started, in the body's facing: x = right, z = forward")]
        public Vector2 Offset;
        [Tooltip("Positive = down")] public float Pitch;
        public float Yaw;
        [Tooltip("Positive = head tilts left, negative = right")] public float Roll;
        [Range(0.5f, PlayerZoom.MaxScale), Tooltip("World field of view multiplier reached at the end of the move")]
        public float FovScale = 1f;
        [Min(0f), Tooltip("Seconds held after arriving (Continuous: extra time before the next beat; the eye keeps moving)")] public float Hold;
        [Tooltip("When the move starts")] public SoundID StartSound;
        [Tooltip("When the move ends")] public SoundID ArriveSound;

        public Vector3 Euler => new(Pitch, Yaw, Roll);
    }
}
