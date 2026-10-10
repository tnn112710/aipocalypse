using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace TextStudio
{
    /// <summary>
    /// Lean asset-backed definition for programmable leaf effects. Lifecycle, playback, curves,
    /// sequencing, and scope are authored by Composed Effect layers rather than this asset.
    /// </summary>
    public abstract class TextStudioCodeEffectAsset : TextStudioEffectDefinitionAsset,
        ITextStudioEffectFactory,
        ITextStudioEffectMetadataProvider
    {
        [SerializeField] string _id = string.Empty;
        [SerializeField] TextStudioEffectAreaMode _defaultArea = TextStudioEffectAreaMode.Glyph;
        [SerializeField, Min(1)] int _defaultGroupSize = 1;
        [SerializeField] EffectBlendMode _blendMode = EffectBlendMode.Additive;
        [SerializeField] string _category = TextStudioTagCategories.Effects;
        [SerializeField] string _displayName = string.Empty;
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;
        [SerializeField] string _markupSnippet = string.Empty;
        [SerializeField] TextStudioPhaseMask _suggestedPhases = TextStudioPhaseMask.Active;
        [SerializeField] TextStudioRenderChannels _renderChannels = TextStudioRenderChannels.All;
        [SerializeField] TextStudioPhaseMask _supportedPhases = TextStudioPhaseMask.All;
        [SerializeField] TextStudioEffectPerformanceTier _performanceTier = TextStudioEffectPerformanceTier.Realtime;
        [SerializeField] TextStudioReducedMotionBehavior _reducedMotionBehavior = TextStudioReducedMotionBehavior.Reduce;
        [SerializeField] bool _isDeterministic = true;
        [SerializeField] bool _canFlash;
        [SerializeField] TextStudioRenderDomain _supportedRenderDomains = TextStudioRenderDomain.All;
        [SerializeField, Tooltip("Whether output keeps stable bounds, changes bounds through streamed output, or requires a geometry refresh.")]
        TextStudioEffectBoundsBehavior _boundsBehavior =
            TextStudioEffectBoundsBehavior.Unchanged;
        [SerializeField, HideInInspector] bool _identityInitialized;

        public override string Id => _id ?? string.Empty;
        public override TextStudioEffectAreaSettings DefaultArea =>
            new TextStudioEffectAreaSettings(_defaultArea, Mathf.Max(1, _defaultGroupSize));
        public override EffectBlendMode BlendMode => _blendMode;
        public override TextStudioEffectCapabilities Capabilities => new TextStudioEffectCapabilities(
            _renderChannels,
            _supportedPhases,
            _performanceTier,
            _reducedMotionBehavior,
            _isDeterministic,
            _canFlash,
            _supportedRenderDomains,
            _boundsBehavior);

        protected virtual string DefaultId => CreateDefaultId(GetType());
        protected virtual string DefaultCategory => TextStudioTagCategories.Effects;
        protected virtual string DefaultDisplayName => Nicify(DefaultId);
        protected virtual string DefaultDescription => string.Empty;
        protected virtual string DefaultMarkupSnippet => "<" + DefaultId + ">";
        protected virtual TextStudioPhaseMask DefaultSuggestedPhases => TextStudioPhaseMask.Active;

        protected string MetadataCategory => _category ?? string.Empty;
        protected string MetadataDisplayName => _displayName ?? string.Empty;
        protected string MetadataDescription => _description ?? string.Empty;
        protected string MetadataMarkupSnippet => _markupSnippet ?? string.Empty;
        protected TextStudioPhaseMask MetadataSuggestedPhases => _suggestedPhases;

        public abstract TextStudioEffectMetadata Metadata { get; }

        public abstract ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters);

        protected virtual void OnEnable()
        {
            EnsureIdentityInitialized();
        }

        protected override void OnValidate()
        {
            EnsureIdentityInitialized();
            ValidatePublishedParameters();
            base.OnValidate();
        }

        /// <summary>Validates the published parameter contract after serialized values change.</summary>
        protected abstract void ValidatePublishedParameters();

        /// <summary>Initializes missing identity and metadata fields from the effect's declared defaults.</summary>
        protected void EnsureIdentityInitialized()
        {
            if (!_identityInitialized)
            {
                if (string.IsNullOrWhiteSpace(_id))
                    _id = DefaultId;
                if (string.IsNullOrWhiteSpace(_category))
                    _category = DefaultCategory;
                if (string.IsNullOrWhiteSpace(_displayName))
                    _displayName = DefaultDisplayName;
                if (string.IsNullOrWhiteSpace(_description))
                    _description = DefaultDescription;
                if (string.IsNullOrWhiteSpace(_markupSnippet))
                    _markupSnippet = DefaultMarkupSnippet;
                if (_suggestedPhases == TextStudioPhaseMask.None)
                    _suggestedPhases = DefaultSuggestedPhases;
                _identityInitialized = true;
            }

            _id = string.IsNullOrWhiteSpace(_id) ? DefaultId : _id.Trim();
            _category = string.IsNullOrWhiteSpace(_category) ? DefaultCategory : _category.Trim();
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? Nicify(_id) : _displayName.Trim();
            _defaultGroupSize = Mathf.Max(1, _defaultGroupSize);
            if (_supportedPhases == TextStudioPhaseMask.None)
                _supportedPhases = TextStudioPhaseMask.All;
            if (_supportedRenderDomains == TextStudioRenderDomain.None)
                _supportedRenderDomains = TextStudioRenderDomain.All;
        }

        static string CreateDefaultId(Type type)
        {
            string name = type != null ? type.Name : "codeEffect";
            string[] suffixes = { "CodeEffectAsset", "EffectAsset", "Effect" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (!name.EndsWith(suffix, StringComparison.Ordinal) || name.Length <= suffix.Length)
                    continue;
                name = name.Substring(0, name.Length - suffix.Length);
                break;
            }

            return string.IsNullOrEmpty(name)
                ? "codeEffect"
                : char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                    builder.Append(' ');
                builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return builder.ToString();
        }
    }

    /// <summary>Typed Code Effect asset with one default parameter struct and isolated per-glyph state.</summary>
    [Preserve]
    public abstract class TextStudioCodeEffectAsset<TState, TParameters> : TextStudioCodeEffectAsset
        where TState : struct
        where TParameters : struct
    {
        [SerializeField] TParameters _defaultParameters;
        [SerializeField, HideInInspector] bool _parameterDefaultsInitialized;

        [NonSerialized] TextStudioEffectMetadata _metadata;
        [NonSerialized] string _metadataValidationError;

        public TParameters DefaultParameters => _defaultParameters;

        public sealed override TextStudioEffectMetadata Metadata
        {
            get
            {
                EnsureDefaultsInitialized();
                if (_metadata == null)
                    _metadata = CreateMetadata();
                return _metadata;
            }
        }

        public sealed override ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            EnsureDefaultsInitialized();
            TParameters resolved = _defaultParameters;
            if (!TryBindParameters(ref resolved, in parameters, out _))
                return null;
            return new Evaluator(this, in resolved);
        }

        /// <summary>Creates the typed parameter defaults stored by a newly initialized Code Effect asset.</summary>
        protected virtual TParameters CreateDefaultParameters()
        {
            return default;
        }

        /// <summary>Creates isolated initial state for one glyph in one effect invocation.</summary>
        protected virtual TState CreateDefaultState()
        {
            return default;
        }

        /// <summary>
        /// Binds parsed parameters once for an invocation. Override for custom validation or syntax;
        /// the default binder supports Text Studio's published primitive parameter types.
        /// </summary>
        protected virtual bool TryBindParameters(
            ref TParameters parameters,
            in TextStudioTagParameters values,
            out string error)
        {
            return TextStudioTypedEffectParameters<TParameters>.TryBind(
                ref parameters,
                in values,
                out error);
        }

        /// <summary>Reports whether a custom binder intentionally handles unsupported published fields.</summary>
        protected virtual bool HandlesUnsupportedPublishedParameters => false;

        /// <summary>Evaluates the Code Effect for one glyph using the invocation's bound parameters and isolated state.</summary>
        protected abstract void EvaluateEffect(
            ref EvaluatedGlyphState glyphState,
            ref TState effectState,
            in TParameters parameters,
            in EffectEvaluationContext context,
            EffectBlendMode blendMode);

        /// <summary>Validates that the typed parameter struct can be published through Text Studio metadata.</summary>
        protected sealed override void ValidatePublishedParameters()
        {
            EnsureDefaultsInitialized();
            _metadata = null;
            TextStudioTypedEffectParameters<TParameters>.CreateDefinitions(
                in _defaultParameters,
                out _metadataValidationError);
            if (!HandlesUnsupportedPublishedParameters &&
                !string.IsNullOrEmpty(_metadataValidationError))
            {
                Debug.LogError(
                    "[TextStudio] Code Effect '" + Id + "': " + _metadataValidationError,
                    this);
            }
        }

        TextStudioEffectMetadata CreateMetadata()
        {
            TextStudioTagParameterDefinition[] definitions =
                TextStudioTypedEffectParameters<TParameters>.CreateDefinitions(
                    in _defaultParameters,
                    out _metadataValidationError);
            return new TextStudioEffectMetadata(
                MetadataCategory,
                MetadataDisplayName,
                MetadataDescription,
                MetadataMarkupSnippet,
                MetadataSuggestedPhases,
                definitions);
        }

        void EnsureDefaultsInitialized()
        {
            EnsureIdentityInitialized();
            if (_parameterDefaultsInitialized)
                return;
            _defaultParameters = CreateDefaultParameters();
            _parameterDefaultsInitialized = true;
            _metadata = null;
        }

        [Preserve]
        sealed class Evaluator : ITextStudioEffect, ITextStudioEffectCapabilitiesProvider
        {
            readonly TextStudioCodeEffectAsset<TState, TParameters> _definition;
            readonly TParameters _parameters;
            TState[] _states;
            int _stateCount;

            public string Id => _definition.Id;
            public TextStudioEffectAreaSettings DefaultArea => _definition.DefaultArea;
            public TextStudioEffectCapabilities Capabilities => _definition.Capabilities;

            internal Evaluator(
                TextStudioCodeEffectAsset<TState, TParameters> definition,
                in TParameters parameters)
            {
                _definition = definition;
                _parameters = parameters;
            }

            public void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
            {
                EnsureStates(context.totalGlyphs);
                ref TState effectState = ref _states[context.glyphIndex];
                _definition.EvaluateEffect(
                    ref state,
                    ref effectState,
                    in _parameters,
                    in context,
                    _definition.BlendMode);
            }

            public void Reset()
            {
                _stateCount = 0;
            }

            void EnsureStates(int count)
            {
                if (_states == null || _states.Length < count)
                    _states = new TState[TextStudioBufferCapacity.ForCount(count)];
                if (_stateCount >= count)
                    return;
                for (int i = _stateCount; i < count; i++)
                    _states[i] = _definition.CreateDefaultState();
                _stateCount = count;
            }
        }
    }
}
