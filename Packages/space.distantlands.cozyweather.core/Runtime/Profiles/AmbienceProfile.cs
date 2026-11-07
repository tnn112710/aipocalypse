//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{

    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Ambience Profile", order = 361)]
    public class AmbienceProfile : CozyProfile, IStateMachineProfile
    {

        [Tooltip("Specifies the minimum length for this ambience profile.")]
        [SerializeField]
        private MeridiemTimeDelta minTime = new MeridiemTimeDelta(0, 30);
        [Tooltip("Specifies the maximum length for this ambience profile.")]
        [SerializeField]
        private MeridiemTimeDelta maxTime = new MeridiemTimeDelta(2, 30);

        public MeridiemTimeDelta MinTime => minTime;

        public MeridiemTimeDelta MaxTime => maxTime;

        public WeatherProfile[] dontPlayDuring;

        public string description;
        
        [SerializeField]
        private WeightedRandomChance chance;
        public WeightedRandomChance Chance => chance;

        [FX]
        [SerializeReference]
        private CozyFXProfile[] FX;

        public float GetChance(CozyWeather weather)
        {
            return chance.GetChance(ConditionSnapshot.GetConditions(weather));
        }
        public float GetChance(ConditionSnapshot condition)
        {
            return chance.GetChance(condition);
        }
        public void Play(float weightVal)
        {
            foreach (CozyFXProfile fx in FX)
                if (fx) fx.PlayEffect(weightVal);
        }
    }

}