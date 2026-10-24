namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        CompiledTextStudioAppearancePlan _compiledAppearancePlan;

        /// <summary>The composed appearances resolved for this source, including local and catalog invocations.</summary>
        public CompiledTextStudioAppearancePlan CompiledAppearancePlan => _compiledAppearancePlan;
        internal CompiledTextStudioAppearancePlan ActiveCompiledAppearancePlan =>
            SupportsActiveTextStudio2DAppearance ? _compiledAppearancePlan : null;
        internal TextStudio2DStyleSettings ActiveAppearanceSettings => AppearanceSource == TextStudioAppearanceSource.Inline
            ? _inlineAppearanceSettings : AppearanceStyle != null ? AppearanceStyle.GetSettingsForCompilation() : default;

        void EnsureAppearancePlanCurrent(bool force = false)
        {
            if (_compiledAppearanceStyle == null) return;
            using (AppearancePlanProfilerMarker.Auto())
                EnsureCompiledAppearancePlanCurrent(force);
        }

        void EnsureCompiledAppearancePlanCurrent(bool force)
        {
            if (!force && TextStudioAppearancePlanCompiler.IsCurrent(_compiledAppearancePlan, _runtime.Plan, _compiledAppearanceStyle)) return;
            // A text change that keeps the unit count of untagged text would compile an identical default-only plan.
            if (!force && TextStudioAppearancePlanCompiler.CanReuseDefaultOnly(_compiledAppearancePlan, _runtime.Plan, _compiledAppearanceStyle)) return;
            // A parked hub relies on the plan it parked with polling nothing.
            Unpark();
            CompiledTextStudioAppearancePlan previous = _compiledAppearancePlan;
            TextStudio2DStyleSettings settings = ActiveAppearanceSettings;
            CompiledTextStudioAppearancePlan current = TextStudioAppearancePlanCompiler.Compile(
                _runtime.Plan, _compiledAppearanceStyle, in settings, AppearanceStyle, previous);
            _compiledAppearancePlan = current;
            if (!SupportsActiveTextStudio2DAppearance || previous != null && previous.Fingerprint == current.Fingerprint) return;
            DirtyFlags dirty = DirtyFlags.Visual;
            if (previous == null || previous.Fingerprint.Topology != current.Fingerprint.Topology)
                dirty |= DirtyFlags.AppearanceTopology | DirtyFlags.Layout;
            if (previous == null || previous.Fingerprint.Parameters != current.Fingerprint.Parameters)
                dirty |= DirtyFlags.AppearanceParameters;
            if (previous == null || previous.Fingerprint.RenderState != current.Fingerprint.RenderState)
                dirty |= DirtyFlags.AppearanceRenderState;
            if (previous == null || previous.Fingerprint.Bounds != current.Fingerprint.Bounds)
                dirty |= DirtyFlags.AppearanceBounds | DirtyFlags.Layout;
            if (previous == null || previous.IsPassThrough != current.IsPassThrough) dirty |= DirtyFlags.Layout;
            _runtime.SetDirty(dirty);
            if (_isTicking) _globalDirtyThisTick |= dirty;
        }

        internal void HandleAppearanceCatalogChanged()
        {
            if (_compiledAppearancePlan == null || !_compiledAppearancePlan.HasInvocations ||
                _compiledAppearancePlan.AreCatalogBindingsCurrent()) return;
            EnsureAppearancePlanCurrent();
            if (!UnityEngine.Application.isPlaying) TextStudioEditorHooks.RequestRepaint();
        }

        bool IsAppearanceInvocationDependency(TextStudio2DStyleAsset candidate)
        {
            if (_compiledAppearancePlan == null || candidate == null) return false;
            foreach (TextStudio2DStyleAsset dependency in _compiledAppearancePlan.Dependencies)
            {
                int depth = 0;
                for (TextStudio2DStyleAsset current = dependency; current != null && depth++ < 64;)
                {
                    if (current == candidate) return true;
                    if (current.InheritanceMode == TextStudio2DStyleInheritanceMode.Replace) break;
                    current = current.BaseStyle;
                    // Cyclic inheritance is diagnosed by the compiler; dependency polling must terminate as well.
                    if (current == dependency) break;
                }
            }
            return false;
        }
    }
}
