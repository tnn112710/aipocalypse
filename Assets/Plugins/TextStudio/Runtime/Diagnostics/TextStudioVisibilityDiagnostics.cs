using System;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioOwnershipMode
    {
        NoRenderer,
        Mirror,
        Drive,
        ExternalSource,
        Renderer,
        RendererMix
    }

    [Flags]
    public enum TextStudioVisibilityIssue
    {
        None = 0,
        InactiveGameObject = 1 << 0,
        DisabledComponent = 1 << 1,
        EmptyMarkup = 1 << 2,
        NoRenderer = 1 << 3,
        MissingTmpText = 1 << 4,
        ZeroRevealedGlyphs = 1 << 5,
        RevealPaused = 1 << 6,
        ExternalSourceOwnsText = 1 << 7,
        NoTarget = 1 << 8
    }

    /// <summary>Immutable visibility-readiness report with issue flags, diagnostic state, and user guidance.</summary>
    public readonly struct TextStudioVisibilityReport
    {
        public readonly TextStudioVisibilityIssue Issues;
        public readonly TextStudioOwnershipMode OwnershipMode;
        public readonly string OwnershipLabel;
        public readonly string Summary;

        /// <summary>Creates a visibility report with issue flags, ownership state, summary, and suggested action.</summary>
        public TextStudioVisibilityReport(
            TextStudioVisibilityIssue issues,
            TextStudioOwnershipMode ownershipMode,
            string ownershipLabel,
            string summary)
        {
            Issues = issues;
            OwnershipMode = ownershipMode;
            OwnershipLabel = ownershipLabel ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public bool HasBlockingIssue =>
            HasIssue(TextStudioVisibilityIssue.NoTarget) ||
            HasIssue(TextStudioVisibilityIssue.InactiveGameObject) ||
            HasIssue(TextStudioVisibilityIssue.DisabledComponent) ||
            HasIssue(TextStudioVisibilityIssue.EmptyMarkup) ||
            HasIssue(TextStudioVisibilityIssue.NoRenderer) ||
            HasIssue(TextStudioVisibilityIssue.MissingTmpText);

        /// <summary>Reports whether a specific visibility issue flag is present.</summary>
        public bool HasIssue(TextStudioVisibilityIssue issue)
        {
            return (Issues & issue) != 0;
        }
    }

    /// <summary>Evaluates whether a Text Studio host has content, a valid target, layout, glyphs, and visible output.</summary>
    public static class TextStudioVisibilityDiagnostics
    {
        /// <summary>Captures current visibility readiness without mutating the target.</summary>
        public static TextStudioVisibilityReport Evaluate(TextStudioText hub)
        {
            if (hub == null)
            {
                return new TextStudioVisibilityReport(
                    TextStudioVisibilityIssue.NoTarget | TextStudioVisibilityIssue.NoRenderer,
                    TextStudioOwnershipMode.NoRenderer,
                    GetOwnershipLabel(TextStudioOwnershipMode.NoRenderer),
                    "No Text Studio target is selected.");
            }

            TextStudioOwnershipMode ownershipMode = EvaluateOwnership(hub);
            TextStudioVisibilityIssue issues = TextStudioVisibilityIssue.None;

            if (!hub.gameObject.activeInHierarchy)
                issues |= TextStudioVisibilityIssue.InactiveGameObject;
            if (!hub.enabled)
                issues |= TextStudioVisibilityIssue.DisabledComponent;
            if (string.IsNullOrWhiteSpace(hub.Markup))
                issues |= TextStudioVisibilityIssue.EmptyMarkup;
            if (!HasBackend(hub) && !HasRenderer(hub))
                issues |= TextStudioVisibilityIssue.NoRenderer;
            if (hub.TotalGlyphCount > 0 && hub.RevealedGlyphCount == 0 && (hub.IsRevealing || !hub.GetRevealDebugSnapshot().IsComplete))
                issues |= TextStudioVisibilityIssue.ZeroRevealedGlyphs;
            if (hub.IsRevealPaused)
                issues |= TextStudioVisibilityIssue.RevealPaused;
            if (hub.HasExternalTextSource)
                issues |= TextStudioVisibilityIssue.ExternalSourceOwnsText;

            return new TextStudioVisibilityReport(
                issues,
                ownershipMode,
                GetOwnershipLabel(ownershipMode),
                BuildSummary(issues, ownershipMode));
        }

        static TextStudioOwnershipMode EvaluateOwnership(TextStudioText hub)
        {
            bool hasTmp = HasBackend(hub);
            bool hasRenderers = HasRenderer(hub);

            if (hub.HasExternalTextSource)
                return TextStudioOwnershipMode.ExternalSource;
            if (!hasTmp && !hasRenderers)
                return TextStudioOwnershipMode.NoRenderer;
            if (hasTmp && hasRenderers)
                return TextStudioOwnershipMode.RendererMix;
            if (hub.Backends.IsDrivingOutput || MarkupRequiresTextStudioOwnership(hub.Markup))
                return TextStudioOwnershipMode.Drive;
            if (hasTmp)
                return TextStudioOwnershipMode.Mirror;

            return TextStudioOwnershipMode.Renderer;
        }

        static bool MarkupRequiresTextStudioOwnership(string markup)
        {
            if (string.IsNullOrEmpty(markup))
                return false;

            TextStudioDiagnosticReport report = TextStudioMarkupDiagnostics.Analyze(markup);
            for (int i = 0; i < report.Tags.Length; i++)
            {
                TextStudioTagKind kind = report.Tags[i].Kind;
                if (kind != TextStudioTagKind.NativeRichText && kind != TextStudioTagKind.Unknown)
                    return true;
            }

            return false;
        }

        static bool HasBackend(TextStudioText hub)
        {
            return hub.Backends.ActiveBackend != null
                ? hub.Backends.ActiveBackend.IsAvailable
                : hub.TMPText != null && hub.TMPText.isActiveAndEnabled;
        }

        static bool HasRenderer(TextStudioText hub)
        {
            for (int i = 0; i < hub.Renderers.Count; i++)
                if (TextStudioCapabilityReport.IsActive(hub.Renderers[i]) && hub.Renderers[i].IsAvailable)
                    return true;
            return false;
        }

        static string GetOwnershipLabel(TextStudioOwnershipMode mode)
        {
            switch (mode)
            {
                case TextStudioOwnershipMode.Mirror:
                    return "Mirror";
                case TextStudioOwnershipMode.Drive:
                    return "Drive";
                case TextStudioOwnershipMode.ExternalSource:
                    return "External Source";
                case TextStudioOwnershipMode.Renderer:
                    return "Renderer";
                case TextStudioOwnershipMode.RendererMix:
                    return "Renderer Mix";
                case TextStudioOwnershipMode.NoRenderer:
                default:
                    return "No Renderer";
            }
        }

        static string BuildSummary(TextStudioVisibilityIssue issues, TextStudioOwnershipMode ownershipMode)
        {
            if ((issues & TextStudioVisibilityIssue.NoTarget) != 0)
                return "No Text Studio target is selected.";
            if ((issues & TextStudioVisibilityIssue.InactiveGameObject) != 0)
                return "The GameObject is inactive.";
            if ((issues & TextStudioVisibilityIssue.DisabledComponent) != 0)
                return "The Text Studio component is disabled.";
            if ((issues & TextStudioVisibilityIssue.EmptyMarkup) != 0)
                return "Source text is empty.";
            if ((issues & TextStudioVisibilityIssue.NoRenderer) != 0)
                return "No enabled text backend or Text Studio renderer is ready.";
            if ((issues & TextStudioVisibilityIssue.ZeroRevealedGlyphs) != 0)
                return "Reveal has not exposed any glyphs yet.";
            if ((issues & TextStudioVisibilityIssue.RevealPaused) != 0)
                return "Reveal is paused.";
            if ((issues & TextStudioVisibilityIssue.ExternalSourceOwnsText) != 0)
                return "An external source owns the current text.";

            switch (ownershipMode)
            {
                case TextStudioOwnershipMode.Mirror:
                    return "The text backend is the editable text source.";
                case TextStudioOwnershipMode.Drive:
                    return "Text Studio is rendering the compiled text preview.";
                case TextStudioOwnershipMode.Renderer:
                    return "Text Studio is rendering through the configured renderer.";
                case TextStudioOwnershipMode.RendererMix:
                    return "Text Studio is rendering through a text backend and additional renderers.";
                case TextStudioOwnershipMode.ExternalSource:
                    return "An external source owns the current text.";
                case TextStudioOwnershipMode.NoRenderer:
                default:
                    return "No enabled text backend or Text Studio renderer is ready.";
            }
        }
    }
}
