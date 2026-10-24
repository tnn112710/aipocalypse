using System;
using System.Collections.Generic;
using System.Globalization;

namespace TextStudio
{
    /// <summary>Resolved effect definition plus its authored binding.</summary>
    public readonly struct TextStudioResolvedEffectBinding
    {
        public readonly string EffectId;
        public readonly ITextStudioEffect Effect;
        public readonly TextStudioTagParameters Parameters;
        public readonly TextStudioPhaseMask PhaseMask;
        public readonly float Intensity;
        public readonly float TimeOffset;
        public readonly EffectBlendMode BlendMode;
        public readonly TextStudioEffectAreaSettings Area;
        public readonly PhaseParams PhaseParams;
        public readonly TextStudioSequenceSettings Sequence;
        public readonly bool HasAuthoringOverrides;
        internal readonly int PhaseBlendGroupId;
        internal readonly float RevealActiveBlendDuration;
        internal readonly TextStudioPhaseMask RevealActiveBlendPhaseMask;
        internal readonly float RevealActiveBlendTimeOffset;
        internal readonly TextStudioEffectScopeChain Scope;
        internal readonly TextStudioComposedEnvelopeStack Envelope;
        internal readonly TextStudioRangeScopeChain RangeScope;
        internal readonly TextStudioEffectSelectorChain SelectorScope;

        /// <summary>Creates an unresolved binding identified by effect ID and parsed parameters.</summary>
        public TextStudioResolvedEffectBinding(
            string effectId,
            TextStudioTagParameters parameters,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset)
        {
            EffectId = effectId ?? string.Empty;
            Effect = null;
            Parameters = parameters;
            PhaseMask = phaseMask;
            Intensity = intensity;
            TimeOffset = timeOffset;
            BlendMode = EffectBlendMode.Additive;
            Area = default;
            PhaseParams = default;
            Sequence = TextStudioSequenceSettings.Unstaggered;
            HasAuthoringOverrides = false;
            PhaseBlendGroupId = 0;
            RevealActiveBlendDuration = 0f;
            RevealActiveBlendPhaseMask = TextStudioPhaseMask.None;
            RevealActiveBlendTimeOffset = 0f;
            Scope = null;
            Envelope = null;
            RangeScope = null;
            SelectorScope = null;
        }

        /// <summary>Creates a resolved binding with complete playback, blend, and semantic-area configuration.</summary>
        public TextStudioResolvedEffectBinding(
            ITextStudioEffect effect,
            string effectId,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset,
            EffectBlendMode blendMode,
            TextStudioEffectAreaSettings area,
            PhaseParams phaseParams)
        {
            EffectId = effectId ?? string.Empty;
            Effect = effect;
            Parameters = TextStudioTagParameters.Empty;
            PhaseMask = phaseMask;
            Intensity = intensity;
            TimeOffset = timeOffset;
            BlendMode = blendMode;
            Area = area;
            PhaseParams = phaseParams;
            Sequence = TextStudioSequenceSettings.Unstaggered;
            HasAuthoringOverrides = true;
            PhaseBlendGroupId = 0;
            RevealActiveBlendDuration = 0f;
            RevealActiveBlendPhaseMask = TextStudioPhaseMask.None;
            RevealActiveBlendTimeOffset = 0f;
            Scope = null;
            Envelope = null;
            RangeScope = null;
            SelectorScope = null;
        }

        internal TextStudioResolvedEffectBinding WithPhaseBlend(
            int phaseBlendGroupId,
            float revealActiveBlendDuration,
            TextStudioPhaseMask revealActiveBlendPhaseMask,
            float revealActiveBlendTimeOffset)
        {
            return new TextStudioResolvedEffectBinding(
                EffectId,
                Effect,
                Parameters,
                PhaseMask,
                Intensity,
                TimeOffset,
                BlendMode,
                Area,
                PhaseParams,
                Sequence,
                HasAuthoringOverrides,
                phaseBlendGroupId,
                revealActiveBlendDuration,
                revealActiveBlendPhaseMask,
                revealActiveBlendTimeOffset,
                Scope,
                Envelope,
                RangeScope,
                SelectorScope);
        }

        internal TextStudioResolvedEffectBinding WithSequence(in TextStudioSequenceSettings sequence)
        {
            return new TextStudioResolvedEffectBinding(
                EffectId,
                Effect,
                Parameters,
                PhaseMask,
                Intensity,
                TimeOffset,
                BlendMode,
                Area,
                PhaseParams,
                sequence,
                HasAuthoringOverrides,
                PhaseBlendGroupId,
                RevealActiveBlendDuration,
                RevealActiveBlendPhaseMask,
                RevealActiveBlendTimeOffset,
                Scope,
                Envelope,
                RangeScope,
                SelectorScope);
        }

        internal TextStudioResolvedEffectBinding WithScope(
            TextStudioInfluenceFieldStack spatialInfluence,
            TextStudioSourceColorSelector sourceColorSelector)
        {
            TextStudioEffectScopeChain scope = TextStudioEffectScopeChain.Append(
                Scope,
                spatialInfluence,
                sourceColorSelector);
            return new TextStudioResolvedEffectBinding(
                EffectId,
                Effect,
                Parameters,
                PhaseMask,
                Intensity,
                TimeOffset,
                BlendMode,
                Area,
                PhaseParams,
                Sequence,
                HasAuthoringOverrides,
                PhaseBlendGroupId,
                RevealActiveBlendDuration,
                RevealActiveBlendPhaseMask,
                RevealActiveBlendTimeOffset,
                scope,
                Envelope,
                RangeScope,
                SelectorScope);
        }

        internal TextStudioResolvedEffectBinding WithEnvelope(
            TextStudioComposedEnvelopeStack envelope)
        {
            return new TextStudioResolvedEffectBinding(
                EffectId,
                Effect,
                Parameters,
                PhaseMask,
                Intensity,
                TimeOffset,
                BlendMode,
                Area,
                PhaseParams,
                Sequence,
                HasAuthoringOverrides,
                PhaseBlendGroupId,
                RevealActiveBlendDuration,
                RevealActiveBlendPhaseMask,
                RevealActiveBlendTimeOffset,
                Scope,
                envelope,
                RangeScope,
                SelectorScope);
        }

        internal TextStudioResolvedEffectBinding WithRange(TextStudioTextRange range)
        {
            return new TextStudioResolvedEffectBinding(
                EffectId, Effect, Parameters, PhaseMask, Intensity, TimeOffset,
                BlendMode, Area, PhaseParams, Sequence, HasAuthoringOverrides,
                PhaseBlendGroupId, RevealActiveBlendDuration, RevealActiveBlendPhaseMask,
                RevealActiveBlendTimeOffset, Scope, Envelope,
                new TextStudioRangeScopeChain(range, RangeScope), SelectorScope);
        }

        internal TextStudioResolvedEffectBinding WithSelector(TextStudioApplicationSelector selector)
        {
            TextStudioApplicationSelectorNode[] nodes = selector.GetNodesUnsafe();
            if (nodes == null || nodes.Length == 0)
                return this;
            return new TextStudioResolvedEffectBinding(
                EffectId, Effect, Parameters, PhaseMask, Intensity, TimeOffset,
                BlendMode, Area, PhaseParams, Sequence, HasAuthoringOverrides,
                PhaseBlendGroupId, RevealActiveBlendDuration, RevealActiveBlendPhaseMask,
                RevealActiveBlendTimeOffset, Scope, Envelope, RangeScope,
                new TextStudioEffectSelectorChain(selector, SelectorScope));
        }

        TextStudioResolvedEffectBinding(
            string effectId,
            ITextStudioEffect effect,
            TextStudioTagParameters parameters,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset,
            EffectBlendMode blendMode,
            TextStudioEffectAreaSettings area,
            PhaseParams phaseParams,
            TextStudioSequenceSettings sequence,
            bool hasAuthoringOverrides,
            int phaseBlendGroupId,
            float revealActiveBlendDuration,
            TextStudioPhaseMask revealActiveBlendPhaseMask,
            float revealActiveBlendTimeOffset,
            TextStudioEffectScopeChain scope,
            TextStudioComposedEnvelopeStack envelope,
            TextStudioRangeScopeChain rangeScope,
            TextStudioEffectSelectorChain selectorScope)
        {
            EffectId = effectId ?? string.Empty;
            Effect = effect;
            Parameters = parameters;
            PhaseMask = phaseMask;
            Intensity = intensity;
            TimeOffset = timeOffset;
            BlendMode = blendMode;
            Area = area;
            PhaseParams = phaseParams;
            Sequence = sequence.Equals(default)
                ? TextStudioSequenceSettings.Unstaggered
                : sequence;
            HasAuthoringOverrides = hasAuthoringOverrides;
            PhaseBlendGroupId = phaseBlendGroupId;
            RevealActiveBlendDuration = revealActiveBlendDuration;
            RevealActiveBlendPhaseMask = revealActiveBlendPhaseMask;
            RevealActiveBlendTimeOffset = revealActiveBlendTimeOffset;
            Scope = scope;
            Envelope = envelope;
            RangeScope = rangeScope;
            SelectorScope = selectorScope;
        }
    }

    internal sealed class TextStudioDiagnosticSink
    {
        readonly List<TextStudioDiagnostic> _diagnostics;
        readonly string _tagName;
        readonly int _sourceStart;
        readonly int _sourceEnd;

        public TextStudioDiagnosticSink(
            List<TextStudioDiagnostic> diagnostics,
            string tagName,
            int sourceStart,
            int sourceEnd)
        {
            _diagnostics = diagnostics;
            _tagName = tagName ?? string.Empty;
            _sourceStart = sourceStart;
            _sourceEnd = sourceEnd;
        }

        public void Add(
            TextStudioDiagnosticSeverity severity,
            TextStudioDiagnosticCode code,
            string message,
            string tagName = null)
        {
            if (_diagnostics == null)
                return;

            _diagnostics.Add(new TextStudioDiagnostic(
                severity,
                code,
                message,
                string.IsNullOrEmpty(tagName) ? _tagName : tagName,
                _sourceStart,
                _sourceEnd));
        }
    }

    internal sealed class TextStudioEffectResolver
    {
        readonly struct PhaseBlendScope
        {
            public readonly int GroupId;
            public readonly float Duration;
            public readonly TextStudioPhaseMask PhaseMask;
            public readonly float TimeOffset;

            public PhaseBlendScope(
                int groupId,
                float duration,
                TextStudioPhaseMask phaseMask,
                float timeOffset)
            {
                GroupId = groupId;
                Duration = duration;
                PhaseMask = phaseMask;
                TimeOffset = timeOffset;
            }

            public bool IsEnabled => Duration > 0f;
            public bool HasAllocatedGroup => GroupId != 0;
        }

        static readonly string[] s_inheritedBindingParameterNames =
        {
            "area",
            "group",
            "charOffset",
            "wordOffset",
            "lineOffset"
        };

        TextStudioComposedEffectAsset _previewSoloOwner;
        string _previewSoloLayerId;
        readonly List<string> _resolutionStack = new List<string>(8);
        int _nextPhaseBlendGroupId = 1;

        // The next phase blend group, saved and restored around a resolution whose results are discarded.
        internal int PhaseBlendGroupState
        {
            get => _nextPhaseBlendGroupId;
            set => _nextPhaseBlendGroupId = value;
        }

        public bool TryResolve(
            string effectId,
            TextStudioTagParameters callSiteParameters,
            TextStudioPhaseMask phaseMask,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics)
        {
            return TryResolve(
                effectId,
                callSiteParameters,
                phaseMask,
                output,
                diagnostics,
                1f,
                0f,
                null);
        }

        public bool TryResolve(
            string effectId,
            TextStudioTagParameters callSiteParameters,
            TextStudioPhaseMask phaseMask,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics,
            float intensity,
            float timeOffset)
        {
            return TryResolve(
                effectId,
                callSiteParameters,
                phaseMask,
                output,
                diagnostics,
                intensity,
                timeOffset,
                null);
        }

        internal bool TryResolve(
            string effectId,
            TextStudioTagParameters callSiteParameters,
            TextStudioPhaseMask phaseMask,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics,
            float intensity,
            float timeOffset,
            TextStudioComposedEffectAsset localEffect,
            string previewSoloLayerId = null)
        {
            _previewSoloOwner = localEffect;
            _previewSoloLayerId = previewSoloLayerId;
            if (output == null)
                return false;

            TextStudioRegistry.EnsureInitialized();
            _resolutionStack.Clear();
            return TryResolveInternal(
                effectId,
                callSiteParameters,
                NormalizePhaseMask(phaseMask),
                intensity,
                timeOffset,
                output,
                diagnostics,
                localEffect,
                default,
                null);
        }

        internal bool TryResolve(
            ITextStudioEffectDefinition definition,
            TextStudioPhaseMask phaseMask,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics)
        {
            _previewSoloOwner = null;
            _previewSoloLayerId = null;
            if (definition == null || output == null)
                return false;

            TextStudioRegistry.EnsureInitialized();
            _resolutionStack.Clear();
            if (definition is TextStudioComposedEffectAsset composed)
            {
                return TryResolveComposed(
                    composed.Id,
                    composed,
                    TextStudioTagParameters.Empty,
                    NormalizePhaseMask(phaseMask),
                    1f,
                    0f,
                    output,
                    diagnostics,
                    composed,
                    default,
                    null);
            }

            if (!TextStudioEffects.TryCreate(
                    definition,
                    in TextStudioTagParameters.Empty,
                    out ITextStudioEffect effect))
                return false;

            output.Add(new TextStudioResolvedEffectBinding(
                effect,
                definition.Id,
                NormalizePhaseMask(phaseMask),
                1f,
                0f,
                definition is TextStudioEffectDefinitionAsset asset ? asset.BlendMode : EffectBlendMode.Additive,
                definition.DefaultArea,
                PhaseParams.NoOffset));
            return true;
        }

        bool TryResolveInternal(
            string effectId,
            TextStudioTagParameters parameters,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics,
            TextStudioComposedEffectAsset localEffect,
            in PhaseBlendScope phaseBlendScope,
            TextStudioComposedEnvelopeStack envelope)
        {
            string canonicalId = ResolveEffectId(effectId);
            if (string.IsNullOrEmpty(canonicalId))
            {
                diagnostics?.Add(
                    TextStudioDiagnosticSeverity.Error,
                    TextStudioDiagnosticCode.InvalidEffectInvocation,
                    "Effect invocation is missing an effect id.",
                    effectId);
                return false;
            }

            TextStudioComposedEffectAsset composed = null;
            if (localEffect != null &&
                string.Equals(canonicalId, localEffect.Id, StringComparison.OrdinalIgnoreCase))
            {
                composed = localEffect;
            }
            else if (TextStudioRegistry.Effects.TryGetQuiet(
                         canonicalId,
                         out ITextStudioEffectDefinition effect))
            {
                composed = effect as TextStudioComposedEffectAsset;
            }

            if (composed == null)
            {
                output.Add(new TextStudioResolvedEffectBinding(
                    canonicalId,
                    parameters,
                    NormalizePhaseMask(phaseMask),
                    intensity,
                    timeOffset).WithPhaseBlend(
                        phaseBlendScope.GroupId,
                        phaseBlendScope.Duration,
                        phaseBlendScope.PhaseMask,
                        phaseBlendScope.TimeOffset).WithEnvelope(envelope));
                return true;
            }

            return TryResolveComposed(
                canonicalId,
                composed,
                parameters,
                phaseMask,
                intensity,
                timeOffset,
                output,
                diagnostics,
                localEffect,
                phaseBlendScope,
                envelope);
        }

        bool TryResolveComposed(
            string canonicalId,
            TextStudioComposedEffectAsset composed,
            TextStudioTagParameters parameters,
            TextStudioPhaseMask phaseMask,
            float intensity,
            float timeOffset,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics,
            TextStudioComposedEffectAsset localEffect,
            in PhaseBlendScope inheritedPhaseBlendScope,
            TextStudioComposedEnvelopeStack envelope)
        {
            canonicalId = string.IsNullOrWhiteSpace(canonicalId) ? composed.Id : canonicalId;

            if (ContainsStack(canonicalId))
            {
                diagnostics?.Add(
                    TextStudioDiagnosticSeverity.Error,
                    TextStudioDiagnosticCode.ComposedEffectCycle,
                    "Composed effect cycle detected while resolving '" + canonicalId + "'.",
                    canonicalId);
                return false;
            }

            _resolutionStack.Add(canonicalId);
            bool ok = true;
            PhaseBlendScope phaseBlendScope = inheritedPhaseBlendScope;
            if (!phaseBlendScope.IsEnabled && composed.RevealActiveBlendDuration > 0f)
            {
                phaseBlendScope = new PhaseBlendScope(
                    0,
                    composed.RevealActiveBlendDuration,
                    TextStudioPhaseMask.None,
                    timeOffset);
            }
            string soloLayerId = ResolvePreviewSolo(composed);
            List<string> compositionInvocations = new List<string>(8);
            if (string.IsNullOrEmpty(soloLayerId) &&
                !composed.TryGetTagInvocations(compositionInvocations, out string tagStackError))
            {
                diagnostics?.Add(
                    TextStudioDiagnosticSeverity.Error,
                    TextStudioDiagnosticCode.InvalidEffectInvocation,
                    "Composed effect '" + canonicalId + "' has an invalid Tag Stack: " + tagStackError,
                    canonicalId);
                ok = false;
            }
            else
            {
                PhaseBlendScope tagStackBlendScope = ResolveLanePhaseBlendScope(
                    in phaseBlendScope,
                    phaseMask,
                    timeOffset);
                for (int i = 0; i < compositionInvocations.Count; i++)
                {
                    if (!TextStudioEffectInvocationParser.TryParse(
                        compositionInvocations[i],
                        out TextStudioEffectInvocation invocation,
                        out string error))
                    {
                        diagnostics?.Add(
                            TextStudioDiagnosticSeverity.Error,
                            TextStudioDiagnosticCode.InvalidEffectInvocation,
                            "Composed effect '" + canonicalId + "' has an invalid Tag Stack entry: " + error,
                            canonicalId);
                        ok = false;
                        continue;
                    }

                    TextStudioPhaseMask childMask = IntersectPhaseMasks(phaseMask, invocation.PhaseMask);
                    if (childMask == TextStudioPhaseMask.None)
                        continue;

                    float invocationTimeOffset = timeOffset + invocation.TimeOffset;
                    int invocationOutputStart = output.Count;
                    ok &= TryResolveInternal(
                        invocation.EffectId,
                        InheritBindingParameters(parameters, invocation.Parameters),
                        childMask,
                        intensity * invocation.Intensity,
                        invocationTimeOffset,
                        output,
                        diagnostics,
                        localEffect,
                        tagStackBlendScope,
                        envelope);
                    if (invocation.HasRange)
                        for (int bindingIndex = invocationOutputStart; bindingIndex < output.Count; bindingIndex++)
                            output[bindingIndex] = output[bindingIndex].WithRange(invocation.Range);
                }
            }

            ok &= ResolveCompositionLayers(
                composed.Layers,
                parameters,
                phaseMask,
                intensity,
                timeOffset,
                output,
                diagnostics,
                soloLayerId,
                localEffect,
                phaseBlendScope,
                envelope);

            _resolutionStack.RemoveAt(_resolutionStack.Count - 1);
            return ok;
        }

        string ResolvePreviewSolo(TextStudioComposedEffectAsset composed)
        {
            if (!ReferenceEquals(composed, _previewSoloOwner) || string.IsNullOrEmpty(_previewSoloLayerId))
                return null;
            IReadOnlyList<TextStudioCompositionLayer> layers = composed.Layers;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] != null && layers[i].Enabled && layers[i].StableId == _previewSoloLayerId)
                    return _previewSoloLayerId;
            return null;
        }

        bool ResolveCompositionLayers(
            IReadOnlyList<TextStudioCompositionLayer> layers,
            TextStudioTagParameters parentParameters,
            TextStudioPhaseMask parentMask,
            float parentIntensity,
            float parentTimeOffset,
            List<TextStudioResolvedEffectBinding> output,
            TextStudioDiagnosticSink diagnostics,
            string soloLayerId,
            TextStudioComposedEffectAsset localEffect,
            in PhaseBlendScope phaseBlendScope,
            TextStudioComposedEnvelopeStack parentEnvelope)
        {
            if (layers == null)
                return true;

            bool ok = true;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioCompositionLayer layer = layers[i];
                if (layer == null || !layer.Enabled)
                    continue;

                if (!string.IsNullOrEmpty(soloLayerId) &&
                    !string.Equals(layer.StableId, soloLayerId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                TextStudioApplicationSelector layerSelector = layer.Selector;
                if (!TextStudioSelectorCompiler.TryCompile(in layerSelector,
                        TextStudioSelectorCapabilities.LayoutStable, out _, out string selectionError))
                {
                    diagnostics?.Add(TextStudioDiagnosticSeverity.Error,
                        TextStudioDiagnosticCode.InvalidEffectInvocation,
                        "Composed effect layer '" + layer.DisplayName + "' has an invalid text selection: " + selectionError,
                        layer.EffectId);
                    ok = false;
                    continue;
                }

                if (layer.Kind == TextStudioCompositionLayerKind.Writing)
                {
                    // Writing is a persistent treatment of the canonical glyph lifecycle.
                    // Parent playback and phase envelopes must not start or hide its strokes.
                    TextStudioComposedWritingRegistry.EnsureRegistered(layer);
                    output.Add(new TextStudioResolvedEffectBinding(
                        layer, layer.Id, TextStudioPhaseMask.All,
                        parentIntensity * layer.Intensity, 0f, layer.BlendMode,
                        layer.DefaultArea, layer.Timing).WithScope(
                            layer.SpatialInfluence, layer.SourceColorSelector)
                        .WithSelector(layer.Selector));
                    continue;
                }

                TextStudioPhaseMask layerMask = IntersectPhaseMasks(parentMask, layer.Phases);
                if (layerMask == TextStudioPhaseMask.None)
                    continue;

                float layerIntensity = parentIntensity * layer.Intensity;
                float layerEnvelopeTimeOffset = parentTimeOffset + layer.TimeOffset;
                if (layer.Kind == TextStudioCompositionLayerKind.Channel)
                {
                    PhaseBlendScope channelBlendScope = ResolveLanePhaseBlendScope(
                        in phaseBlendScope,
                        layerMask,
                        layerEnvelopeTimeOffset);
                    output.Add(new TextStudioResolvedEffectBinding(
                        layer,
                        layer.Id,
                        layerMask,
                        layerIntensity,
                        layerEnvelopeTimeOffset,
                        layer.BlendMode,
                        layer.DefaultArea,
                        layer.Timing).WithPhaseBlend(
                            channelBlendScope.GroupId,
                            channelBlendScope.Duration,
                            channelBlendScope.PhaseMask,
                            channelBlendScope.TimeOffset).WithScope(
                            layer.SpatialInfluence,
                            layer.SourceColorSelector).WithEnvelope(parentEnvelope)
                        .WithSelector(layer.Selector));
                    continue;
                }

                if (!layer.TryGetEffectInvocation(out TextStudioEffectInvocation invocation, out string error))
                {
                    diagnostics?.Add(
                        TextStudioDiagnosticSeverity.Error,
                        TextStudioDiagnosticCode.InvalidEffectInvocation,
                        "Composed effect layer '" + layer.DisplayName + "' is invalid: " + error,
                        layer.EffectId);
                    ok = false;
                    continue;
                }

                TextStudioPhaseMask effectMask = IntersectPhaseMasks(layerMask, invocation.PhaseMask);
                if (effectMask == TextStudioPhaseMask.None)
                    continue;

                TextStudioTagParameters layerParameters = ApplyLayerBindingParameters(
                    invocation.Parameters,
                    layer.DefaultArea,
                    layer.Timing);
                float invocationTimeOffset = layerEnvelopeTimeOffset + invocation.TimeOffset;
                PhaseBlendScope effectBlendScope = ResolveLanePhaseBlendScope(
                    in phaseBlendScope,
                    effectMask,
                    layerEnvelopeTimeOffset);
                TextStudioComposedEnvelopeStack layerEnvelope =
                    TextStudioComposedEnvelopeStack.Append(parentEnvelope, layer);
                int outputStart = output.Count;
                bool layerResolved = TryResolveInternal(
                    invocation.EffectId,
                    InheritBindingParameters(parentParameters, layerParameters),
                    effectMask,
                    layerIntensity * invocation.Intensity,
                    invocationTimeOffset,
                    output,
                    diagnostics,
                    localEffect,
                    effectBlendScope,
                    layerEnvelope);
                ok &= layerResolved;
                if (layerResolved)
                {
                    TextStudioSequenceSettings sequence = layer.Sequence;
                    for (int bindingIndex = outputStart; bindingIndex < output.Count; bindingIndex++)
                    {
                        output[bindingIndex] = output[bindingIndex]
                            .WithSequence(in sequence)
                            .WithScope(layer.SpatialInfluence, layer.SourceColorSelector)
                            .WithSelector(layer.Selector);
                        if (invocation.HasRange)
                            output[bindingIndex] = output[bindingIndex].WithRange(invocation.Range);
                    }
                }
            }

            return ok;
        }

        PhaseBlendScope ResolveLanePhaseBlendScope(
            in PhaseBlendScope phaseBlendScope,
            TextStudioPhaseMask phaseMask,
            float timeOffset)
        {
            if (!phaseBlendScope.IsEnabled || phaseBlendScope.HasAllocatedGroup)
                return phaseBlendScope;

            return new PhaseBlendScope(
                AllocatePhaseBlendGroupId(),
                phaseBlendScope.Duration,
                phaseMask,
                timeOffset);
        }

        int AllocatePhaseBlendGroupId()
        {
            int groupId = _nextPhaseBlendGroupId++;
            if (_nextPhaseBlendGroupId <= 0)
                _nextPhaseBlendGroupId = 1;
            return groupId;
        }

        static string ResolveEffectId(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return string.Empty;

            string trimmed = effectId.Trim();
            return TextStudioPhaseTagUtility.TryResolveEffectId(trimmed, out string canonicalId)
                ? canonicalId
                : trimmed;
        }

        static TextStudioPhaseMask IntersectPhaseMasks(
            TextStudioPhaseMask parent,
            TextStudioPhaseMask child)
        {
            if (parent == TextStudioPhaseMask.None || child == TextStudioPhaseMask.None)
                return TextStudioPhaseMask.None;
            return parent & child;
        }

        static TextStudioPhaseMask NormalizePhaseMask(TextStudioPhaseMask phaseMask)
        {
            return phaseMask == TextStudioPhaseMask.None ? TextStudioPhaseMask.All : phaseMask;
        }

        static TextStudioTagParameters InheritBindingParameters(
            TextStudioTagParameters parent,
            TextStudioTagParameters child)
        {
            if (!TextStudioEffectParameterUtility.HasAny(in parent))
                return child;

            List<TextStudioNamedTagParameter> merged = null;
            for (int parameterIndex = 0; parameterIndex < s_inheritedBindingParameterNames.Length; parameterIndex++)
            {
                string parameterName = s_inheritedBindingParameterNames[parameterIndex];
                if (!parent.TryGetString(parameterName, out string parentValue) ||
                    child.TryGetString(parameterName, out _))
                {
                    continue;
                }

                if (merged == null)
                {
                    merged = new List<TextStudioNamedTagParameter>(
                        child.Named.Count + s_inheritedBindingParameterNames.Length);
                    for (int childIndex = 0; childIndex < child.Named.Count; childIndex++)
                        merged.Add(child.Named[childIndex]);
                }

                merged.Add(new TextStudioNamedTagParameter(parameterName, parentValue, false));
            }

            if (merged == null)
                return child;

            return TextStudioEffectParameterUtility.CreateCanonicalParameters(child.Positional, merged);
        }

        static TextStudioTagParameters ApplyLayerBindingParameters(
            TextStudioTagParameters child,
            TextStudioEffectAreaSettings area,
            PhaseParams timing)
        {
            List<TextStudioNamedTagParameter> merged = new List<TextStudioNamedTagParameter>(
                child.Named.Count + s_inheritedBindingParameterNames.Length);
            for (int i = 0; i < child.Named.Count; i++)
            {
                TextStudioNamedTagParameter parameter = child.Named[i];
                if (!IsLayerOwnedBindingParameter(parameter.Name))
                    merged.Add(parameter);
            }

            merged.Add(new TextStudioNamedTagParameter(
                "area",
                area.Mode.ToString().ToLowerInvariant(),
                false));
            merged.Add(new TextStudioNamedTagParameter(
                "group",
                Math.Max(1, area.GroupSize).ToString(CultureInfo.InvariantCulture),
                false));
            merged.Add(new TextStudioNamedTagParameter(
                "charOffset",
                Math.Max(0f, timing.charOffset).ToString("R", CultureInfo.InvariantCulture),
                false));
            merged.Add(new TextStudioNamedTagParameter(
                "wordOffset",
                Math.Max(0f, timing.wordOffset).ToString("R", CultureInfo.InvariantCulture),
                false));
            merged.Add(new TextStudioNamedTagParameter(
                "lineOffset",
                Math.Max(0f, timing.lineOffset).ToString("R", CultureInfo.InvariantCulture),
                false));

            return TextStudioEffectParameterUtility.CreateCanonicalParameters(child.Positional, merged);
        }

        static bool IsLayerOwnedBindingParameter(string name)
        {
            for (int i = 0; i < s_inheritedBindingParameterNames.Length; i++)
            {
                if (string.Equals(name, s_inheritedBindingParameterNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        bool ContainsStack(string effectId)
        {
            for (int i = 0; i < _resolutionStack.Count; i++)
            {
                if (string.Equals(_resolutionStack[i], effectId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
