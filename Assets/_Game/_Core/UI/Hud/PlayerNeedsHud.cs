using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.PlayerSystem.Needs;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // Hunger, thirst and sanity rings for the active player: values each frame, a pulse and the style's body sound when a
    // need enters a worse tier, the sound again now and then while very low. Hidden when the player has no needs.
    public sealed class PlayerNeedsHud : PlayerHudView
    {
        sealed class Gauge
        {
            public NeedRing Ring;
            public Need Need;
            public float SoundLeft;
        }

        [SerializeField] NeedRing _hunger;
        [SerializeField] NeedRing _thirst;
        [SerializeField] NeedRing _sanity;
        [SerializeField, Tooltip("Shown only while the player has needs")] GameObject _root;

        AudioService _audio;
        readonly Gauge[] _gauges = { new(), new(), new() };

        [Inject]
        void Construct(AudioService audio) => _audio = audio;

        protected override void Bind(IPlayer player)
        {
            PlayerNeeds needs = player.Needs;
            if (_root != null)
                _root.SetActive(needs != null);
            if (needs == null)
                return;
            Track(_gauges[0], _hunger, needs.Hunger);
            Track(_gauges[1], _thirst, needs.Thirst);
            Track(_gauges[2], _sanity, needs.Sanity);
        }

        protected override void Unbind(IPlayer player)
        {
            foreach (Gauge gauge in _gauges)
                gauge.Need = null;
            if (_root != null)
                _root.SetActive(false);
        }

        static void Track(Gauge gauge, NeedRing ring, Need need)
        {
            gauge.Ring = ring;
            gauge.Need = need;
            gauge.SoundLeft = 0f;
            if (ring == null)
                return;
            ring.SetValue(need.Normalized);
            ring.SetSeverity(need.Severity, false);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float flicker = SanityGlitch.TryRead(Player, out float sanity, out SanityEffectSettings effects)
                ? SanityGlitch.Flicker(sanity, Time.time, effects.LiesFrom) : 1f;
            foreach (Gauge gauge in _gauges)
            {
                if (gauge.Need == null || gauge.Ring == null)
                    continue;
                NeedSeverity severity = gauge.Need.Severity;
                bool worse = severity > gauge.Ring.Severity;
                if (severity != gauge.Ring.Severity)
                {
                    gauge.Ring.SetSeverity(severity, pulse: true);
                    if (worse && severity >= NeedSeverity.Low)
                        PlaySound(gauge);
                }
                gauge.Ring.SetValue(gauge.Need.Normalized);
                gauge.Ring.SetFlicker(flicker);
                if (severity >= NeedSeverity.VeryLow && (gauge.SoundLeft -= dt) <= 0f)
                    PlaySound(gauge);
            }
        }

        void PlaySound(Gauge gauge)
        {
            NeedRingStyle style = gauge.Ring.Style;
            gauge.SoundLeft = style != null && style.RepeatSoundEvery > 0f ? style.RepeatSoundEvery : float.PositiveInfinity;
            if (style != null && style.TierSound.IsValid())
                _audio?.Play(style.TierSound);
        }
    }
}
