using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Non-asset timings used as fallbacks when no <see cref="TypingTimingsBase"/>
    /// ScriptableObject is assigned on a hub. Selected via the hub's
    /// <c>_builtInTimingsPreset</c> / inspector dropdown.
    /// </summary>
    public sealed class CodeTimings : ITextStudioBuiltIn
    {
        readonly TextStudioTimingEvaluator _evaluator;

        public string Id { get; }
        public string Category => "Timings";
        public string DisplayName { get; }
        public string Description { get; }

        public CodeTimings(
            string id,
            string displayName,
            string description,
            TextStudioTimingEvaluator evaluator)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            _evaluator = evaluator;
        }

        public float GetRevealDelayAfter(in TextStudioTimingContext context)
            => _evaluator != null ? _evaluator(in context) : 0f;
    }

    /// <summary>
    /// Curated library of reveal timing presets. Each preset produces a per-glyph
    /// delay in seconds. Designed so authors can pick one on the reveal tab and get
    /// a polished result without authoring a <see cref="TypingTimingsBase"/> asset.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Built-in timing definitions are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInTimings
    {
        static float Base => Mathf.Max(0f, TextStudioDefaultSettings.Instance.defaultCharDelay);

        // Small deterministic hash so presets can jitter without System.Random allocations.
        static float Hash01(int glyphIndex, int salt)
        {
            unchecked
            {
                int h = glyphIndex * 374761393 + salt * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        static bool IsTerminator(char c) => c == '.' || c == '!' || c == '?';
        static bool IsMidPunct(char c) => c == ',' || c == ';' || c == ':' || c == '-';

        // ── Presets ─────────────────────────────────────────────────────────

        public static readonly CodeTimings Uniform = new CodeTimings(
            "timings.uniform", "Uniform",
            "Constant delay per character. Smooth, predictable, no pauses.",
            (in TextStudioTimingContext context) => Base);

        public static readonly CodeTimings Fast = new CodeTimings(
            "timings.fast", "Fast",
            "Rapid typewriter - half the base delay. Great for action UI.",
            (in TextStudioTimingContext context) => Base * 0.5f);

        public static readonly CodeTimings Slow = new CodeTimings(
            "timings.slow", "Slow",
            "Deliberate narration - double the base delay. Lets the reader savor each word.",
            (in TextStudioTimingContext context) => Base * 2f);

        public static readonly CodeTimings Expressive = new CodeTimings(
            "timings.expressive", "Expressive",
            "Natural reading pacing: long pause on sentence terminators (.!?), medium on " +
            "commas and separators (,;:-). The best compromise between flow and pacing.",
            (in TextStudioTimingContext context) =>
            {
                char c = context.PrimaryCharacter;
                if (IsTerminator(c)) return Base * 20f;
                if (c == ',') return Base * 10f;
                if (IsMidPunct(c)) return Base * 6.67f;
                return Base;
            });

        public static readonly CodeTimings Cinematic = new CodeTimings(
            "timings.cinematic", "Cinematic",
            "Dramatic pacing: heavy pauses after sentences, generous breathing after " +
            "commas, and a small wait after every word. Dialogue-cutscene feel.",
            (in TextStudioTimingContext context) =>
            {
                char c = context.PrimaryCharacter;
                if (IsTerminator(c)) return Base * 35f;
                if (c == ',') return Base * 15f;
                if (IsMidPunct(c)) return Base * 10f;
                if (c == ' ') return Base * 4f;
                return Base * 1.3f;
            });

        public static readonly CodeTimings Staccato = new CodeTimings(
            "timings.staccato", "Staccato",
            "Quarter-speed - tight rapid-fire reveal. Punchy for notifications and " +
            "terminal-style text.",
            (in TextStudioTimingContext context) => Base * 0.25f);

        public static readonly CodeTimings ByWord = new CodeTimings(
            "timings.byWord", "By Word",
            "Characters within a word flash in together; a real pause lands between words.",
            (in TextStudioTimingContext context) =>
            {
                char c = context.PrimaryCharacter;
                if (c == ' ') return Base * 10f;
                if (c == '\n') return Base * 12f;
                return 0f;
            });

        public static readonly CodeTimings Typist = new CodeTimings(
            "timings.typist", "Typist",
            "Human-feeling typewriter: each character has a small ±40% jitter so the " +
            "rhythm doesn't feel mechanical. Occasional longer micro-pauses.",
            (in TextStudioTimingContext context) =>
            {
                char c = context.PrimaryCharacter;
                int glyphIndex = context.UnitIndex;
                float j = Hash01(glyphIndex, 0x74597); // 0..1
                float jitter = 0.6f + j * 0.8f; // 0.6..1.4
                float d = Base * jitter;
                // 8% of glyphs feel like a mini-hesitation
                if (Hash01(glyphIndex, 0xA137F) < 0.08f) d += Base * 2f;
                // Still respect terminator breathing (but lighter than Expressive)
                if (IsTerminator(c)) d += Base * 6f;
                else if (c == ',') d += Base * 3f;
                return d;
            });

        public static readonly CodeTimings Narrative = new CodeTimings(
            "timings.narrative", "Narrative",
            "Gentle dialogue-box pacing: characters tick at a calm rate with a longer " +
            "breath at sentence ends, and a soft pause on commas. No heavy theatrics.",
            (in TextStudioTimingContext context) =>
            {
                char c = context.PrimaryCharacter;
                if (IsTerminator(c)) return Base * 14f;
                if (c == ',') return Base * 7f;
                if (IsMidPunct(c)) return Base * 4f;
                return Base * 1.2f;
            });

        public static System.Collections.Generic.IEnumerable<CodeTimings> All()
        {
            yield return Uniform;
            yield return Fast;
            yield return Slow;
            yield return Expressive;
            yield return Cinematic;
            yield return Staccato;
            yield return ByWord;
            yield return Typist;
            yield return Narrative;
        }

        public static CodeTimings Default => Uniform;

        public static CodeTimings ForId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (CodeTimings timing in All())
            {
                if (string.Equals(timing.Id, id, StringComparison.OrdinalIgnoreCase))
                    return timing;
            }

            return null;
        }

        public static bool TryGetPreset(string id, out TextStudio.RevealTimingsPreset preset)
        {
            foreach (TextStudio.RevealTimingsPreset candidate in Enum.GetValues(typeof(TextStudio.RevealTimingsPreset)))
            {
                CodeTimings timing = For(candidate);
                if (timing != null && string.Equals(timing.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    preset = candidate;
                    return true;
                }
            }

            preset = TextStudio.RevealTimingsPreset.Uniform;
            return false;
        }

        /// <summary>
        /// Resolve a <see cref="RevealTimingsPreset"/> enum to its backing
        /// <see cref="CodeTimings"/> implementation.
        /// </summary>
        public static CodeTimings For(TextStudio.RevealTimingsPreset preset)
        {
            switch (preset)
            {
                case TextStudio.RevealTimingsPreset.Fast:       return Fast;
                case TextStudio.RevealTimingsPreset.Slow:       return Slow;
                case TextStudio.RevealTimingsPreset.Expressive: return Expressive;
                case TextStudio.RevealTimingsPreset.Cinematic:  return Cinematic;
                case TextStudio.RevealTimingsPreset.Staccato:   return Staccato;
                case TextStudio.RevealTimingsPreset.ByWord:     return ByWord;
                case TextStudio.RevealTimingsPreset.Typist:     return Typist;
                case TextStudio.RevealTimingsPreset.Narrative:  return Narrative;
                default:                                        return Uniform;
            }
        }
    }

    public readonly struct TextStudioBuiltInTypewriterPreset : ITextStudioBuiltIn, IEquatable<TextStudioBuiltInTypewriterPreset>
    {
        public readonly RevealMode Mode;
        public readonly string TimingPresetId;
        public readonly string SampleText;
        public readonly TextStudioTypewriterSettings Settings;

        public string Id { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public TextStudioBuiltInTypewriterPreset(
            string id,
            string displayName,
            string category,
            string description,
            RevealMode mode,
            string timingPresetId,
            string revealEffectExpression,
            string hideEffectExpression,
            string sampleText)
            : this(
                id,
                displayName,
                category,
                description,
                mode,
                timingPresetId,
                sampleText,
                CreateSettings(mode, timingPresetId, revealEffectExpression, hideEffectExpression))
        {
        }

        internal TextStudioBuiltInTypewriterPreset(
            string id,
            string displayName,
            string category,
            string description,
            RevealMode mode,
            string timingPresetId,
            string sampleText,
            TextStudioTypewriterSettings settings)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            Mode = mode;
            TimingPresetId = timingPresetId;
            SampleText = sampleText;
            settings.RevealMode = mode;
            Settings = NormalizeSettings(settings);
        }

        internal static TextStudioTypewriterSettings CreateSettings(
            RevealMode mode,
            string timingPresetId,
            string revealEffectExpression,
            string hideEffectExpression)
        {
            TextStudioTypewriterSettings settings = TextStudioTypewriterSettings.Default;
            settings.RevealMode = mode;
            settings.StartMode = TextStudioTypewriterStartMode.OnEnable;
            if (BuiltInTimings.TryGetPreset(timingPresetId, out RevealTimingsPreset timingPreset))
                settings.BuiltInTimingsPreset = timingPreset;
            settings.RevealEffectExpression = revealEffectExpression ?? string.Empty;
            settings.HideEffectExpression = hideEffectExpression ?? string.Empty;
            return NormalizeSettings(settings);
        }

        static TextStudioTypewriterSettings NormalizeSettings(TextStudioTypewriterSettings settings)
        {
            return TextStudioTypewriterSettings.NormalizeAuthoringSettings(settings);
        }

        public bool Equals(TextStudioBuiltInTypewriterPreset other)
            => string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is TextStudioBuiltInTypewriterPreset other && Equals(other);

        public override int GetHashCode()
            => Id != null ? Id.GetHashCode() : 0;
    }

#if UNITY_6000_7_OR_NEWER
    // Built-in typewriter presets are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInTypewriterPresets
    {
        const string FadeEffect = "<fade>";
        const string PaperStrikeReveal = "<fade><size scale=1.12><slideVertical a=.045><slideHorizontal a=.012>";
        const string DialogueLiftReveal = "<fade><slideVertical a=.055>";
        const string DialogueLiftHide = "<fade><slideVertical a=.045>";
        const string UiPopReveal = "<fade><elastic strength=.18 cycles=1.5 damping=6>";
        const string UiPopHide = "<fade><size scale=.92>";
        const string WordPopReveal = "<fade><pop a=.25>";
        const string CinematicRiseReveal = "<fade><tracking amount=.08><slideVertical a=.075>";
        const string CinematicRiseHide = "<fade><slideVertical a=.08>";
        const string TerminalReveal = "<fade><slideHorizontal a=.025>";
        const string CarriageReveal = "<fade><slideHorizontal a=.045>";
        const string CarriageHide = "<fade><slideHorizontal a=-.04>";
        const string ScatterReveal = "<fade><scatter2D distance=.16 cone=35 radial=false>";
        const string ScatterHide = "<fade><scatter2D distance=.22 cone=55 radial=false>";

        public static readonly TextStudioBuiltInTypewriterPreset ClassicTypewriter = new TextStudioBuiltInTypewriterPreset(
            "typewriter.classic",
            "Classic Typewriter",
            "Typewriter",
            "Old-school typewriter pacing with a restrained reveal effect.",
            RevealMode.ByGlyph,
            BuiltInTimings.Typist.Id,
            PaperStrikeReveal,
            FadeEffect,
            "The carriage returns at dawn.");

        public static readonly TextStudioBuiltInTypewriterPreset SmoothDialogue = new TextStudioBuiltInTypewriterPreset(
            "typewriter.dialogue.smooth",
            "Dialogue Typewriter",
            "Dialogue",
            "Balanced glyph-by-glyph dialogue with readable punctuation and subtle Reveal and Hide effects.",
            RevealMode.ByGlyph,
            BuiltInTimings.Narrative.Id,
            DialogueLiftReveal,
            DialogueLiftHide,
            "Let me tell you something...");

        public static readonly TextStudioBuiltInTypewriterPreset FastUi = new TextStudioBuiltInTypewriterPreset(
            "typewriter.ui.fast",
            "Fast UI",
            "UI",
            "Fast glyph reveal with a compact effect for action UI and notifications.",
            RevealMode.ByGlyph,
            BuiltInTimings.Fast.Id,
            UiPopReveal,
            UiPopHide,
            "Objective updated");

        public static readonly TextStudioBuiltInTypewriterPreset ToastFlash = new TextStudioBuiltInTypewriterPreset(
            "typewriter.ui.toastFlash",
            "Toast Flash",
            "UI",
            "Fast short-message reveal with an all-at-once hide for toasts, counters, and reward labels.",
            RevealMode.ByWord,
            BuiltInTimings.Fast.Id,
            "10",
            CreateToastFlashSettings());

        public static readonly TextStudioBuiltInTypewriterPreset ReactiveCounter = new TextStudioBuiltInTypewriterPreset(
            "typewriter.ui.reactiveCounter",
            "Reactive Counter",
            "UI",
            "All-at-once counter changes that retain outgoing renderer visuals when supported and otherwise hide cleanly before the next value.",
            RevealMode.ByGlyph,
            BuiltInTimings.Fast.Id,
            "10",
            CreateReactiveCounterSettings());

        public static readonly TextStudioBuiltInTypewriterPreset WordFade = new TextStudioBuiltInTypewriterPreset(
            "typewriter.word.fade", "Word Fade", "Editorial", "Reveal each word with a clean fade using the shared Typewriter cadence.",
            RevealMode.ByWord, BuiltInTimings.ByWord.Id, FadeEffect, FadeEffect, "Words set the pace");

        public static readonly TextStudioBuiltInTypewriterPreset LetterFade = new TextStudioBuiltInTypewriterPreset(
            "typewriter.letter.fade", "Letter Fade", "Editorial", "Reveal shaped letters with a clean fade using the shared Typewriter cadence.",
            RevealMode.ByLetter, BuiltInTimings.Narrative.Id, FadeEffect, FadeEffect, "Letter by letter");

        public static readonly TextStudioBuiltInTypewriterPreset WordPop = new TextStudioBuiltInTypewriterPreset(
            "typewriter.word.pop",
            "Word Pop",
            "Dialogue",
            "Word-paced reveal with a playful pop effect on each unit.",
            RevealMode.ByWord,
            BuiltInTimings.ByWord.Id,
            WordPopReveal,
            FadeEffect,
            "Each word gets focus");

        public static readonly TextStudioBuiltInTypewriterPreset CinematicLine = new TextStudioBuiltInTypewriterPreset(
            "typewriter.line.cinematic",
            "Cinematic Line",
            "Cinematic",
            "Line-paced reveal for subtitles and chapter cards with restrained Reveal and Hide effects.",
            RevealMode.ByLine,
            BuiltInTimings.Cinematic.Id,
            CinematicRiseReveal,
            CinematicRiseHide,
            "First line.\nSecond line.");

        public static readonly TextStudioBuiltInTypewriterPreset TerminalStaccato = new TextStudioBuiltInTypewriterPreset(
            "typewriter.terminal.staccato",
            "Terminal Staccato",
            "Terminal",
            "Rapid terminal-style reveal with a tight horizontal effect.",
            RevealMode.ByGlyph,
            BuiltInTimings.Staccato.Id,
            TerminalReveal,
            FadeEffect,
            "BOOT SEQUENCE READY");

        public static readonly TextStudioBuiltInTypewriterPreset LoopingSignal = new TextStudioBuiltInTypewriterPreset(
            "typewriter.loop.signal",
            "Looping Signal",
            "Looping",
            "Continuous fast reveal that repeats after a short hold for ambient UI, terminals, and looping signage.",
            RevealMode.ByGlyph,
            BuiltInTimings.Staccato.Id,
            "SIGNAL LOCKED",
            CreateLoopingSignalSettings());

        public static readonly TextStudioBuiltInTypewriterPreset ReverseCaption = new TextStudioBuiltInTypewriterPreset(
            "typewriter.hide.reverseCaption",
            "Reverse Caption",
            "Dismissal",
            "Readable caption reveal that dismisses from the end back to the start.",
            RevealMode.ByGlyph,
            BuiltInTimings.Narrative.Id,
            "The last word leaves first.",
            CreateReverseCaptionSettings());

        public static readonly TextStudioBuiltInTypewriterPreset ScatterVanish = new TextStudioBuiltInTypewriterPreset(
            "typewriter.hide.scatterVanish",
            "Scatter Vanish",
            "Dismissal",
            "Deterministic random disappearance for magical, corrupted, or high-emphasis text.",
            RevealMode.ByGlyph,
            BuiltInTimings.Expressive.Id,
            "The message breaks apart.",
            CreateScatterVanishSettings());

        public static readonly TextStudioBuiltInTypewriterPreset LowMotion = new TextStudioBuiltInTypewriterPreset(
            "typewriter.accessibility.lowMotion",
            "Low Motion",
            "Accessibility",
            "Readable reveal with fade-only effects for motion-sensitive interfaces.",
            RevealMode.ByGlyph,
            BuiltInTimings.Narrative.Id,
            FadeEffect,
            FadeEffect,
            "Readable without extra movement.");

        public static readonly TextStudioBuiltInTypewriterPreset InstantAccessible = new TextStudioBuiltInTypewriterPreset(
            "typewriter.accessibility.instant",
            "Instant Accessible",
            "Accessibility",
            "No timed reveal or transition effects; useful for accessibility-first fallbacks.",
            RevealMode.Instant,
            BuiltInTimings.Uniform.Id,
            string.Empty,
            string.Empty,
            "Everything is visible immediately.");

        static TextStudioTypewriterSettings CreateToastFlashSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioBuiltInTypewriterPreset.CreateSettings(
                RevealMode.ByWord,
                BuiltInTimings.Fast.Id,
                UiPopReveal,
                UiPopHide);
            settings.RevealSequence = new TextStudioSequenceSettings
            {
                Order = TextStudioSequenceOrder.AllAtOnce,
                Spread = 0f,
                Seed = 1729
            };
            settings.HideSequence = settings.RevealSequence;
            settings.RevealAppearanceDuration = 0.18f;
            settings.RevealDisappearanceDuration = 0.18f;
            settings.SkipHideMode = TextStudioTypewriterSkipHideMode.FinishHideEffect;
            return settings;
        }

        static TextStudioTypewriterSettings CreateLoopingSignalSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioBuiltInTypewriterPreset.CreateSettings(
                RevealMode.ByGlyph,
                BuiltInTimings.Staccato.Id,
                TerminalReveal,
                FadeEffect);
            settings.LoopReveal = true;
            settings.LoopRevealDelay = 0.55f;
            settings.RevealAppearanceDuration = 0.16f;
            settings.RevealDisappearanceDuration = 0.16f;
            return settings;
        }

        static TextStudioTypewriterSettings CreateReactiveCounterSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioBuiltInTypewriterPreset.CreateSettings(
                RevealMode.ByGlyph,
                BuiltInTimings.Fast.Id,
                UiPopReveal,
                UiPopHide);
            TextStudioSequenceSettings sequence = TextStudioSequenceSettings.Default;
            sequence.Order = TextStudioSequenceOrder.AllAtOnce;
            sequence.Spread = 0f;
            settings.RevealSequence = sequence;
            settings.HideSequence = sequence;
            settings.StartMode = TextStudioTypewriterStartMode.OnEnableOrTextChanged;
            settings.TextChangeMode = TextStudioTextChangeMode.OverlapHandoff;
            settings.TextChangeScope = TextStudioTextChangeScope.ChangedGlyphs;
            settings.RevealAppearanceDuration = 0.18f;
            settings.RevealDisappearanceDuration = 0.2f;
            settings.SkipHideMode = TextStudioTypewriterSkipHideMode.FinishHideEffect;
            return settings;
        }

        static TextStudioTypewriterSettings CreateReverseCaptionSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioBuiltInTypewriterPreset.CreateSettings(
                RevealMode.ByGlyph,
                BuiltInTimings.Narrative.Id,
                CarriageReveal,
                CarriageHide);
            settings.HideSequence = TextStudioSequenceSettings.Default;
            settings.HideSequence.Order = TextStudioSequenceOrder.Backward;
            settings.RevealDisappearanceDuration = 0.35f;
            settings.SkipHideMode = TextStudioTypewriterSkipHideMode.FinishHideEffect;
            return settings;
        }

        static TextStudioTypewriterSettings CreateScatterVanishSettings()
        {
            TextStudioTypewriterSettings settings = TextStudioBuiltInTypewriterPreset.CreateSettings(
                RevealMode.ByGlyph,
                BuiltInTimings.Expressive.Id,
                ScatterReveal,
                ScatterHide);
            settings.HideSequence = TextStudioSequenceSettings.Default;
            settings.HideSequence.Order = TextStudioSequenceOrder.SeededRandom;
            settings.RevealDisappearanceDuration = 0.45f;
            settings.SkipHideMode = TextStudioTypewriterSkipHideMode.FinishHideEffect;
            return settings;
        }

        static readonly TextStudioBuiltInTypewriterPreset[] s_all =
        {
            ClassicTypewriter,
            SmoothDialogue,
            FastUi,
            ToastFlash,
            ReactiveCounter,
            WordFade,
            LetterFade,
            WordPop,
            CinematicLine,
            TerminalStaccato,
            LoopingSignal,
            ReverseCaption,
            ScatterVanish,
            LowMotion,
            InstantAccessible
        };

        public static IReadOnlyList<TextStudioBuiltInTypewriterPreset> All()
            => s_all;

        public static TextStudioBuiltInTypewriterPreset Default => ClassicTypewriter;

        public static TextStudioBuiltInTypewriterPreset? ForId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < s_all.Length; i++)
            {
                TextStudioBuiltInTypewriterPreset preset = s_all[i];
                if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
                    return preset;
            }

            return null;
        }
    }
}

namespace TextStudio
{
    internal static class TextStudioTransitionStyleUtility
    {
        public static bool TrySplitInvocations(string style, List<string> invocations, out string error)
        {
            if (invocations == null)
            {
                error = "Invocation target list is required.";
                return false;
            }

            return TryReadInvocations(style, invocations, null, out error);
        }

        public static bool TryParseInvocations(
            string style,
            List<TextStudioEffectInvocation> invocations,
            out string error)
        {
            if (invocations == null)
            {
                error = "Invocation target list is required.";
                return false;
            }

            return TryReadInvocations(style, null, invocations, out error);
        }

        static bool TryReadInvocations(
            string style,
            List<string> invocationSources,
            List<TextStudioEffectInvocation> parsedInvocations,
            out string error)
        {
            error = string.Empty;
            invocationSources?.Clear();
            parsedInvocations?.Clear();

            string source = style != null ? style.Trim() : string.Empty;
            if (source.Length == 0)
                return true;

            int index = 0;
            while (index < source.Length)
            {
                while (index < source.Length && char.IsWhiteSpace(source[index]))
                    index++;

                if (index >= source.Length)
                    break;

                if (source[index] != '<')
                {
                    string remainder = source.Substring(index).Trim();
                    if (remainder.IndexOf('<') >= 0 || remainder.IndexOf('>') >= 0)
                    {
                        error = "Transition style contains mixed bare text and tag invocations.";
                        invocationSources?.Clear();
                        parsedInvocations?.Clear();
                        return false;
                    }

                    if (!TextStudioEffectInvocationParser.TryParse(
                            remainder,
                            out TextStudioEffectInvocation parsed,
                            out error))
                    {
                        invocationSources?.Clear();
                        parsedInvocations?.Clear();
                        return false;
                    }

                    invocationSources?.Add(remainder);
                    parsedInvocations?.Add(parsed);
                    return true;
                }

                int close = source.IndexOf('>', index);
                if (close < 0)
                {
                    error = "Transition style invocation is missing its closing '>'.";
                    invocationSources?.Clear();
                    parsedInvocations?.Clear();
                    return false;
                }

                string invocation = source.Substring(index, close - index + 1);
                if (!TextStudioEffectInvocationParser.TryParse(
                        invocation,
                        out TextStudioEffectInvocation parsedInvocation,
                        out error))
                {
                    invocationSources?.Clear();
                    parsedInvocations?.Clear();
                    return false;
                }

                invocationSources?.Add(invocation);
                parsedInvocations?.Add(parsedInvocation);
                index = close + 1;
            }

            return true;
        }
    }
}
