using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioCompositionAuthoringSpace
    {
        TwoD,
        ThreeD
    }

    public enum TextStudioCompositionLayerKind
    {
        Channel,
        Effect,
        Writing
    }

    public enum TextStudioCompositionChannel
    {
        Position,
        Rotation,
        Scale,
        Alpha,
        Color,
        Skew,
        UVOffset,
        Pivot,
        CornerDeformation
    }

    public enum TextStudioCompositionColorMode
    {
        Replace,
        MultiplyExistingColor
    }

    /// <summary>Serializable tag selector used to scope a composed-effect layer to authored semantic tags.</summary>
    [Serializable]
    public sealed class TextStudioCompositionTagSpec
    {
        [SerializeField] bool _enabled = true;
        [SerializeField, TextArea(1, 4)] string _openingMarkup = string.Empty;
        [SerializeField] TextStudioTagExpansionClosingMode _closingMode = TextStudioTagExpansionClosingMode.Auto;
        [SerializeField, TextArea(1, 4)] string _customClosingMarkup = string.Empty;

        public bool Enabled => _enabled;
        public string OpeningMarkup => _openingMarkup ?? string.Empty;
        public TextStudioTagExpansionClosingMode ClosingMode => _closingMode;
        public string CustomClosingMarkup => _customClosingMarkup ?? string.Empty;
        public string ClosingMarkup => _closingMode == TextStudioTagExpansionClosingMode.Custom
            ? CustomClosingMarkup
            : TextStudioTagExpansionUtility.DeriveClosingMarkup(OpeningMarkup);

        internal bool TryGetInvocations(List<string> invocations, out string error)
        {
            if (!_enabled || string.IsNullOrWhiteSpace(OpeningMarkup))
            {
                invocations.Clear();
                error = string.Empty;
                return true;
            }

            if (!TextStudioTransitionStyleUtility.TrySplitInvocations(OpeningMarkup, invocations, out error))
                return false;

            if (_closingMode != TextStudioTagExpansionClosingMode.Custom)
                return true;

            string derived = TextStudioTagExpansionUtility.DeriveClosingMarkup(OpeningMarkup);
            if (string.Equals(CustomClosingMarkup.Trim(), derived, StringComparison.Ordinal))
                return true;

            error = "Custom closing tags must close every opening effect in reverse order. Expected '" + derived + "'.";
            invocations.Clear();
            return false;
        }

        internal void InitializeForTests(
            string openingMarkup,
            TextStudioTagExpansionClosingMode closingMode = TextStudioTagExpansionClosingMode.Auto,
            string customClosingMarkup = "")
        {
            _enabled = true;
            _openingMarkup = openingMarkup ?? string.Empty;
            _closingMode = closingMode;
            _customClosingMarkup = customClosingMarkup ?? string.Empty;
        }

        internal void ConfigureForAuthoring(
            bool enabled,
            string openingMarkup,
            TextStudioTagExpansionClosingMode closingMode,
            string customClosingMarkup)
        {
            _enabled = enabled;
            _openingMarkup = openingMarkup ?? string.Empty;
            _closingMode = closingMode;
            _customClosingMarkup = customClosingMarkup ?? string.Empty;
        }
    }

    /// <summary>One ordered effect or channel layer with phase, area, color, and influence-field configuration.</summary>
    [Serializable]
    public sealed class TextStudioCompositionLayer : ITextStudioEffect,
        ITextStudioEffectCapabilitiesProvider
    {
        [SerializeField, HideInInspector] string _stableId = string.Empty;
        [SerializeField] string _name = "Position";
        [SerializeField] bool _enabled = true;
        [SerializeField] TextStudioCompositionLayerKind _kind = TextStudioCompositionLayerKind.Channel;
        [SerializeField] TextStudioCompositionChannel _channel = TextStudioCompositionChannel.Position;
        [SerializeField, HideInInspector] string _renderPropertyId = string.Empty;

        [SerializeField] string _effectId = TextStudioBuiltInEffectIds.Wave;
        [SerializeField, TextArea(1, 3)] string _effectParameters = string.Empty;
        [SerializeField] List<TextStudioCompositionParameterEntry> _effectParameterEntries =
            new List<TextStudioCompositionParameterEntry>();
        [SerializeField, HideInInspector] bool _effectParameterSnapshotInitialized;
        [SerializeField, HideInInspector] string _effectParameterSnapshotEffectId = string.Empty;
        [SerializeField, HideInInspector] bool _effectEnvelopeInitialized;

        [SerializeField] TextStudioWritingStyleAsset _writingStyle;
        [NonSerialized] CompiledTextStudioWritingStyle _compiledWritingStyle;
        [NonSerialized] ulong _writingDependencyRevision;
        [NonSerialized] string _compositionOwnerId = string.Empty;

        [SerializeField] TextStudioPhaseMask _phases = TextStudioPhaseMask.Active;
        [SerializeField] TextStudioEffectPlaybackPreset _playback = TextStudioEffectPlaybackPreset.Loop;
        [SerializeField] EffectPlaybackAsset _customPlayback;
        [SerializeField] TextStudioEffectCurvePreset _curve = TextStudioEffectCurvePreset.Sine;
        [SerializeField] TextStudioCurveProfile _customCurveAsset;
        [SerializeField] AnimationCurve _customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] Vector3 _vector = new Vector3(0f, 0.15f, 0f);
        [SerializeField] Vector2 _bottomLeftCorner;
        [SerializeField] Vector2 _topLeftCorner = new Vector2(-0.08f, 0f);
        [SerializeField] Vector2 _topRightCorner = new Vector2(0.08f, 0f);
        [SerializeField] Vector2 _bottomRightCorner;
        [SerializeField] TextStudioCompositionColorMode _colorMode = TextStudioCompositionColorMode.MultiplyExistingColor;
        [SerializeField] Gradient _colorGradient = CreateDefaultColorGradient();
        [SerializeField] float _scalar;
        [SerializeField] uint _unsigned = 1u;
        [SerializeField, Range(0f, 1f)] float _intensity = 1f;
        [SerializeField] float _timeOffset;
        [SerializeField] PhaseParams _timing = default;
        [SerializeField] TextStudioSequenceSettings _sequence = default;
        [SerializeField] TextStudioEffectAreaSettings _area = default;
        [SerializeField] EffectBlendMode _blendMode = EffectBlendMode.Additive;
        [SerializeField, Tooltip("Renderer surfaces affected by extension properties that support surface targeting.")]
        TextStudioSurfaceMask _surfaceMask = TextStudioSurfaceMask.All;
        [SerializeField, Tooltip("Optional spatial masks that control where this layer is visible in source, placed, or world space.")]
        TextStudioInfluenceFieldStack _spatialInfluence = new TextStudioInfluenceFieldStack();
        [SerializeField, Tooltip("Optional filter based on each glyph's immutable original source color.")]
        TextStudioSourceColorSelector _sourceColorSelector = new TextStudioSourceColorSelector();
        [SerializeField, Tooltip("Logical text units selected for this layer. Ranges use the enclosing effect's text as their origin.")]
        TextStudioApplicationSelector _selector;
        [NonSerialized] string _cachedMetadataEffectId = string.Empty;
        [NonSerialized] TextStudioEffectMetadata _cachedEffectMetadata;
        [NonSerialized] uint _cachedMetadataRegistryRevision;

        public string Id => "__composed_" + StableId;
        public string StableId => string.IsNullOrEmpty(_stableId) ? "layer" : _stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(_name) ? ResolveChannelDisplayName() : _name.Trim();
        public bool Enabled => _enabled;
        public TextStudioCompositionLayerKind Kind => _kind;
        public TextStudioCompositionChannel Channel => _channel;
        public string RenderPropertyId => _renderPropertyId ?? string.Empty;
        public bool UsesRenderProperty => !string.IsNullOrEmpty(RenderPropertyId);
        public TextStudioCompositionColorMode ColorMode => _colorMode;
        public Gradient ColorGradient => _colorGradient;
        public EffectPlaybackAsset CustomPlayback => _customPlayback;
        public TextStudioCurveProfile CustomCurveAsset => _customCurveAsset;
        public string EffectId => _effectId ?? string.Empty;
        public string EffectParameters => _effectParameters ?? string.Empty;
        public TextStudioWritingStyleAsset WritingStyle => _writingStyle;
        internal string WritingPropertyId =>
            TextStudioComposedWritingRegistry.BuildPropertyId(
                _compositionOwnerId,
                StableId);
        internal IReadOnlyList<TextStudioCompositionParameterEntry> ParameterEntries =>
            _effectParameterEntries ??= new List<TextStudioCompositionParameterEntry>();
        public TextStudioPhaseMask Phases => _kind == TextStudioCompositionLayerKind.Writing ? TextStudioPhaseMask.All : _phases;
        public float Intensity => Mathf.Clamp01(_intensity);
        public float TimeOffset => _kind == TextStudioCompositionLayerKind.Writing ? 0f : _timeOffset;
        public PhaseParams Timing => _kind == TextStudioCompositionLayerKind.Writing ? PhaseParams.NoOffset : NormalizeTiming(_timing);
        public TextStudioSequenceSettings Sequence => _kind == TextStudioCompositionLayerKind.Writing || _sequence.Equals(default)
            ? TextStudioSequenceSettings.Unstaggered
            : _sequence;
        public TextStudioEffectAreaSettings DefaultArea => _area.Equals(default)
            ? TextStudioEffectAreaSettings.Default
            : _area;
        public EffectBlendMode BlendMode => _blendMode;
        public TextStudioSurfaceMask SurfaceMask => _surfaceMask;
        public Vector3 Vector => _vector;
        public uint Unsigned => _unsigned;
        public TextStudioInfluenceFieldStack SpatialInfluence =>
            _spatialInfluence ??= new TextStudioInfluenceFieldStack();
        public TextStudioSourceColorSelector SourceColorSelector =>
            _sourceColorSelector ??= new TextStudioSourceColorSelector();
        public TextStudioApplicationSelector Selector => _selector;
        internal bool UsesProgressPlayback =>
            ResolveEffectMetadata()?.PlaybackRole == TextStudioEffectPlaybackRole.Progress;
        internal bool HasConstantFullEnvelope =>
            _curve == TextStudioEffectCurvePreset.Hold;
        public TextStudioEffectCapabilities Capabilities
        {
            get
            {
                if (_kind == TextStudioCompositionLayerKind.Writing)
                {
                    TextStudioReducedMotionBehavior reducedMotion =
                        _writingStyle != null
                            ? _writingStyle.Configuration.ReducedMotionBehavior
                            : TextStudioReducedMotionBehavior.Reduce;
                    return new TextStudioEffectCapabilities(
                        TextStudioRenderChannels.None,
                        Phases,
                        TextStudioEffectPerformanceTier.Realtime,
                        reducedMotion,
                        supportedRenderDomains:
                            TextStudioRenderDomain.FlatText |
                            TextStudioRenderDomain.Extruded3D);
                }

                if (_kind == TextStudioCompositionLayerKind.Channel && UsesRenderProperty)
                {
                    if (TextStudioRenderPropertyRegistry.TryGet(
                            RenderPropertyId,
                            out TextStudioRenderPropertyDescriptor descriptor))
                    {
                        return new TextStudioEffectCapabilities(
                            TextStudioRenderChannels.None,
                            _phases,
                            TextStudioRenderPropertyCapabilities.ResolvePerformanceTier(
                                descriptor.PerformanceClass),
                            descriptor.ReducedMotionBehavior,
                            supportedRenderDomains: descriptor.SupportedRenderDomains,
                            boundsBehavior: descriptor.BoundsBehavior);
                    }

                    return new TextStudioEffectCapabilities(
                        TextStudioRenderChannels.None,
                        _phases,
                        supportedRenderDomains: TextStudioRenderDomain.None);
                }

                return new TextStudioEffectCapabilities(
                    ResolveRenderChannels(_channel),
                    _phases);
            }
        }

        static TextStudioRenderChannels ResolveRenderChannels(
            TextStudioCompositionChannel channel)
        {
            return channel switch
            {
                TextStudioCompositionChannel.Position => TextStudioRenderChannels.Position,
                TextStudioCompositionChannel.Rotation => TextStudioRenderChannels.Rotation,
                TextStudioCompositionChannel.Scale => TextStudioRenderChannels.Scale,
                TextStudioCompositionChannel.Alpha => TextStudioRenderChannels.Alpha,
                TextStudioCompositionChannel.Color => TextStudioRenderChannels.Color,
                TextStudioCompositionChannel.Skew => TextStudioRenderChannels.Skew,
                TextStudioCompositionChannel.UVOffset => TextStudioRenderChannels.UvOffset,
                TextStudioCompositionChannel.Pivot => TextStudioRenderChannels.Pivot,
                TextStudioCompositionChannel.CornerDeformation =>
                    TextStudioRenderChannels.CornerOffsets,
                _ => TextStudioRenderChannels.None
            };
        }

        internal bool HasNonPlanarValues
        {
            get
            {
                if (_kind != TextStudioCompositionLayerKind.Channel || UsesRenderProperty)
                    return false;
                return _channel switch
                {
                    TextStudioCompositionChannel.Position => !Mathf.Approximately(_vector.z, 0f),
                    TextStudioCompositionChannel.Rotation =>
                        !Mathf.Approximately(_vector.x, 0f) || !Mathf.Approximately(_vector.y, 0f),
                    TextStudioCompositionChannel.Scale => !Mathf.Approximately(_vector.z, 1f),
                    _ => false
                };
            }
        }

        internal void ResetNonPlanarValues()
        {
            if (_kind != TextStudioCompositionLayerKind.Channel || UsesRenderProperty)
                return;
            switch (_channel)
            {
                case TextStudioCompositionChannel.Position:
                    _vector.z = 0f;
                    break;
                case TextStudioCompositionChannel.Rotation:
                    _vector.x = 0f;
                    _vector.y = 0f;
                    break;
                case TextStudioCompositionChannel.Scale:
                    _vector.z = 1f;
                    break;
            }
        }

        internal string GetNonPlanarValuesWarning(string subject)
        {
            return GetNonPlanarValuesWarning(_channel, subject);
        }

        internal static string GetNonPlanarValuesWarning(
            TextStudioCompositionChannel channel,
            string subject)
        {
            string prefix = string.IsNullOrWhiteSpace(subject) ? "This layer" : subject;
            return channel switch
            {
                TextStudioCompositionChannel.Position =>
                    prefix + " has a hidden Z position value. In 2D, Z position must be 0. " +
                    "Use Fix for 2D or switch Authoring Space to 3D.",
                TextStudioCompositionChannel.Rotation =>
                    prefix + " has hidden X or Y rotation values. In 2D, X and Y rotation must be 0. " +
                    "Use Fix for 2D or switch Authoring Space to 3D.",
                TextStudioCompositionChannel.Scale =>
                    prefix + " has a hidden Z scale value. In 2D, Z scale must be 1. " +
                    "Use Fix for 2D or switch Authoring Space to 3D.",
                _ =>
                    prefix + " contains values that are not available in 2D. " +
                    "Use Fix for 2D or switch Authoring Space to 3D."
            };
        }

        /// <summary>Creates a layer targeting one built-in renderer-neutral channel.</summary>
        public static TextStudioCompositionLayer CreateChannel(TextStudioCompositionChannel channel)
        {
            TextStudioCompositionLayer layer = new TextStudioCompositionLayer
            {
                _kind = TextStudioCompositionLayerKind.Channel,
                _channel = channel,
                _name = ObjectNamesForRuntime(channel),
                _timing = PhaseParams.NoOffset,
                _area = TextStudioEffectAreaSettings.Default
            };
            layer.ApplyChannelDefaults(channel);
            layer.EnsureInitialized();
            return layer;
        }

        /// <summary>Creates a layer targeting a registered render property by stable namespaced ID.</summary>
        public static TextStudioCompositionLayer CreateRenderProperty(string propertyId)
        {
            TextStudioCompositionLayer layer = new TextStudioCompositionLayer
            {
                _kind = TextStudioCompositionLayerKind.Channel,
                _renderPropertyId = propertyId?.Trim() ?? string.Empty,
                _timing = PhaseParams.NoOffset,
                _area = TextStudioEffectAreaSettings.Default
            };
            layer.ApplyDescriptorDefaults();
            layer.EnsureInitialized();
            return layer;
        }

        /// <summary>Creates a layer that invokes a registered effect definition by stable ID.</summary>
        public static TextStudioCompositionLayer CreateEffect(string effectId)
        {
            TextStudioCompositionLayer layer = new TextStudioCompositionLayer
            {
                _kind = TextStudioCompositionLayerKind.Effect,
                _effectId = string.IsNullOrWhiteSpace(effectId) ? TextStudioBuiltInEffectIds.Wave : effectId.Trim(),
                _name = string.IsNullOrWhiteSpace(effectId) ? "Wave" : effectId.Trim(),
                _phases = TextStudioPhaseMask.Active,
                _playback = TextStudioEffectPlaybackPreset.External,
                _curve = TextStudioEffectCurvePreset.Hold,
                _effectEnvelopeInitialized = true,
                _timing = PhaseParams.NoOffset,
                _area = TextStudioEffectAreaSettings.Default
            };
            layer.EnsureInitialized();
            if (TextStudioEffects.TryGetDefinition(
                    layer.EffectId,
                    out ITextStudioEffectDefinition definition) &&
                definition is ITextStudioEffectMetadataProvider provider &&
                provider.Metadata != null)
            {
                layer._area = definition.DefaultArea;
                layer.ApplyMetadataForEffectSelection(provider.Metadata);
            }
            layer._effectParameterSnapshotEffectId = layer.EffectId;
            layer.SynchronizeParameterSnapshot(true);
            return layer;
        }

        /// <summary>Creates a layer that schedules one shared writing style through composition phases.</summary>
        public static TextStudioCompositionLayer CreateWriting(
            TextStudioWritingStyleAsset writingStyle)
        {
            TextStudioCompositionLayer layer = new TextStudioCompositionLayer
            {
                _kind = TextStudioCompositionLayerKind.Writing,
                _writingStyle = writingStyle,
                _name = writingStyle != null ? writingStyle.name : "Writing",
                _phases = TextStudioPhaseMask.Reveal,
                _playback = TextStudioEffectPlaybackPreset.External,
                _curve = TextStudioEffectCurvePreset.Linear,
                _effectEnvelopeInitialized = true,
                _timing = PhaseParams.NoOffset,
                _area = TextStudioEffectAreaSettings.Default
            };
            layer.EnsureInitialized();
            return layer;
        }

        internal void EnsureInitialized()
        {
            if (string.IsNullOrEmpty(_stableId))
                _stableId = Guid.NewGuid().ToString("N");
            if (_timing.speed <= 0f)
                _timing = PhaseParams.NoOffset;
            if (_area.Equals(default))
                _area = TextStudioEffectAreaSettings.Default;
            if (_sequence.Equals(default))
                _sequence = TextStudioSequenceSettings.Unstaggered;
            _customCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _effectParameterEntries ??= new List<TextStudioCompositionParameterEntry>();
            _colorGradient ??= CreateDefaultColorGradient();
            _spatialInfluence ??= new TextStudioInfluenceFieldStack();
            _sourceColorSelector ??= new TextStudioSourceColorSelector();
            if (_kind != TextStudioCompositionLayerKind.Writing)
                TextStudioComposedWritingRegistry.Unregister(this);
            else
            {
                _phases = TextStudioPhaseMask.All;
                _timeOffset = 0f;
                _timing = PhaseParams.NoOffset;
                _sequence = TextStudioSequenceSettings.Unstaggered;
                _playback = TextStudioEffectPlaybackPreset.External;
                _customPlayback = null;
            }
            if (_kind == TextStudioCompositionLayerKind.Effect && !_effectEnvelopeInitialized)
            {
                _playback = TextStudioEffectPlaybackPreset.External;
                _curve = TextStudioEffectCurvePreset.Hold;
                _effectEnvelopeInitialized = true;
            }
        }

        internal void ConfigureChannelForAuthoring(
            string stableId,
            string displayName,
            TextStudioCompositionChannel channel,
            TextStudioEffectPlaybackPreset playback,
            TextStudioEffectCurvePreset curve,
            Vector3 vector,
            float scalar,
            Gradient colorGradient,
            TextStudioCompositionColorMode colorMode,
            TextStudioPhaseMask phases,
            float intensity,
            float timeOffset,
            PhaseParams timing,
            TextStudioEffectAreaSettings area,
            EffectBlendMode blendMode)
        {
            _stableId = stableId ?? string.Empty;
            _name = displayName ?? string.Empty;
            _enabled = true;
            _kind = TextStudioCompositionLayerKind.Channel;
            _channel = channel;
            _renderPropertyId = string.Empty;
            _playback = playback;
            _curve = curve;
            _customCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _vector = vector;
            _scalar = scalar;
            _unsigned = 1u;
            _colorGradient = colorGradient ?? CreateDefaultColorGradient();
            _colorMode = colorMode;
            _phases = phases;
            _intensity = Mathf.Clamp01(intensity);
            _timeOffset = timeOffset;
            _timing = timing;
            _area = area;
            _blendMode = blendMode;
            EnsureInitialized();
        }

        internal void ConfigureSelectorForAuthoring(TextStudioApplicationSelector selector)
        {
            _selector = selector;
        }

        internal void ConfigureRenderPropertyForAuthoring(
            string stableId,
            string displayName,
            string propertyId,
            TextStudioEffectPlaybackPreset playback,
            TextStudioEffectCurvePreset curve,
            Vector3 vector,
            float scalar,
            Gradient colorGradient,
            TextStudioCompositionColorMode colorMode,
            TextStudioPhaseMask phases,
            float intensity,
            float timeOffset,
            PhaseParams timing,
            TextStudioEffectAreaSettings area,
            EffectBlendMode blendMode,
            uint unsigned = 1u)
        {
            ConfigureChannelForAuthoring(
                stableId,
                displayName,
                TextStudioCompositionChannel.Position,
                playback,
                curve,
                vector,
                scalar,
                colorGradient,
                colorMode,
                phases,
                intensity,
                timeOffset,
                timing,
                area,
                blendMode);
            _renderPropertyId = propertyId?.Trim() ?? string.Empty;
            _unsigned = unsigned;
        }

        internal void ConfigureEffectForAuthoring(
            string stableId,
            string displayName,
            string effectId,
            string parameters,
            TextStudioPhaseMask phases,
            float intensity,
            float timeOffset,
            TextStudioEffectAreaSettings area)
        {
            ConfigureEffectForAuthoring(
                stableId,
                displayName,
                effectId,
                parameters,
                phases,
                intensity,
                timeOffset,
                area,
                PhaseParams.NoOffset);
        }

        internal void ConfigureEffectForAuthoring(
            string stableId,
            string displayName,
            string effectId,
            string parameters,
            TextStudioPhaseMask phases,
            float intensity,
            float timeOffset,
            TextStudioEffectAreaSettings area,
            PhaseParams timing)
        {
            _stableId = stableId ?? string.Empty;
            _name = displayName ?? string.Empty;
            _enabled = true;
            _kind = TextStudioCompositionLayerKind.Effect;
            _effectId = string.IsNullOrWhiteSpace(effectId) ? TextStudioBuiltInEffectIds.Wave : effectId.Trim();
            _effectParameters = parameters ?? string.Empty;
            _effectParameterEntries ??= new List<TextStudioCompositionParameterEntry>();
            _effectParameterEntries.Clear();
            _effectParameterSnapshotInitialized = false;
            _effectParameterSnapshotEffectId = _effectId;
            _effectEnvelopeInitialized = true;
            _playback = TextStudioEffectPlaybackPreset.External;
            _curve = TextStudioEffectCurvePreset.Hold;
            _phases = phases;
            _intensity = Mathf.Clamp01(intensity);
            _timeOffset = timeOffset;
            _area = area;
            _timing = timing;
            EnsureInitialized();
            TextStudioEffectMetadata metadata = ResolveEffectMetadata();
            if (metadata != null)
            {
                _playback = metadata.DefaultPlayback;
                _curve = metadata.DefaultCurve;
            }
            SynchronizeParameterSnapshot(false);
        }

        internal void ConfigureWritingForAuthoring(
            string stableId,
            string displayName,
            TextStudioWritingStyleAsset writingStyle,
            float intensity,
            TextStudioEffectAreaSettings area)
        {
            _stableId = stableId ?? string.Empty;
            _name = displayName ?? string.Empty;
            _enabled = true;
            _kind = TextStudioCompositionLayerKind.Writing;
            _writingStyle = writingStyle;
            _intensity = Mathf.Clamp01(intensity);
            _area = area;
            _playback = TextStudioEffectPlaybackPreset.External;
            _curve = TextStudioEffectCurvePreset.Linear;
            _effectEnvelopeInitialized = true;
            _compiledWritingStyle = null;
            _writingDependencyRevision = 0UL;
            EnsureInitialized();
        }

        internal void SetWritingStyleForAuthoring(
            TextStudioWritingStyleAsset writingStyle)
        {
            _writingStyle = writingStyle;
            _compiledWritingStyle = null;
            _writingDependencyRevision = 0UL;
            if (string.IsNullOrWhiteSpace(_name) ||
                string.Equals(_name, "Writing", StringComparison.Ordinal))
            {
                _name = writingStyle != null ? writingStyle.name : "Writing";
            }
        }

        internal void SetCompositionOwnerId(string compositionId)
        {
            string resolved = compositionId?.Trim() ?? string.Empty;
            if (string.Equals(
                    _compositionOwnerId,
                    resolved,
                    StringComparison.Ordinal))
            {
                return;
            }
            TextStudioComposedWritingRegistry.Unregister(this);
            _compositionOwnerId = resolved;
        }

        internal void ConfigureChannelShapeForAuthoring(
            AnimationCurve customCurve,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight)
        {
            _customCurve = customCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _bottomLeftCorner = bottomLeft;
            _topLeftCorner = topLeft;
            _topRightCorner = topRight;
            _bottomRightCorner = bottomRight;
        }

        internal void ConfigureSequenceForAuthoring(TextStudioSequenceSettings sequence)
        {
            _sequence = sequence.Equals(default)
                ? TextStudioSequenceSettings.Unstaggered
                : sequence;
        }

        internal void ConfigureEnvelopeForAuthoring(
            TextStudioEffectPlaybackPreset playback,
            EffectPlaybackAsset customPlayback,
            TextStudioEffectCurvePreset curve,
            TextStudioCurveProfile customCurveAsset,
            AnimationCurve inlineCurve)
        {
            _playback = playback;
            _customPlayback = customPlayback;
            _curve = curve;
            _customCurveAsset = customCurveAsset;
            _customCurve = inlineCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            _effectEnvelopeInitialized = true;
        }

        internal bool SynchronizeParameterSnapshot(bool resetKnownParameters)
        {
            if (_kind != TextStudioCompositionLayerKind.Effect)
                return false;

            _effectParameterEntries ??= new List<TextStudioCompositionParameterEntry>();
            bool effectChanged = !string.IsNullOrWhiteSpace(_effectParameterSnapshotEffectId) &&
                                 !string.Equals(
                _effectParameterSnapshotEffectId,
                EffectId,
                StringComparison.OrdinalIgnoreCase);
            if (!TextStudioEffects.TryGetDefinition(
                    EffectId,
                    out ITextStudioEffectDefinition definition) ||
                definition is not ITextStudioEffectMetadataProvider provider ||
                provider.Metadata == null)
            {
                if (effectChanged || resetKnownParameters)
                    _effectParameterEntries.Clear();
                _effectParameterSnapshotInitialized = true;
                _effectParameterSnapshotEffectId = EffectId;
                return effectChanged || resetKnownParameters;
            }

            TextStudioEffectMetadata metadata = provider.Metadata;
            IReadOnlyList<TextStudioTagParameterDefinition> definitions = metadata.Parameters;
            bool changed = false;
            if (effectChanged)
            {
                _effectParameterEntries.Clear();
                _effectParameterSnapshotInitialized = false;
                _area = definition.DefaultArea;
                ApplyMetadataForEffectSelection(metadata);
                changed = true;
            }

            if (!_effectParameterSnapshotInitialized)
            {
                MigrateAdvancedKnownParameters(definitions);
                changed = true;
            }

            for (int entryIndex = _effectParameterEntries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                TextStudioCompositionParameterEntry entry = _effectParameterEntries[entryIndex];
                TextStudioTagParameterDefinition definitionForEntry =
                    FindDefinition(definitions, entry != null ? entry.Name : string.Empty);
                if (definitionForEntry != null &&
                    !TextStudioEffectParameterOwnership.IsOwnedByProgressLayer(
                        metadata,
                        definitionForEntry.Name))
                {
                    entry.CanonicalizeName(definitionForEntry.Name);
                    continue;
                }

                if (definitionForEntry == null &&
                    entry != null &&
                    !string.IsNullOrWhiteSpace(entry.Name))
                {
                    AppendAdvancedParameter(entry.GetAuthoredParameterSource());
                }
                _effectParameterEntries.RemoveAt(entryIndex);
                changed = true;
            }

            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                TextStudioTagParameterDefinition parameterDefinition = definitions[definitionIndex];
                if (TextStudioEffectParameterOwnership.IsOwnedByProgressLayer(
                        metadata,
                        parameterDefinition.Name))
                {
                    continue;
                }
                TextStudioCompositionParameterEntry entry = FindEntry(parameterDefinition.Name);
                if (entry == null)
                {
                    _effectParameterEntries.Insert(
                        Mathf.Min(definitionIndex, _effectParameterEntries.Count),
                        TextStudioCompositionParameterEntry.FromDefault(parameterDefinition));
                    changed = true;
                    continue;
                }

                if (resetKnownParameters)
                {
                    entry.ResetTo(parameterDefinition.DefaultValue);
                    changed = true;
                }
            }

            _effectParameterEntries.Sort((left, right) =>
                GetDefinitionIndex(definitions, left != null ? left.Name : string.Empty).CompareTo(
                    GetDefinitionIndex(definitions, right != null ? right.Name : string.Empty)));
            _effectParameterSnapshotInitialized = true;
            _effectParameterSnapshotEffectId = EffectId;
            return changed;
        }

        void ApplyMetadataForEffectSelection(TextStudioEffectMetadata metadata)
        {
            if (metadata == null)
                return;
            if (metadata.SuggestedPhases != TextStudioPhaseMask.None)
                _phases = metadata.SuggestedPhases;
            _playback = metadata.DefaultPlayback;
            _curve = metadata.DefaultCurve;
            if (string.IsNullOrWhiteSpace(_name) ||
                string.Equals(_name, _effectParameterSnapshotEffectId, StringComparison.OrdinalIgnoreCase))
            {
                _name = string.IsNullOrWhiteSpace(metadata.DisplayName)
                    ? EffectId
                    : metadata.DisplayName;
            }
        }

        internal bool ResetParameterToCurrentDefault(string canonicalName)
        {
            if (!TextStudioEffects.TryGetDefinition(
                    EffectId,
                    out ITextStudioEffectDefinition definition) ||
                definition is not ITextStudioEffectMetadataProvider provider)
            {
                return false;
            }

            TextStudioTagParameterDefinition parameter =
                FindDefinition(provider.Metadata.Parameters, canonicalName);
            TextStudioCompositionParameterEntry entry = FindEntry(canonicalName);
            if (parameter == null || entry == null)
                return false;
            entry.ResetTo(parameter.DefaultValue);
            return true;
        }

        void MigrateAdvancedKnownParameters(
            IReadOnlyList<TextStudioTagParameterDefinition> definitions)
        {
            TextStudioTagParameters parsed = TextStudioTagParameterParser.Parse(
                _effectParameters ?? string.Empty,
                allowNumericModifiers: true);
            bool[] claimedPositionals = new bool[parsed.PositionalCount];
            TextStudioEffectMetadata metadata = ResolveEffectMetadata();
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                TextStudioTagParameterDefinition definition = definitions[definitionIndex];
                if (TextStudioEffectParameterOwnership.IsOwnedByProgressLayer(
                        metadata,
                        definition.Name))
                {
                    continue;
                }
                TextStudioCompositionParameterEntry entry = FindEntry(definition.Name) ??
                    TextStudioCompositionParameterEntry.FromDefault(definition);
                bool found = false;
                for (int namedIndex = parsed.NamedCount - 1; namedIndex >= 0; namedIndex--)
                {
                    TextStudioNamedTagParameter parameter = parsed.Named[namedIndex];
                    if (!definition.MatchesName(parameter.Name))
                        continue;
                    entry.SetAuthored(parameter.Value, parameter.Operation, definition.DefaultValue);
                    found = true;
                    break;
                }
                if (!found &&
                    definition.PositionalIndex >= 0 &&
                    definition.PositionalIndex < parsed.PositionalCount)
                {
                    entry.SetAuthored(
                        parsed.Positional[definition.PositionalIndex],
                        TextStudioTagParameterOperation.Set,
                        definition.DefaultValue);
                    claimedPositionals[definition.PositionalIndex] = true;
                }
                if (FindEntry(definition.Name) == null)
                    _effectParameterEntries.Add(entry);
            }

            List<string> remainingPositionals = new List<string>();
            for (int i = 0; i < parsed.PositionalCount; i++)
            {
                if (!claimedPositionals[i])
                    remainingPositionals.Add(parsed.Positional[i]);
            }
            List<TextStudioNamedTagParameter> remainingNamed =
                new List<TextStudioNamedTagParameter>();
            for (int i = 0; i < parsed.NamedCount; i++)
            {
                TextStudioNamedTagParameter parameter = parsed.Named[i];
                if (FindDefinition(definitions, parameter.Name) == null)
                    remainingNamed.Add(parameter);
            }
            _effectParameters = TextStudioEffectParameterUtility.BuildRawParameterString(
                remainingPositionals,
                remainingNamed);
        }

        void AppendAdvancedParameter(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;
            _effectParameters = string.IsNullOrWhiteSpace(_effectParameters)
                ? source.Trim()
                : _effectParameters.Trim() + " " + source.Trim();
        }

        TextStudioCompositionParameterEntry FindEntry(string name)
        {
            for (int i = 0; i < _effectParameterEntries.Count; i++)
            {
                TextStudioCompositionParameterEntry entry = _effectParameterEntries[i];
                if (entry != null && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        static TextStudioTagParameterDefinition FindDefinition(
            IReadOnlyList<TextStudioTagParameterDefinition> definitions,
            string name)
        {
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                TextStudioTagParameterDefinition definition = definitions[i];
                if (definition != null && definition.MatchesName(name))
                    return definition;
            }
            return null;
        }

        static int GetDefinitionIndex(
            IReadOnlyList<TextStudioTagParameterDefinition> definitions,
            string name)
        {
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].MatchesName(name))
                    return i;
            }
            return int.MaxValue;
        }

        internal bool TryGetEffectInvocation(out TextStudioEffectInvocation invocation, out string error)
        {
            SynchronizeParameterSnapshot(false);
            if (!TryGetEffectInvocationSource(out string source, out error))
            {
                invocation = default;
                return false;
            }
            return TextStudioEffectInvocationParser.TryParse(source, out invocation, out error);
        }

        internal string GetEffectInvocationSource()
        {
            SynchronizeParameterSnapshot(false);
            if (TryGetEffectInvocationSource(out string source, out _))
                return source;
            source = "<" + EffectId;
            if (!string.IsNullOrWhiteSpace(EffectParameters))
                source += " " + EffectParameters.Trim();
            return source + ">";
        }

        bool TryGetEffectInvocationSource(out string source, out string error)
        {
            source = "<" + EffectId;
            error = string.Empty;
            List<TextStudioNamedTagParameter> structured =
                new List<TextStudioNamedTagParameter>(_effectParameterEntries.Count);
            IReadOnlyList<TextStudioTagParameterDefinition> definitions =
                Array.Empty<TextStudioTagParameterDefinition>();
            if (TextStudioEffects.TryGetDefinition(
                    EffectId,
                    out ITextStudioEffectDefinition definition) &&
                definition is ITextStudioEffectMetadataProvider provider)
            {
                definitions = provider.Metadata.Parameters;
            }

            for (int entryIndex = 0; entryIndex < _effectParameterEntries.Count; entryIndex++)
            {
                TextStudioCompositionParameterEntry entry = _effectParameterEntries[entryIndex];
                TextStudioTagParameterDefinition parameterDefinition =
                    FindDefinition(definitions, entry != null ? entry.Name : string.Empty);
                if (entry == null || parameterDefinition == null)
                    continue;
                if (!entry.TryCreateRuntimeParameter(
                        parameterDefinition,
                        out TextStudioNamedTagParameter runtimeParameter,
                        out error))
                {
                    return false;
                }
                structured.Add(runtimeParameter);
            }

            TextStudioTagParameters advanced = TextStudioTagParameterParser.Parse(
                _effectParameters ?? string.Empty,
                allowNumericModifiers: true);
            for (int i = 0; i < advanced.NamedCount; i++)
            {
                TextStudioNamedTagParameter parameter = advanced.Named[i];
                if (FindDefinition(definitions, parameter.Name) == null)
                    continue;
                error = "Advanced Parameters duplicates the structured parameter '" +
                    FindDefinition(definitions, parameter.Name).DisplayName + "'.";
                return false;
            }
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                int positionalIndex = definitions[definitionIndex].PositionalIndex;
                if (positionalIndex < 0 || positionalIndex >= advanced.PositionalCount)
                    continue;
                error = "Advanced Parameters duplicates the structured parameter '" +
                    definitions[definitionIndex].DisplayName + "' by position.";
                return false;
            }

            string structuredSource = TextStudioEffectParameterUtility.BuildRawParameterString(
                Array.Empty<string>(),
                structured);
            if (!string.IsNullOrWhiteSpace(structuredSource))
                source += " " + structuredSource;
            if (!string.IsNullOrWhiteSpace(EffectParameters))
                source += " " + EffectParameters.Trim();
            source += ">";
            return true;
        }

        /// <summary>Evaluates this layer's semantic scope, fields, timing, and payload into one glyph state.</summary>
        public void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (!_enabled)
                return;

            if (_kind == TextStudioCompositionLayerKind.Writing)
            {
                EvaluateWriting(in context);
                return;
            }

            if (_kind != TextStudioCompositionLayerKind.Channel)
                return;

            // The resolved binding already applies this layer's offset, speed, timing,
            // and sequence. Evaluate only the authored playback shape here.
            float progress = EvaluatePlayback(in context, true);
            if (TextStudioEffectCurveUtility.ShouldMirrorLoopProgress(_playback, _curve))
                progress = Mathf.PingPong(progress * 2f, 1f);
            float value = EvaluateCurve(progress);
            float influence = EvaluateInfluence(value);

            if (!string.IsNullOrEmpty(RenderPropertyId))
            {
                ApplyExtensionProperty(in context, progress, value, influence);
                return;
            }

            switch (_channel)
            {
                case TextStudioCompositionChannel.Position:
                    ApplyPosition(ref state, _vector * value, influence);
                    break;
                case TextStudioCompositionChannel.Rotation:
                    ApplyRotation(ref state, Quaternion.Euler(_vector * value), influence);
                    break;
                case TextStudioCompositionChannel.Scale:
                    ApplyScale(ref state, Vector3.LerpUnclamped(Vector3.one, _vector, value), influence);
                    break;
                case TextStudioCompositionChannel.Alpha:
                    ApplyAlpha(ref state, Mathf.Clamp01(_scalar), influence);
                    break;
                case TextStudioCompositionChannel.Color:
                    ApplyColor(
                        ref state,
                        _colorGradient.Evaluate(Mathf.Clamp01(progress)),
                        influence);
                    break;
                case TextStudioCompositionChannel.Skew:
                    ApplySkew(ref state, new Vector2(_vector.x, _vector.y) * value, influence);
                    break;
                case TextStudioCompositionChannel.UVOffset:
                    ApplyUvOffset(ref state, new Vector2(_vector.x, _vector.y) * value, influence);
                    break;
                case TextStudioCompositionChannel.Pivot:
                    ApplyPivot(ref state, new Vector2(_vector.x, _vector.y) * value, influence);
                    break;
                case TextStudioCompositionChannel.CornerDeformation:
                    ApplyCornerDeformation(ref state, value, influence);
                    break;
            }
        }

        public void Reset()
        {
        }

        void EvaluateWriting(in EffectEvaluationContext context)
        {
            CompiledTextStudioWritingStyle style = ResolveCompiledWritingStyle();
            if (style == null || !style.IsValid || style.IsPassThrough)
                return;

            TextStudioComposedWritingRegistry.EnsureRegistered(this);
            float resolved = EvaluateInfluence(1f);
            context.RenderProperties.Apply(
                WritingPropertyId,
                new Vector4(resolved, resolved, 0f, 0f),
                blendMode: EffectBlendMode.Override);
        }

        internal float EvaluateWritingCurve(float progress) => progress <= 0f ? 0f : progress >= 1f ? 1f : Mathf.Clamp01(EvaluateCurve(progress));

        internal CompiledTextStudioWritingStyle ResolveCompiledWritingStyle()
        {
            if (_writingStyle == null)
                return null;
            ulong revision = TextStudioWritingCompiler.ComputeDependencyRevision(
                _writingStyle);
            if (_compiledWritingStyle == null ||
                _writingDependencyRevision != revision)
            {
                _compiledWritingStyle = TextStudioWritingCompiler.Compile(_writingStyle);
                _writingDependencyRevision = revision;
            }
            return _compiledWritingStyle;
        }

        internal float EvaluateEnvelope(in EffectEvaluationContext context)
        {
            return EvaluateEnvelope(in context, false);
        }

        internal float EvaluateBoundEnvelope(in EffectEvaluationContext context)
        {
            return EvaluateEnvelope(in context, true);
        }

        internal float EvaluateBoundProgress(in EffectEvaluationContext context)
        {
            float progress = EvaluatePlayback(in context, true);
            return Mathf.Clamp01(EvaluateCurve(progress));
        }

        float EvaluateEnvelope(
            in EffectEvaluationContext context,
            bool timingAlreadyApplied)
        {
            float progress = EvaluatePlayback(in context, timingAlreadyApplied);
            if (TextStudioEffectCurveUtility.ShouldMirrorLoopProgress(_playback, _curve))
                progress = Mathf.PingPong(progress * 2f, 1f);
            return EvaluateInfluence(EvaluateCurve(progress));
        }

        internal TextStudioCompositionLayer Clone()
        {
            TextStudioCompositionLayer clone = JsonUtility.FromJson<TextStudioCompositionLayer>(JsonUtility.ToJson(this));
            clone.EnsureInitialized();
            clone.RegenerateStableIds();
            return clone;
        }

        void RegenerateStableIds()
        {
            _stableId = Guid.NewGuid().ToString("N");
        }

        void ApplyChannelDefaults(TextStudioCompositionChannel channel)
        {
            _phases = TextStudioPhaseMask.Active;
            _playback = TextStudioEffectPlaybackPreset.Loop;
            _curve = TextStudioEffectCurvePreset.Sine;
            _intensity = 1f;
            _scalar = 0f;
            _unsigned = 1u;
            _blendMode = EffectBlendMode.Additive;
            switch (channel)
            {
                case TextStudioCompositionChannel.Position:
                    _vector = new Vector3(0f, 0.15f, 0f);
                    break;
                case TextStudioCompositionChannel.Rotation:
                    _vector = new Vector3(0f, 0f, 12f);
                    break;
                case TextStudioCompositionChannel.Scale:
                    _vector = new Vector3(1.15f, 1.15f, 1f);
                    _blendMode = EffectBlendMode.Multiply;
                    break;
                case TextStudioCompositionChannel.Alpha:
                    _scalar = 0f;
                    _curve = TextStudioEffectCurvePreset.HalfSine;
                    _blendMode = EffectBlendMode.Multiply;
                    break;
                case TextStudioCompositionChannel.Color:
                    _colorMode = TextStudioCompositionColorMode.MultiplyExistingColor;
                    _colorGradient = CreateDefaultColorGradient();
                    _curve = TextStudioEffectCurvePreset.HalfSine;
                    _blendMode = EffectBlendMode.Lerp;
                    break;
                case TextStudioCompositionChannel.Skew:
                    _vector = new Vector3(0.12f, 0f, 0f);
                    break;
                case TextStudioCompositionChannel.UVOffset:
                    _vector = new Vector3(0.08f, 0f, 0f);
                    break;
                case TextStudioCompositionChannel.Pivot:
                    _vector = new Vector3(0f, 0.5f, 0f);
                    break;
                case TextStudioCompositionChannel.CornerDeformation:
                    _bottomLeftCorner = Vector2.zero;
                    _topLeftCorner = new Vector2(-0.08f, 0f);
                    _topRightCorner = new Vector2(0.08f, 0f);
                    _bottomRightCorner = Vector2.zero;
                    break;
            }
        }

        void ApplyDescriptorDefaults()
        {
            _phases = TextStudioPhaseMask.Active;
            _playback = TextStudioEffectPlaybackPreset.Loop;
            _curve = TextStudioEffectCurvePreset.Sine;
            _intensity = 1f;
            _blendMode = EffectBlendMode.Lerp;
            _scalar = 1f;
            _unsigned = 1u;
            _vector = Vector3.one;
            if (!TextStudioRenderPropertyRegistry.TryGet(RenderPropertyId, out TextStudioRenderPropertyDescriptor descriptor))
            {
                _name = string.IsNullOrEmpty(RenderPropertyId) ? "Extension Property" : RenderPropertyId;
                return;
            }

            _name = descriptor.DisplayName;
            _scalar = descriptor.RecommendedMaximum.x;
            _unsigned = descriptor.RecommendedMaximumUnsigned;
            _vector = new Vector3(
                descriptor.RecommendedMaximum.x,
                descriptor.RecommendedMaximum.y,
                descriptor.RecommendedMaximum.z);
            if (descriptor.ValueType == TextStudioRenderPropertyValueType.Color)
            {
                Color color = descriptor.RecommendedMaximum;
                _colorGradient = new Gradient();
                _colorGradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
            }
        }

        void ApplyExtensionProperty(
            in EffectEvaluationContext context,
            float progress,
            float value,
            float influence)
        {
            if (!TextStudioRenderPropertyRegistry.TryGet(
                    RenderPropertyId,
                    out TextStudioRenderPropertyDescriptor descriptor))
                return;

            if (descriptor.ValueType == TextStudioRenderPropertyValueType.UInt)
            {
                uint targetUnsigned = InterpolateUnsigned(
                    descriptor.DefaultUnsigned,
                    _unsigned,
                    value);
                context.RenderProperties.ApplyUInt(
                    RenderPropertyId,
                    targetUnsigned,
                    influence,
                    _blendMode,
                    descriptor.SupportsSurfaceMask
                        ? _surfaceMask
                        : TextStudioSurfaceMask.All);
                return;
            }

            Vector4 target = descriptor.ValueType switch
            {
                TextStudioRenderPropertyValueType.Float => new Vector4(_scalar, 0f, 0f, 0f),
                TextStudioRenderPropertyValueType.Vector2 => new Vector4(_vector.x, _vector.y, 0f, 0f),
                TextStudioRenderPropertyValueType.Vector3 => new Vector4(_vector.x, _vector.y, _vector.z, 0f),
                TextStudioRenderPropertyValueType.Color => (Vector4)_colorGradient.Evaluate(Mathf.Clamp01(progress)),
                _ => new Vector4(_vector.x, _vector.y, _vector.z, _scalar)
            };
            Vector4 animated = Vector4.LerpUnclamped(descriptor.DefaultValue, target, value);
            context.RenderProperties.Apply(
                RenderPropertyId,
                animated,
                influence,
                _blendMode,
                descriptor.SupportsSurfaceMask ? _surfaceMask : TextStudioSurfaceMask.All);
        }

        static uint InterpolateUnsigned(uint first, uint second, float amount)
        {
            double value = first + ((double)second - first) * amount;
            return (uint)Math.Min(
                uint.MaxValue,
                Math.Max(
                    0d,
                    Math.Round(value, MidpointRounding.AwayFromZero)));
        }

        string ResolveChannelDisplayName()
        {
            if (_kind == TextStudioCompositionLayerKind.Writing)
                return _writingStyle != null ? _writingStyle.name : "Writing";
            if (!string.IsNullOrEmpty(RenderPropertyId) &&
                TextStudioRenderPropertyRegistry.TryGet(RenderPropertyId, out TextStudioRenderPropertyDescriptor descriptor))
                return descriptor.DisplayName;
            if (!string.IsNullOrEmpty(RenderPropertyId))
                return RenderPropertyId;
            return ObjectNamesForRuntime(_channel);
        }

        float EvaluatePlayback(in EffectEvaluationContext context)
        {
            return EvaluatePlayback(in context, false);
        }

        float EvaluatePlayback(
            in EffectEvaluationContext context,
            bool timingAlreadyApplied)
        {
            PhaseParams timing = timingAlreadyApplied ? PhaseParams.NoOffset : Timing;
            float delay = 0f;
            float timeOffset = 0f;
            if (!timingAlreadyApplied)
            {
                delay = Mathf.Max(0f, timing.ComputeDelay(
                    context.unitIndex,
                    context.wordIndex,
                    context.lineIndex));
                delay += TextStudioSequenceUtility.ComputeDelay(
                    context.unitIndex,
                    context.unitCount,
                    in _sequence);
                timeOffset = _timeOffset;
            }
            bool usesContinuousClock = context.usesContinuousPhaseClock;
            float continuousSource = usesContinuousClock
                ? Mathf.Max(0f, (context.time + timeOffset - delay) * Mathf.Max(0.01f, timing.speed))
                : Mathf.Clamp01((context.phaseProgress + timeOffset - delay) * Mathf.Max(0.01f, timing.speed));
            float playbackSource = usesContinuousClock
                ? Mathf.Max(0f, (context.playbackTime + timeOffset - delay) * Mathf.Max(0.01f, timing.speed))
                : continuousSource;
            TextStudioEffectMetadata metadata = ResolveEffectMetadata();
            bool progressDriven = metadata?.PlaybackRole == TextStudioEffectPlaybackRole.Progress;
            float progressDuration = metadata != null
                ? metadata.DefaultPlaybackDuration
                : 1f;
            float progressGap = metadata != null
                ? metadata.DefaultDelayBetweenCycles
                : 0f;
            switch (_playback)
            {
                case TextStudioEffectPlaybackPreset.Loop:
                case TextStudioEffectPlaybackPreset.Cycle:
                    return usesContinuousClock
                        ? progressDriven
                            ? EvaluateLoopProgress(continuousSource, progressDuration, progressGap)
                            : Mathf.Repeat(continuousSource, 1f)
                        : continuousSource;
                case TextStudioEffectPlaybackPreset.PingPong:
                    return usesContinuousClock
                        ? progressDriven
                            ? EvaluatePingPongProgress(continuousSource, progressDuration, progressGap)
                            : Mathf.PingPong(continuousSource, 1f)
                        : continuousSource;
                case TextStudioEffectPlaybackPreset.Once:
                case TextStudioEffectPlaybackPreset.Appearance:
                case TextStudioEffectPlaybackPreset.Weighted:
                    return Mathf.Clamp01(
                        progressDriven && usesContinuousClock
                            ? playbackSource / progressDuration
                            : playbackSource);
                case TextStudioEffectPlaybackPreset.External:
                    return Mathf.Clamp01(
                        (context.phaseProgress + timeOffset - delay) * Mathf.Max(0.01f, timing.speed));
                case TextStudioEffectPlaybackPreset.Custom:
                    return _customPlayback != null
                        ? _customPlayback.Evaluate(
                            _customPlayback.IsInfinite ? continuousSource : playbackSource,
                            0f)
                        : Mathf.Clamp01(playbackSource);
                default:
                    return Mathf.Clamp01(playbackSource);
            }
        }

        static float EvaluateLoopProgress(float time, float duration, float gap)
        {
            float safeDuration = Mathf.Max(0.0001f, duration);
            float cycleTime = Mathf.Repeat(Mathf.Max(0f, time), safeDuration + Mathf.Max(0f, gap));
            return cycleTime >= safeDuration ? 1f : cycleTime / safeDuration;
        }

        static float EvaluatePingPongProgress(float time, float duration, float gap)
        {
            float safeDuration = Mathf.Max(0.0001f, duration);
            float motionDuration = safeDuration * 2f;
            float cycleTime = Mathf.Repeat(Mathf.Max(0f, time), motionDuration + Mathf.Max(0f, gap));
            if (cycleTime >= motionDuration)
                return 0f;
            return cycleTime <= safeDuration
                ? cycleTime / safeDuration
                : 1f - (cycleTime - safeDuration) / safeDuration;
        }

        TextStudioEffectMetadata ResolveEffectMetadata()
        {
            if (_kind != TextStudioCompositionLayerKind.Effect)
                return null;
            TextStudioRegistry.EnsureInitialized();
            uint registryRevision = TextStudioRegistry.Effects.Revision;
            if (_cachedMetadataRegistryRevision == registryRevision && string.Equals(
                    _cachedMetadataEffectId,
                    EffectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return _cachedEffectMetadata;
            }

            _cachedMetadataEffectId = EffectId;
            _cachedMetadataRegistryRevision = registryRevision;
            _cachedEffectMetadata = TextStudioEffects.TryGetDefinition(
                    EffectId,
                    out ITextStudioEffectDefinition definition) &&
                definition is ITextStudioEffectMetadataProvider provider
                    ? provider.Metadata
                    : null;
            return _cachedEffectMetadata;
        }

        internal bool TryGetFinitePlaybackDuration(out float totalDuration)
        {
            switch (_playback)
            {
                case TextStudioEffectPlaybackPreset.Once:
                case TextStudioEffectPlaybackPreset.Appearance:
                case TextStudioEffectPlaybackPreset.Weighted:
                    TextStudioEffectMetadata metadata = ResolveEffectMetadata();
                    totalDuration = metadata?.PlaybackRole == TextStudioEffectPlaybackRole.Progress
                        ? metadata.DefaultPlaybackDuration
                        : 1f;
                    return true;
                case TextStudioEffectPlaybackPreset.Custom:
                    if (_customPlayback != null && !_customPlayback.IsInfinite)
                    {
                        totalDuration = _customPlayback.GetTotalDuration();
                        return !float.IsNaN(totalDuration) && !float.IsInfinity(totalDuration);
                    }
                    break;
            }

            totalDuration = 0f;
            return false;
        }

        float EvaluateCurve(float t)
        {
            if (_curve != TextStudioEffectCurvePreset.Custom)
                return TextStudioEffectCurveUtility.Evaluate(_curve, t);
            float clamped = Mathf.Clamp01(t);
            if (_customCurveAsset != null)
                return _customCurveAsset.Evaluate(clamped);
            return _customCurve != null ? _customCurve.Evaluate(clamped) : clamped;
        }

        float EvaluateInfluence(float value)
        {
            switch (_curve)
            {
                case TextStudioEffectCurvePreset.Sine:
                case TextStudioEffectCurvePreset.Square:
                    return Mathf.Clamp01(value * 0.5f + 0.5f);
                default:
                    return Mathf.Clamp01(value);
            }
        }

        void ApplyPosition(ref EvaluatedGlyphState state, Vector3 value, float influence)
        {
            if (_blendMode == EffectBlendMode.Override)
                state.positionOffset = value;
            else if (_blendMode == EffectBlendMode.Multiply)
                state.positionOffset = Vector3.Scale(state.positionOffset, value);
            else if (_blendMode == EffectBlendMode.Lerp)
                state.positionOffset = Vector3.LerpUnclamped(state.positionOffset, value, influence);
            else
                state.positionOffset += value;
        }

        void ApplyRotation(ref EvaluatedGlyphState state, Quaternion value, float influence)
        {
            if (_blendMode == EffectBlendMode.Override)
                state.rotation = value;
            else if (_blendMode == EffectBlendMode.Lerp)
                state.rotation = Quaternion.SlerpUnclamped(state.rotation, value, influence);
            else
                state.rotation = value * state.rotation;
        }

        void ApplyScale(ref EvaluatedGlyphState state, Vector3 value, float influence)
        {
            if (_blendMode == EffectBlendMode.Override)
                state.scale = value;
            else if (_blendMode == EffectBlendMode.Lerp)
                state.scale = Vector3.LerpUnclamped(state.scale, value, influence);
            else if (_blendMode == EffectBlendMode.Additive)
                state.scale += value - Vector3.one;
            else
                state.scale = Vector3.Scale(state.scale, value);
        }

        void ApplyAlpha(ref EvaluatedGlyphState state, float target, float influence)
        {
            float animated = Mathf.LerpUnclamped(1f, target, influence);
            if (_blendMode == EffectBlendMode.Override)
                state.alpha = animated;
            else if (_blendMode == EffectBlendMode.Lerp)
                state.alpha = Mathf.LerpUnclamped(state.alpha, target, influence);
            else if (_blendMode == EffectBlendMode.Additive)
                state.alpha = Mathf.Clamp01(state.alpha + (target - 1f) * influence);
            else
                state.alpha *= animated;
        }

        void ApplyColor(ref EvaluatedGlyphState state, Color value, float influence)
        {
            if (influence <= 0f)
                return;

            state.BlendColor(
                (Color32)value,
                _colorMode == TextStudioCompositionColorMode.MultiplyExistingColor
                    ? TextStudioColorBlendMode.Multiply
                    : TextStudioColorBlendMode.Set,
                influence);
        }

        static Gradient CreateDefaultColorGradient()
        {
            Color warmGold = new Color(1f, 0.75f, 0.25f, 1f);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(warmGold, 0f),
                    new GradientColorKey(warmGold, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        void ApplySkew(ref EvaluatedGlyphState state, Vector2 value, float influence)
        {
            state.skew = _blendMode switch
            {
                EffectBlendMode.Override => value,
                EffectBlendMode.Multiply => Vector2.Scale(state.skew, value),
                EffectBlendMode.Lerp => Vector2.LerpUnclamped(state.skew, value, influence),
                _ => state.skew + value
            };
        }

        void ApplyUvOffset(ref EvaluatedGlyphState state, Vector2 value, float influence)
        {
            state.uvOffset = _blendMode switch
            {
                EffectBlendMode.Override => value,
                EffectBlendMode.Multiply => Vector2.Scale(state.uvOffset, value),
                EffectBlendMode.Lerp => Vector2.LerpUnclamped(state.uvOffset, value, influence),
                _ => state.uvOffset + value
            };
        }

        void ApplyPivot(ref EvaluatedGlyphState state, Vector2 value, float influence)
        {
            state.pivotOffset = _blendMode switch
            {
                EffectBlendMode.Override => value,
                EffectBlendMode.Multiply => Vector2.Scale(state.pivotOffset, value),
                EffectBlendMode.Lerp => Vector2.LerpUnclamped(state.pivotOffset, value, influence),
                _ => state.pivotOffset + value
            };
        }

        void ApplyCornerDeformation(ref EvaluatedGlyphState state, float value, float influence)
        {
            TextStudioCornerOffsets target = TextStudioCornerOffsets.Scale(
                new TextStudioCornerOffsets(
                    _bottomLeftCorner,
                    _topLeftCorner,
                    _topRightCorner,
                    _bottomRightCorner),
                value);
            state.cornerOffsets = _blendMode switch
            {
                EffectBlendMode.Override => target,
                EffectBlendMode.Multiply => TextStudioCornerOffsets.Multiply(in state.cornerOffsets, in target),
                EffectBlendMode.Lerp => TextStudioCornerOffsets.Lerp(in state.cornerOffsets, in target, influence),
                _ => TextStudioCornerOffsets.Add(in state.cornerOffsets, in target)
            };
        }

        static PhaseParams NormalizeTiming(PhaseParams timing)
        {
            if (timing.speed <= 0f)
                timing.speed = 1f;
            return timing;
        }

        static string ObjectNamesForRuntime(TextStudioCompositionChannel channel)
        {
            return channel == TextStudioCompositionChannel.UVOffset ? "UV Offset" : channel.ToString();
        }
    }

    /// <summary>Reusable effect definition composed from ordered semantic effect and channel layers.</summary>
    [CreateAssetMenu(
        fileName = "Composed Effect",
        menuName = "Text Studio/Animation/Composed Effect",
        order = 102)]
    public sealed class TextStudioComposedEffectAsset : TextStudioEffectDefinitionAsset
    {
        [SerializeField] string _id = "composed";
        [SerializeField] TextStudioCompositionAuthoringSpace _authoringSpace =
            TextStudioCompositionAuthoringSpace.TwoD;
        [SerializeField, Range(0f, 5f)] float _revealActiveBlendDuration = 1f;
        [SerializeField] TextStudioCompositionTagSpec _tagStack = new TextStudioCompositionTagSpec();
        [SerializeField] List<TextStudioCompositionLayer> _layers = new List<TextStudioCompositionLayer>();

        readonly List<string> _invocationBuffer = new List<string>(8);
        [NonSerialized] HashSet<TextStudioCurveProfile> _subscribedCurves;

        public override string Id => _id ?? string.Empty;
        public override TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Default;
        public override EffectBlendMode BlendMode => EffectBlendMode.Additive;
        public TextStudioCompositionAuthoringSpace AuthoringSpace => _authoringSpace;
        public TextStudioRenderDomain RequiredRenderDomain => _authoringSpace ==
            TextStudioCompositionAuthoringSpace.ThreeD
                ? TextStudioRenderDomain.Extruded3D
                : TextStudioRenderDomain.FlatText;
        public TextStudioRenderDomain SupportedRenderDomains =>
            TryResolveSupportedRenderDomains(out TextStudioRenderDomain domains, out _)
                ? domains
                : TextStudioRenderDomain.None;
        public override TextStudioEffectCapabilities Capabilities => new TextStudioEffectCapabilities(
            TextStudioRenderChannels.All,
            TextStudioPhaseMask.All,
            supportedRenderDomains: SupportedRenderDomains);
        public float RevealActiveBlendDuration => Mathf.Clamp(_revealActiveBlendDuration, 0f, 5f);
        public TextStudioCompositionTagSpec TagStack => _tagStack;
        public IReadOnlyList<TextStudioCompositionLayer> Layers => _layers;

        void OnEnable()
        {
            EnsureInitialized();
        }

        void OnDisable()
        {
            ClearCurveSubscriptions();
            UnregisterWritingLayers();
        }

        protected override void OnValidate()
        {
            EnsureInitialized();
            base.OnValidate();
        }

        /// <summary>Replaces the stack with one active-position layer suitable for initial authoring.</summary>
        public void ResetLayers()
        {
            for (int i = 0; i < _layers.Count; i++)
                _layers[i]?.Reset();
            NotifyEffectContentChanged();
        }

        internal void ClearLayersForAuthoring()
        {
            UnregisterWritingLayers();
            _layers.Clear();
            NotifyEffectContentChanged();
        }

        internal void SetAuthoringSpaceForAuthoring(TextStudioCompositionAuthoringSpace authoringSpace)
        {
            _authoringSpace = authoringSpace;
            NotifyEffectContentChanged();
        }

        internal void SetRevealActiveBlendDurationForAuthoring(float duration)
        {
            _revealActiveBlendDuration = Mathf.Clamp(duration, 0f, 5f);
            NotifyEffectContentChanged();
        }

        internal bool TryGetTagInvocations(List<string> invocations, out string error)
        {
            EnsureInitialized();
            return _tagStack.TryGetInvocations(invocations, out error);
        }

        internal bool TryGetAllEffectInvocations(List<string> invocations, out string error)
        {
            if (!TryGetTagInvocations(invocations, out error))
                return false;
            return AppendLayerInvocations(_layers, invocations, out error);
        }

        internal TextStudioCompositionLayer AddChannelLayer(TextStudioCompositionChannel channel)
        {
            TextStudioCompositionLayer layer = TextStudioCompositionLayer.CreateChannel(channel);
            _layers.Add(layer);
            NotifyEffectContentChanged();
            return layer;
        }

        internal TextStudioCompositionLayer AddRenderPropertyLayer(string propertyId)
        {
            TextStudioCompositionLayer layer = TextStudioCompositionLayer.CreateRenderProperty(propertyId);
            _layers.Add(layer);
            NotifyEffectContentChanged();
            return layer;
        }

        internal TextStudioCompositionLayer AddEffectLayer(string effectId)
        {
            TextStudioCompositionLayer layer = TextStudioCompositionLayer.CreateEffect(effectId);
            _layers.Add(layer);
            NotifyEffectContentChanged();
            return layer;
        }

        internal TextStudioCompositionLayer AddWritingLayer(
            TextStudioWritingStyleAsset writingStyle)
        {
            TextStudioCompositionLayer layer =
                TextStudioCompositionLayer.CreateWriting(writingStyle);
            layer.SetCompositionOwnerId(ResolveRuntimeOwnerId());
            _layers.Add(layer);
            NotifyEffectContentChanged();
            return layer;
        }

        internal void DuplicateLayer(int index)
        {
            if (index < 0 || index >= _layers.Count || _layers[index] == null)
                return;
            TextStudioCompositionLayer clone = _layers[index].Clone();
            clone.SetCompositionOwnerId(ResolveRuntimeOwnerId());
            _layers.Insert(index + 1, clone);
            NotifyEffectContentChanged();
        }

        internal void RemoveLayer(int index)
        {
            if (index >= 0 && index < _layers.Count)
            {
                TextStudioComposedWritingRegistry.Unregister(_layers[index]);
                _layers.RemoveAt(index);
                NotifyEffectContentChanged();
            }
        }

        internal void MoveLayer(int index, int offset)
        {
            int destination = index + offset;
            if (index < 0 || index >= _layers.Count || destination < 0 || destination >= _layers.Count)
                return;
            TextStudioCompositionLayer layer = _layers[index];
            _layers.RemoveAt(index);
            _layers.Insert(destination, layer);
            NotifyEffectContentChanged();
        }

        internal bool DuplicateLayer(string stableId)
        {
            if (!TryFindLayer(stableId, out TextStudioCompositionLayer layer, out List<TextStudioCompositionLayer> owner, out int index))
                return false;
            TextStudioCompositionLayer clone = layer.Clone();
            clone.SetCompositionOwnerId(ResolveRuntimeOwnerId());
            owner.Insert(index + 1, clone);
            NotifyEffectContentChanged();
            return true;
        }

        internal bool RemoveLayer(string stableId)
        {
            if (!TryFindLayer(stableId, out TextStudioCompositionLayer layer, out List<TextStudioCompositionLayer> owner, out int index))
                return false;
            TextStudioComposedWritingRegistry.Unregister(layer);
            owner.RemoveAt(index);
            NotifyEffectContentChanged();
            return true;
        }

        internal bool MoveLayer(string stableId, int offset)
        {
            if (!TryFindLayer(stableId, out TextStudioCompositionLayer layer, out List<TextStudioCompositionLayer> owner, out int index))
                return false;
            int destination = index + offset;
            if (destination < 0 || destination >= owner.Count)
                return false;
            owner.RemoveAt(index);
            owner.Insert(destination, layer);
            NotifyEffectContentChanged();
            return true;
        }

        internal bool HasLayer(string stableId)
        {
            return !string.IsNullOrEmpty(stableId) &&
                TryFindLayer(stableId, out _, out _, out _);
        }

        internal bool TryCloneLayer(string stableId, out TextStudioCompositionLayer clone)
        {
            if (TryFindLayer(stableId, out TextStudioCompositionLayer layer, out _, out _) && layer != null)
            {
                clone = layer.Clone();
                return true;
            }

            clone = null;
            return false;
        }

        internal bool ReplaceLayerWithEffect(string stableId, string effectId)
        {
            if (!TryFindLayer(
                    stableId,
                    out TextStudioCompositionLayer previous,
                    out List<TextStudioCompositionLayer> owner,
                    out int index))
                return false;
            TextStudioCompositionLayer replacement =
                TextStudioCompositionLayer.CreateEffect(effectId);
            owner[index] = replacement;
            NotifyEffectContentChanged();
            return true;
        }

        internal bool SetLayerWritingStyle(
            string stableId,
            TextStudioWritingStyleAsset writingStyle)
        {
            if (!TryFindLayer(
                    stableId,
                    out TextStudioCompositionLayer layer,
                    out _,
                    out _) ||
                layer == null ||
                layer.Kind != TextStudioCompositionLayerKind.Writing)
            {
                return false;
            }
            layer.SetWritingStyleForAuthoring(writingStyle);
            NotifyEffectContentChanged();
            return true;
        }

        internal void Initialize(string id, IEnumerable<TextStudioCompositionLayer> layers)
        {
            UnregisterWritingLayers();
            _id = string.IsNullOrWhiteSpace(id) ? "composed" : id.Trim();
            _tagStack = new TextStudioCompositionTagSpec();
            _layers.Clear();
            if (layers != null)
                _layers.AddRange(layers);
            EnsureInitialized();
            NotifyEffectContentChanged();
        }

        internal void Initialize(
            string id,
            TextStudioCompositionAuthoringSpace authoringSpace,
            IEnumerable<TextStudioCompositionLayer> layers)
        {
            _authoringSpace = authoringSpace;
            Initialize(id, layers);
        }

        bool TryFindLayer(
            string stableId,
            out TextStudioCompositionLayer layer,
            out List<TextStudioCompositionLayer> owner,
            out int index)
        {
            return TryFindLayer(_layers, stableId, out layer, out owner, out index);
        }

        static bool TryFindLayer(
            List<TextStudioCompositionLayer> layers,
            string stableId,
            out TextStudioCompositionLayer layer,
            out List<TextStudioCompositionLayer> owner,
            out int index)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer candidate = layers[i];
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    layer = candidate;
                    owner = layers;
                    index = i;
                    return true;
                }

            }

            layer = null;
            owner = null;
            index = -1;
            return false;
        }

        internal void InitializeForTests(
            string id,
            string openingMarkup,
            IEnumerable<TextStudioCompositionLayer> layers = null,
            TextStudioTagExpansionClosingMode closingMode = TextStudioTagExpansionClosingMode.Auto,
            string customClosingMarkup = "",
            float revealActiveBlendDuration = 0f)
        {
            UnregisterWritingLayers();
            _id = id ?? string.Empty;
            _revealActiveBlendDuration = revealActiveBlendDuration;
            _tagStack.InitializeForTests(openingMarkup, closingMode, customClosingMarkup);
            _layers.Clear();
            if (layers != null)
                _layers.AddRange(layers);
            EnsureInitialized();
            NotifyEffectContentChanged();
        }

        internal bool TryCompileForValidation(out string error)
        {
            TextStudioRegistry.EnsureInitialized();
            List<TextStudioComposedEffectAsset> validationStack = new List<TextStudioComposedEffectAsset>(8);
            if (!TryCompileForValidation(validationStack, out error))
                return false;
            return TryResolveSupportedRenderDomains(out _, out error);
        }

        internal bool TryResolveSupportedRenderDomains(
            out TextStudioRenderDomain supportedDomains,
            out string error)
        {
            TextStudioRegistry.EnsureInitialized();
            List<TextStudioComposedEffectAsset> stack = new List<TextStudioComposedEffectAsset>(8);
            return TryResolveSupportedRenderDomains(stack, out supportedDomains, out error);
        }

        bool TryResolveSupportedRenderDomains(
            List<TextStudioComposedEffectAsset> stack,
            out TextStudioRenderDomain supportedDomains,
            out string error)
        {
            if (stack.Contains(this))
            {
                supportedDomains = TextStudioRenderDomain.None;
                error = "Composed effect cycle detected at '" + Id + "'.";
                return false;
            }

            stack.Add(this);
            try
            {
                TextStudioRenderDomain required = RequiredRenderDomain;
                supportedDomains = _authoringSpace == TextStudioCompositionAuthoringSpace.ThreeD
                    ? TextStudioRenderDomain.Extruded3D
                    : TextStudioRenderDomain.All;

                List<string> invocations = new List<string>(8);
                if (!TryGetTagInvocations(invocations, out error))
                    return false;
                for (int i = 0; i < invocations.Count; i++)
                {
                    if (!TryResolveInvocationDomains(
                            invocations[i],
                            "Tag Stack",
                            required,
                            stack,
                            ref supportedDomains,
                            out error))
                        return false;
                }

                for (int i = 0; i < _layers.Count; i++)
                {
                    TextStudioCompositionLayer layer = _layers[i];
                    if (layer == null || !layer.Enabled || layer.Phases == TextStudioPhaseMask.None)
                        continue;
                    string location = "Visual Layer '" + layer.DisplayName + "'";
                    if (layer.Kind == TextStudioCompositionLayerKind.Channel)
                    {
                        TextStudioRenderDomain layerDomains = TextStudioRenderDomain.All;
                        if (layer.UsesRenderProperty)
                        {
                            if (!TextStudioRenderPropertyRegistry.TryGet(
                                    layer.RenderPropertyId,
                                    out TextStudioRenderPropertyDescriptor descriptor))
                            {
                                supportedDomains = TextStudioRenderDomain.None;
                                error = location + " requires the render property '" +
                                    layer.RenderPropertyId + "', but its extension is not installed or enabled.";
                                return false;
                            }
                            layerDomains = descriptor.SupportedRenderDomains;
                        }
                        if ((layerDomains & required) == 0)
                        {
                            supportedDomains = TextStudioRenderDomain.None;
                            error = location + ": " +
                                TextStudioEffectCompatibilityResolver.BuildUnsupportedReason(layerDomains);
                            return false;
                        }
                        if (_authoringSpace == TextStudioCompositionAuthoringSpace.TwoD &&
                            layer.HasNonPlanarValues)
                        {
                            supportedDomains = TextStudioRenderDomain.None;
                            error = layer.GetNonPlanarValuesWarning(location);
                            return false;
                        }
                        supportedDomains &= layerDomains;
                        continue;
                    }

                    if (layer.Kind == TextStudioCompositionLayerKind.Writing)
                    {
                        TextStudioRenderDomain layerDomains =
                            TextStudioRenderDomain.FlatText;
                        if ((layerDomains & required) == 0)
                        {
                            supportedDomains = TextStudioRenderDomain.None;
                            error = location + ": Writing layers are supported by flat text renderers only.";
                            return false;
                        }
                        if (layer.WritingStyle == null)
                        {
                            supportedDomains = TextStudioRenderDomain.None;
                            error = location + " needs a Writing Style asset.";
                            return false;
                        }
                        CompiledTextStudioWritingStyle writing =
                            layer.ResolveCompiledWritingStyle();
                        if (writing == null || !writing.IsValid)
                        {
                            supportedDomains = TextStudioRenderDomain.None;
                            error = location + " references an invalid Writing Style asset.";
                            return false;
                        }
                        supportedDomains &= layerDomains;
                        continue;
                    }

                    if (!TryResolveInvocationDomains(
                            layer.GetEffectInvocationSource(),
                            location,
                            required,
                            stack,
                            ref supportedDomains,
                            out error))
                        return false;
                }

                error = string.Empty;
                return supportedDomains != TextStudioRenderDomain.None;
            }
            finally
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        static bool TryResolveInvocationDomains(
            string source,
            string location,
            TextStudioRenderDomain required,
            List<TextStudioComposedEffectAsset> stack,
            ref TextStudioRenderDomain supportedDomains,
            out string error)
        {
            if (!TextStudioEffectInvocationParser.TryParse(
                    source,
                    out TextStudioEffectInvocation invocation,
                    out string parseError))
            {
                error = location + " is invalid: " + parseError;
                return false;
            }

            string effectId = invocation.EffectId;
            if (TextStudioPhaseTagUtility.TryResolveEffectId(effectId, out string resolvedId))
                effectId = resolvedId;
            ITextStudioEffectDefinition definition = null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (string.Equals(stack[i].Id, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = stack[i];
                    break;
                }
            }
            if (definition == null &&
                !TextStudioRegistry.Effects.TryGetQuiet(effectId, out definition))
            {
                error = location + " references missing effect '" + effectId + "'.";
                supportedDomains = TextStudioRenderDomain.None;
                return false;
            }

            TextStudioRenderDomain domains;
            if (definition is TextStudioComposedEffectAsset composed)
            {
                if (!composed.TryResolveSupportedRenderDomains(stack, out domains, out error))
                {
                    error = location + ": " + error;
                    supportedDomains = TextStudioRenderDomain.None;
                    return false;
                }
            }
            else
            {
                domains = TextStudioEffectCompatibilityResolver.GetSupportedDomains(definition);
            }

            if ((domains & required) == 0)
            {
                supportedDomains = TextStudioRenderDomain.None;
                error = location + ": " +
                    TextStudioEffectCompatibilityResolver.BuildUnsupportedReason(domains);
                return false;
            }

            supportedDomains &= domains;
            error = string.Empty;
            return true;
        }

        bool TryCompileForValidation(
            List<TextStudioComposedEffectAsset> validationStack,
            out string error)
        {
            for (int i = 0; i < validationStack.Count; i++)
            {
                if (validationStack[i] != this)
                    continue;
                error = "Composed effect cycle detected at '" + Id + "'.";
                return false;
            }

            validationStack.Add(this);
            try
            {
                _invocationBuffer.Clear();
                if (!TryGetTagInvocations(_invocationBuffer, out error))
                    return false;

                for (int i = 0; i < _invocationBuffer.Count; i++)
                {
                    if (!TryValidateInvocation(
                            _invocationBuffer[i],
                            "Tag Stack",
                            validationStack,
                            out error))
                        return false;
                }

                return TryValidateLayers(_layers, validationStack, out error);
            }
            finally
            {
                validationStack.RemoveAt(validationStack.Count - 1);
            }
        }

        bool TryValidateLayers(
            IReadOnlyList<TextStudioCompositionLayer> layers,
            List<TextStudioComposedEffectAsset> validationStack,
            out string error)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled || layer.Phases == TextStudioPhaseMask.None)
                    continue;
                if (layer.Kind == TextStudioCompositionLayerKind.Channel &&
                    layer.UsesRenderProperty &&
                    !TextStudioRenderPropertyRegistry.TryGet(layer.RenderPropertyId, out _))
                {
                    error = "Visual Layer '" + layer.DisplayName +
                        "' requires the render property '" + layer.RenderPropertyId +
                        "', but its extension is not installed or enabled.";
                    return false;
                }
                if (layer.Kind == TextStudioCompositionLayerKind.Writing)
                {
                    if (layer.WritingStyle == null)
                    {
                        error = "Visual Layer '" + layer.DisplayName +
                            "' needs a Writing Style asset.";
                        return false;
                    }
                    CompiledTextStudioWritingStyle writing =
                        layer.ResolveCompiledWritingStyle();
                    if (writing == null || !writing.IsValid)
                    {
                        error = "Visual Layer '" + layer.DisplayName +
                            "' references an invalid Writing Style asset.";
                        return false;
                    }
                    continue;
                }
                if (layer.Kind != TextStudioCompositionLayerKind.Effect)
                    continue;
                if (!TryValidateInvocation(
                        layer.GetEffectInvocationSource(),
                        "Visual Layer '" + layer.DisplayName + "'",
                        validationStack,
                        out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        bool TryValidateInvocation(
            string source,
            string location,
            List<TextStudioComposedEffectAsset> validationStack,
            out string error)
        {
            if (!TextStudioEffectInvocationParser.TryParse(
                    source,
                    out TextStudioEffectInvocation invocation,
                    out string parseError))
            {
                error = location + " is invalid: " + parseError;
                return false;
            }

            string effectId = invocation.EffectId;
            if (TextStudioPhaseTagUtility.TryResolveEffectId(effectId, out string resolvedId))
                effectId = resolvedId;

            ITextStudioEffectDefinition effect = null;
            for (int i = validationStack.Count - 1; i >= 0; i--)
            {
                TextStudioComposedEffectAsset candidate = validationStack[i];
                if (!string.Equals(candidate.Id, effectId, StringComparison.OrdinalIgnoreCase))
                    continue;
                effect = candidate;
                break;
            }

            if (effect == null && !TextStudioRegistry.Effects.TryGetQuiet(effectId, out effect))
            {
                error = location + " references missing effect '" + effectId + "'.";
                return false;
            }

            if (effect is TextStudioComposedEffectAsset composed &&
                !composed.TryCompileForValidation(validationStack, out error))
            {
                error = location + ": " + error;
                return false;
            }

            error = string.Empty;
            return true;
        }

        void EnsureInitialized()
        {
            _tagStack ??= new TextStudioCompositionTagSpec();
            _layers ??= new List<TextStudioCompositionLayer>();
            if (string.IsNullOrWhiteSpace(_id))
                _id = "composed";
            else
                _id = _id.Trim();
            _revealActiveBlendDuration = Mathf.Clamp(_revealActiveBlendDuration, 0f, 5f);
            string ownerId = ResolveRuntimeOwnerId();
            for (int i = 0; i < _layers.Count; i++)
            {
                TextStudioCompositionLayer layer = _layers[i];
                layer?.EnsureInitialized();
                layer?.SetCompositionOwnerId(ownerId);
            }
            RefreshCurveSubscriptions();
        }

        void RefreshCurveSubscriptions()
        {
            ClearCurveSubscriptions();
            _subscribedCurves ??= new HashSet<TextStudioCurveProfile>();
            for (int i = 0; i < _layers.Count; i++)
            {
                TextStudioCurveProfile profile = _layers[i]?.CustomCurveAsset;
                if (profile != null && _subscribedCurves.Add(profile))
                    profile.Changed += OnCurveProfileChanged;
            }
        }

        void ClearCurveSubscriptions()
        {
            if (_subscribedCurves == null) return;
            foreach (TextStudioCurveProfile profile in _subscribedCurves)
                if (profile != null) profile.Changed -= OnCurveProfileChanged;
            _subscribedCurves.Clear();
        }

        void OnCurveProfileChanged(TextStudioCurveProfile profile)
        {
            NotifyEffectContentChanged();
        }

        string ResolveRuntimeOwnerId()
        {
            return _id + "." + unchecked((uint)this.GetStableId()).ToString("x8");
        }

        void UnregisterWritingLayers()
        {
            if (_layers == null)
                return;
            for (int index = 0; index < _layers.Count; index++)
                TextStudioComposedWritingRegistry.Unregister(_layers[index]);
        }

        static bool AppendLayerInvocations(
            IReadOnlyList<TextStudioCompositionLayer> layers,
            List<string> invocations,
            out string error)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled || layer.Phases == TextStudioPhaseMask.None)
                    continue;
                if (layer.Kind != TextStudioCompositionLayerKind.Effect)
                    continue;
                if (!layer.TryGetEffectInvocation(out _, out error))
                    return false;
                invocations.Add(layer.GetEffectInvocationSource());
            }

            error = string.Empty;
            return true;
        }
    }
}
