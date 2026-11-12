
using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Resound Track", order = 361)]
    public class ResoundTrack : CozyProfile, IStateMachineProfile
    {
        [Tooltip("Specifies the minimum length for this track in real-world time.")]
        [SerializeField]
        private MeridiemTimeDelta minTime = new MeridiemTimeDelta(0, 30);
        public MeridiemTimeDelta MinTime => LoopMode == LoopMethod.PlayOnce ? clip.length / 86400f : minTime;
        
        [Tooltip("Specifies the maximum length for this track in real-world time.")]
        [SerializeField]
        private MeridiemTimeDelta maxTime = new MeridiemTimeDelta(2, 30);


        public MeridiemTimeDelta MaxTime => LoopMode == LoopMethod.PlayOnce ? clip.length / 86400f : maxTime;


        [SerializeField]
        private AudioClip clip;
        public AudioClip Clip => clip;

        [SerializeField]
        [Range(0, 1)]
        private float volume = 1;
        public float Volume => volume;

        public enum LoopMethod
        {
            PlayOnce,
            Loop
        }

        [SerializeField]
        private LoopMethod loopMode;
        public LoopMethod LoopMode => loopMode;


        [SerializeField]
        private WeightedRandomChance chance = new WeightedRandomChance();

        /// <summary>
        /// Gets the weight evaluated against the current COZY conditions, or the base weight
        /// if no weather system is present. Setting it changes the base weight.
        /// </summary>
        public float Chance
        {
            get
            {
                CozyWeather weather = CozyWeather.Instance;
                return chance.GetChance(weather != null ? ConditionSnapshot.GetConditions(weather) : default);
            }
            set => chance.baseChance = Mathf.Clamp01(value);
        }
        public float GetChance(ConditionSnapshot conditions) => chance.GetChance(conditions);
    }
}