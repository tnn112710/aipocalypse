using System;
using Ami.BroAudio;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // Everything a NeedRing looks and sounds like: icon, ring sprites, colours by how full it is, dimming and pulses. Sizes stay in the HUD.
    [CreateAssetMenu(menuName = "Aipocalypse/UI/Need Ring Style", fileName = "NeedRingStyle")]
    public sealed class NeedRingStyle : ScriptableObject
    {
        [BoxGroup("Look")] public Sprite Icon;
        [BoxGroup("Look")] public Color IconColor = Color.white;
        [BoxGroup("Look"), Tooltip("The ring that empties clockwise (Image Filled, Radial 360)")] public Sprite RingSprite;
        [BoxGroup("Look"), Tooltip("Drawn under the ring: the empty part")] public Sprite TrackSprite;
        [BoxGroup("Look")] public Color TrackColor = new(0f, 0f, 0f, 0.45f);
        [BoxGroup("Look"), Tooltip("Ring colour by how full it is, highest first: a band's colour shows above its share; the last "
            + "(Above 0) covers the rest down to empty. The same on every need")]
        public ColorBand[] Bands =
        {
            new() { Above = 0.6f, Color = new Color(0.45f, 0.85f, 0.35f) },
            new() { Above = 0.3f, Color = new Color(1f, 0.86f, 0.25f) },
            new() { Above = 0.1f, Color = new Color(1f, 0.55f, 0.15f) },
            new() { Above = 0f, Color = new Color(0.95f, 0.22f, 0.18f) },
        };

        [BoxGroup("Dimming"), Range(0f, 1f), Tooltip("Alpha while the ring is in its top band (green) and nothing just changed")]
        public float DimAlpha = 0.45f;
        [BoxGroup("Dimming"), Min(0f), Tooltip("Real seconds a ring stays bright after a tier change or a refill")] public float BrightTime = 2.5f;
        [BoxGroup("Dimming"), Min(0.01f), Tooltip("Alpha change per real second")] public float FadeSpeed = 2f;

        [BoxGroup("Pulse"), Min(1f), Tooltip("Scale of the one punch on entering a tier")] public float PulseScale = 1.18f;
        [BoxGroup("Pulse"), Min(0.01f)] public float PulseTime = 0.35f;
        [BoxGroup("Pulse"), Min(0f), Tooltip("Pulses per second while empty (0 = none)")] public float EmptyPulseRate = 1.2f;
        [BoxGroup("Pulse"), Range(0f, 1f), Tooltip("Alpha the empty pulse dips to")] public float EmptyPulseAlpha = 0.45f;

        [BoxGroup("Sound"), Tooltip("Played on entering Low or worse (2D); empty = silent")] public SoundID TierSound;
        [BoxGroup("Sound"), Min(0f), Tooltip("Real seconds between repeats while VeryLow or Empty (0 = never)")] public float RepeatSoundEvery = 40f;

        [Serializable]
        public struct ColorBand
        {
            [Range(0f, 1f)] public float Above;
            public Color Color;
        }

        // The first band (highest first) whose share the value is above; the last band covers the rest.
        public Color ColorAt(float share)
        {
            if (Bands == null || Bands.Length == 0)
                return Color.white;
            for (int i = 0; i < Bands.Length - 1; i++)
                if (share > Bands[i].Above)
                    return Bands[i].Color;
            return Bands[^1].Color;
        }

        // Only the top band (green) dims: a ring in any warning colour stays bright.
        public bool Dims(float share) => Bands == null || Bands.Length == 0 || share > Bands[0].Above;

#if UNITY_EDITOR
        // Edit-time preview only: rings re-apply their style in Awake.
        void OnValidate() => StyleUtils.RefreshViews<NeedRing>(this, ring => ring.Style, ring => ring.ApplyStyle());
#endif
    }
}
