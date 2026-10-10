using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One output's advertised capabilities. Unknown channels are never treated as unsupported.</summary>
    public readonly struct TextStudioOutputCapabilities
    {
        public readonly string Name;
        public readonly bool Available;
        public readonly bool ChannelsKnown;
        public readonly TextStudioRenderChannels Channels;
        public readonly TextStudioRenderDomain Domain;

        /// <summary>Creates an output capability snapshot with its availability, known channels, and rendering domain.</summary>
        public TextStudioOutputCapabilities(string name, bool available, bool channelsKnown,
            TextStudioRenderChannels channels, TextStudioRenderDomain domain)
        {
            Name = name ?? string.Empty;
            Available = available;
            ChannelsKnown = channelsKnown;
            Channels = channels;
            Domain = domain;
        }
    }

    /// <summary>Optional add-on diagnostics collected on demand, without dependencies from the base package.</summary>
    public interface ITextStudioCapabilityReportProvider
    {
        /// <summary>Appends this provider's current configuration and capability diagnostics to the caller-owned list.</summary>
        void CollectCapabilityDiagnostics(List<TextStudioPreviewDiagnostic> diagnostics);
    }

    /// <summary>Reusable, caller-owned capability snapshot. Collect again after configuration or topology changes.</summary>
    public sealed class TextStudioCapabilityReport
    {
        readonly List<TextStudioOutputCapabilities> _outputs = new List<TextStudioOutputCapabilities>(4);
        readonly List<TextStudioPreviewDiagnostic> _diagnostics = new List<TextStudioPreviewDiagnostic>(8);
        internal readonly List<MonoBehaviour> Components = new List<MonoBehaviour>(8);

        public string BackendName { get; internal set; } = string.Empty;
        public TextStudioTextBackendCapabilities BackendCapabilities { get; internal set; }
        public TextStudioMotionPolicy EffectiveMotionPolicy { get; internal set; }
        /// <summary>Whether an outgoing handoff path is configured. A particular capture may still fail.</summary>
        public bool CanOverlapTextChanges { get; internal set; }
        public IReadOnlyList<TextStudioOutputCapabilities> Outputs => _outputs;
        public IReadOnlyList<TextStudioPreviewDiagnostic> Diagnostics => _diagnostics;
        internal List<TextStudioPreviewDiagnostic> DiagnosticBuffer => _diagnostics;

        internal void Clear(TextStudioMotionPolicy policy)
        {
            BackendName = string.Empty;
            BackendCapabilities = TextStudioTextBackendCapabilities.None;
            EffectiveMotionPolicy = TextStudioMotionSettings.Resolve(policy);
            CanOverlapTextChanges = false;
            _outputs.Clear();
            _diagnostics.Clear();
            Components.Clear();
        }

        internal void AddOutput(in TextStudioOutputCapabilities output, TextStudioCompiledPlan plan)
        {
            _outputs.Add(output);
            if (!output.Available)
            {
                Add(TextStudioPreviewDiagnosticSeverity.Warning, output.Name + " is not ready.",
                    "Enable the output and complete its setup.");
                return;
            }
            if (plan == null)
                return;
            TextStudioRenderChannels missing = TextStudioRenderChannels.None;
            bool wrongDomain = false;
            for (int i = 0; i < plan.EffectBindingCount; i++)
            {
                ref EffectBinding binding = ref plan.EffectBindings[i];
                ITextStudioEffectCapabilitiesProvider provider = binding.effect as ITextStudioEffectCapabilitiesProvider;
                if (provider == null && TextStudioEffects.TryGetDefinition(binding.effectId, out ITextStudioEffectDefinition definition))
                    provider = definition as ITextStudioEffectCapabilitiesProvider;
                if (provider == null) continue;
                TextStudioEffectCapabilities capabilities = provider.Capabilities;
                if ((capabilities.SupportedRenderDomains & output.Domain) == 0)
                    wrongDomain = true;
                else if (output.ChannelsKnown)
                    missing |= capabilities.RenderChannels & ~output.Channels;
            }
            if (missing != TextStudioRenderChannels.None)
                Add(TextStudioPreviewDiagnosticSeverity.Warning, output.Name + " cannot display these effect channels: " + missing + ".",
                    "Choose effects supported by this output or select a renderer that implements these channels.");
            if (wrongDomain)
                Add(TextStudioPreviewDiagnosticSeverity.Warning, "Some effects target a different render domain than " + output.Name + ".",
                    "Select effects for this output's 2D or 3D domain.");
        }

        internal void Add(TextStudioPreviewDiagnosticSeverity severity, string message, string action = "")
        {
            _diagnostics.Add(new TextStudioPreviewDiagnostic(severity, message, action));
        }

        internal void AddOverlapFallback(TextStudioTextChangeMode mode)
        {
            if (mode == TextStudioTextChangeMode.OverlapHandoff && !CanOverlapTextChanges)
                Add(TextStudioPreviewDiagnosticSeverity.Information,
                    "Overlap has no outgoing capture path; text hides before replacement.",
                    "Assign a qualified outgoing Reaction or renderer handoff provider, or select Hide Then Reveal.");
        }

        internal static bool IsActive(object provider)
        {
            return provider != null && (provider is not UnityEngine.Object unityObject || unityObject != null) &&
                   (provider is not Behaviour behaviour || behaviour.isActiveAndEnabled);
        }
    }
}
