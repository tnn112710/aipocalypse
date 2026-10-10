namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Base class for stateless code-defined effects. All parameters are fields with sensible defaults.
    /// These never reference UnityEngine.Object subclasses so they are safe to construct at static init.
    /// </summary>
    public abstract class CodeEffectBase : ITextStudioEffect, ITextStudioBuiltIn,
        ITextStudioEffectFactory, ITextStudioEffectMetadataProvider,
        ITextStudioEffectCapabilitiesProvider
    {
        TextStudioEffectMetadata _metadata;
        TextStudioEffectCapabilities _capabilities;
        bool _hasCapabilities;

        public abstract string Id { get; }
        public virtual string Category => "Effects";
        public virtual string DisplayName => Id;
        public virtual string Description => "";
        public virtual TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.All;
        public virtual TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Default;
        public virtual TextStudioEffectPlaybackRole PlaybackRole => TextStudioEffectPlaybackRole.Envelope;
        public virtual TextStudioEffectPlaybackPreset DefaultPlayback => TextStudioEffectPlaybackPreset.External;
        public virtual TextStudioEffectCurvePreset DefaultCurve => TextStudioEffectCurvePreset.Hold;
        public virtual float DefaultPlaybackDuration => 1f;
        public virtual float DefaultDelayBetweenCycles => 0f;
        public virtual EffectBlendMode BlendMode => EffectBlendMode.Additive;
        // Built-in capabilities derive only from the constant id and default phases, so each instance resolves them once.
        public virtual TextStudioEffectCapabilities Capabilities
        {
            get
            {
                if (!_hasCapabilities)
                {
                    _capabilities = BuiltInEffectCapabilities.For(Id, DefaultPhases);
                    _hasCapabilities = true;
                }
                return _capabilities;
            }
        }
        public TextStudioEffectMetadata Metadata => _metadata ??= CreateMetadata();

        /// <summary>Creates cached authoring metadata for this code-defined effect.</summary>
        protected virtual TextStudioEffectMetadata CreateMetadata()
        {
            return new TextStudioEffectMetadata(
                Category,
                DisplayName,
                Description,
                "<" + Id + ">",
                DefaultPhases,
                TextStudioBuiltInParameterMetadata.Create(
                    this,
                    BuiltInPlaybacks.CopyTextAnimatorPlaybackKeywords()),
                PlaybackRole,
                DefaultPlayback,
                DefaultCurve,
                DefaultPlaybackDuration,
                DefaultDelayBetweenCycles);
        }

        public abstract void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context);

        internal virtual bool SupportsPersistentFastPath => false;

        internal virtual bool SupportsUniformPersistentState => false;

        /// <summary>True when the persistent fast path writes render properties through its writer overload.</summary>
        internal virtual bool WritesPersistentRenderProperties => false;

        internal virtual bool ReusesPersistentStateBetweenSteps => false;

        internal virtual bool HasSamePersistentState(float previousTime, float currentTime)
        {
            return false;
        }

        internal virtual void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
        }

        internal virtual void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            EvaluatePersistentFast(
                ref state,
                time,
                playbackTime,
                phaseProgress,
                unitIndex,
                normalizedIndex);
        }

        public virtual void Reset() { }

        public virtual ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            // Parameter names belong to the prototype schema, shared by all bound instances.
            if (parameters.Named.Count > 0)
                _metadata ??= CreateMetadata();
            CodeEffectBase clone = (CodeEffectBase)MemberwiseClone();
            clone._metadata = _metadata;
            if (!TextStudioEffectParameterUtility.HasAny(in parameters))
                return clone;
            TextStudioBuiltInParameterBinder.Apply(clone, in parameters);
            clone.ApplyCustomParameters(in parameters);
            return clone;
        }

        /// <summary>Allows an add-on effect family to bind parameters without base-assembly type knowledge.</summary>
        protected virtual void ApplyCustomParameters(in TextStudioTagParameters parameters)
        {
        }
    }
}
