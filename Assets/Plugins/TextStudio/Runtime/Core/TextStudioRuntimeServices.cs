namespace TextStudio
{
    /// <summary>
    /// Process-wide service overrides used by Text Studio runtime components.
    /// Assign <see cref="InputProvider"/> before playback to replace the default
    /// Unity input adapter for all Text Studio instances.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // The version-independent subsystem reset clears the active input provider for every Play session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioRuntimeServices
    {
        /// <summary>
        /// Gets or sets the input source used by actions that wait for user input.
        /// A <see langword="null"/> value restores the built-in Unity input adapter.
        /// </summary>
        public static ITextStudioInputProvider InputProvider { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForFastEnterPlayMode()
        {
            InputProvider = null;
        }
    }
}
