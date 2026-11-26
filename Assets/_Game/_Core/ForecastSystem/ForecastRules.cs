using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // When a forecast is hidden or shown, as pure functions of the clock (ForecastTests).
    public static class ForecastRules
    {
        // The day whose forecast hangs now: a new one from NewForecastHour; before that hour it's still the day before's.
        public static int DayOf(WorldTime now, float newForecastHour) => now.Hour >= newForecastHour ? now.Day : now.Day - 1;

        // Shown when this day's forecast was revealed, or once its SelfRevealHour has passed.
        public static bool IsRevealed(WorldTime now, int revealedDay, ForecastSettings settings)
        {
            if (revealedDay == DayOf(now, settings.NewForecastHour))
                return true;
            float sinceNew = Mathf.Repeat(now.Hour - settings.NewForecastHour, 24f);
            float selfReveal = Mathf.Repeat(settings.SelfRevealHour - settings.NewForecastHour, 24f);
            return sinceNew >= selfReveal;
        }
    }
}
