using System;

namespace TextStudio
{
    /// <summary>
    /// Capability flags that describe what a renderer can express for a given frame.
    /// Used to warn users about mismatches (e.g. color effects on a monochrome tile board).
    /// </summary>
    [Flags]
    public enum RendererCapabilities
    {
        None = 0,
        WorldSpace = 1 << 0,
        UIOverlay = 1 << 1,
        SupportsPerGlyphColor = 1 << 2,
        SupportsPerGlyphTRS = 1 << 3,
        SupportsPerGlyphAlpha = 1 << 4,
        SupportsIdleFrameSkipping = 1 << 5,
        ConsumesRawFrameState = 1 << 6,
    }

    /// <summary>
    /// Renderer add-on contract. Components that visualize TextStudio output
    /// (extruded 3D text, mechanical flip tiles, custom backends) implement this
    /// interface and are auto-detected by the <see cref="TextStudioText"/> hub.
    ///
    /// Drive-all semantics: every enabled renderer receives every frame. Visibility
    /// is managed by the user enabling/disabling the component like any other Unity
    /// MonoBehaviour - the hub never hides renderers for the user.
    /// </summary>
    public interface ITextStudioRenderer
    {
        /// <summary>
        /// True when the renderer is fully initialized and ready to consume frames.
        /// Returning false skips this renderer for the current tick without logging.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// What this renderer can express. Most flags are informational;
        /// <see cref="RendererCapabilities.SupportsIdleFrameSkipping"/> allows the hub to skip
        /// unchanged frames because the renderer schedules its own asynchronous updates, while
        /// <see cref="RendererCapabilities.ConsumesRawFrameState"/> allows it to reconstruct
        /// composed glyph state directly from the frame's placement and animation arrays.
        /// </summary>
        RendererCapabilities Capabilities { get; }

        /// <summary>Called when the hub binds to this renderer (OnEnable or when added at runtime).</summary>
        void Attach(TextStudioText hub);

        /// <summary>Called when the hub disconnects (OnDisable, hub removed, component destroyed).</summary>
        void Detach();

        /// <summary>
        /// Consume one built frame. Called once per pipeline tick on every enabled renderer.
        /// Implementations should honor <see cref="TextStudioFrame.Dirty"/> for incremental writes
        /// and <see cref="TextStudioFrame.GlobalDirty"/> for topology rebuilds. Frame buffers are
        /// engine-owned views whose contents may change on a later tick; copy only data that must
        /// remain stable after the call.
        /// </summary>
        void Render(in TextStudioFrame frame);

        /// <summary>
        /// Reveal lifecycle hook for renderers with external state (tile schedulers,
        /// mesh queues). Called after <see cref="RevealController.Reset"/> so the renderer
        /// can flush any cached per-glyph state before the next tick.
        /// </summary>
        void OnRevealReset();
    }

    /// <summary>
    /// Optional renderer extension contract for precise effect-channel compatibility.
    /// Text Studio keeps effect evaluation renderer-agnostic; renderers use this
    /// contract to advertise which parts of the shared per-glyph state they display.
    /// </summary>
    public interface ITextStudioRendererChannelSupport
    {
        /// <summary>Shared Text Studio channels faithfully rendered by this backend.</summary>
        TextStudioRenderChannels SupportedRenderChannels { get; }
    }

    /// <summary>
    /// Optional renderer contract used by authoring catalogs and diagnostics to distinguish
    /// flat text from extrusion-specific output without depending on an add-on assembly.
    /// </summary>
    public interface ITextStudioRenderDomainProvider
    {
        TextStudioRenderDomain RenderDomain { get; }
    }

    /// <summary>How one compiled effect binding is executed for the current renderer set.</summary>
    public enum TextStudioRendererEffectExecution : byte
    {
        /// <summary>The shared CPU evaluator remains authoritative.</summary>
        Cpu = 0,

        /// <summary>
        /// The renderer can reproduce the complete binding exactly without shared CPU evaluation.
        /// The hub still makes the final all-renderer ownership decision.
        /// </summary>
        RendererExact = 1
    }

    /// <summary>
    /// Allocation-free request used to negotiate exact renderer execution for one compiled effect
    /// binding. The plan and effect instances are engine-owned and valid only during the call.
    /// </summary>
    public readonly struct TextStudioRendererEffectExecutionRequest
    {
        /// <summary>The complete semantic plan containing the requested binding.</summary>
        public TextStudioPlanView Plan { get; }

        /// <summary>The binding's stable index inside <see cref="TextStudioPlanView.EffectBindings"/>.</summary>
        public int BindingIndex { get; }

        /// <summary>The resolved compiled binding.</summary>
        public EffectBinding Binding { get; }

        /// <summary>The resolved effect instance used by the shared evaluator.</summary>
        public ITextStudioEffect Effect { get; }

        /// <summary>The effect's complete declared runtime capabilities.</summary>
        public TextStudioEffectCapabilities Capabilities { get; }

        /// <summary>The effective project and component motion policy for this frame.</summary>
        public TextStudioMotionPolicy MotionPolicy { get; }

        /// <summary>Whether every visible glyph is in the stable persistent phase for this negotiation pass.</summary>
        public bool AllGlyphsPersistent { get; }

        /// <summary>Whether the persistent effect family is enabled for this negotiation pass.</summary>
        public bool PersistentEffectsEnabled { get; }

        /// <summary>
        /// Hub-local revision shared by every binding request in one negotiation pass. Renderers may
        /// use equality only to cache common capability checks for that pass.
        /// </summary>
        public uint NegotiationVersion { get; }

        internal TextStudioRendererEffectExecutionRequest(
            TextStudioCompiledPlan plan,
            int bindingIndex,
            in EffectBinding binding,
            ITextStudioEffect effect,
            in TextStudioEffectCapabilities capabilities,
            TextStudioMotionPolicy motionPolicy,
            bool allGlyphsPersistent,
            bool persistentEffectsEnabled,
            uint negotiationVersion)
        {
            Plan = new TextStudioPlanView(plan);
            BindingIndex = bindingIndex;
            Binding = binding;
            Effect = effect;
            Capabilities = capabilities;
            MotionPolicy = motionPolicy;
            AllGlyphsPersistent = allGlyphsPersistent;
            PersistentEffectsEnabled = persistentEffectsEnabled;
            NegotiationVersion = negotiationVersion;
        }
    }

    /// <summary>
    /// Optional renderer contract for exact prepared execution of compiled effect bindings.
    /// Returning <see cref="TextStudioRendererEffectExecution.RendererExact"/> is a capability
    /// vote only. The hub defers shared CPU evaluation when every active renderer whose domain
    /// overlaps the effect accepts the same binding and no other frame consumer requires CPU state.
    /// </summary>
    public interface ITextStudioRendererEffectExecution
    {
        /// <summary>Reports whether this renderer can own the complete binding for the current frame.</summary>
        TextStudioRendererEffectExecution GetEffectExecution(
            in TextStudioRendererEffectExecutionRequest request);
    }

    /// <summary>How an external renderer relates to the text backend's primary visual output.</summary>
    public enum TextStudioPrimaryOutputOwnership : byte
    {
        /// <summary>The renderer does not make an explicit primary-output ownership request.</summary>
        None = 0,

        /// <summary>The renderer replaces the backend output with independently owned rendering.</summary>
        ReplaceBackendOutput = 1,

        /// <summary>The renderer replaces the output while reusing the backend's render components.</summary>
        ReuseBackendComponents = 2
    }

    /// <summary>
    /// Optional renderer contract for declaring primary visual-output ownership without coupling
    /// the Text Studio hub to TMP, a particular render component, or a specific add-on.
    /// Layout, editing, accessibility, and source data remain owned by the active text backend.
    /// </summary>
    public interface ITextStudioPrimaryOutputOwner
    {
        /// <summary>Describes whether the renderer replaces or reuses the backend's visual output.</summary>
        TextStudioPrimaryOutputOwnership PrimaryOutputOwnership { get; }
    }
}
