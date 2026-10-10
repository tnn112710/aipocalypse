using System;

namespace TextStudio
{
    /// <summary>Serializable all-phase or per-phase default effect expressions applied when markup omits explicit effects.</summary>
    [Serializable]
    public struct TextStudioDefaultEffectSettings : IEquatable<TextStudioDefaultEffectSettings>
    {
        public TextStudioDefaultEffectMode Mode;
        public string AllPhaseEffects;
        public string RevealEffects;
        public string ActiveEffects;
        public string HideEffects;

        public static TextStudioDefaultEffectSettings None
        {
            get
            {
                return new TextStudioDefaultEffectSettings
                {
                    Mode = TextStudioDefaultEffectMode.None,
                    AllPhaseEffects = string.Empty,
                    RevealEffects = string.Empty,
                    ActiveEffects = string.Empty,
                    HideEffects = string.Empty
                };
            }
        }

        /// <summary>Gets a behavior profile's default effects, or disabled settings when unavailable.</summary>
        public static TextStudioDefaultEffectSettings FromBehaviorProfile(
            TextStudioBehaviorProfile profile)
        {
            if (profile == null ||
                !profile.Provides(TextStudioBehaviorDomain.DefaultEffects))
                return None;

            return profile.DefaultEffectSettings;
        }

        public bool Equals(TextStudioDefaultEffectSettings other)
        {
            return Mode == other.Mode &&
                   string.Equals(AllPhaseEffects ?? string.Empty, other.AllPhaseEffects ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(RevealEffects ?? string.Empty, other.RevealEffects ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(ActiveEffects ?? string.Empty, other.ActiveEffects ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(HideEffects ?? string.Empty, other.HideEffects ?? string.Empty, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioDefaultEffectSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Mode;
                hash = (hash * 397) ^ (AllPhaseEffects ?? string.Empty).GetHashCode();
                hash = (hash * 397) ^ (RevealEffects ?? string.Empty).GetHashCode();
                hash = (hash * 397) ^ (ActiveEffects ?? string.Empty).GetHashCode();
                hash = (hash * 397) ^ (HideEffects ?? string.Empty).GetHashCode();
                return hash;
            }
        }
    }
}
