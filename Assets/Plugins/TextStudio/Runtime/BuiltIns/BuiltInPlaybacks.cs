using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Code-defined playback. Stateless, zero-alloc.
    /// </summary>
    public sealed class CodePlayback : IEffectPlayback, ITextStudioBuiltIn
    {
        readonly System.Func<float, float, float> _fn;

        public string Id { get; }
        public string Category => "Playbacks";
        public string DisplayName { get; }
        public string Description { get; }
        public PlaybackMode Mode { get; }

        public CodePlayback(string id, string displayName, string description, PlaybackMode mode, System.Func<float, float, float> fn)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Mode = mode;
            _fn = fn;
        }

        public float Evaluate(float time, float duration) => _fn(time, duration);
    }

    /// <summary>
    /// Built-in playback library.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Playback definitions and their read-only keyword views are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInPlaybacks
    {
        const string OnceKeyword = "once";
        const string LoopKeyword = "loop";
        const string InfiniteKeyword = "infinite";
        const string ExternalKeyword = "external";
        const string ShortKeyword = "short";

        static readonly string[] s_textAnimatorPlaybackKeywords =
        {
            OnceKeyword,
            LoopKeyword,
            InfiniteKeyword,
            ExternalKeyword,
            ShortKeyword
        };

        static readonly ReadOnlyCollection<string> s_readOnlyTextAnimatorPlaybackKeywords =
            Array.AsReadOnly(s_textAnimatorPlaybackKeywords);

        public static IReadOnlyList<string> TextAnimatorPlaybackKeywords =>
            s_readOnlyTextAnimatorPlaybackKeywords;

        internal static string[] CopyTextAnimatorPlaybackKeywords()
        {
            return (string[])s_textAnimatorPlaybackKeywords.Clone();
        }

        public static readonly CodePlayback Once = new CodePlayback(
            "playback.once", "Once", "Plays once across duration, then holds at 1.",
            PlaybackMode.Once,
            (t, d) => d > 0f ? Mathf.Clamp01(t / d) : 1f);

        public static readonly CodePlayback Loop = new CodePlayback(
            "playback.loop", "Loop", "Repeats [0,1] every 'duration' seconds.",
            PlaybackMode.Loop,
            (t, d) => d > 0f ? Mathf.Repeat(t / d, 1f) : 0f);

        public static readonly CodePlayback PingPong = new CodePlayback(
            "playback.pingPong", "Ping-Pong", "Oscillates 0->1->0.",
            PlaybackMode.PingPong,
            (t, d) => d > 0f ? Mathf.PingPong(t / d, 1f) : 0f);

        public static readonly CodePlayback Cycle = new CodePlayback(
            "playback.cycle", "Cycle", "Normalized time over period=1.",
            PlaybackMode.Loop,
            (t, _) => Mathf.Repeat(t, 1f));

        public static readonly CodePlayback Weighted = new CodePlayback(
            "playback.weighted", "Weighted", "Linear ramp clamped across duration.",
            PlaybackMode.Weighted,
            (t, d) => d > 0f ? Mathf.Clamp01(t / d) : 1f);

        public static readonly CodePlayback Appearance = new CodePlayback(
            "playback.appearance", "Appearance", "One-shot from 0 to 1 over duration, clamped.",
            PlaybackMode.Once,
            (t, d) => d > 0f ? Mathf.Clamp01(t / d) : 1f);

        public static readonly CodePlayback External = new CodePlayback(
            "playback.external", "External", "Driven entirely by external time (e.g. Timeline).",
            PlaybackMode.External,
            (t, _) => Mathf.Clamp01(t));

        public static readonly CodePlayback Default = Once;

        public static System.Collections.Generic.IEnumerable<CodePlayback> All()
        {
            yield return Once;
            yield return Loop;
            yield return PingPong;
            yield return Cycle;
            yield return Weighted;
            yield return Appearance;
            yield return External;
        }

        public static bool TryResolveTextAnimatorKeyword(string keyword, out string playback)
        {
            playback = string.Empty;
            if (string.IsNullOrWhiteSpace(keyword))
                return false;

            string normalized = keyword.Trim();
            if (string.Equals(normalized, OnceKeyword, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, ShortKeyword, StringComparison.OrdinalIgnoreCase))
            {
                playback = OnceKeyword;
                return true;
            }

            if (string.Equals(normalized, LoopKeyword, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, InfiniteKeyword, StringComparison.OrdinalIgnoreCase))
            {
                playback = string.Equals(normalized, InfiniteKeyword, StringComparison.OrdinalIgnoreCase)
                    ? InfiniteKeyword
                    : LoopKeyword;
                return true;
            }

            if (string.Equals(normalized, ExternalKeyword, StringComparison.OrdinalIgnoreCase))
            {
                playback = ExternalKeyword;
                return true;
            }

            if (TextStudioRegistry.Playbacks.TryGetQuiet(normalized, out IEffectPlayback registeredPlayback) &&
                registeredPlayback != null)
            {
                playback = normalized;
                return true;
            }

            return false;
        }

        public static TextStudioTagParameters CanonicalizeTextAnimatorKeywordParameters(
            in TextStudioTagParameters parameters)
        {
            if (parameters.PositionalCount == 0 && parameters.NamedCount == 0)
                return parameters;

            if (parameters.TryGetString("playback", out _))
                return parameters;

            string playback = string.Empty;
            List<string> positional = null;
            if (parameters.PositionalCount > 0)
            {
                positional = new List<string>(parameters.PositionalCount);
                for (int i = 0; i < parameters.PositionalCount; i++)
                {
                    string value = parameters.GetString(i);
                    if (TryResolveTextAnimatorKeyword(value, out string resolvedPlayback))
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
                    TextStudioNamedTagParameter parameter = parameters.Named[i];
                    if (parameter.IsFlag &&
                        TryResolveTextAnimatorKeyword(parameter.Name, out string resolvedPlayback))
                    {
                        if (string.IsNullOrEmpty(playback))
                            playback = resolvedPlayback;
                        continue;
                    }

                    named.Add(parameter);
                }
            }

            if (string.IsNullOrEmpty(playback))
                return parameters;

            if (named == null)
                named = new List<TextStudioNamedTagParameter>(1);
            named.Add(new TextStudioNamedTagParameter("playback", playback, false));

            return new TextStudioTagParameters(
                parameters.Raw,
                positional != null ? positional.ToArray() : Array.Empty<string>(),
                named.ToArray());
        }
    }
}
