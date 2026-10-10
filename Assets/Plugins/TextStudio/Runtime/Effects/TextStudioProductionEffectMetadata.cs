using System;

namespace TextStudio
{
    [Flags]
    public enum TextStudioRenderDomain
    {
        None = 0,
        FlatText = 1 << 0,
        Extruded3D = 1 << 1,
        All = FlatText | Extruded3D
    }

    public enum TextStudioEffectCompatibilityState
    {
        Compatible,
        PartiallyCompatible,
        Unsupported,
        MissingDependency
    }

    /// <summary>Compatibility result for an effect definition against one or more requested render domains.</summary>
    public readonly struct TextStudioEffectCompatibility
    {
        public TextStudioEffectCompatibilityState State { get; }
        public TextStudioRenderDomain SupportedDomains { get; }
        public string Reason { get; }

        public bool IsCompatible => State == TextStudioEffectCompatibilityState.Compatible;

        /// <summary>Creates a compatibility result with supported domains and user-facing guidance.</summary>
        public TextStudioEffectCompatibility(
            TextStudioEffectCompatibilityState state,
            TextStudioRenderDomain supportedDomains,
            string reason = "")
        {
            State = state;
            SupportedDomains = supportedDomains;
            Reason = reason ?? string.Empty;
        }
    }

    public enum TextStudioEffectPerformanceTier
    {
        Realtime,
        VertexStream,
        Material,
        GeometryRefresh,
        PreparedDeformation,
        Simulation
    }

    /// <summary>Describes how an effect or renderer property can change rendered spatial bounds.</summary>
    public enum TextStudioEffectBoundsBehavior
    {
        Unchanged,
        DynamicOutput,
        GeometryRefresh
    }

    public enum TextStudioReducedMotionBehavior
    {
        Preserve,
        Reduce,
        Disable
    }

    public enum TextStudioMotionPolicy
    {
        Inherit,
        Full,
        Reduced,
        None
    }

    /// <summary>Immutable phase, channel, performance, accessibility, determinism, render-domain, and bounds metadata for an effect.</summary>
    public readonly struct TextStudioEffectCapabilities
    {
        public TextStudioRenderChannels RenderChannels { get; }
        public TextStudioPhaseMask SupportedPhases { get; }
        public TextStudioEffectPerformanceTier PerformanceTier { get; }
        public TextStudioReducedMotionBehavior ReducedMotionBehavior { get; }
        public bool IsDeterministic { get; }
        public bool CanFlash { get; }
        public TextStudioRenderDomain SupportedRenderDomains { get; }
        public TextStudioEffectBoundsBehavior BoundsBehavior { get; }
        /// <summary>True when the effect reads the shared spatial target from its evaluation context directly, without a Target Distance scope.</summary>
        public bool ReadsInfluenceTarget { get; }

        /// <summary>Creates complete effect capability metadata with explicit performance and accessibility behavior.</summary>
        public TextStudioEffectCapabilities(
            TextStudioRenderChannels renderChannels,
            TextStudioPhaseMask supportedPhases,
            TextStudioEffectPerformanceTier performanceTier = TextStudioEffectPerformanceTier.Realtime,
            TextStudioReducedMotionBehavior reducedMotionBehavior = TextStudioReducedMotionBehavior.Reduce,
            bool isDeterministic = true,
            bool canFlash = false,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All,
            TextStudioEffectBoundsBehavior boundsBehavior =
                TextStudioEffectBoundsBehavior.Unchanged,
            bool readsInfluenceTarget = false)
        {
            RenderChannels = renderChannels;
            SupportedPhases = supportedPhases;
            PerformanceTier = performanceTier;
            ReducedMotionBehavior = reducedMotionBehavior;
            IsDeterministic = isDeterministic;
            CanFlash = canFlash;
            SupportedRenderDomains = supportedRenderDomains;
            BoundsBehavior = boundsBehavior;
            ReadsInfluenceTarget = readsInfluenceTarget;
        }
    }

    /// <summary>Publishes renderer compatibility, performance, phase, and accessibility metadata for an effect.</summary>
    public interface ITextStudioEffectCapabilitiesProvider
    {
        /// <summary>Gets immutable capabilities used by authoring validation and runtime compatibility checks.</summary>
        TextStudioEffectCapabilities Capabilities { get; }
    }

    /// <summary>Resolves supported render domains and actionable compatibility status for effect definitions.</summary>
    public static class TextStudioEffectCompatibilityResolver
    {
        /// <summary>Gets declared render domains, defaulting to all domains for definitions without capability metadata.</summary>
        public static TextStudioRenderDomain GetSupportedDomains(ITextStudioEffectDefinition definition)
        {
            if (definition is ITextStudioEffectCapabilitiesProvider provider)
                return provider.Capabilities.SupportedRenderDomains;
            return TextStudioRenderDomain.All;
        }

        /// <summary>Compares an effect's declared domains with a requested renderer domain set.</summary>
        public static TextStudioEffectCompatibility Resolve(
            ITextStudioEffectDefinition definition,
            TextStudioRenderDomain requestedDomains)
        {
            if (definition == null)
            {
                return new TextStudioEffectCompatibility(
                    TextStudioEffectCompatibilityState.MissingDependency,
                    TextStudioRenderDomain.None,
                    "The effect definition is not installed.");
            }

            TextStudioRenderDomain supported = GetSupportedDomains(definition);
            TextStudioRenderDomain overlap = supported & requestedDomains;
            if (overlap == TextStudioRenderDomain.None)
            {
                return new TextStudioEffectCompatibility(
                    TextStudioEffectCompatibilityState.Unsupported,
                    supported,
                    BuildUnsupportedReason(supported));
            }

            if (overlap != requestedDomains)
            {
                return new TextStudioEffectCompatibility(
                    TextStudioEffectCompatibilityState.PartiallyCompatible,
                    supported,
                    "The effect is only supported by some active text renderers.");
            }

            return new TextStudioEffectCompatibility(
                TextStudioEffectCompatibilityState.Compatible,
                supported);
        }

        /// <summary>Builds user-facing guidance for requested render domains outside the supported set.</summary>
        public static string BuildUnsupportedReason(TextStudioRenderDomain supported)
        {
            if (supported == TextStudioRenderDomain.FlatText)
                return "This effect is available for flat text only.";
            if (supported == TextStudioRenderDomain.Extruded3D)
                return "This effect requires the 3D add-on and an extruded text renderer.";
            return "This effect does not support the selected text renderer.";
        }
    }

    /// <summary>Global reduced-motion policy used to resolve effect and transition behavior.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioMotionSettings
    {
        static TextStudioMotionPolicy _globalPolicy = TextStudioMotionPolicy.Full;

        /// <summary>Occurs after the global motion policy changes.</summary>
        public static event Action Changed;

        public static TextStudioMotionPolicy GlobalPolicy
        {
            get => _globalPolicy;
            set
            {
                TextStudioMotionPolicy resolved = value == TextStudioMotionPolicy.Inherit
                    ? TextStudioMotionPolicy.Full
                    : value;
                if (_globalPolicy == resolved)
                    return;

                _globalPolicy = resolved;
                Changed?.Invoke();
            }
        }

        /// <summary>Combines a local motion preference with the active global accessibility policy.</summary>
        public static TextStudioMotionPolicy Resolve(TextStudioMotionPolicy localPolicy)
        {
            return localPolicy == TextStudioMotionPolicy.Inherit ? _globalPolicy : localPolicy;
        }
    }
}
