//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

//  Documentation provided here: https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/utilities/chance

using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    [System.Serializable]
    public class WeightedRandomChance
    {

        [Range(0, 1)]
        public float baseChance = 1;
        [Tooltip("Animation curves that increase or decrease chance based on time, temperature, etc.")]
        public List<WeightedChanceCurve> chanceEffectors = new();

        /// <summary>
        /// Evaluates the current chance now.
        /// </summary>
        /// <returns></returns>
        public float GetChance() => 0;

        /// <summary>
        /// Evaluates the current chance now.
        /// </summary>
        /// <param name="conditions">Holds a reference to the COZY Weather system</param>
        public float GetChance(ConditionSnapshot conditions)
        {

            float i = baseChance;

            foreach (WeightedChanceCurve j in chanceEffectors)
                if (j != null)
                {
                    i *= j.EvaluateBasedOnConditions(conditions);
                }

            return Mathf.Max(i, 0);
        }

        public bool HasLimit(ChanceConditionType limit)
        {
            foreach (WeightedChanceCurve effector in chanceEffectors)
            {
                if (effector.curveType == limit)
                    return true;
            }

            return false;
        }

        public static implicit operator float(WeightedRandomChance chance)
        {
            return chance.GetChance();
        }

    }
}