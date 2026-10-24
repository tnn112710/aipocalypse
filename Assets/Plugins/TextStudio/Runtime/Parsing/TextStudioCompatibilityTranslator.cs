using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TextStudio.BuiltIns;

namespace TextStudio
{
    internal enum TextStudioCompatibilityDiagnosticSeverity : byte
    {
        Information,
        Warning
    }

    internal readonly struct TextStudioCompatibilityDiagnostic
    {
        internal readonly TextStudioCompatibilityDiagnosticSeverity Severity;
        internal readonly int SourceStart;
        internal readonly int SourceLength;
        internal readonly string Message;

        internal TextStudioCompatibilityDiagnostic(
            TextStudioCompatibilityDiagnosticSeverity severity,
            int sourceStart,
            int sourceLength,
            string message)
        {
            Severity = severity;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// Dependency-free compatibility front end. It translates supported provider syntax into
    /// Text Studio's canonical markup before the normal tokenizer and resolver run.
    /// </summary>
    internal sealed class TextStudioCompatibilityTranslator
    {
        enum VendorRangeKind : byte
        {
            Persistent,
            Show,
            Hide,
            Command,
            Passthrough
        }

        readonly struct VendorRange
        {
            internal readonly string VendorName;
            internal readonly string ClosingMarkup;
            internal readonly VendorRangeKind Kind;

            internal VendorRange(string vendorName, string closingMarkup, VendorRangeKind kind)
            {
                VendorName = vendorName;
                ClosingMarkup = closingMarkup;
                Kind = kind;
            }
        }

        readonly StringBuilder _textAnimatorBuilder = new StringBuilder(128);
        readonly StringBuilder _tmpeffectsBuilder = new StringBuilder(128);
        readonly StringBuilder _parameterBuilder = new StringBuilder(64);
        readonly List<VendorRange> _vendorRanges = new List<VendorRange>(8);
        readonly List<TextStudioCompatibilityDiagnostic> _diagnostics =
            new List<TextStudioCompatibilityDiagnostic>(4);

        internal IReadOnlyList<TextStudioCompatibilityDiagnostic> Diagnostics => _diagnostics;
        internal bool TMPEffectsDetected { get; private set; }
        internal int TranslatedTagCount { get; private set; }

        // Native and Compatibility text that translates to itself records the profile, its length and the position from which
        // text appended to it must be scanned again, so an append scans only its own tags (TryTranslateAppendedIdentically).
        TextStudioAuthoringProfile _identityProfile;
        int _identityLength = -1;
        int _identityResume;

        internal string Translate(string source, TextStudioAuthoringProfile profile)
        {
            source ??= string.Empty;
            _diagnostics.Clear();
            _vendorRanges.Clear();
            TMPEffectsDetected = false;
            TranslatedTagCount = 0;
            _identityLength = -1;

            switch (profile)
            {
                case TextStudioAuthoringProfile.Native:
                    return EscapeTMPEffectsPrefixedTags(source);

                case TextStudioAuthoringProfile.Custom:
                    return source;

                case TextStudioAuthoringProfile.TextAnimator:
                    return TranslateTextAnimatorPhaseTags(EscapeNonTmpAngleTags(source));

                case TextStudioAuthoringProfile.TMPEffects:
                    TMPEffectsDetected = true;
                    return TranslateTMPEffects(source, false);

                case TextStudioAuthoringProfile.Compatibility:
                default:
                    string textAnimatorText = TranslateTextAnimatorPhaseTags(source);
                    int resume = 0;
                    TMPEffectsDetected = !string.IsNullOrEmpty(textAnimatorText) &&
                                         textAnimatorText.IndexOf('<') >= 0 &&
                                         HasTMPEffectsEvidence(textAnimatorText, 0, out resume);
                    if (!TMPEffectsDetected &&
                        profile == TextStudioAuthoringProfile.Compatibility &&
                        textAnimatorText.IndexOf('{') < 0)
                    {
                        RecordIdentity(profile, textAnimatorText.Length, textAnimatorText.IndexOf('<') >= 0 ? resume : textAnimatorText.Length);
                    }
                    return TMPEffectsDetected
                        ? TranslateTMPEffects(textAnimatorText, true)
                        : textAnimatorText;
            }
        }

        /// <summary>
        /// Translate for <paramref name="source"/>, which appends to the text of the last translation from
        /// <paramref name="appendStart"/> on, when both translate to themselves: it then scans only from the last resume
        /// position and leaves the translator as Translate would. Returns false, changing nothing, otherwise.
        /// </summary>
        internal bool TryTranslateAppendedIdentically(string source, int appendStart, TextStudioAuthoringProfile profile)
        {
            if (_identityLength < 0 || _identityLength != appendStart || _identityProfile != profile ||
                source == null || source.Length < appendStart)
            {
                return false;
            }

            int resume;
            switch (profile)
            {
                case TextStudioAuthoringProfile.Native:
                    if (!ScanEscapeIdentity(source, _identityResume, out resume))
                        return false;
                    break;
                case TextStudioAuthoringProfile.Compatibility:
                    if (source.IndexOf('{', appendStart) >= 0 ||
                        HasTMPEffectsEvidence(source, _identityResume, out resume))
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }

            _diagnostics.Clear();
            _vendorRanges.Clear();
            TMPEffectsDetected = false;
            TranslatedTagCount = 0;
            RecordIdentity(profile, source.Length, resume);
            return true;
        }

        void RecordIdentity(TextStudioAuthoringProfile profile, int length, int resume)
        {
            _identityProfile = profile;
            _identityLength = length;
            _identityResume = resume;
        }

        static bool TryGetLiteralMarkupEnd(string source, int open, out int end)
        {
            return TryGetLiteralMarkupEnd(source, open, out end, out _);
        }

        // unterminated reports a literal that runs to the end of the text, which appended text could still terminate.
        static bool TryGetLiteralMarkupEnd(string source, int open, out int end, out bool unterminated)
        {
            end = 0;
            unterminated = false;
            if (TextStudioTagExpansionUtility.IsEscaped(source, open))
            {
                int close = source.IndexOf('>', open + 1);
                unterminated = close < 0;
                end = close >= 0 ? close + 1 : source.Length;
                return true;
            }
            if (TextStudioTagExpansionUtility.IsCommentStart(source, open))
            {
                int close = source.IndexOf("-->", open + 4, StringComparison.Ordinal);
                unterminated = close < 0;
                end = close >= 0 ? close + 3 : source.Length;
                return true;
            }
            int nameStart = open + 1;
            while (nameStart < source.Length && char.IsWhiteSpace(source[nameStart])) nameStart++;
            if (nameStart + 7 > source.Length || !source.AsSpan(nameStart, 7).Equals("noparse".AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
            int nameEnd = nameStart + 7;
            while (nameEnd < source.Length && char.IsWhiteSpace(source[nameEnd])) nameEnd++;
            if (nameEnd >= source.Length || source[nameEnd] != '>') return false;
            int closing = source.IndexOf("</noparse>", nameEnd + 1, StringComparison.OrdinalIgnoreCase);
            unterminated = closing < 0;
            end = closing >= 0 ? closing + 10 : source.Length;
            return true;
        }

        internal static bool HasTMPEffectsEvidence(string source)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf('<') < 0)
                return false;

            return HasTMPEffectsEvidence(source, 0, out _);
        }

        // Scans from start on. Without evidence, resume is the position text appended to source must be scanned again from:
        // where a scan read to the end of the text without finding a '>' or a literal's end, otherwise the end. A tag that ends
        // before that point, and the characters a check could still need past the end, cannot change by appending: a missing
        // '>' already stops the scan there.
        static bool HasTMPEffectsEvidence(string source, int scanStart, out int resume)
        {
            resume = source.Length;
            int cursor = scanStart;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('<', cursor);
                if (open < 0)
                    return false;

                if (TryGetLiteralMarkupEnd(source, open, out int literalEnd, out bool unterminated))
                {
                    if (unterminated)
                    {
                        resume = open;
                        return false;
                    }
                    cursor = literalEnd;
                    continue;
                }

                int close = source.IndexOf('>', open + 1);
                if (close < 0)
                {
                    resume = open;
                    return false;
                }

                int start = open + 1;
                while (start < close && char.IsWhiteSpace(source[start]))
                    start++;
                if (start >= close)
                {
                    cursor = close + 1;
                    continue;
                }

                char prefix = source[start];
                if (prefix == '+' || prefix == '-' || prefix == '!' || prefix == '?')
                    return true;

                if (prefix == '/')
                {
                    cursor = close + 1;
                    continue;
                }

                int nameEnd = ScanNameEnd(source, start, close);
                string name = source.Substring(start, nameEnd - start);
                if (IsTMPEffectsExclusiveName(name) ||
                    HasTMPEffectsSpecificParameter(source, nameEnd, close))
                {
                    return true;
                }

                cursor = close + 1;
            }

            return false;
        }

        string TranslateTextAnimatorPhaseTags(string source)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf('{') < 0)
                return source ?? string.Empty;

            _textAnimatorBuilder.Clear();
            int cursor = 0;
            bool changed = false;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('{', cursor);
                if (open < 0)
                {
                    _textAnimatorBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                int protectedEnd = -1;
                int protectedStart = source.IndexOf('<', cursor);
                while (protectedStart >= 0 && protectedStart < open)
                {
                    if (TryGetLiteralMarkupEnd(source, protectedStart, out int literalEnd) && literalEnd > open)
                    {
                        protectedEnd = literalEnd;
                        break;
                    }
                    protectedStart = source.IndexOf('<', protectedStart + 1);
                }
                if (protectedEnd >= 0)
                {
                    _textAnimatorBuilder.Append(source, cursor, protectedEnd - cursor);
                    cursor = protectedEnd;
                    continue;
                }

                int close = source.IndexOf('}', open + 1);
                if (close < 0)
                {
                    _textAnimatorBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (!TryTranslateTextAnimatorBraceTag(source, open + 1, close, out string replacement))
                {
                    _textAnimatorBuilder.Append(source, cursor, close + 1 - cursor);
                    cursor = close + 1;
                    continue;
                }

                _textAnimatorBuilder.Append(source, cursor, open - cursor);
                _textAnimatorBuilder.Append(replacement);
                cursor = close + 1;
                changed = true;
                TranslatedTagCount++;
            }

            return changed ? _textAnimatorBuilder.ToString() : source;
        }

        string TranslateTMPEffects(string source, bool allowTextStudioTags)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf('<') < 0)
                return source ?? string.Empty;

            _tmpeffectsBuilder.Clear();
            int cursor = 0;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('<', cursor);
                if (open < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (TryGetLiteralMarkupEnd(source, open, out int literalEnd))
                {
                    _tmpeffectsBuilder.Append(source, cursor, literalEnd - cursor);
                    cursor = literalEnd;
                    continue;
                }

                int close = source.IndexOf('>', open + 1);
                if (close < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                _tmpeffectsBuilder.Append(source, cursor, open - cursor);
                if (!TryTranslateTMPEffectsTag(source, open, close, allowTextStudioTags))
                    AppendLiteralTag(source, open, close);
                cursor = close + 1;
            }

            return _tmpeffectsBuilder.ToString();
        }

        bool TryTranslateTMPEffectsTag(
            string source,
            int open,
            int close,
            bool allowTextStudioTags)
        {
            int start = open + 1;
            int end = close;
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            while (end > start && char.IsWhiteSpace(source[end - 1]))
                end--;
            if (start >= end)
                return false;

            if (IsTmpRichTextTag(source, start, end))
            {
                TrackPassthroughRange(source, start, end);
                _tmpeffectsBuilder.Append(source, open, close + 1 - open);
                return true;
            }

            if (source[start] == '/')
                return TryTranslateClosingTag(source, start + 1, end, open, close, allowTextStudioTags);

            char prefix = source[start];
            if (prefix == '!')
                return TryTranslateWriterCommand(source, start + 1, end, open, close);
            if (prefix == '?')
                return TryTranslateEvent(source, start + 1, end, open, close);
            if (prefix == '+' || prefix == '-')
                return TryTranslatePhaseAnimation(source, start + 1, end, open, close, prefix);

            int nameEnd = ScanNameEnd(source, start, end);
            string vendorName = source.Substring(start, nameEnd - start);
            if (TryMapPersistent(vendorName, out string effectMarkup))
            {
                string parameters = NormalizeEffectParameters(source, nameEnd, end);
                AppendOpenRange(
                    vendorName,
                    effectMarkup,
                    parameters,
                    "</" + GetCanonicalRangeName(effectMarkup) + ">",
                    VendorRangeKind.Persistent);
                AddParameterApproximation(source, nameEnd, end, open, close);
                return true;
            }

            if (allowTextStudioTags && IsKnownTextStudioTag(vendorName))
            {
                TrackPassthroughRange(source, open + 1, end);
                _tmpeffectsBuilder.Append(source, open, close + 1 - open);
                return true;
            }

            AddDiagnostic(
                TextStudioCompatibilityDiagnosticSeverity.Warning,
                open,
                close + 1 - open,
                "Unsupported TMPEffects tag '" + vendorName + "' was kept as literal text.");
            return false;
        }

        void TrackPassthroughRange(string source, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(source[start])) start++;
            bool closing = start < end && source[start] == '/';
            if (closing) start++;
            while (start < end && char.IsWhiteSpace(source[start])) start++;
            int nameEnd = ScanNameEnd(source, start, end);
            if (nameEnd <= start) return;
            string name = TextStudioRangeLedger.CanonicalCloseName(source.Substring(start, nameEnd - start));
            if (closing)
            {
                TryPopRange(name, VendorRangeKind.Passthrough, out _);
                return;
            }
            if (source[end - 1] == '/' || BuiltInStyles.IsSelfContainedNativeRichTextFormatting(name, nameEnd < end) ||
                BuiltInActions.TryGetCanonicalActionId(name, out _) || TextStudioRegistry.Actions.TryGetQuiet(name, out _)) return;
            if (TextStudioTagUtility.TryGetExtensionTag(name, nameEnd < end, out TextStudioExtensionTagDefinition extension) &&
                extension.Shape != TextStudioExtensionTagShape.Range) return;
            _vendorRanges.Add(new VendorRange(name, "</" + name + ">", VendorRangeKind.Passthrough));
        }

        bool TryTranslateClosingTag(
            string source,
            int start,
            int end,
            int open,
            int close,
            bool allowTextStudioTags)
        {
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;

            if (start >= end)
            {
                if (TryPopLatestRange(out VendorRange anonymousRange))
                {
                    _tmpeffectsBuilder.Append(anonymousRange.ClosingMarkup);
                    TranslatedTagCount++;
                    return true;
                }

                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Warning,
                    open,
                    close + 1 - open,
                    "Anonymous TMPEffects close tag has no supported open range.");
                return false;
            }

            char prefix = source[start];
            if (prefix == '!' || prefix == '+' || prefix == '-')
            {
                VendorRangeKind kind = prefix == '!'
                    ? VendorRangeKind.Command
                    : prefix == '+' ? VendorRangeKind.Show : VendorRangeKind.Hide;
                int nameStart = start + 1;
                int nameEnd = ScanNameEnd(source, nameStart, end);
                string name = nameEnd > nameStart
                    ? source.Substring(nameStart, nameEnd - nameStart)
                    : string.Empty;
                if (TryPopRange(name, kind, out VendorRange phaseRange))
                {
                    _tmpeffectsBuilder.Append(phaseRange.ClosingMarkup);
                    TranslatedTagCount++;
                    return true;
                }

                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Warning,
                    open,
                    close + 1 - open,
                    "TMPEffects close tag has no matching supported range.");
                return false;
            }

            int regularNameEnd = ScanNameEnd(source, start, end);
            string vendorName = source.Substring(start, regularNameEnd - start);
            if (TryMapPersistent(vendorName, out string effectMarkup))
            {
                if (TryPopRange(vendorName, VendorRangeKind.Persistent, out VendorRange persistentRange))
                    _tmpeffectsBuilder.Append(persistentRange.ClosingMarkup);
                else
                    _tmpeffectsBuilder.Append("</").Append(GetCanonicalRangeName(effectMarkup)).Append('>');
                TranslatedTagCount++;
                return true;
            }

            if (allowTextStudioTags && IsKnownTextStudioTag(vendorName))
            {
                TrackPassthroughRange(source, open + 1, end);
                _tmpeffectsBuilder.Append(source, open, close + 1 - open);
                return true;
            }

            AddDiagnostic(
                TextStudioCompatibilityDiagnosticSeverity.Warning,
                open,
                close + 1 - open,
                "Unsupported TMPEffects close tag '" + vendorName + "' was kept as literal text.");
            return false;
        }

        bool TryTranslateWriterCommand(string source, int start, int end, int open, int close)
        {
            int nameEnd = ScanNameEnd(source, start, end);
            if (nameEnd <= start)
                return false;

            string name = source.Substring(start, nameEnd - start);
            string directValue = ReadDirectValue(source, nameEnd, end);
            if (string.Equals(name, "wait", StringComparison.OrdinalIgnoreCase))
            {
                _tmpeffectsBuilder.Append("<wait");
                if (!string.IsNullOrEmpty(directValue))
                    _tmpeffectsBuilder.Append('=').Append(directValue);
                _tmpeffectsBuilder.Append('>');
                TranslatedTagCount++;
                return true;
            }

            if (string.Equals(name, "show", StringComparison.OrdinalIgnoreCase))
            {
                _tmpeffectsBuilder.Append("<instant>");
                _vendorRanges.Add(new VendorRange(name, "</instant>", VendorRangeKind.Command));
                TranslatedTagCount++;
                return true;
            }

            if (string.Equals(name, "delay", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(
                        directValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float seconds) &&
                    seconds > 0f)
                {
                    float multiplier = 0.03f / seconds;
                    _tmpeffectsBuilder.Append("<speed multiplier=")
                        .Append(multiplier.ToString("0.###", CultureInfo.InvariantCulture))
                        .Append('>');
                    AddDiagnostic(
                        TextStudioCompatibilityDiagnosticSeverity.Information,
                        open,
                        close + 1 - open,
                        "TMPEffects delay was approximated as a Text Studio reveal-speed multiplier.");
                    TranslatedTagCount++;
                    return true;
                }

                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Warning,
                    open,
                    close + 1 - open,
                    "TMPEffects delay needs a positive numeric value and was kept as literal text.");
                return false;
            }

            if (string.Equals(name, "skippable", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Information,
                    open,
                    close + 1 - open,
                    "TMPEffects skippable is already covered by the host's Text Studio skip policy.");
                TranslatedTagCount++;
                return true;
            }

            if (string.Equals(name, "debug", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Information,
                    open,
                    close + 1 - open,
                    "TMPEffects debug was consumed as a safe diagnostic no-op.");
                TranslatedTagCount++;
                return true;
            }

            AddDiagnostic(
                TextStudioCompatibilityDiagnosticSeverity.Warning,
                open,
                close + 1 - open,
                "Unsupported TMPEffects command '" + name + "' was kept as literal text.");
            return false;
        }

        bool TryTranslateEvent(string source, int start, int end, int open, int close)
        {
            int nameEnd = ScanNameEnd(source, start, end);
            if (nameEnd <= start)
                return false;

            string route = source.Substring(start, nameEnd - start);
            string directValue = ReadDirectValue(source, nameEnd, end, out int remainingStart);
            _tmpeffectsBuilder.Append("<event route=").Append(QuoteIfNeeded(route));
            if (!string.IsNullOrEmpty(directValue) || remainingStart < end)
            {
                _parameterBuilder.Clear();
                if (!string.IsNullOrEmpty(directValue))
                    _parameterBuilder.Append(directValue);
                if (remainingStart < end)
                {
                    if (_parameterBuilder.Length > 0)
                        _parameterBuilder.Append(' ');
                    _parameterBuilder.Append(source, remainingStart, end - remainingStart);
                }

                _tmpeffectsBuilder.Append(" payload=")
                    .Append(QuoteIfNeeded(_parameterBuilder.ToString()));
            }
            _tmpeffectsBuilder.Append('>');
            if (remainingStart < end)
            {
                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Information,
                    open,
                    close + 1 - open,
                    "TMPEffects event parameters were preserved together as one Text Studio payload.");
            }
            TranslatedTagCount++;
            return true;
        }

        bool TryTranslatePhaseAnimation(
            string source,
            int start,
            int end,
            int open,
            int close,
            char prefix)
        {
            int nameEnd = ScanNameEnd(source, start, end);
            if (nameEnd <= start)
                return false;

            string vendorName = source.Substring(start, nameEnd - start);
            bool show = prefix == '+';
            if (!TryMapPhase(vendorName, show, out string effects))
            {
                AddDiagnostic(
                    TextStudioCompatibilityDiagnosticSeverity.Warning,
                    open,
                    close + 1 - open,
                    "Unsupported TMPEffects " + (show ? "show" : "hide") +
                    " animation '" + vendorName + "' was kept as literal text.");
                return false;
            }

            string phase = show
                ? TextStudioPhaseTagUtility.AppearTag
                : TextStudioPhaseTagUtility.DisappearTag;
            string parameters = NormalizeEffectParameters(source, nameEnd, end);
            _tmpeffectsBuilder.Append('<').Append(phase).Append(' ').Append(effects);
            if (!string.IsNullOrEmpty(parameters))
                _tmpeffectsBuilder.Append(' ').Append(parameters);
            _tmpeffectsBuilder.Append('>');
            _vendorRanges.Add(new VendorRange(
                vendorName,
                "</" + phase + ">",
                show ? VendorRangeKind.Show : VendorRangeKind.Hide));
            AddParameterApproximation(source, nameEnd, end, open, close);
            TranslatedTagCount++;
            return true;
        }

        string EscapeNonTmpAngleTags(string source)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf('<') < 0)
                return source ?? string.Empty;

            _tmpeffectsBuilder.Clear();
            int cursor = 0;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('<', cursor);
                if (open < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (TryGetLiteralMarkupEnd(source, open, out int literalEnd))
                {
                    _tmpeffectsBuilder.Append(source, cursor, literalEnd - cursor);
                    cursor = literalEnd;
                    continue;
                }

                int close = source.IndexOf('>', open + 1);
                if (close < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                _tmpeffectsBuilder.Append(source, cursor, open - cursor);
                int start = open + 1;
                int end = close;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
                if (IsTmpRichTextTag(source, start, end))
                    _tmpeffectsBuilder.Append(source, open, close + 1 - open);
                else
                    AppendLiteralTag(source, open, close);
                cursor = close + 1;
            }

            return _tmpeffectsBuilder.ToString();
        }

        string EscapeTMPEffectsPrefixedTags(string source)
        {
            if (string.IsNullOrEmpty(source) || source.IndexOf('<') < 0)
            {
                source ??= string.Empty;
                RecordIdentity(TextStudioAuthoringProfile.Native, source.Length, source.Length);
                return source;
            }

            // Most text holds no prefixed tag; it returns unchanged without copying it into the builder.
            if (ScanEscapeIdentity(source, 0, out int resume))
            {
                RecordIdentity(TextStudioAuthoringProfile.Native, source.Length, resume);
                return source;
            }

            _tmpeffectsBuilder.Clear();
            int cursor = 0;
            bool changed = false;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('<', cursor);
                if (open < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                if (TryGetLiteralMarkupEnd(source, open, out int literalEnd))
                {
                    _tmpeffectsBuilder.Append(source, cursor, literalEnd - cursor);
                    cursor = literalEnd;
                    continue;
                }

                int close = source.IndexOf('>', open + 1);
                if (close < 0)
                {
                    _tmpeffectsBuilder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                _tmpeffectsBuilder.Append(source, cursor, open - cursor);
                if (IsTMPEffectsPrefixedTag(source, open + 1, close))
                {
                    AppendLiteralTag(source, open, close);
                    AddDiagnostic(
                        TextStudioCompatibilityDiagnosticSeverity.Information,
                        open,
                        close + 1 - open,
                        "TMPEffects syntax is disabled by the Native authoring profile and was kept as literal text.");
                    changed = true;
                }
                else
                {
                    _tmpeffectsBuilder.Append(source, open, close + 1 - open);
                }

                cursor = close + 1;
            }

            return changed ? _tmpeffectsBuilder.ToString() : source;
        }

        // Whether EscapeTMPEffectsPrefixedTags leaves the text from start on unchanged, with the position text appended to it
        // must be scanned again from, as in HasTMPEffectsEvidence.
        static bool ScanEscapeIdentity(string source, int start, out int resume)
        {
            resume = source.Length;
            int cursor = start;
            while (cursor < source.Length)
            {
                int open = source.IndexOf('<', cursor);
                if (open < 0)
                    return true;

                if (TryGetLiteralMarkupEnd(source, open, out int literalEnd, out bool unterminated))
                {
                    if (unterminated)
                    {
                        resume = open;
                        return true;
                    }
                    cursor = literalEnd;
                    continue;
                }

                int close = source.IndexOf('>', open + 1);
                if (close < 0)
                {
                    resume = open;
                    return true;
                }

                if (IsTMPEffectsPrefixedTag(source, open + 1, close))
                    return false;
                cursor = close + 1;
            }

            return true;
        }

        void AppendOpenRange(
            string vendorName,
            string effectMarkup,
            string parameters,
            string closeMarkup,
            VendorRangeKind kind)
        {
            _tmpeffectsBuilder.Append('<').Append(effectMarkup);
            if (!string.IsNullOrEmpty(parameters))
                _tmpeffectsBuilder.Append(' ').Append(parameters);
            _tmpeffectsBuilder.Append('>');
            _vendorRanges.Add(new VendorRange(vendorName, closeMarkup, kind));
            TranslatedTagCount++;
        }

        void AppendLiteralTag(string source, int open, int close)
        {
            _tmpeffectsBuilder.Append('\\').Append(source, open, close + 1 - open);
        }

        string NormalizeEffectParameters(string source, int nameEnd, int end)
        {
            int start = nameEnd;
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            if (start >= end)
                return string.Empty;

            _parameterBuilder.Clear();
            bool quoted = false;
            char quote = '\0';
            int tokenStart = start;
            for (int i = start; i <= end; i++)
            {
                bool atEnd = i == end;
                if (!atEnd)
                {
                    char current = source[i];
                    if ((current == '"' || current == '\'') &&
                        (i == tokenStart || source[i - 1] != '\\'))
                    {
                        if (!quoted)
                        {
                            quoted = true;
                            quote = current;
                        }
                        else if (quote == current)
                        {
                            quoted = false;
                        }
                    }
                }

                if (!atEnd && (quoted || !char.IsWhiteSpace(source[i])))
                    continue;

                if (i > tokenStart)
                {
                    if (_parameterBuilder.Length > 0)
                        _parameterBuilder.Append(' ');
                    AppendNormalizedParameter(source, tokenStart, i);
                }

                while (i + 1 < end && char.IsWhiteSpace(source[i + 1]))
                    i++;
                tokenStart = i + 1;
            }

            return _parameterBuilder.ToString();
        }

        void AppendNormalizedParameter(string source, int start, int end)
        {
            int equals = source.IndexOf('=', start, end - start);
            if (equals <= start)
            {
                _parameterBuilder.Append(source, start, end - start);
                return;
            }

            string name = source.Substring(start, equals - start);
            _parameterBuilder.Append(CanonicalParameterName(name));
            _parameterBuilder.Append(source, equals, end - equals);
        }

        void AddParameterApproximation(string source, int nameEnd, int end, int open, int close)
        {
            int start = nameEnd;
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            if (start >= end)
                return;

            AddDiagnostic(
                TextStudioCompatibilityDiagnosticSeverity.Information,
                open,
                close + 1 - open,
                "TMPEffects parameters were normalized to the closest Text Studio controls; unsupported provider-specific values are ignored.");
        }

        void AddDiagnostic(
            TextStudioCompatibilityDiagnosticSeverity severity,
            int start,
            int length,
            string message)
        {
            _diagnostics.Add(new TextStudioCompatibilityDiagnostic(severity, start, length, message));
        }

        bool TryPopLatestRange(out VendorRange range)
        {
            if (_vendorRanges.Count == 0)
            {
                range = default;
                return false;
            }

            int index = _vendorRanges.Count - 1;
            range = _vendorRanges[index];
            _vendorRanges.RemoveAt(index);
            return true;
        }

        bool TryPopRange(string name, VendorRangeKind kind, out VendorRange range)
        {
            for (int i = _vendorRanges.Count - 1; i >= 0; i--)
            {
                VendorRange candidate = _vendorRanges[i];
                if (candidate.Kind != kind)
                    continue;
                if (!string.IsNullOrEmpty(name) &&
                    !string.Equals(candidate.VendorName, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                range = candidate;
                _vendorRanges.RemoveAt(i);
                return true;
            }

            range = default;
            return false;
        }

        static bool TryTranslateTextAnimatorBraceTag(
            string source,
            int start,
            int end,
            out string replacement)
        {
            replacement = string.Empty;
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            while (end > start && char.IsWhiteSpace(source[end - 1]))
                end--;
            if (start >= end)
                return false;

            bool isClose = source[start] == '/';
            if (isClose)
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
            }

            bool isDisappear = start < end && source[start] == '#';
            if (isDisappear)
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
            }

            if (start >= end)
                return false;

            int nameEnd = start;
            while (nameEnd < end &&
                   !char.IsWhiteSpace(source[nameEnd]) &&
                   source[nameEnd] != '/' &&
                   source[nameEnd] != '#')
            {
                nameEnd++;
            }

            if (nameEnd <= start)
                return false;

            string effectName = source.Substring(start, nameEnd - start);
            if (!TextStudioPhaseTagUtility.TryResolveEffectId(effectName, out _))
                return false;

            string phaseTag = isDisappear
                ? TextStudioPhaseTagUtility.DisappearTag
                : TextStudioPhaseTagUtility.AppearTag;
            if (isClose)
            {
                replacement = "</" + phaseTag + ">";
                return true;
            }

            while (nameEnd < end && char.IsWhiteSpace(source[nameEnd]))
                nameEnd++;
            string parameters = nameEnd < end
                ? source.Substring(nameEnd, end - nameEnd).Trim()
                : string.Empty;
            replacement = string.IsNullOrEmpty(parameters)
                ? "<" + phaseTag + " " + effectName + ">"
                : "<" + phaseTag + " " + effectName + " " + parameters + ">";
            return true;
        }

        static bool TryMapPersistent(string name, out string markup)
        {
            markup = string.Empty;
            if (string.IsNullOrEmpty(name))
                return false;

            switch (name.ToLowerInvariant())
            {
                case "jump": markup = TextStudioBuiltInEffectIds.Bounce; return true;
                case "dangle": markup = TextStudioBuiltInEffectIds.Dangle; return true;
                case "spread": markup = TextStudioBuiltInEffectIds.Expand; return true;
                case "fade": markup = TextStudioBuiltInEffectIds.Fade; return true;
                case "grow": markup = TextStudioBuiltInEffectIds.Size; return true;
                case "swing": markup = TextStudioBuiltInEffectIds.Pendulum; return true;
                case "palette": markup = TextStudioBuiltInEffectIds.Rainbow; return true;
                case "pivotc": markup = TextStudioBuiltInEffectIds.Rotate; return true;
                case "shake": markup = TextStudioBuiltInEffectIds.Shake; return true;
                case "pivot": markup = TextStudioBuiltInEffectIds.Swing; return true;
                case "wave": markup = TextStudioBuiltInEffectIds.Wave; return true;
                case "sketchy": markup = TextStudioBuiltInEffectIds.Wiggle; return true;
                case "shear": markup = TextStudioBuiltInEffectIds.Shear; return true;
                case "char": markup = BuiltInCharacterSubstitutions.Scramble + " infinite"; return true;
                case "funky": markup = TextStudioBuiltInEffectIds.CornerWarp + " mode=squeeze"; return true;
                default: return false;
            }
        }

        static bool TryMapPhase(string name, bool show, out string effects)
        {
            effects = string.Empty;
            if (string.IsNullOrEmpty(name))
                return false;

            switch (name.ToLowerInvariant())
            {
                case "fade": effects = TextStudioBuiltInEffectIds.Fade; return true;
                case "grow": effects = TextStudioBuiltInEffectIds.Size; return true;
                case "spread": effects = TextStudioBuiltInEffectIds.Expand; return true;
                case "pivot": effects = TextStudioBuiltInEffectIds.Swing; return true;
                case "shake": effects = TextStudioBuiltInEffectIds.Shake; return true;
                case "move":
                    effects = TextStudioBuiltInEffectIds.SlideHorizontal + "," +
                              TextStudioBuiltInEffectIds.SlideVertical;
                    return true;
                case "char":
                    effects = show
                        ? TextStudioBuiltInEffectIds.Decrypt
                        : TextStudioBuiltInEffectIds.Shake + "," + TextStudioBuiltInEffectIds.Fade;
                    return true;
                default:
                    return false;
            }
        }

        static bool IsTMPEffectsExclusiveName(string name)
        {
            if (!TryMapPersistent(name, out _))
                return false;

            return !string.Equals(name, "wave", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(name, "shake", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(name, "fade", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(name, "swing", StringComparison.OrdinalIgnoreCase);
        }

        static bool HasTMPEffectsSpecificParameter(string source, int start, int end)
        {
            if (start >= end)
                return false;

            string parameters = source.Substring(start, end - start);
            string[] evidence =
            {
                "amp=", "upperiod=", "uppd=", "downperiod=", "downpd=", "dnpd=",
                "upcurve=", "upcrv=", "downcurve=", "downcrv=", "minangle=",
                "maxangle=", "growanchor=", "shrinkanchor=", "targetopacity=",
                "startpercentage=", "targetpercentage="
            };
            for (int i = 0; i < evidence.Length; i++)
            {
                if (parameters.IndexOf(evidence[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static bool IsTMPEffectsPrefixedTag(string source, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(source[start]))
                start++;
            if (start >= end)
                return false;

            if (source[start] == '/')
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
                if (start >= end)
                    return true;
                if (string.Equals(
                        source.Substring(start, end - start),
                        "all",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return start < end &&
                   (source[start] == '+' ||
                    source[start] == '-' ||
                    source[start] == '!' ||
                    source[start] == '?');
        }

        static bool IsKnownTextStudioTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name[0] == '$' && TextStudioAppearanceTagSyntax.IsValidId(name.Substring(1))) return true;
            if (TextStudioPhaseTagUtility.TryResolveEffectId(name, out _) ||
                TextStudioPhaseTagUtility.IsPhaseTag(name) ||
                BuiltInActions.TryGetCanonicalActionId(name, out _) ||
                BuiltInCharacterSubstitutions.IsCharacterSubstitutionTag(name))
            {
                return true;
            }

            if (string.Equals(name, "instant", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "uu", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "o", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return TextStudioTagUtility.TryGetExtensionTag(name, false, out _);
        }

        static bool IsTmpRichTextTag(string source, int start, int end)
        {
            if (start >= end)
                return false;

            if (source[start] == '/')
            {
                start++;
                while (start < end && char.IsWhiteSpace(source[start]))
                    start++;
            }

            int nameEnd = ScanNameEnd(source, start, end);
            if (nameEnd <= start)
                return false;

            string name = source.Substring(start, nameEnd - start);
            bool hasParameter = nameEnd < end;
            int assignmentCursor = nameEnd;
            while (assignmentCursor < end && char.IsWhiteSpace(source[assignmentCursor]))
                assignmentCursor++;
            bool usesDirectAssignment =
                assignmentCursor < end && source[assignmentCursor] == '=';
            if (BuiltInStyles.IsNativeRichTextFormattingInvocation(
                    name,
                    hasParameter,
                    usesDirectAssignment) ||
                BuiltInStyles.IsSelfContainedNativeRichTextFormatting(name, hasParameter))
            {
                return true;
            }

            return string.Equals(name, "noparse", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "style", StringComparison.OrdinalIgnoreCase);
        }

        static string GetCanonicalRangeName(string markup)
        {
            int end = markup.IndexOf(' ');
            return end > 0 ? markup.Substring(0, end) : markup;
        }

        static int ScanNameEnd(string source, int start, int end)
        {
            int cursor = start;
            while (cursor < end)
            {
                char value = source[cursor];
                if (char.IsWhiteSpace(value) || value == '=' || value == '/')
                    break;
                cursor++;
            }

            return cursor;
        }

        static string ReadDirectValue(string source, int nameEnd, int end)
        {
            return ReadDirectValue(source, nameEnd, end, out _);
        }

        static string ReadDirectValue(string source, int nameEnd, int end, out int remainingStart)
        {
            int cursor = nameEnd;
            while (cursor < end && char.IsWhiteSpace(source[cursor]))
                cursor++;
            if (cursor >= end || source[cursor] != '=')
            {
                remainingStart = cursor;
                return string.Empty;
            }

            cursor++;
            while (cursor < end && char.IsWhiteSpace(source[cursor]))
                cursor++;
            if (cursor >= end)
            {
                remainingStart = end;
                return string.Empty;
            }

            int valueStart = cursor;
            if (source[cursor] == '"' || source[cursor] == '\'')
            {
                char quote = source[cursor++];
                while (cursor < end && source[cursor] != quote)
                    cursor++;
                if (cursor < end)
                    cursor++;
            }
            else
            {
                while (cursor < end && !char.IsWhiteSpace(source[cursor]))
                    cursor++;
            }

            string value = source.Substring(valueStart, cursor - valueStart).Trim();
            remainingStart = cursor;
            while (remainingStart < end && char.IsWhiteSpace(source[remainingStart]))
                remainingStart++;
            return TrimMatchingQuotes(value);
        }

        static string CanonicalParameterName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name ?? string.Empty;

            switch (name.ToLowerInvariant())
            {
                case "amp": return "amplitude";
                case "dur": return "duration";
                case "spd": return "speed";
                case "freq": return "frequency";
                case "targetopacity":
                case "targetop": return "min";
                case "minangle": return "angle";
                case "maxangle": return "amplitude";
                case "chars":
                case "char": return "charset";
                case "rot": return "rotation";
                default: return name;
            }
        }

        static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) < 0)
                return value;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        static string TrimMatchingQuotes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return value ?? string.Empty;
            char first = value[0];
            return (first == '"' || first == '\'') && value[value.Length - 1] == first
                ? value.Substring(1, value.Length - 2)
                : value;
        }
    }
}
