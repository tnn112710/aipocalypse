//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using System.Collections.Generic;

namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Multi FX", order = 361)]
    public class MultiFX : CozyFXProfile
    {
        private CozyWeather weatherManager;

        [System.Serializable]
        public class MultiFXType
        {
            public CozyFXProfile FX;
            public WeightedChanceCurve intensityCurve;
        }

        public List<MultiFXType> multiFX;

        public override bool IsValid
        {
            get
            {
                foreach (var item in multiFX)
                {
                    if (!item.FX.IsValid) return false;
                }

                return true;
            }
        }

        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                if (InitializeEffect() == false)
                    return;

            ConditionSnapshot snapshot = ConditionSnapshot.GetConditions(weatherManager);

            foreach (MultiFXType i in multiFX)
            {
                i.FX.PlayEffect(i.intensityCurve.EvaluateBasedOnConditions(snapshot) * weight);
            }
        }

        public override bool InitializeEffect()
        {
            weatherManager = CozyWeather.Instance;
            if (weatherManager == null) return false;

            return true;
        }

        public override void StopEffect()
        {

        }

        public override void DeinitializeEffect()
        {

        }
    }
}
