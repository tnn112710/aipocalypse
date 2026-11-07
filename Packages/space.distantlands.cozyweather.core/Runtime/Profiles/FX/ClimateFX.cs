//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using System.Collections;


namespace DistantLands.Cozy
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/FX/Climate FX", order = 361)]
    public class ClimateFX : CozyFXProfile
    {

        [SerializeField]
        private TransitionTimeModifier transitionTimeModifier = TransitionTimeModifier.Linear;

        public Overridable<TemperatureDelta> temperature;
        public Overridable<HumidityDelta> humidity;
        public Overridable<PressureDelta> pressure;
        public Overridable<UVIndexDelta> uvIndex;
        public Overridable<AirQualityDelta> airQuality;
        IClimateModule climate;

        public override bool IsValid => !(climate == null && InitializeEffect() == false);
        public override TransitionTimeModifier TransitionTimeModifier => transitionTimeModifier;

        public override void PlayEffect(float weight)
        {
            if (!IsValid)
                return;

            if (temperature.HasOverride) climate.Temperature.Offset += temperature.Value * Easing.Evaluate(weight, TransitionTimeModifier);
            if (humidity.HasOverride) climate.Humidity.Offset += humidity.Value * Easing.Evaluate(weight, TransitionTimeModifier);
            if (pressure.HasOverride) climate.Pressure.Offset += pressure.Value * Easing.Evaluate(weight, TransitionTimeModifier);
            if (uvIndex.HasOverride) climate.UVIndex.Offset += uvIndex.Value * Easing.Evaluate(weight, TransitionTimeModifier);
            if (airQuality.HasOverride) climate.AirQuality.Offset += airQuality.Value * Easing.Evaluate(weight, TransitionTimeModifier);

        }

        public override bool InitializeEffect()
        {
            CozyWeather weatherManager = CozyWeather.Instance;
            if (weatherManager == null) return false;

            climate = weatherManager.Climate;
            if (climate == null) return false;

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
