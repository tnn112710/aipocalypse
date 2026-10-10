namespace TextStudio
{
    /// <summary>Registers and resolves actions invoked by Text Studio markup.</summary>
    public static class TextStudioActions
    {
        /// <summary>Register an action in the runtime tier.</summary>
        public static void Register(ITextStudioAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.Actions.Register(action.Id, action);
        }

        /// <summary>Resolve an action by identifier without logging.</summary>
        public static bool TryGet(string id, out ITextStudioAction action)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.Actions.TryGetQuiet(id, out action);
        }

        /// <summary>Remove the highest-priority user-owned action.</summary>
        public static bool Unregister(string id)
        {
            return TextStudioRegistry.Actions.Unregister(id);
        }
    }
}
