namespace MB13.Aipocalypse.Core.Entities
{
    // When a scene entity injected by its scope may start acting. [Inject] Construct can run before or after Start (scopes
    // build in Awake order); the first activation waits for both, and each re-enable after that activates again.
    public sealed class ActivationGate
    {
        public bool Started { get; private set; }
        public bool Injected { get; private set; }

        // From Construct: true when it should activate now (Start already ran and it's enabled).
        public bool OnConstructed(bool enabled)
        {
            Injected = true;
            return Started && enabled;
        }

        // From Start: true when it should activate now; false means it wasn't injected (yet).
        public bool OnStarted()
        {
            Started = true;
            return Injected;
        }

        // From OnEnable: true when it should activate again (the first OnEnable comes before Start, so it waits).
        public bool OnEnabled() => Started && Injected;
    }
}
