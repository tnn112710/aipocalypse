using DistantLands.Cozy;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // The only code that touches COZY. Shows the director's look hour, our day and moon in COZY's sky, clouds and sun / moon
    // lights (COZY's own clock stays paused), reads its sun and moon back, and can mute COZY fog for the other fog modes.
    [ExecuteAlways]
    public sealed class CozyBridge : MonoBehaviour
    {
        [SerializeField, Tooltip("The scene's COZY Weather")] CozyWeather _cozy;

        CozyTimeModule _time;
        CozyAtmosphereModule _atmosphere;
        int _shownDay = int.MinValue;
        // Ours: 0 new moon, 0.5 full.
        float _moonPhase = 0.5f;
        float _newMoonLight = 1f;
        bool _fog = true;

        public bool IsReady => Resolve();
        // Toward the sun / moon (COZY's own directions point the way the light travels).
        public Vector3 SunDirection => _atmosphere != null ? -_atmosphere.SunDirection : Vector3.up;
        public Vector3 MoonDirection => _atmosphere != null ? -_atmosphere.MoonDirection : Vector3.down;
        // COZY switches its sun light off when the look gives the sun no light; the moon lights the scene then.
        public bool MoonLights => _atmosphere != null && _atmosphere.SunLight != null && !_atmosphere.SunLight.enabled;

        void OnEnable() => Resolve();

        void OnDisable()
        {
            if (_atmosphere != null)
                _atmosphere.RunAfterBiomes -= AfterBiomes;
            _atmosphere = null;
            _time = null;
            _shownDay = int.MinValue;
        }

        bool Resolve()
        {
            if (_time != null && _atmosphere != null)
                return true;
            if (_cozy == null || !isActiveAndEnabled)
                return false;
            _time = _cozy.Time as CozyTimeModule;
            var atmosphere = _cozy.Atmosphere as CozyAtmosphereModule;
            if (atmosphere != null && atmosphere != _atmosphere)
            {
                _atmosphere = atmosphere;
                _atmosphere.RunAfterBiomes += AfterBiomes;
            }
            return _time != null && _atmosphere != null;
        }

        // COZY's sun rises at 06:00 and sets at 18:00. Our sunrise → sunset maps onto 06–18 and the night onto 18–30, so the
        // looks keep the sun height they were tuned at.
        public static float CozyHour(float hour, float sunrise, float sunset)
        {
            float dayLength = Mathf.Repeat(sunset - sunrise, 24f);
            if (dayLength <= 0f)
                return Mathf.Repeat(hour, 24f);
            float sinceSunrise = Mathf.Repeat(hour - sunrise, 24f);
            float cozy = sinceSunrise < dayLength
                ? 6f + sinceSunrise / dayLength * 12f
                : 18f + (sinceSunrise - dayLength) / (24f - dayLength) * 12f;
            return Mathf.Repeat(cozy, 24f);
        }

        // The inverse of CozyHour.
        public static float OurHour(float cozyHour, float sunrise, float sunset)
        {
            float dayLength = Mathf.Repeat(sunset - sunrise, 24f);
            if (dayLength <= 0f)
                return Mathf.Repeat(cozyHour, 24f);
            float sinceSix = Mathf.Repeat(cozyHour - 6f, 24f);
            float hour = sinceSix < 12f
                ? sunrise + sinceSix / 12f * dayLength
                : sunset + (sinceSix - 12f) / 12f * (24f - dayLength);
            return Mathf.Repeat(hour, 24f);
        }

        // hour 0–24 on COZY's clock (CozyHour), day as WorldClock counts it (from 1), moon phase 0 new … 0.5 full.
        // COZY applies it on its next update.
        public void Show(float hour, int day, float moonPhase, float newMoonLight, bool fog)
        {
            if (!Resolve())
                return;
            if (!_time.PauseTime)
                _time.PauseTime = true;
            _time.CurrentTime = Mathf.Repeat(hour, 24f) / 24f;
            // Every date write runs COZY's day-change events, so only on a new day. COZY's absolute day starts at 0.
            if (day != _shownDay)
            {
                _shownDay = day;
                _time.CurrentDate = Mathf.Max(0, day - 1);
            }
            _moonPhase = moonPhase;
            _newMoonLight = newMoonLight;
            _fog = fog;
        }

        // COZY points RenderSettings.sun at its sun light every frame, also at night while that light is off. URP then takes the
        // brightest other directional light (the enemy night highlight) as the main light and the moon casts no shadows.
        // Call after COZY's update (LateUpdate).
        public void PointSunAtLitLight()
        {
            if (!Resolve())
                return;
            Light lit = MoonLights && _atmosphere.MoonLight != null ? _atmosphere.MoonLight : _atmosphere.SunLight;
            if (lit != null && RenderSettings.sun != lit)
                RenderSettings.sun = lit;
        }

        // Where COZY puts the sun and moon at an hour (on COZY's clock) of the shown day (toward them), without waiting for its next update.
        public void Directions(float hour, out Vector3 sun, out Vector3 moon)
        {
            sun = Vector3.up;
            moon = Vector3.down;
            if (!Resolve())
                return;
            _atmosphere.GetCelestialDirections(Mathf.Max(0, _shownDay - 1), _time.GetModifiedTime(Mathf.Repeat(hour, 24f) / 24f),
                out Vector3 sunTravel, out Vector3 moonTravel);
            sun = -sunTravel;
            moon = -moonTravel;
        }

        // Runs inside COZY's update, after its profile is evaluated and before anything is written.
        void AfterBiomes(CozyAtmosphereModule atmosphere)
        {
            AtmosphereParameters parameters = atmosphere.Parameters;
            // COZY's phase is repeat((day + t + offset) / period), full at 0.5: offset it so it lands on ours.
            float period = Mathf.Max(0.01f, parameters.skyMoonDiskOrbitPeriod.value);
            float dayAndTime = Mathf.Max(0, _shownDay - 1) + (_time != null ? _time.ModifiedTime : 0f);
            parameters.skyMoonDiskOrbitOffset.value = _moonPhase * period - dayAndTime;
            float fullness = 1f - Mathf.Abs(_moonPhase - 0.5f) * 2f;
            parameters.lightingMoonLightIntensity.value *= Mathf.Lerp(_newMoonLight, 1f, fullness);
            if (!_fog)
                parameters.fogBaseDensityMultiplier.value = 0f;
        }
    }
}
