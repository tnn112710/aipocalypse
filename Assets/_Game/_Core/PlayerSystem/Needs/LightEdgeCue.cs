using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Stepping out of every safe light while it's dark: a cue (SanityPresenter plays a sound and the LeftLight screen look)
    // that marks the moment, then fades (user 2026-11-29: you can't see where safety ends). Not on the first look (a load or
    // a respawn outside), not by day, not again within the cooldown (walking along the edge).
    public sealed class LightEdgeCue
    {
        bool _known;
        bool _wasInLight;
        float _cooldown;
        float _since = float.PositiveInfinity;

        // The look's strength: rises over LeftLightRise, then fades over LeftLightFade.
        public float Strength { get; private set; }

        // True on the frame the player steps out (play the sound then).
        public bool Advance(bool inLight, float darkness, float deltaTime, SanityEffectSettings effects)
        {
            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
            _since += deltaTime;
            bool cue = _known && _wasInLight && !inLight && darkness > effects.LeftLightDarkness && _cooldown <= 0f;
            _known = true;
            _wasInLight = inLight;
            if (cue)
            {
                _since = 0f;
                _cooldown = effects.LeftLightCooldown;
            }
            Strength = Shape(_since, effects);
            return cue;
        }

        public void Reset()
        {
            _known = false;
            _cooldown = 0f;
            _since = float.PositiveInfinity;
            Strength = 0f;
        }

        static float Shape(float since, SanityEffectSettings effects)
        {
            if (float.IsInfinity(since))
                return 0f;
            if (since < effects.LeftLightRise)
                return since / effects.LeftLightRise;
            return effects.LeftLightFade > 0f ? Mathf.Clamp01(1f - (since - effects.LeftLightRise) / effects.LeftLightFade) : 0f;
        }
    }
}
