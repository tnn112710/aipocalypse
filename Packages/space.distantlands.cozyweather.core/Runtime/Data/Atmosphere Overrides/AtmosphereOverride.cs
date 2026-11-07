//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{

    [System.Serializable]
    public class AtmosphereOverride
    {
        public bool active = true;
        public virtual string Title => "Atmosphere Override";
        public virtual string Category => "";
        public virtual bool FeatureEnabledGlobally => true;
        public virtual void ToggleFeature() { }

        public virtual void ResetToDefaults()
        {

        }
        public virtual void ApplyToModel(AtmosphereModel model)
        {

        }

        public virtual void CopyModel(AtmosphereModel model)
        {

        }

        public virtual void ApplyToWeatherSphere(AtmosphereParameters parameters, float time)
        {

        }

        public virtual void ApplyAsBiome(AtmosphereParameters parameters, float time, float weight)
        {

        }
    }
}