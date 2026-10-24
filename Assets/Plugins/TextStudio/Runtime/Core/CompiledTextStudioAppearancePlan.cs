using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>A half-open semantic range. Mapping domains remain intact when visible ranges acquire holes.</summary>
    public readonly struct TextStudioAppearanceRange
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length;
        /// <summary>Creates a half-open semantic range from its starting unit and length.</summary>
        public TextStudioAppearanceRange(int start, int length) { Start = start; Length = length; }
        /// <summary>Returns whether the supplied unit lies within this half-open semantic range.</summary>
        public bool Contains(int unit) => unit >= Start && unit < End;
    }

    /// <summary>Reports whether a requested appearance binding resolved to an active definition or is missing, disabled, or invalid.</summary>
    public enum TextStudioAppearanceResolution : byte { Active, Missing, Disabled, Invalid }

    /// <summary>One source invocation. Its immutable program can be shared with other invocations.</summary>
    public sealed class TextStudioAppearanceOccurrence
    {
        readonly TextStudioAppearanceRange[] _visibleRanges;
        IReadOnlyList<TextStudioAppearanceRange> _visibleView;
        public string Id { get; }
        public int OccurrenceId { get; }
        public int ParentIndex { get; }
        public int ProgramIndex { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        public TextStudioAppearanceRange Domain { get; }
        public TextStudioAppearanceApplyMode ApplyMode { get; }
        public TextStudioAppearanceResolution Resolution { get; }
        public Color Tint { get; }
        public float Opacity { get; }
        // Created on first use: compiling a plan for every text change only reads the count.
        public IReadOnlyList<TextStudioAppearanceRange> VisibleRanges => _visibleView ??= Array.AsReadOnly(_visibleRanges);
        internal int VisibleRangeCount => _visibleRanges.Length;
        public bool IsActive => Resolution == TextStudioAppearanceResolution.Active;
        internal TextStudioAppearanceDefinition Definition { get; }
        internal TextStudioAppearanceDefinitionTable LocalDefinitions { get; }

        internal TextStudioAppearanceOccurrence(string id, int occurrenceId, int parent, int program,
            int sourceStart, int sourceLength, TextStudioAppearanceRange domain, TextStudioAppearanceApplyMode mode,
            TextStudioAppearanceResolution resolution, Color tint, float opacity, TextStudioAppearanceDefinition definition,
            TextStudioAppearanceDefinitionTable localDefinitions, TextStudioAppearanceRange[] visibleRanges)
        {
            Id = id ?? string.Empty; OccurrenceId = occurrenceId; ParentIndex = parent; ProgramIndex = program;
            SourceStart = sourceStart; SourceLength = sourceLength; Domain = domain; ApplyMode = mode;
            Resolution = resolution; Tint = tint; Opacity = opacity; Definition = definition;
            LocalDefinitions = localDefinitions;
            _visibleRanges = visibleRanges ?? Array.Empty<TextStudioAppearanceRange>();
        }

        /// <summary>Returns whether the supplied unit belongs to any visible range of this appearance occurrence.</summary>
        public bool ContainsVisibleUnit(int unit)
        {
            int low = 0, high = _visibleRanges.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                TextStudioAppearanceRange range = _visibleRanges[middle];
                if (unit < range.Start) high = middle - 1;
                else if (unit >= range.End) low = middle + 1;
                else return true;
            }
            return false;
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            return memory.Text(Id) + memory.Array(_visibleRanges) +
                (Definition?.CaptureManagedPayload(memory) ?? 0) + (LocalDefinitions?.CaptureManagedPayload(memory) ?? 0);
        }
    }

    /// <summary>A contextual appearance diagnostic; unresolved invocations remain inspectable.</summary>
    public readonly struct TextStudioAppearanceDiagnostic
    {
        public string Message { get; }
        public int OccurrenceId { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }
        internal TextStudioAppearanceDiagnostic(string message, int occurrence, int sourceStart, int sourceLength)
        { Message = message; OccurrenceId = occurrence; SourceStart = sourceStart; SourceLength = sourceLength; }
    }

    /// <summary>One persistent composition link, ordered from this contribution toward its backdrop.</summary>
    public readonly struct TextStudioAppearanceContribution
    {
        public int OccurrenceIndex { get; }
        public int BackdropIndex { get; }
        internal TextStudioAppearanceContribution(int occurrence, int backdrop)
        { OccurrenceIndex = occurrence; BackdropIndex = backdrop; }
    }

    /// <summary>Shared immutable programs and instance-owned source composition. Contains no rendering resources or clocks.</summary>
    public sealed class CompiledTextStudioAppearancePlan
    {
        readonly int[] _unitContributions;
        readonly TextStudioAppearanceContribution[] _contributions;
        readonly CompiledTextStudio2DStyle[] _programs;
        readonly TextStudioAppearanceOccurrence[] _occurrences;
        readonly TextStudioAppearanceDiagnostic[] _diagnostics;
        IReadOnlyList<CompiledTextStudio2DStyle> _programsView;
        IReadOnlyList<TextStudioAppearanceOccurrence> _occurrencesView;
        IReadOnlyList<TextStudioAppearanceDiagnostic> _diagnosticsView;
        public CompiledTextStudio2DStyle DefaultStyle => _programs[0];
        // Views are created on first use: plain text compiles a plan per change that nothing may enumerate.
        public IReadOnlyList<CompiledTextStudio2DStyle> Programs => _programsView ??= Array.AsReadOnly(_programs);
        public IReadOnlyList<TextStudioAppearanceOccurrence> Occurrences => _occurrencesView ??= Array.AsReadOnly(_occurrences);
        public IReadOnlyList<TextStudioAppearanceDiagnostic> Diagnostics => _diagnosticsView ??= Array.AsReadOnly(_diagnostics);
        public int UnitCount => _unitContributions.Length;
        public int ContributionCount => _contributions.Length;
        public bool HasInvocations => _occurrences.Length > 1;
        public bool IsPassThrough { get; }
        public bool IsValid { get; }
        public TextStudioStackFingerprint Fingerprint { get; }
        public ulong DependencyRevision { get; }
        internal uint SourceVersion { get; }
        // Validation cache only; catalog polling never changes the compiled composition or its dependencies.
        internal uint CatalogRevision { get; private set; }
        readonly Dictionary<string, TextStudioAppearanceDefinition> _catalogDefinitions;
        internal TextStudioCompiledPlan SourceIdentity { get; }
        internal IReadOnlyDictionary<string, CompiledTextStudio2DStyle> Variants { get; }
        internal IReadOnlyDictionary<string, string[]> VariantDiagnostics { get; }
        internal Dictionary<string, TextStudio2DStyleSettings> VariantSettings { get; }
        internal TextStudio2DStyleSettings[] DependencySettings { get; }
        internal TextStudio2DStyleAsset[] Dependencies { get; }
        internal TextStudioAppearanceTagIndex TagIndex { get; }
        /// <summary>Currency checks read tag styles, variant settings or catalog bindings, not only the default style.</summary>
        internal bool HasPolledDependencies => Dependencies.Length > 0 || DependencySettings.Length > 0 || _catalogDefinitions.Count > 0;

        // Sources without appearance spans index no names, so their plans share one immutable empty index.
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly TextStudioAppearanceTagIndex NoTags = new TextStudioAppearanceTagIndex(null);

        internal CompiledTextStudioAppearancePlan(TextStudioCompiledPlan source, uint catalogRevision,
            CompiledTextStudio2DStyle[] programs, TextStudioAppearanceOccurrence[] occurrences,
            TextStudioAppearanceContribution[] contributions, int[] unitContributions,
            TextStudioAppearanceDiagnostic[] diagnostics, ulong dependencyRevision,
            Dictionary<string, CompiledTextStudio2DStyle> variants, TextStudio2DStyleAsset[] dependencies,
            Dictionary<string, string[]> variantDiagnostics, Dictionary<string, TextStudio2DStyleSettings> variantSettings,
            TextStudio2DStyleSettings[] dependencySettings, Dictionary<string, TextStudioAppearanceDefinition> catalogDefinitions)
        {
            SourceIdentity = source; SourceVersion = source?.SourceVersion ?? 0; CatalogRevision = catalogRevision;
            _programs = programs; _occurrences = occurrences; _contributions = contributions;
            _unitContributions = unitContributions; _diagnostics = diagnostics;
            DependencyRevision = dependencyRevision;
            Variants = variants; Dependencies = dependencies;
            VariantDiagnostics = variantDiagnostics;
            VariantSettings = variantSettings;
            DependencySettings = dependencySettings;
            _catalogDefinitions = catalogDefinitions;
            TagIndex = source != null && source.AppearanceSpanCount > 0 ? new TextStudioAppearanceTagIndex(source) : NoTags;
            IsValid = true; IsPassThrough = true;
            TextStudioDeterministicHash64 topology = default, parameters = default, state = default, bounds = default, complete = default;
            foreach (TextStudioAppearanceOccurrence occurrence in occurrences)
            {
                // Program-table indices describe sharing, not the occurrence's appearance.
                // A sibling paint variant can repartition that table without changing our topology or bounds.
                topology.Add(occurrence.ParentIndex);
                topology.Add(occurrence.Domain.Start); topology.Add(occurrence.Domain.Length);
                topology.Add((byte)occurrence.ApplyMode); topology.Add((byte)occurrence.Resolution);
                parameters.Add(occurrence.Tint.r); parameters.Add(occurrence.Tint.g);
                parameters.Add(occurrence.Tint.b); parameters.Add(occurrence.Tint.a); parameters.Add(occurrence.Opacity);
                if (occurrence.IsActive)
                {
                    TextStudioStackFingerprint effective = programs[occurrence.ProgramIndex].Fingerprint;
                    topology.Add(effective.Topology); parameters.Add(effective.Parameters);
                    state.Add(effective.RenderState); bounds.Add(effective.Bounds);
                }
                if (!occurrence.IsActive || occurrence.VisibleRangeCount == 0) continue;
                CompiledTextStudio2DStyle program = programs[occurrence.ProgramIndex];
                if (program.IsPassThrough && occurrence.ApplyMode == TextStudioAppearanceApplyMode.Overlay) continue;
                IsValid &= program.IsValid;
                IsPassThrough &= program.IsPassThrough && occurrence.Tint == Color.white && occurrence.Opacity == 1f;
            }
            complete.Add(topology.Value); complete.Add(parameters.Value); complete.Add(state.Value); complete.Add(bounds.Value);
            Fingerprint = new TextStudioStackFingerprint(topology.Value, parameters.Value, state.Value, bounds.Value, complete.Value);
        }

        /// <summary>Returns the contribution index for a semantic unit, or -1 if the unit is outside the plan.</summary>
        public int GetContributionIndex(int unit) => (uint)unit < (uint)_unitContributions.Length ? _unitContributions[unit] : -1;
        /// <summary>Returns the composition contribution at the specified index.</summary>
        public TextStudioAppearanceContribution GetContribution(int index) => _contributions[index];
        internal bool AreCatalogBindingsCurrent()
        {
            uint revision = TextStudio2DStyles.Revision;
            if (CatalogRevision == revision) return true;
            // Includes misses, but excludes names resolved in a nearer local scope.
            foreach (KeyValuePair<string, TextStudioAppearanceDefinition> binding in _catalogDefinitions)
            {
                if (!TextStudio2DStyles.TryGetDefinition(binding.Key, out TextStudio2DStyleAsset current)) current = null;
                if (!ReferenceEquals(binding.Value?.Asset, current)) return false;
            }
            CatalogRevision = revision;
            return true;
        }

        internal bool RequiresContinuousRenderingForPolicy(TextStudioMotionPolicy policy)
        {
            foreach (TextStudioAppearanceOccurrence occurrence in _occurrences)
                if (occurrence.IsActive && occurrence.VisibleRangeCount > 0 &&
                    _programs[occurrence.ProgramIndex].RequiresContinuousRenderingForPolicy(policy)) return true;
            return false;
        }

        internal long CaptureManagedPayload(TextStudioAppearanceManagedMemory memory)
        {
            long bytes = memory.Array(_unitContributions) + memory.Array(_contributions) +
                (long)(_programs.Length + _occurrences.Length) * IntPtr.Size + memory.Array(_diagnostics) + memory.Array(DependencySettings);
            foreach (TextStudioAppearanceOccurrence occurrence in _occurrences) bytes += occurrence.CaptureManagedPayload(memory);
            foreach (TextStudioAppearanceDiagnostic diagnostic in _diagnostics) bytes += memory.Text(diagnostic.Message);
            bytes += (long)_catalogDefinitions.Count * IntPtr.Size * 2;
            foreach (KeyValuePair<string, TextStudioAppearanceDefinition> binding in _catalogDefinitions)
                bytes += memory.Text(binding.Key) + (binding.Value?.CaptureManagedPayload(memory) ?? 0);
            foreach (KeyValuePair<string, TextStudio2DStyleSettings> variant in VariantSettings)
                bytes += memory.Text(variant.Key) + variant.Value.CaptureManagedPayload(memory);
            foreach (KeyValuePair<string, string[]> variant in VariantDiagnostics)
            {
                bytes += memory.Text(variant.Key);
                if (memory.Visit(variant.Value)) bytes += (long)variant.Value.Length * IntPtr.Size;
                foreach (string message in variant.Value) bytes += memory.Text(message);
            }
            bytes += TagIndex.CaptureManagedPayload(memory);
            return bytes;
        }
    }
}
