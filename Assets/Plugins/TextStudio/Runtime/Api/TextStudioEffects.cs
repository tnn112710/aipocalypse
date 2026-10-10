namespace TextStudio
{
    /// <summary>
    /// Registers and resolves effect definitions, curves, and playback strategies.
    /// Runtime registrations have priority over project and built-in entries.
    /// </summary>
    public static class TextStudioEffects
    {
        /// <summary>Register an effect definition in the runtime tier.</summary>
        public static void Register(ITextStudioEffectDefinition effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.Id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.Effects.Register(effect.Id, effect);
        }

        /// <summary>Resolve a leaf effect evaluator by identifier without logging.</summary>
        public static bool TryGet(string id, out ITextStudioEffect effect)
        {
            effect = null;
            if (!TryGetDefinition(id, out ITextStudioEffectDefinition definition))
                return false;
            return TryCreate(definition, in TextStudioTagParameters.Empty, out effect);
        }

        /// <summary>Resolve a leaf evaluator or expansion-only effect definition by identifier.</summary>
        public static bool TryGetDefinition(string id, out ITextStudioEffectDefinition effect)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Effects.TryGetQuiet(id, out effect);
        }

        /// <summary>
        /// Create a detached evaluator and apply tag parameters without mutating its registered definition.
        /// </summary>
        public static bool TryCreate(
            string id,
            in TextStudioTagParameters parameters,
            out ITextStudioEffect effect)
        {
            effect = null;
            if (!TryGetDefinition(id, out ITextStudioEffectDefinition definition))
                return false;
            return TryCreate(definition, in parameters, out effect);
        }

        /// <summary>
        /// Create a detached evaluator from a known definition without performing another registry lookup.
        /// </summary>
        public static bool TryCreate(
            ITextStudioEffectDefinition definition,
            in TextStudioTagParameters parameters,
            out ITextStudioEffect effect)
        {
            effect = null;
            if (definition == null)
                return false;
            effect = definition is ITextStudioEffectFactory factory
                ? factory.CreateEffect(in parameters)
                : definition as ITextStudioEffect;
            return effect != null;
        }

        /// <summary>Remove the highest-priority user-owned effect definition.</summary>
        public static bool Unregister(string id)
        {
            return TextStudioRegistry.Effects.Unregister(id);
        }

        /// <summary>Register an effect curve in the runtime tier.</summary>
        public static void RegisterCurve(string id, ITextStudioCurve curve)
        {
            if (curve == null || string.IsNullOrWhiteSpace(id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.Curves.Register(id, curve);
        }

        /// <summary>Resolve an effect curve by identifier without logging.</summary>
        public static bool TryGetCurve(string id, out ITextStudioCurve curve)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Curves.TryGetQuiet(id, out curve);
        }

        /// <summary>Remove the highest-priority user-owned curve.</summary>
        public static bool UnregisterCurve(string id)
        {
            return TextStudioRegistry.Curves.Unregister(id);
        }

        /// <summary>Register an effect playback strategy in the runtime tier.</summary>
        public static void RegisterPlayback(string id, IEffectPlayback playback)
        {
            if (playback == null || string.IsNullOrWhiteSpace(id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.Playbacks.Register(id, playback);
        }

        /// <summary>Resolve an effect playback strategy by identifier without logging.</summary>
        public static bool TryGetPlayback(string id, out IEffectPlayback playback)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Playbacks.TryGetQuiet(id, out playback);
        }

        /// <summary>Remove the highest-priority user-owned playback strategy.</summary>
        public static bool UnregisterPlayback(string id)
        {
            return TextStudioRegistry.Playbacks.Unregister(id);
        }
    }
}
