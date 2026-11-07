
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy.Blocks
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Blocks Profile", order = 362)]
    public class BlocksProfile : CozyProfile
    {
        [SerializeReference]
        [BlockOverridesList]
        public List<BlockOverride> overrides = new();

        public void ApplyToWeatherSphere(AtmosphereParameters parameters, float weight)
        {
            if (overrides == null || weight <= 0f)
                return;

            for (int i = 0; i < overrides.Count; i++)
            {
                BlockOverride blockOverride = overrides[i];
                if (blockOverride == null || !blockOverride.active || !blockOverride.FeatureEnabledGlobally)
                    continue;

                blockOverride.ApplyToWeatherSphere(parameters, weight);
            }

        }
    }
}