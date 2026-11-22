using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The companion dog as a whole: the switch, its name and prompts, how it follows, settles, answers the whistle,
    // stays, and how often it makes noise. Asset in Data/Dogs/.
    [CreateAssetMenu(fileName = "DogSettings", menuName = "Aipocalypse/Dogs/Dog Settings")]
    public sealed class DogSettings : ScriptableObject
    {
        [Tooltip("Off: the dog is never activated; no wheel, no whistle answer")] public bool Enabled = true;
        [Tooltip("Its name over its head and in prompts (\"Loki\")")] public LocalizedString Name;
        [Tooltip("Tap E prompt's verb (\"Orders\")")] public LocalizedString OrdersVerb;
        [Tooltip("Hold E prompt's verb (\"Pet\")")] public LocalizedString PetVerb;
        [Tooltip("Whistle prompt's verb (\"Call\")")] public LocalizedString CallVerb;
        [Tooltip("Wheel slice labels")] public LocalizedString FollowLabel, StayLabel, PetLabel;
        [Tooltip("Shown with its name tag and on the HUD")] public Sprite Icon;
        [Min(0.02f), Tooltip("Seconds between its thinks")] public float ThinkInterval = 0.1f;
        [Tooltip("Awake inside these hours; at home it sleeps in its dog house outside them")] public TimeWindow Awake = new(6f, 20f);

        [Header("Speeds (m/s; the clips' own: walk 0.56, trot 1.36, canter 3.5, run 4.41, sprint 5.78)")]
        [Min(0.1f)] public float WalkSpeed = 0.9f;
        [Min(0.1f)] public float TrotSpeed = 1.8f;
        [Min(0.1f), Tooltip("Faster than the player's walk (3 m/s)")] public float CanterSpeed = 3.8f;
        [Min(0.1f), Tooltip("Faster than the player's sprint (6 m/s)")] public float SprintSpeed = 7.5f;

        [Header("Following")]
        [Min(0f), Tooltip("Inside this it's free and does its own thing (m)")] public float InnerRadius = 3f;
        [Min(0f), Tooltip("Past this it comes back toward the inner ring (m)")] public float OuterRadius = 8f;
        [Min(0f), Tooltip("Past this it sprints back (m)")] public float SprintRadius = 15f;
        [Min(0f), Tooltip("Past this it reappears near the player, out of view (m)")] public float CatchUpRadius = 40f;
        [Min(0f), Tooltip("Seconds with no path before it reappears near the player")] public float NoPathTime = 3f;
        [Min(0f), Tooltip("It reappears this far behind the player (m)")] public float ReappearMin = 10f;
        [Min(0f)] public float ReappearMax = 15f;
        [Tooltip("Seconds before it reacts to the player starting or stopping (min, max)")] public Vector2 ReactionDelay = new(0.3f, 0.8f);
        [Min(0f), Tooltip("Seconds of walking one way before it scouts ahead")] public float SteadyTime = 2f;
        [Tooltip("How far ahead it scouts (m, min, max)")] public Vector2 Lead = new(4f, 7f);
        [Tooltip("Seconds before it swaps sides (min, max)")] public Vector2 SideSwap = new(20f, 40f);
        [Range(0f, 45f), Tooltip("While the player aims it stays out of this cone round the camera's forward (degrees)")] public float AimCone = 12f;
        [Min(0f), Tooltip("And this much further out (m)")] public float AimClearance = 2f;

        [Header("Free time")]
        [Min(0f), Tooltip("Sniff points are picked within this of the dog (m), inside the outer ring")] public float SniffRadius = 6f;
        [Tooltip("Player still this long: it sits (s, min, max)")] public Vector2 SitAfter = new(3f, 6f);
        [Tooltip("Then it lies down (s, min, max)")] public Vector2 LieAfter = new(15f, 25f);
        [Min(0f), Tooltip("Then it naps (s)")] public float NapAfter = 60f;
        [Tooltip("Seconds between check-ins (min, max)")] public Vector2 CheckIn = new(30f, 60f);
        [Min(0f), Tooltip("A check-in stops this far in front of the player (m)")] public float CheckInDistance = 1.5f;
        [Min(0f), Tooltip("Water deeper than this (m) and it swims")] public float SwimDepth = 0.45f;
        [Min(0f), Tooltip("Looked at within this (m)...")] public float LookRange = 10f;
        [Min(0f), Tooltip("...for this long (s): a head tilt")] public float LookTime = 1f;
        [Min(0f), Tooltip("Seconds between head tilts")] public float HeadTiltGap = 20f;

        [Header("Whistle")]
        [Min(0f), Tooltip("A plain whistle within this calls it (m)")] public float HearWhistleRange = 60f;
        [Min(2), Tooltip("This many plain whistles...")] public int SpamWhistles = 4;
        [Min(0f), Tooltip("...within this many seconds annoy it (never in a fight)")] public float SpamWindow = 5f;
        [Min(0f), Tooltip("Seconds it ignores plain whistles once annoyed")] public float AnnoyedTime = 6f;

        [Header("Stay")]
        [Min(0f), Tooltip("It keeps within this of where it was told to stay (m)")] public float StayRadius = 4f;
        [Min(0f), Tooltip("Player farther than this from a dog staying out in the world: it walks home (m)")] public float StayGoHomeDistance = 150f;

        [Header("Command wheel")]
        [Range(0.05f, 1f), Tooltip("Time scale while the wheel is open")] public float WheelTimeScale = 0.3f;

        [Header("Voice")]
        [Min(0f), Tooltip("At most one sound per this many seconds")] public float VoiceGap = 1.2f;
        [Min(0f)] public float BarkGap = 6f;
        [Min(1), Tooltip("Barks within any minute")] public int BarkPerMinute = 3;
        [Min(0f)] public float WhineGap = 8f;
        [Min(0f)] public float YelpGap = 1f;
        [SerializeField, Tooltip("One sound per moment (3D, at the dog)")] public Dictionary<DogSound, SoundID> Sounds = new();
    }
}
