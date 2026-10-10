using System;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        /// <summary>Collects current backend, output, motion, fallback, and add-on diagnostics without changing text.</summary>
        public void CollectCapabilities(TextStudioCapabilityReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.Clear(MotionPolicy);
            ITextStudioTextBackend backend = Backends.ActiveBackend;
            report.BackendName = backend?.DisplayName ?? "TextMeshPro";
            report.BackendCapabilities = backend?.Capabilities ??
                (TextStudioTextBackendCapabilities.RichText | TextStudioTextBackendCapabilities.SourceVertexColors |
                 TextStudioTextBackendCapabilities.TextStudioVertexAnimation);
            report.CanOverlapTextChanges = CanOverlapTextChanges;
            bool externalOutput = false;
            for (int i = 0; i < _renderers.Count; i++)
            {
                ITextStudioRenderer renderer = _renderers[i];
                if (!TextStudioCapabilityReport.IsActive(renderer)) continue;
                bool available = renderer.IsAvailable;
                externalOutput |= available;
                ITextStudioRendererChannelSupport channels = renderer as ITextStudioRendererChannelSupport;
                report.AddOutput(new TextStudioOutputCapabilities(renderer.GetType().Name, available,
                    channels != null, channels?.SupportedRenderChannels ?? TextStudioRenderChannels.None,
                    (renderer as ITextStudioRenderDomainProvider)?.RenderDomain ?? TextStudioRenderDomain.All), CompiledPlan);
            }
            bool backendAvailable = backend != null ? backend.IsAvailable : TMPText != null && TMPText.isActiveAndEnabled;
            if (backendAvailable && !ShouldSuppressTextBackendOutput(externalOutput))
            {
                ITextStudioTextBackendRenderChannelSupport channels = backend as ITextStudioTextBackendRenderChannelSupport;
                report.AddOutput(new TextStudioOutputCapabilities(report.BackendName, true, backend == null || channels != null,
                    backend == null ? TextStudioRenderChannels.All : channels?.SupportedRenderChannels ?? TextStudioRenderChannels.None,
                    TextStudioRenderDomain.FlatText), CompiledPlan);
            }
            if (!backendAvailable && !externalOutput)
                report.Add(TextStudioPreviewDiagnosticSeverity.Error, "No text output is ready.",
                    "Add or enable a text backend or renderer and configure its font or character set.");
            if ((_compiledAppearanceStyle != null && !_compiledAppearanceStyle.IsPassThrough) && !SupportsActiveTextStudio2DAppearance)
                report.Add(TextStudioPreviewDiagnosticSeverity.Warning, "The selected backend cannot display 2D Appearance.",
                    "Use a backend that supports 2D Appearance or choose native styling for this output.");
            if (!string.IsNullOrEmpty(MaterialCompatibilityWarning))
                report.Add(TextStudioPreviewDiagnosticSeverity.Warning, MaterialCompatibilityWarning, "Review the assigned text material.");
            if (!string.IsNullOrEmpty(AppearanceQualityHint))
                report.Add(TextStudioPreviewDiagnosticSeverity.Information, AppearanceQualityHint);
            report.AddOverlapFallback(TextChangeMode);
            GetComponents(report.Components);
            try
            {
                for (int i = 0; i < report.Components.Count; i++)
                {
                    UnityEngine.MonoBehaviour component = report.Components[i];
                    if (!TextStudioCapabilityReport.IsActive(component)) continue;
                    if (component is ITextStudioCapabilityReportProvider provider)
                        provider.CollectCapabilityDiagnostics(report.DiagnosticBuffer);
                    else if (component is ITextStudioPreviewStageProvider preview)
                        preview.CollectPreviewDiagnostics(report.DiagnosticBuffer);
                }
            }
            finally { report.Components.Clear(); }
        }
    }
}
