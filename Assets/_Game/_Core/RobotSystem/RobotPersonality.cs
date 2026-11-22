using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // One robot's own share of the settings' temper, rolled once, so two robots never move or react in step.
    public readonly struct RobotPersonality
    {
        public readonly float StrollChance;
        public readonly float WaveChance;
        public readonly float FlourishChance;
        // Scales how long it idles between moves.
        public readonly float Patience;

        RobotPersonality(float stroll, float wave, float flourish, float patience)
        {
            StrollChance = stroll;
            WaveChance = wave;
            FlourishChance = flourish;
            Patience = patience;
        }

        // Each value is the temper's × (1 ± spread), picked by its roll (0..1).
        public static RobotPersonality Roll(in RobotSettings.Temper temper, float spread, float stroll, float wave, float flourish, float patience) =>
            new(Chance(temper.StrollChance, spread, stroll), Chance(temper.WaveChance, spread, wave), Chance(temper.FlourishChance, spread, flourish),
                Mathf.Max(0.1f, Scale(temper.Patience, spread, patience)));

        static float Chance(float value, float spread, float roll) => Mathf.Clamp01(Scale(value, spread, roll));
        static float Scale(float value, float spread, float roll) => value * (1f + spread * (2f * roll - 1f));
    }
}
