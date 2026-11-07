//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

namespace DistantLands.Cozy
{
    public class AmbienceParameters : IBlendableParameters<AmbienceParameters>
    {
        public Dictionary<AmbienceProfile, float> ambienceWeights;

        public void Clear()
        {
            ambienceWeights ??= new Dictionary<AmbienceProfile, float>();
            ambienceWeights.Clear();
        }

        public void Blend(AmbienceParameters parameters, float weight)
        {
            if (weight == 0 || parameters?.ambienceWeights == null || parameters.ambienceWeights.Count == 0) return;

            ambienceWeights ??= new Dictionary<AmbienceProfile, float>();

            foreach (var kvp in parameters.ambienceWeights)
            {
                if (!ambienceWeights.TryGetValue(kvp.Key, out float currentWeight))
                    currentWeight = 0f;

                ambienceWeights[kvp.Key] = currentWeight + kvp.Value * weight;
            }
        }
    }
}
