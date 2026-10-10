namespace TextStudio
{
    public enum TextStudioBehaviorValueSource
    {
        Component,
        BehaviorProfile
    }

    /// <summary>
    /// Resolves the three renderer-neutral behavior domains independently.
    /// </summary>
    public readonly struct TextStudioResolvedBehaviorSettings
    {
        /// <summary>Creates resolved settings and records the source selected for each behavior domain.</summary>
        public TextStudioResolvedBehaviorSettings(
            TextStudioDefaultEffectSettings defaultEffects,
            TextStudioTypewriterRecipe typewriter,
            TextStudioBehaviorValueSource defaultEffectsSource,
            TextStudioBehaviorValueSource typewriterSource)
        {
            DefaultEffects = defaultEffects;
            Typewriter = typewriter;
            DefaultEffectsSource = defaultEffectsSource;
            TypewriterSource = typewriterSource;
        }

        public TextStudioDefaultEffectSettings DefaultEffects { get; }
        public TextStudioTypewriterRecipe Typewriter { get; }
        public TextStudioBehaviorValueSource DefaultEffectsSource { get; }
        public TextStudioBehaviorValueSource TypewriterSource { get; }
        public bool UsesBehaviorProfileDefaultEffects =>
            DefaultEffectsSource == TextStudioBehaviorValueSource.BehaviorProfile;
        public bool UsesBehaviorProfileTypewriter =>
            TypewriterSource == TextStudioBehaviorValueSource.BehaviorProfile;
    }

    /// <summary>Resolves behavior domains from an optional profile and component-local fallbacks.</summary>
    public static class TextStudioBehaviorResolver
    {
        /// <summary>Resolves default effects and typewriter settings independently from profile or local values.</summary>
        public static TextStudioResolvedBehaviorSettings Resolve(
            TextStudioBehaviorProfile profile,
            TextStudioDefaultEffectSettings localDefaultEffects,
            TextStudioTypewriterRecipe localTypewriter)
        {
            bool useProfileDefaultEffects = profile != null &&
                profile.Provides(TextStudioBehaviorDomain.DefaultEffects);
            bool useProfileTypewriter = profile != null &&
                profile.Provides(TextStudioBehaviorDomain.Typewriter);
            return new TextStudioResolvedBehaviorSettings(
                useProfileDefaultEffects
                    ? profile.DefaultEffectSettings
                    : localDefaultEffects,
                useProfileTypewriter
                    ? profile.Typewriter
                    : localTypewriter,
                useProfileDefaultEffects
                    ? TextStudioBehaviorValueSource.BehaviorProfile
                    : TextStudioBehaviorValueSource.Component,
                useProfileTypewriter
                    ? TextStudioBehaviorValueSource.BehaviorProfile
                    : TextStudioBehaviorValueSource.Component);
        }
    }
}
