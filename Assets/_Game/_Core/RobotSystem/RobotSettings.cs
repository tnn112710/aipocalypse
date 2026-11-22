using System;
using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // Service robots as a whole: the switch, their day, how they idle, stroll, greet, answer the whistle and react, their
    // faces and beeps. Asset in Data/Robots/.
    [CreateAssetMenu(fileName = "RobotSettings", menuName = "Aipocalypse/Robots/Robot Settings")]
    public sealed class RobotSettings : ScriptableObject
    {
        // How lively a robot is before its own personality spread.
        [Serializable]
        public struct Temper
        {
            [Range(0f, 1f), Tooltip("Chance it takes a short stroll after idling")] public float StrollChance;
            [Range(0f, 1f), Tooltip("Chance a greeting is a wave (else it talks)")] public float WaveChance;
            [Range(0f, 1f), Tooltip("Chance an idle ends with a little dance or laugh")] public float FlourishChance;
            [Min(0.1f), Tooltip("Scales how long it idles")] public float Patience;
        }

        [Tooltip("Off: robots stand still and offer nothing")] public bool Enabled = true;
        [Min(0.02f), Tooltip("Seconds between a robot's thinks")] public float ThinkInterval = 0.2f;
        [Tooltip("Awake inside these hours; standby at their spot outside them")] public TimeWindow Awake = new(6f, 20f);

        [Header("Personality")]
        public Temper BaseTemper = new() { StrollChance = 0.25f, WaveChance = 0.6f, FlourishChance = 0.05f, Patience = 1f };
        [Range(0f, 0.9f), Tooltip("Each robot's temper is the base times 1 ± this, rolled once")] public float PersonalitySpread = 0.3f;

        [Header("Home life")]
        [Tooltip("Seconds it idles between moves (min, max), times its patience")] public Vector2 IdleTime = new(4f, 9f);
        [Min(0.5f), Tooltip("Strolls end within this far of its spot (m)")] public float StrollRadius = 4f;
        [Min(0f), Tooltip("Seconds a walk may take beyond 1.5× its straight-line time before it gives up")] public float TravelTimeout = 4f;
        [Min(1), Tooltip("Tries to find a reachable point for a walk")] public int DestinationAttempts = 8;
        [Min(0.1f), Tooltip("Walking pace (m/s): near the walk clip's own pace at the robot's size")] public float WalkSpeed = 0.62f;
        [Min(0.1f), Tooltip("Trotting pace when called or stepping aside (m/s)")] public float TrotSpeed = 1.3f;
        [Min(1f), Tooltip("How fast it turns to face something (degrees per second)")] public float FaceTurnSpeed = 240f;

        [Header("Greeting")]
        [Min(0f), Tooltip("It notices the player this close (m): head turns, greetings hold")] public float NoticeRadius = 5f;
        [Min(0f), Tooltip("Seconds a greeting lasts after the player stops looking")] public float GreetHold = 2.5f;
        [Min(0f), Tooltip("Seconds before it waves again (it talks instead)")] public float WaveCooldown = 60f;

        [Header("Whistle")]
        [Min(0.5f), Tooltip("Stops this far from the player (m)")] public float ArriveDistance = 1.8f;
        [Min(0f), Tooltip("Seconds it waits by the player before going home")] public float CallWait = 20f;
        [Min(1f), Tooltip("Goes home early when the player walks this far from where they whistled (m)")] public float LeaveDistance = 8f;
        [Min(0f), Tooltip("Whistled from outside the yard, it stops this far inside the fence (m)")] public float YardMargin = 1.2f;

        [Header("Reactions")]
        [Min(0f), Tooltip("It steps aside from the player this close (m)")] public float BumpRadius = 0.9f;
        [Min(0.5f), Tooltip("How far it steps aside (m)")] public float StepAside = 1.5f;
        [Min(0f), Tooltip("A gun pointed at it this close makes it nervous (m)")] public float NervousRadius = 6f;
        [Range(1f, 30f), Tooltip("How close to its head the gun must point (degrees)")] public float NervousAngle = 6f;
        [Min(0f), Tooltip("Seconds before it gets nervous again")] public float NervousCooldown = 8f;
        [Min(0.1f), Tooltip("Seconds a hit stops it")] public float HitHold = 1.2f;

        [Header("Face")]
        [Tooltip("Seconds between blinks (min, max)")] public Vector2 BlinkInterval = new(2.5f, 6f);
        [Min(0.02f), Tooltip("Seconds a blink lasts")] public float BlinkTime = 0.15f;
        [Tooltip("Seconds between small changes of look while idle (min, max)")] public Vector2 MoodInterval = new(6f, 14f);

        [Header("Head")]
        [Range(0f, 120f), Tooltip("How far its head turns left or right toward the player (degrees)")] public float HeadYawLimit = 70f;
        [Range(0f, 80f), Tooltip("How far its head tilts up or down (degrees)")] public float HeadPitchLimit = 30f;
        [Min(0.1f), Tooltip("How quickly the head follows (higher = snappier)")] public float HeadTurnSpeed = 6f;

        [Header("Beeps")]
        [Min(0f), Tooltip("Seconds between one robot's beeps")] public float BeepGap = 1.5f;
        [Min(0f), Tooltip("Seconds between any two robots' beeps")] public float SharedBeepGap = 0.5f;
        [SerializeField, Tooltip("One beep per moment (3D, at the robot)")] public Dictionary<RobotSound, SoundID> Sounds = new();
    }
}
