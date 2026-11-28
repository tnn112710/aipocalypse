using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    [Serializable]
    public sealed class SanitySettings
    {
        [Min(0f), Tooltip("Game hours after waking before the lack of sleep drains sanity")] public float AwakeGraceHours = 8f;
        [Min(0f), Tooltip("Hours awake at which the lack of sleep alone has emptied a full (base max) meter")] public float AwakeEmptyHours = 18f;
        [Min(0f), Tooltip("Darkness drain per game hour at FullDrainDistance and beyond, at night")] public float DarknessFullPerHour = 25f;
        [Min(1f), Tooltip("Metres outside the nearest light where the darkness drain reaches its full rate")] public float FullDrainDistance = 150f;
        [Tooltip("Darkness drain share (0..1) by distance outside the light (0..1 of FullDrainDistance)")]
        public AnimationCurve DarknessByDistance = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Min(0f), Tooltip("Recovery per game hour inside a light, day or night")] public float LightRecoveryPerHour = 3.3f;
        [Range(0f, 1f), Tooltip("Darkness drain share near a comfort source (Loki)")] public float ComfortDrainScale = 0.6f;
    }

    // Where the player is, as sanity sees it: how dark it is (0 day .. 1 night), how far outside the nearest light, whether
    // inside one, whether near a comfort source.
    public readonly struct SanityInput
    {
        public readonly float Darkness;
        public readonly float DistanceOutsideLight;
        public readonly bool InLight;
        public readonly bool NearComfort;

        public SanityInput(float darkness, float distanceOutsideLight, bool inLight, bool nearComfort)
        {
            Darkness = darkness;
            DistanceOutsideLight = distanceOutsideLight;
            InLight = inLight;
            NearComfort = nearComfort;
        }
    }

    // Sanity's rates per game hour: the lack of sleep (day and night, after the grace hours), darkness outside every light
    // (faster the farther, at night only, slower near comfort), and recovery inside a light.
    public static class SanityExposure
    {
        public static void Rates(in SanityInput input, float hoursAwake, SanitySettings settings, float baseMax, out float drain, out float recovery)
        {
            drain = 0f;
            recovery = 0f;
            if (settings == null)
                return;
            if (hoursAwake >= settings.AwakeGraceHours)
                drain += baseMax / Mathf.Max(0.01f, settings.AwakeEmptyHours - settings.AwakeGraceHours);
            if (input.InLight)
                recovery += settings.LightRecoveryPerHour;
            else if (input.Darkness > 0f)
            {
                float share = Mathf.Clamp01(settings.DarknessByDistance.Evaluate(Mathf.Clamp01(input.DistanceOutsideLight / settings.FullDrainDistance)));
                float darkness = settings.DarknessFullPerHour * share * Mathf.Clamp01(input.Darkness);
                drain += input.NearComfort ? darkness * settings.ComfortDrainScale : darkness;
            }
        }

        public static float RatePerHour(in SanityInput input, float hoursAwake, SanitySettings settings, float baseMax)
        {
            Rates(input, hoursAwake, settings, baseMax, out float drain, out float recovery);
            return recovery - drain;
        }
    }
}
