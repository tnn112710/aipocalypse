using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Read-only reveal information exposed to character-substitution extensions.
    /// </summary>
    public interface ITextStudioRevealState
    {
        /// <summary>Reports whether a visible glyph has crossed its reveal boundary.</summary>
        /// <param name="glyphIndex">Zero-based visible-glyph index.</param>
        /// <returns><see langword="false"/> for an invalid index.</returns>
        bool IsGlyphRevealed(int glyphIndex);

        /// <summary>Estimates when a visible glyph becomes revealed under current timing.</summary>
        /// <param name="glyphIndex">Zero-based visible-glyph index.</param>
        /// <returns>Approximate seconds from reveal start, or 0 for an invalid index.</returns>
        float GetApproximateRevealTime(int glyphIndex);

        /// <summary>Seconds since this glyph actually appeared, preserving age across retained text changes.</summary>
        float GetGlyphRevealElapsedTime(int glyphIndex);
    }

    /// <summary>Defines a registered markup-driven transformation of presentation glyphs.</summary>
    public interface ITextStudioCharacterSubstitution
    {
        string Id { get; }
        TextStudioCharacterSubstitutionDefinition Definition { get; }
        /// <summary>Normalizes authored parameters into the substitution's canonical form.</summary>
        TextStudioTagParameters CanonicalizeParameters(in TextStudioTagParameters parameters);

        /// <summary>Creates isolated mutable state for one host and compiled binding.</summary>
        ITextStudioCharacterSubstitutionRuntime CreateRuntime();

        /// <summary>Calculates how long the substitution remains active after its glyphs are revealed.</summary>
        /// <returns>Duration in seconds, or 0 when no completion delay is required.</returns>
        float GetCompletionDuration(
            TextStudioPlanView plan,
            in CharacterSubstitutionBinding binding,
            ITextStudioRevealState reveal);
    }

    /// <summary>Per-host runtime instance that updates one character-substitution binding.</summary>
    public interface ITextStudioCharacterSubstitutionRuntime
    {
        /// <summary>Resets runtime state after the compiled semantic plan changes.</summary>
        void OnTextChanged(TextStudioPlanView plan);

        /// <summary>Updates presentation glyphs for the current frame.</summary>
        /// <returns>Whether glyph output changed and whether further frame updates are required.</returns>
        TextStudioCharacterSubstitutionResult Update(in TextStudioCharacterSubstitutionContext context);
    }

    /// <summary>Metadata, aliases, examples, and parameter schema for one substitution tag.</summary>
    public sealed class TextStudioCharacterSubstitutionDefinition
    {
        readonly string[] _aliases;
        readonly string[] _examples;
        readonly TextStudioTagParameterDefinition[] _parameters;

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> Examples => _examples;
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;

        /// <summary>Creates a discoverable substitution definition with defensive empty defaults.</summary>
        public TextStudioCharacterSubstitutionDefinition(
            string id,
            string displayName = null,
            string description = null,
            string[] aliases = null,
            string[] examples = null,
            TextStudioTagParameterDefinition[] parameters = null)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            Description = description ?? string.Empty;
            _aliases = aliases ?? Array.Empty<string>();
            _examples = examples ?? Array.Empty<string>();
            _parameters = parameters ?? Array.Empty<TextStudioTagParameterDefinition>();
        }
    }

    /// <summary>Transient update context provided to a character-substitution runtime.</summary>
    public readonly struct TextStudioCharacterSubstitutionContext
    {
        public readonly TextStudioPlanView Plan;
        public readonly CharacterSubstitutionBinding Binding;
        public readonly TextStudioPresentationGlyphWriter Glyphs;
        public readonly int GlyphCount;
        public readonly float DeltaTime;
        public readonly float Time;
        public readonly bool IsDiscontinuous;
        public readonly ITextStudioRevealState Reveal;

        internal TextStudioCharacterSubstitutionContext(
            TextStudioCompiledPlan plan,
            in CharacterSubstitutionBinding binding,
            PresentationGlyph[] glyphs,
            int glyphCount,
            float deltaTime,
            float time,
            bool isDiscontinuous,
            ITextStudioRevealState reveal)
        {
            Plan = new TextStudioPlanView(plan);
            Binding = binding;
            Glyphs = new TextStudioPresentationGlyphWriter(glyphs, glyphCount);
            GlyphCount = glyphCount;
            DeltaTime = deltaTime;
            Time = time;
            IsDiscontinuous = isDiscontinuous;
            Reveal = reveal;
        }
    }

    /// <summary>Reports the output and scheduling consequences of one substitution update.</summary>
    public readonly struct TextStudioCharacterSubstitutionResult
    {
        public readonly bool Changed;
        public readonly bool HasActiveSubstitutions;

        /// <summary>Creates an update result.</summary>
        public TextStudioCharacterSubstitutionResult(bool changed, bool hasActiveSubstitutions)
        {
            Changed = changed;
            HasActiveSubstitutions = hasActiveSubstitutions;
        }
    }
}
