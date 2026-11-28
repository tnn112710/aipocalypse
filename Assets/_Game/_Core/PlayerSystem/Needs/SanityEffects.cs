using System;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // How low sanity shows: each screen look fades in over a band of the sanity share (Uneasy from UneasyFrom down to LowFrom,
    // Low from LowFrom to CriticalFrom, Critical from CriticalFrom to 0), the sounds, and where the HUD starts to lie.
    [Serializable]
    public sealed class SanityEffectSettings
    {
        [Range(0f, 1f), Tooltip("Sanity share where the first effects start")] public float UneasyFrom = 0.6f;
        [Range(0f, 1f)] public float LowFrom = 0.3f;
        [Range(0f, 1f)] public float CriticalFrom = 0.1f;
        [Min(0f), Tooltip("Real seconds for a look to follow a change")] public float EaseTime = 1f;

        [Header("HUD")]
        [Range(0f, 1f), Tooltip("Below this the clock lies now and then and the bars and gauges flicker (values stay true)")] public float LiesFrom = 0.3f;
        [Range(0f, 1f), Tooltip("Below this the minimap shows fake enemy dots and the compass letters swap")] public float BlipsFrom = 0.1f;

        [Header("Sound (2D)")]
        [Tooltip("A low loop under the Low and Critical looks, at the Low look's strength")] public SoundID Drone;
        [Range(0f, 1f)] public float DroneVolume = 0.5f;
        [Tooltip("Played now and then below UneasyFrom, louder the lower sanity is")] public SoundID Whisper;
        [Range(0f, 1f)] public float WhisperVolume = 0.35f;
        [Min(0.5f), Tooltip("Seconds between whispers (random in this range)")] public float WhisperEveryMin = 6f;
        [Min(0.5f)] public float WhisperEveryMax = 14f;
        [Tooltip("Beats below HeartbeatFrom (the low-health heartbeat's entity)")] public SoundID Heartbeat;
        [Range(0f, 1f)] public float HeartbeatFrom = 0.3f;
        [Min(0.2f), Tooltip("Seconds between beats at HeartbeatFrom; faster towards empty")] public float HeartbeatEvery = 1.1f;
        [Range(0f, 1f)] public float HeartbeatVolume = 0.6f;

        [Header("Leaving the light")]
        [Tooltip("Played (2D) when the player steps out of every safe light in the dark: where safety ends (LightEdgeCue)")] public SoundID LeftLightSound;
        [Range(0f, 1f)] public float LeftLightVolume = 0.5f;
        [Range(0f, 1f), Tooltip("Darkness (0 day .. 1 night) above which stepping out is cued")] public float LeftLightDarkness = 0.5f;
        [Min(0.01f), Tooltip("Real seconds the LeftLight screen look takes to come in")] public float LeftLightRise = 0.4f;
        [Min(0f), Tooltip("Real seconds it then takes to fade: it marks the moment, it doesn't stay on")] public float LeftLightFade = 3f;
        [Min(0f), Tooltip("Real seconds before it can cue again (walking along the edge)")] public float LeftLightCooldown = 10f;

        // 0 above the look's band, 1 below it, linear inside.
        public float Strength(ScreenLookId look, float share) => look switch
        {
            ScreenLookId.SanityUneasy => Band(share, UneasyFrom, LowFrom),
            ScreenLookId.SanityLow => Band(share, LowFrom, CriticalFrom),
            ScreenLookId.SanityCritical => Band(share, CriticalFrom, 0f),
            _ => 0f,
        };

        static float Band(float share, float from, float to) => from <= to ? (share <= to ? 1f : 0f) : Mathf.Clamp01((from - share) / (from - to));
    }
}
