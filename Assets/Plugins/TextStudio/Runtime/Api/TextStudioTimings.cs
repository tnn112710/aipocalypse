namespace TextStudio
{
    /// <summary>Registers and resolves reusable typing-timing assets.</summary>
    public static class TextStudioTimings
    {
        /// <summary>Register a timing asset in the runtime tier.</summary>
        public static void Register(string id, TypingTimingsBase timings)
        {
            if (timings == null || string.IsNullOrWhiteSpace(id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.Timings.Register(id, timings);
        }

        /// <summary>Resolve a timing asset by identifier without logging.</summary>
        public static bool TryGet(string id, out TypingTimingsBase timings)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Timings.TryGetQuiet(id, out timings);
        }

        /// <summary>Remove the highest-priority user-owned timing asset.</summary>
        public static bool Unregister(string id)
        {
            return TextStudioRegistry.Timings.Unregister(id);
        }
    }
}
