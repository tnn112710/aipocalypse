using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    public enum KeyBlend
    {
        Smooth,
        Linear,
        Hold,
    }

    [Serializable]
    public struct TimeOfDayKey
    {
        public string Name;
        [Range(0f, 24f)] public float Hour;
        public LightingPreset Look;
        [Tooltip("Post-processing at this hour. Every grade in a profile must override the same settings")]
        public VolumeProfile Grade;
        [Tooltip("How this key blends into the next one")] public KeyBlend Blend;
    }

    // Where the sun and moon are at each hour. Angles: bearing from north (+Z) toward east (+X); elevation above the horizon.
    [Serializable]
    public sealed class SunPath
    {
        [Range(0f, 24f)] public float Sunrise = 6f;
        [Range(0f, 24f)] public float Sunset = 19f;
        [Range(1f, 90f)] public float NoonElevation = 50f;
        [Range(0f, 360f), Tooltip("Bearing the sun rises at")] public float SunriseBearing = 70f;
        [Range(0f, 360f), Tooltip("Degrees of bearing the sun travels from sunrise to sunset")] public float DayArc = 220f;

        [Range(1f, 90f)] public float MoonElevation = 45f;
        [Range(0f, 360f), Tooltip("Bearing the moon rises at (at sunset)")] public float MoonriseBearing = 100f;
        [Range(0f, 360f)] public float NightArc = 160f;

        [Range(0f, 30f), Tooltip("The light never goes lower than this, so terrain is never lit from below; the sky disc still sets")]
        public float MinLightElevation = 8f;
        [Range(0.5f, 20f), Tooltip("Degrees above the horizon over which sunlight and moonlight fade in")]
        public float HorizonFade = 12f;
        [Range(1f, 90f), Tooltip("Degrees the sun sinks below the horizon at midnight (and the moon at noon)")]
        public float NightDepth = 30f;
        [Range(0f, 1f), Tooltip("Moonlight at new moon, as a share of the look's Moon Intensity (full moon)")]
        public float NewMoonLight = 1f / 3f;

        [Range(-20f, 0f), Tooltip("Sun elevation where Daylight starts rising from 0 (lamps, windows and other reactors use it)")]
        public float DaylightStartElevation = -6f;
        [Range(0f, 30f), Tooltip("Sun elevation where Daylight reaches 1")]
        public float DaylightFullElevation = 10f;

        public float DayLength => Sunset > Sunrise ? Sunset - Sunrise : Sunset + 24f - Sunrise;

        // 0–1 through the day (sunrise → sunset), or through the night when isDay is false.
        public float Progress(float hour, out bool isDay)
        {
            float intoDay = hour - Sunrise;
            if (intoDay < 0f)
                intoDay += 24f;
            float day = DayLength;
            isDay = intoDay < day;
            return isDay ? intoDay / day : (intoDay - day) / (24f - day);
        }

        // Direction toward the sun; below the horizon at night.
        public Vector3 Sun(float hour, out float elevation)
        {
            float p = Progress(hour, out bool isDay);
            float arc = Mathf.Sin(p * Mathf.PI);
            elevation = isDay ? arc * NoonElevation : -arc * NightDepth;
            float bearing = isDay ? SunriseBearing + p * DayArc : SunriseBearing + DayArc + p * (360f - DayArc);
            return Direction(bearing, elevation);
        }

        public Vector3 Moon(float hour, out float elevation)
        {
            float p = Progress(hour, out bool isDay);
            float arc = Mathf.Sin(p * Mathf.PI);
            elevation = isDay ? -arc * NightDepth : arc * MoonElevation;
            float bearing = isDay ? MoonriseBearing + NightArc + p * (360f - NightArc) : MoonriseBearing + p * NightArc;
            return Direction(bearing, elevation);
        }

        public static Vector3 Direction(float bearing, float elevation)
        {
            float b = bearing * Mathf.Deg2Rad;
            float e = elevation * Mathf.Deg2Rad;
            float c = Mathf.Cos(e);
            return new Vector3(Mathf.Sin(b) * c, Mathf.Sin(e), Mathf.Cos(b) * c);
        }
    }

    // The look of a whole day: keyframes at hours (each a LightingPreset + a post-processing grade) and the sun path.
    // Add, move or retune hours here; gameplay timing lives in ClockSettings and never changes with the look.
    [CreateAssetMenu(menuName = "Aipocalypse/Environment/Time Of Day Profile", fileName = "TimeOfDayProfile")]
    public sealed class TimeOfDayProfile : ScriptableObject
    {
        [TableList(ShowIndexLabels = false), InfoBox("$GradeWarning", InfoMessageType.Warning, nameof(HasGradeWarning))]
        public List<TimeOfDayKey> Keys = new();

        [BoxGroup("Sun and moon"), HideLabel] public SunPath Path = new();

        [BoxGroup("Phase looks"), SerializeField, Tooltip("Transition By Phases: the hour (0–24) whose look, sun and moon each phase shows. " +
            "Pick an hour with the sun or moon up, or the phase gets no direct light")]
        public Dictionary<DayPhase, float> PhaseLooks = new()
        {
            { DayPhase.Dawn, 7f },
            { DayPhase.Morning, 9f },
            { DayPhase.Afternoon, 14f },
            { DayPhase.Dusk, 17.5f },
            { DayPhase.Evening, 20.5f },
            { DayPhase.Midnight, 0f },
        };

        // A missing phase shows midnight.
        public float PhaseLookHour(DayPhase phase) => PhaseLooks.TryGetValue(phase, out float hour) ? Mathf.Clamp(hour, 0f, 24f) : 0f;

        public readonly struct Segment
        {
            public readonly int From;
            public readonly int To;
            public readonly float T;

            public Segment(int from, int to, float t)
            {
                From = from;
                To = to;
                T = t;
            }
        }

        // The blended look at an hour, and which two keys it came from. False when the profile has no usable key.
        public bool Evaluate(float hour, out AtmosphereState state, out Segment segment)
        {
            int count = Keys.Count;
            if (count == 0 || Keys[0].Look == null)
            {
                state = default;
                segment = default;
                return false;
            }

            int to = 0;
            while (to < count && Keys[to].Hour <= hour)
                to++;
            int from = to - 1;
            if (to == count)
                to = 0;
            if (from < 0)
                from = count - 1;

            TimeOfDayKey a = Keys[from];
            TimeOfDayKey b = Keys[to];
            float span = b.Hour - a.Hour;
            if (span <= 0f)
                span += 24f;
            float into = hour - a.Hour;
            if (into < 0f)
                into += 24f;
            float t = Ease(a.Blend, count > 1 ? Mathf.Clamp01(into / span) : 0f);

            // A key without a Look borrows its neighbour's; with neither, the first key's (checked above).
            LightingPreset lookA = a.Look != null ? a.Look : b.Look != null ? b.Look : Keys[0].Look;
            LightingPreset lookB = b.Look != null ? b.Look : lookA;
            state = AtmosphereState.Lerp(lookA.ToState(), lookB.ToState(), t);
            segment = new Segment(from, to, t);
            return true;
        }

        static float Ease(KeyBlend blend, float t) => blend switch
        {
            KeyBlend.Linear => t,
            KeyBlend.Hold => 0f,
            _ => Mathf.SmoothStep(0f, 1f, t),
        };

        [Button("Sort keys by hour"), PropertyOrder(-1)]
        public void SortKeys() => Keys.Sort((x, y) => x.Hour.CompareTo(y.Hour));

        // Only when out of order, so a row doesn't jump on every unrelated edit.
        void OnValidate()
        {
            EnvironmentDirector.MarkLooksEdited();
            for (int i = 1; i < Keys.Count; i++)
            {
                if (Keys[i].Hour < Keys[i - 1].Hour)
                {
                    SortKeys();
                    return;
                }
            }
        }

        bool HasGradeWarning => !string.IsNullOrEmpty(GradeWarning);

        // Grades blend pairwise; one that overrides a setting its neighbour doesn't makes that setting jump at the key.
        string GradeWarning
        {
            get
            {
                string reference = null;
                string referenceName = null;
                foreach (TimeOfDayKey key in Keys)
                {
                    if (key.Grade == null)
                        continue;
                    string signature = Signature(key.Grade);
                    if (reference == null)
                    {
                        reference = signature;
                        referenceName = key.Grade.name;
                    }
                    else if (signature != reference)
                        return $"Grade '{key.Grade.name}' overrides different settings than '{referenceName}': they will pop instead of blending.";
                }
                return null;
            }
        }

        static string Signature(VolumeProfile profile)
        {
            var parts = new List<string>();
            foreach (VolumeComponent component in profile.components)
            {
                if (!component.active)
                    continue;
                var text = new System.Text.StringBuilder(component.GetType().Name).Append(':');
                for (int i = 0; i < component.parameters.Count; i++)
                    if (component.parameters[i].overrideState)
                        text.Append(i).Append(',');
                parts.Add(text.ToString());
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
        }
    }
}
