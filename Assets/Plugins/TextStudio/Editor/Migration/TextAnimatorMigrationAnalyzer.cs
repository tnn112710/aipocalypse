using System;
using System.Collections.Generic;
using System.Text;
using TextStudio.BuiltIns;

namespace TextStudio.Editor
{
    internal static class TextAnimatorMigrationAnalyzer
    {
        public static TextAnimatorMigrationResult Analyze(string source)
        {
            return Analyze(source, TextAnimatorMigrationStyleSheet.Empty);
        }

        public static TextAnimatorMigrationResult Analyze(string source, TextAnimatorMigrationStyleSheet styleSheet)
        {
            source ??= string.Empty;
            styleSheet ??= TextAnimatorMigrationStyleSheet.Empty;

            IReadOnlyList<TextAnimatorMarkupToken> tokens = TextAnimatorMarkupScanner.Scan(source);
            List<TextAnimatorMigrationIssue> issues = new List<TextAnimatorMigrationIssue>(tokens.Count);
            List<TextAnimatorMigrationReplacement> replacements = new List<TextAnimatorMigrationReplacement>(tokens.Count);
            StringBuilder builder = new StringBuilder(source.Length + 32);

            int cursor = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                TextAnimatorMarkupToken token = tokens[i];
                if (token.Start < cursor)
                    continue;

                builder.Append(source, cursor, token.Start - cursor);
                string replacement = ResolveToken(token, styleSheet, issues, replacements);
                builder.Append(replacement);
                cursor = token.End;
            }

            if (cursor < source.Length)
                builder.Append(source, cursor, source.Length - cursor);

            return new TextAnimatorMigrationResult(source, builder.ToString(), issues, replacements);
        }

        static string ResolveToken(
            TextAnimatorMarkupToken token,
            TextAnimatorMigrationStyleSheet styleSheet,
            List<TextAnimatorMigrationIssue> issues,
            List<TextAnimatorMigrationReplacement> replacements)
        {
            if (token.IsBrace)
                return ResolveBraceToken(token, issues);

            if (TryResolveStyleSheetToken(token, styleSheet, issues, replacements, out string styleReplacement))
                return styleReplacement;

            if (TryResolveTextStudioPhaseToken(token, issues))
            {
                AnalyzeParameters(token, issues);
                return token.RawText;
            }

            if (TextAnimatorMigrationRules.TryGet(token.Name, out TextAnimatorMigrationRule rule))
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    rule.Status,
                    rule.Kind,
                    token,
                    token.RawText,
                    rule.Message,
                    rule.Status == TextAnimatorMigrationStatus.Covered
                        ? "No change required."
                        : "Review this tag before applying.");
            }
            else if (BuiltInActions.TryGetCanonicalActionId(token.Name, out _))
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    TextAnimatorMigrationStatus.Covered,
                    TextAnimatorMigrationRuleKind.Action,
                    token,
                    token.RawText,
                    "Text Studio supports this action marker directly.",
                    "No change required.");
            }
            else if (IsNativeRichText(token))
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    TextAnimatorMigrationStatus.Covered,
                    TextAnimatorMigrationRuleKind.NativeRichText,
                    token,
                    token.RawText,
                    "Native rich-text formatting is passed through by Text Studio.",
                    "No change required.");
            }
            else if (TextAnimatorMigrationRules.TryGetRegisteredCoverage(token.Name, out TextAnimatorMigrationRuleKind registeredKind))
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    TextAnimatorMigrationStatus.Covered,
                    registeredKind,
                    token,
                    token.RawText,
                    registeredKind == TextAnimatorMigrationRuleKind.Style
                        ? "Text Studio expands this registered tag alias."
                        : "Text Studio supports this tag through a registered project asset.",
                    "No change required.");
            }
            else
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Warning,
                    TextAnimatorMigrationStatus.Manual,
                    TextAnimatorMigrationRuleKind.Unknown,
                    token,
                    token.RawText,
                    "Text Animator tag uses a custom or unknown implementation.",
                    "Create a matching Text Studio effect, action, style, or replace this tag manually.");
            }

            AnalyzeParameters(token, issues);
            return token.RawText;
        }

        static bool TryResolveTextStudioPhaseToken(
            TextAnimatorMarkupToken token,
            List<TextAnimatorMigrationIssue> issues)
        {
            if (!TextStudioPhaseTagUtility.TryGetPhase(token.Name, out AnimationPhase phase))
                return false;

            AddIssue(
                issues,
                TextAnimatorMigrationSeverity.Info,
                TextAnimatorMigrationStatus.Covered,
                phase == AnimationPhase.Disappearance
                    ? TextAnimatorMigrationRuleKind.Disappearance
                    : TextAnimatorMigrationRuleKind.Appearance,
                token,
                token.RawText,
                "Text Studio phase wrapper is already native markup.",
                "No change required.");
            return true;
        }

        static string ResolveBraceToken(
            TextAnimatorMarkupToken token,
            List<TextAnimatorMigrationIssue> issues)
        {
            if (!TextStudioPhaseTagUtility.TryResolveEffectId(token.Name, out _))
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Warning,
                    TextAnimatorMigrationStatus.Manual,
                    token.IsDisappearanceBrace ? TextAnimatorMigrationRuleKind.Disappearance : TextAnimatorMigrationRuleKind.Appearance,
                    token,
                    token.RawText,
                    "Text Animator brace tag uses a custom or unknown effect.",
                    "Create a matching Text Studio effect or replace the tag manually.");
                return token.RawText;
            }

            AddIssue(
                issues,
                TextAnimatorMigrationSeverity.Info,
                TextAnimatorMigrationStatus.Covered,
                token.IsDisappearanceBrace ? TextAnimatorMigrationRuleKind.Disappearance : TextAnimatorMigrationRuleKind.Appearance,
                token,
                token.RawText,
                "Text Studio supports this Text Animator brace phase tag directly.",
                "No change required.");
            return token.RawText;
        }

        static bool TryResolveStyleSheetToken(
            TextAnimatorMarkupToken token,
            TextAnimatorMigrationStyleSheet styleSheet,
            List<TextAnimatorMigrationIssue> issues,
            List<TextAnimatorMigrationReplacement> replacements,
            out string replacement)
        {
            replacement = string.Empty;
            if (styleSheet == null || !styleSheet.TryGet(token.Name, out TextAnimatorMigrationStyle style))
                return false;

            replacement = token.IsClosing
                ? GetStyleClosingMarkup(style)
                : style.OpeningTag;
            replacements.Add(new TextAnimatorMigrationReplacement(token.Start, token.End, token.RawText, replacement));
            AddIssue(
                issues,
                TextAnimatorMigrationSeverity.Info,
                TextAnimatorMigrationStatus.Converted,
                TextAnimatorMigrationRuleKind.Style,
                token,
                replacement,
                "Text Animator style-sheet tag can be expanded to Text Studio markup.",
                "Apply the style-sheet replacement.");
            return true;
        }

        static string GetStyleClosingMarkup(TextAnimatorMigrationStyle style)
        {
            if (!string.IsNullOrWhiteSpace(style.ClosingTag))
                return style.ClosingTag;

            return TextStudioTagExpansionUtility.DeriveClosingMarkup(style.OpeningTag);
        }

        static void AnalyzeParameters(TextAnimatorMarkupToken token, List<TextAnimatorMigrationIssue> issues)
        {
            if (token.IsClosing || string.IsNullOrWhiteSpace(token.RawParameter))
                return;

            TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(
                token.RawParameter,
                allowNumericModifiers: true);
            for (int i = 0; i < parameters.PositionalCount; i++)
                AnalyzePlaybackValue(parameters.GetString(i), token, issues);

            for (int i = 0; i < parameters.NamedCount; i++)
            {
                TextStudioNamedTagParameter parameter = parameters.Named[i];
                if (parameter.Operation == TextStudioTagParameterOperation.Multiply)
                {
                    AnalyzeNumericModifier(parameter, token, issues);
                    continue;
                }

                if (parameter.IsFlag)
                    AnalyzePlaybackValue(parameter.Name, token, issues);
            }
        }

        static void AnalyzeNumericModifier(
            TextStudioNamedTagParameter parameter,
            TextAnimatorMarkupToken token,
            List<TextAnimatorMigrationIssue> issues)
        {
            bool validFactor = float.TryParse(
                                   parameter.Value,
                                   System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   out float factor) &&
                               !float.IsNaN(factor) &&
                               !float.IsInfinity(factor);
            bool parameterizedEffect = TextStudioPhaseTagUtility.TryResolveEffectId(token.Name, out string effectId) &&
                                       TextStudioRegistry.Effects.TryGetQuiet(effectId, out ITextStudioEffectDefinition effect) &&
                                       effect is ITextStudioEffectFactory;

            if (validFactor && parameterizedEffect)
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    TextAnimatorMigrationStatus.Covered,
                    TextAnimatorMigrationRuleKind.Modifier,
                    token,
                    token.RawText,
                    "Text Studio supports this numeric Text Animator parameter multiplier directly.",
                    "No change required.");
                return;
            }

            AddIssue(
                issues,
                TextAnimatorMigrationSeverity.Warning,
                TextAnimatorMigrationStatus.Manual,
                TextAnimatorMigrationRuleKind.Modifier,
                token,
                token.RawText,
                validFactor
                    ? "This tag does not expose Text Studio-compatible numeric parameter multiplication."
                    : "This Text Animator parameter multiplier does not use a finite numeric factor.",
                validFactor
                    ? "Recreate the behavior with a parameter-aware Text Studio effect or replace the modifier."
                    : "Replace the multiplier with a finite number.");
        }

        static void AnalyzePlaybackValue(
            string value,
            TextAnimatorMarkupToken token,
            List<TextAnimatorMigrationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (TextAnimatorMigrationRules.IsKnownPlaybackKeyword(value, out _) ||
                TextAnimatorMigrationRules.TryGetRegisteredCoverage(value, out TextAnimatorMigrationRuleKind registeredKind) &&
                registeredKind == TextAnimatorMigrationRuleKind.Playback)
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Info,
                    TextAnimatorMigrationStatus.Covered,
                    TextAnimatorMigrationRuleKind.Playback,
                    token,
                    token.RawText,
                    "Text Studio accepts this Text Animator playback keyword directly.",
                    "No change required.");
                return;
            }

            if (value.IndexOf("playback", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddIssue(
                    issues,
                    TextAnimatorMigrationSeverity.Warning,
                    TextAnimatorMigrationStatus.Manual,
                    TextAnimatorMigrationRuleKind.Playback,
                    token,
                    token.RawText,
                    "This looks like a custom Text Animator playback keyword.",
                    "Import or recreate the Text Animator playback as a Text Studio timing/effect profile.");
            }
        }

        static bool IsNativeRichText(TextAnimatorMarkupToken token)
        {
            if (string.IsNullOrEmpty(token.Name))
                return false;

            bool hasParameter = !string.IsNullOrEmpty(token.RawParameter);
            return BuiltInStyles.IsNativeRichTextFormatting(token.Name, hasParameter) ||
                   BuiltInStyles.IsSelfContainedNativeRichTextFormatting(token.Name, hasParameter);
        }

        static void AddIssue(
            List<TextAnimatorMigrationIssue> issues,
            TextAnimatorMigrationSeverity severity,
            TextAnimatorMigrationStatus status,
            TextAnimatorMigrationRuleKind kind,
            TextAnimatorMarkupToken token,
            string replacementText,
            string message,
            string action)
        {
            issues.Add(new TextAnimatorMigrationIssue(
                severity,
                status,
                kind,
                token.RawText,
                replacementText,
                token.Start,
                token.End,
                message,
                action));
        }
    }
}
