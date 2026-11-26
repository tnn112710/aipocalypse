using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The night forecast the board shows. The same every day for now; rolled per day and tied to the raids later.
    // Asset: Data/Forecast/ForecastSettings.
    [CreateAssetMenu(fileName = "ForecastSettings", menuName = "Aipocalypse/Forecast/Forecast Settings")]
    public sealed class ForecastSettings : ScriptableObject
    {
        [Tooltip("Off: the board shows no forecast")] public bool Enabled = true;

        [BoxGroup("Periods"), SerializeField, Tooltip("Every NightPeriod needs an entry: its clock hours and the fight it brings")]
        public Dictionary<NightPeriod, ForecastPeriod> Periods = new()
        {
            { NightPeriod.Evening, new ForecastPeriod(20f, 22f, FightSize.Small) },
            { NightPeriod.Midnight, new ForecastPeriod(22f, 0f, FightSize.Small) },
            { NightPeriod.LateNight, new ForecastPeriod(0f, 2f, FightSize.Small) },
        };

        [BoxGroup("Reveal"), Tooltip("On: each day's forecast hangs as '?' until revealed (the rope, or by itself at SelfRevealHour). " +
            "Off: the plaques always show it")]
        public bool HideUntilRevealed = true;
        [BoxGroup("Reveal"), Range(0f, 24f), Tooltip("A new day's forecast hangs as '?' from this hour")]
        public float NewForecastHour = 6f;
        [BoxGroup("Reveal"), Range(0f, 24f), Tooltip("A forecast nobody revealed reveals itself at this hour, before the night")]
        public float SelfRevealHour = 19f;
    }
}
