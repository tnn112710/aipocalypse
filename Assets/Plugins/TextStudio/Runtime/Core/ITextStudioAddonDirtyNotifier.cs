namespace TextStudio
{
    /// <summary>
    /// Addon dirty notification interface.
    /// Addons signal the base controller when they need a presentation update.
    /// </summary>
    public interface ITextStudioAddonDirtyNotifier
    {
        /// <summary>Gets whether the add-on has pending presentation work.</summary>
        bool IsDirty { get; }

        /// <summary>Gets the minimum pipeline stages that must be refreshed.</summary>
        DirtyFlags AddonDirtyFlags { get; }

        /// <summary>Clears the pending state after the host has processed the requested stages.</summary>
        void ClearDirty();
    }
}
