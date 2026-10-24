using MalbersAnimations;
using MalbersAnimations.Scriptables;
using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Rarity tier for a fish, used by FishableArea to weight random selection.
    /// </summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare
    }

    [HelpURL("https://sunfish-studios.gitbook.io/fishing-for-animal-controller/main-components/fish-data")]
    [CreateAssetMenu(fileName = "New Fish", menuName = "Malbers Animations/Fishing/Fish Data")]
    public class FishData : ScriptableObject
    {
        [Header("Identity")]
        public GameObject FishPrefab;
        public string fishName;
        public Sprite fishIcon;
        [Tooltip("Rarity tier — controls how likely this fish is to be selected from the fishing pool.")]
        public Rarity rarity = Rarity.Common;

        [Header("Holder")]
        [Tooltip("Additional local position offset applied to this fish while it is held, on top of the Fish Joint's Holder Offset.")]
        public Vector3 holderOffset;
        [Tooltip("When enabled, locks this fish's current pose to the holder when the presentation ends and the store animation begins.")]
        public bool lockFishBodyToHolderDuringStore = true;

        public virtual float Weight
        {
            get
            {
                float value = Random.Range(minWeight, maxWeight);
                if (debug) { Debug.Log("Weight: " + value); }
                return value;
            }
        }
        public virtual float Nibbles
        {
            get
            {
                float value = Random.Range(minNibbles, maxNibbles);
                if (debug) { Debug.Log("Nibbles: " + value); }
                return value;
            }
        }
        public virtual float NibbleTimer
        {
            get
            {
                float value = Random.Range(minTimeBetweenNibbles, maxTimeBetweenNibbles);
                if (debug) { Debug.Log("NibbleTimer: " + value); }
                return value;
            }
        }
        public virtual float StrikeTimer
        {
            get
            {
                float value = Random.Range(minStrikeTime, maxStrikeTime);
                if (debug) { Debug.Log("StrikeTimer: " + value); }
                return value;
            }
        }
        public virtual float FleeSpeed
        {
            get
            {
                float value = fleeSpeed;
                if (debug) { Debug.Log("FleeSpeed: " + fleeSpeed.Value); }
                return value;
            }
        }
        public virtual float StruggleDuration
        {
            get
            {
                float value = Random.Range(minStruggleStageDuration, maxStruggleStageDuration);
                if (debug) { Debug.Log("StruggleDuration: " + value); }
                return value;
            }
        }
        public virtual float TiredDuration
        {
            get
            {
                float value = Random.Range(minTiredStageDuration, maxTiredStageDuration);
                if (debug) { Debug.Log("TiredDuration: " + value); }
                return value;
            }
        }
        public virtual float FleeRange
        {
            get
            {
                float value = Random.Range(fleeRangeAngle.Min, fleeRangeAngle.Max);
                if (debug) { Debug.Log("FleeRange: " + value); }
                return value;
            }
        }



        [Header("Weight")]
        [SerializeField] public FloatReference minWeight = 2;
        [SerializeField] public FloatReference maxWeight = 6;

        [Header("Timing")]
        [Tooltip("Time that you have to finish fishing this fish, otherwise it will run away /Range min and max")]
        [SerializeField] public FloatReference minStrikeTime = 1f;
        [Tooltip("Time that you have to finish fishing this fish, otherwise it will run away /Range min and max")]
        [SerializeField] public FloatReference maxStrikeTime = 4f;

        [Header("Nibbling Behavior")]
        [Tooltip("Min times that this fish will nibble before bite")]
        [SerializeField] public IntReference minNibbles = 2;
        [Tooltip("Max times that this fish can nibble before bite")]
        [SerializeField] public IntReference maxNibbles = 6;

        [SerializeField] public FloatReference minTimeBetweenNibbles = 1f;
        [SerializeField] public FloatReference maxTimeBetweenNibbles = 4f;


        [Header("Tug of War Behavior")]
        [Tooltip("Range of angles the fish can choose to try to flee from the player, 0 is left, 180 is right, 90 is straight forward, all in relation to player pos")]
        [MinMaxRange(0, 180)]
        [SerializeField] private RangedFloat fleeRangeAngle = new(40f, 140f);

        [Tooltip("Flee speed (units per second) the fish uses when escaping during the Struggle stage.")]
        [SerializeField] private FloatReference fleeSpeed = 2f;
  
        [Tooltip("Minimum duration (in seconds) of the Struggle stage before the fish may transition to Tired.")]
        [SerializeField] private FloatReference minStruggleStageDuration = 2f;
        [Tooltip("Maximum duration (in seconds) of the Struggle stage before the fish may transition to Tired.")]
        [SerializeField] private FloatReference maxStruggleStageDuration = 3f;

        [Tooltip("Minimum duration (in seconds) the fish remains in the Tired stage (waiting/resting) before possibly re-entering Struggle.")]
        [SerializeField] private FloatReference minTiredStageDuration = 2f;
        [Tooltip("Maximum duration (in seconds) the fish remains in the Tired stage (waiting/resting) before possibly re-entering Struggle.")]
        [SerializeField] private FloatReference maxTiredStageDuration = 3f;

        [Header("Debug")]
        [Tooltip("Show Debug Log")]
        [SerializeField] private bool debug = false;


        public override string ToString()
        {
            return $"{fishName} ({Weight} kg)";
        }
    }
}
