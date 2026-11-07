using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy.Eclipse
{
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Eclipse Profile", order = 363)]
    public class EclipseProfile : CozyProfile
    {
        [SerializeReference]
        [EclipseOverridesList]
        public List<EclipseOverride> overrides = new();

        public void ApplyToAtmosphere(AtmosphereParameters parameters, float weight)
        {
            if (overrides == null || weight <= 0f)
                return;

            for (int i = 0; i < overrides.Count; i++)
            {
                EclipseOverride eclipseOverride = overrides[i];
                if (eclipseOverride == null || !eclipseOverride.active || !eclipseOverride.FeatureEnabledGlobally)
                    continue;

                eclipseOverride.ApplyToAtmosphere(parameters, weight);
            }
        }
    }
}
