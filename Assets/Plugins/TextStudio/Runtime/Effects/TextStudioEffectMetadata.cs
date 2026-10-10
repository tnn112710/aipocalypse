using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TextStudio
{
    /// <summary>Defines whether authored playback controls an effect's strength or its spatial progress.</summary>
    public enum TextStudioEffectPlaybackRole
    {
        Envelope,
        Progress
    }

    /// <summary>Immutable discovery and parameter metadata for one effect definition.</summary>
    public sealed class TextStudioEffectMetadata
    {
        readonly ReadOnlyCollection<TextStudioTagParameterDefinition> _parameters;

        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string MarkupSnippet { get; }
        public TextStudioPhaseMask SuggestedPhases { get; }
        public TextStudioEffectPlaybackRole PlaybackRole { get; }
        public TextStudioEffectPlaybackPreset DefaultPlayback { get; }
        public TextStudioEffectCurvePreset DefaultCurve { get; }
        public float DefaultPlaybackDuration { get; }
        public float DefaultDelayBetweenCycles { get; }
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;

        /// <summary>Creates immutable authoring metadata in the supplied parameter display order.</summary>
        public TextStudioEffectMetadata(
            string category,
            string displayName,
            string description,
            string markupSnippet,
            TextStudioPhaseMask suggestedPhases,
            IReadOnlyList<TextStudioTagParameterDefinition> parameters = null,
            TextStudioEffectPlaybackRole playbackRole = TextStudioEffectPlaybackRole.Envelope,
            TextStudioEffectPlaybackPreset defaultPlayback = TextStudioEffectPlaybackPreset.External,
            TextStudioEffectCurvePreset defaultCurve = TextStudioEffectCurvePreset.Hold,
            float defaultPlaybackDuration = 1f,
            float defaultDelayBetweenCycles = 0f)
        {
            Category = string.IsNullOrWhiteSpace(category) ? TextStudioTagCategories.Effects : category.Trim();
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            MarkupSnippet = markupSnippet ?? string.Empty;
            SuggestedPhases = suggestedPhases == TextStudioPhaseMask.None
                ? TextStudioPhaseMask.Active
                : suggestedPhases;
            PlaybackRole = playbackRole;
            DefaultPlayback = defaultPlayback;
            DefaultCurve = defaultCurve;
            DefaultPlaybackDuration = Math.Max(0.0001f, defaultPlaybackDuration);
            DefaultDelayBetweenCycles = Math.Max(0f, defaultDelayBetweenCycles);

            if (parameters == null || parameters.Count == 0)
            {
                _parameters = Array.AsReadOnly(Array.Empty<TextStudioTagParameterDefinition>());
                return;
            }

            TextStudioTagParameterDefinition[] copy =
                new TextStudioTagParameterDefinition[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
                copy[i] = parameters[i];
            _parameters = Array.AsReadOnly(copy);
        }
    }

    /// <summary>Publishes effect identity, documentation, phase suggestions, and typed parameters.</summary>
    public interface ITextStudioEffectMetadataProvider
    {
        TextStudioEffectMetadata Metadata { get; }
    }

    internal static class TextStudioEffectParameterOwnership
    {
        internal static bool IsOwnedByProgressLayer(
            TextStudioEffectMetadata metadata,
            string parameterName)
        {
            return metadata?.PlaybackRole == TextStudioEffectPlaybackRole.Progress &&
                   (string.Equals(parameterName, "playback", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parameterName, "curve", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parameterName, "speed", StringComparison.OrdinalIgnoreCase));
        }
    }

    internal static class TextStudioEffectMetadataResolver
    {
        internal static TextStudioEffectMetadata Get(ITextStudioEffectDefinition definition)
        {
            if (definition is ITextStudioEffectMetadataProvider provider && provider.Metadata != null)
                return provider.Metadata;

            string id = definition != null ? definition.Id : string.Empty;
            return new TextStudioEffectMetadata(
                TextStudioTagCategories.Effects,
                id,
                string.IsNullOrEmpty(id) ? string.Empty : "Applies the " + id + " Text Studio effect.",
                string.IsNullOrEmpty(id) ? string.Empty : "<" + id + ">",
                TextStudioPhaseMask.Active);
        }
    }
}
