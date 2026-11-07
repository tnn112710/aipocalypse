//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public static class AtmosphereFactory
    {
        private static AtmosphereProperty<T> CreateInterpolated<T>(params (T value, float time)[] keys)
            where T : IInterpolatedValue<T>
        {
            var gradientKeys = new AtmosphereProperty<T>.AtmosphereGradientKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                gradientKeys[i] = new AtmosphereProperty<T>.AtmosphereGradientKey(keys[i].value, keys[i].time);
            }

            return new AtmosphereProperty<T>
            {
                overrideValue = true,
                interpolateValue = true,
                interpolatedValue = gradientKeys
            };
        }

        public static AtmosphereProperty<T> Constant<T>(T value) where T : IInterpolatedValue<T>
        {
            return new AtmosphereProperty<T>
            {
                overrideValue = true,
                interpolateValue = false,
                constantValue = value,
                interpolatedValue = new[] { new AtmosphereProperty<T>.AtmosphereGradientKey(value, 0.5f) }
            };
        }

        public static AtmosphereProperty<T> Disabled<T>() where T : IInterpolatedValue<T>
        {
            return new AtmosphereProperty<T>
            {
                overrideValue = false
            };
        }
        public static AtmosphereProperty<T> Disabled<T>(T value) where T : IInterpolatedValue<T>
        {
            return new AtmosphereProperty<T>
            {
                overrideValue = false,
                interpolateValue = false,
                constantValue = value,
            };
        }

        private const float MORNING_LENGTH = 0.07f;
        private const float MORNING_TIME = 0.25f;
        private const float EVENING_LENGTH = 0.07f;
        private const float EVENING_TIME = 0.75f;

        public static AtmosphereProperty<T> FourStateInterpolated<T>(T day, T night, T morning, T evening) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, MORNING_TIME - MORNING_LENGTH), (morning, MORNING_TIME), (day, MORNING_TIME + MORNING_LENGTH), (day, EVENING_TIME - EVENING_LENGTH), (evening, EVENING_TIME), (night, EVENING_TIME + EVENING_LENGTH));

        public static AtmosphereProperty<T> ThreeStateInterpolated<T>(T day, T night, T morning) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, MORNING_TIME - MORNING_LENGTH), (morning, MORNING_TIME), (day, MORNING_TIME + MORNING_LENGTH), (day, EVENING_TIME - EVENING_LENGTH), (morning, EVENING_TIME), (night, EVENING_TIME + EVENING_LENGTH));

        public static AtmosphereProperty<T> FourStateInterpolatedSmooth<T>(T day, T night, T morning, T evening) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, 0.0f), (morning, MORNING_TIME), (day, 0.5f), (evening, EVENING_TIME));

        public static AtmosphereProperty<T> TwoStateInterpolatedSmooth<T>(T day, T night) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, 0.0f), (day, 0.5f));

        public static AtmosphereProperty<T> TwoStateInterpolated<T>(T day, T night) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, MORNING_TIME - MORNING_LENGTH), (day, MORNING_TIME + MORNING_LENGTH), (day, EVENING_TIME - EVENING_LENGTH), (night, EVENING_TIME + EVENING_LENGTH));

        public static AtmosphereProperty<T> LateNight<T>(T day, T night) where T : IInterpolatedValue<T>
            => CreateInterpolated((night, 0.10f), (day, 0.15f), (day, 0.92f), (night, 0.97f));

        public static AtmosphereProperty<T> DotAccent<T>(T dayNight, T morningEvening) where T : IInterpolatedValue<T>
            => CreateInterpolated((dayNight, 0.15f), (morningEvening, 0.2f), (dayNight, 0.25f), (dayNight, 0.75f), (morningEvening, 0.8f), (dayNight, 0.85f));

    }
}