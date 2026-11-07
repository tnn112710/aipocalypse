//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

namespace DistantLands.Cozy
{
    public class WeatherParameters : IBlendableParameters<WeatherParameters>
    {
        public Dictionary<WeatherProfile, float> weatherWeights;

        public void Clear()
        {
            weatherWeights ??= new Dictionary<WeatherProfile, float>();
            weatherWeights.Clear();
        }

        public void Blend(WeatherParameters parameters, float weight)
        {
            if (weight == 0 || parameters?.weatherWeights == null || parameters.weatherWeights.Count == 0) return;

            weatherWeights ??= new Dictionary<WeatherProfile, float>();

            foreach (var kvp in parameters.weatherWeights)
            {
                if (!weatherWeights.TryGetValue(kvp.Key, out float currentWeight))
                    currentWeight = 0f;

                weatherWeights[kvp.Key] = currentWeight + kvp.Value * weight;
            }
        }
    }
}
