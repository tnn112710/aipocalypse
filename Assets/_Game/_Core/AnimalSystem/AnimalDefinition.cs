using Ami.BroAudio;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // One farm animal species (cow, sheep): how it moves, grazes, earns and sounds. Lives in Content/Animals/<Name>/.
    [CreateAssetMenu(fileName = "Animal", menuName = "Aipocalypse/Animals/Animal Definition")]
    public sealed class AnimalDefinition : ScriptableObject
    {
        public string DisplayName = "Animal";

        [Header("Movement")]
        [Min(0.1f), Tooltip("Grazing and walking home (m/s); set near the walk clip's own pace")] public float WalkSpeed = 1f;
        [Min(0.1f), Tooltip("Hurried home by the player (m/s)")] public float HurrySpeed = 3f;
        [Min(0f)] public float StopDistance = 0.4f;
        [Min(0f), Tooltip("Grass bends this far round it (m); 0 = none")] public float GrassBendRadius = 1f;

        [Header("Grazing")]
        [Min(0.5f), Tooltip("Picks its next spot this far from where it grazes: the pasture's middle, its flock's leader, or itself (m)")]
        public float GrazeRadius = 12f;
        [Min(0f), Tooltip("Skips spots closer than this (m)")] public float MinTravel = 2f;
        [Tooltip("Seconds it eats at a spot before moving on (min, max)")] public Vector2 GrazeTime = new(4f, 10f);
        [Min(1f), Tooltip("Gives up on a spot after this many seconds of walking")] public float TravelTimeout = 60f;
        [Min(1)] public int DestinationAttempts = 8;

        [Header("Flock")]
        [Tooltip("Grazes round the first of its kind (the leader) instead of anywhere in the pasture, and hurries when any of its kind is hurried")]
        public bool FollowsLeader;
        [Min(0.5f), Tooltip("How far from the leader a follower grazes (m)")] public float FlockRadius = 5f;

        [Header("Tokens")]
        [Min(0f), Tooltip("Tokens earned over one waking day in the pen")] public float TokensPerDayPenned = 10f;
        [Min(0f), Tooltip("Tokens earned over one waking day out on the pasture")] public float TokensPerDayRoaming = 20f;
        [Min(0f), Tooltip("Holds at most this many roaming days of Tokens, then stops earning until collected")] public float HoldDays = 2f;
        [Min(1), Tooltip("Shows its coin marker, and hands its Tokens over, from this many")] public int CollectAt = 3;

        [Header("Sounds")]
        [Tooltip("Its call now and then while awake (3D)")] public SoundID Call;
        [Tooltip("Seconds between calls (min, max)")] public Vector2 CallInterval = new(12f, 30f);
        [Tooltip("When it is hurried (3D)")] public SoundID Hurried;

        public float MaxHeld => HoldDays * TokensPerDayRoaming;
        public float TokensPerDay(bool roaming) => roaming ? TokensPerDayRoaming : TokensPerDayPenned;
    }
}
