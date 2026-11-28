using System.Collections.Generic;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Everything a NeedRing looks and sounds like: icon, ring sprites, a colour per tier, dimming and pulses. Sizes stay in the HUD.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Need Ring Style", fileName = "NeedRingStyle")]
    public sealed class NeedRingStyle : ScriptableObject
    {
        [BoxGroup("Look")] public Sprite Icon;
        [BoxGroup("Look")] public Color IconColor = Color.white;
        [BoxGroup("Look"), Tooltip("The ring that empties clockwise (Image Filled, Radial 360)")] public Sprite RingSprite;
        [BoxGroup("Look"), Tooltip("Drawn under the ring: the empty part")] public Sprite TrackSprite;
        [BoxGroup("Look")] public Color TrackColor = new(0f, 0f, 0f, 0.45f);
        [BoxGroup("Look"), SerializeField, Tooltip("Ring colour per tier; every NeedSeverity needs an entry")]
        public Dictionary<NeedSeverity, Color> Colors = new()
        {
            { NeedSeverity.Good, new Color(0.55f, 0.9f, 0.45f) },
            { NeedSeverity.Normal, Color.white },
            { NeedSeverity.Low, new Color(1f, 0.62f, 0.18f) },
            { NeedSeverity.VeryLow, new Color(0.95f, 0.25f, 0.2f) },
            { NeedSeverity.Empty, new Color(0.95f, 0.25f, 0.2f) },
        };

        [BoxGroup("Dimming"), Range(0f, 1f), Tooltip("Alpha while the need is fine (Good or Normal) and nothing just changed")]
        public float DimAlpha = 0.45f;
        [BoxGroup("Dimming"), Min(0f), Tooltip("Real seconds a ring stays bright after a tier change or a refill")] public float BrightTime = 2.5f;
        [BoxGroup("Dimming"), Min(0.01f), Tooltip("Alpha change per real second")] public float FadeSpeed = 2f;

        [BoxGroup("Pulse"), Min(1f), Tooltip("Scale of the one punch on entering a tier")] public float PulseScale = 1.18f;
        [BoxGroup("Pulse"), Min(0.01f)] public float PulseTime = 0.35f;
        [BoxGroup("Pulse"), Min(0f), Tooltip("Pulses per second while empty (0 = none)")] public float EmptyPulseRate = 1.2f;
        [BoxGroup("Pulse"), Range(0f, 1f), Tooltip("Alpha the empty pulse dips to")] public float EmptyPulseAlpha = 0.45f;

        [BoxGroup("Sound"), Tooltip("Played on entering Low or worse (2D); empty = silent")] public SoundID TierSound;
        [BoxGroup("Sound"), Min(0f), Tooltip("Real seconds between repeats while VeryLow or Empty (0 = never)")] public float RepeatSoundEvery = 40f;

        public Color ColorFor(NeedSeverity severity) => Colors != null && Colors.TryGetValue(severity, out Color color) ? color : Color.white;

#if UNITY_EDITOR
        // Edit-time preview only: rings re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<NeedRing>(this, ring => ring.Style, ring => ring.ApplyStyle());
#endif
    }
}
