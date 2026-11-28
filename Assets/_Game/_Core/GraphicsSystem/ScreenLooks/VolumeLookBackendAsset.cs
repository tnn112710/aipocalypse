using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks
{
    [Serializable]
    public sealed class VolumeLook
    {
        [Tooltip("The look at full strength: any volume overrides (URP's, SC Screen Effects', another pack's)")] public VolumeProfile Profile;
        [Min(0f), Tooltip("Pulses per second (0 = steady)")] public float PulseRate;
        [Range(0f, 1f), Tooltip("How far a pulse dips the weight")] public float PulseDepth;
    }

    // A backend for any tool whose effects are volume overrides: one global volume per look, weighted by its strength. The
    // URP-only and SC Screen Effects looks are two of these assets with different profiles, so no code names a tool.
    // Assets: Data/Graphics/ScreenLooks/.
    [CreateAssetMenu(fileName = "VolumeLooks", menuName = "Aipocalypse/Graphics/Volume Look Backend")]
    public sealed class VolumeLookBackendAsset : ScreenLookBackendAsset
    {
        [SerializeField, Tooltip("A profile per look; a look without one draws nothing")]
        public Dictionary<ScreenLookId, VolumeLook> Looks = new();
        [Tooltip("Above the time-of-day grade volumes; each look adds its ScreenLookId number")] public float Priority = 200f;

        public override IScreenLookBackend Create(Transform parent) => new VolumeLookBackend(this, parent);
    }

    public sealed class VolumeLookBackend : IScreenLookBackend
    {
        // Below this weight the volume is off: a disabled volume costs nothing.
        const float Off = 0.001f;

        readonly Dictionary<ScreenLookId, (Volume Volume, VolumeLook Look)> _volumes = new();

        public VolumeLookBackend(VolumeLookBackendAsset asset, Transform parent)
        {
            foreach (KeyValuePair<ScreenLookId, VolumeLook> pair in asset.Looks)
            {
                if (pair.Value == null || pair.Value.Profile == null)
                    continue;
                var go = new GameObject($"Look_{pair.Key}");
                go.transform.SetParent(parent, false);
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                // Later looks on top: Critical's overrides win over Low's where both set a value.
                volume.priority = asset.Priority + (int)pair.Key;
                volume.sharedProfile = pair.Value.Profile;
                volume.weight = 0f;
                volume.enabled = false;
                _volumes[pair.Key] = (volume, pair.Value);
            }
        }

        public void Apply(ScreenLookId look, float strength)
        {
            if (!_volumes.TryGetValue(look, out (Volume Volume, VolumeLook Look) entry) || entry.Volume == null)
                return;
            float weight = Mathf.Clamp01(strength);
            if (weight > Off && entry.Look.PulseRate > 0f)
                weight *= 1f - entry.Look.PulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * entry.Look.PulseRate * Mathf.PI * 2f));
            bool on = weight > Off;
            if (entry.Volume.enabled != on)
                entry.Volume.enabled = on;
            entry.Volume.weight = weight;
        }

        public void Dispose()
        {
            foreach ((Volume Volume, VolumeLook Look) entry in _volumes.Values)
            {
                if (entry.Volume == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(entry.Volume.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(entry.Volume.gameObject);
            }
            _volumes.Clear();
        }
    }
}
