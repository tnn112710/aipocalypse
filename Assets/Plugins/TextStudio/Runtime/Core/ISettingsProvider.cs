namespace TextStudio
{
    /// <summary>
    /// Settings cascade interface: Global SO → Shared SO → Local inline.
    /// Never cached - always read through provider for runtime swappability.
    /// Adopted from Febucci Text Animator's ISettingsProvider pattern.
    /// </summary>
    public interface ISettingsProvider<T> where T : struct
    {
        /// <summary>Resolves the settings currently effective for the provider.</summary>
        /// <returns>A value snapshot that callers may retain independently of the provider.</returns>
        T GetSettings();
    }
}
