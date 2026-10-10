using System;

namespace TextStudio
{
    public sealed partial class TextStudioBinding
    {
        /// <summary>Collects the active UI Toolkit strategy and its actual fallback behavior.</summary>
        public void CollectCapabilities(TextStudioCapabilityReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.Clear(MotionPolicy);
            report.BackendName = "UI Toolkit";
            report.BackendCapabilities = TextStudioTextBackendCapabilities.RichText;
            report.CanOverlapTextChanges = _runtime.ReactionsRequireOutgoingCapture ||
                !UnityEngine.Application.isPlaying &&
                (_behaviorProfile != null && _behaviorProfile.Provides(TextStudioBehaviorDomain.Reactions)
                    ? _behaviorProfile.Reactions : _reactions).HasOutgoingCaptureLayer();
            bool full = _strategy != null && _strategy.FeatureSet == TextStudioUITKFeatureSet.FullParity;
            if (full) report.BackendCapabilities |= TextStudioTextBackendCapabilities.TextStudioVertexAnimation;
            report.AddOutput(new TextStudioOutputCapabilities(full ? "UI Toolkit Glyphs" : "UI Toolkit Basic",
                _element != null && _isPanelAttached, true, full ? TextStudioRenderChannels.All & ~TextStudioRenderChannels.UvOffset : TextStudioRenderChannels.None,
                TextStudioRenderDomain.FlatText), _runtime.Plan);
            if (!full)
                report.Add(TextStudioPreviewDiagnosticSeverity.Information, "Basic UI Toolkit rendering supports text reveal without per-glyph mesh effects.",
                    "Use Unity 6000.2 or newer with a compatible text generator for per-glyph animation.");
            if (_textGeneratorPreference == TextStudioUITKTextGeneratorPreference.PreferAdvanced &&
                TextStudioUITKCapabilities.GetAdvancedTextAvailability(_element?.panel) == false)
                report.Add(TextStudioPreviewDiagnosticSeverity.Warning,
                    "Advanced Text is disabled for this panel; Standard rendering is active.",
                    "Enable Advanced Text in Project Settings > UI Toolkit, then reselect the generator.");
            if (_diagnostic.IsError)
                report.Add(TextStudioPreviewDiagnosticSeverity.Error, _diagnostic.Message, "Review the binding target and source settings.");
            report.AddOverlapFallback(TextChangeMode);
        }
    }
}
