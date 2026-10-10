using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    internal enum CharacterSubstitutionPlayback
    {
        Infinite,
        Once,
        Loop
    }

#if UNITY_6000_7_OR_NEWER
    // Built-in substitution definitions are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInCharacterSubstitutions
    {
        public const string Scramble = "scramble";
        internal const float DefaultScrambleRevealDuration = 0.35f;

        public static ITextStudioCharacterSubstitution ScrambleSubstitution { get; } =
            new ScrambleCharacterSubstitution();

        public static bool TryGetCanonicalTagId(string tagName, out string canonicalId)
        {
            canonicalId = string.Empty;
            if (string.IsNullOrEmpty(tagName) ||
                !TextStudioRegistry.CharacterSubstitutions.TryGetQuiet(
                    tagName,
                    out ITextStudioCharacterSubstitution substitution) ||
                substitution == null)
            {
                return false;
            }

            canonicalId = substitution.Id;
            return true;
        }

        public static bool IsCharacterSubstitutionTag(string tagName)
        {
            return TryGetCanonicalTagId(tagName, out _);
        }

        public static TextStudioTagParameters CanonicalizeParameters(
            string tagId,
            in TextStudioTagParameters parameters)
        {
            if (!TextStudioRegistry.CharacterSubstitutions.TryGetQuiet(
                    tagId,
                    out ITextStudioCharacterSubstitution substitution) ||
                substitution == null)
            {
                return parameters;
            }

            return substitution.CanonicalizeParameters(in parameters);
        }

        internal static TextStudioTagParameters CanonicalizeScrambleParameters(
            in TextStudioTagParameters parameters)
        {
            if (parameters.PositionalCount == 0 && parameters.NamedCount == 0)
                return parameters;

            string playback = string.Empty;
            bool hasPlaybackParameter = parameters.TryGetString("playback", out _);

            List<string> positional = null;
            if (parameters.PositionalCount > 0)
            {
                positional = new List<string>(parameters.PositionalCount);
                for (int i = 0; i < parameters.PositionalCount; i++)
                {
                    string value = parameters.GetString(i);
                    if (!hasPlaybackParameter &&
                        BuiltInPlaybacks.TryResolveTextAnimatorKeyword(value, out string resolvedPlayback))
                    {
                        if (string.IsNullOrEmpty(playback))
                            playback = resolvedPlayback;
                        continue;
                    }

                    positional.Add(value);
                }
            }

            List<TextStudioNamedTagParameter> named = null;
            if (parameters.NamedCount > 0)
            {
                named = new List<TextStudioNamedTagParameter>(parameters.NamedCount + 1);
                for (int i = 0; i < parameters.NamedCount; i++)
                {
                    TextStudioNamedTagParameter source = parameters.Named[i];
                    string canonicalName = CanonicalizeScrambleParameterName(source.Name);
                    if (source.IsFlag &&
                        !hasPlaybackParameter &&
                        BuiltInPlaybacks.TryResolveTextAnimatorKeyword(canonicalName, out string resolvedPlayback))
                    {
                        if (string.IsNullOrEmpty(playback))
                            playback = resolvedPlayback;
                        continue;
                    }

                    named.Add(new TextStudioNamedTagParameter(
                        canonicalName,
                        source.Value,
                        source.IsFlag));
                }
            }

            if (!hasPlaybackParameter && !string.IsNullOrEmpty(playback))
            {
                if (named == null)
                    named = new List<TextStudioNamedTagParameter>(1);
                named.Add(new TextStudioNamedTagParameter("playback", playback, false));
            }

            return new TextStudioTagParameters(
                parameters.Raw,
                positional != null ? positional.ToArray() : Array.Empty<string>(),
                named != null ? named.ToArray() : Array.Empty<TextStudioNamedTagParameter>());
        }

        internal static float GetScrambleCompletionDuration(
            TextStudioPlanView plan,
            in CharacterSubstitutionBinding binding,
            ITextStudioRevealState reveal)
        {
            if (!plan.IsCreated ||
                string.IsNullOrEmpty(plan.ResolvedText) ||
                !ScrambleResolvesAtEnd(in binding.parameters))
            {
                return 0f;
            }

            float scrambleDuration = Mathf.Max(0f, GetFloat(in binding.parameters, "duration", 0, 0.5f));
            float revealDuration = Mathf.Max(
                0f,
                GetFloat(
                    in binding.parameters,
                    "revealDuration",
                    -1,
                    DefaultScrambleRevealDuration));
            float substitutionDuration = scrambleDuration + revealDuration;
            if (substitutionDuration <= 0f)
                return 0f;

            int lastGlyphIndex = FindLastSubstitutedGlyphIndex(plan, in binding, plan.VisibleGlyphCount);
            if (lastGlyphIndex < 0)
                return 0f;

            float revealTime = reveal != null ? reveal.GetApproximateRevealTime(lastGlyphIndex) : 0f;
            float actionDuration = GetDeterministicWaitDurationThroughGlyph(plan, lastGlyphIndex);
            return revealTime + actionDuration + substitutionDuration;
        }

        static int FindLastSubstitutedGlyphIndex(
            TextStudioPlanView plan,
            in CharacterSubstitutionBinding binding,
            int visibleGlyphCount)
        {
            bool preserveWhitespace = GetBool(in binding.parameters, "preserveWhitespace", true);
            bool preservePunctuation = GetBool(in binding.parameters, "preservePunctuation", false);
            int start = Mathf.Clamp(binding.glyphStart, 0, visibleGlyphCount);
            int end = Mathf.Clamp(binding.glyphStart + binding.glyphCount, 0, visibleGlyphCount);

            for (int glyphIndex = end - 1; glyphIndex >= start; glyphIndex--)
            {
                char character = plan.GetPrimaryChar(glyphIndex);
                if (preserveWhitespace && char.IsWhiteSpace(character))
                    continue;

                if (preservePunctuation && char.IsPunctuation(character))
                    continue;

                return glyphIndex;
            }

            return -1;
        }

        static float GetDeterministicWaitDurationThroughGlyph(TextStudioPlanView plan, int glyphIndex)
        {
            if (!plan.IsCreated || !plan.ActionMarkers.IsCreated || plan.ActionMarkerCount == 0)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < plan.ActionMarkerCount; i++)
            {
                ActionMarker marker = plan.ActionMarkers[i];
                if (marker.sourceIndex > glyphIndex)
                    break;

                if (!string.Equals(marker.actionId, BuiltInActions.Wait.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                marker.TryGetFloat(0, out float seconds);
                duration += seconds > 0f ? seconds : WaitAction.DefaultSeconds;
            }

            return duration;
        }

        static float GetFloat(
            in TextStudioTagParameters parameters,
            string name,
            int positionalIndex,
            float defaultValue)
        {
            if (!string.IsNullOrEmpty(name) && parameters.TryGetFloat(name, out float named))
                return named;

            if (positionalIndex >= 0 && parameters.TryGetFloat(positionalIndex, out float positional))
                return positional;

            return defaultValue;
        }

        static bool GetBool(in TextStudioTagParameters parameters, string name, bool defaultValue)
        {
            return parameters.TryGetBool(name, out bool value) ? value : defaultValue;
        }

        internal static CharacterSubstitutionPlayback ResolveScramblePlayback(in TextStudioTagParameters parameters)
        {
            if (parameters.TryGetString("playback", out string playback))
                return ResolveScramblePlayback(playback);

            return CharacterSubstitutionPlayback.Infinite;
        }

        internal static bool ScrambleResolvesAtEnd(in TextStudioTagParameters parameters)
        {
            return ResolveScramblePlayback(in parameters) == CharacterSubstitutionPlayback.Once;
        }

        static CharacterSubstitutionPlayback ResolveScramblePlayback(string playback)
        {
            if (string.IsNullOrWhiteSpace(playback))
                return CharacterSubstitutionPlayback.Infinite;

            string normalized = playback.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            if (string.Equals(normalized, "once", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "short", StringComparison.OrdinalIgnoreCase))
                return CharacterSubstitutionPlayback.Once;

            if (string.Equals(normalized, "loop", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "cycle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "pingpong", StringComparison.OrdinalIgnoreCase))
                return CharacterSubstitutionPlayback.Loop;

            return CharacterSubstitutionPlayback.Infinite;
        }

        static string CanonicalizeScrambleParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            if (string.Equals(name, "d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "time", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "seconds", StringComparison.OrdinalIgnoreCase))
                return "duration";

            if (string.Equals(name, "revealTime", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "resolveDuration", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "resolveTime", StringComparison.OrdinalIgnoreCase))
                return "revealDuration";

            if (string.Equals(name, "chars", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "characters", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "randomChars", StringComparison.OrdinalIgnoreCase))
                return "charset";

            if (string.Equals(name, "speed", StringComparison.OrdinalIgnoreCase))
                return "rate";

            return name;
        }

#if UNITY_6000_7_OR_NEWER
        // The canonical substitution definition is immutable after construction.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        sealed partial class ScrambleCharacterSubstitution : ITextStudioCharacterSubstitution
        {
            static readonly TextStudioCharacterSubstitutionDefinition s_definition =
                new TextStudioCharacterSubstitutionDefinition(
                    Scramble,
                    "Scramble",
                    "Substitutes visible glyphs with random characters for a timed scramble/decode presentation, then resolves one-shot playback over a short reveal phase.",
                    examples: new[]
                    {
                        "<scramble>ACCESS</scramble>",
                        "<scramble once duration=0.8 revealDuration=0.35 charset=hex>ACCESS GRANTED</scramble>",
                        "<scramble loop duration=0.8 charset=binary seed=7>0101</scramble>"
                    },
                    parameters: new[]
                    {
                        new TextStudioTagParameterDefinition(
                            "playback",
                            TextStudioTagParameterKind.Enum,
                            description: "Controls scramble playback. Bare and infinite stay active, once resolves after duration, and loop repeats.",
                            allowedValues: BuiltInPlaybacks.CopyTextAnimatorPlaybackKeywords(),
                            defaultValue: "infinite"),
                        new TextStudioTagParameterDefinition(
                            "duration",
                            TextStudioTagParameterKind.Float,
                            positionalIndex: 0,
                            description: "Seconds before one-shot scramble resolves, or the cycle length for loop playback.",
                            aliases: new[] { "d", "time", "seconds" },
                            defaultValue: "0.5"),
                        new TextStudioTagParameterDefinition(
                            "revealDuration",
                            TextStudioTagParameterKind.Float,
                            description: "Seconds used by one-shot scramble to progressively lock scrambled glyphs back to the source text. Set to 0 to resolve instantly.",
                            aliases: new[] { "revealTime", "resolveDuration", "resolveTime" },
                            defaultValue: "0.35"),
                        new TextStudioTagParameterDefinition(
                            "charset",
                            TextStudioTagParameterKind.String,
                            positionalIndex: 1,
                            description: "Built-in character set or literal characters to use while scrambling.",
                            aliases: new[] { "chars", "characters", "randomChars" },
                            defaultValue: "ascii"),
                        new TextStudioTagParameterDefinition(
                            "seed",
                            TextStudioTagParameterKind.Integer,
                            description: "Deterministic random seed for repeatable scramble frames.",
                            defaultValue: "0"),
                        new TextStudioTagParameterDefinition(
                            "rate",
                            TextStudioTagParameterKind.Float,
                            description: "Character substitution updates per second. Defaults are based on charset size; explicit values are used as authored.",
                            aliases: new[] { "speed" },
                            exampleValue: "12"),
                        new TextStudioTagParameterDefinition(
                            "widthMode",
                            TextStudioTagParameterKind.Enum,
                            description: "Controls whether scramble characters are selected from width-similar candidates or from the full character set.",
                            allowedValues: new[] { "matched", "any" },
                            defaultValue: "matched"),
                        new TextStudioTagParameterDefinition(
                            "preserveWhitespace",
                            TextStudioTagParameterKind.Boolean,
                            description: "Keeps whitespace characters unchanged while scrambling.",
                            defaultValue: "true"),
                        new TextStudioTagParameterDefinition(
                            "preservePunctuation",
                            TextStudioTagParameterKind.Boolean,
                            description: "Keeps punctuation characters unchanged while scrambling.",
                            defaultValue: "false")
                    });

            public string Id => Scramble;
            public TextStudioCharacterSubstitutionDefinition Definition => s_definition;

            public TextStudioTagParameters CanonicalizeParameters(in TextStudioTagParameters parameters)
            {
                return CanonicalizeScrambleParameters(in parameters);
            }

            public ITextStudioCharacterSubstitutionRuntime CreateRuntime()
            {
                return new TextStudioScramblePresentation();
            }

            public float GetCompletionDuration(
                TextStudioPlanView plan,
                in CharacterSubstitutionBinding binding,
                ITextStudioRevealState reveal)
            {
                return GetScrambleCompletionDuration(plan, in binding, reveal);
            }
        }
    }
}
