using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // How the screen warns that the active player is badly hurt: a pulsing vignette (LowHealthHud, its texture made in code)
    // with a heartbeat on every pulse.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Low Health Style", fileName = "LowHealthStyle")]
    public sealed class LowHealthStyle : ScriptableObject
    {
        [Tooltip("Off: no vignette and no heartbeat")] public bool Enabled = true;
        [BoxGroup("When"), Range(0f, 1f), Tooltip("Health share under which the warning starts (faint), growing to full at Full At")]
        public float Below = 0.35f;
        [BoxGroup("When"), Range(0f, 1f), Tooltip("Health share at which it is at full strength")] public float FullAt = 0.1f;

        [BoxGroup("Look")] public Color Color = new(0.55f, 0.02f, 0.02f, 1f);
        [BoxGroup("Look"), Range(0f, 1f), Tooltip("Vignette alpha at full strength, at the top of a pulse")] public float MaxAlpha = 0.75f;
        [BoxGroup("Look"), Range(0f, 1f), Tooltip("Share of the alpha left at the bottom of a pulse")] public float PulseFloor = 0.55f;
        [BoxGroup("Look"), Range(0f, 1f), Tooltip("Share of the half-diagonal left clear in the middle of the screen")]
        public float ClearRadius = 0.38f;
        [BoxGroup("Look"), Min(0.1f), Tooltip("Pulses per second just under the threshold")] public float PulseRate = 1.1f;
        [BoxGroup("Look"), Min(0.1f), Tooltip("Pulses per second at full strength")] public float PulseRateFull = 1.9f;
        [BoxGroup("Look"), Min(0.01f), Tooltip("Seconds the strength takes to follow the health (real time)")] public float Ease = 0.4f;

        [BoxGroup("Sound"), Tooltip("One beat, played at the top of every pulse")] public SoundID Heartbeat;
        [BoxGroup("Sound"), Range(0f, 1f), Tooltip("Beat volume just under the threshold; 1 at full strength")]
        public float MinVolume = 0.35f;

        [BoxGroup("When"), Range(0f, 1f), Tooltip("Strength just under the threshold (1 = full)")] public float StartStrength = 0.2f;

        // 0 at the threshold and above; StartStrength just under it, 1 at FullAt and below.
        public float Strength(float healthShare)
        {
            if (!Enabled || healthShare >= Below)
                return 0f;
            return FullAt >= Below ? 1f : Mathf.Lerp(StartStrength, 1f, Mathf.InverseLerp(Below, FullAt, healthShare));
        }
    }
}
