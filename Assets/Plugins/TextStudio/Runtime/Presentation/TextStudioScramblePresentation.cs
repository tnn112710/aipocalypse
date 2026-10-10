using System;
using System.Globalization;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    internal sealed class TextStudioScramblePresentation : ITextStudioCharacterSubstitutionRuntime
    {
        const string k_Ascii = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const string k_Binary = "01";
        const string k_Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string k_Lower = "abcdefghijklmnopqrstuvwxyz";
        const string k_Digits = "0123456789";
        const string k_Hex = "0123456789ABCDEF";
        const string k_Symbols = "!@#$%&*+-=?";
        const float k_MinScrambleRate = 0.01f;
        const int k_NarrowWidthClass = -1;
        const int k_DefaultWidthClass = 0;
        const int k_WideWidthClass = 1;

        bool _hasSubstitutions;
        float[] _glyphStartTimes;
        bool[] _glyphStarted;

        // Code points of the last resolved charset, grouped by approximate width class in charset order.
        string _charset;
        int[] _charsetCodePoints = Array.Empty<int>();
        int[] _widthCandidates = Array.Empty<int>();
        int _charsetCount;
        int _narrowCandidateCount;
        int _defaultCandidateCount;
        int _wideCandidateCount;
        int _distinctCharsetCount;

        enum ScrambleWidthMode : byte
        {
            Matched,
            Any
        }

        public bool HasActiveSubstitutions => _hasSubstitutions;

        public void OnTextChanged(TextStudioPlanView plan)
        {
            _hasSubstitutions = plan.IsCreated && plan.CharacterSubstitutionBindingCount > 0;
            int glyphCount = plan.VisibleGlyphCount;
            if (glyphCount > 0)
            {
                EnsureGlyphTimingCapacity(glyphCount);
                Array.Clear(_glyphStartTimes, 0, glyphCount);
                Array.Clear(_glyphStarted, 0, glyphCount);
            }
        }

        internal void OnTextChanged(TextStudioCompiledPlan plan)
        {
            OnTextChanged(new TextStudioPlanView(plan));
        }

        public TextStudioCharacterSubstitutionResult Update(in TextStudioCharacterSubstitutionContext context)
        {
            if (!context.Plan.IsCreated ||
                !context.Glyphs.IsCreated ||
                context.GlyphCount <= 0 ||
                string.IsNullOrEmpty(context.Plan.ResolvedText))
            {
                _hasSubstitutions = false;
                return default;
            }

            EnsureGlyphTimingCapacity(context.GlyphCount);

            bool anyActive = false;
            bool changed = ApplyScrambleBinding(
                context.Plan.CompiledPlan,
                in context.Binding,
                context.Glyphs.Items,
                context.GlyphCount,
                context.Time,
                context.IsDiscontinuous,
                context.Reveal,
                ref anyActive);

            _hasSubstitutions = anyActive;
            return new TextStudioCharacterSubstitutionResult(changed, anyActive);
        }

        bool ApplyScrambleBinding(
            TextStudioCompiledPlan plan,
            in CharacterSubstitutionBinding binding,
            PresentationGlyph[] glyphs,
            int glyphCount,
            float time,
            bool isDiscontinuous,
            ITextStudioRevealState reveal,
            ref bool anyActive)
        {
            float scrambleDuration = GetFloat(in binding.parameters, "duration", 0, 0.5f);
            CharacterSubstitutionPlayback playback = BuiltInCharacterSubstitutions.ResolveScramblePlayback(in binding.parameters);
            float revealDuration = playback == CharacterSubstitutionPlayback.Once
                ? GetFloat(
                    in binding.parameters,
                    "revealDuration",
                    -1,
                    BuiltInCharacterSubstitutions.DefaultScrambleRevealDuration)
                : 0f;

            PrepareCharset(ResolveCharset(GetString(in binding.parameters, "charset", 1, "ascii")));
            int seed = GetInt(in binding.parameters, "seed", 0);
            float rate = ResolveRate(in binding.parameters);
            ScrambleWidthMode widthMode = ResolveWidthMode(GetString(in binding.parameters, "widthMode", -1, "matched"));
            bool preserveWhitespace = GetBool(in binding.parameters, "preserveWhitespace", true);
            bool preservePunctuation = GetBool(in binding.parameters, "preservePunctuation", false);

            bool changed = false;
            int start = Mathf.Max(0, binding.glyphStart);
            int end = Mathf.Min(binding.glyphStart + binding.glyphCount, Mathf.Min(glyphCount, plan.VisibleGlyphCount));
            int revealCandidateCount = playback == CharacterSubstitutionPlayback.Once
                ? CountRevealCandidates(plan, start, end, preserveWhitespace, preservePunctuation)
                : 0;
            int revealCandidateIndex = 0;
            for (int glyphIndex = start; glyphIndex < end; glyphIndex++)
            {
                char source = plan.GetPrimaryChar(glyphIndex);
                bool canSubstitute = ShouldSubstitute(source, preserveWhitespace, preservePunctuation);
                int candidateIndex = canSubstitute ? revealCandidateIndex++ : -1;

                if (reveal != null && !reveal.IsGlyphRevealed(glyphIndex))
                {
                    changed |= ResetGlyphToSource(plan, glyphs, glyphIndex);
                    if (_glyphStarted != null && glyphIndex < _glyphStarted.Length)
                    {
                        _glyphStarted[glyphIndex] = false;
                        _glyphStartTimes[glyphIndex] = 0f;
                    }
                    continue;
                }

                float elapsed = GetGlyphElapsed(
                    glyphIndex,
                    time,
                    isDiscontinuous,
                    reveal);
                float progress = playback == CharacterSubstitutionPlayback.Once
                    ? EvaluateOnceProgress(elapsed, scrambleDuration, revealDuration)
                    : EvaluateProgress(elapsed, scrambleDuration, playback);
                bool resolved = playback == CharacterSubstitutionPlayback.Once &&
                                IsOnceGlyphResolved(
                                    elapsed,
                                    scrambleDuration,
                                    revealDuration,
                                    candidateIndex,
                                    revealCandidateCount,
                                    canSubstitute);
                if (canSubstitute && !resolved)
                    anyActive = true;

                int target = source;
                bool substitute = canSubstitute && !resolved;

                if (substitute)
                {
                    int bucket = Mathf.FloorToInt(elapsed * rate);
                    target = PickCodePoint(seed, glyphIndex, bucket, source, widthMode);
                }

                PresentationGlyph glyph = glyphs[glyphIndex];
                int targetGlyphId = substitute ? target : plan.GetPrimaryCodePoint(glyphIndex);
                SubstitutionState targetState = substitute ? SubstitutionState.Scrambling : SubstitutionState.Resolved;
                PresentationFlags targetFlags = substitute
                    ? glyph.flags | PresentationFlags.Substituted | PresentationFlags.Dirty
                    : (glyph.flags & ~PresentationFlags.Substituted) | PresentationFlags.Dirty;

                if (glyph.glyphId != targetGlyphId ||
                    glyph.substitutionState != targetState ||
                    !Mathf.Approximately(glyph.substitutionProgress, progress) ||
                    glyph.flags != targetFlags)
                {
                    glyph.glyphId = targetGlyphId;
                    glyph.substitutionState = targetState;
                    glyph.substitutionProgress = substitute ? progress : 1f;
                    glyph.flags = targetFlags;
                    glyphs[glyphIndex] = glyph;
                    changed = true;
                }
            }

            return changed;
        }

        static bool IsOnceGlyphResolved(
            float elapsed,
            float scrambleDuration,
            float revealDuration,
            int revealCandidateIndex,
            int revealCandidateCount,
            bool canSubstitute)
        {
            if (!canSubstitute)
                return true;

            float safeScrambleDuration = Mathf.Max(0f, scrambleDuration);
            if (elapsed < safeScrambleDuration)
                return false;

            if (revealDuration <= 0f || revealCandidateCount <= 0)
                return true;

            float revealElapsed = elapsed - safeScrambleDuration;
            if (revealElapsed >= revealDuration)
                return true;

            if (revealElapsed <= 0f || revealCandidateIndex < 0)
                return false;

            float revealProgress = Mathf.Clamp01(revealElapsed / revealDuration);
            int resolvedCount = Mathf.FloorToInt(revealProgress * revealCandidateCount);
            return revealCandidateIndex < resolvedCount;
        }

        static float EvaluateOnceProgress(float elapsed, float scrambleDuration, float revealDuration)
        {
            float totalDuration = Mathf.Max(0f, scrambleDuration) + Mathf.Max(0f, revealDuration);
            if (totalDuration <= 0f)
                return 1f;

            return Mathf.Clamp01(Mathf.Max(0f, elapsed) / totalDuration);
        }

        static float EvaluateProgress(float elapsed, float duration, CharacterSubstitutionPlayback playback)
        {
            if (duration <= 0f)
                return playback == CharacterSubstitutionPlayback.Loop ? 0f : 1f;

            float normalized = Mathf.Max(0f, elapsed) / duration;
            return playback == CharacterSubstitutionPlayback.Loop
                ? Mathf.Repeat(normalized, 1f)
                : Mathf.Clamp01(normalized);
        }

        float GetGlyphElapsed(
            int glyphIndex,
            float time,
            bool isDiscontinuous,
            ITextStudioRevealState reveal)
        {
            float sampleTime = Mathf.Max(0f, time);
            if (reveal == null ||
                _glyphStartTimes == null ||
                _glyphStarted == null ||
                glyphIndex >= _glyphStartTimes.Length)
            {
                return sampleTime;
            }

            if (isDiscontinuous)
            {
                _glyphStarted[glyphIndex] = true;
                float glyphRevealTime = Mathf.Max(0f, reveal.GetApproximateRevealTime(glyphIndex));
                float reconstructedElapsed = Mathf.Max(0f, sampleTime - glyphRevealTime);
                _glyphStartTimes[glyphIndex] = sampleTime - reconstructedElapsed;
                return reconstructedElapsed;
            }

            if (!_glyphStarted[glyphIndex])
            {
                _glyphStarted[glyphIndex] = true;
                _glyphStartTimes[glyphIndex] = sampleTime;
                return 0f;
            }

            return Mathf.Max(0f, sampleTime - _glyphStartTimes[glyphIndex]);
        }

        bool ResetGlyphToSource(TextStudioCompiledPlan plan, PresentationGlyph[] glyphs, int glyphIndex)
        {
            if (plan == null || glyphIndex < 0 || glyphIndex >= glyphs.Length || glyphIndex >= plan.VisibleGlyphCount)
                return false;

            PresentationGlyph glyph = glyphs[glyphIndex];
            int sourceGlyphId = plan.GetPrimaryCodePoint(glyphIndex);
            PresentationFlags resolvedFlags = glyph.flags & ~PresentationFlags.Substituted;
            if (glyph.glyphId == sourceGlyphId &&
                glyph.substitutionState == SubstitutionState.Resolved &&
                Mathf.Approximately(glyph.substitutionProgress, 1f) &&
                glyph.flags == resolvedFlags)
            {
                return false;
            }

            glyph.glyphId = sourceGlyphId;
            glyph.substitutionState = SubstitutionState.Resolved;
            glyph.substitutionProgress = 1f;
            glyph.flags = resolvedFlags | PresentationFlags.Dirty;
            glyphs[glyphIndex] = glyph;
            return true;
        }

        void EnsureGlyphTimingCapacity(int glyphCount)
        {
            if (glyphCount <= 0)
                return;

            if (_glyphStartTimes == null || _glyphStartTimes.Length < glyphCount)
            {
                int capacity = TextStudioBufferCapacity.ForCount(glyphCount);
                _glyphStartTimes = new float[capacity];
                _glyphStarted = new bool[capacity];
            }
        }

        // Picks by code point. For charsets without supplementary characters every index equals the historical
        // UTF-16 index, so output is unchanged; supplementary characters are picked whole, never as halves.
        // TMP output shows a supplementary pick when the glyph's font asset contains it; the UI Toolkit
        // visible-text rebuild cannot hold one in a single UTF-16 slot and keeps the source character instead.
        int PickCodePoint(
            int seed,
            int glyphIndex,
            int bucket,
            char source,
            ScrambleWidthMode widthMode)
        {
            int count = _charsetCount;
            if (count <= 0)
                return '?';

            uint hash = ComputeCharacterHash(seed, glyphIndex, bucket);
            if (widthMode == ScrambleWidthMode.Any)
                return _charsetCodePoints[(int)(hash % (uint)count)];

            int widthClass = GetApproximateWidthClass(source);
            int start = widthClass == k_NarrowWidthClass
                ? 0
                : widthClass == k_WideWidthClass
                    ? _narrowCandidateCount + _defaultCandidateCount
                    : _narrowCandidateCount;
            int matchingCount = widthClass == k_NarrowWidthClass
                ? _narrowCandidateCount
                : widthClass == k_WideWidthClass
                    ? _wideCandidateCount
                    : _defaultCandidateCount;
            if (matchingCount <= 0)
                return _charsetCodePoints[(int)(hash % (uint)count)];

            return _widthCandidates[start + (int)(hash % (uint)matchingCount)];
        }

        // Parses a charset once per distinct value; unpaired surrogates are dropped so no lone half is shown.
        void PrepareCharset(string charset)
        {
            if (ReferenceEquals(charset, _charset))
                return;
            if (_charset != null && string.Equals(charset, _charset, StringComparison.Ordinal))
            {
                _charset = charset;
                return;
            }

            _charset = charset;
            int length = charset != null ? charset.Length : 0;
            if (_charsetCodePoints.Length < length)
            {
                _charsetCodePoints = new int[length];
                _widthCandidates = new int[length];
            }

            int count = 0;
            for (int i = 0; i < length; i++)
            {
                char character = charset[i];
                if (char.IsHighSurrogate(character) && i + 1 < length && char.IsLowSurrogate(charset[i + 1]))
                {
                    _charsetCodePoints[count++] = char.ConvertToUtf32(character, charset[i + 1]);
                    i++;
                }
                else if (!char.IsSurrogate(character))
                {
                    _charsetCodePoints[count++] = character;
                }
            }
            _charsetCount = count;

            _narrowCandidateCount = 0;
            _defaultCandidateCount = 0;
            _wideCandidateCount = 0;
            for (int i = 0; i < count; i++)
            {
                int widthClass = GetApproximateWidthClass(_charsetCodePoints[i]);
                if (widthClass == k_NarrowWidthClass)
                    _narrowCandidateCount++;
                else if (widthClass == k_WideWidthClass)
                    _wideCandidateCount++;
                else
                    _defaultCandidateCount++;
            }

            int narrow = 0;
            int standard = _narrowCandidateCount;
            int wide = _narrowCandidateCount + _defaultCandidateCount;
            for (int i = 0; i < count; i++)
            {
                int codePoint = _charsetCodePoints[i];
                int widthClass = GetApproximateWidthClass(codePoint);
                if (widthClass == k_NarrowWidthClass)
                    _widthCandidates[narrow++] = codePoint;
                else if (widthClass == k_WideWidthClass)
                    _widthCandidates[wide++] = codePoint;
                else
                    _widthCandidates[standard++] = codePoint;
            }

            _distinctCharsetCount = CountDistinctCodePoints(_charsetCodePoints, count);
        }

        static uint ComputeCharacterHash(int seed, int glyphIndex, int bucket)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, (uint)seed);
            hash = Mix(hash, (uint)glyphIndex);
            hash = Mix(hash, (uint)bucket);
            return hash;
        }

        static int GetApproximateWidthClass(int value)
        {
            switch (value)
            {
                case 'i':
                case 'I':
                case 'l':
                case '1':
                case '!':
                case '|':
                case '.':
                case ',':
                case ':':
                case ';':
                case '\'':
                    return k_NarrowWidthClass;
                case 'm':
                case 'M':
                case 'w':
                case 'W':
                case 'O':
                case 'Q':
                case '0':
                case '@':
                case '#':
                case '%':
                case '&':
                    return k_WideWidthClass;
                default:
                    return k_DefaultWidthClass;
            }
        }

        float ResolveRate(in TextStudioTagParameters parameters)
        {
            if (TryGetFloat(in parameters, "rate", -1, out float explicitRate))
                return Mathf.Max(k_MinScrambleRate, explicitRate);

            return GetDefaultRateForDistinctCount(_distinctCharsetCount);
        }

        static float GetDefaultRateForDistinctCount(int count)
        {
            if (count <= 2)
                return 6f;
            if (count <= 10)
                return 8f;
            if (count <= 16)
                return 10f;
            if (count <= 26)
                return 11f;

            return 12f;
        }

        static int CountDistinctCodePoints(int[] codePoints, int length)
        {
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                int candidate = codePoints[i];
                bool seen = false;
                for (int j = 0; j < i; j++)
                {
                    if (codePoints[j] != candidate)
                        continue;

                    seen = true;
                    break;
                }

                if (!seen)
                {
                    count++;
                    if (count > 26)
                        return count;
                }
            }

            return count;
        }

        static int CountRevealCandidates(
            TextStudioCompiledPlan plan,
            int start,
            int end,
            bool preserveWhitespace,
            bool preservePunctuation)
        {
            if (plan == null)
                return 0;

            int count = 0;
            for (int glyphIndex = start; glyphIndex < end; glyphIndex++)
            {
                if (ShouldSubstitute(plan.GetPrimaryChar(glyphIndex), preserveWhitespace, preservePunctuation))
                    count++;
            }

            return count;
        }

        static bool ShouldSubstitute(char source, bool preserveWhitespace, bool preservePunctuation)
        {
            return (!preserveWhitespace || !char.IsWhiteSpace(source)) &&
                   (!preservePunctuation || !char.IsPunctuation(source));
        }

        static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash * 16777619u;
        }

        static string ResolveCharset(string value)
        {
            if (string.IsNullOrEmpty(value))
                return k_Ascii;

            if (string.Equals(value, "ascii", StringComparison.OrdinalIgnoreCase))
                return k_Ascii;
            if (string.Equals(value, "binary", StringComparison.OrdinalIgnoreCase))
                return k_Binary;
            if (string.Equals(value, "letters", StringComparison.OrdinalIgnoreCase))
                return k_Letters;
            if (string.Equals(value, "lower", StringComparison.OrdinalIgnoreCase))
                return k_Lower;
            if (string.Equals(value, "digits", StringComparison.OrdinalIgnoreCase))
                return k_Digits;
            if (string.Equals(value, "hex", StringComparison.OrdinalIgnoreCase))
                return k_Hex;
            if (string.Equals(value, "symbols", StringComparison.OrdinalIgnoreCase))
                return k_Symbols;

            return value;
        }

        static ScrambleWidthMode ResolveWidthMode(string value)
        {
            if (string.Equals(value, "any", StringComparison.OrdinalIgnoreCase))
                return ScrambleWidthMode.Any;

            return ScrambleWidthMode.Matched;
        }

        static string GetString(
            in TextStudioTagParameters parameters,
            string name,
            int positionalIndex,
            string defaultValue)
        {
            if (!string.IsNullOrEmpty(name) && parameters.TryGetString(name, out string named))
                return named;

            if (positionalIndex >= 0 && parameters.PositionalCount > positionalIndex)
                return parameters.GetString(positionalIndex, defaultValue);

            return defaultValue;
        }

        static float GetFloat(
            in TextStudioTagParameters parameters,
            string name,
            int positionalIndex,
            float defaultValue)
        {
            string raw = GetString(in parameters, name, positionalIndex, string.Empty);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;

            return defaultValue;
        }

        static bool TryGetFloat(
            in TextStudioTagParameters parameters,
            string name,
            int positionalIndex,
            out float value)
        {
            string raw = GetString(in parameters, name, positionalIndex, string.Empty);
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static int GetInt(in TextStudioTagParameters parameters, string name, int defaultValue)
        {
            if (parameters.TryGetString(name, out string raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;

            return defaultValue;
        }

        static bool GetBool(in TextStudioTagParameters parameters, string name, bool defaultValue)
        {
            if (parameters.TryGetBool(name, out bool value))
                return value;

            return defaultValue;
        }
    }
}
