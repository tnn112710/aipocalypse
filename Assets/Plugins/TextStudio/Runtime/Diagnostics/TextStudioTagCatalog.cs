using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using TextStudio.BuiltIns;

namespace TextStudio
{
    /// <summary>Stable category identifiers used to group tags in discovery and authoring UI.</summary>
    public static class TextStudioTagCategories
    {
        public const string Effects = "Effects";
        public const string Transitions = "Transitions";
        public const string Typewriter = "Typewriter";
        public const string Actions = "Actions";
        public const string Styling = "Styling";
        public const string Appearance = "Appearance";
        public const string TagExpansions = "Tag Expansions";
        public const string IntegrationPrefix = "Integration ";
    }

    /// <summary>Groups catalogued markup tags by purpose, including effects, cues, formatting, integrations, and appearance.</summary>
    public enum TextStudioTagCategory
    {
        Effect,
        ComposedEffect,
        Cue,
        Formatting,
        Integration,
        Unknown,
        Appearance
    }

    /// <summary>Flags the text backends and feature levels for which a catalogued markup tag is supported.</summary>
    [Flags]
    public enum TextStudioTagBackend
    {
        None = 0,
        TextMeshPro = 1 << 0,
        UIToolkitBasic = 1 << 1,
        UIToolkitFullParity = 1 << 2,
        UniText = 1 << 3,
        UniTextRichText = 1 << 4,
        UnityUIText = 1 << 5,
        UnityUITextRichText = 1 << 6,
        All = TextMeshPro |
              UIToolkitBasic |
              UIToolkitFullParity |
              UniText |
              UniTextRichText |
              UnityUIText |
              UnityUITextRichText
    }

    /// <summary>Declares whether a catalogued tag surrounds a text range, marks a single point, or is self-contained markup.</summary>
    public enum TextStudioTagShape
    {
        Range,
        Point,
        SelfContained
    }

    /// <summary>Identifies whether Text Studio consumes a markup tag or passes it through to the active text backend.</summary>
    public enum TextStudioTagOwnership
    {
        ConsumedByTextStudio,
        PassthroughToBackend
    }

    /// <summary>Declares whether a tag optionally accepts parameters, requires them, or forbids them.</summary>
    public enum TextStudioTagParameterRequirement
    {
        Any,
        Required,
        Forbidden
    }

    /// <summary>Declares the value format accepted by a markup parameter, including numbers, colors, vectors, pivots, routes, and asset references.</summary>
    public enum TextStudioTagParameterKind
    {
        String,
        Float,
        Integer,
        Boolean,
        Color,
        Enum,
        Vector2,
        Vector3,
        Pivot,
        RouteId,
        AssetReference
    }

    /// <summary>Stable lookup keys for injecting project-specific parameter suggestions.</summary>
    public static class TextStudioTagSuggestionKeys
    {
        public const string EventRoute = "event.route";
        public const string MarkerRoute = "marker.route";
        public const string StyleName = "style.name";
        public const string AppearanceStyle = "appearance.style";
        public const string FontName = "font.name";
        public const string SpriteName = "sprite.name";
    }

    /// <summary>Parameter name, aliases, type, requirement, defaults, examples, and suggestion metadata for a tag.</summary>
    public sealed class TextStudioTagParameterDefinition
    {
        readonly ReadOnlyCollection<string> _allowedValues;
        readonly ReadOnlyCollection<string> _aliases;

        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public TextStudioTagParameterKind Kind { get; }
        public bool Required { get; }
        public int PositionalIndex { get; }
        public string SuggestionKey { get; }
        public string DefaultValue { get; }
        public string ExampleValue { get; }
        public float? Minimum { get; }
        public float? Maximum { get; }
        public float Snap { get; }
        public string Suffix { get; }
        public bool AllowOverflow { get; }
        public IReadOnlyList<string> AllowedValues => _allowedValues;
        public IReadOnlyList<string> Aliases => _aliases;
        // Family metadata can reuse its runtime parser while retaining named value suggestions.
        internal Func<string, bool> ValueValidator { get; set; }

        /// <summary>Creates a parameter schema with normalized names, aliases, examples, and suggestions.</summary>
        public TextStudioTagParameterDefinition(
            string name,
            TextStudioTagParameterKind kind = TextStudioTagParameterKind.String,
            bool required = false,
            int positionalIndex = -1,
            string displayName = null,
            string description = null,
            string suggestionKey = null,
            string[] allowedValues = null,
            string[] aliases = null,
            string defaultValue = null,
            string exampleValue = null,
            float? minimum = null,
            float? maximum = null,
            float snap = 0f,
            string suffix = null,
            bool allowOverflow = false)
        {
            Name = name ?? string.Empty;
            DisplayName = displayName ?? Name;
            Description = description ?? string.Empty;
            Kind = kind;
            Required = required;
            PositionalIndex = positionalIndex;
            SuggestionKey = suggestionKey ?? string.Empty;
            DefaultValue = defaultValue ?? string.Empty;
            ExampleValue = exampleValue ?? string.Empty;
            Minimum = minimum;
            Maximum = maximum;
            Snap = snap > 0f ? snap : 0f;
            Suffix = suffix ?? string.Empty;
            AllowOverflow = allowOverflow;
            _allowedValues = Array.AsReadOnly(
                allowedValues != null ? (string[])allowedValues.Clone() : Array.Empty<string>());
            _aliases = Array.AsReadOnly(aliases != null ? (string[])aliases.Clone() : Array.Empty<string>());
        }

        internal TextStudioTagParameterDefinition WithDefaultValue(string defaultValue)
        {
            return new TextStudioTagParameterDefinition(
                Name,
                Kind,
                Required,
                PositionalIndex,
                DisplayName,
                Description,
                SuggestionKey,
                ToArray(_allowedValues),
                ToArray(_aliases),
                defaultValue,
                ExampleValue,
                Minimum,
                Maximum,
                Snap,
                Suffix,
                AllowOverflow)
            {
                ValueValidator = ValueValidator
            };
        }

        internal bool IsValueAllowed(string value)
        {
            if (ValueValidator != null)
                return ValueValidator(value);

            if (_allowedValues.Count == 0)
                return true;

            for (int i = 0; i < _allowedValues.Count; i++)
            {
                if (string.Equals(_allowedValues[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string[] ToArray(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] result = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                result[i] = values[i];
            return result;
        }

        /// <summary>Matches the canonical parameter name or an alias using ordinal case-insensitive comparison.</summary>
        public bool MatchesName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (string.Equals(Name, name, StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < _aliases.Count; i++)
            {
                if (string.Equals(_aliases[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>Immutable discovery and validation schema for one built-in, native, action, or extension tag.</summary>
    public sealed class TextStudioTagDefinition
    {
        readonly ReadOnlyCollection<string> _aliases;
        readonly ReadOnlyCollection<string> _examples;
        readonly ReadOnlyCollection<TextStudioTagParameterDefinition> _parameters;

        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string OwnerId { get; }
        public string OwnerDisplayName { get; }
        public string Description { get; }
        public TextStudioTagKind Kind { get; }
        public TextStudioTagCategory SemanticCategory { get; }
        public TextStudioTagShape Shape { get; }
        public TextStudioTagOwnership Ownership { get; }
        public TextStudioTagBackend BackendSupport { get; }
        public TextStudioTagParameterRequirement ParameterRequirement { get; }
        public TextStudioRenderDomain SupportedRenderDomains { get; }
        public IReadOnlyList<string> Aliases => _aliases;
        public IReadOnlyList<string> Examples => _examples;
        public IReadOnlyList<TextStudioTagParameterDefinition> Parameters => _parameters;

        /// <summary>Creates a complete immutable tag definition for discovery and diagnostics.</summary>
        public TextStudioTagDefinition(
            string id,
            TextStudioTagKind kind,
            string category,
            string displayName = null,
            string description = null,
            TextStudioTagShape shape = TextStudioTagShape.Range,
            TextStudioTagOwnership ownership = TextStudioTagOwnership.ConsumedByTextStudio,
            TextStudioTagBackend backendSupport = TextStudioTagBackend.All,
            TextStudioTagParameterRequirement parameterRequirement = TextStudioTagParameterRequirement.Any,
            string ownerId = null,
            string ownerDisplayName = null,
            string[] aliases = null,
            string[] examples = null,
            TextStudioTagParameterDefinition[] parameters = null,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Category = category ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            Description = description ?? string.Empty;
            SemanticCategory = ResolveSemanticCategory(Kind, Category);
            Shape = shape;
            Ownership = ownership;
            BackendSupport = backendSupport;
            ParameterRequirement = parameterRequirement;
            SupportedRenderDomains = supportedRenderDomains;
            OwnerId = ownerId ?? string.Empty;
            OwnerDisplayName = ownerDisplayName ?? OwnerId;
            _aliases = Array.AsReadOnly(aliases != null ? (string[])aliases.Clone() : Array.Empty<string>());
            _examples = Array.AsReadOnly(examples != null ? (string[])examples.Clone() : Array.Empty<string>());
            _parameters = Array.AsReadOnly(
                parameters != null
                    ? (TextStudioTagParameterDefinition[])parameters.Clone()
                    : Array.Empty<TextStudioTagParameterDefinition>());
        }

        static TextStudioTagCategory ResolveSemanticCategory(TextStudioTagKind kind, string category)
        {
            if (!string.IsNullOrEmpty(category) &&
                category.StartsWith(TextStudioTagCategories.IntegrationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return TextStudioTagCategory.Integration;
            }

            switch (kind)
            {
                case TextStudioTagKind.Appearance:
                    return TextStudioTagCategory.Appearance;
                case TextStudioTagKind.TextStudioEffect:
                    return TextStudioTagCategory.Effect;
                case TextStudioTagKind.Action:
                case TextStudioTagKind.Marker:
                    return TextStudioTagCategory.Cue;
                case TextStudioTagKind.NativeRichText:
                case TextStudioTagKind.CustomDecoration:
                    return TextStudioTagCategory.Formatting;
                case TextStudioTagKind.Typewriter:
                    return TextStudioTagCategory.Cue;
                case TextStudioTagKind.TagExpansion:
                    return TextStudioTagCategory.Formatting;
                case TextStudioTagKind.Extension:
                    return TextStudioTagCategory.Integration;
                default:
                    return TextStudioTagCategory.Unknown;
            }
        }

        /// <summary>Reports whether the tag is valid for the requested authoring backend.</summary>
        public bool SupportsBackend(TextStudioTagBackend backend)
        {
            return backend == TextStudioTagBackend.None || (BackendSupport & backend) != 0;
        }

        /// <summary>Reports whether the tag's parameter requirement permits the supplied parameter presence.</summary>
        public bool MatchesParameterState(bool hasParameter)
        {
            switch (ParameterRequirement)
            {
                case TextStudioTagParameterRequirement.Required:
                    return hasParameter;
                case TextStudioTagParameterRequirement.Forbidden:
                    return !hasParameter;
                case TextStudioTagParameterRequirement.Any:
                default:
                    return true;
            }
        }

        /// <summary>Gets the positional schema for an index, including a repeatable tail when configured.</summary>
        public TextStudioTagParameterDefinition GetPositionalParameter(int index)
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].PositionalIndex == index)
                    return _parameters[i];
            }

            return null;
        }

        /// <summary>Gets a named schema by case-insensitive canonical name or alias.</summary>
        public TextStudioTagParameterDefinition GetNamedParameter(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].MatchesName(name))
                    return _parameters[i];
            }

            return null;
        }
    }

    /// <summary>Searchable catalog of supported tags assembled from built-ins and installed extension contracts.</summary>
    public sealed class TextStudioTagCatalog
    {
        readonly TextStudioTagDefinition[] _definitions;
        readonly ReadOnlyCollection<TextStudioTagDefinition> _readOnlyDefinitions;
        readonly Dictionary<string, List<TextStudioTagDefinition>> _lookup;

        TextStudioTagCatalog(TextStudioTagDefinition[] definitions)
        {
            _definitions = definitions ?? Array.Empty<TextStudioTagDefinition>();
            _readOnlyDefinitions = Array.AsReadOnly(_definitions);
            _lookup = new Dictionary<string, List<TextStudioTagDefinition>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _definitions.Length; i++)
                IndexDefinition(_definitions[i]);
        }

        public IReadOnlyList<TextStudioTagDefinition> Definitions => _readOnlyDefinitions;

        /// <summary>Builds the catalog from built-ins and globally registered actions and extensions.</summary>
        public static TextStudioTagCatalog BuildDefault()
        {
            TextStudioRegistry.EnsureInitialized();
            CatalogBuilder builder = BuildDefaultCatalog();
            return new TextStudioTagCatalog(builder.ToArray());
        }

        /// <summary>Builds the default catalog plus a caller-supplied local action set.</summary>
        public static TextStudioTagCatalog BuildDefault(IEnumerable<ITextStudioAction> localActions)
        {
            TextStudioRegistry.EnsureInitialized();
            CatalogBuilder builder = BuildDefaultCatalog();
            AddActionDefinitions(builder, localActions);
            return new TextStudioTagCatalog(builder.ToArray());
        }

        static CatalogBuilder BuildDefaultCatalog()
        {
            CatalogBuilder builder = new CatalogBuilder();
            AddCharacterSubstitutionDefinitions(builder);
            AddTypewriterDefinitions(builder);
            AddEffectDefinitions(builder);
            AddTransitionDefinitions(builder);
            AddActionDefinitions(builder);
            AddStylingDefinitions(builder);
            AddTagExpansionDefinitions(builder);
            AddExtensionDefinitions(builder);
            return builder;
        }

        static void AddCharacterSubstitutionDefinitions(CatalogBuilder builder)
        {
            foreach (ITextStudioCharacterSubstitution substitution in TextStudioRegistry.CharacterSubstitutions.GetAll())
            {
                if (substitution == null)
                    continue;

                TextStudioCharacterSubstitutionDefinition definition = substitution.Definition;
                string id = definition != null && !string.IsNullOrEmpty(definition.Id)
                    ? definition.Id
                    : substitution.Id;
                builder.Add(new TextStudioTagDefinition(
                    id,
                    TextStudioTagKind.TextStudioEffect,
                    TextStudioTagCategories.Effects,
                    definition != null ? definition.DisplayName : id,
                    definition != null ? definition.Description : string.Empty,
                    TextStudioTagShape.Range,
                    TextStudioTagOwnership.ConsumedByTextStudio,
                    TextStudioTagBackend.All,
                    aliases: definition != null ? ToArray(definition.Aliases) : null,
                    examples: definition != null ? ToArray(definition.Examples) : null,
                    parameters: definition != null ? ToArray(definition.Parameters) : null));
            }
        }

        static void AddTypewriterDefinitions(CatalogBuilder builder)
        {
            builder.Add(new TextStudioTagDefinition(
                "instant",
                TextStudioTagKind.Typewriter,
                TextStudioTagCategories.Typewriter,
                "Instant Range",
                "Reveals this range immediately while surrounding text still follows the active typewriter timing.",
                TextStudioTagShape.Range,
                TextStudioTagOwnership.ConsumedByTextStudio,
                TextStudioTagBackend.All,
                TextStudioTagParameterRequirement.Forbidden,
                examples: new[]
                {
                    "<instant>already visible</instant>"
                }));
        }

        /// <summary>Resolves a tag name and parameter state to a registered definition or alias.</summary>
        public bool TryGetDefinition(string tagName, bool hasParameter, out TextStudioTagDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(tagName) || !_lookup.TryGetValue(tagName, out List<TextStudioTagDefinition> candidates))
                return false;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].Id, tagName, StringComparison.OrdinalIgnoreCase) &&
                    candidates[i].MatchesParameterState(hasParameter) &&
                    candidates[i].ParameterRequirement != TextStudioTagParameterRequirement.Any)
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].Id, tagName, StringComparison.OrdinalIgnoreCase) &&
                    candidates[i].MatchesParameterState(hasParameter))
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].MatchesParameterState(hasParameter) &&
                    candidates[i].ParameterRequirement != TextStudioTagParameterRequirement.Any)
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].MatchesParameterState(hasParameter))
                {
                    definition = candidates[i];
                    return true;
                }
            }

            return false;
        }

        internal bool TryGetDefinition(
            string tagName,
            bool hasParameter,
            TextStudioTagKind kind,
            out TextStudioTagDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(tagName) || !_lookup.TryGetValue(tagName, out List<TextStudioTagDefinition> candidates))
                return false;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == kind &&
                    string.Equals(candidates[i].Id, tagName, StringComparison.OrdinalIgnoreCase) &&
                    candidates[i].MatchesParameterState(hasParameter) &&
                    candidates[i].ParameterRequirement != TextStudioTagParameterRequirement.Any)
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == kind &&
                    string.Equals(candidates[i].Id, tagName, StringComparison.OrdinalIgnoreCase) &&
                    candidates[i].MatchesParameterState(hasParameter))
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == kind &&
                    candidates[i].MatchesParameterState(hasParameter) &&
                    candidates[i].ParameterRequirement != TextStudioTagParameterRequirement.Any)
                {
                    definition = candidates[i];
                    return true;
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == kind && candidates[i].MatchesParameterState(hasParameter))
                {
                    definition = candidates[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns a detached list of definitions in one stable category.</summary>
        public List<TextStudioTagDefinition> GetDefinitionsInCategory(string category)
        {
            List<TextStudioTagDefinition> result = new List<TextStudioTagDefinition>();
            for (int i = 0; i < _definitions.Length; i++)
            {
                if (string.Equals(_definitions[i].Category, category, StringComparison.OrdinalIgnoreCase))
                    result.Add(_definitions[i]);
            }

            return result;
        }

        void IndexDefinition(TextStudioTagDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            AddLookup(definition.Id, definition);
            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
                AddLookup(aliases[i], definition);
        }

        void AddLookup(string key, TextStudioTagDefinition definition)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!_lookup.TryGetValue(key, out List<TextStudioTagDefinition> definitions))
            {
                definitions = new List<TextStudioTagDefinition>(2);
                _lookup[key] = definitions;
            }

            definitions.Add(definition);
        }

        static void AddEffectDefinitions(CatalogBuilder builder)
        {
            foreach (KeyValuePair<string, string> pair in BuiltInStyles.DefaultTagMap)
            {
                ITextStudioEffectDefinition effect;
                TextStudioRegistry.Effects.TryGetQuiet(pair.Value, out effect);
                TextStudioEffectMetadata metadata = TextStudioEffectMetadataResolver.Get(effect);

                builder.Add(new TextStudioTagDefinition(
                    pair.Key,
                    TextStudioTagKind.TextStudioEffect,
                    TextStudioTagCategories.Effects,
                    string.IsNullOrEmpty(metadata.DisplayName) ? pair.Key : metadata.DisplayName,
                    metadata.Description,
                    TextStudioTagShape.Range,
                    TextStudioTagOwnership.ConsumedByTextStudio,
                    ResolveEffectBackendSupport(effect),
                    TextStudioTagParameterRequirement.Any,
                    aliases: CreateEffectAliases(pair.Key, pair.Value),
                    examples: CreateEffectExamples(pair.Key),
                    parameters: CreateEffectParameters(pair.Value, effect),
                    supportedRenderDomains: ResolveEffectRenderDomains(effect)));
            }

            foreach (KeyValuePair<string, ITextStudioEffectDefinition> pair in TextStudioRegistry.Effects.GetAll())
            {
                if (BuiltInStyles.DefaultTagMap.ContainsKey(pair.Key))
                    continue;

                TextStudioEffectMetadata metadata = TextStudioEffectMetadataResolver.Get(pair.Value);
                builder.Add(new TextStudioTagDefinition(
                    pair.Key,
                    TextStudioTagKind.TextStudioEffect,
                    TextStudioTagCategories.Effects,
                    string.IsNullOrEmpty(metadata.DisplayName) ? pair.Key : metadata.DisplayName,
                    metadata.Description,
                    TextStudioTagShape.Range,
                    TextStudioTagOwnership.ConsumedByTextStudio,
                    ResolveEffectBackendSupport(pair.Value),
                    aliases: CreateEffectAliases(pair.Key, pair.Key),
                    examples: CreateEffectExamples(pair.Key),
                    parameters: CreateEffectParameters(pair.Key, pair.Value),
                    supportedRenderDomains: ResolveEffectRenderDomains(pair.Value)));
            }
        }

        static TextStudioTagBackend ResolveEffectBackendSupport(ITextStudioEffectDefinition effect)
        {
            if (effect is not ITextStudioEffectCapabilitiesProvider provider)
                return TextStudioTagBackend.TextMeshPro |
                       TextStudioTagBackend.UIToolkitFullParity |
                       TextStudioTagBackend.UniText |
                       TextStudioTagBackend.UniTextRichText;

            TextStudioEffectCapabilities capabilities = provider.Capabilities;
            if (capabilities.PerformanceTier == TextStudioEffectPerformanceTier.Material)
                return TextStudioTagBackend.TextMeshPro;

            TextStudioTagBackend support =
                TextStudioTagBackend.TextMeshPro |
                TextStudioTagBackend.UIToolkitFullParity |
                TextStudioTagBackend.UniText |
                TextStudioTagBackend.UniTextRichText;
            TextStudioRenderChannels unsupportedChannels =
                capabilities.RenderChannels & TextStudioRenderChannels.UvOffset;
            if (unsupportedChannels == TextStudioRenderChannels.None)
            {
                support |= TextStudioTagBackend.UnityUIText |
                           TextStudioTagBackend.UnityUITextRichText;
            }
            return support;
        }

        static TextStudioRenderDomain ResolveEffectRenderDomains(ITextStudioEffectDefinition effect)
        {
            return effect is ITextStudioEffectCapabilitiesProvider provider
                ? provider.Capabilities.SupportedRenderDomains
                : TextStudioRenderDomain.All;
        }

        static string[] CreateEffectAliases(string tagName, string effectId)
        {
            List<string> aliases = null;
            foreach (KeyValuePair<string, string> pair in BuiltInStyles.DefaultTagMap)
            {
                if (!string.Equals(pair.Value, effectId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                aliases ??= new List<string>(4);
                aliases.Add(pair.Key);
            }

            return aliases != null ? aliases.ToArray() : Array.Empty<string>();
        }

        static TextStudioTagParameterDefinition[] CreateEffectParameters(
            string effectId,
            ITextStudioEffectDefinition effect)
        {
            TextStudioEffectAreaSettings defaultArea = effect != null
                ? effect.DefaultArea
                : TextStudioEffectAreaSettings.Default;
            TextStudioEffectMetadata metadata = TextStudioEffectMetadataResolver.Get(effect);
            IReadOnlyList<TextStudioTagParameterDefinition> effectParameters = metadata.Parameters;
            List<TextStudioTagParameterDefinition> parameters =
                new List<TextStudioTagParameterDefinition>(effectParameters.Count + 5);
            string[] playbackValues = GetEffectPlaybackValues();
            string[] curveValues = GetEffectCurveValues();
            for (int i = 0; i < effectParameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = effectParameters[i];
                if (string.Equals(parameter.Name, "playback", StringComparison.OrdinalIgnoreCase))
                    parameter = WithAdditionalAllowedValues(parameter, playbackValues);
                else if (string.Equals(parameter.Name, "curve", StringComparison.OrdinalIgnoreCase))
                    parameter = WithAdditionalAllowedValues(parameter, curveValues);
                parameters.Add(parameter);
            }
            parameters.Add(CreateEffectAreaParameter(defaultArea));
            parameters.Add(CreateEffectGroupParameter(defaultArea));
            parameters.Add(CreateEffectCharOffsetParameter());
            parameters.Add(CreateEffectWordOffsetParameter());
            parameters.Add(CreateEffectLineOffsetParameter());
            return parameters.ToArray();
        }

        static TextStudioTagParameterDefinition WithAdditionalAllowedValues(
            TextStudioTagParameterDefinition parameter,
            IReadOnlyList<string> additionalValues)
        {
            List<string> allowedValues = new List<string>(
                parameter.AllowedValues.Count + (additionalValues?.Count ?? 0) + 1);
            for (int i = 0; i < parameter.AllowedValues.Count; i++)
                allowedValues.Add(parameter.AllowedValues[i]);
            for (int i = 0; additionalValues != null && i < additionalValues.Count; i++)
            {
                if (!ContainsIgnoreCase(allowedValues, additionalValues[i]))
                    allowedValues.Add(additionalValues[i]);
            }
            if (!string.IsNullOrWhiteSpace(parameter.DefaultValue) &&
                !ContainsIgnoreCase(allowedValues, parameter.DefaultValue))
            {
                allowedValues.Add(parameter.DefaultValue);
            }

            return new TextStudioTagParameterDefinition(
                parameter.Name,
                parameter.Kind,
                parameter.Required,
                parameter.PositionalIndex,
                parameter.DisplayName,
                parameter.Description,
                parameter.SuggestionKey,
                allowedValues.ToArray(),
                ToArray(parameter.Aliases),
                parameter.DefaultValue,
                parameter.ExampleValue,
                parameter.Minimum,
                parameter.Maximum,
                parameter.Snap,
                parameter.Suffix,
                parameter.AllowOverflow)
            {
                ValueValidator = parameter.ValueValidator
            };
        }

        static TextStudioTagParameterDefinition CreateEffectAreaParameter(
            TextStudioEffectAreaSettings defaultArea)
        {
            return new TextStudioTagParameterDefinition(
                "area",
                TextStudioTagParameterKind.Enum,
                displayName: "Area",
                description: "Controls whether the effect is applied per glyph, word, line, paragraph, or tagged range.",
                allowedValues: new[] { "glyph", "word", "line", "paragraph", "range" },
                defaultValue: defaultArea.Mode.ToString().ToLowerInvariant());
        }

        static TextStudioTagParameterDefinition CreateEffectGroupParameter(
            TextStudioEffectAreaSettings defaultArea)
        {
            return new TextStudioTagParameterDefinition(
                "group",
                TextStudioTagParameterKind.Integer,
                displayName: "Group Size",
                description: "Groups multiple glyphs, words, lines, or paragraphs into one effect unit.",
                defaultValue: defaultArea.GroupSize.ToString(CultureInfo.InvariantCulture));
        }

        static TextStudioTagParameterDefinition CreateEffectCharOffsetParameter()
        {
            return new TextStudioTagParameterDefinition(
                "charOffset",
                TextStudioTagParameterKind.Float,
                displayName: "Character Offset",
                description: "Text Animator-compatible active effect clock offset in seconds per glyph.",
                defaultValue: "0");
        }

        static TextStudioTagParameterDefinition CreateEffectWordOffsetParameter()
        {
            return new TextStudioTagParameterDefinition(
                "wordOffset",
                TextStudioTagParameterKind.Float,
                displayName: "Word Offset",
                description: "Text Animator-compatible active effect clock offset in seconds per word.",
                defaultValue: "0");
        }

        static TextStudioTagParameterDefinition CreateEffectLineOffsetParameter()
        {
            return new TextStudioTagParameterDefinition(
                "lineOffset",
                TextStudioTagParameterKind.Float,
                displayName: "Line Offset",
                description: "Text Animator-compatible active effect clock offset in seconds per line.",
                defaultValue: "0");
        }

        static string[] GetEffectPlaybackValues()
        {
            List<string> values = new List<string>(BuiltInPlaybacks.TextAnimatorPlaybackKeywords);
            if (!ContainsIgnoreCase(values, "pingpong"))
                values.Insert(Math.Min(2, values.Count), "pingpong");
            if (!ContainsIgnoreCase(values, "cycle"))
                values.Add("cycle");
            if (!ContainsIgnoreCase(values, "weighted"))
                values.Add("weighted");
            if (!ContainsIgnoreCase(values, "appearance"))
                values.Add("appearance");
            foreach (KeyValuePair<string, IEffectPlayback> pair in TextStudioRegistry.Playbacks.GetAll())
            {
                if (!(pair.Value is EffectPlaybackAsset) || ContainsIgnoreCase(values, pair.Key))
                    continue;
                values.Add(pair.Key);
            }
            return values.ToArray();
        }

        static string[] GetEffectCurveValues()
        {
            List<string> values = new List<string>
            {
                "linear",
                "reverseLinear",
                "sine",
                "halfSine",
                "square",
                "step",
                "hold",
                "bounce",
                "easeIn",
                "easeOut",
                "easeInOut",
                "backOut",
                "elasticOut"
            };
            foreach (KeyValuePair<string, ITextStudioCurve> pair in TextStudioRegistry.Curves.GetAll())
            {
                if (!(pair.Value is TextStudioCurveProfile) || ContainsIgnoreCase(values, pair.Key))
                    continue;
                values.Add(pair.Key);
            }
            return values.ToArray();
        }

        static bool ContainsIgnoreCase(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static string[] CreateEffectExamples(string effectId)
        {
            if (string.Equals(effectId, TextStudioBuiltInEffectIds.SurfaceSweep, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<surfaceSweep>Text</surfaceSweep>",
                    "<surfaceSweep angle=20 width=.15 brightness=1.25 area=line>Text</surfaceSweep>",
                    "<surfaceSweep playback=loop curve=reverseLinear>Text</surfaceSweep>"
                };
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.Shimmer, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<shimmer>Text</shimmer>",
                    "<shimmer color=#fff2b0 width=.2 speed=.8 area=range>Text</shimmer>",
                    "<shimmer playback=once curve=reverseLinear>Text</shimmer>"
                };
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.PixelGrid, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<pixelGrid>Text</pixelGrid>",
                    "<pixelGrid size=8 space=screen quality=preserve>Text</pixelGrid>",
                    "<pixelGrid active=.65 pulse=1.2 min=4 max=10 space=glyph>Text</pixelGrid>"
                };
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.ColorPulse, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<colorPulse>Text</colorPulse>",
                    "<colorPulse=#ff3366,#33ccff>Text</colorPulse>",
                    "<colorPulse from=#ff3366 to=#33ccff speed=1.8>Text</colorPulse>",
                    "<colorPulse from=light red to=dark blue>Text</colorPulse>"
                };
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.Wave, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<wave>Text</wave>",
                    "<wave a=.2 speed=.8>Text</wave>",
                    "<wave a*2>Text</wave>"
                };
            }

            if (string.Equals(effectId, TextStudioBuiltInEffectIds.Float, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "<float>Text</float>",
                    "<float vertical=.24 horizontal=.06 spread=1.5>Text</float>",
                    "<float a*2>Text</float>"
                };
            }

            return new[] { "<" + effectId + ">Text</" + effectId + ">" };
        }

        static void AddTransitionDefinitions(CatalogBuilder builder)
        {
            TextStudioTagParameterDefinition[] appearParameters = CreateTransitionParameters(true);
            TextStudioTagParameterDefinition[] disappearParameters = CreateTransitionParameters(false);
            builder.Add(new TextStudioTagDefinition(
                TextStudioPhaseTagUtility.AppearTag,
                TextStudioTagKind.TextStudioEffect,
                TextStudioTagCategories.Transitions,
                "Appear",
                "Applies one or more effects only while glyphs are entering through reveal/typewriter playback.",
                TextStudioTagShape.Range,
                TextStudioTagOwnership.ConsumedByTextStudio,
                TextStudioTagBackend.TextMeshPro |
                TextStudioTagBackend.UIToolkitFullParity |
                TextStudioTagBackend.UniText |
                TextStudioTagBackend.UniTextRichText |
                TextStudioTagBackend.UnityUIText |
                TextStudioTagBackend.UnityUITextRichText,
                TextStudioTagParameterRequirement.Required,
                aliases: new[] { "appearance" },
                examples: new[]
                {
                    "<appear fade>Text</appear>",
                    "<appear fade,slideV blend=0.2 charOffset=0.02>Text</appear>"
                },
                parameters: appearParameters));

            builder.Add(new TextStudioTagDefinition(
                TextStudioPhaseTagUtility.DisappearTag,
                TextStudioTagKind.TextStudioEffect,
                TextStudioTagCategories.Transitions,
                "Disappear",
                "Applies one or more effects only while visible glyphs are hiding through StartDisappear.",
                TextStudioTagShape.Range,
                TextStudioTagOwnership.ConsumedByTextStudio,
                TextStudioTagBackend.TextMeshPro |
                TextStudioTagBackend.UIToolkitFullParity |
                TextStudioTagBackend.UniText |
                TextStudioTagBackend.UniTextRichText |
                TextStudioTagBackend.UnityUIText |
                TextStudioTagBackend.UnityUITextRichText,
                TextStudioTagParameterRequirement.Required,
                aliases: new[] { "disappearance", "hide" },
                examples: new[]
                {
                    "<disappear size>Text</disappear>",
                    "<disappear fade,size charOffset=0.02>Text</disappear>"
                },
                parameters: disappearParameters));
        }

        static TextStudioTagParameterDefinition[] CreateTransitionParameters(bool includeRevealBlend)
        {
            List<TextStudioTagParameterDefinition> parameters = new List<TextStudioTagParameterDefinition>
            {
                new TextStudioTagParameterDefinition(
                    "effect",
                    TextStudioTagParameterKind.String,
                    true,
                    0,
                    description: "Effect id or comma-separated effect ids to layer during this transition.",
                    exampleValue: "fade"),
                new TextStudioTagParameterDefinition(
                    "charOffset",
                    TextStudioTagParameterKind.Float,
                    description: "Additional delay in seconds per glyph for this transition effect.",
                    defaultValue: "0"),
                new TextStudioTagParameterDefinition(
                    "wordOffset",
                    TextStudioTagParameterKind.Float,
                    description: "Additional delay in seconds per word for this transition effect.",
                    defaultValue: "0"),
                new TextStudioTagParameterDefinition(
                    "lineOffset",
                    TextStudioTagParameterKind.Float,
                    description: "Additional delay in seconds per line for this transition effect.",
                    defaultValue: "0"),
                new TextStudioTagParameterDefinition(
                    "speed",
                    TextStudioTagParameterKind.Float,
                    description: "Speed multiplier for this transition effect.",
                    defaultValue: "1")
            };

            if (includeRevealBlend)
            {
                parameters.Add(new TextStudioTagParameterDefinition(
                    "blend",
                    TextStudioTagParameterKind.Float,
                    displayName: "Reveal Blend",
                    description: "Seconds used to blend this reveal stack smoothly into active text. Defaults to 1 and is clamped from 0 to 5.",
                    defaultValue: "1"));
            }

            return parameters.ToArray();
        }

        static void AddActionDefinitions(CatalogBuilder builder)
        {
            foreach (CodeActionBase action in BuiltInActions.All())
            {
                TextStudioActionDefinition actionDefinition = CreateActionDefinition(action, action.Id);
                builder.Add(CreateActionTagDefinition(actionDefinition, CreateActionExample(actionDefinition.Id)));
            }

            foreach (KeyValuePair<string, ITextStudioAction> pair in TextStudioRegistry.Actions.GetAll())
            {
                if (BuiltInActions.TryGetCanonicalActionId(pair.Key, out _))
                    continue;

                TextStudioActionDefinition actionDefinition = CreateActionDefinition(pair.Value, pair.Key);
                builder.Add(CreateActionTagDefinition(actionDefinition, "<" + actionDefinition.Id + ">"));
            }
        }

        static void AddActionDefinitions(CatalogBuilder builder, IEnumerable<ITextStudioAction> actions)
        {
            if (actions == null)
                return;

            foreach (ITextStudioAction action in actions)
            {
                if (action == null)
                    continue;

                TextStudioActionDefinition actionDefinition = CreateActionDefinition(action, action.Id);
                builder.Add(CreateActionTagDefinition(actionDefinition, "<" + actionDefinition.Id + ">"));
            }
        }

        static TextStudioTagDefinition CreateActionTagDefinition(
            TextStudioActionDefinition actionDefinition,
            string fallbackExample)
        {
            return new TextStudioTagDefinition(
                actionDefinition.Id,
                actionDefinition.IsMarker ? TextStudioTagKind.Marker : TextStudioTagKind.Action,
                TextStudioTagCategories.Actions,
                actionDefinition.DisplayName,
                actionDefinition.Description,
                TextStudioTagShape.Point,
                TextStudioTagOwnership.ConsumedByTextStudio,
                TextStudioTagBackend.All,
                aliases: ToArray(actionDefinition.Aliases),
                parameters: ToArray(actionDefinition.Parameters),
                examples: actionDefinition.Examples.Count > 0
                    ? ToArray(actionDefinition.Examples)
                    : new[] { fallbackExample });
        }

        static TextStudioActionDefinition CreateActionDefinition(ITextStudioAction action, string fallbackId)
        {
            if (TextStudioActionDefinitionUtility.TryGetDefinition(action, out TextStudioActionDefinition definition))
                return definition;

            string id = !string.IsNullOrEmpty(fallbackId) ? fallbackId : action != null ? action.Id : string.Empty;
            return new TextStudioActionDefinition(
                id,
                id,
                "Runs a registered Text Studio action.",
                examples: new[] { "<" + id + ">" });
        }

        static TextStudioTagParameterDefinition[] CreateActionParameters(string actionId)
        {
            if (string.Equals(actionId, TextStudioBuiltInActionIds.Wait, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new TextStudioTagParameterDefinition(
                        "seconds",
                        TextStudioTagParameterKind.Float,
                        positionalIndex: 0,
                        description: "Seconds to pause reveal.")
                };
            }

            if (string.Equals(actionId, TextStudioBuiltInActionIds.Speed, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new TextStudioTagParameterDefinition(
                        "multiplier",
                        TextStudioTagParameterKind.Float,
                        positionalIndex: 0,
                        description: "Reveal speed multiplier.")
                };
            }

            if (string.Equals(actionId, TextStudioBuiltInActionIds.SpeedUp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, TextStudioBuiltInActionIds.SpeedDown, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new TextStudioTagParameterDefinition(
                        "factor",
                        TextStudioTagParameterKind.Float,
                        positionalIndex: 0,
                        description: "Speed factor. Defaults to 2.")
                };
            }

            if (string.Equals(actionId, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new TextStudioTagParameterDefinition(
                        "route",
                        TextStudioTagParameterKind.RouteId,
                        true,
                        0,
                        description: "Named event route.",
                        suggestionKey: TextStudioTagSuggestionKeys.EventRoute),
                    new TextStudioTagParameterDefinition(
                        "payload",
                        TextStudioTagParameterKind.String,
                        positionalIndex: 1,
                        description: "Optional payload values after the route.")
                };
            }

            return Array.Empty<TextStudioTagParameterDefinition>();
        }

        static string CreateActionExample(string actionId)
        {
            if (string.Equals(actionId, TextStudioBuiltInActionIds.Wait, StringComparison.OrdinalIgnoreCase))
                return "<wait=0.5>";
            if (string.Equals(actionId, TextStudioBuiltInActionIds.Speed, StringComparison.OrdinalIgnoreCase))
                return "<speed=1.25>";
            if (string.Equals(actionId, TextStudioBuiltInActionIds.SpeedUp, StringComparison.OrdinalIgnoreCase))
                return "<speedUp=2>";
            if (string.Equals(actionId, TextStudioBuiltInActionIds.SpeedDown, StringComparison.OrdinalIgnoreCase))
                return "<speedDown=2>";
            if (string.Equals(actionId, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase))
                return "<event=choiceA>";
            if (string.Equals(actionId, TextStudioBuiltInActionIds.WaitForInput, StringComparison.OrdinalIgnoreCase))
                return "<waitForInput>";
            return "<" + actionId + ">";
        }

        static string[] CreateActionAliases(string actionId)
        {
            if (string.Equals(actionId, BuiltInActions.Wait.Id, StringComparison.OrdinalIgnoreCase))
                return new[] { "waitfor" };
            if (string.Equals(actionId, BuiltInActions.WaitForInput.Id, StringComparison.OrdinalIgnoreCase))
                return new[] { "waitinput" };
            return null;
        }

        static void AddStylingDefinitions(CatalogBuilder builder)
        {
            builder.Add(new TextStudioTagDefinition(
                "style", TextStudioTagKind.Appearance, TextStudioTagCategories.Appearance,
                "Appearance Style", "Applies a named appearance or local fill, outline, shadow, and glow. Distances are font-em values.",
                examples: new[] { "<style ref=\"warning\">Text</style>", "<style fill=#FFFFFF outline=#000000 outlineWidth=0.03>Text</style>" },
                parameters: new[]
                {
                    new TextStudioTagParameterDefinition("ref", TextStudioTagParameterKind.AssetReference, suggestionKey: TextStudioTagSuggestionKeys.AppearanceStyle),
                    new TextStudioTagParameterDefinition("mode", TextStudioTagParameterKind.Enum, allowedValues: new[] { "replace", "overlay" }),
                    new TextStudioTagParameterDefinition("tint", TextStudioTagParameterKind.Color),
                    new TextStudioTagParameterDefinition("opacity", TextStudioTagParameterKind.Float, minimum: 0, maximum: 1),
                    new TextStudioTagParameterDefinition("fill", TextStudioTagParameterKind.Color),
                    new TextStudioTagParameterDefinition("outline", TextStudioTagParameterKind.Color),
                    new TextStudioTagParameterDefinition("outlineWidth", TextStudioTagParameterKind.Float, minimum: 0, suffix: "em"),
                    new TextStudioTagParameterDefinition("shadow", TextStudioTagParameterKind.Color),
                    new TextStudioTagParameterDefinition("shadowX", TextStudioTagParameterKind.Float, suffix: "em"),
                    new TextStudioTagParameterDefinition("shadowY", TextStudioTagParameterKind.Float, suffix: "em"),
                    new TextStudioTagParameterDefinition("shadowBlur", TextStudioTagParameterKind.Float, minimum: 0, suffix: "em"),
                    new TextStudioTagParameterDefinition("glow", TextStudioTagParameterKind.Color),
                    new TextStudioTagParameterDefinition("glowRadius", TextStudioTagParameterKind.Float, minimum: 0, suffix: "em")
                }));
            IReadOnlyList<TextStudioNativeRichTextTagDescriptor> specs = BuiltInStyles.NativeRichTextTagDescriptors;
            for (int i = 0; i < specs.Count; i++)
            {
                TextStudioNativeRichTextTagDescriptor spec = specs[i];
                builder.Add(new TextStudioTagDefinition(
                    spec.Id,
                    TextStudioTagKind.NativeRichText,
                    TextStudioTagCategories.Styling,
                    spec.DisplayName,
                    spec.Description,
                    spec.Shape,
                    TextStudioTagOwnership.PassthroughToBackend,
                    spec.BackendSupport,
                    spec.ParameterRequirement,
                    aliases: ToArray(spec.Aliases),
                    examples: ToArray(spec.Examples),
                    parameters: ToArray(spec.Parameters),
                    supportedRenderDomains: spec.SupportedRenderDomains));
            }

            builder.Add(new TextStudioTagDefinition(
                "uu",
                TextStudioTagKind.CustomDecoration,
                TextStudioTagCategories.Styling,
                "Double Underline",
                "Draws a Text Studio double underline over the tagged glyph range.",
                TextStudioTagShape.Range,
                TextStudioTagOwnership.ConsumedByTextStudio,
                TextStudioTagBackend.TextMeshPro | TextStudioTagBackend.UIToolkitFullParity,
                examples: new[] { "<uu>Text</uu>" }));
            builder.Add(new TextStudioTagDefinition(
                "o",
                TextStudioTagKind.CustomDecoration,
                TextStudioTagCategories.Styling,
                "Overline",
                "Draws a Text Studio overline over the tagged glyph range.",
                TextStudioTagShape.Range,
                TextStudioTagOwnership.ConsumedByTextStudio,
                    TextStudioTagBackend.TextMeshPro | TextStudioTagBackend.UIToolkitFullParity,
                    examples: new[] { "<o>Text</o>" }));
        }

        static void AddTagExpansionDefinitions(CatalogBuilder builder)
        {
            foreach (KeyValuePair<string, TextStudioTagExpansionEntry> pair in TextStudioRegistry.TagExpansions.GetAll())
            {
                TextStudioTagExpansionEntry entry = pair.Value;
                if (entry == null || string.IsNullOrWhiteSpace(entry.TagId))
                    continue;

                string id = entry.TagId.Trim();
                builder.Add(new TextStudioTagDefinition(
                    id,
                    TextStudioTagKind.TagExpansion,
                    TextStudioTagCategories.TagExpansions,
                    displayName: id,
                    description: "Expands to reusable Text Studio markup before tags are resolved.",
                    shape: TextStudioTagShape.Range,
                    ownership: TextStudioTagOwnership.ConsumedByTextStudio,
                    backendSupport: TextStudioTagBackend.All,
                    parameterRequirement: TextStudioTagParameterRequirement.Forbidden,
                    examples: new[] { "<" + id + ">text</" + id + ">" }));
            }
        }

        static void AddExtensionDefinitions(CatalogBuilder builder)
        {
            Dictionary<string, int> ownerIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (TextStudioExtensionTagDefinition extension in TextStudioRegistry.ExtensionTags.GetAllDefinitions())
            {
                if (extension == null || string.IsNullOrEmpty(extension.Id))
                    continue;

                string ownerId = string.IsNullOrEmpty(extension.OwnerId) ? "Extensions" : extension.OwnerId;
                if (!ownerIndices.TryGetValue(ownerId, out int index))
                {
                    index = ownerIndices.Count + 1;
                    ownerIndices[ownerId] = index;
                }

                builder.Add(new TextStudioTagDefinition(
                    extension.Id,
                    TextStudioTagKind.Extension,
                    TextStudioTagCategories.IntegrationPrefix + index.ToString(CultureInfo.InvariantCulture),
                    extension.DisplayName,
                    extension.Description,
                    extension.Shape == TextStudioExtensionTagShape.Range ? TextStudioTagShape.Range : TextStudioTagShape.Point,
                    TextStudioTagOwnership.ConsumedByTextStudio,
                    TextStudioTagBackend.All,
                    ownerId: ownerId,
                    ownerDisplayName: ownerId,
                    aliases: ToArray(extension.Aliases),
                    examples: extension.Examples.Count > 0 ? ToArray(extension.Examples) : new[] { "<" + extension.Id + "=value>" },
                    parameters: ToArray(extension.Parameters)));
            }
        }

        static string[] ToArray(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            string[] copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        static TextStudioTagParameterDefinition[] ToArray(IReadOnlyList<TextStudioTagParameterDefinition> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<TextStudioTagParameterDefinition>();

            TextStudioTagParameterDefinition[] copy = new TextStudioTagParameterDefinition[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }

        sealed class CatalogBuilder
        {
            readonly List<TextStudioTagDefinition> _definitions = new List<TextStudioTagDefinition>(128);
            readonly HashSet<string> _keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public void Add(TextStudioTagDefinition definition)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    return;

                string key = definition.Id + "|" + definition.Kind + "|" + definition.ParameterRequirement;
                if (!_keys.Add(key))
                    return;

                _definitions.Add(definition);
            }

            public TextStudioTagDefinition[] ToArray()
            {
                _definitions.Sort(CompareDefinitions);
                return _definitions.ToArray();
            }

            static int CompareDefinitions(TextStudioTagDefinition left, TextStudioTagDefinition right)
            {
                int category = GetCategoryRank(left.Category).CompareTo(GetCategoryRank(right.Category));
                if (category != 0)
                    return category;
                category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
                if (category != 0)
                    return category;
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            }

            static int GetCategoryRank(string category)
            {
                if (string.Equals(category, TextStudioTagCategories.Effects, StringComparison.OrdinalIgnoreCase))
                    return 0;
                if (string.Equals(category, TextStudioTagCategories.Actions, StringComparison.OrdinalIgnoreCase))
                    return 2;
                if (string.Equals(category, TextStudioTagCategories.Transitions, StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (string.Equals(category, TextStudioTagCategories.Styling, StringComparison.OrdinalIgnoreCase))
                    return 3;
                if (string.Equals(category, TextStudioTagCategories.TagExpansions, StringComparison.OrdinalIgnoreCase))
                    return 4;
                if (!string.IsNullOrEmpty(category) &&
                    category.StartsWith(TextStudioTagCategories.IntegrationPrefix, StringComparison.OrdinalIgnoreCase))
                    return 5;
                return 10;
            }
        }
    }

    /// <summary>Optional backend, catalog, and project-specific suggestion values used by markup diagnostics.</summary>
    public sealed class TextStudioTagAnalysisContext
    {
        // Owns its normalized copy, so source contexts can safely share catalog metadata.
        internal sealed class SuggestionSnapshot
        {
            internal IReadOnlyList<string> Values { get; }

            internal SuggestionSnapshot(IEnumerable<string> values)
            {
                List<string> distinct = new List<string>();
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (values != null)
                {
                    foreach (string value in values)
                        if (!string.IsNullOrEmpty(value) && seen.Add(value)) distinct.Add(value);
                }
                Values = distinct.AsReadOnly();
            }
        }

        readonly Dictionary<string, SuggestionSnapshot> _suggestions =
            new Dictionary<string, SuggestionSnapshot>(StringComparer.OrdinalIgnoreCase);

        public TextStudioTagCatalog Catalog { get; }
        public TextStudioTagBackend Backend { get; }
        public TextStudioRenderDomain RenderDomains { get; }

        /// <summary>Creates analysis context for one catalog, backend, and installed render-domain set.</summary>
        public TextStudioTagAnalysisContext(
            TextStudioTagCatalog catalog = null,
            TextStudioTagBackend backend = TextStudioTagBackend.None,
            TextStudioRenderDomain renderDomains = TextStudioRenderDomain.All)
        {
            Catalog = catalog ?? TextStudioTagCatalog.BuildDefault();
            Backend = backend;
            RenderDomains = renderDomains;
        }

        /// <summary>Replaces normalized suggestion values for a stable suggestion key.</summary>
        public void SetSuggestionValues(string key, IEnumerable<string> values)
        {
            if (string.IsNullOrEmpty(key))
                return;

            SetSuggestionSnapshot(key, new SuggestionSnapshot(values));
        }

        internal void SetSuggestionSnapshot(string key, SuggestionSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(key) && snapshot != null) _suggestions[key] = snapshot;
        }

        /// <summary>Tries to obtain caller-supplied suggestions for a stable key.</summary>
        public bool TryGetSuggestionValues(string key, out IReadOnlyList<string> values)
        {
            values = Array.Empty<string>();
            if (string.IsNullOrEmpty(key) || !_suggestions.TryGetValue(key, out SuggestionSnapshot stored))
                return false;

            values = stored.Values;
            return values.Count > 0;
        }
    }

    internal static class TextStudioTagSourceEditor
    {
        internal readonly struct EditResult
        {
            public string Text { get; }
            public int SelectionStart { get; }
            public int SelectionEnd { get; }

            public EditResult(string text, int selectionStart, int selectionEnd)
            {
                Text = text ?? string.Empty;
                SelectionStart = selectionStart;
                SelectionEnd = selectionEnd;
            }
        }

        public static string AppendExample(string source, TextStudioTagDefinition definition)
        {
            string snippet = CreateExample(definition);
            return AppendSnippet(source, snippet);
        }

        public static string AppendSnippet(string source, string snippet)
        {
            return (source ?? string.Empty) + snippet;
        }

        public static string ReplaceRange(string source, int sourceStart, int sourceEnd, string replacement)
        {
            string text = source ?? string.Empty;
            int start = Math.Max(0, Math.Min(sourceStart, text.Length));
            int end = Math.Max(start, Math.Min(sourceEnd, text.Length));
            return text.Substring(0, start) + (replacement ?? string.Empty) + text.Substring(end);
        }

        public static EditResult ReplaceSelectionWithSnippet(
            string source,
            int sourceStart,
            int sourceEnd,
            string snippet)
        {
            string text = source ?? string.Empty;
            int start = Math.Max(0, Math.Min(sourceStart, text.Length));
            int end = Math.Max(start, Math.Min(sourceEnd, text.Length));
            string safeSnippet = snippet ?? string.Empty;
            string replacement = safeSnippet;
            int relativeSelectionStart = safeSnippet.Length;
            int relativeSelectionEnd = relativeSelectionStart;

            if (TrySplitPairedSnippet(
                    safeSnippet,
                    out string opening,
                    out string exampleContent,
                    out string closing))
            {
                if (end > start)
                {
                    string selection = text.Substring(start, end - start);
                    replacement = opening + selection + closing;
                    relativeSelectionStart = opening.Length;
                    relativeSelectionEnd = relativeSelectionStart + selection.Length;
                }
                else
                {
                    relativeSelectionStart = opening.Length;
                    relativeSelectionEnd = relativeSelectionStart + exampleContent.Length;
                }
            }

            string next = ReplaceRange(text, start, end, replacement);
            return new EditResult(
                next,
                start + relativeSelectionStart,
                start + relativeSelectionEnd);
        }

        static bool TrySplitPairedSnippet(
            string snippet,
            out string opening,
            out string content,
            out string closing)
        {
            opening = string.Empty;
            content = string.Empty;
            closing = string.Empty;
            if (string.IsNullOrEmpty(snippet))
                return false;

            int openingEnd = snippet.IndexOf('>');
            int closingStart = snippet.LastIndexOf("</", StringComparison.Ordinal);
            if (openingEnd < 0 || closingStart <= openingEnd || !snippet.EndsWith(">", StringComparison.Ordinal))
                return false;

            opening = snippet.Substring(0, openingEnd + 1);
            content = snippet.Substring(openingEnd + 1, closingStart - openingEnd - 1);
            closing = snippet.Substring(closingStart);
            return true;
        }

        public static string CreateExample(TextStudioTagDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            if (definition.Examples.Count > 0)
                return definition.Examples[0];

            return "<" + definition.Id + ">";
        }
    }

    public static class TextStudioTagParameterParser
    {
        public static TextStudioTagParameters Parse(string raw)
        {
            return Parse(raw, false);
        }

        public static TextStudioTagParameters Parse(string raw, bool allowNumericModifiers)
        {
            raw = raw != null ? raw.Trim() : string.Empty;
            if (string.IsNullOrEmpty(raw))
                return TextStudioTagParameters.Empty;

            List<string> positional = new List<string>(4);
            List<TextStudioNamedTagParameter> named = new List<TextStudioNamedTagParameter>(4);

            if (!ContainsNamedOperatorOutsideQuotes(raw, allowNumericModifiers))
            {
                AddCommaSeparated(raw, positional);
                return new TextStudioTagParameters(raw, positional.ToArray(), named.ToArray());
            }

            if (!ContainsWhitespaceOutsideQuotes(raw))
            {
                AddParameterPart(raw, positional, named, allowNumericModifiers);
                return new TextStudioTagParameters(raw, positional.ToArray(), named.ToArray());
            }

            int index = 0;
            while (index < raw.Length)
            {
                while (index < raw.Length && char.IsWhiteSpace(raw[index]))
                    index++;
                if (index >= raw.Length)
                    break;

                int tokenStart = index;
                bool quoted = false;
                char quote = '\0';
                while (index < raw.Length)
                {
                    char c = raw[index];
                    if (quoted)
                    {
                        if (c == quote)
                            quoted = false;
                    }
                    else if (c == '"' || c == '\'')
                    {
                        quoted = true;
                        quote = c;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        break;
                    }

                    index++;
                }

                AddParameterPart(
                    raw.Substring(tokenStart, index - tokenStart),
                    positional,
                    named,
                    allowNumericModifiers);
            }

            return new TextStudioTagParameters(raw, positional.ToArray(), named.ToArray());
        }

        static bool ContainsNamedOperatorOutsideQuotes(string raw, bool allowNumericModifiers)
        {
            bool quoted = false;
            char quote = '\0';
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (quoted)
                {
                    if (c == quote)
                        quoted = false;
                }
                else if (c == '"' || c == '\'')
                {
                    quoted = true;
                    quote = c;
                }
                else if (c == '=' || (allowNumericModifiers && c == '*'))
                {
                    return true;
                }
            }

            return false;
        }

        static bool ContainsWhitespaceOutsideQuotes(string raw)
        {
            bool quoted = false;
            char quote = '\0';
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (quoted)
                {
                    if (c == quote)
                        quoted = false;
                }
                else if (c == '"' || c == '\'')
                {
                    quoted = true;
                    quote = c;
                }
                else if (char.IsWhiteSpace(c))
                {
                    return true;
                }
            }

            return false;
        }

        static void AddCommaSeparated(string raw, List<string> positional)
        {
            int start = 0;
            bool quoted = false;
            char quote = '\0';
            for (int i = 0; i <= raw.Length; i++)
            {
                if (i < raw.Length)
                {
                    char c = raw[i];
                    if (quoted)
                    {
                        if (c == quote)
                            quoted = false;
                        continue;
                    }

                    if (c == '"' || c == '\'')
                    {
                        quoted = true;
                        quote = c;
                        continue;
                    }

                    if (c != ',')
                        continue;
                }

                string value = Unquote(raw.Substring(start, i - start).Trim());
                if (value.Length > 0)
                    positional.Add(value);
                start = i + 1;
            }
        }

        static void AddParameterPart(
            string part,
            List<string> positional,
            List<TextStudioNamedTagParameter> named,
            bool allowNumericModifiers)
        {
            if (string.IsNullOrWhiteSpace(part))
                return;

            int equals = part.IndexOf('=');
            if (equals > 0)
            {
                string key = part.Substring(0, equals).Trim();
                string value = Unquote(part.Substring(equals + 1).Trim());
                if (!string.IsNullOrEmpty(key))
                    named.Add(new TextStudioNamedTagParameter(key, value, false));
                return;
            }

            int multiply = allowNumericModifiers ? part.IndexOf('*') : -1;
            if (multiply > 0)
            {
                string key = part.Substring(0, multiply).Trim();
                string value = Unquote(part.Substring(multiply + 1).Trim());
                if (!string.IsNullOrEmpty(key))
                {
                    named.Add(new TextStudioNamedTagParameter(
                        key,
                        value,
                        false,
                        TextStudioTagParameterOperation.Multiply));
                }
                return;
            }

            if (part.IndexOf(',') >= 0)
            {
                AddCommaSeparated(part, positional);
                return;
            }

            named.Add(new TextStudioNamedTagParameter(part.Trim(), string.Empty, true));
        }

        static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}
