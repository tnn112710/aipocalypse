using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextAnimatorMigrationSeverity
    {
        Info,
        Warning,
        Error
    }

    internal enum TextAnimatorMigrationStatus
    {
        Covered,
        Converted,
        Manual,
        Unsupported
    }

    internal enum TextAnimatorMigrationRuleKind
    {
        Effect,
        Appearance,
        Disappearance,
        Action,
        Playback,
        Modifier,
        Style,
        NativeRichText,
        Unknown
    }

    internal enum TextAnimatorMigrationSourceKind
    {
        ManualText,
        Selection,
        Scene,
        Prefab,
        TextAsset,
        Folder,
        StyleSheet
    }

    internal readonly struct TextAnimatorMigrationRule
    {
        public readonly string TextAnimatorTag;
        public readonly string TextStudioTag;
        public readonly TextAnimatorMigrationRuleKind Kind;
        public readonly TextAnimatorMigrationStatus Status;
        public readonly string Message;

        public TextAnimatorMigrationRule(
            string textAnimatorTag,
            string textStudioTag,
            TextAnimatorMigrationRuleKind kind,
            TextAnimatorMigrationStatus status,
            string message)
        {
            TextAnimatorTag = textAnimatorTag ?? string.Empty;
            TextStudioTag = textStudioTag ?? string.Empty;
            Kind = kind;
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    internal readonly struct TextAnimatorMigrationIssue
    {
        public readonly TextAnimatorMigrationSeverity Severity;
        public readonly TextAnimatorMigrationStatus Status;
        public readonly TextAnimatorMigrationRuleKind RuleKind;
        public readonly string OriginalText;
        public readonly string ReplacementText;
        public readonly int Start;
        public readonly int End;
        public readonly string Message;
        public readonly string Action;

        public TextAnimatorMigrationIssue(
            TextAnimatorMigrationSeverity severity,
            TextAnimatorMigrationStatus status,
            TextAnimatorMigrationRuleKind ruleKind,
            string originalText,
            string replacementText,
            int start,
            int end,
            string message,
            string action)
        {
            Severity = severity;
            Status = status;
            RuleKind = ruleKind;
            OriginalText = originalText ?? string.Empty;
            ReplacementText = replacementText ?? string.Empty;
            Start = Mathf.Max(0, start);
            End = Mathf.Max(Start, end);
            Message = message ?? string.Empty;
            Action = action ?? string.Empty;
        }
    }

    internal readonly struct TextAnimatorMigrationReplacement
    {
        public readonly int Start;
        public readonly int End;
        public readonly string OriginalText;
        public readonly string ReplacementText;

        public TextAnimatorMigrationReplacement(int start, int end, string originalText, string replacementText)
        {
            Start = Mathf.Max(0, start);
            End = Mathf.Max(Start, end);
            OriginalText = originalText ?? string.Empty;
            ReplacementText = replacementText ?? string.Empty;
        }
    }

    internal sealed class TextAnimatorMigrationResult
    {
        readonly TextAnimatorMigrationIssue[] _issues;
        readonly TextAnimatorMigrationReplacement[] _replacements;

        public string SourceMarkup { get; }
        public string ConvertedMarkup { get; }
        public IReadOnlyList<TextAnimatorMigrationIssue> Issues => _issues;
        public IReadOnlyList<TextAnimatorMigrationReplacement> Replacements => _replacements;
        public int CoveredCount { get; }
        public int ConvertedCount { get; }
        public int ManualCount { get; }
        public int UnsupportedCount { get; }
        public bool HasChanges => _replacements.Length > 0 && !string.Equals(SourceMarkup, ConvertedMarkup, StringComparison.Ordinal);

        public TextAnimatorMigrationResult(
            string sourceMarkup,
            string convertedMarkup,
            IReadOnlyList<TextAnimatorMigrationIssue> issues,
            IReadOnlyList<TextAnimatorMigrationReplacement> replacements)
        {
            SourceMarkup = sourceMarkup ?? string.Empty;
            ConvertedMarkup = convertedMarkup ?? string.Empty;
            _issues = Copy(issues);
            _replacements = Copy(replacements);

            for (int i = 0; i < _issues.Length; i++)
            {
                switch (_issues[i].Status)
                {
                    case TextAnimatorMigrationStatus.Covered:
                        CoveredCount++;
                        break;
                    case TextAnimatorMigrationStatus.Converted:
                        ConvertedCount++;
                        break;
                    case TextAnimatorMigrationStatus.Manual:
                        ManualCount++;
                        break;
                    case TextAnimatorMigrationStatus.Unsupported:
                        UnsupportedCount++;
                        break;
                }
            }
        }

        static TextAnimatorMigrationIssue[] Copy(IReadOnlyList<TextAnimatorMigrationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
                return Array.Empty<TextAnimatorMigrationIssue>();

            TextAnimatorMigrationIssue[] copy = new TextAnimatorMigrationIssue[issues.Count];
            for (int i = 0; i < issues.Count; i++)
                copy[i] = issues[i];
            return copy;
        }

        static TextAnimatorMigrationReplacement[] Copy(IReadOnlyList<TextAnimatorMigrationReplacement> replacements)
        {
            if (replacements == null || replacements.Count == 0)
                return Array.Empty<TextAnimatorMigrationReplacement>();

            TextAnimatorMigrationReplacement[] copy = new TextAnimatorMigrationReplacement[replacements.Count];
            for (int i = 0; i < replacements.Count; i++)
                copy[i] = replacements[i];
            return copy;
        }
    }

    internal sealed class TextAnimatorMigrationCandidate
    {
        public string Label { get; }
        public string AssetPath { get; }
        public TextAnimatorMigrationSourceKind SourceKind { get; }
        public UnityEngine.Object Target { get; }
        public string PropertyPath { get; }
        public string SourceMarkup { get; }
        public bool CanApply => Target != null && !string.IsNullOrEmpty(PropertyPath);

        public TextAnimatorMigrationCandidate(
            string label,
            string assetPath,
            TextAnimatorMigrationSourceKind sourceKind,
            UnityEngine.Object target,
            string propertyPath,
            string sourceMarkup)
        {
            Label = label ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            SourceKind = sourceKind;
            Target = target;
            PropertyPath = propertyPath ?? string.Empty;
            SourceMarkup = sourceMarkup ?? string.Empty;
        }
    }

    internal readonly struct TextAnimatorMigrationStyle
    {
        public readonly string StyleTag;
        public readonly string OpeningTag;
        public readonly string ClosingTag;

        public TextAnimatorMigrationStyle(string styleTag, string openingTag, string closingTag)
        {
            StyleTag = styleTag ?? string.Empty;
            OpeningTag = openingTag ?? string.Empty;
            ClosingTag = closingTag ?? string.Empty;
        }
    }

#if UNITY_6000_7_OR_NEWER
    // The shared empty value is immutable migration metadata and is safe for the code-load lifetime.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextAnimatorMigrationStyleSheet
    {
        public static readonly TextAnimatorMigrationStyleSheet Empty = new TextAnimatorMigrationStyleSheet(Array.Empty<TextAnimatorMigrationStyle>());

        readonly Dictionary<string, TextAnimatorMigrationStyle> _styles;

        public IReadOnlyDictionary<string, TextAnimatorMigrationStyle> Styles => _styles;
        public int Count => _styles.Count;

        public TextAnimatorMigrationStyleSheet(IEnumerable<TextAnimatorMigrationStyle> styles)
        {
            _styles = new Dictionary<string, TextAnimatorMigrationStyle>(StringComparer.OrdinalIgnoreCase);
            if (styles == null)
                return;

            foreach (TextAnimatorMigrationStyle style in styles)
            {
                if (!string.IsNullOrWhiteSpace(style.StyleTag))
                    _styles[style.StyleTag.Trim()] = style;
            }
        }

        public bool TryGet(string styleTag, out TextAnimatorMigrationStyle style)
        {
            if (string.IsNullOrWhiteSpace(styleTag))
            {
                style = default;
                return false;
            }

            return _styles.TryGetValue(styleTag.Trim(), out style);
        }
    }
}
