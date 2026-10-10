using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Resolves source scopes independently of layout and GPU resource ownership.</summary>
    internal static class TextStudioAppearancePlanCompiler
    {
        internal static bool IsCurrent(CompiledTextStudioAppearancePlan plan, TextStudioCompiledPlan source,
            CompiledTextStudio2DStyle defaultStyle)
        {
            return plan != null && ReferenceEquals(plan.SourceIdentity, source) &&
                plan.SourceVersion == (source?.SourceVersion ?? 0) &&
                ReferenceEquals(plan.DefaultStyle, defaultStyle) && plan.AreCatalogBindingsCurrent() &&
                plan.DependencyRevision == DependencyRevision(defaultStyle, plan.Dependencies, plan.DependencySettings);
        }

        internal static CompiledTextStudioAppearancePlan Compile(TextStudioCompiledPlan source,
            CompiledTextStudio2DStyle defaultStyle, in TextStudio2DStyleSettings defaultSettings,
            TextStudio2DStyleAsset defaultAsset, CompiledTextStudioAppearancePlan previous = null)
        {
            defaultStyle ??= TextStudio2DStyleCompiler.Compile((TextStudio2DStyleAsset)null, TextStudio2DCompileContext.Full);
            int count = source?.AppearanceSpanCount ?? 0;
            int units = source?.VisibleGlyphCount ?? 0;
            if (count == 0) return CompileDefaultOnly(source, defaultStyle, units);
            List<CompiledTextStudio2DStyle> programs = new List<CompiledTextStudio2DStyle> { defaultStyle };
            List<TextStudioAppearanceDiagnostic> diagnostics = new List<TextStudioAppearanceDiagnostic>();
            List<TextStudio2DStyleAsset> dependencies = new List<TextStudio2DStyleAsset>();
            Dictionary<string, CompiledTextStudio2DStyle> variants = new Dictionary<string, CompiledTextStudio2DStyle>(StringComparer.Ordinal);
            Dictionary<string, string[]> variantDiagnostics = new Dictionary<string, string[]>(StringComparer.Ordinal);
            Dictionary<string, TextStudio2DStyleSettings> variantSettings = new Dictionary<string, TextStudio2DStyleSettings>(StringComparer.Ordinal);
            Dictionary<string, TextStudioAppearanceDefinition> globals = new Dictionary<string, TextStudioAppearanceDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TextStudioAppearanceDefinitionTable> localTables = new Dictionary<string, TextStudioAppearanceDefinitionTable>(StringComparer.Ordinal);
            Dictionary<CompiledTextStudio2DStyle, int> programIndices = new Dictionary<CompiledTextStudio2DStyle, int> { [defaultStyle] = 0 };
            Dictionary<TextStudioStackFingerprint, CompiledTextStudio2DStyle> effectivePrograms = new Dictionary<TextStudioStackFingerprint, CompiledTextStudio2DStyle> { [defaultStyle.Fingerprint] = defaultStyle };
            Dictionary<CompiledTextStudio2DStyle, TextStudio2DStyleSettings> dependencySettings = new Dictionary<CompiledTextStudio2DStyle, TextStudio2DStyleSettings>();
            Draft[] drafts = new Draft[count + 1];
            TextStudioAppearanceDefinitionTable rootDefinitions = count > 0 ? TextStudioAppearanceDefinitions.Resolve(defaultSettings, defaultAsset) : null;
            if (rootDefinitions != null)
                foreach (string message in rootDefinitions.Diagnostics)
                    diagnostics.Add(new TextStudioAppearanceDiagnostic(message, -1, 0, 0));
            drafts[0] = new Draft { Id = string.Empty, OccurrenceId = -1, Parent = -1, Program = 0,
                Domain = new TextStudioAppearanceRange(0, units), Local = rootDefinitions, Mode = TextStudioAppearanceApplyMode.Replace };
            List<string> parameterDiagnostics = new List<string>();
            for (int index = 0; index < count; index++)
            {
                TextStudioAppearanceSpan span = source.AppearanceSpans[index];
                int parent = span.ParentIndex >= 0 && span.ParentIndex < index ? span.ParentIndex + 1 : 0;
                int start = Mathf.Clamp(span.Start, 0, units);
                int end = (int)Math.Min(units, Math.Max(start, (long)span.Start + span.Length));
                Draft draft = new Draft { Id = span.Id, OccurrenceId = span.OccurrenceId, Parent = parent,
                    SourceStart = span.SourceStart, SourceLength = span.SourceLength,
                    Domain = new TextStudioAppearanceRange(start, end - start), Program = -1 };
                drafts[index + 1] = draft;
                TextStudioAppearanceDefinition definition = null;
                if (!span.IsAdHoc)
                {
                    for (int scope = parent; scope >= 0; scope = drafts[scope].Parent)
                        if (drafts[scope].Local != null && drafts[scope].Local.TryGet(span.Id, out definition)) break;
                    if (definition == null)
                    {
                        if (!globals.TryGetValue(span.Id, out definition))
                        {
                            if (TextStudio2DStyles.TryGetDefinition(span.Id, out TextStudio2DStyleAsset asset))
                                definition = TextStudioAppearanceDefinitions.FromAsset(asset);
                            globals.Add(span.Id, definition);
                        }
                    }
                    draft.Definition = definition;
                    if (definition == null)
                    {
                        draft.Resolution = TextStudioAppearanceResolution.Missing;
                        AddDiagnostic(diagnostics, draft, "Appearance '" + span.Id + "' is not defined in this scope.");
                        continue;
                    }
                    AddDependency(dependencies, definition.Asset);
                    AddDependency(dependencies, definition.OwnerAsset);
                    if (HasReferenceCycle(definition, parent, drafts))
                    {
                        draft.Resolution = TextStudioAppearanceResolution.Invalid;
                        AddDiagnostic(diagnostics, draft, "Appearance '" + span.Id + "' contains a cyclic asset reference; the enclosing appearance remains active.");
                        continue;
                    }
                    if (!definition.IsEnabled || !definition.IsValid)
                    {
                        draft.Resolution = definition.IsEnabled ? TextStudioAppearanceResolution.Invalid : TextStudioAppearanceResolution.Disabled;
                        AddDiagnostic(diagnostics, draft, string.IsNullOrEmpty(definition.Diagnostic)
                            ? "Appearance '" + span.Id + "' is disabled; the enclosing appearance remains active." : definition.Diagnostic);
                        continue;
                    }
                }
                draft.Mode = definition?.DefaultApplyMode ?? TextStudioAppearanceApplyMode.Replace;
                ReadMode(span.Parameters, draft, diagnostics);
                ReadComposite(span.Parameters, draft, diagnostics);
                TextStudio2DStyleSettings settings = default;
                if (definition != null)
                {
                    string definitionKey = DefinitionKey(definition);
                    if (!localTables.TryGetValue(definitionKey, out TextStudioAppearanceDefinitionTable table))
                    {
                        settings = definition.CompilationSettings;
                        table = TextStudioAppearanceDefinitions.Resolve(settings, definition.Asset ?? definition.OwnerAsset);
                        localTables.Add(definitionKey, table);
                    }
                    draft.Local = table;
                    if (!definition.IsValid)
                    {
                        draft.Resolution = TextStudioAppearanceResolution.Invalid; draft.Local = null;
                        AddDiagnostic(diagnostics, draft, definition.Diagnostic); continue;
                    }
                }
                string key = VariantKey(definition, span.Parameters, span.IsAdHoc, draft.Mode);
                if (!variants.TryGetValue(key, out CompiledTextStudio2DStyle program))
                {
                    if (previous != null && previous.Variants.TryGetValue(key, out CompiledTextStudio2DStyle retained))
                    {
                        program = retained;
                        variantDiagnostics.Add(key, previous.VariantDiagnostics.TryGetValue(key, out string[] messages) ? messages : Array.Empty<string>());
                        variantSettings.Add(key, previous.VariantSettings[key]);
                    }
                    else
                    {
                        parameterDiagnostics.Clear();
                        if (definition != null) settings = definition.CompilationSettings;
                        settings = span.IsAdHoc ? CreateAdHoc(span.Parameters, draft.Mode, parameterDiagnostics)
                            : TextStudioAppearanceParameterBinder.Apply(in settings, span.Parameters, parameterDiagnostics);
                        program = TextStudio2DStyleCompiler.Compile(in settings);
                        variantSettings.Add(key, settings);
                        variantDiagnostics.Add(key, parameterDiagnostics.ToArray());
                    }
                    // Diagnostics belong to the invocation arguments. GPU programs belong to their effective settings.
                    if (program.IsValid)
                    {
                        if (effectivePrograms.TryGetValue(program.Fingerprint, out CompiledTextStudio2DStyle shared)) program = shared;
                        else effectivePrograms.Add(program.Fingerprint, program);
                    }
                    variants.Add(key, program);
                    if (dependencySettings.TryGetValue(program, out TextStudio2DStyleSettings sharedSettings)) variantSettings[key] = sharedSettings;
                    else dependencySettings.Add(program, variantSettings[key]);
                }
                foreach (string message in variantDiagnostics[key]) AddDiagnostic(diagnostics, draft, message);
                if (!program.IsValid)
                {
                    draft.Resolution = TextStudioAppearanceResolution.Invalid;
                    AddDiagnostic(diagnostics, draft, "Appearance '" + span.Id + "' could not compile; the enclosing appearance remains active.");
                    draft.Local = null;
                    continue;
                }
                if (TextStudio2DHeroProgram.RequiresHero(program) && program.HeroProgram == null)
                {
                    draft.Resolution = TextStudioAppearanceResolution.Invalid;
                    string limit = program.HeroProgramStatus == TextStudio2DHeroProgramStatus.OperationCapacityExceeded
                        ? "the renderer supports at most 64 compiled operations per appearance program"
                        : program.HeroProgramStatus == TextStudio2DHeroProgramStatus.RelationshipDepthExceeded
                            ? "the renderer supports at most eight nested group or mask relationships"
                            : "its group or mask relationships are invalid";
                    AddDiagnostic(diagnostics, draft, "Appearance '" + span.Id + "' cannot render because " + limit + "; the enclosing appearance remains active.");
                    draft.Local = null;
                    continue;
                }
                if (!programIndices.TryGetValue(program, out int programIndex))
                { programIndex = programs.Count; programs.Add(program); programIndices.Add(program, programIndex); }
                draft.Program = programIndex;
            }

            TextStudioAppearanceCompositionSpan[] scopes = new TextStudioAppearanceCompositionSpan[drafts.Length];
            for (int index = 0; index < drafts.Length; index++)
                scopes[index] = new TextStudioAppearanceCompositionSpan(drafts[index].Domain, drafts[index].Mode,
                    drafts[index].Resolution == TextStudioAppearanceResolution.Active && drafts[index].Program >= 0);
            TextStudioAppearanceComposition.Build(scopes, units, out TextStudioAppearanceContribution[] contributions,
                out int[] unitContributions, out TextStudioAppearanceRange[][] visibleRanges);
            TextStudioAppearanceOccurrence[] occurrences = new TextStudioAppearanceOccurrence[drafts.Length];
            for (int index = 0; index < drafts.Length; index++)
            {
                Draft draft = drafts[index];
                occurrences[index] = new TextStudioAppearanceOccurrence(draft.Id, draft.OccurrenceId, draft.Parent, draft.Program,
                    draft.SourceStart, draft.SourceLength, draft.Domain, draft.Mode, draft.Resolution, draft.Tint, draft.Opacity,
                    draft.Definition, draft.Local, visibleRanges[index]);
            }
            TextStudio2DStyleAsset[] ownedDependencies = dependencies.ToArray();
            TextStudio2DStyleSettings[] uniqueSettings = new TextStudio2DStyleSettings[dependencySettings.Count];
            dependencySettings.Values.CopyTo(uniqueSettings, 0);
            return new CompiledTextStudioAppearancePlan(source, TextStudio2DStyles.Revision, programs.ToArray(), occurrences,
                contributions, unitContributions, diagnostics.ToArray(), DependencyRevision(defaultStyle, ownedDependencies, uniqueSettings), variants, ownedDependencies, variantDiagnostics, variantSettings, uniqueSettings, globals);
        }

        // Text without invocations assigns every unit to the root occurrence, so it needs no resolution or composition state.
        static CompiledTextStudioAppearancePlan CompileDefaultOnly(TextStudioCompiledPlan source,
            CompiledTextStudio2DStyle defaultStyle, int units)
        {
            TextStudioAppearanceRange domain = new TextStudioAppearanceRange(0, units);
            TextStudioAppearanceOccurrence root = new TextStudioAppearanceOccurrence(string.Empty, -1, -1, 0, 0, 0, domain,
                TextStudioAppearanceApplyMode.Replace, TextStudioAppearanceResolution.Active, Color.white, 1f, null, null,
                units > 0 ? new[] { domain } : null);
            TextStudio2DStyleAsset[] dependencies = Array.Empty<TextStudio2DStyleAsset>();
            TextStudio2DStyleSettings[] dependencySettings = Array.Empty<TextStudio2DStyleSettings>();
            return new CompiledTextStudioAppearancePlan(source, TextStudio2DStyles.Revision, new[] { defaultStyle },
                new[] { root }, new[] { new TextStudioAppearanceContribution(0, -1) }, new int[units],
                Array.Empty<TextStudioAppearanceDiagnostic>(),
                DependencyRevision(defaultStyle, dependencies, dependencySettings),
                NoVariants, dependencies, NoVariantDiagnostics, NoVariantSettings, dependencySettings, NoCatalogDefinitions);
        }

        // A default-only plan reads its source only for identity and unit count, so it stays exact for any later version of the same source with as many units and no invocations. Reusing it skips the compile of each such text change.
        internal static bool CanReuseDefaultOnly(CompiledTextStudioAppearancePlan plan, TextStudioCompiledPlan source,
            CompiledTextStudio2DStyle defaultStyle)
        {
            return plan != null && source != null && !plan.HasInvocations &&
                ReferenceEquals(plan.SourceIdentity, source) && source.AppearanceSpanCount == 0 &&
                plan.UnitCount == source.VisibleGlyphCount && ReferenceEquals(plan.DefaultStyle, defaultStyle) &&
                plan.AreCatalogBindingsCurrent() &&
                plan.DependencyRevision == DependencyRevision(defaultStyle, plan.Dependencies, plan.DependencySettings);
        }

        // Default-only plans never add entries, so they share these empty tables. They hold no arrays, and memory observation counts only their entries.
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly Dictionary<string, CompiledTextStudio2DStyle> NoVariants =
            new Dictionary<string, CompiledTextStudio2DStyle>(StringComparer.Ordinal);
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly Dictionary<string, string[]> NoVariantDiagnostics =
            new Dictionary<string, string[]>(StringComparer.Ordinal);
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly Dictionary<string, TextStudio2DStyleSettings> NoVariantSettings =
            new Dictionary<string, TextStudio2DStyleSettings>(StringComparer.Ordinal);
#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static readonly Dictionary<string, TextStudioAppearanceDefinition> NoCatalogDefinitions =
            new Dictionary<string, TextStudioAppearanceDefinition>(StringComparer.OrdinalIgnoreCase);

        static void ReadMode(TextStudioTagParameters parameters, Draft draft, List<TextStudioAppearanceDiagnostic> diagnostics)
        {
            if (!parameters.TryGetNamedParameter("mode", out _)) return;
            if (parameters.TryGetString("mode", out string value) && string.Equals(value, "overlay", StringComparison.OrdinalIgnoreCase))
                draft.Mode = TextStudioAppearanceApplyMode.Overlay;
            else if (parameters.TryGetString("mode", out value) && string.Equals(value, "replace", StringComparison.OrdinalIgnoreCase))
                draft.Mode = TextStudioAppearanceApplyMode.Replace;
            else AddDiagnostic(diagnostics, draft, "Appearance mode must be replace or overlay; the authored mode was kept.");
        }

        static void ReadComposite(TextStudioTagParameters parameters, Draft draft, List<TextStudioAppearanceDiagnostic> diagnostics)
        {
            if (parameters.TryGetNamedParameter("tint", out _))
            {
                if (parameters.TryGetColor("tint", out Color32 tint)) draft.Tint = tint;
                else AddDiagnostic(diagnostics, draft, "Appearance tint is not a valid color; the authored tint was kept.");
            }
            if (parameters.TryGetNamedParameter("opacity", out _))
            {
                if (parameters.TryGetFloat("opacity", out float opacity) && float.IsFinite(opacity) && opacity >= 0 && opacity <= 1) draft.Opacity = opacity;
                else AddDiagnostic(diagnostics, draft, "Appearance opacity must be between 0 and 1; the authored opacity was kept.");
            }
        }

        static string VariantKey(TextStudioAppearanceDefinition definition, TextStudioTagParameters parameters, bool adHoc, TextStudioAppearanceApplyMode mode)
        {
            StringBuilder key = new StringBuilder();
            if (definition != null) key.Append(DefinitionKey(definition));
            else key.Append("adhoc:").Append((int)mode);
            // Keep lexical arguments for typed validation and diagnostic replay. For example an
            // integer exposure accepts 1 but rejects 1.0. Effective programs are interned after binding.
            foreach (TextStudioNamedTagParameter parameter in parameters.Named)
            {
                if (IsReserved(parameter.Name)) continue;
                string value = parameter.Value ?? string.Empty;
                key.Append('|').Append(parameter.Name.Length).Append(':').Append(parameter.Name).Append(':').Append((int)parameter.Operation);
                key.Append(':').Append(parameter.IsFlag ? '1' : '0').Append(':').Append(value.Length).Append(':').Append(value);
            }
            return key.ToString();
        }

        static string DefinitionKey(TextStudioAppearanceDefinition definition) =>
            (definition.Asset != null ? definition.Asset.GetStableId() : definition.OwnerAsset != null ? definition.OwnerAsset.GetStableId() : 0).ToString(CultureInfo.InvariantCulture) +
            ":" + definition.ItemId + ":" + definition.Revision.ToString(CultureInfo.InvariantCulture);

        static bool IsReserved(string name) => string.Equals(name, "ref", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "mode", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "tint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "opacity", StringComparison.OrdinalIgnoreCase);

        static ulong DependencyRevision(CompiledTextStudio2DStyle defaultStyle, IReadOnlyList<TextStudio2DStyleAsset> dependencies,
            TextStudio2DStyleSettings[] variantSettings)
        {
            TextStudioDeterministicHash64 hash = default;
            hash.Add(defaultStyle.DependencyRevision);
            for (int index = 0; index < dependencies.Count; index++)
            {
                TextStudio2DStyleAsset asset = dependencies[index];
                hash.Add(asset != null);
                if (asset != null) hash.Add(TextStudio2DStyleCompiler.ComputeDependencyRevision(asset));
            }
            foreach (TextStudio2DStyleSettings settings in variantSettings)
            {
                hash.Add(TextStudio2DStyleCompiler.ComputeDependencyRevision(in settings));
            }
            return hash.Value;
        }

        static void AddDependency(List<TextStudio2DStyleAsset> assets, TextStudio2DStyleAsset asset)
        { if (asset != null && !assets.Contains(asset)) assets.Add(asset); }

        static bool HasReferenceCycle(TextStudioAppearanceDefinition definition, int parent, Draft[] drafts)
        {
            // Only authored asset-reference edges form a dependency cycle. Calling a global asset,
            // or nesting the same definition again, does not itself add an asset-reference edge.
            if (!definition.ItemId.IsValid || definition.Asset == null || definition.OwnerAsset == null) return false;
            TextStudio2DStyleAsset target = definition.Asset;
            TextStudio2DStyleAsset owner = definition.OwnerAsset;
            if (owner == target) return true;
            for (int scope = parent; scope > 0; scope = drafts[scope].Parent)
            {
                TextStudioAppearanceDefinition ancestor = drafts[scope].Definition;
                if (drafts[scope].Resolution != TextStudioAppearanceResolution.Active || ancestor == null ||
                    !ancestor.ItemId.IsValid || ancestor.Asset != owner || ancestor.OwnerAsset == null) continue;
                owner = ancestor.OwnerAsset;
                if (owner == target) return true;
            }
            return false;
        }

        static void AddDiagnostic(List<TextStudioAppearanceDiagnostic> diagnostics, Draft draft, string message)
        { diagnostics.Add(new TextStudioAppearanceDiagnostic(message, draft.OccurrenceId, draft.SourceStart, draft.SourceLength)); }

        internal static TextStudio2DStyleSettings CreateAdHoc(TextStudioTagParameters parameters, TextStudioAppearanceApplyMode mode, List<string> diagnostics)
        {
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>();
            bool hasFill = parameters.TryGetNamedParameter("fill", out _);
            if (!hasFill && mode == TextStudioAppearanceApplyMode.Replace)
                layers.Add(TextStudio2DLayerDefaults.CreateNativeFill());
            else if (hasFill)
                layers.Add(TextStudio2DLayerDefaults.Create(TextStudio2DLayerKind.Fill).WithPaint(TextStudio2DPaint.Solid(ReadColor(parameters, "fill", Color.white, diagnostics))));
            if (parameters.TryGetNamedParameter("outline", out _) || parameters.TryGetNamedParameter("outlineWidth", out _))
            {
                TextStudio2DLayer layer = TextStudio2DLayerDefaults.Create(TextStudio2DLayerKind.Stroke);
                layers.Add(TextStudio2DLayer.CreateStroke(layer.DisplayName, TextStudio2DPaint.Solid(ReadColor(parameters, "outline", layer.Paint.Color, diagnostics)),
                    new TextStudio2DStrokeSpec(ReadDistance(parameters, "outlineWidth", layer.Stroke.Width, false, diagnostics))));
            }
            if (parameters.TryGetNamedParameter("shadow", out _) || parameters.TryGetNamedParameter("shadowX", out _) ||
                parameters.TryGetNamedParameter("shadowY", out _) || parameters.TryGetNamedParameter("shadowBlur", out _))
            {
                TextStudio2DLayer layer = TextStudio2DLayerDefaults.Create(TextStudio2DLayerKind.OuterShadow);
                Vector2 offset = new Vector2(ReadDistance(parameters, "shadowX", layer.Shadow.Offset.x, true, diagnostics),
                    ReadDistance(parameters, "shadowY", layer.Shadow.Offset.y, true, diagnostics));
                layers.Add(TextStudio2DLayer.CreateShadow(layer.DisplayName, false,
                    TextStudio2DPaint.Solid(ReadColor(parameters, "shadow", layer.Paint.Color, diagnostics)),
                    new TextStudio2DShadowSpec(offset, ReadDistance(parameters, "shadowBlur", layer.Shadow.Blur, false, diagnostics))));
            }
            if (parameters.TryGetNamedParameter("glow", out _) || parameters.TryGetNamedParameter("glowRadius", out _))
            {
                TextStudio2DLayer layer = TextStudio2DLayerDefaults.Create(TextStudio2DLayerKind.OuterGlow);
                layers.Add(TextStudio2DLayer.CreateGlow(layer.DisplayName, false,
                    TextStudio2DPaint.Solid(ReadColor(parameters, "glow", layer.Paint.Color, diagnostics)),
                    new TextStudio2DGlowSpec(ReadDistance(parameters, "glowRadius", layer.Glow.Radius, false, diagnostics))));
            }
            foreach (TextStudioNamedTagParameter parameter in parameters.Named)
                if (!IsReserved(parameter.Name) && !IsAdHocParameter(parameter.Name)) diagnostics.Add("Unknown appearance parameter '" + parameter.Name + "'.");
            for (int index = 0; index < layers.Count; index++)
                layers[index] = layers[index].WithIdentity(TextStudioStackItemId.Create("textstudio.appearance.adhoc." + layers[index].Kind.ToString().ToLowerInvariant()));
            TextStudio2DStyleSettings settings = default;
            settings.SetLayers(layers.ToArray());
            return settings;
        }

        static bool IsAdHocParameter(string name) => string.Equals(name, "fill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "outline", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "outlineWidth", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "shadow", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "shadowX", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "shadowY", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "shadowBlur", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "glow", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "glowRadius", StringComparison.OrdinalIgnoreCase);

        static Color ReadColor(TextStudioTagParameters parameters, string name, Color fallback, List<string> diagnostics)
        {
            if (!parameters.TryGetNamedParameter(name, out _)) return fallback;
            if (parameters.TryGetColor(name, out Color32 color)) return color;
            diagnostics.Add("Appearance parameter '" + name + "' is not a valid color; its authored value was kept.");
            return fallback;
        }

        static float ReadDistance(TextStudioTagParameters parameters, string name, float fallback, bool negative, List<string> diagnostics)
        {
            if (!parameters.TryGetNamedParameter(name, out _)) return fallback;
            if (parameters.TryGetFloat(name, out float value) && float.IsFinite(value) && (negative || value >= 0)) return value;
            diagnostics.Add("Appearance parameter '" + name + "' must be a " + (negative ? "finite" : "non-negative finite") + " font-em distance; its authored value was kept.");
            return fallback;
        }

        sealed class Draft
        {
            internal string Id;
            internal int OccurrenceId, Parent, Program, SourceStart, SourceLength;
            internal TextStudioAppearanceRange Domain;
            internal TextStudioAppearanceApplyMode Mode;
            internal TextStudioAppearanceResolution Resolution;
            internal Color Tint = Color.white;
            internal float Opacity = 1;
            internal TextStudioAppearanceDefinition Definition;
            internal TextStudioAppearanceDefinitionTable Local;
        }

    }
}
