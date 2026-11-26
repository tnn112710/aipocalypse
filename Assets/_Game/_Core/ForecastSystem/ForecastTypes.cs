using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.ForecastSystem
{
    // The three parts of a night a fight can start in.
    public enum NightPeriod
    {
        Evening,
        Midnight,
        LateNight,
    }

    // What a period brings: nothing, a small or medium fight, or a boss.
    public enum FightSize
    {
        None,
        Small,
        Medium,
        Boss,
    }

    // One period of the forecast: when it runs (clock hours, end may pass midnight) and the fight it brings.
    [Serializable]
    public struct ForecastPeriod
    {
        [Range(0f, 24f)] public float StartHour;
        [Range(0f, 24f)] public float EndHour;
        public FightSize Fight;

        public ForecastPeriod(float startHour, float endHour, FightSize fight)
        {
            StartHour = startHour;
            EndHour = endHour;
            Fight = fight;
        }
    }
}
