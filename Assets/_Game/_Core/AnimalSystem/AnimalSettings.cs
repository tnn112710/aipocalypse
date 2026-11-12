using Ami.BroAudio;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.AnimalSystem
{
    // Farm animals as a whole: the switch, the waking day, how Tokens are handed over, hurrying and the pen's sounds.
    // Asset in Data/Animals/.
    [CreateAssetMenu(fileName = "AnimalSettings", menuName = "Aipocalypse/Animals/Animal Settings")]
    public sealed class AnimalSettings : ScriptableObject
    {
        [Tooltip("Off: animals stand still, earn nothing, and the gate and bell offer nothing")] public bool Enabled = true;
        [Min(0.02f), Tooltip("Seconds between an animal's thinks")] public float ThinkInterval = 0.2f;
        [Tooltip("Awake and earning inside these hours; asleep where they stand outside them")] public TimeWindow Awake = new(6f, 20f);

        [Header("Tokens")]
        [Tooltip("What animals earn")] public CurrencyDefinition Tokens;
        [Min(1), Tooltip("The Tokens burst out as up to this many pickups")] public int BurstPieces = 4;
        [Min(0.1f), Tooltip("Seconds the Tokens take to arc from the animal to the player")] public float BurstFlightTime = 0.9f;
        [Min(0f), Tooltip("The pieces land this far round the player (m): inside the pickup magnet")] public float BurstScatter = 0.45f;
        [Tooltip("When an animal hands its Tokens over (3D)")] public SoundID CollectSound;

        [Header("Coming home")]
        [Min(0f), Tooltip("Hurrying one animal hurries every animal this close to it (m)")] public float HurryRadius = 10f;
        [Min(0.5f), Tooltip("A called animal that can't get into the pen waits this far from its gate (m)")] public float WaitRadius = 3f;
        [Min(0.5f), Tooltip("The pen gate won't close on an animal this close to it (m)")] public float DoorwayRadius = 1.6f;

        [Header("Sounds")]
        public SoundID BellSound;
        public SoundID GateOpenSound;
        public SoundID GateCloseSound;
    }
}
