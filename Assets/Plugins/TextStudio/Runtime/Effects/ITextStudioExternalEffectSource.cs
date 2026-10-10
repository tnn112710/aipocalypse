namespace TextStudio
{
    /// <summary>
    /// Opt-in source for renderer-neutral effect contributions. Active sources are polled after the
    /// semantic plan is current and before layout, in host registration order.
    /// </summary>
    public interface ITextStudioExternalEffectSource
    {
        /// <summary>Whether the source currently has a contribution to submit.</summary>
        bool IsActive { get; }

        /// <summary>Submits zero or more ordered effect contributions for the current update.</summary>
        void Submit(in TextStudioExternalEffectSubmissionContext context);
    }

    /// <summary>Allocation-free submission view passed to an external effect source.</summary>
    public readonly struct TextStudioExternalEffectSubmissionContext
    {
        readonly TextStudioRuntimeCore _runtime;

        /// <summary>Current host effect-control facet.</summary>
        public ITextStudioEffectControl Effects { get; }

        /// <summary>Current compiled semantic plan.</summary>
        public TextStudioPlanView Plan { get; }

        internal TextStudioExternalEffectSubmissionContext(
            TextStudioRuntimeCore runtime,
            ITextStudioEffectControl effects,
            TextStudioCompiledPlan plan)
        {
            _runtime = runtime;
            Effects = effects;
            Plan = new TextStudioPlanView(plan);
        }

        /// <summary>Adds one contribution after previously submitted external effects.</summary>
        public bool Submit(
            ITextStudioEffectDefinition effect,
            float progress,
            float intensity = 1f,
            TextStudioTextRange range = default)
        {
            return _runtime != null &&
                   _runtime.ApplyExternalEffect(
                       effect,
                       progress,
                       intensity,
                       range);
        }
    }
}
