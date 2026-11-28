using System;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    public enum LoadState
    {
        Light,
        // From NearShare of Comfortable up to it: still full speed.
        NearComfortable,
        // Between Comfortable and Max: slower, dearer stamina.
        Heavy,
        // At Max or above: a crawl, no sprint, no jump.
        Overloaded,
    }

    [Serializable]
    public sealed class CarryLoadSettings
    {
        [Tooltip("Off: weight never slows the player")] public bool Enabled = true;
        [Min(0f), Tooltip("Kg between Comfortable and Max (StatId.WeightMargin)")] public float BaseMargin = 10f;
        [Range(0f, 1f), Tooltip("Share of Comfortable from which the gauge warns (yellow)")] public float NearShare = 0.8f;
        [Range(0f, 1f), Tooltip("Speed at Max, reached linearly from Comfortable")] public float HeavySpeed = 0.6f;
        [Min(1f), Tooltip("Stamina cost scale at Max, reached linearly from Comfortable")] public float HeavyCost = 1.5f;
        [Range(0f, 1f), Tooltip("Speed at Max and above: a crawl, never a stop")] public float OverloadedSpeed = 0.15f;
    }

    // What the carried weight does: speed and stamina cost scales between the Comfortable and Max limits.
    public readonly struct CarryLoad
    {
        public readonly float Weight;
        public readonly float Comfortable;
        public readonly float Max;
        public readonly float SpeedMultiplier;
        public readonly float CostMultiplier;
        public readonly LoadState State;

        CarryLoad(float weight, float comfortable, float max, float speed, float cost, LoadState state)
        {
            Weight = weight;
            Comfortable = comfortable;
            Max = max;
            SpeedMultiplier = speed;
            CostMultiplier = cost;
            State = state;
        }

        public static CarryLoad Evaluate(float weight, float comfortable, float max, CarryLoadSettings settings)
        {
            max = Mathf.Max(max, comfortable);
            if (settings == null || !settings.Enabled || weight < comfortable * settings.NearShare)
                return new CarryLoad(weight, comfortable, max, 1f, 1f, LoadState.Light);
            if (weight <= comfortable)
                return new CarryLoad(weight, comfortable, max, 1f, 1f, LoadState.NearComfortable);
            if (weight >= max)
                return new CarryLoad(weight, comfortable, max, settings.OverloadedSpeed, settings.HeavyCost, LoadState.Overloaded);
            float t = (weight - comfortable) / (max - comfortable);
            return new CarryLoad(weight, comfortable, max, Mathf.Lerp(1f, settings.HeavySpeed, t), Mathf.Lerp(1f, settings.HeavyCost, t),
                LoadState.Heavy);
        }

        // Comfortable = StatId.ComfortableWeight over the inventory's limit (bag included); Max = it + StatId.WeightMargin.
        public static void Limits(IStatProvider stats, float inventoryMax, CarryLoadSettings settings, out float comfortable, out float max)
        {
            comfortable = Mathf.Max(0f, stats.Get(StatId.ComfortableWeight, inventoryMax));
            max = comfortable + Mathf.Max(0f, stats.Get(StatId.WeightMargin, settings != null ? settings.BaseMargin : 0f));
        }
    }
}
