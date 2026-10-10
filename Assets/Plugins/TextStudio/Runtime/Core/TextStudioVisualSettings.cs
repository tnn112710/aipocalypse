using System;

namespace TextStudio
{
    /// <summary>Chooses a built-in preset, custom preset asset, or inline settings as one resolvable typewriter recipe.</summary>
    [Serializable]
    public struct TextStudioTypewriterRecipe : IEquatable<TextStudioTypewriterRecipe>
    {
        public TextStudioTypewriterSource Source;
        public string BuiltInPresetId;
        public TextStudioTypewriterPreset CustomPreset;
        public TextStudioTypewriterSettings InlineSettings;

        public static TextStudioTypewriterRecipe None
        {
            get
            {
                return new TextStudioTypewriterRecipe
                {
                    Source = TextStudioTypewriterSource.None,
                    BuiltInPresetId = BuiltIns.BuiltInTypewriterPresets.Default.Id,
                    CustomPreset = null,
                    InlineSettings = TextStudioTypewriterSettings.Default
                };
            }
        }

        /// <summary>Creates a recipe that resolves a built-in preset by stable identifier.</summary>
        public static TextStudioTypewriterRecipe BuiltIn(string presetId)
        {
            TextStudioTypewriterRecipe recipe = None;
            recipe.Source = TextStudioTypewriterSource.BuiltIn;
            recipe.BuiltInPresetId = string.IsNullOrEmpty(presetId)
                ? BuiltIns.BuiltInTypewriterPresets.Default.Id
                : presetId;
            return recipe;
        }

        /// <summary>Creates a recipe backed by a reusable custom preset asset.</summary>
        public static TextStudioTypewriterRecipe Custom(TextStudioTypewriterPreset preset)
        {
            TextStudioTypewriterRecipe recipe = None;
            recipe.Source = TextStudioTypewriterSource.Custom;
            recipe.CustomPreset = preset;
            return recipe;
        }

        /// <summary>Creates a recipe containing an independent inline settings snapshot.</summary>
        public static TextStudioTypewriterRecipe Inline(TextStudioTypewriterSettings settings)
        {
            TextStudioTypewriterRecipe recipe = None;
            recipe.Source = TextStudioTypewriterSource.Inline;
            recipe.InlineSettings = NormalizeInlineSettings(settings);
            return recipe;
        }

        /// <summary>Resolves the selected source into normalized runtime settings with safe fallbacks.</summary>
        public TextStudioTypewriterSettings Resolve()
        {
            switch (Source)
            {
                case TextStudioTypewriterSource.None:
                    return CreateNoTypewriterSettings();
                case TextStudioTypewriterSource.Custom:
                    return CustomPreset != null
                        ? TextStudioTypewriterSettings.ResolveRuntimeSettings(CustomPreset.Settings)
                        : CreateNoTypewriterSettings();
                case TextStudioTypewriterSource.Inline:
                    return TextStudioTypewriterSettings.ResolveRuntimeSettings(InlineSettings);
                default:
                {
                    TextStudio.BuiltIns.TextStudioBuiltInTypewriterPreset? preset =
                        BuiltIns.BuiltInTypewriterPresets.ForId(BuiltInPresetId);
                    return preset.HasValue
                        ? TextStudioTypewriterSettings.ResolveRuntimeSettings(preset.Value.Settings)
                        : TextStudioTypewriterSettings.ResolveRuntimeSettings(BuiltIns.BuiltInTypewriterPresets.Default.Settings);
                }
            }
        }

        public bool Equals(TextStudioTypewriterRecipe other)
        {
            return Source == other.Source &&
                   string.Equals(BuiltInPresetId ?? string.Empty, other.BuiltInPresetId ?? string.Empty, StringComparison.Ordinal) &&
                   CustomPreset == other.CustomPreset &&
                   TypewriterSettingsEqual(InlineSettings, other.InlineSettings);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioTypewriterRecipe other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Source;
                hash = (hash * 397) ^ (BuiltInPresetId ?? string.Empty).GetHashCode();
                hash = (hash * 397) ^ (CustomPreset != null ? CustomPreset.GetHashCode() : 0);
                hash = (hash * 397) ^ InlineSettings.GetHashCode();
                return hash;
            }
        }

        /// <summary>Creates settings that display text immediately without reveal or hide motion.</summary>
        public static TextStudioTypewriterSettings CreateNoTypewriterSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = RevealMode.Instant;
            settings.Timings = null;
            settings.BuiltInTimingsPreset = RevealTimingsPreset.Uniform;
            settings.RevealSpeedMultiplier = 1f;
            settings.LoopReveal = false;
            settings.LoopRevealDelay = 0f;
            settings.RevealEffect = null;
            settings.HideEffect = null;
            settings.RevealEffectExpression = string.Empty;
            settings.HideEffectExpression = string.Empty;
            return settings;
        }

        static TextStudioTypewriterSettings NormalizeInlineSettings(TextStudioTypewriterSettings settings)
        {
            return TextStudioTypewriterSettings.NormalizeAuthoringSettings(settings);
        }

        static bool TypewriterSettingsEqual(TextStudioTypewriterSettings a, TextStudioTypewriterSettings b)
        {
            return a.TimingMode == b.TimingMode && a.WritingEnabled == b.WritingEnabled && a.WritingStyle == b.WritingStyle && a.EraseOnHide == b.EraseOnHide && a.RevealMode == b.RevealMode &&
                   a.Timings == b.Timings &&
                   a.BuiltInTimingsPreset == b.BuiltInTimingsPreset &&
                   a.WaitAfterFinalReveal == b.WaitAfterFinalReveal &&
                   Math.Abs(a.RevealSpeedMultiplier - b.RevealSpeedMultiplier) < 0.0001f &&
                   Math.Abs(a.RevealAppearanceDuration - b.RevealAppearanceDuration) < 0.0001f &&
                   Math.Abs(a.RevealDisappearanceDuration - b.RevealDisappearanceDuration) < 0.0001f &&
                   a.RevealSequence.Equals(b.RevealSequence) &&
                   a.HideSequence.Equals(b.HideSequence) &&
                   a.StartMode == b.StartMode &&
                   a.TextChangeMode == b.TextChangeMode &&
                   a.TextChangeScope == b.TextChangeScope &&
                   a.ResetSpeedOnNewText == b.ResetSpeedOnNewText &&
                   a.SkipRevealMode == b.SkipRevealMode &&
                   a.TriggerRemainingMarkersOnSkip == b.TriggerRemainingMarkersOnSkip &&
                   a.SkipCurrentWaitOnSkip == b.SkipCurrentWaitOnSkip &&
                   a.SkipHideMode == b.SkipHideMode &&
                   a.LoopReveal == b.LoopReveal &&
                   Math.Abs(a.LoopRevealDelay - b.LoopRevealDelay) < 0.0001f &&
                   a.RevealEffect == b.RevealEffect &&
                   a.HideEffect == b.HideEffect &&
                   string.Equals(a.RevealEffectExpression ?? string.Empty, b.RevealEffectExpression ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(a.HideEffectExpression ?? string.Empty, b.HideEffectExpression ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>Complete renderer-neutral behavior snapshot used by behavior profiles and local hosts.</summary>
    [Serializable]
    public struct TextStudioBehaviorSettings : IEquatable<TextStudioBehaviorSettings>
    {
        public TextStudioBehaviorDomain Domains;
        public TextStudioDefaultEffectSettings DefaultEffects;
        public TextStudioTypewriterRecipe Typewriter;
        public TextStudioReactionRecipe Reactions;

        public static TextStudioBehaviorSettings LocalDefault
        {
            get
            {
                return new TextStudioBehaviorSettings
                {
                    Domains = TextStudioBehaviorDomain.DefaultEffects |
                              TextStudioBehaviorDomain.Typewriter,
                    DefaultEffects = TextStudioDefaultEffectSettings.None,
                    Typewriter = TextStudioTypewriterRecipe.None
                };
            }
        }

        public bool Equals(TextStudioBehaviorSettings other)
        {
            return Domains == other.Domains &&
                   DefaultEffects.Equals(other.DefaultEffects) &&
                   Typewriter.Equals(other.Typewriter) &&
                   Reactions.Equals(other.Reactions);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioBehaviorSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Domains;
                hash = (hash * 397) ^ DefaultEffects.GetHashCode();
                hash = (hash * 397) ^ Typewriter.GetHashCode();
                hash = (hash * 397) ^ Reactions.GetHashCode();
                return hash;
            }
        }
    }
}
